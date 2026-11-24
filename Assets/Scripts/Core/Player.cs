using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>玩家一世的状态。全部字段都在世界状态里，可存档、可重放。</summary>
public sealed class PlayerLife
{
    public int NpcId = -1;
    public int Lives = 0;
    public string Origin = "";        // 出身：寒门 / 族中子弟 / 皇室旁支 / 荒野弃婴
    public string TalentText = "";    // 天赋描述
    public string Region = "";        // 出生地：秩序带 / 荒野 / 山隅 / 海隅
    public string Era = "洪荒·早期";  // 这一世开局所处的纪元形态
    public int Quad;                  // 出身 × 天赋 四象限
    public float TalentScore = 1f;
    public int Years;                 // 已过月数（真实时间流速：12 个月 = 1 岁）
    public int ActedYear = -1;        // 上次行动的年（行动冷却：一月一事）
    public int ActedMonth = -1;       // 上次行动的月
    public int Grain, Stone;          // 粟 / 灵石
    public bool Dead;
    public string Verdict = "";
    public int Help, Refuse, Rob;
    public int MoveCredit;
    public string LastEvent = "投生于此。";
    public readonly List<string> Journal = new();
    public readonly List<int> Debtors = new();   // 你欠了谁：来世他就在你身边
    public readonly List<int> Bag = new();       // 行囊里的法宝/丹药
    public string Fate = "";          // 死后去向（树/草/狗/人……）
    public BeingKind Kind = BeingKind.Human;
    public Grade Grade = Grade.Fan;
    public int NodeId = -1;           // 你开辟/继承的小世界
    public float Reputation;          // 声望：众人对你的总评（正面好走路，负面成靶子）
    public int Gifts, Ambushed;       // 受助 / 被杀人夺宝 次数
    public string Killer = "";        // 杀你的是谁
}

/// <summary>
/// 可玩层：把世界内核包成"一世"的循环。
/// 所有改变都走 <see cref="Act"/> —— 单机与将来的服务器共用同一条命令通道，
/// 命令 + 种子 + 世界时间 = 可重放状态（这就是保留的联机接口）。
/// </summary>
public sealed partial class World
{
    public PlayerLife Player = new();

    private static readonly string[] Regions = { "宗门辖地", "荒野", "山隅", "海隅" };
    private static readonly string[] Fates = { "人", "人", "人", "树", "草", "狗", "鹰", "鱼" };

    public bool InPlay => Player.NpcId >= 0 && !Player.Dead;

    public Npc PlayerNpc => Player.NpcId > 0
        ? Npcs.Find(n => n.Id == Player.NpcId)
        : null;

