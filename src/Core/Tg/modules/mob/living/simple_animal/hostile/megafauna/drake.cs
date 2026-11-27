// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/*
 *
 *ASH DRAKE
 *
 *Ash drakes spawn randomly wherever a lavaland creature is able to spawn. They are the draconic guardians of the Necropolis.
 *
 *It acts as a melee creature, chasing down and attacking its target while also using different attacks to augment its power that increase as it takes damage.
 *
 *Whenever possible, the drake will breathe fire directly at its target, igniting and heavily damaging anything caught in the blast.
 *It also often causes lava to pool from the ground around you - many nearby turfs will temporarily turn into lava, dealing damage to anything on the turfs.
 *The drake also utilizes its wings to fly into the sky, flying after its target and attempting to slam down on them. Anything near when it slams down takes huge damage.
 *Sometimes it will chain these swooping attacks over and over, making swiftness a necessity.
 *Sometimes, it will encase its target in an arena of lava
 *
 *When an ash drake dies, it leaves behind a chest that can contain four things:
 *A. A spectral blade that allows its wielder to call ghosts to it, enhancing its power
 *B. A lava staff that allows its wielder to create lava
 *C. A spellbook and wand of fireballs
 *D. A bottle of dragon's blood with several effects, including turning its imbiber into a drake themselves.
 *
 *When butchered, they leave behind diamonds, sinew, bone, and ash drake hide. Ash drake hide can be used to create a hooded cloak that protects its wearer from ash storms.
 *
 *Intended Difficulty: Medium
 */

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:32
[DmPath("/mob/living/simple_animal/hostile/megafauna/dragon")]
public partial class mob_living_simple_animal_hostile_megafauna_dragon : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:2  #define DRAKE_ENRAGED (health < maxHealth*0.5) (file-local, rules 11.4)
    private bool DRAKE_ENRAGED => health < maxHealth * 0.5f;
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:4-5 (file-local defines)
    private const float SWOOP_DAMAGEABLE = 1;
    private const float SWOOP_INVULNERABLE = 2;

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:89
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        fire_cone = Dm.@new<datum_action_cooldown_mob_cooldown_fire_breath_cone>(this);
        meteors = Dm.@new<datum_action_cooldown_mob_cooldown_meteors>(this);
        mass_fire = Dm.@new<datum_action_cooldown_mob_cooldown_fire_breath_mass_fire>(this);
        lava_swoop = Dm.@new<datum_action_cooldown_mob_cooldown_lava_swoop>(this);
        Waiting.NoSleep(fire_cone.Grant(this));
        Waiting.NoSleep(meteors.Grant(this));
        Waiting.NoSleep(mass_fire.Grant(this));
        Waiting.NoSleep(lava_swoop.Grant(this));
        RegisterSignal(this, COMSIG_MOB_ABILITY_STARTED, nameof(start_attack));
        RegisterSignal(this, COMSIG_MOB_ABILITY_FINISHED, nameof(finished_attack));
        RegisterSignal(this, COMSIG_SWOOP_INVULNERABILITY_STARTED, nameof(swoop_invulnerability_started));
        RegisterSignal(this, COMSIG_LAVA_ARENA_FAILED, nameof(on_arena_fail));
        Waiting.NoSleep(AddElement(typeof(datum_element_change_force_on_death), Dm.named("move_force", MOVE_FORCE_DEFAULT)));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:105
    public override object Destroy(float? force = null)
    {
        fire_cone = null;
        meteors = null;
        mass_fire = null;
        lava_swoop = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:112
    public override async DmTask OpenFire(atom A = null)
    {
        if (Dm.truthy(swooping))
            return;

        if (client != null)
            return;

        if (prob(15 + anger_modifier))
        {
            if (DRAKE_ENRAGED)
            {
                // Lava Arena
                await lava_swoop.Trigger(null, null, target);
                return;
            }
            // Lava Pools
            if (Dm.truthy(await lava_swoop.Trigger(null, null, target)))
            {
                // SLEEP_CHECK_DEATH(0, src) (__DEFINES/mobs.dm:627), expanded inline
                await sleep(0);
                if (QDELETED(this) || stat == DEAD)
                    return;
                fire_cone.StartCooldown(0);
                await fire_cone.Trigger(null, null, target);
                meteors.StartCooldown(0);
                await meteors.Trigger(null, null, target);
                return;
            }
        }
        else if (prob(10 + anger_modifier) && DRAKE_ENRAGED)
        {
            await mass_fire.Trigger(null, null, target);
            return;
        }
        if (Dm.truthy(await fire_cone.Trigger(null, null, target)) && prob(50))
        {
            meteors.StartCooldown(0);
            await meteors.Trigger(null, null, target);
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:139
    [SignalHandler]
    public virtual void start_attack(mob_living owner = null, datum_action_cooldown activated = null)
    {
        if (activated == lava_swoop)
        {
            icon_state = "dragon_shadow";
            swooping = SWOOP_DAMAGEABLE;
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:145
    [SignalHandler]
    public virtual void swoop_invulnerability_started()
    {
        swooping = SWOOP_INVULNERABLE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:149
    [SignalHandler]
    public virtual void finished_attack(mob_living owner = null, datum_action_cooldown finished = null)
    {
        if (finished == lava_swoop)
        {
            icon_state = Dm.initial(this, (mob_living_simple_animal_hostile_megafauna_dragon p) => p.icon_state);
            swooping = NONE;
        }
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:155
    [SignalHandler]
    public virtual void on_arena_fail()
    {
        Waiting.Detach(arena_escape_enrage()); // INVOKE_ASYNC
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:159
    public virtual async DmTask arena_escape_enrage() // you ran somehow / teleported away from my arena attack now i'm mad fucker
    {
        // SLEEP_CHECK_DEATH(0, src), expanded inline
        await sleep(0);
        if (QDELETED(this) || stat == DEAD)
            return;
        visible_message(span_boldwarning(I18n.T("[] starts to glow vibrantly as its wounds close up!", this)));
        await adjust_brute_loss(-250); // yeah you're gonna pay for that, don't run nerd
        await add_atom_colour(rgb(255, 255, 0), TEMPORARY_COLOUR_PRIORITY);
        move_to_delay = move_to_delay / 2;
        set_light_range(10);
        // SLEEP_CHECK_DEATH(5 SECONDS, src), expanded inline: run.
        await sleep(5 * SECONDS);
        if (QDELETED(this) || stat == DEAD)
            return;
        await mass_fire.Activate(target);
        mass_fire.StartCooldown(8 * SECONDS);
        move_to_delay = Dm.num(Dm.initial(this, (mob_living_simple_animal_hostile p) => p.move_to_delay));
        await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY);
        set_light_range(Dm.initial(this, (atom p) => p.light_range));
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:174
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) <= EXPLODE_LIGHT)
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // Prevents damage from adjust_x_loss while in a host, because that damage would be nullified by the next [proc/sync_health] call. Adjust host blood volume instead.
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:180
    public override object can_adjust_brute_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        if (!Dm.truthy(forced) && Dm.truthy(Dm.bitand(swooping, SWOOP_INVULNERABLE)))
            return FALSE;
        return base.can_adjust_brute_loss(amount, forced, required_bodytype);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:185
    public override object can_adjust_fire_loss(object amount = null, object forced = null, object required_bodytype = null)
    {
        if (!Dm.truthy(forced) && Dm.truthy(Dm.bitand(swooping, SWOOP_INVULNERABLE)))
            return FALSE;
        return base.can_adjust_fire_loss(amount, forced, required_bodytype);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:190
    public override object can_adjust_tox_loss(object amount = null, object forced = null, float? required_biotype = null)
    {
        if (!Dm.truthy(forced) && Dm.truthy(Dm.bitand(swooping, SWOOP_INVULNERABLE)))
            return FALSE;
        return base.can_adjust_tox_loss(amount, forced, required_biotype);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:195
    public override object can_adjust_oxy_loss(object amount = null, object forced = null, object required_biotype = null)
    {
        if (!Dm.truthy(forced) && Dm.truthy(Dm.bitand(swooping, SWOOP_INVULNERABLE)))
            return FALSE;
        return base.can_adjust_oxy_loss(amount, forced, required_biotype);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:200
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        if (Dm.num(amount) > 0)
            lava_swoop.enraged = Dm.num(DRAKE_ENRAGED);
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:205
    public override async DmTask updatehealth()
    {
        await base.updatehealth();
        anger_modifier = clamp(((maxHealth - health) / 60), 0, 20);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:209
    public override void visible_message(object message = null, object self_message = null, object blind_message = null, float? vision_distance = null, object ignored_mobs = null, float? visible_message_flags = null)
    {
        if (Dm.truthy(Dm.bitand(swooping, SWOOP_INVULNERABLE))) //to suppress attack messages without overriding every single proc that could send a message saying we got hit
            return;
        base.visible_message(message, self_message, blind_message, vision_distance, ignored_mobs, visible_message_flags);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:214
    public override async DmTask<object> AttackingTarget(atom attacked_target = null)
    {
        if (!Dm.truthy(swooping))
            return await base.AttackingTarget(attacked_target);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:218
    public override async DmTask DestroySurroundings()
    {
        if (!Dm.truthy(swooping))
            await base.DestroySurroundings();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:222  (tg returns nothing, a moved step included)
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (!Dm.truthy(swooping))
            await base.Move(newloc, direction, glide_size_override, update_dir);
        return null;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:226
    public override object Goto(object target = null, object delay = null, object minimum_distance = null)
    {
        if (!Dm.truthy(swooping))
            base.Goto(target, delay, minimum_distance);
        return null;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:230
public partial class obj_effect_temp_visual_lava_warning : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:239
    [DmParams("mapload", "reset_time")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object reset_time = Dm.arg(__extra, 0) ?? 10;
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(fall(reset_time)); // INVOKE_ASYNC
        alpha = 63.75f;
        animate(this, Dm.named("alpha", 255), Dm.named("time", duration));
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:245
    public virtual async DmTask fall(object reset_time = null)
    {
        turf our_turf = get_turf(this);
        playsound(our_turf, Dm.resource("sound/effects/magic/fleshtostone.ogg"), 80, TRUE);
        await sleep(Dm.num(duration));
        our_turf = (turf)turf.dm_identity(our_turf); // TURF-IDENTITY: tg's our_turf names the cell, which may have turned to lava meanwhile
        playsound(our_turf, Dm.resource("sound/effects/magic/fireball.ogg"), 200, TRUE);
        bool can_transform_turf = !isclosedturf(our_turf) && !islava(our_turf);

        foreach (mob_living victim in Dm.each<mob_living>(our_turf))
        {
            if (victim is mob_living_simple_animal_hostile_megafauna_dragon || victim == owner)
                continue;
            await victim.adjust_fire_loss(10);
            if (can_transform_turf)
                to_chat(victim, span_userdanger(I18n.T("You fall directly into the pool of lava!")));
            else
                to_chat(victim, span_userdanger(I18n.T("You are set ablaze by a fireball from above!")));
        }

        // deals damage to mechs
        foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(our_turf))
            await mech.take_damage(45, BRUTE, MELEE, 1);

        // changes turf to lava temporarily if possible, create a fire visual otherwise
        if (!can_transform_turf)
        {
            Dm.@new<obj_effect_temp_visual_fire_light>(our_turf);
            return;
        }

        Type lava_turf = typeof(turf_open_lava_smooth);
        Type reset_turf = our_turf.type;
        await our_turf.TerraformTurf(lava_turf, flags: CHANGETURF_INHERIT_AIR);
        our_turf = (turf)turf.dm_identity(our_turf); // TURF-IDENTITY: tg's our_turf names the cell, the lava from here on
        _addtimer(Dm.@new<datum_callback>(our_turf, nameof(turf.ChangeTurf), reset_turf, null, CHANGETURF_INHERIT_AIR), Dm.num(reset_time), Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm", line: 275);
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:296
public partial class obj_effect_temp_visual_fireball : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:307
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        animate(this, Dm.named("pixel_z", 0), Dm.named("time", duration));
        return dot;
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:311
public partial class obj_effect_temp_visual_target : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:319
    [DmParams("mapload", "flame_hit")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList flame_hit = Dm.arg(__extra, 0) as DmList;
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(fall(flame_hit)); // INVOKE_ASYNC
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:323
    public virtual async DmTask fall(DmList flame_hit = null)
    {
        turf T = get_turf(this);
        playsound(T, Dm.resource("sound/effects/magic/fleshtostone.ogg"), 80, TRUE);
        Dm.@new<obj_effect_temp_visual_fireball>(T);
        await sleep(Dm.num(duration));
        T = (turf)turf.dm_identity(T); // TURF-IDENTITY: tg's T names the cell, which may have changed while the meteor fell
        if (ismineralturf(T))
        {
            turf_closed_mineral M = T as turf_closed_mineral;
            await M.gets_drilled();
            T = (turf)turf.dm_identity(T); // TURF-IDENTITY: the floor the rock left
        }
        playsound(T, SFX_EXPLOSION, 80, TRUE);
        Dm.@new<obj_effect_hotspot>(T);
        T.hotspot_expose(DRAKE_FIRE_TEMP, DRAKE_FIRE_EXPOSURE, 1);
        foreach (mob_living L in Dm.each<mob_living>(T.contents))
        {
            if (L is mob_living_simple_animal_hostile_megafauna_dragon)
                continue;
            if (islist(flame_hit) && !Dm.truthy(flame_hit[L]))
            {
                await L.adjust_fire_loss(40);
                to_chat(L, span_userdanger(I18n.T("You're hit by the drake's fire breath!")));
                flame_hit[L] = TRUE;
            }
            else
                await L.adjust_fire_loss(10); //if we've already hit them, do way less damage
        }
    }
}

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:346
public partial class mob_living_simple_animal_hostile_megafauna_dragon_lesser : mob_living_simple_animal_hostile_megafauna_dragon
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:362
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(meteors.Remove(this));
        Waiting.NoSleep(mass_fire.Remove(this));
        lava_swoop.cooldown_time = 20 * SECONDS;
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/drake.dm:368
    public override async DmTask on_damage_loss_changed(object amount = null, object updating_health = null, object forced = null, object damage_type = null)
    {
        await base.on_damage_loss_changed(amount, updating_health, forced, damage_type);
        if (lava_swoop != null)
            lava_swoop.enraged = FALSE; // In case taking damage caused us to start deleting ourselves
    }
}
