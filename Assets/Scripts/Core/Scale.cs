using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>一个"世界"节点：洞府、洞天、秘境、小世界、大界都算。</summary>
public sealed class WorldNode
{
    public int Id;
    public string Name = "";
    public int Tier;                 // 0..11，见 Scale.Tiers
    public double AreaKm2;           // 占地面积（km²）
    public int OwnerId = -1;         // 开辟者（NPC/玩家）
    public float TimeScale = 1f;     // 内时流速
    public int GateTo = -1;          // 界门通向哪个节点
    public bool GateOpen;            // 通道是否开启（需阵法/信物匹配）
    public int RealmNeed;            // 过门所需境界
    public int GridW, GridH;         // 内部场景网格（步/m 级）
    public float CellMeters = 1f;    // 每格代表多少米

    // 小世界的经济学：开多大、流多快，都要付代价（需求 v1.1 §2.16）
    public Grade Grade = Grade.Fan;  // 空间品阶，受开辟者品阶封顶
    public float CostMonths;         // 开辟时折掉的寿元（月）
    public float UpkeepQi;           // 每月维持消耗的灵气
    public float Instability;        // 不稳定度：到 1 就崩
    public int OpenedYear;
    public bool Collapsed;           // 崩解后成为"遗府"，不再是可用的私有世界
}

/// <summary>
/// 尺度模型：一切面积都以地球为基准，不许"随便画一张图当世界"。
/// 地球陆地面积 1.49 亿 km² = Tier9；人在 1.7 m，一格 1 km 的县图上不到 1 像素 —— 所以必须分三级 LOD。
/// </summary>
public sealed partial class World
{
    public readonly List<WorldNode> Worlds = new();

    /// <summary>面积档位表（km²）。每一档相差 1~2 个数量级，不允许"跳跃式随便定"。</summary>
    public static readonly (string Name, double Km2)[] Tiers =
    {
        ("静室",        0.0001),      // 0  10 m²：一榻一炉
        ("洞府",        0.01),        // 1  100 m²
        ("小院",        0.04),        // 2  400 m²
        ("庄园",        1.0),         // 3  1 km²
        ("村落",        10.0),        // 4
        ("乡镇",        100.0),       // 5
        ("县域",        1000.0),      // 6  32×32 km
        ("州郡",        10000.0),     // 7
        ("国度",        100000.0),    // 8
        ("大陆",        1000000.0),   // 9a 百万 km²
        ("地球级",      149000000.0), // 9b 地球陆地
        ("巨界",        1e10),        // 10 恒星系尺度
        ("宇宙级",      1e23),        // 11 不可观测，只作叙事
    };

    /// <summary>境界 → 能开辟到的基础档位。凡人连洞府都要靠运气。</summary>
    public static int BaseTierForRealm(int realm) => realm switch
    {
        <= 1 => 1,     // 洞府
        2 or 3 => 2,   // 小院
        4 => 3,        // 庄园
        5 => 4,        // 村落
        6 => 5,        // 乡镇
        7 => 6,        // 县域
        _ => 7         // 州郡（再往上要靠法宝与灵脉，不靠修为硬撑）
    };

    /// <summary>
    /// 实际能开辟多大 = 基础档 + 资源加成（灵石/灵脉/法宝），最多再推 2 档。
    /// 这就是"看境界与资源决定面积"，不是拍脑袋。
    /// </summary>
    public int TierFor(int realm, int stone, bool hasTreasure)
    {
        int t = BaseTierForRealm(realm);
        if (stone >= 4) t++;
        if (stone >= 12) t++;
        if (hasTreasure) t++;
        return Math.Min(Tiers.Length - 1, t);
    }

    public static double AreaOf(int tier) => tier >= 0 && tier < Tiers.Length ? Tiers[tier].Km2 : 0;

    /// <summary>开辟代价（月寿元）：面积越大、流速越猛，折寿越快；品阶高的人摊得薄一些。</summary>
    public static float OpenCostMonths(int tier, float flow, Grade grade)
    {
        double areaK = 1.0 + 0.9 * Math.Max(0, tier - 1);
        double flowK = Math.Pow(Math.Max(1f, flow) / 5.0, 0.85);
        double g = 1.0 / (1.0 + 0.35 * (int)grade);
        return (float)Math.Round(12.0 * areaK * flowK * g, 1);
    }

