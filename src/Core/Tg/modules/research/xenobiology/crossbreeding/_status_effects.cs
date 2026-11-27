// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_status_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbreeds' status effects (the rainbow protection, the adamantine slimeskin, the bluespace recall, the
// frozen stasis, the slime clone and its decay, the blood chill and the bone chill, the rebreathing), the consuming extracts'
// cookie effects and every stabilized extract's effect, with the burning fingertips. Kept as tg has them: the stabilized cerulean
// effect also tells its owner that no body was found after it moved them into the clone; the stabilized gold familiar's speech is
// copied from its owner as the master language holder; the stabilized black effect's tick returns before its base unless it is
// draining someone, so it never fades when the extract is dropped. The var blocks are generated (D111), but the recall's overlay
// and the gold familiar, which tg types as an icon and a simple animal and fills with an image and any pet.

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:7
public partial class datum_status_effect_rainbow_protection : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:13
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        owner.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_PACIFISM), TRAIT_STATUS_EFFECT(id));
        owner.visible_message(span_warning(I18n.T("[] shines with a brilliant rainbow light.", owner)),
            span_notice(I18n.T("You feel protected by an unknown force!")));
        // okay, now time for the rainbow animation.
        owner.add_filter(Dm.text("rainbow_protection_[]", REF(this)), 2, color_matrix_filter(Dm.list(0, 0, 0, 0, 0.75f, 0, 0, 0, 1, 0, 0.25f, 0), COLORSPACE_HSL) as DmList);
        object color_filter = owner.get_filter(Dm.text("rainbow_protection_[]", REF(this)));
        Dm.animate(color_filter, Dm.named("color", Dm.list(0, 0, 0, 0, 0.75f, 0, 0, 0, 1, 0.33f, 0.25f, 0)), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("color", Dm.list(0, 0, 0, 0, 0.75f, 0, 0, 0, 1, 0.66f, 0.25f, 0)), Dm.named("time", 1 * SECONDS));
        Dm.animate(Dm.named("color", Dm.list(0, 0, 0, 0, 0.75f, 0, 0, 0, 1, 1, 0.25f, 0)), Dm.named("time", 1 * SECONDS));
        Dm.animate(Dm.named("color", Dm.list(0, 0, 0, 0, 0.75f, 0, 0, 0, 1, 0, 0.25f, 0)), Dm.named("time", 0)); // IMPORTANT!
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:26
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_filter(Dm.text("rainbow_protection_[]", REF(this)));
        owner.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_PACIFISM), TRAIT_STATUS_EFFECT(id));
        owner.visible_message(span_notice(I18n.T("[] stops glowing, the rainbow light fading away.", owner)),
            span_warning(I18n.T("You no longer feel protected...")));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:38
