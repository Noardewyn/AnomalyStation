using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tools;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The restaurant: the cook opens the restaurant at its portal with his ID, a robot tourist walks out of it to a seat and orders,
/// shows its order in a thought bubble to whoever looks at it and in its examine, eats the right food and pays for it, refuses a
/// wrong item, warns and then fights whoever hits it, and leaves through its portal when the venue closes. The bar's customers
/// order drinks in a glass. The round end reports what each venue earned.
/// </summary>
public class RestaurantTests
{
    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(17, 11, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root));
        return r;
    }

    private static mob_living_carbon_human Staff(AudioRoom r, int x, int y, Type job, string key = "Bob")
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.In(() => Waiting.Detach(SSjob.equip_rank(who, SSjob.get_job_type(job) as datum_job, who.client)));
        r.Step(5);
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                qdel(held);
        });
        r.Step(1);
        return who;
    }

    private static datum_venue Venue<T>(AudioRoom r) where T : datum_venue
        => r.In(() => SSrestaurant.all_venues[typeof(T)] as datum_venue);

    /// <summary>Only the American comes, and orders only <paramref name="order"/> at the venue kind.</summary>
    private static void OnlyAmericansOrdering(AudioRoom r, datum_venue venue, Type order) => r.In(() =>
    {
        venue.customer_types = Dm.list(Dm.kv(typeof(datum_customer_data_american), 1));
        var american = (datum_customer_data)SSrestaurant.all_customers[typeof(datum_customer_data_american)];
        american.orderable_objects[venue.venue_type] = Dm.list(Dm.kv(order, 1));
    });

    private static void RunUntil(AudioRoom r, Func<bool> done, float seconds, string what)
    {
        for (int i = 0; i < seconds * Second && !r.In(done); i++)
            r.Step();
        Assert.True(r.In(done), what);
    }

    private static datum_ai_controller Brain(mob_living_basic_robot_customer customer) => (datum_ai_controller)customer.ai_controller;

    private static mob_living_basic_robot_customer CustomerWithAnOrder(AudioRoom r, datum_venue venue)
    {
        RunUntil(r, () => Dm.each<mob_living_basic_robot_customer>(venue.current_visitors).Any(c => Brain(c).blackboard[BB_CUSTOMER_CURRENT_ORDER] != null), 60,
            "a customer came out of the portal, sat and ordered");
        return r.In(() => Dm.each<mob_living_basic_robot_customer>(venue.current_visitors).First());
    }

    private static void Hand(AudioRoom r, mob_living_carbon_human who, obj_item thing)
        => r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(thing)))));

    [Fact]
    public void The_cook_opens_the_restaurant_with_his_id_and_a_customer_orders_and_pays_for_its_burger()
    {
        AudioRoom r = Room(14201);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Staff(r, 6, 5, typeof(datum_job_cook));
        var portal = Place<obj_machinery_restaurant_portal_restaurant>(r, 5, 5);
        Place<obj_structure_holosign_robot_seat_restaurant>(r, 6, 6);
        datum_venue venue = Venue<datum_venue_restaurant>(r);
        OnlyAmericansOrdering(r, venue, typeof(obj_item_food_burger_plain));
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(portal));
            Assert.Same(venue, portal.linked_venue);
            Assert.Contains(Dm.each<object>(venue.restaurant_portals), p => p == portal);
            Assert.Single(Dm.each<obj_structure_holosign_robot_seat>(venue.linked_seats));
            // closed: the portal's door is drawn shut over it
            Assert.Contains("portal_door", OverlayStates(portal));
        });

        Click(r, bob, portal);
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(venue.open)), "the cook's ID opened the restaurant");
        r.In(() => Assert.DoesNotContain("portal_door", OverlayStates(portal)));

        mob_living_basic_robot_customer customer = CustomerWithAnOrder(r, venue);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(customer));
            Assert.EndsWith("-bot", customer.name);
            Assert.Equal("amerifat", customer.icon_state);
            Assert.Contains("amerifat_features", OverlayStates(customer));
            Assert.Contains("amerifat_clothes", OverlayStates(customer));
            Assert.Equal(typeof(obj_item_food_burger_plain), Brain(customer).blackboard[BB_CUSTOMER_CURRENT_ORDER]);
            // it sits on its seat, which the venue gives to it
            var seat = Brain(customer).blackboard[BB_CUSTOMER_MY_SEAT] as obj_structure_holosign_robot_seat;
            Assert.NotNull(seat);
            Assert.Same(customer, venue.linked_seats[seat]);
            Assert.Same(seat.loc, customer.loc);
            // the thought bubble: an image of a burger over it, for the player who looks at it
            Assert.NotNull(customer.hud_to_show_on_hover);
        });
        Assert.True(feed.Any("take a plain burger"), "the customer said its order");
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("tourist_talk", StringComparison.Ordinal));

        // hovering over it shows the player the order; its examine reads it
        r.Command(() => ClientSeam.Hover(bob.client, customer));
        r.Step(2);
        Assert.True(r.In(() => Dm.each<image>(bob.client.images).Any(i => i.loc == customer)), "the player looking at it sees its food thoughts");
        DmList examined = r.In(() => Waiting.NoSleep(customer.examine(bob)) as DmList);
        Assert.Contains(Dm.each<object>(examined), l => Dm.str(l).Contains("Their order was: \"I'll take a plain burger.\"", StringComparison.Ordinal));

        // the cook hands it its burger: it is eaten and paid for with a holochip
        var burger = r.In(() => Dm.@new<obj_item_food_burger_plain>(bob.loc));
        r.Step(Second);
        Hand(r, bob, burger);
        Click(r, bob, customer);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(burger), "the customer ate the burger");
            Assert.Equal(1, venue.customers_served);
            Assert.True(venue.total_income > 0, "the venue earned its price");
            Assert.NotEmpty(Dm.each<obj_item_holochip>(range(1, bob)));
            Assert.True(Dm.truthy(Brain(customer).blackboard[BB_CUSTOMER_EATING]));
            Assert.Null(Brain(customer).blackboard[BB_CUSTOMER_CURRENT_ORDER]);
        });
        Assert.True(feed.Any("pushes the plain burger into their mouth-shaped hole!") || feed.Any("into their mouth-shaped hole!"));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("cashregister", StringComparison.Ordinal));

        // fed, it leaves happy through its portal
        RunUntil(r, () => QDELETED(customer), 60, "the fed customer walked back into its portal");
        Assert.Empty(r.In(() => Dm.each<object>(venue.current_visitors).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wrong_item_is_refused_and_a_weapon_is_warned_about()
    {
        AudioRoom r = Room(14202);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = Staff(r, 6, 5, typeof(datum_job_cook));
        var portal = Place<obj_machinery_restaurant_portal_restaurant>(r, 5, 5);
        Place<obj_structure_holosign_robot_seat_restaurant>(r, 6, 6);
        datum_venue venue = Venue<datum_venue_restaurant>(r);
        OnlyAmericansOrdering(r, venue, typeof(obj_item_food_burger_plain));
        Click(r, bob, portal);
        mob_living_basic_robot_customer customer = CustomerWithAnOrder(r, venue);

        // a harmless wrong item: "No, I don't want that."
        var paper = r.In(() => Dm.@new<obj_item_paper>(bob.loc));
        Hand(r, bob, paper);
        Click(r, bob, customer);
        r.Step(Second);
        Assert.True(feed.Any("want that."), "the customer refused the paper");
        r.In(() =>
        {
            Assert.False(QDELETED(paper));
            Assert.Equal(typeof(obj_item_food_burger_plain), Brain(customer).blackboard[BB_CUSTOMER_CURRENT_ORDER]);
            Assert.Equal(0, venue.customers_served);
            Waiting.NoSleep(bob.dropItemToGround(paper));
        });

        // a weapon: the first and the second warning; the venue remembers who did it
        var knife = r.In(() => Dm.@new<obj_item_knife_kitchen>(bob.loc));
        Hand(r, bob, knife);
        r.In(() => bob.set_combat_mode(TRUE));
        Click(r, bob, customer);
        r.Step(Second);
        Assert.True(feed.Any("tread on me!"), "the first warning");
        Assert.True(r.In(() => Dm.num(venue.mob_blacklist[REF(bob)])) >= 1);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_bartender_opens_the_bar_and_a_customer_slurps_its_beer()
    {
        AudioRoom r = Room(14203);
        Feed feed = Listen(r);
        mob_living_carbon_human sam = Staff(r, 6, 5, typeof(datum_job_bartender), "Sam");
        var portal = Place<obj_machinery_restaurant_portal_bar>(r, 5, 5);
        Place<obj_structure_holosign_robot_seat_bar>(r, 6, 6);
        datum_venue bar = Venue<datum_venue_bar>(r);
        OnlyAmericansOrdering(r, bar, typeof(datum_reagent_consumable_ethanol_beer));
        Click(r, sam, portal);
        Assert.True(r.In(() => Dm.truthy(bar.open)), "the bartender's ID opened the bar");

        mob_living_basic_robot_customer customer = CustomerWithAnOrder(r, bar);
        r.In(() =>
        {
            var order = Brain(customer).blackboard[BB_CUSTOMER_CURRENT_ORDER] as datum_custom_order_reagent_drink;
            Assert.NotNull(order);
            Assert.Equal(typeof(datum_reagent_consumable_ethanol_beer), order.reagent_type);
        });
        Assert.True(feed.Any("take 10u of Beer"), "the customer ordered ten units of beer");

        // a glass with too little beer is no order; a glass of 15 units is
        var glass = r.In(() =>
        {
            var g = Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass>(sam.loc);
            Waiting.NoSleep(((datum_reagents)g.reagents).add_reagent(typeof(datum_reagent_consumable_ethanol_beer), 5));
            return g;
        });
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(bar.is_correct_order(glass, Brain(customer).blackboard[BB_CUSTOMER_CURRENT_ORDER]))), "5 units are too few");
        r.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(typeof(datum_reagent_consumable_ethanol_beer), 10)));
        // a glass is served set down in front of the customer (a click with it on the customer offers it a drink instead)
        r.In(() => Waiting.NoSleep(glass.forceMove(get_step(customer, customer.dir))));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(0, ((datum_reagents)glass.reagents).total_volume);
            Assert.False(QDELETED(glass), "the bar's customer gives the glass back");
            Assert.Equal(1, bar.customers_served);
            Assert.True(bar.total_income > 0);
        });
        Assert.True(feed.Any("in one go!"), "the customer slurped the beer");
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("sound/items/drink.ogg", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_card_without_the_access_does_not_open_and_closing_sends_the_customers_home()
    {
        AudioRoom r = Room(14204);
        Feed feed = Listen(r);
        mob_living_carbon_human sam = Staff(r, 4, 5, typeof(datum_job_assistant), "Sam");
        mob_living_carbon_human bob = Staff(r, 6, 5, typeof(datum_job_cook));
        var portal = Place<obj_machinery_restaurant_portal_restaurant>(r, 5, 5);
        Place<obj_structure_holosign_robot_seat_restaurant>(r, 6, 6);
        datum_venue venue = Venue<datum_venue_restaurant>(r);
        OnlyAmericansOrdering(r, venue, typeof(obj_item_food_burger_plain));

        Click(r, sam, portal);
        Assert.False(r.In(() => Dm.truthy(venue.open)), "an assistant's card does not open the restaurant");
        Assert.True(feed.Any("This card lacks the access to change this venue's status."));

        Click(r, bob, portal);
        mob_living_basic_robot_customer customer = CustomerWithAnOrder(r, venue);
        Click(r, bob, portal);
        r.In(() =>
        {
            Assert.False(Dm.truthy(venue.open));
            Assert.True(Dm.truthy(Brain(customer).blackboard[BB_CUSTOMER_LEAVING]), "closing sends the customers away");
            Assert.Contains("portal_door", OverlayStates(portal));
        });
        RunUntil(r, () => QDELETED(customer), 60, "the customer walked back into its portal");
        r.In(() =>
        {
            Assert.Empty(Dm.each<object>(venue.current_visitors));
            // its seat is free again
            Assert.All(Dm.each<object>(venue.linked_seats), s => Assert.Null(venue.linked_seats[s]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_customer_kind_is_made_with_its_look_and_goes()
    {
        AudioRoom r = Room(14205);
        mob_living_carbon_human bob = Staff(r, 6, 5, typeof(datum_job_cook));
        datum_venue venue = Venue<datum_venue_restaurant>(r);
        List<Type> kinds = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_customer_data))).ToList());
        Assert.Equal(11, kinds.Count);
        foreach (Type kind in kinds)
        {
            var customer = r.In(() => Dm.@new<mob_living_basic_robot_customer>(locate(3, 3, 1), kind, venue));
            r.Step(2);
            r.In(() =>
            {
                datum_customer_data data = (datum_customer_data)SSrestaurant.all_customers[kind];
                Assert.Equal(data.base_icon_state, customer.icon_state);
                Assert.Contains(customer.icon_state + "_features", OverlayStates(customer));
                Assert.Contains(customer.clothes_set, OverlayStates(customer));
                Assert.NotEmpty(Dm.each<object>(data.name_prefixes));
                Assert.True(Dm.truthy(customer.voice));
                // the French flag shows only on the way out
                Brain(customer).set_blackboard_key(BB_CUSTOMER_LEAVING, TRUE);
                customer.update_appearance(UPDATE_ICON);
                if (kind == typeof(datum_customer_data_french))
                    Assert.Contains("french_flag", OverlayStates(customer));
                if (kind == typeof(datum_customer_data_moth))
                {
                    Assert.Contains("mothbot_jetpack", OverlayStates(customer));
                    Assert.NotEmpty(Dm.each<object>(customer.underlays));
                }
                qdel(customer);
            });
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_customer_dies_into_robot_gibs_and_the_round_end_reports_the_venues()
    {
        AudioRoom r = Room(14206);
        datum_venue venue = Venue<datum_venue_restaurant>(r);
        var customer = r.In(() => Dm.@new<mob_living_basic_robot_customer>(locate(4, 4, 1), typeof(datum_customer_data_italian), venue));
        r.Step(2);
        r.In(() => Waiting.NoSleep(customer.death()));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(customer), "a robot customer is deleted when it dies");
            Assert.Contains(Dm.each<obj_effect_decal_cleanable>(range(4, locate(4, 4, 1))), d => d is obj_effect_decal_cleanable_blood_gibs_robot_debris || d is obj_effect_decal_cleanable_blood_oil);
            venue.customers_served = 3;
            venue.total_income = 450;
            string report = SSticker.market_report();
            Assert.Contains("served 3 customers and made 450", report);
            Assert.Contains("In total, they earned 450", report);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_portal_board_picks_its_venue_and_a_portal_built_from_it_serves_it()
    {
        AudioRoom r = Room(14207);
        var board = r.In(() => Dm.@new<obj_item_circuitboard_machine_restaurant_portal>(locate(3, 3, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(board));
            board.venue_type = typeof(datum_venue_bar);
            var portal = Dm.@new<obj_machinery_restaurant_portal>(locate(4, 4, 1));
            board.configure_machine(portal);
            datum_venue bar = (datum_venue)SSrestaurant.all_venues[typeof(datum_venue_bar)];
            Assert.Same(bar, portal.linked_venue);
            Assert.Contains(Dm.each<object>(bar.restaurant_portals), p => p == portal);
            qdel(portal);
            Assert.DoesNotContain(Dm.each<object>(bar.restaurant_portals), p => p == portal);
        });
        r.AssertNoRuntimes();
    }

    private static List<string> OverlayStates(atom a) => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList();
}
