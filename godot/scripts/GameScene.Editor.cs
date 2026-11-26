using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Editor;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Client.Shared.Editor;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The map editor's mode of the game scene (docs/ai/specs/map-editor.md 3 and 5.2, D336): the scene draws an EditorFeed's map with the
// game's own renderer. There is no player: the camera is the free one, flown as an engine editor's (5.6: the right mouse button held, the
// mouse turns the view and W A S D Q E fly, Shift faster, the wheel changes the speed; the middle button, or Alt with the left, turns it
// around a point; the wheel alone goes toward what is under the mouse; F brings the selection into view; T is the top view). The
// left mouse button is the armed tool's: select, place, erase or pick, on the tile and the thing under the mouse. The place and the erase
// tools work by a shape (every tile crossed, a line, a rectangle, a circle, a zone); the select tool selects a thing, a tile or a rectangle
// of tiles, which is moved by dragging it, copied and pasted (5.5). Every change is a step of the document, taken back with Ctrl+Z. The
// simplest look (every surface lit, the low preset) is the default (R5), the game's interface is hidden by the scene's own switch, and
// the game's keys do nothing here.
public partial class GameScene
{
    private enum EditorTool
    {
        Select,
        Place,
        Erase,
        Pick,
    }

    // how the place tool puts the palette's type on what is clicked (5.8)
    private enum EditorPlaceMode
    {
        Add, // on the tile under the mouse: a turf in the turf's place, a thing one more thing; a thing against a wall's side on the tile in front of it
        Replace, // in the place of what was clicked: the thing or the turf of that type gives its place
        Attach, // on the tile the clicked face looks at: beside a block's side, over its top (the level above), on a floor's tile
    }

    private const float EditorSpeedStep = 1.25f;
    private const float EditorMinSpeed = 0.1f, EditorMaxSpeed = 20f;
    private const double EditorPickMetres = 2000; // a map is clicked from far above
    private const float EditorWheelShare = 0.85f; // a notch of the wheel leaves this share of the way to the point under the mouse
    private const float EditorNearest = 2f, EditorHighest = 650f; // how near the wheel brings the camera to that point, and how high above the floor it goes
    private const float EditorTopLowest = 3f; // the top view's least height: over the walls
    // notches of the wheel per unit of a trackpad's two-finger move. The engine hands every gesture to the scene twice (its gesture dispatch, then the
    // general one), so a unit moves the view by twice this and a pinch by its factor squared: measured by a scripted gesture, 2026-11-17, and left so,
    // since it is the speed the trackpad has had here and in the game's own zoom. An engine that sends each once would halve both.
    private const float EditorTrackpadNotch = 0.3f;
    private const float EditorSmallPartRange = 470f; // ChunkBatch.SmallPartRange in the editor
    private const float EditorDarkLifted = 0.3f; // the least light of a surface while the dark areas are shown lit

    // the parts of the game's look the view bar switches (5.6), in the bar's order
    private enum EditorLook
    {
        Lighting, // the map's own light: the lamps, and the dark where there are none (off: every surface lit)
        Shadows, // the lamps' and the sun's shadows
        Dark, // the dark where no lamp lights (off: lifted, so that what stands there is seen)
        Bloom,
    }

    // what the mouse does to the camera while a button holds it (5.6)
    private enum EditorCamera
    {
        Fly, // the mouse turns the view and the keys fly
        Orbit, // the camera turns around a point
        Pan, // the camera moves across its view: the map follows the mouse
    }

    // R5, the simplest look: the low preset at the full resolution, drawn unshaded with every surface lit (EditorSimple)
    private static readonly RenderPreset EditorSimplePreset = RenderPreset.Low with { Name = "editor", RenderScalePercent = 100, Glow = false };

    private EditorSession _editor; // null: the game
    private bool _editorLooking; // a mouse button holds the camera (_editorCamera says how): the mouse is the camera's, not the tools'
    private EditorCamera _editorCamera;
    private MouseButton _editorCameraButton; // the button that holds it
    private Vector3 _editorPivot; // the point an orbit turns around
    private float _editorPanScale; // metres across the view per pixel of the mouse while it pans
    private float _editorFlySpeed = 1f; // the wheel's share of the flight's speed
    private bool _editorTop; // the top view: straight down, north up, no perspective
    private (float Yaw, float Pitch) _editorTopFrom; // the view the top view was entered from
    private Godot.Environment.BGMode _editorTopSky; // and the background it had
    private bool _editorSimple = true; // R5: the simplest look is the default
    private readonly bool[] _editorLook = { true, true, true, true }; // R5: the game's look part by part (EditorLook), while the simplest look is off
    private bool _editorForcedQuality; // --quality chose the preset: the look's switch leaves it alone
    private string _editorNotice; // what the last save or a refused change said, shown for a few seconds
    private double _editorNoticeUntil;
    private int _editorLevel = 1; // the level that is edited
    private bool _editorAbove; // the levels over the edited one are shown too (5.8)
    private bool _editorLevelLock; // the tools work on the edited level alone (5.17); off, on what is under the mouse on any level shown
    private bool _editorArrowsOn; // an arrow over every thing that has a way to face (5.19)
    private const int EditorCeilingMember = -2; // _editorSelected's member for the ceiling over the tile (-1: the tile alone)
    private Func<float, float, int, bool> _editorInSight; // the things' part of the same rule, made once
    private EditorPlaceMode _editorPlaceMode;
    private string _editorReplaceTarget; // the replace mode: the type that gives its place, noted when the button went down
    private string _editorReplaceUnder; // and from under a level: what the clicked ceiling showed (MapEdits.UndersideOf)
    private int _editorHeight = 1; // the levels a shape, a stroke or a click of the place and the erase tools goes up through
    private int _editorStrokeZ; // the level a held stroke began on
    private bool _editorPreviewOn = true; // what the place tool is about to place is shown where the mouse is (5.9)
    private string _editorPreviewWanted, _editorPreviewGiven; // what the preview is to show, as a text that changes when it does, and what the world was given
    private double _editorPreviewSince; // when the wanted preview last changed: it is given once it has stood a moment
    private readonly List<PickShape> _editorPreviewShapes = new();
    private readonly PreviewTint _editorTint = new(); // the preview's blue, by the shaders
    private readonly List<PreviewBox> _editorTintBoxes = new();
    private FacingArrows _editorArrows; // the arrows over the things that have a way to face
    private (long Tick, long Revision, int Level, bool Above, int Beat) _editorArrowsKey; // what they were found for
    private readonly Dictionary<string, bool> _editorArrowTypes = new(StringComparer.Ordinal); // per type: whether it can get one
    private readonly List<(int X, int Y, int Z, int Dir)> _editorArrowList = new();
    private SelectionMark _editorSelectionMark, _editorHoverMark; // the frame around what is selected, and the thin one around what the mouse is over
    private readonly List<PickShape> _editorMarkShapes = new(); // the selected thing's shapes, as the world draws it
    private ((int X, int Y, int Z, int Member) Selected, long Revision, long Tick, long Thing) _editorMarkKey; // what they were found for

    private EditorTool _editorTool;
    private MapMember? _editorBrush; // a thing or a turf picked off the map with its vars: what the place tool places instead of the palette's bare type
    private (int X, int Y, int Z, int Member)? _editorSelected; // the selected tile and its member (-1: the tile alone)
    private PickHit _editorHit; // what is under the mouse
    private PickShape _editorShape;
    private (int X, int Y, int Z) _editorCell; // the tile the armed tool acts on there
    private bool _editorOnMap;
    private bool _editorStroke; // the left button is held with the place or the erase tool: one step of the document
    private EditorErase _editorErase; // what the erase tool takes away, by what the button went down on
    private bool _editorStrokeCeiling; // the button went down on a ceiling: the stroke or the shape is of ceilings
    private bool _editorStrokeSlab; // the place tool's button went down on a level's underside to change it: the stroke or the shape is of those
    private bool _editorStrokeUnder; // the button went down on a ceiling or an underside: the edited level does not follow what is made there
    private float _editorStrokeY; // the height of the world the button went down at: a held tool goes on over the tiles the mouse crosses there
    private bool _editorSlab; // the mouse is on a ceiling or on a level's underside, seen from under it (5.18)
    private bool _editorFromUnder; // or on the place where a ceiling would be: what the mouse is on is seen from under it
    private (int X, int Y, int Z) _editorRoom; // the tile under that ceiling
    private (int X, int Y, int Z) _editorStrokeCell;
    private long _editorStrokeThing;
    private Vector2 _editorStrokeMouse; // where the mouse was when the stroke last took a thing away
    private const float EditorEraseStep = 6f; // pixels the mouse moves before a held erase takes the next thing: a still mouse takes one

    // What the erase tool takes away (5.17): what was highlighted when the button went down, and the same kind on every tile a held stroke or a
    // shape reaches. A thing; one layer of a tile's turf (MapEdits.Peel); the ceiling's top layer; from under a level the tile laid under its
    // plating, or with none laid its floor or its wall, whole (5.18); with Shift the whole tile. While the palette shows the areas it takes a
    // tile's area and nothing else, Shift or not: an area is under no mouse, and a mapper who paints areas means them (EditorEraseFor).
    private enum EditorErase
    {
        Thing,
        Wall,
        Tile,
        Base,
        Ceiling,
        Laid,
        Slab,
        All,
        Area,
    }

    // the shapes and the selection (5.5)
    private static MapClip _editorClip; // what was copied: kept from map to map
    private static readonly Color MarkSelected = new(0.3f, 0.8f, 1f, 0.22f), MarkPlaced = new(1f, 0.85f, 0.3f, 0.34f), MarkErased = new(1f, 0.35f, 0.3f, 0.34f), MarkTarget = new(0.4f, 1f, 0.5f, 0.3f), MarkFound = new(1f, 0.45f, 0.1f, 0.4f),
        MarkOpenSky = new(0.45f, 0.75f, 1f, 0.3f), MarkCeiling = new(0.55f, 1f, 0.7f, 0.25f);
    private MapShape _editorDraw; // the shape the place and the erase tools work by
    private (int X, int Y, int Z)? _editorDragFrom; // a shape is dragged from this tile: the release makes it
    private (int X, int Y, int Z)? _editorBandFrom; // the select tool's rectangle is dragged from this tile
    private (int X, int Y, int Z)? _editorMoveFrom; // the selected rectangle, or the selected thing, is dragged by this tile
    private (int X, int Y, int Z, int Member)? _editorPressed; // what the select tool was pressed on, inside the selected rectangle
    private (int X, int Y, int Z) _editorDragTo; // the tile a drag has reached
    private (int X0, int Y0, int X1, int Y1, int Z)? _editorRegionLast;
    private readonly List<(int X0, int Y0, int X1, int Y1, int Z)> _editorMoreRegions = new(); // the rectangles selected before it and kept with Shift: of its level
    private bool _editorBandAdds; // the rectangle being dragged joins the selected ones (Shift)
    private bool _editorClipboard = true; // copies go to the system's clipboard and pastes come from it (a scripted run leaves it alone unless told)

    // The selected rectangle of tiles: the last one dragged. Setting it is a new selection, so the ones kept before it go (EditorAddRegion keeps them).
    private (int X0, int Y0, int X1, int Y1, int Z)? _editorRegion
    {
        get => _editorRegionLast;
        set
        {
            _editorRegionLast = value;
            _editorMoreRegions.Clear();
        }
    }
    private bool _editorPasting; // the copy follows the mouse: a click pastes it
    private byte _editorMountSide; // the place tool is against a wall's side: the way from its tile to that wall (0: not)
    private byte _editorShapeMount; // and was so where the button went down for a shape: its things hang on that side's walls
    private readonly List<(int X, int Y)> _editorRun = new(); // the tiles of a stroke that draws a run, in its order
    private readonly List<MapMember> _editorRunPieces = new(); // and the piece it placed on each
    private EditorCells _editorMarks, _editorAreaMarks, _editorFoundMarks, _editorCeilingMarks;
    private bool _editorAreas; // the layer of the areas: a colour over every tile of an area
    private bool _editorBorderOn = true; // the map's edge is drawn: a box of lines around all its tiles, level by level
    private MeshInstance3D _editorBorder;
    private (int Width, int Height, int Levels, int Level) _editorBorderShown;
    private static readonly Color BorderBright = new(1f, 0.72f, 0.2f), BorderDim = new(0.55f, 0.38f, 0.1f);

    /// <summary>True when the scene runs as the map editor.</summary>
    private bool EditorOn => _editor != null;

    private void StartEditor(App app, ClientArgs args)
    {
        _editor = app.Editing ? app.Editor : null; // (a round of the map that is played is the game's scene: GameScene.EditorPlay.cs)
        if (_editor == null)
            return;
        // the camera is already the free one (_Ready sets the mode before it places the view)
        _editor.Clock = () => App.NowSeconds; // (a setting's slider dragged is one undo step)
        _editorForcedQuality = args.Quality != null;
        _editorClipboard = !args.Automated; // (a scripted run leaves the system's clipboard as it is: editor clipboard on)
        ReadEditorSettings(args);
        StartEditorAutosave(args);
        ApplyEditorLook();
        InterfaceCommand("hide"); // the HUD, the chat and the minimap are the game's
        // the world behind the editor stands in the lobby: tg's lobby screen and its windows are the game's (the hidden interface keeps tgui's windows,
        // which a thing in the world opens; the editor has none)
        _tguiHost.Visible = false;
        AudioServer.SetBusMute(0, true); // the world behind the editor stands in the lobby: its title music and its sounds are not the editor's
        _pickWorld.IgnoreCeilings = true; // a map is edited from above: a click goes through a room's ceiling
        // (the preview of what the place tool places is drawn by the world and is not there for a pick: 5.9)
        _pickWorld.PickSkips = id => _editor.Feed.IsPreview(id);
        _pickWorld.WallOverride = (x, y, z) => _editor.Feed.Previewed(x, y, z) ? MapEdits.TurfOf(_editor.Document[x, y, z])?.StartsWith("/turf/closed", StringComparison.Ordinal) == true : null;
        _pickWorld.VoidOverride = (x, y, z) => _editor.Feed.Previewed(x, y, z) ? TurfStyle.Classify(MapEdits.TurfOf(_editor.Document[x, y, z])) == TurfKind.Void : null;
        _spawnMapping = true; // a mapper places the landmarks, the spawners and the helpers
        _devTool = DevTool.None;
        EnsureCatalog();
        GetTree().AutoAcceptQuit = false; // the window's close button asks first when the map has changes that are not saved
        _editorAreaMarks = new EditorCells(0.05f, 11) { Name = "EditorAreas" };
        _editorMarks = new EditorCells(0.07f, 13) { Name = "EditorMarks" };
        // what the search found: a box over each find's tile, drawn through the walls, so that where the finds are is seen at once (the owner, 2026-11-17)
        _editorFoundMarks = new EditorCells(WorldScale.WallHeight / 2f, 12, new BoxMesh { Size = new Vector3(1.5f, WorldScale.WallHeight, 1.5f) }) { Name = "EditorFound" };
        _editorCeilingMarks = new EditorCells(0.04f, 10) { Name = "EditorCeilings" };
        AddChild(_editorCeilingMarks);
        AddChild(_editorAreaMarks);
        AddChild(_editorFoundMarks);
        AddChild(_editorMarks);
        _editorBorder = new MeshInstance3D
        {
            Name = "EditorBorder",
            Mesh = new ImmediateMesh(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, DisableReceiveShadows = true },
            Visible = false,
        };
        AddChild(_editorBorder);
        AddChild(_editorArrows = new FacingArrows());
        AddChild(_editorHoverMark = new SelectionMark(SelectionMark.Hovered, 1.5f, 0f) { Name = "HoverMark" });
        AddChild(_editorSelectionMark = new SelectionMark(SelectionMark.Selected, 3f, 0.1f));
        BuildEditorShell();
        if (_editor.JsonUnreadable)
            EditorNotice("The map's json could not be read (" + _editor.JsonPath + "): the map's settings are the game's own, and a save leaves that file as it is.", 20);
        ResumeEditor(app);
    }