public partial class datum_status_effect_slimeskin : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:43
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_atom_colour(color_transition_filter("#3070CC", SATURATION_OVERRIDE), TEMPORARY_COLOUR_PRIORITY);
        owner.damage_resistance += 10;
        owner.visible_message(span_warning(I18n.T("[] is suddenly covered in a strange, blue-ish gel!", owner)),
            span_notice(I18n.T("You are covered in a thick, rubbery gel.")));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:50
    public override async DmTask<object> on_remove()
    {
        await owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        owner.damage_resistance -= 10;
        owner.visible_message(span_warning(I18n.T("[]'s gel coating liquefies and dissolves away.", owner)),
            span_notice(I18n.T("Your gel second-skin dissolves!")));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:57
public partial class datum_status_effect_slimerecall : datum_status_effect
{
    // tg declares it an icon and stores the chronofield overlay's icon in it; the generator would type it a typepath
    public object bluespace; // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:63

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:57
    public datum_status_effect_slimerecall()
    {
        id = "slime_recall";
        duration = -1;
        tick_interval = -1;
        alert_type = null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:65
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_RESIST, nameof(resistField));
        to_chat(owner, span_danger(I18n.T("You feel a sudden tug from an unknown force, and feel a pull to bluespace!")));
        to_chat(owner, span_notice(I18n.T("Resist if you wish avoid the force!")));
        bluespace = Dm.icon(Dm.resource("icons/effects/effects.dmi"), "chronofield");
        owner.add_overlay(bluespace);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:73
    [SignalHandler]
    public virtual void resistField()
    {
        interrupted = TRUE;
        owner.remove_status_effect(this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:78
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_RESIST);
        owner.cut_overlay(bluespace);
        if (Dm.truthy(interrupted) || !ismob(target))
        {
            to_chat(owner, span_warning(I18n.T("The bluespace tug fades away, and you feel that the force has passed you by.")));
            return null;
        }
        turf old_location = get_turf(owner);
        if (Dm.truthy(await do_teleport(owner, target.loc, channel: TELEPORT_CHANNEL_QUANTUM))) //despite being named a bluespace teleportation method the quantum channel is used to preserve precision teleporting with a bag of holding
        {
            old_location.visible_message(span_warning(I18n.T("[] disappears in a flurry of sparks!", owner)));
            to_chat(owner, span_warning(I18n.T("The unknown force snatches briefly you from reality, and deposits you next to []!", target)));
        }
        return null;
    }
}


// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:93
public partial class datum_status_effect_frozenstasis : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:103
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        cube = Dm.@new<obj_structure_ice_stasis>(get_turf(owner));
        await owner.forceMove(cube);
        RegisterSignal(cube, COMSIG_QDELETING, nameof(clear_effect));
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(has_escaped));
        if (Dm.truthy(resistable))
            RegisterSignal(owner, COMSIG_LIVING_RESIST, nameof(clear_effect));
        ADD_TRAIT(owner, TRAIT_GODMODE, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:113
    [SignalHandler]
    public virtual void clear_effect(params object[] __extra)
    {
        qdel(this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:118
    [SignalHandler]
    public virtual void has_escaped(params object[] __extra)
    {
        if (owner.loc != cube)
            qdel(this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:124
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_GODMODE, TRAIT_STATUS_EFFECT(id));
        UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(owner, COMSIG_LIVING_RESIST);
        UnregisterSignal(cube, COMSIG_QDELETING);
        QDEL_NULL(ref cube);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:135
public partial class datum_status_effect_slime_clone : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:142
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        Type typepath = (Type)owner.type;
        clone = Dm.@new(typepath, owner.drop_location()) as mob_living;
        if (iscarbon(owner) && iscarbon(clone))
        {
            mob_living_carbon carbon_owner = (mob_living_carbon)owner;
            mob_living_carbon carbon_clone = (mob_living_carbon)clone;
            carbon_clone.real_name = carbon_owner.real_name;
            await carbon_owner.dna.copy_dna(carbon_clone.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
            await carbon_clone.updateappearance(mutcolor_update: TRUE);
        }
        if (owner.mind != null)
        {
            originalmind = owner.mind;
            await owner.mind.transfer_to(clone);
        }
        await clone.apply_status_effect(typeof(datum_status_effect_slime_clone_decay));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:157
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await DmTask.CompletedTask;
        if (!(clone is mob_living) || IS_UNCONSCIOUS_OR_CRIT(clone))
            owner.remove_status_effect(this);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:161
    public override async DmTask<object> on_remove()
    {
        if (clone?.mind != null && owner != null)
            await clone.mind.transfer_to(owner);
        else
        {
            if (owner != null && originalmind != null)
            {
                await originalmind.transfer_to(owner);
                if (Dm.truthy(originalmind.key))
                    owner.ckey = Dm.str(originalmind.key);
            }
        }
        if (clone != null)
        {
            await clone.unequip_everything();
            QDEL_NULL(ref clone);
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:179
public partial class datum_status_effect_slime_clone_decay : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:184
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_atom_colour("#007BA7", FIXED_COLOUR_PRIORITY);
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:188
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float need_mob_update;
        need_mob_update = Dm.num(await owner.adjust_tox_loss(1, updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_oxy_loss(1, updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_brute_loss(1, updating_health: FALSE));
        need_mob_update += Dm.num(await owner.adjust_fire_loss(1, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:204
public partial class datum_status_effect_bloodchill : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:208
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_bloodchill));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:212
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (SPT_PROB(50, seconds_between_ticks))
            await owner.adjust_fire_loss(2);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:216
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_bloodchill));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:220
public partial class datum_status_effect_bonechill : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:224
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_bonechill));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:228
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!SPT_PROB(50, seconds_between_ticks))
            return null;
        await owner.adjust_fire_loss(1);
        await owner.set_jitter_if_lower(6 * SECONDS);
        owner.adjust_bodytemperature(-10);
        if (ishuman(owner))
        {
            mob_living_carbon_human humi = (mob_living_carbon_human)owner;
            humi.adjust_coretemperature(-10);
        }
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:238
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_bonechill));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:248
public partial class datum_status_effect_rebreathing : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:252
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await owner.adjust_oxy_loss(-6 * Dm.num(seconds_between_ticks)); //Just a bit more than normal breathing.
        return null;
    }
}