    /// <summary>投生：抽身世与天赋，落在未被组织犁过的"洪荒早期"节点上。</summary>
    public PlayerLife BeginLife(int? forceQuad = null)
    {
        int prevLives = Player?.Lives ?? 0;
        int quad = forceQuad ?? (_rng.NextDouble() < 0.55
            ? (_rng.NextDouble() < 0.5 ? 0 : 1)
            : (_rng.NextDouble() < 0.5 ? 2 : 3));

        // 每一世的开局都落在洪荒早期：先找未被人烟犁过的干净地
        var cell = FindPrimordialCell();
        int gx = cell.X, gy = cell.Y;

        // 没有相欠，何来相见：前世欠下的人，这一世就生在他近旁
        if (Player.Debtors.Count > 0)
        {
            var debtor = Npcs.Find(n => n.Alive && !n.IsPlayer && Player.Debtors.Contains(n.Id));
            if (debtor != null)
            {
                gx = Math.Clamp((int)debtor.Gx + R(-2, 2), 1, W - 2);
                gy = Math.Clamp((int)debtor.Gy + R(-2, 2), 1, H - 2);
            }
        }

        BeingKind kind = Cultivation.KindOfFate(Player?.Fate ?? "人");
        string origin = kind switch
        {
            BeingKind.Plant => "荒野草木·承露而生",
            BeingKind.Beast => "山野之兽·开智未久",
            _ => quad switch
            {
                0 => "族中子弟·天资过人",
                1 => "族中子弟·资质平平",
                2 => "寒门出身·天资过人",
                _ => "寒门出身·资质平平"
            }
        };
        string talent = quad is 0 or 2 ? "天资过人" : "资质平平";
        string region = kind == BeingKind.Plant ? "荒野" : Regions[R(0, 3)];
        if (kind == BeingKind.Human && quad is 0 or 1 && Clans.Count > 0) region = "宗门辖地";

        int startRealm = kind == BeingKind.Human
            ? quad switch { 0 => R(1, 3), 2 => R(0, 2), 1 => 0, _ => 0 }
            : R(0, 1);

        Grade grade = Grade.Fan;
        var npc = new Npc
        {
            Id = NextId(),
            Name = Surnames[R(0, Surnames.Length - 1)] + Givens[R(0, Givens.Length - 1)],
            Lives = prevLives + 1,
            Gx = gx, Gy = gy, Tgx = gx, Tgy = gy,
            Height = 0.90f + Rf() * 0.22f,
            Girth = 0.88f + Rf() * 0.26f,
            Hue = Rf(),
            EyeGap = 0.90f + Rf() * 0.20f,
            JawW = 0.85f + Rf() * 0.28f,
            BrowH = 0.90f + Rf() * 0.20f,
            BrowAngle = Rf() * 0.3f - 0.15f,
            WearMask = 0x7F,
            AccHue = Rf(),
            AccessoryKind = R(0, 3),
            Kind = kind,
            Grade = grade,
            Realm = startRealm,
            Age = R(14, 18) * 12,   // 月龄
            Quad = quad,
            IsPlayer = true,
            Favor = 0,
            Karma = (int)(KarmaLedger * 0.1f)
        };
        npc.Lifespan = Cultivation.Lifespan(kind, startRealm, grade);
        npc.Prog = 0.02f;
        Npcs.Add(npc);

        var pl = new PlayerLife
        {
            NpcId = npc.Id,
            Lives = prevLives + 1,
            Origin = origin,
            TalentText = talent,
            Region = region,
            Era = EraAt(gx, gy),
            Quad = quad,
            Kind = kind,
            Grade = grade,
            TalentScore = quad is 0 or 2 ? 1.25f : 1f,
            Years = 0,
            Grain = kind == BeingKind.Human ? (quad is 0 or 1 ? 30 : 8) : 0,
            Stone = kind == BeingKind.Human && quad is 0 or 1 ? 2 : 0,
            LastEvent = $"{pl_text(region, origin, kind)}",
            ActedYear = Year, ActedMonth = Month   // 出生当月即已"行过一事"，先等月流转
        };
        pl.Journal.Add($"Y{Year}　{npc.Name} 生于{region}（{pl.Era}），{origin}");
        Player = pl;
        Player.Dead = false;
        Player.Verdict = "";
        AddLog($"Y{Year} 你投入此世：{npc.Name} · {region} · {pl.Era}");
        return pl;
    }

    private static string pl_text(string region, string origin, BeingKind kind)
        => kind == BeingKind.Human ? $"{region}生人：{origin}。" : $"{origin}：这一世，你不是人。";

