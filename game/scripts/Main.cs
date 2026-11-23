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

    public override void _Ready()
    {
        _w = new World(20261124L);
        _font = ThemeDB.FallbackFont;
        Resize();
        GetViewport().SizeChanged += Resize;
        // 预热 40 年，让世界已有组织与恩怨
        _w.Tick(40);
    }

    private void Resize()
    {
        var vp = GetViewportRect().Size;
        // 取景：一屏约 10 格宽（等距），地图外溢被裁切 —— 这才是 2.5D 的正经取景
        _scale = vp.X / (10f * TW);
        _origin = new Vector2(vp.X * 0.5f, vp.Y * 0.235f);
    }

    private bool _smoke;

    public override void _Process(double delta)
    {
        if (!_smoke && OS.HasEnvironment("SMOKE_SHOT")) _smoke = true;
        if (_smoke && Engine.GetProcessFrames() == 45)
        {
            var img = GetViewport().GetTexture().GetImage();
            var path = OS.GetEnvironment("SMOKE_SHOT");
            img.SavePng(path);
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
        if (e is InputEventScreenTouch t && t.Pressed)
        {
            if (t.Position.Y < GetViewportRect().Size.Y * 0.10f) return; // 顶部按钮区
            if (t.Position.Y > GetViewportRect().Size.Y * 0.90f) { _auto = !_auto; return; }
            HitPick(t.Position);
            _w.Tick(1);
        }
        else if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Position.Y > GetViewportRect().Size.Y * 0.90f) { _auto = !_auto; return; }
            HitPick(mb.Position);
            _w.Tick(1);
        }
    }

    // 世界坐标 → 屏幕坐标（含缩放与原点）
    private Vector2 Iso(float gx, float gy, float z = 0f)
    {
        float sx = (gx - gy) * TW * 0.5f;
        float sy = (gx + gy) * TH * 0.5f - z;
        return _origin + new Vector2(sx, sy) * _scale;
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

        // 组织旗帜
        foreach (var f in _w.Factions)
        {
            var anchor = f.Members.Count > 0 ? _w.Npcs.Find(n => n.Id == f.Members[0]) : null;
            if (anchor == null) continue;
            var p = Iso(anchor.Gx + 0.5f, anchor.Gy + 0.5f);
            DrawFlag(p, f.Kind);
        }

        // 人物：按深度排序
        var drawList = new System.Collections.Generic.List<Npc>();
        foreach (var n in _w.Npcs) if (n.Alive) drawList.Add(n);
        drawList.Sort((a, b) => (a.Gx + a.Gy).CompareTo(b.Gx + b.Gy));
        foreach (var n in drawList) DrawNpc(n);

        // 暗角（四边渐深，聚焦版图）
        var vg = new Color(0, 0, 0, 0.22f);
        DrawRect(new Rect2(0, 0, vp.X, vp.Y * 0.06f), vg);
        DrawRect(new Rect2(0, vp.Y * 0.94f, vp.X, vp.Y * 0.06f), vg);

        DrawHud(vp);
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
        DrawColoredPolygon(new[]
        {
            new Vector2(c.X, c.Y - hh), new Vector2(c.X + hw, c.Y),
            new Vector2(c.X, c.Y + hh), new Vector2(c.X - hw, c.Y)
        }, fill);

        // 瓦片描边（1px 墨线，压住平涂感）
        DrawPolyline(new[]
        {
            new Vector2(c.X, c.Y - hh), new Vector2(c.X + hw, c.Y),
            new Vector2(c.X, c.Y + hh), new Vector2(c.X - hw, c.Y),
            new Vector2(c.X, c.Y - hh)
        }, new Color(0.05f, 0.09f, 0.07f, 0.16f), 1f);

        // 水质高光
        if (terr == Terrain.Water)
            DrawLine(new Vector2(c.X - hw * 0.45f, c.Y), new Vector2(c.X + hw * 0.1f, c.Y - hh * 0.35f),
                     new Color(0.62f, 0.86f, 0.88f, 0.25f), 1f);
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
        DrawRect(new Rect2(0, 0, vp.X, vp.Y * 0.115f), new Color(0.05f, 0.09f, 0.08f, 0.92f));
        DrawLine(new Vector2(0, vp.Y * 0.115f), new Vector2(vp.X, vp.Y * 0.115f), C_Jade with { A = 0.5f }, 1f);

        int fs = (int)(vp.X * 0.030f);
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.043f),
                   $"洪荒 · 第 {_w.Year} 年", HorizontalAlignment.Left, -1, fs, C_Paper);
        DrawString(_font, new Vector2(vp.X * 0.045f, vp.Y * 0.092f),
                   $"生民 {_w.AliveCount}   恩怨 {_w.Grudges}/{_w.Favors}   组织 {_w.Factions.Count}" +
                   (_w.CurrencyWord != "" ? $"   通货 {_w.CurrencyWord}" : ""),
                   HorizontalAlignment.Left, -1, (int)(fs * 0.72f), new Color(0.72f, 0.84f, 0.78f));

        // 选中角色卡（立绘将来从这里弹出）
        var sel = _w.Npcs.Find(n => n.Id == _selected && n.Alive);
        if (sel != null)
        {
            var card = new Rect2(vp.X * 0.045f, vp.Y * 0.615f, vp.X * 0.44f, vp.Y * 0.135f);
            DrawRect(card, new Color(0.06f, 0.10f, 0.09f, 0.88f));
            DrawRect(card, new Color(0.56f, 0.79f, 0.66f, 0.45f), false, 1f);
            DrawString(_font, card.Position + new Vector2(14, 30), sel.Name,
                       HorizontalAlignment.Left, -1, fs, C_Paper);
            DrawString(_font, card.Position + new Vector2(14, 56),
                       $"{sel.Realm} 境 · 身高 {sel.Height:0.00} · 体宽 {sel.Girth:0.00}",
                       HorizontalAlignment.Left, card.Size.X - 20, (int)(fs * 0.6f), new Color(0.7f, 0.85f, 0.78f));
            DrawString(_font, card.Position + new Vector2(14, 80),
                       $"记恩 {sel.Favor} · 记恨 {sel.Grudge} · 因果 {sel.Karma:0}",
                       HorizontalAlignment.Left, card.Size.X - 20, (int)(fs * 0.6f), new Color(0.85f, 0.62f, 0.45f));
        }

        // 底部操作区
        var barY = vp.Y * 0.905f;
        DrawRect(new Rect2(0, barY, vp.X, vp.Y * 0.095f), new Color(0.05f, 0.09f, 0.08f, 0.92f));
        DrawString(_font, new Vector2(vp.X * 0.045f, barY + vp.Y * 0.040f),
                   _auto ? "自动推演中 · 点击暂停" : "点击画面推进一年 · 点击此处自动推演",
                   HorizontalAlignment.Left, -1, (int)(fs * 0.70f), C_Paper);

        // 事件流（最近 3 条，置于下方留白区）
        int ly = (int)(vp.Y * 0.775f);
        for (int i = Math.Max(0, _w.Log.Count - 3); i < _w.Log.Count; i++)
        {
            string line = _w.Log[i];
            if (line.Length > 26) line = line[..26] + "…";
            DrawString(_font, new Vector2(vp.X * 0.045f, ly),
                       line, HorizontalAlignment.Left, vp.X * 0.75f, (int)(fs * 0.62f),
                       new Color(0.80f, 0.90f, 0.84f, 0.55f));
            ly += (int)(fs * 0.90f);
        }
    }
}
