// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/mouse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4; C2 translated splat). The swab's inert parts are rows; the lazarus injector came with gaps MD3.

// tg: code/modules/mob/living/basic/vermin/mouse.dm:2
public partial class mob_living_basic_mouse : mob_living_basic
{
    ///list of pet commands we follow
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:37
    private static DmList pet_commands => TgState.Current.mob_living_basic_mouse__pet_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_follow_start_active), typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:61
    [DmParams("mapload", "tame", "new_body_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object tame = Dm.arg(__extra, 0) ?? FALSE;
        object new_body_color = Dm.arg(__extra, 1);
        if (Dm.truthy(contributes_to_ratcap))
            Dm.combine(ref datum_controller_subsystem_mobs.cheeserats, this);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);

        if (Dm.truthy(tame))
            ADD_TRAIT(this, TRAIT_TAMED, INNATE_TRAIT);
        if (new_body_color != null)
            body_color = new_body_color;
        if (body_color == null)
            body_color = pick("brown", "gray", "white");
        held_state = Dm.text("mouse_[]", body_color); // not handled by variety element
        Waiting.NoSleep(AddElement(typeof(datum_element_animal_variety), "mouse", body_color, FALSE));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOUSE, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 10f));
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/mobs/non-humanoids/mouse/mousesqueek.ogg"), 1f)), 100f, null, null, null, SHORT_RANGE_SOUND_EXTRARANGE)); //as quiet as a mouse or whatever
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        make_tameable();
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming), 16f, 16f)); //max_x, max_y
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }

    private static ref DmList Initialize__loc_connections => ref TgState.Current.mob_living_basic_mouse__Initialize__loc_connections;

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:85
    public virtual void make_tameable()
    {
        if (HAS_TRAIT(this, TRAIT_TAMED))
            add_faction(FACTION_NEUTRAL);
        else
            Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), TgState.Current.mob_living_basic_mouse__make_tameable__food_types ??= Dm.list(typeof(obj_item_food_cheese)), 100f));

        Waiting.NoSleep(AddElement(typeof(datum_element_regal_rat_minion), typeof(mob_living_basic_mouse_rat), null, GLOB.regal_rat_minion_commands));
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:94
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref datum_controller_subsystem_mobs.cheeserats, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:98
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        bool sameside = Dm.truthy(user.faction_check_atom(this, exact_match: TRUE));
        if (user is mob_living_basic_regal_rat)
        {
            if (sameside)
                Dm.append(ref dot, span_notice("This rat serves under you."));
            else
                Dm.append(ref dot, span_warning(Dm.text("This peasant serves a different king! Strike [] down!", p_them())));
        }
        else if (user != this && user is mob_living_basic_mouse)
        {
            if (sameside)
                Dm.append(ref dot, span_notice("You both serve the same king."));
            else
                Dm.append(ref dot, span_warning("This fool serves a different king!"));
        }
        return dot;
    }

    /// Kills the rat and changes its icon state to be splatted (bloody).
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:112
    public virtual void splat()
    {
        icon_dead = Dm.text("mouse_[]_splat", body_color);
        Waiting.NoSleep(adjust_brute_loss(Dm.num(maxHealth)));
    }

    // On revival, re-add the mouse to the ratcap, or block it if we're at it
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:117
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        if (!Dm.truthy(contributes_to_ratcap))
            return await base.revive(full_heal_flags, excess_healing, force_grab_ghost);

        object aheal_included = Dm.bitand(full_heal_flags ?? NONE, HEAL_ADMIN);
        object cap = config.Get(typeof(datum_config_entry_number_ratcap));
        if (!Dm.truthy(aheal_included) && !Dm.truthy(ckey) && Dm.length(datum_controller_subsystem_mobs.cheeserats) >= Dm.num(cap))
        {
            visible_message(span_warning(Dm.text("[] twitches, but does not continue moving due to the overwhelming rodent population on the station!", this)));
            return null;
        }

        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (stat != DEAD)
            Dm.combine(ref datum_controller_subsystem_mobs.cheeserats, this);
        return dot;
    }

    // On death, remove the mouse from the ratcap, and turn it into an item if applicable
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:134
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        Dm.remove(ref datum_controller_subsystem_mobs.cheeserats, this);
        // Rats with a mind will not turn into a lizard snack on death
        if (mind != null)
            return await base.death(gibbed, __extra);

        // Call parent with gibbed = TRUE, becuase we're getting rid of the body
        object dot = await base.death(TRUE, __extra);
        // Now if we were't ACTUALLY gibbed, spawn the dead mouse
        if (!Dm.truthy(gibbed))
        {
            bool make_a_corpse = true;
            atom place_to_make_corpse = loc;
            bool must_equip = false;
            object equip_slot = null;
            mob_living_carbon holding_mob = null;
            obj_item_mob_holder found_holder = null;
            if (loc is obj_item_mob_holder)//If our mouse is dying in place holder we want to put the dead mouse where the place holder was
            {
                found_holder = loc as obj_item_mob_holder;
                place_to_make_corpse = found_holder.loc;
                if (found_holder.loc is mob_living_carbon)
                {
                    holding_mob = found_holder.loc as mob_living_carbon;
                    place_to_make_corpse = get_turf(holding_mob) as atom;
                    equip_slot = holding_mob.get_slot_by_item(found_holder);
                    if (Dm.eq(equip_slot, ITEM_SLOT_HANDS) || Dm.eq(equip_slot, ITEM_SLOT_RPOCKET) || Dm.eq(equip_slot, ITEM_SLOT_LPOCKET))
                        must_equip = true;
                }
                if (found_holder.loc is obj_machinery_microwave)//Microwaves gib things that die when cooked, so we don't need to make a dead body too
                    make_a_corpse = false;
            }
            if (make_a_corpse)
            {
                obj_item_food_deadmouse mouse = Dm.@new<obj_item_food_deadmouse>(place_to_make_corpse);
                mouse.copy_corpse(this);
                if (HAS_TRAIT(this, TRAIT_BEING_SHOCKED))
                {
                    mouse.desc = "They're toast.";
                    await mouse.add_atom_colour("#3A3A3A", FIXED_COLOUR_PRIORITY);
                }
                if (found_holder != null)
                    await found_holder.release(FALSE);
                if (must_equip)
                {
                    if (Dm.eq(equip_slot, ITEM_SLOT_HANDS))
                        await holding_mob.dropItemToGround(found_holder);
                    await holding_mob.equip_to_slot(mouse, equip_slot);
                }
            }
        }
        qdel(this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:179
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        object dot = await base.UnarmedAttack(attack_target, proximity_flag, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(proximity_flag))
            return dot;

        if (attack_target is obj_item_food_cheese)
        {
            await try_consume_cheese(attack_target as obj_item_food_cheese);
            return TRUE;
        }

        if (attack_target is obj_structure_cable)
        {
            await try_bite_cable(attack_target as obj_structure_cable);
            return TRUE;
        }
        return dot;
    }

    /// Signal proc for [COMSIG_ATOM_ENTERED]. Sends a lil' squeak to chat when someone walks over us.
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:195
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered = null)
    {
        if (ishuman(entered) && !IS_UNCONSCIOUS_OR_CRIT(this))
            to_chat(entered, span_notice("Squeak!")); // the chat icon (icon2html) is the chat icons row (not-translated.md, M9)
    }

    /// Called when a mouse is hand-fed some cheese, it will stop being afraid of humans
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:202
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
        add_faction(FACTION_NEUTRAL);
        await try_consume_cheese(food as obj_item_food_cheese);
        if (ai_controller != null)
            await (ai_controller as datum_ai_controller).cancel_current_plan(); // Interrupt any current fleeing
    }

    /// Attempts to consume a piece of cheese, causing a few effects.
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:210
    public virtual async DmTask try_consume_cheese(obj_item_food_cheese cheese = null)
    {
        // Royal cheese will evolve us into a regal rat
        if (cheese is obj_item_food_cheese_royal)
        {
            visible_message(
                span_warning(Dm.text("[] devours []! They morph into something... greater!", this, cheese)),
                span_notice(Dm.text("You devour [], and start morphing into something... greater!", cheese))
            );
            await evolve_into_regal_rat();
            qdel(cheese);
            return;
        }

        object cap = config.Get(typeof(datum_config_entry_number_ratcap));
        // Normal cheese will either heal us
        if (prob(90) || Dm.num(health) < Dm.num(maxHealth))
        {
            visible_message(
                span_notice(Dm.text("[] nibbles [].", this, cheese)),
                span_notice(Dm.text("You nibble [][].", cheese, Dm.num(health) < Dm.num(maxHealth) ? ", restoring your health" : ""))
            );
            await adjust_brute_loss(-Dm.num(maxHealth));
        }

        // Or, if we're at full health, there's a 10% chance that normal cheese will spawn a new mouse
        // ...if the rat cap allows us, that is
        else if (Dm.length(datum_controller_subsystem_mobs.cheeserats) >= Dm.num(cap))
        {
            visible_message(
                span_warning(Dm.text("[] carefully eats [], hiding it from the [] mice on the station!", this, cheese, cap)),
                span_notice(Dm.text("You carefully nibble [], hiding it from the [] other mice on board the station.", cheese, cap))
            );
        }
        else
        {
            visible_message(
                span_notice(Dm.text("[] nibbles through [], attracting another mouse!", this, cheese)),
                span_notice(Dm.text("You nibble through [], attracting another mouse!", cheese))
            );
            create_a_new_rat();
        }

        qdel(cheese);
    }

    /// Evolves this rat into a regal rat
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:249
    public virtual async DmTask evolve_into_regal_rat()
    {
        mob_living_basic_regal_rat_controlled regalrat = Dm.@new<mob_living_basic_regal_rat_controlled>(loc);
        if (mind != null)
            await mind.transfer_to(regalrat);
        Waiting.Detach(regalrat.say("RISE, MY SUBJECTS! SCREEEEEEE!"));
        qdel(this);
    }

    /// Creates a new mouse based on this mouse's subtype.
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:256
    public virtual void create_a_new_rat()
    {
        Dm.@new<mob_living_basic_mouse>(loc, HAS_TRAIT(this, TRAIT_TAMED) ? TRUE : FALSE);
    }

    /// Biting into a cable will cause a mouse to get shocked and die if applicable. Or do nothing if they're lucky.
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:260
    public virtual async DmTask try_bite_cable(obj_structure_cable cable = null)
    {
        if (Dm.truthy(cable.avail()) && !HAS_TRAIT(this, TRAIT_SHOCKIMMUNE) && prob(cable_zap_prob))
        {
            visible_message(
                span_warning(Dm.text("[] chews through \\the []. It's toast!", this, cable)),
                span_userdanger(Dm.text("As you bite deeply into [], you suddenly realize this may have been a bad idea.", cable)),
                span_hear("You hear electricity crack.")
            );
            // Finely toasted
            ADD_TRAIT(this, TRAIT_BEING_SHOCKED, TRAIT_GENERIC);
            // Unfortunately we can't check the return value of electrocute_act before displaying a message,
            // as it's possible the damage from electrocution results in our hunter being deleted.
            // But what are the odds of the shock failing? Hahaha...
            await electrocute_act(Dm.num(maxHealth) * 2, cable, flags: SHOCK_SUPPRESS_MESSAGE);
        }
        else
        {
            visible_message(
                span_warning(Dm.text("[] chews through \\the [].", this, cable)),
                span_notice(Dm.text("You chew through \\the [].", cable))
            );
        }

        playsound(cable, Dm.resource("sound/effects/sparks/sparks2.ogg"), 100, TRUE);
        await cable.deconstruct();
    }
}

