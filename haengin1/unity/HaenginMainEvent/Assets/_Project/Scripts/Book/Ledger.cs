// 행인1의 메인이벤트 — 가계부(docs/09_M3_버티컬슬라이스_설계.md 2-4): "장부는 없다. 머릿속에 있다."(2화) — 시우 머릿속 숫자.
// 지갑 · '하늘 케이크' 봉투(0 / 40,000 — 버릴 수 없는 퀘스트 아이템) · 움직인 돈 기록 · 회차 줄 · 아이템.
// 바꾸는 말(StoryRunner.Apply 가 넘김): "ledger:+8000:배달비 → 엄마 통장"(번 돈 — 지갑은 그대로, 엄마 통장으로) ·
// "wallet:-200:매점" · "wallet=2600" · "envelope:+5000" · "spend:38000:2호 초코 생크림" · "item:폐기 직전 삼각김밥:2" · "entry:글"(돈 없이 한 줄)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Haengin
{
    [Serializable]
    public sealed class Ledger
    {
        public const int EnvelopeGoal = 40000;

        public int Wallet = 2600;
        /// 봉투에 모은 돈(쓴 돈은 따로 — 회차 줄 '40,000 / 40,000' 을 쓴 뒤에도 셈)
        public int Envelope;
        public int EnvelopeAdds;
        public int Spent;
        /// 번 돈 합계(엄마 통장·고지서 밑으로 간 것 포함)
        public int Earned;
        public List<string> Entries = new List<string>();
        public List<string> Items = new List<string>();

        public static string Won(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
        public static string Signed(int v) => (v >= 0 ? "+" : "-") + Won(Math.Abs(v));   // 붓 글꼴(Black Han Sans)에 − (U+2212)가 없어 ASCII

        public string EnvelopeText => $"하늘 케이크 {Won(Envelope)} / {Won(EnvelopeGoal)}";

        /// 명령 하나를 적용하고 화면에 띄울 한 줄을 돌려준다(없으면 null)
        public string Apply(string verb, string arg)
        {
            var p = arg.Split(new[] { ':' }, 2);
            switch (verb)
            {
                case "ledger":
                    {
                        int v = ParseInt(p[0]);
                        Earned += v;
                        string what = p.Length > 1 ? p[1] : "";
                        string line = $"{Signed(v)} {what}".Trim();
                        Entries.Add(line);
                        return line;
                    }
                case "wallet":
                    {
                        int v = ParseInt(p[0]);
                        Wallet += v;
                        string line = $"{Signed(v)} {(p.Length > 1 ? p[1] : "")} · 지갑 {Won(Wallet)}원".Replace("  ", " ");
                        Entries.Add(line);
                        return line;
                    }
                case "envelope":
                    {
                        int v = ParseInt(p[0]);
                        Envelope += v;
                        EnvelopeAdds++;
                        string line = $"봉투 {Signed(v)} · {EnvelopeText}";
                        Entries.Add(line);
                        return line;
                    }
                case "spend":
                    {
                        int v = ParseInt(p[0]);
                        Spent += v;
                        string line = $"{Signed(-v)} {(p.Length > 1 ? p[1] : "")} (봉투)".Trim();
                        Entries.Add(line);
                        return line;
                    }
                case "item":
                    {
                        int n = p.Length > 1 ? ParseInt(p[1]) : 1;
                        for (int i = 0; i < n; i++) Items.Add(p[0]);
                        return $"얻음: {p[0]}{(n > 1 ? " ×" + n : "")}";
                    }
                case "entry":
                    Entries.Add(arg);
                    return arg;
            }
            return null;
        }

        public void SetWallet(int v) => Wallet = v;

        public static int ParseInt(string s)
        {
            s = s.Replace(",", "").Replace("+", "").Trim();
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        public string Summary => $"지갑 {Won(Wallet)} · 봉투 {Won(Envelope)}({EnvelopeAdds}번) · 쓴 돈 {Won(Spent)} · 번 돈 {Won(Earned)} · 아이템 {Items.Count}";
    }
}
