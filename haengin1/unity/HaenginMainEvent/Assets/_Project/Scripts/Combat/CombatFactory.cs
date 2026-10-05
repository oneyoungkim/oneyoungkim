// 행인1의 메인이벤트 — 전투 부품 조립 (docs/08_M2_전투_설계.md 6-1·10-1)
// 장면 생성(에디터 CombatSetup·M1Setup)과 PlayMode 테스트가 같은 함수로 만든다 → 테스트가 실제 장면과 같은 설정을 검사(RigFactory 와 같은 방식).
// 런타임 AddComponent 에서는 Reset() 이 불리지 않으므로 Cinemachine 필드를 전부 명시한다.
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public static class CombatFactory
    {
        public const string CombatCamName = "CM_Combat", PivotName = "CombatPivot";

        static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// 플레이어(RigFactory 리그)에 전투 부품: Fighter(시우, HP 200) + HitReact(Visual 의 모델) + LockOn + PlayerCombat. PInput 이 있으면 연결
        public static PlayerCombat AddPlayer(GameObject player, CombatTuning t, MoveSet moves)
        {
            var motor = player.GetComponent<PlayerMotor>();
            var f = Get<Fighter>(player);
            f.Team = 0;
            f.Label = "반시우";
            f.IsPlayer = true;
            f.Radius = motor != null ? motor.T.radius : 0.25f;
            f.Height = motor != null ? motor.T.height : 1.74f;
            f.ChestHeight = 1.25f;
            f.MaxHp = f.Hp = t != null ? t.PlayerHp : 200;
            f.Tuning = t;
            var visual = player.transform.Find("Visual");
            if (visual != null)
            {
                var react = Get<HitReact>(visual.gameObject);
                react.Setup(visual.childCount > 0 ? visual.GetChild(0) : visual, f);
                f.React = react;
            }
            var lockOn = Get<LockOn>(player);
            lockOn.Me = f;
            lockOn.Tuning = t;
            var pc = Get<PlayerCombat>(player);
            pc.Motor = motor;
            pc.Me = f;
            pc.Tuning = t;
            pc.Moves = moves;
            pc.Lock = lockOn;
            var input = player.GetComponent<PInput>();
            if (input != null) input.Combat = pc;
            return pc;
        }

        /// 적·허수아비: 레이어 Fighter, CharacterController + FighterBody + Fighter + HitReact(모델)
        public static Fighter AddFighter(GameObject go, string label, int team, int hp, float height, float radius, CombatTuning t, Transform model)
        {
            RigFactory.SetLayer(go, Layers.Fighter);
            var cc = Get<CharacterController>(go);
            FighterBody.Setup(cc, height, radius);
            Get<FighterBody>(go);
            var f = Get<Fighter>(go);
            f.Team = team;
            f.Label = label;
            f.MaxHp = f.Hp = hp;
            f.Height = height;
            f.Radius = radius;
            f.ChestHeight = height * 0.72f;
            f.Tuning = t;
            f.Body = go.GetComponent<FighterBody>();
            var react = Get<HitReact>(go);
            react.Setup(model != null ? model : go.transform, f);
            f.React = react;
            return f;
        }

        /// 캡슐 모양 몸(키 height, 바닥 = 발). 충돌체 없음(몸은 CharacterController)
        public static GameObject Capsule(string name, float height, float radius, Color color)
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cap.name = name;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            cap.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            // 정면 표시(코) — 젖힘·방향이 보이게
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(cap.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.6f, 0.5f);
            nose.transform.localScale = new Vector3(0.25f, 0.12f, 0.3f);
            var root = new GameObject(name + "_Model");
            cap.transform.SetParent(root.transform, false);
            var r = cap.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null && r != null)
            {
                var m = new Material(sh) { name = name + "_Mat" };
                m.SetColor("_BaseColor", color);
                r.sharedMaterial = m;
                nose.GetComponent<Renderer>().sharedMaterial = m;
            }
            return root;
        }

        /// 허수아비(맞기만 함). model 이 없으면 회색 캡슐
        public static Fighter Dummy(string label, Vector3 feet, float yaw, int hp = 999, GameObject model = null, float height = 1.80f, float radius = 0.30f, CombatTuning t = null, int team = 1)
        {
            var go = new GameObject(label);
            go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            if (model == null) model = Capsule(label, height, radius, new Color(0.55f, 0.56f, 0.58f));
            model.transform.SetParent(go.transform, false);
            var f = AddFighter(go, label, team, hp, height, radius, t, model.transform);
            f.Body.SetYaw(yaw);
            return f;
        }

        /// CM_Combat(6-1): OrbitalFollow(Sphere) + RotationComposer + Deoccluder + TraumaShake + CombatCamRig, 추적·보는 대상 = CombatPivot. 브레인 블렌드도 설정
        public static CombatCamRig BuildCombatCam(RigFactory.Rig r, PlayerCombat pc, CamTuning ct, CombatTuning t, InputActionAsset actions)
        {
            var pivot = new GameObject(PivotName).transform;
            pivot.position = r.CamTarget.position;
            var go = new GameObject(CombatCamName);
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Priority = 0;
            cam.Target = new CameraTarget { TrackingTarget = pivot, LookAtTarget = null, CustomLookAtTarget = false };
            var lens = LensSettings.Default;
            lens.FieldOfView = 47f;
            lens.NearClipPlane = ct.near;
            lens.FarClipPlane = ct.far;
            cam.Lens = lens;

            var o = go.AddComponent<CinemachineOrbitalFollow>();
            o.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            o.Radius = 5f;
            o.TargetOffset = Vector3.zero;
            o.TrackerSettings = new TrackerSettings
            {
                BindingMode = BindingMode.WorldSpace,
                PositionDamping = ct.posDamping,
                AngularDampingMode = AngularDampingMode.Euler,
                RotationDamping = Vector3.zero,
                QuaternionDamping = 0f,
            };
            o.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
            float yaw = Mathf.DeltaAngle(0f, r.Player.transform.eulerAngles.y);
            o.HorizontalAxis = new InputAxis
            {
                Value = yaw, Center = yaw, Range = new Vector2(-180f, 180f), Wrap = true,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 9999f, Time = 1f },
            };
            o.VerticalAxis = new InputAxis
            {
                Value = 14f, Center = 14f, Range = new Vector2(ct.pitchMin, ct.pitchMax), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 9999f, Time = 1f },
            };
            o.RadialAxis = new InputAxis
            {
                Value = 1f, Center = 1f, Range = new Vector2(1f, 1f), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 1f, Time = 2f },
            };

            var c = go.AddComponent<CinemachineRotationComposer>();
            var comp = ScreenComposerSettings.Default;
            comp.ScreenPosition = Vector2.zero;
            comp.DeadZone.Enabled = false;
            comp.DeadZone.Size = Vector2.zero;
            comp.HardLimits.Enabled = true;
            comp.HardLimits.Size = new Vector2(0.8f, 0.8f);
            comp.HardLimits.Offset = Vector2.zero;
            c.Composition = comp;
            c.CenterOnActivate = true;
            c.TargetOffset = Vector3.zero;
            c.Damping = new Vector2(0.10f, 0.20f);
            c.Lookahead = new LookaheadSettings { Enabled = false, Time = 0f, Smoothing = 0f, IgnoreY = false };

            var d = go.AddComponent<CinemachineDeoccluder>();
            d.CollideAgainst = Layers.CameraBlock;
            d.IgnoreTag = "Player";
            d.TransparentLayers = 0;
            d.MinimumDistanceFromTarget = 0.01f;
            d.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = 0f,
                CameraRadius = ct.camRadius,
                UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings { Enabled = false, YOffset = 0f },
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 4,
                SmoothingTime = ct.holdTime,
                Damping = ct.releaseDamping,
                DampingWhenOccluded = ct.occludedDamping,
            };
            d.ShotQualityEvaluation.Enabled = false;
            go.AddComponent<TraumaShake>();

            var rig = go.AddComponent<CombatCamRig>();
            rig.Cam = cam;
            rig.Orbit = o;
            rig.Occ = d;
            rig.Pivot = pivot;
            rig.Motor = r.Motor;
            rig.CamTarget = r.CamTarget;
            rig.Lock = pc != null ? pc.Lock : null;
            rig.CamT = ct;
            rig.Tuning = t;
            rig.Actions = actions;
            if (pc != null) pc.CombatCam = rig;

            if (r.Brain != null && r.Brain.CustomBlends == null) r.Brain.CustomBlends = CombatMode.Blends();
            var rot = Quaternion.Euler(14f, yaw, 0f);
            go.transform.SetPositionAndRotation(pivot.position + rot * new Vector3(0f, 0f, -5f), rot);
            return rig;
        }

        /// 전환 담당(CombatMode) 오브젝트
        public static CombatMode AddMode(RigFactory.Rig r, PlayerCombat pc, CombatCamRig cam, bool startInCombat)
        {
            var go = new GameObject("Combat");
            var m = go.AddComponent<CombatMode>();
            m.Player = pc;
            m.Input = r.Input;
            m.CombatCam = cam;
            m.ExploreCam = r.CamRig;
            m.StartInCombat = startInCombat;
            return m;
        }
    }
}
