// 행인1의 메인이벤트 — 맵 아트 패스 6차 간판 점검(2026-10-25, docs/09 6-5-3 6차)
//   S01 Sign_Glyphs : Zone1 장면의 TMP 글자 전수 — 글꼴(대체 글꼴 포함)에 없는 글자 0개(창 시트지 '명륜 !동산' 같은 빠짐 방지) + 구운 글자 메시가 있음
//   S02 Sign_Layout : 간판 면·창 시트지끼리 겹침 0 · 키트 건물 부재(발코니·실외기·창틀 = *_LOD0)가 면 앞 1.6m 안에서 15% 넘게 가리는 면 0
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haengin.Tests
{
    public sealed class SignTextTests
    {
        [UnityTearDown] public IEnumerator TearDown() => M3Lab.TearDown();

        [UnityTest, Timeout(600000)]
        public IEnumerator S01_Sign_Glyphs()
        {
            yield return M3Lab.OpenZone1();
            yield return null;
            int miss = SignAudit.MissingGlyphs(out int texts, out string detail);
            Debug.Log($"[S01] 간판 글자: TMP {texts}개 · 빠진 글자 {miss}{(miss > 0 ? " — " + detail : "")}");
            Assert.Greater(texts, 100, "Zone1 TMP 글자가 너무 적음(간판·시트지·주소판이 빠졌나)");
            Assert.AreEqual(0, miss, "빠진 글자: " + detail);
            var baked = GameObject.Find("Zone1/Env/글자 합침");
            Assert.NotNull(baked, "구운 글자 메시 묶음(Zone1/Env/글자 합침) 없음");
            int meshes = baked.GetComponentsInChildren<MeshFilter>().Count(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0);
            Assert.Greater(meshes, 0, "구운 글자 메시가 비었음");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator S02_Sign_Layout()
        {
            yield return M3Lab.OpenZone1();
            yield return null;
            var env = GameObject.Find("Zone1/Env");
            Assert.NotNull(env, "Zone1/Env 없음");
            var faces = SignAudit.Faces(env.transform);
            var ov = SignAudit.Overlaps(faces);
            var oc = SignAudit.Occlusions(env.transform, faces, 0.15f, out int checkedN);
            Debug.Log($"[S02] 간판 면 {faces.Count}(시트지 {faces.Count(f => f.Sheet)}) · 겹침 {ov.Count} · 가림 {oc.Count}/{checkedN}" +
                      (ov.Count + oc.Count > 0 ? " — " + string.Join(" / ", ov.Concat(oc).Take(8)) : ""));
            Assert.Greater(faces.Count, 40, "간판 면이 너무 적음");
            Assert.Greater(faces.Count(f => f.Sheet), 5, "창 시트지가 너무 적음");
            Assert.IsEmpty(ov, "겹침: " + string.Join(" / ", ov.Take(8)));
            Assert.IsEmpty(oc, "가림: " + string.Join(" / ", oc.Take(8)));
        }
    }
}
