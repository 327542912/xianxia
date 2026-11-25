using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>
/// 可重放与世代存档：命令 + 种子 + 世界时间 → 同一个哈希。
/// 这是"保留联机接口"真正的地基：服务器与客户端拿同一串命令，必须落到同一个数。
/// </summary>
public sealed partial class World
{
    /// <summary>世界状态的稳定指纹：只吃"可重放"的状态，不吃渲染与随机流游标。</summary>
    public ulong StateHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }

        Mix(Year); Mix((int)TotalHours); Mix(Npcs.Count); Mix(AliveCount);
        Mix(Nations.Count); Mix(Clans.Count); Mix(Factions.Count);
        Mix((long)Math.Round(TitheAvg * 1000f));
        foreach (var n in Npcs)
        {
            if (!n.Alive) continue;
            Mix(n.Id); Mix((long)(n.Gx * 100)); Mix((long)(n.Gy * 100));
            Mix(n.Realm); Mix((long)(n.Prog * 1000f)); Mix(n.Age); Mix(n.Species); Mix(n.Gender);
        }
        return h;
    }

    /// <summary>存档摘要：只留种子 / 月数 / 哈希，够复现，不泄漏全量状态。</summary>
    public string SaveSnapshot() => $"seed={Seed} months={(int)(TotalHours / 360)} hash={StateHash():X16}";

    // 世代链：只增不改，没有回退入口
    public readonly List<string> Archive = new();

    public void ArchiveLife(string why)
    {
        Archive.Add($"Y{Year} lives={Player.Lives} why={why} {SaveSnapshot()}");
    }

    /// <summary>洪荒早期落点：新一世开局，方圆五格不该有已立的组织，地脉也不该已经被烧过。</summary>
    public (int X, int Y) FindPrimordialCell()
    {
        for (int t = 0; t < 60; t++)
        {
            int x = R(3, W - 4), y = R(3, H - 4);
            if (!Walkable(x, y)) continue;
            bool clean = true;
            for (int dx = -5; dx <= 5 && clean; dx++)
                for (int dy = -5; dy <= 5; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (Dmg[nx, ny] > 0.2f) { clean = false; break; }
                }
            if (!clean) continue;
            // 附近没有组织扎堆（有组织的格子周边就是"已经有主的地"）
            bool nearOrg = false;
            foreach (var n in Npcs)
            {
                if (!n.Alive || n.IsPlayer || n.Faction < 0) continue;
                if (Math.Abs(n.Gx - x) < 5 && Math.Abs(n.Gy - y) < 5) { nearOrg = true; break; }
            }
            if (!nearOrg) return (x, y);
        }
        return (R(3, W - 4), R(3, H - 4));
    }
}
