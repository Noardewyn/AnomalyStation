// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/_transport_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file but add_context (the screentips stay deferred, live-partial.txt).

// tg: code/modules/transport/_transport_machinery.dm:1
public partial class obj_machinery_transport : obj_machinery
{
    // tg: code/modules/transport/_transport_machinery.dm:11  var/static/list/how_do_we_fix_it (rules 10.6)
    public static ref DmList how_do_we_fix_it => ref TgState.Current.obj_machinery_transport__how_do_we_fix_it;

    // tg: code/modules/transport/_transport_machinery.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(id_tag))
            id_tag = assign_random_name();
        return dot;
    }

    /**
     * Finds the tram
     *
     * Locates tram parts in the lift global list after everything is done.
     */
    // tg: code/modules/transport/_transport_machinery.dm:56
    public virtual void link_tram()
    {
        foreach (datum_transport_controller_linear_tram tram in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (!Dm.eq(tram.specific_transport_id, configured_transport_id))
                continue;
            transport_ref = WEAKREF(tram) as datum_weakref;
            log_transport(Dm.text("[]: Successfuly linked to transport ID [] []", id_tag, tram.specific_transport_id, transport_ref));
            break;
        }

        if (transport_ref == null)
            log_transport(Dm.text("[]: Tried to find a transport with ID [], but failed!", id_tag, configured_transport_id));
    }

    // tg: code/modules/transport/_transport_machinery.dm:67
    public virtual async DmTask local_fault()
    {
        if (Dm.truthy(malfunctioning) || repair_signals != null)
            return;

        generate_repair_signals();
        malfunctioning = TRUE;
        await set_is_operational(FALSE);
        update_appearance();
    }

    /**
     * All subtypes have the same method of repair for consistency and predictability
     * The key of this assoc list is the "method" of how they're fixing the thing (just flavor for examine),
     * and the value is what tool they actually need to use on the thing to fix it
     */
    // tg: code/modules/transport/_transport_machinery.dm:81
    public virtual void generate_repair_signals()
    {
        // Select a few methods of how to fix it
        DmList fix_it_keys = assoc_to_keys(how_do_we_fix_it) as DmList;
        Dm.append(ref methods_to_fix, pick_n_take(fix_it_keys));

        // Construct the signals
        LAZYINITLIST(ref repair_signals);
        foreach (object tool_method in Dm.each(methods_to_fix))
            Dm.append(ref repair_signals, COMSIG_ATOM_TOOL_ACT(how_do_we_fix_it[tool_method]));

        // Register signals to make it fixable
        if (Dm.truthy(length(repair_signals)))
            RegisterSignals(this, repair_signals, nameof(on_machine_tooled));
    }

    // tg: code/modules/transport/_transport_machinery.dm:96
    public virtual void clear_repair_signals()
    {
        UnregisterSignal(this, repair_signals);
        LAZYNULL(ref repair_signals);
    }

    // tg: code/modules/transport/_transport_machinery.dm:100
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (methods_to_fix != null)
        {
            foreach (object tool_method in Dm.each(methods_to_fix))
                Dm.append(ref dot, span_warning(I18n.T("It needs someone to [].", EXAMINE_HINT(tool_method))));
        }
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be deconstructed with a []", EXAMINE_HINT("crowbar."))));
        return dot;
    }

    /**
     * Signal proc for [COMSIG_ATOM_TOOL_ACT], from a variety of signals, registered on the machinery.
     *
     * We allow for someone to stop the event early by using the proper tools, hinted at in examine, on the machine
     */
    // tg: code/modules/transport/_transport_machinery.dm:114
    public virtual object on_machine_tooled(obj_machinery source = null, mob_living user = null, obj_item tool = null)
    {
        Waiting.Detach(try_fix_machine(source as obj_machinery_transport, user, tool)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    /// Attempts a do_after, and if successful, stops the event
    // tg: code/modules/transport/_transport_machinery.dm:121
    public virtual async DmTask<object> try_fix_machine(obj_machinery_transport machine = null, mob_living user = null, obj_item tool = null)
    {
        machine.balloon_alert(user, I18n.T("percussive maintenance..."));
        if (!Dm.truthy(await tool.use_tool(machine, user, 7 * SECONDS, volume: 50)))
        {
            machine.balloon_alert(user, I18n.T("interrupted!"));
            return FALSE;
        }

        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 75, use_reverb: TRUE);
        machine.balloon_alert(user, I18n.T("success!"));
        UnregisterSignal(this, repair_signals);
        LAZYNULL(ref repair_signals);
        methods_to_fix = Dm.list();
        malfunctioning = FALSE;
        await set_machine_stat(Dm.bitand(machine_stat, ~EMAGGED));
        await set_is_operational(TRUE);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/transport/_transport_machinery.dm:140
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            balloon_alert(user, I18n.T("it doesn't need repairs!"));
            return TRUE;
        }
        balloon_alert(user, I18n.T("repairing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 0, volume: 50)))
            return TRUE;
        balloon_alert(user, I18n.T("repaired"));
        atom_integrity = max_integrity;
        await set_machine_stat(Dm.bitand(machine_stat, ~BROKEN));
        update_appearance();
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/transport/_transport_machinery.dm:11  var/static/list/how_do_we_fix_it
    internal DmList obj_machinery_transport__how_do_we_fix_it = Dm.list(
        Dm.kv("try turning it off and on again with a multitool", TOOL_MULTITOOL),
        Dm.kv("try forcing an unexpected reboot with a multitool", TOOL_MULTITOOL),
        Dm.kv("patch the system's call table with a multitool", TOOL_MULTITOOL),
        Dm.kv("gently reset the invalid memory with a crowbar", TOOL_CROWBAR),
        Dm.kv("secure its ground connection with a wrench", TOOL_WRENCH),
        Dm.kv("tighten some screws with a screwdriver", TOOL_SCREWDRIVER),
        Dm.kv("check its wire voltages with a multitool", TOOL_MULTITOOL),
        Dm.kv("cut some excess wires with wirecutters", TOOL_WIRECUTTER));
}
