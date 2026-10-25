# 동작 라이브러리 번호표 (Meshy, 힉스필드 3D Rigging `--animation_action_id`)

출처: https://docs.meshy.ai/en/api/animation-library (2026-10-06 확인). 힉스필드 `3d_rigging --enable_animation true --animation_action_id N` 한 번에 동작 1개, 리깅 포함 8크레딧. 결과 GLB 는 뼈 24개(손가락 없음) + 클립 1개.

## 이동·대기 (게임에 쓰는 것 굵게)
| 번호 | 이름 | 비고 |
|---|---|---|
| 0 | Idle | 두리번거림 — 안 씀 |
| 11 | Idle_02 | 1.5초, 차분 |
| **243** | **Idle_3** | 태오 대기 |
| **246** | **Idle_6** | 시우 대기(한쪽 다리에 무게) |
| 30 | Casual_Walk | 느린 산책(원래 0.7m/s) — 안 씀 |
| 115 | Quick_Walk | 예전 걷기(2026-10-25까지, 골반 기울기 ±9.1° — 대표 "여성스러움") → **여성 인물(엄마·하늘·나리·여학생) 걷기로 남김** `anim/siwoo_walk115.glb` |
| **566** | **walking_2** | **남성 걷기(2026-10-25부터 시우·태오·적·관원·남학생)** — 아래 '걷기 교체' 판정표 |
| 106, 121 | Confident_Walk, Thoughtful_Walk | 걷기 교체 후보(안 씀) |
| **14** | **Run_02** | 달리기 |
| 15, 16 | Run_03, RunFast | |
| 20, 21 | Walk_Fight_Back, Walk_Fight_Forward | 전투 자세 이동 |
| 89 | Combat_Stance | 전투 대기 |
| 156, 157, 162, 164 | Stand_Dodge, _1, _2, _3 | 제자리 회피 |
| 158~161, 163 | Roll_Dodge ~ _4 | 구르기 |

## 공격
| 번호 | 이름 |
|---|---|
| 191 | Left_Jab_from_Guard |
| 192 | Right_Jab_from_Guard |
| 193 | Left_Hook_from_Guard |
| 194 | Right_Uppercut_from_Guard |
| 195 | Right_Upper_Hook_from_Guard |
| 196 | Left_Uppercut_from_Guard |
| 197 | Left_Short_Hook_from_Guard |
| 198, 200, 201, 203, 204, 205 | Punch_Combo, _1 ~ _5 |
| 206 | Spartan_Kick |
| 207 | Roundhouse_Kick |
| 208 | Lunge_Roundhouse_Kick |
| 209 | Boxing_Guard_Right_Straight_Kick |
| 210 | Boxing_Guard_Prep_Straight_Punch |
| 211 | Boxing_Guard_Step_Knee_Strike |
| 212 | Elbow_Strike |
| 213 | Leg_Sweep |
| 214 | Punch_Forward_with_Both_Fists |
| 215 | High_Kick |
| 216 | Lunge_Spin_Kick |
| 217 | Sweeping_Kick |
| 218 | Step_in_High_Kick |
| 4, 87, 90, 96, 103, 105 | Attack, Boxing_Practice, Counterstrike, Kung_Fu_Punch, Simple_Kick, Triple_Combo_Attack |

## 방어·피격·쓰러짐
| 번호 | 이름 |
|---|---|
| 138~146 | Block1 ~ Block10 (7 없음) |
| 173 | Slap_Reaction |
| 174, 175, 176 | Face_Punch_Reaction, _1, _2 |
| 178, 179 | Hit_Reaction, _1 |
| 7 | BeHit_FlyUp |
| 187, 190 | Knock_Down, Knock_Down_1 |
| 189 | dying_backwards |
| 344~353 | Stand_Up1 ~ Stand_Up10 |

