// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/chasm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304). A chasm's fishing spot (the rescue hook's catch, :56) waits for fishing (ROADMAP section 4).

// Used by /turf/open/chasm and subtypes to implement the "dropping" mechanic
// tg: code/datums/components/chasm.dm:2
public partial class datum_component_chasm : datum_component
{
    private const int CHASM_NOT_DROPPING = 0;
    private const int CHASM_DROPPING = 1;
    ///Doesn't drop the movable, but registers a few signals to try again if the conditions change.
    private const int CHASM_REGISTER_SIGNALS = 2;

    /// List of refs to falling objects -> how many levels deep we've fallen
    private static DmList falling_atoms
    {
        get => TgState.Current.datum_component_chasm__falling_atoms;
        set => TgState.Current.datum_component_chasm__falling_atoms = value;
    }

    private static DmList forbidden_types => TgState.Current.datum_component_chasm__forbidden_types ??= typecacheof(Dm.list(
        typeof(obj_docking_port),
        typeof(obj_effect_abstract),
        typeof(obj_effect_atmos_shield),
        typeof(obj_effect_collapse),
        typeof(obj_effect_constructing_effect),
        typeof(obj_effect_dummy_phased_mob),
        typeof(obj_effect_ebeam),
        typeof(obj_effect_fishing_float),
        typeof(obj_effect_hotspot),
        typeof(obj_effect_landmark),
        typeof(obj_effect_light_emitter_tendril),
        typeof(obj_effect_mapping_helpers),
        typeof(obj_effect_particle_effect_ion_trails),
        typeof(obj_effect_particle_effect_sparks),
        typeof(obj_effect_portal),
        typeof(obj_effect_projectile),
        typeof(obj_effect_spectre_of_resurrection),
        typeof(obj_effect_temp_visual),
        typeof(obj_effect_wisp),
        typeof(obj_energy_ball),
        typeof(obj_narsie),
        typeof(obj_projectile),
        typeof(obj_singularity),
        typeof(obj_structure_lattice),
        typeof(obj_structure_stone_tile),
        typeof(obj_structure_ore_vent))) as DmList;

