// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): death and send_death_moodlets are translated (gaps package G4: its viewers' loop and its overrides); M6 slice 5 (gibbing): gib, inflate_gib,
// gib_animation, spawn_gibs, get_gibs_type, spill_organs and spread_bodyparts (:11-93). The M7 supermatter slice: dusting
// (dust, dust_animation, the dust animation's effect and spawn_dust, :90-172), the crystal's touch. gib ghostizes its player (the ghosts, 2026-10-16, D151). The types /mob/living/basic and /mob/living/simple_animal keep their
// [DmPath] here (W4.0 contracts: the earliest .dme file that declares them, rules 2.3).

// tg: code/modules/mob/living/death.dm:199  (gaps package G4: the overrides of send_death_moodlets, :199-216)
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/death.dm:199
    public override async DmTask send_death_moodlets(float? dusted = null, float? gibbed = null)
    {
        return; // You are a machine (Future todo, roboticists feel sad though)
    }
}

// tg: code/modules/mob/living/death.dm:201
[DmPath("/mob/living/basic")]
public partial class mob_living_basic : mob_living
{
    // tg: code/modules/mob/living/death.dm:202
    public override async DmTask send_death_moodlets(float? dusted = null, float? gibbed = null)
    {
        if (!Dm.truthy(Dm.bitand(basic_mob_flags, SENDS_DEATH_MOODLETS)))
            return;
        await base.send_death_moodlets(dusted, gibbed);
        add_memory_in_range(this, 7, typeof(datum_memory_pet_died), Dm.named("deuteragonist", this));
    }
}

// tg: code/modules/mob/living/death.dm:207
[DmPath("/mob/living/simple_animal")]
public partial class mob_living_simple_animal : mob_living
{
    // tg: code/modules/mob/living/death.dm:207
    public override async DmTask send_death_moodlets(float? dusted = null, float? gibbed = null)
    {
        return; // I don't care about you anymore
    }
}

// tg: code/modules/mob/living/death.dm:210
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/death.dm:210
    public override async DmTask send_death_moodlets(float? dusted = null, float? gibbed = null)
    {
        // Deaths of people undergoing surgery don't count
        // otherwise surgeons would be depressed and that would be too realistic
        if (HAS_TRAIT(this, TRAIT_READY_TO_OPERATE))
            return;
        await base.send_death_moodlets(dusted, gibbed);
        add_memory_in_range(this, 7, Dm.truthy(gibbed) ? typeof(datum_memory_witness_gib) : typeof(datum_memory_witnessed_death), Dm.named("protagonist", this));
    }
}

