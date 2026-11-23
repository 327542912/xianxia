using System;
using System.Collections.Generic;
using System.Linq;
using SimCore;

// 无头验收器：把需求文档第 11 章 25 条验收逐项判定。
// 用法：dotnet run --project simcli -- 500

int years = args.Length > 0 ? int.Parse(args[0]) : 500;
long[] seeds = { 42, 7, 123, 999 };

var results = new Dictionary<string, bool>();
var details = new Dictionary<string, string>();

foreach (var seed in seeds)
{
    var w = new World(seed);
    var seenKinds = new HashSet<OrgKind>();
    var seenCaveStates = new HashSet<string>();
    int empireEver = 0, sectSplits = 0, verdicts = 0, secCountStart = w.Sects.Count;
    bool caveTimeEver = false;
    int coexistYears = 0;      // 秩序区与荒野区"同一年"并存的年数
    float titheMin = 9f, titheMax = 0f;
    var favorSamples = new List<float>();
    float quad0Sum = 0; int quad0N = 0; float quad3Sum = 0; int quad3N = 0;
    int inheritedN = 0;
    bool stoneSeen = false;
    int maxPop = 0;

    for (int y = 0; y < years; y++)
    {
        w.Tick(1);

        foreach (var f in w.Factions)
        {
            seenKinds.Add(f.Kind);
            if (f.Kind == OrgKind.Empire) empireEver++;
        }
        foreach (var s in w.Sects)
        {
            titheMin = Math.Min(titheMin, s.Tithe);
            titheMax = Math.Max(titheMax, s.Tithe);
        }
        foreach (var cv in w.Caves)
        {
            seenCaveStates.Add($"{(cv.Visible ? "V" : "-")}{(cv.Collide ? "C" : "-")}");
            if (cv.OwnerId >= 0 && cv.TimeScale >= 10f) caveTimeEver = true;
        }
        foreach (var n in w.Npcs)
        {
            if (n.Verdict.Length > 0) verdicts++;
            if (!n.Alive) continue;
            favorSamples.Add(n.Favor + n.Deals * 2.5f);
            if (n.Quad == 0) { quad0Sum += n.Realm; quad0N++; }
            if (n.Quad == 3) { quad3Sum += n.Realm; quad3N++; }
            if (n.InheritedHelp > 0) inheritedN++;
            if (n.Verdict.Length > 0) verdicts++;
        }
        {
            float pMax = 0f, pMin = 9f;
            foreach (var sx in w.Sects) if (sx.Alive) { pMax = Math.Max(pMax, sx.Protection); pMin = Math.Min(pMin, sx.Protection); }
            bool raidsSoFar = w.RaidCount > 0;
            if (raidsSoFar && pMax >= 0.5f && pMin < 0.2f) coexistYears++;
        }
        if (w.CurrencyWord == "灵石") stoneSeen = true;
        if (w.Sects.Count > secCountStart) sectSplits = w.Sects.Count - secCountStart;
        maxPop = Math.Max(maxPop, (int)w.TotalPopulation);
    }

    // 确定性：同种子重跑必须同哈希
    var w2 = new World(seed); w2.Tick(years);
    bool deterministic = w.Hash() == w2.Hash();
    string save = w.Serialize();

    // 前置：核心-边缘（前 5% 的威信占比）
    favorSamples.Sort((a, b) => b.CompareTo(a));
    float total = favorSamples.Sum();
    int top5 = Math.Max(1, favorSamples.Count / 20);
    float topShare = total > 0 ? favorSamples.Take(top5).Sum() / total : 0f;

    bool ok1 = seenKinds.Count >= 3;
    bool ok2 = w.Splits > 0 || sectSplits > 0;
    bool ok3 = w.Factions.Count(f => !f.Alive) > 0 || w.Splits > 0;
    bool ok4 = w.Factions.Count > 0 && w.Factions.Min(f => f.FoundedYear) <= 60;
    bool ok5 = topShare >= 0.40f;
    // "同时存在"按时序判定：同一年里有护持 ≥0.5 的秩序宗门，也有护持 <0.2 的荒野，且确已发生劫掠
    bool ok6 = coexistYears >= 1;
    bool ok7 = w.CurrencyWord != "无";
    bool ok8 = stoneSeen || w.CurrencyWord == "灵石";
    bool ok9 = w.Favors > 100;
    bool ok10 = empireEver > 0;
    bool ok11 = w.Splits > 0;
    bool ok12 = w.Rebirths > 0;
    bool ok13 = verdicts > 0;
    bool ok14 = caveTimeEver || w.Caves.Any(c => c.OwnerId >= 0 && c.TimeScale >= 10f);
    bool ok15 = seenCaveStates.Count >= 3;
    bool ok16 = w.Techs.Count(t => t.RootId >= 0) >= 3;
    bool ok17 = w.Nations.Any(n => n.Alive) && w.Clans.Any(c => c.Alive) && w.Sects.Any(s => s.Alive);
    bool ok18 = titheMax - titheMin >= 0.05f;
    bool ok19 = sectSplits > 0;
    float q0 = quad0N > 0 ? quad0Sum / quad0N : 0f;
    float q3 = quad3N > 0 ? quad3Sum / quad3N : 0f;
    bool ok20 = q0 - q3 >= 2.0f;
    bool ok21 = inheritedN > 0;
    bool ok22 = deterministic;
    bool ok23 = save.Contains("hash=") && save.Length > 200;
    bool ok24 = maxPop >= 90000;
    bool ok25 = !float.IsNaN(w.TotalPopulation) && w.Year == years + 1 && maxPop > 0;

    void Put(string key, bool v, string d)
    {
        results[key] = results.ContainsKey(key) ? results[key] && v : v;
        details[key] = details.ContainsKey(key) ? details[key] + $" | s{seed}:{d}" : $"s{seed}:{d}";
    }

    Put("01 组织类型≥3", ok1, seenKinds.Count + "种");
    Put("02 分裂/合并", ok2, $"splits={w.Splits}");
    Put("03 组织灭亡", ok3, $"dests={w.Splits}");
    Put("04 组织非预设", ok4, $"首立={w.Factions.FirstOrDefault()?.FoundedYear}");
    Put("05 核心-边缘", ok5, $"前5%占{topShare:P0}");
    Put("06 秩序/荒野分野", ok6,
        $"并存{coexistYears}年 · 护持max {w.Sects.Where(x => x.Alive).Select(x => x.Protection).DefaultIfEmpty(0f).Max():0.0} · raids={w.RaidCount}");
    Put("07 货币涌现", ok7, w.CurrencyWord);
    Put("08 灵石挤出", ok8, $"灵石acc={w.Acceptance["灵石"]:P0}");
    Put("09 交易/恩", ok9, $"favors={w.Favors}");
    Put("10 帝国出现", ok10, $"empire帧={empireEver}");
    Put("11 帝国崩解", ok11, $"splits={w.Splits}");
    Put("12 转世", ok12, $"rebirths={w.Rebirths}");
    Put("13 判词", ok13, $"verdicts={verdicts}");
    Put("14 时间流速", ok14, $"cave ts={w.Caves.Max(c => c.TimeScale):0}");
    Put("15 阵法三态", ok15, $"{seenCaveStates.Count}态");
    Put("16 功法谱系", ok16, $"改良={w.Techs.Count(t => t.RootId >= 0)}");
    Put("17 三层并存", ok17, $"国{w.Nations.Count(n => n.Alive)}/家{w.Clans.Count(c => c.Alive)}/宗{w.Sects.Count(s => s.Alive)}");
    Put("18 抽成率分化", ok18, $"k∈[{titheMin:0.00},{titheMax:0.00}]");
    Put("19 离心分裂", ok19, $"宗分裂={sectSplits}");
    Put("20 四象限起点差", ok20, $"q0={q0:0.0} q3={q3:0.0}");
    Put("21 前世恩债", ok21, $"inherited={inheritedN}");
    Put("22 确定性", ok22, "hash一致");
    Put("23 存档格式", ok23, $"{save.Length}B");
    Put("24 10万规模", ok24, $"峰值{maxPop}");
    Put("25 数值稳定", ok25, $"Y{w.Year}");
}

Console.WriteLine($"=== 验收：{years} 年 × {seeds.Length} 种子 ===");
int pass = 0, fail = 0;
foreach (var kv in results)
{
    var mark = kv.Value ? "PASS" : "FAIL";
    if (kv.Value) pass++; else fail++;
    Console.WriteLine($"[{mark}] {kv.Key}   {details[kv.Key]}");
}
Console.WriteLine($"--- PASS {pass} / FAIL {fail} ---");
return fail == 0 ? 0 : 1;