    // tg: code/datums/components/chasm.dm:39
    [DmParams("target", "mapload")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var target = Dm.arg(__extra, 0) as turf;
        object mapload = Dm.arg(__extra, 1);
        if (!isturf(parent))
            return COMPONENT_INCOMPATIBLE;
        RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_CHASM_STOPPED), nameof(on_chasm_stopped));
        RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_CHASM_STOPPED), nameof(on_chasm_no_longer_stopped));
        target_turf = target;
        ADD_TRAIT(parent, TRAIT_AI_AVOID_TURF, REF(this));
        RegisterSignal(parent, COMSIG_ATOM_ABSTRACT_ENTERED, nameof(entered));
        RegisterSignal(parent, COMSIG_ATOM_ABSTRACT_EXITED, nameof(exited));
        RegisterSignal(parent, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(initialized_on));
        RegisterSignal(parent, COMSIG_ATOM_INTERCEPT_TELEPORTING, nameof(block_teleport));
        //allow catwalks to give the turf the CHASM_STOPPED trait before dropping stuff when the turf is changed.
        //otherwise don't do anything because turfs and areas are initialized before movables.
        if (!Dm.truthy(mapload))
            _addtimer(Dm.@new<datum_callback>(this, nameof(drop_stuff)), 0);
        else if (HAS_TRAIT(parent, TRAIT_CHASM_STOPPED)) // The lattice initialized before we could set our signals
            on_chasm_stopped(parent);
        // tg: :56  parent.AddComponent(/datum/component/fishing_spot, GLOB.preset_fish_sources[/datum/fish_source/chasm]): fishing is not in the core version (ROADMAP section 4)
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/chasm.dm:58
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        REMOVE_TRAIT(parent, TRAIT_AI_AVOID_TURF, REF(this));
        storage = null;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/chasm.dm:62
    [SignalHandler]
    public virtual void entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        drop_stuff();
    }

    // tg: code/datums/components/chasm.dm:66
    [SignalHandler]
    public virtual void exited(datum source = null, atom_movable exited = null)
    {
        UnregisterSignal(exited, Dm.list(COMSIG_MOVETYPE_FLAG_DISABLED, COMSIG_LIVING_SET_BUCKLED, COMSIG_MOVABLE_THROW_LANDED));
    }

    // tg: code/datums/components/chasm.dm:70
    [SignalHandler]
    public virtual void initialized_on(datum source = null, atom_movable movable = null, object mapload = null)
    {
        drop_stuff(movable);
    }

    // tg: code/datums/components/chasm.dm:74
    [SignalHandler]
    public virtual object block_teleport()
    {
        return COMPONENT_INTERCEPT_TELEPORT;
    }

    // tg: code/datums/components/chasm.dm:77
    [SignalHandler]
    public virtual void on_chasm_stopped(datum source = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
        foreach (atom_movable movable in Dm.each<atom_movable>(source).ToList())
            UnregisterSignal(movable, Dm.list(COMSIG_MOVETYPE_FLAG_DISABLED, COMSIG_LIVING_SET_BUCKLED, COMSIG_MOVABLE_THROW_LANDED));
    }

    // tg: code/datums/components/chasm.dm:83
    [SignalHandler]
    public virtual void on_chasm_no_longer_stopped(datum source = null)
    {
        RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(entered));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(exited));
        RegisterSignal(parent, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(initialized_on));
        drop_stuff();
    }

    // tg: code/datums/components/chasm.dm:95
    [SignalHandler]
    public virtual void drop_stuff(atom_movable dropped_thing = null)
    {
        if (HAS_TRAIT(parent, TRAIT_CHASM_STOPPED))
            return;
        var atom_parent = (atom)parent;
        object to_check = dropped_thing != null ? Dm.list(dropped_thing) : atom_parent.contents;
        foreach (atom_movable thing in Dm.each<atom_movable>(to_check).ToList())
        {
            int dropping = droppable(thing);
            switch (dropping)
            {
                case CHASM_DROPPING:
                    Waiting.Detach(drop(thing)); // INVOKE_ASYNC(src, PROC_REF(drop), thing)
                    break;
                case CHASM_REGISTER_SIGNALS:
                    RegisterSignals(thing, Dm.list(COMSIG_MOVETYPE_FLAG_DISABLED, COMSIG_LIVING_SET_BUCKLED, COMSIG_MOVABLE_THROW_LANDED), nameof(drop_stuff), TRUE);
                    break;
            }
        }
    }

    // tg: code/datums/components/chasm.dm:108
    public virtual int droppable(atom_movable dropped_thing = null)
    {
        object falling_ref = WEAKREF(dropped_thing);
        // avoid an infinite loop, but allow falling a large distance
        object falling_atom = falling_atoms?[falling_ref]; // LAZYACCESS
        if (Dm.truthy(falling_atom) && Dm.num(falling_atom) > 30)
            return CHASM_NOT_DROPPING;
        if (Dm.truthy(is_type_in_typecache(dropped_thing, forbidden_types)) || (!isliving(dropped_thing) && !isobj(dropped_thing)))
            return CHASM_NOT_DROPPING;
        if (Dm.truthy(dropped_thing.throwing) || Dm.truthy(Dm.bitand(dropped_thing.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return CHASM_REGISTER_SIGNALS;
        foreach (atom thing_to_check in Dm.each<atom>(parent))
        {
            if (HAS_TRAIT(thing_to_check, TRAIT_CHASM_STOPPER))
                return CHASM_NOT_DROPPING;
        }

        if (!ismob(dropped_thing))
            return CHASM_DROPPING;

        //Flies right over the chasm
        var victim = (mob)dropped_thing;
        if (victim.buckled != null && droppable(victim.buckled) != CHASM_DROPPING)
            return CHASM_REGISTER_SIGNALS;
        return CHASM_DROPPING;
    }

    // tg: code/datums/components/chasm.dm:135
    public virtual async DmTask drop(atom_movable dropped_thing = null)
    {
        var falling_ref = WEAKREF(dropped_thing) as datum_weakref;
        DmList falling = falling_atoms;
        //Make sure the item is still there after our sleep
        if (dropped_thing == null || falling_ref?.resolve() == null)
        {
            LAZYREMOVE(ref falling, falling_ref);
            falling_atoms = falling;
            return;
        }

        LAZYSET(ref falling, falling_ref, Dm.num(falling?[falling_ref] ?? 0) + 1);
        falling_atoms = falling;
        turf below_turf = target_turf;
        var parent = (atom)this.parent;

        if (Dm.num(falling_atoms?[falling_ref]) > 1)
            return; // We're already handling this

        // SEND_SIGNAL (__DEFINES/dcs/helpers.dm), expanded inline
        object dropped_signal = Dm.truthy(dropped_thing._listen_lookup?[COMSIG_MOVABLE_CHASM_DROPPED]) ? dropped_thing._SendSignal(COMSIG_MOVABLE_CHASM_DROPPED, Dm.list(dropped_thing, parent)) : NONE;
        if (Dm.truthy(Dm.bitand(dropped_signal, COMPONENT_NO_CHASM_DROP)))
        {
            Forget(falling_ref);
            return;
        }

        // Free (if possible) and drop all buckled mobs separately, so drivers can escape their doomed vehicle if they're not glued to it
        foreach (mob_living buckled in Dm.each<mob_living>(dropped_thing.buckled_mobs).ToList())
        {
            await dropped_thing.unbuckle_mob(buckled);
            drop_stuff(buckled);
        }

        if (below_turf != null)
        {
            if (HAS_TRAIT(dropped_thing, TRAIT_CHASM_DESTROYED))
            {
                qdel(dropped_thing);
                return;
            }

            // send to the turf below
            dropped_thing.visible_message(span_boldwarning(Dm.text("[] falls into []!", dropped_thing, parent)), span_userdanger(Dm.text("[]", fall_message)));
            below_turf.visible_message(span_boldwarning(Dm.text("[] falls from above!", dropped_thing)));
            await dropped_thing.forceMove(below_turf);
            if (isliving(dropped_thing))
            {
                var fallen = (mob_living)dropped_thing;
                await fallen.Paralyze(100);
                await fallen.adjust_brute_loss(30);
            }
            Forget(falling_ref);
            return;
        }

        // send to oblivion

        if (isliving(dropped_thing))
        {
            var falling_mob = (mob_living)dropped_thing;
            ADD_TRAIT(falling_mob, TRAIT_NO_TRANSFORM, REF(this));
            await falling_mob.Stun(20 * SECONDS, ignore_canstun: TRUE);

            if (HAS_MIND_TRAIT(falling_mob, TRAIT_NAIVE))
            {
                falling_mob.do_alert_animation();
                dropped_thing.visible_message(span_boldwarning(Dm.text("[] kicks [] legs in the air, as if running in place!", dropped_thing, dropped_thing.p_their())));
                dropped_thing.Shake(1, 0, 2 * SECONDS, 0.3f * SECONDS);
                await sleep(3 * SECONDS);
            }

            if (get_turf(falling_mob) != get_turf(parent))
            {
                REMOVE_TRAIT(falling_mob, TRAIT_NO_TRANSFORM, REF(this));
                await falling_mob.Paralyze(17 * SECONDS, ignore_canstun: TRUE); // Wow nice job
                Forget(falling_ref);
                return;
            }
        }

        dropped_thing.visible_message(span_boldwarning(Dm.text("[] falls into []!", dropped_thing, parent)), span_userdanger(Dm.text("[]", oblivion_message)));

        object oldtransform = dropped_thing.transform;
        object oldcolor = dropped_thing.color;
        float oldalpha = dropped_thing.alpha;
        float oldoffset = dropped_thing.pixel_y;

        Dm.animate(dropped_thing, Dm.named("transform", Dm.sub(matrix(), matrix())), Dm.named("alpha", 0), Dm.named("color", rgb(0, 0, 0)), Dm.named("time", 10));
        for (int i = 1; i <= 5; i++)
        {
            //Make sure the item is still there after our sleep
            if (dropped_thing == null || QDELETED(dropped_thing))
                return;
            dropped_thing.pixel_y--;
            await sleep(0.2f * SECONDS);
        }

        //Make sure the item is still there after our sleep
        if (dropped_thing == null || QDELETED(dropped_thing))
            return;

        if (HAS_TRAIT(dropped_thing, TRAIT_CHASM_DESTROYED))
        {
            qdel(dropped_thing);
            return;
        }

        storage ??= locate<obj_effect_abstract_chasm_storage>(parent) ?? Dm.@new<obj_effect_abstract_chasm_storage>(parent);

        if (Dm.truthy(storage.contains(dropped_thing)))
        {
            Forget(falling_ref);
            return;
        }

        dropped_thing.alpha = oldalpha;
        dropped_thing.color = oldcolor;
        dropped_thing.transform = oldtransform;
        dropped_thing.pixel_y = oldoffset;

        if (!Dm.truthy(await dropped_thing.forceMove(storage)))
        {
            parent.visible_message(span_boldwarning(Dm.text("[] spits out []!", parent, dropped_thing)));
            await dropped_thing.throw_at((atom)get_edge_target_turf((atom)parent, Dm.num(pick(GLOB.alldirs))), Dm.rand(1, 10), Dm.rand(1, 10));
        }
        else if (isliving(dropped_thing))
        {
            var fallen_mob = (mob_living)dropped_thing;
            REMOVE_TRAIT(fallen_mob, TRAIT_NO_TRANSFORM, REF(this));
            if (!Dm.eq(fallen_mob.stat, DEAD))
            {
                fallen_mob.investigate_log("has died from falling into a chasm.", INVESTIGATE_DEATHS);
                if (issilicon(fallen_mob))
                {
                    //Silicons are held together by hopes and dreams, unfortunately, I'm having a nightmare
                    var fallen_borg = (mob_living_silicon_robot)fallen_mob;
                    fallen_borg.mmi = null;
                }
                await fallen_mob.death(TRUE);
                await fallen_mob.apply_damage(300);
            }
        }

        Forget(falling_ref);
    }

    // LAZYREMOVE(falling_atoms, falling_ref) on the static list
    private static void Forget(object falling_ref)
    {
        DmList falling = falling_atoms;
        LAZYREMOVE(ref falling, falling_ref);
        falling_atoms = falling;
    }

    /**
     * Called when something has left the chasm depths storage.
     * Arguments
     *
     * * source - Chasm object holder.
     * * gone - Item which has just left the chasm contents.
     */
    // tg: code/datums/components/chasm.dm:253
    [SignalHandler]
    public virtual void left_chasm(atom source = null, atom_movable gone = null)
    {
        UnregisterSignal(gone, COMSIG_LIVING_REVIVE);
    }
}

// tg: code/datums/components/chasm.dm:258
public partial class datum_controller_global_vars
{
    // tg: code/datums/components/chasm.dm:258  GLOBAL_LIST_EMPTY(chasm_fallen_mobs) ///Global list needed to let fishermen with a rescue hook fish fallen mobs from any place
    public DmList chasm_fallen_mobs;

    // tg: code/datums/components/chasm.dm:258  (GLOBAL_MANAGED(chasm_fallen_mobs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalchasm_fallen_mobs()
    {
        chasm_fallen_mobs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "chasm_fallen_mobs");
    }
}

/**
 * An abstract object which is basically just a bag that the chasm puts people inside
 */
// tg: code/datums/components/chasm.dm:263
public partial class obj_effect_abstract_chasm_storage : obj_effect_abstract
{
    private const string CHASM_TRAIT = "chasm trait";

    // tg: code/datums/components/chasm.dm:269
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SECLUDED_LOCATION, INNATE_TRAIT);
        return dot;
    }

    // tg: code/datums/components/chasm.dm:273
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (isliving(arrived))
        {
            //Mobs that have fallen in reserved area should be deleted to avoid fishing stuff from the deathmatch or VR.
            if (Dm.truthy(is_reserved_level(loc.z)) && !istype(get_area(loc), typeof(area_shuttle)))
            {
                qdel(arrived);
                return dot;
            }
            RegisterSignal(arrived, COMSIG_LIVING_REVIVE, nameof(on_revive));
            object category = get_chasm_category(loc as turf);
            DmList fallen = GLOB.chasm_fallen_mobs[category] as DmList;
            LAZYADD(ref fallen, arrived);
            GLOB.chasm_fallen_mobs[category] = fallen;
        }
        return dot;
    }

    // tg: code/datums/components/chasm.dm:283
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (isliving(gone))
        {
            UnregisterSignal(gone, COMSIG_LIVING_REVIVE);
            object category = get_chasm_category(loc as turf);
            DmList fallen = GLOB.chasm_fallen_mobs[category] as DmList;
            LAZYREMOVE(ref fallen, gone);
            GLOB.chasm_fallen_mobs[category] = fallen;
        }
        return dot;
    }

    // tg: code/datums/components/chasm.dm:289
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        object old_cat = get_chasm_category(old_turf);
        object new_cat = get_chasm_category(new_turf);
        DmList mobs = Dm.list();
        foreach (mob fallen in Dm.each<mob>(this))
            Dm.append(ref mobs, fallen);
        DmList old_list = GLOB.chasm_fallen_mobs[old_cat] as DmList;
        LAZYREMOVE(ref old_list, mobs);
        GLOB.chasm_fallen_mobs[old_cat] = old_list;
        DmList new_list = GLOB.chasm_fallen_mobs[new_cat] as DmList;
        LAZYADD(ref new_list, mobs);
        GLOB.chasm_fallen_mobs[new_cat] = new_list;
    }

    /**
     * Called if something comes back to life inside the pit. Expected sources are badmins and changelings.
     * Ethereals should take enough damage to be smashed and not revive.
     * Arguments
     * escapee - Lucky guy who just came back to life at the bottom of a hole.
     */
    // tg: code/datums/components/chasm.dm:329
    [SignalHandler]
    public virtual void on_revive(mob_living escapee = null)
    {
        turf turf = get_turf(this);
        if (turf.GetComponent(typeof(datum_component_chasm)) != null)
        {
            turf.visible_message(span_boldwarning(Dm.text("After a long climb, [] leaps out of []!", escapee, turf)));
        }
        else
        {
            playsound(turf, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
            turf.visible_message(span_boldwarning(Dm.text("[] busts through [], leaping out of the chasm below", escapee, turf)));
            Waiting.NoSleep(turf.ScrapeAway(2, flags: CHANGETURF_INHERIT_AIR));
        }
        ADD_TRAIT(escapee, TRAIT_MOVE_FLYING, CHASM_TRAIT); //Otherwise they instantly fall back in
        Waiting.NoSleep(escapee.forceMove(turf));
        Waiting.NoSleep(escapee.throw_at((atom)get_edge_target_turf(turf, Dm.num(pick(GLOB.alldirs))), Dm.rand(1, 10), Dm.rand(1, 10)));
        REMOVE_TRAIT(escapee, TRAIT_MOVE_FLYING, CHASM_TRAIT);
        Waiting.NoSleep(escapee.Paralyze(20 * SECONDS, TRUE));
        UnregisterSignal(escapee, COMSIG_LIVING_REVIVE);
    }
}

public static partial class GlobalProcs
{
    /**
     * Returns a key to store, remove and access fallen mobs depending on the z-level.
     * This stops rescuing people from places that are waaaaaaaay too far-fetched.
     */
    // tg: code/datums/components/chasm.dm:303
    public static object get_chasm_category(turf turf = null)
    {
        object z_level = turf?.z;
        var area = get_area(turf) as area;
        if (istype(area, typeof(area_shuttle))) //shuttle move between z-levels, so they're a special case.
            return area;

        if (Dm.truthy(SSmapping.level_trait(z_level, ZTRAIT_AWAY))) // is_away_level (__HELPERS/_level_traits.dm), expanded inline
            return ZTRAIT_AWAY;
        if (Dm.truthy(is_mining_level(z_level)))
            return ZTRAIT_MINING;
        if (Dm.truthy(is_station_level(z_level)))
            return ZTRAIT_STATION;
        if (Dm.truthy(is_centcom_level(z_level)))
            return ZTRAIT_CENTCOM;
        if (Dm.truthy(is_reserved_level(z_level)))
            return ZTRAIT_RESERVED;

        return ZTRAIT_SPACE_RUINS;
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_component_chasm__falling_atoms;
    internal DmList datum_component_chasm__forbidden_types;
}
