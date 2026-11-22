"""The wave models' catalog specs (catalog_waves.py): which tg icon and states each model draws and what each of its clips means in tg.

A clip maps to 's:<icon state>' / 'o:<overlay state>' (the nodes it shows are drawn while tg shows that; a list for several), 'open <cond>|<cond>' (its moving
nodes swing or slide while one of the conds holds: the entry's open channel), 'hide <cond>|<cond>' (a second mover, a maintenance hatch: its nodes are not
drawn while the cond holds, which shows tg's open panel on the body behind it) or None (not used: the nodes it shows stay hidden).
"""

ATMOS = 'icons/obj/pipes_n_cables/atmos.dmi'

SPECS = [
    # Atmospheric machines (atmosphere_wave/README.md).
    {'id': 'portable_air_pump', 'glb': 'machines/atmosphere_wave/portable_air_pump.glb', 'awayFromWall': True,
     'icons': [ATMOS], 'states': ['siphon_0', 'siphon_1'],  # pump.dm: "[initial(icon_state)]_[on]"
     'clips': {'siphon_0': 's:siphon_0', 'siphon_1': 's:siphon_1'}},
    # The improvised heater (spaceheater.dm) always shows sheater-rigged; it is tried before the plain heater, which shares its states.
    {'id': 'improvised_chemical_heater', 'glb': 'machines/atmosphere_wave/improvised_chemical_heater.glb', 'family': 'space_heater', 'awayFromWall': True,
     'icons': [ATMOS], 'states': ['sheater-off', 'sheater-heat', 'sheater-cool'], 'overlays': ['sheater-rigged'],
     'clips': {'sheater-off': 's:sheater-off', 'sheater-heat': 's:sheater-heat', 'sheater-cool': 's:sheater-cool', 'sheater-standby': None,
               'maintenance_open': 'open o:sheater-open', 'maintenance_close': None}},
    {'id': 'space_heater', 'glb': 'machines/atmosphere_wave/space_heater.glb', 'family': 'space_heater', 'awayFromWall': True,
     'icons': [ATMOS], 'states': ['sheater-off', 'sheater-heat', 'sheater-cool', 'sheater-standby'],  # "[base_icon_state]-[on ? mode : "off"]"
     'clips': {'sheater-off': 's:sheater-off', 'sheater-heat': 's:sheater-heat', 'sheater-cool': 's:sheater-cool', 'sheater-standby': 's:sheater-standby',
               'maintenance_open': 'open o:sheater-open', 'maintenance_close': None}},
    {'id': 'electrolyzer', 'glb': 'machines/atmosphere_wave/electrolyzer.glb', 'awayFromWall': True,
     'icons': [ATMOS], 'states': ['electrolyzer-off', 'electrolyzer-standby', 'electrolyzer-working'],  # "electrolyzer-[on ? mode : "off"]"
     'clips': {'electrolyzer-off': 's:electrolyzer-off', 'electrolyzer-standby': 's:electrolyzer-standby', 'electrolyzer-working': 's:electrolyzer-working',
               'maintenance_open': 'open o:electrolyzer-open', 'maintenance_close': None}},
    # crystallizer.dm: a binary pipe component (its dir joins its pipes); its panel open is its own state, -open
    {'id': 'gas_crystallizer', 'glb': 'machines/atmosphere_wave/gas_crystallizer.glb', 'rotate': False,
     'icons': ['icons/obj/machines/atmospherics/machines.dmi'], 'states': ['crystallizer-off', 'crystallizer-on', 'crystallizer-open'],
     'clips': {'crystallizer-off': ['s:crystallizer-off', 's:crystallizer-open'], 'crystallizer-on': 's:crystallizer-on',
               'maintenance_open': 'hide s:crystallizer-open', 'maintenance_close': None}},
    # tank_compressor.dm: the chamber is shut (-closed) while a tank is in; its panel shows -maintenance
    {'id': 'tank_compressor', 'glb': 'machines/atmosphere_wave/tank_compressor.glb', 'rotate': False,
     'icons': ['icons/obj/machines/research.dmi'], 'states': ['tank_compressor-open', 'tank_compressor-closed'],
     'clips': {'open': 'open s:tank_compressor-open', 'close': None, 'maintenance_open': 'hide o:tank_compressor-maintenance', 'maintenance_close': None}},
    # shieldgen.dm: "shield[active ? "on" : "off"][(machine_stat & BROKEN) ? "br" : null]"
    # its column's uv reach the sprite's transparent pixels (ring tops, the base): drawn opaque, as Blender shows it (the owner, 2026-11-08)
    {'id': 'breach_shield_generator', 'glb': 'machines/atmosphere_wave/breach_shield_generator.glb', 'awayFromWall': True, 'part': {'opaque': True},
     'icons': ['icons/obj/machines/shield_generator.dmi'], 'states': ['shieldoff', 'shieldon', 'shieldoffbr', 'shieldonbr'],
     'clips': {'shieldoff': 's:shieldoff', 'shieldon': 's:shieldon', 'shieldoffbr': 's:shieldoffbr', 'shieldonbr': 's:shieldonbr'}},
    # atmos_field_gen.dm: powered and on, it shows active or wantpower; its dir is the field's
    {'id': 'atmospheric_shield_generator', 'glb': 'machines/atmosphere_wave/atmospheric_shield_generator.glb', 'rotate': False,
     'icons': ['icons/obj/machines/atmosshieldgen.dmi'], 'states': ['atmosshield'],
     'clips': {'active': 'o:active', 'wantpower': 'o:wantpower', 'inactive': None}},
]


def telecomms(id, glb, base, on_clip, off_clip, **more):
    """telecomunications.dm: "[initial(icon_state)][panel_open ? "_o" : null][on ? null : "_off"]"; the open panel swings the service hatch."""
    s = {'id': id, 'glb': 'machines/communications_wave/' + glb, 'awayFromWall': True, 'icons': [TELECOMMS],
         'states': [base, base + '_o', base + '_off', base + '_o_off'],
         'clips': {on_clip: ['s:' + base, 's:' + base + '_o'], off_clip: ['s:' + base + '_off', 's:' + base + '_o_off'],
                   'maintenance_open': f'open s:{base}_o|s:{base}_o_off', 'maintenance_close': None}}
    s.update(more)
    return s


TELECOMMS = 'icons/obj/machines/telecomms.dmi'
SPECS += [
    # Servers and communications (communications_wave/README.md).
    telecomms('telecomms_hub', 'telecomms_hub.glb', 'hub', 'hub', 'hub_off'),
    telecomms('telecomms_processor', 'telecomms_processor.glb', 'processor', 'processor', 'processor_off'),
    telecomms('telecomms_bus', 'telecomms_bus_ntnet.glb', 'bus', 'bus', 'bus_off', family='telecomms_bus'),
    telecomms('telecomms_server', 'telecomms_server.glb', 'comm_server', 'comm_server', 'comm_server_off'),
    telecomms('telecomms_receiver', 'telecomms_dish.glb', 'broadcast receiver', 'receiver', 'off', family='telecomms_dish'),
    dict(telecomms('telecomms_broadcaster', 'telecomms_dish.glb', 'broadcaster', 'transmitter', 'off', family='telecomms_dish'), clips={
        'transmitter': ['s:broadcaster', 's:broadcaster_o'], 'receiver': None, 'off': None,
        'maintenance_open': 'open s:broadcaster_o|s:broadcaster_o_off', 'maintenance_close': None}),
    # relays.dm: the NTNet relay shows bus or bus_off, which telecomms_bus draws
    # server.dm: "[base_icon_state]-off" unpowered, "-on_t" with its panel open, else -on or -halt
    {'id': 'research_server', 'glb': 'machines/communications_wave/research_server.glb', 'awayFromWall': True,
     'icons': ['icons/obj/machines/research.dmi'], 'states': ['RD-server-on', 'RD-server-halt', 'RD-server-off', 'RD-server-on_t'],
     'clips': {'RD-server-on': ['s:RD-server-on', 's:RD-server-on_t'], 'RD-server-halt': 's:RD-server-halt', 'RD-server-off': 's:RD-server-off',
               'maintenance_open': 'open s:RD-server-on_t', 'maintenance_close': None}},
    # announcement_system.dm: "AAS_[On|Off][_Open]" and its three lights as overlays
    {'id': 'announcement_system', 'glb': 'machines/communications_wave/announcement_system.glb', 'awayFromWall': True,
     'icons': [TELECOMMS], 'states': ['AAS_On', 'AAS_Off', 'AAS_On_Open', 'AAS_Off_Open'],
     'clips': {'AAS_On': ['s:AAS_On', 's:AAS_On_Open'], 'AAS_Off': ['s:AAS_Off', 's:AAS_Off_Open'],
               'Light_Green': 'o:Light_Green', 'Light_Pink': 'o:Light_Pink', 'Error_Red': 'o:Error_Red',
               'Light_Green_off': None, 'Light_Pink_off': None, 'Error_Red_off': None,
               'maintenance_open': 'open s:AAS_On_Open|s:AAS_Off_Open', 'maintenance_close': None}},
    # message_server.dm: "blackbox[stored ? null : "_b"]"; the reels turn while the box records
    {'id': 'blackbox_recorder', 'glb': 'machines/communications_wave/blackbox_recorder.glb', 'awayFromWall': True,
     'icons': [TELECOMMS], 'states': ['blackbox', 'blackbox_b'],
     'clips': {'recording': 's:blackbox', 'empty': None},
     'always': ['Tape_Reel_5', 'Tape_Reel_18'],
     'spin': {'Tape_Reel_5': ([0, 0, -1], 180), 'Tape_Reel_18': ([0, 0, -1], 180)},
     'parts': {'Tape_Reel_5': {'states': ['blackbox']}, 'Tape_Reel_18': {'states': ['blackbox']}},
     'extra': {'Tape_Reel_5_still': {'node': 'Tape_Reel_5', 'states': ['blackbox_b']}, 'Tape_Reel_18_still': {'node': 'Tape_Reel_18', 'states': ['blackbox_b']}}},
]


def engineering(id, glb, icon, states, **more):
    s = {'id': id, 'glb': 'machines/engineering_wave/' + glb, 'icons': [icon], 'states': states}
    s.update(more)
    return s


def turbine(id, glb, state, base):
    """turbine.dm: the part's overlays [base]_on while it runs (the rotor turns) and [base]_open with its panel open; its dir lines up the assembly."""
    rotor = {'pivot': True, 'axis': [0, 0, -1], 'spin': 360, 'overlays': [base + '_on']}
    blades = ('Axial_Hub', 'Pitched_Blades')  # the Rotor node's meshes
    return engineering(id, glb, 'icons/obj/machines/engine/turbine.dmi', [state], rotate=False, family='gas_turbine',
                       clips={'running': None, 'stopped': None, 'maintenance_open': f'open o:{base}_open', 'maintenance_close': None},
                       always=['Core_Running_Indicator'],  # the core's lamp sits on its hatch: it is not drawn with the hatch open
                       parts=dict({n: rotor for n in blades}, Core_Running_Indicator={'overlays': [base + '_on'], 'exceptOverlays': [base + '_open']}),
                       extra={n + '_still': {'node': n, 'exceptOverlays': [base + '_on']} for n in blades})


def reflector(id, glb, overlay, head):
    """reflector.dm: the base stands; the deflector (the Rotating_Head node's meshes) is an overlay tg turns by its rotation_angle (a transform), as the sun rule's heads."""
    return engineering(id, glb, 'icons/obj/structures.dmi', ['reflector_base'], rule='sun', azimuth=overlay, overlays=[overlay], family='reflector',
                       clips={'rotate_90': None}, names={n: 'head_' + n for n in head}, parts={n: {'turn': 180} for n in head})


def frames(nodes, seconds, **part):
    """Meshes of different shapes that play as one state's frames: each blinks in its turn."""
    return {n: dict(part, blink=[seconds, round(seconds * (len(nodes) - 1), 2), round(seconds * i, 2)]) for i, n in enumerate(nodes)}


COIL = 'icons/obj/machines/engine/tesla_coil.dmi'


def discharge(id, glb, state, count):
    """coil.dm: flick("[base_icon_state]hit"): an entry per frame of the flick, with that frame's bolt (their shapes differ, so a live picture cannot play them)."""
    return [engineering(f'{id}_{n}', glb, COIL, [state], flickFrame=n, always=[f'Discharge_{n}']) for n in range(count)]


