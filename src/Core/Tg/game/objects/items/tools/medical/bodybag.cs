// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/medical/bodybag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298): the folded body bags.

// tg: code/game/objects/items/tools/medical/bodybag.dm:3
public partial class obj_item_bodybag : obj_item
{
    // tg: code/game/objects/items/tools/medical/bodybag.dm:10
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.is_holding(this)))
            await deploy_bodybag(user, get_turf(user));
        else
            await deploy_bodybag(user, get_turf(this));
        return null;
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:16
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (isopenturf(interacting_with))
        {
            await deploy_bodybag(user, interacting_with);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:22
    public override async DmTask<object> attempt_pickup(mob_living user = null, float? skip_grav = null)
    {
        // can't pick ourselves up if we are inside of the bodybag, else very weird things may happen
        if (Dm.truthy(contains(user)))
            return TRUE;
        return await base.attempt_pickup(user, skip_grav);
    }

    /**
     * Creates a new body bag item when unfolded, at the provided location, replacing the body bag item.
     * * mob/user: User opening the body bag.
     * * atom/location: the place/entity/mob where the body bag is being deployed from.
     */
    // tg: code/game/objects/items/tools/medical/bodybag.dm:33
    public virtual async DmTask<obj_structure_closet_body_bag> deploy_bodybag(mob user = null, atom location = null)
    {
        obj_structure_closet_body_bag item_bag = Dm.@new(unfoldedbag_path, location) as obj_structure_closet_body_bag;
        await item_bag.open(user as mob_living);
        item_bag.add_fingerprint(user);
        item_bag.foldedbag_instance = this;
        await moveToNullspace();
        return item_bag;
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:41
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (isopenturf(user.loc))
        {
            user.visible_message(span_suicide(I18n.T("[] is crawling into []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            obj_structure_closet_body_bag R = Dm.@new(unfoldedbag_path, user.loc) as obj_structure_closet_body_bag;
            R.add_fingerprint(user);
            qdel(this);
            await user.forceMove(R);
            playsound(this, Dm.resource("sound/items/zip/zip.ogg"), 15, TRUE, -3);
            return OXYLOSS;
        }
        return null;
    }
}

// Bluespace bodybag

// tg: code/game/objects/items/tools/medical/bodybag.dm:53
public partial class obj_item_bodybag_bluespace : obj_item_bodybag
{
    // tg: code/game/objects/items/tools/medical/bodybag.dm:63
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(contents.len))
        {
            string s = contents.len == 1 ? "" : "s";
            dot.Add(span_notice(I18n.T("You can make out the shape[] of [] object[] through the fabric.", s, contents.len, s)));
        }
        return dot;
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:69
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable A in Dm.each<atom_movable>(contents.Copy()))
        {
            Waiting.NoSleep(A.forceMove(get_turf(this)));
            if (isliving(A))
                to_chat(A, span_notice(I18n.T("You suddenly feel the space around you torn apart! You're free!")));
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:76
    public override async DmTask<obj_structure_closet_body_bag> deploy_bodybag(mob user = null, atom location = null)
    {
        obj_structure_closet_body_bag item_bag = Dm.@new(unfoldedbag_path, location) as obj_structure_closet_body_bag;
        foreach (atom_movable inside in Dm.each<atom_movable>(contents.Copy()))
        {
            await inside.forceMove(item_bag);
            if (isliving(inside))
                to_chat(inside, span_notice(I18n.T("You suddenly feel air around you! You're free!")));
        }
        await item_bag.open(user as mob_living);
        item_bag.add_fingerprint(user);
        item_bag.foldedbag_instance = this;
        await moveToNullspace();
        return item_bag;
    }

    // tg: code/game/objects/items/tools/medical/bodybag.dm:88
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (Dm.truthy(user.incapacitated))
        {
            to_chat(user, span_warning(I18n.T("You can't get out while you're restrained like this!")));
            return null;
        }
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        to_chat(user, span_notice(I18n.T("You claw at the fabric of [], trying to tear it open...", this)));
        to_chat(loc, span_warning(I18n.T("Someone starts trying to break free of []!", this)));
        if (!Dm.truthy(await do_after(user, 12 * SECONDS, this, timed_action_flags: Dm.bitor(IGNORE_TARGET_LOC_CHANGE, IGNORE_HELD_ITEM))))
            return null;
        // you are still in the bag? time to go unless you KO'd, honey!
        // if they escape during this time and you rebag them the timer is still clocking down and does NOT reset so they can very easily get out.
        if (Dm.truthy(user.incapacitated))
        {
            to_chat(loc, span_warning(I18n.T("The pressure subsides. It seems that they've stopped resisting...")));
            return null;
        }
        loc.visible_message(span_warning(I18n.T("[] suddenly appears in front of []!", user, loc)), span_userdanger(I18n.T("[] breaks free of []!", user, this)));
        qdel(this);
        return null;
    }
}

// tg: code/game/objects/items/tools/medical/bodybag.dm:149
public partial class obj_item_bodybag_stasis : obj_item_bodybag
{
    // tg: code/game/objects/items/tools/medical/bodybag.dm:149
    public override async DmTask<obj_structure_closet_body_bag> deploy_bodybag(mob user = null, atom location = null)
    {
        obj_structure_closet_body_bag_environmental_stasis bag = await base.deploy_bodybag(user, location) as obj_structure_closet_body_bag_environmental_stasis;
        bag.last_filter_update = -1;
        bag.update_integrity(get_integrity());
        return bag;
    }
}
