using System;
using System.Collections.Generic;

namespace SimCore;

// ---------------------------------------------------------------------------
// P0-lite：确定性世界内核（无 UnityEngine / 无 Godot 依赖，可在 dotnet 下独立跑）
// 设计铁律：一切状态变更只在本层发生；呈现层只读。
// ---------------------------------------------------------------------------

public enum Terrain { Grass, Rock, Water, Sand, Cliff }

public sealed class Npc
{
    public int Id;
    public string Name = "";
    public string Dao = "";
    public float Gx, Gy;        // 世界格坐标（浮点，用于插值移动）
    public float Tgx, Tgy;      // 目标格
    public float Height = 1f;   // 身材：身高系数 0.82~1.18（立绘/小人随之变形）
    public float Girth = 1f;    // 身材：体宽系数 0.85~1.25
    public float Hue;           // 衣色色相 0~1
    public int Realm;           // 境界 0..8
    public int Age;
    public int Lifespan;
    public int Grudge;          // 记恨数
    public int Favor;           // 记恩数
    public int Faction = -1;
    public float Karma;         // 因果净值（负=恶名）
    public bool Alive = true;
    public int DeathYear = -1;
}

public sealed class Faction
{
    public int Id;
    public string Name = "";
    public int Kind;            // 0 部落 1 商帮 2 城邦 3 宗门 4 帝国
    public List<int> Members = new();
    public int Power;
    public int FoundedYear;
    public static readonly string[] KindNames = { "部落", "商帮", "城邦", "宗门", "帝国" };
}

public sealed class World
{
    public const int W = 16, H = 16;
    public int Year;
    public long Seed;
    public Terrain[,] Tiles = new Terrain[W, H];
    public readonly List<Npc> Npcs = new();
    public readonly List<Faction> Factions = new();
    public readonly List<string> Log = new();
    public int Grudges, Favors;
    public string CurrencyWord = "";

    private readonly Random _rng;
    private int _nextFactionAt;

    private static readonly string[] Surnames =
        { "李","陈","张","柳","慕容","上官","赵","沈","云","萧","顾","谢","苏","叶","秦","陆","宁","季","洛","白" };
    private static readonly string[] Givens =
        { "尘","渊","清","玄","辞","舟","砚","川","岚","述","珩","棠","弋","禺","澈","瑟","笙","爻","临","孤" };
    private static readonly string[] DaoWords =
        { "玄清","太一","无名","凌霄","归墟","抱朴","青冥","溯光","无咎","照夜" };

    public World(long seed)
    {
        Seed = seed;
        _rng = new Random((int)(seed & 0x7fffffff));
        Year = 1;
        GenTerrain();
        for (int i = 0; i < 18; i++) Spawn();
        _nextFactionAt = 8;
    }

    private int R(int min, int max) => _rng.Next(min, max + 1);
    private float Rf() => (float)_rng.NextDouble();