///////////////////////////////////////////////////////
//////////////////CONSUMING EXTRACTS///////////////////
///////////////////////////////////////////////////////

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:260
public partial class datum_status_effect_firecookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:266
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_RESISTCOLD, TRAIT_STATUS_EFFECT(id));
        owner.adjust_bodytemperature(110);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:271
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_RESISTCOLD, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:275
public partial class datum_status_effect_watercookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:280
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:284
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        foreach (turf_open T in Dm.each<turf_open>(range(get_turf(owner), 1)))
            await T.MakeSlippery(TURF_WET_WATER, min_wet_time: 10, wet_time_to_add: 5);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:288
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:292
public partial class datum_status_effect_metalcookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:298
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        MODIFY_PHYSIOLOGY(owner, BRUTE, 0.9f);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:302
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        MODIFY_PHYSIOLOGY(owner, BRUTE, 1 / 0.9f);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:306
public partial class datum_status_effect_sparkcookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:312
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_SHOCKIMMUNE, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:316
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_SHOCKIMMUNE, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:320
public partial class datum_status_effect_toxincookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:326
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_TOXINLOVER, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:330
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_TOXINLOVER, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:334
public partial class datum_status_effect_timecookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:340
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_timecookie));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:344
    public override async DmTask<object> on_remove()
    {
        owner.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_timecookie));
        return await base.on_remove();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:349
public partial class datum_status_effect_lovecookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:354
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await DmTask.CompletedTask;
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return null;
        if (iscarbon(owner))
        {
            mob_living_carbon C = (mob_living_carbon)owner;
            if (C.handcuffed != null)
                return null;
        }
        DmList huggables = Dm.list();
        foreach (mob_living_carbon L in Dm.each<mob_living_carbon>(range(get_turf(owner), 1)))
        {
            if (L != owner)
                Dm.append(ref huggables, L);
        }
        if (Dm.truthy(Dm.length(huggables)))
        {
            mob_living_carbon hugged = pick(huggables) as mob_living_carbon;
            owner.visible_message(span_notice(I18n.T("[] hugs []!", owner, hugged)), span_notice(I18n.T("You hug []!", hugged)));
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:370
public partial class datum_status_effect_tarcookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:375
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        foreach (mob_living_carbon_human L in Dm.each<mob_living_carbon_human>(range(get_turf(owner), 1)))
        {
            if (L != owner)
                await L.apply_status_effect(typeof(datum_status_effect_tarfoot));
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:381
public partial class datum_status_effect_tarfoot : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:386
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_tarfoot));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:390
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_tarfoot));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:394
public partial class datum_status_effect_spookcookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:399
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        image skeleton_image = image(icon: Dm.resource("icons/mob/human/human.dmi"), icon_state: "skeleton", layer: ABOVE_MOB_LAYER, loc: owner);
        skeleton_image.@override = TRUE;
        owner.add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_everyone), "spookyscary", skeleton_image);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:405
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_alt_appearance("spookyscary");
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:409
public partial class datum_status_effect_peacecookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:414
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        foreach (mob_living L in Dm.each<mob_living>(range(get_turf(owner), 1)))
            await L.apply_status_effect(typeof(datum_status_effect_plur));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:419
public partial class datum_status_effect_plur : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:425
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:429
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:433
public partial class datum_status_effect_adamantinecookie : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:439
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        MODIFY_PHYSIOLOGY(owner, BURN, 0.9f);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:443
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        MODIFY_PHYSIOLOGY(owner, BURN, 1 / 0.9f);
        return null;
    }
}

///////////////////////////////////////////////////////
//////////////////STABILIZED EXTRACTS//////////////////
///////////////////////////////////////////////////////

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:451
public partial class datum_status_effect_stabilized : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:459
    [DmParams("new_owner", "linked_extract")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        this.linked_extract = Dm.arg(__extra, 0) as obj_item_slimecross_stabilized;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:463
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await DmTask.CompletedTask;
        if (linked_extract == null)
        {
            qdel(this);
            return null;
        }
        if (linked_extract.get_held_mob() == owner)
            return null;
        owner.balloon_alert(owner, I18n.T("[] extract faded!", colour));
        if (!QDELETED(linked_extract))
        {
            linked_extract.linked_effect = null;
            START_PROCESSING(SSobj, linked_extract);
        }
        qdel(this);
        return null;
    }
}

