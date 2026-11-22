# Next objects to bring to 3D (second wave)

List of 2026-10-18; the first wave (`next_3d_priority.md`: lights, railings, conveyor, flaps, wall boxes) is done. "On Meta" is how many such objects MetaStation has (including the subtypes in the tg column). Tilesets are converted tg spritesheets (written by `tools/Anomaly.Tools.Icons`, not stored in git). Coordinates are the top-left corner of the tile in sheet pixels (x,y). Directions follow the BYOND file order: S, N, E, W (then SE, SW, NE, NW); "N×M" is directions × animation frames. States marked "overlay" are drawn by tg over the body.

## 1. Small wall fixtures (the wall rule, as for the alarms)

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Door button**<br>`/obj/machinery/button/door (+ /button/flasher etc.)` | 108 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `button` | normal | 1×1 | 416,32 |
|  |  |  |  | `button-warning` | with warning | 1×1 | 448,32 |
|  |  |  |  | `button-nopower` | unpowered | 1×1 | 480,32 |
|  |  |  |  | `button-warning-nopower` | with warning, unpowered | 1×1 | 512,32 |
|  |  |  |  | `button-open` | open panel | 1×1 | 352,32 |
|  |  |  |  | `button-warning-open` | with warning, open | 1×1 | 384,32 |
|  |  |  |  | `button-light-mask` | glow (overlay) | 1×1 | 544,32 |
|  |  |  |  | `button-overlay-success` | press: granted (overlay) | 1×2 | 32,64 64,64 |
|  |  |  |  | `button-overlay-error` | press: denied (overlay) | 1×2 | 96,64 128,64 |
|  |  |  |  | `button_assembly` | unassembled | 1×1 | 320,32 |
| **Airlock access button**<br>`/obj/machinery/door_buttons/access_button` | 4 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `access_button_standby` | idle | 1×1 | 352,0 |
|  |  |  |  | `access_button_off` | off | 1×1 | 384,0 |
|  |  |  |  | `access_button_cycle` | airlock cycle | 1×2 | 416,0 448,0 |
| **Flasher**<br>`/obj/machinery/flasher` | 21 | `godot\assets\tg\icons\obj\wallmounts.png` | 32×32 | `mflash1` | ready | 1×2 | 256,96 0,128 |
|  |  |  |  | `mflash_flash` | flash | 1×3 | 32,128 64,128 96,128 |
|  |  |  |  | `mflash1-p` | unpowered | 1×1 | 128,128 |
|  |  |  |  | `mflash_frame` | frame | 1×1 | 160,128 |
| **Wall healer**<br>`/obj/machinery/wall_healer` | 16 | `godot\assets\tg\icons\obj\machines\wall_healer.png` | 32×32 | `wall_healer` | body | 1×1 | 0,0 |
|  |  |  |  | `open` | open | 1×1 | 96,64 |
|  |  |  |  | `bar0` | charge 0 (overlay; bar0 … bar7) | 1×1 | 64,0 |
|  |  |  |  | `bar7` | charge 7 (overlay) | 1×1 | 32,64 |
|  |  |  |  | `bar_emissive` | charge glow | 1×1 | 64,64 |
|  |  |  |  | `bar_shadow` | stripe shadow | 1×1 | 32,0 |
| **Wall clock**<br>`/obj/machinery/digital_clock` | 11 | `godot\assets\tg\icons\obj\digital_clock.png` | 32×32 | `digital_clock_base` | body | 1×1 | 0,0 |
|  |  |  |  | `+0` | digit 0 (overlay; +1 … +9 likewise) | 1×1 | 64,0 |
|  |  |  |  | `+separator` | colon (overlay) | 1×1 | 32,0 |
| **Pepper spray tank**<br>`/obj/structure/reagent_dispensers/wall/peppertank` | 9 | `godot\assets\tg\icons\obj\medical\chemical_tanks.png` | 32×32 | `pepper` | tank | 1×1 | 128,32 |
| **Keycard authentication**<br>`/obj/machinery/keycard_auth/wall_mounted` | 8 | `godot\assets\tg\icons\obj\machines\wallmounts.png` | 32×32 | `auth_off` | idle | 1×1 | 544,64 |
|  |  |  |  | `auth_on` | confirmation request | 1×2 | 32,0 0,32 |
| **Defibrillator mount**<br>`/obj/machinery/defibrillator_mount` | 4 | `godot\assets\tg\icons\obj\machines\defib_mount.png` | 32×32 | `defibrillator_mount` | empty | 1×1 | 0,0 |
|  |  |  |  | `defib` | defibrillator (overlay) | 1×1 | 64,0 |
|  |  |  |  | `clamps` | clamps (overlay) | 1×1 | 32,0 |
|  |  |  |  | `online` | on (overlay) | 1×1 | 96,0 |
|  |  |  |  | `charge25` | charge 25% | 1×1 | 64,32 |
|  |  |  |  | `charge50` | charge 50% | 1×1 | 96,32 |
|  |  |  |  | `charge75` | charge 75% | 1×1 | 0,64 |
|  |  |  |  | `charge100` | charge 100% | 1×1 | 32,64 |
|  |  |  |  | `emagged` | emagged | 1×2 | 0,32 32,32 |

