using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Anomaly.Client.Editor;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The map's own settings in the map editor (docs/ai/specs/map-editor.md 5.10; R7 and R8): the Map panel, where the mapper names the station and sets
// the sky, the sun, the air a room and the outside start with, the mine and each level's weather, gravity and base turf; and the sky and the sun of
// the view, which show what is set at once, saved or not. The settings are a key of the map json (MapSettings), tg's own level traits and tg's
// minetype; a save writes them with the map.
public partial class GameScene
{
    // tg's weathers a level can have (the level traits Weather_...), with the names the panel shows
    private static readonly (string Name, string Trait)[] EditorWeathers =
    {
        ("None", null), ("Ash storms", "Weather_Ashstorm"), ("Snow storms", "Weather_Snowstorm"), ("Rain storms", "Weather_Rainstorm"),
        ("Sand storms", "Weather_Sandstorm"), ("Void storms", "Weather_Voidstorm"),
    };

    // the base turfs to choose from (tg's level trait Baseturf): what a turf of the level is when it is destroyed or scraped away
    private static readonly (string Name, string Turf)[] EditorBaseTurfs =
    {
        ("tg's own (space)", null), ("Open space over space", EditorSession.OpenSpace), ("Open air over a floor", "/turf/open/openspace"),
    };

    // the mines the game reads (the map json's minetype: tg's datum/map_config and SSmapping's loadWorld)
    private static readonly (string Name, string Mine)[] EditorMines =
    {
        ("tg's own (Lavaland)", null), ("None", "none"), ("Lavaland", "lavaland"), ("Ice (the map's own levels)", "ice"),
    };

    // gas mixes to start from, as tg writes one
    private static readonly (string Name, string Mix)[] EditorAirs =
    {
        ("tg's own", null), ("Station air", "o2=22;n2=82;TEMP=293.15"), ("Vacuum", "TEMP=2.7"), ("Cold thin air", "o2=14;n2=30;TEMP=180"),
        ("Hot thin air", "o2=14;n2=23;TEMP=320"), ("Carbon dioxide", "co2=100;TEMP=293.15"),
    };

    private static readonly string[] EditorSkies = { "Space (the game's own)", "The stars alone", "Two colours (a planet's sky)", "A picture" };

    private const int MapPanelLevels = 64;

    // the panel's controls
    private LineEdit _mapName, _mapStation, _mapAirInside, _mapAirOutside;
    private OptionButton _mapSky, _mapMine, _mapAirInsidePick, _mapAirOutsidePick;
    private ColorPickerButton _mapSkyTop, _mapSkyHorizon, _mapSunColour;
    private Control _mapSkyColours, _mapSkyPicture, _mapSunRows, _mapLevels;
    private Label _mapSkyFile;
    private HSlider _mapStars, _mapSunEnergy, _mapSunSize, _mapSunHeight, _mapSunAzimuth;
    private CheckBox _mapSun, _mapSunFixed;
    private MapSettings _mapShown; // the settings the controls show
    private (int Levels, string Traits) _mapLevelsShown;

    // the shuttles (5.11)
    private static readonly string[] EditorShuttleSlots = { "cargo", "emergency", "ferry", "whiteship" };
    // the shuttle of a slot the map json does not name (tg's map_config)
    private static readonly Dictionary<string, string> EditorShuttleDefaults = new() { ["cargo"] = "cargo_box", ["emergency"] = "emergency_box", ["ferry"] = "ferry_fancy", ["whiteship"] = "whiteship_box" };
    private static readonly Color ShuttleFine = new(0.45f, 0.85f, 0.5f), ShuttleWrong = new(1f, 0.42f, 0.38f), ShuttleNote = new(1f, 0.78f, 0.35f);
    private static readonly Color MarkDock = new(0.2f, 0.9f, 1f, 0.5f), MarkShuttle = new(1f, 0.4f, 0.9f, 0.5f);
    private Control _mapShuttles;
    private string _mapShuttlesShown;
    private bool _editorDocksOn = true; // the docks' rectangles are drawn
    private EditorCells _editorDockMarks;
    private (long Revision, List<MapDock> Docks) _editorDocks = (-1, new List<MapDock>());
    private readonly Dictionary<string, (DateTime Stamp, MapDocument Map, (DockSize Size, int Ports)? Shuttle)> _shuttleFiles = new();
    private readonly HashSet<string> _editorShuttlesOn = new(); // the rows whose shuttle is shown at its dock, for this run of the editor
    private (string Key, double At, long Revision, List<ShuttleRow> Rows) _editorShuttleRows;
    private (string Key, List<(int X, int Y, int Z, MapModel Model)> Tiles) _editorShuttleTiles;
    private readonly HashSet<(int X, int Y, int Z)> _editorShuttleCells = new(); // the tiles the shown shuttles take
    private readonly HashSet<(int X, int Y, int Z)> _editorPreviewTool = new(); // the tiles of what a tool is about to place, as the world was told
    private AcceptDialog _shuttleDialog;

    /// <summary>
    /// A shuttle that comes to a dock of the map: one of the map json's slots, which docks at its home when it is called, or a dock's own, which
    /// the round's start loads there (its roundstart_template).
    /// <c>Docked</c> is the shuttle stood at its dock, null when there is no dock or no file of it to read; <c>Wrong</c> is why it cannot stand
    /// there, a line for each thing, which the Show box's window says; <c>Note</c> is what docking does to the map, which stops no shuttle.
    /// </summary>
    private sealed record ShuttleRow(string Key, string Label, string Slot, string Chosen, List<string> Ids, string Id, long Stamp, MapDock? Dock, string Status, bool? Fine, List<string> Wrong, string Note, DockedShuttle Docked);

