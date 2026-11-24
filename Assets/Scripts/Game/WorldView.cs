using System;
using System.Collections.Generic;
using UnityEngine;
using SimCore;

namespace Xianxia.Game
{
    /// <summary>
    /// 世界呈现：2.5D 正交等距低模。
    /// 全部几何在运行时生成，零美术资源：地形一张网格、地物一张网格、人物一张网格，
    /// 三个 draw call 撑起整个画面（移动端 60 帧的预算就是这么来的）。
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public World W;
        public float Cell = 1f;

        private Transform _terrainRoot, _propRoot, _pawnRoot, _selRoot;
        private Mesh _terrainMesh, _propMesh, _pawnMesh, _selMesh;
        private Material _mat;

        private readonly List<Vector3> _v = new(8192);
        private readonly List<Color32> _c = new(8192);
        private readonly List<int> _t = new(16384);

        private static readonly Color C_Grass = Hex("4F7D63");
        private static readonly Color C_GrassSummer = Hex("4F8A68");
        private static readonly Color C_GrassAutumn = Hex("8A7B4E");
        private static readonly Color C_GrassWinter = Hex("6E7E76");
        private static readonly Color C_Rock = Hex("6B6B59");
        private static readonly Color C_Sand = Hex("B09A78");
        private static readonly Color C_Water = Hex("2B5F6B");
        private static readonly Color C_Cliff = Hex("27352E");
        private static readonly Color C_Dirt = Hex("3A332B");
        private static readonly Color C_Ink = Hex("0B1210");

        private int _lastSeason = -1;
        private int _terrainDirty = 0;

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        public void Init(World world)
        {
            W = world;
            var sh = Shader.Find("Xianxia/FlatVC");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Standard");
            _mat = new Material(sh) { name = "world_flat" };
            // 不设 _mat.color：FlatVC 的颜色全走顶点色，避免找不到 _Color 属性刷日志

            _terrainRoot = MakeChild("terrain");
            _propRoot = MakeChild("props");
            _pawnRoot = MakeChild("pawns");
            _selRoot = MakeChild("selection");

            _terrainMesh = Bind(_terrainRoot, "terrainMesh");
            _propMesh = Bind(_propRoot, "propMesh");
            _pawnMesh = Bind(_pawnRoot, "pawnMesh");
            _selMesh = Bind(_selRoot, "selMesh");

            BuildTerrain();
            BuildProps();
            BuildSelection();
        }

