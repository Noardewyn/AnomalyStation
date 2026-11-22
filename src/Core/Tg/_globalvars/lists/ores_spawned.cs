// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/ores_spawned.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file.

/**
 * Sets of global lists breaking down the base spawning distributions for various maps and stations.
 *
 * Currently used for ore vents on roundstart when the map is generated. (See ore_vent.dm, seedRuins() and ore_generation.dm)
 * Ore vent lists here are copied to ore_vent_minerals on ruin seeding,
 * in order to dynamically adjust the spawn rates as materials are picked and set a global ore distribution from vents.
 *
 * By default vents pull 4 unique materials each, but this can vary with subtypes.
 */
public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/ores_spawned.dm:11  GLOBAL_LIST_INIT(ore_vent_minerals_lavaland, list(...))
    public DmList ore_vent_minerals_lavaland;

    // tg: code/_globalvars/lists/ores_spawned.dm:11  (GLOBAL_MANAGED(ore_vent_minerals_lavaland, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalore_vent_minerals_lavaland()
    {
        ore_vent_minerals_lavaland = Dm.list(
            Dm.kv(typeof(datum_material_iron), 14),
            Dm.kv(typeof(datum_material_glass), 13),
            Dm.kv(typeof(datum_material_plasma), 10),
            Dm.kv(typeof(datum_material_silver), 7),
            Dm.kv(typeof(datum_material_titanium), 6),
            Dm.kv(typeof(datum_material_gold), 5),
            Dm.kv(typeof(datum_material_uranium), 4),
            Dm.kv(typeof(datum_material_plastic), 1));
        Dm.append(ref gvars_datum_init_order, "ore_vent_minerals_lavaland");
    }

    // tg: code/_globalvars/lists/ores_spawned.dm:22  GLOBAL_LIST_INIT(ore_vent_minerals_triple_z, list(...))
    public DmList ore_vent_minerals_triple_z;

    // tg: code/_globalvars/lists/ores_spawned.dm:22  (GLOBAL_MANAGED(ore_vent_minerals_triple_z, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalore_vent_minerals_triple_z()
    {
        ore_vent_minerals_triple_z = Dm.list(
            Dm.kv(typeof(datum_material_iron), 25),
            Dm.kv(typeof(datum_material_glass), 24),
            Dm.kv(typeof(datum_material_plasma), 17),
            Dm.kv(typeof(datum_material_titanium), 10),
            Dm.kv(typeof(datum_material_silver), 10),
            Dm.kv(typeof(datum_material_gold), 8),
            Dm.kv(typeof(datum_material_uranium), 5),
            Dm.kv(typeof(datum_material_plastic), 1));
        Dm.append(ref gvars_datum_init_order, "ore_vent_minerals_triple_z");
    }
}
