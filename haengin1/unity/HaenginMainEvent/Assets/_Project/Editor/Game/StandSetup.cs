// 행인1의 메인이벤트 — 서 있는 자세(2026-10-31 대표 "서 있는 자세가 전부 이상하고 등이 굽어 보인다"): 243 Idle_3 은 어깨가 말리고 머리가 앞으로 나오고 팔이 벌어지고 무릎이 굽은 동작이라
// 인물마다 자기 아바타에 맞춰 Humanoid 근육 값으로 전용 대기 클립을 굽는다(크레딧 0). Anim/Stand/Stand_<인물>.anim
//   만드는 법: 바인드 포즈 → 뼈 방향을 곧은 자세로(가슴 펴기·어깨 내려 뒤로·턱 당기기·팔 몸 옆·무릎 거의 펴기·발 어깨 너비) → HumanPoseHandler 로 근육 값 → 숨쉬기·무게 이동만 아주 작게 → 발바닥을 바닥에.
//   재기: Measure — 뼈와 구운 메시로 귀~어깨·어깨~엉덩이 앞뒤, 무릎, 위팔 벌어짐, 손 자리, 좌우 어깨 높이(원본 Idle 과 나란히).
//   배치: StandSetup.Build(굽기 + 컨트롤러 연결), StandSetup.MeasureOnly(재기만) · -standout <폴더> 에 stand_measure.tsv
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Haengin.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Haengin.EditorGame
{
    public static class StandSetup
    {
        const string Tag = "[Stand]";
        public const string Dir = "Assets/_Project/Anim/Stand";

        /// 인물별 자세 값. 기본 = 곧게 선 자세, 체형·성격 차이만 여기서
        public sealed class Tune
        {
            public float ArmOut = 7f;        // 위팔 벌어짐(도, 앞에서 본 몸통선 기준)
            public float HandFwd = 0.03f;    // 손목을 엉덩이보다 앞으로(m) — 위팔 앞뒤 기울기를 이 값에 맞춰 풂(손이 허벅지 옆)
            public float Elbow = 6f;         // 팔꿈치: 휴지 자세보다 더 굽힘(도)
            public float ForearmIn = 3f;     // 아래팔을 몸 쪽으로(도, 손이 허벅지 옆에 오게)
            public float Knee = -2f;         // 무릎: 휴지 자세보다 더 굽힘(도) — 자동 리깅 무릎 뼈가 앞으로 나와 있어 뼈 각도는 휴지에서 이미 10~33°(메시 다리는 곧음). Foot IK 가 +1~6° 더함
            public float FootSep = 0.80f;    // 발목 간격 ÷ 어깨 관절 간격(발 바깥 가장자리 ≈ 어깨 너비)
            public float FootOut = 7f;       // 발끝 바깥으로(도)
            public float ChestBack = 2f;     // 윗가슴 뒤로(도, 가슴 펴기)
            public float ClavDrop = 3f;      // 빗장뼈 휴지보다 아래로(도)
            public float ClavBack = 6f;      // 빗장뼈 휴지보다 뒤로(도)
            public float ShoulderFwd = 0f;   // 어깨 중심을 엉덩이보다 앞으로(m)
            public float EarFwd = 0.01f;     // 머리 가운데를 어깨보다 앞으로(m)
            public float HeadPitch = 0f;     // 머리 앞(+) 숙임 보정(도)
            public float Weight = 0f;        // 무게 치우침(m, + = 캐릭터 오른쪽(+X))
            public float KneeSide = 0f;      // 무게를 뺀 쪽 무릎 더 굽힘(도, Weight 쪽 반대)
            public float Pelvis = 0f;        // 골반 기울기(도, 무게 실은 쪽이 위로)
            public float RelaxFoot = 0f;     // 무게 뺀 발을 앞으로(m)
            public float ShoulderTilt = 0f;  // 어깨 기울기(도, + = 캐릭터 오른쪽 어깨가 위)
            public float HeadYaw = 0f;       // 고개 돌림(도, + = 캐릭터 오른쪽으로)
            public float HeadRoll = 0f;      // 고개 기울임(도, + = 오른쪽 귀가 위)
            public float RelaxOut = 3f;      // 무게 뺀 발끝을 더 바깥으로(도)
            public float StanceZ = 0f;       // 앞뒤로 벌린 발(m, + = 왼발 앞·오른발 뒤)
            public float TorsoYaw = 0f;      // 몸통 돌림(도, + = 왼쪽 어깨가 앞 — 왼발 앞 자세)
            public float HeelLift = 0f;      // 뒤꿈치 들기(도 — 앞꿈치 무게)
            public float ElbowL = 0f, ElbowR = 0f, ArmOutL = 0f, ArmOutR = 0f, HandFwdL = 0f, HandFwdR = 0f;   // 양팔 비대칭(더하는 값)
            public float SwayAmp = 1f;       // 건들거림 크기(등·목 좌우 곡선 배수 — 뿌리는 그대로, 발이 미끄러지지 않게)
            public float BreathSec = 4f, SwaySec = 8f, Phase = 0f;   // 숨쉬기·무게 이동 주기(초)와 시작 시점 — 함께 흔들리지 않게
            public Tune Copy() => (Tune)MemberwiseClone();
        }

        public sealed class Who
        {
            public string Key, Fbx, Label, Group;
            public Tune T = new Tune();
        }

        public static List<Who> All()
        {
            var r = new List<Who>();
            void Add(string key, string fbx, string label, string group, Action<Tune> tune = null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) { Debug.LogWarning($"{Tag} 모델 없음: {fbx}"); return; }
                var w = new Who { Key = key, Fbx = fbx, Label = label, Group = group };
                tune?.Invoke(w.T);
                r.Add(w);
            }
            // 인물별 차이(2026-10-31 대표): 곧은 몸에서 좌우로만. 이윤준 = 여유, 시우 = 한쪽 다리에 뚜렷하게, 곽반장·아빠 = 묵직, 할머니 = 약간 낮게
            Add("Yunjun_v2", GymSetup.NpcFbx("Yunjun_v2"), "이윤준", "코치", t => { t.Weight = 0.07f; t.Pelvis = 5f; t.KneeSide = 15f; t.ShoulderTilt = -4f; t.HeadYaw = 12f; t.HeadRoll = 4f; t.FootSep = 1.05f; t.FootOut = 14f; t.RelaxFoot = 0.07f; t.RelaxOut = 14f; t.Elbow = 9f; t.ElbowL = 28f; t.ArmOutL = 6f; t.SwayAmp = 1.5f; t.BreathSec = 4.4f; t.SwaySec = 8.8f; });
            Add("Seungyeon_v2", GymSetup.NpcFbx("Seungyeon_v2"), "김승연", "코치", t => { t.FootSep = 1.1f; t.FootOut = 14f; t.Weight = -0.02f; t.Pelvis = 1.5f; t.KneeSide = 4f; t.HeadYaw = -10f; t.HeadPitch = 1f; t.ChestBack = 3f; t.ArmOut = 9f; t.ForearmIn = 6f; t.Elbow = 14f; t.ElbowR = 10f; t.BreathSec = 4.8f; t.SwaySec = 9.6f; });
            Add("JinsooSon_v2", GymSetup.NpcFbx("JinsooSon_v2"), "손진수", "코치", t => { t.FootSep = 0.8f; t.FootOut = 5f; t.Knee = 6f; t.StanceZ = 0.04f; t.TorsoYaw = 4f; t.Elbow = 22f; t.ElbowR = 8f; t.ArmOut = 8f; t.ForearmIn = 8f; t.HeadYaw = 6f; t.HeadPitch = 2f; t.ShoulderFwd = 0.012f; t.Weight = 0.012f; t.Pelvis = 1.5f; t.KneeSide = 3f; t.SwayAmp = 2f; t.BreathSec = 3.2f; t.SwaySec = 6.4f; });
            Add("JinsooSeo_v2", GymSetup.NpcFbx("JinsooSeo_v2"), "서진수", "코치", t => { t.ShoulderTilt = 3.8f; t.Weight = -0.05f; t.Pelvis = 4f; t.KneeSide = 8f; t.HeadRoll = -3f; t.HeadYaw = 9f; t.FootOut = 14f; t.RelaxFoot = 0.04f; t.RelaxOut = 10f; t.FootSep = 1.05f; t.ElbowL = 4f; t.ElbowR = 12f; t.ArmOutR = 3f; t.SwayAmp = 2.5f; t.BreathSec = 4.2f; t.SwaySec = 8.4f; });
            Add("SeungminBaek", GymSetup.NpcFbx("SeungminBaek"), "백승민", "코치", t => { t.StanceZ = -0.09f; t.TorsoYaw = -8f; t.FootSep = 0.92f; t.FootOut = 9f; t.Weight = -0.03f; t.Pelvis = 3f; t.KneeSide = 8f; t.HeadYaw = 8f; t.HeadPitch = 1f; t.Elbow = 12f; t.ElbowR = 8f; t.ForearmIn = 5f; t.HandFwd = 0.06f; t.HandFwdR = 0.04f; t.SwayAmp = 1.5f; t.BreathSec = 4.6f; t.SwaySec = 9.2f; });
            Add("Gojun", GymSetup.NpcFbx("Gojun"), "고준", "코치", t => { t.FootSep = 1f; t.FootOut = 6f; t.ChestBack = 4f; t.Elbow = 4f; t.ArmOut = 5f; t.SwayAmp = 0.5f; t.BreathSec = 3.6f; t.SwaySec = 7.2f; });
            Add("Siwoo", CharSetup.SiwooFbx, "시우", "주인공", t => { t.Weight = 0.09f; t.Pelvis = 6f; t.KneeSide = 26f; t.ShoulderTilt = -5f; t.HeadYaw = -9f; t.HeadPitch = 7f; t.EarFwd = -0.012f; t.HeadRoll = -4f; t.RelaxFoot = 0.10f; t.RelaxOut = 14f; t.FootOut = 14f; t.ElbowL = 18f; t.ArmOutL = 4f; t.SwayAmp = 1.5f; });    // 시우 = 한쪽 다리에 뚜렷하게(246 의 성격)
            Add("Taeo", CharSetup.TaeoFbx, "태오", "주인공", t => { t.ChestBack = 9f; t.HeadPitch = -10f; t.ClavBack = 12f; t.FootSep = 1.3f; t.FootOut = 14f; t.ArmOut = 8f; t.ForearmIn = 7f; t.Elbow = 10f; t.Weight = -0.01f; t.BreathSec = 3.7f; t.SwaySec = 7.4f; });
            var tuneE = new Dictionary<string, Action<Tune>>
            {
                ["Kkanjok"] = t => { t.Weight = -0.08f; t.Pelvis = 6f; t.KneeSide = 24f; t.ShoulderTilt = 6f; t.HeadYaw = 14f; t.HeadRoll = -6f; t.RelaxFoot = 0.09f; t.RelaxOut = 14f; t.ElbowL = 18f; t.ArmOutL = 6f; t.SwayAmp = 3.5f; t.BreathSec = 3.4f; t.SwaySec = 6.8f; },
                ["Seokdal"] = t => { t.StanceZ = 0.08f; t.TorsoYaw = 7f; t.FootSep = 0.95f; t.FootOut = 10f; t.Elbow = 16f; t.ElbowR = 8f; t.ForearmIn = 5f; t.HandFwd = 0.06f; t.HandFwdL = 0.03f; t.HeadYaw = -9f; t.HeadPitch = 3f; t.Weight = 0.02f; t.KneeSide = 5f; t.BreathSec = 3.8f; t.SwaySec = 7.6f; },
                ["Naengjanggo"] = t => { t.FootSep = 1.55f; t.FootOut = 18f; t.ArmOut = 5f; t.ForearmIn = 2f; t.Knee = 6f; t.HeadPitch = 2.5f; t.EarFwd = 0f; t.Weight = 0.006f; t.BreathSec = 5f; t.SwaySec = 10f; },
                ["Scrum"] = t => { t.FootSep = 1.05f; t.FootOut = 10f; t.ArmOut = 5f; t.ForearmIn = 2f; t.Elbow = 26f; t.ChestBack = 4f; t.HeadPitch = -5f; t.BreathSec = 4.5f; t.SwaySec = 9f; },
            };
            var tuneC = new Dictionary<string, Action<Tune>>
            {
                ["Mom"] = t => { t.Weight = 0.06f; t.Pelvis = 5f; t.KneeSide = 16f; t.ShoulderTilt = -3f; t.RelaxFoot = 0.07f; t.RelaxOut = 10f; t.HeadRoll = 4f; t.HeadYaw = 6f; t.Elbow = 20f; t.ElbowR = 18f; t.FootSep = 0.75f; t.FootOut = 6f; t.BreathSec = 4.2f; t.SwaySec = 8.4f; },
                ["Haneul"] = t => { t.FootSep = 0.55f; t.FootOut = -6f; t.Weight = -0.03f; t.Pelvis = 3f; t.KneeSide = 10f; t.HeadRoll = 6f; t.HeadYaw = 5f; t.Elbow = 16f; t.ArmOut = 8f; t.SwayAmp = 1.5f; t.BreathSec = 3.2f; t.SwaySec = 6.4f; },
                ["Dokyungmin"] = t => { t.Weight = 0.045f; t.Pelvis = 4f; t.KneeSide = 12f; t.ShoulderTilt = -3f; t.FootSep = 0.8f; t.Elbow = 16f; t.ElbowR = 20f; t.HeadYaw = -7f; t.HeadRoll = 2f; t.BreathSec = 4.4f; t.SwaySec = 8.8f; },
                ["Minjae"] = t => { t.Weight = 0.07f; t.Pelvis = 6f; t.KneeSide = 20f; t.ShoulderTilt = -4f; t.HeadPitch = -10f; t.HeadYaw = 12f; t.HeadRoll = -5f; t.FootOut = 18f; t.RelaxFoot = 0.08f; t.RelaxOut = 14f; t.FootSep = 1.2f; t.ArmOutR = 4f; t.SwayAmp = 3.5f; t.BreathSec = 3.6f; t.SwaySec = 7.2f; },
                ["Geonwoo"] = t => { t.FootSep = 1.08f; t.FootOut = 14f; t.ArmOut = 5f; t.ForearmIn = 2f; t.Knee = 4f; t.Elbow = 10f; t.HeadPitch = 4f; t.BreathSec = 5f; t.SwaySec = 10f; },
                ["StudentM"] = t => { t.Weight = -0.05f; t.Pelvis = 5f; t.KneeSide = 16f; t.ShoulderTilt = 4f; t.HeadPitch = 6f; t.HeadYaw = -7f; t.RelaxFoot = 0.06f; t.ElbowL = 10f; t.BreathSec = 3.8f; t.SwaySec = 7.6f; },
                ["StudentF"] = t => { t.FootSep = 0.5f; t.FootOut = -2f; t.Weight = 0.03f; t.Pelvis = 3f; t.KneeSide = 10f; t.HeadRoll = -6f; t.ElbowL = 16f; t.BreathSec = 3.6f; t.SwaySec = 7.2f; },
                ["Gwak"] = t => { t.FootSep = 1.08f; t.FootOut = 14f; t.Knee = 4f; t.Elbow = 20f; t.ArmOut = 9f; t.ForearmIn = 6f; t.ChestBack = 2f; t.HeadPitch = 2f; t.Weight = 0.006f; t.BreathSec = 5.2f; t.SwaySec = 10.4f; },
                ["Halmeoni"] = t => { t.FootSep = 0.5f; t.FootOut = 8f; t.Knee = 16f; t.ClavDrop = 7f; t.ChestBack = 1f; t.HeadPitch = 3f; t.ArmOut = 6f; t.Elbow = 26f; t.HeadYaw = -5f; t.BreathSec = 4.6f; t.SwaySec = 9.2f; },
                ["BakeryStaff"] = t => { t.Weight = -0.06f; t.Pelvis = 5f; t.KneeSide = 16f; t.ShoulderTilt = 3f; t.RelaxFoot = 0.07f; t.RelaxOut = 10f; t.HeadRoll = -4f; t.HeadYaw = 5f; t.FootSep = 0.7f; t.FootOut = 5f; t.Elbow = 18f; t.ElbowL = 16f; t.BreathSec = 4f; t.SwaySec = 8f; },
                ["Dad"] = t => { t.FootSep = 1.1f; t.FootOut = 14f; t.Knee = 4f; t.Elbow = 20f; t.ArmOut = 9f; t.ForearmIn = 6f; t.ChestBack = 2f; t.Weight = 0.012f; t.HeadPitch = 2f; t.BreathSec = 5f; t.SwaySec = 10f; },
            };
            foreach (var en in ClipSetup.Enemies) Add(en, ClipSetup.EnemyFbx(en), "관원(" + en + ")", "관원", tuneE.TryGetValue(en, out var te) ? te : null);
            foreach (var (k, n) in CastSetup.Cast) Add(k, CastSetup.Fbx(k), n, "조연", tuneC.TryGetValue(k, out var tc) ? tc : null);
            for (int i = 0; i < r.Count; i++) r[i].T.Phase = i * 2.399f;      // 시작 시점(황금각 — 서로 어긋남)
            return r;
        }

        public static string ClipPath(string key) => $"{Dir}/Stand_{key}.anim";
        /// 전투 밖 살짝 쥔 손(Grip 셰이프 키 무게 %, HandShape.Relax)
        public const float HandRelax = 38f;
        /// 살짝 쥔 손을 안 쓰는 인물(셰이프 키가 손을 망가뜨림 — docs/15 8-9)
        public static readonly HashSet<string> NoRelax = new HashSet<string> { "Taeo", "Mom", "Haneul", "Dokyungmin", "Minjae", "Geonwoo", "StudentM", "StudentF", "Halmeoni", "BakeryStaff", "Dad" };

        /// 소품 focus_mitts_pair(두 짝이 한 메시) → 한 짝씩 메시 애셋. 메시 축 = 패드 +Y · 손끝 −Z · 원점 = 한 짝 가운데
        public static (Mesh l, Mesh r, Material mat) MittMeshes()
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Gym/P_focus_mitts_pair.prefab");
            if (pf == null) { Debug.LogWarning($"{Tag} 미트 프리팹 없음"); return (null, null, null); }
            var mfs = pf.GetComponentsInChildren<MeshFilter>(true);
            var mf = mfs.FirstOrDefault(m => m.sharedMesh != null && m.name.Contains("LOD1")) ?? mfs.FirstOrDefault(m => m.sharedMesh != null);
            var src = mf.sharedMesh;
            var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
            if (!src.isReadable) { Debug.LogError($"{Tag} 미트 메시를 읽을 수 없음: {src.name}"); return (null, null, mat); }
            var v = src.vertices; var n = src.normals; var t4 = src.tangents; var uv = src.uv; var tris = src.triangles;
            Mesh Part(float sign, string path)
            {
                var map = new Dictionary<int, int>();
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nu = new List<Vector2>(); var ni = new List<int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = (v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f;
                    if (Mathf.Sign(c.x) != sign) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int o = tris[i + k];
                        if (!map.TryGetValue(o, out int m)) { m = nv.Count; map[o] = m; nv.Add(v[o]); nn.Add(n[o]); if (t4.Length > 0) nt.Add(t4[o]); nu.Add(uv[o]); }
                        ni.Add(m);
                    }
                }
                var b = new Bounds(nv[0], Vector3.zero); foreach (var q in nv) b.Encapsulate(q);
                for (int i = 0; i < nv.Count; i++) nv[i] -= b.center;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool isNew = mesh == null;
                if (isNew) mesh = new Mesh();
                mesh.Clear();
                mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
                mesh.SetVertices(nv); mesh.SetNormals(nn); if (nt.Count > 0) mesh.SetTangents(nt); mesh.SetUVs(0, nu); mesh.SetTriangles(ni, 0);
                mesh.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                return mesh;
            }
            const string P = "Assets/_Project/Art/Env/Gym/Props/";
            var ml = Part(-1f, P + "Mitt_L.asset"); var mr = Part(1f, P + "Mitt_R.asset");
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} 미트 메시: {src.name} {v.Length}정점 → 왼 {ml.vertexCount} · 오른 {mr.vertexCount}");
            return (ml, mr, mat);
        }

        /// 프리팹 HandShape 살짝 쥔 손(배역·태오)
        static void ApplyHands()
        {
            var paths = CastSetup.Cast.Select(c => CastSetup.Prefab(c.key)).Concat(new[] { FormalSetup.TaeoPrefab });
            int n = 0;
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var hs = root.GetComponentInChildren<HandShape>(true);
                    var host = hs != null ? hs.gameObject : root;
                    if (hs == null) hs = host.AddComponent<HandShape>();
                    float want = NoRelax.Contains(System.IO.Path.GetFileNameWithoutExtension(path)) ? 0f : HandRelax;
                    if (!Mathf.Approximately(hs.Relax, want)) { hs.Relax = want; PrefabUtility.SaveAsPrefabAsset(root, path); n++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log($"{Tag} 살짝 쥔 손: 프리팹 {n}개 고침(무게 {HandRelax})");
        }

        /// 설정 스크립트가 쓰는 대기 클립: 그 모델의 전용 대기가 있으면 그것, 없으면 FBX 의 Idle
        public static AnimationClip Idle(string fbx) => CharSetup.StandIdle(fbx);

        // ───────────────────────── 배치 진입
        public static void MeasureOnly() => Run(() => Report(Arg("-standout")));

        public static void Build() => Run(() =>
        {
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath), Dir));
            AssetDatabase.Refresh();
            foreach (var w in All()) { Bake(w); if (w.Group == "조연") BakeTalk(w); }
            AssetDatabase.SaveAssets();
            Apply();
            AssetDatabase.SaveAssets();
            Report(Arg("-standout"));
        });

        static void Run(Action a)
        {
            int code = 0;
            try { a(); AssetDatabase.SaveAssets(); }
            catch (Exception e) { Debug.LogError($"{Tag} 실패: {e}"); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : "";
        }

        // ───────────────────────── 재기
        public struct Posture
        {
            public float Ear, Shoulder, KneeL, KneeR, ArmL, ArmR, Arm3D, KneeDelta, HandL, HandR, HandZL, HandZR, HandYL, HandYR, ShoulderDy, Protract, SpineBend, HeadPitch, Sole, EarBone, FootWidth, MeshKneeL, MeshKneeR, BackBulge, BackLean, HipAnkle;
        }

        sealed class Rig
        {
            public GameObject Go; public Animator An; public SkinnedMeshRenderer Smr; public Mesh Baked = new Mesh();
            public Transform Root, Hips, Spine, Chest, Upper, Neck, Head, HeadEnd, LS, RS, LUA, RUA, LLA, RLA, LH, RH, LUL, RUL, LLL, RLL, LF, RF;
            public Transform B(HumanBodyBones b) => An.GetBoneTransform(b);
            public void Dispose() { Object.DestroyImmediate(Baked); Object.DestroyImmediate(Go); }
        }

        static Rig MakeRig(string fbx)
        {
            var model = CharSetup.Load<GameObject>(fbx);
            var go = Object.Instantiate(model);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var an = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            an.avatar = CharSetup.AvatarOf(fbx);
            an.applyRootMotion = false;
            an.runtimeAnimatorController = null;
            an.Rebind();
            var r = new Rig { Go = go, An = an, Smr = CharSetup.MainSkin(go), Root = go.transform };
            r.Hips = r.B(HumanBodyBones.Hips); r.Spine = r.B(HumanBodyBones.Spine); r.Chest = r.B(HumanBodyBones.Chest); r.Upper = r.B(HumanBodyBones.UpperChest);
            r.Neck = r.B(HumanBodyBones.Neck); r.Head = r.B(HumanBodyBones.Head);
            r.LS = r.B(HumanBodyBones.LeftShoulder); r.RS = r.B(HumanBodyBones.RightShoulder);
            r.LUA = r.B(HumanBodyBones.LeftUpperArm); r.RUA = r.B(HumanBodyBones.RightUpperArm); r.LLA = r.B(HumanBodyBones.LeftLowerArm); r.RLA = r.B(HumanBodyBones.RightLowerArm);
            r.LH = r.B(HumanBodyBones.LeftHand); r.RH = r.B(HumanBodyBones.RightHand);
            r.LUL = r.B(HumanBodyBones.LeftUpperLeg); r.RUL = r.B(HumanBodyBones.RightUpperLeg); r.LLL = r.B(HumanBodyBones.LeftLowerLeg); r.RLL = r.B(HumanBodyBones.RightLowerLeg);
            r.LF = r.B(HumanBodyBones.LeftFoot); r.RF = r.B(HumanBodyBones.RightFoot);
            r.HeadEnd = r.Head.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head_end");
            return r;
        }

        /// 메시의 바인드 포즈(모델 파일의 기본 자세와 다를 수 있음)
        static void PutBind(Rig r)
        {
            var bp = r.Smr.sharedMesh.bindposes;
            var bones = r.Smr.bones;
            var idx = Enumerable.Range(0, bones.Length).Where(i => bones[i] != null).OrderBy(i => { int d = 0; for (var t = bones[i]; t != null; t = t.parent) d++; return d; });
            foreach (int i in idx)
            {
                var m = r.Smr.transform.localToWorldMatrix * bp[i].inverse;
                bones[i].SetPositionAndRotation(m.GetColumn(3), m.rotation);
            }
        }

        static Vector3 Mid(Vector3 a, Vector3 b) => (a + b) * 0.5f;

        /// 머리 가운데(앞뒤): 머리 뼈 위쪽 메시의 앞뒤 끝 가운데 — 귀 자리에 가장 가까움(머리카락이 뒤로 많아도 덜 끌림)
        static Vector3 HeadCenter(Rig r)
        {
            r.Smr.BakeMesh(r.Baked, true);
            var m = r.Root.worldToLocalMatrix * r.Smr.transform.localToWorldMatrix;
            var hp = r.Root.InverseTransformPoint(r.Head.position);
            var endP = r.HeadEnd != null ? r.Root.InverseTransformPoint(r.HeadEnd.position) : hp + Vector3.up * 0.22f;
            float len = Mathf.Max(0.12f, (endP - hp).magnitude);
            var c = hp + (endP - hp) * 0.45f;
            float zMin = float.MaxValue, zMax = float.MinValue, xs = 0f, ys = 0f; int n = 0;
            foreach (var v in r.Baked.vertices)
            {
                var q = m.MultiplyPoint3x4(v);
                if ((q - c).sqrMagnitude > len * len * 0.5f) continue;
                if (q.y < hp.y + len * 0.05f) continue;
                if (q.z < zMin) zMin = q.z;
                if (q.z > zMax) zMax = q.z;
                xs += q.x; ys += q.y; n++;
            }
            return n == 0 ? c : new Vector3(xs / n, ys / n, (zMin + zMax) * 0.5f);
        }

        static float SoleY(Rig r)
        {
            r.Smr.BakeMesh(r.Baked, true);
            var m = r.Root.worldToLocalMatrix * r.Smr.transform.localToWorldMatrix;
            float min = float.MaxValue;
            foreach (var v in r.Baked.vertices) { float y = m.MultiplyPoint3x4(v).y; if (y < min) min = y; }
            return min;
        }

        static Posture Measure(Rig r)
        {
            Vector3 L(Transform t) => r.Root.InverseTransformPoint(t.position);
            var p = new Posture();
            var sh = Mid(L(r.LUA), L(r.RUA));
            var hip = Mid(L(r.LUL), L(r.RUL));
            var ear = HeadCenter(r);
            p.Ear = (ear.z - sh.z) * 100f;
            p.EarBone = (L(r.Head).z - sh.z) * 100f;
            p.Shoulder = (sh.z - hip.z) * 100f;
            float Knee(Transform a, Transform b, Transform c) => Vector3.Angle(L(b) - L(a), L(c) - L(b));
            p.KneeL = Knee(r.LUL, r.LLL, r.LF); p.KneeR = Knee(r.RUL, r.RLL, r.RF);
            var down = (hip - sh).normalized;
            // 위팔 벌어짐 = 앞에서 본 각도(몸통선 기준). Arm3D = 앞뒤 기울기까지 넣은 각도(자동 리깅 팔꿈치 뼈가 뒤로 치우쳐 휴지에서도 위팔 뼈가 뒤로 기움)
            float Front(Vector3 v) => Vector3.Angle(Vector3.ProjectOnPlane(v, Vector3.forward), Vector3.ProjectOnPlane(down, Vector3.forward));
            p.ArmL = Front(L(r.LLA) - L(r.LUA)); p.ArmR = Front(L(r.RLA) - L(r.RUA));
            p.Arm3D = Mathf.Max(Vector3.Angle(L(r.LLA) - L(r.LUA), down), Vector3.Angle(L(r.RLA) - L(r.RUA), down));
            p.HandL = Mathf.Abs(L(r.LH).x - L(r.LUL).x) * 100f; p.HandR = Mathf.Abs(L(r.RH).x - L(r.RUL).x) * 100f;
            p.HandZL = (L(r.LH).z - hip.z) * 100f; p.HandZR = (L(r.RH).z - hip.z) * 100f;
            p.HandYL = (L(r.LH).y - L(r.LLL).y) / Mathf.Max(0.01f, L(r.LUL).y - L(r.LLL).y);
            p.HandYR = (L(r.RH).y - L(r.RLL).y) / Mathf.Max(0.01f, L(r.RUL).y - L(r.RLL).y);
            p.ShoulderDy = Mathf.Abs(L(r.LUA).y - L(r.RUA).y) * 100f;
            p.Protract = (sh.z - L(r.Upper).z) * 100f;
            p.SpineBend = Vector3.Angle(L(r.Upper) - hip, L(r.Neck) - L(r.Upper));
            p.HeadPitch = Vector3.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(r.Head.up, Vector3.right), Vector3.right);
            p.Sole = SoleY(r) * 100f;
            p.FootWidth = Mathf.Abs(L(r.LF).x - L(r.RF).x) * 100f;
            p.HipAnkle = (hip.z - Mid(L(r.LF), L(r.RF)).z) * 100f;
            // 메시 잣대(뼈는 자동 리깅이 앞으로 치우쳐 잡음): 옆에서 본 허벅지·무릎·정강이 단면 가운데, 등 윤곽
            r.Smr.BakeMesh(r.Baked, true);
            var m = r.Root.worldToLocalMatrix * r.Smr.transform.localToWorldMatrix;
            var vs = r.Baked.vertices;
            for (int i = 0; i < vs.Length; i++) vs[i] = m.MultiplyPoint3x4(vs[i]);
            float Z(float cx, float halfX, float y, float halfY, bool back)
            {
                float a0 = float.MaxValue;
                var band = new List<float>();
                foreach (var q in vs)
                {
                    if (Mathf.Abs(q.x - cx) > halfX || Mathf.Abs(q.y - y) > halfY) continue;
                    if (q.z < a0) a0 = q.z;
                    band.Add(q.z);
                }
                if (band.Count == 0) return float.NaN;
                if (back) return a0;
                band.Sort();
                return (band[band.Count / 10] + band[band.Count * 9 / 10]) * 0.5f;      // 단면 앞뒤 끝(10·90%) 가운데 — 바지 주름 끝점·정점 몰림에 덜 끌림
            }
            float MeshKnee(Transform ul, Transform ll, Transform f)
            {
                var u = L(ul); var k = L(ll); var a2 = L(f);
                float yT = Mathf.Lerp(k.y, u.y, 0.6f), yS = Mathf.Lerp(a2.y, k.y, 0.5f);
                float xT = Mathf.Lerp(k.x, u.x, 0.6f), xS = Mathf.Lerp(a2.x, k.x, 0.5f);
                float zT = Z(xT, 0.07f, yT, 0.05f, false), zK = Z(k.x, 0.07f, k.y, 0.04f, false), zS = Z(xS, 0.07f, yS, 0.05f, false);
                if (float.IsNaN(zT) || float.IsNaN(zK) || float.IsNaN(zS)) return 0f;
                float up = Mathf.Atan2(zT - zK, yT - k.y) * Mathf.Rad2Deg;     // 허벅지가 위로 가며 앞으로
                float dn = Mathf.Atan2(zS - zK, k.y - yS) * Mathf.Rad2Deg;     // 정강이가 아래로 가며 앞으로
                return -(up + dn);                                              // + = 무릎이 앞으로 나옴(굽음), − = 뒤로 꺾임
            }
            p.MeshKneeL = MeshKnee(r.LUL, r.LLL, r.LF); p.MeshKneeR = MeshKnee(r.RUL, r.RLL, r.RF);
            // 등 윤곽: 몸 가운데 폭 ±9cm, 엉덩이~어깨 사이 등 쪽 끝. 양 끝을 이은 선보다 뒤로 튀어나온 만큼 = 굽음, 양 끝 앞뒤 차 = 기울기
            float yH = hip.y, yS2 = sh.y;
            float bH = Z(0f, 0.09f, yH, 0.02f, true), bS = Z(0f, 0.09f, yS2, 0.02f, true), bulge = 0f;
            if (!float.IsNaN(bH) && !float.IsNaN(bS))
            {
                for (int i = 1; i < 8; i++)
                {
                    float y = Mathf.Lerp(yH, yS2, i / 8f), b0 = Z(0f, 0.09f, y, 0.015f, true);
                    if (float.IsNaN(b0)) continue;
                    bulge = Mathf.Max(bulge, Mathf.Lerp(bH, bS, i / 8f) - b0);
                }
                p.BackLean = (bS - bH) * 100f;
            }
            p.BackBulge = bulge * 100f;
            return p;
        }

        /// 클립을 N 점에서 재서 각 항목의 '가장 나쁜 값'(절댓값 큰 쪽)
        static Posture MeasureClip(Rig r, AnimationClip clip, bool footIk = false, int n = 8)
        {
            var graph = PlayableGraph.Create("Stand.Measure");
            bool started = false;
            var worst = new Posture();
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", r.An);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(footIk);
                output.SetSourcePlayable(cp);
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                for (int i = 0; i < n; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, clip.length * i / n);
                    AnimationMode.EndSampling();
                    var p = Measure(r);
                    worst = i == 0 ? p : Worse(worst, p);
                }
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
            }
            return worst;
        }

        static Posture Worse(Posture a, Posture b)
        {
            float W(float x, float y) => Mathf.Abs(y) > Mathf.Abs(x) ? y : x;
            a.Ear = W(a.Ear, b.Ear); a.Shoulder = W(a.Shoulder, b.Shoulder); a.KneeL = W(a.KneeL, b.KneeL); a.KneeR = W(a.KneeR, b.KneeR); a.ArmL = W(a.ArmL, b.ArmL); a.ArmR = W(a.ArmR, b.ArmR); a.Arm3D = W(a.Arm3D, b.Arm3D);
            a.HandL = W(a.HandL, b.HandL); a.HandR = W(a.HandR, b.HandR); a.HandZL = W(a.HandZL, b.HandZL); a.HandZR = W(a.HandZR, b.HandZR);
            a.ShoulderDy = W(a.ShoulderDy, b.ShoulderDy); a.Protract = W(a.Protract, b.Protract); a.SpineBend = W(a.SpineBend, b.SpineBend); a.HeadPitch = W(a.HeadPitch, b.HeadPitch); a.Sole = W(a.Sole, b.Sole); a.EarBone = W(a.EarBone, b.EarBone); a.MeshKneeL = W(a.MeshKneeL, b.MeshKneeL); a.MeshKneeR = W(a.MeshKneeR, b.MeshKneeR); a.BackBulge = W(a.BackBulge, b.BackBulge); a.BackLean = W(a.BackLean, b.BackLean);
            return a;
        }

        static string Row(string who, string what, Posture p, Posture rest) =>
            $"{who}\t{what}\t{p.Ear:F1}\t{p.EarBone:F1}\t{p.Shoulder:F1}\t{Mathf.Max(p.KneeL, p.KneeR):F1}\t{Mathf.Max(p.KneeL - rest.KneeL, p.KneeR - rest.KneeR):F1}\t{Mathf.Max(p.ArmL, p.ArmR):F1}\t{p.Arm3D:F1}\t{Mathf.Max(p.HandL, p.HandR):F1}\t{p.HandZL:F1}/{p.HandZR:F1}\t{p.HandYL:F2}/{p.HandYR:F2}\t{p.ShoulderDy:F1}\t{p.Protract:F1}\t{p.SpineBend:F1}\t{p.HeadPitch:F1}\t{p.FootWidth:F0}\t{p.Sole:F1}\t{p.MeshKneeL:F1}/{p.MeshKneeR:F1}\t{p.BackBulge:F1}\t{p.BackLean:F1}\t{p.HipAnkle:F1}";

        static void Report(string outDir)
        {
            var sb = new StringBuilder("인물\t자세\t귀-어깨(머리 메시, 앞+ cm)\t머리뼈-어깨\t어깨-엉덩이(앞+ cm)\t무릎 뼈°\t무릎 휴지 대비°\t위팔 벌어짐(정면)°\t위팔 3D°\t손-허벅지 옆 cm\t손-엉덩이 앞뒤 L/R\t손 높이(0 무릎 1 사타구니)\t어깨 높이차 cm\t어깨 말림(어깨-윗가슴 앞+ cm)\t등 굽음°\t머리 숙임°\t발목 간격 cm\t발바닥 높이 cm\t무릎(메시) L/R°\t등 튀어나옴(메시) cm\t등 기울기(어깨쪽 앞+ cm)\t엉덩이-발목 앞뒤 cm\n");
            var siwoo246 = CharSetup.Clip(CharSetup.SiwooFbx, "Idle");
            foreach (var w in All())
            {
                var r = MakeRig(w.Fbx);
                try
                {
                    PutBind(r);
                    var rest = Measure(r);
                    sb.AppendLine(Row(w.Label, "메시 휴지", rest, rest));
                    r.An.Rebind();
                    var idle = CharSetup.Clip(w.Fbx, "Idle");
                    if (idle != null) sb.AppendLine(Row(w.Label, w.Key == "Siwoo" ? "246 Idle" : "243 Idle", MeasureClip(r, idle), rest));
                    if (w.Key == "Halmeoni" || w.Key == "Mom" || w.Key == "Dokyungmin" || w.Key == "Minjae")
                        foreach (var talk in TalkStates)
                        {
                            var tc = TalkSrc(talk);
                            if (tc != null) { r.An.Rebind(); sb.AppendLine(Row(w.Label, talk + " 원본", MeasureClip(r, tc), rest)); }
                            var nc = AssetDatabase.LoadAssetAtPath<AnimationClip>(TalkPath(w.Key, talk));
                            if (nc != null) { r.An.Rebind(); sb.AppendLine(Row(w.Label, talk + " 새", MeasureClip(r, nc), rest)); }
                        }
                    if (w.Group == "코치" && siwoo246 != null) { r.An.Rebind(); sb.AppendLine(Row(w.Label, "246 후보", MeasureClip(r, siwoo246), rest)); }
                    var stand = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(w.Key));
                    if (stand != null)
                    {
                        r.An.Rebind(); sb.AppendLine(Row(w.Label, "새 대기", MeasureClip(r, stand), rest));
                        r.An.Rebind(); sb.AppendLine(Row(w.Label, "새 대기 Foot IK", MeasureClip(r, stand, true, 4), rest));
                    }
                }
                finally { r.Dispose(); }
            }
            // 다른 대기 후보 — 시우 246 은 위 시우 243 Idle 자리(시우 FBX 의 Idle 이 246)
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/stand_measure.tsv", sb.ToString(), new UTF8Encoding(true));
            if (!string.IsNullOrEmpty(outDir)) { Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "stand_measure.tsv"), sb.ToString(), new UTF8Encoding(true)); }
            Debug.Log($"{Tag} 재기 끝 → Logs/stand_measure.tsv");
        }

        // ───────────────────────── 자세 만들기(뼈 방향)
        static void Aim(Transform bone, Vector3 childPos, Vector3 dir)
        {
            var cur = childPos - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(cur.normalized, dir.normalized) * bone.rotation;
        }

        static Vector3 Tilt(float fwdDeg) => new Vector3(0f, Mathf.Cos(fwdDeg * Mathf.Deg2Rad), Mathf.Sin(fwdDeg * Mathf.Deg2Rad));

        /// 곧은 자세. 뿌리 = 원점·정면 +Z, 캐릭터 왼쪽 = −X
        /// 기준 = 메시 휴지 자세(A포즈 설정화에서 나와 다리·몸통이 곧다). 자동 리깅 뼈는 무릎·등에서 앞으로 치우쳐(뼈 각도 무릎 19~33°, 등 최대 22°) 뼈를 곧게 펴면 메시가 뒤로 꺾임
        /// → 다리·등은 휴지 각도를 살리고, 팔만 몸 옆으로 내리고, 어깨 중심·머리 가운데를 엉덩이 위로 맞춘다.
        static void PoseStand(Rig r, Tune t)
        {
            PutBind(r);
            Quaternion headBind = r.Head.rotation, footL = r.LF.rotation, footR = r.RF.rotation;
            float ankleL = r.LF.position.y, ankleR = r.RF.position.y, sole0 = SoleY(r);
            float ankleH = Mathf.Max(0.02f, (ankleL + ankleR) * 0.5f - sole0);
            float shWidth = Mathf.Abs(r.LUA.position.x - r.RUA.position.x);
            var headC0 = HeadCenter(r);
            var headCLocal = Quaternion.Inverse(r.Head.rotation) * (headC0 - r.Head.position);
            var hipRest = Mid(r.LUL.position, r.RUL.position);
            float hipAnkleRest = hipRest.z - Mid(r.LF.position, r.RF.position).z;

            // 다리 길이·휴지 무릎 각도
            float l1L = (r.LLL.position - r.LUL.position).magnitude, l2L = (r.LF.position - r.LLL.position).magnitude;
            float l1R = (r.RLL.position - r.RUL.position).magnitude, l2R = (r.RF.position - r.RLL.position).magnitude;
            float k0L = Vector3.Angle(r.LLL.position - r.LUL.position, r.LF.position - r.LLL.position);
            float k0R = Vector3.Angle(r.RLL.position - r.RUL.position, r.RF.position - r.RLL.position);

            // 골반: 수평·정면
            var hipLine = r.RUL.position - r.LUL.position;
            r.Hips.rotation = Quaternion.FromToRotation(hipLine.normalized, Vector3.right) * r.Hips.rotation;
            float wSign = t.Weight > 0f ? 1f : t.Weight < 0f ? -1f : 0f;
            r.Hips.rotation = Quaternion.AngleAxis(wSign * t.Pelvis, Vector3.forward) * r.Hips.rotation;     // 무게 실은 쪽 골반이 위로
            r.Hips.rotation = Quaternion.AngleAxis(t.TorsoYaw * 0.6f, Vector3.up) * r.Hips.rotation;
            var yawT = Quaternion.AngleAxis(t.TorsoYaw, Vector3.up);

            // 발목 자리(뿌리 기준): 휴지 때 엉덩이~발목 앞뒤 차를 살림(메시 다리가 곧은 그 선)
            float sep = shWidth * t.FootSep;
            var hipC = new Vector3(t.Weight, 0f, 0f);
            float heelUp = 0.12f * Mathf.Sin(t.HeelLift * Mathf.Deg2Rad);      // 앞꿈치를 축으로 — 발목이 그만큼 올라감
            var ankL = new Vector3(-sep * 0.5f + t.Weight * 0.15f, ankleH + heelUp, -hipAnkleRest + t.StanceZ + (t.Weight > 0f ? t.RelaxFoot : 0f));
            var ankR = new Vector3(sep * 0.5f + t.Weight * 0.15f, ankleH + heelUp, -hipAnkleRest - t.StanceZ + (t.Weight < 0f ? t.RelaxFoot : 0f));

            // 골반 높이
            float Reach(float a, float b, float k) => Mathf.Sqrt(a * a + b * b + 2f * a * b * Mathf.Cos(k * Mathf.Deg2Rad));
            float kL = k0L + t.Knee + (t.Weight > 0f ? t.KneeSide : 0f), kR = k0R + t.Knee + (t.Weight < 0f ? t.KneeSide : 0f);
            float dL = Reach(l1L, l2L, kL), dR = Reach(l1R, l2R, kR);
            r.Hips.position += hipC - new Vector3(Mid(r.LUL.position, r.RUL.position).x, 0f, Mid(r.LUL.position, r.RUL.position).z);
            var ulL = r.LUL.position; var ulR = r.RUL.position;
            float midY = (ulL.y + ulR.y) * 0.5f;
            float yL = ankL.y + Mathf.Sqrt(Mathf.Max(0.01f, dL * dL - Sq(ulL.x - ankL.x) - Sq(ulL.z - ankL.z))) - (ulL.y - midY);
            float yR = ankR.y + Mathf.Sqrt(Mathf.Max(0.01f, dR * dR - Sq(ulR.x - ankR.x) - Sq(ulR.z - ankR.z))) - (ulR.y - midY);
            float wantY = Mathf.Min(yL, yR);
            r.Hips.position += Vector3.up * (wantY - midY);

            void Leg(Transform ul, Transform ll, Transform foot, Vector3 ank, float a, float b, Quaternion footRot, float outDeg)
            {
                var h = ul.position;
                var dv = ank - h;
                float d = Mathf.Min(dv.magnitude, a + b - 1e-4f);
                var along = dv.normalized;
                var perp = Vector3.ProjectOnPlane(Vector3.forward, along).normalized;
                float x = (a * a - b * b + d * d) / (2f * d);
                float y = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
                var knee = h + along * x + perp * y;
                Aim(ul, ll.position, knee - h);
                Aim(ll, foot.position, ank - ll.position);
                var yawF = Quaternion.Euler(0f, outDeg, 0f);
                foot.rotation = Quaternion.AngleAxis(t.HeelLift, yawF * Vector3.right) * yawF * footRot;
            }
            Leg(r.LUL, r.LLL, r.LF, ankL, l1L, l2L, footL, -(t.FootOut + (t.Weight > 0f ? t.RelaxOut : 0f)));
            Leg(r.RUL, r.RLL, r.RF, ankR, l1R, l2R, footR, t.FootOut + (t.Weight < 0f ? t.RelaxOut : 0f));
            // 골반이 기운 만큼 등에서 되돌려 몸통은 곧게
            r.Spine.rotation = Quaternion.AngleAxis(-wSign * t.Pelvis * 0.6f, Vector3.forward) * r.Spine.rotation;
            r.Chest.rotation = Quaternion.AngleAxis(-wSign * t.Pelvis * 0.4f, Vector3.forward) * r.Chest.rotation;
            r.Chest.rotation = Quaternion.AngleAxis(t.ShoulderTilt, Vector3.forward) * r.Chest.rotation;     // 어깨 기울기는 가슴에서(쇄골만 틀면 으쓱한 것처럼 보임)
            // 짝다리 S 자: 엉덩이는 무게 실은 발 쪽으로, 어깨는 몸 가운데로 되돌림(등 아래를 기울이고 가슴에서 수평을 되찾음) — 머리가 옆으로 안 밀림(계산대 등 좁은 자리 카메라도 그대로)
            for (int it = 0; it < 3 && Mathf.Abs(t.Weight) > 1e-4f; it++)
            {
                var shS = Mid(r.LUA.position, r.RUA.position);
                float wantX = Mid(r.LUL.position, r.RUL.position).x - t.Weight * 0.6f;
                float lean = Mathf.Rad2Deg * Mathf.Atan2(shS.x - wantX, Mathf.Max(0.2f, shS.y - r.Spine.position.y));
                r.Spine.rotation = Quaternion.AngleAxis(lean, Vector3.forward) * r.Spine.rotation;
                r.Chest.rotation = Quaternion.AngleAxis(-lean, Vector3.forward) * r.Chest.rotation;
            }

            // 등: 휴지 각도를 두고 윗가슴만 조금 뒤로(가슴 펴기) · 어깨선이 정면을 보게
            r.Upper.rotation = Quaternion.AngleAxis(-t.ChestBack, Vector3.right) * r.Upper.rotation;
            var clav = r.RS.position - r.LS.position; clav.y = 0f;
            if (clav.sqrMagnitude > 1e-6f) r.Upper.rotation = Quaternion.FromToRotation(clav.normalized, yawT * Vector3.right) * r.Upper.rotation;

            // 빗장뼈: 휴지에서 내려서 뒤로, 좌우 높이 맞춤
            void Clav(Transform c, Transform ua, float s)
            {
                var d = (ua.position - c.position).normalized;
                d = Quaternion.AngleAxis(s * t.ClavBack, Vector3.up) * d;      // 왼쪽(−X)·오른쪽(+X) 모두 끝이 뒤로
                d = Quaternion.AngleAxis(-s * t.ClavDrop, Vector3.forward) * d;
                Aim(c, ua.position, d);
            }
            Clav(r.LS, r.LUA, -1f);
            Clav(r.RS, r.RUA, 1f);
            float dyWant = shWidth * Mathf.Tan(t.ShoulderTilt * Mathf.Deg2Rad);
            for (int it = 0; it < 3; it++)
            {
                float dy = r.RUA.position.y - r.LUA.position.y - dyWant;
                if (Mathf.Abs(dy) < 0.001f) break;
                r.LS.rotation = Quaternion.AngleAxis(-Mathf.Rad2Deg * Mathf.Atan2(dy * 0.5f, 0.15f), Vector3.forward) * r.LS.rotation;
                r.RS.rotation = Quaternion.AngleAxis(-Mathf.Rad2Deg * Mathf.Atan2(dy * 0.5f, 0.15f), Vector3.forward) * r.RS.rotation;
            }

            // 어깨 중심을 엉덩이 위로: 아래 등(Spine)을 앞뒤로 돌림
            for (int it = 0; it < 3; it++)
            {
                var sh0 = Mid(r.LUA.position, r.RUA.position);
                var hip0 = Mid(r.LUL.position, r.RUL.position);
                float dz0 = sh0.z - hip0.z - t.ShoulderFwd;
                if (Mathf.Abs(dz0) < 0.002f) break;
                float h0 = Mathf.Max(0.2f, sh0.y - r.Spine.position.y);
                r.Spine.rotation = Quaternion.AngleAxis(-Mathf.Rad2Deg * Mathf.Atan2(dz0, h0), Vector3.right) * r.Spine.rotation;
            }

            // 팔: 몸 옆으로. 앞에서 본 몸통선 기준 벌어짐 + 조금 앞 → 팔꿈치 살짝
            var shM = Mid(r.LUA.position, r.RUA.position);
            var hipM = Mid(r.LUL.position, r.RUL.position);
            var torsoDown = (hipM - shM).normalized;
            Vector3 rightT = yawT * Vector3.right, fwdT = yawT * Vector3.forward;
            Vector3 Arm(float s, float af)
            {
                float ao = (t.ArmOut + (s < 0f ? t.ArmOutL : t.ArmOutR)) * Mathf.Deg2Rad;
                return (torsoDown * Mathf.Cos(ao) * Mathf.Cos(af) + rightT * (s * Mathf.Sin(ao)) + fwdT * Mathf.Sin(af)).normalized;
            }
            void ArmChain(Transform ua, Transform la, Transform h, float s)
            {
                float e0 = Vector3.Angle(la.position - ua.position, h.position - la.position);
                float e = (e0 + t.Elbow + (s < 0f ? t.ElbowL : t.ElbowR)) * Mathf.Deg2Rad;
                float af = 0f;
                for (int it = 0; it < 4; it++)
                {
                    var d = Arm(s, af);
                    Aim(ua, la.position, d);
                    var f = (d * Mathf.Cos(e) + Vector3.ProjectOnPlane(fwdT, d).normalized * Mathf.Sin(e) - rightT * (s * Mathf.Sin(t.ForearmIn * Mathf.Deg2Rad))).normalized;
                    Aim(la, h.position, f);
                    float err = h.position.z - (hipM.z + t.HandFwd + (s < 0f ? t.HandFwdL : t.HandFwdR));
                    float reach = Mathf.Max(0.2f, (h.position - ua.position).magnitude);
                    af -= Mathf.Atan2(err, reach);
                }
            }
            ArmChain(r.LUA, r.LLA, r.LH, -1f);
            ArmChain(r.RUA, r.RLA, r.RH, 1f);

            // 목·머리: 머리 방향은 휴지(정면 설정화의 수평)에 숙임 보정 — 머리 가운데가 어깨 바로 위(EarFwd)에 오게 목을 기울임
            var sh = Mid(r.LUA.position, r.RUA.position);
            var headRot = Quaternion.AngleAxis(t.HeadYaw, Vector3.up) * Quaternion.AngleAxis(t.HeadRoll, Vector3.forward) * Quaternion.AngleAxis(t.HeadPitch, Vector3.right) * headBind;
            float neckLen = (r.Head.position - r.Neck.position).magnitude;
            float wantHeadZ = sh.z + t.EarFwd - (headRot * headCLocal).z;
            float dz = Mathf.Clamp(wantHeadZ - r.Neck.position.z, -neckLen * 0.8f, neckLen * 0.8f);
            float dx = sh.x - r.Neck.position.x;
            var target = r.Neck.position + new Vector3(dx, Mathf.Sqrt(Mathf.Max(1e-4f, neckLen * neckLen - dz * dz - dx * dx)), dz);
            Aim(r.Neck, r.Head.position, target - r.Neck.position);
            r.Head.rotation = headRot;
        }

        static float Sq(float x) => x * x;

        // ───────────────────────── 굽기
        /// 근육 이름 → 클립 곡선 이름(손가락은 안 씀 — 자세만)
        static readonly string[] BreathChest = { "Chest Front-Back", "UpperChest Front-Back" };

        public static AnimationClip Bake(Who w)
        {
            var r = MakeRig(w.Fbx);
            try
            {
                PoseStand(r, w.T);
                var h = new HumanPoseHandler(r.An.avatar, r.Root);
                var pose = new HumanPose();
                h.GetHumanPose(ref pose);
                h.SetHumanPose(ref pose);        // 근육으로 다시 세운 자세에서 손발 목표를 읽음(Foot IK 켠 상태·블렌드 트리에서 발이 제자리에)
                var goals = Goals(r, pose);
                h.Dispose();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(w.Key));
                bool isNew = clip == null;
                if (isNew) clip = new AnimationClip();
                clip.ClearCurves();
                clip.name = "Stand_" + w.Key;
                clip.legacy = false;
                clip.frameRate = 30f;
                Write(clip, pose, goals, w.T);
                var st = AnimationUtility.GetAnimationClipSettings(clip);
                st.loopTime = true; st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
                st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true; st.heightFromFeet = false; st.level = 0f; st.orientationOffsetY = 0f;
                AnimationUtility.SetAnimationClipSettings(clip, st);
                if (isNew) AssetDatabase.CreateAsset(clip, ClipPath(w.Key));
                // 발바닥을 바닥에: 재서 RootT.y 를 고침(사람 키 크기로 나눈 값)
                r.An.Rebind();
                float scale = Mathf.Max(0.1f, r.An.humanScale);
                for (int it = 0; it < 3; it++)
                {
                    float s = MeasureSole(r, clip);
                    if (Mathf.Abs(s) < 0.002f) break;
                    pose.bodyPosition.y -= s / scale;
                    Write(clip, pose, goals, w.T);
                    r.An.Rebind();
                }
                EditorUtility.SetDirty(clip);
                return clip;
            }
            finally { r.Dispose(); }
        }

        static float MeasureSole(Rig r, AnimationClip clip)
        {
            var graph = PlayableGraph.Create("Stand.Sole");
            bool started = false;
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", r.An);
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(false);
                output.SetSourcePlayable(cp);
                if (!AnimationMode.InAnimationMode()) { AnimationMode.StartAnimationMode(); started = true; }
                float min = float.MaxValue;
                for (int i = 0; i < 6; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SamplePlayableGraph(graph, 0, clip.length * i / 6f);
                    AnimationMode.EndSampling();
                    min = Mathf.Min(min, SoleY(r));
                }
                return min;
            }
            finally
            {
                if (started) AnimationMode.StopAnimationMode();
                graph.Destroy();
            }
        }

        // ───────────────────────── 말하는 자세(서서 손짓 TalkHip·TalkOpen — 엄마·선생님·사장 할머니·민재가 서 있는 기본 자세): 원본은 엉덩이가 7~12cm 앞으로 나오고 발이 3~4cm 묻혀
        // 팔·손 곡선만 원본에서, 나머지(몸통·다리·머리·발 목표)는 그 사람의 곧은 대기 값으로 합친다. Anim/Stand/Talk_<인물>_<상태>.anim
        public static readonly string[] TalkStates = { "TalkHip", "TalkOpen" };
        public static string TalkPath(string key, string state) => $"{Dir}/Talk_{key}_{state}.anim";
        static bool IsArm(string n) => n.StartsWith("Left Arm") || n.StartsWith("Right Arm") || n.StartsWith("Left Forearm") || n.StartsWith("Right Forearm") || n.StartsWith("Left Hand") || n.StartsWith("Right Hand");
        static AnimationClip TalkSrc(string state) => AssetDatabase.LoadAllAssetsAtPath($"{CastSetup.ClipDir}/{state}.fbx").OfType<AnimationClip>().FirstOrDefault(x => x.name == state);

        static void BakeTalk(Who w)
        {
            var stand = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(w.Key));
            if (stand == null) return;
            foreach (var state in TalkStates)
            {
                var src = TalkSrc(state);
                if (src == null) continue;
                var path = TalkPath(w.Key, state);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool isNew = clip == null;
                if (isNew) clip = new AnimationClip();
                clip.ClearCurves();
                clip.name = $"Talk_{w.Key}_{state}";
                clip.frameRate = 30f;
                foreach (var b in AnimationUtility.GetCurveBindings(stand))
                {
                    if (IsArm(b.propertyName)) continue;
                    float v = AnimationUtility.GetEditorCurve(stand, b).keys[0].value;
                    AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(new Keyframe(0f, v), new Keyframe(src.length, v)));
                }
                int arms = 0;
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                    if (IsArm(b.propertyName)) { AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(src, b)); arms++; }
                if (arms == 0) Debug.LogWarning($"{Tag} {state} 팔 곡선을 못 읽음");
                var st = AnimationUtility.GetAnimationClipSettings(clip);
                st.loopTime = true; st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
                st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true; st.heightFromFeet = false; st.level = 0f; st.orientationOffsetY = 0f;
                AnimationUtility.SetAnimationClipSettings(clip, st);
                if (isNew) AssetDatabase.CreateAsset(clip, path);
                EditorUtility.SetDirty(clip);
            }
        }

        /// 손발 IK 목표 곡선 값(몸통 기준, 사람 키 크기로 나눔). 이 곡선이 없으면 Foot IK 가 발을 몸 가운데로 끌어 올림(무릎 162°·발 59cm 뜸 — 2026-10-31 실측)
        /// 공식 = Unity 애니메이션 팀 AvatarUtility.GetIKGoalTQ(뼈 회전 × post rotation, 발은 축 길이만큼 발바닥으로)
        static (string name, Vector3 t, Quaternion q)[] Goals(Rig r, HumanPose pose)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var avatar = r.An.avatar;
            var mPost = typeof(Avatar).GetMethod("GetPostRotation", F) ?? throw new Exception("Avatar.GetPostRotation 없음");
            var mLen = typeof(Avatar).GetMethod("GetAxisLength", F) ?? throw new Exception("Avatar.GetAxisLength 없음");
            float scale = Mathf.Max(0.1f, r.An.humanScale);
            var bodyT = pose.bodyPosition * scale; var bodyQ = pose.bodyRotation;
            var inv = Quaternion.Inverse(bodyQ);
            (string, Vector3, Quaternion) One(string name, HumanBodyBones b, bool foot)
            {
                var t = r.An.GetBoneTransform(b);
                var post = (Quaternion)mPost.Invoke(avatar, new object[] { (int)b });
                var q = t.rotation * post;
                var p = t.position;
                if (foot) p += q * new Vector3((float)mLen.Invoke(avatar, new object[] { (int)b }), 0f, 0f);
                return (name, inv * (p - bodyT) / scale, inv * q);
            }
            return new[] { One("LeftFoot", HumanBodyBones.LeftFoot, true), One("RightFoot", HumanBodyBones.RightFoot, true), One("LeftHand", HumanBodyBones.LeftHand, false), One("RightHand", HumanBodyBones.RightHand, false) };
        }

        /// 곡선 쓰기: 대부분 값 고정(키 2개), 숨쉬기(4초)·무게 이동(8초)만 작게
        static void Write(AnimationClip clip, HumanPose pose, (string name, Vector3 t, Quaternion q)[] goals, Tune tu)
        {
            float Length = tu.SwaySec;
            int breaths = Mathf.Max(1, Mathf.RoundToInt(tu.SwaySec / tu.BreathSec));   // 고리가 이어지게 정수 번
            clip.ClearCurves();
            void Set(string prop, Func<float, float> f, bool moving)
            {
                var c = new AnimationCurve();
                int n = moving ? 8 * breaths : 1;
                for (int i = 0; i <= n; i++) { float t = Length * i / n; c.AddKey(new Keyframe(t, f(t))); }
                if (moving) for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), c);
            }
            float Breath(float t) => Mathf.Sin(t / Length * Mathf.PI * 2f * breaths + tu.Phase);
            float Sway(float t) => Mathf.Sin(t / Length * Mathf.PI * 2f + tu.Phase * 0.7f);
            var body = pose.bodyPosition; var q = pose.bodyRotation;
            Set("RootT.x", t => body.x + Sway(t) * 0.0025f, true);
            Set("RootT.y", t => body.y + Breath(t) * 0.0004f, true);
            Set("RootT.z", t => body.z, false);
            Set("RootQ.x", t => q.x, false); Set("RootQ.y", t => q.y, false); Set("RootQ.z", t => q.z, false); Set("RootQ.w", t => q.w, false);
            foreach (var (gn, gt, gq) in goals)
            {
                Set(gn + "T.x", t => gt.x, false); Set(gn + "T.y", t => gt.y, false); Set(gn + "T.z", t => gt.z, false);
                Set(gn + "Q.x", t => gq.x, false); Set(gn + "Q.y", t => gq.y, false); Set(gn + "Q.z", t => gq.z, false); Set(gn + "Q.w", t => gq.w, false);
            }
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                if (name.Contains("Thumb") || name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little")) continue;
                float v = pose.muscles[i];
                if (BreathChest.Contains(name)) Set(name, t => v + Breath(t) * 0.012f, true);
                else if (name == "Left Shoulder Down-Up" || name == "Right Shoulder Down-Up") Set(name, t => v + Breath(t) * 0.012f, true);
                else if (name == "Spine Left-Right") Set(name, t => v + Sway(t) * 0.006f * tu.SwayAmp, true);
                else if (name == "Chest Left-Right" || name == "Neck Tilt Left-Right") Set(name, t => v - Sway(t) * 0.006f * tu.SwayAmp, true);
                else if (name == "Neck Turn Left-Right" || name == "Head Turn Left-Right") Set(name, t => v + Sway(t) * 0.006f * tu.SwayAmp, true);
                else if (name == "Left Arm Down-Up" || name == "Right Arm Down-Up") Set(name, t => v + Breath(t) * 0.002f, true);
                else Set(name, t => v, false);
            }
        }

        // ───────────────────────── 컨트롤러 연결(이미 있는 애셋을 바로 고침 — 설정 스크립트도 Idle() 을 씀)
        public static void Apply()
        {
            // 시우: 움직임 블렌드 트리의 Idle 칸 · Sit 등은 그대로
            var siwooStand = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("Siwoo"));
            var sc = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharSetup.SiwooCtrl);
            AnimationClip oldSiwoo = null;
            if (sc != null && siwooStand != null)
                foreach (var cs in sc.layers[0].stateMachine.states)
                    if (cs.state.motion is BlendTree bt && bt.name == "Locomotion")     // 전투 이동 트리(Stance)는 건드리지 않음
                    {
                        var ch = bt.children;
                        for (int i = 0; i < ch.Length; i++)
                            if (ch[i].threshold == 0f && ch[i].motion is AnimationClip c && (c.name == "Idle" || c.name.StartsWith("Stand_"))) { oldSiwoo = c; ch[i].motion = siwooStand; }
                        bt.children = ch;
                        EditorUtility.SetDirty(bt);
                    }
            if (sc != null) EditorUtility.SetDirty(sc);
            // 태오: 컨트롤러 Idle · 달리기용 Override
            var taeoStand = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("Taeo"));
            var tc = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharSetup.TaeoCtrl);
            if (tc != null && taeoStand != null)
            {
                foreach (var cs in tc.layers[0].stateMachine.states) if (cs.state.name == "Idle") { cs.state.motion = taeoStand; cs.state.iKOnFeet = false; }     // 자세가 곧 발 자리 — Foot IK 는 곧은 다리에서 무릎만 흔듦
                EditorUtility.SetDirty(tc);
            }
            var run = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(CharSetup.AnimDir + "/TaeoRun.overrideController");
            if (run != null && taeoStand != null && siwooStand != null)
            {
                run.runtimeAnimatorController = sc;
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                run.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++) if (pairs[i].Key == siwooStand) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, taeoStand);
                run.ApplyOverrides(pairs);
                EditorUtility.SetDirty(run);
            }
            // 배역: Cast.controller 의 기본 Idle = 시우 대기(임찬도 이것) · 사람마다 Override
            var cast = AssetDatabase.LoadAssetAtPath<AnimatorController>(CastSetup.CastCtrl);
            if (cast != null && siwooStand != null)
            {
                foreach (var cs in cast.layers[0].stateMachine.states) if (cs.state.name == "Idle") { cs.state.motion = siwooStand; cs.state.iKOnFeet = false; }
                EditorUtility.SetDirty(cast);
                foreach (var (k, _) in CastSetup.Cast)
                {
                    var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>($"{CharSetup.AnimDir}/Cast_{k}.overrideController");
                    var own = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(k));
                    if (oc == null || own == null) continue;
                    oc.runtimeAnimatorController = cast;
                    var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    oc.GetOverrides(pairs);
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        if (pairs[i].Key == siwooStand) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, own);
                        else if (pairs[i].Key != null && Array.IndexOf(TalkStates, pairs[i].Key.name) >= 0)
                        {
                            var talk = AssetDatabase.LoadAssetAtPath<AnimationClip>(TalkPath(k, pairs[i].Key.name));
                            if (talk != null) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, talk);
                        }
                    }
                    oc.ApplyOverrides(pairs);
                    EditorUtility.SetDirty(oc);
                }
            }
            ApplyHands();
            Debug.Log($"{Tag} 연결: 시우 {(oldSiwoo != null ? oldSiwoo.name : "-")} → {(siwooStand != null ? siwooStand.name : "-")} · 태오 · 배역 {CastSetup.Cast.Length}명 (체육관 NPC 는 GymSetup.Build 가 Idle() 로)");
        }
    }
}
