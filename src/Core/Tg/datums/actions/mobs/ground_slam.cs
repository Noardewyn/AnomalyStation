// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/ground_slam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/ground_slam.dm:1
public partial class datum_action_cooldown_mob_cooldown_ground_slam : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/ground_slam.dm:16
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        RegisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE, nameof(on_move), @override: TRUE);
        await do_slam(target_atom);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Slams the ground around the source throwing back enemies caught nearby, delay is for the radius increase
    // tg: code/datums/actions/mobs/ground_slam.dm:26
    public virtual async DmTask do_slam(atom target = null)
    {
        await wendigo_slam(owner, range, delay, throw_range);
    }

    // tg: code/datums/actions/mobs/ground_slam.dm:29
    [SignalHandler]
    public virtual object on_move(atom source = null, atom new_loc = null)
    {
        if (!Dm.truthy(can_move))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        return null;
    }
}
