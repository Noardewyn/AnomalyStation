// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/anomaly/anomaly_refinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the anomaly refinery. A raw anomaly core and a ready transfer valve go in; the implosion opens the valve, and
// the blast of the valve's tank, caught inside the machine (COMSIG_ATOM_INTERNAL_EXPLOSION), compresses the core when its
// theoretical range reaches the core's required radius (four tiles for the first core of a kind, up to twenty for the last one
// the station may make), shakes the room either way and is cancelled. The test times out after five SSobj ticks and ejects what
// is inside with the result. The bomb simulator merges the valve's two tanks in the machine and steps their reactions for the
// window; an emagged refinery throws the live valve out instead. Its AnomalyRefinery window, its off state with the panel open.
// The var block is generated (D111) but for inserted_core, which tg fills with the finished core after the implosion.

/**
 * # Explosive compressor machines
 *
 * The explosive compressor machine used in anomaly core production.
 *
 * Uses the standard ordnance/tank explosion scaling to compress raw anomaly cores into completed ones. The required explosion radius increases as more cores of that type are created.
 */
// tg: code/modules/research/anomaly/anomaly_refinery.dm:13
public partial class obj_machinery_research_anomaly_refinery : obj_machinery_research
{
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:1  #define MAX_RADIUS_REQUIRED 20 //maxcap (file-local)
    private const float MAX_RADIUS_REQUIRED = 20;
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:2  #define MIN_RADIUS_REQUIRED 4 //1, 2, 4 (file-local)
    private const float MIN_RADIUS_REQUIRED = 4;
    /// How long the compression test can last before the machine just gives up and ejects the items.
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:4  #define COMPRESSION_TEST_TIME (SSOBJ_DT SECONDS * 5) (file-local)
    private static float COMPRESSION_TEST_TIME() => SSOBJ_DT() * SECONDS * 5;

    /// The raw core inserted in the machine.
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:23  (typed object: tg stores the finished anomaly core in it after the
    // implosion, :244, which the timeout then ejects; each use casts, rules 3.2 "typed vars that hold wider values")
    public object inserted_core;

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:14  (the type's defaults: the class declares a var by hand, rules 10.2)
    public obj_machinery_research_anomaly_refinery()
    {
        name = "anomaly refinery";
        desc = "An advanced machine capable of implosion-compressing raw anomaly cores into finished artifacts. Also equipped with state of the art bomb prediction software.";
        circuit = typeof(obj_item_circuitboard_machine_anomaly_refinery);
        icon = Dm.resource("icons/obj/machines/research.dmi");
        base_icon_state = "explosive_compressor";
        icon_state = "explosive_compressor";
        density = 1;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_INTERNAL_EXPLOSION, nameof(check_test));
        return dot;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:45
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_notice(I18n.T("A small panel on [] side is dislaying a notice. Something about firmware?", p_their())));
        return dot;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:51
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        await DmTask.CompletedTask;
        return null; // Required to make the TTV not vent directly into the air.
    }