//Stabilized effects start below.
// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:481
public partial class datum_status_effect_stabilized_grey : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:484
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        foreach (mob_living_basic_slime slimes_in_range in Dm.each<mob_living_basic_slime>(range(1, get_turf(owner))))
        {
            if (!Dm.truthy(slimes_in_range.has_ally(owner)))
            {
                to_chat(owner, span_notice(I18n.T("[] pulses gently as it communicates with [].", linked_extract, slimes_in_range)));
                await slimes_in_range.befriend(owner);
            }
        }
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:492
public partial class datum_status_effect_stabilized_orange : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:495
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float body_temp_target = Dm.num(owner.get_body_temp_normal(apply_change: FALSE));

        float body_temp_actual = Dm.num(owner.bodytemperature);
        float body_temp_offset = body_temp_target - body_temp_actual;
        body_temp_offset = clamp(body_temp_offset, -5, 5);
        owner.adjust_bodytemperature(body_temp_offset);

        if (ishuman(owner))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)owner;
            float core_temp_actual = Dm.num(human.coretemperature);
            float core_temp_offset = body_temp_target - core_temp_actual;
            core_temp_offset = clamp(core_temp_offset, -5, 5);
            human.adjust_coretemperature(core_temp_offset);
        }

        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:513
public partial class datum_status_effect_stabilized_purple : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:518
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        healed_last_tick = FALSE;
        float need_mob_update = FALSE;

        if (Dm.num(owner.get_brute_loss()) > 0)
        {
            need_mob_update += Dm.num(await owner.adjust_brute_loss(-0.2f, updating_health: FALSE));
            healed_last_tick = TRUE;
        }

        if (Dm.num(owner.get_fire_loss()) > 0)
        {
            need_mob_update += Dm.num(await owner.adjust_fire_loss(-0.2f, updating_health: FALSE));
            healed_last_tick = TRUE;
        }

        if (Dm.num(owner.get_tox_loss()) > 0)
        {
            // Forced, so slimepeople are healed as well.
            need_mob_update += Dm.num(await owner.adjust_tox_loss(-0.2f, updating_health: FALSE, forced: TRUE));
            healed_last_tick = TRUE;
        }

        if (Dm.truthy(need_mob_update))
            await owner.updatehealth();

        // Technically, "healed this tick" by now.
        if (Dm.truthy(healed_last_tick))
            Dm.@new<obj_effect_temp_visual_heal>(get_turf(owner), COLOR_RED);

        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:544
    public override object get_examine_text(mob examiner = null)
    {
        if (Dm.truthy(healed_last_tick))
            return span_warning(I18n.T("[] [] regenerating slowly, purplish goo filling in small injuries!", owner.p_They(), owner.p_are()));

        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:551
public partial class datum_status_effect_stabilized_blue : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:554
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:558
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:562
public partial class datum_status_effect_stabilized_metal : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:567
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (cooldown > 0)
            cooldown--;
        else
        {
            cooldown = max_cooldown;
            DmList sheets = Dm.list();
            foreach (obj_item_stack_sheet S in Dm.each<obj_item_stack_sheet>(owner.get_all_contents()))
            {
                if (Dm.num(S.amount) < S.max_amount)
                    Dm.append(ref sheets, S);
            }

            if (sheets.len > 0)
            {
                obj_item_stack_sheet S = pick(sheets) as obj_item_stack_sheet;
                await S.add(1);
                to_chat(owner, span_notice(I18n.T("[] adds a layer of slime to [], which metamorphosizes into another sheet of material!", linked_extract, S)));
            }
        }
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:585
public partial class datum_status_effect_stabilized_yellow : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:590
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("Nearby electronics seem just a little more charged wherever [] go[].", owner.p_they(), owner.p_es()));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:593
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (cooldown > 0)
        {
            cooldown--;
            return await base.tick(seconds_between_ticks);
        }
        cooldown = max_cooldown;
        DmList batteries = Dm.list();
        foreach (obj_item_stock_parts_power_store C in Dm.each<obj_item_stock_parts_power_store>(assoc_to_values(owner.get_all_cells() as DmList)))
        {
            if (Dm.num(C.charge) < Dm.num(C.maxcharge))
                Dm.append(ref batteries, C);
        }
        if (batteries.len > 0)
        {
            obj_item_stock_parts_power_store ToCharge = pick(batteries) as obj_item_stock_parts_power_store;
            ToCharge.charge = Dm.num(ToCharge.charge) + Math.Min(Dm.num(ToCharge.maxcharge) - Dm.num(ToCharge.charge), Dm.num(ToCharge.maxcharge) / 10); //10% of the cell, or to maximum.
        }
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:608
public partial class obj_item_hothands : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:611
    public override object get_temperature()
    {
        return 290; //Below what's required to ignite plasma.
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:615
public partial class datum_status_effect_stabilized_darkpurple : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:619
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_RESISTHEATHANDS, TRAIT_STATUS_EFFECT(id));
        fire = Dm.@new<obj_item_hothands>(owner);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:624
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        obj_item item = owner.get_active_held_item() as obj_item;
        if (item != null)
        {
            if (IS_EDIBLE(item) && Dm.truthy(Dm.bitand(item.microwave_act(microwaver: owner), COMPONENT_MICROWAVE_SUCCESS)))
                to_chat(owner, span_warning(I18n.T("[] flares up brightly, and your hands alone are enough cook []!", linked_extract, item)));
            else
                await item.attackby(fire, owner);
        }
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:633
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_RESISTHEATHANDS, TRAIT_STATUS_EFFECT(id));
        QDEL_NULL(ref fire);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:637
    public override object get_examine_text(mob examiner = null)
    {
        return span_notice(I18n.T("[] fingertips burn brightly!", owner.p_Their()));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:641
public partial class datum_status_effect_stabilized_darkblue : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:644
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.num(owner.fire_stacks) > 0 && prob(80))
        {
            await owner.adjust_wet_stacks(1);
            if (Dm.num(owner.fire_stacks) <= 0)
                to_chat(owner, span_notice(I18n.T("[] coats you in a watery goo, extinguishing the flames.", linked_extract)));
        }
        obj O = owner.get_active_held_item() as obj;
        if (O != null)
        {
            await O.extinguish(); //All shamelessly copied from water's expose_obj, since I didn't seem to be able to get it here for some reason.
            await O.wash(CLEAN_TYPE_ACID);
        }
        // Monkey cube
        if (O is obj_item_food_monkeycube)
        {
            to_chat(owner, span_warning(I18n.T("[] kept your hands wet! It makes [] expand!", linked_extract, O)));
            obj_item_food_monkeycube cube = (obj_item_food_monkeycube)O;
            await cube.Expand();
        }

        // Dehydrated carp
        else if (O is obj_item_toy_plush_carpplushie_dehy_carp)
        {
            to_chat(owner, span_warning(I18n.T("[] kept your hands wet! It makes [] expand!", linked_extract, O)));
            obj_item_toy_plush_carpplushie_dehy_carp dehy = (obj_item_toy_plush_carpplushie_dehy_carp)O;
            dehy.Swell(); // Makes a carp
        }

        else if (O is obj_item_stack_sheet_hairlesshide)
        {
            to_chat(owner, span_warning(I18n.T("[] kept your hands wet! It wets []!", linked_extract, O)));
            obj_item_stack_sheet_hairlesshide HH = (obj_item_stack_sheet_hairlesshide)O;
            Dm.@new<obj_item_stack_sheet_wethide>(get_turf(HH), HH.amount);
            qdel(HH);
        }
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:673
public partial class datum_status_effect_stabilized_silver : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:676
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HUNGER_MOD, 0.8f);
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:680
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HUNGER_MOD, 1.25f);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:696
public partial class datum_status_effect_stabilized_bluespace : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:701
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_bluespacestabilization))))
        {
            linked_alert.desc = "The stabilized bluespace extract is still aligning you with the bluespace axis.";
            linked_alert.icon_state = "slime_bluespace_off";
            return await base.tick(seconds_between_ticks);
        }
        else
        {
            linked_alert.desc = "The stabilized bluespace extract will try to redirect you from harm!";
            linked_alert.icon_state = "slime_bluespace_on";
        }

        if (Dm.truthy(healthcheck) && (Dm.num(healthcheck) - Dm.num(owner.health)) > 5)
        {
            owner.visible_message(span_warning(I18n.T("[] notices the sudden change in []'s physical health, and activates!", linked_extract, owner)));
            do_sparks(5, FALSE, owner);
            turf emergency_turf = find_safe_turf(owner.z, extended_safety_checks: TRUE) as turf;
            float range = 0;
            if (emergency_turf == null)
            {
                emergency_turf = get_turf(owner);
                range = 50;
            }
            if (Dm.truthy(await do_teleport(owner, emergency_turf, range, channel: TELEPORT_CHANNEL_BLUESPACE)))
            {
                to_chat(owner, span_notice(I18n.T("[] will take some time to re-align you on the bluespace axis.", linked_extract)));
                do_sparks(5, FALSE, owner);
                await owner.apply_status_effect(typeof(datum_status_effect_bluespacestabilization));
            }
        }
        healthcheck = owner.health;
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:726
public partial class datum_status_effect_stabilized_sepia : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:730
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (prob(50) && mod > -1)
        {
            mod--;
            await owner.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_sepia), multiplicative_slowdown: -0.5f);
        }
        else if (mod < 1)
        {
            mod++;
            // yeah a value of 0 does nothing but replacing the trait in place is cheaper than removing and adding repeatedly
            await owner.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_sepia), multiplicative_slowdown: 0);
        }
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:740
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_sepia));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:744
public partial class datum_status_effect_stabilized_cerulean : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:748
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        Type typepath = (Type)owner.type;
        clone = Dm.@new(typepath, owner.drop_location()) as mob_living;
        if (iscarbon(owner) && iscarbon(clone))
        {
            mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
            mob_living_carbon_human human_clone = clone as mob_living_carbon_human;
            human_clone.physique = human_owner.physique;
            human_clone.real_name = human_owner.real_name;
            human_clone.age = human_owner.age;
            human_clone.voice = human_owner.voice;
            human_clone.voice_filter = human_owner.voice_filter;
            foreach (datum_quirk original_quirks in Dm.each_cast<datum_quirk>(human_owner.quirks))
                await human_clone.add_quirk((Type)original_quirks.type, add_unique: FALSE, announce: FALSE);
            await human_owner.dna.copy_dna(human_clone.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
            await human_clone.updateappearance(mutcolor_update: TRUE);
        }
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:765
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.stat == DEAD)
        {
            if (clone != null && clone.stat != DEAD)
            {
                owner.visible_message(span_warning(I18n.T("[] blazes with brilliant light, [] whisking [] soul away.", owner, linked_extract, owner.p_their())),
                    span_notice(I18n.T("You feel a warm glow from [], and you open your eyes... elsewhere.", linked_extract)));
                if (owner.mind != null)
                    await owner.mind.transfer_to(clone);
                clone = null;
                qdel(linked_extract);
            }
            if (clone == null || clone.stat == DEAD)
            {
                to_chat(owner, span_notice(I18n.T("[] desperately tries to move your soul to a living body, but can't find one!", linked_extract)));
                qdel(linked_extract);
            }
        }
        await base.tick(seconds_between_ticks);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:779
    public override async DmTask<object> on_remove()
    {
        if (clone != null)
        {
            clone.visible_message(span_warning(I18n.T("[] dissolves into a puddle of goo!", clone)));
            await clone.unequip_everything();
            qdel(clone);
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:786
public partial class datum_status_effect_stabilized_pyrite : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:789
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        string new_color = rgb(rand(0, 360), 100, 50, space: COLORSPACE_HSL);
        await owner.add_atom_colour(color_transition_filter(new_color, SATURATION_OVERRIDE), TEMPORARY_COLOUR_PRIORITY);
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:794
    public override async DmTask<object> on_remove()
    {
        await owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:798
public partial class datum_status_effect_stabilized_red : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:801
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        owner.add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_equipment_speedmod));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:805
    public override async DmTask<object> on_remove()
    {
        owner.remove_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_equipment_speedmod));
        return await base.on_remove();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:810
