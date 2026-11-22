# Next objects to bring to 3D

The owner's priority (2026-10-17). "On Meta" is how many such objects MetaStation has. Tilesets are converted tg spritesheets (written by `tools/Anomaly.Tools.Icons`, not stored in git). Coordinates are the top-left corner of the tile in sheet pixels (x,y). Directions follow the BYOND file order: S, N, E, W (then SE, SW, NE, NW); "N×M" is directions × animation frames, animation frames listed in order. States marked "overlay" are drawn by tg over the body.

## 0. Lights first

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Tube light**<br>`/obj/machinery/light (+ cold, dim, no_nightlight)` | 240 | `godot\assets\tg\icons\obj\lighting.png` | 32×32 | `tube` | body with tube | 4×1 | S: 0,0<br>N: 32,0<br>E: 64,0<br>W: 96,0 |
|  |  |  |  | `tube-empty` | no tube | 4×1 | S: 256,0<br>N: 288,0<br>E: 320,0<br>W: 352,0 |
|  |  |  |  | `tube-burned` | burned out | 4×1 | S: 384,0<br>N: 416,0<br>E: 448,0<br>W: 480,0 |
|  |  |  |  | `tube-broken` | broken | 4×1 | S: 512,0<br>N: 0,32<br>E: 32,32<br>W: 64,32 |
|  |  |  |  | `tube_emergency` | emergency mode | 4×1 | S: 128,0<br>N: 160,0<br>E: 192,0<br>W: 224,0 |
| `tube glow (overlay)` |  | `godot\assets\tg\icons\obj\lighting_overlay.png` | 32×32 | `tube` | light | 4×1 | S: 0,0<br>N: 32,0<br>E: 64,0<br>W: 96,0 |
|  |  |  |  | `tube_nightshift` | night shift | 4×1 | S: 288,0<br>N: 0,32<br>E: 32,32<br>W: 64,32 |
|  |  |  |  | `tube_emergency` | emergency | 4×1 | S: 256,32<br>N: 288,32<br>E: 0,64<br>W: 32,64 |
|  |  |  |  | `tube_flickering` | flickering | 4×8 | S: 224,64 32,96 160,96 288,96 96,128 224,128 32,160 160,160<br>N: 256,64 64,96 192,96 0,128 128,128 256,128 64,160 192,160<br>E: 288,64 96,96 224,96 32,128 160,128 288,128 96,160 224,160<br>W: 0,96 128,96 256,96 64,128 192,128 0,160 128,160 256,160 |
| **Small light**<br>`/obj/machinery/light/small (+ dim, broken, red)` | 368 | `godot\assets\tg\icons\obj\lighting.png` | 32×32 | `bulb` | body with bulb | 4×1 | S: 96,32<br>N: 128,32<br>E: 160,32<br>W: 192,32 |
|  |  |  |  | `bulb-empty` | no bulb | 4×1 | S: 480,32<br>N: 512,32<br>E: 0,64<br>W: 32,64 |
|  |  |  |  | `bulb-burned` | burned out | 4×1 | S: 64,64<br>N: 96,64<br>E: 128,64<br>W: 160,64 |
|  |  |  |  | `bulb-broken` | broken | 4×1 | S: 352,32<br>N: 384,32<br>E: 416,32<br>W: 448,32 |
|  |  |  |  | `bulb_emergency` | emergency mode | 4×1 | S: 224,32<br>N: 256,32<br>E: 288,32<br>W: 320,32 |
| `bulb glow (overlay)` |  | `godot\assets\tg\icons\obj\lighting_overlay.png` | 32×32 | `bulb` | light | 4×1 | S: 128,0<br>N: 160,0<br>E: 192,0<br>W: 224,0 |
|  |  |  |  | `bulb_nightshift` | night shift | 4×1 | S: 96,32<br>N: 128,32<br>E: 160,32<br>W: 192,32 |
|  |  |  |  | `bulb_emergency` | emergency | 4×1 | S: 64,64<br>N: 96,64<br>E: 128,64<br>W: 160,64 |
|  |  |  |  | `bulb_flickering` | flickering | 4×8 | S: 288,160 96,192 224,192 32,224 160,224 288,224 96,256 224,256<br>N: 0,192 128,192 256,192 64,224 192,224 0,256 128,256 256,256<br>E: 32,192 160,192 288,192 96,224 224,224 32,256 160,256 288,256<br>W: 64,192 192,192 0,224 128,224 256,224 64,256 192,256 0,288 |
| **Floor light**<br>`/obj/machinery/light/floor` | 33 | `godot\assets\tg\icons\obj\lighting.png` | 32×32 | `floor` | body | 1×1 | 384,64 |
|  |  |  |  | `floor-empty` | no lamp | 1×1 | 448,64 |
|  |  |  |  | `floor-burned` | burned out | 1×1 | 480,64 |
|  |  |  |  | `floor-broken` | broken | 1×1 | 512,64 |
|  |  |  |  | `floor_emergency` | emergency mode | 1×1 | 416,64 |
| `floor light glow (overlay)` |  | `godot\assets\tg\icons\obj\lighting_overlay.png` | 32×32 | `floor` | light | 1×1 | 256,0 |
|  |  |  |  | `floor_nightshift` | night shift | 1×1 | 224,32 |
|  |  |  |  | `floor_emergency` | emergency | 1×1 | 192,64 |
|  |  |  |  | `floor_flickering` | flickering | 1×8 | 32,288 64,288 96,288 128,288 160,288 192,288 224,288 256,288 |

