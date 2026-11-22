// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/computers/telemonitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D2: the telecomms monitoring console): it probes a network for its telecomms machines and shows a machine's
// links in its TelecommsMonitor window. The var block is generated (D111).

// tg: code/game/machinery/telecomms/computers/telemonitor.dm:1  #define MAIN_VIEW 0, MACHINE_VIEW 1, MAX_NETWORK_ID_LENGTH 15 (file-local)
// tg: code/game/machinery/telecomms/computers/telemonitor.dm:5
public partial class obj_machinery_computer_telecomms_monitor : obj_machinery_computer_telecomms
{
    private const float MAIN_VIEW = 0;
    private const float MACHINE_VIEW = 1;
    private const float MAX_NETWORK_ID_LENGTH = 15;

    // tg: code/game/machinery/telecomms/computers/telemonitor.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        LAZYINITLIST(ref machine_refs);
        return dot;
    }

    // tg: code/game/machinery/telecomms/computers/telemonitor.dm:28
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/telecomms)): the asset transport is the client's (DV44); the
        // client draws the "tcomms" sheet's pictures from its own copy of tg's .dmi file (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/machinery/telecomms/computers/telemonitor.dm:33
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TelecommsMonitor");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/telecomms/computers/telemonitor.dm:41
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["screen"] = screen;
        data["statusMessage"] = status_message;

        if (screen == MAIN_VIEW)
        {
            data["network"] = network;

            data["machines"] = Dm.list();
            foreach (datum_weakref machine_ref in Dm.each<datum_weakref>(machine_refs.Copy()))
            {
                obj_machinery_telecomms machine = machine_ref.resolve() as obj_machinery_telecomms;
                if (machine == null)
                {
                    Dm.remove(ref machine_refs, machine_ref);
                    continue;
                }

                data["machines"] = Dm.add(data["machines"], Dm.list(Dm.list(
                    Dm.kv("id", machine.id),
                    Dm.kv("name", machine.name),
                    Dm.kv("icon", Dm.initial(machine, (obj_machinery_telecomms p) => p.icon_state)))));
            }
        }
        else if (screen == MACHINE_VIEW)
        {
            obj_machinery_telecomms selected = selected_machine_ref?.resolve() as obj_machinery_telecomms;
            if (selected != null)
            {
                DmList linked_machines = Dm.list();
                foreach (obj_machinery_telecomms machine in Dm.each<obj_machinery_telecomms>(selected.links))
                {
                    Dm.append(ref linked_machines, Dm.list(Dm.list(
                        Dm.kv("id", machine.id),
                        Dm.kv("name", machine.name),
                        Dm.kv("icon", Dm.initial(machine, (obj_machinery_telecomms p) => p.icon_state)))));
                }

                data["machine"] = Dm.list(
                    Dm.kv("id", selected.id),
                    Dm.kv("name", selected.name),
                    Dm.kv("network", selected.network),
                    Dm.kv("linkedMachines", linked_machines));
            }
        }

        return data;
    }

    // tg: code/game/machinery/telecomms/computers/telemonitor.dm:82
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        status_message = null;

        if (Dm.eq(action, "flush"))
        {
            machine_refs = Dm.list();
            network = "";
            return TRUE;
        }
        else if (Dm.eq(action, "home"))
        {
            selected_machine_ref = null;
            screen = MAIN_VIEW;
            return TRUE;
        }
        else if (Dm.eq(action, "view"))
        {
            object machine_id = @params["id"];
            if (machine_id == null)
                return null;

            foreach (datum_weakref machine_ref in Dm.each<datum_weakref>(machine_refs.Copy()))
            {
                obj_machinery_telecomms machine = machine_ref.resolve() as obj_machinery_telecomms;
                if (machine == null)
                {
                    Dm.remove(ref machine_refs, machine_ref);
                    continue;
                }

                if (!Dm.eq(machine.id, machine_id))
                    continue;

                selected_machine_ref = machine_ref;
                screen = MACHINE_VIEW;
            }

            if (selected_machine_ref == null)
                status_message = I18n.T("OPERATION FAILED: UNABLE TO LOCATE MACHINERY.");

            return TRUE;
        }
        else if (Dm.eq(action, "probe"))
        {
            object network_id = @params["id"];
            if (Dm.length(network_id) > MAX_NETWORK_ID_LENGTH)
            {
                status_message = I18n.T("OPERATION FAILED: NETWORK ID TOO LONG.");
                return TRUE;
            }

            list_clear_empty_weakrefs(machine_refs);

            if (Dm.length(machine_refs) > 0)
            {
                status_message = I18n.T("OPERATION FAILED: CANNOT PROBE WHEN BUFFER FULL.");
                return TRUE;
            }

            network = Dm.str(network_id);

            DmList telecomms_machines = SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_telecomms)) as DmList;
            foreach (obj_machinery_telecomms machine in Dm.each<obj_machinery_telecomms>(telecomms_machines))
            {
                if (!Dm.eq(machine.network, network))
                    continue;

                Dm.append(ref machine_refs, WEAKREF(machine));
            }

            if (Dm.length(machine_refs) == 0)
            {
                status_message = I18n.T("OPERATION FAILED: UNABLE TO LOCATE NETWORK ENTITIES IN [].", network);
                return TRUE;
            }

            status_message = I18n.T("[] ENTITIES LOCATED & BUFFERED", Dm.length(machine_refs));
            return TRUE;
        }

        return TRUE;
    }
}
