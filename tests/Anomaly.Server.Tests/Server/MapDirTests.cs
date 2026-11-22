using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Server;

/// <summary>
/// DV508: a thing whose map says the way it faces is noted, so the 3D models' own facing rule leaves it as the mapper turned it; tg's random
/// spawner gives its dir to its pick, and the note with it.
/// </summary>
public class MapDirTests
{
    [Fact]
    public void A_random_spawners_pick_faces_as_the_map_turned_the_spawner()
    {
        var world = new World(3);
        // tg's strings/ files, as the server mounts them
        if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tgRoot)
            TgStrings.Mount(DmWorldState.Of(world).Files, tgRoot);
        TgWorld.Boot(world, _ => TestRoom.Build(8, 8));
        using (world.Enter())
        {
            // as the map loader makes a member with vars: the preloader sets them in the thing's New
            float previous = SSatoms.initialized;
            SSatoms.initialized = Defines.INITIALIZATION_INNEW_MAPLOAD;
            Dm.world.preloader_setup(Dm.list(Dm.kv("dir", (float)Dm.WEST)), typeof(obj_effect_spawner_random_structure_closet_empty));
            Dm.@new<obj_effect_spawner_random_structure_closet_empty>(locate(3, 3, 1));
            SSatoms.initialized = previous;
            List<atom> here = Dm.each_cast<atom>(((turf)locate(3, 3, 1)).contents).ToList();
            Assert.True(here.OfType<obj_structure_closet>().Count() == 1, string.Join(", ", here.Select(a => a.GetType().Name)));
            obj_structure_closet closet = here.OfType<obj_structure_closet>().Single();
            Assert.Equal((int)Dm.WEST, (int)Dm.num(closet.dir));
            Assert.True(MapSeam.HasMapDir(closet));

            // a thing no map turned is held to no way
            Assert.False(MapSeam.HasMapDir(Dm.@new<obj_structure_closet>(locate(5, 5, 1))));
        }
    }
}
