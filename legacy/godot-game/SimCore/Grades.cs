using System;

namespace SimCore;

/// <summary>四阶：凡 / 灵 / 仙 / 神。法宝与功法的品阶都走这一套。</summary>
public enum Rank { 凡 = 0, 灵 = 1, 仙 = 2, 神 = 3 }

public static class Grades
{
    public static readonly string[] Name = { "凡品", "灵品", "仙品", "神品" };
    public static string Text(Rank r) => Name[(int)r];

    /// <summary>炼制者境界决定他能做出多高的品：凡手做不出灵品，灵手做不出神品。</summary>
    public static Rank CeilingOf(int realm) => realm >= 8 ? Rank.神 : realm >= 7 ? Rank.仙 : realm >= 4 ? Rank.灵 : Rank.凡;

    /// <summary>成品品阶 = min(炼制者上限, 材料上限)。材料次一等，手再高也出不来。</summary>
    public static Rank Result(int crafterRealm, Rank material)
        => (Rank)Math.Min((int)CeilingOf(crafterRealm), (int)material);

    /// <summary>
    /// 一生只能升一次；升完锁死。降阶不可逆（降了就永远低）。重炼是唯一重置。
    /// 返回 true 表示这次真的升上去了。
    /// </summary>
    public static bool Ascend(Item it, Rank cap)
    {
        if (it.RankLocked || it.RankEverAscended) return false;
        if ((int)it.Rank >= (int)cap) { it.RankLocked = true; return false; }
        it.Rank = (Rank)((int)it.Rank + 1);
        it.RankEverAscended = true;
        it.RankLocked = true;          // 一升即锁，不给第二次
        return true;
    }

    /// <summary>降阶：可以降，但降完仍然锁着——"不可逆"说的是锁，不是数值。</summary>
    public static bool Downgrade(Item it)
    {
        if ((int)it.Rank == 0) return false;
        it.Rank = (Rank)((int)it.Rank - 1);
        it.RankLocked = true;
        return true;
    }

    /// <summary>重炼：唯一能把锁解开、把品阶打回凡品的路径。</summary>
    public static void Reforge(Item it)
    {
        it.Rank = Rank.凡;
        it.RankLocked = false;
        it.RankEverAscended = false;
        it.Qty = Math.Max(1, it.Qty);
    }
}
