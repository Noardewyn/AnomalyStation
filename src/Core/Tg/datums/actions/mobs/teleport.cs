// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/teleport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/teleport.dm:1
public partial class datum_action_cooldown_mob_cooldown_teleport : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/teleport.dm:10
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await teleport_to(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Handles randomly teleporting the owner around the target in view
    // tg: code/datums/actions/mobs/teleport.dm:18
    public virtual async DmTask teleport_to(atom teleport_target = null)
    {
        DmList possible_ends = Dm.sub(view(radius, teleport_target.loc), view(radius - 1, teleport_target.loc)) as DmList;
        foreach (turf_closed cant_teleport_turf in Dm.each<turf_closed>(possible_ends))
            Dm.remove(ref possible_ends, cant_teleport_turf);
        if (!Dm.truthy(possible_ends.len))
            return;
        turf end = pick(possible_ends) as turf;
        await do_teleport(owner, end, 0, channel: TELEPORT_CHANNEL_BLUESPACE, forced: TRUE);
    }
}
