using System;
using System.Collections.Generic;
using Godot;
using SimCore;

namespace Xianxia;

/// <summary>
/// 可玩层 HUD：可见摇杆（连续走）+ 点击寻路（BFS）+ 视角/高度 + 小地图 + NPC 详情 + 说明条。
/// 三块输入互不抢：左下摇杆=连续走，空白拖动=挪地图，空白点击=寻路，点人=看详情。
/// </summary>
public partial class Main
{
    private Vector2 _cam;
    private float _zoom = 1f;
    private float _alt;
    private float _altSmooth;
    private int _viewMode;
    private string _msg = "你睁眼，先看清自己生在哪。";
    private float _msgAge = 999f;
    private readonly List<(Rect2 R, string Label, string Verb, int Arg)> _btns = new();
    private readonly List<(Vector2 At, float Age, int Element)> _fx = new();
    private int _pressed = -1;
    private bool _clockRun = true;
    private int _clockSpeed = 1;

    private bool _stickOn;
    private int _stickTouch = -1;
    private Vector2 _stickBase, _stickKnob, _stickDir;
    private float _stickAcc;
    private readonly List<(int X, int Y)> _path = new();
    private float _pathAcc;
    private int _scene;          // 0 世界 1 洞府内景
    private int _dlgNpc = -1;
    private int _dlgRound;
    private string _dlgLine = "";

    private Npc Ply => _w.PlayerNpc;

    private void PlaySetup()
    {
        _w.BeginLife();
        _selected = _w.Player.NpcId;
        CenterOnPlayer();
    }

    private void CenterOnPlayer()
    {
        var pn = Ply;
        if (pn == null) return;
        var vp = GetViewportRect().Size;
        float sx = (pn.Gx + 0.5f - (pn.Gy + 0.5f)) * TW * 0.5f;
        float sy = (pn.Gx + 0.5f + pn.Gy + 0.5f) * TH * 0.5f;
        _cam = new Vector2(vp.X * 0.5f, vp.Y * 0.30f) - (_origin + new Vector2(sx, sy) * _scale);
        QueueRedraw();
    }

    private void PlayTick(float dt)
    {
        var vp = GetViewportRect().Size;
        var pn = Ply;
        _altSmooth = Mathf.Lerp(_altSmooth, _alt, Mathf.Min(1f, dt * 3.2f));
        if (pn == null || _w.Player.Dead) return;

        if (_stickOn && _stickDir.LengthSquared() > 0.02f)
        {
            _stickAcc += dt;
            if (_stickAcc >= 0.32f)
            {
                _stickAcc = 0f;
                float ang = Mathf.Atan2(_stickDir.Y, _stickDir.X);
                int dir = ang > -0.78f && ang < 0.78f ? 1
                        : ang >= 0.78f && ang < 2.36f ? 2
                        : ang <= -0.78f && ang > -2.36f ? 0 : 3;
                string m = _w.ActTimed("move", dir, 0.5f);
                if (m.Length > 0) { _msg = m; _msgAge = 0f; }
                _path.Clear();
            }
        }
        if (_path.Count > 0)
        {
            _pathAcc += dt;
            if (_pathAcc >= 0.26f)
            {
                _pathAcc = 0f;
                var cell = _path[0];
                int dx = Math.Sign(cell.X - (int)pn.Gx), dy = Math.Sign(cell.Y - (int)pn.Gy);
                int dir = dx > 0 ? 1 : dx < 0 ? 3 : dy > 0 ? 2 : 0;
                string m = _w.ActTimed("move", dir, 0.5f);
                if (m.Length > 0 && !m.Contains("行至")) { _msg = m; _msgAge = 0f; _path.Clear(); }
                if ((int)pn.Gx == cell.X && (int)pn.Gy == cell.Y) _path.RemoveAt(0);
            }
        }
        float sx2 = (pn.Gx + 0.5f - (pn.Gy + 0.5f)) * TW * 0.5f;
        float sy2 = (pn.Gx + 0.5f + pn.Gy + 0.5f) * TH * 0.5f;
        var want = new Vector2(vp.X * 0.5f, vp.Y * 0.30f) - (_origin + new Vector2(sx2, sy2) * _scale);
        _cam = _cam.Lerp(want, Mathf.Min(1f, dt * 2.4f));
    }

    private void PlayDraw(Vector2 vp)
    {
        float u = vp.X / 720f;
        if (_scene != 0) { DrawInterior(vp, u); return; }
        DrawSky(u);
        DrawCloseDetail(u);
        DrawWeather(vp, u);
        DrawWorldFx(u);
        DrawSelection(u);
        DrawStatusCard(vp, u);
        DrawClock(vp, u);
        DrawMinimap(vp, u);
        DrawCommands(vp, u);
        DrawStick(vp, u);
        DrawViewPad(vp, u);
        DrawHint(vp, u);
        DrawToast(vp, u);
        if (_dlgNpc >= 0 && !_w.Player.Dead) DrawDialogue(vp, u);
        DrawPanels(vp, u);
        if (_w.Player.Dead) DrawVerdict(vp, u);
        else DrawPlayerRing(u);
    }

    private void DrawSky(float u)
    {
        float k = Mathf.Clamp(_altSmooth / 2f, 0f, 1f);
        if (k <= 0.001f) return;
        var vp = GetViewportRect().Size;
        var sky = new Color(0.20f, 0.30f, 0.40f).Lerp(new Color(0.52f, 0.62f, 0.78f), Mathf.Min(1f, _altSmooth * 0.5f));
        for (int i = 0; i < 16; i++)
        {
            float t = i / 15f;
            DrawRect(new Rect2(0, vp.Y * 0.52f - vp.Y * 0.30f * k * t, vp.X, vp.Y * 0.025f + 2f),
                     sky with { A = 0.75f * k * (1f - t * 0.4f) });
        }
        for (int c = 0; c < 3; c++)
        {
            float sp = 6f + c * 9f;
            float y = vp.Y * (0.16f + c * 0.10f) + Mathf.Sin(_t * 0.3f + c) * 6f * u;
            float x = Mathf.PosMod(_t * sp * u + c * 260f * u, vp.X + 300f * u) - 150f * u;
            for (int b = 0; b < 4; b++)
                DrawCircle(new Vector2(x + b * 26f * u, y + Mathf.Sin(b * 2f) * 6f * u),
                           (26f - b * 4f) * u, new Color(1f, 1f, 1f, 0.10f * k));
        }
        if (k > 0.2f)
            for (int s = 0; s < 5; s++)
            {
                float x = Mathf.PosMod(_t * 14f * u + s * 180f * u, vp.X + 240f * u) - 120f * u;
                RR(new Rect2(x, vp.Y * (0.30f + s * 0.06f), 130f * u, 54f * u), 30f * u, new Color(0f, 0f, 0f, 0.10f * k));
            }
    }

