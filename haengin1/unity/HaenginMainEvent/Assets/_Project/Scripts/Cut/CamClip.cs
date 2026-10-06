// 행인1의 메인이벤트 — 컷신 타임라인 클립(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 클립 + 동작(PlayableBehaviour 는 ScriptableObject 가 아니라 같은 파일에 둠)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Haengin
{
    [Serializable]
    public sealed class CamClip : PlayableAsset, ITimelineClipAsset, ICutEnd
    {
        public CamPreset Preset = CamPreset.Wide;
        /// 주 대상·둘째 대상(배우 이름 — "시우"·무대 안 오브젝트 이름)
        public string Subject = "시우", Other = "";

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CamClipBeh>.Create(graph);
            p.GetBehaviour().Clip = this;
            return p;
        }

        public void ApplyEnd() { }
    }

    public sealed class CamClipBeh : PlayableBehaviour
    {
        public CamClip Clip;
        internal bool played;
        CutCamDriver cam;
        public override void OnBehaviourPlay(Playable p, FrameData info)
        {
            played = true;
            if (cam == null) cam = CutCams.Make(Clip.Preset, CutCtx.Actor(Clip.Subject), CutCtx.Actor(Clip.Other));
            cam.Snap();
            cam.SetLive(true);
            CutCtx.Log.Add($"카메라:{CutCams.Name(Clip.Preset)}:{Clip.Subject}");
        }
        public override void OnBehaviourPause(Playable p, FrameData info) { if (!played) return; played = false; if (cam != null) cam.SetLive(false); }
        public override void OnPlayableDestroy(Playable p) { if (cam != null) CutCams.Release(cam); cam = null; }
    }
}