public partial class datum_status_effect_stabilized_green : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:815
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        to_chat(owner, span_warning(I18n.T("You feel different...")));
        if (ishuman(owner))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)owner;
            originalDNA = Dm.@new((Type)H.dna.type) as datum_dna;
            originalname = H.real_name;
            await H.dna.copy_dna(originalDNA, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
            await randomize_human(H);
        }
        return await base.on_apply(__extra);
    }

    // Only occasionally give examiners a warning.
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:826
    public override object get_examine_text(mob examiner = null)
    {
        if (prob(50))
            return span_warning(I18n.T("[] look[] a bit green and gooey...", owner.p_They(), owner.p_s()));

        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:832
    public override async DmTask<object> on_remove()
    {
        to_chat(owner, span_notice(I18n.T("You feel more like yourself.")));
        if (ishuman(owner))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)owner;
            await originalDNA.copy_dna(human.dna, Dm.bitor(Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES), COPY_DNA_MUTATIONS));
            human.real_name = Dm.str(originalname);
            await human.updateappearance(mutcolor_update: 1);
        }
        originalDNA = null;
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:847
public partial class datum_status_effect_pinkdamagetracker : datum_status_effect
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:853
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await DmTask.CompletedTask;
        if (Dm.num(Dm.sub(lasthealth, owner.health)) > 0)
            damage += Dm.num(Dm.sub(lasthealth, owner.health));
        lasthealth = owner.health;
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:859
public partial class datum_status_effect_stabilized_pink : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:866
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        faction_name = Dm.text("pink_[]", owner); // FACTION_PINK_EXTRACT(owner)
        owner.add_ally(faction_name);
        to_chat(owner, span_notice(I18n.T("[] pulses, generating a fragile aura of peace.", linked_extract)));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:872
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        await update_nearby_mobs();
        object has_ally = owner.has_ally(faction_name);
        if (Dm.truthy(has_ally))
        {
            if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_brokenpeace))))
            {
                owner.remove_ally(faction_name);
                to_chat(owner, span_userdanger(I18n.T("The peace has been broken! Hostile creatures will now react to you!")));
            }
        }
        else if (!Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_brokenpeace))))
        {
            to_chat(owner, span_notice(I18n.T("[] pulses, generating a fragile aura of peace.", linked_extract)));
            owner.add_ally(faction_name);
        }
        return await base.tick(seconds_between_ticks);
    }

    /// Pacifies mobs you can see and unpacifies mobs you no longer can
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:885
    public virtual async DmTask update_nearby_mobs()
    {
        DmList visible_things = view(7, get_turf(owner));
        // Unpacify far away or offended mobs
        foreach (datum_weakref weak_mob in Dm.each_cast<datum_weakref>(mobs))
        {
            mob_living beast = weak_mob.resolve() as mob_living;
            if (beast == null)
            {
                Dm.remove(ref mobs, weak_mob);
                continue;
            }
            datum_status_effect_pinkdamagetracker damage_tracker = beast.has_status_effect(typeof(datum_status_effect_pinkdamagetracker)) as datum_status_effect_pinkdamagetracker;
            if (damage_tracker is datum_status_effect_pinkdamagetracker && damage_tracker.damage > 0)
            {
                damage_tracker.damage = 0;
                await owner.apply_status_effect(typeof(datum_status_effect_brokenpeace));
                return; // No point continuing from here if we're going to end the effect
            }
            if (Dm.@in(beast, visible_things))
                continue;
            beast.remove_ally(faction_name);
            beast.remove_status_effect(typeof(datum_status_effect_pinkdamagetracker));
            Dm.remove(ref mobs, weak_mob);
        }

        // Pacify nearby mobs
        foreach (mob_living beast in Dm.each<mob_living>(visible_things))
        {
            if (!isanimal_or_basicmob(beast))
                continue;
            datum_weakref weak_mob = WEAKREF(beast) as datum_weakref;
            if (Dm.@in(weak_mob, mobs))
                continue;
            Dm.append(ref mobs, weak_mob);
            await beast.apply_status_effect(typeof(datum_status_effect_pinkdamagetracker));
            beast.add_faction(faction_name);
        }
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:915
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        foreach (datum_weakref weak_mob in Dm.each_cast<datum_weakref>(mobs))
        {
            mob_living beast = weak_mob.resolve() as mob_living;
            if (beast == null)
                continue;
            beast.remove_faction(faction_name);
            beast.remove_status_effect(typeof(datum_status_effect_pinkdamagetracker));
        }
        owner.remove_faction(faction_name);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:925
public partial class datum_status_effect_stabilized_oil : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:928
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.stat == DEAD)
        {
            await explosion(owner, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 4, flame_range: 5, explosion_cause: this);
            qdel(linked_extract);
        }
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:934
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("[] smell[] of sulfur and oil!", owner.p_They(), owner.p_s()));
    }
}

