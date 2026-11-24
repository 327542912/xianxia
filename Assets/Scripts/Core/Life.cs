using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>
/// 一世档案：死亡后不可修改的一条记录。世代链只向前长，不提供任何"回到过去"的入口
/// （需求 v1.1 §5.6）。世界继续走下去，你只是换了一具身体。
/// </summary>
public sealed class LifeRecord
{
    public int Index;            // 第几世
    public int NpcId;
    public string Name = "";
    public BeingKind Kind;
    public Grade Grade;
    public int StartYear, EndYear;
    public int Realm;
    public string Verdict = "";
    public string Fate = "";
    public string Era = "";
    public int Help, Refuse, Rob;
    public float Reputation;
}

/// <summary>世代链与确定性存档（无回退入口）。</summary>
public sealed partial class World
{
    public readonly List<LifeRecord> Lives = new();
    public string Era = "洪荒·早期";

    private float OrgDensity(float gx, float gy)
    {
        float d = 0f;
        foreach (var s in Sects) if (s.Alive) d += 1f / (1f + Math.Abs(s.Gx - gx) + Math.Abs(s.Gy - gy));
        foreach (var c in Clans) if (c.Alive) d += 0.6f / (1f + Math.Abs(c.Gx - gx) + Math.Abs(c.Gy - gy));
        foreach (var n2 in Nations) if (n2.Alive) d += 0.8f / (1f + Math.Abs(n2.Gx - gx) + Math.Abs(n2.Gy - gy));
        return d;
    }

    /// <summary>方圆 r 格之内有没有组织的香火（宗门/家族/国度）。</summary>
    private bool NearOrg(float gx, float gy, float r)
    {
        foreach (var s in Sects) if (s.Alive && Math.Abs(s.Gx - gx) <= r && Math.Abs(s.Gy - gy) <= r) return true;
        foreach (var c in Clans) if (c.Alive && Math.Abs(c.Gx - gx) <= r * 0.8f && Math.Abs(c.Gy - gy) <= r * 0.8f) return true;
        foreach (var n2 in Nations) if (n2.Alive && Math.Abs(n2.Gx - gx) <= r && Math.Abs(n2.Gy - gy) <= r) return true;
        return false;
    }

    /// <summary>
    /// 此地属于哪个纪元。方圆五格没有香火、地脉未被破坏的地方，仍是洪荒早期；
    /// 组织近在咫尺是承平，介于两者之间是荒服边地。
    /// 每一世的开局都落在这类未被人烟犁过的节点上（需求 v1.1 §6.3）。
    /// </summary>
    public string EraAt(float gx, float gy)
    {
        int x = Math.Clamp((int)gx, 0, W - 1), y = Math.Clamp((int)gy, 0, H - 1);
        bool untouched = Dmg[x, y] < 0.05f && Veg[x, y] > Qi[x, y] * 0.45f;
        bool farFromOrg = !NearOrg(gx, gy, 5f);
        bool nearOrg = NearOrg(gx, gy, 2f) || OrgDensity(gx, gy) > 1.8f;
        if (untouched && farFromOrg) return "洪荒·早期";
        if (nearOrg) return "承平·秩序带";
        return "荒服·边地";
    }

    /// <summary>找一处尚未被组织犁过的落脚点：洪荒早期的"干净地"。找不到再退回随机。</summary>
    public (int X, int Y) FindPrimordialCell()
    {
        int bx = -1, by = -1;
        float best = float.MaxValue;
        // 网格抽样，避免每次投生都全表扫描
        for (int x = 2; x < W - 2; x += 2)
            for (int y = 2; y < H - 2; y += 2)
            {
                if (!Walkable(x, y)) continue;
                float dev = (NearOrg(x, y, 5f) ? 6f : 0f) + (NearOrg(x, y, 3f) ? 4f : 0f)
                            + Dmg[x, y] * 3f - Veg[x, y] * 0.5f - Qi[x, y] * 0.2f;
                if (dev < best) { best = dev; bx = x; by = y; }
            }
        if (bx < 0) { bx = R(2, W - 3); by = R(2, H - 3); }
        return (bx, by);
    }

    /// <summary>把这一世封存进世代链。只写一次，之后任何路径都不能修改。</summary>
    public void ArchiveLife(Npc npc, string verdict, string fate, string era, PlayerLife pl)
    {
        if (npc == null) return;
        Lives.Add(new LifeRecord
        {
            Index = Lives.Count + 1,
            NpcId = npc.Id,
            Name = npc.Name,
            Kind = npc.Kind,
            Grade = npc.Grade,
            StartYear = Math.Max(0, Year - Math.Max(0, pl?.Years ?? 0) / 12),
            EndYear = Year,
            Realm = npc.Realm,
            Verdict = verdict ?? "",
            Fate = fate ?? "",
            Era = era ?? Era,
            Help = pl?.Help ?? 0,
            Refuse = pl?.Refuse ?? 0,
            Rob = pl?.Rob ?? 0,
            Reputation = pl?.Reputation ?? 0f
        });
    }

    /// <summary>
    /// 世界状态哈希：覆盖新增的品阶/体系/小世界/世代链。
    /// 同种子、同命令序列必须得到同一个值（联机与回放的地基）。
    /// </summary>
    public ulong StateHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }

        Mix((ulong)Year); Mix((ulong)Month); Mix((ulong)Npcs.Count);
        Mix((ulong)Sects.Count); Mix((ulong)Worlds.Count); Mix((ulong)Lives.Count);
        foreach (var n in Npcs)
        {
            Mix((ulong)n.Id); Mix((ulong)n.Realm); Mix((ulong)(int)n.Kind);
            Mix((ulong)(int)n.Grade); Mix((ulong)n.Age);
            Mix((ulong)(n.Gx * 100)); Mix((ulong)(n.Gy * 100));
        }
        foreach (var w in Worlds)
        {
            Mix((ulong)w.Id); Mix((ulong)(int)(w.TimeScale * 100));
            Mix((ulong)(w.Instability * 1000)); Mix(w.Collapsed ? 1UL : 0UL);
        }
        return h;
    }

    /// <summary>
    /// 存档摘要。只存"种子 + 已过月数 + 哈希"，读档靠重放重建：
    /// 因为时间不可回溯，所以不存在"回到第 N 年"的存档入口。
    /// </summary>
    public string SaveSnapshot()
    {
        int months = Year * 12 + Month;
        return $"seed={Seed}\nmonths={months}\nhash={StateHash()}\n";
    }
}