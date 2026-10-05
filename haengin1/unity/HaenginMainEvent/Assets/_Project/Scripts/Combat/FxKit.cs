// 행인1의 메인이벤트 — 전투 이펙트·효과음·HUD 그림 묶음 (docs/08_M2_전투_설계.md 5-5·5-7·7장)
// 그림은 tools/ink_fx_gen.py(시안 inkFxTex·wordTexture 를 옮김), 소리는 tools/sfx_gen.py(시안 Sound 레시피). 에디터 CombatSetup 이 Settings/FxKit.asset 으로 묶는다.
using TMPro;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/FxKit")]
    public sealed class FxKit : ScriptableObject
    {
        [Header("먹 이펙트(흰 그림 — 재질 색으로 먹을 입힘: brush·splat·lines·ring / 먹 테 구움: shard·star·dust)")]
        public Texture2D Brush, Splat, Shard, Star, Lines, Ring, Dust;
        [Header("의성어(시안 wordTexture 와 같은 그림)")]
        public Texture2D WordPok, WordPpak, WordKwajik, WordKung, WordTuk, WordRead, WordHeat;
        [Header("HUD")]
        public Texture2D Edge, Tri, Arc;
        public TMP_FontAsset Font;
        [Header("재질")]
        [Tooltip("Haengin/FxSprite — 깊이 검사(바닥 고리 등)")] public Material Normal;
        [Tooltip("Haengin/FxSprite — 맨 위(깊이 무시, 시안 top)")] public Material Top;
        [Header("효과음(5-7)")]
        [Tooltip("위력 1~4(약·중·강·기세)")] public AudioClip[] Hit = new AudioClip[5];
        public AudioClip Whoosh, Slam, Heat, Block, Dodge;

        public Texture2D WordFor(string w)
        {
            switch (w)
            {
                case "퍽!": return WordPok;
                case "빡!": return WordPpak;
                case "콰직!": return WordKwajik;
                case "쿵!": return WordKung;
                case "툭": return WordTuk;
                case "읽었다": return WordRead;
                case "기세": return WordHeat;
                default: return null;
            }
        }
    }
}
