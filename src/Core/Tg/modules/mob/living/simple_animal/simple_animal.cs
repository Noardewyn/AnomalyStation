// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/simple_animal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/// Simple, mostly AI-controlled critters, such as pets, bots, and drones.
// tg: code/modules/mob/living/simple_animal/simple_animal.dm:2
public partial class mob_living_simple_animal : mob_living
{
    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:162
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ((DmList)GLOB.simple_animals[AIStatus]).Add(this);
        if (Dm.eq(gender, PLURAL))
            gender = pick(MALE, FEMALE) as string;
        if (!Dm.truthy(real_name))
            real_name = name;
        if (loc == null)
            stack_trace("Simple animal being instantiated in nullspace", "code/modules/mob/living/simple_animal/simple_animal.dm", 170);
        Waiting.NoSleep(update_simplemob_varspeed());
        ADD_TRAIT(this, TRAIT_NOFIRE_SPREAD, ROUNDSTART_TRAIT);
        if (Dm.truthy(length(weather_immunities)))
            add_traits(weather_immunities, ROUNDSTART_TRAIT);
        if (environment_smash >= ENVIRONMENT_SMASH_WALLS)
            Waiting.NoSleep(AddElement(typeof(datum_element_wall_smasher), Dm.named("strength_flag", environment_smash)));
        if (Dm.truthy(LAZYLEN(speak)))
            speak = string_list(speak) as DmList;
        if (Dm.truthy(LAZYLEN(speak_emote)))
            speak_emote = string_list(speak_emote) as DmList;
        if (Dm.truthy(LAZYLEN(emote_hear)))
            emote_hear = string_list(emote_hear) as DmList;
        if (Dm.truthy(LAZYLEN(emote_see)))
            emote_see = string_list(emote_see) as DmList;

        if (Dm.truthy(footstep_type))
            Waiting.NoSleep(AddElement(typeof(datum_element_footstep), footstep_type));
        if (unsuitable_cold_damage == null)
            unsuitable_cold_damage = unsuitable_atmos_damage;
        if (unsuitable_heat_damage == null)
            unsuitable_heat_damage = unsuitable_atmos_damage;