## M2 2차 후보 확인 결과 (2026-10-06, 8프레임 띠로 봄)
| 번호 | 이름 | 판정 | 파일 |
|---|---|---|---|
| **366** | falling_down | **채택** — 무릎이 꺾이며 뒤로 넘어가 눕는다. 큰 훅 다운용(187 은 공중제비처럼 과함) | `combat/siwoo_fall.glb` |
| 190 | Knock_Down_1 | 탈락 — 다리가 하늘로 들리며 뒤집힘, 187 처럼 과함 | |
| 502, 503 | Fall1, Fall2 | 탈락 — 공중에서 떨어지는 자세 반복(엎드림·웅크림) | |
| **128** | Heavy_Hammer_Swing | **채택** — 양손을 머리 위로 들어 내려찍음(무기 없이 쓰면 맨손 내려찍기). 냉장고 큰 휘두르기, 태오 리그 | `combat/taeo_smash.glb` |
| **525, 526** | Cautious_Crouch_Walk_Left/Right | **채택** — 무릎 굽혀 옆으로 딛는 걸음. 옆걸음 하체로 쓰고 상체는 전투 자세 층으로 덮음 | `combat/siwoo_side_l.glb`, `siwoo_side_r.glb` |

## M3 동작 확인 결과 (2026-10-06, 8프레임 띠로 봄)
09 설계서 5장 필수 16개. 몸은 시우 `siwoo_tripo_v2_front.glb`(1.74)·태오 `taeo_tripo_v1_front.glb`(1.83)·엄마 `m3/ngunpar_front.glb`(1.58), raw 주소는 커밋 SHA 고정. 미리보기가 필요한 3개(558·255·490)는 후보(555·26·519)를 하나씩 더 뽑아 띠와 발·손·엉덩이 높이 측정으로 골랐다. 시간은 24fps 기준.

| 번호 | 이름 | 판정 | 파일 |
|---|---|---|---|
| **33** | Chair_Sit_Idle_M | **채택** — 의자에 앉아 팔꿈치를 무릎에 대고 숙였다가 다시 기댐, 10.7초 반복. 자전거 IK 바탕 자세 | `m3/siwoo_chair_sit_idle.glb` |
| **57** | Stand_to_Sit_Transition_M | **채택** — 서서 엉덩이를 뒤로 빼며 앉음, 4.8초 | `m3/siwoo_stand_to_sit.glb` |
| **53** | Sit_to_Stand_Transition_M | **채택** — 앉은 자세에서 몸을 숙여 일어섬, 6.2초 | `m3/siwoo_sit_to_stand.glb` |
| **274** | Female_Crouch_Pick_Up_Place_Side | **채택** — 무릎·엉덩이를 굽혀 옆 바닥의 물건을 들었다 내려놓음, 9.5초 | `m3/siwoo_crouch_pickup.glb` |
| **276** | Male_Bend_Over_Pick_Up | **채택** — 무릎을 편 채 허리만 숙여 줍기(잘못된 자세 경고에 맞음), 7.2초 | `m3/siwoo_bend_pickup.glb` |
| **551** | Carry_Heavy_Object_Walk | **채택** — 두 팔을 가슴 앞(손 높이 약 1.3m)에 들고 무겁게 걸음, 0.26m/s(6.5초에 1.7m) | `m3/siwoo_carry_heavy_walk.glb` |
| **441** | Climb_Stairs | **채택** — 무릎을 높이 들어 오름. 2.5초에 0.74m 오르는데 앞으로는 0.29m 뿐(가파른 계단 기준) — 곰방 계단에서는 루트 이동을 끄고 코드로 옮김 | `m3/siwoo_climb_stairs.glb` |
| **62** | penguin_walk | **채택** — 제자리 펭귄 걸음(앞으로 0m), 2초 반복. 팔을 옆으로 퍼덕임(손 높이 1.2~1.5m) — 이동은 코드로, 팔이 과하면 하체만 | `m3/siwoo_penguin_walk.glb` |
| **558** | Limping_Walk | **채택** — 한쪽 다리를 끄는 절뚝 걸음, 곧게 0.71m/s(3.5초에 2.5m) | `m3/siwoo_limp_walk.glb` |
| 555 | Limping_Walk_1 | 탈락 — 1.5초 한 주기, 왼발을 0.36m 들어 깡충거리고 한 주기에 옆으로 19cm 샘 | |
| **26** | Angry_Stomp | **채택(255 대신)** — 0.5~1.3초에 왼발을 0.46m 들어 쾅 밟고, 이어 팔짱·삿대질로 화냄(전체 8초). 케이크 밟기는 앞 1.3초만 | `m3/siwoo_angry_stomp.glb` |
| 255 | Angry_Ground_Stomp | 탈락 — 무릎을 굽히고 양발로 동동 구르는 짜증(발이 5cm도 안 들림, 1.4초). '밟는다'로 안 읽힘 | |
| **389** | Grip_and_Throw_Down | **채택** — 두 팔로 머리 위까지 잡아 올렸다가 앞으로 내리꽂음, 4.7초. 태오 리그 | `m3/taeo_grip_throw_down.glb` |
| **354** | Sitting_Clap | **채택** — 앉아서 박수, 3.3초 | `m3/siwoo_sitting_clap.glb` |
| **304** | Seated_Fist_Pump | **채택** — 앉아서 주먹을 머리 위로 올려 흔듦, 3.3초. 책상을 두드리는 동작은 아님(두드리기가 꼭 필요하면 IK) | `m3/siwoo_seated_fist_pump.glb` |
| **313** | Talk_with_Hands_Open | **채택** — 서서 두 손을 펴 보이며 말함, 4초. (Meshy 문서 요약에는 Talk_with_Right_Hand_Open 으로 나왔지만 받은 GLB 의 클립 이름은 Talk_with_Hands_Open) | `m3/siwoo_talk_hands_open.glb` |
| **309** | Talk_with_Left_Hand_on_Hip | **채택** — 왼손을 허리에 얹고 오른손으로 몸짓하며 말함, 5.2초. 엄마 리그(1.58)로 뽑음 | `m3/ngunpar_talk_hand_on_hip.glb` |
| **519** | sliding_stumble | **채택(490 대신)** — 0~3.6초는 땅에서 0.37m 떠서 미끄러지며 균형 잡는 부분이라 쓰지 않음. 3.7~5초에 앞으로 고꾸라져 손·무릎을 짚고(앞으로 1.5m) 9초까지 일어남. 「배달 자전거」에 걸려 엎어지는 데는 프레임 88~216 | `m3/siwoo_sliding_stumble.glb` |
| 490 | Fall_Down | 예비 — 서 있다가 앞으로 휘청, 몸이 돌며 다리가 들려 뒤로 벌러덩(4.7초, 끝은 다리를 든 채 누움). 선 자세에서 바로 시작해 잇기는 쉽지만 '엎어짐'이 아니라 '뒤로 넘어짐' | `m3/siwoo_fall_down.glb`(예비) |

