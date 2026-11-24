using System;
using System.Collections.Generic;

namespace SimCore;

// ===========================================================================
// 数据模型：世界状态的全部类型。呈现层只读，不得修改。
// ===========================================================================

public enum Terrain { Grass, Rock, Water, Sand, Cliff }

public enum OrgKind { Tribe, Merchant, City, Sect, Empire }

public static class OrgNames
{
    public static readonly string[] Kind = { "部落", "商帮", "城邦", "宗门", "帝国" };
}

public sealed class Npc
{
    public int Id;
    public string Name = "";
    public string Dao = "";

    public float Gx, Gy, Tgx, Tgy;

    // 身体参数：立绘/小人由同一组参数派生（小人不穿大人衣服的物理来源）
    public float Height = 1f;
    public float Girth = 1f;
    public float Hue;

    // 立绘参数：五官与身材独立可调；衣饰按位拆装（不是换整套皮）
    public float EyeGap = 1f, JawW = 1f, BrowH = 1f, BrowAngle = 0f;
    public int WearMask = 0x7F;      // bit0 底衣 1 外袍 2 下裳 3 腰带 4 头饰 5 佩饰 6 发型
    public float AccHue = 0.33f;     // 佩饰色相（玉/金/骨/木）
    public int AccessoryKind = 0;    // 0 玉佩 1 剑匣 2 葫芦 3 珠串

    public int Realm;            // 境界 0..8
    public float Prog;           // 当前境界进度（个体独立，不能共享累加器）
    public int Age, Lifespan;
    public int Grudge, Favor;    // 记恨 / 记恩
    public int Deals;            // 居间撮合笔数（核心-边缘的度量）
    public int Karma;            // 因果净值
    public int Quad;             // 出生四象限
    public int InheritedHelp;    // 前世恩债带来的助力

    public int SectId = -1;
    public int ClanId = -1;
    public int CaveId = -1;
    public int TechId = -1;
    public int Faction = -1;

    public float TimeScale = 1f; // 所处区域时间流速
    public bool Alive = true;
    public bool IsPlayer;        // 玩家当前这一世的肉身（渲染层高亮、存档标记）
    public int Goal;             // 此人的目的：0 求道 1 聚财 2 传承 3 复仇 4 结党 5 避劫
    public float Imp;            // 对玩家的第一印象/评价（-100..100，会随时间淡化）
    public int Ambush;           // 出手杀人夺宝的次数
    public int Held;             // 身上带的灵石/宝货（被夺的也进这里）
    public int DeathYear = -1;
    public int Lives;            // 第几世
    public string Verdict = "";  // 判词（身殁后生成）
}

public sealed class Sect
{
    public int Id;
    public string Name = "";
    public int Tier = 1;
    public int ParentId = -1;    // 向上纳贡的大宗门（五层庇护链的第二环）
    public float Tithe = 0.30f;
    public float Treasury;
    public float Protection;
    public float Gx, Gy;
    public bool Alive = true;
    public int FoundedYear;
    public float PatriarchRealm = 4f;
    public float BestBranchRealm;
    public int Disciples;
    public int SplitFrom = -1;
    public int TechId = -1;
}

public sealed class Clan
{
    public int Id;
    public string Name = "";
    public int NationId = -1;
    public int SectId = -1;
    public float Wealth;
    public float Spirit;
    public float Gx, Gy;
    public bool Alive = true;
    public int SentTalents;
    public bool Royal;           // 皇室/执政家族
}

public sealed class Nation
{
    public int Id;
    public string Name = "";
    public float Pop;            // L2 统计层人口（凡人）
    public float K = 110000f;    // 承载上限：越接近越滞涨（马尔萨斯墙）
    public float Yield;
    public int SectId = -1;
    public float Gx, Gy;
    public bool Alive = true;
    public int Raids;
    public float Loyalty = 1f;
    public float Tithe;
}

public sealed class Faction
{
    public int Id;
    public string Name = "";
    public OrgKind Kind;
    public List<int> Members = new();
    public List<int> Nodes = new();
    public int Power;
    public int FoundedYear;
    public bool Alive = true;
    public int TeleportNodes;     // 传送阵覆盖
    public float ArrayHealth = 1f; // 阵法/传送阵健康度
}

public sealed class Cave
{
    public int Id;
    public int OwnerId = -1;
    public float Gx, Gy;
    public float Qi;             // 灵气/风水质量
    public int ArraySlots;
    public bool Visible = true;  // 阵法遮蔽：看得见
    public bool Collide = true;  // 阵法遮蔽：能撞墙
    public int WorldNodeId = -1;  // 对应一方世界（面积/内时/界门）
    public int Layout;           // 装修风格
    public float Upkeep;
    public float TimeScale = 1f; // 洞府内时间流速
}

public sealed class Technique
{
    public int Id;
    public string Name = "";
    public int RootId = -1;      // 改良自哪一部（-1 = 创派功法）
    public int FounderId;
    public int FoundYear;
    public int Element;          // 0金 1木 2水 3火 4土 5雷 6风 7花 8雪 9月
    public int Form;             // 0 剑气 1 光爆 2 波纹 3 火焰 4 藤蔓
    public float Power;
    public float Risk;           // 走火入魔率
    public int Improve;          // 改良次数
    public int OwnerSect = -1;
}

public sealed class Chronicle
{
    public int Year;
    public string Text = "";
}
