// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the crystal's processing (gas absorption, power, heat, damage, waste gas), its zaps, the
// strategy selection, the countdown and the delamination (G6), its appearance (glow, psy overlay, rays filter, light) and the warp effect. Its radio
// announcements came with the radio (M9 slice D); the monitor window (ui_interact, ui_static_data, ui_data, sm_ui_data: the
// ghosts' Supermatter window, and the data of NtOS's NT CIMS program) came with the engineering package E2 (D287). Left out, in
// docs/ai/specs/live-partial.txt and docs/ai/specs/not-translated.md: the logs and admin messages (M10). The var block is generated (D111).

//Ported from /vg/station13, which was in turn forked from baystation12;
//Please do not bother them with bugs from this port, however, as it has been modified quite a bit.
//Modifications include removing the world-ending full supermatter variation, and leaving only the shard.

// tg: code/modules/power/supermatter/supermatter.dm:15  GLOBAL_DATUM(main_supermatter_engine, /obj/machinery/power/supermatter_crystal)
public partial class datum_controller_global_vars
{
    // tg: code/modules/power/supermatter/supermatter.dm:15  GLOBAL_DATUM(main_supermatter_engine, /obj/machinery/power/supermatter_crystal)
    public obj_machinery_power_supermatter_crystal main_supermatter_engine;

    // tg: code/modules/power/supermatter/supermatter.dm:15  (GLOBAL_UNMANAGED(main_supermatter_engine), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalmain_supermatter_engine()
    {
        return;
    }
}

// tg: code/modules/power/supermatter/supermatter.dm:17
public partial class obj_machinery_power_supermatter_crystal : obj_machinery_power
{
    // tg: code/modules/power/supermatter/supermatter.dm:37  var/static/gl_uid = 1 (rules 10.6: a TgState field)
    public static ref float gl_uid => ref TgState.Current.obj_machinery_power_supermatter_crystal__gl_uid;

