using System.IO;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate N1b (D316) through the real server: MetaStation with the lobby and a dynamic.toml (tg's file as a server operator edits it)
/// whose only tier is Low Chaos and whose only roundstart ruleset is the nuclear operatives, from any population. Alice and Bob turn the
/// operative on in the lobby's Antagonists page and ready up; at the round's start Dynamic makes them the squad: forced into the operative
/// job, they wake in the syndicate base's elevator (its lazy template), one of them the leader; three seconds on the base loads, the
/// elevator's contents move down into it and its shutters open, and the leader's infiltrator is called and flies to its dock at the base.
/// </summary>
public sealed class NukieRoundTests : IDisposable
{
    private const int Second = 20;
    private const float GameStatePregame = Defines.GAME_STATE_PREGAME, GameStatePlaying = Defines.GAME_STATE_PLAYING;

    private readonly ITestOutputHelper _output;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-nukies-" + Guid.NewGuid().ToString("N"));

    public NukieRoundTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(Path.Combine(_folder, "saves"));
        File.WriteAllText(Path.Combine(_folder, "dynamic.toml"),
            "[\"Greenshift\"]\nweight = 0\n\n[\"Low Chaos\"]\nweight = 1\nruleset_type_settings.roundstart.low = 1\nruleset_type_settings.roundstart.high = 1\n\n"
            + "[\"Low-Medium Chaos\"]\nweight = 0\n\n[\"Medium-High Chaos\"]\nweight = 0\n\n[\"High Chaos\"]\nweight = 0\n\n"
            + "[\"Roundstart Traitor\"]\nweight = 0\n\n[\"Roundstart Blood Brothers\"]\nweight = 0\n\n"
            + "[\"Roundstart Nukeops\"]\nweight = 5\nmin_pop = 0\nmin_antag_cap = 2\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    // the windows as the player's client draws them (one TguiWindows keeps each window's local state, as the menu's page)
    private static TguiWindow Window(TguiWindows windows, RealHeadlessWorld.Player p, string @interface)
    {
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static TguiWindow Window(RealHeadlessWorld.Player p, string @interface) => Window(WindowsOf(p), p, @interface);

    // the lobby's character setup, its Antagonists page, a click on the operative's picture, then the menu closed (tg's ui_close saves)
    private static void PreferOperative(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        TguiWindows windows = WindowsOf(p);
        Window(windows, p, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, p, "PreferencesMenu");
        Assert.NotNull(prefs);
        prefs.Content.Walk().Single(n => n.Key == "page:Antagonists").Children[0].OnClick();
        prefs = Window(windows, p, "PreferencesMenu");
        prefs.Content.Walk().Single(n => n.Key == "antag:operative").Walk().Single(n => n.Key == "icon").OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, p, "PreferencesMenu");
        Assert.Equal("green", prefs.Content.Walk().Single(n => n.Key == "antag:operative").Walk().Single(n => n.Key == "icon").Color);
        p.Session.UiClose(prefs.Index);
        w.Run(Second / 2);
    }

    private static void RunUntil(RealHeadlessWorld w, Func<bool> done, int seconds)
    {
        for (int i = 0; i < seconds * 4 && !In(w, done); i++)
            w.Run(Second / 4);
    }

    [MetaServerFact]
    public void Two_players_who_prefer_the_operative_wake_in_the_base_as_the_squad()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
            ConfigFile = Path.Combine(_folder, "server.txt"), PlayerSavesFolder = Path.Combine(_folder, "saves"),
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 49, TimeoutSteps = 40000 }, config, strict: false);
        int bootRuntimes = w.World.RuntimeErrorCount;
        for (int i = 0; i < 10 * Second && In(w, () => SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        Assert.Equal(GameStatePregame, In(w, () => SSticker.current_state));

        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        PreferOperative(w, alice);
        PreferOperative(w, bob);
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob })
            Window(p, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(2 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => SSticker.current_state));

        // the squad: both operatives of one team in the operative job, one the leader; they woke in the elevator
        var bodies = new[] { alice, bob }.Select(p => Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, p))).ToArray();
        var ops = bodies.Select(b => In(w, () => b.mind.has_antag_datum(typeof(datum_antagonist_nukeop)) as datum_antagonist_nukeop)).ToArray();
        Assert.All(ops, Assert.NotNull);
        Assert.Same(ops[0].nuke_team, ops[1].nuke_team);
        Assert.Single(ops, op => op is datum_antagonist_nukeop_leader);
        Assert.All(bodies, b => Assert.IsType<datum_job_nuclear_operative>(In(w, () => b.mind.assigned_role)));
        Assert.All(bodies, b => Assert.IsType<obj_item_clothing_shoes_combat>(b.shoes));
        _output.WriteLine("at the start: " + string.Join(", ", bodies.Select(b => In(w, () => Dm.str(AREACOORD(b))))));

        // they start in the elevator (its own template, in the base's control area); the base loads, the elevator's contents move down
        // into it, its shutters open
        var start = bodies.Select(b => In(w, () => (turf)get_turf(b))).ToArray();
        Assert.All(start, t => Assert.True(In(w, () => Dm.@in(t, GLOB.nukeop_elevator_start)), "in the elevator"));
        RunUntil(w, () => Dm.each<obj_machinery_door_poddoor>(Dm.world).Any(d => Dm.eq(d.id_tag, "nukiespawnlift"))
            && Dm.each<obj_machinery_door_poddoor>(Dm.world).Where(d => Dm.eq(d.id_tag, "nukiespawnlift")).All(d => !Dm.truthy(d.density)), 30);
        Assert.All(In(w, () => Dm.each<obj_machinery_door_poddoor>(Dm.world).Where(d => Dm.eq(d.id_tag, "nukiespawnlift")).ToList()),
            d => Assert.False(Dm.truthy(d.density), "the elevator's shutter is open"));
        _output.WriteLine("after: " + string.Join(", ", bodies.Select(b => In(w, () => Dm.str(AREACOORD(b))))));
        for (int i = 0; i < bodies.Length; i++)
        {
            turf now = In(w, () => (turf)get_turf(bodies[i]));
            Assert.NotSame(start[i], now);
            Assert.True(In(w, () => Dm.truthy(bodies[i].onSyndieBase())), "on the base");
        }

        // the leader's infiltrator flies to its dock at the base; the team learns the base nuke's code
        RunUntil(w, () => SSshuttle.getShuttle("syndicate") is obj_docking_port_mobile ship && ship.get_docked() is obj_docking_port_stationary dock && Dm.eq(dock.shuttle_id, "syndicate_away"), 120);
        Assert.Equal("syndicate_away", In(w, () => Dm.str(((SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile)?.get_docked() as obj_docking_port_stationary)?.shuttle_id)));
        RunUntil(w, () => ops[0].nuke_team.tracked_nuke != null, 30);
        Assert.IsType<obj_machinery_nuclearbomb_syndicate>(In(w, () => ops[0].nuke_team.tracked_nuke));

        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
