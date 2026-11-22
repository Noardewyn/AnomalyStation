// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/cryo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8x slice A (D205): the occupant visual and the cryo cell: its pipe connection (machine_connector.dm), the patient's temperature
// shared with the cell's gas, the beaker's vapour, the eject on death, full health or too little gas, the tools, the construction
// from the board, the picture and the sound loop, the Cryo window's data, and (M9 slice D) the medical radio announcements through
// the announcement system (aas_config_announce, D209). The game logs are deferred leaves; the z-level plane change
// (on_changed_z_level, one z) and the beaker's share of an explosion (contents_explosion) are not translated (section 57). The var
// blocks are generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:1  the file-local defines (#undef at :686-690)
public partial class obj_machinery_cryo_cell
{
    ///Max temperature allowed inside the cryotube, should break before reaching this heat
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:2
    private const float MAX_TEMPERATURE = 4000;
    // Multiply factor is used with efficiency to multiply Tx quantity
    // Tx quantity is how much volume should be removed from the cell's beaker - multiplied by seconds_per_tick
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:7
    private const float CRYO_MULTIPLY_FACTOR = 25;
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:8
    private const float CRYO_TX_QTY = 0.5f;
    // The minimum O2 moles in the cryotube before it switches off.
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:10
    private const float CRYO_MIN_GAS_MOLES = 5;
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:11
    private const float CRYO_BREAKOUT_TIME = 30 * SECONDS;
}

/// This is a visual helper that shows the occupant inside the cryo cell.
// tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:14
public partial class atom_movable_visual_cryo_occupant : atom_movable_visual
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:27
    [DmParams("mapload", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_cryo_cell parent = Dm.arg<object>(__extra, 0) as obj_machinery_cryo_cell;
        object dot = base.Initialize(mapload, __extra);
        // Alpha masking
        // It will follow this as the animation goes, but that's no problem as the "mask" icon state
        // already accounts for this.
        add_filter("alpha_mask", 1, Dm.list(Dm.kv("type", "alpha"), Dm.kv("icon", Dm.icon(Dm.resource("icons/obj/medical/cryogenics.dmi"), "mask")), Dm.kv("y", -22)));
        RegisterSignal(parent, COMSIG_MACHINERY_SET_OCCUPANT, nameof(on_set_occupant));
        RegisterSignal(parent, COMSIG_CRYO_SET_ON, nameof(on_set_on));
        return dot;
    }

    /// COMSIG_MACHINERY_SET_OCCUPANT callback
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:37
    [SignalHandler]
    public virtual void on_set_occupant(datum source = null, atom_movable new_occupant = null)
    {
        if (occupant != null)
        {
            Dm.remove(vis_contents, occupant);
            occupant.vis_flags = Dm.bitand(occupant.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
            occupant.remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_FORCED_STANDING), CRYO_TRAIT);
        }

        occupant = new_occupant as mob_living;
        if (occupant == null)
            return;

        Waiting.NoSleep(occupant.setDir(SOUTH));
        // We want to pull our occupant up to our plane so we look right
        occupant.vis_flags = Dm.bitor(occupant.vis_flags, VIS_INHERIT_PLANE);
        Dm.append(vis_contents, occupant);
        pixel_y = 22;
        // Keep them standing! They'll go sideways in the tube when they fall asleep otherwise.
        occupant.add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_FORCED_STANDING), CRYO_TRAIT);
    }

    /// COMSIG_CRYO_SET_ON callback
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:58
    [SignalHandler]
    public virtual void on_set_on(datum source = null, object on = null)
    {
        if (Dm.truthy(on))
        {
            animate(this, Dm.named("pixel_y", 24), Dm.named("time", 20), Dm.named("loop", -1));
            animate(Dm.named("pixel_y", 22), Dm.named("time", 20));
        }
        else
            animate(this);
    }
}

