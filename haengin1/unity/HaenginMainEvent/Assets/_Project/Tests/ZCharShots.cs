// 행인1의 메인이벤트 — 6·7단계 눈 확인(-s6shots <폴더>, 그래픽 필요): 정식 모델 근접(표정 5칸 × 3벌) · 손 자세 8종(시우 오른손) · 줄 세움(정면·옆)
//   s6_face_<누구>_<표정>.png · s6_hand_<자세>.png · s6_lineup_front.png · s6_lineup_side.png · notes_s6.txt
// 전투 연습장(CombatLab — 조명·바닥이 단순)을 빌려 찍는다. 애니메이터를 멈추고 HandPoser.ApplyNow · FaceRig.Set 으로 칸을 바꿔 같은 프레임에 찍음.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Haengin.Tests
{
    public sealed class ZCharShots
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            yield return Lab.UnloadOurs(default);
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Z_CharShots()
        {
            string dir = Shots.Arg("-s6shots");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("-s6shots <폴더> 를 줄 때만"); yield break; }
            if (!Shots.Graphics) { Assert.Ignore("그래픽 장치 없음"); yield break; }
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            Directory.CreateDirectory(dir);
            Time.captureDeltaTime = Lab.Dt;
            yield return Lab.UnloadOurs(default);
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ZCombatShots.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            var scene = SceneManager.GetSceneByPath(ZCombatShots.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) { Assert.Ignore("CombatLab 장면 없음"); yield break; }
            SceneManager.SetActiveScene(scene);
            // 연습장의 시우·허수아비는 치움
            foreach (var g in scene.GetRootGameObjects()) if (g.GetComponentInChildren<Animator>() != null && g.GetComponentInChildren<Camera>() == null) g.SetActive(false);
            var notes = new List<string>();
            GameObject pf = null, tf = null;
#if UNITY_EDITOR
            pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            tf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Char/Taeo.prefab");
#endif
            GameObject Make(GameObject p, Vector3 at, float yaw)
            {
                var h = new GameObject("H"); h.SetActive(false);
                var o = Object.Instantiate(p, at, Quaternion.Euler(0f, yaw, 0f), h.transform);
                foreach (var i in o.GetComponentsInChildren<PInput>(true)) i.enabled = false;
                foreach (var bl in o.GetComponentsInChildren<BodyLean>(true)) bl.enabled = false;   // 연습장 카메라 거리로 인물을 숨기지 않게
                foreach (var pm in o.GetComponentsInChildren<PlayerMotor>(true)) pm.enabled = false;  // 중력으로 연습장 바닥 밑으로 떨어지던 것(줄 세움에 시우가 안 보임)
                foreach (var cc in o.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
                o.transform.SetParent(null, true); Object.Destroy(h);
                SceneManager.MoveGameObjectToScene(o, scene);
                return o;
            }
            var a = Make(pf, new Vector3(-1.1f, 0f, 0f), 180f);
            var b = Make(pf, new Vector3(0f, 0f, 0f), 180f);
            var c = Make(tf, new Vector3(1.2f, 0f, 0f), 180f);
            yield return null;
            b.GetComponent<Outfit>().Set(Outfit.Delivery);
            yield return Lab.Frames(30);
            var models = new[] { ("siwoo", a.GetComponent<Outfit>().Model), ("delivery", b.GetComponent<Outfit>().Model), ("taeo", c) };
            var cam = Camera.main;
            Assert.IsNotNull(cam, "메인 카메라");
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            cam.nearClipPlane = 0.05f;
            foreach (var (_, m) in models)
            {
                var an = m.GetComponent<Animator>(); an.enabled = false;
                var hs = m.GetComponent<HandShape>(); if (hs != null) hs.enabled = false;
                var hp = m.GetComponent<HandPoser>(); if (hp != null) hp.enabled = false;
                var fr = m.GetComponent<FaceRig>(); if (fr != null) { fr.Blink = false; fr.Talk = false; }
                // 애니메이터를 멈춘 뒤 뼈만 바꾸면(ApplyNow) 화면 밖이던 스킨이 옛 모양으로 남는다 → 늘 다시 씌움(손 사진이 팔뚝만 찍히던 것)
                foreach (var smr in m.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            }
            // ① 줄 세움
            cam.fieldOfView = 34f;
            cam.transform.position = new Vector3(0f, 1.0f, -5.4f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - cam.transform.position);
            notes.Add("줄 세움 정면: " + Shots.Shot(cam, Path.Combine(dir, "s6_lineup_front.png"), 1920, 1080, 2, false));
            cam.transform.position = new Vector3(-4.6f, 1.0f, -2.6f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.95f, 0f) - cam.transform.position);
            notes.Add("줄 세움 옆(3/4): " + Shots.Shot(cam, Path.Combine(dir, "s6_lineup_side.png"), 1920, 1080, 2, false));
            // ② 얼굴 근접(얼굴 상자가 화면 높이 40%)
            foreach (var (who, m) in models)
            {
                var an = m.GetComponent<Animator>();
                var head = an.GetBoneTransform(HumanBodyBones.Head);
                var fm = m.GetComponent<FormalModel>();
                var fr = m.GetComponent<FaceRig>();
                float faceH = fm != null && fm.FaceBox.y > 0.05f ? fm.FaceBox.y : 0.26f;
                cam.fieldOfView = 30f;
                float dist = faceH / (0.40f * 2f * Mathf.Tan(15f * Mathf.Deg2Rad));
                var centre = head.position + Vector3.up * 0.06f;
                // 얼굴 쪽 = 위 × (왼팔 − 오른팔) — 모델 루트 방향(시우 모델은 Visual 아래에서 돌아가 있음)에 기대지 않음
                var face = Vector3.Cross(Vector3.up, an.GetBoneTransform(HumanBodyBones.LeftUpperArm).position - an.GetBoneTransform(HumanBodyBones.RightUpperArm).position).normalized;
                cam.transform.position = centre + face * dist;
                cam.transform.rotation = Quaternion.LookRotation(centre - cam.transform.position);
                foreach (var e in FaceRig.ExprNames)
                {
                    fr.Set(e);
                    yield return null;
                    notes.Add($"얼굴 {who} {e}: " + Shots.Shot(cam, Path.Combine(dir, $"s6_face_{who}_{e}.png"), 960, 960, 2, false));
                }
                fr.Set("normal");
            }
            // ③ 손 자세 8종(시우 오른손 — 손등 쪽에서)
            var sm = models[0].Item2;
            var poser = sm.GetComponent<HandPoser>();
            var hand = poser.HandOf(true);
            var palm = poser.PalmOf(true); var palmN = poser.PalmNOf(true);
            var nrm = (palmN.position - palm.position).normalized;
            cam.fieldOfView = 30f;
            int k = 0;
            foreach (var n in HandPoseLib.Names)
            {
                poser.ApplyNow(true, poser.Pose(true, n));
                var mid = hand.position + (poser.HandOf(true).GetComponentsInChildren<Transform>().First(t => t.name == "RightHandMiddle1").position - hand.position) * 0.9f;
                cam.transform.position = mid - nrm * 0.42f + Vector3.up * 0.05f + sm.transform.right * 0.12f;
                cam.transform.rotation = Quaternion.LookRotation(mid - cam.transform.position);
                yield return null; yield return null;
                notes.Add($"손 {n}: " + Shots.Shot(cam, Path.Combine(dir, $"s6_hand_{k++}_{n.Replace(" ", "")}.png"), 720, 720, 2, false));
            }
            poser.ResetRest();
            File.WriteAllLines(Path.Combine(dir, "notes_s6.txt"), notes);
            Debug.Log($"[M3Test] Z 정식 모델 사진 {notes.Count}장 → {dir}");
            foreach (var o in new[] { a, b, c }) Object.Destroy(o);
            if (brain != null) brain.enabled = true;
        }
    }
}
