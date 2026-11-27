// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/thing/thing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mob/living/basic/boss/thing/thing.dm:3
public partial class mob_living_basic_boss_thing : mob_living_basic_boss
{
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:1  #define PHASEREGEN_FILTER "healing_glow" (file-local)
    internal const string PHASEREGEN_FILTER = "healing_glow";
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:2  #define RUIN_QUEUE "the_thing_depleter" (file-local)
    internal const string RUIN_QUEUE = "the_thing_depleter";

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:57  var/static/list/innate_actions (a proc-level static, rules 10.6)
    private static ref DmList Initialize__innate_actions => ref TgState.Current.mob_living_basic_boss_thing__Initialize__innate_actions;

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_keycard_thing_boss), FALSE));

        grant_actions_by_list(Initialize__innate_actions);
        Waiting.NoSleep(AddElement(typeof(datum_element_relay_attackers))); // used to immediately aggro if shot from outside aggro range
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(immediate_aggro));
        maploaded = Dm.num(mapload);
        if (!Dm.truthy(maploaded))
            return dot;
        spawn_loc = loc as turf;
        RegisterSignal(this, COMSIG_AI_BLACKBOARD_KEY_SET(BB_CURRENT_TARGET), nameof(target_gained));
        RegisterSignal(this, COMSIG_AI_BLACKBOARD_KEY_CLEARED(BB_CURRENT_TARGET), nameof(target_lost));
        SSqueuelinks.add_to_queue(this, RUIN_QUEUE, 0);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:79
    public override void LateInitialize(params object[] __extra)
    {
        SSqueuelinks.pop_link(RUIN_QUEUE);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:82
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            icon_state = "dead";
            return dot;
        }
        icon_state = Dm.text("p[]", phase);
        icon_living = icon_state;
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:90
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        if (phase == 3 || IS_UNCONSCIOUS_OR_CRIT(this) || Dm.num(amount) <= 0)
        {
            await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
            return;
        }
        object old_update = updating_health;
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type); //This call is necessary so we convert the other damage types to BRUTE and remove the need of using switch cases for each damage type
        bruteloss = min(Dm.num(bruteloss), (Dm.num(maxHealth) / 3) * phase); //Caps the damage that the mob receives to the health loss required to progress to the next phase
        if (Dm.truthy(old_update))
            await updatehealth();
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:100
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        if (phase < 3 && !Dm.truthy(stat) && Dm.num(bruteloss) >= (Dm.num(maxHealth) / 3) * phase)
            await phase_health_depleted();
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:105
    public virtual async DmTask phase_health_depleted()
    {
        if (Dm.truthy(phase_invulnerability_timer))
            return; //wtf?

        if (!Dm.truthy(maploaded) || client != null)
        {
            await phase_successfully_depleted();
            return;
        }
        if (client == null && istype(get_area(this), typeof(area_station))) //retreat to station if AI controlled
        {
            await return_to_spawnloc();
            return;
        }
        add_traits(Dm.list(TRAIT_GODMODE, TRAIT_IMMOBILIZED), MEGAFAUNA_TRAIT);
        balloon_alert_to_viewers(I18n.T("invulnerable! overload the machines!"));
        visible_message(span_danger(I18n.T("[] drops to the ground staggered, unable to keep up with injuries!", this)));
        phase_invulnerability_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(phase_too_slow)), phase_invul_time, Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), file: "code/modules/mob/living/basic/boss/thing/thing.dm", line: 118);
        add_filter(PHASEREGEN_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_PALE_GREEN), Dm.kv("alpha", 0), Dm.kv("size", 1)));
        object filter = get_filter(PHASEREGEN_FILTER);
        animate(filter, Dm.named("alpha", 200), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1));
        animate(Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
        SEND_SIGNAL(COMSIG_MEGAFAUNA_THETHING_PHASEUPDATED, Dm.list(this));
    }

    /// Delete our return timer when we gain a target if we started premapped
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:125
    [SignalHandler]
    public virtual void target_gained(datum source = null)
    {
        if (!Dm.truthy(return_timer))
            return;
        deltimer(return_timer);
        return_timer = null;
    }

    /// If we started premapped, and we lost our target, start a 3 minute timer to return to spawn turf unless we gain aggro again
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:133
    [SignalHandler]
    public virtual void target_lost(datum source = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this) || client != null || loc == spawn_loc || Dm.truthy(return_timer))
            return;
        return_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(return_to_spawn_check)), 3 * MINUTES, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/modules/mob/living/basic/boss/thing/thing.dm", line: 137);
    }

    /// Return us to our spawn loc (ruin boss only) if we are alive and have an ai controller and our loc isnt the spawn loc
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:140
    public virtual async DmTask return_to_spawn_check()
    {
        if (ai_controller == null || QDELETED(this) || loc == spawn_loc || IS_UNCONSCIOUS_OR_CRIT(this) || client != null)
            return;
        await return_to_spawnloc();
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:145
    public virtual async DmTask return_to_spawnloc()
    {
        if (spawn_loc == null)
            throw Dm.CRASH("The Thing tried to return to spawn_loc but it was null! This shouldnt happen", "code/modules/mob/living/basic/boss/thing/thing.dm", 147);
        foreach (turf_open target in Dm.each<turf_open>(RANGE_TURFS(1, loc)))
            Dm.@new<obj_effect_temp_visual_mook_dust>(target);
        playsound(loc, Dm.resource("sound/effects/meteorimpact.ogg"), 40, TRUE);
        visible_message(span_danger(I18n.T("[] retreats through the ground back to where it came from!", this)));
        await forceMove(spawn_loc);
    }

    /// The Thing is successfully hit by incendiary fire while downed by damage (alternatively takes too much damage if not ruin spawned)
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:155
    public virtual async DmTask phase_successfully_depleted()
    {
        playsound(this, Dm.resource("sound/effects/pop_expl.ogg"), 65);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_THETHING_NOAOE, FALSE);
        remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_IMMOBILIZED), MEGAFAUNA_TRAIT);
        deltimer(phase_invulnerability_timer);
        phase_invulnerability_timer = null;
        if (phase < 3) //after phase 3 we literally just die
        {
            phase++;
            await emote("scream");
        }
        update_appearance();
        object filter = get_filter(PHASEREGEN_FILTER);
        if (filter != null)
        {
            animate(filter);
            remove_filter(PHASEREGEN_FILTER);
        }
        SEND_SIGNAL(COMSIG_MEGAFAUNA_THETHING_PHASEUPDATED, Dm.list(this));
        Dm.@new<obj_effect_gibspawner_human_bodypartless>(loc);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:173
    public virtual async DmTask phase_too_slow()
    {
        phase_invulnerability_timer = null;
        remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_IMMOBILIZED), MEGAFAUNA_TRAIT);
        balloon_alert_to_viewers(I18n.T("recovers!"));
        visible_message(span_danger(I18n.T("[] recovers from the damage! Too slow!", this)));
        await adjust_brute_loss(-(Dm.num(maxHealth) / 3) * 0.5f); //half of a phase (which is a third of maxhealth)
        object filter = get_filter(PHASEREGEN_FILTER);
        if (filter != null)
        {
            animate(filter);
            remove_filter(PHASEREGEN_FILTER);
        }
        await emote("roar");
        SEND_SIGNAL(COMSIG_MEGAFAUNA_THETHING_PHASEUPDATED, Dm.list(this));
    }

    /// Immediately set out blackboard target key (if empty) to whoever attacks us; this is primarily because it has a lowered aggro range and a high sight range
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:188
    [SignalHandler]
    public virtual void immediate_aggro(datum source = null, mob attacker = null, object flags = null)
    {
        if (ai_controller == null || IS_UNCONSCIOUS_OR_CRIT(this) || attacker == null || Dm.truthy((ai_controller as datum_ai_controller).blackboard_key_exists(BB_CURRENT_TARGET)))
            return;
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_CURRENT_TARGET, attacker);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:194
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(phase)))
        {
            (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_THETHING_NOAOE, phase > 1 ? FALSE : TRUE);
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:200
    public override object Destroy(float? force = null)
    {
        spawn_loc = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing.dm:204
public partial class mob_living_basic_boss_thing_with_ruin_loot : mob_living_basic_boss_thing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_organ_brain_cybernetic_ai), FALSE));
        return dot;
    }
}

