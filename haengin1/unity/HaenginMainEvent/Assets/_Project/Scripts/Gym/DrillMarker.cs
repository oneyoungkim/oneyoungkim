// 행인1의 메인이벤트 — 드릴 자리 마커(docs/12_체육관_설계.md 2장): 바닥 먹 원 + 가까이 가면 드릴 이름·최고 등급 뱃지(TMP) + 상호작용(× / E)
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DrillMarker : MonoBehaviour
    {
        public string DrillId = "";
        public Interactable Interact;
        public TextMeshPro Label;
        public Renderer Ring;
        public GymManager Gym;
        Transform player;
        float blink;

        void Start()
        {
            if (Interact == null) Interact = GetComponent<Interactable>();
            if (Interact != null) Interact.Used += () => Gym?.OnMarkerUsed(this);
            Refresh();
        }

        public void Refresh()
        {
            if (Label == null || Gym == null) return;
            var def = Gym.Book != null ? Gym.Book.Find(DrillId) : null;
            var ds = GymState.Current.Of(DrillId, false);
            string best = ds != null && ds.HasBest ? DrillGrader.Letter(ds.BestGrade) : "—";
            Label.text = def != null ? $"{def.NameKr}\n<size=70%>최고 {best}{(ds != null && ds.SimUnlocked ? " · 시뮬" : "")}{(def.Playable ? "" : " · 자리만")}</size>" : DrillId;
            if (Interact != null) Interact.Prompt = def != null ? $"{def.NameKr}{(def.Playable ? "" : "(자리만)")}" : DrillId;
        }

        void Update()
        {
            if (player == null) { var m = FindAnyObjectByType<PlayerMotor>(); if (m != null) player = m.transform; }
            if (Label == null) return;
            bool near = player != null && (new Vector2(player.position.x - transform.position.x, player.position.z - transform.position.z)).magnitude < 3.2f;
            bool show = near && (Gym == null || !Gym.Busy);
            if (Label.gameObject.activeSelf != show) Label.gameObject.SetActive(show);
            if (show && Camera.main != null)
            {
                var to = Camera.main.transform.position - Label.transform.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-4f) Label.transform.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
            }
            if (Ring != null) { blink += UiKit.RealDt; var c = Ring.material.color; c.a = near ? 0.85f + 0.15f * Mathf.Sin(blink * 4f) : 0.55f; Ring.material.color = c; }
        }
    }
}
