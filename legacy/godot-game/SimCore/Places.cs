using System;
using System.Collections.Generic;

namespace SimCore;

/// <summary>
/// 场所层：坊市 / 钱庄 / 山门 / 宅院 / 飞舟舱。
/// 把"面板"变成"能走进去的地方"——每个地方的动作都落在同一个经济与权力链路上：
/// 坊市卖的是地里长出来的东西，钱庄存的是卖货挣的石，宗门抽的是坊市的头，
/// 家族分的产业又回到坊市，飞舟能把你去更远的市场。
/// </summary>
public sealed partial class World
{
    public string BoatStyle = "云纹";
    public static readonly string[] BoatStyles = { "云纹", "雷纹", "星图", "竹影", "赤金" };
    public int BoatTrips;

    /// <summary>一格被毁：植被掉、灵气掉、账上记一笔。不可逆。</summary>
    public void RuinAt(int x, int y, float amt)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        Dmg[x, y] = Math.Min(1f, Dmg[x, y] + amt);
        Veg[x, y] = Math.Max(0f, Veg[x, y] * (1f - amt));
        Qi[x, y] = Math.Max(0f, Qi[x, y] - amt * 1.1f);
        EnvDestroyed++;
    }

    /// <summary>战乱：真的在地图上留下成片焦土，不是只加个计数。</summary>
    public void WarRuin(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = _rng.Next(2, W - 2), y = _rng.Next(2, H - 2);
            if (Tiles[x, y] == Terrain.Water) { i--; continue; }
            RuinAt(x, y, (float)(0.25 + _rng.NextDouble() * 0.35));
            if (_rng.NextDouble() < 0.5) RuinAt(x + 1, y, 0.15f);
            if (_rng.NextDouble() < 0.5) RuinAt(x, y + 1, 0.15f);
        }
    }

    // ---------------------------------------------------------------- 山门
    public string SectTask()
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        int reward = 2 + pn.Realm;
        Player.Stone += reward;
        Player.Reputation += 0.6f;
        SpendHours(4f);
        AddLog($"Y{Year} 你在山门领了一件差事，得 {reward} 石，声望 +0.6。宗门记得谁肯跑腿。");
        return $"执事递来一件差事：巡一段山道，得 {reward} 石，声望 {Player.Reputation:0.0}。";
    }

    public string SectTithe() =>
        $"宗门向坊市抽成 {TitheAvg * 100f:0}%：抽得太狠，坊市货少价高，凡人先苦；抽得轻，宗门护不住疆界。";

    // ---------------------------------------------------------------- 宅院
    public string ClanHelp()
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        if (Clans.Count == 0) return "这一带还没有立起家族。";
        int grain = 2 + Clans.Count / 2;
        Give(new Item("灵稻", ItemKind.灵植, grain, 1));
        Player.Reputation += 0.3f;
        SpendHours(2f);
        return $"族老分了 {grain} 份灵稻给你：族产是根，肯回来的人才有份。";
    }

    // ---------------------------------------------------------------- 飞舟
    public string BoatRestyle()
    {
        int k = 0; while (k < BoatStyles.Length && BoatStyles[k] != BoatStyle) k++;
        BoatStyle = BoatStyles[(k + 1) % BoatStyles.Length];
        SpendHours(2f);
        return $"舱内换作「{BoatStyle}」纹样：内饰自定义，不影响飞行。";
    }

    public string BoatSail()
    {
        var pn = PlayerNpc; if (pn == null) return "身已殒。";
        BoatTrips++;
        pn.Gx = Math.Clamp(pn.Gx + _rng.Next(-6, 7), 1, W - 2);
        pn.Gy = Math.Clamp(pn.Gy + _rng.Next(-6, 7), 1, H - 2);
        SpendHours(3f);
        return $"飞舟掠过 {(int)pn.Gx},{(int)pn.Gy}：换个市场，价就不一样。第 {BoatTrips} 次起航。";
    }

    // ---------------------------------------------------------------- 市价一览（坊市用）
    public string PriceBoard()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"天时 {Weather}　收成×{HarvestMul:0.00}　");
        foreach (var g in Goods) sb.Append($"{g} {PriceOf(g):0.0}　");
        return sb.ToString();
    }
}
