using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using SimCore;
using Xianxia.UI;

namespace Xianxia.Game
{
    /// <summary>
    /// 游戏总装：世界时钟、相机、触控、与界面的唯一连接点。
    /// 规则全部在 SimCore（纯 C#，headless 可验）；这里只负责「把状态画出来、把点击变成命令」。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I;

        public World W { get; private set; }
        public WorldView View { get; private set; }
        public Hud Hud { get; private set; }

        /// <summary>1x 下 30 秒 = 1 月（6 分钟 = 1 年）。洞府/小世界另有内时。</summary>
        public const float SecPerMonth = 30f;

        private Camera _cam;
        private float _acc;
        private int _speed = 1;
        private bool _paused;
        private int _selected = -1;
        private float _zoom = 9.2f;
        private Vector2 _focus = new(16f, 16f);
        private bool _focusInit;

        private readonly Dictionary<int, Vector3> _pos = new();
        private readonly Dictionary<int, Vector3> _prev = new();
        private Vector3 _playerVis;
        private readonly List<Vector2Int> _path = new();
        private int _pathIx;
        private float _stepT = 99f;

        private Vector2 _touchStart;
        private bool _dragging;
        private float _pinch0 = -1f;
        private float _syncT;
        private int _lastSeason = -1;

        public int Selected => _selected;
        public bool Paused => _paused;
        public int Speed => _speed;
        public float MonthFrac => Mathf.Clamp01(_acc / SecPerMonth);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameApp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;

            BuildCamera();
            W = new World(20261124L);
            // 洪荒开局：不做几十年的预热，世界从第一年开始，你是最早醒来的那一批人之一
            W.BeginLife();

            View = gameObject.AddComponent<WorldView>();
            View.Init(W);

            Hud = gameObject.AddComponent<Hud>();
            Hud.Build(this);