    // 近景：放大到 _zoom>1.5 时，地面补草叶/碎石/花，不再是一格一张图
    private void DrawCloseDetail(float u)
    {
        if (_zoom < 1.45f) return;
        float k = Mathf.Clamp((_zoom - 1.45f) / 0.6f, 0f, 1f);
        float sc = _scale;
        for (int x = 0; x < World.W; x++)
            for (int y = 0; y < World.H; y++)
            {
                var t = _w.Tiles[x, y];
                var c = Iso(x + 0.5f, y + 0.5f);
                if (c.X < -40 || c.X > GetViewportRect().Size.X + 40 || c.Y < 0 || c.Y > GetViewportRect().Size.Y) continue;
                float h1 = Hash2(x * 13 + 7, y * 29 + 3), h2 = Hash2(x * 3 + 1, y * 17 + 11);
                if (t == Terrain.Grass)
                {
                    for (int b = 0; b < 5; b++)
                    {
                        float bx = (Hash2(x * 5 + b, y * 7 + 2) - 0.5f) * 26f * sc;
                        float by = (Hash2(x * 9 + b, y * 3 + 5) - 0.5f) * 12f * sc;
                        var col = new Color(0.42f, 0.62f, 0.40f, 0.55f * k);
                        DrawLine(c + new Vector2(bx, by) + new Vector2(0, 3f * sc), c + new Vector2(bx + 1.2f * sc, by - 5f * sc), col, 1f * sc);
                    }
                    if (h1 > 0.82f)
                        DrawCircle(c + new Vector2((h2 - 0.5f) * 20f * sc, 4f * sc), 2.1f * sc, new Color(0.92f, 0.82f, 0.62f, 0.5f * k));
                }
                else if (t == Terrain.Sand || t == Terrain.Rock)
                    for (int b = 0; b < 3; b++)
                    {
                        float bx = (Hash2(x * 11 + b, y * 5 + 9) - 0.5f) * 24f * sc;
                        float by = (Hash2(x * 7 + b, y * 13 + 4) - 0.5f) * 12f * sc;
                        DrawCircle(c + new Vector2(bx, by), (1.3f + h2) * sc, new Color(0.72f, 0.70f, 0.64f, 0.35f * k));
                    }
            }
    }

