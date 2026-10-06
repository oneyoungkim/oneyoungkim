// 행인1의 메인이벤트 — 컷신 문맥(docs/09_M3_버티컬슬라이스_설계.md 2-2 '컷신'): 타임라인 트랙들이 부르는 곳
// 트랙은 장면 오브젝트에 묶지 않고(무대는 Additive 로 열리고 닫혀 바인딩이 깨짐) 이 정적 문맥을 거친다.
// 배우 = 이름으로 찾음: "시우" = 플레이어 · 그 밖 = 지금 무대(StagePlace) 아래 같은 이름 오브젝트 · 없으면 장면 전체에서.
// 건너뛰기(D05): 아직 안 끝난 클립 중 ICutEnd 인 것의 ApplyEnd 를 시간 순서대로 부른다. 같은 클립 효과는 한 번만(applied).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Haengin
{
    /// 건너뛸 때 끝 상태를 남기는 클립
    public interface ICutEnd { void ApplyEnd(); }

    public static class CutCtx
    {
        public static SubtitleUi Sub;
        public static InnerVoiceUi Inner;
        public static StoryHud Hud;
        /// 이야기 명령(가계부·도감·플래그 — StoryRunner.ApplyNow)
        public static Action<string> ApplyNow;
        public static Transform Player;

        /// 일어난 순서(D05·D06): "자막+:화자:글" · "자막-" · "표정:배우:표정" · "손:배우:자세" · "소품:이름:자리:붙임" · "UI:명령" · "소리:이름" · "시간:배율" · "카메라:프리셋:대상"
        public static readonly List<string> Log = new List<string>();
        public static readonly Dictionary<string, string> Faces = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> Props = new Dictionary<string, string>();
        public static int Sfx;
        /// 자막 클립이 실제로 보인 시간(초) — D06
        public static readonly List<(string text, float secs)> SubTimes = new List<(string, float)>();

        static readonly HashSet<object> applied = new HashSet<object>();
        static readonly Dictionary<object, float> subStart = new Dictionary<object, float>();
        public static float Clock;

        public static void Begin()
        {
            applied.Clear(); subStart.Clear();
            Log.Clear(); SubTimes.Clear();
            Clock = 0f;
        }

        public static Transform Actor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name == "시우") return Player;
            foreach (var p in StagePlace.All)
            {
                if (p == null) continue;
                var t = FindDeep(p.transform, name);
                if (t != null) return t;
            }
            var g = GameObject.Find(name);
            return g != null ? g.transform : null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public static bool Once(object clip) => applied.Add(clip);

        // ───────────────────────── 트랙이 부르는 것
        public static void Subtitle(object clip, string who, string text, DlgMode mode, bool on)
        {
            if (on)
            {
                Log.Add($"자막+:{who}:{text}");
                subStart[clip] = Clock;
                if (mode == DlgMode.Inner) { if (Inner != null) Inner.Show(text.Replace("\\n", "\n")); }
                else if (mode == DlgMode.Screen) { if (Hud != null) Hud.Screen(text.Replace("\\n", "\n"), 0f); }
                else if (Sub != null) Sub.Show(who, text.Replace("\\n", "\n"));
            }
            else
            {
                Log.Add("자막-");
                if (subStart.TryGetValue(clip, out float t0)) { SubTimes.Add((text, Clock - t0)); subStart.Remove(clip); }
                if (mode == DlgMode.Inner) { if (Inner != null) Inner.Hide(); }
                else if (mode == DlgMode.Screen) { if (Hud != null) Hud.Screen(null, 0f); }
                else if (Sub != null) Sub.Hide();
            }
        }

        public static void Face(string who, string face)
        {
            Faces[who ?? ""] = face;
            Log.Add($"표정:{who}:{face}");
        }

        public static void Hand(string who, string pose, float weight)
        {
            Log.Add($"손:{who}:{pose}");
            var a = Actor(who);
            var hs = a != null ? a.GetComponentInChildren<HandShape>() : null;
            if (hs != null) hs.ForceFist = pose == "주먹" ? 100f * weight : -1f;
        }

        public static void Prop(string prop, string socket, bool attach)
        {
            Log.Add($"소품:{prop}:{socket}:{(attach ? "붙임" : "뗌")}");
            Props[prop ?? ""] = attach ? socket : "";
            var p = Actor(prop);
            if (p == null) return;
            // 다른 장면(무대 ↔ Zone1 플레이어)끼리 부모를 바꾸면 무대를 내려도 소품이 남는다 → 따라가기만 한다(PropFollow)
            var f = p.GetComponent<PropFollow>() ?? p.gameObject.AddComponent<PropFollow>();
            if (attach) { f.Target = Socket(socket); p.gameObject.SetActive(true); }
            else { f.Target = null; p.gameObject.SetActive(false); }
        }

        /// 소켓 "배우/자리"(예: 시우/오른손) — 정식 모델 전에는 배우 루트 위 높이로 대신
        static Transform Socket(string socket)
        {
            if (string.IsNullOrEmpty(socket)) return null;
            var parts = socket.Split('/');
            var a = Actor(parts[0]);
            if (a == null || parts.Length < 2) return a;
            return FindDeep(a, parts[1]) ?? a;
        }

        public static void Ui(object clip, string action)
        {
            if (!Once(clip)) return;
            Log.Add($"UI:{action}");
            ApplyNow?.Invoke(action);
        }

        public static void SfxPlay(string name)
        {
            Sfx++;
            Log.Add($"소리:{name}");
        }

        public static void Time(float scale, float secs)
        {
            Log.Add($"시간:{scale:F2}");
            TimeFx.Slow(secs, scale);
        }

        public static void End()
        {
            foreach (var kv in subStart) SubTimes.Add(("(끊김)", Clock - kv.Value));
            subStart.Clear();
            if (Sub != null) Sub.Hide(); if (Inner != null) Inner.Hide(); if (Hud != null) Hud.Screen(null, 0f);
            CutCams.ReleaseAll();
            InkMode.Set(0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Sub = null; Inner = null; Hud = null; ApplyNow = null; Player = null;
            Log.Clear(); Faces.Clear(); Props.Clear(); SubTimes.Clear(); applied.Clear(); subStart.Clear(); Sfx = 0; Clock = 0f;
        }
    }
}