    ///Our internal radio
    // tg: code/modules/power/supermatter/supermatter.dm:122  (M9 slice D)
    public obj_item_radio radio;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio, M9 slice D)
    // tg: code/modules/power/supermatter/supermatter.dm:18
    public obj_machinery_power_supermatter_crystal()
    {
        name = "supermatter crystal";
        desc = "A strangely translucent and iridescent crystal.";
        icon = Dm.resource("icons/obj/machines/engine/supermatter.dmi");
        density = 1;
        anchored = 1;
        layer = 4;
        flags_1 = 128;
        resistance_flags = 243;
        critical_machine = 1;
        base_icon_state = "sm";
        icon_state = "sm";
        light_on = 1;
        light_range = 1.4f;
        light_power = 2;
        light_color = "#ffd04f";
    }

    // tg: code/modules/power/supermatter/supermatter.dm:222  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_power_supermatter_crystal__Initialize__loc_connections;

    // tg: code/modules/power/supermatter/supermatter.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        current_gas_behavior = init_sm_gas() as DmList;
        gas_percentage = Dm.list();
        absorbed_gasmix = Dm.@new<datum_gas_mixture>();
        uid = gl_uid++;
        set_delam(SM_DELAM_PRIO_NONE, typeof(datum_sm_delam_explosive));
        SSair.start_processing_machine(this);
        countdown = Dm.@new<obj_effect_countdown_supermatter>(this);
        countdown.start();
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this)); // tg: :208 (M10x slice C)
        radio = Dm.@new<obj_item_radio>(this);
        radio.keyslot = Dm.@new(radio_key);
        radio.set_listening(FALSE);
        radio.recalculateChannels();
        investigate_log("has been created.", INVESTIGATE_ENGINE);
        if (Dm.truthy(is_main_engine))
            GLOB.main_supermatter_engine = this;

        Waiting.NoSleep(AddElement(typeof(datum_element_bsa_blocker)));
        RegisterSignal(this, COMSIG_ATOM_BSA_BEAM, nameof(force_delam));
        RegisterSignal(this, COMSIG_ATOM_TIMESTOP_FREEZE, nameof(time_frozen));
        RegisterSignal(this, COMSIG_ATOM_TIMESTOP_UNFREEZE, nameof(time_unfrozen));
        RegisterSignal(this, COMSIG_ATOM_PRE_BULLET_ACT, nameof(eat_bullets));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections)); //Speficially for the tram, hacky

        Waiting.NoSleep(AddComponent(typeof(datum_component_supermatter_crystal), Dm.@new<datum_callback>(this, nameof(wrench_act_callback)), Dm.@new<datum_callback>(this, nameof(consume_callback))));
        soundloop = Dm.@new<datum_looping_sound_supermatter>(this, TRUE);

        if (check_holidays(FESTIVE_SEASON) != null)
            holiday_lights__proc();

        if (!Dm.truthy(moveable))
            move_resist = MOVE_FORCE_OVERPOWERING; // Avoid being moved by statues or other memes

        // Damn math nerds
        powerloss_linear_threshold = sqrt(POWERLOSS_LINEAR_RATE / 3 * Dm.pow(POWERLOSS_CUBIC_DIVISOR, 3));
        powerloss_linear_offset = -1 * powerloss_linear_threshold * POWERLOSS_LINEAR_RATE + Dm.pow(powerloss_linear_threshold / POWERLOSS_CUBIC_DIVISOR, 3);
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:240
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(warp))
        {
            Dm.remove(vis_contents, warp);
            QDEL_NULL(ref warp);
        }
        investigate_log("has been destroyed.", INVESTIGATE_ENGINE);
        SSair.stop_processing_machine(this);
        absorbed_gasmix = null;
        QDEL_NULL(ref radio);
        QDEL_NULL(ref countdown);
        if (Dm.truthy(is_main_engine) && GLOB.main_supermatter_engine == this)
            GLOB.main_supermatter_engine = null;
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // tg: code/modules/power/supermatter/supermatter.dm:254
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        if (Dm.truthy(warp))
            SET_PLANE_EXPLICIT(warp, Dm.num(PLANE_TO_TRUE(warp.plane)), this);
    }

    // tg: code/modules/power/supermatter/supermatter.dm:261
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        float immune = Dm.num(HAS_MIND_TRAIT(user, TRAIT_MADNESS_IMMUNE));
        if (isliving(user))
        {
            if (!Dm.truthy(immune) && (get_dist(user, this) < SM_HALLUCINATION_RANGE(internal_energy)))
                Dm.append(ref dot, span_danger(I18n.T("You get headaches just from looking at it.")));
            mob_living living_user = (mob_living)user;
            if (HAS_TRAIT(user, TRAIT_REMOTE_TASTING))
            {
                to_chat(user, span_warning(I18n.T("The taste is overwhelming and indescribable!")));
                await living_user.electrocute_act(shock_damage: 15, source: this, flags: Dm.bitor(SHOCK_KNOCKDOWN, SHOCK_NOGLOVES));
                Dm.append(ref dot, span_notice(I18n.T("It could use a little more Sodium Chloride...")));
            }
        }

        if (Dm.truthy(holiday_lights))
            Dm.append(ref dot, span_notice(I18n.T("Radiating both festive cheer and actual radiation, it has a dazzling spectacle lights wrapped lovingly around the base transforming it from a potential doomsday device into a cosmic yuletide centerpiece.")));

        Dm.append(ref dot, delamination_strategy.examine(this));
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:279
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        // PART 1: PRELIMINARIES
        if (disable_process != SM_PROCESS_ENABLED)
            return dot.Value;

        turf local_turf = loc as turf;
        if (!(local_turf is turf))//We are in a crate or somewhere that isn't turf, if we return to turf resume processing but for now.
            return dot.Value;  //Yeah just stop.
        if (isclosedturf(local_turf))
        {
            turf did_it_melt = await local_turf.Melt() as turf;
            if (!isclosedturf(did_it_melt)) //In case some joker finds way to place these on indestructible walls
                visible_message(span_warning(I18n.T("[] melts through []!", this, local_turf)));
            return dot.Value;
        }

        // PART 2: GAS PROCESSING
        datum_gas_mixture env = await local_turf.return_air() as datum_gas_mixture;
        absorbed_gasmix = (env?.remove_ratio(absorption_ratio) is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.@new<datum_gas_mixture>()) as datum_gas_mixture;
        absorbed_gasmix.volume = Dm.num(env?.volume is var or__2 && Dm.truthy(or__2) ? or__2 : CELL_VOLUME) * absorption_ratio; // To match the pressure.
        calculate_gases();
        // Extra effects should always fire after the compositions are all finished
        // Some extra effects like [/datum/sm_gas/carbon_dioxide/extra_effects]
        // needs more than one gas and rely on a fully parsed gas_percentage.
        foreach (object gas_path in Dm.each(absorbed_gasmix.moles))
        {
            datum_sm_gas sm_gas = (datum_sm_gas)current_gas_behavior[gas_path];
            if (sm_gas != null)
                await sm_gas.extra_effects(this);
        }

        // PART 3: POWER PROCESSING
        internal_energy_factors = calculate_internal_energy() as DmList;
        zap_factors = calculate_zap_transmission_rate() as DmList;
        float delta_time = (SSmachines.times_fired - last_energy_accumulation_perspective_machines) * SSmachines.wait / (1 * SECONDS);
        object accumulated_energy = accumulate_energy(ZAP_ENERGY_ACCUMULATION_NORMAL, energy: internal_energy * zap_transmission_rate * delta_time);
        if (Dm.truthy(accumulated_energy) && (last_power_zap + (4 - internal_energy * 0.001f) * SECONDS) < world.time)
        {
            object discharged_energy = discharge_energy(ZAP_ENERGY_ACCUMULATION_NORMAL);
            playsound(this, Dm.resource("sound/items/weapons/emitter2.ogg"), 70, TRUE);
            hue_angle_shift = clamp(903 * log(10, (internal_energy + 8000)) - 3590, -50, 240);
            object zap_color = color_matrix_rotate_hue(hue_angle_shift);
            await supermatter_zap(
                zapstart: this,
                range: 3,
                zap_str: Dm.num(discharged_energy),
                zap_flags: ZAP_SUPERMATTER_FLAGS,
                zap_cutoff: 240 * KILO * JOULES,
                power_level: internal_energy,
                color: zap_color
            );

            last_power_zap = world.time;
        }
        last_energy_accumulation_perspective_machines = SSmachines.times_fired;

        // PART 4: DAMAGE PROCESSING
        temp_limit_factors = calculate_temp_limit() as DmList;
        damage_archived = Dm.num(damage);
        damage_factors = calculate_damage() as DmList;
        if (Dm.num(damage) == 0) // Clear any in game forced delams if on full health.
            set_delam(SM_DELAM_PRIO_IN_GAME, null); // SM_DELAM_STRATEGY_PURGE (null)
        else if (!Dm.truthy(final_countdown))
            set_delam(SM_DELAM_PRIO_NONE, null); // SM_DELAM_STRATEGY_PURGE (null). This one cant clear any forced delams.
        await delamination_strategy.delam_progress(this);
        if (Dm.num(damage) > explosion_point && !Dm.truthy(final_countdown))
            count_down();

        // PART 5: WASTE GAS PROCESSING
        waste_multiplier_factors = calculate_waste_multiplier() as DmList;
        float device_energy = internal_energy * REACTION_POWER_MODIFIER;

        /// Do waste on another gasmix so we can keep a copy of the gasmix we use for processing.
        datum_gas_mixture merged_gasmix = absorbed_gasmix.copy() as datum_gas_mixture;
        merged_gasmix.temperature += device_energy * waste_multiplier / THERMAL_RELEASE_MODIFIER;
        merged_gasmix.temperature = clamp(merged_gasmix.temperature, TCMB, 2500 * waste_multiplier);
        merged_gasmix.assert_gases(typeof(datum_gas_plasma), typeof(datum_gas_oxygen));
        Dm.append_at(merged_gasmix.moles, typeof(datum_gas_plasma), max(device_energy * waste_multiplier / PLASMA_RELEASE_MODIFIER, 0));
        Dm.append_at(merged_gasmix.moles, typeof(datum_gas_oxygen), max(((device_energy + merged_gasmix.temperature * waste_multiplier) - T0C) / OXYGEN_RELEASE_MODIFIER, 0));
        merged_gasmix.garbage_collect();
        env.merge(merged_gasmix);
        air_update_turf(FALSE, FALSE);

        // PART 6: EXTRA BEHAVIOUR
        emit_radiation();
        processing_sound();
        await handle_high_power();
        await psychological_examination();

        // handle the engineers that saved the engine from cascading, if there were any
        if (Dm.num(get_status()) < SUPERMATTER_EMERGENCY && saviors != null)
        {
            foreach (datum_weakref savior_ref in Dm.each_cast<datum_weakref>(saviors))
            {
                mob_living savior = savior_ref.resolve() as mob_living;
                if (!(savior is mob_living)) // didn't live to tell the tale, sadly.
                    continue;
                // savior.client?.give_award(/datum/award/achievement/jobs/theoretical_limits, savior)
                Dm.Deferred("code/modules/power/supermatter/supermatter.dm:367", "process_atmos: the theoretical_limits achievement of the engineers who turned a cascade around (achievements, M10+)");
            }
            LAZYNULL(ref saviors);
        }

        if (prob(15))
            await supermatter_pull(loc as turf, min(internal_energy / 850, 3));//850, 1700, 2550
        update_appearance();
        await delamination_strategy.lights(this);
        delamination_strategy.filters(this);
        absorption_ratio = clamp(absorption_ratio - 0.05f, 0.15f, 1);
        return TRUE;
    }

    // SupermatterMonitor UI for ghosts only. Inherited attack_ghost will call this.
    // tg: code/modules/power/supermatter/supermatter.dm:379
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!isobserver(user))
            return FALSE;
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Supermatter");
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:388
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["gas_metadata"] = sm_gas_data();
        return data;
    }

    /// Returns data that are exclusively about this sm.
    // tg: code/modules/power/supermatter/supermatter.dm:394
    public virtual object sm_ui_data()
    {
        DmList data = Dm.list();
        data["uid"] = uid;
        data["area_name"] = get_area_name(this);

        data["integrity"] = get_integrity_percent();
        DmList integrity_factors = Dm.list();
        data["integrity_factors"] = integrity_factors;
        foreach (object factor in Dm.each(damage_factors))
        {
            float amount = round(Dm.num(damage_factors[factor]), 0.01f);
            if (!Dm.truthy(amount))
                continue;
            Dm.append(ref integrity_factors, Dm.list(Dm.list(
                Dm.kv("name", factor),
                Dm.kv("amount", amount * -1))));
        }
        DmList internal_energy_si_derived_data = siunit_isolated(internal_energy * 1e6f, "eV", 3) as DmList;
        data["internal_energy"] = internal_energy;
        data["internal_energy_coefficient"] = internal_energy_si_derived_data[SI_COEFFICIENT];
        data["internal_energy_unit"] = internal_energy_si_derived_data[SI_UNIT];
        DmList internal_energy_factors_data = Dm.list();
        data["internal_energy_factors"] = internal_energy_factors_data;
        foreach (object factor in Dm.each(internal_energy_factors))
        {
            DmList internal_energy_factor_si_derived_data = siunit_isolated(Dm.num(internal_energy_factors[factor]) * 1e6f, "eV", 3) as DmList;
            float amount = round(Dm.num(internal_energy_factors[factor]), 0.01f);
            if (!Dm.truthy(amount))
                continue;
            Dm.append(ref internal_energy_factors_data, Dm.list(Dm.list(
                Dm.kv("name", factor),
                Dm.kv("amount", internal_energy_factor_si_derived_data[SI_COEFFICIENT]),
                Dm.kv("unit", internal_energy_factor_si_derived_data[SI_UNIT]))));
        }
        data["temp_limit"] = temp_limit;
        DmList temp_limit_factors_data = Dm.list();
        data["temp_limit_factors"] = temp_limit_factors_data;
        foreach (object factor in Dm.each(temp_limit_factors))
        {
            float amount = round(Dm.num(temp_limit_factors[factor]), 0.01f);
            if (!Dm.truthy(amount))
                continue;
            Dm.append(ref temp_limit_factors_data, Dm.list(Dm.list(
                Dm.kv("name", factor),
                Dm.kv("amount", amount))));
        }
        data["waste_multiplier"] = waste_multiplier;
        DmList waste_multiplier_factors_data = Dm.list();
        data["waste_multiplier_factors"] = waste_multiplier_factors_data;
        foreach (object factor in Dm.each(waste_multiplier_factors))
        {
            float amount = round(Dm.num(waste_multiplier_factors[factor]), 0.01f);
            if (!Dm.truthy(amount))
                continue;
            Dm.append(ref waste_multiplier_factors_data, Dm.list(Dm.list(
                Dm.kv("name", factor),
                Dm.kv("amount", amount))));
        }

        DmList zap_transmission_factors = Dm.list();
        data["zap_transmission_factors"] = zap_transmission_factors;
        foreach (object factor in Dm.each(zap_factors))
        {
            DmList zap_factor_si_derived_data = siunit_isolated(Dm.num(zap_factors[factor]) * internal_energy, "W", 2) as DmList;
            if (!Dm.truthy(zap_factor_si_derived_data[SI_COEFFICIENT]))
                continue;
            Dm.append(ref zap_transmission_factors, Dm.list(Dm.list(
                Dm.kv("name", factor),
                Dm.kv("amount", zap_factor_si_derived_data[SI_COEFFICIENT]),
                Dm.kv("unit", zap_factor_si_derived_data[SI_UNIT]))));
        }

        ///Add high energy bonus to the zap transmission data so we can accurately measure our power generation from zaps.
        float high_energy_bonus = 0;
        float zap_transmission = zap_transmission_rate * internal_energy;
        float zap_power_multiplier = 1;
        if (internal_energy > POWER_PENALTY_THRESHOLD) //Supermatter zaps multiply power internally under some conditions for some reason, so we'll snowflake this for now.
        {
            ///Power multiplier bonus applied to all zaps. Zap power generation doubles when it reaches 7GeV and 9GeV.
            zap_power_multiplier *= Dm.pow(2, clamp(round((internal_energy - POWER_PENALTY_THRESHOLD) / 2000), 0, 2));
            ///The supermatter releases additional zaps after 5GeV, with more at 7GeV and 9GeV.
            // INVERSE_LERP(1000, 3000, internal_energy) (__DEFINES/maths.dm:107), inline (rules 11.1 (a))
            float additional_zap_bonus = clamp(internal_energy * 3200, 6.4e6f, 3.2e7f) * clamp(round((internal_energy - 1000) / (3000 - 1000)), 1, 4);
            high_energy_bonus = (zap_transmission + additional_zap_bonus) * zap_power_multiplier - zap_transmission;
            DmList zap_factor_si_derived_data = siunit_isolated(high_energy_bonus, "W", 2) as DmList;
            Dm.append(ref zap_transmission_factors, Dm.list(Dm.list(
                Dm.kv("name", "High Energy Bonus"),
                Dm.kv("amount", zap_factor_si_derived_data[SI_COEFFICIENT]),
                Dm.kv("unit", zap_factor_si_derived_data[SI_UNIT]))));
        }

        DmList zap_transmission_si_derived_data = siunit_isolated(zap_transmission + high_energy_bonus, "W", 2) as DmList;
        data["zap_transmission"] = zap_transmission + high_energy_bonus;
        data["zap_transmission_coefficient"] = zap_transmission_si_derived_data[SI_COEFFICIENT];
        data["zap_transmission_unit"] = zap_transmission_si_derived_data[SI_UNIT];

        data["absorbed_ratio"] = absorption_ratio;
        DmList formatted_gas_percentage = Dm.list();
        foreach (Type gas_path in Dm.each_cast<Type>(subtypesof(typeof(datum_gas))))
        {
            object percentage = gas_percentage?[gas_path];
            formatted_gas_percentage[gas_path] = Dm.truthy(percentage) ? percentage : 0;
        }
        data["gas_composition"] = formatted_gas_percentage;
        data["gas_temperature"] = absorbed_gasmix.temperature;
        data["gas_total_moles"] = absorbed_gasmix.total_moles();
        return data;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:487
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["sm_data"] = Dm.list(sm_ui_data());
        return data;
    }

    /// Encodes the current state of the supermatter.
    // tg: code/modules/power/supermatter/supermatter.dm:493
    public virtual object get_status()
    {
        if (!Dm.truthy(absorbed_gasmix))
            return SUPERMATTER_ERROR;
        if (Dm.truthy(final_countdown))
            return SUPERMATTER_DELAMINATING;
        if (Dm.num(damage) >= emergency_point)
            return SUPERMATTER_EMERGENCY;
        if (Dm.num(damage) >= danger_point)
            return SUPERMATTER_DANGER;
        if (Dm.num(damage) >= warning_point)
            return SUPERMATTER_WARNING;
        if (absorbed_gasmix.temperature > temp_limit * 0.8f)
            return SUPERMATTER_NOTIFY;
        if (Dm.truthy(internal_energy))
            return SUPERMATTER_NORMAL;
        return SUPERMATTER_INACTIVE;
    }

    /// Returns the integrity percent of the Supermatter. No rounding made yet, round it yourself.
    // tg: code/modules/power/supermatter/supermatter.dm:511
    public virtual object get_integrity_percent()
    {
        float integrity = Dm.num(damage) / explosion_point;
        integrity = 100 - integrity * 100;
        integrity = integrity < 0 ? 0 : integrity;
        return integrity;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:517
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (psy_coeff > 0)
            Dm.append(ref dot, mutable_appearance(icon: icon, icon_state: Dm.text("[]-psy", base_icon_state), layer: FLOAT_LAYER - 1, alpha: psy_coeff * 255));
        if (Dm.truthy(delamination_strategy))
            Dm.append(ref dot, delamination_strategy.overlays(this));
        if (Dm.truthy(holiday_lights))
        {
            if (this is obj_machinery_power_supermatter_crystal_shard)
            {
                Dm.append(ref dot, mutable_appearance(icon, "holiday_lights_shard"));
                // . += emissive_appearance(icon, "holiday_lights_shard_e", src, alpha = src.alpha): DEVIATION(DV12), no emissive plane
            }
            else
            {
                Dm.append(ref dot, mutable_appearance(icon, "holiday_lights"));
                // . += emissive_appearance(icon, "holiday_lights_e", src, alpha = src.alpha): DEVIATION(DV12), no emissive plane
            }
        }
        // if(light_mask_icon) . += emissive_appearance(icon, light_mask_icon, src, alpha = src.alpha): DEVIATION(DV12), the
        // crystal's emissive mask on the emissive plane; its glow is the light and the rays filter
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:534
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        if (gas_heat_power_generation > 0.8f)
            icon_state = Dm.text("[]-glow", base_icon_state);
        else
            icon_state = (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:541
    [SignalHandler]
    public virtual void time_frozen()
    {
        if (disable_process != SM_PROCESS_ENABLED)
            return;

        disable_process = SM_PROCESS_TIMESTOP;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:548
    [SignalHandler]
    public virtual void time_unfrozen()
    {
        if (disable_process != SM_PROCESS_TIMESTOP)
            return;

        disable_process = SM_PROCESS_ENABLED;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:555
    [SignalHandler]
    public virtual void force_delam()
    {
        investigate_log("was forcefully delaminated", INVESTIGATE_ENGINE);
        Waiting.Detach(delamination_strategy.delaminate(this)); // INVOKE_ASYNC(delamination_strategy, TYPE_PROC_REF(/datum/sm_delam, delaminate), src)
    }

    /**
     * Count down, spout some messages, and then execute the delam itself.
     * We guard for last second delam strat changes here, mostly because some have diff messages.
     *
     * By last second changes, we mean that it's possible for say, a tesla delam to
     * just explode normally if at the absolute last second it loses power and switches to default one.
     * Even after countdown is already in progress.
     */
    // tg: code/modules/power/supermatter/supermatter.dm:568
    public virtual void count_down()
    {
        Waiting.WaitforFalse(count_down__body());
    }

    private async DmTask count_down__body()
    {
        if (Dm.truthy(final_countdown)) // We're already doing it go away
        {
            stack_trace(Dm.text("[] told to delaminate again while it's already delaminating.", this), "code/modules/power/supermatter/supermatter.dm", 572);
            return;
        }

        final_countdown = TRUE;

        notify_ghosts(
            I18n.T("[] has begun the delamination process!", this),
            source: this,
            header: I18n.T("Meltdown Incoming"));

        DmList count_down_messages = delamination_strategy.count_down_messages() as DmList;

        radio.talk_into(
            this,
            count_down_messages[1],
            emergency_channel,
            Dm.list(SPAN_COMMAND)
        );

        float delamination_countdown_time = SUPERMATTER_COUNTDOWN_TIME;
        // If a sliver was removed from the supermatter, the countdown time is significantly decreased
        if (supermatter_sliver_removed == TRUE)
        {
            delamination_countdown_time = SUPERMATTER_SLIVER_REMOVED_COUNTDOWN_TIME;
            radio.talk_into(
                this,
                I18n.T("WARNING: Projected time until full crystal delamination significantly lower than expected. Please inspect crystal for structural abnormalities or sabotage!"),
                emergency_channel,
                Dm.list(SPAN_COMMAND)
            );
        }

        for (float i = delamination_countdown_time, i__end = 0; i >= i__end; i += -10)
        {
            object message = null;
            float healed = FALSE;

            if (Dm.num(damage) < explosion_point) // Cutting it a bit close there engineers
            {
                message = count_down_messages[2];
                healed = TRUE;
            }
            else if ((Dm.mod(i, 50)) != 0 && i > 50) // A message once every 5 seconds until the final 5 seconds which count down individualy
            {
                await sleep(1 * SECONDS);
                continue;
            }
            else if (i > 50)
                message = Dm.text("[] []", DisplayTimeText(i, TRUE), count_down_messages[3]);
            else
                message = Dm.text("[]...", i * 0.1f);

            radio.talk_into(this, message, emergency_channel, Dm.list(SPAN_COMMAND));

            if (Dm.truthy(healed))
            {
                final_countdown = FALSE;

                if (!(delamination_strategy is datum_sm_delam_cascade))
                    return;

                foreach (mob_living lucky_engi in Dm.each_cast<mob_living>(mobs_in_area_type(Dm.list(typeof(area_station_engineering_supermatter)))))
                {
                    if (lucky_engi.client == null)
                        continue;
                    if (isanimal_or_basicmob(lucky_engi))
                        continue;
                    LAZYADD(ref saviors, WEAKREF(lucky_engi));
                }

                return; // delam averted
            }
            await sleep(1 * SECONDS);
        }

        await delamination_strategy.delaminate(this);
    }

    // All the calculate procs should only update variables.
    // Move the actual real-world effects to [/obj/machinery/power/supermatter_crystal/process_atmos].

    /**
     * Perform calculation for variables that depend on gases.
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/list/gas_percentage]
     * [/obj/machinery/power/supermatter_crystal/var/gas_power_transmission_rate]
     * [/obj/machinery/power/supermatter_crystal/var/gas_heat_modifier]
     * [/obj/machinery/power/supermatter_crystal/var/gas_heat_resistance]
     * [/obj/machinery/power/supermatter_crystal/var/gas_heat_power_generation]
     * [/obj/machinery/power/supermatter_crystal/var/gas_powerloss_inhibition]
     *
     * Returns: null
     */
    // tg: code/modules/power/supermatter/supermatter.dm:656
    public virtual void calculate_gases()
    {
        if (Dm.truthy(disable_gas))
            return;

        gas_percentage = Dm.list();
        gas_power_transmission_rate = 0;
        gas_heat_modifier = 0;
        gas_heat_resistance = 0;
        gas_heat_power_generation = 0;
        gas_powerloss_inhibition = 0;

        float total_moles = Dm.num(absorbed_gasmix.total_moles());
        if (total_moles < MINIMUM_MOLE_COUNT) //it's not worth processing small amounts like these, total_moles can also be 0 in vacuume
            return;
        foreach ((object gas_path, object mole_count) in Dm.pairs(absorbed_gasmix.moles))
        {
            if (Dm.num(mole_count) < MINIMUM_MOLE_COUNT) //save processing power from small amounts like these
                continue;
            gas_percentage[gas_path] = Dm.num(mole_count) / total_moles;
            datum_sm_gas sm_gas = (datum_sm_gas)current_gas_behavior[gas_path];
            if (!Dm.truthy(sm_gas))
                continue;
            gas_power_transmission_rate += sm_gas.power_transmission * Dm.num(gas_percentage[gas_path]);
            gas_heat_modifier += sm_gas.heat_modifier * Dm.num(gas_percentage[gas_path]);
            gas_heat_resistance += sm_gas.heat_resistance * Dm.num(gas_percentage[gas_path]);
            gas_heat_power_generation += sm_gas.heat_power_generation * Dm.num(gas_percentage[gas_path]);
            gas_powerloss_inhibition += sm_gas.powerloss_inhibition * Dm.num(gas_percentage[gas_path]);
        }

        gas_heat_power_generation = clamp(gas_heat_power_generation, 0, 1);
        gas_powerloss_inhibition = clamp(gas_powerloss_inhibition, 0, 1);
    }

    /**
     * Perform calculation for power lost and gained this tick.
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/internal_energy]
     * [/obj/machinery/power/supermatter_crystal/var/external_power_trickle]
     * [/obj/machinery/power/supermatter_crystal/var/external_power_immediate]
     *
     * Returns: The factors that have influenced the calculation. list[FACTOR_DEFINE] = number
     */
    // tg: code/modules/power/supermatter/supermatter.dm:697
    public virtual object calculate_internal_energy()
    {
        if (Dm.truthy(disable_power_change))
            return null;
        DmList additive_power = Dm.list();

        /// If we have a small amount of external_power_trickle we just round it up to 40.
        additive_power[SM_POWER_EXTERNAL_TRICKLE] = Dm.truthy(external_power_trickle) ? max(external_power_trickle / MATTER_POWER_CONVERSION, 40) : 0;
        external_power_trickle -= min(Dm.num(additive_power[SM_POWER_EXTERNAL_TRICKLE]), external_power_trickle);
        additive_power[SM_POWER_EXTERNAL_IMMEDIATE] = external_power_immediate;
        external_power_immediate = 0;
        additive_power[SM_POWER_HEAT] = gas_heat_power_generation * absorbed_gasmix.temperature * GAS_HEAT_POWER_SCALING_COEFFICIENT;
        if (Dm.truthy(additive_power[SM_POWER_HEAT]))
            log_activation(who: "environmental factors");

        // I'm sorry for this, but we need to calculate power lost immediately after power gain.
        // Helps us prevent cases when someone dumps superhothotgas into the SM and shoots the power to the moon for one tick.
        /// Power if we dont have decay. Used for powerloss calc.
        float momentary_power = internal_energy;
        foreach (object powergain_type in Dm.each(additive_power))
            momentary_power += Dm.num(additive_power[powergain_type]);
        if (momentary_power < powerloss_linear_threshold) // Negative numbers
            additive_power[SM_POWER_POWERLOSS] = -1 * Dm.pow(momentary_power / POWERLOSS_CUBIC_DIVISOR, 3);
        else
            additive_power[SM_POWER_POWERLOSS] = -1 * (momentary_power * POWERLOSS_LINEAR_RATE + powerloss_linear_offset);
        // Positive number
        additive_power[SM_POWER_POWERLOSS_GAS] = -1 * gas_powerloss_inhibition * Dm.num(additive_power[SM_POWER_POWERLOSS]);
        additive_power[SM_POWER_POWERLOSS_SOOTHED] = -1 * min(1 - gas_powerloss_inhibition, 0.2f * psy_coeff) * Dm.num(additive_power[SM_POWER_POWERLOSS]);

        foreach (object powergain_types in Dm.each(additive_power))
            internal_energy += Dm.num(additive_power[powergain_types]);
        internal_energy = max(internal_energy, 0);
        if (Dm.truthy(internal_energy) && !Dm.truthy(activation_logged))
        {
            stack_trace(Dm.text("Supermatter powered for the first time without being logged. Internal energy factors: []", json_encode(internal_energy_factors)), "code/modules/power/supermatter/supermatter.dm", 728);
            activation_logged = TRUE; // so we dont spam the log.
        }
        else if (!Dm.truthy(internal_energy))
        {
            last_power_zap = world.time;
            last_energy_accumulation_perspective_machines = SSmachines.times_fired;
        }
        return additive_power;
    }

    /** Log when the supermatter is activated for the first time.
     * Everything that can increase [/obj/machinery/power/supermatter_crystal/var/internal_energy]
     * either directly or indirectly MUST call this.
     *
     * Arguments:
     * * who - Either a string or a datum. Whatever gave power to the SM. Mandatory.
     * * how - A datum. How they powered it. Optional.
     */
    // tg: code/modules/power/supermatter/supermatter.dm:743
    public virtual void log_activation(object who = null, object how = null)
    {
        if (Dm.truthy(activation_logged) || Dm.truthy(disable_power_change))
            return;
        if (!Dm.truthy(who))
            throw Dm.CRASH("Supermatter activated by an unknown source", "code/modules/power/supermatter/supermatter.dm", 747);

        investigate_log(Dm.text("has been powered for the first time by [][].", who, Dm.truthy(how) ? " with [how]" : ""), INVESTIGATE_ENGINE);
        message_admins(Dm.text("[] [] has been powered for the first time by [][].", this, ADMIN_JMP(this), who, Dm.truthy(how) ? " with [how]" : ""));
        activation_logged = TRUE;
    }

    /**
     * Perform calculation for the main zap power transmission rate in W/MeV.
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/zap_transmission_rate]
     *
     * Returns: The factors that have influenced the calculation. list[FACTOR_DEFINE] = number
     */
    // tg: code/modules/power/supermatter/supermatter.dm:766
    public virtual object calculate_zap_transmission_rate()
    {
        DmList additive_transmission_rate = Dm.list();
        additive_transmission_rate[SM_ZAP_BASE] = BASE_POWER_TRANSMISSION_RATE;
        additive_transmission_rate[SM_ZAP_GAS] = BASE_POWER_TRANSMISSION_RATE * gas_power_transmission_rate;

        zap_transmission_rate = 0;
        foreach (object transmission_types in Dm.each(additive_transmission_rate))
            zap_transmission_rate += Dm.num(additive_transmission_rate[transmission_types]);
        zap_transmission_rate = max(zap_transmission_rate, 0);
        return additive_transmission_rate;
    }

    /**
     * Perform calculation for the waste multiplier.
     * This number affects the temperature, plasma, and oxygen of the waste gas.
     * Multiplier is applied to energy for plasma and temperature but temperature for oxygen.
     *
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/waste_multiplier]
     *
     * Returns: The factors that have influenced the calculation. list[FACTOR_DEFINE] = number
     */
    // tg: code/modules/power/supermatter/supermatter.dm:789
    public virtual object calculate_waste_multiplier()
    {
        waste_multiplier = 0;
        if (Dm.truthy(disable_gas))
            return null;
        /// Tell people the heat output in energy. More informative than telling them the heat multiplier.
        DmList additive_waste_multiplier = Dm.list();
        additive_waste_multiplier[SM_WASTE_BASE] = 1;
        additive_waste_multiplier[SM_WASTE_GAS] = gas_heat_modifier;
        additive_waste_multiplier[SM_WASTE_SOOTHED] = -0.2f * psy_coeff;

        foreach (object waste_type in Dm.each(additive_waste_multiplier))
            waste_multiplier += Dm.num(additive_waste_multiplier[waste_type]);
        waste_multiplier = clamp(waste_multiplier, 0.5f, INFINITY);
        return additive_waste_multiplier;
    }

    /**
     * Calculate at which temperature the sm starts taking damage.
     * heat limit is given by: (T0C+40) * (1 + gas heat res + psy_coeff)
     *
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/temp_limit]
     *
     * Returns: The factors that have influenced the calculation. list[FACTOR_DEFINE] = number
     */
    // tg: code/modules/power/supermatter/supermatter.dm:815
    public virtual object calculate_temp_limit()
    {
        DmList additive_temp_limit = Dm.list();
        additive_temp_limit[SM_TEMP_LIMIT_BASE] = T0C + HEAT_PENALTY_THRESHOLD;
        additive_temp_limit[SM_TEMP_LIMIT_GAS] = gas_heat_resistance * (T0C + HEAT_PENALTY_THRESHOLD);
        additive_temp_limit[SM_TEMP_LIMIT_SOOTHED] = psy_coeff * 45;
        additive_temp_limit[SM_TEMP_LIMIT_LOW_MOLES] = clamp(2 - Dm.num(absorbed_gasmix.total_moles()) / 100, 0, 1) * (T0C + HEAT_PENALTY_THRESHOLD);

        temp_limit = 0;
        foreach (object resistance_type in Dm.each(additive_temp_limit))
            temp_limit += Dm.num(additive_temp_limit[resistance_type]);
        temp_limit = max(temp_limit, TCMB);

        return additive_temp_limit;
    }

    /**
     * Perform calculation for the damage taken or healed.
     * Description of each factors can be found in the defines.
     *
     * Updates:
     * [/obj/machinery/power/supermatter_crystal/var/damage]
     *
     * Returns: The factors that have influenced the calculation. list[FACTOR_DEFINE] = number
     */
    // tg: code/modules/power/supermatter/supermatter.dm:838
    public virtual object calculate_damage()
    {
        if (Dm.truthy(disable_damage))
            return null;

        DmList additive_damage = Dm.list();
        float total_moles = Dm.num(absorbed_gasmix.total_moles());

        // We dont let external factors deal more damage than the emergency point.
        // Only cares about the damage before this proc is run. We ignore soon-to-be-applied damage.
        additive_damage[SM_DAMAGE_EXTERNAL] = external_damage_immediate * clamp((emergency_point - Dm.num(damage)) / emergency_point, 0, 1);
        external_damage_immediate = 0;

        additive_damage[SM_DAMAGE_HEAT] = clamp((absorbed_gasmix.temperature - temp_limit) / 24000, 0, 0.15f);
        additive_damage[SM_DAMAGE_POWER] = clamp((internal_energy - POWER_PENALTY_THRESHOLD) / 40000, 0, 0.1f);
        additive_damage[SM_DAMAGE_MOLES] = clamp((total_moles - MOLE_PENALTY_THRESHOLD) / 3200, 0, 0.1f);

        float is_spaced = FALSE;
        if (isturf(this.loc))
        {
            turf local_turf = (turf)this.loc;
            foreach (turf_open_space turf in Dm.each<turf_open_space>(Dm.add(local_turf.atmos_adjacent_turfs is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list(), local_turf)))
            {
                additive_damage[SM_DAMAGE_SPACED] = clamp(internal_energy * 0.000125f, 0, 1);
                is_spaced = TRUE;
                break;
            }
        }

        if (total_moles > 0 && !Dm.truthy(is_spaced))
            additive_damage[SM_DAMAGE_HEAL_HEAT] = clamp((absorbed_gasmix.temperature - temp_limit) / 6000, -0.1f, 0);

        float total_damage = 0;
        foreach (object damage_type in Dm.each(additive_damage))
            total_damage += Dm.num(additive_damage[damage_type]);

        damage = Dm.num(damage) + total_damage;
        damage = max(Dm.num(damage), 0);
        return additive_damage;
    }

    /**
     * Sets the delam of our sm.
     *
     * Arguments:
     * * priority: Truthy values means a forced delam. If current forced_delam is higher than priority we dont run.
     * Set to a number higher than [SM_DELAM_PRIO_IN_GAME] to fully force an admin delam.
     * * delam_path: Typepath of a [/datum/sm_delam]. [SM_DELAM_STRATEGY_PURGE] means reset and put prio back to zero.
     *
     * Returns: Not used for anything, just returns true on successful set, manual and automatic. Helps admins check stuffs.
     */
    // tg: code/modules/power/supermatter/supermatter.dm:883
    public virtual object set_delam(float? priority = null, object manual_delam_path = null)
    {
        priority ??= SM_DELAM_PRIO_NONE;
        // manual_delam_path = SM_DELAM_STRATEGY_PURGE: null, the parameter's own default
        if (Dm.num(priority) < delam_priority)
            return FALSE;
        datum_sm_delam new_delam = null;

        if (manual_delam_path == null) // SM_DELAM_STRATEGY_PURGE (null)
        {
            foreach (object delam_path in Dm.each(GLOB.sm_delam_list))
            {
                datum_sm_delam delam = (datum_sm_delam)GLOB.sm_delam_list[delam_path];
                if (!Dm.truthy(delam.can_select(this)))
                    continue;
                if (delam == delamination_strategy)
                    return FALSE;
                new_delam = delam;
                break;
            }
            delam_priority = SM_DELAM_PRIO_NONE;
        }
        else
        {
            new_delam = (datum_sm_delam)GLOB.sm_delam_list[manual_delam_path];
            delam_priority = Dm.num(priority);
        }

        if (!Dm.truthy(new_delam))
            return FALSE;
        delamination_strategy?.on_deselect(this);
        delamination_strategy = new_delam;
        delamination_strategy.on_select(this);
        return TRUE;
    }

    /**
     * Accumulates energy for the zap_energy_accumulation key.
     * Args:
     * * key: The zap energy accumulation key to use.
     * * energy: The amount of energy to accumulate.
     * Returns: The accumulated energy for that key.
     */
    // tg: code/modules/power/supermatter/supermatter.dm:916
    public virtual object accumulate_energy(object key = null, object energy = null)
    {
        object dot = null;
        dot = Dm.add(Dm.truthy(zap_energy_accumulation[key]) ? zap_energy_accumulation[key] : 0, energy);
        zap_energy_accumulation[key] = dot;
        return dot;
    }

    /**
     * Depletes a portion of the accumulated energy for the given key and returns it. Used for discharging energy from the supermatter.
     * Args:
     * * key: The zap energy accumulation key to use.
     * * portion: The portion of the accumulated energy that gets discharged.
     * Returns: The discharged energy for that key.
     */
    // tg: code/modules/power/supermatter/supermatter.dm:927
    public virtual object discharge_energy(object key = null, float? portion = null)
    {
        portion ??= ZAP_ENERGY_DISCHARGE_PORTION;
        object dot = null;
        dot = Dm.num(portion) * Dm.num(zap_energy_accumulation[key]);
        Dm.remove_at(zap_energy_accumulation, key, dot);
        return dot;
    }

    // For /datum/sm_delam to check if it should be sending an alert on common radio channel
    // tg: code/modules/power/supermatter/supermatter.dm:1080
    public virtual object should_alert_common()
    {
        if (!(common_radio_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return FALSE;

        common_radio_cooldown = world.time + SUPERMATTER_COMMON_RADIO_DELAY; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:1087
    public virtual void holiday_lights__proc()
    {
        holiday_lights = TRUE;
        RegisterSignal(this, COMSIG_ATOM_ITEM_INTERACTION, nameof(holiday_item_interaction));
        update_appearance();
    }

    /// Consume the santa hat and add it as an overlay
    // tg: code/modules/power/supermatter/supermatter.dm:1093
    [SignalHandler]
    public virtual object holiday_item_interaction(object source = null, mob_living user = null, obj_item item = null, DmList modifiers = null)
    {
        if (item is obj_item_clothing_head_costume_santa)
        {
            QDEL_NULL(ref item);
            RegisterSignal(this, COMSIG_ATOM_EXAMINE, nameof(holiday_hat_examine));
            if (this is obj_machinery_power_supermatter_crystal_shard)
                add_overlay(mutable_appearance(icon, "santa_hat_shard"));
            else
                add_overlay(mutable_appearance(icon, "santa_hat"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return NONE;
    }

    /// Adds the hat flavor text when examined
    // tg: code/modules/power/supermatter/supermatter.dm:1106
    [SignalHandler]
    public virtual void holiday_hat_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_info(I18n.T("There's a santa hat placed atop it. How it got there without being dusted is a mystery.")));
    }
}

// tg: code/modules/power/supermatter/supermatter.dm:931
public partial class obj_machinery : obj
{
    //Zap constants, speeds up targeting (file-local, rules 11.4)
    // tg: code/modules/power/supermatter/supermatter.dm:7-13
    private const int LOWEST = (1);
    private const int OBJECT = (LOWEST + 1);
    private const int MACHINERY = (OBJECT + 1);
    private const int LIVING = (MACHINERY + 1);
    private const int ROD = (LIVING + 1);
    private const int COIL = (ROD + 1);
    private const int BIKE = (COIL + 1);

    // tg: code/modules/power/supermatter/supermatter.dm:931
    public virtual async DmTask<object> supermatter_zap(atom zapstart = null, float? range = null, float? zap_str = null, float? zap_flags = null, DmList targets_hit = null, float? zap_cutoff = null, float? power_level = null, string zap_icon = null, object color = null)
    {
        zapstart ??= this;
        range ??= 5;
        zap_str ??= 3.2f * MEGA * JOULES;
        zap_flags ??= ZAP_SUPERMATTER_FLAGS;
        targets_hit ??= Dm.list();
        zap_cutoff ??= 1.2f * MEGA * JOULES;
        power_level ??= 0;
        zap_icon ??= DEFAULT_ZAP_ICON_STATE;
        object dot = null;
        if (QDELETED(zapstart))
            return dot;
        if (zap_cutoff <= 0)
        {
            stack_trace("/obj/machinery/supermatter_zap() was called with a non-positive value", "code/modules/power/supermatter/supermatter.dm", 935);
            return dot;
        }
        if (zap_str <= 0) // Just in case something scales zap_str and zap_cutoff to 0.
            return dot;
        dot = zapstart.dir;
        //If the strength of the zap decays past the cutoff, we stop
        if (zap_str < zap_cutoff)
            return dot;
        atom target = null;
        float target_type = LOWEST;
        DmList arc_targets = Dm.list();
        //Making a new copy so additons further down the recursion do not mess with other arcs
        //Lets put this ourself into the do not hit list, so we don't curve back to hit the same thing twice with one arc
        foreach (atom test in Dm.each_cast<atom>(oview(zapstart, range)))
        {
            if (!Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_ALLOW_DUPLICATES)) && Dm.truthy(LAZYACCESS(targets_hit, test)))
                continue;

            if (test is obj_vehicle_ridden_bicycle)
            {
                obj_vehicle_ridden_bicycle bike = (obj_vehicle_ridden_bicycle)test;
                if (!HAS_TRAIT(bike, TRAIT_BEING_SHOCKED) && Dm.truthy(bike.can_buckle))//God's not on our side cause he hates idiots.
                {
                    if (target_type != BIKE)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = BIKE;
                }
            }

            if (target_type > COIL)
                continue;

            if (test is obj_machinery_power_energy_accumulator_tesla_coil)
            {
                obj_machinery_power_energy_accumulator_tesla_coil coil = (obj_machinery_power_energy_accumulator_tesla_coil)test;
                if (!HAS_TRAIT(coil, TRAIT_BEING_SHOCKED) && Dm.truthy(coil.anchored) && !Dm.truthy(coil.panel_open) && prob(70))//Diversity of death
                {
                    if (target_type != COIL)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = COIL;
                }
            }

            if (target_type > ROD)
                continue;

            if (test is obj_machinery_power_energy_accumulator_grounding_rod)
            {
                obj_machinery_power_energy_accumulator_grounding_rod rod = (obj_machinery_power_energy_accumulator_grounding_rod)test;
                //We're adding machine damaging effects, rods need to be surefire
                if (Dm.truthy(rod.anchored) && !Dm.truthy(rod.panel_open))
                {
                    if (target_type != ROD)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = ROD;
                }
            }

            if (target_type > LIVING)
                continue;

            if (isliving(test))
            {
                mob_living alive = (mob_living)test;
                if (!HAS_TRAIT(alive, TRAIT_TESLA_SHOCKIMMUNE) && !HAS_TRAIT(alive, TRAIT_BEING_SHOCKED) && alive.stat != DEAD && prob(20))//let's not hit all the engineers with every beam and/or segment of the arc
                {
                    if (target_type != LIVING)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = LIVING;
                }
            }

            if (target_type > MACHINERY)
                continue;

            if (ismachinery(test))
            {
                if (!HAS_TRAIT(test, TRAIT_BEING_SHOCKED) && prob(40))
                {
                    if (target_type != MACHINERY)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = MACHINERY;
                }
            }

            if (target_type > OBJECT)
                continue;

            if (isobj(test))
            {
                if (!HAS_TRAIT(test, TRAIT_BEING_SHOCKED))
                {
                    if (target_type != OBJECT)
                        arc_targets = Dm.list();
                    Dm.append(arc_targets, test);
                    target_type = OBJECT;
                }
            }
        }

        if (Dm.truthy(arc_targets.len))//Pick from our pool
            target = pick(arc_targets) as atom;

        if (QDELETED(target))//If we didn't found something
            return dot;

        //Do the animation to zap to it from here
        if (!Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_ALLOW_DUPLICATES)))
            LAZYSET(ref targets_hit, target, TRUE);
        zapstart.Beam(target, icon_state: zap_icon, time: 0.5f * SECONDS, beam_color: color);
        float zapdir = get_dir(zapstart, target);
        if (Dm.truthy(zapdir))
            dot = zapdir;

        //Going boom should be rareish
        if (prob(80))
            zap_flags = Dm.bitand(zap_flags.Value, bitnot(ZAP_MACHINE_EXPLOSIVE));
        if (target_type == COIL || target_type == ROD)
        {
            float multi = 1;
            float sw__1 = power_level.Value;
            //Between 7k and 9k it's 2, above that it's 4
            if (sw__1 >= SEVERE_POWER_PENALTY_THRESHOLD && sw__1 <= CRITICAL_POWER_PENALTY_THRESHOLD)
                multi = 2;
            else if (sw__1 >= CRITICAL_POWER_PENALTY_THRESHOLD && sw__1 <= INFINITY)
                multi = 4;
            if (Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_SUPERMATTER_FLAGS)))
            {
                object remaining_power = await target.zap_act(zap_str.Value * multi, zap_flags);
                zap_str = Dm.num(remaining_power) / multi; //Coils should take a lot out of the power of the zap
            }
            else
                zap_str /= 3;
        }

        else if (isliving(target))//If we got a fleshbag on our hands
        {
            mob_living creature = (mob_living)target;
            ADD_TRAIT(creature, TRAIT_BEING_SHOCKED, WAS_SHOCKED);
            _addtimer(TRAIT_CALLBACK_REMOVE(creature, TRAIT_BEING_SHOCKED, WAS_SHOCKED), 1 * SECONDS, file: "code/modules/power/supermatter/supermatter.dm", line: 1047);
            //3 shots a human with no resistance. 2 to crit, one to death. This is at at least 10000 power.
            //There's no increase after that because the input power is effectivly capped at 10k
            //Does 1.5 damage at the least
            float shock_damage = (Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_DAMAGE)) ? (power_level.Value / 200) - 10 : rand(5, 10));
            await creature.electrocute_act(shock_damage, "Supermatter Discharge Bolt", 1, (Dm.truthy(Dm.bitand(zap_flags.Value, ZAP_MOB_STUN)) ? SHOCK_TESLA : SHOCK_NOSTUN));
            zap_str /= 1.5f; //Meatsacks are conductive, makes working in pairs more destructive
        }

        else
            zap_str = Dm.num(await target.zap_act(zap_str, zap_flags));

        //This gotdamn variable is a boomer and keeps giving me problems
        turf target_turf = get_turf(target);
        float pressure = 1;
        // Calculate pressure and do electrolysis.
        if (target_turf != null && Dm.truthy(await target_turf.return_air()))
        {
            datum_gas_mixture air_mixture = await target_turf.return_air() as datum_gas_mixture;
            pressure = max(1, Dm.num(air_mixture.return_pressure()));
            air_mixture.electrolyze(working_power: zap_str / 200, electrolyzer_args: Dm.list(Dm.kv(ELECTROLYSIS_ARGUMENT_SUPERMATTER_POWER, power_level)));
            target_turf.air_update_turf();
        }
        //We get our range with the strength of the zap and the pressure, the higher the former and the lower the latter the better
        float new_range = clamp(zap_str.Value / pressure * 10, 2, 7);
        float zap_count = 1;
        if (prob(5))
        {
            zap_str -= (zap_str / 10);
            zap_count += 1;
        }
        for (float j = 1, j__end = zap_count; j <= j__end; j++)
        {
            DmList child_targets_hit = targets_hit;
            if (zap_count > 1)
                child_targets_hit = targets_hit.Copy(); //Pass by ref begone
            await supermatter_zap(target, new_range, zap_str, zap_flags, child_targets_hit, zap_cutoff, power_level, zap_icon, color);
        }
        return dot;
    }
}

