using System.Globalization;
using System.Linq;
using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client;

// M4a P7 (D124-D128, D131; docs/ai/specs/interaction.md): the game scene's interaction half: the pick under the crosshair and its highlight, the mouse and keys, hover and
// the radial, cursor mode and the native windows, the feedback, and the scripted shot scenarios. The engine-free parts are in Shared/Play (WorldPicker, HudPicker, RadialModel,
// ContainerLayout, ChatFeed, ThrowVisual); this file only reads the engine's input and draws.
public partial class GameScene
{
    private WorldPickAdapter _pickWorld;
    private WorldPicker _picker;
    private HudPicker _hudPicker;
    private HoverHighlight _highlight;
    private Radial _radial;
    private ChatLines _chatLines;
    private ViewModel _viewModel;
    private ContainerWindow _storageWindow;
    private Anomaly.Client.Shared.Tgui.TguiWindows _tguiWindows;
    private Hud.Tgui.TguiHost _tguiHost;
    private AudioStreamPlayer _lobbySounds;
    private Hud.TooltipView _tooltipView; // protocol version 19: tg's tooltip of the hovered HUD object

    /// <summary>M5 slice 3: a tgui window is open (the mouse is free for it, as for the dev menu).</summary>
    private bool TguiOpen => _tguiWindows?.AnyOpen == true;
    private InventoryPanel _inventoryPanel;
    private readonly RadialModel _radialModel = new();
    private readonly ChatFeed _chat = new();
    private readonly BalloonModel _balloon = new();
    private PickHit _hit;

    // Version 34 (DV94): the camera's aim this frame (the eye and the crosshair's direction), null when the view does not aim at the world.
    private ShotAim? _aim;
    private PickShape _hitShape;
    private ClickTarget _target;
    private ContextHintsMessage _hints;
    private readonly MouseOwner _mouse = new(); // D282: cursor mode and the pointer
    private bool _throwModeOn;
    private bool _radialSticky;
    private ClickTarget _rightTarget, _radialTarget;
    private PickRay? _throwRay; // D328: the crosshair's ray this frame, for the one-key throw
    private bool _throwThird;
    private double _throwLead;
    private long _throwExclude;
    private ClickModifiers _rightModifiers;
    private readonly Queue<(double At, MouseButton Button, Vector2 Point)> _pendingReleases = new();
    // Protocol version 17 (drag and drop): the cursor mode's left press, drag and release, and the dragged icon under the pointer.
    private readonly DragModel _drag = new();
    private TextureRect _dragIcon;
    private IAppearanceTextures _dragTextures;
    private readonly Queue<(double At, Vector2 Point)> _pendingMoves = new();
    private Vector2 _scriptedMouse; // where a scripted drag's mouse is (a captured mouse does not move: the way moved is counted from here)
    private MouseButton _scriptedDrag; // the button a scripted drag holds
    private readonly List<(double At, Action Do)> _shotSteps = new();
    private int _shotNext;

    private void BuildInteractionUi(CanvasLayer ui, IAppearanceTextures textures)
    {
        ReplicaWorld replica = _feed.Replica;
        _pickWorld = new WorldPickAdapter(_map, _entities);
        _picker = new WorldPicker(_pickWorld);
        _hudPicker = new HudPicker(new CompositeSurface(this));
        _highlight = new HoverHighlight();
        AddChild(_highlight);

        var windows = new CanvasLayer { Name = "Windows", Layer = 2 };
        AddChild(windows); // not an interface layer: a container opened in the world still opens with the interface hidden
        _viewModel = new ViewModel();
        _viewModel.Bind(replica, textures, _rig.Camera); // D285: the held things hang in front of the camera
        _entities.OwnFired = colour =>
        {
            _crosshair?.Fired(App.NowSeconds); // DV96: the gun crosshair opens
            return _viewModel.Fire(colour); // D176: the own gun's kick and muzzle flash
        };
        _entities.OwnMuzzle = _viewModel.TryMuzzle; // D285: the own bolts start at the gun's muzzle
        _storageWindow = new ContainerWindow();
        windows.AddChild(_storageWindow);
        _storageWindow.Bind(replica, textures, HudGroup.Storage, "Open container", "storage", _options, SaveOptions);
        _inventoryPanel = new InventoryPanel();
        windows.AddChild(_inventoryPanel);
        _inventoryPanel.Bind(replica, textures, HudGroup.ToggleableInventory, "Inventory", "inventory", _options, SaveOptions);
        // Version 17: BYOND draws the dragged object's icon at the pointer; above the HUD and the windows.
        var dragLayer = new CanvasLayer { Name = "Drag", Layer = 3 };
        AddChild(dragLayer); // stays with the windows: a thing dragged out of an open container
        _dragIcon = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, StretchMode = TextureRect.StretchModeEnum.Scale, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Modulate = new Color(1f, 1f, 1f, 0.8f), TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        dragLayer.AddChild(_dragIcon);
        _dragTextures = textures;

        // M5 slice 3 (D148, protocol 16): tg's tgui windows, drawn natively over the HUD.
        Anomaly.Client.Shared.Tgui.TguiContext.CopyText = text => DisplayServer.ClipboardSet(text);
        _tguiWindows = new Anomaly.Client.Shared.Tgui.TguiWindows(_options.WindowSizes)
        {
            SendAct = (window, action, parameters) => _session.UiAct(window, action, parameters),
            SendClose = window => _session.UiClose(window),
            SendTopic = href => _session?.Topic(href),
        };
        _tguiWindows.PositionsChanged += SaveOptions;
        _tguiHost = new Hud.Tgui.TguiHost();
        _tguiHost.LobbySettings = () => _optionsPanel.Visible = true; // D282
        var tguiLayer = new CanvasLayer { Name = "Tgui", Layer = 5 }; // over the HUD, the crosshair and the screentips
        AddChild(tguiLayer); // not an interface layer: a console or computer used in the world opens its window with the interface hidden
        tguiLayer.AddChild(_tguiHost);
        _tguiHost.Bind(_tguiWindows, textures);
        // M10 slice F: tg's lobby screen: its menu sounds (tgui-lobby audio.ts: a one-shot at 0.6) and its info TV's font
        _lobbySounds = new AudioStreamPlayer { Name = "LobbySounds", Bus = Anomaly.Client.Audio.SoundPlayer.UiBus, VolumeDb = Mathf.LinearToDb(0.6f) };
        AddChild(_lobbySounds);
        _tguiHost.BindLobby(path =>
        {
            AudioStream stream = _sound?.Library?.Get(path, false);
            if (stream == null)
                return;
            _lobbySounds.Stream = stream;
            _lobbySounds.Play();
        }, Hud.Maptext.MaptextRenderer.Shared.Fonts.Get(Anomaly.Client.Shared.Maptext.MaptextFont.Grand9K, false, false));

        // Protocol version 19 (DV47): tg's tooltip, over everything (tg's tooltip is a window of its own over the map and the browsers).
        _tooltipView = new Hud.TooltipView { Name = "TooltipView" };
        var tooltipLayer = new CanvasLayer { Name = "Tooltip", Layer = 6 };
        AddChild(tooltipLayer);
        _interfaceLayers.Add(tooltipLayer);
        tooltipLayer.AddChild(_tooltipView);

        _chatLines = new ChatLines();
        ui.AddChild(_chatLines);
        ui.MoveChild(_chatLines, 0);
        _statsCorner = new StatsCorner { Visible = false };
        ui.AddChild(_statsCorner);
        ui.MoveChild(_statsCorner, 1);
        _radial = new Radial();
        ui.AddChild(_radial);
        _radial.Bind(_radialModel, replica, textures);
        // protocol version 78: the own mob's command verbs (the AI's "AI Commands" tab)
        var commands = new CommandsWindow();
        commands.Bind(replica, path => _session?.RunVerb(path), ui, _options, SaveOptions, () => _hudLayer?.TopLeftRowRight(48f) ?? 0f);
        // protocol version 78 (DV387): the popup windows and the AI's multicamera windows
        var viewWindows = new ViewWindows();
        AddChild(viewWindows);
        viewWindows.Bind(replica, ui, _options, SaveOptions, () => _session);

        _feed.ContextHintsReceived += OnHints;
        replica.RadialChanged += OnServerRadial;
        replica.ScreenEntryChanged += OnScreenEntry;
        replica.ScreenEntryRemoved += OnScreenGone;
        BuildShotScript();
    }

    private void TeardownInteraction()
    {
        if (_feed == null)
            return;
        _feed.ContextHintsReceived -= OnHints;
        try
        {
            _feed.Replica.ScreenEntryChanged -= OnScreenEntry;
            _feed.Replica.ScreenEntryRemoved -= OnScreenGone;
            _feed.Replica.RadialChanged -= OnServerRadial;
        }
        catch (NotImplementedException)
        {
        }
    }

    // The HUD pick reads the native windows first (they are drawn over the HUD), then the HUD layer's entries.
    private sealed class CompositeSurface : IHudSurface
    {
        private readonly GameScene _scene;