/// Cryo cell
// tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:68
public partial class obj_machinery_cryo_cell : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:113
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // tg writes new(mapload, src): the first argument of an atom's new is its loc, and a number is no loc
        occupant_vis = Dm.@new<atom_movable_visual_cryo_occupant>(null, this);
        Dm.append(vis_contents, occupant_vis);
        internal_connector = Dm.@new<datum_gas_machine_connector>(loc, this, dir, CELL_VOLUME * 0.5f);
        soundloop = Dm.@new<datum_looping_sound_cryo_cell>(this);

        register_context();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:123
    public override object Destroy(float? force = null)
    {
        on = FALSE;

        vis_contents.Cut();
        QDEL_NULL(ref occupant_vis);
        QDEL_NULL(ref beaker);
        QDEL_NULL(ref internal_connector);
        QDEL_NULL(ref soundloop);

        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:134
    [ShouldNotOverride]
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (!QDELETED(occupant))
        {
            occupant.vis_flags = Dm.bitand(occupant.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
            REMOVE_TRAIT(occupant, TRAIT_IMMOBILIZED, CRYO_TRAIT);
            REMOVE_TRAIT(occupant, TRAIT_FORCED_STANDING, CRYO_TRAIT);
        }

        await base.handle_deconstruct(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:144
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (beaker != null)
            await beaker.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:161
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
            beaker = null;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:166
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_CTRL_LMB] = Dm.text("Turn []", Dm.truthy(on) ? "off" : "on");
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[] door", Dm.truthy(state_open) ? "Close" : "Open");
        if (held_item == null)
            return CONTEXTUAL_SCREENTIP_SET;

        if (QDELETED(beaker) && istype(held_item, typeof(obj_item_reagent_containers_cup)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert beaker";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            if (!Dm.truthy(state_open) && !Dm.truthy(panel_open) && !Dm.truthy(is_operational))
                context[SCREENTIP_CONTEXT_LMB] = "Pry Open";
            else if (Dm.truthy(panel_open))
                context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]", Dm.truthy(panel_open) ? "Rotate" : "");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:188
    public override async DmTask<object> examine(mob user = null, params object[] __extra) //this is leaving out everything but efficiency since they follow the same idea of "better beaker, better results"
    {
        object dot = await base.examine(user, __extra);
        if (in_range(user, this) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Efficiency at <b>[]</b>%.", efficiency * 100)));
            if (Dm.truthy(occupant))
            {
                if (Dm.truthy(on))
                    Dm.append(ref dot, span_notice(I18n.T("Someone's inside []!", this)));
                else
                    Dm.append(ref dot, span_notice(I18n.T("You can barely make out a form floating in [].", this)));
            }
            else
                Dm.append(ref dot, span_notice(I18n.T("[] seems empty.", this)));
            if (beaker != null)
                Dm.append(ref dot, span_notice(I18n.T("A beaker of []u capacity is located inside.", ((datum_reagents)beaker.reagents).maximum_volume)));
            else
                Dm.append(ref dot, span_warning(I18n.T("Its missing a beaker.")));

            Dm.append(ref dot, span_notice(I18n.T("Use [] to [] the machine.", EXAMINE_HINT(I18n.T("Alt-Click")), Dm.truthy(state_open) ? "Close" : "Open")));
            Dm.append(ref dot, span_notice(I18n.T("Use [] to turn [] the machine.", EXAMINE_HINT(I18n.T("Ctrl-Click")), Dm.truthy(on) ? "Off" : "On")));

            Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] open.", EXAMINE_HINT(I18n.T("screwed")))));
            if (Dm.truthy(panel_open))
            {
                Dm.append(ref dot, span_notice(I18n.T("[] can be [] apart.", this, EXAMINE_HINT(I18n.T("pried")))));
                Dm.append(ref dot, span_notice(I18n.T("[] can be rotated with a [].", this, EXAMINE_HINT(I18n.T("wrench")))));
            }
            else if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
                Dm.append(ref dot, span_notice(I18n.T("[] can be [] open.", this, EXAMINE_HINT(I18n.T("pried")))));
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:214
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        SET_PLANE_IMPLICIT(this, Dm.initial(this, (atom p) => p.plane));
        return await base.update_icon(updates, __extra);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:218
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(state_open) ? "pod-open" : ((Dm.truthy(on) && Dm.truthy(is_operational)) ? "pod-on" : "pod-off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:222
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "pod-panel");
        if (Dm.truthy(state_open))
            return dot;
        Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/medical/cryogenics.dmi"), Dm.text("cover-[]", Dm.truthy(on) && Dm.truthy(is_operational) ? "on" : "off"), ABOVE_ALL_MOB_LAYER, this, plane: ABOVE_GAME_PLANE));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:230
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (!istype(tool, typeof(obj_item_reagent_containers_cup)))
            return NONE;
        if (!QDELETED(beaker))
        {
            balloon_alert(user, I18n.T("beaker present!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        beaker = tool as obj_item_reagent_containers_cup;
        balloon_alert(user, I18n.T("beaker inserted"));
        user.log_message(Dm.text("added \\a [] to cryo containing [].", tool, pretty_string_from_reagent_list(((datum_reagents)tool.reagents).reagent_list)), LOG_GAME);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:248
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(occupant))
        {
            balloon_alert(user, I18n.T("occupant inside!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:259
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off!"));
            return ITEM_INTERACT_BLOCKING;
        }

        bool can_crowbar = false;
        if (!Dm.truthy(state_open) && !Dm.truthy(panel_open) && !Dm.truthy(is_operational)) //can pry open
            can_crowbar = true;
        else if (Dm.truthy(panel_open)) //can deconstruct
            can_crowbar = true;
        if (!can_crowbar)
            return ITEM_INTERACT_BLOCKING;

        obj_machinery_atmospherics node = internal_connector.gas_connector.nodes[1] as obj_machinery_atmospherics;
        float internal_pressure = 0;

        if (node is obj_machinery_atmospherics_components_unary_portables_connector portable_devices_connector)
            internal_pressure = !Dm.truthy(portable_devices_connector.connected_device) ? 1 : 0;

        datum_gas_mixture inside_air = internal_connector.gas_connector.airs[1] as datum_gas_mixture;
        if (Dm.num(inside_air.total_moles()) > 0)
        {
            if (node == null || internal_pressure > 0)
            {
                datum_gas_mixture environment_air = await loc.return_air() as datum_gas_mixture;
                internal_pressure = Dm.num(inside_air.return_pressure()) - Dm.num(environment_air.return_pressure());
            }
        }

        bool unsafe_release = false;
        if (internal_pressure > 2 * ONE_ATMOSPHERE)
        {
            to_chat(user, span_warning(I18n.T("As you begin prying \\the [] a gush of air blows in your face... maybe you should reconsider?", this)));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            unsafe_release = true;
        }

        object deconstruct_it = FALSE;
        // DEVIATION(DV116): tg passes the crowbar as the user (default_pry_open(tool)), which reads a null crowbar's tool_behaviour
        if (!Dm.truthy(Dm.bitand(await default_pry_open(user, tool), ITEM_INTERACT_SUCCESS)))
            deconstruct_it = can_crowbar_deconstruct();

        if (unsafe_release)
            await internal_connector.gas_connector.unsafe_pressure_release(user, internal_pressure);

        tool.play_tool_sound(this, 50);
        if (Dm.truthy(deconstruct_it))
            await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:304
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(occupant))
        {
            balloon_alert(user, I18n.T("occupant inside!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(state_open))
        {
            balloon_alert(user, I18n.T("close first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(await default_change_direction_wrench(user, tool)))
        {
            update_appearance(UPDATE_ICON);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:326
    public override void set_occupant(atom_movable new_occupant = null)
    {
        base.set_occupant(new_occupant);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:330
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        float max_tier = 0;
        foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            max_tier += Dm.num(bin.tier);

        efficiency = Dm.initial(this, (obj_machinery_cryo_cell c) => c.efficiency) * max_tier;
        heat_capacity = Dm.initial(this, (obj_machinery_cryo_cell c) => c.heat_capacity) / max_tier;
        conduction_coefficient = Dm.initial(this, (obj_machinery_cryo_cell c) => c.conduction_coefficient) * max_tier;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:341
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        subset ??= Dm.list();
        //only drop mobs when opening the machine
        foreach (mob_living living_guy in Dm.each<mob_living>(contents))
            Dm.append(ref subset, living_guy);
        await base.dump_inventory_contents(subset);
    }

    /**
     * Turns the machine on/off
     *
     * Arguments
     * * active - TRUE to turn the machine on, FALSE to turn it off
     */
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:353
    [DmPrivate]
    public virtual object set_on(object active = null)
    {
        if (Dm.eq(on, active))
            return null;

        if (Dm.truthy(this._listen_lookup?[COMSIG_CRYO_SET_ON]))
            this._SendSignal(COMSIG_CRYO_SET_ON, Dm.list(this, active));
        object dot = on;
        on = Dm.num(active);
        update_appearance(UPDATE_ICON);

        update_use_power(Dm.truthy(on) ? ACTIVE_POWER_USE : IDLE_POWER_USE);
        if (Dm.truthy(on)) //Turned on
            begin_processing();
        else //Turned off
            end_processing();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:370
    public override void begin_processing()
    {
        base.begin_processing();
        SSair.start_processing_machine(this);
        if (soundloop != null)
            soundloop.start();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:376
    public override void end_processing()
    {
        base.end_processing();
        SSair.stop_processing_machine(this);
        if (soundloop != null)
            soundloop.stop();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:382
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        //Turned off
        if (Dm.truthy(old_value))
            set_on(FALSE);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:387  (set waitfor = FALSE, inherited from
    // /datum/process: the wrapper of waiting-model.md 5.4; open_machine and the beaker's transfer may sleep)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        if (!Dm.truthy(on) || QDELETED(occupant))
        {
            //somehow an deleting mob is inside us. dump everything out
            if (occupant != null && QDELING(occupant))
            {
                await open_machine();
                on = FALSE; //in case panel was open we need to set to FALSE explicitly
            }

            //if not on end processing
            if (!Dm.truthy(on))
            {
                set_on(FALSE); //this explicitly disables processing so is nessassary
                dot.Value = PROCESS_KILL;
            }

            return dot.Value;
        }

        mob_living mob_occupant = occupant as mob_living;
        if (Dm.truthy(mob_occupant.on_fire))
            mob_occupant.extinguish_mob();
        if (mob_occupant.stat == DEAD) // Notify doctors and potentially eject if the patient is dead
        {
            set_on(FALSE);
            await aas_config_announce(typeof(datum_aas_config_entry_medical_cryo_announcements), Dm.list(Dm.kv("EJECTING", autoeject)), this, Dm.list(broadcast_channel), "Deceased");
            if (Dm.truthy(autoeject)) // Eject if configured.
                await open_machine();
            playsound(this, Dm.resource("sound/machines/cryo_warning.ogg"), 100);
            return PROCESS_KILL;
        }

        // Don't bother with fully healed people.
        if (Dm.num(mob_occupant.get_organic_health()) >= Dm.num(mob_occupant.getMaxHealth()))
        {
            if (mob_occupant is mob_living_carbon C)
            {
                if (Dm.truthy(C.all_wounds))
                {
                    if (!Dm.truthy(treating_wounds)) // if we have wounds and haven't already alerted the doctors we're only dealing with the wounds, let them know
                    {
                        treating_wounds = TRUE;
                        playsound(this, Dm.resource("sound/machines/cryo_warning.ogg"), 100); // Bug the doctors.
                        await aas_config_announce(typeof(datum_aas_config_entry_medical_cryo_announcements), Dm.list(), this, Dm.list(broadcast_channel), "Wound Treatment");
                    }
                }
                else // otherwise if we were only treating wounds and now we don't have any, turn off treating_wounds so we can boot 'em out
                    treating_wounds = FALSE;
            }

            if (!Dm.truthy(treating_wounds))
            {
                set_on(FALSE);
                playsound(this, Dm.resource("sound/machines/cryo_warning.ogg"), 100); // Bug the doctors.
                await aas_config_announce(typeof(datum_aas_config_entry_medical_cryo_announcements), Dm.list(Dm.kv("EJECTING", autoeject)), this, Dm.list(broadcast_channel), "Fully Recovered");
                if (Dm.truthy(autoeject)) // Eject if configured.
                    await open_machine();
                return PROCESS_KILL;
            }
        }

        datum_gas_mixture air1 = internal_connector.gas_connector.airs[1] as datum_gas_mixture;
        if (!QDELETED(beaker) && Dm.num(air1.total_moles()) > CRYO_MIN_GAS_MOLES)
        {
            await ((datum_reagents)beaker.reagents).trans_to(
                occupant,
                (CRYO_TX_QTY / (efficiency * CRYO_MULTIPLY_FACTOR)) * Dm.num(seconds_per_tick),
                efficiency * CRYO_MULTIPLY_FACTOR,
                methods: VAPOR
            );
        }
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:441  (set waitfor = FALSE, the slot's, _machinery.dm:322)
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot);
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on))
            return PROCESS_KILL;

        datum_gas_mixture air1 = internal_connector.gas_connector.airs[1] as datum_gas_mixture;

        //check for workable conditions
        if (!Dm.truthy(internal_connector.gas_connector.nodes[1]) || air1 == null || !Dm.truthy(Dm.length(air1.moles)) || Dm.num(air1.total_moles()) < CRYO_MIN_GAS_MOLES) // Turn off if the machine won't work.
        {
            set_on(FALSE);
            await aas_config_announce(typeof(datum_aas_config_entry_medical_cryo_announcements), Dm.list(Dm.kv("EJECTING", autoeject)), this, Dm.list(broadcast_channel), "Insufficient Gas");
            if (Dm.truthy(autoeject)) // Eject if configured.
                await open_machine();
            return PROCESS_KILL;
        }

        //take damage from high temperatures
        if (air1.temperature > 2000)
            await take_damage(clamp(air1.temperature / 200, 10, 20), BURN);

        //adjust temperature of mob
        if (!QDELETED(occupant))
        {
            mob_living mob_occupant = occupant as mob_living;
            float cold_protection = 0;
            float temperature_delta = air1.temperature - mob_occupant.bodytemperature; // The only semi-realistic thing here: share temperature between the cell and the occupant.

            if (mob_occupant is mob_living_carbon_human H)
                cold_protection = Dm.num(H.get_cold_protection(air1.temperature));

            if (abs(temperature_delta) > 1)
            {
                float air_heat_capacity = Dm.num(air1.heat_capacity());
                float heat = ((1 - cold_protection) * 0.1f + conduction_coefficient) * CALCULATE_CONDUCTION_ENERGY(temperature_delta, heat_capacity, air_heat_capacity);

                mob_occupant.adjust_bodytemperature(heat / heat_capacity, TCMB);
                air1.temperature = clamp(air1.temperature - heat / air_heat_capacity, TCMB, MAX_TEMPERATURE);

                //lets have the core temp match the body temp in humans
                if (mob_occupant is mob_living_carbon_human humi)
                    humi.adjust_coretemperature(humi.bodytemperature - humi.coretemperature);
            }
        }

        //spread temperature changes throughout the pipenet
        internal_connector.gas_connector.update_parents();
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:484
    public override async DmTask<object> handle_internal_lifeform(mob lifeform_inside_me = null, object breath_request = null)
    {
        if (Dm.num(breath_request) <= 0)
            return null;

        //return breathable air
        datum_gas_mixture air1 = internal_connector.gas_connector.airs[1] as datum_gas_mixture;
        float breath_percentage = Dm.num(breath_request) / Dm.num(air1.volume);
        object dot = air1.remove(Dm.num(air1.total_moles()) * breath_percentage);

        //update molar changes throughout the pipenet
        internal_connector.gas_connector.update_parents();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:496
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        return ((datum_gas_mixture)internal_connector.gas_connector.airs[1]).merge(giver);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:499
    public override async DmTask<object> return_temperature()
    {
        datum_gas_mixture internal_air = internal_connector.gas_connector.airs[1] as datum_gas_mixture;

        return Dm.num(internal_air.total_moles()) > CRYO_MIN_GAS_MOLES ? internal_air.temperature : await base.return_temperature();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:504
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        if (!Dm.truthy(state_open) && !Dm.truthy(panel_open))
            set_on(FALSE);
        flick("pod-open-anim", this);
        return await base.open_machine(drop, density_to_set);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:510
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        object dot = null;
        treating_wounds = FALSE;
        if (Dm.truthy(state_open) && !Dm.truthy(panel_open))
        {
            flick("pod-close-anim", this);
            dot = await base.close_machine(target, density_to_set);
            if (!QDELETED(occupant)) //auto on if an occupant is inside
                set_on(TRUE);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:518
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_notice(I18n.T("You see [] kicking against the glass of []!", user, this)),
            span_notice(I18n.T("You struggle inside [], kicking the release with your foot... (this will take about [].)", this, DisplayTimeText(CRYO_BREAKOUT_TIME))),
            span_hear(I18n.T("You hear a thump from [].", this)));
        if (Dm.truthy(await do_after(user, CRYO_BREAKOUT_TIME, target: this, cog_icon: null)))
        {
            if (!Dm.truthy(user) || IS_UNCONSCIOUS_OR_CRIT(user) || user.loc != this)
                return null;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:530
    public override object ui_state(mob user = null)
    {
        return GLOB.notcontained_state;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:533
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Cryo", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:539
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["T0C"] = T0C;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:543
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["isOperating"] = on;
        dot["isOpen"] = state_open;
        dot["autoEject"] = autoeject;

        DmList occupant_data = null;
        if (!QDELETED(occupant))
        {
            mob_living mob_occupant = occupant as mob_living;
            occupant_data = Dm.list();

            occupant_data["name"] = mob_occupant.name;
            if (mob_occupant.stat == DEAD)
                occupant_data["stat"] = "Dead";
            else if (IS_UNCONSCIOUS(mob_occupant))
                occupant_data["stat"] = "Unconscious";
            else
                occupant_data["stat"] = "Stable";

            occupant_data["bodyTemperature"] = round(mob_occupant.bodytemperature, 1);

            occupant_data["health"] = mob_occupant.health;
            occupant_data["maxHealth"] = mob_occupant.maxHealth;
            occupant_data["bruteLoss"] = mob_occupant.get_brute_loss();
            occupant_data["oxyLoss"] = mob_occupant.get_oxy_loss();
            occupant_data["toxLoss"] = mob_occupant.get_tox_loss();
            occupant_data["fireLoss"] = mob_occupant.get_fire_loss();
        }
        dot["occupant"] = occupant_data;

        datum_gas_mixture air1 = internal_connector.gas_connector.airs[1] as datum_gas_mixture;
        dot["cellTemperature"] = air1.temperature;

        DmList beaker_data = null;
        if (!QDELETED(beaker))
        {
            datum_reagents beaker_reagents = (datum_reagents)beaker.reagents;
            beaker_data = Dm.list();
            beaker_data["maxVolume"] = beaker.volume;
            beaker_data["currentVolume"] = beaker_reagents.total_volume;
            DmList beakerContents = Dm.list();
            if (Dm.truthy(Dm.length(beaker_reagents.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each<datum_reagent>(beaker_reagents.reagent_list))
                    Dm.append(ref beakerContents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING))))); // list in a list because Byond merges the first list...
            }
            beaker_data["contents"] = beakerContents;
        }
        dot["beaker"] = beaker_data;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:587
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                if (Dm.truthy(on))
                    set_on(FALSE);
                else if (!Dm.truthy(state_open))
                    set_on(TRUE);
                return TRUE;

            case "door":
                if (Dm.truthy(state_open) && get_turf(ui.user) != get_turf(this))
                    await close_machine();
                else
                    await open_machine();
                return TRUE;

            case "autoeject":
                autoeject = Dm.not(autoeject);
                return TRUE;

            case "eject":
                if (!QDELETED(beaker))
                {
                    mob_living user = ui.user as mob_living;
                    if (Dm.truthy(await Adjacent(user)) && !issilicon(user))
                        await user.put_in_hands(beaker);
                    else
                        await beaker.forceMove(drop_location() as atom);
                    return TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:620
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        //must not be in the machine or on its turf to interact
        if (get_turf(user) == get_turf(this))
            return FALSE;
        return await base.can_interact(user, require_adjacent_turf);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:626
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (Dm.truthy(is_operational) && !Dm.truthy(state_open))
        {
            set_on(Dm.not(on));
            balloon_alert(user, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
            return CLICK_ACTION_SUCCESS;
        }
        return CLICK_ACTION_BLOCKING;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:633
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        //Required so players don't close the cryo on themselves without a doctor's help
        if (get_turf(user) == get_turf(this))
            return CLICK_ACTION_BLOCKING;

        if (Dm.truthy(state_open))
            await close_machine();
        else
            await open_machine();
        balloon_alert(user, I18n.T("door []", Dm.truthy(state_open) ? "opened" : "closed"));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:645
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        mob target = dropped as mob;
        if (!iscarbon(target))
            return null;

        if (isliving(target))
        {
            mob_living living_mob = target as mob_living;
            if (Dm.truthy(living_mob.incapacitated))
                await close_machine(target);
            return null;
        }

        user.visible_message(span_notice(I18n.T("[] starts shoving [] inside [].", user, target, this)), span_notice(I18n.T("You start shoving [] inside [].", target, this)));
        if (Dm.truthy(await do_after(user, 2.5f * SECONDS, target: target)))
            await close_machine(target);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:659
    public override void get_remote_view_fullscreens(mob user = null)
    {
        user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 1);
    }
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:662  (M9 slice D: the announcement system's entry)
public partial class datum_aas_config_entry_medical_cryo_announcements : datum_aas_config_entry
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/cryo.dm:675
    public override object compile_announce(DmList variables_map = null, object announcement_line = null)
    {
        variables_map["AUTOEJECTING"] = Dm.truthy(variables_map["EJECTING"]) ? announcement_lines_map["Autoejecting"] : "";
        DmList exploded_string = splittext_char(base.compile_announce(variables_map, announcement_line), "[NO DATA]") as DmList;
        DmList trimed_message = Dm.list();
        foreach (object line_in in Dm.each(exploded_string))
        {
            object line = trim(line_in);
            if (Dm.truthy(line))
                Dm.append(ref trimed_message, line);
        }
        // Rebuild the string without empty lines
        return trimed_message.Join(" ", 1, 0);
    }
}
