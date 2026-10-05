// 행인1의 메인이벤트 — 1구역 배치도(zone1.json) 읽기
// 데이터 규칙은 haengin1/docs/06_M1_그레이박스_설계.md 1장·14장. 원점 = 혜화동 로터리, +X 동, +Y 위, +Z 북, 단위 m.
// yaw = Unity Y 회전(도, 위에서 볼 때 시계 방향 +). 0 이면 size[0] 이 +X, size[2] 가 +Z.
// Zone1Builder(장면 생성)와 이후 M1 리그 설정(스폰·문 위치)이 같은 읽기 코드를 쓴다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Haengin.EditorTools
{
    public sealed class Zone1Data
    {
        public const string DefaultPath = "Assets/_Project/Data/zone1.json";

        public string Name, Version;
        public float SlopeLimit = 35f, StepOffset = 0.3f, SiwooHeight = 1.74f, Walk = 1.6f, Run = 4.5f;
        public Vector3 BoundsMin, BoundsMax;
        public HeightGrid Grid;
        public readonly List<Pad> Pads = new List<Pad>();
        public readonly List<Road> Roads = new List<Road>();
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Stair> Stairs = new List<Stair>();
        public readonly List<Landmark> Landmarks = new List<Landmark>();
        public readonly List<Checkpoint> Route = new List<Checkpoint>();
        public readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        public Vector3 SpawnPos;
        public float SpawnYaw;

        /// 2m 간격 높이 격자. H[j, i] = (x = Origin.x + i*Cell, z = Origin.y + j*Cell) 의 높이. j=0 남쪽 줄, i=0 서쪽 칸.
        public sealed class HeightGrid
        {
            public string Name;
            public Vector2 Origin;
            public float Cell;
            public int NX, NZ;
            public float[,] H;

            public Vector3 Vertex(int i, int j) => new Vector3(Origin.x + i * Cell, H[j, i], Origin.y + j * Cell);

            /// 메시와 같은 삼각 분할(대각선 (i,j)–(i+1,j+1))로 보간한 높이.
            public float Sample(float x, float z)
            {
                float fx = (x - Origin.x) / Cell, fz = (z - Origin.y) / Cell;
                int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, NX - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, NZ - 2);
                float u = fx - i, v = fz - j;
                float h00 = H[j, i], h10 = H[j, i + 1], h01 = H[j + 1, i], h11 = H[j + 1, i + 1];
                return u >= v ? h00 + (h10 - h00) * u + (h11 - h10) * v
                              : h00 + (h11 - h01) * u + (h01 - h00) * v;
            }
        }

        public sealed class Pad { public string Name, Shape, Surface; public Vector3 Center; public Vector2 Size; public float Yaw; }
        public sealed class Road { public string Name, Kind; public float Width; public Vector3[] Points; }
        public sealed class Block { public string Name, Kind, Use; public Vector3 Center, Size; public float Yaw; public int Floors; }
        public sealed class Wall { public string Name; public Vector3[] Points; public float Height = 4.5f, Thickness = 2.5f; }
        public sealed class Stair { public string Name; public Vector3 From, To; public float Width; public int Steps; }
        public sealed class Landmark { public string Name, Kind, Zone; public Vector3 Pos; public float Height, Radius; }
        public sealed class Checkpoint { public string Name, Zone; public Vector3 Pos; public float Radius; }

        // ───────────────────────── 읽기
        public static Zone1Data Load(string path = DefaultPath)
        {
            string full = Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!File.Exists(full)) throw new FileNotFoundException("1구역 배치도가 없습니다", full);
            var root = MiniJson.Parse(File.ReadAllText(full)) as Dictionary<string, object>
                       ?? throw new FormatException("zone1.json 맨 바깥이 객체가 아닙니다");
            var z = new Zone1Data();

            var meta = Obj(root, "meta");
            if (meta != null)
            {
                z.Name = Str(meta, "name"); z.Version = Str(meta, "version");
                var ctl = Obj(meta, "controller");
                if (ctl != null) { z.SlopeLimit = Num(ctl, "slopeLimitDeg", 35f); z.StepOffset = Num(ctl, "stepOffset", 0.3f); }
                var sp = Obj(meta, "speeds");
                if (sp != null) { z.Walk = Num(sp, "walk", 1.6f); z.Run = Num(sp, "run", 4.5f); }
                var ph = Obj(meta, "playerHeights");
                if (ph != null) z.SiwooHeight = Num(ph, "siwoo", 1.74f);
            }

            var b = Need(root, "bounds");
            z.BoundsMin = V3(b["min"]); z.BoundsMax = V3(b["max"]);

            foreach (var o in List(root, "ground"))
            {
                var g = (Dictionary<string, object>)o;
                string kind = Str(g, "kind");
                if (kind == "heightgrid")
                {
                    var hg = new HeightGrid
                    {
                        Name = Str(g, "name"), Origin = V2(g["origin"]), Cell = Num(g, "cell", 2f),
                        NX = (int)Num(g, "nx", 0), NZ = (int)Num(g, "nz", 0),
                    };
                    var rows = (List<object>)g["heights"];
                    if (rows.Count != hg.NZ) throw new FormatException($"heights 줄 수 {rows.Count} ≠ nz {hg.NZ}");
                    hg.H = new float[hg.NZ, hg.NX];
                    for (int j = 0; j < hg.NZ; j++)
                    {
                        var row = (List<object>)rows[j];
                        if (row.Count != hg.NX) throw new FormatException($"heights[{j}] 칸 수 {row.Count} ≠ nx {hg.NX}");
                        for (int i = 0; i < hg.NX; i++) hg.H[j, i] = F(row[i]);
                    }
                    z.Grid = hg;
                }
                else if (kind == "pad")
                {
                    z.Pads.Add(new Pad
                    {
                        Name = Str(g, "name"), Shape = Str(g, "shape") ?? "rect", Surface = Str(g, "surface") ?? "dirt",
                        Center = V3(g["center"]), Size = V2(g["size"]), Yaw = Num(g, "yaw", 0f),
                    });
                }
            }
            if (z.Grid == null) throw new FormatException("ground 에 heightgrid 가 없습니다");

            foreach (var o in List(root, "roads"))
            {
                var r = (Dictionary<string, object>)o;
                z.Roads.Add(new Road { Name = Str(r, "name"), Kind = Str(r, "kind"), Width = Num(r, "width", 2f), Points = Pts(r["points"]) });
            }
            foreach (var o in List(root, "blocks"))
            {
                var r = (Dictionary<string, object>)o;
                z.Blocks.Add(new Block
                {
                    Name = Str(r, "name"), Kind = Str(r, "kind"), Use = Str(r, "use"),
                    Center = V3(r["center"]), Size = V3(r["size"]), Yaw = Num(r, "yaw", 0f), Floors = (int)Num(r, "floors", 0f),
                });
            }
            foreach (var o in List(root, "walls"))
            {
                var r = (Dictionary<string, object>)o;
                z.Walls.Add(new Wall { Name = Str(r, "name"), Points = Pts(r["points"]), Height = Num(r, "height", 4.5f), Thickness = Num(r, "thickness", 2.5f) });
            }
            foreach (var o in List(root, "stairs"))
            {
                var r = (Dictionary<string, object>)o;
                z.Stairs.Add(new Stair { Name = Str(r, "name"), From = V3(r["from"]), To = V3(r["to"]), Width = Num(r, "width", 2f), Steps = Math.Max(1, (int)Num(r, "steps", 1f)) });
            }
            foreach (var o in List(root, "landmarks"))
            {
                var r = (Dictionary<string, object>)o;
                z.Landmarks.Add(new Landmark
                {
                    Name = Str(r, "name"), Kind = Str(r, "kind"), Zone = Str(r, "zone"),
                    Pos = V3(r["pos"]), Height = Num(r, "height", 0f), Radius = Num(r, "radius", 0f),
                });
            }
            var spawn = Need(root, "spawn");
            z.SpawnPos = V3(spawn["pos"]); z.SpawnYaw = Num(spawn, "yaw", 0f);
            foreach (var o in List(root, "route"))
            {
                var r = (Dictionary<string, object>)o;
                z.Route.Add(new Checkpoint { Name = Str(r, "name"), Zone = Str(r, "zone"), Pos = V3(r["pos"]), Radius = Num(r, "radius", 2.5f) });
            }
            var cols = Obj(root, "colors");
            if (cols != null)
                foreach (var kv in cols)
                    if (kv.Value is string hex && ColorUtility.TryParseHtmlString(hex, out var c)) z.Colors[kv.Key] = c;
            return z;
        }

        // ───────────────────────── 작은 도우미
        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;

        static Dictionary<string, object> Need(Dictionary<string, object> d, string k) =>
            Obj(d, k) ?? throw new FormatException($"zone1.json 에 '{k}' 가 없습니다");

        static List<object> List(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();

        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as string : null;

        static float Num(Dictionary<string, object> d, string k, float def) =>
            d.TryGetValue(k, out var v) && v != null && !(v is string) && !(v is List<object>) && !(v is Dictionary<string, object>) ? F(v) : def;

        static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);

        static Vector3 V3(object o)
        {
            var l = (List<object>)o;
            return new Vector3(F(l[0]), F(l[1]), l.Count > 2 ? F(l[2]) : 0f);
        }

        static Vector2 V2(object o)
        {
            var l = (List<object>)o;
            return new Vector2(F(l[0]), l.Count > 1 ? F(l[1]) : F(l[0]));
        }

        static Vector3[] Pts(object o)
        {
            var l = (List<object>)o;
            var a = new Vector3[l.Count];
            for (int i = 0; i < l.Count; i++) a[i] = V3(l[i]);
            return a;
        }
    }
}
