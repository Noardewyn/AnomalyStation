using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice G4 (D282): tg's escape menu on MetaStation through the real server. 3 s after the connection the client has the escape
/// menu's window (its own index, tg's init: the station, the round, both players, Leave Body allowed); opened, it is processed every 2 s
/// (the server time); its Leave Body's Ghost is tg's ghost verb (the Yes/No alert, then the observer), after which Leave Body is off.
/// </summary>
public class EscapeMenuGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiWindowState Menu(RealHeadlessWorld.Player p) => p.Session.Replica.UiWindows.GetValueOrDefault(Limits.EscapeMenuUiWindow);

    [MetaServerFact]
    public void The_escape_menu_shows_the_round_and_the_players_and_its_Ghost_leaves_the_body()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        Assert.Null(Menu(alice)); // tg's initialize_escape_menu sleeps 3 s first
        w.Run(3 * Second);

        UiWindowState menu = Menu(alice);
        Assert.NotNull(menu);
        Assert.Equal(Limits.EscapeMenuUiInterface, menu.Interface);
        Assert.Equal(In(w, () => GlobalProcs.station_name() as string), menu.Data.Str("stationName"));
        Assert.True(menu.Data.Get("canLeaveBody").Truthy);
        Assert.Equal(new[] { "alice", "bob" }, menu.Data.Get("players").Items.Select(p => p.Str("ckey")).Order());
        Assert.Empty(menu.Data.Get("resources").Items); // D286: the Change Log is the lobby's alone
        Assert.NotNull(Menu(bob));

        int serial = menu.Serial;
        Assert.True(alice.Session.UiAct(Limits.EscapeMenuUiWindow, "opened", null));
        w.Run(3 * Second);
        Assert.True(Menu(alice).Serial > serial); // SSescape_menu's process: the times and the lists every 2 s
        Assert.NotEmpty(Menu(alice).Data.Str("serverTime"));

        Assert.True(alice.Session.UiAct(Limits.EscapeMenuUiWindow, "ghost", null));
        w.Run(Second);
        UiWindowState alert = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == "AlertModal");
        Assert.Contains("You won't be able to re-enter your body!", alert.Merged.Str("message"));
        Assert.True(alice.Session.UiAct(alert.Window, "choose", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("choice", UiValue.Of("Ghost")) })));
        w.Run(Second);
        Assert.IsType<mob_dead_observer>(In(w, () => Dm.each<client>(GlobalProcs.GLOB.clients).First(c => c.key as string == "Alice").mob));
        Assert.False(Menu(alice).Data.Get("canLeaveBody").Truthy); // on_client_mob_login

        Assert.True(alice.Session.UiAct(Limits.EscapeMenuUiWindow, "closed", null));
        w.Run(Second);
        serial = Menu(alice).Serial;
        w.Run(3 * Second);
        Assert.Equal(serial, Menu(alice).Serial); // no longer processed
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // D286: in the round the escape menu's Character shows the character to look at only: tg's preview of the character (a screen
    // object on the client's screen, drawn by the page) turns, the page's edits are refused and its inputs are disabled.
    [MetaServerFact]
    public void In_the_round_the_escape_menus_Character_shows_the_character_and_its_preview_read_only()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        ReplicaWorld replica = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        TguiWindow Prefs()
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Open.FirstOrDefault(x => x.Interface == "PreferencesMenu");
        }

        Assert.True(alice.Session.UiAct(Limits.EscapeMenuUiWindow, "character", null));
        w.Run(Second);
        TguiWindow prefs = Prefs();
        Assert.NotNull(prefs);
        string mapRef = prefs.Context.Data.Str("character_preview_view", null);
        Assert.StartsWith("character_preview_", mapRef);
        ScreenEntry Preview() => replica.Screen.Values.Single(e => e.ScreenLoc?.StartsWith(mapRef + ":", StringComparison.Ordinal) == true);
        byte PreviewDir() => replica.TryGetAppearance(Preview().Appearance, out AppearanceEntry look) ? look.Dir : (byte)0;
        Assert.Equal(2, PreviewDir());
        string worn = string.Join(" ; ", CrewOutfitTests.Icons(replica, Preview().Appearance).Select(i => $"{i.Icon}:{i.State}"));
        Assert.True(worn.Contains("bodyparts_greyscale.dmi:human_chest_", StringComparison.Ordinal), worn);
        if (Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot() is string iconRoot)
        {
            var compositor = new Anomaly.Client.Shared.Icons.AppearanceCompositor(new Anomaly.Client.Shared.Icons.IconLibrary(new Anomaly.Client.Shared.Icons.DirectoryIconFiles(iconRoot)));
            Assert.True(compositor.TryCompose(new HairGameplayTests.Tables(replica), Preview().Appearance, 2, 0, out Anomaly.Client.Shared.Icons.ComposedRaster picture, rootOffset: true));
            Assert.Contains(Enumerable.Range(0, picture.Image.Pixels.Length / 4), i => picture.Image.Pixels[i * 4 + 3] != 0);
        }

        // read only: the edits are disabled on the page and refused by the server; the preview still turns at once
        Assert.True(prefs.Context.Data.Get("read_only")?.Truthy);
        Assert.All(prefs.Content.Walk().Where(n => n.Key?.StartsWith("gender:", StringComparison.Ordinal) == true).SelectMany(n => n.Walk()).Where(n => n.Kind == UiKind.Button), b => Assert.True(b.Disabled));
        Assert.True(prefs.Content.Walk().Single(n => n.Key == "name:real_name").Disabled);
        string gender = prefs.Context.Data.Get("character_preferences").Get("misc").Str("gender");
        prefs.Context.Act("set_preference", ("preference", "gender"), ("value", gender == "male" ? "female" : "male"));
        prefs.Content.Walk().Single(n => n.Key == "arrows").Walk().Single(n => n.Kind == UiKind.Button && n.Icon == "chevron-right").OnClick(); // the preview's, not the voice pitch's
        w.Run(2);
        Assert.Equal(4, PreviewDir()); // turn(SOUTH, 90), within a tick
        w.Run(Second / 2);
        Assert.Equal(gender, Prefs().Context.Data.Get("character_preferences").Get("misc").Str("gender"));

        // DV215: Job Clothes works in the round and shows the clothes worn now: the uniform's worn icon, gone when it is off
        var human = Assert.IsType<mob_living_carbon_human>(In(w, () => Dm.each<client>(GlobalProcs.GLOB.clients).First(c => c.key as string == "Alice").mob));
        string uniformIcon = In(w, () => (human.w_uniform as obj_item)?.worn_icon?.ToString());
        Assert.False(string.IsNullOrEmpty(uniformIcon));
        string Worn() => string.Join(" ; ", CrewOutfitTests.Icons(replica, Preview().Appearance).Select(i => $"{i.Icon}:{i.State}"));
        Assert.Contains(uniformIcon, Worn());
        UiNode jobClothes = Prefs().Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Job Clothes");
        Assert.False(jobClothes.Disabled);
        jobClothes.OnClick();
        w.Run(Second / 2);
        Assert.DoesNotContain(uniformIcon, Worn());

        // the owner (2026-11-07): closed by the server (as a console's window is when one walks away), the window is suspended in the
        // pool; opened again, the menu takes that window, and the preview still shows on it
        In(w, () => GlobalProcs.SStgui.close_uis(Dm.each<client>(GlobalProcs.GLOB.clients).First(c => c.key as string == "Alice").prefs));
        w.Run(Second / 2);
        Assert.Null(Prefs());
        Assert.True(alice.Session.UiAct(Limits.EscapeMenuUiWindow, "character", null));
        w.Run(Second);
        mapRef = Prefs().Context.Data.Str("character_preview_view", null);
        Assert.StartsWith("character_preview_", mapRef);
        Assert.Contains("bodyparts_greyscale.dmi:human_chest_", Worn());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The owner (2026-10-30): the inventory key (I) clicks tg's inventory toggle, which the client finds by its icon state; the click sends
    // the worn slots (HudGroup.ToggleableInventory) and a second one takes them away
    [MetaServerFact]
    public void The_inventory_key_finds_tgs_inventory_toggle_and_it_shows_and_hides_the_worn_slots()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(4 * Second);
        ReplicaWorld replica = alice.Session.Replica;
        long toggle = replica.Screen.Values.Where(e => replica.TryGetAppearance(e.Appearance, out AppearanceEntry look)
            && replica.ResolveString(look.IconState) is "toggle" or "toggle_active").Select(e => e.Id).Single();
        bool Slots() => replica.Screen.Values.Any(e => e.Group == HudGroup.ToggleableInventory);
        Assert.False(Slots());
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.True(Slots());
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.False(Slots());
    }
}
