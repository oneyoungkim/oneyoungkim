// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 슬로 요청(주인은 TimeFx 하나)
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.7f, 0.7f, 0.7f), TrackClipType(typeof(TimeClip))]
    public sealed class TimeTrack : TrackAsset { }
}