SPECS += [
    # Engineering (engineering_wave).
    # coil.dm: "[base_icon_state][panel_open ? "_open" : ""][anchored]"
    engineering('tesla_coil', 'tesla_coil.glb', COIL, ['coil0', 'coil1', 'coil_open0', 'coil_open1'],
                clips={'maintenance_open': 'open s:coil_open0|s:coil_open1', 'maintenance_close': None}),
    *discharge('tesla_coil_hit', 'tesla_coil.glb', 'coilhit', 6),
    # its open panel takes the insulator out and shows the conductor
    engineering('grounding_rod', 'grounding_rod.glb', COIL, ['grounding_rod0', 'grounding_rod1', 'grounding_rod_open0', 'grounding_rod_open1'],
                clips={'maintenance_open': ['s:grounding_rod_open0', 's:grounding_rod_open1'], 'maintenance_close': ['s:grounding_rod0', 's:grounding_rod1']}),
    *discharge('grounding_rod_hit', 'grounding_rod.glb', 'grounding_rodhit', 5),
    # field_generator.dm: +a1..3 while warming up, +on with its fields up, +p1..6 by its power
    engineering('containment_field_generator', 'containment_field_generator.glb', 'icons/obj/machines/field_generator.dmi', ['Field_Gen'],
                clips=dict({f'warmup_{n}': f'o:+a{n}' for n in (1, 2, 3)}, **{f'charge_{n}': f'o:+p{n}' for n in range(1, 7)},
                           powered=['o:+on', 'o:+a1', 'o:+a2', 'o:+a3'], unpowered=['!o:+on', '!o:+a1', '!o:+a2', '!o:+a3'])),
    # shieldgen.dm: "[base_icon_state][active ? "_on" : ""]", shieldgen_wires with its panel open; the arcs are the 8 frames of _on
    engineering('shield_wall_generator', 'shield_wall_generator.glb', 'icons/obj/machines/shield_generator.dmi', ['shield_wall_gen', 'shield_wall_gen_on'],
                clips={'powered': 's:shield_wall_gen_on', 'unpowered': 's:shield_wall_gen', 'maintenance_open': 'open o:shieldgen_wires', 'maintenance_close': None},
                parts=frames([f'Powered_Arc_{n}' for n in range(8)], 0.1)),
    # emitter.dm: emitter or emitter_open; emitter_overlay while it is on, lime or (low power) orange: the lights take tg's colour, so the model's orange set is not used
    engineering('emitter', 'emitter.glb', 'icons/obj/machines/engine/singularity.dmi', ['emitter', 'emitter_open'],
                clips={'maintenance_open': 'open s:emitter_open', 'maintenance_close': None}, always=['Powered_Lights'],
                # the uv of these three stray past the sprite's edge: the nearest solid colour there, not a hole (the owner, 2026-11-08)
                parts=dict({n: {'opaque': True, 'fill': True} for n in ('Emitter_Flat_Head', 'Emitter_Stand_And_Brace', 'Rear_Brace')},
                           Powered_Lights={'overlays': ['emitter_overlay'], 'tintOverlay': 'emitter_overlay'})),
    # mech_bay.dm: panel_open ? "[base_icon_state]-o" : base_icon_state
    engineering('mech_bay_power_port', 'mech_bay_power_port.glb', 'icons/obj/machines/mech_bay.dmi', ['recharge_port', 'recharge_port-o'],
                clips={'maintenance_open': 'open s:recharge_port-o', 'maintenance_close': None}),
    # rechargestation.dm: "[base_icon_state][state_open ? 0 : (occupant ? 1 : 2)]", unpowered -u0/-u1, borgdecon2 while it takes a borg apart;
    # the shutter is let down while it is shut
    engineering('cyborg_recharge_station', 'cyborg_recharge_station.glb', 'icons/obj/machines/borg_charger.dmi',
                ['borgcharger0', 'borgcharger1', 'borgcharger2', 'borgcharger-u0', 'borgcharger-u1', 'borgdecon2'], awayFromWall=True,
                clips={'open': 's:borgcharger0', 'close': 's:borgcharger2', 'charging': 's:borgcharger1', 'unpowered': ['s:borgcharger-u0', 's:borgcharger-u1'],
                       'maintenance': 's:borgdecon2'},
                # borgcharger1's other frames play on Status_Occupied's live picture; the shutter's own lights hang from its rolled-up scale of 0.025
                skip=['Charger_Solid_Shutter'] + [f'Status_Occupied_Frame{n}{s}' for n in (1, 2, 3) for s in ('', '_Shutter')],
                parts={n: {'nodeScale': [1, 40, 1]} for n in ('Status_Closed_Shutter', 'Status_Occupied_Shutter')},
                extra={'Charger_Solid_Shutter': {'node': 'Charger_Solid_Shutter', 'nodeScale': [1, 1, 1], 'states': ['borgcharger1', 'borgcharger2', 'borgcharger-u1']}}),
    # rtg.dm: panel_open ? "[base_icon_state]-open" : base_icon_state
    engineering('radioisotope_thermoelectric_generator', 'radioisotope_thermoelectric_generator.glb', 'icons/obj/machines/engine/other.dmi', ['rtg', 'rtg-open'],
                clips={'Service open': 'open s:rtg-open'}),
    turbine('gas_turbine_inlet', 'gas_turbine_inlet.glb', 'inlet_compressor', 'inlet'),
    turbine('gas_turbine_core', 'gas_turbine_core.glb', 'core_rotor', 'core'),
    turbine('gas_turbine_outlet', 'gas_turbine_outlet.glb', 'turbine_outlet', 'outlet'),
    reflector('reflector_single', 'beam_reflector_single.glb', 'reflector', ['Optical_Cassette', 'Rear_Service_Panel', 'Rotor_Hub']),
    reflector('reflector_double', 'beam_reflector_double.glb', 'reflector_double', ['Optical_Cassette', 'Rotor_Hub']),
    reflector('reflector_box', 'beam_reflector_box.glb', 'reflector_box', ['Beam_Port', 'Diamond_Optical_Housing', 'Optical_Window', 'Rotor_Hub']),
]


def medical(id, glb, icon, states, **more):
    s = {'id': id, 'glb': 'machines/medical_wave/' + glb, 'icons': [icon], 'states': states}
    s.update(more)
    return s


CHEMICAL = 'icons/obj/medical/chemical.dmi'
CLONING = 'icons/obj/machines/cloning.dmi'
SMARTFRIDGE = 'icons/obj/machines/smartfridge.dmi'
FRIDGE_CONTENTS = [f'smartfridge-{kind}-{n}' for kind in ('chem', 'food', 'plant', 'slime', 'drink', 'organ', 'petri', 'viro') for n in (1, 2, 3)]


def fills(node, overlay, levels):
    """tg's fill overlays: a mesh per level, drawn with its overlay in the overlay's colour (the reagents')."""
    return {f'{node}{n}': {'overlays': [f'{overlay}{n}'], 'tintOverlay': f'{overlay}{n}'} for n in levels}


IV_FILLS = (0, 10, 25, 50, 75, 80, 90)
SEPARATOR_FILLS = dict(fills('separator_m_', 'separator_m_', (1, 30, 80)), **fills('separator_b_', 'separator_b_', (1, 30, 80)))
SEPARATOR_DRIPPING = frames([f'separator_dripping_frame_{n}' for n in range(8)], 0.1, overlays=['separator_dripping'], tintOverlay='separator_dripping')


def chem_master(id, state):
    """chem_master.dm: the screen overlay by what it does (none with its panel open or unpowered), _overlay_panel with its panel open."""
    return medical(id, 'chem_master.glb' if state == 'chemmaster' else 'condiment_master.glb', CHEMICAL, [state], awayFromWall=True, overlayFrames=True,
                   clips={'idle': 'o:chemmaster_overlay_screen', 'loaded': 'o:chemmaster_overlay_screen_main', 'printing': 'o:chemmaster_overlay_screen_active',
                          'unpowered': None, 'maintenance_open': 'open o:chemmaster_overlay_panel', 'maintenance_close': None})


def body_container(id, glb, icon, states, open_state, tray_id, tray_icon, tray_state, clips):
    """morgue.dm: open, the tray is its own thing on the tile before it (connected, turned as the container): the container's tray is
    drawn only while it is shut, and the tray thing draws the same tray where the container's would have slid to (1.45 m of the 2 m tile)."""
    shut = {'exceptStates': [open_state]}
    return [medical(id, glb, icon, states, rotate=False, clips=dict(clips, open=None, close=None),
                    parts={'Sliding_Tray': shut, 'Optional_Name_Label': shut}),
            medical(tray_id, glb, tray_icon, [tray_state], rotate=False, rest='top', clips={'open': None, 'close': None},
                    skip=['Chamber_Rear_Panel', 'Chamber_Side_Panel', 'Low_Body_Container'] + ['State_' + s for s in states if s != open_state],
                    parts={'Sliding_Tray': {'offset': [0, 0, -0.55]}})]


SPECS += [
    # Medical (medical_wave/README.md).
    # iv_drip.dm: "[base_icon_state]_[mode ? "injecting" : "donating"]" while attached, else _injectidle / _donateidle
    # its container: beakeractive with a patient, else beakeridle, and reagent[0-90] in the reagents' colour
    medical('iv_drip', 'iv_drip.glb', 'icons/obj/medical/iv_drip.dmi', ['iv_drip_injectidle', 'iv_drip_donateidle', 'iv_drip_injecting', 'iv_drip_donating'],
            clips={s: 's:' + s for s in ['iv_drip_injectidle', 'iv_drip_donateidle', 'iv_drip_injecting', 'iv_drip_donating']},
            skip=[f'iv_drip_{s}_frame_{n}' for s in ('injecting', 'donating') for n in (1, 2, 3)],
            always=[f'Container_reagent{n}' for n in IV_FILLS],
            parts=dict(fills('Container_reagent', 'reagent', IV_FILLS), IV_Reservoir={'overlays': ['beakeractive', 'beakeridle']},
                       Drip_Line_active={'overlays': ['beakeractive']}, Drip_Line_idle={'overlays': ['beakeridle']})),
    # cryo.dm: pod-open, pod-on, pod-off; pod-panel with its panel open; a unary pipe device (its dir is its port)
    medical('cryo_cell', 'cryo_cell.glb', 'icons/obj/medical/cryogenics.dmi', ['pod-off', 'pod-on', 'pod-open'], rotate=False,
            clips={'open': 'open s:pod-open', 'close': None, 'cooling': 's:pod-on', 'off': None}),  # the revised model has no maintenance hatch
    # dna_scanner.dm: scanner[_open][_unpowered|_maintenance], scanner_occupied
    medical('dna_scanner', 'dna_scanner.glb', CLONING, ['scanner', 'scanner_open', 'scanner_occupied', 'scanner_unpowered', 'scanner_open_unpowered',
                                                         'scanner_maintenance', 'scanner_open_maintenance'], awayFromWall=True,
            clips={'scanner': ['s:scanner', 's:scanner_open'], 'scanner_occupied': 's:scanner_occupied',
                   'scanner_unpowered': ['s:scanner_unpowered', 's:scanner_open_unpowered'], 'scanner_maintenance': ['s:scanner_maintenance', 's:scanner_open_maintenance'],
                   'open': 'open s:scanner_open|s:scanner_open_unpowered|s:scanner_open_maintenance', 'close': None}),
    # dna_infuser.dm: infuser (unpowered), infuser_panel, infuser_on (infusing), infuser[_open]
    medical('dna_infuser', 'dna_infuser.glb', CLONING, ['infuser', 'infuser_open', 'infuser_on', 'infuser_panel'], awayFromWall=True,
            clips={'infuser': ['s:infuser', 's:infuser_open'], 'infuser_on': 's:infuser_on', 'infuser_panel': 's:infuser_panel', 'open': 'open s:infuser_open', 'close': None}),
    # brain_computer_interface.dm: bci_implanter[_open], _occupied or its busy states; the canopy halves are drawn shut while it is shut
    medical('bci_implanter', 'bci_implanter.glb', 'icons/obj/machines/bci_implanter.dmi',
            ['bci_implanter', 'bci_implanter_open', 'bci_implanter_occupied', 'bci_implanter_raising', 'bci_implanter_active', 'bci_implanter_falling'], awayFromWall=True,
            clips={'open': None, 'close': None}, parts={f'BCI_{side}_Canopy': {'exceptStates': ['bci_implanter_open']} for side in ('Left', 'Right')}),
    # chem_heater.dm: "[base_icon_state][(beaker && !panel_open) ? 1 : 0]b"
    medical('chem_heater', 'reaction_chamber.glb', CHEMICAL, ['mixer0b', 'mixer1b'], awayFromWall=True, clips={'open': None, 'close': None}),
    # chem_mass_spec.dm: HPLC_on while it can work, else HPLC; its graph overlays; HPLC_panel-o with its panel open
    medical('hplc', 'hplc_chromatograph.glb', CHEMICAL, ['HPLC', 'HPLC_on'], awayFromWall=True, overlayFrames=True,
            clips={'analysing': 'o:HPLC_graph_active', 'idle': 'o:HPLC_graph_idle', 'off': None, 'HPLC': 's:HPLC', 'HPLC_on': 's:HPLC_on',
                   'maintenance_open': 'open o:HPLC_panel-o', 'maintenance_close': None}),
    # medical_kiosk.dm: kiosk_open with its panel open, kiosk_off unpowered, kiosk[_active]
    medical('medical_kiosk', 'medical_kiosk.glb', 'icons/obj/machines/medical_kiosk.dmi', ['kiosk', 'kiosk_active', 'kiosk_off', 'kiosk_open'], awayFromWall=True,
            clips={'kiosk_off': 's:kiosk_off', 'kiosk': 's:kiosk', 'kiosk_active': 's:kiosk_active', 'maintenance_open': 'open s:kiosk_open', 'maintenance_close': None}),
    # vatgrower.dm: [icon_state]_on or _off, its _reagent always
    medical('growing_vat', 'biological_growing_vat.glb', 'icons/obj/science/vatgrowing.dmi', ['growing_vat'], awayFromWall=True, overlayFrames=True,
            clips={'filled': 'o:growing_vat_reagent', 'empty': None, 'growing_vat_off': 'o:growing_vat_off', 'growing_vat_on': 'o:growing_vat_on',
                   'bubbling': 'o:growing_vat_bubbles'},
            parts={'Reagent_Surface': {'tintOverlay': 'growing_vat_reagent'}}),
    # synthesizer.dm: a plumbing machine (its ducts join by its dir)
    medical('chemical_synthesizer', 'chemical_synthesizer.glb', 'icons/obj/pipes_n_cables/hydrochem/plumbers.dmi', ['synthesizer'], rotate=False,
            parts={'Tintable_Reagent_Band': {'tintOverlay': 'synthesizer_overlay'}}),
    medical('operating_table', 'operating_table.glb', 'icons/obj/medical/surgery_table.dmi', ['surgery_table'], rest='top'),
    medical('microscope', 'microscope.glb', 'icons/obj/science/vatgrowing.dmi', ['microscope']),
    # chem_separator.dm: separator_burn with the burner lit, separator_m_* in the flask, separator_dripping while it boils, separator_beaker and its
    # separator_b_*, separator_temp_* on the thermometer
    medical('chem_separator', 'manual_chemical_separator.glb', CHEMICAL, ['separator'], overlayFrames=True,
            skip=[f'separator_burn_frame_{n}' for n in (1, 2, 3)],
            always=['Receiving_Beaker', 'separator_burn_frame_0', 'separator_temp_50', 'separator_temp_100', *SEPARATOR_FILLS, *SEPARATOR_DRIPPING],
            parts=dict(SEPARATOR_FILLS, **SEPARATOR_DRIPPING, **{f'separator_temp_{n}': {'overlays': [f'separator_temp_{n}']} for n in (0, 50, 100)},
                       Falling_Drop_6=SEPARATOR_DRIPPING['separator_dripping_frame_6'], Falling_Drop_7=SEPARATOR_DRIPPING['separator_dripping_frame_7'],
                       Receiving_Beaker={'overlays': ['separator_beaker']}, separator_burn_frame_0={'overlays': ['separator_burn']})),
    chem_master('chem_master', 'chemmaster'),
    chem_master('condi_master', 'condimaster'),
    # chem_dispenser.dm: its nopower_state unpowered, its panel overlay, its working flick (drawn by the live screen)
    medical('chem_dispenser', 'chemical_dispenser.glb', CHEMICAL, ['dispenser', 'dispenser_nopower'], awayFromWall=True,
            clips={'dispenser': 's:dispenser', 'dispenser_nopower': 's:dispenser_nopower', 'dispense': None,
                   'maintenance_open': 'open o:dispenser_panel-o', 'maintenance_close': None}),
    medical('soda_dispenser', 'soda_dispenser.glb', CHEMICAL, ['soda_dispenser'], awayFromWall=True),
    medical('booze_dispenser', 'alcohol_dispenser.glb', CHEMICAL, ['booze_dispenser'], awayFromWall=True),
    # smartfridge.dm: [base]-broken; its contents overlay by kind and level, -glass[-broken], -panel with its panel open
    # (smartfridge-icon is the map's picture: the port's fridge has no procs yet, so it keeps it)
    medical('smartfridge', 'smart_fridge.glb', SMARTFRIDGE, ['smartfridge', 'smartfridge-broken', 'smartfridge-icon'], awayFromWall=True,
            clips=dict({s: 'o:' + s for s in FRIDGE_CONTENTS}, empty=None, open=None, close=None, broken=None, intact=None,
                       maintenance_open='open o:smartfridge-panel', maintenance_close=None),
            parts={'Glass_Door': {'exceptOverlays': ['smartfridge-glass-broken']}}),
    # smartfridge.dm (drying): dehydrator-on / -off overlays, its glass as the fridge's
    medical('dehydrator', 'dehydrator.glb', SMARTFRIDGE, ['dehydrator', 'dehydrator-broken', 'dehydrator-icon'], awayFromWall=True,
            clips={'dehydrator-off': 'o:dehydrator-off', 'dehydrator-on': 'o:dehydrator-on', 'open': None, 'close': None, 'broken': None, 'intact': None},
            parts={'Glass_Door': {'exceptOverlays': ['dehydrator-glass-broken']}}),
    *body_container('morgue', 'morgue_section.glb', 'icons/obj/structures.dmi', ['morgue0', 'morgue1', 'morgue2', 'morgue3', 'morgue4'], 'morgue0',
                    'morgue_tray', 'icons/obj/structures.dmi', 'morguet',
                    dict({f'morgue{n}': f's:morgue{n}' for n in (1, 2, 3, 4)}, label_on='o:morgue_label', label_off=None)),
    *body_container('crematorium', 'crematorium.glb', 'icons/obj/machines/crematorium.dmi', ['crema0', 'crema1', 'crema2', 'crema_active'], 'crema0',
                    'crematorium_tray', 'icons/obj/machines/crematorium.dmi', 'cremat', {s: 's:' + s for s in ('crema1', 'crema2', 'crema_active')}),
]


