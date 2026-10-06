// 행인1의 메인이벤트 — 장면 전환(docs/09_M3_버티컬슬라이스_설계.md 2-1)
// 무대 장면(Scenes/St_<키>.unity)을 Additive 로 열고 닫는다. Zone1 장면은 내리지 않고 'Zone1' 루트만 꺼 둔다(다시 나올 때 다시 읽지 않게).
// 전환 = 먹 닦기(덮기 0.25초 → 열기·옮기기 → 걷기 0.25초). 덮인 동안 입력 막음(GameState.InputLocked).
// 스폰: Zone1 = 장면 데이터의 자리(발 = 그 아래 Ground), 무대 = StagePlace 의 스폰 자리. 옮긴 뒤 SnapBehind.
// 실내 무대는 탐색 카메라를 거리 2.8m · FOV 50 으로(CamTuning 복제본을 바꿔 끼움 — 에셋은 그대로).
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string Zone1 = "Zone1";
        public static string StagePath(string key) => $"Assets/_Project/Scenes/St_{key}.unity";

        public InkWipe Wipe;
        public PlayerMotor Motor;
        public CamRig Cam;
        /// Zone1 장면의 'Zone1' 루트(지형·건물·길잡이·이름표)
        public GameObject ZoneRoot;

        /// 지금 있는 곳("Zone1" 또는 무대 키)
        public string Place { get; private set; } = Zone1;
        public bool Busy { get; private set; }
        public StagePlace Stage { get; private set; }
        /// 마지막 전환에 걸린 실제 시간(초)·무대 여는 데 걸린 시간(초)
        public float LastTotal { get; private set; }
        public float LastLoad { get; private set; }
        public int Transitions { get; private set; }
        /// 먹 닦기가 화면을 다 덮었을 때(회차 카드를 거둠)
        public event Action Covered;
        /// 장소를 바꾸고 시우를 옮긴 뒤(걷히기 전) — 시간대 조명(빛 자리가 켜진 장소에서 적용)
        public event Action Placed;

        float zoneKillY = -1000f;
        CamTuning outdoor, indoor;
        Scene stageScene;

        void Awake()
        {
            if (Motor == null) Motor = FindAnyObjectByType<PlayerMotor>();
            if (Cam == null) Cam = FindAnyObjectByType<CamRig>();
            if (Motor != null) zoneKillY = Motor.KillY;
        }

        public bool Indoor => Place != Zone1 && Stage != null && Stage.Indoor;

        /// 실내 카메라 조정값(거리 2.8 · 달리기 3.0 · FOV 50/52)
        CamTuning Indoors()
        {
            if (indoor != null) return indoor;
            if (Cam == null) return null;
            outdoor = Cam.Tuning;
            indoor = outdoor != null ? Instantiate(outdoor) : ScriptableObject.CreateInstance<CamTuning>();
            indoor.name = "CamTuning(실내)";
            indoor.radius = 2.8f; indoor.radiusRun = 3.0f;
            indoor.fov = 50f; indoor.fovRun = 52f;
            return indoor;
        }

        public float ExpectedCamDistance => Cam == null ? 0f : (Indoor ? Indoors().radius : (outdoor != null ? outdoor.radius : Cam.T.radius));

        /// 장소로 옮긴다. place = "Zone1"·무대 키, Zone1 이면 pos·yaw, 무대면 spawn 이름. wipe = 먹 닦기
        public IEnumerator Go(string place, string spawn, bool hasPos, Vector3 pos, float yaw, bool wipe = true)
        {
            if (string.IsNullOrEmpty(place)) place = Zone1;
            Busy = true;
            GameState.InputLocked = true;
            var sw = Stopwatch.StartNew();
            if (wipe && Wipe != null) yield return Wipe.Cover();
            Covered?.Invoke();

            if (place != Place)
            {
                // 지금 무대를 내린다
                if (stageScene.IsValid() && stageScene.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(stageScene);
                    while (op != null && !op.isDone) yield return null;
                }
                stageScene = default;
                Stage = null;
                if (place == Zone1)
                {
                    if (ZoneRoot != null) ZoneRoot.SetActive(true);
                }
                else
                {
                    if (ZoneRoot != null) ZoneRoot.SetActive(false);
                    var lw = Stopwatch.StartNew();
                    string path = StagePath(place);
                    var op = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                    if (op == null) { Debug.LogError($"[M3] 무대 장면을 열지 못함(빌드 목록 확인): {path}"); }
                    while (op != null && !op.isDone) yield return null;
                    stageScene = SceneManager.GetSceneByPath(path);
                    yield return null;
                    Stage = StagePlace.Find(place);
                    LastLoad = (float)lw.Elapsed.TotalSeconds;
                    if (Stage == null) Debug.LogError($"[M3] 무대 {place} 에 StagePlace 가 없음");
                }
                Place = place;
            }

            // 스폰
            Vector3 feet = pos; float y = yaw;
            if (place == Zone1)
            {
                if (hasPos && Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 10f, 1 << Layers.Ground, QueryTriggerInteraction.Ignore)) feet.y = hit.point.y;
            }
            else if (Stage != null)
            {
                var sp = Stage.SpawnPoint(spawn);
                feet = sp.position; y = sp.eulerAngles.y;
                hasPos = true;
            }
            if (Motor != null && hasPos)
            {
                Motor.ClearMoveInput();
                Motor.KillY = place == Zone1 ? zoneKillY : (Stage != null ? Stage.KillY : -10f);
                Motor.Teleport(feet, y);
            }
            if (Cam != null)
            {
                var want = Indoor ? Indoors() : (outdoor != null ? outdoor : Cam.Tuning);
                if (outdoor == null) outdoor = Cam.Tuning;
                if (want != null && Cam.Tuning != want) Cam.Tuning = want;
                Cam.SnapBehind();
            }
            Placed?.Invoke();
            yield return null;
            yield return null;
            if (wipe && Wipe != null) yield return Wipe.Uncover();
            sw.Stop();
            LastTotal = (float)sw.Elapsed.TotalSeconds;
            Transitions++;
            GameState.InputLocked = false;
            Busy = false;
        }

        /// 이야기를 끝내거나 테스트를 정리할 때: 무대를 내리고 Zone1 루트를 켠다(먹 닦기 없이)
        public IEnumerator Reset()
        {
            if (stageScene.IsValid() && stageScene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(stageScene);
                while (op != null && !op.isDone) yield return null;
            }
            stageScene = default;
            Stage = null;
            Place = Zone1;
            if (ZoneRoot != null) ZoneRoot.SetActive(true);
            if (Cam != null && outdoor != null) Cam.Tuning = outdoor;
            if (Motor != null) Motor.KillY = zoneKillY;
            GameState.InputLocked = false;
            Busy = false;
        }
    }
}
