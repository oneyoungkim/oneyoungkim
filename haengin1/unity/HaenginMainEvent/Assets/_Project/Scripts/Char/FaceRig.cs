// 행인1의 메인이벤트 — 얼굴 데칼 표정(docs/09_M3_버티컬슬라이스_설계.md 4-2 #4, FUJIMOTO 3-4·7-2)
// 얼굴 데칼 메시(Face_LOD0~2, tools/formal_model.py)에 표정 아틀라스(<이름>Face.png, tools/face_tex.py)의 칸을 끼운다.
//   아틀라스 2048² · 칸 409px · 5×5: 줄(위→아래) = 표정 normal · hurt · ko · win · endure(시우 '참기' = 이 악문 무표정)
//                                    칸(왼→오른) = open · half · closed(깜빡임) · talkA · talkB(말하기 입 2장)
// 칸 바꾸기 = MaterialPropertyBlock 의 _MainTex_ST(UTS) — 재질은 인물마다 하나, 렌더러마다 칸이 다르다. 바뀌는 데 같은 프레임(D14 ≤ 0.08초).
// 깜빡임: 2.5~5.5초마다 반쯤(0.04) → 감음(0.06) → 반쯤(0.04). 말하기(Talk): talkA 0.12 → talkB 0.10 → open 0.08 반복. 깜빡임이 말하기보다 먼저.
using UnityEngine;

namespace Haengin
{
    [DefaultExecutionOrder(18), DisallowMultipleComponent]
    public sealed class FaceRig : MonoBehaviour
    {
        public static readonly string[] ExprNames = { "normal", "hurt", "ko", "win", "endure" };
        public static readonly string[] StateNames = { "open", "half", "closed", "talkA", "talkB" };

        public Renderer[] Faces;
        public int Cells = 5, CellPx = 409, AtlasPx = 2048;
        [Tooltip("지금 표정(normal · hurt · ko · win · endure)")] public string Expr = "normal";
        [Tooltip("말하는 중(입 2장 번갈아)")] public bool Talk;
        public bool Blink = true;
        public float BlinkMin = 2.5f, BlinkMax = 5.5f;
        [Tooltip("깜빡임 순서를 정하는 씨앗(인물마다 다르게)")] public int Seed = 7;

        MaterialPropertyBlock mpb;
        System.Random rng;
        float blinkIn, blinkT = -1f, talkT;
        int row = -1, col = -1;

        /// 지금 끼운 칸("normal/open")
        public string Cell => row < 0 ? "" : $"{ExprNames[row]}/{StateNames[col]}";
        /// 칸이 바뀐 횟수 · 마지막으로 바뀐 시각(게임 시간)
        public int Switches { get; private set; }
        public float LastSwitch { get; private set; }

        static readonly int StMain = Shader.PropertyToID("_MainTex_ST"), StBase = Shader.PropertyToID("_BaseMap_ST");

        void Awake()
        {
            rng = new System.Random(Seed);
            blinkIn = Next();
            Apply(true);
        }

        void OnEnable() { if (mpb != null) Apply(true); }

        float Next() => BlinkMin + (float)rng.NextDouble() * (BlinkMax - BlinkMin);

        /// 표정 이름 → 줄. 한국어 이름도 받는다(컷신 표정 트랙: 기본·아픔·기절·승리·참기)
        public static int ExprIndex(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "normal": case "기본": case "평소": case "무표정": return 0;
                case "hurt": case "아픔": case "찡그림": case "아파": return 1;
                case "ko": case "기절": case "넉아웃": return 2;
                case "win": case "승리": case "웃음": case "비웃음": return 3;
                case "endure": case "참기": case "이악물기": return 4;
                default: return -1;
            }
        }

        /// 표정 바꾸기(같은 프레임에 칸이 바뀐다). 모르는 이름이면 그대로 두고 false
        public bool Set(string expr)
        {
            int i = ExprIndex(expr);
            if (i < 0) return false;
            Expr = ExprNames[i];
            Apply(false, Mathf.Max(col, 0));
            return true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            int state = 0;
            if (Blink)
            {
                if (blinkT < 0f) { blinkIn -= dt; if (blinkIn <= 0f) blinkT = 0f; }
                if (blinkT >= 0f)
                {
                    blinkT += dt;
                    if (blinkT < 0.04f) state = 1;
                    else if (blinkT < 0.10f) state = 2;
                    else if (blinkT < 0.14f) state = 1;
                    else { blinkT = -1f; blinkIn = Next(); }
                }
            }
            if (state == 0 && Talk)
            {
                talkT = (talkT + dt) % 0.30f;
                state = talkT < 0.12f ? 3 : talkT < 0.22f ? 4 : 0;
            }
            else if (!Talk) talkT = 0f;
            Apply(false, state);
        }

        /// 깜빡임을 지금 시작(테스트·연출)
        public void BlinkNow() { blinkT = 0f; }

        void Apply(bool force, int state = 0)
        {
            int r = Mathf.Max(0, ExprIndex(Expr));
            if (!force && r == row && state == col) return;
            if (row >= 0 && (r != row || state != col)) { Switches++; LastSwitch = Time.time; }
            row = r; col = state;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            float s = CellPx / (float)AtlasPx;
            var st = new Vector4(s, s, col * s, 1f - (row + 1) * s);
            if (Faces == null) return;
            foreach (var f in Faces)
            {
                if (f == null) continue;
                f.GetPropertyBlock(mpb);
                mpb.SetVector(StMain, st);
                mpb.SetVector(StBase, st);
                f.SetPropertyBlock(mpb);
            }
        }

        /// 칸 → 텍스처 ST(테스트용)
        public Vector4 CellST(int r, int c)
        {
            float s = CellPx / (float)AtlasPx;
            return new Vector4(s, s, c * s, 1f - (r + 1) * s);
        }

        /// 렌더러에 지금 들어간 ST(테스트용)
        public Vector4 CurrentST(Renderer f)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            f.GetPropertyBlock(mpb);
            return mpb.GetVector(StMain);
        }
    }
}
