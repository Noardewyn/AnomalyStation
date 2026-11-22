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
/// Gaps package TP1 (D333's station order: the bridge's teleporter) in a booted toy room with players. The console, station and hub
/// link at their placement; the Teleporter window locks a beacon from the list, calibrates the hub in tg's time and the station
/// engages it; whoever walks into the engaged hub lands on the beacon. An uncalibrated jump can turn a man into a flyperson; the
/// regime switches to the gate. The hand teleporter opens a portal pair to an engaged teleporter's target from its list, the
/// right-click reopens it, a click dispels its own portal; the bluespace locator points at a tracking implant.
/// </summary>
public class TeleporterTP1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    // a tgui window the mob has open, answered with an act (as the client's message)
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }

    private static (obj_machinery_computer_teleporter Console, obj_machinery_teleport_station Station, obj_machinery_teleport_hub Hub, obj_item_beacon Beacon) Teleporter(AudioRoom r)
    {
        var console = Place<obj_machinery_computer_teleporter>(r, 3, 6);
        var station = Place<obj_machinery_teleport_station>(r, 4, 6);
        var hub = Place<obj_machinery_teleport_hub>(r, 5, 6);
        var beacon = Place<obj_item_beacon>(r, 12, 3);
        return (console, station, hub, beacon);
    }

    private static string BeaconKey(AudioRoom r, atom beacon) => r.In(() => Dm.str(format_text((get_area(beacon) as area).name)));

    private static void Walk(AudioRoom r, mob who, int x, int y, float dir)
    {
        r.Step(Second / 2);
        r.In(() => Waiting.NoSleep(who.Move(locate(x, y, 1), dir)));
        r.Step(4);
    }

    [Fact]
    public void The_engaged_hub_sends_whoever_walks_in_to_the_locked_beacon()
    {
        var r = new AudioRoom(15, 9, seed: 9991);
        Feed feed = Listen(r);
        var bob = r.Join(3, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var (console, station, hub, beacon) = Teleporter(r);
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(console));
            Assert.False(InertSeam.IsInert(station));
            Assert.False(InertSeam.IsInert(hub));
            Assert.Same(station, hub.power_station);
            Assert.Same(hub, station.teleporter_hub);
            Assert.Same(console, station.teleporter_console);
            Assert.Same(station, console.power_station);
            Assert.Equal("tele0", Dm.str(hub.icon_state));
            Assert.Equal("teleporter control console", Dm.str(console.name));
            Assert.Equal("teleport", Dm.str(console.icon_screen));
        });

        // the window: linked, no target
        Click(r, bob, console);
        var payload = LastUpdate(Take(bob), out string windowId);
        Assert.Equal("Teleporter", InterfaceOf(payload));
        var data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("power_station").GetInt32());
        Assert.Equal(1, data.GetProperty("teleporter_hub").GetInt32());
        Assert.Equal("None", data.GetProperty("target").GetString());
        Assert.Equal("Teleporter", data.GetProperty("regime_set").GetString());

        // the station refuses without a target
        r.In(() => Waiting.NoSleep(station.interact(bob)));
        Assert.True(feed.Any("No target detected."), Diag(feed, r));

        // lock the beacon from the list
        Act(r, bob, windowId, "settarget");
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", BeaconKey(r, beacon)));
        Assert.Same(beacon, r.In(() => console.target_ref?.resolve()));

        // calibrate: the station shows it, then the hub is calibrated after 5 s per missing matter bin tier
        float accuracy = r.In(() => hub.accuracy);
        Act(r, bob, windowId, "calibrate");
        Assert.True(feed.Any("Processing hub calibration to target..."), Diag(feed, r));
        Assert.Equal("controller-c", r.In(() => Dm.str(station.icon_state)));
        r.Step((int)(5 * (3 - accuracy)) * Second + Second);
        Assert.Equal(1f, r.In(() => hub.calibrated));
        Assert.True(feed.Any("Calibration complete."), Diag(feed, r));
        Assert.Equal("controller", r.In(() => Dm.str(station.icon_state)));

        // engage it: the hub lights up
        r.In(() => Waiting.NoSleep(station.interact(bob)));
        Assert.True(feed.Any("Teleporter engaged!"), Diag(feed, r));
        Assert.Equal("tele1", r.In(() => Dm.str(hub.icon_state)));

        // Bob walks into the hub and lands on the beacon, unchanged
        Walk(r, bob, 4, 5, EAST);
        Walk(r, bob, 5, 5, EAST);
        Walk(r, bob, 5, 6, NORTH);
        Assert.Equal((12f, 3f), r.In(() => (bob.x, bob.y)));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").Contains("portal", StringComparison.OrdinalIgnoreCase) || (s.File ?? "").Contains("phase", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotType<datum_species_fly>(r.In(() => bob.dna.species));
        Assert.Equal(0f, r.In(() => hub.calibrated)); // each jump uses the calibration up
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_uncalibrated_jump_can_make_a_flyperson_and_the_regime_switches_to_the_gate()
    {
        var r = new AudioRoom(15, 9, seed: 9992);
        Feed feed = Listen(r);
        var bob = r.Join(3, 5, "Bob");
        var rob = r.Dummy(7, 7);
        var (console, station, hub, beacon) = Teleporter(r);
        r.Step(Second);

        r.In(() =>
        {
            console.set_teleport_target(beacon);
            station.engaged = TRUE;
            hub.update_appearance();
            hub.accuracy = -7; // prob(30 - accuracy * 10) = 100: the accident for sure
            hub.Bumped(rob);
        });
        r.Step(2);
        Assert.Equal((12f, 3f), r.In(() => (rob.x, rob.y)));
        r.In(() => Assert.IsType<datum_species_fly>(rob.dna.species));
        Assert.True(feed.Any("You hear a buzzing in your ears."), Diag(feed, r));
        r.In(() => Assert.Contains(Dm.each(rob.organs), o => o is obj_item_organ_eyes_fly));

        // the gate regime: the target is dropped and the window says so
        Click(r, bob, console);
        LastUpdate(Take(bob), out string windowId);
        Act(r, bob, windowId, "regimeset");
        Assert.Equal("Gate", r.In(() => console.regime_set));
        Assert.Null(r.In(() => console.target_ref));
        Assert.Equal(0f, r.In(() => station.engaged));
        // no linked stations: the gate has none to lock
        Act(r, bob, windowId, "settarget");
        Assert.True(feed.Any("No active connected stations located."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_hand_teleporter_opens_portals_to_an_engaged_teleporter_and_the_locator_points_at_an_implant()
    {
        var r = new AudioRoom(15, 9, seed: 9993);
        Feed feed = Listen(r);
        var bob = r.Join(8, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var (console, station, hub, beacon) = Teleporter(r);
        r.Step(Second);
        r.In(() =>
        {
            console.set_teleport_target(beacon);
            station.engaged = TRUE;
        });

        var tele = r.In(() => Dm.@new<obj_item_hand_tele>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(tele)));
        Assert.False(r.In(() => InertSeam.IsInert(tele)));
        r.In(() => Waiting.Detach(tele.attack_self(bob)));
        string key = r.In(() => Dm.text("[] (Active)", get_area(beacon)));
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", key));
        Assert.True(feed.Any("Locked in."), Diag(feed, r));
        var portals = r.In(() => Dm.each<obj_effect_portal>().ToList());
        Assert.Equal(2, portals.Count);
        Assert.Contains(portals, p => r.In(() => Dm.num(get_dist(p, beacon)) <= 1)); // create_portal_pair's accuracy 1: a turf around the beacon
        Assert.Contains(portals, p => r.In(() => Dm.num(get_dist(p, bob)) == 1));
        Assert.Equal(1f, r.In(() => (float)Dm.length(tele.active_portal_pairs)));

        // the right-click reopens the last destination
        r.In(() => Waiting.NoSleep(tele.interact_with_atom_secondary(bob.loc as atom, bob, Dm.list())));
        Assert.Equal(2f, r.In(() => (float)Dm.length(tele.active_portal_pairs)));

        // a click on its own portal dispels the pair
        var mine = r.In(() => (obj_effect_portal)Dm.each(tele.active_portal_pairs).First());
        r.In(() => Waiting.NoSleep(tele.interact_with_atom(mine, bob, Dm.list())));
        Assert.True(feed.Any("You dispel"), Diag(feed, r));
        r.Step(2);
        Assert.Equal(1f, r.In(() => (float)Dm.length(tele.active_portal_pairs)));

        // the bluespace locator: a tracking implant two tiles east
        var rob = r.Dummy(10, 5);
        r.In(() =>
        {
            var implant = Dm.@new<obj_item_implant_tracking>(rob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(implant.implant(rob, bob))));
        });
        var locator = r.In(() => Dm.@new<obj_item_locator>(bob.loc));
        r.Step(2);
        var tracked = r.In(() =>
        {
            var d = (DmList)Waiting.NoSleep(locator.ui_data(bob));
            Assert.Equal(35f, Dm.num(d["trackingrange"]));
            return Dm.each(d["trackimplants"] as DmList).Cast<DmList>().Select(e => (Dm.str(e["name"]), Dm.str(e["direction"]), Dm.num(e["distance"]))).ToList();
        });
        Assert.Contains((r.In(() => Dm.str(rob.name)), "east", 2f), tracked);
        r.AssertNoRuntimes();
    }
}
