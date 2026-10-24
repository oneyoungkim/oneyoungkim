// 행인1의 메인이벤트 — 박자 드릴(docs/12_체육관_설계.md 4-2): D01 줄넘기 기본 · D02 더블언더 · D05 샌드백 라운드 · D06 스피드백
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public static class DrillStations
    {
        /// 드릴 id → 구현(없으면 null = 자리만)
        public static DrillStation Create(string id) => id switch
        {
            "D01" => new JumpRopeStation(false),
            "D02" => new JumpRopeStation(true),
            "D03" => new ShadowStation(false),
            "D04" => new ShadowStation(true),
            "D05" => new BagStation(),
            "D06" => new SpeedBagStation(),
            "D07" => new SprawlStation(),
            "D08" => new DummyStation(),
            "D10" => new GroundStation(),
            "D11" => new SparStation(),
            _ => null,
        };

        /// 시우 연출 공용: 발 떼기(점프) 곡선 — 끝나면 0
        public static float Hop(double sinceBeat, double period, float height)
        {
            if (sinceBeat < 0 || sinceBeat > period * 0.5) return 0f;
            float x = (float)(sinceBeat / (period * 0.5));
            return Mathf.Sin(x * Mathf.PI) * height;
        }
    }

    /// D01 줄넘기 기본(BPM 120 → 2R 132, × 로 점프) · D02 더블언더(△ 표시 박자에 △ — 4박마다 → 2박마다)
    public sealed class JumpRopeStation : DrillStation
    {
        readonly bool doubleUnder;
        double lastBeatT = -9; int beats;
        float tripFreeze; double freezeUntil = -1;
        LineRenderer rope;
        public JumpRopeStation(bool du) { doubleUnder = du; }
        public override string Hint => doubleUnder ? "박자에 × 점프 · △ 표시에는 △ 더블언더" : "박자에 × 점프 — 발끝";

        public override void RoundStart(int round)
        {
            double bpm = doubleUnder ? 120 : (round >= 2 ? 132 : 120);
            Beat = new BeatTrack(bpm, 1.0) { Window = BeatTrack.WindowFor(R.Speed) };
            if (doubleUnder) { int every = round >= 3 ? 2 : round == 2 ? 3 : 4; Beat.MarkOf = b => b > 0 && b % every == 0 ? 1 : 0; }
            Beat.Reset(); beats = 0; tripFreeze = 0f; freezeUntil = -1;
            if (Actors.Player != null && Actors.Me != null) Actors.Me.ScriptAnim = "Stance";
            EnsureRope();
        }

        void EnsureRope()
        {
            if (rope != null || Actors.Visual == null) return;
            var go = new GameObject("줄넘기 줄");
            go.transform.SetParent(Actors.Visual, false);
            rope = go.AddComponent<LineRenderer>();
            rope.useWorldSpace = false; rope.positionCount = 24; rope.startWidth = rope.endWidth = 0.012f;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh != null) { var m = new Material(sh); m.SetColor("_BaseColor", new Color(0.1f, 0.08f, 0.09f)); m.color = new Color(0.1f, 0.08f, 0.09f); rope.sharedMaterial = m; }
            rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (tripFreeze > 0f) tripFreeze -= dt;
            foreach (var b in Beat.Missed(t, Def.RoundSec))
            {
                if (Beat.TimeOf(b) < freezeUntil) continue;          // 걸려서 멈춘 0.8초 동안 지나간 박자는 다시 세지 않음
                // 입력 없이 지나간 박자 = 걸림(발이 줄에)
                Log.Add("trip", t, 1, R.Round);
                if (doubleUnder && Beat.Mark(b) == 1) Log.Add("du", t, 0, R.Round);
                tripFreeze = 0.8f; freezeUntil = t + 0.8;
                R.Toast("걸림!"); Actors.Say?.Invoke("이윤준", "다시.");
            }
            // 연출: 박자마다 발 떼기 + 줄 돌리기
            double since = t - lastBeatT;
            if (Actors.Visual != null)
            {
                float h = tripFreeze > 0f ? 0f : DrillStations.Hop(since, Beat.Interval, doubleUnder ? 0.22f : 0.13f);
                Actors.Visual.localPosition = new Vector3(0f, h, 0f);
            }
            if (rope != null)
            {
                float ang = tripFreeze > 0f ? 2.6f : (float)((t - Beat.Lead) / Beat.Interval * Mathf.PI * 2f) + Mathf.PI;
                for (int i = 0; i < rope.positionCount; i++)
                {
                    float u = i / (float)(rope.positionCount - 1);
                    float x = Mathf.Lerp(-0.42f, 0.42f, u);
                    float r = 1.0f * Mathf.Sin(u * Mathf.PI) * 0.5f + 0.5f;    // 손에서 멀수록 큰 원
                    rope.SetPosition(i, new Vector3(x, 0.95f + Mathf.Cos(ang) * 1.0f * r, Mathf.Sin(ang) * 0.9f * r));
                }
            }
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (b != DrillBtn.Dodge && b != DrillBtn.Heavy) return;
            if (tripFreeze > 0f) return;                                           // 걸린 뒤 0.8초 정지
            int beat = Beat.Judge(t, out double err);
            double ms = err * 1000.0;
            lastBeatT = Beat.TimeOf(beat);
            beats++;
            bool mark = doubleUnder && Beat.Mark(beat) == 1;
            if (Math.Abs(err) > Beat.Window * 2.5)
            {
                Log.Add("trip", t, 1, R.Round);
                if (mark) Log.Add("du", t, 0, R.Round);
                tripFreeze = 0.8f; freezeUntil = t + 0.8; R.Toast("걸림!"); Actors.Say?.Invoke("이윤준", "다시.");
                return;
            }
            Log.Add("beat_err", t, ms, R.Round);
            if (doubleUnder)
            {
                if (mark) { bool ok = b == DrillBtn.Heavy && Math.Abs(err) <= Beat.Window * 1.5; Log.Add("du", t, ok ? 1 : 0, R.Round); if (!ok) { Log.Add("trip", t, 1, R.Round); tripFreeze = 0.8f; freezeUntil = t + 0.8; R.Toast("걸림!"); } }
                else if (b == DrillBtn.Heavy) { Log.Add("trip", t, 1, R.Round); tripFreeze = 0.6f; freezeUntil = t + 0.6; R.Toast("걸림!"); }   // 표시 없는 박자에 △ = 줄 두 바퀴 못 돎
            }
        }

        public override void End(bool aborted)
        {
            if (rope != null) UnityEngine.Object.Destroy(rope.gameObject);
            rope = null;
        }

        int botNext;
        public override void Bot(double t, float skill)
        {
            if (t < 0.001) botNext = 0;
            while (true)
            {
                double bt = Beat.TimeOf(botNext);
                if (bt > Def.RoundSec - 0.01) return;
                double err = Jitter(botNext + R.Round * 1000, skill, 140) / 1000.0;
                if (t < bt + err) return;
                bool mark = doubleUnder && Beat.Mark(botNext) == 1;
                if (!Flub(botNext + R.Round * 1000, skill, 0.25)) R.Inject(mark ? DrillBtn.Heavy : DrillBtn.Dodge, bt + err);
                botNext++;
            }
        }
    }

    /// D05 샌드백 라운드: 박자 띠(BPM 100) 안에 치면 '리듬 타격', 파워 = 홀드(△ 길게 = 강) + 리듬, 백이 돌아올 때 ×1.3, 치고 1초 안 R1 안 하면 복귀율 깎임, 3R 마지막 20초 몰아치기
    public sealed class BagStation : DrillStation
    {
        BagSwing bag;
        Fighter target;
        double lastHitT = -9; bool guardPending; bool rushCalled;
        int hitsThisRound;
        /// 진단: 누름 · 기술 시작 · 몸 상태 때문에 건너뜀 · 집계된 타격
        public int Presses, Attacks, SkippedBusy, Hits;
        public string LastSkip = "";
        public override string Hint => "□ 약 · △ 강(길게) · 박자 안에 · 돌아올 때 쳐 · 치고 R1 가드";

        public override void Begin(DrillRunner r)
        {
            base.Begin(r);
            bag = null;
            float best = float.MaxValue;
            foreach (var b in Actors.Bags) { if (b == null) continue; float d = (b.transform.position - Actors.Station.position).sqrMagnitude; if (d < best) { best = d; bag = b; } }
            if (bag != null) target = bag.Target;
            Fighter.AnyHit += OnHit;
        }

        public override void RoundStart(int round)
        {
            Beat = new BeatTrack(100, 1.0) { Window = BeatTrack.WindowFor(R.Speed) * 1.5 };
            Beat.Reset(); hitsThisRound = 0; rushCalled = false; guardPending = false;
            if (Actors.Me != null) { Actors.Me.ScriptAnim = null; Actors.Me.ResetFighter(); }
        }

        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (guardPending)
            {
                if (inp.Guard) { Log.Add("guard", t, 1, R.Round); guardPending = false; }
                else if (t - lastHitT > 1.0) { Log.Add("guard", t, 0, R.Round); guardPending = false; R.Toast("어깨 툭"); }
            }
            if (R.Round == Def.Rounds && !rushCalled && t >= Def.RoundSec - 20.0) { rushCalled = true; Actors.Say?.Invoke("고준", Def.CueGojun); }
        }

        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (b != DrillBtn.Light && b != DrillBtn.Heavy) return;
            Presses++;
            var me = Actors.Me; var moves = Actors.Moves;
            if (me == null || moves == null) { LastSkip = "몸 없음"; return; }
            if (me.State != Fighter.Phase.Free && !(me.State == Fighter.Phase.Act && me.Run != null && me.Run.LinkOpen)) { SkippedBusy++; LastSkip = me.State + (me.Run != null ? $" {me.Run.Move.Label} T{me.Run.T:F2}" : ""); return; }
            Beat.Judge(t, out double err);
            pendingRhythm = err * 1000.0; pendingHold = hold; pendingT = t;
            MoveDef m = b == DrillBtn.Light ? (hold ? moves.Hook : moves.Jab) : (hold ? moves.Upper : moves.Cross);
            me.StartAttack(m, target, 0, target, 0);
            Attacks++;
        }

        double pendingRhythm; bool pendingHold; double pendingT;
        /// 파워(4-2 D05): 위력 단계 + 길게 0.5 + 리듬 안 0.5, 백이 돌아올 때 ×1.3 — 순수 함수(G10)
        public static float PowerOf(Power p, bool hold, bool rhythmOk, bool returning) => ((int)p + (hold ? 0.5f : 0f) + (rhythmOk ? 0.5f : 0f)) * (returning ? 1.3f : 1f);
        void OnHit(HitEvent ev)
        {
            if (!R.Running || ev.Attacker == null || ev.Attacker != Actors.Me || ev.Victim != target) return;
            RegisterHit(ev.Power, ev.Dir, bag != null && bag.Returning);
        }
        /// 타격 하나 집계(시험에서 직접 부름)
        public void RegisterHit(Power p, Vector3 dir, bool returning)
        {
            double t = R.T;
            Hits++;
            float power = PowerOf(p, pendingHold, Math.Abs(pendingRhythm) <= Beat.Window * 1000.0, returning);
            Log.Add("hit", t, 1, R.Round); hitsThisRound++;
            Log.Add("rhythm", t, pendingRhythm, R.Round);
            Log.Add("power", t, power, R.Round);
            lastHitT = t; guardPending = true;
            if (bag != null) bag.Hit(dir, power);
            if (returning) R.Toast("팡!");
        }

        public override void End(bool aborted)
        {
            Fighter.AnyHit -= OnHit;
            if (Actors.Me != null) Actors.Me.CancelAttack();
            Debug.Log($"[Gym] 샌드백 진단: 누름 {Presses} · 기술 {Attacks} · 몸 바빠 건너뜀 {SkippedBusy}({LastSkip}) · 타격 {Hits} · 대상 {(target != null ? target.Label + " " + target.State : "없음")} · 시우 {(Actors.Me != null ? Actors.Me.State.ToString() : "없음")}");
        }

        int botNext; double botGuardAt = -1;
        public override void Bot(double t, float skill)
        {
            if (t < 0.001) { botNext = 0; botGuardAt = -1; }
            if (botGuardAt >= 0 && t >= botGuardAt) { R.InjectGuard(true); if (t >= botGuardAt + 0.2) { R.InjectGuard(false); botGuardAt = -1; } }
            // 매 박자(BPM 100 = 0.6초, 기술 길이 ~0.35초) → 라운드 ~100회(S 기준 50 = 현실 150/3분을 게임 60초로 환산)
            while (true)
            {
                int beat = botNext;
                double bt = Beat.TimeOf(beat);
                if (bt > Def.RoundSec - 0.4) return;
                double err = Jitter(botNext + R.Round * 1000, skill, 160) / 1000.0;
                if (t < bt + err) return;
                bool heavy = botNext % 3 == 2;
                R.Inject(heavy ? DrillBtn.Heavy : DrillBtn.Light, bt + err, heavy && botNext % 2 == 0);
                botGuardAt = bt + err + 0.45 + (1.0 - skill) * 0.9;
                botNext++;
            }
        }
    }

    /// D06 스피드백: 좌-좌-우-우(□□△△) 리듬 BPM 150 → 170 → 190, 틀리면 백이 튕겨 0.6초 멈춤
    public sealed class SpeedBagStation : DrillStation
    {
        float freeze; double lastBeatT = -9, freezeUntil = -1;
        public override string Hint => "□ □ △ △ 교대 — 박자에";
        public override void RoundStart(int round)
        {
            Beat = new BeatTrack(round == 1 ? 150 : round == 2 ? 170 : 190, 1.0) { Window = BeatTrack.WindowFor(R.Speed) };
            Beat.MarkOf = b => (b / 2) % 2 == 0 ? 0 : 1;    // 0 = 왼(□) · 1 = 오른(△)
            Beat.Reset(); freeze = 0f; freezeUntil = -1;
            if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance";
        }
        public override void Tick(float dt, double t, in DrillInput inp)
        {
            if (freeze > 0f) freeze -= dt;
            foreach (var b in Beat.Missed(t, Def.RoundSec)) { if (Beat.TimeOf(b) < freezeUntil) continue; Log.Add("sb", t, 0, R.Round); freeze = 0.6f; freezeUntil = t + 0.6; }
            if (Actors.SpeedBag != null)
            {
                double since = t - lastBeatT;
                float sw = freeze > 0f ? Mathf.Sin((float)t * 40f) * 25f : (float)Math.Max(0, 1 - since / Beat.Interval) * 18f;
                Actors.SpeedBag.localRotation = Quaternion.Euler(sw, 0f, 0f);
            }
        }
        public override void Press(DrillBtn b, bool hold, bool stickDown, double t)
        {
            if (b != DrillBtn.Light && b != DrillBtn.Heavy) return;
            if (freeze > 0f) return;
            int beat = Beat.Judge(t, out double err);
            lastBeatT = Beat.TimeOf(beat);
            bool want = Beat.Mark(beat) == 1 ? b == DrillBtn.Heavy : b == DrillBtn.Light;
            bool ok = want && Math.Abs(err) <= Beat.Window * 2.0;
            Log.Add("sb", t, ok ? 1 : 0, R.Round);
            if (ok) Log.Add("beat_err", t, err * 1000.0, R.Round);
            else { freeze = 0.6f; freezeUntil = t + 0.6; R.Toast("튕김"); }
            var me = Actors.Me;
            if (me != null && ok) { me.ScriptAnim = null; me.StartAttack(b == DrillBtn.Light ? Actors.Moves.Jab : Actors.Moves.Cross, null); }
        }
        public override void RoundEnd(int round) { if (Actors.Me != null) Actors.Me.ScriptAnim = "Stance"; }
        int botNext;
        public override void Bot(double t, float skill)
        {
            if (t < 0.001) botNext = 0;
            while (true)
            {
                double bt = Beat.TimeOf(botNext);
                if (bt > Def.RoundSec - 0.01) return;
                double err = Jitter(botNext + R.Round * 1000, skill, 90) / 1000.0;
                if (t < bt + err) return;
                if (!Flub(botNext + R.Round * 1000, skill, 0.2)) R.Inject(Beat.Mark(botNext) == 1 ? DrillBtn.Heavy : DrillBtn.Light, bt + err);
                botNext++;
            }
        }
    }
}
