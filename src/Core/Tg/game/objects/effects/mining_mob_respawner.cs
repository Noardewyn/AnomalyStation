// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/mining_mob_respawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Respawns a mob if it's died and there's a storm
// tg: code/game/objects/effects/mining_mob_respawner.dm:2
public partial class obj_effect_mining_mob_respawner : obj_effect
{
    /// Types of storm that respawn mobs, we might as well make it static because it's only used on init so there's no point in it supporting VV edits
    // tg: code/game/objects/effects/mining_mob_respawner.dm:31 (var/static/list/valid_weather)
    public static DmList valid_weather => TgState.Current.obj_effect_mining_mob_respawner__valid_weather ??= Dm.list(
        typeof(datum_weather_particle_ash_storm),
        typeof(datum_weather_sand_storm), // We don't have any mining areas with sand but someone might start one there with an anomaly
        typeof(datum_weather_snow_storm));

    // tg: code/game/objects/effects/mining_mob_respawner.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload) && Dm.truthy(Dm.length(valid_mobs)))
        {
            DmList filtered_mobs = Dm.list();
            foreach (object path in Dm.each(valid_mobs))
            {
                if (!Dm.@in(path, invalid_mobs))
                    Dm.append(ref filtered_mobs, path);
            }
            valid_mobs = filtered_mobs;
        }

        if (our_mob?.resolve() == null && !Dm.truthy(registered_spawn_signal))
            make_mob();

        // We're just going to go ahead and assume these won't move after being spawned
        foreach (object weather in Dm.each(valid_weather))
            RegisterSignal(get_area(this) as datum, COMSIG_WEATHER_BEGAN_IN_AREA(weather), nameof(on_storm_event));
        return dot;
    }

    /// Let the map generator system pass us a list of mobs
    // tg: code/game/objects/effects/mining_mob_respawner.dm:55
    public virtual void setup(DmList valid_mobs = null, atom initial_spawn = null)
    {
        this.valid_mobs = valid_mobs;

        if (initial_spawn is obj_effect_spawner_random)
        {
            RegisterSignal(get_turf(this), COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(get_spawned_mob));
            registered_spawn_signal = TRUE;
        }
        else
        {
            register_spawn(this, initial_spawn);
        }
    }

    /// Record something as our spawn, so we don't need to spawn something else
    // tg: code/game/objects/effects/mining_mob_respawner.dm:65
    [SignalHandler]
    public virtual void register_spawn(atom source = null, atom new_spawn = null)
    {
        if (new_spawn == null)
            return;
        if (isliving(new_spawn))
            our_mob = WEAKREF(new_spawn) as datum_weakref;
    }

    /// Make a guy and subscribe to see if they die
    // tg: code/game/objects/effects/mining_mob_respawner.dm:73
    public virtual void make_mob()
    {
        if (!Dm.truthy(Dm.length(valid_mobs)))
            return;
        object spawn_path = Dm.pick(valid_mobs);

        DmList valid_locations = Dm.list();

        foreach (turf turf_in_view in Dm.each<turf>(view(respawn_range, get_turf(this))))
        {
            if (isclosedturf(turf_in_view) || isgroundlessturf(turf_in_view))
                continue;
            if (Dm.truthy(outdoor_only))
            {
                area turf_area = get_area(turf_in_view) as area;
                if (!Dm.truthy(turf_area.outdoors)) // Don't spawn inside a miner's little pod house
                    continue;
            }
            Dm.append(ref valid_locations, turf_in_view);
        }

        if (!Dm.truthy(Dm.length(valid_locations)))
            return;

        turf spawn_turf = Dm.pick(valid_locations) as turf;
        // Bit roundabout but it's the only way of intercepting mob spawners
        RegisterSignal(spawn_turf, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(get_spawned_mob));
        registered_spawn_signal = TRUE;
        Dm.@new((Type)spawn_path, spawn_turf);
    }

    /// Intercept the next mob spawned on the turf, because we might have spawned an object which spawns a mob instead
    // tg: code/game/objects/effects/mining_mob_respawner.dm:100
    [SignalHandler]
    public virtual void get_spawned_mob(datum source = null, atom new_spawn = null)
    {
        if (!isliving(new_spawn))
            return;

        UnregisterSignal(source, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        registered_spawn_signal = FALSE;
        register_spawn(source as atom, new_spawn);
        play_spawn_animation(new_spawn as mob_living, source as turf);
    }

    /// When a storm hits & if our mob is dead, make a new one
    // tg: code/game/objects/effects/mining_mob_respawner.dm:111
    [SignalHandler]
    public virtual void on_storm_event()
    {
        mob_living resolved = our_mob?.resolve() as mob_living;
        if (resolved != null && resolved.stat != DEAD)
            return;

        DmList blockers = Dm.list();
        // Respawns get blocked by active Candela network nodes nearby
        foreach (datum_mining_beacon_network network in Dm.each<datum_mining_beacon_network>(GLOB.mining_beacon_networks))
        {
            if (!Dm.truthy(network.powered))
                continue;
            foreach (datum_component_candela_node node in Dm.each<datum_component_candela_node>(network.linked_nodes))
                blockers[get_turf(node.parent as atom)] = TRUE;
        }

        foreach (turf turf_in_view in Dm.each<turf>(view(network_block_range, get_turf(this))))
        {
            if (Dm.truthy(blockers[turf_in_view]))
                return;
        }

        if (prob(respawn_chance))
            _addtimer(Dm.@new<datum_callback>(this, nameof(make_mob)), rand(min_delay, max_delay), TIMER_DELETE_ME, file: "code/game/objects/effects/mining_mob_respawner.dm", line: 131);
    }

    /// Play an awesome animation
    // tg: code/game/objects/effects/mining_mob_respawner.dm:134
    public virtual void play_spawn_animation(mob_living spawned = null, turf spawned_turf = null)
    {
        if (HAS_TRAIT(spawned, TRAIT_MOVE_FLYING))
        {
            Dm.@new<obj_effect_fly_down>(spawned_turf, spawned);
        }
        else
        {
            Dm.@new<obj_effect_unburrow>(spawned_turf, spawned);
            spawned_turf.Shake(pixelshiftx: 0.5f, pixelshifty: 0.5f, duration: 2 * SECONDS);
        }
    }
}

