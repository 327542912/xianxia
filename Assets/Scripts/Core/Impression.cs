using System;

namespace SimCore;

/// <summary>印象的来源。权重差别是这套政治的地基：亲眼所见远大于道听途说。</summary>
public enum ImpSource { Direct, Secondhand, Rumor }

/// <summary>
/// 印象系统（需求 v1.1 §5.7）。
/// 三条规则：
///   1. 第一印象受「时空」与「人物」影响（同门/同族/境界差/是否在他的地界）；
///   2. 直接见闻权重远大于道听途说（亲见 1.00 / 一次转述 0.35 / 传闻 0.12）；
///   3. 重复传播会放大印象，但有上限（最多 1.6 倍），越极端淡得越快。
/// </summary>
public sealed partial class World
{
    private static float ClampImp(float v) => Math.Clamp(v, -100f, 100f);

    /// <summary>某人亲历一件事：这是最硬的一手印象。</summary>
    public void Perceive(Npc observer, float delta, ImpSource src, int hops = 1)
    {
        if (observer == null || !observer.Alive || observer.IsPlayer) return;
        switch (src)
        {
            case ImpSource.Direct:
                observer.ImpDirect = ClampImp(observer.ImpDirect + delta);
                break;
            case ImpSource.Secondhand:
                // 一次转述：话从别人嘴里过了一道，信七分
                observer.ImpRumor = ClampImp(observer.ImpRumor + delta * 0.35f);
                observer.RumorHits++;
                break;
            default:
                // 传闻：每多传一手再打对折，但不为零（世上没有不透风的墙）
                float w = 0.12f / (1f + 0.5f * Math.Max(0, hops - 1));
                observer.ImpRumor = ClampImp(observer.ImpRumor + delta * w);
                observer.RumorHits++;
                break;
        }
    }

    /// <summary>
    /// 时空与人物给第一印象的偏置：同门、同族、境界悬殊、是否在对方地界，
    /// 都会让同一件事被记成不同的分量。
    /// </summary>
    public float FirstImpressionBias(Npc observer, Npc subject)
    {
        if (observer == null || subject == null) return 0f;
        float bias = 0f;
        if (observer.SectId >= 0 && observer.SectId == subject.SectId) bias += 6f;     // 同门：先信三分
        if (observer.ClanId >= 0 && observer.ClanId == subject.ClanId) bias += 5f;     // 同族：血亲先护
        int gap = subject.Realm - observer.Realm;
        if (gap >= 3) bias -= 4f;                                                       // 境界压人：畏多于敬
        if (gap <= -3) bias += 3f;                                                      // 对方远弱：轻慢
        if (subject.CaveId >= 0) bias -= 3f;                                            // 在他的洞府里：客随主便
        float seasonK = Season == Season.Winter ? 0.85f : Season == Season.Summer ? 1.05f : 1f;
        return bias * seasonK;
    }

    /// <summary>亲见一手 + 现场偏置，写进印象。</summary>
    public void Witness(Npc observer, Npc subject, float delta)
    {
        if (observer == null) return;
        float b = FirstImpressionBias(observer, subject);
        Perceive(observer, delta + b, ImpSource.Direct);
    }

    /// <summary>
    /// 传播：一个人对玩家的评价会经由中间商/同门扩散到邻里。
    /// 一手印象越强，传得越远；重复听见会放大（EffectiveImp 里结算），但封顶。
    /// </summary>
    public void SpreadRumor(Npc source, float delta, int hops = 3)
    {
        if (source == null || !source.Alive || source.IsPlayer) return;
        int spread = 0;
        foreach (var n in Npcs)
        {
            if (spread >= 4 || n == source || !n.Alive || n.IsPlayer) continue;
            float dx = n.Gx - source.Gx, dy = n.Gy - source.Gy;
            if (dx * dx + dy * dy > 36f) continue;    // 六格之内才听得到
            Perceive(n, delta, spread == 0 ? ImpSource.Secondhand : ImpSource.Rumor, hops);
            spread++;
        }
    }

    /// <summary>有效印象 = 亲见 + 传闻 × 放大系数（重复传播最多 1.6 倍）。</summary>
    public float EffectiveImp(Npc n)
    {
        if (n == null) return 0f;
        float amp = Math.Min(1.6f, 1f + 0.06f * n.RumorHits);
        return ClampImp(n.ImpDirect + n.ImpRumor * amp);
    }

    /// <summary>印象按月淡化：越极端淡得越快，传闻比亲见淡得更快（流言本就短命）。</summary>
    public void DecayImpressions()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive || n.IsPlayer) continue;
            float dDirect = 0.30f + 0.012f * Math.Abs(n.ImpDirect);
            float dRumor = 0.55f + 0.020f * Math.Abs(n.ImpRumor);
            if (Math.Abs(n.ImpDirect) <= dDirect) n.ImpDirect = 0f;
            else n.ImpDirect -= Math.Sign(n.ImpDirect) * dDirect;

            if (Math.Abs(n.ImpRumor) <= dRumor) n.ImpRumor = 0f;
            else n.ImpRumor -= Math.Sign(n.ImpRumor) * dRumor;

            // 印象淡到极处，传闻次数也归零：旧事不再被提起
            if (Math.Abs(n.ImpRumor) < 0.5f) n.RumorHits = Math.Max(0, n.RumorHits - 2);
        }
    }
}