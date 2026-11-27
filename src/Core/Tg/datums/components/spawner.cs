// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/spawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/components/spawner.dm:3
public partial class datum_component_spawner : datum_component
{
    // tg: code/datums/components/spawner.dm:47
    [DmParams("spawn_types", "spawn_time", "max_spawned", "max_spawn_per_attempt", "max_spawn_types_per_attempt", "faction", "spawn_text", "spawn_callback", "spawn_distance", "spawn_distance_exclude", "initial_spawn_delay", "spawner_logic", "max_waves", "effect", "sound_effect", "spawn_windup", "linked_mob")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object spawn_types = Dm.arg(__extra, 0);
        object spawn_time = Dm.arg(__extra, 1);
        object max_spawned = Dm.arg(__extra, 2);
        object max_spawn_per_attempt = Dm.arg(__extra, 3);
        object max_spawn_types_per_attempt = Dm.arg(__extra, 4);
        DmList faction = Dm.arg<DmList>(__extra, 5);
        object spawn_text = Dm.arg(__extra, 6);
        datum_callback spawn_callback = Dm.arg<datum_callback>(__extra, 7);
        object spawn_distance = Dm.arg(__extra, 8);
        object spawn_distance_exclude = Dm.arg(__extra, 9);
        object initial_spawn_delay = Dm.arg(__extra, 10);
        object spawner_logic = Dm.arg(__extra, 11);
        object max_waves = Dm.arg(__extra, 12);
        Type effect = Dm.arg<Type>(__extra, 13);
        object sound_effect = Dm.arg(__extra, 14);
        object spawn_windup = Dm.arg(__extra, 15);
        mob linked_mob = Dm.arg<mob>(__extra, 16);
        spawn_types ??= Dm.list();
        spawn_time ??= 30 * SECONDS;
        max_spawned ??= 5;
        max_spawn_per_attempt ??= 1;
        max_spawn_types_per_attempt ??= 1;
        faction ??= Dm.list(FACTION_MINING);
        spawn_distance ??= 1;
        spawn_distance_exclude ??= 0;
        initial_spawn_delay ??= 0 * SECONDS;
        spawner_logic ??= SPAWN_CONTINUOUS_BEHAVIOR;
        max_waves ??= 1;
        spawn_windup ??= 0.5f * SECONDS;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        if (!islist(spawn_types))
            throw Dm.CRASH("invalid spawn_types to spawn specified for spawner component!", "code/datums/components/spawner.dm", 70);

        this.spawn_time = spawn_time;
        this.spawn_types = spawn_types as DmList;
        this.faction = faction;
        this.spawn_text = spawn_text;
        this.max_spawned = max_spawned;
        this.spawn_callback = spawn_callback;
        this.max_spawn_per_attempt = max_spawn_per_attempt;
        this.max_spawn_types_per_attempt = max_spawn_types_per_attempt;
        this.spawn_distance = spawn_distance;
        this.spawn_distance_exclude = spawn_distance_exclude;
        this.linked_mob = linked_mob;
        // If set, doesn't instantly spawn a creature when the spawner component is applied.
        if (Dm.truthy(initial_spawn_delay))
            spawn_delay = world.time + Dm.num(spawn_time); // COOLDOWN_START(src, spawn_delay, spawn_time)
        this.spawner_logic = spawner_logic;
        if (Dm.eq(spawner_logic, SPAWN_BY_WAVE_BEHAVIOR))
            this.max_waves = max_waves;
        this.effect = effect;
        if (effect != null)
            this.spawn_windup = spawn_windup;
        this.sound_effect = sound_effect;

        RegisterSignals(parent, Dm.list(COMSIG_QDELETING), nameof(stop_spawning));
        if (linked_mob != null)
            RegisterSignals(linked_mob, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH), nameof(stop_spawning));
        if (Dm.num(spawn_time) < 2 * SECONDS)
            START_PROCESSING(SSfastprocess, this);
        else
            START_PROCESSING(SSprocessing, this);
        return null;
    }

    // tg: code/datums/components/spawner.dm:99
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        try_spawn_mob();
        return null;
    }

    /// Stop spawning mobs
    // tg: code/datums/components/spawner.dm:103
    [SignalHandler]
    public virtual void stop_spawning(object force = null)
    {
        STOP_PROCESSING(SSprocessing, this);
        spawned_things = Dm.list();
        if (Dm.truthy(parent._listen_lookup?[COMSIG_VENT_WAVE_CONCLUDED]))
            parent._SendSignal(COMSIG_VENT_WAVE_CONCLUDED, Dm.list(parent));
        qdel(this);
    }

    /// Determine if we can spawn a mob based on the current spawn logic.
    // tg: code/datums/components/spawner.dm:111
    public virtual object check_spawn_availability(object mobs_spawned = null)
    {
        if (!Dm.truthy(spawner_logic))
            throw Dm.CRASH("A spawner was created without selecting it's spawning logic!", "code/datums/components/spawner.dm", 113);

        validate_references();
        object sw__1 = spawner_logic;
        if (Dm.eq(sw__1, SPAWN_CONTINUOUS_BEHAVIOR))
        {
            if (Dm.num(mobs_spawned) >= Dm.num(max_spawned))
                return FALSE;
        }
        else if (Dm.eq(sw__1, SPAWN_BY_WAVE_BEHAVIOR))
        {
            if (Dm.truthy(mobs_spawned)) //If any mobs are still alive.
                return FALSE;
            if (completed_waves >= Dm.num(max_waves))
            {
                stop_spawning();
                return FALSE;
            }
            completed_waves++;
            atom spawner_atom = parent as atom;
            spawner_atom.balloon_alert_to_viewers(Dm.text("wave []/[]", completed_waves, max_waves));
        }
        return TRUE;
    }

    /// Try to create a new mob
    // tg: code/datums/components/spawner.dm:132
    public virtual void try_spawn_mob()
    {
        if (!Dm.truthy(Dm.length(spawn_types)))
            return;
        if (!(spawn_delay <= world.time)) // COOLDOWN_FINISHED(src, spawn_delay)
            return;
        float spawned_total = Dm.length(spawned_things);
        if (!Dm.truthy(check_spawn_availability(spawned_total)))
            return;
        atom spawner = parent as atom;
        spawn_delay = world.time + Dm.num(spawn_time); // COOLDOWN_START(src, spawn_delay, spawn_time)
        DmList local_spawn_types = spawn_types.Copy();
        for (float i = 1, i__end = Dm.num(max_spawn_types_per_attempt); i <= i__end; i++)
        {
            Type chosen_mob_type = pick_n_take(local_spawn_types) as Type; //This way we avoid duplicates when spawning.
            float adjusted_spawn_count = 1;
            float max_spawn_this_attempt = Dm.min(Dm.num(max_spawn_per_attempt), Dm.num(max_spawned) - spawned_total);
            if (max_spawn_this_attempt > 1)
                adjusted_spawn_count = rand(1, max_spawn_this_attempt);
            for (float j = 1, j__end = adjusted_spawn_count; j <= j__end; j++)
            {
                atom created = null;
                turf picked_spot = select_turf(spawner) as turf;
                if (effect == null || (effect != null && !Dm.truthy(spawn_windup)))
                    created = Dm.@new(chosen_mob_type, picked_spot) as atom;
                else
                {
                    Dm.@new(effect, picked_spot);
                    _addtimer(Dm.@new<datum_callback>(this, nameof(delayed_mob_spawn), picked_spot, chosen_mob_type, spawner), Dm.num(spawn_windup), file: "code/datums/components/spawner.dm", line: 156);
                }

                if (created != null)
                    setup_spawned_mob(created, spawner);
            }
        }
        if (Dm.truthy(sound_effect))
            playsound(parent as atom, sound_effect, 40);
    }

    /**
     * This proc determines the tile that a spawner will place a mob on.
     */
    // tg: code/datums/components/spawner.dm:167
    public virtual object select_turf(atom spawner = null)
    {
        turf picked_spot = null;
        if (Dm.num(spawn_distance) == 1)
            picked_spot = spawner.loc as turf;
        else if (Dm.num(spawn_distance) >= 1 && Dm.num(spawn_distance_exclude) >= 1)
        {
            picked_spot = pick(turf_peel(spawn_distance, spawn_distance_exclude, spawner.loc, view_based: TRUE, reject_dense: TRUE)) as turf;
            if (picked_spot == null)
                picked_spot = pick(circle_range_turfs(spawner.loc, spawn_distance)) as turf;
            if (picked_spot == spawner.loc)
            {
                if (Dm.truthy(spawner._listen_lookup?[COMSIG_SPAWNER_SPAWNED_DEFAULT]))
                    spawner._SendSignal(COMSIG_SPAWNER_SPAWNED_DEFAULT, Dm.list(spawner));
            }
        }
        else if (Dm.num(spawn_distance) >= 1)
            picked_spot = pick(circle_range_turfs(spawner.loc, spawn_distance)) as turf;
        return picked_spot;
    }

    /**
     * Adds handling to the created mob from the spawner component.
     */
    // tg: code/datums/components/spawner.dm:190
    public virtual void delayed_mob_spawn(turf picked_spot = null, Type chosen_mob_type = null, atom spawner = null)
    {
        if (picked_spot == null)
            throw Dm.CRASH("Incorrect parameters for delayed mob spawn - no picked spot to spawn!", "code/datums/components/spawner.dm", 192);

        atom created = Dm.@new(chosen_mob_type, picked_spot) as atom;
        if (created == null)
            throw Dm.CRASH("Failed to spawn mob!", "code/datums/components/spawner.dm", 196);
        setup_spawned_mob(created, spawner);
    }

    /**
     * Registers signals and flags onto a component spawned mob, to keep track of the spawned mob, as well as prevent them from getting treated as naturally spawned.
     */
    // tg: code/datums/components/spawner.dm:204
    public virtual void setup_spawned_mob(atom spawned_mob = null, atom spawner = null)
    {
        spawned_mob.flags_1 = Dm.num(Dm.bitor(spawned_mob.flags_1, Dm.bitand(spawner.flags_1, ADMIN_SPAWNED_1)));
        Dm.append(ref spawned_things, WEAKREF(spawned_mob));

        if (isliving(spawned_mob))
        {
            mob_living created_mob = spawned_mob as mob_living;
            created_mob.set_faction(faction);
            RegisterSignal(created_mob, COMSIG_LIVING_DEATH, nameof(mob_death));
            if (Dm.eq(spawner_logic, SPAWN_BY_WAVE_BEHAVIOR))
                Waiting.NoSleep(created_mob.adjust_timed_status_effect(60 * SECONDS, typeof(datum_status_effect_heads_up)));
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_SPAWNER_SPAWNED]))
            _SendSignal(COMSIG_SPAWNER_SPAWNED, Dm.list(this, spawned_mob));
        RegisterSignal(spawned_mob, COMSIG_QDELETING, nameof(on_deleted));
        if (spawn_callback != null)
            Waiting.NoSleep(spawn_callback.Invoke(spawned_mob));

        if (Dm.truthy(spawn_text))
            spawner.visible_message(span_danger(I18n.T("A creature [] [].", spawn_text, spawner)));
    }

    /// Remove weakrefs to atoms which have been killed or deleted without us picking it up somehow
    // tg: code/datums/components/spawner.dm:224
    public virtual void validate_references()
    {
        foreach (datum_weakref weak_thing in Dm.each_cast<datum_weakref>(spawned_things))
        {
            atom previously_spawned = weak_thing?.resolve() as atom;
            if (previously_spawned == null)
            {
                Dm.remove(ref spawned_things, weak_thing);
                continue;
            }
            if (!isliving(previously_spawned))
                continue;
            mob_living spawned_mob = previously_spawned as mob_living;
            if (spawned_mob.stat != DEAD)
                continue;
            Dm.remove(ref spawned_things, weak_thing);
        }
    }

    /// Called when an atom we spawned is deleted, remove it from the list
    // tg: code/datums/components/spawner.dm:238
    [SignalHandler]
    public virtual void on_deleted(atom source = null)
    {
        Dm.remove(ref spawned_things, WEAKREF(source));
    }

    /// Called when a mob we spawned dies, remove it from the list and unregister signals
    // tg: code/datums/components/spawner.dm:243
    public virtual void mob_death(mob_living source = null)
    {
        Dm.remove(ref spawned_things, WEAKREF(source));
        source.remove_status_effect(typeof(datum_status_effect_heads_up));
        UnregisterSignal(source, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH));
    }
}
