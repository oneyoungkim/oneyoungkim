// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 표정 아틀라스 칸(4-2) — 지금은 표정 이벤트 기록만(정식 모델 6단계에서 칸 바꾸기)
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.85f, 0.6f, 0.6f), TrackClipType(typeof(FaceClip))]
    public sealed class FaceTrack : TrackAsset { }
}
