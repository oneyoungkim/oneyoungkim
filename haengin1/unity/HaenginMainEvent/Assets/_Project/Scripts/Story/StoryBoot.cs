// 행인1의 메인이벤트 — 타이틀 → 이야기 시작 요청(docs/09_M3_버티컬슬라이스_설계.md 2-10)
// 타이틀이 Request 를 넣고 Zone1 을 연다 → Zone1 의 StoryRunner 가 Start 에서 받아 시작한다.
// 장면 바꾸기는 LoadBase 를 거친다: 빌드·플레이 = SceneManager.LoadScene(Single), 테스트 = Additive 로 바꿔 끼움
// (테스트 실행기가 든 첫 장면을 내리면 실행이 멈추기 때문 — unity/README 자동 테스트).
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Haengin
{
    public static class StoryBoot
    {
        public const string TitleScene = "Assets/_Project/Scenes/Title.unity";
        public const string Zone1Scene = "Assets/_Project/Scenes/Zone1.unity";
        public const string LabScene = "Assets/_Project/Scenes/CombatLab.unity";

        public enum Mode { None, New, Continue, Select, Practice }

        public sealed class Req
        {
            public Mode Mode;
            /// Select: 시작 장면 id
            public string SceneId;
            /// Continue: 불러온 저장
            public SaveData Save;
        }

        /// 다음에 뜨는 Zone1 이 처리할 요청(처리하면 지움)
        public static Req Request;

        /// 바탕 장면(타이틀·Zone1·연습장)을 연다. 테스트는 이 함수를 바꿔 끼운다
        public static Action<string> LoadBase = path => SceneManager.LoadScene(path, LoadSceneMode.Single);

        public static void Go(Req r, string scene = Zone1Scene)
        {
            Request = r;
            Debug.Log($"[M3] 바탕 장면 열기: {System.IO.Path.GetFileNameWithoutExtension(scene)} · 요청 {r?.Mode}{(r != null && r.SceneId != null ? " " + r.SceneId : "")}");
            LoadBase(scene);
        }

        public static Req Take() { var r = Request; Request = null; return r; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Request = null;
            LoadBase = path => SceneManager.LoadScene(path, LoadSceneMode.Single);
        }
    }
}
