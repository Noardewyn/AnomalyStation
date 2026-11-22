// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/zlevel_manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the whole file (D108: SSmapping's station path calls it for the station level only).

// tg: code/modules/mapping/space_management/zlevel_manager.dm:1
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    // Populate the space level list and prepare space transitions
    // tg: code/modules/mapping/space_management/zlevel_manager.dm:2
    public virtual void InitializeDefaultZLevels()
    {
        if (Dm.truthy(z_list))  // subsystem/Recover or badminnery, no need
            return;

        z_list = Dm.list();
        z_level_to_plane_offset = Dm.list();
        z_level_to_lowest_plane_offset = Dm.list();
        // DEFAULT_MAP_TRAITS (__DEFINES/maps.dm:162, DECLARE_LEVEL :160), expanded inline (rules 11.1 (e)): the compiled-in
        // CentCom level (_maps/_basemap.dm)
        DmList default_map_traits = Dm.list(Dm.list(Dm.kv(DL_NAME, "CentCom"), Dm.kv(DL_TRAITS, Dm.list(Dm.kv(ZTRAIT_CENTCOM, TRUE), Dm.kv(ZTRAIT_NOPHASE, TRUE)))));

        if (!Dm.eq(default_map_traits.len, Dm.world.maxz))
        {
            log_mapping(Dm.text("More or less map attributes pre-defined ([]) than existent z-levels ([]). Ignoring the larger.", default_map_traits.len, Dm.world.maxz));
            if (default_map_traits.len > Dm.world.maxz)
                default_map_traits.Cut(Dm.world.maxz + 1);
        }

        for (float I = 1, I__end = default_map_traits.len; I <= I__end; I++)
        {
            var features = (DmList)default_map_traits[I];
            var S = Dm.@new<datum_space_level>(I, features[DL_NAME], features[DL_TRAITS]);
            manage_z_level(S, filled_with_space: FALSE);
        }
        generate_z_level_linkages(); // Default Zs don't use add_new_zlevel() so they don't automatically generate z-linkages.
    }

    /// Generates a real, honest to god new z level. Will create the actual space, and also generate a datum that holds info about the new plot of land
    /// Accepts the name, traits list, datum type, and if we should manage the turfs we create
    // tg: code/modules/mapping/space_management/zlevel_manager.dm:24
    public virtual async DmTask<object> add_new_zlevel(object name = null, object traits = null, Type z_type = null, float? contain_turfs = null)
    {
        traits ??= Dm.list();
        z_type ??= typeof(datum_space_level);
        contain_turfs ??= TRUE;
        // UNTIL(!adding_new_zlevel) (__DEFINES/_helpers.dm), expanded inline (rules 11.1 (e))
        while (Dm.truthy(adding_new_zlevel))
            await stoplag();
        adding_new_zlevel = TRUE;
        float new_z = z_list.len + 1;
        if (Dm.world.maxz < new_z)
        {
            Dm.world.incrementMaxZ();
            // CHECK_TICK (__DEFINES/_tick.dm:21), awaited in a suspending proc
            if (TICK_CHECK())
                await stoplag();
        }
        // TODO: sleep here if the Z level needs to be cleared
        var S = (datum_space_level)Dm.@new(z_type, new_z, name, traits);
        manage_z_level(S, filled_with_space: TRUE, contain_turfs: contain_turfs);
        generate_linkages_for_z_level(new_z);
        calculate_z_level_gravity(new_z);
        adding_new_zlevel = FALSE;
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NEW_Z, S) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NEW_Z]))
            SSdcs._SendSignal(COMSIG_GLOB_NEW_Z, Dm.list(SSdcs, S));
        return S;
    }

    /// Returns the /datum/space_level associated with the given z level.
    // tg: code/modules/mapping/space_management/zlevel_manager.dm:41
    public virtual object get_level(object z = null)
    {
        // RETURN_TYPE(/datum/space_level)
        // ISINRANGE(z, 1, length(z_list)) (__DEFINES/maths.dm), expanded inline (rules 11.1 (e))
        if (isnum(z) && Dm.ge(z, 1) && Dm.le(z, length(z_list)))
            return z_list[z];
        throw Dm.CRASH(Dm.text("Unmanaged z-level []! maxz = [], z_list.len = []", z, Dm.world.maxz, z_list != null ? z_list.len : "null"), "code/modules/mapping/space_management/zlevel_manager.dm", 45);
    }
}
