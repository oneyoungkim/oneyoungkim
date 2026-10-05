// 행인1의 메인이벤트 — 한글 이름표(랜드마크·체크포인트·성곽) 빌보드
// 이름표는 늘 카메라를 보고(화면과 나란히), 일정 거리 밖에서는 숨기며(끝 20% 에서 옅어짐), 거리만큼 키우고 줄여 화면 크기를 거의 일정하게 둔다.
// 글자는 깊이 검사 없이 맨 위에 그린다(벽에 반쯤 묻혀 잘리지 않게). 대신
//   ① 카메라 → 이름표 자리 사이를 땅·벽이 막으면 숨기고(Always = 체크포인트는 예외: 건물 너머 목표도 보여 줌)
//   ② 화면에서 겹치면 중요한 것(Priority 작은 것)·가까운 것만 남긴다 — 상가 앞처럼 이름표가 몰린 곳에서 글이 포개지지 않게.
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

        /// 가림 검사 대상: 땅·벽·카메라 전용 벽(07 3-5)
        public static readonly int OccluderMask = Layers.Mask(Layers.Ground, Layers.Wall, Layers.CamBlock);

        public int VisibleCount { get; private set; }
        public int HiddenByOverlap { get; private set; }
        public int HiddenByOcclusion { get; private set; }

        struct Cand { public int I; public Rect R; public float D; public float A; }
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
            cands.Clear();
            int occl = 0;
            for (int i = 0; i < Tags.Length; i++)
            {
                var t = Tags[i].Text;
                if (t == null) continue;
                if (!t.gameObject.activeInHierarchy) continue;
                var tr = t.transform;
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
            int vis = 0, over = 0;
            foreach (var c in cands)
            {
                var t = Tags[c.I].Text;
                bool ok = true;
                foreach (var r in placed) if (r.Overlaps(c.R)) { ok = false; break; }
                if (!ok) { SetVisible(t, false); over++; continue; }
                placed.Add(c.R);
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
