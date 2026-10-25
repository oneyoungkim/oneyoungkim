# 룩데브 핵심(docs/14_룩데브_계획.md) — 프레임 ↔ Unity 화면 차이를 재는 지표 + Unity 화면에 얹어 보는 후처리 6단계(오프라인 시안)
# 이 파일은 함수만. 실행은 run_lookdev.py(짝·시트·LUT 내보내기).
# 지표(모두 sRGB → CIELAB D65, Unity 화면 해상도로 맞춘 뒤):
#   palette_de   = 16색 k-평균 팔레트끼리 양방향 가중 최근접 ΔE2000 평균(내용이 달라도 '쓰는 색'이 얼마나 다른지)
#   sh_lab/lit_lab = 그늘(주변 밝은 곳 대비 0.80 배 아래)·밝은 면의 평균 L*a*b* — 그림자 색조
#   line_*       = 먹선(블랙햇 > 14, L* < 50): 화면 비율·평균 굵기(px)·굵기 변동(CV)·선 색
#   grain/brush  = 평평한 곳의 고주파(σ1 잔차)·중주파(σ1.5−σ6) 표준편차 — 종이 결·붓 얼룩
#   far_*        = 먼 띠(화면 28~50% 높이·가운데 60%, 하늘 뺌) ÷ 가까운 띠(아래 28%) 대비·채도, b* 차
#   edge         = Canny 경계 밀도(디테일 밀도)
import numpy as np, cv2
from PIL import Image
from skimage import color as skc
from skimage.morphology import skeletonize
from sklearn.cluster import MiniBatchKMeans
from scipy import ndimage as ndi


# ───────────────────────── 입출력
def load(path, size):
    """그림을 size(w,h) 로 — 비율이 다르면 가운데를 잘라 맞춘 뒤 줄임(PIL: 한글 경로 안전)"""
    im = Image.open(path).convert('RGB')
    w, h = im.size; tw, th = size
    r = tw / th
    if abs(w / h - r) > 1e-3:
        if w / h > r:
            nw = round(h * r); x0 = (w - nw) // 2; im = im.crop((x0, 0, x0 + nw, h))
        else:
            nh = round(w / r); y0 = (h - nh) // 2; im = im.crop((0, y0, w, y0 + nh))
    if im.size != (tw, th):
        im = im.resize((tw, th), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0


def save(path, img, q=90):
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path, quality=q)


def lab(img):
    return skc.rgb2lab(np.clip(img, 0, 1)).astype(np.float32)


def rgb(lab_):
    return np.clip(skc.lab2rgb(lab_), 0, 1).astype(np.float32)


def srgb2lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def smooth01(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ───────────────────────── 마스크
def sky_mask(L, indoor=False):
    """위 가장자리에 닿는 평평한 덩어리(화면 62% 위) = 하늘. 실내는 없음"""
    h, w = L.shape
    if indoor:
        return np.zeros_like(L, bool)
    Ls = cv2.GaussianBlur(L, (0, 0), 2)
    g = np.hypot(cv2.Sobel(Ls, cv2.CV_32F, 1, 0), cv2.Sobel(Ls, cv2.CV_32F, 0, 1)) / 8
    cand = ((g < 1.0) & (np.arange(h)[:, None] < 0.62 * h)).astype(np.uint8)
    cand = cv2.morphologyEx(cand, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    n, lbl = cv2.connectedComponents(cand, connectivity=8)
    top = [i for i in np.unique(lbl[:4, :]) if i != 0]
    m = np.isin(lbl, top) & (lbl > 0)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((11, 11), np.uint8)).astype(bool)
    return m


def line_field(L):
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    bh = cv2.morphologyEx(L, cv2.MORPH_BLACKHAT, k)
    return bh, (bh > 14) & (L < 50)


def shade_ratio(L):
    """주변 밝은 면(21px 팽창 + 흐림) 대비 밝기 — 그늘 판단(셀 그림자 ≈ 0.75)"""
    lit = cv2.GaussianBlur(cv2.dilate(L, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21))), (0, 0), 12)
    return L / np.maximum(lit, 1.0)


# ───────────────────────── 지표
def palette(lb, k=16, n=60000, seed=0):
    f = lb.reshape(-1, 3)
    idx = np.random.default_rng(seed).choice(len(f), min(n, len(f)), replace=False)
    km = MiniBatchKMeans(k, random_state=seed, n_init=3, batch_size=4096).fit(f[idx])
    w = np.bincount(km.labels_, minlength=k).astype(float); w /= w.sum()
    return km.cluster_centers_.astype(np.float32), w


