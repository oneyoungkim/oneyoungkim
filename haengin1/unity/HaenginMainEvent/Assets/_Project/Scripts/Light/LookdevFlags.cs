// 행인1의 메인이벤트 — 룩데브 단계 켜고 끄기(docs/14 4장·9장 '구현 결과'): 화면 효과는 셰이더 전역 값으로, 디테일 소품은 'Detail14' 루트로.
// 기본 = 전부 켬. 비교 사진(ZLookShots -lookvariants)만 단계별로 끈다:
//   base   = ② 색 설계만(먹선·안개·빛·디테일·LUT 끔 — 먹선은 예전 #1A1417 고정색·굵기 2.4)
//   ink    = + ③ 먹선(따뜻한 짙은 갈색·굵기 2.0·붓 압력·색 경계 잔선)
//   haze   = + ④ 깊이 안개
//   light  = + ⑤ 땅 햇빛 얼룩·밤 빛 웅덩이 ×2
//   detail = + ⑥ 디테일 소품
//   lut    = + ① 시간대·장소 LUT(= 전부 켬, now 와 같음)
using UnityEngine;

namespace Haengin
{
    public static class LookdevFlags
    {
        public static bool Ink = true, Haze = true, Light = true, Detail = true, Lut = true;
        static readonly int IdInk = Shader.PropertyToID("_HaenginInkOn"), IdHaze = Shader.PropertyToID("_HaenginHazeOn"), IdLight = Shader.PropertyToID("_HaenginLightOn");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { Ink = Haze = Light = Detail = Lut = true; Push(); }

        /// 셰이더 전역 값(편집 모드 촬영에서도 DayLight.Apply 가 부름)
        public static void Push()
        {
            Shader.SetGlobalFloat(IdInk, Ink ? 1f : 0f);
            Shader.SetGlobalFloat(IdHaze, Haze ? 1f : 0f);
            Shader.SetGlobalFloat(IdLight, Light ? 1f : 0f);
        }

        /// 변형 이름(위 표) → 단계 켜기. 모르는 이름·'now' = 전부 켬
        public static void Variant(string v)
        {
            int lvl = v switch { "base" => 0, "ink" => 1, "haze" => 2, "light" => 3, "detail" => 4, _ => 5 };
            Ink = lvl >= 1; Haze = lvl >= 2; Light = lvl >= 3; Detail = lvl >= 4; Lut = lvl >= 5;
            Push();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.Contains("Detail14")) t.gameObject.SetActive(Detail);
            var dl = DayLight.Instance;
            if (dl != null) dl.RefreshLut();
            foreach (var s in Object.FindObjectsByType<StageLights>(FindObjectsSortMode.None)) s.RefreshLut();
            foreach (var g in Object.FindObjectsByType<GymLights>(FindObjectsSortMode.None)) g.RefreshLut();
        }
    }
}