        public CompositeSurface(GameScene scene) => _scene = scene;

        public void Collect(List<HudDrawn> bottomToTop)
        {
            _scene._hudLayer.Collect(bottomToTop);
            _scene._inventoryPanel.Collect(bottomToTop);
            _scene._storageWindow.Collect(bottomToTop);
        }
    }

    // ---- hover, hints, throw mode ----

    private void OnHints(ContextHintsMessage hints)
    {
        _hints = hints;
        // Version 36 (D230): the answer to the radial's re-hover (OpenRadial) brings the entries possible now; they replace the open radial's
        // only when they differ (a replacement re-centres the aim).
        if (_radialModel.Phase != RadialPhase.Open || _radialSticky || _radialModel.ServerMenu != 0 || HintsFor(_radialTarget) != hints)
            return;
        List<RadialEntry> fresh = RadialModel.Build(hints, PileAt(_radialTarget).Count, _radialTarget.Kind == TargetKind.Entity);
        if (!fresh.SequenceEqual(_radialModel.Entries))
            _radialModel.SetEntries(fresh);
    }

    private void OnScreenEntry(ScreenEntry entry)
    {
        if (entry.Group != HudGroup.Hotkeys || !_feed.Replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
            return;
        string state = _feed.Replica.ResolveString(app.IconState);
        if (state is "act_throw" or "act_throw_on")
            _throwModeOn = state == "act_throw_on";
    }

    private void OnScreenGone(long id)
    {
    }

    // ui-overhaul.md section 5: Godot sends no key release for keys let go while the window has no focus (Input.release_pressed_events clears the
    // key state only), so a lost focus drops a lone Ctrl's or Shift's waiting action
    private void OnFocusLost() => _modifierTap.Drop();

    // The hints that belong to what is hovered now (the answer to an older hover is dropped).
    // DV382: what the own view looks from: a camera eye's drawn place (the AI's eye far from its core), else the own predicted mob
    private (float X, float Y, int Z) ViewerPosition() => _cameraEye ? (_eyePose.X, _eyePose.Y, (int)_eyePose.Z) : _session.Prediction.RenderPosition();

    private ClickTarget _hudHover; // the HUD entry the free pointer hovers (cursor mode), else none
    private TooltipMessage _hudTip;

    // tg's screentip of a hovered HUD entry (on_mouse_enter names it, with its click hints), drawn as the pointer's tooltip where tg's own
    // tooltip (openToolTip: action buttons, alerts) says nothing: the AI's and the cyborgs' HUD buttons have only their names (the owner, 2026-11-14)
    private TooltipMessage HudTooltip()
    {
        if (!_hudHover.IsSet || HintsFor(_hudHover) is not ContextHintsMessage h || string.IsNullOrEmpty(h.Name))
            return null;
        string lmb = HintLabel(h, ClickButton.Left), rmb = HintLabel(h, ClickButton.Right);
        string text = "<p>" + System.Net.WebUtility.HtmlEncode(h.Name) + "</p>"
            + (lmb != null ? "<p>[LMB] " + System.Net.WebUtility.HtmlEncode(lmb) + "</p>" : string.Empty)
            + (rmb != null ? "<p>[RMB] " + System.Net.WebUtility.HtmlEncode(rmb) + "</p>" : string.Empty);
        if (_hudTip?.Text != text)
            _hudTip = new TooltipMessage { Kind = TooltipKind.Show, Text = text, Theme = "default" };
        return _hudTip;
    }

    private ContextHintsMessage HintsFor(in ClickTarget target)
    {
        ContextHintsMessage h = _hints;
        if (h == null || !target.IsSet || h.TargetKind != target.Kind)
            return null;
        return target.Kind switch
        {
            TargetKind.Ceiling or TargetKind.Turf => h.TurfX == target.X && h.TurfY == target.Y && h.TurfZ == target.Z ? h : null,
            _ => h.Target == target.Id ? h : null,
        };
    }

    private static string HintLabel(ContextHintsMessage h, ClickButton button)
    {
        foreach (ContextHint hint in h.Hints)
        {
            if (hint.Button == button && hint.Modifiers == ClickModifiers.None)
                return hint.Label;
        }
        return null;
    }

    private string TargetName(in ClickTarget target)
    {
        string name = BaseTargetName(target);
        if (target.Kind != TargetKind.Turf || name.Length == 0)
            return name;
        // tg names every wall "wall" and every floor "floor": the type tells which (turf/closed/wall/mineral/titanium: "wall (wall/mineral/titanium)").
        string path = _feed.Replica.TurfTypeAt(target.X, target.Y, target.Z) ?? string.Empty;
        int slash = path.StartsWith("/turf/", StringComparison.Ordinal) ? path.IndexOf('/', 6) : -1;
        return slash < 0 ? name : $"{name} ({path.Substring(slash + 1).Replace('_', ' ')})";
    }

    private string BaseTargetName(in ClickTarget target)
    {
        ContextHintsMessage h = HintsFor(target);
        if (h?.Name != null)
            return h.Name;
        if (target.Kind == TargetKind.Entity && _feed.Replica.TryGet(target.Id, out ReplicaEntity e) && !string.IsNullOrEmpty(e.State.Name))
            return e.State.Name;
        if (target.Kind != TargetKind.Turf)
            return string.Empty;
        // Until the cell's hints arrive: what kind of turf it is (a wall was named "floor" before, the owner's play test 2026-10-14).
        string turf = _feed.Replica.TurfTypeAt(target.X, target.Y, target.Z) ?? string.Empty;
        return turf.StartsWith("/turf/closed", StringComparison.Ordinal) ? "wall" : turf.StartsWith("/turf/open/space", StringComparison.Ordinal) ? "space" : "floor";
    }

    // ---- the frame ----

    private PickRay MakeRay(Camera3D camera, int z, double maxMetres)
    {
        Vector3 origin = camera.GlobalPosition;
        Vector3 forward = (-camera.GlobalTransform.Basis.Z).Normalized();
        return new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, forward.X, -forward.Z, forward.Y, maxMetres);
    }

