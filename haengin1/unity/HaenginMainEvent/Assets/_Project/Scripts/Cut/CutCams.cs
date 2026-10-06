// 행인1의 메인이벤트 — 컷신 카메라 프리셋 6종(docs/09_M3_버티컬슬라이스_설계.md 2-2, FUJIMOTO 8-7)
// ① 로우앵글(높이 0.6m, 위로 15~25°, 세로 FOV 74° ≈ 24mm) ② 근접(세로 FOV 16° ≈ 85mm, 얼굴·눈·손) ③ 와이드(인물 화면 높이 ≤ 20%)
// ④ 낮은 고정 정면(두 사람 나란히) ⑤ 등 뒤 트래킹 ⑥ 측면 트래킹(1화 오르막·4화 청소차 — 태오 루트와 같은 숏)
// 카메라 = CinemachineCamera(우선순위 60, 몸·조준 부품 없이 CutCamDriver 가 자리를 매 프레임 계산 — 결정적).
// 대상 키 = 대상 렌더러 경계 높이(캡슐·모델 모두). 프리팹(Prefabs/Cam/CP_*.prefab)은 CutSetup 이 이 공장으로 만든다(손으로 고칠 때 참고용).
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    public enum CamPreset { LowAngle, Close, Wide, LowFront, BackTrack, SideTrack }

    public static class CutCams
    {
        public const int LivePriority = 60;
        static readonly List<CutCamDriver> live = new List<CutCamDriver>();

        public static float Fov(CamPreset p) => p switch
        {
            CamPreset.LowAngle => 74f, CamPreset.Close => 16f, CamPreset.Wide => 45f, CamPreset.LowFront => 40f, CamPreset.BackTrack => 45f, _ => 40f,
        };

        public static string Name(CamPreset p) => p switch
        {
            CamPreset.LowAngle => "로우앵글", CamPreset.Close => "근접", CamPreset.Wide => "와이드", CamPreset.LowFront => "낮은 정면", CamPreset.BackTrack => "등 뒤 트래킹", _ => "측면 트래킹",
        };

        /// 프리셋 카메라를 만든다(꺼진 우선순위 0). subject = 주 대상, other = 둘째 대상(낮은 정면)
        public static CutCamDriver Make(CamPreset p, Transform subject, Transform other = null)
        {
            var go = new GameObject("CutCam_" + p);
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Priority = 0;
            var lens = LensSettings.Default;
            lens.FieldOfView = Fov(p);
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 500f;
            cam.Lens = lens;
            var d = go.AddComponent<CutCamDriver>();
            d.Preset = p; d.Subject = subject; d.Other = other; d.Cam = cam;
            d.Snap();
            live.Add(d);
            return d;
        }

        public static void Release(CutCamDriver d)
        {
            if (d == null) return;
            live.Remove(d);
            Object.Destroy(d.gameObject);
        }

        public static void ReleaseAll()
        {
            foreach (var d in live) if (d != null) Object.Destroy(d.gameObject);
            live.Clear();
        }

        public static int LiveCount => live.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { live.Clear(); }
    }

    [DisallowMultipleComponent]
    public sealed class CutCamDriver : MonoBehaviour
    {
        public CamPreset Preset;
        public Transform Subject, Other;
        public CinemachineCamera Cam;
        /// 트래킹 따라가는 빠르기(1/초)
        public float Follow = 6f;

        public void SetLive(bool on) { if (Cam != null) Cam.Priority = on ? CutCams.LivePriority : 0; }

        static void Measure(Transform t, out Vector3 feet, out float h, out Vector3 fwd)
        {
            feet = t.position; h = 1.74f; fwd = t.forward;
            fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            feet = new Vector3(t.position.x, b.min.y, t.position.z);
            h = Mathf.Max(0.5f, b.size.y);
        }

        void Want(out Vector3 pos, out Vector3 look)
        {
            pos = transform.position; look = pos + transform.forward;
            if (Subject == null) return;
            Measure(Subject, out var feet, out float h, out var fwd);
            var right = Vector3.Cross(Vector3.up, fwd);
            var head = feet + Vector3.up * (h * 0.89f);
            switch (Preset)
            {
                case CamPreset.LowAngle:
                    pos = feet + fwd * 3.0f + right * 0.4f + Vector3.up * 0.6f;
                    look = head;
                    break;
                case CamPreset.Close:
                    pos = head + fwd * 1.9f + right * 0.15f;
                    look = head;
                    break;
                case CamPreset.Wide:
                    pos = feet + fwd * 10.5f + right * 2.0f + Vector3.up * 1.6f;
                    look = feet + Vector3.up * (h * 0.5f);
                    break;
                case CamPreset.LowFront:
                    {
                        var b = Other != null ? Other.position : feet + right * 1.2f;
                        var mid = (feet + new Vector3(b.x, feet.y, b.z)) * 0.5f;
                        var line = new Vector3(b.x - feet.x, 0f, b.z - feet.z);
                        var n = line.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, line.normalized) : fwd;
                        if (Vector3.Dot(n, fwd) < 0f) n = -n;
                        pos = mid + n * 4.2f + Vector3.up * 0.9f;
                        look = mid + Vector3.up * (h * 0.66f);
                        break;
                    }
                case CamPreset.BackTrack:
                    pos = feet - fwd * 3.4f + Vector3.up * 1.7f;
                    look = head + fwd * 2.0f;
                    break;
                default:   // SideTrack
                    pos = feet + right * 3.6f + Vector3.up * 1.3f;
                    look = feet + Vector3.up * (h * 0.62f) + fwd * 0.6f;
                    break;
            }
        }

        static readonly int Block = Layers.Mask(Layers.Default, Layers.Ground, Layers.Wall, Layers.CamBlock);

        /// 보는 점 → 카메라 자리 사이에 벽이 있으면 그 앞까지 당김(작은 실내 무대에서 와이드·등 뒤가 벽 밖으로 나가지 않게)
        static Vector3 Clear(Vector3 look, Vector3 pos)
        {
            var d = pos - look;
            float len = d.magnitude;
            if (len < 0.05f) return pos;
            if (Physics.SphereCast(look, 0.12f, d / len, out var hit, len, Block, QueryTriggerInteraction.Ignore))
                return look + d / len * Mathf.Max(0.3f, hit.distance - 0.05f);
            return pos;
        }

        public void Snap()
        {
            Want(out var p, out var l);
            p = Clear(l, p);
            transform.SetPositionAndRotation(p, Quaternion.LookRotation((l - p).sqrMagnitude > 1e-6f ? l - p : Vector3.forward));
            if (Cam != null) Cam.PreviousStateIsValid = false;
        }

        void LateUpdate()
        {
            Want(out var p, out var l);
            p = Clear(l, p);
            bool track = Preset == CamPreset.BackTrack || Preset == CamPreset.SideTrack;
            float k = track ? 1f - Mathf.Exp(-Follow * UiKit.RealDt) : 1f;
            var np = Vector3.Lerp(transform.position, p, k);
            transform.SetPositionAndRotation(np, Quaternion.LookRotation((l - np).sqrMagnitude > 1e-6f ? l - np : transform.forward));
        }
    }
}
