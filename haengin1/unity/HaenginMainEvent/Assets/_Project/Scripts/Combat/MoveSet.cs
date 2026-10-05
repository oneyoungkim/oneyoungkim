// 행인1의 메인이벤트 — 시우 기술 묶음 (docs/08_M2_전투_설계.md 3-3·10-1)
// ScriptableObject 는 파일 이름 = 클래스 이름이어야 에셋으로 저장된다(MoveDef.cs 에서 떼어 냄).
using UnityEngine;

namespace Haengin
{
    /// 시우 기술 묶음(PlayerCombat 이 읽음). 에셋 하나에 MoveDef 를 하위 에셋으로 담는다(경로 짧게).
    [CreateAssetMenu(menuName = "Haengin/MoveSet")]
    public sealed class MoveSet : ScriptableObject
    {
        public MoveDef Jab, Cross, Hook, CrossEnd, FrontKick, Upper, StepKnee, BigHook;
        public MoveDef Grab, Knee, Knee3, Push;
        public MoveDef CounterCross, DuckUpper;

        public MoveDef[] All => new[] { Jab, Cross, Hook, CrossEnd, FrontKick, Upper, StepKnee, BigHook, Grab, Knee, Knee3, Push, CounterCross, DuckUpper };

        /// 약공격 콤보 순번(1~4)의 기술
        public MoveDef Light(int idx) => idx switch { 1 => Jab, 2 => Cross, 3 => Hook, 4 => CrossEnd, _ => null };

        /// 지금 콤보 순번 뒤 △(08 3-3): 0 앞차기 · 1 어퍼 · 2 스텝 무릎 · 3·4 큰 훅
        public MoveDef Finisher(int combo) => combo switch { 0 => FrontKick, 1 => Upper, 2 => StepKnee, _ => BigHook };

        public static MoveSet CreateDefault()
        {
            var s = CreateInstance<MoveSet>();
            s.name = "SiwooMoves";
            s.Jab = MoveLib.Jab(); s.Cross = MoveLib.Cross(); s.Hook = MoveLib.Hook(); s.CrossEnd = MoveLib.CrossEnd();
            s.FrontKick = MoveLib.FrontKick(); s.Upper = MoveLib.Upper(); s.StepKnee = MoveLib.StepKnee(); s.BigHook = MoveLib.BigHook();
            s.Grab = MoveLib.Grab(); s.Knee = MoveLib.Knee(false); s.Knee3 = MoveLib.Knee(true); s.Push = MoveLib.Push();
            s.CounterCross = MoveLib.CounterCross(); s.DuckUpper = MoveLib.DuckUpper();
            return s;
        }
    }
}
