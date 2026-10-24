// 행인1의 메인이벤트 — 체육관 NPC(docs/12_체육관_설계.md 3장): 이윤준·고준·관원. 시간대별 자리(소켓)로 옮겨 서서 동작 반복(기존 전투 클립 상태 이름)
// 이윤준은 스파링에 안 들어감(세게 맞을 수 없는 몸) — 미트만 잡고 케이지 문·매트 가장자리에서 큐.
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public enum GymPart { Dawn, Morning, Day, Evening, Night }

    [DisallowMultipleComponent]
    public sealed class GymNpc : MonoBehaviour
    {
        public string Id = "";
        public string DisplayName = "";
        public Animator Anim;
        /// 반복 동작: 샌드백 치기 · 섀도 · 줄넘기 · 스트레칭 · 앉기 · 팔짱 · 없음
        public enum Routine { None, Bag, Shadow, Rope, Stretch, Sit, Watch, Mitts }
        public Routine Now = Routine.None;
        public string Current { get; private set; } = "";
        float clock, next;
        int step;
        Vector3 basePos;
        float hop;

        void Awake() { if (Anim == null) Anim = GetComponentInChildren<Animator>(); }

        /// 자리로(발 위치·방향)
        public void Place(Vector3 feet, Vector3 forward, Routine r)
        {
            transform.position = feet;
            if (forward.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.z).normalized, Vector3.up);
            basePos = feet;
            Now = r; clock = 0f; next = 0.2f; step = 0;
            Play(r == Routine.Sit ? "ChairSit" : r == Routine.Stretch ? "Kneel" : "Stance", 0.05f);
        }

        public void Play(string state, float fade = 0.08f)
        {
            if (Anim == null || Anim.runtimeAnimatorController == null) return;
            int h = Animator.StringToHash(state);
            if (!Anim.HasState(0, h)) { if (state != "Stance" && Anim.HasState(0, Animator.StringToHash("Stance"))) state = "Stance"; else return; h = Animator.StringToHash(state); }
            Anim.CrossFadeInFixedTime(h, fade, 0);
            Current = state;
        }

        void Update()
        {
            if (Now == Routine.None || Now == Routine.Sit || Now == Routine.Watch || Now == Routine.Mitts) return;
            float dt = TimeFx.Dt;
            if (dt <= 0f) return;
            clock += dt;
            if (Now == Routine.Rope)
            {
                hop = Mathf.Abs(Mathf.Sin(clock * Mathf.PI * 2f)) * 0.10f;
                transform.position = basePos + Vector3.up * hop;
                if (Current != "Stance") Play("Stance");
                return;
            }
            if (clock < next) return;
            switch (Now)
            {
                case Routine.Bag:
                case Routine.Shadow:
                    {
                        string[] seq = Now == Routine.Bag ? new[] { "Jab", "Cross", "Jab", "Hook", "Stance", "Cross", "Upper", "Stance" } : new[] { "Jab", "Cross", "Stance", "Dodge", "Jab", "Jab", "Cross", "Stance" };
                        Play(seq[step % seq.Length], 0.06f);
                        next = clock + (Current == "Stance" ? 1.1f : 0.55f) + (Id.GetHashCode() & 3) * 0.05f;
                        step++;
                        break;
                    }
                case Routine.Stretch:
                    Play(step % 2 == 0 ? "Kneel" : "Stance", 0.3f);
                    next = clock + 2.5f; step++;
                    break;
            }
        }
    }
}
