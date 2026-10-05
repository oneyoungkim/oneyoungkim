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

        public const float Fps = 60f;
        public static double Sec(int f) => f / (double)Fps;
        public double ActiveStart => Sec(Startup);
        public double ActiveEnd => Sec(Startup + Active);
        public double LinkAt => Sec(Startup + Active + LinkAfter);
        public double Total => Sec(Startup + Active + Recovery);
        public float BlockKnockDist => BlockKnock >= 0f ? BlockKnock : Knock * 0.5f;

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
            m.Advance = 0.4f; m.Crouch = true;
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
    }
}
