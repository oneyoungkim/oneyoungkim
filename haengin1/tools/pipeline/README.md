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

## 조연 3명(국밥집 사장 할머니·로터리 빵집 직원 누나·아빠 반정호, 가상 인물) — 2026-10-29
자막만 나오던 조연을 FBX 로. **할머니는 10/6 에 이미 만든 `halmeoni`(Tripo standard, 리깅 `anim/halmeoni_idle243.glb`, 1.50 m)를 그대로 FBX 로만 바꿨다(0cr)**. 새로 만든 둘은 `char_gen.ps1 -Name bakery_staff|dad`(프롬프트 `char/<이름>_{color,apose}.txt`): color 2.75 → apose 4.25 → `crop4.py` → Tripo detailed 18 → `reorient.py`(1.62·1.74 m, 설정에 키가 없어 정함) → 커밋 347f522 raw 주소로 rig 243 Idle_3 8(뼈 24) = 33, 둘 합계 66cr(낭비 0). 직원 누나 = 20대 중반, 초콜릿색 가슴 앞치마(허벅지 중간까지 — 무릎을 넘으면 다리가 붙음), 크림 셔츠, 낮은 쪽머리. 아빠 = 40세 형틀목수(4-2 회상 7살 시우 기준), 바랜 파랑 작업 셔츠 + 흰 티 + 카키 작업 바지, **집 안 장면이라 회색 양말(신발 없음)**, 안전모·망치는 소품. FBX `Art/Characters/M3/{Halmeoni,BakeryStaff,Dad}.fbx`(glb2fbx `Idle`, 메시 1·손 0) + `*Tex.jpg`(GLB 안 JPEG 그대로). 앞치마는 엄마 앞치마처럼 앉기·쪼그리기에서 다리를 따라 늘어난다. 보스전 6-3(민재)·7-1(민재·건우·임찬=깐족이)은 모델이 이미 있어 만들지 않았다.

