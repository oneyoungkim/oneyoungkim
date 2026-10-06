// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 가계부·도감·회차 카드·흑백 전환(A′) 등 이야기 명령
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.9f, 0.5f, 0.3f), TrackClipType(typeof(UiClip))]
    public sealed class UiTrack : TrackAsset { }
}
