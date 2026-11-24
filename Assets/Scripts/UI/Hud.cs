using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SimCore;

namespace Xianxia.UI
{
    /// <summary>
    /// 界面唯一入口。上一版丑在「每个人各画一块面板、坐标互相抢位」，这一版只有一套版式：
    /// 顶栏（世界钟/速度）→ 人物卡 → 地图 → 事件条 → 指令坞 → 卷宗（底部弹层）。
    /// 所有间距、字号、圆角来自 Tok；所有面板互不重叠，触控热区 ≥ 88px。
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        private GameApp _app;
        private World W => _app.W;

        private RectTransform _root, _top, _charCard, _ticker, _dock, _npcCard, _sheet, _body, _verdict, _toast;
        private Text _clock, _season, _era, _pop, _toastTxt, _name, _realm, _grade, _stats, _event;
        private Text _npcName, _npcSub;
        private Image _lifeBar, _progBar, _toastBg;
        private Button _pause, _s1, _s2, _s4, _verdictBtn;
        private Text _sheetTitle, _verdictTitle, _verdictBody, _verdictFoot;
        private CanvasGroup _toastGroup;

        private readonly Dictionary<string, Button> _acts = new();
        private readonly Dictionary<string, Button> _tabs = new();
        private string _tab = "身世";
        private bool _tabDirty = true;
        private float _toastLife;
        private static Sprite _gradTop, _gradBottom;

        private const float W1080 = 1080f;
        private const float H1920 = 1920f;

        // ------------------------------------------------------------------ 构建
        public void Build(GameApp app)
        {
            _app = app;
            EnsureEventSystem();

            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var cv = canvasGo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W1080, H1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _root = U.Node(canvasGo.transform, "root");

            // 上下压角：地图与天空之间留出阅读带，文字永远压得住
            var topShade = GradImg(_root, "topShade", Tok.Ink.With(0.90f), Tok.Ink.With(0f));
            PlaceStretchTop(topShade.rectTransform, 0f, 300f);

            var dockShade = GradImg(_root, "dockShade", Tok.Ink.With(0f), Tok.Ink.With(0.96f));
            PlaceStretchBottom(dockShade.rectTransform, 0f, 620f);

            BuildTop();
            BuildCharCard();
            BuildTicker();
            BuildDock();
            BuildNpcCard();
            BuildSheet();
            BuildVerdict();
            BuildToast();

            Sync();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        private RectTransform At(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = U.Node(parent, name);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static void PlaceStretchTop(RectTransform rt, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, -top - height); rt.offsetMax = new Vector2(0, -top);
        }

        private static void PlaceStretchBottom(RectTransform rt, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0, bottom); rt.offsetMax = new Vector2(0, bottom + height);
        }

        private static Image GradImg(Transform parent, string name, Color top, Color bot)
        {
            var im = U.Img(parent, name, Color.white, 0);
            im.sprite = Grad(top, bot);
            im.type = Image.Type.Simple;
            im.raycastTarget = false;   // 压角只负责压暗，不挡地图点击
            return im;
        }

