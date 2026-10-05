// 행인1의 메인이벤트 — 레이어 번호 (docs/07_M1_조작_설계.md 3-5)
// 6 Ground / 7 Wall / 8 Player / 9 PlayerOnly / 10 Interact / 11 CamBlock. 이름은 Zone1Builder 가 TagManager 에 넣는다.
namespace Haengin
{
    public static class Layers
    {
        public const int Default = 0, IgnoreRaycast = 2, Ground = 6, Wall = 7, Player = 8, PlayerOnly = 9, Interact = 10, CamBlock = 11;

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (int l in layers) m |= 1 << l;
            return m;
        }

        /// 플레이어가 딛고 부딪히는 것(플레이어·트리거·카메라 전용 벽 빼고 전부)
        public static readonly int Solid = ~Mask(Player, IgnoreRaycast, Interact, CamBlock);

        /// 카메라 가림 검사 대상(07 4-3: 얇은 PlayerOnly 는 카메라가 통과)
        public static readonly int CameraBlock = Mask(Default, Ground, Wall, CamBlock);
    }
}
