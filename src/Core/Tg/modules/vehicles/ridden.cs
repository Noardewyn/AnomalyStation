// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/ridden.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the ridden vehicle (buckling onto it, its key put in by hand and taken out with an
// alt-click by its rider, the examine line about the key, the occupant added on buckling). The var block is generated (D111).

// tg: code/modules/vehicles/ridden.dm:1
public partial class obj_vehicle_ridden : obj_vehicle
{
    // tg: code/modules/vehicles/ridden.dm:10
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        object key_message = examine_key_message();
        if (Dm.truthy(key_message))
            Dm.append(ref dot, key_message);
        return dot;
    }

    // tg: code/modules/vehicles/ridden.dm:16
    public virtual object examine_key_message()
    {
        if (key_type == null)
            return null;
        if (inserted_key == null)
            return span_notice(I18n.T("Put a key inside it by clicking it with the [].", Dm.initial(key_type, (obj o) => o.name)));
        else
            return span_notice(I18n.T("Alt-click [] to remove \\the [].", this, inserted_key));
    }

    // tg: code/modules/vehicles/ridden.dm:24
    public override object generate_action_type(Type actiontype = null)
    {
        var A = base.generate_action_type(actiontype) as datum_action_vehicle_ridden;
        object dot = A;
        if (A != null)
            A.vehicle_ridden_target = this;
        return dot;
    }

    // tg: code/modules/vehicles/ridden.dm:30
    public override async DmTask post_unbuckle_mob(mob_living M = null)
    {
        await remove_occupant(M);
        await base.post_unbuckle_mob(M);
    }

    // tg: code/modules/vehicles/ridden.dm:34
    public override async DmTask post_buckle_mob(mob_living M = null)
    {
        await add_occupant(M);
        await base.post_buckle_mob(M);
    }

    // tg: code/modules/vehicles/ridden.dm:38
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (key_type == null || is_key(inserted_key) || !is_key(tool))
            return NONE;
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] seems to be stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You insert \\the [] into \\the [].", tool, this)));
        if (inserted_key != null) //just in case there's an invalid key
            await inserted_key.forceMove(drop_location() as atom);
        inserted_key = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/ridden.dm:50
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (inserted_key == null)
            return CLICK_ACTION_BLOCKING;
        if (!is_occupant(user))
        {
            to_chat(user, span_warning(I18n.T("You must be riding the [] to remove []'s []!", this, this, inserted_key)));
            return CLICK_ACTION_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You remove \\the [] from \\the [].", inserted_key, this)));
        await user.put_in_hands(inserted_key);
        inserted_key = null;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/vehicles/ridden.dm:61
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        if (!Dm.truthy(in_range(user, this)) || !Dm.truthy(in_range(M, this)))
            return FALSE;
        return await base.user_buckle_mob(M, user, FALSE);
    }

    // tg: code/modules/vehicles/ridden.dm:66
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(force) && occupant_amount() >= max_occupants)
            return FALSE;

        object response = M.SEND_SIGNAL(COMSIG_VEHICLE_RIDDEN, Dm.list(M, this));
        if (Dm.truthy(Dm.bitand(response, EJECT_FROM_VEHICLE)))
            return FALSE;

        return await base.buckle_mob(M, force, check_loc, buckle_mob_flags);
    }

    // tg: code/modules/vehicles/ridden.dm:76
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        await zap_buckle_check(power);
        return await base.zap_act(power, zap_flags);
    }
}
