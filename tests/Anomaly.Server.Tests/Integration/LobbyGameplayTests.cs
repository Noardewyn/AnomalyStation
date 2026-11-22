using System.IO;
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
/// M10 slice F (D267, D270): tg's lobby through the real server on MetaStation with the lobby on. Alice joins into tg's lobby: her mob
/// is tg's new player, her client has the lobby's window (tgui-lobby's state: the pregame, Ready, the countdown). She opens the
/// character setup (tg's PreferencesMenu), names her character, makes her female and 40, and readies up; when the countdown ends tg's
/// ticker sets the round up: she is the station engineer, the only open job (the overflow), at an engineer's start landmark, with
/// her name, gender and age, in tg's engineer outfit; the lobby closes. Bob joins after the start: the lobby shows Join, tg's late
/// join menu (JobSelection) lists the station engineer open, the other jobs closed but the assistant (tg's newbie rule without a
/// database), and he late-joins as an engineer.
/// </summary>
public class LobbyGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public LobbyGameplayTests(ITestOutputHelper output) => _output = output;

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

    // The host's client holds tg's !localhost! rank (ENABLE_LOCALHOST_RANK), so the lobby's Start Now sign (R_SERVER) starts the round at once.
    [MetaServerFact]
    public void The_host_starts_the_round_at_once_with_the_lobbys_Start_Now_sign()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 44, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        TguiWindows windows = WindowsOf(alice);
        TguiWindow lobby = Window(windows, alice.Session.Replica, Limits.LobbyUiInterface);
        Assert.True(lobby.Context.Data.Truthy("isLocalhost"));
        Assert.True(In(w, () => Dm.num(GlobalProcs.SSticker.timeLeft)) > 60 * 10, "the countdown is still running");
        lobby.Context.Act("start_now");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
    }

    [MetaServerFact]
    public void Alice_sets_up_her_character_readies_and_starts_as_the_engineer_and_Bob_late_joins_from_the_job_menu()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 8000 }, config, strict: false);
        int bootRuntimes = w.World.RuntimeErrorCount;
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1); // joins open at the Master's init; tg's ticker enters the pregame at its first fire
        Assert.Equal(GameStatePregame, In(w, () => GlobalProcs.SSticker.current_state));

        RealHeadlessWorld.Player alice = w.Join("Alice");
        ReplicaWorld replica = alice.Session.Replica;
        TguiWindows windows = WindowsOf(alice);
        Assert.IsType<mob_dead_new_player>(MobOf(w, alice));

        // the lobby: tg's state (pregame, Ready, not ready), drawn by the client as the lobby screen
        TguiWindow lobby = Window(windows, replica, Limits.LobbyUiInterface);
        Assert.NotNull(lobby);
        Assert.Equal(Limits.LobbyUiWindow, lobby.Index);
        Assert.Equal("pregame", lobby.Context.Data.Str("gamePhase"));
        Assert.True(lobby.Context.Data.Truthy("canReady"));
        Assert.False(lobby.Context.Data.Truthy("isReady"));
        Assert.True(lobby.Context.Data.Truthy("canObserve")); // tg: past the startup (M10x slice B)
        Assert.Equal("MetaStation", lobby.Context.Data.Str("mapName"));
        _output.WriteLine($"lobby: countdown {lobby.Context.Data.Str("countdown")}, players {lobby.Context.Data.Num("playerCount")}");

        // the character setup: tg's PreferencesMenu, the character's name, gender and age (D267)
        lobby.Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, replica, "PreferencesMenu");
        Assert.NotNull(prefs);
        string randomName = prefs.Context.Data.Get("character_preferences")?.Get("names")?.Str("real_name");
        _output.WriteLine($"random character: {randomName}, {prefs.Context.Data.Get("character_preferences")?.Get("misc")?.Str("gender")}");
        Assert.False(string.IsNullOrEmpty(randomName));
        prefs.Context.Act("set_preference", ("preference", "real_name"), ("value", "ada lovelace"));
        prefs.Context.Act("set_preference", ("preference", "gender"), ("value", "female"));
        prefs.Context.Act("set_preference", ("preference", "age"), ("value", 40));
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Equal("Ada Lovelace", prefs.Context.Data.Get("character_preferences")?.Get("names")?.Str("real_name")); // tg capitalises it
        Assert.Equal("Ada Lovelace", prefs.Content.Walk().Single(n => n.Kind == UiKind.Input && n.Key == "name:real_name").TextValue);
        Assert.True(prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Icon == "venus").Selected);
        Assert.Equal(40, prefs.Content.Walk().Single(n => n.Kind == UiKind.NumberInput && n.Key == "age").Value);

        // M10x slices D3 and D4: her looks (tg's species_features/basic.dm and clothing.dm) on the page: the hairstyle's button opens
        // its grid of pictures, where she picks the braid; the eyes' colour button opens tg's colour picker
        UiNode hairButton = prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Hairstyle");
        Assert.NotNull(hairButton.Children.Single().DmIcon); // the current hairstyle's picture
        hairButton.OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        UiNode braid = prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Braid (Floorlength)");
        Assert.StartsWith("iconops:", braid.Children.Single().DmIcon);
        braid.OnClick();
        prefs.Context.Act("set_preference", ("preference", "hair_color"), ("value", "#aa5500"));
        prefs.Context.Act("set_preference", ("preference", "underwear"), ("value", "Briefs"));
        prefs.Context.Act("set_preference", ("preference", "backpack"), ("value", "Grey Satchel"));
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.True(prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Braid (Floorlength)").Selected);
        Assert.Contains(prefs.Content.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Eye color");
        prefs.Content.Walk().Single(n => n.Key == "color:eye_color").OnClick();
        w.Run(Second / 2);
        TguiWindow picker = Window(windows, replica, "ColorPickerModal");
        Assert.NotNull(picker);
        Assert.Equal("Select new color", picker.Context.Data.Str("message"));
        // M10x slice D5: Godot's picker in tg's window (DV209), the colour picked and tg's Submit pressed
        UiNode colorPicker = picker.Content.Walk().Single(n => n.Kind == UiKind.ColorPicker);
        Assert.StartsWith("#", colorPicker.Color); // the eyes' colour so far
        colorPicker.OnInput("#3366FF");
        w.Run(Second / 2);
        // DV230: the colour picked shows before Submit (the preference is written and the preview redrawn)
        Assert.Equal("#3366ff", Window(windows, replica, "PreferencesMenu").Context.Data.Get("character_preferences").Fields.Select(f => f.Value.Get("eye_color")?.Text).Single(t => t != null));
        picker = Window(windows, replica, "ColorPickerModal");
        picker.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Key == "submit").OnClick();
        w.Run(Second / 2);
        Assert.Null(Window(windows, replica, "ColorPickerModal"));

        // Ready
        lobby = Window(windows, replica, Limits.LobbyUiInterface);
        lobby.Context.Act("ready_toggle");
        w.Run(Second / 2);
        lobby = Window(windows, replica, Limits.LobbyUiInterface);
        Assert.True(lobby.Context.Data.Truthy("isReady"));

        // tg deletes the start landmarks after the round start (landmarks.dm after_round_start): their turfs, kept before it
        List<object> engineerStarts = In(w, () => Dm.each<obj_effect_landmark_start>(GlobalProcs.GLOB.start_landmarks_list)
            .Where(l => Dm.eq(l.name, "Station Engineer")).Select(l => (object)l.loc).ToList());
        Assert.NotEmpty(engineerStarts);

        // Alice's dev console: startnow, tg's admin Start Now, instead of the rest of the 120 s countdown; tg's ticker sets the round up
        Assert.True(In(w, () => Dm.num(GlobalProcs.SSticker.timeLeft)) > 60 * 10, "the countdown is still running");
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
        var ada = Assert.IsType<mob_living_carbon_human>(MobOf(w, alice));
        Assert.Equal("Ada Lovelace", In(w, () => ada.real_name));
        Assert.Equal("female", In(w, () => ada.gender));
        Assert.Equal(40, In(w, () => ada.age));
        Assert.Equal("Station Engineer", In(w, () => ada.mind.assigned_role.title));
        Assert.Equal(typeof(obj_item_clothing_under_rank_engineering_engineer), In(w, () => ada.w_uniform?.GetType()));
        var head = In(w, () => (obj_item_bodypart_head)ada.get_bodypart(Defines.BODY_ZONE_HEAD));
        Assert.Equal("Braid (Floorlength)", In(w, () => head.hairstyle));
        Assert.Equal("#aa5500", In(w, () => Dm.str(head.hair_color)));
        Assert.Equal("Briefs", In(w, () => ada.underwear));
        Assert.Equal("#3366ff", In(w, () => ada.eye_color_left));
        Assert.Equal("#3366ff", In(w, () => Dm.str(((obj_item_organ_eyes)ada.get_organ_by_type(typeof(obj_item_organ_eyes))).eye_color_left)));
        Assert.Equal(typeof(obj_item_storage_backpack_satchel), In(w, () => ada.back?.GetType())); // tg's grey satchel over the engineer's
        Assert.Contains(In(w, () => (object)ada.loc), engineerStarts); // a station engineer's start landmark
        Assert.Null(Window(windows, replica, Limits.LobbyUiInterface)); // the lobby closes (tg's skin shows the map)
        Assert.Contains(In(w, () => Dm.each(GlobalProcs.GLOB.manifest.general).Select(r => Dm.str(((datum_record)r).name)).ToList()), n => n == "Ada Lovelace");

        // Bob joins after the start: Join, the late join menu, the engineer open and the assistant closed
        RealHeadlessWorld.Player bob = w.Join("Bob");
        TguiWindows bobWindows = WindowsOf(bob);
        TguiWindow bobLobby = Window(bobWindows, bob.Session.Replica, Limits.LobbyUiInterface);
        Assert.Equal("playing", bobLobby.Context.Data.Str("gamePhase"));
        Assert.True(bobLobby.Context.Data.Truthy("canJoin"));
        bobLobby.Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow menu = Window(bobWindows, bob.Session.Replica, "JobSelection");
        Assert.NotNull(menu);
        _output.WriteLine("departments: " + string.Join("; ", menu.Context.Data.Get("departments").Fields.Select(d => d.Key + ": "
            + string.Join(", ", d.Value.Get("jobs").Fields.Select(j => j.Key + (j.Value.Get("unavailable_reason") != null ? " (closed)" : ""))))));
        UiValue engineering = menu.Context.Data.Get("departments")?.Get("Engineering")?.Get("jobs")?.Get("Station Engineer");
        Assert.NotNull(engineering);
        Assert.Null(engineering.Get("unavailable_reason"));
        UiValue atmos = menu.Context.Data.Get("departments")?.Get("Engineering")?.Get("jobs")?.Get("Atmospheric Technician");
        Assert.Equal("Atmospheric Technician is already filled to capacity.", atmos?.Get("unavailable_reason")?.Text); // closed (jobs.txt)
        // tg's newbie rule (new_player.dm:137): without a database the account's age is unknown (-1), so the assistant stays open
        UiValue assistant = menu.Context.Data.Get("departments")?.Get("No Department")?.Get("jobs")?.Get("Assistant");
        Assert.NotNull(assistant);
        Assert.Null(assistant.Get("unavailable_reason"));
        Assert.Contains("Station Engineer", menu.Content.AllText());
        menu.Context.Act("select_job", ("job", "Station Engineer"));
        w.Run(2 * Second);
        var bobsMob = Assert.IsType<mob_living_carbon_human>(MobOf(w, bob));
        Assert.Equal("Station Engineer", In(w, () => bobsMob.mind.assigned_role.title));
        Assert.Equal(typeof(obj_item_clothing_under_rank_engineering_engineer), In(w, () => bobsMob.w_uniform?.GetType()));
        // tg's late join: a chair of the docked arrivals shuttle (M10 slice G3; DV180's own job start is the fallback without it)
        Assert.IsType<area_shuttle_arrival>(In(w, () => GlobalProcs.get_area(bobsMob)));
        Assert.IsAssignableFrom<obj_structure_chair>(In(w, () => bobsMob.buckled));
        _output.WriteLine($"Bob late-joined as {In(w, () => bobsMob.real_name)} at {In(w, () => bobsMob.x)},{In(w, () => bobsMob.y)}");

        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // D271, D274: with OPEN_JOBS the late join menu opens tg's jobs with tg's slots, the heads of staff and the cyborg (DV426) too; Bob joins
    // after the round start (Alice's startnow) and late-joins as a medical doctor in tg's doctor outfit; Carol late-joins as a cyborg.
    [MetaServerFact]
    public void With_open_jobs_Bob_late_joins_as_a_medical_doctor_and_Carol_as_a_cyborg()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 8000 }, config, strict: false);
        int bootRuntimes = w.World.RuntimeErrorCount;
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));

        RealHeadlessWorld.Player bob = w.Join("Bob");
        TguiWindows bobWindows = WindowsOf(bob);
        Window(bobWindows, bob.Session.Replica, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow menu = Window(bobWindows, bob.Session.Replica, "JobSelection");
        Assert.NotNull(menu);
        UiValue Job(string department, string title) => menu.Context.Data.Get("departments")?.Get(department)?.Get("jobs")?.Get(title);
        Assert.Null(Job("Medical", "Medical Doctor")?.Get("unavailable_reason"));
        Assert.NotNull(Job("Medical", "Medical Doctor"));
        Assert.Null(Job("Security", "Security Officer")?.Get("unavailable_reason"));
        Assert.NotNull(Job("Command", "Captain"));
        Assert.Null(Job("Command", "Captain").Get("unavailable_reason")); // D274: the heads open
        Assert.Null(Job("Silicon", "Cyborg").Get("unavailable_reason")); // DV426: the cyborg takes late joins

        menu.Context.Act("select_job", ("job", "Medical Doctor"));
        w.Run(2 * Second);
        var doctor = Assert.IsType<mob_living_carbon_human>(MobOf(w, bob));
        Assert.Equal("Medical Doctor", In(w, () => doctor.mind.assigned_role.title));
        Assert.Equal(typeof(obj_item_clothing_under_rank_medical_doctor), In(w, () => doctor.w_uniform?.GetType()));
        Assert.IsType<area_shuttle_arrival>(In(w, () => GlobalProcs.get_area(doctor))); // tg's late join: the docked arrivals shuttle (M10 slice G3)

        // DV426: Carol late-joins as a cyborg: tg's cyborg job spawns its robot on the arrivals shuttle
        RealHeadlessWorld.Player carol = w.Join("Carol");
        TguiWindows carolWindows = WindowsOf(carol);
        Window(carolWindows, carol.Session.Replica, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        Window(carolWindows, carol.Session.Replica, "JobSelection").Context.Act("select_job", ("job", "Cyborg"));
        w.Run(2 * Second);
        var borg = Assert.IsType<mob_living_silicon_robot>(MobOf(w, carol));
        Assert.Equal("Cyborg", In(w, () => borg.mind.assigned_role.title));
        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // D278: with open jobs, Alice sets the medical doctor to High on the character setup's Occupations page (tg's JobsPage) and readies;
    // Bob readies without job preferences. At the round start tg's DivideOccupations makes Alice a doctor and Bob tg's overflow role,
    // the assistant.
    [MetaServerFact]
    public void With_open_jobs_the_round_start_gives_jobs_by_the_occupations_page()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 48, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        int bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player alice = w.Join("Alice"), bob = w.Join("Bob");
        TguiWindows windows = WindowsOf(alice);
        ReplicaWorld replica = alice.Session.Replica;

        // the character setup's Occupations page: the departments' rows, the doctor's High button
        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, replica, "PreferencesMenu");

        // M10x slice E (D284): tg's character preview, a map view of a dummy in the highest job's outfit (none set yet: tg's unassigned
        // job, no outfit), reaches her screen with the map's id in its screen_loc; the page draws it as a 2D picture, turned by its arrows and
        // by Rotate as tg's setDir turns it
        string mapRef = prefs.Context.Data.Str("character_preview_view", null);
        Assert.StartsWith("character_preview_", mapRef);
        Assert.Contains(prefs.Content.Walk(), n => n.Kind == UiKind.ByondUi && n.MapPicture && n.MapRef == mapRef);
        ScreenEntry Preview() => replica.Screen.Values.Single(e => e.ScreenLoc?.StartsWith(mapRef + ":", StringComparison.Ordinal) == true);
        byte PreviewDir() => replica.TryGetAppearance(Preview().Appearance, out AppearanceEntry look) ? look.Dir : (byte)0;
        string Worn() => string.Join(" ; ", CrewOutfitTests.Icons(replica, Preview().Appearance).Select(i => $"{i.Icon}:{i.State}"));
        Assert.Equal(2, PreviewDir());
        Assert.True(Worn().Contains("bodyparts_greyscale.dmi:human_chest_", StringComparison.Ordinal), Worn());
        UiNode PreviewArrow(string icon) => prefs.Content.Walk().Single(n => n.Key == "arrows").Walk().Single(n => n.Kind == UiKind.Button && n.Icon == icon); // the voice pitch has arrows too
        PreviewArrow("chevron-right").OnClick();
        w.Run(Second / 2);
        Assert.Equal(4, PreviewDir()); // turn(SOUTH, 90)
        prefs = Window(windows, replica, "PreferencesMenu");
        PreviewArrow("chevron-left").OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Rotate").OnClick();
        w.Run(Second / 2);
        Assert.Equal(8, PreviewDir()); // turn(SOUTH, -90)
        // the client's compositor draws the picture facing west (TguiMapPicture composes it so)
        if (Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot() is string iconRoot)
        {
            var compositor = new Anomaly.Client.Shared.Icons.AppearanceCompositor(new Anomaly.Client.Shared.Icons.IconLibrary(new Anomaly.Client.Shared.Icons.DirectoryIconFiles(iconRoot)));
            Assert.True(compositor.TryCompose(new HairGameplayTests.Tables(replica), Preview().Appearance, 8, 0, out Anomaly.Client.Shared.Icons.ComposedRaster picture));
            Assert.Contains(Enumerable.Range(0, picture.Image.Pixels.Length / 4), i => picture.Image.Pixels[i * 4 + 3] != 0);
        }

        // the randomization (middleware/random.dm): random_body's Randomize shows each feature's dice; the hairstyle's (on by default)
        // set to Do not randomize turns red, and Randomize again rerolls the character (randomize_character) into the preview
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.DoesNotContain(prefs.Content.Walk(), n => n.Key == "random:hairstyle_name");
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Randomize").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        UiNode dice = prefs.Content.Walk().Single(n => n.Key == "random:hairstyle_name");
        Assert.Equal(("dice-d20", "green"), (dice.Icon, dice.Color)); // randomize_by_default: get_default_randomization's RANDOM_ENABLED
        dice.OnSelected("Do not randomize");
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Equal("red", prefs.Content.Walk().Single(n => n.Key == "random:hairstyle_name").Color);
        Assert.Equal(0, prefs.Context.Data.Get("character_preferences").Get("randomization").Num("hairstyle_name", 0)); // removed from randomise
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Randomize" && n.Color == "green").OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.DoesNotContain(prefs.Content.Walk(), n => n.Key == "random:hairstyle_name"); // the toggle is off again
        Assert.True(Worn().Contains("bodyparts_greyscale.dmi:human_chest_", StringComparison.Ordinal), Worn());

        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Occupations").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        UiNode Row(string job) => prefs.Content.Walk().SingleOrDefault(n => n.Key == "job:" + job);
        Assert.NotNull(Row("Station Engineer"));
        Assert.NotNull(Row("Captain"));
        Assert.Equal("Assistant", prefs.Context.Data.Str("overflow_role"));
        Row("Medical Doctor").Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High").OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Equal("green", Row("Medical Doctor").Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High").Color);
        Assert.Equal("white", Row("Medical Doctor").Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Low").Color);
        Assert.True(Worn().Contains("under/medical.dmi", StringComparison.Ordinal), Worn()); // the preview wears the highest priority job's outfit (jobs.dm:25's update_body)
        // the Loadout page's Job Clothes checkbox (DV210, toggle_job_clothes): off, the preview shows the body in its underwear; on, the outfit again
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Character").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Job Clothes" && n.Checkbox == true).OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.False(Worn().Contains("under/medical.dmi", StringComparison.Ordinal), Worn());
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Job Clothes" && n.Checkbox == false).OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.True(Worn().Contains("under/medical.dmi", StringComparison.Ordinal), Worn());

        foreach (RealHeadlessWorld.Player p in new[] { alice, bob })
            Window(WindowsOf(p), p.Session.Replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
        var doctor = Assert.IsType<mob_living_carbon_human>(MobOf(w, alice));
        Assert.Equal("Medical Doctor", In(w, () => doctor.mind.assigned_role.title));
        Assert.Equal(typeof(obj_item_clothing_under_rank_medical_doctor), In(w, () => doctor.w_uniform?.GetType()));
        var assistant = Assert.IsType<mob_living_carbon_human>(MobOf(w, bob));
        Assert.Equal("Assistant", In(w, () => assistant.mind.assigned_role.title));
        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The owner (2026-11-13): the silicons are picked on the Occupations page as tg's jobs (ALLOW_AI, silicons SI4): Alice puts the AI on
    // High and Bob the cyborg, and the round start makes her the station's AI and him a cyborg
    [MetaServerFact]
    public void With_open_jobs_the_occupations_page_picks_the_AI_and_the_cyborg()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 48, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        int bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player alice = w.Join("Alice"), bob = w.Join("Bob");
        foreach ((RealHeadlessWorld.Player p, string job) in new[] { (alice, "AI"), (bob, "Cyborg") })
        {
            TguiWindows windows = WindowsOf(p);
            ReplicaWorld replica = p.Session.Replica;
            Window(windows, replica, Limits.LobbyUiInterface).Context.Act("character_setup");
            w.Run(Second / 2);
            TguiWindow prefs = Window(windows, replica, "PreferencesMenu");
            Assert.NotNull(prefs);
            prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Occupations").OnClick();
            prefs = Window(windows, replica, "PreferencesMenu");
            UiNode row = prefs.Content.Walk().SingleOrDefault(n => n.Key == "job:" + job);
            Assert.NotNull(row); // the Silicon department's row
            row.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High").OnClick();
            w.Run(Second / 2);
            prefs = Window(windows, replica, "PreferencesMenu");
            Assert.Equal("green", prefs.Content.Walk().Single(n => n.Key == "job:" + job).Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High").Color);
            Window(windows, replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        }
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
        var ai = Assert.IsType<mob_living_silicon_ai>(MobOf(w, alice));
        Assert.Equal("AI", In(w, () => ai.mind.assigned_role.title));
        var borg = Assert.IsType<mob_living_silicon_robot>(MobOf(w, bob));
        Assert.Equal("Cyborg", In(w, () => borg.mind.assigned_role.title));
        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The owner (2026-10-28): joining the round opens no readout panel; no text the client gets at the round start is a readout.
    [MetaServerFact]
    public void The_round_start_sends_no_readout()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 48, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        Window(WindowsOf(alice), alice.Session.Replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
        Assert.DoesNotContain(texts, t => Anomaly.Client.Shared.ReadoutModel.IsReadout(t));
    }

    // D279: tg's character slots. Alice makes a second character, Bea, and a third she deletes again; on Ada's Occupations page she sets
    // the medical doctor to High and puts Bea on the doctor (Show Characters). At the round start tg's create_character loads Bea's
    // slot for the doctor's job, so Alice plays Bea.
    [MetaServerFact]
    public void Alice_keeps_characters_in_slots_and_plays_the_one_she_put_on_her_job()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 49, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        int bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player alice = w.Join("Alice");
        TguiWindows windows = WindowsOf(alice);
        ReplicaWorld replica = alice.Session.Replica;
        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, replica, "PreferencesMenu");
        void Refresh() { w.Run(Second / 2); prefs = Window(windows, replica, "PreferencesMenu"); }
        UiNode Keyed(string key) => prefs.Content.Walk().Single(n => n.Key == key);
        UiNode ButtonIn(string key, Func<UiNode, bool> which = null) => Keyed(key).Walk().Single(n => n.Kind == UiKind.Button && (which == null || which(n)));
        string[] Profiles() => prefs.Context.Data.Get("character_profiles").Items.Select(p => p.Kind == UiValueKind.Null ? null : p.Text).ToArray();

        prefs.Context.Act("set_preference", ("preference", "real_name"), ("value", "ada lovelace"));
        Refresh();
        Assert.Equal(new[] { "Ada Lovelace", null, null }, Profiles());
        Assert.True(ButtonIn("delete").Disabled); // one character

        // a second slot: a new random character, renamed Bea
        Assert.Equal("New Character", ButtonIn("slot:1").Text);
        ButtonIn("slot:1").OnClick();
        Refresh();
        Assert.Equal(2, prefs.Context.Data.Num("active_slot"));
        prefs.Context.Act("set_preference", ("preference", "real_name"), ("value", "bea doctor"));
        Refresh();

        // a third one, deleted through the popup: back to the closest slot below it
        ButtonIn("slot:2").OnClick();
        Refresh();
        Assert.Equal(3, prefs.Context.Data.Num("active_slot"));
        Assert.False(ButtonIn("delete").Disabled);
        ButtonIn("delete").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        ButtonIn("deleteCharacter", n => n.Text == "Delete").OnClick();
        Refresh();
        Assert.Equal(new[] { "Ada Lovelace", "Bea Doctor", null }, Profiles());
        Assert.Equal(2, prefs.Context.Data.Num("active_slot"));

        // back to Ada: her Occupations page, the doctor High and Bea put on it
        ButtonIn("slot:0").OnClick();
        Refresh();
        Assert.Equal("Ada Lovelace", prefs.Context.Data.Get("character_preferences").Get("names").Str("real_name"));
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Occupations").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        Keyed("job:Medical Doctor").Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High").OnClick();
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Show Characters").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        UiNode beaButton = ButtonIn("profile:1");
        beaButton.OnDragStart(); // Bea dragged onto the doctor's name
        prefs = Window(windows, replica, "PreferencesMenu");
        UiNode doctorName = Keyed("job:Medical Doctor").Walk().Single(n => n.Kind == UiKind.Box && n.Text == "Medical Doctor");
        doctorName.OnDragEnter();
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.NotNull(Keyed("job:Medical Doctor").Walk().Single(n => n.Key == "jobName").Background); // hovered
        Keyed("job:Medical Doctor").Walk().Single(n => n.Kind == UiKind.Box && n.Text == "Medical Doctor").OnDrop();
        beaButton.OnDragEnd();
        Refresh();
        Assert.Null(Keyed("job:Medical Doctor").Walk().Single(n => n.Key == "jobName").Background);
        Assert.Contains(Keyed("job:Medical Doctor").Walk(), n => n.Kind == UiKind.Box && n.Text == "↳ Bea Doctor");

        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));
        var bea = Assert.IsType<mob_living_carbon_human>(MobOf(w, alice));
        Assert.Equal("Medical Doctor", In(w, () => bea.mind.assigned_role.title));
        Assert.Equal("Bea Doctor", In(w, () => bea.real_name));
        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // A client that leaves during the round and comes back with its kept token (tg's SSD) goes straight back into its body, without
    // the lobby; one that left from the lobby gets a new lobby (tg's new player Logout deletes it) whose Join still late-joins.
    [MetaServerFact]
    public void A_player_who_leaves_during_the_round_comes_back_into_the_body_without_the_lobby()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
        };
        string tokens = Path.Combine(Path.GetTempPath(), "anomaly-lobby-tokens-" + Guid.NewGuid().ToString("N") + ".txt");
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 49, TimeoutSteps = 8000 }, config, strict: false);
        RealHeadlessWorld.Player Launch(string name)
        {
            var join = new JoinAttempt(() => w.Network.CreateClient(), new ReconnectTokens(tokens), "127.0.0.1", w.Server.Port, name);
            Assert.Equal(FeedPhase.Playing, w.Join(join));
            return w.Adopt(name, join.Client);
        }
        try
        {
            int bootRuntimes = w.World.RuntimeErrorCount;
            for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
                w.Run(1);
            RealHeadlessWorld.Player alice = Launch("Alice");
            Window(WindowsOf(alice), alice.Session.Replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
            w.Run(Second / 2);
            alice.Session.DevCommand("startnow");
            w.Run(5 * Second);
            var body = Assert.IsType<mob_living_carbon_human>(MobOf(w, alice));

            w.Leave(alice);
            Assert.Null(In(w, () => body.client));
            RealHeadlessWorld.Player back = Launch("Alice");
            Assert.Same(body, MobOf(w, back));
            Assert.Equal(body.dm_id, back.Client.OwnEntity);
            Assert.NotNull(In(w, () => body.client));
            Assert.Null(Window(WindowsOf(back), back.Session.Replica, Limits.LobbyUiInterface));

            RealHeadlessWorld.Player bob = Launch("Bob");
            Assert.IsType<mob_dead_new_player>(MobOf(w, bob));
            w.Leave(bob);
            RealHeadlessWorld.Player bobBack = Launch("Bob");
            TguiWindows bobWindows = WindowsOf(bobBack);
            TguiWindow lobby = Window(bobWindows, bobBack.Session.Replica, Limits.LobbyUiInterface);
            Assert.NotNull(lobby);
            Assert.True(lobby.Context.Data.Truthy("canJoin"));
            lobby.Context.Act("join", ("ctrlClick", false));
            w.Run(Second / 2);
            TguiWindow menu = Window(bobWindows, bobBack.Session.Replica, "JobSelection");
            Assert.NotNull(menu);
            menu.Context.Act("select_job", ("job", "Station Engineer"));
            w.Run(2 * Second);
            Assert.IsType<mob_living_carbon_human>(MobOf(w, bobBack));
            _output.WriteLine(w.Err.ToString());
            Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        }
        finally
        {
            File.Delete(tokens);
        }
    }

    private const float GameStatePregame = Defines.GAME_STATE_PREGAME;
    private const float GameStatePlaying = Defines.GAME_STATE_PLAYING;
}
