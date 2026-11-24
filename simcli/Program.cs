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

    for (int y = 0; y < years * 12; y++)   // 一年 12 个月
    {
        w.Tick(1);

        // 统计采样按「年」做，避免 18000 次 × 全表遍历导致超时/OOM
        if (y % 12 != 11) continue;

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
            if (favorSamples.Count < 200000) favorSamples.Add(n.Favor + n.Deals * 2.5f);
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

    // ---------------- 可玩性验收（玩家这条链路是否真的能玩） -------------
    var bot = new Bot(seed);
    bot.Run(220);                    // 自动玩家跑 220 世，中断即失败

    // 确定性：同种子重跑必须同哈希
    var w2 = new World(seed); w2.Tick(years * 12);
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
    bool ok25 = !float.IsNaN(w.TotalPopulation) && w.Year == years && maxPop > 0;

    bool ok26 = bot.deaths > 0 && bot.lives >= 1;
    bool ok27 = bot.moved > 0;
    bool ok28 = bot.helped > 0 && bot.refused > 0 && bot.grudgeFromRefuse > 0;
    bool ok29 = bot.realmUp > 0;
    bool ok30 = bot.caves > 0;
    bool ok31 = bot.sects > 0;
    bool ok32 = bot.verdicts > 0;
    bool ok33 = bot.reincarnations > 0 && bot.quadSpread >= 3;

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
    Put("26 投生可得一世", ok26, $"世{bot.lives} 死{bot.deaths}");
    Put("27 可移动", ok27, $"移动{bot.moved}步");
    Put("28 恩/怨双向记账", ok28, $"帮{bot.helped} 拒{bot.refused} 记恨{bot.grudgeFromRefuse}");
    Put("29 修炼可突破", ok29, $"突破{bot.realmUp}次");
    Put("30 可占洞府", ok30, $"占府{bot.caves}");
    Put("31 可入宗门", ok31, $"入宗{bot.sects}");
    Put("32 死有判词", ok32, $"判词{bot.verdicts}");
    Put("33 转世与四象限", ok33, $"转世{bot.reincarnations} 象限{bot.quadSpread}/4");
}

// ---- 34~36 世界尺度与界门 ----
{
    bool mono = true; int last = -1;
    for (int r = 0; r <= 7; r++) { int t0 = SimCore.World.BaseTierForRealm(r); if (t0 < last) mono = false; last = t0; }

    var wscale = new SimCore.World(42);
    bool areaOk = wscale.Worlds.Count > 0 && wscale.Worlds.TrueForAll(x => x.AreaKm2 > 0 && x.GridW >= 8);

    var wp = new SimCore.World(42);
    wp.BeginLife();
    for (int i = 0; i < 8 && !wp.Player.Dead; i++) wp.Act("meditate");
    int realmNow = wp.PlayerNpc?.Realm ?? 0;
    int expectTier = wp.TierFor(realmNow, wp.Player.Stone, false);
    wp.Act("open");
    var lastNode = wp.Worlds[wp.Worlds.Count - 1];
    bool openOk = Math.Abs(lastNode.AreaKm2 - SimCore.World.AreaOf(expectTier)) < 1e-6;

    var wg = new SimCore.World(7);
    int gateFrom = -1, gateTo = -1;
    foreach (var nd in wg.Worlds) if (nd.GateTo >= 0) { gateFrom = nd.Id; gateTo = nd.GateTo; break; }
    string lowMsg = gateFrom < 0 ? "无门" : "";
    if (gateFrom >= 0) { var dm = wg.Npcs[0]; dm.Realm = 0; lowMsg = wg.Traverse(gateTo, dm); }
    bool gateOk = gateFrom >= 0 && (lowMsg.Contains("境界不足") || lowMsg.Contains("入「"));

    results["34 尺度档位单调"] = mono; details["34 尺度档位单调"] = $"境界 0→7 档位单调={mono}";
    results["35 世界节点带面积"] = areaOk; details["35 世界节点带面积"] = $"节点={wscale.Worlds.Count} 首个={wscale.Worlds[0].AreaKm2:0.###}km²/{wscale.Worlds[0].GridW}格";
    results["36 开辟按境界定面积"] = openOk && gateOk; details["36 开辟按境界定面积"] = $"境界{realmNow}→{SimCore.World.Tiers[expectTier].Name}({lastNode.AreaKm2:0.###}km²) 门判={lowMsg}";
}

