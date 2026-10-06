// 행인1의 메인이벤트 — A′ 흑백 컷(docs/09_M3_버티컬슬라이스_설계.md 2-2, FUJIMOTO 1-5 '해칭은 A′ 에만')
// 화면 전체 흑백 노탄 + 해칭 톤. 쇼크 컷과 같은 방식: URP Full Screen Pass(CutSetup 이 PC·Mobile 렌더러에 'InkMode' 로 붙임) + 전역 값 _HaenginInk(0~1).
// 학예회 회상·우유 미역국·아빠 밀기 회상·몽타주 일부. 컷신이 끝나거나 건너뛰면 0(CutCtx.End).
using UnityEngine;

namespace Haengin
{
    public static class InkMode
    {
        static readonly int Id = Shader.PropertyToID("_HaenginInk");
        public static float Amount { get; private set; }
        public static bool On => Amount > 0.001f;
        public static int Count { get; private set; }

        public static void Set(float v)
        {
            v = Mathf.Clamp01(v);
            if (v > 0.001f && Amount <= 0.001f) Count++;
            Amount = v;
            Shader.SetGlobalFloat(Id, v);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Amount = 0f; Count = 0; Shader.SetGlobalFloat(Id, 0f); }
    }
}