// tg: code/modules/mob/living/basic/vermin/mouse.dm:292
public partial class mob_living_basic_mouse_brown_tom : mob_living_basic_mouse_brown
{
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:304
    public override void make_tameable()
    {
        ADD_TRAIT(this, TRAIT_TAMED, INNATE_TRAIT);
        base.make_tameable();
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:308
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Tom fears no cable.
        ADD_TRAIT(this, TRAIT_SHOCKIMMUNE, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "squeak"));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:314
    public override void create_a_new_rat()
    {
        Dm.@new<mob_living_basic_mouse_brown>(loc, HAS_TRAIT(this, TRAIT_TAMED) ? TRUE : FALSE); // dominant gene
    }
}

// tg: code/modules/mob/living/basic/vermin/mouse.dm:317
public partial class mob_living_basic_mouse_rat : mob_living_basic_mouse
{
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:329
    public override void make_tameable()
    {
        // Unlike in real life, space rats are horrible creatures who don't like you
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:332
    public override void create_a_new_rat()
    {
        Dm.@new<mob_living_basic_mouse_rat>(loc);
    }
}

/// Mice turn into food when they die
// tg: code/modules/mob/living/basic/vermin/mouse.dm:336
public partial class obj_item_food_deadmouse : obj_item_food
{
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:351
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOUSE, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 10f));
        RegisterSignal(this, COMSIG_ATOM_ON_LAZARUS_INJECTOR, nameof(use_lazarus));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:356
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_blood), 20f), Dm.kv(typeof(datum_reagent_consumable_liquidgibs), 5f));
    }

    /// Copy properties from an imminently dead mouse
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:360
    public virtual void copy_corpse(mob_living_basic_mouse dead_critter = null)
    {
        body_color = dead_critter.body_color as string;
        critter_type = dead_critter.GetType();
        name = dead_critter.name;
        icon_state = Dm.text("[]", dead_critter.icon_dead);
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:367
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (reagents is datum_reagents holder && (Dm.truthy(holder.has_reagent(typeof(datum_reagent_yuck))) || Dm.truthy(holder.has_reagent(typeof(datum_reagent_fuel)))))
            Dm.append(ref dot, span_warning(Dm.text("[] dripping with fuel and smells terrible.", p_Theyre())));
        return dot;
    }

    ///Spawn a new mouse from this dead mouse item when hit by a lazarus injector and conditions are met.
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:373
    [SignalHandler]
    public virtual object use_lazarus(datum source = null, obj_item_lazarus_injector injector = null, mob user = null)
    {
        if (!Dm.eq(injector.revive_type, SENTIENCE_ORGANIC))
        {
            balloon_alert(user, "invalid creature!");
            return null;
        }
        mob_living_basic_mouse revived_critter = Dm.@new((Type)critter_type, drop_location(), FALSE, body_color) as mob_living_basic_mouse;
        revived_critter.name = name;
        Waiting.NoSleep(revived_critter.lazarus_revive(user as mob_living, injector.malfunctioning));
        injector.expend(revived_critter, user);
        qdel(this);
        return LAZARUS_INJECTOR_USED;
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:385
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()) || !Dm.truthy(user.combat_mode))
            return NONE;
        if (!isturf(loc))
        {
            balloon_alert(user, "can't butcher here!");
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, "butchering...");
        if (!Dm.truthy(await do_after(user, 0.75f * SECONDS, this)))
        {
            balloon_alert(user, "interrupted!");
            return ITEM_INTERACT_BLOCKING;
        }

        loc.balloon_alert(user, "butchered");
        Dm.@new<obj_item_food_meat_slab_mouse>(loc);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/vermin/mouse.dm:402
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (reagents == null || !Dm.truthy(interacting_with.is_open_container()))
            return NONE;

        // is_open_container will not return truthy if target.reagents doesn't exist
        datum_reagents target_reagents = interacting_with.reagents as datum_reagents;
        datum_reagents own_reagents = reagents as datum_reagents;
        float trans_amount = Dm.num(own_reagents.maximum_volume) - Dm.num(own_reagents.total_volume) * (4f / 3);
        if (Dm.truthy(target_reagents.has_reagent(typeof(datum_reagent_fuel))) && Dm.truthy(await target_reagents.trans_to(this, trans_amount)))
        {
            to_chat(user, span_notice(Dm.text("You dip [] into [].", this, interacting_with)));
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// tg: code/modules/mob/living/basic/vermin/mouse.dm:414
public partial class obj_item_food_deadmouse_moldy : obj_item_food_deadmouse
{
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:422
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_blood), 20f), Dm.kv(typeof(datum_reagent_consumable_liquidgibs), 5f), Dm.kv(typeof(datum_reagent_consumable_mold), 10f));
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_mouse__pet_commands;
    internal DmList mob_living_basic_mouse__make_tameable__food_types;
    // tg: code/modules/mob/living/basic/vermin/mouse.dm:76
    internal DmList mob_living_basic_mouse__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(mob_living_basic_mouse.on_entered)));
}
