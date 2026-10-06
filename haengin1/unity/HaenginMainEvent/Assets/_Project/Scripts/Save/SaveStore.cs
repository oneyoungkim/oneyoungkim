// 행인1의 메인이벤트 — 저장 파일(docs/09_M3_버티컬슬라이스_설계.md 2-10)
// 슬롯 = 자동(save_auto.json) + 수동 1(save_1.json). 위치 = Application.persistentDataPath(회사·제품 이름 폴더 — 한글 제품명 경로, D11).
// 안전: 임시 파일(.tmp)에 다 쓴 뒤 바꿔 넣는다(File.Replace — 이전 파일은 .prev.json 으로 남김). 쓰다 꺼지면 이전 파일이 그대로 남는다.
// 깨진 파일·모르는 버전 → Read 가 오류 글을 돌려주고, 타이틀이 경고 후 '새 게임' 또는 '이전 자동 저장'(.prev)을 고르게 한다.
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Haengin
{
    public static class SaveStore
    {
        public const string Auto = "auto", Manual = "1";

        /// 저장 폴더(테스트는 바꿔 끼움)
        public static string Dir = null;
        public static string Folder => string.IsNullOrEmpty(Dir) ? Application.persistentDataPath : Dir;
        /// 테스트: 임시 파일까지 쓰고 바꿔 넣기 전에 끊긴 것처럼(예외)
        public static bool FailBeforeSwap;
        public static int Writes { get; private set; }

        public static string PathOf(string slot) => Path.Combine(Folder, $"save_{slot}.json");
        public static string PrevOf(string slot) => Path.Combine(Folder, $"save_{slot}.prev.json");

        public static bool Exists(string slot) => File.Exists(PathOf(slot));

        public static void Write(string slot, SaveData d)
        {
            Directory.CreateDirectory(Folder);
            d.Version = SaveData.CurrentVersion;
            d.SavedAt = DateTime.Now.ToString("o");      // 소수 초까지(같은 초에 쓴 자동·수동 칸도 새것을 가림)
            string dst = PathOf(slot), tmp = dst + ".tmp", prev = PrevOf(slot);
            File.WriteAllText(tmp, d.ToJson(), new UTF8Encoding(false));
            if (FailBeforeSwap) throw new IOException("시험: 바꿔 넣기 전에 끊김");
            if (File.Exists(dst))
            {
                if (File.Exists(prev)) File.Delete(prev);
                File.Replace(tmp, dst, prev);
            }
            else File.Move(tmp, dst);
            Writes++;
        }

        /// 읽기. 실패하면 null + 오류 글("없음" · "깨짐: …" · "모르는 버전 N")
        public static SaveData Read(string slot, out string error) => ReadFile(PathOf(slot), out error);

        public static SaveData ReadPrev(string slot, out string error) => ReadFile(PrevOf(slot), out error);

        public static SaveData ReadFile(string path, out string error)
        {
            error = null;
            if (!File.Exists(path)) { error = "없음"; return null; }
            SaveData d;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) { error = "깨짐: 내용이 JSON 이 아님"; return null; }
                d = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e) { error = "깨짐: " + e.GetType().Name; return null; }
            if (d == null || string.IsNullOrEmpty(d.SceneId)) { error = "깨짐: 장면 id 없음"; return null; }
            if (d.Version != SaveData.CurrentVersion) { error = $"모르는 버전 {d.Version}"; return null; }
            if (d.Ledger == null) d.Ledger = new Ledger();
            if (d.Names == null) d.Names = new NameBook();
            if (d.Flags == null) d.Flags = new StoryFlags();
            return d;
        }

        public static void Delete(string slot)
        {
            foreach (var p in new[] { PathOf(slot), PrevOf(slot), PathOf(slot) + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Dir = null; FailBeforeSwap = false; Writes = 0; }
    }
}
