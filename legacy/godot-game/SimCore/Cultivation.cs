using System;

namespace SimCore;

/// <summary>
/// 三套体系：人、兽、草木。寿元、修炼速度、能做的事都不同——
/// 不是换张皮，是换了一套"你能不能动、能不能进门派"的规矩。
/// </summary>
public static class Cultivation
{
    // 三境凡品寿元：草木最久，人其次，兽最短（v1.1 验收 51 的口径）
    public const int PlantLife = 1098, HumanLife = 180, BeastLife = 142;

    public static int LifespanOf(int species, int realm)
    {
        int baseLife = species switch
        {
            1 => PlantLife,
            2 => PlantLife / 2,
            3 or 4 or 5 => BeastLife,
            _ => HumanLife
        };
        // 境界每高一层，寿元再往上抬一点（凡身只有基寿）
        float mul = 1f + realm * (species is 1 or 2 ? 0.06f : 0.12f);
        return (int)(baseLife * mul * 0.42f);   // 凡境寿数压到基寿的四成，突破才延长
    }

    public static float GrowMul(int species, int realm) => species switch
    {
        1 or 2 => 0.55f,          // 草木长得慢，但活得久
        3 or 4 or 5 => 1.15f,     // 兽类开智快
        _ => 1f
    };

    /// <summary>能不能做某件事：草木不动、草木与兽不入宗、草木与兽不与人对谈。</summary>
    public static bool CanAct(int species, string verb)
    {
        if (species == 0) return true;
        if (species is 1 or 2 && verb == "move") return false;
        if (species != 0 && (verb == "sect" || verb == "talk" || verb == "cave" ||
                             verb == "open" || verb == "gate" || verb == "delve" ||
                             verb == "plant" || verb == "till" || verb == "harvest" || verb == "sowcrop"))
            return false;
        return true;
    }

    public static string Why(int species, string verb)
        => species is 1 or 2 && verb == "move"
            ? "根扎在这里，挪不动。"
            : $"你是{SpeciesKind.Name[species]}，做不了人世之事。";
}
