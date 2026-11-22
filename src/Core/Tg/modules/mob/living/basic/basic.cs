// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/basic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but vv_edit_var (admin tooling, a row) (gaps package C1; the examine since X3). The var block is generated.

// tg: code/modules/mob/living/basic/basic.dm:3
public partial class mob_living_basic : mob_living
{
    // tg: code/modules/mob/living/basic/basic.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.eq(gender, PLURAL) && Dm.truthy(randomize_gender))
            gender = pick(MALE, FEMALE) as string;

        if (!Dm.truthy(real_name))
            real_name = name;

        if (loc == null)
            stack_trace("Basic mob being instantiated in nullspace", "code/modules/mob/living/basic/basic.dm", 118);

        Waiting.NoSleep(update_basic_mob_varspeed());
        apply_target_randomisation();
        make_stamina_slowable();

        if (Dm.truthy(LAZYLEN(speak_emote)))
            speak_emote = string_list(speak_emote) as DmList;

        ///We need to wait for SSair to be initialized before we can check atmos/temp requirements.
        // tg: if(PERFORM_ALL_TESTS(focus_only/atmos_and_temp_requirements) && ...): FALSE in production (rules 1.7)
        apply_atmos_requirements(mapload);
        apply_temperature_requirements(mapload);
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:133
    [SignalHandler]
    public virtual void on_ssair_init(datum source = null)
    {
        UnregisterSignal(SSair, COMSIG_SUBSYSTEM_POST_INITIALIZE);
        apply_atmos_requirements(TRUE);
        apply_temperature_requirements(TRUE);
    }