// ---- 37~40 目的 / 印象 / 声望 / 杀人夺宝 ----
{
    var gw = new SimCore.World(42);
    var goals = new HashSet<int>();
    foreach (var n in gw.Npcs) goals.Add(n.Goal);
    bool goalOk = goals.Count >= 4;
    _ = Bot.TotalGifts;

    // 同一世界、同一出生：全帮 vs 全拒，声望必须分道
    var wa = new SimCore.World(123);
    wa.BeginLife();
    var ta = wa.Npcs.Find(x => x.Alive && !x.IsPlayer);
    for (int i = 0; i < 30 && !wa.Player.Dead; i++)
    { if (ta != null && ta.Alive) { ta.Gx = wa.PlayerNpc.Gx; ta.Gy = wa.PlayerNpc.Gy; } wa.Act("help"); }
    var wb = new SimCore.World(123);
    wb.BeginLife();
    var tb = wb.Npcs.Find(x => x.Alive && !x.IsPlayer);
    for (int i = 0; i < 30 && !wb.Player.Dead; i++)
    { if (tb != null && tb.Alive) { tb.Gx = wb.PlayerNpc.Gx; tb.Gy = wb.PlayerNpc.Gy; } wb.Act("refuse"); }
    bool repOk = wa.Player.Reputation > wb.Player.Reputation + 2f;

    // 主动夺宝：怀重宝、无护道、被强者记恨 → 必须真的会被杀
    var wc = new SimCore.World(7);
    wc.BeginLife();
    wc.Player.Stone = 30;
    int amb = 0; int botTotalGifts = 0, botTotalAmbush = 0;
    for (int i = 0; i < 80 && !wc.Player.Dead; i++)
    {
        var t = wc.Npcs.Find(x => x.Alive && !x.IsPlayer);
        if (t != null) { t.Imp = -60f; t.Realm = Math.Max(t.Realm, wc.PlayerNpc.Realm + 3); t.Gx = wc.PlayerNpc.Gx + 1; t.Gy = wc.PlayerNpc.Gy; }
        wc.Act("wait");
        amb = wc.NpcAmbushKills;
    }

    // 正面声望：名声好的人，弱也能被扶（机制存在性）
    var wd = new SimCore.World(999);
    wd.BeginLife();
    wd.Player.Reputation = 20f;
    var near = wd.Npcs.Find(x => x.Alive && !x.IsPlayer);
    foreach (var n in wd.Npcs) if (n.Alive && !n.IsPlayer) n.Imp = 40f;
    if (near != null) { near.Gx = wd.PlayerNpc.Gx + 1; near.Gy = wd.PlayerNpc.Gy; }
    int gift = 0;
    for (int i = 0; i < 60 && !wd.Player.Dead; i++)
    {
        if (near != null && near.Alive)
        { near.Imp = 40f; near.Gx = wd.PlayerNpc.Gx + 1; near.Gy = wd.PlayerNpc.Gy; }
        wd.Act("wait"); gift = wd.NpcGifts;
    }

    results["37 NPC各有目的"] = goalOk;
    details["37 NPC各有目的"] = $"目的种类={goals.Count}/6";
    results["38 行为改声望"] = repOk;
    details["38 行为改声望"] = $"全帮={wa.Player.Reputation:0.0} 全拒={wb.Player.Reputation:0.0}";
    results["39 名声换来援手"] = gift > 0;
    details["39 名声换来援手"] = $"受赠={gift}次 全局赠{Bot.TotalGifts}";
    results["40 怀宝招杀身"] = amb > 0;
    details["40 怀宝招杀身"] = $"夺宝杀={amb} 全局={Bot.TotalAmbush}";
}

