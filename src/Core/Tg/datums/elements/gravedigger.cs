// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/gravedigger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole: right click with a shovel on dirt digs a grave.

// tg: code/datums/elements/gravedigger.dm:5
public partial class datum_element_gravedigger : datum_element
{
    // tg: code/datums/elements/gravedigger.dm:19
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        await base.Attach(target, __extra);

        if (!isitem(target)) //Must be an item to use toolspeed variable.
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY, nameof(dig_checks));
        return null;
    }

    // tg: code/datums/elements/gravedigger.dm:27
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY);
        return dot;
    }

    // tg: code/datums/elements/gravedigger.dm:31
    public virtual object dig_checks(datum source = null, mob_living user = null, atom interacting_with = null, DmList modifiers = null)
    {
        if (!Dm.truthy(is_type_in_typecache(interacting_with, TgState.Current.datum_element_gravedigger__turfs_to_consider)))
            return NONE;

        if (Dm.locate<obj_structure_closet_crate_grave>(interacting_with) != null)
        {
            user.balloon_alert(user, I18n.T("grave already present!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.balloon_alert(user, I18n.T("digging grave..."));
        playsound(interacting_with, Dm.resource("sound/effects/shovel_dig.ogg"), 50, TRUE);
        Waiting.Detach(perform_digging(user, interacting_with, source as obj_item));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/datums/elements/gravedigger.dm:46
    public virtual async DmTask perform_digging(mob user = null, atom dig_area = null, obj_item our_tool = null)
    {
        if (Dm.truthy(await our_tool.use_tool(dig_area, user as mob_living, 10 * SECONDS * (HAS_MIND_TRAIT(user, TRAIT_MORBID) ? 0.7f : 1) * Dm.num(our_tool.toolspeed))))
            Dm.@new<obj_structure_closet_crate_grave_fresh>(dig_area); //We don't get_turf for the location since this is guaranteed to be a turf at this point.
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/gravedigger.dm:11  var/static/list/turfs_to_consider
    internal DmList datum_element_gravedigger__turfs_to_consider = typecacheof(Dm.list(
        typeof(turf_open_misc_asteroid),
        typeof(turf_open_misc_dirt),
        typeof(turf_open_misc_grass),
        typeof(turf_open_misc_basalt),
        typeof(turf_open_misc_ashplanet),
        typeof(turf_open_misc_snow),
        typeof(turf_open_misc_sandy_dirt))) as DmList;
}
