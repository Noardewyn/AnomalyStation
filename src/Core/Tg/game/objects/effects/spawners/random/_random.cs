// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/_random.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Base class for all random spawners.
 */
// M3 P4: the random spawner base is live: its loot is picked with the world RNG (D31) at init, scattered over the turfs in
// view (the builtin view()) when it has a scatter radius; the loot table maker is a debug item.

// tg: code/game/objects/effects/spawners/random/_random.dm:4
public partial class obj_effect_spawner_random : obj_effect_spawner
{
    // tg: code/game/objects/effects/spawners/random/_random.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // DEVIATION(DV504): a map editor's world keeps the spawner where the map has it and spawns nothing (MapEditSeam.On)
        if (MapEditSeam.On)
            return INITIALIZE_HINT_NORMAL;
        Waiting.NoSleep(spawn_loot());
        return dot;
    }

    ///If the spawner has any loot defined, randomly picks some and spawns it. Does not cleanup the spawner.
    // tg: code/game/objects/effects/spawners/random/_random.dm:40
    public virtual async DmTask spawn_loot(object lootcount_override = null)
    {
        if (!prob(spawn_loot_chance))
            return;

        DmList spawn_locations = await get_spawn_locations(spawn_scatter_radius) as DmList;
        float spawn_loot_count = (lootcount_override == null ? this.spawn_loot_count : Dm.num(lootcount_override));

        if (Dm.truthy(spawn_all_loot))
        {
            spawn_loot_count = INFINITY;
            spawn_loot_double = FALSE;
        }

        if (Dm.truthy(loot_type_path))
            Dm.append(ref loot, typesof(loot_type_path));

        if (Dm.truthy(loot_subtype_path))
            Dm.append(ref loot, subtypesof(loot_subtype_path));

        if (!Dm.eq(config.Get(typeof(datum_config_entry_number_random_loot_weight_modifier)), 1))
            skew_loot_weights(loot, config.Get(typeof(datum_config_entry_number_random_loot_weight_modifier)));

        if (Dm.truthy(loot?.len))
        {
            float loot_spawned = 0;
            float pixel_divider = FLOOR(16 / spawn_loot_split_pixel_offsets, 1); // 16 pixels offsets is max that should be allowed in any direction
            while (Dm.truthy(spawn_loot_count - loot_spawned) && Dm.truthy(loot.len))
            {
                object lootspawn = pick_weight_recursive(loot);
                if (!Dm.truthy(can_spawn(lootspawn)))
                {
                    if (Dm.truthy(remove_if_cant_spawn))
                        loot.Remove(lootspawn);
                    continue;
                }
                if (!Dm.truthy(spawn_loot_double))
                    loot.Remove(lootspawn);
                if (Dm.truthy(lootspawn) && (spawn_scatter_radius == 0 || Dm.truthy(spawn_locations.len)))
                {
                    atom spawn_loc = loc; // tg's var/turf keeps a container too (a spawner in a box or a letter)
                    if (spawn_scatter_radius > 0)
                        spawn_loc = pick_n_take(spawn_locations) as turf;

                    // DEVIATION(DV508): the way the map turned the spawner is the way the map turned its pick. The pick is made with that dir as the
                    // loader makes a thing with its map's vars (tg's preloader, which notes it): the 3D models' facing rule runs at the pick's init
                    // and sees it, and a spawner among the loot, which spawns in its own init, hands it on to its pick (tg's setDir below comes
                    // after both).
                    bool turned = MapSeam.HasMapDir(this);
                    if (turned)
                        Dm.world.preloader_setup(Dm.list(Dm.kv("dir", dir)), lootspawn);
                    atom_movable spawned_loot = await make_item(spawn_loc, lootspawn) as atom_movable;
                    if (turned)
                    {
                        GLOB.use_preloader = FALSE; // (a loot that made nothing of that type would leave the preloader armed)
                        MapSeam.NoteMapDir(spawned_loot);
                    }
                    await spawned_loot.setDir(dir);

                    if (!Dm.truthy(spawn_loot_split) && !Dm.truthy(spawn_random_offset))
                    {
                        if (pixel_x != 0)
                            spawned_loot.pixel_x = pixel_x;
                        if (pixel_y != 0)
                            spawned_loot.pixel_y = pixel_y;
                    }
                    else if (Dm.truthy(spawn_random_offset))
                    {
                        spawned_loot.pixel_x = rand(-16, 16);
                        spawned_loot.pixel_y = rand(-16, 16);
                    }
                    else if (Dm.truthy(spawn_loot_split))
                    {
                        if (Dm.truthy(loot_spawned))
                        {
                            float column = FLOOR(loot_spawned / pixel_divider, 1);
                            spawned_loot.pixel_x = spawn_loot_split_pixel_offsets * Dm.mod(loot_spawned, pixel_divider) + (column * spawn_loot_split_pixel_offsets);
                            spawned_loot.pixel_y = spawn_loot_split_pixel_offsets * Dm.mod(loot_spawned, pixel_divider);
                        }
                    }
                }
                loot_spawned++;
            }
        }
    }

    ///Levels out the weights of loot if lower than 1, or makes rarer spawns even more rare.
    // tg: code/game/objects/effects/spawners/random/_random.dm:95
    public virtual void skew_loot_weights(DmList loot_list = null, object exponent = null)
    {
        ///This helps keeping the modified weights more or less correct, since pick_weight doesn't appreciate decimals.
        float precision = 1;
        if (Dm.lt(exponent, 1))
            precision = round((1 - Dm.num(exponent)) * 10) + 1;
        foreach (object loot_type in Dm.each(loot_list))
        {
            if (islist(loot_type))
                skew_loot_weights(loot_type as DmList, exponent);
            object loot_weight = loot_list[loot_type];
            if (Dm.le(loot_weight, 1))
            {
                if (Dm.lt(exponent, 1))
                    loot_list[loot_type] = precision;
                continue;
            }
            loot_list[loot_type] = Dm.round(Dm.num(Dm.pow(loot_weight, exponent)) * precision, 1);
        }
    }

    /**
     *  Makes the actual item related to our spawner.
     *
     * spawn_loc - where are we spawning it?
     * type_path_to_make - what are we spawning?
     **/
    // tg: code/game/objects/effects/spawners/random/_random.dm:116
    public virtual async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        return await Dm.new_async((Type)type_path_to_make, spawn_loc);
    }

    /**
     *  If the spawner has a spawn_scatter_radius set, this creates a list of nearby turfs available that are in view and have an unblocked line to them.
     *
     * radius - The scatter radius to search within.
     * ignore_groundless - If TRUE, allows spawning on groundless turfs like lava, water, or open space even if there isn't a turf below it.
     **/
    // tg: code/game/objects/effects/spawners/random/_random.dm:125
    public virtual async DmTask<object> get_spawn_locations(object radius = null, float? ignore_groundless = null)
    {
        ignore_groundless ??= FALSE;
        DmList scatter_locations = Dm.list();

        if (!Dm.truthy(radius))
            return scatter_locations;

        foreach (turf turf_in_view in Dm.each<turf>(view(radius, get_turf(this))))
        {
            if (isclosedturf(turf_in_view))
                continue;

            if (!Dm.truthy(ignore_groundless) && isgroundlessturf(turf_in_view) && !Dm.truthy(GET_TURF_BELOW(turf_in_view)))
                continue;

            if (!Dm.truthy(await has_unblocked_line(turf_in_view)))
                continue;

            Dm.append(ref scatter_locations, turf_in_view);
        }

        return scatter_locations;
    }

    // tg: code/game/objects/effects/spawners/random/_random.dm:145
    public virtual async DmTask<object> has_unblocked_line(object destination = null)
    {
        foreach (turf potential_blockage in Dm.each_cast<turf>(get_line(get_turf(this), destination as atom)))
        {
            if (!Dm.truthy(await potential_blockage.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/objects/effects/spawners/random/_random.dm:170
public partial class obj_item_loot_table_maker : obj_item
{
    // tg: code/game/objects/effects/spawners/random/_random.dm:170
    public virtual void make_table()
    {
        Dm.NotTranslated("code/game/objects/effects/spawners/random/_random.dm:170", "make_table: the loot table maker, a debug item (M10+)");
    }
}
