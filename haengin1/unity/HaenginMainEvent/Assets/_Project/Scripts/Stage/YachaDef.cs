// 행인1의 메인이벤트 — 야차 정의(docs/08_M2_전투_설계.md 8-2). ScriptableObject 는 클래스 이름과 같은 파일에 있어야 에셋이 스크립트를 찾는다.
using System;
using UnityEngine;

namespace Haengin
{
    [CreateAssetMenu(menuName = "Haengin/YachaDef")]
    public sealed class YachaDef : ScriptableObject
    {
        public string Title = "Y1 와룡공원 성곽 아래 공터";
        public Vector3 Center = new Vector3(-192.0f, 38.0f, 142.0f);
        public float RingPush = 6.0f, RingWall = 6.8f, PushSpeed = 3f;
        public int Stake = 50000, DownLimit = 3, CrowdCount = 12;
        public EnemyDef Foe;
        public GameObject FoeModel;
        public GameObject[] CrowdModels = new GameObject[0];
        public GameObject RefereeModel;
        [Tooltip("심판 형 자리(월드) — 원 남쪽 가장자리")] public Vector3 Referee = new Vector3(-192.0f, 38.0f, 135.0f);
        public string CardSmall = "와룡공원 야차 첫판", CardBig = "'스크럼' 육중현";
        public string Ask = "판돈 오만. 룰 알지? 무기 없고, 항복하면 끝. 원 밖으로 밀리면 다시 가운데.";
        public string WinText = "야차 승 — 판돈 +50,000원 · 땀 +60 · 평판 +3";
        public string LoseText = "야차 패 — 판돈 −50,000원";
    }
}
