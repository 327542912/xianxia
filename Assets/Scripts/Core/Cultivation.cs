using System;

namespace SimCore;

/// <summary>三套身体：人、草木、兽类。三者的境界链、寿元表、行动能力互不相同。</summary>
public enum BeingKind { Human, Beast, Plant }

/// <summary>
/// 修炼体系（需求 v1.1 §2.4）。人与动植物各有独立体系、境界与寿元表：
/// 人族九境循序而上；兽类开智极快但寿短、后劲靠血脉；草木极慢却能活过王朝。
/// 「境界」只表示在各自体系内的层级，不表示跨体系战力相同。
/// </summary>
public static class Cultivation
{
    public const int MaxRealmIndex = 9;

    public static readonly string[] HumanRealms =
        { "凡体", "炼气", "筑基", "金丹", "元婴", "化神", "炼虚", "合体", "大乘", "渡劫" };
    public static readonly string[] BeastRealms =
        { "凡兽", "开智", "凝丹", "化形", "通灵", "妖将", "妖王", "大妖", "天妖", "妖祖" };
    public static readonly string[] PlantRealms =
        { "凡草", "抽芽", "凝灵", "结丹", "通灵", "化形", "古木", "灵根", "地仙", "木祖" };

    public static readonly string[] KindName = { "人", "兽", "草木" };

    public static string[] Realms(BeingKind kind) => kind switch
    {
        BeingKind.Beast => BeastRealms,
        BeingKind.Plant => PlantRealms,
        _ => HumanRealms
    };

    public static string RealmName(BeingKind kind, int realm)
    {
        var t = Realms(kind);
        if (realm < 0) realm = 0;
        if (realm >= t.Length) realm = t.Length - 1;
        return t[realm];
    }

    public static int MaxRealm(BeingKind kind) => MaxRealmIndex;

    /// <summary>基础寿元（月）。草木以「百年」为单位生长，人族以「十年」计，兽类最短。</summary>
    public static int BaseLifespanMonths(BeingKind kind) => kind switch
    {
        BeingKind.Beast => 42 * 12,
        BeingKind.Plant => 180 * 12,
        _ => 68 * 12
    };

    /// <summary>每进一境延长多少比例的基础寿元：草木最陡（活得久才等得到下一次抽芽）。</summary>
    public static float LifespanStep(BeingKind kind) => kind switch
    {
        BeingKind.Beast => 0.80f,
        BeingKind.Plant => 1.70f,
        _ => 0.55f
    };

    /// <summary>该体系、该境界、该品阶下的寿元（月）。品阶是横向乘数，不替代境界。</summary>
    public static int Lifespan(BeingKind kind, int realm, Grade grade)
    {
        double baseM = BaseLifespanMonths(kind) * (1.0 + LifespanStep(kind) * Math.Max(0, realm));
        double g = grade == Grade.Shen ? 2.20 : grade == Grade.Ling ? 1.45 : 1.0;
        return (int)Math.Round(baseM * g);
    }

    /// <summary>
    /// 修炼速度乘数。兽类前快后慢（血脉用尽），草木极慢（但时间站在它这边），人族最均衡。
    /// </summary>
    public static float GainMul(BeingKind kind, int realm) => kind switch
    {
        BeingKind.Beast => realm <= 3 ? 2.10f : realm <= 5 ? 1.25f : 0.55f,
        BeingKind.Plant => realm <= 3 ? 0.34f : 0.22f,
        _ => realm >= 7 ? 0.50f : realm >= 4 ? 0.72f : 1.00f
    };

    /// <summary>这条命能不能离开原地（草木扎根，动不了）。</summary>
    public static bool CanMove(BeingKind kind) => kind != BeingKind.Plant;

    /// <summary>该体系能做的行动。规则层与界面共用一张表，避免界面点得动、内核不认。</summary>
    public static bool CanAct(BeingKind kind, string verb)
    {
        if (kind == BeingKind.Plant)
            return verb is "meditate" or "wait" or "plant" or "explore" or "ascend" or "craft"
                or "help" or "reforge" or "reincarnate" or "flow" or "open";
        if (kind == BeingKind.Beast)
            return verb is not ("sect" or "plant");
        return true;
    }

    /// <summary>投胎去向 → 身体。树/草是草木世，狗/鹰/鱼是兽类世。</summary>
    public static BeingKind KindOfFate(string fate) => fate switch
    {
        "树" or "草" => BeingKind.Plant,
        "狗" or "鹰" or "鱼" => BeingKind.Beast,
        _ => BeingKind.Human
    };
}