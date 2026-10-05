// 행인1의 메인이벤트 — 정해진 입력 순서를 프레임 단위로 넣는 재생기 (docs/08_M2_전투_설계.md 10-1·11장 — 테스트·녹화 공용)
// 예: InputScript.On(pc).Press(0, Btn.Light).Press(5, Btn.Light).Stick(30, 60, Vector3.right).Guard(70, 120)
// 프레임 = 붙인 뒤 지난 Update 횟수(히트스톱 프레임도 센다 — 사람 손가락은 히트스톱에도 움직인다).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(-48)]
    public sealed class InputScript : MonoBehaviour
    {
        public PlayerCombat Target;
        struct Step { public int From, To; public int Kind; public Btn B; public Vector3 Dir; public bool Run; public Action Act; }
        readonly List<Step> steps = new List<Step>();
        public int Frame { get; private set; }
        public int LastFrame { get; private set; }
        public bool Done => Frame > LastFrame;

        public static InputScript On(PlayerCombat pc)
        {
            var s = pc.gameObject.AddComponent<InputScript>();
            s.Target = pc;
            return s;
        }

        InputScript Add(Step s) { steps.Add(s); LastFrame = Mathf.Max(LastFrame, s.To); return this; }
        /// 이 프레임에 버튼 누름
        public InputScript Press(int frame, Btn b) => Add(new Step { From = frame, To = frame, Kind = 0, B = b });
        /// from~to 프레임 동안 월드 방향 스틱
        public InputScript Stick(int from, int to, Vector3 dir, bool run = false) => Add(new Step { From = from, To = to, Kind = 1, Dir = dir, Run = run });
        /// from~to 프레임 동안 막기
        public InputScript Guard(int from, int to) => Add(new Step { From = from, To = to, Kind = 2 });
        /// 이 프레임에 아무 일(락온 등)
        public InputScript Do(int frame, Action a) => Add(new Step { From = frame, To = frame, Kind = 3, Act = a });

        void Update()
        {
            if (Target == null) return;
            bool stick = false, guard = false;
            foreach (var s in steps)
            {
                if (Frame < s.From || Frame > s.To) continue;
                switch (s.Kind)
                {
                    case 0: Target.Press(s.B); break;
                    case 1: Target.SetStickWorld(s.Dir, 1f, s.Run); stick = true; break;
                    case 2: guard = true; break;
                    case 3: s.Act?.Invoke(); break;
                }
            }
            if (!stick) Target.SetStickWorld(Vector3.zero, 0f);
            Target.SetGuard(guard);
            Frame++;
        }
    }
}
