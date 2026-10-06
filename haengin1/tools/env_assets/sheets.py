"""Contact sheets for the report and docs/10.

usage: python sheets.py frames|props|compare <renders dir for compare>
frames  -> concept/art/env/frames/_frames_sheet.jpg (3 x 3) and _best3.jpg (picked in BEST)
props   -> concept/art/env/props/_props_sheet.jpg from tools/env_assets/logs/renders/<name>_34.png (+ size label)
compare -> concept/art/env/props/_compare_sheet.jpg from <dir>/<prop>__<model>_34.png and _front.png
"""
import sys, json, glob, os, pathlib
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
ENV = HERE.parent.parent / 'concept/art/env'
PAPER = (244, 239, 230)
BEST = ['f02_parking_sunset', 'f05_waryong_climb_dawn', 'f01_hoomun_street_night']


def font(sz):
    for f in (r'C:\Windows\Fonts\malgun.ttf', r'C:\Windows\Fonts\arial.ttf'):
        if os.path.exists(f):
            return ImageFont.truetype(f, sz)
    return ImageFont.load_default()


def frames():
    fs = sorted(glob.glob(str(ENV / 'frames/f*.png')))
    W, H = 900, 506
    out = Image.new('RGB', (W * 3 + 40, (H + 34) * 3 + 10), PAPER); d = ImageDraw.Draw(out); ft = font(22)
    for i, f in enumerate(fs[:9]):
        im = Image.open(f).convert('RGB').resize((W, H), Image.LANCZOS)
        r, c = divmod(i, 3); x, y = 10 + c * (W + 10), 10 + r * (H + 34)
        out.paste(im, (x, y + 28)); d.text((x, y + 2), os.path.basename(f)[:-4], fill=(26, 20, 23), font=ft)
    out.save(ENV / 'frames/_frames_sheet.jpg', quality=88)
    W, H = 1344, 760
    out = Image.new('RGB', (W, (H + 10) * 3), PAPER)
    for i, n in enumerate(BEST):
        out.paste(Image.open(ENV / f'frames/{n}.png').convert('RGB').resize((W, H), Image.LANCZOS), (0, i * (H + 10)))
    out.save(ENV / 'frames/_best3.jpg', quality=90)
    print('frames sheet', len(fs))


def props():
    rep = json.load(open(ENV / 'props/props_report.json', encoding='utf-8'))
    meta = json.load(open(HERE / 'specs/props_meta.json', encoding='utf-8'))
    names = sorted(rep)
    W = 300; cols = 8; rows = (len(names) + cols - 1) // cols
    out = Image.new('RGB', (W * cols, (W + 40) * rows), PAPER); d = ImageDraw.Draw(out); ft = font(15)
    for i, n in enumerate(names):
        r, c = divmod(i, cols); x, y = c * W, r * (W + 40)
        p = HERE / f'logs/renders/{n}_34.png'
        if p.exists():
            out.paste(Image.open(p).convert('RGB').resize((W, W)), (x, y + 40))
        rr = rep[n]
        d.text((x + 4, y + 2), f"{n} ({meta[n]['tier']})", fill=(26, 20, 23), font=ft)
        d.text((x + 4, y + 20), f"{rr['w']}x{rr['d']}x{rr['h']} m  {rr['tris']} tris", fill=(90, 80, 75), font=ft)
    out.save(ENV / 'props/_props_sheet.jpg', quality=85)
    print('props sheet', len(names))


def compare(rdir):
    order = [('vending_machine', ['tripo', 'hunyuan', 'hunyuan_lp', 'sam']), ('trash_bin_wheeled', ['tripo', 'hunyuan', 'hunyuan_lp']),
             ('delivery_scooter', ['tripo', 'hunyuan', 'sam'])]
    price = {'tripo': '9cr', 'hunyuan': '11cr', 'hunyuan_lp': '14cr', 'sam': '1cr'}
    W = 280
    out = Image.new('RGB', (W * 4 + W, (2 * W + 30) * 3), PAPER); d = ImageDraw.Draw(out); ft = font(18)
    for r, (p, ms) in enumerate(order):
        y = r * (2 * W + 30)
        ref = Image.open(ENV / f'props/ref/{p}.png').convert('RGB'); ref.thumbnail((W, W))
        out.paste(ref, (0, y + 30)); d.text((4, y + 4), f'{p} ref', fill=(26, 20, 23), font=ft)
        for c, m in enumerate(ms):
            x = W + c * W
            for j, t in enumerate(('34', 'front')):
                f = os.path.join(rdir, f'{p}__{m}_{t}.png')
                if os.path.exists(f):
                    out.paste(Image.open(f).convert('RGB').resize((W, W)), (x, y + 30 + j * W))
            d.text((x + 4, y + 4), f'{m} {price[m]}', fill=(26, 20, 23), font=ft)
    out.save(ENV / 'props/_compare_sheet.jpg', quality=85)
    print('compare sheet')


if __name__ == '__main__':
    k = sys.argv[1]
    if k == 'frames': frames()
    elif k == 'props': props()
    elif k == 'compare': compare(sys.argv[2])
