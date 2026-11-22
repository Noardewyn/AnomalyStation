// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/glassbottle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298: the bottle of nothing): the glass bottle root and the broken bottle; the champagne, the molotov, pruno, wine, absinthe and the other bottles with procs of their own stay inert with the bar.
// A broken bottle draws its bottle's sprite: the torn outline is an icon operation (a row). The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:8
public partial class obj_item_reagent_containers_cup_glass_bottle : obj_item_reagent_containers_cup_glass
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:33
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // var/static/list/recipes = list(/datum/crafting_recipe/molotov); AddElement(/datum/element/slapcrafting, recipes)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.list(typeof(datum_crafting_recipe_molotov))));
        // register_item_context(): the context tips (screentips, not now)
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:53
    public override void on_reagent_change(datum holder = null, params object[] __extra)
    {
        base.on_reagent_change(holder, __extra);
        if (!Dm.truthy(((datum_reagents)reagents)?.total_volume))
            tool_behaviour = TOOL_ROLLINGPIN; // Glass bottles can be used as rolling pins when empty
        else
            tool_behaviour = null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:60
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == message_in_a_bottle)
        {
            message_in_a_bottle = null;
            if (!QDELETED(this))
                await update_icon(UPDATE_OVERLAYS);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:67
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        if (message_in_a_bottle != null)
            await message_in_a_bottle.forceMove(drop_location() as atom);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:71
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (message_in_a_bottle != null)
            dot.Add(span_info(I18n.T("there's \\a [] inside it. Break it to take it out, or find a beach or ocean and toss it with [].", message_in_a_bottle, EXAMINE_HINT("right-click"))));
        else if (Dm.truthy(isGlass))
            dot.Add(span_tinynoticeital(I18n.T("you could place a paper, photo or space cash inside it...")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:78
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (message_in_a_bottle != null)
        {
            object overlay = add_message_overlay();
            if (Dm.truthy(overlay))
                dot.Add(overlay);
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:85
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode) || !HAS_TRAIT(interacting_with, TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION))
            return await base.interact_with_atom_secondary(interacting_with, user, modifiers);
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
        {
            balloon_alert(user, I18n.T("it's stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] tosses [] in []", user, this, interacting_with)), span_notice(I18n.T("You toss [] in []", this, interacting_with)), span_notice(I18n.T("you hear a splash.")));
        // SSpersistence.save_message_bottle(message_in_a_bottle, type): the bottles kept between rounds (persistence, M10+)
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/glassbottle.dm:92", "interact_with_atom_secondary: SSpersistence.save_message_bottle (the messages kept between rounds)");
        playsound(interacting_with, Dm.resource("sound/effects/bigsplash.ogg"), 70);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:97
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(isGlass))
            return NONE;
        if (tool is not obj_item_paper && tool is not obj_item_stack_spacecash && tool is not obj_item_photo)
            return NONE;
        if (message_in_a_bottle != null)
        {
            balloon_alert(user, I18n.T("has a message already!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("it's stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("message inserted"));
        message_in_a_bottle = tool;
        await update_icon(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:113
    public virtual object add_message_overlay()
    {
        if (message_in_a_bottle is obj_item_paper)
            return "paper_in_bottle";
        if (message_in_a_bottle is obj_item_photo)
            return "photo_in_bottle";
        if (message_in_a_bottle is obj_item_stack_spacecash)
            return "cash_in_bottle";
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:128
    public override async DmTask post_smash(atom target = null, atom thrower = null, datum_thrownthing throwingdatum = null, obj_item_broken_bottle broken = null)
    {
        if (throwingdatum == null && ismob(thrower))
            await ((mob)thrower).put_in_hands(broken);
        broken.inhand_icon_state = broken_inhand_icon_state;
        if (message_in_a_bottle != null)
            await message_in_a_bottle.forceMove(drop_location() as atom);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:134
    public override async DmTask<object> try_splash(mob user = null, atom target = null)
    {
        if (!Dm.truthy(isGlass))
            return await base.try_splash(user, target);
        return FALSE; // instead of splashing, hit them with the bottle!
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:139
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(isGlass))
            return;

        bool head_hitter = Dm.eq(user.zone_selected, BODY_ZONE_HEAD) && isliving(target);
        if (!QDELETED(target))
        {
            // An attack that targets the head of a living mob will attempt to knock them down
            if (head_hitter)
            {
                mob_living living_target = (mob_living)target;
                float knockdown_effectiveness = 0;
                if (!HAS_TRAIT(target, TRAIT_HEAD_INJURY_BLOCKED))
                    knockdown_effectiveness = bottle_knockdown_duration + ((force / 10) * 1 * SECONDS) - Dm.num(living_target.getarmor(BODY_ZONE_HEAD, MELEE));
                if (prob(knockdown_effectiveness))
                    await living_target.Knockdown(min(knockdown_effectiveness, 20 * SECONDS));
            }
        }

        // Displays a custom message which follows the attack
        if (target == user)
        {
            user.visible_message(
                span_warning(I18n.T("[] smashes [] []!", user, this, head_hitter ? I18n.T("over [] head", user.p_their()) : I18n.T("against []selves", user.p_them()))),
                span_warning(I18n.T("You smash [] []!", this, head_hitter ? I18n.T("over your head") : I18n.T("against yourself"))));
        }
        else
        {
            user.visible_message(
                span_warning(I18n.T("[] smashes [] []!", user, this, head_hitter ? I18n.T("over []'s head", target) : I18n.T("against []", target))),
                span_warning(I18n.T("You smash [] []!", this, head_hitter ? I18n.T("over []'s head", target) : I18n.T("against []", target))));
        }

        // Finally, smash the bottle. This kills (del) the bottle and also does all the logging for us
        await smash(target, user);
    }

    /*
     * Proc to make the bottle spill some of its contents out in a froth geyser of varying intensity/height
     * Arguments:
     * * offset_x = pixel offset by x from where the froth animation will start
     * * offset_y = pixel offset by y from where the froth animation will start
     * * intensity = how strong the effect is, both visually and in the amount of reagents lost. comes in three flavours
    */
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:177
    public virtual async DmTask make_froth(object offset_x = null, object offset_y = null, object intensity = null)
    {
        if (!Dm.truthy(intensity))
            return;

        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
            return;

        float amount_lost = Dm.num(intensity) * 5;
        await holder.remove_all(amount_lost);

        visible_message(span_warning(I18n.T("Some of []'s contents are let loose!", name)));
        string intensity_state = null;
        switch (Dm.num(intensity))
        {
            case 1:
                intensity_state = "low";
                break;
            case 2:
                intensity_state = "medium";
                break;
            case 3:
                intensity_state = "high";
                break;
        }
        ///The froth fountain that we are sticking onto the bottle
        mutable_appearance froth = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/drinks/drink_effects.dmi"), Dm.text("froth_bottle_[]", intensity_state));
        froth.pixel_w = Dm.num(offset_x);
        froth.pixel_z = Dm.num(offset_y);
        add_overlay(froth);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:204
public partial class obj_item_broken_bottle : obj_item
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:228
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", force)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 20 * SECONDS), Dm.named("effectiveness", 55f)));
        return dot;
    }

    /// Mimics the appearance and properties of the passed in bottle.
    /// Takes the broken bottle to mimic, and the thing the bottle was broken agaisnt as args
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:238
    public virtual async DmTask mimic_broken(obj_item_reagent_containers_cup_glass to_mimic = null, atom target = null, object break_top = null)
    {
        icon_state = to_mimic.icon_state;
        // var/icon/drink_icon = new(to_mimic.icon, icon_state); drink_icon.Blend(broken_outline, ICON_OVERLAY, rand(5), ...); SwapColor; icon = drink_icon: the torn outline
        icon = to_mimic.icon;
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/glassbottle.dm:243", "mimic_broken: the torn outline blended on the bottle's sprite (an icon operation the client has no replica of): the broken bottle draws the whole bottle");
        if (Dm.truthy(break_top)) //if the bottle breaks its top off instead of the bottom
            desc = I18n.T("A bottle with its neck smashed off.");

        if (to_mimic is obj_item_reagent_containers_cup_glass_bottle_juice)
        {
            force = 0;
            throwforce = 0;
            desc = I18n.T("A carton with the bottom half burst open. Might give you a papercut.");
        }
        else
        {
            if (prob(33))
                Dm.@new<obj_item_shard>(to_mimic.drop_location());
            playsound(this, SFX_SHATTER, 70, TRUE);
        }
        name = Dm.text("broken []", to_mimic.name);
        to_mimic.transfer_fingerprints_to(this);
    }
}

// Gaps H3 (D333, the bar): the bottles with procs of their own.

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:381
public partial class obj_item_reagent_containers_cup_glass_bottle_holywater : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:391
    public override object add_message_overlay() => null; //looks too weird...
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:427
public partial class obj_item_reagent_containers_cup_glass_bottle_wine : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:434
    public override async DmTask add_initial_reagents()
    {
        await base.add_initial_reagents();
        object wine_info = generate_vintage();
        var located_wine = Dm.locate<datum_reagent_consumable_ethanol_wine>(((datum_reagents)reagents).reagent_list);
        if (located_wine != null)
        {
            DmList wine_data = located_wine.data as DmList;
            LAZYSET(ref wine_data, "vintage", wine_info);
            located_wine.data = wine_data;
        }
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:441
    public virtual object generate_vintage()
    {
        return Dm.text("[] Nanotrasen Light Red", CURRENT_STATION_YEAR);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:444
public partial class obj_item_reagent_containers_cup_glass_bottle_wine_unlabeled : obj_item_reagent_containers_cup_glass_bottle_wine
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:448
    public override object generate_vintage()
    {
        float year = rand(CURRENT_STATION_YEAR - 50, CURRENT_STATION_YEAR);
        object type = pick(
            "Bold Red",
            "Dessert",
            "Dry White",
            "Light Red",
            "Medium Red",
            "Rich White",
            "Rose",
            "Sparkling",
            "Sweet White");
        object origin = pick(
            "Local",
            "Nanotrasen",
            "Syndicate");
        return Dm.text("[] [] []", year, origin, type);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:468
public partial class obj_item_reagent_containers_cup_glass_bottle_absinthe : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:475
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        redact();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:479
    public virtual void redact()
    {
        // There was a large fight in the coderbus about a player reference
        // in absinthe. Ergo, this is why the name generation is now so
        // complicated. Judge us kindly.
        object shortname = pick_weight(Dm.list(Dm.kv("T&T", 1), Dm.kv("A&A", 1), Dm.kv("Generic", 1)));
        string fullname = null;
        switch (Dm.str(shortname))
        {
            case "T&T":
                fullname = "Teal and Tealer";
                break;
            case "A&A":
                fullname = "Ash and Asher";
                break;
            case "Generic":
                fullname = "Nanotrasen Cheap Imitations";
                break;
        }
        DmList removals = Dm.list(
            "[REDACTED]",
            "[EXPLETIVE DELETED]",
            "[EXPUNGED]",
            "[INFORMATION ABOVE YOUR SECURITY CLEARANCE]",
            "[MOVE ALONG CITIZEN]",
            "[NOTHING TO SEE HERE]");
        float chance = 50;

        if (prob(chance))
            shortname = pick_n_take(removals);

        DmList final_fullname = Dm.list();
        foreach (object each in Dm.each<object>(splittext(fullname, " ")))
        {
            object word = each;
            if (prob(chance))
                word = pick_n_take(removals);
            Dm.append(ref final_fullname, word);
        }

        fullname = Dm.str(jointext(final_fullname, " "));

        // Actually finally setting the new name and desc
        name = Dm.text("[] []", shortname, name);
        desc = Dm.text("[] [] Inc.", desc, fullname);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:519
public partial class obj_item_reagent_containers_cup_glass_bottle_absinthe_premium : obj_item_reagent_containers_cup_glass_bottle_absinthe
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:525
    public override void redact()
    {
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:557
public partial class obj_item_reagent_containers_cup_glass_bottle_sake : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:564
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(10))
        {
            name = "Fluffy Tail Sake";
            desc += " On the bottle is a picture of a kitsune with nine touchable tails.";
            icon_state = "sakebottle_k";
        }
        else if (prob(10))
        {
            name = "Inubashiri's Home Brew";
            desc += " Awoo.";
            icon_state = "sakebottle_i";
        }
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:575
    public override object add_message_overlay()
    {
        if (icon_state == "sakebottle_k") //doesn't fit the sprite
            return null;
        return base.add_message_overlay();
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:598
public partial class obj_item_reagent_containers_cup_glass_bottle_curacao : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:605
    public override object add_message_overlay() => null; //doesn't fit the sprite
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:640
public partial class obj_item_reagent_containers_cup_glass_bottle_champagne : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:653
    public override object add_message_overlay() => null; //doesn't stylistically fit the sprite

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:659
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_open_container()))
            return await base.attack_self(user, modifiers, __extra);
        balloon_alert(user, "fiddling with cork...");
        if (Dm.truthy(await do_after(user, 1 * SECONDS, this)))
            return await pop_cork(user as mob_living, sabrage: FALSE, froth_severity: pick(0, 1));
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:666
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (Dm.truthy(is_open_container()))
            return NONE;

        if (!Dm.eq(tool.get_sharpness(), SHARP_EDGED))
            return NONE;

        if (tool != user.get_active_held_item()) //no TK allowed
        {
            to_chat(user, span_userdanger(I18n.T("Such a feat is beyond your skills of telekinesis!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.num(tool.force) < 5)
        {
            balloon_alert(user, "not strong enough!");
            return ITEM_INTERACT_BLOCKING;
        }

        playsound(user, Dm.resource("sound/items/unsheath.ogg"), 25, TRUE);
        balloon_alert(user, "preparing to swing...");
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this))) //takes longer because you are supposed to take the foil off the bottle first
            return ITEM_INTERACT_BLOCKING;

        //The bonus to success chance that the user gets for being a command role
        var liver = (user as mob_living_carbon)?.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        float command_bonus = liver != null && HAS_TRAIT(liver, TRAIT_ROYAL_METABOLISM) ? 20 : 0;

        //The bonus to success chance that the user gets for having a sabrage skillchip installed/otherwise having the trait through other means
        float skillchip_bonus = HAS_TRAIT(user, TRAIT_SABRAGE_PRO) ? 35 : 0;
        //calculate success chance. example: captain's sabre - 15 force = 75% chance
        float sabrage_chance = (Dm.num(tool.force) * sabrage_success_percentile) + command_bonus + skillchip_bonus;

        if (prob(sabrage_chance))
        {
            ///Severity of the resulting froth to pass to make_froth()
            object severity_to_pass = null;
            if (sabrage_chance > 100)
                severity_to_pass = 0;
            else
            {
                //the less likely we were to succeed, the more of the drink will end up wasted in froth
                if (sabrage_chance >= 1 && sabrage_chance <= 33)
                    severity_to_pass = 3;
                else if (sabrage_chance >= 34 && sabrage_chance <= 66)
                    severity_to_pass = 2;
                else if (sabrage_chance >= 67 && sabrage_chance <= 99)
                    severity_to_pass = 1;
            }
            return Dm.truthy(await pop_cork(user, sabrage: TRUE, froth_severity: severity_to_pass)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(
            span_danger(I18n.T("[] fumbles the sabrage and cuts [] in half, spilling it over themselves!", user, this)),
            span_danger(I18n.T("You fail your stunt and cut [] in half, spilling it over you!", this)));
        await user.add_mood_event("sabrage_fail", typeof(datum_mood_event_sabrage_fail));
        return Dm.truthy(await smash(target: user, break_top: TRUE)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:720
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(is_open_container()))
        {
            if (Dm.truthy(sabraged))
                icon_state = Dm.text("[]_sabrage", base_icon_state);
            else
                icon_state = Dm.text("[]_popped", base_icon_state);
        }
        else
            icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:730
    public virtual async DmTask<object> pop_cork(mob_living user = null, object sabrage = null, object froth_severity = null)
    {
        if (!Dm.truthy(sabrage))
        {
            user.visible_message(
                span_danger(I18n.T("[] loosens the cork of [], causing it to pop out of the bottle with great force.", user, this)),
                span_nicegreen(I18n.T("You elegantly loosen the cork of [], causing it to pop out of the bottle with great force.", this)));
        }
        else
        {
            sabraged = TRUE;
            user.visible_message(
                span_danger(I18n.T("[] cleanly slices off the cork of [], causing it to fly off the bottle with great force.", user, this)),
                span_nicegreen(I18n.T("You elegantly slice the cork off of [], causing it to fly off the bottle with great force.", this)));
            foreach (mob_living_carbon stunt_witness in Dm.each<mob_living_carbon>(view(7, user)))
            {
                stunt_witness.clear_mood_event("sabrage_success");
                if (stunt_witness == user)
                {
                    await stunt_witness.add_mood_event("sabrage_success", typeof(datum_mood_event_sabrage_success));
                    continue;
                }
                await stunt_witness.add_mood_event("sabrage_witness", typeof(datum_mood_event_sabrage_witness));
            }
        }

        add_container_flags(OPENCONTAINER);
        playsound(this, Dm.resource("sound/items/champagne_pop.ogg"), 70, TRUE);
        update_appearance();
        await make_froth(offset_x: 0, offset_y: Dm.truthy(sabraged) ? 13 : 15, intensity: froth_severity); //the y offset for sabraged is lower because the bottle's lip is smashed
        ///Type of cork to fire away
        Type cork_to_fire = Dm.truthy(sabraged) ? typeof(obj_projectile_bullet_champagne_cork_sabrage) : typeof(obj_projectile_bullet_champagne_cork);
        ///Our resulting cork projectile
        var popped_cork = (obj_projectile_bullet_champagne_cork)Dm.@new(cork_to_fire, drop_location());
        popped_cork.firer = user;
        popped_cork.fired_from = this;
        await popped_cork.fire(Dm.num(dir2angle(user.dir)) + rand(-30, 30));
        return TRUE;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:762
public partial class obj_projectile_bullet_champagne_cork : obj_projectile_bullet
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:778
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_projectile_drop), drop_type));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:812
public partial class obj_item_reagent_containers_cup_glass_bottle_hooch : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:819
    public override object add_message_overlay() => null; //doesn't fit the sprite
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:822
public partial class obj_item_reagent_containers_cup_glass_bottle_moonshine : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:829
    public override object add_message_overlay() => null; //doesn't fit the sprite
}

////////////////////////// MOLOTOV ///////////////////////
// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:890
public partial class obj_item_reagent_containers_cup_glass_bottle_molotov : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:908
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        var bottle = Dm.locate<obj_item_reagent_containers_cup_glass_bottle>(components);
        if (bottle == null)
        {
            await base.on_craft_completion(components, current_recipe, crafter);
            return;
        }
        icon_state = bottle.icon_state;
        await ((datum_reagents)bottle.reagents).trans_to(this, 100, copy_only: TRUE);
        if (bottle is obj_item_reagent_containers_cup_glass_bottle_juice)
        {
            desc += " You're not sure if making this out of a carton was the brightest idea.";
            isGlass = FALSE;
        }
        await base.on_craft_completion(components, current_recipe, crafter);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:919
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        base.throw_impact(hit_atom, throwingdatum, FALSE); // do_splash = FALSE
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:922
    public override async DmTask<object> smash(atom target = null, atom thrower = null, datum_thrownthing throwingdatum = null, float? break_top = null)
    {
        bool firestarter = false;
        foreach (datum_reagent contained_reagent in Dm.each<datum_reagent>(((datum_reagents)reagents).reagent_list))
        {
            foreach (Type accelerant_type in Dm.each<Type>(accelerants))
            {
                if (istype(contained_reagent, accelerant_type))
                {
                    firestarter = true;
                    break;
                }
            }
        }
        object dot = await base.smash(target, thrower, throwingdatum, break_top);
        if (firestarter && Dm.truthy(active))
        {
            if (!QDELETED(target))
                await target.fire_act();
            Dm.@new<obj_effect_hotspot>(get_turf(target));
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:935
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.num(tool.get_temperature()) < FIRE_MINIMUM_TEMPERATURE_TO_EXIST || Dm.truthy(active))
            return NONE;

        active = TRUE;
        log_bomber(user, "has primed a", this, "for detonation");

        to_chat(user, span_info(I18n.T("You light [] on fire.", this)));
        add_overlay(custom_fire_overlay__proc() is var custom && Dm.truthy(custom) ? custom : GLOB.fire_overlay);
        if (!Dm.truthy(isGlass))
            _addtimer(Dm.@new<datum_callback>(this, nameof(explode)), 5 * SECONDS, file: "code/modules/reagents/reagent_containers/cups/glassbottle.dm", line: 945);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:948
    public virtual async DmTask explode()
    {
        if (!Dm.truthy(active))
            return;
        if (get_turf(this) != null)
        {
            atom target = loc;
            for (int i = 1; i <= 2; i++)
            {
                if (target is obj_item_storage)
                    target = target.loc;
            }
            await splash_reagents(target, allow_closed_splash: TRUE);
            await target.fire_act();
        }
        qdel(this);
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:960
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(isGlass))
            {
                to_chat(user, span_danger(I18n.T("The flame's spread too far on it!")));
                return null;
            }
            to_chat(user, span_info(I18n.T("You snuff out the flame on [].", this)));
            cut_overlay(custom_fire_overlay__proc() is var custom && Dm.truthy(custom) ? custom : GLOB.fire_overlay);
            active = FALSE;
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:972
public partial class obj_item_reagent_containers_cup_glass_bottle_pruno : obj_item_reagent_containers_cup_glass_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:982
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(check_fermentation));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:986
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_MOVABLE_MOVED);
        return base.Destroy(force);
    }

    // Checks to see if the pruno can ferment, i.e. is it inside a structure (e.g. toilet), or a machine (e.g. washer)?
    // TODO: make it so the washer spills reagents if a reagent container is in there, for now, you can wash pruno
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:993
    [SignalHandler]
    public virtual void check_fermentation()
    {
        if (!(ismachinery(loc) || isstructure(loc)))
        {
            if (Dm.truthy(fermentation_timer))
            {
                fermentation_time_remaining = timeleft(fermentation_timer);
                deltimer(fermentation_timer);
                fermentation_timer = null;
            }
            return;
        }
        if (Dm.truthy(fermentation_timer))
            return;
        if (!Dm.truthy(fermentation_time_remaining))
            fermentation_time_remaining = fermentation_time;
        fermentation_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(do_fermentation)), Dm.num(fermentation_time_remaining), Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/modules/reagents/reagent_containers/cups/glassbottle.dm", line: 1005);
        fermentation_time_remaining = null;
    }

    // actually ferment
    // tg: code/modules/reagents/reagent_containers/cups/glassbottle.dm:1010
    public virtual async DmTask do_fermentation()
    {
        fermentation_time_remaining = null;
        fermentation_timer = null;
        var holder = (datum_reagents)reagents;
        await holder.remove_reagent(typeof(datum_reagent_consumable_prunomix), 50);
        if (prob(10))
        {
            await holder.add_reagent(typeof(datum_reagent_toxin_bad_food), 15); // closest thing we have to botulism
            await holder.add_reagent(typeof(datum_reagent_consumable_ethanol_pruno), 35);
        }
        else
            await holder.add_reagent(typeof(datum_reagent_consumable_ethanol_pruno), 50);
        name = "bag of pruno";
        desc = "Fermented prison wine made from fruit, sugar, and despair. You probably shouldn't drink this around Security.";
        icon_state = "trashbag1"; // pruno releases air as it ferments, we don't want to simulate this in atmos, but we can make it look like it did
        foreach (mob_living M in Dm.each<mob_living>(view(2, get_turf(this)))) // letting people and/or narcs know when the pruno is done
        {
            if (HAS_TRAIT(M, TRAIT_ANOSMIA))
                to_chat(M, span_info(I18n.T("A pungent smell emanates from [], like fruit puking out its guts.", this)));
            playsound(get_turf(this), Dm.resource("sound/effects/bubbles/bubbles2.ogg"), 25, TRUE);
        }
    }
}