    // The simplest look (R5): every surface lit and drawn unshaded, so no lamp, shadow or bloom is worked out. The other look is the game's
    // own, by the player's graphics options, with the view bar's switches (5.6): the map's light (off: every surface lit; from above the
    // walls it is tg's light tile by tile, UpdateEditorCamera), the lamps' and the sun's shadows, the dark where no lamp lights (off: lifted,
    // so that what stands there is seen), the bloom. Neither look has the game's distance fog (ApplyLook): a map is looked at from far above.
    private void ApplyEditorLook()
    {
        _fullbright = !EditorLit;
        GetViewport().DebugDraw = _editorSimple ? Viewport.DebugDrawEnum.Unshaded : Viewport.DebugDrawEnum.Disabled;
        if (!_editorForcedQuality)
        {
            RenderPreset preset = _editorSimple ? EditorSimplePreset : _options.Graphics with { Name = "editor", Glow = _editorLook[(int)EditorLook.Bloom] && _options.Graphics.Glow };
            if (!_editorSimple && !(EditorLit && _editorLook[(int)EditorLook.Shadows]))
                preset = preset with { ShadowedLights = 0, SunShadow = 0, ObjectShadows = 0, CharacterShadows = false };
            ApplyQuality(preset);
        }
        ApplyLook();
    }

    // The map is lit by its own light (the lamps, and the dark where there are none), not every surface alike.
    private bool EditorLit => !_editorSimple && _editorLook[(int)EditorLook.Lighting];

    // The least light a surface gets: none, as in the game, or enough to see what stands in the dark while the lamps' light is looked at.
    private (float R, float G, float B) EditorLightFloor => EditorLit && !_editorLook[(int)EditorLook.Dark] ? (EditorDarkLifted, EditorDarkLifted, EditorDarkLifted) : default;

