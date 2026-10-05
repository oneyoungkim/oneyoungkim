// 행인1의 메인이벤트 — 구역 경로 데이터(체크포인트·시작 지점). M1Setup 이 zone1.json 에서 채워 Zone1/Route 에 붙인다.
// 런타임(테스트·나중의 체크포인트 진행 코드)이 에디터용 JSON 읽기 코드 없이 쓸 수 있게.
using UnityEngine;

namespace Haengin
{
    public sealed class RouteData : MonoBehaviour
    {
        public string[] Names = new string[0];
        public Vector3[] Points = new Vector3[0];
        public float[] Radii = new float[0];
        public Vector3 SpawnPos;
        public float SpawnYaw;
        public Vector3 BoundsMin, BoundsMax;

        public int Count => Points != null ? Points.Length : 0;
    }
}
