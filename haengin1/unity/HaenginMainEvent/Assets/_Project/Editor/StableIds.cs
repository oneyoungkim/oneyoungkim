// 행인1의 메인이벤트 — 생성 장면·서브 에셋의 fileID 를 결정적으로 바꾸기 (Zone1Builder 가 저장 직후 부른다)
// Unity 는 새로 만든 오브젝트·서브 에셋에 무작위 fileID 를 매겨서, 장면을 다시 만들 때마다 Zone1.unity·Zone1_Mesh.asset 전체가 바뀐다.
// 여기서는 저장된 YAML(Force Text)을 읽어
//   장면: GameObject = 계층 경로(이름#같은 이름 순번) 해시로 64 칸 묶음을 잡고, 컴포넌트는 m_Component 순서대로 +1, +2 …
//         PrefabInstance = 원본 프리팹 GUID#순번, stripped 자리표시 = +1 … (원본 fileID 순)
//   메시 에셋: 서브 에셋 = 메시 이름 해시(주 오브젝트 4300000 은 그대로)
// 로 바꾸고 fileID 순서로 다시 정렬해 쓴다(Unity 가 저장할 때와 같은 순서). 같은 데이터면 같은 파일이 나온다.
// 장면 안 참조 {fileID: N} 과 메시 에셋 참조 {fileID: N, guid: <메시 GUID>} 를 함께 고친다. 다른 에셋 참조(guid 있는 것)는 건드리지 않는다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Haengin.EditorTools
{
    public static class StableIds
    {
        sealed class Doc
        {
            public int ClassId;
            public long Id, NewId;
            public bool Stripped;
            public string Head;          // "--- !u!1 &123" 뒤에 붙는 나머지(" stripped" 등)
            public List<string> Lines = new List<string>();
            public string Key;
        }

        static readonly Regex HeadRx = new Regex(@"^--- !u!(\d+) &(-?\d+)(.*)$", RegexOptions.Compiled);
        static readonly Regex LocalRef = new Regex(@"\{fileID: (-?\d+)\}", RegexOptions.Compiled);
        static readonly Regex FieldRef = new Regex(@"^\s*(m_GameObject|m_Father|m_PrefabInstance|m_TransformParent): \{fileID: (-?\d+)\}", RegexOptions.Compiled);
        static readonly Regex ListRef = new Regex(@"^\s*- (?:component: )?\{fileID: (-?\d+)\}\s*$", RegexOptions.Compiled);
        static readonly Regex SrcRef = new Regex(@"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: ([0-9a-f]+)", RegexOptions.Compiled);
        static readonly Regex PrefabSrc = new Regex(@"^\s*m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]+)", RegexOptions.Compiled);

        static (string header, List<Doc> docs) Read(string path)
        {
            var lines = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n").Split('\n');
            var header = new StringBuilder();
            var docs = new List<Doc>();
            Doc cur = null;
            foreach (var l in lines)
            {
                var m = HeadRx.Match(l);
                if (m.Success)
                {
                    cur = new Doc
                    {
                        ClassId = int.Parse(m.Groups[1].Value), Id = long.Parse(m.Groups[2].Value), Head = m.Groups[3].Value,
                        Stripped = m.Groups[3].Value.Contains("stripped"),
                    };
                    docs.Add(cur);
                }
                else if (cur == null) header.Append(l).Append('\n');
                else cur.Lines.Add(l);
            }
            // 마지막 줄바꿈 뒤 빈 줄 정리
            if (docs.Count > 0)
                while (docs[docs.Count - 1].Lines.Count > 0 && docs[docs.Count - 1].Lines[docs[docs.Count - 1].Lines.Count - 1].Length == 0)
                    docs[docs.Count - 1].Lines.RemoveAt(docs[docs.Count - 1].Lines.Count - 1);
            return (header.ToString(), docs);
        }

        static void Write(string path, string header, List<Doc> docs)
        {
            var sb = new StringBuilder(header);
            foreach (var d in docs.OrderBy(d => d.NewId))
            {
                sb.Append("--- !u!").Append(d.ClassId).Append(" &").Append(d.NewId).Append(d.Head).Append('\n');
                foreach (var l in d.Lines) sb.Append(l).Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        static ulong Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 1099511628211UL; }
            return h;
        }

        static long Field(Doc d, string name)
        {
            foreach (var l in d.Lines)
            {
                var m = FieldRef.Match(l);
                if (m.Success && m.Groups[1].Value == name) return long.Parse(m.Groups[2].Value);
            }
            return 0;
        }

        static List<long> List(Doc d, string name)
        {
            var r = new List<long>();
            bool inList = false;
            foreach (var l in d.Lines)
            {
                if (l.StartsWith("  " + name + ":")) { inList = !l.TrimEnd().EndsWith("[]"); continue; }
                if (!inList) continue;
                var m = ListRef.Match(l);
                if (m.Success) r.Add(long.Parse(m.Groups[1].Value));
                else break;
            }
            return r;
        }

        static string Name(Doc d)
        {
            foreach (var l in d.Lines)
                if (l.StartsWith("  m_Name: ")) return l.Substring(10);
            return "";
        }

        // ───────────────────────── 메시 에셋
        /// 메시 에셋의 서브 에셋 fileID 를 이름 해시로. 돌려주는 사전 = 옛 ID → 새 ID(장면 참조 고칠 때 씀).
        public static Dictionary<long, long> RewriteSubAssets(string assetPath, long mainId, out int count)
        {
            var (header, docs) = Read(assetPath);
            var map = new Dictionary<long, long>();
            var used = new HashSet<long> { mainId };
            var seen = new Dictionary<string, int>();
            foreach (var d in docs.OrderBy(d => Name(d), StringComparer.Ordinal).ThenBy(d => d.Id))
            {
                if (d.Id == mainId) { d.NewId = d.Id; continue; }
                string n = Name(d);
                seen.TryGetValue(n, out int k); seen[n] = k + 1;
                long id = (long)Fnv($"{d.ClassId}:{n}#{k}");
                while (id == 0 || Math.Abs(id) < 100000000L || used.Contains(id)) id = (long)Fnv(id.ToString());
                used.Add(id);
                d.NewId = id;
                map[d.Id] = id;
            }
            foreach (var d in docs)
                for (int i = 0; i < d.Lines.Count; i++)
                    d.Lines[i] = LocalRef.Replace(d.Lines[i], m => map.TryGetValue(long.Parse(m.Groups[1].Value), out var n) ? $"{{fileID: {n}}}" : m.Value);
            Write(assetPath, header, docs);
            count = map.Count;
            return map;
        }

        // ───────────────────────── 장면
        /// 장면 fileID 를 계층 경로 기준으로. extGuid 에셋(생성 메시)을 가리키는 참조는 extMap 으로 함께 고친다.
        /// 돌려주는 값: (바꾼 오브젝트 수, 경로를 못 정해 그대로 둔 수)
        public static (int changed, int kept) RewriteScene(string scenePath, string extGuid, Dictionary<long, long> extMap)
        {
            var (header, docs) = Read(scenePath);
            var byId = docs.ToDictionary(d => d.Id);

            // 1) GameObject 경로 키: SceneRoots 순서 → Transform 자식 순서
            var goKey = new Dictionary<long, string>();
            var trOfGo = new Dictionary<long, long>();
            foreach (var d in docs.Where(d => (d.ClassId == 4 || d.ClassId == 224) && !d.Stripped))
                trOfGo[Field(d, "m_GameObject")] = d.Id;
            var piKey = new Dictionary<long, string>();

            void Walk(long trId, string parentKey, Dictionary<string, int> sib)
            {
                if (!byId.TryGetValue(trId, out var tr)) return;
                if (tr.Stripped) return;
                long go = Field(tr, "m_GameObject");
                if (!byId.TryGetValue(go, out var god)) return;
                string n = Name(god);
                sib.TryGetValue(n, out int k); sib[n] = k + 1;
                string key = parentKey + "/" + n + "#" + k;
                goKey[go] = key;
                var childSib = new Dictionary<string, int>();
                foreach (var c in List(tr, "m_Children")) Walk(c, key, childSib);
                // 이 Transform 밑에 붙은 프리팹 인스턴스(m_TransformParent = 이 Transform)
                int pk = 0;
                foreach (var pi in docs.Where(x => x.ClassId == 1001 && Field(x, "m_TransformParent") == trId))
                    piKey[pi.Id] = key + "/<prefab>" + PrefabGuid(pi) + "#" + pk++;
            }

            var roots = docs.FirstOrDefault(d => d.ClassId == 1660057539);
            var rootSib = new Dictionary<string, int>();
            int rootPi = 0;
            if (roots != null)
                foreach (var r in List(roots, "m_Roots"))
                {
                    if (!byId.TryGetValue(r, out var rd)) continue;
                    if (rd.ClassId == 1001) piKey[rd.Id] = "/<prefab>" + PrefabGuid(rd) + "#" + rootPi++;
                    else Walk(r, "", rootSib);
                }

            // 2) ID 배정: 키 순서대로 64 칸 묶음(겹치면 다음 묶음)
            const long Lo = 100000000L, Slots = (int.MaxValue - Lo) / 64;
            var used = new HashSet<long>(docs.Where(d => d.Id <= 100 || d.Id == long.MaxValue).Select(d => d.Id));
            var map = new Dictionary<long, long>();
            long Block(string key)
            {
                long b = Lo + (long)(Fnv(key) % (ulong)Slots) * 64;
                while (used.Contains(b)) b = b + 64 >= int.MaxValue ? Lo : b + 64;
                for (int i = 0; i < 64; i++) used.Add(b + i);
                return b;
            }
            foreach (var kv in goKey.OrderBy(kv => kv.Value, StringComparer.Ordinal))
            {
                long b = Block("go:" + kv.Value);
                map[kv.Key] = b;
                var comps = List(byId[kv.Key], "m_Component");
                for (int i = 0; i < comps.Count && i < 63; i++) map[comps[i]] = b + 1 + i;
            }
            foreach (var kv in piKey.OrderBy(kv => kv.Value, StringComparer.Ordinal))
            {
                long b = Block("pi:" + kv.Value);
                map[kv.Key] = b;
                var stripped = docs.Where(d => d.Stripped && Field(d, "m_PrefabInstance") == kv.Key)
                                   .OrderBy(d => SourceId(d)).ThenBy(d => d.ClassId).ToList();
                for (int i = 0; i < stripped.Count && i < 63; i++) map[stripped[i].Id] = b + 1 + i;
            }
            // 프리팹 인스턴스에 더한 컴포넌트·남은 것: 붙은 GameObject 키 + 종류로
            int kept = 0;
            foreach (var d in docs.Where(d => !map.ContainsKey(d.Id) && d.Id > 100 && d.Id != long.MaxValue).OrderBy(d => d.Id))
            {
                long go = Field(d, "m_GameObject");
                if (go != 0 && map.TryGetValue(go, out var gid))
                {
                    long id = (long)(Fnv($"add:{gid}:{d.ClassId}:{d.Lines.Count}") % (ulong)(int.MaxValue - Lo)) + Lo;
                    while (used.Contains(id)) id++;
                    used.Add(id); map[d.Id] = id;
                }
                else kept++;
            }
            foreach (var d in docs) d.NewId = map.TryGetValue(d.Id, out var n) ? n : d.Id;
            if (docs.Select(d => d.NewId).Distinct().Count() != docs.Count) throw new Exception("StableIds: 새 fileID 가 겹칩니다");

            // 3) 참조 고치기
            var extRx = string.IsNullOrEmpty(extGuid) ? null : new Regex(@"\{fileID: (-?\d+), guid: " + extGuid + @", type: (\d+)\}");
            foreach (var d in docs)
                for (int i = 0; i < d.Lines.Count; i++)
                {
                    string l = LocalRef.Replace(d.Lines[i], m => map.TryGetValue(long.Parse(m.Groups[1].Value), out var n) ? $"{{fileID: {n}}}" : m.Value);
                    if (extRx != null && extMap != null && l.Contains(extGuid))
                        l = extRx.Replace(l, m => extMap.TryGetValue(long.Parse(m.Groups[1].Value), out var n)
                            ? $"{{fileID: {n}, guid: {extGuid}, type: {m.Groups[2].Value}}}" : m.Value);
                    d.Lines[i] = l;
                }
            Write(scenePath, header, docs);
            return (map.Count, kept);
        }

        static string PrefabGuid(Doc pi)
        {
            foreach (var l in pi.Lines)
            {
                var m = PrefabSrc.Match(l);
                if (m.Success) return m.Groups[1].Value;
            }
            return "?";
        }

        static long SourceId(Doc d)
        {
            foreach (var l in d.Lines)
            {
                var m = SrcRef.Match(l);
                if (m.Success) return long.Parse(m.Groups[1].Value);
            }
            return 0;
        }
    }
}
