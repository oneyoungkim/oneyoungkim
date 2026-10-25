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
    public enum CamPreset { LowAngle, Close, Wide, LowFront, BackTrack, SideTrack, Mid }

    public static class CutCams
    {
        public const int LivePriority = 60;
        static readonly List<CutCamDriver> live = new List<CutCamDriver>();

        public static float Fov(CamPreset p) => p switch
        {
            CamPreset.LowAngle => 74f, CamPreset.Close => 24f, CamPreset.Wide => 45f, CamPreset.LowFront => 40f, CamPreset.BackTrack => 45f, CamPreset.Mid => 30f, _ => 40f,
        };

        public static string Name(CamPreset p) => p switch
        {
            CamPreset.LowAngle => "로우앵글", CamPreset.Close => "근접", CamPreset.Wide => "와이드", CamPreset.LowFront => "낮은 정면", CamPreset.BackTrack => "등 뒤 트래킹", CamPreset.Mid => "미디엄", _ => "측면 트래킹",
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

        /// 대상 발·키·앞·머리. 사람 모델이면 머리 뼈 기준(앉은 배우는 스킨 경계가 선 자세 그대로라 경계 높이로 재면 머리 위를 찍음 — 2026-10-25 1화)
        static void Measure(Transform t, out Vector3 feet, out float h, out Vector3 fwd, out Vector3 head)
        {
            feet = t.position; h = 1.74f; fwd = t.forward;
            var an = t.GetComponentInChildren<Animator>();
            if (an != null && an.isHuman) fwd = an.transform.forward;
            fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
            head = feet + Vector3.up * (h * 0.89f);
            var rs = t.GetComponentsInChildren<Renderer>();
            bool any = false; Bounds b = default;
            foreach (var r in rs)
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) { feet = new Vector3(t.position.x, b.min.y, t.position.z); h = Mathf.Max(0.5f, b.size.y); head = feet + Vector3.up * (h * 0.89f); }
            var hb = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            if (hb != null)
            {
                head = hb.position + Vector3.up * 0.07f;
                h = Mathf.Max(0.5f, head.y - feet.y + 0.1f);
            }
        }

        void Want(out Vector3 pos, out Vector3 look, out Vector3 origin)
        {
            pos = transform.position; look = pos + transform.forward; origin = look;
            if (Subject == null) return;
            Measure(Subject, out var feet, out float h, out var fwd, out var head);
            origin = head;      // 벽 검사는 머리에서(교탁·책상 너머로 — 허리 높이에서 쏘면 교탁에 막혀 카메라가 배우 코앞으로 옴)
            var right = Vector3.Cross(Vector3.up, fwd);
            switch (Preset)
            {
                case CamPreset.LowAngle:
                    pos = feet + fwd * 3.0f + right * 0.4f + Vector3.up * 0.6f;
                    look = head;
                    break;
                case CamPreset.Close:
                    // 눈높이보다 조금 위(앉은 배우 앞 책상·식탁 위로 — 벽 검사가 책상 상판에 걸려 코앞으로 당겨지던 것)
                    pos = head + fwd * 1.25f + right * 0.12f + Vector3.up * 0.12f;
                    look = head - Vector3.up * 0.03f;
                    break;
                case CamPreset.Wide:
                    // 머리들 위(2.5m)에서 내려다봄 — 앉은 반 친구 머리 속에 카메라가 박히지 않게(1화 교실)
                    pos = feet + fwd * 9.0f + right * 2.0f + Vector3.up * 2.5f;
                    look = Vector3.Lerp(feet, head, 0.6f);
                    break;
                case CamPreset.Mid:
                    {
                        // 미디엄(머리~허리, 손짓이 보이게): 둘째 대상이 있으면 그쪽에서(그 사람 눈높이 쪽 시선 — 1-6 태오 엄지 척)
                        var dir = fwd;
                        if (Other != null)
                        {
                            var o = Other.position - feet; o.y = 0f;
                            if (o.sqrMagnitude > 0.25f && o.sqrMagnitude < 400f) dir = o.normalized;
                        }
                        var side = Vector3.Cross(Vector3.up, dir);
                        var chest = Vector3.Lerp(feet, head, 0.78f);
                        pos = chest + dir * 2.1f + side * 0.3f + Vector3.up * 0.12f;
                        look = chest + Vector3.up * 0.05f;
                        break;
                    }
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
            if (Physics.SphereCast(look, 0.08f, d / len, out var hit, len, Block, QueryTriggerInteraction.Ignore))
                return look + d / len * Mathf.Max(0.3f, hit.distance - 0.05f);
            return pos;
        }

        // ───────── 다른 배우 피하기(1화 교실: 근접·와이드가 앞줄 친구 머리·몸 속에 박히던 것): 대상 머리 둘레로 돌려 빈 각을 찾고 그 각을 고정
        readonly List<Animator> others = new List<Animator>();
        float spin;
        static readonly HumanBodyBones[] BodyBones = { HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Hips };
        static readonly float[] Spins = { 0f, 25f, -25f, 45f, -45f, 70f, -70f, 100f, -100f };

        void Gather()
        {
            others.Clear();
            if (Subject == null) return;
            foreach (var a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                if (a.isHuman && a.isActiveAndEnabled && !a.transform.IsChildOf(Subject) && !Subject.IsChildOf(a.transform)) others.Add(a);
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        /// 카메라 → 보는 점(85%까지) 선분이 다른 배우 머리(0.2m)·가슴·골반(0.28m)을 지나는가
        public bool Blocked(Vector3 pos, Vector3 look)
        {
            var end = Vector3.Lerp(pos, look, 0.85f);
            foreach (var a in others)
            {
                if (a == null || !a.isActiveAndEnabled) continue;
                foreach (var bb in BodyBones)
                {
                    var t = a.GetBoneTransform(bb);
                    if (t != null && SegDist(t.position, pos, end) < (bb == HumanBodyBones.Head ? 0.2f : 0.28f)) return true;
                }
            }
            return false;
        }

        static Vector3 Spin(Vector3 p, Vector3 l, float deg) => deg == 0f ? p : l + Quaternion.Euler(0f, deg, 0f) * (p - l);

        public float SpinDeg => spin;

        public void Snap()
        {
            Gather();
            Want(out var p0, out var l, out var o);
            bool track = Preset == CamPreset.BackTrack || Preset == CamPreset.SideTrack;
            spin = 0f;
            var p = Clear(o, p0);
            if (!track && others.Count > 0)
                foreach (var d in Spins)
                {
                    var c = Clear(o, Spin(p0, l, d));
                    if (!Blocked(c, l)) { spin = d; p = c; break; }
                }
            transform.SetPositionAndRotation(p, Quaternion.LookRotation((l - p).sqrMagnitude > 1e-6f ? l - p : Vector3.forward));
            if (Cam != null) Cam.PreviousStateIsValid = false;
        }

        void LateUpdate()
        {
            Want(out var p, out var l, out var o);
            p = Clear(o, Spin(p, l, spin));
            bool track = Preset == CamPreset.BackTrack || Preset == CamPreset.SideTrack;
            float k = track ? 1f - Mathf.Exp(-Follow * UiKit.RealDt) : 1f;
            var np = Vector3.Lerp(transform.position, p, k);
            transform.SetPositionAndRotation(np, Quaternion.LookRotation((l - np).sqrMagnitude > 1e-6f ? l - np : transform.forward));
        }
    }
}
