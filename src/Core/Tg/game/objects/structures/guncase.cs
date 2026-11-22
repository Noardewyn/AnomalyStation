// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/guncase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The gun lockers (any gun, the shotgun, energy gun and WT-550 lockers): guns of their kind on the
// map turf are taken in at load, up to four; a gun is put in while the door is open, a hand opens and closes the door or, with guns
// inside and the door open, offers them on a radial menu; the sprite shows one gun per slot and the door open or shut. The var
// blocks are generated (D111).

//GUNCASES//
// tg: code/game/objects/structures/guncase.dm:2
public partial class obj_structure_guncase : obj_structure
{
    // tg: code/game/objects/structures/guncase.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            foreach (obj_item I in Dm.each<obj_item>(loc.contents))
            {
                if (istype(I, gun_category))
                    Waiting.NoSleep(I.forceMove(this));
                if (Dm.length(contents) >= capacity)
                    break;
            }
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/guncase.dm:25
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(case_type) && Dm.truthy(LAZYLEN(contents)))
        {
            mutable_appearance gun_overlay = (mutable_appearance)mutable_appearance(icon, case_type);
            for (int i = 1; i <= Dm.length(contents); i++)
            {
                gun_overlay.pixel_w = 3 * (i - 1);
                Dm.append(ref dot, Dm.@new<mutable_appearance>(gun_overlay));
            }
        }
        Dm.append(ref dot, Dm.text("[]_[]", icon_state, Dm.truthy(open) ? "open" : "door"));
        return dot;
    }

    // tg: code/game/objects/structures/guncase.dm:34
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (iscyborg(user) || isalien(user))
            return NONE;
        if (istype(tool, gun_category) && Dm.truthy(open))
        {
            if (Dm.eq(LAZYLEN(contents), capacity))
            {
                to_chat(user, span_warning(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You place [] in [].", tool, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode))
        {
            open = Dm.truthy(open) ? FALSE : TRUE;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/guncase.dm:56
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (iscyborg(user) || isalien(user))
            return dot;
        if (Dm.length(contents) > 0 && Dm.truthy(open))
            await show_menu(user);
        else
        {
            open = Dm.truthy(open) ? FALSE : TRUE;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/structures/guncase.dm:68
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (iscyborg(user) || isalien(user))
            return await base.attack_hand_secondary(user, modifiers);
        open = Dm.truthy(open) ? FALSE : TRUE;
        update_appearance();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * show_menu: Shows a radial menu to a user consisting of an available weaponry for taking
     *
     * Arguments:
     * * user The mob to which we are showing the radial menu
     */
    // tg: code/game/objects/structures/guncase.dm:81
    public virtual async DmTask show_menu(mob user = null)
    {
        if (!Dm.truthy(LAZYLEN(contents)))
            return;

        DmList display_names = Dm.list();
        DmList items = Dm.list();
        for (int i = 1; i <= Dm.length(contents); i++)
        {
            obj_item thing = contents[i] as obj_item;
            display_names[Dm.text("[] ([])", thing.name, i)] = REF(thing);
            image item_image = image(icon: thing.icon, icon_state: thing.icon_state);
            if (Dm.length(thing.overlays) > 0)
                item_image.overlays = thing.overlays.Copy(); // item_image.copy_overlays(thing): /image/proc/copy_overlays (overlays.dm:122) on an image with none of its own
            Dm.append(ref items, Dm.list(Dm.kv(Dm.text("[] ([])", thing.name, i), item_image)));
        }

        object pick = await show_radial_menu(user, this, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE);
        if (!Dm.truthy(pick))
            return;

        object weapon_reference = display_names[pick];
        obj_item weapon = Dm.locate_ref(weapon_reference) is obj_item found && Dm.@in(found, contents) ? found : null; // locate(weapon_reference) in contents
        if (weapon == null)
            return;
        if (!Dm.truthy(await user.put_in_hands(weapon)))
            await weapon.forceMove(get_turf(this));
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with a menu
     */
    // tg: code/game/objects/structures/guncase.dm:112
    public virtual object check_menu(mob_living_carbon_human user = null)
    {
        if (!Dm.truthy(open))
            return FALSE;
        if (user == null) // !istype(user, /mob/living/carbon/human): the parameter is typed
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/guncase.dm:121
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/guncase.dm:125
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, contents);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, contents);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, contents);
                break;
        }
        await DmTask.CompletedTask;
    }
}