/// How much damage is dealt per healing done for the stabilized back.
/// This multiplier is applied to prevent two people from converting each other's damage away.
// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:942
public partial class datum_status_effect_stabilized_black : datum_status_effect_stabilized
{
    private const float DRAIN_DAMAGE_MULTIPLIER = 1.2f;

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:949
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOVABLE_SET_GRAB_STATE, nameof(on_grab));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:953
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOVABLE_SET_GRAB_STATE);
        return await base.on_remove();
    }

    /// Whenever we grab someone by the neck, set "draining" to a weakref of them.
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:958
    [SignalHandler]
    public virtual void on_grab(mob_living source = null, object new_state = null)
    {
        if (Dm.num(new_state) < GRAB_KILL || !isliving(source.pulling))
        {
            draining_ref = null;
            return;
        }

        mob_living draining = (mob_living)source.pulling;
        if (draining.stat == DEAD)
            return;

        draining_ref = WEAKREF(draining) as datum_weakref;
        to_chat(owner, span_boldnotice(I18n.T("You feel your hands melt around []'s neck as you start to drain [] of [] life!", draining, draining.p_them(), draining.p_their())));
        to_chat(draining, span_userdanger(I18n.T("[]'s hands melt around your neck as you can feel your life starting to drain away!", owner)));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:973
    public override object get_examine_text(mob examiner = null)
    {
        mob_living draining = draining_ref?.resolve() as mob_living;
        if (draining == null)
            return null;

        return span_warning(I18n.T("[] [] draining health from []!", owner.p_They(), owner.p_are(), draining));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:980
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.grab_state < GRAB_KILL || !IS_WEAKREF_OF(owner.pulling, draining_ref))
            return null;

        mob_living drained = draining_ref.resolve() as mob_living;
        if (drained.stat == DEAD)
        {
            to_chat(owner, span_warning(I18n.T("[] is dead, you cannot drain anymore life from them!", drained)));
            draining_ref = null;
            return null;
        }

        DmList healing_types = Dm.list();
        if (Dm.num(owner.get_brute_loss()) > 0)
            Dm.append(ref healing_types, BRUTE);
        if (Dm.num(owner.get_fire_loss()) > 0)
            Dm.append(ref healing_types, BURN);
        if (Dm.num(owner.get_tox_loss()) > 0)
            Dm.append(ref healing_types, TOX);

        if (Dm.truthy(Dm.length(healing_types)))
            await owner.heal_damage_type(heal_amount, damagetype: Dm.str(pick(healing_types)));

        await owner.adjust_nutrition(3);
        await drained.apply_damage(heal_amount * DRAIN_DAMAGE_MULTIPLIER, damagetype: BRUTE, spread_damage: TRUE);
        return await base.tick(seconds_between_ticks);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1008
public partial class datum_status_effect_stabilized_lightpink : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1011
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_lightpink));
        ADD_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1016
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        foreach (mob_living_carbon_human H in Dm.each<mob_living_carbon_human>(range(1, get_turf(owner))))
        {
            if (H != owner && H.stat != DEAD && Dm.num(H.health) <= 0 && !Dm.truthy(((datum_reagents)H.reagents).has_reagent(typeof(datum_reagent_medicine_epinephrine))))
            {
                to_chat(owner, I18n.T("[] pulses in sync with []'s heartbeat, trying to keep [] alive.", linked_extract, H, H.p_them()));
                await ((datum_reagents)H.reagents).add_reagent(typeof(datum_reagent_medicine_epinephrine), 5);
            }
        }
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1023
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_lightpink));
        REMOVE_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1028