def wave(folder):
    def make(id, glb, icon, states, **more):
        s = {'id': id, 'glb': f'machines/{folder}/{glb}', 'icons': [icon], 'states': states}
        s.update(more)
        return s
    return make


production = wave('production_wave')
resources = wave('resources_wave')
RESEARCH = 'icons/obj/machines/research.dmi'
MINING = 'icons/obj/machines/mining_machines.dmi'


def rnd_production(id, glb, base, busy, stripe=False):
    """_production.dm: busy, its production_animation state; [base]_t with its panel open; a department's stripe overlay (protolathe_stripe[_t])."""
    clips = {'working': None, 'idle': None, 'maintenance_open': f'open s:{base}_t', 'maintenance_close': None}
    if stripe:
        clips.update(department_stripe=['o:protolathe_stripe', 'o:protolathe_stripe_t'], no_stripe=None)
    return production(id, glb, RESEARCH, [base, base + '_t', busy], awayFromWall=True, clips=clips)


SPECS += [
    # Production (production_wave/README.md).
    # autolathe.dm: busy ? _n : panel_open ? _t : autolathe; the live body plays its working state
    production('autolathe', 'autolathe.glb', 'icons/obj/machines/lathes.dmi', ['autolathe', 'autolathe_n', 'autolathe_t'], awayFromWall=True,
               clips={'working': None, 'idle': None, 'maintenance_open': 'open s:autolathe_t', 'maintenance_close': None}),
    rnd_production('protolathe', 'protolathe_techfab.glb', 'protolathe', 'protolathe_n', stripe=True),
    rnd_production('circuit_imprinter', 'circuit_imprinter.glb', 'circuit_imprinter', 'circuit_imprinter_ani'),
    # mech_fabricator.dm: fab-o with its panel open, else fab-idle; fab-active overlay while it builds
    production('mecha_fabricator', 'mecha_fabricator.glb', 'icons/obj/machines/robotics.dmi', ['fab-idle', 'fab-o'], awayFromWall=True,
               clips={'working': 'o:fab-active', 'idle': None, 'maintenance_open': 'open s:fab-o', 'maintenance_close': None}),
    # component_printer.dm: as the fabricator's states, in wiremod_fab.dmi
    production('component_printer', 'component_printer.glb', 'icons/obj/machines/wiremod_fab.dmi', ['fab-idle', 'fab-o'], awayFromWall=True,
               clips={'working': 'o:fab-active', 'idle': None, 'maintenance_open': 'open s:fab-o', 'maintenance_close': None}),
    # component_printer.dm (module duplicator): module-fab-o / -idle; its print and scan flicks play on the live body
    production('module_duplicator', 'module_duplicator.glb', 'icons/obj/machines/wiremod_fab.dmi', ['module-fab-idle', 'module-fab-o'], awayFromWall=True,
               clips={'print': None, 'scan': None, 'idle': None, 'maintenance_open': 'open s:module-fab-o', 'maintenance_close': None}),
    # Resources (resources_wave/README.md).
    # machine_redemption.dm: "[base_icon_state][panel_open ? "-open" : powered() ? null : "-off"]"
    resources('ore_redemption', 'ore_redemption.glb', MINING, ['ore_redemption', 'ore_redemption-open', 'ore_redemption-off'], rotate=False,
              clips={'ore_redemption': 's:ore_redemption', 'ore_redemption-open': 's:ore_redemption-open', 'ore_redemption-off': 's:ore_redemption-off',
                     'maintenance_open': 'open s:ore_redemption-open', 'maintenance_close': None}),
    # _boulder_processing.dm: "[base_icon_state][-off]" (unanchored, its panel open or unpowered), drawn on the live body; simple_rotation of its own
    resources('boulder_smelter', 'boulder_smelter.glb', MINING, ['smelter', 'smelter-off']),
    resources('boulder_refinery', 'mineral_stacker_refinery.glb', MINING, ['stacker', 'stacker-off'],
              clips={'stacker': 's:stacker', 'stacker-off': 's:stacker-off'}),
    # brm.dm: brm[-off][-toggled]; its retrieval flick plays on the live body
    resources('boulder_retrieval_matrix', 'boulder_retrieval_matrix.glb', MINING, ['brm', 'brm-off', 'brm-toggled', 'brm-off-toggled'],
              clips={'brm': 's:brm', 'brm-off': ['s:brm-off', 's:brm-off-toggled'], 'brm-toggled': 's:brm-toggled', 'retrieval': None, 'idle': None}),
    # machine_silo.dm: silo; its silo_active flick plays on the live body
    resources('ore_silo', 'ore_silo.glb', 'icons/obj/machines/ore_silo.dmi', ['silo'], awayFromWall=True, clips={'silo': 's:silo', 'silo_active': None}),
    # prisonlabor.dm: offline unpowered, loop while it presses, online_loaded with a plate, online
    resources('plate_press', 'plate_press.glb', 'icons/obj/machines/prison.dmi', ['offline', 'online', 'online_loaded', 'loop'], awayFromWall=True,
              clips={'offline': 's:offline', 'online': 's:online', 'online_loaded': ['s:online_loaded', 's:loop'], 'press_cycle': 'open s:loop'}, seconds=1.37),
    # recycler.dm: grinder-oOpen with its panel open, else grinder-o[on and unsafe]; its dir runs its conveyor
    resources('recycler', 'recycler.glb', 'icons/obj/machines/recycling.dmi', ['grinder-o0', 'grinder-o1', 'grinder-oOpen'], rotate=False,
              clips={'grinder-o0': 's:grinder-o0', 'grinder-o1': 's:grinder-o1', 'grinder-oOpen': 's:grinder-oOpen'}),
]


science = wave('science_wave')


def later_frames(base, count):
    """The frame nodes after the first of a state the live first frame plays (exported as <base>_frame_N, which a chemical separator's blinks use too)."""
    return [f'{base}_frame_{i}' for i in range(1, count)]


BITRUNNING = 'icons/obj/machines/bitrunning.dmi'
NUKE_STATES = ['nuclearbomb_base', 'nuclearbomb_timing', 'nuclearbomb_exploding']
TURRETS = 'icons/obj/weapons/turrets.dmi'

SPECS += [
    # Science (science_wave/README.md).
    # portable_turret.dm: popped down, the turret is invisible and its cover (its own thing) shows turretCover; up, the cover shows openTurretCover and the
    # turret [base]_off/_stun/_lethal/_broken. The cover draws the base and its lids; the turret its head, turned to its dir as tg's aims it.
    science('turret_cover', 'ai_defense_turret.glb', TURRETS, ['turretCover', 'openTurretCover'], rotate=False,
            skip=['Rotating_Head', 'Mode_standard_off', 'Mode_standard_stun', 'Mode_standard_lethal', 'Mode_standard_broken'],
            clips={'deploy': None, 'retract': None, 'rotate_90': None},
            parts={'Circular_Lid_1': {'states': ['turretCover']}, 'Circular_Lid_-1': {'states': ['turretCover']}}),
    science('turret_standard', 'ai_defense_turret.glb', TURRETS, ['standard_off', 'standard_stun', 'standard_lethal', 'standard_broken'], rotate=False,
            skip=['Circular_Base', 'Circular_Lid_1', 'Circular_Lid_-1', 'Lifting_Well_Floor_And_Support'],
            clips={'standard_off': 's:standard_off', 'standard_stun': 's:standard_stun', 'standard_lethal': 's:standard_lethal',
                   'standard_broken': 's:standard_broken', 'deploy': None, 'retract': None, 'rotate_90': None}),
    # _nuclear_bomb.dm: nuclearbomb_base (off, or taken apart), _timing, _exploding; its lights, panel and plate overlays are drawn on the live device
    science('nuclear_bomb', 'beer_nuclear_device.glb', 'icons/obj/machines/nuke.dmi', NUKE_STATES,
            clips={'nuclearbomb_base': 's:nuclearbomb_base', 'nuclearbomb_timing': 's:nuclearbomb_timing',
                   'nuclearbomb_exploding': 's:nuclearbomb_exploding', 'nuclearbomb_safetyoff': None}),
    science('nuclear_terminal', 'station_selfdestruct_terminal.glb', 'icons/obj/machines/nuke_terminal.dmi', NUKE_STATES, awayFromWall=True,
            clips={'nuclearbomb_base': 's:nuclearbomb_base', 'nuclearbomb_timing': 's:nuclearbomb_timing',
                   'nuclearbomb_exploding': 's:nuclearbomb_exploding', 'nuclearbomb_safetyoff': None,
                   'maintenance_open': 'hide o:panel-unscrewed|o:panel-removed', 'maintenance_close': None}),
    # destructive_scanner.dm: tube_open, tube_on while it scans (the tube let down over its grille, blinking); its tube_down / tube_up flicks are the
    # tube's squeeze
    science('destructive_scanner', 'destructive_scanner.glb', 'icons/obj/machines/destructive_scanner.dmi', ['tube_open', 'tube_on'], awayFromWall=True,
            clips={'close': 'open s:tube_on', 'open': None, 'tube_on': None}, skip=['Scanning_Grille_1'],
            extra={'Scanning_Grille_0': {'node': 'Scanning_Grille_0', 'states': ['tube_on']}}),
    # destructive_analyzer.dm: d_analyzer[_l], rdmachines.dm's _t with its panel open; its _la and _process flicks play on the live body
    science('destructive_analyzer', 'destructive_analyzer.glb', 'icons/obj/machines/research.dmi', ['d_analyzer', 'd_analyzer_l', 'd_analyzer_t'], awayFromWall=True,
            clips={'working': None, 'idle': None, 'open': 'flick d_analyzer_la', 'close': None}),
    # experimentor.dm: h_lathe, _wloop while it works, _t with its panel open; its 64-pixel sprite covers its tile and the one east of it
    science('experimentor', 'experimentor.glb', 'icons/obj/machines/experimentator.dmi', ['h_lathe', 'h_lathe_wloop', 'h_lathe_t'], rotate=False, offset=[2.0, 0, 0],
            clips={'working': 's:h_lathe_wloop', 'idle': None, 'h_lathe_load': 'flick h_lathe_load', 'h_lathe_leave': None,
                   'maintenance_open': 'open s:h_lathe_t', 'maintenance_close': None}),
    # _netpod.dm: netpod unpowered, _open_active open, _panel, _closed[_active]
    science('netpod', 'netpod.glb', BITRUNNING, ['netpod', 'netpod_open_active', 'netpod_panel', 'netpod_closed', 'netpod_closed_active'], awayFromWall=True,
            skip=later_frames('netpod_open_active', 6) + later_frames('netpod_closed_active', 8),
            clips={'netpod': 's:netpod', 'netpod_open_active': 's:netpod_open_active', 'netpod_closed_active': 's:netpod_closed_active',
                   'netpod_closed': 's:netpod_closed', 'netpod_panel': 's:netpod_panel',
                   'open': 'open s:netpod_open_active', 'close': None, 'maintenance_open': 'hide s:netpod_panel', 'maintenance_close': None}),
    # server/_parent.dm: qserver without a domain or power, _panel, _on when ready, _off cooling down
    science('quantum_server', 'quantum_server.glb', BITRUNNING, ['qserver', 'qserver_panel', 'qserver_on', 'qserver_off'], awayFromWall=True,
            skip=later_frames('qserver_on', 12) + later_frames('qserver_off', 13),
            clips={'qserver': 's:qserver', 'qserver_on': 's:qserver_on', 'qserver_off': 's:qserver_off', 'qserver_panel': 's:qserver_panel',
                   'maintenance_open': 'open s:qserver_panel', 'maintenance_close': None}),
    # byteforge.dm: byteforge or byteforge_panel
    science('byteforge', 'byteforge.glb', BITRUNNING, ['byteforge', 'byteforge_panel'], awayFromWall=True,
            skip=[f for o in ('on_overlay', 'on_overlay_angry', 'on_particles', 'on_particles_angry') for f in later_frames(o, 7)],
            overlayFrames=True, clips={'on_overlay': 'o:on_overlay', 'on_overlay_angry': 'o:on_overlay_angry', 'on_particles': 'o:on_particles',
                                       'on_particles_angry': 'o:on_particles_angry', 'maintenance_open': 'open s:byteforge_panel', 'maintenance_close': None}),
    # skill_station.dm: implantchair[_open][_occupied]; the sheet's unpowered and maintenance states are not used
    science('skill_station', 'skillsoft_station.glb', 'icons/obj/machines/implant_chair.dmi', ['implantchair', 'implantchair_open', 'implantchair_occupied'],
            awayFromWall=True, clips={'implantchair': 's:implantchair', 'implantchair_open': 's:implantchair_open', 'implantchair_occupied': 's:implantchair_occupied',
                                      'implantchair_maintenance': None, 'implantchair_open_maintenance': None, 'implantchair_unpowered': None,
                                      'implantchair_unpowered_maintenance': None, 'implantchair_open_unpowered': None, 'implantchair_open_unpowered_maintenance': None,
                                      'open': 'open s:implantchair_open', 'close': None}),
    # anomaly_refinery.dm: its panel open is -off
    science('anomaly_refinery', 'anomaly_refinery.glb', 'icons/obj/machines/research.dmi', ['explosive_compressor', 'explosive_compressor-off'], awayFromWall=True,
            clips={'explosive_compressor': 's:explosive_compressor', 'explosive_compressor-off': 's:explosive_compressor-off'}),
]


service = wave('service_wave')
KITCHEN = 'icons/obj/machines/kitchen.dmi'
LIBRARY = 'icons/obj/service/library.dmi'


def kitchen_oven(id, glb, base):
    """oven.dm: [base]_on / _off; its door is the overlay [base]_lid_open or _lid_closed (the tray slides out with it)."""
    return service(id, glb, KITCHEN, [base + '_off', base + '_on'], awayFromWall=True,
                   clips={base + '_off': f's:{base}_off', base + '_on': f's:{base}_on', 'open': f'open o:{base}_lid_open', 'close': None, 'interact': None})