    /// <summary>
    /// 唯一的状态变更入口。返回值是要显示给玩家的一句话；将来服务器把同一串命令
    /// 喂给同一份世界即可得到完全相同的哈希（联机接口的地基）。
    /// </summary>
    public string Act(string verb, int arg = 0)
    {
        if (Player.Dead)
        {
            if (verb == "reincarnate") { BeginLife(); return Player.LastEvent; }
            return "已死。先投生。";
        }
        var npc = PlayerNpc;
        if (npc == null || !npc.Alive) return "身已殒。";

        // 体系限制：草木扎根，走不了、入不了宗门；兽类不通人伦香火
        if (!Cultivation.CanAct(npc.Kind, verb))
            return npc.Kind == BeingKind.Plant ? "草木之身，离不得脚下这一寸土。" : "兽身不通人间礼法，此事做不得。";

        // 行动冷却：一次行动 = 一个月。月内再点只提示，不白扣寿元/粮。
        bool spent = Year < Player.ActedYear ||
                     (Year == Player.ActedYear && Month <= Player.ActedMonth);
        if (spent && verb != "wait" && verb != "move")
            return "本月已行一事，待月流转。";
        if (!spent && verb != "move")
        {
            Player.ActedYear = Year;
            Player.ActedMonth = Month;
        }

        string Out(string s)
        {
            Player.LastEvent = s;
            Player.Journal.Add($"Y{Year}.{Month + 1}　{s}");
            if (Player.Journal.Count > 60) Player.Journal.RemoveAt(0);
            return s;
        }

        switch (verb)
        {
            case "move":
            {
                if (!Cultivation.CanMove(npc.Kind)) return Out("根扎在此，动不了。");
                int dx = arg == 0 ? -1 : arg == 1 ? 1 : 0;
                int dy = arg == 2 ? -1 : arg == 3 ? 1 : 0;
                int tx = (int)npc.Gx + dx, ty = (int)npc.Gy + dy;
                if (!Walkable(tx, ty)) return Out("前方是深水或断崖，过不去。");
                npc.Gx = tx; npc.Gy = ty; npc.Tgx = tx; npc.Tgy = ty;
                // 行走不耗月：实时探索世界，寿元由世界时钟（StepPlayerLife）结算
                return Out($"行至({tx},{ty})。");
            }

            case "meditate":
                return Out(Meditate(npc));

            case "help":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                t.Favor += 2; npc.Karma += 2; Player.Help++;
                // 亲见一手：他自己受了你的恩，权重最重；邻里听说的只是转述
                Witness(t, npc, 18f);
                SpreadRumor(t, 6f, 2);
                Player.Reputation += 1.6f;
                Acceptance["粟"] = Math.Min(0.86f, Acceptance["粟"] + 0.001f);
                AdvanceLife();
                return Out($"你帮了 {t.Name}。他记下这份恩。");
            }

            case "refuse":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                t.Grudge += 3; npc.Karma -= 2; Player.Refuse++; Grudges++;
                Witness(t, npc, -11f);
                SpreadRumor(t, -4f, 2);
                Player.Reputation -= 0.9f;
                AdvanceLife();
                return Out($"你没有帮 {t.Name}。他记下这笔怨。");
            }

