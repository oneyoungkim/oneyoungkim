// 행인1의 메인이벤트 — 락온 (docs/08_M2_전투_설계.md 2-3)
// 켤 때: 반경 10m, 카메라 정면 ±70°, 시우와 대상 사이에 Wall 없는 적 중 화면 가운데에 가장 가까운 것(없으면 카메라 등 뒤 정렬).
// 대상 전환: 오른스틱을 중립(< 0.3)에서 0.15초 안에 0.75 넘게 튕기면 그 화면 방향(±60°)의 가장 가까운 적 / 휠·Tab = 화면 왼→오 순서.
// 풀림: 대상 탈락 → 0.4초 뒤 다음 적(예고 중 우선, 없으면 가장 가까운 적), 12m 넘게 멀어짐 · Wall 뒤 1.0초 넘게 → 풀림. 다운은 유지.
// 전투 달리기 중엔 잠깐 내려 두고(Suspended) 놓으면 같은 대상으로(탈락했으면 새로 고름).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-44), DisallowMultipleComponent]
    public sealed class LockOn : MonoBehaviour
    {
        public Fighter Me;
        public CombatTuning Tuning;
        [Tooltip("화면 계산 카메라(없으면 Camera.main)")] public Camera View;
        public CombatTuning T => Tuning != null ? Tuning : CombatTuning.Default;

        Fighter target, held;
        bool suspended;
        float hiddenFor;
        double nextAt = -1;
        double neutralAt = -1e9;
        bool armed = true;

        /// 지금 대상(달리는 동안은 null)
        public Fighter Target => suspended ? null : target;
        /// 달리기 때문에 내려 둔 대상
        public Fighter HeldTarget => suspended ? target : null;
        public bool Suspended => suspended;
        public event Action<Fighter> Changed;
        /// 켰는데 대상이 없음 → 카메라 등 뒤 정렬
        public event Action NoTarget;
        public int Switches { get; private set; }

        Camera Cam => View != null ? View : Camera.main;

        void Awake()
        {
            if (Me == null) Me = GetComponent<Fighter>();
        }

        public void Toggle()
        {
            if (target != null) { Unlock(); return; }
            var t = Pick();
            if (t != null) Set(t);
            else NoTarget?.Invoke();
        }

        public void Unlock()
        {
            if (target == null && !suspended) return;
            target = null;
            suspended = false;
            nextAt = -1;
            Changed?.Invoke(null);
        }

        public void Set(Fighter f)
        {
            if (f == target) return;
            target = f;
            hiddenFor = 0f;
            nextAt = -1;
            Changed?.Invoke(f);
        }

        /// 전투 달리기: on = 내려 두기, off = 같은 대상으로(없으면 새로)
        public void Suspend(bool on)
        {
            if (on == suspended) return;
            if (on) { if (target == null) return; suspended = true; Changed?.Invoke(null); return; }
            suspended = false;
            if (target == null || target.KO || !target.isActiveAndEnabled) { target = null; var t = Pick(); if (t != null) Set(t); else Changed?.Invoke(null); }
            else Changed?.Invoke(target);
        }

        IEnumerable<Fighter> Enemies()
        {
            foreach (var f in Fighter.All)
                if (f != null && f != Me && Me != null && f.Team != Me.Team && f.isActiveAndEnabled && !f.KO && f.State != Fighter.Phase.Out) yield return f;
        }

        bool Blocked(Fighter f)
        {
            var a = Me.Chest; var b = f.Chest;
            return Physics.Linecast(a, b, 1 << Layers.Wall, QueryTriggerInteraction.Ignore);
        }

        /// 켤 때 대상(2-3)
        public Fighter Pick()
        {
            var cam = Cam;
            if (Me == null) return null;
            var fwd = cam != null ? HitResolver.Flat(cam.transform.forward) : Me.Forward;
            Fighter best = null;
            float bestD = float.MaxValue;
            foreach (var f in Enemies())
            {
                var d = HitResolver.Flat(f.Position - Me.Position);
                if (d.magnitude > T.LockRadius) continue;
                if (Vector3.Angle(fwd, d) > T.LockAngle) continue;
                if (Blocked(f)) continue;
                float score;
                if (cam != null)
                {
                    var v = cam.WorldToViewportPoint(f.Chest);
                    score = v.z > 0f ? new Vector2(v.x - 0.5f, v.y - 0.5f).magnitude : 9f + Vector3.Angle(fwd, d);
                }
                else score = Vector3.Angle(fwd, d);
                if (score < bestD) { bestD = score; best = f; }
            }
            return best;
        }

        /// 휠·Tab: 화면 왼→오 순서로 다음(+1)/이전(−1)
        public void Cycle(int dir)
        {
            if (target == null) { Toggle(); return; }
            var cam = Cam;
            var list = new List<Fighter>(Enemies());
            if (list.Count < 2) return;
            list.Sort((a, b) => ScreenX(cam, a).CompareTo(ScreenX(cam, b)));
            int i = list.IndexOf(target);
            int n = (i + (dir >= 0 ? 1 : -1) + list.Count) % list.Count;
            if (list[n] != target) { Set(list[n]); Switches++; }
        }

        float ScreenX(Camera cam, Fighter f)
        {
            if (cam == null) return Vector3.Dot(f.Position - Me.Position, Quaternion.Euler(0f, Me.Yaw, 0f) * Vector3.right);
            var v = cam.WorldToViewportPoint(f.Chest);
            return v.z > 0f ? v.x : (v.x < 0.5f ? 2f - v.x : -1f - v.x);
        }

        /// 오른스틱 튕기기(매 프레임 넣는다)
        public void Flick(Vector2 s)
        {
            var t = T;
            double now = TimeFx.Real;
            float m = s.magnitude;
            if (m < t.FlickNeutral) { neutralAt = now; armed = true; return; }
            if (!armed || m < t.FlickThresh) return;
            if (now - neutralAt > t.FlickTime + 1e-6) { armed = false; return; }
            armed = false;
            if (target == null) return;
            var cam = Cam;
            var from = Screen(cam, target);
            Fighter best = null;
            float bestD = float.MaxValue;
            var want = s.normalized;
            foreach (var f in Enemies())
            {
                if (f == target) continue;
                var d = Screen(cam, f) - from;
                if (d.sqrMagnitude < 1e-6f) continue;
                if (Vector2.Angle(want, d) > t.FlickAngle) continue;
                float dist = HitResolver.Flat(f.Position - Me.Position).magnitude;
                if (dist < bestD) { bestD = dist; best = f; }
            }
            if (best != null) { Set(best); Switches++; }
        }

        Vector2 Screen(Camera cam, Fighter f)
        {
            if (cam == null)
            {
                var rel = Quaternion.Euler(0f, -Me.Yaw, 0f) * (f.Position - Me.Position);
                return new Vector2(rel.x, rel.z * 0.2f);
            }
            var v = cam.WorldToViewportPoint(f.Chest);
            if (v.z < 0f) { v.x = 1f - v.x; v.y = 1f - v.y; }
            return new Vector2(v.x * cam.aspect, v.y);
        }

        void Update()
        {
            if (target == null || Me == null) return;
            var t = T;
            float dt = TimeFx.Dt;
            double now = TimeFx.Real;
            // 탈락·사라짐 → 0.4초 뒤 다음 적
            if (target.KO || target.State == Fighter.Phase.Out || target.Leaving || !target.isActiveAndEnabled)
            {
                if (nextAt < 0) nextAt = now + t.LockNextDelay;
                if (now + 1e-6 >= nextAt) AutoNext();
                return;
            }
            if (suspended) return;
            if (HitResolver.Flat(target.Position - Me.Position).magnitude > t.LockLoseDist) { Unlock(); return; }
            hiddenFor = Blocked(target) ? hiddenFor + dt : 0f;
            if (hiddenFor > t.LockHideTime) Unlock();
        }

        void AutoNext()
        {
            Fighter best = null;
            float bestD = float.MaxValue;
            foreach (var f in Enemies())
            {
                if (f == target || !f.Targetable && !f.Down) continue;
                float d = HitResolver.Flat(f.Position - Me.Position).magnitude - (f.Telegraphing ? 1000f : 0f);
                if (d < bestD) { bestD = d; best = f; }
            }
            target = null;
            nextAt = -1;
            if (best != null) Set(best);
            else { suspended = false; Changed?.Invoke(null); }
        }
    }
}
