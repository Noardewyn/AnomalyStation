using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.Tg;

namespace Anomaly.Client;

// The map editor's gas (docs/ai/specs/map-editor.md 5.23): the Tile panel's Gas row says what the selected tiles start the round with, and its window
// edits it for all of them at once: tg's gases (the dev menu's gas list), their moles, the temperature and the pressure that makes. It is the
// initial_gas_mix var of each tile's turf, as tg's maps set it.
public partial class GameScene
{
    private const string GasTip = "The gas the selected tiles start the round with: the initial_gas_mix var of their turfs (moles of each gas on a tile, and the temperature), else the floor type's own. Walls hold none. The gas spreads from the first second where nothing seals the tiles.";

    private Label _tileGas;
    private Button _tileGasEdit;
    private Control _tileGasRow;
    private ConfirmationDialog _gasDialog;
    private VBoxContainer _gasRows;
    private OptionButton _gasAdd;
    private SpinBox _gasKelvinSpin;
    private Label _gasCelsius, _gasPressure;
    private LineEdit _gasText;
    private GasMix _gasEdit = GasMix.Parse(GasMix.NormalAir);
    private bool _gasSyncing;
    private float _gasScale = 1f;

    // The Tile panel's row: what the selection starts with, and the button of the window.
    private Control BuildTileGasRow(float s)
    {
        _gasScale = s;
        var row = new HBoxContainer { Visible = false };
        row.AddChild(new Label { Text = "Gas", TooltipText = GasTip, MouseFilter = Control.MouseFilterEnum.Stop });
        _tileGas = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, TooltipText = GasTip, MouseFilter = Control.MouseFilterEnum.Stop };
        _tileGasEdit = new Button { Text = "Edit...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Set the gas of every selected tile" };
        _tileGasEdit.Pressed += OpenGasDialog;
        row.AddChild(_tileGas);
        row.AddChild(_tileGasEdit);
        BuildGasDialog(s);
        return row;
    }

    private void BuildGasDialog(float s)
    {
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(420 * s, 0) };
        var presets = new HBoxContainer();
        presets.AddChild(new Label { Text = "Start from" });
        foreach ((string name, string mix, string tip) in new[]
        {
            ("Normal air", GasMix.NormalAir, "A room's air: 22 mol of oxygen and 82 of nitrogen at 20 C, 101 kPa"),
            ("Vacuum", GasMix.Vacuum, "No gas"),
            ("Gas tank", null, "The gases listed, 100 000 mol in all, as tg's gas chambers hold (reinforced walls: about 970 bar)"),
        })
        {
            var button = new Button { Text = name, TooltipText = tip, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => SetGasEdit(mix != null ? GasMix.Parse(mix) : Tank(_gasEdit));
            presets.AddChild(button);
        }
        body.AddChild(presets);
        body.AddChild(new HSeparator());

        _gasRows = new VBoxContainer();
        body.AddChild(_gasRows);
        _gasAdd = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Add one of tg's gases to the mix", FitToLongestItem = false };
        _gasAdd.ItemSelected += index =>
        {
            if (index > 0 && _gasAdd.GetItemMetadata((int)index).AsString() is { Length: > 0 } id)
            {
                _gasEdit[id] = _gasEdit[id] > 0 ? _gasEdit[id] : 10;
                SetGasEdit(_gasEdit);
            }
        };
        body.AddChild(_gasAdd);

        var heat = new HBoxContainer();
        heat.AddChild(new Label { Text = "Temperature" });
        _gasKelvinSpin = new SpinBox { MinValue = 0, MaxValue = 100000, Step = 0.01, Suffix = "K", SelectAllOnFocus = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _gasKelvinSpin.ValueChanged += value =>
        {
            if (_gasSyncing)
                return;
            _gasEdit.Kelvin = value;
            ShowGasEdit(rows: false);
        };
        heat.AddChild(_gasKelvinSpin);
        _gasCelsius = new Label();
        heat.AddChild(_gasCelsius);
        body.AddChild(heat);
        _gasPressure = new Label();
        body.AddChild(_gasPressure);

        var text = new HBoxContainer();
        text.AddChild(new Label { Text = "As text", TooltipText = "tg's gas string, as the map keeps it: moles of each gas by its id, then TEMP in kelvin" });
        _gasText = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SelectAllOnFocus = true };
        _gasText.TextChanged += value =>
        {
            if (_gasSyncing || GasMix.Parse(value) is not { } typed)
                return;
            _gasEdit = typed;
            ShowGasEdit(rows: true, text: false);
        };
        text.AddChild(_gasText);
        body.AddChild(text);

        _gasDialog = new ConfirmationDialog { Title = "Gas of the selected tiles", OkButtonText = "Apply", Theme = _shell.UiTheme };
        _gasDialog.AddButton("The floor's own", false, "own");
        _gasDialog.AddChild(body);
        _gasDialog.Confirmed += () =>
        {
            _gasKelvinSpin.Apply(); // (a number typed and not yet taken)
            EditorSetGas(GasMix.Parse(_gasText.Text) is { } typed ? typed.ToString() : _gasEdit.ToString());
        };
        _gasDialog.CustomAction += action =>
        {
            _gasDialog.Hide();
            if (action.ToString() == "own")
                EditorSetGas(null);
        };
        _shell.AddChild(_gasDialog);
    }

    // The listed gases, 100 000 mol in all in the same shares (oxygen alone when none is listed), as tg's ATMOS_TANK_* strings.
    private static GasMix Tank(GasMix from)
    {
        var tank = new GasMix { Kelvin = 293.15 };
        double total = from.Gases.Sum(g => g.Moles);
        if (total <= 0)
            tank["o2"] = 100000;
        foreach ((string id, double moles) in from.Gases.Where(g => g.Moles > 0))
            tank[id] = Math.Round(100000 * moles / total, 3);
        return tank;
    }

    private void OpenGasDialog()
    {
        // the window starts from what the selection starts with (its first open tile's), or a room's air
        MapDocument map = _editor.Document;
        string first = EditorSelectionCells().Where(c => map.Contains(c.X, c.Y, c.Z)).Select(c => MapEdits.GasMixOf(map[c.X, c.Y, c.Z], out _)).FirstOrDefault(m => m != null);
        if (first == null)
        {
            EditorNotice("Nothing selected holds air: select floors (a wall holds none).", 4);
            return;
        }
        SetGasEdit(GasMix.Parse(first) ?? GasMix.Parse(GasMix.NormalAir));
        _gasDialog.PopupCentered();
    }

    private void SetGasEdit(GasMix mix)
    {
        _gasEdit = mix;
        ShowGasEdit(rows: true);
    }

    // The window shows the mix being edited: a row for each gas (its colour, its name, its moles and a button that takes it away), the gases that
    // can be added, the temperature, the pressure and the text.
    private void ShowGasEdit(bool rows, bool text = true)
    {
        float s = _gasScale;
        _gasSyncing = true;
        if (rows)
        {
            foreach (Node child in _gasRows.GetChildren())
                child.QueueFree();
            foreach ((string id, double moles) in _gasEdit.Gases.ToList())
            {
                GasEntry gas = GasCatalog.Find(id);
                var row = new HBoxContainer();
                uint rgb = gas?.Rgb ?? 0xFFFFFF;
                row.AddChild(new ColorRect { Color = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f), CustomMinimumSize = new Vector2(14 * s, 14 * s), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                row.AddChild(new Label { Text = gas?.Name ?? id, TooltipText = id + (gas == null ? "  (no such gas of tg's)" : "  " + gas.Path), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Stop });
                var spin = new SpinBox { MinValue = 0, MaxValue = 10000000, Step = 0.01, Suffix = "mol", SelectAllOnFocus = true, Value = moles, CustomMinimumSize = new Vector2(150 * s, 0) };
                spin.ValueChanged += value =>
                {
                    if (_gasSyncing)
                        return;
                    _gasEdit[id] = value;
                    ShowGasEdit(rows: false);
                };
                row.AddChild(spin);
                var remove = new Button { Text = "x", TooltipText = "Take this gas out", FocusMode = Control.FocusModeEnum.None };
                remove.Pressed += () =>
                {
                    _gasEdit.Gases.RemoveAll(g => g.Id == id);
                    SetGasEdit(_gasEdit);
                };
                row.AddChild(remove);
                _gasRows.AddChild(row);
            }
            _gasAdd.Clear();
            _gasAdd.AddItem("Add a gas...");
            _gasAdd.SetItemMetadata(0, string.Empty);
            foreach (GasEntry gas in GasCatalog.All.Where(g => _gasEdit.Gases.All(e => e.Id != g.Id)))
            {
                _gasAdd.AddItem(gas.Name);
                _gasAdd.SetItemMetadata(_gasAdd.ItemCount - 1, gas.Id);
            }
            _gasAdd.Selected = 0;
            _gasKelvinSpin.Value = _gasEdit.Kelvin;
        }
        _gasCelsius.Text = Inv($"{_gasEdit.Kelvin - 273.15:0.#} C");
        double kpa = _gasEdit.Kpa;
        _gasPressure.Text = Inv($"Pressure on a tile: {kpa:0.#} kPa") + (kpa > 5000 ? " (reinforced walls hold it; a door or a window does not)" : kpa < 0.5 ? " (vacuum)" : string.Empty);
        if (text)
            _gasText.Text = _gasEdit.ToString();
        _gasSyncing = false;
    }

    // The tiles a selection-wide setting applies to: every tile of the selected rectangles, else the selected tile.
    private IEnumerable<(int X, int Y, int Z)> EditorSelectionCells()
        => _editorRegion is { } r ? EditorRegionCells().Select(c => (c.X, c.Y, r.Z))
            : _editorSelected is { } s ? new[] { (s.X, s.Y, s.Z) } : Array.Empty<(int, int, int)>();

    // The gas of every selected tile that holds air (null: its floor type's own again), as one step.
    private void EditorSetGas(string mix)
    {
        MapDocument map = _editor.Document;
        try
        {
            var tiles = new List<(int X, int Y, int Z, MapModel Model)>();
            foreach ((int x, int y, int z) in EditorSelectionCells().Where(c => map.Contains(c.X, c.Y, c.Z)))
            {
                MapModel tile = map[x, y, z], set = MapEdits.WithGasMix(map, tile, mix);
                if (!ReferenceEquals(set, tile))
                    tiles.Add((x, y, z, set));
            }
            if (tiles.Count > 0 && !map.InStep)
                map.Edit(Inv($"Gas ({tiles.Count} tiles)"), tiles);
            EditorNotice(tiles.Count == 0 ? "No tile changed: the selection already starts with this gas, or holds no air." : Inv($"Gas of {tiles.Count} tiles: {mix ?? "their floors' own"}"), 3);
        }
        catch (MapTextException e)
        {
            EditorNotice("Not changed: " + e.Message);
        }
    }

    // The Tile panel's row: the selection's gas, the same on all its tiles that hold air, or "mixed".
    private void UpdateTileGas()
    {
        MapDocument map = _editor.Document;
        bool any = _editorRegion != null || _editorSelected != null;
        _tileGasRow.Visible = any;
        if (!any)
            return;
        string shown = null;
        bool mixed = false, set = false;
        foreach ((int x, int y, int z) in EditorSelectionCells())
        {
            if (!map.Contains(x, y, z) || MapEdits.GasMixOf(map[x, y, z], out bool here) is not { } mix)
                continue;
            if (shown == null)
                (shown, set) = (mix, here);
            else if (mix != shown || here != set)
            {
                mixed = true;
                break;
            }
        }
        string text = shown == null ? "none (a wall)" : mixed ? "mixed" : (set ? string.Empty : "the floor's own: ") + GasSummary(shown);
        if (_tileGas.Text != text)
            (_tileGas.Text, _tileGas.TooltipText) = (text, GasTip + (shown == null || mixed ? string.Empty : "\n\n" + shown));
        _tileGasEdit.Disabled = shown == null;
    }

    // A gas string in a few words: its gases, its temperature and its pressure.
    private static string GasSummary(string mix)
    {
        if (GasMix.Parse(mix) is not { } gas)
            return mix; // (one of tg's named atmospheres)
        string gases = gas.Gases.Count == 0 ? "vacuum" : string.Join(", ", gas.Gases.Select(g => Inv($"{g.Id} {g.Moles:0.##}")));
        return Inv($"{gases}; {gas.Kelvin - 273.15:0.#} C, {gas.Kpa:0.#} kPa");
    }
}