## 2. Atmospherics (demo item 4)

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Vent**<br>`/obj/machinery/atmospherics/components/unary/vent_pump` | 268 | `godot\assets\tg\icons\obj\machines\atmospherics\unary_devices.png` | 32×32 | `vent_off` | off | 4×1 | S: 416,128<br>N: 448,128<br>E: 480,128<br>W: 512,128 |
|  |  |  |  | `vent_out` | pressurizing | 4×4 | S: 544,192 672,192 800,192 96,224<br>N: 576,192 704,192 0,224 128,224<br>E: 608,192 736,192 32,224 160,224<br>W: 640,192 768,192 64,224 192,224 |
|  |  |  |  | `vent_out-starting` | starting pressurize | 4×12 | S: 672,128 … 416,192 (12 frames in a row)<br>N: 704,128 … 448,192 (12 frames in a row)<br>E: 736,128 … 480,192 (12 frames in a row)<br>W: 768,128 … 512,192 (12 frames in a row) |
|  |  |  |  | `vent_in` | depressurizing | 4×4 | S: 224,288 352,288 480,288 608,288<br>N: 256,288 384,288 512,288 640,288<br>E: 288,288 416,288 544,288 672,288<br>W: 320,288 448,288 576,288 704,288 |
|  |  |  |  | `vent_in-starting` | starting depressurize | 4×12 | S: 352,224 … 96,288 (12 frames in a row)<br>N: 384,224 … 128,288 (12 frames in a row)<br>E: 416,224 … 160,288 (12 frames in a row)<br>W: 448,224 … 192,288 (12 frames in a row) |
|  |  |  |  | `vent_welded` | welded | 4×1 | S: 544,128<br>N: 576,128<br>E: 608,128<br>W: 640,128 |
|  |  |  |  | `vent_out-shutdown` | stopping | 4×16 | S: 224,608 … 480,672 (16 frames in a row)<br>N: 256,608 … 512,672 (16 frames in a row)<br>E: 288,608 … 544,672 (16 frames in a row)<br>W: 320,608 … 576,672 (16 frames in a row) |
|  |  |  |  | `vent_cap` | pipe cap | 4×1 | S: 736,288<br>N: 768,288<br>E: 800,288<br>W: 0,320 |
| **Scrubber**<br>`/obj/machinery/atmospherics/components/unary/vent_scrubber` | 226 | `godot\assets\tg\icons\obj\machines\atmospherics\unary_devices.png` | 32×32 | `scrub_off` | off | 4×1 | S: 544,448<br>N: 576,448<br>E: 608,448<br>W: 640,448 |
|  |  |  |  | `scrub_on` | filtering | 4×24 | S: 800,320 … 416,448 (24 frames in a row)<br>N: 0,352 … 448,448 (24 frames in a row)<br>E: 32,352 … 480,448 (24 frames in a row)<br>W: 64,352 … 512,448 (24 frames in a row) |
|  |  |  |  | `scrub_wide` | wide mode | 4×8 | S: 96,480 224,480 352,480 480,480 608,480 736,480 32,512 160,512<br>N: 128,480 256,480 384,480 512,480 640,480 768,480 64,512 192,512<br>E: 160,480 288,480 416,480 544,480 672,480 800,480 96,512 224,512<br>W: 192,480 320,480 448,480 576,480 704,480 0,512 128,512 256,512 |
|  |  |  |  | `scrub_purge` | purge | 4×8 | S: 288,512 416,512 544,512 672,512 800,512 96,544 224,544 352,544<br>N: 320,512 448,512 576,512 704,512 0,544 128,544 256,544 384,544<br>E: 352,512 480,512 608,512 736,512 32,544 160,544 288,544 416,544<br>W: 384,512 512,512 640,512 768,512 64,544 192,544 320,544 448,544 |
|  |  |  |  | `scrub_welded` | welded | 4×1 | S: 800,448<br>N: 0,480<br>E: 32,480<br>W: 64,480 |
|  |  |  |  | `scrub_cap` | pipe cap | 4×1 | S: 672,448<br>N: 704,448<br>E: 736,448<br>W: 768,448 |
| **Portable connector**<br>`/obj/machinery/atmospherics/components/unary/portables_connector` | 58 | `godot\assets\tg\icons\obj\machines\atmospherics\unary_devices.png` | 32×32 | `connector` | connector | 1×1 | 256,64 |
|  |  |  |  | `connector_cap` | cap | 4×1 | S: 288,64<br>N: 320,64<br>E: 352,64<br>W: 384,64 |
| **Airlock pump**<br>`/obj/machinery/atmospherics/components/unary/airlock_pump` | 29 | `godot\assets\tg\icons\obj\machines\atmospherics\unary_devices.png` | 32×32 | `airlock_pump` | pump | 4×1 | S: 160,768<br>N: 192,768<br>E: 224,768<br>W: 256,768 |
| **Passive vent**<br>`/obj/machinery/atmospherics/components/unary/passive_vent` | 12 | `godot\assets\tg\icons\obj\machines\atmospherics\unary_devices.png` | 32×32 | `passive_vent` | grille | 4×1 | S: 544,576<br>N: 576,576<br>E: 608,576<br>W: 640,576 |
| **Pressure meter**<br>`/obj/machinery/meter` | 57 | `godot\assets\tg\icons\obj\pipes_n_cables\meter.png` | 32×32 | `meter_base` | body | 1×1 | 0,0 |
|  |  |  |  | `pressure_off` | needle: no data | 1×1 | 32,0 |
|  |  |  |  | `pressure0` | pressure 0 | 1×1 | 64,0 |
|  |  |  |  | `pressure1_1` | low (1_1 … 1_6) | 1×1 | 96,0 |
|  |  |  |  | `pressure2_1` | medium (2_1 … 2_6) | 1×1 | 32,32 |
|  |  |  |  | `pressure3_1` | high (3_1 … 3_6) | 1×1 | 224,32 |
|  |  |  |  | `pressure4` | critical | 1×1 | 160,64 |
|  |  |  |  | `buttons0` | buttons (0 … 4) | 1×10 | 192,64 … 224,96 (10 frames in a row) |
|  |  |  |  | `meter_emissive` | glow | 1×1 | 32,160 |
| **Gas pump**<br>`/obj/machinery/atmospherics/components/binary/pump (+ volume_pump)` | 64 | `godot\assets\tg\icons\obj\machines\atmospherics\binary_devices.png` | 32×32 | `pump_off-1` | off (layer 1; -2 for the second) | 4×1 | S: 608,288<br>N: 640,288<br>E: 672,288<br>W: 704,288 |
|  |  |  |  | `pump_on-1` | running | 4×5 | S: 864,288 992,288 1120,288 64,320 192,320<br>N: 896,288 1024,288 1152,288 96,320 224,320<br>E: 928,288 1056,288 0,320 128,320 256,320<br>W: 960,288 1088,288 32,320 160,320 288,320 |
|  |  |  |  | `pump_on-1-emissive` | glow | 4×5 | S: 320,320 448,320 576,320 704,320 832,320<br>N: 352,320 480,320 608,320 736,320 864,320<br>E: 384,320 512,320 640,320 768,320 896,320<br>W: 416,320 544,320 672,320 800,320 928,320 |
|  |  |  |  | `volpump_off-1` | volume: off | 4×1 | S: 896,384<br>N: 928,384<br>E: 960,384<br>W: 992,384 |
|  |  |  |  | `volpump_on-1` | volume: running | 4×10 | S: 288,480 … 256,512 (10 frames in a row)<br>N: 320,480 … 288,512 (10 frames in a row)<br>E: 352,480 … 320,512 (10 frames in a row)<br>W: 384,480 … 352,512 (10 frames in a row) |
| **Valve**<br>`/obj/machinery/atmospherics/components/binary/valve (+ digital)` | 8 | `godot\assets\tg\icons\obj\machines\atmospherics\binary_devices.png` | 32×32 | `mvalve_off-1` | closed | 4×1 | S: 896,0<br>N: 928,0<br>E: 960,0<br>W: 992,0 |
|  |  |  |  | `mvalve_on-1` | open | 4×1 | S: 224,32<br>N: 256,32<br>E: 288,32<br>W: 320,32 |
|  |  |  |  | `mvalve_01-1` | opening | 4×5 | S: 736,32 864,32 992,32 1120,32 64,64<br>N: 768,32 896,32 1024,32 1152,32 96,64<br>E: 800,32 928,32 1056,32 0,64 128,64<br>W: 832,32 960,32 1088,32 32,64 160,64 |
|  |  |  |  | `mvalve_10-1` | closing | 4×5 | S: 832,64 960,64 1088,64 32,96 160,96<br>N: 864,64 992,64 1120,64 64,96 192,96<br>E: 896,64 1024,64 1152,64 96,96 224,96<br>W: 928,64 1056,64 0,96 128,96 256,96 |
|  |  |  |  | `dvalve_off-1` | digital: closed | 4×1 | S: 384,128<br>N: 416,128<br>E: 448,128<br>W: 480,128 |
|  |  |  |  | `dvalve_on-1` | digital: open | 4×1 | S: 896,128<br>N: 928,128<br>E: 960,128<br>W: 992,128 |
|  |  |  |  | `dvalve_nopower-1` | digital: unpowered | 4×1 | S: 256,256<br>N: 288,256<br>E: 320,256<br>W: 352,256 |
| **Gas filter**<br>`/obj/machinery/atmospherics/components/trinary/filter` | 12 | `godot\assets\tg\icons\obj\machines\atmospherics\trinary_devices.png` | 32×32 | `filter_off-0` | off | 4×1 | S: 224,352<br>N: 256,352<br>E: 288,352<br>W: 320,352 |
|  |  |  |  | `filter_on-0` | running | 4×4 | S: 32,416 160,416 288,416 416,416<br>N: 64,416 192,416 320,416 448,416<br>E: 96,416 224,416 352,416 480,416<br>W: 128,416 256,416 384,416 512,416 |
|  |  |  |  | `filter_off-0_f` | flipped: off | 4×1 | S: 128,384<br>N: 160,384<br>E: 192,384<br>W: 224,384 |
|  |  |  |  | `filter_on-0_f` | flipped: running | 4×4 | S: 416,544 544,544 672,544 64,576<br>N: 448,544 576,544 704,544 96,576<br>E: 480,544 608,544 0,576 128,576<br>W: 512,544 640,544 32,576 160,576 |
|  |  |  |  | `filter_on-0-emissive` | glow | 4×4 | S: 96,480 224,480 352,480 480,480<br>N: 128,480 256,480 384,480 512,480<br>E: 160,480 288,480 416,480 544,480<br>W: 192,480 320,480 448,480 576,480 |
| **Gas mixer**<br>`/obj/machinery/atmospherics/components/trinary/mixer` | 3 | `godot\assets\tg\icons\obj\machines\atmospherics\trinary_devices.png` | 32×32 | `mixer_off-0` | off | 4×1 | S: 256,0<br>N: 288,0<br>E: 320,0<br>W: 352,0 |
|  |  |  |  | `mixer_on-0` | running | 4×4 | S: 64,64 192,64 320,64 448,64<br>N: 96,64 224,64 352,64 480,64<br>E: 128,64 256,64 384,64 512,64<br>W: 160,64 288,64 416,64 544,64 |
|  |  |  |  | `mixer_off-0_f` | flipped: off | 4×1 | S: 160,32<br>N: 192,32<br>E: 224,32<br>W: 256,32 |
|  |  |  |  | `mixer_on-0_f` | flipped: running | 4×4 | S: 576,192 704,192 96,224 224,224<br>N: 608,192 0,224 128,224 256,224<br>E: 640,192 32,224 160,224 288,224<br>W: 672,192 64,224 192,224 320,224 |
| **Thermomachine**<br>`/obj/machinery/atmospherics/components/unary/thermomachine` | 7 | `godot\assets\tg\icons\obj\machines\atmospherics\thermomachine.png` | 32×32 | `thermo_base` | off | 1×1 | 0,0 |
|  |  |  |  | `thermo_base_1` | running | 1×10 | 32,0 … 96,32 (10 frames in a row) |
|  |  |  |  | `thermo_base-o` | open panel | 1×1 | 128,32 |
|  |  |  |  | `temp_meter` | gauge (overlay) | 1×1 | 64,64 |
|  |  |  |  | `thermo-emissive` | glow | 1×10 | 0,128 … 64,160 (10 frames in a row) |
| **Space heater**<br>`/obj/machinery/space_heater` | 11 | `godot\assets\tg\icons\obj\pipes_n_cables\atmos.png` | 32×32 | `sheater-off` | off | 1×1 | 32,128 |
|  |  |  |  | `sheater-standby` | idle | 1×1 | 64,128 |
|  |  |  |  | `sheater-heat` | heating | 1×5 | 224,96 256,96 288,96 320,96 0,128 |
|  |  |  |  | `sheater-cool` | cooling | 1×4 | 96,96 128,96 160,96 192,96 |
|  |  |  |  | `sheater-open` | open | 1×1 | 64,160 |

