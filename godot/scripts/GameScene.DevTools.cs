using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using ImGuiNET;
using Anomaly.Client.Dev;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Anomaly.Client;

// The dev menu's tools (the owner, 2026-10-13; docs/ai/specs/apps.md 2.18): the spawn panel (tg's own: every /obj, /turf and
// /mob type by tab, the dev menu's categories, a search, a dropdown of every type, their icons; an amount and a dir), the
// delete tool, and the gas tools (add, remove, heat, fill, ignite; tg's gases with their sprites). A tool armed, a left click
// on the map under the mouse (not over a window) applies it there and a right click puts it away. Each action is a dev
// command line the server runs with tg's procs (spawn: spawn_atom; delete: admin_delete; air: the gas_mixture procs).
public partial class GameScene
{
    private enum DevTool
    {
        None,
        Spawn,
        Delete,
        Gas,
        Rotate,
        Mount,
        Possess, // D346: a click on a mob takes it over (the Player tab's Possess section arms it)
    }

    private enum GasOp
    {
        Add,
        Remove,
        RemoveAll,
        Temperature,
        Fill,
        Ignite,
    }

    private static readonly string[] GasOpNames = { "Add", "Remove", "Remove all", "Temperature", "Fill", "Ignite" };
    private static readonly string[] FillNames = { "Normal air", "Vacuum", "The gas, pure, at one atmosphere", "A gas string" };
    private static readonly string[] DirNames = { "The type's own", "North", "South", "East", "West" };
    private static readonly byte[] DirValues = { 0, ByondDir.North, ByondDir.South, ByondDir.East, ByondDir.West };
    private static readonly string[] TabNames = { "Objects", "Turfs", "Mobs", "Areas" }; // the fourth is the map editor's (SpawnTab.Areas)

    // the dev menu's map tools at most 8 a second (the server's dev command bucket)
    private const double ToolClickSeconds = 0.125;

    private DevTool _devTool;
    private IconThumbs _thumbs;
    private Task<SpawnCatalog> _catalogTask;
    private SpawnCatalog _catalog;
    private bool _catalogFailed;

    private SpawnTab _spawnTab;
    private int _spawnCategory = -1;
    private string _spawnQuery = string.Empty;
    private bool _spawnMapping;
    private bool _spawnAbstract;
    private readonly List<SpawnEntry> _spawnList = new();
    private string _spawnListKey;

    // What the list of types shows (the dev menu's spawn panel, the map editor's palette): the tree of the filtered list by the types' paths,
    // with the branches the user opened in its tab; or the list flat, a row a type, for a search's matches and for the favourites.
    private readonly List<SpawnRow> _spawnRows = new();
    private SpawnTree _spawnTree;
    // (the turfs' list holds the full-tile windows of /obj too, so /turf is a branch there: it starts open)
    private readonly HashSet<string>[] _spawnOpen = { new(StringComparer.Ordinal), new(StringComparer.Ordinal) { "/turf" }, new(StringComparer.Ordinal), new(StringComparer.Ordinal) };

    // The rows of the list that was just filtered.
    private void SpawnRowsOfList()
    {
        _spawnTree = string.IsNullOrWhiteSpace(_spawnQuery) && !_spawnFavouritesOnly ? new SpawnTree(_spawnList) : null;
        if (_spawnTree == null)
            SpawnTree.Flat(_spawnList, _spawnRows);
        else
            _spawnTree.Rows(_spawnOpen[(int)_spawnTab], _spawnRows);
    }

    // A branch of the tree folds or unfolds.
    private void SpawnToggleBranch(string path)
    {
        HashSet<string> open = _spawnOpen[(int)_spawnTab];
        if (!open.Remove(path))
            open.Add(path);
        _spawnTree?.Rows(open, _spawnRows);
    }

