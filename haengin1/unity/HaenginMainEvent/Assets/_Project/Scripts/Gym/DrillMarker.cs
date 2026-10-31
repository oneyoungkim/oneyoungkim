// 행인1의 메인이벤트 — 드릴 자리 마커(docs/12_체육관_설계.md 2장): 바닥 먹 원 + 가까이 가면 드릴 이름·최고 등급 뱃지(TMP) + 상호작용(× / E)
// QA 2차 A7(2026-10-26): 바닥·허공 글('D08 더미 테이크다운 / 최고 등급 / 시뮬')이 가까이 서면 사람 몸 앞에 크게 겹쳐 보였음 →
//   ① 둘 다 가까이 갈 때만(바닥 스티커도) ② 작게(이름표 1.5 → 1.0 · 스티커 1.1 → 0.75) · 이름표는 머리 위(1.35 → 2.05m) ③ 깊이 검사(사람 뒤로 그려짐 —
//   TMP SDF 셰이더의 ZTest 는 전역 unity_GUIZTestMode 를 따라 화면 UI 캔버스가 남긴 'Always' 로 그려지던 것 → 재질 사본에 LessEqual)
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
        /// 바닥 스티커(늘 보임, 작게)
        public TextMeshPro Sticker;
        public Renderer Ring;
        public const float ShowDist = 1.9f;
        public bool StickerShown => Sticker != null && Sticker.gameObject.activeSelf;
        public bool LabelShown => Label != null && Label.gameObject.activeSelf;
        public GymManager Gym;
        Transform player;
        float blink;

        void Start()
        {
            if (Interact == null) Interact = GetComponent<Interactable>();
            if (Interact != null) Interact.Used += () => Gym?.OnMarkerUsed(this);
            Tidy();
            Refresh();
        }

        /// QA 2차 A7: 크기·높이·깊이 검사(장면이 예전 값이어도 여기서 맞춤)
        public void Tidy()
        {
            // 2026-10-31 대표: 공중에 뜬 이름표·바닥에 뒤집혀 깔린 스티커 없앰(옛 장면이어도 끔) — 이름·최고 등급은 화면 안내로
            if (Label != null) Label.gameObject.SetActive(false);
            if (Sticker != null) Sticker.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (Gym == null) return;
            var def = Gym.Book != null ? Gym.Book.Find(DrillId) : null;
            var ds = GymState.Current.Of(DrillId, false);
            string best = ds != null && ds.HasBest ? DrillGrader.Letter(ds.BestGrade) : "—";
            if (Interact != null) Interact.Prompt = def != null ? $"{def.NameKr}  ·  최고 {best}{(ds != null && ds.SimUnlocked ? " · 시뮬" : "")}{(def.Playable ? "" : " · 자리만")}" : DrillId;
        }

        void Update()
        {
            if (player == null) { var m = FindAnyObjectByType<PlayerMotor>(); if (m != null) player = m.transform; }
            bool near = player != null && (new Vector2(player.position.x - transform.position.x, player.position.z - transform.position.z)).magnitude < ShowDist;
            if (Ring != null) { blink += UiKit.RealDt; var c = Ring.material.color; c.a = near ? 0.85f + 0.15f * Mathf.Sin(blink * 4f) : 0.55f; Ring.material.color = c; }
        }
    }
}
