// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/faxnotif.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298): FaxBond, the heads' PDA app that tells them their fax received something, with its NtosFaxBond
// window. The var block is generated.

// tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:1
public partial class datum_computer_file_program_faxbond : datum_computer_file_program
{
    /**
     * Proc for subscribing to faxes. Adds weakref to fax's listeners and updates fax related vars for program. Includes type checking.
     * Arguments:
     * * target - [/datum/computer_file/program/proc/tap] proc target, can be anything
     */
    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:21
    public virtual object connect_fax(obj_machinery_fax target = null)
    {
        if (target == null)
            return FALSE;

        object our_id = target.fax_id;

        if (!Dm.truthy(connected_faxes[our_id]))
            LAZYSET(ref target.fax_listeners, REF(this), WEAKREF(this));

        DmList fax_info = Dm.list();
        area our_area = get_area(target) as area;
        fax_info["ref"] = WEAKREF(target);
        fax_info["name"] = target.fax_name;
        fax_info["area_name"] = our_area.name;
        fax_info["muted"] = FALSE;
        // connected_faxes[our_id] += fax_info: an assoc entry that was null becomes the list's copy; one that was there grows
        if (connected_faxes[our_id] is DmList existing)
            Dm.append(existing, fax_info);
        else
            connected_faxes[our_id] = fax_info.Copy();

        return TRUE;
    }

    /**
     * Disconnects a fax given its ID (if it was connected before), removing it from the relevant lists.
     * Arguments:
     * * fax_id - fax id to disconnect from our PDA
     */
    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:45
    public virtual void disconnect_fax(object fax_id = null)
    {
        if (!Dm.truthy(connected_faxes[fax_id]))
            return;

        DmList fax_info = connected_faxes[fax_id] as DmList;
        datum_weakref fax_ref = fax_info["ref"] as datum_weakref;
        obj_machinery_fax our_fax = fax_ref.resolve() as obj_machinery_fax;
        if (our_fax != null)
            LAZYREMOVE(ref our_fax.fax_listeners, REF(this));

        connected_faxes.Remove(fax_id);
    }

    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:57
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        foreach (object fax in Dm.each(connected_faxes?.Copy()))
            disconnect_fax(fax);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:62
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        return connect_fax(tapped_atom as obj_machinery_fax);
    }

    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:65
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["faxes_info"] = Dm.list();
        foreach (object fax_id in Dm.each(connected_faxes))
        {
            DmList fax_info = connected_faxes[fax_id] as DmList;
            DmList fax_data = Dm.list(
                Dm.kv("id", fax_id),
                Dm.kv("name", fax_info["name"]),
                Dm.kv("location", fax_info["area_name"]),
                Dm.kv("muted", fax_info["muted"]));
            Dm.append((DmList)data["faxes_info"], Dm.list(fax_data));
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/faxnotif.dm:80
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "unsubscribe":
                disconnect_fax(@params["id"]);
                return TRUE;
            case "mute":
            {
                DmList fax_info = connected_faxes[@params["id"]] as DmList;
                if (fax_info == null)
                    return FALSE;
                fax_info["muted"] = Dm.truthy(fax_info["muted"]) ? FALSE : TRUE;
                return TRUE;
            }
        }
        return dot;
    }
}
