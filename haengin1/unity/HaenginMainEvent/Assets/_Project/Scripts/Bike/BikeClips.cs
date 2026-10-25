// 행인1의 메인이벤트 — 자전거 페달 클립 곡선(docs/14 6장, 2026-10-26): tools/bike_anim.py 의 bike_curves.json 을 BikeSetup 이 굽는 에셋 Settings/BikeClips.asset
//   클립마다 프레임별 크랭크 각 θ(Unity Crank.localRotation = Euler(θ,0,0)) · 기울기 Lean(LeanRoot z, + = 라이더 왼쪽) · IK 목표 17개
//   [왼발 페달 위, 오른발 페달 위, 왼 발목 xyz, 오른 발목 xyz, 왼 손목 xyz, 오른 손목 xyz, 골반 xyz] — 자전거 좌표. 페달 위 발목은 그 페달 가운데에서의 차이.
using System;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/BikeClips")]
    public sealed class BikeClips : ScriptableObject
    {
        [Serializable]
        public sealed class Track
        {
            public string Name;
            public int Frames;                     // 키 수(닫힌 루프는 끝 = 처음)
            public float Fps = 30f;
            public bool Loop;
            public float[] Theta = new float[0], Lean = new float[0], Ik = new float[0];
            public float Length => Mathf.Max(1, Frames - 1) / Fps;
        }

        public const int IkStride = 17;
        public Track Cruise = new Track(), Climb = new Track(), Coast = new Track(), StartStop = new Track();
        [Tooltip("startstop 구간(프레임): 출발 0~StartEnd · 정지 StopFrom~끝")] public int StartEnd = 45, StopFrom = 100;
        public float CrankLen = 0.16f;
        [Tooltip("내리막 관성 크랭크 각(오른발 앞 수평)")] public float CoastTheta = 115.5f;
        [Tooltip("오르막 댄싱 자전거 기울기 = Amp·sin(θ − Phase)")] public float ClimbLeanAmp = 8f, ClimbLeanPhase = 35.5f;

        public struct Pose
        {
            public bool OnL, OnR;
            public Vector3 AnkleL, AnkleR, WristL, WristR, Hips;
            public float Theta, Lean;
        }

        /// 프레임 f(소수)의 값 — 이웃 두 키를 선형 보간(θ 는 각도 보간). 루프 클립은 f 를 한 주기로 감음
        public static Pose Sample(Track t, float f)
        {
            var p = new Pose();
            if (t == null || t.Frames < 1 || t.Ik.Length < t.Frames * IkStride) return p;
            int n = t.Frames;
            if (t.Loop) f = Mathf.Repeat(f, n - 1);
            f = Mathf.Clamp(f, 0f, n - 1);
            int a = Mathf.Min((int)f, n - 1), b = Mathf.Min(a + 1, n - 1);
            float k = f - a;
            float I(int fr, int i) => t.Ik[fr * IkStride + i];
            Vector3 V(int i) => Vector3.Lerp(new Vector3(I(a, i), I(a, i + 1), I(a, i + 2)), new Vector3(I(b, i), I(b, i + 1), I(b, i + 2)), k);
            int near = k < 0.5f ? a : b;
            p.OnL = I(near, 0) > 0.5f; p.OnR = I(near, 1) > 0.5f;
            // 페달 위/땅 상태가 바뀌는 프레임 사이에선 보간이 섞인 좌표가 되므로 가까운 쪽 키 그대로
            bool stableL = (I(a, 0) > 0.5f) == (I(b, 0) > 0.5f), stableR = (I(a, 1) > 0.5f) == (I(b, 1) > 0.5f);
            p.AnkleL = stableL ? V(2) : new Vector3(I(near, 2), I(near, 3), I(near, 4));
            p.AnkleR = stableR ? V(5) : new Vector3(I(near, 5), I(near, 6), I(near, 7));
            p.WristL = V(8); p.WristR = V(11); p.Hips = V(14);
            p.Theta = t.Theta.Length > b ? Mathf.LerpAngle(t.Theta[a], t.Theta[b], k) : 0f;
            p.Lean = t.Lean.Length > b ? Mathf.Lerp(t.Lean[a], t.Lean[b], k) : 0f;
            return p;
        }
    }
}
