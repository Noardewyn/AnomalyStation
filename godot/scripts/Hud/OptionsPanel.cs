using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Speech;
using Anomaly.Client.Shared.World;
using Anomaly.Client.Speech;

namespace Anomaly.Client.Hud;

/// <summary>
/// The Settings window (D70, D53; D282: the client's own options only, in tabs, opened by F10, the Esc menu and the lobby's Settings):
/// Gameplay (the camera, the highlights, the chat translator), Controls (the mouse and the keys: click an action, press the new key;
/// Escape cancels), Graphics (the display, the quality preset, the post-processing), Audio (D296), TTS/STT, Interface (the HUD, the
/// windows and their scales), Accessibility (the colourblind palette of the health HUD, the zone labels and the hatching, the strength
/// limit of fullscreen effects) and Developer, as most games group them (the owner, 2026-11-08). Changes apply at once and are announced with
/// <see cref="Changed"/> so that the scene can save the file. It is moved, resized and closed as every window (<see cref="WindowChrome"/>).
/// </summary>
public partial class OptionsPanel : PanelContainer
{
    private PlayOptions _options;
    private PlayAction? _capturing;
    private readonly Dictionary<PlayAction, Button> _bindButtons = new();
    private Label _hint;
    private Label _ttsStatus;
    private Label _sttStatus;
    private Button _ttsDelete;
    private Button _sttDelete;
    private Label _translationStatus;
    private Button _translationDelete;
    private bool _building;

    /// <summary>An option or a binding changed.</summary>
    public event Action Changed;

    /// <summary>D309: Test pressed; true: as heard over the radio.</summary>
    public event Action<bool> VoiceTested;

    /// <summary>D310: the player asked to delete a model's downloaded files (the scene lets its engine go, then deletes them).</summary>
    public event Action<SpeechModel> ModelDeleteRequested;

    public bool IsOpen => Visible;

