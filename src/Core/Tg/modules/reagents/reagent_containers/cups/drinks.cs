// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/drinks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: gaps J3 (D298: the bottle of nothing) the drink root's smashing; syndicate G3c (D316) the mug (the uplink's hot cocoa);
// gaps H3 (D333, the bar) the rest: the trophy cups, the coffee cups, the water bottles with their caps and flips, the paper cup, the juice
// carton, the colo cups and the shaker with its custom drinks. The cuffable element waits (restraints). The var blocks are generated.

////////////////////////////////////////////////////////////////////////////////
/// Drinks.
////////////////////////////////////////////////////////////////////////////////
// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:5
public partial class obj_item_reagent_containers_cup_glass : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:17
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(dot)) //if the bottle wasn't caught
            Waiting.NoSleep(smash(hit_atom, throwingdatum?.get_thrower() as atom, throwingdatum));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:22
    public virtual async DmTask<object> smash(atom target = null, atom thrower = null, datum_thrownthing throwingdatum = null, float? break_top = null)
    {
        break_top ??= FALSE;
        if (!Dm.truthy(isGlass))
            return FALSE;
        if (QDELING(this) || target == null) //Invalid loc
            return FALSE;
        if (ismob(thrower) && Dm.truthy(await bartender_check(target, thrower as mob)) && throwingdatum != null)
            return FALSE;
        atom splash_target = QDELETED(target) ? target.drop_location() as atom : target;
        mob splash_thrower = thrower as mob;
        await splash_reagents(splash_target, splash_thrower, allow_closed_splash: TRUE);
        obj_item_broken_bottle broken = Dm.@new<obj_item_broken_bottle>(loc);
        await broken.mimic_broken(this, target, break_top);
        await post_smash(target, thrower, throwingdatum, broken);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:38
    public virtual async DmTask post_smash(atom target = null, atom thrower = null, datum_thrownthing throwingdatum = null, obj_item_broken_bottle broken = null)
    {
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:41
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (QDELETED(this))
            return dot;
        if (hitting_projectile.damage > 0 && Dm.eq(hitting_projectile.damage_type, BRUTE))
        {
            atom T = get_turf(this);
            await smash(T);
        }
        return dot;
    }
}

// parent type is literally just so empty mug sprites are a thing
// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:172
public partial class obj_item_reagent_containers_cup_glass_mug : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:179
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/drinks.dm:182", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)");
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:183
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(((datum_reagents)reagents).total_volume) ? null : "_empty");
        return base.update_icon_state(__extra);
    }
}

// Gaps H3 (D333, the bar): the rest of the file.

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:66
public partial class obj_item_reagent_containers_cup_glass_trophy_gold_cup : obj_item_reagent_containers_cup_glass_trophy
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/drinks.dm:81", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)"); //closed handles
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:83
public partial class obj_item_reagent_containers_cup_glass_trophy_silver_cup : obj_item_reagent_containers_cup_glass_trophy
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/drinks.dm:97", "Initialize: AddElement(/datum/element/cuffable_item) (cuffing an item to a wrist: restraints, M10+)"); //closed handle
        return dot;
    }
}

///////////////////////////////////////////////Drinks
//Notes by Darem: Drinks are simply containers that start preloaded. Unlike condiments, the contents can be ingested directly
// rather then having to add it to something else first. They should only contain liquids. They have a default container size of 50.
// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:116
public partial class obj_item_reagent_containers_cup_glass_coffee : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:136
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to toggle cup lid.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:141
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[] Lid", Dm.truthy(lid_open) ? "Add" : "Remove");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:146
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        lid_open = Dm.truthy(lid_open) ? FALSE : TRUE;
        await update_icon_state();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:151
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(lid_open))
            icon_state = Dm.text(Dm.truthy(((datum_reagents)reagents).total_volume) ? "[]_full" : "[]_empty", base_icon_state);
        else
            icon_state = Dm.str(base_icon_state);
        return base.update_icon_state(__extra);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:208
