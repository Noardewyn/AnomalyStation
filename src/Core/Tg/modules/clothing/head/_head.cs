// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/_head.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /obj/item/clothing/head's procs translated (M4a clothing: hats are put on, taken off and drawn on the wearer;
// M4c slice 2: can_throw_equip and throw_impact, a hat thrown onto a head, so that the head type and its helmets are live;
// the cyborg branch since silicons SI2). The dog fashion and the subtypes of this file are translated with their
// mechanics (M9, M10).

// tg: code/modules/clothing/head/_head.dm:1
public partial class obj_item_clothing_head : obj_item_clothing
{
    // tg: code/modules/clothing/head/_head.dm:61
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damagedhelmet"));
        return dot;
    }

    // tg: code/modules/clothing/head/_head.dm:68
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        object blood_overlay = await get_blood_overlay("helmet", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/head/_head.dm:76
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.update_worn_head();
        }
    }

    /// Can we land on people's heads?
    // tg: code/modules/clothing/head/_head.dm:12
    public virtual object can_throw_equip(atom hit_atom = null)
    {
        return TRUE;
    }

    // Special throw_impact for hats to frisbee hats at people to place them on their heads/attempt to de-hat them.
    // tg: code/modules/clothing/head/_head.dm:16  (waitfor = FALSE inherited from /atom/movable/throw_impact; the calls below
    // that are coloured do not sleep here: equip_to_slot_if_possible bypasses the equip delay, waiting-model.md 5.4)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing thrownthing = null, params object[] __extra)
    {
        object dot = null;
        dot = base.throw_impact(hit_atom, thrownthing, __extra);
        if (!Dm.truthy(can_throw_equip(hit_atom)))
            return dot;
        // if the thrown object's target zone isn't the head
        if (!Dm.eq(thrownthing.target_zone, BODY_ZONE_HEAD))
            return dot;
        // if the hat happens to be capable of holding contents and has something in it. mostly to prevent super cheesy stuff like stuffing a mini-bomb in a hat and throwing it
        if (Dm.truthy(LAZYLEN(contents)))
            return dot;
        if (iscarbon(hit_atom))
        {
            mob_living_carbon H = hit_atom as mob_living_carbon;
            obj_item WH = H.get_item_by_slot(ITEM_SLOT_HEAD) as obj_item;
            if (WH is obj_item)
            {
                ///check if the item has NODROP
                if (HAS_TRAIT(WH, TRAIT_NODROP))
                {
                    H.visible_message(span_warning(I18n.T("[] bounces off []'s []!", this, H, WH.name)), span_warning(I18n.T("[] bounces off your [], falling to the floor.", this, WH.name)));
                    return dot;
                }
                ///check if the item is an actual clothing head item, since some non-clothing items can be worn
                if (WH is obj_item_clothing_head)
                {
                    obj_item_clothing_head WHH = WH as obj_item_clothing_head;
                    ///SNUG_FIT hats are immune to being knocked off
                    if (Dm.truthy(Dm.bitand(WHH.clothing_flags, SNUG_FIT)))
                    {
                        H.visible_message(span_warning(I18n.T("[] bounces off []'s []!", this, H, WHH.name)), span_warning(I18n.T("[] bounces off your [], falling to the floor.", this, WHH.name)));
                        return dot;
                    }
                }
                ///if the hat manages to knock something off
                if (Dm.truthy(Waiting.NoSleep(H.dropItemToGround(WH))))
                    H.visible_message(span_warning(I18n.T("[] knocks [] off []'s head!", this, WH, H)), span_warning(I18n.T("[] is suddenly knocked off your head by []!", WH, this)));
            }
            if (Dm.truthy(Waiting.NoSleep(H.equip_to_slot_if_possible(this, ITEM_SLOT_HEAD, 0, 1, 1))))
            {
                H.visible_message(span_notice(I18n.T("[] lands neatly on []'s head!", this, H)), span_notice(I18n.T("[] lands perfectly onto your head!", this)));
                Waiting.NoSleep(H.update_held_items()); //force update hands to prevent ghost sprites appearing when throw mode is on
            }
            return dot;
        }
        if (iscyborg(hit_atom))
        {
            mob_living_silicon_robot R = (mob_living_silicon_robot)hit_atom;
            obj_item worn_hat = R.hat;
            if (worn_hat != null && HAS_TRAIT(worn_hat, TRAIT_NODROP))
            {
                R.visible_message(span_warning(I18n.T("[] bounces off [], without an effect!", this, worn_hat)), span_warning(I18n.T("[] bounces off your mighty [], falling to the floor in defeat.", this, worn_hat.name)));
                return dot;
            }
            if (Dm.truthy(is_type_in_typecache(this, GLOB.blacklisted_borg_hats))) //hats in the borg's blacklist bounce off
            {
                R.visible_message(span_warning(I18n.T("[] bounces off []!", this, R)), span_warning(I18n.T("[] bounces off you, falling to the floor.", this)));
                return dot;
            }
            else
            {
                R.visible_message(span_notice(I18n.T("[] lands neatly on top of []!", this, R)), span_notice(I18n.T("[] lands perfectly on top of you.", this)));
                Waiting.NoSleep(R.place_on_head(this)); //hats aren't designed to snugly fit borg heads or w/e so they'll always manage to knock eachother off
            }
        }
        return dot;
    }
}
