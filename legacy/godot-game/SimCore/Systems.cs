using System;
using System.Collections.Generic;

namespace SimCore;

public enum ItemKind { 功法, 法宝, 丹药, 符箓, 材料, 灵植 }

public sealed class Item
{
    public string Name;
    public ItemKind Kind;
    public int Qty;
    public int Grade;
    public Rank Rank = Rank.凡;       // 品阶：凡/灵/仙/神
    public bool RankLocked;           // 升过一次就锁；降阶也不解
    public bool RankEverAscended;     // 这一生升过没有（重炼才清）
    public Item(string name, ItemKind kind, int qty, int grade)
    { Name = name; Kind = kind; Qty = qty; Grade = grade; }
    public string Tag => Kind switch
    {
        ItemKind.功法 => "功法", ItemKind.法宝 => "法宝", ItemKind.丹药 => "丹药",
        ItemKind.符箓 => "符箓", ItemKind.材料 => "材料", _ => "灵植"
    };
}

public sealed partial class World
{
    public const int FieldFallow = 0, FieldTilled = 1, FieldSown = 2, FieldReady = 3;
    public int[] FieldState = new int[W * H];
    public float[] FieldGrow = new float[W * H];
    public string[] FieldCrop = new string[W * H];

    private void InitFields()
    {
        for (int i = 0; i < FieldState.Length; i++) { FieldState[i] = FieldFallow; FieldGrow[i] = 0f; FieldCrop[i] = ""; }
    }

    private int Idx(int x, int y) => x * H + y;

    public string CropName(int k) => k switch { 0 => "灵稻", 1 => "玉麦", 2 => "紫芝", _ => "雪参" };
    public float CropHours(int k) => k switch { 0 => 26f, 1 => 32f, 2 => 44f, _ => 60f };

    public string FieldText()
    {
        var pn = PlayerNpc; if (pn == null) return "无地";
        int i = Idx((int)pn.Gx, (int)pn.Gy);
        return FieldState[i] switch
        {
            FieldTilled => "已开垦（等你播种）",
            FieldSown => $"{FieldCrop[i]}·长成 {Math.Min(99f, FieldGrow[i] * 100f):0}%",
            FieldReady => $"{FieldCrop[i]}·可收",
            _ => "荒地（可开垦）"
        };
    }

