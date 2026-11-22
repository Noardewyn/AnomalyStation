// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/observer_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: can_z_move (the ghosts, 2026-10-16, D151) and the ghost's Move Up and Move Down (DV422: Space and Ctrl).

// tg: code/modules/mob/dead/observer/observer_movement.dm:1
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/observer_movement.dm:1
    public override async DmTask<object> down()
    {
        if (Dm.truthy(await zMove(DOWN, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move down.")));
        return null;
    }

    // tg: code/modules/mob/dead/observer/observer_movement.dm:5
    public override async DmTask<object> up()
    {
        if (Dm.truthy(await zMove(UP, z_move_flags: ZMOVE_FEEDBACK)))
            to_chat(this, span_notice(I18n.T("You move upwards.")));
        return null;
    }
}

// tg: code/modules/mob/dead/observer/observer_movement.dm:9
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/observer_movement.dm:9
    public override object can_z_move(object direction = null, turf start = null, turf destination = null, float? z_move_flags = null, mob_living rider = null)
    {
        z_move_flags ??= NONE;
        z_move_flags = Dm.num(Dm.bitor(z_move_flags, ZMOVE_IGNORE_OBSTACLES)); //observers do not respect these FLOORS you speak so much of.
        return base.can_z_move(direction, start, destination, z_move_flags, rider);
    }
}
