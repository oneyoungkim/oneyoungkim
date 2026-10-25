// 행인1의 메인이벤트 — 의상 전환(docs/09_M3_버티컬슬라이스_설계.md 4-2 #9, 7단계)
// 플레이어 Visual 아래 정식 모델 두 벌(교복 = Siwoo_Model · 배달복 = SiwooDelivery_Model — 후드 + 주황 배달 조끼) 중 하나만 켠다.
// 모델마다 Animator(Siwoo.controller — Humanoid 라 두 아바타 모두 같은 클립)·LocoAnim·FighterAnim·HandShape·HandPoser·FaceRig 를 따로 갖고 있어서
// 켜진 쪽만 돈다(꺼진 쪽은 OnDisable 로 구독을 푼다). 전투 몸(HitReact)이 흔드는 모델도 켜진 쪽으로 바꿔 끼운다.
// 이야기 장면 데이터의 "outfit" 칸(새벽 배달 1-1~1-4 · 현장 4-1~4-3 = 배달복)을 StoryRunner 가 장면 시작에 넣는다.
using System;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class Outfit : MonoBehaviour
    {
        public const string Uniform = "교복", Delivery = "배달복";

        public GameObject[] Models = new GameObject[0];
        public string[] Names = new string[0];

        int current = -1;
        public int Current => current >= 0 ? current : Find();
        public string CurrentName => Current >= 0 && Current < Names.Length ? Names[Current] : "";
        public GameObject Model => Current >= 0 && Current < Models.Length ? Models[Current] : null;
        public Animator Anim => Model != null ? Model.GetComponent<Animator>() : null;
        /// 바꾼 횟수(테스트)
        public int Switches { get; private set; }
        public event Action<string> Changed;

        int Find()
        {
            for (int i = 0; i < Models.Length; i++) if (Models[i] != null && Models[i].activeSelf) return current = i;
            return -1;
        }

        public bool Set(string name)
        {
            int i = Array.IndexOf(Names, string.IsNullOrEmpty(name) ? Uniform : name);
            if (i < 0) return false;
            Set(i);
            return true;
        }

        public void Set(int i)
        {
            if (i < 0 || i >= Models.Length || Models[i] == null) return;
            int was = Current;
            // 자세를 이어받는다(바꾸는 순간 대기 자세로 튀지 않게 — 같은 컨트롤러라 상태 이름이 같다)
            var from = was >= 0 && was < Models.Length && Models[was] != null ? Models[was].GetComponent<Animator>() : null;
            int hash = 0; float nt = 0f;
            if (from != null && from.isActiveAndEnabled && from.runtimeAnimatorController != null)
            {
                var st = from.GetCurrentAnimatorStateInfo(0);
                hash = st.fullPathHash; nt = st.normalizedTime;
            }
            for (int k = 0; k < Models.Length; k++) if (Models[k] != null) Models[k].SetActive(k == i);
            current = i;
            var to = Models[i].GetComponent<Animator>();
            if (to != null && hash != 0 && to.HasState(0, hash)) to.Play(hash, 0, nt);
            var f = GetComponent<Fighter>();
            if (f != null && f.React != null) f.React.Setup(Models[i].transform, f);
            if (i != was) { Switches++; Changed?.Invoke(Names[i]); }
        }
    }
}
