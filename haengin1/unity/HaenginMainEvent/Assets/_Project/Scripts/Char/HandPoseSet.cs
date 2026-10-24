// 행인1의 메인이벤트 — 손 자세 8종 값(인물·손마다 맞춘 것). FormalSetup 이 HandPoseLib 기본값에서 시작해 관통 검사(HandCheck)로 줄여 저장한다.
using System;
using UnityEngine;

namespace Haengin
{
    /// 손가락 하나: 마디 1·2·3 굽힘(도) + 1마디 벌림(도, + = 가운뎃손가락에서 멀어짐 · 가운뎃손가락은 + = 약지 쪽 · 엄지는 + = 검지에서 멀어짐)
    [Serializable]
    public struct FingerPose
    {
        public float A, B, C, Spread;
        public FingerPose(float a, float b, float c, float spread = 0f) { A = a; B = b; C = c; Spread = spread; }
        public static FingerPose Lerp(FingerPose x, FingerPose y, float t) =>
            new FingerPose(Mathf.Lerp(x.A, y.A, t), Mathf.Lerp(x.B, y.B, t), Mathf.Lerp(x.C, y.C, t), Mathf.Lerp(x.Spread, y.Spread, t));
        public float this[int j] => j == 0 ? A : j == 1 ? B : C;
    }

    [Serializable]
    public sealed class HandPoseDef
    {
        public string Name;
        /// 0 엄지 · 1 검지 · 2 중지 · 3 약지 · 4 새끼
        public FingerPose[] F = new FingerPose[5];
        /// 엄지 맞섬(도): 엄지 1마디를 손 긴 축으로 돌려 손바닥 쪽으로
        public float ThumbOpp;

        public HandPoseDef Clone() => new HandPoseDef { Name = Name, F = (FingerPose[])F.Clone(), ThumbOpp = ThumbOpp };

        public static HandPoseDef Lerp(HandPoseDef a, HandPoseDef b, float t, HandPoseDef into = null)
        {
            var r = into ?? new HandPoseDef();
            r.Name = t >= 0.5f ? b.Name : a.Name;
            if (r.F == null || r.F.Length != 5) r.F = new FingerPose[5];
            for (int i = 0; i < 5; i++) r.F[i] = FingerPose.Lerp(a.F[i], b.F[i], t);
            r.ThumbOpp = Mathf.Lerp(a.ThumbOpp, b.ThumbOpp, t);
            return r;
        }
    }

    [CreateAssetMenu(menuName = "Haengin/손 자세 묶음")]
    public sealed class HandPoseSet : ScriptableObject
    {
        public HandPoseDef[] Left = new HandPoseDef[0], Right = new HandPoseDef[0];
        [Tooltip("맞춘 뒤 자세별 최대 관통(mm) — 왼손·오른손")] public float[] PenLeft = new float[0], PenRight = new float[0];
        [TextArea] public string Note;

        public HandPoseDef Find(bool right, string name)
        {
            var arr = right ? Right : Left;
            if (arr != null) foreach (var p in arr) if (p != null && p.Name == name) return p;
            return null;
        }
    }
}
