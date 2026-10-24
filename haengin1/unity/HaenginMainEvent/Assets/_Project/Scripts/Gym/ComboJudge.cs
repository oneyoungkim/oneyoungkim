// 행인1의 메인이벤트 — 콤비 호출·판정(docs/12_체육관_설계.md 4-2 D03·D04) — M2 MoveDef 의 발생/판정/연결 창을 그대로 쓴다(G08)
// 토큰: 1 잽 □ · 2 크로스 △ · 3 왼 훅 □ 길게 · 4 오른 어퍼 △ 길게 · 5 왼 바디 L↓+□ · 6 오른 바디 L↓+△ · K 킥 R2 · N 니 R2 길게 · S 슬립 × · R 롤 L↓+×
// 연결 창(08 3-2): 앞 기술 시작 t0 뒤 [LinkAt, Total + ComboReset] 안에 다음 입력. 그 앞(12f 버퍼 안)은 LinkAt 로 미뤄 받고, 더 이르거나 늦으면 '틀림'.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public enum DrillBtn { Light, Heavy, Grab, Dodge, Kick, Guard }

    public sealed class ComboToken
    {
        public char Code;
        public string Label;          // 호출 글("원", "투", "슬립" …)
        public string Icon;           // 아이콘 글자(□ △ × R2 …)
        public DrillBtn Btn;
        public bool Hold;             // 길게
        public bool StickDown;        // L↓ 와 함께
        public bool Defensive;        // 슬립·롤(공격 아님)
        public MoveDef Move;          // 기대 타격 시각 계산용(M2 MoveDef, 없으면 회피 기본 24f)
        public override string ToString() => Label;
    }

    public static class ComboLib
    {
        /// 토큰 표(4-2 D03). moves 가 있으면 M2 기술을 연결(발생/판정/연결 창), 없으면 MoveLib 기본값으로 만든다
        public static Dictionary<char, ComboToken> Tokens(MoveSet moves)
        {
            MoveDef jab = moves != null ? moves.Jab : MoveLib.Jab(), cross = moves != null ? moves.Cross : MoveLib.Cross(), hook = moves != null ? moves.Hook : MoveLib.Hook(),
                    upper = moves != null ? moves.Upper : MoveLib.Upper(), kick = moves != null ? moves.FrontKick : MoveLib.FrontKick(), knee = moves != null ? moves.StepKnee : MoveLib.StepKnee();
            return new Dictionary<char, ComboToken>
            {
                ['1'] = new ComboToken { Code = '1', Label = "원", Icon = "□", Btn = DrillBtn.Light, Move = jab },
                ['2'] = new ComboToken { Code = '2', Label = "투", Icon = "△", Btn = DrillBtn.Heavy, Move = cross },
                ['3'] = new ComboToken { Code = '3', Label = "쓰리", Icon = "□─", Btn = DrillBtn.Light, Hold = true, Move = hook },
                ['4'] = new ComboToken { Code = '4', Label = "포", Icon = "△─", Btn = DrillBtn.Heavy, Hold = true, Move = upper },
                ['5'] = new ComboToken { Code = '5', Label = "파이브", Icon = "↓□", Btn = DrillBtn.Light, StickDown = true, Move = jab },
                ['6'] = new ComboToken { Code = '6', Label = "식스", Icon = "↓△", Btn = DrillBtn.Heavy, StickDown = true, Move = cross },
                ['K'] = new ComboToken { Code = 'K', Label = "킥", Icon = "R2", Btn = DrillBtn.Kick, Move = kick },
                ['N'] = new ComboToken { Code = 'N', Label = "니", Icon = "R2─", Btn = DrillBtn.Kick, Hold = true, Move = knee },
                ['S'] = new ComboToken { Code = 'S', Label = "슬립", Icon = "×", Btn = DrillBtn.Dodge, Defensive = true },
                ['R'] = new ComboToken { Code = 'R', Label = "롤", Icon = "↓×", Btn = DrillBtn.Dodge, StickDown = true, Defensive = true },
            };
        }

        /// 라운드별 콤비 목록(4-2 D03): 1R 3타 · 2R 4타+슬립 · 3R 5타+롤·킥. 결정적(라운드·순번으로 고름)
        public static readonly string[][] Rounds =
        {
            new[] { "12", "123", "112", "132", "212", "123", "121", "132" },
            new[] { "12S", "123S", "1S2", "1232", "S12", "1234", "12S3", "2S12" },
            new[] { "12K", "123R", "1R23", "12S4", "123K", "1S2K", "12R34", "1232K" },
        };

        public static string Pick(int round, int index)
        {
            var r = Rounds[Mathf.Clamp(round - 1, 0, Rounds.Length - 1)];
            return r[Mathf.Abs(index) % r.Length];
        }

        /// 입력 버퍼(08 2-2: 12f) · 콤보 리셋(조정값 없을 때 0.5초)
        public const double Buffer = 12.0 / MoveDef.Fps, ComboReset = 0.5;
        public const double DefensiveTotal = 24.0 / MoveDef.Fps;

        /// 토큰의 '기대 타격 시각': 호출 뒤 첫 입력은 call + 반응 기준(사람 마다) — 미트 들림(call + lead)이 기대 시각.
        /// 콤비 안 다음 토큰의 기대 시각 = 앞 토큰 시작 + 앞 Move.LinkAt(연결 창이 열리는 때)
        public static double Total(ComboToken t) => t.Move != null ? t.Move.Total : DefensiveTotal;
        public static double LinkAt(ComboToken t) => t.Move != null ? t.Move.LinkAt : DefensiveTotal * 0.6;
        public static double ActiveStart(ComboToken t) => t.Move != null ? t.Move.ActiveStart : 0.1;
    }

    /// 콤비 하나의 진행 판정(G08): 호출 → 토큰마다 입력 받기 → 끝나면 가드 복귀
    public sealed class ComboRun
    {
        public readonly List<ComboToken> Tokens = new List<ComboToken>();
        public double CalledAt;
        /// 토큰 시작 시각(입력 받아들인 때), 틀림 여부
        public readonly List<double> StartedAt = new List<double>();
        public readonly List<bool> Ok = new List<bool>();
        public int Next;                 // 다음 받을 토큰 번호
        public bool Done => Next >= Tokens.Count;
        public bool GuardDone, Scored;
        public double FirstInputAt = -1;

        public ComboRun(string code, Dictionary<char, ComboToken> lib, double calledAt)
        {
            CalledAt = calledAt;
            foreach (char c in code) if (lib.TryGetValue(c, out var t)) Tokens.Add(t);
        }

        /// 다음 토큰의 연결 창 [열림, 닫힘). 첫 토큰은 호출 뒤 바로(0.4초 안 첫 반응이 지표, 창 자체는 3초)
        public void Window(out double open, out double close)
        {
            if (Next == 0) { open = CalledAt; close = CalledAt + 3.0; return; }
            var prev = Tokens[Next - 1];
            double t0 = StartedAt[Next - 1];
            open = t0 + ComboLib.LinkAt(prev);
            close = t0 + ComboLib.Total(prev) + ComboLib.ComboReset;
        }

        /// 입력 하나를 받는다. 돌려주는 것: 받아들인 토큰(맞음·틀림 모두 Next 가 넘어감) 또는 null(창 밖 — 무시, 틀림으로 세지 않음: 너무 이른 연타)
        public ComboToken Feed(DrillBtn b, bool hold, bool stickDown, double t, out bool correct, out double startAt)
        {
            correct = false; startAt = t;
            if (Done) return null;
            Window(out double open, out double close);
            var want = Tokens[Next];
            if (t < open - ComboLib.Buffer) return null;                      // 버퍼보다 이르면 무시(아직 앞 기술 중)
            if (t < open) startAt = open;                                     // 버퍼 안 → 연결 창 열릴 때 나감(M2 InputClock)
            bool late = t > close;                                            // 창 지나 들어옴 = 끊김(틀림)
            bool match = b == want.Btn && (!want.Hold || hold) && (!want.StickDown || stickDown) && (want.Hold || !hold || want.Btn == DrillBtn.Dodge);
            correct = match && !late;
            if (FirstInputAt < 0) FirstInputAt = t;
            StartedAt.Add(startAt);
            Ok.Add(correct);
            Next++;
            return want;
        }

        /// 입력 없이 창이 닫혔으면 그 토큰은 '틀림'(빠뜨림). 돌려주는 것 = 빠뜨린 토큰 수
        public int Expire(double now)
        {
            int n = 0;
            while (!Done)
            {
                Window(out _, out double close);
                if (now <= close) break;
                StartedAt.Add(close); Ok.Add(false); Next++; n++;
            }
            return n;
        }

        public int Correct { get { int n = 0; foreach (var o in Ok) if (o) n++; return n; } }
        public double EndsAt => Tokens.Count == 0 ? CalledAt : (StartedAt.Count == Tokens.Count ? StartedAt[Tokens.Count - 1] + ComboLib.Total(Tokens[Tokens.Count - 1]) : double.MaxValue);
        public string Code { get { var s = ""; foreach (var t in Tokens) s += t.Code; return s; } }
        public string Spoken { get { var l = new List<string>(); foreach (var t in Tokens) l.Add(t.Label); return string.Join("-", l); } }
    }
}
