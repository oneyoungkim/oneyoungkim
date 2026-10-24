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
        Vector3 angVel;              // 도/초(x: 앞뒤, z: 좌우)
        Vector3 ang;
        public bool Returning { get; private set; }

        void Awake() { if (Target == null) Target = GetComponentInChildren<Fighter>(true); }
        public float Angle => ang.magnitude;

        public void Hit(Vector3 dir, float power)
        {
            var d = new Vector3(dir.x, 0f, dir.z).normalized;
            float k = 22f * Mathf.Clamp(power, 0.5f, 5f);
            // 방향 d 로 밀림: x 축 회전은 z 방향 이동, z 축 회전은 −x 방향 이동
            angVel += new Vector3(d.z * k, 0f, -d.x * k);
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
            ang = Vector3.ClampMagnitude(ang, 35f);
            transform.localRotation = Quaternion.Euler(ang.x, 0f, ang.z);
            // 되돌아오는 중 = 각이 줄어드는 중이고 속도가 있음
            Returning = ang.magnitude < prev.magnitude && angVel.magnitude > 8f && ang.magnitude > 2f;
        }
    }
}