    public string TillField()
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        int i = Idx((int)pn.Gx, (int)pn.Gy);
        if (FieldState[i] != FieldFallow) return "这块地已经在用了。";
        if (Tiles[(int)pn.Gx, (int)pn.Gy] == Terrain.Water) return "水里种不得。";
        FieldState[i] = FieldTilled; FieldGrow[i] = 0f;
        SpendHours(2f);
        return "开垦出三分灵田：土已翻，等你下种。";
    }

    public string SowField(int crop)
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        int i = Idx((int)pn.Gx, (int)pn.Gy);
        if (FieldState[i] != FieldTilled) return "先开垦。";
        if (Player.Stone < 1) return "没有灵石换种子。";
        Player.Stone -= 1;
        FieldState[i] = FieldSown; FieldGrow[i] = 0f; FieldCrop[i] = CropName(crop);
        SpendHours(1f);
        return $"播下{CropName(crop)}。一日之内可见苗，收成按地气定。";
    }

    public string HarvestField()
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        int i = Idx((int)pn.Gx, (int)pn.Gy);
        if (FieldState[i] != FieldReady) return "还没熟。";
        int x = (int)pn.Gx, y = (int)pn.Gy;
        float q = Mathf(Qi[x, y]);
        int qty = 1 + (int)(q * 2f) + (FieldCrop[i] == "紫芝" || FieldCrop[i] == "雪参" ? 1 : 0);
        Give(new Item(FieldCrop[i], ItemKind.灵植, qty, q > 0.8f ? 2 : 1));
        Player.Reputation += 0.4f;
        FieldState[i] = FieldTilled; FieldGrow[i] = 0f;
        SpendHours(2f);
        return $"收了 {FieldCrop[i]} ×{qty}，地气 {q:0.00}。";
    }

    private static float Mathf(float v) => v;

    public void GrowFields(float hours)
    {
        for (int i = 0; i < FieldState.Length; i++)
        {
            if (FieldState[i] != FieldSown) continue;
            int x = i / H, y = i % H;
            float k = 0.85f + Qi[x, y] * 0.5f;
            FieldGrow[i] += hours / Math.Max(20f, CropHoursByName(FieldCrop[i])) * k;
            if (FieldGrow[i] >= 1f) { FieldState[i] = FieldReady; FieldGrow[i] = 1f; }
        }
    }

    private float CropHoursByName(string n) => n switch
    { "灵稻" => 26f, "玉麦" => 32f, "紫芝" => 44f, "雪参" => 60f, _ => 30f };

    public void Give(Item it)
    {
        foreach (var b in Player.Bag)
            if (b.Name == it.Name && b.Kind == it.Kind)
            { b.Qty += it.Qty; if (it.Grade > b.Grade) b.Grade = it.Grade; return; }
        Player.Bag.Add(it);
        if (Player.Bag.Count > 24) Player.Bag.RemoveAt(0);
    }

    public int BagCount => Player.Bag.Count;

    /// <summary>修炼的分支：打坐之外的炼丹/制符/炼器/炼武技，都按时辰与灵石计。</summary>
    public string UseSkill(int kind)
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        string what = kind switch { 0 => "炼丹", 1 => "制符", 2 => "炼器", 3 => "炼武技", _ => "研功法" };
        int cost = kind switch { 0 => 2, 1 => 1, 2 => 3, 3 => 0, _ => 0 };
        if (Player.Stone < cost) return $"{what}要 {cost} 灵石，你没有。";
        Player.Stone -= cost;
        int realm = pn.Realm;
        double luck = _rng.NextDouble();
        SpendHours(kind == 2 ? 8f : kind == 0 ? 6f : 4f);
        if (luck < 0.12f + realm * 0.01f)
        {
            Player.LastEvent = $"{what}失败，料废了。";
            return Player.LastEvent;
        }
        switch (kind)
        {
            case 0: Give(new Item("凝气丹", ItemKind.丹药, 1 + (realm >= 4 ? 1 : 0), 1 + realm / 3)); break;
            case 1: Give(new Item("护身符", ItemKind.符箓, 1, 1 + realm / 4)); break;
            case 2: Give(new Item(realm >= 4 ? "飞剑" : "法器", ItemKind.法宝, 1, 1 + realm / 3)); break;
            case 3: pn.Prog += 0.12f * Math.Max(1f, pn.TimeScale); break;
            default: Give(new Item("残卷·古法", ItemKind.功法, 1, 1 + realm / 4)); break;
        }
        Player.LastEvent = $"{what}成，入行囊。";
        return Player.LastEvent;
    }

    /// <summary>关系网：师门/同门/友人/仇人，按恩怨与宗门归属推。</summary>
    public List<(int Id, string Name, string Kind, float Imp)> Relations()
    {
        var list = new List<(int, string, string, float)>();
        var pn = PlayerNpc;
        if (pn == null) return list;
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;
            if (Math.Abs(n.Gx - pn.Gx) > 8 && Math.Abs(n.Gy - pn.Gy) > 8 && n.Id != pn.Id) continue;
            string kind =
                n.Grudge >= 3 ? "仇人"
                : n.Grudge >= 1 && n.Favor <= 0 ? "有怨"
                : n.Favor >= 3 ? "友人"
                : pn.SectId >= 0 && n.SectId == pn.SectId ? (n.Realm > pn.Realm + 1 ? "师长" : "同门")
                : "同道";
            list.Add((n.Id, n.Name, kind, n.Imp));
        }
        list.Sort((a, b) => a.Item4.CompareTo(b.Item4));
        return list;
    }

    // -------------------------------------------------------------- 洞府/飞舟内景
    public List<string> Decor = new();          // 已摆放的装修（按格位顺序）
    public const int DecorSlots = 12;

    public string Decorate(int slot, string thing)
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        if (pn.CaveId < 0) return "你还没有洞府。先占府或开辟。";
        if (Decor.Count < DecorSlots)
            while (Decor.Count <= slot) Decor.Add("");
        if (slot >= 0 && slot < Decor.Count) Decor[slot] = thing;
        SpendHours(1f);
        return $"「{thing}」已安在洞府第 {slot + 1} 位。";
    }

    public string CaveName => PlayerNpc != null && PlayerNpc.CaveId >= 0 && PlayerNpc.CaveId < Caves.Count
        ? "洞府·" + Caves[PlayerNpc.CaveId].Id : "荒野露宿";

    // -------------------------------------------------------------- 飞行姿态
    /// <summary>0 悬停 1 平飞 2 俯冲；影响视距与移动耗时，不影响内核时间。</summary>
    public int FlightPose;
    public string FlightPoseName => FlightPose switch { 0 => "悬停", 1 => "平飞", _ => "俯冲" };
}
