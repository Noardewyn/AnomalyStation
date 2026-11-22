// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/landmarks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 P2): the base landmark (INITIALIZE_IMMEDIATE, Initialize, Destroy), the job start landmark (Initialize,
// Destroy, after_round_start) and the late-join landmark (Initialize), which the station's spawn points need, and the generic
// event spawn (Initialize, Destroy: the M7 delamination slice); the other
// landmark types keep their generated code (inert until their milestone). singularity_act and singularity_pull are
// docs/ai/specs/live-partial.txt rows (M10+).

// tg: code/game/objects/effects/landmarks.dm:1
public partial class obj_effect_landmark : obj_effect
{
    // The New() that INITIALIZE_IMMEDIATE(/obj/effect/landmark) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/game/objects/effects/landmarks.dm:17
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/game/objects/effects/landmarks.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.landmarks_list, this);
        return dot;
    }

    // tg: code/game/objects/effects/landmarks.dm:23
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.landmarks_list, this);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/effects/landmarks.dm:28
public partial class obj_effect_landmark_start : obj_effect_landmark
{
    // tg: code/game/objects/effects/landmarks.dm:37
    public virtual void after_round_start()
    {
        // We'd like to keep these around for unit tests, so we can check that they exist.
        // tg: #if !defined(UNIT_TESTS) && !defined(MAP_TEST) (production)
        if (Dm.truthy(delete_after_roundstart))
        {
            // DEVIATION(DV180): the late join's last resort remembers where the landmark stood (SSjob.get_last_resort_spawn_points)
            if (get_turf(this) is turf T)
            {
                Dm.append(ref SSjob.last_resort_turfs, T);
                if (SSjob.job_start_turfs[name] is not DmList same)
                    SSjob.job_start_turfs[name] = same = Dm.list();
                Dm.append(ref same, T);
            }
            qdel(this);
        }
    }

    // tg: code/game/objects/effects/landmarks.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.start_landmarks_list, this);
        MapSeam.NoteStartLandmark(this); // DV26: the host's spawn points, in the map's order (the round start shuffles and deletes these)
        if (Dm.truthy(jobspawn_override))
        {
            // LAZYADDASSOCLIST(GLOB.jobspawn_overrides, name, src) (__DEFINES/_lists.dm), expanded inline (rules 11.1 (e))
            GLOB.jobspawn_overrides ??= Dm.list();
            Dm.append_at(GLOB.jobspawn_overrides, name, Dm.list(this));
        }
        if (name != "start")
            tag = Dm.text("start*[]", name);
        return dot;
    }

    // tg: code/game/objects/effects/landmarks.dm:52
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.start_landmarks_list, this);
        if (Dm.truthy(jobspawn_override))
        {
            // LAZYREMOVEASSOC(GLOB.jobspawn_overrides, name, src) (__DEFINES/_lists.dm), expanded inline (rules 11.1 (e))
            if (GLOB.jobspawn_overrides != null)
            {
                if (Dm.truthy(GLOB.jobspawn_overrides[name]))
                {
                    Dm.remove_at(GLOB.jobspawn_overrides, name, this);
                    if (!Dm.truthy(length(GLOB.jobspawn_overrides[name])))
                        Dm.remove(ref GLOB.jobspawn_overrides, name);
                }
                if (!Dm.truthy(length(GLOB.jobspawn_overrides)))
                    GLOB.jobspawn_overrides = null;
            }
        }
        return base.Destroy(force);
    }
}

// tg: code/game/objects/effects/landmarks.dm:342
public partial class obj_effect_landmark_latejoin : obj_effect_landmark
{
    // tg: code/game/objects/effects/landmarks.dm:345
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        // GAP(lead): SSjob's hand-written dm_init_vars (controllers/subsystem/job.cs) lacks tg's latejoin_trackers = list()
        // (job.dm:32, the generator's GAP note); until it has it, the list is made here, where tg first adds to it.
        // GAP(lead): the hand-built test worlds (tests/Anomaly.Core.Tests/Tg/TgToyRoom.PrepareWorld, create_and_destroy) make no
        // SSjob, which tg's MC always has; without one there is no tracker list to add to.
        if (SSjob != null)
        {
            SSjob.latejoin_trackers ??= Dm.list();
            Dm.append(ref SSjob.latejoin_trackers, loc);
        }
        return INITIALIZE_HINT_QDEL;
    }
}

/**
 * Generic event spawn points
 *
 * These are placed in locales where there are likely to be players, and places which are identifiable at a glance -
 * Such as public hallways, department rooms, head of staff offices, and non-generic maintenance locations
 *
 * Used in events to cause effects in locations where it is likely to effect players
 */
// tg: code/game/objects/effects/landmarks.dm:473  (the M7 delamination slice: the delamination's anomalies, rift and crystal mass)
public partial class obj_effect_landmark_event_spawn : obj_effect_landmark
{
    // tg: code/game/objects/effects/landmarks.dm:477
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.generic_event_spawns, this);
        return dot;
    }

    // tg: code/game/objects/effects/landmarks.dm:481
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.generic_event_spawns, this);
        return base.Destroy(force);
    }
}

