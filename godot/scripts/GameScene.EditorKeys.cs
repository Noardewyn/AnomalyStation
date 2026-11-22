using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Shared.Editor;

namespace Anomaly.Client;

// The map editor's keys (docs/ai/specs/map-editor.md 5.16). Every function of the editor has an id, and one table (HotkeyMap) says which key
// starts it: a key pressed over the view is looked up there and the function is run (EditorDo), a menu's item and a tool bar's button run the
// same function, and the Hotkeys window (Edit, Hotkeys) changes the table, which the editor's settings keep. Fixed: Esc (a paste or a drag is
// given up, then the tool, then the selection), the mouse's buttons with their modifiers, and F8 in a played round.
public partial class GameScene
{
    private const string CameraKeys = "Camera (while the key is held)";

    // the functions that go on while their key is held down
    private static readonly HashSet<string> EditorRepeats = new(StringComparer.Ordinal) { "edit.undo", "edit.redo", "level.up", "level.down" };

    private HotkeyMap _editorKeys;
    private Dictionary<string, int> _editorActionIds; // a function's place in the table: its item's id in a menu
    private int _editorKeysVersion; // counts the table's changes: the settings' file follows it
    private readonly List<(PopupMenu Menu, string Action)> _keyedItems = new(); // the menus' items that run a function
    private readonly List<(Control Control, Func<string> Tip, string Action)> _keyedTips = new(); // the controls whose tip names their function's keys
    private AcceptDialog _keysDialog;
    private Tree _keysTree;
    private Label _keysNote;
    private Button _keysSet, _keysNone, _keysDefault;
    private string _keysCatching; // the function whose key the Hotkeys window waits for

    // Every function a key can start, in the order the Hotkeys window lists them, with the keys it starts with. The command key is Command on a
    // Mac and Ctrl elsewhere.
    private static List<HotkeyAction> EditorActions()
    {
        bool mac = OS.HasFeature("macos");
        KeyCombo Cmd(Key key, bool shift = false) => new((int)key, Ctrl: !mac, Shift: shift, Meta: mac);
        static KeyCombo On(Key key, bool shift = false) => new((int)key, Shift: shift);
        var all = new List<HotkeyAction>();
        void Add(string section, string id, string title, params KeyCombo[] keys) => all.Add(new HotkeyAction(id, section, title, keys));
        void Hold(string id, string title, Key key) => all.Add(new HotkeyAction(id, CameraKeys, title, new[] { new KeyCombo((int)key) }, Held: true));

        Add("File", "file.new", "New map", Cmd(Key.N));
        Add("File", "file.newshuttle", "New shuttle");
        Add("File", "file.open", "Open", Cmd(Key.O));
        Add("File", "file.save", "Save", Cmd(Key.S));
        Add("File", "file.saveas", "Save as", Cmd(Key.S, shift: true));
        Add("File", "file.import", "Import a .dmm map");
        Add("File", "file.package", "Export the map's package");
        Add("File", "file.quit", "Quit", Cmd(Key.Q));

        Add("Edit", "edit.undo", "Undo", Cmd(Key.Z));
        Add("Edit", "edit.redo", "Redo", Cmd(Key.Z, shift: true), Cmd(Key.Y));
        Add("Edit", "edit.cut", "Cut", Cmd(Key.X));
        Add("Edit", "edit.copy", "Copy", Cmd(Key.C));
        Add("Edit", "edit.paste", "Paste", Cmd(Key.V));
        Add("Edit", "edit.delete", "Delete what is selected", On(Key.Delete), On(Key.Backspace));
        Add("Edit", "edit.turn", "Turn right: what is selected, or what the place tool places", On(Key.R));
        Add("Edit", "edit.turnback", "Turn left", On(Key.R, shift: true));
        Add("Edit", "edit.find", "Find on the map", Cmd(Key.F));
        Add("Edit", "edit.keys", "Hotkeys");

        for (int i = 0; i < EditorToolNames.Length; i++)
            Add("Tools", "tool." + EditorToolNames[i].ToLowerInvariant(), "Tool: " + EditorToolNames[i], On(Key.Key1 + i));
        Add("Tools", "mode.next", "Place mode: the next one", On(Key.Tab));
        for (int i = 0; i < EditorPlaceModes.Length; i++)
            Add("Tools", "mode." + ((EditorPlaceMode)i).ToString().ToLowerInvariant(), "Place mode: " + EditorPlaceModes[i].Name);
        for (int i = 0; i < EditorShapes.Length; i++)
            Add("Tools", "shape." + ((MapShape)i).ToString().ToLowerInvariant(), "Shape: " + EditorShapes[i].Name);

        // (+ is Shift with = on most keyboards: both are the key)
        Add("Levels", "level.up", "Level up", On(Key.Equal), On(Key.Equal, shift: true), On(Key.KpAdd), On(Key.Pageup), On(Key.Period));
        Add("Levels", "level.down", "Level down", On(Key.Minus), On(Key.KpSubtract), On(Key.Pagedown), On(Key.Comma));
        Add("Levels", "level.lock", "Lock the tools to the edited level", On(Key.L));
        Add("Levels", "level.above", "Show the levels above");

        Add("View", "view.focus", "Focus on the selection", On(Key.F));
        Add("View", "view.home", "Go to the map's first tile", On(Key.Home));
        Add("View", "view.top", "Top view", On(Key.T));
        Add("View", "view.simple", "Simple look");
        for (int i = 0; i < EditorLookParts.Length; i++)
            Add("View", "look." + LookKey(i), "Look: " + EditorLookParts[i].Name);
        Add("View", "view.under", "T-ray: under the floor");
        Add("View", "view.markers", "Markers");
        Add("View", "view.areas", "Areas");
        Add("View", "view.border", "Map border");
        Add("View", "view.docks", "Shuttle docks");
        Add("View", "view.preview", "Preview of what the place tool places");
        Add("View", "view.arrows", "Arrows: the way things face");
        foreach (string title in EditorPanels)
            Add("View", PanelAction(title), "Panel: " + title);
        Add("View", "play.start", "Play the map", On(Key.F5));

        Hold("cam.forward", "Fly forward", Key.W);
        Hold("cam.back", "Fly back", Key.S);
        Hold("cam.left", "Fly left", Key.A);
        Hold("cam.right", "Fly right", Key.D);
        Hold("cam.up", "Fly up (the top view: a smaller scale)", Key.E);
        Hold("cam.down", "Fly down (the top view: a larger scale)", Key.Q);
        Hold("cam.fast", "Fly faster", Key.Shift);
        return all;
    }

