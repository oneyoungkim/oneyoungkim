// 행인1의 메인이벤트 — 샌드백 흔들림(docs/12_체육관_설계.md 4-2 D05). MonoBehaviour 는 파일 이름 = 클래스 이름(GymNpc.cs 에서 떼어 냄 — 장면 참조가 깨짐)
using UnityEngine;

namespace Haengin
{
    /// 샌드백 흔들림(docs/12 4-2 D05): 보 밑 레일 피벗에서 2단 힌지 대신 한 축 진자 — 타격 방향으로 각속도, 되돌아오는 구간이면 '돌아올 때'
    public sealed class BagSwing : MonoBehaviour
    {
        public Transform Pivot;      // 체인 위(레일) — 이 오브젝트
        public Fighter Target;       // 맞는 몸(Fighter, 팀 1)
        public float Damping = 1.6f, Length = 2.1f;
        /// G8(2026-11-05): 각 한도(도) — 백 줄은 동쪽 벽에서 0.8m(백 아래 끝 2.08m · 반지름 0.2 → 16.8° 에 닿음). 예전 35° 는 벽을 뚫을 만큼
        public float MaxAngle = 15f;
        /// 타격 세기 1 당 각속도(도/초). 예전 22 — 부호를 바로잡자 0.6초마다 치는 봇에 한도(15°)에 붙어 '돌아올 때'가 줄어 흉내 계산으로 11
        public float Push = 11f;
        Vector3 angVel;              // 도/초(x: 앞뒤, z: 좌우)
        Vector3 ang;
        public bool Returning { get; private set; }

        void Awake() { if (Target == null) Target = GetComponentInChildren<Fighter>(true); }
        public float Angle => ang.magnitude;

        public void Hit(Vector3 dir, float power)
        {
            if (transform.parent != null) dir = transform.parent.InverseTransformDirection(dir);
            var d = new Vector3(dir.x, 0f, dir.z).normalized;
            float k = Push * Mathf.Clamp(power, 0.5f, 5f);
            // 방향 d 로 밀림(매달린 백 = 피벗 아래): Euler(θ,0,0) 은 아래 끝을 −z 로, Euler(0,0,θ) 는 +x 로 보낸다 → x 축 −d.z · z 축 +d.x.
            //   G8(2026-11-05): 예전 (d.z, 0, −d.x) 는 피벗 위 점 기준이라 치는 사람(시우)·카메라 쪽으로 넘어와 시우 머리·몸을 덮었음
            angVel += new Vector3(-d.z * k, 0f, d.x * k);
        }

        void Update()
        {
            float dt = TimeFx.Dt;
            if (dt <= 0f) return;
            float g = 9.81f / Mathf.Max(0.5f, Length) * Mathf.Rad2Deg;
            var acc = -ang * (g / 57.3f) - angVel * Damping;
            var prev = ang;
            angVel += acc * dt;
            ang += angVel * dt;
            if (ang.magnitude > MaxAngle)
            {
                ang = Vector3.ClampMagnitude(ang, MaxAngle);
                // 한도에 닿으면 바깥으로 가는 속도는 버림(벽·체인에 걸린 것처럼 — 붙어 있지 않고 바로 돌아옴)
                var n = ang.normalized; float outv = Vector3.Dot(angVel, n);
                if (outv > 0f) angVel -= n * outv;
            }
            transform.localRotation = Quaternion.Euler(ang.x, 0f, ang.z);
            // 되돌아오는 중 = 각이 줄어드는 중이고 속도가 있음
            Returning = ang.magnitude < prev.magnitude && angVel.magnitude > 8f && ang.magnitude > 2f;
        }
    }
}