/// A "good enough" alpha mask effect that looks like something is coming out of the ground
/// Not guaranteed to look right on things taller than 32 pixels because i didnt want to make a really massive alpha mask
// tg: code/game/objects/effects/mining_mob_respawner.dm:141
public partial class obj_effect_unburrow : obj_effect
{
    // tg: code/game/objects/effects/mining_mob_respawner.dm:146
    [DmParams("mapload", "thing")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable thing = Dm.arg<atom_movable>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (QDELETED(thing))
            return INITIALIZE_HINT_QDEL;
        Waiting.NoSleep(thing.forceMove(this));
        setup(thing);
        return dot;
    }

    // tg: code/game/objects/effects/mining_mob_respawner.dm:153
    public virtual void setup(atom_movable thing = null)
    {
        Dm.append(vis_contents, thing);
        thing.pixel_y -= 32;
        animate(thing, Dm.named("pixel_y", 32), Dm.named("time", animate_time), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        thing.Shake(pixelshiftx: 1, pixelshifty: 0.5f, duration: animate_time);
        thing.dir = Dm.num(Dm.pick(GLOB.cardinals));

        add_filter("alpha_mask", 2, alpha_mask_filter(icon: icon(Dm.resource("icons/effects/64x64.dmi"), "alpha_unburrow"), y: 16) as DmList);
        object filter = get_filter("alpha_mask");
        animate(filter, Dm.named("y", -16), Dm.named("delay", animate_time), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(release), thing), Dm.num(animate_time) + 0.5f * SECONDS, TIMER_DELETE_ME, file: "code/game/objects/effects/mining_mob_respawner.dm", line: 163);
    }

    // tg: code/game/objects/effects/mining_mob_respawner.dm:165
    public virtual void release(atom_movable thing = null)
    {
        if (!QDELETED(thing))
            Waiting.NoSleep(thing.forceMove(drop_location() as atom));
        qdel(this);
    }
}

/// An effect that shows an object swooping to the ground from above
// tg: code/game/objects/effects/mining_mob_respawner.dm:171
public partial class obj_effect_fly_down : obj_effect
{
    // tg: code/game/objects/effects/mining_mob_respawner.dm:177
    [DmParams("mapload", "thing")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable thing = Dm.arg<atom_movable>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (thing == null)
            thing = Dm.@new<mob_living_basic_mining_watcher>(loc);
        if (QDELETED(thing))
            return INITIALIZE_HINT_QDEL;
        Waiting.NoSleep(thing.forceMove(this));
        setup(thing);
        return dot;
    }

    // tg: code/game/objects/effects/mining_mob_respawner.dm:186
    public virtual void setup(atom_movable thing = null)
    {
        Dm.append(vis_contents, thing);
        thing.pixel_y += 64;
        float direction = Dm.num(Dm.pick(Dm.list(1, -1)));
        float anim_offset = 32 * direction;
        thing.pixel_x += anim_offset;
        thing.dir = direction < 0 ? EAST : WEST;

        animate(this, Dm.named("alpha", 255), Dm.named("time", 0.4f * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));
        animate(thing, Dm.named("pixel_y", -64), Dm.named("time", animate_time), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        animate(thing, Dm.named("pixel_x", -anim_offset), Dm.named("time", animate_time), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(release), thing), Dm.num(animate_time), TIMER_DELETE_ME, file: "code/game/objects/effects/mining_mob_respawner.dm", line: 198);
    }

    // tg: code/game/objects/effects/mining_mob_respawner.dm:200
    public virtual void release(atom_movable thing = null)
    {
        if (!QDELETED(thing))
            Waiting.NoSleep(thing.forceMove(drop_location() as atom));
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/mining_mob_respawner.dm:31 (var/static/list/valid_weather)
    internal DmList obj_effect_mining_mob_respawner__valid_weather;
}