## 3. Construction and destruction (demo item 5)

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Grille**<br>`/obj/structure/grille (+ broken)` | 76 | `godot\assets\tg\icons\obj\structures.png` | 32×32 | `grille` | intact | 1×1 | 480,0 |
|  |  |  |  | `brokengrille` | broken | 1×1 | 512,0 |
| **Girder**<br>`/obj/structure/girder` | 8 | `godot\assets\tg\icons\obj\structures.png` | 32×32 | `girder` | frame | 1×1 | 0,0 |
|  |  |  |  | `displaced` | offset | 1×1 | 64,0 |
|  |  |  |  | `reinforced` | reinforced | 1×1 | 448,0 |
| **Machine frame**<br>`/obj/structure/frame/machine` | 7 | `godot\assets\tg\icons\obj\devices\stock_parts.png` | 32×32 | `box_0` | empty | 1×1 | 0,64 |
|  |  |  |  | `box_1` | wired | 1×1 | 32,64 |
|  |  |  |  | `box_2` | with circuit board | 1×1 | 64,64 |
| **Light frame**<br>`/obj/structure/light_construct (+ small)` | 14 | `godot\assets\tg\icons\obj\lighting.png` | 32×32 | `tube-construct-stage1` | tube: frame | 4×1 | S: 0,96<br>N: 32,96<br>E: 64,96<br>W: 96,96 |
|  |  |  |  | `tube-construct-stage2` | tube: wired | 4×1 | S: 256,96<br>N: 288,96<br>E: 320,96<br>W: 352,96 |
|  |  |  |  | `bulb-construct-stage1` | bulb: frame | 4×1 | S: 128,96<br>N: 160,96<br>E: 192,96<br>W: 224,96 |
|  |  |  |  | `bulb-construct-stage2` | bulb: wired | 4×1 | S: 384,96<br>N: 416,96<br>E: 448,96<br>W: 480,96 |

