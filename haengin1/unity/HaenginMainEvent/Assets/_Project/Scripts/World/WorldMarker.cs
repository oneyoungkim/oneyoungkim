// 행인1의 메인이벤트 — 표시 자리(PlaceMarker) 하나 = 활동 하나(docs/13 11장 '무대 4곳' 행): × · E 로 WorldRunner.UseMarker.
// 안내 글은 지금 할 수 있는지·시각·돈(WorldRunner.MarkerPrompt). 오픈월드가 아닐 때(연습장)는 붙지 않는다(무대가 열린 뒤 WorldRunner 가 붙임).
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class WorldMarker : MonoBehaviour
    {
        public PlaceMarker Marker;
        WorldRunner runner;
        Interactable it;
        float left;

        public void Bind(WorldRunner r, PlaceMarker m)
        {
            runner = r; Marker = m;
            it = GetComponent<Interactable>();
            if (it == null) it = gameObject.AddComponent<Interactable>();
            it.Radius = 1.3f;
            it.Prompt = m.Label;
            it.Used -= Use; it.Used += Use;
        }

        void Use() { if (runner != null && Marker != null) runner.UseMarker(Marker.Func, transform); }

        void Update()
        {
            if (runner == null || it == null || Marker == null) return;
            left -= Time.unscaledDeltaTime;
            if (left > 0f) return;
            left = 0.3f;
            var p = runner.MarkerPrompt(Marker.Func);
            it.Prompt = string.IsNullOrEmpty(p) ? Marker.Label : p;
        }
    }
}
