// 행인1의 메인이벤트 — 컷신 재생기(docs/09_M3_버티컬슬라이스_설계.md 2-2): Unity Timeline 을 '손으로 넘기는 시간'(DirectorUpdateMode.Manual)으로 돌린다.
// 시간 = TimeFx.RealDt(고정 프레임 녹화·테스트에서 결정적, 일시정지면 멈춤). 슬로(TimeTrack)는 세상만 느려지고 컷신 시계는 실제 시간 그대로.
// 건너뛰기(길게 0.8초): 아직 안 끝난 클립 중 ICutEnd 의 끝 상태를 시간 순서대로 적용 → 멈춤 → 정리. 끝까지 본 것과 끝 상태가 같다(D05).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class Cutscene : MonoBehaviour
    {
        public PlayableDirector Director;

        public bool Playing { get; private set; }
        public bool Skipped { get; private set; }
        public int Frames { get; private set; }
        public double Duration { get; private set; }
        public string Name { get; private set; } = "";

        PlayableDirector D
        {
            get
            {
                if (Director == null && !TryGetComponent(out Director)) Director = gameObject.AddComponent<PlayableDirector>();
                return Director;
            }
        }

        public IEnumerator Play(TimelineAsset tl, Func<bool> wantSkip)
        {
            if (tl == null) yield break;
            var d = D;
            Name = tl.name;
            Playing = true; Skipped = false; Frames = 0;
            Duration = tl.duration;
            d.playOnAwake = false;
            d.timeUpdateMode = DirectorUpdateMode.Manual;
            d.extrapolationMode = DirectorWrapMode.None;
            d.playableAsset = tl;
            CutCtx.Begin();
            // 컷신 숏 사이·끝은 블렌드 없이 컷(블렌드 중엔 카메라가 인물을 가로질러 엉뚱한 벽을 봄 — 녹화에서 확인)
            brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            prevBlend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new Unity.Cinemachine.CinemachineBlendDefinition(Unity.Cinemachine.CinemachineBlendDefinition.Styles.Cut, 0f);
            d.RebuildGraph();
            d.time = 0;
            d.Play();
            d.Evaluate();
            while (d.time < Duration - 1e-6)
            {
                if (wantSkip != null && wantSkip()) { Skip(tl, d.time); break; }
                yield return null;
                if (GameState.Paused) continue;
                float rdt = UiKit.RealDt;
                CutCtx.Clock += rdt;
                d.time = Math.Min(Duration, d.time + rdt);
                d.Evaluate();
                Frames++;
            }
            d.Stop();
            CutCtx.End();
            yield return null; yield return null;       // 탐색 카메라로 컷으로 돌아간 뒤 블렌드 되돌림
            if (brain != null) brain.DefaultBlend = prevBlend;
            brain = null;
            Playing = false;
            Debug.Log($"[M3] 컷신 {Name}: {(Skipped ? "건너뜀" : "끝")} · {Frames}프레임 · 길이 {Duration:F2}초");
        }

        Unity.Cinemachine.CinemachineBrain brain;
        Unity.Cinemachine.CinemachineBlendDefinition prevBlend;

        /// 이야기를 멈출 때(코루틴이 중간에 끊김): 멈추고 정리, 블렌드 되돌림
        public void Abort()
        {
            if (!Playing) return;
            if (Director != null) Director.Stop();
            CutCtx.End();
            if (brain != null) brain.DefaultBlend = prevBlend;
            brain = null;
            Playing = false;
        }

        /// 지금 시각 이후에 끝나는 클립의 끝 상태를 시작 시각 순서로
        void Skip(TimelineAsset tl, double now)
        {
            Skipped = true;
            var list = new List<TimelineClip>();
            foreach (var tr in tl.GetOutputTracks())
                foreach (var c in tr.GetClips())
                    if (c.end > now + 1e-6 && c.asset is ICutEnd) list.Add(c);
            list.Sort((a, b) => a.start.CompareTo(b.start));
            foreach (var c in list) ((ICutEnd)c.asset).ApplyEnd();
            CutCtx.Log.Add("건너뜀");
        }

        /// 타임라인 안 자막 클립 목록(D06 — 표시 시간 ≥ 글자 수 ÷ 8 + 0.8초)
        public static IEnumerable<(SubtitleClip clip, double secs)> Subtitles(TimelineAsset tl)
        {
            foreach (var tr in tl.GetOutputTracks())
                foreach (var c in tr.GetClips())
                    if (c.asset is SubtitleClip s) yield return (s, c.duration);
        }
    }
}