        private Transform MakeChild(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Mesh Bind(Transform t, string name)
        {
            var mf = t.gameObject.AddComponent<MeshFilter>();
            var mr = t.gameObject.AddComponent<MeshRenderer>();
            var m = new Mesh { name = name };
            m.MarkDynamic();
            mf.sharedMesh = m;
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return m;
        }

        // ------------------------------------------------------------------ 地形
        public float HeightAt(int x, int y)
        {
            if (W == null || x < 0 || y < 0 || x >= World.W || y >= World.H) return -0.6f;
            var t = W.Tiles[x, y];
            float qi = W.Qi[x, y];
            float h = t switch
            {
                Terrain.Water => -0.34f,
                Terrain.Cliff => 1.15f + qi * 0.25f,
                Terrain.Rock => 0.52f + qi * 0.35f,
                Terrain.Sand => 0.06f,
                _ => 0.16f + qi * 0.12f
            };
            return h;
        }

        private Color TileColor(int x, int y, Terrain t)
        {
            Color c = t switch
            {
                Terrain.Water => C_Water,
                Terrain.Cliff => C_Cliff,
                Terrain.Rock => C_Rock,
                Terrain.Sand => C_Sand,
                _ => W.Season switch
                {
                    Season.Summer => C_GrassSummer,
                    Season.Autumn => C_GrassAutumn,
                    Season.Winter => C_GrassWinter,
                    _ => C_Grass
                }
            };
            // 灵气越薄越枯，破坏越重越土；四季与地脉都写进顶点色
            float qi = Mathf.Clamp01(W.Qi[x, y] / 1.2f);
            float dmg = Mathf.Clamp01(W.Dmg[x, y]);
            c = Color.Lerp(c, C_Dirt, dmg * 0.75f);
            float k = 0.86f + qi * 0.22f;
            c *= k;
            // 每格一点色差，避免整片死板
            float n = Mathf.PerlinNoise(x * 0.37f + 3.1f, y * 0.41f + 7.7f) - 0.5f;
            c += new Color(n * 0.05f, n * 0.06f, n * 0.04f, 0f);
            c.a = 1f;
            return c;
        }

        public void BuildTerrain()
        {
            if (W == null) return;
            _v.Clear(); _c.Clear(); _t.Clear();

            for (int x = 0; x < World.W; x++)
                for (int y = 0; y < World.H; y++)
                {
                    float h = HeightAt(x, y);
                    float hx1 = HeightAt(x + 1, y), hy1 = HeightAt(x, y + 1), hxy = HeightAt(x + 1, y + 1);
                    var p00 = new Vector3(x, h, y);
                    var p10 = new Vector3(x + 1, hx1, y);
                    var p01 = new Vector3(x, hy1, y + 1);
                    var p11 = new Vector3(x + 1, hxy, y + 1);
                    Color c = TileColor(x, y, W.Tiles[x, y]);
                    Quad(p00, p10, p11, p01, c);

                    // 侧面：只在与邻格有落差处生成，省一半三角形
                    SideQuad(p00, p10, h, HeightAt(x + 1, y), c, x, y, 1, 0);
                    SideQuad(p10, p11, HeightAt(x + 1, y), HeightAt(x + 1, y + 1), c, x, y, 0, 1);
                    SideQuad(p11, p01, HeightAt(x + 1, y + 1), HeightAt(x, y + 1), c, x, y, -1, 0);
                    SideQuad(p01, p00, HeightAt(x, y + 1), h, c, x, y, 0, -1);
                }

            // 地图边缘的断崖，避免"浮空棋盘"
            for (int i = 0; i < World.W; i++)
            {
                EdgeWall(i, 0, 1, 0);
                EdgeWall(i, World.H - 1, -1, 0);
                EdgeWall(0, i, 0, 1);
                EdgeWall(World.W - 1, i, 0, -1);
            }

            Upload(_terrainMesh);
            _lastSeason = (int)W.Season;
        }

        private void SideQuad(Vector3 a, Vector3 b, float ha, float hb, Color c, int x, int y, int nx, int ny)
        {
            float nb = HeightAt(x + nx, y + ny);
            if (ha - nb < 0.08f) return;
            var da = new Vector3(a.x, ha - 0.9f, a.z);
            var db = new Vector3(b.x, hb - 0.9f, b.z);
            Quad(a, b, db, da, c * 0.58f);
        }

        private void EdgeWall(int x, int y, int nx, int ny)
        {
            float h = HeightAt(x, y);
            var a = new Vector3(x, h, y);
            var b = new Vector3(x + 1, h, y);
            var c = new Vector3(x + 1, h, y + 1);
            var d = new Vector3(x, h, y + 1);
            Vector3 da, db, dc, dd;
            if (nx == 1) { da = a; db = b; dc = c; dd = d; }
            else if (nx == -1) { da = b; db = a; dc = d; dd = c; }
            else if (ny == 1) { da = b; db = c; dc = d; dd = a; }
            else { da = d; db = c; dc = b; dd = a; }
            var low = -1.6f;
            var c1 = C_Ink;
            Quad(da, db, new Vector3(db.x, low, db.z), new Vector3(da.x, low, da.z), c1);
            Quad(db, dc, new Vector3(dc.x, low, dc.z), new Vector3(db.x, low, db.z), c1 * 0.9f);
            Quad(dc, dd, new Vector3(dd.x, low, dd.z), new Vector3(dc.x, low, dc.z), c1 * 0.8f);
            Quad(dd, da, new Vector3(da.x, low, da.z), new Vector3(dd.x, low, dd.z), c1 * 0.85f);
        }

        // ------------------------------------------------------------------ 地物
        public void BuildProps()
        {
            if (W == null) return;
            _v.Clear(); _c.Clear(); _t.Clear();

            // 树木：按格散布，草原与林莽更密
            for (int x = 1; x < World.W - 1; x++)
                for (int y = 1; y < World.H - 1; y++)
                {
                    if (W.Tiles[x, y] != Terrain.Grass) continue;
                    int hsh = (x * 73856093) ^ (y * 19349663) ^ (int)W.Seed;
                    if ((hsh & 7) != 0) continue;                      // 约 1/8 的格子有树
                    float jx = ((hsh >> 3) & 15) / 15f - 0.5f;
                    float jy = ((hsh >> 7) & 15) / 15f - 0.5f;
                    float s = 0.45f + ((hsh >> 11) & 7) / 7f * 0.35f;
                    Tree(x + 0.5f + jx * 0.5f, y + 0.5f + jy * 0.5f, HeightAt(x, y), s);
                }

            foreach (var nat in W.Nations)
            {
                if (!nat.Alive) continue;
                Ring(nat.Gx, nat.Gy, 2.1f, 0.055f, HeightAt((int)nat.Gx, (int)nat.Gy) + 0.03f, C_Gold, 40);
            }

            foreach (var cl in W.Clans)
            {
                if (!cl.Alive) continue;
                float h = HeightAt((int)cl.Gx, (int)cl.Gy);
                House(cl.Gx, cl.Gy, h, cl.Royal ? C_Gold : C_Sand);
            }

            foreach (var sc in W.Sects)
            {
                if (!sc.Alive) continue;
                float h = HeightAt((int)sc.Gx, (int)sc.Gy);
                Pagoda(sc.Gx, sc.Gy, h, Mathf.Clamp(sc.Tier, 1, 4));
            }

            foreach (var cv in W.Caves)
            {
                float h = HeightAt((int)cv.Gx, (int)cv.Gy);
                Arch(cv.Gx, cv.Gy, h, cv.OwnerId >= 0 ? C_Jade : C_Rock);
            }

            Upload(_propMesh);
        }

        private static readonly Color C_Gold = Hex("D9B463");
        private static readonly Color C_Jade = Hex("8FD3B4");

        private void Tree(float x, float z, float h, float s)
        {
            Box(new Vector3(x, h + 0.22f * s, z), new Vector3(0.09f * s, 0.44f * s, 0.09f * s), Hex("4A3B2C"));
            Cone(new Vector3(x, h + 0.55f * s, z), 0.42f * s, 0.78f * s, Hex("335C46"), 6);
        }

        private void House(float x, float z, float h, Color roof)
        {
            Box(new Vector3(x, h + 0.28f, z), new Vector3(0.9f, 0.56f, 0.7f), Hex("4C5A4E"));
            Pyramid(new Vector3(x, h + 0.56f, z), 0.62f, 0.26f, 0.9f, roof);
        }

        private void Pagoda(float x, float z, float h, int tiers)
        {
            float y = h;
            float w = 0.9f;
            for (int i = 0; i < tiers; i++)
            {
                Box(new Vector3(x, y + 0.26f, z), new Vector3(w, 0.52f, w), Hex("3E4A43"));
                Pyramid(new Vector3(x, y + 0.52f, z), w * 0.72f, 0.22f, w, C_Gold * (0.85f - i * 0.08f));
                y += 0.62f;
                w *= 0.78f;
            }
            Box(new Vector3(x, y + 0.3f, z), new Vector3(0.06f, 0.6f, 0.06f), C_Gold);
        }

        private void Arch(float x, float z, float h, Color c)
        {
            Box(new Vector3(x - 0.34f, h + 0.32f, z), new Vector3(0.14f, 0.64f, 0.14f), c * 0.8f);
            Box(new Vector3(x + 0.34f, h + 0.32f, z), new Vector3(0.14f, 0.64f, 0.14f), c * 0.8f);
            Box(new Vector3(x, h + 0.66f, z), new Vector3(0.86f, 0.12f, 0.14f), c);
        }

        // ------------------------------------------------------------------ 人物
        public void RenderPawns(List<Npc> npcs, Dictionary<int, Vector3> pos, int selected, int frame)
        {
            _v.Clear(); _c.Clear(); _t.Clear();
            foreach (var n in npcs)
            {
                if (!n.Alive) continue;
                if (!pos.TryGetValue(n.Id, out var p)) continue;
                Pawn(n, p, n.Id == selected);
            }
            Upload(_pawnMesh);
            if (selected >= 0 && pos.TryGetValue(selected, out var sp))
            {
                _selRoot.position = new Vector3(sp.x, HeightAt((int)sp.x, (int)sp.z) + 0.045f, sp.z);
                _selRoot.gameObject.SetActive(true);
                float pulse = 1f + Mathf.Sin(frame * 0.06f) * 0.06f;
                _selRoot.localScale = new Vector3(pulse, 1f, pulse);
            }
            else _selRoot.gameObject.SetActive(false);
        }

        private void Pawn(Npc n, Vector3 p, bool selected)
        {
            Color robe = Color.HSVToRGB(Mathf.Repeat(n.Hue, 1f), 0.30f, 0.46f);
            robe = Color.Lerp(robe, C_Ink, 0.18f);
            Color skin = Color.HSVToRGB(0.08f, 0.22f, 0.72f);
            float h = HeightAt((int)p.x, (int)p.z);
            float height = 1.05f + n.Height * 0.55f;
            bool plant = n.Kind == BeingKind.Plant;
            bool beast = n.Kind == BeingKind.Beast;

            if (plant)
            {
                Cone(new Vector3(p.x, h + 0.22f, p.z), 0.26f, 0.5f, Hex("4E7A52"), 5);
                Cone(new Vector3(p.x + 0.14f, h + 0.16f, p.z + 0.1f), 0.16f, 0.34f, Hex("5D8C5C"), 5);
            }
            else if (beast)
            {
                Box(new Vector3(p.x, h + 0.22f, p.z), new Vector3(0.42f, 0.28f, 0.22f), robe);
                Box(new Vector3(p.x + 0.24f, h + 0.34f, p.z), new Vector3(0.18f, 0.18f, 0.18f), robe * 0.9f);
            }
            else
            {
                Box(new Vector3(p.x, h + height * 0.34f, p.z), new Vector3(0.30f, height * 0.62f, 0.22f), robe);
                Box(new Vector3(p.x, h + height * 0.82f, p.z), new Vector3(0.21f, 0.21f, 0.20f), skin);
                Box(new Vector3(p.x, h + height * 0.95f, p.z), new Vector3(0.23f, 0.06f, 0.22f), robe * 0.7f);
            }

            // 品阶：灵品戴金冠，神品脚下有金环
            if (n.Grade >= Grade.Ling)
                Box(new Vector3(p.x, h + height * 1.02f, p.z), new Vector3(0.16f, 0.05f, 0.16f), C_Gold);
            if (n.Grade == Grade.Shen)
                Ring(p.x, p.z, 0.52f, 0.04f, h + 0.02f, C_Gold, 18);
            if (selected)
                Ring(p.x, p.z, 0.46f, 0.05f, h + 0.03f, C_Jade, 18);
        }

        public void BuildSelection()
        {
            _v.Clear(); _c.Clear(); _t.Clear();
            Ring(0, 0, 0.55f, 0.05f, 0f, C_Jade, 28);
            Upload(_selMesh);
            _selRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 几何原语
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            var cc = (Color32)col;
            _c.Add(cc); _c.Add(cc); _c.Add(cc); _c.Add(cc);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        private void Box(Vector3 center, Vector3 size, Color col)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
                p[i] = center + new Vector3((i & 1) == 0 ? -hx : hx, (i & 2) == 0 ? -hy : hy, (i & 4) == 0 ? -hz : hz);
            Quad(p[2], p[3], p[7], p[6], col);                 // +Y
            Quad(p[0], p[1], p[5], p[4], col * 0.55f);         // -Y
            Quad(p[1], p[3], p[7], p[5], col * 0.82f);         // +X
            Quad(p[0], p[2], p[6], p[4], col * 0.72f);         // -X
            Quad(p[4], p[5], p[7], p[6], col * 0.92f);         // +Z
            Quad(p[0], p[1], p[3], p[2], col * 0.66f);         // -Z
        }

        private void Pyramid(Vector3 baseCenter, float radius, float height, float width, Color col)
        {
            Vector3 tip = baseCenter + new Vector3(0, height, 0);
            float hx = width * 0.5f;
            var a = baseCenter + new Vector3(-hx, 0, -hx);
            var b = baseCenter + new Vector3(hx, 0, -hx);
            var c = baseCenter + new Vector3(hx, 0, hx);
            var d = baseCenter + new Vector3(-hx, 0, hx);
            Tri(a, b, tip, col * 0.9f);
            Tri(b, c, tip, col * 0.98f);
            Tri(c, d, tip, col * 0.86f);
            Tri(d, a, tip, col * 0.78f);
        }

        private void Cone(Vector3 baseCenter, float radius, float height, Color col, int seg)
        {
            Vector3 tip = baseCenter + new Vector3(0, height, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg;
                float a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                var p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                Tri(p0, p1, tip, col * (0.85f + 0.15f * Mathf.Cos(a0)));
            }
        }

        private void Ring(float cx, float cz, float r, float w, float y, Color col, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg;
                float a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(cx + Mathf.Cos(a0) * (r - w), y, cz + Mathf.Sin(a0) * (r - w));
                var p1 = new Vector3(cx + Mathf.Cos(a0) * r, y, cz + Mathf.Sin(a0) * r);
                var p2 = new Vector3(cx + Mathf.Cos(a1) * r, y, cz + Mathf.Sin(a1) * r);
                var p3 = new Vector3(cx + Mathf.Cos(a1) * (r - w), y, cz + Mathf.Sin(a1) * (r - w));
                Quad(p0, p1, p2, p3, col);
            }
        }

        private void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c);
            var cc = (Color32)col;
            _c.Add(cc); _c.Add(cc); _c.Add(cc);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
        }

        private void Upload(Mesh m)
        {
            m.Clear();
            if (_v.Count == 0) return;
            m.SetVertices(_v);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
        }

        /// <summary>季节或地脉变化时重建地形；不是每帧。</summary>
        public void Tick()
        {
            if (W == null) return;
            _terrainDirty++;
            if (_lastSeason != (int)W.Season || _terrainDirty > 240)
            {
                BuildTerrain();
                _terrainDirty = 0;
            }
        }
    }
}