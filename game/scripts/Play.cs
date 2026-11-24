using System;
using System.Collections.Generic;
using Godot;
using SimCore;

namespace Xianxia;

/// <summary>
/// 可玩层：把 SimCore 的一世接成能够用手指操作的界面。
/// 地图可拖动；点地面走过去；点人则选人；底部一排命令；死出判词后可按"投生"。
/// </summary>
public partial class Main
{
    private Vector2 _cam;
    private float _zoom = 1f;
    private string _msg = "投生完成：你睁眼，先看清自己生在哪。";
    private float _msgAge = 999f;
    private readonly List<(Rect2 R, string Label, string Verb, int Arg)> _btns = new();
    private readonly List<(Vector2 At, float Age, int Element)> _fx = new();
    private readonly List<(Rect2 R, string Label, string Verb, int Arg)> _btnsRef = new();

    private Npc Ply => _w.PlayerNpc;

    private void PlaySetup()
    {
        _w.BeginLife();
        _selected = _w.Player.NpcId;
        CenterOnPlayer();
    }

    /// <summary>镜头对准"你"：出生与投生都要把画面拉回主角，否则玩家找不到自己。</summary>
    private void CenterOnPlayer()
    {
        var pn = Ply;
        if (pn == null) return;
        var vp = GetViewportRect().Size;
        float sx = (pn.Gx + 0.5f - (pn.Gy + 0.5f)) * TW * 0.5f;
        float sy = (pn.Gx + 0.5f + pn.Gy + 0.5f) * TH * 0.5f;
        var iso = new Vector2(sx, sy) * _scale;
        _cam = new Vector2(vp.X * 0.5f, vp.Y * 0.255f) - (_origin + iso);
        QueueRedraw();
    }

    // ---------- 绘制 ----------

