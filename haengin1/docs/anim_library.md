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

## 기타
| 번호 | 이름 |
|---|---|
| 88 | Chest_Pound_Taunt |
| 59, 403, 412 | Victory_Cheer, Victory_Fist_Pump, victory |

무기·총·검 동작(97, 102, 147~155, 177, 180~186, 199, 202, 219~221 등)은 17세 맨손 격투 게임에 맞지 않아 쓰지 않는다.
