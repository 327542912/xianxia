using System;
using Godot;

namespace Xianxia;

/// <summary>
/// 界面基础件：圆角面板、胶囊按钮、进度条、描边。全部用顶点多边形自己画，
/// 不依赖主题资源，换来的是手机上一致的质感和可预期的圆角。
/// </summary>
public partial class Main
{
    private static readonly Color UI_Ink = new("0d1512");
    private static readonly Color UI_Panel = new(0.055f, 0.085f, 0.075f, 0.94f);
    private static readonly Color UI_Line = new(0.62f, 0.82f, 0.68f, 0.22f);
    private static readonly Color UI_Gold = new("d9b463");
    private static readonly Color UI_Jade = new("9fd9bb");
    private static readonly Color UI_Text = new("efe6d2");
    private static readonly Color UI_Sub = new(0.78f, 0.85f, 0.80f, 0.78f);

    /// <summary>圆角矩形（多边形拼出，圆角段数固定，手机上也稳）。</summary>
    private void RR(Rect2 r, float rad, Color fill, Color? border = null, float bw = 1.4f)
    {
        var pts = RoundPts(r, rad);
        DrawColoredPolygon(pts, fill);
        if (border.HasValue)
        {
            var b = border.Value;
            for (int i = 0; i < pts.Length; i++)
                DrawLine(pts[i], pts[(i + 1) % pts.Length], b, bw);
        }
    }

    private Vector2[] RoundPts(Rect2 r, float rad)
    {
        rad = Mathf.Min(rad, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f);
        var list = new System.Collections.Generic.List<Vector2>(40);
        int seg = 5;
        var corners = new (Vector2 C, float A0)[] {
            (new Vector2(r.Position.X + r.Size.X - rad, r.Position.Y + rad), -Mathf.Pi * 0.5f),
            (new Vector2(r.Position.X + r.Size.X - rad, r.Position.Y + r.Size.Y - rad), 0f),
            (new Vector2(r.Position.X + rad, r.Position.Y + r.Size.Y - rad), Mathf.Pi * 0.5f),
            (new Vector2(r.Position.X + rad, r.Position.Y + rad), Mathf.Pi)
        };
        foreach (var c in corners)
            for (int i = 0; i <= seg; i++)
            {
                float a = c.A0 + Mathf.Pi * 0.5f * i / seg;
                list.Add(c.C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad);
            }
        return list.ToArray();
    }

    /// <summary>竖向渐变底（分带绘制，避免 DrawRect 的平涂脸）。</summary>
    private void VGrad(Rect2 r, Color top, Color bot, int bands = 16)
    {
        float bh = r.Size.Y / bands;
        for (int i = 0; i < bands; i++)
            DrawRect(new Rect2(r.Position.X, r.Position.Y + i * bh, r.Size.X, bh + 1.2f), top.Lerp(bot, i / (float)(bands - 1)));
    }

    /// <summary>胶囊按钮：圆角 + 顶部细高光 + 文字。</summary>
    private void Pill(Rect2 r, string label, float u, bool on, bool accent = false, bool disabled = false)
    {
        var baseCol = accent ? new Color(0.42f, 0.26f, 0.16f, disabled ? 0.35f : 0.96f)
                             : new Color(0.09f, 0.15f, 0.13f, disabled ? 0.35f : 0.94f);
        RR(r, r.Size.Y * 0.5f, baseCol, on ? UI_Jade with { A = 0.9f } : UI_Line, on ? 2f : 1.2f);
        // 顶部高光条（让平涂有体积）
        DrawLine(new Vector2(r.Position.X + r.Size.Y * 0.35f, r.Position.Y + 1.2f),
                 new Vector2(r.End.X - r.Size.Y * 0.35f, r.Position.Y + 1.2f),
                 new Color(1f, 1f, 1f, disabled ? 0.05f : 0.16f), 1.2f);
        var tc = disabled ? new Color(1, 1, 1, 0.30f) : (accent ? new Color(1f, 0.94f, 0.82f) : UI_Text);
        int fs = (int)Mathf.Clamp(r.Size.Y * 0.52f, 11f * u, 20f * u);
        float tw = _font.GetStringSize(label, HorizontalAlignment.Left, -1, fs).X;
        DrawString(_font, new Vector2(r.Position.X + (r.Size.X - tw) * 0.5f, r.Position.Y + r.Size.Y * 0.5f + fs * 0.36f),
                   label, HorizontalAlignment.Left, -1, fs, tc);
    }

    /// <summary>细进度条：底槽 + 填充 + 端点微光。</summary>
    private void Bar(Rect2 r, float t, Color fill, Color? bg = null)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        RR(r, r.Size.Y * 0.5f, bg ?? new Color(0, 0, 0, 0.35f));
        if (t <= 0.01f) return;
        var fr = new Rect2(r.Position, new Vector2(Mathf.Max(r.Size.Y, r.Size.X * t), r.Size.Y));
        RR(fr, r.Size.Y * 0.5f, fill);
        DrawCircle(new Vector2(fr.End.X - fr.Size.Y * 0.5f, fr.Position.Y + fr.Size.Y * 0.5f), fr.Size.Y * 0.42f,
                   fill with { A = 0.55f });
    }

    /// <summary>玩家自己的立绘高亮框（选中态）</summary>
    private void SelectFrame(Rect2 r, float u)
    {
        RR(r, 10f * u, new Color(0.10f, 0.16f, 0.14f, 0.55f), UI_Gold with { A = 0.35f }, 1.2f);
    }
}