// special stuff for our ruin to make a cooler bossfight

// tg: code/modules/mob/living/basic/boss/thing/thing.dm:210
public partial class obj_structure_thing_boss_phase_depleter : obj_structure
{
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        go_in_floor();
        SSqueuelinks.add_to_queue(this, mob_living_basic_boss_thing.RUIN_QUEUE, 0);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:228
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        if (!Dm.eq(id, mob_living_basic_boss_thing.RUIN_QUEUE))
            return;
        mob_living_basic_boss_thing thing = Dm.locate<mob_living_basic_boss_thing>(partners);
        if (thing == null)
        {
            qdel(this);
            return;
        }
        boss_weakref = WEAKREF(thing) as datum_weakref;
        RegisterSignal(thing, COMSIG_MEGAFAUNA_THETHING_PHASEUPDATED, nameof(thing_phaseupdated));
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:238
    [SignalHandler]
    public virtual void thing_phaseupdated(mob_living_basic_boss_thing source = null)
    {
        if (!Dm.truthy(functional))
            return;
        if (Dm.truthy(source.phase_invulnerability_timer))
            go_out_floor();
        else
            go_in_floor();
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:246
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.truthy(density) ? span_boldnotice(I18n.T("It may be possible to overload this and destroy that things defenses...")) : span_bolddanger(I18n.T("The machine is currently being restrained by tendrils.")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:250
    public virtual void set_circuit_floor(object state = null)
    {
        foreach (turf_open_floor_circuit circuit in Dm.each<turf_open_floor_circuit>(RANGE_TURFS(1, loc)))
        {
            circuit.on = Dm.num(state);
            circuit.update_appearance();
        }
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:255
    public virtual void go_in_floor()
    {
        if (!Dm.truthy(density))
            return;
        density = FALSE;
        obj_flags = Dm.num(Dm.bitand(obj_flags, Dm.bitnot(CAN_BE_HIT)));
        set_circuit_floor(FALSE);
        name = "hatch";
        icon_state = "thingdepleter_infloor";
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:264
    public virtual void go_out_floor()
    {
        if (Dm.truthy(density))
            return;
        density = TRUE;
        obj_flags = Dm.num(Dm.bitor(obj_flags, CAN_BE_HIT));
        set_circuit_floor(TRUE);
        name = Dm.initial(this, (atom p) => p.name);
        icon_state = "thingdepleter";
        Dm.@new<obj_effect_temp_visual_mook_dust>(loc);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:274
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        mob_living_basic_boss_thing the_thing = boss_weakref?.resolve() as mob_living_basic_boss_thing;
        if (the_thing == null || !Dm.truthy(functional) || !Dm.truthy(density))
            return null;
        if (!Dm.truthy(await user.can_perform_action(this)) || !Dm.truthy(await user.can_interact_with(this)))
            return null;
        balloon_alert_to_viewers(I18n.T("overloading..."));
        icon_state = "thingdepleter_overriding";
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, this)))
        {
            if (Dm.truthy(density))
                icon_state = "thingdepleter";
            return null;
        }
        Dm.@new<obj_effect_temp_visual_circle_wave_orange>(loc);
        playsound(this, Dm.resource("sound/effects/explosion/explosion3.ogg"), 100);
        animate(this, Dm.named("transform", Dm.mul(Dm.matrix(), 1.5f)), Dm.named("time", 0.2f * SECONDS));
        animate(Dm.named("transform", Dm.matrix()), Dm.named("time", 0));
        await the_thing.phase_successfully_depleted();
        functional = FALSE;
        go_in_floor();
        icon_state = "thingdepleter_overriding";
        // VARSET_CALLBACK(src, icon_state, "thingdepleter_broken"): ___callbackvarset is not declared, so the timer calls icon_state__varset
        _addtimer(Dm.@new<datum_callback>(this, nameof(icon_state__varset), "thingdepleter_broken"), 0.2f * SECONDS, file: "code/modules/mob/living/basic/boss/thing/thing.dm", line: 295);
        return null;
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, icon_state, value)
    public virtual void icon_state__varset(object value = null) => icon_state = value as string;
}

// tg: code/modules/mob/living/basic/boss/thing/thing.dm:300
public partial class obj_structure_aggro_gate : obj_structure
{
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:316
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        SSqueuelinks.add_to_queue(this, queue_id);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:320
    public override void MatchedLinks(object id = null, DmList partners = null)
    {
        if (!Dm.eq(id, queue_id))
            return;
        foreach (mob_living partner in Dm.each<mob_living>(partners))
        {
            RegisterSignal(partner, COMSIG_AI_BLACKBOARD_KEY_SET(target_bb_key), nameof(bar_the_gates));
            RegisterSignals(partner, Dm.list(COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_bb_key), COMSIG_LIVING_DEATH, COMSIG_MOB_LOGIN), nameof(open_gates));
        }
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:327
    [SignalHandler]
    public virtual void bar_the_gates(mob_living source = null)
    {
        atom target = (source.ai_controller as datum_ai_controller)?.blackboard[target_bb_key] as atom;
        if (QDELETED(target))
            return;
        invisibility = INVISIBILITY_NONE;
        density = TRUE;
        playsound(this, SFX_SPARKS, 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(3, cardinal_only: FALSE, source: this);
    }

    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:336
    public virtual void open_gates(mob_living source = null)
    {
        playsound(this, SFX_SPARKS, 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(3, cardinal_only: FALSE, source: this);
        density = FALSE;
        invisibility = INVISIBILITY_MAXIMUM;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/boss/thing/thing.dm:57
    internal DmList mob_living_basic_boss_thing__Initialize__innate_actions = Dm.list(
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_the_thing_decimate), BB_THETHING_DECIMATE),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_charge_the_thing), BB_THETHING_CHARGE),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_the_thing_big_tendrils), BB_THETHING_BIGTENDRILS),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_the_thing_shriek), BB_THETHING_SHRIEK),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_the_thing_cardinal_tendrils), BB_THETHING_CARDTENDRILS),
        Dm.kv(typeof(datum_action_cooldown_mob_cooldown_the_thing_acid_spit), BB_THETHING_ACIDSPIT));
}
