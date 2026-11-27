using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The restaurant on the converted MetaStation: the kitchen's and the bar's portals and the two seat projectors boot live and are
/// linked to their venues; opened, each venue sends a robot customer out of its portal; closed, the customers walk back; and some
/// seconds go by with no runtime from the restaurant's files.
/// </summary>
public class RestaurantMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public RestaurantMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "_venue.cs", "generic_venues.cs", "custom_order.cs", "_customer.cs", "robot_customer.cs", "robot_customer_controller.cs",
        "robot_customer_behaviors.cs", "venue_price.cs", "restaurant.cs",
    };

    [MetaFact]
    public void Metas_restaurant_and_bar_portals_open_and_their_customers_come_and_go()
    {
        // the round is on (the venues process on SSobj, which waits in the lobby)
        TgTestWorld.RunLevel(_meta.World, RUNLEVEL_GAME);
        List<obj_machinery_restaurant_portal> portals;
        using (_meta.World.Enter())
        {
            portals = Dm.each<obj_machinery_restaurant_portal>(Dm.world).ToList();
            var creators = Dm.each<obj_item_holosign_creator_robot_seat>(Dm.world).ToList();
            _output.WriteLine($"Meta: {portals.Count} restaurant portals, {creators.Count} seat projectors");
            Assert.Equal(2, portals.Count);
            Assert.Equal(2, creators.Count);
            Assert.Single(portals.OfType<obj_machinery_restaurant_portal_restaurant>());
            Assert.Single(portals.OfType<obj_machinery_restaurant_portal_bar>());
            foreach (var portal in portals)
            {
                Assert.False(InertSeam.IsInert(portal));
                var venue = Assert.IsAssignableFrom<datum_venue>(portal.linked_venue);
                Assert.Contains(Dm.each<object>(venue.restaurant_portals), p => p == portal);
                Waiting.NoSleep(venue.toggle_open());
            }
            Assert.All(creators, c => Assert.False(InertSeam.IsInert(c)));
        }
        for (int tick = 0; tick < 300; tick++)
            _meta.World.Step();
        AssertNoRestaurantRuntimes();
        using (_meta.World.Enter())
        {
            foreach (var portal in portals)
            {
                var venue = (datum_venue)portal.linked_venue;
                var visitors = Dm.each<mob_living_basic_robot_customer>(venue.current_visitors).ToList();
                _output.WriteLine($"{venue.name}: {visitors.Count} customers");
                Assert.NotEmpty(visitors);
                Waiting.NoSleep(venue.toggle_open());
                Assert.All(visitors, v => Assert.True(Dm.truthy(((datum_ai_controller)v.ai_controller).blackboard[BB_CUSTOMER_LEAVING])));
            }
        }
        for (int tick = 0; tick < 400; tick++)
            _meta.World.Step();
        AssertNoRestaurantRuntimes();
    }

    private void AssertNoRestaurantRuntimes()
    {
        using (_meta.World.Enter())
        {
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            if (runtimes.Count > 0)
                _output.WriteLine(runtimes[0].ToString());
            Assert.Empty(runtimes);
        }
    }
}