def palette_de(pa, pb):
    (ca, wa), (cb, wb) = pa, pb
    d = skc.deltaE_ciede2000(ca[:, None, :], cb[None, :, :])
    return float(0.5 * ((d.min(1) * wa).sum() + (d.min(0) * wb).sum()))


def metrics(img, indoor=False, pal=True):
    lb = lab(img); L = lb[..., 0]; h, w = L.shape
    sky = sky_mask(L, indoor)
    ns = ~sky
    bh, lm = line_field(L)
    m = {}
    m['L_mean'] = float(L[ns].mean())
    m['L_p5'], m['L_p50'], m['L_p95'] = (float(x) for x in np.percentile(L[ns], [5, 50, 95]))
    C = np.hypot(lb[..., 1], lb[..., 2])
    m['C_mean'] = float(C[ns].mean()); m['C_p90'] = float(np.percentile(C[ns], 90))
    m['a_mean'] = float(lb[..., 1][ns].mean()); m['b_mean'] = float(lb[..., 2][ns].mean())
    m['sky_frac'] = float(sky.mean())
    # 그늘·밝은 면 색조(먹선·하늘 뺌)
    r = shade_ratio(L)
    lmd = cv2.dilate(lm.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    base = ns & ~lmd & (L > 8)
    ws = base & (r < 0.80)
    wl = base & (r > 0.93) & (L > 40)
    for nm, mk in (('sh', ws), ('lit', wl)):
        if mk.sum() < 100:
            mk = base
        m[nm + '_lab'] = [float(lb[..., c][mk].mean()) for c in range(3)]
        m[nm + '_frac'] = float(mk.mean())
    a, b = m['sh_lab'][1], m['sh_lab'][2]
    m['sh_hue'] = float(np.degrees(np.arctan2(b, a)) % 360); m['sh_chroma'] = float(np.hypot(a, b))
    a, b = m['lit_lab'][1], m['lit_lab'][2]
    m['lit_hue'] = float(np.degrees(np.arctan2(b, a)) % 360)
    # 그늘 곱셈값(선형 RGB, 밝기 1 로 맞춘 색조) — 셀 그림자 색 추천용
    lin = srgb2lin(img)
    sh = lin[ws].mean(0) if ws.sum() > 100 else lin[base].mean(0)
    lt = lin[wl].mean(0) if wl.sum() > 100 else lin[base].mean(0)
    ratio = sh / np.maximum(lt, 1e-4)
    lum = 0.2126 * ratio[0] + 0.7152 * ratio[1] + 0.0722 * ratio[2]
    m['shade_mult'] = [float(x) for x in ratio]; m['shade_tint'] = [float(x / max(lum, 1e-4)) for x in ratio]
    # 먹선
    m['line_frac'] = float(lm[ns].mean())
    if lm.sum() > 50:
        dt = cv2.distanceTransform(lm.astype(np.uint8), cv2.DIST_L2, 3)
        sk = skeletonize(lm)
        wid = 2 * dt[sk]
        m['line_w'] = float(wid.mean()); m['line_cv'] = float(wid.std() / max(wid.mean(), 1e-3))
        strong = lm & (bh > np.percentile(bh[lm], 60))
        m['line_lab'] = [float(lb[..., c][strong].mean()) for c in range(3)]
    else:
        m['line_w'] = 0.0; m['line_cv'] = 0.0; m['line_lab'] = [0.0, 0.0, 0.0]
    # 디테일·결
    can = cv2.Canny(np.clip(L * 2.55, 0, 255).astype(np.uint8), 50, 120) > 0
    m['edge'] = float(can[ns].mean())
    g2 = cv2.GaussianBlur(L, (0, 0), 2)
    grad = np.hypot(cv2.Sobel(g2, cv2.CV_32F, 1, 0), cv2.Sobel(g2, cv2.CV_32F, 0, 1)) / 8
    flat = (grad < 0.6) & ~lmd
    res = L - cv2.GaussianBlur(L, (0, 0), 1.0)
    m['grain'] = float(res[flat].std()) if flat.sum() > 1000 else 0.0
    m['flat_frac'] = float(flat.mean())
    g6 = cv2.GaussianBlur(L, (0, 0), 6)
    gr6 = np.hypot(cv2.Sobel(g6, cv2.CV_32F, 1, 0), cv2.Sobel(g6, cv2.CV_32F, 0, 1)) / 8
    flat6 = (gr6 < 1.2) & ~lmd
    mid = cv2.GaussianBlur(L, (0, 0), 1.5) - g6
    m['brush'] = float(mid[flat6].std()) if flat6.sum() > 1000 else 0.0
    # 대기 원근
    yy, xx = np.mgrid[0:h, 0:w]
    far = ns & (yy > 0.28 * h) & (yy < 0.50 * h) & (xx > 0.2 * w) & (xx < 0.8 * w)
    near = ns & (yy > 0.72 * h)
    if far.sum() > 500 and near.sum() > 500:
        m['far_contrast'] = float(L[far].std() / max(L[near].std(), 1e-3))
        m['far_chroma'] = float(C[far].mean() / max(C[near].mean(), 1e-3))
        m['far_db'] = float(lb[..., 2][far].mean() - lb[..., 2][near].mean())
        m['far_lab'] = [float(lb[..., c][far].mean()) for c in range(3)]
        m['edge_far'] = float(can[far].mean())
    # 빛 웅덩이·하이라이트(하늘 뺀 밝은 3%)
    thr = np.percentile(L[ns], 97)
    hi = ns & (L >= thr)
    m['hi_lab'] = [float(lb[..., c][hi].mean()) for c in range(3)]
    m['near_range'] = float(np.percentile(L[near], 98) - np.percentile(L[near], 50)) if near.sum() > 500 else 0.0
    if pal:
        m['_pal'] = palette(lb)
    return m


def compare(mu, mf):
    """Unity(또는 후처리) 지표 ↔ 프레임 지표 → 차이 요약"""
    d = {}
    d['palette_de'] = palette_de(mu['_pal'], mf['_pal'])
    d['shadow_de'] = float(skc.deltaE_ciede2000(np.array(mu['sh_lab']), np.array(mf['sh_lab'])))
    d['lit_de'] = float(skc.deltaE_ciede2000(np.array(mu['lit_lab']), np.array(mf['lit_lab'])))
    d['line_ratio'] = mu['line_frac'] / max(mf['line_frac'], 1e-5)
    d['grain_ratio'] = mu['grain'] / max(mf['grain'], 1e-5)
    d['brush_ratio'] = mu['brush'] / max(mf['brush'], 1e-5)
    d['edge_ratio'] = mu['edge'] / max(mf['edge'], 1e-5)
    if 'far_contrast' in mu and 'far_contrast' in mf:
        d['far_contrast_u'] = mu['far_contrast']; d['far_contrast_f'] = mf['far_contrast']
        d['far_db_u'] = mu['far_db']; d['far_db_f'] = mf['far_db']
    return d


# ───────────────────────── ① 색 전이(IDT, Pitié 2007) → 3D LUT
def _rot(rng):
    q, r = np.linalg.qr(rng.normal(size=(3, 3)))
    return q * np.sign(np.diag(r))


def _interp(x, sq, tq):
    y = np.interp(x, sq, tq)
    lo = x < sq[0]; hi = x > sq[-1]
    y[lo] = tq[0] + (x[lo] - sq[0]); y[hi] = tq[-1] + (x[hi] - sq[-1])
    return y


def idt_fit(src, dst, n_iter=10, n_q=200, seed=0):
    """src·dst = (N,3) Lab 표본 → 회전 + 1차원 분위 맞춤 단계들(아무 색에나 다시 쓸 수 있음)"""
    rng = np.random.default_rng(seed)
    x = src.astype(np.float64).copy(); qs = np.linspace(0, 1, n_q)
    steps = []
    for _ in range(n_iter):
        R = _rot(rng)
        xp = x @ R.T; yp = dst @ R.T
        tabs = []
        for d in range(3):
            sq = np.quantile(xp[:, d], qs); tq = np.quantile(yp[:, d], qs)
            sq = sq + np.arange(n_q) * 1e-6
            tabs.append((sq, tq)); xp[:, d] = _interp(xp[:, d], sq, tq)
        x = xp @ R
        steps.append((R, tabs))
    return steps


def idt_apply(lb, steps):
    x = lb.reshape(-1, 3).astype(np.float64).copy()
    for R, tabs in steps:
        xp = x @ R.T
        for d, (sq, tq) in enumerate(tabs):
            xp[:, d] = _interp(xp[:, d], sq, tq)
        x = xp @ R
    return x.reshape(lb.shape)


def build_lut(steps, n=33, strength=0.85, smooth=0.9):
    """sRGB → sRGB 3D LUT (n,n,n,3) — 인덱스 [b][g][r]. 이동량을 격자에서 흐려(σ=smooth 칸) 띠·튐 방지, strength 만큼만 적용"""
    t = np.linspace(0, 1, n)
    b, g, r = np.meshgrid(t, t, t, indexing='ij')
    grid = np.stack([r, g, b], -1).astype(np.float32)
    lb = lab(grid.reshape(-1, 1, 3)).reshape(n, n, n, 3).astype(np.float64)
    out = idt_apply(lb, steps)
    dlt = out - lb
    for c in range(3):
        dlt[..., c] = ndi.gaussian_filter(dlt[..., c], smooth, mode='nearest')
    res = rgb((lb + strength * dlt).reshape(-1, 1, 3)).reshape(n, n, n, 3)
    return res


def apply_lut(img, lut):
    n = lut.shape[0]
    p = np.clip(img, 0, 1) * (n - 1)
    i0 = np.minimum(np.floor(p).astype(np.int32), n - 2); f = p - i0
    r0, g0, b0 = i0[..., 0], i0[..., 1], i0[..., 2]
    fr, fg, fb = f[..., 0:1], f[..., 1:2], f[..., 2:3]

    def at(db, dg, dr):
        return lut[b0 + db, g0 + dg, r0 + dr]
    c00 = at(0, 0, 0) * (1 - fr) + at(0, 0, 1) * fr
    c01 = at(0, 1, 0) * (1 - fr) + at(0, 1, 1) * fr
    c10 = at(1, 0, 0) * (1 - fr) + at(1, 0, 1) * fr
    c11 = at(1, 1, 0) * (1 - fr) + at(1, 1, 1) * fr
    c0 = c00 * (1 - fg) + c01 * fg; c1 = c10 * (1 - fg) + c11 * fg
    return (c0 * (1 - fb) + c1 * fb).astype(np.float32)


def write_cube(path, lut, title):
    n = lut.shape[0]
    with open(path, 'w', encoding='ascii', newline='\n') as f:
        f.write(f'TITLE "{title}"\nLUT_3D_SIZE {n}\nDOMAIN_MIN 0.0 0.0 0.0\nDOMAIN_MAX 1.0 1.0 1.0\n')
        for bi in range(n):
            for gi in range(n):
                for ri in range(n):
                    c = lut[bi, gi, ri]
                    f.write(f'{c[0]:.6f} {c[1]:.6f} {c[2]:.6f}\n')


def write_strip(path, lut):
    """URP Color Lookup 용 2D 띠(n²×n): 가로 = 파랑 칸 × 빨강, 세로 = 초록(Unity 텍스처는 아래가 0 — PNG 는 위가 0 이라 뒤집어 씀)"""
    n = lut.shape[0]
    img = np.zeros((n, n * n, 3), np.float32)
    for bi in range(n):
        img[:, bi * n:(bi + 1) * n] = lut[bi][::-1]
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path)


