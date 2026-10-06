// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class UiClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        /// 이야기 명령(StoryRunner.ApplyNow 와 같은 말): "name:국밥" · "ledger:+8000:배달비" · "ink:1" · "endure:on" · "card:2" …
        public string Action = "";

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<UiClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() => CutCtx.Ui(this, Action);
    }

    public sealed class UiClipBeh : PlayableBehaviour
    {
        public UiClip Clip;
        internal bool played;
        public override void OnBehaviourPlay(Playable p, FrameData info) { played = true; CutCtx.Ui(Clip, Clip.Action); }
    }
}
