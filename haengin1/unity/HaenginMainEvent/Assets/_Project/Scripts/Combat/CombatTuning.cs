// 행인1의 메인이벤트 — 전투 조정값 (docs/08_M2_전투_설계.md 2장·3장·5-1·10-1)
// 기본값 = 08 문서 숫자. 에셋 Settings/CombatTuning.asset 은 없을 때만 만든다(손으로 고친 값 보존).
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/CombatTuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Header("타격감 5-1 (위력 0 없음 · 1 약 · 2 중 · 3 강 · 4 기세)")]
        public float[] HitStop = { 0f, 0.060f, 0.095f, 0.160f, 0.200f };
        public float[] Trauma = { 0f, 0.28f, 0.45f, 0.80f, 1.00f };
        [Tooltip("피격 젖힘 무게(시안 .55 + p × .3)")] public float[] FlinchW = { 0f, 0.85f, 1.15f, 1.45f, 1.45f };
        [Tooltip("막혔을 때 히트스톱·흔들림 = 약 단계 × 이 배율")] public float BlockFx = 0.5f;
        public float FlashTime = 0.1f, FlashLevel = 0.5f;
        [Tooltip("히트 셰이크(히트스톱 동안 피격자 떨림) 가로·앞뒤 ± m")] public float JitterX = 0.015f, JitterZ = 0.010f;
        [Tooltip("트라우마 감소(실제 시간 /초)")] public float TraumaDecay = 1.8f;
        [Tooltip("줌 펀치: 거리 × (1 − Amount·p), p 는 e^(−Decay·t)")] public float PunchAmount = 0.10f, PunchDecay = 5f;
        [Tooltip("다운 착지 트라우마")] public float DownTrauma = 0.5f;
        [Tooltip("젖힘 감소(게임 시간, e^(−k·t))")] public float FlinchDecay = 9f;
        [Tooltip("뼈마다 젖힘 최대(°)")] public float FlinchMax = 60f;
        [Tooltip("넉백 스프링: 속도 = 거리 × k, 감쇠 e^(−k·t) (총거리 = 거리)")] public float KnockK = 10f;
        [Tooltip("이 거리 이상 넉백은 ease-out 으로")] public float BigKnock = 0.3f, BigKnockTime = 0.25f;

        [Header("입력 2-2")]
        [Tooltip("버퍼(실제 시간 초)")] public float Buffer = 0.20f;
        [Tooltip("기술 끝 + 이 시간 안에 □/△ 가 없으면 콤보 순번 0")] public float ComboReset = 0.15f;
        [Tooltip("전투 시작 뒤 공격 입력을 받기까지(08 2-5: 0.8 → 1.2초)")] public float InputDelay = 0.4f;

        [Header("소프트 조준·자석 2-3")]
        public float AimRadius = 3.5f, AimAngle = 75f, AimAngleW = 0.02f, AimTelegraphBonus = 0.5f;
        [Tooltip("공격 시작 때 대상 쪽으로 몸 돌리기: AimTurnFrames 동안 최대 AimTurnMax°")] public float AimTurnMax = 45f;
        public int AimTurnFrames = 3;
        [Tooltip("사거리 + 이만큼 안이면 미끄러짐")] public float MagnetReach = 0.8f;
        [Tooltip("미끄러진 뒤 남길 거리(사거리 − 이만큼)")] public float MagnetMargin = 0.15f;
        [Tooltip("닿는 거리 자석(11-3 결정 1): 클립이 실제로 닿는 거리(측정 뻗음)까지 붙여 주는 최대 미끄러짐 m. 넘으면 예전 자석(사거리 − 0.15, 0.8m)")] public float ContactMax = 1.2f;
        [Tooltip("닿는 거리에서 주먹이 상대 표면 안으로 들어가게 더 붙는 깊이 m")] public float ContactSink = 0.04f;

        [Header("락온 2-3")]
        public float LockRadius = 10f, LockAngle = 70f, LockTurnRate = 540f, LockLoseDist = 12f, LockHideTime = 1.0f, LockNextDelay = 0.4f;
        public float FlickNeutral = 0.3f, FlickThresh = 0.75f, FlickTime = 0.15f, FlickAngle = 60f;

        [Header("이동 2-4 (m/s)")]
        public float WalkFwd = 1.6f, WalkBack = 1.3f, WalkSide = 1.3f, WalkGuard = 1.0f;

        [Header("시우 3-2·3-6")]
        public int PlayerHp = 200;
        public float GuardDmgRatio = 0.2f;
        [Tooltip("시우 경직 f: 약 18 · 중 24 · 강 40")] public int[] PlayerStagger = { 0, 18, 24, 40, 40 };

        [Header("회피 3-5 (f)")]
        public int DodgeTotal = 24, IFrameStart = 2, IFrameEnd = 14, DodgeMoveF = 14;
        public float DodgeDist = 1.6f;
        [Tooltip("회피 → 공격으로 끊기 / 연속 회피 / 2연속 뒤 더 기다림")] public int DodgeCancel = 15, DodgeChain = 18, DodgeDoubleWait = 15;

        [Header("읽었다 3-5")]
        public float ReadSlow = 0.3f, ReadScale = 0.5f, ReadWindow = 0.6f;
        public int ReadHeat = 15;

        [Header("막기 3-5")]
        public float GuardMax = 100f;
        [Tooltip("가드 게이지 깎임: 약 15 · 중 25 · 강 40")] public float[] GuardCost = { 0f, 15f, 25f, 40f, 40f };
        public float GuardRegenDelay = 0.8f, GuardRegen = 30f, GuardAngle = 60f;
        public int CrushStagger = 48, CrushHeat = -5, BlockHeat = 2, GuardBreakStagger = 48;

        [Header("전진 버팀 3-5")]
        public float BraceCooldown = 2.0f;
        public int BraceHeat = 3;

        [Header("기세 3-7")]
        public float HeatStart = 20f, HeatIdle = 4f, HeatDecay = 3f;
        public int DownHeat = -20;

        [Header("다운 3-6·4-1 (초)")]
        public float DownFall = 0.42f, LiePlayer = 2.0f, LieEnemy = 1.0f, LieMashCut = 0.1f, LieMin = 0.8f, GetUp = 0.85f;
        public float AfterUpPlayer = 0.5f, AfterUpEnemy = 0.3f;

        [Header("잡기 3-4")]
        public float GrabHold = 2.0f, GrabDist = 0.55f, TurnTime = 0.35f;
        public int ShakeOffStagger = 18, ReleaseStagger = 12;
        public float ShakeOffPush = 0.3f, ReleaseSep = 0.3f, PushDist = 1.8f, PushTime = 0.3f;
        public int PushStagger = 40, SlamDamage = 6, SlamStagger = 60, SlamHeat = 12, BumpDamage = 4, BumpStagger = 30;

        [Header("적 4장")]
        [Tooltip("슈퍼아머 경직 게이지 깎임: 약 6 · 중 12 · 강 30(스텝 무릎은 기술에 20)")] public float[] ArmorCost = { 0f, 6f, 12f, 30f, 30f };
        public int ArmorBreakStagger = 60;
        [Tooltip("경직 게이지가 마지막으로 깎인 뒤 이 시간이면 가득(초)")] public float ArmorRefill = 3f;
        [Tooltip("적 공격권(4-3): 동시 공격 최대(보통 1 · 어려움 2) · 한 적 공격 끝 → 다음 적 시작 최소 간격 · 굶김 한도(초)")] public int MaxAttackers = 1;
        public float AttackGap = 0.6f, StarveLimit = 15f;
        [Tooltip("자리(4-2): 공격권 가진 적 2.0~2.6m, 나머지 3.5~5.0m, 서로 70° 이상, 카메라 정면 ±40° 우선, 서로 밀어내기 0.9m")] public float RingNear = 2.3f, RingFar = 4.2f, SlotSep = 70f, CamArc = 40f, Separate = 0.9f;
        [Tooltip("공격권을 받으면 사거리까지 들어가는 최대 거리(m)")] public float AttackInMax = 1.5f;
        [Tooltip("화면 밖에서 공격하면 발생 + 이만큼(초)")] public float OffscreenDelay = 0.2f;
        [Tooltip("도발(88, 1.4초) 중 맞으면 경직 ×1.5 · 1.0초 안 맞고 버티면 시우 기세 +8")] public float TauntTime = 1.4f, TauntMul = 1.5f, TauntHold = 1.0f;
        public int TauntHeat = 8;
        [Tooltip("막기형(4-5): 막기 유지(마지막 공격 뒤) · 카운터 창 · 카운터 확률")] public float BlockHold = 0.4f, CounterWindow = 0.25f, CounterChance = 0.6f;

        [Header("난수")]
        public int Seed = 20261006;

        [Header("접근성")]
        [Tooltip("흔들림 줄이기: 트라우마 ×0.3, 슬로 끔(히트스톱은 둠)")] public bool Reduced;

        static CombatTuning fallback;
        /// 에셋이 없을 때 쓰는 기본값(테스트·시험장)
        public static CombatTuning Default => fallback != null ? fallback : (fallback = CreateInstance<CombatTuning>());

        public float Stop(Power p) => HitStop[Mathf.Clamp((int)p, 0, 4)];
        public float Tr(Power p) => Trauma[Mathf.Clamp((int)p, 0, 4)];
    }
}