        private static Sprite Grad(Color top, Color bot)
        {
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { name = "ui_grad", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[128];
            for (int y = 0; y < 64; y++)
            {
                var c = Color.Lerp(bot, top, y / 63f);
                for (int x = 0; x < 2; x++) px[y * 2 + x] = c;
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildTop()
        {
            _top = U.Box(_root, "top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -120), new Vector2(-30, -22));
            var bg = U.Img(_top, "bg", Tok.Ink.With(0.55f), 20);
            PlaceStretch(bg.rectTransform);

            _clock = U.Txt(_top, "Y1 · 1月 春", Tok.H1, Tok.Paper);
            Place(_clock.rectTransform, 20, 4, 420, 46);

            _season = U.Txt(_top, "", Tok.Caption, Tok.Sub);
            Place(_season.rectTransform, 20, 48, 520, 30);

            var eraBg = U.Img(_top, "eraBg", Tok.JadeDim, 999);
            Place(eraBg.rectTransform, 440, 8, 210, 38);
            _era = U.Txt(_top, "洪荒·早期", Tok.Caption, Tok.Jade);
            Place(_era.rectTransform, 452, 10, 190, 34);
            _era.alignment = TextAnchor.MiddleCenter;

            _pop = U.Txt(_top, "", Tok.Caption, Tok.Faint);
            Place(_pop.rectTransform, 20, 76, 520, 26);

            _pause = U.Btn(_top, "暂停", Tok.PanelHi, Tok.Paper, Tok.Caption, () => _app.TogglePause(), 14);
            PlaceRight(_pause.GetComponent<RectTransform>(), 258, 10, 96, 52);

            _s1 = U.Btn(_top, "1×", Tok.PanelHi, Tok.Sub, Tok.Caption, () => _app.SetSpeed(1), 14);
            _s2 = U.Btn(_top, "2×", Tok.PanelHi, Tok.Sub, Tok.Caption, () => _app.SetSpeed(2), 14);
            _s4 = U.Btn(_top, "4×", Tok.PanelHi, Tok.Sub, Tok.Caption, () => _app.SetSpeed(4), 14);
            PlaceRight(_s1.GetComponent<RectTransform>(), 190, 10, 58, 52);
            PlaceRight(_s2.GetComponent<RectTransform>(), 126, 10, 58, 52);
            PlaceRight(_s4.GetComponent<RectTransform>(), 62, 10, 58, 52);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void PlaceRight(RectTransform rt, float fromRight, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-fromRight, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void PlaceStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void BuildCharCard()
        {
            _charCard = U.Box(_root, "char", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -336), new Vector2(750, -142));
            var bg = U.Img(_charCard, "bg", Tok.Panel.With(0.90f), Tok.RadiusPanel);
            PlaceStretch(bg.rectTransform);
            var line = U.Img(_charCard, "hair", Tok.GoldDim, 0);
            Place(line.rectTransform, 24, 0, 660, 2);

            var seal = U.Img(_charCard, "seal", Tok.JadeDim, 999);
            Place(seal.rectTransform, 16, 34, 78, 78);
            var sealT = U.Txt(seal.transform, "命", Tok.H1, Tok.Jade, TextAnchor.MiddleCenter);
            PlaceStretch(sealT.rectTransform);

            _name = U.Txt(_charCard, "", Tok.H1, Tok.Paper);
            Place(_name.rectTransform, 108, 14, 380, 46);

            var gradeBg = U.Img(_charCard, "gradeBg", Tok.Gold, 999);
            Place(gradeBg.rectTransform, 508, 18, 96, 34);
            _grade = U.Txt(_charCard, "凡品", Tok.Caption, Tok.Ink, TextAnchor.MiddleCenter);
            Place(_grade.rectTransform, 508, 18, 96, 34);

            _realm = U.Txt(_charCard, "", Tok.Small, Tok.Jade);
            Place(_realm.rectTransform, 108, 60, 360, 32);

            _stats = U.Txt(_charCard, "", Tok.Caption, Tok.Sub);
            Place(_stats.rectTransform, 108, 92, 360, 30);

            _lifeBar = U.Bar(_charCard, 0.5f, Tok.Cinnabar, Tok.Ink.With(0.65f));
            Place(_lifeBar.rectTransform, 108, 126, 300, 10);

            _progBar = U.Bar(_charCard, 0.3f, Tok.Jade, Tok.Ink.With(0.65f));
            Place(_progBar.rectTransform, 108, 140, 300, 7);

            _event = U.Txt(_charCard, "", Tok.Caption, Tok.Gold);
            Place(_event.rectTransform, 420, 96, 290, 60);
            _event.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void BuildTicker()
        {
            _ticker = U.Box(_root, "ticker", new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 330), new Vector2(1050, 388));
            var bg = U.Img(_ticker, "bg", Tok.Panel.With(0.86f), 14);
            PlaceStretch(bg.rectTransform);
            var seal = U.Seal(_ticker, "事", Tok.Cinnabar, Tok.Paper, 44);
            Place(seal.rectTransform, 8, 5, 38, 38);
            var t = U.Txt(_ticker, "", Tok.Small, Tok.Paper);
            Place(t.rectTransform, 56, 2, 960, 54);
            t.name = "tickerText";
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void BuildDock()
        {
            _dock = U.Box(_root, "dock", new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 16), new Vector2(1062, 316));
            var bg = U.Img(_dock, "bg", Tok.Panel.With(0.96f), 26);
            PlaceStretch(bg.rectTransform);

            string[][] rows =
            {
                new[] { "修行|meditate", "行善|help", "夺灵|rob", "洞府|cave", "入宗|sect" },
                new[] { "开辟|open", "渡门|gate", "游历|explore", "种灵|plant", "采脉|delve" },
                new[] { "升品|ascend", "炼宝|craft", "改流速|flow", "待月|wait", "卷宗|@tab" }
            };
            float[] ys = { 168f, 96f, 28f };
            float[] hs = { 72f, 62f, 46f };
            for (int r = 0; r < rows.Length; r++)
            {
                float w = (1008f - 4 * 12f) / 5f;
                for (int i = 0; i < 5; i++)
                {
                    var parts = rows[r][i].Split('|');
                    string label = parts[0], verb = parts[1];
                    bool accent = verb == "meditate";
                    var bgc = accent ? Tok.Hex("3A2C18") : Tok.PanelHi;
                    var fgc = accent ? Tok.Gold : Tok.Paper;
                    int fs = r == 0 ? Tok.Body : r == 1 ? Tok.Small : Tok.Caption;
                    var btn = U.Btn(_dock, label, bgc, fgc, fs, () =>
                    {
                        if (verb == "@tab") ShowTab("身世");
                        else _app.DoAct(verb);
                    }, 14);
                    Place(btn.GetComponent<RectTransform>(), 12 + i * (w + 12), ys[r], w, hs[r]);
                    _acts[verb] = btn;
                }
            }
        }

        private void BuildNpcCard()
        {
            _npcCard = U.Box(_root, "npc", new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 400), new Vector2(1050, 512));
            var bg = U.Img(_npcCard, "bg", Tok.Panel.With(0.94f), Tok.RadiusCard);
            PlaceStretch(bg.rectTransform);
            _npcName = U.Txt(_npcCard, "", Tok.Body, Tok.Paper);
            Place(_npcName.rectTransform, 20, 8, 560, 40);
            _npcSub = U.Txt(_npcCard, "", Tok.Caption, Tok.Sub);
            Place(_npcSub.rectTransform, 20, 50, 620, 54);

            var b1 = U.Btn(_npcCard, "帮", Tok.PanelHi, Tok.Jade, Tok.Small, () => _app.DoAct("help"), 14);
            var b2 = U.Btn(_npcCard, "拒", Tok.PanelHi, Tok.Sub, Tok.Small, () => _app.DoAct("refuse"), 14);
            var b3 = U.Btn(_npcCard, "夺", Tok.PanelHi, Tok.Cinnabar, Tok.Small, () => _app.DoAct("rob"), 14);
            PlaceRight(b1.GetComponent<RectTransform>(), 272, 26, 80, 62);
            PlaceRight(b2.GetComponent<RectTransform>(), 182, 26, 80, 62);
            PlaceRight(b3.GetComponent<RectTransform>(), 92, 26, 80, 62);

            _npcCard.gameObject.SetActive(false);
        }

        private void BuildSheet()
        {
            _sheet = U.Box(_root, "sheet", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrim = U.Img(_sheet, "scrim", Tok.Scrim, 0);
            PlaceStretch(scrim.rectTransform);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => _sheet.gameObject.SetActive(false));

            var panel = U.Img(_sheet, "panel", Tok.Panel.With(0.98f), 28);
            panel.rectTransform.anchorMin = new Vector2(0, 0);
            panel.rectTransform.anchorMax = new Vector2(1, 0);
            panel.rectTransform.pivot = new Vector2(0.5f, 0f);
            panel.rectTransform.offsetMin = new Vector2(0, 0);
            panel.rectTransform.offsetMax = new Vector2(0, 1240);
            panel.raycastTarget = true;

            var handle = U.Img(panel.transform, "handle", Tok.Faint, 999);
            Place(handle.rectTransform, 480, 12, 120, 6);

            _sheetTitle = U.Txt(panel.transform, "身世", Tok.H1, Tok.Paper);
            Place(_sheetTitle.rectTransform, 36, 28, 500, 48);

            var close = U.Btn(panel.transform, "×", Tok.PanelHi, Tok.Sub, Tok.H1, () => _sheet.gameObject.SetActive(false), 20);
            PlaceRight(close.GetComponent<RectTransform>(), 32, 24, 64, 64);

            string[] tabs = { "身世", "洞天", "宗门", "世系", "设定" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                var b = U.Btn(panel.transform, tab, Tok.PanelHi, Tok.Sub, Tok.Caption, () => ShowTab(tab), 999);
                Place(b.GetComponent<RectTransform>(), 32 + i * 128, 88, 116, 44);
                _tabs[tab] = b;
            }

            _body = U.Box(panel.transform, "body", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 24), new Vector2(0, -150));
            _body.anchoredPosition = new Vector2(0, 0);
            _sheet.gameObject.SetActive(false);
        }

        private void BuildVerdict()
        {
            _verdict = U.Box(_root, "verdict", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = U.Img(_verdict, "bg", Tok.Ink.With(0.97f), 0);
            PlaceStretch(bg.rectTransform);
            var card = U.Img(_verdict, "card", Tok.Panel.With(0.98f), Tok.RadiusPanel);
            card.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(900, 980);

            _verdictTitle = U.Txt(card.transform, "判 词", Tok.Title, Tok.Gold, TextAnchor.MiddleCenter);
            Place(_verdictTitle.rectTransform, 0, 60, 900, 72);

            _verdictBody = U.Txt(card.transform, "", Tok.H1, Tok.Paper, TextAnchor.UpperCenter);
            Place(_verdictBody.rectTransform, 70, 180, 760, 420);
            _verdictBody.lineSpacing = 1.35f;
            _verdictBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            _verdictFoot = U.Txt(card.transform, "", Tok.Small, Tok.Sub, TextAnchor.MiddleCenter);
            Place(_verdictFoot.rectTransform, 70, 630, 760, 120);
            _verdictFoot.horizontalOverflow = HorizontalWrapMode.Wrap;

            _verdictBtn = U.Btn(card.transform, "再入轮回", Tok.Gold, Tok.Ink, Tok.Body, () =>
            {
                _verdict.gameObject.SetActive(false);
                _app.DoAct("reincarnate");
            }, 999);
            Place(_verdictBtn.GetComponent<RectTransform>(), 250, 790, 400, 92);

            _verdict.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            _toast = U.Box(_root, "toast", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-330, -420), new Vector2(330, -356));
            _toastBg = U.Img(_toast, "bg", Tok.Ink.With(0.92f), 999);
            PlaceStretch(_toastBg.rectTransform);
            _toastTxt = U.Txt(_toast, "", Tok.Small, Tok.Paper, TextAnchor.MiddleCenter);
            PlaceStretch(_toastTxt.rectTransform);
            _toastTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
        }

        // ------------------------------------------------------------------ 数据同步
        public void SyncNow() { _syncPending = true; }
        private bool _syncPending;

        private void LateUpdate()
        {
            if (_toastLife > 0f)
            {
                _toastLife -= Time.deltaTime;
                _toastGroup.alpha = Mathf.Clamp01(_toastLife);
            }
            if (_syncPending) { _syncPending = false; Sync(); }
        }

        public void Sync()
        {
            if (W == null || _clock == null) return;
            var p = W.Player;
            var npc = W.PlayerNpc;

            _clock.text = $"Y{W.Year} · {W.Month + 1}月 {W.SeasonName}";
            _season.text = W.Phenology;
            _era.text = string.IsNullOrEmpty(p.Era) ? "洪荒·早期" : p.Era;
            _pop.text = $"在册 {(int)W.TotalPopulation:N0} 人 · {(int)W.AliveCount} 修士 · {W.CurrencyWord}为通货";
            _pause.GetComponentInChildren<Text>().text = _app.Paused ? "继续" : "暂停";
            _s1.image.color = _app.Speed == 1 && !_app.Paused ? Tok.Hex("3A2C18") : Tok.PanelHi;
            _s2.image.color = _app.Speed == 2 && !_app.Paused ? Tok.Hex("3A2C18") : Tok.PanelHi;
            _s4.image.color = _app.Speed == 4 && !_app.Paused ? Tok.Hex("3A2C18") : Tok.PanelHi;

            if (npc == null)
            {
                _name.text = "尚无肉身";
                _realm.text = "点「再入轮回」入世";
                _stats.text = "";
                _event.text = "";
                U.SetBar(_lifeBar, 0f);
                U.SetBar(_progBar, 0f);
            }
            else
            {
                _name.text = npc.Name;
                string kind = npc.Kind == BeingKind.Human ? "人" : npc.Kind == BeingKind.Beast ? "兽" : "草木";
                _realm.text = $"{kind} · {Cultivation.RealmName(npc.Kind, npc.Realm)}（{npc.Realm} 境） · 第 {npc.Lives} 世";
                _grade.text = Grades.MarkOf(npc.Grade) + "品";
                _grade.color = npc.Grade == Grade.Shen ? Tok.Ink : npc.Grade == Grade.Ling ? Tok.Ink : Tok.Paper;
                var gradeBg = _grade.transform.parent == _charCard ? _charCard.Find("gradeBg")?.GetComponent<Image>() : null;
                if (gradeBg != null) gradeBg.color = npc.Grade == Grade.Shen ? Tok.Gold : npc.Grade == Grade.Ling ? Tok.Jade : Tok.Faint;

                _stats.text = $"灵石 {p.Stone} · 名望 {p.Reputation:0} · 恩 {npc.Favor} 怨 {npc.Grudge}";
                float ageY = npc.Age / 12f, lifeY = Mathf.Max(1f, npc.Lifespan / 12f);
                U.SetBar(_lifeBar, Mathf.Clamp01(1f - ageY / lifeY));
                U.SetBar(_progBar, Mathf.Clamp01(npc.Prog));
                _event.text = Trim(p.LastEvent, 26);

                bool dead = p.Dead;
                bool busy = !dead && (W.Year == p.ActedYear ? W.Month <= p.ActedMonth : W.Year < p.ActedYear);
                foreach (var kv in _acts)
                {
                    bool allow = !dead && (!busy || kv.Key == "wait");
                    if (kv.Key == "@tab") allow = true;
                    if (kv.Key == "flow") allow = allow && p.NodeId >= 0;
                    if (kv.Key == "gate") allow = allow && W.Worlds.Exists(x => x.GateTo >= 0 && !x.Collapsed);
                    if (kv.Key == "plant") allow = allow && p.Stone >= 2;
                    if (kv.Key == "craft") allow = allow && p.Stone >= 3;
                    kv.Value.interactable = allow;
                }
            }

            var tickerText = _ticker.Find("tickerText")?.GetComponent<Text>();
            if (tickerText != null)
                tickerText.text = npc == null ? "世界已经从洪荒走了 " + W.Year + " 年。你还没有入场。"
                                              : (p.Dead ? "身殒。" + p.Verdict : Trim(p.LastEvent, 44));

            var sel = _app.SelectedNpc;
            _npcCard.gameObject.SetActive(sel != null && !p.Dead);
            if (sel != null)
            {
                _npcName.text = $"{sel.Name}　{Cultivation.RealmName(sel.Kind, sel.Realm)}（{sel.Realm} 境）";
                float imp = W.EffectiveImp(sel);
                _npcSub.text = $"{Grades.Of(sel.Grade)} · 恩 {sel.Favor} 怨 {sel.Grudge} · " +
                               $"对你的印象 {imp:0}（亲见 {sel.ImpDirect:0} / 传闻 {sel.ImpRumor:0}×{Mathf.Min(1.6f, 1f + 0.06f * sel.RumorHits):0.0}）";
            }

            if (_sheet.gameObject.activeSelf && _tabDirty) { _tabDirty = false; RebuildTab(); }
        }

        private static string Trim(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        public void Toast(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            _toastTxt.text = msg.Length > 46 ? msg.Substring(0, 46) + "…" : msg;
            _toastLife = 2.6f;
            _toastGroup.alpha = 1f;
        }

        // ------------------------------------------------------------------ 摊层
        public void ShowTab(string tab)
        {
            _tab = tab;
            _tabDirty = true;
            _sheet.gameObject.SetActive(true);
            _sheetTitle.text = tab;
            foreach (var kv in _tabs)
            {
                kv.Value.image.color = kv.Key == tab ? Tok.Hex("3A2C18") : Tok.PanelHi;
                var t = kv.Value.GetComponentInChildren<Text>();
                if (t != null) t.color = kv.Key == tab ? Tok.Gold : Tok.Sub;
            }
            RebuildTab();
        }

        private void ClearBody()
        {
            for (int i = _body.childCount - 1; i >= 0; i--) Destroy(_body.GetChild(i).gameObject);
        }

        private float _y;

        private void Row(string k, string v, Color? vc = null, float h = 54f)
        {
            var kt = U.Txt(_body, k, Tok.Body, Tok.Sub);
            Place(kt.rectTransform, 36, _y, 220, h);
            var vt = U.Txt(_body, v, Tok.Body, vc ?? Tok.Paper);
            Place(vt.rectTransform, 260, _y, 760, h);
            _y += h;
        }

        private void Note(string s, Color? c = null)
        {
            var t = U.Txt(_body, s, Tok.Small, c ?? Tok.Faint);
            Place(t.rectTransform, 36, _y, 980, 46);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            _y += 46;
        }

        private Button SheetBtn(string label, Color bg, Color fg, float x, float w, Action act)
        {
            var b = U.Btn(_body, label, bg, fg, Tok.Small, act, 14);
            Place(b.GetComponent<RectTransform>(), 36 + x, _y, w, 64);
            return b;
        }

        private void RebuildTab()
        {
            ClearBody();
            _y = 0f;
            var p = W.Player;
            var npc = W.PlayerNpc;

            switch (_tab)
            {
                case "身世":
                {
                    if (npc == null) { Note("尚未投生。"); break; }
                    Row("姓名", npc.Name);
                    Row("体系", npc.Kind == BeingKind.Human ? "人 · 九境循序" : npc.Kind == BeingKind.Beast ? "兽 · 开智化形" : "草木 · 抽芽结丹");
                    Row("境界", $"{Cultivation.RealmName(npc.Kind, npc.Realm)}（{npc.Realm} 境） 进度 {npc.Prog:P0}", Tok.Jade);
                    Row("品阶", $"{Grades.Of(npc.Grade)}" + (npc.GradeAscended ? "（此生已升过一次）" : "（可升一次）") + (npc.GradeDowngrades > 0 ? $" · 降 {npc.GradeDowngrades} 次" : ""), Tok.Gold);
                    Row("寿元", $"{(npc.Age / 12f):0.#} / {(npc.Lifespan / 12f):0.#} 岁");
                    Row("出身", $"{p.Origin} · {p.TalentText}");
                    Row("纪元", p.Era);
                    Row("因果", $"恩 {npc.Favor} · 怨 {npc.Grudge} · 业 {npc.Karma} · 名望 {p.Reputation:0}");
                    Row("行止", $"行善 {p.Help} · 拒人 {p.Refuse} · 夺灵 {p.Rob}");
                    Note("一世只能升一次品阶；升阶之后只能单向降；分解重炼是唯一的重置手段。");
                    SheetBtn("分解重炼", Tok.PanelHi, Tok.Cinnabar, 0, 240, () => { _app.DoAct("reforge"); ShowTab("身世"); });
                    break;
                }
                case "洞天":
                {
                    WorldNode node = p.NodeId >= 0 && p.NodeId < W.Worlds.Count ? W.Worlds[p.NodeId] : null;
                    if (node == null) foreach (var w in W.Worlds) if (w.OwnerId == (npc?.Id ?? -1) && !w.Collapsed) { node = w; break; }
                    if (node == null)
                    {
                        Note("你还没有自己的小世界。开辟一方世界要折寿，也要担阵基不稳的风险。");
                        SheetBtn("开辟", Tok.Gold, Tok.Ink, 0, 200, () => { _app.DoAct("open"); ShowTab("洞天"); });
                    }
                    else
                    {
                        Row("名称", node.Name);
                        Row("品阶", Grades.Of(node.Grade));
                        Row("面积", $"{node.AreaKm2:0.###} km²（{World.Tiers[node.Tier].Name}）");
                        Row("内时", $"{node.TimeScale:0}×（外界 1 月 = 内界 {node.TimeScale:0} 月）", Tok.Gold);
                        Row("不稳", $"{node.Instability:P0}" + (node.Instability > 0.7f ? " · 濒临崩解" : ""), node.Instability > 0.7f ? Tok.Cinnabar : Tok.Sub);
                        Row("维持", $"每月抽灵 {node.UpkeepQi:0.##} · 已折寿 {node.CostMonths / 12:0.#} 年");
                        Note("调快内时需要付寿元，且不稳会累积；到顶就崩成遗府。这是自定义时间流速的代价。");
                        _y += 6;
                        SheetBtn("增速 50%", Tok.PanelHi, Tok.Gold, 0, 220, () => { _app.DoAct("flow", Mathf.RoundToInt(node.TimeScale * 1.5f)); ShowTab("洞天"); });
                        SheetBtn("减速 50%", Tok.PanelHi, Tok.Sub, 240, 220, () => { _app.DoAct("flow", Mathf.Max(1, Mathf.RoundToInt(node.TimeScale * 0.5f))); ShowTab("洞天"); });
                        SheetBtn("渡门", Tok.PanelHi, Tok.Jade, 480, 180, () => { _app.DoAct("gate"); ShowTab("洞天"); });
                    }
                    break;
                }
                case "宗门":
                {
                    Sect sc = npc != null && npc.SectId >= 0 && npc.SectId < W.Sects.Count ? W.Sects[npc.SectId] : null;
                    if (sc == null) { Note("你不在任何宗门里。入宗要走到山门附近。"); SheetBtn("入宗", Tok.PanelHi, Tok.Jade, 0, 200, () => { _app.DoAct("sect"); ShowTab("宗门"); }); }
                    else
                    {
                        Row("宗门", sc.Name);
                        Row("品级", $"{sc.Tier} 等");
                        Row("抽成", $"{sc.Tithe:P0}", sc.Tithe > 0.4f ? Tok.Cinnabar : Tok.Paper);
                        Row("护持", $"{sc.Protection:P0}", sc.Protection < 0.4f ? Tok.Cinnabar : Tok.Jade);
                        Row("宗主", $"{sc.PatriarchRealm:0.0} 境 · 弟子 {sc.Disciples}");
                    }
                    Note($"五层庇护：国度 {W.Nations.FindAll(n => n.Alive).Count} · 家族 {W.Clans.FindAll(c => c.Alive).Count} · 宗门 {W.Sects.FindAll(s => s.Alive).Count}");
                    break;
                }
                case "世系":
                {
                    Note("时间单向不可逆。死即入下一世，没有回到过去任何一年的入口。", Tok.Cinnabar);
                    int from = Math.Max(0, W.Lives.Count - 8);
                    for (int i = W.Lives.Count - 1; i >= from; i--)
                    {
                        var r = W.Lives[i];
                        Row($"第 {r.Index} 世",
                            $"{r.Name} · {Cultivation.RealmName(r.Kind, r.Realm)} · {Grades.Of(r.Grade)} · Y{r.StartYear}–{r.EndYear}");
                        var t = U.Txt(_body, r.Verdict + "　来世为" + r.Fate, Tok.Caption, Tok.Gold);
                        Place(t.rectTransform, 260, _y - 6, 760, 34);
                    }
                    if (W.Lives.Count == 0) Note("还没有走完一世。");
                    break;
                }
                default:
                {
                    Row("版本", "0.7.0 · Unity 6 · 2.5D 低模");
                    Row("世界", $"种子 {W.Seed} · Y{W.Year} · 月 {W.Month + 1}");
                    Row("在册", $"{(int)W.TotalPopulation:N0} 人（含统计层凡人）");
                    Row("帧率", $"{(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)):0} FPS（目标 60）");
                    Note("移动端预算：无实时阴影、无实时全局光、单批绘制地形与人物；画面靠构图与配色，不靠光效。");
                    _y += 8;
                    SheetBtn("1×", Tok.PanelHi, Tok.Sub, 0, 150, () => _app.SetSpeed(1));
                    SheetBtn("2×", Tok.PanelHi, Tok.Sub, 166, 150, () => _app.SetSpeed(2));
                    SheetBtn("4×", Tok.PanelHi, Tok.Sub, 332, 150, () => _app.SetSpeed(4));
                    SheetBtn("回中原", Tok.PanelHi, Tok.Jade, 498, 220, () => _app.FocusOnPlayer(false));
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 判词
        public void ShowVerdict()
        {
            var p = W.Player;
            var npc = W.PlayerNpc;
            _verdict.gameObject.SetActive(true);
            _verdictTitle.text = "判 词";
            string name = npc?.Name ?? "无名者";
            _verdictBody.text = p.Verdict;
            _verdictFoot.text =
                $"{name} · {Cultivation.RealmName(npc?.Kind ?? BeingKind.Human, npc?.Realm ?? 0)} · {Grades.Of(npc?.Grade ?? Grade.Fan)}\n" +
                $"生 Y{p.Lives} 世 · 元年 {W.Year - Math.Max(0, p.Years) / 12}–{W.Year}\n" +
                $"来世为 {p.Fate}（因果定形：善者为人，恶者为草木禽兽）\n" +
                $"行善 {p.Help} · 拒人 {p.Refuse} · 夺灵 {p.Rob} · 名望 {p.Reputation:0}";
        }
    }
}