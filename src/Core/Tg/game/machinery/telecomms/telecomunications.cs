// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/telecomunications.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: telecomms): the base telecomms machine, its links, the relay of a signal along them, its power,
// its traffic and its EMP. The var block is generated (D111).

public partial class datum_controller_global_vars
{
    /// A list of all of the `/obj/machinery/telecomms` (and subtypes) machines
    /// that exist in the world currently.
    // tg: code/game/machinery/telecomms/telecomunications.dm:3  GLOBAL_LIST_EMPTY(telecomm_machines)
    public DmList telecomm_machines;

    // tg: code/game/machinery/telecomms/telecomunications.dm:3  (GLOBAL_MANAGED(telecomm_machines, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltelecomm_machines()
    {
        telecomm_machines = Dm.list();
        Dm.append(ref gvars_datum_init_order, "telecomm_machines");
    }
}

/**
 * The basic telecomms machinery type, implementing all of the logic that's
 * shared between all of the telecomms machinery.
 */
// tg: code/game/machinery/telecomms/telecomunications.dm:9
public partial class obj_machinery_telecomms : obj_machinery
{
    /// relay signal to all linked machinery that are of type [filter]. If signal has been sent [amount] times, stop sending
    // tg: code/game/machinery/telecomms/telecomunications.dm:48
    public virtual async DmTask<object> relay_information(datum_signal_subspace signal = null, Type filter = null, object copysig = null, float? amount = null)
    {
        amount ??= 20;
        if (!Dm.truthy(on))
            return null;

        if (filter == null || !ispath(filter, typeof(obj_machinery_telecomms)))
            throw Dm.CRASH(Dm.text("null or non /obj/machinery/telecomms typepath given as the filter argument! given typepath: []", filter), "code/game/machinery/telecomms/telecomunications.dm", 54);

        float send_count = 0;

        // Apply some lag based on traffic rates
        float netlag = Dm.round(traffic / 50);
        if (Dm.gt(netlag, signal.data["slow"]))
            signal.data["slow"] = netlag;

        // Loop through all linked machines and send the signal or copy.

        DmList connected_zs = SSmapping.get_connected_levels(get_turf(this));
        foreach (obj_machinery_telecomms filtered_machine in Dm.each<obj_machinery_telecomms>((links_by_telecomms_type?[filter] as DmList)?.Copy()))
        {
            if (!Dm.truthy(filtered_machine.on))
                continue;
            if (amount != 0 && send_count >= amount)
                break;
            turf filtered_home = get_turf(filtered_machine);
            if (!Dm.@in(filtered_home.z, connected_zs) && !Dm.truthy(long_range_link) && !Dm.truthy(filtered_machine.long_range_link))
                continue;

            send_count++;
            if (filtered_machine.is_freq_listening(signal))
                filtered_machine.traffic++;

            if (Dm.truthy(copysig))
                await filtered_machine.receive_information(signal.copy(), this);
            else
                await filtered_machine.receive_information(signal, this);
        }

        if (send_count > 0 && is_freq_listening(signal))
            traffic++;

        return send_count;
    }

    /// Sends a signal directly to a machine.
    // tg: code/game/machinery/telecomms/telecomunications.dm:89
    public virtual async DmTask relay_direct_information(datum_signal signal = null, obj_machinery_telecomms machine = null)
    {
        await machine.receive_information(signal, this);
    }

    /// Receive information from linked machinery
    // tg: code/game/machinery/telecomms/telecomunications.dm:93
    public virtual async DmTask receive_information(datum_signal signal = null, obj_machinery_telecomms machine_from = null)
    {
    }

    /**
     * Checks whether the machinery is listening to that signal.
     *
     * Returns `TRUE` if found, `FALSE` if not.
     */
    // tg: code/game/machinery/telecomms/telecomunications.dm:101
    public virtual bool is_freq_listening(datum_signal signal = null)
    {
        return signal != null && (!Dm.truthy(length(freq_listening)) || Dm.@in(signal.frequency, freq_listening));
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.telecomm_machines, this);
        if (Dm.truthy(mapload) && autolinkers.len > 0)
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:110
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        DmList connected_zs = SSmapping.get_connected_levels(get_turf(this));
        foreach (obj_machinery_telecomms telecomms_machine in Dm.each_cast<obj_machinery_telecomms>(GLOB.telecomm_machines))
        {
            if (telecomms_machine == this)
                continue;
            turf their_home = get_turf(telecomms_machine);
            if (!(Dm.truthy(long_range_link) && Dm.truthy(telecomms_machine.long_range_link)) && !(GET_CARDINAL_DIST(this, telecomms_machine) <= 20 || Dm.@in(their_home.z, connected_zs)))
                continue;
            if (!Dm.truthy(length(Dm.bitand(telecomms_machine.autolinkers, autolinkers))))
                continue;
            add_new_link(telecomms_machine);
        }
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:123
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.telecomm_machines, this);
        foreach (obj_machinery_telecomms comm in Dm.each_cast<obj_machinery_telecomms>(GLOB.telecomm_machines))
            remove_link(comm);
        links = Dm.list();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:130
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", Dm.initial(this, (obj_machinery_telecomms p) => p.icon_state), Dm.truthy(panel_open) ? "_o" : null, Dm.truthy(on) ? null : "_off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:134
    public override void on_set_panel_open(object old_value = null)
    {
        update_appearance();
        base.on_set_panel_open(old_value);
    }

    /**
     * Handles updating the power state of the machine, modifying its `on`
     * variable based on if it's `toggled` and if it's either broken, has no power
     * or it's EMP'd. Handles updating appearance based on that power change.
     */
    // tg: code/game/machinery/telecomms/telecomunications.dm:143
    public virtual void update_power()
    {
        float old_on = on;
        if (Dm.truthy(toggled))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, Dm.bitor(NOPOWER, EMPED))))) // if powered, on. if not powered, off. if too damaged, off
                on = FALSE;
            else
                on = TRUE;
        }
        else
            on = FALSE;
        if (old_on != on)
            update_appearance();
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:155
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        update_power();

        if (traffic > 0)
            traffic -= netspeed * Dm.num(seconds_per_tick);
        return null;
    }

    // tg: code/game/machinery/telecomms/telecomunications.dm:161
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (Dm.prob(100 / Dm.num(severity)) && !Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            await set_machine_stat(Dm.bitor(machine_stat, EMPED));
            float duration = (300 * SECONDS) / Dm.num(severity);
            _addtimer(Dm.@new<datum_callback>(this, nameof(de_emp)), Dm.rand(duration - 2 * SECONDS, duration + 2 * SECONDS), file: "code/game/machinery/telecomms/telecomunications.dm", line: 168);
        }
        return dot;
    }

    /// Handles the machine stopping being affected by an EMP.
    // tg: code/game/machinery/telecomms/telecomunications.dm:171
    public virtual async DmTask de_emp()
    {
        await set_machine_stat(Dm.bitand(machine_stat, ~EMPED));
    }
}