## 걷기 교체 (2026-10-25 대표 피드백 "걷는 게 이상하게 여성스러움", 크레딧 32)
시우 리그(`siwoo_tripo_v2_front.glb` 1.74m, 커밋 9c96853 raw 주소)로 후보 4개 → `concept/art/3d/anim/walk/siwoo_walk<번호>.glb`(`tools/pipeline/walk_clips.ps1`), 한 주기를 재는 `tools/pipeline/walk_metrics.py`(Blender) + 옆·뒤 8프레임 띠 → 세션 scratchpad `m3_shots/walk_compare.jpg`.

| 번호 | 이름 | 골반 기울기 | 골반 비틀기 | 골반 좌우 | 팔 앞뒤 | 어깨 비틀기 | 상체 숙임 | 보폭 · 분당 | 고유 속도 → 1.4 배율 | 판정 |
|---|---|---:|---:|---:|---:|---:|---:|---|---|---|
| 115 | Quick_Walk(지금) | **±9.1°** | ±3.2° | 2.4cm | 51cm | **±19.9°** | 3.3° | 0.65m · 120 | 1.30 → ×1.08 | 골반이 크게 기울고(엉덩이가 좌우로 떨어짐) 어깨가 크게 돎 — '여성스럽다'의 원인. 여성 인물용으로 남김 |
| 106 | Confident_Walk | ±4.6° | ±13.4° | 4.8cm | 21cm | ±11.6° | −3.4°(뒤로 젖힘) | 0.51m · 87 | 0.74 → ×1.89 | 한 손을 허리에 얹고 다른 손을 흔드는 과장된 몸짓 — 캐릭터 연기 동작이라 걷기 기본값으로 못 씀. 1.4m/s 에 거의 두 배속 |
| 121 | Thoughtful_Walk | ±3.5° | ±12.1° | 3.5cm | 41cm | ±15.9° | −0.1° | 0.48m · 76 | 0.60 → ×2.33 | 골반은 차분하지만 생각하며 걷는 느린 걸음 — 1.4m/s 에 2.3배속이면 종종걸음 |
| **566** | **walking_2** | **±6.0°** | ±7.6° | 3.9cm | 46cm | **±7.6°** | **+4.8°(앞으로)** | 0.59m · 103 | 1.01 → **×1.39** | **채택.** 어깨 비틀기가 115 의 2/5, 골반 기울기 2/3, 상체를 조금 앞으로 숙이고 팔은 몸 옆에서 앞뒤로 — 지친 배달 소년의 무심한 걸음. 1.4m/s 에 1.39배(분당 약 143걸음 — 빠른 걸음으로 보임) |
| 30 | Casual_Walk | ±2.5° | ±2.5° | **6.4cm** | 29cm | ±7.8° | 0.4° | 0.50m · 85 | 0.71 → ×1.97 | 골반은 가장 덜 기울지만 몸 전체가 좌우로 흔들리고(6.4cm) 뻣뻣함, 1.4m/s 에 두 배속 |

