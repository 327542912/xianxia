using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>玩家一世的状态。全部字段都在世界状态里，可存档、可重放。</summary>
public sealed class PlayerLife
{
    public List<Item> Bag = new();      // 行囊：功法/法宝/丹药/符箓/材料/灵植
    public int NpcId = -1;
    public int Lives = 1;
    public string Origin = "";        // 出身：寒门 / 族中子弟 / 皇室旁支 / 荒野弃婴
    public string TalentText = "";    // 天赋描述
    public string Region = "";        // 出生地：秩序带 / 荒野 / 山隅 / 海隅
    public int Quad;                  // 出身 × 天赋 四象限
    public float TalentScore = 1f;
    public int Years;
    public int Grain, Stone;          // 粟 / 灵石
    public bool Dead;
    public string Verdict = "";
    public int Help, Refuse, Rob;
    public int MoveCredit;
    public string LastEvent = "投生于此。";
    public readonly List<string> Journal = new();
    public readonly List<int> Debtors = new();   // 你欠了谁：来世他就在你身边
    public string Fate = "";          // 死后去向（树/草/狗/人……）
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
    public int? ChoiceGender;        // 投生前由界面选定：0 女 1 男；空则随缘

    private static readonly string[] Regions = { "宗门辖地", "荒野", "山隅", "海隅" };
    private static readonly string[] Fates = { "人", "人", "人", "树", "草", "狗", "鹰", "鱼" };

    public bool InPlay => Player.NpcId >= 0 && !Player.Dead;

