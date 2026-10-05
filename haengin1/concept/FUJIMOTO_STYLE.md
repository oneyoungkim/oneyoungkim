# 후지모토풍 스타일 바이블 — 캐릭터 그림체 기준 (A 컬러 셀셰이딩)

> 2026-10-05 대표 결정: 아트 방향 **A(컬러 셀셰이딩)**, 엔진 **Unity**, 캐릭터 그림체는 **후지모토 타츠키**(체인소맨·파이어 펀치·룩백·17-26)의 그림 스타일·인물 비율·만화 스타일을 최대한 가져온다.
> 대표 피드백: 지금 3D 캐릭터 얼굴이 **"너무 이상하고 사실적이지 않다"**.
>
> **2026-10-05 추가 결정**: 반시우 162cm / 48kg → **174cm / 61kg(밴텀급)**. 여전히 마른 체형이다. 2장 수치는 머리 크기를 거의 그대로 두고 몸통·다리를 늘려 다시 계산했고(시우 7.0 → **7.4등신**), 부록 B 프롬프트도 새 값으로 고쳤다. `art/fujimoto/`의 설정화 11장은 바뀌기 전 값(162cm / 48kg, 7등신)으로 생성한 것이다.
>
> **문서 우선순위**: 캐릭터의 비율·얼굴·머리카락·옷·색은 이 문서가 기준이다. `STYLISH_SPEC.md` 1장(목표 룩)·2장(캐릭터 디자인)과 `REFERENCES.md` 1장(주술회전풍 C1~C5)은 이 문서와 부딪치면 이 문서를 따른다. `STYLISH_SPEC.md` 3장(관절 규약)·4장(파일 인터페이스)과 `REFERENCES.md` 2장(한옥 골목 배경)은 그대로 유효하다.
>
> 근거: 조사 보고서 2건(2026-10-05). ① 핀터레스트 공개 이미지 166장 관찰·실측(공식 원고, MAPPA 설정화, 공식 컬러 원고 기준), ② 인터뷰·제작 기사·비평(CGWORLD, ANN, Slashfilm, 사쿠가 블로그, 리얼사운드 등, 부록 A).

---

## 1장. 화풍 핵심

### 1-1. 한 줄 정의
**"형태는 사실, 선은 최소."** 두개골·턱·목·손은 실제 사람 몸처럼 만들고, 얼굴 위에 그리는 선은 몇 개로 줄인다. 표정은 평소에 덤덤하다가 갑자기 터지고, 연기는 이목구비보다 **자세와 카메라**로 한다.

### 1-2. 조사 근거 요약
| 항목 | 관찰·출처 | 우리 결론 |
|---|---|---|
| 작가 배경 | 미대 서양화 전공, 뎃생·크로키 중심. 영향: 사무라 히로아키, 니헤이 츠토무 같은 거친 펜·사실적 몸의 작가 | 데포르메 공식보다 **관찰 드로잉**이 출발점 |
| 영화 | "한국 영화처럼 만화를 그리고 싶다"(<추격자>). 타란티노식 클로즈업, 롱테이크, 정적 뒤 폭발 | 혜화동 밤 골목, 둔탁한 타격, 긴 정적 → 첫 타격에서 폭발 |
| 비율 | MAPPA 키 대비표: 173cm 16세 6.95등신(머리털 포함)·두개골 기준 약 7.45. 원고 롱샷 7.2~7.7 | 10대 남 **7.0~7.5등신**. 주술회전풍 7.6/8.4 폐기 |
| 얼굴 | 원고 얼굴의 약 45%가 무표정·반쯤 감은 눈. 작은 홍채, 흰자 많음, 눈밑선(다크서클), 코는 짧은 획 | 무표정 기본값 + 짧게 터지는 과장 표정 |
| 선 | G펜, 굵고 느슨하며 조금 거침. 외곽:내부 굵기 약 2~3:1. 끊기고 겹친 선이 남음 | 굵은 실루엣 외곽 + 가는 안쪽 선, 굵기 흔들림 |
| 애니 3갈래 | TV 1기: 사실적 비율·절제된 색·영화 카메라, 그러나 선이 가늘어 "밋밋하다"는 비판. 레제편: **굵은 검은 외곽선 복원, 눈 하이라이트 제거, 그림자 양 줄임**. 룩백: 스케치 선 보존 | **TV 1기의 비율·색·카메라 + 레제편의 굵은 외곽·하이라이트 없는 눈·적은 그림자** |
| 컬러 | 공식 컬러 원고: 피부 #FBDEC9, 그림자 #CA9F91(모브 갈색), 흑발 #0E1630~#232C49(진남색), 외곽 #120C13. 애니: 채도·대비 억제, 보색 필터 | 중간~낮은 채도, 모브 그림자, 강조색 하나 |

### 1-3. 가져오는 것
1. 사실적인 등신과 해부(마른 몸은 마르게, 무거운 몸은 무겁게).
2. 실제 두상 위의 최소한의 얼굴 선: 작은 홍채, 하이라이트 없는 눈, 눈밑선, 짧은 코 획, 일자 입.
3. 무표정 기본 + 못생기게 일그러지는 감정 폭발(짧게).
4. 흑발 = 거의 검정의 평면 덩어리 + 흐름 따라 가는 밝은 틈 선.
5. 굵고 조금 거친 검은 외곽, 가는 안쪽 선.
6. 일상복의 사실적인 핏과 관절에만 몰린 주름.
7. 구부정한 서기, 주머니 손, 늘어뜨린 팔, 한쪽 다리에 무게.
8. 영화 같은 카메라(로우앵글+전선, 익스트림 클로즈업, 인물을 작게 넣은 와이드, 정면 나란히 서기).

### 1-4. 가져오지 않는 것 (기존 캐릭터 고유 요소 — 닮음 금지)
| 금지 | 이유 / 우리 쪽 위험 지점 |
|---|---|
| 톱니·상어 이빨, 송곳니 강조 | 덴지·파워 고유. 우리는 **평범한 사람 치아** |
| 동심원(링) 홍채 | 마키마·요루 고유. 쓰지 않는다(각성 연출도 다른 기호로) |
| 뿔, 체인소·시동 줄 모티프 | 파워·덴지 고유 |
| 정수리의 높은 상투(촌마게) + 검은 정장 + 흰 셔츠 + 넥타이 조합 | 아키 고유. **태오가 가장 위험** → 4-3·5-3 차별화 규칙 필수 |
| 입 아래 점 + 귀 피어싱 줄 | 요시다 고유 |
| 초커 | 레제 고유 |
| 손가락 총 포즈("빵") | 마키마 연상이 강함 → **쓰지 않는다**. 피스 사인·주머니 손은 OK |
| 흡연·음주 | 17세 규칙. 소품은 바나나우유 |
| 작가명·작품명·캐릭터명 | 프롬프트·마케팅 문구에 넣지 않는다(9장) |

> **화풍만 가져오고 캐릭터는 오리지널.** 얼굴 배치와 머리 모양 조합은 우리 것으로 만든다. 실루엣 테스트(검은 그림자만으로 시우·태오가 구분되는지, 기존 캐릭터가 떠오르지 않는지)를 시안마다 한다.

### 1-5. 두 보고서가 엇갈린 곳과 결정
| 쟁점 | 보고서 ① (이미지 실측) | 보고서 ② (제작 기사) | **결정** |
|---|---|---|---|
| 눈 하이라이트 | 작은 점 1개 | 없음(레제편 지침) | **인게임 기본 없음.** 컷신 초근접에서만 홍채 지름 10% 점 1개 허용 |
| 윗눈꺼풀 | 굵은 먹선, 눈꼬리 밖으로 10~15% 삐침 | 가는 단선, 날개 삭제 | 얼굴에서 **가장 굵은 선(외곽선의 1.5배)**, 꼬리는 눈 폭의 10%까지만. 아이라이너식 긴 날개는 삭제 |
| 컬러 모드 해칭 | 그림자에 45° 해칭 오버레이 | A에서는 해칭·톤 끔 | **A(인게임)는 해칭 끔.** 해칭·스크린톤·먹칠 규칙은 A′(흑백 모드: 회상·필살기 컷인·설정화)에 보존 |
| 그림자 양 | 임계값 0.45 | 그림자 줄이기 | 임계값은 지금 수준(피부 .30, 옷 .35) 유지, 경계만 또렷하게 |
| 외곽선 색 | #120C13(보라기 검정) | #1b1514(따뜻한 검정) | **#1A1417** (따뜻한 검정에 아주 약한 보라) |
| 만화 기호(땀방울·분노 십자) | U자 땀방울·눈물 줄 사용 | TV 1기는 기호 안 씀 | **이모트 기호 안 씀.** 눈물·코피·입술 상처 같은 물리 현상은 OK |
| 목 굵기 | 얼굴폭의 0.55~0.6 | 시우 neckR .035 유지 | **굵게**(실측 쪽). 지금 시우 목 지름 7cm는 실제 174cm 마른 소년(약 10cm)보다 가늘다 |
| 손 크기 | 머리높이 × 0.75~0.85 | 키의 10~11% | 두 기준이 같은 값이다(실제 손 ≈ 머리높이 × 0.83). 지금 값 거의 유지 |
| 태오 기본 표정 | 덤덤 | 무표정 기본 | 태오는 "잘 웃는" 인물 → **눈을 뜬 채 입꼬리 한쪽만 살짝 올린 반미소**가 기본. 지금의 실눈 웃음은 win 표정으로 옮김 |

---

## 2장. 인물 비율 수치

