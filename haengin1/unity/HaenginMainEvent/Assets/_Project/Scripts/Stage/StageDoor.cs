// 행인1의 메인이벤트 — 출입문(2026-10-25 오픈월드 방향: 플레이어가 Zone1 에서 걸어 들어가는 실내 장소)
// Zone1 쪽 문(국밥집·하루편의점·시우네·혜성고 방면) = Target 무대 키 + 스폰 'Door' / 무대 안 문 = Target 'Zone1' → 짝 문(Zone1 쪽, 같은 Key)의 ReturnPos 로.
// 같은 GameObject 의 Interactable(× / E, 반경 1.6m)이 부른다. 이야기 진행 중(StoryRunner.Active)에는 막음.
using System.Collections;
using UnityEngine;

namespace Haengin
{
    [RequireComponent(typeof(Interactable))]
    public sealed class StageDoor : MonoBehaviour
    {
        /// 갈 곳: 무대 키(Gukbap·Home·Conv·School) 또는 SceneLoader.Zone1
        public string Target = SceneLoader.Zone1;
        /// 무대 쪽 스폰 이름(무대로 들어갈 때)
        public string Spawn = "Door";
        /// 이 문이 어느 장소의 문인가(Zone1 쪽 문 = 들어갈 무대, 무대 안 문 = 자기 무대)
        public string Key = "";
        /// Zone1 쪽 문: 무대에서 나왔을 때 설 자리(편집기에서 빈 땅을 찾아 둠)
        public Vector3 ReturnPos;
        public float ReturnYaw;

        public static int Uses { get; private set; }
        /// 오픈월드 문지기(WorldRunner): false 면 안 열림(체육관 = 체험 뒤 · 현장 = 연락 뒤 · 마을버스 시간)
        public static System.Func<StageDoor, bool> Gate;

        void Awake() { GetComponent<Interactable>().Used += Use; }

        public void Use()
        {
            var loader = FindAnyObjectByType<SceneLoader>(FindObjectsInactive.Include);
            if (loader == null || loader.Busy || StoryRunner.Active) return;
            if (Gate != null && !Gate(this)) return;
            Uses++;
            loader.StartCoroutine(Go(loader));
        }

        IEnumerator Go(SceneLoader loader)
        {
            if (Target == SceneLoader.Zone1)
            {
                StageDoor back = null;
                foreach (var d in FindObjectsByType<StageDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (d != this && d.Target == Key) { back = d; break; }
                Vector3 p = back != null ? back.ReturnPos : Vector3.zero;
                float yaw = back != null ? back.ReturnYaw : 0f;
                Debug.Log($"[Door] {Key} → Zone1 {(back != null ? back.name : "(짝 문 없음 — 처음 스폰)")}");
                yield return loader.Go(SceneLoader.Zone1, null, back != null, p, yaw);
            }
            else
            {
                Debug.Log($"[Door] Zone1 → {Target}/{Spawn}");
                yield return loader.Go(Target, Spawn, false, Vector3.zero, 0f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Uses = 0; Gate = null; }
    }
}
