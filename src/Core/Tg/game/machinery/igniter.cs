// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/igniter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the sparker's attack_ai (the AI, a live-partial row), the engineering package E8 (D287): the floor igniter,
// switched by its ignition controller's id (doorcontrol.dm), keeps its turf at 1000 K while on (the incinerators' burn chambers);
// the wall-mounted sparker sparks by its id, by an EMP, every 5 seconds at most; both are set by a multitool and welded apart.
// The var blocks are generated.

// tg: code/game/machinery/igniter.dm:1
public partial class obj_machinery_igniter : obj_machinery
{
    // tg: code/game/machinery/igniter.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/game/machinery/igniter.dm:22
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Connect []", this);
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Unweld";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/machinery/igniter.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a [] to set its ID to match your ignition controller's ID.", EXAMINE_HINT("multitool"))));
        Dm.append(ref dot, span_notice(I18n.T("It could be [] apart.", EXAMINE_HINT("welded"))));
        return dot;
    }

    // tg: code/game/machinery/igniter.dm:44
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
            return null;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
        {
            balloon_alert(user, I18n.T("not enough fuel!"));
            return null;
        }

        ((atom)loc).balloon_alert(user, I18n.T("dismantling []", this));
        if (!Dm.truthy(await tool.use_tool(this, user, delay: 2.5f * SECONDS, amount: 2, volume: 50)))
            return null;
        ((atom)loc).balloon_alert(user, I18n.T("[] dismantled", this));

        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/igniter.dm:60
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
        Dm.@new<obj_item_assembly_igniter>(loc);
        return null;
    }

    // tg: code/game/machinery/igniter.dm:64
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object change_id = await tgui_input_number(user, I18n.T("Set the igniter controller's ID"), I18n.T("Igniter ID"), id, 100);
        if (!Dm.truthy(change_id) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        id = change_id;
        balloon_alert(user, I18n.T("id set to []", id));
        to_chat(user, span_notice(I18n.T("You change the ID to [].", id)));
        return ITEM_INTERACT_SUCCESS;
    }

    /// turns the igniter on/off
    // tg: code/game/machinery/igniter.dm:96
    public virtual void toggle()
    {
        on = Dm.num(!Dm.truthy(on));
        if (Dm.truthy(on))
            begin_processing();
        else
            end_processing();
        update_appearance();
    }

    // tg: code/game/machinery/igniter.dm:104
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        add_fingerprint(user);
        toggle();
        return dot;
    }

    /// Have to process to ignite any gas that comes in the turf
    // tg: code/game/machinery/igniter.dm:112
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        turf location = loc as turf;
        if (!isturf(loc) || !isopenturf(loc)) //don't ignite stuff inside walls
            on = FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            on = FALSE;
        // use_energy is coloured (the APC cell's use can sleep through an override); the cell's own does not (waiting-model.md 5.3)
        if (!Dm.truthy(Waiting.NoSleep(use_energy(active_power_usage, force: FALSE)))) // Use energy to keep the turf hot. Doesn't necessarily use the correct amount of energy though (this should be changed).
            on = FALSE;
        if (!Dm.truthy(on))
        {
            update_appearance();
            return PROCESS_KILL;
        }

        location.hotspot_expose(1000, 500, 1);
        return null;
    }

    // tg: code/game/machinery/igniter.dm:126
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, on);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/igniter.dm:130
    public override DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        id = Dm.text("[]_[]", port.shuttle_id, id);
        return DmTask<object>.FromResult(default);
    }
}

// Wall mounted remote-control igniter.

// tg: code/game/machinery/igniter.dm:144
public partial class obj_machinery_sparker : obj_machinery
{
    // tg: code/game/machinery/igniter.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg passes (2, TRUE, src) to New(location, amount, cardinals_only): no turf, one spark, and src as cardinals_only, which is
        // only read as a truth value; the port's numeric cardinals_only takes TRUE for it
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(2, TRUE, TRUE);
        spark_system.attach(this);
        register_context();
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/igniter.dm:169
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/igniter.dm:173
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("Connect []", this);
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_WELDER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Unweld";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/game/machinery/igniter.dm:187
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a [] to set its ID to match your ignition controller's ID.", EXAMINE_HINT("multitool"))));
        Dm.append(ref dot, span_notice(I18n.T("It could be [] apart.", EXAMINE_HINT("welded"))));
        return dot;
    }

    // tg: code/game/machinery/igniter.dm:192
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
        {
            balloon_alert(user, I18n.T("not enough fuel!"));
            return TRUE;
        }

        ((atom)loc).balloon_alert(user, I18n.T("dismantling []", this));
        if (!Dm.truthy(await tool.use_tool(this, user, delay: 1.5f * SECONDS, amount: 1, volume: 50)))
            return null;
        ((atom)loc).balloon_alert(user, I18n.T("[] dismantled", this));

        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/igniter.dm:205
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_wallframe_sparker>(loc);
        return null;
    }

    // tg: code/game/machinery/igniter.dm:208
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object change_id = await tgui_input_number(user, I18n.T("Set the sparker controller's ID"), I18n.T("Sparker ID"), id, 100);
        if (!Dm.truthy(change_id) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        id = change_id;
        balloon_alert(user, I18n.T("id set to []", id));
        to_chat(user, span_notice(I18n.T("You change the ID to [].", id)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/igniter.dm:217
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(disable))
        {
            icon_state = Dm.text("[]-d", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(powered__proc()) ? null : "-p");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/igniter.dm:224
    public override object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        if (Dm.truthy(disable))
            return FALSE;
        return base.powered__proc(chan, ignore_use_power);
    }

    // tg: code/game/machinery/igniter.dm:229
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        tool.play_tool_sound(this, 50);
        disable = Dm.num(!Dm.truthy(disable));
        if (Dm.truthy(disable))
            user.visible_message(span_notice(I18n.T("[] disables \\the []!", user, this)), span_notice(I18n.T("You disable the connection to \\the [].", this)));
        if (!Dm.truthy(disable))
            user.visible_message(span_notice(I18n.T("[] reconnects \\the []!", user, this)), span_notice(I18n.T("You fix the connection to \\the [].", this)));
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/igniter.dm:246
    public virtual async DmTask<object> ignite()
    {
        if (!Dm.truthy(powered__proc()))
            return FALSE;

        if (Dm.truthy(disable) || (Dm.truthy(last_spark) && world.time < last_spark + 50))
            return FALSE;

        turf location = loc as turf;
        if (!isturf(loc) || !isopenturf(loc))
            return FALSE;

        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
            return FALSE;

        flick(Dm.text("[]-spark", Dm.initial(this, (atom p) => p.icon_state)), this);
        await spark_system.start();
        last_spark = world.time;
        location.hotspot_expose(1000, 2500, 1);

        return TRUE;
    }

    // tg: code/game/machinery/igniter.dm:267
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            await ignite();
        return dot;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/igniter.dm:240
public partial class obj_machinery_sparker : obj_machinery
{
    // tg: code/game/machinery/igniter.dm:240
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (Dm.truthy(anchored))
            return await ignite();
        else
            return null;
    }
}