            SyncPositions(true);
            FocusOnPlayer(true);
            Hud.SyncNow();
        }

        private void BuildCamera()
        {
            var go = new GameObject("WorldCamera");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = _zoom;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Tok.Ink;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 260f;
            // 正交等距：34° 俯角 + 45° 偏转，人物尺寸恒定、无透视畸变
            go.transform.rotation = Quaternion.Euler(34f, 45f, 0f);
            go.transform.position = new Vector3(16f, 0f, 16f) - go.transform.forward * 60f;
        }

        private void Update()
        {
            if (W == null) return;

            HandleClock();
            HandleInput();
            StepWalk();
            SyncPositions(false);
            UpdateCamera();
            View.RenderPawns(W.Npcs, _pos, _selected, Time.frameCount);
            View.Tick();

            if (_lastSeason != (int)W.Season)
            {
                _lastSeason = (int)W.Season;
                View.BuildProps();
            }

            _syncT += Time.deltaTime;
            if (_syncT > 0.25f) { _syncT = 0f; Hud.Sync(); }
        }

        private void HandleClock()
        {
            if (_paused) return;
            _acc += Time.deltaTime * _speed * (W.Player.NodeId >= 0 && NodeFlow() > 1f ? 1f : 1f);
            while (_acc >= SecPerMonth)
            {
                _acc -= SecPerMonth;
                SnapshotPrev();
                bool wasDead = W.Player.Dead;
                W.Tick(1);
                SyncPositions(true);
                if (!wasDead && W.Player.Dead) Hud.ShowVerdict();
                if (W.Player.Dead && !wasDead) break;
            }
        }

        private float NodeFlow()
        {
            int id = W.Player.NodeId;
            return id >= 0 && id < W.Worlds.Count ? W.Worlds[id].TimeScale : 1f;
        }

        public void SetSpeed(int s) { _speed = Mathf.Clamp(s, 1, 4); _paused = false; Hud.SyncNow(); }
        public void TogglePause() { _paused = !_paused; Hud.SyncNow(); }

        /// <summary>界面上一切行动的唯一入口。</summary>
        public string DoAct(string verb, int arg = 0)
        {
            string msg = W.Act(verb, arg);
            if (W.Player.Dead) Hud.ShowVerdict();
            switch (verb)
            {
                case "open":
                case "cave":
                case "flow":
                case "reincarnate":
                    View.BuildProps();
                    FocusOnPlayer(false);
                    break;
            }
            Hud.Toast(msg);
            SyncPositions(true);
            Hud.SyncNow();
            return msg;
        }

        public void Select(int id)
        {
            _selected = id;
            Hud.SyncNow();
        }

        public Npc SelectedNpc => _selected > 0 ? W.Npcs.Find(n => n.Id == _selected) : null;

        // ------------------------------------------------------------------ 位置与时钟
        private void SnapshotPrev()
        {
            _prev.Clear();
            foreach (var n in W.Npcs)
                if (n.Alive) _prev[n.Id] = new Vector3(n.Gx, 0f, n.Gy);
        }

        private void SyncPositions(bool hard)
        {
            float frac = hard ? 1f : MonthFrac;
            foreach (var n in W.Npcs)
            {
                if (!n.Alive) { _pos.Remove(n.Id); continue; }
                var cur = new Vector3(n.Gx, 0f, n.Gy);
                Vector3 from = _prev.TryGetValue(n.Id, out var p) ? p : cur;
                Vector3 v = Vector3.Lerp(from, cur, frac);
                v.y = View.HeightAt((int)v.x, (int)v.z) + 0.02f;
                _pos[n.Id] = v;
            }
            if (W.PlayerNpc != null && _pos.TryGetValue(W.PlayerNpc.Id, out var pv)) _playerVis = pv;
        }

        // ------------------------------------------------------------------ 相机
        private void UpdateCamera()
        {
            if (!_focusInit) FocusOnPlayer(true);
            var rot = _cam.transform.rotation;
            var pos = new Vector3(_focus.x, 0f, _focus.y) - rot * Vector3.forward * 60f;
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, Time.deltaTime * 6f);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _zoom, Time.deltaTime * 8f);
            float m = 2.5f;
            _focus.x = Mathf.Clamp(_focus.x, -m, World.W + m);
            _focus.y = Mathf.Clamp(_focus.y, -m, World.H + m);
        }

        public void FocusOnPlayer(bool instant)
        {
            var p = W.PlayerNpc;
            if (p == null) return;
            _focus = new Vector2(p.Gx, p.Gy);
            _focusInit = true;
            if (instant)
            {
                var rot = _cam.transform.rotation;
                _cam.transform.position = new Vector3(_focus.x, 0f, _focus.y) - rot * Vector3.forward * 60f;
            }
        }

        private void PanCamera(Vector2 delta)
        {
            float k = _zoom * 2f / Mathf.Max(1f, Screen.height);
            var right = new Vector2(0.7071f, 0.7071f);
            var fwd = new Vector2(0.7071f, -0.7071f);
            _focus -= right * delta.x * k + fwd * delta.y * k;
        }

        // ------------------------------------------------------------------ 触控
        private void HandleInput()
        {
            if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began || _pinch0 < 0f) _pinch0 = d;
                else if (Mathf.Abs(d - _pinch0) > 1f)
                {
                    _zoom = Mathf.Clamp(_zoom * (_pinch0 / Mathf.Max(1f, d)), 4.5f, 16f);
                    _pinch0 = d;
                }
                _dragging = true;
                return;
            }
            _pinch0 = -1f;

            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _touchStart = t.position;
                        _dragging = OverUi(t.fingerId);
                        break;
                    case TouchPhase.Moved:
                        if (!_dragging && (t.position - _touchStart).magnitude > 26f) _dragging = true;
                        if (_dragging && !OverUi(t.fingerId)) PanCamera(t.deltaPosition);
                        break;
                    case TouchPhase.Ended:
                        if (!_dragging && !OverUi(t.fingerId)) Tap(t.position);
                        _dragging = false;
                        break;
                }
                return;
            }

            // 编辑器/桌面上用鼠标走同一条链路
            if (Input.GetMouseButtonDown(0))
            {
                _touchStart = Input.mousePosition;
                _dragging = OverUi(-1);
            }
            else if (Input.GetMouseButton(0))
            {
                var d = (Vector2)Input.mousePosition - _touchStart;
                if (!_dragging && d.magnitude > 26f) _dragging = true;
                if (_dragging && !OverUi(-1)) PanCamera(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 20f);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (!_dragging && !OverUi(-1)) Tap(Input.mousePosition);
                _dragging = false;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom - wheel * 0.9f, 4.5f, 16f);
        }

        private static bool OverUi(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return fingerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(fingerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private void Tap(Vector2 screenPos)
        {
            var ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.18f, 0f));
            if (!plane.Raycast(ray, out float dist)) return;
            var hit = ray.GetPoint(dist);

            // 先选人：点到谁就是谁
            int best = -1; float bd = 0.85f;
            foreach (var n in W.Npcs)
            {
                if (!n.Alive || !_pos.TryGetValue(n.Id, out var p)) continue;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(hit.x, hit.z));
                if (d < bd) { bd = d; best = n.Id; }
            }
            if (best > 0) { Select(best); return; }

            int gx = Mathf.FloorToInt(hit.x), gy = Mathf.FloorToInt(hit.z);
            if (!W.Walkable(gx, gy)) return;
            if (!Cultivation.CanMove(W.PlayerNpc?.Kind ?? BeingKind.Human)) { Hud.Toast("根扎在此，动不了。"); return; }
            BuildPath(gx, gy);
        }

        private void BuildPath(int tx, int ty)
        {
            var p = W.PlayerNpc;
            if (p == null) return;
            int sx = Mathf.RoundToInt(p.Gx), sy = Mathf.RoundToInt(p.Gy);
            _path.Clear(); _pathIx = 0;
            if (sx == tx && sy == ty) return;

            var prev = new Dictionary<long, long>();
            var q = new Queue<Vector2Int>();
            var start = new Vector2Int(sx, sy);
            q.Enqueue(start);
            prev[Key(sx, sy)] = -1;
            int guard = 0;
            while (q.Count > 0 && guard++ < 4096)
            {
                var c = q.Dequeue();
                if (c.x == tx && c.y == ty) break;
                for (int d = 0; d < 4; d++)
                {
                    int nx = c.x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = c.y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (!W.Walkable(nx, ny)) continue;
                    long k = Key(nx, ny);
                    if (prev.ContainsKey(k)) continue;
                    prev[k] = Key(c.x, c.y);
                    q.Enqueue(new Vector2Int(nx, ny));
                }
            }
            long cur = Key(tx, ty);
            if (!prev.ContainsKey(cur)) return;
            var rev = new List<Vector2Int>();
            while (cur != Key(sx, sy) && cur >= 0 && rev.Count < 256)
            {
                rev.Add(new Vector2Int((int)(cur / 1000), (int)(cur % 1000)));
                cur = prev[cur];
            }
            rev.Reverse();
            _path.AddRange(rev);
            _pathIx = 0;
            _stepT = 0f;
        }

        private static long Key(int x, int y) => x * 1000L + y;

        private void StepWalk()
        {
            if (_path.Count == 0 || W.Player.Dead) return;
            _stepT += Time.deltaTime;
            if (_stepT < 0.16f) return;
            _stepT = 0f;
            var p = W.PlayerNpc;
            if (p == null) { _path.Clear(); return; }
            var next = _path[_pathIx];
            int dx = next.x - Mathf.RoundToInt(p.Gx);
            int dy = next.y - Mathf.RoundToInt(p.Gy);
            string r = null;
            if (dx == -1) r = W.Act("move", 0);
            else if (dx == 1) r = W.Act("move", 1);
            else if (dy == -1) r = W.Act("move", 2);
            else if (dy == 1) r = W.Act("move", 3);
            if (r == null || !r.StartsWith("行至")) _path.Clear();
            else _pathIx++;
            if (_pathIx >= _path.Count) _path.Clear();
            SyncPositions(true);
        }
    }
}