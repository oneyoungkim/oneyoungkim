// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 손 자세 8종(4-2) — 지금은 '주먹'만 셰이프 키(HandShape), 나머지는 기록
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.6f, 0.8f, 0.6f), TrackClipType(typeof(HandPoseClip))]
    public sealed class HandPoseTrack : TrackAsset { }
}
