// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/ore_generation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L1 (D304): the whole file; the ore vents' parts with L2c. The var block is generated (D111).

// tg: code/controllers/subsystem/ore_generation.dm:1 SUBSYSTEM_DEF(ore_generation)
public partial class datum_controller_subsystem_ore_generation : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(ore_generation) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/ore_generation.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSore_generation != this)
        {
            if (SSore_generation is datum_controller_subsystem_ore_generation)
            {
                Recover();
                qdel(SSore_generation);
            }
            SSore_generation = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/ore_generation.dm:30
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        /// First, lets sort each ore_vent here based on their distance to the landmark, then we'll assign sizes.
        DmList sort_vents = Dm.list();
        foreach (obj_structure_ore_vent vent in Dm.each<obj_structure_ore_vent>(possible_vents))
        {
            atom landmark_anchor = null; //We need to find the mining epicenter to gather distance from.
            foreach (atom possible_landmark in Dm.each<atom>(GLOB.mining_center)) // have to check multiple due to icebox
            {
                if (possible_landmark.z == vent.z)
                {
                    landmark_anchor = possible_landmark;
                    break;
                }
            }

            if (landmark_anchor == null) //We're missing a mining epicenter landmark, but it's not crash-worthy.
            {
                vent.vent_size_setup(random: TRUE, force_size: null, map_loading: TRUE);
                continue;
            }

            sort_vents.Insert(length(sort_vents), vent);
            sort_vents[vent] = get_dist(get_turf(vent), get_turf(landmark_anchor));
        }

        sortTim(sort_vents, "/proc/" + nameof(GlobalProcs.cmp_numeric_asc), associative: TRUE); // Should sort list from closest to farthest.
        possible_vents = sort_vents; // Now we can work with the main list

        float cutoff = round(length(possible_vents) / 3f);
        string vent_size_level = SMALL_VENT_TYPE;

        foreach (obj_structure_ore_vent vent in Dm.each<obj_structure_ore_vent>(possible_vents))
        {
            vent.vent_size_setup(random: FALSE, force_size: vent_size_level, map_loading: TRUE);
            cutoff--;
            if (cutoff > 0 || vent_size_level == LARGE_VENT_TYPE)
                continue;
            cutoff = round(length(possible_vents) / 3f);
            switch (vent_size_level)
            {
                case SMALL_VENT_TYPE:
                    vent_size_level = MEDIUM_VENT_TYPE;
                    break;
                case MEDIUM_VENT_TYPE:
                    vent_size_level = LARGE_VENT_TYPE;
                    break;
            }
        }

        //Finally, we're going to round robin through the list of ore vents and assign a mineral to them until complete.
        foreach (obj_structure_ore_vent vent in Dm.each<obj_structure_ore_vent>(possible_vents))
        {
            if (Dm.truthy(vent.unique_vent))
                continue; //Ya'll already got your minerals.
            vent.generate_mineral_breakdown(map_loading: TRUE);
        }

        logger.Log(
            LOG_CATEGORY_CAVE_GENERATION,
            "Ore Generation spawned the following vent sizes",
            Dm.list(
                Dm.kv("large", LAZYACCESS(GLOB.ore_vent_sizes, LARGE_VENT_TYPE)),
                Dm.kv("medium", LAZYACCESS(GLOB.ore_vent_sizes, MEDIUM_VENT_TYPE)),
                Dm.kv("small", LAZYACCESS(GLOB.ore_vent_sizes, SMALL_VENT_TYPE))));

        calculate_rock_edges();
        foreach (turf_closed_mineral_random rock in Dm.each<turf_closed_mineral_random>(ore_turfs)) // Typecheck in case they got destroyed
            rock.randomize_ore();

        calculate_ore_spread();

        return SS_INIT_SUCCESS;
    }

    /// Generates debug data about ore spread among rock turfs
    // tg: code/controllers/subsystem/ore_generation.dm:92
    public virtual void calculate_ore_spread()
    {
        DmList result = Dm.list();
        DmList totals = Dm.list(Dm.kv("chance", 0), Dm.kv("raw_sum", 0));
        float summary_count = 0;
        foreach (object rock_type in Dm.each<object>(ore_spread_probabilities))
        {
            DmList rock_data = (DmList)ore_spread_probabilities[rock_type];
            DmList result_rock = Dm.list();
            float total_count = 0;
            float total_chance = 0;
            foreach (object spread_range in Dm.each<object>(rock_data))
            {
                DmList dist_info = (DmList)rock_data[spread_range];
                foreach (object ore_type in Dm.each<object>(Dm.sub(dist_info, Dm.list("chance", "count"))))
                {
                    if (!Dm.truthy(result_rock[ore_type]))
                        result_rock[ore_type] = 0;
                    result_rock[ore_type] = Dm.num(result_rock[ore_type]) + Dm.num(dist_info[ore_type]) * Dm.num(dist_info["chance"]) / Dm.num(Dm.initial((System.Type)rock_type, "mineral_chance")) * Dm.num(dist_info["count"]);
                }
                total_count += Dm.num(dist_info["count"]);
                total_chance += Dm.num(dist_info["chance"]) * Dm.num(dist_info["count"]);
            }

            result_rock["chance"] = total_chance / total_count;
            result_rock["count"] = total_count;
            totals["chance"] = Dm.num(totals["chance"]) + total_chance;
            summary_count += total_count;
            result[rock_type] = result_rock;
        }

        foreach (object rock_type in Dm.each<object>(result))
        {
            DmList rock_data = (DmList)result[rock_type];
            float raw_sum = 0;
            foreach (object ore_type in Dm.each<object>(rock_data))
            {
                if (!ispath(ore_type))
                    continue;
                rock_data[ore_type] = Dm.num(rock_data[ore_type]) / summary_count;
                raw_sum += Dm.num(rock_data[ore_type]);
                if (!Dm.truthy(totals[ore_type]))
                    totals[ore_type] = 0;
                if (ispath(ore_type, typeof(turf_closed_mineral_gibtonite_volcanic)))
                    totals[typeof(turf_closed_mineral_gibtonite_volcanic)] = Dm.num(totals[typeof(turf_closed_mineral_gibtonite_volcanic)]) + Dm.num(rock_data[ore_type]);
                else
                    totals[ore_type] = Dm.num(totals[ore_type]) + Dm.num(rock_data[ore_type]);
            }
            rock_data["raw_sum"] = raw_sum;
            totals["raw_sum"] = Dm.num(totals["raw_sum"]) + raw_sum;
        }

        foreach (object spawn_type in Dm.each<object>(totals))
        {
            if (!ispath(spawn_type))
                continue;
            totals[spawn_type] = Dm.num(totals[spawn_type]) / (Dm.num(totals["raw_sum"]) / 104);
        }

        totals["count"] = summary_count;
        if (summary_count > 0)
            totals["chance"] = Dm.num(totals["chance"]) / summary_count;
        result["total"] = totals;
        ore_spread_probabilities = result;
    }

    // tg: code/controllers/subsystem/ore_generation.dm:144
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        available_boulders.Cut(); // reset upon new fire.
        foreach (obj_structure_ore_vent current_vent in Dm.each<obj_structure_ore_vent>(processed_vents))
        {
            float local_vent_count = 0;
            foreach (obj_item_boulder old_rock in Dm.each<obj_item_boulder>(current_vent.loc))
            {
                available_boulders.Add(old_rock);
                local_vent_count++;
            }

            if (local_vent_count >= MAX_BOULDERS_PER_VENT)
                continue; //We don't want to be accountable for literally hundreds of unprocessed boulders for no reason.

            Dm.append(available_boulders, await current_vent.produce_boulder());
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/ore_generation.dm:1  SUBSYSTEM_DEF(ore_generation): GLOBAL_REAL(SSore_generation, /datum/controller/subsystem/ore_generation)
    internal datum_controller_subsystem_ore_generation SSore_generation;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_ore_generation SSore_generation => ref TgState.Current.SSore_generation;
}
