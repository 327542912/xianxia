using System;
using System.Collections.Generic;
using System.Text;

namespace SimCore;

/// <summary>
/// 世界内核（纯 C#，确定性，无引擎依赖）。
/// 覆盖需求文档 15 章：庇护链 / 资源流 / 抽成率 / 上升通道 / 离心 / 四象限出生 /
/// 前世恩债 / 货币涌现 / 家庭 / 组织 / 帝国 / 洞府阵法 / 功法改良 / 时间流速 / 判词 / 存档。
/// </summary>
public sealed partial class World
{
    public const int W = 32, H = 32;      // 大地图：一屏约 10 格，可拖动缩放

    public int Year;
    public int Month;            // 0..11，当前年份内的月份（真实时间流速：一年 12 个月）
    public int NpcGifts, NpcAmbushKills;   // NPC 主动扶你 / 主动抢你的次数
    public long Seed;
    public Terrain[,] Tiles = new Terrain[W, H];
    // 世界本身就是资源：每格灵气、植被、不可逆破坏（Dmg 只增不减）
    public float[,] Qi = new float[W, H];
    public float[,] Veg = new float[W, H];
    public float[,] Dmg = new float[W, H];
    public float QiMax;                 // 初始灵气总量（守恒的基准线）
    public float TotalQi;               // 当前世界灵气总量
    public int Marriages, Births;       // 婚配数 / 新生儿数
    public int EnvDestroyed, EnvPlanted; // 破坏 / 新增（种植、阵法回补）

    public readonly List<Npc> Npcs = new();
    public readonly List<Faction> Factions = new();
    public readonly List<Nation> Nations = new();
    public readonly List<Clan> Clans = new();
    public readonly List<Sect> Sects = new();
    public readonly List<Cave> Caves = new();
    public readonly List<Technique> Techs = new();
    public readonly List<Chronicle> Log = new();

    public int Grudges, Favors, Splits, Merges, RaidCount, TalentsSent, Deaths, Rebirths;
    public float TitheAvg;
    public float KarmaLedger;
    public readonly int[] Quads = new int[4];
    public readonly Dictionary<string, float> Acceptance = new() { ["粟"] = 0.18f, ["铁"] = 0.10f, ["灵石"] = 0.02f };
    public string CurrencyWord = "无";

    /// <summary>
    /// 自研确定性 RNG（xorshift64*）。不用 System.Random：
    /// 它的实现在不同 .NET 版本间不保证一致，而联机/回放要求「同种子必同世界」跨机器成立。
    /// </summary>
    private sealed class Rng
    {
        private ulong _s;
        public Rng(long seed)
        {
            _s = (ulong)seed * 6364136223846793005UL + 1442695040888963407UL;
            if (_s == 0) _s = 88172645463325252UL;
            for (int i = 0; i < 12; i++) NextU64();
        }
        private ulong NextU64()
        {
            ulong x = _s;
            x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
            _s = x;
            return x * 2685821657736338717UL;
        }
        public int Next(int a, int b) => a + (int)((NextU64() >> 33) % (ulong)Math.Max(1, b - a));
        public double NextDouble() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
    }

    private readonly Rng _rng;
    private readonly Rng _rngImp;   // 印象/声望独立随机流：不污染世界演化，保证老验收不漂
    private int _nextFamilyYear, _nextFactionYear, _nextCaveYear, _nextTechYear, _nextBirthYear;
    private int _idCounter;

    private static readonly string[] Surnames =
        { "李","陈","张","柳","慕容","上官","赵","沈","云","萧","顾","谢","苏","叶","秦","陆","宁","季","洛","白", "东方","端木" };
    private static readonly string[] Givens =
        { "尘","渊","清","玄","辞","舟","砚","川","岚","述","珩","棠","弋","禺","澈","瑟","笙","爻","临","孤","牧","烛" };
    private static readonly string[] DaoWords =
        { "玄清","太一","无名","凌霄","归墟","抱朴","青冥","溯光","无咎","照夜","洗尘","斩妄" };

    public World(long seed)
    {
        Seed = seed;
        _rng = new Rng(seed);
        _rngImp = new Rng(seed ^ 0x5DEECE66DL);
        Year = 1;
        GenTerrain();
        InitResources();
        InitFeudal();
        InitCaves();
        InitTechs();
        for (int i = 0; i < 36; i++) Spawn();
        InitWorlds();
        _nextFactionYear = 10; _nextFamilyYear = 6; _nextCaveYear = 18; _nextTechYear = 22; _nextBirthYear = 8;
    }

    private int R(int a, int b) => _rng.Next(a, b + 1);
    private float Rf() => (float)_rng.NextDouble();
    private int NextId() => ++_idCounter;

