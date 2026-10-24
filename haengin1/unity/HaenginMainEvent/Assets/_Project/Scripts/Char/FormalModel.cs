// 행인1의 메인이벤트 — 정식 모델 표시(docs/09_M3_버티컬슬라이스_설계.md 4-2 '정식 모델 2명의 정의', 6단계)
// tools/formal_model.py(Blender) + tools/face_tex.py 가 만든 측정값을 모델 루트에 붙여 둔다(FormalSetup 이 <이름>Face.json 에서 채움).
// 테스트 D15 가 여기 값과 메시를 함께 다시 잰다: 키(바인드 자세 메시 높이) · 두개골 기준 등신((정수리 − 턱끝) ÷ 1.08) · LOD 삼각형 · 소켓 5개.
using UnityEngine;

namespace Haengin
{
    [DisallowMultipleComponent]
    public sealed class FormalModel : MonoBehaviour
    {
        /// 소켓 이름(4-2 #8). 컷신 소품 트랙이 "배우/자리" 로 찾는다(CutCtx.Socket)
        public const string SocketR = "오른손", SocketL = "왼손", SocketHead = "머리", SocketBack = "등", SocketWaist = "허리";
        public static readonly string[] Sockets = { SocketR, SocketL, SocketHead, SocketBack, SocketWaist };

        [Tooltip("이름(Siwoo · SiwooDelivery · Taeo)")] public string Who;
        [Tooltip("바이블 등신 목표(시우 7.4 · 태오 7.5)")] public float HeadsTarget;
        [Tooltip("도구가 잰 두개골 기준 등신")] public float HeadsMeasured;
        [Tooltip("도구가 잰 키(m)")] public float Height;
        [Tooltip("턱끝 높이 — 메시 공간(Blender Z 위) m")] public float ChinZ;
        [Tooltip("머리 배율(바이블 등신 맞춤) · 몸 되돌림 배율")] public float HeadScale = 1f, BodyRescale = 1f;
        [Tooltip("윗눈꺼풀 굵기(월드 mm)")] public float LidMm;
        [Tooltip("얼굴 상자 가로·세로(m)")] public Vector2 FaceBox;
        [Tooltip("LOD 삼각형(도구 기록): 0 · 1 · 2 · 얼굴")] public int TrisLod0, TrisLod1, TrisLod2, TrisFace;
    }
}
