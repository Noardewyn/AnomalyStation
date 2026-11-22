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
/// Gaps J2 (D298) through the real server on MetaStation with the lobby and OPEN_JOBS. A round-start officer with a department preference
/// spawns at that department's checkpoint, in tg's outfit with the department's headset, armband and trim, drawn on the other player's
/// client. A late-joining officer is told his department and announced on the security channel. A clown late-joins clumsy and in tg's
/// outfit; his horn honks and the others hear it; a mime late-joins vowed.
/// </summary>
public class JobKitGameplayTests(ITestOutputHelper output)
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
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        return w;
    }

    private static mob_living_carbon_human LateJoin(RealHeadlessWorld w, RealHeadlessWorld.Player p, string title)
    {
        TguiWindows windows = WindowsOf(p);
        Window(windows, p.Session.Replica, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow menu = Window(windows, p.Session.Replica, "JobSelection");
        Assert.NotNull(menu);
        Assert.Null(menu.Context.Data.Get("departments")?.Get("Service")?.Get("jobs")?.Get("Bartender")?.Get("unavailable_reason"));
        menu.Context.Act("select_job", ("job", title));
        w.Run(2 * Second);
        var human = Assert.IsType<mob_living_carbon_human>(MobOf(w, p));
        Assert.Equal(title, In(w, () => Dm.str(human.mind.assigned_role.title)));
        return human;
    }

    [MetaServerFact]
    public void A_round_start_officer_stands_at_his_checkpoint_in_his_departments_kit()
    {
        using RealHeadlessWorld w = Round(71);
        int bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player alice = w.Join("Alice"), bob = w.Join("Bob");
        var hud = bob.Session.Replica;

        // Bob's character: the Security Officer on High with the medical department, then readied
        w.Run(Second / 2);
        In(w, () =>
        {
            client c = (client)GLOB.directory[Dm.ckey("Bob")];
            c.prefs.job_preferences[JOB_SECURITY_OFFICER] = JP_HIGH;
            c.prefs.write_preference(GLOB.preference_entries[typeof(datum_preference_choiced_security_department)] as datum_preference, SEC_DEPT_MEDICAL);
            return true;
        });
        TguiWindows windows = WindowsOf(bob);
        Window(windows, hud, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        Window(WindowsOf(alice), alice.Session.Replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        for (int i = 0; i < 20 * Second && In(w, () => SSticker.current_state) != GameStatePlaying; i++)
            w.Run(1);
        w.Run(3 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => SSticker.current_state));

        var officer = Assert.IsType<mob_living_carbon_human>(MobOf(w, bob));
        Assert.Equal(JOB_SECURITY_OFFICER, In(w, () => Dm.str(officer.mind.assigned_role.title)));
        Assert.IsType<area_station_security_checkpoint_medical>(In(w, () => get_area(officer)));
        Assert.IsType<obj_item_clothing_under_rank_security_officer>(In(w, () => officer.w_uniform));
        Assert.IsType<obj_item_clothing_suit_armor_vest_alt_sec>(In(w, () => officer.wear_suit));
        Assert.IsType<obj_item_radio_headset_headset_sec_alt_department_med>(In(w, () => officer.ears));
        Assert.IsType<datum_id_trim_job_security_officer_medical>(In(w, () => ((obj_item_card_id)officer.wear_id).trim));
        Assert.Equal(SEC_DEPT_MEDICAL, In(w, () => GLOB.security_officer_distribution[REF(officer)]));
        Assert.NotNull(In(w, () => Dm.locate<obj_item_clothing_accessory_armband_medblue>(((obj_item_clothing_under)officer.w_uniform).attached_accessories)));

        // the other player's client draws him in the uniform
        w.Run(Second);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == officer.dm_id);
        List<(string Icon, string State, float Layer)> worn = CrewOutfitTests.Icons(alice.Session.Replica, seen.State.Appearance);
        output.WriteLine(string.Join(", ", worn.Select(i => i.State)));
        Assert.Contains(worn, i => i.Icon?.Contains("clothing/under/security", StringComparison.Ordinal) == true);
        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void Late_joining_officer_clown_and_mime_take_their_jobs_and_the_horn_is_heard()
    {
        using RealHeadlessWorld w = Round(72);
        int bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => SSticker.current_state));

        // the officer: told his department, announced on the security channel he hears, drawn in his kit on his own HUD
        var officer = LateJoin(w, alice, JOB_SECURITY_OFFICER);
        object department = In(w, () => GLOB.security_officer_distribution[REF(officer)]);
        output.WriteLine($"{department}: " + string.Join(" | ", aliceTexts.TakeLast(5)));
        Assert.NotNull(department);
        Assert.Contains(aliceTexts, t => t.Contains($"You have been assigned to {department}!", StringComparison.Ordinal));
        Assert.Contains(aliceTexts, t => t.Contains(Dm.str(department), StringComparison.Ordinal) && t.Contains(In(w, () => Dm.str(officer.real_name)), StringComparison.Ordinal) && !t.Contains("You have been", StringComparison.Ordinal));
        Assert.IsType<obj_item_clothing_under_rank_security_officer>(In(w, () => officer.w_uniform));

        // the clown, beside the officer on the arrivals shuttle
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var clown = LateJoin(w, bob, JOB_CLOWN);
        Assert.True(In(w, () => HAS_TRAIT(clown, TRAIT_CLUMSY)));
        Assert.IsType<obj_item_clothing_shoes_clown_shoes>(In(w, () => clown.shoes));
        Assert.True(In(w, () => HAS_TRAIT(clown, TRAIT_WADDLING)));
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        In(w, () => Waiting.NoSleep(clown.forceMove(get_turf(officer))));
        var bikehorn = In(w, () => clown.l_store as obj_item_bikehorn);
        Assert.NotNull(bikehorn);
        In(w, () => Waiting.NoSleep(clown.put_in_active_hand(bikehorn)));
        w.Run(Second / 2);
        bob.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        output.WriteLine(string.Join(", ", heard.Select(s => s.File)));
        Assert.Contains(heard, s => s.File?.Contains("bikehorn", StringComparison.Ordinal) == true);

        // the mime: vowed, silent
        RealHeadlessWorld.Player carl = w.Join("Carl");
        var mime = LateJoin(w, carl, JOB_MIME);
        Assert.True(In(w, () => HAS_MIND_TRAIT(mime, TRAIT_MIMING)));
        Assert.NotNull(In(w, () => Dm.locate<datum_action_cooldown_spell_vow_of_silence>(mime.actions)));
        var carlTexts = new List<string>();
        carl.Client.ServerTextReceived += t => carlTexts.Add(t.Html);
        carl.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "hello"));
        w.Run(Second / 2);
        Assert.Contains(carlTexts, t => t.Contains("Your vow of silence prevents you from speaking!", StringComparison.Ordinal));

        // a book opens tg's MarkdownViewer on the client
        In(w, () =>
        {
            obj_item_book book = Dm.@new<obj_item_book>(get_turf(mime));
            book.book_data.set_content("A page.");
            return Waiting.NoSleep(mime.put_in_active_hand(book));
        });
        w.Run(Second / 2);
        carl.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        TguiWindow bookWindow = Window(WindowsOf(carl), carl.Session.Replica, "MarkdownViewer");
        Assert.NotNull(bookWindow);
        Assert.Contains(bookWindow.Content.Walk(), n => n.Text == "A page.");

        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
