// 행인1의 메인이벤트 — 상호작용 자리 (07 6장 × / E, docs/08_M2_전투_설계.md 8-2 '심판 형')
// 시우가 Radius 안에 있으면 HUD 에 「× 야차 — 들어가기」, 탐색 맵 Interact(× / E)를 누르면 Used.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public sealed class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();
        public string Prompt = "말 걸기";
        public float Radius = 2.0f;
        public event Action Used;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        public static Interactable Nearest(Vector3 p)
        {
            Interactable best = null;
            float bd = float.MaxValue;
            foreach (var i in All)
            {
                if (i == null || !i.isActiveAndEnabled) continue;
                float d = new Vector2(i.transform.position.x - p.x, i.transform.position.z - p.z).magnitude;
                if (d <= i.Radius && Mathf.Abs(i.transform.position.y - p.y) < 2.5f && d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// 가장 가까운 것을 쓴다(없으면 false)
        public static bool TryUse(Vector3 p)
        {
            var i = Nearest(p);
            if (i == null) return false;
            i.Use();
            return true;
        }

        public void Use() => Used?.Invoke();
    }
}
