// 행인1의 메인이벤트 — 자전거 탄 자세(docs/14 6-4, 2026-10-26 페달 클립으로 다시): 시우 모델(교복·배달복 각각)에 붙는다.
// 바탕 자세 = 페달 1회전 = 1주기 클립 4종(tools/bike_anim.py — 순항·오르막 댄싱·내리막 관성·출발/정지, Humanoid, 루트 = 자전거 원점)
//   · 순항·댄싱은 애니메이터 Motion Time 을 크랭크 각에 묶음(BikePhase = θ/360) — 크랭크가 도는 만큼 정확히 밟는다(재생 속도 맞출 필요 없음)
//   · 출발·정지는 BikeT(0~1)로 클립 시각을 직접 몰고 크랭크(BikeMotor.SetCrank)·기울기를 bike_curves 곡선대로 — 한 발(왼발)을 땅에
//   · 내리막 관성은 시간 재생, 크랭크는 오른발 앞 수평(115.5°)으로 부드럽게 붙음
// 그 위 IK 는 보정만: 발목 → '실제 페달 자리 + 클립에서 그 페달과의 차이'(땅 디딘 발은 자전거 기준 자리), 손목 → 클립 손목 자리(자전거 기준), 회전은 클립 그대로.
//   같은 시우면 보정량 ≈ 0, 배달복 모델·자전거 배율이 다르면 이 보정이 발을 페달에 붙인다. 모델 자리는 옮기지 않는다(루트 = 자전거 원점 = Visual 원점).
// 기울기: 댄싱(8·sin(θ − 35.5°))·출발/정지(곡선)는 BikeVisual.ClipLean 으로 — BikeVisual 의 박자 흔들기는 끔(이중 기울기 방지).
// 정차 = 왼발만 땅(FootDown), 오른발은 페달. **내리지 않는다.**
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(30), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class BikeRider : MonoBehaviour
    {
        public const string RideState = "Ride";
        public const string CruiseState = "BikeCruise", ClimbState = "BikeClimb", CoastState = "BikeCoast", StartState = "BikeStart", StopState = "BikeStop";
        public const string PhaseParam = "BikePhase", TimeParam = "BikeT";
        public enum Mode { None, Hold, Start, Cruise, Climb, Coast, Stop }

        public BikeMotor Motor;
        public BikeVisual Bike;
        public BikeClips Clips;
        [Tooltip("IK 보정 무게(0 = 클립 그대로)")] [Range(0f, 1f)] public float FootIK = 1f, HandIK = 0.9f;
        public bool Riding { get; private set; }
        public Mode Current { get; private set; }
        /// 골반 – 클립 골반 자리(자전거 기준) 거리(m, 테스트)
        public float HipGap { get; private set; }
        /// 발목 – IK 목표 거리(m, 테스트 — 페달 위 발은 '페달 + 클립 차이', 땅 디딘 발은 땅 자리)
        public float FootGapL { get; private set; }
        public float FootGapR { get; private set; }
        public bool FootDownL { get; private set; }
        public int ModeChanges { get; private set; }
        /// 진단(테스트): 마지막 IK 목표(세상) · 클립 자세(자전거 좌표)
        public Vector3 TargetL => tgtL;
        public Vector3 TargetR => tgtR;
        public BikeClips.Pose LastPose { get; private set; }

        Animator anim;
        LocoAnim loco;
        Behaviour lean2;
        float modeT, coastT, noPedalT, lean;
        Vector3 tgtL, tgtR; bool tgtOk;
        Vector3 hipCorr;     // 모델 자리 보정(자전거 좌표) — 배달복 아바타가 클립 아바타보다 작아(humanScale 0.966) 골반이 2~3cm 낮게 오던 것을 클립 골반 자리로
        static readonly int IdPhase = Animator.StringToHash(PhaseParam), IdT = Animator.StringToHash(TimeParam);

        void Awake()
        {
            anim = GetComponent<Animator>();
            loco = GetComponent<LocoAnim>();
        }

        public void Begin(BikeMotor m, BikeVisual b)
        {
            Motor = m; Bike = b;
            if (anim == null) Awake();
            if (loco != null) loco.enabled = false;
            var bl = GetComponentInParent<BodyLean>();
            if (bl != null) { bl.enabled = false; lean2 = bl; bl.transform.localRotation = Quaternion.identity; bl.transform.localPosition = Vector3.zero; }
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
            hipCorr = Vector3.zero;
            Riding = true; enabled = true;
            if (Bike != null) Bike.UseClipLean = Clips != null;
            Current = Mode.None;
            Go(Mode.Hold, true);
        }

        public void End()
        {
            Riding = false;
            if (loco != null) loco.enabled = true;
            if (lean2 != null) { lean2.transform.localRotation = Quaternion.identity; lean2.enabled = true; }
            if (Bike != null) { Bike.UseClipLean = false; Bike.ClipLean = 0f; }
            if (Motor != null) Motor.ReleaseCrank();
            transform.localPosition = Vector3.zero; hipCorr = Vector3.zero;
            if (anim != null && anim.isActiveAndEnabled) anim.CrossFadeInFixedTime(CharSetupNames.Move, 0.2f, 0);
        }

        bool Has(string st) => anim != null && anim.HasState(0, Animator.StringToHash(st));

        void Go(Mode m, bool instant = false)
        {
            if (m == Current) return;
            var prev = Current;
            Current = m; modeT = 0f; ModeChanges++;
            if (anim == null || Clips == null) return;
            switch (m)
            {
                case Mode.Hold:
                    anim.SetFloat(IdT, 1f);
                    if (Has(StopState)) anim.CrossFadeInFixedTime(StopState, instant ? 0f : 0.2f, 0);
                    break;
                case Mode.Start:
                    anim.SetFloat(IdT, 0f);
                    if (Has(StartState)) anim.CrossFadeInFixedTime(StartState, instant ? 0f : 0.12f, 0);
                    break;
                case Mode.Stop:
                    anim.SetFloat(IdT, 0f);
                    if (Has(StopState)) anim.CrossFadeInFixedTime(StopState, 0.2f, 0);
                    break;
                case Mode.Cruise: if (Has(CruiseState)) anim.CrossFadeInFixedTime(CruiseState, prev == Mode.Start ? 0.1f : 0.2f, 0); break;
                case Mode.Climb: if (Has(ClimbState)) anim.CrossFadeInFixedTime(ClimbState, 0.2f, 0); break;
                case Mode.Coast: coastT = 0f; if (Has(CoastState)) anim.CrossFadeInFixedTime(CoastState, 0.3f, 0); break;
            }
        }

        float StartLen => Clips != null ? Clips.StartEnd / Clips.StartStop.Fps : 1.5f;
        float StopLen => Clips != null ? (Clips.StartStop.Frames - 1 - Clips.StopFrom) / Clips.StartStop.Fps : 1.67f;

        void Update()
        {
            if (!Riding || Motor == null || Bike == null) return;
            float dt = Time.deltaTime;
            modeT += dt;
            if (Clips == null) return;
            bool moving = Motor.Speed > 0.15f;
            bool pedal = Motor.Pedal > 0.01f;
            noPedalT = pedal ? 0f : noPedalT + dt;
            // ── 상태 고르기
            switch (Current)
            {
                case Mode.Hold:
                    if (Motor.Posture == BikePosture.Stand && moving) Go(Mode.Climb);
                    else if (pedal || moving) Go(Mode.Start);
                    break;
                case Mode.Start:
                    if (modeT >= StartLen) Go(Motor.Posture == BikePosture.Stand ? Mode.Climb : Mode.Cruise);
                    break;
                case Mode.Stop:
                    if (pedal && moving) Go(Mode.Cruise);
                    else if (modeT >= StopLen) Go(Mode.Hold);
                    break;
                default:
                    if (!moving && !pedal && Motor.Posture != BikePosture.Stand) Go(Mode.Stop);
                    else if (Motor.Posture == BikePosture.Stand) Go(Mode.Climb);
                    else if (Current == Mode.Climb) Go(Mode.Cruise);
                    else if (Current == Mode.Cruise && noPedalT > 0.25f && Motor.Speed > 0.3f) Go(Mode.Coast);
                    else if (Current == Mode.Coast && pedal) Go(Mode.Cruise);
                    break;
            }
            // ── 크랭크·기울기·클립 시각
            var ss = Clips.StartStop;
            float wantLean = 0f;
            switch (Current)
            {
                case Mode.Hold:
                    {
                        var p = BikeClips.Sample(ss, ss.Frames - 1);
                        Motor.SetCrank(p.Theta); wantLean = p.Lean; anim.SetFloat(IdT, 1f);
                        break;
                    }
                case Mode.Start:
                    {
                        float u = Mathf.Clamp01(modeT / StartLen);
                        var p = BikeClips.Sample(ss, u * Clips.StartEnd);
                        Motor.SetCrank(p.Theta); wantLean = p.Lean; anim.SetFloat(IdT, u);
                        if (u >= 1f) Motor.ReleaseCrank();
                        break;
                    }
                case Mode.Stop:
                    {
                        float u = Mathf.Clamp01(modeT / StopLen);
                        var p = BikeClips.Sample(ss, Clips.StopFrom + u * (ss.Frames - 1 - Clips.StopFrom));
                        Motor.SetCrank(p.Theta); wantLean = p.Lean; anim.SetFloat(IdT, u);
                        break;
                    }
                case Mode.Coast:
                    coastT += dt;
                    Motor.SetCrank(Mathf.MoveTowardsAngle(Motor.Crank, Clips.CoastTheta, 200f * dt));
                    break;
                case Mode.Climb:
                    Motor.ReleaseCrank();
                    wantLean = Clips.ClimbLeanAmp * Mathf.Sin((Motor.Crank - Clips.ClimbLeanPhase) * Mathf.Deg2Rad);
                    break;
                default:
                    Motor.ReleaseCrank();
                    break;
            }
            anim.SetFloat(IdPhase, Mathf.Repeat(Motor.Crank, 360f) / 360f);
            lean = Mathf.MoveTowards(lean, wantLean, 40f * dt);
            Bike.ClipLean = lean;
            FootDownL = Current == Mode.Hold || (Current == Mode.Stop && modeT > StopLen * 0.6f) || (Current == Mode.Start && modeT < StartLen * 0.35f);
        }

        /// 지금 상태의 클립 자세(자전거 좌표)
        BikeClips.Pose Pose()
        {
            var ss = Clips.StartStop;
            switch (Current)
            {
                case Mode.Hold: return BikeClips.Sample(ss, ss.Frames - 1);
                case Mode.Start: return BikeClips.Sample(ss, Mathf.Clamp01(modeT / StartLen) * Clips.StartEnd);
                case Mode.Stop: return BikeClips.Sample(ss, Clips.StopFrom + Mathf.Clamp01(modeT / StopLen) * (ss.Frames - 1 - Clips.StopFrom));
                case Mode.Climb: return BikeClips.Sample(Clips.Climb, Mathf.Repeat(Motor.Crank, 360f) / 360f * (Clips.Climb.Frames - 1));
                case Mode.Coast: return BikeClips.Sample(Clips.Coast, coastT * Clips.Coast.Fps);
                default: return BikeClips.Sample(Clips.Cruise, Mathf.Repeat(Motor.Crank, 360f) / 360f * (Clips.Cruise.Frames - 1));
            }
        }

        void OnAnimatorIK(int layer)
        {
            tgtOk = false;
            if (!Riding || !enabled || Motor == null || Bike == null || Clips == null || layer != 0) return;
            var p = Pose();
            LastPose = p;
            var bt = Bike.transform;
            // 페달 위 발: 실제 페달 자리 + 클립 차이(자전거 좌표 → 세상) · 땅 디딘 발: 자전거 기준 자리
            Vector3 Foot(bool on, Vector3 v, Transform pedal) => on && pedal != null ? pedal.position + bt.TransformVector(v) : bt.TransformPoint(v);
            tgtL = Foot(p.OnL, p.AnkleL, Bike.PedalL); tgtR = Foot(p.OnR, p.AnkleR, Bike.PedalR);
            tgtOk = true;
            Goal(AvatarIKGoal.LeftFoot, tgtL, FootIK, AvatarIKHint.LeftKnee, tgtL + bt.forward * 0.45f + bt.up * 0.45f - bt.right * 0.05f);
            Goal(AvatarIKGoal.RightFoot, tgtR, FootIK, AvatarIKHint.RightKnee, tgtR + bt.forward * 0.45f + bt.up * 0.45f + bt.right * 0.05f);
            var wl = bt.TransformPoint(p.WristL); var wr = bt.TransformPoint(p.WristR);
            Goal(AvatarIKGoal.LeftHand, wl, HandIK, AvatarIKHint.LeftElbow, wl - bt.right * 0.3f - bt.forward * 0.25f - bt.up * 0.1f);
            Goal(AvatarIKGoal.RightHand, wr, HandIK, AvatarIKHint.RightElbow, wr + bt.right * 0.3f - bt.forward * 0.25f - bt.up * 0.1f);
        }

        void Goal(AvatarIKGoal g, Vector3 pos, float w, AvatarIKHint h, Vector3 hint)
        {
            anim.SetIKPositionWeight(g, w);
            anim.SetIKPosition(g, pos);
            anim.SetIKRotationWeight(g, 0f);
            anim.SetIKHintPositionWeight(h, w * 0.6f);
            anim.SetIKHintPosition(h, hint);
        }

        void LateUpdate()
        {
            if (!Riding || Clips == null || Bike == null || !tgtOk) return;
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot); var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (lf != null) FootGapL = (lf.position - tgtL).magnitude;
            if (rf != null) FootGapR = (rf.position - tgtR).magnitude;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                var bt = Bike.transform;
                var want = Pose().Hips;
                var have = bt.InverseTransformPoint(hips.position);
                HipGap = (have - want).magnitude;
                // 다음 프레임부터 모델을 그만큼 옮김(자전거 좌표 — 천천히 수렴, 2cm 넘는 차이는 0.3초 안에). 페달·손잡이는 IK 가 붙잡음
                var err = want - (have - hipCorr);
                err = Vector3.ClampMagnitude(err, 0.08f);
                hipCorr = Vector3.Lerp(hipCorr, err, 1f - Mathf.Exp(-Time.deltaTime * 6f));
                if (transform.parent != null) transform.localPosition = transform.parent.InverseTransformVector(bt.TransformVector(hipCorr));
            }
        }
    }

    /// 애니메이터 상태 이름(CharSetup 과 같은 값 — 런타임 어셈블리에서 쓰려고)
    public static class CharSetupNames { public const string Move = "Move"; }
}