## 백승민(코너맨 관장, 실존 인물·본인 허락) — 2026-10-29
`char_gen.ps1 -Name seungmin`: color 2.75(공개 사진 2장 참조 — 출처 `concept/art/fujimoto/README.md`) → apose 4.25 → `crop4.py` → Tripo detailed 18 → `reorient.py` 1.83 m → 커밋 745da52 raw 주소로 rig 243 Idle_3 8(뼈 24) = 33cr. 옷은 슬레이트 틸 무지 반팔 티(#456462)·차콜 조거·흰 양말·검정 슬리퍼(다른 코치 넷과 색 구별). FBX `Art/Characters/M3/SeungminBaek.fbx` + `SeungminBaekTex.jpg`.

## 이윤준 닮음 고치기 v2 — 2026-10-29(0cr)
비교 사진: 세션 scratchpad `yunjun_likeness/compare.jpg`. **원인 ① Tripo**: A포즈 정면까지는 눈 사이/얼굴 폭 .46 이었는데 Tripo 모델(형상·텍스처 모두)이 .54 로 넓히고 눈을 작게 그림(실물 .39, 설정화 .43, 눈~입/눈 사이 실물 1.04~1.07 → 모델 .85). **원인 ② Unity**: `CharSetup.Texture` 기본 최대 2048(원본 4096 → 얼굴 약 240px), 실내에서 얼굴이 UTS 1단 그늘(텍스처 × .78·.71·.74 × 조명색)에 들어가 갈색으로 납작, 헐 외곽선 4.5 mm 가 눈두덩·코 밑에 먹을 칠함.
고친 것(형상·뼈 24·UV·동작 그대로 → 기존 애니메이션 그대로 붙음): `face_warp_map.py`(정면 투영 휘기 — 그린 눈·눈썹을 4.5 mm씩 가운데로·2 mm 위로·10% 크게, 눈 사이 7.5 → 6.6 cm) → `face_warp_apply.py` → `glb_swap_tex.py`(GLB 이미지만 바꿈) `concept/art/3d/m3/yunjun_v2_idle243.glb` → glb2fbx `Yunjun_v2.fbx` + `Yunjun_v2Tex.jpg`. `face_masks.py` → `face_shade_outw.py` 로 `Yunjun_v2Shade.jpg`(얼굴 앞면 .93·.88·.88, 피부 .80·.72·.72, 나머지 .78·.71·.74)·`Yunjun_v2OutW.png`(1024², 얼굴 앞면 0·머리카락 .45·나머지 1) — `face_tex.py`(정식 모델)와 같은 값. 형상 자체를 옮기는 방법은 얼굴 정점이 성겨(눈 둘레 215개) 눈이 찌그러져 버렸다.
**Unity 담당 적용값**: `Yunjun_v2Tex.jpg`·`Yunjun_v2Shade.jpg` 최대 크기 4096(BC7 그대로), `Yunjun_v2OutW.png` 는 sRGB 끔·1024(`Texture(path, false, 1024)`). 재질은 `CharSetup.FormalToon` 의 몸 재질과 같게: `_Use_BaseAs1st` 0 · `_1st_ShadeMap` = Yunjun_v2Shade · `_1st_ShadeColor` 흰색 · `_Use_1stAs2nd` 0 · `_2nd_ShadeMap` = Yunjun_v2Shade · `_2nd_ShadeColor` (.88,.86,.88) · `_Outline_Sampler` = Yunjun_v2OutW(외곽선 폭 값은 그대로). NPC 키 `Yunjun` → `Yunjun_v2` 로 바꾸면 `NpcTex`·`NpcFbx` 경로가 맞는다. 기존 `Yunjun.fbx`·`YunjunTex.jpg` 는 건드리지 않았다.

## 코치 3명 v2(김승연·손진수·서진수, 최근 모습) — 2026-10-29
`char_gen.ps1 -Name {seungyeon,jinsoo_son,jinsoo_seo}_v2`: color 2.75(최근 사진 3장 참조) → apose 4.25 → `crop4.py` → Tripo detailed 18(김승연 1차는 서버 failed·자동 환불 뒤 다시) → `reorient.py` 1.80·1.68·1.70 → 커밋 4377658 raw 주소로 rig 243 Idle_3 8(뼈 24) = 사람당 33, 셋 99cr. 프롬프트에 '눈 사이·눈 크기·코 길이·눈~입 거리를 사진 그대로, 눈을 벌리지 말 것'을 넣었다(이윤준 v2 교훈). **PowerShell 함정**: 변수 이름은 대소문자를 안 가린다 — `$H`(SHA)와 `$h`(키)를 같이 쓰면 주소가 `…/oneyoungkim/1.80/…` 이 되어 리깅 3건이 failed(자동 환불, 0cr). 
닮음 점검(정면, 격자 판독 ±5%): 눈~입÷눈 사이 = 실물 정면 김승연 1.16 · 손진수 1.32 · 서진수 1.21(실물은 입을 벌리거나 웃는 사진이라 조금 큼) / 색 설정화 ~1.04(3/4라 참고) · 0.97 · 0.99 / A포즈 0.87 · 0.99 · 0.87 / v1 1.06 · 0.97 · 0.95 / Tripo 0.82 · 0.90 · 0.86 → 보정 뒤 0.94 · 1.00 · 1.00. 눈 사이 ÷ 얼굴 폭 = 실물 0.45 · 0.37 · 0.44 / Tripo 0.43 · 0.44 · 0.41 → 보정 뒤 0.39 · 0.41 · 0.37(머리카락이 관자놀이를 덮어 얼굴 폭 판독이 흔들림 — 모델은 cm 로 봄). 모델 눈 사이(눈동자 가운데) = Tripo 7.0 · 7.2 · 7.3 cm → 보정 뒤 6.5 · 6.5 · 6.5 cm. 이번에는 A포즈 단계부터 눈~입이 짧아졌고 Tripo 가 눈 사이를 넓혔다.
보정(0cr, 형상·뼈·UV 그대로): `face_warp_map.py`(눈마다 2.5·3.5·4.0 mm 가운데로, 4·3.5·3.5 mm 위로, 8% 크게) → `face_warp_apply.py`. 손진수는 Tripo 텍스처의 왼눈(화면 오른쪽) 눈동자에 흰 점이 박혀 `face_mirror_map.py`(반대쪽 눈을 가운데 선으로 거울 복사, 반지름 1.7 cm, 가장자리 섞기 `face_warp_apply.py … blend`)로 고쳤다. → `glb_swap_tex.py` `concept/art/3d/m3/<이름>_v2_idle243_fix.glb` → glb2fbx `Art/Characters/M3/{Seungyeon,JinsooSon,JinsooSeo}_v2.fbx` + `_v2Tex.jpg`, `face_masks.py`·`face_shade_outw.py` 로 `_v2Shade.jpg`·`_v2OutW.png`(이윤준 v2 와 같은 값). Unity 적용값도 이윤준 v2 와 같다(텍스처 4096, `FormalToon` 몸 재질처럼 Shade 지도·`_Outline_Sampler`). v1(`Seungyeon.fbx` 등)은 그대로.