    // The docking ports of the map, found again when the map changed.
    private List<MapDock> EditorDocks()
    {
        MapDocument map = _editor.Document;
        if (_editorDocks.Revision != map.Revision)
            _editorDocks = (map.Revision, MapDocks.Find(map));
        return _editorDocks.Docks;
    }

    // The docks' layer: the border of the rectangle each docking port's shuttle takes, a dock's in blue and a shuttle's own in pink.
    private void UpdateEditorDocks()
    {
        if (_editorDockMarks == null)
            AddChild(_editorDockMarks = new EditorCells(0.06f, 12) { Name = "EditorDocks" });
        List<MapDock> docks = _editorDocksOn ? EditorDocks() : new List<MapDock>();
        // a shuttle shown at its dock (its Show box) has the rectangle it takes there in pink, whether the docks' layer is on or not
        var shown = EditorShuttleRows().Where(row => row.Docked != null && _editorShuttlesOn.Contains(row.Key)).Select(row => (row.Docked.Box, row.Dock.Value.Z)).ToList();
        if (docks.Count == 0 && shown.Count == 0)
        {
            _editorDockMarks.Clear();
            return;
        }
        MapDocument map = _editor.Document;
        _editorDockMarks.Show(Inv($"docks {map.Revision} {docks.Count} ") + string.Join(";", shown), () => docks.SelectMany(dock => MapShapes.Box(dock.X0, dock.Y0, dock.X1, dock.Y1, filled: false)
            .Where(cell => map.Contains(cell.X, cell.Y, dock.Z)).Select(cell => (cell.X, cell.Y, dock.Z, dock.Mobile ? MarkShuttle : MarkDock)))
            .Concat(shown.SelectMany(shuttle => MapShapes.Box(shuttle.Box.X0, shuttle.Box.Y0, shuttle.Box.X1, shuttle.Box.Y1, filled: false)
                .Where(cell => map.Contains(cell.X, cell.Y, shuttle.Z)).Select(cell => (cell.X, cell.Y, shuttle.Z, MarkShuttle)))));
    }

    // A shuttle's map of the maps folder with its size, read again when its file changed; null when it cannot be read.
    private (MapDocument Map, (DockSize Size, int Ports)? Shuttle, long Stamp)? ShuttleFile(string file)
    {
        try
        {
            DateTime stamp = File.GetLastWriteTimeUtc(file);
            if (!_shuttleFiles.TryGetValue(file, out var kept) || kept.Stamp != stamp)
            {
                MapDocument map = MapDocument.Parse(File.ReadAllText(file), file);
                _shuttleFiles[file] = kept = (stamp, map, MapDocks.ShuttleOf(map));
            }
            return (kept.Map, kept.Shuttle, stamp.Ticks);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MapTextException)
        {
            return null;
        }
    }

    // The shuttles that come to the map's docks, each stood at its dock and measured there: the four slots of the map json, then every dock that
    // loads a shuttle of its own at the round's start (the arrivals shuttle). Found again when the map or its json changed, at most five times a second
    // (a stroke changes the map every frame, and a large shuttle is hundreds of tiles to stand at its dock), and twice a second for the files.
    // A shuttle's own map has none.
    private List<ShuttleRow> EditorShuttleRows()
    {
        MapDocument map = _editor.Document;
        string folder = _editor.MapsFolder;
        string key = Inv($"{map.Revision}|{folder}|") + string.Join("|", EditorShuttleSlots.Select(slot => _editor.JsonString("shuttles", slot)));
        double now = App.NowSeconds;
        if (_editorShuttleRows.Rows != null && now - _editorShuttleRows.At < (_editorShuttleRows.Key == key ? 0.5 : 0.2))
            return _editorShuttleRows.Rows;
        var rows = new List<ShuttleRow>();
        List<MapDock> docks = EditorDocks();
        string maps = folder ?? EditorFeed.GameMaps; // (the game's own folder for a map of none)
        // What is wrong is said in red, a shuttle that fits in green (the owner, 2026-11-18); a dock whose shuttle's file is not there to measure
        // says where it is and how large. What a fitting shuttle stands on is a note in amber: tg docks it all the same and destroys what is under
        // it, and tg's own stations have a sign or a beacon under theirs.
        ShuttleRow Row(string rowKey, string label, string slot, string chosen, List<string> ids, string files, string id, MapDock? dock, string noDock, bool named)
        {
            string said = named ? string.Empty : Inv($"tg's own ({id}) ");
            string file = maps == null ? null : Path.Combine(maps, "shuttles", files, id + MapText.Extension);
            var read = dock != null && file != null && File.Exists(file) ? ShuttleFile(file) : null;
            var wrong = new List<string>();
            if (dock == null)
            {
                wrong.Add(noDock);
                return new ShuttleRow(rowKey, label, slot, chosen, ids, id, 0, null, noDock, false, wrong, null, null);
            }
            if (read?.Shuttle is not { } shuttle)
            {
                wrong.Add(Inv($"its file, {Path.Combine("shuttles", files, id + MapText.Extension)}, is not in the maps folder, or has no docking port of its own"));
                return named ? new ShuttleRow(rowKey, label, slot, chosen, ids, id, 0, dock, "its file is not in the maps folder, or has no port", false, wrong, null, null)
                    : new ShuttleRow(rowKey, label, slot, chosen, ids, id, 0, dock, Inv($"dock at {dock.Value.X}, {dock.Value.Y}: {dock.Value.Size.Width} x {dock.Value.Size.Height}"), null, wrong, null, null);
            }
            DockedShuttle docked = MapDocks.Docked(map, dock.Value, read.Value.Map);
            if (MapDocks.Misfit(shuttle.Size, dock.Value.Size) is { } misfit)
                wrong.Add("does not fit its dock: " + misfit);
            if (shuttle.Ports > 1)
                wrong.Add(Inv($"its file has {shuttle.Ports} docking ports of its own: a shuttle has one"));
            if (docked.OffMap > 0)
                wrong.Add(Inv($"{docked.OffMap} of its tiles would stand past the map's edge"));
            string note = docked.Crushed == 0 ? null
                : Inv($"{docked.Crushed} of its tiles stand on walls or things of the map, which docking destroys (one is at {docked.FirstCrushed.Value.X}, {docked.FirstCrushed.Value.Y})");
            return new ShuttleRow(rowKey, label, slot, chosen, ids, id, read.Value.Stamp, dock, said + (wrong.Count == 0 ? "fits its dock" : string.Join("; ", wrong)) + (note == null ? null : "; " + note),
                wrong.Count == 0, wrong, note, docked);
        }
        if (!docks.Any(dock => dock.Mobile))
        {
            foreach (string slot in EditorShuttleSlots)
            {
                string chosen = _editor.JsonString("shuttles", slot);
                string files = folder == null ? null : Path.Combine(folder, "shuttles", slot);
                List<string> ids = files != null && Directory.Exists(files)
                    ? Directory.GetFiles(files, slot + "_*" + MapText.Extension).Select(Path.GetFileNameWithoutExtension).OrderBy(id => id, StringComparer.Ordinal).ToList() : new List<string>();
                MapDock? dock = docks.Where(d => !d.Mobile && d.Id == slot + "_home").Cast<MapDock?>().FirstOrDefault();
                // the shuttle that comes to the dock: the one the map names, else tg's own for the slot
                rows.Add(Row(slot, char.ToUpperInvariant(slot[0]) + slot.Substring(1), slot, chosen, ids, slot, chosen ?? EditorShuttleDefaults[slot], dock,
                    Inv($"no dock: a stationary docking port with the shuttle_id \"{slot}_home\""), chosen != null));
            }
            foreach (MapDock dock in docks)
            {
                if (dock.Mobile || MapDocks.TemplateOf(map[dock.X, dock.Y, dock.Z].Members[dock.Member]) is not { } own)
                    continue;
                string label = char.ToUpperInvariant(own.Port[0]) + own.Port.Substring(1).Replace('_', ' ');
                rows.Add(Row(Inv($"dock {dock.X},{dock.Y},{dock.Z},{dock.Member}"), label, null, null, null, own.Folder, own.Id, dock, null, true));
            }
        }
        _editorShuttleRows = (key, now, map.Revision, rows);
        return rows;
    }

