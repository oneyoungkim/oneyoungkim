// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 자막·속마음·화면 글자 — 클립이 걸친 동안 보임
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.95f, 0.85f, 0.55f), TrackClipType(typeof(SubtitleClip))]
    public sealed class SubtitleTrack : TrackAsset { }
}
