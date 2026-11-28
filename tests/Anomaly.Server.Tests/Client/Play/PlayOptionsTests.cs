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
        // ui-overhaul.md section 3: SS14's keys where SS14 has the function (the whole table: TheDefaultKeysAreTheTableOfTheUiOverhaul)
        Assert.Equal(new[] { "C" }, keys.KeysOf(PlayAction.Rest));
        Assert.Equal(new[] { "F" }, keys.KeysOf(PlayAction.ToggleCombatMode));
        Assert.Equal(new[] { "Shift" }, keys.KeysOf(PlayAction.ToggleMoveIntent));
        Assert.Equal(new[] { "W", "Up" }, keys.KeysOf(PlayAction.MoveForward));
        Assert.Equal(new[] { "MouseRight" }, keys.KeysOf(PlayAction.SecondaryClick));
        Assert.Equal(new[] { "MouseLeft" }, keys.KeysOf(PlayAction.PrimaryClick));
        Assert.Equal(new[] { "Backslash" }, keys.KeysOf(PlayAction.DevMenu)); // D137: the | key (F8 before)
        Assert.Equal(new[] { "F11" }, keys.KeysOf(PlayAction.Fullscreen));
        Assert.Equal(new[] { "H", "Delete" }, keys.KeysOf(PlayAction.StopPulling)); // tg's stop_pulling hotkeys (keybinding/mob.dm)
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, keys.KeysOf(PlayAction.Pull));
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
                // slice 4B-2: the quick transfer shares Pull's Ctrl+left click, acting only on an item of the Tab inventory (KeyBindings.SharesKeys)
                if (action == PlayAction.QuickTransfer && keys.Has(PlayAction.Pull, key))
                    continue;
                Assert.True(seen.Add(key), key + " is bound twice");
            }
        }
    }

    [Fact]
    public void ITogglesTheInventoryAndTheNumbersClickTheActionButtons()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.Inventory, keys.ActionOf("I"));
        // ui-overhaul.md section 10: the numbers are the hotbar's, Shift and a number tg's action buttons
        Assert.Equal(PlayAction.ActionButton1, keys.ActionOf("Shift_Key1"));
        Assert.Equal(PlayAction.ActionButton9, keys.ActionOfPress("Key9", shift: true));
        Assert.Equal(PlayAction.Hotbar1, keys.ActionOf("Key1"));
        Assert.Equal(PlayAction.Hotbar9, keys.ActionOf("Kp9"));
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
        Assert.Null(keys.Bind(PlayAction.Rest, "Bracketright")); // M4a: Z is the default of ActivateInHand now; M9: J the emote wheel; DV585: K the scoreboard; ui-overhaul.md: ; the radio
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
        options.Bindings.Set(PlayAction.MoveForward, "Bracketright"); // a key no other action has (Semicolon is the radio's since ui-overhaul.md)
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
        Assert.Equal(new[] { "Bracketright" }, back.Bindings.KeysOf(PlayAction.MoveForward));
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
    public void TheWindowScaleKeysAreTheGamesAndThePanelRefusesThem()
    {
        // GameScene.WindowScaleKey reads WindowScaleAfterKey with the engine's key name; the panel refuses exactly those keys with Ctrl (ui-overhaul.md 5)
        Assert.Equal(90, PlayOptions.WindowScaleAfterKey("Equal", 80));
        Assert.Equal(90, PlayOptions.WindowScaleAfterKey("KpAdd", 80));
        Assert.Equal(70, PlayOptions.WindowScaleAfterKey("Minus", 80));
        Assert.Equal(70, PlayOptions.WindowScaleAfterKey("KpSubtract", 80));
        Assert.Equal(100, PlayOptions.WindowScaleAfterKey("Key0", 80));
        Assert.Equal(100, PlayOptions.WindowScaleAfterKey("Kp0", 80));
        Assert.Equal(-1, PlayOptions.WindowScaleAfterKey("Q", 80));
        foreach (string key in new[] { "Equal", "KpAdd", "Minus", "KpSubtract", "Key0", "Kp0" })
        {
            Assert.True(PlayOptions.IsWindowScalePair(key, ctrl: true), key); // with Shift too: the game checks Ctrl alone
            Assert.False(PlayOptions.IsWindowScalePair(key, ctrl: false), key); // a plain = or 0 stays bindable
        }
        Assert.False(PlayOptions.IsWindowScalePair("Q", ctrl: true)); // Ctrl+Q (throw) stays bindable
        Assert.False(PlayOptions.IsWindowScalePair("Key1", ctrl: true));
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
        Assert.Equal(new[] { "C" }, options.Bindings.KeysOf(PlayAction.Rest));
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
        // D138: the FPS and the server's numbers at the top right, F3 (SS14's ShowDebugMonitors; F4 before) or the dev menu; on by default
        var options = new PlayOptions();
        Assert.True(options.ShowStats);
        Assert.Contains("show_stats=true", options.ToText());
        options.ShowStats = false;
        Assert.False(PlayOptions.Parse(options.ToText(), null).ShowStats);
        Assert.Equal(new[] { "F3" }, options.Bindings.KeysOf(PlayAction.DebugHud));
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
        // DV422: a version 4 file saved every binding; its PageUp, PageDown and Space-held throw keys move to the new layout. Its H and Delete
        // stay stop pulling: a version 4 file has no pull line, so Pull keeps its default (Ctrl+left click since ui-overhaul.md section 3; H was
        // the default DV422 gave it, which took H from stop pulling)
        PlayOptions old = PlayOptions.Parse("options_version=4\nbind.hold_throw=Space\nbind.stop_pulling=H,Delete\nbind.move_up_level=Pageup\nbind.move_down_level=Pagedown\n", null);
        Assert.Equal(new[] { "Space" }, old.Bindings.KeysOf(PlayAction.MoveUpLevel));
        Assert.Empty(old.Bindings.KeysOf(PlayAction.HoldThrow));
        Assert.Equal(new[] { "Ctrl" }, old.Bindings.KeysOf(PlayAction.MoveDownLevel));
        Assert.Equal(new[] { "Ctrl_MouseLeft" }, old.Bindings.KeysOf(PlayAction.Pull));
        Assert.Equal(new[] { "H", "Delete" }, old.Bindings.KeysOf(PlayAction.StopPulling));
        // keys the player chose stay
        PlayOptions chosen = PlayOptions.Parse("options_version=4\nbind.hold_throw=Space\nbind.move_up_level=K\n", null);
        Assert.Equal(new[] { "K" }, chosen.Bindings.KeysOf(PlayAction.MoveUpLevel));
        Assert.Equal(new[] { "Space" }, chosen.Bindings.KeysOf(PlayAction.HoldThrow));
    }

    // ui-overhaul.md section 3, row by row ("Default now"); the rows that name several actions are split into theirs
    private static readonly Dictionary<PlayAction, string[]> KeysTable = new()
    {
        [PlayAction.MoveForward] = new[] { "W", "Up" },
        [PlayAction.MoveBack] = new[] { "S", "Down" },
        [PlayAction.MoveLeft] = new[] { "A", "Left" },
        [PlayAction.MoveRight] = new[] { "D", "Right" },
        [PlayAction.ToggleMoveIntent] = new[] { "Shift" }, // walk and run: Shift (tapped alone, ModifierTap)
        [PlayAction.Rest] = new[] { "C" },
        [PlayAction.ToggleCombatMode] = new[] { "F" },
        [PlayAction.PrimaryClick] = new[] { "MouseLeft" },
        [PlayAction.SecondaryClick] = new[] { "MouseRight" },
        [PlayAction.DropItem] = new[] { "Q" },
        [PlayAction.ThrowMode] = new[] { "Ctrl_Q" },
        [PlayAction.SwapHand] = new[] { "X" },
        [PlayAction.MiddleClick] = new[] { "MouseMiddle" }, // swap hands' second binding
        [PlayAction.ActivateInHand] = new[] { "Z" },
        [PlayAction.Examine] = new[] { "Shift_MouseLeft" },
        [PlayAction.Pull] = new[] { "Ctrl_MouseLeft" },
        [PlayAction.QuickTransfer] = new[] { "Ctrl_MouseLeft" }, // section 12, slice 4B-2: on an item of the Tab inventory, Pull elsewhere
        [PlayAction.StopPulling] = new[] { "H", "Delete" }, // H and Delete, as in tg
        [PlayAction.Say] = new[] { "T" },
        [PlayAction.Radio] = new[] { "Semicolon" },
        [PlayAction.Me] = new[] { "Shift_Apostrophe" },
        [PlayAction.Ooc] = new[] { "Bracketleft" },
        [PlayAction.EmoteWheel] = new[] { "Y" },
        [PlayAction.Inventory] = new[] { "I" },
        [PlayAction.CloseWindows] = new[] { "Shift_Escape" },
        // ui-overhaul.md section 10: the hotbar on 1 to 9 and 0, the number row and the keypad
        [PlayAction.Hotbar1] = new[] { "Key1", "Kp1" },
        [PlayAction.Hotbar2] = new[] { "Key2", "Kp2" },
        [PlayAction.Hotbar3] = new[] { "Key3", "Kp3" },
        [PlayAction.Hotbar4] = new[] { "Key4", "Kp4" },
        [PlayAction.Hotbar5] = new[] { "Key5", "Kp5" },
        [PlayAction.Hotbar6] = new[] { "Key6", "Kp6" },
        [PlayAction.Hotbar7] = new[] { "Key7", "Kp7" },
        [PlayAction.Hotbar8] = new[] { "Key8", "Kp8" },
        [PlayAction.Hotbar9] = new[] { "Key9", "Kp9" },
        [PlayAction.Hotbar10] = new[] { "Key0", "Kp0" },
        [PlayAction.ActionButton1] = new[] { "Shift_Key1" }, // section 10: Shift and the digit
        [PlayAction.ActionButton2] = new[] { "Shift_Key2" }, // section 10: Shift and the digit
        [PlayAction.ActionButton3] = new[] { "Shift_Key3" }, // section 10: Shift and the digit
        [PlayAction.ActionButton4] = new[] { "Shift_Key4" }, // section 10: Shift and the digit
        [PlayAction.ActionButton5] = new[] { "Shift_Key5" }, // section 10: Shift and the digit
        [PlayAction.ActionButton6] = new[] { "Shift_Key6" }, // section 10: Shift and the digit
        [PlayAction.ActionButton7] = new[] { "Shift_Key7" }, // section 10: Shift and the digit
        [PlayAction.ActionButton8] = new[] { "Shift_Key8" }, // section 10: Shift and the digit
        [PlayAction.ActionButton9] = new[] { "Shift_Key9" }, // section 10: Shift and the digit
        [PlayAction.Console] = new[] { "Quoteleft" },
        [PlayAction.DebugHud] = new[] { "F3" },
        [PlayAction.HideInterface] = new[] { "Shift_F4" },
        [PlayAction.Fullscreen] = new[] { "F11" },
        [PlayAction.Names] = new[] { "F4" },
        [PlayAction.ViewPetCommands] = new[] { "J" },
        [PlayAction.CursorMode] = new[] { "Tab" },
        [PlayAction.QuickEquip] = new[] { "R" }, // section 12, slice 4E: E is world interact
        // section 12, slice 4E: Space Station 14's keys
        [PlayAction.WorldInteract] = new[] { "E" },
        [PlayAction.AltInteract] = new[] { "Alt_E" },
        [PlayAction.AltUseInHand] = new[] { "Alt_Z" },
        [PlayAction.QuickEquipBag] = new[] { "Shift_B" },
        [PlayAction.QuickEquipBelt] = new[] { "Shift_E" },
        [PlayAction.QuickEquipLeftPocket] = new[] { "Shift_G" },
        [PlayAction.QuickEquipRightPocket] = new[] { "Shift_H" },
        [PlayAction.QuickEquipSuitStorage] = new[] { "Shift_F" },
        [PlayAction.OpenBackpack] = new[] { "V" },
        [PlayAction.OpenBelt] = new[] { "Shift_V" },
        [PlayAction.Whisper] = new[] { "Comma" },
        [PlayAction.MovePulled] = new[] { "Ctrl_MouseRight" },
        // the last row: resist, pray, push to talk, scoreboard, levels, the other function keys, the | key; slice E (section 6): the flashlight moved
        // to U, L is the language menu, G (the give key's, which is gone) crafting, M navigate
        [PlayAction.Resist] = new[] { "B" },
        [PlayAction.Pray] = new[] { "P" },
        [PlayAction.CraftingMenu] = new[] { "G" },
        [PlayAction.LanguageMenu] = new[] { "L" },
        [PlayAction.Navigate] = new[] { "M" },
        [PlayAction.PushToTalk] = new[] { "N" },
        [PlayAction.Flashlight] = new[] { "U" },
        [PlayAction.Scoreboard] = new[] { "K" },
        [PlayAction.MoveUpLevel] = new[] { "Space" },
        [PlayAction.MoveDownLevel] = new[] { "Ctrl" },
        [PlayAction.CameraMode] = new[] { "F1" },
        [PlayAction.Ceiling] = new[] { "F2" },
        [PlayAction.ThirdPersonView] = new[] { "F5" },
        [PlayAction.Options] = new[] { "F10" },
        [PlayAction.DevMenu] = new[] { "Backslash" },
        // not in the table, so unchanged
        [PlayAction.ChatWindow] = new[] { "Shift_T" },
        [PlayAction.PushToTalkTyping] = new[] { "Alt" },
        [PlayAction.HoldThrow] = Array.Empty<string>(), // DV422: Space is it for a mob that cannot change level
    };

    public static IEnumerable<object[]> KeysTableRows() => KeysTable.Select(row => new object[] { row.Key, string.Join(",", row.Value) });

    [Theory]
    [MemberData(nameof(KeysTableRows))]
    public void TheDefaultKeysAreTheTableOfTheUiOverhaul(PlayAction action, string keys)
    {
        Assert.Equal(keys, string.Join(",", KeyBindings.Defaults().KeysOf(action)));
    }

    [Fact]
    public void TheKeysTableNamesEveryAction()
    {
        Assert.Equal(Enum.GetValues<PlayAction>().OrderBy(a => a), KeysTable.Keys.OrderBy(a => a));
    }

    [Fact]
    public void CtrlWithAKeyOrAClickIsItsOwnBindingAndFallsBackToTheKey()
    {
        KeyBindings keys = KeyBindings.Defaults();
        // Ctrl+Q throws (SS14's ThrowItemInHand) and only throws: the press is one action, not Q's drop as well
        Assert.Equal(PlayAction.ThrowMode, keys.ActionOfPress("Q", shift: false, ctrl: true));
        Assert.Equal(PlayAction.DropItem, keys.ActionOfPress("Q", shift: false));
        Assert.Equal(PlayAction.ThrowMode, keys.ActionOfPress("Q", shift: true, ctrl: true)); // walking (Shift held) and throwing
        // Ctrl itself is the level key, not a pair with itself
        Assert.Equal("Ctrl", KeyBindings.NameOf("Ctrl", false, true));
        Assert.Equal(PlayAction.MoveDownLevel, keys.ActionOfPress("Ctrl", shift: false, ctrl: true));
        Assert.Equal(PlayAction.ToggleMoveIntent, keys.ActionOfPress("Shift", shift: true, ctrl: true));
        // a key without a Ctrl binding does its own action
        Assert.Equal(PlayAction.MoveForward, keys.ActionOfPress("W", shift: false, ctrl: true));
        Assert.Equal(PlayAction.Say, keys.ActionOfPress("T", shift: false, ctrl: true));
        // Ctrl before Shift in a name; with both held the Ctrl pair comes first, then the Shift one
        Assert.Equal("Ctrl_Shift_T", KeyBindings.NameOf("T", true, true));
        Assert.Equal(PlayAction.ChatWindow, keys.ActionOfPress("T", shift: true, ctrl: true));
        Assert.True(KeyBindings.IsValidKeyName("Ctrl_Shift_MouseMiddle"));
        // the clicks: Shift+left click examines, Ctrl+left click pulls (Ctrl-Shift too, tg's Ctrl-Shift-click), a plain one is the left click
        Assert.Equal(PlayAction.Examine, keys.ActionOfPress("MouseLeft", shift: true));
        Assert.Equal(PlayAction.Pull, keys.ActionOfPress("MouseLeft", shift: false, ctrl: true));
        Assert.Equal(PlayAction.Pull, keys.ActionOfPress("MouseLeft", shift: true, ctrl: true));
        Assert.Equal(PlayAction.PrimaryClick, keys.ActionOfPress("MouseLeft", shift: false));
        Assert.Equal(PlayAction.MovePulled, keys.ActionOfPress("MouseRight", shift: true, ctrl: true)); // slice 4E: Ctrl+right click moves the pulled thing
        Assert.Equal(PlayAction.SecondaryClick, keys.ActionOfPress("MouseRight", shift: true));
        // the Shift pairs of the table
        Assert.Equal(PlayAction.HideInterface, keys.ActionOfPress("F4", shift: true));
        Assert.Equal(PlayAction.Names, keys.ActionOfPress("F4", shift: false));
        Assert.Equal(PlayAction.Me, keys.ActionOfPress("Apostrophe", shift: true));
        Assert.Null(keys.ActionOfPress("Apostrophe", shift: false));
    }

    [Fact]
    public void ThePanelTakesAMouseButtonOnlyWhereTheGameActsOnItAndNeverFromAClickAction()
    {
        // ui-overhaul.md section 5: a plain button only for the three click actions (a modifier held then is not recorded)
        Assert.Equal("MouseLeft", KeyBindings.MouseBindingName(PlayAction.PrimaryClick, "MouseLeft", shift: true, ctrl: false));
        Assert.Equal("MouseRight", KeyBindings.MouseBindingName(PlayAction.SecondaryClick, "MouseRight", false, false));
        Assert.Equal("MouseMiddle", KeyBindings.MouseBindingName(PlayAction.MiddleClick, "MouseMiddle", false, true));
        KeyBindings keys = KeyBindings.Defaults();
        keys.Bind(PlayAction.PrimaryClick, KeyBindings.MouseBindingName(PlayAction.PrimaryClick, "MouseLeft", true, false));
        Assert.Equal(PlayAction.PrimaryClick, keys.ActionOf("MouseLeft"));
        Assert.Equal(PlayAction.Examine, keys.ActionOfPress("MouseLeft", shift: true));
        // Examine and Pull: the left button with Shift, Ctrl or both, nothing else from the mouse (never a plain button, never another button)
        Assert.Equal("Shift_MouseLeft", KeyBindings.MouseBindingName(PlayAction.Examine, "MouseLeft", true, false));
        Assert.Equal("Ctrl_MouseLeft", KeyBindings.MouseBindingName(PlayAction.Examine, "MouseLeft", false, true));
        Assert.Equal("Ctrl_Shift_MouseLeft", KeyBindings.MouseBindingName(PlayAction.Pull, "MouseLeft", true, true));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Examine, "MouseLeft", false, false));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Pull, "MouseLeft", false, false));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Examine, "MouseRight", true, false)); // it took the secondary click's button before
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Pull, "MouseMiddle", false, true));
        // any other action: nothing from the mouse (Say captured with Ctrl+left click took the primary click's button before)
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Say, "MouseLeft", false, true));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.DropItem, "MouseRight", false, false));
        Assert.True(KeyBindings.TakesModifiedMouse(PlayAction.Examine));
        Assert.False(KeyBindings.TakesModifiedMouse(PlayAction.PrimaryClick));
        Assert.False(KeyBindings.TakesModifiedMouse(null));
    }

    [Fact]
    public void AModifierThatIsPartOfAPullOrExamineBindingIsNotPassedOnOnlyOneHeldOnTop()
    {
        // ui-overhaul.md section 5: the click a press bound to Pull or Examine sends, from the binding it matched (MatchPress)
        Anomaly.Protocol.ClickModifiers Sent(KeyBindings keys, string key, bool shift, bool ctrl)
        {
            (PlayAction? action, bool ownShift, bool ownCtrl) = keys.MatchPress(key, shift, ctrl);
            Assert.True(KeyBindings.TakesModifiedMouse(action));
            return KeyBindings.ModifiedClick(action.Value, shift && !ownShift, ctrl && !ownCtrl);
        }
        const Anomaly.Protocol.ClickModifiers Ctrl = Anomaly.Protocol.ClickModifiers.Ctrl, Shift = Anomaly.Protocol.ClickModifiers.Shift;
        KeyBindings defaults = KeyBindings.Defaults();
        Assert.Equal(Ctrl, Sent(defaults, "MouseLeft", false, true)); // Pull on Ctrl+left click: tg's Ctrl-click
        Assert.Equal(Ctrl | Shift, Sent(defaults, "MouseLeft", true, true)); // with Shift held on top: tg's Ctrl-Shift-click
        Assert.Equal(Shift, Sent(defaults, "MouseLeft", true, false)); // Examine on Shift+left click
        var rebound = KeyBindings.Defaults();
        rebound.Bind(PlayAction.Pull, "Shift_MouseLeft"); // takes it from Examine
        Assert.Equal(Ctrl, Sent(rebound, "MouseLeft", true, false)); // pulls: the Shift is the binding's
        rebound.Bind(PlayAction.Pull, "Ctrl_Shift_MouseLeft");
        Assert.Equal(Ctrl, Sent(rebound, "MouseLeft", true, true));
        rebound.Bind(PlayAction.Examine, "Ctrl_MouseLeft");
        Assert.Equal(Shift, Sent(rebound, "MouseLeft", false, true)); // examines: the Ctrl is the binding's
        // a key pair the same way: Pull on Shift+P is tg's Ctrl-click, on P with Shift held on top Ctrl-Shift
        rebound.Bind(PlayAction.Pull, "Shift_P");
        Assert.Equal(Ctrl, Sent(rebound, "P", true, false));
        rebound.Bind(PlayAction.Pull, "P");
        Assert.Equal(Ctrl | Shift, Sent(rebound, "P", true, false));
        Assert.Equal(Ctrl, Sent(rebound, "P", false, false));
    }

    [Fact]
    public void ASavedFileWithTheOldKeysLoadsUnchanged()
    {
        // every binding as the client saved the layout before ui-overhaul.md section 3 (options version 5 writes every action)
        string old = "options_version=5\nbind.move_forward=W,Up\nbind.move_back=S,Down\nbind.move_left=A,Left\nbind.move_right=D,Right\nbind.rest=U\n"
            + "bind.toggle_move_intent=C\nbind.toggle_combat_mode=F\nbind.primary_click=MouseLeft\nbind.secondary_click=MouseRight\nbind.console=Quoteleft\n"
            + "bind.options=F10\nbind.analyzer=F9\nbind.dev_menu=Backslash\nbind.camera_mode=F1\nbind.third_person_view=F5\nbind.ceiling=F2\nbind.names=F3\n"
            + "bind.debug_hud=F4\nbind.flashlight=L\nbind.middle_click=MouseMiddle\nbind.drop_item=Q\nbind.throw_mode=R\nbind.swap_hand=X\n"
            + "bind.activate_in_hand=Z\nbind.quick_equip=E\nbind.hold_throw=\nbind.cursor_mode=Tab\nbind.examine=V\nbind.fullscreen=F12\n"
            + "bind.stop_pulling=Delete\nbind.resist=B\nbind.say=T\nbind.radio=Y\nbind.me=M\nbind.ooc=O\nbind.pray=P\nbind.emote_wheel=J\n"
            + "bind.chat_window=Shift_T\nbind.give_item=G\nbind.view_pet_commands=Shift\nbind.close_windows=Shift_Escape\nbind.inventory=I\n"
            + "bind.action_button1=Key1,Kp1\nbind.action_button2=Key2,Kp2\nbind.action_button3=Key3,Kp3\nbind.action_button4=Key4,Kp4\n"
            + "bind.action_button5=Key5,Kp5\nbind.action_button6=Key6,Kp6\nbind.action_button7=Key7,Kp7\nbind.action_button8=Key8,Kp8\n"
            + "bind.action_button9=Key9,Kp9\nbind.push_to_talk=N\nbind.push_to_talk_typing=Alt\nbind.move_up_level=Space\nbind.move_down_level=Ctrl\n"
            + "bind.hide_interface=F6\nbind.pull=H\nbind.scoreboard=K\n";
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse(old, errors);
        Assert.Empty(errors);
        int lines = 0;
        foreach (string line in old.Split('\n'))
        {
            if (!line.StartsWith("bind.", StringComparison.Ordinal))
                continue;
            lines++;
            string[] pair = line.Substring(5).Split('=');
            if (pair[0] is "give_item" or "analyzer")
            {
                // slice E: the give key is gone, and the readout panel's key with the panel (D184); each line is read without an error and names no action
                Assert.Null(KeyBindings.ActionOfId(pair[0]));
                continue;
            }
            PlayAction action = KeyBindings.ActionOfId(pair[0]).Value;
            if (action is >= PlayAction.ActionButton1 and <= PlayAction.ActionButton9)
            {
                // slice 4E (options version 6): the action buttons left on the plain digits move to Shift and the digits, the digits go back to the hotbar
                Assert.Equal(new[] { "Shift_" + pair[1].Split(',')[0] }, options.Bindings.KeysOf(action));
                continue;
            }
            Assert.True(string.Join(",", options.Bindings.KeysOf(action)) == pair[1], $"{pair[0]}: {string.Join(",", options.Bindings.KeysOf(action))}, saved {pair[1]}");
        }
        // every action of the time, the give key among them: today's actions less slice E's three menu keys, section 10's ten hotbar keys, slice
        // 4B-2's quick transfer and slice 4E's twelve keys, and the give and analyzer keys
        Assert.Equal(Enum.GetValues<PlayAction>().Length - 3 - 10 - 1 - 12 + 2, lines);
        // slice 4E: quick equip stays on E there, as R is that file's throw key; the world interact key then has none
        Assert.Equal(new[] { "E" }, options.Bindings.KeysOf(PlayAction.QuickEquip));
        Assert.Empty(options.Bindings.KeysOf(PlayAction.WorldInteract));
        // slice E's menu keys keep their defaults where the old file left the key free (G: the give key's line is ignored), and lose it where the old
        // file gave it to another action (L the flashlight's, M the me key's), as a key serves one action
        Assert.Equal(new[] { "G" }, options.Bindings.KeysOf(PlayAction.CraftingMenu));
        Assert.Empty(options.Bindings.KeysOf(PlayAction.LanguageMenu));
        Assert.Empty(options.Bindings.KeysOf(PlayAction.Navigate));
        // and the old keys do what they did: R throws, Ctrl+Q drops (no Ctrl pair there), Shift+left click is the plain left click, V examines
        Assert.Equal(PlayAction.ThrowMode, options.Bindings.ActionOfPress("R", shift: false));
        Assert.Equal(PlayAction.DropItem, options.Bindings.ActionOfPress("Q", shift: false, ctrl: true));
        Assert.Equal(PlayAction.PrimaryClick, options.Bindings.ActionOfPress("MouseLeft", shift: true));
        // slice 4B-2: the quick transfer (no line in the old file) keeps its default Ctrl+left click, so Ctrl and Shift with the left click match it;
        // the game still takes that press as the plain left click with those modifiers anywhere but on an item of the Tab inventory (it is no
        // action that takes a modified mouse press, GameScene.OnMouseButton)
        Assert.Equal(PlayAction.QuickTransfer, options.Bindings.ActionOfPress("MouseLeft", shift: true, ctrl: true));
        Assert.False(KeyBindings.TakesModifiedMouse(PlayAction.QuickTransfer));
        Assert.Equal(PlayAction.Examine, options.Bindings.ActionOf("V"));
        Assert.Equal(PlayAction.ViewPetCommands, options.Bindings.ActionOfPress("Shift", shift: true));
    }

    [Fact]
    public void Slice4EOptionsUpgradeMovesQuickEquipToRAndGivesETheWorldInteractKey()
    {
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse("options_version=5\nbind.quick_equip=E\nbind.throw_mode=Ctrl_Q\n", errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "R" }, options.Bindings.KeysOf(PlayAction.QuickEquip));
        Assert.Equal(new[] { "E" }, options.Bindings.KeysOf(PlayAction.WorldInteract));
        Assert.Equal(PlayAction.WorldInteract, options.Bindings.ActionOfPress("E", shift: false));
        // a version 6 file is read as it is: quick equip on E stays the player's choice
        PlayOptions chosen = PlayOptions.Parse("options_version=6\nbind.quick_equip=E\n", errors);
        Assert.Equal(new[] { "E" }, chosen.Bindings.KeysOf(PlayAction.QuickEquip));
        Assert.Empty(chosen.Bindings.KeysOf(PlayAction.WorldInteract));
        // the defaults and the shipped starting file agree
        Assert.Equal(new[] { "R" }, KeyBindings.Defaults().KeysOf(PlayAction.QuickEquip));
        Assert.Equal(new[] { "E" }, KeyBindings.Defaults().KeysOf(PlayAction.WorldInteract));
    }

    [Fact]
    public void Slice4EKeysMatchWithAltShiftAndCtrlAndTheirNamesRoundTrip()
    {
        KeyBindings keys = KeyBindings.Defaults();
        Assert.Equal(PlayAction.AltInteract, keys.ActionOfPress("E", shift: false, ctrl: false, alt: true));
        Assert.Equal(PlayAction.AltUseInHand, keys.ActionOfPress("Z", shift: false, ctrl: false, alt: true));
        Assert.Equal(PlayAction.WorldInteract, keys.ActionOfPress("E", shift: false));
        Assert.Equal(PlayAction.QuickEquipBelt, keys.ActionOfPress("E", shift: true));
        Assert.Equal(PlayAction.QuickEquipBag, keys.ActionOfPress("B", shift: true));
        Assert.Equal(PlayAction.Resist, keys.ActionOfPress("B", shift: false));
        Assert.Equal(PlayAction.QuickEquipLeftPocket, keys.ActionOfPress("G", shift: true));
        Assert.Equal(PlayAction.QuickEquipRightPocket, keys.ActionOfPress("H", shift: true));
        Assert.Equal(PlayAction.QuickEquipSuitStorage, keys.ActionOfPress("F", shift: true));
        Assert.Equal(PlayAction.ToggleCombatMode, keys.ActionOfPress("F", shift: false));
        Assert.Equal(PlayAction.OpenBackpack, keys.ActionOfPress("V", shift: false));
        Assert.Equal(PlayAction.OpenBelt, keys.ActionOfPress("V", shift: true));
        Assert.Equal(PlayAction.Whisper, keys.ActionOfPress("Comma", shift: false));
        Assert.Equal(ChatInputModel.Whisper, ChatInputModel.ChannelOfKey(PlayAction.Whisper)); // the chat input's whisper; tg's # in the say window
        Assert.Equal("#", ChatInputModel.WhisperKey);
        Assert.Equal(PlayAction.MovePulled, keys.ActionOfPress("MouseRight", shift: false, ctrl: true));
        Assert.Equal(PlayAction.SecondaryClick, keys.ActionOfPress("MouseRight", shift: false));
        Assert.Equal(PlayAction.Pull, keys.ActionOfPress("MouseLeft", shift: false, ctrl: true));
        // Alt with a key that has no Alt binding is the key's own action (as Shift and Ctrl held out of habit), and Alt alone the push to talk key
        Assert.Equal(PlayAction.QuickEquip, keys.ActionOfPress("R", shift: false, ctrl: false, alt: true));
        Assert.Equal(PlayAction.PushToTalkTyping, keys.ActionOfPress("Alt", shift: false, ctrl: false, alt: true));
        Assert.Equal("Alt_E", KeyBindings.NameOf("E", false, false, alt: true));
        Assert.Equal("Ctrl_Alt_Shift_E", KeyBindings.NameOf("E", true, true, alt: true));
        Assert.Equal("Alt", KeyBindings.NameOf("Alt", false, false, alt: true));
        Assert.Equal("Alt+E", KeyChips.Label("Alt_E"));
        // the options panel takes Ctrl+right click for the move-pulled key only
        Assert.Equal("Ctrl_MouseRight", KeyBindings.MouseBindingName(PlayAction.MovePulled, "MouseRight", false, true));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.MovePulled, "MouseRight", false, false));
        Assert.Null(KeyBindings.MouseBindingName(PlayAction.Pull, "MouseRight", false, true));
        Assert.Equal(Anomaly.Protocol.KeybindingCode.QuickEquipBag, KeyBindings.QuickEquipCodeOf(PlayAction.QuickEquipBag));
        Assert.Equal(Anomaly.Protocol.KeybindingCode.QuickEquipSuitStorage, KeyBindings.QuickEquipCodeOf(PlayAction.QuickEquipSuitStorage));
        Assert.Equal(Anomaly.Protocol.KeybindingCode.None, KeyBindings.QuickEquipCodeOf(PlayAction.QuickEquip));
        // a saved file keeps the new keys (the shipped starting file: TheSettingsANewPlayerStartsFromHaveTheDefaultKeys)
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(new PlayOptions().ToText(), errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "Alt_E" }, back.Bindings.KeysOf(PlayAction.AltInteract));
        Assert.Contains("options_version=6\n", ShippedDefaults());
    }

    [Fact]
    public void TheGiveKeyIsGoneAndAFileThatStillNamesItLoadsWithoutAnError()
    {
        // ui-overhaul.md section 6, slice E: "Offer item" is in the radial; the give key's line is read and ignored, whatever key it names
        Assert.DoesNotContain("give_item", Enum.GetValues<PlayAction>().Select(KeyBindings.IdOf));
        Assert.DoesNotContain(Enum.GetValues<PlayAction>().Select(KeyBindings.TitleOf), t => t.StartsWith("Give", StringComparison.Ordinal));
        var errors = new List<string>();
        PlayOptions options = PlayOptions.Parse("bind.give_item=G\nbind.flashlight=U\n", errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "G" }, options.Bindings.KeysOf(PlayAction.CraftingMenu));
        PlayOptions taken = PlayOptions.Parse("bind.give_item=L\n", errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { "L" }, taken.Bindings.KeysOf(PlayAction.LanguageMenu)); // it takes no key from today's actions
        Assert.DoesNotContain("give_item", taken.ToText());
        // an unknown action still is an error
        PlayOptions.Parse("bind.give_items=G\n", errors);
        Assert.Single(errors);
        // the three menu keys in the options panel, in the style of the others
        Assert.Equal(("Crafting menu", "Language menu", "Navigate"), (KeyBindings.TitleOf(PlayAction.CraftingMenu), KeyBindings.TitleOf(PlayAction.LanguageMenu), KeyBindings.TitleOf(PlayAction.Navigate)));
        Assert.Equal(("crafting_menu", "language_menu", "navigate"), (KeyBindings.IdOf(PlayAction.CraftingMenu), KeyBindings.IdOf(PlayAction.LanguageMenu), KeyBindings.IdOf(PlayAction.Navigate)));
    }

    [Fact]
    public void TheSettingsANewPlayerStartsFromHaveTheDefaultKeys()
    {
        // D324: a new player starts from godot/default-options.cfg, so its keys are the default layout's
        PlayOptions shipped = PlayOptions.Parse(ShippedDefaults());
        KeyBindings defaults = KeyBindings.Defaults();
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
            Assert.True(defaults.KeysOf(action).SequenceEqual(shipped.Bindings.KeysOf(action)), $"{action}: {string.Join(",", shipped.Bindings.KeysOf(action))}");
    }
}
