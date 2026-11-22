// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tanks/tanks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the gas tank (M4c slice 1): its air, the internals hooks and sounds, the processing, the tolerances, leaking and
// rupture, examine and the hand paths. M5 slice 7: atom_deconstruct (the air released), atom_break (the leak) and
// atom_destruction (the rupture; its dyn_explosion since M5x slice 3); package E3 (docs/ai/specs/engineering.md): the tgui window (ui_state,
// ui_interact, ui_static_data, ui_data, ui_act); gaps AS1 (D333): the tank bomb (IsSpecialAssembly, receive_signal, bomb_assemble
// and ignite: an assembly holder with an igniter attached, the signalled beep and the welded tank's blast by its mix). Left for its
// milestone: suicide_act (live-partial, M10). The var block is generated (D111). [DmPath] is here: the earliest .dme
// file that declares the type (rules 2.3).

// tg: code/game/objects/items/tanks/tanks.dm:14
[DmPath("/obj/item/tank")]
public partial class obj_item_tank : obj_item
{
    // tg: code/game/objects/items/tanks/tanks.dm:2  #define ASSUME_AIR_DT_FACTOR 1 (file-local, #undef at :580)
    private const float ASSUME_AIR_DT_FACTOR = 1;

    // The earlier of the type's two dropped definitions (rules 7.5: the later one's ..() calls it).
    /// Closes the tank if dropped while open.
    // tg: code/game/objects/items/tanks/tanks.dm:73
    public async DmTask<object> dropped__d0(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        // Close open air tank if its current user got sent to the shadowrealm.
        if (QDELETED(breathing_mob))
        {
            breathing_mob = null;
            return dot;
        }
        // Close open air tank if it got dropped by its current user.
        if (loc != breathing_mob)
            await breathing_mob.cutoff_internals();
        return dot;
    }

