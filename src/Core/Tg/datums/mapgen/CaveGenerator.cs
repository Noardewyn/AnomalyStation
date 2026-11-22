// Translated from tgstation (snapshot 1d99bd7f): code/datums/mapgen/CaveGenerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304). The rust-g calls are Builtin/rust_g.mapgen.builtin.cs.

// tg: code/datums/mapgen/CaveGenerator.dm:10
public partial class datum_map_generator_cave_generator : datum_map_generator
{
    /// The random offset applied to square coordinates, causes intermingling at biome borders
    private const float BIOME_RANDOM_SQUARE_DRIFT = 2;
    // Categories for spawned things
    private const string CAVE_SPAWN_MOB = "mob";
    private const string CAVE_SPAWN_FEATURE = "feature";
    private const string CAVE_SPAWN_TENDRIL = "tendril";
    private const string CAVE_SPAWN_MEGAFAUNA = "megafauna";

    // tg: code/datums/mapgen/CaveGenerator.dm:115
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (weighted_mob_spawn_list == null)
        {
            weighted_mob_spawn_list = Dm.list(
                Dm.kv(typeof(mob_living_basic_mining_basilisk), 4),
                Dm.kv(typeof(mob_living_basic_mining_goldgrub), 1),
                Dm.kv(typeof(mob_living_basic_mining_goliath_ancient), 5),
                Dm.kv(typeof(mob_living_basic_mining_hivelord), 3));
        }
        mob_spawn_list = expand_weights(weighted_mob_spawn_list) as DmList;
        mob_spawn_no_mega_list = expand_weights(Dm.sub(weighted_mob_spawn_list, SPAWN_MEGAFAUNA) as DmList) as DmList;
        if (weighted_megafauna_spawn_list == null)
            weighted_megafauna_spawn_list = GLOB.megafauna_spawn_list;
        megafauna_spawn_list = expand_weights(weighted_megafauna_spawn_list) as DmList;
        if (weighted_flora_spawn_list == null)
        {
            weighted_flora_spawn_list = Dm.list(
                Dm.kv(typeof(obj_structure_flora_ash_leaf_shroom), 2),
                Dm.kv(typeof(obj_structure_flora_ash_cap_shroom), 2),
                Dm.kv(typeof(obj_structure_flora_ash_stem_shroom), 2),
                Dm.kv(typeof(obj_structure_flora_ash_cacti), 1),
                Dm.kv(typeof(obj_structure_flora_ash_tall_shroom), 2),
                Dm.kv(typeof(obj_structure_flora_ash_seraka), 2));
        }
        flora_spawn_list = expand_weights(weighted_flora_spawn_list) as DmList;
        if (weighted_feature_spawn_list == null)
        {
            weighted_feature_spawn_list = Dm.list(
                Dm.kv(typeof(obj_structure_geyser_random), 1),
                Dm.kv(typeof(obj_structure_ore_vent_random), 1));
        }
        feature_spawn_list = expand_weights(weighted_feature_spawn_list) as DmList;
        open_turf_types = expand_weights(weighted_open_turf_types);
        closed_turf_types = expand_weights(weighted_closed_turf_types);
    }

    // tg: code/datums/mapgen/CaveGenerator.dm:148
    public override async DmTask generate_terrain(DmList turfs = null, area generate_in = null)
    {
        await base.generate_terrain(turfs, generate_in);
        if (!Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, CAVES_ALLOWED)))
            return;

        if (Dm.truthy(length(possible_biomes)))
        {
            await generate_terrain_with_biomes(turfs, generate_in);
            return;
        }

        float start_time = REALTIMEOFDAY();

        string_gen = generate_cave(generate_in);
        string cave = (string)string_gen;

        foreach (turf gen_turf in Dm.each<turf>(turfs)) //Go through all the turfs and generate them
        {
            bool closed = cave[(int)(Dm.world.maxx * (gen_turf.y - 1) + gen_turf.x) - 1] != '1';
            var new_turf_type = (Type)pick(closed ? closed_turf_types : open_turf_types);

            // The assumption is this will be faster then changeturf, and changeturf isn't required since by this point
            // The old tile hasn't got the chance to init yet
            var new_turf = (turf)Dm.@new(new_turf_type, gen_turf);

            if (Dm.truthy(Dm.bitand(gen_turf.turf_flags, NO_RUINS)))
                new_turf.turf_flags = Dm.bitor(new_turf.turf_flags, NO_RUINS);
        }

        string message = Dm.text("[] terrain generation finished in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
        to_chat(world, span_boldannounce(Dm.text("[]", message)), MESSAGE_TYPE_DEBUG);
        log_world(message);
    }

    /**
     * This proc handles including biomes in the cave generation. This is slower than
     * `generate_terrain()`, so please use it only if you actually need biomes.
     *
     * This should only be called by `generate_terrain()`, if you have to call this,
     * you're probably doing something wrong.
     */
    // tg: code/datums/mapgen/CaveGenerator.dm:183
    public virtual async DmTask generate_terrain_with_biomes(DmList turfs = null, area generate_in = null)
    {
        if (!Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, CAVES_ALLOWED)))
            return;

        object humidity_seed = null;
        object heat_seed = null;

        // Make sure that all caves on the same z level generate smoothly regardless of the area
        if (Dm.truthy(shared_seed))
        {
            TgState state = TgState.Current;
            if (state.datum_map_generator_cave_generator_generate_terrain_with_biomes__static_heat == null)
            {
                state.datum_map_generator_cave_generator_generate_terrain_with_biomes__static_heat = Dm.list();
                state.datum_map_generator_cave_generator_generate_terrain_with_biomes__static_humi = Dm.list();
            }
            DmList static_heat = state.datum_map_generator_cave_generator_generate_terrain_with_biomes__static_heat;
            DmList static_humi = state.datum_map_generator_cave_generator_generate_terrain_with_biomes__static_humi;

            string z_key = Dm.text("[]", ((turf)turfs[1]).z);
            if (!Dm.truthy(static_heat[z_key]))
            {
                static_heat[z_key] = Dm.rand(0, 50000);
                static_humi[z_key] = Dm.rand(0, 50000);
            }

            heat_seed = static_heat[z_key];
            humidity_seed = static_humi[z_key];
        }
        else
        {
            humidity_seed = Dm.rand(0, 50000);
            heat_seed = Dm.rand(0, 50000);
        }

        float start_time = REALTIMEOFDAY();
        string_gen = generate_cave(generate_in);
        string cave = (string)string_gen;

        DmList humidity_gen = Dm.list();
        humidity_gen[BIOME_HIGH_HUMIDITY] = rustg_dbp_generate(Dm.text("[]", humidity_seed), "60", Dm.text("[]", biome_stamp_size), Dm.text("[]", Dm.world.maxx), Dm.text("[]", high_heat_threshold), "1.1");
        humidity_gen[BIOME_MEDIUM_HUMIDITY] = rustg_dbp_generate(Dm.text("[]", humidity_seed), "60", Dm.text("[]", biome_stamp_size), Dm.text("[]", Dm.world.maxx), Dm.text("[]", medium_heat_threshold), Dm.text("[]", high_heat_threshold));

        DmList heat_gen = Dm.list();
        heat_gen[BIOME_HIGH_HEAT] = rustg_dbp_generate(Dm.text("[]", heat_seed), "60", Dm.text("[]", biome_stamp_size), Dm.text("[]", Dm.world.maxx), Dm.text("[]", high_heat_threshold), "1.1");
        heat_gen[BIOME_MEDIUM_HEAT] = rustg_dbp_generate(Dm.text("[]", heat_seed), "60", Dm.text("[]", biome_stamp_size), Dm.text("[]", Dm.world.maxx), Dm.text("[]", medium_heat_threshold), Dm.text("[]", high_heat_threshold));

        if (Dm.truthy(shared_seed))
        {
            static_biome_maps[Dm.text("[]", ((turf)turfs[1]).z)] = Dm.list(
                Dm.kv(BIOME_HIGH_HUMIDITY, humidity_gen[BIOME_HIGH_HUMIDITY]),
                Dm.kv(BIOME_MEDIUM_HUMIDITY, humidity_gen[BIOME_MEDIUM_HUMIDITY]),
                Dm.kv(BIOME_HIGH_HEAT, heat_gen[BIOME_HIGH_HEAT]),
                Dm.kv(BIOME_MEDIUM_HEAT, heat_gen[BIOME_MEDIUM_HEAT]));
        }

        DmList to_generate = Dm.list();
        foreach (turf gen_turf in Dm.each<turf>(turfs)) //Go through all the turfs and generate them
        {
            bool closed = cave[(int)(Dm.world.maxx * (gen_turf.y - 1) + gen_turf.x) - 1] != '1';

            // Here comes the meat of the biome code.
            float drift_x = clamp(gen_turf.x + Dm.rand(-BIOME_RANDOM_SQUARE_DRIFT, BIOME_RANDOM_SQUARE_DRIFT), 1, Dm.world.maxx); // / perlin_zoom
            float drift_y = clamp(gen_turf.y + Dm.rand(-BIOME_RANDOM_SQUARE_DRIFT, BIOME_RANDOM_SQUARE_DRIFT), 2, Dm.world.maxy); // / perlin_zoom

            // Where we go in the generated string (generated outside of the loop for s p e e d)
            int coordinate = (int)(Dm.world.maxx * (drift_y - 1) + drift_x);

            // Type of humidity zone we're in (LOW-MEDIUM-HIGH)
            string humidity_level = BiomeBit(humidity_gen[BIOME_HIGH_HUMIDITY], coordinate) ? BIOME_HIGH_HUMIDITY
                : BiomeBit(humidity_gen[BIOME_MEDIUM_HUMIDITY], coordinate) ? BIOME_MEDIUM_HUMIDITY : BIOME_LOW_HUMIDITY;
            // Type of heat zone we're in (LOW-MEDIUM-HIGH)
            string heat_level = BiomeBit(heat_gen[BIOME_HIGH_HEAT], coordinate) ? BIOME_HIGH_HEAT
                : BiomeBit(heat_gen[BIOME_MEDIUM_HEAT], coordinate) ? BIOME_MEDIUM_HEAT : BIOME_LOW_HEAT;

            object selected_biome = ((DmList)possible_biomes[heat_level])[humidity_level];
            DmList biome_turfs = to_generate[selected_biome] as DmList;
            LAZYSET(ref biome_turfs, gen_turf, closed ? TRUE : FALSE);
            to_generate[selected_biome] = biome_turfs;
        }

        foreach (object biome in Dm.each(to_generate))
        {
            var generating_biome = (datum_biome)SSmapping.biomes[biome];
            DmList generated_turfs = await generating_biome.generate_turfs_for_terrain(to_generate[biome] as DmList);
            generated_turfs_per_biome[biome] = Dm.add(generated_turfs_per_biome[biome] ?? Dm.list(), generated_turfs);
            DmList area_list = generated_turfs_per_area_biome[biome] as DmList;
            if (area_list == null)
            {
                area_list = Dm.list();
                generated_turfs_per_area_biome[biome] = area_list;
            }
            area_list[generate_in] = generated_turfs;
        }

        string message = Dm.text("[] terrain generation finished in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
        to_chat(world, span_boldannounce(Dm.text("[]", message)), MESSAGE_TYPE_DEBUG);
        log_world(message);
    }

    // text2num(map[coordinate]): the noise string's character at a DM (1-based) index
    private static bool BiomeBit(object noise, int coordinate) => ((string)noise)[coordinate - 1] == '1';

    /// Returns a biome datum that the turf was initialized with, or would be if it is present on our Z level and we use a consistent shared seed
    /// Not consistent between calls by itself due to using RNG in perlin zoom, needs a static coordinate-based formula
    // tg: code/datums/mapgen/CaveGenerator.dm:267
    public virtual object get_biome_for_turf(turf target = null)
    {
        foreach (object biome in Dm.each(generated_turfs_per_biome))
        {
            DmList generated_turfs = generated_turfs_per_biome[biome] as DmList;
            if (Dm.truthy(generated_turfs[target]))
                return biome;
        }

        DmList biome_map = static_biome_maps[Dm.text("[]", target.z)] as DmList;
        if (!Dm.truthy(shared_seed) || biome_map == null)
            return null;

        float drift_x = clamp(target.x + Dm.rand(-BIOME_RANDOM_SQUARE_DRIFT, BIOME_RANDOM_SQUARE_DRIFT), 1, Dm.world.maxx);
        float drift_y = clamp(target.y + Dm.rand(-BIOME_RANDOM_SQUARE_DRIFT, BIOME_RANDOM_SQUARE_DRIFT), 2, Dm.world.maxy);
        int coordinate = (int)(Dm.world.maxx * (drift_y - 1) + drift_x);

        string humidity_level = BiomeBit(biome_map[BIOME_HIGH_HUMIDITY], coordinate) ? BIOME_HIGH_HUMIDITY
            : BiomeBit(biome_map[BIOME_MEDIUM_HUMIDITY], coordinate) ? BIOME_MEDIUM_HUMIDITY : BIOME_LOW_HUMIDITY;
        string heat_level = BiomeBit(biome_map[BIOME_HIGH_HEAT], coordinate) ? BIOME_HIGH_HEAT
            : BiomeBit(biome_map[BIOME_MEDIUM_HEAT], coordinate) ? BIOME_MEDIUM_HEAT : BIOME_LOW_HEAT;

        return ((DmList)possible_biomes[heat_level])[humidity_level];
    }

    // tg: code/datums/mapgen/CaveGenerator.dm:288
    public override async DmTask populate_terrain(DmList turfs = null, area generate_in = null)
    {
        if (Dm.truthy(biome_population) && Dm.truthy(length(possible_biomes)))
        {
            await populate_terrain_with_biomes(turfs, generate_in);
            return;
        }

        // Area var pullouts to make accessing in the loop faster
        bool flora_allowed = Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, FLORA_ALLOWED)) && Dm.truthy(length(flora_spawn_list));
        bool feature_allowed = Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, FLORA_ALLOWED)) && Dm.truthy(length(feature_spawn_list));
        bool mobs_allowed = Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, MOB_SPAWN_ALLOWED)) && Dm.truthy(length(mob_spawn_list));
        bool megas_allowed = Dm.truthy(Dm.bitand(generate_in.area_flags_mapping, MEGAFAUNA_SPAWN_ALLOWED)) && Dm.truthy(length(megafauna_spawn_list));

        float start_time = REALTIMEOFDAY();

        // Assoc list of spawned categories to
        DmList spawn_data = Dm.list(
            Dm.kv(CAVE_SPAWN_FEATURE, Dm.list()),
            Dm.kv(CAVE_SPAWN_TENDRIL, Dm.list()),
            Dm.kv(CAVE_SPAWN_MOB, Dm.list()),
            Dm.kv(CAVE_SPAWN_MEGAFAUNA, Dm.list()));

        object open_turf_cache = typecacheof(open_turf_types);
        foreach (turf target_turf in Dm.each<turf>(turfs))
        {
            // Only put stuff on open turfs we generated, so closed walls and rivers and stuff are skipped
            if (!Dm.truthy(is_type_in_typecache(target_turf, open_turf_cache)))
                continue;

            if (!Dm.truthy(Dm.bitand(target_turf.turf_flags, TURF_BLOCKS_POPULATE_TERRAIN_FLORAFEATURES)))
            {
                if (flora_allowed && Dm.prob(flora_spawn_chance))
                {
                    object flora_type = pick(flora_spawn_list);
                    Dm.@new((Type)flora_type, target_turf);
                    continue;
                }

                if (feature_allowed && Dm.prob(feature_spawn_chance))
                {
                    bool can_spawn = true;
                    foreach (turf spawn_turf in Dm.each<turf>(spawn_data[CAVE_SPAWN_FEATURE]))
                    {
                        if (get_dist(spawn_turf, target_turf) <= feature_exclusion_radius)
                        {
                            can_spawn = false;
                            break;
                        }
                    }

                    if (can_spawn)
                    {
                        object picked_feature = pick(feature_spawn_list);
                        Dm.@new((Type)picked_feature, target_turf);
                        Dm.append((DmList)spawn_data[CAVE_SPAWN_FEATURE], target_turf);
                        continue;
                    }
                }
            }

            if (!mobs_allowed || !Dm.prob(mob_spawn_chance))
                continue;

            object picked_mob = pick(megas_allowed ? mob_spawn_list : mob_spawn_no_mega_list);
            bool is_megafauna = false;
            if (Dm.eq(picked_mob, SPAWN_MEGAFAUNA))
            {
                picked_mob = pick(megafauna_spawn_list);
                is_megafauna = true;
            }

            bool can_spawn_mob = true;
            if (ispath(picked_mob, typeof(mob_living_basic_mining_tendril)))
            {
                // Prevents tendrils spawning in each other's collapse range
                foreach (turf spawn_turf in Dm.each<turf>(spawn_data[CAVE_SPAWN_TENDRIL]))
                {
                    if (get_dist(spawn_turf, target_turf) <= 2)
                    {
                        can_spawn_mob = false;
                        break;
                    }
                }

                // Also avoid spawning them next to megafauna
                foreach (turf spawn_turf in Dm.each<turf>(spawn_data[CAVE_SPAWN_MEGAFAUNA]))
                {
                    if (get_dist(spawn_turf, target_turf) <= megafauna_exclusion_radius)
                    {
                        can_spawn_mob = false;
                        break;
                    }
                }
            }
            else if (is_megafauna)
            {
                // Megafauna can spawn wherever it wants as long as its not next to another mega
                foreach (turf spawn_turf in Dm.each<turf>(spawn_data[CAVE_SPAWN_MEGAFAUNA]))
                {
                    if (get_dist(spawn_turf, target_turf) <= megafauna_exclusion_radius)
                    {
                        can_spawn_mob = false;
                        break;
                    }
                }
            }
            else
            {
                foreach (turf spawn_turf in Dm.each<turf>(spawn_data[CAVE_SPAWN_MOB]))
                {
                    if (get_dist(spawn_turf, target_turf) <= mob_exclusion_radius)
                    {
                        can_spawn_mob = false;
                        break;
                    }
                }
            }

            if (!can_spawn_mob)
                continue;

            if (ispath(picked_mob, typeof(mob_living_basic_mining_tendril)))
            {
                Dm.append((DmList)spawn_data[CAVE_SPAWN_TENDRIL], target_turf);
            }
            else
            {
                if (is_megafauna)
                    Dm.append((DmList)spawn_data[CAVE_SPAWN_MEGAFAUNA], target_turf);
                Dm.append((DmList)spawn_data[CAVE_SPAWN_MOB], target_turf);
            }

            Dm.@new((Type)picked_mob, target_turf);

            // There can be only one Bubblegum, so don't waste spawns on it
            if (ispath(picked_mob, typeof(mob_living_simple_animal_hostile_megafauna_bubblegum)))
            {
                weighted_megafauna_spawn_list.Remove(picked_mob);
                megafauna_spawn_list = expand_weights(weighted_megafauna_spawn_list) as DmList;
                megas_allowed = megas_allowed && Dm.truthy(length(megafauna_spawn_list));
            }
        }

        string message = Dm.text("[] terrain population finished in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
        to_chat(world, span_boldannounce(Dm.text("[]", message)), MESSAGE_TYPE_DEBUG);
        log_world(message);
    }

    /**
     * This handles the population of terrain with biomes. Should only be called by
     * `populate_terrain()`, if you find yourself calling this, you're probably not
     * doing it right.
     *
     * This proc won't do anything if the area we're trying to generate in does not
     * have `FLORA_ALLOWED` or `MOB_SPAWN_ALLOWED` in its `area_flags`.
     */
    // tg: code/datums/mapgen/CaveGenerator.dm:399
    public virtual async DmTask populate_terrain_with_biomes(DmList turfs = null, area generate_in = null)
    {
        // Area var pullouts to make accessing in the loop faster
        object flora_allowed = Dm.bitand(generate_in.area_flags_mapping, FLORA_ALLOWED);
        object features_allowed = Dm.bitand(generate_in.area_flags_mapping, FLORA_ALLOWED);
        object fauna_allowed = Dm.bitand(generate_in.area_flags_mapping, MOB_SPAWN_ALLOWED);

        float start_time = REALTIMEOFDAY();

        // No sense in doing anything here if nothing is allowed anyway.
        if (!Dm.truthy(flora_allowed) && !Dm.truthy(features_allowed) && !Dm.truthy(fauna_allowed))
        {
            string none = Dm.text("[] terrain population finished in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
            to_chat(world, span_boldannounce(Dm.text("[]", none)), MESSAGE_TYPE_DEBUG);
            log_world(none);
            return;
        }

        foreach (object biome in Dm.each(generated_turfs_per_area_biome))
        {
            var generating_biome = (datum_biome)SSmapping.biomes[biome];
            DmList areas_list = generated_turfs_per_area_biome[biome] as DmList;
            await generating_biome.populate_turfs(areas_list[generate_in] as DmList, flora_allowed, features_allowed, fauna_allowed);
        }

        string message = Dm.text("[] terrain population finished in []s!", name, (REALTIMEOFDAY() - start_time) / 10);
        to_chat(world, span_boldannounce(Dm.text("[]", message)), MESSAGE_TYPE_DEBUG);
        log_world(message);
    }

    ///Generates the cave shape using Rust-G
    // tg: code/datums/mapgen/CaveGenerator.dm:427
    public virtual string generate_cave(area generate_in = null)
    {
        ///Loop through all the active ruins for this z-level and make a json format out of it so we can send it to the generator
        DmList active_ruins_list = Dm.list();

        foreach (turf bottom_left_turf in Dm.each<turf>(SSmapping.active_ruins))
        {
            var active_ruin = (datum_map_template_ruin)SSmapping.active_ruins[bottom_left_turf];
            if (bottom_left_turf.z != generate_in.z)
                continue;
            Dm.append(ref active_ruins_list, Dm.list(Dm.list(
                Dm.kv("x", max(1, bottom_left_turf.x - active_ruin.terrain_padding)),
                Dm.kv("y", max(1, bottom_left_turf.y - active_ruin.terrain_padding)),
                Dm.kv("w", active_ruin.width + active_ruin.terrain_padding * 2),
                Dm.kv("h", active_ruin.height + active_ruin.terrain_padding * 2),
                Dm.kv("isEnclosed", active_ruin.enclosed_for_terrain))));
        }

        string active_ruin_string = json_encode(active_ruins_list);

        string generated = rustg_cave_system_generator_generate(Dm.text("[]", Dm.world.maxx), Dm.text("[]", Dm.world.maxy), active_ruin_string, Dm.text("[]", min_bsp_size),
            Dm.text("[]", max_ratio), Dm.text("[]", padding), Dm.text("[]", room_fill_percent), Dm.text("[]", corridor_width), Dm.text("[]", loop_percent),
            Dm.text("[]", noise_percent), Dm.text("[]", ca_steps), Dm.text("[]", birth_limit), Dm.text("[]", survival_limit), Dm.text("[]", edges_are_alive));

        return generated;
    }
}

// rules 10.6: the proc-level statics above, made on first use
public sealed partial class TgState
{
    internal DmList datum_map_generator_cave_generator_generate_terrain_with_biomes__static_heat;
    internal DmList datum_map_generator_cave_generator_generate_terrain_with_biomes__static_humi;
}