    /**
     * Determines how much explosive power (last value, so light impact theoretical radius) is required to make a certain anomaly type.
     *
     * Returns null if the max amount has already been reached.
     *
     * Arguments:
     * * anomaly_type - anomaly type define
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:62
    public virtual object get_required_radius(object anomaly_type = null)
    {
        if (!SSresearch.is_core_available(anomaly_type))
            return null; //return null

        float already_made = Dm.num(SSresearch.created_anomaly_types[anomaly_type]);
        float hard_limit = Dm.num(SSresearch.anomaly_hard_limit_by_type[anomaly_type]);

        // my crappy autoscale formula
        // linear scaling.
        float radius_span = MAX_RADIUS_REQUIRED - MIN_RADIUS_REQUIRED;
        float radius_increase_per_core = radius_span / hard_limit;
        float radius = clamp(round(MIN_RADIUS_REQUIRED + radius_increase_per_core * already_made, 1), MIN_RADIUS_REQUIRED, MAX_RADIUS_REQUIRED);
        return radius;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:76
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            to_chat(user, span_warning(I18n.T("You can't insert [] into [] while [] currently active.", tool, this, p_theyre())));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(tool, typeof(obj_item_raw_anomaly_core)))
        {
            if (inserted_core != null)
            {
                to_chat(user, span_warning(I18n.T("There is already a core in [].", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand.", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_raw_anomaly_core raw_core = tool as obj_item_raw_anomaly_core;
            if (!Dm.truthy(get_required_radius(raw_core.anomaly_type)))
            {
                await say(I18n.T("Unfortunately, due to diminishing supplies of condensed anomalous matter, [] and any cores of its type are no longer of a sufficient quality level to be compressed into a working core.", raw_core));
                return ITEM_INTERACT_BLOCKING;
            }

            inserted_core = raw_core;
            to_chat(user, span_notice(I18n.T("You insert [] into [].", raw_core, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!istype(tool, typeof(obj_item_transfer_valve)))
            return NONE;

        if (inserted_bomb != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a bomb in [].", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_transfer_valve valve = tool as obj_item_transfer_valve;
        if (!Dm.truthy(valve.ready()))
        {
            to_chat(user, span_warning(I18n.T("[] is incomplete.", valve)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand.", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        inserted_bomb = tool as obj_item_transfer_valve;
        tank_to_target = inserted_bomb.tank_two;
        to_chat(user, span_notice(I18n.T("You insert [] into []", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:120
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.str(Dm.truthy(panel_open) ? Dm.text("[]-off", base_icon_state) : base_icon_state);
        return dot;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:124
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:129
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:132
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:135
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("already hacked!"));
            return null;
        }

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        playsound(this, "sound/machines/buzz/buzz-sigh.ogg", 50, vary: FALSE);
        await say(I18n.T("ERROR: Unauthorized firmware access."));
        return TRUE;
    }

    /**
     * Starts a compression test.
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:149
    public virtual async DmTask start_test()
    {
        if (Dm.truthy(active))
        {
            await say(I18n.T("ERROR: Already running a compression test."));
            return;
        }

        if (!istype(inserted_core, typeof(obj_item_raw_anomaly_core)) || !istype(inserted_bomb, typeof(obj_item_transfer_valve)))
        {
            await end_test(I18n.T("ERROR: Missing equipment. Items ejected."));
            return;
        }

        if (inserted_bomb?.tank_one == null || inserted_bomb?.tank_two == null || !(tank_to_target == inserted_bomb?.tank_one || tank_to_target == inserted_bomb?.tank_two))
        {
            await end_test(I18n.T("ERROR: Transfer valve malfunctioning. Items ejected."));
            return;
        }

        await say(I18n.T("Beginning compression test. Opening transfer valve."));
        active = TRUE;
        test_status = null;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            await say(I18n.T("ERROR: An firmware issue was detected while starting a process. Running autopatcher."));
            playsound(this, "sound/machines/ding.ogg", 50, vary: TRUE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(error_test)), 2 * SECONDS, Dm.bitor(Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), TIMER_NO_HASH_WAIT), file: "code/modules/research/anomaly/anomaly_refinery.dm", line: 169); // Synced with the sound.
            return;
        }

        await inserted_bomb.toggle_valve(tank_to_target);
        // GAP(types): the stoppable timer's id is text, which the generated datum_timedevent var cannot hold; tg's QDEL_NULL of it
        // (end_test) only clears the var, so the port keeps the timer's id nowhere and the timer fires as in tg
        timeout_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(timeout_test)), COMPRESSION_TEST_TIME(), Dm.bitor(Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), TIMER_NO_HASH_WAIT), file: "code/modules/research/anomaly/anomaly_refinery.dm", line: 173) as datum_timedevent;
        return;
    }

    /**
     * Ejects a live TTV.
     * Triggered by attempting to operate an emagged anomaly refinery.
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:180
    public virtual async DmTask error_test()
    {
        message_admins(Dm.text("[] was emagged and ejected a TTV.", this));
        investigate_log("was emagged and ejected a TTV.", INVESTIGATE_RESEARCH);
        obj_flags = Dm.bitand(obj_flags, ~EMAGGED);

        await say(I18n.T("Issue resolved. Have a nice day!"));
        await inserted_bomb.toggle_valve(tank_to_target);
        await eject_bomb(force: TRUE);
        // GAP(types): as in start_test
        timeout_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(timeout_test)), COMPRESSION_TEST_TIME(), Dm.bitor(Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), TIMER_NO_HASH_WAIT), file: "code/modules/research/anomaly/anomaly_refinery.dm", line: 188) as datum_timedevent; // Actually start the test so they can't just put the bomb back in.
    }

    /**
     * Ends a compression test.
     *
     * Arguments:
     * - message: A message for the compressor to say when the test ends.
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:196
    public virtual async DmTask end_test(object message = null)
    {
        active = FALSE;
        tank_to_target = null;
        test_status = null;
        if (inserted_core != null)
            await eject_core();
        if (inserted_bomb != null)
            await eject_bomb();
        if (timeout_timer != null)
            QDEL_NULL(ref timeout_timer);
        if (Dm.truthy(message))
            await say(message);
        return;
    }

    /**
     * Checks whether an internal explosion was sufficient to compress the core.
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:213
    [SignalHandler]
    public virtual object check_test(atom source = null, DmList arguments = null)
    {
        if (inserted_core == null)
        {
            test_status = I18n.T("ERROR: No core present during detonation.");
            return COMSIG_CANCEL_EXPLOSION;
        }

        float heavy = Dm.num(arguments[EXARG_KEY_DEV_RANGE]);
        float medium = Dm.num(arguments[EXARG_KEY_HEAVY_RANGE]);
        float light = Dm.num(arguments[EXARG_KEY_LIGHT_RANGE]);
        float explosion_range = max(heavy, medium, light, 0);
        object required_range = get_required_radius(Dm.get_var(inserted_core, "anomaly_type"));
        turf location = get_turf(this);

        object cap_multiplier = SSmapping.level_trait(location.z, ZTRAIT_BOMBCAP_MULTIPLIER);
        if (cap_multiplier == null)
            cap_multiplier = 1;
        float capped_heavy = min(GLOB.MAX_EX_DEVESTATION_RANGE * Dm.num(cap_multiplier), heavy);
        float capped_medium = min(GLOB.MAX_EX_HEAVY_RANGE * Dm.num(cap_multiplier), medium);
        SSexplosions.shake_the_room(location, explosion_range, (capped_heavy * 15) + (capped_medium * 20), capped_heavy, capped_medium);

        if (explosion_range < Dm.num(required_range))
        {
            test_status = I18n.T("Resultant detonation failed to produce enough implosive power to compress []. Items ejected.", inserted_core);
            return COMSIG_CANCEL_EXPLOSION;
        }

        if (Dm.truthy(test_status))
            return COMSIG_CANCEL_EXPLOSION;
        inserted_core = ((obj_item_raw_anomaly_core)inserted_core).create_core(this, TRUE, TRUE);
        test_status = I18n.T("Success. Resultant detonation has theoretical range of []. Required radius was []. Core production complete.", explosion_range, required_range);
        return COMSIG_CANCEL_EXPLOSION;
    }

    /**
     * Handles timing out the test after a while.
     */
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:246
    public virtual async DmTask timeout_test()
    {
        timeout_timer = null;
        if (!Dm.truthy(test_status))
            test_status = I18n.T("Transfer valve resulted in negligible explosive power. Items ejected.");
        await end_test(test_status);
    }