SPECS += [
    # Service (service_wave/README.md).
    # biogenerator.dm: its _o_panel, _o_container, _o_biomass_N, _o_process and _o_screen overlays
    service('biogenerator', 'biogenerator.glb', 'icons/obj/machines/biogenerator.dmi', ['biogenerator'], awayFromWall=True, overlayFrames=True,
            clips=dict({f'biomass_{n}': f'o:biogenerator_o_biomass_{n}' for n in range(1, 8)}, container='o:biogenerator_o_container', no_container=None,
                       working='o:biogenerator_o_process', idle=None, powered='o:biogenerator_o_screen', unpowered=None, empty=None,
                       maintenance_open='open o:biogenerator_o_panel', maintenance_close=None)),
    # lib_machines.dm: binder / bigscanner, "[base]2" with its panel open; their working flicks play on the live body
    service('bookbinder', 'bookbinder.glb', LIBRARY, ['binder', 'binder2'], awayFromWall=True,
            clips={'working': None, 'idle': None, 'maintenance_open': 'open s:binder2', 'maintenance_close': None}),
    service('library_scanner', 'library_scanner.glb', LIBRARY, ['bigscanner', 'bigscanner2'], awayFromWall=True, clips={'working': None, 'idle': None}),
    # photocopier.dm: photocopier, photocopier2 with its panel open; its photocopier1 flick on the live body
    service('photocopier', 'photocopier.glb', LIBRARY, ['photocopier', 'photocopier2'], awayFromWall=True,
            clips={'working': None, 'idle': None, 'maintenance_open': 'open s:photocopier2', 'maintenance_close': None}),
    # deep_fryer.dm: "[base_icon_state]_[frying ? "on" : panel_open ? "open" : "off"]"; the baskets are let down while it fries
    service('deep_fryer', 'deep_fryer.glb', KITCHEN, ['fryer_off', 'fryer_on', 'fryer_open'], awayFromWall=True,
            clips={'fryer_off': ['s:fryer_off', 's:fryer_open'], 'fryer_on': 's:fryer_on', 'lower': 'open s:fryer_on', 'raise': None}),
    # gibber.dm: grinder[_open]; its grinder_empty / _loaded / _active overlays, the jaws turning while it works
    service('gibber', 'gibber.glb', KITCHEN, ['grinder', 'grinder_open'], awayFromWall=True,
            clips={'grinder_active': 'o:grinder_active', 'grinder_empty': 'o:grinder_empty', 'grinder_loaded': 'o:grinder_loaded', 'working': None, 'idle': None},
            parts={'Grinding_Jaw_0': {'pivot': True, 'axis': [0, 0, -1], 'spin': 450, 'overlays': ['grinder_jaws_active']},
                   'Grinding_Jaw_1': {'pivot': True, 'axis': [0, 0, 1], 'spin': 450, 'overlays': ['grinder_jaws_active']}},
            extra={'Grinding_Jaw_0_still': {'node': 'Grinding_Jaw_0', 'exceptOverlays': ['grinder_jaws_active']},
                   'Grinding_Jaw_1_still': {'node': 'Grinding_Jaw_1', 'exceptOverlays': ['grinder_jaws_active']}}),
    # griddle.dm: "griddle[variant]_[on ? "on" : "off"]"
    service('griddle', 'griddle.glb', KITCHEN, [f'griddle{v}_{m}' for v in (1, 2, 3) for m in ('off', 'on')], awayFromWall=True,
            clips={f'griddle{v}_{m}': f's:griddle{v}_{m}' for v in (1, 2, 3) for m in ('off', 'on')}),
    kitchen_oven('kitchen_oven', 'kitchen_oven.glb', 'oven'),
    kitchen_oven('kitchen_range', 'kitchen_range.glb', 'range'),
    # PDApainter.dm: pdapainter[-off|-broken]; its -closed overlay while it holds no PDA (the lid shut)
    service('pda_painter', 'pda_painter.glb', 'icons/obj/machines/pda.dmi', ['pdapainter', 'pdapainter-off', 'pdapainter-broken'], awayFromWall=True,
            clips={'pdapainter': 's:pdapainter', 'pdapainter-off': 's:pdapainter-off', 'pdapainter-broken': 's:pdapainter-broken',
                   'open': None, 'close': None}),
    # photobooth.dm: booth_off unpowered, booth_open, booth_closed (its curtain drawn); booth_panel with its panel open
    service('photobooth', 'photobooth.glb', 'icons/obj/machines/photobooth.dmi', ['booth_open', 'booth_closed', 'booth_off'], awayFromWall=True,
            clips={'booth_open': ['s:booth_open', 's:booth_closed'], 'booth_off': 's:booth_off', 'curtain_closed': 's:booth_closed', 'curtain_open': None}),
    # seed_extractor.dm, processor.dm: [base]_open with the panel open
    service('seed_extractor', 'seed_extractor.glb', 'icons/obj/service/hydroponics/equipment.dmi', ['sextractor', 'sextractor_open'], awayFromWall=True,
            clips={'maintenance_open': 'open s:sextractor_open', 'maintenance_close': None}),
    service('food_processor', 'food_processor.glb', KITCHEN, ['processor', 'processor_open'], awayFromWall=True,
            clips={'maintenance_open': 'open s:processor_open', 'maintenance_close': None}),
    service('slime_processor', 'slime_processor.glb', KITCHEN, ['processor_slime', 'processor_slime_open'], awayFromWall=True,
            clips={'maintenance_open': 'open s:processor_slime_open', 'maintenance_close': None}),
    # washing_machine.dm: wm_running_[bloody] while it runs (the drum turns), else wm_[state_open]_blood or wm_[state_open]_[full]
    service('washing_machine', 'washing_machine.glb', 'icons/obj/machines/washing_machine.dmi',
            ['wm_0_0', 'wm_0_1', 'wm_1_0', 'wm_1_1', 'wm_0_blood', 'wm_1_blood', 'wm_running_0', 'wm_running_1'], awayFromWall=True,
            clips={'wm_0_0': None, 'wm_0_1': ['s:wm_0_1', 's:wm_1_1', 's:wm_running_0'], 'wm_0_blood': ['s:wm_0_blood', 's:wm_1_blood', 's:wm_running_1'],
                   'running': None, 'stopped': None, 'open': 'open s:wm_1_0|s:wm_1_1|s:wm_1_blood', 'close': None},
            parts={'Drum': {'pivot': True, 'axis': [0, 0, -1], 'spin': 225, 'states': ['wm_running_0', 'wm_running_1']}},
            extra={'Drum_still': {'node': 'Drum', 'exceptStates': ['wm_running_0', 'wm_running_1']}}),
    # coffeemaker.dm (impressa): its pot_[empty|full|bluespace] overlay while it holds a pot; its cups, extras and grinder overlays on the live body
    service('coffeemaker_impressa', 'coffee_machine_impressa.glb', 'icons/obj/machines/coffeemaker.dmi', ['coffeemaker_impressa'], awayFromWall=True,
            parts={n: {'overlays': ['pot_empty', 'pot_full', 'pot_bluespace']} for n in ('Coffee_Pot', 'Pot_Handle')}),
    service('food_cart', 'food_cart.glb', 'icons/obj/service/kitchen.dmi', ['foodcart']),
    service('icecream_vat', 'icecream_cart.glb', 'icons/obj/service/kitchen.dmi', ['icecream_vat']),
]


transport = wave('transport_wave')
TELEPORTER = 'icons/obj/machines/teleporter.dmi'

SPECS += [
    # Transport (transport_wave/README.md).
    # pipe_dispenser.dm: the atmospherics, disposal and transit dispensers all show pipe_d
    transport('pipe_dispenser', 'pipe_dispenser.glb', 'icons/obj/machines/lathes.dmi', ['pipe_d'], awayFromWall=True),
    # mechlaunchpad.dm: mechpad, mechpad-open with its panel open; its mechpad-launch flick plays on the live pad
    transport('mech_pad', 'orbital_mech_pad.glb', 'icons/obj/machines/telepad.dmi', ['mechpad', 'mechpad-open'],
              clips={'launch': None, 'idle': None, 'maintenance_open': 'open s:mechpad-open', 'maintenance_close': None}),
    # teleporter.dm: hub "[base_icon_state][panel_open ? "-o" : (is_ready() ? 1 : 0)]"; its coils play tele1's frames
    transport('teleporter_hub', 'teleporter_hub.glb', TELEPORTER, ['tele0', 'tele1', 'tele-o'],
              clips={'active': 's:tele1', 'inactive': None, 'maintenance_open': 'open s:tele-o', 'maintenance_close': None}),
    # teleporter.dm: station controller, -o with its panel open, -p unpowered, -c calibrating
    transport('teleporter_station', 'teleporter_station.glb', TELEPORTER, ['controller', 'controller-o', 'controller-p', 'controller-c'],
              clips={'controller': 's:controller', 'controller-p': 's:controller-p', 'controller-c': 's:controller-c', 'calibrating': 's:controller-c',
                     'powered': None, 'unpowered': None, 'maintenance_open': 'open s:controller-o', 'maintenance_close': None}),
    # gateway.dm: portal_frame drawn 32 pixels left and down, bound 96 x 64 from its tile's west neighbour: it covers its row and the one north of it;
    # portal_light while it can teleport, portal_effect while it is open
    transport('gateway', 'gateway_low_profile.glb', 'icons/obj/machines/gateway.dmi', ['portal_frame'], rotate=False, offset=[0, 0, -1.0], overlayFrames=True,
              clips={'ready': 'o:portal_light', 'active': 'o:portal_effect', 'off': None}),
    # exodrone.dm: the launcher's launchpad_fuel_* overlay by its fuel
    transport('exodrone_launcher', 'exploration_drone_launcher.glb', 'icons/obj/exploration.dmi', ['launcher'],
              clips={'launchpad_fuel_basic': 'o:launchpad_fuel_basic', 'launchpad_fuel_advanced': 'o:launchpad_fuel_advanced',
                     'launchpad_fuel_exotic': 'o:launchpad_fuel_exotic', 'empty': None}),
]


wall = wave('wall_wave')


def on_wall(id, glb, icon, states, back=-0.08, make=None, **more):
    """A wall wave model is made from the floor (its real height) with its back at z = back: the wall rule raises it to the planner's mount height for its
    type (1.2 m at a 2 m wall for these) and puts its origin on the open tile 1 m before the wall's face, so the offset takes both back."""
    return (make or wall)(id, glb, icon, states, rule='wall', offset=[0, -1.2, round(-1.0 - back + 0.005, 3)], **more)


VITALS_OVERLAYS = ['blood', 'buttons', 'ekg_fast', 'ekg', 'ekg_flat', 'human_chest', 'human_head', 'human_l_arm', 'human_l_leg', 'human_r_arm',
                   'human_r_leg', 'mob', 'resp_flat', 'resp']

SPECS += [
    # Wall devices (wall_wave/README.md). The detective board is tg's noticeboard state of wallmounts.dmi, which the notice board's model draws already.
    # wall_vitals.dm: the frame and its overlays: buttons, the ekg / resp lines by the patient's state, the body parts or a mob, blood; tinted by health
    on_wall('vitals_monitor', 'vitals_monitor.glb', 'icons/obj/machines/vitals_monitor.dmi', ['frame'], overlayFrames=True,
            clips=dict({s: 'o:' + s for s in VITALS_OVERLAYS}, off=None, **{s + '_hidden': None for s in VITALS_OVERLAYS})),
    # incident_display.dm: display_normal / _broken / _shame; its digits are overlays drawn on the live display
    on_wall('incident_display', 'incident_display.glb', 'icons/obj/machines/incident_display.dmi', ['display_normal', 'display_broken', 'display_shame'], numbered=True,
            clips={'display_normal': 's:display_normal', 'display_broken': 's:display_broken', 'display_shame': 's:display_shame'}),
    # mining_weather_monitor.dm, weatherannouncer.dm: the wallgreen / wallyellow / wallred overlay by the weather
    on_wall('mining_weather_monitor', 'mining_weather_monitor.glb', 'icons/obj/devices/miningradio.dmi', ['wallmount'], overlayFrames=True,
            clips={'wallgreen': 'o:wallgreen', 'wallyellow': 'o:wallyellow', 'wallred': 'o:wallred', 'off': None}),
    # reagent_dispenser.dm: the wall virus food dispenser
    on_wall('virus_food_dispenser', 'virus_food_dispenser.glb', 'icons/obj/medical/chemical_tanks.dmi', ['virus_food']),
    # fish_mount.dm: the plaque; the fish is its own thing drawn in front of it
    on_wall('fish_mount', 'fish_trophy_mount.glb', 'icons/obj/wallmounts.dmi', ['fish_mount'], back=-0.07),
    # law.dm: the AI core's law rack; its cards are one overlay state at several pixel heights, drawn on the live rack
    wall('ai_core_law_rack', 'ai_core_law_rack.glb', 'icons/obj/machines/law_rack.dmi', ['core_rack'], awayFromWall=True,
         clips={f'slot_{n}_{s}': None for n in range(1, 11) for s in ('loaded', 'empty')}),
]


construction = wave('computers_construction')
gravity = wave('gravity_and_tanks')
_TANK = ('Body', 'Handle', 'Valve')
_EXTINGUISHER = ('Body', 'Handle', 'Hose', 'Nozzle', 'Valve')
HOLDER_OWN = {'holder_anesthetic': _TANK, 'holder_anesthetic_warning': _TANK, 'holder_emergency_clown': _TANK, 'holder_emergency_engi': _TANK,
              'holder_foam_extinguisher': _EXTINGUISHER, 'holder_generic': _TANK, 'holder_oxygen_f': _TANK, 'holder_oxygen_fr': _TANK}
HOLDER_SHARED = {'holder_emergency': _TANK, 'holder_extinguisher': _EXTINGUISHER, 'holder_oxygen': _TANK, 'holder_skub': ('Body',)}
GRAVITY = 'icons/obj/machines/gravity_generator.dmi'
GRAVITY_FIELD = {'startup': 'Field_startup_3', 'idle': 'Field_idle_0', 'activating': 'Field_activating_2', 'activated': 'Field_activated_0'}  # the widest frame each

