// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_decorators/target_can_be_shove_stunned.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Passes when shoving the target right now would knock them down rather than just push them back. In the future this could maybe check for any options, right now ti just checks from me to target
// tg: code/datums/ai/generic_decorators/target_can_be_shove_stunned.dm:1
public partial class datum_bt_node_decorator_target_can_be_shove_stunned : datum_bt_node_decorator
{
    // tg: code/datums/ai/generic_decorators/target_can_be_shove_stunned.dm:6
    public override async DmTask<object> check_condition(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        mob_living living_target = controller.blackboard[target_key] as mob_living;

        if (!isliving(living_pawn) || !isliving(living_target))
            return FALSE;

        // Standing up, not ourselves, not sharing a turf. Also guarantees a real shove direction below.
        if (!Dm.truthy(living_pawn.can_disarm(living_target)))
            return FALSE;

        // Authoritative source for whether this pairing can hit something, is knockdown immune, or gets side kicked.
        object shove_flags = living_target.get_shove_flags(living_pawn, null);
        if (!Dm.truthy(Dm.bitand(shove_flags, SHOVE_CAN_HIT_SOMETHING)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(shove_flags, Dm.bitor(SHOVE_KNOCKDOWN_BLOCKED, SHOVE_CAN_KICK_SIDE))))
            return FALSE;

        return await shove_will_knockdown(living_pawn, living_target);
    }

    /// Whether the target has nowhere to be pushed, which is what turns a shove into a knockdown. (and thus succesful)
    // tg: code/datums/ai/generic_decorators/target_can_be_shove_stunned.dm:27
    public virtual async DmTask<object> shove_will_knockdown(mob_living living_pawn = null, mob_living living_target = null)
    {
        float shove_dir = get_dir(living_pawn.loc, living_target.loc);
        turf target_turf = get_turf(living_target);
        turf shove_turf = get_step(target_turf, shove_dir);

        // Nothing behind them at all, so they've got nowhere to go.
        if (shove_turf == null)
            return TRUE;

        // Passing the target as source_atom runs CanPass, which covers directional windows on the far turf.
        if (Dm.truthy(await shove_turf.is_blocked_turf(source_atom: living_target)))
            return TRUE;

        // A border object on their own turf facing the way we'd shove stops them leaving it in the first place,
        // which is_blocked_turf() on the destination can't see.
        if (!Dm.@in(shove_dir, GLOB.cardinals))
            return FALSE;
        foreach (obj obj_content in Dm.each<obj>(target_turf))
        {
            if (Dm.truthy(Dm.bitand(obj_content.flags_1, ON_BORDER_1)) && Dm.eq(obj_content.dir, shove_dir) && Dm.truthy(obj_content.density))
                return TRUE;
        }

        return FALSE;
    }
}