// Warp Effect //

// tg: code/modules/power/supermatter/supermatter.dm:1112
public partial class atom_movable_warp_effect : atom_movable
{
    // tg: code/modules/power/supermatter/supermatter.dm:1120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        turf new_turf = get_turf(this);
        if (Dm.truthy(new_turf))
        {
            object new_offset = GET_TURF_PLANE_OFFSET(new_turf);
            ADD_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(new_offset), Dm.@ref(this));
        }
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:1127
    public override object Destroy(float? force = null)
    {
        // Just in case I've forgotten how the movement api works
        object offset = GET_TURF_PLANE_OFFSET(loc);
        REMOVE_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(offset), Dm.@ref(this));
        return base.Destroy(force);
    }

    // tg: code/modules/power/supermatter/supermatter.dm:1133
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        turf new_turf = get_turf(this);
        turf old_turf = get_turf(old_loc);
        if (!Dm.truthy(new_turf))
        {
            object old_offset = GET_TURF_PLANE_OFFSET(old_turf);
            REMOVE_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(old_offset), Dm.@ref(this));
            return dot;
        }
        else if (Dm.truthy(get_turf(old_loc)))
            return dot;
        // If we're in a thing on a turf we COUNT as a distortion source
        object new_offset = GET_TURF_PLANE_OFFSET(new_turf);
        ADD_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(new_offset), Dm.@ref(this));
        return dot;
    }

    // tg: code/modules/power/supermatter/supermatter.dm:1147
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        if (Dm.truthy(old_turf))
        {
            object old_offset = GET_TURF_PLANE_OFFSET(old_turf);
            REMOVE_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(old_offset), Dm.@ref(this));
        }
        if (Dm.truthy(new_turf))
        {
            object new_offset = GET_TURF_PLANE_OFFSET(new_turf);
            ADD_TRAIT(GLOB, TRAIT_DISTORTION_IN_USE(new_offset), Dm.@ref(this));
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/supermatter/supermatter.dm:37  var/static/gl_uid = 1
    internal float obj_machinery_power_supermatter_crystal__gl_uid = 1;

    // tg: code/modules/power/supermatter/supermatter.dm:222  var/static/list/loc_connections = list(COMSIG_TURF_INDUSTRIAL_LIFT_ENTER = PROC_REF(tram_contents_consume))
    internal DmList obj_machinery_power_supermatter_crystal__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_TURF_INDUSTRIAL_LIFT_ENTER, nameof(obj_machinery_power_supermatter_crystal.tram_contents_consume)));
}
