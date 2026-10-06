// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 카메라 프리셋 6종(CutCams) — 클립 동안 그 프리셋 카메라가 우선순위 60
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.4f, 0.8f, 0.8f), TrackClipType(typeof(CamClip))]
    public sealed class CamTrack : TrackAsset { }
}