    // The third-person pick's ray: from the camera through the screen's middle (the crosshair) or the pointer. lead is how far along it the
    // own mob is (the camera's distance from it), so the pick ranges count from the mob as the first person's count from the eye.
    private PickRay ThirdPersonRay(Camera3D camera, int z, bool pointer, out double lead)
    {
        Vector3 origin, dir;
        if (pointer)
        {
            Vector2 at = GetViewport().GetMousePosition();
            origin = camera.ProjectRayOrigin(at);
            dir = camera.ProjectRayNormal(at);
        }
        else
        {
            origin = camera.GlobalPosition;
            dir = (-camera.GlobalTransform.Basis.Z).Normalized();
        }
        (float px, float py, int pz) = ViewerPosition();
        var mob = new Vector3(TileSpace.WorldX(px), TileSpace.WorldY(pz) + TileSpace.EyeHeight, TileSpace.WorldZ(py));
        Vector3 view = (-camera.GlobalTransform.Basis.Z).Normalized(); // the camera's forward: an item's picture is square to it (PickShape.FacesEye)
        var ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, dir.X, -dir.Z, dir.Y, WorldPicker.DefaultRange, view.X, -view.Z, view.Y);
        lead = WorldPicker.CameraLead(ray, mob.X, -mob.Z, mob.Y - TileSpace.WorldY(z));
        return ray with { MaxMetres = lead + WorldPicker.DefaultRange };
    }

    // Version 34 (DV94): the third person aims from the own mob's eye toward what the camera's ray hit (or along the ray): a shot leaves the
    // body, not the camera behind it.
    private PickRay EyeRay(in PickRay ray, float ox, float oy, int z) => EyeRay(ray, ox, oy, z, _hit);

    // the ray from the own eye through what the camera's ray hit (the ray's end without a hit)
    private static PickRay EyeRay(in PickRay ray, float ox, float oy, int z, in PickHit _hit)
    {
        double eastE = TileSpace.WorldX(ox), northE = -TileSpace.WorldZ(oy), upE = TileSpace.EyeHeight;
        double tx = _hit.IsHit ? _hit.HitEast : ray.OriginEast + ray.DirEast * ray.MaxMetres;
        double ty = _hit.IsHit ? _hit.HitNorth : ray.OriginNorth + ray.DirNorth * ray.MaxMetres;
        // (a hit on another level: its height is over that level's floor, D325)
        double tz = _hit.IsHit ? _hit.HitUp + (_hit.Z - z) * TileSpace.LevelHeight : ray.OriginUp + ray.DirUp * ray.MaxMetres;
        double dx = tx - eastE, dy = ty - northE, dz = tz - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 1e-6)
            return ray with { OriginEast = eastE, OriginNorth = northE, OriginUp = upE };
        return new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, ray.MaxMetres);
    }

    private void UpdateInteraction(double now)
    {
        Camera3D camera = _rig.Camera;
        bool firstPerson = _rig.Mode == CameraMode.FirstPerson && _session.Predicting && camera != null;
        // Third-person play (the owner, 2026-10-16): the orbit view aims as the first person does, along the crosshair from the camera;
        // in cursor mode the pointer aims at the world as BYOND's and Space Station 14's mouse does (the HUD under it first).
        bool thirdPerson = _rig.Mode == CameraMode.Orbit && _session.Predicting && camera != null;
        bool panels = PanelsOpen;
        bool pointerAim = (firstPerson || thirdPerson) && _mouse.CursorMode && !panels && !_drag.Dragging && !PointerHud().IsSet;
        bool aiming = (firstPerson || thirdPerson) && !_mouse.CursorMode && !panels || pointerAim;
        _hit = default;
        _hitShape = null;
        bool reach = false;
        if (aiming)
        {
            (float ox, float oy, int z) = ViewerPosition();
            long exclude = thirdPerson && pointerAim ? 0 : _feed.OwnEntity;
            _pickWorld.PickExclude = exclude;
            double lead = 0;
            PickRay ray = thirdPerson ? ThirdPersonRay(camera, z, pointerAim, out lead) : MakeRay(camera, z, WorldPicker.DefaultRange);
            _picker.PickEmptyCeilings = Input.IsKeyPressed(Key.Alt) || HoldsACeilingTool();
            _picker.SeeIntoWalls = _entities.TRayShown; // DV356
            if (thirdPerson)
            {
                // The ranges are the eye's: from the camera they start where the ray comes level with the own mob.
                _hit = _throwModeOn ? _picker.PickToRangeFrom(ray, exclude, lead + WorldPicker.ThrowRange) : _picker.Pick(ray with { MaxMetres = lead + WorldPicker.DefaultRange }, exclude);
                if (!_hit.IsHit && !_throwModeOn)
                    _hit = _picker.PickToRangeFrom(ray, exclude, lead + WorldPicker.ViewRange);
                // what the mob cannot see is not clicked (it is in the dark)
                if (_fovOn && _hit.IsHit && !_fov.CellVisible(_hit.X, _hit.Y, _fov.Z))
                    _hit = default;
            }
            else
            {
                if (pointerAim)
                {
                    Vector2 at = GetViewport().GetMousePosition();
                    Vector3 origin = camera.ProjectRayOrigin(at), dir = camera.ProjectRayNormal(at);
                    ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, dir.X, -dir.Z, dir.Y, WorldPicker.DefaultRange);
                }
                _hit = _throwModeOn ? _picker.PickThrow(ray, _feed.OwnEntity) : _picker.Pick(ray, _feed.OwnEntity);
                // Nothing within the ordinary range (open space, a long corridor): the click lands on what is in view farther on, as BYOND's
                // screen always has a turf under the mouse (a ranged item's target: the extinguisher's spray and its push in zero gravity).
                if (!_hit.IsHit && !_throwModeOn)
                    _hit = _picker.PickInView(ray, _feed.OwnEntity);
            }
            _hitShape = _hit.IsHit ? _picker.LastShape : null;
            (_throwRay, _throwThird, _throwLead, _throwExclude) = (ray, thirdPerson, lead, exclude); // the one-key throw's pick (ThrowAtCrosshair)
            reach = WorldPicker.InReach((int)Math.Round(ox), (int)Math.Round(oy), z, _hit, _picker.OpensUp((int)Math.Round(ox), (int)Math.Round(oy), z));
            // D320: the held tool's next build on the aimed tile; the server names the tile, none where a click would not build
            BuildPreviewMessage preview = _feed.BuildPreview;
            _buildGhost?.Show(preview, BuildGhostPlacement.Shows(preview, _hit), preview.X, preview.Y, preview.Z, _ghostIcons);
            _aim = PlaySession.AimOf(thirdPerson ? EyeRay(ray, ox, oy, z) : ray);
        }
        else
        {
            _aim = null;
            _throwRay = null;
            _buildGhost?.Show(null, false, 0, 0, 0, null);
        }
        if (_session.MouseHeld != ClickButton.None)
            _session.MouseDrag(ClickTarget.Of(_hit), HeldModifiers(), _aim, now); // version 34: the held button's aim (tg's full auto follows it)
        _target = ClickTarget.Of(_hit);
        _targetUnderCrosshair = _hit.Kind == TargetKind.Entity ? _hit.Id : 0;
        // The hover: a thing or a cell under the crosshair (tg's screentip names a turf too: its hints carry the wall's or floor's name; while
        // throwing, a cell's hint is the throw).
        ClickTarget hover = _hit.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling ? _target : ClickTarget.None;
        // Protocol version 14: in cursor mode the pointer hovers the HUD entry under it, as BYOND's mouse over the screen does (tg's hover
        // states: the suit display's percentage, an inventory slot's outline, the action buttons' names), and leaving it is an unhover.
        if (_mouse.CursorMode && _session.Predicting && !PanelsOpen && !pointerAim)
            hover = ClickTarget.Of(_hudPicker.PickScreen(GetViewport().GetMousePosition().X, GetViewport().GetMousePosition().Y));
        _session.Hover(hover, now);
        _hudHover = hover.Kind == TargetKind.Screen ? hover : default;
        _crosshair.Visible = (firstPerson || thirdPerson) && !PanelsOpen && !_mouse.CursorMode;
        _crosshair.Set(_hit.Kind == TargetKind.Entity, reach);
        GunSightMessage sight = _feed.GunSight; // DV96: the shooter's crosshair while a gun is held
        _crosshair.SetGun(sight.Gun, sight.ConeDegrees, _rig.FovDegrees);
        if (_feed.GunSightHits != _gunSightHits)
        {
            _gunSightHits = _feed.GunSightHits;
            _crosshair.Hit(now);
        }
        _crosshair.Animate(now);
        // A tile and a thing are outlined only with their options on (the owner, 2026-10-09 and 2026-10-13).
        // Version 17: while dragging in cursor mode, the world thing under the pointer (where the drag would land) is outlined.
        PickShape dragShape = null;
        PickHit dragOver = _drag.Dragging && camera != null ? PointerWorldPick(camera, out dragShape) : default;
        if (aiming && (_hit.Kind is TargetKind.Turf or TargetKind.Ceiling ? _options.HighlightTile : _hit.Kind == TargetKind.Entity && _options.HighlightThing))
            _highlight.Show(_hit, _hitShape, camera.GlobalTransform.Basis, now);
        else if (dragOver.Kind == TargetKind.Entity && !PointerHud().IsSet)
            _highlight.Show(dragOver, dragShape, camera.GlobalTransform.Basis, now);
        else
            _highlight.Show(default, null, Basis.Identity, now);
        UpdateDragIcon(now);

        Vector2 size = GetViewport().GetVisibleRect().Size;
        var metrics = new HudMetrics(size.X, size.Y, _options.UiScale);
        _viewModel.Update(now, _handsShown, _feed.OwnEntity, HeldHolder());
        _storageWindow.Update(now, metrics);
        _inventoryPanel.CursorMode = _mouse.CursorMode;
        _inventoryPanel.Update(now, metrics);

        if (_radialModel.Phase == RadialPhase.Pressed && _radialModel.Update(now))
            OpenRadial();
        if (_radialModel.ServerMenu != 0 && _radialModel.Phase == RadialPhase.Open)
        {
            if (_mouse.CursorMode)
            {
                // cursor mode: the pointer aims the server's radial from the screen's centre (the captured mouse aims it by its movement)
                Vector2 pointer = GetViewport().GetMousePosition() - GetViewport().GetVisibleRect().Size / 2f;
                _radialModel.AimAt(pointer.X, pointer.Y);
            }
            int lit = _radialModel.Highlight;
            if (lit >= 0 && lit < _radialModel.Entries.Count && _radialModel.Entries[lit].ClickOnHover)
                PickServerRadial(); // tg's click_on_hover
        }
        _radial.UiScale = _options.UiScale;
        _radial.Opacity = _options.RadialOpacity;
        _radial.Refresh();
        UpdateFeedback(now, camera, aiming);
        RunShotScript(now);
        while (_pendingMoves.Count > 0 && _pendingMoves.Peek().At <= now)
        {
            Vector2 point = _pendingMoves.Dequeue().Point;
            // a drag's move: the way moved and the held button, as a mouse's has (the engine's own drag and drop of controls counts the way)
            MouseButtonMask mask = _scriptedDrag == MouseButton.Right ? MouseButtonMask.Right : _scriptedDrag == MouseButton.Middle ? MouseButtonMask.Middle : MouseButtonMask.Left;
            var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = point - _scriptedMouse, ScreenRelative = point - _scriptedMouse, ButtonMask = mask };
            _scriptedMouse = point;
            Input.WarpMouse(point);
            Input.ParseInputEvent(motion);
        }
        while (_pendingReleases.Count > 0 && _pendingReleases.Peek().At <= now)
        {
            var release = _pendingReleases.Dequeue();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = release.Button, Pressed = false, Position = release.Point, GlobalPosition = release.Point });
        }
    }

    private static readonly List<ProgressBarInfo> NoBars = new();

    private void UpdateFeedback(double now, Camera3D camera, bool aiming)
    {
        Vector2 view = GetViewport().GetVisibleRect().Size;
        _chatLines.TextScale = new HudMetrics(view.X, view.Y, _options.UiScale).TextScale; // scales with the HUD (the owner, 2026-10-18)
        _chatLines.ChatScale = _options.ChatLinesScale; // and its chat lines by their own share (the owner, 2026-11-19)
        _chatLines.Lines = ChatWindowOpen ? new List<ChatLine>() : _chat.Visible(now); // D252: the feed is hidden while the chat window is open
        _chatLines.Balloon = _balloon.Current(now, out float alpha);
        _chatLines.BalloonAlpha = alpha;
        string tip = null;
        if (aiming && _target.IsSet && _target.Kind != TargetKind.Screen && _radialModel.Phase != RadialPhase.Open)
        {
            string name = TargetName(_target);
            ContextHintsMessage h = HintsFor(_target);
            string lmb = h == null ? null : HintLabel(h, ClickButton.Left), rmb = h == null ? null : HintLabel(h, ClickButton.Right);
            if (name.Length > 0 || lmb != null || rmb != null)
                tip = name + (lmb != null ? "    [LMB] " + lmb : string.Empty) + (rmb != null ? "    [RMB] " + rmb : string.Empty);
        }
        _chatLines.Screentip = tip;
        List<ProgressBarInfo> bars = _feed.Replica.Images.Count > 0 ? ProgressBars.Of(_feed.Replica) : NoBars;
        _chatLines.PlaceBars(bars, _feed.Replica.OwnEntity, _hudLayer.ActionButtonOf);
    }

    // ---- the mouse ----

    private static ClickModifiers HeldModifiers()
    {
        var modifiers = ClickModifiers.None;
        // Ctrl and Shift with a click are bindings of their own (ui-overhaul.md section 3: Ctrl+left click is the Pull action's, Shift+left
        // click the Examine action's; OnMouseButton adds their modifier); only tg's Alt and Alt-Shift go with any click.
        if (Input.IsPhysicalKeyPressed(Key.Alt)) modifiers |= ClickModifiers.Alt;
        if (modifiers != ClickModifiers.None && Input.IsPhysicalKeyPressed(Key.Shift)) modifiers |= ClickModifiers.Shift;
        return modifiers;
    }

    // version 51: BYOND's DblClick after the second of two clicks on one thing (a ghost follows a mob, jumps to a turf)
    private readonly DoubleClick _doubleClick = new();

    // The owner (2026-11-13, D328): one key throws the held thing (or a mob held in an aggressive grab, as tg's throw does) at once where
    // the crosshair points, along the camera's aim (protocol 81's Throw action), with the throw pick's longer reach (the floor the crosshair
    // meets, as in throw mode).
    private void ThrowAtCrosshair()
    {
        if (_throwRay is not PickRay ray)
            return;
        PickHit hit = _throwModeOn ? _hit : _throwThird ? _picker.PickToRangeFrom(ray, _throwExclude, _throwLead + WorldPicker.ThrowRange) : _picker.PickThrow(ray, _feed.OwnEntity);
        SendThrow(ClickTarget.Of(hit));
    }

    // D328: the Throw action at a target; DV30: at a mob, aimed at the body part under the crosshair (tg's zone_selected)
    private void SendThrow(ClickTarget target)
    {
        if (target.Kind == TargetKind.Entity && target.IconX != 0)
            target = target with { Aim = _entities.AimZoneAt(target.Id, target.IconX, target.IconY) };
        if (!_session.Throw(target, _aim))
            return;
        AimedAtWorld();
        _viewModel?.LetGo(); // D285: a thrown thing is gone from the hand at once
        _crosshair.PulseNow(App.NowSeconds);
        _viewModel?.Clicked(); // D285: the held thing swings as it leaves the hand
    }

    private void SendClick(in ClickTarget clicked, ClickButton button, ClickModifiers modifiers, ShotAim? aim = null)
    {
        // D183: with an item in the active hand, a click on a limb of the health doll is a click on the own mob aimed at that limb
        ClickTarget target = DragModel.SelfItemClickOnDoll(clicked, modifiers, _viewModel?.HasItem == true, _hudLayer.IsDoll, _feed.OwnEntity,
            clicked.Kind == TargetKind.Screen ? _hudLayer.DollZoneAt(clicked.Id, clicked.IconX, clicked.IconY) : AimZone.None);
        ClickTarget aimed = target;
        // DV30: a click on a mob's picture aims at the body part under the crosshair (the server selects it as tg's zone_selected)
        if (target.Kind == TargetKind.Entity && target.IconX != 0)
            aimed = target with { Aim = _entities.AimZoneAt(target.Id, target.IconX, target.IconY) };
        // version 34 (DV94): a click on the world carries the camera's aim (a gun's shot flies along the crosshair)
        bool sent = _session.Click(aimed, button, modifiers, target.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling ? aim ?? _aim : null);
        if (sent)
        {
            double now = App.NowSeconds;
            if (_doubleClick.Clicked(target, button, now))
                _session.DblClick(target, button, modifiers);
            _crosshair.PulseNow(now);
            _highlight.PulseNow(now);
            if (button == ClickButton.Left && target.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling
                && _viewModel?.Clicked() is SwingSound.Whoosh whoosh) // D285: the held thing swings; DV586: in combat mode it whooshes
                _sound?.PlayOwn(whoosh.File, whoosh.Volume);
            else if (button == ClickButton.Right && target.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling)
                _viewModel?.RightClicked(); // DV361: a mech's right arm fires
            // a shot or a hit, not a swap of hands or a click on the own mob
            bool atWorld = target.Kind is TargetKind.Turf or TargetKind.Ceiling || target.Kind == TargetKind.Entity && target.Id != _feed.OwnEntity;
            if (button != ClickButton.Middle && atWorld)
                AimedAtWorld();
            // the hit's burst starts at the point the crosshair met the picture (the owner, 2026-10-10)
            if (target.Kind == TargetKind.Entity && _hit.Kind == TargetKind.Entity && _hit.Id == target.Id)
                _entities.RememberAimedHit(target.Id, new Vector3((float)_hit.HitEast, TileSpace.WorldY(_hit.Z) + (float)_hit.HitUp, -(float)_hit.HitNorth), now);
        }
        if (App.Instance.Args.Automated)
        {
            string what = target.Kind switch { TargetKind.Entity => "#" + target.Id, TargetKind.Screen => "screen #" + target.Id, TargetKind.Turf => $"turf {target.X},{target.Y},{target.Z}", _ => "nothing" };
            GD.Print($"[anomaly] click: {button}{(modifiers != ClickModifiers.None ? "+" + modifiers : string.Empty)} on {(sent ? what : "nothing")}{(sent && target.IconX > 0 ? $" icon {target.IconX},{target.IconY}" : string.Empty)}");
        }
    }

    // Left, middle or a right tap in the captured view: a click on what is under the crosshair; in cursor mode, on the HUD entry under the pointer.
    private void ClickAtCrosshair(ClickButton button, ClickModifiers modifiers)
    {
        if (button == ClickButton.Middle && (modifiers & ClickModifiers.Shift) != 0 && (!_mouse.CursorMode || !PointerHud().IsSet))
        {
            Camera3D camera = _rig.Camera;
            if (camera == null || !_session.Predicting) return;
            (float ox, float oy, int z) = _session.Prediction.RenderPosition();
            double lead = 0;
            PickRay ray = _rig.Mode == CameraMode.Orbit
                ? ThirdPersonRay(camera, z, _mouse.CursorMode, out lead)
                : MakeRay(camera, z, WorldPicker.ViewRange);
            if (_mouse.CursorMode && _rig.Mode != CameraMode.Orbit)
            {
                Vector2 pointer = GetViewport().GetMousePosition();
                Vector3 origin = camera.ProjectRayOrigin(pointer), direction = camera.ProjectRayNormal(pointer);
                ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, direction.X, -direction.Z, direction.Y, WorldPicker.ViewRange);
            }
            PickHit pointed = _picker.PickPoint(ray, _feed.OwnEntity, lead);
            if (pointed.IsHit && (!_fovOn || _fov.CellVisible(pointed.X, pointed.Y, _fov.Z)))
                // DV232: the aim from the eye through the point aimed at, where the server puts the arrow
                SendClick(ClickTarget.Of(pointed), button, modifiers, PlaySession.AimOf(_rig.Mode == CameraMode.Orbit ? EyeRay(ray, ox, oy, z, pointed) : ray));
            return;
        }
        if (_mouse.CursorMode)
        {
            PickHit hud = _hudPicker.PickScreen(GetViewport().GetMousePosition().X, GetViewport().GetMousePosition().Y);
            // Off the HUD a click in cursor mode is nothing, so nothing is shot or hit while the mouse is free for the HUD (the owner,
            // 2026-11-13; interaction.md 4.4); the Pull key ctrl-clicks the world pick there (DV422)
            if (!hud.IsHit)
            {
                // the owner (2026-11-14): through a camera eye (the AI's) the pointer clicks the world, as BYOND's mouse does
                PickHit world = _cameraEye ? PointerWorldPick(_rig.Camera, out _) : default;
                if (world.IsHit)
                    SendClick(ClickTarget.Of(world), button, modifiers);
                return;
            }
            SendClick(ClickTarget.Of(hud), button, modifiers);
            return;
        }
        if (!_target.IsSet)
        {
            if (button == ClickButton.Middle)
                _session.Keybinding(KeybindingCode.SwapHands); // tg's middle click is swap_hand(): with nothing under the crosshair it is the keybinding
            return;
        }
        SendClick(_target, button, modifiers);
    }

    private bool OrbitZoomable(bool shift) => _rig.Mode == CameraMode.Orbit && !shift
        && Input.MouseMode == Input.MouseModeEnum.Captured && _radialModel.Phase == RadialPhase.Closed;

    // A Mac trackpad sends no wheel buttons: two-finger scroll is a pan gesture, a pinch a magnify gesture
    private void OnTrackpadZoom(InputEvent gesture)
    {
        if (gesture is InputEventPanGesture pan && OrbitZoomable(pan.ShiftPressed))
            _rig.Zoom(pan.Delta.Y);
        else if (gesture is InputEventMagnifyGesture { Factor: > 0f } pinch && OrbitZoomable(pinch.ShiftPressed))
            _rig.Zoom(-Mathf.Log(pinch.Factor) / Mathf.Log(1.12f)); // pinch apart (factor > 1) moves the camera in
    }

    private void OnMouseButton(InputEventMouseButton button)
    {
        bool automated = App.Instance.Args.Automated;
        bool wheel = button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown;
        if (wheel && OrbitZoomable(button.ShiftPressed))
        {
            _rig.Zoom(button.ButtonIndex == MouseButton.WheelUp ? -1 : 1); // third-person play: the wheel zooms the view in and out
            return;
        }
        if (OnEmoteWheelMouse(button))
            return; // M9 slice B: the emote wheel's own
        if (wheel)
        {
            // version 70: BYOND's MouseWheel over the aimed thing (tg's RPD: the wheel picks the pipe layer, D320: Shift with it turns the next build)
            if (_session.Predicting && (Input.MouseMode == Input.MouseModeEnum.Captured || _mouse.CursorMode || automated) && _radialModel.Phase == RadialPhase.Closed && !PanelsOpen)
                _session.MouseWheel(_target, button.ButtonIndex == MouseButton.WheelUp, HeldModifiers() | (button.ShiftPressed ? ClickModifiers.Shift : ClickModifiers.None));
            return;
        }
        if (!button.Pressed)
        {
            if (button.ButtonIndex == MouseButton.Right)
                OnRightReleased(App.NowSeconds);
            else if (button.ButtonIndex == MouseButton.Left && _drag.Pressed)
                OnLeftReleased(button.Position);
            if (button.ButtonIndex == MouseButton.Left)
                _session.MouseUp(_target, HeldModifiers(), _aim); // version 34: BYOND's MouseUp (stops tg's full auto)
            return;
        }
        if (DevMenuHasMouse && TryDevToolClick(button))
        {
            GetViewport().SetInputAsHandled(); // the dev menu's map tool took it (GameScene.DevTools.cs)
            return;
        }
        if (Input.MouseMode != Input.MouseModeEnum.Captured && !automated && !_mouse.CursorMode)
            return; // D282: a panel has the pointer (SyncMouse captures it again when the panel closes)
        string name = button.ButtonIndex switch
        {
            MouseButton.Left => "MouseLeft",
            MouseButton.Right => "MouseRight",
            MouseButton.Middle => "MouseMiddle",
            _ => null,
        };
        PlayAction? action = name == null ? null : _options.Bindings.ActionOf(name);
        ClickModifiers modifiers = HeldModifiers();
        // ui-overhaul.md section 3 (SS14's ExamineEntity and TryPullObject): a button bound to Examine (Shift+left click) or Pull (Ctrl+left click)
        // is tg's left click with Shift or Ctrl, and with a modifier held on top of the binding (KeyBindings.ModifiedClick: Pull bound to Shift+left
        // click is tg's Ctrl-click), so it takes the left click's ways (cursor mode, the radial)
        (PlayAction? modified, bool ownShift, bool ownCtrl) = name == null || modifiers != ClickModifiers.None ? (null, false, false) : _options.Bindings.MatchPress(name, button.ShiftPressed, button.CtrlPressed);
        if (KeyBindings.TakesModifiedMouse(modified))
        {
            modifiers = KeyBindings.ModifiedClick(modified.Value, button.ShiftPressed && !ownShift, button.CtrlPressed && !ownCtrl);
            action = PlayAction.PrimaryClick;
        }
        // M8z (D245): Shift alone counts with the middle button, tg's shift-middle-click (pointing); Shift does not run with it
        if (action == PlayAction.MiddleClick && Input.IsPhysicalKeyPressed(Key.Shift))
            modifiers |= ClickModifiers.Shift;
        if (_radialModel.ServerMenu != 0 && _radialModel.Phase == RadialPhase.Open)
        {
            // Version 15: tg's radial stays open until answered: a left click picks the slice aimed at (the centre: the close button),
            // the right button closes it as the centre does.
            if (action == PlayAction.PrimaryClick)
                PickServerRadial();
            else if (action == PlayAction.SecondaryClick)
                CloseServerRadial();
            return;
        }
        if (_radialSticky && _radialModel.Phase == RadialPhase.Open)
        {
            // The pile list stays open: a left click chooses, anything else cancels.
            RadialOutcome sticky = action == PlayAction.PrimaryClick ? _radialModel.Confirm(out RadialEntry picked) : CancelSticky(out picked);
            _radialSticky = false;
            if (sticky == RadialOutcome.Choice)
                ExecuteChoice(picked, _radialTarget);
            return;
        }
        switch (action)
        {
            case PlayAction.PrimaryClick when _mouse.CursorMode && button.ButtonIndex == MouseButton.Left && _chatLines.LinkAt(button.Position) is string href:
                // Version 24 (D154): a link of tg's chat (the self-check's embedded object): BYOND's Topic of that link
                _session.Topic(href);
                GetViewport().SetInputAsHandled();
                break;
            case PlayAction.PrimaryClick when _mouse.CursorMode && button.ButtonIndex == MouseButton.Left:
                // Version 17: in cursor mode the left press may start a drag (of a HUD entry, an item in a slot or a world thing under the
                // pointer); its release is the click (on the HUD entry, as before) or the drop.
                ClickTarget source = PointerHud();
                if (!source.IsSet)
                    source = ClickTarget.Of(PointerWorldPick(_rig.Camera, out _));
                bool wallMount = source.Kind == TargetKind.Entity && _feed.Replica?.TryGet(source.Id, out ReplicaEntity pressed) == true && pressed.TypePath != null && Shared.World.PropKinds.IsWallMountType(pressed.TypePath);
                if (!_drag.Press(source, ClickButton.Left, modifiers, button.Position.X, button.Position.Y, App.NowSeconds, !wallMount))
                    ClickAtCrosshair(ClickButton.Left, modifiers); // nothing under the pointer: as before
                break;
            case PlayAction.PrimaryClick:
                // version 34: BYOND's MouseDown before the press's click (tg's full auto takes the press and drops the click)
                if (button.ButtonIndex == MouseButton.Left && _aim.HasValue)
                    _session.MouseDown(_target, ClickButton.Left, modifiers, _aim, App.NowSeconds);
                ClickAtCrosshair(ClickButton.Left, modifiers);
                break;
            case PlayAction.SecondaryClick:
                if (_mouse.CursorMode)
                    ClickAtCrosshair(ClickButton.Right, modifiers);
                else
                {
                    _rightTarget = _target;
                    _rightModifiers = modifiers;
                    _radialModel.Press(App.NowSeconds);
                }
                break;
            case PlayAction.MiddleClick:
                ClickAtCrosshair(ClickButton.Middle, modifiers);
                break;
        }
    }

    // ---- drag and drop (protocol version 17; docs/ai/specs/interaction.md section 8) ----

    // The HUD entry (or the item shown in a slot) under the pointer.
    private ClickTarget PointerHud()
    {
        Vector2 pointer = GetViewport().GetMousePosition();
        return ClickTarget.Of(_hudPicker.PickScreen(pointer.X, pointer.Y));
    }

    // The world thing or cell under the pointer: the pick along the camera's ray through the pointer (the crosshair's pick is the ray
    // through the screen's centre).
    private PickHit PointerWorldPick(Camera3D camera, out PickShape shape)
    {
        shape = null;
        if (camera == null || !_session.Predicting)
            return default;
        if (_rig.Mode == CameraMode.Orbit)
        {
            // third-person play: the pointer's ray from the camera, the ranges counted from the own mob, nothing out of its sight
            (_, _, int oz) = ViewerPosition();
            _pickWorld.PickExclude = 0;
            PickRay far = ThirdPersonRay(camera, oz, true, out double lead);
            PickHit found = _picker.Pick(far with { MaxMetres = lead + WorldPicker.DefaultRange }, 0);
            if (!found.IsHit)
                found = _picker.PickToRangeFrom(far, 0, lead + WorldPicker.ViewRange);
            if (_fovOn && found.IsHit && !_fov.CellVisible(found.X, found.Y, _fov.Z))
                return default;
            shape = found.IsHit ? _picker.LastShape : null;
            return found;
        }
        Vector2 pointer = GetViewport().GetMousePosition();
        Vector3 origin = camera.ProjectRayOrigin(pointer);
        Vector3 dir = camera.ProjectRayNormal(pointer);
        (_, _, int z) = _session.Prediction.RenderPosition();
        _pickWorld.PickExclude = _feed.OwnEntity;
        Vector3 view = (-camera.GlobalTransform.Basis.Z).Normalized(); // the camera's forward: an item's picture is square to it (PickShape.FacesEye)
        var ray = new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, dir.X, -dir.Z, dir.Y, WorldPicker.DefaultRange, view.X, -view.Z, view.Y);
        PickHit hit = _picker.Pick(ray, _feed.OwnEntity);
        shape = _picker.LastShape;
        return hit;
    }

    // A cursor-mode release clicks its pressed HUD/world target, or drops onto the current target after a drag.
    private void OnLeftReleased(Vector2 at)
    {
        _drag.Move(at.X, at.Y);
        ClickTarget over = DragModel.ChooseOver(PointerHud(), ClickTarget.Of(PointerWorldPick(_rig.Camera, out _)));
        if (_drag.Dragging)
            over = DragModel.SelfOnDoll(over, _hudLayer.IsDoll, _feed.OwnEntity);
        DragOutcome outcome = _drag.Release(over, App.NowSeconds, out DragRelease release);
        _dragIcon.Visible = false;
        switch (outcome)
        {
            case DragOutcome.Click:
                if (DragModel.ClickIsSent(release, _cameraEye))
                    SendClick(DragModel.SelfCtrlClickOnDoll(release.Source, release.Modifiers, _hudLayer.IsDoll, _feed.OwnEntity), release.Button, release.Modifiers);
                break;
            case DragOutcome.Drop:
                release = DragModel.SelfFromDoll(release, _hudLayer.IsDoll, _feed.OwnEntity); // DV417
                bool sent = _session.Drop(release);
                if (App.Instance.Args.Automated)
                    GD.Print($"[anomaly] drop: {Describe(release.Source)} onto {Describe(release.Over)}{(sent ? string.Empty : " (not sent)")} after {release.DragTime} ds");
                break;
        }
    }

    private static string Describe(in ClickTarget target) => target.Kind switch
    {
        TargetKind.Entity => "#" + target.Id,
        TargetKind.Screen => "screen #" + target.Id,
        TargetKind.Turf => $"turf {target.X},{target.Y},{target.Z}",
        _ => "nothing",
    };

    // The dragged thing's icon at the pointer while dragging (BYOND draws it there).
    private void UpdateDragIcon(double now)
    {
        if (_dragIcon == null)
            return;
        if (!_drag.Dragging || !_mouse.CursorMode)
        {
            _dragIcon.Visible = false;
            return;
        }
        TableRef icon = DragModel.IconOf(_feed.Replica, _drag.Source);
        if (icon.IsNone || _dragTextures == null || !_dragTextures.TryCompose(_feed.Replica, icon, 2, now, out ComposedAppearance composed) || composed.Texture == null)
        {
            _dragIcon.Visible = false;
            return;
        }
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float scale = new HudMetrics(view.X, view.Y, _options.UiScale).PixelScale; // the HUD's own pixel size, as a slot draws the item
        var size = new Vector2(composed.PixelWidth, composed.PixelHeight) * scale;
        Vector2 pointer = GetViewport().GetMousePosition();
        _dragIcon.Texture = composed.Texture;
        _dragIcon.Size = size;
        _dragIcon.Position = pointer - size / 2f;
        _dragIcon.Visible = true;
    }

    private RadialOutcome CancelSticky(out RadialEntry choice)
    {
        choice = default;
        _radialModel.Reset();
        return RadialOutcome.Cancel;
    }

    private void OnRightReleased(double now)
    {
        if (_radialSticky)
            return;
        RadialOutcome outcome = _radialModel.Release(now, out RadialEntry choice);
        switch (outcome)
        {
            case RadialOutcome.Tap:
                if (_rightTarget.IsSet)
                    SendClick(_rightTarget, ClickButton.Right, _rightModifiers);
                break;
            case RadialOutcome.Choice:
                ExecuteChoice(choice, _radialTarget);
                break;
        }
        _radial.Refresh();
    }

    // ---- the radial ----

    // Version 15 (DV40): tg's radial menus the server shows; the newest open one is drawn at the crosshair, sticky until answered.
    private void OnServerRadial(RadialMenuMessage changed)
    {
        RadialMenuMessage top = _feed.Replica.TopRadial;
        if (top != null)
        {
            _radialSticky = true;
            _radialModel.OpenServer(top);
            _radial.Title = string.Empty;
        }
        else if (_radialModel.ServerMenu != 0)
        {
            _radialModel.Reset();
            _radialSticky = false;
        }
        _radial.Refresh();
    }

    // The aimed slice of the server's radial is the player's pick (tg's slice Click); the centre is its close button's.
    private void PickServerRadial()
    {
        long menu = _radialModel.ServerMenu;
        bool closeButton = _radialModel.ServerCloseButton;
        RadialOutcome outcome = _radialModel.Confirm(out RadialEntry picked);
        _radialSticky = false;
        if (outcome == RadialOutcome.Choice)
            _session.RadialPick(menu, picked.Element);
        else if (outcome == RadialOutcome.Cancel && closeButton)
            _session.RadialPick(menu, 0);
        else if (_feed.Replica.TopRadial is RadialMenuMessage still)
        {
            // no close button: tg's menu stays until it is answered
            _radialSticky = true;
            _radialModel.OpenServer(still);
        }
        _radial.Refresh();
    }

    // Esc or the right button: the centre button (tg's close), when the menu has one.
    private bool CloseServerRadial()
    {
        if (_radialModel.ServerMenu == 0 || !_radialModel.ServerCloseButton)
            return false;
        _session.RadialPick(_radialModel.ServerMenu, 0);
        _radialModel.Reset();
        _radialSticky = false;
        _radial.Refresh();
        return true;
    }

    // The number keys 1 to 9 pick the server radial's slices by position.
    private bool OnServerRadialKey(InputEventKey key)
    {
        if (_radialModel.ServerMenu == 0 || _radialModel.Phase != RadialPhase.Open)
            return false;
        int number = key.PhysicalKeycode switch
        {
            Key.Key1 or Key.Kp1 => 1, Key.Key2 or Key.Kp2 => 2, Key.Key3 or Key.Kp3 => 3, Key.Key4 or Key.Kp4 => 4, Key.Key5 or Key.Kp5 => 5,
            Key.Key6 or Key.Kp6 => 6, Key.Key7 or Key.Kp7 => 7, Key.Key8 or Key.Kp8 => 8, Key.Key9 or Key.Kp9 => 9,
            _ => 0,
        };
        if (number == 0 || !_radialModel.SelectNumber(number))
            return false;
        PickServerRadial();
        return true;
    }

    private void OpenRadial()
    {
        _radialTarget = _rightTarget;
        ContextHintsMessage hints = HintsFor(_radialTarget);
        List<ReplicaEntity> pile = PileAt(_radialTarget);
        _radialModel.SetEntries(RadialModel.Build(hints, pile.Count, _radialTarget.Kind == TargetKind.Entity));
        _session.Rehover(); // version 36: fresh hints for what is possible now (OnHints)
        _radial.Title = _radialTarget.IsSet ? TargetName(_radialTarget) : "Nothing here";
    }

    // The things lying on the tile of the target: entities that are not mobs (a pile).
    private List<ReplicaEntity> PileAt(in ClickTarget target)
    {
        var pile = new List<ReplicaEntity>();
        if (!target.IsSet || target.Kind == TargetKind.Screen)
            return pile;
        int tx, ty, tz;
        if (target.Kind is TargetKind.Turf or TargetKind.Ceiling)
            (tx, ty, tz) = (target.X, target.Y, target.Z);
        else if (_feed.Replica.TryGet(target.Id, out ReplicaEntity e))
            (tx, ty, tz) = (e.State.X, e.State.Y, e.State.Z);
        else
            return pile;
        foreach (ReplicaEntity other in _feed.Replica.Entities)
        {
            if (!other.IsMob() && other.State.X == tx && other.State.Y == ty && other.State.Z == tz && other.Id != _feed.OwnEntity)
                pile.Add(other);
            if (pile.Count >= 10)
                break;
        }
        return pile;
    }

    private void ExecuteChoice(in RadialEntry choice, in ClickTarget target)
    {
        switch (choice.Kind)
        {
            case RadialKind.Hint:
            case RadialKind.Examine:
                SendClick(target, choice.Button, choice.Modifiers);
                break;
            case RadialKind.ThrowAt:
                SendThrow(target); // D328: one action, whatever the throw mode is
                break;
            case RadialKind.SwapHand:
                _session.Keybinding(KeybindingCode.SwapHands);
                break;
            case RadialKind.Sleep:
                _session.Keybinding(KeybindingCode.VerbSleep); // version 37 (D240)
                break;
            case RadialKind.Succumb:
                _session.Keybinding(KeybindingCode.VerbSuccumb);
                break;
            case RadialKind.DropSelf:
                // version 36 (D230): Buckle, tg's drag of oneself onto the thing (a long drag: never tg's quick drag as a click)
                if (_feed.OwnEntity != 0)
                    _session.Drop(new DragRelease(new ClickTarget(TargetKind.Entity, _feed.OwnEntity, 0, 0, 0, 16, 16), target, ClickButton.Left, ClickModifiers.None, 255));
                break;
            case RadialKind.StopPulling:
                _session.Keybinding(KeybindingCode.StopPulling);
                break;
            case RadialKind.DropOntoSelf:
                // version 37 (D238): Strip, tg's drag of the mob onto oneself (the doll in first person) opens his strip window
                if (_feed.OwnEntity != 0)
                    _session.Drop(new DragRelease(target, new ClickTarget(TargetKind.Entity, _feed.OwnEntity, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None, 255));
                break;
            case RadialKind.Extra:
                _session.ContextPick(target, choice.Label); // version 86 (DV503): the table's Flip
                break;
            case RadialKind.ThingsHere:
                var items = PileAt(target).Select(e => new RadialEntry(RadialKind.Item, string.IsNullOrEmpty(e.State.Name) ? "thing #" + e.Id : e.State.Name, ClickButton.Left, ClickModifiers.None, e.Id, Icon: e.State.Appearance)).ToList();
                items.Add(new RadialEntry(RadialKind.Cancel, "Cancel"));
                _radialSticky = true;
                _radialModel.OpenSticky(items);
                _radial.Title = "Things here";
                break;
            case RadialKind.Item:
                SendClick(new ClickTarget(TargetKind.Entity, choice.ItemId, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
                break;
        }
    }

    private float _dizzy;

    // D285: what moves the held things besides their own motions: the head bob, the own mob's shake (tg's jitter animates its pixel offsets) and
    // tg's dizziness wobble of the view
    private ViewModel.Holder HeldHolder()
    {
        double shakeX = 0, shakeY = 0;
        ReplicaWorld replica = _feed.Replica;
        if (replica.TryGet(_feed.OwnEntity, out ReplicaEntity me) && me.Animation != null && replica.TryGetAppearance(me.State.Appearance, out AppearanceEntry root)
            && Anomaly.Client.Shared.Icons.AnimationPlayback.TryEvaluate(me.Animation, _session.RenderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond,
                Anomaly.Client.Shared.Icons.AnimatedBase.Of(root), out Anomaly.Client.Shared.Icons.AnimatedLook look, double.NaN))
            (shakeX, shakeY) = (look.PixelX, look.PixelY);
        CameraLayer camera = _session.Camera;
        _dizzy = Mathf.MoveToward(_dizzy, camera.Wobbling ? 1f : 0f, (float)GetProcessDeltaTime() * 2f);
        GunKick kick = camera.Kick; // a fresh kick only: a gun with none (a mech's) kicks lightly
        HeldKick held = kick.SinceKick < HeldPose.KickFresh ? new HeldKick(kick.LastDegrees, kick.Heavy, kick.RollSide) : default;
        return new ViewModel.Holder(camera.BobAmount, camera.BobPhase, shakeX, shakeY, _dizzy, held);
    }

    // ---- keys ----

    private void OnInteractionKey(PlayAction? action)
    {
        if (action is PlayAction.CursorMode && _devMenuOpen && !_mouse.CursorMode)
        {
            ToggleDevMenuFocus(); // the owner (2026-11-10): the dev menu stays open, Tab gives the mouse to the game and back
            return;
        }
        if (action is PlayAction.CursorMode)
        {
            SetCursorMode(!_mouse.CursorMode);
            return;
        }
        if (action is PlayAction.Inventory)
        {
            ToggleInventory();
            return;
        }
        if (_rig.Mode == CameraMode.Free)
            return; // Q and E fly the free camera; the avatar stands still
        switch (action)
        {
            case PlayAction.DropItem:
                _viewModel.LetGo(); // D285: a dropped thing is gone from the hand at once
                // D329: put down at the crosshair's point (the server drops it at the feet out of reach); tg's drop key with nothing in the world there
                if (!_session.PutDown(ClickTarget.Of(_hit), _aim))
                    _session.Keybinding(KeybindingCode.DropItem);
                break;
            case PlayAction.ThrowMode:
                ThrowAtCrosshair();
                break;
            case PlayAction.SwapHand:
                _session.Keybinding(KeybindingCode.SwapHands);
                break;
            case PlayAction.ActivateInHand:
                _session.Keybinding(KeybindingCode.ActivateInhand);
                break;
            case PlayAction.QuickEquip:
                _session.Keybinding(KeybindingCode.QuickEquip);
                break;
            case PlayAction.HoldThrow:
                _session.Keybinding(KeybindingCode.HoldThrowMode);
                break;
            case PlayAction.StopPulling:
                _session.Keybinding(KeybindingCode.StopPulling);
                break;
            case PlayAction.Pull:
                // tg's Ctrl-click on what the crosshair points at, with a modifier held on top of the binding (KeyBindings.ModifiedClick), else stop pulling
                if (_target.IsSet)
                    SendClick(_target, ClickButton.Left, KeyBindings.ModifiedClick(PlayAction.Pull, _pressExtraShift, _pressExtraCtrl));
                else
                    _session.Keybinding(KeybindingCode.StopPulling);
                break;
            case PlayAction.Resist:
                _session.Keybinding(KeybindingCode.Resist); // version 37 (M8z): tg's resist keybinding
                break;
            case PlayAction.GiveItem:
                _session.Keybinding(KeybindingCode.GiveItem); // version 41 (M9 slice F): tg's give keybinding
                break;
            case PlayAction.ViewPetCommands:
                _session.Keybinding(KeybindingCode.ViewPetCommands); // version 59 (gaps C2): tg's view pet commands: held, hover a pet
                break;
            case PlayAction.Scoreboard:
                _session.Keybinding(KeybindingCode.Scoreboard); // version 93 (DV585): the deathmatch scoreboard, while held
                break;
            case >= PlayAction.ActionButton1 and <= PlayAction.ActionButton9:
                // the owner (2026-10-29): tg's click on the shown action button of that number
                long button = _hudLayer.ActionButtonNumber(action.Value - PlayAction.ActionButton1 + 1);
                if (button != 0)
                    _session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
                break;
            case PlayAction.Examine:
                // tg's shift-click on what is under the crosshair (ShiftClickOn -> examinate), with a modifier held on top of the binding
                ClickAtCrosshair(ClickButton.Left, KeyBindings.ModifiedClick(PlayAction.Examine, _pressExtraShift, _pressExtraCtrl));
                break;
        }
    }

    private void OnKeyUp(InputEventKey key)
    {
        string name = key.PhysicalKeycode.ToString();
        // a lone Ctrl or Shift let go with nothing else pressed: its press's action now (ModifierTap), unless a panel took the keys meanwhile
        if (_modifierTap.Release(name, KeyboardTaken || _rig.Mode == CameraMode.Free) is PlayAction tapped && !_console.IsOpen && !_optionsPanel.IsOpen && !SayOpen && !_escMenu.IsOpen)
        {
            (_pressExtraShift, _pressExtraCtrl) = (false, false); // the lone modifier is the binding itself
            RunKeyAction(tapped, name);
        }
        if (_console.IsOpen || _optionsPanel.IsOpen || _rig.Mode == CameraMode.Free)
            return;
        OnEmoteWheelKeyUp(key);
        if (_options.Bindings.Has(PlayAction.HoldThrow, key.PhysicalKeycode.ToString()))
            _session.Keybinding(KeybindingCode.HoldThrowMode, up: true);
        if (_options.Bindings.Has(PlayAction.MoveUpLevel, key.PhysicalKeycode.ToString()))
            _session.Keybinding(KeybindingCode.ZLevelUp, up: true); // DV422: lets go of the throw mode Space held for a mob that cannot change level
        if (_options.Bindings.Has(PlayAction.Resist, key.PhysicalKeycode.ToString()))
            _session.Keybinding(KeybindingCode.Resist, up: true); // tg's resist/up: the resist button unlit
        if (_options.Bindings.Has(PlayAction.ViewPetCommands, key.PhysicalKeycode.ToString()))
            _session.Keybinding(KeybindingCode.ViewPetCommands, up: true);
        if (_options.Bindings.Has(PlayAction.Scoreboard, key.PhysicalKeycode.ToString()))
            _session.Keybinding(KeybindingCode.Scoreboard, up: true);
    }

    // D292: the chat window's input or a text field of a tgui window has the keyboard
    private bool TextFieldTyping => (ChatTyping || _tguiHost?.Typing == true) && GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;

    // ui-overhaul.md section 5: a text field has the keyboard (the say window, the chat input, a tgui window's field, any LineEdit or TextEdit, the
    // dev menu's ImGui field): a lone Shift or Ctrl does nothing in the game (ModifierTap)
    private bool KeyboardTaken => SayOpen || _imgui?.WantsText == true || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit;

    // D292: the text field lets the keyboard go and the view takes the mouse; the windows stay open
    private void BackToGame()
    {
        GetViewport().GuiReleaseFocus();
        SetCursorMode(false);
    }

    // D292 (the owner, 2026-10-29): the cursor-mode key (Tab) or Esc in a text field goes back to the game, before the field takes it (the
    // chat switches its channel with Shift+Tab, DV224)
    public override void _Input(InputEvent @event)
    {
        // ModifierTap: any key or button pressed while a lone Ctrl or Shift waits cancels its action (seen here, before a window or field takes it)
        if (@event is InputEventKey { Pressed: true, Echo: false } pressed)
            _modifierTap.AnyPress(pressed.PhysicalKeycode.ToString());
        else if (@event is InputEventMouseButton { Pressed: true } clicked)
            _modifierTap.AnyPress("Mouse" + clicked.ButtonIndex);
        if (!_leaving && App.Instance.Playing && @event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F8 })
        {
            StopPlay(null); // the map editor's Play: F8 goes back to the editor, over every window and menu
            GetViewport().SetInputAsHandled();
            return;
        }
        // (in the map editor every key is the editor's: the push-to-talk key, N, was taken from its text fields, and the microphone started)
        if (!_leaving && !EditorOn && @event is InputEventKey talk && PushToTalkInput(talk))
            return; // D307
        // the fullscreen key (F11) works over every window and menu (a player's report: not in the options): taken here before the GUI and OnKey's early returns;
        // the options panel's key capture (a child, its _Input runs first) has already taken a key being bound
        if (!_leaving && @event is InputEventKey { Pressed: true, Echo: false } full && _options.Bindings.ActionOfPress(full.PhysicalKeycode.ToString(), full.ShiftPressed, full.CtrlPressed) == PlayAction.Fullscreen)
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_leaving || EditorOn || @event is not InputEventKey { Pressed: true, Echo: false, ShiftPressed: false } key
            || (key.PhysicalKeycode != Key.Escape && !_options.Bindings.Has(PlayAction.CursorMode, key.PhysicalKeycode.ToString())))
            return;
        if (TextFieldTyping)
        {
            BackToGame();
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode != Key.Escape && !PanelsOpen && GetViewport().GuiGetFocusOwner() is not (null or LineEdit or TextEdit))
            GetViewport().GuiReleaseFocus(); // a focused label (a browse() page's) would spend Tab on Godot's focus navigation: it goes on to OnKey
    }

    // D292: Esc closes the top window, a machine's first, then the open storage (tg's close button); false when none is open
    private bool CloseTopWindow() => _tguiWindows?.CloseTop() == true || CloseStorage();

    // D292: the close-windows key (Shift+Esc)
    private void CloseAllWindows()
    {
        _tguiWindows?.CloseAll();
        CloseStorage();
    }

    // The owner (2026-10-30): the inventory key is tg's click on the HUD's inventory toggle (screen/human/toggle, which sends the worn
    // slots); the panel shows in cursor mode, so opening it frees the mouse and closing it gives the mouse back to the view
    private void ToggleInventory()
    {
        ReplicaWorld replica = _feed.Replica;
        long toggle = replica.Screen.Values.Where(e => replica.TryGetAppearance(e.Appearance, out AppearanceEntry look)
            && replica.ResolveString(look.IconState) is "toggle" or "toggle_active").Select(e => e.Id).FirstOrDefault();
        bool opening = !_inventoryPanel.View.IsOpen || !_mouse.CursorMode;
        if (toggle != 0 && _inventoryPanel.View.IsOpen != opening)
            _session.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        SetCursorMode(opening);
    }

    private bool CloseStorage()
    {
        long close = _storageWindow?.CloseEntry ?? 0;
        return close != 0 && _session.Click(new ClickTarget(TargetKind.Screen, close, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
    }

    private void SetCursorMode(bool on)
    {
        if (_mouse.CursorMode == on)
            return;
        _mouse.Set(on);
        CursorModeChanged();
    }

    // Cursor mode was turned on or off (by Tab or by a window, MouseOwner); the pointer follows in SyncMouse.
    private void CursorModeChanged()
    {
        _radialModel.Reset();
        _radialSticky = false;
        _drag.Cancel(); // version 17: a drag ends with cursor mode (the captured view has none)
        if (_dragIcon != null)
            _dragIcon.Visible = false;
    }

    // ---- the scripted shot scenarios (ANOMALY_SHOT=hover|radial|hud|container|throw, with --fake-server; see godot/tools/README) ----

    private FakeServer FakeServerOrNull => (_feed as FakeFeed)?.Server;

    private void BuildShotScript()
    {
        string scenario = System.Environment.GetEnvironmentVariable("ANOMALY_SHOT");
        FakeServer server = FakeServerOrNull;
        if (string.IsNullOrEmpty(scenario) || server == null)
            return;
        void At(double seconds, Action what) => _shotSteps.Add((seconds, what));
        void Pick(long id, double seconds)
        {
            At(seconds, () => AimAtEntity(id));
            At(seconds + 0.45, () => ClickAtCrosshair(ClickButton.Left, ClickModifiers.None));
        }
        switch (scenario)
        {
            case "hover":
                At(0.8, () => AimAtEntity(server.ToolboxId));
                break;
            case "radial":
                At(0.8, () => AimAtEntity(server.ToolboxId));
                At(1.8, () => { _rightTarget = _target; _rightModifiers = ClickModifiers.None; _radialModel.Press(App.NowSeconds); });
                At(2.7, () => _radialModel.Motion(78, -45));
                break;
            case "hud":
                Pick(server.ToolboxId, 0.8);
                At(2.0, () => SetCursorMode(true));
                break;
            case "container":
                Pick(server.ToolboxId, 0.8);
                At(2.0, () => _session.Click(new ClickTarget(TargetKind.Screen, 2001, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
                break;
            case "throw":
                Pick(server.WelderId, 0.8);
                At(2.0, () => _session.Keybinding(KeybindingCode.ToggleThrowMode));
                At(2.5, () => AimAtPoint(PickGrid.Tile * (server.TileOfThing(server.ToolboxId).X - 0.5) + 0.0, PickGrid.Tile * (server.TileOfThing(server.ToolboxId).Y - 0.5) + 6.0, 0.0));
                At(3.0, () => ClickAtCrosshair(ClickButton.Left, ClickModifiers.None));
                break;
        }
        _shotSteps.Sort((a, b) => a.At.CompareTo(b.At));
        GD.Print($"[anomaly] shot scenario: {scenario} ({_shotSteps.Count} steps)");
    }

    private void RunShotScript(double now)
    {
        double t = now - _startedAt;
        while (_shotNext < _shotSteps.Count && t >= _shotSteps[_shotNext].At)
            _shotSteps[_shotNext++].Do();
    }

    // Turns the view to a point (east, north, up metres on the own level).
    private void AimAtPoint(double east, double north, double up)
    {
        Camera3D camera = _rig.Camera;
        if (camera == null)
            return;
        Vector3 eye = camera.GlobalPosition;
        double de = east - eye.X, dn = north - -eye.Z, du = up - eye.Y;
        _rig.Yaw = (float)Math.Atan2(-de, dn);
        _rig.Pitch = (float)Math.Atan2(du, Math.Sqrt(de * de + dn * dn));
    }

    private void AimAtEntity(long id)
    {
        if (!_feed.Replica.TryGet(id, out ReplicaEntity e))
            return;
        AimAtPoint(TileSpace.WorldX(e.State.X), -TileSpace.WorldZ(e.State.Y), TileSpace.WorldY(e.State.Z) + 0.2);
    }

    // D295: with something in the active hand but a gun (whose shots go through a hole), a look up through a hole aims at the empty
    // ceiling there, so rods or a floor tile build it without Alt; an empty hand or throw mode leave the hole as it is
    private bool HoldsACeilingTool()
    {
        HeldHandsView hands = HeldHands.Find(_feed.Replica);
        TableRef held = hands.LeftActive ? hands.Left : hands.Right;
        if (hands.Throwing || held.IsNone || !_feed.Replica.TryGetAppearance(held, out AppearanceEntry app)) return false;
        return !HeldHands.IsGunIcon(_feed.Replica.ResolveResource(app.Icon) ?? _feed.Replica.ResolveString(app.Icon));
    }
}
