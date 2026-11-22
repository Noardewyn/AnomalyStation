// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/portable/portable_atmospherics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the canisters and portable machines): the init (the air from initial_gas_mix or the room's
// temperature, SSair's processing), Destroy, process_atmos (the reactions), take_atmos_damage, return_air and
// return_analyzable_air, connect, Move, disconnect, unregister_holding and insert_nob_crystal. Deferred leaves: the
// standing height (the elevation element: the client's structure top, D239), the
// screentips (M9); M5 slice 1: the atmos damage itself (take_damage, which DV27's guard keeps from the type until its
// atom_break and atom_fix rows are translated); M5x slice 3: ex_act (a devastated can releases its air). Generated live-partial stubs:
// the map export (M10), the construction and deconstruction (M5), the context tips (M9), the tool, tank and item acts (welder; the wrench onto a connector port, the alt-click tank removal, replace_tank and item_interaction since the M5 leftovers),
// the break and repair and a hit's log (M5). The var block is generated.

// tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:1  #define PORTABLE_ATMOS_IGNORE_ATMOS_LIMIT 0 (file-local, #undef at :339)

// tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:3
public partial class obj_machinery_portable_atmospherics : obj_machinery
{
    private const float PORTABLE_ATMOS_IGNORE_ATMOS_LIMIT = 0;

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:54
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(initial_gas_mix))
            air_contents = SSair.parse_gas_string(initial_gas_mix) as datum_gas_mixture;
        else
        {
            air_contents = Dm.@new<datum_gas_mixture>();
            air_contents.temperature = T20C;
        }
        air_contents.volume = volume;
        SSair.start_processing_machine(this);
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable), Dm.named("climb_time", 3 * SECONDS), Dm.named("climb_stun", 3 * SECONDS)));
        // AddElement(/datum/element/elevation, pixel_shift = 8): standing on it (D239: the client's structure top)
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context (M9)
        Dm.Deferred("code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:64", "Initialize: AddElement(/datum/element/elevation, pixel_shift = 8) (the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)) and register_context (screentips, M10+)");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:77
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(disconnect(destroyed: TRUE));
        air_contents = null;
        if (holding != null)
            unregister_holding();
        SSair.stop_processing_machine(this);

        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:86  (the earlier of the type's two examine definitions: rules 7.5)
    public async DmTask<object> examine__d0(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(nob_crystal_inserted))
            Dm.append(ref dot, I18n.T("There is a hypernoblium crystal inside it that allows for reactions inside to be suppressed."));
        if (Dm.truthy(suppress_reactions))
            Dm.append(ref dot, I18n.T("The hypernoblium crystal inside is glowing with a faint blue colour, indicating reactions inside are currently being suppressed."));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:104
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        excited = (!Dm.truthy(suppress_reactions) && (Dm.truthy(excited) || Dm.truthy(await air_contents.react(this)))) ? TRUE : FALSE;
        if (!Dm.truthy(excited))
            return PROCESS_KILL;
        excited = FALSE;
        return dot.Value;
    }

    /// Take damage if a variable is exceeded. Damage is equal to temp/limit * heat/limit.
    /// The damage multiplier is treated as 1 if something is being ignored while the other one is exceeded.
    /// On most cases only one will be exceeded, so the other one is scaled down.
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:110
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity) || Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || !Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("repairing..."));
        while (Dm.truthy(await tool.use_tool(this, user, 2.5f * SECONDS, volume: 40)))
        {
            atom_integrity = min(Dm.num(atom_integrity) + 25, max_integrity);
            if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
            {
                balloon_alert(user, I18n.T("repaired"));
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("partially repaired..."));
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:136
    public virtual async DmTask<object> take_atmos_damage()
    {
        float taking_damage = FALSE;

        float temp_damage = 1;
        float pressure_damage = 1;

        if (temp_limit != PORTABLE_ATMOS_IGNORE_ATMOS_LIMIT)
        {
            temp_damage = air_contents.temperature / temp_limit;
            taking_damage = temp_damage > 1 ? TRUE : FALSE;
        }

        if (pressure_limit != PORTABLE_ATMOS_IGNORE_ATMOS_LIMIT)
        {
            pressure_damage = Dm.num(air_contents.return_pressure()) / pressure_limit;
            taking_damage = (Dm.truthy(taking_damage) || pressure_damage > 1) ? TRUE : FALSE;
        }

        if (!Dm.truthy(taking_damage))
            return FALSE;

        await take_damage(clamp(temp_damage * pressure_damage, 5, 50), BURN, 0);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:156
    public override async DmTask<object> return_air()
    {
        SSair.start_processing_machine(this);
        return air_contents;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:160
    public override object return_analyzable_air()
    {
        return air_contents;
    }

    /**
     * Allow the portable machine to be connected to a connector
     * Arguments:
     * * new_port - the connector that we trying to connect to
     */
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:168
    public virtual async DmTask<object> connect(obj_machinery_atmospherics_components_unary_portables_connector new_port = null)
    {
        //Make sure not already connected to something else
        if (connected_port != null || new_port == null || new_port.connected_device != null)
            return FALSE;

        //Make sure are close enough for a valid connection
        if (new_port.loc != get_turf(this))
            return FALSE;

        //Perform the connection
        connected_port = new_port;
        connected_port.connected_device = this;
        datum_pipeline connected_port_parent = connected_port.parents[1] as datum_pipeline;
        await connected_port_parent.reconcile_air();

        await set_anchored(TRUE); //Prevent movement
        pixel_x = new_port.pixel_x;
        pixel_y = new_port.pixel_y;

        SSair.start_processing_machine(this);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:191
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        if (Dm.truthy(dot))
            await disconnect();
        return dot;
    }

    /**
     * Allow the portable machine to be disconnected from the connector
     */
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:199
    public virtual async DmTask<object> disconnect(float? destroyed = null)
    {
        destroyed ??= FALSE;
        if (connected_port == null)
            return FALSE;
        connected_port.connected_device = null;
        connected_port = null;
        if (Dm.truthy(destroyed))
            return TRUE;

        await set_anchored(FALSE);
        pixel_x = 0;
        pixel_y = 0;

        SSair.start_processing_machine(this);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:215
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(holding))
            return CLICK_ACTION_BLOCKING;
        to_chat(user, span_notice(I18n.T("You remove [] from [].", holding, this)));
        await replace_tank(user as mob_living, TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await examine__d0(user, __extra);
        if (holding == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("\\The [] contains []. Alt-click [] to remove it.", this, holding, this)) + span_notice(I18n.T("Click [] with another gas tank to hot swap [].", this, holding)));
        return dot;
    }

    /**
     * Allow the player to place a tank inside the machine.
     * Arguments:
     * * User: the player doing the act
     * * close_valve: used in the canister.dm file, check if the valve is open or not
     * * new_tank: the tank we are trying to put in the machine
     */
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:234
    public virtual async DmTask<object> replace_tank(mob_living user = null, object close_valve = null, obj_item_tank new_tank = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return FALSE;
        if (user == null)
            return FALSE;
        if (new_tank != null && !Dm.truthy(await user.transferItemToLoc(new_tank, this)))
            return FALSE;

        if (Dm.truthy(holding) && new_tank != null)//for when we are actually switching tanks
        {
            investigate_log(Dm.text("had its internal [] swapped with [] by [].", holding, new_tank, key_name(user)), INVESTIGATE_ATMOS);
            to_chat(user, span_notice(I18n.T("In one smooth motion you pop [] out of []'s connector and replace it with [].", holding, this, new_tank)));
            await user.put_in_hands(holding);
            UnregisterSignal(holding, COMSIG_QDELETING);
            holding = new_tank;
            RegisterSignal(holding, COMSIG_QDELETING, nameof(unregister_holding));
            playsound(this, insert_sound, sound_vol);
            playsound(this, remove_sound, sound_vol);
        }
        else if (Dm.truthy(holding))//we remove a tank
        {
            investigate_log(Dm.text("had its internal [] removed by [].", holding, key_name(user)), INVESTIGATE_ATMOS);
            to_chat(user, span_notice(I18n.T("You remove [] from [].", holding, this)));
            if (Dm.truthy(await Adjacent(user)))
                await user.put_in_hands(holding);
            else
                await ((atom_movable)holding).forceMove(get_turf(this));
            playsound(this, remove_sound, sound_vol);
            UnregisterSignal(holding, COMSIG_QDELETING);
            holding = null;
        }
        else if (new_tank != null)//we insert the tank
        {
            investigate_log(Dm.text("had [] inserted into it by [].", new_tank, key_name(user)), INVESTIGATE_ATMOS);
            to_chat(user, span_notice(I18n.T("You insert [] into [].", new_tank, this)));
            holding = new_tank;
            playsound(this, insert_sound, sound_vol);
            RegisterSignal(holding, COMSIG_QDELETING, nameof(unregister_holding));
        }

        SSair.start_processing_machine(this);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:274
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_tank tank)
            return NONE;

        if (!Dm.truthy(await replace_tank(user, FALSE, tank)))
            return ITEM_INTERACT_BLOCKING;

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:283
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item wrench = tool;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return FALSE;
        if (Dm.truthy(connected_port))
        {
            investigate_log(Dm.text("was disconnected from [] by [].", connected_port, key_name(user)), INVESTIGATE_ATMOS);
            await disconnect();
            wrench.play_tool_sound(this);
            user.visible_message(
                I18n.T("[] disconnects [].", user, this),
                span_notice(I18n.T("You unfasten [] from the port.", this)),
                span_hear(I18n.T("You hear a ratchet.")));
            update_appearance();
            return TRUE;
        }
        var possible_port = Dm.locate<obj_machinery_atmospherics_components_unary_portables_connector>(loc);
        if (possible_port == null)
        {
            to_chat(user, span_notice(I18n.T("Nothing happens.")));
            return FALSE;
        }
        if (!Dm.truthy(await connect(possible_port)))
        {
            to_chat(user, span_notice(I18n.T("[] failed to connect to the port.", name)));
            return FALSE;
        }
        wrench.play_tool_sound(this);
        user.visible_message(
            I18n.T("[] connects [].", user, this),
            span_notice(I18n.T("You fasten [] to the port.", this)),
            span_hear(I18n.T("You hear a ratchet.")));
        update_appearance();
        investigate_log(Dm.text("was connected to [] by [].", possible_port, key_name(user)), INVESTIGATE_ATMOS);
        return TRUE;
    }

    /// Holding tanks can get to zero integrity and be destroyed without other warnings due to pressure change.
    /// This checks for that case and removes our reference to it.
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:329
    [SignalHandler]
    public virtual void unregister_holding()
    {
        UnregisterSignal(holding, COMSIG_QDELETING);
        holding = null;
    }

    /// Insert Hypernob crystal into the machine
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:336
    public virtual void insert_nob_crystal()
    {
        nob_crystal_inserted = TRUE;
    }
}

