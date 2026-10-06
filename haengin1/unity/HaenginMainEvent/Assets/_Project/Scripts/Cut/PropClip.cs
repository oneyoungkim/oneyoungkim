// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class PropClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        public string Prop = "";
        /// "배우/자리"(예 "시우/오른손")
        public string Socket = "";
        public bool Attach = true;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<PropClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() { if (CutCtx.Once(this)) CutCtx.Prop(Prop, Socket, Attach); }
    }

    public sealed class PropClipBeh : PlayableBehaviour
    {
        public PropClip Clip;
        internal bool played;
        public override void OnBehaviourPlay(Playable p, FrameData info) { played = true; if (CutCtx.Once(Clip)) CutCtx.Prop(Clip.Prop, Clip.Socket, Clip.Attach); }
    }
}
