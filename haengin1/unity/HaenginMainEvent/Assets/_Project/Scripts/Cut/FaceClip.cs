// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class FaceClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        public string Who = "";
        public string Face = "normal";

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<FaceClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() => CutCtx.Face(Who, Face);
    }

    public sealed class FaceClipBeh : PlayableBehaviour
    {
        public FaceClip Clip;
        internal bool played;
        public override void OnBehaviourPlay(Playable p, FrameData info) { played = true; CutCtx.Face(Clip.Who, Clip.Face); }
    }
}
