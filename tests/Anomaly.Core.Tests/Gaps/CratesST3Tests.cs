using System.Text.Json;
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
/// Gaps package ST3 (D333): the closets, crates and dispensers left, in a booted toy room with Bob as a player: the abandoned crate's
/// deca-code lock (a wrong guess answered by its bulls and cows, an invalid one refused, the multitool's AbandonedCrate window, the
/// right code's loot, the anti-tamper bomb), the water cooler (its paper cups, its jug taken off and put back, pushed over and stood up),
/// the trash bin's fill and its bag, the critter and stasis crates, the glittery crate's glitter, the closets' contents, the infinite
/// closet, and the wall clock's and calendar's examines.
/// </summary>
public class CratesST3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    // the answer to tg's input window of that interface (TextInputModal) the player has open
    private static void Answer(AudioRoom r, mob who, string iface, object entry)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    // tg's bulls and cows of a guess against a code, counted here on their own
    private static (int Bulls, int Cows) Score(string code, string guess)
    {
        int bulls = 0, cows = 0;
        for (int i = 0; i < code.Length; i++)
        {
            if (guess[i] == code[i])
                bulls++;
            else if (code.Contains(guess[i]))
                cows++;
        }
        return (bulls, cows);
    }

    // a guess of four different digits that is not the code
    private static string WrongGuess(string code)
    {
        foreach (string guess in new[] { "1234", "5678", "9012", "3456", "7890" })
            if (guess != code)
                return guess;
        throw new InvalidOperationException("no wrong guess");
    }

    [Fact]
    public void TheAbandonedCrateAnswersAWrongGuessWithBullsAndCowsAndOpensToItsCode()
    {
        var (r, feed, bob) = Room(8301);
        var crate = Place<obj_structure_closet_crate_secure_loot>(r, 5, 4);
        string code = r.In(() => Dm.str(crate.code));
        Assert.Equal(4, code.Length);
        Assert.Equal(4, code.Distinct().Count()); // four different digits
        Assert.All(code, c => Assert.True(char.IsDigit(c)));
        r.In(() => Assert.True(Dm.truthy(crate.locked)));

        // a wrong guess: the red light, one attempt fewer, its bulls and cows
        string wrong = WrongGuess(code);
        Use(r, bob, crate);
        Answer(r, bob, "TextInputModal", wrong);
        Assert.True(feed.Any("A red light flashes."));
        var (bulls, cows) = Score(code, wrong);
        r.In(() =>
        {
            Assert.Equal(9, crate.attempts);
            Assert.Equal(1, crate.previous_attempts.Count);
            DmList attempt = (DmList)crate.previous_attempts[1];
            Assert.Equal(wrong, Dm.str(attempt["attempt"]));
            Assert.Equal(bulls, (int)Dm.num(attempt["bulls"]));
            Assert.Equal(cows, (int)Dm.num(attempt["cows"]));
            Assert.True(Dm.truthy(crate.locked));
        });

        // an invalid guess (a repeated digit) is refused and costs nothing
        Use(r, bob, crate);
        Answer(r, bob, "TextInputModal", "1123");
        Assert.True(feed.Any("You leave the crate alone."));
        r.In(() => Assert.Equal(9, crate.attempts));

        // the multitool shows the attempts in tg's AbandonedCrate window
        Hold<obj_item_multitool>(r, bob);
        Take(bob);
        Use(r, bob, crate);
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("AbandonedCrate", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(9, data.GetProperty("attempts_left").GetInt32());
        JsonElement row = data.GetProperty("previous_attempts")[0];
        Assert.Equal(wrong, row.GetProperty("attempt").GetString());
        Assert.Equal(bulls, row.GetProperty("bulls").GetInt32());
        Assert.Equal(cows, row.GetProperty("cows").GetInt32());
        r.In(() => qdel(bob.get_active_held_item()));

        // the code unlocks it with one pick of the loot inside and the anti-tamper off
        Use(r, bob, crate);
        Answer(r, bob, "TextInputModal", code);
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(crate.locked));
            Assert.True(Dm.truthy(crate.spawned_loot));
            Assert.Equal(0f, crate.tamperproof);
        });
    }

    [Fact]
    public void TheLastWrongGuessSetsOffTheAntiTamperBomb()
    {
        var (r, feed, bob) = Room(8302);
        var crate = Place<obj_structure_closet_crate_secure_loot>(r, 6, 4);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 4, 1))));
        r.In(() => crate.attempts = 1);
        string wrong = WrongGuess(r.In(() => Dm.str(crate.code)));
        Use(r, bob, crate);
        Answer(r, bob, "TextInputModal", wrong);
        r.Step(Second);
        Assert.True(feed.Any("anti-tamper system activates"));
        r.In(() => Assert.True(QDELETED(crate)));
    }

    [Fact]
    public void TheWaterCoolerHandsOutCupsGivesUpItsJugTakesItBackAndTipsOver()
    {
        var (r, feed, bob) = Room(8303);
        var cooler = Place<obj_structure_reagent_dispensers_water_cooler>(r, 5, 4);
        r.In(() =>
        {
            Assert.NotNull(cooler.our_jug);
            Assert.Equal("water_cooler", cooler.icon_state);
            Assert.Equal(200f, Dm.num(((datum_reagents)cooler.reagents).total_volume));
        });
        Assert.Contains(Overlays(r, cooler), o => o == "water_cooler_overlay100");
        Assert.Contains("There are 25 paper cups left.", Examine(r, cooler, bob));

        // an empty hand takes a paper cup
        Use(r, bob, cooler);
        r.In(() =>
        {
            Assert.IsType<obj_item_reagent_containers_cup_glass_sillycup>(bob.get_active_held_item());
            Assert.Equal(24f, cooler.paper_cups);
            qdel(bob.get_active_held_item());
        });

        // in combat mode the hand takes the jug off, the cooler's water with it
        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, cooler);
        r.Step(9 * Second);
        var jug = r.In(() => bob.get_active_held_item() as obj_item_reagent_containers_cooler_jug);
        Assert.NotNull(jug);
        r.In(() =>
        {
            Assert.Null(cooler.our_jug);
            Assert.Equal("water_cooler_forlorn", cooler.icon_state);
            Assert.Equal(0f, Dm.num(((datum_reagents)cooler.reagents).total_volume));
            Assert.True(Dm.num(((datum_reagents)jug.reagents).total_volume) > 0);
            bob.set_combat_mode(FALSE);
        });

        // the jug goes back on with its water
        Use(r, bob, cooler);
        r.Step(9 * Second);
        r.In(() =>
        {
            Assert.Same(jug, cooler.our_jug);
            Assert.Equal("water_cooler", cooler.icon_state);
            Assert.True(Dm.num(((datum_reagents)cooler.reagents).total_volume) > 0);
        });

        // anchored, it will not tip; loosened, a right click pushes it over: the jug thrown, the water spilt
        RightClick(r, bob, cooler);
        Assert.True(feed.Any("it's anchored!"));
        r.In(() => Waiting.NoSleep(cooler.set_anchored(FALSE)));
        RightClick(r, bob, cooler);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(cooler.tipped));
            Assert.Null(cooler.our_jug);
            Assert.Equal("water_cooler_disgraced", cooler.icon_state);
            Assert.Equal(0f, Dm.num(((datum_reagents)cooler.reagents).total_volume));
        });
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("glassbash"));

        // a hand stands it up again, with no jug
        Use(r, bob, cooler);
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(cooler.tipped));
            Assert.Equal("water_cooler_forlorn", cooler.icon_state);
        });
    }

    [Fact]
    public void TheTrashBinShowsItsFillAndEmptiesIntoATrashBag()
    {
        var (r, feed, bob) = Room(8304);
        var bin = Place<obj_structure_closet_crate_bin>(r, 5, 4);
        Assert.Contains("trashbin_empty", Overlays(r, bin));
        r.In(() =>
        {
            Dm.@new<obj_item_trash_can>(bin);
            Dm.@new<obj_item_trash_can>(bin);
            bin.update_appearance();
        });
        Assert.Contains("trashbin_some", Overlays(r, bin));

        Use(r, bob, bin); // open it
        r.In(() => Assert.True(Dm.truthy(bin.opened)));
        var bag = Hold<obj_item_storage_bag_trash>(r, bob);
        // what was in it fell out when it opened: put two cans back in and fill the bag from it
        r.In(() =>
        {
            foreach (obj_item_trash_can can in Dm.each<obj_item_trash_can>(bin.loc))
                Waiting.NoSleep(can.forceMove(bin));
        });
        Use(r, bob, bin);
        Assert.True(feed.Any("You fill the bag."));
        r.In(() => Assert.Equal(2, Dm.each<obj_item_trash_can>(bag.contents).Count()));
        r.Step(2 * Second);
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("bin_close"));
    }

    [Fact]
    public void TheCritterCrateBreathesFromItsTankAndTheStasisSealBreaksOnce()
    {
        var (r, feed, bob) = Room(8305);
        var crate = Place<obj_structure_closet_crate_critter>(r, 5, 4);
        r.In(() =>
        {
            Assert.NotNull(crate.tank);
            Assert.Same(Waiting.NoSleep(crate.tank.return_air()), Waiting.NoSleep(crate.return_air()));
            Assert.Equal("crittercrate", crate.icon_state);
        });
        Assert.Contains("crittercrate_door", Overlays(r, crate));
        Use(r, bob, crate);
        r.In(() =>
        {
            Assert.True(Dm.truthy(crate.opened));
            Assert.Equal("crittercrate", crate.icon_state); // the body keeps its state
        });
        Assert.Contains("crittercrate_door_open", Overlays(r, crate));
        var tank = r.In(() => crate.tank);
        r.In(() => qdel(crate));
        r.Step(2);
        r.In(() => Assert.Same(locate(5, 4, 1), tank.loc));

        var stasis = Place<obj_structure_closet_crate_critter_stasis>(r, 6, 4);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 4, 1))));
        Use(r, bob, stasis);
        Assert.True(feed.Any("one-use stasis mechanism has been triggered"));
        r.In(() =>
        {
            Assert.False(Dm.truthy(stasis.stasis_sealed));
            Assert.Null(stasis.paint_jobs);
        });
    }

    [Fact]
    public void TheGlitteryCrateLeavesGlitterWhereItWas()
    {
        var (r, _, _) = Room(8306);
        var crate = Place<obj_structure_closet_crate_glitter>(r, 7, 6);
        r.In(() =>
        {
            crate.glitter_prob = 100;
            Waiting.NoSleep(step(crate, EAST));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(locate(8, 6, 1), crate.loc);
            var glitter = Dm.locate<obj_effect_decal_cleanable_glitter>(locate(7, 6, 1));
            Assert.NotNull(glitter);
            Assert.Equal("#ff8080", Dm.str(glitter.color));
        });
    }

    [Fact]
    public void TheClosetsAndCratesOpenFilledAsTgFillsThem()
    {
        var (r, _, bob) = Room(8307);
        var formal = Place<obj_structure_closet_gmcloset>(r, 2, 2);
        var organs = Place<obj_structure_closet_crate_freezer_organ>(r, 3, 2);
        var law = Place<obj_structure_closet_lawcloset>(r, 4, 2);
        var gold = Place<obj_structure_closet_crate_goldcrate_stocked>(r, 5, 2);
        r.In(() =>
        {
            // what populate_contents_immediate makes is there at once: the lawyers' badges, the gold crate's gold
            Assert.Equal(2, Dm.each<obj_item_clothing_accessory_lawyers_badge>(law.contents).Count());
            Assert.Equal(3, Dm.each<obj_item_stack_sheet_mineral_gold>(gold.contents).Count());
            Waiting.NoSleep(formal.open());
            Waiting.NoSleep(organs.open());
        });
        r.Step(2);
        r.In(() =>
        {
            // the formal closet's 20 things (generate_items_inside), the organ freezer's eight organs
            Assert.Equal(20, Dm.each<obj_item>(locate(2, 2, 1)).Count());
            Assert.Equal(2, Dm.each<obj_item_clothing_head_hats_tophat>(locate(2, 2, 1)).Count());
            Assert.Equal(8, Dm.each<obj_item_organ>(locate(3, 2, 1)).Count());
        });
    }

    [Fact]
    public void TheInfiniteClosetCopiesWhatIsPutInItAndShutsItself()
    {
        var (r, feed, _) = Room(8308);
        var closet = Place<obj_structure_closet_infinite>(r, 7, 6);
        r.In(() =>
        {
            Dm.@new<obj_item_trash_can>(closet);
            closet.auto_close_time = 2 * SECONDS;
        });
        r.Step(12 * Second); // SSobj's ticks: one copy each
        r.In(() => Assert.Equal(4, Dm.each<obj_item_trash_can>(closet.contents).Count()));
        r.In(() =>
        {
            Waiting.NoSleep(closet.open());
            Waiting.NoSleep(closet.close());
            Waiting.NoSleep(closet.open());
        });
        r.Step(4 * Second);
        Assert.True(feed.Any("closes on its own."));
        r.In(() => Assert.False(Dm.truthy(closet.opened)));
    }

    [Fact]
    public void TheWallClockAndCalendarTellTheTimeAndDate()
    {
        var (r, _, bob) = Room(8309);
        var clock = Place<obj_structure_sign_clock>(r, 5, 5);
        var calendar = Place<obj_structure_sign_calendar>(r, 6, 5);
        string time = Examine(r, clock, bob);
        Assert.Contains("The current NST (local) time is:", time);
        Assert.Contains("into the shift.", time);
        string date = Examine(r, calendar, bob);
        Assert.Contains("The current date is:", date);
        Assert.Contains(r.In(() => Dm.str(CURRENT_STATION_YEAR)), date);
    }
}