// M5 slice 7 (machinery integrity, D148): the portable machine built and taken apart (on_construction, on_deconstruction),
// broken (no damage deflection) and fixed, and hit.
// tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:1
public partial class obj_machinery_portable_atmospherics : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:67
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        await set_anchored(FALSE);
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:71
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(nob_crystal_inserted))
            Dm.@new<obj_item_hypernoblium_crystal>(this);

        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:312
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        damage_deflection = 0;
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:316
    public override async DmTask atom_fix()
    {
        await base.atom_fix();
        damage_deflection = Dm.initial(this, (obj_machinery_portable_atmospherics p) => p.damage_deflection);
    }

    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:320
    public override async DmTask<object> attacked_by(obj_item item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attacked_by(item, user, modifiers, attack_modifiers);
        if (Dm.num(dot) <= 0)
            return dot;
        investigate_log(Dm.text("was smacked with \\a [] by [].", item, key_name(user)), INVESTIGATE_ATMOS);
        // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:325
        add_hiddenprint(user);
        return dot;
    }
}

// tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:93 (interleaved types, rules 1.9; M5x slice 3)
public partial class obj_machinery_portable_atmospherics : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/portable/portable_atmospherics.dm:93  (set waitfor = FALSE, inherited from
    // /atom/ex_act: the wrapper of waiting-model.md 5.4; assume_air may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return FALSE; //Indestructible cans shouldn't release air

        if (Dm.eq(severity, EXPLODE_DEVASTATE) || target == this)
        {
            //This explosion will destroy the can, release its air.
            turf local_turf = get_turf(this);
            await local_turf.assume_air(air_contents);
        }

        return base.ex_act(severity, target, __extra);
    }
}
