using System.Collections.Generic;
using System.IO;
using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the player's options and key bindings (D53, D70).</summary>
public class PlayOptionsTests
{
    private static string ShippedDefaults([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        string dir = Path.GetDirectoryName(here);
        while (!File.Exists(Path.Combine(dir, "AnomalyStation.sln")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir, "godot", "default-options.cfg"));
    }

    // D184: the readout panel's key went with the panel; an options file that still binds it reads without an error
    [Fact]
    public void TheRetiredAnalyzerKeyReadsClean()
    {
        var errors = new List<string>();
        PlayOptions.Parse("bind.analyzer=F9\n", errors);
        Assert.Empty(errors);
    }

    // D324: the file stays readable as the options change, and copying the owner's settings must not hand a new player a dev view
    [Fact]
    public void TheSettingsANewPlayerStartsFromHaveNoErrorAndNoDevView()
    {
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse(ShippedDefaults(), errors);
        Assert.Empty(errors);
        Assert.False(options.Dev.Fullbright);
        Assert.False(options.Dev.MinimapMobs);
        Assert.False(options.Dev.PlayerMarkers);
        Assert.False(options.Dev.GasOverlay);
        Assert.False(options.Dev.NamesOverlay);
    }

    [Fact]
    public void DefaultsKeepTgsKeysAndTheOffSwitchesAreOn()
    {
        var options = new PlayOptions();
        Assert.True(options.HeadBob);
        Assert.True(options.CameraShake);
        Assert.Equal(ColorblindMode.Off, options.Colorblind);
        Assert.Equal(100, options.FullscreenStrengthPercent);
        KeyBindings keys = options.Bindings;
        Assert.Equal(new[] { "U" }, keys.KeysOf(PlayAction.Rest));
        Assert.Equal(new[] { "F" }, keys.KeysOf(PlayAction.ToggleCombatMode));
        Assert.Equal(new[] { "C" }, keys.KeysOf(PlayAction.ToggleMoveIntent));
        Assert.Equal(new[] { "W", "Up" }, keys.KeysOf(PlayAction.MoveForward));
        Assert.Equal(new[] { "MouseRight" }, keys.KeysOf(PlayAction.SecondaryClick));
        Assert.Equal(new[] { "MouseLeft" }, keys.KeysOf(PlayAction.PrimaryClick));
        Assert.Equal(new[] { "Backslash" }, keys.KeysOf(PlayAction.DevMenu)); // D137: the | key (F8 before)
        Assert.Equal(new[] { "F12" }, keys.KeysOf(PlayAction.Fullscreen));
        Assert.Equal(new[] { "Delete" }, keys.KeysOf(PlayAction.StopPulling)); // tg's stop_pulling hotkeys (keybinding/mob.dm:6) less H, the Pull key (DV422)
        Assert.Equal(new[] { "H" }, keys.KeysOf(PlayAction.Pull));
        Assert.Equal(new[] { "Space" }, keys.KeysOf(PlayAction.MoveUpLevel)); // DV422: the owner's level keys
        Assert.Equal(new[] { "Ctrl" }, keys.KeysOf(PlayAction.MoveDownLevel));
        Assert.Equal(new[] { "K" }, keys.KeysOf(PlayAction.Scoreboard)); // DV585: held, the deathmatch scoreboard
        Assert.Equal("stop_pulling", KeyBindings.IdOf(PlayAction.StopPulling));
        // the owner (2026-10-09, 2026-10-13): neither a tile nor a thing under the crosshair is outlined by default
        Assert.False(options.HighlightTile);
        Assert.False(options.HighlightThing);
        // the owner (2026-10-18): the doll's text labels beside it are off by default
        Assert.False(options.ZoneLabels);
    }

    [Fact]
    public void EveryActionHasAKeyAndNoKeyServesTwoActions()
    {
        KeyBindings keys = KeyBindings.Defaults();
        var seen = new HashSet<string>();
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            if (action != PlayAction.HoldThrow) // DV422: the server takes Space as hold throw mode for a mob that cannot change level
                Assert.NotEmpty(keys.KeysOf(action));
            foreach (string key in keys.KeysOf(action))
            {
                Assert.True(KeyBindings.IsValidKeyName(key), key);
                Assert.True(seen.Add(key), key + " is bound twice");
            }
        }
    }

