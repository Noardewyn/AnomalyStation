using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps S4d (D327) through the real server on MetaStation: six players ghost, open the Minigames menu from their HUD's button, pick
/// Mafia, sign up in the lobby and vote to start; the game loads one of tg's Mafia maps on CentCom's game area, puts each player in a
/// body there, and each one opens the panel from the body's Mafia Panel action and sees their role.
/// </summary>
public class MafiaGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static TguiWindows Windows(RealHeadlessWorld.Player p) => new TguiWindows
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    // the player's mob, a ghost too (w.MobOf gives only the living)
    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static UiWindowState Window(RealHeadlessWorld.Player p, string iface) => p.Session.Replica.UiWindows.Tgui().LastOrDefault(x => x.Interface == iface);

    [MetaServerFact]
    public void Six_ghosts_sign_up_from_the_minigames_menu_and_start_a_game_on_centcom()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 4320, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        var players = new List<RealHeadlessWorld.Player>();
        for (int i = 0; i < 6; i++)
            players.Add(w.Join("Player" + i));
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        foreach (RealHeadlessWorld.Player p in players)
        {
            var body = (mob_living_carbon_human)w.MobOf(p);
            In(w, () => body.ghostize(TRUE));
        }
        w.Run(Second);

        foreach (RealHeadlessWorld.Player p in players)
        {
            Assert.IsType<mob_dead_observer>(MobOf(w, p));
            // the HUD's Minigames button (screen_objects/ghost.dm:65)
            long button = p.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is atom_movable_screen_ghost_minigames_menu)).Id;
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            UiWindowState menu = Window(p, "MinigamesMenu");
            Assert.NotNull(menu);
            TguiWindows windows = Windows(p);
            ByKey(Shown(windows, p.Session.Replica, menu.Window), "play:mafia").OnClick();
            w.Run(Second);
            UiWindowState panel = Window(p, "MafiaPanel");
            Assert.NotNull(panel);
            ByKey(Shown(windows, p.Session.Replica, panel.Window), "signup").OnClick();
            w.Run(Second / 2);
        }
        Assert.Equal(6, In(w, () => length(GLOB.mafia_signup)));
        UiNode lobby = Shown(Windows(players[0]), players[0].Session.Replica, Window(players[0], "MafiaPanel").Window);
        Assert.Equal("The lobby currently has 6/12 valid players signed up.", ByKey(lobby, "count").Text);

        // half the lobby votes to start early (MAFIA_MIN_PLAYER_COUNT is six)
        foreach (RealHeadlessWorld.Player p in players.Take(3))
        {
            ByKey(Shown(Windows(p), p.Session.Replica, Window(p, "MafiaPanel").Window), "vote-start").OnClick();
            w.Run(Second / 2);
        }
        w.Run(5 * Second);

        var game = In(w, () => GLOB.mafia_game as datum_mafia_controller);
        Assert.NotNull(game);
        Assert.Equal(MAFIA_PHASE_DAY, In(w, () => game.phase));
        float centcom = In(w, () => get_turf(Dm.locate_type(typeof(obj_effect_landmark_mafia_game_area), GLOB.landmarks_list) as atom).z);
        foreach (RealHeadlessWorld.Player p in players)
        {
            var body = Assert.IsType<mob_living_carbon_human>(MobOf(w, p));
            Assert.Equal(centcom, In(w, () => body.z)); // in a house of the Mafia map on CentCom
            Assert.True(In(w, () => HAS_TRAIT(body, TRAIT_GODMODE)));
        }
        w.Run(2 * Second);
        foreach (RealHeadlessWorld.Player p in players)
        {
            // the ghost's windows closed when it left for the body (mob/Logout's SStgui.on_logout): the body's Mafia Panel action opens it
            Assert.Null(Window(p, "MafiaPanel"));
            var body = (mob_living_carbon_human)MobOf(w, p);
            var button = In(w, () => Dm.each_cast<datum_action>(body.actions).OfType<datum_action_innate_mafia_panel>().Single().viewers[body.hud_used] as atom_movable_screen_movable_action_button);
            Assert.NotNull(button);
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, In(w, () => button.dm_id), 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            UiWindowState panel = Window(p, "MafiaPanel");
            Assert.NotNull(panel);
            UiNode tree = Shown(Windows(p), p.Session.Replica, panel.Window);
            Assert.StartsWith("You are the ", ByKey(tree, "you-are").Text);
            Assert.Equal(6, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("player:", StringComparison.Ordinal)));
        }

        output.WriteLine(string.Join("\n", errors.Select(e => e.Message)));
        Assert.DoesNotContain(errors, e => e.StackTrace != null && e.StackTrace.Contains("mafia", StringComparison.OrdinalIgnoreCase));
    }
}
