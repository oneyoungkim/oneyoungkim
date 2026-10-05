// 행인1의 메인이벤트 — 접근성 설정(docs/08_M2_전투_설계.md 5-1·7-5, 11장 16단계)
// '흔들림 줄이기' = PlayerPrefs haengin.fx.reduced(1/0). 켜면 트라우마 ×0.3, 화면 번쩍·쇼크 컷·슬로 끔(히트스톱은 둠).
// 일시정지 메뉴(GameUi)가 바꾸고, CombatTuning.ReducedNow 가 조정값과 함께 읽는다.
using UnityEngine;

namespace Haengin
{
    public static class Accessibility
    {
        public const string ReducedKey = "haengin.fx.reduced";
        static int reduced = -1;

        public static bool Reduced
        {
            get
            {
                if (reduced < 0) reduced = PlayerPrefs.GetInt(ReducedKey, 0) == 1 ? 1 : 0;
                return reduced == 1;
            }
            set
            {
                reduced = value ? 1 : 0;
                PlayerPrefs.SetInt(ReducedKey, reduced);
                PlayerPrefs.Save();
                Shake.Reduced = value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { reduced = -1; }
    }
}
