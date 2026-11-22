// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/openspace_item_click_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (rods and floor tiles carry it); its diverted click onto another level since
// D325 (multi-z).

/**
 * allow players to easily use items such as iron rods, rcds on open space without
 * having to pixelhunt for portions not occupied by object or mob visuals.
 */
// tg: code/datums/elements/openspace_item_click_handler.dm:5
public partial class datum_element_openspace_item_click_handler : datum_element
{
    // tg: code/datums/elements/openspace_item_click_handler.dm:7
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM, nameof(divert_interaction));
        return dot;
    }

    // tg: code/datums/elements/openspace_item_click_handler.dm:13
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM);
        return base.Detach(source, __extra);
    }

    //Invokes the proctype with a turf above as target.
    // tg: code/datums/elements/openspace_item_click_handler.dm:18
    [SignalHandler]
    public virtual object divert_interaction(obj_item source = null, mob user = null, atom target = null, DmList modifiers = null)
    {
        if ((target.z == 0) || (user.z == 0) || target.z == user.z)
            return NONE;
        // DEVIATION(DV368): tg's parse_caught_click_modifiers(modifiers, get_turf(user.client?.eye || user), user.client) is the
        // turf under the 2D client's click catcher on the eye's level; the first-person client clicks the thing under its ray, so
        // the turf is the one of the user's level over (or under) the clicked thing
        turf target_turf = locate(target.x, target.y, user.z) as turf;
        if (target_turf?.z == user.z && Dm.truthy(Waiting.NoSleep(target_turf.IsReachableBy(user, source?.reach))))
        {
            Waiting.Detach(source.handle_openspace_click(target_turf, user, modifiers));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }
}
