# 3D 캐릭터·동작 파이프라인 스크립트 (2026-10-06 세션 임시 폴더에서 옮김)

실제로 돌린 원본이다. 스크립트 안의 절대 경로(세션 scratchpad, `C:\클로드\haengin1-repo`)는 그때 그대로라 다시 쓸 땐 경로부터 고칠 것. PowerShell 5.1 로 돌릴 .ps1 은 UTF-8 BOM 필수(없으면 한글 경로가 깨짐).

| 순서 | 파일 | 하는 일 |
|---|---|---|
| 1 | `enemy_color.ps1`, `prompts/*_color.txt` | GPT Image 2.5 색 설정화(high 2k 2:3, 2.75cr) — 한 장 먼저 보고 진행 |
| 2 | `gen_apose.ps1`, `enemy_apose.ps1`, `prompts/*_apose.txt` | 색 설정화를 참조로 A포즈 4방향 시트(high 4k 16:9, 4.25cr) |
| 3 | `crop4.py` | 시트를 정면·왼·뒤·오른 4장으로 자름. 흰색 아닌 화소로 판정, 구분선은 행의 90% 이상일 때만(옛 판정은 냉장고 팔을 잘랐음). 자른 4장은 반드시 눈으로 확인 |
| 4 | `gen_3d.ps1`, `enemy3d.ps1`, `naeng_v2.ps1` | Tripo 멀티뷰 → 3D(detailed 18 / standard 9cr). **시우·태오도 `geometry_quality detailed` 로 뽑았음(M3 결정 4 근거)** |
| 5 | `reorient.py` | Tripo 결과 정면 +X → −90° 돌려 +Z, 키 맞추기, 발 z=0 |
| 6 | (각 .ps1 안) | front GLB 를 커밋·푸시 → **커밋 SHA 고정 raw 주소**로 `3d_rigging`(동작 1개 포함 8cr) |
| 7 | `posetest_front.py`, `still.py` | 리깅 자세 시험·정면 렌더 |
| 8 | `combat_siwoo.ps1`, `m2_clips.ps1`, `m2_clips2.ps1`, `animframes2.py` | 동작 뽑기와 8프레임 띠 확인 |

M3 인물 12명의 프롬프트는 `concept/art/fujimoto/m3/prompts/`.

## 이윤준(혜화 MMA 관장, 실존 인물) — 2026-10-24, 체육관 작업과 함께
스크립트는 `tools/env/gym/char/`(이 README 의 순서 그대로, 경로만 체육관 작업 폴더). 색 설정화(2.75, 공개 사진 2장 참조 — `concept/art/fujimoto/README.md` 출처) → A포즈 4k 16:9(4.25) → `crop4.py` → `concept/art/3d/m3/input/yunjun_*.png` → Tripo detailed(18) `yunjun_tripo.glb` → `reorient.py` 1.75m `yunjun_front.glb` → 커밋 9c96853 raw 주소로 `3d_rigging` + 243 Idle_3(8) `yunjun_idle243.glb`(뼈 24). 합계 33cr. 사진 참조는 GPT Image 2.5 가 거부하지 않았다.

## 김승연·손진수·서진수(딥마카이 진행자 코치 3명, 실존 인물) — 2026-10-25
범용 `tools/env/gym/char/char_gen.ps1 -Name <이름> -Step color|apose|tripo|rig`(프롬프트 `char/<이름>_{color,apose}.txt`). 이름 `seungyeon`·`jinsoo_son`·`jinsoo_seo`, 키 1.80·1.68·1.70 m(공개 프로필 — `concept/art/fujimoto/README.md` 출처). 사람마다 color 2.75(사진 2장 참조) → apose 4.25 → `crop4.py` → Tripo detailed 18 → `reorient.py` → 커밋 a437f75 raw 주소로 rig 243 Idle_3 8(뼈 24) = 33, 셋 합계 99cr. 업로드 520(과금 없음) 두 번은 `generate list` 확인 뒤 재실행. **Tripo 좌우 규약**: '왼쪽' 이미지(화면 왼쪽을 향한 뷰) = 캐릭터 왼쪽 옆면(이윤준 왼손목 시계로 확인) — 좌우가 다른 무늬(김승연 왼팔 문신)는 시트의 반대쪽 뷰에 그려지면 그 뷰를 덧칠하고 넣을 것. FBX `Art/Characters/M3/{Seungyeon,JinsooSon,JinsooSeo}.fbx`(glb2fbx `Idle`, 메시 1·손 0 = Yunjun 과 같음) + `*Tex.jpg`(GLB 안 JPEG 그대로).
