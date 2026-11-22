using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Tram;

/// <summary>
/// The tram port: tg's Tramstation (maps/map_jsons/tramstation.json) booted whole; its tram links its platforms, cabinet and
/// landmarks, and a call button sends it along the line to the button's platform, carrying what stands on it. The boot takes a
/// minute, so these are Meta facts (left out of the quick run).
/// </summary>
public class TramstationTramTests
{
    private static string Folder()
    {
        string folder = MetaMaps.Folder();
        return folder != null && File.Exists(Path.Combine(folder, "map_jsons", "tramstation.json")) ? folder : null;
    }

    private static World Boot(string folder, out TgTestWorld.Log log)
    {
        World world = TgTestWorld.Boot(out log, configure: w => MapSeam.Select(w, new MapSelection(folder, "tramstation")), compiledMap: TgWorld.CompiledMap, maxTicks: 600);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        world.Step(20);
        return world;
    }

    private static datum_transport_controller_linear_tram Tram()
    {
        DmList trams = (DmList)SStransport.transports_by_type[TRANSPORT_TYPE_TRAM];
        Assert.NotNull(trams);
        return Assert.Single(Dm.each<datum_transport_controller_linear_tram>(trams));
    }

    [MetaFact]
    public void ACallButtonSendsTheTramToItsPlatformCarryingWhatStandsOnIt()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        datum_transport_controller_linear_tram tram;
        obj_effect_landmark_transport_nav_beacon_tram_platform target;
        obj_item_wrench rider;
        float startX;
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            tram = Tram();
            Assert.NotNull(tram.paired_cabinet);
            Assert.NotNull(tram.idle_platform);
            Assert.True(Dm.truthy(tram.controller_operational));
            Assert.NotEmpty(Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules));

            // the call button of a platform the tram does not stand at
            obj_item_assembly_control_transport_call_button button = Dm.each<obj_item_assembly_control_transport_call_button>()
                .First(b => !Dm.eq(b.specific_transport_id, null) && Dm.eq(b.specific_transport_id, tram.specific_transport_id) && !Dm.eq(b.id, tram.idle_platform.platform_code));
            target = Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>()
                .First(p => Dm.eq(p.specific_transport_id, tram.specific_transport_id) && Dm.eq(p.platform_code, button.id));

            obj_structure_transport_linear_tram platform = Dm.each<obj_structure_transport_linear_tram>(tram.transport_modules).First();
            rider = Dm.@new<obj_item_wrench>(platform.locs[1]);
            startX = rider.x;
            Waiting.Detach(button.activate());
        }
        world.Step(20);
        using (world.Enter())
        {
            Assert.True(Dm.truthy(tram.controller_active), "the tram took the call");
            Assert.Equal(startX, rider.x); // the door chime (1.4 s) and the doors' closing (3 s) come first
        }
        world.Step(120);
        using (world.Enter())
            Assert.NotEqual(startX, rider.x); // the wrench rides with it
        for (int i = 0; i < 60; i++)
        {
            using (world.Enter())
                if (!Dm.truthy(tram.controller_active))
                    break;
            world.Step(20);
        }
        using (world.Enter())
        {
            Assert.False(Dm.truthy(tram.controller_active), "the tram stopped");
            Assert.Same(target, tram.idle_platform);
            Assert.Empty(log.Runtimes);
        }
    }
}
