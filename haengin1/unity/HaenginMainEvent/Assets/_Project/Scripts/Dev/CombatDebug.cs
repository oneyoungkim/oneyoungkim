// 행인1의 메인이벤트 — 전투 연습장 디버그 키 (docs/08_M2_전투_설계.md 1-2: F2 적 다시 · F3 기세 MAX · F4 시우 무적 · F7 판정 보기, 개발용)
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
        public static bool ShowFans;

        struct Home { public Fighter F; public Vector3 Pos; public float Yaw; }
        readonly List<Home> homes = new List<Home>();
        Material lineMat;

        void Start()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCombat>();
            foreach (var f in Fighter.All) if (!f.IsPlayer) homes.Add(new Home { F = f, Pos = f.Position, Yaw = f.Yaw });
        }

        void OnEnable() => RenderPipelineManager.endCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

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
