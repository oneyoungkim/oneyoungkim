// 행인1의 메인이벤트 — 인카운터 정의(docs/08_M2_전투_설계.md 8-1). ScriptableObject 는 클래스 이름과 같은 파일에 있어야 에셋이 스크립트를 찾는다.
using System;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/EncounterDef")]
    public sealed class EncounterDef : ScriptableObject
    {
        [Serializable] public struct Foe { public EnemyDef Def; public GameObject Model; public Vector2 Local; }
        [Serializable] public struct Box { public Vector2 Local; public Vector2 Size; public float LocalYaw; }
        public string Title = "Y4 후문 뒷골목 주차장";
        [Tooltip("무대 가운데(월드)·yaw·크기(가로 x · 세로 z)")] public Vector3 Center;
        public float Yaw;
        public Vector2 Size = new Vector2(18f, 12f);
        [Tooltip("시작: 안쪽으로 이만큼 들어오고 + 적과 이 거리 안")] public float TriggerInset = 1f, TriggerEnemyDist = 9f;
        public Foe[] Foes = new Foe[0];
        [Tooltip("구경꾼 자리(로컬)")] public Vector2[] Crowd = new Vector2[0];
        public GameObject[] CrowdModels = new GameObject[0];
        [Tooltip("CrowdRing 구간(로컬 선분 — 진입로 6m)")] public Vector2 CrowdLineA, CrowdLineB;
        [Tooltip("전투 동안만 켜는 경계 벽(PlayerOnly, 로컬)")] public Box[] Walls = new Box[0];
        public string Line1Who = "깐족이", Line1 = "어? 야, 거기. 너 지금 우리 보고 웃었냐?";
        public string Line2Who = "냉장고", Line2 = "(말없이 껌 풍선)";
        public string ResultText = "정리 완료 — 땀 +30 · 평판 +1";
        [Tooltip("패배 뒤 '다시' 시우 자리(로컬, 북쪽 진입로 바깥 인도) · 볼 방향(로컬 yaw)")] public Vector2 RetryLocal = new Vector2(0f, 8.6f);
        public float RetryYaw = 180f;

        public Vector3 ToWorld(Vector2 l) => Center + Quaternion.Euler(0f, Yaw, 0f) * new Vector3(l.x, 0f, l.y);
        public Vector2 ToLocal(Vector3 w) { var d = Quaternion.Euler(0f, -Yaw, 0f) * (w - Center); return new Vector2(d.x, d.z); }
    }
}
