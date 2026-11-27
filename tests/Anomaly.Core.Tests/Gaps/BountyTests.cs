using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P4c (D298): the bounties in a booted toy room with a supply shuttle (CargoEconomyTests' room). Every bounty kind is made
/// and read; a crewman takes a civilian bounty at the terminal, ships soap on its pad and gets a bounty cube, which sells on the cargo
/// shuttle into his account and the cargo budget, with the scanner's handling tip; the station bounties fill the list and pay their
/// helpers; the toolbox, reagent and patrol bounties check what they get; the Civilian Bounties app; the terminal's window data.
/// </summary>
public class BountyTests
{
    private static (obj_machinery_piratepad_civilian Pad, obj_machinery_computer_piratepad_control_civilian Terminal) Machines(CargoEconomyTests.Cargo c)
    {
        var pad = Place<obj_machinery_piratepad_civilian>(c.R, 6, 4);
        var terminal = Place<obj_machinery_computer_piratepad_control_civilian>(c.R, 5, 5);
        c.R.Step(2);
        Assert.Same(pad, c.R.In(() => terminal.pad_ref?.resolve()));
        return (pad, terminal);
    }

    private static obj_item_card_id Card(CargoEconomyTests.Cargo c, Type trim)
    {
        VendingTests.Funds(c.R, c.Bob, 0);
        return c.R.In(() =>
        {
            var card = (obj_item_card_id)c.Bob.wear_id;
            Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
            return card;
        });
    }

    private static object Act(CargoEconomyTests.Cargo c, datum target, string action, DmList @params = null)
    {
        var ui = c.R.In(() => Dm.@new<datum_tgui>(c.Bob, target, "CivCargoHoldTerminal", "terminal"));
        return c.R.In(() => Waiting.NoSleep(((datum)target).ui_act(action, @params ?? Dm.list(), ui, null)));
    }

    [Fact]
    public void Every_bounty_kind_is_made_and_read()
    {
        CargoEconomyTests.Cargo c = Room(9701);
        var failures = new List<string>();
        foreach (Type type in c.R.In(() => Dm.each_cast<Type>(subtypesof(typeof(datum_bounty))).ToList()))
        {
            // the patrols pick one of the station's areas: the toy room has none of them (Patrols_track_the_card_in_their_area)
            if (typeof(datum_bounty_patrol).IsAssignableFrom(type))
                continue;
            try
            {
                c.R.In(() =>
                {
                    var bounty = (datum_bounty)Dm.@new(type);
                    bool leaf = Dm.each_cast<Type>(subtypesof(type)).Count() == 0; // the kinds' bases are nameless
                    Assert.True(!leaf || !string.IsNullOrEmpty(Dm.str(bounty.name)), type.Name + " has a name");
                    Assert.True(Dm.num(bounty.get_bounty_reward()) >= 0);
                    _ = bounty.print_required();
                    _ = bounty.can_claim();
                    _ = bounty.can_get();
                    _ = bounty.get_total();
                    _ = bounty.get_max();
                });
            }
            catch (Exception e)
            {
                failures.Add(type.Name + ": " + e.Message);
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        c.R.AssertNoRuntimes();
    }

    private static CargoEconomyTests.Cargo Room(ulong seed) => CargoEconomyTests.Room(seed);

    [Fact]
    public void A_civilian_bounty_ships_on_the_pad_and_its_cube_sells_on_the_shuttle()
    {
        CargoEconomyTests.Cargo c = Room(9702);
        var (pad, terminal) = Machines(c);
        var card = Card(c, typeof(datum_id_trim_job_assistant));
        datum_bank_account mine = c.R.In(() => card.registered_account);

        // the card goes in; three bounties to choose from
        Assert.Equal(ITEM_INTERACT_SUCCESS, c.R.In(() => Waiting.NoSleep(terminal.item_interaction(c.Bob, card))));
        Assert.Same(card, c.R.In(() => terminal.inserted_scan_id));
        Act(c, terminal, "bounty");
        Assert.Equal(3, c.R.In(() => mine.bounties.len));
        DmList data = c.R.In(() => Waiting.NoSleep(terminal.ui_data(c.Bob)) as DmList);
        Assert.Equal(TRUE, c.R.In(() => Dm.num(data["picking"])));
        Act(c, terminal, "pick", c.R.In(() => Dm.list(Dm.kv("value", 1f))));
        Assert.NotNull(c.R.In(() => mine.civilian_bounty));
        Assert.Null(c.R.In(() => mine.bounties));

        // a known bounty: three bars of soap
        c.R.In(() => mine.set_bounty(Dm.@new<datum_bounty_item_assistant_soap>(), card));
        float reward = c.R.In(() => Dm.num(mine.civilian_bounty.get_bounty_reward()));
        var soaps = c.R.In(() => Enumerable.Range(0, 3).Select(_ => Dm.@new<obj_item_soap>(get_turf(pad))).ToList());
        Act(c, terminal, "recalc");
        Assert.Equal("Civilian Bounty: Target Applicable.", c.R.In(() => terminal.status_report));
        Act(c, terminal, "send");
        Assert.Equal(TRUE, c.R.In(() => Dm.num(terminal.sending)));
        c.R.Step(4 * Second); // the terminal's warmup
        Assert.True(c.R.In(() => soaps.All(s => QDELETED(s))), "the soap was shipped");
        Assert.Null(c.R.In(() => mine.civilian_bounty));
        var cube = c.R.In(() => Dm.each<obj_item_bounty_cube>(get_turf(terminal)).SingleOrDefault());
        Assert.NotNull(cube);
        Assert.Equal(reward, c.R.In(() => cube.bounty_value));
        Assert.EndsWith(c.R.In(() => Dm.text("[] cr bounty cube", reward)), c.R.In(() => cube.name)); // after tg's \improper mark
        Assert.NotNull(c.R.In(() => cube.GetComponent(typeof(datum_component_pricetag))));
        Assert.Contains("x1 Bounty completed!", c.R.In(() => terminal.status_report));

        // the card comes back to Bob's hand
        Act(c, terminal, "eject");
        Assert.Null(c.R.In(() => terminal.inserted_scan_id));
        Assert.Same(c.Bob, c.R.In(() => card.loc));

        // the cube on the shuttle: Eve's scanner registers her for the handling tip; the sale pays Bob's cut, Eve's tip (DV410) and
        // the budget the rest
        mob_living_carbon_human eve = c.R.Join(5, 3, "Eve");
        datum_bank_account hers = VendingTests.Funds(c.R, eve, 0);
        float budget = c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
        c.R.In(() => Waiting.NoSleep(cube.forceMove(locate(9, 5, 1))));
        var scanner = c.R.In(() => Dm.@new<obj_item_universal_scanner>(eve.loc));
        c.R.In(() => Waiting.NoSleep(scanner.export_scan(cube, eve)));
        Assert.Same(hers, c.R.In(() => cube.bounty_handler_account));
        // each economy fire boosts a few random exports threefold (tg's EXPORT_BOOST_MULT), and the cube's may be among them
        float boost = c.R.In(() => Dm.@in(typeof(datum_export_bounty_box), SSeconomy.boosted_exports)) ? EXPORT_BOOST_MULT : 1;
        c.R.In(() => Waiting.NoSleep(c.Shuttle.sell()));
        Assert.True(c.R.In(() => QDELETED(cube)));
        float value = (float)Math.Round(reward * 1.2 * boost, 2); // the speedy delivery bonus is still on
        float cut = round(value * 0.3f);
        float tip = round(value * 0.1f);
        Assert.Equal(cut, c.R.In(() => mine.account_balance));
        Assert.Equal(tip, c.R.In(() => hers.account_balance));
        Assert.Equal(budget + value - cut - tip, c.R.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_station_bounties_fill_the_list_and_pay_their_helpers()
    {
        CargoEconomyTests.Cargo c = Room(9703);
        c.R.In(() => GLOB.shared_crew_bounties.Cut());
        // a hallway, as every station has: the security job's patrol bounty, when the list rolls it, picks a patrol area (security.dm:24)
        c.R.In(() => Dm.@new(typeof(area_station_hallway_primary_central)));
        var (pad, terminal) = Machines(c);
        var card = Card(c, typeof(datum_id_trim_job_assistant));
        c.R.In(() => Waiting.NoSleep(terminal.item_interaction(c.Bob, card)));
        Act(c, terminal, "update_list");
        c.R.Step(10 * Second);
        float count = c.R.In(() => GLOB.shared_crew_bounties.len);
        Assert.True(count >= CIV_BOUNTY_BASELINE, "the list has " + count + ": " + string.Join("\n", c.R.Room.RuntimeText));
        Assert.True(c.R.In(() => Dm.each(GLOB.shared_crew_bounties).OfType<datum_bounty_item_special>().Any()), "the special bounties come with the first list");

        // a station bounty of soap, shipped with the global send
        var soap = c.R.In(() =>
        {
            var b = Dm.@new<datum_bounty_item_assistant_soap>();
            b.required_count = 1;
            GLOB.shared_crew_bounties.Insert(1, b);
            return b;
        });
        c.R.In(() => Dm.@new<obj_item_soap>(get_turf(pad)));
        Act(c, terminal, "send", c.R.In(() => Dm.list(Dm.kv("global", TRUE))));
        c.R.Step(4 * Second);
        Assert.Equal(TRUE, c.R.In(() => soap.claimed));
        Assert.Equal(1f, c.R.In(() => SSeconomy.civ_bounty_tracker));
        Assert.False(c.R.In(() => Dm.@in(soap, GLOB.shared_crew_bounties)), "a claimed bounty leaves the list");
        Assert.True(c.R.In(() => Dm.each<obj_item_bounty_cube>(get_turf(terminal)).Any()));
        DmList data = c.R.In(() => Waiting.NoSleep(terminal.ui_data(c.Bob)) as DmList);
        Assert.Equal(1f, c.R.In(() => Dm.num(data["claimed_bounties"])));

        // the printed list
        Act(c, terminal, "print");
        Assert.True(c.R.In(() => Dm.each<obj_item_paper>(get_turf(terminal)).Any(p => p.name == "paper - Bounties")));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_toolbox_and_reagent_bounties_check_what_they_get()
    {
        CargoEconomyTests.Cargo c = Room(9704);
        c.R.In(() =>
        {
            var toolbox = Dm.@new<datum_bounty_item_assistant_toolbox>();
            var box = Dm.@new<obj_item_storage_toolbox>(locate(5, 5, 1));
            foreach (atom_movable thing in Dm.each<atom_movable>(box.contents.Copy()))
                qdel(thing);
            Assert.Equal(FALSE, toolbox.applies_to(box));
            foreach (Type tool in new[] { typeof(obj_item_screwdriver), typeof(obj_item_wrench), typeof(obj_item_weldingtool), typeof(obj_item_crowbar), typeof(obj_item_analyzer), typeof(obj_item_wirecutters) })
                Dm.@new(tool, box);
            var extra = Dm.@new<obj_item_soap>(box);
            Assert.Equal(TRUE, toolbox.applies_to(box));
            Assert.Equal(TRUE, Waiting.NoSleep(toolbox.ship(box)));
            Assert.Same(get_turf(box), extra.loc); // what is not on the packing list comes back out
            Assert.Equal(TRUE, toolbox.can_claim());

            var drink = Dm.@new<datum_bounty_reagent_simple_drink>();
            Type wanted = ((datum_reagent)drink.wanted_reagent).GetType();
            var glass = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(locate(5, 5, 1));
            Assert.Equal(FALSE, drink.applies_to(glass));
            Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(wanted, 30));
            Assert.Equal(TRUE, drink.applies_to(glass));
            Assert.Equal(30f, Dm.num(drink.contribution_amount(glass)));
            Waiting.NoSleep(drink.ship(glass));
            Assert.Equal(TRUE, drink.can_claim()); // 10u wanted
            Assert.Equal("10/10 u", Dm.str(drink.print_required()));

            var soup = Dm.@new<datum_bounty_reagent_chef_soup>(); // its wanted reagent is a typepath (DV409)
            var pot = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(locate(5, 5, 1));
            Waiting.NoSleep(((datum_reagents)pot.reagents).add_reagent((Type)soup.wanted_reagent, 30));
            Assert.Equal(TRUE, soup.applies_to(pot));
        });
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void Patrols_track_the_card_in_their_area()
    {
        CargoEconomyTests.Cargo c = Room(9705);
        // the west columns become a hallway of 6x7 floor tiles
        c.R.In(() =>
        {
            var hall = Dm.@new<area_station_hallway_primary_central>();
            foreach (turf t in Dm.each<turf>(block(1, 2, 1, 6, 8, 1)))
            {
                t.change_area((area)t.loc, hall);
                Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
            }
        });
        var card = Card(c, typeof(datum_id_trim_job_security_officer));
        var patrol = c.R.In(() =>
        {
            datum_bounty_patrol p = null;
            for (int i = 0; i < 20 && (p == null || p.demanded_area != typeof(area_station_hallway_primary_central)); i++)
                p = Dm.@new<datum_bounty_patrol>();
            return p;
        });
        Assert.Equal(typeof(area_station_hallway_primary_central), c.R.In(() => patrol.demanded_area));
        float needed = c.R.In(() => patrol.needed_coverage);
        Assert.InRange(needed, 16, 34); // 42 tiles at 40-80 %
        Assert.StartsWith("Patrol Station: ", c.R.In(() => Dm.str(patrol.name)));
        c.R.In(() => card.registered_account.set_bounty(patrol, card));

        // walking the hallway counts each tile up to three times
        for (int round = 0; round < 3 && !c.R.In(() => Dm.truthy(patrol.can_claim())); round++)
        {
            for (int y = 2; y <= 8; y++)
            {
                for (int x = 1; x <= 6; x++)
                {
                    int tx = (y % 2 == 0) ? x : 7 - x;
                    c.R.In(() => Waiting.NoSleep(c.Bob.forceMove(locate(tx, y, 1))));
                }
            }
        }
        Assert.Equal(TRUE, c.R.In(() => patrol.can_claim()));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Patrol started."));
        Assert.Contains(c.Feed.Chat, m => m.Html.Contains("Patrol complete."));
        c.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_civilian_bounties_app_hands_out_a_bounty()
    {
        CargoEconomyTests.Cargo c = Room(9706);
        var card = Card(c, typeof(datum_id_trim_job_cook));
        var (pda, app) = c.R.In(() =>
        {
            var p = Dm.@new<obj_item_modular_computer_pda>(c.Bob.loc);
            Waiting.NoSleep(c.Bob.put_in_active_hand(p));
            Waiting.NoSleep(p.insert_id(card, c.Bob));
            var a = Dm.@new<datum_computer_file_program_civilianbounties>();
            p.store_file(a);
            return (p, a);
        });
        Act(c, app, "bounty");
        DmList data = c.R.In(() => Waiting.NoSleep(app.ui_data(c.Bob)) as DmList);
        Assert.Equal(TRUE, c.R.In(() => Dm.num(data["picking"])));
        Assert.Equal(3, c.R.In(() => ((DmList)data["id_bounty_names"]).len));
        Act(c, app, "pick", c.R.In(() => Dm.list(Dm.kv("value", 2f))));
        Assert.NotNull(c.R.In(() => card.registered_account.civilian_bounty));
        // the app keeps the choices after a pick (the terminal clears them); a second request waits five minutes
        DmList choices = c.R.In(() => card.registered_account.bounties);
        Act(c, app, "bounty");
        Assert.Same(choices, c.R.In(() => card.registered_account.bounties));
        c.R.AssertNoRuntimes();
    }
}
