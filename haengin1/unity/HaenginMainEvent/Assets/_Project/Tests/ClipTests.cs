// 행인1의 메인이벤트 — M2 9단계 클립·손 테스트 C14 (docs/08_M2_전투_설계.md 10-5, 11장 9단계)
// 실제 Player 프리팹(시우 FBX + 전투 클립 + 손 셰이프 키) · 적 프리팹(Enemy_Kkanjok) 을 평지에 세워
//   C14  HandShape_States : 탐색 → 전투 → 잡기 → 끝 에서 Fist_R · Grip_L 무게와 걸린 프레임(0.08초 ± 1f)
//   C14b Anim_Siwoo       : □□□□ · △ · ○ 마다 애니메이터 상태가 기술 클립으로 바뀌고, 판정 첫 프레임에 클립이 타격 시각 근처인지
//   C14c Anim_Enemy       : 깐족이 모델 — 원투(잽 → 크로스 이어짐) · 다운(쓰러짐 → 누움 → 일어서기 → 전투 자세)
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class ClipTests
    {
        const float Dt = Lab.Dt;
        const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        const string EnemyPath = "Assets/_Project/Prefabs/Enemy_Kkanjok.prefab";
        CombatTuning tune;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.captureDeltaTime = Dt;
            TimeFx.Reset();
            tune = ScriptableObject.CreateInstance<CombatTuning>();
            yield return Lab.FreshScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TimeFx.Reset();
            GameState.SetPaused(false);
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        static GameObject Load(string path)
        {
            GameObject g = null;
#if UNITY_EDITOR
            g = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            if (g == null) Assert.Ignore(path + " 을 에디터에서만 읽을 수 있음");
            return g;
        }

        /// 실제 Player 프리팹(사람 입력 끔 — AnimTests 와 같은 방법)
        static PlayerCombat SpawnSiwoo(Vector3 feet, float yaw)
        {
            var prefab = Load(PlayerPath);
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            var p = Object.Instantiate(prefab, feet, Quaternion.Euler(0f, yaw, 0f), holder.transform);
            foreach (var i in p.GetComponentsInChildren<PInput>(true)) i.enabled = false;
            p.transform.SetParent(null, true);
            Object.Destroy(holder);
            var pc = p.GetComponent<PlayerCombat>();
            Assert.IsNotNull(pc, "프리팹에 PlayerCombat");
            return pc;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// 조건이 맞을 때까지 프레임 수(최대 max)
        static IEnumerator Until(System.Func<bool> ok, int max, int[] outFrames)
        {
            int n = 0;
            while (!ok() && n < max) { yield return null; n++; }
            outFrames[0] = ok() ? n : -1;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.5f;

        // ───────────────────────── C14 손 셰이프 키
        [UnityTest]
        public IEnumerator C14_HandShape_States()
        {
            Lab.Floor(0f, 40f);
            var pc = SpawnSiwoo(Vector3.zero, 0f);
            var hs = pc.GetComponentInChildren<HandShape>();
            Assert.IsNotNull(hs, "프리팹에 HandShape");
            yield return Frames(3);
            Assert.IsTrue(hs.Ready, "시우 메시에 Fist_L·Fist_R·Grip_L·Grip_R 셰이프 키");
            var d = CombatFactory.Dummy("허수아비", new Vector3(0f, 0f, 0.8f), 180f, 999, null, 1.80f, 0.30f, tune);
            yield return Frames(10);
            var log = new List<string>();
            float fr0 = hs.Weight("Fist_R"), gl0 = hs.Weight("Grip_L");
            log.Add($"탐색: Fist_R {fr0:F0} · Grip_L {gl0:F0}");
            Assert.That(fr0, Is.EqualTo(0f).Within(0.5f), "탐색 Fist_R 0");
            Assert.That(gl0, Is.EqualTo(0f).Within(0.5f), "탐색 Grip_L 0");

            int want = Mathf.CeilToInt(0.08f / Dt);     // 0.08초 = 4.8f → 5f
            var f = new int[1];
            pc.Begin(0f);
            yield return Until(() => Near(hs.Weight("Fist_R"), 100f), 30, f);
            int toFist = f[0];
            log.Add($"전투: Fist_R 100 까지 {toFist}f · Grip_L {hs.Weight("Grip_L"):F0}");
            Assert.That(toFist, Is.InRange(want - 1, want + 1), "전투 들어가면 주먹 0.08초 ± 1f");
            Assert.That(hs.Weight("Grip_L"), Is.EqualTo(0f).Within(0.5f), "전투 Grip_L 0");

            pc.Press(Btn.Grab);
            yield return Until(() => Near(hs.Weight("Grip_L"), 100f), 40, f);
            int toGrip = f[0];
            log.Add($"잡기: Grip_L 100 까지 {toGrip}f · Fist_R {hs.Weight("Fist_R"):F0} · Fist_L {hs.Weight("Fist_L"):F0}");
            Assert.That(toGrip, Is.InRange(want - 1, want + 1), "잡기 누르면 왼손 쥐기 0.08초 ± 1f");
            Assert.That(hs.Weight("Fist_R"), Is.EqualTo(100f).Within(0.5f), "잡기 중 오른손 주먹 100");
            yield return Frames(12);
            Assert.AreEqual(d, pc.Held, "실제로 잡힘");
            Assert.That(hs.Weight("Grip_L"), Is.EqualTo(100f).Within(0.5f), "잡고 있는 동안 Grip_L 100");

            pc.End();
            yield return Until(() => Near(hs.Weight("Grip_L"), 0f) && Near(hs.Weight("Fist_R"), 0f), 30, f);
            int toOff = f[0];
            log.Add($"끝(탐색): Grip_L·Fist_R 0 까지 {toOff}f");
            Assert.That(toOff, Is.InRange(want - 1, want + 1), "전투 끝 → 손 풀림 0.08초 ± 1f");
            Debug.Log("[M2Test] C14 손 셰이프 키: " + string.Join(" | ", log));
        }

        // ───────────────────────── C05b 닿는 거리 자석(11-3 결정 1)
        /// 실제 Player 프리팹(클립 있음)으로 □ / △: 판정 첫 프레임에 클립이 닿는 거리(측정 뻗음 − 0.04)까지 붙었나, 주먹(발)이 상대 몸 표면에 닿았나.
        /// 붙는 거리가 1.2m 를 넘으면 예전 자석(사거리 − 0.15)
        [UnityTest]
        public IEnumerator C05b_Magnet_ClipContact()
        {
            Lab.Floor(0f, 40f);
            var log = new List<string>();
            var pc = SpawnSiwoo(Vector3.zero, 0f);
            var model = Object.Instantiate(Load("Assets/_Project/Prefabs/Enemy_Seokdal.prefab"));
            var d = CombatFactory.Dummy("허수아비", new Vector3(0f, 0f, 1.6f), 180f, 999, model, 1.77f, 0.30f, tune);
            var fa = pc.GetComponentInChildren<FighterAnim>();
            var a = fa.Animator;
            yield return Frames(5);
            pc.Begin(0f);
            yield return Frames(10);

            IEnumerator Case(string name, Btn b, float surf0, HumanBodyBones limb, bool expectContact, float[] outGap)
            {
                pc.Me.ResetFighter(); d.ResetFighter();
                ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
                d.Body.Place(new Vector3(0f, 0f, surf0 + d.Radius), 180f);
                yield return Frames(40);
                pc.Press(b);
                AttackRun run = null;
                float gapLimb = 9f, surfAt = -1f;
                bool contact = false;
                for (int i = 0; i < 80; i++)
                {
                    yield return null;
                    var r = pc.Me.Run;
                    if (r != null && run == null) { run = r; contact = r.Contact >= 0f; }
                    if (run != null && r == run && (r.ActiveNow || r.PastActive) && surfAt < 0f)
                    {
                        HitResolver.Measure(pc.Me.Position, pc.Me.Yaw, d.Position, d.Radius, out _, out surfAt, out _);
                    }
                    if (surfAt >= 0f && i < 80)
                    {
                        // 그린 자세(애니메이터 갱신 뒤)의 치는 끝 ↔ 상대 몸통 축 거리 − 반지름(수평)
                        var t = a.GetBoneTransform(limb);
                        if (t != null)
                        {
                            var off = HitResolver.Flat(t.position - d.Position);
                            gapLimb = Mathf.Min(gapLimb, off.magnitude - d.Radius);
                        }
                    }
                    if (run != null && pc.Me.Run != run && surfAt >= 0f) break;
                }
                float reach = pc.Me.ReachOf != null && run != null ? pc.Me.ReachOf(run.Move) : -1f;
                log.Add($"{name}: 시작 표면 {surf0:F2}m · 닿는 거리 자석 {contact} · 클립 뻗음 {reach:F2}m · 판정 첫 프레임 표면 {surfAt:F2}m · 치는 끝 ↔ 몸 표면 최소 {gapLimb:F2}m · 사거리 {(run != null ? run.Move.Range : 0f):F1}");
                Assert.AreEqual(expectContact, contact, name + " 자석 종류");
                if (expectContact)
                {
                    Assert.That(surfAt, Is.EqualTo(reach - tune.ContactSink).Within(0.06f), name + " 판정 첫 프레임 표면 거리 = 뻗음 − 0.04");
                    Assert.That(gapLimb, Is.LessThanOrEqualTo(0.12f), name + " 치는 끝이 몸에 닿음(≤ 0.12m)");
                }
                outGap[0] = gapLimb;
            }
            var g = new float[1];
            float kick = pc.Me.ReachOf(pc.Moves.FrontKick) - tune.ContactSink;     // 앞차기 클립이 닿는 표면 거리
            yield return Case("□ 잽(1.3m)", Btn.Light, 1.3f, HumanBodyBones.LeftHand, true, g);
            yield return Case("□ 잽(0.9m — 사거리 안이어도 붙음)", Btn.Light, 0.9f, HumanBodyBones.LeftHand, true, g);
            yield return Case($"△ 앞차기({kick + 1.1f:F2}m — 미끄러짐 1.1m)", Btn.Heavy, kick + 1.1f, HumanBodyBones.RightFoot, true, g);
            yield return Case($"△ 앞차기({kick + 1.35f:F2}m — 1.35m 라 1.2m 넘음 → 예전 자석)", Btn.Heavy, kick + 1.35f, HumanBodyBones.RightFoot, false, g);
            Debug.Log("[M2Test] C05b 닿는 거리 자석: " + string.Join(" | ", log));
        }

        // ───────────────────────── C14b 시우 애니메이터
        static bool InState(Animator a, string st)
        {
            var c = a.GetCurrentAnimatorStateInfo(0);
            var n = a.GetNextAnimatorStateInfo(0);
            int h = Animator.StringToHash(st);
            return c.shortNameHash == h || (a.IsInTransition(0) && n.shortNameHash == h);
        }

        [UnityTest]
        public IEnumerator C14b_Anim_Siwoo()
        {
            Lab.Floor(0f, 40f);
            var pc = SpawnSiwoo(Vector3.zero, 0f);
            var fa = pc.GetComponentInChildren<FighterAnim>();
            Assert.IsNotNull(fa, "프리팹에 FighterAnim");
            Assert.IsNotNull(fa.Clips, "ClipTable 연결");
            var a = fa.Animator;
            var d = CombatFactory.Dummy("허수아비", new Vector3(0f, 0f, 1.0f), 180f, 999, null, 1.80f, 0.30f, tune);
            yield return Frames(5);
            pc.Begin(0f);
            yield return Frames(20);
            Assert.AreEqual("CombatMove", fa.Current, "전투 대기 = CombatMove");
            var log = new List<string> { "대기 " + fa.Current };

            // □ 네 번(잽 · 크로스 · 훅 · 크로스 끝) — 각 기술의 판정 첫 프레임에서 클립 시각 vs 표의 타격 시각
            var seen = new List<string>();
            var lags = new List<string>();
            var trace = new List<string>();
            AttackRun traced = null;
            AttackRun last = null;
            int g = 0;
            for (int i = 0; i < 200 && g < 4; i++)
            {
                if (pc.Me.State == Fighter.Phase.Free || (pc.Me.Run != null && pc.Me.Run.LinkOpen)) { if (i % 6 == 0) pc.Press(Btn.Light); }
                yield return null;
                var r = pc.Me.Run;
                if (r != null && r != traced) { traced = r; trace.Add("‖ " + r.Move.Label); }
                if (r != null && !r.ActiveNow && r.T < r.Move.ActiveEnd)
                {
                    bool tr = a.IsInTransition(0);
                    var ci = a.GetCurrentAnimatorStateInfo(0); var ni = a.GetNextAnimatorStateInfo(0);
                    trace.Add($"T{r.T * 60:F1}f ts{Time.timeScale:F0} {fa.Current} 전환{(tr ? 1 : 0)} 현재 {ci.normalizedTime * ci.length:F3}/{ci.length:F2}×{ci.speed * ci.speedMultiplier:F2} 다음 {ni.normalizedTime * ni.length:F3}/{ni.length:F2}");
                }
                if (r != null && r != last && r.ActiveNow)
                {
                    last = r;
                    g++;
                    string st = ClipTable.StateFor(r.Move);
                    seen.Add($"{r.Move.Label}→{fa.Current}");
                    Assert.AreEqual(st, fa.Current, r.Move.Label + " 상태");
                    Assert.IsTrue(InState(a, st), r.Move.Label + " 애니메이터가 그 상태(또는 그리로 넘어가는 중)");
                    Assert.That(fa.Rate, Is.InRange(0.2f, fa.MaxRate + 1e-3f), r.Move.Label + " 재생 배율 0.2~1.6");
                    if (fa.Clips.TryGet(st, out var e) && e.Hit > 0f)
                    {
                        var info = a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash(st) ? a.GetNextAnimatorStateInfo(0) : a.GetCurrentAnimatorStateInfo(0);
                        float clipT = info.normalizedTime * e.Length;
                        float lag = (clipT - e.Hit) / Mathf.Max(0.2f, fa.Rate) * 60f;
                        lags.Add($"{r.Move.Label} 클립 {clipT:F2}/{e.Hit:F2}초(배율 ×{fa.Rate:F2}, 어긋남 {lag:+0.0;-0.0}f)");
                        if (Mathf.Abs(lag) > 3f) Debug.Log("[M2Test] C14b 프레임별: " + string.Join(" | ", trace));
                        Assert.That(Mathf.Abs(lag), Is.LessThanOrEqualTo(3f), r.Move.Label + " 판정 첫 프레임에 클립이 타격 시각 ± 3f");
                    }
                }
            }
            Assert.AreEqual(4, g, "□ 네 번 모두 나감");
            log.Add("□×4: " + string.Join(", ", seen));
            log.Add("타격 맞춤: " + string.Join(" / ", lags));

            // 잡기 → 무릎 · 놓기
            yield return Frames(60);
            pc.Me.ResetFighter(); d.ResetFighter();
            ((PlayerBody)pc.Me.Body).Place(Vector3.zero, 0f);
            d.Body.Place(new Vector3(0f, 0f, 0.8f), 180f);
            yield return Frames(10);
            pc.Press(Btn.Grab);
            var grab = new List<string>();
            for (int i = 0; i < 40; i++) { yield return null; if (grab.Count == 0 || grab[grab.Count - 1] != fa.Current) grab.Add(fa.Current); }
            Assert.AreEqual(d, pc.Held, "잡힘");
            Assert.Contains("Grab", grab, "잡기 상태");
            pc.Press(Btn.Light);
            for (int i = 0; i < 30; i++) { yield return null; if (grab[grab.Count - 1] != fa.Current) grab.Add(fa.Current); }
            Assert.Contains("Knee", grab, "클린치 무릎 상태");
            log.Add("잡기: " + string.Join(" → ", grab));

            // 피격(중) · 다운
            yield return Frames(90);
            pc.End(); pc.Me.ResetFighter(); pc.Begin(0f);
            yield return Frames(10);
            pc.Me.SetStagger(0.4f);
            var hurt = new List<string> { fa.Current };
            pc.Me.StartDown(Vector3.back, 0.5f);
            for (int i = 0; i < 400; i++)
            {
                yield return null;
                if (hurt[hurt.Count - 1] != fa.Current) hurt.Add(fa.Current);
                if (pc.Me.State == Fighter.Phase.Free && i > 30) break;
            }
            log.Add("다운: " + string.Join(" → ", hurt));
            Assert.That(hurt, Does.Contain("Fall").And.Contain("Lie").And.Contain("StandUp"), "쓰러짐 → 누움 → 일어서기");
            Debug.Log("[M2Test] C14b 시우 클립: " + string.Join(" | ", log));
        }

        // ───────────────────────── C14c 적 모델 애니메이터
        [UnityTest]
        public IEnumerator C14c_Anim_Enemy()
        {
            Lab.Floor(0f, 40f);
            var pc = SpawnSiwoo(Vector3.zero, 0f);
            pc.Me.MaxHp = pc.Me.Hp = 9999;
            var dir = CombatFactory.Director(pc.Me, tune);
            var model = Object.Instantiate(Load(EnemyPath));
            var def = EnemyLib.Kkanjok();
            var b = CombatFactory.Enemy(def, new Vector3(0f, 0f, 1.2f), 180f, pc.Me, dir, 0, tune, model);
            b.NoAttack = true;
            var fa = b.GetComponentInChildren<FighterAnim>();
            var hs = b.GetComponentInChildren<HandShape>();
            Assert.IsNotNull(fa, "적 프리팹에 FighterAnim");
            Assert.IsNotNull(hs, "적 프리팹에 HandShape");
            var a = fa.Animator;
            Assert.IsTrue(a.isHuman, "적 Humanoid");
            yield return Frames(5);
            Assert.IsTrue(hs.Ready, "적 메시에 손 셰이프 키");
            var log = new List<string> { $"대기 {fa.Current}" };
            b.Activate();
            pc.Begin(0f);
            yield return Frames(20);
            log.Add($"활성 {fa.Current} · 주먹 {hs.Weight("Fist_R"):F0}");
            Assert.That(hs.Weight("Fist_R"), Is.EqualTo(100f).Within(0.5f), "적 전투 중 주먹");

            // 원투: 잽 → 크로스(이어짐)
            var oneTwo = def.Moves.First(m => m.Followup != null);
            b.Me.StartAttack(oneTwo, pc.Me);
            var seq = new List<string> { fa.Current };
            for (int i = 0; i < 90; i++) { yield return null; if (seq[seq.Count - 1] != fa.Current) seq.Add(fa.Current); }
            log.Add("원투: " + string.Join(" → ", seq));
            Assert.That(seq, Does.Contain("Jab").And.Contain("Cross"), "원투 = 잽 → 크로스");
            Assert.IsTrue(seq.IndexOf("Jab") < seq.IndexOf("Cross"), "잽 다음 크로스");

            // 다운: 쓰러짐 → 누움 → 일어서기 → 전투 자세
            yield return Frames(30);
            b.Me.StartDown(Vector3.forward, 0.5f);
            var down = new List<string> { fa.Current };
            for (int i = 0; i < 400; i++)
            {
                yield return null;
                if (down[down.Count - 1] != fa.Current) down.Add(fa.Current);
                if (b.Me.State == Fighter.Phase.Free && i > 30) break;
            }
            yield return Frames(10);
            down.Add(fa.Current);
            log.Add("다운: " + string.Join(" → ", down));
            Assert.That(down, Does.Contain("Fall").And.Contain("Lie").And.Contain("StandUp"), "적 쓰러짐 → 누움 → 일어서기");
            Assert.IsTrue(InState(a, fa.Current), "애니메이터가 마지막 상태에 있음");
            Debug.Log("[M2Test] C14c 깐족이 클립: " + string.Join(" | ", log));
        }
    }
}
