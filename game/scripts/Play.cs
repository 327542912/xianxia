using System;
using System.Collections.Generic;
using Godot;
using SimCore;

namespace Xianxia;

/// <summary>
/// 可玩层：把 SimCore 的一世接成手指可操作的界面。
/// 视觉：抬高视点的 2.5D（地物有高度与投影）、悬浮玻璃面板、胶囊命令键。
/// </summary>
public partial class Main
{
    private Vector2 _cam;
    private float _zoom = 1f;
    private string _msg = "你睁眼，先看清自己生在哪。";
    private float _msgAge = 999f;
    private readonly List<(Rect2 R, string Label, string Verb, int Arg)> _btns = new();
    private readonly List<(Vector2 At, float Age, int Element)> _fx = new();
    private int _pressed = -1;

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
        _cam = new Vector2(vp.X * 0.5f, vp.Y * 0.265f) - (_origin + new Vector2(sx, sy) * _scale);
        QueueRedraw();
    }

    // ---------------------------------------------------------------- 绘制

    private void PlayDraw(Vector2 vp)
    {
        float u = vp.X / 720f;
        DrawWorldFx(u);
        DrawStatusCard(vp, u);
        DrawCommands(vp, u);
        DrawToast(vp, u);
        if (_w.Player.Dead) DrawVerdict(vp, u);
        else DrawPlayerRing(u);
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
                0 => new Color(0.96f, 0.90f, 0.68f),
                1 => new Color(0.55f, 0.88f, 0.60f),
                2 => new Color(0.58f, 0.80f, 0.98f),
                3 => new Color(0.97f, 0.56f, 0.40f),
                4 => new Color(0.88f, 0.76f, 0.46f),
                5 => new Color(0.72f, 0.93f, 0.88f),
                6 => new Color(0.78f, 0.72f, 0.98f),
                _ => new Color(0.96f, 0.86f, 0.62f)
            };
            float r = (14f + 52f * k) * u;
            DrawArc(f.At, r, 0f, Mathf.Tau, 36, col with { A = (1f - k) * 0.85f }, (3.2f - 2.4f * k) * u);
            DrawArc(f.At, r * 0.62f, 0f, Mathf.Tau, 28, col with { A = (1f - k) * 0.5f }, 1.6f * u);
            for (int s = 0; s < 8; s++)
            {
                float a = s * Mathf.Tau / 8f + k * 1.8f;
                var pt = f.At + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.72f) * r;
                DrawCircle(pt, (3.4f - 2.4f * k) * u, col with { A = (1f - k) * 0.9f });
            }
        }
    }

    private void DrawStatusCard(Vector2 vp, float u)
    {
        var pn = Ply;
        var card = new Rect2(vp.X * 0.030f, vp.Y * 0.415f, vp.X * 0.940f, vp.Y * 0.052f);
        RR(card, 11f * u, UI_Panel, UI_Line, 1.2f);

        float tx = card.Position.X + 12f * u;
        string realmName = (pn?.Realm ?? 0) >= 7 ? "宗师" : (pn?.Realm ?? 0) >= 4 ? "修士" : "凡民";
        DrawString(_font, new Vector2(tx, card.Position.Y + 22f * u), $"{(pn?.Name ?? "—")}　{(pn?.Realm ?? 0)}境·{realmName}",
                   HorizontalAlignment.Left, -1, (int)(13f * u), UI_Text);
        DrawString(_font, new Vector2(tx, card.Position.Y + 38f * u), $"Y{_w.Year}　粟{_w.Player.Grain}　石{_w.Player.Stone}",
                   HorizontalAlignment.Left, -1, (int)(11f * u), UI_Sub);

        float bx = card.Position.X + card.Size.X * 0.40f;
        float bw = card.Size.X * 0.42f;
        float bh = 6.5f * u;
        float lifeT = pn == null ? 0f : 1f - Mathf.Clamp((pn.Age - 16f) / Mathf.Max(1f, pn.Lifespan - 16f), 0f, 1f);
        float labelW = 34f * u;
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

    private void DrawCommands(Vector2 vp, float u)
    {
        _btns.Clear();
        var spec = new (string L, string V, bool Key)[]
        {
            ("修炼", "meditate", true), ("帮人", "help", false), ("拒人", "refuse", false), ("夺取", "rob", false),
            ("占府", "cave", false), ("入宗", "sect", false), ("开辟", "open", false), ("过门", "gate", false),
            ("探索", "explore", false), ("待年", "wait", false), ("种植", "plant", true), ("开山", "delve", false)
        };
        float top = 0.474f, h = 0.0345f, gapx = 0.006f;
        float w = (0.940f - gapx * 5f) / 6f;
        bool dead = _w.Player.Dead;
        // 命令区底板：挡住地图，按钮才是实体而不是浮标
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

        float dq = Mathf.Min(vp.X * 0.055f, 46f * u);
        var dpad = new Rect2(vp.X * 0.985f - dq * 3f, vp.Y * 0.200f, dq * 3f, dq * 3f);
        RR(dpad, 12f * u, new Color(0.06f, 0.10f, 0.09f, 0.5f), UI_Line, 1f);
        var cells = new (int dx, int dy, string L, int D)[] { (1, 0, "北", 0), (2, 1, "东", 1), (1, 2, "南", 2), (0, 1, "西", 3) };
        foreach (var c in cells)
        {
            var rr = new Rect2(dpad.Position.X + c.dx * dq + 2f, dpad.Position.Y + c.dy * dq + 2f, dq - 4f, dq - 4f);
            Pill(rr, c.L, u, false, false, dead);
            if (!dead) _btns.Add((rr, c.L, "move", c.D));
        }
    }

    private void DrawToast(Vector2 vp, float u)
    {
        if (_msgAge > 4.5f) return;
        float a = Mathf.Clamp(1f - (_msgAge - 3.6f) / 0.9f, 0f, 1f);
        int fs = (int)(12.5f * u);
        float w = Mathf.Min(vp.X * 0.90f, _font.GetStringSize(_msg, HorizontalAlignment.Left, -1, fs).X + 24f * u);
        var r = new Rect2(vp.X * 0.05f, vp.Y * 0.392f, w, 24f * u);
        RR(r, 13f * u, new Color(0.04f, 0.07f, 0.06f, 0.92f * a), UI_Line, 1f);
        DrawString(_font, new Vector2(r.Position.X + 12f * u, r.Position.Y + 17.5f * u), _msg,
                   HorizontalAlignment.Left, (int)(r.Size.X - 20f * u), fs, UI_Text with { A = a });
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

    // ---------------------------------------------------------------- 输入

    private bool PlayTap(Vector2 p, Vector2 vp)
    {
        for (int i = 0; i < _btns.Count; i++)
        {
            var b = _btns[i];
            if (!b.R.HasPoint(p)) continue;
            if (b.Verb == "reincarnate")
            {
                _w.Act("reincarnate");
                _msg = _w.Player.LastEvent;
                CenterOnPlayer();
            }
            else
            {
                _msg = _w.Act(b.Verb, b.Arg);
                _pressed = i;
            }
            _msgAge = 0;
            if (_msg.Contains("突破") || b.Verb == "explore" || b.Verb == "delve" || b.Verb == "plant")
                _fx.Add((Iso((Ply?.Gx ?? 0) + 0.5f, (Ply?.Gy ?? 0) + 0.5f), 0f, (_w.Player.Reputation < 0 ? 3 : 1)));
            QueueRedraw();
            return true;
        }
        if (p.Y > vp.Y * 0.545f) return false;
        if (p.Y > vp.Y * 0.410f) return true;
        if (!_w.Player.Dead) StepToward(p);
        return true;
    }

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
                float d = Iso(nx + 0.5f, ny + 0.5f).DistanceTo(sp);
                if (d < bestD) { bestD = d; bx = nx; by = ny; }
            }
        if (bx < 0) { _msg = "四面皆阻，无处可去。"; return; }
        int dir = bx > cx ? 1 : bx < cx ? 3 : by > cy ? 2 : 0;
        _msg = _w.Act("move", dir);
        _msgAge = 0;
    }

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
