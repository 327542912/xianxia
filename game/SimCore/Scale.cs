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
    public float TimeScale = 1f;
    public int GateTo = -1;          // 界门通向哪个节点
    public bool GateOpen;            // 通道是否开启（需阵法/信物匹配）
    public int RealmNeed;            // 过门所需境界
    public int GridW, GridH;         // 内部场景网格（步/m 级）
    public float CellMeters = 1f;    // 每格代表多少米
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

    /// <summary>
    /// 开辟一方世界。内部场景网格按"人能画出来"的尺度生成：
    /// 洞府/小院用米级格，县域以上用公里级格，否则人在图里会小于 1 像素。
    /// </summary>
    public WorldNode OpenWorld(Npc owner, int tier, string name = null)
    {
        double areaKm2 = AreaOf(tier);
        float cellMeters = tier <= 2 ? 0.5f : tier <= 5 ? 10f : 1000f;
        double sideM = Math.Sqrt(areaKm2) * 1000.0;               // 近似正方形边长（米）
        int gw = Math.Max(8, Math.Min(64, (int)(sideM / cellMeters)));
        int gh = gw;

        var node = new WorldNode
        {
            Id = Worlds.Count,
            Name = name ?? $"{Tiers[tier].Name}·{owner.Name}",
            Tier = tier,
            AreaKm2 = areaKm2,
            OwnerId = owner.Id,
            TimeScale = 3f + tier * 2.2f,                          // 越大越自成时序
            GridW = gw, GridH = gh,
            CellMeters = cellMeters,
            RealmNeed = Math.Max(1, tier - 1)
        };
        Worlds.Add(node);
        AddLog($"Y{Year} {owner.Name} 开辟「{node.Name}」：{areaKm2:0.###} km²（{Tiers[tier].Name}），内时 {node.TimeScale:0.0}×");
        return node;
    }

    /// <summary>界门：小世界与小世界之间的通道。开不开，取决于两边阵法是否对得上。</summary>
    public WorldNode OpenGate(WorldNode from, WorldNode to)
    {
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
        double cellKm2 = 1.0;                      // 战略图一格 = 1 km²
        return $"地球陆地 {earthLand:0.00e0} km²；一个人脚下 {humanFootprint:0.00e0} km²，占 {humanFootprint / earthLand:0.00e0}；" +
               $"在 1 格=1 km² 的县域图上，人不足 1 像素（格边 1000 m，人 1.7 m）。";
    }

    /// <summary>初始化：给已有洞府的主人补上"世界节点"，让尺度从一开始就成立。</summary>
    private void InitWorlds()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive) continue;
            if (n.Realm >= 2 && n.CaveId >= 0)
            {
                int t = TierFor(n.Realm, 3, false);
                var node = OpenWorld(n, t);
                var cv = Caves[n.CaveId];
                cv.WorldNodeId = node.Id;
            }
        }
        // 两个既有小世界 + 一道界门，用来验证"通道"这条规则
        if (Npcs.Count > 0)
        {
            var a = OpenWorld(Npcs[0], 4, "云梦泽");
            var b = OpenWorld(Npcs[0], 5, "赤霄洞天");
            OpenGate(a, b);
        }
    }
}
