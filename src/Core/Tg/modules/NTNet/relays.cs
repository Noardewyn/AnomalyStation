// Translated from tgstation (snapshot 1d99bd7f): code/modules/NTNet/relays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: NtOS's network): the NTNet quantum relay, whose working copy keeps the PDAs' NTNet up, its DoS buffer
// and its NtnetRelay window. The DoS attack program that fills the buffer (programs/antagonist/dos.dm) is an antagonist's
// (dos_sources stays empty). The var block is generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/NTNet/relays.dm:2
    public static object find_functional_ntnet_relay()
    {
        // Check all relays. If we have at least one working relay, ntos is up.
        foreach (obj_machinery_ntnet_relay relays in Dm.each<obj_machinery_ntnet_relay>(SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay))))
        {
            if (!Dm.truthy(relays.is_operational))
                continue;
            return TRUE;
        }
        return FALSE;
    }
}

// Relays don't handle any actual communication. Global NTNet datum does that, relays only tell the datum if it should or shouldn't work.
// tg: code/modules/NTNet/relays.dm:11
public partial class obj_machinery_ntnet_relay : obj_machinery
{
    // tg: code/modules/NTNet/relays.dm:27  var/static/gl_uid = 1
    public float gl_uid
    {
        get => TgState.Current.obj_machinery_ntnet_relay__gl_uid ?? 1;
        set => TgState.Current.obj_machinery_ntnet_relay__gl_uid = value;
    }

    // tg: code/modules/NTNet/relays.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        uid = gl_uid++;
        DmList current_machines = SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay)) as DmList;
        SSmodular_computers.add_log(Dm.text("New quantum relay activated. Current amount of linked relays: []", current_machines.len));
        return dot;
    }

    // tg: code/modules/NTNet/relays.dm:40
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        DmList machines_left = SSmachines.get_machines_by_type(typeof(obj_machinery_ntnet_relay)) as DmList;
        SSmodular_computers.add_log(Dm.text("Quantum relay connection severed. Current amount of linked relays: []", machines_left.len));
        // for(var/datum/computer_file/program/ntnet_dos/D in dos_sources): the DoS program (an antagonist's) never fills dos_sources
        return dot;
    }

    ///Proc called to change the value of the `relay_enabled` variable and append behavior related to its change.
    // tg: code/modules/NTNet/relays.dm:49
    public virtual async DmTask<object> set_relay_enabled(object new_value = null)
    {
        if (Dm.eq(new_value, relay_enabled))
            return null;
        object dot = relay_enabled;
        relay_enabled = Dm.num(new_value);
        if (Dm.truthy(dot)) //Turned off
            await set_is_operational(FALSE);
        else if (!Dm.truthy(dos_failure) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN | MAINT))) //Turned on
            await set_is_operational(TRUE);
        return dot;
    }

    ///Proc called to change the value of the `dos_failure` variable and append behavior related to its change.
    // tg: code/modules/NTNet/relays.dm:60
    public virtual async DmTask<object> set_dos_failure(object new_value = null)
    {
        if (Dm.eq(new_value, dos_failure))
            return null;
        object dot = dos_failure;
        dos_failure = Dm.num(new_value);
        if (Dm.truthy(dot)) //Failure ended
        {
            if (Dm.truthy(relay_enabled) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN | MAINT)))
                await set_is_operational(TRUE);
        }
        else //Failure started
            await set_is_operational(FALSE);
        return dot;
    }

    // tg: code/modules/NTNet/relays.dm:71
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        if (Dm.truthy(Dm.bitand(old_value, NOPOWER | BROKEN | MAINT)))
        {
            if (Dm.truthy(relay_enabled) && !Dm.truthy(dos_failure) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN | MAINT))) //From off to on.
                await set_is_operational(TRUE);
        }
        else if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN | MAINT))) //From on to off.
            await set_is_operational(FALSE);
    }

    // tg: code/modules/NTNet/relays.dm:78
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("bus[]", Dm.truthy(is_operational) ? null : "_off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/NTNet/relays.dm:82
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        update_use_power(Dm.truthy(is_operational) ? ACTIVE_POWER_USE : IDLE_POWER_USE);

        update_appearance();

        if (dos_overload > 0)
            dos_overload = Dm.max(0, dos_overload - dos_dissipate * Dm.num(seconds_per_tick));

        // If DoS traffic exceeded capacity, crash.
        if (dos_overload > dos_capacity && !Dm.truthy(dos_failure))
        {
            Waiting.NoSleep(set_dos_failure(TRUE));
            update_appearance();
            SSmodular_computers.add_log("Quantum relay switched from normal operation mode to overload recovery mode.");
        }
        // If the DoS buffer reaches 0 again, restart.
        if (dos_overload == 0 && Dm.truthy(dos_failure))
        {
            Waiting.NoSleep(set_dos_failure(FALSE));
            update_appearance();
            SSmodular_computers.add_log("Quantum relay switched from overload recovery mode to normal operation mode.");
        }
        return TRUE;
    }

    // tg: code/modules/NTNet/relays.dm:102
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "NtnetRelay");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/NTNet/relays.dm:108
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["enabled"] = relay_enabled;
        data["dos_capacity"] = dos_capacity;
        data["dos_overload"] = dos_overload;
        data["dos_crashed"] = dos_failure;
        return data;
    }

    // tg: code/modules/NTNet/relays.dm:116
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "restart"))
        {
            dos_overload = 0;
            await set_dos_failure(FALSE);
            update_appearance();
            SSmodular_computers.add_log("Quantum relay manually restarted from overload recovery mode to normal operation mode.");
            return TRUE;
        }
        if (Dm.eq(action, "toggle"))
        {
            await set_relay_enabled(!Dm.truthy(relay_enabled) ? TRUE : FALSE);
            SSmodular_computers.add_log(Dm.text("Quantum relay manually [].", Dm.truthy(relay_enabled) ? "enabled" : "disabled"));
            update_appearance();
            return TRUE;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/NTNet/relays.dm:27  /obj/machinery/ntnet_relay's var/static/gl_uid = 1
    internal float? obj_machinery_ntnet_relay__gl_uid;
}
