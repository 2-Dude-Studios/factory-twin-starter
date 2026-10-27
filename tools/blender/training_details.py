"""Builds the air-dump valve, the line control station and the inspection panel (M12) and exports them for Unity.

Run headless:
  blender -b --factory-startup -P tools/blender/training_details.py -- <LOTO_Valve.fbx> <Training_Station.fbx> [<out.blend>]

The valve's parts (Valve_Body, Valve_Lever, Valve_Silencer) go to Assets/_Project/Art/LOTO, where LotoProps picks
them up by name; the station's (ControlStation_Panel, ControlStation_Post, PushButton_Bezel) and the inspection
panel (Inspection) to Assets/_Project/Art/Training, where TrainingPointsSetup does. Pivots match the builders'
placeholders; geometry is in Unity's axes (unity_parts.py). The buttons themselves stay the training points'
own caps, so their lit colours and pokes are unchanged.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_parts import Part, material, save_and_export, setup_scene  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT_VALVE = argv[0] if argv else 'LOTO_Valve.fbx'
OUT_STATION = argv[1] if len(argv) > 1 else 'Training_Station.fbx'
OUT_BLEND = argv[2] if len(argv) > 2 else None

setup_scene()

VALVE_BLUE = material('Valve_Anodised', (0.06, 0.2, 0.55), metallic=0.6, roughness=0.35)
STEEL = material('Detail_Steel', (0.72, 0.73, 0.75), metallic=1.0, roughness=0.3)
DARK_STEEL = material('Detail_DarkSteel', (0.18, 0.19, 0.2), metallic=0.8, roughness=0.45)
BRASS = material('Detail_Brass', (0.78, 0.58, 0.24), metallic=1.0, roughness=0.35)
BRONZE = material('Valve_SinteredBronze', (0.55, 0.38, 0.2), metallic=0.7, roughness=0.85)
TUBE_BLUE = material('Valve_PUTube', (0.1, 0.35, 0.85), roughness=0.4)
COLLAR = material('Valve_FittingCollar', (0.05, 0.6, 0.85), roughness=0.5)
RED_GRIP = material('Valve_RedGrip', (0.7, 0.04, 0.03), roughness=0.5)
SAFETY_YELLOW = material('Detail_SafetyYellow', (0.95, 0.72, 0.04), roughness=0.5)
BLACK = material('Detail_Black', (0.02, 0.02, 0.022), roughness=0.6)
ENCLOSURE = material('Station_EnclosureGrey', (0.72, 0.73, 0.71), roughness=0.45)
LEGEND = material('Station_LegendPlate', (0.03, 0.03, 0.035), roughness=0.35)
HEADER = material('Station_HeaderBlue', (0.05, 0.16, 0.38), roughness=0.4)
CHROME = material('Station_Chrome', (0.85, 0.86, 0.88), metallic=1.0, roughness=0.15)
GLASS = material('Inspection_SmokedGlass', (0.03, 0.05, 0.06), metallic=0.2, roughness=0.05)
LABEL_WHITE = material('Detail_LabelWhite', (0.92, 0.92, 0.9), roughness=0.6)


def bolt_heads(part, centers, axis, radius=0.004, depth=0.003, mat=DARK_STEEL):
    for c in centers:
        part.cylinder(c, radius, depth, axis, mat, segments=6)


# ===================== Air-dump valve (LOTO_Valve.fbx) =====================
# Valve_Body: pivot at the training point, on the CNC's east end (the machine's face is 60 mm west). A lockable
# 3/2 quarter-turn dump valve: anodised block, push-in fittings and blue air tube on its north and south ports
# running back into the machine, a standoff bracket to the machine, a lockout tab and a yellow warning plate.
body = Part('Valve_Body')
body.box((0, 0, 0), (0.05, 0.065, 0.075), VALVE_BLUE, bevel=0.004)
body.box((0.0255, 0.026, 0), (0.001, 0.009, 0.05), SAFETY_YELLOW)
body.cylinder((0.028, 0, 0), 0.012, 0.007, 'x', STEEL, segments=28)
body.box((0.027, -0.03, 0), (0.003, 0.016, 0.02), STEEL, bevel=0.001, segments=1)
body.cylinder((0.029, -0.032, 0), 0.0035, 0.0035, 'x', BLACK, segments=16)
# Bracket and standoffs back to the machine.
body.box((-0.0285, 0, 0), (0.007, 0.1, 0.1), DARK_STEEL, bevel=0.0015, segments=1)
for y in (-0.04, 0.04):
    body.box((-0.046, y, 0), (0.028, 0.012, 0.08), DARK_STEEL)
bolt_heads(body, [(-0.024, y, z) for y in (-0.04, 0.04) for z in (-0.042, 0.042)], 'x')
# Ports: hex nipple, push-in fitting with its release collar, tube bending back into the machine.
for s in (-1, 1):
    body.cylinder((0, 0, s * 0.042), 0.011, 0.01, 'z', BRASS, segments=6)
    body.cylinder((0, 0, s * 0.051), 0.0085, 0.009, 'z', BRASS, segments=24)
    body.cylinder((0, 0, s * 0.0575), 0.0075, 0.004, 'z', COLLAR, segments=24)
    path = [(0, 0, s * 0.056), (0, 0, s * 0.07)]
    path += [(-0.022 + 0.022 * math.cos(math.radians(a)), 0, s * (0.07 + 0.022 * math.sin(math.radians(a)))) for a in range(15, 91, 15)]
    path += [(-0.064, 0, s * 0.092)]
    body.tube(path, 0.004, TUBE_BLUE, segments=12)
body.build((0, 0, 0))

# Valve_Lever: pivot on the turning axis (+X out of the body's east face): hub, flat steel arm up to a red vinyl
# grip, a lockout ear under the hub. Up is supply, a quarter turn south is exhaust (XrTurnHandle).
lever = Part('Valve_Lever')
lever.cylinder((0.004, 0, 0), 0.012, 0.008, 'x', STEEL, segments=28)
lever.cylinder((0.0095, 0, 0), 0.005, 0.003, 'x', DARK_STEEL, segments=6)
lever.box((0.006, 0.045, 0), (0.005, 0.075, 0.014), STEEL, bevel=0.0015, segments=2)
lever.box((0.007, 0.078, 0), (0.012, 0.05, 0.02), RED_GRIP, bevel=0.0045)
lever.box((0.004, -0.018, 0), (0.003, 0.016, 0.018), STEEL, bevel=0.001, segments=1)
lever.cylinder((0.0058, -0.021, 0), 0.0032, 0.001, 'x', BLACK, segments=16)
lever.build((0.1, 0, 0))

# Valve_Silencer: pivot 60 mm below the valve's centre; a brass hex nipple threaded into the body's exhaust port,
# a sintered-bronze muffler under it.
silencer = Part('Valve_Silencer')
silencer.cylinder((0, 0.0215, 0), 0.008, 0.012, 'y', BRASS, segments=6)
silencer.cylinder((0, 0.012, 0), 0.0065, 0.007, 'y', BRASS, segments=20)
silencer.cylinder((0, -0.004, 0), 0.0115, 0.026, 'y', BRONZE, segments=24, smooth=True)
silencer.cylinder((0, -0.021, 0), 0.0115, 0.008, 'y', BRONZE, segments=24, radius2=0.006, smooth=True)
silencer.build((0.2, 0, 0))

save_and_export(OUT_VALVE, None)
for obj in list(bpy.context.scene.objects):
    bpy.data.objects.remove(obj)

# ===================== Control station and inspection panel (Training_Station.fbx) =====================
# ControlStation_Panel: pivot at the enclosure's centre, its front (z -35 mm) facing the cell (south). A
# 440 x 300 x 70 mm light-grey pushbutton enclosure: front cover with corner screws, a blue header strip, black
# legend plates beside the four buttons (TrainingPointsSetup prints the words), a cable gland underneath and a
# clamp to the post.
BUTTONS = [(-0.14, 0.065), (-0.14, -0.065), (0.07, 0.065), (0.07, -0.065)]
panel = Part('ControlStation_Panel')
panel.box((0, 0, 0.004), (0.44, 0.3, 0.062), ENCLOSURE, bevel=0.01)
panel.box((0, 0, -0.0305), (0.42, 0.28, 0.006), ENCLOSURE, bevel=0.004, segments=2)
bolt_heads(panel, [(x, y, -0.0345) for x in (-0.195, 0.195) for y in (-0.125, 0.125)], 'z', radius=0.0045, depth=0.002, mat=STEEL)
panel.box((0, 0.118, -0.0342), (0.36, 0.03, 0.0014), HEADER, bevel=0.0005, segments=1)
for x, y in BUTTONS:
    panel.box((x + 0.098, y, -0.0342), (0.1, 0.028, 0.0014), LEGEND, bevel=0.0005, segments=1)
panel.cylinder((0, -0.15, 0.0), 0.011, 0.016, 'y', BLACK, segments=6)
panel.cylinder((0, -0.162, 0.0), 0.0075, 0.014, 'y', BLACK, segments=20)
panel.box((0, -0.04, 0.045), (0.09, 0.12, 0.02), DARK_STEEL, bevel=0.002, segments=1)
panel.build((0, 0.6, 0))

# PushButton_Bezel: pivot at a button's centre on the enclosure's front; a chrome ring and black collar round
# the cap, standing 6 mm proud.
bezel = Part('PushButton_Bezel')
bezel.ring((0, 0, -0.003), 0.038, 0.0305, 0.006, 'z', CHROME, segments=40)
bezel.ring((0, 0, -0.0005), 0.044, 0.038, 0.001, 'z', BLACK, segments=40)
bezel.build((0.5, 0.6, 0))

# ControlStation_Post: pivot at the post's centre (0.5 m up); a 80 mm square steel tube on a floor plate with four
# anchors, a yellow band at eye-level-for-a-forklift, and a head plate the enclosure clamps to.
post = Part('ControlStation_Post')
post.box((0, -0.025, 0), (0.08, 0.93, 0.08), DARK_STEEL, bevel=0.004, segments=2)
post.box((0, -0.494, 0), (0.25, 0.012, 0.25), DARK_STEEL, bevel=0.002, segments=1)
bolt_heads(post, [(x, -0.486, z) for x in (-0.1, 0.1) for z in (-0.1, 0.1)], 'y', radius=0.008, depth=0.006, mat=STEEL)
post.box((0, -0.1, 0), (0.084, 0.12, 0.084), SAFETY_YELLOW, bevel=0.003, segments=1)
post.box((0, 0.445, 0), (0.12, 0.012, 0.1), DARK_STEEL, bevel=0.002, segments=1)
post.build((0.8, 0.5, 0))

# Inspection: pivot at the plate's centre on the CNC's east end, facing east (+X). A 200 x 200 mm guarded access
# door: steel frame with corner bolts, a smoked window onto the tool area, hinges on its south edge, a latch on the
# north, a guard-interlock switch and a white checklist label (TrainingPointsSetup prints it).
insp = Part('Inspection')
insp.box((-0.004, 0, 0), (0.012, 0.2, 0.2), STEEL, bevel=0.003, segments=2)
insp.box((0.0025, 0.035, 0), (0.002, 0.098, 0.15), BLACK, bevel=0.001, segments=1)
insp.box((0.0036, 0.035, 0), (0.001, 0.088, 0.14), GLASS)
bolt_heads(insp, [(0.002, y, z) for y in (-0.088, 0.088) for z in (-0.088, 0.088)], 'x', radius=0.0045, depth=0.003)
for y in (-0.06, 0.06):
    insp.cylinder((0.004, y, -0.104), 0.006, 0.035, 'y', DARK_STEEL, segments=16)
insp.box((0.009, 0.0, 0.088), (0.012, 0.05, 0.014), BLACK, bevel=0.003)
insp.box((0.009, -0.07, 0.078), (0.016, 0.034, 0.03), SAFETY_YELLOW, bevel=0.002, segments=1)
insp.box((0.0175, -0.07, 0.078), (0.004, 0.012, 0.008), RED_GRIP, bevel=0.001, segments=1)
insp.box((0.0024, -0.055, -0.015), (0.001, 0.06, 0.13), LABEL_WHITE, bevel=0.0004, segments=1)
insp.build((1.0, 0.6, 0))

save_and_export(OUT_STATION, OUT_BLEND)
