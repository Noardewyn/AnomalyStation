using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
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
/// M10 slice G2 (D274) through the real server on MetaStation with the lobby and OPEN_JOBS: Alice late-joins as the captain: tg's
/// "Captain ... on deck!" announcement, the captain's outfit and gold ID, and the spare ID safe's code on a slip in a biscuit card she carries. On the
/// bridge she opens the captain's spare ID safe with tg's LockedSafe keypad and the code, and logs in at the communications console as
/// the captain (the alert level and the announcement are hers). In a second round Bob late-joins as the Head of Personnel with no
/// captain aboard and is promoted to Acting Captain.
/// </summary>
public class HeadsGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int GameStatePregame = 1, GameStatePlaying = 3;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static TguiWindow Window(TguiWindows windows, ReplicaWorld replica, string @interface)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static RealHeadlessWorld Round(ulong seed)
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true, NoInterest = true,
        };
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 20000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        return w;
    }

    // Joins after the round start and picks the job in tg's late join menu.
    private static mob_living_carbon_human LateJoin(RealHeadlessWorld w, RealHeadlessWorld.Player p, string title)
    {
        TguiWindows windows = WindowsOf(p);
        Window(windows, p.Session.Replica, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow menu = Window(windows, p.Session.Replica, "JobSelection");
        Assert.NotNull(menu);
        menu.Context.Act("select_job", ("job", title));
        w.Run(2 * Second);
        var human = Assert.IsType<mob_living_carbon_human>(MobOf(w, p));
        Assert.Equal(title, In(w, () => Dm.str(human.mind.assigned_role.title)));
        return human;
    }

    [MetaServerFact]
    public void The_captain_opens_the_spare_ID_safe_with_the_code_and_logs_in_at_the_console_and_a_lone_head_becomes_acting_captain()
    {
        using (RealHeadlessWorld w = Round(61))
        {
            int bootRuntimes = w.World.RuntimeErrorCount;
            RealHeadlessWorld.Player alice = w.Join("Alice");
            var texts = new List<string>();
            alice.Client.ServerTextReceived += t => texts.Add(t.Html);
            alice.Session.DevCommand("startnow");
            w.Run(5 * Second);
            Assert.Equal(GameStatePlaying, In(w, () => SSticker.current_state));

            var captain = LateJoin(w, alice, JOB_CAPTAIN);
            string name = In(w, () => Dm.str(captain.real_name));
            string code = In(w, () => Dm.str(SSid_access.spare_id_safe_code));
            output.WriteLine($"{name} is the captain; the code {code}; texts: " + string.Join(" | ", texts.TakeLast(6)));
            Assert.Contains(texts, t => t.Contains($"Captain {name} on deck!", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("spare ID", StringComparison.Ordinal));
            // the code is on the slip in the confidential biscuit card she carries (tg's promote_to_captain; gaps S3 lifted DV191)
            obj_item_folder_biscuit_confidential_spare_id_safe_code card = In(w, () => Dm.each_cast<atom>(captain.get_all_contents()).OfType<obj_item_folder_biscuit_confidential_spare_id_safe_code>().Single());
            Assert.Contains(code, In(w, () => Dm.str((card.contained_slip as obj_item_paper).get_raw_text())), StringComparison.Ordinal);
            Assert.True(In(w, () => Dm.truthy(SSjob.assigned_captain)));
            Assert.IsType<obj_item_clothing_under_rank_captain>(In(w, () => captain.w_uniform));
            Assert.IsType<obj_item_card_id_advanced_gold>(In(w, () => captain.wear_id));

            // the spare ID safe on the bridge: tg's keypad, the code, unlocked, the spare ID inside
            obj_structure_secure_safe_caps_spare safe = In(w, () => Dm.each<obj_structure_secure_safe_caps_spare>(Dm.world).Single());
            Assert.Equal(STORAGE_FULLY_LOCKED, In(w, () => Dm.num(safe.atom_storage.locked)));
            Assert.Contains(In(w, () => Dm.each_cast<atom>(safe.contents).ToList()), a => a is obj_item_card_id_advanced_gold_captains_spare);
            using (w.World.Enter())
            {
                turf beside = Dm.each<turf>(orange(1, safe)).First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));
                Waiting.NoSleep(captain.forceMove(beside));
            }
            w.Run(Second);
            TguiWindows windows = WindowsOf(alice);
            Assert.True(alice.Session.Click(CryoGameplayTests.On(safe), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
            TguiWindow keypad = Window(windows, alice.Session.Replica, "LockedSafe");
            Assert.NotNull(keypad);
            Assert.Contains("Locked", keypad.Content.AllText());
            foreach (char digit in code + "E")
            {
                keypad.Context.Act("keypad", ("digit", digit.ToString()));
                w.Run(2);
            }
            w.Run(Second / 2);
            Assert.Equal(STORAGE_NOT_LOCKED, In(w, () => Dm.num(safe.atom_storage.locked)));
            keypad = Window(windows, alice.Session.Replica, "LockedSafe");
            Assert.Contains("Unlocked", keypad.Content.AllText());

            // the communications console: the captain's login offers the alert level and the announcement
            obj_machinery_computer_communications console = In(w, () => Dm.each<obj_machinery_computer_communications>(Dm.world)
                .First(c => get_area(c) is area_station_command_bridge));
            using (w.World.Enter())
                Waiting.NoSleep(captain.forceMove(get_step(console, console.dir)));
            w.Run(Second);
            Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
            TguiWindow comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
            Assert.NotNull(comms);
            comms.Context.Act("toggleAuthentication");
            w.Run(Second / 2);
            comms = Window(windows, alice.Session.Replica, "CommunicationsConsole");
            Assert.True(comms.Context.Data.Truthy("authenticated"));
            Assert.Equal("SWIPE_NEEDED", comms.Context.Data.Str("canSetAlertLevel"));
            Assert.True(comms.Context.Data.Truthy("canMakeAnnouncement"));
            Assert.Contains("Alert Level", comms.Content.AllText());
            output.WriteLine(w.Err.ToString());
            Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        }

        using (RealHeadlessWorld w = Round(62))
        {
            int bootRuntimes = w.World.RuntimeErrorCount;
            RealHeadlessWorld.Player bob = w.Join("Bob");
            var texts = new List<string>();
            bob.Client.ServerTextReceived += t => texts.Add(t.Html);
            bob.Session.DevCommand("startnow");
            w.Run(5 * Second);
            var hop = LateJoin(w, bob, JOB_HEAD_OF_PERSONNEL);
            string name = In(w, () => Dm.str(hop.real_name));
            output.WriteLine("texts: " + string.Join(" | ", texts.TakeLast(6)));
            Assert.Contains(texts, t => t.Contains($"Acting Captain {name} on deck!", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("promoted to Acting Captain", StringComparison.Ordinal));
            Assert.True(In(w, () => Dm.truthy(SSjob.assigned_captain)));
            Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        }
    }
}
