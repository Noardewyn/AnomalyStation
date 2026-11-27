// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the admins' setups (/datum/event_admin_setup, as the other events'): the bitrunning glitch event, which picks
// a running quantum server with a domain mob to mutate and lets it spawn a glitch for a ghost. The var blocks are generated.

// tg: code/modules/bitrunning/event.dm:1
public partial class datum_round_event_control_bitrunning_glitch : datum_round_event_control
{
    // tg: code/modules/bitrunning/event.dm:16
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        allow_magic ??= FALSE;
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        active_servers.Cut();

        validate_servers();

        if (Dm.truthy(length(active_servers)))
            return TRUE;
        // kept as tg has it: the proc ends with the parent's truthy result, so the event may run without a server (spawn_role then waits)
        return dot;
    }

    /// All servers currently running, has players in it, and map has valid mobs
    // tg: code/modules/bitrunning/event.dm:29
    public virtual object validate_servers()
    {
        active_servers.Cut();
        foreach (obj_machinery_quantum_server server in Dm.each<obj_machinery_quantum_server>(SSmachines.get_machines_by_type(typeof(obj_machinery_quantum_server))))
        {
            if (Dm.truthy(server.validate_mutation_candidates()) && server.generated_domain.difficulty != BITRUNNER_DIFFICULTY_NONE)
                active_servers.Add(WEAKREF(server));
        }

        return Dm.num(length(active_servers) > 0);
    }
}

// tg: code/modules/bitrunning/event.dm:71
public partial class datum_round_event_ghost_role_bitrunning_glitch : datum_round_event_ghost_role
{
    // tg: code/modules/bitrunning/event.dm:77
    public override async DmTask<object> spawn_role()
    {
        datum_round_event_control_bitrunning_glitch cyber_control = control as datum_round_event_control_bitrunning_glitch;
        if (!Dm.truthy(length(cyber_control.active_servers)))
            return WAITING_FOR_SOMETHING;

        datum_weakref server_ref = pick(cyber_control.active_servers) as datum_weakref;
        obj_machinery_quantum_server unlucky_server = server_ref?.resolve() as obj_machinery_quantum_server;
        if (unlucky_server == null)
            return WAITING_FOR_SOMETHING;

        cyber_control.active_servers.Cut();
        if (!Dm.truthy(unlucky_server.validate_mutation_candidates()))
            return WAITING_FOR_SOMETHING;

        mob spawned = await unlucky_server.setup_glitch(forced_role as Type) as mob;
        if (spawned == null)
            return WAITING_FOR_SOMETHING;

        Dm.append(spawned_mobs, spawned);

        return SUCCESSFUL_SPAWN;
    }
}
