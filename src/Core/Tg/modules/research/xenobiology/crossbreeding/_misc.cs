// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbreeds' items (the rewind and timefreeze cameras, the hypercharged slime core, the barrier cube and its
// barricade, the gel walls, the ice stasis block, the gold capture device), with /datum/saved_bodypart, apply_saved_bodyparts and
// save_bodyparts (the limbs the dejavu component puts back, datums/components/dejavu.dm). The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:9
//Rewind camera - I'm already Burning Sepia
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:9
public partial class obj_item_camera_rewind : obj_item_camera
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:48
    public override async DmTask on_flash(atom target = null, mob user = null)
    {
        await base.on_flash(target, user);
        if (user == target)
            to_chat(user, span_notice(I18n.T("You take a selfie!")));
        else
        {
            to_chat(user, span_notice(I18n.T("You take a photo with []!", target)));
            to_chat(target, span_notice(I18n.T("[] takes a photo with you!", user)));
        }
        to_chat(target, span_boldnotice(I18n.T("You'll remember this moment forever!")));

        await target.AddComponent(typeof(datum_component_dejavu), 2);
    }
}

//Timefreeze camera - Old Burning Sepia result. Kept in case admins want to spawn it
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:61
public partial class obj_item_camera_timefreeze : obj_item_camera
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:66
    public override async DmTask on_flash(atom target = null, mob user = null)
    {
        await base.on_flash(target, user);
        Dm.@new<obj_effect_timestop>(get_turf(target), 2, 50, Dm.list(user));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:17
public partial class datum_saved_bodypart : datum
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:22
    [DmParams("part")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_bodypart part = Dm.arg(__extra, 0) as obj_item_bodypart;
        old_part = part;
        bodypart_type = part.type;
        brute_dam = part.brute_dam;
        burn_dam = part.burn_dam;
    }
}

public partial class mob_living_carbon
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:28
    public virtual async DmTask apply_saved_bodyparts(DmList parts = null)
    {
        DmList dont_chop = Dm.list();
        foreach (object zone in Dm.each(parts))
        {
            datum_saved_bodypart saved_part = parts[zone] as datum_saved_bodypart;
            obj_item_bodypart already = get_bodypart(zone as string) as obj_item_bodypart;
            if (QDELETED(saved_part.old_part))
                saved_part.old_part = Dm.@new(saved_part.bodypart_type as System.Type) as obj_item_bodypart;
            if (already == null || already != saved_part.old_part)
                await saved_part.old_part.replace_limb(this);
            await saved_part.old_part.heal_damage(INFINITY, INFINITY, null, FALSE);
            await saved_part.old_part.receive_damage(Dm.num(saved_part.brute_dam), Dm.num(saved_part.burn_dam), wound_bonus: CANT_WOUND);
            dont_chop[zone] = TRUE;
        }
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:41
    public virtual object save_bodyparts()
    {
        DmList ret = Dm.list();
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            datum_saved_bodypart saved_part = Dm.@new<datum_saved_bodypart>(part);
            ret[part.body_zone] = saved_part;
        }
        return ret;
    }
}

//Barrier cube - Chilling Grey
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:85
public partial class obj_item_barriercube : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:91
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.locate<obj_structure_barricade_slime>(get_turf(loc)) != null)
        {
            to_chat(user, span_warning(I18n.T("You can't fit more than one barrier in the same space!")));
            return null;
        }
        to_chat(user, span_notice(I18n.T("You squeeze [].", this)));
        obj B = Dm.@new<obj_structure_barricade_slime>(get_turf(loc));
        B.visible_message(span_warning(I18n.T("[] suddenly grows into a large, gelatinous barrier!", this)));
        qdel(this);
        return null;
    }
}

//Ice stasis block - Chilling Dark Blue
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:127
public partial class obj_structure_ice_stasis : obj_structure
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        playsound(this, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, TRUE);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:147
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable M in Dm.each<atom_movable>(contents))
            Waiting.NoSleep(M.forceMove(loc));
        playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 50, TRUE);
        return base.Destroy(force);
    }
}

//Gold capture device - Chilling Gold
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:155
public partial class obj_item_capturedevice : obj_item
{
    ///traits we give and remove from the mob on exit and entry
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:161  var/static/list/traits_on_transfer (rules 10.6)
    public DmList traits_on_transfer => TgState.Current.obj_item_capturedevice__traits_on_transfer ??= Dm.list(
        TRAIT_IMMOBILIZED,
        TRAIT_HANDS_BLOCKED,
        TRAIT_AI_PAUSED
    );

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:167
    public override async DmTask<object> attack__proc(mob_living pokemon = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.length(contents)))
        {
            to_chat(user, span_warning(I18n.T("The device already has something inside.")));
            return null;
        }
        if (!isanimal_or_basicmob(pokemon))
        {
            to_chat(user, span_warning(I18n.T("The capture device only works on simple creatures.")));
            return null;
        }
        if (pokemon.mind != null)
        {
            to_chat(user, span_notice(I18n.T("You offer the device to [].", pokemon)));
            if (Dm.eq(await tgui_alert(pokemon, I18n.T("Would you like to enter []'s capture device?", user), I18n.T("Gold Capture Device"), Dm.list("Yes", "No")), "Yes"))
            {
                if (Dm.truthy(await user.can_perform_action(this)) && Dm.truthy(await user.can_perform_action(pokemon)))
                {
                    to_chat(user, span_notice(I18n.T("You store [] in the capture device.", pokemon)));
                    to_chat(pokemon, span_notice(I18n.T("The world warps around you, and you're suddenly in an endless void, with a window to the outside floating in front of you.")));
                    await store(pokemon);
                }
                else
                {
                    to_chat(user, span_warning(I18n.T("You were too far away from [].", pokemon)));
                    to_chat(pokemon, span_warning(I18n.T("You were too far away from [].", user)));
                }
            }
            else
            {
                to_chat(user, span_warning(I18n.T("[] refused to enter the device.", pokemon)));
                return null;
            }
        }
        else if (!Dm.truthy(pokemon.has_faction(FACTION_NEUTRAL)))
        {
            to_chat(user, span_warning(I18n.T("This creature is too aggressive to capture.")));
            return null;
        }
        to_chat(user, span_notice(I18n.T("You store [] in the capture device.", pokemon)));
        await store(pokemon);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:193
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (contents.len > 0)
        {
            to_chat(user, span_notice(I18n.T("You open the capture device!")));
            await release();
        }
        else
            to_chat(user, span_warning(I18n.T("The device is empty...")));
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:200
    public virtual async DmTask store(mob_living pokemon = null)
    {
        await pokemon.forceMove(this);
        pokemon.add_traits(traits_on_transfer, ABSTRACT_ITEM_TRAIT);
        await pokemon.cancel_camera();
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:205
    public virtual async DmTask release()
    {
        foreach (mob_living pokemon in Dm.each<mob_living>(contents))
        {
            await pokemon.forceMove(get_turf(loc));
            pokemon.remove_traits(traits_on_transfer, ABSTRACT_ITEM_TRAIT);
            await pokemon.cancel_camera();
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:161  the capture device's var/static/list/traits_on_transfer
    internal DmList obj_item_capturedevice__traits_on_transfer;
}
