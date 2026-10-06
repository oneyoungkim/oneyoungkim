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
| **115** | **Quick_Walk** | 걷기(원래 약 1.43m/s → 게임 1.4m/s) |
| 566 | walking_2 | 후보 |
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

## 기타
| 번호 | 이름 |
|---|---|
| 88 | Chest_Pound_Taunt |
| 59, 403, 412 | Victory_Cheer, Victory_Fist_Pump, victory |

무기·총·검 동작(97, 102, 147~155, 177, 180~186, 199, 202, 219~221 등)은 17세 맨손 격투 게임에 맞지 않아 쓰지 않는다.
