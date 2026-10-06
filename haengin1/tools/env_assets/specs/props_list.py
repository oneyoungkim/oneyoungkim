# Source of truth for the prop list. Writes specs/props.json (image refs) and specs/props_meta.json (3D + Blender sizes).
# dims = [width X, depth Z (front-back), height Y] in meters, glTF axes (front = +Z). tier H = hero, R = repeated.
import json, pathlib
P = [
 # name, tier, dims[w,d,h], face_limit, aspect, prompt
 ("vending_machine","H",[1.0,0.75,1.83],12000,"1:1","a Korean outdoor drink vending machine: a tall upright cabinet painted pale cream-white with a muted red top band, a large glass display window on the upper half showing two rows of plain drink can and bottle dummies with blank labels, a row of small push buttons under each row, a coin and bill panel on the right, a dark pickup slot at the bottom, a small rain hood on top"),
 ("trash_bin_wheeled","R",[0.58,0.73,1.07],6000,"1:1","a green plastic wheeled municipal trash bin (240 liter) with a hinged lid, two black wheels at the back and a handle bar, slightly scuffed"),
 ("delivery_scooter","H",[0.75,1.9,1.35],15000,"1:1","a small 125cc Korean delivery motorbike in faded white and dark grey, underbone scooter style, with a big square muted red-brown insulated delivery box on the rear rack, round headlight, side mirrors, kickstand down, no badges"),
 ("delivery_bicycle","H",[0.6,1.75,1.1],15000,"1:1","a sturdy black steel delivery city bicycle with thick tires, mudguards, a front wire basket, kickstand down, and a large square cream-colored plastic delivery box strapped on the rear rack"),
 ("truck_1ton_box","H",[1.74,5.1,2.7],20000,"1:1","a Korean one-ton light commercial truck with a flat-nosed cab-over front cab painted pale blue, and behind it a tall closed aluminum box cargo body with silver-white ribbed panels and a rear roll-up door, no badges"),
 ("mixer_truck","H",[2.5,9.0,3.7],20000,"1:1","a concrete mixer truck: a white cab-over cab and a large rotating drum painted in faded dull red and cream stripes on a heavy chassis with three axles, no badges"),
 ("gamasot_stove","H",[0.9,0.9,0.95],10000,"1:1","a large Korean black cast-iron cauldron (gamasot) with a heavy wooden lid made of two halves with a handle, set into a squat square stove base covered in white square tiles, with a small steel firebox door at the bottom front"),
 ("parking_barrier","H",[3.8,0.5,1.1],8000,"3:2","a parking lot boom barrier gate: a squat grey-painted steel control cabinet on a small concrete base, with a long straight arm in red and white stripes lowered horizontally to one side"),
 ("bus_stop_shelter","H",[3.2,1.4,2.6],12000,"3:2","a small Korean village bus stop shelter: slim dark grey steel frame, a flat roof canopy, a clear glass back panel, a narrow metal bench, and a blank sign panel on a side post"),
 ("utility_pole","H",[1.4,0.8,12.0],12000,"9:16","a tall Korean grey concrete utility pole, tapered, with two steel cross-arms with white porcelain insulators near the top, one grey cylindrical transformer can strapped to the pole below them, small metal step bolts, and a yellow-and-black striped guard sleeve around the bottom; short cut cable ends only, no long wires"),
 ("street_lamp_cobra","R",[0.4,2.0,8.0],6000,"9:16","a Korean street light: a slender grey steel pole about 8 m tall on a small base, with a long gently curved arm at the top ending in a flat cobra-head lamp housing"),
 ("street_lamp_trail","R",[0.5,0.5,3.6],6000,"2:3","a traditional-style park lamp post in black painted cast iron, about 3.5 m tall, fluted pole, with a lantern-shaped lamp head with four frosted glass panes and a small hipped cap on top"),
 ("ac_outdoor_unit","R",[0.85,0.4,0.62],6000,"1:1","an air-conditioner outdoor condenser unit: an off-white rectangular steel box with a round fan grille on the front, copper pipe connections wrapped in grey insulation tape on the side, sitting on a simple angled steel wall bracket"),
 ("gas_meter_pipe","R",[0.45,0.22,0.6],5000,"1:1","a Korean household gas meter (small grey box with a round dial window) mounted on a short run of bright yellow painted gas pipe with two elbows and a valve with a small red handle, as it hangs on a wall"),
 ("electric_meter_box","R",[0.45,0.2,0.6],5000,"1:1","a grey metal outdoor electric meter box with a small glass window in the door, two conduit pipes coming out of the bottom, as mounted on a wall"),
 ("lpg_cylinders","R",[0.8,0.45,1.3],6000,"1:1","two tall grey LPG gas cylinders standing side by side, held by a chain on a small steel wall bracket, with brass valves on top and a coiled black rubber hose"),
 ("mailbox_cluster","R",[0.9,0.2,0.6],5000,"1:1","a cluster of six small steel apartment mailboxes in two rows, painted dull silver-grey, each with a small slot and a tiny blank name plate, mounted on a backing board"),
 ("water_tank_roof","R",[1.3,1.3,1.7],6000,"1:1","a sky-blue plastic cylindrical rooftop water storage tank on a short steel frame, with a round screw lid on top and an outlet pipe at the bottom"),
 ("traffic_mirror","R",[0.9,0.4,3.0],6000,"2:3","a convex road safety mirror (round, with a muted red-orange rim and a small hood) on a tall slender grey steel pole with a yellow-black striped base sleeve"),
 ("laundry_rack","R",[1.7,0.6,1.0],10000,"3:2","a foldable stainless steel clothes drying rack opened in a wing shape with a few towels and shirts hanging on it"),
 ("basin_planter_onion","R",[0.65,0.65,0.55],8000,"1:1","a black rubber wide round tub (Korean gomu daya basin) filled with soil and growing green onions, slightly worn"),
 ("styrofoam_planter","R",[0.6,0.4,0.45],8000,"1:1","a white styrofoam box used as a vegetable planter, with young lettuce and chili seedlings growing in soil"),
 ("clay_pots_cluster","R",[0.9,0.5,0.6],10000,"1:1","a cluster of five terracotta and brown glazed flower pots of different sizes with small leafy plants and a jade plant"),
 ("jangdok_set","R",[1.4,0.8,0.8],10000,"1:1","a group of five Korean earthenware sauce jars (onggi) in three sizes, dark brown glaze with lids, standing on a low stone ledge"),
 ("clothing_bin","R",[1.0,0.8,1.8],6000,"1:1","a Korean used-clothing collection bin: a tall steel box painted muted green with a pull-down drop chute near the top and short legs, completely blank surfaces"),
 ("trash_bag_pile","R",[1.0,0.8,0.6],8000,"1:1","a small pile of white and pale blue semi-transparent plastic garbage bags, tied at the top"),
 ("plastic_crates_stack","R",[0.45,0.35,1.1],8000,"2:3","a stack of four empty plastic beverage crates in faded green and yellow with open grid sides"),
 ("cardboard_boxes","R",[0.8,0.6,0.8],6000,"1:1","a stack of three plain brown cardboard boxes with packing tape, slightly crushed, with one flattened box leaning against them"),
 ("fire_hydrant","R",[0.45,0.35,0.9],6000,"1:1","a Korean red above-ground fire hydrant, short pillar type, with two side outlets with caps and a cap on top"),
 ("roll_container","R",[0.8,0.6,1.7],10000,"2:3","a convenience-store delivery roll cage cart: a steel wire mesh cage on four casters, half loaded with plain cardboard boxes"),
 ("ice_cream_freezer","R",[1.2,0.65,0.85],6000,"1:1","a chest-type ice cream freezer with a sliding glass top lid, white body with a blank muted blue band, on small feet"),
 ("conv_table_set","R",[1.5,1.5,0.85],10000,"1:1","an outdoor plastic table set as found outside a Korean convenience store: a square green plastic table with four matching green plastic armless chairs"),
 ("parasol_folded","R",[0.5,0.5,2.4],5000,"2:3","a folded green outdoor patio parasol, closed with the fabric wrapped around the pole, standing in a heavy round grey concrete base"),
 ("traffic_cones","R",[1.0,0.6,0.7],6000,"1:1","three traffic cones in muted red with white reflective bands, two standing and one tipped over on its side"),
 ("park_bench","R",[1.8,0.65,0.85],6000,"3:2","a park bench with warm brown wooden slats on black cast iron side frames"),
 ("exercise_machine","R",[1.4,1.0,1.6],10000,"1:1","an outdoor public exercise machine (air walker type) with a steel frame painted faded blue and yellow, two swinging foot pedals and hand grips"),
 ("stockpot_burner","R",[0.6,0.6,0.9],8000,"1:1","a large dented stainless steel stock pot sitting on a low three-legged cast iron outdoor gas burner stand"),
 ("cement_bags","R",[1.1,1.1,0.7],6000,"1:1","a pile of grey-white paper cement bags on a wooden pallet, blank bags"),
 ("euroform_stack","R",[1.2,0.6,0.9],6000,"1:1","a stack of steel-framed plywood concrete form panels lying flat on two wooden blocks, pale yellow plywood faces with grey steel frames"),
 ("cat_loaf","H",[0.22,0.4,0.25],8000,"1:1","a chubby cream-and-orange tabby cat sitting in a loaf pose with all paws tucked under, eyes half closed"),
]
common = ("Reference image for building a 3D game prop. Single isolated object in a three-quarter front view from slightly above "
  "(camera about 35 degrees to the left of the front and 20 degrees above), the whole object fully visible and centered with generous margin, "
  "on a pure white background, no floor, no cast shadow, nothing else in the image. Stylized hand-painted look for a cel-shaded game: "
  "flat matte base colors only (albedo), even shadowless lighting, no specular highlights, no reflections, no ambient occlusion, no gradients, "
  "no black outlines. Simple clean readable shapes with crisp edges and correct real-world proportions. Muted low-saturation palette that fits "
  "a Seoul hillside neighborhood drawn in a flat-colored anime background style. No text, no letters, no numbers, no logos, no brand marks.")
items, meta = [], {}
for n, tier, dims, fl, ar, pr in P:
    items.append({"name": n, "out": f"concept/art/env/props/ref/{n}.png", "aspect": ar, "prompt": "OBJECT: " + pr + "."})
    meta[n] = {"tier": tier, "dims": dims, "face_limit": fl}
here = pathlib.Path(__file__).parent
json.dump({"note": "Prop reference images (white background, three-quarter view) -> image to 3D. Generated from props_list.py.",
           "defaults": {"quality": "high", "resolution": "2k", "aspect": "1:1"}, "common": common, "items": items},
          open(here / "props.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
json.dump(meta, open(here / "props_meta.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(items), "props")
