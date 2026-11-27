using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// On the converted MetaStation: the blobstart, generic maintenance and security equipment landmarks filled their lists (the nuke
/// disk's fallback, the maintenance spawns and the spare security lockers read them), the AI's areas carry their ambience, the bar's
/// areas are listed, and the engine's radiation shutters are live.
/// </summary>
public class MetaLeftoversMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaLeftoversMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_landmarks_areas_and_radiation_shutters_are_live()
    {
        using (_meta.World.Enter())
        {
            _output.WriteLine($"blobstart {GLOB.blobstart.len}, maintenance {GLOB.generic_maintenance_landmarks.len}, secequipment {GLOB.secequipment.len}");
            Assert.Equal(25, GLOB.blobstart.len);
            Assert.Equal(30, GLOB.generic_maintenance_landmarks.len);
            Assert.InRange(GLOB.secequipment.len, 1, 4);
            Assert.All(Dm.each(GLOB.blobstart).ToList(), t => Assert.IsAssignableFrom<turf>(t));
            Assert.Empty(Dm.each<obj_effect_landmark_blobstart>());

            var ai = Dm.each<area_station_ai>().ToList();
            Assert.NotEmpty(ai);
            object malf = Dm.resource("sound/ambience/misc/ambimalf.ogg");
            foreach (area_station_ai a in ai)
            {
                if (!Dm.truthy(a.secure))
                    Assert.Null(a.ambientsounds);
                else if (a.annoying_ambience != null)
                    Assert.Contains(Dm.each(a.ambientsounds).ToList(), s => Dm.eq(s, malf));
            }
            Assert.Contains(ai, a => a.ambientsounds != null && Dm.each(a.ambientsounds).ToList().Any(s => Dm.eq(s, malf)));

            var bars = Dm.each<area_station_service_bar>().ToList();
            Assert.NotEmpty(bars);
            Assert.All(bars, b => Assert.Contains(b, Dm.each(GLOB.bar_areas).ToList()));

            var shutters = Dm.each<obj_machinery_door_poddoor_shutters_radiation>().Where(s => s.z > 0).ToList();
            Assert.Equal(11, shutters.Count);
            Assert.All(shutters, s => Assert.False(InertSeam.IsInert(s), "radiation shutters are inert"));
            Assert.All(shutters, s => Assert.Equal(Dm.truthy(s.density) ? RAD_EXTREME_INSULATION : RAD_NO_INSULATION, s.rad_insulation));
        }
    }

    [MetaFact]
    public void Metas_painters_dispenser_laptops_and_gateway_are_live_and_linked()
    {
        using (_meta.World.Enter())
        {
            var painters = Dm.each<obj_machinery_pdapainter>().Where(p => p.z > 0).ToList();
            Assert.True(painters.Count >= 6, $"{painters.Count} PDA painters");
            Assert.All(painters, p => Assert.False(InertSeam.IsInert(p), "a PDA painter is inert"));
            Assert.All(painters, p => Assert.True(p.pda_types.len > 0 && p.card_trims.len > 0, "a PDA painter offers nothing"));

            var dispensers = Dm.each<obj_machinery_pipedispenser>().Where(p => p.z > 0).ToList();
            Assert.NotEmpty(dispensers);
            Assert.All(dispensers, d => Assert.False(InertSeam.IsInert(d), "a pipe dispenser is inert"));

            var laptops = Dm.each<obj_item_modular_computer_laptop>().Where(l => l.z > 0 || l.loc != null).ToList();
            Assert.True(laptops.Count >= 2, $"{laptops.Count} laptops");
            Assert.All(laptops, l => Assert.False(InertSeam.IsInert(l), "a laptop is inert"));

            Assert.NotNull(GLOB.the_gateway);
            Assert.False(InertSeam.IsInert(GLOB.the_gateway));
            var consoles = Dm.each<obj_machinery_computer_gateway_control>().Where(c => c.z > 0).ToList();
            Assert.Single(consoles);
            Assert.Same(GLOB.the_gateway, consoles[0].G);
            _output.WriteLine($"painters {painters.Count}, dispensers {dispensers.Count}, laptops {laptops.Count}, gateway destinations {GLOB.gateway_destinations.len}");
        }
    }
}