// The nuclear operatives' spawn points (syndicate N1b, D316)
public partial class datum_controller_global_vars
{
    /// Places to put midround nukeops (normal)
    // tg: code/game/objects/effects/landmarks.dm:268  GLOBAL_LIST_EMPTY(nukeop_base_start)
    public DmList nukeop_base_start;
    /// Places to put midround nukeops (leader)
    // tg: code/game/objects/effects/landmarks.dm:270  GLOBAL_LIST_EMPTY(nukeop_base_leader_start)
    public DmList nukeop_base_leader_start;
    /// Places to put nukeops overwatch agents
    // tg: code/game/objects/effects/landmarks.dm:272  GLOBAL_LIST_EMPTY(nukeop_base_overwatch_start)
    public DmList nukeop_base_overwatch_start;
    /// Places to spawn nukeops on roundstart
    // tg: code/game/objects/effects/landmarks.dm:274  GLOBAL_LIST_EMPTY(nukeop_elevator_start)
    public DmList nukeop_elevator_start;

    // tg: code/game/objects/effects/landmarks.dm:268  (GLOBAL_MANAGED(nukeop_base_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnukeop_base_start()
    {
        nukeop_base_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "nukeop_base_start");
    }

    // tg: code/game/objects/effects/landmarks.dm:270  (GLOBAL_MANAGED(nukeop_base_leader_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnukeop_base_leader_start()
    {
        nukeop_base_leader_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "nukeop_base_leader_start");
    }

    // tg: code/game/objects/effects/landmarks.dm:272  (GLOBAL_MANAGED(nukeop_base_overwatch_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnukeop_base_overwatch_start()
    {
        nukeop_base_overwatch_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "nukeop_base_overwatch_start");
    }

    // tg: code/game/objects/effects/landmarks.dm:274  (GLOBAL_MANAGED(nukeop_elevator_start, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnukeop_elevator_start()
    {
        nukeop_elevator_start = Dm.list();
        Dm.append(ref gvars_datum_init_order, "nukeop_elevator_start");
    }
}

// tg: code/game/objects/effects/landmarks.dm:276
public partial class obj_effect_landmark_start_nukeop_base : obj_effect_landmark_start
{
    // tg: code/game/objects/effects/landmarks.dm:281
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        DmList add_to = get_global_list() as DmList;
        add_to.Add(loc);
        return INITIALIZE_HINT_QDEL;
    }

    // tg: code/game/objects/effects/landmarks.dm:287
    public virtual object get_global_list()
    {
        return GLOB.nukeop_base_start;
    }
}

// tg: code/game/objects/effects/landmarks.dm:290
public partial class obj_effect_landmark_start_nukeop_base_leader : obj_effect_landmark_start_nukeop_base
{
    // tg: code/game/objects/effects/landmarks.dm:295
    public override object get_global_list()
    {
        return GLOB.nukeop_base_leader_start;
    }
}

// tg: code/game/objects/effects/landmarks.dm:298
public partial class obj_effect_landmark_start_nukeop_base_overwatch : obj_effect_landmark_start_nukeop_base
{
    // tg: code/game/objects/effects/landmarks.dm:303
    public override object get_global_list()
    {
        return GLOB.nukeop_base_overwatch_start;
    }
}

// tg: code/game/objects/effects/landmarks.dm:306
public partial class obj_effect_landmark_start_nukeop_elevator : obj_effect_landmark_start
{
    // tg: code/game/objects/effects/landmarks.dm:311
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.nukeop_elevator_start, loc);
        return INITIALIZE_HINT_QDEL;
    }
}

