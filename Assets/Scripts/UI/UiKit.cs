using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Xianxia.UI
{
    /// <summary>
    /// 设计令牌（tokens）。界面上所有颜色、字号、间距、圆角都从这里取，
    /// 不再让每个面板自己写死坐标与颜色 —— 上一版 UI 丑在「每个面板各画一套」。
    /// </summary>
    public static class Tok
    {
        // 墨夜为底，纸白为字，金为骨，朱砂为警，青玉为生
        public static readonly Color Ink = Hex("0B1210");
        public static readonly Color Ink2 = Hex("12201A");
        public static readonly Color Panel = Hex("0E1714", 0.94f);
        public static readonly Color PanelHi = Hex("16241E", 0.96f);
        public static readonly Color Paper = Hex("F2EADB");
        public static readonly Color Sub = Hex("B9C4B6", 0.82f);
        public static readonly Color Faint = Hex("7F8F83", 0.55f);
        public static readonly Color Gold = Hex("D9B463");
        public static readonly Color GoldDim = Hex("D9B463", 0.22f);
        public static readonly Color Cinnabar = Hex("C05B45");
        public static readonly Color Jade = Hex("8FD3B4");
        public static readonly Color JadeDim = Hex("8FD3B4", 0.16f);
        public static readonly Color Scrim = new Color(0.02f, 0.05f, 0.04f, 0.62f);

        public const int Title = 44;
        public const int H1 = 34;
        public const int Body = 28;
        public const int Small = 24;
        public const int Caption = 21;

        public const int RadiusPanel = 22;
        public const int RadiusCard = 16;

        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
                if (_font == null)
                {
                    // 兜底：用系统 CJK 字体（Android 上一般有 Noto Sans CJK）
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Noto Sans CJK SC", "Noto Sans SC", "Droid Sans Fallback", "sans-serif" }, Body);
                }
                return _font;
            }
        }

        public static Color Hex(string hex, float? a = null)
        {
            if (!ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c)) c = Color.magenta;
            if (a.HasValue) c.a = a.Value;
            return c;
        }

        public static Color With(this Color c, float a) { c.a = a; return c; }
    }

    /// <summary>运行时生成 UI 用图：九宫圆角、渐变、细线。不依赖任何美术资源。</summary>
    public static class Sprites
    {
        private static readonly Dictionary<int, Sprite> Rounds = new();
        private static Sprite _white;

        public static Sprite White => _white ??= MakeSolid();

        private static Sprite MakeSolid()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "ui_white" };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>圆角矩形九宫图：同一个 sprite 可任意拉伸而不糊边。</summary>
        public static Sprite Round(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 96);
            if (Rounds.TryGetValue(radius, out var s)) return s;
            int size = radius * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ui_round_" + radius };
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - size * 0.5f + 0.5f) - (size * 0.5f - radius));
                    float dy = Mathf.Max(0f, Mathf.Abs(y - size * 0.5f + 0.5f) - (size * 0.5f - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = "round" + radius;
            Rounds[radius] = s;
            return s;
        }
    }

    /// <summary>小组件工厂：面板 / 文字 / 胶囊按钮 / 进度条 / 徽章。全部只设一次，不做每帧布局。</summary>
    public static class U
    {
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>按锚点与四边内缩建一个矩形。offMin/offMax 用像素，参考分辨率 1080×1920。</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = Node(parent, name);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        public static Image Img(Transform parent, string name, Color color, int radius = 0, float alpha = 1f)
        {
            var rt = Node(parent, name);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = radius > 0 ? Sprites.Round(radius) : Sprites.White;
            im.type = radius > 0 ? Image.Type.Sliced : Image.Type.Simple;
            im.color = color.With(color.a * alpha);
            im.raycastTarget = true;   // UI 面板默认吃点击：避免点面板穿透到地图
            return im;
        }

        public static Text Txt(Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rt = Node(parent, "txt");
            var tx = rt.gameObject.AddComponent<Text>();
            tx.font = Tok.Font;
            tx.fontSize = size;
            tx.color = color;
            tx.alignment = anchor;
            tx.text = content;
            tx.supportRichText = true;
            tx.horizontalOverflow = HorizontalWrapMode.Wrap;
            tx.verticalOverflow = VerticalWrapMode.Truncate;
            tx.raycastTarget = false;
            return tx;
        }

        /// <summary>胶囊/圆角按钮：底板 + 文字 + 点击。返回按钮本身，便于改 interactable / 颜色。</summary>
        public static Button Btn(Transform parent, string label, Color bg, Color fg, int size, Action onClick, int radius = 999)
        {
            var rt = Node(parent, "btn");
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Sprites.Round(Mathf.Min(radius, 44));
            im.type = Image.Type.Sliced;
            im.color = bg;
            im.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var tx = Txt(rt, label, size, fg, TextAnchor.MiddleCenter);
            tx.rectTransform.offsetMin = new Vector2(10, 0);
            tx.rectTransform.offsetMax = new Vector2(-10, 0);
            return btn;
        }

        public static Image Bar(Transform parent, float fill, Color fg, Color bg)
        {
            var back = Img(parent, "bar_bg", bg, 999);
            var frontRt = Node(back.transform, "fill");
            frontRt.anchorMin = new Vector2(0, 0); frontRt.anchorMax = new Vector2(Mathf.Clamp01(fill), 1);
            frontRt.offsetMin = Vector2.zero; frontRt.offsetMax = Vector2.zero;
            var front = frontRt.gameObject.AddComponent<Image>();
            front.sprite = Sprites.Round(999); front.type = Image.Type.Sliced;
            front.color = fg; front.raycastTarget = false;
            return back;
        }

        /// <summary>把已有 Image 的填充比例改掉（寿元/进度条用）。</summary>
        public static void SetBar(Image bar, float fill)
        {
            var f = bar.transform.Find("fill") as RectTransform;
            if (f == null) return;
            f.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
        }

        /// <summary>一枚方形「印」：单字徽记，做图标用，胜过 emoji 与生图。</summary>
        public static Image Seal(Transform parent, string glyph, Color bg, Color fg, int size = 64)
        {
            var im = Img(parent, "seal", bg, 999);
            im.rectTransform.sizeDelta = new Vector2(size, size);
            var t = Txt(im.transform, glyph, Mathf.RoundToInt(size * 0.52f), fg, TextAnchor.MiddleCenter);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = Vector2.zero; t.rectTransform.offsetMax = Vector2.zero;
            return im;
        }

        public static void Shadow(Text t, Color c, Vector2 dist)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = c; s.effectDistance = dist;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}