    /// Closes the tank if given to another mob while open.
    // tg: code/game/objects/items/tanks/tanks.dm:84
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        // Close open air tank if it was equipped by a mob other than the current user.
        if (breathing_mob != null && user != breathing_mob)
            await breathing_mob.cutoff_internals();
    }

    /// Called by carbons after they connect the tank to their breathing apparatus.
    // tg: code/game/objects/items/tanks/tanks.dm:91
    public virtual void after_internals_opened(mob_living_carbon carbon_target = null)
    {
        breathing_mob = carbon_target;
        playsound(loc, Dm.resource("sound/items/internals/internals_on.ogg"), 15, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        object pressure_on_open = air_contents.return_pressure(); //we start off "full" when we toggle, and count down from there.
        volume_bar = Dm.@new<datum_progressbar>(carbon_target, pressure_on_open, this, pressure_on_open);
    }

    /// Called by carbons after they disconnect the tank from their breathing apparatus.
    // tg: code/game/objects/items/tanks/tanks.dm:98
    public virtual void after_internals_closed(mob_living_carbon carbon_target = null)
    {
        breathing_mob = null;
        playsound(loc, Dm.resource("sound/items/internals/internals_off.ogg"), 15, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        QDEL_NULL(ref volume_bar);
    }

    /// Attempts to toggle the mob's internals on or off using this tank. Returns TRUE if successful.
    // tg: code/game/objects/items/tanks/tanks.dm:104
    public virtual async DmTask<object> toggle_internals(mob_living_carbon mob_target = null)
    {
        return await mob_target.toggle_internals(this);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:107
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await toggle_internals(user as mob_living_carbon);
        return null;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:110
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(tank_holder_icon_state))
            Waiting.NoSleep(AddComponent(typeof(datum_component_container_item_tank_holder), tank_holder_icon_state));

        air_contents = Dm.@new<datum_gas_mixture>(volume); //liters
        air_contents.temperature = T20C;

        populate_gas();

        reaction_info = Dm.list();
        explosion_info = Dm.list();

        // COMSIG_GASMIX_MERGING and COMSIG_GASMIX_REMOVING are not defined in tg, so DM's list(KEY = value) keys them by
        // their names as text (two signals nothing sends).
        Waiting.NoSleep(AddComponent(typeof(datum_component_atmos_reaction_recorder), Dm.named("reset_criteria", Dm.list(Dm.kv("COMSIG_GASMIX_MERGING", air_contents), Dm.kv("COMSIG_GASMIX_REMOVING", air_contents))), Dm.named("target_list", reaction_info)));

        // This is separate from the reaction recorder.
        // In this case we are only listening to determine if the tank is overpressurized but not destroyed.
        RegisterSignal(air_contents, COMSIG_GASMIX_MERGED, nameof(merging_information));

        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:132
    public virtual void populate_gas()
    {
        return;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:135
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        air_contents = null;
        QDEL_NULL(ref tank_assembly);
        QDEL_NULL(ref volume_bar);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:142
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (tank_assembly != null)
        {
            Dm.append(ref dot, tank_assembly.icon_state);
            Dm.append(ref dot, tank_assembly.overlays);
            Dm.append(ref dot, "bomb_assembly");
        }
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:149
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        obj icon = this;
        dot = await base.examine(user, __extra);
        if (loc is obj_item_assembly)
            icon = loc as obj;
        if (!in_range(this, user) && !isobserver(user))
        {
            if (icon == this)
                Dm.append(ref dot, span_notice(I18n.T("If you want any more information you'll need to get closer.")));
            return dot;
        }

        Dm.append(ref dot, span_notice(I18n.T("The pressure gauge reads [] kPa.", round(Dm.num(air_contents.return_pressure()), 0.01f))));

        float celsius_temperature = air_contents.temperature - T0C;
        string descriptive = null;

        if (celsius_temperature < 20)
            descriptive = I18n.T("cold");
        else if (celsius_temperature < 40)
            descriptive = I18n.T("room temperature");
        else if (celsius_temperature < 80)
            descriptive = I18n.T("lukewarm");
        else if (celsius_temperature < 100)
            descriptive = I18n.T("warm");
        else if (celsius_temperature < 300)
            descriptive = I18n.T("hot");
        else
            descriptive = I18n.T("furiously hot");

        Dm.append(ref dot, span_notice(I18n.T("It feels [].", descriptive)));

        if (tank_assembly != null)
            Dm.append(ref dot, span_warning(I18n.T("There is some kind of device [] to the tank!", EXAMINE_HINT(I18n.T("rigged")))));
        return dot;
    }


    // tg: code/game/objects/items/tanks/tanks.dm:182  (M5 slice 7)
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        atom location = loc;
        if (location != null)
        {
            await location.assume_air(air_contents);
            playsound(location, Dm.resource("sound/effects/spray.ogg"), 10, TRUE, -3); // tg: code/game/objects/items/tanks/tanks.dm:186
        }
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:189  suicide_act: live-partial (suicides, M10).

    // tg: code/game/objects/items/tanks/tanks.dm:202
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/objects/items/tanks/tanks.dm:203
        add_fingerprint(user);
        if (!(tool is obj_item_assembly_holder))
            return NONE;
        if (tank_assembly != null)
        {
            balloon_alert(user, I18n.T("something already attached!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await bomb_assemble(tool as obj_item_assembly_holder, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:212
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (tank_assembly != null)
        {
            tool.play_tool_sound(this);
            await bomb_disassemble(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.wrench_act(user, tool);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:219
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(bomb_status))
        {
            balloon_alert(user, I18n.T("already welded!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
        {
            bomb_status = TRUE;
            balloon_alert(user, I18n.T("bomb armed"));
            log_bomber(user, "welded a single tank bomb,", this, Dm.text("| Temp: [] Pressure: []", air_contents.temperature, air_contents.return_pressure()));
            // tg: code/game/objects/items/tanks/tanks.dm:227
            add_fingerprint(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.welder_act(user, tool);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:231
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:234
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Tank", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:240
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("defaultReleasePressure", round(TANK_DEFAULT_RELEASE_PRESSURE)),
            Dm.kv("minReleasePressure", round(TANK_MIN_RELEASE_PRESSURE)),
            Dm.kv("maxReleasePressure", round(TANK_MAX_RELEASE_PRESSURE)),
            Dm.kv("leakPressure", round(TANK_LEAK_PRESSURE)),
            Dm.kv("fragmentPressure", round(TANK_FRAGMENT_PRESSURE))
        );
    }

    // tg: code/game/objects/items/tanks/tanks.dm:249
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list(
            Dm.kv("tankPressure", round(Dm.num(air_contents.return_pressure()))),
            Dm.kv("releasePressure", round(distribute_pressure))
        );

        mob_living_carbon carbon_user = user as mob_living_carbon;
        if (carbon_user == null)
            carbon_user = loc as mob_living_carbon;
        if (carbon_user != null && (carbon_user.external == this || carbon_user.@internal == this))
            dot["connected"] = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:261
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "pressure":
            {
                object pressure = @params["pressure"];
                if (Dm.eq(pressure, "reset"))
                {
                    pressure = Dm.initial(this, (obj_item_tank p) => p.distribute_pressure);
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "min"))
                {
                    pressure = TANK_MIN_RELEASE_PRESSURE;
                    dot = TRUE;
                }
                else if (Dm.eq(pressure, "max"))
                {
                    pressure = TANK_MAX_RELEASE_PRESSURE;
                    dot = TRUE;
                }
                else if (text2num(pressure) != null)
                {
                    pressure = text2num(pressure);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                    distribute_pressure = Dm.clamp(round(Dm.num(pressure)), TANK_MIN_RELEASE_PRESSURE, TANK_MAX_RELEASE_PRESSURE);
                break;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:283
    public override object remove_air(object amount = null)
    {
        START_PROCESSING(SSobj, this);
        return air_contents.remove(amount);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:287
    public override async DmTask<object> return_air()
    {
        START_PROCESSING(SSobj, this);
        return air_contents;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:291
    public override object return_analyzable_air()
    {
        return air_contents;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:294
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        START_PROCESSING(SSobj, this);
        air_contents.merge(giver);
        await handle_tolerances(ASSUME_AIR_DT_FACTOR);
        return TRUE;
    }

    /**
     * Removes some volume of the tanks gases as the tanks distribution pressure.
     *
     * Arguments:
     * - volume_to_return: The amount of volume to remove from the tank.
     */
    // tg: code/game/objects/items/tanks/tanks.dm:306
    public virtual object remove_air_volume(object volume_to_return = null)
    {
        if (air_contents == null)
            return null;

        float tank_pressure = Dm.num(air_contents.return_pressure());
        float actual_distribute_pressure = clamp(tank_pressure, 0, distribute_pressure);

        // Lets do some algebra to understand why this works, yeah?
        // R_IDEAL_GAS_EQUATION is (kPa * L) / (K * mol) by the by, so the units in this equation look something like this
        // kpa * L / (R_IDEAL_GAS_EQUATION * K)
        // Or restated (kpa * L / K) * 1/R_IDEAL_GAS_EQUATION
        // (kpa * L * K * mol) / (kpa * L * K)
        // If we cancel it all out, we get moles, which is the expected unit
        // This sort of thing comes up often in atmos, keep the tool in mind for other bits of code
        float moles_needed = actual_distribute_pressure * Dm.num(volume_to_return) / (R_IDEAL_GAS_EQUATION * air_contents.temperature);

        return remove_air(moles_needed);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:324
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (air_contents == null)
            return null;
        if (!QDELETED(volume_bar))
            volume_bar.update(air_contents.return_pressure());

        //Allow for reactions
        excited = Dm.num(Dm.bitor(excited, Waiting.NoSleep(air_contents.react(this))));
        excited = Dm.num(Dm.bitor(excited, Waiting.NoSleep(handle_tolerances(seconds_per_tick))));
        excited = Dm.bitor(excited, leaking);

        if (!Dm.truthy(excited))
            STOP_PROCESSING(SSobj, this);
        excited = FALSE;

        if (QDELETED(this) || !Dm.truthy(leaking) || air_contents == null)
            return null;
        atom location = loc;
        if (location == null)
            return null;
        datum_gas_mixture leaked_gas = air_contents.remove_ratio(0.25f) as datum_gas_mixture;
        Waiting.NoSleep(location.assume_air(leaked_gas));
        return null;
    }

    /**
     * Handles the minimum and maximum pressure tolerances of the tank.
     *
     * Returns true if it did anything of significance, false otherwise
     * Arguments:
     * - seconds_per_tick: How long has passed between ticks.
     */
    // tg: code/game/objects/items/tanks/tanks.dm:354
    public virtual async DmTask<object> handle_tolerances(object seconds_per_tick = null)
    {
        if (air_contents == null)
            return FALSE;

        float pressure = Dm.num(air_contents.return_pressure());
        float temperature = Dm.num(air_contents.return_temperature());
        if (temperature >= TANK_MELT_TEMPERATURE)
        {
            float temperature_damage_ratio = (temperature - TANK_MELT_TEMPERATURE) / temperature;
            await take_damage(max_integrity * temperature_damage_ratio * Dm.num(seconds_per_tick), BURN, FIRE, FALSE, NONE);
            if (QDELETED(this))
                return TRUE;
        }

        if (pressure >= TANK_LEAK_PRESSURE)
        {
            float pressure_damage_ratio = (pressure - TANK_LEAK_PRESSURE) / (TANK_RUPTURE_PRESSURE - TANK_LEAK_PRESSURE);
            await take_damage(max_integrity * pressure_damage_ratio * Dm.num(seconds_per_tick), BRUTE, BOMB, FALSE, NONE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:373  (M5 slice 7)
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(leaking))
            return dot;

        leaking = TRUE;
        START_PROCESSING(SSobj, this);

        if (Dm.num(atom_integrity) < 0) // So we don't play the alerts while we are exploding or rupturing.
            return dot;
        visible_message(span_warning(I18n.T("[] springs a leak!", this)));
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 10, TRUE, -3); // tg: code/game/objects/items/tanks/tanks.dm:384
        return dot;
    }

    /// Handles rupturing and fragmenting
    // tg: code/game/objects/items/tanks/tanks.dm:387  (M5 slice 7)
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (air_contents == null)
        {
            await base.atom_destruction(damage_flag);
            return;
        }

        /// Handle fragmentation
        float pressure = Dm.num(air_contents.return_pressure());
        if (pressure > TANK_FRAGMENT_PRESSURE)
        {
            if (!(loc is obj_item_transfer_valve))
                log_bomber(get_mob_by_key(fingerprintslast), "was last key to touch", this, "which ruptured explosively");
            //Give the gas a chance to build up more pressure through reacting
            await air_contents.react(this);
            pressure = Dm.num(air_contents.return_pressure());

            // As of writing this this is calibrated to maxcap at 140L and 160atm.
            float power = (Dm.num(air_contents.volume) * (pressure - TANK_FRAGMENT_PRESSURE)) / TANK_FRAGMENT_SCALE;
            log_atmos(Dm.text("[] exploded with a power of [] and a mix of ", type, power), air_contents);
            await dyn_explosion(this, power, flash_range: 1.5f, ignorecap: FALSE);
        }
        await base.atom_destruction(damage_flag);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:406
    public virtual void merging_information()
    {
        if (Dm.num(air_contents.return_pressure()) > TANK_FRAGMENT_PRESSURE)
            Dm.append(ref explosion_info, TANK_MERGE_OVERPRESSURE);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:411
    public virtual object explosion_information()
    {
        return Dm.list(Dm.kv(TANK_RESULTS_REACTION, reaction_info), Dm.kv(TANK_RESULTS_MISC, explosion_info));
    }

    // tg: code/game/objects/items/tanks/tanks.dm:414
    public override async DmTask<object> on_found(mob finder = null) //for mousetraps
    {
        object dot = null;
        dot = await base.on_found(finder);
        if (tank_assembly != null)
            await tank_assembly.on_found(finder);
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:419
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra) //also for mousetraps
    {
        if (Dm.truthy(await base.attack_hand(user, modifiers, __extra)))
            return null;
        if (tank_assembly != null)
            await tank_assembly.attack_hand();
        return null;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:425
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (tank_assembly != null)
        {
            await tank_assembly.attack_self(user);
            return TRUE;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/tanks/tanks.dm:431
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = null;
        dot = await base.attack_self_secondary(user, modifiers);
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:435
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (tank_assembly != null)
            await tank_assembly.setDir(dir);
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:440
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await dropped__d0(user, silent, __extra);
        if (tank_assembly != null)
            await tank_assembly.dropped();
        return dot;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:445
    public override object IsSpecialAssembly()
    {
        return TRUE;
    }

    // tg: code/game/objects/items/tanks/tanks.dm:448
    public override object receive_signal(datum_signal signal = null) //This is mainly called by the sensor through sense() to the holder, and from the holder to here.
    {
        // [icon2html(src, hearers(src))]: the chat icons (section 76)
        audible_message(span_warning(I18n.T("*beep* *beep* *beep*")));
        playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(ignite)), 1 * SECONDS, file: "code/game/objects/items/tanks/tanks.dm", line: 451);
        return null;
    }

    /// Attaches an assembly holder to the tank to create a bomb.
    // tg: code/game/objects/items/tanks/tanks.dm:454
    public virtual async DmTask bomb_assemble(obj_item_assembly_holder assembly = null, mob_living user = null)
    {
        //Check if either part of the assembly has an igniter, but if both parts are igniters, then fuck it
        float igniter_count = 0;
        foreach (obj_item_assembly_igniter attached_assembly in Dm.each<obj_item_assembly_igniter>(assembly.assemblies))
            igniter_count++;

        if (LAZYLEN(assembly.assemblies) == igniter_count)
            return;

        if (isitem(loc)) // we are in a storage item
        {
            balloon_alert(user, I18n.T("can't reach!"));
            return;
        }

        if (Dm.@in(this, user.get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_ACCESSORIES))) && !Dm.truthy(user.canUnEquip(this)))
        {
            balloon_alert(user, I18n.T("it's stuck!"));
            return;
        }

        if (!Dm.truthy(user.canUnEquip(assembly)))
        {
            balloon_alert(user, I18n.T("it's stuck!"));
            return;
        }

        if (!Dm.truthy(await user.transferItemToLoc(assembly, this)))
        {
            balloon_alert(user, I18n.T("it's stuck!"));
            return;
        }

        tank_assembly = assembly; //Tell the tank about its assembly part
        assembly.master = this; //Tell the assembly about its new owner
        await assembly.on_attach();
        update_weight_class(WEIGHT_CLASS_BULKY);

        balloon_alert(user, I18n.T("bomb assembled"));
        update_appearance(UPDATE_OVERLAYS);
    }

    /// Detaches an assembly holder from the tank, disarming the bomb
    // tg: code/game/objects/items/tanks/tanks.dm:488
    public virtual async DmTask bomb_disassemble(mob user = null)
    {
        bomb_status = FALSE;
        balloon_alert(user, I18n.T("bomb disarmed"));
        if (tank_assembly == null)
            throw Dm.CRASH("bomb_disassemble() called on a tank with no assembly!", "code/game/objects/items/tanks/tanks.dm", 492);
        await user.put_in_hands(tank_assembly);
        tank_assembly.master = null;
        tank_assembly = null;
        update_weight_class(Dm.initial(this, (obj_item_tank p) => p.w_class));
        update_appearance(UPDATE_OVERLAYS);
    }

    /// Ignites the contents of the tank. Called when receiving a signal if the tank is welded and has an igniter attached.
    // tg: code/game/objects/items/tanks/tanks.dm:500
    public virtual async DmTask ignite()
    {
        if (!Dm.truthy(bomb_status)) // if it isn't welded, release the gases instead
        {
            release();
            return;
        }

        // check to make sure it's not already exploding before exploding it
        if (Dm.truthy(igniting))
            throw Dm.CRASH(Dm.text("ignite() called multiple times on []", type), "code/game/objects/items/tanks/tanks.dm", 507);
        igniting = TRUE;

        datum_gas_mixture our_mix = await return_air() as datum_gas_mixture;
        our_mix.assert_gases(typeof(datum_gas_plasma), typeof(datum_gas_oxygen));
        float fuel_moles = Dm.num(our_mix.moles[typeof(datum_gas_plasma)]) + Dm.num(our_mix.moles[typeof(datum_gas_oxygen)]) / 6;
        our_mix.garbage_collect();
        datum_gas_mixture bomb_mixture = our_mix.copy() as datum_gas_mixture;
        float strength = 1;

        turf ground_zero = get_turf(loc);

        /// Used to determine what the temperature of the hotspot when it isn't able to explode
        float igniter_temperature = 0;
        foreach (obj_item_assembly_igniter firestarter in Dm.each<obj_item_assembly_igniter>(tank_assembly.assemblies))
            igniter_temperature = Math.Max(igniter_temperature, Dm.num(firestarter.heat));

        if (!Dm.truthy(igniter_temperature))
            throw Dm.CRASH(Dm.text("[] called ignite() without any igniters attached", type), "code/game/objects/items/tanks/tanks.dm", 526);

        if (bomb_mixture.temperature > T0C + 400)
        {
            strength = fuel_moles / 15;

            if (strength >= 2)
                await explosion(ground_zero, devastation_range: round(strength, 1), heavy_impact_range: round(strength * 2, 1), light_impact_range: round(strength * 3, 1), flash_range: round(strength * 4, 1), explosion_cause: this);
            else if (strength >= 1)
                await explosion(ground_zero, devastation_range: round(strength, 1), heavy_impact_range: round(strength * 2, 1), light_impact_range: round(strength * 2, 1), flash_range: round(strength * 3, 1), explosion_cause: this);
            else if (strength >= 0.5f)
                await explosion(ground_zero, heavy_impact_range: 1, light_impact_range: 2, flash_range: 4, explosion_cause: this);
            else if (strength >= 0.2f)
                await explosion(ground_zero, devastation_range: -1, light_impact_range: 1, flash_range: 2, explosion_cause: this);
            else
            {
                await ground_zero.assume_air(bomb_mixture);
                ground_zero.hotspot_expose(igniter_temperature, 125);
            }
        }
        else if (bomb_mixture.temperature > T0C + 250)
        {
            strength = fuel_moles / 20;

            if (strength >= 1)
                await explosion(ground_zero, heavy_impact_range: round(strength, 1), light_impact_range: round(strength * 2, 1), flash_range: round(strength * 3, 1), explosion_cause: this);
            else if (strength >= 0.5f)
                await explosion(ground_zero, devastation_range: -1, light_impact_range: 1, flash_range: 2, explosion_cause: this);
            else
            {
                await ground_zero.assume_air(bomb_mixture);
                ground_zero.hotspot_expose(igniter_temperature, 125);
            }
        }
        else if (bomb_mixture.temperature > T0C + 100)
        {
            strength = fuel_moles / 25;

            if (strength >= 1)
                await explosion(ground_zero, devastation_range: -1, light_impact_range: round(strength, 1), flash_range: round(strength * 3, 1), explosion_cause: this);
            else
            {
                await ground_zero.assume_air(bomb_mixture);
                ground_zero.hotspot_expose(igniter_temperature, 125);
            }
        }
        else
        {
            await ground_zero.assume_air(bomb_mixture);
            ground_zero.hotspot_expose(igniter_temperature, 125);
        }

        qdel(this);
    }

    /// Releases air stored in the tank. Called when signaled without being welded, or when ignited without enough pressure to explode.
    // tg: code/game/objects/items/tanks/tanks.dm:569
    public virtual void release()
    {
        datum_gas_mixture our_mix = Waiting.NoSleep(return_air()) as datum_gas_mixture;
        datum_gas_mixture removed = remove_air(our_mix.total_moles()) as datum_gas_mixture;
        turf T = get_turf(this);
        if (T == null)
            return;
        log_atmos(Dm.text("[] released its contents of ", type), removed);
        Waiting.NoSleep(T.assume_air(removed));
    }
}
