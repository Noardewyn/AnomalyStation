using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice C (D281): a ghost's ways of moving around and watching, through the real server on MetaStation with the lobby on. Alice
/// observes from the lobby; Bob and Carol play. Alice's HUD Orbit button opens tg's orbit menu with Bob among the living; she orbits him,
/// follows him when he walks and stops when she moves. A double click on a turf jumps her there, one on Bob follows him. The Teleport
/// button's area list moves her to the area picked. The orbit menu's auto-observe puts her eye in Bob (protocol 51's eye) with Bob's HUD
/// on her screen; moving ends it and gives her own HUD back. Carol dies, and dead chat's (F) link of the death notice follows her body.
/// </summary>
public class ObserverOrbitGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const double North = 0, East = -Math.PI / 2, South = Math.PI, West = Math.PI / 2;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static (RealHeadlessWorld.Player Player, List<string> Texts) Join(RealHeadlessWorld w, string name, string address)
    {
        LoopbackClientTransport transport = w.Network.CreateClient();
        transport.Address = address;
        var client = new GameClient(transport, "127.0.0.1", w.Server.Port, name);
        var texts = new List<string>();
        client.ServerTextReceived += t => texts.Add(t.Html);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        return (w.Adopt(name, client), texts);
    }

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface)
        => Assert.Single(p.Session.Replica.UiWindows.Values, v => v.Interface == @interface);

    private static void Act(RealHeadlessWorld.Player p, string @interface, string action, params (string Key, UiValue Value)[] parameters)
        => Assert.True(p.Session.UiAct(Window(p, @interface).Window, action,
            parameters.Length == 0 ? null : UiValue.ObjectOf(parameters.Select(x => new KeyValuePair<string, UiValue>(x.Key, x.Value)).ToArray())));

    private static long ButtonOf<T>(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => p.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is T)).Id;

    private static bool HasButton<T>(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => p.Session.Replica.Screen.Values.Any(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is T));

    private static void ClickScreen(RealHeadlessWorld.Player p, long id)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    private static turf TurfOf(RealHeadlessWorld w, atom a) => In(w, () => GlobalProcs.get_turf(a));

    // Walks the player a tile or two along the first direction that moves him (a spawn may face a wall).
    private static void Walk(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob who)
    {
        turf start = TurfOf(w, who);
        foreach (double yaw in new[] { North, East, South, West })
        {
            p.Yaw = yaw;
            p.Keys = MoveKeys.Forward;
            for (int i = 0; i < Second && TurfOf(w, who) == start; i++)
                w.Run(1);
            w.Run(4);
            p.Keys = MoveKeys.None;
            w.Run(Second / 2);
            if (TurfOf(w, who) != start)
                return;
        }
        Assert.Fail(p.Name + " could not walk off his tile");
    }

    [MetaServerFact]
    public void A_ghost_orbits_follows_jumps_teleports_and_observes_through_a_players_eyes()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 20000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < Defines.GAME_STATE_PREGAME; i++)
            w.Run(1);
        (RealHeadlessWorld.Player alice, List<string> aliceTexts) = Join(w, "Alice", null);
        (RealHeadlessWorld.Player bob, _) = Join(w, "Bob", "10.1.2.3");
        (RealHeadlessWorld.Player carol, _) = Join(w, "Carol", "10.1.2.4");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.Message); previous?.Invoke(e); };

        // Alice observes; Bob and Carol ready up and the round starts.
        Act(alice, Limits.LobbyUiInterface, "observe");
        w.Run(Second);
        Assert.True(alice.Session.UiAct(Window(alice, "AlertModal").Window, "choose", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("choice", UiValue.Of("Yes")) })));
        w.Run(2 * Second);
        var ghost = Assert.IsType<mob_dead_observer>(MobOf(w, alice));
        Act(bob, Limits.LobbyUiInterface, "ready_toggle");
        Act(carol, Limits.LobbyUiInterface, "ready_toggle");
        w.Run(Second);
        In(w, () => GlobalProcs.SSticker.start_immediately = Dm.TRUE);
        for (int i = 0; i < 20 * Second && In(w, () => GlobalProcs.SSticker.current_state) != Defines.GAME_STATE_PLAYING; i++)
            w.Run(1);
        w.Run(2 * Second);
        var bobMob = Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, bob));
        var carolMob = Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, carol));
        string bobRef = In(w, () => (string)GlobalProcs.REF(bobMob));
        string bobName = In(w, () => Dm.str(bobMob.real_name));
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(GlobalProcs.SSpoints_of_interest.is_valid_poi(bobMob)))), "Bob is a point of interest");

        // 1. The HUD's Orbit button opens tg's orbit menu: Bob among the living, Alice among the ghosts.
        Assert.True(HasButton<atom_movable_screen_ghost_teleport>(w, alice), "the ghost HUD has the Teleport button");
        ClickScreen(alice, ButtonOf<atom_movable_screen_ghost_orbit>(w, alice));
        w.Run(Second);
        UiWindowState menu = Window(alice, "Orbit");
        UiValue bobEntry = menu.StaticData.Arr("alive").Single(e => e.Str("ref") == bobRef);
        Assert.Equal(bobName, bobEntry.Str("name"));
        Assert.True(bobEntry.Num("health") > 0);
        Assert.Contains(menu.StaticData.Arr("ghosts"), e => e.Str("full_name").Contains("Alice", StringComparison.Ordinal) || e.Str("ref") == In(w, () => (string)GlobalProcs.REF(ghost)));

        // 2. Alice orbits Bob, follows him when he walks and stops when she moves.
        Act(alice, "Orbit", "orbit", ("ref", UiValue.Of(bobRef)), ("auto_observe", UiValue.Of(false)));
        w.Run(Second);
        Assert.Same(bobMob, In(w, () => ghost.orbit_target));
        Assert.Same(TurfOf(w, bobMob), TurfOf(w, ghost));
        Assert.Contains(aliceTexts, t => t.Contains("Now orbiting", StringComparison.Ordinal));
        Assert.True(alice.Session.Replica.TryGet(In(w, () => ghost.dm_id), out _));
        Walk(w, bob, bobMob);
        Assert.Same(bobMob, In(w, () => ghost.orbit_target));
        Assert.Same(TurfOf(w, bobMob), TurfOf(w, ghost));
        Assert.Equal(bobRef, In(w, () => ghost.orbiting_ref as string));
        UiValue orbiting = Window(alice, "Orbit").Data.Get("orbiting");
        Assert.Equal(bobRef, orbiting?.Str("ref"));
        Walk(w, alice, ghost);
        Assert.Null(In(w, () => ghost.orbit_target));
        Assert.Null(In(w, () => ghost.orbiting));
        Assert.Null(In(w, () => ghost.orbiting_ref));

        // 3. A double click on a turf jumps there; one on Bob follows him.
        turf here = TurfOf(w, ghost);
        (int jx, int jy, int jz) = In(w, () => ((int)here.x + 2, (int)here.y, (int)here.z));
        var jumpTo = new ClickTarget(TargetKind.Turf, 0, jx, jy, jz, 16, 16);
        Assert.True(alice.Session.Click(jumpTo, ClickButton.Left, ClickModifiers.None));
        Assert.True(alice.Session.DblClick(jumpTo, ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Equal((jx, jy), In(w, () => ((int)ghost.x, (int)ghost.y)));
        var onBob = new ClickTarget(TargetKind.Entity, In(w, () => bobMob.dm_id), 0, 0, 0, 16, 16);
        Assert.True(alice.Session.Click(onBob, ClickButton.Left, ClickModifiers.None));
        Assert.True(alice.Session.DblClick(onBob, ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Same(bobMob, In(w, () => ghost.orbit_target));
        Assert.Same(TurfOf(w, bobMob), TurfOf(w, ghost));
        Walk(w, alice, ghost);
        Assert.Null(In(w, () => ghost.orbit_target));

        // 4. The Teleport button: tg's area list (tgui_input_list), the ghost lands in the area picked.
        ClickScreen(alice, ButtonOf<atom_movable_screen_ghost_teleport>(w, alice));
        w.Run(Second);
        UiWindowState list = Window(alice, "ListInputWindow");
        List<string> areas = list.StaticData.Arr("items").Select(v => v.Text).ToList();
        string pick = areas.FirstOrDefault(a => a.Contains("Bar", StringComparison.Ordinal)) ?? areas[areas.Count / 2];
        output.WriteLine($"{areas.Count} areas to jump to; picked {pick}");
        Act(alice, "ListInputWindow", "submit", ("entry", UiValue.Of(pick)));
        w.Run(Second);
        Assert.Equal(pick, In(w, () => Dm.str(GlobalProcs.get_area(ghost)))); // the list names an area as DM's text does ("The Bar")

        // 5. Observe Bob through the orbit menu's auto-observe: Alice's eye is Bob (protocol 51) and Bob's HUD is on her screen.
        ClickScreen(alice, ButtonOf<atom_movable_screen_ghost_orbit>(w, alice));
        w.Run(Second);
        Act(alice, "Orbit", "orbit", ("ref", UiValue.Of(bobRef)), ("auto_observe", UiValue.Of(true)));
        w.Run(Second);
        Assert.Same(bobMob, In(w, () => ghost.observetarget));
        Assert.Same(bobMob, In(w, () => ghost.client.eye));
        Assert.True(In(w, () => Dm.@in(ghost, bobMob.observers)), "Alice is among Bob's observers");
        long bobId = In(w, () => bobMob.dm_id);
        Assert.Equal(bobId, alice.Session.Replica.EyeEntity);
        Assert.Equal(0, bob.Session.Replica.EyeEntity);
        HashSet<long> bobHud = In(w, () =>
        {
            var ids = new HashSet<long>();
            DmList screen = bobMob.client.screen;
            for (int i = 1; i <= screen.Count; i++)
                if (screen[(float)i] is atom_movable m && m.dm_id != 0)
                    ids.Add(m.dm_id);
            return ids;
        });
        int shared = alice.Session.Replica.Screen.Keys.Count(bobHud.Contains);
        output.WriteLine($"Alice's screen: {alice.Session.Replica.Screen.Count} entries, {shared} of them Bob's");
        Assert.True(shared >= 5, "Bob's HUD reaches Alice's screen");
        Assert.False(HasButton<atom_movable_screen_ghost_orbit>(w, alice), "the ghost's own HUD is hidden while observing");
        Walk(w, bob, bobMob);
        Assert.Same(bobMob, In(w, () => ghost.observetarget));
        Assert.Same(TurfOf(w, bobMob), TurfOf(w, ghost));

        // Moving ends the orbit and the observing: her own eye and HUD come back.
        Walk(w, alice, ghost);
        Assert.Null(In(w, () => ghost.observetarget));
        Assert.Same(ghost, In(w, () => ghost.client.eye));
        Assert.Equal(0, alice.Session.Replica.EyeEntity);
        Assert.False(In(w, () => Dm.@in(ghost, bobMob.observers)), "Alice left Bob's observers");
        Assert.True(HasButton<atom_movable_screen_ghost_orbit>(w, alice), "the ghost's HUD is back");

        // 6. Carol dies: dead chat's (F) link of the death notice follows her body.
        In(w, () => Waiting.NoSleep(carolMob.death()));
        w.Run(Second);
        string notice = aliceTexts.Last(t => t.Contains("has died at", StringComparison.Ordinal));
        var links = new List<ChatLink>();
        string plain = HtmlText.ToPlain(notice, links); // the chat window's line: tg's unquoted href is a link the client offers
        ChatLink follow = Assert.Single(links);
        Assert.Equal("(F)", plain.Substring(follow.Start, follow.Length));
        string href = follow.Href;
        output.WriteLine("the death notice's link: " + href);
        Assert.Contains("follow=", href, StringComparison.Ordinal);
        Assert.True(alice.Session.Topic(href));
        w.Run(Second);
        Assert.Same(carolMob, In(w, () => ghost.orbit_target));
        Assert.Same(TurfOf(w, carolMob), TurfOf(w, ghost));

        // No runtime: the ghost's single clicks (its ClickOn, tg's attack_ghost) are translated since E2 (D287).
        Assert.True(runtimes.Count == 0, string.Join("\n", runtimes.Take(3)));
    }
}