    /// <summary>维持消耗（每月灵气）：流速越高的世界，抽灵脉越凶。</summary>
    public static float UpkeepOf(int tier, float flow)
        => (float)Math.Round((tier + 1) * 0.45 + flow * 0.12, 2);

    /// <summary>
    /// 开辟一方世界。内部场景网格按"人能画出来"的尺度生成：
    /// 洞府/小院用米级格，县域以上用公里级格，否则人在图里会小于 1 像素。
    /// flow 由开辟者定义（这就是"可以自定义时间流速"），但代价 = 面积 × 流速，超线性折寿。
    /// </summary>
    public WorldNode OpenWorld(Npc owner, int tier, string name = null, float flow = 0f, bool charge = true)
    {
        if (owner == null) return null;
        tier = Math.Clamp(tier, 0, Tiers.Length - 1);
        if (flow <= 0f) flow = 3f + tier * 2.2f;
        flow = Math.Clamp(flow, 1f, 120f);

        double areaKm2 = AreaOf(tier);
        float cellMeters = tier <= 2 ? 0.5f : tier <= 5 ? 10f : 1000f;
        double sideM = Math.Sqrt(areaKm2) * 1000.0;
        int gw = Math.Max(8, Math.Min(64, (int)(sideM / cellMeters)));
        int gh = gw;

        float cost = OpenCostMonths(tier, flow, owner.Grade);

        var node = new WorldNode
        {
            Id = Worlds.Count,
            Name = name ?? $"{Tiers[tier].Name}·{owner.Name}",
            Tier = tier,
            AreaKm2 = areaKm2,
            OwnerId = owner.Id,
            TimeScale = flow,
            GridW = gw, GridH = gh,
            CellMeters = cellMeters,
            RealmNeed = Math.Max(1, tier - 1),
            Grade = owner.Grade,
            CostMonths = cost,
            UpkeepQi = UpkeepOf(tier, flow),
            Instability = Math.Min(0.55f, 0.08f + 0.22f * (float)Math.Pow(flow / 50f, 1.3) * (0.5f + tier / 6f)),
            OpenedYear = Year
        };

        if (charge)
        {
            // 代价从开辟者的寿元里扣；扣到不够活过当月，就是拿命换一片天地
            owner.Lifespan = Math.Max(owner.Age + 1, owner.Lifespan - (int)Math.Round(cost));
        }

        Worlds.Add(node);
        AddLog(charge
            ? $"Y{Year} {owner.Name} 开辟「{node.Name}」：{areaKm2:0.###} km²（{Tiers[tier].Name}），内时 {node.TimeScale:0.0}×，折寿 {cost / 12:0.#} 年"
            : $"Y{Year} {owner.Name} 开辟「{node.Name}」：{areaKm2:0.###} km²（{Tiers[tier].Name}），内时 {node.TimeScale:0.0}×");
        return node;
    }

    /// <summary>
    /// 改流速（需求 v1.1 §2.16：定义时间流速必须有代价）。
    /// 调快 → 折寿、维持翻倍、不稳定度上涨；调慢 → 不退还已付的代价。
    /// </summary>
    public bool SetFlow(WorldNode node, Npc owner, float flow, out string msg)
    {
        if (node == null || node.Collapsed) { msg = "此界已崩，改不了流速。"; return false; }
        if (owner == null || node.OwnerId != owner.Id) { msg = "不是你的世界，阵纹不认你。"; return false; }
        flow = Math.Clamp(flow, 1f, 120f);
        if (Math.Abs(flow - node.TimeScale) < 0.5f) { msg = $"内时已是 {node.TimeScale:0}×。"; return false; }

        float delta = Math.Abs(flow - node.TimeScale);
        float cost = (float)Math.Round(delta * (node.Tier + 1) * 0.9 / (1.0 + 0.35 * (int)owner.Grade), 1);
        owner.Lifespan = Math.Max(owner.Age + 1, owner.Lifespan - (int)Math.Round(cost));
        node.TimeScale = flow;
        node.UpkeepQi = UpkeepOf(node.Tier, flow);
        node.Instability = Math.Min(0.99f, node.Instability + delta / Math.Max(20f, flow) * 0.55f);
        node.CostMonths += cost;
        msg = $"「{node.Name}」内时改为 {flow:0}×：折寿 {cost / 12:0.#} 年，不稳 {node.Instability:P0}。";
        if (node.Instability >= 0.99f) CollapseWorld(node, "流速过猛，阵基承受不住");
        return true;
    }

