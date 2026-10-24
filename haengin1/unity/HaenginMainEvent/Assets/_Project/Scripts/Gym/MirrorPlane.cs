// 행인1의 메인이벤트 — 거울 평면 반사(docs/12_체육관_설계.md 1장 '거울 벽': 평면 반사 1장, 드릴 중에만 켬 — 꺼져 있을 땐 M_Mirror 판)
// 반사 카메라를 거울면 기준으로 비친 자리에 두고, 거울 사각형 네 모서리에 맞춘 비대칭 절두체(일반화 투영, Kooima)로 512×256 에 그려 거울 판 UV 에 그대로 입힌다.
// near = 거울면 거리라 벽 뒤는 잘린다. 좌우 뒤집힘은 기저(vr = −(vu × n))에 들어 있어 타일 −1 로 되돌려 붙인다.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class MirrorPlane : MonoBehaviour
    {
        public Renderer Panel;
        public Texture Fallback;
        public Vector3 Normal = Vector3.forward;
        public float HalfWidth = 4.3f, HalfHeight = 1.0f;
        public int Width = 512, Height = 256;
        public bool Active { get; private set; }
        public int Renders { get; private set; }
        Camera cam; RenderTexture rt; Material mat;

        void Awake()
        {
            if (Panel == null) Panel = GetComponent<Renderer>();
            mat = Panel != null ? Panel.material : null;
            var go = new GameObject("MirrorCam");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.74f, 0.76f);
            cam.cullingMask = ~(1 << 5);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            cam.allowMSAA = false; cam.allowHDR = false; cam.useOcclusionCulling = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false; data.renderPostProcessing = false; data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            rt = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = "MirrorRT" };
            rt.Create();
            cam.targetTexture = rt;
            Set(false);
        }

        public void Set(bool on)
        {
            Active = on;
            if (mat == null) return;
            foreach (var id in new[] { "_BaseMap", "_MainTex" })
            {
                if (!mat.HasProperty(id)) continue;
                mat.SetTexture(id, on ? rt : Fallback);
                mat.SetTextureScale(id, on ? new Vector2(-1f, 1f) : Vector2.one);
                mat.SetTextureOffset(id, on ? new Vector2(1f, 0f) : Vector2.zero);
            }
        }

        void LateUpdate()
        {
            if (!Active || cam == null) return;
            var main = Camera.main;
            if (main == null) return;
            Render(main);
        }

        public void Render(Camera main)
        {
            var n = transform.TransformDirection(Normal).normalized;
            var c = transform.position;
            var vu = transform.up.normalized;
            var vr = -Vector3.Cross(vu, n);                       // Cross(vr, vu) = −n(눈 쪽)
            var pe = main.transform.position;
            var pr = pe - 2f * Vector3.Dot(pe - c, n) * n;        // 비친 눈
            var pa = c - vr * HalfWidth - vu * HalfHeight;
            var pb = c + vr * HalfWidth - vu * HalfHeight;
            var pc = c - vr * HalfWidth + vu * HalfHeight;
            var vn = Vector3.Cross(vr, vu);
            var va = pa - pr; var vb = pb - pr; var vc = pc - pr;
            float d = -Vector3.Dot(va, vn);
            if (d < 0.02f) return;                                 // 눈이 거울 뒤
            float near = d, far = cam.farClipPlane;
            float l = Vector3.Dot(vr, va), r = Vector3.Dot(vr, vb), b = Vector3.Dot(vu, va), t = Vector3.Dot(vu, vc);
            var M = Matrix4x4.identity;
            M.SetRow(0, new Vector4(vr.x, vr.y, vr.z, 0f));
            M.SetRow(1, new Vector4(vu.x, vu.y, vu.z, 0f));
            M.SetRow(2, new Vector4(vn.x, vn.y, vn.z, 0f));
            cam.transform.position = pr;
            cam.transform.rotation = Quaternion.LookRotation(-vn, vu);
            cam.worldToCameraMatrix = M * Matrix4x4.Translate(-pr);
            cam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near, far);
            bool inv = GL.invertCulling;
            GL.invertCulling = !inv;
            cam.Render();
            GL.invertCulling = inv;
            Renders++;
        }

        void OnDestroy() { if (rt != null) { rt.Release(); Destroy(rt); } }
    }
}
