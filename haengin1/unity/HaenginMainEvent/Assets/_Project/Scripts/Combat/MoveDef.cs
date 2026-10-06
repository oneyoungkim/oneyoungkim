// 행인1의 메인이벤트 — 기술 정의 (docs/08_M2_전투_설계.md 3-3·3-4·3-5·10-1)
// 숫자 단위: 프레임 f = 60fps 기준 1/60초(설계 단위). 실행은 초로 한다(10-3) — Sec(f) = f / 60.
// 기본값 = 08 문서 표. 에셋(Settings/SiwooMoves.asset 안의 하위 에셋)이 없으면 MoveLib 이 같은 값으로 만든다.
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/MoveDef")]
    public sealed class MoveDef : ScriptableObject
    {
        [Header("이름")]
        public string Label = "";
        [Tooltip("애니메이터 상태 이름(9단계에서 클립 연결)")] public string State = "";
        [Tooltip("동작 라이브러리 번호(anim_library.md)")] public int ClipId;

        [Header("프레임(f, 60fps)")]
        public int Startup = 7, Active = 3, Recovery = 10;
        [Tooltip("연결 창 = 판정 끝 + 이만큼 ~ 기술 끝(08 3-2: 3f)")] public int LinkAfter = 3;

        [Header("판정 — 공격자 기준 부채꼴(08 3-2)")]
        [Tooltip("가슴 중심 → 피격자 캡슐 표면 m")] public float Range = 1.1f;
        public float HalfAngle = 35f;
        public float HeightTol = 0.6f;
        public int MaxTargets = 1;

        [Header("결과")]
        public int Damage = 5;
        [Tooltip("경직 f")] public int Stagger = 20;
        [Tooltip("넉백 총거리 m")] public float Knock = 0.05f;
        [Tooltip("넉백을 공격자 오른쪽으로(왼손 훅)")] public bool KnockSide;
        [Tooltip("막혔을 때 넉백 m(음수 = 넉백의 절반)")] public float BlockKnock = -1f;
        public Power Power = Power.Light;
        [Tooltip("가드 게이지 깎는 양(음수 = 위력별 기본 15/25/40)")] public float GuardDmg = -1f;
        [Tooltip("시우가 맞혔을 때 기세")] public int Heat = 4;
        public string Word = "퍽!";
        public FlinchKind Flinch = FlinchKind.Head;

        [Header("플래그")]
        [Tooltip("막는 상대의 가드를 깨고 경직 48f(어퍼)")] public bool GuardBreak;
        [Tooltip("맞으면 다운")] public bool Down;
        [Tooltip("다운 때 뒤로 밀리는 거리 m")] public float DownKnock = 1.0f;
        [Tooltip("막기 불가('!!')")] public bool Unblockable;
        [Tooltip("맞은 쪽 경직을 '숙임'으로(스텝 무릎 — 숙임 중 잡기는 반드시 잡힘)")] public bool Crouch;
        [Tooltip("슈퍼아머 무시(큰 훅)")] public bool IgnoreArmor;
        [Tooltip("발생 동안 앞으로 들어가는 거리 m(스텝 무릎 0.4)")] public float Advance;
        [Tooltip("자석 최대 거리 m(08 2-3: 0.8)")] public float Magnet = 0.8f;
        [Tooltip("자석이 붙는 거리 = 사거리 + 이만큼(음수 = 조정값 0.8). 읽었다 반격은 회피 1.6m 를 메우게 2.4")] public float MagnetReach = -1f;
        [Tooltip("맞혔을 때 슬로(실제 시간 초, 0 = 없음)")] public float Slow;
        [Range(0.05f, 1f)] public float SlowScale = 0.3f;
        [Tooltip("히트스톱 위력을 따로(0 = Power 그대로)")] public Power StopPower = Power.None;
        [Tooltip("슈퍼아머 경직 게이지 깎는 양(음수 = 위력별 6/12/30, 스텝 무릎 20 — 08 4-1)")] public float StaggerGauge = -1f;

        [Header("적 공격 — 예고·돌진·연결(08 4-1·4-4)")]
        [Tooltip("예고 시작 → 판정 시작(초). 0 = 예고 없음. 발생보다 길면 그 차이만큼 발생 앞에 몸짓(윈드업)")] public float Lead;
        [Tooltip("예고 표시: 0 없음 · 1 '!' · 2 '!!'(막기 불가, 몸 테두리 흰 번쩍 2번)")] public int Warn;
        [Tooltip("돌진 거리 m · 시간 초(깐족이 달려들기 2.5m 0.45초): 예고 뒤 발생 앞에 들어간다")] public float ChargeDist, ChargeTime;
        [Tooltip("이어지는 기술(원투의 2타) — 이 기술의 연결 창에서 바로")] public MoveDef Followup;
        [Tooltip("클립 재생 배율(표의 '0.7배' 등) — 9단계 클립 연결에서 측정 배율에 곱함")] public float ClipRate = 1f;
        [Tooltip("판정 동안 앞으로 달리는 거리 m(스크럼 태클 4.0m — 판정 내내 검사, 맞히면 멈춤)")] public float ActiveAdvance;
        [Tooltip("몸을 던지는 공격(냉장고 전부 · 달려들기 · 태클 — 08 12장 11): 시작(예고 몸짓·돌진 포함)부터 판정 끝까지 시우 □ 연타에 경직 없음. 피해는 받음. △·잡기·기세·반격엔 끊김")] public bool Committed;

        public const float Fps = 60f;
        public static double Sec(int f) => f / (double)Fps;
        public double ActiveStart => Sec(Startup);
        public double ActiveEnd => Sec(Startup + Active);
        public double LinkAt => Sec(Startup + Active + LinkAfter);
        public double Total => Sec(Startup + Active + Recovery);
        public float BlockKnockDist => BlockKnock >= 0f ? BlockKnock : Knock * 0.5f;
        /// 발생 앞 준비 시간(초): 돌진 = 예고 + 돌진, 아니면 max(0, 예고 − 발생)
        public double PreTime => ChargeTime > 0f ? Lead + ChargeTime : System.Math.Max(0.0, Lead - ActiveStart);

        public MoveDef Clone()
        {
            var c = Instantiate(this);
            c.name = name;
            return c;
        }
    }

    /// 08 문서 표의 기본값으로 MoveDef 를 만든다(에셋 생성·테스트 공용)
    public static class MoveLib
    {
        static MoveDef M(string id, string label, int clip, int s, int a, int r, float range, float half, int dmg, int stag, float knock,
                         Power p, int heat, string word, FlinchKind fk = FlinchKind.Head)
        {
            var m = ScriptableObject.CreateInstance<MoveDef>();
            m.name = id;
            m.Label = label; m.State = id; m.ClipId = clip;
            m.Startup = s; m.Active = a; m.Recovery = r;
            m.Range = range; m.HalfAngle = half;
            m.Damage = dmg; m.Stagger = stag; m.Knock = knock;
            m.Power = p; m.Heat = heat; m.Word = word; m.Flinch = fk;
            return m;
        }

        // ── 3-3 타격기
        public static MoveDef Jab() => M("Jab", "잽", 191, 7, 3, 10, 1.1f, 35f, 5, 20, 0.05f, Power.Light, 4, "퍽!");
        public static MoveDef Cross() => M("Cross", "크로스", 192, 8, 3, 13, 1.2f, 35f, 7, 22, 0.10f, Power.Mid, 6, "빡!");
        public static MoveDef Hook()
        {
            var m = M("Hook", "훅", 193, 12, 3, 13, 1.0f, 60f, 8, 24, 0.10f, Power.Mid, 6, "퍽!", FlinchKind.Hook);
            m.MaxTargets = 2; m.KnockSide = true;
            return m;
        }
        public static MoveDef CrossEnd() => M("CrossEnd", "크로스(끝)", 192, 8, 3, 19, 1.2f, 35f, 9, 30, 0.30f, Power.Mid, 7, "빡!");
        public static MoveDef FrontKick()
        {
            var m = M("FrontKick", "앞차기", 209, 14, 4, 16, 1.5f, 30f, 12, 36, 1.2f, Power.Mid, 8, "빡!", FlinchKind.Body);
            m.MaxTargets = 2; m.GuardDmg = 40f;
            return m;
        }
        public static MoveDef Upper()
        {
            var m = M("Upper", "어퍼컷", 194, 13, 4, 13, 0.9f, 40f, 14, 40, 0.16f, Power.Heavy, 10, "콰직!", FlinchKind.Upper);
            m.GuardBreak = true;
            return m;
        }
        public static MoveDef StepKnee()
        {
            var m = M("StepKnee", "스텝 무릎", 211, 12, 4, 14, 0.8f, 40f, 12, 50, 0.10f, Power.Mid, 8, "퍽!", FlinchKind.Body);
            m.Advance = 0.4f; m.Crouch = true; m.StaggerGauge = 20f;
            return m;
        }
        public static MoveDef BigHook()
        {
            var m = M("BigHook", "큰 훅", 195, 16, 4, 16, 1.1f, 60f, 18, 0, 0f, Power.Heavy, 12, "콰직!", FlinchKind.Hook);
            m.MaxTargets = 2; m.Down = true; m.DownKnock = 1.0f; m.IgnoreArmor = true;
            return m;
        }

        // ── 3-4 잡기
        public static MoveDef Grab()
        {
            var m = M("Grab", "멱살 잡기", 259, 8, 4, 18, 0.9f, 50f, 0, 0, 0f, Power.None, 5, "");
            m.Unblockable = true;   // 가드 무시
            return m;
        }
        public static MoveDef Knee(bool third)
        {
            var m = third ? M("Knee3", "클린치 무릎(3)", 211, 9, 3, 10, 0.9f, 90f, 8, 0, 0f, Power.Mid, 4, "퍽!", FlinchKind.Body)
                          : M("Knee", "클린치 무릎", 211, 9, 3, 10, 0.9f, 90f, 6, 0, 0f, Power.Light, 4, "퍽!", FlinchKind.Body);
            m.LinkAfter = 0; m.Magnet = 0f; m.Unblockable = true;
            return m;
        }
        public static MoveDef Push()
        {
            var m = M("Push", "하체 밀기", 259, 10, 4, 16, 1.2f, 90f, 8, 40, 0f, Power.Mid, 8, "퍽!", FlinchKind.Body);
            m.Magnet = 0f; m.Unblockable = true;
            return m;
        }

        // ── 3-5 반격
        // 읽었다 반격의 자석: 회피(입력 없음 = 뒤 1.6m) 뒤에도 닿게 1.6 + 0.8 = 2.4m(08 3-5 구현 메모 — 문서의 0.8m 로는 뒤 회피 뒤 표면 2.7m 라 헛침)
        public const float CounterMagnet = 2.4f;
        public static MoveDef CounterCross()
        {
            var m = M("CounterCross", "카운터 크로스", 192, 6, 3, 13, 1.2f, 35f, 14, 40, 0.30f, Power.Heavy, 8, "빡!");
            m.Magnet = CounterMagnet; m.MagnetReach = CounterMagnet;
            return m;
        }
        public static MoveDef DuckUpper()
        {
            var m = M("DuckUpper", "더킹 어퍼", 194, 10, 4, 14, 0.9f, 40f, 20, 0, 0f, Power.Heat, 10, "콰직!", FlinchKind.Upper);
            m.Down = true; m.DownKnock = 1.0f; m.Slow = 0.5f; m.SlowScale = 0.3f;
            m.Magnet = CounterMagnet; m.MagnetReach = CounterMagnet;
            return m;
        }

        // ── 벽꽝·부딪힘(하체 밀기에서 생기는 타격 — 08 3-4). 기록·이펙트용 가짜 기술(한 번만 만든다)
        static MoveDef slam, bump;
        public static MoveDef Slam(CombatTuning t)
        {
            if (slam != null) return slam;
            slam = M("WallSlam", "벽꽝", 0, 0, 1, 0, 0f, 0f, t.SlamDamage, t.SlamStagger, 0f, Power.Heavy, t.SlamHeat, "쿵!", FlinchKind.Body);
            return slam;
        }
        public static MoveDef Bump(CombatTuning t)
        {
            if (bump != null) return bump;
            bump = M("Bump", "부딪힘", 0, 0, 1, 0, 0f, 0f, t.BumpDamage, t.BumpStagger, 0f, Power.Mid, 0, "퍽!", FlinchKind.Body);
            return bump;
        }

        // ── 4-4 적 기술(시험용 — 적 AI 는 7단계). 석 달 잽: 9/3/11, 1.1m, 5, 경직 18, 0.05
        public static MoveDef EnemyJab()
        {
            var m = M("EJab", "잽(적)", 191, 9, 3, 11, 1.1f, 35f, 5, 18, 0.05f, Power.Light, 0, "퍽!");
            return m;
        }
        /// 시험용 중타(깐족이 원투의 2타: 10/3/14, 8, 24, 0.10)
        public static MoveDef EnemyCross()
        {
            var m = M("ECross", "크로스(적)", 192, 10, 3, 14, 1.2f, 35f, 8, 24, 0.10f, Power.Mid, 0, "빡!");
            return m;
        }

        // ── 4-4 인카운터 3유형 공격표. 피해·경직은 시우가 받는 값
        /// 깐족이 원투: 191 9/3/12 → 192 10/3/14, 1.1m, 6 → 8, 경직 18 → 24, 넉백 .05 → .10, 약 → 중
        public static MoveDef KkOneTwo()
        {
            var a = M("KkJab", "원투(깐족이)", 191, 9, 3, 12, 1.1f, 35f, 6, 18, 0.05f, Power.Light, 0, "퍽!");
            var b = M("KkCross", "원투 2타(깐족이)", 192, 10, 3, 14, 1.1f, 35f, 8, 24, 0.10f, Power.Mid, 0, "빡!");
            a.Followup = b;
            return a;
        }
        /// 깐족이 달려들기: '!' 0.5초(몸 낮춤) → 510 돌진 2.5m 0.45초 → 192 10/3/16, 돌진 + 1.2m, 12, 경직 30, 0.3, 중, 가드 −25
        public static MoveDef KkCharge()
        {
            var m = M("KkCharge", "달려들기(깐족이)", 510, 10, 3, 16, 1.2f, 35f, 12, 30, 0.30f, Power.Mid, 0, "빡!");
            m.Lead = 0.5f; m.Warn = 1; m.ChargeDist = 2.5f; m.ChargeTime = 0.45f; m.GuardDmg = 25f; m.Committed = true;
            return m;
        }
        /// 석 달 잽: 191 9/3/11, 1.1m, 5, 18, .05, 약
        public static MoveDef SdJab() => M("SdJab", "잽(석 달)", 191, 9, 3, 11, 1.1f, 35f, 5, 18, 0.05f, Power.Light, 0, "퍽!");
        /// 석 달 원투: 191 9/3/12 → 192 10/3/14, 1.2m, 5 → 8, 18 → 24
        public static MoveDef SdOneTwo()
        {
            var a = M("SdOne", "원투(석 달)", 191, 9, 3, 12, 1.2f, 35f, 5, 18, 0.05f, Power.Light, 0, "퍽!");
            var b = M("SdTwo", "원투 2타(석 달)", 192, 10, 3, 14, 1.2f, 35f, 8, 24, 0.10f, Power.Mid, 0, "빡!");
            a.Followup = b;
            return a;
        }
        /// 석 달 카운터 훅: 193, '!' 0.2초, 10/3/14, 1.0m, 10, 24, .10, 중(막은 직후 0.25초 안 60%)
        public static MoveDef SdCounter()
        {
            var m = M("SdCounter", "카운터 훅(석 달)", 193, 10, 3, 14, 1.0f, 60f, 10, 24, 0.10f, Power.Mid, 0, "퍽!", FlinchKind.Hook);
            m.Lead = 0.2f; m.Warn = 1; m.KnockSide = true;
            return m;
        }
        /// 냉장고 큰 휘두르기: 193(0.7배), '!' 0.6초(팔을 크게 뒤로), 22/4/20, 1.2m, 16, 40, 0.3, 강, 가드 −40
        public static MoveDef NjSwing()
        {
            // 클립 128 양손 내려찍기(2026-10-06 2차 — 처음엔 훅 193 을 0.7배로 썼다). 위에서 내려찍으니 넉백은 정면, 젖힘은 머리
            var m = M("NjSwing", "큰 휘두르기(냉장고)", 128, 22, 4, 20, 1.2f, 60f, 16, 40, 0.30f, Power.Heavy, 0, "콰직!", FlinchKind.Head);
            m.Lead = 0.6f; m.Warn = 1; m.GuardDmg = 40f; m.Committed = true;
            return m;
        }
        /// 냉장고 앞차기: 206 Spartan_Kick, '!' 0.5초, 20/4/18, 1.5m, 14, 36, 1.5m(막아도 1.0m), 중
        public static MoveDef NjKick()
        {
            var m = M("NjKick", "앞차기(냉장고)", 206, 20, 4, 18, 1.5f, 30f, 14, 36, 1.5f, Power.Mid, 0, "빡!", FlinchKind.Body);
            m.Lead = 0.5f; m.Warn = 1; m.BlockKnock = 1.0f; m.Committed = true;
            return m;
        }
        // ── 4-6 야차 상대 스크럼(188cm·102kg)
        /// 스크럼 밀기: 260, 12/4/16, 1.2m, 6, 넉백 1.2m(막아도 0.8m), 중
        public static MoveDef ScPush()
        {
            var m = M("ScPush", "밀기(스크럼)", 260, 12, 4, 16, 1.2f, 40f, 6, 24, 1.2f, Power.Mid, 0, "퍽!", FlinchKind.Body);
            m.BlockKnock = 0.8f;
            return m;
        }
        /// 페이즈 2: 밀기 → 휘두르기 연결
        public static MoveDef ScPushChain()
        {
            var m = ScPush();
            m.name = "ScPush2"; m.Label = "밀기 → 휘두르기(스크럼)";
            m.Followup = ScSwing();
            return m;
        }
        /// 스크럼 휘두르기: 128 양손 내려찍기(문서 193 0.75배 → 2차 클립), '!' 0.5초, 20/4/18, 1.2m, 15, 경직 40, 강, 가드 −40
        public static MoveDef ScSwing()
        {
            var m = M("ScSwing", "휘두르기(스크럼)", 128, 20, 4, 18, 1.2f, 60f, 15, 40, 0.30f, Power.Heavy, 0, "콰직!", FlinchKind.Head);
            m.Lead = 0.5f; m.Warn = 1; m.GuardDmg = 40f;
            return m;
        }
        /// 스크럼 태클: 512, '!!' 0.6초(몸 낮추고 발 구름) + 흰 번쩍, 판정 = 돌진 4.0m(0.73초) 내내 몸 앞 0.8m, 22, 다운, 막기 불가
        public static MoveDef ScTackle()
        {
            var m = M("ScTackle", "태클(스크럼)", 512, 1, 44, 12, 0.8f, 50f, 22, 0, 0f, Power.Heavy, 0, "쿵!", FlinchKind.Body);
            m.Lead = 0.6f; m.Warn = 2; m.Unblockable = true; m.Down = true; m.DownKnock = 1.5f;
            m.ActiveAdvance = 4.0f; m.Magnet = 0f; m.LinkAfter = 0; m.Committed = true;
            return m;
        }

        /// 냉장고 껴안기: 259, '!!' 0.7초 + 흰 번쩍 2번, 24/6/30(헛방), 1.2m, 18, 다운 1.5m, 강, 막기 불가
        public static MoveDef NjHug()
        {
            var m = M("NjHug", "껴안기(냉장고)", 259, 24, 6, 30, 1.2f, 45f, 18, 0, 0f, Power.Heavy, 0, "쿵!", FlinchKind.Body);
            m.Lead = 0.7f; m.Warn = 2; m.Unblockable = true; m.Down = true; m.DownKnock = 1.5f; m.Committed = true;
            return m;
        }
    }
}