    // The table is made with its defaults and takes what the settings' file changed.
    private void ReadEditorKeys()
    {
        _editorKeys = new HotkeyMap(EditorActions());
        foreach ((string first, string second) in _editorKeys.Clashes())
            GD.PrintErr($"[anomaly] editor: the keys' defaults give one key to {first} and {second}");
        _editorActionIds = _editorKeys.Actions.Select((action, index) => (action.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
        _editorKeys.Load(_editorSettings.Section("keys"));
    }

    // A key with its modifiers, as the interface names it.
    private static string KeyName(KeyCombo combo)
    {
        bool mac = OS.HasFeature("macos");
        string key = (Key)combo.Key switch
        {
            Key.Equal => "=",
            Key.Minus => "-",
            Key.Period => ".",
            Key.Comma => ",",
            Key.Slash => "/",
            Key.Backslash => "\\",
            Key.Semicolon => ";",
            Key.Apostrophe => "'",
            Key.Bracketleft => "[",
            Key.Bracketright => "]",
            Key.Quoteleft => "`",
            Key.KpAdd => "Num +",
            Key.KpSubtract => "Num -",
            Key.KpMultiply => "Num *",
            Key.KpDivide => "Num /",
            Key.Pageup => "Page Up",
            Key.Pagedown => "Page Down",
            Key.Meta => mac ? "Command" : "Meta",
            Key.Alt => mac ? "Option" : "Alt",
            Key other => OS.GetKeycodeString(other),
        };
        return (combo.Ctrl ? "Ctrl + " : string.Empty) + (combo.Alt ? (mac ? "Option + " : "Alt + ") : string.Empty) + (combo.Shift ? "Shift + " : string.Empty)
            + (combo.Meta ? (mac ? "Command + " : "Meta + ") : string.Empty) + key;
    }

    // A key by its name in a console line: cmd+shift+z, ctrl+s, f5, equal, pageup (the engine's names of the keys), or as the settings' file has it.
    private static KeyCombo? KeyFromText(string text)
    {
        if (KeyCombo.Parse(text) is { } stored)
            return stored;
        string[] parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !Enum.TryParse(parts[^1], ignoreCase: true, out Key key) || key == Key.None || int.TryParse(parts[^1], out _))
            return null;
        bool mac = OS.HasFeature("macos");
        var combo = new KeyCombo((int)key);
        foreach (string modifier in parts.Take(parts.Length - 1))
        {
            switch (modifier.ToLowerInvariant())
            {
                case "ctrl": combo = combo with { Ctrl = true }; break;
                case "shift": combo = combo with { Shift = true }; break;
                case "alt": combo = combo with { Alt = true }; break;
                case "meta": combo = combo with { Meta = true }; break;
                case "cmd": combo = mac ? combo with { Meta = true } : combo with { Ctrl = true }; break;
                default: return null;
            }
        }
        return combo;
    }

    // The keys of a function, named; empty when it has none.
    private string KeysText(string action) => string.Join(", ", _editorKeys.Of(action).Select(KeyName));

    // A function's first key, named, for a line of text ("R turns it").
    private string KeyOf(string action) => _editorKeys.Of(action) is { Count: > 0 } keys ? KeyName(keys[0]) : "(no key)";

    // The line a tip ends with: its function's keys.
    private string KeyLine(string action) => _editorKeys.Of(action).Count == 0 ? string.Empty : "\nKey: " + KeysText(action);

    private string ActionTitle(string action) => _editorKeys.Actions[_editorActionIds[action]].Title;

    // A control whose tip ends with its function's keys, as they are now.
    private T Keyed<T>(T control, string action, Func<string> tip) where T : Control
    {
        _keyedTips.Add((control, tip, action));
        control.TooltipText = tip() + KeyLine(action);
        return control;
    }

    // A menu's item that runs a function: its id in the menu is the function's place in the table.
    private void KeyedItem(PopupMenu menu, string action, string text, bool check = false)
    {
        if (check)
            menu.AddCheckItem(text, _editorActionIds[action]);
        else
            menu.AddItem(text, _editorActionIds[action]);
        _keyedItems.Add((menu, action));
    }

    private int ItemIndex(PopupMenu menu, string action) => menu.GetItemIndex(_editorActionIds[action]);

    // The interface names the keys as they are now: beside the menus' items and in the tips. A menu shows its item's key and does not take it: the
    // keys are the view's, so that a field that is typed in keeps its letters, and a menu that took one too would run the function twice.
    private void ShowEditorKeys()
    {
        foreach ((PopupMenu menu, string action) in _keyedItems)
        {
            int index = ItemIndex(menu, action);
            Shortcut shown = null;
            if (_editorKeys.Of(action) is { Count: > 0 } keys)
            {
                KeyCombo key = keys[0];
                var pressed = new InputEventKey { Keycode = (Key)key.Key, CtrlPressed = key.Ctrl, ShiftPressed = key.Shift, AltPressed = key.Alt, MetaPressed = key.Meta };
                shown = new Shortcut { Events = new Godot.Collections.Array { pressed } };
            }
            menu.SetItemShortcut(index, shown);
            menu.SetItemShortcutDisabled(index, true);
        }
        foreach ((Control control, Func<string> tip, string action) in _keyedTips)
            control.TooltipText = tip() + KeyLine(action);
    }

    // A key pressed over the view.
    private void EditorKey(InputEventKey key)
    {
        if (key.PhysicalKeycode == Key.Escape)
        {
            if (key.Echo)
                return;
            // a paste or a drag that is under way is given up first, then the tool, then the selection
            if (_editorPasting || EditorDragging)
                (_editorPasting, _editorDragFrom, _editorBandFrom, _editorMoveFrom) = (false, null, null, null);
            else if (_editorTool != EditorTool.Select)
                _editorTool = EditorTool.Select;
            else
                (_editorSelected, _editorRegion) = (null, null);
            return;
        }
        string action = _editorKeys.Find(new KeyCombo((int)key.PhysicalKeycode, key.CtrlPressed, key.ShiftPressed, key.AltPressed, key.MetaPressed));
        if (action != null && (!key.Echo || EditorRepeats.Contains(action)))
            EditorDo(action);
    }

    // Whether a held function's key is down (the camera's flight).
    private bool EditorHeld(string action)
    {
        foreach (KeyCombo key in _editorKeys.Of(action))
        {
            if (Input.IsPhysicalKeyPressed((Key)key.Key))
                return true;
        }
        return false;
    }

    // The way the held keys fly the camera: right, up, forward.
    private Vector3 EditorFlyKeys()
    {
        float Axis(string more, string less) => (EditorHeld(more) ? 1 : 0) - (EditorHeld(less) ? 1 : 0);
        return new Vector3(Axis("cam.right", "cam.left"), Axis("cam.up", "cam.down"), Axis("cam.forward", "cam.back"));
    }

    // Runs a function of the editor by its id: a key, a menu's item and a tool bar's button come here.
    private void EditorDo(string action)
    {
        switch (action)
        {
            case "file.new": EditorAskNew(shuttle: false); break;
            case "file.newshuttle": EditorAskNew(shuttle: true); break;
            case "file.open": EditorOpenFile(); break;
            case "file.save": SaveEditorMap(); break;
            case "file.saveas": EditorSaveAs(); break;
            case "file.import": EditorImportFile(); break;
            case "file.package": EditorExportPackage(); break;
            case "file.quit": EditorGuard(() => App.Instance.Quit(0)); break;
            case "edit.undo": EditorUndo(redo: false); break;
            case "edit.redo": EditorUndo(redo: true); break;
            case "edit.cut": EditorCopy(cut: true); break;
            case "edit.copy": EditorCopy(cut: false); break;
            case "edit.paste": EditorStartPaste(); break;
            case "edit.delete": EditorDeleteSelected(); break;
            case "edit.turn": EditorTurn(clockwise: true); break;
            case "edit.turnback": EditorTurn(clockwise: false); break;
            case "edit.find":
                ShowPanel("Search");
                _searchField.GrabFocus();
                _searchField.SelectAll();
                break;
            case "edit.keys": EditorAskKeys(); break;
            case "mode.next":
                EditorSetMode((EditorPlaceMode)(((int)_editorPlaceMode + 1) % EditorPlaceModes.Length));
                break;
            case "level.up": EditorSetLevel(_editorLevel + 1); break;
            case "level.down": EditorSetLevel(_editorLevel - 1); break;
            case "level.above": EditorSetAbove(!_editorAbove); break;
            case "level.lock":
                _editorLevelLock = !_editorLevelLock;
                EditorNotice(_editorLevelLock ? "The tools work on the edited level alone." : "The tools work on what is under the mouse, on any level that is shown.", 3);
                break;
            case "view.focus": EditorFocus(); break;
            case "view.home": EditorHome(); break;
            case "view.top": EditorSetTop(!_editorTop); break;
            case "view.simple":
                _editorSimple = !_editorSimple;
                ApplyEditorLook();
                break;
            case "view.under": _editor.Feed.UnderFloor = !_editor.Feed.UnderFloor; break;
            case "view.markers": _editor.Feed.Markers = !_editor.Feed.Markers; break;
            case "view.areas": _editorAreas = !_editorAreas; break;
            case "view.border": _editorBorderOn = !_editorBorderOn; break;
            case "view.docks": _editorDocksOn = !_editorDocksOn; break;
            case "view.preview": _editorPreviewOn = !_editorPreviewOn; break;
            case "view.arrows": _editorArrowsOn = !_editorArrowsOn; break;
            case "play.start": EditorPlay(); break;
            default:
                string name = action.Substring(action.IndexOf('.') + 1);
                if (action.StartsWith("tool.", StringComparison.Ordinal))
                    _editorTool = Enum.Parse<EditorTool>(name, ignoreCase: true);
                else if (action.StartsWith("mode.", StringComparison.Ordinal))
                    EditorSetMode(Enum.Parse<EditorPlaceMode>(name, ignoreCase: true));
                else if (action.StartsWith("shape.", StringComparison.Ordinal))
                    _editorDraw = Enum.Parse<MapShape>(name, ignoreCase: true);
                else if (action.StartsWith("look.", StringComparison.Ordinal))
                {
                    int part = (int)Enum.Parse<EditorLook>(name, ignoreCase: true);
                    _editorLook[part] = !_editorLook[part];
                    ApplyEditorLook();
                }
                else if (action.StartsWith("panel.", StringComparison.Ordinal))
                    TogglePanel(EditorPanels.First(title => PanelAction(title) == action));
                break;
        }
    }

    // The place tool's mode is another: the tool is armed, and the status says what the mode does.
    private void EditorSetMode(EditorPlaceMode mode)
    {
        _editorPlaceMode = mode;
        _editorTool = EditorTool.Place;
        EditorNotice("Place: " + EditorPlaceModes[(int)mode].Tip, 4);
    }

    // The turn's key: what is selected turns a quarter; with nothing selected, or with another tool than the select one, what the place tool
    // places faces the next way (north, south, east, west and round again).
    private void EditorTurn(bool clockwise)
    {
        if (_editorTool == EditorTool.Select && !_editorPasting && (_editorRegion != null || _editorSelected != null))
        {
            EditorRotate(clockwise);
            return;
        }
        int ways = DirNames.Length - 1;
        _spawnDirIndex = clockwise ? _spawnDirIndex % ways + 1 : _spawnDirIndex <= 1 ? ways : _spawnDirIndex - 1;
        _editorBrush = null;
    }

    // ---- the Hotkeys window ----

    private void BuildKeysDialog(float s)
    {
        _keysDialog = new AcceptDialog { Title = "Hotkeys", OkButtonText = "Close", Theme = _shell.UiTheme };
        var body = new VBoxContainer();
        // (a label that wraps is as tall as its width makes it: it is given one, or the window would be measured for a label a word wide)
        body.AddChild(new Label
        {
            Text = "Every function of the editor and its keys. Choose a function, press Set key and then the key, with Ctrl, Shift, Alt or the command key if you like. A key another function had is taken from it. Esc and the mouse's buttons are fixed.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(460 * s, 0),
        });
        _keysTree = new Tree { HideRoot = true, Columns = 2, ColumnTitlesVisible = true, SelectMode = Tree.SelectModeEnum.Row, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 160 * s) };
        _keysTree.SetColumnTitle(0, "Function");
        _keysTree.SetColumnTitle(1, "Keys");
        _keysTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _keysTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _keysTree.SetColumnExpandRatio(0, 3);
        _keysTree.SetColumnExpandRatio(1, 2);
        _keysTree.ItemSelected += UpdateKeysButtons;
        _keysTree.ItemActivated += () =>
        {
            if (SelectedKeyAction() != null)
                _keysSet.ButtonPressed = true; // a double click: the key is waited for
        };
        body.AddChild(_keysTree);

        var row = new HBoxContainer();
        _keysSet = new Button { Text = "Set key...", ToggleMode = true, TooltipText = "Then press the key this function is to have. Esc leaves it as it is." };
        _keysSet.Toggled += on =>
        {
            _keysCatching = on ? SelectedKeyAction() : null;
            _keysDialog.DialogCloseOnEscape = _keysCatching == null; // (Esc gives the wait up, not the window)
            _keysSet.Text = _keysCatching != null ? "Press the key (Esc: leave it)" : "Set key...";
            if (_keysCatching != null)
                _keysSet.GrabFocus();
            else if (on)
                _keysSet.SetPressedNoSignal(false);
        };
        _keysSet.GuiInput += KeyCaught;
        _keysSet.FocusExited += () =>
        {
            if (_keysCatching != null)
                _keysSet.ButtonPressed = false;
        };
        _keysNone = new Button { Text = "No key", TooltipText = "The function has no key: it is run from its menu or its button." };
        _keysNone.Pressed += () =>
        {
            if (SelectedKeyAction() is { } action)
                SetEditorKey(action, null);
        };
        _keysDefault = new Button { Text = "Default", TooltipText = "The keys the function started with." };
        _keysDefault.Pressed += () =>
        {
            if (SelectedKeyAction() is not { } action)
                return;
            List<string> lost = _editorKeys.Reset(action);
            EditorKeysChanged(lost.Count == 0 ? string.Empty : "Taken from: " + string.Join(", ", lost.Distinct().Select(ActionTitle)) + ".");
        };
        var all = new Button { Text = "All defaults", TooltipText = "Every function has the keys it started with." };
        all.Pressed += () =>
        {
            _editorKeys.ResetAll();
            EditorKeysChanged("Every function has its own keys again.");
        };
        row.AddChild(_keysSet);
        row.AddChild(_keysNone);
        row.AddChild(_keysDefault);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(all);
        body.AddChild(row);
        _keysNote = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460 * s, 40 * s) };
        body.AddChild(_keysNote);
        _keysDialog.AddChild(body);
        _keysDialog.VisibilityChanged += () =>
        {
            if (!_keysDialog.Visible && _keysCatching != null)
                _keysSet.ButtonPressed = false;
        };
        _shell.AddChild(_keysDialog);
    }

    private void EditorAskKeys()
    {
        _keysNote.Text = string.Empty;
        FillKeysTree();
        // (as large as the list reads well, within the editor's window)
        _keysDialog.PopupCenteredClamped(new Vector2I(Mathf.RoundToInt(660 * _shell.UiScale), Mathf.RoundToInt(640 * _shell.UiScale)), 0.86f);
    }

    private string SelectedKeyAction()
        => _keysTree.GetSelected()?.GetMetadata(0) is { VariantType: Variant.Type.String } id ? id.AsString() : null;

    private void FillKeysTree()
    {
        string selected = SelectedKeyAction();
        _keysTree.Clear();
        TreeItem root = _keysTree.CreateItem(), section = null;
        string name = null;
        var dim = new Color(0.62f, 0.63f, 0.66f);
        foreach (HotkeyAction action in _editorKeys.Actions)
        {
            if (action.Section != name)
            {
                section = _keysTree.CreateItem(root);
                section.SetText(0, name = action.Section);
                section.SetSelectable(0, false);
                section.SetSelectable(1, false);
                section.SetCustomColor(0, dim);
            }
            TreeItem item = _keysTree.CreateItem(section);
            item.SetText(0, action.Title);
            item.SetText(1, KeysText(action.Id));
            item.SetMetadata(0, action.Id);
            if (!_editorKeys.IsDefault(action.Id))
                item.SetCustomColor(1, new Color(1f, 0.82f, 0.4f)); // (changed from its default)
            if (action.Id == selected)
                item.Select(0);
        }
        UpdateKeysButtons();
    }

    private void UpdateKeysButtons()
    {
        string action = SelectedKeyAction();
        _keysSet.Disabled = action == null;
        _keysNone.Disabled = action == null || _editorKeys.Of(action).Count == 0;
        _keysDefault.Disabled = action == null || _editorKeys.IsDefault(action);
    }

    // The key pressed while the window waits for one.
    private void KeyCaught(InputEvent @event)
    {
        if (_keysCatching == null || @event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        _keysSet.AcceptEvent();
        string action = _keysCatching;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code == Key.Escape)
        {
            _keysSet.ButtonPressed = false;
            return;
        }
        // (Shift, Ctrl, Alt and the command key pressed first: the key that follows is the one; a held function may have one of them for its key)
        bool held = _editorKeys.Actions[_editorActionIds[action]].Held;
        if (code == Key.None || code is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta && !held)
            return;
        _keysSet.ButtonPressed = false;
        SetEditorKey(action, new KeyCombo((int)code, key.CtrlPressed, key.ShiftPressed, key.AltPressed, key.MetaPressed));
    }

    // A function gets a key (null: none), and the function that had it is named.
    private void SetEditorKey(string action, KeyCombo? key)
    {
        string lost = _editorKeys.Set(action, key);
        EditorKeysChanged(lost == null ? string.Empty
            : Inv($"{KeysText(action)} was a key of \"{ActionTitle(lost)}\", which has {(KeysText(lost).Length > 0 ? KeysText(lost) : "no key")} now."));
    }

    private void EditorKeysChanged(string note)
    {
        _editorKeysVersion++;
        ShowEditorKeys();
        if (_keysDialog.Visible)
            FillKeysTree();
        _keysNote.Text = note;
    }

    // The console's "editor key FUNCTION KEY|none|default" and "editor keys".
    private void EditorKeyCommand(string rest)
    {
        string[] p = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 2 || !_editorActionIds.ContainsKey(p[0]))
        {
            EditorNotice(p.Length < 2 ? "editor key FUNCTION KEY|none|default" : "No such function: " + p[0] + " (editor keys lists them)");
            return;
        }
        if (p[1] == "default")
        {
            _editorKeys.Reset(p[0]);
            EditorKeysChanged(string.Empty);
        }
        else if (p[1] == "none")
        {
            SetEditorKey(p[0], null);
        }
        else if (KeyFromText(p[1]) is { } key)
        {
            SetEditorKey(p[0], key);
        }
        else
        {
            EditorNotice("No such key: " + p[1] + " (cmd+shift+z, ctrl+s, f5, equal, pageup)");
            return;
        }
        EditorNotice(Inv($"{p[0]}: {(KeysText(p[0]).Length > 0 ? KeysText(p[0]) : "no key")}{(_keysNote.Text.Length > 0 ? "   " + _keysNote.Text : string.Empty)}"));
    }

    private void EditorPrintKeys()
    {
        foreach (HotkeyAction action in _editorKeys.Actions)
            GD.Print(Inv($"[anomaly] editor:   {action.Id,-22} {KeysText(action.Id),-40} {action.Title}"));
    }
}
