// 행인1의 메인이벤트 — 박자 띠(docs/12_체육관_설계.md 4-2 D01·D02·D05·D06·D10): BPM 박자열 + 입력 ↔ 가장 가까운 박자 오차(ms)
// 스탯 보정(4-3)은 '허용 창'에만: 기본 ±60ms → 스피드 100 에서 ±110ms. 산식(오차 값)은 스탯과 무관.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    public sealed class BeatTrack
    {
        public double Bpm;
        public double Interval => 60.0 / Bpm;
        /// 라운드 시작 뒤 첫 박자까지(초)
        public double Lead = 1.0;
        /// 허용 창(초) — 이 안이면 '맞음', 밖이면 걸림/실수
        public double Window = 0.060;
        /// 박자마다 표시(더블언더 △ · 스피드백 좌우)
        public Func<int, int> MarkOf;
        /// 이미 입력을 받은 박자 번호
        readonly HashSet<int> taken = new HashSet<int>();
        /// 지나갔는데 입력이 없던 박자 번호(걸림 검사)
        int lastChecked = -1;

        public BeatTrack(double bpm, double lead = 1.0) { Bpm = bpm; Lead = lead; }

        public static double WindowFor(int speedStat) => 0.060 + 0.050 * Mathf.Clamp01(speedStat / 100f);

        public double TimeOf(int beat) => Lead + beat * Interval;
        public int BeatAt(double t) => (int)Math.Floor((t - Lead) / Interval + 0.5);
        public int Mark(int beat) => MarkOf != null ? MarkOf(beat) : 0;
        public bool Taken(int beat) => taken.Contains(beat);
        public void Reset() { taken.Clear(); lastChecked = -1; }

        /// 입력 t → 가장 가까운(아직 안 받은) 박자 번호와 오차(초, 입력 − 박자). 그 박자가 이미 받아졌으면 다음 박자로 본다
        public int Judge(double t, out double err)
        {
            int b = Math.Max(0, BeatAt(t));
            if (taken.Contains(b)) b++;
            err = t - TimeOf(b);
            taken.Add(b);
            return b;
        }

        /// 입력 없이 지나간 박자들(창 닫힘) — 돌려주는 것 = 새로 놓친 박자 번호들
        public List<int> Missed(double now, double roundLen)
        {
            var l = new List<int>();
            int upTo = (int)Math.Floor((now - Lead - Window) / Interval);
            for (int b = lastChecked + 1; b <= upTo; b++)
            {
                if (TimeOf(b) > roundLen) break;
                if (!taken.Contains(b)) l.Add(b);
            }
            lastChecked = Math.Max(lastChecked, upTo);
            return l;
        }

        /// 박자 표시용: now 기준 앞뒤 창(초) 안 박자들
        public void Visible(double now, double before, double after, List<(int beat, double t)> into, double roundLen)
        {
            into.Clear();
            int b0 = Math.Max(0, (int)Math.Floor((now - before - Lead) / Interval));
            for (int b = b0; ; b++)
            {
                double t = TimeOf(b);
                if (t > now + after || t > roundLen) break;
                if (t < now - before) continue;
                into.Add((b, t));
            }
        }
    }
}
