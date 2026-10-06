// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class HandPoseClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        public string Who = "시우";
        /// 편 손 · 주먹 · 멱살 잡기 · 엄지 척 · 피스 · 병 쥐기 · 주머니 손 · 손목 그립(손가락 총은 없음 — FUJIMOTO 1-4)
        public string Pose = "주먹";
        public float Weight = 1f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<HandPoseClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() { }
    }

    public sealed class HandPoseClipBeh : PlayableBehaviour
    {
        public HandPoseClip Clip;
        internal bool played;
        public override void OnBehaviourPlay(Playable p, FrameData info) { played = true; CutCtx.Hand(Clip.Who, Clip.Pose, Clip.Weight); }
        public override void OnBehaviourPause(Playable p, FrameData info) { if (!played) return; played = false; CutCtx.Hand(Clip.Who, "편 손", 0f); }
    }
}
