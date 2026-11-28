using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using Anomaly.Client.Dev;
using Anomaly.Client.Editor;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The map editor's interface and files (docs/ai/specs/map-editor.md 5.2, 5.3). The interface is the engine's own controls in an
// EditorShell: the menus (File, Edit, View), the tool bar, and the panels in the docks around the 3D view: the palette of tg's types
// with their pictures, the selected tile with its members, the selected member's vars, and the log of the world behind the view. The
// files: new, open, save as, a .dmm import, and the question about changes that are not saved.
public partial class GameScene
{
    private static readonly string[] EditorToolNames = { "Select", "Place", "Erase", "Pick" };

    // a tool's tip (its key follows it: Keyed)
    private string EditorToolHint(int tool) => tool switch
    {
        0 => Inv($"Select: click a thing, a floor, a wall or (from under it) a ceiling; drag over tiles (Shift: from a thing's tile too) to select a rectangle. Drag the selection to move it. {KeyOf("edit.delete")} takes it away, {KeyOf("edit.turn")} turns it."),
        1 => Inv($"Place: click or drag to place the palette's type, by the shape chosen. A turf takes the tile's turf, an area its area; a floor, a lattice or a catwalk placed on a ceiling, from under it, is that ceiling. {KeyOf("edit.turn")} turns it. Ctrl + click picks what is under the mouse."),
        2 => "Erase: click or drag to take away what is highlighted: a thing, or one layer of the tile (a wall or a floor tile leaves its plating, plating leaves space, a ceiling's tile leaves its plating). A drag goes on with the same kind. With Shift the whole tile becomes the map's default tile.",
        _ => "Pick: click a thing, a turf or a ceiling to place more of it, with its vars.",
    };

    // the status bar's short line for the armed tool (a button's tip says the rest)
    private string EditorToolStatus(int tool) => tool switch
    {
        0 => "Select: click a thing or a tile, drag to select tiles, drag the selection to move it.",
        1 => Inv($"Place: click or drag by the shape chosen. {KeyOf("edit.turn")} turns it. Against a wall it hangs there."),
        2 => "Erase: click or drag takes away what is highlighted, by the shape chosen. Shift: the whole tile.",
        _ => "Pick: click a thing or a turf to place more of it.",
    };

    // the shapes of the place and the erase tools, in MapShape's order
    private static readonly (string Name, string Tip)[] EditorShapes =
    {
        ("Free", "Every tile the mouse crosses"),
        ("Line", "A line from the tile pressed to the tile let go. A disposal pipe turns along it"),
        ("Rectangle", "The border of the rectangle dragged"),
        ("Filled rectangle", "Every tile of the rectangle dragged"),
        ("Circle", "The border of the circle that fits in the rectangle dragged"),
        ("Filled circle", "Every tile of that circle"),
        ("Zone", "A click: the tiles joined to the clicked one that have its turf and its area (a room's floor, a run of wall)"),
    };

    // the place tool's modes, in EditorPlaceMode's order
    private static readonly (string Name, string Tip)[] EditorPlaceModes =
    {
        ("Add", "Add: on the tile under the mouse. A turf takes the tile's turf, a thing is one more thing; against a wall's side a thing goes on the tile in front and wall furniture hangs on that wall."),
        ("Replace", "Replace: in the place of what is clicked. A thing gives its place to a thing, a floor or a wall to a turf (a wall to a thing, on plating). A shape replaces every one of the clicked type it covers and leaves the rest."),
        ("Attach", "Attach: on the tile the clicked face looks at. Beside the side of a wall, a window or anything that fills its tile; over a block's top, on the level above; wall furniture on the wall clicked. A click at a time."),
    };

    // the parts of the game's look on the view bar, in EditorLook's order
    private static readonly (string Name, string Tip)[] EditorLookParts =
    {
        ("Lighting", "The map's own light: the lamps, and the dark where there are none. From above the walls every tile has tg's own light, so the rooms no lamp lights are seen at once; fly under the ceilings to see the lamps' light as a player does. Off: every surface lit."),
        ("Shadows", "The shadows of the lamps and of the sun (those of the lamps nearest the camera, as in the game)."),
        ("Dark areas", "The dark where no lamp lights, as in the game. Off: lifted, so that what stands there is seen while the lamps' light is looked at."),
        ("Bloom", "The glow around bright lamps and screens."),
    };

    // the panels of the docks, by their titles; the function that shows or hides one is "panel." with its title in small letters (GameScene.EditorKeys.cs)
    private static readonly string[] EditorPanels = { "Palette", "Tile", "Map", "Properties", "Log", "Search", "Checks" };

    private static string PanelAction(string title) => "panel." + title.ToLowerInvariant();

    private const int SearchRows = 500; // the most members a search lists
    private const int SearchMarks = 5000; // and the most it marks on the map

    private static readonly string[] EditorLogNoise = { "Deferred (not translated", "status ", "join ", "welcome " };

    // the sizes of the interface a mapper picks from, in percent (100: a 13 pixel font); 0 in the settings is the automatic one
    private static readonly int[] EditorScales = { 75, 100, 125, 150, 175, 200, 250, 300 };

    private MapEditorSettings _editorSettings;
    private string _editorSettingsKept; // what the settings' file holds, as a text that changes when the arrangement does
    private bool _editorInSplash; // the interface was made in the small window of the program's start, which has not its own size yet
    private double _editorSettingsNextAt;
    private EditorShell _shell;
    private PopupMenu _menuFile, _menuEdit, _menuView;
    private PopupMenu _menuRecent; // File, Recent maps
    private string[] _recentShown = Array.Empty<string>(); // ... the files of its items
    private const int RecentMenuId = 1_000_000; // (the menus' other items have their functions' numbers)
    private Button[] _toolButtons;
    private OptionButton _shapeOption, _modeOption;
    private SpinBox _heightSpin;
    private Label _levelCount;
    private Button _levelDown, _levelUp, _lockButton, _aboveButton;
    private Button _undoButton, _redoButton, _simpleButton, _topButton, _trayButton, _turnButton, _arrowsButton;
    private Button[] _lookButtons;
    private Label _titleLabel;
    private ConfirmationDialog _newDialog, _askDialog;
    private SpinBox _newWidth, _newHeight, _newLevels;
    private Action _editorAfterAsk; // what goes on once the question about the unsaved changes is answered
    private bool _editorNewAsked; // the New map window is asked for (the key, the menu)
    private bool _newIsShuttle; // the New window asks for a shuttle's map
    private Label _newHint;
    private Action _newCount; // the New window's count of tiles is worked out again

    // the palette
    private TabBar _paletteTabs;
    private OptionButton _paletteCategory, _paletteDir;
    private LineEdit _paletteSearch;
    private CheckBox _paletteFavourites, _paletteHelpers, _paletteAbstract;
    private bool _paletteHelpersOnly; // the palette lists the mapping helpers alone
    private TypeListView _paletteList;
    private Label _paletteCount, _paletteName, _palettePath, _paletteBrush;
    private TextureRect _paletteIcon;
    private string _paletteShownKey;

    // the tile and its member's vars
    private Label _tileTitle, _propsTitle, _propsThing;
    private OptionButton _tileCeiling;
    private Control _tileCeilingRow;

    // the ceilings a map sets over a tile (5.12): the turf's ceiling var
    private static readonly (string Name, string Value)[] EditorCeilings =
    {
        ("The game's rule", null), ("None: open to the sky", "none"), ("Plating", "/turf/open/floor/plating"), ("Glass", "/turf/open/floor/glass"),
        ("Reinforced glass", "/turf/open/floor/glass/reinforced"), ("Reinforced floor", "/turf/open/floor/engine"), ("Lattice", "lattice"), ("Catwalk", "catwalk"),
    };
    private Button _tileDefault, _propsPick, _propsSet;
    private Tree _tileTree, _propsTree;
    private LineEdit _propsSearch, _propsNewVar, _propsNewValue;
    private CheckBox _propsSetOnly;
    private ImageTexture _crossIcon;
    private (MapModel Tile, int X, int Y, int Z, int Member, int Thumbs) _tileShown;
    private bool _tilePicturesWaiting; // a member's picture was not made when its row was: the rows are made again once more pictures are
    private (MapModel Tile, int Member, string Filter, bool SetOnly, long Thing) _propsShown;

    // the search
    private LineEdit _searchField;
    private Label _searchCount;
    private Tree _searchTree;
    private List<MapFound> _searchFound = new();
    private readonly List<(int X, int Y, int Z)> _searchMarked = new(); // the tiles of what the search found, each once: marked on the map
    private string _searchQuery = string.Empty;
    private (string Query, long Revision) _searchShown;
    private string _searchLogged; // the words whose count the log has (a scripted check reads it)
    private double _searchNextAt;