    private void GenTerrain()
    {
        // 简单高度场：中心为盆地（可居），边缘抬升为山，低洼成水。
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            float cx = (x - W * 0.5f) / (W * 0.5f);
            float cy = (y - H * 0.5f) / (H * 0.5f);
            float d = (float)Math.Sqrt(cx * cx + cy * cy);
            float n = Rf() * 0.28f;
            float h = d * 0.9f + n;
            Tiles[x, y] = h > 0.92f ? Terrain.Cliff
                : h > 0.72f ? Terrain.Rock
                : h > 0.60f ? Terrain.Sand
                : h < 0.18f ? Terrain.Water
                : Terrain.Grass;
        }
    }

    public bool Walkable(int x, int y) =>
        x >= 0 && y >= 0 && x < W && y < H &&
        Tiles[x, y] != Terrain.Water && Tiles[x, y] != Terrain.Cliff;

    private void Spawn()
    {
        int gx = 0, gy = 0, guard = 0;
        do { gx = R(1, W - 2); gy = R(1, H - 2); } while (!Walkable(gx, gy) && ++guard < 50);

        var n = new Npc
        {
            Id = Npcs.Count + 1,
            Name = Surnames[R(0, Surnames.Length - 1)] + Givens[R(0, Givens.Length - 1)],
            Gx = gx, Gy = gy, Tgx = gx, Tgy = gy,
            Height = 0.82f + Rf() * 0.36f,
            Girth = 0.85f + Rf() * 0.40f,
            Hue = Rf(),
            Realm = _rng.NextDouble() < 0.12 ? R(3, 6) : R(0, 2),
            Age = R(16, 42),
            Lifespan = 60 + R(0, 40)
        };
        if (n.Realm >= 3) n.Dao = DaoWords[R(0, DaoWords.Length - 1)] + "真人";
        Npcs.Add(n);
    }

    public void Tick(int years = 1)
    {
        for (int k = 0; k < years; k++)
        {
            StepOnce();
            Year++;
        }
    }

    private void StepOnce()
    {
        foreach (var n in Npcs)
        {
            if (!n.Alive) continue;

            // 移动：向目标走 1 格；到点后可能改目标
            if (Math.Abs(n.Gx - n.Tgx) < 0.05f && Math.Abs(n.Gy - n.Tgy) < 0.05f)
            {
                if (_rng.NextDouble() < 0.45)
                {
                    int tx = Math.Clamp((int)n.Gx + R(-3, 3), 0, W - 1);
                    int ty = Math.Clamp((int)n.Gy + R(-3, 3), 0, H - 1);
                    if (Walkable(tx, ty)) { n.Tgx = tx; n.Tgy = ty; }
                }
            }
            else
            {
                float dx = Math.Sign(n.Tgx - n.Gx), dy = Math.Sign(n.Tgy - n.Gy);
                if (Walkable((int)(n.Gx + dx), (int)n.Gy)) n.Gx += dx * 0.5f;
                if (Walkable((int)n.Gx, (int)(n.Gy + dy))) n.Gy += dy * 0.5f;
            }

            n.Age++;
            if (n.Age > n.Lifespan || _rng.NextDouble() < 0.004)
            {
                n.Alive = false;
                n.DeathYear = Year;
                Log.Add($"Y{Year} {n.Name} 身殒，因果 {(n.Karma >= 0 ? "+" : "")}{n.Karma:0}");
            }
        }

        // 相遇：产生恩怨（相邻即可能发生）
        for (int i = 0; i < Npcs.Count; i++)
        {
            var a = Npcs[i];
            if (!a.Alive) continue;
            for (int j = i + 1; j < Npcs.Count; j++)
            {
                var b = Npcs[j];
                if (!b.Alive) continue;
                float dx = a.Gx - b.Gx, dy = a.Gy - b.Gy;
                if (dx * dx + dy * dy > 1.2f) continue;
                if (_rng.NextDouble() > 0.30) continue;

                // 逻辑：强者向弱者索取 → 记怨；平辈互助 → 记恩
                int diff = a.Realm - b.Realm;
                if (diff >= 2 && _rng.NextDouble() < 0.6)
                {
                    b.Grudge++; b.Karma -= 3; a.Karma += 1;
                    Grudges++;
                    Log.Add($"Y{Year} {a.Name}({a.Realm}境) 劫 {b.Name} 之资，{b.Name} 记恨入骨");
                }
                else if (Math.Abs(diff) <= 1 && _rng.NextDouble() < 0.55)
                {
                    a.Favor++; b.Favor++;
                    a.Karma += 1; b.Karma += 1;
                    Favors++;
                    if (_rng.NextDouble() < 0.25)
                        Log.Add($"Y{Year} {a.Name} 与 {b.Name} 交换灵石，两相得利");
                }
            }
        }

        // 组织：记恩成网 → 结社（个体→群体→组织）
        if (Year >= _nextFactionAt && Factions.Count < 6)
        {
            var seedNpc = Npcs.Find(n => n.Alive && n.Favor > 0 && n.Faction < 0);
            if (seedNpc != null)
            {
                var f = new Faction
                {
                    Id = Factions.Count,
                    FoundedYear = Year,
                    Kind = R(0, 2),
                    Power = R(3, 12)
                };
                f.Name = $"{seedNpc.Name}氏" + Faction.KindNames[f.Kind];
                foreach (var n in Npcs)
                    if (n.Alive && n.Faction < 0 && n.Favor > 0 && _rng.NextDouble() < 0.45)
                    { n.Faction = f.Id; f.Members.Add(n.Id); }
                if (f.Members.Count >= 3)
                {
                    Factions.Add(f);
                    Log.Add($"Y{Year} {f.Name} 立，聚众 {f.Members.Count} 人");
                }
                _nextFactionAt = Year + R(10, 25);
            }
        }

        // 人口维持
        if (Npcs.Count < 26 && _rng.NextDouble() < 0.25) Spawn();

        // 货币：交易规模过阈值 → 一般等价物
        if (CurrencyWord == "" && Favors > 60)
        {
            CurrencyWord = "灵石";
            Log.Add($"Y{Year} 交易日盛，{CurrencyWord} 渐为众所共认");
        }
    }

    public int AliveCount
    {
        get { int c = 0; foreach (var n in Npcs) if (n.Alive) c++; return c; }
    }
}
