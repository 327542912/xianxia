using System;
using SimCore;

class P
{
    static void Main()
    {
        int pass = 0, fail = 0;
        void Chk(string name, bool ok, string detail)
        {
            Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}   {detail}");
            if (ok) pass++; else fail++;
        }

        // 52 供给决定价格：破坏地脉后植被掉，灵植必须涨价
        var w1 = new World(7);
        for (int i = 0; i < 12; i++) w1.Tick(1);
        float p0 = w1.PriceOf("灵植");
        for (int x = 0; x < World.W; x++)
            for (int y = 0; y < World.H; y++) { w1.Dmg[x, y] = 0.9f; w1.Veg[x, y] *= 0.2f; }
        w1.EnvDestroyed += 400;
        float p1 = w1.PriceOf("灵植");
        Chk("52 稀缺抬价", p1 > p0 * 1.05f, $"破坏前 {p0:0.00} 石 → 破坏后 {p1:0.00} 石");

        // 53 天灾压低收成、推高价
        var w2 = new World(11);
        float baseP = w2.PriceOf("灵植");
        w2.HarvestMul = 0.45f;
        float disP = w2.PriceOf("灵植");
        Chk("53 灾年抬价", disP > baseP, $"常年 {baseP:0.00} → 灾年 {disP:0.00}");

        // 54 钱庄账实相符
        var w3 = new World(13);
        w3.BeginLife();
        w3.Player.Stone = 20;
        int before = w3.Player.Stone + (int)w3.Bank;
        w3.Deposit(10); w3.Withdraw(5);
        int after = w3.Player.Stone + (int)w3.Bank;
        Chk("54 钱庄存取对账", after == before - 5 + 5 - 5 + 5 && w3.Bank == 5 && w3.Player.Stone == 15,
            $"存10取5：身上 {w3.Player.Stone} 石，账上 {w3.Bank} 石");

        // 55 交易真的改变账与货
        var w4 = new World(17);
        w4.BeginLife();
        w4.Player.Stone = 30;
        int s0 = w4.Player.Stone;
        string r = w4.BuyGood("灵植", 2);
        bool bought = w4.Player.Stone < s0 && w4.Player.Bag.Count > 0;
        Chk("55 买入扣石入货", bought, r);

        // 56 性别可选：投生前选定即生效，立绘与称谓走这一位
        var w5 = new World(19);
        w5.ChoiceGender = 0;
        w5.BeginLife();
        Chk("56 性别可选", w5.PlayerNpc.Gender == 0 && w5.ChoiceGender == null,
            $"选定女 → 本世 Gender={w5.PlayerNpc.Gender}（消费后清空）");

        // 57 因果定物种：判成犬，投生后肉身就是犬，且做不了人世之事
        var w6 = new World(23);
        w6.BeginLife();
        w6.Player.Fate = "狗";
        w6.BeginLife();
        bool dog = w6.PlayerNpc.Species == 3;
        string blocked = w6.Act("sect");
        Chk("57 投生为犬", dog && blocked.Contains("做不了人世之事"),
            $"Species={w6.PlayerNpc.Species}（{SpeciesKind.Name[w6.PlayerNpc.Species]}）· 入宗返回：{blocked}");

        // 58 草木根系固定：挪不动
        var w7 = new World(29);
        w7.BeginLife();
        w7.Player.Fate = "树";
        w7.BeginLife();
        string mv = w7.Act("move", 1);
        Chk("58 草木不可移动", w7.PlayerNpc.Species == 1 && mv.Contains("根扎"),
            $"Species={w7.PlayerNpc.Species}（{SpeciesKind.Name[w7.PlayerNpc.Species]}）· move 返回：{mv}");

        // 59 品阶一生只能升一次；降阶不可逆；重炼是唯一重置
        var it59 = new Item("青锋剑", ItemKind.法宝, 1, 2);
        bool a1 = Grades.Ascend(it59, Rank.仙), a2 = Grades.Ascend(it59, Rank.神);
        bool dg = Grades.Downgrade(it59);
        bool a3 = Grades.Ascend(it59, Rank.神);
        Grades.Reforge(it59);
        bool a4 = Grades.Ascend(it59, Rank.灵);
        Chk("59 品阶一升即锁", a1 && !a2 && dg && !a3 && a4,
            $"升1={a1} 升2={a2} 降={dg} 降后再升={a3} 重炼后={a4}");