SPECS += [
    # Computers and construction (computers_construction). The airlock assembly is drawn by the station airlocks' entry, which takes every state of their icons.
    # buildandrepair.dm: a computer frame shows its construction state, 0 .. 4
    construction('computer_frame', 'computer_frame.glb', 'icons/obj/devices/stock_parts.dmi', ['0', '1', '2', '3', '4'],
                 part={'live': False},
                 clips={'empty': 's:0', 'board_installed': 's:1', 'board_secured': 's:2', 'wired': 's:3', 'glassed': 's:4'}),
    # constructable_frame.dm: "[base_icon_state][state]", box_0 .. box_2
    construction('machine_frame', 'machine_frame.glb', 'icons/obj/devices/stock_parts.dmi', ['box_0', 'box_1', 'box_2'],
                 part={'live': False},
                 clips={'empty': 's:box_0', 'wired': 's:box_1', 'board_installed': 's:box_2'}),
    # _computer.dm, records/medical.dm: the laptop's keyboard overlay laptop_key (its _off unpowered), its screen medlaptop, [icon_state]_broken
    construction('medical_laptop', 'medical_laptop.glb', 'icons/obj/machines/computer.dmi', ['laptop'],
                 clips={'powered': None, 'unpowered': None, 'broken': None},
                 part={'live': False}, skip=['Broken_Screen', 'Unpowered_Keyboard'],
                 parts={'Medical_Screen': {'live': True, 'picture': 'appearance'},
                        'Powered_Keyboard': {'live': True, 'picture': 'appearance'}}),
    # modular_computer.dm: console / console-off; its program's overlay or menu / standby on the screen, bsod and broken when broken
    construction('modular_console', 'modular_computer.glb', 'icons/obj/machines/modular_console.dmi', ['console', 'console-off'],
                 clips={'powered': None, 'standby': None, 'unpowered': None, 'broken': None},
                 part={'live': False}, skip=['Broken_Screen', 'Unpowered_Keyboard'],
                 parts={'Program_Screen': {'live': True, 'picture': 'appearance'},
                        'Powered_Keyboard': {'live': True, 'picture': 'appearance'}}),
    # gravitygenerator.dm: nine machines on a 3 x 3 square, each "[status]_[sprite_number]"; the main one (8) is the bottom middle and draws the whole low
    # generator (the owner, 2026-10-27), 2 m north of its tile; the other eight draw nothing; its flat field ring is left out (the owner, 2026-10-29); broken,
    # get_status() is fix0 .. fix3 by its repair stage, each its own file (repair art; its four corner lights only on the running one); the charge field is
    # the centre part's (below)
    gravity('gravity_generator', 'gravity_generator_low_profile_operational.glb', GRAVITY, ['on_8'], rotate=False, offset=[0, 0, -2.0], size=(2.0, 2.0),
            skip=[f'Field_{phase}_{n}' for phase in ('startup', 'idle', 'activating', 'activated') for n in range(9)]),
    *(gravity(f'gravity_generator_{s}', f'gravity_generator_low_profile_{s}.glb', GRAVITY, [f'{s}_8'], rotate=False, offset=[0, 0, -2.0], size=(2.0, 2.0))
      for s in ('off', 'fix0', 'fix1', 'fix2', 'fix3')),
    {'id': 'gravity_generator_parts', 'hidden': True, 'icons': [GRAVITY],
     'states': [f'{s}_{n}' for s in ('on', 'off', 'fix0', 'fix1', 'fix2', 'fix3') for n in range(1, 10) if n not in (5, 8)]},
    # the centre part (5, the tile north of the main one, the model's origin) draws the charge field: set_state()'s overlay by charge_count (startup, idle,
    # activating, activated, none below 21 per cent) on one frame mesh each, which the overlay's own animated picture steps through
    gravity('gravity_generator_field', 'gravity_generator_low_profile_operational.glb', GRAVITY,
            [f'{s}_5' for s in ('on', 'off', 'fix0', 'fix1', 'fix2', 'fix3')], rotate=False, size=(2.0, 2.0), overlayFrames=True,
            always=list(GRAVITY_FIELD.values()), parts={node: {'overlays': [phase]} for phase, node in GRAVITY_FIELD.items()},
            skip=[f'Corner_{n}_Peripheral_{p}' for n in range(1, 5) for p in ('Cap', 'Central_Coil', 'Forks', 'Housing', 'Lights')]
                 + [f'Generator_{p}' for p in ('Center', 'Left_Brace', 'Left_Coil', 'Right_Brace', 'Right_Coil')]
                 + ['Floor_Conduits', 'Installation_Base_3x3', 'Rear_Field_Stabilizer', 'Field_activated_1', 'Field_activated_2', 'Field_activated_3']),
    # tank_holder.dm: holder, or the held thing's tank_holder_icon_state. Each held thing is a group of the file; four groups share the node names Item_*,
    # told apart by their materials. Its clamps reach the sprite's transparent pixels: drawn opaque, as Blender shows it
    gravity('tank_holder', 'tank_holder.glb', 'icons/obj/canisters.dmi', ['holder'] + list(HOLDER_OWN) + list(HOLDER_SHARED), rotate=False, part={'opaque': True},
            skip=['Item_' + n for n in ('Body', 'Handle', 'Hose', 'Nozzle', 'Valve')],
            parts={f'{state}_{n}': {'states': [state]} for state, names in HOLDER_OWN.items() for n in names},
            extra={f'{state}_{n}': {'node': 'Item_' + n, 'material': 'TG_' + state, 'states': [state], 'opaque': True}
                   for state, names in HOLDER_SHARED.items() for n in names}),
    # tank_dispenser.dm: oxygen-1 .. -4 and plasma-1 .. -5 overlays by its stock (capped)
    gravity('tank_dispenser', 'tank_dispenser.glb', 'icons/obj/structures.dmi', ['dispenser'], awayFromWall=True,
            parts=dict({f'Oxygen_Tank_{k}': {'overlays': [f'oxygen-{n}' for n in range(k, 5)]} for k in range(1, 5)},
                       **{f'Plasma_Tank_{k}': {'overlays': [f'plasma-{n}' for n in range(k, 6)]} for k in range(1, 6)})),
    # thermomachine.dm: a GAGS machine, thermo-open with its panel open, thermo_1 on, thermo_base; a unary pipe device (its dir is its port)
    gravity('thermomachine', 'temperature_control_unit.glb', 'gags:/datum/greyscale_config/thermomachine|*', ['thermo_base', 'thermo_1', 'thermo-open'],
            rotate=False),
]


def props(folder):
    def make(id, glb, icon, states, **more):
        s = {'id': id, 'glb': f'station_props/{folder}/{glb}', 'icons': [icon], 'states': states}
        s.update(more)
        return s
    return make


furniture = props('furniture_wave')
shuttle = props('shuttle_furniture')
BUREAUCRACY = 'icons/obj/service/bureaucracy.dmi'
STRUCTURES = 'icons/obj/structures.dmi'
GYM = 'icons/obj/fluff/gym_equipment.dmi'
WIDE = 'icons/obj/chairs_wide.dmi'


def cabinet(id, glb, state):
    """filingcabinet.dm: "[initial(icon_state)]-open" for half a second as something goes in or comes out (the middle drawer slides out)."""
    return furniture(id, glb, BUREAUCRACY, [state, state + '-open'], awayFromWall=True,
                     clips={'open': f'open s:{state}-open', 'close': None, 'interact': None})


def bench(kind):
    """sofa.dm: the GAGS benches (bench_middle, _left, _right, _solo, _corner), turned by their dir as tg's chairs; the cover keeps its own grey."""
    return furniture('bench_' + kind, f'bench_{kind}.glb', f'gags:/datum/greyscale_config/bench_{kind}|*', ['bench_' + kind], family='bench')


def propulsion(id, glb, states):
    """shuttle_engines.dm: propulsion and burst engines share one model; the energy core glows the blue of the sprite's exhaust (#53a0ed, shuttle.dmi propulsion)
    and hums with a quick flicker, a bluespace-ish halo and a small blue spill (the owner, 2026-10-28)."""
    core = {'live': False, 'color': '#53a0ed', 'shader': 'nozzle', 'halo': '#53a0ed',
            'pulse': [[0.12, 0.75], [0.08, 1.0], [0.15, 0.85], [0.1, 1.0], [0.2, 0.7]],
            'light': {'position': [0, 1.0, 0.95], 'color': '#53a0ed', 'range': 1.6, 'energy': 0.35}}
    return shuttle(id, glb, 'icons/turf/shuttle.dmi', states, part={'live': False}, names={'Energy_Core': 'core'}, always=['Energy_Core'], parts={'Energy_Core': core})


def seat_row(id, glb, state, corner=False):
    """sofa.dm, pew.dm: a module of 0.9 m stretched along its row over the 2 m tile, so the modules of a row join (the owner, 2026-10-27); a corner over
    both sides of its tile."""
    return shuttle(id, glb, WIDE, [state], size=(0.9, 2.0), fit={'width': 'tile' if corner else 'row', 'height': 2.0})


SPECS += [
    # Furniture (furniture_wave/README.md).
    cabinet('filing_cabinet', 'filing_cabinet.glb', 'filingcabinet'),
    cabinet('filing_cabinet_white', 'filing_cabinet_white.glb', 'tallcabinet'),
    cabinet('employment_cabinet', 'employment_cabinet.glb', 'employmentcabinet'),
    cabinet('drawer_cabinet', 'drawer_cabinet.glb', 'chestdrawer'),
    # displaycase.dm: glassbox with its glassbox_closed overlay while the hood is on, glassbox_broken once smashed; the showpiece is an overlay of its own
    furniture('display_case', 'display_case.glb', STRUCTURES, ['glassbox'], clips={'broken': 'o:glassbox_broken', 'intact': None, 'open': None, 'close': None},
              parts={'Glass_Hood': {'overlays': ['glassbox_closed']}}),
    # displaycase.dm (forsale): laserbox[_broken|_open|_empty]
    furniture('food_display_case', 'food_display_case.glb', 'icons/obj/machines/display.dmi', ['laserbox', 'laserbox_open', 'laserbox_broken', 'laserbox_empty'],
              clips={'broken': 's:laserbox_broken', 'intact': None, 'open': 'open s:laserbox_open', 'close': None},
              parts={'Glass_Hood': {'exceptStates': ['laserbox_broken']}}),
    # bedsheet_bin.dm: linenbin-full / -half / -empty
    furniture('linen_bin', 'linen_bin.glb', STRUCTURES, ['linenbin-full', 'linenbin-half', 'linenbin-empty'],
              clips={'full': ['s:linenbin-full', 's:linenbin-half'], 'half': None, 'empty': None}),
    # reagent_dispenser.dm: water_cooler; its water_cooler_overlay100 / 50 in the mix's colour
    furniture('water_cooler', 'water_cooler.glb', 'icons/obj/medical/chemical_tanks.dmi', ['water_cooler'], awayFromWall=True,
              clips={'full': None, 'half': None, 'empty': None, 'jug_removed': None, 'jug_installed': None}, always=['Removable_Jug'],
              parts={'Liquid': {'overlays': ['water_cooler_overlay100'], 'tintOverlay': 'water_cooler_overlay100'}},
              extra={'Liquid_half': {'node': 'Liquid', 'overlays': ['water_cooler_overlay50'], 'tintOverlay': 'water_cooler_overlay50'}}),
    # janitor.dm: the bucket's and the cart's water overlay
    furniture('mop_bucket', 'mop_bucket.glb', 'icons/obj/service/janitor.dmi', ['mopbucket'], clips={'filled': 'o:mopbucket_water', 'empty': None}),
    furniture('janitorial_cart', 'janitorial_cart.glb', 'icons/obj/service/janitor.dmi', ['cart'], clips={'filled': 'o:cart_water', 'empty': None}),
    # safe.dm: "[initial(icon_state)][open ? null : "_locked"]"
    furniture('safe', 'vault_safe.glb', STRUCTURES, ['safe', 'safe_locked'], awayFromWall=True, clips={'open': 'open s:safe', 'close': None}),
    # weight_machine.dm, punching_bag.dm, desk_bell.dm: their flicks play on the live models
    furniture('weight_machine', 'chest_press.glb', GYM, ['stacklifter'], clips={'workout': None}),
    furniture('bench_press', 'bench_press.glb', GYM, ['benchpress'], clips={'workout': None}),
    furniture('punching_bag', 'punching_bag.glb', GYM, ['punchingbag'], clips={'punch': 'flick punchingbag-punch'}),
    furniture('desk_bell', 'desk_bell.glb', BUREAUCRACY, ['desk_bell'], clips={'ring': None}),
    # religion_structures.dm: the altar of gods, its sect's convertaltar-* variant drawn on the live altar
    furniture('altar_of_gods', 'altar.glb', 'icons/obj/service/hand_of_god_structures.dmi', ['convertaltar'], awayFromWall=True),
    furniture('easel', 'easel.glb', 'icons/obj/art/artstuff.dmi', ['easel']),
    furniture('dog_bed', 'pet_bed.glb', 'icons/obj/bed.dmi', ['dogbed']),
    furniture('dresser', 'wooden_dresser.glb', 'icons/obj/fluff/general.dmi', ['dresser'], awayFromWall=True),
    furniture('training_machine', 'training_machine.glb', 'icons/obj/machines/sec.dmi', ['training_machine']),
    *[bench(k) for k in ('middle', 'left', 'right', 'solo', 'corner')],
    # Shuttle furniture (shuttle_furniture/README.md).
    seat_row('sofa_middle', 'sofa_middle.glb', 'sofamiddle'),
    seat_row('sofa_left', 'sofa_left.glb', 'sofaend_left'),
    seat_row('sofa_right', 'sofa_right.glb', 'sofaend_right'),
    seat_row('sofa_corner', 'sofa_corner.glb', 'sofacorner', corner=True),
    seat_row('corp_sofa_middle', 'corporate_sofa_middle.glb', 'corp_sofamiddle'),
    seat_row('corp_sofa_left', 'corporate_sofa_left.glb', 'corp_sofaend_left'),
    seat_row('corp_sofa_right', 'corporate_sofa_right.glb', 'corp_sofaend_right'),
    seat_row('corp_sofa_corner', 'corporate_sofa_corner.glb', 'corp_sofacorner', corner=True),
    seat_row('pew_middle', 'wooden_pew_middle.glb', 'pewmiddle'),
    seat_row('pew_left', 'wooden_pew_left.glb', 'pewend_left'),
    seat_row('pew_right', 'wooden_pew_right.glb', 'pewend_right'),
    # chair.dm: the shuttle chair's restraint is down (_down_front / _down_behind) while someone is buckled, else up (_up)
    shuttle('shuttle_chair', 'shuttle_seat.glb', 'icons/obj/chairs.dmi', ['shuttle_chair'], clips={'open': 'open o:shuttle_chair_up', 'close': None}),
    # bookcase.dm: book-0 .. book-5 by how many books it holds, bookempty with no shelves yet; three rows of books. The map's random_bookcase (the port's
    # bookcase has no procs yet, so it keeps it) is drawn full.
    shuttle('bookcase', 'bookcase.glb', 'icons/obj/service/library.dmi', [f'book-{n}' for n in range(6)] + ['bookempty', 'random_bookcase'], awayFromWall=True,
            parts={'Books_Row_1': {'states': [f'book-{n}' for n in range(1, 6)] + ['random_bookcase']},
                   'Books_Row_2': {'states': ['book-3', 'book-4', 'book-5', 'random_bookcase']}, 'Books_Row_3': {'states': ['book-5', 'random_bookcase']}}),
    # shuttle_engines.dm: heater, propulsion, propulsion_l, propulsion_r; their dir is the thrust's
    shuttle('shuttle_heater', 'engine_heater.glb', 'icons/turf/shuttle.dmi', ['heater'], rotate=False),
    propulsion('shuttle_propulsion', 'propulsion_engine.glb', ['propulsion']),
    propulsion('shuttle_propulsion_left', 'propulsion_engine_left.glb', ['propulsion_l', 'burst_l']),
    propulsion('shuttle_propulsion_right', 'propulsion_engine_right.glb', ['propulsion_r', 'burst_r']),
    # airlock.dm: the titanium airlock (escape pods and the emergency shuttle); its leaves slide apart as tg's door animation opens it
    shuttle('titanium_airlock', 'escape_pod_airlock.glb', 'icons/obj/doors/airlocks/shuttle/shuttle.dmi', [], rule='airlock', fit={'width': 'tile', 'height': 'wall'},
            names={'Frame': 'frame', 'Door_Left': 'left', 'Door_Right': 'right'},
            parts={'Door_Left': {'slide': -1.3}, 'Door_Right': {'slide': 1.3}}),
]


extras = props('station_extras')
HATCH = {'maintenance_close': None}

