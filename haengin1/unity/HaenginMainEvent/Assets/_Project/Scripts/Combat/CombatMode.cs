// 행인1의 메인이벤트 — 탐색 ↔ 전투 전환 (docs/08_M2_전투_설계.md 2-5)
// 전투 시작: Explore 맵 끔 → Combat 맵 켬, CM_Combat 우선순위 20(브레인 블렌드 0.6초 EaseInOut), 길잡이 HUD·이름표 숨김,
//            공격 입력은 0.4초 뒤부터(08 2-5: 0.8초 시작 → 1.2초 조작 — 전에 누른 것은 버퍼에 남기지 않음), 카메라는 실제 시간.
// 전투 끝: 탐색 카메라를 시우 등 뒤로(SnapBehind, 블렌드 시작 때) → CM_Combat 0(블렌드 0.8초) → Combat 끔 → Explore 켬, HUD 다시, TimeFx.Reset.
// 시비(0~0.8초)·마무리 슬로·결과 카드는 인카운터(13단계)가 이 앞뒤에 붙인다.
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    public sealed class CombatMode : MonoBehaviour
    {
        public static CombatMode Current { get; private set; }

        public PInput Input;
        public PlayerCombat Player;
        public CombatCamRig CombatCam;
        public CamRig ExploreCam;
        [Tooltip("시험장: 장면이 뜨면 바로 전투")] public bool StartInCombat;
        public float InputDelay = 0.4f;

        public bool Active { get; private set; }
        public double StartedAt { get; private set; }
        public event Action<bool> Changed;

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }

        IEnumerator Start()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            if (Input == null && Player != null) Input = Player.GetComponent<PInput>();
            if (CombatCam == null) CombatCam = FindAnyObjectByType<CombatCamRig>();
            if (ExploreCam == null) ExploreCam = FindAnyObjectByType<CamRig>();
            yield return null;
            if (StartInCombat && !Active) Begin(0f);
        }

        public void Begin(float inputDelay = -1f)
        {
            if (Active) return;
            Active = true;
            StartedAt = TimeFx.Real;
            TimeFx.CameraRealTime = true;
            if (Input != null) Input.SetCombatMap(true);
            if (Player != null)
            {
                Player.CombatCam = CombatCam;
                Player.Begin(inputDelay >= 0f ? inputDelay : InputDelay);
            }
            if (CombatCam != null) CombatCam.Activate(true);
            SetExploreHud(false);
            Changed?.Invoke(true);
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            if (ExploreCam != null) ExploreCam.SnapBehind();
            if (CombatCam != null) CombatCam.Activate(false);
            if (Player != null) Player.End();
            if (Input != null) Input.SetCombatMap(false);
            TimeFx.Reset();
            SetExploreHud(true);
            Changed?.Invoke(false);
        }

        /// 길잡이 HUD·이름표·목표 화살표(주황 = 목표 색)를 전투 중 숨김(08 7장)
        static void SetExploreHud(bool on)
        {
            foreach (var h in FindObjectsByType<RouteHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                h.enabled = on;
                var c = h.GetComponentInParent<Canvas>(true);
                if (c != null) c.enabled = on;
            }
            foreach (var n in FindObjectsByType<NameTags>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!on) n.HideAll();
                n.enabled = on;
            }
        }

        /// Cinemachine 블렌드 설정의 '아무 카메라' 표시(CinemachineBlenderSettings.kBlendFromAnyCameraLabel 은 internal)
        public const string AnyCamera = "**ANY CAMERA**";

        /// 브레인 블렌드(2-5): CM_Explore → CM_Combat 0.6초, 되돌아올 때 0.8초, * → CM_Heat 0.15초(EaseIn), CM_Heat → CM_Combat 0.3초
        public static CinemachineBlenderSettings Blends()
        {
            var b = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            b.name = "CombatBlends";
            b.CustomBlends = new[]
            {
                Blend("CM_Explore", "CM_Combat", CinemachineBlendDefinition.Styles.EaseInOut, 0.6f),
                Blend("CM_Combat", "CM_Explore", CinemachineBlendDefinition.Styles.EaseInOut, 0.8f),
                Blend(AnyCamera, "CM_Heat", CinemachineBlendDefinition.Styles.EaseIn, 0.15f),
                Blend("CM_Heat", "CM_Combat", CinemachineBlendDefinition.Styles.EaseInOut, 0.3f),
            };
            return b;
        }

        static CinemachineBlenderSettings.CustomBlend Blend(string from, string to, CinemachineBlendDefinition.Styles s, float time) =>
            new CinemachineBlenderSettings.CustomBlend { From = from, To = to, Blend = new CinemachineBlendDefinition(s, time) };
    }
}