    // 洞府内景：独立场景（不是同一张地图放大），12 位装修真的摆在这屋里
    // ---------------------------------------------------------------- 场所内景：1 洞府 2 坊市 3 钱庄 4 山门 5 宅院 6 飞舟舱
    private void DrawInterior(Vector2 vp, float u)
    {
        var floorC = new Color(0.16f, 0.20f, 0.17f);
        var wallC = new Color(0.10f, 0.14f, 0.13f);
        var glowC = new Color(0.55f, 0.80f, 0.68f);
        switch (_scene)
        {
            case 2: floorC = new Color(0.30f, 0.23f, 0.15f); wallC = new Color(0.17f, 0.12f, 0.08f); glowC = new Color(0.95f, 0.72f, 0.36f); break;
            case 3: floorC = new Color(0.14f, 0.19f, 0.20f); wallC = new Color(0.08f, 0.12f, 0.13f); glowC = new Color(0.86f, 0.74f, 0.40f); break;
            case 4: floorC = new Color(0.19f, 0.21f, 0.19f); wallC = new Color(0.12f, 0.15f, 0.13f); glowC = new Color(0.62f, 0.86f, 0.80f); break;
            case 5: floorC = new Color(0.22f, 0.16f, 0.14f); wallC = new Color(0.14f, 0.09f, 0.08f); glowC = new Color(0.94f, 0.60f, 0.42f); break;
            case 6: floorC = new Color(0.13f, 0.15f, 0.22f); wallC = new Color(0.09f, 0.10f, 0.16f); glowC = new Color(0.70f, 0.78f, 0.96f); break;
        }

        DrawRect(new Rect2(0, 0, vp.X, vp.Y), new Color(0.03f, 0.045f, 0.04f));
        // 墙面：竖向木构/石构，加窗光
        DrawRect(new Rect2(0, vp.Y * 0.14f, vp.X, vp.Y * 0.365f), wallC);
        for (int i = 1; i < 9; i++)
        {
            float x = vp.X * i / 9f;
            DrawLine(new Vector2(x, vp.Y * 0.14f), new Vector2(x, vp.Y * 0.505f), new Color(0f, 0f, 0f, 0.22f), 2f);
        }
        for (int i = 0; i < 5; i++)
            DrawCircle(new Vector2(vp.X * 0.5f, vp.Y * 0.32f), vp.X * (0.13f + i * 0.055f), glowC with { A = 0.075f - i * 0.012f });
        // 地面（透视梯形）
        var tl = new Vector2(vp.X * 0.10f, vp.Y * 0.505f);
        var tr = new Vector2(vp.X * 0.90f, vp.Y * 0.505f);
        var br = new Vector2(vp.X * 1.05f, vp.Y * 0.86f);
        var bl = new Vector2(vp.X * -0.05f, vp.Y * 0.86f);
        DrawColoredPolygon(new[] { tl, tr, br, bl }, floorC);
        for (int i = 1; i < 9; i++)
        {
            float t = i / 9f;
            var a = tl.Lerp(bl, t); var b = tr.Lerp(br, t);
            DrawLine(a, b, new Color(0f, 0f, 0f, 0.26f), 1.2f);
        }
        DrawLine(tl, tr, new Color(0f, 0f, 0f, 0.45f), 2f);
        DrawRect(new Rect2(0, vp.Y * 0.14f, vp.X, vp.Y * 0.03f), new Color(0f, 0f, 0f, 0.35f));
        DrawColoredPolygon(new[] { tl, tr, new Vector2(tr.X, tr.Y + vp.Y * 0.05f), new Vector2(tl.X, tl.Y + vp.Y * 0.05f) },
                           new Color(1f, 0.94f, 0.82f, 0.045f));

        // 各场所的陈设（每处不同，看得出是什么地方）
        switch (_scene)
        {
            case 2:  // 坊市：四座摊棚、货箱、幌子
                for (int i = 0; i < 4; i++)
                {
                    float x = vp.X * (0.16f + i * 0.23f), y = vp.Y * (0.50f + (i % 2) * 0.10f);
                    var bw = 62f * u;
                    DrawColoredPolygon(new[] { new Vector2(x - bw, y), new Vector2(x + bw, y),
                        new Vector2(x + bw * 0.7f, y - 26f * u), new Vector2(x - bw * 0.7f, y - 26f * u) },
                        i % 2 == 0 ? new Color(0.55f, 0.22f, 0.16f, 0.75f) : new Color(0.24f, 0.34f, 0.44f, 0.75f));
                    DrawRect(new Rect2(x - bw, y, bw * 2f, 16f * u), new Color(0.28f, 0.20f, 0.13f));
                    for (int k = 0; k < 3; k++)
                        DrawRect(new Rect2(x - 40f * u + k * 26f * u, y - 14f * u, 18f * u, 12f * u),
                                 new Color(0.62f, 0.52f, 0.30f, 0.9f));
                }
                break;
            case 3:  // 钱庄：长柜台、算盘、库门光圈、石堆
                DrawRect(new Rect2(vp.X * 0.08f, vp.Y * 0.56f, vp.X * 0.84f, 46f * u), new Color(0.30f, 0.22f, 0.14f));
                DrawRect(new Rect2(vp.X * 0.08f, vp.Y * 0.56f, vp.X * 0.84f, 8f * u), new Color(0.62f, 0.48f, 0.26f));
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 9; c++)
                        DrawRect(new Rect2(vp.X * 0.14f + c * 22f * u, vp.Y * 0.47f + r * 13f * u, 14f * u, 8f * u), new Color(0.52f, 0.42f, 0.26f, 0.85f));
                DrawCircle(new Vector2(vp.X * 0.80f, vp.Y * 0.30f), 52f * u, new Color(0.10f, 0.14f, 0.13f));
                DrawArc(new Vector2(vp.X * 0.80f, vp.Y * 0.30f), 52f * u, 0f, Mathf.Tau, 40, glowC with { A = 0.7f }, 3f);
                for (int i = 0; i < 4; i++)
                    DrawRect(new Rect2(vp.X * 0.72f + i * 18f * u, vp.Y * 0.62f - i * 5f * u, 26f * u, 10f * u), new Color(0.68f, 0.72f, 0.78f, 0.9f));
                break;
            case 4:  // 山门：牌坊、石阶、香炉、旗
                DrawRect(new Rect2(vp.X * 0.30f, vp.Y * 0.22f, vp.X * 0.40f, 16f * u), new Color(0.22f, 0.26f, 0.24f));
                DrawRect(new Rect2(vp.X * 0.34f, vp.Y * 0.20f, vp.X * 0.32f, 26f * u), new Color(0.16f, 0.22f, 0.20f));
                DrawString(_font, new Vector2(vp.X * 0.34f, vp.Y * 0.20f + 19f * u), "宗　门",
                           HorizontalAlignment.Center, (int)(vp.X * 0.32f), (int)(15f * u), glowC);
                for (int i = 0; i < 2; i++)
                {
                    float x = vp.X * (i == 0 ? 0.30f : 0.686f);
                    DrawRect(new Rect2(x, vp.Y * 0.235f, 14f * u, vp.Y * 0.23f), new Color(0.20f, 0.23f, 0.21f));
                }
                for (int i = 0; i < 5; i++)
                    DrawRect(new Rect2(vp.X * 0.34f + i * vp.X * 0.064f, vp.Y * 0.50f + i * 13f * u, vp.X * 0.32f - i * 2f * u, 12f * u), new Color(0.24f, 0.27f, 0.25f, 0.92f));
                DrawCircle(new Vector2(vp.X * 0.16f, vp.Y * 0.56f), 20f * u, new Color(0.30f, 0.24f, 0.18f));
                for (int i = 0; i < 3; i++)
                    DrawCircle(new Vector2(vp.X * 0.16f + Mathf.Sin(_t * 0.6f + i) * 8f * u, vp.Y * 0.50f - i * 12f * u), (7f - i * 1.5f) * u, new Color(0.80f, 0.84f, 0.86f, 0.18f));
                break;
            case 5:  // 宅院：梁、祖宗龛、长桌、灯笼
                DrawRect(new Rect2(vp.X * 0.06f, vp.Y * 0.13f, vp.X * 0.88f, 10f * u), new Color(0.36f, 0.20f, 0.14f));
                DrawRect(new Rect2(vp.X * 0.36f, vp.Y * 0.18f, vp.X * 0.28f, 34f * u), new Color(0.24f, 0.13f, 0.10f));
                for (int i = 0; i < 5; i++)
                    DrawRect(new Rect2(vp.X * 0.40f + i * vp.X * 0.04f, vp.Y * 0.185f + 6f * u, vp.X * 0.028f, 22f * u), new Color(0.58f, 0.44f, 0.28f));
                DrawRect(new Rect2(vp.X * 0.24f, vp.Y * 0.585f, vp.X * 0.52f, 34f * u), new Color(0.30f, 0.17f, 0.12f));
                for (int i = 0; i < 2; i++)
                {
                    float x = vp.X * (i == 0 ? 0.14f : 0.86f);
                    DrawCircle(new Vector2(x, vp.Y * 0.24f), 16f * u, new Color(0.86f, 0.30f, 0.22f, 0.85f));
                    DrawCircle(new Vector2(x, vp.Y * 0.24f), 30f * u, new Color(0.96f, 0.60f, 0.34f, 0.10f));
                }
                break;
            case 6:  // 飞舟：舷窗、云海、舵盘、舱毯
                for (int i = 0; i < 2; i++)
                    for (int k = 0; k < 2; k++)
                    {
                        var pc2 = new Vector2(vp.X * (0.30f + k * 0.40f), vp.Y * (0.24f + i * 0.10f));
                        DrawCircle(pc2, 40f * u, new Color(0.06f, 0.09f, 0.14f));
                        DrawArc(pc2, 40f * u, 0f, Mathf.Tau, 36, new Color(0.62f, 0.72f, 0.88f, 0.8f), 3f);
                        for (int c = 0; c < 3; c++)
                        {
                            float off = Mathf.PosMod(_t * 26f + c * 26f + i * 10f, 72f);
                            DrawCircle(pc2 + new Vector2(-40f * u + off * u, 10f * u - c * 8f * u), (14f + c * 4f) * u,
                                       new Color(0.92f, 0.94f, 0.98f, 0.16f));
                        }
                    }
                DrawCircle(new Vector2(vp.X * 0.50f, vp.Y * 0.60f), 26f * u, new Color(0.42f, 0.34f, 0.22f));
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.Pi / 3f;
                    DrawLine(new Vector2(vp.X * 0.50f, vp.Y * 0.60f), new Vector2(vp.X * 0.50f + Mathf.Cos(ang) * 34f * u, vp.Y * 0.60f + Mathf.Sin(ang) * 34f * u), new Color(0.52f, 0.42f, 0.28f), 2f);
                }
                for (int i = 0; i < 5; i++)
                    DrawRect(new Rect2(vp.X * (0.08f + i * 0.018f), vp.Y * (0.52f + i * 0.06f), vp.X * 0.84f, 5f * u), glowC with { A = 0.16f });
                break;
            default:  // 洞府：十二位
                var things = new[] { "蒲团", "书架", "药炉", "灵泉", "屏风", "剑架" };
                for (int i = 0; i < 12; i++)
                {
                    int col = i % 4, row = i / 4;
                    float x = vp.X * (0.16f + col * 0.18f);
                    float y = vp.Y * (0.50f + row * 0.09f);
                    string cur2 = _w.Decor.Count > i ? _w.Decor[i] : "";
                    var r = new Rect2(x - 26f * u, y - 30f * u, 52f * u, 44f * u);
                    DrawColoredPolygon(new[] { new Vector2(r.Position.X, r.End.Y), new Vector2(r.End.X, r.End.Y),
                        new Vector2(r.End.X - 6f * u, r.Position.Y), new Vector2(r.Position.X + 6f * u, r.Position.Y) },
                        cur2.Length == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(0.30f, 0.42f, 0.34f, 0.55f));
                    if (cur2.Length > 0)
                    {
                        DrawRect(new Rect2(r.Position.X + 8f * u, r.Position.Y + 6f * u, r.Size.X - 16f * u, 8f * u), new Color(0.55f, 0.72f, 0.58f, 0.7f));
                        DrawString(_font, new Vector2(r.Position.X + 4f * u, r.Position.Y + 26f * u), cur2,
                                   HorizontalAlignment.Left, -1, (int)(9.5f * u), UI_Jade);
                    }
                    _btns.Add((r, "", "decor", i));
                }
                break;
        }

        // 你站在屋里
        var pc = new Vector2(vp.X * 0.5f, vp.Y * 0.74f);
        DrawCircle(pc + new Vector2(3f, 4f) * u, 26f * u, new Color(0f, 0f, 0f, 0.25f));
        DrawColoredPolygon(new[] { pc + new Vector2(-12f, 34f) * u, pc + new Vector2(12f, 34f) * u,
            pc + new Vector2(9f, -10f) * u, pc + new Vector2(-9f, -10f) * u }, new Color(0.52f, 0.58f, 0.50f));
        DrawCircle(pc + new Vector2(0, -22f) * u, 9f * u, new Color(0.86f, 0.74f, 0.62f));

        string title, sub;
        switch (_scene)
        {
            case 2:
                title = "坊市 · 商帮与摊位";
                sub = $"天时 {_w.Weather}　灵植 {_w.PriceOf("灵植"):0.0}　丹药 {_w.PriceOf("丹药"):0.0}　符箓 {_w.PriceOf("符箓"):0.0}　法宝 {_w.PriceOf("法宝"):0.0}";
                break;
            case 3:
                title = "钱庄 · 灵石出入";
                sub = $"账上 {_w.Bank} 石　身上 {_w.Player.Stone} 石　成交 {_w.Traded} 笔";
                break;
            case 4:
                title = "山门 · 宗门议事";
                sub = $"抽成 {_w.TitheAvg * 100f:0}%　在册组织 {_w.Factions.Count}　宗门庇护凡间，凡间供资源";
                break;
            case 5:
                title = "宅院 · 家族内堂";
                sub = $"在册家族 {_w.Clans.Count}　嫡系分产业，旁支跑腿，族产是根";
                break;
            case 6:
                title = "飞舟 · 舱内";
                sub = $"舱纹「{_w.BoatStyle}」　起航 {_w.BoatTrips} 次　换一处市场，价就不同";
                break;
            default:
                title = _w.CaveName + " · 洞府内景";
                sub = $"内时 {(Ply?.TimeScale ?? 1f):0}×　十二位可安：蒲团·书架·药炉·灵泉·屏风·剑架";
                break;
        }
        DrawString(_font, new Vector2(vp.X * 0.06f, vp.Y * 0.198f), title, HorizontalAlignment.Left, -1, (int)(18f * u), UI_Gold);
        DrawString(_font, new Vector2(vp.X * 0.06f, vp.Y * 0.232f), sub, HorizontalAlignment.Left, (int)(vp.X * 0.88f), (int)(10.5f * u), UI_Sub);

        // 操作行（每处一套，点下去真的做事）
        var acts = _scene switch
        {
            2 => new[] { ("买灵植", "i_buy", 0), ("卖灵植", "i_sell", 0), ("摆摊", "i_stall", 0), ("看市价", "i_price", 0) },
            3 => new[] { ("存 5 石", "i_dep5", 0), ("取 5 石", "i_wd5", 0), ("存 10 石", "i_dep10", 0), ("查账", "i_acct", 0) },
            4 => new[] { ("入宗", "i_sect", 0), ("领差事", "i_task", 0), ("问抽成", "i_tithe", 0) },
            5 => new[] { ("求族助", "i_clan", 0), ("看族产", "i_claninfo", 0) },
            6 => new[] { ("换舱纹", "i_style", 0), ("起航", "i_sail", 0) },
            _ => Array.Empty<(string, string, int)>()
        };
        float bw2 = vp.X * 0.215f, bh2 = 36f * u, gap = vp.X * 0.022f;
        for (int i = 0; i < acts.Length; i++)
        {
            int col = i % 3, row = i / 3;
            var br2 = new Rect2(vp.X * 0.055f + col * (bw2 + gap), vp.Y * 0.815f + row * (bh2 + 10f * u), bw2, bh2);
            Pill(br2, acts[i].Item1, u, false, true, false);
            _btns.Add((br2, acts[i].Item1, acts[i].Item2, acts[i].Item3));
        }
        var outBtn = new Rect2(vp.X * 0.055f, vp.Y * 0.905f, vp.X * 0.26f, 34f * u);
        Pill(outBtn, "出门", u, false, true, false);
        _btns.Add((outBtn, "", "leavecave", 0));
    }

    private void DrawSelection(float u)
    {
        if (_selected <= 0 || _selected == _w.Player.NpcId) return;
        var n = _w.Npcs.Find(z => z.Id == _selected && z.Alive);
        if (n == null) return;
        var p = Iso(n.Gx + 0.5f, n.Gy + 0.5f);
        DrawArc(p, 16f * u, 0f, Mathf.Tau, 28, UI_Jade, 2f * u);
    }

    private void DrawPlayerRing(float u)
    {
        var pn = Ply;
        if (pn == null || !pn.Alive) return;
        var p = Iso(pn.Gx + 0.5f, pn.Gy + 0.5f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 2.2f);
        DrawCircle(p, (17f + 2f * pulse) * u, new Color(1f, 0.85f, 0.45f, 0.10f));
        DrawArc(p, (15f + 1.5f * pulse) * u, 0f, Mathf.Tau, 36, new Color(1f, 0.86f, 0.48f, 0.85f), 2f * u);
        RR(new Rect2(p.X - 13f * u, p.Y - 42f * u, 26f * u, 17f * u), 8.5f * u, new Color(0.10f, 0.14f, 0.12f, 0.88f), UI_Gold with { A = 0.35f }, 1f);
        DrawString(_font, new Vector2(p.X - 8f * u, p.Y - 29.5f * u), "你", HorizontalAlignment.Left, -1, (int)(12.5f * u), UI_Gold);
    }

    private void DrawWorldFx(float u)
    {
        for (int i = _fx.Count - 1; i >= 0; i--)
        {
            var f = _fx[i];
            float k = f.Age / 0.8f;
            if (k >= 1f) { _fx.RemoveAt(i); continue; }
            var col = f.Element switch
            {
                1 => new Color(0.55f, 0.88f, 0.60f),
                2 => new Color(0.58f, 0.80f, 0.98f),
                3 => new Color(0.97f, 0.56f, 0.40f),
                _ => new Color(0.96f, 0.90f, 0.68f)
            };
            float r = (14f + 52f * k) * u;
            DrawArc(f.At, r, 0f, Mathf.Tau, 36, col with { A = (1f - k) * 0.85f }, (3.2f - 2.4f * k) * u);
        }
    }

    /// <summary>天气落到画面上：雨/雪/风/旱/战火，各自不同的粒子与色偏。</summary>
    private void DrawWeather(Vector2 vp, float u)
    {
        float top = vp.Y * 0.055f;
        string w = _w.Weather;
        Color tint = w switch
        {
            "旱" => new Color(0.80f, 0.60f, 0.32f, 0.13f),
            "涝" => new Color(0.26f, 0.38f, 0.46f, 0.25f),
            "阴雨" => new Color(0.28f, 0.38f, 0.44f, 0.16f),
            "风" => new Color(0.62f, 0.66f, 0.60f, 0.06f),
            "战乱" => new Color(0.52f, 0.10f, 0.06f, 0.16f),
            "丰年" => new Color(0.38f, 0.62f, 0.34f, 0.09f),
            _ => new Color(1f, 1f, 1f, 0f)
        };
        if (tint.A > 0.01f) DrawRect(new Rect2(0, top, vp.X, vp.Y - top), tint);

        if (w == "阴雨" || w == "涝")
        {
            for (int i = 0; i < 90; i++)
            {
                float rx = Hash2(i, 21), ry = Hash2(i, 37);
                float y = top + Mathf.PosMod(ry * vp.Y + _t * (600f + rx * 320f), vp.Y * 0.94f);
                float x = rx * vp.X + Mathf.Sin(_t * 0.5f + i) * 6f;
                DrawLine(new Vector2(x, y), new Vector2(x - 5f * u, y + 20f * u), new Color(0.74f, 0.86f, 0.94f, 0.30f), 1.2f);
            }
            DrawRect(new Rect2(0, vp.Y * 0.45f, vp.X, vp.Y * 0.29f), new Color(0.40f, 0.56f, 0.62f, 0.12f));
        }
        else if (w == "风")
        {
            for (int i = 0; i < 40; i++)
            {
                float ry = Hash2(i, 9);
                float x = Mathf.PosMod(Hash2(i, 3) * vp.X + _t * 340f, vp.X * 1.2f) - vp.X * 0.1f;
                float y = top + ry * vp.Y * 0.55f;
                DrawLine(new Vector2(x, y), new Vector2(x + 34f * u, y - 6f * u), new Color(0.86f, 0.84f, 0.72f, 0.16f), 1f);
            }
        }
        else if (w == "旱")
        {
            for (int i = 0; i < 6; i++)
            {
                float y = vp.Y * (0.48f + i * 0.06f);
                DrawLine(new Vector2(0, y + Mathf.Sin(_t * 1.2f + i) * 3f * u), new Vector2(vp.X, y), new Color(0.90f, 0.72f, 0.44f, 0.07f), 2f);
            }
            for (int i = 0; i < 26; i++)
            {
                float rx = Hash2(i, 5);
                float x = Mathf.PosMod(rx * vp.X + _t * 120f, vp.X);
                float y = vp.Y * (0.50f + Hash2(i, 11) * 0.36f) + Mathf.Sin(_t * 0.8f + i) * 4f * u;
                DrawCircle(new Vector2(x, y), (1.6f + Hash2(i, 17) * 2f) * u, new Color(0.86f, 0.74f, 0.50f, 0.22f));
            }
        }
        else if (w == "战乱")
        {
            for (int i = 0; i < 22; i++)
            {
                float rx = Hash2(i, 41);
                float x = Mathf.PosMod(rx * vp.X + Mathf.Sin(_t + i) * 30f, vp.X);
                float y = vp.Y * 0.62f - Mathf.PosMod(_t * (40f + Hash2(i, 7) * 50f) + Hash2(i, 13) * 400f, vp.Y * 0.42f);
                DrawCircle(new Vector2(x, y), (2f + Hash2(i, 23) * 3f) * u, new Color(0.98f, 0.46f, 0.18f, 0.34f));
            }
            DrawRect(new Rect2(0, top, vp.X, vp.Y - top), new Color(0.60f, 0.12f, 0.06f, 0.05f * (1f + Mathf.Sin(_t * 2f))));
        }

        // 冬季落雪：季节是真的，雪落在画面也是真的
        if (_w.Season == Season.Winter)
        {
            for (int i = 0; i < 55; i++)
            {
                float rx = Hash2(i, 61), ry = Hash2(i, 71);
                float y = top + Mathf.PosMod(ry * vp.Y + _t * (110f + rx * 90f), vp.Y * 0.94f);
                float x = Mathf.PosMod(rx * vp.X + Mathf.Sin(_t * 0.7f + i) * 22f, vp.X);
                DrawCircle(new Vector2(x, y), (1.4f + rx * 2.4f) * u, new Color(0.96f, 0.98f, 1f, 0.55f));
            }
        }
    }

    private void DrawStatusCard(Vector2 vp, float u)
    {
        var pn = Ply;
        var card = new Rect2(vp.X * 0.030f, vp.Y * 0.398f, vp.X * 0.940f, vp.Y * 0.052f);
        RR(card, 11f * u, UI_Panel, UI_Line, 1.2f);
        float tx = card.Position.X + 12f * u;
        string realmName = (pn?.Realm ?? 0) >= 7 ? "宗师" : (pn?.Realm ?? 0) >= 4 ? "修士" : "凡民";
        DrawString(_font, new Vector2(tx, card.Position.Y + 22f * u), $"{(pn?.Name ?? "—")}　{(pn?.Realm ?? 0)}境·{realmName}",
                   HorizontalAlignment.Left, -1, (int)(13f * u), UI_Text);
        DrawString(_font, new Vector2(tx, card.Position.Y + 38f * u),
                   $"{_w.ClockText}　{_w.Weather}　{_w.BiomeName((int)(pn?.Gx ?? 0), (int)(pn?.Gy ?? 0))}　石{_w.Player.Stone}",
                   HorizontalAlignment.Left, -1, (int)(10.5f * u), UI_Sub);

        float bx = card.Position.X + card.Size.X * 0.40f;
        float bw = card.Size.X * 0.42f, bh = 6.5f * u, labelW = 34f * u;
        float lifeT = pn == null ? 0f : 1f - Mathf.Clamp((pn.Age - 16f) / Mathf.Max(1f, pn.Lifespan - 16f), 0f, 1f);
        Bar(new Rect2(bx + labelW, card.Position.Y + 6f * u, bw - labelW, bh), lifeT, new Color(0.62f, 0.86f, 0.66f));
        Bar(new Rect2(bx + labelW, card.Position.Y + 19f * u, bw - labelW, bh), _w.AvgQi,
            _w.AvgQi < 0.5f ? new Color(0.92f, 0.55f, 0.40f) : new Color(0.60f, 0.80f, 0.92f));
        Bar(new Rect2(bx + labelW, card.Position.Y + 32f * u, bw - labelW, bh),
            Mathf.Clamp((_w.Player.Reputation + 20f) / 60f, 0f, 1f), new Color(0.86f, 0.74f, 0.42f));
        DrawString(_font, new Vector2(bx, card.Position.Y + 13f * u), "寿元", HorizontalAlignment.Left, -1, (int)(9.5f * u), UI_Sub);
        DrawString(_font, new Vector2(bx, card.Position.Y + 26f * u), "灵气", HorizontalAlignment.Left, -1, (int)(9.5f * u), UI_Sub);
        DrawString(_font, new Vector2(bx, card.Position.Y + 39f * u), "声望", HorizontalAlignment.Left, -1, (int)(9.5f * u), UI_Sub);
        DrawString(_font, new Vector2(bx + bw - 26f * u, card.Position.Y + 13f * u), $"{pn?.Age ?? 0}/{pn?.Lifespan ?? 0}",
                   HorizontalAlignment.Right, (int)(26f * u), (int)(9f * u), UI_Sub);
        DrawString(_font, new Vector2(bx + bw - 26f * u, card.Position.Y + 39f * u), $"{_w.Player.Reputation:0}",
                   HorizontalAlignment.Right, (int)(26f * u), (int)(9f * u), UI_Sub);
    }

    private void DrawClock(Vector2 vp, float u)
    {
        var r = new Rect2(vp.X * 0.030f, vp.Y * 0.356f, vp.X * 0.30f, 24f * u);
        RR(r, 12f * u, new Color(0.05f, 0.08f, 0.07f, 0.90f), UI_Line, 1f);
        DrawString(_font, new Vector2(r.Position.X + 10f * u, r.Position.Y + 16f * u), _w.ClockText,
                   HorizontalAlignment.Left, -1, (int)(11f * u), UI_Text);
        var tb = new Rect2(r.End.X + 6f * u, r.Position.Y, 48f * u, 24f * u);
        Pill(tb, _clockRun ? "行" : "停", u, _clockRun, true, false);
        _btns.Add((tb, "钟", _clockRun ? "timestop" : "timestart", 0));
        var sb = new Rect2(tb.End.X + 5f * u, r.Position.Y, 34f * u, 24f * u);
        Pill(sb, "×" + _clockSpeed, u, _clockSpeed > 1, false, false);
        _btns.Add((sb, "速", "timespeed", 0));
    }

    private void DrawMinimap(Vector2 vp, float u)
    {
        float side = Mathf.Min(vp.X * 0.21f, 92f * u);
        var map = new Rect2(vp.X - side - vp.X * 0.030f, vp.Y * 0.140f, side, side);
        RR(map, 8f * u, new Color(0.05f, 0.08f, 0.07f, 0.92f), UI_Line, 1f);
        float cw = (side - 8f * u) / World.W;
        for (int x = 0; x < World.W; x++)
            for (int y = 0; y < World.H; y++)
            {
                var c = _w.Tiles[x, y] switch
                {
                    Terrain.Water => new Color(0.34f, 0.52f, 0.60f),
                    Terrain.Rock => new Color(0.46f, 0.44f, 0.40f),
                    Terrain.Sand => new Color(0.62f, 0.56f, 0.42f),
                    Terrain.Cliff => new Color(0.28f, 0.26f, 0.24f),
                    _ => new Color(0.36f, 0.50f, 0.38f)
                };
                DrawRect(new Rect2(map.Position.X + 4f * u + x * cw, map.Position.Y + 4f * u + y * cw, cw, cw), c);
            }
        var pn = Ply;
        if (pn != null)
            DrawCircle(new Vector2(map.Position.X + 4f * u + (pn.Gx + 0.5f) * cw, map.Position.Y + 4f * u + (pn.Gy + 0.5f) * cw),
                       2.4f * u, new Color(1f, 0.85f, 0.45f));
        DrawString(_font, new Vector2(map.Position.X + 5f * u, map.End.Y - 5f * u), "天下 32×32",
                   HorizontalAlignment.Left, -1, (int)(8f * u), UI_Sub with { A = 0.7f });
    }

    private void DrawCommands(Vector2 vp, float u)
    {
        _btns.Clear();
        var spec = new (string L, string V, bool Key)[]
        {
            ("行囊", "panel2", true), ("修炼", "panel1", true), ("关系", "panel3", false), ("天下", "panel4", false),
            ("洞府", "panel5", false), ("交谈", "talk", false), ("田园", "panel6", false), ("探索", "explore", false),
            ("占府", "cave", false), ("开辟", "open", false), ("过门", "gate", false), ("开山", "delve", false)
        };
        float top = 0.462f, h = 0.0345f, gapx = 0.006f;
        float w = (0.940f - gapx * 5f) / 6f;
        bool dead = _w.Player.Dead;
        RR(new Rect2(vp.X * 0.018f, vp.Y * (top - 0.008f), vp.X * 0.964f,
                     vp.Y * (h * 2f + 0.004f + 0.016f)), 12f * u,
           new Color(0.045f, 0.075f, 0.065f, 0.96f), UI_Line, 1f);
        for (int i = 0; i < spec.Length; i++)
        {
            int col = i % 6, row = i / 6;
            var r = new Rect2(vp.X * (0.030f + col * (w + gapx)), vp.Y * (top + row * (h + 0.004f)), vp.X * w, vp.Y * h);
            Pill(r, spec[i].L, u, _pressed == i, spec[i].Key, dead);
            if (!dead) _btns.Add((r, spec[i].L, spec[i].V, 0));
        }
    }

    private void DrawStick(Vector2 vp, float u)
    {
        float R = Mathf.Min(vp.X * 0.115f, 74f * u);
        _stickBase = new Vector2(vp.X * 0.030f + R, vp.Y - vp.X * 0.026f - R);
        DrawCircle(_stickBase, R, new Color(0.06f, 0.10f, 0.09f, 0.55f));
        DrawArc(_stickBase, R, 0f, Mathf.Tau, 48, UI_Line with { A = 0.9f }, 1.4f * u);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi * 0.5f;
            var d2 = _stickBase + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R * 0.62f);
            DrawArc(d2, R * 0.16f, 0f, Mathf.Tau, 12, UI_Line with { A = 0.55f }, 1.2f * u);
        }
        var knob = _stickOn ? _stickKnob : _stickBase;
        DrawCircle(knob, R * 0.36f, new Color(0.78f, 0.84f, 0.76f, _stickOn ? 0.9f : 0.62f));
        DrawCircle(knob + new Vector2(0, -2f * u), R * 0.30f, new Color(0.90f, 0.94f, 0.86f, 0.85f));
        DrawString(_font, new Vector2(_stickBase.X - R * 0.30f, _stickBase.Y - R - 6f * u), "移动",
                   HorizontalAlignment.Left, -1, (int)(10f * u), UI_Sub);
    }

    private void DrawViewPad(Vector2 vp, float u)
    {
        float bw = 46f * u, bh = 30f * u, gap = 5f * u;
        float x0 = vp.X - vp.X * 0.030f - bw * 2f - gap;
        float y0 = vp.Y - vp.X * 0.026f - 92f * u - bh * 3f - gap * 2f;
        var views = new[] { ("俯", 0), ("斜", 1), ("平", 2) };
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect2(x0, y0 + i * (bh + gap), bw, bh);
            Pill(r, views[i].Item1 + "视", u, _viewMode == views[i].Item2, false, false);
            _btns.Add((r, "", "view", views[i].Item2));
        }
        var up = new Rect2(x0 + bw + gap, y0, bw, bh);
        var dn = new Rect2(x0 + bw + gap, y0 + (bh + gap) * 2f, bw, bh);
        Pill(up, "升高", u, false, false, _alt >= 2f);
        Pill(dn, "降低", u, false, false, _alt <= 0f);
        _btns.Add((up, "", "altup", 0));
        _btns.Add((dn, "", "altdn", 0));
        var fly = new Rect2(x0 + bw + gap, y0 + bh + gap, bw, bh);
        Pill(fly, _alt > 0.5f ? "御剑" : "落剑", u, _alt > 0.5f, true, false);
        _btns.Add((fly, "", "fly", 0));
        var pose = new Rect2(x0, y0 + (bh + gap) * 3f, bw * 2f + gap, bh);
        Pill(pose, "姿态·" + _w.FlightPoseName, u, _w.FlightPose > 0, false, false);
        _btns.Add((pose, "", "pose", 0));
        var cave = new Rect2(x0, y0 + (bh + gap) * 4f, bw * 2f + gap, bh);
        Pill(cave, "入洞府", u, false, false, Ply == null || Ply.CaveId < 0);
        _btns.Add((cave, "", "entercave", 0));
        var near = new Rect2(x0, y0 + (bh + gap) * 5f, bw * 2f + gap, bh);
        Pill(near, _zoom > 1.45f ? "近景中" : "看近景", u, _zoom > 1.45f, false, false);
        _btns.Add((near, "", "nearview", 0));
        DrawString(_font, new Vector2(x0, y0 - 6f * u), "高度 " + _altSmooth.ToString("0.0"),
                   HorizontalAlignment.Left, -1, (int)(9f * u), UI_Sub);
    }

    private void DrawHint(Vector2 vp, float u)
    {
        var r = new Rect2(vp.X * 0.030f, vp.Y - 24f * u - vp.X * 0.013f, vp.X * 0.94f, 22f * u);
        RR(r, 11f * u, new Color(0.04f, 0.07f, 0.06f, 0.86f), UI_Line with { A = 0.5f }, 1f);
        DrawString(_font, new Vector2(r.Position.X + 10f * u, r.Position.Y + 15f * u),
                   "摇杆=连续走　点地=寻路　拖空白=挪图　点人=详情　右上=天下图",
                   HorizontalAlignment.Left, (int)(r.Size.X - 20f * u), (int)(9.5f * u), UI_Sub);
    }

    private void DrawToast(Vector2 vp, float u)
    {
        if (_msgAge > 4.5f) return;
        float a = Mathf.Clamp(1f - (_msgAge - 3.6f) / 0.9f, 0f, 1f);
        int fs = (int)(12.5f * u);
        float w = Mathf.Min(vp.X * 0.90f, _font.GetStringSize(_msg, HorizontalAlignment.Left, -1, fs).X + 24f * u);
        var r = new Rect2(vp.X * 0.05f, vp.Y * 0.330f, w, 24f * u);
        RR(r, 13f * u, new Color(0.04f, 0.07f, 0.06f, 0.92f * a), UI_Line, 1f);
        DrawString(_font, new Vector2(r.Position.X + 12f * u, r.Position.Y + 17.5f * u), _msg,
                   HorizontalAlignment.Left, (int)(r.Size.X - 20f * u), fs, UI_Text with { A = a });
    }

    private void DrawDialogue(Vector2 vp, float u)
    {
        var t = _w.Npcs.Find(n => n.Id == _dlgNpc && n.Alive);
        if (t == null) { _dlgNpc = -1; return; }
        var card = new Rect2(vp.X * 0.030f, vp.Y * 0.520f, vp.X * 0.94f, vp.Y * 0.238f);
        RR(card, 14f * u, new Color(0.055f, 0.085f, 0.075f, 0.97f), UI_Jade with { A = 0.35f }, 1.2f);
        DrawString(_font, new Vector2(card.Position.X + 14f * u, card.Position.Y + 26f * u),
                   $"{t.Name}　{t.Realm}境　{(t.SectId >= 0 ? "宗门" : "散修")}", HorizontalAlignment.Left, -1, (int)(14f * u), UI_Jade);
        string goal = t.Goal switch { 0 => "求道", 1 => "聚财", 2 => "传承", 3 => "复仇", 4 => "结党", _ => "避劫" };
        DrawString(_font, new Vector2(card.Position.X + 14f * u, card.Position.Y + 44f * u),
                   $"所图：{goal}　印象 {t.Imp:0}　恩怨 {t.Grudge}/{t.Favor}　交谈 {t.TalkCount} 轮",
                   HorizontalAlignment.Left, -1, (int)(10.5f * u), UI_Sub);
        string mem = t.Memory.Count == 0 ? "（他还没跟你多说过话）" : t.Memory[t.Memory.Count - 1];
        DrawString(_font, new Vector2(card.Position.X + 14f * u, card.Position.Y + 64f * u),
                   _dlgLine.Length > 0 ? _dlgLine : mem,
                   HorizontalAlignment.Left, (int)(card.Size.X - 28f * u), (int)(11.5f * u), UI_Text);

        var topics = new[] { "问来历", "施援手", "论道", "挑衅" };
        float bw = (card.Size.X - 28f * u - 18f * u) / 4f;
        for (int i = 0; i < 4; i++)
        {
            var rr = new Rect2(card.Position.X + 14f * u + i * (bw + 6f * u), card.End.Y - 44f * u, bw, 30f * u);
            Pill(rr, topics[i], u, false, i == 1, false);
            _btns.Add((rr, topics[i], "dtopic", i));
        }
        var close = new Rect2(card.End.X - 14f * u - 44f * u, card.Position.Y + 10f * u, 40f * u, 22f * u);
        Pill(close, "别过", u, false, false, false);
        _btns.Add((close, "别过", "dclose", 0));
    }

    private void DrawVerdict(Vector2 vp, float u)
    {
        RR(new Rect2(0, vp.Y * 0.13f, vp.X, vp.Y * 0.30f), 0f, new Color(0.02f, 0.035f, 0.030f, 0.90f));
        var card = new Rect2(vp.X * 0.05f, vp.Y * 0.152f, vp.X * 0.90f, vp.Y * 0.245f);
        RR(card, 16f * u, new Color(0.07f, 0.10f, 0.09f, 0.97f), UI_Gold with { A = 0.42f }, 1.4f);
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 28f * u), "判 词",
                   HorizontalAlignment.Left, -1, (int)(19f * u), UI_Gold);
        DrawLine(new Vector2(card.Position.X + 16f * u, card.Position.Y + 36f * u),
                 new Vector2(card.End.X - 16f * u, card.Position.Y + 36f * u), UI_Gold with { A = 0.25f }, 1f);
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 64f * u), _w.Player.Verdict,
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(15f * u), UI_Text);
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 92f * u),
                   $"帮 {_w.Player.Help}　拒 {_w.Player.Refuse}　夺 {_w.Player.Rob}　声望 {_w.Player.Reputation:0}　被夺宝 {_w.Player.Ambushed}",
                   HorizontalAlignment.Left, -1, (int)(12f * u), UI_Sub);
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 114f * u),
                   $"来世：{_w.Player.Fate}　{(string.IsNullOrEmpty(_w.Player.Killer) ? "无人加害" : "死于 " + _w.Player.Killer)}",
                   HorizontalAlignment.Left, -1, (int)(12.5f * u), UI_Jade);
        var btn = new Rect2(card.End.X - 12f * u - card.Size.X * 0.30f, card.End.Y - 34f * u, card.Size.X * 0.30f, 26f * u);
        Pill(btn, "投生", u, false, true, false);
        _btns.Add((btn, "投生", "reincarnate", 0));
    }

    private bool PlayTap(Vector2 p, Vector2 vp)
    {
        float u = vp.X / 720f;
        float R = Mathf.Min(vp.X * 0.115f, 74f * u);
        if (p.DistanceTo(_stickBase) < R * 1.15f) return true;

        for (int i = 0; i < _btns.Count; i++)
        {
            var b = _btns[i];
            if (!b.R.HasPoint(p)) continue;
            switch (b.Verb)
            {
                case "timestop": _clockRun = false; _msg = "时间停住了。"; _msgAge = 0; return true;
                case "timestart": _clockRun = true; _msg = "时间重新走。"; _msgAge = 0; return true;
                case "timespeed": _clockSpeed = _clockSpeed >= 12 ? 1 : _clockSpeed * 2; _msg = "流速 ×" + _clockSpeed; _msgAge = 0; return true;
                case "view": _viewMode = b.Arg; ApplyView(); return true;
                case "altup": _alt = Mathf.Min(2f, _alt + 0.5f); ApplyView(); return true;
                case "altdn": _alt = Mathf.Max(0f, _alt - 0.5f); ApplyView(); return true;
                case "fly": _alt = _alt > 0.5f ? 0f : 1.5f; ApplyView(); _msg = _alt > 0 ? "御剑而起。" : "落剑归地。"; _msgAge = 0; return true;
                case "entercave":
                    if (Ply == null || Ply.CaveId < 0) { _msg = "你还没有洞府：先「占府」或「开辟」。"; _msgAge = 0; }
                    else { _scene = 1; _msgAge = 0; }
                    return true;
                case "leavecave": _scene = 0; QueueRedraw(); return true;
                case "nearview":
                    _zoom = _zoom > 1.45f ? 1f : 2.2f; ApplyView();
                    _msg = _zoom > 1.45f ? "近景：草叶、碎石、花都长出来了。" : "回到常规视距。"; _msgAge = 0; return true;
                case "pose":
                    _w.FlightPose = (_w.FlightPose + 1) % 3;
                    _msg = "飞行姿态：" + _w.FlightPoseName + (_w.FlightPose == 0 ? "（耗气少，看得远）" : _w.FlightPose == 1 ? "（快，视野窄）" : "（最快，最耗）");
                    _msgAge = 0; return true;
                case "dclose": _dlgNpc = -1; _dlgRound = 0; return true;
                case "dtopic":
                    _dlgLine = _w.Talk(_dlgNpc, b.Arg); _dlgRound++; _msgAge = 0;
                    _fx.Add((Iso((Ply?.Gx ?? 0) + 0.5f, (Ply?.Gy ?? 0) + 0.5f), 0f, b.Arg == 1 ? 1 : b.Arg == 3 ? 3 : 0));
                    QueueRedraw(); return true;
                case "reincarnate":
                    _w.Act("reincarnate"); _msg = _w.Player.LastEvent; _pressed = -1;
                    _dlgNpc = -1; _selected = _w.Player.NpcId; _path.Clear(); _clockRun = true; CenterOnPlayer();
                    _msgAge = 0; return true;
                case "panel1": case "panel2": case "panel3": case "panel4": case "panel5": case "panel6":
                    OpenPanel(int.Parse(b.Verb.Substring(5))); _msgAge = 0; return true;
                case "pclose": _panel = 0; return true;
                case "bagtab": _bagTab = b.Arg; return true;
                case "skill0": _msg = _w.UseSkill(0); _msgAge = 0; return true;
                case "skill1": _msg = _w.UseSkill(1); _msgAge = 0; return true;
                case "skill2": _msg = _w.UseSkill(2); _msgAge = 0; return true;
                case "skill3": _msg = _w.UseSkill(3); _msgAge = 0; return true;
                case "skill4": _msg = _w.UseSkill(4); _msgAge = 0; return true;
                case "till": _msg = _w.TillField(); _msgAge = 0; return true;
                case "harvest": _msg = _w.HarvestField(); _msgAge = 0; return true;
                case "pickcrop": _cropKind = b.Arg; _msg = "待播：" + _w.CropName(b.Arg); _msgAge = 0; return true;
                case "decor":
                {
                    var things = new[] { "蒲团", "书架", "药炉", "灵泉", "屏风", "剑架" };
                    string cur = _w.Decor.Count > b.Arg ? _w.Decor[b.Arg] : "";
                    int k = 0; while (k < things.Length && things[k] != cur) k++;
                    _msg = _w.Decorate(b.Arg, things[(k + 1) % things.Length]); _msgAge = 0; return true;
                }
                case "enterscene": _scene = b.Arg; _panel = 0; _msgAge = 0; return true;
                case "i_buy": _msg = _w.BuyGood("灵植", 1); _msgAge = 0; return true;
                case "i_sell": _msg = _w.SellGood("灵植", 1); _msgAge = 0; return true;
                case "i_stall": _msg = _w.Stall(1); _msgAge = 0; return true;
                case "i_price": _msg = _w.PriceBoard(); _msgAge = 0; return true;
                case "i_dep5": _msg = _w.Deposit(5); _msgAge = 0; return true;
                case "i_wd5": _msg = _w.Withdraw(5); _msgAge = 0; return true;
                case "i_dep10": _msg = _w.Deposit(10); _msgAge = 0; return true;
                case "i_acct": _msg = $"钱庄账上 {_w.Bank} 石，身上 {_w.Player.Stone} 石。"; _msgAge = 0; return true;
                case "i_sect": _msg = _w.Act("sect"); _msgAge = 0; return true;
                case "i_task": _msg = _w.SectTask(); _msgAge = 0; return true;
                case "i_tithe": _msg = _w.SectTithe(); _msgAge = 0; return true;
                case "i_clan": _msg = _w.ClanHelp(); _msgAge = 0; return true;
                case "i_claninfo": _msg = $"在册家族 {_w.Clans.Count}：嫡系管产业，旁支跑腿，族产越厚越能护住人。"; _msgAge = 0; return true;
                case "i_style": _msg = _w.BoatRestyle(); _msgAge = 0; return true;
                case "i_sail": _msg = _w.BoatSail(); _scene = 0; CenterOnPlayer(); _msgAge = 0; return true;
                case "world_1": case "world_2": case "world_3": case "world_4": case "world_6":
                    _scene = b.Verb switch { "world_1" => 4, "world_2" => 5, "world_3" => 2, "world_4" => 3, _ => 6 };
                    _panel = 0; _msgAge = 0; return true;
                case "world_5": _msg = _w.Stall(1); _msgAge = 0; return true;
                case "rtalk":
                {
                    var nb = _w.Npcs.Find(z => z.Id == b.Arg && z.Alive);
                    if (nb == null) { _msg = "人已不在。"; }
                    else
                    {
                        var me = Ply;
                        if (me != null && Math.Abs(nb.Gx - me.Gx) <= 2 && Math.Abs(nb.Gy - me.Gy) <= 2)
                        { _dlgNpc = nb.Id; _dlgLine = "（" + nb.Name + " 正看着你）"; _panel = 0; }
                        else _msg = nb.Name + " 不在这附近，得走过去。";
                    }
                    _msgAge = 0; return true;
                }
                case "talk":
                    var nt = NearestTo(2.5f);
                    if (nt == null) { _msg = "四下无人，只有风。"; }
                    else { _dlgNpc = nt.Id; _dlgLine = "（" + nt.Name + " 抬眼看你）"; }
                    _pressed = i; _msgAge = 0; return true;
                case "sowcrop":
                    _msg = _w.SowField(_cropKind); _msgAge = 0; _pressed = i; return true;
                default:
                    float cost = b.Verb switch
                    {
                        "meditate" => 6f, "help" => 2f, "refuse" => 1f, "rob" => 3f, "cave" => 8f,
                        "sect" => 3f, "open" => 10f, "gate" => 2f, "explore" => 4f, "sowcrop" => 1f, "delve" => 4f, _ => 1f
                    };
                    _msg = _w.ActTimed(b.Verb, b.Arg, cost);
                    _pressed = i; _msgAge = 0;
                    if (_msg.Contains("突破") || b.Verb == "explore" || b.Verb == "delve" || b.Verb == "plant")
                        _fx.Add((Iso((Ply?.Gx ?? 0) + 0.5f, (Ply?.Gy ?? 0) + 0.5f), 0f, _w.Player.Reputation < 0 ? 3 : 1));
                    QueueRedraw(); return true;
            }
        }

        var hit = NpcAt(p);
        if (hit != null)
        {
            _selected = hit.Id; _dlgNpc = hit.Id; _dlgLine = "（" + hit.Name + " 停下手里的活）"; _dlgRound = 0;
            _msgAge = 0; QueueRedraw(); return true;
        }

        if (_scene != 0) return true;
        if (p.Y > vp.Y * 0.545f) return false;
        if (p.Y > vp.Y * 0.398f) return true;
        if (!_w.Player.Dead) TapWalk(p);
        return true;
    }

    private Npc NpcAt(Vector2 p)
    {
        Npc best = null; float bd = 26f * _scale + 14f;
        foreach (var n in _w.Npcs)
        {
            if (!n.Alive) continue;
            var s = Iso(n.Gx + 0.5f, n.Gy + 0.5f);
            float d = p.DistanceTo(s + new Vector2(0, -10f * _scale));
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    private void TapWalk(Vector2 sp)
    {
        var pn = Ply;
        if (pn == null) return;
        int tx = -1, ty = -1; float best = 1e9f;
        for (int x = 0; x < World.W; x++)
            for (int y = 0; y < World.H; y++)
            {
                var s = Iso(x + 0.5f, y + 0.5f);
                float d = s.DistanceTo(sp);
                if (d < best && _w.Walkable(x, y)) { best = d; tx = x; ty = y; }
            }
        if (tx < 0) return;
        var path = Pathfind((int)pn.Gx, (int)pn.Gy, tx, ty, 14);
        if (path.Count == 0) { _msg = "过不去。"; _msgAge = 0; return; }
        _path.Clear(); _path.AddRange(path);
        _pathAcc = 0.26f;
        _msg = $"循路向({tx},{ty})，{path.Count} 步。"; _msgAge = 0;
    }

    private List<(int X, int Y)> Pathfind(int sx, int sy, int tx, int ty, int maxSteps)
    {
        var q = new Queue<(int X, int Y)>();
        var prev = new Dictionary<int, int>();
        q.Enqueue((sx, sy)); prev[sx * 1000 + sy] = -1;
        int step = 0; bool found = false;
        while (q.Count > 0 && step < maxSteps * 4)
        {
            var cur = q.Dequeue(); step++;
            if (cur.X == tx && cur.Y == ty) { found = true; break; }
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cur.X + d.Item1, ny = cur.Y + d.Item2;
                if (!_w.Walkable(nx, ny)) continue;
                int key = nx * 1000 + ny;
                if (prev.ContainsKey(key)) continue;
                prev[key] = cur.X * 1000 + cur.Y;
                q.Enqueue((nx, ny));
            }
        }
        var path = new List<(int, int)>();
        if (!found) return path;
        int cx = tx, cy = ty;
        while (cx != sx || cy != sy)
        {
            path.Add((cx, cy));
            int k = prev[cx * 1000 + cy];
            if (k < 0) break;
            cx = k / 1000; cy = k % 1000;
        }
        path.Reverse();
        if (path.Count > maxSteps) path.RemoveRange(maxSteps, path.Count - maxSteps);
        return path;
    }

    private Npc NearestTo(float r)
    {
        var pn = Ply; if (pn == null) return null;
        Npc best = null; float bd = r * r;
        foreach (var n in _w.Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;
            float dx = n.Gx - pn.Gx, dy = n.Gy - pn.Gy, d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    private void ApplyView()
    {
        _zoom = _viewMode switch { 0 => 1f, 1 => 0.62f, _ => 1.35f };
        Resize();
        QueueRedraw();
    }

    private bool PlayDrag(InputEvent e)
    {
        var vp = GetViewportRect().Size;
        float u = vp.X / 720f;
        float R = Mathf.Min(vp.X * 0.115f, 74f * u);
        if (e is InputEventScreenTouch t)
        {
            if (t.Pressed && t.Position.DistanceTo(_stickBase) < R * 1.15f)
            { _stickOn = true; _stickTouch = t.Index; _stickKnob = t.Position; _stickDir = Vector2.Zero; return true; }
            if (!t.Pressed && t.Index == _stickTouch)
            { _stickOn = false; _stickTouch = -1; _stickDir = Vector2.Zero; _stickKnob = _stickBase; return true; }
        }
        if (e is InputEventScreenDrag d)
        {
            if (_stickOn && d.Index == _stickTouch)
            {
                var off = d.Position - _stickBase;
                if (off.Length() > R * 0.55f) off = off.Normalized() * R * 0.55f;
                _stickKnob = _stickBase + off;
                _stickDir = off;
                return true;
            }
            _cam += d.Relative;
            QueueRedraw();
            return true;
        }
        if (e is InputEventMagnifyGesture m)
        {
            _zoom = Mathf.Clamp(_zoom * m.Factor, 0.45f, 2.6f);
            Resize(); QueueRedraw();
            return true;
        }
        return false;
    }
}