    // The branches over a type open, so that its row is in the list (a type picked off the map); false when its row was there already.
    private bool SpawnUnfoldTo(SpawnEntry entry)
    {
        if (_spawnTree == null || entry == null)
            return false;
        HashSet<string> open = _spawnOpen[(int)_spawnTab];
        bool opened = false;
        foreach (string path in _spawnTree.Over(entry.Path))
            opened |= open.Add(path);
        if (opened)
            _spawnTree.Rows(open, _spawnRows);
        return opened;
    }
    private SpawnEntry _spawnSelected;
    private int _spawnAmount = 1;
    private int _spawnDirIndex;
    private bool _spawnStick;
    private bool _spawnFavouritesOnly;
    // the favourite types of each tab (the owner, 2026-11-10), saved in the options file (DevMenuOptions.SpawnFavourites)
    private readonly HashSet<string>[] _favourites = { new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal) };
    private int _favouritesVersion;
    private string _allQuery = string.Empty;
    private readonly List<SpawnEntry> _allList = new();
    private readonly List<SpawnRow> _allRows = new(); // (its rows: flat, the dropdown is a search)
    private string _allListKey;

    private int _gasIndex;
    private GasOp _gasOp;
    private float _gasMoles = 50f;
    private float _gasKelvin = 293.15f;
    private int _gasRadius;
    private int _fillIndex;
    private string _fillMix = "o2=22;n2=82;TEMP=293.15";

    private (int X, int Y, int Z) _ownCell;
    private PickHit _toolHit;
    private PickShape _toolShape;
    private (int X, int Y, int Z) _toolCell;
    private bool _toolOnMap;
    private double _nextToolClickAt;
    private long _mountPicked;
    private string _mountPickedName;
    private MeshInstance3D _toolArea;

    private void BuildDevTools(IconLibrary icons)
    {
        _thumbs = new IconThumbs(icons, _imgui);
        _toolArea = new MeshInstance3D
        {
            Name = "DevToolArea",
            Mesh = new PlaneMesh { Size = new Vector2(1, 1) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.3f, 0.8f, 1f, 0.18f),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                DisableReceiveShadows = true,
                RenderPriority = 12,
            },
        };
        AddChild(_toolArea);
        // ANOMALY_DEV_TOOL=spawn:PATH|delete|gas:ID|possess: a tool armed from the start (scripted shots; with ANOMALY_DEV_MENU and --keys clicks)
        string tool = System.Environment.GetEnvironmentVariable("ANOMALY_DEV_TOOL");
        if (!string.IsNullOrEmpty(tool))
        {
            string[] parts = tool.Split(':', 2);
            _devTool = parts[0] switch { "spawn" => DevTool.Spawn, "delete" => DevTool.Delete, "gas" => DevTool.Gas, "possess" => DevTool.Possess, _ => DevTool.None };
            if (parts.Length > 1 && _devTool == DevTool.Spawn) _startPick = parts[1];
            if (parts.Length > 1 && _devTool == DevTool.Gas)
                _gasIndex = Math.Max(0, GasCatalog.All.ToList().FindIndex(g => g.Id == parts[1]));
        }
    }

    private string _startPick;

    // tg's types are read off the main thread the first time the menu opens (a few seconds).
    private void EnsureCatalog()
    {
        if (_catalog != null || _catalogTask != null || _catalogFailed) return;
        bool areas = EditorOn; // the map editor's palette lists tg's areas too
        _catalogTask = Task.Run(() => SpawnCatalog.Build(areas));
    }

    // Each frame after the interaction: the catalog, the icons, and what the armed tool points at under the mouse.
    private void UpdateDevTools(bool hasOwn, RenderPose pose)
    {
        _ownCell = hasOwn ? ((int)Math.Round(pose.X), (int)Math.Round(pose.Y), pose.Z) : _cameraCell;
        if (_catalog == null && _catalogTask is { IsCompleted: true })
        {
            if (_catalogTask.IsCompletedSuccessfully)
            {
                _catalog = _catalogTask.Result;
                if (_startPick != null) _spawnSelected = _catalog.Find(_startPick);
            }
            else
            {
                GD.PushError("dev menu: tg's types could not be read: " + _catalogTask.Exception?.GetBaseException().Message);
                _catalogFailed = true; // not read again every frame
            }
        }
        _thumbs?.Pump(_devMenuOpen ? 4.0 : 1.0);

        _toolOnMap = false;
        _toolArea.Visible = false;
        if (!DevMenuHasMouse || _devTool == DevTool.None || _imgui.WantsMouse || _imgui.IsOverWindow(GetViewport().GetMousePosition()) || _rig.Camera == null || _feed.Map == null)
            return;
        if (!PickToolAt(GetViewport().GetMousePosition()))
            return;
        Camera3D camera = _rig.Camera;
        double now = App.NowSeconds;
        if (_devTool is DevTool.Delete or DevTool.Possess || BuildsCeiling)
        {
            _highlight.Show(_toolHit, _toolShape, camera.GlobalTransform.Basis, now);
            return;
        }
        // a block (a wall turf, a full-tile window) shows the cube it fills, anything else the floor it lands on (the owner, 2026-11-10); a turf spawned over a wall the wall
        bool wall = _devTool == DevTool.Spawn && SpawnsBlock || _toolHit.Surface == PickSurface.Wall && _toolCell == (_toolHit.X, _toolHit.Y, _toolHit.Z);
        var cell = new PickHit(wall ? PickSurface.Wall : PickSurface.Floor, TargetKind.Turf, 0, _toolCell.X, _toolCell.Y, _toolCell.Z, _toolHit.Distance, 0, 0, 0, 0, 0);
        _highlight.Show(cell, null, camera.GlobalTransform.Basis, now);
        if (_devTool == DevTool.Gas && _gasRadius > 0 && _gasOp != GasOp.Ignite)
        {
            float side = (2 * _gasRadius + 1) * TileSpace.TileSize;
            _toolArea.Scale = new Vector3(side, 1, side);
            _toolArea.Position = new Vector3(TileSpace.WorldX(_toolCell.X), TileSpace.WorldY(_toolCell.Z) + 0.05f, TileSpace.WorldZ(_toolCell.Y));
            _toolArea.Visible = true;
        }
    }

    // What the armed tool points at through a point of the screen: the hit and the cell a spawn or the air lands in (the hit
    // cell, or the one in front of a wall face).
    private bool PickToolAt(Vector2 screen)
    {
        _toolOnMap = false;
        Camera3D camera = _rig.Camera;
        if (camera == null || _feed.Map == null) return false;
        Vector3 origin = camera.ProjectRayOrigin(screen), normal = camera.ProjectRayNormal(screen);
        int z = _cameraCell.Z;
        _pickWorld.PickExclude = _feed.OwnEntity;
        Vector3 view = (-camera.GlobalTransform.Basis.Z).Normalized(); // the camera's forward: an item's picture is square to it (PickShape.FacesEye)
        var ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, normal.X, -normal.Z, normal.Y, 64.0, view.X, -view.Z, view.Y);
        // D295: a type that goes on a ceiling, aimed up through a hole, lands on the empty ceiling there
        _picker.PickEmptyCeilings = _devTool == DevTool.Spawn && _spawnSelected != null && CeilingSpawnLine(_spawnSelected.Path, 0, 0, 0) != null;
        _toolHit = _picker.Pick(ray, _feed.OwnEntity, entitiesOnly: false);
        _toolShape = _picker.LastShape;
        if (!_toolHit.IsHit || _toolHit.Kind == TargetKind.Screen)
            return false;
        // (a turf type replaces the wall itself; anything else lands on the floor in front of it). A wall's top and, sticking, any face is the cell in front
        // of that face: over the top, beside the side, under the ceiling (the owner, 2026-11-10: a wall on a wall, a floor on walls)
        bool onWall = _devTool == DevTool.Delete || (_devTool == DevTool.Spawn && _spawnSelected != null && (SpawnCatalog.TabOf(_spawnSelected.Path) == SpawnTab.Turfs || SpawnsBlock));
        bool top = _toolHit.Surface == PickSurface.Wall && _toolHit.HitUp >= WorldScale.WallHeight - 1e-3;
        if (_devTool != DevTool.Delete && (top || Sticking))
        {
            (int X, int Y, int Z) front = PickGrid.InFront(_toolHit, normal.X, -normal.Z, normal.Y, TileSpace.LevelHeight);
            // the cell over a top is on a level that stands on this one, or nowhere: over the top level (or under another map: tg's next z) nothing goes
            // (it went into the block itself: the owner's report)
            bool linked = front.Z == _toolHit.Z || front.Z > _toolHit.Z ? front.Z == _toolHit.Z || _feed.Replica?.IsLinkedBelow(front.Z) == true
                : _feed.Replica?.IsLinkedBelow(_toolHit.Z) == true;
            if (!linked || front.Z < 1 || front.Z > _feed.Map.Levels)
                return false;
            _toolCell = front;
        }
        else if (_toolHit.Surface == PickSurface.Wall && !onWall)
        {
            double east = _toolHit.HitEast - normal.X * 0.05, north = _toolHit.HitNorth + normal.Z * 0.05;
            _toolCell = ((int)Math.Floor(east / TileSpace.TileSize) + 1, (int)Math.Floor(north / TileSpace.TileSize) + 1, _toolHit.Z);
        }
        else
        {
            _toolCell = (_toolHit.X, _toolHit.Y, _toolHit.Z);
        }
        _toolOnMap = true;
        return true;
    }

    // The spawn tool puts a thing in the cell in front of the face clicked, a ceiling built from a floor type aside (the owner, 2026-11-10).
    private bool Sticking => (_spawnStick || SpawnsMount) && _devTool == DevTool.Spawn;

    // The selected type is wall furniture (a lamp, an alarm, a sign, a console on the wall): a click on a wall face hangs it there, facing the wall.
    private bool SpawnsMount => _spawnSelected != null && Shared.World.PropKinds.IsWallMountType(_spawnSelected.Path);

    // The way a mount spawned at the clicked cell faces: toward the wall face under the mouse (0: not on a wall's side, the Dir box decides).
    private byte MountDir()
        => SpawnsMount && _toolHit.Surface == PickSurface.Wall && _toolCell.Z == _toolHit.Z
            ? Shared.World.WallMounts.DirOfStep(_toolHit.X - _toolCell.X, _toolHit.Y - _toolCell.Y) : (byte)0;

    // The selected type is a block: a wall turf or a full-tile window.
    private bool SpawnsBlock => _spawnSelected != null && (_spawnSelected.Path.StartsWith("/turf/closed", StringComparison.Ordinal) || SpawnCatalog.IsBlockWindow(_spawnSelected.Path));

    // The full-tile window a hit is on (0: none).
    private long HitBlockWindow()
        => _toolHit.Kind == TargetKind.Entity && _feed.Replica != null && _feed.Replica.TryGet(_toolHit.Id, out var e) && SpawnCatalog.IsBlockWindow(e.TypePath) ? _toolHit.Id : 0;

    // What a block spawned with the mouse clears first (the owner, 2026-11-10): the block it is aimed at, which it replaces (a full-tile window is deleted, a wall
    // under a window scraped down to its plating; a wall turf replaces a wall itself), and under a window stuck on the top of a block of the level below the floor
    // of its cell, so the two join (no floor or plating under a stacked window).
    private IEnumerable<string> ClearsForBlock()
    {
        if (_devTool != DevTool.Spawn || !SpawnsBlock || BuildsCeiling)
            yield break;
        (int x, int y, int z) = _toolCell;
        bool window = SpawnCatalog.IsBlockWindow(_spawnSelected.Path);
        if (!Sticking && HitBlockWindow() is long id and not 0 && (_toolHit.X, _toolHit.Y, _toolHit.Z) == _toolCell)
            yield return Inv($"delete #{id}");
        if (window && !Sticking && _toolHit.Surface == PickSurface.Wall && (_toolHit.X, _toolHit.Y, _toolHit.Z) == _toolCell)
            yield return Inv($"delete {x} {y} {z}");
        if (window && _toolCell.Z > _toolHit.Z)
        {
            string turf = _feed.Replica?.TurfTypes.ElementAtOrDefault(_feed.Replica.TurfTypeIndexAt(x, y, z));
            if (turf != null && TurfStyle.Classify(turf) == TurfKind.Open)
                yield return Inv($"spawn /turf/open/openspace {x} {y} {z} 1 0");
        }
    }

    // The spawn tool aimed at a ceiling builds it (a floor turf, a lattice or a catwalk; anything else sticking goes in the cell under it).
    private bool BuildsCeiling => _devTool == DevTool.Spawn && _spawnSelected != null && _toolHit.Kind == TargetKind.Ceiling
        && (!_spawnStick || CeilingSpawnLine(_spawnSelected.Path, 0, 0, 0) != null);

    // A click on the map while the dev menu is open: true when a tool took it. The pick is made again at the click's own
    // position (a scripted click is at the centre of the screen, wherever the mouse is).
    private bool TryDevToolClick(InputEventMouseButton button)
    {
        if (!_devMenuOpen || _devTool == DevTool.None || _imgui.IsOverWindow(button.Position)) return false;
        if (button.ButtonIndex == MouseButton.Right)
        {
            _devTool = DevTool.None;
            return true;
        }
        if (button.ButtonIndex != MouseButton.Left) return false;
        if (button.CtrlPressed)
        {
            PickSpawnType(button.Position);
            return true;
        }
        if (!PickToolAt(button.Position)) return true;
        double now = App.NowSeconds;
        if (now < _nextToolClickAt) return true;
        string line = ToolLine(button.ShiftPressed);
        if (line != null)
        {
            _nextToolClickAt = now + ToolClickSeconds;
            foreach (string clear in ClearsForBlock())
                OnConsoleSubmitted(clear);
            OnConsoleSubmitted(line);
        }
        return true;
    }

    // Ctrl + click (the owner, 2026-11-10: a picker): the wall, floor, ceiling or thing under the mouse becomes the spawn tool's type, shown in its tab's list.
    private void PickSpawnType(Vector2 screen)
    {
        if (_catalog == null || _feed.Replica is not { } replica || !PickToolAt(screen)) return;
        (int x, int y, int z) = (_toolHit.X, _toolHit.Y, _toolHit.Z);
        Anomaly.Core.Motion.CeilingState ceiling = replica.CeilingAt(x, y, z);
        string path = _toolHit.Kind switch
        {
            TargetKind.Entity => replica.TryGet(_toolHit.Id, out var e) ? e.TypePath : null,
            TargetKind.Ceiling when ceiling.Sealed => ceiling.Turf,
            TargetKind.Ceiling when ceiling.Exists => ceiling.Lattice == Anomaly.Core.Motion.CeilingLattice.Catwalk ? "/obj/structure/lattice/catwalk" : "/obj/structure/lattice",
            TargetKind.Ceiling => replica.IsLinkedBelow(z + 1) ? replica.TurfTypes.ElementAtOrDefault(replica.TurfTypeIndexAt(x, y, z + 1)) : null, // the floor of the level above
            _ => replica.TurfTypes.ElementAtOrDefault(replica.TurfTypeIndexAt(x, y, z)),
        };
        SpawnEntry entry = path == null ? null : _catalog.Find(path, SpawnCatalog.IsBlockWindow(path) ? SpawnTab.Turfs : null);
        if (entry == null) return;
        _spawnSelected = entry;
        _devTool = DevTool.Spawn;
        // the list shows it: its tab, every category, no search, the helpers it is
        _spawnTab = entry.Tab;
        _spawnCategory = -1;
        _spawnQuery = string.Empty;
        _spawnMapping |= entry.Mapping;
        _spawnAbstract |= entry.Abstract;
        if (!_favourites[(int)entry.Tab].Contains(entry.Path)) _spawnFavouritesOnly = false;
        _spawnScrollToSelected = true;
        _startTab = "Spawn";
    }

    private bool _spawnScrollToSelected;

    // The dev command a click sends (null: nothing to do there).
    private string ToolLine(bool shift)
    {
        (int x, int y, int z) = _toolCell;
        switch (_devTool)
        {
            case DevTool.Spawn:
                if (BuildsCeiling)
                    return CeilingSpawnLine(_spawnSelected.Path, x, y, z);
                return _spawnSelected == null ? null : SpawnLine(_spawnSelected, x, y, z, MountDir());
            case DevTool.Delete:
                // D295: a click takes the whole ceiling away, Shift + click scrapes one layer off it (tg's ScrapeAway)
                if (_toolHit.Kind == TargetKind.Ceiling)
                    return Inv($"ceiling {(shift ? "scrape" : "none")} {x} {y} {z}");
                if (_toolHit.Kind == TargetKind.Entity)
                    return Inv($"delete #{_toolHit.Id}");
                return shift ? Inv($"delete {x} {y} {z}") : null;
            case DevTool.Gas:
                return GasLine(x, y, z);
            case DevTool.Rotate:
                // DV56: the way a thing faces (a closet's 3D model), for the round; the map editor turns what a map keeps
                return _toolHit.Kind == TargetKind.Entity ? Inv($"rotate #{_toolHit.Id} {(shift ? "ccw" : "cw")}") : null;
            case DevTool.Mount:
                return MountToolLine();
            case DevTool.Possess:
                // D346: Shift + click takes a mob from its player (they become a ghost); the tool goes away once it is used
                if (_toolHit.Kind != TargetKind.Entity || _feed.Replica?.TryGet(_toolHit.Id, out ReplicaEntity aimed) != true || !aimed.TypePath.StartsWith("/mob/living", StringComparison.Ordinal))
                    return null;
                _devTool = DevTool.None;
                SetMobTarget("me");
                return Inv($"possess #{_toolHit.Id}{(shift ? " force" : "")}");
        }
        return null;
    }

    // The spawn tool aimed at a ceiling: a floor turf, a lattice or a catwalk becomes the ceiling's block (null: nothing else goes there).
    private static string CeilingSpawnLine(string path, int x, int y, int z) => path switch
    {
        "/obj/structure/lattice" => Inv($"ceiling lattice {x} {y} {z}"),
        "/obj/structure/lattice/catwalk" => Inv($"ceiling catwalk {x} {y} {z}"),
        _ when path.StartsWith("/turf/open/floor", StringComparison.Ordinal) => Inv($"ceiling {path} {x} {y} {z}"),
        _ => null,
    };

    // DV56: the first click picks a wall mount, the next a bare wall face to hang it on (a click on another thing picks that one instead).
    private string MountToolLine()
    {
        if (_toolHit.Kind == TargetKind.Entity)
        {
            _mountPicked = _toolHit.Id;
            _mountPickedName = TargetName(ClickTarget.Of(_toolHit));
            return null;
        }
        string side = MountSide();
        if (_mountPicked == 0 || side == null) return null;
        string line = Inv($"mount #{_mountPicked} {_toolHit.X} {_toolHit.Y} {_toolHit.Z} {side}");
        _mountPicked = 0;
        return line;
    }

    // The side of the wall under the mouse: toward the cell in front of the face that was hit.
    private string MountSide()
    {
        if (_toolHit.Surface != PickSurface.Wall) return null;
        return (_toolCell.X - _toolHit.X, _toolCell.Y - _toolHit.Y) switch
        {
            (0, 1) => "north",
            (0, -1) => "south",
            (1, 0) => "east",
            (-1, 0) => "west",
            _ => null,
        };
    }

    private string SpawnLine(SpawnEntry entry, int x, int y, int z, byte dir = 0)
        => Inv($"spawn {entry.Path} {x} {y} {z} {_spawnAmount} {(dir != 0 ? dir : DirValues[_spawnDirIndex])}");

    private string GasLine(int x, int y, int z)
    {
        IReadOnlyList<GasEntry> gases = GasCatalog.All;
        GasEntry gas = gases.Count > 0 ? gases[Math.Clamp(_gasIndex, 0, gases.Count - 1)] : null;
        string at = Inv($"{x} {y} {z} {_gasRadius}");
        switch (_gasOp)
        {
            case GasOp.Add: return gas == null ? null : Inv($"air add {gas.Id} {_gasMoles:0.###} {at}");
            case GasOp.Remove: return gas == null ? null : Inv($"air add {gas.Id} {-_gasMoles:0.###} {at}");
            case GasOp.RemoveAll: return gas == null ? null : $"air remove {gas.Id} {at}";
            case GasOp.Temperature: return Inv($"air temp {_gasKelvin:0.##} {at}");
            case GasOp.Fill: return "air set " + FillMix(gas) + " " + at;
            case GasOp.Ignite: return Inv($"ignite {x} {y} {z}");
        }
        return null;
    }

    private string FillMix(GasEntry gas) => _fillIndex switch
    {
        0 => "air",
        1 => "vacuum",
        2 => gas == null ? "air" : Inv($"{gas.Id}={GasOverlay.MolesCellStandard:0.##};TEMP=293.15"),
        _ => string.IsNullOrWhiteSpace(_fillMix) ? "air" : _fillMix.Trim(),
    };

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // ---- the windows ----

    private void DrawToolBar()
    {
        ImGui.TextUnformatted("Tool:");
        ImGui.SameLine();
        if (ImGui.RadioButton("none", _devTool == DevTool.None)) _devTool = DevTool.None;
        ImGui.SameLine();
        if (ImGui.RadioButton("spawn", _devTool == DevTool.Spawn)) _devTool = DevTool.Spawn;
        ImGui.SameLine();
        if (ImGui.RadioButton("delete", _devTool == DevTool.Delete)) _devTool = DevTool.Delete;
        ImGui.SameLine();
        if (ImGui.RadioButton("gas", _devTool == DevTool.Gas)) _devTool = DevTool.Gas;
        ImGui.SameLine();
        if (ImGui.RadioButton("rotate", _devTool == DevTool.Rotate)) _devTool = DevTool.Rotate;
        ImGui.SameLine();
        if (ImGui.RadioButton("mount", _devTool == DevTool.Mount)) _devTool = DevTool.Mount;
        ImGui.SameLine();
        if (ImGui.RadioButton("possess", _devTool == DevTool.Possess)) _devTool = DevTool.Possess;
        ImGui.TextDisabled(_devTool switch
        {
            DevTool.Spawn => _spawnSelected == null ? "Pick a type in Spawn first" : "Left click the map: spawn there (wall furniture, a lamp or a sign, hangs on the wall face you click, facing it; a floor turf, a lattice or a catwalk aimed at a ceiling or a hole in it builds the ceiling); Ctrl + click a wall, floor, ceiling or thing: pick its type; right click: put the tool away",
            DevTool.Delete => "Left click a thing or a ceiling: delete it; Shift + click a wall, floor or ceiling: scrape it away",
            DevTool.Gas => "Left click the map: apply the gas tool there; right click: put it away",
            DevTool.Rotate => "Left click a thing: turn it clockwise; Shift + click: counter-clockwise (for the round; " + ClientArgs.EditorName + " turns what a map keeps)",
            DevTool.Mount => "Left click a wall mount, then a wall face: hang it there (for the round; " + ClientArgs.EditorName + " hangs what a map keeps)",
            DevTool.Possess => "Left click a mob: play it (Shift + click: take it from its player, who becomes a ghost); the Player tab's Possess section takes you back",
            _ => "Arm a tool, then click on the map under the mouse; with a tool armed, Ctrl + click picks the type of what is under the mouse for Spawn",
        });
    }

    // The tooltip at the mouse over the map: what a click does there.
    private void DrawToolTooltip()
    {
        if (!_toolOnMap || _devTool == DevTool.None) return;
        (int x, int y, int z) = _toolCell;
        string text = _devTool switch
        {
            DevTool.Spawn when BuildsCeiling
                => CeilingSpawnLine(_spawnSelected.Path, x, y, z) is string line ? line : "only a floor turf, a lattice or a catwalk goes on a ceiling",
            DevTool.Spawn => _spawnSelected == null ? "pick a type first" : Inv($"spawn {_spawnAmount} x {_spawnSelected.Name} at ({x}, {y}, {z})"),
            DevTool.Delete when _toolHit.Kind == TargetKind.Ceiling => Inv($"click: remove the ceiling at ({x}, {y}, {z}); Shift + click: scrape one layer"),
            DevTool.Delete => _toolHit.Kind == TargetKind.Entity ? "delete " + TargetName(ClickTarget.Of(_toolHit)) + Inv($" #{_toolHit.Id}") : Inv($"Shift + click: scrape away ({x}, {y}, {z})"),
            DevTool.Gas => GasLine(x, y, z) ?? "",
            DevTool.Rotate => _toolHit.Kind == TargetKind.Entity ? "rotate " + TargetName(ClickTarget.Of(_toolHit)) + Inv($" #{_toolHit.Id}") : "",
            DevTool.Possess => _toolHit.Kind == TargetKind.Entity && _feed.Replica?.TryGet(_toolHit.Id, out ReplicaEntity aimed) == true && aimed.TypePath.StartsWith("/mob/living", StringComparison.Ordinal)
                ? "possess " + TargetName(ClickTarget.Of(_toolHit)) + Inv($" #{_toolHit.Id}") : "only a living mob can be possessed",
            DevTool.Mount => _toolHit.Kind == TargetKind.Entity ? "pick " + TargetName(ClickTarget.Of(_toolHit)) + Inv($" #{_toolHit.Id}")
                : _mountPicked == 0 ? "pick a wall mount first"
                : MountSide() is { } side ? "hang " + _mountPickedName + Inv($" on the {side} side of ({_toolHit.X}, {_toolHit.Y})") : "pick a wall face",
            _ => "",
        };
        ImGui.BeginTooltip();
        if (_devTool == DevTool.Spawn && _spawnSelected != null)
        {
            Icon(_spawnSelected.Icon, _spawnSelected.IconState, 32 * _imgui.UiScale);
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(text);
        ImGui.EndTooltip();
    }

    private void DrawSpawnTab()
    {
        float s = _imgui.UiScale;
        if (!DrawTypePicker(150 * s + ImGui.GetTextLineHeightWithSpacing())) // (the footer below and the picker's hint line)
            return;
        SpawnEntry e = _spawnSelected;
        ImGui.SetNextItemWidth(120 * s);
        ImGui.SliderInt("Amount", ref _spawnAmount, 1, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120 * s);
        ImGui.Combo("Dir", ref _spawnDirIndex, DirNames, DirNames.Length);
        ImGui.SameLine();
        ImGui.Checkbox("Stick", ref _spawnStick);
        ImGui.SetItemTooltip("Place in the cell in front of the face clicked: on a wall's or window's top, beside its side, under a ceiling");
        if (ImGui.Button(_devTool == DevTool.Spawn ? "Placing: click the map" : "Place with the mouse"))
            _devTool = _devTool == DevTool.Spawn ? DevTool.None : DevTool.Spawn;
        ImGui.SameLine();
        if (ImGui.Button("At my feet"))
            OnConsoleSubmitted(SpawnLine(e, _ownCell.X, _ownCell.Y, _ownCell.Z));
        if (e.IsItem)
        {
            ImGui.SameLine();
            if (ImGui.Button("In my hand"))
                OnConsoleSubmitted("give me " + e.Path);
        }
    }

    // The list of tg's types with their pictures, by tab, category and search, and the selected type under it; the dev menu's spawn tab and the map
    // editor's palette. True when a type is selected. footer: the height the caller draws under it.
    private bool DrawTypePicker(float footer)
    {
        EnsureCatalog();
        if (_catalog == null)
        {
            ImGui.TextUnformatted(_catalogFailed ? "tg's types could not be read (see the log)" : "Reading tg's types (a few seconds the first time)...");
            return false;
        }
        float s = _imgui.UiScale;

        // the dropdown of every type
        ImGui.SetNextItemWidth(-1);
        string preview = _spawnSelected != null ? _spawnSelected.Name + "   " + _spawnSelected.Path : "Every type: search and pick...";
        if (ImGui.BeginCombo("##every", preview, ImGuiComboFlags.HeightLargest))
        {
            // Esc closes the dropdown (the first one also leaves its search field); the last search is selected when it opens
            // again, so typing replaces it
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) ImGui.CloseCurrentPopup();
            if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##everyq", "search every type by name or path", ref _allQuery, 128, ImGuiInputTextFlags.AutoSelectAll);
            string key = _allQuery;
            if (key != _allListKey)
            {
                _catalog.Filter(null, -1, _allQuery, true, true, _allList);
                SpawnTree.Flat(_allList, _allRows);
                _allListKey = key;
            }
            ImGui.TextDisabled(Inv($"{_allList.Count} of {_catalog.Entries.Count} types"));
            // at most half the screen, so the dropdown leaves the menu (its close button) and the map in view
            float everyHeight = Math.Min(360 * s, GetViewport().GetVisibleRect().Size.Y * 0.5f);
            if (ImGui.BeginChild("##everylist", new NVector2(0, everyHeight)))
            {
                if (TypeList(_allRows, false, 24 * s, out SpawnEntry picked, out _, out _))
                {
                    Select(picked);
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndChild();
            ImGui.EndCombo();
        }

        // tg's three lists, the categories, the search
        for (int i = 0; i < (EditorOn ? TabNames.Length : TabNames.Length - 1); i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.RadioButton(TabNames[i], (int)_spawnTab == i))
            {
                _spawnTab = (SpawnTab)i;
                _spawnCategory = -1;
            }
        }
        IReadOnlyList<SpawnCategory> categories = SpawnCatalog.CategoriesOf(_spawnTab);
        string[] categoryNames = new string[categories.Count + 1];
        categoryNames[0] = "All " + TabNames[(int)_spawnTab].ToLowerInvariant();
        for (int i = 0; i < categories.Count; i++) categoryNames[i + 1] = categories[i].Name;
        int category = _spawnCategory + 1;
        ImGui.SetNextItemWidth(180 * s);
        if (ImGui.Combo("##category", ref category, categoryNames, categoryNames.Length))
            _spawnCategory = category - 1;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##spawnq", "search by name or path", ref _spawnQuery, 128);
        ImGui.Checkbox("Favourites", ref _spawnFavouritesOnly);
        ImGui.SetItemTooltip("Only the types starred in this tab (click a row's star to add or take it away)");
        ImGui.SameLine();
        ImGui.Checkbox("Mapping helpers", ref _spawnMapping);
        ImGui.SameLine();
        ImGui.Checkbox("Abstract types", ref _spawnAbstract);

        string listKey = Inv($"{_spawnTab}|{_spawnCategory}|{_spawnMapping}|{_spawnAbstract}|{_spawnQuery}|{_spawnFavouritesOnly}|{_favouritesVersion}");
        if (listKey != _spawnListKey)
        {
            _catalog.Filter(_spawnTab, _spawnCategory, _spawnQuery, _spawnMapping, _spawnAbstract, _spawnList, _spawnFavouritesOnly ? _favourites[(int)_spawnTab] : null);
            _spawnListKey = listKey;
            SpawnRowsOfList();
        }
        // the list takes what the selected type's footer leaves, but never less than three rows (a short window scrolls
        // instead: the list was a thin line in a window under about 1000 pixels)
        float listHeight = Math.Max(ImGui.GetContentRegionAvail().Y - footer, 3 * 37 * s);
        if (ImGui.BeginChild("##spawnlist", new NVector2(0, listHeight), ImGuiChildFlags.Borders))
        {
            if (_spawnScrollToSelected)
            {
                // a type picked off the map: its row in the middle of the list (the row height as TypeList's)
                SpawnUnfoldTo(_spawnSelected);
                int row = _spawnRows.FindIndex(r => ReferenceEquals(r.Entry, _spawnSelected));
                float stride = Math.Max(32 * s, ImGui.GetTextLineHeight() * 2) + 2 + ImGui.GetStyle().ItemSpacing.Y;
                if (row >= 0) ImGui.SetScrollY(Math.Max(0, row * stride - (listHeight - stride) / 2));
                _spawnScrollToSelected = false;
            }
            if (TypeList(_spawnRows, _spawnTree != null, 32 * s, out SpawnEntry picked, out bool doubled, out string toggled))
            {
                Select(picked);
                if (doubled && !EditorOn) OnConsoleSubmitted(Inv($"spawn {picked.Path} {_ownCell.X} {_ownCell.Y} {_ownCell.Z} {_spawnAmount} {DirValues[_spawnDirIndex]}"));
            }
            if (toggled != null)
                SpawnToggleBranch(toggled);
        }
        ImGui.EndChild();
        ImGui.TextDisabled(_spawnFavouritesOnly && _spawnList.Count == 0 ? "No favourites in this tab yet: star a type in the list"
            : EditorOn ? Inv($"{_spawnList.Count} types") : Inv($"{_spawnList.Count} types; double click: spawn at your feet"));
        ImGui.PushTextWrapPos(0);
        ImGui.TextDisabled(EditorOn ? "Ctrl + click a wall, floor or thing on the map: pick it, with its vars, to place"
            : "Ctrl + click a wall, floor, ceiling or thing on the map (with a tool armed): pick its type here");
        ImGui.PopTextWrapPos();

        // the selected type
        if (_spawnSelected == null)
        {
            ImGui.TextUnformatted(EditorOn ? "Pick a type to place." : "Pick a type to spawn.");
            return false;
        }
        SpawnEntry e = _spawnSelected;
        Icon(e.Icon, e.IconState, 48 * s);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextUnformatted(e.Name);
        ImGui.TextDisabled(e.Path);
        if (!string.IsNullOrEmpty(e.Description))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextDisabled(e.Description);
            ImGui.PopTextWrapPos();
        }
        ImGui.EndGroup();
        return true;
    }

    private void Select(SpawnEntry entry)
    {
        _spawnSelected = entry;
        if (_devTool == DevTool.None) _devTool = DevTool.Spawn;
        if (EditorOn) EditorTypeChosen();
    }

    // A clipped list of types, an icon and the name and path on each row; true when a type was clicked. As a tree (SpawnTree) a row stands in
    // by its depth, is named by the last part of its path under its name, and a branch has an arrow: a click on the arrow, or on the row of
    // a branch that is no type itself, folds or unfolds it (toggled: its path).
    private unsafe bool TypeList(IReadOnlyList<SpawnRow> rows, bool tree, float iconSize, out SpawnEntry picked, out bool doubled, out string toggled)
    {
        picked = null;
        doubled = false;
        toggled = null;
        float line = ImGui.GetTextLineHeight();
        float rowHeight = Math.Max(iconSize, line * 2) + 2, step = line * 1.1f;
        ImGuiListClipperPtr clipper = new(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(rows.Count, rowHeight + ImGui.GetStyle().ItemSpacing.Y);
        uint text = ImGui.GetColorU32(ImGuiCol.Text), dim = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        while (clipper.Step())
        {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                SpawnRow row = rows[i];
                SpawnEntry e = row.Entry;
                NVector2 at = ImGui.GetCursorScreenPos();
                float width = ImGui.GetContentRegionAvail().X, left = tree ? (row.Depth + 1) * step : 0;
                if (ImGui.Selectable("##" + row.Path, e != null && ReferenceEquals(e, _spawnSelected), ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowOverlap, new NVector2(0, rowHeight)))
                {
                    if (row.Branch && (e == null || ImGui.GetMousePos().X < at.X + left))
                        toggled = row.Path;
                    else if (e != null)
                    {
                        picked = e;
                        doubled = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
                    }
                }
                if (e != null && ImGui.IsItemHovered() && !string.IsNullOrEmpty(e.Description))
                    ImGui.SetItemTooltip(e.Description);
                ImDrawListPtr draw = ImGui.GetWindowDrawList();
                if (e != null)
                {
                    // the star at the row's right end: a click adds the type to its tab's favourites or takes it away
                    HashSet<string> favourites = _favourites[(int)e.Tab];
                    bool favourite = favourites.Contains(e.Path);
                    float star = Math.Min(rowHeight, line * 1.6f);
                    var starAt = new NVector2(at.X + width - star - 4, at.Y + (rowHeight - star) / 2);
                    ImGui.SameLine();
                    ImGui.SetCursorScreenPos(starAt);
                    if (ImGui.InvisibleButton("##fav" + e.Path, new NVector2(star, star)))
                    {
                        if (!favourites.Remove(e.Path)) favourites.Add(e.Path);
                        _favouritesVersion++;
                    }
                    bool starHovered = ImGui.IsItemHovered();
                    if (starHovered) ImGui.SetItemTooltip(favourite ? "Take it out of the favourites" : "Add it to the favourites");
                    DrawStar(draw, starAt + new NVector2(star / 2, star / 2), star * 0.45f, favourite, starHovered);
                    DrawIcon(draw, e.Icon, e.IconState, at + new NVector2(left + 1, 1), iconSize);
                }
                if (row.Branch)
                {
                    // the arrow: to the right when the branch is folded, down when it shows what stands under it
                    var c = new NVector2(at.X + left - step / 2, at.Y + rowHeight / 2);
                    float r = step * 0.3f;
                    if (row.Open) draw.AddTriangleFilled(c + new NVector2(-r, -r * 0.6f), c + new NVector2(r, -r * 0.6f), c + new NVector2(0, r * 0.8f), dim);
                    else draw.AddTriangleFilled(c + new NVector2(-r * 0.6f, -r), c + new NVector2(r * 0.8f, 0), c + new NVector2(-r * 0.6f, r), dim);
                }
                // flat: the name over the whole path. In a tree the path is the way down to the row: the last part of it, and what a branch holds
                string held = row.Types == 1 ? "1 type" : Inv($"{row.Types} types");
                string under = !tree ? e?.Path : !row.Branch ? row.Leaf : e == null ? held : row.Leaf + "  \u00b7  " + held;
                float tx = at.X + left + iconSize + 8;
                draw.AddText(new NVector2(tx, at.Y + 1), text, e?.Name ?? row.Leaf);
                draw.AddText(new NVector2(tx, at.Y + 1 + line), dim, under ?? string.Empty);
            }
        }
        clipper.End();
        clipper.Destroy();
        return picked != null;
    }

    // A five-pointed star: filled gold for a favourite, an outline otherwise (brighter under the mouse).
    private static void DrawStar(ImDrawListPtr draw, NVector2 centre, float radius, bool filled, bool hovered)
    {
        Span<NVector2> points = stackalloc NVector2[10];
        for (int k = 0; k < 10; k++)
        {
            float r = k % 2 == 0 ? radius : radius * 0.45f;
            float angle = -MathF.PI / 2 + k * MathF.PI / 5;
            points[k] = centre + new NVector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }
        uint colour = filled ? ImGui.GetColorU32(new NVector4(1f, 0.8f, 0.2f, 1f)) : ImGui.GetColorU32(new NVector4(1, 1, 1, hovered ? 0.8f : 0.35f));
        for (int k = 0; k < 10; k++)
        {
            NVector2 a = points[k], b = points[(k + 1) % 10];
            if (filled) draw.AddTriangleFilled(centre, a, b, colour); // a star is star-shaped from its centre: a fan fills it
            else draw.AddLine(a, b, colour, 1.2f);
        }
    }

    // An icon in the layout (a dummy of its size).
    private void Icon(string icon, string state, float size)
    {
        NVector2 at = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new NVector2(size, size));
        DrawIcon(ImGui.GetWindowDrawList(), icon, state, at, size);
    }

    // The picture fitted into a square, the largest whole scale when it fits (tg's pixel art), a grey box until it is made.
    private void DrawIcon(ImDrawListPtr draw, string icon, string state, NVector2 at, float size)
    {
        if (_thumbs == null || !_thumbs.TryGet(icon, state, out Thumb thumb))
        {
            draw.AddRectFilled(at, at + new NVector2(size, size), ImGui.GetColorU32(new NVector4(1, 1, 1, 0.06f)), 3);
            return;
        }
        if (!thumb.HasPicture)
        {
            draw.AddRect(at, at + new NVector2(size, size), ImGui.GetColorU32(new NVector4(1, 1, 1, 0.15f)), 3);
            return;
        }
        float fit = size / Math.Max(thumb.Width, thumb.Height);
        float scale = fit >= 1 ? MathF.Floor(fit) : fit;
        var drawn = new NVector2(thumb.Width * scale, thumb.Height * scale);
        NVector2 min = at + (new NVector2(size, size) - drawn) / 2;
        draw.AddImage(thumb.Id, min, min + drawn);
    }

    private void DrawGasTab()
    {
        float s = _imgui.UiScale;
        IReadOnlyList<GasEntry> gases = GasCatalog.All;
        if (gases.Count == 0)
        {
            ImGui.TextUnformatted("No gases (tg's /datum/gas types were not found)");
            return;
        }
        _gasIndex = Math.Clamp(_gasIndex, 0, gases.Count - 1);

        ImGui.TextUnformatted("Gas");
        float cell = 150 * s;
        int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cell));
        if (ImGui.BeginChild("##gases", new NVector2(0, 190 * s), ImGuiChildFlags.Borders))
        {
            if (ImGui.BeginTable("##gastable", columns))
            {
                float icon = 28 * s;
                for (int i = 0; i < gases.Count; i++)
                {
                    ImGui.TableNextColumn();
                    GasEntry gas = gases[i];
                    NVector2 at = ImGui.GetCursorScreenPos();
                    if (ImGui.Selectable("##gas" + gas.Id, i == _gasIndex, ImGuiSelectableFlags.None, new NVector2(0, icon + 2)))
                        _gasIndex = i;
                    if (ImGui.IsItemHovered())
                        ImGui.SetItemTooltip(gas.Id + "  " + gas.Path + (gas.Overlay == null ? "  (invisible)" : ""));
                    ImDrawListPtr draw = ImGui.GetWindowDrawList();
                    DrawGasIcon(draw, gas, at + new NVector2(1, 1), icon);
                    draw.AddText(new NVector2(at.X + icon + 6, at.Y + (icon - ImGui.GetTextLineHeight()) / 2 + 1), ImGui.GetColorU32(ImGuiCol.Text), gas.Name);
                }
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        GasEntry chosen = gases[_gasIndex];
        int op = (int)_gasOp;
        for (int i = 0; i < GasOpNames.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.RadioButton(GasOpNames[i], op == i)) _gasOp = (GasOp)i;
        }
        switch (_gasOp)
        {
            case GasOp.Add:
            case GasOp.Remove:
                ImGui.SliderFloat("Moles", ref _gasMoles, 0.1f, 10000f, "%.1f", ImGuiSliderFlags.Logarithmic);
                foreach (float preset in new[] { 1f, 10f, 50f, 100f, 500f, 2000f })
                {
                    if (ImGui.SmallButton(Inv($"{preset:0}"))) _gasMoles = preset;
                    ImGui.SameLine();
                }
                ImGui.TextDisabled("mol (a room's tile holds 104)");
                break;
            case GasOp.Temperature:
                ImGui.SliderFloat("Kelvin", ref _gasKelvin, 2.7f, 10000f, "%.1f", ImGuiSliderFlags.Logarithmic);
                ImGui.TextDisabled(Inv($"{_gasKelvin - 273.15f:0.#} C"));
                foreach ((string name, float k) in new[] { ("cold 0 C", 273.15f), ("room 20 C", 293.15f), ("hot 100 C", 373.15f), ("fire 1000 K", 1000f) })
                {
                    if (ImGui.SmallButton(name)) _gasKelvin = k;
                    ImGui.SameLine();
                }
                ImGui.NewLine();
                break;
            case GasOp.Fill:
                ImGui.Combo("Air", ref _fillIndex, FillNames, FillNames.Length);
                if (_fillIndex == 3)
                    ImGui.InputText("##mix", ref _fillMix, 120);
                ImGui.TextDisabled(FillMix(chosen));
                break;
            case GasOp.Ignite:
                ImGui.TextDisabled("A spark (tg's sparks: hotspot_expose(1000, 100)); it needs oxygen and plasma, tritium or hydrogen");
                break;
            default:
                ImGui.TextDisabled("Every mole of " + chosen.Name + " out of the air");
                break;
        }
        if (_gasOp != GasOp.Ignite)
            ImGui.SliderInt("Radius", ref _gasRadius, 0, 7, _gasRadius == 0 ? "one tile" : "%d tiles");

        if (ImGui.Button(_devTool == DevTool.Gas ? "Applying: click the map" : "Apply with the mouse"))
            _devTool = _devTool == DevTool.Gas ? DevTool.None : DevTool.Gas;
        ImGui.SameLine();
        if (ImGui.Button("Where I stand"))
        {
            string line = GasLine(_ownCell.X, _ownCell.Y, _ownCell.Z);
            if (line != null) OnConsoleSubmitted(line);
        }
        ImGui.TextDisabled(GasLine(_ownCell.X, _ownCell.Y, _ownCell.Z) ?? "");
        ImGui.Separator();
        if (ImGui.Button("Normal air everywhere")) OnConsoleSubmitted("gas air");
        ImGui.SameLine();
        if (ImGui.Button("Show the air of each cell"))
        {
            _gasOverlayOn = true;
            _gasOverlay.Visible = true;
            _nextProbeAt = 0;
        }
    }

    // tg's overlay sprite of the gas over its colour; an invisible gas is a disc of its colour.
    private void DrawGasIcon(ImDrawListPtr draw, GasEntry gas, NVector2 at, float size)
    {
        Color c = GasOverlay.Rgb(gas.Rgb);
        NVector2 centre = at + new NVector2(size / 2, size / 2);
        if (gas.Overlay == null)
        {
            draw.AddCircleFilled(centre, size * 0.38f, ImGui.GetColorU32(new NVector4(c.R, c.G, c.B, 0.9f)));
            draw.AddCircle(centre, size * 0.38f, ImGui.GetColorU32(new NVector4(1, 1, 1, 0.35f)));
            return;
        }
        draw.AddRectFilled(at, at + new NVector2(size, size), ImGui.GetColorU32(new NVector4(0.12f, 0.12f, 0.14f, 1f)), 3);
        DrawIcon(draw, GasCatalog.Icon, gas.Overlay, at, size);
    }
}
