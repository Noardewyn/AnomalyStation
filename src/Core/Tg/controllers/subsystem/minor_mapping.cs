// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/minor_mapping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The vermin's migration (gaps package C4): the flat satchels and the weakpoints stay inert, rows.

// tg: code/controllers/subsystem/minor_mapping.dm:3 SUBSYSTEM_DEF(minor_mapping)
public partial class datum_controller_subsystem_minor_mapping : datum_controller_subsystem
{
    private const int PROB_SPIDER_REPLACEMENT = 50; // tg: code/controllers/subsystem/minor_mapping.dm:1

    // The New() that SUBSYSTEM_DEF(minor_mapping) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/minor_mapping.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSminor_mapping != this)
        {
            if (SSminor_mapping is datum_controller_subsystem_minor_mapping)
            {
                Recover();
                qdel(SSminor_mapping);
            }
            SSminor_mapping = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/minor_mapping.dm:19
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // tg's "#if defined(MAP_TEST) || defined(UNIT_TESTS)": a toy world has no station map to drop vermin on.
        if (SSmapping.current_map == null)
            return SS_INIT_NO_NEED;
        await trigger_migration(config.Get(typeof(datum_config_entry_number_mice_roundstart)));
        place_satchels(2f);
        float weakpoint_spawns = 5;
        if (HAS_TRAIT(SSstation, STATION_TRAIT_SPAWN_WEAKPOINTS))
            weakpoint_spawns = rand(6, 12);

        weakpoint_spawns += Dm.num(SSmapping.current_map?.bonus_weakpoints); //This will add 0 by default, or additional on large maps where it's included in the config.
        place_weakpoints(weakpoint_spawns);
        return SS_INIT_SUCCESS;
    }

    /// Spawns some critters on exposed wires, usually but not always mice
    // tg: code/controllers/subsystem/minor_mapping.dm:37
    public virtual async DmTask trigger_migration(object to_spawn = null)
    {
        float how_many = Dm.num(to_spawn ?? 10f);
        DmList exposed_wires = await find_exposed_wires();
        turf_open proposed_turf;
        while (how_many > 0 && Dm.length(exposed_wires) > 0)
        {
            proposed_turf = pick_n_take(exposed_wires) as turf_open;
            if (!Dm.truthy(valid_mouse_turf(proposed_turf)))
                continue;

            how_many--;

            object picked_path;
            if (HAS_TRAIT(SSstation, STATION_TRAIT_SPIDER_INFESTATION) && prob(PROB_SPIDER_REPLACEMENT))
                picked_path = typeof(mob_living_basic_spider_maintenance);
            else
                picked_path = pick_weight(vermin_chances);

            Dm.@new((Type)picked_path, proposed_turf);
        }
    }

    /// Returns true if a mouse won't die if spawned on this turf
    // tg: code/controllers/subsystem/minor_mapping.dm:56
    public virtual object valid_mouse_turf(turf_open proposed_turf = null)
    {
        if (proposed_turf == null)
            return FALSE;
        datum_gas_mixture turf_gasmix = proposed_turf.air;
        float turf_temperature = Dm.num(proposed_turf.temperature);
        return Dm.truthy(turf_gasmix.has_gas(typeof(datum_gas_oxygen), 5)) && turf_temperature < NPC_DEFAULT_MAX_TEMP && turf_temperature > NPC_DEFAULT_MIN_TEMP ? TRUE : FALSE;
    }

    // tg: code/controllers/subsystem/minor_mapping.dm:63
    public virtual void place_satchels(object satchel_amount = null)
    {
        Dm.Deferred("code/controllers/subsystem/minor_mapping.dm:63", "place_satchels: the flat satchels under the floor (the flat satchel stays inert)");
    }

    /// This behaves nearly the same as spawning underfloot satchels, but instead spawns weakpoints.
    // tg: code/controllers/subsystem/minor_mapping.dm:108
    public virtual void place_weakpoints(object weakpoint_amount = null)
    {
        Dm.Deferred("code/controllers/subsystem/minor_mapping.dm:108", "place_weakpoints: the hull weakpoints (the weakpoint stays inert, with the void raptor's assault)");
    }

    /// Areas for minor_mapping procs to avoid. Mutate or adjust based on use case.
    // tg: code/controllers/subsystem/minor_mapping.dm:125
    public virtual DmList get_blacklist_areas()
    {
        return Dm.list(typeof(area_station_holodeck), typeof(area_space_nearstation), typeof(area_station_solars));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/minor_mapping.dm:3  SUBSYSTEM_DEF(minor_mapping): GLOBAL_REAL(SSminor_mapping, /datum/controller/subsystem/minor_mapping)
    internal datum_controller_subsystem_minor_mapping SSminor_mapping;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_minor_mapping SSminor_mapping => ref TgState.Current.SSminor_mapping;

    // tg: code/controllers/subsystem/minor_mapping.dm:83
    public static async DmTask<DmList> find_exposed_wires()
    {
        DmList exposed_wires = Dm.list();

        DmList all_turfs = null;
        foreach (object z in Dm.each(SSmapping.levels_by_trait(ZTRAIT_STATION)))
            Dm.combine(ref all_turfs, Z_TURFS(Dm.num(z)));
        foreach (turf_open_floor_plating T in Dm.each<turf_open_floor_plating>(all_turfs))
        {
            if (Dm.truthy(await T.is_blocked_turf()))
                continue;

            //dont include multiz cables in the list because repairing them sucks
            obj_structure_cable cable = Dm.locate<obj_structure_cable>(T);
            if (cable != null && cable is not obj_structure_cable_multilayer_multiz)
                Dm.append(ref exposed_wires, T);
        }

        return shuffle(exposed_wires) as DmList;
    }
}
