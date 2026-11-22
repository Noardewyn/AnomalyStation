// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/transform.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the Transform power, the flesh disguise items the changeling grows in its profile's slots
// (reabsorbed by hand), the flesh ID with its security HUD job icon, and the changeling's disguise menu (select_dna, a radial
// menu of its profiles' snapshots with the "Drop Flesh Disguise" choice; check_menu). The flesh ID's flattened picture in the chat
// (getFlatIcon, icon2html) is the presentation's chat icons (not-translated.md 187). The var blocks are generated (D111).

// tg: code/modules/antagonists/changeling/powers/transform.dm:1
public partial class datum_action_changeling_transform : datum_action_changeling
{
    //Change our DNA to that of somebody we've absorbed.
    // tg: code/modules/antagonists/changeling/powers/transform.dm:173
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        datum_changeling_profile chosen_prof = await changeling.select_dna() as datum_changeling_profile;

        if (chosen_prof == null)
            return null;
        await base.sting_action(user, target);
        await changeling.transform(user as mob_living_carbon_human, chosen_prof);
        if (Dm.truthy(user._listen_lookup?[COMSIG_CHANGELING_TRANSFORM]))
            user._SendSignal(COMSIG_CHANGELING_TRANSFORM, Dm.list(user));
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:10
public partial class obj_item_clothing_glasses_changeling : obj_item_clothing_glasses
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:16
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:23
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:26
public partial class obj_item_clothing_under_changeling : obj_item_clothing_under
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:32
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:39
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:42
public partial class obj_item_clothing_suit_changeling : obj_item_clothing_suit
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:49
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:56
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:59
public partial class obj_item_clothing_head_changeling : obj_item_clothing_head
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:66
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:73
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:76
public partial class obj_item_clothing_shoes_changeling : obj_item_clothing_shoes
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:82
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:89
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:92
public partial class obj_item_clothing_gloves_changeling : obj_item_clothing_gloves
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:98
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:105
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:108
public partial class obj_item_clothing_mask_changeling : obj_item_clothing_mask
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:114
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:121
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:124
public partial class obj_item_changeling : obj_item
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/antagonists/changeling/powers/transform.dm:131
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user && Dm.truthy(IS_CHANGELING(user)))
        {
            to_chat(user, span_notice(I18n.T("You reabsorb [] into your body.", this)));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:138
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:141
public partial class obj_item_changeling_id : obj_item_changeling
{
    // tg: code/modules/antagonists/changeling/powers/transform.dm:150
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(hud_icon))
            return;
        user.set_hud_image_state(ID_HUD, hud_icon, hud_icon_state);
    }

    /**
     * Returns cached flat icon of the ID, creates one if there is not one already cached
     */
    // tg: code/modules/antagonists/changeling/powers/transform.dm:159
    public virtual object get_cached_flat_icon()
    {
        if (cached_flat_icon == null)
        {
            // cached_flat_icon = getFlatIcon(src); cached_flat_icon.Crop(ID_ICON_BORDERS)
            Dm.Deferred("code/modules/antagonists/changeling/powers/transform.dm:161", "get_cached_flat_icon: getFlatIcon of the flesh ID, its picture in chat (chat icons, the presentation's)");
        }
        return cached_flat_icon;
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:165
    public override object get_id_examine_strings(mob user = null)
    {
        object dot = base.get_id_examine_strings(user);
        // . += list("[icon2html(get_cached_flat_icon(), user, extra_classes = "hugeicon")]"): the ID's picture in the chat line
        Dm.Deferred("code/modules/antagonists/changeling/powers/transform.dm:167", "get_id_examine_strings: icon2html of the flesh ID's flat icon (the 3D client's chat is text: the chat icons)");
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/transform.dm:169
    public override object get_examine_icon(mob user = null)
    {
        Dm.Deferred("code/modules/antagonists/changeling/powers/transform.dm:170", "get_examine_icon: icon2html of the flesh ID's flat icon (the 3D client's chat is text: the chat icons)");
        return null;
    }
}

// tg: code/modules/antagonists/changeling/powers/transform.dm:187 (the changeling antagonist's procs defined in this file)
public partial class datum_antagonist_changeling : datum_antagonist
{
    /**
     * Gives a changeling a list of all possible dnas in their profiles to choose from and returns profile containing their chosen dna
     */
    // tg: code/modules/antagonists/changeling/powers/transform.dm:187
    public virtual async DmTask<object> select_dna()
    {
        mob_living_carbon user = owner.current as mob_living_carbon;
        if (user is not mob_living_carbon)
            return null;

        DmList disguises = Dm.list(Dm.kv(I18n.T("Drop Flesh Disguise"), image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_drop")));
        foreach (datum_changeling_profile current_profile in Dm.each_cast<datum_changeling_profile>(stored_profiles))
        {
            datum_icon_snapshot snap = current_profile.profile_snapshot;
            image disguise_image = image(icon: snap.icon, icon_state: snap.icon_state);
            disguise_image.overlays = snap.overlays;
            disguises[current_profile.name] = disguise_image;
        }

        object chosen_name = await show_radial_menu(user, user, disguises, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 40, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(chosen_name))
            return null;

        if (Dm.eq(chosen_name, I18n.T("Drop Flesh Disguise")))
        {
            foreach (object slot in Dm.each(slot2type))
            {
                // user.vars[slot]: the slot's var read by name (DmVars, as a:b; rules 4.8, 10.7)
                if (istype(Dm.get_var(user, (string)slot), slot2type[slot] as Type))
                    qdel(Dm.get_var(user, (string)slot));
            }
            foreach (object i in Dm.each(user.all_scars))
            {
                datum_scar iter_scar = i as datum_scar;
                if (Dm.truthy(iter_scar.fake))
                    qdel(iter_scar);
            }
            return null;
        }

        datum_changeling_profile prof = get_dna(chosen_name) as datum_changeling_profile;
        return prof;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The carbon mob interacting with the menu
     */
    // tg: code/modules/antagonists/changeling/powers/transform.dm:222
    public virtual object check_menu(mob_living_carbon user = null)
    {
        if (user is not mob_living_carbon)
            return FALSE;
        object changeling_datum = IS_CHANGELING(user);
        if (!Dm.truthy(changeling_datum))
            return FALSE;
        return TRUE;
    }
}
