// 행인1의 메인이벤트 — 대화 진행(docs/09_M3_버티컬슬라이스_설계.md 2-2·2-3)
// 줄 단위(화자·표정·대사·표시 방식·대기·카메라·이벤트). × / Enter / 마우스 왼쪽 = 다음, 길게 0.8초 = 건너뛰기, 설정 '자동 진행'(haengin.talk.auto). 음성 없음.
// 대기 칸이 있는 줄은 그 시간(≥ 글자 수 ÷ 8 + 0.8초)이 지나면 저절로 넘어간다(컷신용). 없으면 입력(자동 진행이면 글자 수 ÷ 8 + 0.8초).
// 건너뛰면 남은 줄의 '끝 상태'를 바로 적용한다: 이벤트는 실행, 선택은 0번(=[참는다])으로(D05 — 끝까지 본 것과 같게, 봇의 기본 답도 0번).
// '참기' 선택(2-3): 비네팅·진동·환경음 낮춤 → [참는다]가 아닌 것을 고르면 경고 팝업 1.5초 → [참는다]로 강제.
//   Endured +1(강제된 것도), 처음 고른 게 [참는다]면 EnduredWillingly +1.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class DialogueRunner : MonoBehaviour
    {
        public SubtitleUi Sub;
        public InnerVoiceUi Inner;
        public ChoiceUi Choice;
        public StoryHud Hud;
        public TalkInput In;
        public EndureFx Endure;
        public LedgerUi LedgerUi;

        public const string AutoKey = "haengin.talk.auto";
        public const float HoldToSkip = 0.8f, MinAdvance = 0.15f;
        public static readonly string[] EndureWarnings = { "합의금 예상: ???원", "학폭 심의 — 보호자 출석" };

        public static bool Auto
        {
            get => PlayerPrefs.GetInt(AutoKey, 0) == 1;
            set { PlayerPrefs.SetInt(AutoKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// 이벤트 실행(기다리는 것 — 가계부 공개 등) · 바로 적용(건너뛸 때)
        public Func<string, IEnumerator> ApplyCo;
        public Action<string> ApplyNow;
        public Func<StoryFlags> Flags;
        /// 봇: 대사를 글자 수 ÷ 8 + 0.8초에 넘기고, 선택은 이 함수가 고른 번호(null 이면 사람 입력)
        public Func<DlgLine, int> Bot;

        public bool Playing { get; private set; }
        public bool Skipped { get; private set; }
        public DlgLine Current { get; private set; }
        /// 보인 순서(D03): "줄:id:표시:화자:표정" · "표정:화자:표정" · "선택:id=번호" · "이벤트:id:글"
        public readonly List<string> Log = new List<string>();
        public event Action<DlgLine> Shown;
        public event Action<DlgLine, int> Chosen;

        bool skipReq;

        public void RequestSkip() => skipReq = true;
        /// 장면이 끝나면 쓰지 않은 건너뛰기 요청을 지운다(다음 장면 대사로 새지 않게)
        public void ClearSkip() => skipReq = false;

        public IEnumerator Play(IList<DlgLine> lines, bool skippable = true)
        {
            Playing = true; Skipped = false;      // skipReq 는 Play 전에 들어온 건너뛰기 요청(봇·장면 건너뛰기)을 살린다
            int i = 0;
            for (; i < lines.Count; i++)
            {
                if (skippable && WantSkip()) { Skipped = true; break; }
                var l = lines[i];
                Current = l;
                switch (l.Mode)
                {
                    case DlgMode.Event:
                        Log.Add($"이벤트:{l.Id}:{l.Text}");
                        Shown?.Invoke(l);
                        if (ApplyCo != null) { var co = ApplyCo(l.Text); if (co != null) yield return co; }
                        continue;
                    case DlgMode.Choice:
                        {
                            Shown?.Invoke(l);
                            int pick = -1;
                            yield return Ask(l, v => pick = v);
                            continue;
                        }
                }
                ShowLine(l);
                float t = 0f;
                float need = l.Wait > 0f ? Mathf.Max(l.Wait, l.MinShow) : (Auto || Bot != null ? l.MinShow : float.MaxValue);
                while (true)
                {
                    yield return null;
                    if (skippable && WantSkip()) { Skipped = true; break; }
                    if (!GameState.Paused) t += UiKit.RealDt;
                    if (t >= need) break;
                    var inp = In != null ? In : TalkInput.Instance;
                    if (inp != null && inp.Next && t >= MinAdvance) break;
                }
                HideLine(l);
                if (Skipped) break;
            }
            if (Skipped)
            {
                // 남은 줄의 끝 상태(i = 아직 처리 안 한 줄 또는 보이던 대사 줄 — 대사 줄은 상태가 없어 그대로 지나감)
                for (int k = i; k < lines.Count; k++) Resolve(lines[k]);
                Log.Add("건너뜀");
            }
            HideAll();
            Current = null;
            skipReq = false;
            Playing = false;
        }

        bool WantSkip()
        {
            if (skipReq) return true;
            var inp = In != null ? In : TalkInput.Instance;
            return inp != null && inp.HoldTime >= HoldToSkip;
        }

        /// 건너뛸 때 줄 하나의 끝 상태
        void Resolve(DlgLine l)
        {
            if (l.Mode == DlgMode.Event) { Log.Add($"이벤트:{l.Id}:{l.Text}"); ApplyNow?.Invoke(l.Text); }
            else if (l.Mode == DlgMode.Choice) Commit(l, 0, 0);
        }

        void ShowLine(DlgLine l)
        {
            Log.Add($"줄:{l.Id}:{DlgLine.ModeName(l.Mode)}:{l.Who}:{l.Face}");
            if (!string.IsNullOrEmpty(l.Face)) Log.Add($"표정:{l.Who}:{l.Face}");
            switch (l.Mode)
            {
                case DlgMode.Inner: Inner?.Show(l.Display); break;
                case DlgMode.Screen: Hud?.Screen(l.Display, 0f); break;
                default: Sub?.Show(l.Who, l.Display); break;      // 자막 · 말풍선(머리 위 자리는 인물 모델이 들어온 뒤 — 지금은 자막 판)
            }
            Shown?.Invoke(l);
        }

        void HideLine(DlgLine l)
        {
            switch (l.Mode)
            {
                case DlgMode.Inner: Inner?.Hide(); break;
                case DlgMode.Screen: Hud?.Screen(null, 0f); break;
                default: Sub?.Hide(); break;
            }
        }

        public void HideAll()
        {
            Sub?.Hide(); Inner?.Hide(); Hud?.Screen(null, 0f);
            if (Endure != null && Endure.On) Endure.End();
        }

        IEnumerator Ask(DlgLine l, Action<int> done)
        {
            var opts = l.Options;
            bool endure = l.IsEndure;
            if (endure) Endure?.Begin();
            int first = -1;
            if (Bot != null) { first = Mathf.Clamp(Bot(l), 0, Mathf.Max(0, opts.Length - 1)); yield return null; }
            else if (Choice != null)
            {
                // 선택지가 떠 있을 때 길게 누르면 0번([참는다])으로 고르고 건너뛴다
                var e = Choice.Ask(opts, v => first = v);
                while (e.MoveNext()) { if (WantSkip()) Choice.Force(0); yield return e.Current; }
            }
            else first = 0;
            int final = first;
            if (endure && first != 0)
            {
                // 경고 팝업 1.5초 → [참는다]로 강제
                LedgerUi?.Warn(EndureWarnings[(first - 1) % EndureWarnings.Length]);
                Choice?.Highlight(0);
                float u = 0f;
                while (u < LedgerUi.WarnSecs) { if (!GameState.Paused) u += UiKit.RealDt; yield return null; }
                final = 0;
            }
            if (endure) Endure?.End();
            Commit(l, first, final);
            done?.Invoke(final);
        }

        void Commit(DlgLine l, int first, int final)
        {
            var f = Flags != null ? Flags() : null;
            Log.Add($"선택:{l.Id}={first}{(final != first ? "→" + final : "")}");
            if (f != null)
            {
                f.Choices.Add($"{l.Id}={first}");
                if (l.IsEndure) { f.Endured++; if (first == 0) f.EnduredWillingly++; }
                else if (l.Note.StartsWith("flag:"))
                {
                    var fl = l.Note.Substring(5).Split(',');
                    if (final >= 0 && final < fl.Length) f.Raise(fl[final].Trim());
                }
            }
            Chosen?.Invoke(l, final);
        }
    }
}