public partial class obj_item_reagent_containers_cup_glass_coffee_cup : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:218
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(((datum_reagents)reagents).total_volume) ? Dm.str(base_icon_state) : Dm.text("[]_e", base_icon_state);
        return base.update_icon_state(__extra);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:231
public partial class obj_item_reagent_containers_cup_glass_waterbottle : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:253
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        cap_overlay = (mutable_appearance)mutable_appearance(cap_icon, cap_icon_state);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(start_capped))
        {
            // this is not done via initial_reagent_flags because it represents state
            update_container_flags(Dm.bitor(SEALED_CONTAINER, TRANSPARENT));
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:261
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(is_open_container()))
            Dm.append(ref dot, cap_overlay);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:266
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(cap_lost))
            Dm.append(ref dot, span_notice(I18n.T("The cap seems to be missing.")));
        else if (!Dm.truthy(is_open_container()))
            Dm.append(ref dot, span_notice(I18n.T("The cap is firmly on to prevent spilling. Alt-click to remove the cap.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The cap has been taken off. Alt-click to put a cap on.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:275
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(cap_lost))
        {
            to_chat(user, span_warning(I18n.T("The cap seems to be missing! Where did it go?")));
            return CLICK_ACTION_BLOCKING;
        }

        bool fumbled = HAS_TRAIT(user, TRAIT_CLUMSY) && prob(5);
        if (!Dm.truthy(is_open_container()) || fumbled)
        {
            reset_container_flags();
            Dm.animate(this, Dm.named("transform", null), Dm.named("time", 2), Dm.named("loop", 0));
            if (fumbled)
            {
                to_chat(user, span_warning(I18n.T("You fumble with []'s cap! The cap falls onto the ground and simply vanishes. Where the hell did it go?", this)));
                cap_lost = TRUE;
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You remove the cap from [].", this)));
                playsound(loc, Dm.resource("sound/items/handling/reagent_containers/plastic_bottle/bottle_cap_open.ogg"), 50, TRUE);
            }
        }
        else
        {
            update_container_flags(Dm.bitor(SEALED_CONTAINER, TRANSPARENT));
            to_chat(user, span_notice(I18n.T("You put the cap on [].", this)));
            playsound(loc, Dm.resource("sound/items/handling/reagent_containers/plastic_bottle/bottle_cap_close.ogg"), 50, TRUE);
        }
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // heehoo bottle flipping
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:298
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (QDELETED(this))
            return dot;
        if (Dm.truthy(is_open_container()) || !Dm.truthy(((datum_reagents)reagents).total_volume))
            return dot;
        if (prob(flip_chance)) // landed upright
        {
            visible_message(span_notice(I18n.T("[] lands upright!", this)));
            if (throwingdatum?.get_thrower() is mob_living thrower)
                Waiting.Detach(thrower.add_mood_event("bottle_flip", typeof(datum_mood_event_bottle_flip)));
        }
        else // landed on its side
            Dm.animate(this, Dm.named("transform", matrix(prob(50) ? 90 : -90, MATRIX_ROTATE)), Dm.named("time", 3), Dm.named("loop", 0));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:312
    public override async DmTask<object> pickup(mob user = null)
    {
        object dot = await base.pickup(user);
        Dm.animate(this, Dm.named("transform", null), Dm.named("time", 1), Dm.named("loop", 0));
        return dot;
    }
}

// Admin spawn
// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:335
public partial class obj_item_reagent_containers_cup_glass_waterbottle_relic : obj_item_reagent_containers_cup_glass_waterbottle
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:340
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var reagent_id = get_random_reagent_id() as Type;
        var random_reagent = (datum_reagent)Dm.@new(reagent_id);
        list_reagents = Dm.list(Dm.kv(random_reagent.GetType(), 50));
        object dot = base.Initialize(mapload, __extra);
        desc += span_notice(I18n.T("The writing reads '[]'.", random_reagent.name));
        update_appearance();
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:349
public partial class obj_item_reagent_containers_cup_glass_sillycup : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:358
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(((datum_reagents)reagents).total_volume) ? "water_cup" : "water_cup_e";
        return base.update_icon_state(__extra);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:362
public partial class obj_item_reagent_containers_cup_glass_bottle_juice_smallcarton : obj_item_reagent_containers_cup_glass_bottle_juice
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:371
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_takes_reagent_appearance),
            Dm.named("on_icon_changed", Dm.@new<datum_callback>(this, nameof(on_cup_change))),
            Dm.named("on_icon_reset", Dm.@new<datum_callback>(this, nameof(on_cup_reset))),
            Dm.named("base_container_type", typeof(obj_item_reagent_containers_cup_glass_bottle_juice_smallcarton))));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:380