    // the log
    private RichTextLabel _logText;
    private int _logShownCount = -1;
    private double _logNextAt;

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMWindowFocusOut)
            OnFocusLost(); // GameScene.Interaction.cs
        // the window's close button: the editor asks about changes that are not saved first (StartEditor turned the automatic quit off)
        if (what == NotificationWMCloseRequest && EditorOn)
            EditorGuard(() => App.Instance.Quit(0));
        else if (what == NotificationWMCloseRequest && App.Instance.Play != null)
            StopPlay(null); // a round of the map is played: the window's close button goes back to the editor, which asks about what is not saved
    }

    // Runs an action that leaves the open map, after asking about its unsaved changes.
    private void EditorGuard(Action then)
    {
        if (!_editor.NeedsSave)
        {
            DropAutosave();
            then();
            return;
        }
        // (left on purpose, saved or not: the kept copy of its changes goes)
        _editorAfterAsk = () =>
        {
            DropAutosave();
            then();
        };
        _askDialog.DialogText = _editor.Title.TrimEnd(' ', '*') + " has changes that are not saved.";
        _askDialog.PopupCentered();
    }

    // ---- the interface is built ----

    private void BuildEditorShell()
    {
        int percent = _editorSettings.Int("view", "scale", 0);
        int screen = GetWindow().CurrentScreen;
        _shell = new EditorShell(percent > 0 ? percent / 100f : EditorShell.AutomaticScale(screen));
        AddChild(_shell);
        // (the panels are new: what they showed is shown again)
        _paletteShownKey = null;
        _tileShown = default;
        _propsShown = default;
        _searchShown = default;
        _logShownCount = -1;
        _editorSettingsKept = null;
        App.Instance.VersionTagShown = false; // the tool bar is in its corner: the status bar names the build
        _crossIcon = _shell.CrossIcon();
        float s = _shell.UiScale;

        _keyedItems.Clear();
        _keyedTips.Clear();
        _menuFile = _shell.AddMenu("File");
        KeyedItem(_menuFile, "file.new", "New map...");
        KeyedItem(_menuFile, "file.newshuttle", "New shuttle...");
        KeyedItem(_menuFile, "file.open", "Open...");
        _menuRecent = new PopupMenu();
        _menuRecent.IdPressed += id => EditorOpen(_recentShown[(int)id], _editor.Document.Width, _editor.Document.Height, _editor.Document.Levels);
        _menuFile.AddSubmenuNodeItem("Recent maps", _menuRecent, RecentMenuId);
        NoteRecentMap();
        KeyedItem(_menuFile, "file.save", "Save");
        KeyedItem(_menuFile, "file.saveas", "Save as...");
        _menuFile.AddSeparator();
        KeyedItem(_menuFile, "file.import", "Import a .dmm map...");
        KeyedItem(_menuFile, "file.package", "Export the map's package...");
        _menuFile.AddSeparator();
        KeyedItem(_menuFile, "file.quit", "Quit");
        _menuEdit = _shell.AddMenu("Edit");
        KeyedItem(_menuEdit, "edit.undo", "Undo");
        KeyedItem(_menuEdit, "edit.redo", "Redo");
        _menuEdit.AddSeparator();
        KeyedItem(_menuEdit, "edit.cut", "Cut");
        KeyedItem(_menuEdit, "edit.copy", "Copy");
        KeyedItem(_menuEdit, "edit.paste", "Paste");
        KeyedItem(_menuEdit, "edit.delete", "Delete");
        KeyedItem(_menuEdit, "edit.turn", "Turn right");
        KeyedItem(_menuEdit, "edit.turnback", "Turn left");
        _menuEdit.AddSeparator();
        KeyedItem(_menuEdit, "edit.find", "Find on the map...");
        _menuEdit.AddSeparator();
        KeyedItem(_menuEdit, "edit.keys", "Hotkeys...");
        _menuView = _shell.AddMenu("View");
        KeyedItem(_menuView, "view.focus", "Focus on the selection");
        KeyedItem(_menuView, "view.home", "Go to the map's first tile");
        KeyedItem(_menuView, "view.top", "Top view", check: true);
        _menuView.AddSeparator();
        KeyedItem(_menuView, "level.up", "Level up");
        KeyedItem(_menuView, "level.down", "Level down");
        KeyedItem(_menuView, "level.above", "Show the levels above", check: true);
        KeyedItem(_menuView, "level.lock", "Lock the tools to the edited level", check: true);
        _menuView.AddSeparator();
        KeyedItem(_menuView, "view.simple", "Simple look (every surface lit, no shadows)", check: true);
        _menuView.AddSeparator();
        KeyedItem(_menuView, "view.under", "T-ray: under the floor (cables, pipes, disposal pipes)", check: true);
        KeyedItem(_menuView, "view.markers", "Markers (landmarks, spawners, mapping helpers)", check: true);
        KeyedItem(_menuView, "view.areas", "Areas (a colour over each area's tiles; always shown on the palette's Areas tab)", check: true);
        KeyedItem(_menuView, "view.border", "Map border (lines around the map's tiles, level by level)", check: true);
        KeyedItem(_menuView, "view.docks", "Shuttle docks (the tiles a docked shuttle takes)", check: true);
        KeyedItem(_menuView, "view.preview", "Preview of what the place tool places", check: true);
        KeyedItem(_menuView, "view.arrows", "Arrows: the way things face", check: true);
        _menuView.AddSeparator();
        foreach (string title in EditorPanels)
            KeyedItem(_menuView, PanelAction(title), title, check: true);
        _menuView.AddSeparator();
        var sizes = new PopupMenu();
        sizes.AddRadioCheckItem(Inv($"Automatic ({EditorShell.AutomaticScale(screen) * 100:0}%)"), 0);
        foreach (int size in EditorScales)
            sizes.AddRadioCheckItem(Inv($"{size}%"), size);
        for (int i = 0; i < sizes.ItemCount; i++)
            sizes.SetItemChecked(i, sizes.GetItemId(i) == percent);
        sizes.IdPressed += id => SetEditorScale((int)id);
        _menuView.AddSubmenuNodeItem("Interface size", sizes);
        foreach (PopupMenu menu in new[] { _menuFile, _menuEdit, _menuView })
        {
            menu.IdPressed += id =>
            {
                if (id < _editorKeys.Actions.Count) // (an item that opens a menu of its own has no function)
                    EditorDo(_editorKeys.Actions[(int)id].Id);
            };
        }
        _menuView.AboutToPopup += SyncViewMenu;

        // the tool bar
        var group = new ButtonGroup();
        _toolButtons = new Button[EditorToolNames.Length];
        for (int i = 0; i < EditorToolNames.Length; i++)
        {
            int tool = i;
            _toolButtons[i] = Keyed(new Button { Text = EditorToolNames[i], ToggleMode = true, ButtonGroup = group, FocusMode = Control.FocusModeEnum.None },
                "tool." + EditorToolNames[i].ToLowerInvariant(), () => EditorToolHint(tool));
            _toolButtons[i].Pressed += () => _editorTool = (EditorTool)tool;
            _shell.Tools.AddChild(_toolButtons[i]);
        }
        _shapeOption = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "The shape the place and the erase tools work by" };
        foreach ((string name, string tip) in EditorShapes)
        {
            _shapeOption.AddItem(name);
            _shapeOption.SetItemTooltip(_shapeOption.ItemCount - 1, tip);
        }
        _shapeOption.ItemSelected += index => _editorDraw = (MapShape)index;
        _shell.Tools.AddChild(_shapeOption);
        _modeOption = Keyed(new OptionButton { FocusMode = Control.FocusModeEnum.None }, "mode.next", () => "How the place tool puts the palette's type on what is clicked. The key goes through the modes.");
        foreach ((string name, string tip) in EditorPlaceModes)
        {
            _modeOption.AddItem(name);
            _modeOption.SetItemTooltip(_modeOption.ItemCount - 1, tip);
        }
        _modeOption.ItemSelected += index => _editorPlaceMode = (EditorPlaceMode)index;
        _shell.Tools.AddChild(_modeOption);
        _heightSpin = ToolSpin("Height", "The levels a click, a stroke or a shape of the place and the erase tools goes up through: 1 is the level clicked alone, 3 that one and the two over it (a wall three levels high, a floor on each of three levels)", value => _editorHeight = value);
        _shell.Tools.AddChild(new VSeparator());
        // the edited level: two buttons large enough to hit, with the level between them (the owner, 2026-11-17: the number box's arrows were too small)
        const string levelTip = "The level that is edited. The view shows it and the levels under it, and a click on empty space builds on it.";
        _shell.Tools.AddChild(new Label { Text = "Level", TooltipText = levelTip, MouseFilter = Control.MouseFilterEnum.Stop });
        _levelDown = Keyed(new Button { Icon = _shell.ArrowIcon(up: false), IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(44 * s, 0), FocusMode = Control.FocusModeEnum.None },
            "level.down", () => "The level below.");
        _levelDown.Pressed += () => EditorDo("level.down");
        _levelCount = new Label { HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(58 * s, 0), TooltipText = levelTip, MouseFilter = Control.MouseFilterEnum.Stop };
        _levelUp = Keyed(new Button { Icon = _shell.ArrowIcon(up: true), IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(44 * s, 0), FocusMode = Control.FocusModeEnum.None },
            "level.up", () => "The level above.");
        _levelUp.Pressed += () => EditorDo("level.up");
        _shell.Tools.AddChild(_levelDown);
        _shell.Tools.AddChild(_levelCount);
        _shell.Tools.AddChild(_levelUp);
        _lockButton = Keyed(ViewToggle("Lock", null, on => _editorLevelLock = on), "level.lock",
            () => "Lock the tools to the edited level. Off (the default): a tool works on what is under the mouse on any level that is shown, so a block of a lower level is built on, erased, picked or selected without changing the level; a click on empty space is still the edited level's. On: every tool works on the edited level alone, and its open tiles are clicked, not what is seen through them.");
        _shell.Tools.AddChild(_lockButton);
        _aboveButton = Keyed(ViewToggle("Above", null, EditorSetAbove), "level.above",
            () => "Show the levels over the edited one too, as the game draws them. Off: they are hidden, so the edited level is seen from above.");
        _shell.Tools.AddChild(_aboveButton);
        _shell.Tools.AddChild(new VSeparator());
        _undoButton = new Button { Text = "Undo", FocusMode = Control.FocusModeEnum.None };
        _undoButton.Pressed += () => EditorUndo(redo: false);
        _redoButton = new Button { Text = "Redo", FocusMode = Control.FocusModeEnum.None };
        _redoButton.Pressed += () => EditorUndo(redo: true);
        _shell.Tools.AddChild(_undoButton);
        _shell.Tools.AddChild(_redoButton);
        _turnButton = Keyed(new Button { Text = "Turn", FocusMode = Control.FocusModeEnum.None }, "edit.turn",
            () => "Turn what is selected a quarter clockwise (with Shift held, the other way): the selected thing where it stands, or the selected tiles with all that stands on them. With the place tool: turn what it places.");
        _turnButton.Pressed += () => EditorTurn(clockwise: !Input.IsKeyPressed(Key.Shift));
        _shell.Tools.AddChild(_turnButton);
        _shell.Tools.AddChild(new VSeparator());
        _playButton = new Button { Text = "Play", FocusMode = Control.FocusModeEnum.None };
        _playButton.Pressed += () => EditorPlay();
        _shell.Tools.AddChild(_playButton);
        // where Play starts the player (the owner, 2026-11-17: the arrival by shuttle is tested from the editor too)
        _playStartOption = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Where Play starts you" };
        _playStartOption.AddItem("On the tile");
        _playStartOption.SetItemTooltip(0, "Play starts you on the selected tile, or where the view looks.");
        _playStartOption.AddItem("As an arrival");
        _playStartOption.SetItemTooltip(1, "Play starts you as a player who joins a round in progress: in a seat of the map's arrivals shuttle, or at a late-join landmark.");
        _playStartOption.AddItem("At a spawn point");
        _playStartOption.SetItemTooltip(2, "Play starts you on the map's round-start spawn point (a job's start landmark) nearest to the selected tile, or to where the view looks.");
        _playStartOption.ItemSelected += index => (_playArrive, _playAtStart) = (index == 1, index == 2);
        _shell.Tools.AddChild(_playStartOption);
        _titleLabel = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right, ClipText = true };
        _shell.Tools.AddChild(_titleLabel);

        // the view's bar (5.6): the camera, then the look
        string turn = "The middle mouse button, or Alt with the left, turns the view around what is selected; with Shift it moves the view. The wheel goes toward the point under the mouse.";
        Button focus = Keyed(new Button { Text = "Focus", FocusMode = Control.FocusModeEnum.None }, "view.focus", () => "Bring what is selected into the view; with nothing selected, the whole map. " + turn);
        focus.Pressed += EditorFocus;
        _topButton = Keyed(ViewToggle("Top view", null, EditorSetTop), "view.top",
            () => "The top view: straight down, north up, no perspective. The right or the middle mouse button moves the map, the wheel changes its scale. Again gives the view back.");
        Button home = Keyed(new Button { Text = "Home", FocusMode = Control.FocusModeEnum.None }, "view.home", () => "Go to the map's first tile, 1, 1: its south-west corner. The status bar says where the camera is.");
        home.Pressed += EditorHome;
        _trayButton = Keyed(ViewToggle("T-ray", null, on => _editor.Feed.UnderFloor = on), "view.under",
            () => "Show what the floor tiles cover, as a T-ray scanner does but steady and over the whole map: the cables, the pipes and the disposal pipes, half clear over their tiles.");
        _arrowsButton = Keyed(ViewToggle("Arrows", null, on => _editorArrowsOn = on), "view.arrows",
            () => "An arrow over every thing that has a way to face (furniture, machines, mobs, spawners, landmarks), pointing that way: a thing still drawn as a flat picture shows its way so.");
        _shell.ViewBar.AddChild(focus);
        _shell.ViewBar.AddChild(home);
        _shell.ViewBar.AddChild(_topButton);
        _shell.ViewBar.AddChild(_trayButton);
        _shell.ViewBar.AddChild(_arrowsButton);
        _shell.ViewBar.AddChild(new VSeparator());
        _simpleButton = Keyed(ViewToggle("Simple look", null, on =>
        {
            _editorSimple = on;
            ApplyEditorLook();
        }), "view.simple", () => "Every surface lit and no shadows: the fastest view. Off: the game's own look, by the switches beside this one.");
        _shell.ViewBar.AddChild(_simpleButton);
        _lookButtons = new Button[EditorLookParts.Length];
        for (int i = 0; i < _lookButtons.Length; i++)
        {
            int part = i;
            _lookButtons[i] = Keyed(ViewToggle(EditorLookParts[i].Name, null, on =>
            {
                _editorLook[part] = on;
                ApplyEditorLook();
            }), "look." + LookKey(i), () => EditorLookParts[part].Tip);
            _shell.ViewBar.AddChild(_lookButtons[i]);
        }

        _shell.AddPanel(_shell.LeftTopDock, "Palette", _shell.Padded(BuildPalettePanel(s)));
        _shell.AddPanel(_shell.RightTopDock, "Tile", _shell.Padded(BuildTilePanel(s)));
        _shell.AddPanel(_shell.RightTopDock, "Map", _shell.Padded(BuildMapPanel(s)));
        _shell.RightTopDock.CurrentTab = 0;
        _mapShown = null;
        _mapLevelsShown = default;
        _shell.AddPanel(_shell.RightBottomDock, "Properties", _shell.Padded(BuildPropertiesPanel(s)));
        _logText = new RichTextLabel { ScrollFollowing = true, SelectionEnabled = true, FocusMode = Control.FocusModeEnum.Click };
        _shell.AddPanel(_shell.BottomDock, "Log", _shell.Padded(_logText));
        _shell.AddPanel(_shell.BottomDock, "Search", _shell.Padded(BuildSearchPanel(s)));
        _shell.AddPanel(_shell.BottomDock, "Checks", _shell.Padded(BuildChecksPanel(s)));
        _shell.BottomDock.CurrentTab = 0;
        _shell.LoadLayout(_editorSettings); // the panels go where the mapper left them
        _editorInSplash = App.Instance.SplashWindow;

        // the windows that ask
        _newWidth = Spin(1, MapDocument.MaxWidth, 64);
        _newHeight = Spin(1, MapDocument.MaxHeight, 64);
        _newLevels = Spin(1, MapDocument.MaxLevels, 1);
        var grid = new GridContainer { Columns = 2 };
        foreach ((string label, SpinBox spin) in new[] { ("Width (x)", _newWidth), ("Height (y)", _newHeight), ("Levels (z)", _newLevels) })
        {
            grid.AddChild(new Label { Text = label });
            grid.AddChild(spin);
        }
        var newBody = new VBoxContainer();
        newBody.AddChild(_newHint = new Label { Text = "The size of the new map, in tiles. Every tile is space." });
        newBody.AddChild(grid);
        var newTiles = new Label();
        // A number box takes what is typed in it only on Enter or when it loses the keys: the count reads the text as it is typed (the owner,
        // 2026-11-17: 10 x 10 x 10 said 100 tiles, the levels typed last and not yet taken).
        static long Typed(SpinBox spin)
            => long.TryParse(spin.GetLineEdit().Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long typed) ? Math.Clamp(typed, (long)spin.MinValue, (long)spin.MaxValue) : (long)spin.Value;
        void CountTiles()
        {
            long tiles = Typed(_newWidth) * Typed(_newHeight) * (_newIsShuttle ? 1 : Typed(_newLevels));
            // (MetaStation's one level is 65,025 tiles and opens in half a minute)
            newTiles.Text = Inv($"{tiles:N0} tiles") + (tiles > 400_000 ? ": a map this large takes minutes to open and several gigabytes." : tiles > 130_000 ? ": a large map, it takes a minute to open." : ".");
        }
        foreach (SpinBox spin in new[] { _newWidth, _newHeight, _newLevels })
        {
            spin.ValueChanged += _ => CountTiles();
            spin.GetLineEdit().TextChanged += _ => CountTiles();
        }
        _newCount = CountTiles;
        CountTiles();
        newBody.AddChild(newTiles);
        _newDialog = new ConfirmationDialog { Title = "New map", OkButtonText = "Choose its file...", Theme = _shell.UiTheme };
        _newDialog.AddChild(newBody);
        _newDialog.Confirmed += () =>
        {
            foreach (SpinBox spin in new[] { _newWidth, _newHeight, _newLevels })
                spin.Apply(); // (a number typed and not yet taken)
            (int w, int h, int levels, bool shuttle) = ((int)_newWidth.Value, (int)_newHeight.Value, _newIsShuttle ? 1 : (int)_newLevels.Value, _newIsShuttle);
            // (the window opens in the maps folder of the open map, else in the game's own: a station's file goes into its map_files, a shuttle's into its shuttles)
            string maps = _editor.MapsFolder ?? EditorFeed.GameMaps;
            EditorFileDialog(FileDialog.FileModeEnum.SaveFile, shuttle ? "The new shuttle's file (shuttles/PORT/PORT_NAME in a maps folder)" : "The new map's file (map_files/NAME in a maps folder)", new[] { "*.as3dmap ; Maps" },
                path => EditorOpen(WithExtension(path), w, h, levels, create: true, shuttle: shuttle), maps == null ? null : Path.Combine(maps, shuttle ? "shuttles" : "map_files"));
        };
        _shell.AddChild(_newDialog);

        _askDialog = new ConfirmationDialog { Title = "Changes that are not saved", OkButtonText = "Save", Theme = _shell.UiTheme };
        _askDialog.AddButton("Do not save", false, "discard");
        _askDialog.Confirmed += () =>
        {
            SaveEditorMap();
            EditorAnswered(go: !_editor.NeedsSave);
        };
        _askDialog.CustomAction += action =>
        {
            _askDialog.Hide();
            EditorAnswered(go: action.ToString() == "discard");
        };
        _askDialog.Canceled += () => _editorAfterAsk = null;
        _shell.AddChild(_askDialog);
        BuildKeysDialog(s);
        BuildRestoreDialog(s);
        ShowEditorKeys();
    }

    // A small whole number in the tool bar, with its name before it.
    private SpinBox ToolSpin(string name, string tip, Action<int> changed)
    {
        _shell.Tools.AddChild(new Label { Text = name, TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Stop });
        var spin = new SpinBox { MinValue = 1, MaxValue = 1, Step = 1, Rounded = true, TooltipText = tip, SelectAllOnFocus = true };
        spin.GetLineEdit().FocusMode = Control.FocusModeEnum.Click;
        spin.ValueChanged += value => changed((int)value);
        // (the number typed, Enter gives the keys back to the view)
        spin.GetLineEdit().TextSubmitted += _ => spin.GetLineEdit().ReleaseFocus();
        _shell.Tools.AddChild(spin);
        return spin;
    }

    // The window that asks a new map's size: a station's, or a shuttle's (one level, every tile tg's tile that leaves the map under it as it is).
    private void EditorAskNew(bool shuttle)
    {
        _newIsShuttle = shuttle;
        _newDialog.Title = shuttle ? "New shuttle" : "New map";
        _newHint.Text = shuttle ? "The size of the new shuttle's map, in tiles. Every tile is empty: what you build is the shuttle.\nPlace one docking port of its own (/obj/docking_port/mobile) where it meets its dock."
            : "The size of the new map, in tiles. Every tile is space.\nName its file in the map_files of a maps folder (the game's own is offered) and the map is that folder's:\nit is saved at once as map_files/NAME/NAME.as3dmap with its json, and has the folder's shuttles and CentCom.";
        _newLevels.Editable = !shuttle;
        if (shuttle)
            (_newWidth.Value, _newHeight.Value) = (Math.Min(_newWidth.Value, 16), Math.Min(_newHeight.Value, 16));
        _newCount();
        _newDialog.PopupCentered();
    }

    private static Button ViewToggle(string text, string tip, Action<bool> toggled)
    {
        var button = new Button { Text = text, ToggleMode = true, FocusMode = Control.FocusModeEnum.None, TooltipText = tip };
        button.Toggled += on => toggled(on);
        return button;
    }

    // The editor's own settings are read once, before the look and the interface are made: the look, and the favourites of each of the palette's lists
    // (a list the file has not got keeps the dev menu's).
    private void ReadEditorSettings(ClientArgs args)
    {
        _editorSettings = new MapEditorSettings(args.EditorSettingsPath);
        ReadEditorKeys();
        _editorLevelLock = _editorSettings.Bool("view", "level_lock", false);
        _editorArrowsOn = _editorSettings.Bool("view", "arrows", false);
        _editorSimple = _editorSettings.Bool("view", "simple_look", _editorSimple);
        for (int i = 0; i < _editorLook.Length; i++)
            _editorLook[i] = _editorSettings.Bool("view", LookKey(i), true);
        _editorFlySpeed = Mathf.Clamp(_editorSettings.Int("view", "fly_speed", 100) / 100f, EditorMinSpeed, EditorMaxSpeed);
        _editor.Feed.UnderFloor = _editorSettings.Bool("view", "under_floor", false);
        _editor.Feed.Markers = _editorSettings.Bool("view", "markers", true);
        _editorAreas = _editorSettings.Bool("view", "areas", false);
        _editorBorderOn = _editorSettings.Bool("view", "border", true);
        _editorPreviewOn = _editorSettings.Bool("view", "preview", true);
        _editorDocksOn = _editorSettings.Bool("view", "docks", true);
        _playArrive = _editorSettings.Bool("play", "arrive", false);
        _playAtStart = !_playArrive && _editorSettings.Bool("play", "start", false);
        for (int i = 0; i < _favourites.Length; i++)
        {
            if (!_editorSettings.Has("palette", Inv($"favourites_{i}")))
                continue;
            _favourites[i].Clear();
            _favourites[i].UnionWith(_editorSettings.Texts("palette", Inv($"favourites_{i}")));
        }
        _favouritesVersion++;
    }

    // The settings' file follows what it holds: looked at once a second, written when the arrangement, the look or the favourites changed.
    private void KeepEditorSettings(double now)
    {
        if (_editorInSplash)
        {
            // The interface was laid out in the small window of the program's start (App.TakeFirstWindow). Nothing of that arrangement is written,
            // and the panels go where the mapper left them once more when the window has its own size.
            if (App.Instance.SplashWindow)
                return;
            _editorInSplash = false;
            _shell.LoadLayout(_editorSettings);
        }
        if (now < _editorSettingsNextAt)
            return;
        _editorSettingsNextAt = now + 1;
        string kept = Inv($"{_shell.LayoutSignature()}|{_editorSimple}|{_favouritesVersion}|{_editor.Feed.UnderFloor}|{_editor.Feed.Markers}|{_editorAreas}|{_editorBorderOn}|{_editorPreviewOn}|{_editorDocksOn}|{string.Join(' ', _editorLook)}|{_editorFlySpeed}|{_editorKeysVersion}|{_editorLevelLock}|{_editorArrowsOn}|{_playArrive}|{_playAtStart}");
        if (kept == _editorSettingsKept)
            return;
        _editorSettingsKept = kept;
        WriteEditorSettings();
    }

    private void WriteEditorSettings()
    {
        _shell.SaveLayout(_editorSettings);
        _editorSettings.Set("view", "simple_look", _editorSimple);
        for (int i = 0; i < _editorLook.Length; i++)
            _editorSettings.Set("view", LookKey(i), _editorLook[i]);
        _editorSettings.Set("view", "fly_speed", Mathf.RoundToInt(_editorFlySpeed * 100f));
        _editorSettings.Set("view", "under_floor", _editor.Feed.UnderFloor);
        _editorSettings.Set("view", "markers", _editor.Feed.Markers);
        _editorSettings.Set("view", "areas", _editorAreas);
        _editorSettings.Set("view", "border", _editorBorderOn);
        _editorSettings.Set("view", "preview", _editorPreviewOn);
        _editorSettings.Set("view", "docks", _editorDocksOn);
        _editorSettings.Set("view", "level_lock", _editorLevelLock);
        _editorSettings.Set("view", "arrows", _editorArrowsOn);
        _editorSettings.Set("play", "arrive", _playArrive);
        _editorSettings.Set("play", "start", _playAtStart);
        _editorSettings.SetSection("keys", _editorKeys.Changed());
        for (int i = 0; i < _favourites.Length; i++)
            _editorSettings.Set("palette", Inv($"favourites_{i}"), _favourites[i].OrderBy(path => path, StringComparer.Ordinal).ToArray());
        _editorSettings.Save();
    }

    // The settings' key of a part of the game's look: lighting, shadows, dark, bloom.
    private static string LookKey(int part) => ((EditorLook)part).ToString().ToLowerInvariant();

    // Another size of the interface (0: the automatic one): the interface is made again at that size, with the panels where they are.
    private void SetEditorScale(int percent)
    {
        WriteEditorSettings();
        _editorSettings.Set("view", "scale", percent);
        _editorSettings.Save();
        // (this runs inside a menu of the interface that is taken away)
        Callable.From(() =>
        {
            _shell.QueueFree();
            BuildEditorShell();
        }).CallDeferred();
    }

    private void EditorAnswered(bool go)
    {
        Action then = _editorAfterAsk;
        _editorAfterAsk = null;
        if (go)
            then?.Invoke();
    }

    private static SpinBox Spin(int min, int max, int value)
        => new() { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true, CustomMinimumSize = new Vector2(120, 0) };

    private Control BuildPalettePanel(float s)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Mathf.RoundToInt(5 * s));
        _paletteTabs = new TabBar { ClipTabs = true, FocusMode = Control.FocusModeEnum.None };
        foreach (string tab in TabNames)
            _paletteTabs.AddTab(tab);
        _paletteTabs.TabChanged += tab =>
        {
            _spawnTab = (SpawnTab)tab;
            _spawnCategory = -1;
            FillPaletteCategories();
        };
        column.AddChild(_paletteTabs);

        var filter = new HBoxContainer();
        _paletteCategory = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(130 * s, 0), ClipText = true };
        _paletteCategory.ItemSelected += index => _spawnCategory = (int)index - 1;
        _paletteSearch = new LineEdit { PlaceholderText = "Search by name or path", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _paletteSearch.TextChanged += text => _spawnQuery = text;
        filter.AddChild(_paletteCategory);
        filter.AddChild(_paletteSearch);
        column.AddChild(filter);

        var boxes = new HFlowContainer(); // (they go onto a second line in a narrow dock)
        _paletteFavourites = Check("Favourites", "Only the types starred in this list (click a row's star)", on => _spawnFavouritesOnly = on);
        _paletteHelpers = Check("Helpers", "Only the mapping helpers, the landmarks, the spawners and the docking ports (they are in the whole list too)", on => _paletteHelpersOnly = on);
        _paletteAbstract = Check("Abstract", "The base types that are not meant to be placed", on => _spawnAbstract = on);
        boxes.AddChild(_paletteFavourites);
        boxes.AddChild(_paletteHelpers);
        boxes.AddChild(_paletteAbstract);
        column.AddChild(boxes);

        _paletteList = new TypeListView { SizeFlagsVertical = Control.SizeFlags.ExpandFill, Thumbs = _thumbs, IsFavourite = e => _favourites[(int)e.Tab].Contains(e.Path) };
        _paletteList.Picked += Select;
        _paletteList.StarClicked += e =>
        {
            if (!_favourites[(int)e.Tab].Remove(e.Path))
                _favourites[(int)e.Tab].Add(e.Path);
            _favouritesVersion++;
        };
        _paletteList.Toggled += path =>
        {
            SpawnToggleBranch(path);
            _paletteList.SetRows(_spawnRows, keepScroll: true);
        };
        column.AddChild(_paletteList);
        _paletteCount = new Label();
        _paletteCount.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        column.AddChild(_paletteCount);

        // the type the place tool places
        var chosen = new HBoxContainer();
        _paletteIcon = new TextureRect { CustomMinimumSize = new Vector2(48 * s, 48 * s), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _paletteName = new Label { ClipText = true, Text = "Pick a type to place." };
        _palettePath = new Label { ClipText = true };
        _palettePath.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        _paletteBrush = new Label { ClipText = true };
        _paletteBrush.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        names.AddChild(_paletteName);
        names.AddChild(_palettePath);
        names.AddChild(_paletteBrush);
        chosen.AddChild(_paletteIcon);
        chosen.AddChild(names);
        column.AddChild(chosen);
        var facing = new HBoxContainer();
        facing.AddChild(new Label { Text = "Facing" });
        _paletteDir = Keyed(new OptionButton { FocusMode = Control.FocusModeEnum.None }, "edit.turn", () => "The way what the place tool places faces. The turn's key goes to the next way.");
        foreach (string dir in DirNames)
            _paletteDir.AddItem(dir);
        _paletteDir.ItemSelected += index =>
        {
            _spawnDirIndex = (int)index;
            _editorBrush = null;
        };
        facing.AddChild(_paletteDir);
        column.AddChild(facing);
        FillPaletteCategories();
        return column;
    }

    private static CheckBox Check(string text, string tip, Action<bool> toggled)
    {
        var box = new CheckBox { Text = text, TooltipText = tip, FocusMode = Control.FocusModeEnum.None };
        box.Toggled += on => toggled(on);
        return box;
    }

    private void FillPaletteCategories()
    {
        _paletteCategory.Clear();
        _paletteCategory.AddItem("All " + TabNames[(int)_spawnTab].ToLowerInvariant());
        foreach (SpawnCategory category in SpawnCatalog.CategoriesOf(_spawnTab))
            _paletteCategory.AddItem(category.Name);
        _paletteCategory.Selected = Math.Clamp(_spawnCategory + 1, 0, _paletteCategory.ItemCount - 1);
    }

    private Control BuildTilePanel(float s)
    {
        var column = new VBoxContainer();
        var head = new HBoxContainer();
        // (a long line wraps: a label that does not would widen its dock)
        _tileTitle = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Text = "Select a thing or a tile on the map.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _tileDefault = new Button { Text = "Empty tile", TooltipText = "Make this tile its level's empty tile again: the map's default tile, with the level's base turf where the Map panel sets one", FocusMode = Control.FocusModeEnum.None, Visible = false };
        _tileDefault.Pressed += () =>
        {
            if (_editorSelected is { } tile)
                EditorEdit("Clear tile", tile.X, tile.Y, tile.Z, (m, _) => m.Empty(tile.Z));
        };
        head.AddChild(_tileTitle);
        head.AddChild(_tileDefault);
        column.AddChild(head);
        // the ceiling over the selected tile, or over every tile of the selected rectangle
        var ceiling = new HBoxContainer { Visible = false };
        ceiling.AddChild(new Label { Text = "Ceiling", TooltipText = CeilingTip, MouseFilter = Control.MouseFilterEnum.Stop });
        _tileCeiling = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false, TooltipText = CeilingTip };
        foreach ((string name, _) in EditorCeilings)
            _tileCeiling.AddItem(name);
        _tileCeiling.AddItem("Another");
        _tileCeiling.ItemSelected += index =>
        {
            if (index < EditorCeilings.Length)
                EditorSetCeiling(EditorCeilings[index].Value);
        };
        ceiling.AddChild(_tileCeiling);
        column.AddChild(_tileCeilingRow = ceiling);
        column.AddChild(_tileGasRow = BuildTileGasRow(s)); // the gas the selected tiles start with
        _tileTree = new Tree { HideRoot = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = Tree.SelectModeEnum.Row, FocusMode = Control.FocusModeEnum.Click };
        _tileTree.AddThemeConstantOverride("icon_max_width", Mathf.RoundToInt(32 * s));
        _tileTree.ItemSelected += () =>
        {
            if (_editorSelected is { } tile && _tileTree.GetSelected() is { } item)
                _editorSelected = (tile.X, tile.Y, tile.Z, (int)item.GetMetadata(0));
        };
        _tileTree.ButtonClicked += (item, _, _, _) =>
        {
            if (_editorSelected is not { } tile)
                return;
            int index = (int)item.GetMetadata(0);
            MapMember member = _editor.Document[tile.X, tile.Y, tile.Z].Members[index];
            if (EditorEdit("Delete " + member.Path, tile.X, tile.Y, tile.Z, (m, t) => MapEdits.Remove(m, t, index, m.Empty(tile.Z))))
                _editorSelected = (tile.X, tile.Y, tile.Z, -1);
        };
        column.AddChild(_tileTree);
        return column;
    }

    private Control BuildPropertiesPanel(float s)
    {
        var column = new VBoxContainer();
        _propsTitle = new Label { ClipText = true, Text = "Select a member of a tile to see its vars." };
        _propsThing = new Label { ClipText = true };
        _propsThing.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        column.AddChild(_propsTitle);
        column.AddChild(_propsThing);
        var filter = new HBoxContainer();
        _propsSearch = new LineEdit { PlaceholderText = "Search the vars", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _propsSetOnly = new CheckBox { Text = "Only set", TooltipText = "Only the vars this member sets itself", FocusMode = Control.FocusModeEnum.None };
        _propsPick = new Button { Text = "Pick to place", TooltipText = "Place more of this member, with its vars", FocusMode = Control.FocusModeEnum.None };
        _propsPick.Pressed += () =>
        {
            if (_editorSelected is not { Member: >= 0 } selected)
                return;
            MapMember member = _editor.Document[selected.X, selected.Y, selected.Z].Members[selected.Member];
            EditorPickMember(member);
        };
        filter.AddChild(_propsSearch);
        filter.AddChild(_propsSetOnly);
        filter.AddChild(_propsPick);
        column.AddChild(filter);

        _propsTree = new Tree { HideRoot = true, Columns = 2, ColumnTitlesVisible = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = Tree.SelectModeEnum.Row, FocusMode = Control.FocusModeEnum.Click };
        _propsTree.SetColumnTitle(0, "Var");
        _propsTree.SetColumnTitle(1, "Value");
        _propsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _propsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _propsTree.SetColumnExpandRatio(0, 1);
        _propsTree.SetColumnExpandRatio(1, 1);
        _propsTree.ItemEdited += () =>
        {
            if (_propsTree.GetEdited() is not { } item || _editorSelected is not { Member: >= 0 } selected)
                return;
            string name = item.GetText(0), def = (string)item.GetMetadata(1), text = item.GetText(1);
            // the type's own value again, or nothing: the member need not set the var
            EditorSetVar(selected, name, text == def ? null : text);
            _propsShown = default; // the list is read again, whether or not the change was taken
        };
        _propsTree.ButtonClicked += (item, _, _, _) =>
        {
            if (_editorSelected is { Member: >= 0 } selected)
                EditorSetVar(selected, item.GetText(0), null);
        };
        column.AddChild(_propsTree);

        // a var the list has not got (a type whose class is not in the code yet), set by its name
        var add = new HBoxContainer();
        _propsNewVar = new LineEdit { PlaceholderText = "var", CustomMinimumSize = new Vector2(110 * s, 0) };
        _propsNewValue = new LineEdit { PlaceholderText = "\"text\", 5, null, /a/path", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _propsSet = new Button { Text = "Set", FocusMode = Control.FocusModeEnum.None };
        void SetNew()
        {
            if (_editorSelected is not { Member: >= 0 } selected || _propsNewVar.Text.Trim().Length == 0 || _propsNewValue.Text.Trim().Length == 0)
                return;
            if (EditorSetVar(selected, _propsNewVar.Text.Trim(), _propsNewValue.Text))
            {
                _propsNewVar.Clear();
                _propsNewValue.Clear();
            }
        }
        _propsSet.Pressed += SetNew;
        _propsNewValue.TextSubmitted += _ => SetNew();
        add.AddChild(_propsNewVar);
        add.AddChild(_propsNewValue);
        add.AddChild(_propsSet);
        column.AddChild(add);
        return column;
    }

    private bool EditorSetVar((int X, int Y, int Z, int Member) selected, string name, string literal)
        => EditorEdit("Set " + name, selected.X, selected.Y, selected.Z, (m, t) => MapEdits.SetVar(m, t, selected.Member, name, literal));

    // A member of the map becomes what the place tool places, and the palette shows its type.
    private void EditorPickMember(MapMember member)
    {
        _editorBrush = member;
        _editorTool = EditorTool.Place;
        _editorPasting = false;
        EditorShowLayerOf(member.Path);
        if (_catalog?.Find(member.Path) is not { } entry)
            return;
        _spawnSelected = entry;
        _spawnTab = entry.Tab;
        _spawnCategory = -1;
        _spawnQuery = string.Empty;
        _spawnAbstract |= entry.Abstract;
        _paletteHelpersOnly &= entry.Mapping;
        _spawnFavouritesOnly = false;
        _spawnScrollToSelected = true;
    }

    // ---- the menus ----

    // A panel is shown and brought to the front of its dock.
    private void ShowPanel(string title)
    {
        (TabContainer dock, int tab) = PanelOf(title);
        if (dock == null)
            return;
        dock.SetTabHidden(tab, false);
        dock.CurrentTab = tab;
    }

    // A panel is a tab of the dock it was dragged to: found by its name.
    private (TabContainer Dock, int Tab) PanelOf(string title)
    {
        foreach (TabContainer dock in _shell.Docks)
        {
            for (int i = 0; i < dock.GetTabCount(); i++)
            {
                if (dock.GetTabControl(i).Name.ToString() == title)
                    return (dock, i);
            }
        }
        return (null, -1);
    }

    private void TogglePanel(string title)
    {
        (TabContainer dock, int tab) = PanelOf(title);
        if (dock == null)
            return;
        dock.SetTabHidden(tab, !dock.IsTabHidden(tab));
    }

    private void SyncViewMenu()
    {
        foreach ((string action, bool on) in new[]
        {
            ("view.simple", _editorSimple), ("view.top", _editorTop), ("view.under", _editor.Feed.UnderFloor), ("view.markers", _editor.Feed.Markers), ("view.areas", _editorAreas),
            ("view.border", _editorBorderOn), ("view.docks", _editorDocksOn), ("view.preview", _editorPreviewOn), ("view.arrows", _editorArrowsOn), ("level.above", _editorAbove), ("level.lock", _editorLevelLock),
        })
            _menuView.SetItemChecked(ItemIndex(_menuView, action), on);
        foreach (string title in EditorPanels)
        {
            (TabContainer dock, int tab) = PanelOf(title);
            _menuView.SetItemChecked(ItemIndex(_menuView, PanelAction(title)), dock != null && !dock.IsTabHidden(tab));
        }
    }

    // ---- every frame: the interface shows the editor's state ----

    private void UpdateEditorShell()
    {
        MapDocument map = _editor.Document;
        double now = App.NowSeconds;
        if (_editorNewAsked)
        {
            _editorNewAsked = false;
            EditorAskNew(shuttle: false);
        }
        for (int i = 0; i < _toolButtons.Length; i++)
            _toolButtons[i].SetPressedNoSignal((int)_editorTool == i);
        _undoButton.Disabled = !map.CanUndo;
        _undoButton.TooltipText = (map.CanUndo ? "Take back: " + map.UndoLabel : "Nothing to take back") + KeyLine("edit.undo");
        _redoButton.Disabled = !map.CanRedo;
        _redoButton.TooltipText = (map.CanRedo ? "Do again: " + map.RedoLabel : "Nothing to do again") + KeyLine("edit.redo");
        _menuFile.SetItemDisabled(ItemIndex(_menuFile, "file.save"), !_editor.NeedsSave);
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.undo"), !map.CanUndo);
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.redo"), !map.CanRedo);
        bool selection = _editorRegion != null || _editorSelected != null;
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.cut"), !selection);
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.copy"), !selection);
        // (with the system's clipboard in use another editor's copy may be there: Paste looks, and says when there is none)
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.paste"), _editorClip == null && !_editorClipboard);
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.delete"), _editorRegion == null && _editorSelected is not { Member: >= 0 or EditorCeilingMember });
        // (with nothing selected the turn is the place tool's: what it places faces the next way)
        bool turns = selection || _editorTool == EditorTool.Place;
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.turn"), !turns);
        _menuEdit.SetItemDisabled(ItemIndex(_menuEdit, "edit.turnback"), !turns);
        _turnButton.Disabled = !turns;
        if (_shapeOption.Selected != (int)_editorDraw)
            _shapeOption.Selected = (int)_editorDraw;
        _shapeOption.Disabled = _editorTool is not (EditorTool.Place or EditorTool.Erase);
        if (_modeOption.Selected != (int)_editorPlaceMode)
            _modeOption.Selected = (int)_editorPlaceMode;
        _modeOption.Disabled = _editorTool != EditorTool.Place;
        // (a map of one level has no level to go to and no height to build up through)
        if ((int)_heightSpin.MaxValue != map.Levels)
            _heightSpin.MaxValue = map.Levels;
        if ((int)_heightSpin.Value != _editorHeight)
            _heightSpin.SetValueNoSignal(_editorHeight);
        _heightSpin.Editable = map.Levels > 1 && _editorTool is EditorTool.Place or EditorTool.Erase;
        _levelCount.Text = Inv($"{_editorLevel} of {map.Levels}");
        _levelDown.Disabled = _editorLevel <= 1;
        _levelUp.Disabled = _editorLevel >= map.Levels;
        _lockButton.SetPressedNoSignal(_editorLevelLock);
        _lockButton.Disabled = map.Levels <= 1;
        _aboveButton.SetPressedNoSignal(_editorAbove);
        _aboveButton.Disabled = map.Levels <= 1;
        _arrowsButton.SetPressedNoSignal(_editorArrowsOn);
        _simpleButton.SetPressedNoSignal(_editorSimple);
        _topButton.SetPressedNoSignal(_editorTop);
        _trayButton.SetPressedNoSignal(_editor.Feed.UnderFloor);
        for (int i = 0; i < _lookButtons.Length; i++)
        {
            _lookButtons[i].SetPressedNoSignal(_editorLook[i]);
            // (the shadows and the dark are the light's: with every surface lit there are none)
            _lookButtons[i].Disabled = _editorSimple || ((EditorLook)i is EditorLook.Shadows or EditorLook.Dark && !_editorLook[(int)EditorLook.Lighting]);
        }
        _playButton.Text = _playStarting == null ? "Play" : Inv($"Starting... {now - _playAskedAt:0} s");
        _playButton.TooltipText = _playStarting == null
            ? "Play the map as it is, the changes that are not saved too: a round of the game starts and you stand on the selected tile, or where the view looks; or, by the choice beside this button, you arrive on the map's arrivals shuttle or stand on its nearest spawn point. You are an admin there. F8 comes back." + KeyLine("play.start")
            : "The round is starting, as a server does. Press to give it up.";
        int playFrom = _playArrive ? 1 : _playAtStart ? 2 : 0;
        if (_playStartOption.Selected != playFrom)
            _playStartOption.Selected = playFrom;
        _titleLabel.Text = Inv($"{_editor.Title}   {map.Width} x {map.Height} x {map.Levels}");
        string title = ClientArgs.EditorName + ": " + _editor.Title;
        if (GetWindow().Title != title)
            GetWindow().Title = title;

        Vector2 mouse = GetViewport().GetMousePosition();
        bool offMap = !_editorOnMap && !_editorLooking && _shell.OverView(mouse); // the mouse is over the view and on no tile of the map: a tool does nothing there
        _shell.Status.Text = _editorLooking ? EditorCameraStatus() : _editorOnMap ? EditorHoverText() : offMap ? Inv($"Off the map: no tile is under the mouse (the map's border is the yellow lines; {KeyOf("view.home")} goes to its first tile).")
            : _editorPasting ? "Paste: click where the copy goes. Esc: do not." : EditorToolStatus((int)_editorTool);
        Vector3 eye = _rig.FreePosition;
        _shell.Camera.Text = Inv($"camera {Mathf.FloorToInt(eye.X / TileSpace.TileSize) + 1}, {Mathf.FloorToInt(-eye.Z / TileSpace.TileSize) + 1}, {_editorLevel}   {eye.Y - EditorFloorY:0} m up");
        _shell.Notice.Text = _editorNotice != null && now < _editorNoticeUntil ? _editorNotice : string.Empty;
        _shell.State.Text = Inv($"{(_editor.Feed.Shown ? string.Empty : "showing the change...   ")}v{BuildInfo.Display}   {Engine.GetFramesPerSecond():0} fps");
        _shell.Tip(_editorOnMap && !_editorLooking ? EditorTipText() : null, mouse);

        UpdatePalettePanel();
        UpdateTilePanel();
        UpdatePropertiesPanel();
        UpdateMapPanel();
        UpdateSearchPanel(now);
        UpdateChecksPanel(now);
        KeepEditorSettings(now);
        UpdateEditorAutosave(now);
        if (now >= _logNextAt && _logText.IsVisibleInTree())
        {
            _logNextAt = now + 0.5;
            IReadOnlyCollection<string> log = _editor.Feed.Log;
            if (log.Count != _logShownCount)
            {
                _logShownCount = log.Count;
                // the world's notes of what is not translated yet, of its speed and of who joined are not a mapper's to read
                bool Meant(string line) => !EditorLogNoise.Any(noise => line.StartsWith(noise, StringComparison.Ordinal));
                IEnumerable<string> played = App.Instance.LastPlayLog.Where(Meant).TakeLast(200);
                _logText.Text = string.Join("\n", log.Where(Meant).TakeLast(200).Concat(played.Any() ? played.Prepend("---- the last round played ----") : played));
            }
        }
    }

    private void UpdatePalettePanel()
    {
        if (_catalog == null)
        {
            _paletteCount.Text = _catalogFailed ? "tg's types could not be read (see the log)" : "Reading tg's types...";
            return;
        }
        // the controls follow the state: a pick off the map changes the list that is shown
        if (_paletteTabs.CurrentTab != (int)_spawnTab)
        {
            _paletteTabs.CurrentTab = (int)_spawnTab;
            FillPaletteCategories();
        }
        if (_paletteCategory.Selected != _spawnCategory + 1)
            _paletteCategory.Selected = Math.Clamp(_spawnCategory + 1, 0, _paletteCategory.ItemCount - 1);
        if (_paletteSearch.Text != _spawnQuery && !_paletteSearch.HasFocus())
            _paletteSearch.Text = _spawnQuery;
        _paletteFavourites.SetPressedNoSignal(_spawnFavouritesOnly);
        _paletteHelpers.SetPressedNoSignal(_paletteHelpersOnly);
        _paletteAbstract.SetPressedNoSignal(_spawnAbstract);
        if (_paletteDir.Selected != _spawnDirIndex)
            _paletteDir.Selected = _spawnDirIndex;

        string key = Inv($"{_spawnTab}|{_spawnCategory}|{_paletteHelpersOnly}|{_spawnAbstract}|{_spawnQuery}|{_spawnFavouritesOnly}|{_favouritesVersion}");
        if (key != _paletteShownKey)
        {
            bool sameList = _paletteShownKey != null && key.Substring(0, key.LastIndexOf('|')) == _paletteShownKey.Substring(0, _paletteShownKey.LastIndexOf('|'));
            _paletteShownKey = key;
            _catalog.Filter(_spawnTab, _spawnCategory, _spawnQuery, mapping: true, _spawnAbstract, _spawnList, _spawnFavouritesOnly ? _favourites[(int)_spawnTab] : null);
            if (_paletteHelpersOnly)
                _spawnList.RemoveAll(entry => !entry.Mapping);
            SpawnRowsOfList();
            _paletteList.Tree = _spawnTree != null;
            _paletteList.SetRows(_spawnRows, keepScroll: sameList);
            _paletteCount.Text = Inv($"{_spawnList.Count} types");
        }
        _paletteList.Selected = _spawnSelected;
        if (_spawnScrollToSelected)
        {
            _spawnScrollToSelected = false;
            if (SpawnUnfoldTo(_spawnSelected))
                _paletteList.SetRows(_spawnRows, keepScroll: true); // (a type picked off the map: the branches over it open)
            _paletteList.ScrollTo(_spawnSelected);
        }
        _paletteList.QueueRedraw();

        if (_spawnSelected is { } entry)
        {
            _paletteName.Text = entry.Name;
            _palettePath.Text = entry.Path;
            _paletteIcon.Texture = _thumbs.TryGet(entry.Icon, entry.IconState, out Thumb thumb) ? thumb.Texture : null;
        }
        _paletteBrush.Text = _editorBrush is { } brush ? Inv($"picked with {brush.Overrides?.Count ?? 0} vars set (choose a type for the bare one)") : string.Empty;
    }

    private const string CeilingTip = "What is over the tile (over every tile of a selected rectangle): the game's rule (plating over what is indoors, nothing over space and outdoor areas), nothing, or a ceiling of your choice. It is the ceiling var of the tile's turf, kept in the map; see it from under the walls.";
    private const string CeilingAboveTip = "This level has a level over it: the floor of that level is the ceiling here. A floor's type chosen here is laid under that floor's plating (glass takes the floor's place; plating, or the game's rule, takes a laid tile off). Nothing, a lattice or a catwalk mean nothing here: take the floor of the level above away instead.";

    // The level the selection is on has its ceilings in the map's own word (the ceiling var): a level with a level over it has that level's floors for
    // them, and its own word lays a tile under their plating (5.18).
    private bool EditorOwnCeilings(out int level)
    {
        level = _editorRegion?.Z ?? _editorSelected?.Z ?? 0;
        return level == 0 || !_map.LevelAbove(level);
    }

    // The ceiling over the selected tile, or over every tile of the selected rectangle, as one step.
    private void EditorSetCeiling(string ceiling)
    {
        MapDocument map = _editor.Document;
        bool under = !EditorOwnCeilings(out _); // (a level under another: the word lays a tile under the plating of the level above)
        if (under && ceiling != null && MapEdits.CeilingFrom(ceiling) != ceiling)
        {
            EditorNotice(CeilingAboveTip, 10);
            return;
        }
        try
        {
            var tiles = new List<(int X, int Y, int Z, MapModel Model)>();
            foreach ((int x, int y, int z) in EditorSelectionCells().Where(c => map.Contains(c.X, c.Y, c.Z)))
            {
                MapModel tile = map[x, y, z];
                if (!under || ceiling == null)
                {
                    MapModel set = MapEdits.WithCeiling(map, tile, ceiling);
                    if (!ReferenceEquals(set, tile))
                        tiles.Add((x, y, z, set));
                    continue;
                }
                MapModel over = map[x, y, z + 1];
                (MapModel room, MapModel slab) = MapEdits.Ceiled(map, tile, over, new MapMember(ceiling, null));
                if (!ReferenceEquals(room, tile))
                    tiles.Add((x, y, z, room));
                if (!ReferenceEquals(slab, over))
                    tiles.Add((x, y, z + 1, slab));
            }
            if (tiles.Count > 0 && !map.InStep)
                map.Edit(Inv($"Ceiling ({tiles.Count} tiles)"), tiles);
        }
        catch (MapTextException e)
        {
            EditorNotice("Not changed: " + e.Message);
        }
    }

    // The ceiling the Tile panel shows: the selected tile's, or the selected rectangle's when all its tiles have the same.
    private void UpdateTileCeiling()
    {
        MapDocument map = _editor.Document;
        bool any = _editorRegion != null || _editorSelected != null;
        _tileCeilingRow.Visible = any;
        if (!any)
            return;
        string ceiling = null;
        bool mixed = false;
        if (_editorRegion is { } r)
        {
            bool first = true;
            foreach ((int x, int y) in EditorRegionCells())
            {
                if (!map.Contains(x, y, r.Z))
                    continue;
                string here = MapEdits.CeilingOf(map[x, y, r.Z]);
                (ceiling, mixed, first) = first ? (here, false, false) : (ceiling, mixed || here != ceiling, false);
            }
        }
        else if (_editorSelected is { } s)
            ceiling = MapEdits.CeilingOf(map[s.X, s.Y, s.Z]);
        int index = mixed ? EditorCeilings.Length : Array.FindIndex(EditorCeilings, c => c.Value == ceiling);
        if (index < 0)
            index = EditorCeilings.Length;
        if (_tileCeiling.Selected != index)
            _tileCeiling.Selected = index;
        // (under a level above the list says why it sets nothing; a var set there before can still be given back to the rule)
        string tip = EditorOwnCeilings(out _) ? CeilingTip : CeilingAboveTip;
        if (_tileCeiling.TooltipText != tip)
            _tileCeiling.TooltipText = tip;
    }

    private void UpdateTilePanel()
    {
        UpdateTileCeiling();
        UpdateTileGas();
        if (_editorSelected is not { } selected)
        {
            if (_tileShown.Tile != null)
            {
                _tileShown = default;
                _tileTree.Clear();
                _tileDefault.Visible = false;
            }
            string idle = _editorRegion is { } r
                ? (_editorMoreRegions.Count > 0 ? Inv($"{_editorMoreRegions.Count + 1} rectangles, {EditorRegionCells().Count} tiles.")
                    : Inv($"{Math.Abs(r.X1 - r.X0) + 1} x {Math.Abs(r.Y1 - r.Y0) + 1} tiles from {Math.Min(r.X0, r.X1)}, {Math.Min(r.Y0, r.Y1)}."))
                    + Inv($" Drag them to move them; copy, cut, turn ({KeyOf("edit.turn")}) or delete them. Shift + drag selects one more rectangle.")
                : "Select a thing or a tile on the map.";
            if (_tileTitle.Text != idle)
                _tileTitle.Text = idle;
            return;
        }
        MapDocument map = _editor.Document;
        MapModel tile = map[selected.X, selected.Y, selected.Z];
        var shown = (tile, selected.X, selected.Y, selected.Z, selected.Member, _tilePicturesWaiting ? _thumbs.Count : -1);
        if (shown == _tileShown)
            return;
        bool rows = !ReferenceEquals(_tileShown.Tile, tile) || (_tileShown.X, _tileShown.Y, _tileShown.Z) != (selected.X, selected.Y, selected.Z) || _tileShown.Thumbs != shown.Item6;
        _tileShown = shown;
        _tileTitle.Text = selected.Member == EditorCeilingMember
            ? _map.LevelAbove(selected.Z)
                ? Inv($"The ceiling over tile {selected.X}, {selected.Y}, {selected.Z}: {EditorCeilingName(MapEdits.LaidCeiling(tile))}, laid under the plating of level {selected.Z + 1}. {KeyOf("edit.delete")} takes it off; Ceiling below lays another.")
                : Inv($"The ceiling over tile {selected.X}, {selected.Y}, {selected.Z}: {EditorCeilingName(EditorCeilingAt(selected.X, selected.Y, selected.Z))}. {KeyOf("edit.delete")} takes its top layer off; Ceiling below sets it.")
            : Inv($"Tile {selected.X}, {selected.Y}, {selected.Z}");
        _tileDefault.Visible = true;
        _tileDefault.Disabled = ReferenceEquals(tile, map.Empty(selected.Z));
        if (rows)
        {
            _tilePicturesWaiting = false;
            _tileTree.Clear();
            TreeItem root = _tileTree.CreateItem();
            for (int i = 0; i < tile.Members.Count; i++)
            {
                MapMember member = tile.Members[i];
                SpawnEntry entry = _catalog?.Find(member.Path);
                TreeItem item = _tileTree.CreateItem(root);
                int set = member.Overrides?.Count ?? 0;
                string onPlating = MapEdits.IsTurf(member.Path) && MapEdits.LayerOf(member.Path) != TurfLayer.Base ? "   (on plating)" : string.Empty;
                item.SetText(0, (entry?.Name ?? member.Path.Substring(member.Path.LastIndexOf('/') + 1)) + (set > 0 ? Inv($"   ({set} vars set)") : string.Empty) + onPlating + "\n" + member.Path);
                item.SetTooltipText(0, member.Path);
                item.SetMetadata(0, i);
                if (entry != null)
                {
                    if (!_thumbs.TryGet(entry.Icon, entry.IconState, out Thumb thumb))
                        _tilePicturesWaiting = true;
                    else if (thumb.Texture != null)
                        item.SetIcon(0, thumb.Texture);
                }
                if (MapEdits.IsThing(member.Path))
                    item.AddButton(0, _crossIcon, i, false, "Take it away");
            }
        }
        // the selected member's row
        for (TreeItem item = _tileTree.GetRoot()?.GetFirstChild(); item != null; item = item.GetNext())
        {
            if ((int)item.GetMetadata(0) == selected.Member)
                item.Select(0);
            else
                item.Deselect(0);
        }
    }

    // Every var of the selected member's type with its value: the member's own where the map sets it (brighter, with a button that gives the type's
    // value back), else the type's. A value cell is edited in place.
    private void UpdatePropertiesPanel()
    {
        if (_editorSelected is not { Member: >= 0 } selected)
        {
            if (_propsShown.Tile != null || _propsTree.GetRoot() != null)
            {
                _propsShown = default;
                _propsTree.Clear();
                _propsTitle.Text = "Select a member of a tile to see its vars.";
                _propsThing.Text = string.Empty;
            }
            _propsPick.Disabled = _propsSet.Disabled = true;
            return;
        }
        _propsPick.Disabled = _propsSet.Disabled = false;
        MapModel tile = _editor.Document[selected.X, selected.Y, selected.Z];
        MapMember member = tile.Members[selected.Member];
        long thing = MapEdits.IsThing(member.Path) ? _editor.Feed.ThingOf(selected.X, selected.Y, selected.Z, selected.Member) : 0;
        var shown = (tile, selected.Member, _propsSearch.Text, _propsSetOnly.ButtonPressed, thing);
        if (shown == _propsShown)
            return;
        bool rows = !ReferenceEquals(_propsShown.Tile, tile) || _propsShown.Member != selected.Member || _propsShown.Filter != shown.Item3 || _propsShown.SetOnly != shown.Item4;
        _propsShown = shown;
        _propsTitle.Text = member.Path;
        _propsThing.Text = !MapEdits.IsThing(member.Path) ? string.Empty : thing != 0 ? Inv($"shown as thing #{thing}") : "not drawn (the game's init takes it away or hides it)";
        if (!rows)
            return;

        _propsTree.Clear();
        TreeItem root = _propsTree.CreateItem();
        IReadOnlyList<TypeVar> known = TypeVars.Of(member.Path);
        var listed = new HashSet<string>(StringComparer.Ordinal);
        string filter = _propsSearch.Text;
        var dim = new Color(0.62f, 0.63f, 0.66f);
        void Row(string name, string def, string set)
        {
            if (!listed.Add(name) || !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return;
            TreeItem item = _propsTree.CreateItem(root);
            item.SetText(0, name);
            item.SetText(1, set ?? def ?? string.Empty);
            item.SetMetadata(1, def ?? string.Empty);
            item.SetEditable(1, true);
            item.SetTooltipText(0, name);
            item.SetTooltipText(1, set != null ? "Set on this member. The type's own value: " + (def ?? "an object or a list") : def == null ? "The type's value is an object or a list: type a literal to set the var on this member" : "The type's own value: edit it to set the var on this member");
            if (set != null)
                item.AddButton(1, _crossIcon, 0, false, "The type's own value again");
            else
                item.SetCustomColor(0, dim);
            if (set == null)
                item.SetCustomColor(1, dim);
        }
        // the vars the member sets first, a var its type's class does not have among them
        foreach (MapOverride o in member.Overrides ?? Array.Empty<MapOverride>())
            Row(o.Var, known.FirstOrDefault(v => v.Name == o.Var)?.Default, o.Value.Literal);
        if (!_propsSetOnly.ButtonPressed)
        {
            foreach (TypeVar v in known)
                Row(v.Name, v.Default, null);
        }
    }

    // ---- the search (5.5) ----

    private Control BuildSearchPanel(float s)
    {
        var column = new VBoxContainer();
        var row = new HBoxContainer();
        _searchField = new LineEdit { PlaceholderText = "Find on the map: a type's path or name, a var the map sets (airlock command, name = \"Bridge\")", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Text = _searchQuery };
        _searchField.TextSubmitted += text => EditorFind(text, show: false);
        _searchField.TextChanged += text =>
        {
            if (text.Length == 0)
                EditorFind(text, show: false); // (the field emptied: its finds' marks go from the map)
        };
        var find = new Button { Text = "Find", FocusMode = Control.FocusModeEnum.None };
        find.Pressed += () => EditorFind(_searchField.Text, show: false);
        _searchCount = new Label();
        _searchCount.AddThemeColorOverride("font_color", new Color(0.62f, 0.63f, 0.66f));
        row.AddChild(_searchField);
        row.AddChild(find);
        row.AddChild(_searchCount);
        column.AddChild(row);
        _searchTree = new Tree { HideRoot = true, Columns = 3, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = Tree.SelectModeEnum.Row, FocusMode = Control.FocusModeEnum.Click };
        _searchTree.SetColumnExpand(0, false);
        _searchTree.SetColumnCustomMinimumWidth(0, Mathf.RoundToInt(96 * s));
        _searchTree.SetColumnExpandRatio(1, 1);
        _searchTree.SetColumnExpandRatio(2, 3);
        _searchTree.ItemSelected += () =>
        {
            if (_searchTree.GetSelected() is { } item)
                EditorGoTo(_searchFound[(int)item.GetMetadata(0)]);
        };
        column.AddChild(_searchTree);
        return column;
    }

    // The words are looked for on the map; show: the Search panel comes to the front (the console's line).
    private void EditorFind(string query, bool show)
    {
        _searchQuery = query?.Trim() ?? string.Empty;
        _searchShown = default;
        if (!show)
            return;
        _searchField.Text = _searchQuery;
        ShowPanel("Search");
    }

    // The list follows the words and the map: looked for again when either changed, a few times a second at most.
    private void UpdateSearchPanel(double now)
    {
        MapDocument map = _editor.Document;
        // (with words to look for it goes on behind another panel too: its finds are marked on the map)
        if (_searchShown == (_searchQuery, map.Revision) || now < _searchNextAt || (!_searchTree.IsVisibleInTree() && _searchQuery.Length == 0 && _searchMarked.Count == 0))
            return;
        _searchNextAt = now + 0.3;
        _searchShown = (_searchQuery, map.Revision);
        List<MapFound> all = MapSearch.Find(map, _searchQuery, path => _catalog?.Find(path)?.Name, SearchMarks, out int total);
        _searchFound = all.Count > SearchRows ? all.GetRange(0, SearchRows) : all;
        _searchMarked.Clear();
        _searchMarked.AddRange(all.Select(found => (found.X, found.Y, found.Z)).Distinct());
        _searchCount.Text = _searchQuery.Length == 0 ? string.Empty : total == 0 ? "nothing found" : total > _searchFound.Count ? Inv($"{total} found, the first {_searchFound.Count} listed") : Inv($"{total} found");
        _searchTree.Clear();
        TreeItem root = _searchTree.CreateItem();
        for (int i = 0; i < _searchFound.Count; i++)
        {
            MapFound found = _searchFound[i];
            MapMember member = map[found.X, found.Y, found.Z].Members[found.Member];
            TreeItem item = _searchTree.CreateItem(root);
            item.SetText(0, Inv($"{found.X}, {found.Y}, {found.Z}"));
            item.SetText(1, _catalog?.Find(member.Path)?.Name ?? member.Path.Substring(member.Path.LastIndexOf('/') + 1));
            item.SetText(2, MapDocument.TextOf(new[] { member }));
            item.SetMetadata(0, i);
        }
        if (_searchQuery != _searchLogged)
            GD.Print(Inv($"[anomaly] editor: find '{_searchQuery}': {total} found"));
        _searchLogged = _searchQuery;
    }

    // A found member is selected, and the camera goes over its tile, looking the way it looks.
    private void EditorGoTo(MapFound found)
    {
        if (!_editor.Document.Contains(found.X, found.Y, found.Z))
            return;
        (_editorSelected, _editorRegion) = ((found.X, found.Y, found.Z, found.Member), null);
        var target = new Vector3(TileSpace.WorldX(found.X), TileSpace.WorldY(found.Z), TileSpace.WorldZ(found.Y));
        EditorLookAt(target, Mathf.Clamp((_rig.FreePosition.Y - target.Y) / Mathf.Max(0.3f, -_rig.Forward.Y), 6f, 80f));
    }

    // The status bar's line while a mouse button holds the camera: what the mouse and the keys do now.
    private string EditorCameraStatus() => _editorCamera switch
    {
        EditorCamera.Fly => Inv($"Flying: the mouse turns the view, {KeyOf("cam.forward")} {KeyOf("cam.left")} {KeyOf("cam.back")} {KeyOf("cam.right")} {KeyOf("cam.down")} {KeyOf("cam.up")} move, {KeyOf("cam.fast")} is faster, the wheel changes the speed (x{_editorFlySpeed:0.##})."),
        EditorCamera.Orbit => "Turning the view around what is selected, or the middle of the view. With Shift: moving the view.",
        _ => _editorTop ? Inv($"Moving the map. {KeyOf("cam.forward")} {KeyOf("cam.left")} {KeyOf("cam.back")} {KeyOf("cam.right")} move it too, {KeyOf("cam.down")} and {KeyOf("cam.up")} change its scale.") : "Moving the view.",
    };

    // The status bar's line for what is under the mouse.
    private string EditorHoverText()
    {
        (int x, int y, int z) = _editorCell;
        string under = _editorHit.Kind == TargetKind.Entity && _feed.Replica.TryGet(_editorHit.Id, out ReplicaEntity thing)
            ? thing.State.Name + "   " + thing.TypePath
            : _editorHit.Kind == TargetKind.Ceiling ? "ceiling: " + EditorCeilingName(EditorCeilingAt(_editorHit.X, _editorHit.Y, _editorHit.Z))
            : _editorSlab && MapEdits.LaidCeiling(_editor.Document[_editorRoom.X, _editorRoom.Y, _editorRoom.Z]) is { } laid
                ? "ceiling: " + EditorCeilingName(laid) + " under " + _feed.Replica.TurfTypeAt(_editorHit.X, _editorHit.Y, _editorHit.Z)
            : _feed.Replica.TurfTypeAt(_editorHit.X, _editorHit.Y, _editorHit.Z);
        string area = _editor.Document[x, y, z].Members.Count > 0 ? _editor.Document[x, y, z].Members[^1].Path : string.Empty;
        return Inv($"{x}, {y}, {z}   {under}   {area}");
    }

    // The line at the mouse over the view: what a click of the armed tool does there.
    private string EditorTipText()
    {
        (int x, int y, int z) = _editorCell;
        ReplicaEntity hit = null;
        bool thing = _editorHit.Kind == TargetKind.Entity && _feed.Replica.TryGet(_editorHit.Id, out hit);
        switch (_editorTool)
        {
            case EditorTool.Place when EditorBrush() is { } roofing && (EditorDragCeiling ?? EditorOnCeiling(roofing)):
                return MapEdits.CeilingFrom(roofing.Path) == null ? "a ceiling is a floor, a lattice or a catwalk: choose one to place here"
                    : _editorDragFrom is { } corner ? Inv($"ceiling of {EditorBrushName()}: {EditorShapes[(int)_editorDraw].Name.ToLowerInvariant()} from {corner.X}, {corner.Y} to {x}, {y}")
                    : Inv($"ceiling of {EditorBrushName()} over {x}, {y}, {z} (now: {EditorCeilingName(EditorCeilingAt(x, y, z))})");
            case EditorTool.Place when EditorBrush() is { } brush:
                if (_editorDragFrom is { } from)
                    return Inv($"place {EditorBrushName()}: {EditorShapes[(int)_editorDraw].Name.ToLowerInvariant()} from {from.X}, {from.Y} to {x}, {y}");
                if (_editorDraw == MapShape.Zone)
                    return Inv($"place {EditorBrushName()} on the zone of {x}, {y}, {z}");
                string wall = MapEdits.Mounted(brush, _editorMountSide).Equals(brush) ? string.Empty
                    : " on the " + (_editorMountSide switch { 1 => "north", 2 => "south", 4 => "east", _ => "west" }) + " wall";
                string high = _editorHeight > 1 ? Inv($", {Math.Min(_editorHeight, _editor.Document.Levels - z + 1)} levels up") : string.Empty;
                if (_editorPlaceMode == EditorPlaceMode.Replace)
                {
                    string target = EditorMemberUnderMouse() is { Member: >= 0 } under ? _editor.Document[under.X, under.Y, under.Z].Members[under.Member].Path : MapEdits.TurfOf(_editor.Document[x, y, z]);
                    return Inv($"replace {_catalog?.Find(target)?.Name ?? target} at {x}, {y}, {z} with {EditorBrushName()}{high}");
                }
                if (EditorOnSlabs && _editorDragFrom == null && !_editorStroke && z > 1)
                {
                    // from under a level: its floor is the ceiling here. Glass takes the floor's place; any other tile is laid under its plating
                    MapModel over = _editor.Document[x, y, z], room = _editor.Document[x, y, z - 1];
                    (MapModel newRoom, MapModel newOver) = MapEdits.Ceiled(_editor.Document, room, over, brush);
                    bool wallOver = MapEdits.LayerOf(MapEdits.TurfOf(over)) == TurfLayer.Wall;
                    if (ReferenceEquals(newRoom, room) && ReferenceEquals(newOver, over))
                        return wallOver && TurfStyle.GlassFloor(brush.Path) ? Inv($"a wall of level {z} stands here: glass takes a floor's place, not a wall's")
                            : MapEdits.IsThing(brush.Path) ? Inv($"the floor of level {z} is the ceiling here: a lattice goes where there is none") : Inv($"the ceiling here is {EditorBrushName()} already");
                    if (MapEdits.IsThing(brush.Path))
                        return Inv($"ceiling: {EditorBrushName()} on level {z} at {x}, {y}");
                    if (TurfStyle.GlassFloor(brush.Path))
                        return Inv($"ceiling: {EditorBrushName()} in the place of the floor of level {z} at {x}, {y}, seen through from both sides");
                    string made = ReferenceEquals(newOver, over) ? string.Empty : ", on new plating";
                    return MapEdits.LayerOf(brush.Path) == TurfLayer.Base ? Inv($"ceiling: bare plating under level {z} at {x}, {y}{made}")
                        : Inv($"ceiling: {EditorBrushName()} under the plating of level {z} at {x}, {y}{made}");
                }
                if (_editorSlab && (x, y, z) == _editorRoom && !EditorOnCeiling(brush))
                    return Inv($"place {EditorBrushName()} at {x}, {y}, {z}, under the ceiling{high}");
                if (_editorPlaceMode == EditorPlaceMode.Attach && (x, y, z) != (_editorHit.X, _editorHit.Y, _editorHit.Z))
                    return Inv($"attach {EditorBrushName()} at {x}, {y}, {z}{wall}{(z > _editorHit.Z ? ": on the level above" : string.Empty)}{high}");
                return Inv($"place {EditorBrushName()} at {x}, {y}, {z}{wall}{high}");
            case EditorTool.Place:
                return "pick a type in the palette first";
            case EditorTool.Erase:
            {
                // what the tool takes: what is highlighted, or what the button went down on
                bool held = _editorStroke || _editorDragFrom != null;
                EditorErase kind = held ? _editorErase : EditorEraseFor(Input.IsKeyPressed(Key.Shift));
                string what = kind switch
                {
                    EditorErase.All => "clear the tile: the level's empty tile",
                    EditorErase.Area => "take the area away: the map's default area is left, the turf and the things stay",
                    EditorErase.Thing => thing && !held && _editorDraw == MapShape.Free ? "take away " + hit.State.Name : "take the things away",
                    EditorErase.Wall => "take the wall away: its plating stays",
                    EditorErase.Tile => "take the floor tile off: its plating stays",
                    EditorErase.Base => ReferenceEquals(MapEdits.Peel(_editor.Document, _editor.Document[x, y, z], null, _editor.Document.Empty(z)), _editor.Document[x, y, z]) && !held ? "nothing to take away here" : "take the floor away: the level's empty turf is left",
                    EditorErase.Laid => held ? "take the ceilings' tiles off: the plating stays" : "take the ceiling's tile off: the plating stays",
                    EditorErase.Slab => held ? "take the floors away from under them" : MapEdits.LayerOf(MapEdits.TurfOf(_editor.Document[x, y, z])) == TurfLayer.Wall
                        ? Inv($"take the wall of level {z} away with its plating, the ceiling here") : Inv($"take the floor of level {z} away, the ceiling here"),
                    _ => EditorCeilingAt(x, y, z) is { } roof || held ? Inv($"take the ceiling's top layer off{(held ? string.Empty : " (" + EditorCeilingName(EditorCeilingAt(x, y, z)) + ")")}") : "no ceiling here",
                };
                if (_editorDragFrom is { } start)
                    return Inv($"{what}: {EditorShapes[(int)_editorDraw].Name.ToLowerInvariant()} from {start.X}, {start.Y} to {x}, {y}");
                string whole = kind == EditorErase.All || held ? string.Empty : "   (Shift: the whole tile)";
                return _editorDraw == MapShape.Zone ? Inv($"{what}, over the zone of {x}, {y}, {z}{whole}") : _editorDraw != MapShape.Free ? "drag: " + what + whole : what + whole;
            }
            case EditorTool.Select when _editorPasting:
                return _editorClip?.Thing is { } copied ? Inv($"paste {copied.Path} at {x}, {y}, {z}") : Inv($"paste {_editorClip?.Width} x {_editorClip?.Height} tiles here (Esc: do not)");
            case EditorTool.Pick:
                return thing ? "pick " + hit.State.Name : _editorHit.Kind == TargetKind.Ceiling ? "pick the ceiling: " + EditorCeilingName(EditorCeilingAt(x, y, z))
                    : "pick " + _feed.Replica.TurfTypeAt(_editorHit.X, _editorHit.Y, _editorHit.Z);
            default:
                return thing ? hit.State.Name : _editorHit.Kind == TargetKind.Ceiling ? "the ceiling: " + EditorCeilingName(EditorCeilingAt(x, y, z)) : null;
        }
    }

    // ---- the map's files ----

    private static string WithExtension(string path) => path.EndsWith(MapText.Extension, StringComparison.OrdinalIgnoreCase) ? path : path + MapText.Extension;

    // The operating system's own file window. picked: the chosen file's path.
    private void EditorFileDialog(FileDialog.FileModeEnum mode, string title, string[] filters, Action<string> picked, string folder = null)
    {
        var dialog = new FileDialog
        {
            FileMode = mode,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = title,
            Filters = filters,
            CurrentDir = folder != null && Directory.Exists(folder) ? folder : Path.GetDirectoryName(_editor.FilePath),
        };
        dialog.FileSelected += path =>
        {
            dialog.QueueFree();
            picked(path);
        };
        dialog.Canceled += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    // File, Recent maps (the owner, 2026-11-18): the maps opened and saved last, the newest first, kept in the editor's settings. The open map heads the
    // list once it has a file; the menu lists the others that are still there.
    private void NoteRecentMap()
    {
        if (_editor.OnDisk)
        {
            _editorSettings.Set("files", "recent", RecentMaps.With(_editorSettings.Texts("files", "recent"), _editor.FilePath));
            _editorSettingsKept = null; // (written at the next look at the settings)
        }
        string open = Path.GetFullPath(_editor.FilePath), home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        _recentShown = _editorSettings.Texts("files", "recent").Where(path => path != open && File.Exists(path)).ToArray();
        _menuRecent.Clear();
        foreach (string path in _recentShown)
        {
            string folder = Path.GetDirectoryName(path) ?? string.Empty;
            _menuRecent.AddItem(Path.GetFileName(path) + "   (" + (home.Length > 0 && folder.StartsWith(home, StringComparison.Ordinal) ? "~" + folder.Substring(home.Length) : folder) + ")");
        }
        _menuFile.SetItemDisabled(_menuFile.GetItemIndex(RecentMenuId), _recentShown.Length == 0);
    }

    private void SaveEditorMap() => EditorSaveTo(_editor.FilePath);

    private void EditorSaveAs()
        => EditorFileDialog(FileDialog.FileModeEnum.SaveFile, "Save the map as", new[] { "*.as3dmap ; Maps" }, path => EditorSaveTo(EditorSession.Placed(WithExtension(path))));

    private void EditorSaveTo(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            EditorNotice("Not saved: no file was named");
            return;
        }
        try
        {
            bool wasOfFolder = _editor.MapsFolder != null;
            _editor.SaveAs(path);
            DropAutosave(); // (the kept copy of its changes is of no use)
            // a map at a station's place of a maps folder is that folder's once it is saved, unless the json of its name there is another map's
            string joined = wasOfFolder ? string.Empty : _editor.MapsFolder != null ? " (the map of " + Path.GetFileName(_editor.JsonPath) + " in its maps folder now)"
                : EditorFeed.PlaceOf(_editor.FilePath) is { } place ? Inv($" (of no maps folder: map_jsons/{place.Name}.json there is another map's)") : string.Empty;
            EditorNotice("Saved " + _editor.FilePath + joined, joined.Length > 0 ? 12 : 6);
            GD.Print("[anomaly] editor: saved " + _editor.FilePath + joined);
            NoteRecentMap(); // (a new map's first save, a save under another name)
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            EditorNotice("Not saved: " + e.Message);
            GD.PrintErr("[anomaly] editor: not saved: " + e.Message);
        }
    }

    private void EditorOpenFile()
        => EditorFileDialog(FileDialog.FileModeEnum.OpenFile, "Open a map", new[] { "*.as3dmap, *.ss3dmap ; Maps" }, // .ss3dmap: a map saved before D347
            path => EditorOpen(path, _editor.Document.Width, _editor.Document.Height, _editor.Document.Levels));

    // Leaves the open map for the map of a file, or (create) for a new map of the given size that the file will hold once it is saved. The file is read
    // before the open map is left, so a file that is no map leaves the editor as it is.
    private void EditorOpen(string path, int width, int height, int levels, bool create = false, bool shuttle = false)
    {
        EditorSession next;
        try
        {
            next = create ? EditorSession.New(path, width, height, levels, shuttle ? EditorSession.NewShuttleMembers : EditorSession.NewMapMembers) : EditorSession.Open(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MapTextException or ArgumentException)
        {
            EditorNotice((create ? "Not made: " : "Not opened: ") + e.Message);
            return;
        }
        // (the scene is changed at the start of the next frame: a console line asks from the middle of this one, which would go on with the scene out of the tree)
        EditorGuard(() =>
        {
            // A new station at its place in a maps folder is saved at once, with its json: it is that folder's map from its first minute (its shuttle
            // slots, its Play with CentCom), and not a lone one until its first save.
            if (create && !shuttle && EditorFeed.PlaceOf(next.FilePath) != null)
            {
                try
                {
                    next.Save();
                    GD.Print("[anomaly] editor: saved " + next.FilePath + (next.MapsFolder != null ? " (the map of " + Path.GetFileName(next.JsonPath) + " in its maps folder)" : string.Empty));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    next.Dispose();
                    EditorNotice("Not made: " + e.Message);
                    return;
                }
            }
            _sceneChange = () => App.Instance.OpenEditor(next);
        });
    }

    private void EditorImportFile()
        => EditorFileDialog(FileDialog.FileModeEnum.OpenFile, "Import a .dmm map", new[] { "*.dmm ; BYOND maps" }, path => EditorImport(path, null));

    // A tg map (.dmm) converted into our format (tools/Anomaly.Tools.Maps' converter) beside it, or into the named file, and opened.
    private void EditorImport(string dmmPath, string mapPath)
    {
        try
        {
            Anomaly.Tools.Maps.ConversionResult converted = Anomaly.Tools.Maps.MapConverter.Convert(File.ReadAllText(dmmPath), Path.GetFileName(dmmPath), null, allowBadPaths: true, tgCommit: null);
            if (converted.Text == null)
            {
                EditorNotice("Not imported: " + string.Join("; ", converted.Findings.Take(3)));
                GD.PrintErr("[anomaly] editor: not imported: " + string.Join("; ", converted.Findings.Take(20)));
                return;
            }
            mapPath = string.IsNullOrWhiteSpace(mapPath) ? Path.ChangeExtension(dmmPath, MapText.Extension) : WithExtension(mapPath);
            if (File.Exists(mapPath))
            {
                EditorNotice("Not imported: " + mapPath + " exists already (open it, or import into another file)");
                return;
            }
            File.WriteAllText(mapPath, converted.Text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            GD.Print(Inv($"[anomaly] editor: imported {dmmPath} into {mapPath}: {converted.Width}x{converted.Height}x{converted.Levels}, {converted.Movables} things, {converted.BadPaths.Count} unknown paths or vars left out"));
            EditorOpen(mapPath, converted.Width, converted.Height, converted.Levels);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or FormatException)
        {
            EditorNotice("Not imported: " + e.Message);
            GD.PrintErr("[anomaly] editor: not imported: " + e.Message);
        }
    }
}
