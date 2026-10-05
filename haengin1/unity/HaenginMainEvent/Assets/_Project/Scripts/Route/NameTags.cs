// 행인1의 메인이벤트 — 한글 이름표(랜드마크·체크포인트·성곽) 빌보드
// 이름표는 늘 카메라를 보고(화면과 나란히), 일정 거리 밖에서는 숨기며(끝 20% 에서 옅어짐), 거리만큼 키우고 줄여 화면 크기를 거의 일정하게 둔다.
// 글자는 깊이 검사 없이 맨 위에 그린다(벽에 반쯤 묻혀 잘리지 않게). 대신
//   ① 카메라 → 이름표 자리 사이를 땅·벽이 막으면 숨기고(Always = 체크포인트는 예외: 건물 너머 목표도 보여 줌)
//   ② 화면에서 겹치면 중요한 것(Priority 작은 것)·가까운 것만 남긴다 — 상가 앞처럼 이름표가 몰린 곳에서 글이 포개지지 않게.
//   ③ (2026-10-06, 3차 검수) 위 가운데 길잡이 판(RouteHud)과 겹치지 않게: 지금 목표 이름표(Always)는 화면 안으로 당기고 판과 겹치면 판 밑으로 내린다.
//      나머지 이름표는 판과 겹치면 숨긴다. 옮긴 자리는 매 프레임 원래 자리에서 다시 계산한다(이름표 오브젝트는 그대로).
// 목록은 RouteSetup(에디터)이 장면을 만들 때 채운다. 체크포인트 이름표를 켜고 끄는 일은 RouteGuide 몫(여기서는 켜진 것만 다룸).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(200)]
    public sealed class NameTags : MonoBehaviour
    {
        [Serializable]
        public struct Tag
        {
            public TextMeshPro Text;
            public float MaxDist;     // 이 거리(m) 밖이면 숨김
            public int Priority;      // 작을수록 중요(겹치면 남음): 체크포인트 0 · 성곽·관문·전망 1 · 암문 2 · 정류장·표지 3 · 출입문 4 · 전단 보드 5
            public bool Always;       // 가려도 보임(지금 목표 체크포인트)
            public string Kind;       // 종류(gate·door·cp·wall …) — 정보용
        }

        public Tag[] Tags = new Tag[0];
        [Tooltip("이 거리에서 원래 크기. 거리에 비례해 키우고 줄여 화면 크기를 거의 일정하게(1080p 에서 글 높이 약 40px)")]
        public float RefDist = 14f;
        [Tooltip("최소·최대 배율(아주 가까우면 조금 커지고, 아주 멀면 조금 작아짐)")]
        public float MinScale = 0.4f, MaxScale = 3.2f;
        [Tooltip("겹침 판정 여유(픽셀)")]
        public float Margin = 6f;
        [Tooltip("비워 두면 Camera.main")]
        public Camera Cam;
        [Tooltip("길잡이 HUD(판과 겹치지 않게). 비워 두면 장면에서 찾음")]
        public RouteHud Hud;
        [Tooltip("지금 목표 이름표를 화면 가장자리·길잡이 판에서 띄우는 거리(1080p 기준 픽셀)")]
        public float EdgeMargin = 14f;

        /// 가림 검사 대상: 땅·벽·카메라 전용 벽(07 3-5)
        public static readonly int OccluderMask = Layers.Mask(Layers.Ground, Layers.Wall, Layers.CamBlock);

        public int VisibleCount { get; private set; }
        public int HiddenByOverlap { get; private set; }
        public int HiddenByOcclusion { get; private set; }

        struct Cand { public int I; public Rect R; public float D; public float A; }
        Vector3[] basePos;
        Rect[] lastRect;
        bool hudSearched;

        /// 지금 화면에서 길잡이 판 사각형(픽셀, 없으면 크기 0) — 테스트·디버그
        public Rect HudRect { get; private set; }
        /// 이번 프레임에 옮긴 지금 목표 이름표의 이동량(픽셀)
        public Vector2 TargetShift { get; private set; }

        /// 이 이름표의 이번 프레임 화면 사각형(픽셀, 여유 없이). 안 보이면 false
        public bool TryGetRect(TextMeshPro t, out Rect r)
        {
            r = default;
            if (lastRect == null || !IsShown(t)) return false;
            for (int i = 0; i < Tags.Length; i++) if (Tags[i].Text == t) { r = lastRect[i]; return r.width > 0f; }
            return false;
        }
        readonly List<Cand> cands = new List<Cand>();
        readonly List<Rect> placed = new List<Rect>();

        void LateUpdate()
        {
            var cam = Cam != null ? Cam : Camera.main;
            if (cam != null) Apply(cam);
        }

        /// 이 카메라 기준으로 방향·크기·보임을 맞춘다(배치 촬영에서도 부른다)
        public void Apply(Camera cam)
        {
            var cp = cam.transform.position;
            var rot = cam.transform.rotation;
            float pxPerM = cam.pixelHeight / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)); // 거리 1m 에서 1m 의 화면 픽셀
            if (basePos == null || basePos.Length != Tags.Length)
            {
                basePos = new Vector3[Tags.Length];
                lastRect = new Rect[Tags.Length];
                for (int i = 0; i < Tags.Length; i++) basePos[i] = Tags[i].Text != null ? Tags[i].Text.transform.position : Vector3.zero;
            }
            if (Hud == null && !hudSearched) { Hud = FindAnyObjectByType<RouteHud>(); hudSearched = true; }
            Rect hud = default;
            bool hasHud = Hud != null && Hud.TryGetPlateRect(out hud);
            float ui = cam.pixelHeight / 1080f, edge = EdgeMargin * ui;
            if (hasHud) hud = new Rect(hud.x - edge, hud.y - edge, hud.width + 2f * edge, hud.height + 2f * edge);
            HudRect = hasHud ? hud : default;
            TargetShift = Vector2.zero;
            cands.Clear();
            int occl = 0;
            for (int i = 0; i < Tags.Length; i++)
            {
                var t = Tags[i].Text;
                if (t == null) continue;
                if (!t.gameObject.activeInHierarchy) continue;
                var tr = t.transform;
                lastRect[i] = default;
                if (tr.position != basePos[i]) tr.position = basePos[i];
                var pos = tr.position;
                float d = Vector3.Distance(cp, pos);
                float max = Tags[i].MaxDist > 0f ? Tags[i].MaxDist : 40f;
                bool show = d <= max;
                if (show && !Tags[i].Always && Physics.Linecast(cp, pos + Vector3.up * 0.15f, OccluderMask, QueryTriggerInteraction.Ignore))
                { show = false; occl++; }
                var vp = cam.WorldToViewportPoint(pos);
                if (show && vp.z <= 0.05f) show = false;
                if (!show) { SetVisible(t, false); continue; }

                tr.rotation = rot;
                float s = Mathf.Clamp(d / RefDist, MinScale, MaxScale);
                if (Mathf.Abs(tr.localScale.x - s) > 0.01f) tr.localScale = new Vector3(s, s, s);
                // 화면 사각형(기준점 = 글 아래 가운데). 글 크기는 TMP 가 잰 값(아직 없으면 글자 크기로 어림)
                var b = t.textBounds;
                float w = (b.size.x > 0.01f ? b.size.x : t.text.Length * t.fontSize * 0.1f) * s;
                float h = (b.size.y > 0.01f ? b.size.y : t.fontSize * 0.12f) * s;
                float k = pxPerM / Mathf.Max(0.1f, vp.z);
                var sp = new Vector2(vp.x * cam.pixelWidth, vp.y * cam.pixelHeight);
                if (Tags[i].Always && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                {
                    // 지금 목표(기준점이 화면 안일 때만): 글이 화면 밖으로 삐져나가면 안으로, 길잡이 판과 겹치면 판 밑으로.
                    // 기준점이 화면 밖이면 옮기지 않는다(옆 멀리 있는 목표를 끌어오면 깊이가 얕아 글이 커짐 — 그때는 방향 화살표가 맡음)
                    float wpx = w * k, hpx = h * k;
                    var want = sp;
                    want.x = Mathf.Clamp(want.x, edge + wpx / 2f, cam.pixelWidth - edge - wpx / 2f);
                    if (want.y + hpx > cam.pixelHeight - edge) want.y = cam.pixelHeight - edge - hpx;
                    if (hasHud && new Rect(want.x - wpx / 2f - Margin, want.y - Margin, wpx + 2f * Margin, hpx + 2f * Margin).Overlaps(hud)) want.y = hud.yMin - hpx - Margin - 1f;
                    var shift = want - sp;
                    if (shift.sqrMagnitude > 0.25f)
                    {
                        tr.position = pos + (rot * Vector3.right) * (shift.x / k) + (rot * Vector3.up) * (shift.y / k);
                        sp = want;
                        TargetShift = shift;
                    }
                }
                cands.Add(new Cand
                {
                    I = i, D = d, A = 1f - Mathf.InverseLerp(max * 0.8f, max, d),
                    R = new Rect(sp.x - w * k / 2f - Margin, sp.y - Margin, w * k + 2f * Margin, h * k + 2f * Margin),
                });
            }
            HiddenByOcclusion = occl;

            // 겹치면 중요한 것 → 가까운 것 순으로 남긴다
            cands.Sort((a, c) =>
            {
                int p = Tags[a.I].Priority.CompareTo(Tags[c.I].Priority);
                return p != 0 ? p : a.D.CompareTo(c.D);
            });
            placed.Clear();
            if (hasHud) placed.Add(hud);   // 길잡이 판 자리를 먼저 차지 — 겹치는 다른 이름표는 숨김(지금 목표는 위에서 이미 비켜 둠)
            int vis = 0, over = 0;
            foreach (var c in cands)
            {
                var t = Tags[c.I].Text;
                bool ok = true;
                foreach (var r in placed) if (r.Overlaps(c.R)) { ok = false; break; }
                if (!ok) { SetVisible(t, false); over++; continue; }
                placed.Add(c.R);
                lastRect[c.I] = new Rect(c.R.x + Margin, c.R.y + Margin, c.R.width - 2f * Margin, c.R.height - 2f * Margin);
                SetVisible(t, true);
                if (Mathf.Abs(t.alpha - c.A) > 0.03f || (c.A >= 1f && t.alpha < 1f)) t.alpha = c.A;
                vis++;
            }
            VisibleCount = vis;
            HiddenByOverlap = over;
        }

        static void SetVisible(TextMeshPro t, bool on)
        {
            if (t.renderer.enabled != on) t.renderer.enabled = on;
        }

        public void HideAll()
        {
            foreach (var t in Tags) if (t.Text != null) t.Text.renderer.enabled = false;
        }

        /// 이름으로 찾기(테스트·디버그) — 이름표 글 첫 줄
        public TextMeshPro Find(string firstLine)
        {
            foreach (var t in Tags)
                if (t.Text != null && t.Text.text.Split('\n')[0].Trim() == firstLine) return t.Text;
            return null;
        }

        /// 지금 화면에 보이는 이름표인지(테스트·디버그)
        public static bool IsShown(TextMeshPro t) => t != null && t.gameObject.activeInHierarchy && t.renderer.enabled;
    }
}