### 2-1. 원칙
- 등신 = 키 ÷ 머리 높이(정수리 피부~턱끝, **머리털 볼륨 제외**). 머리털은 정수리 위로 머리높이의 6~10%를 더한다.
- **두 사람의 머리 크기는 거의 같다**(23.5cm vs 24.4cm). 9cm 키 차이는 거의 다 몸통·다리에서 낸다.
- 2026-10-05 시우 키 변경(162 → 174cm) 때 **머리 크기는 거의 그대로**(23.1 → 23.5cm) 두고 몸통·다리를 늘려 등신을 다시 계산했다. 머리를 23.1cm로 딱 고정하면 7.53등신이지만, 키가 크면 머리도 조금 커지는 게 자연스럽고 1-2의 MAPPA 키 대비표(173cm 16세, 두개골 기준 약 7.45)와도 맞아 **7.4**로 정했다.
- 둘의 차이는 이제 키보다 **폭과 무게**에서 난다(어깨 약 38cm vs 53cm, 목 지름 9cm vs 12.4cm, 61kg vs 88kg). 실루엣 테스트도 폭으로 구분되는지 본다.
- 어깨는 수평보다 처진 경우가 많다. 대기 자세에서 어깨 관절을 3~6° 내리고 2~4° 앞으로 만다(태오는 처지지 않게).

### 2-2. 수치표
| 항목 | 반시우 (174cm / 61kg, 마름·밴텀급) | 강태오 (183cm / 88kg, 유도 -90kg) | 이 문서 전 코드(주술회전풍) |
|---|---|---|---|
| 등신 | **7.4** (머리 높이 23.5cm) | **7.5** (머리 높이 24.4cm) | 7.6 / 8.4 (머리 21.3 / 21.8cm, 너무 작음) |
| 다리(고관절 높이 ÷ 키) `legF` | **.52** (90cm) | **.53** (97cm) | .54 / .555 |
| 어깨폭(삼각근 바깥) ÷ 머리폭(머리털 포함) | **2.0 안팎** (약 38cm, 여전히 좁고 처짐) | **2.6~2.7** (약 53cm) | 오버핏 재킷 때문에 시우 약 2.5 |
| 어깨 관절 `sh` (반폭, m) | **.160** | **.215** 유지 | .165 / .215 |
| 목: 보이는 길이 | 머리높이 × .35 (약 8cm), 가늘고 김 | 머리높이 × .30, 짧아 보임 | — |
| 목: 굵기 `neckR` | **.045** (지름 9cm = 얼굴폭 × .58) | **.062** (턱폭과 같음) + 승모근 경사 | .035 / .045 |
| 승모근 | 거의 없음, 쇄골 V가 보임 | 목 옆에서 어깨 끝까지 사선(목 밑동보다 어깨 끝이 4~5cm 낮게) | 없음 |
| 손 길이 `hand` | **.180** (머리높이 × .77), 가늘고 손가락이 김, 손목뼈 | **.198** (머리높이 × .81), 두툼, 손가락 굵음, 마디 굳은살 | .178 / .195 (거의 유지) |
| 손가락 | 손 길이의 약 .5, 마디선·손톱 윤곽 | 같음, 마디가 굵음 | — |
| 얼굴폭 ÷ 머리높이 | **.66** (좁은 달걀형) | **.70** (사각 턱) | 구 가로 .76 / .80 |
| 몸통 | 납작한 가슴, 갈비뼈 쪽이 좁음, 허리 가늚. 배달·노가다로 붙은 잔근육(팔뚝 힘줄, 종아리)만 있고 덩어리 근육은 없음 | **통짜** 그래플러 체형: 가슴 두껍고 허리도 굵음(허리 ≈ 가슴의 .85), 복근 선 없음, 역삼각 과장 금지 | 태오 허리 과하게 좁음 |
| 팔 길이 | 차렷에서 손목이 가랑이 높이, 손끝이 허벅지 중간 | 같음 | — |
| 자세 | 등 살짝 굽힘, 고개 5~8° 기울임, 짝다리 | 발 넓게, 무게중심 낮음, 어깨 펴짐 | — |

> 등신 오차 ±0.3은 허용(원근·포즈 오차 범위). 최종 확인은 정면 정사영 스크린샷에서 머리 높이로 키를 나눠 잰다.
>
> 2026-10-05 시우 값 변경(162cm / 48kg → 174cm / 61kg): 등신 7.0 → 7.4, 고관절 84 → 90cm, 어깨폭 약 36 → 38cm, `sh` .150 → .160, `hand` .175 → .180, 위팔·아래팔(`upperL`·`foreL`, m 단위)은 키 비율(×1.07)만큼 늘림. 목 굵기 `neckR`와 얼굴폭 비율은 그대로다. 몸통·바지 반지름도 그대로 두는 것이 기본이다. 체중대로 계산하면 약 9% 굵어져야 하지만, 잔근육 위주의 가늘고 가벼운 인상이 이 캐릭터의 핵심이라 키만 늘린다. 화면에서 너무 앙상해 보이면 5%까지만 키운다.

---

## 3장. 얼굴 구조와 선

