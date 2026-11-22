# Asset audit, 2026-11-22

What under `godot/assets/models/` the game uses. The client loads only `godot/assets/models/models.json`; the `models.json` files in subfolders are model notes, not catalogs. A `.glb` is used when that catalog names it.

**Done the same day:** the replaced models (with their `.blend` sources and, for thermomachine, gas pumps and canister connectors, their whole folders), the preview and review scenes, the removable libraries and six scratch blends (`modular_table_examples`, `shuttle_furniture_checks`, `station_props_pose_checks`, the old `stationary_tanks`, `thermomachine_v1`, `transit_tubes_assembly`) were deleted, and the model READMEs no longer name them. Kept on purpose: `body_container_shared`, `chem_master_shared` and `vending_catalog` (unclear role), `suit_storage_classic.glb` (a test fixture), the unwired finished models and the extra state presets. The audit JSONs in `docs/ai/art/` still name deleted files: they are records of that time.

## Unused GLBs (100, 5.7 MB)

### Replaced or abandoned (39)

- `furniture/metal_locker/metal_locker.glb`: replaced by the atlas lockers
- `furniture/metal_table/metal_table.glb`: replaced by the table kits
- `items/c4_charge/c4_charge_active.glb`: superseded preset
- `items/c4_charge/c4_charge_active_bright.glb`: superseded preset
- `items/c4_charge/c4_charge_legacy_green.glb`: old colour variant
- `items/c4_charge/c4_charge_legacy_white.glb`: old colour variant
- `machines/canister_connector/canister_connector_cap.glb`: connectors stay 2D (owner decision)
- `machines/canister_connector/canister_connector_floor.glb`: connectors stay 2D (owner decision)
- `machines/canister_connector/canister_connector_pipe.glb`: connectors stay 2D (owner decision)
- `machines/gas_pumps/pressure_gas_pump_off.glb`: gas pumps stay 2D (owner decision)
- `machines/gas_pumps/pressure_gas_pump_on.glb`: gas pumps stay 2D (owner decision)
- `machines/gas_pumps/volume_gas_pump_off.glb`: gas pumps stay 2D (owner decision)
- `machines/gas_pumps/volume_gas_pump_on.glb`: gas pumps stay 2D (owner decision)
- `machines/gas_pumps/volume_gas_pump_overclocked.glb`: gas pumps stay 2D (owner decision)
- `machines/gravity_and_tanks/gravity_generator.glb`: replaced by gravity_generator_fix*.glb
- `machines/gravity_and_tanks/gravity_generator_low_profile.glb`: replaced by gravity_generator_fix*.glb
- `machines/gravity_and_tanks/gravity_peripheral_module.glb`: replaced by gravity_generator_fix*.glb
- `machines/stasis/stasis_v3_powered.glb`: superseded preset
- `machines/thermomachine/thermomachine_cooling.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `machines/thermomachine/thermomachine_heating.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `machines/thermomachine/thermomachine_maintenance.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `machines/thermomachine/thermomachine_network_pipe.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `machines/thermomachine/thermomachine_off.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `machines/thermomachine/thermomachine_on.glb`: replaced by gravity_and_tanks/temperature_control_unit.glb
- `station_props/machines/suit_storage_classic.glb`: KEEP: fixture of ModelTests (the game draws the v3 unit)
- `station_props/stationary_tanks/stationary_tank_center.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `station_props/stationary_tanks/stationary_tank_corner.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `station_props/stationary_tanks/stationary_tank_end.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `station_props/stationary_tanks/stationary_tank_single.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `station_props/stationary_tanks/stationary_tank_straight.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `station_props/stationary_tanks/stationary_tank_tee.glb`: replaced by stationary_tanks/rounded/pressure_*.glb
- `structures/floor_plating/floor_plating.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_corner.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_edge.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_end.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_isolated.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_simple.glb`: not drawn: floors are built by the map renderer
- `structures/floor_plating/floor_plating_strip.glb`: not drawn: floors are built by the map renderer
- `structures/multilevel/atmos_multideck_adapter.glb`: duplicate of structures/transport/atmos_multideck_adapter.glb (used)

