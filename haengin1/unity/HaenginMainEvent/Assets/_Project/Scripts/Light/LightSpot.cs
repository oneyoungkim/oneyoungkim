// 행인1의 메인이벤트 — Zone1 빛 자리(docs/09_M3_버티컬슬라이스_설계.md 3-2 '빛 자리'·3-5)
// 가로등·보안등·자판기·편의점 차양·국밥집 창·상가 간판 점광, 그리고 창문 불(발광 판). DayLight 가 프리셋에 따라 켜고 끈다.
// StorySetup 이 Zone1 을 만들 때 Zone1/Lights 아래에 시드 고정으로 붙인다(결정적 저장 — D07).
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public enum LightKind { Street, Security, Vending, ConvAwning, GukbapWindow, ShopSign, Window }

    [DisallowMultipleComponent]
    public sealed class LightSpot : MonoBehaviour
    {
        public LightKind Kind;
        /// 창문: 이 값보다 켜짐 비율이 크면 켬(0~1, 시드 고정)
        public float Threshold;
        /// 프리셋 색을 따르지 않는 고정 색(편의점 청록 등은 프리셋 값)
        public bool KeepColor;

        public static readonly List<LightSpot> All = new List<LightSpot>();
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Lit { get; private set; }

        public void Set(bool on, Color c, float intensity)
        {
            Lit = on;
            var l = GetComponent<Light>();
            if (l != null)
            {
                l.enabled = on;
                if (!KeepColor) l.color = c;
                l.intensity = intensity;
            }
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = on;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }
    }
}