public partial class obj_item_reagent_containers_cup_glass_colocup : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:394
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-4, 4);
        pixel_y = rand(-4, 4);
        if (!Dm.truthy(random_sprite))
            return dot;
        icon_state = Dm.text("colocup[]", rand(0, 6));
        if (icon_state == "colocup6")
            desc = "A cheap, mass produced style of cup, typically used at parties. Woah, this one is in red! What the hell?";
        return dot;
    }
}

//////////////////////////drinkingglass and shaker//
// tg: code/modules/reagents/reagent_containers/cups/drinks.dm:417
public partial class obj_item_reagent_containers_cup_glass_shaker : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:434
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(10))
        {
            name = "Nanotrasen 20th Anniversary Shaker";
            desc += " It has an emblazoned Nanotrasen logo on it.";
            icon_state = "shaker_n";
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:441
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[] custom drinks", Dm.truthy(using_custom_drinks) ? "Disable" : "Enable");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:446
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(Dm.truthy(using_custom_drinks) ? I18n.T("Alt-click to disable custom drink naming") : I18n.T("Alt-click to enable custom drink naming")));
        if (Dm.truthy(using_custom_drinks))
        {
            Dm.append(ref dot, span_notice(I18n.T("Drinks poured from this shaker will have the following name: []", custom_drink_name)));
            Dm.append(ref dot, span_notice(I18n.T("Drinks poured from this shaker will have the following description: []", custom_drink_desc)));
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:453
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(using_custom_drinks))
        {
            using_custom_drinks = FALSE;
            disable_custom_drinks();
            balloon_alert(user, "custom drinks disabled");
            return CLICK_ACTION_BLOCKING;
        }

        object new_name = reject_bad_text(await tgui_input_text(user, I18n.T("Drink name"), I18n.T("Set drink name"), custom_drink_name, 45, FALSE), 64);
        if (!Dm.truthy(new_name))
        {
            balloon_alert(user, "invalid drink name!");
            using_custom_drinks = FALSE;
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH))))
            return CLICK_ACTION_BLOCKING;
        object new_desc = reject_bad_text(await tgui_input_text(user, I18n.T("Drink description"), I18n.T("Set drink description"), custom_drink_desc, 64, TRUE), 128);
        if (!Dm.truthy(new_desc))
        {
            balloon_alert(user, "invalid drink description!");
            using_custom_drinks = FALSE;
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH))))
            return CLICK_ACTION_BLOCKING;

        using_custom_drinks = TRUE;
        custom_drink_name = Dm.str(new_name);
        custom_drink_desc = Dm.str(new_desc);
        enable_custom_drinks();
        balloon_alert(user, "now pouring custom drinks");
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:486
    public virtual void enable_custom_drinks()
    {
        RegisterSignal(this, COMSIG_REAGENTS_CUP_TRANSFER_TO, nameof(handle_transfer));
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:489
    public virtual void disable_custom_drinks()
    {
        UnregisterSignal(this, COMSIG_REAGENTS_CUP_TRANSFER_TO);
    }

    // tg: code/modules/reagents/reagent_containers/cups/drinks.dm:492
    [SignalHandler]
    public virtual void handle_transfer(atom origin = null, atom target = null)
    {
        // Should only work on drinking/shot glasses
        if (target is not obj_item_reagent_containers_cup_glass_drinkingglass target_glass)
            return;
        target_glass.name = custom_drink_name;
        target_glass.desc = custom_drink_desc;
    }
}