    [Fact]
    public void ITogglesTheInventoryAndTheNumbersClickTheActionButtons()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.Inventory, keys.ActionOf("I"));
        Assert.Equal(PlayAction.ActionButton1, keys.ActionOf("Key1"));
        Assert.Equal(PlayAction.ActionButton9, keys.ActionOf("Kp9"));
        Assert.Equal("Action button 3", KeyBindings.TitleOf(PlayAction.ActionButton3));
    }

    [Fact]
    public void BindingAKeyTakesItFromTheActionThatHadIt()
    {
        KeyBindings keys = KeyBindings.Defaults();
        PlayAction? displaced = keys.Bind(PlayAction.Rest, "F");
        Assert.Equal(PlayAction.ToggleCombatMode, displaced);
        Assert.Equal(new[] { "F" }, keys.KeysOf(PlayAction.Rest));
        Assert.Empty(keys.KeysOf(PlayAction.ToggleCombatMode));
        Assert.Equal(PlayAction.Rest, keys.ActionOf("F"));
        Assert.Null(keys.Bind(PlayAction.Rest, "Semicolon")); // M4a: Z is the default of ActivateInHand now; M9: J the emote wheel; DV585: K the scoreboard
        Assert.Throws<ArgumentException>(() => keys.Bind(PlayAction.Rest, "not a key"));
    }

    [Fact]
    public void ShiftWithAKeyIsItsOwnBindingAndFallsBackToTheKey()
    {
        // M9 slice C (D247): Shift+T opens the chat window; a key with Shift held (to run) still does its own action
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(new[] { "Shift_T" }, keys.KeysOf(PlayAction.ChatWindow));
        Assert.Equal(PlayAction.ChatWindow, keys.ActionOfPress("T", shift: true));
        Assert.Equal(PlayAction.Say, keys.ActionOfPress("T", shift: false));
        Assert.Equal(PlayAction.MoveForward, keys.ActionOfPress("W", shift: true));
        Assert.Equal("Shift", KeyBindings.NameOf("Shift", true));
        Assert.Equal("Shift_J", KeyBindings.NameOf("J", true));
        Assert.Equal("chat_window", KeyBindings.IdOf(PlayAction.ChatWindow));
    }

    [Fact]
    public void ActionIdsRoundTrip()
    {
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            string id = KeyBindings.IdOf(action);
            Assert.Equal(action, KeyBindings.ActionOfId(id));
            Assert.Matches("^[a-z_0-9]+$", id);
        }
        Assert.Equal("toggle_combat_mode", KeyBindings.IdOf(PlayAction.ToggleCombatMode));
        Assert.Null(KeyBindings.ActionOfId("fly"));
    }

    [Fact]
    public void TheTextRoundTrips()
    {
        var options = new PlayOptions
        {
            HeadBob = false,
            CameraShake = false,
            Colorblind = ColorblindMode.BlueYellow,
            FullscreenStrengthPercent = 40,
            ZoneLabels = true,
            HatchCues = false,
            UiScalePercent = 125,
            WindowScalePercent = 160,
            FovDegrees = 90,
            MouseSensitivity = 0.004f,
            HighlightThing = true,
            PortStatus = true,
            AimFacingSeconds = 2.5f,
        };
        options.Bindings.Bind(PlayAction.Rest, "Z");
        options.Bindings.Set(PlayAction.MoveForward, "Semicolon");
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(options.ToText(), errors);
        Assert.Empty(errors);
        Assert.False(back.HeadBob);
        Assert.False(back.CameraShake);
        Assert.Equal(ColorblindMode.BlueYellow, back.Colorblind);
        Assert.Equal(40, back.FullscreenStrengthPercent);
        Assert.True(back.ZoneLabels);
        Assert.False(back.HatchCues);
        Assert.Equal(125, back.UiScalePercent);
        Assert.Equal(160, back.WindowScalePercent);
        Assert.Equal(90f, back.FovDegrees);
        Assert.Equal(0.004f, back.MouseSensitivity, 5);
        Assert.True(back.HighlightThing);
        Assert.True(back.PortStatus);
        Assert.Equal(2.5f, back.AimFacingSeconds);
        Assert.False(back.HighlightTile);
        Assert.Equal(new[] { "Z" }, back.Bindings.KeysOf(PlayAction.Rest));
        Assert.Equal(new[] { "Semicolon" }, back.Bindings.KeysOf(PlayAction.MoveForward));
        Assert.Equal(options.ToText(), back.ToText());
    }

    [Fact]
    public void SavedWindowPlacesKeepTheirShareOfAnyScreen()
    {
        // The owner (2026-10-28): the places are for the screen they were saved on; an older file takes the first screen's.
        var options = new PlayOptions();
        Assert.Equal((960.0, 540.0), options.WindowOnScreen(960, 540, 1920, 1080));
        Assert.Equal((1920, 1080), options.WindowScreen);
        Assert.Equal((1920.0, 1080.0), options.WindowOnScreen(960, 540, 3840, 2160));
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(options.ToText(), errors);
        Assert.Empty(errors);
        Assert.Equal((1920, 1080), back.WindowScreen);
    }

    [Fact]
    public void TheWindowsGrowWithTheScreenHeightAndTheirOwnScale()
    {
        var options = new PlayOptions { WindowScalePercent = 100 };
        Assert.Equal(1f, options.WindowScaleFor(1080));
        Assert.Equal(1f, options.WindowScaleFor(720)); // never below tg's own size
        Assert.Equal(2f, options.WindowScaleFor(2160)); // a 4K screen
        options.WindowScalePercent = 150;
        Assert.Equal(3f, options.WindowScaleFor(2160));
        var errors = new List<string>();
        Assert.Equal(80, PlayOptions.Parse("window_scale=20\n", errors).WindowScalePercent);
        Assert.Single(errors);
        // the chat has its own scale, the windows' scale leaves it alone
        Assert.Equal(2f, options.ChatWindowScaleFor(1080) / 0.8f, 3);
        Assert.Equal(150, PlayOptions.Parse(new PlayOptions { ChatScalePercent = 150 }.ToText(), errors).ChatScalePercent);
    }

    [Fact]
    public void TheChatLinesOverTheViewHaveTheirOwnScaleHalfTheHudsByDefault()
    {
        Assert.Equal(0.5f, new PlayOptions().ChatLinesScale);
        var errors = new List<string>();
        PlayOptions read = PlayOptions.Parse(new PlayOptions { ChatLinesScalePercent = 75 }.ToText(), errors);
        Assert.Empty(errors);
        Assert.Equal(75, read.ChatLinesScalePercent);
        Assert.Equal(80, read.ChatScalePercent); // the chat window's own scale is apart
        Assert.Equal(50, PlayOptions.Parse("chat_lines_scale=10\n", errors).ChatLinesScalePercent);
        Assert.Single(errors);
    }

    [Fact]
    public void ABadLineIsReportedAndSkipped()
    {
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse("head_bob=maybe\nfov=500\ncolorblind=purple\nbind.fly=Q\nbind.rest=a b\nnonsense\n# comment\n\nui_scale=150\n", errors);
        Assert.Equal(6, errors.Count);
        Assert.True(options.HeadBob);
        Assert.Equal(75f, options.FovDegrees);
        Assert.Equal(150, options.UiScalePercent);
        Assert.Equal(new[] { "U" }, options.Bindings.KeysOf(PlayAction.Rest));
    }

    [Fact]
    public void ASettingFromTheCommandLineWorksLikeOneFromTheFile()
    {
        var options = new PlayOptions();
        Assert.True(options.TrySet("bind.secondary_click", "MouseMiddle", out _));
        Assert.Equal(new[] { "MouseMiddle" }, options.Bindings.KeysOf(PlayAction.SecondaryClick));
        Assert.True(options.TrySet("head_bob", "off", out _));
        Assert.False(options.HeadBob);
        Assert.False(options.TrySet("nothing", "1", out string problem));
        Assert.Equal("unknown option", problem);
    }

    [Fact]
    public void AMissingFileGivesTheDefaults()
    {
        PlayOptions options = PlayOptions.Parse(null);
        Assert.Equal(new PlayOptions().ToText(), options.ToText());
    }

    [Fact]
    public void TheStatsCornerIsOnByDefaultAndSaved()
    {
        // D138: the FPS and the server's numbers at the top right, F4 or the dev menu; on by default (the owner, 2026-10-18)
        var options = new PlayOptions();
        Assert.True(options.ShowStats);
        Assert.Contains("show_stats=true", options.ToText());
        options.ShowStats = false;
        Assert.False(PlayOptions.Parse(options.ToText(), null).ShowStats);
        Assert.Equal(new[] { "F4" }, options.Bindings.KeysOf(PlayAction.DebugHud));
    }

    [Fact]
    public void ShiftEscapeClosesTheWindows()
    {
        // D292: the close-windows key is Shift+Esc; a plain Esc stays the menu's
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.CloseWindows, keys.ActionOfPress("Escape", shift: true));
        Assert.Null(keys.ActionOfPress("Escape", shift: false));
        Assert.Equal(PlayAction.CloseWindows, PlayOptions.Parse(new PlayOptions().ToText()).Bindings.ActionOf("Shift_Escape"));
    }

    [Fact]
    public void TheWindowOpacityIsHalfByDefaultAndSaved()
    {
        // the owner (2026-10-29): the windows' and the chat's backgrounds, 50 % by default
        var options = new PlayOptions();
        Assert.Equal(0.5f, options.WindowOpacity);
        options.WindowOpacityPercent = 80;
        Assert.Equal(80, PlayOptions.Parse(options.ToText(), null).WindowOpacityPercent);
        Assert.False(options.TrySet("window_opacity", "5", out _));
    }

    [Fact]
    public void TheHudTextOpacityIsSaved()
    {
        var options = new PlayOptions();
        Assert.Equal(0.6f, options.HudTextOpacity);
        options.HudTextOpacityPercent = 30;
        Assert.Equal(30, PlayOptions.Parse(options.ToText(), null).HudTextOpacityPercent);
        Assert.False(options.TrySet("hud_text_opacity", "5", out _));
    }

    [Fact]
    public void TheRadialOpacityIsSaved()
    {
        var options = new PlayOptions();
        Assert.Equal(0.85f, options.RadialOpacity);
        options.RadialOpacityPercent = 40;
        Assert.Equal(40, PlayOptions.Parse(options.ToText(), null).RadialOpacityPercent);
        Assert.False(options.TrySet("radial_opacity", "5", out _));
    }

    [Fact]
    public void TheFullscreenResolutionIsSaved()
    {
        var options = new PlayOptions();
        Assert.Equal((0, 0), PlayOptions.Parse(options.ToText(), null).FullscreenResolution);
        options.FullscreenResolution = (1600, 900);
        Assert.Equal((1600, 900), PlayOptions.Parse(options.ToText(), null).FullscreenResolution);
        Assert.False(options.TrySet("fullscreen_resolution", "16", out _));
    }

    [Fact]
    public void TheDevMenusSettingsRoundTripAsDevLinesAndBadOnesAreRefused()
    {
        // the owner (2026-10-14): the airflow streams' settings and the dev menu's others live in the options file
        var defaults = new PlayOptions();
        Assert.True(defaults.Dev.Airflow);
        Assert.Equal(880, defaults.Dev.AirflowMaxStreams);
        Assert.Equal(126, defaults.Dev.AirflowMaxPerHole);
        Assert.Equal(20, defaults.Dev.AirflowSpawnDistance);
        Assert.False(defaults.Dev.AirflowDebug);
        Assert.True(defaults.Dev.GasHaze);
        Assert.True(defaults.Dev.Ceiling);

        var options = new PlayOptions();
        options.Dev.Airflow = false;
        options.Dev.AirflowMaxStreams = 900;
        options.Dev.AirflowMaxPerHole = 33;
        options.Dev.AirflowSpawnDistance = 40;
        options.Dev.AirflowDebug = true;
        options.Dev.GasHaze = false;
        options.Dev.GasOverlay = true;
        options.Dev.GasOverlayMode = "pressure";
        options.Dev.GasOverlayGas = "water_vapor";
        options.Dev.GasOverlayOpacityPercent = 70;
        options.Dev.CellCoordinates = true;
        options.Dev.CellCoordinatesRadius = 9;
        options.Dev.Ceiling = false;
        options.Dev.NamesOverlay = true;
        options.Dev.SpawnAmount = 12;
        options.Dev.GasMoles = 125.5f;
        options.Dev.GasRadius = 3;
        string text = options.ToText();
        Assert.Contains("dev.airflow=false\n", text);
        Assert.Contains("dev.airflow_max_streams=900\n", text);

        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(text, errors);
        Assert.Empty(errors);
        Assert.Equal(options.Dev.ToText(), back.Dev.ToText());
        Assert.Equal(text, back.ToText());

        var bad = new List<string>();
        PlayOptions refused = PlayOptions.Parse("dev.airflow_max_streams=5000\ndev.gas_overlay_mode=fog\ndev.nonsense=1\ndev.airflow=maybe\n", bad);
        Assert.Equal(4, bad.Count);
        Assert.Equal(880, refused.Dev.AirflowMaxStreams);
        Assert.Equal("mix", refused.Dev.GasOverlayMode);
        Assert.True(refused.TrySet("dev.airflow_max_per_hole", "1", out _));
        Assert.Equal(1, refused.Dev.AirflowMaxPerHole);
    }

    [Fact]
    public void TheSpawnPanelsFavouritesOfEachTabAndTheStickModeRoundTrip()
    {
        // the owner (2026-11-10): favourites per tab, kept with the other dev menu settings
        var options = new PlayOptions();
        options.Dev.SpawnStick = true;
        options.Dev.SpawnFavouritesOnly = true;
        options.Dev.SpawnFavourites[0] = DevMenuOptions.JoinPaths(new[] { "/obj/item/wrench", "/obj/item/crowbar" });
        options.Dev.SpawnFavourites[1] = "/obj/structure/window/reinforced/fulltile";
        string text = options.ToText();
        Assert.Contains("dev.spawn_favourites_objects=/obj/item/crowbar,/obj/item/wrench\n", text);
        Assert.Contains("dev.spawn_favourites_mobs=\n", text);

        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(text, errors);
        Assert.Empty(errors);
        Assert.True(back.Dev.SpawnStick && back.Dev.SpawnFavouritesOnly);
        Assert.Equal(new[] { "/obj/item/crowbar", "/obj/item/wrench" }, DevMenuOptions.SplitPaths(back.Dev.SpawnFavourites[0]));
        Assert.Equal(text, back.ToText());

        // a copy has its own lists
        DevMenuOptions copy = back.Dev.Clone();
        copy.SpawnFavourites[2] = "/mob/living/basic/cow";
        Assert.Equal("", back.Dev.SpawnFavourites[2]);

        var bad = new List<string>();
        PlayOptions.Parse("dev.spawn_favourites_turfs=/turf/closed/wall,rm -rf\ndev.spawn_favourites_items=/obj/item\n", bad);
        Assert.Equal(2, bad.Count);
    }

    [Fact]
    public void TheMinimapIs200PerCentByDefaultAndAnOlderFilesFirstDefaultMovesToIt()
    {
        Assert.Equal(200, new PlayOptions().MinimapSizePercent);
        Assert.Equal(200, PlayOptions.Parse("options_version=3\nminimap_size=100\n", null).MinimapSizePercent); // D315's first default
        Assert.Equal(150, PlayOptions.Parse("options_version=3\nminimap_size=150\n", null).MinimapSizePercent); // a chosen size stays
        Assert.Equal(100, PlayOptions.Parse("options_version=4\nminimap_size=100\n", null).MinimapSizePercent);
        Assert.Contains("minimap_size=200", new PlayOptions().ToText());
    }

    [Fact]
    public void MinimapTextSizeIsSavedAndBounded()
    {
        Assert.Equal(100, new PlayOptions().MinimapTextPercent);
        Assert.Equal(250, PlayOptions.Parse(new PlayOptions { MinimapTextPercent = 250 }.ToText(), null).MinimapTextPercent);
        Assert.Equal(100, PlayOptions.Parse("minimap_text=900\n", null).MinimapTextPercent); // out of 50 to 400: the default stays
    }

    [Fact]
    public void AnOlderFilesLevelKeysMoveToSpaceAndCtrlAndHPulls()
    {
        // DV422: a version 4 file saved every binding; its PageUp, PageDown, Space-held throw and H-stop-pulling keys move to the new layout
        PlayOptions old = PlayOptions.Parse("options_version=4\nbind.hold_throw=Space\nbind.stop_pulling=H,Delete\nbind.move_up_level=Pageup\nbind.move_down_level=Pagedown\n", null);
        Assert.Equal(new[] { "Space" }, old.Bindings.KeysOf(PlayAction.MoveUpLevel));
        Assert.Empty(old.Bindings.KeysOf(PlayAction.HoldThrow));
        Assert.Equal(new[] { "Ctrl" }, old.Bindings.KeysOf(PlayAction.MoveDownLevel));
        Assert.Equal(new[] { "H" }, old.Bindings.KeysOf(PlayAction.Pull));
        Assert.Equal(new[] { "Delete" }, old.Bindings.KeysOf(PlayAction.StopPulling));
        // keys the player chose stay
        PlayOptions chosen = PlayOptions.Parse("options_version=4\nbind.hold_throw=Space\nbind.move_up_level=K\n", null);
        Assert.Equal(new[] { "K" }, chosen.Bindings.KeysOf(PlayAction.MoveUpLevel));
        Assert.Equal(new[] { "Space" }, chosen.Bindings.KeysOf(PlayAction.HoldThrow));
    }
}
