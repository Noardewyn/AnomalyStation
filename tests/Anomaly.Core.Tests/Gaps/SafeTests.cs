using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G1: the combination safe (safe.dm) in a booted toy room. A click opens tg's "Safe" window with the dial, the open and
/// locked flags and the contents of an open safe; the dial's acts turn the number down ("turnright") and up ("turnleft") one tick at a
/// time, a turn the wrong way resets the tumblers, the last number unlocks it ("Spring!") and "open" opens it; "retrieve" hands an item to
/// the player; items go in by hand; the wrench resets the combination through two number inputs and ten seconds of work, with its sound.
/// </summary>
public class SafeTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob, obj_structure_safe Safe) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var safe = Place<obj_structure_safe>(r, 5, 4);
        r.In(() => safe.tumblers = Dm.list(30, 60)); // the combination is random in tg: 30 then 60
        return (r, bob, safe);
    }

    private static string OpenWindow(AudioRoom r, mob who, atom safe)
    {
        Take(who);
        Click(r, who, safe);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal("Safe", InterfaceOf(payload));
        return window;
    }

    private static JsonElement Dial(AudioRoom r, mob who, string window, string action, int amount)
    {
        Act(r, who, window, action, ("num", amount));
        return Data(r, who, out _);
    }

    private static void Unlock(AudioRoom r, mob who, string window)
    {
        Dial(r, who, window, "turnright", 70);
        Dial(r, who, window, "turnleft", 30);
    }

    [Fact]
    public void AClickOpensTheWindowWithTgsDataAndTheContentsStayHiddenWhileItIsShut()
    {
        var (r, bob, safe) = Room(9601);
        Take(bob);
        Click(r, bob, safe);
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("Safe", InterfaceOf(payload));
        Assert.Equal("safe", payload.GetProperty("config").GetProperty("title").GetString());
        Assert.Equal(UI_INTERACTIVE, payload.GetProperty("config").GetProperty("status").GetInt32());
        JsonElement data = payload.GetProperty("data");
        Assert.Equal((0, 0, 1, 0), (data.GetProperty("dial").GetInt32(), data.GetProperty("open").GetInt32(), data.GetProperty("locked").GetInt32(), data.GetProperty("broken").GetInt32()));
        Assert.False(data.TryGetProperty("contents", out _));
        Assert.Equal("safe_locked", r.In(() => safe.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDialTurnsATickAtATimeAndTheCombinationUnlocksIt()
    {
        var (r, bob, safe) = Room(9602);
        string window = OpenWindow(r, bob, safe);

        // "Left 70" is tg's act turnright: the dial goes down, 0 -> 99 ... 30 and the first number is hit
        JsonElement data = Dial(r, bob, window, "turnright", 70);
        Assert.Equal(30, data.GetProperty("dial").GetInt32());
        r.In(() => Assert.Equal((2f, 1f), (safe.current_tumbler_index, safe.locked)));

        // the second number is up: turning down again resets the tumblers to the first one
        data = Dial(r, bob, window, "turnright", 5);
        Assert.Equal(25, data.GetProperty("dial").GetInt32());
        r.In(() => Assert.Equal(1f, safe.current_tumbler_index));

        // 95 ticks down from 25 land on 30 again (the first number), then 30 up land on 60 (the second): the lock opens
        Dial(r, bob, window, "turnright", 95);
        r.In(() => Assert.Equal(2f, safe.current_tumbler_index));
        data = Dial(r, bob, window, "turnleft", 30);
        Assert.Equal((60, 0), (data.GetProperty("dial").GetInt32(), data.GetProperty("locked").GetInt32()));
        r.In(() => Assert.Equal((3f, 0f), (safe.current_tumbler_index, safe.locked)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOpenButtonOpensAnUnlockedSafeAndTheContentsShowTheItemsInside()
    {
        var r = new AudioRoom(13, 9, seed: 9603);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        // an item on the safe's tile is taken inside when the safe is made (safe.dm:75)
        var crowbar = Place<obj_item_crowbar>(r, 5, 5);
        var safe = Place<obj_structure_safe>(r, 5, 5);
        r.In(() =>
        {
            safe.tumblers = Dm.list(30, 60);
            Assert.Same(safe, crowbar.loc);
            Assert.Equal(crowbar.w_class, safe.space);
        });
        string window = OpenWindow(r, bob, safe);

        // locked: the open button does nothing
        Act(r, bob, window, "open");
        r.In(() => Assert.Equal(0f, safe.open ?? 0));

        Unlock(r, bob, window);
        Act(r, bob, window, "open");
        JsonElement data = Data(r, bob, out _);
        Assert.Equal((1, 0), (data.GetProperty("open").GetInt32(), data.GetProperty("locked").GetInt32()));
        JsonElement item = data.GetProperty("contents").EnumerateArray().Single();
        Assert.Equal(("pocket crowbar", crowbar.icon_state), (item.GetProperty("name").GetString(), item.GetProperty("sprite").GetString()));
        Assert.Equal("icons/obj/tools.dmi", item.GetProperty("icon").GetString());
        r.In(() => Assert.Equal("safe", safe.icon_state));

        // an open safe does not turn its dial
        Act(r, bob, window, "turnright", ("num", 5));
        r.In(() => Assert.Equal(60f, safe.dial));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RetrieveHandsTheItemOverAndAnItemGoesBackInByHand()
    {
        var r = new AudioRoom(13, 9, seed: 9604);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var crowbar = Place<obj_item_crowbar>(r, 5, 5);
        var safe = Place<obj_structure_safe>(r, 5, 5);
        r.In(() => safe.tumblers = Dm.list(30, 60));
        string window = OpenWindow(r, bob, safe);
        Unlock(r, bob, window);
        Act(r, bob, window, "open");
        Take(bob);

        Act(r, bob, window, "retrieve", ("index", 1));
        JsonElement data = Data(r, bob, out _);
        Assert.Equal(0, data.GetProperty("contents").GetArrayLength());
        r.In(() =>
        {
            Assert.Same(crowbar, bob.get_active_held_item());
            Assert.Equal(0f, safe.space);
        });

        // the same crowbar, clicked on the open safe: it goes in (item_interaction, :121)
        Click(r, bob, safe);
        r.In(() =>
        {
            Assert.Same(safe, crowbar.loc);
            Assert.Equal(crowbar.w_class, safe.space);
        });
        data = Data(r, bob, out _, steps: 40); // no act answers here: the update is SStgui's periodic one
        Assert.Equal(1, data.GetProperty("contents").GetArrayLength());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenLockOpensWithoutTheCombination()
    {
        var (r, bob, safe) = Room(9605);
        r.In(() => safe.explosion_count = 3); // BROKEN_THRESHOLD (safe.dm:10): the lock seems to be broken
        string window = OpenWindow(r, bob, safe);
        Act(r, bob, window, "open");
        JsonElement data = Data(r, bob, out _);
        Assert.Equal((1, 1), (data.GetProperty("open").GetInt32(), data.GetProperty("broken").GetInt32()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWrenchResetsTheCombinationWithTwoNumbersAndTenSecondsOfWork()
    {
        var r = new AudioRoom(13, 9, seed: 9606);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var safe = Place<obj_structure_safe_open>(r, 5, 4);
        object[] before = r.In(() => new[] { safe.tumblers[1], safe.tumblers[2] }); // the random combination tg made
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(bob));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(wrench)));
        r.In(() => Assert.Same(wrench, bob.get_active_held_item()));
        Take(bob);

        Click(r, bob, safe);
        JsonElement modal = LastFullUpdate(Take(bob), out string first);
        Assert.Equal("NumberInputModal", InterfaceOf(modal));
        Assert.Equal("Set tumbler #1 (0-99):", modal.GetProperty("static_data").GetProperty("message").GetString());
        Assert.Equal(99, modal.GetProperty("static_data").GetProperty("max_value").GetInt32());
        Act(r, bob, first, "submit", ("entry", 11));
        modal = LastFullUpdate(Take(bob), out string second);
        Assert.Equal("Set tumbler #2 (0-99):", modal.GetProperty("static_data").GetProperty("message").GetString());
        Act(r, bob, second, "submit", ("entry", 22));
        r.Step(2);
        r.In(() => Assert.Equal(before, new[] { safe.tumblers[1], safe.tumblers[2] })); // still the old combination while bob works

        // the wrench's sound, once at the start and once at the end of the ten seconds
        int ratchets = r.Sounds.Heard.Count(h => h.File?.Contains("ratchet") == true);
        Assert.Equal(1, ratchets);
        r.Step(10 * Second + 5);
        r.In(() => Assert.Equal(new object[] { 11f, 22f }, new[] { safe.tumblers[1], safe.tumblers[2] }));
        Assert.Equal(2, r.Sounds.Heard.Count(h => h.File?.Contains("ratchet") == true));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClosedSafeRefusesTheWrenchAndOtherItems()
    {
        var (r, bob, safe) = Room(9607);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(bob));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(wrench)));
        Click(r, bob, safe);
        Assert.DoesNotContain(Take(bob), o => o.Kind == TguiOutputKind.Initialize && o.WindowId != "lobby_menu"); // "must be open!": no number input
        r.In(() => Assert.Same(wrench, bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }
}