    // The tiles of the shuttles that are shown at their docks, as the world is to make them (the preview, 5.9), with a text that changes when they
    // do; nothing when none is shown. The document has none of it.
    private (string Key, List<(int X, int Y, int Z, MapModel Model)> Tiles) EditorShuttleTiles()
    {
        List<ShuttleRow> rows = _editorShuttlesOn.Count == 0 ? null : EditorShuttleRows().Where(row => row.Docked != null && _editorShuttlesOn.Contains(row.Key)).ToList();
        // (the rows are found again twice a second: the tiles are made again only when the map, a file or the choice changed)
        string key = rows == null || rows.Count == 0 ? null : Inv($"shuttles {_editorShuttleRows.Revision} ") + string.Join(",", rows.Select(row => Inv($"{row.Key}:{row.Id}:{row.Stamp}")));
        if (key == _editorShuttleTiles.Key)
            return _editorShuttleTiles;
        _editorShuttleCells.Clear();
        List<(int X, int Y, int Z, MapModel Model)> tiles = key == null ? null : rows.SelectMany(row => row.Docked.Tiles).ToList();
        foreach ((int x, int y, int z, MapModel _) in tiles ?? new List<(int, int, int, MapModel)>())
            _editorShuttleCells.Add((x, y, z));
        return _editorShuttleTiles = (key, tiles);
    }

    // A tile that shows a shuttle at its dock and nothing a tool is about to place: it is drawn as it is, with none of the preview's blue.
    private bool EditorShuttleOnly(int x, int y, int z) => _editorShuttleCells.Contains((x, y, z)) && !_editorPreviewTool.Contains((x, y, z));

    // A row's Show box (the owner, 2026-11-18): the shuttle is shown on the map where it docks, or no more. What is wrong
    // with it there is said at once in a window, each thing in a line; a shuttle that cannot be stood at a dock at all (no dock, no file of it)
    // is not ticked.
    private void EditorShowShuttle(string key, CheckBox box, bool on)
    {
        if (!on)
        {
            _editorShuttlesOn.Remove(key);
            return;
        }
        _editorShuttleRows = default; // (the files are read now, not in half a second)
        ShuttleRow row = EditorShuttleRows().FirstOrDefault(r => r.Key == key);
        if (row?.Docked != null)
            _editorShuttlesOn.Add(key);
        else
            box.SetPressedNoSignal(false);
        if (row == null || row.Wrong.Count == 0)
            return;
        if (_shuttleDialog == null)
            AddChild(_shuttleDialog = new AcceptDialog { OkButtonText = "Close", Theme = _shell.UiTheme, DialogAutowrap = true });
        _shuttleDialog.Title = Inv($"{row.Label} shuttle: {row.Id}");
        _shuttleDialog.DialogText = (row.Docked != null ? "The shuttle is shown at its dock. This is wrong with it there:" : "The shuttle cannot be shown:")
            + "\n\n" + string.Join("\n", (row.Note == null ? row.Wrong : row.Wrong.Append(row.Note)).Select(line => "- " + line));
        _shuttleDialog.PopupCenteredClamped(new Vector2I(Mathf.RoundToInt(620 * _shell.UiScale), Mathf.RoundToInt(240 * _shell.UiScale)), 0.86f);
    }