### Finished models never added to the catalog (31): candidates to wire in, not trash

**Wired in the same day** (`tools/art/catalog_wave_specs.py`, entries appended to `models.json`): the syndicate singularity beacon, turret, sleeper and corgi pinata, the Interdyne shipping container, the uranium nuke statue, the flatpacker, the flatpack cart, the minimoog (and its broken state) and the organ harvester. Left out on purpose: the items (flatpack, petri dish, replacement bulbs and tubes, the solar, flash and button frames: items keep their picture), the supply pod (its hatch opens when tg's door overlay goes away, which the open channel cannot express yet), the plastic flaps variants (tg draws them like the standard flaps), and the models another entry draws already (the detective board, the airlock assembly, the gulag teleporter, the elevator call button, the escape shuttle airlock).

- `items/flatpack/flatpack.glb`
- `items/petri_dish/petri_dish.glb`
- `machines/computers_construction/airlock_assembly.glb`
- `machines/door_button/door_button_assembly.glb`
- `machines/lighting/replacement_bulb.glb`
- `machines/lighting/replacement_bulb_broken.glb`
- `machines/lighting/replacement_bulb_burned.glb`
- `machines/lighting/replacement_tube.glb`
- `machines/lighting/replacement_tube_broken.glb`
- `machines/lighting/replacement_tube_burned.glb`
- `machines/mounted_flash/mounted_flash_frame.glb`
- `machines/production_wave/flatpacker.glb`
- `machines/required_models/organ_harvester.glb`
- `machines/syndicate/singularity_beacon.glb`
- `machines/syndicate/syndicate_ballistic_turret.glb`
- `machines/syndicate/syndicate_medical_sleeper.glb`
- `machines/wall_wave/detective_board.glb`
- `station_props/piano_and_slots/minimoog.glb`
- `station_props/piano_and_slots/minimoog_broken.glb`
- `station_props/power/solar_panel_frame.glb`
- `station_props/power/solar_tracker_frame.glb`
- `station_props/shuttle_furniture/escape_shuttle_airlock.glb`
- `station_props/station_extras/gulag_teleporter.glb`
- `structures/flatpack_cart/flatpack_cart.glb`
- `structures/multilevel/elevator_call_button.glb`
- `structures/syndicate/syndicate_corgi_pinata.glb`
- `structures/syndicate/syndicate_shipping_container.glb`
- `structures/syndicate/syndicate_supply_pod.glb`
- `structures/syndicate/uranium_nuclear_statue.glb`
- `structures/transport/plastic_flaps_kitchen.glb`
- `structures/transport/plastic_flaps_opaque.glb`

### Extra state presets the catalog does not use (30): the catalog draws these states from tg frames or nodes of the main model; check per model before deleting

- `furniture/emergency_tool_cabinet/fire_axe_cabinet_broken_open.glb`
- `furniture/emergency_tool_cabinet/fire_axe_cabinet_open.glb`
- `furniture/emergency_tool_cabinet/fire_axe_cabinet_open_empty.glb`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_broken_open.glb`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_open.glb`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_open_empty.glb`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_broken_open.glb`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_open.glb`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_open_empty.glb`
- `machines/access_button/access_button_cycle_red.glb`
- `machines/airlock_controller/airlock_controller_process_1.glb`
- `machines/airlock_pressure_controller/airlock_pressure_controller_process_1.glb`
- `machines/airlock_sensor/airlock_sensor_alert_1.glb`
- `machines/airlock_sensor/airlock_sensor_alert_2.glb`
- `machines/airlock_sensor/airlock_sensor_cycle_1.glb`
- `machines/atmos_vents/vent_pump_broken.glb`
- `machines/atmos_vents/vent_pump_overclocked.glb`
- `machines/defib_mount/defib_mount_penlite_emagged_1.glb`
- `machines/defib_mount/defib_mount_standard_emagged_1.glb`
- `machines/fax/fax_idle_alternate.glb`
- `machines/mounted_flash/mounted_flash_ready_alternate.glb`
- `machines/status_display/status_display_text_ready.glb`
- `machines/turret_controller/turret_controller_kill_1.glb`
- `machines/turret_controller/turret_controller_standby_1.glb`
- `machines/turret_controller/turret_controller_stun_1.glb`
- `machines/wall_healer/wall_healer_mixed.glb`
- `machines/wall_healer/wall_healer_off.glb`
- `machines/weapon_recharger/weapon_recharger_fail.glb`
- `station_props/station_equipment/gizmo_active_1.glb`
- `structures/wall_safe/wall_safe_open.glb`

## Preview and review scenes: 90 removable (19.7 MB), 0 to keep

- `doors/additional_doors_preview.blend`
- `doors/external/external_airlock_preview.blend`
- `doors/hatch/vault_hatch_preview.blend`
- `doors/public_glass/public_glass_airlock_preview.blend`
- `furniture/chair_family/chair_family_preview.blend`
- `furniture/emergency_tool_cabinet/emergency_tool_cabinet_preview.blend`
- `furniture/metal_locker/metal_locker_showcase.blend`
- `furniture/metal_locker/storage_complete_showcase.blend`
- `furniture/notice_board/notice_board_preview.blend`
- `furniture/storage_rack/storage_rack_preview.blend`
- `furniture/table_kits/table_seams_verification.blend`
- `items/c4_charge/c4_charge_preview.blend`
- `machines/access_button/access_button_preview.blend`
- `machines/air_alarm/air_alarm_preview.blend`
- `machines/airlock_controller/airlock_controller_preview.blend`
- `machines/airlock_pressure_controller/airlock_pressure_controller_preview.blend`
- `machines/airlock_sensor/airlock_sensor_preview.blend`
- `machines/apc/apc_preview.blend`
- `machines/atmos_vents/atmos_vents_preview.blend`
- `machines/atmosphere_wave/atmosphere_review.blend`
- `machines/bar_sign/bar_sign_preview.blend`
- `machines/bar_sign/bar_sign_remaining_1_preview.blend`
- `machines/bar_sign/bar_sign_remaining_2_preview.blend`
- `machines/bar_sign/bar_sign_remaining_3_preview.blend`
- `machines/bar_sign/bar_sign_shaped_preview.blend`
- `machines/canister_connector/canister_connector_preview.blend`
- `machines/cell_charger/cell_charger_preview.blend`
- `machines/communications_wave/communications_review.blend`
- `machines/computers_construction/computers_construction_review.blend`
- `machines/consoles/console_sides_preview.blend`
- `machines/consoles/space_consoles_preview.blend`
- `machines/defib_mount/defib_mount_preview.blend`
- `machines/device_revision/device_revision_preview.blend`
- `machines/digital_clock/digital_clock_preview.blend`
- `machines/door_button/door_button_preview.blend`
- `machines/engineering_wave/engineering_review.blend`
- `machines/entertainment_monitor/entertainment_monitor_preview.blend`
- `machines/fax/fax_preview.blend`
- `machines/fire_alarm/fire_alarm_preview.blend`
- `machines/gas_pumps/gas_pumps_preview.blend`
- `machines/gravity_and_tanks/gravity_generator_low_profile_review.blend`
- `machines/gravity_and_tanks/gravity_states_review.blend`
- `machines/gravity_and_tanks/machinery_review.blend`
- `machines/keycard_auth/keycard_auth_preview.blend`
- `machines/light_construction/light_construction_preview.blend`
- `machines/light_switch/light_switch_preview.blend`
- `machines/lighting/lighting_assembly_preview.blend`
- `machines/mass_driver/mass_driver_preview.blend`
- `machines/medical_wave/medical_review.blend`
- `machines/microwave/microwave_preview.blend`
- `machines/mounted_flash/mounted_flash_preview.blend`
- `machines/mounted_igniter/mounted_igniter_preview.blend`
- `machines/newscaster/newscaster_preview.blend`
- `machines/parcel_receiver/parcel_receiver_review.blend`
- `machines/pepper_refiller/pepper_refiller_preview.blend`
- `machines/production_wave/production_review.blend`
- `machines/production_wave/production_service_review.blend`
- `machines/reagent_grinder/reagent_grinder_preview.blend`
- `machines/requests_console/requests_console_preview.blend`
- `machines/resources_wave/resources_review.blend`
- `machines/science_wave/science_review.blend`
- `machines/security_camera/security_camera_preview.blend`
- `machines/service_wave/service_review.blend`
- `machines/station_intercom/station_intercom_preview.blend`
- `machines/status_display/status_display_preview.blend`
- `machines/telescreen/telescreen_preview.blend`
- `machines/transport_wave/transport_review.blend`
- `machines/turret_controller/turret_controller_preview.blend`
- `machines/wall_healer/wall_healer_preview.blend`
- `machines/wall_wave/wall_review.blend`
- `machines/weapon_recharger/weapon_recharger_preview.blend`
- `station_props/arcade_ore/preview.blend`
- `station_props/bathroom/urinal_preview.blend`
- `station_props/furniture_wave/furniture_review.blend`
- `station_props/furniture_wave/furniture_states_review.blend`
- `station_props/machinery_and_tanks_preview.blend`
- `station_props/piano_and_slots/piano_slots_review.blend`
- `station_props/power/solar_additions_preview.blend`
- `station_props/shuttle_furniture/shuttle_furniture_review.blend`
- `station_props/station_extras/station_extras_review.blend`
- `station_props/vending_machines_preview.blend`
- `structures/extinguisher_cabinet/extinguisher_cabinet_preview.blend`
- `structures/picture_frame/picture_frame_preview.blend`
- `structures/ticket_machine/ticket_machine_preview.blend`
- `structures/transport/conveyor_assembly_preview.blend`
- `structures/transport/plastic_flaps_preview.blend`
- `structures/transport/railing_assembly_preview.blend`
- `structures/wall_safe/wall_safe_preview.blend`
- `structures/warning_sign/warning_sign_preview.blend`
- `transit_tubes/transit_tubes_review.blend`

## Libraries: 51 removable (7.1 MB), 5 to keep

Keep (the only source of used GLBs): `furniture/crates/crates_library.blend`, `furniture/metal_locker/metal_locker_atlas_library.blend`, `furniture/metal_locker/remaining_storage_library.blend`, `furniture/metal_table/modular/modular_table_kit.blend`, `furniture/table_kits/table_families_kit.blend`

- `furniture/chair_family/chair_family_library.blend`
- `furniture/emergency_tool_cabinet/emergency_tool_cabinet_variants_library.blend`
- `furniture/metal_locker/metal_locker_variants.blend`
- `furniture/notice_board/notice_board_variants_library.blend`
- `furniture/table_kits/all_table_variants.blend`
- `furniture/table_kits/table_family_examples.blend`
- `items/c4_charge/c4_charge_variants_library.blend`
- `machines/access_button/access_button_variants_library.blend`
- `machines/air_alarm/air_alarm_variants_library.blend`
- `machines/airlock_controller/airlock_controller_variants_library.blend`
- `machines/airlock_pressure_controller/airlock_pressure_controller_variants_library.blend`
- `machines/airlock_sensor/airlock_sensor_variants_library.blend`
- `machines/apc/apc_variants_library.blend`
- `machines/atmos_vents/atmos_vents_variants_library.blend`
- `machines/bar_sign/bar_sign_variants_library.blend`
- `machines/canister_connector/canister_connector_variants_library.blend`
- `machines/cell_charger/cell_charger_variants_library.blend`
- `machines/defib_mount/defib_mount_variants_library.blend`
- `machines/digital_clock/digital_clock_variants_library.blend`
- `machines/door_button/door_button_variants_library.blend`
- `machines/entertainment_monitor/entertainment_monitor_variants_library.blend`
- `machines/fax/fax_variants_library.blend`
- `machines/fire_alarm/fire_alarm_variants_library.blend`
- `machines/gas_pumps/gas_pumps_variants_library.blend`
- `machines/keycard_auth/keycard_auth_variants_library.blend`
- `machines/light_construction/light_construction_variants_library.blend`
- `machines/light_switch/light_switch_variants_library.blend`
- `machines/lighting/lighting_variants_library.blend`
- `machines/microwave/microwave_variants_library.blend`
- `machines/mounted_flash/mounted_flash_variants_library.blend`
- `machines/mounted_igniter/mounted_igniter_variants_library.blend`
- `machines/newscaster/newscaster_variants_library.blend`
- `machines/pepper_refiller/pepper_refiller_variants_library.blend`
- `machines/reagent_grinder/reagent_grinder_variants_library.blend`
- `machines/requests_console/requests_console_variants_library.blend`
- `machines/security_camera/security_camera_variants_library.blend`
- `machines/station_intercom/station_intercom_variants_library.blend`
- `machines/status_display/status_display_variants_library.blend`
- `machines/telescreen/telescreen_variants_library.blend`
- `machines/turret_controller/turret_controller_variants_library.blend`
- `machines/wall_healer/wall_healer_variants_library.blend`
- `machines/weapon_recharger/weapon_recharger_variants_library.blend`
- `station_props/bathroom/urinal_variants_library.blend`
- `station_props/machinery_and_tanks_library.blend`
- `station_props/power/solar_additions_variants_library.blend`
- `structures/extinguisher_cabinet/extinguisher_cabinet_variants_library.blend`
- `structures/picture_frame/picture_frame_variants_library.blend`
- `structures/ticket_machine/ticket_machine_variants_library.blend`
- `structures/transport/transport_variants_library.blend`
- `structures/wall_safe/wall_safe_variants_library.blend`
- `structures/warning_sign/warning_sign_variants_library.blend`

## Sources of unused GLBs: 87 removable (9.7 MB), 0 to keep

- `furniture/emergency_tool_cabinet/fire_axe_cabinet_broken_open.blend`
- `furniture/emergency_tool_cabinet/fire_axe_cabinet_open.blend`
- `furniture/emergency_tool_cabinet/fire_axe_cabinet_open_empty.blend`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_broken_open.blend`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_open.blend`
- `furniture/emergency_tool_cabinet/mech_removal_cabinet_open_empty.blend`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_broken_open.blend`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_open.blend`
- `furniture/emergency_tool_cabinet/rescue_jaws_cabinet_open_empty.blend`
- `furniture/metal_locker/metal_locker.blend`
- `furniture/metal_table/metal_table.blend`
- `items/c4_charge/c4_charge_active.blend`
- `items/c4_charge/c4_charge_active_bright.blend`
- `items/c4_charge/c4_charge_legacy_green.blend`
- `items/c4_charge/c4_charge_legacy_white.blend`
- `items/flatpack/flatpack.blend`
- `items/petri_dish/petri_dish.blend`
- `machines/access_button/access_button_cycle_red.blend`
- `machines/airlock_controller/airlock_controller_process_1.blend`
- `machines/airlock_pressure_controller/airlock_pressure_controller_process_1.blend`
- `machines/airlock_sensor/airlock_sensor_alert_1.blend`
- `machines/airlock_sensor/airlock_sensor_alert_2.blend`
- `machines/airlock_sensor/airlock_sensor_cycle_1.blend`
- `machines/atmos_vents/vent_pump_broken.blend`
- `machines/atmos_vents/vent_pump_overclocked.blend`
- `machines/canister_connector/canister_connector_cap.blend`
- `machines/canister_connector/canister_connector_floor.blend`
- `machines/canister_connector/canister_connector_pipe.blend`
- `machines/computers_construction/airlock_assembly.blend`
- `machines/defib_mount/defib_mount_penlite_emagged_1.blend`
- `machines/defib_mount/defib_mount_standard_emagged_1.blend`
- `machines/door_button/door_button_assembly.blend`
- `machines/fax/fax_idle_alternate.blend`
- `machines/gas_pumps/pressure_gas_pump_off.blend`
- `machines/gas_pumps/pressure_gas_pump_on.blend`
- `machines/gas_pumps/volume_gas_pump_off.blend`
- `machines/gas_pumps/volume_gas_pump_on.blend`
- `machines/gas_pumps/volume_gas_pump_overclocked.blend`
- `machines/gravity_and_tanks/gravity_generator.blend`
- `machines/gravity_and_tanks/gravity_generator_low_profile.blend`
- `machines/gravity_and_tanks/gravity_peripheral_module.blend`
- `machines/lighting/replacement_bulb.blend`
- `machines/lighting/replacement_bulb_broken.blend`
- `machines/lighting/replacement_bulb_burned.blend`
- `machines/lighting/replacement_tube.blend`
- `machines/lighting/replacement_tube_broken.blend`
- `machines/lighting/replacement_tube_burned.blend`
- `machines/mounted_flash/mounted_flash_frame.blend`
- `machines/mounted_flash/mounted_flash_ready_alternate.blend`
- `machines/production_wave/flatpacker.blend`
- `machines/required_models/organ_harvester.blend`
- `machines/status_display/status_display_text_ready.blend`
- `machines/syndicate/singularity_beacon.blend`
- `machines/syndicate/syndicate_ballistic_turret.blend`
- `machines/syndicate/syndicate_medical_sleeper.blend`
- `machines/turret_controller/turret_controller_kill_1.blend`
- `machines/turret_controller/turret_controller_standby_1.blend`
- `machines/turret_controller/turret_controller_stun_1.blend`
- `machines/wall_healer/wall_healer_mixed.blend`
- `machines/wall_healer/wall_healer_off.blend`
- `machines/wall_wave/detective_board.blend`
- `machines/weapon_recharger/weapon_recharger_fail.blend`
- `station_props/machines/suit_storage_classic.blend`
- `station_props/piano_and_slots/minimoog.blend`
- `station_props/piano_and_slots/minimoog_broken.blend`
- `station_props/power/solar_panel_frame.blend`
- `station_props/power/solar_tracker_frame.blend`
- `station_props/shuttle_furniture/escape_shuttle_airlock.blend`
- `station_props/station_equipment/gizmo_active_1.blend`
- `station_props/station_extras/gulag_teleporter.blend`
- `structures/flatpack_cart/flatpack_cart.blend`
- `structures/floor_plating/floor_plating.blend`
- `structures/floor_plating/floor_plating_corner.blend`
- `structures/floor_plating/floor_plating_edge.blend`
- `structures/floor_plating/floor_plating_end.blend`
- `structures/floor_plating/floor_plating_isolated.blend`
- `structures/floor_plating/floor_plating_simple.blend`
- `structures/floor_plating/floor_plating_strip.blend`
- `structures/multilevel/atmos_multideck_adapter.blend`
- `structures/multilevel/elevator_call_button.blend`
- `structures/syndicate/syndicate_corgi_pinata.blend`
- `structures/syndicate/syndicate_shipping_container.blend`
- `structures/syndicate/syndicate_supply_pod.blend`
- `structures/syndicate/uranium_nuclear_statue.blend`
- `structures/transport/plastic_flaps_kitchen.blend`
- `structures/transport/plastic_flaps_opaque.blend`
- `structures/wall_safe/wall_safe_open.blend`

## Blends with no GLB of the same name: 8 removable (2.5 MB), 2 to keep

Keep (the only source of used GLBs): `station_props/chemical_tanks/chemical_tanks.blend`, `station_props/stationary_tanks/rounded/stationary_pressure_tanks.blend`

- `furniture/metal_table/modular/modular_table_examples.blend`
- `machines/medical_wave/body_container_shared.blend`
- `machines/medical_wave/chem_master_shared.blend`
- `station_props/shuttle_furniture/shuttle_furniture_checks.blend`
- `station_props/station_props_pose_checks.blend`
- `station_props/stationary_tanks/stationary_tanks.blend`
- `station_props/vending_catalog.blend`
- `transit_tubes/transit_tubes_assembly.blend`

## Versioned copies: 1 removable (0.8 MB), 2 to keep

Keep (the only source of used GLBs): `machines/stasis/stasis_capsule_v3.blend`, `machines/suit_storage_classic/suit_storage_classic_v3.blend`

- `machines/thermomachine/thermomachine_v1.blend`