### 3-1. 왜 지금 얼굴이 "이상한가" (코드 기준 진단)
1. **구(sphere) + 등장방형 텍스처 얼굴**: 턱각·광대 아래 평면·눈두덩 오목·뒷머리 볼륨이 약해 "탈"처럼 보인다.
2. **이목구비 높이**: 지금 FACE_LAYOUT을 실제 변형된 머리에서 재 보면(정수리 0, 턱 1) 눈 .52, **코끝 .66**, 입 .79. 코끝이 높고 코~입 간격이 좁아 얼굴 아래쪽이 비어 보인다. 목표는 눈 .53, 코끝 .74, 입 .84.
3. **애니 기호**: 굵기 5→11의 윗눈꺼풀 + 날카로운 바깥 날개(아이라이너), 홍채 위 흰 하이라이트 점, 굵은 칼날 눈썹(시우는 화난 각도 고정)은 미소년·주술회전풍 기호다.
4. **머리가 작다**(7.6/8.4등신): 기본 카메라 거리에서 이목구비가 안 읽힌다.
5. **그림자 색**: 컬러 모드 그림자가 고유색을 푸른 먹색(#151722)으로 섞어 만든다 → 피부가 회색으로 탁해진다. 후지모토 컬러의 그림자는 모브 갈색 곱셈이다.
6. **상시 림라이트 .7**: "게임 셰이더" 티가 난다.

### 3-2. 세로 위치 (정수리 피부 0, 턱끝 1)
| 위치 | 목표 | 머리털 포함 높이 기준(조사 실측) |
|---|---|---|
| 눈선(눈 가운데) | **.53** | .55~.58 |
| 눈썹 | .43~.45 | .46 |
| 코끝 | **.74** | .75~.77 |
| 입 | **.84** | .85~.86 |
| 귀 | 눈썹 높이 ~ 코밑 높이, 얼굴 옆선보다 뒤 | — |

얼굴 삼등분: 이마(앞머리에 대부분 덮임) : 눈썹~코끝 : 코끝~턱 ≈ 1 : 1 : 0.85. **아래 얼굴이 짧고 콤팩트**한 것이 후지모토 쪽이다.

### 3-3. 얼굴 선 규칙 (선 굵기 = 실루엣 외곽선을 1.0으로)
| 부위 | 규칙 | 굵기 |
|---|---|---|
| 눈 모양 | 가로로 긴 아몬드, 세로:가로 = 1 : 3~3.5. 눈 하나 폭 = 얼굴폭의 .25~.28. 눈 사이 = 눈 하나 폭 × .9~1.1 | — |
| 윗눈꺼풀 | 얼굴에서 가장 진한 선. 가운데~바깥 1/3이 가장 굵고, 꼬리는 눈 폭의 10%까지만 바깥으로. **긴 날개 금지** | 1.5 |
| 쌍꺼풀 선 | 윗눈꺼풀 위에 따로, 가늘고 김. 피부 그림자색 | .5 |
| 아래눈꺼풀 | 바깥 2/3 구간만, 가는 선 + 짧은 아래 속눈썹 틱(시우 3개, 태오 2개) | .5 / 틱 .4 |
| 홍채 | 지름 = 눈 폭의 **.36**. 짙은 갈색(시우 #2B1E18, 태오 #3A2A20), 테두리가 더 진함. 윗눈꺼풀이 위쪽을 덮음(시우 35%, 태오 25%) → 아래 흰자가 살짝 보이는 삼백안 경향 | — |
| 동공 | 홍채의 **30%** 작은 점(외곽선 색) | — |
| 하이라이트 | **없음**(컷신 초근접에서만 홍채 지름 10% 점 1개) | — |
| 시선 | 기본은 카메라에서 살짝 빗나감. 정면 응시는 노려볼 때만(그래서 섬뜩해짐) | — |
| 다크서클 | 아래눈꺼풀 밑 곡선. **시우 2줄, 태오 1줄(아주 연하게)**. 피부 그림자색 | .4 |
| 눈썹 | 가늘고 곧은 한 획, 앞머리에 반쯤 가려짐. 감정은 미간 세로 주름 2줄로 보탬 | 시우 .6 / 태오 .8 |
| 코 | 콧대 선 없음. 그림자 쪽 짧은 세로선 1개(코 아래 40% 구간만) + 콧구멍 틱 1개. 태오만 코 옆면에 그림자 면 | .5 |
| 입 | 짧은 일자(눈 사이 간격보다 짧게) + 그 아래 짧은 대시(아랫입술 그림자). 립 색·광택 없음. 웃을 때 비대칭 | .7 / 대시 .4 |
| 이 | 흰 띠 + 세로 구분선 4~6개. 평범한 치아(송곳니 강조·톱니 금지) | .4 |
| 귀 | 사실적인 C자 + 안쪽 연골 선 1~2개. 피어싱 없음 | 외곽 .8 / 안 .4 |
| 턱·광대 | 턱끝은 작고 턱각이 살아 있음, 광대 아래 볼은 평평(볼살 없음). 턱 아래 목 그림자 띠 고정 | — |
| 홍조 | 볼·코끝·귀 가장자리에 연분홍(3D는 텍스처 면, 흑백 원고에서만 사선 3~5줄) | — |

### 3-4. 표정 (텍스처 4종: normal / hurt / ko / win)
과장 표정은 **0.3~0.6초**만 보여 주고 무표정으로 돌아온다. 눈 깜빡임은 느리게.

| 표정 | 반시우 | 강태오 |
|---|---|---|
| **normal** | 반쯤 감은 눈(윗눈꺼풀이 홍채 35% 덮음), 시선 살짝 옆, 눈썹 거의 수평(미간 쪽만 아주 조금 내려감, 지금의 화난 각도 3.4 → 1.0), 입 일자 + 아랫입술 대시, 다크서클 2줄, 콧등 반창고 | 편하게 뜬 눈(25% 덮음), 눈썹 수평, **입꼬리 한쪽만 살짝 올린 반미소**, 눈밑선 1줄 연하게, 볼에 아주 옅은 홍조 |
| **hurt** | 비대칭: 한쪽 눈은 꽉 감기고 반대쪽은 크게 떠서 홍채가 작아 보임. 미간 주름 2줄, **이를 악물어** 흰 띠 + 구분선, 입술 상처나 코피 한 줄 | 두 눈을 크게 떠 홍채 위아래로 흰자, 이 악물기, 코 주름 2줄. 웃던 얼굴이 무너지는 대비가 핵심 |
| **ko** | 지금의 X 눈 폐기 → **초점 없는 반쯤 뜬 눈**(홍채가 위·바깥으로 흐름, 동공 생략), 입이 힘없이 벌어짐(어두운 입 안 + 윗니 줄 살짝) | 눈 감김(윗눈꺼풀 선 하나 + 속눈썹 틱), 입 벌림 |
| **win** | 숨이 차서 입이 살짝 벌어진 채 **한쪽 입꼬리만 올라간 짧은 비웃음**(한쪽 윗니 몇 개 보임), 눈은 여전히 반쯤 감김. 터지는 웃음은 스토리 컷신에만 | 원래 기본이던 **실눈 웃음**(눈이 위로 휜 곡선) + 윗니 줄이 보이는 크게 벌린 웃음, 볼 홍조 진하게 |

---

## 4장. 머리카락

### 4-1. 공통 규칙
- **덩어리**: 뾰족한 다발(lock) 여러 개가 아니라 **둥글고 무겁게 내려앉은 큰 덩어리 3~5개 + 앞머리 4~7갈래**. 덩어리 경계에 V홈 3~4개를 파서 헬멧처럼 안 보이게 한다.
- **끝 처리**: 끝은 원뿔이 아니라 **납작한 쐐기**, 길이는 ±20% 제각각, 거칠게 흩어짐. 삐친 가닥 1~3개(알파 카드 또는 가는 지오메트리).
- **흑발 칠**: 거의 검정의 평면 **진남색**(3D 기본 #1A1F33). 셀 그림자는 앞머리 아래·귀 뒤 같은 가려진 곳에만 #121626. **스펙큘러 띠·엔젤링 금지.**
- **하이라이트**: 흐름 방향을 따라 가늘게 남긴 밝은 틈 선(#59617A, 폭 1~2%)을 **정수리 곡면에만** 4~8줄. 텍스처로 그린다(셰이더 하이라이트 아님).
- **외곽선**: 머리 실루엣은 굵게(1.0), 덩어리 사이 안쪽 경계는 .4.

### 4-2. 반시우 헤어 재정의
- **형태**: 덥수룩한 **반곱슬 미디엄**. 귀를 반쯤 덮고 뒷머리는 목덜미에 닿는 길이(멀릿처럼 길게 빼지 않음). 실루엣은 둥근 버섯형이 무겁게 내려앉고, 곱슬기는 끝이 안·밖으로 제각각 살짝 말리는 것으로만 표현.
- **앞머리(식별 요소 유지)**: 5~6갈래. **가장 긴 덩어리가 오른쪽 눈(캐릭터 기준, -X)의 위쪽 절반을 덮는다** — 눈썹과 윗눈꺼풀이 가려지고 홍채 아래쪽과 다크서클은 보이게. 나머지 갈래는 눈썹선 근처에서 길이가 들쭉날쭉.
- **삐친 가닥**: 정수리 1개, 왼쪽 옆머리 1개, 뒤통수 1개.
- **3D**: 지금의 정수리 다발 14개 + 뒷머리 11개 → **큰 덩어리 4개(정수리 앞·정수리 뒤·좌·우) + 뒷머리 덩어리 3개 + 앞머리 6개 + 삐침 카드 3개**로 재구성. 다발 `w`는 R×.55~.7로 넓게, `bend`는 약하게.
- 그 밖의 식별 요소: 콧등 반창고(베이지 #E8CFA6, 패드 #D9B98A), 회색 후드, 주황 밑창, 손 마디 흰 테이프 — 모두 유지(5장).

### 4-3. 강태오 헤어 재정의 (아키 상투와 확실히 다르게)
- **옆·뒤 투블럭**: 3~6mm로 짧게 밀어 **두피가 비치는** 짙은 회남색(#3D4252). 경계선이 귀 위에서 또렷하다. → 아키에게 없는 요소. 실루엣 테스트의 핵심.
- **윗머리 하프업 번**: 윗머리(7~10cm)만 뒤로 넘겨 **작고 헝클어진 반묶음 매듭**. 위치는 **정수리보다 아래, 뒤통수 위쪽 1/3** — 정면에서 머리 위로 솟아 보이지 않게(높이 솟은 상투 금지). 매듭 끝은 삐죽 흩어지고, **딥블루 고무줄**(#24489A, 작은 면적의 포인트).
- **잔머리**: 이마로 흘러내린 가는 잔머리 2가닥, 굵기·길이 다르게(S자 흐름 약하게).
- **이마**: 넘긴 이마가 드러난다(앞머리가 이마를 덮는 아키형 금지).
- **만두귀**: 왼쪽(+X) 귀가 부어 연골 형태가 뭉개진 덩어리. 다른 쪽 귀보다 두껍고 C자 안쪽 선이 끊겨 있다.
- **3D**: 윗머리 헬멧 느낌을 깨기 위해 빗어 넘긴 줄기 9개 → **넓은 덩어리 4~5개 + 덩어리 사이 V홈**, 번 지름은 지금(R×.31)의 70%로 줄이고 매듭 끝 삐침 카드 2개 추가.

---

## 5장. 옷

### 5-1. 공통 원칙
- **실제 교복 비율**: 어깨선이 대체로 맞고, 재킷 기장은 엉덩이(골반 아래 3~5cm)까지, 바지는 일자핏. **과장된 오버핏 코트·통 넓은 슬랙스 폐기**.
- **주름은 관절에만**: 팔꿈치(걷은 소매 지그재그 2~3줄), 겨드랑이 방사선 2~3줄, 바지에 넣은 셔츠 허리 가로주름, 무릎 뒤, 바지 밑단의 한 번 접힘. 한 벌에 **5~10개**.
- **주름 표현 방법**: 셰이더 가짜 주름(fold) 대신 **선 데칼 텍스처**(선 2~3개짜리). 지오메트리로는 소매 걷은 부분과 밑단 접힘만 부피를 준다.
- **칼라·넥타이**: 칼라 모서리는 날카롭게, 넥타이는 좁고 길게.
- 상표·글자·로고 없음.

### 5-2. 반시우
| 부위 | 규칙 |
|---|---|
| 재킷 | 사촌형(180cm, 덩치가 큼)에게 물려받은 **한 치수 큰** 남색 교복 재킷(설정 유지). 키 차이는 6cm뿐이라 남는 건 길이보다 **어깨와 품**이다. 단, 크기는 "한 치수"만: 어깨 솔기가 2~3cm 처지고, 기장은 엉덩이까지, 앞은 열림. 소매는 팔뚝 중간까지 **한두 번 접어 올림**(지그재그 주름 뭉침) |
| 안 | **회색 후드티**(무지), 후드가 재킷 깃 밖으로 나옴, 흰 끈 2개. 넥타이 없음 |
| 바지 | 짙은 남색 교복 바지, 일자핏, 밑단이 신발 위에서 **한 번** 접힘 |
| 신발 | 흰 하이탑 캔버스 운동화 + **주황 밑창**(이 캐릭터의 유일한 강조색), 앞코에 흰 테이프 한 줄 |
| 손 | 양손 마디에 흰 테이프, 손등에 자잘한 상처 |
| 실루엣 | 좁은 처진 어깨 + 살짝 남는 재킷 + 가는 다리. "가늘고 마른데 옷이 조금 큰" 느낌. 키는 평균이라 실루엣으로 눈에 띄지 않는 것이 맞다(행인1) |

### 5-3. 강태오 (아키 정장 실루엣 회피)
| 부위 | 규칙 |
|---|---|
| 기본 의상 | **재킷 없는 셔츠 차림**(스토리 바이블 원안: 재킷은 어깨에 걸침). 검은 정장 + 흰 셔츠 + 넥타이 실루엣을 피한다 |
| 셔츠 | 흰 교복 셔츠, 단추 두 개 풀고, 소매를 팔꿈치까지 걷어 굵은 팔뚝 노출. 바지 안에 넣어 허리 가로주름 |
| 넥타이 | 느슨하게 푼 **남색 교복 넥타이 + 가는 사선 줄무늬 1개(바랜 민트)** — 학교 넥타이임을 줄무늬로 보여 줌 |
| 재킷 | 쇼 포즈·컷신에서만: 남색 교복 재킷을 **한쪽 어깨에 걸침**(팔은 안 끼움). 인게임 기본 모델에서는 생략 |
| 바지·벨트 | 짙은 남색 교복 바지 일자핏 + 무광 검정 벨트 |
| 신발 | 검정 가죽 로퍼(광택은 흰 조각 하이라이트 1개) + **딥블루 양말** |
| 소품 | **바나나우유**(노란 항아리 모양 병 + 흰 뚜껑 + 빨대, 글자 없음) — 태오의 강조색. 유도복 가방(흰 도복을 검은 띠로 묶음)은 컷신·이동 장면용 |

---

## 6장. A 스타일 컬러 규칙 (절제된 극장판 애니 셀)

### 6-1. 원칙
1. **그림자 1단**, 경계는 또렷하게. 2단 그림자는 턱 아래와 앞머리 아래(가려진 곳)에만.
2. **그림자 = 고유색 × 곱셈값**(모브 쪽). 먹색과 섞지 않는다.
3. 인물 채도는 **중간~낮음**. 캐릭터마다 **강조색 1개**: 시우 = 주황 밑창, 태오 = 바나나우유 노랑.
4. 외곽선은 **따뜻한 검정 #1A1417**, 실루엣은 굵게, 안쪽은 가늘게.
5. 눈·피부·머리카락에 **스펙큘러 0**. 림라이트는 역광·가로등 컷에서만.
6. 해칭·스크린톤은 A에서 끈다(A′ 흑백 모드 전용).
7. 화면 전체에 **필름 느낌**: 종이 그레인, 보색 스플릿 톤, 채도 .85.

### 6-2. 색표
| 대상 | 기본색 | 그림자 1단 | 그림자 2단 / 비고 |
|---|---|---|---|
| 외곽선·얼굴 선(ink) | **#1A1417** | — | 피부 위 안쪽 선은 그림자색 쪽으로 30% 섞기 |
| 종이(paper) | **#F4EFE6** | — | 흰 셔츠 최대 밝기, 이·흰자 |
| 시우 피부 | **#BE8C6A** (지금 #C58B63에서 채도 15%↓, 다문화 설정 결정과 묶여 있어 밝기·색상 유지) | **#8F6252** (모브) | #715046 / 홍조 #B86E60 (불투명도 .3~.4) |
| 태오 피부 | **#F3D2BC** (지금 #E7BD97보다 밝게) | **#C29787** | #A37162 / 홍조 #EBA79C (.3~.4) |
| 흑발(공통) | **#1A1F33** (진남색 먹) | 가려진 곳만 #121626 | 틈 하이라이트 선 #59617A |
| 태오 투블럭 옆머리 | **#3D4252** | #2C303D | 두피 비침 표현 |
| 홍채 | 시우 #2B1E18 / 태오 #3A2A20 | — | 동공·테두리 = ink |
| 흰자 | #F4EFE6 | 윗눈꺼풀 그림자 #D9CFC6 | 윗부분 10%만 |
| 교복 재킷(남색) | **#2B3049** (지금 #252C4F에서 채도 30%↓) | #1E2238 | 라펠 #333955 |
| 교복 바지 | **#30323B** | #22232B | — |
| 흰 셔츠 | **#F4F2EC** | **#BEC2D0** (차가운 청회색) | — |
| 시우 회색 후드 | **#9E9FA4** | #737480 | 끈 #F4EFE2 |
| 시우 운동화 / 밑창 | #F2F0EA / **#E2582C** (강조) | #C3C4CC / #B24324 | 테이프 #F4EFE2 |
| 태오 넥타이 / 줄무늬 | #252B41 / #8FBFB0 | #191D2E | — |
| 태오 로퍼 / 양말·고무줄 | #1B1B20 / **#24489A** | #121216 | 로퍼 광택 조각 #F4EFE6 |
| 바나나우유 | 병 **#F2CF55** / 뚜껑·빨대 #F6F4EE | #C9A63C | 태오 강조색 |
| 반창고 | #E8CFA6 / 패드 #D9B98A | #BFA27C | — |

### 6-3. 셰이더 곱셈값 (role별 기본 그림자)
| role | 곱셈 (R, G, B) | 용도 |
|---|---|---|
| skin | (.80, .72, .72) | 밝은 피부. 시우는 `shadow` 지정값(#8F6252) 사용 권장(곱셈만으로는 주황빛이 강해짐) |
| face | (.93, .88, .88) | 텍스처 얼굴: 거의 평면, 앞머리·턱 그림자만 |
| light | (.78, .80, .88) | 흰 셔츠·운동화 (차가운 그림자) |
| mid | (.74, .74, .80) | 회색 후드 |
| dark | (.70, .70, .78) | 재킷·바지·머리 |
| spot | (.80, .74, .74) | 밑창·바나나우유 |
| 2단(가려진 곳) | 1단 × (.84, .80, .80) | 턱 아래, 앞머리 아래, 겨드랑이 |

### 6-4. 필름 후처리 (화면 전체)
- 채도 .85, 대비 약간 억제(감마 커브 끝 살짝 눕힘).
- **스플릿 톤**: 그림자 쪽 청록 #2F4A5A 6~8%, 밝은 쪽 따뜻한 #F3D9B0 5~6%. 밤 골목은 반대로 나트륨 가로등 주황 + 형광등 청록(<추격자> 톤).
- 종이 그레인 곱하기 3~5%(정지 텍스처, 프레임마다 미세 이동).
- 깊이 안개: 먼 배경을 종이색 #E9E0CC로 섞음(REFERENCES 2장 하늘과 같은 색).
- 블룸·렌즈 플레어·색수차 없음. 컷신만 2.39:1 레터박스 + 아주 약한 핸드헬드 흔들림.

### 6-5. `07_ink.js` color 모드와 `01_data.js` `STYLES.A`에 넣을 값
```js
// 01_data.js — STYLES.A (바뀌는 키만)
heads: { siwoo: 7.4, taeo: 7.5 },            // ★ STYLISH_DEF.heads보다 우선 적용되므로 반드시 같이 바꿀 것 (시우 7.0 → 7.4: 2026-10-05 174cm 변경)
outline: { w: .0075, color: '#1a1417', tint: false },
tone: 'film',                                 // 02_textures.js styleColor에 추가: hsl.s *= .85
ink: {
  mode: 'color', ink: '#1a1417', paper: '#f4efe6',
  shade: { skin: [.80, .72, .72], face: [.93, .88, .88], light: [.78, .80, .88], mid: [.74, .74, .80], dark: [.70, .70, .78], spot: [.80, .74, .74] },
  shade2: [.84, .80, .80],                    // 가려진 곳 2단
  hatchInColor: false,                        // A에서 해칭 끔 (A′ mono는 기존대로)
  hatch: { spacing: 5, width: 1.4, angle: .95 }, tone: { size: 4.5 },
  rim: .3, rimOn: false,                      // 상시 림 끔, 역광·밤 컷에서만 켬
  grain: .04, sat: .85, split: { shadow: '#2f4a5a', shadowK: .07, high: '#f3d9b0', highK: .05 },
},
fog: ['#e9e0cc', 30, 140],
card: { one: '사실적인 비율과 최소한의 얼굴 선. 모브 그림자 1단, 굵은 따뜻한 검정 외곽선, 절제된 영화 색.', ref: '청년만화 사실 비율 + 극장판 애니 셀(굵은 외곽·하이라이트 없는 눈)', pipe: 'Blender 스컬프트 두상·체형 → Unity URP 툰 셰이더(얼굴 SDF 그림자) + 데칼 선' /* 나머지 card 키 유지 */ },
```
```glsl
// 07_ink.js INK_SHADE — color 모드 그림자 계산 교체
// 전: vec3 sh = mix( base, uInk, uMix );
vec3 sh = base * uShade;                 // role별 곱셈 (uniform vec3, kit.solid의 o.shadow hex가 있으면 그 색을 그대로)
col = mix( sh, base, lit );
// role == 1(dark): 하이라이트 띠(hl)는 uHi 기본 .9 이상으로 거의 끔, rim은 uRimOn일 때만
// role != 3: color 모드 ink_hatch 줄 삭제 (uHatchInColor == 0)
```

---

## 7장. 3D로 옮기는 규칙

### 7-1. 지오메트리 조형
1. **두상**: 구 + 등장방형 텍스처를 버리고 **실제 두상 메시**(Blender 스컬프트 → 리토폴로지). 뒷머리 볼륨, 턱각, 작은 턱끝, 광대 아래 평면, 눈두덩 오목(깊이 3~4mm 상당), 낮은 코(콧등 돌출은 작게, 코끝만 살짝), 실제 위치의 귀(눈썹~코밑 높이, 얼굴 옆선보다 뒤).
2. **얼굴 노멀 정리**: 얼굴 셀 그림자가 지저분하게 갈라지지 않게 **얼굴 SDF 그림자 맵**(조명 각도별 경계 텍스처) 또는 **구에서 노멀 전사**. 코 그림자는 정해진 모양(그림자 쪽 작은 삼각형)만 나오게.
3. **목**: 2장 수치. 턱 아래 목 그림자 띠는 셰이더가 아니라 고정(텍스처/2단 그림자 마스크).
4. **손**: 지금 `buildHand` 구조(손바닥 + 마디 2개 손가락 + 엄지) 유지. 크기는 2장. 마디선·손톱 윤곽은 데칼. 기본 포즈 세트: 주먹, 늘어뜨린 손, 주머니 손(**숨기지 말고 엄지만 밖으로**), 깃 잡기(태오), 바나나우유 쥐기, 피스 사인. 손가락 총은 쓰지 않는다.
5. **옷**: 오버핏 셸을 줄이고(5장), 소매 걷은 뭉치와 밑단 접힘만 지오메트리로 부피.
6. **머리카락**: 4장. 큰 덩어리 + 쐐기 끝 + V홈 + 삐침 카드.

### 7-2. 얼굴 텍스처(선) 
- 프로토타입(three.js): `stylishFaceTexture` 캔버스를 유지하되 3장 규칙으로 다시 그림.
- Unity: 눈·눈썹·입을 **표정별 텍스처 교체 또는 데칼 아틀라스**(눈 4종 × 입 5종 조합). 블렌드셰이프는 턱 벌림·볼 올림 정도만.
- 선 색: 진한 선(윗눈꺼풀·동공·입) = ink #1A1417, 연한 선(쌍꺼풀·다크서클·코·아랫입술 대시) = 피부 그림자색.
- 기본 거리에서도 읽혀야 하는 순서: 윗눈꺼풀 > 홍채 > 입 > 눈썹 > 나머지. 1080p 기본 카메라에서 윗눈꺼풀이 최소 2px.

### 7-3. 셰이딩
- 2톤 셀(6-3 곱셈값), 경계 aa .02 수준으로 또렷하게. 임계값: 피부 .30, 옷 .35, 얼굴 .20.
- 2단 그림자는 가려진 곳만. 해칭·스크린톤 끔. 스펙큘러 0(로퍼 광택 조각만 예외). 림 끔(역광 컷 예외).
- 피격 번쩍임(`flashables`)은 그대로 유지.

### 7-4. 아웃라인
- 인버티드 헐 유지. **실루엣 1.0 / 머리 덩어리 경계·옷 안쪽 .4~.5 / 얼굴 안쪽 0**(얼굴 선은 텍스처).
- 그림자 쪽을 굵게(지금 `INK_HULL_VERT`의 extra 유지), 빛 쪽은 가늘게.
- 굵기에 저주파 노이즈 ±20~30%(정점 해시), 5~10% 확률로 짧게 끊김 → 손선 느낌.
- **코끝·입술·귀 안쪽에서는 헐이 생기지 않게** 정점 색(또는 마스크)으로 굵기 0.
- 1080p 기준 실루엣 2~3px, 안쪽 1px 안팎.

### 7-5. 기존 코드에서 바꿀 목록
**`01_data.js`**
- [ ] `STYLES.A.heads` → `{ siwoo: 7.4, taeo: 7.5 }` (이 값이 `STYLISH_DEF.heads`를 덮어쓴다 — 11_stylish.js 80행 부근)
- [ ] 시우 캐릭터 정의 `H` 1.62 → **1.74**(2026-10-05 키 변경). `build` .86은 마른 체형 계수라 유지. 같은 변경으로 `99_app.js` 스탯표(키·몸무게·리치·최종 체급 밴텀급)와 `page.html` 이름판(`EXTRA · 174CM 61KG`)도 새 값으로
- [ ] `STYLES.A.outline.color` `#14151d` → `#1a1417`, `ink.ink` `#151722` → `#1a1417`, `ink.paper` → `#f4efe6`
- [ ] `ink.rim` .7 → .3 + `rimOn: false`, `shade`/`shade2`/`hatchInColor`/`grain`/`sat`/`split` 추가(6-5)
- [ ] `tone: 'vivid'` → `'film'`(채도 ×.85), `fog` → `['#e9e0cc', 30, 140]`
- [ ] `card` 문구에서 주술회전 언급 삭제(6-5)
- [ ] `STYLES.I`(A′)도 `heads`만 같이 바꿈(비율은 공통)

**`02_textures.js`**
- [ ] `styleColor`에 `S.tone === 'film'` → `hsl.s *= .85`

**`07_ink.js`**
- [ ] color 모드 그림자: `mix(base, uInk, uMix)` → `base * uShade`(role별 vec3, `o.shadow` hex 지정 시 그 색) + 가려진 곳 2단
- [ ] color 모드 `ink_hatch` 줄 제거(또는 `uHatchInColor`로 끔)
- [ ] dark role 하이라이트 띠: 머리카락은 끔, 재킷·바지는 `hi` .9 이상
- [ ] 림: `uRimOn` 추가, 기본 0
- [ ] 헐 굵기 노이즈 ±25%·끊김, 정점 색 마스크로 부위별 굵기 0
- [ ] (후처리 담당과 함께) 그레인·스플릿 톤·채도 .85 패스

**`10_faces.js`**
- [ ] `FACE_LAYOUT` → `{ eyeU: 24, eyeV: 6, eyeW: 11.5, browV: -6, noseV: 30, mouthV: 43, chinV: 64 }` (지금 머리 변형 기준으로 계산하면 눈 .53·코끝 .74·입 .84. 두상 메시를 바꾸면 다시 잼)
- [ ] 81행 홍채 위 흰 하이라이트 점 삭제(컷신 옵션으로만)
- [ ] 84행 윗눈꺼풀 brush 굵기 5→11 → **3.5→6**, 85행 바깥 날개 brush 삭제 → 눈 폭 10% 길이의 짧은 꼬리로 대체
- [ ] 홍채 반지름 `ir` 3.4 → **2.9**, 동공 `ir*.55` → **`ir*.3`**, 윗눈꺼풀이 홍채 위 30~35%를 덮게 `top` 조정
- [ ] 아래눈꺼풀: 바깥 2/3 가는 선(굵기 2) + 속눈썹 틱(시우 3, 태오 2)
- [ ] 다크서클 추가: 시우 2줄, 태오 1줄(피부 그림자색, 굵기 1.6)
- [ ] 눈썹 `thick` 8 / 10.5 → **4.5 / 5.5**, 거의 직선. 시우 `angry` 3.4 → normal 1.0(화난 각도는 hurt에만)
- [ ] 코: 콧대 선을 아래 40% 구간으로 줄이고 색을 그림자색으로, 콧구멍 틱 1개
- [ ] 입: 태오 normal = 일자에 한쪽 입꼬리만 올린 반미소(지금 위로 휜 brush 대체), 실눈 웃음 + 이 보이는 웃음은 win으로
- [ ] ko: X 눈 폐기 → 초점 없는 반쯤 뜬 눈 + 벌어진 입
- [ ] hurt: 비대칭 눈(시우) / 크게 뜬 눈(태오) + 이 악물기(흰 띠 + 구분선 4~6) + 미간 주름 2줄
- [ ] 볼 홍조: color 모드는 사선 해칭 대신 연분홍 타원 면(불투명도 .3), 코끝·귀에도. 사선 해칭은 mono 전용
- [ ] 얼굴 ink `#1a1520` → `#1a1417`

**`11_stylish.js`**
- [ ] `STYLISH_DEF.siwoo`: `heads` 7.4, `legF` .52, `sh` .160, `hip` .088, `upperL` .305, `foreL` .268, `neckR` .045, `hand` .180, `skin` #be8c6a, `jacket` #2b3049, `pants` #30323b, `inner` #9e9fa4, `sole` #e2582c
- [ ] `STYLISH_DEF.siwoo.coat`: `hem` .14 → .06, `flare` .1 → .05, `w` 폭 전체 ×.85(처진 어깨 2~3cm만)
- [ ] `STYLISH_DEF.siwoo.pantsR` [.088,.077,.075,.08] → **[.082,.068,.062,.064]**(일자핏)
- [ ] `STYLISH_DEF.taeo`: `heads` 7.5, `legF` .53, `neckR` .062, `hand` .198, `skin` #f3d2bc, `torso` 허리(.09~.16 구간) 반지름 +10%로 통짜 체형, 기본 재킷 생략(쇼 포즈용 어깨 걸침 메시 별도), `tie` #252b41 + 줄무늬, 양말 #24489a
- [ ] 목: 태오만 승모근 사선(목 밑동~어깨 끝) 메시 추가
- [ ] `buildHead`: `JW.w` .76/.80 → **.70/.74**, 턱각 강조(lat 40~55°, lon ±45° 부근 가로 유지 후 급격히 좁힘), 눈두덩 오목 .02 → .035, 코 돌출 .1 → .06, 귀를 C자 메시(안쪽 연골 선 데칼)로
- [ ] `hairSiwoo`/`hairTaeo`: 4장 구조로 재구성. 머리 재질 `hi .8` → 끔, `rimM .45` → 0, `fold` → 0
- [ ] 재질의 `fold`(가짜 주름) 전부 0 → 선 데칼로 대체
- [ ] 태오 쇼 포즈 왼손 `hide: true` → 주머니 손을 보여 줌(엄지 밖)
- [ ] 대기 자세: 골반 3~5° 기울임 + 한쪽 무릎 굽힘 + 고개 5~8° 기울임(시우는 어깨 앞으로 말기)

**Unity 이전 시(파이프라인)**
- VRoid 베이스는 애니 얼굴 비율이 박혀 있어 이 화풍과 맞지 않는다 → **Blender에서 두상·체형 직접 스컬프트**(위 수치), Unity URP + 툰 셰이더(얼굴 SDF 그림자, 정점 색 아웃라인), 표정은 텍스처 교체.

---

## 8장. 한옥 골목 배경(REFERENCES 2장)과 어울리게

1. **선 위계**: 캐릭터 실루엣 외곽(1.0) > 근경 건물(.6) > 원경(.3, 해칭으로 대체). 캐릭터가 항상 가장 굵은 선을 가져 배경에서 떠 보이게.
2. **채도 위계**: 배경은 REFERENCES 2장 팔레트(크림 하늘 #E9E0CC, 먹회색 기와 #3A3A3A, 바랜 주칠 #9B4A3A, 모래 땅 #D9C9A8) 그대로, 캐릭터는 그보다 조금 높은 채도. **강조색(주황 밑창, 바나나우유 노랑)은 배경에 쓰지 않는다** — 배경의 주칠(#9B4A3A)은 채도가 낮아 시우 주황(#E2582C)과 겹치지 않는다.
3. **그림자 색 통일**: 배경 그림자도 같은 방식(고유색 × 곱셈)으로, 배경은 따뜻한 회색 쪽(.78, .76, .74), 캐릭터는 모브 쪽. 먹색 섞기 금지(같은 이유: 탁해짐).
4. **종이 바탕 공유**: 하늘·안개·그레인 모두 같은 종이색 계열(#E9E0CC ~ #F4EFE6)이라 캐릭터 흰 셔츠와 하늘이 같은 "종이"로 읽힌다.
5. **전봇대·전선**: 후지모토식 로우앵글의 시그니처. 혜화동 골목에 전봇대와 처진 전선을 전경·하늘에 꼭 넣는다(04_env의 `wire`/`pole` 활용). 로우앵글 컷의 하늘을 전선이 가로지르게.
6. **여백**: 컷신·메뉴는 인물을 화면 1/3 구석에 작게, 종이색 하늘과 성곽 벽을 크게 남긴다.
7. **카메라 프리셋**: ① 로우앵글(높이 0.6m, 위로 15~25°, 24mm 상당, 전선 포함) ② 85mm 얼굴·눈 초근접 ③ 와이드(인물 화면 높이 20% 이하) ④ 낮은 고정 정면(두 사람 나란히) ⑤ 등 뒤 트래킹.
8. **밤 골목**: 나트륨 가로등 주황 + 편의점 형광등 청록, 젖은 바닥 반사 — 스플릿 톤을 장면 감정에 맞춰 바꾼다(6-4).
9. **타격 연출**: 긴 정적(음악 끔, 환경음만) → 첫 타격에 히트스톱 + 먹 이펙트(`fx:'ink'`) + 1~2프레임 흑백 반전 "쇼크 컷". 먹 튐은 배경의 먹 외곽선과 같은 색(#1A1417).

---

## 9장. 설정화 프롬프트 (힉스필드 GPT Image 2.5)

규칙: **작가명·작품명·기존 캐릭터명은 넣지 않는다.** 화풍은 서술어로. 모든 프롬프트에 담배·술·상표·글자 금지 문구를 넣는다. 생성 전 크레딧을 확인하고, **결제가 필요하면 대표에게 먼저 말한다.** 한 장 먼저 뽑아 확인한 뒤 나머지를 돌린다. 결과물은 `concept/art/fujimoto/`에 `<id>_v1.png`로 저장.

| id | 인물 | 용도 | 비율 |
|---|---|---|---|
| siwoo_turn | 시우 | 전신 턴어라운드(흑백) | 3:2 |
| taeo_turn | 태오 | 전신 턴어라운드(흑백) | 3:2 |
| siwoo_face | 시우 | 얼굴 시트 + 표정(흑백) | 3:2 |
| taeo_face | 태오 | 얼굴 시트 + 표정(흑백) | 3:2 |
| siwoo_color | 시우 | 정면 전신 컬러(A) | 2:3 |
| taeo_color | 태오 | 정면 전신 컬러(A) | 2:3 |
| duo_show | 둘 | 쇼 포즈 키 비주얼(컬러, 혜화동) | 3:2 |
| hands_sheet | 둘 | 손 시트(흑백) | 3:2 |
| poses_sheet | 둘 | 자세 시트(흑백) | 3:2 |

프롬프트 원문은 아래 부록 B에 있다(작업 결과로 함께 반환됨).

---

## 부록 A. 출처(요약)
- 이미지 관찰: 핀터레스트 공개 검색 10종(藤本タツキ, chainsaw man manga panel, ルックバック 漫画, fire punch manga, チェンソーマン 設定資料 등) 첫 화면 166장. 실측 핵심: MAPPA 키 대비표, 촌마게 청년 턴어라운드(벨트선 = 머리 4.1개), 공식 컬러 원고 "KON."(색 추출), 11권 표지, 셔츠 소년·정장 청년 반신(주름 수).
- 제작 기사: Wikipedia(작가·TV 시리즈), Sakugabooru Blog 제작 노트, ANN "The Chainsaw Man Anime's Style Feels Off", Anime Herald(영화적 사실성), Slashfilm·Yahoo(레제편 화풍 변경: 굵은 외곽, 하이라이트 제거, 그림자 줄임), AWN, CGWORLD 체인소맨 1~4·레제편(3DCG·촬영: 보색 필터, 깊이 공기감, 핸디캠), 리얼사운드(일상 연기, 안녕 에리 카메라), Anime Corner 오시야마 인터뷰(룩백 스케치 선), Brad Luen(자세로 인물 만들기).
- 한계: 핀터레스트는 검색어당 첫 화면만 봤다. 팬아트가 섞여 있어 수치는 공식 자료 기준. 작가의 작화 도구(아날로그/디지털)는 1차 출처를 찾지 못했다.

## 부록 B. 프롬프트 원문 (영어)

### 공통 블록
- **STYLE_BW**: Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic.
- **STYLE_COLOR**: Muted cinematic anime cel shading, like a still from a grounded theatrical anime film: flat colors with one crisp shadow tone (a second darker tone only under the chin and beneath the fringe), skin shadows in soft mauve-brown, shadows on white cloth in cool blue-grey, soft pink flush on cheeks, nose tip, ears and knuckles. Bold, slightly rough warm near-black outlines (#1A1417), thick on the silhouette and thin inside. Low-to-medium saturation with a single accent color, no gradients on the character, no glossy highlights, no rim-light glow, no eye sparkle, very subtle paper grain.
- **AVOID**: Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8–9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.

(각 프롬프트 전문은 아래 — 공통 블록을 풀어 쓴 완성본이다. 시우 값은 2026-10-05 변경분(174cm / 61kg, 7.4등신)으로 고쳐 두었다. 이미 만든 그림에 실제로 보낸 원문(옛 값)과, 머리 높이 안내선을 넣은 재생성용 판은 `art/fujimoto/README.md`에 있다.)

### siwoo_turn (siwoo, 3:2)
- 용도: 시우 전신 턴어라운드(정면·3/4·옆·뒤), 흑백 망가 펜화. 7.4등신·처진 어깨·한 치수 큰 재킷 핏 확인용

```text
Character turnaround model sheet in black-and-white manga ink. Four full-body views of the same original character standing side by side at identical scale on one shared ground line, evenly spaced: front view, three-quarter front view, side profile view, back view. Relaxed natural standing pose, not a T-pose: slightly hunched, arms hanging loosely, weight on one leg, head tilted a little. Faint thin horizontal guide lines run across the whole sheet at the top of the head, chin, shoulders, waist, crotch and knees so the proportions match between views; the whole body is visible from hair tip to shoe soles in every view. The character is a 17-year-old Korean high-school boy (second-year student), 174 cm tall (average height for his age), thin and wiry (61 kg) - a light, narrow frame with lean, ropy muscles and no bulk, not skeletal - about 7.4 heads tall with realistic proportions, a normal-sized head on a thin body of average height, not a lanky long-legged fashion model: narrow sloping shoulders about twice his head width, a long thin neck with visible collarbones, slim wrists, slightly hunched posture. Lightly sun-tanned warm-brown skin. Narrow oval face with a defined jaw angle and a small chin, flat cheeks, no baby fat. Messy, slightly curly black hair of medium length that half covers his ears and touches the nape, sitting in heavy rounded clumps with uneven, roughly wedge-shaped ends that curl slightly in different directions; a fringe of five or six clumps of different lengths, the longest clump hanging over his right eye and covering the eyebrow and upper eyelid on that side; two or three stray strands sticking out. Heavy-lidded, tired, half-closed eyes with small dark-brown irises partly covered by the upper lids, gaze slightly off-center, two faint curved dark-circle lines under each eye, thin straight eyebrows half hidden by the fringe, a plain beige adhesive bandage across the bridge of his nose, a short flat mouth with a short dash under the lower lip, realistic C-shaped ears. Outfit: a navy school blazer one size too big (a hand-me-down) worn open, shoulder seams dropping slightly, hip length, sleeves pushed up and rolled to mid-forearm with zigzag folds; a plain heather-grey pullover hoodie underneath with the hood lying out over the blazer collar and two white drawstrings; no necktie; straight-leg dark navy school trousers whose hems break once over the shoes; white high-top canvas sneakers with bright orange rubber soles and a strip of white tape around the toe; white athletic tape wrapped around the knuckles of both hands; small scratches on the backs of his hands. Plain unbranded clothing. The back view shows the hood lying over the blazer collar and the hair touching the nape. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Plain white background. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### taeo_turn (taeo, 3:2)
- 용도: 태오 전신 턴어라운드(정면·3/4·옆·뒤), 흑백 망가 펜화. 7.5등신·통짜 체형·투블럭과 낮은 번 위치 확인용

```text
Character turnaround model sheet in black-and-white manga ink. Four full-body views of the same original character standing side by side at identical scale on one shared ground line, evenly spaced: front view, three-quarter front view, side profile view, back view. Relaxed natural standing pose, not a T-pose: feet planted about shoulder-width apart, shoulders square and not slumped, arms hanging with slightly curled fingers. Faint thin horizontal guide lines run across the whole sheet at the top of the head, chin, shoulders, waist, crotch and knees so the proportions match between views; the whole body is visible from hair tip to shoe soles in every view. The character is a 17-year-old Korean high-school boy (second-year student) and judo athlete, 183 cm and 88 kg, about 7.5 heads tall with realistic proportions: broad shoulders about 2.6 times his head width, a thick neck as wide as his jaw with sloping trapezius muscles, thick forearms and wrists, big but realistic hands with thick fingers, a solid heavy grappler's body with a thick chest and a thick waist (not a bodybuilder, no visible abs, no exaggerated V-shape), low center of gravity. Fair light skin with warm pink tones on the cheeks, nose tip, ears and knuckles. Hair: a two-block undercut - the sides and back clipped very short so the scalp shows through, with a crisp line above the ears; the longer black top hair is combed back and tied into a small, slightly messy half-up knot low on the back of the crown (not a high topknot; it does not stick up above the head in front view), loose ends sticking out, held with a deep-blue hair tie; two thin loose strands of different lengths fall onto his bare forehead. His left ear is a cauliflower ear (swollen, lumpy cartilage). Longer face with a square jaw, relaxed open eyes with small dark-brown irises, straight eyebrows slightly thicker than average, one faint line under each eye, a short nose stroke with a little shading on the side of the nose, an easy lopsided half-smile with only one mouth corner raised. Outfit: a white school dress shirt tucked in, top two buttons undone, sleeves rolled to the elbows showing thick forearms; a loosened navy school necktie with one thin faded-mint diagonal stripe; dark navy straight school trousers with a plain matte black belt; black leather loafers with deep-blue socks. No suit jacket. Plain unbranded clothing. The side profile shows his left side with the cauliflower ear; the back view shows the clipped undercut line and the small low knot. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Plain white background. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### siwoo_face (siwoo, 3:2)
- 용도: 시우 얼굴 시트(정면·3/4·옆) + 표정 normal/hurt/win, 흑백. 얼굴 텍스처·두상 메시 기준

```text
Head and face model sheet in black-and-white manga ink for an original character, on a plain white background: six heads at the same scale in two rows of three, each drawn from the collarbones up with the hood and blazer collar visible. Top row, neutral expression: front view, three-quarter view, side profile. Bottom row, front views of three expressions: (1) default deadpan - heavy half-lidded eyes looking slightly off to the side, flat mouth; (2) hurt - one eye squeezed shut and the other open wide with a tiny iris, brows drawn together with two short vertical creases between them, teeth gritted showing an even row of ordinary human teeth, a small cut on the lower lip; (3) win - eyes still half-lidded, breathless, mouth slightly open with one corner pulled up in a short lopsided smirk showing a few upper teeth on one side. The character is a 17-year-old Korean high-school boy, 174 cm, thin and wiry, with a narrow oval face, a defined jaw angle and a small chin, no baby fat, lightly sun-tanned warm-brown skin, a long thin neck, messy slightly curly black medium-length hair in heavy rounded clumps with uneven wedge-shaped ends, the longest fringe clump covering his right eyebrow and the top of his right eye, two or three stray strands, two faint curved dark-circle lines under each eye, thin straight eyebrows, a plain beige adhesive bandage across the bridge of his nose, a short flat mouth with a short dash under the lower lip, realistic C-shaped ears; a heather-grey hoodie with the hood out over an open navy school blazer collar. Proportions: eyes at about the middle of the head, nose tip about three-quarters of the way down, mouth close above a compact chin; realistic skull with back-of-head volume, eye sockets, flat cheeks, a low nose and realistic ears placed between brow and nose-base height. Eyes are long horizontal almonds; irises are about a third of the eye width, cut off at the top by the upper lid, with a small dot pupil and no highlight; the outer two-thirds of the lower lid has a thin line with two or three tiny lash ticks. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### taeo_face (taeo, 3:2)
- 용도: 태오 얼굴 시트(정면·3/4·옆) + 표정 normal/hurt/win, 흑백. 만두귀·낮은 번·사각 턱 기준

```text
Head and face model sheet in black-and-white manga ink for an original character, on a plain white background: six heads at the same scale in two rows of three, each drawn from the collarbones up with the open shirt collar and loosened tie visible. Top row, neutral expression: front view, three-quarter view, side profile showing his left side with the cauliflower ear and the small low knot at the back of the head. Bottom row, front views of three expressions: (1) default - relaxed open eyes and an easy lopsided half-smile with one mouth corner raised; (2) hurt - both eyes wide open with small irises and white showing above and below, teeth gritted showing an even row of ordinary human teeth, nose wrinkled with two short lines, the easy smile collapsed; (3) win - eyes closed into gentle upward arcs, a big open grin showing an even upper row of ordinary teeth, flushed cheeks drawn with a few short diagonal lines. The character is a 17-year-old Korean high-school judo athlete, 183 cm and 88 kg, with a longer face and a square jaw, fair light skin, a thick neck as wide as his jaw with sloping trapezius muscles, a two-block undercut with the sides and back clipped very short so the scalp shows through, black top hair combed back into a small, slightly messy half-up knot low on the back of the crown (not a high topknot) held by a hair tie, two thin loose strands of different lengths on his bare forehead, a swollen lumpy cauliflower left ear, straight eyebrows slightly thicker than average, one faint line under each eye, a short nose stroke with a little shading on the side; a white school shirt with two buttons undone and a loosened navy necktie with a thin diagonal stripe. Proportions: eyes at about the middle of the head, nose tip about three-quarters of the way down, mouth close above a compact chin; realistic skull with back-of-head volume, eye sockets, flat cheeks, a low nose and realistic ears placed between brow and nose-base height. Eyes are long horizontal almonds; irises are about a third of the eye width, cut off at the top by the upper lid, with a small dot pupil and no highlight; the outer two-thirds of the lower lid has a thin line with two or three tiny lash ticks. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### siwoo_color (siwoo, 2:3)
- 용도: 시우 정면 전신 컬러(A 스타일 색 기준: 피부·모브 그림자·강조색 주황 밑창)

```text
Full-body front-view color character illustration of an original character, standing relaxed and slightly slouched with his weight on one leg, his left hand in his trouser pocket with the thumb out and his right arm hanging so the taped knuckles show, head tilted a little, deadpan stare just past the viewer. The character is a 17-year-old Korean high-school boy (second-year student), 174 cm tall (average height for his age), thin and wiry (61 kg) - a light, narrow frame with lean, ropy muscles and no bulk, not skeletal - about 7.4 heads tall with realistic proportions, a normal-sized head on a thin body of average height, not a lanky long-legged fashion model: narrow sloping shoulders about twice his head width, a long thin neck with visible collarbones, slim wrists, slightly hunched posture. Lightly sun-tanned warm-brown skin. Narrow oval face with a defined jaw angle and a small chin, flat cheeks, no baby fat. Messy, slightly curly black hair of medium length that half covers his ears and touches the nape, sitting in heavy rounded clumps with uneven, roughly wedge-shaped ends that curl slightly in different directions; a fringe of five or six clumps of different lengths, the longest clump hanging over his right eye and covering the eyebrow and upper eyelid on that side; two or three stray strands sticking out. Heavy-lidded, tired, half-closed eyes with small dark-brown irises partly covered by the upper lids, gaze slightly off-center, two faint curved dark-circle lines under each eye, thin straight eyebrows half hidden by the fringe, a plain beige adhesive bandage across the bridge of his nose, a short flat mouth with a short dash under the lower lip, realistic C-shaped ears. Outfit: a navy school blazer one size too big (a hand-me-down) worn open, shoulder seams dropping slightly, hip length, sleeves pushed up and rolled to mid-forearm with zigzag folds; a plain heather-grey pullover hoodie underneath with the hood lying out over the blazer collar and two white drawstrings; no necktie; straight-leg dark navy school trousers whose hems break once over the shoes; white high-top canvas sneakers with bright orange rubber soles and a strip of white tape around the toe; white athletic tape wrapped around the knuckles of both hands; small scratches on the backs of his hands. Plain unbranded clothing. Colors: warm tan skin (#BE8C6A) with mauve-brown shadows (#8F6252), deep blue-black flat hair (#1A1F33) with a few thin lighter strand lines (#59617A) near the crown, a dusty desaturated navy blazer (#2B3049), a heather-grey hoodie (#9E9FA4), dark navy trousers (#30323B), off-white sneakers, and the bright orange soles (#E2582C) as the only strong accent color. Plain warm off-white paper background (#F4EFE6) with a soft grounding shadow under the feet, whole body visible from hair to soles. Muted cinematic anime cel shading, like a still from a grounded theatrical anime film: flat colors with one crisp shadow tone (a second darker tone only under the chin and beneath the fringe), skin shadows in soft mauve-brown, shadows on white cloth in cool blue-grey, soft pink flush on cheeks, nose tip, ears and knuckles. Bold, slightly rough warm near-black outlines (#1A1417), thick on the silhouette and thin inside. Low-to-medium saturation with a single accent color, no gradients on the characters, no glossy highlights, no rim-light glow, no eye sparkle, very subtle paper grain. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### taeo_color (taeo, 2:3)
- 용도: 태오 정면 전신 컬러(A 스타일 색 기준: 밝은 피부·모브 그림자·강조색 바나나우유 노랑)

```text
Full-body front-view color character illustration of an original character standing in a relaxed contrapposto, weight on his right leg, left hand in his trouser pocket with the thumb hooked out, right hand holding a small banana-milk bottle at chest height - a squat jar-shaped pale-yellow plastic bottle with a white cap and a white straw, completely blank with no label or text - head tilted slightly, easy lopsided half-smile, looking at the viewer. The character is a 17-year-old Korean high-school boy (second-year student) and judo athlete, 183 cm and 88 kg, about 7.5 heads tall with realistic proportions: broad shoulders about 2.6 times his head width, a thick neck as wide as his jaw with sloping trapezius muscles, thick forearms and wrists, big but realistic hands with thick fingers, a solid heavy grappler's body with a thick chest and a thick waist (not a bodybuilder, no visible abs, no exaggerated V-shape), low center of gravity. Fair light skin with warm pink tones on the cheeks, nose tip, ears and knuckles. Hair: a two-block undercut - the sides and back clipped very short so the scalp shows through, with a crisp line above the ears; the longer black top hair is combed back and tied into a small, slightly messy half-up knot low on the back of the crown (not a high topknot; it does not stick up above the head in front view), loose ends sticking out, held with a deep-blue hair tie; two thin loose strands of different lengths fall onto his bare forehead. His left ear is a cauliflower ear (swollen, lumpy cartilage). Longer face with a square jaw, relaxed open eyes with small dark-brown irises, straight eyebrows slightly thicker than average, one faint line under each eye, a short nose stroke with a little shading on the side of the nose, an easy lopsided half-smile with only one mouth corner raised. Outfit: a white school dress shirt tucked in, top two buttons undone, sleeves rolled to the elbows showing thick forearms; a loosened navy school necktie with one thin faded-mint diagonal stripe; dark navy straight school trousers with a plain matte black belt; black leather loafers with deep-blue socks. No suit jacket. Plain unbranded clothing. Colors: fair skin (#F3D2BC) with mauve shadows (#C29787) and pink flush on the cheeks, ears and knuckles, deep blue-black hair (#1A1F33) with very short grey-navy clipped sides (#3D4252), a white shirt (#F4F2EC) with cool blue-grey shadows (#BEC2D0), a navy tie (#252B41) with a faded mint stripe, dark navy trousers (#30323B), black loafers, small deep-blue accents on the hair tie and socks (#24489A), and the yellow banana-milk bottle (#F2CF55) as the main accent color. Plain warm off-white paper background (#F4EFE6) with a soft grounding shadow, whole body visible from hair to soles. Muted cinematic anime cel shading, like a still from a grounded theatrical anime film: flat colors with one crisp shadow tone (a second darker tone only under the chin and beneath the fringe), skin shadows in soft mauve-brown, shadows on white cloth in cool blue-grey, soft pink flush on cheeks, nose tip, ears and knuckles. Bold, slightly rough warm near-black outlines (#1A1417), thick on the silhouette and thin inside. Low-to-medium saturation with a single accent color, no gradients on the characters, no glossy highlights, no rim-light glow, no eye sparkle, very subtle paper grain. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### duo_show (both, 3:2)
- 용도: 두 사람 쇼 포즈 키 비주얼: 시우 양키 스쿼트로 카메라 노려봄 + 태오 콘트라포스토·바나나우유, 로우앵글, 혜화동 성곽·한옥 골목(REFERENCES 2장 팔레트)·전선

```text
Low-angle cinematic color key visual of two original teenage characters in a hillside alley of Hyehwa-dong, Seoul, in late-afternoon light. The camera is close to the ground looking up about 20 degrees through a wide 24mm-like lens. Front left: a thin, wiry 17-year-old Korean schoolboy (174 cm, 61 kg, about 7.4 heads tall, narrow sloping shoulders) with lightly sun-tanned warm-brown skin, messy slightly curly black medium-length hair in heavy clumps whose longest fringe clump covers his right eye, a plain beige bandage across the bridge of his nose, faint dark circles under heavy-lidded eyes, an open one-size-too-big navy school blazer with sleeves rolled to mid-forearm over a heather-grey hoodie with the hood out, dark navy straight trousers, white high-top sneakers with bright orange soles, and white tape on his knuckles, crouching in a deep street squat - heels flat on the ground, knees spread wide, forearms resting on his knees with hands hanging loosely and the taped knuckles visible - head tilted to one side, glaring straight into the camera with heavy-lidded eyes. Behind him on the right: a broad-shouldered 17-year-old Korean judo athlete (183 cm, 88 kg, about 7.5 heads tall, thick neck, solid heavy build with a thick waist) with fair skin, a two-block undercut with very short sides and a small messy half-up knot low on the back of his head tied with a deep-blue hair tie, two loose strands on his bare forehead, a swollen cauliflower left ear, an easy lopsided half-smile, a white school shirt with rolled sleeves and two open buttons, a loosened navy tie with a thin mint stripe, dark navy straight trousers, a black belt and black loafers, standing in a relaxed contrapposto with his navy school blazer draped over his shoulders and his arms not in the sleeves, left hand in his trouser pocket, right hand holding a small blank jar-shaped yellow banana-milk bottle with a straw near his chest, head tilted, looking down at the camera. Both have realistic proportions; the boy is only a little shorter than the judo athlete but much narrower and lighter, so the difference in width and mass between them is clear. Background: the old Seoul city wall of large pale grey-beige granite blocks climbing the slope, and a narrow hanok alley - dark charcoal-grey tiled roofs (#3A3A3A) with rows of small white dots along the roof-end tiles and reddish-brown rafter ends, faded vermilion-red wooden pillars, doors and lattice windows (#9B4A3A), lower walls of grey stone blocks with thick dark mortar lines under white plaster upper walls, sandy beige ground (#D9C9A8) with a few dark specks. A flat cream paper-colored sky (#E9E0CC) with no gradient, crossed by sagging electric wires from a concrete utility pole, and a distant grey mountain ridge (#8F8A80) with fine hatching on the horizon. The background is painted in flat low-saturation colors with thin ink outlines and almost no shadow, slightly less saturated and thinner-lined than the characters so they stand out; the only strong accents are the orange sneaker soles and the yellow banana-milk bottle. No shop signs and no text anywhere. Muted cinematic anime cel shading, like a still from a grounded theatrical anime film: flat colors with one crisp shadow tone (a second darker tone only under the chin and beneath the fringe), skin shadows in soft mauve-brown, shadows on white cloth in cool blue-grey, soft pink flush on cheeks, nose tip, ears and knuckles. Bold, slightly rough warm near-black outlines (#1A1417), thick on the silhouette and thin inside. Low-to-medium saturation with a single accent color, no gradients on the characters, no glossy highlights, no rim-light glow, no eye sparkle, very subtle paper grain. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### hands_sheet (both, 3:2)
- 용도: 손 시트: 시우 마른 손·테이프 주먹·가드 / 태오 두툼한 손·깃 잡기·바나나우유. buildHand 크기·포즈 세트 기준

```text
Hand study model sheet in black-and-white manga ink on white paper for two original teenage characters, realistic hand anatomy drawn from observation, hands at realistic size (hand length about four-fifths of the head height), long fingers with knuckle lines and fingernail outlines. Left half - the hands of a thin, wiry 17-year-old Korean schoolboy (174 cm, 61 kg) with slim wrists, visible wrist bones, lightly tanned skin, white athletic tape wrapped around the knuckles and small scratches on the backs of the hands, the rolled-up cuff of a navy school blazer sleeve at the forearm: a tight fist from the front, the same fist foreshortened toward the viewer, a relaxed hanging hand, a hand pushed into a trouser pocket with the thumb out, two raised boxing-guard fists. Right half - the hands of a heavy 17-year-old Korean judo athlete (183 cm, 88 kg) with broad palms, thick fingers, calloused knuckles and a rolled white shirt cuff at the thick forearm: an open hand reaching forward to grab a collar, a hand gripping a jacket lapel tightly, a hand holding a small blank jar-shaped banana-milk bottle with a straw, a relaxed hand with the thumb hooked in a pocket, a peace sign. Arranged in a loose grid with generous white space. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```

### poses_sheet (both, 3:2)
- 용도: 자세 시트: 대기(짝다리·주머니 손)·파이트 스탠스·유도 자세·앉기. 애니메이션 키포즈 기준

```text
Gesture and pose model sheet in black-and-white manga ink on white paper: eight full-body figures at the same scale in two rows (the thin boy is 174 cm and the judo athlete 183 cm, so the boy is only slightly shorter but much narrower), loose but anatomically accurate, with realistic proportions. Top row - a thin, wiry 17-year-old Korean schoolboy (174 cm, 61 kg, about 7.4 heads tall, narrow sloping shoulders) with lightly sun-tanned warm-brown skin, messy slightly curly black medium-length hair in heavy clumps whose longest fringe clump covers his right eye, a plain beige bandage across the bridge of his nose, faint dark circles under heavy-lidded eyes, an open one-size-too-big navy school blazer with sleeves rolled to mid-forearm over a heather-grey hoodie with the hood out, dark navy straight trousers, white high-top sneakers with bright orange soles, and white tape on his knuckles: (1) idle slouch with both hands in his trouser pockets, head tilted, weight on one leg; (2) a low, quick boxing guard with chin tucked and fists high, light on his feet; (3) throwing a straight punch, slightly off-balance, raw and unpolished; (4) sitting on the ground hugging his knees. Bottom row - a broad-shouldered 17-year-old Korean judo athlete (183 cm, 88 kg, about 7.5 heads tall, thick neck, solid heavy build with a thick waist) with fair skin, a two-block undercut with very short sides and a small messy half-up knot low on the back of his head tied with a deep-blue hair tie, two loose strands on his bare forehead, a swollen cauliflower left ear, an easy lopsided half-smile, a white school shirt with rolled sleeves and two open buttons, a loosened navy tie with a thin mint stripe, dark navy straight trousers, a black belt and black loafers: (5) a relaxed contrapposto holding a small blank banana-milk bottle with a straw; (6) a judo stance - low, knees bent, both open hands forward reaching for an opponent's collar; (7) walking with a bundled white judo uniform tied with a black belt slung over his shoulder; (8) arms crossed, feet planted wide. No opponent drawn. Plain white background. Black-and-white seinen manga pen-and-ink drawing on clean white paper. Fast, slightly rough dip-pen lines with uneven weight and a few broken or doubled strokes; outer contours about twice as thick as the sparse inner lines. Drawn from observation with realistic, ordinary human anatomy and proportions. Minimal facial linework on a realistic skull and jaw: small dark irises with white visible around them, no eye sparkle, the upper eyelid as the darkest line with only a short tail, a thin crease line above it, short lower lid lines, a short nose stroke, a short flat mouth line. Shadows are one-direction parallel hatching at about 45 degrees plus flat dot screentone; black hair is solid ink with thin white gap lines following the hair flow near the crown. Deadpan, understated, cinematic. Avoid: big shiny anime eyes, eye highlights, eyeliner wings, spiky anime hair spikes, glossy skin, elongated 8-9-head fashion proportions, oversized fashion coats, wide baggy trousers, horns, ringed or spiral eyes, shark teeth or fangs, high samurai topknot, black business suit, finger-gun gestures, sweat-drop or anger-mark symbols, cigarettes, smoking, alcohol, brand logos, any readable text, letters, numbers, labels, watermarks or signatures. Original characters, not resembling any existing anime or manga character.
```