    /// Ensures this mob can take atmospheric damage if it's supposed to
    // tg: code/modules/mob/living/basic/basic.dm:140
    public virtual void apply_atmos_requirements(object mapload = null)
    {
        if (unsuitable_atmos_damage == 0 || habitable_atmos == null)
            return;
        //String assoc list returns a cached list, so this is like a static list to pass into the element below.
        habitable_atmos = string_assoc_list(habitable_atmos) as DmList;
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_requirements), habitable_atmos, unsuitable_atmos_damage, mapload));
    }

    /// Ensures this mob can take temperature damage if it's supposed to
    // tg: code/modules/mob/living/basic/basic.dm:148
    public virtual void apply_temperature_requirements(object mapload = null)
    {
        if ((Dm.num(unsuitable_cold_damage) == 0 && Dm.num(unsuitable_heat_damage) == 0) || (minimum_survivable_temperature <= 0 && maximum_survivable_temperature >= INFINITY))
            return;
        Waiting.NoSleep(AddElement(typeof(datum_element_body_temp_sensitive), minimum_survivable_temperature, maximum_survivable_temperature, unsuitable_cold_damage, unsuitable_heat_damage, mapload));
    }

    /// Ensures that this mob can be slowed from taking stamina damage
    // tg: code/modules/mob/living/basic/basic.dm:154
    public virtual void make_stamina_slowable()
    {
        if (max_stamina == BASIC_MOB_STAMINA_MATCH_HEALTH)
            max_stamina = maxHealth;
        if (Dm.num(GET_PHYSIOLOGY(this, STAMINA)) <= 0 || max_stamina <= 0 || max_stamina_slowdown <= 0)
            return;
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_stamina_slowdown), Dm.named("minium_stamina_threshold", max_stamina / 3), Dm.named("maximum_stamina", max_stamina), Dm.named("maximum_slowdown", max_stamina_slowdown)));
    }

    // tg: code/modules/mob/living/basic/basic.dm:161
    public virtual void apply_target_randomisation()
    {
        if (Dm.truthy(Dm.bitand(basic_mob_flags, PRECISE_ATTACK_ZONES)))
            return;
        Waiting.NoSleep(AddElement(typeof(datum_element_attack_zone_randomiser)));
    }

    // tg: code/modules/mob/living/basic/basic.dm:166
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (Dm.num(staminaloss) > 0)
            Waiting.NoSleep(adjust_stamina_loss(-stamina_recovery * Dm.num(seconds_per_tick)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:171
    public override object get_default_say_verb()
    {
        return Dm.truthy(length(speak_emote)) ? pick(speak_emote) : base.get_default_say_verb();
    }

    // tg: code/modules/mob/living/basic/basic.dm:174
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        if (Dm.truthy(Dm.bitand(basic_mob_flags, DEL_ON_DEATH)))
        {
            ghostize(can_reenter_corpse: FALSE);
            qdel(this);
        }
        else
        {
            health = 0;
            look_dead();
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:182
    public override async DmTask gib(float? drop_bitflags = null)
    {
        if (Dm.truthy(butcher_results) || Dm.truthy(guaranteed_butcher_results))
        {
            DmList butcher_loot = Dm.list();
            if (Dm.truthy(butcher_results))
                Dm.append(ref butcher_loot, butcher_results);
            if (Dm.truthy(guaranteed_butcher_results))
                Dm.append(ref butcher_loot, guaranteed_butcher_results);
            atom loot_destination = drop_location() as atom;
            foreach (object path in Dm.each(butcher_loot))
            {
                for (float i = 1, i__end = Dm.num(butcher_loot[path]); i <= i__end; i++)
                    Dm.@new((Type)path, loot_destination);
            }
        }
        await base.gib(drop_bitflags);
    }

    /**
     * Apply the appearance and properties this mob has when it dies
     * This is called by the mob pretending to be dead too so don't put loot drops in here or something
     */
    // tg: code/modules/mob/living/basic/basic.dm:198
    public virtual void look_dead()
    {
        icon_state = icon_dead;
        if (Dm.truthy(Dm.bitand(basic_mob_flags, FLIP_ON_DEATH)))
            transform = ((matrix)transform).Turn(180);
        if (!Dm.truthy(Dm.bitand(basic_mob_flags, REMAIN_DENSE_WHILE_DEAD)))
            ADD_TRAIT(this, TRAIT_UNDENSE, BASIC_MOB_DEATH_TRAIT);
        if (Dm.truthy(_listen_lookup?[COMSIG_BASICMOB_LOOK_DEAD]))
            _SendSignal(COMSIG_BASICMOB_LOOK_DEAD, Dm.list(this));
    }

    // tg: code/modules/mob/living/basic/basic.dm:206
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;
        look_alive();
        return dot;
    }

    /// Apply the appearance and properties this mob has when it is alive
    // tg: code/modules/mob/living/basic/basic.dm:213
    public virtual void look_alive()
    {
        icon_state = icon_living;
        if (Dm.truthy(Dm.bitand(basic_mob_flags, FLIP_ON_DEATH)))
            transform = ((matrix)transform).Turn(180);
        if (!Dm.truthy(Dm.bitand(basic_mob_flags, REMAIN_DENSE_WHILE_DEAD)))
            REMOVE_TRAIT(this, TRAIT_UNDENSE, BASIC_MOB_DEATH_TRAIT);
        if (Dm.truthy(_listen_lookup?[COMSIG_BASICMOB_LOOK_ALIVE]))
            _SendSignal(COMSIG_BASICMOB_LOOK_ALIVE, Dm.list(this));
    }

    // tg: code/modules/mob/living/basic/basic.dm:223
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (stat != DEAD)
            return dot;
        Dm.append(ref dot, span_deadsay(I18n.T("Upon closer examination, [] appear[] to be [].", p_they(), p_s(), HAS_MIND_TRAIT(user, TRAIT_NAIVE) ? I18n.T("asleep") : I18n.T("dead"))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:229
    public virtual async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        ignore_cooldown ??= FALSE;
        object early_melee_result = await early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(early_melee_result)) //Truthy value means we want to end the chain
        {
            if (!Dm.truthy(ignore_cooldown) && Dm.eq(early_melee_result, BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN))
                changeNext_move(melee_attack_cooldown);
            return FALSE;
        }
        object result = await target.attack_basic_mob(this, modifiers);
        if (Dm.truthy(_listen_lookup?[COMSIG_HOSTILE_POST_ATTACKINGTARGET]))
            _SendSignal(COMSIG_HOSTILE_POST_ATTACKINGTARGET, Dm.list(this, target, result));
        if (Dm.truthy(result) && !Dm.truthy(ignore_cooldown)) //Only set cooldown if the attack achieved something, which is the case when the value is true-ey. This could definitely be done better but is probably easier once living/simple is gone and we no longer use attack_animal.
            changeNext_move(melee_attack_cooldown);
        return result;
    }

    // tg: code/modules/mob/living/basic/basic.dm:241
    public virtual async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        ignore_cooldown ??= FALSE;
        face_atom(target);
        if (Dm.truthy(_listen_lookup?[COMSIG_HOSTILE_PRE_ATTACKINGTARGET]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, Dm.list(this, target, await Adjacent(target), modifiers)), COMPONENT_HOSTILE_NO_ATTACK)))
            return BASIC_MOB_END_ATTACK_CHAIN; //but more importantly return before attack_animal called
        return BASIC_MOB_CONTINUE_ATTACK_CHAIN;
    }

    // tg: code/modules/mob/living/basic/basic.dm:247
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        await melee_attack(attack_target, modifiers);
        return null;
    }

    // tg: code/modules/mob/living/basic/basic.dm:276
    public virtual async DmTask set_varspeed(object var_value = null)
    {
        speed = Dm.num(var_value);
        await update_basic_mob_varspeed();
    }

    // tg: code/modules/mob/living/basic/basic.dm:280
    public virtual async DmTask update_basic_mob_varspeed()
    {
        if (speed == 0)
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_simplemob_varspeed));
        await add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_simplemob_varspeed), multiplicative_slowdown: speed);
        if (Dm.truthy(_listen_lookup?[POST_BASIC_MOB_UPDATE_VARSPEED]))
            _SendSignal(POST_BASIC_MOB_UPDATE_VARSPEED, Dm.list(this));
    }

    // tg: code/modules/mob/living/basic/basic.dm:286
    public override object update_movespeed()
    {
        object dot = base.update_movespeed();
        if (Dm.num(cached_multiplicative_slowdown) > END_GLIDE_SPEED)
            ADD_TRAIT(this, TRAIT_NO_GLIDE, SPEED_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_NO_GLIDE, SPEED_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:293
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(user.incapacitated))
            return null;
        return relaydrive(user, direction);
    }

    // tg: code/modules/mob/living/basic/basic.dm:298
    public override object compare_sentience_type(object compare_type = null)
    {
        return Dm.num(Dm.eq(sentience_type, compare_type));
    }

    // tg: code/modules/mob/living/basic/basic.dm:301
    public override async DmTask on_fire_stack(object seconds_per_tick = null, datum_status_effect_fire_handler_fire_stacks fire_handler = null)
    {
        adjust_bodytemperature((maximum_survivable_temperature + (Dm.num(fire_handler.stacks) * 12)) * 0.5f * Dm.num(seconds_per_tick));
    }

    // tg: code/modules/mob/living/basic/basic.dm:304
    public override object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        return make_generic_fire_overlay();
    }

    // tg: code/modules/mob/living/basic/basic.dm:307
    public override async DmTask<object> put_in_hands(obj_item I = null, float? del_on_fail = null, float? merge_stacks = null, float? forced = null, float? ignore_animation = null, float? visuals_only = null)
    {
        del_on_fail ??= FALSE;
        merge_stacks ??= TRUE;
        ignore_animation ??= TRUE;
        object dot = await base.put_in_hands(I, del_on_fail, merge_stacks, forced, ignore_animation, visuals_only);
        if (Dm.truthy(dot))
            await update_held_items();
        return dot;
    }

    // tg: code/modules/mob/living/basic/basic.dm:312
    public override object get_body_temp_heat_damage_limit()
    {
        return maximum_survivable_temperature;
    }

    // tg: code/modules/mob/living/basic/basic.dm:315
    public override object get_body_temp_cold_damage_limit()
    {
        return minimum_survivable_temperature;
    }

    // tg: code/modules/mob/living/basic/basic.dm:318
    public virtual async DmTask hop_on_nearby_turf()
    {
        float dir = Dm.num(pick(GLOB.cardinals));
        await Move(get_step(this, dir), dir);
        animate(this, Dm.named("pixel_y", 18), Dm.named("time", 0.4f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)));
        animate(Dm.named("pixel_y", -18), Dm.named("time", 0.4f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)));
    }

    ///ovverride to add mob specific cytology mutation effects, returns TRUE if we added a mob specific mutation
    // tg: code/modules/mob/living/basic/basic.dm:324
    public virtual object mutate()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_BASICMOB_MUTATED]) && Dm.truthy(Dm.bitand(_SendSignal(COMSIG_BASICMOB_MUTATED, Dm.list(this)), MUTATED_NO_FURTHER_MUTATIONS)))
        {
            //Tells the vat our mob has been mutated by another source and we don't want to add potentially incompatible mutations such as shiny mutation.
            return TRUE;
        }
        else
            return FALSE;
    }
}
