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
/// Science's pads and small machines in toy rooms (machinery/quantum_pad.dm, computer/launchpad_control.dm, mechlaunchpad.dm,
/// computer/mechlaunchpad.dm, ecto_sniffer.dm): a quantum pad links to another by a multitool's buffer and sends who stands on it
/// there, then recharges; the launchpad console takes a launchpad from a multitool, and its window selects it, sets its target and
/// launches what lies on it; the orbital pad console takes a pad from a multitool and its window lists, renames and selects it; the
/// ecto sniffer reports paranormal activity when its wire is pulsed and turns off by hand.
/// </summary>
public class SmallMachineTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 15, int height = 9)
    {
        var r = new AudioRoom(width, height, seed: seed);
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, T item) where T : obj_item
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
        });
        r.Step(2);
        return item;
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
        });
        r.Step(2);
    }

    private static JsonElement Open(AudioRoom r, mob_living_carbon_human who, atom machine, out string windowId)
    {
        EmptyHands(r, who);
        Take(who);
        r.Step(Second);
        Click(r, who, machine);
        r.Step(4);
        return LastFullUpdate(Take(who), out windowId);
    }

    [Fact]
    public void AQuantumPadSendsWhoStandsOnItToItsLinkedPad()
    {
        var (r, feed, bob) = Room(15041);
        var here = r.In(() => Dm.@new<obj_machinery_quantumpad>(locate(4, 4, 1)));
        var there = r.In(() => Dm.@new<obj_machinery_quantumpad>(locate(11, 6, 1)));
        r.Step(2);
        Assert.Equal("qpad-idle", r.In(() => Dm.str(here.icon_state)));

        // the far pad's data goes to a multitool through its open panel, and from it to the near pad
        var multitool = Hold(r, bob, r.In(() => Dm.@new<obj_item_multitool>(bob.loc)));
        r.In(() =>
        {
            there.panel_open = TRUE;
            Waiting.NoSleep(there.multitool_act(bob, multitool));
            there.panel_open = FALSE;
        });
        Assert.Same(there, r.In(() => multitool.buffer));
        r.In(() => Waiting.NoSleep(here.multitool_act(bob, multitool)));
        Assert.Same(there, r.In(() => here.linked_pad));
        Assert.True(feed.Any("balloon: data uploaded from buffer"));
        r.In(() =>
        {
            multitool.set_buffer(here);
            Waiting.NoSleep(here.multitool_act(bob, multitool));
        });
        Assert.True(feed.Any("balloon: cannot link to self!"));

        // Bob stands on the pad and works it: it charges, then he is on the other pad
        EmptyHands(r, bob);
        r.In(() => here.last_teleport = -10 * 60 * Second); // the toy world starts at time 0, a round has run for a while
        Click(r, bob, here);
        Assert.True(r.In(() => Dm.truthy(here.teleporting)));
        r.Step(10 * Second);
        Assert.Same(r.In(() => there.loc), r.In(() => bob.loc));
        Assert.False(r.In(() => Dm.truthy(here.teleporting)));

        // and it recharges
        r.In(() => Waiting.NoSleep(bob.forceMove(here.loc)));
        Click(r, bob, here);
        Assert.True(feed.Any("is recharging power. Please wait"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLaunchpadConsoleLaunchesWhatLiesOnItsPad()
    {
        var (r, feed, bob) = Room(15042);
        var pad = r.In(() => Dm.@new<obj_machinery_launchpad>(locate(8, 4, 1)));
        var console = r.In(() => Dm.@new<obj_machinery_computer_launchpad>(locate(5, 4, 1)));
        r.Step(2);
        var multitool = Hold(r, bob, r.In(() => Dm.@new<obj_item_multitool>(bob.loc)));
        r.In(() =>
        {
            multitool.set_buffer(pad);
            Waiting.NoSleep(console.multitool_act(bob, multitool));
        });
        Assert.Equal(1, r.In(() => Dm.length(console.launchpads)));
        Assert.Null(r.In(() => multitool.buffer));
        Assert.True(feed.Any("You upload the data from the"));

        JsonElement payload = Open(r, bob, console, out string window);
        Assert.Equal("LaunchpadConsole", InterfaceOf(payload));
        JsonElement pads = payload.GetProperty("data").GetProperty("launchpads");
        Assert.Equal(1, pads.GetArrayLength());
        Assert.Equal("Launchpad", pads[0].GetProperty("name").GetString());

        Act(r, bob, window, "select_pad", ("id", 1));
        Act(r, bob, window, "set_pos", ("x", 2), ("y", 1));
        Assert.Equal((2f, 1f), r.In(() => (pad.x_offset, pad.y_offset)));
        Act(r, bob, window, "rename", ("name", "Range"));
        Assert.Equal("Range", r.In(() => pad.display_name));
        JsonElement data = Data(r, bob, out _);
        Assert.Equal(1, data.GetProperty("pad_active").GetInt32());
        Assert.Equal(10, data.GetProperty("range").GetInt32());

        var crayon = r.In(() => Dm.@new<obj_item_toy_crayon_red>(locate(8, 4, 1)));
        r.Step(2);
        Act(r, bob, window, "launch");
        r.Step(5 * Second);
        Assert.Same(r.In(() => locate(10, 5, 1)), r.In(() => crayon.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOrbitalPadConsoleListsAndRenamesItsPads()
    {
        var (r, feed, bob) = Room(15043);
        var pad = r.In(() => Dm.@new<obj_machinery_mechpad>(locate(8, 4, 1)));
        var console = r.In(() => Dm.@new<obj_machinery_computer_mechpad>(locate(5, 4, 1)));
        r.Step(2);
        Assert.StartsWith("handmade", r.In(() => Dm.str(console.id)));
        var multitool = Hold(r, bob, r.In(() => Dm.@new<obj_item_multitool>(bob.loc)));
        r.In(() =>
        {
            pad.panel_open = TRUE;
            Waiting.NoSleep(pad.multitool_act(bob, multitool));
            pad.panel_open = FALSE;
            Waiting.NoSleep(console.multitool_act(bob, multitool));
        });
        Assert.Equal(1, r.In(() => Dm.length(console.mechpads)));

        JsonElement payload = Open(r, bob, console, out string window);
        Assert.Equal("MechpadConsole", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal("Orbital Pad - Test Room", data.GetProperty("mechpads")[0].GetProperty("name").GetString()); // its area's name
        Assert.Equal(0, data.GetProperty("connected_mechpad").GetInt32());
        Act(r, bob, window, "select_pad", ("id", 1));
        Act(r, bob, window, "rename", ("name", "Mining"));
        Assert.Equal("Mining", r.In(() => pad.display_name));
        data = Data(r, bob, out _);
        Assert.Equal("Mining", data.GetProperty("pad_name").GetString());
        Assert.Equal(1, data.GetProperty("pad_active").GetInt32());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEctoSnifferReportsWhenPulsedAndTurnsOffByHand()
    {
        var (r, feed, bob) = Room(15044);
        var sniffer = r.In(() => Dm.@new<obj_machinery_ecto_sniffer>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal("ecto_sniffer", r.In(() => Dm.str(sniffer.icon_state)));
        r.In(() => Waiting.NoSleep(sniffer.wires.pulse(WIRE_ACTIVATE)));
        r.Step(2);
        Assert.True(feed.Any("levels of paranormal activity!"), "the sniffer said nothing");

        EmptyHands(r, bob);
        Click(r, bob, sniffer);
        Assert.False(r.In(() => Dm.truthy(sniffer.on)));
        Assert.Equal("ecto_sniffer-p", r.In(() => Dm.str(sniffer.icon_state)));
        Assert.True(feed.Any("balloon: sniffer turned off"));

        // a cut sensor wire stops the ghosts' sniffing
        r.In(() => Waiting.NoSleep(sniffer.wires.cut(WIRE_ACTIVATE)));
        Assert.False(r.In(() => Dm.truthy(sniffer.sensor_enabled)));
        r.AssertNoRuntimes();
    }
}
