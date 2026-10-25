// 행인1의 메인이벤트 — M3 6·7단계 정식 모델 테스트(docs/09_M3_버티컬슬라이스_설계.md 6-2 D13~D15)
//   D13 Rig_Fingers : 정식 3벌(시우 교복·배달복·태오) Humanoid 손가락 30 매핑 · 손 자세 8종에서 손가락끼리·손바닥 관통 ≤ 4mm(정점 검사, 휴지 붙음 뺌) · 손가락 총 없음
//   D14 Face_Expr   : 표정 5칸(normal·hurt·ko·win·endure) 같은 프레임에 바뀜(≤ 0.08초) · 깜빡임(반쯤 → 감음 → 반쯤) · 말하기 입 2장 ·
//                     윗눈꺼풀 근접(얼굴 화면 높이 40%) ≥ 6px · 탐색 4m(FOV 53 = 카메라 B, 1080p) ≥ 2px
//   D15 Body_Spec   : 키 ± 1cm · 두개골 기준 등신 7.4/7.5 ± 0.3 · LOD 3단 삼각형 예산 · 소켓 5 · 7단계 의상 전환(교복 ↔ 배달복)
// 이름이 M3… 인 까닭: 입력 테스트(InputTests) 뒤에 돌게(실제 Player 프리팹 — PInput 은 끔).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class M3CharTests
    {
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        const string TaeoPath = "Assets/_Project/Prefabs/Char/Taeo.prefab";
        readonly List<GameObject> made = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Lab.Dt;
            TimeFx.Reset();
            yield return Lab.FreshScene();
            Lab.Floor(0f, 30f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in made) if (g != null) Object.Destroy(g);
            made.Clear();
            TimeFx.Reset();
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        static GameObject Load(string path)
        {
            GameObject g = null;
#if UNITY_EDITOR
            g = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            if (g == null) Assert.Ignore(path + " 없음(FormalSetup.Build 먼저) 또는 에디터에서만");
            return g;
        }

        GameObject Spawn(string path, Vector3 at, bool animOff = false)
        {
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            var p = Object.Instantiate(Load(path), at, Quaternion.identity, holder.transform);
            foreach (var i in p.GetComponentsInChildren<PInput>(true)) i.enabled = false;
            if (animOff) foreach (var a in p.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            p.transform.SetParent(null, true);
            Object.Destroy(holder);
            made.Add(p);
            return p;
        }

        /// 정식 3벌의 모델 루트(Animator 가 붙은 것): 시우 교복 · 시우 배달복 · 태오
        IEnumerator Models(List<(string who, GameObject model)> into, bool animOff = false)
        {
            var player = Spawn(PlayerPath, new Vector3(-1.2f, 0f, 0f), animOff);
            var player2 = Spawn(PlayerPath, new Vector3(0f, 0f, 0f), animOff);
            var taeo = Spawn(TaeoPath, new Vector3(1.2f, 0f, 0f), animOff);
            yield return null;
            var o2 = player2.GetComponent<Outfit>();
            Assert.IsNotNull(o2, "Player 프리팹에 Outfit(교복 + 배달복)");
            o2.Set(Outfit.Delivery);
            yield return null;
            into.Add(("시우", player.GetComponent<Outfit>().Model));
            into.Add(("시우 배달복", o2.Model));
            into.Add(("태오", taeo));
            foreach (var (w, m) in into) Assert.IsNotNull(m, w + " 모델");
        }

        static SkinnedMeshRenderer Body(GameObject m, int lod = 0) =>
            m.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body_LOD" + lod);

        static readonly HumanBodyBones[] Fingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        // ───────────────────────── D13
        [UnityTest]
        public IEnumerator D13_Rig_Fingers()
        {
            // 휴지 자세 기준선은 애니메이터가 돌기 전(바인드 자세)에 잡는다 — 애니메이터가 팔을 돌린 자세에서 잡으면 같은 주먹이 8.9mm 로 나왔다
            // (2026-10-25 진단: 기준을 바인드 자세에서 잡으면 애니메이터 10프레임 뒤에도 3.6mm — 손목을 48° 돌린 자세에서 잡은 기준이 틀어짐). 재는 것은 게임 자세(대기 클립 10프레임 뒤)
            var log = new List<string>();
            var ms = new List<(string, GameObject)>();
            yield return Models(ms, true);
            var refs = new Dictionary<GameObject, HandCheck.Map>();
            foreach (var (_, m) in ms)
            {
                var hp = m.GetComponent<HandPoser>();
                hp.ResetRest();
                var mp = HandCheck.Build(Body(m));
                HandCheck.SetRest(mp);
                refs[m] = mp;
            }
            foreach (var (_, m) in ms) m.GetComponent<Animator>().enabled = true;
            yield return Lab.Frames(10);
            foreach (var (who, m) in ms)
            {
                var anim = m.GetComponent<Animator>();
                Assert.IsTrue(anim != null && anim.isHuman, who + " Humanoid");
                int mapped = Fingers.Count(b => anim.GetBoneTransform(b) != null);
                Assert.AreEqual(30, mapped, who + " Humanoid 손가락 30 매핑");
                var poser = m.GetComponent<HandPoser>();
                Assert.IsTrue(poser != null && poser.Ready, who + " HandPoser(손가락 뼈 + 손바닥 표시 뼈)");
                Assert.IsNotNull(poser.Set, who + " 맞춘 손 자세 묶음(HandPoses_<이름>.asset)");
                foreach (var n in HandPoseLib.Names)
                    foreach (bool right in new[] { false, true })
                    {
                        var p = poser.Set.Find(right, n);
                        Assert.IsNotNull(p, $"{who} {(right ? "오른손" : "왼손")} '{n}'");
                        Assert.IsFalse(HandPoseLib.IsFingerGun(p), $"{who} '{n}' 가 손가락 총 모양이면 안 됨");
                    }
                Assert.AreEqual(8, HandPoseLib.Names.Length, "손 자세 8종");
                // 같은 프레임 안에서 굽혀 굽고 잰다(애니메이터가 다시 쓰기 전)
                var hs = m.GetComponent<HandShape>(); if (hs != null) hs.enabled = false;
                anim.enabled = false;
                poser.enabled = false;
                poser.ResetRest();
                var map = refs[m];
                var per = new float[2, 5];
                var worst = new int[2, 5];
                var row = new List<string>();
                float max = 0f;
                foreach (var n in HandPoseLib.Names)
                {
                    poser.ApplyNow(false, poser.Pose(false, n));
                    poser.ApplyNow(true, poser.Pose(true, n));
                    float pen = HandCheck.Measure(map, per, worst);
                    string det = "";
                    for (int h = 0; h < 2; h++) for (int f = 0; f < 5; f++) if (per[h, f] > 2f) det += $" {(h == 0 ? "L" : "R")}{f}>{worst[h, f]}:{per[h, f]:F1}";
                    row.Add($"{n} {pen:F1}{(det.Length > 0 ? "[" + det.Trim() + "]" : "")}");
                    max = Mathf.Max(max, pen);
                }
                Debug.Log($"[M3Test] D13 {who} 진단: 물갈퀴 {map.WebCount} · 어긋난 줄기 L{map.BadChain[0]}/R{map.BadChain[1]} · 검지1 위치 {anim.GetBoneTransform(HumanBodyBones.RightIndexProximal).localPosition * 100f} · " + string.Join(", ", row));
                Assert.That(max, Is.LessThanOrEqualTo(4.0f), $"{who} 손가락 관통 ≤ 4mm ({string.Join(", ", row)})");
                // 실제로 굽었는지(주먹 검지 2마디)
                poser.ApplyNow(true, poser.Pose(true, HandPoseLib.Fist));
                var idx = anim.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
                poser.ResetRest();
                var rest = idx.localRotation;
                poser.ApplyNow(true, poser.Pose(true, HandPoseLib.Fist));
                float bend = Quaternion.Angle(rest, idx.localRotation);
                Assert.That(bend, Is.GreaterThan(50f), who + " 주먹에서 검지 2마디가 굽음");
                poser.ResetRest();
                anim.enabled = true; poser.enabled = true; if (hs != null) hs.enabled = true;
                Object.Destroy(map.Baked);
                log.Add($"{who}: 손가락 30 · 관통 최대 {max:F1}mm({string.Join(", ", row)}) · 주먹 검지 {bend:F0}°");
            }
            Debug.Log("[M3Test] D13 손가락: " + string.Join(" | ", log));
        }

        // ───────────────────────── D14
        [UnityTest]
        public IEnumerator D14_Face_Expr()
        {
            var ms = new List<(string, GameObject)>();
            yield return Models(ms);
            yield return Lab.Frames(3);
            var log = new List<string>();
            foreach (var (who, m) in ms)
            {
                var fr = m.GetComponent<FaceRig>();
                Assert.IsNotNull(fr, who + " FaceRig");
                Assert.AreEqual(3, fr.Faces.Length, who + " 얼굴 데칼 LOD 3");
                Assert.IsTrue(fr.Faces.All(f => f != null && f.sharedMaterial != null && f.sharedMaterial.name.EndsWith("_Face")), who + " 얼굴 재질");
                fr.Blink = false; fr.Talk = false;
                yield return null;
                // 표정 5칸: 바꾸는 그 프레임에 칸이 바뀐다
                for (int r = 0; r < FaceRig.ExprNames.Length; r++)
                {
                    float t0 = Time.time;
                    Assert.IsTrue(fr.Set(FaceRig.ExprNames[r]), who + " 표정 " + FaceRig.ExprNames[r]);
                    Assert.AreEqual(fr.CellST(r, 0), fr.CurrentST(fr.Faces[0]), $"{who} {FaceRig.ExprNames[r]} 같은 프레임 칸");
                    yield return null;
                    Assert.AreEqual($"{FaceRig.ExprNames[r]}/open", fr.Cell, who + " 칸 이름");
                    Assert.That(fr.LastSwitch - t0, Is.LessThanOrEqualTo(0.08f), who + " 바뀌는 데 ≤ 0.08초");
                }
                Assert.IsTrue(fr.Set("참기") && fr.Expr == "endure", who + " 한국어 이름(컷신 표정 트랙)");
                fr.Set("normal");
                // 깜빡임
                fr.Blink = true; fr.BlinkNow();
                var seen = new HashSet<string>();
                for (int i = 0; i < 12; i++) { yield return null; seen.Add(fr.Cell); }
                Assert.IsTrue(seen.Contains("normal/half") && seen.Contains("normal/closed"), $"{who} 깜빡임 반쯤 → 감음 ({string.Join(",", seen)})");
                fr.Blink = false;
                // 말하기 입 2장
                fr.Talk = true;
                seen.Clear();
                for (int i = 0; i < 30; i++) { yield return null; seen.Add(fr.Cell); }
                fr.Talk = false;
                Assert.IsTrue(seen.Contains("normal/talkA") && seen.Contains("normal/talkB"), $"{who} 말하기 입 2장 ({string.Join(",", seen)})");
                // 윗눈꺼풀 화면 굵기
                var fm = m.GetComponent<FormalModel>();
                Assert.IsNotNull(fm, who + " FormalModel");
                float faceH = Mathf.Max(fm.FaceBox.y, 0.01f);
                float closePx = fm.LidMm / 1000f / faceH * 0.40f * 1080f;
                float explorePx = fm.LidMm / 1000f / (2f * 4f * Mathf.Tan(53f * 0.5f * Mathf.Deg2Rad)) * 1080f;
                float camBPx = fm.LidMm / 1000f / (2f * 5.5f * Mathf.Tan(53f * 0.5f * Mathf.Deg2Rad)) * 1080f;
                Assert.That(closePx, Is.GreaterThanOrEqualTo(6f), who + " 근접(얼굴 40%) 윗눈꺼풀 ≥ 6px");
                Assert.That(explorePx, Is.GreaterThanOrEqualTo(2f), who + " 탐색 4m(FOV 53) 윗눈꺼풀 ≥ 2px");
                log.Add($"{who}: 칸 5×5 · 깜빡임·말하기 OK · 눈꺼풀 {fm.LidMm:F1}mm = 근접 {closePx:F1}px · 4m {explorePx:F2}px · 카메라 B 5.5m {camBPx:F2}px");
            }
            Debug.Log("[M3Test] D14 표정: " + string.Join(" | ", log));
        }

        // ───────────────────────── D15
        [UnityTest]
        public IEnumerator D15_Body_Spec()
        {
            var ms = new List<(string, GameObject)>();
            yield return Models(ms);
            yield return Lab.Frames(3);
            var log = new List<string>();
            var want = new Dictionary<string, (float h, float heads)> { { "시우", (1.74f, 7.4f) }, { "시우 배달복", (1.74f, 7.4f) }, { "태오", (1.83f, 7.5f) } };
            foreach (var (who, m) in ms)
            {
                var fm = m.GetComponent<FormalModel>();
                Assert.IsNotNull(fm, who + " FormalModel");
                var b0 = Body(m, 0);
                Assert.IsNotNull(b0, who + " Body_LOD0");
                var bb = b0.sharedMesh.bounds;   // 바인드 자세 메시(Blender Z 위 → 메시 z = 키)
                float s = b0.transform.lossyScale.x;
                float h = bb.size.z * s;
                float top = bb.max.z * s;
                float skull = (top - fm.ChinZ) / 1.08f;        // ChinZ = 도구가 잰 턱끝(Blender 월드 Z, m)
                float heads = h / skull;
                var (wh, wheads) = want[who];
                Assert.That(h, Is.EqualTo(wh).Within(0.01f), who + " 키 ± 1cm");
                Assert.That(heads, Is.EqualTo(wheads).Within(0.3f), who + " 두개골 기준 등신 ± 0.3");
                Assert.That(fm.HeadsMeasured, Is.EqualTo(wheads).Within(0.3f), who + " 도구 측정 등신");
                // LOD
                var lg = m.GetComponentInChildren<LODGroup>(true);
                Assert.IsNotNull(lg, who + " LODGroup");
                var lods = lg.GetLODs();
                Assert.AreEqual(3, lods.Length, who + " LOD 3단");
                int Tris(int k) => lods[k].renderers.Where(r => r != null).Sum(r =>
                {
                    var mesh = r is SkinnedMeshRenderer sr ? sr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) return 0;
                    int n = 0; for (int i = 0; i < mesh.subMeshCount; i++) n += (int)mesh.GetIndexCount(i) / 3; return n;
                });
                int t0 = Tris(0), t1 = Tris(1), t2 = Tris(2);
                Assert.That(t0, Is.LessThanOrEqualTo(60000), who + " LOD0 ≤ 60k");
                Assert.That(t1, Is.LessThanOrEqualTo(t0 * 0.55f), who + " LOD1 ≈ 50%");
                Assert.That(t2, Is.LessThanOrEqualTo(t0 * 0.25f), who + " LOD2 ≈ 20%");
                Assert.IsTrue(lods.All(l => l.renderers.Any(r => r != null && r.name.StartsWith("Face_"))), who + " LOD 마다 얼굴 데칼");
                // 소켓 5
                var names = new HashSet<string>(m.GetComponentsInChildren<Transform>(true).Select(t => t.name));
                foreach (var so in FormalModel.Sockets) Assert.IsTrue(names.Contains(so), $"{who} 소켓 '{so}'");
                log.Add($"{who}: 키 {h:F3}m · 등신 {heads:F2}(도구 {fm.HeadsMeasured:F2}) · LOD {t0}/{t1}/{t2} · 소켓 5");
            }
            Debug.Log("[M3Test] D15 몸: " + string.Join(" | ", log));
        }

        // ───────────────────────── D15b 의상 전환(7단계)
        [UnityTest]
        public IEnumerator D15b_Outfit_Switch()
        {
            var p = Spawn(PlayerPath, Vector3.zero);
            yield return Lab.Frames(3);
            var o = p.GetComponent<Outfit>();
            Assert.IsNotNull(o, "Outfit");
            Assert.AreEqual(Outfit.Uniform, o.CurrentName, "처음은 교복");
            var react = p.GetComponentInChildren<HitReact>(true);
            o.Set(Outfit.Delivery);
            yield return null;
            Assert.AreEqual(Outfit.Delivery, o.CurrentName);
            Assert.IsFalse(o.Models[0].activeSelf, "교복 모델 꺼짐");
            Assert.IsTrue(o.Models[1].activeSelf, "배달복 모델 켜짐");
            if (react != null) Assert.AreEqual(o.Models[1].transform, react.Model, "피격 흔들기 모델 = 배달복");
            // 배달복으로 걷기 → 배달복 애니메이터가 걷기 블렌드
            var motor = p.GetComponent<PlayerMotor>();
            for (int i = 0; i < 60; i++) { motor.SetMoveInput(Vector3.forward, 1f, false); yield return null; }
            float blend = o.Anim.GetFloat("Blend");
            Assert.That(blend, Is.GreaterThan(0.7f), "배달복 모델도 걷기 블렌드(LocoAnim)");
            o.Set(Outfit.Uniform);
            yield return null;
            Assert.IsTrue(o.Models[0].activeSelf && !o.Models[1].activeSelf, "다시 교복");
            Debug.Log($"[M3Test] D15b 의상: 교복 → 배달복(걷기 Blend {blend:F2}) → 교복 · 바꾼 횟수 {o.Switches}");
        }
    }
}