SPECS += [
    # Station extras (station_extras/README.md). The gulag teleporter's model is left out: it shares implant_chair.dmi's implantchair states with the
    # skill station, whose model draws them.
    # suit_storage_unit.dm: industrial; _open while open (the shutter is up), _panel with its panel open; its lights are the live lamp's overlays
    extras('industrial_suit_storage', 'industrial_suit_storage.glb', 'icons/obj/machines/suit_storage.dmi', ['industrial'], awayFromWall=True,
           clips=dict(HATCH, open=None, close=None, maintenance_open='open o:industrial_panel'),
           parts={'Retracting_Shutter': {'exceptOverlays': ['industrial_open']}}),
    # drone_dispenser.dm: off / on / make / recharge on the live display
    extras('drone_dispenser', 'drone_shell_dispenser.glb', 'icons/obj/machines/drone_dispenser.dmi', ['off', 'on', 'make', 'recharge'], awayFromWall=True,
           clips=dict(HATCH, maintenance_open=None)),
    # scanner_gate.dm: scangate[_open]; its scan line (passive, scanning, alarm) across the arch
    extras('scanner_gate', 'weapon_scanner_gate.glb', 'icons/obj/machines/scangate.dmi', ['scangate', 'scangate_open'],
           clips=dict(HATCH, idle=None, scanning=['o:passive', 'o:scanning'], alarm='o:alarm', maintenance_open='open s:scangate_open')),
    # syndicatebomb.dm: "[initial(icon_state)][active ? -active : -inactive][open_panel ? -wires : ]" on the live display
    extras('training_bomb', 'training_bomb.glb', 'icons/obj/devices/assemblies.dmi',
           ['training-bomb'] + [f'training-bomb-{a}{w}' for a in ('active', 'inactive') for w in ('', '-wires')]),
    # gulag_item_reclaimer.dm: gulag_off with its screen overlay while powered; on the wall (its back at z = -0.24)
    extras('gulag_item_reclaimer', 'gulag_item_reclaimer.glb', 'icons/obj/machines/wallmounts.dmi', ['gulag_off'], rule='wall', offset=[0, -1.2, -0.755]),
    # monkeyrecycler.dm: grinder[_open]
    extras('monkey_recycler', 'monkey_recycler.glb', 'icons/obj/machines/kitchen.dmi', ['grinder', 'grinder_open'], awayFromWall=True,
           clips=dict(HATCH, working=None, maintenance_open='open s:grinder_open')),
    extras('basketball_hoop', 'basketball_hoop.glb', 'icons/obj/fluff/basketball_hoop.dmi', ['hoop'], awayFromWall=True),
    # mannequin.dm: "mannequin_[material]_[male|female]"
    extras('skeleton_mannequin', 'skeleton_model.glb', 'icons/mob/human/mannequin.dmi', ['mannequin_skeleton_male', 'mannequin_skeleton_female']),
    extras('quartermaster_statue', 'quartermaster_statue.glb', 'icons/obj/art/statue.dmi', ['qm']),
    extras('snowman', 'snowman.glb', 'icons/obj/art/statue.dmi', ['snowman']),
    # showcase.dm: the retired cloning pod (pod_0) and the horrific experiment's (pod_g); the old sleeper (sleeper-open); the TV, as the wooden TV console
    extras('cloning_pod_exhibit', 'cloning_pod_exhibit.glb', 'icons/obj/machines/cloning.dmi', ['pod_0', 'pod_g']),
    extras('old_sleeper_exhibit', 'damaged_cryo_exhibit.glb', 'icons/obj/machines/sleeper.dmi', ['sleeper-open']),
    extras('television', 'television_exhibit.glb', 'icons/obj/machines/computer.dmi', ['television'], awayFromWall=True),
    # fermenting_barrel.dm: barrel / barrel_open (the lid comes off)
    extras('fermenting_barrel', 'fermenting_barrel.glb', STRUCTURES, ['barrel', 'barrel_open'], clips={'open': 'open s:barrel_open', 'close': None}),
    # kitchen_spike.dm: the frame (spikeframe) and the spike with its hooks (spike, spikebloody*)
    extras('meat_spike', 'meat_spike.glb', 'icons/obj/service/kitchen.dmi',
           ['spikeframe', 'spike', 'spikebloodycorgi', 'spikebloodyalien', 'spikebloodymonkey', 'spikebloodybear'], awayFromWall=True,
           clips={'frame': None, 'assembled': ['s:spike', 's:spikebloodycorgi', 's:spikebloodyalien', 's:spikebloodymonkey', 's:spikebloodybear']}),
    extras('ripley_wreckage', 'ripley_wreckage.glb', 'icons/mob/rideables/mecha.dmi', ['ripley-broken']),
    # doppler_array.dm: tdoppler; _open with its panel open, _screen-off / -broken on the live monitor; it faces its dir
    extras('doppler_array', 'tachyon_doppler_array.glb', 'icons/obj/machines/research.dmi', ['tdoppler'],
           clips=dict(HATCH, maintenance_open='open o:tdoppler_open')),
    # ecto_sniffer.dm: ecto_sniffer[-p], _open with its panel open
    extras('ecto_sniffer', 'ectoscopic_sniffer.glb', 'icons/obj/machines/research.dmi', ['ecto_sniffer', 'ecto_sniffer-p', 'ecto_sniffer_open'],
           clips=dict(HATCH, maintenance_open='open s:ecto_sniffer_open')),
    # scanner_array.dm: scanner_off / _ready / _on / _open
    extras('exoscanner', 'exploration_scanner.glb', 'icons/obj/exploration.dmi', ['scanner_off', 'scanner_ready', 'scanner_on', 'scanner_open'],
           clips=dict(HATCH, maintenance_open='open s:scanner_open')),
    # fishing_portal_machine.dm: portal; portal_on and its destination while active, portal_open with its panel open
    extras('fishing_portal_generator', 'fishing_portal_generator.glb', 'icons/obj/fishing.dmi', ['portal'],
           clips=dict(HATCH, off=None, on='o:portal_on', maintenance_open='open o:portal_open')),
    # status_display.dm: the greenscreen camera, its camera_on / camera_off overlay on the live lamp
    extras('greenscreen_camera', 'broadcast_camera.glb', 'icons/obj/machines/stationary_camera.dmi', ['camera']),
    # _venue.dm: portal[-open]; portal_door while its venue is closed
    extras('restaurant_portal', 'restaurant_portal.glb', 'icons/obj/machines/restaurant_portal.dmi', ['portal', 'portal-open'], awayFromWall=True,
           clips=dict(HATCH, venue_open=None, venue_closed='o:portal_door', maintenance_open='open s:portal-open')),
]


device = wave('device_revision')
arcade_ore = props('arcade_ore')

SPECS += [
    # Device revision (device_revision/source_states.json): one mesh each on tg's atlases; their off, deny, open and overlay pictures are the live frames'.
    # scrubber.dm: "[initial(icon_state)]_[on]"; scrubber-open with a tank in, scrubber-connector on a port
    device('portable_air_scrubber', 'portable_air_scrubber.glb', ATMOS, ['scrubber_0', 'scrubber_1'], awayFromWall=True,
           parts={'Connector': {'overlays': ['scrubber-connector']}}),
    # intercom.dm (prison): intercom_prison, its icon_off intercom_prison-p
    on_wall('receive_only_intercom', 'receive_only_intercom.glb', 'icons/obj/machines/wallmounts.dmi', ['intercom_prison', 'intercom_prison-p'], back=-0.02,
            make=device),
    # pirate_shuttle_equipment.dm (piratepad, the civilian bounty pad's parent): lpad-open with its panel open, lpad while it sends, else lpad-off
    device('civilian_bounty_pad', 'civilian_bounty_pad.glb', 'icons/obj/machines/telepad.dmi', ['lpad-off', 'lpad', 'lpad-open']),
    # outlet.dm: outlet; its outlet-open flick plays on the live body, its doors swung open on the flick's open frames (5 to 7: the lights blink first)
    device('disposal_outlet', 'disposal_outlet.glb', 'icons/obj/pipes_n_cables/disposal.dmi', ['outlet'],
           clips={'Outlet_Open_Close': 'flick outlet-open'}, flickPose={'Outlet_Open_Close': [0, 0, 0, 0, 0, 1, 1, 0]}),
    # mining_order.dm, bitrunning/vendor.dm: order consoles without a screen or keyboard; mining-deny and vendor_off flicks on the live body
    device('mining_equipment_order_console', 'mining_equipment_order_console.glb', MINING, ['mining']),
    device('bitrunning_supplies_order_console', 'bitrunning_supplies_order_console.glb', BITRUNNING, ['vendor']),
    # _arcade.dm: the arcade body without a keyboard; its icon_screen (invaders, battle.dm's fighters) and _computer.dm's arcade_broken are drawn on the live
    # screen, none unpowered
    arcade_ore('arcade', 'arcade.glb', 'icons/obj/machines/computer.dmi', ['arcade'], part={'live': False},
               parts={'Screen': {'live': True, 'picture': 'appearance'}}),
    # mass_driver.dm: mass_driver, _o with its panel open (the rear hatch swings up); its launch flick, mass_driver1, plays on the live body and throws the
    # carriage out over its first six frames and back; its dir is the throw's
    wave('mass_driver')('mass_driver', 'mass_driver.glb', 'icons/obj/machines/floor.dmi', ['mass_driver', 'mass_driver_o'], seconds=0.5,
                        clips={'Service_Open_Close': 'open s:mass_driver_o', 'Launch': 'flick mass_driver1'},
                        flickPose={'Launch': [1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0]}),
    # bin.dm (delivery_chute): intake, turned by its dir (the side it takes things from); always open
    wave('parcel_receiver')('delivery_chute', 'parcel_receiver.glb', 'icons/obj/pipes_n_cables/disposal.dmi', ['intake']),
    # satchel_ore_box.dm: one orebox state
    arcade_ore('ore_box', 'ore_box.glb', 'icons/obj/mining.dmi', ['orebox']),
]


def variants(id, folder, icon, files, size, height, **more):
    """A model exported as one GLB per state (flat-coloured, no tg atlas): each file is drawn whole while its condition holds."""
    e = {'id': id}
    e.update(more)
    e.update({'icons': [icon], 'states': sorted({s for f in files.values() for s in f.get('states', [])}) or more.get('states', []),
              'rule': 'single', 'texture': 'model', 'flatColors': True, 'source': {'width': size, 'height': size},
              'fit': {'width': 'tile', 'height': height}})
    e['parts'] = {('model' if i == 0 else name): dict({'file': f'machines/{folder}/{name}.glb'}, **cond) for i, (name, cond) in enumerate(files.items())}
    return {'id': id, 'entry': e}


SUIT_LIGHTS = ['classic_open', 'classic_panel', 'classic_uvstrong', 'classic_uv', 'classic_locked']

SPECS += [
    # stasis.dm: stasis, _maintenance with its panel open, _broken; its mattress glow is a vis_contents overlay the port does not draw yet, so the
    # powered variant waits. 2.64 m long: drawn at 0.72 to lie inside its tile.
    variants('stasis', 'stasis', 'icons/obj/machines/stasis.dmi',
             {'stasis_v3_normal': {'states': ['stasis']}, 'stasis_v3_maintenance': {'states': ['stasis_maintenance']},
              'stasis_v3_broken': {'states': ['stasis_broken']}}, 2.78, 2.0),
    # suit_storage_unit.dm (classic): _open while open, _panel with its panel open, _uvstrong / _uv while it cleans, _locked; else shut. 2.28 m tall,
    # drawn at 0.83 under the 2 m ceiling. It replaces the atlas model (the owner, 2026-10-27).
    variants('suit_storage', 'suit_storage_classic', 'icons/obj/machines/suit_storage.dmi',
             {'suit_storage_v3_open': {'overlays': ['classic_open']},
              'suit_storage_v3_maintenance': {'overlays': ['classic_panel'], 'exceptOverlays': SUIT_LIGHTS[:1]},
              'suit_storage_v3_uvstrong': {'overlays': ['classic_uvstrong'], 'exceptOverlays': SUIT_LIGHTS[:2]},
              'suit_storage_v3_uv': {'overlays': ['classic_uv'], 'exceptOverlays': SUIT_LIGHTS[:3]},
              'suit_storage_v3_locked': {'overlays': ['classic_locked'], 'exceptOverlays': SUIT_LIGHTS[:4]},
              'suit_storage_v3_closed': {'exceptOverlays': SUIT_LIGHTS}}, 2.4, 1.8, states=['classic'], dir={'awayFromWall': True}),
]


piano_slots = props('piano_and_slots')

SPECS += [
    # Piano and slots (piano_and_slots/README.md). stationary.dm: piano, its broken_icon_state pianobroken; each its own file
    piano_slots('piano', 'piano.glb', 'icons/obj/art/musician.dmi', ['piano']),
    piano_slots('piano_broken', 'piano_broken.glb', 'icons/obj/art/musician.dmi', ['pianobroken']),
    # slotmachine.dm: slots, slots_broken (its own file, no screens); its icon_screen slots_screen, _working while it spins, drawn live on the three screens
    # and none unpowered (_computer.dm). The model's lever pull has no tg counterpart and is not played.
    piano_slots('slot_machine', 'slot_machine.glb', 'icons/obj/machines/computer.dmi', ['slots'], part={'live': False}, clips={'Lever_Pull': None},
                parts={n: {'live': True, 'picture': 'appearance', 'overlays': ['slots_screen', 'slots_screen_working']}
                       for n in ('Screen_Marquee', 'Screen_Reels', 'Screen_Lower_Display')}),
    piano_slots('slot_machine_broken', 'slot_machine_broken.glb', 'icons/obj/machines/computer.dmi', ['slots_broken']),
]

vents = wave('atmos_vents')
UNARY = 'icons/obj/machines/atmospherics/unary_devices.dmi'
# sunk into the floor to the holopad's height above it (0.055 m at 1.5 / 1.0 sunk 0.05725: 0.02525 m); the slab is 0.055 m at 1.2 / 0.9
VENT_SINK = [0, round(0.02525 - 0.055 * 1.2 / 0.9, 5), 0]

SPECS += [
    # Air vents and scrubbers (atmos_vents/README.md; the owner, 2026-10-29, back from 2D): one thin slab per state of update_icon_nopipes, turned by their dir.
    # vent_pump.dm: vent_welded, vent_off, vent_out / vent_in by its direction; its broken and overclocked overlays are the live picture's (the model's own
    # broken and overclocked files are vent_out's slab). vent_scrubber.dm: scrub_welded, scrub_off, scrub_purge, scrub_wide, scrub_on.
    *(vents(f'vent_pump_{s}', f'vent_pump_{s}.glb', UNARY, [f'vent_{s}'], rule='dir4', size=(0.9, 0.9), fit={'width': 'uniform', 'height': 1.2},
            offset=VENT_SINK)
      for s in ('off', 'out', 'in', 'welded')),
    *(vents(f'vent_scrubber_{s}', f'vent_scrubber_{s}.glb', UNARY, [f'scrub_{s}'], rule='dir4', size=(0.9, 0.9), fit={'width': 'uniform', 'height': 1.2},
            offset=VENT_SINK)
      for s in ('off', 'on', 'wide', 'purge', 'welded')),
]