        ///We need to wait for SSair to be initialized before we can check atmos/temp requirements.
        // tg: if(PERFORM_ALL_TESTS(focus_only/atmos_and_temp_requirements) && ...): FALSE in production (rules 1.7)
        Waiting.NoSleep(init_atmos_temp_requirement(mapload));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:199
    [SignalHandler]
    public virtual void on_ssair_init(datum source = null)
    {
        UnregisterSignal(SSair, COMSIG_SUBSYSTEM_POST_INITIALIZE);
        Waiting.NoSleep(init_atmos_temp_requirement(TRUE));
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:204
    public virtual async DmTask init_atmos_temp_requirement(object mapload = null)
    {
        if (atmos_requirements != null && Dm.truthy(unsuitable_atmos_damage))
        {
            atmos_requirements = string_assoc_list(atmos_requirements) as DmList;
            await AddElement(typeof(datum_element_atmos_requirements), atmos_requirements, unsuitable_atmos_damage, mapload);
        }
        if ((Dm.truthy(unsuitable_cold_damage) || Dm.truthy(unsuitable_heat_damage)) && (minbodytemp > 0 || maxbodytemp < INFINITY))
            await AddElement(typeof(datum_element_body_temp_sensitive), minbodytemp, maxbodytemp, unsuitable_cold_damage, unsuitable_heat_damage, mapload);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:211
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (Dm.num(staminaloss) > 0)
            Waiting.NoSleep(adjust_stamina_loss(-stamina_recovery * Dm.num(seconds_per_tick), FALSE, TRUE));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:216
    public override object Destroy(float? force = null)
    {
        ((DmList)GLOB.simple_animals[AIStatus]).Remove(this);
        SSnpcpool?.currentrun.Remove(this); // a harness world without the Master has no SSnpcpool (rules 10.6)

        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (stat == DEAD)
        {
            if (HAS_MIND_TRAIT(user, TRAIT_NAIVE))
                Dm.append(ref dot, span_deadsay(I18n.T("Upon closer examination, [] appear[] to be asleep.", p_they(), p_s())));
            else
                Dm.append(ref dot, span_deadsay(I18n.T("Upon closer examination, [] appear[] to be dead.", p_they(), p_s())));
        }
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:230
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD)
        {
            if (health <= 0)
                Waiting.NoSleep(death());
            else
                Waiting.NoSleep(set_stat(STABLE));
        }
        med_hud_set_status();
    }

    /**
     * Updates the simple mob's stamina loss.
     *
     * Updates the speed and staminaloss of a given simplemob.
     * Reduces the stamina loss by stamina_recovery
     */
    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:246
    public override async DmTask update_stamina()
    {
        if (Dm.num(GET_PHYSIOLOGY(this, STAMINA)) <= 0) //we shouldn't reset our speed to its initial value if we don't need to, as that can mess with things like mulebot motor wires
            return;
        await set_varspeed(Dm.num(Dm.initial(this, (mob_living_simple_animal p) => p.speed)) + (Dm.num(staminaloss) * 0.06f));
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:251
    public virtual object handle_automated_action()
    {
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:255
    public virtual object handle_automated_movement()
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(handle_automated_movement__body(dot), dot);
    }

    private async DmTask<object> handle_automated_movement__body(DotBox<object> dot)
    {
        if (Dm.truthy(stop_automated_movement) || !Dm.truthy(wander))
            return dot.Value;
        if (!isturf(loc) && !Dm.truthy(allow_movement_on_non_turfs))
            return dot.Value;
        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_MOVE))) //This is so it only moves if it's not inside a closet, gentics machine, etc.
            return TRUE;