    // The Map panel's part for shuttles, made again when what it says changed: for a shuttle's own map its template's data and its one port; for a
    // station's the shuttle of each slot of the map json, with whether it fits the dock the map has for it.
    private void UpdateMapShuttles()
    {
        MapDocument map = _editor.Document;
        List<MapDock> docks = EditorDocks();
        string folder = _editor.MapsFolder;
        List<MapDock> own = docks.Where(dock => dock.Mobile).ToList();
        List<ShuttleRow> rows = EditorShuttleRows();
        string shown = own.Count > 0 ? Inv($"shuttle {own.Count} {map.Width} {map.Height} {_editor.Settings.ShuttleName}|{_editor.Settings.ShuttleDescription}|{_editor.Settings.ShuttleCost}")
            : folder + "|" + string.Join("|", rows.Select(row => row.Key + ":" + row.Chosen + ":" + row.Id + ":" + string.Join(",", row.Ids ?? new List<string>()) + ":" + row.Status));
        if (shown == _mapShuttlesShown)
            return;
        // (a field that is typed in is left alone: its text is taken when it is entered or left, and the rows are made again then)
        if (GetViewport().GuiGetFocusOwner() is LineEdit typing && _mapShuttles.IsAncestorOf(typing))
            return;
        _mapShuttlesShown = shown;
        foreach (Node child in _mapShuttles.GetChildren())
            child.QueueFree();
        float scale = _shell.UiScale;
        var dim = new Color(0.62f, 0.63f, 0.66f);
        void Line(string text, Color? colour = null)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            if (colour is { } c)
                label.AddThemeColorOverride("font_color", c);
            _mapShuttles.AddChild(label);
        }

