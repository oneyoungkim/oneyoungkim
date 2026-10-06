// 행인1의 메인이벤트 — 무대 장면의 루트(docs/09_M3_버티컬슬라이스_설계.md 3-3). 무대 = Scenes/St_<키>.unity, SceneLoader 가 Additive 로 연다.
// 무대는 Zone1 과 겹치지 않는 자리(x·z 2000 근처, 바닥 y 0)에 만든다. 실내 무대는 탐색 카메라 거리 2.8m · FOV 50(2-0).
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class StagePlace : MonoBehaviour
    {
        public string Key = "";
        public string Title = "";
        public bool Indoor = true;
        /// 이 높이 아래로 떨어지면 마지막 안전 지점으로(PlayerMotor.KillY)
        public float KillY = -10f;
        /// 스폰 자리(이름으로 찾음, 첫째 = 기본 "Spawn")
        public Transform[] Spawns = new Transform[0];
        /// 자리 무대(9단계 전): 바닥·벽·이름판만
        public bool Placeholder;

        public static readonly List<StagePlace> All = new List<StagePlace>();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static StagePlace Find(string key) => All.Find(p => p != null && p.Key == key);

        public Transform SpawnPoint(string name)
        {
            if (Spawns == null || Spawns.Length == 0) return transform;
            if (!string.IsNullOrEmpty(name))
                foreach (var s in Spawns) if (s != null && s.name == name) return s;
            return Spawns[0] != null ? Spawns[0] : transform;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }
    }
}