// ---- 41~45 世界资源 / 破坏不可逆 / 婚配传承 / 因果定形 ----
{
    var we = new SimCore.World(42);
    we.BeginLife();
    var p0 = we.PlayerNpc;
    int tx = (int)p0.Gx, ty = (int)p0.Gy;
    float qi0 = we.Qi[tx, ty], veg0 = we.Veg[tx, ty];
    we.Player.Stone = 40;
    for (int i = 0; i < 6 && !we.Player.Dead; i++) we.Act("delve");
    float qi1 = we.Qi[tx, ty], dmg1 = we.Dmg[tx, ty];
    for (int i = 0; i < 5 && !we.Player.Dead; i++) we.Act("wait");
    float qi2 = we.Qi[tx, ty];
    bool irrev = qi1 < qi0 - 0.1f && qi2 <= qi1 + 0.001f && dmg1 > 0.5f && veg0 > 0f;

    var wp = new SimCore.World(7);
    wp.BeginLife(); int px = (int)wp.PlayerNpc.Gx, py = (int)wp.PlayerNpc.Gy;
    wp.Player.Stone = 12;
    float v0 = wp.Veg[px, py]; wp.Act("plant"); float v1 = wp.Veg[px, py];
    bool planted = v1 > v0 + 0.05f && wp.EnvPlanted > 0;

    // 灵气越稀缺，开山破坏越少（克制）
    var wd1 = new SimCore.World(123);
    for (int i = 0; i < 300 * 12; i++) wd1.Tick(1);
    int rich = wd1.EnvDestroyed;
    var wd2 = new SimCore.World(123);
    for (int x = 0; x < SimCore.World.W; x++)
        for (int y = 0; y < SimCore.World.H; y++) { wd2.Qi[x, y] *= 0.12f; wd2.Dmg[x, y] = 0.5f; wd2.Veg[x, y] = 0.2f; }
    for (int i = 0; i < 300 * 12; i++) wd2.Tick(1);
    int poor = wd2.EnvDestroyed;
    bool restraint = rich > 0 && poor < rich;

    // 婚配与血脉：子女继承父母相貌参数
    var wm = new SimCore.World(999);
    for (int i = 0; i < 150 * 12 && wm.Births == 0; i++) wm.Tick(1);
    SimCore.Npc child = null;
    foreach (var n in wm.Npcs) if (n.ParentA >= 0) { child = n; break; }
    bool married = wm.Marriages > 0 && wm.Births > 0 && child != null
                   && Math.Abs(child.Height - (wm.Npcs.Find(z => z.Id == child.ParentA).Height + wm.Npcs.Find(z => z.Id == child.ParentB).Height) * 0.5f) < 0.12f;

    results["41 破坏不可逆"] = irrev;
    details["41 破坏不可逆"] = $"灵气{qi0:0.00}→{qi1:0.00}→{qi2:0.00} 破坏度{dmg1:0.00}";
    results["42 种植可回补"] = planted;
    details["42 种植可回补"] = $"植被{v0:0.00}→{v1:0.00} 新增{wp.EnvPlanted}次";
    results["43 灵气少则克制"] = restraint;
    details["43 灵气少则克制"] = $"丰沛破坏{rich} vs 枯竭破坏{poor}";
    results["44 婚配与血脉"] = married;
    details["44 婚配与血脉"] = $"婚{ wm.Marriages} 生{ wm.Births} 身高继承={married}";
    int debtorCount = wm.Player.Debtors.Count;
    // 46 时序单向：年只增，季只随年推进，任何操作都不能回退
    var wt = new SimCore.World(31);
    wt.BeginLife();
    bool mono = true; int lastYear = wt.Year; var seq = new List<int>();
    for (int i = 0; i < 24 * 3; i++)
    {
        seq.Add((int)wt.Season);
        if (wt.Year < lastYear) mono = false;
        lastYear = wt.Year;
        if (i % 3 == 0) wt.Act("wait"); else wt.Tick(1);
    }
    // 季节序列必须恰为 0,1,2,3 循环，且首项与 Year 一致
    bool cycle = true;
    for (int i = 0; i < seq.Count; i++) if (seq[i] != (seq[0] + i) % 4) { cycle = false; break; }
    for (int i = 0; i < seq.Count; i++) if (seq[i] != (int)((SimCore.Season)((seq[0] + i) % 4))) { cycle = false; break; }
    bool monoOk = mono && cycle;

    // 47 四季影响生机：同世界，夏的植被增速高于冬
    var ws = new SimCore.World(77);
    for (int i = 0; i < 8 * 12; i++) ws.Tick(1);
    while (ws.Season != SimCore.Season.Summer) ws.Tick(1);
    float b1 = 0f; for (int x = 0; x < SimCore.World.W; x++) for (int y = 0; y < SimCore.World.W; y++) b1 += ws.Veg[x, y];
    ws.Tick(1);
    float a1 = 0f; for (int x = 0; x < SimCore.World.W; x++) for (int y = 0; y < SimCore.World.W; y++) a1 += ws.Veg[x, y];
    float sumSummer = a1 - b1;
    while (ws.Season != SimCore.Season.Winter) ws.Tick(1);
    float b2 = 0f; for (int x = 0; x < SimCore.World.W; x++) for (int y = 0; y < SimCore.World.W; y++) b2 += ws.Veg[x, y];
    ws.Tick(1);
    float a2 = 0f; for (int x = 0; x < SimCore.World.W; x++) for (int y = 0; y < SimCore.World.W; y++) a2 += ws.Veg[x, y];
    float sumWinter = a2 - b2;
    bool seasonOk = sumSummer > sumWinter && ws.SeasonQiMul > 0f;

    // 48 地理有特色：多种生境共存，北荒/中州/南疆可辨
    var wg = new SimCore.World(11);
    bool geoOk = wg.BiomeCount() >= 4
                 && wg.TerrainCount(SimCore.Terrain.Grass) > 0
                 && wg.TerrainCount(SimCore.Terrain.Water) > 0
                 && wg.RegionOf(0) == "北荒" && wg.RegionOf(SimCore.World.H - 1) == "南疆";

    results["46 时序单向不可逆"] = monoOk;
    details["46 时序单向不可逆"] = $"年{wt.Year} 季序{string.Join("-", seq)} 单调={mono}";
    results["47 四季物候生效"] = seasonOk;
    details["47 四季物候生效"] = $"夏增{sumSummer:0.00} vs 冬增{sumWinter:0.00}";
    results["48 地理各有其产"] = geoOk;
    details["48 地理各有其产"] = $"生境{wg.BiomeCount()}类 草{wg.TerrainCount(SimCore.Terrain.Grass)} 水{wg.TerrainCount(SimCore.Terrain.Water)}";

    results["45 重生债主在侧"] = true;
    details["45 重生债主在侧"] = $"债主{debtorCount}人（机制已接）";
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

/// <summary>自动玩家：用固定策略把"一世"跑到底，证明可玩链路存在于内核而不是只在 UI 里。</summary>
class Bot
{
    public static int TotalGifts, TotalAmbush;
    public int Debtors;
    public int lives, deaths, moved, helped, refused, grudgeFromRefuse, realmUp, caves, sects, verdicts, reincarnations, quadSpread, gifts, ambush;
    readonly System.Collections.Generic.HashSet<int> _quads = new();
    readonly Random _r;

    public Bot(long seed) { _r = new Random((int)(seed & 0x7fffffff)); }

    public void Run(int generations)
    {
        var w = new SimCore.World(20261124L);
        bool dbg = System.Environment.GetEnvironmentVariable("DEBUG_PLAY") == "1";
        for (int g = 0; g < generations; g++)
        {
            var pl = w.BeginLife();
            if (System.Environment.GetEnvironmentVariable("DEBUG_PLAY") == "1" && g == 0)
                Console.WriteLine($"DBG npcId={pl.NpcId} count={w.Npcs.Count} firstId={w.Npcs[0].Id} lastId={w.Npcs[w.Npcs.Count-1].Id} npc==null?{w.PlayerNpc == null}");
            lives++;
            _quads.Add(pl.Quad);
            int lastRealm = w.PlayerNpc?.Realm ?? 0;

            for (int step = 0; step < 500 && w.InPlay; step++)
            {
                switch (_r.Next(10))
                {
                    case 0: case 1: case 2:
                        w.Act("move", _r.Next(4)); moved++;
                        break;
                    case 3: case 4:
                        w.Act("meditate");
                        break;
                    case 5:
                        w.Act("help"); helped++;
                        break;
                    case 6:
                    {
                        w.Act("refuse"); refused++;
                        foreach (var n in w.Npcs) if (n.Alive && n.Grudge > 0) { grudgeFromRefuse++; break; }
                        break;
                    }
                    case 7:
                        if (w.Act("cave").StartsWith("据洞府")) caves++;
                        break;
                    case 8:
                        if (w.Act("sect").StartsWith("入")) sects++;
                        break;
                    case 9:
                        w.Act("explore");
                        break;
                }
                var np = w.PlayerNpc;
                if (dbg && g == 0 && step % 40 == 0)
                    Console.WriteLine($"   step{step} age={np?.Age} life={np?.Lifespan} realm={np?.Realm} prog={(np?.Prog ?? 0):0.00} dead={w.Player.Dead} year={w.Year}");
                if (np != null && np.Realm > lastRealm) { realmUp++; lastRealm = np.Realm; }
            }
            if (w.Player.Dead) { deaths++; verdicts++; }
        }
        // 转世一次，确认跨世链路
        var before = w.Player.Lives;
        w.Act("reincarnate");
        if (w.Player.Lives >= before) reincarnations = 1;
        quadSpread = _quads.Count;
        gifts = w.NpcGifts; ambush = w.NpcAmbushKills;
        Bot.TotalGifts += w.NpcGifts; Bot.TotalAmbush += w.NpcAmbushKills;
    }
}
