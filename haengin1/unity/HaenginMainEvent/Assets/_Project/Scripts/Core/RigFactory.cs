// 행인1의 메인이벤트 — 플레이어·카메라 조립 (docs/07_M1_조작_설계.md 7-8·8장)
// 장면 생성(에디터 M1Setup)과 PlayMode 테스트가 같은 함수로 만든다 → 테스트가 실제 장면과 같은 설정을 검사.
// 주의: 런타임 AddComponent 에서는 Reset() 이 불리지 않으므로 Cinemachine 필드를 전부 명시한다.
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Haengin
{
    public static class RigFactory
    {
        public sealed class Rig
        {
            public GameObject Player;
            public PlayerMotor Motor;
            public Transform Visual, CamTarget;
            public BodyLean Lean;
            public PInput Input;
            public Camera Main;
            public CinemachineBrain Brain;
            public CinemachineCamera Cam;
            public CinemachineOrbitalFollow Orbit;
            public CinemachineRotationComposer Composer;
            public CinemachineDeoccluder Occ;
            public CamRig CamRig;
            public CamClearance Clearance;
            public CamInput Look;
            public DebugHud Hud;
        }

        /// 플레이어 + 카메라 한 벌. existingCam 이 있으면 그 카메라(장면의 Main Camera)에 브레인만 붙여 쓴다.
        public static Rig Build(Vector3 feet, float yawDeg, MoveTuning mt, CamTuning ct, GameObject visual, InputActionAsset actions, Camera existingCam = null)
        {
            var r = BuildPlayer(feet, yawDeg, mt, ct, visual, actions);
            BuildCamera(r, ct, existingCam, actions);
            Wire(r);
            return r;
        }

        /// 플레이어만(프리팹 원본). visual: 이미 만든 모델(에디터 = GLB 인스턴스, 테스트 = 캡슐) 또는 null. actions 가 없으면 PInput 을 붙이지 않는다.
        public static Rig BuildPlayer(Vector3 feet, float yawDeg, MoveTuning mt, CamTuning ct, GameObject visual, InputActionAsset actions)
        {
            var r = new Rig();
            var p = r.Player = new GameObject("Player") { tag = "Player", layer = Layers.Player };
            p.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDeg, 0f));

            var cc = p.AddComponent<CharacterController>();
            PlayerMotor.ApplyController(cc, mt);
            r.Motor = p.AddComponent<PlayerMotor>();
            r.Motor.Tuning = mt;

            r.Visual = new GameObject("Visual") { layer = Layers.Player }.transform;
            r.Visual.SetParent(p.transform, false);
            if (visual != null)
            {
                visual.transform.SetParent(r.Visual, false);
                ModelFit.Fit(visual.transform, mt.height);
                foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                SetLayer(visual, Layers.Player);
            }
            r.Lean = r.Visual.gameObject.AddComponent<BodyLean>();
            r.Lean.Motor = r.Motor;

            r.CamTarget = new GameObject("CamTarget") { layer = Layers.Player }.transform;
            r.CamTarget.SetParent(p.transform, false);
            r.CamTarget.localPosition = new Vector3(0f, ct.targetHeight, 0f);
            r.Motor.CamTarget = r.CamTarget;

            if (actions != null)
            {
                r.Input = p.AddComponent<PInput>();
                r.Input.Actions = actions;
                r.Input.Motor = r.Motor;
            }
            return r;
        }

        /// 메인 카메라 + 브레인 + CM_Explore(OrbitalFollow · RotationComposer · Deoccluder · CamRig · CamInput)
        public static void BuildCamera(Rig r, CamTuning ct, Camera existingCam, InputActionAsset actions)
        {
            float yawDeg = r.Player.transform.eulerAngles.y;

            // ── 메인 카메라 + 브레인
            if (existingCam == null)
            {
                var mc = new GameObject("Main Camera") { tag = "MainCamera" };
                existingCam = mc.AddComponent<Camera>();
                mc.AddComponent<AudioListener>();
            }
            r.Main = existingCam;
            r.Main.gameObject.tag = "MainCamera";
            r.Main.nearClipPlane = ct.near;
            r.Main.farClipPlane = ct.far;
            r.Main.fieldOfView = ct.fov;
            r.Brain = r.Main.GetComponent<CinemachineBrain>();
            if (r.Brain == null) r.Brain = r.Main.gameObject.AddComponent<CinemachineBrain>();
            r.Brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            r.Brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
            r.Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.4f);
            r.Brain.IgnoreTimeScale = false;

            // ── CM_Explore
            var go = new GameObject("CM_Explore");
            r.Cam = go.AddComponent<CinemachineCamera>();
            r.Cam.Priority = 10;
            r.Cam.Target = new CameraTarget { TrackingTarget = r.CamTarget, LookAtTarget = null, CustomLookAtTarget = false };
            var lens = LensSettings.Default;
            lens.FieldOfView = ct.fov;
            lens.NearClipPlane = ct.near;
            lens.FarClipPlane = ct.far;
            r.Cam.Lens = lens;

            var o = r.Orbit = go.AddComponent<CinemachineOrbitalFollow>();
            o.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            o.Radius = ct.radius;
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
            float yaw = Mathf.DeltaAngle(0f, yawDeg);
            o.HorizontalAxis = new InputAxis
            {
                Value = yaw, Center = yaw, Range = new Vector2(-180f, 180f), Wrap = true,
                Recentering = new InputAxis.RecenteringSettings { Enabled = true, Wait = 9999f, Time = ct.recenterTime },
            };
            o.VerticalAxis = new InputAxis
            {
                Value = ct.pitchDefault, Center = ct.pitchDefault, Range = new Vector2(ct.pitchMin, ct.pitchMax), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = true, Wait = 9999f, Time = ct.recenterTime },
            };
            o.RadialAxis = new InputAxis
            {
                Value = 1f, Center = 1f, Range = new Vector2(1f, 1f), Wrap = false,
                Recentering = new InputAxis.RecenteringSettings { Enabled = false, Wait = 1f, Time = 2f },
            };

            var c = r.Composer = go.AddComponent<CinemachineRotationComposer>();
            var comp = ScreenComposerSettings.Default;
            comp.ScreenPosition = ct.screen;
            comp.DeadZone.Enabled = false;
            comp.DeadZone.Size = Vector2.zero;
            comp.HardLimits.Enabled = true;
            comp.HardLimits.Size = new Vector2(0.8f, 0.8f);
            comp.HardLimits.Offset = Vector2.zero;
            c.Composition = comp;
            c.CenterOnActivate = true;
            c.TargetOffset = Vector3.zero;
            c.Damping = ct.aimDamping;
            c.Lookahead = new LookaheadSettings { Enabled = false, Time = 0f, Smoothing = 0f, IgnoreY = false };

            var d = r.Occ = go.AddComponent<CinemachineDeoccluder>();
            d.CollideAgainst = Layers.CameraBlock;
            d.IgnoreTag = "Player";
            d.TransparentLayers = 0;
            d.MinimumDistanceFromTarget = 0.01f;    // 크면 등을 벽에 붙였을 때 검사가 벽 너머에서 시작 → 벽 뚫림(07 4-7)
            d.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = 0f,          // 0 이 아니면 그동안 벽 안이 보인다(소스 확인)
                CameraRadius = ct.camRadius,
                UseFollowTarget = new CinemachineDeoccluder.ObstacleAvoidance.FollowTargetSettings { Enabled = false, YOffset = 0f },
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 4,
                SmoothingTime = ct.holdTime,
                Damping = ct.releaseDamping,
                DampingWhenOccluded = ct.occludedDamping,
            };
            d.ShotQualityEvaluation.Enabled = false;

            // 벽 간격·다리 가림: Deoccluder 다음(Finalize 단계)에서 머리→카메라 선을 따라 당김
            var cl = go.AddComponent<CamClearance>();

            r.CamRig = go.AddComponent<CamRig>();
            r.CamRig.Clearance = cl;
            cl.Rig = r.CamRig;
            r.Clearance = cl;
            r.CamRig.Cam = r.Cam;
            r.CamRig.Orbit = o;
            r.CamRig.Occ = d;
            r.CamRig.Motor = r.Motor;
            r.CamRig.CamTarget = r.CamTarget;
            r.CamRig.Tuning = ct;

            if (actions != null)
            {
                r.Look = go.AddComponent<CamInput>();
                r.Look.Actions = actions;
                r.Look.Orbit = o;
                r.Look.Tuning = ct;
            }

            // 에디터에서 보기 좋게 처음 자리(실행하면 Cinemachine 이 다시 계산)
            var rot = Quaternion.Euler(ct.pitchDefault, yawDeg, 0f);
            go.transform.SetPositionAndRotation(r.CamTarget.position + rot * new Vector3(0f, 0f, -ct.radius), rot);
            r.Main.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);

            r.Hud = r.Main.GetComponent<DebugHud>();
            if (r.Hud == null) r.Hud = r.Main.gameObject.AddComponent<DebugHud>();
        }

        /// 서로 참조 연결(PInput → CamRig, BodyLean → CamRig, HUD)
        public static void Wire(Rig r)
        {
            if (r.Input != null) r.Input.Cam = r.CamRig;
            if (r.Lean != null) r.Lean.Cam = r.CamRig;
            if (r.Hud != null) { r.Hud.Motor = r.Motor; r.Hud.Cam = r.CamRig; }
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
