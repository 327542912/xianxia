using System;
using System.Collections.Generic;

namespace SimCore;

public enum TreasureKind { Artifact, Elixir, Material }

/// <summary>
/// 法宝/丹药/材料（需求 v1.1 §2.15）：品阶上限 = min(炼制者品阶, 材料品阶)，
/// 同样一生只能升一次、可单向降、分解重炼是唯一重置手段。
/// </summary>
public sealed class Treasure
{
    public int Id;
    public string Name = "";
    public TreasureKind Kind;
    public Grade Grade;
    public Grade Material;
    public float Power;
    public int Durability = 100;
    public int OwnerId = -1;
    public bool Ascended;
    public int Reforges;
}

public sealed partial class World
{
    public readonly List<Treasure> Treasures = new();

    private static readonly string[] ArtifactNames =
        { "青冥剑", "玄都镜", "赤霄印", "归墟珠", "照夜灯", "斩妄尺", "抱朴鼎", "溯光梭" };
    private static readonly string[] ElixirNames =
        { "凝气丹", "洗尘露", "回春散", "破境丸", "养魂膏" };

    /// <summary>炼宝：品阶被封顶，材料不够就只能炼出凡品——这不是运气，是上限。</summary>
    public Treasure CraftTreasure(Npc crafter, Grade material, TreasureKind kind)
    {
        if (crafter == null) return null;
        Grade cap = Grades.Cap(crafter.Grade, material);
        string name = kind == TreasureKind.Elixir
            ? ElixirNames[R(0, ElixirNames.Length - 1)]
            : ArtifactNames[R(0, ArtifactNames.Length - 1)];

        var t = new Treasure
        {
            Id = Treasures.Count,
            Name = name,
            Kind = kind,
            Grade = cap,
            Material = material,
            Power = (1.0f + 0.45f * (int)cap) * Grades.PowerMul(cap),
            OwnerId = crafter.Id
        };
        Treasures.Add(t);
        AddLog($"Y{Year} {crafter.Name} 炼成「{t.Name}」（{Grades.Of(t.Grade)}，材料 {Grades.Of(material)}）");
        return t;
    }

    /// <summary>法宝升阶：一生一次，上限受炼制者与材料双重封顶。</summary>
    public bool AscendTreasure(Treasure t, Npc owner, int material, out string msg)
    {
        if (t == null) { msg = "无宝可升。"; return false; }
        if (t.Ascended) { msg = "此宝已升过一次，不能再升。"; return false; }
        if (t.Grade >= Grade.Shen) { msg = "已是神品，升无可升。"; return false; }
        if (material < 2) { msg = "材料不足。"; return false; }
        if (owner == null || owner.Grade < t.Grade + 1) { msg = "持宝者品阶不够：宝不认低阶之主。"; return false; }

        t.Grade = (Grade)((int)t.Grade + 1);
        t.Ascended = true;
        t.Power *= 1.6f;
        msg = $"「{t.Name}」升为{Grades.Of(t.Grade)}。此生只此一次。";
        return true;
    }

    /// <summary>法宝降阶：单向，不可逆。</summary>
    public bool DegradeTreasure(Treasure t, string cause, out string msg)
    {
        if (t == null || t.Grade <= Grade.Fan) { msg = "凡品已是最低。"; return false; }
        t.Grade = (Grade)((int)t.Grade - 1);
        t.Power *= 0.62f;
        msg = $"「{t.Name}」跌落至{Grades.Of(t.Grade)}（{cause}）。";
        return true;
    }

    /// <summary>分解重炼：唯一重置手段。材料折损，品阶归凡，但可以重新选材再炼一次。</summary>
    public Treasure ReforgeTreasure(Treasure t, Npc owner)
    {
        if (t == null || owner == null) return null;
        Treasures.Remove(t);
        var nt = CraftTreasure(owner, t.Material, t.Kind);
        if (nt != null) { nt.Reforges = t.Reforges + 1; nt.Grade = Grade.Fan; nt.Power *= 0.9f; }
        return nt;
    }
}