public partial class datum_status_effect_stabilized_adamantine : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1031
    public override object get_examine_text(mob examiner = null)
    {
        return span_warning(I18n.T("[] [] strange metallic coating on [] skin.", owner.p_They(), owner.p_have(), owner.p_their()));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1063
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        owner.damage_resistance += 5;
        return await base.on_apply(__extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1067
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        owner.damage_resistance -= 5;
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1035
public partial class datum_status_effect_stabilized_gold : datum_status_effect_stabilized
{
    // tg declares a simple animal; the familiar may be any friendly pet, a basic mob as often
    public mob_living familiar; // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1047

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1035
    public datum_status_effect_stabilized_gold()
    {
        id = "stabilizedgold";
        colour = "gold";
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1039
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        obj_item_slimecross_stabilized_gold linked = linked_extract as obj_item_slimecross_stabilized_gold;
        if (QDELETED(familiar))
        {
            familiar = Dm.@new(linked.mob_type, get_turf(owner.loc)) as mob_living;
            familiar.name = linked.mob_name;
            if (isanimal(familiar))
                ((mob_living_simple_animal)familiar).del_on_death = TRUE;
            else //we are a basicmob otherwise
            {
                mob_living_basic basic_familiar = (mob_living_basic)familiar;
                basic_familiar.basic_mob_flags = Dm.bitor(basic_familiar.basic_mob_flags, DEL_ON_DEATH);
            }
            await familiar.befriend(owner);
            familiar.copy_languages(owner, LANGUAGE_MASTER);
            if (linked.saved_mind != null)
            {
                await linked.saved_mind.transfer_to(familiar);
                familiar.ckey = Dm.str(linked.saved_mind.key);
            }
        }
        else
        {
            if (familiar.mind != null)
                linked.saved_mind = familiar.mind;
        }
        return await base.tick(seconds_between_ticks);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1059
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        if (familiar != null)
            qdel(familiar);
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1071
public partial class datum_status_effect_stabilized_rainbow : datum_status_effect_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/_status_effects.dm:1074
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (Dm.num(owner.health) <= 0)
        {
            obj_item_slimecross_stabilized_rainbow X = linked_extract as obj_item_slimecross_stabilized_rainbow;
            if (X is obj_item_slimecross_stabilized_rainbow)
            {
                if (X.regencore != null)
                {
                    await X.regencore.interact_with_atom(owner, owner);
                    X.regencore = null;
                    owner.visible_message(span_warning(I18n.T("[] flashes a rainbow of colors, and [] skin is coated in a milky regenerative goo!", owner, owner.p_their())));
                    qdel(this);
                    qdel(linked_extract);
                }
            }
        }
        return await base.tick(seconds_between_ticks);
    }
}
