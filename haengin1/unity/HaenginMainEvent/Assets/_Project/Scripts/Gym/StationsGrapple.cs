// 행인1의 메인이벤트 — 레슬링·그라운드 드릴(docs/12_체육관_설계.md 4-2): D07 스프롤 · D08 더미 테이크다운 · D10 그라운드
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    /// D07: 고준이 랜덤 간격(1.2~3.0초, 결정적 열) '샷!' → L↓+× (창 0.7초) → R1 0.3초 안 = 자세 OK. 페이크에 반응 = 실수. 복귀 = 스프롤 → 스탠스 시간
    public sealed class SprawlStation : DrillStation
    {
        public struct Shot { public double T; public bool Fake; public bool Reacted, PostureDone; public double SprawlAt; }
        readonly List<Shot> shots = new List<Shot>();
        public IReadOnlyList<Shot> Shots => shots;
        int cur = -1;
        double nextShotAt;
        int seq;
        bool sprawled; double recoverFrom = -1;
        public override string Hint => "'샷!' 에 L↓ + × 스프롤 → 바로 R1 — 페이크엔 가만히";

        public override void RoundStart(int round)
        {
            shots.Clear(); cur = -1; seq = round * 100; nextShotAt = 1.5; sprawled = false; recoverFrom = -1;
            if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance";
            Actors.Gojun?.Play("Stance");
        }

        double Gap(int i) { double u = Math.Sin(i * 3.7 + 0.3) * 0.5 + 0.5; return 1.2 + u * 1.8; }
        bool IsFake(int i) { double u = Math.Sin(i * 5.1 + 2.2) * 0.5 + 0.5; return u < 0.25; }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            // 새 샷
            if (t >= nextShotAt && t < Def.RoundSec - 1.5)
            {
                bool fake = IsFake(seq);
                shots.Add(new Shot { T = t, Fake = fake });
                cur = shots.Count - 1;
                nextShotAt = t + 0.9 + Gap(seq);
                seq++;
                Actors.Say?.Invoke("고준", fake ? "…" : "샷!");
                Actors.Gojun?.Play(fake ? "Taunt" : "Tackle", 0.05f);
            }
            if (cur >= 0)
            {
                var s = shots[cur];
                // 창 닫힘(진짜 샷에 반응 없음) = 반응 실패(D 기준 값)
                if (!s.Fake && !s.Reacted && t - s.T > 0.7) { s.Reacted = true; Log.Add("react", t, 700, R.Round); Log.Add("posture", t, 0, R.Round); R.Toast("들렸다!"); shots[cur] = s; }
                // 스프롤 뒤 R1 0.3초 안 못 누름
                if (s.Reacted && !s.PostureDone && !s.Fake && s.SprawlAt > 0 && t - s.SprawlAt > 0.3) { s.PostureDone = true; Log.Add("posture", t, 0, R.Round); shots[cur] = s; }
            }
            // 스프롤 자세에서 스탠스 복귀: 스틱 위 또는 가드 뗀 뒤 0.4초, 또는 자동 1.2초
            if (sprawled)
            {
                bool up = inp.StickUp || (t - recoverFrom > 1.2);
                if (up && t - recoverFrom > 0.25)
                {
                    Log.Add("recover", t, (t - recoverFrom) * 1000.0, R.Round);
                    sprawled = false;
                    if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance";
                    if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
                }
                else if (Actors.Visual != null) Actors.Visual.localPosition = new Vector3(0f, -0.28f, 0.12f);
            }
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (b == DrillBtn.Dodge)
            {
                if (cur < 0) return;
                var s = shots[cur];
                if (s.Fake)
                {
                    if (!s.Reacted) { s.Reacted = true; Log.Add("fake_fail", t, 1, R.Round); shots[cur] = s; R.Toast("페이크!"); Actors.Say?.Invoke("고준", "페이크지롱."); }
                    return;
                }
                if (s.Reacted) return;
                double ms = (t - s.T) * 1000.0;
                if (ms > 700) return;
                s.Reacted = true; s.SprawlAt = t; shots[cur] = s;
                Log.Add("react", t, ms, R.Round);
                sprawled = true; recoverFrom = t;
                if (Actors.Me != null) Actors.Me.ScriptAnim = "Dodge";
                R.Toast("스프롤");
            }
            else if (b == DrillBtn.Guard)
            {
                if (cur < 0) return;
                var s = shots[cur];
                if (s.Fake || !s.Reacted || s.PostureDone || s.SprawlAt <= 0) return;
                s.PostureDone = true; shots[cur] = s;
                bool ok = t - s.SprawlAt <= 0.3;
                Log.Add("posture", t, ok ? 1 : 0, R.Round);
                if (ok) R.Toast("자세 OK");
            }
        }

        public override void End(bool aborted)
        {
            if (Actors.Me != null) Actors.Me.ScriptAnim = null;
            if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            Actors.Gojun?.Play("Stance");
        }

        int botShot = -1; double botAt, botGuardAt, botUpAt; int botStep;
        public override void Bot(double t, float skill)
        {
            if (cur < 0) return;
            if (botShot != cur)
            {
                botShot = cur; botStep = 0;
                var s = shots[cur];
                botAt = s.T + 0.26 + (1.0 - skill) * 0.45 + Math.Abs(Jitter(cur + R.Round * 1000, skill, 80)) / 1000.0;
                botGuardAt = botAt + 0.12 + (1.0 - skill) * 0.25;
                botUpAt = botGuardAt + 0.45;
            }
            var sh = shots[cur];
            bool reactToFake = sh.Fake && Flub(cur + R.Round * 1000 + 7, skill, 0.6);
            if (botStep == 0 && t >= botAt) { if (!sh.Fake || reactToFake) R.Inject(DrillBtn.Dodge, botAt, false, true); botStep = 1; }
            else if (botStep == 1 && t >= botGuardAt) { if (!sh.Fake) { R.InjectGuard(true); } botStep = 2; }
            else if (botStep == 2 && t >= botGuardAt + 0.1) { R.InjectGuard(false); botStep = 3; }
            else if (botStep == 3 && t >= botUpAt) { R.InjectStick(new Vector2(0f, 1f)); botStep = 4; }
            else if (botStep == 4 && t >= botUpAt + 0.1) { R.InjectStick(Vector2.zero); botStep = 5; }
        }
    }

    /// D08: 3단 바 — ① ○ 레벨 체인지(녹색 구간 60ms, 커서 주기 1.2초) → ② L↑ 0.25초 안 → ③ △ 홀드 0.5초 → 피니시(자동). 더블 1R · 싱글 2R, 10회씩
    public sealed class DummyStation : DrillStation
    {
        public const double Period = 1.2, GreenHalf = 0.030;
        int step; double stepAt; double repStart; int reps; bool holding; double holdFrom;
        bool finishing; double finishAt;
        public double Cursor(double t) { double u = (t - repStart) / Period; u -= Math.Floor(u); return u; }   // 0~1, 녹색 = 0.5 ± 0.025
        public int Step => step; public int Reps => reps; public bool Finishing => finishing;
        public override string Hint => "○ 레벨 체인지(녹색) → L↑ 스텝 → △ 길게 드라이브 → 피니시";

        public override void RoundStart(int round)
        {
            step = 0; reps = 0; repStart = 1.0; finishing = false; holding = false;
            if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance";
            if (Actors.Dummy != null) Actors.Dummy.localRotation = Quaternion.identity;
            Actors.Say?.Invoke("고준", Def.CueGojun);
        }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (finishing)
            {
                float k = Mathf.Clamp01((float)((t - finishAt) / 0.6));
                if (Actors.Dummy != null) Actors.Dummy.localRotation = Quaternion.Euler(R.Round == 1 ? -80f * k : 0f, R.Round == 2 ? 70f * k : 0f, 0f);
                if (k >= 1f) { finishing = false; step = 0; repStart = t + 0.6; if (Actors.Dummy != null) Actors.Dummy.localRotation = Quaternion.identity; if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance"; }
                return;
            }
            if (t < repStart) return;
            if (step == 1 && t - stepAt > 0.25) { Log.Add("order", t, 0, R.Round); Log.Add("finish", t, 0, R.Round); R.Toast("스텝 늦음"); step = 0; repStart = t + 0.4; reps++; }
            if (step == 1 && inp.StickUp) { step = 2; stepAt = t; Log.Add("order", t, 1, R.Round); if (Actors.Me != null) Actors.Me.ScriptAnim = "Tackle"; }
            if (step == 2 && t - stepAt > 0.8) { Log.Add("finish", t, 0, R.Round); R.Toast("드라이브 없음"); step = 0; repStart = t + 0.4; reps++; }
            if (step == 3 && holding)
            {
                if (t - holdFrom >= 0.5) { Log.Add("finish", t, 1, R.Round); reps++; finishing = true; finishAt = t; R.Toast(R.Round == 1 ? "더블 레그!" : "싱글 레그!"); holding = false; }
            }
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (finishing || t < repStart) return;
            if (step == 0)
            {
                if (b != DrillBtn.Grab) { Log.Add("order", t, 0, R.Round); R.Toast("순서!"); return; }
                double u = Cursor(t);
                double err = (u - 0.5) * Period * 1000.0;
                Log.Add("lc_err", t, err, R.Round);
                Log.Add("order", t, 1, R.Round);
                step = 1; stepAt = t;
                if (Actors.Me != null) Actors.Me.ScriptAnim = "Kneel";
                if (Actors.Visual != null) Actors.Visual.localPosition = new Vector3(0f, -0.15f, 0f);
                if (Math.Abs(err) > 250) R.Toast("타이밍");
            }
            else if (step == 2)
            {
                if (b != DrillBtn.Heavy) { Log.Add("order", t, 0, R.Round); return; }
                step = 3; holding = true; holdFrom = t;
                if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            }
            else if (step == 1 && b != DrillBtn.Grab) { Log.Add("order", t, 0, R.Round); R.Toast("순서!"); }
        }

        public override void End(bool aborted)
        {
            if (Actors.Me != null) Actors.Me.ScriptAnim = null;
            if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            if (Actors.Dummy != null) Actors.Dummy.localRotation = Quaternion.identity;
        }

        int botStep = -1; double botAt;
        public override void Bot(double t, float skill)
        {
            if (finishing || t < repStart) { botStep = -1; return; }
            if (botStep != step)
            {
                botStep = step;
                if (step == 0)
                {
                    // 다음 녹색 구간 중앙 시각 + 오차
                    double u = Cursor(t); double toGreen = (0.5 - u); if (toGreen < 0.05) toGreen += 1.0;
                    botAt = t + toGreen * Period + Jitter(reps + R.Round * 1000, skill, 220) / 1000.0;
                }
                else if (step == 1) botAt = t + 0.08 + (1.0 - skill) * 0.3;
                else if (step == 2) botAt = t + 0.1;
            }
            if (step == 0 && t >= botAt) { R.Inject(Flub(reps + R.Round * 1000, skill, 0.3) ? DrillBtn.Light : DrillBtn.Grab, botAt); botStep = -2; }
            else if (step == 1 && t >= botAt) { R.InjectStick(new Vector2(0f, 1f)); }
            else if (step == 2 && t >= botAt) { R.InjectStick(Vector2.zero); R.Inject(DrillBtn.Heavy, botAt, true); botStep = -2; }
        }
    }

    /// D10: ① 새우 빼기 20회(BPM 60, L↓+× 좌우 교대) → ② 마운트 탈출 10회(R1 → 브릿지 ○ 창 0.35초 → L 좌/우 올바른 쪽). 1R = 새우, 2R = 탈출
    public sealed class GroundStation : DrillStation
    {
        int side = -1; int escapes; int phase; double windowAt; bool guardUp; int openSide; bool bridged; double bridgeAt;
        public override string Hint => R != null && R.Round == 1 ? "박자에 L↓ + × 새우 빼기(좌우 교대)" : "R1 가드 손 → 고준 무게가 앞으로 올 때 ○ 브릿지 → 벌어진 쪽 L 좌/우";

        public override void RoundStart(int round)
        {
            if (Actors.Me != null) Actors.Me.ScriptAnim = round == 1 ? "Lie" : "Lie";
            if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            if (round == 1) { Beat = new BeatTrack(60, 1.0) { Window = BeatTrack.WindowFor(R.Speed) * 2.0 }; Beat.Reset(); side = -1; }
            else { Beat = null; escapes = 0; phase = 0; windowAt = 1.5; guardUp = false; bridged = false; Actors.Gojun?.Play("Kneel"); Actors.Say?.Invoke("고준", Def.CueGojun); }
        }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (R.Round == 1)
            {
                foreach (var b in Beat.Missed(t, Def.RoundSec)) { if (b < 20) Log.Add("shrimp_err", t, 200, R.Round); }
                return;
            }
            if (escapes >= 10) return;
            // 탈출 시도 하나: 고준 무게 이동 시각 windowAt(0.35초 창). phase 0 대기 → 1 창 열림 → 2 브릿지 뒤 롤 입력 대기
            if (phase == 0 && t >= windowAt - 0.6 && !guardUp) { /* R1 안 올렸으면 창은 그대로 오고 탈출 실패 처리 */ }
            if (phase == 0 && t >= windowAt) { phase = 1; openSide = ((int)Math.Floor(windowAt * 7.3)) % 2 == 0 ? -1 : 1; Actors.Say?.Invoke("고준", "…무겁지?"); }
            if (phase == 1 && t > windowAt + 0.35)
            {
                Log.Add("bridge_err", t, 300, R.Round); Log.Add("escape", t, 0, R.Round); escapes++; R.Toast("눌렸다");
                phase = 0; windowAt = t + 1.6; guardUp = false;
            }
            if (phase == 2)
            {
                if (inp.StickSide != 0)
                {
                    bool ok = inp.StickSide == openSide;
                    Log.Add("escape", t, ok && guardUp ? 1 : 0, R.Round); escapes++;
                    R.Toast(ok && guardUp ? "탈출!" : ok ? "가드 손 먼저" : "반대쪽");
                    phase = 0; windowAt = t + 1.6; guardUp = false; bridged = false;
                    if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
                }
                else if (t - bridgeAt > 0.8) { Log.Add("escape", t, 0, R.Round); escapes++; phase = 0; windowAt = t + 1.6; guardUp = false; bridged = false; if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero; }
                else if (Actors.Visual != null) Actors.Visual.localPosition = new Vector3(0f, 0.18f * Mathf.Sin(Mathf.Clamp01((float)((t - bridgeAt) / 0.4)) * Mathf.PI), 0f);
            }
            if (phase == 0 && inp.Guard) guardUp = true;
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (R.Round == 1)
            {
                if (b != DrillBtn.Dodge) return;
                int beat = Beat.Judge(t, out double err);
                if (beat >= 20) return;
                int want = beat % 2 == 0 ? -1 : 1;
                Log.Add("shrimp_err", t, err * 1000.0, R.Round);
                side = want;
                if (Actors.Visual != null) Actors.Visual.localPosition = new Vector3(0.12f * side, 0f, -0.1f);
                return;
            }
            if (b == DrillBtn.Guard) { if (phase == 0) guardUp = true; return; }
            if (b == DrillBtn.Grab)
            {
                if (phase != 1 && phase != 0) return;
                double err = (t - windowAt) * 1000.0;
                if (phase == 0 && err < -350) { Log.Add("bridge_err", t, Math.Abs(err), R.Round); Log.Add("escape", t, 0, R.Round); escapes++; R.Toast("너무 빨라"); windowAt = t + 1.6; guardUp = false; return; }
                Log.Add("bridge_err", t, Math.Abs(err), R.Round);
                phase = 2; bridged = true; bridgeAt = t;
                R.Toast(openSide < 0 ? "왼쪽 벌어짐" : "오른쪽 벌어짐");
            }
        }

        public override void End(bool aborted)
        {
            if (Actors.Me != null) Actors.Me.ScriptAnim = null;
            if (Actors.Visual != null) Actors.Visual.localPosition = Vector3.zero;
            Actors.Gojun?.Play("Stance");
        }

        int botNext; int botPhase = -1; double botAt;
        public override void Bot(double t, float skill)
        {
            if (R.Round == 1)
            {
                if (t < 0.001) botNext = 0;
                while (botNext < 20)
                {
                    double bt = Beat.TimeOf(botNext) + Jitter(botNext + 500, skill, 150) / 1000.0;
                    if (t < bt) return;
                    R.Inject(DrillBtn.Dodge, bt, false, true); botNext++;
                }
                return;
            }
            if (escapes >= 10) return;
            if (phase == 0 && !guardUp && t >= windowAt - 0.8) { R.InjectGuard(true); botPhase = 0; }
            if (phase == 0 && guardUp && t >= windowAt - 0.5) { R.InjectGuard(false); }
            if (phase == 1 && botPhase != 1) { botPhase = 1; botAt = windowAt + 0.1 + Math.Abs(Jitter(escapes + 900, skill, 200)) / 1000.0; }
            if (phase == 1 && t >= botAt) { R.Inject(DrillBtn.Grab, botAt); botPhase = 2; }
            if (phase == 2 && botPhase != 3) { botPhase = 3; botAt = t + 0.15; }
            if (phase == 2 && botPhase == 3 && t >= botAt) { bool wrong = Flub(escapes + 77, skill, 0.4); R.InjectStick(new Vector2((wrong ? -openSide : openSide), 0f)); botPhase = 4; }
            if (botPhase == 4 && phase == 0) { R.InjectStick(Vector2.zero); botPhase = -1; }
        }
    }
}