## 4. Common furniture and machines

| Object | On Meta | Tileset (full path) | Tile | State | What it is | Dirs×frames | Tile coordinates (x,y) |
|---|---:|---|---|---|---|---|---|
| **Solar panel**<br>`/obj/machinery/power/solar (+ tracker)` | 145 | `godot\assets\tg\icons\obj\machines\solar.png` | 32×32 | `sp_base` | base (model exists) | 1×1 | 64,32 |
|  |  |  |  | `solar_panel_glass` | panel | 1×1 | 0,32 |
|  |  |  |  | `solar_panel_glass_edge` | panel edge | 1×1 | 32,32 |
|  |  |  |  | `solar_panel_glass-b` | panel broken | 1×1 | 0,0 |
|  |  |  |  | `tracker_base` | tracker: base | 1×1 | 32,64 |
|  |  |  |  | `tracker` | tracker: panel | 1×1 | 96,32 |
|  |  |  |  | `tracker_edge` | tracker: edge | 1×1 | 0,64 |
| **Weapon recharger**<br>`/obj/machinery/recharger` | 19 | `godot\assets\tg\icons\obj\machines\sec.png` | 32×32 | `recharger` | body | 1×1 | 64,32 |
|  |  |  |  | `recharger-empty` | empty | 1×2 | 96,32 128,32 |
|  |  |  |  | `recharger-charging` | charging | 1×10 | 32,64 … 64,96 (10 frames in a row) |
|  |  |  |  | `recharger-full` | charged | 1×2 | 160,32 192,32 |
|  |  |  |  | `recharger-fail` | error | 1×2 | 224,32 0,64 |
|  |  |  |  | `recharger-open` | open | 1×1 | 96,96 |
| **Cell charger**<br>`/obj/machinery/cell_charger` | 16 | `godot\assets\tg\icons\obj\machines\cell_charger.png` | 32×32 | `ccharger` | body | 1×1 | 0,0 |
|  |  |  |  | `ccharger-standard-on` | charging | 1×1 | 96,32 |
|  |  |  |  | `ccharger-o0` | gauge 0 (o0 … o4) | 1×2 | 0,64 32,64 |
|  |  |  |  | `cell` | cell inside (overlay) | 1×1 | 32,0 |
| **Photocopier**<br>`/obj/machinery/photocopier` | 13 | `godot\assets\tg\icons\obj\service\library.png` | 32×32 | `photocopier` | off | 1×1 | 192,96 |
|  |  |  |  | `photocopier1` | copying | 1×40 | 256,96 … 64,192 (40 frames in a row) |
|  |  |  |  | `photocopier2` | variant | 1×1 | 224,96 |
| **Fax**<br>`/obj/machinery/fax` | 9 | `godot\assets\tg\icons\obj\machines\fax.png` | 32×32 | `fax` | body | 1×2 | 0,0 32,0 |
|  |  |  |  | `fax_send` | sending | 1×19 | 64,0 … 160,32 (19 frames in a row) |
|  |  |  |  | `fax_receive` | receiving | 1×24 | 192,32 … 448,64 (24 frames in a row) |
|  |  |  |  | `fax_panel` | open panel | 1×1 | 0,96 |
| **Filing cabinet**<br>`/obj/structure/filingcabinet (+ chestdrawer)` | 14 | `godot\assets\tg\icons\obj\service\bureaucracy.png` | 32×32 | `filingcabinet` | closed | 1×1 | 352,160 |
|  |  |  |  | `filingcabinet-open` | open | 1×1 | 384,160 |
|  |  |  |  | `chestdrawer` | chest of drawers | 1×1 | 32,192 |
|  |  |  |  | `chestdrawer-open` | chest of drawers, open | 1×1 | 64,192 |
|  |  |  |  | `tallcabinet` | tall | 1×1 | 480,160 |
|  |  |  |  | `tallcabinet-open` | tall, open | 1×1 | 0,192 |
| **Bushes**<br>`/obj/structure/flora/bush/*` | 45 | `godot\assets\tg\icons\obj\fluff\flora\ausflora.png` | 32×32 | `fullgrass_1` | grass (1 … 3) | 1×1 | 160,192 |
|  |  |  |  | `fernybush_1` | fern (1 … 3) | 1×1 | 192,64 |
|  |  |  |  | `brflowers_1` | flowers (br, pp, yw) | 1×1 | 128,160 |
|  |  |  |  | `palebush_1` | pale bush | 1×1 | 96,32 |
|  |  |  |  | `sparsegrass_1` | sparse grass | 1×1 | 64,192 |
|  |  |  |  | `lavendergrass_1` | lavender | 1×1 | 128,128 |
| **Morgue**<br>`/obj/structure/bodycontainer/morgue` | 12 | `godot\assets\tg\icons\obj\structures.png` | 32×32 | `morgue1` | closed, empty | 4×1 | S: 224,352<br>N: 256,352<br>E: 288,352<br>W: 320,352 |
|  |  |  |  | `morgue2` | with body | 4×1 | S: 352,352<br>N: 384,352<br>E: 416,352<br>W: 448,352 |
|  |  |  |  | `morgue3` | with body, soul present | 4×1 | S: 480,352<br>N: 512,352<br>E: 544,352<br>W: 0,384 |
|  |  |  |  | `morgue4` | with body, cloneable | 4×1 | S: 32,384<br>N: 64,384<br>E: 96,384<br>W: 128,384 |
|  |  |  |  | `morgue0` | open | 4×1 | S: 96,352<br>N: 128,352<br>E: 160,352<br>W: 192,352 |
|  |  |  |  | `morguet` | tray | 4×1 | S: 160,384<br>N: 192,384<br>E: 224,384<br>W: 256,384 |
|  |  |  |  | `morgue_label` | tag | 1×1 | 288,384 |
| **AI turret**<br>`/obj/machinery/porta_turret/ai` | 11 | `godot\assets\tg\icons\obj\weapons\turrets.png` | 32×32 | `turretCover` | closed | 1×1 | 448,0 |
|  |  |  |  | `openTurretCover` | open | 1×1 | 480,0 |
|  |  |  |  | `popup` | rising | 1×6 | 256,0 288,0 320,0 352,0 384,0 416,0 |
|  |  |  |  | `popdown` | retracting | 1×6 | 512,0 0,32 32,32 64,32 96,32 128,32 |
|  |  |  |  | `standard_stun` | stun | 8×1 | S: 416,32<br>N: 448,32<br>E: 480,32<br>W: 512,32<br>SE: 0,64<br>SW: 32,64<br>NE: 64,64<br>NW: 96,64 |
|  |  |  |  | `standard_lethal` | lethal | 8×1 | S: 128,64<br>N: 160,64<br>E: 192,64<br>W: 224,64<br>SE: 256,64<br>SW: 288,64<br>NE: 320,64<br>NW: 352,64 |
|  |  |  |  | `standard_off` | off | 8×1 | S: 0,0<br>N: 32,0<br>E: 64,0<br>W: 96,0<br>SE: 128,0<br>SW: 160,0<br>NE: 192,0<br>NW: 224,0 |
|  |  |  |  | `standard_broken` | broken | 8×1 | S: 160,32<br>N: 192,32<br>E: 224,32<br>W: 256,32<br>SE: 288,32<br>SW: 320,32<br>NE: 352,32<br>NW: 384,32 |
| **AI foam dispenser**<br>`/obj/machinery/ai_slipper` | 12 | `godot\assets\tg\icons\obj\devices\tool.png` | 32×32 | `ai-slipper0` | ready | 1×1 | 288,224 |
|  |  |  |  | `ai-slipper1` | recharging | 1×6 | 0,256 32,256 64,256 96,256 128,256 160,256 |
| **Microwave**<br>`/obj/machinery/microwave` | 11 | `godot\assets\tg\icons\obj\machines\microwave.png` | 32×32 | `mw` | closed | 1×1 | 32,0 |
|  |  |  |  | `mwo` | open | 1×2 | 64,0 96,0 |
|  |  |  |  | `mw1` | running | 1×4 | 128,0 160,0 192,0 224,0 |
|  |  |  |  | `mwb` | broken | 1×1 | 0,32 |
|  |  |  |  | `mwbloody` | dirty | 1×1 | 32,32 |
|  |  |  |  | `mw_complete` | map icon | 1×1 | 0,0 |
| **Reagent grinder**<br>`/obj/machinery/reagentgrinder` | 9 | `godot\assets\tg\icons\obj\machines\kitchen.png` | 32×32 | `juicer` | empty | 1×1 | 32,128 |
|  |  |  |  | `juicer-beaker` | with beaker | 1×1 | 96,128 |
|  |  |  |  | `juicer-on` | running | 1×1 | 64,128 |
| **IV drip**<br>`/obj/machinery/iv_drip` | 11 | `godot\assets\tg\icons\obj\medical\iv_drip.png` | 32×32 | `iv_drip` | empty | 1×1 | 0,0 |
|  |  |  |  | `iv_drip_injecting` | injecting | 1×4 | 32,0 64,0 96,0 128,0 |
|  |  |  |  | `iv_drip_donating` | drawing | 1×4 | 160,0 0,32 32,32 64,32 |
|  |  |  |  | `beakeridle` | beaker (overlay) | 1×1 | 160,32 |
|  |  |  |  | `reagent50` | level (overlay; 0 … 90) | 1×1 | 160,64 |
| **Recharge station**<br>`/obj/machinery/recharge_station` | 10 | `godot\assets\tg\icons\obj\machines\borg_charger.png` | 32×32 | `borgcharger0` | empty | 1×1 | 0,32 |
|  |  |  |  | `borgcharger1` | occupied | 1×4 | 0,0 32,0 64,0 96,0 |
|  |  |  |  | `borgcharger2` | charging | 1×1 | 32,32 |
|  |  |  |  | `borgcharger-u0` | open, empty | 1×1 | 32,64 |
|  |  |  |  | `borgcharger-u1` | open, occupied | 1×1 | 64,64 |
| **Delivery chute**<br>`/obj/machinery/disposal/delivery_chute` | 11 | `godot\assets\tg\icons\obj\pipes_n_cables\disposal.png` | 32×32 | `intake` | receiving | 4×1 | S: 160,96<br>N: 192,96<br>E: 224,96<br>W: 256,96 |
|  |  |  |  | `intake-flush` | sending | 4×8 | S: 288,96 416,96 0,128 128,128 256,128 384,128 512,128 96,160<br>N: 320,96 448,96 32,128 160,128 288,128 416,128 0,160 128,160<br>E: 352,96 480,96 64,128 192,128 320,128 448,128 32,160 160,160<br>W: 384,96 512,96 96,128 224,128 352,128 480,128 64,160 192,160 |
