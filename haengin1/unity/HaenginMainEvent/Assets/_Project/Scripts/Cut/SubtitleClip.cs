// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class SubtitleClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        public string Who = "";
        public string Text = "";
        public DlgMode Mode = DlgMode.Sub;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<SubtitleClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() { }
    }

    public sealed class SubtitleClipBeh : PlayableBehaviour
    {
        public SubtitleClip Clip;
        internal bool played;
        public override void OnBehaviourPlay(Playable p, FrameData info) { played = true; CutCtx.Subtitle(Clip, Clip.Who, Clip.Text, Clip.Mode, true); }
        public override void OnBehaviourPause(Playable p, FrameData info) { if (!played) return; played = false; CutCtx.Subtitle(Clip, Clip.Who, Clip.Text, Clip.Mode, false); }
    }
}