foam = wave('foam_dispenser')
TOOL = 'icons/obj/devices/tool.dmi'
SPECS += [
    # The AI's foam dispenser (machinery/ai_slipper.dm, foam_dispenser/README.md): a floor plate at the sprite's size, its top at the holopad's height
    # (0.02525 m); ai-slipper0 without power, empty or cooling down, ai-slipper1 ready.
    foam('foam_dispenser', 'foam_dispenser.glb', TOOL, ['ai-slipper0'], offset=[0, round(0.02525 - 0.055, 5), 0]),
    # the ready file's six frame meshes step through its clip; the first one's frame is the live picture's, which plays all six
    foam('foam_dispenser_ready', 'foam_dispenser_ready.glb', TOOL, ['ai-slipper1'], offset=[0, round(0.02525 - 0.055, 5), 0],
         skip=[f'Foam_Ready_Frame_{n}' for n in range(1, 6)]),
]


equipment = props('station_equipment')
CHEM_TANKS = 'icons/obj/medical/chemical_tanks.dmi'
PANDEMIC_STATES = ['pandemic0', 'pandemic1', 'pandemic0_nopower', 'pandemic1_nopower', 'pandemic0_b', 'pandemic1_b']
MESSAGE_SERVER_STATES = ['message_server', 'message_server_off', 'message_server_o', 'message_server_o_off']
PUDDLE_STATES = ['puddle', 'puddle-splash', 'puddle-oil']
BEDSHEET_STATES = ['sheet' + c for c in ('white grey red orange yellow green blue purple brown black rainbow clown mime chap medical rd cmo hos ce hop captain ian NT '
                                         'centcom rev syndie cult wiz qm USA cosmos runtime pirate gondola').split()]


def conveyor_switch(state, clip):
    """conveyor.dm: switch-off, -fwd or -rev; the lever leans one way or the other, so each of the two is an entry of its own with that clip as its open pose."""
    clips = {c: None for c in ('off', 'forward', 'reverse')}
    if clip:
        clips[clip] = 'open s:' + state
    return {'id': state.replace('-', '_').replace('switch', 'conveyor_switch'), 'glb': 'machines/wall_wave/conveyor_switch.glb', 'family': 'conveyor_switch',
            'icons': ['icons/obj/machines/recycling.dmi'], 'states': [state] + (['conveyor_switch_oneway'] if state == 'switch-off' else []),  # the one-way switch's map icon
            'rotate': False, 'clips': clips,
            'offset': [0, -0.835, 0]}  # made at a wall device's height; tg's switch stands on the floor (the owner, 2026-11-08: it flew)


SPECS += [
    conveyor_switch('switch-off', None),
    conveyor_switch('switch-fwd', 'forward'),
    conveyor_switch('switch-rev', 'reverse'),
    # The station's equipment; 'numbered': a state's animation is exported as <state>_N and its frame 0 plays it as a live picture.
    # Two items drawn by their models on the floor (D323). bedsheet_bin.dm: the sheet's colours are its own states; the model's one mesh shows the thing's picture
    # (picture 'appearance': its live frame follows every state, whichever the sheet's colour)
    equipment('bedsheet', 'bedsheets.glb', 'icons/obj/bedsheets.dmi', BEDSHEET_STATES, part={'picture': 'appearance'}),
    # lib_codex_gigas.dm
    equipment('codex_gigas', 'codex_gigas.glb', 'icons/obj/service/library.dmi', ['demonomicon'], rotate=False, numbered=True, clips={'demonomicon': 's:demonomicon'}),
    # pipe_scrubber.dm: _active while on
    equipment('pipe_scrubber', 'pipe_scrubber.glb', 'icons/obj/pipes_n_cables/atmos.dmi', ['pipe_scrubber', 'pipe_scrubber_active'], rotate=False, numbered=True,
              clips={'pipe_scrubber': 's:pipe_scrubber', 'pipe_scrubber_active': 's:pipe_scrubber_active'}),
    # bed.dm: /obj/structure/bed/maint
    equipment('dirty_mattress', 'dirty_mattress.glb', 'icons/obj/bed.dmi', ['dirty_mattress'], rotate=False),
    # reagent_dispenser.dm: a keg's print is an overlay
    equipment('beer_keg', 'beer_keg.glb', CHEM_TANKS, ['keg'], rotate=False, parts={'Beer label': {'overlays': ['keg_beer']}}),
    equipment('cooking_oil_vat', 'cooking_oil_vat.glb', CHEM_TANKS, ['vat'], rotate=False),
    equipment('serving_dish', 'serving_dish.glb', 'icons/obj/service/kitchen.dmi', ['serving'], rotate=False),
    # tables_racks.dm: /obj/structure/rack/skeletal
    equipment('skeletal_minibar', 'skeletal_minibar.glb', 'icons/obj/fluff/general.dmi', ['minibar'], rotate=False),
    equipment('spirit_board', 'spirit_board.glb', STRUCTURES, ['spirit_board'], rotate=False),
    # machine_stacking.dm and the other mining consoles of this sheet
    equipment('stacking_machine_console', 'stacking_machine_console.glb', MINING, ['console'], awayFromWall=True, numbered=True, clips={'console': 's:console'}),
    # gizmo_machines.dm: tucker tubes, _on while it runs
    equipment('gizmo_tucker_tubes', 'gizmo.glb', 'icons/obj/science/gizmos.dmi', ['gizmo_active_0', 'gizmo_active_0_on'], rotate=False, numbered=True,
              clips={'gizmo_active_0': 's:gizmo_active_0', 'gizmo_active_0_on': 's:gizmo_active_0_on'}),
    # message_server.dm: telecomms' _o and _off, "message_server_calibrate" an overlay while it calibrates
    equipment('messaging_server', 'messaging_server.glb', TELECOMMS, MESSAGE_SERVER_STATES, awayFromWall=True, numbered=True, overlayFrames=True,
              clips=dict({s: 's:' + s for s in MESSAGE_SERVER_STATES}, message_server_calibrate='o:message_server_calibrate')),
    # pandemic.dm: pandemic[beaker][_b|_nopower], "waitlight" an overlay
    equipment('pandemic_2200', 'pandemic_2200.glb', 'icons/obj/medical/chemical.dmi', PANDEMIC_STATES, awayFromWall=True, numbered=True,
              clips=dict({s: 's:' + s for s in PANDEMIC_STATES}, waitlight='o:waitlight')),
    # lib_machines.dm: oldcomp, its screen an overlay; _computer.dm draws [icon_state]_broken over it
    equipment('library_console', 'library_console.glb', 'icons/obj/machines/computer.dmi', ['oldcomp'], numbered=True, overlayFrames=True,
              clips={'library': 'o:library', 'oldcomp_broken': 'o:oldcomp_broken'}),
    # water_source.dm: icon_state -splash while it is used; elephantgraveyard.dm's oil puddle flicks its splash on the live picture
    equipment('puddle', 'puddle.glb', 'icons/obj/watercloset.dmi', PUDDLE_STATES, rotate=False, numbered=True,
              clips=dict({s: 's:' + s for s in PUDDLE_STATES}, **{'puddle-oil-splash': None})),
]


def required(id, glb, icon, states, folder='machines', **more):
    """The required-models pass (docs/ai/required-models.md, docs/ai/model-work-progress.md)."""
    s = {'id': id, 'glb': (f'structures/tram/{glb}' if folder == 'structures' else f'{folder}/required_models/{glb}'), 'icons': [icon], 'states': states}
    s.update(more)
    return s


def back_of(glb):
    """The model's back (its least z, metres) from its glb's accessor bounds: a wall model's offset puts it on the wall's face."""
    import json, struct
    from pathlib import Path
    b = (Path(__file__).resolve().parents[2] / 'godot/assets/models' / glb).read_bytes()
    g = json.loads(b[20:20 + struct.unpack('<I', b[12:16])[0]])
    return min(g['accessors'][p['attributes']['POSITION']]['min'][2] for m in g['meshes'] for p in m['primitives'])


def tram_wall(id, glb, icon, states, mount=1.2, **more):
    """A tram machine made at its real height with its back on the wall: the wall rule's origin is 1 m before the face, at its type's mount height
    (PropKinds.MountHeight: 1.2 m, a button's 1.1 m); a state's later frames are the live picture's."""
    back = back_of('machines/required_models/' + glb)
    return required(id, glb, icon, states, rule='wall', offset=[0, -mount, round(-1.0 - back + 0.005, 3)], firstFrames=True, **more)


def tram_controls(state, glb, base):
    """tram_controls.dm: a tram control console on its wall; its screen (icon_screen) an overlay, not live: tram_broken passes for a variant of the body's
    tram_controls, and a live surface would draw the body's whole picture."""
    # update_display never shows the bare base screen (tram, tram_alt), whose name would match the body's own state: its plane is left out
    screens = [base + s for s in ('_1', '_2', '_3', '_01', '_02', '_04', '_08', '_NIS', '_broken')]
    return tram_wall(state, glb, 'icons/obj/machines/computer.dmi', [state], part={'live': False}, skip=[f'Status {base} 0'],
                     clips=dict({s: 'o:' + s for s in screens}, power_off=None, **{base: None}))


TRAM_CONTROLLERS = 'icons/obj/tram/tram_controllers.dmi'
CONTROLLER_LIGHTS = ['active', 'comms', 'doors', 'estop', 'fatal', 'fault', 'normal', 'power']


def tram_signs(prefix, glb, icon):
    """tram_displays.dm: a destination sign's or platform indicator's icon_state is its sign_face ([base]_NIS, [base]_tram_1[active][platform][dir]), else
    [base]_blank: one clip per state."""
    import json, struct
    from pathlib import Path
    b = (Path(__file__).resolve().parents[2] / 'godot/assets/models/machines/required_models' / glb).read_bytes()
    g = json.loads(b[20:20 + struct.unpack('<I', b[12:16])[0]])
    states = [a['name'] for a in g['animations']]
    return tram_wall(prefix, glb, icon, states, clips={s: 's:' + s for s in states})


