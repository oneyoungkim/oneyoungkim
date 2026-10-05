// 행인1의 메인이벤트 — 레이어 번호 (docs/07_M1_조작_설계.md 3-5)
// 6 Ground / 7 Wall / 8 Player / 9 PlayerOnly / 10 Interact / 11 CamBlock. 이름은 Zone1Builder 가 TagManager 에 넣는다.
// M2(08 문서 10-2): 12 Fighter(적 CharacterController — 플레이어 막힘, 카메라 무시) / 13 Crowd(구경꾼 — 플레이어·적 통과, 카메라 무시). 이름은 CombatSetup 이 넣는다.
namespace Haengin
{
    public static class Layers
    {
        public const int Default = 0, IgnoreRaycast = 2, Ground = 6, Wall = 7, Player = 8, PlayerOnly = 9, Interact = 10, CamBlock = 11, Fighter = 12, Crowd = 13;

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (int l in layers) m |= 1 << l;
            return m;
        }

        /// 플레이어가 딛는 땅 검사 대상(플레이어·트리거·카메라 전용 벽·적·구경꾼 빼고 전부 — 적 캡슐을 땅으로 보지 않게)
        public static readonly int Solid = ~Mask(Player, IgnoreRaycast, Interact, CamBlock, Fighter, Crowd);

        /// 카메라 가림 검사 대상(07 4-3: 얇은 PlayerOnly 는 카메라가 통과)
        public static readonly int CameraBlock = Mask(Default, Ground, Wall, CamBlock);
    }
}