    // -------------------------------------------------------------- 时辰时钟（真实时间驱动）
    // 一年 = 12 月 × 30 日 × 12 时辰 = 4320 时辰。界面按秒推进，不再"点一下过一年"。
    public const float HoursPerDay = 12f, HoursPerYear = 4320f;
    public float HourAcc;                 // 当年内已过的时辰
    public long TotalHours;               // 有史以来总时辰（只增，不可逆）
    public int MonthOfYear => (int)(HourAcc / 360f) % 12 + 1;
    public int DayOfMonth => (int)(HourAcc / 12f) % 30 + 1;
    public int HourOfDay => (int)HourAcc % 12;
    public string ShiChen => new[] { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" }[HourOfDay] + "时";
    public string ClockText => $"{Year}年 {MonthOfYear}月 {DayOfMonth}日 {ShiChen}";

    /// <summary>推进时辰：满 4320 时辰才算过一年，年内的月/日/时辰都真实流逝。</summary>
    public void SpendHours(float h)
    {
        if (Player.Dead || h <= 0f) return;
        HourAcc += h; TotalHours += (long)h;
        GrowFields(h);
        int guard = 0;
        while (HourAcc >= HoursPerYear && guard++ < 8)
        {
            HourAcc -= HoursPerYear;
            AdvanceLife();
            if (Player.Dead) { HourAcc = 0f; return; }
        }
    }

    public Npc PlayerNpc => Player.NpcId > 0
        ? Npcs.Find(n => n.Id == Player.NpcId)
        : null;

    /// <summary>投生：抽身世与天赋，落在具体格子上。</summary>
    public PlayerLife BeginLife(int? forceQuad = null)
    {
        int quad = forceQuad ?? (_rng.NextDouble() < 0.55
            ? (_rng.NextDouble() < 0.5 ? 0 : 1)
            : (_rng.NextDouble() < 0.5 ? 2 : 3));

        // 每一世都落在洪荒早期：地脉未破、五格内没有已立的组织
        var primordial = FindPrimordialCell();
        int gx = primordial.X, gy = primordial.Y;
        int guard = 0;

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

        string origin = quad switch { 0 => "族中子弟·天资过人", 1 => "族中子弟·资质平平", 2 => "寒门出身·天资过人", _ => "寒门出身·资质平平" };
        string talent = quad is 0 or 2 ? "天资过人" : "资质平平";
        string region = Regions[R(0, 3)];
        if (quad is 0 or 1 && Clans.Count > 0) region = "宗门辖地";

        int startRealm = quad switch { 0 => R(1, 3), 2 => R(0, 2), 1 => 0, _ => 0 };

        string fate = string.IsNullOrEmpty(Player.Fate) ? "人" : Player.Fate;
        int species = SpeciesKind.FromFate(fate);
        int gender = ChoiceGender ?? (_rng.NextDouble() < 0.5 ? 0 : 1);
        string spName = species switch
        {
            1 => "古松", 2 => "青芜", 3 => "黄犬", 4 => "苍鹰", 5 => "游鱼",
            _ => Surnames[R(0, Surnames.Length - 1)] + Givens[R(0, Givens.Length - 1)]
        };
        int age = species is 1 or 2 ? 1 : R(14, 18);
        int lifeSpan = Math.Max(1, (int)(Cultivation.LifespanOf(species, startRealm) * (0.92f + Rf() * 0.16f)));
        if (species != 0) { origin = fate + "之身"; region = "山川草木之间"; talent = "无凡俗天资"; }

        var npc = new Npc
        {
            Id = NextId(),
            Name = spName,
            Species = species,
            Gender = gender,
            Lives = 1,
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
            Realm = startRealm,
            Age = age,
            Lifespan = lifeSpan,
            Quad = quad,
            IsPlayer = true,
            Favor = 0,
            Karma = (int)(KarmaLedger * 0.1f)
        };
        npc.Prog = 0.02f;
        foreach (var old in Npcs) old.IsPlayer = false;   // 旧肉身交还世界，不再冒充"你"
        Npcs.Add(npc);

        var pl = new PlayerLife
        {
            NpcId = npc.Id,
            Lives = 1,
            Origin = origin,
            TalentText = talent,
            Region = region,
            Quad = quad,
            TalentScore = quad is 0 or 2 ? 1.25f : 1f,
            Years = 0,
            Grain = quad is 0 or 1 ? 30 : 8,
            Stone = quad is 0 or 1 ? 2 : 0,
            LastEvent = $"{region}生人：{origin}。"
        };
        pl.Journal.Add($"Y{Year}　{npc.Name} 生于{region}，{origin}");
        Player = pl;
        Player.Fate = fate;
        ChoiceGender = null;
        return pl;
    }

    /// <summary>
    /// 唯一的状态变更入口。返回值是要显示给玩家的一句话；将来服务器把同一串命令
    /// 喂给同一份世界即可得到完全相同的哈希（联机接口的地基）。
    /// </summary>
    public string Act(string verb, int arg = 0) => ActTimed(verb, arg, -1f);

    /// <summary>计时模式：hours&lt;0 走旧的"一动作一年"（验收用）；hours&gt;=0 按时辰扣时（界面用）。</summary>
    public string ActTimed(string verb, int arg, float hours)
    {
        if (Player.Dead)
        {
            // 死后的唯一出口就是投生；旧代码把这一条挡在"已死"早退之后，按钮点了没反应
            if (verb == "reincarnate") { BeginLife(); return Player.LastEvent; }
            return "已死。先投生。";
        }
        var npc = PlayerNpc;
        if (npc == null || !npc.Alive) return "身已殒。";
        if (!Cultivation.CanAct(npc.Species, verb)) return Out(Cultivation.Why(npc.Species, verb));
        bool humanOnly = Array.IndexOf(new[] { "help", "refuse", "rob", "explore", "donate" }, verb) >= 0;
        if (npc.Species != 0 && humanOnly) return Out(Cultivation.Why(npc.Species, verb));
        bool timed = hours >= 0f;
        string Cost(float h)
        {
            if (timed) SpendHours(h); else AdvanceLife();
            return Player.Dead ? Player.Verdict : "";
        }

        string Out(string s)
        {
            Player.LastEvent = s;
            Player.Journal.Add($"Y{Year}　{s}");
            if (Player.Journal.Count > 60) Player.Journal.RemoveAt(0);
            return s;
        }

        switch (verb)
        {
            case "move":
            {
                int dx = arg == 0 ? -1 : arg == 1 ? 1 : 0;
                int dy = arg == 2 ? -1 : arg == 3 ? 1 : 0;
                int tx = (int)npc.Gx + dx, ty = (int)npc.Gy + dy;
                if (!Walkable(tx, ty)) return Out("前方是深水或断崖，过不去。");
                npc.Gx = tx; npc.Gy = ty; npc.Tgx = tx; npc.Tgy = ty;
                Player.MoveCredit++;
                if (timed || Player.MoveCredit >= 4) { Player.MoveCredit = 0; string dv = Cost(0.5f); if (dv.Length > 0) return dv; }
                return Out($"行至({tx},{ty})。");
            }

            case "meditate":
            {
                string m = Meditate(npc, timed);
                if (Player.Dead) return Out(Player.Verdict);
                string dv = Cost(6f);
                return Out(dv.Length > 0 ? Player.Verdict : m);
            }

            case "help":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                t.Favor += 2; npc.Karma += 2; Player.Help++;
                t.Imp = Math.Min(100f, t.Imp + 12f); Player.Reputation += 1.6f;
                Acceptance["粟"] = Math.Min(0.86f, Acceptance["粟"] + 0.001f);
                Cost(2f);
                return Out($"你帮了 {t.Name}。他记下这份恩。");
            }

            case "refuse":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                t.Grudge += 3; npc.Karma -= 2; Player.Refuse++; Grudges++;
                t.Imp = Math.Max(-100f, t.Imp - 9f); Player.Reputation -= 0.9f;
                Cost(1f);
                return Out($"你没有帮 {t.Name}。他记下这笔怨。");
            }

            case "rob":
            {
                var t = NearestNpc(npc, 2.5f);
                if (t == null) return Out("四下无人。");
                if (t.Realm > npc.Realm) { npc.Realm = Math.Max(0, npc.Realm - 1); Cost(3f); return Out($"夺 {t.Name} 不成，反被所伤。"); }
                Player.Stone += 2 + t.Realm;
                t.Grudge += 4; npc.Karma -= 4; Player.Rob++;
                t.Imp = Math.Max(-100f, t.Imp - 22f); Player.Reputation -= 2.6f;
                Cost(3f);
                return Out($"夺了 {t.Name} 的灵石。他恨你入骨。");
            }

            case "cave":
            {
                if (npc.Realm < 2) return Out("境界不足，洞府不认你。");
                var cv = NearestCave(npc, 6f);
                if (cv == null) return Out("左近没有无主洞府。");
                cv.OwnerId = npc.Id; npc.CaveId = cv.Id; npc.TimeScale = cv.TimeScale;
                if (cv.WorldNodeId < 0) cv.WorldNodeId = OpenWorld(npc, TierFor(npc.Realm, Player.Stone, false)).Id;
                Cost(8f);
                return Out($"据洞府而居：灵气 {cv.Qi:0.00}，内时 {cv.TimeScale:0}× —— 修炼快，老得也快。");
            }

            case "sect":
            {
                var sc = NearestSect(npc, 7f);
                if (sc == null) return Out("左近并无山门。");
                npc.SectId = sc.Id; Player.Stone += 1;
                Cost(3f);
                return Out($"入 {sc.Name} 为外门弟子。抽成 {sc.Tithe:P0}，护持看人脸色。");
            }

            case "open":
            {
                // 开辟一方世界：面积由境界与现有灵石决定，不是随便给张图
                int tier = TierFor(npc.Realm, Player.Stone, false);
                var node = OpenWorld(npc, tier, $"{npc.Name}之{(tier <= 1 ? "洞府" : "洞天")}");
                npc.TimeScale = node.TimeScale;
                Cost(10f);
                return Out($"开辟「{node.Name}」：{node.AreaKm2:0.##} km²（{Tiers[tier].Name}），内时 {node.TimeScale:0}×。");
            }

            case "gate":
            {
                if (Player.MoveCredit <= 0) return Out("今年走不动了。");
                int target = -1;
                foreach (var w in Worlds) if (w.OwnerId == npc.Id && w.GateTo >= 0) { target = w.GateTo; break; }
                if (target < 0)
                    foreach (var w in Worlds) if (w.GateTo >= 0) { target = w.GateTo; break; }
                if (target < 0) return Out("此界尚无门。");
                Player.MoveCredit--;
                string r = Traverse(target, npc);
                Cost(2f);
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
                Player.Reputation += 0.6f;   // 种树养地，是人望
                Cost(3f);
                return Out($"你在({x},{y})种下花木。地气回了 {Qi[x, y]:0.00}，人望 +0.6。");
            }

            case "delve":
            {
                // 移山填海/开山采脉：拿得到宝，但这一块地就真的毁了
                int x = (int)npc.Gx, y = (int)npc.Gy;
                if (x < 0 || y < 0 || x >= W || y >= H) return Out("此处无处下手。");
                float before = Qi[x, y];
                Dmg[x, y] = Math.Min(1f, Dmg[x, y] + 0.22f);
                Qi[x, y] = Math.Max(0f, Qi[x, y] - 0.30f);
                Veg[x, y] = Math.Max(0f, Veg[x, y] - 0.60f);
                Player.Stone += 4;
                EnvDestroyed++;
                Player.Reputation -= 2.2f;   // 伤地脉者，人望下滑；修士会记恨你坏了共用的灵气
                foreach (var n in Npcs) if (n.Alive && !n.IsPlayer) n.Imp -= 1.2f;
                Cost(4f);
                return Out($"开山采脉：得石四枚，({x},{y})灵气 {before:0.00}→{Qi[x, y]:0.00}，此地再难复原。");
            }

            case "explore":
            {
                Cost(4f);
                if (!Player.Dead) return Out(ExploreEvent(npc));
                return Player.Verdict;
            }

            case "wait":
                Cost(HoursPerYear);
                return Player.Dead ? Player.Verdict : Out("又过一年。");

            case "reincarnate":
                BeginLife();          // 活着也允许主动转世（重开一世）
                return Player.LastEvent;
        }
        return Out("无名之举。");
    }

    /// <summary>
    /// 多轮交谈：同一 NPC 可反复交互，每轮改变他对你的印象并留档（恩怨的凭据）。
    /// topic: 0 问来历 1 施援手 2 论道 3 挑衅
    /// </summary>
    public string Talk(int npcId, int topic)
    {
        if (Player.Dead) return "已死，无人可谈。";
        var me = PlayerNpc;
        var t = Npcs.Find(n => n.Id == npcId && n.Alive);
        if (me == null || t == null) return "人已不在。";
        if (Math.Abs(t.Gx - me.Gx) > 2 || Math.Abs(t.Gy - me.Gy) > 2) return "相距太远，喊话不闻。";
        t.TalkCount++;
        string line;
        switch (topic)
        {
            case 0:
                line = $"{t.Name}：我自{(t.SectId >= 0 ? "宗门辖地" : "荒野")}来，欲{(t.Goal == 0 ? "求道" : t.Goal == 1 ? "聚财" : t.Goal == 2 ? "传法" : t.Goal == 3 ? "寻仇" : t.Goal == 4 ? "结党" : "避劫")}。";
                t.Imp = Math.Min(100f, t.Imp + 3f);
                break;
            case 1:
                if (Player.Stone >= 2) { Player.Stone -= 2; t.Favor += 3; t.Imp = Math.Min(100f, t.Imp + 10f); Player.Reputation += 1.2f; Player.Help++; line = $"{t.Name}：这份情我记下了。"; }
                else { t.Imp = Math.Max(-100f, t.Imp - 4f); line = $"{t.Name}：空口白话，也敢称援手？"; }
                break;
            case 2:
                line = t.Realm >= 4 ? $"{t.Name}：你这根基，尚可一论。" : $"{t.Name}：道在我处，你还差得远。";
                t.Imp = Math.Min(100f, t.Imp + (t.Realm >= 4 ? 5f : -3f));
                break;
            default:
                t.Grudge += 2; t.Imp = Math.Max(-100f, t.Imp - 12f); Player.Reputation -= 1.2f; Player.Refuse++;
                line = $"{t.Name}：你今日这话，我记着。";
                break;
        }
        t.Memory.Add($"Y{Year}·{MonthOfYear}月 {line}");
        if (t.Memory.Count > 8) t.Memory.RemoveAt(0);
        Player.Reputation += t.Imp >= 0 ? 0.2f : -0.2f;
        SpendHours(1f);      // 一番话，一个时辰
        Player.LastEvent = line;
        Player.Journal.Add($"Y{Year} {line}");
        if (Player.Journal.Count > 60) Player.Journal.RemoveAt(0);
        return line;
    }

    private string Meditate(Npc npc, bool timed)
    {
        float speed = Math.Max(1f, Math.Min(npc.TimeScale, 20f));
        npc.Prog += 0.09f * speed * Player.TalentScore;
        string msg = "打坐一年，气息略稳。";
        while (npc.Prog >= 1f && npc.Realm < 8)
        {
            npc.Prog -= 1f; npc.Realm++;
            msg = $"突破：{npc.Realm} 境。";
        }
        if (!timed) { AdvanceLife(); if (Player.Dead) return Player.Verdict; }
        return msg;
    }

    private string ExploreEvent(Npc npc)
    {
        double r = _rng.NextDouble();
        if (r < 0.16)
        {
            int bonus = R(2, 5);
            npc.Realm = Math.Min(8, npc.Realm + bonus);
            return $"秘境：内时流转不知几载，出关时已是 {npc.Realm} 境。";
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
            return $"偶得《{t.Name}》残卷，可参其形。";
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
            if (t != null) { t.Favor += 1; npc.Karma += 1; return $"路遇 {t.Name}，同避一场风雨，结一善缘。"; }
            return "独行千里，无事。";
        }
        return "荒山无路，无功而返。";
    }

    private void AdvanceLife()
    {
        var npc = PlayerNpc;
        if (npc == null || !npc.Alive) return;

        Tick(1);
        Player.Years++;
        npc.Age++;
        Player.Grain = Math.Max(0, Player.Grain - 2);
        if (Player.Grain == 0) npc.Lifespan--;              // 饿则短寿

        if (npc.CaveId >= 0) npc.Age += 2;                  // 洞府内时间快，老得也快

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

        if (npc.Age > npc.Lifespan || _rng.NextDouble() < 0.006) Die("寿元耗尽");
    }

    private void Die(string why)
    {
        var npc = PlayerNpc;
        if (npc == null) return;
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
        // 因果定形：恶重者不得为人（草木犬鱼鹰），善者为人；中间随缘
        if (merit <= -15f) Player.Fate = Fates[R(3, Fates.Length - 1)];
        else if (merit >= 15f) Player.Fate = "人";
        else Player.Fate = Fates[R(0, Fates.Length - 1)];
        // 欠下的债：来世会遇到债主（没有相欠，何来相见）
        Player.Debtors.Clear();
        foreach (var n in Npcs) if (n.Alive && !n.IsPlayer && n.Grudge > 0) Player.Debtors.Add(n.Id);
        Player.LastEvent = $"死。判词：{v}";
        Player.Journal.Add($"Y{Year}　身殒（{why}）· 判词：{v} · 来世为{Player.Fate}");
        ArchiveLife(why);      // 世代链只增不改
        AddLog($"Y{Year} 玩家 {npc.Name} 身殒 · {v} · 来世为{Player.Fate}");
    }

    /// <summary>
    /// 印象政治：每个 NPC 有自己的目的，也有一套对你的评价。评价会随时间淡化，
    /// 但"帮过/拒过/抢过"留下的第一印象加上后续权重，决定他下一步是扶你还是抢你。
    /// </summary>
    private void StepImpressions()
    {
        var ply = PlayerNpc;
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;
            float drift = 0.45f + 0.15f * Math.Abs(n.Imp) / 20f;   // 印象越极端，淡得越快
            n.Imp = n.Imp > 0 ? Math.Max(0f, n.Imp - drift) : Math.Min(0f, n.Imp + drift);

            if (ply == null || !ply.Alive) continue;

            float dx = n.Gx - ply.Gx, dy = n.Gy - ply.Gy;
            if (dx * dx + dy * dy > 9f) continue;                   // 三年里走到你身边才谈得上

            bool weak = n.Realm >= ply.Realm + 2;
            bool heavyTreasure = Player.Stone >= 6;
            bool noGuardian = ply.SectId < 0 && ply.CaveId < 0;     // 无宗门、无洞府 = 无护道者

            // 目的驱动：求财(1)/复仇(3)/结党(4) 的人最容易动手
            int goalEdge = n.Goal switch { 1 => 2, 3 => 3, 4 => 1, _ => 0 };

            float eff = EffectiveImp(n);
            if (eff <= -18f && weak && heavyTreasure && noGuardian)
            {
                float p = 0.02f + 0.004f * Math.Abs(eff) + 0.01f * goalEdge;
                if (_rngImp.NextDouble() < p)
                {
                    n.Ambush++; NpcAmbushKills++; Player.Ambushed++;
                    Player.Stone = 0; n.Held += 8;            // 夺来的宝归他
                    Player.Killer = n.Name;
                    Die($"怀宝无护，被 {n.Name} 杀人夺宝");
                    return;
                }
            }
            // 正面声望：即便对方强过你，也会掂量"社会影响"而扶你一把
            if (eff >= 18f && Player.Reputation >= 6f)
            {
                float p = 0.03f;
                if (_rngImp.NextDouble() < p)
                {
                    Player.Stone += 1; Player.Gifts++; NpcGifts++;
                    n.Imp += 3f;
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
