using System;

namespace SimCore;

/// <summary>
/// 品阶（需求 v1.1 §2.15）：凡品 / 灵品 / 神品。横向贯穿人、动植物、资源、空间、功法、法宝。
/// 铁律：一生（或一件器物）只能升一次；升过之后只能单向降；分解重炼是唯一的重置手段。
/// 品阶不是数值奖励，是一条不可逆的因果：升得上去，就得受得住天妒。
/// </summary>
public enum Grade { Fan = 0, Ling = 1, Shen = 2 }

public static class Grades
{
    public static readonly string[] Name = { "凡品", "灵品", "神品" };
    public static readonly string[] Mark = { "凡", "灵", "神" };
    public static readonly string[] Hex = { "9AA79B", "7FC8A6", "D9B463" };

    public static string Of(Grade g) => Name[(int)g];
    public static string MarkOf(Grade g) => Mark[(int)g];

    public static Grade Parse(string s) => s switch
    {
        "灵" or "灵品" => Grade.Ling,
        "神" or "神品" => Grade.Shen,
        _ => Grade.Fan
    };

    /// <summary>品阶对修炼的乘数（横向加成，与境界相乘）。</summary>
    public static float CultMul(Grade g) => g switch
    {
        Grade.Ling => 1.70f,
        Grade.Shen => 3.20f,
        _ => 1.00f
    };

    /// <summary>神品自带天妒：每一次突破都可能招来劫数，修炼越快越危险。</summary>
    public static float TribulationRisk(Grade g) => g switch
    {
        Grade.Shen => 0.030f,
        Grade.Ling => 0.006f,
        _ => 0f
    };

    /// <summary>
    /// 升阶。条件：未升过、境界≥3、有灵脉/材料（此处以灵石或洞府灵气折算）。
    /// 成功后寿元按新阶重算（不是简单乘 1.45，避免叠加失真）。
    /// </summary>
    public static bool Ascend(Npc n, int material, out string msg)
    {
        if (n == null) { msg = "无人可升。"; return false; }
        if (n.Grade >= Grade.Shen) { msg = "已是神品，再无可升。"; return false; }
        if (n.GradeAscended) { msg = "一生只能升一次；错过的那一次，不会再来。"; return false; }
        if (n.Realm < 3) { msg = $"境界不足（需 3 境，今 {n.Realm} 境），品阶不认。"; return false; }
        if (material < 2) { msg = "材料不足：升阶要灵脉底蕴，空手换不来。"; return false; }

        n.Grade = (Grade)((int)n.Grade + 1);
        n.GradeAscended = true;
        n.Lifespan = Cultivation.Lifespan(n.Kind, n.Realm, n.Grade);
        n.Prog = Math.Max(n.Prog, 0.35f);
        msg = $"{n.Name} 由凡入{Grades.Of(n.Grade)}：寿元重定为 {n.Lifespan / 12} 年。此后再不能升。";
        return true;
    }

    /// <summary>单向降阶。原因可以是反噬、天妒、被夺，不可逆；降到此生最低也不会回凡品。</summary>
    public static bool Downgrade(Npc n, string cause, out string msg)
    {
        if (n == null || n.Grade <= Grade.Fan) { msg = "凡品已是最低，降无可降。"; return false; }
        n.Grade = (Grade)((int)n.Grade - 1);
        n.GradeDowngrades++;
        n.Lifespan = Math.Min(n.Lifespan, Cultivation.Lifespan(n.Kind, n.Realm, n.Grade));
        msg = $"{n.Name} 品阶跌落至{Grades.Of(n.Grade)}（{cause}）。降阶不可逆，只能重炼。";
        return true;
    }

    /// <summary>
    /// 分解重炼：唯一的重置手段。肉身/器物毁去，以材料重造，重回凡品，但从头再选一次路。
    /// 对活物来说这是「舍身」——重炼之后是另一个自己。
    /// </summary>
    public static bool Reforge(Npc n, out string msg)
    {
        if (n == null) { msg = "无物可炼。"; return false; }
        n.Grade = Grade.Fan;
        n.GradeAscended = false;
        n.GradeDowngrades = 0;
        n.Reforges++;
        n.Realm = Math.Max(0, n.Realm - 2);          // 重炼伤根：境界回退两境
        n.Prog = 0f;
        n.Lifespan = Cultivation.Lifespan(n.Kind, n.Realm, n.Grade);
        msg = $"{n.Name} 分解重炼：品阶归凡，境界回落至 {n.Realm} 境（第 {n.Reforges} 次重炼）。";
        return true;
    }

    /// <summary>功法/法宝品阶上限：受创造者与材料共同封顶，谁也炼不出高于自己的东西。</summary>
    public static Grade Cap(Grade crafter, Grade material)
        => (Grade)Math.Min((int)crafter, (int)material);

    /// <summary>品阶对战力/威能的乘数。</summary>
    public static float PowerMul(Grade g) => g switch
    {
        Grade.Ling => 1.55f,
        Grade.Shen => 2.60f,
        _ => 1.00f
    };
}