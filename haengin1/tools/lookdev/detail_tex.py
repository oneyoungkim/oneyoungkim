# -*- coding: utf-8 -*-
# 룩데브 ⑥ 디테일 그림(docs/14 4-6 · 9장 '구현 결과', 2026-10-26 — 크레딧 0): 전단지·스티커·낙서 아틀라스 + 실내 4곳 벽 그림(메뉴판·포스터·달력·TV·게시물·POP)
# 사용: python tools/lookdev/detail_tex.py
# 결과:
#   concept/art/env/decals/detail14_atlas.png (2048², 4×4 칸 512) + detail14_atlas.json(칸 이름 → [x, y, w, h] 픽셀, 위 = 0)
#   concept/art/env/stages/textures/M_D14_*.png (실내 벽 그림 — build_stages.py 의 STAGE_MATS 'uv: unit' 재질이 씀)
# 글자: 프로젝트 OFL 글꼴(Noto Sans KR Bold · Black Han Sans · Gaegu)을 PIL 로 굽는다(TMP 아틀라스를 안 건드림 — 글자 빠짐 없음).
# 상호·전화는 모두 가상(국번 012 — 실제 번호가 될 수 없음), 실존 브랜드 없음, 술·담배 없음(17세 규칙).
import os, json, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
FONTS = os.path.join(REPO, 'unity', 'HaenginMainEvent', 'Assets', '_Project', 'Fonts')
NOTO = os.path.join(FONTS, 'NotoSansKR-Bold.ttf')
BHS = os.path.join(FONTS, 'BlackHanSans-Regular.ttf')
GAEGU = os.path.join(FONTS, 'Gaegu-Regular.ttf')
DEC = os.path.join(REPO, 'concept', 'art', 'env', 'decals')
STX = os.path.join(REPO, 'concept', 'art', 'env', 'stages', 'textures')
os.makedirs(DEC, exist_ok=True); os.makedirs(STX, exist_ok=True)
INK = (47, 40, 36)


def F(path, size):
    return ImageFont.truetype(path, size)


def center_text(d, box, text, font, fill, spacing=6):
    x0, y0, x1, y1 = box
    lines = text.split('\n')
    hs = [d.textbbox((0, 0), l, font=font) for l in lines]
    th = sum(b[3] - b[1] for b in hs) + spacing * (len(lines) - 1)
    y = y0 + (y1 - y0 - th) / 2
    for l, b in zip(lines, hs):
        w = b[2] - b[0]
        d.text((x0 + (x1 - x0 - w) / 2 - b[0], y - b[1]), l, font=font, fill=fill)
        y += b[3] - b[1] + spacing


def paper(w, h, base, seed, alpha=255, tear=True):
    rnd = random.Random(seed)
    im = Image.new('RGBA', (w, h), base + (alpha,))
    d = ImageDraw.Draw(im)
    for _ in range(int(w * h / 900)):
        x, y = rnd.randrange(w), rnd.randrange(h)
        c = tuple(max(0, min(255, v + rnd.randint(-10, 6))) for v in base)
        d.point((x, y), fill=c + (alpha,))
    if tear:   # 위 테이프 2개 · 아래 찢긴 가장자리
        d.rectangle((w * 0.12, 0, w * 0.30, h * 0.05), fill=(232, 226, 200, 210))
        d.rectangle((w * 0.70, 0, w * 0.88, h * 0.05), fill=(232, 226, 200, 210))
        for x in range(0, w, 6):
            dy = rnd.randint(0, 10)
            d.rectangle((x, h - dy, x + 6, h), fill=(0, 0, 0, 0))
    return im


