// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/extinguisher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b (the owner, 2026-10-15: put out the demo's plasma fire): the whole file. Left for their milestones (rows in
// docs/ai/specs/live-partial.txt): suicide_act (M10), the ghetto jetpack's
// slapcrafting element came with the crafting menu (M5 slice 8), the head bash (attack_secondary) with the wounds (M6 slice 3).
// The var blocks are generated (D111).

// tg: code/game/objects/items/tools/extinguisher.dm:1
[DmPath("/obj/item/extinguisher")]
public partial class obj_item_extinguisher : obj_item
{
    // The earlier of the type's two Initialize definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/game/objects/items/tools/extinguisher.dm:57
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg: code/game/objects/items/tools/extinguisher.dm:59  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        // tg: code/game/objects/items/tools/extinguisher.dm:61
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_extinguisher__slapcraft_recipe_list));

        register_context();
        return dot;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:68
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item != this)
            return null;
        context[SCREENTIP_CONTEXT_LMB] = "Engage nozzle";
        context[SCREENTIP_CONTEXT_ALT_LMB] = "Empty";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:75
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(fire_extinguisher_reagent_sloshing_sound) && ((datum_reagents)reagents).total_volume > 0)
            playsound(this, fire_extinguisher_reagent_sloshing_sound, LIQUID_SLOSHING_SOUND_VOLUME, vary: TRUE, ignore_walls: FALSE);
        return dot;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:80
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)) && Dm.truthy(fire_extinguisher_reagent_sloshing_sound) && ((datum_reagents)reagents).total_volume > 0)
            playsound(this, fire_extinguisher_reagent_sloshing_sound, LIQUID_SLOSHING_SOUND_VOLUME, vary: TRUE, ignore_walls: FALSE);
    }

    // A secondary attack letting you wind up the extinguisher for a real wallopping to your targets head
    // tg: code/game/objects/items/tools/extinguisher.dm:86
    [DmParams("victim", "user", "params")]
    public override async DmTask<object> attack_secondary(mob_living victim = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // This only makes sense for heavier extinguishers
        if (w_class < WEIGHT_CLASS_BULKY)
            return SECONDARY_ATTACK_CALL_NORMAL;

        if (issilicon(user))
            return SECONDARY_ATTACK_CALL_NORMAL;

        if (!iscarbon(victim))
            return SECONDARY_ATTACK_CALL_NORMAL;

        mob_living_carbon wallopee = (mob_living_carbon)victim;
        obj_item_bodypart_head head_to_bash = wallopee.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;

        if (head_to_bash == null)
            return SECONDARY_ATTACK_CALL_NORMAL;

        string head_name = head_to_bash.name;

        if (Dm.truthy(fire_extinguisher_reagent_sloshing_sound) && ((datum_reagents)reagents).total_volume > 0)
            playsound(this, fire_extinguisher_reagent_sloshing_sound, LIQUID_SLOSHING_SOUND_VOLUME, vary: TRUE, ignore_walls: FALSE);

        log_combat(user, wallopee, Dm.text("prepared to use a bash attack with a [] against []", this, wallopee));

        wallopee.visible_message(span_danger(I18n.T("[] begins to raise [] above []'s [].", user, this, wallopee, head_name)), span_userdanger(I18n.T("[] begins to raise [], aiming to cave in your []!", user, this, head_name)));

        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: wallopee)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        wallopee.visible_message(span_danger(I18n.T("[] brings [] heavily down on []'s [].", user, this, wallopee, head_name)), span_userdanger(I18n.T("[] brings [] heavily down on your []!", user, this, head_name)));

        float min_wound = Dm.num(head_to_bash.get_wound_threshold_of_wound_type(WOUND_BLUNT, WOUND_SEVERITY_SEVERE, return_value_if_no_wound: 30, wound_source: this));
        float max_wound = Dm.num(head_to_bash.get_wound_threshold_of_wound_type(WOUND_BLUNT, WOUND_SEVERITY_CRITICAL, return_value_if_no_wound: 50, wound_source: this));

        await wallopee.apply_damage(Dm.num(force) * 3, damtype, head_to_bash, wound_bonus: rand(min_wound, max_wound + 10), attacking_item: this);
        await wallopee.emote("scream");
        log_combat(user, wallopee, Dm.text("used a bash attack with a [] against []", this, wallopee));
        await user.do_attack_animation(wallopee, used_item: this);

        if (Dm.truthy(fire_extinguisher_reagent_sloshing_sound) && ((datum_reagents)reagents).total_volume > 0)
            playsound(this, fire_extinguisher_reagent_sloshing_sound, LIQUID_SLOSHING_SOUND_VOLUME, vary: TRUE, ignore_walls: FALSE);

        playsound(source: this, soundin: hitsound, vol: get_clamped_volume(), vary: TRUE);

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:178
    public virtual async DmTask refill()
    {
        if (chem == null)
            return;
        create_reagents(max_water, AMOUNT_VISIBLE);
        await ((datum_reagents)reagents).add_reagent(chem, max_water);
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        if (Dm.truthy(tank_holder_icon_state))
            Waiting.NoSleep(AddComponent(typeof(datum_component_container_item_tank_holder), tank_holder_icon_state));
        if (Dm.truthy(starting_water))
            Waiting.NoSleep(refill());
        else if (chem != null)
            create_reagents(max_water, AMOUNT_VISIBLE);
        return dot;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:216  suicide_act: live-partial (suicides, M10).

    // tg: code/game/objects/items/tools/extinguisher.dm:228
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        safety = Dm.not(safety);
        icon_state = Dm.text("[][]", sprite_name, Dm.not(safety));
        balloon_alert(user, I18n.T("safety []", Dm.truthy(safety) ? I18n.T("on") : I18n.T("off")));
        return null;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:234
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user.combat_mode) && !Dm.truthy(safety)) //If we're on help intent and going to spray people, don't bash them.
            return FALSE;
        else
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:240
    public override async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(await AttemptRefill(attacked_atom, user)))
        {
            refilling = TRUE;
            return FALSE;
        }
        else
            return await base.attack_atom(attacked_atom, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:247
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("The safety is [].", Dm.truthy(safety) ? I18n.T("on") : I18n.T("off")));

        if (Dm.truthy(((datum_reagents)reagents).total_volume))
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to empty it.")));
        return dot;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:254
    public virtual async DmTask<object> AttemptRefill(atom target = null, mob user = null)
    {
        if (Dm.truthy(is_type_in_list(target, tanktypes)) && Dm.truthy(await target.Adjacent(user)))
        {
            datum_reagents holder = (datum_reagents)reagents;
            if (holder.total_volume == holder.maximum_volume)
            {
                balloon_alert(user, I18n.T("already full!"));
                return TRUE;
            }
            // Make sure we're refilling with the proper chem.
            if (!Dm.truthy(((datum_reagents)target.reagents).has_reagent(chem, check_subtypes: TRUE)))
            {
                balloon_alert(user, I18n.T("can't refill with this liquid!"));
                return TRUE;
            }
            obj_structure_reagent_dispensers W = target as obj_structure_reagent_dispensers; //will it work?
            object transferred = await ((datum_reagents)W.reagents).trans_to(this, max_water, transferred_by: user);
            if (Dm.gt(transferred, 0))
            {
                to_chat(user, span_notice(I18n.T("\\The [] has been refilled by [] units.", this, transferred)));
                playsound(loc, Dm.resource("sound/effects/refill.ogg"), 50, TRUE, -6);
                foreach (datum_reagent_water R in Dm.each<datum_reagent_water>(holder.reagent_list))
                    R.cooling_temperature = cooling_power;
            }
            else
                to_chat(user, span_warning(I18n.T("\\The [] is empty!", W)));

            return TRUE;
        }
        else
            return FALSE;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:277
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with.loc == user)
            return NONE;
        // Always skip interaction if it's a bag or table (that's not on fire)
        if (!Dm.truthy(Dm.bitand(interacting_with.resistance_flags, ON_FIRE)) && HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:285
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = null;
        if (Dm.truthy(refilling))
        {
            refilling = FALSE;
            return NONE;
        }
        if (Dm.truthy(safety))
            return NONE;

        datum_reagents holder = (datum_reagents)reagents;
        if (holder.total_volume < 1)
        {
            balloon_alert(user, I18n.T("it's empty!"));
            return dot;
        }

        if (world.time < last_use + 12)
            return dot;

        last_use = world.time;

        playsound(loc, Dm.resource("sound/effects/extinguish.ogg"), 75, TRUE, -3);

        float direction = get_dir(this, interacting_with);

        if (Dm.truthy(user.buckled) && isobj(user.buckled) && !Dm.truthy(user.buckled.anchored))
        {
            obj B = (obj)user.buckled;
            float movementdirection = REVERSE_DIR(direction);
            _addtimer(Dm.@new<datum_callback>(this, nameof(move_chair), B, movementdirection), 0.1f * SECONDS, file: "code/game/objects/items/tools/extinguisher.dm", line: 308);
        }
        else
            await user.newtonian_move(dir2angle(REVERSE_DIR(direction)));

        //Get all the turfs that can be shot at
        turf T = get_turf(interacting_with);
        turf T1 = get_step(T, turn(direction, 90));
        turf T2 = get_step(T, turn(direction, -90));
        DmList the_targets = Dm.list(T, T1, T2);
        if (Dm.truthy(precision))
        {
            turf T3 = get_step(T1, turn(direction, 90));
            turf T4 = get_step(T2, turn(direction, -90));
            the_targets.Add(T3, T4);
        }

        DmList water_particles = Dm.list();
        for (float a = 1; a <= 5; a++)
        {
            obj_effect_particle_effect_water_extinguisher water = Dm.@new<obj_effect_particle_effect_water_extinguisher>(get_turf(this));
            object my_target = pick(the_targets);
            water_particles[water] = my_target;
            // If precise, remove turf from targets so it won't be picked more than once
            if (Dm.truthy(precision))
                the_targets.Remove(my_target);
            datum_reagents water_reagents = Dm.@new<datum_reagents>(5);
            water.reagents = water_reagents;
            water_reagents.my_atom = water;
            await holder.trans_to(water, 1, transferred_by: user);
        }

        //Make em move dat ass, hun
        move_particles(water_particles);
        return ITEM_INTERACT_SKIP_TO_ATTACK; // You can smack while spraying
    }

    //Particle movement loop
    // tg: code/game/objects/items/tools/extinguisher.dm:340
    public virtual void move_particles(DmList particles = null)
    {
        float delay = 2;
        // Second loop: Get all the water particles and make them move to their target
        foreach (obj_effect_particle_effect_water_extinguisher water in Dm.each_cast<obj_effect_particle_effect_water_extinguisher>(particles))
            water.move_at(particles[water] as atom, delay, power);
    }

    //Chair movement loop
    // tg: code/game/objects/items/tools/extinguisher.dm:347
    public virtual void move_chair(obj buckled_object = null, object movementdirection = null)
    {
        datum_move_loop loop = GLOB.move_manager.move(buckled_object, movementdirection, 1, timeout: 9, flags: MOVEMENT_LOOP_START_INSTANT, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
        //This means the chair slowing down is dependant on the extinguisher existing, which is weird
        //Couldn't figure out a better way though
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(manage_chair_speed));
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:353
    [SignalHandler]
    public virtual void manage_chair_speed(datum_move_loop_move source = null)
    {
        // SIGNAL_HANDLER
        float lifetime = source.lifetime;
        if (lifetime >= 4 && lifetime <= 5)
            source.delay = 2;
        else if (lifetime >= 1 && lifetime <= 3)
            source.delay = 3;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:361
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(user.is_holding(this)))
        {
            to_chat(user, span_notice(I18n.T("You must be holding [] in your hands to do this!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        await EmptyExtinguisher(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:368
    public virtual async DmTask EmptyExtinguisher(mob user = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (loc == user && Dm.truthy(holder.total_volume))
        {
            await holder.expose(user.loc, TOUCH);
            await holder.clear_reagents();
            user.visible_message(span_notice(I18n.T("[] empties out [] onto the floor using the release valve.", user, this)), span_info(I18n.T("You quietly empty out [] using its release valve.", this)));
        }
    }

    // tg: code/game/objects/items/tools/extinguisher.dm:375
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_bodypart_arm_left_robot && tool is not obj_item_bodypart_arm_right_robot)
            return NONE;

        to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
        qdel(tool);
        obj_item_bot_assembly_firebot assembly = Dm.@new<obj_item_bot_assembly_firebot>(drop_location());
        object held_index = user.is_holding(this);
        qdel(this);
        if (Dm.truthy(held_index))
            await user.put_in_hand(assembly, held_index);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/tools/extinguisher.dm:154  (the vars are generated, D111)
[DmPath("/obj/item/extinguisher/crafted")]
public partial class obj_item_extinguisher_crafted : obj_item_extinguisher
{
    // tg: code/game/objects/items/tools/extinguisher.dm:173
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        safety = Dm.not(safety);
        icon_state = Dm.text("[][]", sprite_name, Dm.not(safety));
        to_chat(user, I18n.T("[].", Dm.truthy(safety) ? I18n.T("You remove the straw and put it on the side of the cool canister") : I18n.T("You insert the straw, readying it for use")));
        return null;
    }
}
