// 행인1의 메인이벤트 — 길잡이 HUD 한 줄(화면 위 가운데): "다음: 반시우네 집 앞 · 23m"
// 글은 RouteGuide.Current 에서 읽기만 한다(HudLine). 도착하면 1.6초 동안 "도착: …" 을 보이고 다음 목표로 넘어간다.
// 마지막 체크포인트에 닿으면 "도착: …" 을 4초 보인 뒤 판을 접는다. 부품(Canvas·판·글)은 RouteSetup(에디터)이 만든다.
// 다른 HUD(대사·퀘스트)는 RouteGuide 의 공개 값·이벤트(CurrentTarget · Distance · Reached · Completed)를 직접 써도 된다.
using System.Collections;
using TMPro;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(60)]   // RouteGuide(50) 다음 — 같은 프레임 거리로 그린다
    public sealed class RouteHud : MonoBehaviour
    {
        public TextMeshProUGUI Text;
        [Tooltip("판(배경). 글이 비면 숨김")]
        public GameObject Plate;
        public RouteGuide Guide;
        public float ArriveFlash = 1.6f, FinishHold = 4f;

        RouteGuide hooked;
        string flash;
        float flashUntil = -1f;
        bool finished;
        string last;

        void OnEnable() => Hook();
        void OnDisable() => Unhook();

        /// 실행 확인 한 줄(Player.log, DebugHud 의 '[M1] 실행 확인' 옆): HUD 글·한글 글리프가 실제로 그려지는지
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3.2f);
            if (Text == null) yield break;
            Text.ForceMeshUpdate();
            uint[] missing = null;
            bool all = Text.font != null && Text.font.HasCharacters(Text.text, out missing, false, true);
            var tags = FindAnyObjectByType<NameTags>();
            Debug.Log($"[M1] 길잡이 확인: HUD \"{Text.text}\" · 그린 글자 {Text.textInfo.characterCount} · 글꼴에 없는 글자 {(all ? 0 : missing?.Length ?? -1)} · " +
                      $"목표 {(hooked != null && hooked.CurrentTarget != null ? hooked.CurrentTarget.Label : "없음")} · 보이는 이름표 {(tags != null ? tags.VisibleCount : -1)}");
        }

        void Hook()
        {
            var g = Guide != null ? Guide : RouteGuide.Current;
            if (g == hooked) return;
            Unhook();
            hooked = g;
            if (hooked == null) return;
            hooked.Reached += OnReached;
            hooked.Completed += OnCompleted;
        }

        void Unhook()
        {
            if (hooked == null) return;
            hooked.Reached -= OnReached;
            hooked.Completed -= OnCompleted;
            hooked = null;
        }

        void OnReached(int i)
        {
            var s = hooked != null && i >= 0 && i < hooked.Stops.Length ? hooked.Stops[i] : null;
            if (s == null) return;
            flash = "도착: " + s.Label;
            flashUntil = Time.unscaledTime + ArriveFlash;
        }

        void OnCompleted()
        {
            finished = true;
            flashUntil = Time.unscaledTime + FinishHold;
        }

        void LateUpdate()
        {
            Hook();
            string line;
            if (Time.unscaledTime < flashUntil) line = flash;
            else if (finished || hooked == null) line = "";
            else line = hooked.HudLine();
            Show(line);
        }

        /// 지금 글(테스트용)
        public string Current => Text != null ? Text.text : "";

        /// 도착 알림·완주 상태를 지운다(순간 이동·이어하기·촬영 때 RouteGuide.SetIndex 와 같이 부름)
        public void ResetFlash()
        {
            flash = null;
            flashUntil = -1f;
            finished = false;
        }

        public void Show(string line)
        {
            if (line == last) return;
            last = line;
            bool on = !string.IsNullOrEmpty(line);
            if (Text != null) { Text.text = line ?? ""; Text.enabled = on; }
            if (Plate != null && Plate.activeSelf != on) Plate.SetActive(on);
        }
    }
}
