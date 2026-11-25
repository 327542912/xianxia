using System;
using System.Collections.Generic;
using Godot;
using SimCore;

namespace Xianxia;

/// <summary>二级/三级面板：行囊、修炼、关系、天下、洞府内景、田园。主按钮点开，面板内再分支。</summary>
public partial class Main
{
    private int _panel;          // 0 关 1 修炼 2 行囊 3 关系 4 天下 5 洞府 6 田园
    private int _bagTab;         // 行囊分类
    private int _cropKind;       // 待播作物

    private void OpenPanel(int id) { _panel = id; QueueRedraw(); }

    private void DrawPanels(Vector2 vp, float u)
    {
        if (_panel == 0) return;
        var card = new Rect2(vp.X * 0.020f, vp.Y * 0.330f, vp.X * 0.96f, vp.Y * 0.395f);
        RR(card, 14f * u, new Color(0.045f, 0.075f, 0.065f, 0.985f), UI_Gold with { A = 0.30f }, 1.2f);
        string title = _panel switch
        {
            1 => "修炼 · 一途分六门", 2 => "行囊 · 空间", 3 => "关系 · 人情网",
            4 => "天下 · 组织与市井", 5 => "洞府内景 · 十二位", _ => "田园 · 一亩三分"
        };
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 28f * u), title,
                   HorizontalAlignment.Left, -1, (int)(16f * u), UI_Gold);
        var close = new Rect2(card.End.X - 16f * u - 46f * u, card.Position.Y + 10f * u, 42f * u, 24f * u);
        Pill(close, "合上", u, false, false, false);
        _btns.Add((close, "合上", "pclose", 0));

        switch (_panel)
        {
            case 1: PanelCultivate(card, u); break;
            case 2: PanelBag(card, u); break;
            case 3: PanelRelations(card, u); break;
            case 4: PanelWorld(card, u); break;
            case 5: PanelCave(card, u); break;
            case 6: PanelField(card, u); break;
        }
    }

    private void PanelCultivate(Rect2 card, float u)
    {
        var items = new[] { ("打坐", "meditate"), ("炼丹", "skill0"), ("制符", "skill1"), ("炼器", "skill2"), ("炼武技", "skill3"), ("研功法", "skill4") };
        float bw = (card.Size.X - 32f * u - 5f * 6f * u) / 3f;
        float bh = 40f * u;
        for (int i = 0; i < items.Length; i++)
        {
            int r = i / 3, c = i % 3;
            var rr = new Rect2(card.Position.X + 16f * u + c * (bw + 6f * u), card.Position.Y + 52f * u + r * (bh + 8f * u), bw, bh);
            Pill(rr, items[i].Item1, u, false, i == 0, false);
            _btns.Add((rr, items[i].Item1, items[i].Item2, 0));
        }
        var pn = Ply;
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.End.Y - 54f * u),
                   $"当前：{pn?.Realm ?? 0}境　修为 {(pn?.Prog ?? 0f):0.00}　灵石 {_w.Player.Stone}　洞府内时 {(pn?.TimeScale ?? 1f):0}×",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(11f * u), UI_Sub);
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.End.Y - 34f * u),
                   "炼丹 2石/6时辰　制符 1石/4时辰　炼器 3石/8时辰　炼武技 4时辰",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(10f * u), UI_Sub with { A = 0.8f });
    }

    private void PanelBag(Rect2 card, float u)
    {
        var kinds = new[] { "全部", "功法", "法宝", "丹药", "符箓", "材料", "灵植" };
        float tabW = (card.Size.X - 32f * u - 6f * 6f * u) / 7f;
        for (int i = 0; i < kinds.Length; i++)
        {
            var rr = new Rect2(card.Position.X + 16f * u + i * (tabW + 6f * u), card.Position.Y + 44f * u, tabW, 26f * u);
            Pill(rr, kinds[i], u, _bagTab == i, false, false);
            _btns.Add((rr, kinds[i], "bagtab", i));
        }
        var list = new List<Item>();
        foreach (var it in _w.Player.Bag)
            if (_bagTab == 0 || it.Tag == kinds[_bagTab]) list.Add(it);
        float ly = card.Position.Y + 84f * u;
        DrawString(_font, new Vector2(card.Position.X + 16f * u, ly),
                   list.Count == 0 ? "行囊空空。去炼丹、制符、开荒，东西自然进来。" : $"共 {list.Count} 类　行囊 {_w.BagCount}/24",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(11f * u), UI_Sub);
        for (int i = 0; i < list.Count && i < 7; i++)
        {
            var it = list[i];
            var row = new Rect2(card.Position.X + 16f * u, ly + (i + 1) * 27f * u, card.Size.X - 32f * u, 24f * u);
            RR(row, 7f * u, new Color(1f, 1f, 1f, 0.045f), Colors.Transparent, 0f);
            DrawString(_font, new Vector2(row.Position.X + 8f * u, row.Position.Y + 17f * u),
                       $"{it.Tag}　{it.Name} ×{it.Qty}", HorizontalAlignment.Left, (int)(row.Size.X * 0.6f), (int)(11.5f * u), UI_Text);
            DrawString(_font, new Vector2(row.Position.X + row.Size.X * 0.62f, row.Position.Y + 17f * u),
                       (it.Kind is ItemKind.法宝 or ItemKind.功法)
                           ? $"{Grades.Text(it.Rank)}{(it.RankLocked ? "·已锁" : "")}"
                           : $"品阶 {it.Grade}",
                       HorizontalAlignment.Left, -1, (int)(10f * u),
                       (it.Kind is ItemKind.法宝 or ItemKind.功法) && (int)it.Rank >= 2 ? UI_Gold : UI_Sub);
        }
    }

    private void PanelRelations(Rect2 card, float u)
    {
        var rel = _w.Relations();
        float ly = card.Position.Y + 54f * u;
        DrawString(_font, new Vector2(card.Position.X + 16f * u, ly),
                   rel.Count == 0 ? "你还没有可托付的人。" : $"左近 {rel.Count} 人：师长/同门/友人/仇人，点一下可当面说话",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(11f * u), UI_Sub);
        for (int i = 0; i < rel.Count && i < 8; i++)
        {
            var r0 = rel[i];
            var row = new Rect2(card.Position.X + 16f * u, ly + (i + 1) * 26f * u, card.Size.X - 32f * u, 23f * u);
            RR(row, 7f * u, new Color(1f, 1f, 1f, 0.045f), Colors.Transparent, 0f);
            var kindCol = r0.Item3 == "仇人" ? new Color(0.92f, 0.52f, 0.40f)
                         : r0.Item3 is "师长" or "同门" ? UI_Jade : UI_Text;
            DrawString(_font, new Vector2(row.Position.X + 8f * u, row.Position.Y + 16f * u),
                       $"{r0.Item3}　{r0.Item2}", HorizontalAlignment.Left, (int)(row.Size.X * 0.6f), (int)(11f * u), kindCol);
            DrawString(_font, new Vector2(row.Position.X + row.Size.X * 0.62f, row.Position.Y + 16f * u),
                       $"印象 {r0.Item4:0}", HorizontalAlignment.Left, -1, (int)(10f * u), UI_Sub);
            var tapRow = row;
            int id = r0.Item1;
            _btns.Add((tapRow, "", "rtalk", id));
        }
    }

    private void PanelWorld(Rect2 card, float u)
    {
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 50f * u),
                   $"天时 {_w.Weather}　收成×{_w.HarvestMul:0.00}　　市价（石/份）：灵植 {_w.PriceOf("灵植"):0.0}　丹药 {_w.PriceOf("丹药"):0.0}　法宝 {_w.PriceOf("法宝"):0.0}",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(10.5f * u), UI_Jade);

        var rows = new[]
        {
            ("国", $"在册组织 {_w.Factions.Count} 个，抽成 {_w.TitheAvg * 100f:0}%"),
            ("宗门", $"进山门（{_w.Factions.Count} 处）：拜师·领差事·抽成 {_w.TitheAvg * 100f:0}%"),
            ("家族", $"进宅院（{_w.Clans.Count} 家）：嫡系分产业，肯回来才有份"),
            ("市场", $"进坊市（灵植 {_w.PriceOf("灵植"):0.0} 石）：买·卖·摆摊"),
            ("钱庄", $"进钱庄（账上 {_w.Bank} 石）：存·取·查账"),
            ("摆摊", "把自种的灵植、自炼的丹药摆出去（比批发多 5%）"),
            ("飞舟", $"登舟（舱纹「{_w.BoatStyle}」）：换一处市场，价就不同"),
            ("行囊", $"灵石 {_w.Player.Stone} · 货物 {_w.BagCount} 类 · 成交 {_w.Traded} 笔")
        };
        float ly = card.Position.Y + 64f * u, rh = 25f * u;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new Rect2(card.Position.X + 16f * u, ly + i * rh, card.Size.X - 32f * u, rh - 3f * u);
            bool action = i is >= 1 and <= 6;
            RR(row, 6f * u, new Color(1f, 1f, 1f, action ? 0.075f : 0.035f), Colors.Transparent, 0f);
            DrawString(_font, new Vector2(row.Position.X + 8f * u, row.Position.Y + 16f * u),
                       rows[i].Item1 + "　" + rows[i].Item2, HorizontalAlignment.Left,
                       (int)(row.Size.X - 16f * u), (int)(10.5f * u), action ? UI_Text : UI_Sub);
            if (action) _btns.Add((row, rows[i].Item1, "world_" + i, 0));
        }
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.End.Y - 12f * u),
                   "点「市场/钱庄/摆摊」真的成交：价由供给与需求算，天灾会抬价",
                   HorizontalAlignment.Left, -1, (int)(9f * u), UI_Sub with { A = 0.75f });
    }

    private void PanelCave(Rect2 card, float u)
    {
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 54f * u),
                   $"{_w.CaveName}　十二位可安物：蒲团·书架·药炉·灵泉·屏风·剑架",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(11f * u), UI_Sub);
        float gw = (card.Size.X - 32f * u - 3f * 8f * u) / 4f;
        float gh = 46f * u;
        var things = new[] { "蒲团", "书架", "药炉", "灵泉", "屏风", "剑架" };
        for (int i = 0; i < 12; i++)
        {
            int r = i / 4, c = i % 4;
            var rr = new Rect2(card.Position.X + 16f * u + c * (gw + 8f * u), card.Position.Y + 76f * u + r * (gh + 8f * u), gw, gh);
            string cur = _w.Decor.Count > i ? _w.Decor[i] : "";
            DrawColoredPolygon(new[] { rr.Position, new Vector2(rr.End.X, rr.Position.Y), rr.End, new Vector2(rr.Position.X, rr.End.Y) },
                               new Color(1f, 1f, 1f, 0.05f));
            DrawPolyline(new[] { rr.Position, new Vector2(rr.End.X, rr.Position.Y), rr.End, new Vector2(rr.Position.X, rr.End.Y), rr.Position },
                         UI_Line with { A = 0.55f }, 1f);
            DrawString(_font, new Vector2(rr.Position.X + 6f * u, rr.GetCenter().Y + 4f * u), cur.Length > 0 ? cur : "空位",
                       HorizontalAlignment.Left, -1, (int)(10.5f * u), cur.Length > 0 ? UI_Jade : UI_Sub);
            _btns.Add((rr, "", "decor", i));
        }
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.End.Y - 14f * u),
                   "点空格换一样东西（未占府先去「占府」或「开辟」）", HorizontalAlignment.Left, -1, (int)(9.5f * u), UI_Sub with { A = 0.75f });
    }

    private void PanelField(Rect2 card, float u)
    {
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.Position.Y + 54f * u),
                   $"脚下这块地：{_w.FieldText()}", HorizontalAlignment.Left, -1, (int)(12f * u), UI_Jade);
        var acts = new[] { ("开垦", "till"), ("收获", "harvest") };
        float bw = card.Size.X * 0.34f;
        for (int i = 0; i < acts.Length; i++)
        {
            var rr = new Rect2(card.Position.X + 16f * u + i * (bw + 10f * u), card.Position.Y + 74f * u, bw, 40f * u);
            Pill(rr, acts[i].Item1, u, false, i == 0, false);
            _btns.Add((rr, acts[i].Item1, acts[i].Item2, 0));
        }
        var crops = new[] { "灵稻", "玉麦", "紫芝", "雪参" };
        float cw = (card.Size.X - 32f * u - 3f * 7f * u) / 4f;
        for (int i = 0; i < crops.Length; i++)
        {
            var rr = new Rect2(card.Position.X + 16f * u + i * (cw + 7f * u), card.Position.Y + 122f * u, cw, 34f * u);
            Pill(rr, "播" + crops[i], u, _cropKind == i, false, false);
            _btns.Add((rr, crops[i], "pickcrop", i));
        }
        DrawString(_font, new Vector2(card.Position.X + 16f * u, card.End.Y - 24f * u),
                   "开垦 2时辰　播种 1石/1时辰　收获按地气给量；地越灵气越足，收得越多",
                   HorizontalAlignment.Left, (int)(card.Size.X - 32f * u), (int)(10f * u), UI_Sub);
    }
}