// tg: code/modules/mob/living/death.dm:11
public partial class mob_living : mob
{
    /**
     * Blow up the mob into giblets
     *
     * drop_bitflags: (see code/__DEFINES/blood.dm)
     * * DROP_BRAIN - Gibbed mob will drop a brain
     * * DROP_ORGANS - Gibbed mob will drop organs
     * * DROP_BODYPARTS - Gibbed mob will drop bodyparts (arms, legs, etc.)
     * * DROP_ITEMS - Gibbed mob will drop carried items (otherwise they get deleted)
     * * DROP_ALL_REMAINS - Gibbed mob will drop everything
    **/
    // tg: code/modules/mob/living/death.dm:11  (M6 slice 5; D151: the player becomes a ghost, as in tg)
    public virtual async DmTask gib(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        float prev_lying = lying_angle;
        await spawn_gibs(drop_bitflags);

        if (!Dm.truthy(prev_lying))
            gib_animation();

        if (stat != DEAD)
            await death(TRUE);
        await send_death_moodlets(gibbed: TRUE);

        ghostize();
        await spill_organs(drop_bitflags);

        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
            await spread_bodyparts(drop_bitflags, gibbed: TRUE);

        // failsafe for if we fuck up and leave our brain behind. (other organs are replaceable so we can ignore them.)
        obj_item_organ_brain brain = get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BRAIN)) && brain != null)
        {
            stack_trace("gib invoked with drop_brain() had their brain after spilling organs and bodyparts, meaning both failed!", "code/modules/mob/living/death.dm", 31);
            await brain.Remove(this as mob_living_carbon);
            await brain.forceMove(drop_location() as atom);
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_GIBBED]))
            this._SendSignal(COMSIG_LIVING_GIBBED, Dm.list(this, drop_bitflags));
        qdel(this);
    }

    // Plays an animation that makes mobs appear to inflate before finally gibbing
    // tg: code/modules/mob/living/death.dm:39
    public virtual void inflate_gib(float? drop_bitflags = null, float? gib_time = null, float? anim_time = null)
    {
        drop_bitflags ??= Dm.bitor(Dm.bitor(DROP_BRAIN, DROP_ORGANS), DROP_ITEMS);
        gib_time ??= 2.5f * SECONDS;
        anim_time ??= 4 * SECONDS;
        _addtimer(Dm.@new<datum_callback>(this, nameof(gib), drop_bitflags), gib_time.Value, file: "code/modules/mob/living/death.dm", line: 40);
        matrix M = Dm.@new<matrix>();
        M.Scale(1.8f, 1.2f);
        animate(this, Dm.named("time", anim_time.Value), Dm.named("transform", M), Dm.named("easing", SINE_EASING));
    }

    // tg: code/modules/mob/living/death.dm:45
    public virtual void gib_animation()
    {
        return;
    }

    /**
     * Spawn bloody gib mess on the floor
     *
     * drop_bitflags: (see code/__DEFINES/blood.dm)
     * * DROP_BODYPARTS - Gibs will spawn with bodypart limbs present
    **/
    // tg: code/modules/mob/living/death.dm:54
    public virtual async DmTask spawn_gibs(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return;

        object gib_type = get_gibs_type(drop_bitflags);
        if (Dm.truthy(gib_type))
            Dm.@new((Type)gib_type, drop_location(), this, await get_static_viruses());
    }

    /// Get type of gibs this mob should spawn based on our flags
    // tg: code/modules/mob/living/death.dm:63
    public virtual object get_gibs_type(float? drop_bitflags = null)
    {
        if (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)))
            return typeof(obj_effect_gibspawner_robot);
        return typeof(obj_effect_gibspawner_generic);
    }

    /**
     * Drops a mob's organs on the floor
     *
     * drop_bitflags: (see code/__DEFINES/blood.dm)
     * * DROP_BRAIN - Mob will drop a brain
     * * DROP_ORGANS - Mob will drop organs
     * * DROP_BODYPARTS - Mob will drop bodyparts (arms, legs, etc.)
     * * DROP_ALL_REMAINS - Mob will drop everything
    **/
    // tg: code/modules/mob/living/death.dm:77
    public virtual async DmTask spill_organs(float? drop_bitflags = null)
    {
        return;
    }

    /**
     * Launches all bodyparts away from the mob
     *
     * drop_bitflags: (see code/__DEFINES/blood.dm)
     * * DROP_BRAIN - Detaches the head from the mob and launches it away from the body
    **/
    // tg: code/modules/mob/living/death.dm:86
    public virtual async DmTask spread_bodyparts(float? drop_bitflags = null, float? gibbed = null)
    {
        return;
    }

    // tg: code/modules/mob/living/death.dm:107
    public override async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        give_moodlet ??= TRUE;
        await base.dust(just_ash, drop_items, give_moodlet, force);
        if (body_position == STANDING_UP)
        {
            // keep us upright so the animation fits.
            ADD_TRAIT(this, TRAIT_FORCED_STANDING, TRAIT_GENERIC);
        }

        if (Dm.truthy(give_moodlet))
            await send_death_moodlets(dusted: TRUE);

        if (Dm.truthy(drop_items))
            await unequip_everything();

        if (Dm.truthy(buckled))
            await buckled.unbuckle_mob(this, force: TRUE);

        await death(TRUE);
        // Some mobs get qdeleted on death
        if (!QDELETED(this))
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_dust), just_ash), DUST_ANIMATION_TIME - 0.3f * SECONDS, file: "code/modules/mob/living/death.dm", line: 125);
            ghostize();
        }
    }

    /**
     * Spawns dust / ash or remains where the mob was
     *
     * just_ash: If TRUE, just ash will spawn where the mob was, as opposed to remains
     */
    // tg: code/modules/mob/living/death.dm:165
    public virtual void spawn_dust(float? just_ash = null)
    {
        just_ash ??= FALSE;
        Type ash_type = typeof(obj_effect_decal_cleanable_ash);
        if (Dm.num(mob_size) >= MOB_SIZE_LARGE)
            ash_type = typeof(obj_effect_decal_cleanable_ash_large);

        obj_effect_decal_cleanable_ash ash = Dm.@new(ash_type, loc) as obj_effect_decal_cleanable_ash;
        ash.pixel_z = -5;
        ash.pixel_w = rand(-1, 1);
    }

    /**
     * Sends mood events to everyone who saw this mob die.
     *
     * Arguments:
     * * dusted - Was the mob dusted?
     * * gibbed - Was the mob gibbed?
     */
    // tg: code/modules/mob/living/death.dm:185
    public virtual async DmTask send_death_moodlets(float? dusted = null, float? gibbed = null)
    {
        dusted ??= FALSE;
        gibbed ??= FALSE;
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return;

        foreach (mob_living nearby in Dm.each<mob_living>(viewers(this)))
        {
            if (nearby == this || IS_UNCONSCIOUS(nearby) || Dm.truthy(nearby.is_blind()))
                continue;
            await nearby.add_mood_event("saw_death", typeof(datum_mood_event_conditional_see_death), this, dusted, gibbed);
            nearby.mind?.witnessed_death(this);
        }

        if (!Dm.truthy(gibbed) && !Dm.truthy(dusted))
            mind?.experienced_death();
    }

    /*
     * Called when the mob dies. Can also be called manually to kill a mob.
     *
     * Arguments:
     * * gibbed - Was the mob gibbed?
    */
    // tg: code/modules/mob/living/death.dm:224
    public virtual async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return FALSE;

        if (!Dm.truthy(gibbed))
        {
            if (Dm.truthy(death_sound) || Dm.truthy(death_message) || Dm.truthy(Dm.bitand(living_flags, ALWAYS_DEATHGASP)))
            {
                // tg: :230  INVOKE_ASYNC(src, TYPE_PROC_REF(/mob, emote), "deathgasp")
                Waiting.Detach(emote("deathgasp")); // INVOKE_ASYNC
            }
            await send_death_moodlets();
        }

        await set_stat(DEAD);
        timeofdeath = world.time;
        station_timestamp_timeofdeath = round_timestamp();
        turf death_turf = get_turf(this);
        area death_area = get_area(this) as area;
        // Display a death message if the mob is a player mob (has an active mind)
        object player_mob_check = ((mind is var and__1 && !Dm.truthy(and__1) ? and__1 : mind.name) is var and__2 && !Dm.truthy(and__2) ? and__2 : (object)mind.active);
        // and, display a death message if the area allows it (or if they're in nullspace)
        float valid_area_check = Dm.num(!Dm.truthy(death_area) || !Dm.truthy(Dm.bitand(death_area.area_flags, NO_DEATH_MESSAGE)));
        if (Dm.truthy(player_mob_check))
        {
            if (Dm.truthy(valid_area_check))
            {
                deadchat_broadcast(I18n.T(" has died at <b>[]</b>.", get_area_name(death_turf)), Dm.text("<b>[]</b>", mind.name), follow_target: this, turf_target: death_turf, message_type: DEADCHAT_DEATHRATTLE);
            }
            // tg: :242-244  if(SSlag_switch.measures[DISABLE_DEAD_KEYLOOP] && !client?.holder) { to_chat(...); ghostize(TRUE) }
            // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm) is not declared; its measures are off by default,
            // so the test is FALSE (ghosts are M10).
            Dm.Deferred("code/modules/mob/living/death.dm:242", "death: the lag switch's disabled dead keyloop (SSlag_switch, M10+)");
        }
        set_disgust(0);
        // DM passes a second argument (0); /mob/living/proc/SetSleeping declares one parameter, so it is dropped (rules 7.3).
        await SetSleeping(0);
        await reset_perspective(null);
        reload_fullscreen();
        await update_mob_action_buttons();
        update_damage_hud();
        update_health_hud();
        med_hud_set_health();
        med_hud_set_status();
        await stop_pulling();

        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_DEATH]))
            _SendSignal(COMSIG_LIVING_DEATH, Dm.list(this, gibbed));
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_MOB_DEATH, src, gibbed) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_MOB_DEATH]))
            SSdcs._SendSignal(COMSIG_GLOB_MOB_DEATH, Dm.list(SSdcs, this, gibbed));

        if (Dm.truthy(client))
            client.move_delay = Dm.initial(client, (client p) => p.move_delay);

        persistent_client?.time_of_death = timeofdeath;

        if (Dm.truthy(mind))
            await GhostNightVision.Ensure(this); // DV174: a player corpse's optional night vision, initially off
        update_sight();
        return TRUE;
    }
}

