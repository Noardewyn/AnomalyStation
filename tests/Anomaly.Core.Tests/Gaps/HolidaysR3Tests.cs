using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package R3 (D327): the holidays and their events in a booted toy room on the station's level. tg's own holiday date tests
/// (unit_tests/holidays.dm); a Christmas date fills the holidays; a surprise egg's prize; Halloween's treat bags and Poly's ghost; Santa
/// hears the prayers that name him and cannot open presents; a valentine protects their date and sees hearts; a christmas cracker pops
/// between two people; the decals and the drones take a holiday's colours and hat.
/// </summary>
public class HolidaysR3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            ((area)get_area(bob)).requires_power = FALSE;
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static obj_item_storage_backpack Backpack(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        var pack = Dm.@new<obj_item_storage_backpack>(who.loc);
        Waiting.NoSleep(who.equip_to_slot(pack, ITEM_SLOT_BACK));
        return pack;
    });

    // the event as its control makes it (_event.dm:178 run_event: new typepath(TRUE, src)), but not processing: the test drives it
    private static T Event<T>(AudioRoom r) where T : datum_round_event => r.In(() =>
    {
        var control = Dm.each<datum_round_event_control>(SSevents.control).FirstOrDefault(c => c.typepath == typeof(T));
        return Dm.@new<T>(FALSE, control);
    });

    [Fact]
    public void TheHolidaysAndTheirEventsAreLiveAndInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_holiday), typeof(datum_holiday_xmas), typeof(datum_holiday_easter), typeof(datum_holiday_hebrew_hanukkah),
            typeof(datum_round_event_easter), typeof(datum_round_event_rabbitrelease), typeof(datum_round_event_spooky),
            typeof(datum_round_event_valentines), typeof(datum_round_event_ghost_role_santa), typeof(datum_antagonist_santa),
            typeof(datum_antagonist_valentine), typeof(datum_antagonist_heartbreaker), typeof(datum_component_listen_prayers),
            typeof(obj_item_gift), typeof(obj_item_gift_anything), typeof(obj_item_surprise_egg), typeof(obj_item_toy_xmas_cracker),
            typeof(datum_status_effect_in_love), typeof(datum_action_cooldown_spell_teleport_area_teleport),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4801);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        foreach (string name in new[] { "Easter Eggselence", "Release the Rabbits!", "2 SPOOKY! (Halloween)", "Valentines!", "Visit by Santa" })
            Assert.Contains(name, names);
        r.AssertNoRuntimes();
    }

    // tg: code/modules/unit_tests/holidays.dm, the seven date tests
    [Fact]
    public void TgsHolidayDateTestsPass()
    {
        var (r, _, _) = Room(4802);
        bool Celebrates<T>(object dd, object mm, object yyyy, object ddd) where T : datum_holiday =>
            r.In(() => Dm.truthy(Dm.@new<T>().shouldCelebrate(dd, mm, yyyy, ddd)));
        Assert.True(Celebrates<datum_holiday_hebrew_hanukkah>(14, DECEMBER, 2123, TUESDAY), "December 14, 2123 was not Hanukkah.");
        Assert.True(Celebrates<datum_holiday_islamic_ramadan>(6, NOVEMBER, 2165, WEDNESDAY), "November 6, 2165 was not Ramadan.");
        Assert.True(Celebrates<datum_holiday_nth_week_thanksgiving>(26, NOVEMBER, 2020, THURSDAY), "November 26, 2020 was not Thanksgiving.");
        // tg passes (9, MAY, 3683, 2, SUNDAY): the fourth argument fills the weekday slot, which nth_week does not read
        Assert.True(Celebrates<datum_holiday_nth_week_mother>(9, MAY, 3683, 2), "May 9, 3683 was not Mother's Day.");
        Assert.True(Celebrates<datum_holiday_hello>(21, NOVEMBER, 2020, SATURDAY), "November 21, 2020 was not Hello day.");
        Assert.True(Celebrates<datum_holiday_new_year>(2, JANUARY, 1983, SUNDAY), "January 2, 1983 was not New Year.");

        // We expect 2 year's worth of moth week, falling on the last full week of july
        var produced = r.In(() => Dm.each(poll_holiday(typeof(datum_holiday_nth_week_moth), 6, 8, 2020, 2021, 31)).Select(Dm.str).ToHashSet());
        var predicted = Enumerable.Range(18, 9).Select(d => $"2020/7/{d}").Concat(Enumerable.Range(17, 9).Select(d => $"2021/7/{d}")).ToHashSet();
        Assert.Empty(produced.Except(predicted)); // improperly Moth Week
        Assert.Empty(predicted.Except(produced)); // not Moth Week
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AChristmasDateFillsTheHolidaysAndGreetsTheRound()
    {
        var (r, _, _) = Room(4803);
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_allow_holidays)]).config_entry_value = TRUE;
            DmWorldState.Current.RealtimeStart = (new DateTime(2026, 12, 25, 12, 0, 0, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds * 10d;
            GLOB.holidays = null;
        });
        Assert.True(r.In(() => Dm.truthy(fill_holidays())), "no holiday on Christmas day");
        var held = r.In(() => Dm.each(GLOB.holidays).Select(Dm.str).ToList());
        Assert.Contains(CHRISTMAS, held);
        Assert.Contains(FESTIVE_SEASON, held);
        Assert.True(r.In(() => Dm.truthy(check_holidays(CHRISTMAS))));
        Assert.Equal("Have a merry Christmas!", r.In(() => Dm.str(((datum_holiday)GLOB.holidays[CHRISTMAS]).greet())));
        Assert.False(string.IsNullOrEmpty(r.In(() => Dm.str(station_name()))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASurpriseEggHoldsAPrizeAndAChocolateEgg()
    {
        var (r, feed, bob) = Room(4804);
        var egg = Place<obj_item_surprise_egg>(r, 3, 4);
        Assert.StartsWith("egg-", r.In(() => Dm.str(egg.icon_state)));
        r.In(() => Waiting.NoSleep(egg.attack_self(bob)));
        Assert.True(feed.Any("and find a prize inside!"));
        Assert.True(r.In(() => QDELETED(egg)));
        var loot = r.In(() => Dm.each<obj_item>(((turf)bob.loc).contents).Select(o => o.GetType()).ToList());
        Assert.Contains(typeof(obj_item_food_chocolateegg), loot);
        Assert.True(loot.Count >= 2, "no prize: " + string.Join(",", loot.Select(t => t.Name)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HalloweenFillsTheBackpacksWithTreatsAndPolyBecomesAGhost()
    {
        var (r, _, bob) = Room(4805);
        var pack = Backpack(r, bob);
        var poly = Place<mob_living_basic_parrot_poly>(r, 8, 6);
        var ev = Event<datum_round_event_spooky>(r);
        r.In(() => Waiting.NoSleep(ev.start()));
        var bag = r.In(() => Dm.locate<obj_item_storage_spooky>(pack.contents));
        Assert.NotNull(bag);
        Assert.Equal(6, r.In(() => Dm.length(bag.contents)));
        Assert.True(r.In(() => QDELETED(poly)), "Poly is still alive");
        Assert.True(r.In(() => Dm.each<mob_living_basic_parrot_poly_ghost>(GLOB.mob_living_list).Any()), "no ghost of Poly");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SantaHearsThePrayersThatNameHimAndCannotOpenPresents()
    {
        var (r, feed, bob) = Room(4806);
        var nick = r.Join(6, 4, "Nick");
        r.In(() => Waiting.NoSleep(nick.mind.add_antag_datum(Dm.@new<datum_antagonist_santa>())));
        r.Step(Second);
        Assert.Equal("Santa Claus", r.In(() => Dm.str(nick.real_name)));
        Assert.True(r.In(() => HAS_MIND_TRAIT(nick, TRAIT_CANNOT_OPEN_PRESENTS) && HAS_MIND_TRAIT(nick, TRAIT_PRESENT_VISION)));
        Assert.True(r.In(() => nick.wear_suit is obj_item_clothing_suit_space_santa), "no santa suit");
        Assert.True(r.In(() => nick.back is obj_item_storage_backpack_santabag), "no gift bag");
        Assert.True(r.In(() => Dm.locate<datum_action_cooldown_spell_teleport_area_teleport_wizard_santa>(nick.actions) != null), "no Santa teleport");
        Assert.True(r.In(() => nick.mind.GetComponent(typeof(datum_component_listen_prayers)) != null), "Santa does not listen to prayers");

        r.In(() => { World w = World.Current; object old = w.usr; w.usr = bob; bob.pray("Dear Santa, a pony please"); w.usr = old; });
        r.Step(Second);
        Assert.True(feed.Chat.Any(c => c.Target == nick && c.Html.Contains("PRAYER: ") && c.Html.Contains("a pony please")), "Santa did not hear the prayer");

        var gift = r.In(() => Dm.@new<obj_item_gift>(nick.loc));
        r.In(() => Waiting.NoSleep(gift.attack_self(nick)));
        Assert.True(feed.Any("You're supposed to be spreading gifts, not opening them yourself!"));
        Assert.False(r.In(() => QDELETED(gift)));
        var lines = r.In(() => Dm.each((DmList)Waiting.NoSleep(gift.examine(nick))).Select(Dm.str).ToList());
        Assert.Contains(lines, l => l.Contains("It contains"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AValentineProtectsTheirDateAndSeesHearts()
    {
        var (r, _, bob) = Room(4807);
        var alice = r.Join(5, 4, "Alice");
        var pack = Backpack(r, bob);
        var ev = Event<datum_round_event_valentines>(r);
        r.In(() =>
        {
            Waiting.NoSleep(ev.give_valentines_things(bob));
            Waiting.NoSleep(ev.forge_valentines_objective(bob, alice));
            Waiting.NoSleep(ev.forge_valentines_objective(alice, bob));
        });
        var given = r.In(() => Dm.each<obj_item>(pack.contents).Select(o => o.GetType()).ToList());
        Assert.Contains(typeof(obj_item_paper_valentine), given);
        Assert.Contains(typeof(obj_item_storage_fancy_heart_box), given);
        Assert.Contains(typeof(obj_item_food_candyheart), given);

        var valentine = r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_valentine)) as datum_antagonist_valentine);
        Assert.NotNull(valentine);
        Assert.Same(r.In(() => alice.mind), r.In(() => valentine.date));
        Assert.Equal($"Protect {r.In(() => Dm.str(alice.mind.name))}, your date.", r.In(() => Dm.str(Dm.locate<datum_objective_protect_valentine>(valentine.objectives).explanation_text)));
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_in_love)))), "Bob is not in love");
        Assert.Contains("had a successful date!", r.In(() => Dm.str(valentine.roundend_report())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AChristmasCrackerPopsBetweenTwoPeople()
    {
        var (r, feed, bob) = Room(4808);
        var alice = r.Join(4, 4, "Alice");
        var cracker = r.In(() =>
        {
            var c = Dm.@new<obj_item_toy_xmas_cracker>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(c));
            return c;
        });
        Assert.Equal(TRUE, r.In(() => Dm.num(Waiting.NoSleep(cracker.attack__proc(alice, bob)))));
        Assert.True(feed.Any("*pop*"));
        Assert.Equal("cracker1", r.In(() => Dm.str(cracker.icon_state)));
        Assert.True(r.In(() => Dm.each<obj_item_paper>(((turf)bob.loc).contents).Any(p => Dm.str(p.name).EndsWith(" joke"))), "no joke");
        Assert.True(r.In(() => Dm.locate<obj_item_clothing_head_costume_festive>(((turf)alice.loc).contents) != null), "no paper hat");
        var half = r.In(() => alice.get_active_held_item() as obj_item_toy_xmas_cracker);
        Assert.NotNull(half);
        Assert.Equal("cracker2", r.In(() => Dm.str(half.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDecalsTheTramFloorAndTheDronesTakeAHolidaysColoursAndHat()
    {
        var (r, _, _) = Room(4809);
        r.In(() =>
        {
            var h = Dm.@new<datum_holiday>();
            h.name = "Test Day";
            h.holiday_colors = Dm.list("#FF0000", "#00FF00");
            h.holiday_hat = typeof(obj_item_clothing_head_costume_festive);
            GLOB.holidays = Dm.list(Dm.kv("Test Day", h));
        });
        var decal = Place<obj_effect_turf_decal_tile_neutral>(r, 6, 6);
        Assert.Contains(r.In(() => Dm.str(decal.color)), new[] { "#FF0000", "#00FF00" });
        Assert.Equal(DECAL_ALPHA, r.In(() => Dm.num(decal.alpha)));
        var drone = Place<mob_living_basic_drone>(r, 8, 6);
        Assert.True(r.In(() => drone.head is obj_item_clothing_head_costume_festive), "the drone took no holiday hat");
        r.AssertNoRuntimes();
    }
}
