// 행인1의 메인이벤트 — 적 정의 (docs/08_M2_전투_설계.md 4-4·4-6·10-1)
// 기본값 = 08 문서 4-4 표(인카운터 3유형). 에셋(Settings/Enemy_<종류>.asset, 기술은 하위 에셋)이 없으면 EnemyLib 이 같은 값으로 만든다.
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/EnemyDef")]
    public sealed class EnemyDef : ScriptableObject
    {
        public enum Kind { Kkanjok, Seokdal, Naengjanggo, Scrum }

        [Header("몸")]
        public Kind Type;
        public string Label = "적";
        [Tooltip("키 m(모델 실측: 깐족이 1.72 · 석 달 1.77 · 냉장고 1.84 · 스크럼 1.88)")] public float Height = 1.75f;
        [Tooltip("캡슐 반지름(4-1: 0.30, 덩치 0.38)")] public float Radius = 0.30f;
        public int Hp = 70;

        [Header("속도 m/s")]
        public float ApproachSpeed = 1.6f;
        [Tooltip("접근할 때 달리는가(깐족이)")] public bool ApproachRun;
        public float StrafeSpeed = 1.0f, AttackInSpeed = 2.5f;

        [Header("행동")]
        [Tooltip("개인 쿨다운(초)")] public float Cooldown = 1.5f;
        [Tooltip("간보기 2초마다 도발 확률")] public float TauntChance;
        [Tooltip("시우에게 잡혔을 때 뿌리치기까지(초): 깐족이 1.6 · 석 달 1.8 · 그 밖 2.0")] public float GrabHold = 2.0f;
        [Tooltip("막기형(4-5)")] public bool Blocks;
        [Tooltip("막기형 가드 게이지(석 달 60)")] public float GuardMax = -1f;
        [Tooltip("슈퍼아머 + 경직 게이지(냉장고 30)")] public bool Armor;
        public float ArmorGauge = 30f;
        [Tooltip("도주 HP 비율(깐족이 0.25 · 동료 모두 탈락일 때) — 0 = 안 함")] public float FleeHp;

        [Header("기술(같은 순서로 확률)")]
        public MoveDef[] Moves = new MoveDef[0];
        public float[] Weights = new float[0];
        [Tooltip("멀리서(표면 > 2m) 쓰는 기술(깐족이 달려들기)과 그 확률")] public MoveDef Far;
        public float FarChance;
        [Tooltip("막은 직후 카운터(석 달 카운터 훅)")] public MoveDef Counter;
    }

    /// 08 문서 4-4 표의 기본값
    public static class EnemyLib
    {
        public static EnemyDef Make(EnemyDef.Kind k) => k switch
        {
            EnemyDef.Kind.Kkanjok => Kkanjok(),
            EnemyDef.Kind.Seokdal => Seokdal(),
            EnemyDef.Kind.Naengjanggo => Naengjanggo(),
            _ => Kkanjok(),
        };

        /// 깐족이(돌진형): 172cm, HP 70, 접근 3.6(달림)·간보기 1.2, 쿨다운 1.2, 도발 25%, 원투 / 멀리서 달려들기 50%
        public static EnemyDef Kkanjok()
        {
            var d = ScriptableObject.CreateInstance<EnemyDef>();
            d.name = "Enemy_Kkanjok";
            d.Type = EnemyDef.Kind.Kkanjok; d.Label = "깐족이";
            d.Height = 1.72f; d.Radius = 0.30f; d.Hp = 70;
            d.ApproachSpeed = 3.6f; d.ApproachRun = true; d.StrafeSpeed = 1.2f; d.AttackInSpeed = 2.5f;
            d.Cooldown = 1.2f; d.TauntChance = 0.25f; d.GrabHold = 1.6f; d.FleeHp = 0.25f;
            d.Moves = new[] { MoveLib.KkOneTwo() }; d.Weights = new[] { 1f };
            d.Far = MoveLib.KkCharge(); d.FarChance = 0.5f;
            return d;
        }

        /// 석 달(막기형): 177cm, HP 90, 접근 1.6·간보기 1.0, 쿨다운 1.5, 도발 없음, 막기(가드 60), 잽 50% · 원투 50%, 막은 직후 카운터 훅
        public static EnemyDef Seokdal()
        {
            var d = ScriptableObject.CreateInstance<EnemyDef>();
            d.name = "Enemy_Seokdal";
            d.Type = EnemyDef.Kind.Seokdal; d.Label = "석 달";
            d.Height = 1.77f; d.Radius = 0.30f; d.Hp = 90;
            d.ApproachSpeed = 1.6f; d.StrafeSpeed = 1.0f; d.AttackInSpeed = 2.5f;
            d.Cooldown = 1.5f; d.TauntChance = 0f; d.GrabHold = 1.8f;
            d.Blocks = true; d.GuardMax = 60f;
            d.Moves = new[] { MoveLib.SdJab(), MoveLib.SdOneTwo() }; d.Weights = new[] { 0.5f, 0.5f };
            d.Counter = MoveLib.SdCounter();
            return d;
        }

        /// 냉장고(덩치형): 184cm·112kg, HP 160, 반지름 0.38, 접근 1.2·간보기 0.8·들어갈 때 2.0, 쿨다운 2.2, 도발(껌) 10%, 슈퍼아머(경직 게이지 30),
        /// 큰 휘두르기 40% · 앞차기 30% · 껴안기(막기 불가) 30%
        public static EnemyDef Naengjanggo()
        {
            var d = ScriptableObject.CreateInstance<EnemyDef>();
            d.name = "Enemy_Naengjanggo";
            d.Type = EnemyDef.Kind.Naengjanggo; d.Label = "냉장고";
            d.Height = 1.84f; d.Radius = 0.38f; d.Hp = 160;
            d.ApproachSpeed = 1.2f; d.StrafeSpeed = 0.8f; d.AttackInSpeed = 2.0f;
            d.Cooldown = 2.2f; d.TauntChance = 0.10f; d.GrabHold = 2.0f;
            d.Armor = true; d.ArmorGauge = 30f;
            d.Moves = new[] { MoveLib.NjSwing(), MoveLib.NjKick(), MoveLib.NjHug() }; d.Weights = new[] { 0.4f, 0.3f, 0.3f };
            return d;
        }
    }
}
