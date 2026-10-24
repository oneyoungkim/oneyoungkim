# -*- coding: utf-8 -*-
"""FBX(이진) 안의 Model 노드 변환값을 그대로 읽어 본다 — Unity 가 읽는 값(Lcl Translation/Rotation/Scaling,
PreRotation, GeometricTranslation)과 부모 연결. 블렌더 없이 시스템 파이썬으로.

  python tools/env/fbx_inspect.py concept/art/env/buildings/Z1_Alley.fbx [이름 앞부분 ...]
출력 끝에 '회전이 0 이 아닌 Model 수' 를 적는다(키트는 0 이어야 함 — 모든 오브젝트 회전 0, 위치만).
"""
import struct
import sys
import zlib


def read_nodes(data, off, ver):
    nodes = []
    while True:
        if ver >= 7500:
            end, nprop, plen = struct.unpack_from("<QQQ", data, off)
            off += 24
        else:
            end, nprop, plen = struct.unpack_from("<III", data, off)
            off += 12
        nlen = data[off]
        off += 1
        if end == 0:
            return nodes, off
        name = data[off:off + nlen].decode("ascii", "replace")
        off += nlen
        props = []
        p = off
        for _ in range(nprop):
            t = chr(data[p])
            p += 1
            if t == "Y":
                v = struct.unpack_from("<h", data, p)[0]; p += 2
            elif t == "C":
                v = data[p]; p += 1
            elif t == "I":
                v = struct.unpack_from("<i", data, p)[0]; p += 4
            elif t == "F":
                v = struct.unpack_from("<f", data, p)[0]; p += 4
            elif t == "D":
                v = struct.unpack_from("<d", data, p)[0]; p += 8
            elif t == "L":
                v = struct.unpack_from("<q", data, p)[0]; p += 8
            elif t in "SR":
                ln = struct.unpack_from("<I", data, p)[0]; p += 4
                v = data[p:p + ln]; p += ln
                if t == "S":
                    v = v.decode("utf-8", "replace")
            elif t in "fdlib":
                ln, enc, clen = struct.unpack_from("<III", data, p); p += 12
                raw = data[p:p + clen]; p += clen
                if enc == 1:
                    raw = zlib.decompress(raw)
                fmt = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}[t]
                v = list(struct.unpack_from("<%d%s" % (ln, fmt), raw))
            else:
                raise ValueError("prop type %r" % t)
            props.append(v)
        off = off + plen
        children = []
        if off < end:
            children, off = read_nodes(data, off, ver)
        nodes.append((name, props, children))
        off = end


def load(path):
    data = open(path, "rb").read()
    assert data[:20] == b"Kaydara FBX Binary  "
    ver = struct.unpack_from("<I", data, 23)[0]
    nodes, _ = read_nodes(data, 27, ver)
    return nodes, ver


def find(nodes, name):
    for n in nodes:
        if n[0] == name:
            return n
    return None


def models(path):
    nodes, ver = load(path)
    objs = find(nodes, "Objects")
    conns = find(nodes, "Connections")
    out = {}
    for n in objs[2]:
        if n[0] != "Model":
            continue
        uid, nm, typ = n[1][0], n[1][1].split("\x00")[0], n[1][2]
        p70 = find(n[2], "Properties70")
        props = {}
        if p70:
            for p in p70[2]:
                key = p[1][0]
                if key in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling", "PreRotation", "GeometricTranslation",
                           "GeometricRotation"):
                    props[key] = tuple(round(x, 4) for x in p[1][4:7])
        out[uid] = dict(name=nm, type=typ, props=props, parent=0)
    for c in conns[2]:
        if c[1][0] == "OO" and c[1][1] in out:
            out[c[1][1]]["parent"] = c[1][2]
    gs = find(nodes, "GlobalSettings")
    axes = {}
    if gs:
        for p in find(gs[2], "Properties70")[2]:
            if "Axis" in p[1][0] or "UnitScale" in p[1][0]:
                axes[p[1][0]] = p[1][4]
    return out, ver, axes


def main():
    path = sys.argv[1]
    pre = tuple(sys.argv[2:])
    ms, ver, axes = models(path)
    print("FBX", ver, axes)
    rot = 0
    for uid, m in ms.items():
        r = m["props"].get("Lcl Rotation", (0, 0, 0))
        pr = m["props"].get("PreRotation", (0, 0, 0))
        if any(abs(v) > 1e-3 for v in r + pr):
            rot += 1
        if not pre or m["name"].startswith(pre):
            par = ms.get(m["parent"], {}).get("name", "(root)")
            print("%-28s %-6s parent=%-14s %s" % (m["name"], m["type"].decode() if isinstance(m["type"], bytes) else m["type"], par, m["props"]))
    print("Model %d개 · 회전이 0 이 아닌 Model %d개" % (len(ms), rot))


if __name__ == "__main__":
    main()
