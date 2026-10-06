// 행인1의 메인이벤트 — '불린 이름' 도감(docs/09_M3_버티컬슬라이스_설계.md 2-5, 부록 A-2)
// 칸 9개(3×3) + 덤 칸. 칸마다 호칭·부른 사람·장면·색·설명 한 줄. 마지막 칸 「반시우」는 물음표로 잠김(23화).
// 색: 회색 = 이름 대신 불린 것 · 따뜻한 색 = 들어도 이가 안 갈리는 것 · 먹 테 = 놀림 · 잠김 · 덤(개그)
// 목록(칸 정의)은 StoryDef.Names(데이터 m3_story.json "names"), 등록 상태(Got)는 저장 파일에.
using System;
using System.Collections.Generic;

namespace Haengin
{
    public enum NameTone { Grey, Warm, Mock, Locked, Bonus, Empty }

    [Serializable]
    public sealed class NameEntry
    {
        /// 1~9 = 3×3 칸, 0 = 덤 칸
        public int Slot;
        public string Name = "", Caller = "", Scene = "", Note = "";
        public NameTone Tone;
    }

    [Serializable]
    public sealed class NameBook
    {
        /// 등록한 호칭(등록 순서)
        public List<string> Got = new List<string>();

        public bool Has(string name) => Got.Contains(name);

        /// 등록(같은 호칭은 다시 안 함 — false). 잠긴 칸은 등록되지 않는다
        public bool Register(string name, NameEntry[] catalog)
        {
            if (string.IsNullOrEmpty(name) || Got.Contains(name)) return false;
            var e = Array.Find(catalog ?? new NameEntry[0], x => x.Name == name);
            if (e == null || e.Tone == NameTone.Locked || e.Tone == NameTone.Empty) return false;
            Got.Add(name);
            return true;
        }

        /// 9칸 중 채운 칸(덤 빼고)
        public int Filled(NameEntry[] catalog)
        {
            int n = 0;
            foreach (var g in Got) { var e = Array.Find(catalog ?? new NameEntry[0], x => x.Name == g); if (e != null && e.Slot > 0) n++; }
            return n;
        }

        public static string ToneName(NameTone t) => t switch
        {
            NameTone.Warm => "따뜻한 색", NameTone.Mock => "먹 테(놀림)", NameTone.Locked => "잠김", NameTone.Bonus => "덤", NameTone.Empty => "빈칸", _ => "회색",
        };
    }
}