            case "rob":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                // 品阶与体系一起算战力：神品的凡体也能压人
                float my = (npc.Realm + 1) * Grades.PowerMul(npc.Grade);
                float his = (t.Realm + 1) * Grades.PowerMul(t.Grade);
                if (his > my * 1.15f)
                {
                    npc.Realm = Math.Max(0, npc.Realm - 1);
                    Witness(t, npc, -25f);
                    AdvanceLife();
                    return Out($"夺 {t.Name} 不成，反被所伤。");
                }
                Player.Stone += 2 + t.Realm;
                t.Grudge += 4; npc.Karma -= 4; Player.Rob++;
                Witness(t, npc, -26f);
                SpreadRumor(t, -10f, 3);
                Player.Reputation -= 2.6f;
                AdvanceLife();
                return Out($"夺了 {t.Name} 的灵石。他恨你入骨。");
            }

            case "cave":
            {
                if (npc.Realm < 2) return Out("境界不足，洞府不认你。");
                var cv = NearestCave(npc, 6f);
                if (cv == null) return Out("左近没有无主洞府。");
                cv.OwnerId = npc.Id; npc.CaveId = cv.Id; npc.TimeScale = cv.TimeScale;
                if (cv.WorldNodeId < 0)
                {
                    var node = OpenWorld(npc, TierFor(npc.Realm, Player.Stone, false), null, cv.TimeScale);
                    if (node != null) { cv.WorldNodeId = node.Id; Player.NodeId = node.Id; }
                }
                AdvanceLife();
                return Out($"据洞府而居：灵气 {cv.Qi:0.00}，内时 {cv.TimeScale:0}× —— 修炼快，老得也快。");
            }

            case "sect":
            {
                var sc = NearestSect(npc, 7f);
                if (sc == null) return Out("左近并无山门。");
                npc.SectId = sc.Id; Player.Stone += 1;
                AdvanceLife();
                return Out($"入 {sc.Name} 为外门弟子。抽成 {sc.Tithe:P0}，护持看人脸色。");
            }

            case "open":
            {
                // 开辟一方世界：面积由境界与灵石决定；流速默认按体量，也可事后用「改流速」付代价调
                int tier = TierFor(npc.Realm, Player.Stone, false);
                var node = OpenWorld(npc, tier, $"{npc.Name}之{(tier <= 1 ? "洞府" : "洞天")}");
                if (node == null) return Out("开辟不成。");
                npc.TimeScale = node.TimeScale;
                Player.NodeId = node.Id;
                Player.Grade = npc.Grade;
                AdvanceLife();
                return Out($"开辟「{node.Name}」：{node.AreaKm2:0.##} km²（{Tiers[tier].Name}），内时 {node.TimeScale:0}× —— 折寿 {node.CostMonths / 12:0.#} 年，不稳 {node.Instability:P0}。");
            }

            case "flow":
            {
                // 自定义小世界的时间流速：这是「神之权柄」，代价必须付
                var node = Player.NodeId >= 0 && Player.NodeId < Worlds.Count ? Worlds[Player.NodeId] : null;
                if (node == null)
                    foreach (var w in Worlds) if (w.OwnerId == npc.Id && !w.Collapsed) { node = w; break; }
                if (node == null) return Out("你还没有自己的小世界。先开辟一方。");
                float want = arg <= 0 ? node.TimeScale * 1.5f : arg;
                bool ok = SetFlow(node, npc, want, out string m);
                AdvanceLife();
                return Out(ok ? m : m);
            }

            case "gate":
            {
                int target = -1;
                foreach (var w in Worlds) if (w.OwnerId == npc.Id && w.GateTo >= 0 && !w.Collapsed) { target = w.GateTo; break; }
                if (target < 0)
                    foreach (var w in Worlds) if (w.GateTo >= 0 && !w.Collapsed) { target = w.GateTo; break; }
                if (target < 0) return Out("此界尚无门。");
                string r = Traverse(target, npc);
                AdvanceLife();
                return Out(r);
            }

            case "plant":
            {
                if (Player.Stone < 2) return Out("没有灵石买种子。");
                int x = (int)npc.Gx, y = (int)npc.Gy;
                if (x < 0 || y < 0 || x >= W || y >= H) return Out("此处无处落土。");
                Player.Stone -= 2;
                Veg[x, y] = Math.Min(Qi[x, y] * 1.05f, Veg[x, y] + 0.25f);
                Qi[x, y] = Math.Min(1.4f, Qi[x, y] + 0.03f);
                EnvPlanted++;
                Player.Reputation += 0.6f;
                AdvanceLife();
                return Out($"你在({x},{y})种下花木。地气回了 {Qi[x, y]:0.00}，人望 +0.6。");
            }

            case "delve":
            {
                int x = (int)npc.Gx, y = (int)npc.Gy;
                if (x < 0 || y < 0 || x >= W || y >= H) return Out("此处无处下手。");
                float before = Qi[x, y];
                Dmg[x, y] = Math.Min(1f, Dmg[x, y] + 0.22f);
                Qi[x, y] = Math.Max(0f, Qi[x, y] - 0.30f);
                Veg[x, y] = Math.Max(0f, Veg[x, y] - 0.60f);
                Player.Stone += 4;
                EnvDestroyed++;
                Player.Reputation -= 2.2f;
                foreach (var n in Npcs)
                    if (n.Alive && !n.IsPlayer && n.SectId == npc.SectId && npc.SectId >= 0)
                        Witness(n, npc, -8f);
                AdvanceLife();
                return Out($"开山采脉：得石四枚，({x},{y})灵气 {before:0.00}→{Qi[x, y]:0.00}，此地再难复原。");
            }

            case "ascend":
            {
                // 升阶：一生一次；材料 = 灵石与洞府灵气
                int material = Player.Stone / 3 + (npc.CaveId >= 0 ? 2 : 0);
                bool ok = Grades.Ascend(npc, material, out string m);
                if (ok)
                {
                    Player.Stone = Math.Max(0, Player.Stone - 4);
                    Player.Grade = npc.Grade;
                    AddLog($"Y{Year} 玩家 {npc.Name} 升品：{Grades.Of(npc.Grade)}");
                }
                AdvanceLife();
                return Out(m);
            }

            case "craft":
            {
                if (Player.Stone < 3) return Out("灵石不足，炼不了宝。");
                Player.Stone -= 3;
                // 材料品阶由灵石底蕴与洞府灵脉决定，炼制者品阶封顶
                Grade mat = npc.CaveId >= 0 && Caves[npc.CaveId].Qi > 0.8f
                    ? (npc.Grade >= Grade.Ling ? Grade.Ling : Grade.Fan)
                    : Grade.Fan;
                var t = CraftTreasure(npc, mat, TreasureKind.Artifact);
                if (t == null) return Out("炼宝失败，材料尽废。");
                Player.Bag.Add(t.Id);
                AdvanceLife();
                return Out($"炼成「{t.Name}」（{Grades.Of(t.Grade)}，品阶上限由你与材料共同封顶）。");
            }

            case "reforge":
            {
                bool ok = Grades.Reforge(npc, out string m);
                if (ok) { Player.Grade = npc.Grade; AddLog($"Y{Year} 玩家 {npc.Name} 分解重炼"); }
                AdvanceLife();
                return Out(m);
            }

            case "explore":
            {
                AdvanceLife();
                if (!Player.Dead) return Out(ExploreEvent(npc));
                return Player.Verdict;
            }

            case "wait":
            {
                AdvanceLife();
                if (Player.Dead) return Player.Verdict;
                Player.ActedYear = Year; Player.ActedMonth = Month;
                return $"又过一月（Y{Year}.{Month + 1}）。";
            }

            case "reincarnate":
                BeginLife();
                return Player.LastEvent;
        }
        return Out("无名之举。");
    }

    private string Meditate(Npc npc)
    {
        float speed = Math.Max(1f, Math.Min(npc.TimeScale, 20f));
        int cap = Cultivation.MaxRealm(npc.Kind);
        npc.Prog += 0.09f * speed * Player.TalentScore
                    * Cultivation.GainMul(npc.Kind, npc.Realm) * Grades.CultMul(npc.Grade);
        string msg = npc.Kind == BeingKind.Plant ? "扎根一月，叶脉里多了一丝灵气。"
                   : npc.Kind == BeingKind.Beast ? "伏于草间一月，灵智渐开。" : "打坐一月，气息略稳。";
        while (npc.Prog >= 1f && npc.Realm < cap)
        {
            npc.Prog -= 1f; npc.Realm++;
            npc.Lifespan = Math.Max(npc.Lifespan, Cultivation.Lifespan(npc.Kind, npc.Realm, npc.Grade));
            msg = $"突破：{Cultivation.RealmName(npc.Kind, npc.Realm)}（{npc.Realm} 境）。";
            // 神品天妒：每一次突破都可能招来劫数
            if (_rng.NextDouble() < Grades.TribulationRisk(npc.Grade))
            {
                if (Grades.Downgrade(npc, "天妒·劫数加身", out string dm))
                {
                    Player.Grade = npc.Grade;
                    msg += " " + dm;
                }
            }
        }
        AdvanceLife();
        if (Player.Dead) return Player.Verdict;
        return msg;
    }

    private string ExploreEvent(Npc npc)
    {
        double r = _rng.NextDouble();
        int cap = Cultivation.MaxRealm(npc.Kind);
        if (r < 0.16)
        {
            int bonus = R(2, 5);
            npc.Realm = Math.Min(cap, npc.Realm + bonus);
            npc.Lifespan = Math.Max(npc.Lifespan, Cultivation.Lifespan(npc.Kind, npc.Realm, npc.Grade));
            return $"秘境：内时流转不知几载，出关时已是 {Cultivation.RealmName(npc.Kind, npc.Realm)}（{npc.Realm} 境）。";
        }
        if (r < 0.34)
        {
            Player.Stone += R(1, 4); Player.Grain += R(10, 30);
            return "得一处灵矿残脉，拾得灵石与粮。";
        }
        if (r < 0.52 && Techs.Count > 0)
        {
            var t = Techs[R(0, Techs.Count - 1)];
            npc.TechId = t.Id;
            return $"偶得《{t.Name}》残卷（{Grades.Of(t.Grade)}），可参其形。";
        }
        if (r < 0.70)
        {
            if (npc.Realm > 0) npc.Realm--;
            Player.Grain = Math.Max(0, Player.Grain - 10);
            return "遇野修截杀，破财折修为，勉强脱身。";
        }
        if (r < 0.86)
        {
            var t = NearestNpc(npc, 3f);
            if (t != null) { t.Favor += 1; npc.Karma += 1; Witness(t, npc, 4f); return $"路遇 {t.Name}，同避一场风雨，结一善缘。"; }
            return "独行千里，无事。";
        }
        return "荒山无路，无功而返。";
    }

    private void AdvanceLife()
    {
        var npc = PlayerNpc;
        if (npc == null || !npc.Alive) return;

        Tick(1);
        Player.Grain = Math.Max(0, Player.Grain - 2);
        if (Player.Grain == 0 && npc.Kind == BeingKind.Human) npc.Lifespan -= 12;   // 饿则短寿（折 1 岁）

        // 修为反噬
        if (npc.TechId >= 0 && npc.TechId < Techs.Count && _rng.NextDouble() < Techs[npc.TechId].Risk * 0.4)
        {
            npc.Realm = Math.Max(0, npc.Realm - 1);
            Player.LastEvent = "功法反噬，修为倒退。";
        }

        // 被记恨者寻仇
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.Id == npc.Id || n.Grudge < 3) continue;
            if (Math.Abs(n.Gx - npc.Gx) > 2 || Math.Abs(n.Gy - npc.Gy) > 2) continue;
            if (_rng.NextDouble() < 0.18)
            {
                if (n.Realm >= npc.Realm) Die("仇家寻上门来");
                else { n.Grudge = 0; npc.Karma -= 1; }
                break;
            }
        }

        if (npc.Age > npc.Lifespan || _rng.NextDouble() < 0.006 / 12.0) Die("寿元耗尽");
    }

    private void Die(string why)
    {
        var npc = PlayerNpc;
        if (npc == null || Player.Dead || !npc.Alive) return;   // 同一月内只死一次：判词与世代链不能重复记
        npc.Alive = false;
        npc.DeathYear = Year;
        Deaths++;
        KarmaLedger += (npc.Favor - npc.Grudge) * 0.12f + (Player.Help - Player.Refuse) * 0.3f;

        string v = Verdict(npc);
        if (Player.Help >= 5 && v.StartsWith("穷通")) v = "清贫自守，乡邻犹记其善";
        if (Player.Refuse >= 5 && npc.Grudge > npc.Favor) v = "横死·众叛亲离";
        if (Player.Reputation >= 12f && !v.StartsWith("横死")) v = "德行服人，四方皆愿援手";
        if (Player.Reputation <= -12f) v = "恶名昭著·众矢之的，死无人收";

        float merit = Player.Reputation + (Player.Help - Player.Refuse) * 0.5f
                      + (npc.Favor - npc.Grudge) * 0.20f - Player.Rob * 3f;
        Player.Dead = true;
        Player.Verdict = v;
        Player.LastEvent = $"Y{Year}.{Month + 1}　{npc.Name} 卒，{why}。判词：{v}";
        Player.Journal.Add(Player.LastEvent);
        if (Player.Journal.Count > 60) Player.Journal.RemoveAt(0);
        // 因果定形：恶重者不得为人（草木犬鱼鹰），善者为人；中间随缘
        if (merit <= -15f) Player.Fate = Fates[R(3, Fates.Length - 1)];
        else if (merit >= 15f) Player.Fate = "人";
        else Player.Fate = Fates[R(0, Fates.Length - 1)];

        // 欠下的债：来世会遇到债主（没有相欠，何来相见）
        Player.Debtors.Clear();
        foreach (var n in Npcs) if (n.Alive && !n.IsPlayer && n.Grudge > 0) Player.Debtors.Add(n.Id);
        Player.LastEvent = $"死。判词：{v}";
        Player.Journal.Add($"Y{Year}　身殒（{why}）· 判词：{v} · 来世为{Player.Fate}");

        // 世代链：封存这一世（只增不改，没有任何回退入口）
        ArchiveLife(npc, v, Player.Fate, Player.Era, Player);
        AddLog($"Y{Year} 玩家 {npc.Name} 身殒 · {v} · 来世为{Player.Fate}");
    }

    /// <summary>
    /// 印象政治：每个 NPC 有自己的目的，也有一套对你的评价。
    /// 评价分「亲见」与「传闻」两本账：亲见的账最硬，传闻会放大但会淡。
    /// 这套账决定他下一步是扶你还是抢你。
    /// </summary>
    private void StepImpressions()
    {
        DecayImpressions();

        var ply = PlayerNpc;
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;
            if (ply == null || !ply.Alive) continue;

            float dx = n.Gx - ply.Gx, dy = n.Gy - ply.Gy;
            if (dx * dx + dy * dy > 9f) continue;

            float imp = EffectiveImp(n);
            bool weak = n.Realm >= ply.Realm + 2;
            bool heavyTreasure = Player.Stone >= 6;
            bool noGuardian = ply.SectId < 0 && ply.CaveId < 0;

            int goalEdge = n.Goal switch { 1 => 2, 3 => 3, 4 => 1, _ => 0 };

            if (imp <= -18f && weak && heavyTreasure && noGuardian)
            {
                float p = 0.02f + 0.004f * Math.Abs(imp) + 0.01f * goalEdge;
                if (_rngImp.NextDouble() < p)
                {
                    n.Ambush++; NpcAmbushKills++; Player.Ambushed++;
                    Player.Stone = 0; n.Held += 8;
                    Player.Killer = n.Name;
                    Die($"怀宝无护，被 {n.Name} 杀人夺宝");
                    return;
                }
            }
            // 正面声望：即便对方强过你，也会掂量"社会影响"而扶你一把
            if (imp >= 18f && Player.Reputation >= 6f)
            {
                float p = 0.03f;
                if (_rngImp.NextDouble() < p)
                {
                    Player.Stone += 1; Player.Gifts++; NpcGifts++;
                    Perceive(n, 3f, ImpSource.Direct);
                    Player.LastEvent = $"{n.Name} 见你名声不坏，赠灵石一枚。";
                }
            }
        }
    }

    private Npc NearestNpc(Npc me, float maxDist)
    {
        Npc best = null; float bd = maxDist * maxDist;
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.Id == me.Id) continue;
            float dx = n.Gx - me.Gx, dy = n.Gy - me.Gy;
            float d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    private Cave NearestCave(Npc me, float maxDist)
    {
        Cave best = null; float bd = maxDist * maxDist;
        foreach (var c in Caves)
        {
            if (c.OwnerId >= 0) continue;
            float dx = c.Gx - me.Gx, dy = c.Gy - me.Gy;
            float d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    private Sect NearestSect(Npc me, float maxDist)
    {
        Sect best = null; float bd = maxDist * maxDist;
        foreach (var s2 in Sects)
        {
            if (!s2.Alive) continue;
            float dx = s2.Gx - me.Gx, dy = s2.Gy - me.Gy;
            float d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = s2; }
        }
        return best;
    }
}