    // Every frame, after the game's own hover: the editor's windows take the mouse unless it is turning the view; what the armed tool points at;
    // a held stroke goes on over the tiles the mouse crosses.
    private void UpdateEditor()
    {
        if (_editor == null)
            return;
        _imgui.Capture = false; // the editor's interface is the engine's own controls (EditorShell)
        MapDocument map = _editor.Document;
        if (_editorSelected is { } selected && (!map.Contains(selected.X, selected.Y, selected.Z) || selected.Member >= map[selected.X, selected.Y, selected.Z].Members.Count))
            _editorSelected = map.Contains(selected.X, selected.Y, selected.Z) ? (selected.X, selected.Y, selected.Z, -1) : null; // an undo took the member away

        (_editorLevel, _editorHeight) = (Math.Clamp(_editorLevel, 1, map.Levels), Math.Clamp(_editorHeight, 1, map.Levels));
        UpdateEditorPlay(App.NowSeconds);
        UpdateEditorCamera();
        Vector2 mouse = GetViewport().GetMousePosition();
        _editorOnMap = !_editorLooking && _shell.OverView(mouse) && EditorPickAt(mouse);
        Camera3D camera = _rig.Camera;
        UpdateEditorPreview();
        UpdateEditorTint();
        if (_editorOnMap && camera != null)
        {
            // a thing shows its own shape; a tile the floor or the block the tool acts on; what the place tool is about to place, once the world shows it, its own
            bool thing = _editorHit.Kind == TargetKind.Entity && _editorTool != EditorTool.Place;
            PickShape previewed = thing ? null : EditorPreviewShape();
            // (what the place tool puts under a ceiling, on the tile the ceiling is over: marked on the ceiling, the mouse's place; a wall by its block)
            bool hangs = _editorSlab && _editorCell == _editorRoom && _editorTool == EditorTool.Place && !_editorPasting && EditorBrush() is { } hung && !EditorOnCeiling(hung);
            bool block = hangs ? MapEdits.LayerOf(EditorBrush().Value.Path) == TurfLayer.Wall : _editorHit.Surface == PickSurface.Wall && _editorCell == (_editorHit.X, _editorHit.Y, _editorHit.Z);
            PickHit shown = thing ? _editorHit
                : new PickHit(previewed != null ? previewed.Surface : block ? PickSurface.Wall : hangs || _editorHit.Kind == TargetKind.Ceiling ? PickSurface.Ceiling : PickSurface.Floor,
                    previewed != null ? TargetKind.Entity : TargetKind.Turf, 0, _editorCell.X, _editorCell.Y, _editorCell.Z, _editorHit.Distance, 0, 0, 0, 0, 0);
            // (the select tool's is fainter: what is selected has its own mark, and the mouse's is not to be taken for it)
            bool faint = _editorTool == EditorTool.Select && !_editorPasting;
            _highlight.Strength = faint ? 0.45f : 1f;
            PickShape around = thing ? _editorShape : previewed;
            _highlight.Show(shown, around, camera.GlobalTransform.Basis, App.NowSeconds);
            // and a thin frame around it: the slab alone is lost on a thing as thin as a cable, and on a bright floor
            HoverHighlight.BoxOf(shown, around, camera.GlobalTransform.Basis, camera.GlobalPosition.Y, out Vector3 size, out Vector3 centre, out Basis turn);
            _editorHoverMark.Strength = faint ? 0.4f : 0.85f;
            _editorHoverMark.ShowBox(centre, size, turn, camera);
        }
        else
            _editorHoverMark.HideBox();
        UpdateEditorSelectionMark(camera);
        UpdateEditorArrows();
        if (_editorStroke || EditorDragging)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                EditorRelease(); // the release went to a window
            }
            else if (_editorOnMap)
            {
                if (_editorStroke)
                    EditorStrokeAt();
                else if (_editorCell.Z == _editorDragTo.Z)
                    _editorDragTo = _editorCell;
            }
        }
        UpdateEditorMarks();
        UpdateEditorShell();
    }

    private bool EditorDragging => _editorDragFrom != null || _editorBandFrom != null || _editorMoveFrom != null;

    // Every frame while the arrows are on (5.19): an arrow over each thing of the shown levels that has a way to face, pointing the way the world
    // shows it to face. A marker gets one (a spawner's way is its pick's), and a thing whose picture has a side for each way or that a model draws.
    // They are found again when what the world shows changed, and every two seconds besides: a picture that is still being read has no sides yet.
    private void UpdateEditorArrows()
    {
        if (!_editorArrowsOn || _feed.Replica == null)
        {
            _editorArrows.Clear();
            _editorArrowsKey = default;
            return;
        }
        var key = (_editor.Feed.Tick, _editor.Document.Revision, _editorLevel, _editorAbove, (int)(App.NowSeconds / 2));
        if (key == _editorArrowsKey)
            return;
        _editorArrowsKey = key;
        _editorArrowList.Clear();
        foreach (ReplicaEntity thing in _feed.Replica.Entities)
        {
            string type = thing.TypePath;
            if (type == null)
                continue;
            if (!_editorArrowTypes.TryGetValue(type, out bool wanted))
                _editorArrowTypes[type] = wanted = FacingMarks.Wanted(type);
            EntityState state = thing.State;
            if (!wanted || !EditorLevelShown(state.Z) || _editor.Feed.IsPreview(thing.Id))
                continue;
            if (FacingMarks.IsMarker(type) || _map.DrawnByModel(thing.Id) || _entities.DirectionsOf(state.Appearance) > 1)
                _editorArrowList.Add((state.X, state.Y, state.Z, state.Dir));
        }
        _editorArrows.ShowThings(_editorArrowList);
    }

    // Every frame: the frame around what is selected (5.20), which stays when the mouse goes elsewhere: a thing by the shapes a click is tested
    // against, as the world draws it; a wall by its block, a floor, an area or a whole tile by the tile's square (a wall's tile by its block), a
    // ceiling by its slab. The shapes are found again when the selection, the map or what the world shows changed.
    private void UpdateEditorSelectionMark(Camera3D camera)
    {
        // (the floor of the first hidden level is seen from under it, the ceiling of the top one shown: its frame is drawn there)
        if (_editorSelected is not { } selected || camera == null
            || !EditorLevelShown(selected.Z) && !(selected.Z == EditorTopShown + 1 && camera.GlobalPosition.Y < TileSpace.WorldY(selected.Z)))
        {
            _editorSelectionMark.HideBox();
            _editorMarkKey = default;
            return;
        }
        MapDocument map = _editor.Document;
        MapModel tile = map[selected.X, selected.Y, selected.Z];
        long thing = selected.Member >= 0 && MapEdits.IsThing(tile.Members[selected.Member].Path) && _editor.Feed.Shown ? _editor.Feed.ThingOf(selected.X, selected.Y, selected.Z, selected.Member) : 0;
        var key = (selected, map.Revision, _editor.Feed.Tick, thing);
        if (key != _editorMarkKey)
        {
            _editorMarkKey = key;
            _editorMarkShapes.Clear();
            if (thing != 0)
            {
                // (a thing hung on a wall is drawn, and kept, over the tile it hangs from or its own: both are looked at)
                _editorPreviewShapes.Clear();
                _map.CollectStatic(selected.X, selected.Y, selected.Z, _editorPreviewShapes);
                _entities.CollectPickShapes(_editorPreviewShapes, 0);
                foreach (PickShape shape in _editorPreviewShapes)
                {
                    if (shape.EntityId == thing)
                        _editorMarkShapes.Add(shape);
                }
            }
        }
        Vector3 size, centre;
        Basis turn = Basis.Identity;
        Basis view = camera.GlobalTransform.Basis;
        if (_editorMarkShapes.Count > 0)
        {
            // one frame around all of the thing's shapes (a door's two panels, a model's boxes)
            var hit = new PickHit(PickSurface.Piece, TargetKind.Entity, thing, selected.X, selected.Y, _editorMarkShapes[0].TileZ, 0, 0, 0, 0, 0, 0);
            HoverHighlight.BoxOf(hit, _editorMarkShapes[0], view, camera.GlobalPosition.Y, out size, out centre, out turn);
            if (_editorMarkShapes.Count > 1)
            {
                Vector3 low = centre - size / 2f, high = centre + size / 2f;
                for (int i = 1; i < _editorMarkShapes.Count; i++)
                {
                    HoverHighlight.BoxOf(hit, _editorMarkShapes[i], view, camera.GlobalPosition.Y, out Vector3 s, out Vector3 c, out _);
                    (low, high) = (low.Min(c - s / 2f), high.Max(c + s / 2f));
                }
                (size, centre, turn) = (high - low, (low + high) / 2f, Basis.Identity);
            }
        }
        else
        {
            bool wall = MapEdits.LayerOf(MapEdits.TurfOf(tile)) == TurfLayer.Wall;
            PickSurface surface = selected.Member == EditorCeilingMember ? PickSurface.Ceiling : wall ? PickSurface.Wall : PickSurface.Floor;
            HoverHighlight.BoxOf(new PickHit(surface, TargetKind.Turf, 0, selected.X, selected.Y, selected.Z, 0, 0, 0, 0, 0, 0), null, view, camera.GlobalPosition.Y, out size, out centre, out turn);
        }
        _editorSelectionMark.ShowBox(centre, size, turn, camera);
    }

    // ---- the levels (5.8) ----

    // The top level the view shows while the levels above the edited one are hidden: the edited level, or a level above it that a tool has built
    // on since the level or Above last changed (EditorSeeBuilt).
    private int EditorTopShown => Math.Max(_editorLevel, _editorSeenTo);

    private int _editorSeenTo;

    // The level the view shows nothing above (0: every level of the stack is shown).
    private int EditorHideAbove => _editorAbove || _editor.Document.Levels <= 1 ? 0 : EditorTopShown;

    // Whether what stands on a level is shown: a level of the edited one's stack, and not one above the top one shown unless those are shown.
    private bool EditorLevelShown(int z) => (EditorHideAbove == 0 || z <= EditorTopShown) && (_feed?.Replica?.SameStack(z, _editorLevel) ?? true);

    // Above, by the button, the key or the console: the levels over the edited one are shown, or hidden again, those a tool built on too.
    private void EditorSetAbove(bool on)
    {
        if (on != _editorAbove)
            (_editorAbove, _editorSeenTo) = (on, 0);
    }

    // Another level is the edited one: the view shows it and what is under it, and the camera stays where it is (the owner, 2026-11-17: it went up or
    // down with the level, and what he was looking at left the view). In the top view it does go with the level, and that is what keeps the picture
    // as it was: the top view's scale is the camera's height over the edited level (UpdateEditorCamera).
    private void EditorSetLevel(int level)
    {
        level = Math.Clamp(level, 1, _editor.Document.Levels);
        if (level == _editorLevel)
            return;
        if (_editorTop)
            _rig.PlaceFree(_rig.FreePosition + Vector3.Up * ((level - _editorLevel) * TileSpace.LevelHeight));
        (_editorLevel, _editorSeenTo) = (level, 0);
        EditorNotice(Inv($"Level {level} of {_editor.Document.Levels}"), 2);
    }

    // The levels a click, a stroke or a shape on a level changes: that one and as many over it as the height says, within the map.
    private IEnumerable<int> EditorLevels(int z)
    {
        for (int level = z; level < z + _editorHeight && level <= _editor.Document.Levels; level++)
            yield return level;
    }

    // The level a drag or a held stroke began on: it goes on over that level wherever the mouse is.
    private int? EditorDragZ => _editorDragFrom?.Z ?? _editorBandFrom?.Z ?? _editorMoveFrom?.Z ?? (_editorStroke ? _editorStrokeZ : null);

    // The tile of a level under a ray, where the ray meets that level's floor.
    private static (int X, int Y, int Z)? EditorPlaneCell(Vector3 origin, Vector3 normal, int z)
    {
        float along = Mathf.Abs(normal.Y) < 1e-4f ? -1f : (TileSpace.WorldY(z) - origin.Y) / normal.Y;
        if (along <= 0f)
            return null;
        Vector3 point = origin + normal * along;
        return (Mathf.FloorToInt(point.X / TileSpace.TileSize) + 1, Mathf.FloorToInt(-point.Z / TileSpace.TileSize) + 1, z);
    }

    // The tile of a level under a ray, where the ray meets a height of the world: a hair further along it, so that a point on a wall's face is in
    // the wall's tile.
    private static (int X, int Y, int Z)? EditorPlaneCell(Vector3 origin, Vector3 normal, float y, int z)
    {
        float along = Mathf.Abs(normal.Y) < 1e-4f ? -1f : (y - origin.Y) / normal.Y;
        if (along <= 0f)
            return null;
        Vector3 point = origin + normal * (along + 0.01f);
        return (Mathf.FloorToInt(point.X / TileSpace.TileSize) + 1, Mathf.FloorToInt(-point.Z / TileSpace.TileSize) + 1, z);
    }

    // ---- the layers and the ceilings (5.17, 5.18) ----

    // A drag or a held stroke of the place or the erase tool is of ceilings (true) or not (false); null with none under way.
    private bool? EditorDragCeiling => _editorStroke || _editorDragFrom != null ? _editorStrokeCeiling : null;

    // What is over a tile now, as the map's ceiling var names it: a floor type's path, lattice, catwalk; null when nothing is. The map's own word where
    // it has one, else what the world shows there (the game's rule).
    private string EditorCeilingAt(int x, int y, int z)
    {
        if (MapEdits.CeilingOf(_editor.Document[x, y, z]) is { } set)
            return set == "none" ? null : set;
        Anomaly.Core.Motion.CeilingState shown = _feed.Replica.CeilingAt(x, y, z);
        return !shown.Exists ? null : shown.Lattice == Anomaly.Core.Motion.CeilingLattice.Catwalk ? "catwalk" : shown.Lattice != Anomaly.Core.Motion.CeilingLattice.None ? "lattice" : shown.Turf;
    }

    // A ceiling's name for a line of text.
    private string EditorCeilingName(string ceiling)
        => ceiling is null or "lattice" or "catwalk" ? ceiling ?? "nothing" : _catalog?.Find(ceiling)?.Name is { } name ? name + " (" + ceiling.Substring(ceiling.LastIndexOf('/') + 1) + ")" : ceiling;

    // What the erase tool takes for a press: while the palette shows the areas a tile's area and nothing else (the owner, 2026-11-17: erasing
    // there took the walls and the things the mouse was on); else with Shift the whole tile, else what is under the mouse.
    private EditorErase EditorEraseFor(bool shift) => _spawnTab == SpawnTab.Areas ? EditorErase.Area : shift ? EditorErase.All : EditorEraseUnderMouse();

    // What the erase tool takes when the button goes down on what is under the mouse.
    private EditorErase EditorEraseUnderMouse()
    {
        if (_editorHit.Kind == TargetKind.Entity)
            return EditorErase.Thing;
        if (_editorHit.Kind == TargetKind.Ceiling)
            return EditorErase.Ceiling;
        // (from under a level: the tile laid under its plating; with none, what is seen is the plating the rest stands on, and it goes whole)
        if (_editorSlab)
            return MapEdits.LaidCeiling(_editor.Document[_editorRoom.X, _editorRoom.Y, _editorRoom.Z]) != null ? EditorErase.Laid : EditorErase.Slab;
        return MapEdits.LayerOf(MapEdits.TurfOf(_editor.Document[_editorHit.X, _editorHit.Y, _editorHit.Z])) switch { TurfLayer.Wall => EditorErase.Wall, TurfLayer.Tile => EditorErase.Tile, _ => EditorErase.Base };
    }

    // A tile as the erase tool leaves it: the kind that was pressed is taken away, where the tile has it.
    private MapModel EditorErased(MapDocument map, int x, int y, int z)
    {
        MapModel tile = map[x, y, z];
        switch (_editorErase)
        {
            case EditorErase.All: return map.Empty(z);
            case EditorErase.Area: return MapEdits.Unareaed(map, tile);
            case EditorErase.Thing: return MapEdits.ClearThings(map, tile);
            case EditorErase.Wall: return MapEdits.Peel(map, tile, TurfLayer.Wall);
            case EditorErase.Tile: return MapEdits.Peel(map, tile, TurfLayer.Tile);
            case EditorErase.Base: return MapEdits.Peel(map, tile, TurfLayer.Base, map.Empty(z));
            case EditorErase.Laid or EditorErase.Slab: return tile; // (two tiles change, the ceiling's and the one under it: EditorCeilingTiles)
            default:
                return MapEdits.CeilingPeeled(EditorCeilingAt(x, y, z)) is { } left ? MapEdits.WithCeiling(map, tile, left) : tile;
        }
    }

    // What the erase tool takes, named for a line of text and for the undo history.
    private string EditorEraseName(bool many) => _editorErase switch
    {
        EditorErase.All => many ? "Clear tiles" : "Clear the tile",
        EditorErase.Area => many ? "Take the areas away" : "Take the area away",
        EditorErase.Thing => many ? "Take the things away" : "Erase",
        EditorErase.Wall => many ? "Take the walls away" : "Take the wall away",
        EditorErase.Tile => many ? "Take the floor tiles off" : "Take the floor tile off",
        EditorErase.Base => many ? "Take the floors away" : "Take the floor away",
        EditorErase.Laid => many ? "Take the ceilings' tiles off" : "Take the ceiling's tile off",
        EditorErase.Slab => many ? "Take the floors away from under them" : "Take the floor away from under it",
        _ => many ? "Take the ceilings' top layer off" : "Take the ceiling's top layer off",
    };

    // What the place tool puts on a tile: the brush as one more member, or (the replace mode) in the place of what was clicked.
    private MapModel EditorPlaced(MapDocument map, int x, int y, int z, MapMember brush) => EditorPlacedBy(map, x, y, z, brush, _editorReplaceTarget, tell: true);

    // A pipe or an atmos machine that would share a layer and a way with one already on the tile is not placed, as tg's wrench refuses to fasten
    // it (construction.dm): the tile stays as it was, and a place that is not the preview says why.
    private MapModel EditorPlacedBy(MapDocument map, int x, int y, int z, MapMember brush, string target, bool tell = false)
    {
        MapModel tile = map[x, y, z];
        MapModel next = _editorPlaceMode == EditorPlaceMode.Replace ? MapEdits.ReplaceOn(map, tile, target, brush) : MapEdits.Place(map, tile, brush);
        if (ReferenceEquals(next, tile) || !brush.Path.StartsWith("/obj/machinery/atmospherics", StringComparison.Ordinal)
            || MapChecks.PipeConflicts(next).Count() <= MapChecks.PipeConflicts(tile).Count())
            return next;
        if (tell)
            EditorNotice(_editorRefused = "Not placed: " + MapChecks.PipeConflictText, 8);
        return tile;
    }

    private string _editorRefused; // why the place tool last left a tile as it was (a shape that changed nothing says it)

    // The place tool is making or changing a ceiling from under it, on a level whose ceiling is the floor of the level above (5.18): a stroke or a
    // shape that began so, else where the mouse is with what makes a ceiling. Two tiles change there, the ceiling's and the one under it
    // (EditorCeilingTiles).
    private bool EditorOnSlabs => _editorStroke || _editorDragFrom != null ? _editorStrokeSlab
        : _editorFromUnder && _editorHit.Kind == TargetKind.Turf && _editorCell == (_editorHit.X, _editorHit.Y, _editorHit.Z)
            && EditorBrush() is { } brush && MapEdits.CeilingFrom(brush.Path) != null;

    // What a place (brush) or an erase (no brush) of ceilings from under a level changes for one of its tiles: the tile over the ceiling, (x, y, z)
    // on the level above, and the one under it, whose turf says which tile lies under the plating. The place tool by MapEdits.Ceiled, in the replace
    // mode on the ceilings that are like the one clicked; the erase tool takes the laid tile off, or the floor whole with what lay under it.
    private IEnumerable<(int X, int Y, int Z, MapModel Model)> EditorCeilingTiles(MapDocument map, int x, int y, int z, MapMember? brush)
    {
        if (z < 2 || !map.Contains(x, y, z))
            yield break;
        MapModel slab = map[x, y, z], room = map[x, y, z - 1];
        (MapModel Room, MapModel Slab) next = (room, slab);
        if (brush is { } placed)
        {
            if (_editorPlaceMode != EditorPlaceMode.Replace || MapEdits.UndersideOf(room, slab) == _editorReplaceUnder)
                next = MapEdits.Ceiled(map, room, slab, placed);
        }
        else if (_editorErase == EditorErase.Laid)
            next = (MapEdits.LaidCeiling(room) != null ? MapEdits.WithCeiling(map, room, null) : room, slab);
        else
            next = (MapEdits.WithCeiling(map, room, null), MapEdits.Unturfed(map, slab, map.Empty(z)));
        if (!ReferenceEquals(next.Room, room))
            yield return (x, y, z - 1, next.Room);
        if (!ReferenceEquals(next.Slab, slab))
            yield return (x, y, z, next.Slab);
    }

    // The place tool would change the ceiling the mouse is on (the map's own, 5.12) and not place under it: its brush makes a ceiling, or the mode is
    // to replace what is clicked.
    private bool EditorOnCeiling(MapMember brush)
        => _editorHit.Kind == TargetKind.Ceiling && (_editorPlaceMode == EditorPlaceMode.Replace || MapEdits.CeilingFrom(brush.Path) != null);

    private bool EditorVoid(int x, int y, int z) => TurfStyle.Classify(MapEdits.TurfOf(_editor.Document[x, y, z])) == TurfKind.Void;

    // A room goes on up over (x, y, z): the tile above is open, and on that level something stands on each side of it before the map's edge (the
    // room's walls, a window, a gallery's floor). Under open space or beside another building only, it does not.
    private bool EditorRoomGoesUp(int x, int y, int z)
    {
        MapDocument map = _editor.Document;
        if (!_map.LevelAbove(z) || !map.Contains(x, y, z + 1) || !EditorVoid(x, y, z + 1))
            return false;
        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int cx = x + dx, cy = y + dy;
            while (map.Contains(cx, cy, z + 1) && EditorVoid(cx, cy, z + 1))
                (cx, cy) = (cx + dx, cy + dy);
            if (!map.Contains(cx, cy, z + 1))
                return false;
        }
        return true;
    }

    // ---- the preview (5.9) ----

    private const int EditorPreviewTiles = 48; // the most tiles a preview shows: a larger shape is marked tile by tile and not made
    private const double EditorPreviewRest = 0.07; // seconds the mouse rests on a tile before the world is asked for its preview

    // Every frame: what the place tool is about to place, or the copy a click would paste, is shown where it would go, made by the world behind the
    // view as the placed thing will be made (its model, its joints with what is beside it, the wall it hangs on), in the highlight's blue
    // (UpdateEditorTint). The document is not changed. The world is asked once the mouse has rested on a tile, so a mouse that crosses the map asks
    // for nothing.
    private void UpdateEditorPreview()
    {
        MapDocument map = _editor.Document;
        List<(int X, int Y, int Z, MapModel Model)> tiles = null;
        string wanted = string.Empty;
        if (_editorPreviewOn && _editorOnMap && _editorTool == EditorTool.Place && !_editorPasting && !_editorStroke && !map.InStep && EditorBrush() is { } brush && !MapEdits.IsArea(brush.Path)
            && !EditorOnCeiling(brush) && !EditorOnSlabs && EditorDragCeiling != true) // (a ceiling is marked where it goes and not made ahead)
        {
            (int x, int y, int z) = _editorCell;
            IReadOnlyList<(int X, int Y)> cells;
            List<MapMember> pieces = null;
            (int X, int Y, int Z) from = _editorDragFrom ?? _editorCell;
            if (_editorDragFrom != null)
            {
                bool run = EditorDrawsRun();
                cells = MapShapes.Cells(_editorDraw, from.X, from.Y, _editorDragTo.X, _editorDragTo.Y, run);
                if (run && _editorDraw == MapShape.Line)
                    pieces = MapEdits.RunPieces(brush, cells);
            }
            else if (_editorDraw == MapShape.Zone)
                cells = (IReadOnlyList<(int, int)>)MapShapes.Zone(map, x, y, z) ?? Array.Empty<(int, int)>();
            else
                cells = new[] { (x, y) };
            // (the replace mode: what is under the mouse gives its place, or what the button went down on)
            string target = _editorPlaceMode != EditorPlaceMode.Replace ? null : _editorDragFrom != null ? _editorReplaceTarget
                : EditorMemberUnderMouse() is { Member: >= 0 } under ? map[under.X, under.Y, under.Z].Members[under.Member].Path : MapEdits.TurfOf(map[x, y, z]);
            int levels = EditorLevels(from.Z).Count();
            if (cells.Count > 0 && cells.Count * levels <= EditorPreviewTiles)
            {
                tiles = new List<(int, int, int, MapModel)>();
                try
                {
                    foreach (int level in EditorLevels(from.Z))
                    {
                        for (int i = 0; i < cells.Count; i++)
                        {
                            (int cx, int cy) = cells[i];
                            if (!map.Contains(cx, cy, level))
                                continue;
                            MapMember placed = pieces != null ? pieces[i] : EditorHung(map, brush, cx, cy, level, _editorDragFrom == null ? _editorMountSide : _editorShapeMount);
                            MapModel tile = map[cx, cy, level];
                            // (a tile the click would not change shows no preview: the thing is there already)
                            MapModel shown = EditorPlacedBy(map, cx, cy, level, placed, target);
                            if (!ReferenceEquals(shown, tile))
                                tiles.Add((cx, cy, level, shown));
                        }
                    }
                }
                catch (MapTextException)
                {
                    tiles = null; // (a brush the format refuses: the click says so)
                }
                if (tiles != null)
                    wanted = string.Join("|", tiles.Select(t => Inv($"{t.X},{t.Y},{t.Z}:{t.Model.Text}")));
            }
        }
        else if (_editorPreviewOn && _editorOnMap && _editorPasting && !map.InStep && _editorClip is { } clip && (clip.Thing != null || clip.Width * clip.Height <= EditorPreviewTiles))
        {
            // the copy that follows the mouse, as a click would paste it (a larger one is marked tile by tile and not made)
            (int px, int py) = EditorPasteCorner(clip);
            try
            {
                tiles = MapEdits.Paste(map, clip, px, py, _editorCell.Z);
                wanted = Inv($"paste {px},{py},{_editorCell.Z} {map.Revision} {System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(clip)}");
            }
            catch (MapTextException)
            {
                tiles = null; // (a copy the format refuses: the click says so)
            }
        }
        // The shuttles that are shown at their docks (5.11) are made by the world in the same way, and stand under whatever the tool previews: a
        // tile of both is the tool's.
        if (wanted.Length == 0)
            tiles = null;
        (string shuttles, List<(int X, int Y, int Z, MapModel Model)> docked) = EditorShuttleTiles();
        if (shuttles != null)
            wanted = shuttles + "\n" + wanted;
        double now = App.NowSeconds;
        if (wanted != _editorPreviewWanted)
            (_editorPreviewWanted, _editorPreviewSince) = (wanted, now);
        // (nothing to preview is said at once; another preview once it has stood a moment)
        if (_editorPreviewWanted == _editorPreviewGiven || (wanted.Length > 0 && now - _editorPreviewSince < EditorPreviewRest))
            return;
        _editorPreviewGiven = _editorPreviewWanted;
        _editorPreviewTool.Clear();
        foreach ((int x, int y, int z, MapModel _) in tiles ?? new List<(int, int, int, MapModel)>())
            _editorPreviewTool.Add((x, y, z));
        _editor.Feed.SetPreview(docked == null ? tiles : tiles == null ? docked : docked.Concat(tiles).ToList());
    }

    // Every frame: the previewed things and turfs are in the highlight's blue. Each gives its box to the world's materials (PreviewTint), which
    // tint what they draw inside it: a thing by the shapes a click is tested against, a turf by its tile's block or floor.
    private void UpdateEditorTint()
    {
        _editorTintBoxes.Clear();
        _editorPreviewShapes.Clear();
        bool any = false;
        foreach ((int x, int y, int z, string turf) in _editor.Feed.PreviewCells)
        {
            if (EditorShuttleOnly(x, y, z))
                continue; // (a shuttle shown at its dock is drawn as it is)
            any = true;
            if (turf != null && _editorTintBoxes.Count < PreviewBoxes.Most)
                _editorTintBoxes.Add(PreviewBoxes.OfTurf(x, y, z, turf.StartsWith("/turf/closed", StringComparison.Ordinal)));
            _map.CollectStatic(x, y, z, _editorPreviewShapes);
        }
        if (any)
        {
            _entities.CollectPickShapes(_editorPreviewShapes, 0);
            PreviewBoxes.AddThings(_editorPreviewShapes, id => _editor.Feed.IsPreview(id) && (_editorShuttleCells.Count == 0
                || !_editor.Feed.Replica.TryGet(id, out ReplicaEntity thing) || !EditorShuttleOnly(thing.State.X, thing.State.Y, thing.State.Z)), _editorTintBoxes);
        }
        _editorTint.Show(_editorTintBoxes);
    }

    // The shape of the previewed thing on the tile under the mouse, once the world shows it: the highlight is drawn around it, as around a thing the
    // mouse is over. Null while there is none (a turf's preview is its tile's own block or floor).
    private PickShape EditorPreviewShape()
    {
        (int x, int y, int z) = _editorCell;
        if (!_editor.Feed.Previewed(x, y, z) || EditorShuttleOnly(x, y, z))
            return null;
        _editorPreviewShapes.Clear();
        _map.CollectStatic(x, y, z, _editorPreviewShapes);
        _entities.CollectPickShapes(_editorPreviewShapes, 0);
        foreach (PickShape shape in _editorPreviewShapes)
        {
            if (_editor.Feed.IsPreview(shape.EntityId))
                return shape;
        }
        return null;
    }

    // ---- the camera (5.6) ----

    // The floor of the level that is edited, in the world.
    private float EditorFloorY => TileSpace.WorldY(_editorLevel);

    // The camera is under the tops of the walls of the top level shown (the edited one while the levels above are hidden), flown into the map: the
    // view draws the rooms as a player sees them, with their ceilings and the undersides of the levels above, and those are there for the tools (5.18).
    private bool EditorUnder => _rig.GlobalPosition.Y < TileSpace.WorldY(EditorHideAbove == 0 ? _editor.Document.Levels : EditorTopShown) + WorldScale.WallHeight - 0.02f;

    // Every frame: the top view's scale follows the camera's height, as far as the same height shows in perspective (so the wheel, the keys and what
    // is drawn by its distance work alike in both), and its keys move as fast as the height makes the map small.
    private void UpdateEditorCamera()
    {
        Camera3D camera = _rig.Camera;
        if (camera == null)
            return;
        float height = _rig.FreePosition.Y - EditorFloorY;
        if (_editorTop)
        {
            if (height < EditorTopLowest)
                _rig.PlaceFree(_rig.FreePosition + Vector3.Up * (EditorTopLowest - height)); // (the keys do not take it under the walls' tops)
            height = Mathf.Max(height, EditorTopLowest);
            camera.Size = 2f * height * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2f);
        }
        _rig.FlySpeedScale = _editorFlySpeed * (_editorTop ? Mathf.Max(1f, height / 10f) : 1f);
        // From above the walls the map's light is tg's own, tile by tile, over the whole map (the light grid): which rooms are lit and which are dark, with no lamp
        // shining through a wall. Under them, flown into a room, it is the game's: the lamps as lights with their shadows, which the game draws around a
        // player and no further.
        // A room two or more levels high is flown into too while a roof is over the camera on a level above it.
        if (_light != null)
            _light.Dynamic = !(EditorLit && height > WorldScale.WallHeight && (_editorTop || !EditorRoofed(camera.GlobalPosition)));
    }

    // A floor or a ceiling over the camera's tile, on a level above the one the camera is in (open space and space are none).
    private bool EditorRoofed(Vector3 eye)
    {
        ReplicaWorld replica = _feed?.Replica;
        if (replica == null)
            return false;
        int x = Mathf.FloorToInt(eye.X / TileSpace.TileSize) + 1, y = Mathf.FloorToInt(-eye.Z / TileSpace.TileSize) + 1;
        int level = Math.Max(1, 1 + Mathf.FloorToInt(eye.Y / TileSpace.LevelHeight));
        for (int z = level; z <= _editor.Document.Levels; z++)
        {
            if (replica.CeilingAt(x, y, z).Sealed)
                return true;
            if (z > level && replica.TurfTypeAt(x, y, z) is string type && !type.StartsWith("/turf/open/space", StringComparison.Ordinal)
                && !type.StartsWith("/turf/open/openspace", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // The point of the edited level's floor under a point of the screen, if the view meets the floor there.
    private Vector3? EditorFloorPoint(Vector2 screen)
    {
        Camera3D camera = _rig.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screen), normal = camera.ProjectRayNormal(screen);
        if (normal.Y > -1e-3f)
            return null;
        float along = (EditorFloorY - origin.Y) / normal.Y;
        return along is > 0f and < 2000f ? origin + normal * along : null;
    }

    // The middle of what is selected, on its floor, and how far from it the selection reaches: the selected rectangle, thing or tile; with nothing
    // selected, the whole map.
    private (Vector3 Middle, float Radius) EditorSelectionBounds(out bool selected)
    {
        MapDocument map = _editor.Document;
        selected = _editorRegion != null || _editorSelected != null;
        (int x0, int y0, int x1, int y1, int z) = _editorRegion is { } r ? (EditorRegions.Min(e => Math.Min(e.X0, e.X1)), EditorRegions.Min(e => Math.Min(e.Y0, e.Y1)), EditorRegions.Max(e => Math.Max(e.X0, e.X1)), EditorRegions.Max(e => Math.Max(e.Y0, e.Y1)), r.Z)
            : _editorSelected is { } s ? (s.X, s.Y, s.X, s.Y, s.Z) : (1, 1, map.Width, map.Height, _editorLevel);
        var middle = new Vector3((TileSpace.WorldX(x0) + TileSpace.WorldX(x1)) / 2f, TileSpace.WorldY(z), (TileSpace.WorldZ(y0) + TileSpace.WorldZ(y1)) / 2f);
        return (middle, 0.5f * TileSpace.TileSize * new Vector2(x1 - x0 + 1, y1 - y0 + 1).Length());
    }

    // The point an orbit turns around: the selection while it is in the view, else the floor in the middle of the view (a point ahead when the view
    // looks over the floor's edge).
    private Vector3 EditorOrbitPivot()
    {
        Camera3D camera = _rig.Camera;
        (Vector3 middle, _) = EditorSelectionBounds(out bool selected);
        if (selected && !camera.IsPositionBehind(middle) && _shell.OverView(camera.UnprojectPosition(middle)))
            return middle;
        return EditorFloorPoint(_shell.Centre.GetGlobalRect().GetCenter()) ?? _rig.FreePosition + _rig.Forward * 20f;
    }

    // A mouse button takes the camera or lets it go. The right one flies it; the middle one, or Alt with the left, turns it around a point, and
    // with Shift moves it across its view. In the top view each of them moves the map under the mouse (T leaves the top view, nothing else).
    private void EditorCameraHold(InputEventMouseButton button, EditorCamera way)
    {
        if (!button.Pressed)
        {
            if (_editorLooking && button.ButtonIndex == _editorCameraButton)
                _editorLooking = false;
            return;
        }
        if (_editorLooking || _editorStroke || EditorDragging)
            return; // one thing at a time
        if (_editorTop)
            way = EditorCamera.Pan;
        (_editorLooking, _editorCamera, _editorCameraButton) = (true, way, button.ButtonIndex);
        Camera3D camera = _rig.Camera;
        if (way == EditorCamera.Orbit)
            _editorPivot = EditorOrbitPivot();
        // the point of the floor under the mouse stays under it: a pixel is as many metres as that point's depth in the view makes it
        float depth = EditorFloorPoint(button.Position) is { } grabbed ? (grabbed - _rig.FreePosition).Dot(_rig.Forward) : 20f;
        _editorPanScale = 2f * Mathf.Max(1f, depth) * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2f) / GetViewport().GetVisibleRect().Size.Y;
    }

    private void EditorCameraMotion(InputEventMouseMotion motion)
    {
        switch (_editorCamera)
        {
            case EditorCamera.Fly:
                _rig.Look(motion.ScreenRelative);
                break;
            case EditorCamera.Orbit:
                _rig.OrbitFree(_editorPivot, motion.ScreenRelative);
                break;
            case EditorCamera.Pan:
                Basis view = _rig.LookBasis;
                _rig.PlaceFree(_rig.FreePosition + (view.Y * motion.Relative.Y - view.X * motion.Relative.X) * _editorPanScale);
                break;
        }
    }

    // The wheel, by notches (toward the map when positive). While the camera flies it changes the flight's speed; else the camera goes toward the
    // point of the floor under the mouse or away from it, a share of the way a notch, so that the point stays under the mouse (in the top view
    // it is the map's scale that changes).
    private void EditorWheel(float notches, Vector2 mouse)
    {
        if (_editorLooking && _editorCamera == EditorCamera.Fly)
        {
            _editorFlySpeed = Mathf.Clamp(_editorFlySpeed * Mathf.Pow(EditorSpeedStep, notches), EditorMinSpeed, EditorMaxSpeed);
            EditorNotice(Inv($"Flight speed x{_editorFlySpeed:0.##}"), 2);
            return;
        }
        Vector3 from = _rig.FreePosition;
        Vector3 to = EditorFloorPoint(mouse) ?? from + _rig.Camera.ProjectRayNormal(mouse) * 20f;
        Vector3 next = to + (from - to) * Mathf.Pow(EditorWheelShare, notches);
        if (notches > 0 ? from.DistanceTo(to) > EditorNearest : next.Y - EditorFloorY < EditorHighest)
            _rig.PlaceFree(next);
    }

    // The keys are the view's: no field is typed in, no menu, question or edited cell is open, and no command key is held.
    private bool EditorKeysFree()
    {
        if (Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Meta) || Input.IsKeyPressed(Key.Alt))
            return false;
        Viewport view = GetViewport();
        if (view.GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox)
            return false;
        foreach (Window window in view.GetEmbeddedSubwindows())
        {
            if (window.Visible)
                return false;
        }
        return true;
    }

    // Home: the camera goes over the map's first tile (1, 1: its south-west corner) on the edited level, looking the way it looks; a camera flown far off
    // an empty map finds the map again (the owner, 2026-11-17).
    private void EditorHome()
    {
        if (!_editorTop && _rig.Pitch > Mathf.DegToRad(-20f))
            _rig.Pitch = Mathf.DegToRad(-45f);
        EditorLookAt(new Vector3(TileSpace.WorldX(1), EditorFloorY, TileSpace.WorldZ(1)), 30f);
    }

    // F: the camera goes where what is selected fills the view, looking the way it looks (down at it, if it looked along the floor or up); with nothing
    // selected, where the whole map does.
    private void EditorFocus()
    {
        (Vector3 middle, float radius) = EditorSelectionBounds(out _);
        if (!_editorTop && _rig.Pitch > Mathf.DegToRad(-20f))
            _rig.Pitch = Mathf.DegToRad(-45f);
        EditorLookAt(middle, Mathf.Clamp(1.15f * radius / Mathf.Tan(Mathf.DegToRad(_rig.Camera.Fov) / 2f), 6f, EditorHighest));
    }

    // Puts the camera where a point is seen from a distance in the middle of the view, the way the view looks. The view's middle is the middle of the
    // free part of the window, between the docks, not the window's, where the camera looks: the camera is moved across its view by the difference.
    private void EditorLookAt(Vector3 point, float distance)
    {
        Vector2 window = GetViewport().GetVisibleRect().Size, off = _shell.Centre.GetGlobalRect().GetCenter() - window / 2f;
        float perPixel = 2f * distance * Mathf.Tan(Mathf.DegToRad(_rig.Camera.Fov) / 2f) / window.Y;
        Basis view = _rig.LookBasis;
        _rig.PlaceFree(point - _rig.Forward * distance - (view.X * off.X - view.Y * off.Y) * perPixel);
    }

    // T: the top view, straight down with north up and no perspective, over the point the view looked at and as far from it; or back to the view it
    // was entered from, over the same point.
    private void EditorSetTop(bool top)
    {
        if (top == _editorTop)
            return;
        Camera3D camera = _rig.Camera;
        Vector3 at = EditorFloorPoint(_shell.Centre.GetGlobalRect().GetCenter()) ?? new Vector3(_rig.FreePosition.X, EditorFloorY, _rig.FreePosition.Z);
        float distance = Mathf.Clamp(_rig.FreePosition.DistanceTo(at), EditorTopLowest, EditorHighest);
        if (top)
            _editorTopFrom = (_rig.Yaw, _rig.Pitch);
        (_rig.Yaw, _rig.Pitch) = top ? (0f, -Mathf.Pi / 2f) : (_editorTopFrom.Yaw, Mathf.Min(_editorTopFrom.Pitch, Mathf.DegToRad(-20f)));
        _editorTop = top;
        _rig.FlyLevel = top;
        camera.Projection = top ? Camera3D.ProjectionType.Orthogonal : Camera3D.ProjectionType.Perspective;
        if (Quality.Environment is { } environment)
        {
            // with no perspective the sky is drawn as streaks: the top view has a plain dark ground behind the map
            if (top)
                (_editorTopSky, environment.BackgroundColor) = (environment.BackgroundMode, new Color(0.03f, 0.035f, 0.05f));
            environment.BackgroundMode = top ? Godot.Environment.BGMode.Color : _editorTopSky;
        }
        EditorLookAt(at, distance);
        UpdateEditorCamera();
    }

    // The tiles marked in the view: the selected rectangle, what a dragged shape or a zone will change, where a moved selection or a copy goes; and
    // the areas' layer.
    private void UpdateEditorMarks()
    {
        MapDocument map = _editor.Document;
        var parts = new List<(string Key, Func<IEnumerable<(int X, int Y)>> Cells, int Z, Color Colour)>();
        foreach ((int X0, int Y0, int X1, int Y1, int Z) region in EditorRegions.ToList())
            parts.Add((Inv($"region {region}"), () => MapShapes.Box(region.X0, region.Y0, region.X1, region.Y1, filled: true), region.Z, MarkSelected));
        (int X, int Y, int Z) to = _editorDragTo;
        if (_editorBandFrom is { } band && (band.X, band.Y) != (to.X, to.Y))
            parts.Add((Inv($"band {band} {to}"), () => MapShapes.Box(band.X, band.Y, to.X, to.Y, filled: true), band.Z, MarkSelected));
        bool erasing = _editorTool == EditorTool.Erase;
        if (_editorDragFrom is { } from)
        {
            bool run = EditorDrawsRun();
            foreach (int level in EditorLevels(from.Z))
                parts.Add((Inv($"shape {_editorDraw} {from} {to} {run} {level}"), () => MapShapes.Cells(_editorDraw, from.X, from.Y, to.X, to.Y, run), level, erasing ? MarkErased : MarkPlaced));
        }
        else if (_editorDraw == MapShape.Zone && _editorOnMap && !_editorPasting && (erasing || _editorTool == EditorTool.Place && EditorBrush() != null))
        {
            (int x, int y, int z) = _editorCell;
            foreach (int level in EditorLevels(z))
                parts.Add((Inv($"zone {_editorCell} {map.Revision} {level}"), () => MapShapes.Zone(map, x, y, z) ?? new List<(int, int)>(), level, erasing ? MarkErased : MarkPlaced));
        }
        if (_editorMoveFrom is { } move && (to.X - move.X, to.Y - move.Y) != (0, 0))
        {
            if (_editorRegion != null)
            {
                (int dx, int dy) = EditorMoveBy(to.X - move.X, to.Y - move.Y);
                foreach ((int X0, int Y0, int X1, int Y1, int Z) moved in EditorRegions.ToList())
                    parts.Add((Inv($"move {moved} {dx} {dy}"), () => MapShapes.Box(moved.X0 + dx, moved.Y0 + dy, moved.X1 + dx, moved.Y1 + dy, filled: true), moved.Z, MarkTarget));
            }
            else if (_editorSelected is { } thing)
            {
                (int tx, int ty) = (thing.X + to.X - move.X, thing.Y + to.Y - move.Y);
                parts.Add((Inv($"move thing {tx} {ty}"), () => new[] { (tx, ty) }, thing.Z, MarkTarget));
            }
        }
        if (_editorPasting && _editorOnMap && _editorClip is { } clip)
        {
            (int px, int py) = EditorPasteCorner(clip);
            parts.Add((Inv($"paste {px} {py} {clip.Width} {clip.Height}"), () => MapShapes.Box(px, py, px + clip.Width - 1, py + clip.Height - 1, filled: true), _editorCell.Z, MarkTarget));
        }
        if (parts.Count == 0)
            _editorMarks.Clear();
        else
            _editorMarks.Show(string.Join("|", parts.Select(p => p.Key)), () => parts.SelectMany(p => p.Cells().Where(c => map.Contains(c.X, c.Y, p.Z)).Select(c => (c.X, c.Y, p.Z, p.Colour))));

        if (_editorAreas || _spawnTab == SpawnTab.Areas) // (the palette's areas show them by themselves: the menu's switch is for the other tabs)
            _editorAreaMarks.Show(Inv($"areas {map.Revision} {_editorLevel}"), () => EditorAreaCells(map, _editorLevel));
        else
            _editorAreaMarks.Clear();
        if (_searchMarked.Count > 0)
            _editorFoundMarks.Show(Inv($"found {_searchQuery} {map.Revision} {_searchMarked.Count}"), () => _searchMarked.Where(c => map.Contains(c.X, c.Y, c.Z)).Select(c => (c.X, c.Y, c.Z, MarkFound)));
        else
            _editorFoundMarks.Clear();
        // the tiles whose ceiling the map sets (5.12): the ones open to the sky in blue, the others in green
        _editorCeilingMarks.Show(Inv($"ceilings {map.Revision} {_editorLevel}"), () => EditorCeilingCells(map, _editorLevel));
        UpdateEditorBorder(map);
        UpdateEditorDocks();
    }

    private static IEnumerable<(int X, int Y, int Z, Color Colour)> EditorCeilingCells(MapDocument map, int z)
    {
        if (z < 1 || z > map.Levels)
            yield break;
        var ceilings = new Dictionary<MapModel, string>(ReferenceEqualityComparer.Instance);
        for (int y = 1; y <= map.Height; y++)
        {
            for (int x = 1; x <= map.Width; x++)
            {
                MapModel tile = map[x, y, z];
                if (!ceilings.TryGetValue(tile, out string ceiling))
                    ceilings[tile] = ceiling = MapEdits.CeilingOf(tile);
                if (ceiling != null)
                    yield return (x, y, z, ceiling == "none" ? MarkOpenSky : MarkCeiling);
            }
        }
    }

    // The map's edge: a box of lines around all its tiles, from the first level's floor to the top level's top, with a ring at every level's floor
    // and a post at each corner; the edited level's part (its floor's ring, its walls' tops' ring, its posts) is the bright one. An empty map is all
    // space and shows nothing else of where it is (the owner, 2026-11-17: a tool did nothing wherever the mouse was off the map, and nothing said
    // so; and the edge was one level high whatever the map's levels).
    private void UpdateEditorBorder(MapDocument map)
    {
        _editorBorder.Visible = _editorBorderOn;
        if (!_editorBorderOn || _editorBorderShown == (map.Width, map.Height, map.Levels, _editorLevel))
            return;
        _editorBorderShown = (map.Width, map.Height, map.Levels, _editorLevel);
        var mesh = (ImmediateMesh)_editorBorder.Mesh;
        mesh.ClearSurfaces();
        float half = TileSpace.TileSize / 2f;
        float west = TileSpace.WorldX(1) - half, east = TileSpace.WorldX(map.Width) + half, south = TileSpace.WorldZ(1) + half, north = TileSpace.WorldZ(map.Height) - half;
        Vector3[] corners = { new(west, 0, south), new(east, 0, south), new(east, 0, north), new(west, 0, north) };
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        void Line(Vector3 a, Vector3 b, bool bright)
        {
            foreach (Vector3 end in new[] { a, b })
            {
                mesh.SurfaceSetColor(bright ? BorderBright : BorderDim);
                mesh.SurfaceAddVertex(end);
            }
        }
        // (a level's top is the floor of the one over it: one ring for both)
        for (int z = 1; z <= map.Levels + 1; z++)
        {
            float y = TileSpace.WorldY(z) + 0.03f;
            bool edited = z == _editorLevel || z == _editorLevel + 1;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                Line(new Vector3(a.X, y, a.Z), new Vector3(b.X, y, b.Z), edited);
                if (z <= map.Levels)
                    Line(new Vector3(a.X, y, a.Z), new Vector3(a.X, TileSpace.WorldY(z + 1) + 0.03f, a.Z), z == _editorLevel);
            }
        }
        mesh.SurfaceEnd();
        _editorBorder.CustomAabb = new Aabb(new Vector3(-100000, -100000, -100000), new Vector3(200000, 200000, 200000));
    }

    // The tiles of a level that are in an area other than the map's default one, each in its area's colour.
    private static IEnumerable<(int X, int Y, int Z, Color Colour)> EditorAreaCells(MapDocument map, int z)
    {
        if (z < 1 || z > map.Levels)
            yield break;
        string none = map.Default.Members.Count > 0 ? map.Default.Members[^1].Path : null;
        var colours = new Dictionary<MapModel, Color?>(ReferenceEqualityComparer.Instance);
        for (int y = 1; y <= map.Height; y++)
        {
            for (int x = 1; x <= map.Width; x++)
            {
                MapModel tile = map[x, y, z];
                if (!colours.TryGetValue(tile, out Color? colour))
                {
                    string area = tile.Members.Count > 0 && MapEdits.IsArea(tile.Members[^1].Path) ? tile.Members[^1].Path : null;
                    colours[tile] = colour = area == null || area == none ? null : EditorAreaColour(area);
                }
                if (colour is { } c)
                    yield return (x, y, z, c);
            }
        }
    }

    // An area's colour, the same in every run: its hue from its path, and three strengths so that areas of close hues differ.
    private static Color EditorAreaColour(string area)
    {
        uint hash = 2166136261;
        foreach (char c in area)
            hash = (hash ^ c) * 16777619;
        return Color.FromHsv(hash % 360 / 360f, 0.55f + (hash >> 9) % 3 * 0.15f, 0.95f - (hash >> 13) % 3 * 0.15f, 0.36f);
    }

    // A run is drawn: the brush is a piece that joins by the way it is turned, and the shape is one with a way (every tile crossed, a line).
    private bool EditorDrawsRun()
        => _editorTool == EditorTool.Place && _editorDraw is MapShape.Free or MapShape.Line && EditorBrush() is { } brush && MapEdits.IsRunPiece(brush.Path);

    // How far the selected rectangle moves when it is dragged by (dx, dy): as far as it stays whole on the map.
    private (int Dx, int Dy) EditorMoveBy(int dx, int dy)
    {
        MapDocument map = _editor.Document;
        (int left, int right) = (EditorRegions.Min(r => Math.Min(r.X0, r.X1)), EditorRegions.Max(r => Math.Max(r.X0, r.X1)));
        (int bottom, int top) = (EditorRegions.Min(r => Math.Min(r.Y0, r.Y1)), EditorRegions.Max(r => Math.Max(r.Y0, r.Y1)));
        return (Math.Clamp(dx, 1 - left, map.Width - right), Math.Clamp(dy, 1 - bottom, map.Height - top));
    }

    // ---- several selected rectangles (5.14) ----

    // Every selected rectangle: the ones kept with Shift, then the last.
    private IEnumerable<(int X0, int Y0, int X1, int Y1, int Z)> EditorRegions
        => _editorRegionLast is { } last ? _editorMoreRegions.Append(last) : Enumerable.Empty<(int, int, int, int, int)>();

    // The tiles of the selected rectangles, each once.
    private List<(int X, int Y)> EditorRegionCells() => EditorRegions.SelectMany(r => MapShapes.Box(r.X0, r.Y0, r.X1, r.Y1, filled: true)).Distinct().ToList();

    private bool EditorInRegions(int x, int y, int z)
        => EditorRegions.Any(r => r.Z == z && x >= Math.Min(r.X0, r.X1) && x <= Math.Max(r.X0, r.X1) && y >= Math.Min(r.Y0, r.Y1) && y <= Math.Max(r.Y0, r.Y1));

    // One more selected rectangle (Shift): the others stay when they are of its level.
    private void EditorAddRegion((int X0, int Y0, int X1, int Y1, int Z) region)
    {
        var kept = _editorRegionLast is { } last && last.Z == region.Z ? _editorMoreRegions.Append(last).ToList() : new List<(int, int, int, int, int)>();
        (_editorSelected, _editorRegion) = (null, region);
        _editorMoreRegions.AddRange(kept);
    }

    // Every selected rectangle becomes another (a move, a turn): the selection is the same rectangles where they are now.
    private void EditorMapRegions(Func<(int X0, int Y0, int X1, int Y1, int Z), (int X0, int Y0, int X1, int Y1, int Z)> to)
    {
        for (int i = 0; i < _editorMoreRegions.Count; i++)
            _editorMoreRegions[i] = to(_editorMoreRegions[i]);
        if (_editorRegionLast is { } last)
            _editorRegionLast = to(last);
    }

    // What is selected turns a quarter (5.14): the selected thing where it stands, or the selected tiles (a selected tile: what stands on it)
    // about the middle of the box that holds them, each thing turned with its tile.
    private void EditorRotate(bool clockwise)
    {
        MapDocument map = _editor.Document;
        if (map.InStep)
            return;
        // the way a thing is seen to face: the game turns some by its own rules (a closet away from the wall behind it), whatever the map says
        int Facing(int x, int y, int z, int member)
            => _editor.Feed.Shown && _editor.Feed.ThingOf(x, y, z, member) is var id and not 0 && _editor.Feed.Replica.TryGet(id, out ReplicaEntity thing) ? thing.State.Dir : 0;
        try
        {
            if (_editorRegion == null && _editorSelected is { Member: EditorCeilingMember })
            {
                EditorNotice("A ceiling has no way to face.", 3);
                return;
            }
            if (_editorRegion == null && _editorSelected is { Member: >= 0 } selected)
            {
                MapModel tile = map[selected.X, selected.Y, selected.Z];
                MapMember member = tile.Members[selected.Member];
                MapMember turned = MapTurns.Turned(member, clockwise, Facing(selected.X, selected.Y, selected.Z, selected.Member));
                if (MapDocument.TextOf(new[] { turned }) == MapDocument.TextOf(new[] { member }))
                {
                    EditorNotice("It has no way to face.", 3);
                    return;
                }
                List<MapMember> members = tile.Members.ToList();
                members[selected.Member] = turned;
                map.Edit("Turn " + (_catalog?.Find(member.Path)?.Name ?? member.Path), new[] { (selected.X, selected.Y, selected.Z, map.Model(members)) });
                return;
            }
            (List<(int X, int Y)> cells, int z) = _editorRegion is { } region ? (EditorRegionCells(), region.Z)
                : _editorSelected is { } one ? (new List<(int X, int Y)> { (one.X, one.Y) }, one.Z) : (null, 0);
            if (cells == null)
            {
                EditorNotice("Nothing is selected.", 3);
                return;
            }
            cells = cells.Where(c => map.Contains(c.X, c.Y, z)).ToList();
            List<(int X, int Y, int Z, MapModel Model)> tiles = MapTurns.Turn(map, cells, z, clockwise, (x, y, member) => Facing(x, y, z, member));
            if (tiles == null)
            {
                EditorNotice("Not turned: the turned selection would reach past the map's edge.", 4);
                return;
            }
            if (!map.Edit(Inv($"Turn {cells.Count} tiles"), tiles))
                return;
            TurnBox box = TurnBox.Of(cells, clockwise);
            EditorMapRegions(r =>
            {
                ((int ax, int ay), (int bx, int by)) = (box.Spot(r.X0, r.Y0), box.Spot(r.X1, r.Y1));
                return (ax, ay, bx, by, r.Z);
            });
        }
        catch (MapTextException e)
        {
            EditorNotice("Not turned: " + e.Message);
        }
    }

    // Where a copy's south-west tile goes: its middle is under the mouse.
    private (int X, int Y) EditorPasteCorner(MapClip clip) => (_editorCell.X - (clip.Width - 1) / 2, _editorCell.Y - (clip.Height - 1) / 2);

    // Whether a point of the world is over the map's rectangle.
    private static bool EditorOverMap(Vector3 point, MapDocument map)
        => point.X >= 0f && point.Z <= 0f && point.X <= map.Width * TileSpace.TileSize && -point.Z <= map.Height * TileSpace.TileSize;

    // How far along a ray from beside the map it comes over the map's rectangle; false when it never does.
    private static bool EditorEntersMap(Vector3 origin, Vector3 normal, MapDocument map, out float along)
    {
        float from = 0f, to = (float)EditorPickMetres;
        bool Within(float at, float step, float size)
        {
            if (Mathf.Abs(step) < 1e-9f)
                return at >= 0f && at <= size;
            float a = -at / step, b = (size - at) / step;
            (from, to) = (Mathf.Max(from, Mathf.Min(a, b)), Mathf.Min(to, Mathf.Max(a, b)));
            return from <= to;
        }
        bool enters = Within(origin.X, normal.X, map.Width * TileSpace.TileSize) && Within(-origin.Z, -normal.Z, map.Height * TileSpace.TileSize);
        along = from;
        return enters;
    }

    // The turf of a level where a ray meets a height of the world, as a hit: its floor, or its wall.
    private PickHit EditorHitOnPlane(Vector3 origin, Vector3 normal, float y, int z)
    {
        if (EditorPlaneCell(origin, normal, y, z) is not { } on || !_editor.Document.Contains(on.X, on.Y, on.Z))
            return PickHit.None;
        float along = (y - origin.Y) / normal.Y;
        Vector3 point = origin + normal * along;
        bool wall = TurfStyle.Classify(MapEdits.TurfOf(_editor.Document[on.X, on.Y, on.Z])) == TurfKind.Closed;
        return new PickHit(wall ? PickSurface.Wall : PickSurface.Floor, TargetKind.Turf, 0, on.X, on.Y, on.Z, along, point.X, -point.Z, y - TileSpace.WorldY(z), 1, 1);
    }

    private void EditorPick(in PickRay ray)
    {
        _editorHit = _picker.Pick(ray, 0, entitiesOnly: false);
        _editorShape = _picker.LastShape;
    }

    // What is under a point of the screen: the hit, and the tile the armed tool acts on (the tile hit; for a thing placed against a wall's side, the tile
    // in front of that face; from under a ceiling the ceiling's own tile, or the one under it for what makes no ceiling, 5.18).
    private bool EditorPickAt(Vector2 screen)
    {
        Camera3D camera = _rig.Camera;
        if (camera == null || _feed.Map == null)
            return false;
        MapDocument map = _editor.Document;
        Vector3 origin = camera.ProjectRayOrigin(screen), normal = camera.ProjectRayNormal(screen);
        (_editorSlab, _editorFromUnder) = (false, false);
        // A camera beside the map (5.17): the ray is taken from where it comes over the map's rectangle, so the pick begins on the level the ray is in
        // there. Taken from the camera it began on the camera's level and ended where it crossed that level's floor, beside the map: from far above or
        // below and to a side of the map no tile was under the mouse (the owner, 2026-11-17).
        bool beside = !EditorOverMap(origin, map);
        if (beside)
        {
            if (!EditorEntersMap(origin, normal, map, out float enters))
                return false;
            origin += normal * Mathf.Max(0f, enters - 0.05f);
        }
        int z = _editorLevel;
        _pickWorld.PickExclude = 0;
        // The ceilings (5.18): from above the walls a click goes through a room's ceiling, which is not drawn; from under them the ceiling is drawn and is
        // what the mouse is on. Where there is none, the place tool with what makes a ceiling (a floor, a lattice, a catwalk) is on the place it would be,
        // from inside the map and over a tile that has a floor: over open space the ray goes on up to what is seen there.
        _pickWorld.IgnoreCeilings = !EditorUnder || EditorDragCeiling == false; // (nor does a stroke that began on the floor take to the ceiling)
        _picker.PickEmptyCeilings = !beside && EditorUnder && origin.Y >= TileSpace.WorldY(1) && !_editorPasting
            && (EditorDragCeiling ?? (_editorTool == EditorTool.Place && EditorBrush() is { } roof && MapEdits.CeilingFrom(roof.Path) != null));
        // The levels (5.8, 5.17): a ray goes no higher than the top level shown. Locked, it goes no lower than the edited one, whose open tile is the hit and
        // not what is seen through it. Else it goes on down through the open tiles to what is seen there, a thing, a wall or a floor of a lower level; and
        // where it finds nothing but space, the edited level's open tile is the hit after all (a click on empty space builds on the edited level, not on
        // the lowest one). A ray that comes up to the edited level from under it does the same from its side: locked, the level's tile where the ray meets
        // its floor is the hit, whatever hangs under it; else what is seen on the way up, and with nothing seen that tile.
        float floor = TileSpace.WorldY(_editorLevel);
        bool up = normal.Y > 0f, rises = up && origin.Y < floor;
        Vector3 view = (-camera.GlobalTransform.Basis.Z).Normalized(); // the camera's forward: an item's picture is square to it (PickShape.FacesEye)
        var ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, normal.X, -normal.Z, normal.Y, EditorPickMetres, view.X, -view.Z, view.Y,
            camera.GlobalTransform.Basis.X.X, -camera.GlobalTransform.Basis.X.Z); // (and its right: the top view looks straight down)
        (_pickWorld.PickFloorLevel, _pickWorld.PickTopLevel) = (_editorLevelLock && !up ? _editorLevel : 0, EditorHideAbove);
        if (_editorLevelLock && rises)
        {
            (_editorHit, _editorShape) = (EditorHitOnPlane(origin, normal, floor, _editorLevel), null);
        }
        else
        {
            EditorPick(ray);
            if (_picker.PickEmptyCeilings && _editorHit.Kind == TargetKind.Ceiling && !_map.HasCeilingAt(_editorHit.X, _editorHit.Y, _editorHit.Z)
                && EditorVoid(_editorHit.X, _editorHit.Y, _editorHit.Z))
            {
                _picker.PickEmptyCeilings = false;
                EditorPick(ray);
            }
            // A room taller than a level (open over the empty ceiling, with its walls going on up): the place where its ceiling would be is the top
            // of its walls, not the first level the ray crosses (a floor there is placed from above).
            int topShown = EditorHideAbove == 0 ? map.Levels : EditorHideAbove;
            while (_picker.PickEmptyCeilings && _editorHit.Kind == TargetKind.Ceiling && _editorHit.Z < topShown
                && !_map.HasCeilingAt(_editorHit.X, _editorHit.Y, _editorHit.Z) && EditorRoomGoesUp(_editorHit.X, _editorHit.Y, _editorHit.Z)
                && EditorHitOnPlane(origin, normal, TileSpace.WorldY(_editorHit.Z + 2), _editorHit.Z + 1) is { IsHit: true } higher
                && EditorVoid(higher.X, higher.Y, higher.Z))
                _editorHit = higher with { Kind = TargetKind.Ceiling, Surface = PickSurface.Ceiling };
        }
        // With the levels above hidden, the floor of the first of them is still the ceiling of the top one shown and is drawn from under it: a ray that
        // goes up to it hits it (the pick itself stops at the top level shown).
        int top = EditorHideAbove;
        if (!_editorHit.IsHit && up && top != 0 && _map.LevelAbove(top) && origin.Y < TileSpace.WorldY(top + 1)
            && EditorHitOnPlane(origin, normal, TileSpace.WorldY(top + 1), top + 1) is { IsHit: true } over && !EditorVoid(over.X, over.Y, over.Z))
            (_editorHit, _editorShape) = (over, null);
        // (space by the map itself: the world may show a preview there, or an edit a moment later)
        bool nothing = !_editorHit.IsHit || _editorHit is { Kind: TargetKind.Turf, Surface: PickSurface.Floor } && _editorHit.Z != _editorLevel
            && map.Contains(_editorHit.X, _editorHit.Y, _editorHit.Z) && EditorVoid(_editorHit.X, _editorHit.Y, _editorHit.Z);
        if (!_editorLevelLock && nothing && !up)
        {
            _pickWorld.PickFloorLevel = _editorLevel;
            EditorPick(ray);
        }
        else if (!_editorLevelLock && nothing && rises)
            (_editorHit, _editorShape) = (EditorHitOnPlane(origin, normal, floor, _editorLevel), null);
        if (!_editorHit.IsHit || _editorHit.Kind == TargetKind.Screen)
        {
            // (a held place or erase tool goes on over its level, at the height its button went down at, where the mouse is on nothing)
            if (!(_editorStroke || _editorDragFrom != null) || EditorDragZ is not { } held)
                return false;
            (_editorHit, _editorShape) = (EditorHitOnPlane(origin, normal, _editorStrokeY, held), null);
            if (!_editorHit.IsHit)
                return false;
        }
        // A ceiling under a level above is that level's floor (D325: a level under another has no ceiling of its own): the place where one would be is
        // that level's open tile, seen from under it.
        if (_editorHit.Kind == TargetKind.Ceiling && _map.LevelAbove(_editorHit.Z) && map.Contains(_editorHit.X, _editorHit.Y, _editorHit.Z + 1))
            _editorHit = _editorHit with { Surface = PickSurface.Floor, Kind = TargetKind.Turf, Z = _editorHit.Z + 1, HitUp = 0 };
        (int X, int Y, int Z) hit = (_editorHit.X, _editorHit.Y, _editorHit.Z);
        // What the mouse is on from under it (5.18): a ceiling; or a floor or a wall of the level above, whose underside is the ceiling of the tile under it.
        // (the place where a ceiling would be is seen from under too, and is no ceiling yet: an open tile of the level above)
        _editorFromUnder = _editorHit.Kind == TargetKind.Ceiling || _editorHit.Kind == TargetKind.Turf && up && _editorHit.HitUp < 1e-6 && hit.Z > 1;
        bool underside = _editorFromUnder && _editorHit.Kind == TargetKind.Turf && map.Contains(hit.X, hit.Y, hit.Z) && !EditorVoid(hit.X, hit.Y, hit.Z);
        _editorSlab = _editorHit.Kind == TargetKind.Ceiling || underside;
        _editorRoom = underside ? (hit.X, hit.Y, hit.Z - 1) : hit;
        // (a thing's tile is its own: a lamp or a sign is drawn on the wall beside its tile and is hit over the wall's tile, where a held click placed a second one)
        if (_editorHit.Kind == TargetKind.Entity && _feed.Replica.TryGet(_editorHit.Id, out ReplicaEntity struck))
            hit = (struck.State.X, struck.State.Y, struck.State.Z);
        _editorMountSide = 0;
        _editorCell = hit;
        int? dragZ = EditorDragZ;
        if (_editorTool == EditorTool.Place && !_editorPasting && EditorBrush() is { } brush)
        {
            bool face = _editorHit.Surface == PickSurface.Wall, side = face && _editorHit.HitUp < WorldScale.WallHeight - 1e-3, thing = MapEdits.IsThing(brush.Path);
            if (underside && dragZ == null)
            {
                // From under a level: what makes a ceiling changes that floor, in its place, and the replace mode takes the place of what was clicked;
                // anything else goes under it, on the tile it is the ceiling of (a wall under a ceiling, a lamp in the room).
                if (_editorPlaceMode != EditorPlaceMode.Replace && MapEdits.CeilingFrom(brush.Path) == null)
                    _editorCell = _editorRoom;
            }
            // The tile the clicked face looks at. Adding: a thing against a wall's side goes on the tile in front of it. Attaching: anything goes beside the side of a
            // block or of a thing that fills its tile, and over a block's top, which is the level above (where the button goes down: a drag goes on from there as ever).
            else if (_editorPlaceMode == EditorPlaceMode.Attach && dragZ == null ? face || _editorHit.Kind == TargetKind.Entity
                : _editorPlaceMode != EditorPlaceMode.Replace && side && thing && !SpawnCatalog.IsBlockWindow(brush.Path))
            {
                _editorCell = PickGrid.InFront(_editorHit, normal.X, -normal.Z, normal.Y, TileSpace.LevelHeight);
                // a thing placed against a wall's side hangs on that wall: a lamp, an alarm, a sign take the type or the dir of that side (MapEdits.Mounted)
                if (side && thing && _editorCell.Z == hit.Z)
                    _editorMountSide = WallMounts.DirOfStep(hit.X - _editorCell.X, hit.Y - _editorCell.Y);
            }
        }
        // A held place or erase tool (5.17) goes on at the height its button went down at. The erase tool over the tiles the mouse crosses there, so a
        // still mouse stays on its tile when what it took away shows what is behind (a thing is taken by the thing under the mouse). A drag goes on over
        // the level it began on: over a hit of another level (a shape drawn a level up, over the walls' tops), the tile of its own level there.
        bool tooling = _editorStroke || _editorDragFrom != null;
        if (_editorTool == EditorTool.Erase && _editorErase != EditorErase.Thing && tooling && dragZ is { } erased)
        {
            if (EditorPlaneCell(origin, normal, _editorStrokeY, erased) is not { } on)
                return false;
            _editorCell = on;
        }
        else if (dragZ is { } level && _editorCell.Z != level)
            _editorCell = (tooling ? EditorPlaneCell(origin, normal, _editorStrokeY, level) : EditorPlaneCell(origin, normal, level)) ?? (_editorCell.X, _editorCell.Y, level);
        return map.Contains(_editorCell.X, _editorCell.Y, _editorCell.Z);
    }

    // What the place tool places: the member picked off the map, else the palette's type with the direction chosen for it.
    private MapMember? EditorBrush()
    {
        if (_editorBrush != null)
            return _editorBrush;
        if (_spawnSelected == null)
            return null;
        byte dir = DirValues[_spawnDirIndex];
        var bare = new MapMember(_spawnSelected.Path, null);
        if (dir == 0 || !MapEdits.IsThing(bare.Path))
            return bare;
        // a family with tg's directional types hangs on that side of its tile (a lamp, an alarm); anything else is turned that way
        return MapEdits.Directional(bare.Path, dir) != null ? MapEdits.Mounted(bare, dir) : MapEdits.WithVar(bare, "dir", dir.ToString(CultureInfo.InvariantCulture));
    }

    // A type was chosen in the palette: it is what the place tool places now.
    private void EditorTypeChosen()
    {
        _editorBrush = null;
        _editorTool = EditorTool.Place;
        _editorPasting = false;
        EditorShowLayerOf(_spawnSelected?.Path);
    }

    // A cable, a pipe or a disposal pipe is placed under the floor tiles: its layer is shown, or the mapper would not see what the tool placed.
    private void EditorShowLayerOf(string path)
    {
        if (path == null || _editor.Feed.UnderFloor || !PropKinds.HasUndertile(path))
            return;
        _editor.Feed.UnderFloor = true;
        EditorNotice("The layer under the floor is shown (View, Under the floor).");
    }

    // True when the editor took the event. In the editor every key and button is the editor's: the game's own do nothing here.
    private bool EditorInput(InputEvent @event)
    {
        // What the interface did not take is the view's only over the view: a list at its end, or a panel's empty part, passes the wheel on (the engine
        // sends a scroll through a control that stops the other buttons), and the map under the docks was zoomed by it.
        if (@event is InputEventMouseButton { Pressed: true } press && !_shell.OverView(press.Position) || @event is InputEventGesture gesture && !_shell.OverView(gesture.Position))
            return true;
        if (@event is InputEventMouseButton { Pressed: true })
            GetViewport().GuiReleaseFocus(); // a click on the view takes the keys back from a panel's field
        switch (@event)
        {
            // On a Mac the system gives Ctrl with the left button as the right button: it is the pick, as Ctrl + click is elsewhere.
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, CtrlPressed: true } picking when OS.HasFeature("macos"):
                if (picking.Pressed && !_editorLooking && EditorPickAt(picking.Position))
                    EditorClick(pick: true, shift: false);
                break;
            case InputEventMouseMotion motion when _editorLooking:
                EditorCameraMotion(motion);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } right:
                EditorCameraHold(right, EditorCamera.Fly);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle:
                EditorCameraHold(middle, middle.ShiftPressed ? EditorCamera.Pan : EditorCamera.Orbit);
                break;
            // Alt with the left button is the middle button (a mouse or a trackpad that has none)
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } alt when alt.Pressed ? alt.AltPressed && !_editorLooking : _editorLooking && _editorCameraButton == MouseButton.Left:
                EditorCameraHold(alt, alt.ShiftPressed ? EditorCamera.Pan : EditorCamera.Orbit);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                EditorWheel(wheel.ButtonIndex == MouseButton.WheelUp ? 1f : -1f, wheel.Position);
                break;
            // a trackpad sends no wheel: two fingers moved are a pan gesture, a pinch a magnify gesture
            case InputEventPanGesture pan:
                EditorWheel(-pan.Delta.Y * EditorTrackpadNotch, pan.Position);
                break;
            case InputEventMagnifyGesture { Factor: > 0f } pinch:
                EditorWheel(-Mathf.Log(pinch.Factor) / Mathf.Log(EditorWheelShare), pinch.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (!left.Pressed)
                {
                    // (the tile the button is let go on, not the one of the frame before)
                    if (EditorDragging && !_editorLooking && _shell.OverView(left.Position) && EditorPickAt(left.Position) && _editorCell.Z == _editorDragTo.Z)
                        _editorDragTo = _editorCell;
                    EditorRelease();
                }
                else if (!_editorLooking && EditorPickAt(left.Position))
                    EditorClick(left.CtrlPressed || left.MetaPressed, left.ShiftPressed);
                break;
            case InputEventKey { Pressed: true } key when !_editorLooking:
                EditorKey(key);
                break;
        }
        return true;
    }

    // A left click on the map with the armed tool (Ctrl: the pick tool for this click).
    private void EditorClick(bool pick, bool shift)
    {
        MapDocument map = _editor.Document;
        (int x, int y, int z) = _editorCell;
        _editorDragTo = _editorCell;
        if (_editorPasting)
        {
            EditorPaste(EditorPasteCorner(_editorClip), z);
            return;
        }
        EditorTool tool = pick ? EditorTool.Pick : _editorTool;
        switch (tool)
        {
            case EditorTool.Select:
                (int X, int Y, int Z, int Member)? under = EditorMemberUnderMouse();
                if (!shift && EditorInRegions(x, y, z))
                {
                    _editorMoveFrom = _editorCell; // the selected rectangles are dragged
                    _editorPressed = under ?? (x, y, z, -1);
                }
                else if (!shift && _editorHit.Kind == TargetKind.Entity && under is { Member: >= 0 } thing && MapEdits.IsThing(map[thing.X, thing.Y, thing.Z].Members[thing.Member].Path))
                {
                    (_editorSelected, _editorRegion) = (thing, null);
                    _editorMoveFrom = _editorCell; // a thing is selected by a click and moved by a drag
                }
                else
                {
                    // Shift with rectangles selected on this level: the drag (a click: its one tile) is one more of them
                    _editorBandAdds = shift && _editorRegion is { } had && had.Z == z;
                    if (!_editorBandAdds)
                        (_editorSelected, _editorRegion) = (shift ? (x, y, z, -1) : under ?? (x, y, z, -1), null);
                    _editorBandFrom = _editorCell; // a drag from here selects a rectangle of tiles
                }
                break;
            case EditorTool.Pick:
                if (_editorHit.Kind == TargetKind.Ceiling)
                {
                    // a ceiling is picked as what makes it: its floor's type, a lattice, a catwalk
                    string roof = EditorCeilingAt(x, y, z);
                    if (roof != null)
                        EditorPickMember(new MapMember(roof == "lattice" ? "/obj/structure/lattice" : roof == "catwalk" ? "/obj/structure/lattice/catwalk" : roof, null));
                }
                else if (EditorMemberUnderMouse() is { Member: EditorCeilingMember } laid)
                {
                    EditorPickMember(new MapMember(MapEdits.LaidCeiling(map[laid.X, laid.Y, laid.Z]), null)); // the tile laid under a level's plating
                }
                else if (EditorMemberUnderMouse() is { Member: >= 0 } picked)
                {
                    EditorPickMember(map[picked.X, picked.Y, picked.Z].Members[picked.Member]);
                }
                break;
            case EditorTool.Place when EditorBrush() != null:
            case EditorTool.Erase:
                // (on a ceiling the place tool changes it with what makes one, or in the replace mode; anything else it places under it, as on any tile)
                _editorErase = EditorEraseFor(shift);
                // (the areas are their tiles', on a ceiling as anywhere: such a stroke is of no ceilings)
                _editorStrokeCeiling = _editorHit.Kind == TargetKind.Ceiling && (tool == EditorTool.Erase ? _editorErase != EditorErase.Area : EditorOnCeiling(EditorBrush().Value));
                _editorStrokeSlab = tool == EditorTool.Place && EditorOnSlabs;
                _editorStrokeUnder = _editorFromUnder;
                _editorStrokeY = TileSpace.WorldY(_editorHit.Z) + (float)_editorHit.HitUp;
                if (tool == EditorTool.Place && _editorStrokeCeiling && MapEdits.CeilingFrom(EditorBrush().Value.Path) == null)
                {
                    EditorNotice("A ceiling is a floor, a lattice or a catwalk: choose one of them to place there.", 4);
                    break;
                }
                // the replace mode: what was clicked gives its place, the thing under the mouse or else the tile's turf, and every other of its type the shape covers
                _editorReplaceTarget = EditorMemberUnderMouse() is { Member: >= 0 } clicked ? map[clicked.X, clicked.Y, clicked.Z].Members[clicked.Member].Path : MapEdits.TurfOf(map[x, y, z]);
                _editorReplaceUnder = _editorStrokeSlab && z > 1 ? MapEdits.UndersideOf(map[x, y, z - 1], map[x, y, z]) : null;
                _editorShapeMount = _editorMountSide;
                if (_editorDraw == MapShape.Zone)
                {
                    List<(int X, int Y)> zone = MapShapes.Zone(map, x, y, z);
                    if (zone == null)
                        EditorNotice(Inv($"The zone has more than {MapShapes.ZoneLimit} tiles: use a rectangle."));
                    else
                        EditorShape(zone, z);
                    break;
                }
                if (_editorDraw != MapShape.Free)
                {
                    _editorDragFrom = _editorCell; // the release makes the shape
                    break;
                }
                map.Begin(tool == EditorTool.Erase ? EditorEraseName(many: false) : (_editorStrokeCeiling ? "Ceiling of " : "Place ") + EditorBrushName());
                _editorStroke = true;
                _editorStrokeZ = z;
                _editorStrokeCell = default;
                _editorStrokeThing = 0;
                _editorRun.Clear();
                _editorRunPieces.Clear();
                EditorStrokeAt();
                // attaching is a click at a time: held, the next tile would be the one beside what the click has just placed
                if (tool == EditorTool.Place && _editorPlaceMode == EditorPlaceMode.Attach)
                    EndEditorStroke();
                if (!_editorStrokeUnder)
                    EditorSeeBuilt(z); // (what is made from under it is seen as the ceiling: the view stays)
                break;
        }
    }

    // The left button is let go: a stroke ends, a dragged shape is made, a dragged rectangle is selected, a dragged selection is moved.
    private void EditorRelease()
    {
        EndEditorStroke();
        MapDocument map = _editor.Document;
        (int X, int Y, int Z) to = _editorDragTo;
        if (_editorDragFrom is { } from)
        {
            _editorDragFrom = null;
            EditorShape(MapShapes.Cells(_editorDraw, from.X, from.Y, to.X, to.Y, EditorDrawsRun()), from.Z);
        }
        if (_editorBandFrom is { } band)
        {
            _editorBandFrom = null;
            if (_editorBandAdds)
                EditorAddRegion((band.X, band.Y, to.X, to.Y, band.Z));
            else if ((band.X, band.Y) != (to.X, to.Y))
                (_editorRegion, _editorSelected) = ((band.X, band.Y, to.X, to.Y, band.Z), null);
            _editorBandAdds = false;
        }
        if (_editorMoveFrom is { } move)
        {
            _editorMoveFrom = null;
            // a click inside the selected rectangle that drags it nowhere selects what was clicked
            if (_editorRegion != null && (to.X, to.Y) == (move.X, move.Y))
                (_editorSelected, _editorRegion) = (_editorPressed, null);
            else
                EditorMove(to.X - move.X, to.Y - move.Y);
        }
    }

    // What the place tool puts on a tile of a shape begun against a wall's side: hung on the wall on that side of the tile, turned to it
    // whatever way the palette faces; the brush as it is where that side has no wall.
    private static MapMember EditorHung(MapDocument map, MapMember brush, int x, int y, int z, byte side)
    {
        if (side == 0)
            return brush;
        (int dx, int dy) = WallMounts.Step(side);
        return map.Contains(x + dx, y + dy, z) && TurfStyle.Classify(MapEdits.TurfOf(map[x + dx, y + dy, z])) == TurfKind.Closed ? MapEdits.Mounted(brush, side) : brush;
    }

    // The place or the erase tool on the tiles of a shape, as one step. A run's pieces are turned along a line.
    private void EditorShape(IReadOnlyList<(int X, int Y)> cells, int z)
    {
        MapDocument map = _editor.Document;
        bool place = _editorTool == EditorTool.Place;
        if (place && EditorBrush() == null)
            return;
        try
        {
            var tiles = new List<(int, int, int, MapModel)>();
            _editorRefused = null;
            MapMember brush = place ? EditorBrush().Value : default;
            List<MapMember> pieces = place && _editorDraw == MapShape.Line && MapEdits.IsRunPiece(brush.Path) ? MapEdits.RunPieces(brush, cells) : null;
            bool slabs = place ? _editorStrokeSlab : _editorErase is EditorErase.Laid or EditorErase.Slab; // ceilings from under a level (5.18)
            foreach (int level in EditorLevels(z))
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    (int x, int y) = cells[i];
                    if (!map.Contains(x, y, level))
                        continue;
                    if (slabs)
                    {
                        tiles.AddRange(EditorCeilingTiles(map, x, y, level, place ? brush : null));
                        continue;
                    }
                    MapModel tile = map[x, y, level];
                    MapModel next = !place ? EditorErased(map, x, y, level) : _editorStrokeCeiling ? MapEdits.WithCeiling(map, tile, MapEdits.CeilingFrom(brush.Path))
                        : pieces != null ? MapEdits.Place(map, tile, pieces[i]) : EditorPlaced(map, x, y, level, EditorHung(map, brush, x, y, level, _editorShapeMount));
                    if (!ReferenceEquals(next, tile))
                        tiles.Add((x, y, level, next));
                }
                if (_editorStrokeCeiling || slabs)
                    break; // (a ceiling is its level's: the height builds no ceilings over it)
            }
            string what = !place ? EditorEraseName(many: true) : _editorStrokeCeiling || slabs ? "Ceiling of " + EditorBrushName() : (_editorPlaceMode == EditorPlaceMode.Replace ? "Replace with " : "Place ") + EditorBrushName();
            if (tiles.Count == 0)
                EditorNotice(_editorRefused ?? (place && _editorPlaceMode == EditorPlaceMode.Replace ? "Nothing to replace there: the shape has none of what was clicked." : "Nothing changed."), _editorRefused == null ? 3 : 8);
            else if (map.Edit(Inv($"{what} ({tiles.Count} tiles)"), tiles))
            {
                EditorNotice(Inv($"{what}: {tiles.Count} tiles"), 2);
                if (!_editorStrokeUnder)
                    EditorSeeBuilt(z);
            }
        }
        catch (MapTextException e)
        {
            EditorNotice("Not changed: " + e.Message);
        }
    }

    // The selection moves by (dx, dy): the selected rectangle with all its tiles hold, or the selected thing to another tile.
    private void EditorMove(int dx, int dy)
    {
        MapDocument map = _editor.Document;
        if (_editorRegion is { } region)
        {
            (dx, dy) = EditorMoveBy(dx, dy);
            if (map.Edit("Move tiles", MapEdits.Move(map, EditorRegionCells(), region.Z, dx, dy)))
                EditorMapRegions(r => (r.X0 + dx, r.Y0 + dy, r.X1 + dx, r.Y1 + dy, r.Z));
        }
        else if (_editorSelected is { Member: >= 0 } thing && (dx, dy) != (0, 0) && map.Contains(thing.X + dx, thing.Y + dy, thing.Z))
        {
            MapMember member = map[thing.X, thing.Y, thing.Z].Members[thing.Member];
            (int X, int Y, int Z) target = (thing.X + dx, thing.Y + dy, thing.Z);
            if (!map.Edit("Move " + (_catalog?.Find(member.Path)?.Name ?? member.Path), MapEdits.MoveThing(map, (thing.X, thing.Y, thing.Z), thing.Member, target)))
                return;
            string text = MapDocument.TextOf(new[] { member });
            IReadOnlyList<MapMember> members = map[target.X, target.Y, target.Z].Members;
            int at = members.Count - 1;
            while (at >= 0 && MapDocument.TextOf(new[] { members[at] }) != text)
                at--;
            _editorSelected = (target.X, target.Y, target.Z, at);
        }
    }

    // What is selected is copied (cut: and taken away): the tiles of the selected rectangle, the selected thing, or the selected tile.
    private void EditorCopy(bool cut)
    {
        MapDocument map = _editor.Document;
        if (_editorRegion is { } region)
        {
            _editorClip = _editorMoreRegions.Count > 0 ? MapEdits.Copy(map, EditorRegionCells(), region.Z) : MapEdits.Copy(map, region.X0, region.Y0, region.X1, region.Y1, region.Z);
            EditorNotice(Inv($"{(cut ? "Cut" : "Copied")} {_editorClip.Count} tiles. Paste, here or in another editor: {KeyOf("edit.paste")}, then click."), 4);
        }
        else if (_editorSelected is { Member: EditorCeilingMember })
        {
            EditorNotice("A ceiling is not copied: pick it and place it, or set it in the Tile panel.", 4);
            return;
        }
        else if (_editorSelected is { } selected)
        {
            MapModel tile = map[selected.X, selected.Y, selected.Z];
            bool thing = selected.Member >= 0 && MapEdits.IsThing(tile.Members[selected.Member].Path);
            _editorClip = thing ? MapClip.Of(tile.Members[selected.Member]) : MapEdits.Copy(map, selected.X, selected.Y, selected.X, selected.Y, selected.Z);
            EditorNotice((cut ? "Cut " : "Copied ") + (thing ? tile.Members[selected.Member].Path : "the tile"), 4);
        }
        else
        {
            EditorNotice("Nothing is selected.", 3);
            return;
        }
        // the copy is the system's too: another editor pastes it, into another map
        if (_editorClipboard)
            DisplayServer.ClipboardSet(_editorClip.ToText());
        if (!cut || map.InStep)
            return;
        // a whole tile that was cut becomes its level's empty tile; a thing or a rectangle goes as Delete takes it
        if (_editorRegion == null && _editorClip.Thing == null && _editorSelected is { } whole)
            EditorEdit("Cut tile", whole.X, whole.Y, whole.Z, (m, _) => m.Empty(whole.Z));
        else
            EditorDeleteSelected();
    }

    // The copy follows the mouse until a click pastes it.
    private void EditorStartPaste()
    {
        // what another editor copied comes through the system's clipboard (this editor's own copy is there too)
        if (_editorClipboard && MapClip.Parse(DisplayServer.ClipboardGet()) is { } copied)
            _editorClip = copied;
        if (_editorClip == null)
        {
            EditorNotice("Nothing was copied.", 3);
            return;
        }
        _editorPasting = true;
        _editorTool = EditorTool.Select;
    }

    // The copy goes on the map with its south-west tile at a tile, as one step; the pasted tiles are the selection.
    private void EditorPaste((int X, int Y) corner, int z)
    {
        MapDocument map = _editor.Document;
        MapClip clip = _editorClip;
        _editorPasting = false;
        try
        {
            if (!map.Edit(clip.Thing is { } thing ? "Paste " + thing.Path : Inv($"Paste {clip.Width} x {clip.Height} tiles"), MapEdits.Paste(map, clip, corner.X, corner.Y, z)))
                return;
            if (clip.Thing != null)
                (_editorSelected, _editorRegion) = ((corner.X, corner.Y, z, -1), null);
            else
                (_editorSelected, _editorRegion) = (null, (Math.Max(1, corner.X), Math.Max(1, corner.Y), Math.Min(map.Width, corner.X + clip.Width - 1), Math.Min(map.Height, corner.Y + clip.Height - 1), z));
        }
        catch (MapTextException e)
        {
            EditorNotice("Not pasted: " + e.Message);
        }
    }

    // The held place or erase tool on the tile and the thing under the mouse, once for each as the mouse crosses them.
    private void EditorStrokeAt()
    {
        MapDocument map = _editor.Document;
        (int x, int y, int z) = _editorCell;
        try
        {
            if (_editorTool == EditorTool.Place)
            {
                if (_editorStrokeCell == _editorCell || EditorBrush() is not { } brush)
                    return;
                if (_editorStrokeCeiling)
                {
                    // a stroke that began on a ceiling goes on over the ceilings the mouse crosses
                    if (_editorHit.Kind != TargetKind.Ceiling || MapEdits.CeilingFrom(brush.Path) is not { } roof)
                        return;
                    foreach ((int cx, int cy) in EditorCrossed(orthogonal: false))
                    {
                        if (map.Contains(cx, cy, z))
                            map.Set(cx, cy, z, MapEdits.WithCeiling(map, map[cx, cy, z], roof));
                    }
                    return;
                }
                if (_editorStrokeSlab)
                {
                    // a stroke that began on a level's underside goes on over the undersides the mouse crosses
                    foreach ((int cx, int cy) in EditorCrossed(orthogonal: false))
                    {
                        foreach ((int tx, int ty, int tz, MapModel model) in EditorCeilingTiles(map, cx, cy, z, brush).ToList())
                            map.Set(tx, ty, tz, model);
                    }
                    return;
                }
                bool run = MapEdits.IsRunPiece(brush.Path);
                foreach ((int cx, int cy) in EditorCrossed(run))
                {
                    if (!map.Contains(cx, cy, z))
                        continue;
                    if (run)
                    {
                        EditorRunTo(brush, cx, cy, z);
                        continue;
                    }
                    foreach (int level in EditorLevels(z))
                        map.Set(cx, cy, level, EditorPlaced(map, cx, cy, level, (cx, cy) == (x, y) ? MapEdits.Mounted(brush, _editorMountSide) : brush));
                }
            }
            else if (_editorErase != EditorErase.Thing)
            {
                // one layer of each tile the mouse crosses, of the kind the button went down on (the pick keeps the mouse's tile at that height)
                if (_editorStrokeCell == _editorCell)
                    return;
                foreach ((int cx, int cy) in EditorCrossed(orthogonal: false))
                {
                    if (_editorErase is EditorErase.Laid or EditorErase.Slab)
                    {
                        foreach ((int tx, int ty, int tz, MapModel model) in EditorCeilingTiles(map, cx, cy, z, null).ToList())
                            map.Set(tx, ty, tz, model);
                        continue;
                    }
                    foreach (int level in _editorErase == EditorErase.Ceiling ? new[] { z } : EditorLevels(z))
                    {
                        if (map.Contains(cx, cy, level))
                            map.Set(cx, cy, level, EditorErased(map, cx, cy, level));
                    }
                }
            }
            else if (_editorHit.Kind == TargetKind.Entity && _editorHit.Id != _editorStrokeThing && _editor.Feed.Shown
                && (_editorStrokeThing == 0 || GetViewport().GetMousePosition().DistanceTo(_editorStrokeMouse) >= EditorEraseStep)
                && _editor.Feed.TryLocate(_editorHit.Id, out int tx, out int ty, out int tz, out int member) && member >= 0)
            {
                // the thing under the mouse; the next one once the mouse has moved (a still mouse does not eat its way through what stands behind)
                (_editorStrokeThing, _editorStrokeMouse) = (_editorHit.Id, GetViewport().GetMousePosition());
                map.Set(tx, ty, tz, MapEdits.Remove(map, map[tx, ty, tz], member));
            }
        }
        catch (MapTextException e)
        {
            EditorNotice("Not placed: " + e.Message);
        }
    }

    // The tiles the mouse crossed since the stroke's last tile, the one under it last: a fast drag skips tiles between two frames.
    private List<(int X, int Y)> EditorCrossed(bool orthogonal)
    {
        (int X, int Y, int Z) last = _editorStrokeCell;
        _editorStrokeCell = _editorCell;
        return last == default || last.Z != _editorCell.Z ? new List<(int, int)> { (_editorCell.X, _editorCell.Y) }
            : MapShapes.Line(last.X, last.Y, _editorCell.X, _editorCell.Y, orthogonal).Skip(1).ToList();
    }

    // A stroke that draws a run reaches a tile: the piece of the tile before it is turned to join this one, and this tile gets its own.
    private void EditorRunTo(MapMember brush, int x, int y, int z)
    {
        MapDocument map = _editor.Document;
        _editorRun.Add((x, y));
        int n = _editorRun.Count, tail = Math.Max(0, n - 3);
        List<MapMember> pieces = MapEdits.RunPieces(brush, _editorRun.GetRange(tail, n - tail));
        if (n >= 2)
        {
            (int px, int py) = _editorRun[n - 2];
            MapMember turned = pieces[^2];
            map.Set(px, py, z, MapEdits.Replace(map, map[px, py, z], _editorRunPieces[n - 2], turned));
            _editorRunPieces[n - 2] = turned;
        }
        _editorRunPieces.Add(pieces[^1]);
        map.Set(x, y, z, MapEdits.Place(map, map[x, y, z], pieces[^1]));
    }

    // What a tool built on a level above the top one shown would not be seen while those levels are hidden: the view opens up to that level (a
    // block attached on a block's top is on the level above). The edited level stays where the mapper set it (the owner, 2026-11-17: it went up
    // with the block, and the next click on empty space built a level higher than he meant); a change of the level or of Above shows the
    // levels by their rule again.
    private void EditorSeeBuilt(int z)
    {
        if (EditorHideAbove == 0 || z <= EditorHideAbove)
            return;
        _editorSeenTo = z;
        EditorNotice(Inv($"Level {z} is shown too, with what was built on it. The edited level is still {_editorLevel}; Above, or a change of the level, hides it again."), 5);
    }

    private void EndEditorStroke()
    {
        if (!_editorStroke)
            return;
        _editorStroke = false;
        _editor.Document.End();
    }

    // The tile and the member of what is under the mouse: a thing's own member, else the tile's turf.
    private (int X, int Y, int Z, int Member)? EditorMemberUnderMouse()
    {
        MapDocument map = _editor.Document;
        if (_editorHit.Kind == TargetKind.Entity)
        {
            if (_editor.Feed.Shown && _editor.Feed.TryLocate(_editorHit.Id, out int x, out int y, out int z, out int member))
                return (x, y, z, member);
            return null;
        }
        (int cx, int cy, int cz) = (_editorHit.X, _editorHit.Y, _editorHit.Z);
        if (!map.Contains(cx, cy, cz))
            return null;
        if (_editorHit.Kind == TargetKind.Ceiling)
            return (cx, cy, cz, EditorCeilingMember);
        // (from under a level: the tile laid under its plating is the ceiling of the tile under it; with none laid, the turf of the level above)
        if (_editorSlab && map.Contains(_editorRoom.X, _editorRoom.Y, _editorRoom.Z) && MapEdits.LaidCeiling(map[_editorRoom.X, _editorRoom.Y, _editorRoom.Z]) != null)
            return (_editorRoom.X, _editorRoom.Y, _editorRoom.Z, EditorCeilingMember);
        IReadOnlyList<MapMember> members = map[cx, cy, cz].Members;
        for (int i = 0; i < members.Count; i++)
        {
            if (MapEdits.IsTurf(members[i].Path))
                return (cx, cy, cz, i);
        }
        return (cx, cy, cz, -1);
    }

    private string EditorBrushName() => EditorBrush() is { } brush ? (_catalog?.Find(brush.Path)?.Name ?? brush.Path) : string.Empty;

    // One whole step on one tile: false, with a notice, when the format refuses the change.
    private bool EditorEdit(string label, int x, int y, int z, Func<MapDocument, MapModel, MapModel> change)
    {
        MapDocument map = _editor.Document;
        try
        {
            return map.Edit(label, new[] { (x, y, z, change(map, map[x, y, z])) });
        }
        catch (MapTextException e)
        {
            EditorNotice("Not changed: " + e.Message);
            return false;
        }
    }

    private void EditorDeleteSelected()
    {
        if (_editor.Document.InStep)
            return; // (a key pressed while a stroke is drawn)
        if (_spawnTab == SpawnTab.Areas)
        {
            // while the palette shows the areas, the areas alone (EditorEraseFor): of the selected rectangles' tiles, or of the selected tile
            MapDocument doc = _editor.Document;
            List<(int X, int Y, int Z)> cells = _editorRegion is { } box ? EditorRegionCells().Where(c => doc.Contains(c.X, c.Y, box.Z)).Select(c => (c.X, c.Y, box.Z)).ToList()
                : _editorSelected is { } tile ? new List<(int X, int Y, int Z)> { (tile.X, tile.Y, tile.Z) } : new List<(int X, int Y, int Z)>();
            List<(int, int, int, MapModel)> bare = cells.Select(c => (c.X, c.Y, c.Z, MapEdits.Unareaed(doc, doc[c.X, c.Y, c.Z]))).Where(c => !ReferenceEquals(c.Item4, doc[c.Item1, c.Item2, c.Item3])).ToList();
            if (bare.Count == 0)
                EditorNotice("No area to take away there: the palette shows the areas, and Delete takes those alone.", 4);
            else
                doc.Edit(bare.Count > 1 ? "Take the areas away" : "Take the area away", bare);
            return;
        }
        if (_editorRegion is { } region)
        {
            // the tiles of the selected rectangles become the level's empty tile
            MapDocument map = _editor.Document;
            map.Edit("Clear tiles", EditorRegionCells().Where(c => map.Contains(c.X, c.Y, region.Z)).Select(c => (c.X, c.Y, region.Z, map.Empty(region.Z))));
            return;
        }
        if (_editorSelected is { Member: EditorCeilingMember } roof)
        {
            // the selected ceiling loses its top layer: a tile's leaves plating, plating nothing. Under a level above the tile laid under that level's
            // plating comes off, and the plating is that level's floor.
            if (_map.LevelAbove(roof.Z))
                EditorEdit("Take the ceiling's tile off", roof.X, roof.Y, roof.Z, (map, tile) => MapEdits.WithCeiling(map, tile, null));
            else if (MapEdits.CeilingPeeled(EditorCeilingAt(roof.X, roof.Y, roof.Z)) is { } left)
                EditorEdit("Take the ceiling's top layer off", roof.X, roof.Y, roof.Z, (map, tile) => MapEdits.WithCeiling(map, tile, left));
            return;
        }
        if (_editorSelected is not { Member: >= 0 } selected)
            return;
        MapMember member = _editor.Document[selected.X, selected.Y, selected.Z].Members[selected.Member];
        if (EditorEdit("Delete " + member.Path, selected.X, selected.Y, selected.Z, (map, tile) => MapEdits.Remove(map, tile, selected.Member, map.Empty(selected.Z))) && MapEdits.IsThing(member.Path))
            _editorSelected = (selected.X, selected.Y, selected.Z, -1);
    }

    private void EditorUndo(bool redo)
    {
        MapDocument map = _editor.Document;
        if (map.InStep)
            return;
        string label = redo ? map.RedoLabel : map.UndoLabel;
        if (redo ? map.Redo() : map.Undo())
            EditorNotice((redo ? "Done again: " : "Taken back: ") + label, 2);
    }

    private void EditorNotice(string text, double seconds = 6)
    {
        _editorNotice = text;
        _editorNoticeUntil = App.NowSeconds + seconds;
    }

    // The console's "editor ..." lines (a scripted run's --dev-after too): true when the line was the editor's.
    //   editor save | editor saveas PATH | editor open PATH | editor new PATH W H Z | editor import DMM [PATH]
    //   editor log [LINES] | editor undo | editor redo | editor place X Y Z MEMBER | editor erase X Y Z INDEX | editor set X Y Z INDEX VAR [LITERAL] | editor select X Y Z [INDEX]
    //   editor tool NAME | editor shape NAME | editor brush MEMBER | editor region X0 Y0 X1 Y1 [Z] | editor copy | editor cut | editor paste X Y [Z] | editor move DX DY
    //   editor delete | editor find TEXT | editor layer under|markers|areas|border|preview on|off
    //   editor level N | editor above on|off | editor lock on|off | editor mode add|replace|attach | editor height N | editor setting KEY [VALUE] | editor ceiling VALUE | editor gas MIX|own|window
    //   editor key FUNCTION KEY|none|default | editor keys | editor do FUNCTION | editor autosave | editor restore [N] | editor state
    //   editor checks | editor package ZIP | editor panel TITLE
    //   editor view X Y EYE YAW PITCH | editor top on|off | editor focus | editor home | editor look simple|lighting|shadows|dark|bloom on|off
    //   editor play [X Y [Z] | arrival | start] (and, in the round: editor stop)
    private bool EditorCommand(string command)
    {
        string[] words = command.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words[0] != "editor")
            return false;
        string rest = words.Length > 2 ? words[2] : string.Empty;
        MapDocument map = _editor.Document;
        _editorNotice = null;
        try
        {
            switch (words[1])
            {
                case "save": SaveEditorMap(); break;
                case "panel": ShowPanel(rest.Trim()); break; // editor panel TITLE: brings a panel in front, for a scripted look
                case "saveas": EditorSaveTo(string.IsNullOrWhiteSpace(rest) ? rest : EditorSession.Placed(rest)); break;
                case "open": EditorOpen(rest, map.Width, map.Height, map.Levels); break;
                case "new":
                {
                    string[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    EditorOpen(p[0], int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture), create: true);
                    break;
                }
                case "newshuttle":
                {
                    // editor newshuttle PATH W H: a new shuttle's map (5.11)
                    string[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    EditorOpen(p[0], int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), 1, create: true, shuttle: true);
                    break;
                }
                case "import":
                {
                    string[] p = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    EditorImport(p[0], p.Length > 1 ? p[1] : null);
                    break;
                }
                case "log":
                    // what the world behind the view wrote last: its warnings, the runtime errors of the map's init, an edit that was not loaded
                    GD.Print(Inv($"[anomaly] editor: log (shown {_editor.Feed.Shown}, busy {_editor.Feed.Busy}, tick {_editor.Feed.Tick}, {_editor.Feed.Count} things, {Engine.GetFramesPerSecond():0} fps, {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} draws, {_light?.LampCount ?? 0} lamps, {_light?.ShadowedLightCount ?? 0} with shadows)"));
                    // the camera as --view has it: the tile it is over, its height, its yaw and pitch
                    GD.Print(Inv($"[anomaly] editor: view {_rig.FreePosition.X / TileSpace.TileSize + 0.5f:0.00} {-_rig.FreePosition.Z / TileSpace.TileSize + 0.5f:0.00} {_rig.FreePosition.Y:0.00} {Mathf.RadToDeg(_rig.Yaw):0.0} {Mathf.RadToDeg(_rig.Pitch):0.0}{(_editorTop ? " top" : string.Empty)}"));
                    foreach (string line in _editor.Feed.Log.TakeLast(rest.Length > 0 ? int.Parse(rest, CultureInfo.InvariantCulture) : 20))
                        GD.Print("[anomaly] editor:   " + line);
                    break;
                case "undo": EditorUndo(redo: false); break;
                case "redo": EditorUndo(redo: true); break;
                case "tool": _editorTool = Enum.Parse<EditorTool>(rest, ignoreCase: true); break;
                case "shape": _editorDraw = Enum.Parse<MapShape>(rest, ignoreCase: true); break;
                case "brush":
                    // the member's text, read by the format's own reader inside a whole tile
                    EditorPickMember(map.Model(rest + (MapEdits.IsArea(rest) ? string.Empty : MapEdits.IsTurf(rest) ? " | /area/space" : " | /turf/open/space | /area/space")).Members[0]);
                    break;
                case "region":
                {
                    // region [add] X0 Y0 X1 Y1 [Z]: the selected rectangle, or one more
                    bool add = rest.StartsWith("add ", StringComparison.Ordinal);
                    int[] p = (add ? rest.Substring(4) : rest).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => int.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    (int, int, int, int, int) region = (p[0], p[1], p[2], p[3], p.Length > 4 ? p[4] : _editorLevel);
                    if (add)
                        EditorAddRegion(region);
                    else
                        (_editorRegion, _editorSelected) = (region, null);
                    break;
                }
                case "rotate": EditorRotate(clockwise: rest != "ccw"); break;
                case "state":
                    // what the editor is at, for a scripted check: the level, the tool, what is selected and what the mouse is on
                    GD.Print(Inv($"[anomaly] editor: state level {_editorLevel} of {map.Levels}, tool {_editorTool}, mode {_editorPlaceMode}, shape {_editorDraw}, lock {(_editorLevelLock ? "on" : "off")}, above {(_editorAbove ? "on" : "off")}, shown to {(EditorHideAbove == 0 ? map.Levels : EditorHideAbove)}, camera {_rig.FreePosition.X:0.##} {_rig.FreePosition.Y:0.##} {_rig.FreePosition.Z:0.##}, arrows {(_editorArrowsOn ? _editorArrowList.Count : -1)}, title '{_editor.Title}', selected {(_editorSelected is { } s ? Inv($"{s.X},{s.Y},{s.Z},{s.Member}") : "none")}, regions {EditorRegions.Count()}, mouse {(_editorOnMap ? Inv($"{_editorHit.Kind} {_editorHit.Surface} at {_editorHit.X},{_editorHit.Y},{_editorHit.Z} cell {_editorCell.X},{_editorCell.Y},{_editorCell.Z}") : "off the map")}"));
                    break;
                case "tile":
                {
                    // editor tile X Y Z: what the map has on a tile, for a scripted check
                    int[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => int.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    GD.Print(Inv($"[anomaly] editor: tile {p[0]},{p[1]},{p[2]} = {map[p[0], p[1], p[2]].Text}"));
                    break;
                }
                case "shapes":
                {
                    // editor shapes X Y Z: what a click is tested against on a tile, for a scripted check
                    int[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => int.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    var shapes = new List<PickShape>();
                    _map.CollectStatic(p[0], p[1], p[2], shapes);
                    _entities.CollectPickShapes(shapes, 0);
                    foreach (PickShape shape in shapes.Where(shape => (shape.TileX, shape.TileY, shape.TileZ) == (p[0], p[1], p[2])))
                        GD.Print(Inv($"[anomaly] editor:   shape #{shape.EntityId} {(_feed.Replica.TryGet(shape.EntityId, out ReplicaEntity of) ? of.TypePath : "?")} {shape.Kind} {shape.Surface} up {shape.Up:0.###} rect {shape.Rect.Left:0.##}..{shape.Rect.Right:0.##} x {shape.Rect.Bottom:0.##}..{shape.Rect.Top:0.##} image {shape.Image?.Width}x{shape.Image?.Height} box {shape.MinUp:0.##}..{shape.MaxUp:0.##}"));
                    break;
                }
                case "autosave": WriteAutosave(); break; // the copy of the unsaved changes is written now (5.21)
                case "restore":
                    // the kept copies are listed and offered; with a number, that one is restored
                    EditorOfferRestore(always: true);
                    if (rest.Length > 0)
                        EditorRestore(int.Parse(rest, CultureInfo.InvariantCulture) - 1);
                    break;
                case "key": EditorKeyCommand(rest); break; // editor key FUNCTION KEY|none|default (5.16)
                case "keys": EditorPrintKeys(); break;
                case "do": EditorDo(_editorActionIds.ContainsKey(rest) ? rest : throw new ArgumentException("no such function: " + rest)); break; // a function by its id, as its key runs it
                case "lock": _editorLevelLock = rest == "on"; break;
                case "clipboard": _editorClipboard = rest == "on"; break;
                case "copy": EditorCopy(cut: false); break;
                case "cut": EditorCopy(cut: true); break;
                case "paste":
                {
                    int[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => int.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    if (_editorClip == null)
                        EditorNotice("Nothing was copied.");
                    else
                        EditorPaste((p[0], p[1]), p.Length > 2 ? p[2] : _editorLevel);
                    break;
                }
                case "move":
                {
                    string[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    EditorMove(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture));
                    break;
                }
                case "delete": EditorDeleteSelected(); break;
                case "find": EditorFind(rest, show: true); break;
                case "view":
                {
                    // the camera over a tile, as --view: X Y in tiles, the eye's height in metres, the yaw and the pitch in degrees
                    float[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => float.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    EditorSetTop(false);
                    _rig.PlaceFree(new Vector3(TileSpace.WorldX(p[0]), p[2], TileSpace.WorldZ(p[1])));
                    (_rig.Yaw, _rig.Pitch) = (Mathf.DegToRad(p[3]), Mathf.DegToRad(p[4]));
                    break;
                }
                case "top": EditorSetTop(rest == "on"); break;
                case "focus": EditorFocus(); break;
                case "home": EditorHome(); break;
                case "checks": ShowPanel("Checks"); break; // the Checks panel comes to the front: it checks the map and prints how many it found (5.13)
                case "package": EditorPackage(rest); break; // the map's package into a zip (5.13)
                case "ceiling": EditorSetCeiling(rest is "" or "rule" ? null : rest); break; // over the selection (5.12): none, lattice, catwalk, a floor type, rule
                case "gas" when rest == "window": OpenGasDialog(); break; // the Tile panel's gas window, for a scripted look
                case "gas": EditorSetGas(rest is "" or "own" ? null : rest); break; // over the selection (5.23): tg's gas string, or own
                case "setting":
                {
                    // a setting of the map by its name (5.10): editor setting sky colours, editor setting sun_energy 2, editor setting station_name Port Tarkon
                    string[] p = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    EditorSetting(p[0], p.Length > 1 ? p[1] : null);
                    break;
                }
                case "level": EditorSetLevel(int.Parse(rest, CultureInfo.InvariantCulture)); break;
                case "above": EditorSetAbove(rest == "on"); break;
                case "mode": _editorPlaceMode = Enum.Parse<EditorPlaceMode>(rest, ignoreCase: true); break;
                case "height": _editorHeight = Math.Clamp(int.Parse(rest, CultureInfo.InvariantCulture), 1, map.Levels); break;
                case "play":
                {
                    // a round of the map with the player on a tile (else where Play finds one, or as the tool bar's choice says); "arrival": as a new
                    // arrival on the map's arrivals shuttle; again while it starts: given up
                    if (rest is "arrival" or "start")
                    {
                        EditorPlay(arrive: rest == "arrival", atStart: rest == "start");
                        break;
                    }
                    int[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => int.Parse(word, CultureInfo.InvariantCulture)).ToArray();
                    EditorPlay(p.Length >= 2 ? (p[0], p[1], p.Length > 2 ? p[2] : _editorLevel) : null);
                    break;
                }
                case "look":
                {
                    string[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    bool on = p[1] == "on";
                    if (p[0] == "simple")
                        _editorSimple = on;
                    else if (Enum.TryParse(p[0], ignoreCase: true, out EditorLook part) && Enum.IsDefined(part))
                        _editorLook[(int)part] = on;
                    else
                        EditorNotice("No such part of the look: " + p[0]);
                    ApplyEditorLook();
                    break;
                }
                case "layer":
                {
                    string[] p = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    bool on = p[1] == "on";
                    if (p[0] == "under")
                        _editor.Feed.UnderFloor = on;
                    else if (p[0] == "markers")
                        _editor.Feed.Markers = on;
                    else if (p[0] == "areas")
                        _editorAreas = on;
                    else if (p[0] == "border")
                        _editorBorderOn = on;
                    else if (p[0] == "preview")
                        _editorPreviewOn = on;
                    else if (p[0] == "docks")
                        _editorDocksOn = on;
                    else
                        EditorNotice("No such layer: " + p[0]);
                    break;
                }
                case "place" or "erase" or "set" or "select":
                {
                    string[] p = rest.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
                    int x = int.Parse(p[0], CultureInfo.InvariantCulture), y = int.Parse(p[1], CultureInfo.InvariantCulture), z = int.Parse(p[2], CultureInfo.InvariantCulture);
                    string tail = p.Length > 3 ? p[3] : string.Empty;
                    if (!map.Contains(x, y, z))
                    {
                        EditorNotice(Inv($"No tile at {x}, {y}, {z}"));
                    }
                    else if (words[1] == "place")
                    {
                        // the member's text, read by the format's own reader inside a whole tile
                        MapMember member = map.Model(tail + (MapEdits.IsArea(tail) ? string.Empty : MapEdits.IsTurf(tail) ? " | /area/space" : " | /turf/open/space | /area/space")).Members[0];
                        EditorEdit("Place " + member.Path, x, y, z, (m, tile) => MapEdits.Place(m, tile, member));
                    }
                    else if (words[1] == "erase")
                    {
                        EditorEdit("Erase", x, y, z, (m, tile) => MapEdits.Remove(m, tile, int.Parse(tail, CultureInfo.InvariantCulture), m.Empty(z)));
                    }
                    else if (words[1] == "select")
                    {
                        (_editorSelected, _editorRegion) = ((x, y, z, tail.Length > 0 ? int.Parse(tail, CultureInfo.InvariantCulture) : -1), null);
                    }
                    else
                    {
                        string[] v = tail.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        EditorEdit("Set " + v[1], x, y, z, (m, tile) => MapEdits.SetVar(m, tile, int.Parse(v[0], CultureInfo.InvariantCulture), v[1], v.Length > 2 ? v[2] : null));
                    }
                    break;
                }
                default:
                    EditorNotice("No such editor command: " + words[1]);
                    break;
            }
        }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException or MapTextException or ArgumentException)
        {
            EditorNotice("editor " + words[1] + ": " + e.Message);
        }
        if (_editorNotice != null)
            GD.Print("[anomaly] editor: " + _editorNotice);
        return true;
    }
}