// tg: code/modules/mob/living/death.dm:101
public partial class atom_movable : atom
{
    // tg: code/modules/mob/living/death.dm:90  #define DUST_ANIMATION_TIME 1.3 SECONDS (file-local, rules 11.4)
    protected const float DUST_ANIMATION_TIME = 1.3f * SECONDS;

    /**
     * This is the proc for turning a mob into ash.
     * Dusting robots does not eject the MMI, so it's a bit more powerful than gib()
     *
     * Arguments: (Only used for mobs)
     * * just_ash - If TRUE, ash will spawn where the mob was, as opposed to remains
     * * drop_items - Should the mob drop their items before dusting?
     * * force - Should this mob be FORCABLY dusted?
    */
    // tg: code/modules/mob/living/death.dm:101
    public virtual async DmTask dust(object just_ash = null, object drop_items = null, object give_moodlet = null, object force = null)
    {
        dust_animation();
        // since this is sometimes called in the middle of movement, allow half a second for movement to finish, ghosting to happen and animation to play.
        // Looks much nicer and doesn't cause multiple runtimes.
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), DUST_ANIMATION_TIME > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), DUST_ANIMATION_TIME, file: "code/modules/mob/living/death.dm", line: 105); // QDEL_IN(src, DUST_ANIMATION_TIME), inline (rules 11.1 (f))
    }

    /// Animates turning into dust.
    /// Does not delete src afterwards, BUT it will become invisible (and grey), so ensure you handle that yourself
    // tg: code/modules/mob/living/death.dm:130
    public virtual void dust_animation(atom anim_loc = null)
    {
        anim_loc ??= this.loc;
        if (anim_loc == null) // the effect breaks if we have a null loc
            return;
        obj_effect_temp_visual_dust_animation_filter dustfx = Dm.@new<obj_effect_temp_visual_dust_animation_filter>(anim_loc, Dm.@ref(this));
        add_filter("dust_animation", 1, displacement_map_filter(render_source: dustfx.render_target, size: 256) as DmList);
        add_filter("dust_color", 1, color_matrix_filter() as DmList);
        transition_filter("dust_color", color_matrix_filter(COLOR_MATRIX_GRAYSCALE()) as DmList, DUST_ANIMATION_TIME - 0.3f * SECONDS);
        Dm.animate(this, Dm.named("alpha", 0), Dm.named("time", DUST_ANIMATION_TIME - 0.1f * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)));
    }
}

