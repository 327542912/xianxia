using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>
/// 经济底层：稀缺定价 + 天灾减产 + 钱庄。
/// 规则一句话：货从自然界来（植被/灵气），破坏与天灾让货变少，货少则价高，
/// 价高才有人去种、去开山——这就是"优胜劣汰"的算账方式，不是文案。
/// </summary>
public sealed partial class World
{
    public string Weather = "晴";
    public int WeatherLeft;
    public int DroughtYears, FloodYears, WarYears;
    public float HarvestMul = 1f;       // 当年收成系数（灾害会压低）
    public long Bank;                   // 存放在钱庄的灵石
    public int Traded, Stalls;          // 成交笔数 / 摆摊次数

    public static readonly string[] Goods = { "灵植", "丹药", "符箓", "法宝", "功法", "材料" };

    /// <summary>供给：全世界的植被存量 + 已收成的货；被破坏得越狠，供给越薄。</summary>
    public float SupplyOf(string kind)
    {
        float veg = 0f;
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) veg += Veg[x, y];
        float supply = kind switch
        {
            "灵植" => veg * (1f - 0.5f * EnvDestroyed / (float)Math.Max(1, W * H)),
            "丹药" => veg * 0.22f + Player.Bag.Count,
            "符箓" => veg * 0.10f,
            "法宝" => 0.6f + TotalQi * 2f,
            "功法" => 1f + Techs.Count * 0.5f,
            _ => TotalQi * 3f
        };
        return Math.Max(0.2f, supply);
    }

    /// <summary>需求：人口越多、越富，吃用的越多。</summary>
    public float DemandOf(string kind)
    {
        float pop = Math.Max(1f, AliveCount + (float)Math.Floor(TotalPopulation / 2000f));
        return kind switch
        {
            "灵植" => pop * 1.4f,
            "丹药" => pop * 0.9f,
            "符箓" => pop * 0.4f,
            "法宝" => pop * 0.25f,
            "功法" => pop * 0.18f,
            _ => pop * 0.5f
        };
    }

    /// <summary>价格 = 基价 × (需求/供给)^弹性，夹在 0.25~12 之间（不会飞天也不会白送）。</summary>
    public float PriceOf(string kind)
    {
        int baseV = kind switch { "灵植" => 2, "丹药" => 6, "符箓" => 4, "法宝" => 18, "功法" => 12, _ => 1 };
        float ratio = DemandOf(kind) / SupplyOf(kind);
        float p = baseV * MathF.Pow(ratio, 0.55f) / HarvestMul;
        return Math.Clamp(p, baseV * 0.25f, baseV * 10f);
    }

    /// <summary>每年一次天气/灾变。灾害不是随机装饰：它真的压低收成、推高价格。</summary>
    public void StepWeather()
    {
        if (WeatherLeft > 0) { WeatherLeft--; return; }
        double r = _rng.NextDouble();
        if (r < 0.10) { Weather = "旱"; HarvestMul = 0.55f; DroughtYears++; WeatherLeft = 2; }
        else if (r < 0.18) { Weather = "涝"; HarvestMul = 0.65f; FloodYears++; WeatherLeft = 2; }
        else if (r < 0.24) { Weather = "战乱"; HarvestMul = 0.45f; WarYears++; WeatherLeft = 3; EnvDestroyed += 6; WarRuin(24); }
        else if (r < 0.34) { Weather = "丰年"; HarvestMul = 1.25f; WeatherLeft = 1; }
        else { int w = (int)(_rng.NextDouble() * 3); Weather = w == 0 ? "晴" : w == 1 ? "阴雨" : "风"; HarvestMul = 1f; }
        AddLog($"Y{Year} 天时：{Weather}　收成×{HarvestMul:0.00}　灵植价 {PriceOf("灵植"):0.0} 石");
    }

    public string BuyGood(string kind, int qty)
    {
        float unit = PriceOf(kind);
        int cost = (int)MathF.Ceiling(unit * qty);
        if (Player.Stone < cost) return $"价 {unit:0.0} 石/份，{qty} 份要 {cost} 石，你只有 {Player.Stone}。";
        Player.Stone -= cost; Traded++;
        Give(new Item(kind == "灵植" ? "灵稻" : kind == "丹药" ? "凝气丹" : kind, KindOf(kind), qty, 1));
        SpendHours(1f);
        return $"市上买 {kind} ×{qty}，付 {cost} 石（价随供需走：{unit:0.0}/份）。";
    }

    public string SellGood(string kind, int qty)
    {
        Item hold = null;
        foreach (var it in Player.Bag) if (it.Tag == kind) { hold = it; break; }
        if (hold == null) return $"行囊里没有{kind}。";
        int n = Math.Min(qty, hold.Qty);
        float unit = PriceOf(kind) * 0.8f;      // 卖价压两成：商帮要抽头
        int gain = Math.Max(1, (int)MathF.Floor(unit * n));
        hold.Qty -= n; if (hold.Qty <= 0) Player.Bag.Remove(hold);
        Player.Stone += gain; Traded++;
        SpendHours(1f);
        return $"出手 {kind} ×{n}，得 {gain} 石（收购价 {unit:0.0}/份）。";
    }

    private ItemKind KindOf(string kind) => kind switch
    {
        "灵植" => ItemKind.灵植, "丹药" => ItemKind.丹药, "符箓" => ItemKind.符箓,
        "法宝" => ItemKind.法宝, "功法" => ItemKind.功法, _ => ItemKind.材料
    };

    public string Deposit(int stone)
    {
        if (Player.Stone < stone) return "身上没这么多石。";
        Player.Stone -= stone; Bank += stone; SpendHours(1f);
        return $"存入 {stone} 石，钱庄账上 {Bank}。";
    }

    public string Withdraw(int stone)
    {
        if (Bank < stone) return $"钱庄只存着 {Bank} 石。";
        Bank -= stone; Player.Stone += stone; SpendHours(1f);
        return $"取出 {stone} 石，身上 {Player.Stone} 石。";
    }

    public string Stall(int qty)
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        Item best = null;
        foreach (var it in Player.Bag) if (it.Kind == ItemKind.灵植 || it.Kind == ItemKind.丹药) { best = it; break; }
        if (best == null) return "没有可摆的货：先种、先炼。";
        int n = Math.Min(qty, best.Qty);
        int gain = Math.Max(1, (int)MathF.Floor(PriceOf(best.Tag) * 1.05f * n));   // 自摆摊比卖给商帮多 5%
        best.Qty -= n; if (best.Qty <= 0) Player.Bag.Remove(best);
        Player.Stone += gain; Stalls++; Traded++;
        Player.Reputation += 0.5f;
        SpendHours(2f);
        return $"在({(int)pn.Gx},{(int)pn.Gy})摆摊卖 {best.Name} ×{n}，得 {gain} 石。";
    }
}