        if (own.Count > 0)
        {
            Line(own.Count == 1 ? Inv($"This map is a shuttle of {map.Width} x {map.Height} tiles: the pink line is what it takes at a dock.")
                : Inv($"A shuttle has one docking port of its own: this map has {own.Count}. Take the others away."), own.Count == 1 ? null : new Color(1f, 0.55f, 0.4f));
            LineEdit Data(string name, string placeholder, string tip, string text, Action<string> changed)
            {
                var row = new HBoxContainer { TooltipText = tip };
                row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(92 * scale, 0) });
                var field = new LineEdit { PlaceholderText = placeholder, Text = text ?? string.Empty, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = tip };
                field.TextSubmitted += value =>
                {
                    changed(value);
                    field.ReleaseFocus();
                };
                field.FocusExited += () => changed(field.Text);
                row.AddChild(field);
                _mapShuttles.AddChild(row);
                return field;
            }
            Data("Name", "the shuttle's id", "The shuttle's name, as the consoles show it.", _editor.Settings.ShuttleName,
                text => EditorSet(_editor.Settings with { ShuttleName = string.IsNullOrWhiteSpace(text) ? null : text.Trim() }));
            Data("Description", "what the purchase list says of it", "A line or two for the list of shuttles at the communications console.", _editor.Settings.ShuttleDescription,
                text => EditorSet(_editor.Settings with { ShuttleDescription = string.IsNullOrWhiteSpace(text) ? null : text.Trim() }));
            Data("Price", "not for sale", "What the shuttle costs at the communications console, in credits. Empty: it is not for sale.",
                _editor.Settings.ShuttleCost?.ToString(CultureInfo.InvariantCulture),
                text => EditorSet(_editor.Settings with { ShuttleCost = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float cost) && cost >= 0 ? cost : null }));
            Line("Save it as shuttles/PORT/PORT_NAME.as3dmap in a maps folder (shuttles/cargo/cargo_mine.as3dmap): a map names it PORT_NAME in its shuttle slots.", dim);
            return;
        }
        foreach (ShuttleRow shuttle in rows)
        {
            string key = shuttle.Key, slot = shuttle.Slot, chosen = shuttle.Chosen;
            List<string> ids = shuttle.Ids;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = shuttle.Label, CustomMinimumSize = new Vector2(92 * scale, 0) });
            if (slot != null)
            {
                var pick = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false, Disabled = folder == null,
                    TooltipText = folder == null ? "The map is in no maps folder (a folder with map_jsons and shuttles): its shuttles are tg's own. Save it as map_files/NAME/NAME.as3dmap of a maps folder to choose them." : Inv($"The shuttle of the map's {slot} slot (the map json's shuttles): a file of shuttles/{slot} in the maps folder.") };
                pick.AddItem("tg's own");
                foreach (string id in ids)
                    pick.AddItem(id);
                if (chosen != null && !ids.Contains(chosen))
                    pick.AddItem(chosen + " (no file)");
                pick.Selected = chosen == null ? 0 : ids.Contains(chosen) ? ids.IndexOf(chosen) + 1 : pick.ItemCount - 1;
                pick.ItemSelected += index => _editor.SetJsonString("shuttles", slot, index == 0 ? null : index <= ids.Count ? ids[(int)index - 1] : chosen);
                row.AddChild(pick);
            }
            else
            {
                // a dock's own shuttle is the dock's to name (its roundstart_template var)
                row.AddChild(new Label { Text = Inv($"{shuttle.Id} at {shuttle.Dock.Value.X}, {shuttle.Dock.Value.Y}"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true,
                    MouseFilter = Control.MouseFilterEnum.Stop, TooltipText = "The shuttle this dock loads at the round's start: the dock's roundstart_template var names it." });
            }
            var show = new CheckBox { Text = "Show", ButtonPressed = _editorShuttlesOn.Contains(key), FocusMode = Control.FocusModeEnum.None,
                TooltipText = "Shows the shuttle on the map where it docks, with a pink line around what it takes. It is no part of the map: nothing of it is saved, and no tool acts on it." };
            show.Toggled += on => EditorShowShuttle(key, show, on);
            row.AddChild(show);
            _mapShuttles.AddChild(row);
            Line(shuttle.Status, shuttle.Fine == false ? ShuttleWrong : shuttle.Fine == null ? dim : shuttle.Note != null ? ShuttleNote : ShuttleFine);
        }
    }

    // the view's sky and sun, from the settings as they are
    private MapSettings _editorLookOf;
    private MapLook _editorLookMade;

    // The sky and the sun the view shows: the map's settings as they are now, the picture read from its file; null: the game's own.
    private MapLook EditorMapLook()
    {
        MapSettings s = _editor.Settings;
        if (ReferenceEquals(s, _editorLookOf))
            return _editorLookMade;
        _editorLookOf = s;
        MapSettings d = MapSettings.Default;
        if (s.Sky == d.Sky && s.Stars == d.Stars && s.Sun == d.Sun && s.SunColour == d.SunColour && s.SunEnergy == d.SunEnergy && s.SunSize == d.SunSize && s.SunElevation == d.SunElevation)
            return _editorLookMade = null;
        byte[] picture = Array.Empty<byte>();
        try
        {
            if (s.Sky == MapSkyKind.Image && _editor.SkyImagePath is { } file && File.Exists(file) && new FileInfo(file).Length <= Limits.MaxSkyImageBytes)
                picture = File.ReadAllBytes(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EditorNotice("The sky's picture was not read: " + e.Message);
        }
        return _editorLookMade = new MapLook
        {
            Sky = (byte)s.Sky, SkyTop = MapSettings.Rgb(s.SkyTop), SkyHorizon = MapSettings.Rgb(s.SkyHorizon), Stars = s.Stars,
            Image = Codec.IsPicture(picture) ? picture : Array.Empty<byte>(),
            Sun = s.Sun, SunColour = MapSettings.Rgb(s.SunColour), SunEnergy = s.SunEnergy, SunSize = s.SunSize, SunElevation = s.SunElevation,
        };
    }

    private float? _editorSunWas;

    // Where the view's sun stands: where the map fixes it, else where the world behind the view has tg's. A setting that moved it shows at once.
    private float EditorSunAzimuth(float world)
    {
        float? set = _editor.Settings.SunAzimuth;
        if (set != _editorSunWas)
        {
            _editorSunWas = set;
            Starfield.SnapSun();
        }
        return set ?? world;
    }

    private void EditorSet(MapSettings settings) => _editor.SetSettings(settings);

    private static string Hex(Color colour) => "#" + colour.ToHtml(false);

    // ---- the Map panel ----

    private Control BuildMapPanel(float s)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", Mathf.RoundToInt(5 * s));
        scroll.AddChild(column);
        var dim = new Color(0.62f, 0.63f, 0.66f);

        void Heading(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeColorOverride("font_color", dim);
            column.AddChild(new HSeparator());
            column.AddChild(label);
        }

        Control Row(string name, Control control, string tip, Control into = null)
        {
            var row = new HBoxContainer { TooltipText = tip };
            row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(92 * s, 0), TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Stop });
            control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            control.TooltipText = tip;
            row.AddChild(control);
            (into ?? column).AddChild(row);
            return row;
        }

        LineEdit Field(string placeholder, Action<string> changed)
        {
            var field = new LineEdit { PlaceholderText = placeholder };
            // (a field's text is taken when it is entered or left, not at every letter)
            field.TextSubmitted += text =>
            {
                changed(text);
                field.ReleaseFocus();
            };
            field.FocusExited += () => changed(field.Text);
            return field;
        }

        HSlider Slider(float min, float max, float step, Action<float> changed)
        {
            // (not scrollable: the wheel wheels the panel, and a slider wheeled past took a step of its setting unseen)
            var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, Scrollable = false, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(80 * s, 0) };
            slider.ValueChanged += value => changed((float)value);
            return slider;
        }

        ColorPickerButton Picker(Action<Color> changed)
        {
            var picker = new ColorPickerButton { EditAlpha = false, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(60 * s, 24 * s) };
            picker.ColorChanged += colour => changed(colour);
            return picker;
        }

        _mapName = Field("the map's name", text => EditorSetJsonText("map_name", text));
        Row("Map", _mapName, "The map's name (the map json's map_name): what the game calls the map.");
        _mapStation = Field("the game picks one", text => EditorSet(_editor.Settings with { StationName = string.IsNullOrWhiteSpace(text) ? null : text.Trim() }));
        Row("Station", _mapStation, "The station's name, as the announcements and the consoles say it. Empty: the game picks one, as tg does.");
        _mapMine = new OptionButton { FocusMode = Control.FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
        foreach ((string mine, _) in EditorMines)
            _mapMine.AddItem(mine);
        _mapMine.ItemSelected += index => _editor.SetJsonString("minetype", EditorMines[index].Mine);
        Row("Mine", _mapMine, "The map's mine (the map json's minetype). tg's own: a round loads Lavaland as its mining level, as for a map that says nothing. None: no mining level is loaded, and a small map's round starts much sooner. Ice: the map's own levels are the mine (an ice moon), and none is loaded.");

        Heading("Sky");
        _mapSky = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (string sky in EditorSkies)
            _mapSky.AddItem(sky);
        _mapSky.ItemSelected += index => EditorSet(_editor.Settings with { Sky = (MapSkyKind)index });
        Row("Sky", _mapSky, "What is behind the map: space with the planet under the station, the stars alone, a planet's sky of two colours, or your own picture.");
        _mapSkyColours = new VBoxContainer();
        _mapSkyTop = Picker(colour => EditorSet(_editor.Settings with { SkyTop = Hex(colour) }));
        _mapSkyHorizon = Picker(colour => EditorSet(_editor.Settings with { SkyHorizon = Hex(colour) }));
        Row("Overhead", _mapSkyTop, "The sky's colour straight up.", _mapSkyColours);
        Row("Horizon", _mapSkyHorizon, "The sky's colour at the horizon, and of the ground far below.", _mapSkyColours);
        column.AddChild(_mapSkyColours);
        var picture = new HBoxContainer();
        _mapSkyFile = new Label { ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var choose = new Button { Text = "Choose...", FocusMode = Control.FocusModeEnum.None };
        choose.Pressed += EditorChooseSky;
        picture.AddChild(_mapSkyFile);
        picture.AddChild(choose);
        _mapSkyPicture = Row("Picture", picture, "A panorama (a picture twice as wide as high, laid around the map: its middle row is the horizon), a png or a jpg of at most 4 MB. It is copied beside the map's file and travels with the map.");
        _mapStars = Slider(0f, 4f, 0.05f, value => EditorSet(_editor.Settings with { Stars = value }));
        Row("Stars", _mapStars, "How bright the stars are (space and the stars alone): 1 is the game's own, 0 none.");

        Heading("Sun");
        _mapSun = Check("The map has a sun", "Its light on what is open to the sky, its disc and its flare.", on => EditorSet(_editor.Settings with { Sun = on }));
        column.AddChild(_mapSun);
        _mapSunRows = new VBoxContainer();
        _mapSunColour = Picker(colour => EditorSet(_editor.Settings with { SunColour = Hex(colour) }));
        Row("Colour", _mapSunColour, "The colour of the sun's light and of its disc.", _mapSunRows);
        _mapSunEnergy = Slider(0f, 8f, 0.05f, value => EditorSet(_editor.Settings with { SunEnergy = value }));
        Row("Strength", _mapSunEnergy, "How strong the sun's light is: 0.7 is the game's own.", _mapSunRows);
        _mapSunSize = Slider(0.2f, 30f, 0.1f, value => EditorSet(_editor.Settings with { SunSize = value }));
        Row("Size", _mapSunSize, "The radius of the sun's disc in degrees: 2 is the game's own.", _mapSunRows);
        _mapSunHeight = Slider(1f, 90f, 1f, value => EditorSet(_editor.Settings with { SunElevation = value }));
        Row("Height", _mapSunHeight, "How high the sun stands over the map, in degrees: 35 is the game's own, 90 straight overhead.", _mapSunRows);
        _mapSunFixed = Check("Stands still", "The sun stands where the slider puts it (degrees clockwise from north) for the whole round. Off: it goes round the map as tg's does, and the solar panels follow it.",
            on => EditorSet(_editor.Settings with { SunAzimuth = on ? (float)_mapSunAzimuth.Value : null }));
        _mapSunRows.AddChild(_mapSunFixed);
        _mapSunAzimuth = Slider(0f, 359f, 1f, value =>
        {
            if (_editor.Settings.SunAzimuth != null)
                EditorSet(_editor.Settings with { SunAzimuth = value });
        });
        Row("Bearing", _mapSunAzimuth, "Where the sun stands, in degrees clockwise from north.", _mapSunRows);
        column.AddChild(_mapSunRows);

        Heading("Air");
        (OptionButton, LineEdit) Air(string name, string tip, Func<MapSettings, string, MapSettings> with)
        {
            var pick = new OptionButton { FocusMode = Control.FocusModeEnum.None, FitToLongestItem = false, ClipText = true, CustomMinimumSize = new Vector2(70 * s, 0) };
            foreach ((string preset, _) in EditorAirs)
                pick.AddItem(preset);
            pick.AddItem("Other");
            LineEdit field = null;
            void Take(string text)
            {
                string mix = string.IsNullOrWhiteSpace(text) ? null : MapSettings.GasMix(text);
                if (mix == null && !string.IsNullOrWhiteSpace(text))
                {
                    EditorNotice("Not a gas mix: write it as tg does, o2=22;n2=82;TEMP=293.15");
                    _mapShown = null; // the field shows what is set again
                    return;
                }
                EditorSet(with(_editor.Settings, mix));
            }
            field = Field("o2=22;n2=82;TEMP=293.15", Take);
            pick.ItemSelected += index =>
            {
                if (index < EditorAirs.Length)
                    EditorSet(with(_editor.Settings, EditorAirs[index].Mix));
                else
                    field.GrabFocus();
            };
            var both = new VBoxContainer();
            both.AddChild(pick);
            both.AddChild(field);
            Row(name, both, tip);
            return (pick, field);
        }
        (_mapAirInsidePick, _mapAirInside) = Air("Inside", "The air a floor of the station's levels starts with, in the place of tg's default (21 kPa of oxygen, 79 of nitrogen, 20 C): moles of each gas on a tile and the temperature in kelvin, as tg writes a gas mix. A floor whose type has an air of its own (a cold room, a vacuum floor) keeps it.",
            (settings, mix) => settings with { AirInside = mix });
        (_mapAirOutsidePick, _mapAirOutside) = Air("Outside", "The air of the station's levels' turfs that are open to a planet's sky (tg's planetary turfs: sand, snow, rock, grass): a planet's own air and temperature. Space is always a vacuum.",
            (settings, mix) => settings with { AirOutside = mix });

        Heading("Levels");
        _mapLevels = new VBoxContainer();
        column.AddChild(_mapLevels);

        Heading("Shuttles");
        _mapShuttles = new VBoxContainer();
        column.AddChild(_mapShuttles);
        _mapShuttlesShown = null;
        return scroll;
    }

    // A string of the map json's own keys (tg's map_name): set, or taken away when empty.
    private void EditorSetJsonText(string key, string text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text == _editor.JsonString(key))
            return;
        _editor.SetJsonString(key, text);
    }

    // The picture of the sky is chosen: it is copied beside the map's file, where the map json names it and a server finds it.
    private void EditorChooseSky()
        => EditorFileDialog(FileDialog.FileModeEnum.OpenFile, "The sky's picture (a panorama)", new[] { "*.png, *.jpg, *.jpeg ; Pictures" }, path =>
        {
            try
            {
                if (new FileInfo(path).Length > Limits.MaxSkyImageBytes)
                {
                    EditorNotice(Inv($"The picture is larger than {Limits.MaxSkyImageBytes / (1024 * 1024)} MB: make it smaller (4096 by 2048 as a jpg is enough)."));
                    return;
                }
                string name = new(Path.GetFileName(path).Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
                string beside = Path.Combine(Path.GetDirectoryName(_editor.FilePath) ?? string.Empty, name);
                if (!string.Equals(Path.GetFullPath(beside), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(beside));
                    File.Copy(path, beside, overwrite: true);
                }
                _editorLookOf = null; // (the same name may be another picture)
                EditorSet(_editor.Settings with { Sky = MapSkyKind.Image, SkyImage = name });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                EditorNotice("The picture was not taken: " + e.Message);
            }
        });

    // Every frame: the controls show the settings when those changed otherwise than by the controls themselves (a map opened, a console line).
    private void UpdateMapPanel()
    {
        MapSettings s = _editor.Settings;
        MapDocument map = _editor.Document;
        if (!ReferenceEquals(s, _mapShown))
        {
            _mapShown = s;
            void Text(LineEdit field, string text)
            {
                if (!field.HasFocus() && field.Text != (text ?? string.Empty))
                    field.Text = text ?? string.Empty;
            }
            Text(_mapName, _editor.JsonString("map_name"));
            Text(_mapStation, s.StationName);
            _mapSky.Selected = (int)s.Sky;
            _mapSkyColours.Visible = s.Sky == MapSkyKind.Colours;
            _mapSkyPicture.Visible = s.Sky == MapSkyKind.Image;
            _mapSkyTop.Color = Color.FromHtml(s.SkyTop);
            _mapSkyHorizon.Color = Color.FromHtml(s.SkyHorizon);
            _mapSkyFile.Text = s.SkyImage == null ? "none chosen" : File.Exists(_editor.SkyImagePath) ? s.SkyImage : s.SkyImage + " (missing)";
            _mapStars.SetValueNoSignal(s.Stars);
            _mapStars.Editable = s.Sky is MapSkyKind.Space or MapSkyKind.Stars;
            _mapSun.SetPressedNoSignal(s.Sun);
            _mapSunRows.Visible = s.Sun;
            _mapSunColour.Color = Color.FromHtml(s.SunColour);
            _mapSunEnergy.SetValueNoSignal(s.SunEnergy);
            _mapSunSize.SetValueNoSignal(s.SunSize);
            _mapSunHeight.SetValueNoSignal(s.SunElevation);
            _mapSunFixed.SetPressedNoSignal(s.SunAzimuth != null);
            _mapSunAzimuth.Editable = s.SunAzimuth != null;
            if (s.SunAzimuth is { } azimuth)
                _mapSunAzimuth.SetValueNoSignal(azimuth);
            foreach ((OptionButton pick, LineEdit field, string mix) in new[] { (_mapAirInsidePick, _mapAirInside, s.AirInside), (_mapAirOutsidePick, _mapAirOutside, s.AirOutside) })
            {
                int preset = Array.FindIndex(EditorAirs, air => air.Mix == mix);
                pick.Selected = preset >= 0 ? preset : EditorAirs.Length;
                Text(field, mix);
            }
        }

        // (the json's own keys are no part of the settings: an undo gives them back with the settings as they are)
        int mine = Array.FindIndex(EditorMines, m => m.Mine == _editor.JsonString("minetype"));
        if (_mapMine.Selected != mine)
            _mapMine.Selected = mine; // (none chosen: a mine written by hand that the game does not read)

        UpdateMapShuttles();

        // the levels' rows: a weather, the gravity and the base turf of each (tg's level traits)
        string traits = string.Join("|", Enumerable.Range(1, Math.Min(map.Levels, MapPanelLevels)).Select(z => string.Join(",", EditorWeathers.Skip(1).Select(w => _editor.LevelTrait(z, w.Trait))) + ";" + _editor.LevelTrait(z, "Gravity") + ";" + _editor.LevelTrait(z, "Baseturf")));
        if (_mapLevelsShown == (map.Levels, traits))
            return;
        // (a field that is typed in is left alone: its text is taken when it is entered or left, and the rows are made again then)
        if (GetViewport().GuiGetFocusOwner() is LineEdit typing && _mapLevels.IsAncestorOf(typing))
            return;
        _mapLevelsShown = (map.Levels, traits);
        foreach (Node child in _mapLevels.GetChildren())
            child.QueueFree();
        float scale = _shell.UiScale;
        for (int z = 1; z <= Math.Min(map.Levels, MapPanelLevels); z++)
        {
            int level = z;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = Inv($"Level {z}"), CustomMinimumSize = new Vector2(60 * scale, 0) });
            var weather = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false,
                TooltipText = "The weather that comes over this level from time to time in a round (tg's level trait): storms of ash, snow, rain or sand, or the void's." };
            foreach ((string name, _) in EditorWeathers)
                weather.AddItem(name);
            weather.Selected = Math.Max(0, Array.FindIndex(EditorWeathers, w => w.Trait != null && _editor.LevelTrait(level, w.Trait) == "true"));
            weather.ItemSelected += index =>
            {
                for (int i = 1; i < EditorWeathers.Length; i++)
                    _editor.SetLevelTrait(level, EditorWeathers[i].Trait, i == index ? JsonValue.Create(true) : null);
            };
            var gravity = new CheckBox { Text = "Gravity", FocusMode = Control.FocusModeEnum.None, ButtonPressed = _editor.LevelTrait(level, "Gravity") is { } g && g != "0" && g != "false",
                TooltipText = "The level has gravity of its own, as a planet has (tg's Gravity trait). Off: a station's, made by its gravity generator." };
            gravity.Toggled += on => _editor.SetLevelTrait(level, "Gravity", on ? JsonValue.Create(1) : null);
            row.AddChild(weather);
            row.AddChild(gravity);
            _mapLevels.AddChild(row);

            const string tip = "What a turf of this level is when it is destroyed or scraped away (tg's level trait Baseturf). tg's own is space, which nothing falls or looks through: a level over another wants open space, and a level over the floors of a planet open air. Other: any turf's path.";
            string set = _editor.LevelTrait(level, "Baseturf")?.Trim('"');
            int preset = Array.FindIndex(EditorBaseTurfs, b => b.Turf == set);
            var baseRow = new HBoxContainer { TooltipText = tip };
            baseRow.AddChild(new Label { Text = "Base turf", CustomMinimumSize = new Vector2(60 * scale, 0), TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Stop });
            var basePick = new OptionButton { FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, FitToLongestItem = false, TooltipText = tip };
            foreach ((string name, _) in EditorBaseTurfs)
                basePick.AddItem(name);
            basePick.AddItem("Other");
            basePick.Selected = preset >= 0 ? preset : EditorBaseTurfs.Length;
            var baseField = new LineEdit { PlaceholderText = "/turf/open/...", Text = preset >= 0 ? string.Empty : set, Visible = preset < 0, TooltipText = tip };
            void Take(string text)
            {
                EditorSetBaseTurf(level, text);
                _mapLevelsShown = default; // the rows show what is set again
            }
            baseField.TextSubmitted += _ => baseField.ReleaseFocus(); // (its text is taken as the field is left)
            baseField.FocusExited += () => Take(baseField.Text);
            basePick.ItemSelected += index =>
            {
                if (index < EditorBaseTurfs.Length)
                    EditorSetBaseTurf(level, EditorBaseTurfs[index].Turf);
                else
                {
                    baseField.Visible = true;
                    baseField.GrabFocus();
                }
            };
            baseRow.AddChild(basePick);
            _mapLevels.AddChild(baseRow);
            _mapLevels.AddChild(baseField);
        }
    }

    // A level's base turf (tg's level trait Baseturf): a turf's path, or nothing for tg's own. A text that is no turf's path is told and not set.
    private void EditorSetBaseTurf(int level, string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (path != null && !typeof(turf).IsAssignableFrom(Anomaly.Core.DmShim.DmTypes.FromPath(path)))
        {
            EditorNotice("Not a turf: " + path);
            return;
        }
        _editor.SetLevelTrait(level, "Baseturf", path == null ? null : JsonValue.Create(path));
    }

    // The console's "editor setting KEY [VALUE]" (a scripted check): a setting of the map by its name.
    private void EditorSetting(string key, string value)
    {
        MapSettings s = _editor.Settings;
        float Number() => float.Parse(value, CultureInfo.InvariantCulture);
        string text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (key)
        {
            case "station_name": EditorSet(s with { StationName = text }); break;
            case "map_name": EditorSetJsonText("map_name", value); break;
            case "sky": EditorSet(s with { Sky = Enum.Parse<MapSkyKind>(value, ignoreCase: true) }); break;
            case "sky_top": EditorSet(s with { SkyTop = MapSettings.Colour(value) ?? s.SkyTop }); break;
            case "sky_horizon": EditorSet(s with { SkyHorizon = MapSettings.Colour(value) ?? s.SkyHorizon }); break;
            case "sky_image":
                _editorLookOf = null;
                EditorSet(s with { Sky = MapSkyKind.Image, SkyImage = text });
                break;
            case "stars": EditorSet(s with { Stars = Math.Clamp(Number(), 0f, 4f) }); break;
            case "sun": EditorSet(s with { Sun = value == "on" }); break;
            case "sun_color": EditorSet(s with { SunColour = MapSettings.Colour(value) ?? s.SunColour }); break;
            case "sun_energy": EditorSet(s with { SunEnergy = Math.Clamp(Number(), 0f, 8f) }); break;
            case "sun_size": EditorSet(s with { SunSize = Math.Clamp(Number(), 0.2f, 30f) }); break;
            case "sun_elevation": EditorSet(s with { SunElevation = Math.Clamp(Number(), 1f, 90f) }); break;
            case "sun_azimuth": EditorSet(s with { SunAzimuth = text == null ? null : Math.Clamp(Number(), 0f, 360f) }); break;
            case "air_inside": EditorSet(s with { AirInside = MapSettings.GasMix(text) }); break;
            case "air_outside": EditorSet(s with { AirOutside = MapSettings.GasMix(text) }); break;
            case "weather":
            {
                // editor setting weather LEVEL NAME (ash, snow, rain, sand, void, none)
                string[] p = (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int level = int.Parse(p[0], CultureInfo.InvariantCulture);
                for (int i = 1; i < EditorWeathers.Length; i++)
                    _editor.SetLevelTrait(level, EditorWeathers[i].Trait, EditorWeathers[i].Trait.EndsWith(p[1] + "storm", StringComparison.OrdinalIgnoreCase) ? JsonValue.Create(true) : null);
                break;
            }
            case "baseturf":
            {
                // editor setting baseturf LEVEL [TURF] (nothing: tg's own)
                string[] p = (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                EditorSetBaseTurf(int.Parse(p[0], CultureInfo.InvariantCulture), p.Length > 1 ? p[1] : null);
                break;
            }
            case "minetype":
                // editor setting minetype [none|lavaland|ice] (nothing: tg's own)
                if (Array.FindIndex(EditorMines, m => m.Mine == text) < 0)
                    EditorNotice("No such mine: " + text);
                else
                    _editor.SetJsonString("minetype", text);
                break;
            default: EditorNotice("No such setting: " + key); break;
        }
    }
}
