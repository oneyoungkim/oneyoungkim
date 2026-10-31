// 행인1의 메인이벤트 — 적 공격 예고 표시 (docs/08_M2_전투_설계.md 4-1·7-3 — HUD 정식판은 12단계)
// 예고가 있는 기술: 판정 max(예고, 0.2)초 전에 머리 위 '!'(0.3초) · 막기 불가는 '!!' + 몸 테두리 흰 번쩍 2번(지금은 피격 번쩍으로 대신).
// 글자는 TextMeshPro(기본 글꼴 — '!' 만 쓰므로 한글 글꼴 불필요), 카메라를 향해 돈다.
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(110), DisallowMultipleComponent]
    public sealed class TelegraphMark : MonoBehaviour
    {
        public Fighter Me;
        public float Show = 0.3f, Above = 0.35f;
        ScreenTag text;
        AttackRun shownFor;
        float left;
        int flashes;
        float flashAt;

        /// 지금 보이는 표시('!'·'!!'·없음) — 테스트용
        public string Current => text != null && text.Shown ? text.Text : "";
        public int Shown { get; private set; }

        void Awake()
        {
            if (Me == null) Me = GetComponent<Fighter>();
            text = ScreenTag.Make("공격 예고", null, 44f);      // 2026-10-31: 공중 3D 글자 대신 화면 판
            text.transform.SetParent(transform, false);
        }

        void LateUpdate()
        {
            if (Me == null || text == null) return;
            var r = Me.Run;
            if (r != null && r != shownFor && r.Warn > 0)
            {
                double at = r.Move.ActiveStart - (r.WarnLead > 0f ? r.WarnLead : Mathf.Max(r.Move.Lead, 0.2f));
                if (r.T >= at - 1e-4)
                {
                    shownFor = r;
                    left = Show;
                    text.Show(r.Warn >= 2 ? "!!" : "!", Me.Position + Vector3.up * (Me.Height + Above));
                    Shown++;
                    if (r.Move.Warn >= 2 && Me.React != null) { Me.React.Flash(); flashes = 1; flashAt = 0.15f; }
                }
            }
            if (flashes > 0)
            {
                flashAt -= TimeFx.RealDt;
                if (flashAt <= 0f && Me.React != null) { Me.React.Flash(); flashes = 0; }
            }
            if (left > 0f)
            {
                left -= TimeFx.RealDt;
                text.Follow(Me.Position + Vector3.up * (Me.Height + Above));
                if (left <= 0f) text.Hide();
            }
        }
    }
}