        turns_since_move++;
        if (turns_since_move < turns_per_move)
            return TRUE;
        if (Dm.truthy(stop_automated_movement_when_pulled) && pulledby != null) //Some animals don't move when pulled
            return TRUE;
        float anydir = Dm.num(pick(GLOB.cardinals));
        if (Dm.truthy(await Process_Spacemove(anydir)))
        {
            await Move(get_step(this, anydir), anydir);
            turns_since_move = 0;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:275
    public virtual void handle_automated_speech(object @override = null)
    {
        Waiting.WaitforFalse(handle_automated_speech__body(@override));
    }

    private async DmTask handle_automated_speech__body(object @override)
    {
        if (Dm.truthy(speak_chance))
        {
            if (prob(speak_chance) || Dm.truthy(@override))
            {
                if (Dm.truthy(speak?.len))
                {
                    if (Dm.truthy(emote_hear?.len) || Dm.truthy(emote_see?.len))
                    {
                        float length = speak.len;
                        if (Dm.truthy(emote_hear?.len))
                            length += emote_hear.len;
                        if (Dm.truthy(emote_see?.len))
                            length += emote_see.len;
                        float randomValue = rand(1, length);
                        if (randomValue <= speak.len)
                            await say(pick(speak), forced: "automated speech");
                        else
                        {
                            randomValue -= speak.len;
                            if (emote_see != null && randomValue <= emote_see.len)
                                manual_emote(pick(emote_see));
                            else
                                manual_emote(pick(emote_hear));
                        }
                    }
                    else
                        await say(pick(speak), forced: "automated speech");
                }
                else
                {
                    if (!Dm.truthy(emote_hear?.len) && Dm.truthy(emote_see?.len))
                        manual_emote(pick(emote_see));
                    if (Dm.truthy(emote_hear?.len) && !Dm.truthy(emote_see?.len))
                        manual_emote(pick(emote_hear));
                    if (Dm.truthy(emote_hear?.len) && Dm.truthy(emote_see?.len))
                    {
                        float length = emote_hear.len + emote_see.len;
                        float pick__1 = rand(1, length);
                        if (pick__1 <= emote_see.len)
                            manual_emote(pick(emote_see));
                        else
                            manual_emote(pick(emote_hear));
                    }
                }
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:310
    public override async DmTask handle_environment(datum_gas_mixture environment = null, object seconds_per_tick = null, params object[] __extra)
    {
        atom A = loc;
        if (isturf(A))
        {
            float areatemp = Dm.num(await get_temperature(environment));
            float temp_delta = areatemp - Dm.num(bodytemperature);
            if (abs(temp_delta) > 5)
            {
                if (temp_delta < 0)
                {
                    if (!Dm.truthy(on_fire))
                        adjust_bodytemperature(clamp(temp_delta * Dm.num(seconds_per_tick) / temperature_normalization_speed, temp_delta, 0));
                }
                else
                    adjust_bodytemperature(clamp(temp_delta * Dm.num(seconds_per_tick) / temperature_normalization_speed, 0, temp_delta));
            }
        }
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:322
    public override async DmTask gib(float? drop_bitflags = null)
    {
        if (Dm.truthy(butcher_results) || Dm.truthy(guaranteed_butcher_results))
        {
            DmList butcher = Dm.list();
            if (Dm.truthy(butcher_results))
                Dm.append(ref butcher, butcher_results);
            if (Dm.truthy(guaranteed_butcher_results))
                Dm.append(ref butcher, guaranteed_butcher_results);
            atom Tsec = drop_location() as atom;
            foreach (object path in Dm.each(butcher))
            {
                for (float i = 1, i__end = Dm.num(butcher[path]); i <= i__end; i++)
                    Dm.@new((Type)path, Tsec);
            }
        }
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:335
    public override void gib_animation()
    {
        if (Dm.truthy(icon_gib))
            Dm.@new<obj_effect_temp_visual_gib_animation_animal>(loc, icon_gib);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:340
    public override object get_default_say_verb()
    {
        return Dm.truthy(length(speak_emote)) ? pick(speak_emote) : base.get_default_say_verb();
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:343
    public virtual async DmTask set_varspeed(object var_value = null)
    {
        speed = Dm.num(var_value);
        await update_simplemob_varspeed();
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:347
    public virtual async DmTask update_simplemob_varspeed()
    {
        if (speed == 0)
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_simplemob_varspeed));
        await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_simplemob_varspeed), multiplicative_slowdown: speed);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:352
    public override object get_status_tab_items()
    {
        object dot = base.get_status_tab_items();
        Dm.append(ref dot, I18n.T("Health: []%", round((health / maxHealth) * 100)));
        Dm.append(ref dot, I18n.T("Combat Mode: []", Dm.truthy(combat_mode) ? "On" : "Off"));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:357
    public virtual async DmTask drop_loot(object drop_loc = null)
    {
        if (!Dm.truthy(length(loot)))
            return;
        foreach (object i in Dm.each(loot))
            Dm.@new((Type)i, drop_loc);
        loot.Cut();
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:364
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object drop_loc = drop_location();
        if (Dm.truthy(del_on_death))
        {
            await base.death(gibbed, __extra);
            await drop_loot(drop_loc);
            //Prevent infinite loops if the mob Destroy() is overridden in such
            //a manner as to cause a call to death() again //Pain
            del_on_death = FALSE;
            qdel(this);
            return null;
        }

        health = 0;
        icon_state = icon_dead;
        if (Dm.truthy(flip_on_death))
            transform = ((matrix)transform).Turn(180);
        ADD_TRAIT(this, TRAIT_UNDENSE, BASIC_MOB_DEATH_TRAIT);
        object dot = await base.death(gibbed, __extra);
        await drop_loot(drop_loc);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:383
    public virtual object CanAttack(object the_target = null)
    {
        if (!isatom(the_target)) // no
        {
            stack_trace(Dm.text("Invalid target in CanAttack(): []", the_target), "code/modules/mob/living/simple_animal/simple_animal.dm", 385);
            return FALSE;
        }
        if (see_invisible < ((atom)the_target).invisibility)
            return FALSE;
        if (ismob(the_target))
        {
            mob mob = the_target as mob;
            if (HAS_TRAIT(mob, TRAIT_GODMODE))
                return FALSE;
        }
        if (isliving(the_target))
        {
            mob_living L = the_target as mob_living;
            if (IS_UNCONSCIOUS_OR_CRIT(L))
                return FALSE;
        }
        if (ismecha(the_target))
        {
            obj_vehicle_sealed_mecha M = the_target as obj_vehicle_sealed_mecha;
            if (Dm.truthy(LAZYLEN(M.occupants)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:403
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;
        icon_state = icon_living;
        REMOVE_TRAIT(this, TRAIT_UNDENSE, BASIC_MOB_DEATH_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:410
    public virtual async DmTask<object> make_babies() // <3 <3 <3
    {
        if (!Dm.eq(gender, FEMALE) || IS_UNCONSCIOUS_OR_CRIT(this) || next_scan_time > world.time || childtype == null || !Dm.truthy(animal_species) || !Dm.truthy(SSticker.IsRoundInProgress()))
            return null;
        next_scan_time = world.time + 400;
        bool alone = true;
        mob_living_simple_animal partner = null;
        float children = 0;
        foreach (mob M in Dm.each<mob>(view(7, this)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(M)) //Check if it's conscious FIRST.
                continue;
            object is_child = is_type_in_list(M, childtype);
            if (Dm.truthy(is_child)) //Check for children SECOND.
                children++;
            else if (istype(M, (Type)animal_species))
            {
                if (Dm.truthy(M.ckey))
                    continue;
                else if (!Dm.truthy(is_child) && Dm.eq(M.gender, MALE) && !Dm.truthy(Dm.bitand(M.flags_1, HOLOGRAM_1))) //Better safe than sorry ;_;
                    partner = M as mob_living_simple_animal;
            }
            else if (isliving(M) && !Dm.truthy(faction_check_atom(M))) //shyness check. we're not shy in front of things that share a faction with us.
                return null; //we never mate when not alone, so just abort early
        }

        if (alone && partner != null && children < 3)
        {
            object childspawn = pick_weight(childtype);
            turf target = get_turf(loc);
            if (target != null)
                return Dm.@new((Type)childspawn, target);
        }
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:438
    public override void update_resting()
    {
        if (Dm.truthy(resting))
            ADD_TRAIT(this, TRAIT_IMMOBILIZED, RESTING_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_IMMOBILIZED, RESTING_TRAIT);
        base.update_resting();
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:445
    public virtual void sentience_act() //Called when a simple animal gains sentience via gold slime potion
    {
        toggle_ai(AI_OFF); // To prevent any weirdness.
        can_have_ai = FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:449
    public override async DmTask<object> put_in_hands(obj_item I = null, float? del_on_fail = null, float? merge_stacks = null, float? forced = null, float? ignore_animation = null, float? visuals_only = null)
    {
        del_on_fail ??= FALSE;
        merge_stacks ??= TRUE;
        ignore_animation ??= TRUE;
        object dot = await base.put_in_hands(I, del_on_fail, merge_stacks, forced, ignore_animation, visuals_only);
        await update_held_items();
        return dot;
    }

    //ANIMAL RIDING

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:455
    public override async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        if (Dm.truthy(user.incapacitated))
            return null;
        foreach (atom_movable A in Dm.each<atom_movable>(get_turf(this)))
        {
            if (A != this && A != M && Dm.truthy(A.density))
                return null;
        }

        return await base.user_buckle_mob(M, user, check_loc);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:464
    public virtual void toggle_ai(object togglestatus = null)
    {
        if (QDELETED(this))
            return;
        if (!Dm.truthy(can_have_ai) && !Dm.eq(togglestatus, AI_OFF))
            return;
        if (!Dm.eq(AIStatus, togglestatus))
        {
            if (Dm.num(togglestatus) > 0 && Dm.num(togglestatus) < 5)
            {
                ((DmList)GLOB.simple_animals[AIStatus]).Remove(this);
                ((DmList)GLOB.simple_animals[togglestatus]).Add(this);
                AIStatus = Dm.num(togglestatus);
            }
            else
                stack_trace(Dm.text("Something attempted to set simple animals AI to an invalid state: []", togglestatus), "code/modules/mob/living/simple_animal/simple_animal.dm", 475);
        }
    }

    ///This proc is used for adding the swabbale element to mobs so that they are able to be biopsied and making sure holographic and butter-based creatures don't yield viable cells samples.
    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:478
    public virtual async DmTask add_cell_sample()
    {
        return;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:481
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(user.incapacitated))
            return null;
        return relaydrive(user, direction);
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:486
    public override async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        mode ??= ANARCHY_MODE;
        cooldown ??= 12 * SECONDS;
        object dot = await AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), mode, Dm.list(), cooldown, Dm.@new<datum_callback>(this, nameof(stop_deadchat_plays)));

        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        stop_automated_movement = TRUE;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:494
    public virtual void stop_deadchat_plays()
    {
        stop_automated_movement = FALSE;
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:497
    public virtual object Goto(object target = null, object delay = null, object minimum_distance = null)
    {
        if (Dm.truthy(prevent_goto_movement))
            return FALSE;
        GLOB.move_manager.move_to(this, target, minimum_distance, delay);
        return TRUE;
    }

    //Makes this mob hunt the prey, be it living or an object. Will kill living creatures, and delete objects.
    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:504
    public virtual async DmTask hunt(object hunted = null)
    {
        if (this == hunted) //Make sure it doesn't eat itself. While not likely to ever happen, might as well check just in case.
            return;
        stop_automated_movement = FALSE;
        if (!isturf(loc)) // Are we on a proper turf?
            return;
        if (IS_UNCONSCIOUS_OR_CRIT(this) || Dm.truthy(resting) || buckled != null) // Are we conscious, upright, and not buckled?
            return;
        if (!(emote_cooldown <= world.time)) // COOLDOWN_FINISHED(src, emote_cooldown), inline (rules 11.1 (a))
            return;
        if (!Dm.truthy(await Adjacent(hunted as atom)) && Dm.truthy(Goto(hunted, 3, 0)))
        {
            stop_automated_movement = TRUE;
            if (Dm.truthy(await Adjacent(hunted as atom)))
                await hunt(hunted); // In case it gets next to the target immediately, skip the scan timer and kill it.
            return;
        }
        if (isliving(hunted)) // Are we hunting a living mob?
        {
            mob_living prey = hunted as mob_living;
            if (Dm.truthy(inept_hunter)) // Make your hunter inept to have them unable to catch their prey.
            {
                visible_message(span_warning(I18n.T("[] chases [] around, to no avail!", this, prey)));
                await step(prey, Dm.num(pick(GLOB.cardinals)));
                emote_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START(src, emote_cooldown, 1 MINUTES)
                return;
            }
            if (!IS_UNCONSCIOUS_OR_CRIT(prey))
            {
                manual_emote(I18n.T("chomps []!", prey));
                await prey.death();
                prey = null;
                emote_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START(src, emote_cooldown, 1 MINUTES)
                return;
            }
        }
        else // We're hunting an object, and should delete it instead of killing it. Mostly useful for decal bugs like ants or spider webs.
        {
            manual_emote(I18n.T("chomps []!", hunted));
            qdel(hunted);
            hunted = null;
            emote_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START(src, emote_cooldown, 1 MINUTES)
            return;
        }
    }

    // tg: code/modules/mob/living/simple_animal/simple_animal.dm:539
    public override object compare_sentience_type(object compare_type = null)
    {
        return Dm.num(Dm.eq(sentience_type, compare_type));
    }
}