def age(im, seed, k=0.12):
    """바램·얼룩(물때)"""
    rnd = random.Random(seed)
    w, h = im.size
    ov = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for _ in range(5):
        x, y, r = rnd.randrange(w), rnd.randrange(h), rnd.randint(w // 10, w // 4)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(120, 100, 70, int(255 * k * rnd.random())))
    ov = ov.filter(ImageFilter.GaussianBlur(w / 30))
    base = im.copy()
    a = base.split()[3]
    base = Image.alpha_composite(base, ov)
    base.putalpha(a)
    return base


# ───────────────────────── 거리 아틀라스(4×4, 칸 512)
CELL = 512
atlas = Image.new('RGBA', (CELL * 4, CELL * 4), (0, 0, 0, 0))
rects = {}


def put(name, im, cx, cy):
    w, h = im.size
    x = cx * CELL + (CELL - w) // 2; y = cy * CELL + (CELL - h) // 2
    atlas.alpha_composite(im, (x, y))
    rects[name] = [x, y, w, h]


def flyer(title, body, foot, base, accent, seed, w=360, h=500, title_font=BHS):
    im = paper(w, h, base, seed)
    d = ImageDraw.Draw(im)
    d.rectangle((14, 34, w - 14, 140), fill=accent + (255,))
    center_text(d, (14, 34, w - 14, 140), title, F(title_font, 52 if len(title) <= 6 else 40), (250, 246, 236))
    center_text(d, (20, 150, w - 20, h - 120), body, F(NOTO, 26), INK, spacing=10)
    # 전화번호 떼는 쪽지
    n = 7; tw = (w - 20) / n
    for k in range(n):
        x0 = 10 + k * tw
        d.line((x0, h - 110, x0, h - 12), fill=(150, 140, 120, 255), width=2)
        tx = Image.new('RGBA', (100, int(tw) - 4), (0, 0, 0, 0))
        ImageDraw.Draw(tx).text((2, 0), foot, font=F(NOTO, 15), fill=INK)
        tx = tx.rotate(90, expand=True)
        im.alpha_composite(tx, (int(x0 + 4), h - 108))
    return age(im, seed)


put('flyer_room', flyer('원룸 투룸', '성대 후문 도보 3분\n풀옵션 · 즉시 입주\n관리비 포함', '02-012-4471', (241, 236, 222), (176, 62, 52), 1), 0, 0)
put('flyer_cat', flyer('고양이를\n찾습니다', '회색 줄무늬 · 이름 「보리」\n명륜3가 골목에서\n3월 2일 저녁에', '02-012-3390', (248, 244, 230), (64, 82, 110), 2), 1, 0)
put('flyer_tutor', flyer('수학 과외', '중·고등 내신 · 수능\n성균관로 대학생 선생님\n첫 상담 무료', '02-012-7731', (236, 241, 232), (70, 120, 92), 3), 2, 0)
put('flyer_piano', flyer('피아노 교습', '초등 · 성인 취미\n명륜 음악교실\n주 2회 반', '02-012-5518', (246, 238, 226), (150, 96, 150), 4), 3, 0)
put('flyer_bike', flyer('자전거 팝니다', '바구니 달린 생활 자전거\n타이어 새것 · 3만 원\n와룡공원 앞', '02-012-6604', (240, 238, 228), (196, 140, 46), 5), 0, 1)
put('flyer_job', flyer('알바 구함', '편의점 · 주말 낮\n고등학생 가능(밤 근무 없음)\n하루편의점 명륜점', '02-012-2048', (244, 240, 226), (52, 92, 132), 6), 1, 1)


def poster(title, sub, foot, bg, fg, seed, w=440, h=600):
    rnd = random.Random(seed)
    im = paper(w, h, bg, seed, tear=False)
    d = ImageDraw.Draw(im)
    for _ in range(6):   # 추상 도형(그림 대신)
        x, y, r = rnd.randint(40, w - 40), rnd.randint(160, 380), rnd.randint(30, 90)
        c = tuple(min(255, max(0, v + rnd.randint(-40, 40))) for v in fg)
        d.ellipse((x - r, y - r, x + r, y + r), fill=c + (200,))
    center_text(d, (20, 20, w - 20, 150), title, F(BHS, 58), INK)
    center_text(d, (20, 400, w - 20, 520), sub, F(NOTO, 28), INK, spacing=8)
    center_text(d, (20, 525, w - 20, 585), foot, F(NOTO, 20), (90, 80, 70))
    return age(im, seed, 0.18)


put('poster_market', poster('벼룩시장', '토요일 오후 2시\n와룡공원 공터', '명륜동 주민모임', (238, 226, 200), (120, 160, 120), 7), 2, 1)
put('poster_walk', poster('성곽길 걷기', '11월 셋째 일요일\n혜화문 출발', '종로 걷기 모임', (226, 232, 236), (190, 120, 80), 8), 3, 1)


def sticker_round(text, bg, fg, seed, r=150):
    im = Image.new('RGBA', (2 * r + 8, 2 * r + 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse((4, 4, 2 * r + 4, 2 * r + 4), fill=(250, 248, 240, 255))
    d.ellipse((14, 14, 2 * r - 6, 2 * r - 6), fill=bg + (255,))
    center_text(d, (14, 14, 2 * r - 6, 2 * r - 6), text, F(BHS, 64), fg)
    return age(im, seed, 0.2)


put('sticker_delivery', sticker_round('배달\nOK', (200, 60, 50), (255, 250, 240), 9), 0, 2)
def sticker_star(seed, r=150):
    im = sticker_round('', (60, 96, 160), (255, 220, 90), seed, r)
    d = ImageDraw.Draw(im)
    cx = cy = r + 4
    pts = [(cx + (r * 0.62 if k % 2 == 0 else r * 0.26) * math.sin(math.pi * k / 5), cy - (r * 0.62 if k % 2 == 0 else r * 0.26) * math.cos(math.pi * k / 5)) for k in range(10)]
    d.polygon(pts, fill=(255, 220, 90, 255))
    return im


put('sticker_star', sticker_star(10), 1, 2)


def sticker_rect(text, bg, fg, seed, w=420, h=150):
    im = Image.new('RGBA', (w, h), bg + (255,))
    d = ImageDraw.Draw(im)
    d.rectangle((6, 6, w - 7, h - 7), outline=fg, width=5)
    center_text(d, (0, 0, w, h), text, F(BHS, 60), fg)
    return age(im, seed, 0.22)


put('sticker_noparking', sticker_rect('주차 금지', (240, 206, 60), (40, 36, 32), 11), 2, 2)
put('sticker_noflyer', sticker_rect('전단지 부착 금지', (236, 236, 230), (170, 40, 40), 12, w=500, h=140), 3, 2)


def tag(seed, color, w=480, h=300):
    """스프레이 낙서(글자 아닌 획 — 실존 팀 이름 없이)"""
    rnd = random.Random(seed)
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for s in range(3):
        pts = []
        x, y = rnd.randint(40, 120), rnd.randint(80, 220)
        for k in range(14):
            x += rnd.randint(18, 42); y += rnd.randint(-55, 55); y = max(30, min(h - 30, y))
            pts.append((x, y))
        d.line(pts, fill=color + (230,), width=rnd.randint(12, 20), joint='curve')
    im = im.filter(ImageFilter.GaussianBlur(1.4))
    # 뿌린 점
    for _ in range(300):
        x, y = rnd.randrange(w), rnd.randrange(h)
        d.point((x, y), fill=color + (rnd.randint(40, 160),))
    return im


put('graffiti_a', tag(13, (60, 110, 170)), 0, 3)
put('graffiti_b', tag(14, (190, 70, 70)), 1, 3)


def doodle(seed, w=360, h=360):
    """분필 낙서: 고양이 얼굴·웃는 얼굴"""
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = (236, 232, 222, 220)
    if seed % 2:
        d.ellipse((60, 90, 300, 310), outline=c, width=9)
        d.polygon([(80, 130), (100, 40), (150, 100)], outline=c, width=9)
        d.polygon([(280, 130), (260, 40), (210, 100)], outline=c, width=9)
        d.ellipse((120, 170, 150, 200), fill=c); d.ellipse((210, 170, 240, 200), fill=c)
        d.line((140, 250, 180, 265, 220, 250), fill=c, width=7)
    else:
        d.ellipse((50, 50, 310, 310), outline=c, width=10)
        d.ellipse((120, 130, 150, 165), fill=c); d.ellipse((210, 130, 240, 165), fill=c)
        d.arc((110, 150, 250, 260), 20, 160, fill=c, width=10)
    return im.filter(ImageFilter.GaussianBlur(0.8))


put('doodle_cat', doodle(15), 2, 3)
put('doodle_smile', doodle(16), 3, 3)
atlas.save(os.path.join(DEC, 'detail14_atlas.png'))
json.dump({'size': CELL * 4, 'rects': rects, 'note': '룩데브 ⑥ 거리 디테일(tools/lookdev/detail_tex.py) — 위 = 0 픽셀 좌표'}, open(os.path.join(DEC, 'detail14_atlas.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ATLAS', len(rects), os.path.join(DEC, 'detail14_atlas.png'))


# ───────────────────────── 실내 벽 그림(uv 0~1 판)
def save(name, im):
    im.convert('RGB').save(os.path.join(STX, name + '.png'))
    print('TEX', name, im.size)


# 국밥집 메뉴판(가로 긴 판 — 칸 7)
W, H = 2048, 300
im = Image.new('RGB', (W, H), (122, 84, 52))
d = ImageDraw.Draw(im)
menu = [('순대국', '8,000'), ('뼈해장국', '9,000'), ('내장탕', '9,000'), ('수육(소)', '18,000'), ('순대(한 접시)', '10,000'), ('공깃밥', '1,000'), ('포장 가능', '')]
cols = [(238, 214, 140), (232, 176, 168), (178, 204, 160), (168, 190, 214), (240, 234, 220), (238, 214, 140), (232, 176, 168)]
cw = W / 7
for k, ((nm, pr), c) in enumerate(zip(menu, cols)):
    x0 = k * cw + 10
    d.rectangle((x0, 14, x0 + cw - 20, H - 14), fill=c)
    center_text(d, (x0, 30, x0 + cw - 20, 170), nm, F(BHS, 56 if len(nm) <= 4 else 40), INK)
    if pr:
        center_text(d, (x0, 175, x0 + cw - 20, H - 30), pr, F(NOTO, 50), (150, 40, 34))
save('M_D14_Menu_Gukbap', im)

# 국밥집 포스터 2·달력 1
im = Image.new('RGB', (600, 840), (232, 220, 196)); d = ImageDraw.Draw(im)
d.ellipse((120, 230, 480, 590), fill=(196, 120, 70)); d.ellipse((160, 270, 440, 550), fill=(236, 210, 160))
for k in range(5):
    d.arc((200 + k * 18, 120 + k * 6, 260 + k * 18, 240), 200, 340, fill=(255, 255, 255), width=8)
center_text(d, (20, 30, 580, 130), '진한 국물', F(BHS, 84), INK)
center_text(d, (20, 620, 580, 800), '새벽 5시부터\n24년째 한자리', F(NOTO, 46), INK, spacing=14)
save('M_D14_Poster_Gukbap', im)
im = Image.new('RGB', (600, 840), (226, 232, 226)); d = ImageDraw.Draw(im)
center_text(d, (20, 40, 580, 200), '원산지 표시', F(BHS, 76), INK)
rows = [('돼지고기', '국내산'), ('쌀', '국내산'), ('배추김치', '국내산'), ('깍두기 무', '국내산'), ('순대', '국내산')]
for k, (a, b) in enumerate(rows):
    y = 240 + k * 105
    d.line((60, y + 90, 540, y + 90), fill=(150, 150, 140), width=3)
    d.text((70, y + 10), a, font=F(NOTO, 48), fill=INK); d.text((360, y + 10), b, font=F(NOTO, 48), fill=(60, 100, 70))
save('M_D14_Origin_Gukbap', im)


def calendar(month, title, bg=(244, 240, 230)):
    im = Image.new('RGB', (600, 860), bg); d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 600, 330), fill=(150, 170, 190))
    for k in range(4):
        d.polygon([(0, 330 - k * 50), (300, 160 - k * 30), (600, 330 - k * 60)], fill=(120 - k * 12, 150 - k * 10, 120 - k * 8))
    center_text(d, (20, 340, 580, 440), f'{month}월', F(BHS, 80), INK)
    f = F(NOTO, 30)
    for dd in range(1, 32):
        r, c = divmod(dd + 2, 7)
        x, y = 40 + c * 76, 450 + r * 70
        d.text((x, y), str(dd), font=f, fill=(176, 50, 46) if c == 0 else INK)
    d.text((20, 815), title, font=F(NOTO, 22), fill=(110, 100, 90))
    return im


save('M_D14_Calendar_3', calendar(3, '명륜동 국밥 — 새해 복 많이'))
save('M_D14_Calendar_Home', calendar(3, '성곽약국 · 건강하세요', (240, 236, 228)))

# TV 화면(뉴스 — 글자 없이 화면 구성만 + 아래 띠)
im = Image.new('RGB', (800, 450), (40, 58, 86)); d = ImageDraw.Draw(im)
d.rectangle((0, 0, 800, 450), fill=(54, 78, 112))
d.rectangle((60, 70, 420, 360), fill=(196, 170, 140)); d.ellipse((170, 110, 310, 250), fill=(232, 204, 176)); d.rectangle((150, 250, 330, 360), fill=(48, 52, 66))
d.rectangle((450, 90, 760, 300), fill=(120, 160, 196)); d.rectangle((470, 200, 740, 290), fill=(90, 120, 90))
d.rectangle((0, 370, 800, 450), fill=(236, 236, 230)); d.rectangle((0, 370, 150, 450), fill=(190, 50, 46))
d.text((20, 392), '속보', font=F(BHS, 40), fill=(255, 255, 255)); d.text((170, 390), '오늘 저녁 서울 맑음 · 내일 아침 쌀쌀', font=F(NOTO, 34), fill=INK)
save('M_D14_TV_News', im)

# 시우네: 포스터(체육관 전단 — 혜화 MMA, 가상)·하늘 그림
im = Image.new('RGB', (600, 840), (30, 30, 34)); d = ImageDraw.Draw(im)
d.polygon([(0, 840), (600, 520), (600, 840)], fill=(190, 60, 50))
center_text(d, (20, 60, 580, 260), '혜화 MMA', F(BHS, 96), (244, 240, 232))
center_text(d, (20, 300, 580, 470), '초보 환영\n청소년반 저녁 6시', F(NOTO, 44), (230, 226, 216), spacing=14)
center_text(d, (20, 700, 580, 800), '대학로 지하 1층', F(NOTO, 34), (250, 240, 230))
save('M_D14_Poster_Gym', im)
im = Image.new('RGB', (600, 600), (250, 248, 240)); d = ImageDraw.Draw(im)
rnd = random.Random(21)
d.rectangle((0, 420, 600, 600), fill=(150, 200, 120)); d.ellipse((420, 40, 540, 160), fill=(250, 200, 80))
d.rectangle((120, 260, 330, 430), fill=(230, 150, 110)); d.polygon([(100, 270), (225, 170), (350, 270)], fill=(170, 80, 70))
d.ellipse((380, 330, 430, 380), fill=(240, 220, 200)); d.line((405, 380, 405, 460), fill=(60, 60, 80), width=8)
d.text((30, 540), '우리 집 — 반하늘', font=F(GAEGU, 44), fill=(60, 60, 120))
save('M_D14_Drawing_Haneul', im)

# 편의점: POP 가격표 띠(선반 앞 — 가로 긴 판, 칸 8) · 행사 포스터 · 천장 매달 표지(분류)
W, H = 2048, 128
im = Image.new('RGB', (W, H), (246, 244, 238)); d = ImageDraw.Draw(im)
pops = [('1+1', (210, 50, 46)), ('2,500', (40, 40, 40)), ('신상', (40, 110, 180)), ('1,800', (40, 40, 40)), ('2+1', (220, 120, 30)), ('990', (40, 40, 40)), ('할인', (200, 50, 120)), ('3,200', (40, 40, 40))]
cw = W / 8
for k, (t, c) in enumerate(pops):
    x0 = k * cw + 6
    d.rectangle((x0, 8, x0 + cw - 12, H - 8), fill=(255, 236, 90) if k % 2 == 0 else (255, 255, 255), outline=(200, 190, 160), width=3)
    center_text(d, (x0, 8, x0 + cw - 12, H - 8), t, F(BHS, 72), c)
save('M_D14_POP_Strip', im)
im = Image.new('RGB', (600, 840), (60, 96, 138)); d = ImageDraw.Draw(im)
center_text(d, (20, 50, 580, 250), '컵라면\n1+1', F(BHS, 110), (255, 236, 90), spacing=10)
d.ellipse((150, 330, 450, 630), fill=(236, 120, 60)); d.ellipse((190, 360, 410, 470), fill=(250, 230, 200))
center_text(d, (20, 680, 580, 800), '하루편의점 이번 주 행사', F(NOTO, 40), (240, 240, 236))
save('M_D14_Promo_Conv', im)
im = Image.new('RGB', (1024, 256), (90, 108, 124)); d = ImageDraw.Draw(im)
for k, t in enumerate(['음료', '라면', '과자', '생활용품']):
    x0 = k * 256
    d.rectangle((x0 + 8, 8, x0 + 248, 248), fill=(236, 232, 220))
    center_text(d, (x0 + 8, 8, x0 + 248, 248), t, F(BHS, 54 if len(t) <= 3 else 40), (60, 76, 92))
save('M_D14_Aisle_Signs', im)

# 교실: 게시판 종이(시간표·급식·공지·학급 목표) — 코르크 위에 붙는 판 한 장(4칸)
im = Image.new('RGBA', (2048, 1024), (0, 0, 0, 0)); d = ImageDraw.Draw(im)


def sheet(x, y, w, h, title, lines, bg, rot, seed):
    s = Image.new('RGBA', (w, h), bg + (255,)); sd = ImageDraw.Draw(s)
    center_text(sd, (0, 10, w, 110), title, F(BHS, 60), INK)
    yy = 130
    for l in lines:
        sd.text((30, yy), l, font=F(NOTO, 34), fill=INK); yy += 54
    sd.ellipse((w / 2 - 14, 4, w / 2 + 14, 32), fill=(200, 60, 50, 255))
    s = s.rotate(rot, expand=True, resample=Image.BICUBIC)
    im.alpha_composite(s, (x, y))


sheet(40, 40, 460, 640, '2학년 3반 시간표', ['월 국어 수학 영어', '화 과학 체육 국어', '수 영어 수학 음악', '목 사회 국어 미술', '금 수학 영어 창체', '', '담임 선생님 교무실 2층'], (244, 242, 236), 2, 1)
sheet(540, 80, 440, 600, '이번 주 급식', ['월 김치볶음밥', '화 돈가스', '수 비빔밥', '목 짜장면', '금 카레라이스'], (250, 236, 200), -3, 2)
sheet(1020, 40, 460, 560, '알림', ['중간고사 범위 공지', '체육복 꼭 챙기기', '복도에서 뛰지 않기', '', '학생회'], (220, 232, 244), 1.5, 3)
sheet(1520, 90, 480, 520, '우리 반 목표', ['서로 인사하기', '지각하지 않기', '청소 당번 지키기'], (240, 222, 226), -2, 4)
for k in range(5):
    sheet(60 + k * 390, 720, 300, 260, f'{k + 1}모둠', ['발표 준비'], [(236, 244, 230), (244, 238, 222), (230, 236, 246), (246, 230, 230), (240, 240, 230)][k], (-1) ** k * 3, 10 + k)
bgc = Image.new('RGBA', im.size, (185, 138, 90, 255))
for _ in range(4000):
    x, y = random.randrange(im.size[0]), random.randrange(im.size[1])
    ImageDraw.Draw(bgc).point((x, y), fill=(160, 116, 74, 255))
bgc.alpha_composite(im)
save('M_D14_Board_School', bgc)   # 코르크 위(불투명 — 툰 재질은 알파를 안 씀)
im = Image.new('RGB', (900, 300), (238, 236, 228)); d = ImageDraw.Draw(im)
center_text(d, (0, 0, 900, 300), '2-3', F(BHS, 200), (60, 70, 80))
save('M_D14_ClassSign', im)
print('끝')
