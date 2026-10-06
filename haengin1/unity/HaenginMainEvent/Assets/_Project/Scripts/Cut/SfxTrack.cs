// 행인1의 메인이벤트 — 컷신 타임라인 트랙(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신' 새 트랙). 합성음·환경음 켜고 끄기(소리 자리 — 재생 횟수만 셈)
using UnityEngine.Timeline;

namespace Haengin
{
    [TrackColor(0.5f, 0.5f, 0.9f), TrackClipType(typeof(SfxClip))]
    public sealed class SfxTrack : TrackAsset { }
}
