using System;
using Godot;
using SimCore;

namespace Xianxia;

/// <summary>
/// 呈现层：只读 SimCore，不写任何世界状态。
/// 等距斜视 2.5D（斜俯 ~50°），人物为场景内小人，立绘将来走侧栏浮层。
/// </summary>
public partial class Main : Node2D
{
    private World _w;
    private Font _font;
    private float _t;
    private int _selected = -1;
    private bool _auto;
    private float _autoAcc;
    private float _scale = 1f;
    private Vector2 _origin;

    // 等距参数
    private const float TW = 76f;
    private const float TH = 38f;
    private const float LIFT = 16f; // 山体抬升

    // 青绿山水调色
    private static readonly Color C_Grass = new("4f7d63");
    private static readonly Color C_Grass2 = new("436e56");
    private static readonly Color C_Rock = new("6b6b59");
    private static readonly Color C_Sand = new("b09a78");
    private static readonly Color C_Water = new("2b5f6b");
    private static readonly Color C_Cliff = new("27352e");
    private static readonly Color C_Side = new("22322c");
    private static readonly Color C_Ink = new("101815");
    private static readonly Color C_Paper = new("efe6d2");
    private static readonly Color C_Accent = new("c8663d");
    private static readonly Color C_Jade = new("8fc9a8");

    // ---------------------------------------------------------------- 美术资产（AI 生成的写实贴片/立绘）
    private static ImageTexture _tiles, _portrait;

    private static ImageTexture LoadRawTex(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var buf = f.GetBuffer((long)f.GetLength());
        var img = new Image();
        if (img.LoadJpgFromBuffer(buf) != Godot.Error.Ok) return null;
        return ImageTexture.CreateFromImage(img);
    }

    public override void _Ready()
    {
        _w = new World(20261124L);
        _font = ThemeDB.FallbackFont;
        _tiles = LoadRawTex("res://assets/tiles.dat");
        _portrait = LoadRawTex("res://assets/portrait.dat");
        Resize();
        GetViewport().SizeChanged += Resize;
        // 预热 40 年，让世界已有组织与恩怨
        _w.Tick(40);
        PlaySetup();
        if (OS.HasEnvironment("SMOKE_SELECT"))
        {
            foreach (var n in _w.Npcs) if (n.Alive && n.Realm >= 3) { _selected = n.Id; break; }
        }
    }

    private void Resize()
    {
        var vp = GetViewportRect().Size;
        // 取景：一屏约 10 格宽（等距），地图外溢被裁切 —— 这才是 2.5D 的正经取景
        _scale = vp.X / (10f * TW) * _zoom;
        _origin = new Vector2(vp.X * 0.5f, vp.Y * 0.265f);
    }

    private bool _smoke;

