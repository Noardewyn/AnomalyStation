// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/quantum_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the quantum console and its QuantumConsole window (the client's
// Tgui/Interfaces/QuantumConsole.cs draws it): the domain list, the boot, the random domain, the stop and the broadcast. The var block
// is generated (D111).

// tg: code/modules/bitrunning/objects/quantum_console.dm:1
public partial class obj_machinery_computer_quantum_console : obj_machinery_computer
{
    // tg: code/modules/bitrunning/objects/quantum_console.dm:13
    [DmParams("mapload", "circuit")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc = I18n.T("Even in the distant year [], Nanotrasen is still using REST APIs. How grim.", CURRENT_STATION_YEAR);
        return dot;
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:17
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        find_server();
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (server_ref == null)
            Dm.append(ref dot, span_warning(I18n.T("Please install an quantum server in any tile next to this console.")));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:26
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);

        if (!Dm.truthy(is_operational))
            return dot;

        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "QuantumConsole");
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:37
    public override DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        obj_machinery_quantum_server server = find_server() as obj_machinery_quantum_server;
        if (server == null)
        {
            data["connected"] = FALSE;
            return DmTask<object>.FromResult(data);
        }

        data["connected"] = TRUE;
        data["generated_domain"] = server.generated_domain?.key;
        data["occupants"] = length(server.avatar_connection_refs);
        data["points"] = server.points;
        data["randomized"] = server.domain_randomized;
        data["ready"] = Dm.num(Dm.truthy(server.is_ready) && Dm.truthy(server.is_operational));
        data["scanner_tier"] = server.scanner_tier;
        data["retries_left"] = length(server.exit_turfs) - server.retries_spent;
        data["broadcasting"] = server.broadcasting;
        data["broadcasting_on_cd"] = Dm.num(!(server.broadcast_toggle_cd <= world.time)); // !COOLDOWN_FINISHED

        return DmTask<object>.FromResult(data);
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:58
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        obj_machinery_quantum_server server = find_server() as obj_machinery_quantum_server;
        if (server == null)
            return data;

        data["available_domains"] = SSbitrunning.get_available_domains(server.scanner_tier, server.points);
        data["avatars"] = server.get_avatar_data();

        return data;
    }

    // tg: code/modules/bitrunning/objects/quantum_console.dm:70
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;

        obj_machinery_quantum_server server = find_server() as obj_machinery_quantum_server;
        if (server == null)
            return FALSE;

        switch (action)
        {
            case "random_domain":
                await server.cold_boot_map(server.get_random_domain_id(), was_random_selection: TRUE);
                return TRUE;
            case "refresh":
                await ui.send_full_update();
                return TRUE;
            case "set_domain":
                await server.cold_boot_map(@params["id"], was_random_selection: FALSE);
                return TRUE;
            case "stop_domain":
                await server.begin_shutdown(usr);
                return TRUE;
            case "broadcast":
                server.toggle_broadcast();
                return TRUE;
        }

        return FALSE;
    }

    /// Attempts to find a quantum server.
    // tg: code/modules/bitrunning/objects/quantum_console.dm:99
    public virtual object find_server()
    {
        obj_machinery_quantum_server server = server_ref?.resolve() as obj_machinery_quantum_server;
        if (server != null)
            return server;

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            obj_machinery_quantum_server nearby_server = Dm.locate<obj_machinery_quantum_server>(get_step(this, Dm.num(direction)));
            if (nearby_server != null)
            {
                server_ref = WEAKREF(nearby_server) as datum_weakref;
                return nearby_server;
            }
        }
        return null;
    }
}