## 1. What looks broken now

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Railing**<br>`/obj/structure/railing` | 80 | `godot\assets\tg\icons\obj\railings.png` | 32×48 | `railing` | straight (S/N/E/W), corner (diagonals) | 8×1 | S: 0,0<br>N: 32,0<br>E: 64,0<br>W: 96,0<br>SE: 128,0<br>SW: 0,48<br>NE: 32,48<br>NW: 64,48 |
|  |  |  |  | `railing_corner` | corner joint | 4×1 | S: 96,48<br>N: 128,48<br>E: 0,96<br>W: 32,96 |
|  |  |  |  | `railing_end` | end cap | 4×1 | S: 64,96<br>N: 96,96<br>E: 128,96<br>W: 0,144 |
|  |  |  |  | `railing_end_flip` | end cap, flipped | 4×1 | S: 32,144<br>N: 64,144<br>E: 96,144<br>W: 128,144 |
| **Conveyor**<br>`/obj/machinery/conveyor` | 37 | `godot\assets\tg\icons\obj\machines\recycling.png` | 32×32 | `conveyor0` | stopped | 8×1 | S: 0,0<br>N: 32,0<br>E: 64,0<br>W: 96,0<br>SE: 128,0<br>SW: 160,0<br>NE: 192,0<br>NW: 224,0 |
|  |  |  |  | `conveyor1` | moving forward | 8×4 | S: 256,0 512,0 192,32 448,32<br>N: 288,0 544,0 224,32 480,32<br>E: 320,0 0,32 256,32 512,32<br>W: 352,0 32,32 288,32 544,32<br>SE: 384,0 64,32 320,32 0,64<br>SW: 416,0 96,32 352,32 32,64<br>NE: 448,0 128,32 384,32 64,64<br>NW: 480,0 160,32 416,32 96,64 |
|  |  |  |  | `conveyor-1` | moving backward | 8×4 | S: 128,64 384,64 64,96 320,96<br>N: 160,64 416,64 96,96 352,96<br>E: 192,64 448,64 128,96 384,96<br>W: 224,64 480,64 160,96 416,96<br>SE: 256,64 512,64 192,96 448,96<br>SW: 288,64 544,64 224,96 480,96<br>NE: 320,64 0,96 256,96 512,96<br>NW: 352,64 32,96 288,96 544,96 |
|  |  |  |  | `conveyor0-flipped` | stopped, flipped | 8×1 | S: 0,128<br>N: 32,128<br>E: 64,128<br>W: 96,128<br>SE: 128,128<br>SW: 160,128<br>NE: 192,128<br>NW: 224,128 |
|  |  |  |  | `conveyor1-flipped` | forward, flipped | 8×4 | S: 256,128 512,128 192,160 448,160<br>N: 288,128 544,128 224,160 480,160<br>E: 320,128 0,160 256,160 512,160<br>W: 352,128 32,160 288,160 544,160<br>SE: 384,128 64,160 320,160 0,192<br>SW: 416,128 96,160 352,160 32,192<br>NE: 448,128 128,160 384,160 64,192<br>NW: 480,128 160,160 416,160 96,192 |
|  |  |  |  | `conveyor-1-flipped` | backward, flipped | 8×4 | S: 128,192 384,192 64,224 320,224<br>N: 160,192 416,192 96,224 352,224<br>E: 192,192 448,192 128,224 384,224<br>W: 224,192 480,192 160,224 416,224<br>SE: 256,192 512,192 192,224 448,224<br>SW: 288,192 544,192 224,224 480,224<br>NE: 320,192 0,224 256,224 512,224<br>NW: 352,192 32,224 288,224 544,224 |
| **Plastic flaps**<br>`/obj/structure/plasticflaps (+ opaque)` | 24 | `godot\assets\tg\icons\obj\structures.png` | 32×32 | `plasticflaps` | flaps | 1×1 | 320,288 |