    private void PlayDraw(Vector2 vp)
    {
        float u = vp.X / 720f;

        // 玩家高亮环（谁是"你"）
        var pn = Ply;
        if (pn != null && pn.Alive && !_w.Player.Dead)
        {
            var p = Iso(pn.Gx + 0.5f, pn.Gy + 0.5f);
            DrawCircle(p, 16f * u, new Color(0.98f, 0.82f, 0.42f, 0.16f));
            DrawArc(p, 15f * u, 0f, Mathf.Tau, 32, new Color(1f, 0.85f, 0.45f, 0.92f), 2.2f * u);
            DrawString(_font, p + new Vector2(-16f * u, -22f * u), "你", HorizontalAlignment.Left, -1, (int)(16f * u), new Color(1f, 0.88f, 0.55f));
        }

        // 年份与内时（时间才会让世界变，屏幕不能静止）
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.196f),
                   $"Y{_w.Year}　境界 {(pn?.Realm ?? 0)}　寿{(pn?.Age ?? 0)}/{pn?.Lifespan ?? 0}　粟{_w.Player.Grain} 石{_w.Player.Stone}",
                   HorizontalAlignment.Left, -1, (int)(15f * u), new Color(0.85f, 0.92f, 0.86f, 0.9f));

        // 命令条（两行）
        _btns.Clear();
        DrawCommands(vp, u);

        // 消息条
        var mc = new Color(0.94f, 0.90f, 0.78f, 0.92f);
        DrawString(_font, new Vector2(vp.X * 0.04f, vp.Y * 0.442f), _msg,
                   HorizontalAlignment.Left, (int)(vp.X * 0.92f), (int)(14f * u), mc);

        // 功法特效：突破时按功法属性上屏（金白/木青/水蓝/火赤/土黄）
        DrawTechFx(u);

        // 判词 / 投生
        if (_w.Player.Dead) DrawVerdict(vp, u);
    }

    private void DrawTechFx(float u)
    {
        for (int i = _fx.Count - 1; i >= 0; i--)
        {
            var f = _fx[i];
            float k = f.Age / 0.75f;
            if (k >= 1f) { _fx.RemoveAt(i); continue; }
            var col = f.Element switch
            {
                0 => new Color(0.95f, 0.90f, 0.65f),   // 金
                1 => new Color(0.55f, 0.85f, 0.55f),   // 木
                2 => new Color(0.55f, 0.75f, 0.95f),   // 水
                3 => new Color(0.95f, 0.55f, 0.40f),   // 火
                4 => new Color(0.85f, 0.75f, 0.45f),   // 土
                5 => new Color(0.70f, 0.90f, 0.85f),   // 风
                6 => new Color(0.75f, 0.70f, 0.95f),   // 雷
                _ => new Color(0.95f, 0.85f, 0.60f)
            };
            float r = (12f + 46f * k) * u;
            DrawArc(f.At, r, 0f, Mathf.Tau, 32, col with { A = (1f - k) * 0.9f }, (3f - 2f * k) * u);
            for (int s = 0; s < 6; s++)
            {
                float a = s * Mathf.Tau / 6f + k * 2f;
                var p = f.At + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                DrawCircle(p, (3.5f - 2.5f * k) * u, col with { A = (1f - k) * 0.85f });
            }
        }
    }

    private void DrawCommands(Vector2 vp, float u)
    {
        const float top = 0.452f, h = 0.046f, w = 0.192f;
        var spec = new (string L, string V)[]
        {
            ("修炼", "meditate"), ("帮人", "help"), ("拒人", "refuse"), ("夺取", "rob"), ("占府", "cave"),
            ("入宗", "sect"), ("开辟", "open"), ("过门", "gate"), ("探索", "explore"), ("待年", "wait")
        };
        for (int i = 0; i < spec.Length; i++)
        {
            int col = i % 5, row = i / 5;
            var r = new Rect2(vp.X * (0.012f + col * (w + 0.006f)), vp.Y * (top + row * (h + 0.006f)), vp.X * w, vp.Y * h);
            bool dim = _w.Player.Dead;
            DrawRect(r, new Color(0.10f, 0.18f, 0.15f, dim ? 0.45f : 0.94f));
            DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, 1.5f)), new Color(0.55f, 0.78f, 0.63f, dim ? 0.2f : 0.5f));
            DrawString(_font, new Vector2(r.Position.X + 8f * u, r.Position.Y + r.Size.Y * 0.72f),
                       spec[i].L, HorizontalAlignment.Left, -1, (int)(16f * u), dim ? new Color(1, 1, 1, 0.35f) : C_Paper);
            if (!dim) _btns.Add((r, spec[i].L, spec[i].V, 0));
        }

        // 方向键：3×3 网格，只有上下左右四格可点
        float dq = vp.X * 0.058f;
        var dpad = new Rect2(vp.X * 0.985f - dq * 3f, vp.Y * 0.315f, dq * 3f, dq * 3f);
        DrawRect(dpad, new Color(0.07f, 0.13f, 0.11f, 0.86f));
        var dirCells = new (int dx, int dy, string L, int D)[] { (1, 0, "北", 0), (2, 1, "东", 1), (1, 2, "南", 2), (0, 1, "西", 3) };
        foreach (var dcell in dirCells)
        {
            var rr = new Rect2(dpad.Position.X + dcell.dx * dq + 1f, dpad.Position.Y + dcell.dy * dq + 1f, dq - 2f, dq - 2f);
            DrawRect(rr, new Color(0.15f, 0.26f, 0.21f, _w.Player.Dead ? 0.35f : 0.95f));
            DrawString(_font, rr.Position + new Vector2(dq * 0.30f, dq * 0.66f), dcell.L,
                       HorizontalAlignment.Left, -1, (int)(15f * u), _w.Player.Dead ? new Color(1,1,1,0.3f) : C_Paper);
            if (!_w.Player.Dead) _btns.Add((rr, dcell.L, "move", dcell.D));
        }
    }

    private void DrawVerdict(Vector2 vp, float u)
    {
        DrawRect(new Rect2(0, vp.Y * 0.10f, vp.X, vp.Y * 0.34f), new Color(0.02f, 0.03f, 0.03f, 0.88f));
        DrawString(_font, new Vector2(vp.X * 0.05f, vp.Y * 0.150f), "判词", HorizontalAlignment.Left, -1, (int)(20f * u), new Color(0.95f, 0.82f, 0.55f));
        DrawString(_font, new Vector2(vp.X * 0.05f, vp.Y * 0.190f), _w.Player.Verdict, HorizontalAlignment.Left, (int)(vp.X * 0.90f), (int)(15f * u), C_Paper);
        DrawString(_font, new Vector2(vp.X * 0.05f, vp.Y * 0.300f),
                   $"帮{_w.Player.Help} 拒{_w.Player.Refuse} 夺{_w.Player.Rob}　来世：{_w.Player.Fate}",
                   HorizontalAlignment.Left, -1, (int)(14f * u), new Color(0.75f, 0.85f, 0.78f));
        var r = new Rect2(vp.X * 0.55f, vp.Y * 0.340f, vp.X * 0.40f, vp.Y * 0.070f);
        DrawRect(r, new Color(0.72f, 0.38f, 0.24f, 0.95f));
        DrawString(_font, new Vector2(r.Position.X + 14f * u, r.Position.Y + r.Size.Y * 0.70f), "投生",
                   HorizontalAlignment.Left, -1, (int)(18f * u), new Color(1f, 0.95f, 0.85f));
        _btns.Add((r, "投生", "reincarnate", 0));
    }

    // ---------- 输入 ----------

    private bool PlayTap(Vector2 p, Vector2 vp)
    {
        foreach (var b in _btns)
        {
            if (!b.R.HasPoint(p)) continue;
            if (b.Verb == "reincarnate")
            {
                _w.Act("reincarnate");
                _msg = _w.Player.LastEvent;
            }
            else
            {
                _msg = _w.Act(b.Verb, b.Arg);
            }
            _msgAge = 0;
            if (_msg.Contains("突破") || b.Verb == "explore")
                _fx.Add((Iso((Ply?.Gx ?? 0) + 0.5f, (Ply?.Gy ?? 0) + 0.5f), 0f, (Ply?.Realm ?? 0) % 10));
            QueueRedraw();
            return true;
        }
        // 这里只处理命令区与地图区；面板区交回 TapPanel
        if (p.Y > vp.Y * 0.545f) return false;
        if (p.Y > vp.Y * 0.44f) return true;            // 消息条空白不误触
        if (!_w.Player.Dead) StepToward(p);
        return true;
    }

    /// <summary>点哪儿走哪儿：朝点击的格子迈一步（一次一年，世界一起走）。

    private void StepToward(Vector2 sp)
    {
        var pn = Ply;
        if (pn == null) return;
        int cx = (int)pn.Gx, cy = (int)pn.Gy;
        int bx = -1, by = -1; float bestD = 1e9f;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                int nx = cx + dx, ny = cy + dy;
                if (!_w.Walkable(nx, ny)) continue;
                var q = Iso(nx + 0.5f, ny + 0.5f);
                float d = q.DistanceTo(sp);
                if (d < bestD) { bestD = d; bx = nx; by = ny; }
            }
        if (bx < 0) { _msg = "四面皆阻，无处可去。"; return; }
        int dir = bx > cx ? 1 : bx < cx ? 3 : by > cy ? 2 : 0;
        _msg = _w.Act("move", dir);
        _msgAge = 0;
    }

    /// <summary>相机：单指拖动平移，双指捏合缩放。地图大于一屏，必须能拖。</summary>
    private bool PlayDrag(InputEvent e)
    {
        if (e is InputEventScreenDrag d)
        {
            _cam += d.Relative;
            QueueRedraw();
            return true;
        }
        if (e is InputEventMagnifyGesture m)
        {
            _zoom = Mathf.Clamp(_zoom * m.Factor, 0.55f, 2.6f);
            Resize();
            QueueRedraw();
            return true;
        }
        return false;
    }
}
