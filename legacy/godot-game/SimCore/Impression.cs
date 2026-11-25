using System;

namespace SimCore;

/// <summary>
/// 印象的来源权重：亲见远大于传闻。传闻会互相放大，但有上限——
/// 否则一个谣言传三十遍就能把人捧成圣人或踩成魔头。
/// </summary>
public sealed partial class World
{
    public const float WitnessWeight = 1.0f;
    public const float RumorWeight = 0.06f;
    public const float RumorCapMul = 1.6f;

    /// <summary>同一件事，亲见与听说，落到印象里的分量差一个量级。</summary>
    public void Perceive(Npc n, float delta, bool witnessed)
    {
        if (n == null) return;
        if (witnessed)
        {
            n.Imp = Math.Clamp(n.Imp + delta * WitnessWeight, -100f, 100f);
        }
        else
        {
            n.Imp = Math.Clamp(n.Imp + delta * RumorWeight, -100f, 100f);
            n.Rumor += Math.Abs(delta) * RumorWeight;
        }
    }

    /// <summary>有效印象：底数 + 传闻放大，放大不超过底数的 1.6 倍。</summary>
    public float EffectiveImp(Npc n)
    {
        if (n == null) return 0f;
        float mag = Math.Abs(n.Imp);
        float amp = Math.Min(mag * (RumorCapMul - 1f), n.Rumor);
        float eff = mag + amp;
        return Math.Sign(n.Imp) * Math.Min(eff, 100f);
    }
}
