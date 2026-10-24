// 행인1의 메인이벤트 — 빌드에서도 간판 글자 빠짐을 한 줄로 남긴다(장면 열고 한 번, 맵 아트 패스 6차)
using UnityEngine;

namespace Haengin
{
    /// 빌드에서도 글자 빠짐을 한 줄로 남긴다(장면 열고 한 번)
    public sealed class GlyphCheck : MonoBehaviour
    {
        void Start()
        {
            int miss = SignAudit.MissingGlyphs(out int texts, out string detail);
            Debug.Log($"[Env] 간판 글자 확인: TMP {texts}개 · 빠진 글자 {miss}{(miss > 0 ? " — " + detail : "")}");
        }
    }
}
