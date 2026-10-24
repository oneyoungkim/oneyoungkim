// 행인1의 메인이벤트 — 콤비 호출 드릴(docs/12_체육관_설계.md 4-2 D03 섀도복싱 · D04 미트 치기): CoachCaller(호출) + ComboRun(M2 연결 창 판정) + M2 StartAttack(연출·판정)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    /// D03(허공) · D04(이윤준 미트 — 미트 들림 = 호출 + Lead 가 기대 타격 시각, 판정 시작과의 |오차| = timing). 3R 미트는 코치 반격 스윙 → 슬립·롤
    public sealed class ShadowStation : DrillStation
    {
        readonly bool mitt;
        Dictionary<char, ComboToken> lib;
        int comboIndex;
        double nextCallAt, callGap = 1.2;
        /// 미트 들림 lead(호출 뒤 초) — 코치 애니 이벤트 자리
        public const double MittLead = 0.45;
        double lastStrikeStart = -1;
        bool guardWindow; double guardDeadline;
        int strikesInCombo;
        readonly List<double> expectHits = new List<double>();   // 미트: 토큰 순서대로 기대 타격 시각
        double lastExpect = -1, lastOpen = -1;
        public ShadowStation(bool mitt) { this.mitt = mitt; }
        public override string Hint => mitt ? "호출 콤비를 미트에 — 미트 들리는 순간 · 끝나면 R1 가드" : "호출 콤비를 허공에 — 끝나면 R1 가드";

        public override void Begin(DrillRunner r)
        {
            base.Begin(r);
            lib = ComboLib.Tokens(Actors.Moves);
            comboIndex = 0;
            if (mitt && Actors.Mitt != null) Actors.Mitt.ResetFighter();
        }

        public override void RoundStart(int round)
        {
            Combo = null; nextCallAt = 1.0; guardWindow = false;
            callGap = round == 1 ? 1.2 : round == 2 ? 1.0 : 0.8;
            if (Actors.Me != null) { Actors.Me.ScriptAnim = null; Actors.Me.ResetFighter(); }
            if (mitt) Actors.Yunjun?.Play("Stance");
        }

        void Call(double t)
        {
            string code = ComboLib.Pick(R.Round, comboIndex++);
            Combo = new ComboRun(code, lib, t);
            strikesInCombo = 0; lastStrikeStart = -1; lastExpect = -1; lastOpen = -1;
            expectHits.Clear();
            Actors.Say?.Invoke("이윤준", Combo.Spoken);
            if (mitt) Actors.Yunjun?.Play("Block", 0.08f);            // 미트 들림(코치 가드 올리기 클립 자리)
        }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (Combo == null)
            {
                if (t >= nextCallAt && t < Def.RoundSec - 2.5) Call(nextCallAt);
                return;
            }
            // 입력 없이 창이 닫힌 토큰 = 틀림
            int missed = Combo.Expire(t);
            for (int i = 0; i < missed; i++) Log.Add("combo", t, 0, R.Round);
            if (Combo.Done)
            {
                if (!Combo.Scored)
                {
                    Combo.Scored = true;
                    guardWindow = true; guardDeadline = Math.Max(t, Combo.EndsAt) + 0.8;
                }
                if (guardWindow)
                {
                    if (inp.Guard) { Log.Add("guard", t, 1, R.Round); guardWindow = false; }
                    else if (t >= guardDeadline) { Log.Add("guard", t, 0, R.Round); guardWindow = false; R.Toast("가드!"); }
                }
                if (!guardWindow) { Combo = null; nextCallAt = t + callGap; if (mitt) Actors.Yunjun?.Play("Stance"); }
            }
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (Combo == null || Combo.Done || b == DrillBtn.Guard) return;
            int idx = Combo.Next;
            Combo.Window(out double openNow, out _);
            var tok = Combo.Feed(b, hold, stickDown, t, out bool correct, out double startAt);
            if (tok == null) return;
            Log.Add("combo", t, correct ? 1 : 0, R.Round);
            if (idx == 0) Log.Add("react", t, Math.Max(0, (t - Combo.CalledAt) * 1000.0), R.Round);
            // 기대 시각 사슬(회피 토큰 포함): 첫 토큰 = 호출 + MittLead(미트 들림), 다음 = 앞 기대 + (앞 LinkAt − 앞 ActiveStart) + 이번 ActiveStart
            double expect = idx == 0 ? Combo.CalledAt + MittLead : lastExpect + (ComboLib.LinkAt(Combo.Tokens[idx - 1]) - ComboLib.ActiveStart(Combo.Tokens[idx - 1])) + ComboLib.ActiveStart(tok);
            lastExpect = expect;
            if (!correct) { R.Toast("틀림"); return; }
            // 연출·판정: M2 기술 재생(섀도는 허공, 미트는 코치 Fighter 를 고정 대상으로)
            var me = Actors.Me;
            if (tok.Defensive)
            {
                if (me != null) { me.ScriptAnim = "Dodge"; R.StartCoroutine(ClearScript(me, 0.35f)); }
            }
            else
            {
                if (me != null && tok.Move != null)
                {
                    me.ScriptAnim = null;
                    var aim = mitt ? Actors.Mitt : null;
                    me.StartAttack(tok.Move, aim, 0, aim, 0);
                }
                // 타격 간격 안정 = 실제 시작 − 연결 창 열림(기대)의 ms(완벽하면 0) — 기술마다 다른 연결 길이를 빼고 잰다
                if (idx > 0) Log.Add("gap", t, Math.Abs(startAt - openNow) * 1000.0, R.Round);
                lastStrikeStart = startAt;
                strikesInCombo++;
                if (mitt)
                {
                    expectHits.Add(expect);
                    // 판정 시작 = 기술 시작(startAt) + 발생 f — M2 StartAttack(t0 = 0) 과 같은 값(G07·G12: Fighter 없이도 결정론)
                    double hitAt = startAt + ComboLib.ActiveStart(tok);
                    double errMs = Math.Abs(hitAt - expect) * 1000.0;
                    Log.Add("timing", t, errMs, R.Round);
                    if (errMs <= 60) R.Toast("팡!");
                }
            }
        }



        System.Collections.IEnumerator ClearScript(Fighter me, float secs)
        {
            float t = 0f;
            while (t < secs) { t += TimeFx.Dt; yield return null; }
            if (me != null && me.ScriptAnim == "Dodge") me.ScriptAnim = null;
        }

        public override void End(bool aborted)
        {
            if (Actors.Me != null) { Actors.Me.CancelAttack(); Actors.Me.ScriptAnim = null; }
            if (mitt) Actors.Yunjun?.Play("Stance");
        }

        // 봇: 호출 뒤 반응(skill 1 → 320ms) → 토큰마다 연결 창 열릴 때 + 오차, 끝나면 가드
        int botTok = -1; double botNextAt; bool botGuard; double botGuardOff;
        public override void Bot(double t, float skill)
        {
            if (botGuard && t >= botGuardOff) { R.InjectGuard(false); botGuard = false; }
            if (Combo == null) { botTok = -1; return; }
            if (Combo.Done)
            {
                if (!botGuard && guardWindow && t >= Combo.EndsAt + 0.05) { R.InjectGuard(true); botGuard = true; botGuardOff = t + 0.25; }
                return;
            }
            if (botTok != Combo.Next)
            {
                botTok = Combo.Next;
                Combo.Window(out double open, out _);
                double react = botTok == 0 ? 0.30 + (1.0 - skill) * 0.6 : 0.0;
                botNextAt = open + react + Math.Abs(Jitter(botTok + comboIndex * 37 + R.Round * 1000, skill, 120)) / 1000.0;
                if (mitt && botTok == 0) botNextAt = Combo.CalledAt + MittLead - ComboLib.ActiveStart(Combo.Tokens[0]) + Jitter(comboIndex * 3 + R.Round * 1000, skill, 170) / 1000.0;
            }
            if (t >= botNextAt)
            {
                var want = Combo.Tokens[Combo.Next];
                bool flub = Flub(comboIndex * 11 + botTok + R.Round * 1000, skill, 0.3);
                var b = flub ? (want.Btn == DrillBtn.Light ? DrillBtn.Heavy : DrillBtn.Light) : want.Btn;
                R.Inject(b, botNextAt, want.Hold, want.StickDown);
            }
        }
    }
}
