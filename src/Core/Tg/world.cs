// Translated from tgstation (snapshot 1d99bd7f): code/world.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//This file is just for the necessary /world definition
//Try looking in /code/game/world.dm, where initialization order is defined

public static partial class TgWorld
{
    /**
     * # World
     *
     * Two possibilities exist: either we are alone in the Universe or we are not. Both are equally terrifying. ~ Arthur C. Clarke
     *
     * The byond world object stores some basic byond level config, and has a few hub specific procs for managing hub visibility
     */
    // tg: code/world.dm:11
    // PORT: /world is the shim's facade DmWorld, which has no constructor of tg's: the var overrides of tg's /world type block
    // are assigned here, by the bootstrap (TgWorld.Boot), before anything else of the world runs (BYOND has them at compile
    // time). The facade stores them per world (DmWorldState).
    internal static void dm_init_vars(DmWorld world)
    {
        world.mob = typeof(mob_dead_new_player);
        world.turf = typeof(turf_open_space_basic);
        world.area = typeof(area_space);
        world.view = "15x15";
        // hub = "Exadv1.spacestation13" and hub_password: PORT: the BYOND hub is not ported (the facade has no hub vars).
        world.name = "/tg/ Station 13";
        // fps = 20: world.fps is the constant 20 in the port (DV6), which is tg's value.
        // cache_lifespan = 0 (BYOND's resource cache) and map_format = SIDE_MAP (BYOND's map renderer): PORT: not ported, the
        // client renders the map itself.
        // tg: #ifdef FIND_REF_NO_CHECK_TICK ... #endif not translated (inactive in production)
    }
}
