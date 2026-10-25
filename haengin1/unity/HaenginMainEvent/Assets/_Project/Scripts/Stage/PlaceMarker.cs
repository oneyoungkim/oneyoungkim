// 행인1의 메인이벤트 — 장소 안 상호작용 표시 자리(2026-10-25 오픈월드 방향 — 기능은 새 설계 뒤, 지금은 자리만)
// Func: job_serving · job_dishes(국밥집) · job_counter · job_stock · job_waste(편의점) · sleep · save · ledger · haneul(시우네) · seat · class(교실)
// 바닥에 옅은 먹 원(자식 'Ring')만 그린다. 나중에 Interactable 을 붙여 알바·잠·저장·가계부를 연결.
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public sealed class PlaceMarker : MonoBehaviour
    {
        public string Place = "";
        public string Func = "";
        public string Label = "";
        public static readonly List<PlaceMarker> All = new List<PlaceMarker>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }
    }
}