    private void GenTerrain()
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float cx = (x - W * 0.5f) / (W * 0.5f);
                float cy = (y - H * 0.5f) / (H * 0.5f);
                float d = (float)Math.Sqrt(cx * cx + cy * cy);
                float h = d * 0.92f + Rf() * 0.26f;
                Tiles[x, y] = h > 0.94f ? Terrain.Cliff
                    : h > 0.74f ? Terrain.Rock
                    : h > 0.60f ? Terrain.Sand
                    : h < 0.16f ? Terrain.Water
                    : Terrain.Grass;
            }
    }

    /// <summary>按地形给资源底子：水脉最足，石山最薄。</summary>
    private void InitResources()
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                float q = Tiles[x, y] switch
                {
                    Terrain.Water => 1.25f,
                    Terrain.Grass => 0.95f,
                    Terrain.Sand => 0.55f,
                    Terrain.Rock => 0.42f,
                    _ => 0.30f
                };
                q += Rf() * 0.12f;
                Qi[x, y] = q;
                Veg[x, y] = Tiles[x, y] == Terrain.Grass ? 0.85f : Tiles[x, y] == Terrain.Sand ? 0.25f : 0.12f;
                Dmg[x, y] = 0f;
            }
        QiMax = 0f;
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) QiMax += Qi[x, y];
        TotalQi = QiMax;
    }

    public float AvgQi => TotalQi / Math.Max(1f, QiMax);

    // -------------------------------------------------------------- 时序：年 → 季，单向
    /// <summary>当前季节。只由 Year 推出，Year 只增，故季节不可回退。</summary>
    // 四季按月份走：1-3 春、4-6 夏、7-9 秋、10-12 冬（真实时间流速，一年 12 个月）
    public Season Season => (Season)(Month / 3);
    public string SeasonName => SeasonNames.Name[(int)Season];
    public string Phenology => SeasonNames.Phenology[(int)Season];

    /// <summary>四季对生机的影响：夏最旺，冬最枯。这是物候，不是数值调节。</summary>
    public float SeasonGrowMul => Season switch
    {
        Season.Spring => 1.10f,
        Season.Summer => 1.38f,
        Season.Autumn => 0.92f,
        _ => 0.42f
    };

    /// <summary>四季对灵气的吞吐：夏涨、冬藏。</summary>
    public float SeasonQiMul => Season switch
    {
        Season.Spring => 1.00f,
        Season.Summer => 1.15f,
        Season.Autumn => 0.95f,
        _ => 0.78f
    };

    // -------------------------------------------------------------- 地理：地貌 → 地名
    /// <summary>逐格地理类型：地形 + 纬度带（北寒/中州/南疆）+ 植被厚薄。</summary>
    public int BiomeAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return 2;
        bool north = y < H * 0.3f, south = y > H * 0.7f;
        Terrain t = Tiles[x, y];
        switch (t)
        {
            case Terrain.Water: return 5;   // 水泽
            case Terrain.Cliff: return 6;   // 绝壁
            case Terrain.Rock: return north || south ? 3 : 3;   // 雪岭（高处封雪）
            case Terrain.Sand: return north ? 0 : 4;            // 北为寒漠、南为荒漠
            default:
                if (north) return 0;                              // 寒原
                return Veg[x, y] > 0.70f ? 1 : 2;                  // 林莽 / 沃野
        }
    }

    public string BiomeName(int x, int y) => Geography.Name[BiomeAt(x, y)];

    public int BiomeCount()
    {
        var seen = new HashSet<int>();
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) seen.Add(BiomeAt(x, y));
        return seen.Count;
    }

    /// <summary>地域称谓：北荒 / 中州 / 南疆。</summary>
    public string RegionOf(int y) => y < H * 0.3f ? "北荒" : y > H * 0.7f ? "南疆" : "中州";

    public int TerrainCount(Terrain t)
    {
        int c = 0;
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (Tiles[x, y] == t) c++;
        return c;
    }

    public bool Walkable(int x, int y) =>
        x >= 0 && y >= 0 && x < W && y < H && Tiles[x, y] != Terrain.Water && Tiles[x, y] != Terrain.Cliff;

    public int AliveCount { get { int c = 0; foreach (var n in Npcs) if (n.Alive) c++; return c; } }

    /// <summary>世界总人口（个体 + 统计层凡人）——"10 万在册"的计算口径。</summary>
    public float TotalPopulation
    {
        get
        {
            float p = 0;
            foreach (var n in Npcs) if (n.Alive) p += 1;
            foreach (var nt in Nations) if (nt.Alive) p += nt.Pop;
            return p;
        }
    }

    // ------------------------------------------------------------------ 初始化
    private void InitFeudal()
    {
        for (int i = 0; i < 2; i++)
            Sects.Add(new Sect
            {
                Id = i, Name = i == 0 ? "太乙宗" : "玄都宗", Tier = 3,
                Tithe = 0.26f + Rf() * 0.12f, Treasury = 150f, Protection = 0.7f,
                PatriarchRealm = 6f + Rf(), Gx = 6 + i * 11, Gy = 6 + i * 11,
                FoundedYear = Year, TechId = i
            });

        string[] mids = { "无量宗", "流云宗", "金石宗" };
        for (int i = 0; i < 3; i++)
            Sects.Add(new Sect
            {
                Id = Sects.Count, Name = mids[i], Tier = 2,
                Tithe = 0.29f + Rf() * 0.09f, Treasury = 70f, Protection = 0.55f,
                PatriarchRealm = 4.2f + Rf(), Gx = 9 + i * 7, Gy = 10 + i * 6,
                ParentId = Sects.Count < 2 ? 0 : (i % 2),
                FoundedYear = Year
            });

        string[] nations = { "陈国", "梁国" };
        string[] clanNames = { "东郭家", "南宫家", "西陵家", "北原家", "平舆家", "安邑家" };
        for (int i = 0; i < 2; i++)
        {
            var nat = new Nation
            {
                Id = i, Name = nations[i], Pop = 46000f + Rf() * 8000f, K = 47000f + Rf() * 12000f,
                SectId = i, Gx = 8 + i * 16, Gy = 8 + i * 16   // 国度由大宗门庇护
            };
            Nations.Add(nat);
            for (int c = 0; c < 3; c++)
                Clans.Add(new Clan
                {
                    Id = Clans.Count, Name = clanNames[(i * 3 + c) % 6],
                    NationId = nat.Id, SectId = 2 + c % 3,
                    Wealth = 24f + Rf() * 16f, Spirit = Rf() * 0.35f,
                    Gx = nat.Gx + R(-4, 4), Gy = nat.Gy + R(-4, 4),
                    Royal = c == 0
                });
        }
        AddLog($"Y{Year} 凡间二国、六家、五宗立：庇护链起，抽成即秩序");
    }

    private void InitCaves()
    {
        for (int i = 0; i < 14; i++)
            Caves.Add(new Cave
            {
                Id = i, Gx = R(1, W - 2), Gy = R(1, H - 2),
                Qi = 0.40f + Rf() * 0.70f, ArraySlots = R(1, 3),
                Visible = _rng.NextDouble() < 0.55, Collide = _rng.NextDouble() < 0.75,
                Layout = R(0, 5), Upkeep = 6f + Rf() * 10f, TimeScale = 10f + Rf() * 60f
            });
    }

    private void InitTechs()
    {
        string[] names = { "青冥剑典", "太乙雷法", "流云水诀", "赤炎焚天", "金石炼体", "照夜月华" };
        int[] elems = { 1, 5, 2, 3, 0, 9 };
        int[] forms = { 0, 1, 2, 3, 0, 1 };
        for (int i = 0; i < names.Length; i++)
            Techs.Add(new Technique
            {
                Id = i, Name = names[i], FounderId = 0, FoundYear = Year - R(20, 80),
                Element = elems[i], Form = forms[i], Power = 1.0f + i * 0.08f,
                Risk = 0.05f + i * 0.01f, OwnerSect = i < 2 ? i : -1
            });
    }

    // ------------------------------------------------------------------ 推进
    /// <summary>推进 n 个月（12 个月 = 1 年）。世界按真实时间流速走：一年 12 个月，逐月结算。</summary>
    public void Tick(int months = 1)
    {
        for (int i = 0; i < months; i++)
        {
            StepOnce();
            Month++;
            if (Month >= 12) { Month = 0; Year++; }
            // 玩家"待月"会自己把时钟拨到下个月；世界时钟若落后，就补到同一月，
            // 否则行动冷却（一月一事）永远解不开，玩家会卡死在"本月已行一事"。
            // 注意：只补"落后"，绝不回拨——回拨会让世界时间倒退，NPC 年龄/事件全乱。
            // 玩家已死时也要对齐：判词卡上的年份不能和世界时钟对不上。
            if (Player.NpcId >= 0 && (Year < Player.ActedYear ||
                (Year == Player.ActedYear && Month < Player.ActedMonth)))
            {
                Year = Player.ActedYear;
                Month = Player.ActedMonth;
            }
            StepPlayerLife();   // 玩家随世界时钟老去：真实时间探索时，寿元也在走
        }
    }

    /// <summary>玩家被动老化：世界每过一月，玩家长一月（洞府内时加速则老得更快）。
    /// 行动耗粮在 AdvanceLife 里结算，这里只管年龄与寿终。</summary>
    private void StepPlayerLife()
    {
        var npc = PlayerNpc;
        if (npc == null || !npc.Alive || Player.Dead) return;
        npc.Age += 1;
        if (npc.CaveId >= 0) npc.Age += 2;   // 内时快，老得快
        Player.Years++;
        if (npc.Age > npc.Lifespan) Die("寿元耗尽");
    }

    private void StepOnce()
    {
        StepCultivation();
        StepImpressions();     // 印象与声望：NPC 的善意与杀意都在这里结算
        StepIndividuals();
        StepFamilies();
        StepFactions();
        StepFeudal();
        StepCaves();
        StepTechniques();
        StepCurrency();
        StepEnvironment();
        StepMarriage();
        StepAggregate();
    }

    // -------------------------------------------------------------- 环境：一切皆资源，破坏不可逆
    private void StepEnvironment()
    {
        float sum = 0f;
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                // 植被恢复：被破坏过的地方恢复得慢（灵气差），这是"伤过就留下了"
                float cap = Qi[x, y] * 1.05f;
                float biomeK = BiomeAt(x, y) switch { 3 or 6 => 0.35f, 0 => 0.55f, 4 => 0.45f, 1 => 1.15f, _ => 1.00f };
                if (Veg[x, y] < cap && Dmg[x, y] < 0.9f)
                    Veg[x, y] = Math.Min(cap, Veg[x, y] + 0.010f / 12f * SeasonGrowMul * biomeK * (1f - Dmg[x, y]));   // 年恢复速率，按月摊薄
                Qi[x, y] = Math.Max(0f, Qi[x, y] - Dmg[x, y] * 0.004f / 12f);   // 破坏持续抽走灵气（年速率摊薄）
                sum += Qi[x, y] * (1f - 0.35f * Dmg[x, y]) * SeasonQiMul;
            }
        TotalQi = sum;

        // 修士克制：灵气越稀缺，动手开山采脉的越少（不是道德，是怕断了自己的道途）
        float ratio = AvgQi;
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer || n.Realm < 4) continue;
            if (_rng.NextDouble() > 0.04f * ratio) continue;
            int x = (int)n.Gx, y = (int)n.Gy;
            if (x < 0 || y < 0 || x >= W || y >= H) continue;
            Dmg[x, y] = Math.Min(1f, Dmg[x, y] + 0.10f);
            Qi[x, y] = Math.Max(0f, Qi[x, y] - 0.12f);
            Veg[x, y] = Math.Max(0f, Veg[x, y] - 0.40f);
            n.Held += 2;
            EnvDestroyed++;
        }

        // 人气随灵气：灵气薄的地方，新生儿留不住、外来人也少（生育按 AvgQi 折算）
    }

    private int AliveNpcsCount() { int c = 0; foreach (var n in Npcs) if (n.Alive) c++; return c; }

    // -------------------------------------------------------------- 婚配与血脉传承
    private void StepMarriage()
    {
        var snapshot = new List<Npc>(Npcs);   // 婚配过程中会添丁，不能在原表上迭代
        foreach (var a in snapshot)
        {
            if (!a.Alive || a.SpouseId >= 0 || a.Age < 18 * 12) continue;
            foreach (var b in snapshot)
            {
                if (b.Id <= a.Id || !b.Alive || b.SpouseId >= 0 || b.Age < 18 * 12) continue;
                if (b.Gender == a.Gender) continue;
                float dx = a.Gx - b.Gx, dy = a.Gy - b.Gy;
                if (dx * dx + dy * dy > 9f) continue;   // 三里之内才谈得上婚配
                if (_rng.NextDouble() > 0.30 / 12.0) continue;   // 年婚配率 0.30，按月摊薄

                a.SpouseId = b.Id; b.SpouseId = a.Id; Marriages++;

                // 婚配当年是否添丁：这是一次性事件，不是月度概率，不能再除 12
                // （旧版误除 12 → 150 年婚 21 生 0，血脉传承断链）
                if (_rng.NextDouble() < 0.42 * Math.Max(0.15f, AvgQi))
                {
                    SpawnChild(a, b);
                }
                break;
            }
        }

        // 已婚夫妇持续添丁：家庭不只在婚配那一瞬产生孩子（在册个体有上限，防 O(n²) 失控）
        if (AliveCount < 90)
        {
            foreach (var a in snapshot)
            {
                if (!a.Alive || a.SpouseId < 0 || a.Gender != 1) continue;   // 以母方计育龄
                if (a.Age < 18 * 12 || a.Age > 45 * 12) continue;
                var b = Npcs.Find(z => z.Id == a.SpouseId);
                if (b == null || !b.Alive) continue;
                if (_rng.NextDouble() < 0.06 / 12.0 * Math.Max(0.15f, AvgQi))   // 年生育率 6%，按月摊薄
                    SpawnChild(a, b);
            }
        }
    }

    /// <summary>血脉传承：相貌/身材/天资从父母继承，家产与恩债按父母因果折算。</summary>
    private void SpawnChild(Npc a, Npc b)
    {
        var child = new Npc
        {
            Id = NextId(),
            Name = a.Name.Substring(0, 1) + Givens[R(0, Givens.Length - 1)],
            Lives = 1,
            Gx = Math.Clamp((a.Gx + b.Gx) * 0.5f + Rf() - 0.5f, 1f, W - 2f),
            Gy = Math.Clamp((a.Gy + b.Gy) * 0.5f + Rf() - 0.5f, 1f, H - 2f),
            Height = (a.Height + b.Height) * 0.5f + (Rf() - 0.5f) * 0.10f,
            Girth = (a.Girth + b.Girth) * 0.5f + (Rf() - 0.5f) * 0.10f,
            Hue = (a.Hue + b.Hue) * 0.5f + (Rf() - 0.5f) * 0.06f,
            EyeGap = (a.EyeGap + b.EyeGap) * 0.5f,
            JawW = (a.JawW + b.JawW) * 0.5f,
            BrowH = (a.BrowH + b.BrowH) * 0.5f,
            BrowAngle = (a.BrowAngle + b.BrowAngle) * 0.5f,
            WearMask = 0x7F,
            Goal = _rngImp.Next(0, 6),
            Gender = _rng.NextDouble() < 0.5 ? 0 : 1,
            Age = 0,
            Lifespan = (60 + R(0, 22)) * 12,   // 月龄
            Realm = Math.Max(0, Math.Min(2, (a.Realm + b.Realm) / 4)),
            Quad = (a.Quad + b.Quad) / 2,
            Karma = (a.Karma + b.Karma) / 4,
            InheritedHelp = Math.Max(0, (a.Favor + b.Favor - a.Grudge - b.Grudge) / 6),
            ParentA = a.Id, ParentB = b.Id,
            ClanId = a.ClanId >= 0 ? a.ClanId : b.ClanId,
            SectId = a.SectId >= 0 ? a.SectId : b.SectId
        };
        Npcs.Add(child);
        Births++;
    }

    // -------------------------------------------------------------- 修炼 / 死亡 / 轮回
    private void StepCultivation()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;   // 玩家不可被世界流水线杀掉/轮回

            float speed = n.CaveId >= 0 ? Math.Min(n.TimeScale, 20f) : 1f;
            float gain = 0.012f / 12f * speed * (n.Quad == 0 || n.Quad == 2 ? 1.35f : 1f);   // 年增速 0.012 按月摊薄（旧版误乘 12，全员速满境界，四象限差消失）
            if (n.InheritedHelp > 0) gain *= 1f + n.InheritedHelp * 0.10f;
            if (n.Realm >= 4) gain *= 0.55f;          // 越高越慢（瓶颈）
            if (n.Realm >= 6) gain *= 0.45f;
            n.Prog += gain;
            while (n.Prog >= 1f && n.Realm < 8) { n.Prog -= 1f; n.Realm++; }

            n.Age += (int)Math.Max(1f, Math.Round(speed));   // 月龄 +1（洞府内时间更快 → 老得也快）
            if (n.Age > n.Lifespan || _rng.NextDouble() < 0.004 / 12.0) Kill(n);   // 意外身故按月摊薄
        }
    }


    private void Kill(Npc n)
    {
        if (n.IsPlayer) return;
        n.Alive = false;
        n.DeathYear = Year;
        Deaths++;
        KarmaLedger += (n.Favor - n.Grudge) * 0.12f;
        n.Verdict = Verdict(n);
        AddLog($"Y{Year} {n.Name} 身殒（{n.Realm}境）：{n.Verdict}");
        if (_rng.NextDouble() < 0.55) { Rebirths++; Spawn(n); }
    }

    /// <summary>判词：事实层 + 评价层 + 裁决层，只出断语，不露分数。</summary>
    public string Verdict(Npc n)
    {
        float score = n.Karma * 1.6f + (n.Favor - n.Grudge) * 2.4f + n.Realm * 7f + n.Lives * 3f;
        if (n.Grudge > n.Favor * 2 && score < 0) return "横死·仇家环伺";
        if (score > 85) return "大富大贵，一乡称雄";
        if (score > 45) return "小康有寿，子孙可托";
        if (score > 12) return "清贫自守，术业有名";
        return "穷通无门，草草一生";
    }

    // -------------------------------------------------------------- 个体
    private void StepIndividuals()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive) continue;
            if (Math.Abs(n.Gx - n.Tgx) < 0.05f && Math.Abs(n.Gy - n.Tgy) < 0.05f)
            {
                if (_rng.NextDouble() < 0.45)
                {
                    int tx = Math.Clamp((int)n.Gx + R(-3, 3), 0, W - 1);
                    int ty = Math.Clamp((int)n.Gy + R(-3, 3), 0, H - 1);
                    if (Walkable(tx, ty)) { n.Tgx = tx; n.Tgy = ty; }
                }
            }
            else
            {
                float dx = Math.Sign(n.Tgx - n.Gx), dy = Math.Sign(n.Tgy - n.Gy);
                if (Walkable((int)(n.Gx + dx), (int)n.Gy)) n.Gx += dx * 0.5f;
                if (Walkable((int)n.Gx, (int)(n.Gy + dy))) n.Gy += dy * 0.5f;
            }
        }

        // 中间商撮合：威信最高者做枢纽 → 核心-边缘
        var brokers = new List<Npc>();
        var alive = new List<Npc>();
        foreach (var n in Npcs) if (n.Alive) alive.Add(n);
        for (int x = 0; x < W; x += 6)
            for (int y = 0; y < H; y += 6)
            {
                Npc best = null; float bestScore = -1;
                foreach (var n in alive)
                {
                    if (Math.Abs(n.Gx - x) > 3 || Math.Abs(n.Gy - y) > 3) continue;
                    float s = n.Favor * 2 - n.Grudge + n.Realm;
                    if (s > bestScore) { bestScore = s; best = n; }
                }
                if (best != null) brokers.Add(best);
            }

        brokers.Sort((u, v) => (v.Favor + v.Deals).CompareTo(u.Favor + u.Deals));
        if (brokers.Count > 2) brokers.RemoveRange(2, brokers.Count - 2);

        foreach (var b in brokers)
            foreach (var n in alive)
            {
                if (n == b) continue;
                if (Math.Abs(n.Gx - b.Gx) > 3 || Math.Abs(n.Gy - b.Gy) > 3) continue;
                if (_rng.NextDouble() > 0.45) continue;
                if (_rng.NextDouble() < 0.70) { b.Favor += 3; n.Favor += 1; }   // 枢纽吃差价
                else { b.Favor += 1; n.Favor += 1; }
                b.Deals += 2; n.Deals += 1;
                Favors++;
                b.Karma++; n.Karma++;
                Acceptance["粟"] = Math.Min(1f, Acceptance["粟"] + 0.004f);
            }

        for (int i = 0; i < Npcs.Count; i++)
        {
            var a = Npcs[i]; if (!a.Alive) continue;
            for (int j = i + 1; j < Npcs.Count; j++)
            {
                var b = Npcs[j]; if (!b.Alive) continue;
                float dx = a.Gx - b.Gx, dy = a.Gy - b.Gy;
                if (dx * dx + dy * dy > 1.3f) continue;
                if (_rng.NextDouble() > 0.28) continue;

                if (a.Realm - b.Realm >= 2 && _rng.NextDouble() < 0.6)
                {
                    b.Grudge++; a.Karma += 2; b.Karma -= 3; Grudges++;
                    if (Log.Count < 8000 && _rng.NextDouble() < 0.25)
                        AddLog($"Y{Year} {a.Name}({a.Realm}境) 夺 {b.Name} 之资，{b.Name} 怀恨");
                }
                else if (Math.Abs(a.Realm - b.Realm) <= 1 && _rng.NextDouble() < 0.55)
                {
                    a.Favor++; b.Favor++; a.Karma++; b.Karma++; Favors++;
                }
            }
        }
    }

    // -------------------------------------------------------------- 家庭
    private void StepFamilies()
    {
        if (Year < _nextFamilyYear) return;
        _nextFamilyYear = Year + R(4, 9);

        var singles = new List<Npc>();
        foreach (var n in Npcs) if (n.Alive && n.Age >= 18 * 12 && n.Age <= 40 * 12) singles.Add(n);
        for (int i = 0; i + 1 < singles.Count; i += 2)
        {
            if (_rng.NextDouble() > 0.35) continue;
            singles[i].Favor++; singles[i + 1].Favor++;
        }
    }

    // -------------------------------------------------------------- 组织 → 帝国 → 崩解
    private void StepFactions()
    {
        if (Year >= _nextFactionYear && Factions.Count < 8)
        {
            _nextFactionYear = Year + R(9, 22);
            Npc founder = null; float best = 0;
            foreach (var n in Npcs)
                if (n.Alive && n.Faction < 0 && n.Favor + n.Realm > best) { best = n.Favor + n.Realm; founder = n; }

            if (founder != null && founder.Favor >= 2)
            {
                var f = new Faction { Id = Factions.Count, FoundedYear = Year, Power = R(3, 10), Kind = OrgKind.Tribe };
                f.Name = founder.Name + "氏" + OrgNames.Kind[0];
                foreach (var n in Npcs)
                    if (n.Alive && n.Faction < 0 && _rng.NextDouble() < 0.35) { n.Faction = f.Id; f.Members.Add(n.Id); }
                if (f.Members.Count >= 3)
                {
                    Factions.Add(f);
                    AddLog($"Y{Year} {f.Name} 立：个体→群体，资源互换的稳定化");
                }
            }
        }

        foreach (var f in Factions)
        {
            if (!f.Alive) continue;
            f.Power += R(0, 2) + f.Members.Count / 4;

            if (f.Power > 22 && f.Kind == OrgKind.Tribe) { f.Kind = OrgKind.Merchant; f.Name = Rename(f); }
            if (f.Power > 40 && f.Kind == OrgKind.Merchant) { f.Kind = OrgKind.City; f.Name = Rename(f); }
            if (f.Power > 68 && f.Kind == OrgKind.City) { f.Kind = OrgKind.Sect; f.Name = Rename(f); }
            if (f.Power > 100 && f.Kind == OrgKind.Sect) { f.Kind = OrgKind.Empire; f.Name = Rename(f); }

            if (f.Kind is OrgKind.Empire or OrgKind.Sect)
            {
                if (f.ArrayHealth > 0.05f && f.Power > 110 && _rng.NextDouble() < 0.25)
                {
                    f.TeleportNodes++;
                    f.ArrayHealth -= 0.02f;
                    if (_rng.NextDouble() < 0.4)
                        AddLog($"Y{Year} {f.Name} 建传送阵，控制半径扩至 {f.TeleportNodes} 节点");
                }
                f.ArrayHealth -= 0.012f;
                if (f.ArrayHealth <= 0f)
                {
                    f.Alive = false; Splits++;
                    AddLog($"Y{Year} {f.Name} 传送阵失修，控制半径崩塌，帝国解体");
                    foreach (var n in Npcs) if (n.Alive && n.Faction == f.Id) n.Faction = -1;
                }
            }
        }
    }

    private string Rename(Faction f)
    {
        string head = f.Name.Contains('氏') ? f.Name.Split('氏')[0] : f.Name;
        return head + "氏" + OrgNames.Kind[(int)f.Kind];
    }

    // -------------------------------------------------------------- 双层庇护链
    private void StepFeudal()
    {
        float titheSum = 0; int aliveSects = 0;

        foreach (var nat in Nations)
        {
            if (!nat.Alive) continue;
            nat.Yield = nat.Pop * 0.31f;
            var sect = nat.SectId >= 0 && nat.SectId < Sects.Count ? Sects[nat.SectId] : null;

            int clanCount = 0;
            foreach (var c in Clans) if (c.Alive && c.NationId == nat.Id) clanCount++;
            if (clanCount == 0) clanCount = 1;

            foreach (var c in Clans)
            {
                if (!c.Alive || c.NationId != nat.Id) continue;
                float flow = nat.Yield / clanCount * 0.34f;      // 家族年入（流量）
                c.Wealth += flow - flow * 0.10f;                  // 族兵、祠堂、修渠的固定消耗

                if (c.SectId >= 0 && c.SectId < Sects.Count && Sects[c.SectId].Alive)
                {
                    var s2 = Sects[c.SectId];
                    float tithe = flow * s2.Tithe;                 // 按年入抽成
                    c.Wealth -= tithe * 0.55f;
                    s2.Treasury += tithe * 0.82f;
                }

                c.Spirit += Rf() * 0.017f + (nat.Pop > 30000 ? 0.007f : 0f);
                if (c.Spirit >= 1f && c.SectId >= 0 && c.SectId < Sects.Count)
                {
                    var s3 = Sects[c.SectId];
                    if (s3.Alive)
                    {
                        s3.Disciples++; c.SentTalents++; TalentsSent++; c.Spirit = 0f;
                        if (_rng.NextDouble() < 0.18)
                            AddLog($"Y{Year} {c.Name} 送资质子弟入 {s3.Name} 外门，求一纸认可");
                    }
                }
            }

            nat.Tithe = sect != null && sect.Alive ? sect.Tithe : 0f;
            nat.Loyalty = sect != null && sect.Alive
                ? Math.Clamp(1.35f - sect.Tithe * 2.2f - (1f - sect.Protection) * 0.6f, 0f, 1f)
                : 0f;

            if (sect != null && sect.Protection < 0.35f && _rng.NextDouble() < 0.16 / 12.0)   // 年劫掠率，按月摊薄
            {
                nat.Raids++; RaidCount++; nat.Pop *= 0.958f;
                if (_rng.NextDouble() < 0.12 / 12.0) AddLog($"Y{Year} {nat.Name} 护持不至，野修劫掠");
            }
            else
            {
                float r = 0.013f / 12.0f;   // 年增长率 1.3%，按月摊薄
                nat.Pop += nat.Pop * r * (1f - Math.Min(1.3f, nat.Pop / nat.K));
            }

            if (nat.Pop < 9000f) { nat.Alive = false; AddLog($"Y{Year} {nat.Name} 亡：宗门外护不至，凡俗先崩"); }
        }

        int seenSectCount = Sects.Count;   // 分裂会新增宗门：本轮只遍历原有，避免边遍历边改集合
        for (int si = 0; si < seenSectCount; si++)
        {
            var sec = Sects[si];
            if (!sec.Alive) continue;
            aliveSects++;
            float protectedPop = 0f;
            foreach (var nn in Nations) if (nn.Alive && nn.SectId == sec.Id) protectedPop += nn.Pop;
            foreach (var cc in Clans) if (cc.Alive && cc.SectId == sec.Id) protectedPop += 6000f;
            float upkeep = 6f + sec.Tier * 9f + sec.Disciples * 1.6f + protectedPop * 0.0125f * (0.40f + sec.Protection);
            sec.Treasury -= upkeep;
            float protTarget = Math.Clamp(sec.Treasury / (2200f * (1f + 0.35f * (sec.Tier - 1))), 0f, 1f);
            sec.Protection += (protTarget - sec.Protection) * 0.05f;

            if (sec.Protection < 0.55f && sec.Treasury > 400f)
            {
                sec.Treasury -= 260f;
                sec.Protection = Math.Min(1f, sec.Protection + 0.055f);   // 扩编护山弟子、修阵
            }

            // 中小宗门向上纳贡（五层链的第二次抽取）
            if (sec.ParentId >= 0 && sec.ParentId < Sects.Count && Sects[sec.ParentId].Alive)
            {
                var par = Sects[sec.ParentId];
                float up = Math.Max(0f, sec.Treasury) * 0.30f;
                sec.Treasury -= up;
                par.Treasury += up * 0.85f;
                if (_rng.NextDouble() < 0.35 / 12.0) par.Disciples += 1;   // 资质子弟上送（按月摊薄）
            }

            if (_rng.NextDouble() < 0.0065 / 12.0)
            {
                sec.Treasury *= 0.30f;
                sec.Protection *= 0.22f;          // 护持力当场击穿，之后逐年修复
                sec.Disciples = sec.Disciples / 2;
                sec.PatriarchRealm = Math.Max(1f, sec.PatriarchRealm - 0.45f);
                AddLog($"Y{Year} {sec.Name} 遭大劫：灵脉枯竭、藏经阁失火，护持力骤降");
            }

            if (sec.Treasury < -60f && sec.Disciples <= 0 && sec.Protection < 0.05f && sec.Tier <= 2)
            {
                sec.Alive = false;
                AddLog($"Y{Year} {sec.Name} 覆灭：无弟子、无香火、无护持，道统断绝");
                foreach (var c in Clans) if (c.Alive && c.SectId == sec.Id) c.SectId = -1;
                continue;
            }

            if (sec.Treasury < 0) sec.Tithe = Math.Min(0.45f, sec.Tithe + 0.008f);
            else if (sec.Protection > 0.80f) sec.Tithe = Math.Max(0.15f, sec.Tithe - 0.004f);

            sec.PatriarchRealm += sec.Protection > 0.5f ? 0.0025f : -0.0012f;
            sec.BestBranchRealm += 0.008f + sec.Disciples * 0.0035f;
            titheSum += sec.Tithe;

            Clan host = null;
            foreach (var c in Clans) if (c.Alive && c.SectId == sec.Id) { host = c; break; }

            if (host != null && sec.Tier >= 1 && sec.BestBranchRealm >= sec.PatriarchRealm - 1.5f
                && sec.Tithe > 0.28f && sec.Disciples >= 3 && _rng.NextDouble() < 0.045)
            {
                var child = new Sect
                {
                    Id = Sects.Count,
                    Name = new[] { "青篁宗", "落霞宗", "抱犊宗", "听雨宗" }[R(0, 3)],
                    Tier = 1, Tithe = 0.20f, Treasury = 25f, Protection = 0.3f,
                    PatriarchRealm = sec.BestBranchRealm,
                    Gx = Math.Clamp(sec.Gx + R(-3, 3), 0, W - 1),
                    Gy = Math.Clamp(sec.Gy + R(-3, 3), 0, H - 1),
                    FoundedYear = Year, SplitFrom = sec.Id
                };
                Sects.Add(child); Splits++;
                host.SectId = child.Id;      // 支脉带走一族，才有税基
                AddLog($"Y{Year} {child.Name} 自 {sec.Name} 分裂：支脉追平宗主，庇护费过苛");
                sec.BestBranchRealm = 0f; sec.Treasury *= 0.7f;
            }
        }
        TitheAvg = aliveSects > 0 ? titheSum / aliveSects : 0f;
    }

    // -------------------------------------------------------------- 洞府 / 阵法
    private void StepCaves()
    {
        if (Year >= _nextCaveYear)
        {
            _nextCaveYear = Year + R(12, 26);
            Npc claimant = null; float best = 0;
            foreach (var n in Npcs)
                if (n.Alive && n.CaveId < 0 && n.Realm >= 2 && n.Realm > best) { best = n.Realm; claimant = n; }

            if (claimant != null)
            {
                int want = 1 + claimant.Realm / 4;   // 洞府数 = f(境界)；多占则维护超线性
                foreach (var cv in Caves)
                {
                    if (want <= 0) break;
                    if (cv.OwnerId >= 0) continue;
                    cv.OwnerId = claimant.Id;
                    claimant.CaveId = cv.Id;
                    claimant.TimeScale = cv.TimeScale;
                    want--;
                    AddLog($"Y{Year} {claimant.Name} 据洞府（灵气 {cv.Qi:0.00}，内时流速 {cv.TimeScale:0}×）");
                }
            }
        }

        foreach (var cv in Caves)
        {
            if (cv.OwnerId < 0) continue;
            Npc owner = null;
            foreach (var n in Npcs) if (n.Id == cv.OwnerId) { owner = n; break; }
            if (owner == null || !owner.Alive) { cv.OwnerId = -1; continue; }

            float budget = owner.Realm * 2f + Math.Max(0f, owner.Karma) * 0.4f;
            if (budget < cv.Upkeep)
            {
                cv.Qi -= 0.004f;
                if (cv.Qi < 0.25f && _rng.NextDouble() < 0.30)
                {
                    cv.Visible = true; cv.Collide = false;
                    AddLog($"Y{Year} {owner.Name} 洞府阵纹退化：遮蔽失效（看得见、可穿行）");
                }
            }
            else cv.Qi = Math.Min(1.2f, cv.Qi + 0.003f);

            if (_rng.NextDouble() < 0.02 / 12.0)
            {
                bool vis = _rng.NextDouble() < 0.55;
                bool col = _rng.NextDouble() < 0.75;
                if (vis != cv.Visible || col != cv.Collide)
                {
                    cv.Visible = vis; cv.Collide = col;
                    AddLog($"Y{Year} 洞府重布阵法：{(vis ? "可见" : "不可见")} / {(col ? "实阵撞墙" : "虚阵可穿")}");
                }
            }
        }
    }

    // -------------------------------------------------------------- 功法改良（谱系）
    private void StepTechniques()
    {
        if (Year < _nextTechYear) return;
        _nextTechYear = Year + R(14, 28);

        Npc founder = null; float best = 0;
        foreach (var n in Npcs) if (n.Alive && n.Realm >= 4 && n.Realm > best) { best = n.Realm; founder = n; }
        if (founder == null) return;

        var root = Techs[R(0, Techs.Count - 1)];
        var t = new Technique
        {
            Id = Techs.Count, Name = root.Name + new[] { "·改", "·新解", "·残卷", "·别传" }[R(0, 3)],
            RootId = root.Id, FounderId = founder.Id, FoundYear = Year,
            Element = _rng.NextDouble() < 0.72 ? root.Element : R(0, 9),
            Form = _rng.NextDouble() < 0.70 ? root.Form : R(0, 4),
            Power = root.Power + 0.03f + Rf() * 0.10f,
            Risk = root.Risk + 0.02f + Rf() * 0.05f,
            Improve = root.Improve + 1
        };
        Techs.Add(t);
        founder.TechId = t.Id;
        AddLog($"Y{Year} {founder.Name} 改良《{root.Name}》→《{t.Name}》：威能 +{(t.Power - root.Power):0.00}，反噬 {t.Risk:P0}");
    }

    // -------------------------------------------------------------- 货币
    private void StepCurrency()
    {
        Acceptance["粟"] = Math.Min(0.86f, Acceptance["粟"] + (Favors > 40 ? 0.0014f : 0.0004f));
        Acceptance["铁"] = Math.Min(0.80f, Acceptance["铁"] + (Factions.Count > 2 ? 0.0012f : 0.0005f));

        // 灵石接受度由修士经济规模驱动（灵脉越集中，挤出越快）
        int rich = 0;
        foreach (var n in Npcs) if (n.Alive && n.Realm >= 3) rich++;
        Acceptance["灵石"] = Math.Min(1.40f, Acceptance["灵石"] + 0.0016f + rich * 0.0035f);

        string bestWord = "无"; float bestAcc = 0.5f;
        foreach (var kv in Acceptance)
            if (kv.Value > bestAcc) { bestAcc = kv.Value; bestWord = kv.Key; }

        if (Acceptance["灵石"] > Acceptance["粟"] * 1.25f && Acceptance["灵石"] > Acceptance["铁"] * 1.25f && rich > 6)
            bestWord = "灵石";

        if (bestWord != "无" && bestWord != CurrencyWord)
        {
            CurrencyWord = bestWord;
            AddLog($"Y{Year} 众所共认：{bestWord} 为一般等价物（接受度 {Acceptance[bestWord]:P0}）");
        }
    }

    private void StepAggregate()
    {
        if (Year < _nextBirthYear) return;
        _nextBirthYear = Year + R(6, 14);
        if (AliveCount < 30 && AvgQi >= 0.45f) for (int i = 0; i < 4; i++) Spawn();
    }

    // -------------------------------------------------------------- 出生 / 转世
    private void Spawn(Npc soul = null)
    {
        int gx, gy, guard = 0;
        do { gx = R(1, W - 2); gy = R(1, H - 2); } while (!Walkable(gx, gy) && ++guard < 60);

        int goodBirth = _rng.NextDouble() < 0.30 ? 1 : 0;
        int highTalent = _rng.NextDouble() < 0.15 ? 1 : 0;
        int quad = (goodBirth == 1 ? 0 : 2) + (highTalent == 1 ? 0 : 1);
        Quads[quad]++;

        int startRealm = quad switch
        {
            0 => R(2, 4),
            2 => R(1, 3),
            1 => R(0, 1),
            _ => 0
        };

        int inherited = KarmaLedger > 60 ? R(0, 3) : KarmaLedger > 20 ? R(0, 1) : 0;

        var n = new Npc
        {
            Id = soul?.Id ?? NextId(),
            Name = soul?.Name ?? Surnames[R(0, Surnames.Length - 1)] + Givens[R(0, Givens.Length - 1)],
            Lives = soul != null ? soul.Lives + 1 : 1,
            Gx = gx, Gy = gy, Tgx = gx, Tgy = gy,
            Height = 0.82f + Rf() * 0.36f,
            Girth = 0.85f + Rf() * 0.40f,
            Hue = Rf(),
            EyeGap = 0.85f + Rf() * 0.30f,
            JawW = 0.80f + Rf() * 0.40f,
            BrowH = 0.85f + Rf() * 0.30f,
            BrowAngle = Rf() * 0.5f - 0.25f,
            WearMask = 0x7F,
            Goal = soul?.Goal ?? _rngImp.Next(0, 6),
            Gender = soul?.Gender ?? (_rng.NextDouble() < 0.5 ? 0 : 1),
            AccHue = Rf(),
            AccessoryKind = R(0, 3),
            Realm = startRealm,
            Age = R(16, 40) * 12,
            Lifespan = (58 + R(0, 34) + startRealm * 9) * 12,   // 月龄
            Quad = quad,
            InheritedHelp = inherited,
            Favor = inherited,
            Karma = inherited * 2
        };
        if (n.Realm >= 3) n.Dao = DaoWords[R(0, DaoWords.Length - 1)] + "真人";

        if (goodBirth == 1 && Clans.Count > 0)
        {
            var c = Clans[R(0, Clans.Count - 1)];
            if (c.Alive) { n.ClanId = c.Id; n.SectId = c.SectId; }
        }

        if (soul == null) { Npcs.Add(n); return; }

        soul.Name = n.Name; soul.Lives = n.Lives; soul.Gx = n.Gx; soul.Gy = n.Gy;
        soul.Tgx = n.Tgx; soul.Tgy = n.Tgy; soul.Height = n.Height; soul.Girth = n.Girth;
        soul.Hue = n.Hue; soul.Realm = n.Realm; soul.Age = n.Age; soul.Lifespan = n.Lifespan;
        soul.Quad = n.Quad; soul.InheritedHelp = n.InheritedHelp;
        soul.Favor = n.Favor; soul.Grudge = 0; soul.Karma = n.Karma;
        soul.Alive = true; soul.DeathYear = -1; soul.Verdict = "";
        soul.SectId = n.SectId; soul.ClanId = n.ClanId;
        soul.CaveId = -1; soul.Faction = -1; soul.TimeScale = 1f;
    }

    // -------------------------------------------------------------- 存档 / 哈希
    /// <summary>确定性哈希：同种子两次运行必须一致（联机与回放的地基）。</summary>
    public ulong Hash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }

        Mix((ulong)Year); Mix((ulong)Npcs.Count); Mix((ulong)Nations.Count);
        foreach (var n in Npcs)
        {
            Mix((ulong)n.Id); Mix((ulong)(n.Gx * 100)); Mix((ulong)(n.Gy * 100));
            Mix((ulong)n.Realm); Mix((ulong)n.Favor); Mix((ulong)n.Grudge);
        }
        foreach (var s in Sects) { Mix((ulong)s.Id); Mix((ulong)(s.Tithe * 1000)); Mix((ulong)(s.Treasury * 10)); }
        foreach (var c in Caves) { Mix((ulong)c.Id); Mix((ulong)(c.Qi * 100)); Mix(c.Visible ? 1UL : 0UL); Mix(c.Collide ? 1UL : 0UL); }
        return h;
    }

    /// <summary>存档：纯文本、可 diff、可校验，面向将来服务端复用。</summary>
    public string Serialize()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"v1 seed={Seed} year={Year} hash={Hash()}");
        sb.AppendLine($"currency={CurrencyWord} pop={TotalPopulation:0} grudges={Grudges} favors={Favors} splits={Splits}");
        foreach (var nt in Nations) if (nt.Alive) sb.AppendLine($"natio {nt.Id} {nt.Name} pop={nt.Pop:0} k={nt.Tithe:0.000} loy={nt.Loyalty:0.000}");
        foreach (var s in Sects) if (s.Alive) sb.AppendLine($"sect {s.Id} {s.Name} t{s.Tier} k={s.Tithe:0.000} tr={s.Treasury:0} prot={s.Protection:0.000}");
        foreach (var c in Clans) if (c.Alive) sb.AppendLine($"clan {c.Id} {c.Name} n{c.NationId} s{c.SectId} w={c.Wealth:0}");
        foreach (var cv in Caves) sb.AppendLine($"cave {cv.Id} own={cv.OwnerId} qi={cv.Qi:0.000} vis={cv.Visible} col={cv.Collide} ts={cv.TimeScale:0}");
        foreach (var t in Techs) sb.AppendLine($"tech {t.Id} {t.Name} root={t.RootId} el={t.Element} form={t.Form} p={t.Power:0.00} r={t.Risk:0.00}");
        return sb.ToString();
    }

    public void AddLog(string text)
    {
        if (Log.Count > 9000) Log.RemoveAt(0);
        Log.Add(new Chronicle { Year = Year, Text = text });
    }
}