    public void Bind(PlayOptions options, WindowChrome chrome)
    {
        _options = options;
        Name = "Settings";
        Visible = false;
        _building = true;
        AddThemeStyleboxOverride("panel", PanelStyle.Dark());
        Theme = MenuStyle.Theme; // the Esc menu's look (the owner, 2026-10-28)
        chrome.MinSize = new Vector2(420, 300);
        var column = new VBoxContainer();
        AddChild(column);
        column.AddChild(MenuStyle.Restyle(chrome.TitleBar("Settings", () => Visible = false)));
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(tabs);
        column.AddChild(chrome.Grip());

        // the owner (2026-11-08): the tabs of most games, each setting in its own one
        VBoxContainer gameplay = Tab(tabs, "Gameplay");
        gameplay.AddChild(Heading("Camera"));
        gameplay.AddChild(Slider("Field of view", PlayOptions.MinFov, PlayOptions.MaxFov, 1, options.FovDegrees, v => options.FovDegrees = (float)v, " deg"));
        gameplay.AddChild(Check("Head bob", options.HeadBob, v => options.HeadBob = v));
        gameplay.AddChild(Check("Camera shake", options.CameraShake, v => options.CameraShake = v));
        gameplay.AddChild(Level("Zero gravity: the eye floats", options.ZeroGBob, v => options.ZeroGBob = v));
        gameplay.AddChild(Level("Zero gravity: the view leans into a glide", options.ZeroGRoll, v => options.ZeroGRoll = v));
        gameplay.AddChild(Check("Third person: shade what your character cannot see", options.ThirdPersonFov, v => options.ThirdPersonFov = v));
        gameplay.AddChild(Slider("Third person free look: face the aim after a shot", 0, PlayOptions.MaxAimFacingSeconds, 0.1, options.AimFacingSeconds, v => options.AimFacingSeconds = (float)v, " s"));
        gameplay.AddChild(Heading("Highlights"));
        gameplay.AddChild(Check("Names over other players' heads", options.PlayerNames, v => options.PlayerNames = v));
        gameplay.AddChild(Check("Outline the floor tile under the crosshair", options.HighlightTile, v => options.HighlightTile = v));
        gameplay.AddChild(Check("Outline the thing under the crosshair", options.HighlightThing, v => options.HighlightThing = v));
        // D319: the chat translator, off until turned on; turning it on downloads the chosen language's models
        gameplay.AddChild(Heading("Chat"));
        gameplay.AddChild(Check("Translate other players' chat lines, the original under the translation (Helsinki-NLP's Opus-MT models, CC BY 4.0)", options.ChatTranslation, v =>
        {
            options.ChatTranslation = v;
            if (v)
                EnsureTranslation(options);
        }));
        var translation = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach ((string code, string name) in ChatTranslation.Targets)
        {
            translation.AddItem(name);
            translation.SetItemMetadata(translation.ItemCount - 1, code);
            if (code == options.ChatTranslationLanguage)
                translation.Selected = translation.ItemCount - 1;
        }
        translation.ItemSelected += index => Apply(() =>
        {
            options.ChatTranslationLanguage = translation.GetItemMetadata((int)index).AsString();
            if (options.ChatTranslation)
                EnsureTranslation(options);
        });
        gameplay.AddChild(Row("Translate the chat into", translation));
        gameplay.AddChild(_translationStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _translationDelete = new Button { Text = "Delete the downloaded translation files", FocusMode = FocusModeEnum.None, Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _translationDelete.Pressed += () =>
        {
            foreach (SpeechModel model in ChatTranslation.Models.Values.Where(SpeechDownloads.CanDelete))
                ModelDeleteRequested?.Invoke(model);
        };
        gameplay.AddChild(_translationDelete);

        VBoxContainer controls = Tab(tabs, "Controls");
        controls.AddChild(Slider("Mouse sensitivity", 1, 60, 1, options.MouseSensitivity * 10000, v => options.MouseSensitivity = (float)(v / 10000.0), string.Empty));
        controls.AddChild(Heading("Keys: click an action, then press a key or a mouse button"));
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = KeyBindings.TitleOf(action), CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart });
            var button = new Button { CustomMinimumSize = new Vector2(200, 0), Text = KeysText(action) };
            PlayAction captured = action;
            button.Pressed += () => BeginCapture(captured);
            row.AddChild(button);
            _bindButtons[action] = button;
            controls.AddChild(row);
        }
        _hint = new Label { Text = string.Empty };
        controls.AddChild(_hint);
        var defaults = new Button { Text = "Default keys", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        defaults.Pressed += () =>
        {
            _options.Bindings = KeyBindings.Defaults();
            RefreshBindings();
            Changed?.Invoke();
        };
        controls.AddChild(defaults);

        // D288: a preset sets every setting under it; changing one of them makes the player's own set, custom
        VBoxContainer graphics = Tab(tabs, "Graphics");
        graphics.AddChild(Heading("Display"));
        graphics.AddChild(Row("Fullscreen resolution", Resolutions(options)));
        graphics.AddChild(Heading("Quality"));
        string[] qualityNames = RenderPreset.All.Select(p => p.Name).Append(RenderPreset.CustomName).ToArray();
        var quality = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string name in qualityNames)
            quality.AddItem(name);
        quality.Selected = Math.Max(0, Array.IndexOf(qualityNames, options.Quality));
        graphics.AddChild(Row("Preset", quality));
        var picks = new List<(GraphicsSetting Setting, OptionButton Pick)>();
        void ShowSettings()
        {
            foreach ((GraphicsSetting setting, OptionButton pick) in picks)
                pick.Selected = Math.Max(0, Array.FindIndex(setting.Choices, c => c.Value == setting.Get(options.Graphics)));
        }
        foreach (GraphicsSetting setting in GraphicsSetting.All)
        {
            var pick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach ((string label, int _) in setting.Choices)
                pick.AddItem(label);
            pick.ItemSelected += index => Apply(() =>
            {
                options.SetGraphics(setting, setting.Choices[(int)index].Value);
                quality.Selected = qualityNames.Length - 1;
            });
            picks.Add((setting, pick));
            graphics.AddChild(Row(setting.Title, pick));
        }
        ShowSettings();
        quality.ItemSelected += index => Apply(() =>
        {
            options.Quality = qualityNames[(int)index];
            ShowSettings();
        });
        // D298: the post-processing look, a style beside the preset
        graphics.AddChild(Heading("Post-processing"));
        graphics.AddChild(Choice("Tonemapping", PostLook.Tonemaps, options.Tonemap, v => options.Tonemap = v));
        graphics.AddChild(Slider("Saturation", 0, 200, 5, options.SaturationPercent, v => options.SaturationPercent = (int)v, "%"));
        graphics.AddChild(Slider("Contrast", 50, 150, 5, options.ContrastPercent, v => options.ContrastPercent = (int)v, "%"));
        graphics.AddChild(Slider("Cool shadows, warm lights", 0, 100, 5, options.SplitTonePercent, v => options.SplitTonePercent = (int)v, "%"));
        graphics.AddChild(Choice("Darkness in the distance", PostLook.Fogs, options.FogTiles, v => options.FogTiles = v));
        graphics.AddChild(Slider("Film grain", 0, 100, 5, options.GrainPercent, v => options.GrainPercent = (int)v, "%"));
        graphics.AddChild(Slider("Vignette", 0, 100, 5, options.VignettePercent, v => options.VignettePercent = (int)v, "%"));
        graphics.AddChild(Slider("Bloom strength", 0, 300, 10, options.BloomPercent, v => options.BloomPercent = (int)v, "%"));
        graphics.AddChild(Choice("Bloom threshold", PostLook.BloomThresholds, options.BloomThresholdPercent, v => options.BloomThresholdPercent = v));
        graphics.AddChild(Choice("Edge outlines", PostLook.Outlines, options.OutlineWidth, v => options.OutlineWidth = v));
        graphics.AddChild(Slider("Edge outline strength", 0, 100, 5, options.OutlinePercent, v => options.OutlinePercent = (int)v, "%"));

