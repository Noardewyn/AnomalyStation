using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate N1a (D316) through the real server on MetaStation: in the vault, a player puts a nuclear disk into the station self-destruct
/// terminal with a click, opens its NuclearBomb window with another and types the code and a 90 second timer on the client's keypad;
/// ARM starts the countdown at delta alert and turns the vault's circuit floors red. When it runs out the clients get tg's cinematic
/// screen (the station explosion picture) over their own, the station is nuked: the player is shredded and the round is over.
/// </summary>
public class NukeGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    // the NuclearBomb window as the client draws it now
    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Open.LastOrDefault(open => open.Interface == "NuclearBomb")?.Content;
    }

    [MetaServerFact]
    public void The_self_destruct_is_armed_on_its_keypad_and_nukes_the_station_with_the_cinematic()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld seen = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);

        var nuke = In(w, () => Dm.locate<obj_machinery_nuclearbomb_selfdestruct>(locate(83, 164, 1)));
        Assert.NotNull(nuke);
        Do(w, () =>
        {
            DevCommands.NukeCode("12345");
            Waiting.NoSleep(aliceMob.forceMove(locate(83, 163, 1)));
            var disk = Dm.@new<obj_item_disk_nuclear>(aliceMob.loc);
            Waiting.NoSleep(aliceMob.put_in_active_hand(disk));
        });
        w.Run(Second);

        // the disk goes in with a click, an empty hand opens the window
        Assert.True(alice.Session.Click(On(nuke), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.NotNull(In(w, () => nuke.auth));
        Assert.True(alice.Session.Click(On(nuke), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiNode content = Shown(windows, seen);
        Assert.True(content != null, "open: " + string.Join(", ", windows.Open.Select(open => open.Interface)));
        Assert.Equal("INPUT CODE", CryoGameplayTests.ByKey(content, "status1").Text);
        foreach (char key in "12345E90E")
        {
            CryoGameplayTests.ByKey(Shown(windows, seen), "key:" + key).OnClick();
            w.Run(2);
        }
        w.Run(Second / 2);
        content = Shown(windows, seen);
        Assert.Equal("DEVICE READY", CryoGameplayTests.ByKey(content, "status1").Text);
        Assert.Equal("TIME: 90", CryoGameplayTests.ByKey(content, "status2").Text);
        CryoGameplayTests.ByKey(content, "arm").OnClick();
        w.Run(Second);

        // the countdown at delta alert; the vault's circuit floors red
        Assert.True(In(w, () => Dm.truthy(nuke.timing)));
        Assert.Equal("DEVICE ARMED", CryoGameplayTests.ByKey(Shown(windows, seen), "status1").Text);
        Assert.Equal((float)SEC_LEVEL_DELTA, In(w, () => Dm.num(SSsecurity_level.get_current_level_as_number())));
        Assert.Equal("nuclearbomb_timing", CryoGameplayTests.StateOf(seen, nuke.dm_id));
        Assert.Equal("rcircuitanim", In(w, () => ((turf)locate(82, 164, 1)).icon_state));

        // ninety seconds, ten more of the alarm: the cinematic's picture on the client's screen
        string cinematic = null;
        for (int i = 0; i < 220 && cinematic == null; i++)
        {
            w.Run(Second / 2);
            foreach (ScreenEntry entry in seen.Screen.Values)
            {
                if (seen.TryGetAppearance(entry.Appearance, out AppearanceEntry look) && seen.ResolveResource(look.Icon) == "icons/effects/station_explosion.dmi")
                    cinematic = entry.ScreenLoc;
            }
        }
        output.WriteLine("the cinematic's screen_loc: " + cinematic);
        Assert.Equal("BOTTOM,LEFT+50%", cinematic);
        Assert.True(ScreenLoc.TryParse(cinematic, out _));

        // the station is nuked: Alice is shredded, the round is over
        w.Run(6 * Second);
        Assert.True(In(w, () => Dm.truthy(GLOB.station_was_nuked)));
        Assert.True(In(w, () => Dm.truthy(SSticker.check_finished())));
        Assert.True(In(w, () => QDELETED(aliceMob) || aliceMob.stat == DEAD));
        // her ghost still sees the cinematic, on its summary (DV379)
        Assert.Contains(seen.Screen.Values, entry => seen.TryGetAppearance(entry.Appearance, out AppearanceEntry look)
            && seen.ResolveResource(look.Icon) == "icons/effects/station_explosion.dmi");
    }
}
