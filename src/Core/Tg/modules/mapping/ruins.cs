// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: tg's ruin seeding (the budget, the placement, the forced, linked and unique ruins), general as tg has it for every
// ruin level. SSmapping's setup_ruins seeds Lavaland's; the space and ice moon levels are not loaded, so nothing seeds them yet.

// tg: code/modules/mapping/ruins.dm:1
public partial class datum_map_template_ruin : datum_map_template
{
    // tg: code/modules/mapping/ruins.dm:1
    public virtual async DmTask<object> try_to_place(object z = null, DmList allowed_areas_typecache = null, turf forced_turf = null, object clear_below = null)
    {
        float sanity = forced_turf != null ? 1 : PLACEMENT_TRIES;
        if (Dm.truthy(SSmapping.level_trait(z, ZTRAIT_ISOLATED_RUINS)))
            return await reserve_isolated_level(z);
        while (sanity > 0)
        {
            sanity--;
            float width_border = TRANSITIONEDGE + SPACERUIN_MAP_EDGE_PAD + Dm.round(width / 2);
            float height_border = TRANSITIONEDGE + SPACERUIN_MAP_EDGE_PAD + Dm.round(height / 2);
            turf central_turf = forced_turf ?? locate(Dm.rand(width_border, Dm.world.maxx - width_border), Dm.rand(height_border, Dm.world.maxy - height_border), Dm.num(z));
            bool valid = true;
            DmList affected_turfs = get_affected_turfs(central_turf, 1) as DmList;
            DmList affected_areas = Dm.list();

            foreach (turf check in Dm.each<turf>(affected_turfs))
            {
                // Use assoc lists to move this out, it's easier that way
                if (Dm.truthy(Dm.bitand(check.turf_flags, NO_RUINS)))
                {
                    valid = false; // set to false before we check
                    break;
                }
                area new_area = get_area(check) as area;
                affected_areas[new_area] = TRUE;
            }

            // This is faster yes. Only BARELY but it is faster
            if (valid)
            {
                foreach (area affct_area in Dm.each<area>(affected_areas))
                {
                    if (!Dm.truthy(allowed_areas_typecache[affct_area.GetType()]))
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid)
                continue;

            // testing("Ruin \"[name]\" reserved at ([central_turf.x], [central_turf.y], [central_turf.z])"): a debug build's log

            foreach (turf T in Dm.each<turf>(affected_turfs))
                T.turf_flags = Dm.bitor(T.turf_flags, NO_RUINS);

            return central_turf;
        }
        return null;
    }

    // tg: code/modules/mapping/ruins.dm:38
    public virtual async DmTask<object> reserve_isolated_level(object z = null)
    {
        var reservation = await SSmapping.request_turf_block_reservation(width, height, 1, z) as datum_turf_reservation; //Make the new level creation work with different traits.
        if (reservation == null)
            return null;
        turf placement = reservation.bottom_left_turfs[1] as turf;
        foreach (turf T in Dm.each<turf>(get_affected_turfs(placement)))
            T.turf_flags = Dm.bitor(T.turf_flags, NO_RUINS);
        turf center = locate(placement.x + Dm.round(width / 2), placement.y + Dm.round(height / 2), placement.z);
        return center;
    }

    ///Loads a specified ruin that was previously reserved.
    // tg: code/modules/mapping/ruins.dm:49
    public virtual async DmTask load_reserved(turf central_turf = null, object clear_below = null)
    {
        central_turf = (turf)turf.dm_identity(central_turf); // TURF-IDENTITY: the cell reserved before the cave generator replaced its turf
        DmList affected_turfs = get_affected_turfs(central_turf, 1) as DmList;

        if (Dm.truthy(clear_below))
        {
            foreach (turf T in Dm.each<turf>(affected_turfs))
            {
                foreach (atom thing in Dm.each<atom>(T))
                {
                    if (Dm.truthy(clear_below_typecache[thing.GetType()]))
                        qdel(thing);
                }
            }
        }

        await load(central_turf, centered: TRUE);
        loaded++;

        central_turf = (turf)turf.dm_identity(central_turf); // TURF-IDENTITY: tg's central_turf names the cell, whose turf the load replaced
        Dm.@new<obj_effect_landmark_ruin>(central_turf, this);
    }

    // tg: code/modules/mapping/ruins.dm:54 (var/static/list/clear_below_typecache)
    private static DmList clear_below_typecache => TgState.Current.datum_map_template_ruin__clear_below_typecache ??= typecacheof(Dm.list(
        typeof(obj_structure_spawner),
        typeof(mob_living_simple_animal),
        typeof(obj_structure_flora))) as DmList;
}

public sealed partial class TgState
{
    internal DmList datum_map_template_ruin__clear_below_typecache;
}

public static partial class GlobalProcs
{
    /**
     * Loads the ruins for a given z level.
     * @param z_levels The z levels to load ruins on.
     * @param budget The budget to spend on ruins. Compare against the cost of the ruins in /datum/map_template/ruin.
     * @param whitelist A list of areas to allow ruins to be placed in.
     * @param potentialRuins A list of ruins to choose from.
     * @param clear_below Whether to clear the area below the ruin. Used for multiz ruins.
     * @param mineral_budget The budget to spend on ruins that spawn ore vents. Map templates with vents have that defined by mineral_cost.
     * @param mineral_budget_update What type of ore distribution should spawn from ruins picked by this cave generator? This list is copied from ores_spawned.dm into SSore_generation.ore_vent_minerals.
     * @param ruin_type The type of ruins that are spawning (ZTRAIT_SPACE_RUINS, ZTRAIT_ICE_RUINS, ZTRAIT_LAVA_RUINS, etc.)
     * @param immediate_load If TRUE, loads the placed ruins' maps in right away instead of leaving them queued in SSmapping.reserved_ruins for terrain generation to run first. Only safe to use outside of the roundstart mapping init sequence, once terrain generation has already finished.
     */
    // tg: code/modules/mapping/ruins.dm:80
    public static async DmTask seedRuins(DmList z_levels = null, float budget = 0, object whitelist = null, DmList potentialRuins = null, object clear_below = null,
        float mineral_budget = 15, object mineral_budget_update = null, object ruins_type = null, object immediate_load = null)
    {
        whitelist ??= Dm.list(typeof(area_space));
        clear_below ??= FALSE;
        ruins_type ??= ZTRAIT_STATION;
        immediate_load ??= FALSE;
        // #if defined(CIBUILDING) && !defined(OPENDREAM): the unit test map's CI skip; the port builds as OpenDream compiles tg (no skip)

        if (z_levels == null || !Dm.truthy(z_levels.len))
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", "No Z levels provided - Not generating ruins", "code/modules/mapping/ruins.dm", 87, null, usr));
            return;
        }
        DmList whitelist_typecache = typecacheof(whitelist) as DmList;