// Gaps R2 (D327): the hangover spawn whole, the Hangover and the Employee Birthday station traits' party and hangover debris and the
// hangover of who joins here (its drunkenness and drugginess since gaps RG1). The personalities are not ported: nobody is teetotal or
// bibulous.
// tg: code/game/objects/effects/landmarks.dm:514
public partial class obj_effect_landmark_start_hangover : obj_effect_landmark_start
{
    // tg: code/game/objects/effects/landmarks.dm:525
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/effects/landmarks.dm:529
    public override object Destroy(float? force = null)
    {
        hangover_debris = null;
        party_debris = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/landmarks.dm:534
    public override void LateInitialize(params object[] __extra)
    {
        if (HAS_TRAIT(SSstation, STATION_TRAIT_BIRTHDAY))
        {
            Dm.append(ref party_debris, Dm.@new<obj_effect_decal_cleanable_confetti>(get_turf(this))); //a birthday celebration can also be a hangover
            DmList bonus_confetti = GLOB.alldirs;
            foreach (object confettis in Dm.each(bonus_confetti))
            {
                turf party_turf_to_spawn_on = get_step(this, Dm.num(confettis));
                if (!isopenturf(party_turf_to_spawn_on))
                    continue;
                bool dense_object = false;
                foreach (atom content in Dm.each<atom>(party_turf_to_spawn_on))
                {
                    if (Dm.truthy(content.density))
                    {
                        dense_object = true;
                        break;
                    }
                }
                if (dense_object)
                    continue;
                if (prob(50))
                    Dm.append(ref party_debris, Dm.@new<obj_effect_decal_cleanable_confetti>(party_turf_to_spawn_on));
                if (prob(10))
                    Dm.append(ref party_debris, Dm.@new<obj_item_toy_balloon>(party_turf_to_spawn_on));
            }
        }
        if (!HAS_TRAIT(SSstation, STATION_TRAIT_HANGOVER))
            return;
        if (prob(60))
            Dm.append(ref hangover_debris, Dm.@new<obj_effect_decal_cleanable_vomit>(get_turf(this)));
        if (prob(70))
        {
            float bottle_count = rand(1, 3);
            for (float index = 1; index <= bottle_count; index++)
            {
                turf turf_to_spawn_on = get_step(this, Dm.num(pick(GLOB.alldirs)));
                if (!isopenturf(turf_to_spawn_on))
                    continue;
                bool dense_object = false;
                foreach (atom content in Dm.each<atom>(turf_to_spawn_on.contents))
                {
                    if (Dm.truthy(content.density))
                    {
                        dense_object = true;
                        break;
                    }
                }
                if (dense_object)
                    continue;
                Dm.append(ref hangover_debris, Dm.@new<obj_item_reagent_containers_cup_glass_bottle_beer_almost_empty>(turf_to_spawn_on));
            }
        }
    }

    ///Spawns the mob with some drugginess/drunkenness, and some disgust.
    // tg: code/game/objects/effects/landmarks.dm:573
    public virtual async DmTask make_hungover(mob hangover_mob = null)
    {
        if (!iscarbon(hangover_mob))
            return;

        var spawned_carbon = (mob_living_carbon)hangover_mob;
        if (Dm.truthy(LAZYACCESS(spawned_carbon.personalities, typeof(datum_personality_teetotal)))) // HAS_PERSONALITY (__DEFINES/mood.dm:109)
        {
            await spawned_carbon.add_mood_event("hungover", typeof(datum_mood_event_teetotal_hangover));
            spawned_carbon.adjust_disgust(30);
            return;
        }

        await spawned_carbon.set_resting(TRUE, silent: TRUE);
        if (Dm.truthy(LAZYACCESS(spawned_carbon.personalities, typeof(datum_personality_bibulous)))) // HAS_PERSONALITY
        {
            await spawned_carbon.add_mood_event("hungover", typeof(datum_mood_event_bibulous_hangover));
            await spawned_carbon.adjust_drunk_effect(rand(30, 60));
            await spawned_carbon.adjust_drugginess(rand(10 * SECONDS, 20 * SECONDS));
            spawned_carbon.adjust_disgust(rand(30, 70));
            return;
        }

        await spawned_carbon.add_mood_event("hungover", typeof(datum_mood_event_normal_hangover));
        spawned_carbon.adjust_disgust(rand(10, 50)); //How hungover are you?
        if (prob(50))
            await spawned_carbon.adjust_drunk_effect(rand(15, 25));
        else
            await spawned_carbon.adjust_drugginess(rand(30 * SECONDS, 40 * SECONDS));
    }

    // tg: code/game/objects/effects/landmarks.dm:597
    public override async DmTask<object> JoinPlayerHere(object joining_mob = null, object buckle = null)
    {
        var created_joining_mob = await base.JoinPlayerHere(joining_mob, buckle) as mob;
        await make_hungover(created_joining_mob);
        foreach (obj_structure_closet closet in Dm.each<obj_structure_closet>(get_turf(this)))
        {
            if (Dm.truthy(closet.opened))
                continue;
            await created_joining_mob.forceMove(closet);
            return created_joining_mob;
        }
        return created_joining_mob;
    }
}

// tg: code/game/objects/effects/landmarks.dm:11 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_effect_landmark : obj_effect
{
    // tg: code/game/objects/effects/landmarks.dm:11
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/objects/effects/landmarks.dm:14
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}

//Department Security spawns

// tg: code/game/objects/effects/landmarks.dm:226
public partial class obj_effect_landmark_start_depsec : obj_effect_landmark_start
{
    // tg: code/game/objects/effects/landmarks.dm:231
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        LAZYADDASSOCLIST(ref GLOB.department_security_spawns, department, this);
        return dot;
    }

    // tg: code/game/objects/effects/landmarks.dm:235
    public override object Destroy(float? force = null)
    {
        LAZYREMOVEASSOC(ref GLOB.department_security_spawns, department, this);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/effects/landmarks.dm:419  (syndicate X1a: the contractor's victims and the ninja's captives are held here)
public partial class obj_effect_landmark_holding_facility : obj_effect_landmark
{
    // tg: code/game/objects/effects/landmarks.dm:423
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.holdingfacility, loc);
        return INITIALIZE_HINT_QDEL;
    }
}
