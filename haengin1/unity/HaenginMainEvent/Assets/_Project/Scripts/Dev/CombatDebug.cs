// 행인1의 메인이벤트 — 전투 연습장 디버그 키 (docs/08_M2_전투_설계.md 1-2: F2 적 다시 · F3 기세 MAX · F4 시우 무적 · F7 판정 보기, 개발용)
// F5 = 적 셋 소환(깐족이·석 달·냉장고 정식 모델 + AI + 공격권, 1:3) · F6 = 소환한 적 지우기.
// F7 판정 보기: 공격 중인 사람의 부채꼴(사거리·반각)을 선으로 — 발생 중 회색, 판정 중 빨강.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Haengin
{
    public sealed class CombatDebug : MonoBehaviour
    {
        public PlayerCombat Player;
        public CombatTuning Tuning;
        [Tooltip("F5 로 소환할 적 정의(깐족이·석 달·냉장고)")] public EnemyDef[] Defs = new EnemyDef[0];
        [Tooltip("같은 순서의 적 모델 프리팹(Enemy_*.prefab)")] public GameObject[] Models = new GameObject[0];
        public static bool ShowFans;

        struct Home { public Fighter F; public Vector3 Pos; public float Yaw; }
        readonly List<Home> homes = new List<Home>();
        readonly List<EnemyBrain> spawned = new List<EnemyBrain>();
        AttackDirector director;
        Material lineMat;

        public IReadOnlyList<EnemyBrain> Spawned => spawned;
        public AttackDirector Director => director;

        void Start()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            foreach (var f in Fighter.All) if (!f.IsPlayer) homes.Add(new Home { F = f, Pos = f.Position, Yaw = f.Yaw });
        }

        void OnEnable() => RenderPipelineManager.endCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

        /// 적 셋 소환(시우 앞 5m 부채꼴) — 녹화·시험도 부름
        public void Spawn()
        {
            Clear();
            if (Player == null || Player.Me == null) return;
            if (director == null) director = CombatFactory.Director(Player.Me, Tuning);
            var p = Player.Me.Position;
            float yaw = Player.Me.Yaw;
            for (int i = 0; i < Defs.Length; i++)
            {
                if (Defs[i] == null) continue;
                var pos = p + HitResolver.YawDir(yaw - 35f + 35f * i) * 5f;
                var model = i < Models.Length && Models[i] != null ? Instantiate(Models[i]) : null;
                var b = CombatFactory.Enemy(Defs[i], pos, HitResolver.Yaw(p - pos), Player.Me, director, i, Tuning, model);
                spawned.Add(b);
                b.Activate();
            }
            DebugHud.Toast($"F5 적 {spawned.Count}명 소환");
        }

        public void Clear()
        {
            foreach (var b in spawned) if (b != null) Destroy(b.gameObject);
            spawned.Clear();
            if (director != null) director.Members.Clear();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.f2Key.wasPressedThisFrame)
            {
                foreach (var h in homes)
                {
                    if (h.F == null) continue;
                    h.F.ResetFighter();
                    h.F.Body?.Place(h.Pos, h.Yaw);
                }
                DebugHud.Toast("F2 적 다시");
            }
            if (k.f3Key.wasPressedThisFrame && Player != null) { Player.Heat.Set(HeatGauge.Max); DebugHud.Toast("F3 기세 MAX"); }
            if (k.f4Key.wasPressedThisFrame && Player != null && Player.Me != null)
            {
                Player.Me.DebugInvuln = !Player.Me.DebugInvuln;
                DebugHud.Toast(Player.Me.DebugInvuln ? "F4 시우 무적 켬" : "F4 시우 무적 끔");
            }
            if (k.f5Key.wasPressedThisFrame) Spawn();
            if (k.f6Key.wasPressedThisFrame) { Clear(); DebugHud.Toast("F6 소환한 적 지움"); }
            if (k.f7Key.wasPressedThisFrame) { ShowFans = !ShowFans; DebugHud.Toast(ShowFans ? "F7 판정 보기 켬" : "F7 판정 보기 끔"); }
        }

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (!ShowFans || cam != Camera.main) return;
            if (lineMat == null)
            {
                var sh = Shader.Find("Hidden/Internal-Colored");
                if (sh == null) return;
                lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                lineMat.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            lineMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            foreach (var f in Fighter.All)
            {
                var r = f.Run;
                if (r == null || r.Move == null) continue;
                GL.Color(r.ActiveNow ? Color.red : new Color(0.4f, 0.4f, 0.4f));
                var o = f.Position + Vector3.up * 0.9f;
                float reach = r.Move.Range + 0.3f;
                Vector3 prev = o;
                for (int i = -8; i <= 8; i++)
                {
                    var p = o + HitResolver.YawDir(f.Yaw + r.Move.HalfAngle * i / 8f) * reach;
                    GL.Vertex(prev); GL.Vertex(p);
                    prev = p;
                }
                GL.Vertex(prev); GL.Vertex(o);
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