    /// This is not the real valve opening process. This is the simulated one used for displaying reactions.
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:253
    public virtual async DmTask<object> simulate_valve()
    {
        if (inserted_bomb?.tank_one == null || inserted_bomb?.tank_two == null)
        {
            await eject_bomb();
            return FALSE;
        }

        if (reaction_increment == 0)
        {
            datum_gas_mixture first_gasmix = await inserted_bomb.tank_one.return_air() as datum_gas_mixture;
            datum_gas_mixture second_gasmix = await inserted_bomb.tank_two.return_air() as datum_gas_mixture;

            combined_gasmix = Dm.@new<datum_gas_mixture>(70);
            combined_gasmix.volume = Dm.num(first_gasmix.volume) + Dm.num(second_gasmix.volume);
            combined_gasmix.merge(first_gasmix.copy() as datum_gas_mixture);
            combined_gasmix.merge(second_gasmix.copy() as datum_gas_mixture);
        }
        else
            await combined_gasmix.react();

        reaction_increment += 1;
        return null;
    }

    /// We dont allow incomplete valves to go in but do code in checks for incomplete valves. Just in case.
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:272
    public virtual async DmTask eject_bomb(mob user = null, float? force = null)
    {
        force ??= FALSE;
        if (inserted_bomb == null || (Dm.truthy(active) && !Dm.truthy(force)))
            return;
        if (user != null)
        {
            await user.put_in_hands(inserted_bomb);
            to_chat(user, span_notice(I18n.T("You remove [] from [].", inserted_bomb, this)));
        }
        else
            await inserted_bomb.forceMove(drop_location() as atom);
        combined_gasmix = null;
        reaction_increment = 0;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:283
    public virtual async DmTask eject_core(mob user = null)
    {
        if (inserted_core == null || Dm.truthy(active))
            return;
        if (user != null)
        {
            await user.put_in_hands((obj_item)inserted_core);
            to_chat(user, span_notice(I18n.T("You remove [] from [].", inserted_core, this)));
        }
        else
            await ((atom_movable)inserted_core).forceMove(drop_location() as atom);
    }

    /// We rely on exited to clear references.
    // tg: code/modules/research/anomaly/anomaly_refinery.dm:293
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == inserted_bomb)
        {
            inserted_bomb = null;
            tank_to_target = null;
        }
        if (gone == (object)inserted_core)
            inserted_core = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:301
    public virtual async DmTask<object> swap_target()
    {
        if (inserted_bomb?.tank_one == null || inserted_bomb?.tank_two == null)
        {
            await eject_bomb();
            return FALSE;
        }
        tank_to_target = (tank_to_target == inserted_bomb.tank_one) ? inserted_bomb.tank_two : inserted_bomb.tank_one;
        return null;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:307
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await eject_bomb();
        await eject_core();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:312
    public override object Destroy(float? force = null)
    {
        inserted_bomb = null;
        inserted_core = null;
        combined_gasmix = null;
        return base.Destroy(force);
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:318
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AnomalyRefinery");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:325
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "react"))
            await simulate_valve();
        else if (Dm.eq(sw__1, "eject_bomb"))
            await eject_bomb(usr);
        else if (Dm.eq(sw__1, "eject_core"))
            await eject_core(usr);
        else if (Dm.eq(sw__1, "start_implosion"))
            await start_test();
        else if (Dm.eq(sw__1, "swap"))
            await swap_target();
        return dot;
    }

    // tg: code/modules/research/anomaly/anomaly_refinery.dm:341
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList parsed_gasmixes = Dm.list();
        obj_item_tank other_tank = null;

        if (inserted_bomb?.tank_one != null && inserted_bomb?.tank_two != null)
            other_tank = inserted_bomb.tank_one == tank_to_target ? inserted_bomb.tank_two : inserted_bomb.tank_one;

        datum_gas_mixture target_air = tank_to_target == null ? null : await tank_to_target.return_air() as datum_gas_mixture;
        datum_gas_mixture other_air = other_tank == null ? null : await other_tank.return_air() as datum_gas_mixture;
        Dm.append(ref parsed_gasmixes, Dm.list(gas_mixture_parser(target_air, tank_to_target?.name)));
        Dm.append(ref parsed_gasmixes, Dm.list(gas_mixture_parser(other_air, other_tank?.name)));
        Dm.append(ref parsed_gasmixes, Dm.list(gas_mixture_parser(combined_gasmix, "Combined Gasmix")));

        data["gasList"] = parsed_gasmixes;

        data["valvePresent"] = inserted_bomb != null ? TRUE : FALSE;
        data["valveReady"] = (inserted_bomb?.tank_one != null && inserted_bomb?.tank_two != null) ? TRUE : FALSE;
        data["reactionIncrement"] = reaction_increment;

        data["core"] = inserted_core != null ? ((atom)inserted_core).name : FALSE;
        data["requiredRadius"] = inserted_core != null ? get_required_radius(Dm.get_var(inserted_core, "anomaly_type")) : null;

        data["active"] = active;

        return data;
    }
}