        VBoxContainer audio = Tab(tabs, "Audio");
        audio.AddChild(Slider("Master", 0, 100, 5, options.MasterVolumePercent, v => options.MasterVolumePercent = (int)v, "%"));
        audio.AddChild(Slider("Effects (steps, doors, weapons, machines)", 0, 100, 5, options.EffectsVolumePercent, v => options.EffectsVolumePercent = (int)v, "%"));
        audio.AddChild(Slider("Ambience (the area's tracks and odd noises)", 0, 100, 5, options.AmbienceVolumePercent, v => options.AmbienceVolumePercent = (int)v, "%"));
        audio.AddChild(Slider("Ship hum", 0, 100, 5, options.ShipHumVolumePercent, v => options.ShipHumVolumePercent = (int)v, "%"));
        audio.AddChild(Slider("Music", 0, 100, 5, options.MusicVolumePercent, v => options.MusicVolumePercent = (int)v, "%"));
        audio.AddChild(Slider("Interface", 0, 100, 5, options.InterfaceVolumePercent, v => options.InterfaceVolumePercent = (int)v, "%"));
        audio.AddChild(Check("Room echo (reverb)", options.Reverb, v => options.Reverb = v));

        // D307: both are off until the player turns them on; turning one on downloads its model
        VBoxContainer speech = Tab(tabs, "TTS/STT");
        speech.AddChild(Heading("Text to speech"));
        speech.AddChild(Check("Text to speech: voice what characters say", options.TextToSpeech, v =>
        {
            options.TextToSpeech = v;
            if (v)
                SpeechDownloads.Ensure(SpeechModel.Tts);
        }));
        speech.AddChild(_ttsStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart });
        speech.AddChild(_ttsDelete = DeleteButton(SpeechModel.Tts));
        speech.AddChild(Slider("Voices volume", 0, 100, 5, options.SpeechVolumePercent, v => options.SpeechVolumePercent = (int)v, "%"));
        // D308: tg's radio TTS preferences
        var radio = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        radio.AddItem("All channels", (int)RadioSpeech.All);
        radio.AddItem("Departmental channels only", (int)RadioSpeech.Departmental);
        radio.AddItem("Off", (int)RadioSpeech.Off);
        radio.Selected = (int)options.RadioSpeech;
        radio.ItemSelected += index => Apply(() => options.RadioSpeech = (RadioSpeech)(int)index);
        speech.AddChild(Row("Voice the radio", radio));
        speech.AddChild(Slider("Radio voices volume", 0, 100, 5, options.RadioSpeechVolumePercent, v => options.RadioSpeechVolumePercent = (int)v, "%"));
        speech.AddChild(Check("Hear your own radio lines", options.HearOwnRadio, v => options.HearOwnRadio = v));
        // D309: hear the voice and the radio's effect; the voice and its pitch are the character's (Character Setup, D311)
        speech.AddChild(new Label { Text = "Test (your character's voice once it has spoken, else a sample voice)", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var tests = new HBoxContainer();
        var test = new Button { Text = "Test voice", FocusMode = FocusModeEnum.None };
        test.Pressed += () => VoiceTested?.Invoke(false);
        var testRadio = new Button { Text = "Test over the radio", FocusMode = FocusModeEnum.None };
        testRadio.Pressed += () => VoiceTested?.Invoke(true);
        tests.AddChild(test);
        tests.AddChild(testRadio);
        speech.AddChild(tests);
        speech.AddChild(Heading("Speech to text"));
        speech.AddChild(Check("Speech to text: hold the push to talk key and speak to type", options.SpeechToText, v =>
        {
            options.SpeechToText = v;
            if (v)
                SpeechDownloads.Ensure(SpeechModel.Stt);
        }));
        speech.AddChild(_sttStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart });
        speech.AddChild(_sttDelete = DeleteButton(SpeechModel.Stt));
        // D313: speech to text in one chosen language, or the closest picked one
        _recognition = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _recognition.ItemSelected += index => Apply(() => options.SpeechToTextLanguage = _recognition.GetItemMetadata((int)index).AsString());
        speech.AddChild(Row("Speech to text language", _recognition));
        speech.AddChild(Heading("Languages"));
        speech.AddChild(new Label { Text = "Languages to voice and recognize (a line in another language is not voiced)", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var languages = new GridContainer { Columns = 3 };
        foreach ((string code, string name) in SpeechLanguages.All)
        {
            var pick = new CheckBox { Text = name, ButtonPressed = options.SpeechLanguageCodes.Contains(code) };
            pick.Toggled += on => Apply(() =>
            {
                options.SpeechLanguageCodes.Remove(code);
                if (on)
                    options.SpeechLanguageCodes.Add(code);
                FillRecognitionLanguages(options);
            });
            languages.AddChild(pick);
        }
        speech.AddChild(languages);
        FillRecognitionLanguages(options);

        VBoxContainer ui = Tab(tabs, "Interface");
        ui.AddChild(Slider("Crosshair size", 50, 400, 10, options.CrosshairScalePercent, v => options.CrosshairScalePercent = (int)v, "%"));
        ui.AddChild(Check("Minimap (in cursor mode: drag the map to look around, its edge to move it, the wheel zooms)", options.Minimap, v => options.Minimap = v)); // D315
        ui.AddChild(Slider("Minimap size", 50, 300, 10, options.MinimapSizePercent, v => options.MinimapSizePercent = (int)v, "%"));
        ui.AddChild(Slider("Minimap text size", 50, 400, 10, options.MinimapTextPercent, v => options.MinimapTextPercent = (int)v, "%"));
        ui.AddChild(Slider("HUD scale", 50, 200, 5, options.UiScalePercent, v => options.UiScalePercent = (int)v, "%"));
        ui.AddChild(Slider("Windows and tooltips scale (Ctrl+=, Ctrl+-)", PlayOptions.MinWindowScale, PlayOptions.MaxWindowScale, 5, options.WindowScalePercent, v => options.WindowScalePercent = (int)v, "%"));
        ui.AddChild(Slider("Chat window scale", PlayOptions.MinWindowScale, PlayOptions.MaxWindowScale, 5, options.ChatScalePercent, v => options.ChatScalePercent = (int)v, "%"));
        ui.AddChild(Slider("Chat lines over the view scale", PlayOptions.MinChatLinesScale, PlayOptions.MaxChatLinesScale, 5, options.ChatLinesScalePercent, v => options.ChatLinesScalePercent = (int)v, "%"));
        ui.AddChild(Slider("Windows and chat opacity", 10, 100, 5, options.WindowOpacityPercent, v => options.WindowOpacityPercent = (int)v, "%"));
        ui.AddChild(Slider("HUD text opacity (top right corner, camera mode)", 10, 100, 5, options.HudTextOpacityPercent, v => options.HudTextOpacityPercent = (int)v, "%"));
        ui.AddChild(Slider("Radial menu opacity", 10, 100, 5, options.RadialOpacityPercent, v => options.RadialOpacityPercent = (int)v, "%"));

        VBoxContainer access = Tab(tabs, "Accessibility");
        var palette = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        palette.AddItem("tg's own", (int)ColorblindMode.Off);
        palette.AddItem("Colourblind: red-green", (int)ColorblindMode.RedGreen);
        palette.AddItem("Colourblind: blue-yellow", (int)ColorblindMode.BlueYellow);
        palette.AddItem("Colourblind: grey", (int)ColorblindMode.Mono);
        palette.Selected = (int)options.Colorblind;
        palette.ItemSelected += index => Apply(() => options.Colorblind = (ColorblindMode)(int)index);
        access.AddChild(Row("Health HUD colours", palette));
        access.AddChild(Check("Health doll: text label per zone", options.ZoneLabels, v => options.ZoneLabels = v));
        access.AddChild(Check("Health doll: hatching by level", options.HatchCues, v => options.HatchCues = v));
        access.AddChild(Slider("Fullscreen effects strength", 0, 100, 5, options.FullscreenStrengthPercent, v => options.FullscreenStrengthPercent = (int)v, "%"));

        VBoxContainer developer = Tab(tabs, "Developer");
        developer.AddChild(Check("Port status over things (green, yellow, red)", options.PortStatus, v => options.PortStatus = v));
        developer.AddChild(Slider("Dev windows scale", 50, 400, 10, options.DevUiScalePercent, v => options.DevUiScalePercent = (int)v, "%"));

        Vector2 screen = GetViewportRect().Size;
        var size = new Vector2(600, 620);
        chrome.Place((screen - size) / 2f, size);
        _building = false;
    }

    // A tab of the window: its page scrolls when the window is smaller than it.
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new WheelScrollContainer { Name = title.Replace('/', ' '), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; // a node name has no '/'
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        scroll.AddChild(margin);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 6);
        margin.AddChild(box);
        tabs.AddChild(scroll);
        tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
        return box;
    }

    public void Toggle() => Visible = !Visible;

    public override void _Process(double delta)
    {
        if (!Visible || _ttsStatus == null)
            return;
        _ttsStatus.Text = SpeechDownloads.Status(SpeechModel.Tts);
        _sttStatus.Text = SpeechDownloads.Status(SpeechModel.Stt);
        // D310: a model's files can go once its option is off
        _ttsDelete.Visible = !_options.TextToSpeech && SpeechDownloads.CanDelete(SpeechModel.Tts);
        _sttDelete.Visible = !_options.SpeechToText && SpeechDownloads.CanDelete(SpeechModel.Stt);
        _translationStatus.Text = string.Join('\n', ChatTranslation.ModelsFor(_options.ChatTranslationLanguage).Select(SpeechDownloads.Status));
        _translationDelete.Visible = !_options.ChatTranslation && ChatTranslation.Models.Values.Any(SpeechDownloads.CanDelete);
    }

    private static void EnsureTranslation(PlayOptions options)
    {
        foreach (SpeechModel model in ChatTranslation.ModelsFor(options.ChatTranslationLanguage))
            SpeechDownloads.Ensure(model);
    }

    private Button DeleteButton(SpeechModel model)
    {
        var button = new Button { Text = $"Delete the downloaded {model.Title} files", FocusMode = FocusModeEnum.None, Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        button.Pressed += () => ModelDeleteRequested?.Invoke(model);
        return button;
    }

    public override void _Input(InputEvent @event)
    {
        if (_capturing == null || !Visible)
            return;
        string name = null;
        if (@event is InputEventKey { Echo: false, PhysicalKeycode: Key.Shift } shift)
        {
            // M9 slice C: Shift held with a key binds the pair (Shift_T); Shift let go alone binds Shift
            if (shift.Pressed)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
            name = "Shift";
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.PhysicalKeycode == Key.Escape)
            {
                EndCapture("Cancelled.");
                GetViewport().SetInputAsHandled();
                return;
            }
            name = KeyBindings.NameOf(key.PhysicalKeycode.ToString(), key.ShiftPressed);
        }
        else if (@event is InputEventMouseButton { Pressed: true } mouse)
        {
            name = mouse.ButtonIndex switch
            {
                MouseButton.Left => "MouseLeft",
                MouseButton.Right => "MouseRight",
                MouseButton.Middle => "MouseMiddle",
                _ => null,
            };
        }
        if (name == null || !KeyBindings.IsValidKeyName(name))
            return;
        PlayAction action = _capturing.Value;
        PlayAction? displaced = _options.Bindings.Bind(action, name);
        EndCapture(displaced.HasValue ? $"{name} now does '{KeyBindings.TitleOf(action)}'; '{KeyBindings.TitleOf(displaced.Value)}' is unbound." : $"{name} bound.");
        RefreshBindings();
        Changed?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    private void BeginCapture(PlayAction action)
    {
        _capturing = action;
        _bindButtons[action].Text = "press a key...";
        _hint.Text = $"Press the new key for '{KeyBindings.TitleOf(action)}' (Escape cancels).";
    }

    private void EndCapture(string message)
    {
        _capturing = null;
        _hint.Text = message;
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        foreach (var pair in _bindButtons)
            pair.Value.Text = KeysText(pair.Key);
    }

    private string KeysText(PlayAction action)
    {
        IReadOnlyList<string> keys = _options.Bindings.KeysOf(action);
        return keys.Count == 0 ? "(unbound)" : string.Join(" / ", keys);
    }

    private void Apply(Action change)
    {
        if (_building)
            return;
        change();
        Changed?.Invoke();
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color(0.75f, 0.9f, 1f));
        return label;
    }

    // a switch, which reads as on or off on the dark panel (the theme's empty check box hardly shows there)
    private CheckButton Check(string text, bool value, Action<bool> set)
    {
        var box = new CheckButton { Text = text, ButtonPressed = value };
        box.Toggled += pressed => Apply(() => set(pressed));
        return box;
    }

    // a labelled control on a row
    private OptionButton _recognition;

    // D313: Auto, then each picked language; a chosen language no longer picked shows as Auto (PlayOptions.RecognitionLanguages)
    private void FillRecognitionLanguages(PlayOptions options)
    {
        _recognition.Clear();
        _recognition.AddItem("Auto (the closest of the picked languages)");
        _recognition.SetItemMetadata(0, "");
        _recognition.Selected = 0;
        foreach ((string code, string name) in SpeechLanguages.All.Where(l => options.SpeechLanguageCodes.Contains(l.Code)))
        {
            _recognition.AddItem(name);
            _recognition.SetItemMetadata(_recognition.ItemCount - 1, code);
            if (code == options.SpeechToTextLanguage)
                _recognition.Selected = _recognition.ItemCount - 1;
        }
    }

    private static Control Row(string text, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(220, 0) });
        row.AddChild(control);
        return row;
    }

    // a setting of a few named values on a row (D298)
    private Control Choice(string text, (string Label, int Value)[] choices, int value, Action<int> set)
    {
        var pick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach ((string label, int _) in choices)
            pick.AddItem(label);
        pick.Selected = Math.Max(0, Array.FindIndex(choices, c => c.Value == value));
        pick.ItemSelected += index => Apply(() => set(choices[(int)index].Value));
        return Row(text, pick);
    }

    // Godot lists no monitor modes, so the common ones that fit the monitor (the owner, 2026-10-30)
    private static readonly (int Width, int Height)[] CommonResolutions =
    {
        (3840, 2160), (2560, 1600), (2560, 1440), (1920, 1200), (1920, 1080), (1680, 1050), (1600, 900), (1440, 900), (1366, 768), (1280, 800), (1280, 720),
    };

    // The fullscreen resolution: the monitor's own first, then the common ones below it (and a saved one that is not common).
    private OptionButton Resolutions(PlayOptions options)
    {
        Vector2I screen = DisplayServer.ScreenGetSize();
        var choices = new List<(int Width, int Height)> { (0, 0) };
        choices.AddRange(CommonResolutions.Where(r => r.Width <= screen.X && r.Height <= screen.Y && (r.Width, r.Height) != (screen.X, screen.Y)));
        if (!choices.Contains(options.FullscreenResolution))
            choices.Add(options.FullscreenResolution);
        var pick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach ((int width, int height) in choices)
            pick.AddItem(width == 0 ? $"Monitor's own ({screen.X} x {screen.Y})" : $"{width} x {height}");
        pick.Selected = choices.IndexOf(options.FullscreenResolution);
        pick.ItemSelected += index => Apply(() => options.FullscreenResolution = choices[(int)index]);
        return pick;
    }

    // M4c slice 7 (D70): a zero-gravity camera effect's level, off, reduced or on.
    private Control Level(string text, ZeroGLevel value, Action<ZeroGLevel> set)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(300, 0) });
        var pick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pick.AddItem("off", (int)ZeroGLevel.Off);
        pick.AddItem("reduced", (int)ZeroGLevel.Reduced);
        pick.AddItem("on", (int)ZeroGLevel.On);
        pick.Selected = (int)value;
        pick.ItemSelected += index => Apply(() => set((ZeroGLevel)(int)index));
        row.AddChild(pick);
        return row;
    }

    private Control Slider(string text, double min, double max, double step, double value, Action<double> set, string unit)
    {
        var row = new HBoxContainer();
        var label = new Label { CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; // the label and the slider share the row
        string Format(double v) => text + ": " + v.ToString(max <= 60 && step < 1 ? "0.0" : "0", CultureInfo.InvariantCulture) + unit;
        label.Text = Format(value);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(240, 24), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        slider.ValueChanged += v =>
        {
            label.Text = Format(v);
            Apply(() => set(v));
        };
        row.AddChild(label);
        row.AddChild(slider);
        return row;
    }
}

