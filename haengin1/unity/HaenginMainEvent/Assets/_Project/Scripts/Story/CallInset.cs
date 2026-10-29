// 행인1의 메인이벤트 — 전화 통화 화면(2026-10-29): 대사 이벤트 call:배우 → 그 배우(무대 밑 '통화 자리'에 숨겨 둠)를 따로 찍어 화면 오른쪽 위 작은 창에. hangup = 닫기
//   C1-19 로터리 빵집 직원 누나(전화 너머 목소리만이던 것)
using UnityEngine;
using UnityEngine.UI;

namespace Haengin
{
    public sealed class CallInset : MonoBehaviour
    {
        public static CallInset Current { get; private set; }
        public Transform Who { get; private set; }
        public bool Open => Who != null;
        Camera cam; Light key; RenderTexture rt; GameObject ui; RawImage img; TMPro.TMP_FontAsset font;
        public const int W = 448, H = 336;

        public static CallInset Show(Transform who, string label, TMPro.TMP_FontAsset font = null)
        {
            if (Current == null) Current = new GameObject("통화 화면").AddComponent<CallInset>();
            Current.font = font;
            Current.Begin(who, label);
            return Current;
        }

        public static void Hide() { if (Current != null) Current.End(); }

        void Begin(Transform who, string label)
        {
            Who = who;
            if (rt == null) { rt = new RenderTexture(W * 2, H * 2, 24, RenderTextureFormat.ARGB32) { name = "통화 RT" }; rt.Create(); }
            if (cam == null)
            {
                cam = new GameObject("통화 카메라").AddComponent<Camera>();
                cam.transform.SetParent(transform, false);
                cam.targetTexture = rt; cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.86f, 0.82f, 0.74f);
                key = new GameObject("통화 빛").AddComponent<Light>();
                key.transform.SetParent(cam.transform, false); key.type = LightType.Point; key.range = 6f; key.intensity = 2.2f; key.color = new Color(1f, 0.93f, 0.84f);
                key.transform.localPosition = new Vector3(0.5f, 0.4f, 0f);
            }
            if (ui == null)
            {
                ui = new GameObject("통화 창", typeof(RectTransform));
                var cv = ui.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 72;
                var cs = ui.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f); cs.matchWidthOrHeight = 0.5f;
                var frame = UiKit.Panel("통화 테", ui.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f - W * 0.5f - 8f, -60f - H * 0.5f - 8f), new Vector2(W + 16f, H + 52f), UiKit.Ink);
                frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var go = new GameObject("통화 그림", typeof(RectTransform)); go.transform.SetParent(frame.transform, false);
                img = go.AddComponent<RawImage>(); img.texture = rt;
                var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(W, H); r.anchoredPosition = new Vector2(0f, -18f);
                var t = UiKit.Text("통화 이름", frame.transform, font, 24f, UiKit.Paper, TMPro.TextAlignmentOptions.Left);
                UiKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(W, 30f));
                t.name = "통화 이름";
            }
            var nameText = ui.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (nameText != null) nameText.text = "통화 중 · " + label;
            ui.SetActive(true); cam.enabled = true; key.enabled = true;
            Aim();
        }

        void Aim()
        {
            if (Who == null || cam == null) return;
            var an = Who.GetComponentInChildren<Animator>();
            var head = an != null && an.isHuman && an.GetBoneTransform(HumanBodyBones.Head) != null ? an.GetBoneTransform(HumanBodyBones.Head).position : Who.position + Vector3.up * 1.5f;
            var f = Who.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            var tgt = head - Vector3.up * 0.18f;
            var pos = tgt + Quaternion.Euler(0f, 18f, 0f) * f * 1.25f + Vector3.up * 0.04f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tgt - pos, Vector3.up));
        }

        void LateUpdate() { if (Open) Aim(); }

        void End()
        {
            Who = null;
            if (ui != null) ui.SetActive(false);
            if (cam != null) cam.enabled = false;
            if (key != null) key.enabled = false;
        }

        void OnDestroy() { if (rt != null) { rt.Release(); Destroy(rt); } if (ui != null) Destroy(ui); if (Current == this) Current = null; }
    }
}