LIMB_GROWER_STATES = ['limbgrower_idleoff', 'limbgrower_idleon', 'limbgrower_panelopen']
PARTICLE_ACCELERATOR = 'icons/obj/machines/particle_accelerator.dmi'
SPECS += [
    # bsa.dm: the three parts and the console turn by their dir
    required('bsa_generator', 'bsa_generator.glb', PARTICLE_ACCELERATOR, ['power_box']),
    required('bsa_bore', 'bsa_bore.glb', PARTICLE_ACCELERATOR, ['emitter_center']),
    required('bsa_fusor', 'bsa_fusor.glb', PARTICLE_ACCELERATOR, ['fuel_chamber']),
    required('bsa_control_console', 'bsa_control_console.glb', PARTICLE_ACCELERATOR, ['control_boxp']),
    # bsa.dm /obj/machinery/bsa/full: eleven tiles from its own tile, facing its dir (WEST: cannon_west, EAST: cannon_east); a single model turns by its dir as if
    # made facing south, and this one is made facing east, so it is turned back by the 270 degrees between them
    required('bsa_cannon', 'bsa_cannon.glb', 'icons/obj/machines/cannon.dmi', ['cannon_west', 'cannon_east'], rotate=False, turn=270),
    # dna_vault.dm: three by three tiles, its own the north middle one (the fillers hold the rest); frame 0 of its readouts plays vault's pulse
    required('dna_vault', 'dna_vault.glb', 'icons/obj/machines/dna_vault.dmi', ['vault'], rotate=False, offset=[0, 0, 2.0], clips={'Vault pulse': None},
             skip=[f'DNA readout pulse ({side}, {i})' for side in (-1, 1) for i in range(1, 7)]),
    # satellite.dm: sat_[active|inactive]
    # shortcut: the Inactive clip lays the satellite on its side by its root node, which no catalog pose can do yet: it stands upright with its off light
    required('meteor_shield_satellite', 'meteor_shield_satellite.glb', 'icons/obj/machines/satellite.dmi', ['sat_inactive', 'sat_active'], rotate=False,
             clips={'Active': 's:sat_active', 'Inactive': 's:sat_inactive'}),
    # limbgrower.dm: limbgrower_idleon while busy, _panelopen, else _idleoff; its fill and unfill flicks play on the live display. A display's later frames
    # are the live animation's
    required('limb_grower', 'limb_grower.glb', 'icons/obj/machines/limbgrower.dmi', LIMB_GROWER_STATES, awayFromWall=True,
             clips=dict({s: 's:' + s for s in LIMB_GROWER_STATES}, limbgrower_fill=None, limbgrower_unfill=None),
             skip=[f'Display {s} {i}' for s in LIMB_GROWER_STATES + ['limbgrower_fill', 'limbgrower_unfill'] for i in range(1, 10)]),
    # tram_controls.dm: on a window (tram_controls) or standalone (tram_alt_controls), a wall fixture; its screen (icon_screen) an overlay: the platform it goes
    # to, the direction it travels, NIS or broken. A screen's later frames are the live animation's
    # tram_doors.dm: the tram's two-tile airlock (multi_tile, bound_width 64): its own tile and the filler's beside it, so it stands a tile's half (1 m) along
    # its width; its leaves slide 1.87 m apart into the wall. tg's light, panel, weld and seal overlays (tram-overlays.dmi) show their planes, front and rear
    required('tram_door', 'tram_door.glb', 'icons/obj/doors/airlocks/tram/tram.dmi', [], folder='structures', rule='airlock', fit={'width': 'tile', 'height': 'wall'},
             offset=[1.0, 0, 0], multiTile=True, overlayFrames=True, firstFrames=True, names={'Frame and guide rails': 'frame'},
             parts=dict({n: {'slide': -1.87} for n in ['Left sliding leaf', 'Left glass 0', 'Left glass 0 pixel lattice', 'Left glass 1', 'Left glass 1 pixel lattice']},
                        **{n: {'slide': 1.87} for n in ['Right sliding leaf', 'Right glass 0', 'Right glass 0 pixel lattice', 'Right glass 1', 'Right glass 1 pixel lattice']}),
             # the opening and closing clips also move the leaves: their lights are tied to their overlays here
             only={f'Status lights_{s} 0{r}': {'overlays': [f'lights_{s}']} for s in ('opening', 'closing') for r in ('', ' rear')},
             clips={s: 'o:' + s for s in ['lights_bolts', 'lights_denied', 'lights_emergency', 'panel_closed', 'panel_open', 'panel_opening', 'panel_closing',
                                          'panel_closed_protected', 'panel_open_protected', 'panel_opening_protected', 'panel_closing_protected', 'sealed',
                                          'welded', 'sparks_broken', 'sparks_damaged', 'sparks_open']}),
    tram_controls('tram_controls', 'tram_control_window.glb', 'tram'),
    tram_controls('tram_alt_controls', 'tram_control_standalone.glb', 'tram_alt'),
    # tram_machinery.dm /obj/machinery/button/transport/tram (and the elevator's call button, the same sprite): [skin], -nopower, -open with its panel open;
    # its success and error lights overlays. The hinged cover swings open with the panel, which shows the well with the installed device or board (their
    # overlays)
    tram_wall('tram_call_button', 'tram_call_button.glb', 'icons/obj/machines/wallmounts.dmi', ['tram', 'tram-nopower', 'tram-open'], mount=1.1,
              clips={'tram': 's:tram', 'tram-nopower': 's:tram-nopower', 'tram-overlay-success': 'o:tram-overlay-success', 'tram-overlay-error': 'o:tram-overlay-error',
                     'panel_open': 'open s:tram-open', 'panel_open_board': None, 'panel_empty': None}, seconds=0.3,
              only={'Internal tram-overlay-device': {'states': ['tram-open'], 'overlays': ['tram-overlay-device']},
                    'Internal tram-overlay-board': {'states': ['tram-open'], 'overlays': ['tram-overlay-board']}}),
    tram_signs('tram_destination_sign', 'tram_destination_sign.glb', 'icons/obj/tram/tram_display.dmi'),
    tram_signs('tram_platform_indicator', 'tram_platform_indicator.glb', 'icons/obj/tram/tram_indicator.dmi'),
    # tram_controller.dm: [base]-closed / -locked / -open the cabinet's door, and a light overlay per status (power, normal, doors, active, comms, estop, fault,
    # fatal); the door swings open while tg shows it open
    tram_wall('tram_local_controller', 'tram_local_controller.glb', TRAM_CONTROLLERS, ['tram-controller'], overlayFrames=True,
              clips=dict({f'tram-{l}': f'o:tram-{l}' for l in CONTROLLER_LIGHTS}, **{f'tram-{l}_off': None for l in CONTROLLER_LIGHTS},
                         locked='o:tram-locked', unlocked=None, cover_open='open o:tram-open', cover_closed=None), seconds=0.3),
    # the central (tcomms) controller: a floor unit with no cover, base home
    required('tram_central_controller', 'tram_central_controller.glb', TRAM_CONTROLLERS, ['home-controller'], awayFromWall=True, overlayFrames=True,
             firstFrames=True, clips=dict({f'home-{l}': f'o:home-{l}' for l in CONTROLLER_LIGHTS}, **{f'home-{l}_off': None for l in CONTROLLER_LIGHTS})),
    # tram_power.dm: rec-power-0 unpowered, else rec-power-1 and rec-active-[is_active]; each plane named [overlay][frame]
    tram_wall('tram_power_rectifier', 'tram_power_rectifier.glb', TRAM_CONTROLLERS, ['rectifier'], overlayFrames=True,
              clips={'unpowered': None, 'idle': None, 'active': None},
              only={'Status rec-power-00': {'overlays': ['rec-power-0']}, 'Status rec-power-10': {'overlays': ['rec-power-1']},
                    'Status rec-active-00': {'overlays': ['rec-active-0']}, 'Status rec-active-10': {'overlays': ['rec-active-1']}},
              skip=['Status rec-power-01', 'Status rec-power-02', 'Status rec-power-03', 'Status rec-active-01', 'Status rec-active-02', 'Status rec-active-03',
                    'Status rec-active-04', 'Status rec-active-11']),
    # tram_signals.dm: crossing-inbound / -outbound (one assembly), its [base]-l[signal_state] light and [base]-s[operating_status] status overlays; it
    # stands on its tile, facing its dir
    required('tram_crossing_signal', 'tram_crossing_signal.glb', 'icons/obj/tram/crossing_signal.dmi', ['crossing-inbound', 'crossing-outbound'],
             overlayFrames=True, firstFrames=True,
             clips=dict({f'aspect_l{i}': [f'o:crossing-inbound-l{i}', f'o:crossing-outbound-l{i}'] for i in range(4)},
                        **{f'status_s{i}': [f'o:crossing-inbound-s{i}', f'o:crossing-outbound-s{i}'] for i in (0, 1, 3, 4)},
                        inbound=None, outbound=None, aspect_off=None, status_off=None)),
    # tram_structures.dm /obj/structure/tram/spoiler: the car's cut corner (made as its south frame, the north-east half), turned by its dir; deployed (and
    # welded, which only a deployed one is) the carriage slides out over tg's deploying flick; the weld seam while welded
    required('tram_spoiler', 'tram_spoiler.glb', 'icons/obj/tram/tram_structure.dmi',
             ['tram-spoiler-retracted', 'tram-spoiler-deployed', 'tram-spoiler-deploying', 'tram-spoiler-retracting', 'tram-spoiler-welded'], folder='structures',
             fit={'width': 'tile', 'height': 'wall'}, firstFrames=True,
             clips={'deploying': 'open s:tram-spoiler-deployed|s:tram-spoiler-welded', 'retracting': None, 'retracted': None, 'deployed': None,
                    'welded': 's:tram-spoiler-welded', 'unwelded': None},
             # the blade's first frame while retracted, its last (11) out while deployed; the carriage slides between them
             only={'Exposed sliding blade 00': {'exceptStates': ['tram-spoiler-deployed', 'tram-spoiler-welded']},
                   'Exposed sliding blade 11': {'states': ['tram-spoiler-deployed', 'tram-spoiler-welded']}}),
    # grill.dm: grill_open (empty, no fuel), grill_on (burning, 9 frames), grill (something on it); the model rests with its lid up and shuts it over
    # what grills. Coal 0 is the unlit grate, coal 1 the first of grill_on's frames (its live picture plays the rest)
    required('barbeque_grill', 'barbeque_grill.glb', 'icons/obj/machines/kitchen.dmi', ['grill_open', 'grill_on', 'grill'], awayFromWall=True,
             firstFrames=True, closedState='grill_open', seconds=0.3,
             clips={'grill': 'open s:grill', 'grill_open': None, 'grill_on': None},
             only={'Grill coal 0': {'exceptStates': ['grill_on']}, 'Grill coal 1': {'states': ['grill_on']}}),
    # smartfridge.dm /obj/machinery/smartfridge/disks: disktoaster, -broken; the -panel overlay with its panel open (the cover swings on its hinge); its
    # -powered overlay is never drawn (has_emissive FALSE)
    required('disk_compartmentalizer', 'disk_compartmentalizer.glb', 'icons/obj/machines/smartfridge.dmi', ['disktoaster', 'disktoaster-broken'],
             awayFromWall=True, seconds=0.3,
             clips={'panel_open': 'open o:disktoaster-panel', 'panel_closed': None, 'unpowered': 's:disktoaster', 'broken': 's:disktoaster-broken',
                    'powered': None}),
    # smartfridge.dm /obj/machinery/smartfridge/drying/rack: drying-rack, its -filled overlay while it holds anything and -drying (6 frames) while it
    # dries; the heater's first frame is always there, its overlay's frames play on it
    required('drying_rack', 'drying_rack.glb', 'icons/obj/machines/smartfridge.dmi', ['drying-rack'], awayFromWall=True, firstFrames=True,
             overlayFrames=True, clips={'empty': None, 'filled': None, 'drying_off': None, 'drying': None},
             only={'Contents shelf 0': {'overlays': ['drying-rack-filled']}, 'Contents shelf 1': {'overlays': ['drying-rack-filled']},
                   'Heater frame 0': {}}),
    # dance_machine.dm: [base] and [base]-active while a song plays (its frames the live picture's); -broken is in the sheet, never set
    required('jukebox', 'jukebox.glb', 'icons/obj/machines/music.dmi', ['jukebox', 'jukebox-active', 'jukebox-broken'], awayFromWall=True,
             firstFrames=True, clips={'idle': 's:jukebox', 'active': 's:jukebox-active', 'broken': 's:jukebox-broken'}),
    # the radiant dance machine (/obj/machinery/jukebox/disco): disco, disco-active (12 frames: the mirror globe and the indicators)
    required('radiant_dance_machine', 'radiant_dance_machine.glb', 'icons/obj/machines/music.dmi', ['disco', 'disco-active', 'disco-broken'],
             awayFromWall=True, firstFrames=True, clips={'idle': 's:disco', 'active': 's:disco-active', 'broken': 's:disco-broken'},
             only={'State indicators 0': {'states': ['disco']}, 'State indicators 1': {'states': ['disco-active']},
                   'State indicators 13': {'states': ['disco-broken']}}),
]

SPECS += [
    # Finished models that were made but never catalogued (docs/ai/asset-audit-2026-11-22.md), each matched to the tg sprite its texture samples.
    # syndicatebeacon.dm: the syndicate's singularity beacon, "[icontype][active]" with icontype beaconsynd
    {'id': 'syndicate_singularity_beacon', 'glb': 'machines/syndicate/singularity_beacon.glb',
     'icons': ['icons/obj/machines/engine/singularity.dmi'], 'states': ['beaconsynd0', 'beaconsynd1'],
     'clips': {'Beacon active': None, 'Beacon off': None}},
    # portable_turret.dm /obj/machinery/porta_turret/syndicate: always up, no cover; "[base_icon_state]_off|_stun|_lethal|_broken" with base syndie
    {'id': 'syndicate_turret', 'glb': 'machines/syndicate/syndicate_ballistic_turret.glb',
     'icons': ['icons/obj/weapons/turrets.dmi'], 'states': ['syndie_off', 'syndie_stun', 'syndie_lethal', 'syndie_broken'],
     'clips': {'Broken': 's:syndie_broken', 'Operational': ['s:syndie_off', 's:syndie_stun', 's:syndie_lethal'],
               'Off': 's:syndie_off', 'Stun': 's:syndie_stun', 'Lethal': 's:syndie_lethal'}},
    # sleepers.dm /obj/machinery/sleeper/syndie: "[base_icon_state][state_open ? -open : panel_open ? -o : ]" with base sleeper_s; the canopy opens with -open.
    # tg draws no occupant: the readout clips are not used.
    {'id': 'syndicate_sleeper', 'glb': 'machines/syndicate/syndicate_medical_sleeper.glb',
     'icons': ['icons/obj/machines/sleeper.dmi'], 'states': ['sleeper_s', 'sleeper_s-open', 'sleeper_s-o'],
     'clips': {'Open canopy': 'open s:sleeper_s-open', 'Close canopy': None, 'Occupied': None, 'Empty': None}},
    # pinatas.dm /obj/structure/pinata/syndie: pinata_syndie_placed, _damaged below half integrity; a hit flicks "[icon_state]_swing"
    {'id': 'syndicate_pinata', 'glb': 'structures/syndicate/syndicate_corgi_pinata.glb',
     'icons': ['icons/obj/toys/toy.dmi'], 'states': ['pinata_syndie_placed', 'pinata_syndie_placed_damaged'],
     'clips': {'Swing': 'flick pinata_syndie_placed_swing|pinata_syndie_placed_damaged_swing', 'Intact': None, 'Damaged': None}},
    # containers.dm /obj/structure/shipping_container/interdyne: a 96 x 32 sprite from its tile east (bound_width 96), so the model is offset a tile east
    {'id': 'interdyne_shipping_container', 'glb': 'structures/syndicate/syndicate_shipping_container.glb', 'rotate': False, 'offset': [2.0, 0, 0],
     'icons': ['icons/obj/fluff/containers.dmi'], 'states': ['interdyne'],
     'clips': {'Refrigerated': None, 'Dry freight': None}},
    # statues.dm /obj/structure/statue/uranium/nuke
    {'id': 'uranium_nuke_statue', 'glb': 'structures/syndicate/uranium_nuclear_statue.glb',
     'icons': ['icons/obj/art/statue.dmi'], 'states': ['nuke']},
    # flatpacker.dm: flatpacker, _o with its panel open; the inserted board is the overlay flatpacker_c
    production('flatpacker', 'flatpacker.glb', 'icons/obj/machines/lathes.dmi', ['flatpacker', 'flatpacker_o'], awayFromWall=True,
               clips={'board_inserted': 'o:flatpacker_c', 'board_removed': None, 'maintenance_open': 'open s:flatpacker_o', 'maintenance_close': None,
                      'material_load': None, 'production_progress': None}),
    # flatpacks.dm /obj/structure/flatpack_cart: flatcart with a flatcart_flat overlay per loaded flatpack; the first stowed package shows while one is loaded
    {'id': 'flatpack_cart', 'glb': 'structures/flatpack_cart/flatpack_cart.glb',
     'icons': ['icons/obj/structures.dmi'], 'states': ['flatcart'],
     'clips': {'load_1': 'o:flatcart_flat', 'load_0': None, 'load_2': None, 'load_3': None}},
    # stationary.dm /obj/structure/musician/piano/minimoog: minimoog, minimoogbroken
    piano_slots('minimoog', 'minimoog.glb', 'icons/obj/art/musician.dmi', ['minimoog']),
    piano_slots('minimoog_broken', 'minimoog_broken.glb', 'icons/obj/art/musician.dmi', ['minimoogbroken']),
    # harvester.dm update_icon_state: -o with its panel open, -open while open, -charging warming up, -active harvesting, else harvester.
    # The lid opens with -open; the service cover is a second mover, not drawn with the panel open.
    {'id': 'organ_harvester', 'glb': 'machines/required_models/organ_harvester.glb', 'awayFromWall': True,
     'icons': ['icons/obj/machines/harvester.dmi'], 'states': ['harvester', 'harvester-open', 'harvester-charging', 'harvester-active', 'harvester-o'],
     'clips': {'harvester': ['s:harvester', 's:harvester-open', 's:harvester-o'], 'harvester-open': 'open s:harvester-open',
               'harvester-charging': 's:harvester-charging', 'harvester-active': 's:harvester-active', 'harvester-o': 'hide s:harvester-o'}},
]

# The owner's fits (2026-10-29): each drawn whole at its height (uniform), not stretched over the tile
OWNER_HEIGHTS = {'computer_frame': 1.7, 'machine_frame': 1.6, 'modular_console': 1.7, 'tank_dispenser': 1.5, 'thermomachine': 1.5, 'disposal_outlet': 1.8,
                 'mining_equipment_order_console': 1.7, 'bitrunning_supplies_order_console': 1.7, 'arcade': 1.8, 'ore_box': 1.8, 'stasis': 1.9,
                 'piano': 1.8, 'piano_broken': 1.8, 'slot_machine': 1.8, 'slot_machine_broken': 1.8,
                 'emitter': 1.435}
for s in SPECS:
    if s['id'] in OWNER_HEIGHTS:
        (s['entry'] if 'entry' in s else s)['fit'] = {'width': 'uniform', 'height': OWNER_HEIGHTS[s['id']]}
def multilevel(id, glb, icon, states, **more):
    s = {'id': id, 'glb': 'structures/multilevel/' + glb, 'icons': [icon], 'states': states}
    s.update(more)
    return s


SPECS += [
    # Multi-deck wall devices (D325, structures/multilevel). The static multi-deck models (ladders, risers, the tram's) are hand-written entries.
    # buttons.dm: the elevator button (elev_controller.dm) is a button of skin "tram", the tram call button's sprite: tram_call_button draws both
    # elev_panel.dm: the elevator's control panel, elevpanel0 or elevpanel1
    on_wall('elevator_control_panel', 'elevator_control_panel.glb', 'icons/obj/wallmounts.dmi', ['elevpanel0', 'elevpanel1'], back=-0.07, make=multilevel,
            clips={'elevpanel0': 's:elevpanel0', 'elevpanel1': 's:elevpanel1'}),
]


# The owner's scale tweaks from the dev Tools tab (cf83aa1e7, 2026-11-06), kept across regenerations
OWNER_TWEAKS = {'reflector_single': {'scale': 0.898}, 'reflector_double': {'scale': 0.898}, 'reflector_box': {'scale': 0.898},
                'thermomachine': {'scale': 1.545, 'offset': [0, -0.057, 0.308]}, 'suit_storage': {'scale': 0.976}, 'television': {'scale': 0.5}, 'library_console': {'scale': 0.6}}
for s in SPECS:
    (s['entry'] if 'entry' in s else s).update(OWNER_TWEAKS.get(s['id'], {}))
