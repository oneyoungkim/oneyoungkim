# 혜화 MMA 체육관 에셋 도구 (설계: `docs/12_체육관_설계.md`)

모두 저장소 `haengin1/` 에서 실행. .ps1 은 UTF-8 BOM(PowerShell 5.1 한글 경로). Higgsfield CLI 는 인자 배열로 호출(셸 없음), 503/520 이 나도 `generate list` 로 과금 여부 확인 뒤 재호출.

| 순서 | 명령 | 결과 |
|---|---|---|
| 1 스타일 프레임 | `powershell -File tools/env/gym/gen_images.ps1 -Spec tools/env/gym/specs/frames.json [-Only g01_...]` | `concept/art/env/gym/frames/g0*.png` (2.75cr/장) |
| 2 소품 참조 | `... gen_images.ps1 -Spec tools/env/gym/specs/props.json` | `props/ref/<name>.png` (2.75cr) |
| 3 소품 3D | `powershell -File tools/env/gym/gen_3d.ps1 -Names a,b,c -Model hunyuan` (`all` 가능) | `props/raw/<name>__hunyuan.glb` (11cr) |
| 4 소품 정규화 | `python tools/env/gym/process_props.py [names] [--force] [--jobs 4]` — `tools/env_assets/normalize_prop.py` 재사용 | `props/<name>.glb` + `props_report.json` (정면 +Z·바닥 0·실측·면 상한·참조 팔레트) |
| 5 텍스처 | `... gen_images.ps1 -Spec tools/env/gym/specs/textures.json` → `python tools/env/gym/make_textures.py` | `textures/M_*.png`(1024² 이음매 없음), `M_Poster_Atlas.png`(2048²) + `poster_atlas.json`, `textures.json`, `_sheet.jpg` |
| 6 키트 | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup -P tools/env/gym/build_gym.py -- [--format fbx\|glb\|both] [--blend x.blend]` | `concept/art/env/gym/Gym_Hyehwa.fbx` + `Gym_Hyehwa_manifest.json` (소켓 115: Prop_·Sock_Light_·Sock_NPC_·Sock_Drill_·Sock_Cam_·Sock_Audio_) |
| 7 미리 보기 | `blender -b --factory-startup -P tools/env/gym/preview_gym.py -- [--shots g01,g02] [--samples 16] [--props 1]` → `python tools/env/gym/compose_compare.py` | `_preview/g0*.png`, `_preview_compare.jpg` (프레임 \| 키트+소품) |
| 8 이윤준 | `char/yunjun_color.ps1 -Refs <사진,…>` → `char/yunjun_apose.ps1` → `python tools/pipeline/crop4.py concept/art/fujimoto/m3/yunjun_apose.png concept/art/3d/m3/input/yunjun` → `char/yunjun_tripo.ps1` → `blender -b --python tools/pipeline/reorient.py -- …yunjun_tripo.glb …yunjun_front.glb 1.75` → 커밋·푸시 → `char/yunjun_rig.ps1 -ModelUrl <SHA 고정 raw 주소>` | `concept/art/fujimoto/m3/yunjun_*.png`, `concept/art/3d/m3/yunjun_{tripo,front,idle243}.glb` |

지정값: `specs/props_meta.json`(크기·면 상한), `specs/props_final.json`(fit·yaw 보정), `specs/textures_meta.json`(결 종류·m). 로그·크레딧: `logs/`.
키트 재질 이름은 `build_gym.py` 의 `GYM_MATS`(docs/12 5-1 표) — `envkit/mats.py` 의 MATS 를 실행 때 늘린다(건물 키트 파일은 손대지 않음).
