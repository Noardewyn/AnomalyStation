// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_ectoplasm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the ectoplasmic anomaly (the ghosts steer it and orbit it to grow its outburst: a defile of the
// floors and windows, haunted items, and a portal that brings the orbiting ghosts back as a swarm for two minutes); the outburst's
// revenant blight on the humans in range came with the diseases (gaps VI1). The var blocks are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:1
public partial class obj_effect_anomaly_ectoplasm : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), Dm.named("_deadchat_mode", ANARCHY_MODE), Dm.named("_inputs", Dm.list()), Dm.named("_input_cooldown", 7 * SECONDS)));
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (isobserver(user))
            Dm.append(ref dot, span_info(I18n.T("Orbiting this anomaly will increase the size and intensity of its effects.")));
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:27
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);

        if (effect_power >= 0 && effect_power <= 25)
            Dm.append(ref dot, span_notice(I18n.T("The space around the anomaly faintly resonates. It doesn't seem very powerful at the moment.")));
        else if (effect_power >= 26 && effect_power <= 49)
            Dm.append(ref dot, span_notice(I18n.T("The space around the anomaly seems to vibrate, letting out a noise that sounds like ghastly moaning. Someone should probably do something about that.")));
        else if (effect_power >= 50 && effect_power <= 100)
            Dm.append(ref dot, span_alert(I18n.T("The anomaly pulsates heavily, about to burst with unearthly energy. This can't be good.")));
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:38
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = await base.anomalyEffect(seconds_per_tick);

        if (Dm.truthy(override_ghosts))
            return dot;

        ghosts_orbiting = 0;
        foreach (mob_dead_observer orbiter in Dm.each<mob_dead_observer>(orbiters?.orbiter_list))
            ghosts_orbiting++;

        if (Dm.truthy(ghosts_orbiting))
        {
            float total_dead = Dm.num(length(GLOB.dead_player_list)) + Dm.num(length(GLOB.current_observers_list));
            effect_power = ghosts_orbiting / total_dead * 100;
        }
        else
            effect_power = 0;

        intensity_update();
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:56
    public override async DmTask detonate()
    {
        await base.detonate();

        if (effect_power < 10) //Under 10% participation, we do nothing more than a small visual *poof*.
        {
            Dm.@new<obj_effect_temp_visual_revenant_cracks>(get_turf(this));
            return;
        }

        if (effect_power >= 10) //Performs something akin to a revenant defile spell.
        {
            float effect_range = ghosts_orbiting + 3;
            foreach (object impacted_thing in Dm.each(range(effect_range, this)))
            {
                if (isfloorturf(impacted_thing))
                {
                    if (prob(5))
                        Dm.@new<obj_effect_decal_cleanable_blood>(get_turf(impacted_thing));
                    else if (prob(10))
                        Dm.@new<obj_effect_decal_cleanable_greenglow_ecto>(get_turf(impacted_thing));
                    else if (prob(10))
                        Dm.@new<obj_effect_decal_cleanable_dirt_dust>(get_turf(impacted_thing));

                    if (!isplatingturf(impacted_thing))
                    {
                        var floor_to_break = (turf_open_floor)impacted_thing;
                        if (Dm.truthy(floor_to_break.overfloor_placed) && floor_to_break.floor_tile != null && prob(20))
                        {
                            Dm.@new(floor_to_break.floor_tile, floor_to_break);
                            await floor_to_break.make_plating(TRUE);
                            floor_to_break.broken = TRUE;
                            floor_to_break.burnt = TRUE;
                        }
                    }
                }

                if (ishuman(impacted_thing))
                {
                    var mob_to_infect = (mob_living_carbon_human)impacted_thing;
                    await mob_to_infect.ForceContractDisease(Dm.@new<datum_disease_revblight>(), FALSE, TRUE); // gaps VI1
                    Dm.@new<obj_effect_temp_visual_revenant>(get_turf(mob_to_infect));
                    to_chat(mob_to_infect, span_revenminor(I18n.T("A cacophony of ghostly wailing floods your ears for a moment. The noise subsides, but a distant whispering continues echoing inside of your head...")));
                }

                if (impacted_thing is obj_structure_window window_to_damage)
                {
                    await window_to_damage.take_damage(rand(60, 90));
                    if (window_to_damage != null && Dm.truthy(window_to_damage.fulltile))
                        Dm.@new<obj_effect_temp_visual_revenant_cracks>(get_turf(window_to_damage));
                }
            }
        }

        if (effect_power >= 35)
        {
            float effect_range = ghosts_orbiting + 3;
            haunt_outburst(epicenter: get_turf(this), range: effect_range, haunt_chance: 45, duration: 2 * MINUTES);
        }

        if (effect_power >= 50) //Summon a ghost swarm!
        {
            DmList candidate_list = Dm.list();
            foreach (mob_dead_observer orbiter in Dm.each<mob_dead_observer>(orbiters?.orbiter_list))
                Dm.append(ref candidate_list, orbiter);

            Dm.@new<obj_structure_ghost_portal>(get_turf(this), candidate_list);

            priority_announce("Anomaly has reached critical mass. Ectoplasmic outburst detected.", "Anomaly Alert");
        }
    }

    /**
     * Manages updating the sprite for the anomaly based on how many orbiters it has.
     *
     *
     * A check that is run to determine which sprite the anoamly should currently be displaying.
     * With 50% or more participation, the "heavy" sprite is used. Otherwise, it is reverted to the normal anomaly sprite.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:115
    public virtual void intensity_update()
    {
        if (effect_power >= 50) //If we're at the threshold for the highest tier effect, we change sprites in preparation for the spooks.
        {
            icon_state = "ectoplasm_heavy";
            Waiting.NoSleep(update_icon_state());
        }
        else
        {
            icon_state = "ectoplasm";
            Waiting.NoSleep(update_icon_state());
        }
    }
}

// Ghost Portal. Used to bring anomaly orbiters into the playing field as ghosts. Destroys itself and all of its associated ghosts after two minutes.
// Can be destroyed early to the same effect.

// tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:128
public partial class obj_structure_ghost_portal : obj_structure
{
    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:134  var/static/list/spooky_noises (a type-level static, rules 10.6)
    public static ref DmList spooky_noises => ref TgState.Current.obj_structure_ghost_portal__spooky_noises;

    internal static DmList SpookyNoises() => spooky_noises ??= Dm.list(
        "sound/effects/hallucinations/growl1.ogg",
        "sound/effects/hallucinations/growl2.ogg",
        "sound/effects/hallucinations/growl3.ogg",
        "sound/effects/hallucinations/veryfar_noise.ogg",
        "sound/effects/hallucinations/wail.ogg");

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:143
    [DmParams("mapload", "candidate_list")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var candidate_list = Dm.arg(__extra, 0) as DmList;
        object dot = base.Initialize(mapload, __extra);

        START_PROCESSING(SSobj, this);
        Waiting.Detach(make_ghost_swarm(candidate_list)); // INVOKE_ASYNC
        playsound(this, pick(SpookyNoises()), 100, TRUE);
        // QDEL_IN(WEAKREF(src), 2 MINUTES), inline (rules 11.1 (f)): the weakref's own qdel deletes the portal
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 2 * MINUTES > GC_FILTER_QUEUE ? (object)WEAKREF((datum)WEAKREF(this)) : WEAKREF(this)), 2 * MINUTES, file: "code/game/objects/effects/anomalies/anomalies_ectoplasm.dm", line: 149);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:151
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);

        if (prob(5))
            playsound(this, pick(SpookyNoises()), 100);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:157
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        playsound(loc, "sound/effects/empulse.ogg", 75, TRUE);
        if (prob(40))
            playsound(this, pick(SpookyNoises()), 50);
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:162
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);

        STOP_PROCESSING(SSobj, this);
        DmList spawned = ghosts_spawned;
        Waiting.Detach(cleanup_ghosts(spawned)); // INVOKE_ASYNC(GLOBAL_PROC, GLOBAL_PROC_REF(cleanup_ghosts), ghosts_spawned)
        ghosts_spawned = null;
        return dot;
    }

    /**
     * Generates a poll for observers, spawning anyone who signs up in a large group of ghost mobs
     *
     * Generates a poll that asks anyone observing for participation. Spawns a bunch of basicmob ghosts with the keys of candidates who have signed up.
     * Ghosts are deleted two minutes after being made, and exist to punch stuff until it breaks.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:176
    public virtual async DmTask make_ghost_swarm(DmList candidate_list = null)
    {
        candidate_list ??= Dm.list();
        if (!Dm.truthy(length(candidate_list))) //If we are not passed a candidate list we just poll everyone who is dead, meaning these can also be spawned directly.
        {
            Dm.append(ref candidate_list, GLOB.current_observers_list);
            Dm.append(ref candidate_list, GLOB.dead_player_list);
        }

        var candidates = await SSpolling.poll_candidates(I18n.T("Would you like to participate in a spooky ghost swarm? (Warning: you will not be able to return to your body!)"), check_jobban: ROLE_SENTIENCE, poll_time: 10 * SECONDS, group: candidate_list, alert_pic: this, role_name_text: "ghost swarm") as DmList;
        foreach (mob_dead_observer candidate_ghost in Dm.each_cast<mob_dead_observer>(candidates))
        {
            var new_ghost = Dm.@new<mob_living_basic_ghost_swarm>(get_turf(this));
            Dm.append(ref ghosts_spawned, new_ghost);
            new_ghost.ghostize(FALSE);
            new_ghost.PossessByPlayer(candidate_ghost.key);
            object policy = get_policy(ROLE_ANOMALY_GHOST);
            if (Dm.truthy(policy))
                to_chat(new_ghost, policy);
            else
                to_chat(new_ghost, span_revenboldnotice(I18n.T("You are a lost soul, brought back to the realm of the living. Your time on this plane is limited, and you will soon be dragged back into the void!")));
            new_ghost.log_message("was returned to the living world as a ghost by an ectoplasmic anomaly.", LOG_GAME);
        }
    }
}

public static partial class GlobalProcs
{
    /**
     * Gives a farewell message and deletes the ghosts produced by a ghost portal structure.
     *
     * Handles cleanup of all ghost mobs spawned a ghost portal. Iterates through the list
     * and calls qdel on its contents, gives a short message, and leaves behind some goop.
     * Stored as a global, as it is called immediately after the portal deletes itself.
     *
     * * delete_list - The list of entities to be deleted by this proc.
     */
    // tg: code/game/objects/effects/anomalies/anomalies_ectoplasm.dm:204
    public static async DmTask cleanup_ghosts(DmList delete_list = null)
    {
        foreach (mob_living mob_to_delete in Dm.each_cast<mob_living>(delete_list))
        {
            mob_to_delete.visible_message(span_alert(I18n.T("The [] wails as it is torn back into the void!", mob_to_delete)), span_alert(I18n.T("You let out one last wail as you are sucked back into the realm of the dead. Then suddenly, you're back in the comforting embrace of the afterlife.")), span_hear(I18n.T("You hear ethereal wailing.")));
            // tg plays pick(delete_list), a ghost mob of the list, as the sound (no sound); the portal's wails are meant
            playsound(mob_to_delete, pick(obj_structure_ghost_portal.SpookyNoises()), 50);
            Dm.@new<obj_effect_temp_visual_revenant_cracks>(get_turf(mob_to_delete));
            Dm.@new<obj_effect_decal_cleanable_greenglow_ecto>(get_turf(mob_to_delete));
            mob_to_delete.ghostize(FALSE); //So we don't throw an alert for deleting a mob with a key inside.
            qdel(mob_to_delete);
        }
    }
}

public sealed partial class TgState
{
    internal DmList obj_structure_ghost_portal__spooky_noises;
}