    private void CollapseWorld(WorldNode node, string why)
    {
        node.Collapsed = true;
        node.GateTo = -1;
        node.OwnerId = -1;
        AddLog($"Y{Year} 「{node.Name}」崩解（{why}）：内时乱流，只余遗府");
    }

    /// <summary>每个月结算所有小世界：维持抽灵、不稳累积、崩解成遗府。</summary>
    private void StepWorlds()
    {
        foreach (var node in Worlds)
        {
            if (node.Collapsed) continue;
            Npc owner = null;
            foreach (var n in Npcs) if (n.Id == node.OwnerId && n.Alive) { owner = n; break; }
            if (owner == null) { CollapseWorld(node, "主人身殒，无人维持阵基"); continue; }

            // 维持：内时越快，主人老得越快（与 StepPlayerLife 的洞府加速同源）
            float aging = Math.Max(0f, node.TimeScale - 1f) * 0.05f;
            owner.Age += (int)Math.Round(aging);

            node.Instability += 0.0008f * node.TimeScale / 10f;
            if (node.Instability >= 0.995f)
            {
                CollapseWorld(node, "阵基年久失修");
                continue;
            }
            if (node.Instability > 0.75f && _rng.NextDouble() < 0.004f)
                CollapseWorld(node, "内时乱流冲垮阵眼");
            else if (_rng.NextDouble() < 0.0009f * node.Tier)
                CollapseWorld(node, "灵脉被抽空");
        }
    }

    /// <summary>界门：小世界与小世界之间的通道。开不开，取决于两边阵法是否对得上。</summary>
    public WorldNode OpenGate(WorldNode from, WorldNode to)
    {
        if (from == null || to == null || from.Collapsed || to.Collapsed) return null;
        from.GateTo = to.Id;
        to.GateTo = from.Id;
        bool match = Math.Abs(from.TimeScale - to.TimeScale) < 6f || _rng.NextDouble() < 0.35;
        from.GateOpen = match;
        to.GateOpen = match;
        AddLog($"Y{Year} 界门：{from.Name} ⇄ {to.Name}，{(match ? "阵纹相合，可通行" : "阵纹不合，撞壁而回")}");
        return match ? to : null;
    }

    /// <summary>过门判定：境界不够或阵纹不合，就是"看得见走不进"。</summary>
    public string Traverse(int nodeId, Npc who)
    {
        if (nodeId < 0 || nodeId >= Worlds.Count) return "此界不存。";
        var node = Worlds[nodeId];
        if (node.Collapsed) return "此界已崩，只余遗迹。";
        if (!node.GateOpen) return "界门阵纹不合，撞壁而回。";
        if (who.Realm < node.RealmNeed) return $"境界不足（需 {node.RealmNeed} 境），门在你眼前，你过不去。";
        who.Gx = R(1, W - 2); who.Gy = R(1, H - 2);
        who.TimeScale = node.TimeScale;
        return $"入「{node.Name}」：{node.AreaKm2:0.###} km²，内时 {node.TimeScale:0}×。";
    }

    /// <summary>人在地球上的比例：用来回答"人到底能占多大"。</summary>
    public static string HumanRatioNote()
    {
        double earthLand = 1.49e8;                 // km²
        double humanFootprint = 0.7 * 0.5 / 1e6;   // 0.35 m² → km²
        return $"地球陆地 {earthLand:0.00e0} km²；一个人脚下 {humanFootprint:0.00e0} km²，占 {humanFootprint / earthLand:0.00e0}；" +
               $"在 1 格=1 km² 的县域图上，人不足 1 像素（格边 1000 m，人 1.7 m）。";
    }

    /// <summary>初始化：给已有洞府的主人补上"世界节点"，让尺度从一开始就成立（不收费）。</summary>
    private void InitWorlds()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive) continue;
            if (n.Realm >= 2 && n.CaveId >= 0)
            {
                int t = TierFor(n.Realm, 3, false);
                var node = OpenWorld(n, t, null, 0f, false);
                var cv = Caves[n.CaveId];
                cv.WorldNodeId = node.Id;
            }
        }
        // 两个既有小世界 + 一道界门，用来验证"通道"这条规则
        if (Npcs.Count > 0)
        {
            var a = OpenWorld(Npcs[0], 4, "云梦泽", 0f, false);
            var b = OpenWorld(Npcs[0], 5, "赤霄洞天", 0f, false);
            OpenGate(a, b);
        }
    }
}