        foreach (object zl in Dm.each(z_levels))
        {
            turf T = locate(1, 1, Dm.num(zl));
            if (T == null)
            {
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Z level [] does not exist - Not generating ruins", zl), "code/modules/mapping/ruins.dm", 95, null, usr));
                return;
            }
        }

        DmList ruins = potentialRuins.Copy();
        float placed_ruins = 0; // our count of how many ruins have been placed
        DmList forced_ruins = Dm.list(); //These go first on the z level associated (same random one by default) or if the assoc value is a turf to the specified turf.
        DmList ruins_available = Dm.list(); //we can try these in the current pass

        // if(PERFORM_ALL_TESTS(maptest_log_mapping)) log_mapping("All ruins being loaded for map testing."): FALSE outside tg's map tests (rules 1.7)

        if (Dm.eq(mineral_budget_update, OREGEN_PRESET_LAVALAND)) //If we use more map configurations, add another case
            SSore_generation.ore_vent_minerals = expand_weights(GLOB.ore_vent_minerals_lavaland) as DmList;
        else if (Dm.eq(mineral_budget_update, OREGEN_PRESET_TRIPLE_Z))
            SSore_generation.ore_vent_minerals = expand_weights(GLOB.ore_vent_minerals_triple_z) as DmList;

        //Set up the starting ruin list
        foreach (object key in Dm.each(ruins))
        {
            var R = (datum_map_template_ruin)ruins[key];

            // PERFORM_ALL_TESTS(maptest_log_mapping): every ruin forced at no cost in tg's map tests; FALSE here (rules 1.7)

            if (R.cost > budget || R.mineral_cost > mineral_budget) //Why would you do that
                continue;
            if (Dm.truthy(R.always_place))
                forced_ruins[R] = -1;
            if (Dm.truthy(R.unpickable))
                continue;
            ruins_available[R] = R.placement_weight;
        }
        while (((budget > 0 || mineral_budget > 0) && Dm.truthy(ruins_available.len)) || Dm.truthy(forced_ruins.len))
        {
            datum_map_template_ruin current_pick = null;
            bool forced = false;
            object forced_z = null; //If set we won't pick z level and use this one instead.
            turf forced_turf = null; //If set we place the ruin centered on the given turf
            if (Dm.truthy(forced_ruins.len)) //We have something we need to load right now, so just pick it
            {
                foreach (object ruin in Dm.each(forced_ruins))
                {
                    current_pick = (datum_map_template_ruin)ruin;
                    if (isturf(forced_ruins[ruin]))
                    {
                        var T = (turf)forced_ruins[ruin];
                        forced_z = T.z; //In case of chained ruins
                        forced_turf = T;
                    }
                    else if (Dm.num(forced_ruins[ruin]) > 0) //Load into designated z
                    {
                        forced_z = forced_ruins[ruin];
                    }
                    forced = true;
                    break;
                }
            }
            else //Otherwise just pick random one
            {
                current_pick = (datum_map_template_ruin)pick_weight(ruins_available);
            }

            float placement_tries = forced_turf != null ? 1 : PLACEMENT_TRIES; //Only try once if we target specific turf
            bool failed_to_place = true;
            object target_z = 0;
            turf placed_turf = null; //Where the ruin ended up if we succeeded
            // outer: (the labelled while: `continue outer` goes on with the next try, `break outer` gives up)
            while (placement_tries > 0)
            {
                placement_tries--;
                target_z = pick(z_levels);
                if (Dm.truthy(forced_z))
                    target_z = forced_z;
                bool continue_outer = false, break_outer = false;
                if (Dm.truthy(current_pick.always_spawn_with)) //If the ruin has part below, make sure that z exists.
                {
                    foreach (object v in Dm.each(current_pick.always_spawn_with))
                    {
                        if (Dm.eq(current_pick.always_spawn_with[v], PLACE_BELOW))
                        {
                            turf T = locate(1, 1, Dm.num(target_z));
                            if (GET_TURF_BELOW(T) == null)
                            {
                                if (Dm.truthy(forced_z))
                                    continue_outer = true;
                                else
                                    break_outer = true;
                                break;
                            }
                        }
                    }
                }
                if (continue_outer)
                    continue;
                if (break_outer)
                    break;

                placed_turf = await current_pick.try_to_place(target_z, whitelist_typecache, forced_turf, clear_below) as turf;
                if (placed_turf == null)
                    continue;
                failed_to_place = false;
                break;
            }

            //That's done remove from priority even if it failed
            if (forced)
            {
                //TODO : handle forced ruins with multiple variants
                Dm.remove(ref forced_ruins, current_pick);
                forced = false;
            }

            if (failed_to_place)
            {
                foreach (datum_map_template_ruin R in Dm.each<datum_map_template_ruin>(ruins_available))
                {
                    if (Dm.eq(R.id, current_pick.id))
                        Dm.remove(ref ruins_available, R);
                }
                log_mapping(Dm.text("Failed to place [] ruin!", current_pick.name));
            }
            else
            {
                placed_ruins++;
                budget -= current_pick.cost;
                mineral_budget -= current_pick.mineral_cost;
                if (!Dm.truthy(current_pick.allow_duplicates))
                {
                    foreach (datum_map_template_ruin R in Dm.each<datum_map_template_ruin>(ruins_available))
                    {
                        if (Dm.eq(R.id, current_pick.id))
                            Dm.remove(ref ruins_available, R);
                    }
                }
                if (Dm.truthy(current_pick.never_spawn_with))
                {
                    foreach (object blacklisted_type in Dm.each(current_pick.never_spawn_with))
                    {
                        foreach (object possible_exclusion in Dm.each(ruins_available))
                        {
                            if (istype(possible_exclusion, (Type)blacklisted_type))
                                Dm.remove(ref ruins_available, possible_exclusion);
                        }
                    }
                }
                if (Dm.truthy(current_pick.always_spawn_with))
                {
                    foreach (object v in Dm.each(current_pick.always_spawn_with))
                    {
                        foreach (object ruin_name in Dm.each(SSmapping.ruins_templates)) //Because we might want to add space templates as linked of lava templates.
                        {
                            var linked = SSmapping.ruins_templates[ruin_name] as datum_map_template_ruin; //why are these assoc, very annoying.
                            if (!istype(linked, (Type)v))
                                continue;
                            object place = current_pick.always_spawn_with[v];
                            if (Dm.eq(place, PLACE_SAME_Z))
                                forced_ruins[linked] = target_z; //I guess you might want a chain somehow
                            else if (Dm.eq(place, PLACE_LAVA_RUIN))
                                forced_ruins[linked] = pick(SSmapping.levels_by_trait(ZTRAIT_LAVA_RUINS));
                            else if (Dm.eq(place, PLACE_SPACE_RUIN))
                                forced_ruins[linked] = pick(SSmapping.levels_by_trait(ZTRAIT_SPACE_RUINS));
                            else if (Dm.eq(place, PLACE_DEFAULT))
                                forced_ruins[linked] = -1;
                            else if (Dm.eq(place, PLACE_BELOW))
                                forced_ruins[linked] = GET_TURF_BELOW(placed_turf);
                            else if (Dm.eq(place, PLACE_ISOLATED))
                                forced_ruins[linked] = await SSmapping.get_isolated_ruin_z();
                        }
                    }
                }

                float bottom_left_x = placed_turf.x - Dm.round(current_pick.width / 2);
                float bottom_left_y = placed_turf.y - Dm.round(current_pick.height / 2);
                float top_right_x = bottom_left_x + current_pick.width - 1;
                float top_right_y = bottom_left_y + current_pick.height - 1;
                log_mapping(Dm.text("Successfully placed [] ruin ([],[],[] to [],[],[]).", current_pick.name, bottom_left_x, bottom_left_y, placed_turf.z, top_right_x, top_right_y, placed_turf.z));

                ///Keep track of the active ruins so we can take it in account for map generation. Using bottom lef turf as key
                SSmapping.active_ruins[locate(bottom_left_x, bottom_left_y, placed_turf.z)] = current_pick;

                if (Dm.truthy(immediate_load))
                    await SSmapping.load_ruin_now(current_pick, placed_turf, clear_below);
                else
                    ///Queue up the actual map load for after terrain generation, so cave/biome gen doesn't overwrite the ruin
                    Dm.append(ref SSmapping.reserved_ruins, Dm.list(Dm.list(current_pick, placed_turf, clear_below)));
            }

            //Update the available list
            foreach (datum_map_template_ruin R in Dm.each<datum_map_template_ruin>(ruins_available))
            {
                if (R.cost > budget || R.mineral_cost > mineral_budget)
                    Dm.remove(ref ruins_available, R);
            }
        }

        log_world(Dm.text("[] loader finished placing []/[] ruins with [] left to spend.", ruins_type, placed_ruins, ruins.len, budget));
    }
}
