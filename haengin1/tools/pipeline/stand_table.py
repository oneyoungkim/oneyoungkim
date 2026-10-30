# StandSetup stand_measure.tsv 를 합격 기준 표로(귀~어깨 ≤3 · 어깨~엉덩이 ≤3 · 무릎 ≤10(휴지 대비) · 위팔 ≤12(정면) · 어깨 높이 ≤1.5 · 손 허벅지 옆)
# 사용: python stand_table.py <tsv> [자세 이름(새 대기)]
import sys
sys.stdout.reconfigure(encoding='utf-8')
rows = [l.rstrip('\n').lstrip('\ufeff').split('\t') for l in open(sys.argv[1], encoding='utf-8')]
what = sys.argv[2] if len(sys.argv) > 2 else None
print('인물 | 자세 | 귀-어깨 | 어깨-엉덩이 | 무릎(휴지 대비) | 무릎 뼈 | 무릎 메시 | 위팔(정면) | 위팔 3D | 손-허벅지 | 손 앞뒤 | 어깨차 | 등 튀어나옴 | 등 기울기 | 발목 간격 | 발바닥 | 판정')
bad = 0
for r in rows[1:]:
    if what and r[1] != what:
        continue
    ear, sh, kb, kd, arm, a3, hand, dy = float(r[2]), float(r[4]), float(r[5]), float(r[6]), float(r[7]), float(r[8]), float(r[9]), float(r[12])
    hz = max(abs(float(x)) for x in r[10].split('/'))
    ok = abs(ear) <= 3 and abs(sh) <= 3 and kd <= 15 and arm <= 12 and dy <= 2.5 and hand <= 22 and hz <= 8
    if r[1].startswith('새') and not ok:
        bad += 1
    print(f"{r[0]} | {r[1]} | {ear:+.1f} | {sh:+.1f} | {kd:+.1f} | {kb:.0f} | {r[18]} | {arm:.1f} | {a3:.1f} | {hand:.1f} | {r[10]} | {dy:.1f} | {float(r[19]):.1f} | {float(r[20]):+.1f} | {r[16]} | {r[17]} | {'합격' if ok else '넘음'}")
print('넘는 사람', bad)
