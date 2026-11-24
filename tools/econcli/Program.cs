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

        Console.WriteLine($"--- PASS {pass} / FAIL {fail} ---");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