## 2. Wall boxes

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Camera**<br>`/obj/machinery/camera` | 282 | `godot\assets\tg\icons\obj\machines\camera.png` | 32×32 | `camera` | unpowered | 8×7 | S: 0,0 256,0 512,0 96,32 352,32 608,32 192,64<br>N: 32,0 288,0 544,0 128,32 384,32 640,32 224,64<br>E: 64,0 320,0 576,0 160,32 416,32 0,64 256,64<br>W: 96,0 352,0 608,0 192,32 448,32 32,64 288,64<br>SE: 128,0 384,0 640,0 224,32 480,32 64,64 320,64<br>SW: 160,0 416,0 0,32 256,32 512,32 96,64 352,64<br>NE: 192,0 448,0 32,32 288,32 544,32 128,64 384,64<br>NW: 224,0 480,0 64,32 320,32 576,32 160,64 416,64 |
|  |  |  |  | `camera_on` | running (lens turning) | 8×7 | S: 448,64 32,96 288,96 544,96 128,128 384,128 640,128<br>N: 480,64 64,96 320,96 576,96 160,128 416,128 0,160<br>E: 512,64 96,96 352,96 608,96 192,128 448,128 32,160<br>W: 544,64 128,96 384,96 640,96 224,128 480,128 64,160<br>SE: 576,64 160,96 416,96 0,128 256,128 512,128 96,160<br>SW: 608,64 192,96 448,96 32,128 288,128 544,128 128,160<br>NE: 640,64 224,96 480,96 64,128 320,128 576,128 160,160<br>NW: 0,96 256,96 512,96 96,128 352,128 608,128 192,160 |
|  |  |  |  | `camera_in_use` | someone watching | 8×7 | S: 416,224 0,256 256,256 512,256 96,288 352,288 608,288<br>N: 448,224 32,256 288,256 544,256 128,288 384,288 640,288<br>E: 480,224 64,256 320,256 576,256 160,288 416,288 0,320<br>W: 512,224 96,256 352,256 608,256 192,288 448,288 32,320<br>SE: 544,224 128,256 384,256 640,256 224,288 480,288 64,320<br>SW: 576,224 160,256 416,256 0,288 256,288 512,288 96,320<br>NE: 608,224 192,256 448,256 32,288 288,288 544,288 128,320<br>NW: 640,224 224,256 480,256 64,288 320,288 576,288 160,320 |
|  |  |  |  | `camera_emp` | after EMP | 8×4 | S: 224,160 480,160 64,192 320,192<br>N: 256,160 512,160 96,192 352,192<br>E: 288,160 544,160 128,192 384,192<br>W: 320,160 576,160 160,192 416,192<br>SE: 352,160 608,160 192,192 448,192<br>SW: 384,160 640,160 224,192 480,192<br>NE: 416,160 0,192 256,192 512,192<br>NW: 448,160 32,192 288,192 544,192 |
|  |  |  |  | `camera_off` | disabled | 8×1 | S: 576,192<br>N: 608,192<br>E: 640,192<br>W: 0,224<br>SE: 32,224<br>SW: 64,224<br>NE: 96,224<br>NW: 128,224 |
|  |  |  |  | `camera_panel` | open panel (overlay) | 8×1 | S: 160,224<br>N: 192,224<br>E: 224,224<br>W: 256,224<br>SE: 288,224<br>SW: 320,224<br>NE: 352,224<br>NW: 384,224 |
| **Fire alarm**<br>`/obj/machinery/firealarm` | 194 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `fire0` | body | 4×1 | S: 192,416<br>N: 224,416<br>E: 256,416<br>W: 288,416 |
|  |  |  |  | `fire_0` | level 0 (overlay) | 4×1 | S: 608,512<br>N: 0,544<br>E: 32,544<br>W: 64,544 |
|  |  |  |  | `fire_1` | level 1 | 4×1 | S: 96,544<br>N: 128,544<br>E: 160,544<br>W: 192,544 |
|  |  |  |  | `fire_2` | level 2 | 4×1 | S: 224,544<br>N: 256,544<br>E: 288,544<br>W: 320,544 |
|  |  |  |  | `fire_3` | level 3 | 4×2 | S: 352,544 480,544<br>N: 384,544 512,544<br>E: 416,544 544,544<br>W: 448,544 576,544 |
|  |  |  |  | `fire_alerting` | alarm | 4×2 | S: 608,544 96,576<br>N: 0,576 128,576<br>E: 32,576 160,576<br>W: 64,576 192,576 |
|  |  |  |  | `fire_offstation` | off station | 4×1 | S: 480,512<br>N: 512,512<br>E: 544,512<br>W: 576,512 |
|  |  |  |  | `fire_disabled` | disabled | 4×1 | S: 480,480<br>N: 512,480<br>E: 544,480<br>W: 576,480 |
|  |  |  |  | `fire_emag` | emagged | 4×4 | S: 608,480 96,512 224,512 352,512<br>N: 0,512 128,512 256,512 384,512<br>E: 32,512 160,512 288,512 416,512<br>W: 64,512 192,512 320,512 448,512 |
| **Air alarm**<br>`/obj/machinery/airalarm` | 198 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `alarmp` | body | 1×1 | 288,64 |
|  |  |  |  | `alarm0` | normal (overlay) | 1×1 | 352,64 |
|  |  |  |  | `alarm2` | warning | 1×3 | 448,64 480,64 512,64 |
|  |  |  |  | `alarm1` | danger | 1×2 | 384,64 416,64 |
|  |  |  |  | `alarmx` | broken | 1×1 | 224,64 |
| **Power controller (APC)**<br>`/obj/machinery/power/apc` | 158 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `apc0` | body | 1×1 | 288,320 |
|  |  |  |  | `apcox-0` | unlocked (overlay) | 1×1 | 608,352 |
|  |  |  |  | `apcox-1` | locked | 1×1 | 0,384 |
|  |  |  |  | `apco3-0` | not charging | 1×3 | 512,320 544,320 576,320 |
|  |  |  |  | `apco3-1` | charging | 1×6 | 320,320 352,320 384,320 416,320 448,320 480,320 |
|  |  |  |  | `apco3-2` | charged | 1×2 | 608,320 0,352 |
|  |  |  |  | `apco0-0` | equipment: off | 1×1 | 32,384 |
|  |  |  |  | `apco0-1` | equipment: auto off | 1×1 | 64,384 |
|  |  |  |  | `apco0-2` | equipment: on | 1×1 | 96,384 |
|  |  |  |  | `apco0-3` | equipment: auto on | 1×1 | 128,384 |
|  |  |  |  | `apco1-0` | lighting: off | 1×1 | 160,384 |
|  |  |  |  | `apco1-1` | lighting: auto off | 1×1 | 192,384 |
|  |  |  |  | `apco1-2` | lighting: on | 1×1 | 224,384 |
|  |  |  |  | `apco1-3` | lighting: auto on | 1×1 | 256,384 |
|  |  |  |  | `apco2-0` | environment: off | 1×1 | 288,384 |
|  |  |  |  | `apco2-1` | environment: auto off | 1×1 | 320,384 |
|  |  |  |  | `apco2-2` | environment: on | 1×1 | 352,384 |
|  |  |  |  | `apco2-3` | environment: auto on | 1×1 | 384,384 |
|  |  |  |  | `apc1-nocover` | no cover | 1×1 | 416,352 |
|  |  |  |  | `apc2-nocover` | no cover, wiring | 1×1 | 512,352 |
|  |  |  |  | `apcmaint` | maintenance | 1×1 | 576,352 |
|  |  |  |  | `apc-spark` | sparks | 1×7 | 32,352 64,352 96,352 128,352 160,352 192,352 224,352 |
| **Extinguisher cabinet**<br>`/obj/structure/extinguisher_cabinet` | 100 | `godot\assets\tg\icons\obj\wallmounts.png` | 32×32 | `extinguisher` | empty | 1×1 | 64,0 |
|  |  |  |  | `extinguisher_default` | with extinguisher | 1×1 | 96,0 |
|  |  |  |  | `extinguisher_advanced` | with advanced | 1×1 | 128,0 |
|  |  |  |  | `extinguisher_mini` | with mini | 1×1 | 160,0 |
|  |  |  |  | `extinguisher_crafted` | with crafted | 1×1 | 192,0 |
|  |  |  |  | `door` | door closed (overlay) | 1×1 | 0,0 |
|  |  |  |  | `door_open` | door open (overlay) | 1×1 | 32,0 |
| **Newscaster**<br>`/obj/machinery/newscaster` | 94 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `newscaster_off` | body, unpowered | 1×1 | 608,96 |
|  |  |  |  | `newscaster_normal` | screen (overlay) | 1×19 | 480,128 … 416,160 (19 frames in a row) |
|  |  |  |  | `newscaster_wanted` | wanted | 1×9 | 192,128 … 448,128 (9 frames in a row) |
|  |  |  |  | `newscaster_alert` | new message | 1×3 | 96,128 128,128 160,128 |
|  |  |  |  | `crack1` | crack 1 | 1×1 | 0,128 |
|  |  |  |  | `crack2` | crack 2 | 1×1 | 32,128 |
|  |  |  |  | `crack3` | crack 3 | 1×1 | 64,128 |
| **Status display**<br>`/obj/machinery/status_display/evac (61), /ai (38)` | 99 | `godot\assets\tg\icons\obj\machines\status_display.png` | 32×32 | `frame` | body | 1×1 | 32,0 |
|  |  |  |  | `outline` | glow outline | 1×1 | 64,0 |
|  |  |  |  | `default` | logo | 1×2 | 96,0 128,0 |
|  |  |  |  | `greenalert` | green alert | 1×16 | 160,0 … 640,0 (16 frames in a row) |
|  |  |  |  | `bluealert` | blue alert | 1×16 | 672,0 … 64,32 (16 frames in a row) |
|  |  |  |  | `redalert` | red alert | 1×7 | 96,32 128,32 160,32 192,32 224,32 256,32 288,32 |
|  |  |  |  | `deltaalert` | delta alert | 1×7 | 320,32 352,32 384,32 416,32 448,32 480,32 512,32 |
|  |  |  |  | `biohazard` | biohazard | 1×2 | 544,32 576,32 |
|  |  |  |  | `lockdown` | lockdown | 1×2 | 608,32 640,32 |
|  |  |  |  | `radiation` | radiation | 1×2 | 672,32 704,32 |
|  |  |  |  | `broken` | broken | 1×2 | 768,1056 800,1056 |
|  |  |  |  | `ai_neutral` | AI: neutral | 1×3 | 288,96 320,96 352,96 |
|  |  |  |  | `ai_happy` | AI: happy | 1×3 | 192,96 224,96 256,96 |
|  |  |  |  | `ai_off` | AI: off | 1×1 | 768,96 |
|  |  |  |  | `ai_bsod` | AI: BSOD | 1×1 | 736,96 |
|  |  |  |  | `0` | text glyph | 1×1 | 128,64 |
|  |  |  |  | `1` | text glyph | 1×1 | 160,64 |
|  |  |  |  | `2` | text glyph | 1×1 | 192,64 |
|  |  |  |  | `3` | text glyph | 1×1 | 224,64 |
|  |  |  |  | `4` | text glyph | 1×1 | 256,64 |
|  |  |  |  | `5` | text glyph | 1×1 | 288,64 |
|  |  |  |  | `6` | text glyph | 1×1 | 320,64 |
|  |  |  |  | `7` | text glyph | 1×1 | 352,64 |
|  |  |  |  | `8` | text glyph | 1×1 | 384,64 |
|  |  |  |  | `9` | text glyph | 1×1 | 416,64 |
|  |  |  |  | `A` | text glyph | 1×1 | 448,64 |
|  |  |  |  | `B` | text glyph | 1×1 | 480,64 |
|  |  |  |  | `C` | text glyph | 1×1 | 512,64 |
|  |  |  |  | `D` | text glyph | 1×1 | 544,64 |
|  |  |  |  | `E` | text glyph | 1×1 | 576,64 |
|  |  |  |  | `F` | text glyph | 1×1 | 608,64 |
|  |  |  |  | `G` | text glyph | 1×1 | 640,64 |
|  |  |  |  | `H` | text glyph | 1×1 | 672,64 |
|  |  |  |  | `I` | text glyph | 1×1 | 704,64 |
|  |  |  |  | `J` | text glyph | 1×1 | 736,64 |
|  |  |  |  | `K` | text glyph | 1×1 | 768,64 |
|  |  |  |  | `L` | text glyph | 1×1 | 800,64 |
|  |  |  |  | `M` | text glyph | 1×1 | 832,64 |
|  |  |  |  | `N` | text glyph | 1×1 | 864,64 |
|  |  |  |  | `O` | text glyph | 1×1 | 896,64 |
|  |  |  |  | `P` | text glyph | 1×1 | 928,64 |
|  |  |  |  | `Q` | text glyph | 1×1 | 960,64 |
|  |  |  |  | `R` | text glyph | 1×1 | 992,64 |
|  |  |  |  | `S` | text glyph | 1×1 | 1024,64 |
|  |  |  |  | `T` | text glyph | 1×1 | 1056,64 |
|  |  |  |  | `U` | text glyph | 1×1 | 0,96 |
|  |  |  |  | `V` | text glyph | 1×1 | 32,96 |
|  |  |  |  | `W` | text glyph | 1×1 | 64,96 |
|  |  |  |  | `X` | text glyph | 1×1 | 96,96 |
|  |  |  |  | `Y` | text glyph | 1×1 | 128,96 |
|  |  |  |  | `Z` | text glyph | 1×1 | 160,96 |
|  |  |  |  | `:` | text glyph | 1×1 | 800,32 |
|  |  |  |  | `.` | text glyph | 1×1 | 1056,32 |
|  |  |  |  | `-` | text glyph | 1×1 | 32,64 |
|  |  |  |  | `!` | text glyph | 1×1 | 96,64 |
|  |  |  |  | `?` | text glyph | 1×1 | 64,64 |
|  |  |  |  | `+` | text glyph | 1×1 | 960,32 |
|  |  |  |  | `/` | text glyph | 1×1 | 1024,32 |
|  |  |  |  | `(` | text glyph | 1×1 | 864,32 |
|  |  |  |  | `)` | text glyph | 1×1 | 896,32 |
|  |  |  |  | `=` | text glyph | 1×1 | 992,32 |
|  |  |  |  | `,` | text glyph | 1×1 | 0,64 |
|  |  |  |  | `'` | text glyph | 1×1 | 832,32 |
|  |  |  |  | `*` | text glyph | 1×1 | 928,32 |
|  |  |  |  | `~` | text glyph | 1×2 | 736,32 768,32 |
| **Requests console**<br>`/obj/machinery/requests_console` | 39 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `req_comp_off` | body | 1×1 | 480,192 |
|  |  |  |  | `req_comp0` | screen: normal (overlay) | 1×14 | 512,192 … 288,224 (14 frames in a row) |
|  |  |  |  | `req_comp1` | new message | 1×8 | 320,224 352,224 384,224 416,224 448,224 480,224 512,224 544,224 |
|  |  |  |  | `req_comp2` | priority | 1×8 | 576,224 608,224 0,256 32,256 64,256 96,256 128,256 160,256 |
|  |  |  |  | `req_comp3` | emergency | 1×8 | 192,256 224,256 256,256 288,256 320,256 352,256 384,256 416,256 |
|  |  |  |  | `req_comp_open` | open panel | 1×1 | 448,256 |
| **Light switch (added)**<br>`/obj/machinery/light_switch` | 129 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `light-nopower` | unpowered | 1×1 | 416,384 |
|  |  |  |  | `light-off` | off | 1×3 | 448,384 480,384 512,384 |
|  |  |  |  | `light-on` | on | 1×3 | 544,384 576,384 608,384 |