/// <summary>
/// The crosshair in the middle of the screen: a dot that opens into four ticks over a mob (D49: the ray from the camera), greener when it is within reach.
/// DV96 (the owner, 2026-11-21): with a gun in hand (protocol 92's GunSight) it is a shooter's crosshair, four lines as far apart as the gun's cone
/// looks (<see cref="GunSightGap"/>), opened by each own shot, and a hit marker when one of the own shots hits a living thing.
/// </summary>
public partial class Crosshair : Control
{
    private const double HitSeconds = 0.25;
    private bool _gun;
    private float _cone, _fov = 75f;
    private double _firedAt = -10, _hitAt = -10;
    private bool _target;
    private bool _reach;
    private double _pulseUntil = -1, _now;
    private float _scale = 1f;

    /// <summary>The crosshair's size as a multiple of its default (the options' crosshair size).</summary>
    public void SetScale(float scale)
    {
        if (Mathf.IsEqualApprox(scale, _scale))
            return;
        _scale = scale;
        QueueRedraw();
    }

    /// <summary>M4a P7: a click was sent: a short ring (local feedback, nothing is predicted).</summary>
    public void PulseNow(double now)
    {
        _pulseUntil = now + 0.16;
        QueueRedraw();
    }

    /// <summary>M4a P7: call every frame so that the ring animates and ends.</summary>
    public void Animate(double now)
    {
        _now = now;
        if (now < _pulseUntil + 0.02 || now - _firedAt < GunSightGap.BloomSeconds + 0.02 || now - _hitAt < HitSeconds + 0.02)
            QueueRedraw();
    }