/// Holds the dust animation filter effect, so we can animate it
// tg: code/modules/mob/living/death.dm:139
public partial class obj_effect_temp_visual_dust_animation_filter : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/death.dm:145
    [DmParams("mapload", "anim_id")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object anim_id = Dm.arg(__extra, 0);
        anim_id ??= "random_default_anti_collision_text";
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // we manually animate this, rather than just using an animated icon state or flick, to work around byond animated state memes
        // (normally, all animated icon states are synced to the same time, which would bad here)
        for (float i = 2, i__end = Dm.num(duration); i <= i__end; i++)
        {
            if (Dm.truthy(FALSE) && !Dm.truthy(icon_exists(icon, Dm.text("dust.[]", i)))) // PERFORM_ALL_TESTS(focus_only/runtime_icon_states) (rules 1.7)
                stack_trace(Dm.text("Missing dust animation icon state: dust.[]", i), "code/modules/mob/living/death.dm", 151);
            Dm.animate(this, Dm.named("time", 1), Dm.named("icon_state", Dm.text("dust.[]", i)), Dm.named("flags", ANIMATION_CONTINUE));
        }
        if (Dm.truthy(FALSE) && Dm.truthy(icon_exists(icon, Dm.text("dust.[]", duration + 1)))) // PERFORM_ALL_TESTS(focus_only/runtime_icon_states)
            stack_trace(Dm.text("Extra dust animation icon state: dust.[]", duration + 1), "code/modules/mob/living/death.dm", 154);
        render_target = Dm.text("*dust-[]", anim_id);
        return dot;
    }
}
