// 행인1의 메인이벤트 — 적·허수아비 몸 (docs/08_M2_전투_설계.md 4-1: EnemyMotor = M1 모터의 중력·접지만 가진 가벼운 판)
// CharacterController + 중력·땅 붙이기 + 걷기 속도(AI, 7단계) + 외부 이동(자석·넉백·밀기). 레이어 Fighter(12).
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(0), RequireComponent(typeof(CharacterController)), DisallowMultipleComponent]
    public sealed class FighterBody : MonoBehaviour, IBody
    {
        public float Gravity = 20f, MaxFall = 20f, GroundStick = 1f;
        [Tooltip("목표 걷기 속도(AI 가 넣음, m/s)")] public Vector3 WalkVelocity;

        CharacterController cc;
        Vector3 ext;
        float vy, yaw;
        Collider sideHit, sideHitNow;

        public bool Grounded { get; private set; }
        public Vector3 Position => transform.position;
        public float Yaw => yaw;
        public Collider SideHit => sideHit;
        public CharacterController Controller => cc != null ? cc : (cc = GetComponent<CharacterController>());

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
        }

        public static void Setup(CharacterController c, float height, float radius)
        {
            c.height = height;
            c.radius = radius;
            c.skinWidth = 0.025f;
            c.center = new Vector3(0f, height * 0.5f + c.skinWidth, 0f);
            c.stepOffset = 0.3f;
            c.slopeLimit = 40f;
            c.minMoveDistance = 0f;
        }

        public void SetYaw(float y)
        {
            yaw = Mathf.DeltaAngle(0f, y);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Push(Vector3 delta) => ext += new Vector3(delta.x, 0f, delta.z);

        public void Halt() => WalkVelocity = Vector3.zero;

        public void Place(Vector3 feet, float y)
        {
            bool was = Controller.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, y, 0f));
            cc.enabled = was;
            yaw = Mathf.DeltaAngle(0f, y);
            ext = Vector3.zero;
            vy = 0f;
        }

        void Update()
        {
            float dt = TimeFx.Dt;
            if (dt <= 0f || !Controller.enabled) return;
            vy = Grounded ? -GroundStick : Mathf.Max(vy - Gravity * dt, -MaxFall);
            var move = (WalkVelocity + Vector3.up * vy) * dt + ext;
            ext = Vector3.zero;
            sideHitNow = null;
            var flags = cc.Move(move);
            sideHit = sideHitNow;
            Grounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if (Grounded) vy = 0f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.3f) sideHitNow = hit.collider;
        }
    }
}