    /// <summary>DV96: whether a gun is held, its cone (degrees off the aim) and the camera's vertical field of view.</summary>
    public void SetGun(bool gun, float coneDegrees, float fovDegrees)
    {
        if (gun == _gun && Mathf.IsEqualApprox(coneDegrees, _cone) && Mathf.IsEqualApprox(fovDegrees, _fov))
            return;
        (_gun, _cone, _fov) = (gun, coneDegrees, fovDegrees);
        QueueRedraw();
    }

    /// <summary>DV96: the own gun fired: the lines open and close again.</summary>
    public void Fired(double now)
    {
        _firedAt = now;
        QueueRedraw();
    }

    /// <summary>DV96: one of the own shots hit a living thing: the hit marker shows.</summary>
    public void Hit(double now)
    {
        _hitAt = now;
        QueueRedraw();
    }

    public void Set(bool target, bool inReach)
    {
        if (target == _target && inReach == _reach)
            return;
        _target = target;
        _reach = inReach;
        QueueRedraw();
    }

    public override void _Ready()
    {
        Name = "Crosshair";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetViewport().SizeChanged += QueueRedraw;
    }

    public override void _Draw()
    {
        // The middle of the window, whatever the size of this control is at the moment.
        Vector2 c = GetViewportRect().Size / 2f;
        DrawSetTransform(c, 0f, new Vector2(_scale, _scale));
        c = Vector2.Zero;
        Color color = !_target ? new Color(1, 1, 1, 0.7f) : _reach ? new Color(0.45f, 1f, 0.5f, 0.95f) : new Color(1f, 0.8f, 0.35f, 0.95f);
        var shadow = new Color(0, 0, 0, 0.6f);
        if (_gun)
        {
            DrawGun(color, shadow);
            return;
        }
        DrawCircle(c, 3.5f, shadow);
        DrawCircle(c, 2f, color);
        if (_now < _pulseUntil)
        {
            float k = 1f - (float)((_pulseUntil - _now) / 0.16);
            DrawArc(c, 9f + 14f * k, 0, Mathf.Tau, 32, new Color(1f, 1f, 0.7f, 1f - k), 3f);
        }
        if (_target)
        {
            foreach (Vector2 d in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            {
                DrawLine(c + d * 8f, c + d * 15f, shadow, 4f);
                DrawLine(c + d * 8f, c + d * 15f, color, 2f);
            }
        }
    }

    // DV96: four lines out from the gap (in screen pixels: the drawing is scaled by the options' size, the gap is not), a small dot, and the hit
    // marker's four diagonal ticks, red and fading
    private void DrawGun(Color color, Color shadow)
    {
        float gap = GunSightGap.Pixels(_cone, _fov, GetViewportRect().Size.Y, _now - _firedAt) / _scale;
        const float length = 8f;
        foreach (Vector2 d in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
        {
            DrawLine(d * gap, d * (gap + length), shadow, 4f);
            DrawLine(d * gap, d * (gap + length), color, 2f);
        }
        DrawCircle(Vector2.Zero, 2f, shadow);
        DrawCircle(Vector2.Zero, 1f, color);
        double since = _now - _hitAt;
        if (since < 0 || since >= HitSeconds)
            return;
        var hit = new Color(1f, 0.25f, 0.2f, 1f - (float)(since / HitSeconds));
        foreach (Vector2 d in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            Vector2 n = d.Normalized();
            DrawLine(n * 6f, n * 13f, new Color(0, 0, 0, hit.A * 0.6f), 4f);
            DrawLine(n * 6f, n * 13f, hit, 2f);
        }
    }
}
