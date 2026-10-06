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
