using System;
using System.Collections.Generic;
using Godot;
using SimCore;

namespace Xianxia;

// 底部操作面板：把 SimCore 的 Act(verb) 接到屏幕上。
// 布局：左 立绘（点一下换装）｜中 身份+属性+最近事件｜右 行动按钮 3x3
public partial class Main
{
    private static readonly (string L, string V)[] Acts =
    {
        ("修行", "meditate"), ("行善", "help"), ("夺灵", "rob"),
        ("立洞府", "cave"), ("入宗门", "sect"), ("开洞天", "open"),
        ("游历", "explore"), ("种灵植", "plant"), ("渡门", "gate"),
    };

    private void DrawActionPanel(Vector2 vp)
    {
        float u = vp.X / 720f;
        var panel = new Rect2(vp.X * 0.025f, vp.Y * 0.556f, vp.X * 0.95f, vp.Y * 0.204f);
        RR(panel, 14f * u, new Color(0.06f, 0.10f, 0.085f, 0.96f), UI_Gold with { A = 0.28f }, 1.2f);
        VGrad(new Rect2(panel.Position.X + 1.5f * u, panel.Position.Y + 1.5f * u,
                        panel.Size.X - 3f * u, panel.Size.Y * 0.34f),
              new Color(1f, 1f, 1f, 0.035f), new Color(0f, 0f, 0f, 0f), 8);

        var ply = Ply;
        bool alive = ply != null && ply.Alive && !_w.Player.Dead;

        // ---- 左：立绘（点一下随机换装，给"拆装"一个入口）
        var fig = new Rect2(panel.Position.X + 8f * u, panel.Position.Y + 10f * u,
                            panel.Size.X * 0.255f, panel.Size.Y - 20f * u);
        RR(fig, 10f * u, new Color(0.10f, 0.16f, 0.135f, 0.9f), new Color(0.55f, 0.75f, 0.62f, 0.16f), 1f);
        DrawCircle(new Vector2(fig.GetCenter().X, fig.Position.Y + fig.Size.Y * 0.5f), fig.Size.X * 0.42f,
                   new Color(0.35f, 0.55f, 0.46f, 0.07f));
        if (ply != null) DrawFigure(ply, fig);
        _hot.Add((fig, () =>
        {
            if (ply == null) return;
            ply.WearMask = 0x7F & ~(1 << (int)(_t * 3f) % 7);
            ply.Hue = (ply.Hue + 0.13f) % 1f;
        }));

        // ---- 中：身份 + 属性 + 最近事件
        float tx = panel.Position.X + panel.Size.X * 0.30f;
        float fs = 24f * u;
        float lineY = panel.Position.Y + 30f * u;
        float midW = panel.Size.X * 0.36f;

        if (ply == null)
        {
            DrawString(_font, new Vector2(tx, lineY), "尚未投生", HorizontalAlignment.Left, -1, (int)fs, C_Paper);
            lineY += fs * 1.3f;
            DrawString(_font, new Vector2(tx, lineY), "点下方「投生」入世。",
                       HorizontalAlignment.Left, -1, (int)(fs * 0.72f), new Color(0.78f, 0.86f, 0.80f, 0.85f));
        }
        else
        {
            string rank = ply.Realm >= 7 ? "宗师" : ply.Realm >= 4 ? "修士" : "凡民";
            DrawString(_font, new Vector2(tx, lineY), $"{ply.Name}　{ply.Realm}境 · {rank}",
                       HorizontalAlignment.Left, -1, (int)fs, C_Paper);
            lineY += fs * 1.30f;
            string live = ply.Lives > 1 ? $"第 {ply.Lives} 世" : "第一世";
            DrawString(_font, new Vector2(tx, lineY),
                       $"{live} · 灵石 {_w.Player.Stone} · 名望 {_w.Player.Reputation:0}",
                       HorizontalAlignment.Left, -1, (int)(fs * 0.70f), new Color(0.78f, 0.86f, 0.80f, 0.85f));
            lineY += fs * 1.05f;
            DrawString(_font, new Vector2(tx, lineY),
                       $"恩 {ply.Favor} 怨 {ply.Grudge} · 业 {ply.Karma}",
                       HorizontalAlignment.Left, -1, (int)(fs * 0.70f), new Color(0.78f, 0.86f, 0.80f, 0.85f));
            lineY += fs * 1.05f;
            string ev = _w.Player.LastEvent ?? "";
            if (ev.Length > 16) ev = ev[..16] + "…";
            DrawString(_font, new Vector2(tx, lineY), ev, HorizontalAlignment.Left, midW,
                       (int)(fs * 0.66f), new Color(0.95f, 0.88f, 0.70f, 0.9f));
        }

        // 身死：中栏给一个显眼的「投生」按钮（行动按钮此时全部禁用）
        if (!alive)
        {
            var rb = new Rect2(tx, panel.Position.Y + panel.Size.Y - 46f * u, midW, 34f * u);
            Pill(rb, "投生 · 再入轮回", u, false, true, false);
            _hot.Add((rb, () => DoAct("reincarnate")));
        }

        // ---- 右：3x3 行动按钮
        float bx = tx + midW + 6f * u;
        float bw = panel.Position.X + panel.Size.X - bx - 10f * u;
        float gap = 5f * u;
        float cw = (bw - gap * 2f) / 3f;
        float ch = Mathf.Clamp(panel.Size.Y * 0.185f, 26f * u, 40f * u);
        float by = panel.Position.Y + (panel.Size.Y - (ch * 3f + gap * 2f)) * 0.5f;

        for (int i = 0; i < Acts.Length; i++)
        {
            var (label, verb) = Acts[i];
            int row = i / 3, col = i % 3;
            var r = new Rect2(bx + col * (cw + gap), by + row * (ch + gap), cw, ch);
            bool disabled = !alive;
            bool accent = verb == "meditate";
            // 本月已行一事：按钮压暗且不挂热区（SimCore 侧也有冷却兜底，不会白扣寿元）
            bool spent = alive && _w.Player.ActedYear == _w.Year && _w.Player.ActedMonth == _w.Month;
            if (spent) disabled = true;
            Pill(r, label, u, false, accent, disabled);
            if (!alive) continue;
            if (spent) continue;   // 本月已行一事：不挂热区，避免连点刷屏
            string v = verb;
            _hot.Add((r, () => DoAct(v)));
        }
    }

    private void DoAct(string verb)
    {
        string msg = _w.Player.Act(verb);
        if (!string.IsNullOrEmpty(msg)) _w.Player.LastEvent = msg;
        if (verb == "reincarnate") { _selected = _w.Player.NpcId; _auto = true; _autoAcc = 0f; }   // 投生即续流，别让玩家卡在暂停里
        if (_w.Player.Dead) _selected = -1;
        if (verb == "open" || verb == "cave" || verb == "reincarnate") CenterOnPlayer();
        QueueRedraw();
    }
}