- **시우·태오 따로 고르지 않음**: 태오용 '가슴 편 걸음' 후보는 106 인데 과장된 손짓 때문에 탈락 — 태오도 566(시우 컨트롤러를 태오 아바타에 Humanoid 로, `Anim/TaeoRun.overrideController`). 남성 NPC(적·관원·구경꾼)는 `Enemy.controller`·`Gym.controller` 가 시우 걷기 클립을 같이 써서 자동으로 바뀜. 여성 인물은 아직 게임에 걷는 장면이 없다 — 들어오면 115 를 쓴다.
- 숫자 뜻(`walk_metrics.py` 머리 주석): 왼발이 가장 앞에 오는 두 순간 사이 한 주기. 골반 기울기 = 골반 옆축이 수평에서 기우는 진폭(엉덩이 떨어짐), 비틀기 = 수평 회전 진폭, 좌우 = 골반의 옆 흔들림 폭.

## 기타
| 번호 | 이름 |
|---|---|
| 88 | Chest_Pound_Taunt |
| 59, 403, 412 | Victory_Cheer, Victory_Fist_Pump, victory |

무기·총·검 동작(97, 102, 147~155, 177, 180~186, 199, 202, 219~221 등)은 17세 맨손 격투 게임에 맞지 않아 쓰지 않는다.

## 바른 앉기 (2026-10-25 — 반 친구가 책상에 엎드린 듯 보임, 크레딧 32)
시우 리그(`siwoo_tripo_v2_front.glb`, 커밋 9c96853 raw 주소)로 후보 4개 → `concept/art/3d/anim/m3/sit/siwoo_sit<번호>.glb`(`tools/pipeline/sit_clips.ps1`), 클립 전체를 재는 `tools/pipeline/sit_metrics.py`(상체 숙임 = 골반→목이 수직과 이루는 각). 띠 그림 `m3_shots/sit_compare.jpg`.

| 번호 | 이름 | 숙임 평균·최대 | 머리(골반 위) | 판정 |
|---|---|---:|---:|---|
| 33 | Chair_Sit_Idle_M(지금) | 34.2° · 50.5° | 0.36m | 팔꿈치를 무릎에 대고 숙임 — 책상 앞에선 엎드린 듯 보임 |
| 32 | Chair_Sit_Idle_F | 22.8° · 52.9° | 0.41m | 탈락 — 중간에 손으로 얼굴을 괴며 크게 숙임 |
| **364** | Sit_on_Chair_Arms_Crossed | **3.8° · 8.6°** | 0.46m | **채택 — Sit**(58프레임 중 끝 46~57 = 팔짱 낀 바른 자세만 반복) |
| **307** | Sitting_Answering_Questions | 6.4° · 19.4° | 0.49m | **채택 — SitTalk**(바르게 앉아 손짓, 7.8초 반복) |
| **363** | Sit_Cross_Legged_on_Floor | 0.8° · 0.9° | 0.44m | **채택 — SitFloor**(바닥 양반다리, 끝 45~60 반복 — 시우네 좌식 밥상) |

