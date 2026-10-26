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
        public const string Gym = "Gym", GymScene = "Assets/_Project/Scenes/Gym.unity";
        public static string StagePath(string key) => key == Gym ? GymScene : $"Assets/_Project/Scenes/St_{key}.unity";

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
            indoor.pitchDefault = 8f; indoor.exploreTargetOffset = 0f;   // 2026-10-24: 탐색 B(피치 3°·표적 1.15)는 야외만 — 실내는 예전 값 그대로
            return indoor;
        }

        public float ExpectedCamDistance => Cam == null ? 0f : (Indoor ? (Stage != null && Stage.CamRadius > 0f ? Stage.CamRadius : Indoors().radius) : (outdoor != null ? outdoor.radius : Cam.T.radius));

        /// 무대가 정한 실내 카메라(체육관: 거리 3.0 · 높이 1.55 · FOV 50 · 피치 6° — docs/12 2장). 없으면 공통 실내값
        CamTuning StageCam(StagePlace st)
        {
            var b = Indoors();
            if (st == null || st.CamRadius <= 0f || b == null) return b;
            if (stageCam == null) stageCam = Instantiate(b);
            stageCam.name = "CamTuning(" + st.Key + ")";
            stageCam.radius = st.CamRadius; stageCam.radiusRun = st.CamRadius + 0.2f;
            stageCam.fov = st.CamFov > 0f ? st.CamFov : b.fov; stageCam.fovRun = stageCam.fov + 2f;
            if (st.CamTargetHeight > 0f) stageCam.targetHeight = st.CamTargetHeight;
            stageCam.pitchDefault = st.CamPitch; stageCam.pitchMax = st.CamPitchMax > 0f ? st.CamPitchMax : b.pitchMax;
            return stageCam;
        }
        CamTuning stageCam;

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
                // QA Q02: 실내 문 스폰은 문 바로 앞이라 등 뒤 벽에 카메라가 막혀 머리 뒤통수로 화면이 가득 찼음 → 이야기 장면이 아니면 카메라 자리가 날 때까지 방 안쪽으로 몇 걸음
                if (Stage.Indoor && (!StoryRunner.Active || spawn == "Door")) feet = RoomForCamera(feet, ref y, 0.75f * (Stage.CamRadius > 0f ? Stage.CamRadius : (Indoors() != null ? Indoors().radius : 2.8f)));
            }
            if (Motor != null && hasPos)
            {
                Motor.ClearMoveInput();
                Motor.KillY = place == Zone1 ? zoneKillY : (Stage != null ? Stage.KillY : -10f);
                Motor.Teleport(feet, y);
            }
            if (Cam != null)
            {
                var want = Indoor ? StageCam(Stage) : (outdoor != null ? outdoor : Cam.Tuning);
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

        /// 등 뒤(카메라 쪽) 빈 거리가 want 가 될 때까지 앞으로 0.2m 씩(최대 2.4m) · 몸 방향 ±25°·±50° 도 — 몸이 들어가고 발밑에 땅이 있는 자리만. 더 나은 자리가 없으면 그대로
        public static Vector3 RoomForCamera(Vector3 feet, float yaw, float want) => RoomForCamera(feet, ref yaw, want);

        public static Vector3 RoomForCamera(Vector3 feet, ref float yaw, float want)
        {
            Vector3 best = feet; float bestYaw = yaw;
            float bestClear = BackClear(feet, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            if (bestClear >= want) return feet;
            int body = Layers.Mask(Layers.Default, Layers.Wall);
            float bestScore = bestClear;
            var walk = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;         // 들어가는 쪽은 문 방향
            var side = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
            foreach (float lat in new[] { 0f, 0.6f, -0.6f, 1.2f, -1.2f })
                for (float d = 0f; d <= 2.41f; d += 0.3f)
                {
                    var p = feet + walk * d + side * lat;
                    if (d > 0f || lat != 0f)
                    {
                        if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.28f, body, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Linecast(feet + Vector3.up * 1.0f, p + Vector3.up * 1.0f, body, QueryTriggerInteraction.Ignore)) continue;     // 벽·계산대 너머 말고 문에서 걸어갈 수 있는 곳
                        if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var h, 1.5f, Layers.Solid, QueryTriggerInteraction.Ignore)) continue;
                        if (Mathf.Abs(h.point.y - feet.y) > 0.3f) continue;
                        p.y = h.point.y;
                    }
                    foreach (float dy in new[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f })
                    {
                        float y = yaw + dy;
                        float c = BackClear(p, Quaternion.Euler(0f, y, 0f) * Vector3.forward);
                        float score = Mathf.Min(c, want) - (d + Mathf.Abs(lat)) * 0.04f - Mathf.Abs(dy) / 600f;
                        if (score > bestScore + 0.05f) { bestScore = score; bestClear = c; best = p; bestYaw = y; }
                    }
                }
            yaw = bestYaw;
            return best;
        }

        /// 머리 높이에서 등 뒤로(조금 위로) 카메라가 설 수 있는 거리
        public static float BackClear(Vector3 feet, Vector3 fwd)
        {
            var o = feet + Vector3.up * 1.5f;
            var dir = (-fwd + Vector3.up * 0.15f).normalized;
            return Physics.SphereCast(o, 0.15f, dir, out var hit, 4f, Layers.CameraBlock, QueryTriggerInteraction.Ignore) ? hit.distance : 4f;
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
