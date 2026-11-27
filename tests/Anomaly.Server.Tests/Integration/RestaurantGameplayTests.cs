using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Through the real server: a joined cook opens the restaurant with a click on its portal, a robot tourist walks out to a seat and
/// orders a burger, the cook sees the order in its thought bubble when he looks at it, hands it the burger and is paid with a
/// holochip, and the fed tourist walks back into its portal.
/// </summary>
public class RestaurantGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool RunUntil(RealHeadlessWorld w, Func<bool> done, int seconds)
    {
        for (int t = 0; t < Second * seconds; t++)
        {
            if (In(w, done))
                return true;
            w.Run(1);
        }
        return In(w, done);
    }

    [Fact]
    public void A_cook_opens_the_restaurant_and_serves_a_robot_customer_its_order()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14261, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Cook");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        var me = (mob_living_carbon_human)w.MobOf(p);
        Do(w, () => Waiting.Detach(SSjob.equip_rank(me, SSjob.get_job_type(typeof(datum_job_cook)) as datum_job, me.client)));
        w.Run(Second / 2);
        Do(w, () => Waiting.NoSleep(me.drop_all_held_items()));
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the portal a tile west, a seat a tile north; only Americans come, and they order a plain burger
        var (portal, venue) = In(w, () =>
        {
            var portal = Dm.@new<obj_machinery_restaurant_portal_restaurant>(get_step(me, WEST));
            Dm.@new<obj_structure_holosign_robot_seat_restaurant>(get_step(me, NORTH));
            var venue = (datum_venue)portal.linked_venue;
            venue.customer_types = Dm.list(Dm.kv(typeof(datum_customer_data_american), 1));
            var american = (datum_customer_data)SSrestaurant.all_customers[typeof(datum_customer_data_american)];
            american.orderable_objects[venue.venue_type] = Dm.list(Dm.kv(typeof(obj_item_food_burger_plain), 1));
            return (portal, venue);
        });
        w.Run(Second);

        Assert.True(p.Session.Click(On(portal), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(venue.open)), "the cook's click on the portal opened the restaurant");

        Assert.True(RunUntil(w, () => Dm.each<mob_living_basic_robot_customer>(venue.current_visitors)
            .Any(c => ((datum_ai_controller)c.ai_controller).blackboard[BB_CUSTOMER_CURRENT_ORDER] != null), 60), "a customer sat and ordered");
        var customer = In(w, () => Dm.each<mob_living_basic_robot_customer>(venue.current_visitors).First());
        output.WriteLine("customer: " + In(w, () => customer.name));
        Assert.Contains(replica.Entities, e => e.Id == customer.dm_id);

        // looking at it shows its order: the private image of a burger in a thought bubble hangs on it
        Assert.True(p.Session.Hover(On(customer), w.Now));
        w.Run(Second);
        ImageEntry bubble = Assert.Single(replica.Images.Values, i => i.AttachedTo == customer.dm_id);
        Assert.True(replica.TryGetAppearance(bubble.Appearance, out AppearanceEntry look), "the bubble's appearance reached the client");
        output.WriteLine($"bubble: icon {replica.ResolveResource(look.Icon)} state {replica.ResolveString(look.IconState)} plane {look.Plane} layer {look.Layer} pixel_w {look.PixelW} pixel_z {look.PixelZ} alpha {look.Alpha} flags {look.AppearanceFlags} underlays {look.Underlays.Count} overlays {look.Overlays.Count}");
        foreach (TableRef under in look.Underlays)
            if (replica.TryGetAppearance(under, out AppearanceEntry u))
                output.WriteLine($"  underlay: icon {replica.ResolveResource(u.Icon)} state {replica.ResolveString(u.IconState)} plane {u.Plane} layer {u.Layer}");

        // the cook hands it the burger and is paid
        var burger = In(w, () =>
        {
            var b = Dm.@new<obj_item_food_burger_plain>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(b));
            return b;
        });
        w.Run(Second / 2);
        Assert.True(p.Session.Click(On(customer), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => QDELETED(burger)), "the customer ate the burger");
        Assert.True(In(w, () => venue.total_income > 0 && venue.customers_served == 1), "the venue was paid");
        Assert.True(In(w, () => Dm.each<obj_item_holochip>(range(2, me)).Any()), "a holochip lies by the customer");

        Assert.True(RunUntil(w, () => QDELETED(customer), 60), "the fed customer walked back into its portal");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
