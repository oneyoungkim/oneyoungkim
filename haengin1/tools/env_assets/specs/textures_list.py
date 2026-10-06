# Base-pattern textures (material names shared with the building generator). Writes specs/textures.json.
# meters = real-world size one tile covers (the building generator sets UV scale from this).
# kind = grid (regular pattern -> period-aligned crop) | free (irregular -> offset blend)
import json, pathlib
T = [
 # name, meters, kind, prompt (surface), colors
 ("M_Brick_Red", 1.0, "grid", "a red-brick wall in running bond: about 15 brick courses tall and 5 bricks across, thin pale mortar joints, each brick a flat fill with slight hue variation between bricks, a few chipped corners drawn with short ink strokes", "red-brown bricks around #8E4A3A with some #9C5545 and #7E4234, mortar #CFC4B3"),
 ("M_Brick_Brown", 1.0, "grid", "a dark brown face-brick wall of a Korean multi-family villa in running bond: about 15 brick courses tall and 5 bricks across, thin grey mortar joints, flat fills with slight variation between bricks", "chocolate brown bricks around #6E4B3A with some #7A5644 and #5E3F31, mortar #A9A196"),
 ("M_Plaster_White", 2.0, "free", "an off-white rough sprayed plaster wall (stucco), surface suggested only by a sparse scatter of tiny ink dots, a few faint hairline cracks and two or three soft rain-streak marks", "off-white #E8E2D6 with marks slightly darker #D3CBBE"),
 ("M_Plaster_Cream", 2.0, "free", "a cream painted cement plaster wall, surface suggested only by sparse tiny ink dots, a couple of hairline cracks and faint patch marks", "cream #E3D5B8 with marks #CDBF9F"),
 ("M_Tile_Beige", 1.0, "grid", "a shop building facade clad in small rectangular ceramic tiles, 20 cm wide and 10 cm tall, stack bond (aligned joints), 10 rows and 5 columns, thin grey grout lines, flat fills with slight tile-to-tile variation", "beige tiles #CDBFA6 with some #C4B59B and #D6C9B1, grout #A79C8B"),
 ("M_Concrete", 2.4, "grid", "a cast-in-place concrete retaining wall with formwork panel lines: a grid of 4 panels across and 2 panels tall, small round form-tie holes in a regular pattern, sparse ink dots and a few faint water stains", "cool grey #B9B6AF with lines #9D9A93"),
 ("M_Granite", 3.0, "grid", "the outer face of an old Korean fortress wall built of large dressed granite blocks laid in rough horizontal courses: about 5 courses, blocks of slightly different widths, thick dark ink joint lines, each block a flat fill with a few ink dots and short hatching on one edge", "warm light grey granite #CFC6B4 with blocks varying #C4BBA8 to #D8D0BF, joints #4A443E"),
 ("M_Stone_Base", 1.5, "grid", "the lower wall of a traditional Korean hanok made of rectangular grey stone blocks about 30 cm by 50 cm in staggered courses, bold dark ink joint lines, flat fills", "grey stone #9C9890 with variation #8E8A82 to #AAA69E, joints #2C2826"),
 ("M_Stone_Rubble", 2.0, "free", "a rough stone retaining wall of irregular rounded fieldstones of many sizes packed together, dark ink outlines around each stone, flat fills", "stones in warm greys #B8AE9C #A79E8C #C6BDAB, gaps #5A534A"),
 ("M_Giwa_Dark", 1.2, "grid", "a traditional Korean roof seen straight from above: parallel vertical rows of dark grey clay roof tiles, alternating convex cover-tile rows and concave pan-tile channels, 4 channels across, overlapping tile ends visible as repeating curved lines along each row", "dark ink grey #3A3A3A with cover tiles #454545 and channel shadows #2C2C2C, thin lighter edge lines #5A5A5A"),
 ("M_Wood_Red", 1.0, "free", "faded vermilion painted wooden boards with long vertical wood grain shown by a few thin darker lines and small knots, paint slightly worn on edges", "faded vermilion #9B4A3A with grain #84402F and worn spots #A9604E"),
 ("M_Wood_Dark", 1.0, "grid", "dark stained wooden planks running vertically, 6 planks across, thin dark gaps between planks, grain drawn with a few thin lines", "dark brown #4A3A30 with grain #3C2F27 and plank variation #554336"),
 ("M_Metal_Green", 1.0, "free", "a flat painted steel surface in dull green with a few small scuffs, scratches and paint chips drawn as tiny flat shapes", "dull green #4F6B58 with chips #3E5546 and scuffs #61806B"),
 ("M_Metal_Rust", 1.0, "grid", "a rusty corrugated galvanized iron sheet with vertical corrugations, about 13 ridges across, rust patches as flat orange-brown shapes over the grey metal, light and shadow sides of each ridge as two flat tones", "grey metal #8E9094 and #7A7C80, rust #8A5A3C and #A0693F"),
 ("M_Metal_Grey", 1.0, "free", "a flat painted grey steel surface with a few small scuffs and scratches drawn as tiny flat marks", "grey #8C8F93 with marks #7A7D81 and #9EA1A5"),
 ("M_Roof_Green", 2.0, "free", "a flat rooftop floor coated in green waterproof urethane paint, with a few hairline cracks, two repaired patches in a slightly different green and sparse ink dots", "green #6E8F6A with patches #648463 and cracks #4F6B4D"),
 ("M_Asphalt", 4.0, "free", "an asphalt road surface seen from above: flat grey with a sparse scatter of small darker and lighter aggregate dots, two thin crack lines and one darker rectangular patch repair", "grey #6F6C69 with dots #5E5B58 and #807D7A, patch #625F5C"),
 ("M_Paving", 1.2, "grid", "a sidewalk of square concrete paving blocks 30 cm by 30 cm, 4 by 4, aligned joints, thin dark joint lines, blocks in two mixed colors in a random pattern", "grey-beige #BDB3A3 and muted red #A9796A, joints #6E665C"),
 ("M_Dirt", 3.0, "free", "packed sandy dirt ground of a small park clearing seen from above: flat fill with a sparse scatter of ink dots, tiny pebbles and a few faint footprint smudges", "sandy beige #D9C9A8 with dots #BFAE8C and pebbles #A89A80"),
]
common = ("Seamless tileable texture swatch for a stylized cel-shaded 3D game. The surface fills the entire square image edge to edge, seen perfectly flat "
  "and straight-on (orthographic), no perspective, no lighting, no shadows, no vignette, no border, no frame, no objects. "
  "Style: flat-colored anime background painting: flat base color fills, pattern drawn with thin hand-drawn warm-dark ink lines, "
  "a few sparse ink dots or short hatching strokes, muted low-saturation colors, no photographic detail, no noise, no gradients, "
  "even brightness everywhere. The pattern continues seamlessly past all four edges. No text, no letters, no logos.")
items, meta = [], {}
for n, m, kind, pr, col in T:
    items.append({"name": n, "out": f"concept/art/env/textures/src/{n}.png", "prompt": f"SURFACE: {pr}. COLORS: {col}. SCALE: the square shows {m} m by {m} m of surface."})
    meta[n] = {"meters": m, "kind": kind}
here = pathlib.Path(__file__).parent
json.dump({"note": "Base pattern textures -> tools/env_assets/seamless.py -> concept/art/env/textures/<name>.png. Generated from textures_list.py.",
           "defaults": {"quality": "high", "resolution": "2k", "aspect": "1:1"}, "common": common, "items": items},
          open(here / "textures.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
json.dump(meta, open(here / "textures_meta.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(items), "textures")