        // 60 成品品阶 = min(炼制者上限, 材料上限)
        bool c60 = Grades.Result(3, Rank.神) == Rank.凡 && Grades.Result(7, Rank.灵) == Rank.灵
                   && Grades.Result(8, Rank.神) == Rank.神;
        Chk("60 法宝品阶封顶", c60,
            $"凡手神料={Grades.Text(Grades.Result(3, Rank.神))} 仙手灵料={Grades.Text(Grades.Result(7, Rank.灵))} 神手神料={Grades.Text(Grades.Result(8, Rank.神))}");

        // 61 同一件事：亲见 ≫ 传闻
        var w61 = new World(31); w61.BeginLife();
        var n61 = w61.Npcs.Find(z => z.Alive && !z.IsPlayer);
        w61.Perceive(n61, 10f, true);
        float wi = n61.Imp;
        n61.Imp = 0f; n61.Rumor = 0f;
        w61.Perceive(n61, 10f, false);
        float ru = n61.Imp;
        Chk("61 亲见重于传闻", wi > ru * 10f, $"亲见 {wi:0.0} vs 传闻 {ru:0.00}");

        // 62 传闻会放大，但有 1.6 倍上限
        n61.Imp = 10f; n61.Rumor = 0f;
        for (int i = 0; i < 30; i++) w61.Perceive(n61, 1f, false);
        float eff62 = w61.EffectiveImp(n61);
        Chk("62 传闻放大有上限", eff62 > 10f && eff62 <= 16f,
            $"传闻30次 → 有效 {eff62:0.0}（上限 {10f * 1.6f:0.0}）");

        // 63 同种子 + 同命令 → 同一哈希（联机接口的地基）
        var wa = new World(37); var wb = new World(37);
        wa.BeginLife(); wb.BeginLife();
        for (int i = 0; i < 6; i++) { wa.Tick(1); wb.Tick(1); }
        bool same63 = wa.StateHash() == wb.StateHash() && wa.StateHash() != 0;
        Chk("63 同种子同哈希", same63, $"A={wa.StateHash():X16} B={wb.StateHash():X16}");

        // 64 存档摘要只留 seed / months / hash
        string snap = wa.SaveSnapshot();
        Chk("64 存档摘要格式", snap.StartsWith("seed=") && snap.Contains("months=") && snap.Contains("hash="),
            snap);

        // 65 每一世都落在洪荒早期：五格内地脉未破
        var w65 = new World(41); w65.BeginLife();
        var p65 = w65.PlayerNpc;
        bool clean65 = true;
        for (int dx = -5; dx <= 5 && clean65; dx++)
            for (int dy = -5; dy <= 5; dy++)
            {
                int nx = (int)p65.Gx + dx, ny = (int)p65.Gy + dy;
                if (nx < 0 || ny < 0 || nx >= World.W || ny >= World.H) continue;
                if (w65.Dmg[nx, ny] > 0.2f) { clean65 = false; break; }
            }
        Chk("65 开局落在早期", clean65, $"落点({(int)p65.Gx},{(int)p65.Gy}) 五格内地脉未破={clean65}");

        // 66 三套寿元：草木 > 人 > 兽
        bool c66 = Cultivation.PlantLife > Cultivation.HumanLife && Cultivation.HumanLife > Cultivation.BeastLife
                   && Cultivation.LifespanOf(1, 3) > Cultivation.LifespanOf(0, 3)
                   && Cultivation.LifespanOf(0, 3) > Cultivation.LifespanOf(3, 3);
        Chk("66 三套寿元表", c66,
            $"草木{Cultivation.LifespanOf(1, 3)} > 人{Cultivation.LifespanOf(0, 3)} > 兽{Cultivation.LifespanOf(3, 3)}");

        // 67 非人门禁：草木不动、草木/兽不入宗
        bool c67 = !Cultivation.CanAct(1, "move") && !Cultivation.CanAct(3, "sect")
                   && !Cultivation.CanAct(2, "sect") && Cultivation.CanAct(0, "sect");
        Chk("67 三套体系限制行动", c67,
            $"草木可动={Cultivation.CanAct(1, "move")} 兽可入宗={Cultivation.CanAct(3, "sect")} 人可入宗={Cultivation.CanAct(0, "sect")}");

        Console.WriteLine($"--- PASS {pass} / FAIL {fail} ---");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