    public override void _Process(double delta)
    {
        _msgAge += (float)delta;
        // 真实时间驱动：1 秒 = 1 时辰（游戏内两小时）。挂机时世界照走，不点也在过。
        if (_w != null && !_w.Player.Dead)
        {
            float rate = _clockRun ? (1f * _clockSpeed) : 0f;
            if (rate > 0f) { _w.SpendHours((float)delta * rate); QueueRedraw(); }
        }
        if (_msgAge > 0.55f && _pressed >= 0) { _pressed = -1; QueueRedraw(); }
        if (_fx.Count > 0)
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
                _fx[i] = (_fx[i].At, _fx[i].Age + (float)delta, _fx[i].Element);
            QueueRedraw();
        }
        if (!_smoke && OS.HasEnvironment("SMOKE_SHOT")) _smoke = true;
        if (_smoke && Engine.GetProcessFrames() == 20)
        {
            var imgA = GetViewport().GetTexture().GetImage();
            imgA.SavePng(OS.GetEnvironment("SMOKE_SHOT"));   // 拆装前
        }
        if (_smoke && Engine.GetProcessFrames() == 28 && OS.HasEnvironment("SMOKE_SHOT2"))
        {
            var pick = _w.Npcs.Find(n => n.Id == _selected);
            if (pick != null)
            {
                pick.WearMask &= ~0b1010010;   // 拆掉 外袍 / 头饰 / 发型，验证分层独立
                GD.Print($"SMOKE_TOGGLE mask={pick.WearMask}");
            }
        }
        if (_smoke && Engine.GetProcessFrames() == 45)
        {
            var img = GetViewport().GetTexture().GetImage();
            var path = OS.GetEnvironment("SMOKE_SHOT");
            if (OS.HasEnvironment("SMOKE_SHOT2")) img.SavePng(OS.GetEnvironment("SMOKE_SHOT2"));   // 拆装后
            GD.Print($"SMOKE_OK frames={Engine.GetProcessFrames()} size={img.GetWidth()}x{img.GetHeight()} year={_w.Year} alive={_w.AliveCount}");
            GetTree().Quit();
        }
        _t += (float)delta;
        if (_auto)
        {
            _autoAcc += (float)delta;
            if (_autoAcc > 0.55f) { _autoAcc = 0; _w.Tick(1); }
        }
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (PlayDrag(e)) return;
        if (e is InputEventScreenTouch t && t.Pressed)
        {
            var vp = GetViewportRect().Size;
            if (t.Position.Y < vp.Y * 0.10f) return; // 顶部按钮区
            if (PlayTap(t.Position, vp)) return;
            if (TapPanel(t.Position, vp)) return;
            if (t.Position.Y > vp.Y * 0.90f) { _auto = !_auto; return; }
            HitPick(t.Position);
            _w.Tick(1);
        }
        else if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var vp = GetViewportRect().Size;
            if (PlayTap(mb.Position, vp)) return;
            if (TapPanel(mb.Position, vp)) return;
            if (mb.Position.Y > vp.Y * 0.90f) { _auto = !_auto; return; }
            HitPick(mb.Position);
            _w.Tick(1);
        }
    }

    // 世界坐标 → 屏幕坐标（含缩放与原点）
    private Vector2 Iso(float gx, float gy, float z = 0f)
    {
        float sx = (gx - gy) * TW * 0.5f;
        float sy = (gx + gy) * TH * 0.5f - z;
        return _origin + _cam + new Vector2(sx, sy) * _scale;
    }

    private bool TapPanel(Vector2 p, Vector2 vp)
    {
        if (p.Y < vp.Y * 0.575f || p.Y > vp.Y * 0.762f) return false;
        foreach (var h in _hot)
            if (h.R.HasPoint(p)) { h.F(); QueueRedraw(); return true; }
        return true;   // 落在面板空白处：吞掉事件，避免误推年份
    }

    private void HitPick(Vector2 sp)
    {
        int best = -1; float bestD = 26f * _scale + 14f;
        foreach (var n in _w.Npcs)
        {
            if (!n.Alive) continue;
            var p = Iso(n.Gx + 0.5f, n.Gy + 0.5f);
            float d = p.DistanceTo(sp);
            if (d < bestD) { bestD = d; best = n.Id; }
        }
        _selected = best;
    }

    public override void _Draw()
    {
        var vp = GetViewportRect().Size;
        var top = new Color("0a1310");
        var bot = new Color("16231d");
        const int bands = 28;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            DrawRect(new Rect2(0, vp.Y * t, vp.X, vp.Y / bands + 1.5f), top.Lerp(bot, t));
        }

        // 远景雾带（把版图压住，避免"浮空的棋盘"）
        for (int i = 0; i < 3; i++)
            DrawRect(new Rect2(0, vp.Y * (0.06f + i * 0.17f), vp.X, vp.Y * 0.055f),
                     new Color(0.55f, 0.75f, 0.68f, 0.05f));

        // 中心微光 + 远景雾层（托住版图，避免浮空）
        var center = Iso(World.W * 0.5f, World.H * 0.5f);
        DrawCircle(center, 420f * _scale, new Color(0.36f, 0.55f, 0.46f, 0.07f));
        DrawCircle(center, 260f * _scale, new Color(0.42f, 0.62f, 0.52f, 0.06f));

        DrawHorizon(vp);

        // 地形：按 gx+gy 由远到近绘制（正确 draw order）
        for (int sum = 0; sum <= (World.W - 1) + (World.H - 1); sum++)
        {
            for (int x = 0; x < World.W; x++)
            {
                int y = sum - x;
                if (y < 0 || y >= World.H) continue;
                DrawTile(x, y);
            }
        }

        // 地物（树/石/苇）：带投影与高度，才是 2.5D 而不是贴图平面
        DrawProps(vp);

        // 凡俗—修真三层地标
        DrawFeudalLayer();

        // 组织旗帜
        foreach (var f in _w.Factions)
        {
            var anchor = f.Members.Count > 0 ? _w.Npcs.Find(n => n.Id == f.Members[0]) : null;
            if (anchor == null) continue;
            var p = Iso(anchor.Gx + 0.5f, anchor.Gy + 0.5f);
            DrawFlag(p, (int)f.Kind);
        }

        // 人物：按深度排序
        var drawList = new System.Collections.Generic.List<Npc>();
        foreach (var n in _w.Npcs) if (n.Alive) drawList.Add(n);
        drawList.Sort((a, b) => (a.Gx + a.Gy).CompareTo(b.Gx + b.Gy));
        foreach (var n in drawList) DrawNpc(n);

        // 远景雾：把最远那几排压进雾里，制造纵深
        for (int i = 0; i < 5; i++)
            DrawRect(new Rect2(0, vp.Y * (0.150f + i * 0.0135f), vp.X, vp.Y * 0.016f),
                     new Color(0.55f, 0.68f, 0.66f, 0.085f - i * 0.015f));

        // 暗角（四边渐深，聚焦版图）
        var vg = new Color(0, 0, 0, 0.22f);
        DrawRect(new Rect2(0, 0, vp.X, vp.Y * 0.06f), vg);
        DrawRect(new Rect2(0, vp.Y * 0.94f, vp.X, vp.Y * 0.06f), vg);

        DrawPortrait(vp);
        PlayDraw(vp);
        DrawHud(vp);
    }

    private float Hash2(int x, int y)
    {
        unchecked { int h = (x * 73856093) ^ (y * 19349663) ^ 0x5e5e; return (h & 0xffff) / 65535f; }
    }

    /// <summary>地平线：两层远山剪影，随相机微量平移（视差），托住"世界很大"。</summary>
    private void DrawHorizon(Vector2 vp)
    {
        float y0 = vp.Y * 0.155f;
        for (int layer = 0; layer < 2; layer++)
        {
            float amp = layer == 0 ? 34f : 20f;
            float baseY = y0 + (layer == 0 ? 6f : 16f);
            var col = layer == 0 ? new Color(0.20f, 0.30f, 0.31f, 0.55f) : new Color(0.26f, 0.37f, 0.36f, 0.42f);
            var pts = new System.Collections.Generic.List<Vector2>();
            float px = -40f;
            pts.Add(new Vector2(px, baseY + amp));
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                float x = vp.X * t;
                float noise = Mathf.Sin(t * 7.1f + layer * 2.3f) * 0.5f + Mathf.Sin(t * 13.7f + layer) * 0.3f;
                pts.Add(new Vector2(x, baseY - amp * (0.55f + 0.45f * noise)));
            }
            pts.Add(new Vector2(vp.X + 40f, baseY + amp));
            DrawColoredPolygon(pts.ToArray(), col);
        }
        DrawRect(new Rect2(0, y0 + 26f, vp.X, vp.Y * 0.02f), new Color(0.55f, 0.70f, 0.68f, 0.07f));
    }

    /// <summary>地表物件：树、石、苇。位置由格坐标哈希决定（确定性），随风轻摆。</summary>
    private void DrawProps(Vector2 vp)
    {
        for (int x = 0; x < World.W; x++)
            for (int y = 0; y < World.H; y++)
            {
                var t = _w.Tiles[x, y];
                if (t == Terrain.Cliff) continue;
                float h = Hash2(x, y);
                int bio1 = _w.BiomeAt(x, y);
                var c = Iso(x + 0.5f + (h - 0.5f) * 0.5f, y + 0.5f + (Hash2(y, x) - 0.5f) * 0.5f);
                if (c.X < -60 || c.X > vp.X + 60 || c.Y < vp.Y * 0.13f || c.Y > vp.Y * 0.62f) continue;

                if (t == Terrain.Grass && h > 0.62f)
                {
                    // 四种树型按格子哈希分配：不再整屏同一棵（树是"墙纸感"的主要来源）
                    int kind = (int)(Hash2(x * 7 + 3, y * 11 + 5) * 4f);
                    float sway = Mathf.Sin(_t * 0.9f + h * 9f) * 1.4f * _scale;
                    float trunkH = (16f + 10f * h) * _scale;
                    DrawCircle(c + new Vector2(2f, 1f), 5.5f * _scale, new Color(0, 0, 0, 0.20f));
                    var leafA = Color.FromHsv(0.28f + h * 0.10f, 0.40f, 0.32f + h * 0.14f);
                    if (kind == 0)          // 松：三层塔
                    {
                        DrawLine(c, c + new Vector2(sway, -trunkH), new Color(0.24f, 0.20f, 0.15f), 2.2f * _scale);
                        for (int k = 0; k < 3; k++)
                        {
                            var lp = c + new Vector2(sway * (0.5f + k * 0.2f), -trunkH - (4f + k * 7f) * _scale);
                            DrawColoredPolygon(new[] {
                                lp + new Vector2(0, -9f * _scale), lp + new Vector2(7f * _scale, 3f * _scale),
                                lp + new Vector2(-7f * _scale, 3f * _scale) }, leafA.Darkened(k * 0.12f));
                        }
                    }
                    else if (kind == 1)     // 柏：细高
                    {
                        DrawLine(c, c + new Vector2(-sway, -trunkH * 1.4f), new Color(0.22f, 0.19f, 0.14f), 2.4f * _scale);
                        DrawColoredPolygon(new[] {
                            c + new Vector2(0, -trunkH - 16f * _scale), c + new Vector2(5f * _scale, -trunkH * 0.5f),
                            c + new Vector2(-5f * _scale, -trunkH * 0.5f) }, leafA.Darkened(0.18f));
                        DrawCircle(c + new Vector2(0, -trunkH * 1.25f), 4.2f * _scale, leafA.Lightened(0.10f));
                    }
                    else if (kind == 2)     // 古榕：扇形冠
                    {
                        DrawLine(c, c + new Vector2(sway, -trunkH * 0.8f), new Color(0.26f, 0.21f, 0.16f), 3f * _scale);
                        for (int k = 0; k < 5; k++)
                        {
                            float a2 = k * 1.26f + h * 6f;
                            DrawCircle(c + new Vector2(Mathf.Cos(a2) * 7f * _scale, -trunkH * 1.05f + Mathf.Sin(a2) * 3.4f * _scale),
                                       (5.2f + (k % 2) * 1.4f) * _scale, leafA.Darkened((k % 3) * 0.08f));
                        }
                    }
                    else                    // 枯木：没叶
                    {
                        DrawLine(c, c + new Vector2(sway, -trunkH), new Color(0.32f, 0.27f, 0.21f), 2.2f * _scale);
                        DrawLine(c + new Vector2(0, -trunkH * 0.6f), c + new Vector2(6f * _scale, -trunkH * 0.9f),
                                 new Color(0.30f, 0.25f, 0.20f), 1.4f * _scale);
                        DrawLine(c + new Vector2(0, -trunkH * 0.75f), c + new Vector2(-5f * _scale, -trunkH * 1.05f),
                                 new Color(0.30f, 0.25f, 0.20f), 1.2f * _scale);
                    }
                }
                else if (t == Terrain.Rock && h > 0.72f)
                {
                    float s2 = (6f + 7f * h) * _scale;
                    DrawCircle(c + new Vector2(2f, 1.5f), s2 * 0.9f, new Color(0, 0, 0, 0.16f));
                    DrawColoredPolygon(new[] {
                        c + new Vector2(-s2, 2f * _scale), c + new Vector2(-s2 * 0.4f, -s2 * 0.85f),
                        c + new Vector2(s2 * 0.55f, -s2 * 0.6f), c + new Vector2(s2, 2f * _scale) },
                        new Color(0.46f, 0.46f, 0.43f));
                    DrawLine(c + new Vector2(-s2 * 0.2f, -s2 * 0.7f), c + new Vector2(s2 * 0.4f, -s2 * 0.5f),
                             new Color(0.72f, 0.74f, 0.70f, 0.7f), 1.4f * _scale);
                }
                else if (t == Terrain.Water && h > 0.70f)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var b0 = c + new Vector2((k - 1) * 4f * _scale, 0);
                        DrawLine(b0, b0 + new Vector2(1.5f * _scale + Mathf.Sin(_t + k) * _scale, -10f * _scale),
                                 new Color(0.55f, 0.72f, 0.58f, 0.65f), 1.2f * _scale);
                    }
                }
            }
    }

    private float Elev(int x, int y)
    {
        if (x < 0 || y < 0 || x >= World.W || y >= World.H) return -9999f;
        return _w.Tiles[x, y] switch
        {
            Terrain.Cliff => LIFT * 2f,
            Terrain.Rock => LIFT,
            _ => 0f
        };
    }

    private void DrawTile(int x, int y)
    {
        var terr = _w.Tiles[x, y];
        float z = terr == Terrain.Cliff ? LIFT * 2 : terr == Terrain.Rock ? LIFT : 0f;
        var c = Iso(x, y, z);
        float hw = TW * 0.5f * _scale, hh = TH * 0.5f * _scale;

        if (z > 0)
        {
            // 侧面（岩体厚度）：只在邻格更低时露出，避免整圈锯齿裙边
            if (Elev(x + 1, y) < z)
            {
                DrawColoredPolygon(new[]
                {
                    Iso(x, y, z), Iso(x + 1, y, z), Iso(x + 1, y, 0), Iso(x, y, 0)
                }, C_Side.Darkened(0.12f));
            }
            if (Elev(x, y + 1) < z)
            {
                DrawColoredPolygon(new[]
                {
                    Iso(x, y, z), Iso(x, y + 1, z), Iso(x, y + 1, 0), Iso(x, y, 0)
                }, C_Side);
            }
        }

        Color fill = terr switch
        {
            Terrain.Grass => ((x + y) % 2 == 0) ? C_Grass : C_Grass2,
            Terrain.Rock => C_Rock,
            Terrain.Sand => C_Sand,
            Terrain.Water => C_Water,
            _ => C_Cliff
        };
        // 生境 → 图集格子（3×3：草地/林莽/沙碛 ｜ 山岭/山岭2/绝壁 ｜ 水泽/泥滩/灵田）
        int cell = _w.BiomeAt(x, y) switch
        {
            1 => 0,   // 林莽：草底，树由地物层长出来（避免同树平铺成墙纸）
            3 => 3,   // 雪岭：山岩
            4 => 2,   // 荒漠：沙碛
            5 => 6,   // 水泽
            6 => 5,   // 绝壁
            _ => 0    // 寒原/沃野：草地
        };
        // 远处压冷、近处加暖 + 顶面受光：一张平涂的地就有了纵深
        float depth = (x + y) / (float)(World.W + World.H);
        fill = fill.Lerp(new Color(0.36f, 0.46f, 0.52f), depth * 0.22f);
        fill = fill.Lerp(new Color(0.86f, 0.78f, 0.60f), (1f - depth) * 0.10f);
        fill = fill.Lightened(0.05f);

        if (_tiles != null)
        {
            // 写实贴片：图集一格铺满一个菱形，冷压远、暖提近
            const float AW = 640f, AH = 602f;
            float col = (cell % 3) * AW, row = (cell / 3) * AH;
            var mod = new Color(1f, 1f, 1f);
            mod = mod.Lerp(new Color(0.62f, 0.72f, 0.82f), depth * 0.42f);          // 远景压冷
            mod = mod.Lerp(new Color(1f, 0.94f, 0.82f), (1f - depth) * 0.16f);        // 近景提暖
            int bio = _w.BiomeAt(x, y);
            if (bio == 0) mod = mod.Lerp(new Color(0.74f, 0.82f, 0.92f), 0.34f);       // 寒原
            else if (bio == 1) mod = mod.Lerp(new Color(0.70f, 0.80f, 0.68f), 0.20f);  // 林荫压暗
            else if (bio == 2) mod = mod.Lerp(new Color(1f, 0.94f, 0.78f), 0.14f);     // 沃野朝阳
            if (_w.Season == Season.Winter) mod = mod.Lerp(new Color(0.82f, 0.88f, 0.96f), 0.30f);
            else if (_w.Season == Season.Autumn) mod = mod.Lerp(new Color(1f, 0.86f, 0.66f), 0.12f);
            DrawTextureRectRegion(_tiles, new Rect2(c.X - hw, c.Y - hw, hw * 2f, hw * 2f), new Rect2(col, row, AW, AH), mod);
        }
        else
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(c.X, c.Y - hh), new Vector2(c.X + hw, c.Y),
                new Vector2(c.X, c.Y + hh), new Vector2(c.X - hw, c.Y)
            }, fill);
        }

        // 瓦片描边（1px 墨线，压住平涂感）
        if (_tiles == null)
            DrawPolyline(new[]
            {
                new Vector2(c.X, c.Y - hh), new Vector2(c.X + hw, c.Y),
                new Vector2(c.X, c.Y + hh), new Vector2(c.X - hw, c.Y),
                new Vector2(c.X, c.Y - hh)
            }, new Color(0.05f, 0.09f, 0.07f, 0.10f), 1f);

        // 水质高光
        if (terr == Terrain.Water)
            DrawLine(new Vector2(c.X - hw * 0.45f, c.Y), new Vector2(c.X + hw * 0.1f, c.Y - hh * 0.35f),
                     new Color(0.62f, 0.86f, 0.88f, 0.25f), 1f);
    }

    // ---------------------------------------------------------------- 立绘 / 拆装
    private readonly System.Collections.Generic.List<(Rect2 R, System.Action F)> _hot = new();

    private static readonly string[] LayerNames = { "底衣", "外袍", "下裳", "腰带", "头饰", "佩饰", "发型" };

    private void DrawPortrait(Vector2 vp)
    {
        _hot.Clear();
        var sel = _w.Npcs.Find(n => n.Id == _selected && n.Alive);
        if (sel == null) return;

        float u = vp.X / 720f;
        var panel = new Rect2(vp.X * 0.025f, vp.Y * 0.556f, vp.X * 0.95f, vp.Y * 0.204f);
        RR(panel, 14f * u, new Color(0.06f, 0.10f, 0.085f, 0.96f), UI_Gold with { A = 0.28f }, 1.2f);
        VGrad(new Rect2(panel.Position.X + 1.5f * u, panel.Position.Y + 1.5f * u, panel.Size.X - 3f * u, panel.Size.Y * 0.34f),
              new Color(1f, 1f, 1f, 0.035f), new Color(0f, 0f, 0f, 0f), 8);
        DrawLine(new Vector2(panel.Position.X + 18f * u, panel.Position.Y + 20f * u),
                 new Vector2(panel.Position.X + panel.Size.X * 0.28f, panel.Position.Y + 20f * u),
                 UI_Gold with { A = 0.25f }, 1f);

        var fig = new Rect2(panel.Position.X + 8f * u, panel.Position.Y + 10f * u, panel.Size.X * 0.255f, panel.Size.Y - 20f * u);
        RR(fig, 10f * u, new Color(0.10f, 0.16f, 0.135f, 0.9f), new Color(0.55f, 0.75f, 0.62f, 0.16f), 1f);
        DrawCircle(new Vector2(fig.GetCenter().X, fig.Position.Y + fig.Size.Y * 0.5f), fig.Size.X * 0.42f,
                   new Color(0.35f, 0.55f, 0.46f, 0.07f));
        if (_portrait != null)
        {
            // cover：按框比例裁原图中心，避免压扁人脸
            float ia = 1024f / 1536f, fa = fig.Size.X / fig.Size.Y;
            Rect2 src;
            if (fa > ia) { float hh2 = 1024f / fa; src = new Rect2(0, (1536f - hh2) * 0.42f, 1024f, hh2); }
            else { float ww2 = 1536f * fa; src = new Rect2((1024f - ww2) * 0.5f, 0f, ww2, 1536f); }
            DrawTextureRectRegion(_portrait, fig, src, Colors.White);
            var t0 = sel.Realm >= 7 ? new Color(1f, 0.92f, 0.70f) : new Color(0.80f, 0.92f, 0.86f);
            VGrad(new Rect2(fig.Position.X, fig.End.Y - fig.Size.Y * 0.30f, fig.Size.X, fig.Size.Y * 0.30f),
                  new Color(0f, 0f, 0f, 0f), new Color(0.02f, 0.05f, 0.04f, 0.92f), 10);
            DrawString(_font, new Vector2(fig.Position.X + 6f * u, fig.End.Y - 8f * u),
                       sel.Realm >= 7 ? "宗师" : sel.Realm >= 4 ? "修士" : "凡民",
                       HorizontalAlignment.Left, -1, (int)(11f * u), t0);
        }
        else DrawFigure(sel, fig);

        float tx = panel.Position.X + panel.Size.X * 0.30f;
        float fs = 24f * u;
        float lineY = panel.Position.Y + 30f * u;

        string rank = sel.Realm >= 7 ? "宗师" : sel.Realm >= 4 ? "修士" : "凡民";
        DrawString(_font, new Vector2(tx, lineY), $"{sel.Name}　{sel.Realm}境 · {rank}",
                   HorizontalAlignment.Left, -1, (int)fs, C_Paper);
        lineY += fs * 1.35f;
        string quadText = sel.Quad switch { 0 => "出身显赫 · 天赋异禀", 2 => "出身显赫 · 禀赋平平", 1 => "寒门 · 天赋异禀", _ => "寒门 · 禀赋平平" };
        DrawString(_font, new Vector2(tx, lineY), quadText, HorizontalAlignment.Left, -1, (int)(fs * 0.72f),
                   new Color(0.78f, 0.86f, 0.80f, 0.85f));
        lineY += fs * 1.1f;
        string live = sel.Lives > 1 ? $"第 {sel.Lives} 世 · 前世恩债 {sel.InheritedHelp}" : "第一世";
        DrawString(_font, new Vector2(tx, lineY), $"{live}　恩 {sel.Favor} 怨 {sel.Grudge}",
                   HorizontalAlignment.Left, -1, (int)(fs * 0.72f), new Color(0.78f, 0.86f, 0.80f, 0.85f));

        // 七层独立拆装 chip
        float chipW = Math.Max(44f, 44f * u);
        float availW = panel.Position.X + panel.Size.X - tx - 8f * u;
        float gap = Math.Max(2f, (availW - chipW * 7) / 6f);
        float chipY = panel.Position.Y + panel.Size.Y - 96f * u;
        for (int i = 0; i < 7; i++)
        {
            int bit = 1 << i;
            bool on = (sel.WearMask & bit) != 0;
            var r = new Rect2(tx + i * (chipW + gap), chipY, chipW, chipW * 0.62f);
            Pill(r, LayerNames[i], u, on, false, false);
            var npcRef = sel; int b = bit;
            _hot.Add((r, () => npcRef.WearMask ^= b));
        }

        // 身材 / 五官调节
        float btnY = panel.Position.Y + panel.Size.Y - 34f * u;
        string[] plus = { "高+", "宽+", "眼距+", "颌宽+" };
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect2(tx + i * (chipW + gap * 0.7f), btnY, chipW, 28f * u);
            Pill(r, plus[i], u, false, false, false);
            var npcRef = sel; int idx = i;
            _hot.Add((r, () =>
            {
                switch (idx)
                {
                    case 0: npcRef.Height = Math.Clamp(npcRef.Height + 0.06f, 0.70f, 1.35f); break;
                    case 1: npcRef.Girth = Math.Clamp(npcRef.Girth + 0.06f, 0.70f, 1.45f); break;
                    case 2: npcRef.EyeGap = Math.Clamp(npcRef.EyeGap + 0.05f, 0.75f, 1.25f); break;
                    case 3: npcRef.JawW = Math.Clamp(npcRef.JawW + 0.05f, 0.70f, 1.30f); break;
                }
            }));
        }
    }

    /// <summary>立绘：按 WearMask 逐层叠加，五官/身材由参数驱动 —— 每一层都可单独拆掉。</summary>
    private void DrawFigure(Npc n, Rect2 a)
    {
        float figH = a.Size.Y * 0.90f;
        float h = figH * (0.86f + n.Height * 0.16f);          // 身高只做 ±8%，不做怪物比例
        float cx = a.Position.X + a.Size.X * 0.5f;
        float baseY = a.Position.Y + a.Size.Y * 0.965f;

        float headR = h * 0.098f;                              // 约 7 头身
        float shoulder = a.Size.X * 0.235f * n.Girth;
        float waist = a.Size.X * 0.175f * n.Girth;

        var skin = new Color(0.93f, 0.86f, 0.76f);
        var robe = Color.FromHsv(n.Hue, 0.30f, n.Realm >= 4 ? 0.70f : 0.52f);
        var inner = new Color(0.88f, 0.86f, 0.80f);

        float hipY = baseY - h * 0.46f;
        float chestY = baseY - h * 0.78f;
        var headC = new Vector2(cx, baseY - h * 0.885f);

        // 下裳（独立一层）
        if ((n.WearMask & 0b100) != 0)
            DrawPoly(new[]{
                new Vector2(cx - waist, hipY), new Vector2(cx + waist, hipY),
                new Vector2(cx + waist * 1.42f, baseY), new Vector2(cx - waist * 1.42f, baseY) },
                robe.Darkened(0.32f));

        // 底衣（中衣）
        if ((n.WearMask & 0b1) != 0)
            DrawPoly(new[]{
                new Vector2(cx - waist * 0.92f, chestY), new Vector2(cx + waist * 0.92f, chestY),
                new Vector2(cx + waist * 1.08f, hipY + h * 0.04f), new Vector2(cx - waist * 1.08f, hipY + h * 0.04f) },
                inner);

        // 外袍（两襟 + 回袖）
        if ((n.WearMask & 0b10) != 0)
        {
            DrawPoly(new[]{
                new Vector2(cx - shoulder, chestY - h * 0.012f), new Vector2(cx - shoulder * 0.12f, chestY - h * 0.030f),
                new Vector2(cx - waist * 0.30f, hipY + h * 0.06f), new Vector2(cx - waist * 1.30f, hipY + h * 0.06f) },
                robe);
            DrawPoly(new[]{
                new Vector2(cx + shoulder * 0.12f, chestY - h * 0.030f), new Vector2(cx + shoulder, chestY - h * 0.012f),
                new Vector2(cx + waist * 1.30f, hipY + h * 0.06f), new Vector2(cx + waist * 0.30f, hipY + h * 0.06f) },
                robe);
            // 交领（压在外袍之上，深色）
            DrawPoly(new[]{
                new Vector2(cx - waist * 0.60f, chestY - h * 0.010f), new Vector2(cx, chestY + h * 0.055f),
                new Vector2(cx - waist * 0.10f, hipY + h * 0.02f) },
                robe.Darkened(0.55f));
            DrawLine(new Vector2(cx + waist * 0.60f, chestY - h * 0.010f), new Vector2(cx, chestY + h * 0.055f),
                     robe.Darkened(0.42f), Math.Max(2f, h * 0.012f));
        }

        // 双臂与袖（属于身体+外袍形制，随 Girth 变宽）
        {
            var sleeve = (n.WearMask & 0b10) != 0 ? robe.Darkened(0.08f) : inner.Darkened(0.05f);
            float armW = shoulder * 0.34f;
            foreach (float sign in new[] { -1f, 1f })
            {
                float ax = cx + sign * (shoulder * 0.92f);
                DrawPoly(new[]{
                    new Vector2(ax - armW, chestY + h * 0.012f), new Vector2(ax + armW, chestY + h * 0.018f),
                    new Vector2(ax + armW * 0.86f, hipY + h * 0.055f), new Vector2(ax - armW * 1.05f, hipY + h * 0.045f) },
                    sleeve);
                DrawPoly(new[]{
                    new Vector2(ax - armW * 1.12f, hipY + h * 0.045f), new Vector2(ax + armW * 1.0f, hipY + h * 0.055f),
                    new Vector2(ax + armW * 0.92f, hipY + h * 0.075f), new Vector2(ax - armW * 1.20f, hipY + h * 0.068f) },
                    sleeve.Darkened(0.25f));
            }
        }

        // 衣纹：三道竖向折线，平涂立刻有布感
        {
            var fold = (robe.Darkened(0.5f));
            for (int k = 0; k < 3; k++)
            {
                float fx = cx + (k - 1) * waist * 0.55f;
                DrawLine(new Vector2(fx, chestY + h * 0.06f), new Vector2(fx + (k - 1) * waist * 0.06f, hipY + h * 0.055f),
                         fold, Math.Max(1.2f, h * 0.004f));
            }
            // 手：袖口露出一点皮色，人是活的
            foreach (float sg in new[] { -1f, 1f })
                DrawCircle(new Vector2(cx + sg * shoulder * 0.86f, hipY + h * 0.070f), h * 0.014f, skin.Darkened(0.04f));
        }

        // 腰带（独立一层）
        if ((n.WearMask & 0b1000) != 0)
        {
            DrawRect(new Rect2(cx - waist * 1.20f, hipY - h * 0.02f, waist * 2.40f, h * 0.040f),
                     new Color(0.26f, 0.19f, 0.13f));
            DrawRect(new Rect2(cx - waist * 0.10f, hipY - h * 0.02f, waist * 0.20f, h * 0.040f),
                     Color.FromHsv(n.AccHue, 0.42f, 0.72f));
        }

        // 颈 + 头
        DrawRect(new Rect2(cx - headR * 0.34f, headC.Y + headR * 0.5f, headR * 0.68f, h * 0.032f), skin.Darkened(0.10f));
        float jaw = headR * (0.62f + n.JawW * 0.30f);
        // 十点轮廓：额宽、颧骨、收腮、尖颏 —— 圆头是"娃娃"，不是成年人
        var facePts = new[]
        {
            new Vector2(headC.X - headR * 0.74f, headC.Y - headR * 0.66f),
            new Vector2(headC.X - headR * 0.86f, headC.Y - headR * 0.30f),
            new Vector2(headC.X - headR * 0.84f, headC.Y + headR * 0.06f),
            new Vector2(headC.X - headR * 0.70f, headC.Y + headR * 0.38f),
            new Vector2(headC.X - jaw, headC.Y + headR * 0.60f),
            new Vector2(headC.X, headC.Y + headR * 0.70f),
            new Vector2(headC.X + jaw, headC.Y + headR * 0.60f),
            new Vector2(headC.X + headR * 0.70f, headC.Y + headR * 0.38f),
            new Vector2(headC.X + headR * 0.84f, headC.Y + headR * 0.06f),
            new Vector2(headC.X + headR * 0.86f, headC.Y - headR * 0.30f),
            new Vector2(headC.X + headR * 0.74f, headC.Y - headR * 0.66f)
        };
        DrawPoly(facePts, skin);
        // 颧骨阴影：只在受光对侧压一层，脸不再是一块平白
        DrawPoly(new[]{
            new Vector2(headC.X - headR * 0.84f, headC.Y + headR * 0.02f),
            new Vector2(headC.X - headR * 0.70f, headC.Y + headR * 0.40f),
            new Vector2(headC.X - jaw * 0.9f, headC.Y + headR * 0.58f),
            new Vector2(headC.X - headR * 0.40f, headC.Y + headR * 0.52f),
            new Vector2(headC.X - headR * 0.44f, headC.Y + headR * 0.10f) },
            skin.Darkened(0.10f));

        // 五官：眼距 / 眉高 / 眉锋 / 颌宽 全部参数化
        float eyeY = headC.Y + headR * 0.06f;
        float dx = headR * 0.34f * n.EyeGap;
        var ink = new Color(0.10f, 0.10f, 0.11f, 0.92f);
        foreach (float sign in new[] { -1f, 1f })
        {
            float ex = cx + sign * dx;
            // 眼白（杏形）
            DrawPoly(new[]{
                new Vector2(ex - headR * 0.15f, eyeY + headR * 0.030f), new Vector2(ex - headR * 0.04f, eyeY - headR * 0.020f),
                new Vector2(ex + headR * 0.06f, eyeY - headR * 0.015f), new Vector2(ex + headR * 0.15f, eyeY + headR * 0.030f),
                new Vector2(ex, eyeY + headR * 0.062f) },
                new Color(0.97f, 0.94f, 0.88f));
            // 瞳（深，微微偏内）
            DrawCircle(new Vector2(ex + sign * headR * 0.012f, eyeY + headR * 0.022f), headR * 0.040f, ink);
            DrawCircle(new Vector2(ex + sign * headR * 0.004f, eyeY + headR * 0.010f), headR * 0.014f, new Color(1f, 1f, 1f, 0.75f));
            // 上睑：压住眼球上缘，眼睛才有神
            DrawLine(new Vector2(ex - headR * 0.15f, eyeY + headR * 0.030f), new Vector2(ex + headR * 0.15f, eyeY + headR * 0.030f),
                     new Color(0.08f, 0.07f, 0.08f, 0.95f), Math.Max(1.6f, h * 0.0055f));
            DrawLine(new Vector2(ex - headR * 0.16f, eyeY + headR * 0.030f), new Vector2(ex - headR * 0.22f, eyeY + headR * 0.006f),
                     new Color(0.08f, 0.07f, 0.08f, 0.8f), Math.Max(1.2f, h * 0.004f));
        }

        float browY = eyeY - headR * 0.20f * n.BrowH;
        float bk = h * 0.012f;
        DrawLine(new Vector2(headC.X - dx - headR * 0.22f, browY + headR * 0.07f * n.BrowAngle),
                 new Vector2(headC.X - dx + headR * 0.13f, browY - headR * 0.07f * n.BrowAngle), ink, bk);
        DrawLine(new Vector2(headC.X + dx + headR * 0.22f, browY + headR * 0.07f * n.BrowAngle),
                 new Vector2(headC.X + dx - headR * 0.13f, browY - headR * 0.07f * n.BrowAngle), ink, bk);

        DrawLine(new Vector2(headC.X, eyeY + headR * 0.08f), new Vector2(headC.X - headR * 0.06f, headC.Y + headR * 0.34f),
                 skin.Darkened(0.22f), Math.Max(1.5f, h * 0.006f));
        DrawPoly(new[]{
            new Vector2(headC.X - headR * 0.11f, headC.Y + headR * 0.46f), new Vector2(headC.X + headR * 0.11f, headC.Y + headR * 0.46f),
            new Vector2(headC.X + headR * 0.06f, headC.Y + headR * 0.52f), new Vector2(headC.X - headR * 0.06f, headC.Y + headR * 0.52f) },
            new Color(0.58f, 0.26f, 0.26f, 0.85f));

        // 发型（独立一层）
        if ((n.WearMask & 0b1000000) != 0)
        {
            var hair = new Color(0.13f, 0.11f, 0.12f);
            DrawPoly(new[]{
                new Vector2(headC.X - headR * 1.02f, headC.Y - headR * 0.10f), new Vector2(headC.X + headR * 1.02f, headC.Y - headR * 0.10f),
                new Vector2(headC.X + headR * 0.86f, headC.Y - headR * 0.92f), new Vector2(headC.X - headR * 0.86f, headC.Y - headR * 0.92f) },
                hair);
            // 额前发缕：参差收边，才不会像戴了块黑板
            DrawPoly(new[]{
                new Vector2(headC.X - headR * 0.74f, headC.Y - headR * 0.62f), new Vector2(headC.X + headR * 0.74f, headC.Y - headR * 0.62f),
                new Vector2(headC.X + headR * 0.68f, headC.Y - headR * 0.50f), new Vector2(headC.X + headR * 0.30f, headC.Y - headR * 0.60f),
                new Vector2(headC.X - headR * 0.04f, headC.Y - headR * 0.47f), new Vector2(headC.X - headR * 0.42f, headC.Y - headR * 0.62f),
                new Vector2(headC.X - headR * 0.70f, headC.Y - headR * 0.52f) },
                hair);
            // 鬓：沿颊侧垂下，收窄脸型
            DrawPoly(new[]{
                new Vector2(headC.X - headR * 0.90f, headC.Y - headR * 0.34f), new Vector2(headC.X - headR * 0.80f, headC.Y - headR * 0.34f),
                new Vector2(headC.X - headR * 0.74f, headC.Y + headR * 0.34f), new Vector2(headC.X - headR * 0.92f, headC.Y + headR * 0.22f) },
                hair);
            DrawPoly(new[]{
                new Vector2(headC.X + headR * 0.90f, headC.Y - headR * 0.34f), new Vector2(headC.X + headR * 0.80f, headC.Y - headR * 0.34f),
                new Vector2(headC.X + headR * 0.74f, headC.Y + headR * 0.34f), new Vector2(headC.X + headR * 0.92f, headC.Y + headR * 0.22f) },
                hair);
            DrawRect(new Rect2(headC.X - headR * 0.09f, headC.Y - headR * 1.62f, headR * 0.18f, headR * 0.80f), hair);
            DrawRect(new Rect2(headC.X - headR * 0.09f, headC.Y - headR * 1.62f, headR * 0.18f, headR * 0.80f), hair);
        }

        // 头饰（冠 / 簪）
        if ((n.WearMask & 0b10000) != 0)
        {
            var gold = new Color(0.80f, 0.62f, 0.30f);
            DrawPoly(new[]{
                new Vector2(headC.X - headR * 0.66f, headC.Y - headR * 0.82f), new Vector2(headC.X + headR * 0.66f, headC.Y - headR * 0.82f),
                new Vector2(headC.X + headR * 0.52f, headC.Y - headR * 1.06f), new Vector2(headC.X - headR * 0.52f, headC.Y - headR * 1.06f) },
                gold);
            DrawLine(new Vector2(headC.X - headR * 0.75f, headC.Y - headR * 0.95f),
                     new Vector2(headC.X + headR * 0.75f, headC.Y - headR * 0.88f), gold, Math.Max(2f, h * 0.009f));
        }

        // 佩饰（独立一层）：佩玉 / 剑匣 / 葫芦 / 珠串
        if ((n.WearMask & 0b100000) != 0)
        {
            var acc = Color.FromHsv(n.AccHue, 0.50f, 0.80f);
            var at = new Vector2(cx + waist * 0.62f, hipY + h * 0.055f);
            switch (n.AccessoryKind)
            {
                case 0:
                    DrawCircle(at, headR * 0.26f, acc);
                    DrawLine(at, at + new Vector2(0, h * 0.045f), new Color(0.42f, 0.30f, 0.20f), Math.Max(1.5f, h * 0.005f));
                    break;
                case 1:
                    DrawLine(at + new Vector2(0, -h * 0.075f), at + new Vector2(0, h * 0.115f), new Color(0.30f, 0.26f, 0.22f), headR * 0.13f);
                    DrawRect(new Rect2(at.X - headR * 0.10f, at.Y - h * 0.085f, headR * 0.20f, headR * 0.10f), acc);
                    break;
                case 2:
                    DrawCircle(at + new Vector2(-headR * 0.06f, 0), headR * 0.20f, acc);
                    DrawCircle(at + new Vector2(headR * 0.12f, headR * 0.10f), headR * 0.14f, acc.Darkened(0.18f));
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        DrawCircle(at + new Vector2(i * headR * 0.20f, (i % 2) * headR * 0.12f), headR * 0.12f, acc);
                    break;
            }
        }

        // 双足（不随衣饰层，属于身体）
        var boot = new Color(0.20f, 0.17f, 0.15f);
        DrawRect(new Rect2(cx - waist * 0.95f, baseY - h * 0.020f, waist * 0.72f, h * 0.024f), boot);
        DrawRect(new Rect2(cx + waist * 0.23f, baseY - h * 0.020f, waist * 0.72f, h * 0.024f), boot);
    }

    private void DrawPoly(Vector2[] pts, Color c)
    {
        DrawColoredPolygon(pts, c);
    }

    private int AliveNations { get { int c = 0; foreach (var n in _w.Nations) if (n.Alive) c++; return c; } }
    private int AliveClans { get { int c = 0; foreach (var c2 in _w.Clans) if (c2.Alive) c++; return c; } }
    private int AliveSects { get { int c = 0; foreach (var s2 in _w.Sects) if (s2.Alive) c++; return c; } }

    private void DrawFeudalLayer()
    {
        float k = _scale;
        var vp = GetViewportRect().Size;
        bool OnScreen(Vector2 p) => p.X > -40 && p.X < vp.X + 40 && p.Y > 100 && p.Y < vp.Y * 0.90f;

        // 国度：护国界（护持越弱，界环越暗）
        foreach (var nat in _w.Nations)
        {
            if (!nat.Alive) continue;
            var c = Iso(nat.Gx + 0.5f, nat.Gy + 0.5f);
            if (!OnScreen(c)) continue;
            var sec = nat.SectId >= 0 && nat.SectId < _w.Sects.Count ? _w.Sects[nat.SectId] : null;
            float prot = sec != null && sec.Alive ? sec.Protection : 0f;
            DrawCircle(c, 54f * k, new Color(0.72f, 0.58f, 0.32f, 0.05f + prot * 0.05f));
            float rx = 54f * k, ry = 27f * k;
            DrawPolyline(new[]
            {
                new Vector2(c.X - rx, c.Y), new Vector2(c.X, c.Y + ry),
                new Vector2(c.X + rx, c.Y), new Vector2(c.X, c.Y - ry), new Vector2(c.X - rx, c.Y)
            }, new Color(0.80f, 0.63f, 0.34f, 0.22f + prot * 0.33f), 1.4f);
            DrawString(_font, new Vector2(c.X - 18 * k, c.Y + 34 * k), nat.Name,
                       HorizontalAlignment.Left, -1, (int)(10 * k) + 6, new Color(0.86f, 0.74f, 0.50f, 0.85f));
        }

        // 家族：小宅
        foreach (var clan in _w.Clans)
        {
            if (!clan.Alive) continue;
            var c = Iso(clan.Gx + 0.5f, clan.Gy + 0.5f);
            if (!OnScreen(c)) continue;
            float w2 = 7f * k, h2 = 5f * k;
            DrawRect(new Rect2(c.X - w2, c.Y - h2, w2 * 2, h2), new Color(0.30f, 0.38f, 0.34f, 0.95f));
            DrawColoredPolygon(new[]
            {
                new Vector2(c.X - w2 * 1.15f, c.Y - h2),
                new Vector2(c.X + w2 * 1.15f, c.Y - h2),
                new Vector2(c.X, c.Y - h2 - 5 * k)
            }, new Color(0.48f, 0.41f, 0.34f));
        }

        // 宗门：塔（层数 = Tier），抽成越高檐色越赤
        foreach (var sec in _w.Sects)
        {
            if (!sec.Alive) continue;
            var c = Iso(sec.Gx + 0.5f, sec.Gy + 0.5f);
            if (!OnScreen(c)) continue;
            float w3 = (4.2f + sec.Tier * 1.5f) * k;
            float hh3 = (4.6f + sec.Tier * 2.0f) * k;
            var body = new Color(0.33f, 0.30f, 0.41f);
            var roof = new Color(0.55f, 0.58f, 0.53f).Lerp(new Color(0.66f, 0.30f, 0.24f), Math.Clamp((sec.Tithe - 0.18f) / 0.27f, 0, 1));
            for (int t2 = 0; t2 < sec.Tier; t2++)
            {
                float ww = w3 * (1f - t2 * 0.16f);
                float yTop = c.Y - (t2 + 1) * hh3 * 0.92f;
                DrawRect(new Rect2(c.X - ww, yTop, ww * 2, hh3 * 0.92f), body.Lightened(t2 * 0.07f));
                DrawColoredPolygon(new[]
                {
                    new Vector2(c.X - ww * 1.30f, yTop),
                    new Vector2(c.X + ww * 1.30f, yTop),
                    new Vector2(c.X, yTop - hh3 * 0.55f)
                }, roof);
            }
            DrawString(_font, new Vector2(c.X - 14 * k, c.Y + 13 * k), sec.Name,
                       HorizontalAlignment.Left, -1, (int)(9 * k) + 6, new Color(0.82f, 0.78f, 0.94f, 0.85f));
        }
    }

    private void DrawFlag(Vector2 p, int kind)
    {
        Color c = kind switch
        {
            0 => new Color("a05a3c"), 1 => new Color("c9a227"),
            2 => new Color("3f6f9e"), 3 => new Color("8a5ba6"), _ => new Color("b03a3a")
        };
        var top = p + new Vector2(0, -46 * _scale);
        DrawLine(p, top, new Color(0.15f, 0.12f, 0.1f, 0.9f), 2f * _scale);
        DrawColoredPolygon(new[] { top, top + new Vector2(11 * _scale, 4 * _scale), top + new Vector2(0, 12 * _scale) }, c);
    }

    private void DrawNpc(Npc n)
    {
        float bob = (float)Math.Sin(_t * 1.6f + n.Id * 0.7f) * 1.2f;
        var p = Iso(n.Gx + 0.5f, n.Gy + 0.5f);
        p.Y += bob * _scale;
        float g = n.Girth * _scale;
        float h = n.Height;

        // 落影（椭圆）
        var sh = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.Tau;
            sh[i] = p + new Vector2(Mathf.Cos(a) * 5.2f * g, Mathf.Sin(a) * 2.6f * g);
        }
        DrawColoredPolygon(sh, new Color(0, 0, 0, 0.30f));

        float bodyH = 18f * h * _scale;   // 身高影响整体（小人穿不了大人衣服的物理来源）
        float shoulder = 3.4f * g;
        float hem = 5.0f * g;
        var headC = p + new Vector2(0, -bodyH - 4.2f * h * _scale);
        float headR = 3.5f * h * _scale;
        var robe = n.Realm >= 3
            ? Color.FromHsv(n.Hue, 0.36f, 0.62f)
            : Color.FromHsv(n.Hue, 0.30f, 0.46f);

        // 袍身外描边（避免与地形糊在一起）
        DrawColoredPolygon(new[]
        {
            p + new Vector2(-hem - 1.2f, 1.2f), p + new Vector2(hem + 1.2f, 1.2f),
            p + new Vector2(shoulder + 1.2f, -bodyH - 0.6f), p + new Vector2(-shoulder - 1.2f, -bodyH - 0.6f)
        }, new Color(0.05f, 0.07f, 0.06f, 0.9f));

        // 袍身（梯形，下摆随体宽展开）
        DrawColoredPolygon(new[]
        {
            p + new Vector2(-hem, 0), p + new Vector2(hem, 0),
            p + new Vector2(shoulder, -bodyH), p + new Vector2(-shoulder, -bodyH)
        }, robe);
        // 衣褶（暗边，避免纯色塑料感）
        DrawLine(p + new Vector2(-hem * 0.9f, -1f), p + new Vector2(-shoulder * 0.8f, -bodyH + 2f),
                 robe.Darkened(0.35f), 1f);
        DrawLine(p + new Vector2(hem * 0.35f, -2f), p + new Vector2(shoulder * 0.55f, -bodyH + 3f),
                 robe.Lightened(0.22f), 1f);

        // 腰带
        DrawLine(p + new Vector2(-hem * 0.62f, -bodyH * 0.5f), p + new Vector2(hem * 0.62f, -bodyH * 0.5f),
                 new Color(0.16f, 0.13f, 0.11f, 0.85f), 1.4f * _scale);

        // 头
        DrawCircle(headC, headR, new Color("e8cdb4"));
        // 发
        DrawCircle(headC + new Vector2(0, -headR * 0.42f), headR * 0.98f, new Color("1d1a19"));
        DrawColoredPolygon(new[]
        {
            headC + new Vector2(-headR, 0.2f * headR), headC + new Vector2(headR, 0.2f * headR),
            headC + new Vector2(headR * 0.55f, -headR * 1.15f), headC + new Vector2(-headR * 0.6f, -headR * 1.1f)
        }, new Color("1d1a19"));

        // 境界标识：金丹以上有微光
        if (n.Realm >= 3)
            DrawCircle(headC + new Vector2(0, -headR * 2.1f), 1.9f * _scale, C_Jade with { A = 0.85f });

        // 选中环
        if (n.Id == _selected)
        {
            var ring = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.Tau;
                ring[i] = p + new Vector2(Mathf.Cos(a) * 9f * g, Mathf.Sin(a) * 4.6f * g);
            }
            DrawPolyline(new[] { ring[15], ring[0] }, C_Accent, 1.6f);
            DrawPolyline(ring, C_Accent, 1.6f);
            DrawString(_font, p + new Vector2(-24 * _scale, -bodyH - 12 * _scale),
                       n.Name, HorizontalAlignment.Left, -1, (int)(9 * _scale) + 6, C_Paper);
        }
    }

    private void DrawHud(Vector2 vp)
    {
        // 顶部条
        DrawRect(new Rect2(0, 0, vp.X, vp.Y * 0.152f), new Color(0.05f, 0.09f, 0.08f, 0.92f));
        DrawLine(new Vector2(0, vp.Y * 0.152f), new Vector2(vp.X, vp.Y * 0.152f), C_Jade with { A = 0.5f }, 1f);

        int fs = (int)(vp.X * 0.030f);
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.043f),
                   $"洪荒 · 第 {_w.Year} 年", HorizontalAlignment.Left, -1, fs, C_Paper);
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.092f),
                   $"国 {AliveNations} · 家 {AliveClans} · 宗 {AliveSects} · 抽成 {_w.TitheAvg * 100f:0}% · 分裂 {_w.Splits}",
                   HorizontalAlignment.Left, -1, (int)(fs * 0.68f), new Color(0.72f, 0.84f, 0.78f));
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.128f),
                   $"生民 {_w.AliveCount} · 恩 {_w.Favors} 怨 {_w.Grudges} · 组织 {_w.Factions.Count}" +
                   $"   通货 {_w.CurrencyWord}",
                   HorizontalAlignment.Left, -1, (int)(fs * 0.72f), new Color(0.72f, 0.84f, 0.78f));

        // 底部操作区
        var barY = vp.Y * 0.905f;
        DrawRect(new Rect2(0, barY, vp.X, vp.Y * 0.095f), new Color(0.05f, 0.09f, 0.08f, 0.92f));
        DrawString(_font, new Vector2(vp.X * 0.045f, barY + vp.Y * 0.040f),
                   _auto ? "自动推演中 · 点击暂停" : "时间自己走（1秒=1时辰）· 点「行/停」挂机或暂停 · 点「×N」加速",
                   HorizontalAlignment.Left, -1, (int)(fs * 0.70f), C_Paper);

        // 事件流（最近 3 条，置于下方留白区）
        int ly = (int)(vp.Y * 0.775f);
        for (int i = Math.Max(0, _w.Log.Count - 3); i < _w.Log.Count; i++)
        {
            string line = _w.Log[i].Text;
            if (line.Length > 26) line = line[..26] + "…";
            DrawString(_font, new Vector2(vp.X * 0.045f, ly),
                       line, HorizontalAlignment.Left, vp.X * 0.75f, (int)(fs * 0.62f),
                       new Color(0.80f, 0.90f, 0.84f, 0.55f));
            ly += (int)(fs * 0.90f);
        }
    }
}
