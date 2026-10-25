// 행인1의 메인이벤트 — 동네 사람(손님·동료): × · E 로 엿들으면 소문 단계에 맞는 한마디(docs/13 9-5 · W12). 오픈월드가 아닐 때는 그냥 말없음.
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class RumorNpc : MonoBehaviour
    {
        public string Who = "손님";
        public string Last { get; private set; } = "";

        void Start()
        {
            var it = GetComponent<Interactable>();
            if (it == null) { it = gameObject.AddComponent<Interactable>(); it.Radius = 1.4f; }
            it.Prompt = $"{Who} — 엿듣기";
            it.Used += () => { if (WorldRunner.On) Last = WorldRunner.Instance.Bark(Who); };
        }
    }
}
