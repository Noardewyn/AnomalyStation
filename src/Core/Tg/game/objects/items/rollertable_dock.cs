// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rollertable_dock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The rolling table dock (the service cyborg's upgrade) starts loaded with a rolling table and
// ejects it onto a clicked free open floor; empty, it says so, and the rolling table docks back into it (tables_racks.cs). tg
// defines deploy_rolling_table twice (:21 and :30); the later one, which keeps the dock, is the one that runs (rules 2.5: the earlier
// is deploy_rolling_table__d0). The var block is generated (D111).

// tg: code/game/objects/items/rollertable_dock.dm:1
public partial class obj_item_rolling_table_dock : obj_item
{
    // tg: code/game/objects/items/rollertable_dock.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        loaded = Dm.@new<obj_structure_table_rolling>(this);
        return dot;
    }

    // tg: code/game/objects/items/rollertable_dock.dm:12
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        turf target_turf = get_turf(interacting_with) as turf;
        if (Dm.truthy(await target_turf.is_blocked_turf(TRUE)) || Dm.truthy(Dm.locate_type(typeof(mob_living), target_turf)))
            return NONE;
        if (isopenturf(interacting_with))
        {
            await deploy_rolling_table(user, interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/rollertable_dock.dm:21  (the earlier of two definitions, rules 2.5)
    public DmTask deploy_rolling_table__d0(mob user = null, atom location = null)
    {
        obj_structure_table_rolling rable = Dm.@new<obj_structure_table_rolling>(location);
        rable.add_fingerprint(user);
        qdel(this);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/rollertable_dock.dm:26
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The dock is [].", loaded != null ? I18n.T("loaded") : I18n.T("empty")));
        return dot;
    }

    // tg: code/game/objects/items/rollertable_dock.dm:30
    public virtual async DmTask deploy_rolling_table(mob user = null, atom location = null)
    {
        if (loaded != null)
        {
            await loaded.forceMove(location);
            user.visible_message(span_notice(I18n.T("[] deploys [].", user, loaded)), balloon_alert(user, I18n.T("you deploy the [].", loaded)));
            loaded = null;
        }
        else
            balloon_alert(user, I18n.T("the dock is Empty!"));
    }
}