# ───────────────────────── ② 그늘 색 나누기(셀 그림자 색조 흉내 — Unity 에선 UTS 1st_ShadeColor 로 정확히)
def shadow_split(img, tgt_sh_ab, tgt_lit_ab, k_sh=1.0, k_lit=0.6, tgt_sh_L=None, k_L=0.6):
    lb = lab(img); L = lb[..., 0]
    _, lm = line_field(L)
    r = shade_ratio(L)
    keep = ~cv2.dilate(lm.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    ws = cv2.GaussianBlur((smooth01(-r, -0.84, -0.66) * smooth01(L, 8, 18) * keep).astype(np.float32), (0, 0), 1.5)
    wl = cv2.GaussianBlur((smooth01(r, 0.90, 0.98) * smooth01(L, 40, 65) * keep).astype(np.float32), (0, 0), 1.5)
    out = lb.copy()
    for wgt, tgt, k in ((ws, tgt_sh_ab, k_sh), (wl, tgt_lit_ab, k_lit)):
        s = wgt.sum()
        if s < 100:
            continue
        cur = np.array([(lb[..., c] * wgt).sum() / s for c in (1, 2)])
        dl = (np.array(tgt) - cur) * k
        out[..., 1] += wgt * dl[0]; out[..., 2] += wgt * dl[1]
    if tgt_sh_L is not None and ws.sum() > 100:
        curL = float((L * ws).sum() / ws.sum())
        out[..., 0] += ws * (tgt_sh_L - curL) * k_L      # 그늘 명도(프레임 그늘은 밝다 — 곱셈값을 올리는 것과 같음)
    return rgb(out)


# ───────────────────────── ③ 붓 그림(먹선 지우기 → 일반화 Kuwahara → 결 따라 번지는 붓 결 + 묽은 얼룩)
def remove_lines(img, lm):
    m = cv2.dilate(lm.astype(np.uint8), np.ones((3, 3), np.uint8))
    u8 = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    return cv2.inpaint(u8, m, 3, cv2.INPAINT_TELEA).astype(np.float32) / 255.0


def kuwahara(img, radius=4, sectors=8, q=6.0):
    """Papari 일반화 Kuwahara — 부채꼴 8개 가우스 가중 평균 중 분산 작은 쪽에 무게"""
    yy, xx = np.mgrid[-radius:radius + 1, -radius:radius + 1].astype(np.float32)
    ang = np.arctan2(yy, xx); rr = np.hypot(xx, yy)
    gauss = np.exp(-(rr ** 2) / (2 * (radius / 2.0) ** 2)) * (rr <= radius)
    x = img.astype(np.float32); x2 = x * x
    num = np.zeros_like(x); den = np.zeros(x.shape[:2], np.float32)
    for i in range(sectors):
        a0 = -np.pi + i * 2 * np.pi / sectors
        d = np.angle(np.exp(1j * (ang - a0 - np.pi / sectors)))
        sec = np.clip(1 - np.abs(d) / (np.pi / sectors), 0, 1)
        k = (gauss * sec).astype(np.float32); k[radius, radius] = gauss[radius, radius] / sectors
        k /= k.sum()
        mu = cv2.filter2D(x, -1, k, borderType=cv2.BORDER_REFLECT)
        m2 = cv2.filter2D(x2, -1, k, borderType=cv2.BORDER_REFLECT)
        var = np.maximum(m2 - mu * mu, 0).sum(-1) * 255 * 255
        a = 1.0 / (1.0 + (var / 100.0) ** (q / 2))
        num += mu * a[..., None]; den += a
    return num / np.maximum(den[..., None], 1e-12)


def fbm(h, w, scale, octaves=4, seed=0):
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        s = max(2, int(scale / (2 ** o)))
        n = rng.normal(size=(h // s + 2, w // s + 2)).astype(np.float32)
        out += amp * cv2.resize(n, (w, h), interpolation=cv2.INTER_CUBIC)[:h, :w]
        tot += amp; amp *= 0.5
    out /= tot
    return out / max(float(out.std()), 1e-6)


def brush(img, amp=2.0, length=6, wash=2.0, seed=1):
    """구조 텐서 접선 방향으로 잡음을 번지게(LIC) → L* ±amp, 낮은 주파 얼룩 ±wash"""
    lb = lab(img); L = lb[..., 0]; h, w = L.shape
    gx = cv2.Sobel(L, cv2.CV_32F, 1, 0); gy = cv2.Sobel(L, cv2.CV_32F, 0, 1)
    jxx = cv2.GaussianBlur(gx * gx, (0, 0), 4); jyy = cv2.GaussianBlur(gy * gy, (0, 0), 4); jxy = cv2.GaussianBlur(gx * gy, (0, 0), 4)
    th = 0.5 * np.arctan2(2 * jxy, jxx - jyy) + np.pi / 2
    tx, ty = np.cos(th).astype(np.float32), np.sin(th).astype(np.float32)
    rng = np.random.default_rng(seed)
    noise = cv2.resize(rng.normal(size=(h // 2, w // 2)).astype(np.float32), (w, h), interpolation=cv2.INTER_LINEAR)
    X, Y = np.meshgrid(np.arange(w, dtype=np.float32), np.arange(h, dtype=np.float32))
    acc = noise.copy(); cnt = 1.0
    for sgn in (1, -1):
        px, py = X.copy(), Y.copy()
        for _ in range(length):
            dx = cv2.remap(tx, px, py, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
            dy = cv2.remap(ty, px, py, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
            px = px + sgn * 1.5 * dx; py = py + sgn * 1.5 * dy
            acc += cv2.remap(noise, px, py, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT); cnt += 1
    lic = acc / cnt; lic /= max(float(lic.std()), 1e-6)
    tr = jxx + jyy; det = np.sqrt((jxx - jyy) ** 2 + 4 * jxy * jxy)
    coh = cv2.GaussianBlur(((det / np.maximum(tr, 1e-3)) ** 2).astype(np.float32), (0, 0), 3)   # 결이 뚜렷한 곳만(평평한 곳 소용돌이 방지)
    lowf = fbm(h, w, 90, 3, seed + 7)
    out = lb.copy()
    out[..., 0] += amp * lic * coh + wash * lowf
    out[..., 1] += 0.6 * fbm(h, w, 60, 3, seed + 11); out[..., 2] += 0.6 * fbm(h, w, 60, 3, seed + 13)
    return rgb(out)


# ───────────────────────── ④ 굵기가 변하는 색 먹선 + 색 경계 잔선
def draw_lines(img, bh0, frame_line_lab, k_main=1.0, k_fine=0.75, seed=3):
    h, w = bh0.shape
    lb = lab(img)
    f = np.clip(bh0 / 38.0, 0, 1)
    P = 0.80 + 0.32 * np.clip(fbm(h, w, 40, 3, seed), -2, 2)
    a_main = smooth01(f * P, 0.15, 0.38) * k_main
    base = cv2.GaussianBlur(lb, (0, 0), 3)
    ln = base.copy()
    ln[..., 0] = np.minimum(base[..., 0] * 0.30, frame_line_lab[0] + 2)
    ln[..., 1] = 0.35 * base[..., 1] + 0.5 * frame_line_lab[1]
    ln[..., 2] = 0.35 * base[..., 2] + 0.5 * frame_line_lab[2]
    L8 = np.clip(lb[..., 0] * 2.55, 0, 255).astype(np.uint8)
    can = cv2.Canny(cv2.GaussianBlur(L8, (0, 0), 1.2), 30, 80).astype(np.float32)
    can = cv2.GaussianBlur(can / 255.0, (0, 0), 0.6) * 1.6
    a_fine = np.clip(can, 0, 1) * k_fine * np.clip(0.6 + 0.4 * fbm(h, w, 25, 2, seed + 1), 0, 1.2)
    a = np.clip(np.maximum(a_main, a_fine), 0, 0.95)[..., None]
    return img * (1 - a) + rgb(ln) * a


# ───────────────────────── ⑤ 대기 원근(깊이 없음 → 소실점 근처 가우스 + 하늘 뺌 — Unity 에선 깊이 안개로)
def fog(img, fog_lab, strength=0.35, vp=(0.5, 0.42), sx=0.30, sy=0.16, indoor=False):
    lb = lab(img); L = lb[..., 0]; h, w = L.shape
    sky = sky_mask(L, indoor)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    g = np.exp(-(((xx / w - vp[0]) / sx) ** 2) - (((yy / h - vp[1]) / sy) ** 2))
    up = yy / h < vp[1]
    g2 = 0.55 * np.exp(-(((xx / w - vp[0]) / (sx * 1.6)) ** 2))
    g[up] = np.maximum(g[up], g2[up])
    wgt = cv2.GaussianBlur((g * ~sky).astype(np.float32), (0, 0), 6) * strength
    fl = rgb(np.array(fog_lab, np.float64).reshape(1, 1, 3))[0, 0]
    return img * (1 - wgt[..., None]) + fl[None, None, :] * wgt[..., None]


# ───────────────────────── ⑥ 종이 결(섬유 + 고운 잡음) + 밝은 곳 종이색
def paper(img, amp=2.0, tint=(0.99, 0.975, 0.95), seed=5):
    h, w = img.shape[:2]
    rng = np.random.default_rng(seed)
    fine = cv2.GaussianBlur(rng.normal(size=(h, w)).astype(np.float32), (0, 0), 0.7)
    fine /= float(fine.std())
    fib = np.zeros((h, w), np.float32)
    for _ in range(int(h * w / 900)):
        x0, y0 = int(rng.integers(0, w)), int(rng.integers(0, h)); a = rng.uniform(0, np.pi); ln = rng.uniform(4, 14)
        cv2.line(fib, (x0, y0), (int(x0 + ln * np.cos(a)), int(y0 + ln * np.sin(a))), float(rng.uniform(0.3, 1.0)), 1, cv2.LINE_AA)
    fib = cv2.GaussianBlur(fib, (0, 0), 0.6); fib = (fib - fib.mean()) / max(float(fib.std()), 1e-6)
    tex = 0.75 * fine + 0.45 * fib + 0.5 * fbm(h, w, 120, 3, seed + 2)
    lb = lab(img)
    lb[..., 0] += amp * tex
    out = rgb(lb)
    hi = smooth01(lb[..., 0], 70, 95)[..., None]
    return out * (1 - hi * (1 - np.array(tint, np.float32)))
