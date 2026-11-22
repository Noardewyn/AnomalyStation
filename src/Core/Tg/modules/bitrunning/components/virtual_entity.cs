// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/virtual_entity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the domain's threats kept out of the safehouse (VIRTUAL_SAFE_AREA) until the server is
// emagged. The var block is generated (D111).

/// Handles all special considerations for "virtual entities" such as bitrunning ghost roles or digital anomaly antagonists.
// tg: code/modules/bitrunning/components/virtual_entity.dm:2
public partial class datum_component_virtual_entity : datum_component
{
    // tg: code/modules/bitrunning/components/virtual_entity.dm:7
    [DmParams("quantum_server")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await DmTask.CompletedTask;
        obj_machinery quantum_server = Dm.arg(__extra, 0) as obj_machinery;
        if (Dm.truthy(Dm.bitand(quantum_server.obj_flags, EMAGGED)))
        {
            jailbreak_mobs();
            return COMPONENT_REDUNDANT;
        }

        RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(on_parent_pre_move));
        RegisterSignal(quantum_server, COMSIG_ATOM_EMAG_ACT, nameof(on_emagged));
        return null;
    }

    /// Self-destructs the component, allowing free-roam by all entities with this restriction.
    // tg: code/modules/bitrunning/components/virtual_entity.dm:17
    public virtual void jailbreak_mobs()
    {
        to_chat(parent, span_bolddanger(I18n.T("You shiver for a moment with a sense of clarity you haven't felt before.")));
        to_chat(parent, span_notice(I18n.T("You could go <i>anywhere</i>, do <i>anything</i>! You could leave this simulation right now if you wanted!")));
        to_chat(parent, span_danger(I18n.T("But be warned, quantum entanglement will interfere with any previous lives.")));
        to_chat(parent, span_notice(I18n.T("You'll have just one chance to go nova, and there's no turning back.")));
    }

    /// Remove any restrictions AFTER the mob has spawned
    // tg: code/modules/bitrunning/components/virtual_entity.dm:25
    [SignalHandler]
    public virtual void on_emagged(datum source = null)
    {
        jailbreak_mobs();
        qdel(this);
    }

    /// Prevents entry to a certain area if it has flags preventing virtual entities from entering.
    // tg: code/modules/bitrunning/components/virtual_entity.dm:33
    [SignalHandler]
    public virtual object on_parent_pre_move(atom_movable source = null, atom new_location = null)
    {
        area location_area = get_area(new_location) as area;
        if (location_area == null)
        {
            stack_trace("Virtual entity entered a location with no area!", "code/modules/bitrunning/components/virtual_entity.dm", 38);
            return null;
        }

        if (Dm.truthy(Dm.bitand(location_area.area_flags_mapping, VIRTUAL_SAFE_AREA)))
        {
            source.balloon_alert(source as mob, I18n.T("out of bounds!"));
            OOB_cooldown = world.time + 2 * SECONDS; // COOLDOWN_START
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        }
        return null;
    }
}
