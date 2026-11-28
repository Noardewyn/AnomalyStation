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
    private bool TguiOpen => _tguiWindows?.AnyShown == true; // slice 4D-1: a withheld strip window holds no pointer, as a withheld storage window does not
    private readonly InventoryExpander _inventoryExpander = new(); // slice 4A: keeps tg's inventory expanded on the human HUD
    private readonly RadialModel _radialModel = new();
    private readonly ChatFeed _chat = new();
    private readonly BalloonModel _balloon = new();
    private PickHit _hit;

    // Version 34 (DV94): the camera's aim this frame (the eye and the crosshair's direction), null when the view does not aim at the world.
    private ShotAim? _aim;
    private PickShape _hitShape;
    private ClickTarget _target;
    private readonly MouseOwner _mouse = new(); // D282: cursor mode and the pointer
    private bool _throwModeOn;
    private bool _radialSticky;
    // DV694: the inventory overlay (the inventory key), shown only while the cursor is free on the human HUD; Tab alone frees the cursor
    private bool _inventoryOpen;
    private bool InventoryShown => _inventoryOpen && _mouse.CursorMode && _hudLayer?.HumanHud == true;
    // DV694: the open sticky wheel is the inventory's right-click wheel on an item (_radialTarget), centred at the pointer; on the active hand's item
    private bool _inventoryMenu, _inventoryMenuHand;
    private ClickTarget _rightTarget, _radialTarget;
    private PickRay? _throwRay; // D328: the crosshair's ray this frame, for the one-key throw
    private bool _throwThird;
    private double _throwLead;
    private long _throwExclude;
    private ClickModifiers _rightModifiers;
    private readonly Queue<(double At, MouseButton Button, Vector2 Point)> _pendingReleases = new();
    // Protocol version 17 (drag and drop): the cursor mode's left press, drag and release, and the dragged icon under the pointer.
    private readonly DragModel _drag = new();
    private InvPlace _dragFrom; // slice 4B-2: the inventory item a cursor-mode press was on (Kind None: none), checked against each new message
    private bool _pressQuick; // slice 4B-2: that press was bound to the quick transfer
    private readonly InventoryNotice _inventoryNotice = new(); // slice 4B-2: the "inventory opened" notice, once per opening
    private (long Item, double At) _actedOn; // slice 4B-2: what the player last clicked or used in hand (a listed storage opening by it)
    private long _closedListed; // slice 4B-2: tg's close button of the listed storage last closed unseen
    private readonly StripOpenings _stripOpenings = new(); // slice 4D-1: each strip window that opens turns the Tab inventory on once
    private long _strippedDrag; // slice 4D-1: the mob last dragged onto the own mob (tg's way of opening its strip window), for telling the strip target
    private PopupMenu _stripMenu; // slice 4D-1: tg's alternate actions on a strip slot
    private (int Window, string Key, List<(string Action, string Label)> Offered) _stripMenuFor;
    // DV695: the first-person drag of oneself (a press on the own body held until the release); whether the own body can be shown at all
    private readonly SelfDrag _selfDrag = new();
    private bool _ownBodyShowable;
    private TextureRect _dragIcon;
    private IAppearanceTextures _dragTextures;
    private readonly Queue<(double At, Vector2 Point)> _pendingMoves = new();

    // the input script's radial=LABEL (slice T4c): when to aim the held radial at the entry of that label, just before its release
    private (double At, string Label)? _pendingRadial;
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
        // slice 4B-2: a storage the Tab inventory lists is shown in its right column, never in tg's window
        _storageWindow.Suppress = items => _session != null && InventoryRules.ListedWindow(items, _session.Storages.Storages,
            App.NowSeconds - _actedOn.At < InventoryRules.ActedOnSeconds ? _actedOn.Item : 0);
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
            // slice 4D-1: on the human HUD tg's strip window is shown by the Tab inventory's strip view, never as the native window
            Withhold = window => window.Interface == StripView.Interface && _hudLayer?.HumanHud == true,
        };
        // slice 4D-1: the strip view's right-click menu of tg's alternate actions on a slot (a small menu at the pointer, as the chat's radio menu)
        _stripMenu = new PopupMenu { Name = "StripMenu" };
        _stripMenu.IdPressed += OnStripMenuPicked;
        ui.AddChild(_stripMenu);
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
            _scene._storageWindow.Collect(bottomToTop);
        }
    }

    // ---- hover, hints, throw mode ----

    private void OnHints(ContextHintsMessage hints)
    {
        _session.HintsArrived(hints);
        // DV694: the answer to the hover of the inventory wheel's item brings its action buttons and tg's context entries
        if (_inventoryMenu && _radialModel.Phase == RadialPhase.Open && HintsFor(_radialTarget) == hints)
        {
            List<RadialEntry> entries = InventoryRules.MenuEntries(hints, _inventoryMenuHand);
            if (!entries.SequenceEqual(_radialModel.Entries))
                _radialModel.SetEntries(entries);
            if (KeyChips.ServerName(hints) is string named)
                _radial.Title = named;
            if (App.Instance.Args.Automated)
                GD.Print($"[anomaly] inventory wheel: {Describe(_radialTarget)} offers {string.Join(", ", entries.Select(e => "\"" + e.Label + "\""))}");
            return;
        }
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
    // (DV695: and a held drag of oneself ends, nothing sent: its release would never come)
    private void OnFocusLost()
    {
        _modifierTap.Drop();
        _selfDrag.Cancel();
    }

    // DV695: something other than the view takes the mouse: the emote wheel, a radial (the context one or the server's), the dev menu, a text field
    private bool SelfDragOverlay => _emoteWheel.IsOpen || _radialModel.Phase != RadialPhase.Closed || DevMenuHasMouse || TextFieldTyping;

    // The hints that belong to what is hovered now (the answer to an older hover is dropped).
    // DV382: what the own view looks from: a camera eye's drawn place (the AI's eye far from its core), else the own predicted mob
    private (float X, float Y, int Z) ViewerPosition() => _cameraEye ? (_eyePose.X, _eyePose.Y, (int)_eyePose.Z) : _session.Prediction.RenderPosition();

    private ClickTarget _hudHover; // the HUD entry the free pointer hovers (cursor mode), else none
    private TooltipMessage _hudTip;

    // T5b: what the world picks leave out: the own mob, unless its body is drawn for its own eye, where it is a target like anyone (LookDown.PickExclude)
    private long OwnPickExclude => LookDown.PickExclude(_feed.OwnEntity, _entities.OwnBodyInView);

    // tg's screentip of a hovered HUD entry (on_mouse_enter names it, with its click hints), drawn as the pointer's tooltip where tg's own
    // tooltip (openToolTip: action buttons, alerts) says nothing: the AI's and the cyborgs' HUD buttons have only their names (the owner, 2026-11-14)
    private TooltipMessage HudTooltip()
    {
        string text;
        if (_hudHover.Kind == TargetKind.Screen && _hudHover.Id == TabInventory.SpriteId)
        {
            // slice 4A: over the own sprite of the Tab inventory the prompt names the part under the pointer
            if (TabInventory.PartName(_hudLayer.SpriteZoneAt(_hudHover.IconX, _hudHover.IconY)) is not string part)
                return null;
            text = "<p>" + System.Net.WebUtility.HtmlEncode(part) + "</p>";
        }
        else if (LiveHud.IsOurs(_hudHover))
        {
            // slice 4B-2: an item in a storage panel's cell or a belt cell of the bar is named from the server's Storages message
            if (_hudLayer.ItemOf(_hudLayer.PlaceOf(_hudHover)).Name is not string item)
                return null;
            text = "<p>" + System.Net.WebUtility.HtmlEncode(item) + "</p>";
        }
        else if (!_hudHover.IsSet || HintsFor(_hudHover) is not ContextHintsMessage h || string.IsNullOrEmpty(h.Name))
            return null;
        else
            text = "<p>" + System.Net.WebUtility.HtmlEncode(h.Name) + "</p>"
                + string.Concat(KeyChips.Prompts(h, _options.Bindings).Select(line => "<p>[" + System.Net.WebUtility.HtmlEncode(line.Key) + "] " + System.Net.WebUtility.HtmlEncode(line.Text) + "</p>"));
        if (_hudTip?.Text != text)
            _hudTip = new TooltipMessage { Kind = TooltipKind.Show, Text = text, Theme = "default" };
        return _hudTip;
    }

    private ContextHintsMessage HintsFor(in ClickTarget target) => KeyChips.AnswerFor(_session.Hints, target);

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
        if (KeyChips.ServerName(HintsFor(target)) is string named)
            return named;
        if (target.Kind == TargetKind.Entity && _feed.Replica.TryGet(target.Id, out ReplicaEntity e) && !string.IsNullOrEmpty(e.State.Name))
            return e.State.Name;
        if (target.Kind == TargetKind.Ceiling)
            return Anomaly.Core.DmShim.I18n.T("Ceiling"); // the server names a ceiling only in reach (DV691)
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
        return Viewed(new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, forward.X, -forward.Z, forward.Y, maxMetres), camera);
    }

    // The camera's forward and right on a ray (PickShape.FacesEye: an item's picture is square to the view, its right the camera's). T5a: past
    // straight down the ray's own ground track points back, and a right taken from it would turn such a picture round.
    private static PickRay Viewed(in PickRay ray, Camera3D camera)
    {
        Basis b = camera.GlobalTransform.Basis;
        Vector3 view = (-b.Z).Normalized();
        return ray with { ViewEast = view.X, ViewNorth = -view.Z, ViewUp = view.Y, RightEast = b.X.X, RightNorth = -b.X.Z };
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
        // DV695: the view lost (a window, death, a stun that lays the body down, a body swap), something else taking the mouse, or the left
        // button let go where the release never reached OnSelfDragReleased: the drag of oneself ends, nothing sent
        if (_selfDrag.Lost(_feed.OwnEntity, firstPerson, _mouse.CursorMode, panels, _ownBodyShowable, SelfDragOverlay, Input.IsMouseButtonPressed(MouseButton.Left)))
            _selfDrag.Cancel();
        bool pointerAim = (firstPerson || thirdPerson) && _mouse.CursorMode && !panels && !_drag.Dragging && !PointerHud().IsSet;
        bool aiming = (firstPerson || thirdPerson) && !_mouse.CursorMode && !panels || pointerAim;
        _hit = default;
        _hitShape = null;
        bool reach = false;
        if (aiming)
        {
            (float ox, float oy, int z) = ViewerPosition();
            long exclude = thirdPerson && pointerAim ? 0 : OwnPickExclude;
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
                    ray = Viewed(new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, dir.X, -dir.Z, dir.Y, WorldPicker.DefaultRange), camera);
                }
                _hit = _throwModeOn ? _picker.PickThrow(ray, exclude) : _picker.Pick(ray, exclude);
                // Nothing within the ordinary range (open space, a long corridor): the click lands on what is in view farther on, as BYOND's
                // screen always has a turf under the mouse (a ranged item's target: the extinguisher's spray and its push in zero gravity).
                if (!_hit.IsHit && !_throwModeOn)
                    _hit = _picker.PickInView(ray, exclude);
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
        // version 100 also (slice T4b): the hover on a mob carries the body zone looked at (the cached zone map), so the hints name what a click does;
        // not in cursor mode, where tg's zone selector holds the player's choice
        if (PlaySession.HoverCarriesZone(hover, _mouse.CursorMode))
            hover = hover with { Aim = _entities.AimZoneAt(hover.Id, hover.IconX, hover.IconY, hover.Picture) };
        // Protocol version 14: in cursor mode the pointer hovers the HUD entry under it, as BYOND's mouse over the screen does (tg's hover
        // states: the suit display's percentage, an inventory slot's outline, the action buttons' names), and leaving it is an unhover.
        if (_mouse.CursorMode && _session.Predicting && !PanelsOpen && !pointerAim)
            hover = ClickTarget.Of(_hudPicker.PickScreen(GetViewport().GetMousePosition().X, GetViewport().GetMousePosition().Y));
        // DV694: while the inventory wheel is open its item stays hovered (the server's hints for it fill the wheel), and no tooltip shows over it
        bool menu = _inventoryMenu && _radialModel.Phase == RadialPhase.Open;
        if (menu)
            hover = _radialTarget;
        _session.Hover(hover, now);
        _hudHover = hover.Kind == TargetKind.Screen && !menu ? hover : default;
        _crosshair.Visible = (firstPerson || thirdPerson) && !PanelsOpen && !_mouse.CursorMode && !_selfDrag.Holding; // DV695: the drag mark replaces it
        _crosshair.Set(_hit.Kind == TargetKind.Entity, reach);
        GunSightMessage sight = _shotGunSight ?? _feed.GunSight; // DV96: the shooter's crosshair while a gun is held (a shot scenario's own, below)
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
        // slice 4B-2: a drag of an inventory item ends, nothing sent, once a message took the item from where it was pressed (gone, moved, its
        // storage gone); while it goes on, the cell under the pointer has a light rim
        if (_drag.Pressed && _dragFrom.Item != 0 && !InventoryRules.DragHolds(_dragFrom, _hudLayer.PlaceOf(_drag.Source)))
        {
            if (App.Instance.Args.Automated)
                GD.Print($"[anomaly] drag: item #{_dragFrom.Item} left its place, the drag ends");
            _drag.Cancel();
            _dragFrom = default;
        }
        _hudLayer.DragOver = _drag.Dragging && _dragFrom.Item != 0 ? PointerHud() : default;
        UpdateDragIcon(now);

        Vector2 size = GetViewport().GetVisibleRect().Size;
        var metrics = new HudMetrics(size.X, size.Y, _options.UiScale);
        _viewModel.Update(now, _handsShown, _feed.OwnEntity, HeldHolder());
        // slice 4C-2: tg's storage window is never shown on the human HUD: the Tab inventory shows the storage open (the other container's panel)
        _storageWindow.Withheld = _hudLayer.HumanHud;
        _storageWindow.Update(now, metrics);
        // slice 4B-2: a storage the column lists opened by tg's click (the worn bag clicked, a held box used): the Tab inventory comes on where it is
        // and tg's storage is closed unseen, once per opening
        long listed = _storageWindow.Suppressed ? _storageWindow.OpenCloseEntry : 0;
        if (_storageWindow.OpenCloseEntry == 0)
            _closedListed = 0;
        else if (listed != 0 && listed != _closedListed)
        {
            _closedListed = listed;
            OpenInventoryFor();
            _session.Click(new ClickTarget(TargetKind.Screen, listed, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
            if (App.Instance.Args.Automated)
                GD.Print($"[anomaly] storage window: a listed storage opened by tg's click, shown in the Tab inventory (tg's window closed, screen #{listed})");
        }
        // slice 4C-2: the Storages message brought an other container where the last had none: the Tab inventory turns on, as tg's window appearing did
        // (the flag is taken only on the human HUD, so a container still open at join turns it on once that HUD is up)
        if (InventoryRules.TabOnForOther(_hudLayer.HumanHud, _session?.Storages))
        {
            OpenInventoryFor();
            if (App.Instance.Args.Automated)
                GD.Print($"[anomaly] storage window: the other container #{_hudLayer.OtherStorage} opened, shown in the Tab inventory");
        }
        // slice 4D-1: a strip window that opened turns the Tab inventory on, once per window (taken only on the human HUD, as the other container's)
        if (_stripOpenings.TakeNew(_tguiWindows.Open.Where(w => w.Interface == StripView.Interface).Select(w => w.Index), _hudLayer.HumanHud))
        {
            OpenInventoryFor();
            if (App.Instance.Args.Automated)
                GD.Print("[anomaly] strip: tg's strip window opened, shown in the Tab inventory");
        }

        if (_radialModel.Phase == RadialPhase.Pressed && _radialModel.Update(now))
            OpenRadial();
        // DV694: the inventory wheel is aimed by the free pointer from its centre; it ends when the wheel closes (a pick, a cancel, cursor mode off)
        if (_inventoryMenu && (!_radialSticky || _radialModel.Phase != RadialPhase.Open || _radialModel.ServerMenu != 0 || !_mouse.CursorMode))
            EndInventoryMenu();
        else if (_inventoryMenu && _radial.Centre is Vector2 wheel)
        {
            Vector2 aim = GetViewport().GetMousePosition() - wheel;
            _radialModel.AimAt(aim.X, aim.Y);
        }
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
        UpdateFeedback(now, camera, aiming, pointerAim);
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
        if (_pendingRadial is { } radial && radial.At <= now)
        {
            _pendingRadial = null;
            string offered = string.Join(", ", _radialModel.Entries.Select(e => "\"" + e.Label + "\""));
            GD.Print(_radialModel.AimAtLabel(radial.Label)
                ? $"[anomaly] radial: picked \"{radial.Label}\" on {Describe(_radialTarget)}"
                : $"[anomaly] radial: \"{radial.Label}\" not offered on {Describe(_radialTarget)}{(_radialModel.Phase == RadialPhase.Open ? string.Empty : " (the radial is not open)")}; offered: {(offered.Length > 0 ? offered : "none")}");
        }
        while (_pendingReleases.Count > 0 && _pendingReleases.Peek().At <= now)
        {
            var release = _pendingReleases.Dequeue();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = release.Button, Pressed = false, Position = release.Point, GlobalPosition = release.Point });
        }
    }

    private static readonly List<ProgressBarInfo> NoBars = new();
    private static readonly List<PromptLine> NoPrompts = new();

    private void UpdateFeedback(double now, Camera3D camera, bool aiming, bool pointerAim)
    {
        Vector2 view = GetViewport().GetVisibleRect().Size;
        var metrics = new HudMetrics(view.X, view.Y, _options.UiScale);
        _chatLines.Metrics = metrics;
        // ui-overhaul.md section 6: the prompts stand beside the crosshair, or beside the free pointer when it aims at the world (cursor mode)
        _chatLines.PromptAnchor = pointerAim ? GetViewport().GetMousePosition() : view / 2f;
        _chatLines.PromptReach = pointerAim ? KeyChips.PointerReach : _crosshair.Extent;
        // beside the crosshair they end before the vitals and their chips, their text cut (the free pointer's block keeps its own room)
        _chatLines.PromptRightEdge = pointerAim || _hudLayer == null ? float.PositiveInfinity : _hudLayer.PanelLeftEdge;
        _chatLines.TextScale = metrics.TextScale; // scales with the HUD
        _chatLines.ChatScale = _options.ChatLinesScale; // and its chat lines by their own share (the owner, 2026-11-19)
        _chatLines.BottomRow = !InLobby; // ui-overhaul.md section 13: in the round the lines keep clear of the hands' row and the hint strip
        _chatLines.Lines = ChatWindowOpen ? new List<ChatLine>() : _chat.Visible(now); // D252: the feed is hidden while the chat window is open
        _chatLines.Balloon = _balloon.Current(now, out float alpha);
        _chatLines.BalloonAlpha = alpha;
        string name = null;
        List<PromptLine> prompts = NoPrompts;
        if (_selfDrag.Holding)
            // DV695: while dragging oneself, only the name of a thing in reach under the crosshair (DV691), no action lines
            name = SelfDrag.PromptName(_target, aiming ? KeyChips.PromptName(HintsFor(_target), _feed is not FakeFeed, TargetName(_target)) : null);
        else if (aiming && _target.IsSet && _target.Kind != TargetKind.Screen && _radialModel.Phase != RadialPhase.Open)
        {
            ContextHintsMessage answer = HintsFor(_target);
            string targetName = KeyChips.PromptName(answer, _feed is not FakeFeed, TargetName(_target)) ?? "";
            List<PromptLine> lines = KeyChips.Prompts(answer, _options.Bindings);
            if (targetName.Length > 0 || lines.Count > 0)
                (name, prompts) = (targetName, lines);
        }
        _chatLines.PromptName = name;
        _chatLines.Prompts = prompts;
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
        PickHit hit = _throwModeOn ? _hit : _throwThird ? _picker.PickToRangeFrom(ray, _throwExclude, _throwLead + WorldPicker.ThrowRange) : _picker.PickThrow(ray, _throwExclude);
        SendThrow(ClickTarget.Of(hit));
    }

    // D328: the Throw action at a target; DV30: at a mob, aimed at the body part under the crosshair (tg's zone_selected)
    private void SendThrow(ClickTarget target)
    {
        if (target.Kind == TargetKind.Entity && target.IconX != 0)
            target = target with { Aim = _entities.AimZoneAt(target.Id, target.IconX, target.IconY, target.Picture) };
        if (!_session.Throw(target, _aim))
            return;
        AimedAtWorld();
        _viewModel?.LetGo(); // D285: a thrown thing is gone from the hand at once
        _crosshair.PulseNow(App.NowSeconds);
        _viewModel?.Clicked(); // D285: the held thing swings as it leaves the hand
    }

    private void SendClick(in ClickTarget clicked, ClickButton button, ClickModifiers modifiers, ShotAim? aim = null)
    {
        // D183: with an item in the active hand, a click on a limb of the health doll is a click on the own mob aimed at that limb; slice 4A: a click on
        // the own sprite of the Tab inventory is one on the doll, or on the own mob at the part of the sprite clicked
        bool holds = _viewModel?.HasItem == true;
        ClickTarget target = clicked.Kind == TargetKind.Screen && clicked.Id == TabInventory.SpriteId
            ? TabInventory.SelfClick(clicked, modifiers, holds, _feed.OwnEntity, _hudLayer.DollEntry, _hudLayer.SpriteZoneAt(clicked.IconX, clicked.IconY))
            : DragModel.SelfItemClickOnDoll(clicked, modifiers, holds, _hudLayer.IsDoll, _feed.OwnEntity,
                clicked.Kind == TargetKind.Screen ? BodyZoneAim.DollFaceAt(_hudLayer.DollZoneAt(clicked.Id, clicked.IconX, clicked.IconY), clicked.IconX, clicked.IconY) : AimZone.None);
        ClickTarget aimed = target;
        // DV30: a click on a mob's picture aims at the body part under the crosshair (the server selects it as tg's zone_selected)
        if (target.Kind == TargetKind.Entity && target.IconX != 0)
            aimed = target with { Aim = _entities.AimZoneAt(target.Id, target.IconX, target.IconY, target.Picture) };
        // version 34 (DV94): a click on the world carries the camera's aim (a gun's shot flies along the crosshair)
        bool sent = _session.Click(aimed, button, modifiers, target.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling ? aim ?? _aim : null);
        if (sent)
        {
            // slice 4C-2: tg's way of opening the container already open as the other container shows it again: the Tab inventory turns on
            if (InventoryRules.ReopensOther(button, modifiers, target.Kind == TargetKind.Entity ? target.Id : 0, _hudLayer.OtherStorage, InventoryShown, _hudLayer.HumanHud))
                ReopenOther();
            double now = App.NowSeconds;
            // slice 4B-2: what was clicked (the item in a clicked hand box or slot), for telling a listed storage's window opening by it
            InvPlace clickedPlace = _hudLayer.PlaceOf(target);
            _actedOn = (clickedPlace.Item != 0 ? clickedPlace.Item : target.Id, now);
            if (_doubleClick.Clicked(target, button, now))
                _session.DblClick(target, button, modifiers);
            _crosshair.PulseNow(now);
            _highlight.PulseNow(now);
            bool onItem = target.Kind == TargetKind.Entity && _feed.Replica?.TryGet(target.Id, out ReplicaEntity clickedThing) == true
                && clickedThing.TypePath != null && Shared.World.PropKinds.IsItem(clickedThing.TypePath);
            if (button == ClickButton.Left && target.Kind is TargetKind.Entity or TargetKind.Turf or TargetKind.Ceiling
                && _viewModel?.Clicked(onItem) is SwingSound.Whoosh whoosh) // D285: the held thing swings; DV586: in combat mode it whooshes
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
                ray = Viewed(new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, direction.X, -direction.Z, direction.Y, WorldPicker.ViewRange), camera);
            }
            PickHit pointed = _picker.PickPoint(ray, OwnPickExclude, lead);
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
        if (_selfDrag.Holding && SelfDragOverlay)
            _selfDrag.Cancel(); // DV695: an overlay that takes this button ends the drag of oneself before it does, nothing sent
        if (OnEmoteWheelMouse(button))
            return; // M9 slice B: the emote wheel's own
        if (wheel && _mouse.CursorMode && _hudLayer.ScrollColumn(button.Position.X, button.Position.Y, button.ButtonIndex == MouseButton.WheelUp ? -1 : 1))
            return; // slice 4B-2: the wheel over the Tab inventory's right column scrolls its panels
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
            else if (button.ButtonIndex == MouseButton.Left && _selfDrag.Holding)
                OnSelfDragReleased();
            if (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Middle && _session.MouseHeld == ClickButton.Middle)
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
        if (_selfDrag.Holding && button.ButtonIndex == MouseButton.Right)
        {
            _selfDrag.Cancel(); // DV695: the right button drops the drag of oneself, and is nothing more
            return;
        }
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
            // The pile list stays open: a left click chooses, anything else cancels. DV694: so does the inventory wheel, aimed at the click's point.
            if (_inventoryMenu && _radial.Centre is Vector2 wheelAt)
                _radialModel.AimAt(button.Position.X - wheelAt.X, button.Position.Y - wheelAt.Y);
            RadialOutcome sticky = action == PlayAction.PrimaryClick ? _radialModel.Confirm(out RadialEntry picked) : CancelSticky(out picked);
            _radialSticky = false;
            if (_inventoryMenu && App.Instance.Args.Automated)
                GD.Print(sticky == RadialOutcome.Choice ? $"[anomaly] inventory wheel: picked \"{picked.Label}\" on {Describe(_radialTarget)}" : "[anomaly] inventory wheel: closed, nothing picked");
            if (_inventoryMenu)
                EndInventoryMenu();
            if (sticky == RadialOutcome.Choice)
                ExecuteChoice(picked, _radialTarget);
            return;
        }
        // slice 4E (SS14's MovePulledObject): the right button bound with a modifier moves the pulled thing with the cursor captured; in cursor mode
        // the press stays the right click (the inventory's item menu)
        if (modified == PlayAction.MovePulled && !_mouse.CursorMode)
        {
            MovePulledAtCrosshair();
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
                // slice 4B-2: a press on an item of the Tab inventory (its drag follows InventoryRules.Drop), and whether it is the quick transfer's
                InvPlace pressedPlace = source.Kind == TargetKind.Screen ? _hudLayer.PlaceOf(source) : default;
                _dragFrom = pressedPlace.Kind is InvKind.Hand or InvKind.Belt or InvKind.Slot or InvKind.Cell or InvKind.Nearby or InvKind.Strip && pressedPlace.Item != 0 ? pressedPlace : default;
                _pressQuick = InventoryRules.IsQuickTransfer(_options.Bindings, KeyBindings.NameOf(name, button.ShiftPressed, button.CtrlPressed), button.AltPressed);
                if (!_drag.Press(source, ClickButton.Left, modifiers, button.Position.X, button.Position.Y, App.NowSeconds, !wallMount))
                    ClickAtCrosshair(ClickButton.Left, modifiers); // nothing under the pointer: as before
                break;
            case PlayAction.PrimaryClick when button.ButtonIndex == MouseButton.Left && _selfDrag.Press(_target, _feed.OwnEntity, _mouse.CursorMode, modifiers, App.NowSeconds):
                // DV695: a plain press on the own body sends nothing (no MouseDown either, so no full auto or held use): the release decides
                break;
            case PlayAction.PrimaryClick:
                // version 34: BYOND's MouseDown before the press's click (tg's full auto takes the press and drops the click)
                if (button.ButtonIndex == MouseButton.Left && _aim.HasValue)
                    _session.MouseDown(_target, ClickButton.Left, modifiers, _aim, App.NowSeconds);
                ClickAtCrosshair(ClickButton.Left, modifiers);
                break;
            case PlayAction.SecondaryClick:
                if (_mouse.CursorMode && OpenStripMenu(button.Position))
                    break; // slice 4D-1: the strip panel's own right click
                if (_mouse.CursorMode && OpenInventoryMenu(button.Position, modifiers))
                    break; // DV694: on an inventory item the wheel of what can be done with it, not tg's right click (which took it to the hand)
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
                // BYOND's MouseDown of the middle press too: a selected MOD module's dig (the drill's) runs while the button is held, as a pick's (DV348, DV354)
                if (button.ButtonIndex == MouseButton.Middle && _aim.HasValue && (modifiers & ClickModifiers.Shift) == 0 && !_mouse.CursorMode)
                    _session.MouseDown(_target, ClickButton.Middle, modifiers, _aim, App.NowSeconds);
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
        _pickWorld.PickExclude = OwnPickExclude;
        PickRay ray = Viewed(new PickRay(origin.X, -origin.Z, origin.Y - TileSpace.WorldY(z), z, dir.X, -dir.Z, dir.Y, WorldPicker.DefaultRange), camera);
        PickHit hit = _picker.Pick(ray, OwnPickExclude);
        shape = _picker.LastShape;
        return hit;
    }

    // A cursor-mode release clicks its pressed HUD/world target, or drops onto the current target after a drag.
    private void OnLeftReleased(Vector2 at)
    {
        _drag.Move(at.X, at.Y);
        ClickTarget over = DragModel.ChooseOver(PointerHud(), ClickTarget.Of(PointerWorldPick(_rig.Camera, out _)));
        bool dragged = _drag.Dragging;
        if (dragged)
            over = DragModel.SelfOnDoll(over, _hudLayer.IsDoll, _feed.OwnEntity);
        (InvPlace from, bool quick) = (_dragFrom, _pressQuick);
        (_dragFrom, _pressQuick) = (default, false);
        _hudLayer.DragOver = default;
        DragOutcome outcome = _drag.Release(over, App.NowSeconds, out DragRelease release);
        _dragIcon.Visible = false;
        // slice 4B-2: a drag of an item of the Tab inventory, released over anything (nothing at all is the floor): one move, tg's drop, a use on oneself or nothing
        if (dragged && from.Item != 0)
        {
            SendInventory(InventoryRules.Drop(from, _hudLayer.PlaceOf(over), _hudLayer.ActiveHand, _hudLayer.StripShown), release);
            return;
        }
        // slice 4B-2: a click on a storage panel, and the quick transfer on an item of the inventory; anything else stays what it was
        if (outcome == DragOutcome.Click && release.Source.Kind == TargetKind.Screen)
        {
            InvPlace place = _hudLayer.PlaceOf(release.Source);
            InvSend? send = quick ? InventoryRules.QuickTransfer(place, _hudLayer.ActiveHand, _hudLayer.BackStorage, _hudLayer.OtherStorage)
                : InventoryRules.Click(place, release.Modifiers, _hudLayer.ActiveHand, _hudLayer.HeldItem, _hudLayer.StripShown);
            if (send is InvSend inventory)
            {
                SendInventory(inventory, release);
                return;
            }
        }
        switch (outcome)
        {
            case DragOutcome.Click:
                if (DragModel.ClickIsSent(release, _cameraEye))
                    SendClick(DragModel.SelfCtrlClickOnDoll(release.Source, release.Modifiers, _hudLayer.IsDoll, _feed.OwnEntity,
                        release.Source.Kind == TargetKind.Screen ? _hudLayer.DollZoneAt(release.Source.Id, release.Source.IconX, release.Source.IconY) : AimZone.None), release.Button, release.Modifiers);
                break;
            case DragOutcome.Drop:
                SendDrop(DragModel.SelfFromDoll(release, _hudLayer.IsDoll, _feed.OwnEntity)); // DV417
                break;
        }
    }

    // DV695: the first-person drag of oneself let go: on the own body the click with the press's zone, on a thing the drop of the own mob onto it
    // (unless the server's answer to the hover says it is out of reach, DV691), on anything else nothing.
    private void OnSelfDragReleased()
    {
        bool outOfReach = HintsFor(_target)?.Name is { Length: 0 };
        switch (_selfDrag.Release(_target, _feed.OwnEntity, outOfReach, App.NowSeconds, out DragRelease release))
        {
            case SelfDragOutcome.Click:
                SendClick(release.Source, release.Button, release.Modifiers);
                break;
            case SelfDragOutcome.Drop:
                SendDrop(release);
                break;
            default:
                if (App.Instance.Args.Automated)
                    GD.Print($"[anomaly] drop: yourself onto {Describe(_target)}{(outOfReach ? " out of reach" : string.Empty)}: nothing sent");
                break;
        }
    }

    // Slice 4B-2: what the Tab inventory's rules chose for a drag, a click or the quick transfer (InventoryRules): the server decides every move.
    private void SendInventory(in InvSend send, in DragRelease release)
    {
        switch (send.Kind)
        {
            case InvSendKind.Move:
                bool sent = _session.InventoryMove(send.Item, send.To, send.Storage, send.Index);
                if (App.Instance.Args.Automated)
                    GD.Print($"[anomaly] move: item #{send.Item} to {send.To}{(send.Storage != 0 ? " #" + send.Storage : string.Empty)}{(send.Index != 0 ? " index " + send.Index : string.Empty)}{(sent ? string.Empty : " (not sent)")}");
                break;
            case InvSendKind.TgDrop:
                SendDrop(release);
                break;
            case InvSendKind.SelfUse:
                SendClick(release.Over, ClickButton.Left, ClickModifiers.None);
                break;
            case InvSendKind.Examine:
                // tg's Shift+click on a stored item: the server takes it for an item its own carried storages show (DV696)
                SendClick(new ClickTarget(TargetKind.Screen, send.Item, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.Shift);
                break;
            case InvSendKind.WorldClick:
                // slice 4C-2: an item of the Nearby strip: tg's click on it where it lies, as the world's click on it (tg decides reach)
                SendClick(new ClickTarget(TargetKind.Entity, send.Item, 0, 0, 0, 16, 16), ClickButton.Left, send.Modifiers);
                break;
            case InvSendKind.StripUse:
            case InvSendKind.StripClose:
                // slice 4D-1: tg's own act on the strip window shown (its slot button's `use`), or the close its own X sends
                Anomaly.Client.Shared.Tgui.TguiWindow strip = _hudLayer.StripShown is StripModel shown ? _tguiWindows.Get(shown.Window) : null;
                string key = send.Index >= 0 && send.Index < StripView.Keys.Length ? StripView.Keys[send.Index] : null;
                if (strip?.Context == null || strip.Interface != StripView.Interface || send.Kind == InvSendKind.StripUse && key == null)
                    break;
                if (send.Kind == InvSendKind.StripClose)
                    _tguiWindows.Close(strip);
                else
                    strip.Context.Act("use", ("key", key));
                if (App.Instance.Args.Automated)
                    GD.Print(send.Kind == InvSendKind.StripClose ? $"[anomaly] strip: window {strip.Index} closed" : $"[anomaly] strip: use {key} (window {strip.Index})");
                break;
            case InvSendKind.StripExamine:
                // slice 4D-1: the world's Shift+click on the strip target, where the client can tell which mob it is
                if (_hudLayer.StripTarget != 0)
                    SendClick(new ClickTarget(TargetKind.Entity, _hudLayer.StripTarget, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.Shift);
                else if (App.Instance.Args.Automated)
                    GD.Print("[anomaly] strip: the target is not known, no examine sent");
                break;
            case InvSendKind.Refused:
                // nothing was sent: the client's own balloon says why, as tg's balloon alerts say a refusal's reason
                _session.Storages.RefuseLocally(send.Item, App.NowSeconds);
                _balloon.Add(InventoryRules.NoBackpack, App.NowSeconds);
                if (App.Instance.Args.Automated)
                    GD.Print($"[anomaly] move: item #{send.Item} has no backpack to go into: refused");
                break;
            default:
                if (App.Instance.Args.Automated)
                    GD.Print($"[anomaly] move: {Describe(release.Source)} onto {Describe(release.Over)}: nothing sent");
                break;
        }
    }

    // Slice 4D-1: a right click on the strip panel (true: it was on the panel, nothing else is done with it): on a slot with tg's alternate actions a
    // small menu of them at the pointer with tg's labels (InventoryRules.StripAlternates); anywhere else on the panel nothing.
    private bool OpenStripMenu(Vector2 at)
    {
        InvPlace place = _hudLayer.PlaceOf(PointerHud());
        if (place.Kind is not (InvKind.Strip or InvKind.StripSprite or InvKind.StripClose))
            return false;
        List<(string Action, string Label)> offered = InventoryRules.StripAlternates(place, _hudLayer.StripShown).ToList();
        if (offered.Count == 0 || _hudLayer.StripShown is not StripModel strip)
            return true;
        _stripMenuFor = (strip.Window, StripView.Keys[place.Index], offered);
        _stripMenu.Clear();
        for (int i = 0; i < offered.Count; i++)
            _stripMenu.AddItem(offered[i].Label, i);
        _stripMenu.Popup(new Rect2I((int)at.X, (int)at.Y, 0, 0));
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] strip: the menu of {StripView.Keys[place.Index]}: {string.Join(", ", offered.Select(o => o.Label))}");
        return true;
    }

    // DV694: a right click (a tap, no modifier) on an item of the inventory (a hand box, a bar cell, a worn slot's cell, a storage panel's cell) opens
    // the wheel of what can be done with it where it is, centred at the pointer and kept on the screen (InventoryRules.MenuItem, MenuEntries); false
    // anywhere else, where the right click stays what it was. The item is hovered at once so the server's hints for it fill the wheel (OnHints).
    private bool OpenInventoryMenu(Vector2 at, ClickModifiers modifiers)
    {
        InvPlace place = _hudLayer.PlaceOf(PointerHud());
        long item = InventoryRules.MenuItem(place, modifiers);
        if (item == 0)
            return false;
        _radialTarget = InventoryRules.MenuTarget(item);
        _inventoryMenuHand = place.Kind == InvKind.Hand && place.Index == _hudLayer.ActiveHand;
        _inventoryMenu = true;
        _radialSticky = true;
        _radialModel.OpenSticky(InventoryRules.MenuEntries(HintsFor(_radialTarget), _inventoryMenuHand));
        _radial.Centre = _radial.CentreNear(at);
        _radial.MoveToFront(); // over the docked chat column, which the wheel may reach into
        _radial.Title = _hudLayer.ItemOf(place).Name ?? KeyChips.ServerName(HintsFor(_radialTarget)) ?? string.Empty;
        _session.Rehover(); // fresh hints for what is possible now
        _session.Hover(_radialTarget, App.NowSeconds);
        _radial.Refresh();
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] inventory wheel: opened on item #{item} ({place.Kind}) at {_radial.Centre.Value.X:0},{_radial.Centre.Value.Y:0}: {string.Join(", ", _radialModel.Entries.Select(e => "\"" + e.Label + "\""))}");
        return true;
    }

    // DV694: the inventory wheel is over (picked, cancelled, replaced by a server menu, cursor mode off): the client's wheel is drawn at the crosshair again
    private void EndInventoryMenu()
    {
        _inventoryMenu = false;
        _radial.Centre = null;
        _radial.Refresh();
    }

    // DV694: Esc closes the open inventory wheel first, as the top thing over the view (true: it was open)
    private bool CloseInventoryMenu()
    {
        if (!_inventoryMenu || _radialModel.Phase != RadialPhase.Open)
            return false;
        _radialModel.Reset();
        _radialSticky = false;
        EndInventoryMenu();
        return true;
    }

    // Slice 4D-1: a pick in that menu: tg's `alt` act on the strip window it was opened for, while that window is still the strip window
    private void OnStripMenuPicked(long id)
    {
        (int window, string key, List<(string Action, string Label)> offered) = _stripMenuFor;
        Anomaly.Client.Shared.Tgui.TguiWindow strip = _tguiWindows?.Get(window);
        if (offered == null || id < 0 || id >= offered.Count || strip?.Context == null || strip.Interface != StripView.Interface)
            return;
        strip.Context.Act("alt", ("key", key), ("alternate_action", offered[(int)id].Action));
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] strip: alt {key} {offered[(int)id].Action} (window {window})");
    }

    // Slice 4D-1: the strip window the Tab inventory shows (the last opened), and its target as far as the client can tell (the window names no
    // entity: the mob last dragged onto oneself, else the one mob of that name next to the player)
    private void UpdateStripView()
    {
        StripModel model = StripView.ModelOf(StripView.Current(_tguiWindows?.Open));
        _hudLayer.Strip = model;
        _hudLayer.StripTarget = model != null && _hudLayer.HumanHud && InventoryShown ? StripView.TargetOf(model.Name, _strippedDrag, StripMobs()) : 0;
        if (App.Instance.Args.Automated)
        {
            // for scripted runs: where the strip panel and each slot's middle are, whenever they move
            StripLayout placed = _hudLayer.StripPlaced;
            string where = !placed.Shown ? "none" : $"panel {placed.Panel}, target #{_hudLayer.StripTarget}, X at {Middle(placed.Close)}, sprite at {Middle(placed.Sprite)}, "
                + string.Join(", ", StripView.Keys.Select((k, i) => placed.Cells[i].Width > 0f ? k + " at " + Middle(placed.Cells[i]) : null).Where(s => s != null));
            if (where != _stripLogged)
                GD.Print("[anomaly] strip view: " + (_stripLogged = where));
            static string Middle(HudRect r) => $"{r.Left + r.Width / 2f:0}x{r.Top + r.Height / 2f:0}";
        }
    }

    private string _stripLogged = "none";

    private IEnumerable<(long Id, string Name, int Distance)> StripMobs()
    {
        ReplicaWorld replica = _feed.Replica;
        if (replica == null || !replica.TryGet(_feed.OwnEntity, out ReplicaEntity me))
            yield break;
        foreach (ReplicaEntity e in replica.Entities)
        {
            if (e.Id != me.Id && e.State.Z == me.State.Z && e.IsMob())
                yield return (e.Id, e.State.Name, Math.Max(Math.Abs(e.State.X - me.State.X), Math.Abs(e.State.Y - me.State.Y)));
        }
    }

    // Slice 4D-1: the client sent tg's way of opening a strip window (the mob `dragged` dropped onto oneself): it is the strip target, and with a strip
    // window already open and Tab off the Tab inventory turns on at once (tg only brings its window forward; ours is withheld)
    private void StripDropSent(long dragged)
    {
        _strippedDrag = dragged;
        bool mob = _feed.Replica?.TryGet(dragged, out ReplicaEntity e) == true && e.IsMob();
        bool open = _tguiWindows?.Open.Any(w => w.Interface == StripView.Interface) == true;
        if (!StripView.ReopensStrip(dragged, _feed.OwnEntity, _feed.OwnEntity, mob, open, InventoryShown, _hudLayer.HumanHud))
            return;
        OpenInventoryFor();
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] strip: tg's strip opened again by its way (#{dragged} onto oneself), shown in the Tab inventory");
    }

    private void SendDrop(in DragRelease release)
    {
        bool sent = _session.Drop(release);
        if (sent && release.Source.Kind == TargetKind.Entity && release.Over.Kind == TargetKind.Entity && release.Over.Id == _feed.OwnEntity && release.Source.Id != _feed.OwnEntity)
            StripDropSent(release.Source.Id); // slice 4D-1: a body dragged onto oneself (the doll counts) opens its strip window
        if (sent && InventoryRules.ReopensOtherByDrag(release.Source.Kind == TargetKind.Entity ? release.Source.Id : 0, release.Over.Kind == TargetKind.Entity ? release.Over.Id : 0,
                _feed.OwnEntity, _hudLayer.OtherStorage, InventoryShown, _hudLayer.HumanHud))
            ReopenOther(); // slice 4C-2: the other container dragged onto oneself, tg's way of opening it
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] drop: {Describe(release.Source)} onto {Describe(release.Over)}{(sent ? string.Empty : " (not sent)")} after {release.DragTime} ds");
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
        // DV695: the first-person drag of oneself draws the own body's icon at the crosshair, as cursor mode draws the dragged thing at the pointer
        bool self = _selfDrag.Holding && !_mouse.CursorMode;
        if (!self && (!_drag.Dragging || !_mouse.CursorMode))
        {
            _dragIcon.Visible = false;
            return;
        }
        TableRef icon = DragModel.IconOf(_feed.Replica, self ? _selfDrag.Pressed : _drag.Source);
        if (icon.IsNone && !self && LiveHud.IsOurs(_drag.Source))
            icon = _hudLayer.ItemOf(_hudLayer.PlaceOf(_drag.Source)).Picture; // slice 4B-2: a storage panel's or belt cell's item
        if (icon.IsNone || _dragTextures == null || !_dragTextures.TryCompose(_feed.Replica, icon, 2, now, out ComposedAppearance composed) || composed.Texture == null)
        {
            _dragIcon.Visible = false;
            return;
        }
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float scale = new HudMetrics(view.X, view.Y, _options.UiScale).PixelScale; // the HUD's own pixel size, as a slot draws the item
        var size = new Vector2(composed.PixelWidth, composed.PixelHeight) * scale;
        Vector2 pointer = self ? view / 2f : GetViewport().GetMousePosition();
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
                if (_feed.OwnEntity != 0 && _session.Drop(new DragRelease(target, new ClickTarget(TargetKind.Entity, _feed.OwnEntity, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None, 255))
                    && target.Kind == TargetKind.Entity)
                    StripDropSent(target.Id); // slice 4D-1: tg's way of opening the strip window
                break;
            case RadialKind.Extra:
                _session.ContextPick(target, choice.Label); // version 86 (DV503): the table's Flip; DV694: an item's action button
                break;
            case RadialKind.UseInHand:
                // DV694: the inventory wheel's "Use in hand" on the active hand's item: tg's activate-in-hand keybinding, as the Z key
                _actedOn = (_hudLayer.HeldItem, App.NowSeconds);
                _session.Keybinding(KeybindingCode.ActivateInhand);
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
        float lowered = _entities.OwnBodyInView ? LookDown.HeldLowered(_rig.Pitch, _rig.FovDegrees, _rig.Camera.Near, _rig.EyeUp) : 0f; // T5a
        return new ViewModel.Holder(camera.BobAmount, camera.BobPhase, shakeX, shakeY, _dizzy, held, lowered);
    }

    // ---- keys ----

    private void OnInteractionKey(PlayAction? action)
    {
        // slice 4E: V and Shift+V are the inventory key with the backpack's or the belt's panel scrolled into view when it opens
        StoragePlace? panel = action switch { PlayAction.OpenBackpack => StoragePlace.Back, PlayAction.OpenBelt => StoragePlace.Belt, _ => null };
        if (panel != null)
            action = PlayAction.Inventory;
        if (action is PlayAction.Inventory && _hudLayer?.HumanHud != true)
            action = PlayAction.CursorMode; // another HUD has no inventory overlay: its inventory key is Tab's, as before
        if (action is PlayAction.CursorMode && _devMenuOpen && !_mouse.CursorMode)
        {
            ToggleDevMenuFocus(); // the owner (2026-11-10): the dev menu stays open, Tab gives the mouse to the game and back
            return;
        }
        // DV694: Tab only frees the cursor; the inventory key opens and closes the inventory overlay on the human HUD (elsewhere it is Tab's)
        if (action is PlayAction.CursorMode or PlayAction.Inventory)
        {
            if (InLobby) // the lobby has no view to give the mouse to: a stray Tab would only hide the pointer
                return;
            bool was = InventoryShown;
            (bool Cursor, bool Inventory) view = action == PlayAction.CursorMode ? TabInventory.TabPressed(_mouse.CursorMode, was)
                : TabInventory.InventoryPressed(_mouse.CursorMode, was, _hudLayer.HumanHud);
            SetView(view);
            if (panel != null && InventoryShown && !was)
                _hudLayer.ScrollToPlace = panel;
            if (App.Instance.Args.Automated)
                GD.Print($"[anomaly] {(action == PlayAction.CursorMode ? "cursor mode key" : "inventory key")}: cursor {(view.Cursor ? "free" : "captured")}, inventory {(InventoryShown ? "open" : "closed")}{(panel != null ? ", panel " + panel : "")}");
            // slice 4B-2: opening the inventory opens the carried storages as tg's opening does (the rustle), at most once a second
            if (_inventoryNotice.Opened(InventoryShown && !was, _hudLayer.HumanHud, App.NowSeconds))
            {
                _session.InventoryOpened();
                if (App.Instance.Args.Automated)
                    GD.Print("[anomaly] inventory: opened, the notice sent");
            }
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
                _actedOn = (_hudLayer.HeldItem, App.NowSeconds); // slice 4B-2: a held box used in hand opens as a listed storage
                _session.Keybinding(KeybindingCode.ActivateInhand);
                break;
            case PlayAction.QuickEquip:
                _session.Keybinding(KeybindingCode.QuickEquip);
                break;
            case PlayAction.QuickEquipBag or PlayAction.QuickEquipBelt or PlayAction.QuickEquipLeftPocket or PlayAction.QuickEquipRightPocket or PlayAction.QuickEquipSuitStorage:
                _session.Keybinding(KeyBindings.QuickEquipCodeOf(action.Value)); // slice 4E: tg's quick_equip_belt keybindings (smart_equip_targeted)
                break;
            case PlayAction.WorldInteract:
                // slice 4E (DV697): tg's empty-hand click on what the crosshair points at (the pointer's world pick in cursor mode), the server's to check
                _session.Interact(_mouse.CursorMode ? ClickTarget.Of(PointerWorldPick(_rig.Camera, out _)) : _target);
                break;
            case PlayAction.AltInteract:
                // slice 4E: tg's alt-click on what the crosshair points at; in cursor mode on what the pointer is over (a HUD entry, else the world)
                ClickTarget altTarget = _mouse.CursorMode ? PointerHud() : _target;
                if (_mouse.CursorMode && !altTarget.IsSet)
                    altTarget = ClickTarget.Of(PointerWorldPick(_rig.Camera, out _));
                if (altTarget.IsSet)
                    SendClick(altTarget, ClickButton.Left, ClickModifiers.Alt);
                break;
            case PlayAction.AltUseInHand:
                // slice 4E: tg's alt-click on the item in the active hand, as a click on it in its hand slot with Alt
                if (_hudLayer.HeldItem != 0)
                    SendClick(InventoryRules.MenuTarget(_hudLayer.HeldItem), ClickButton.Left, ClickModifiers.Alt);
                break;
            case PlayAction.MovePulled:
                MovePulledAtCrosshair();
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
            case PlayAction.CraftingMenu or PlayAction.LanguageMenu or PlayAction.Navigate:
                // ui-overhaul.md section 6, slice E: the click of tg's button the human HUD no longer draws; nothing on a HUD without one
                long menu = LiveHud.ButtonEntry(_feed.Replica, action.Value);
                if (menu != 0)
                    _session.Click(new ClickTarget(TargetKind.Screen, menu, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
                break;
            case PlayAction.ViewPetCommands:
                _session.Keybinding(KeybindingCode.ViewPetCommands); // version 59 (gaps C2): tg's view pet commands: held, hover a pet
                break;
            case PlayAction.Scoreboard:
                _session.Keybinding(KeybindingCode.Scoreboard); // version 93 (DV585): the deathmatch scoreboard, while held
                break;
            case >= PlayAction.Hotbar1 and <= PlayAction.Hotbar10:
                _session.HotbarPress(HotbarModel.CellOf(action.Value)); // ui-overhaul.md section 10 (DV693): the server decides; nothing predicted
                break;
            case >= PlayAction.ActionButton1 and <= PlayAction.ActionButton9:
                // section 15: tg's click on the action button in that slot, with the actions menu open or closed
                UseActionSlot(action.Value - PlayAction.ActionButton1 + 1);
                break;
            case PlayAction.ActionsMenu:
                ToggleActionsMenu(); // section 15: Space Station 14's OpenAbilitiesMenu
                break;
            case PlayAction.Examine:
                // tg's shift-click on what is under the crosshair (ShiftClickOn -> examinate), with a modifier held on top of the binding
                ClickAtCrosshair(ClickButton.Left, KeyBindings.ModifiedClick(PlayAction.Examine, _pressExtraShift, _pressExtraCtrl));
                break;
        }
    }

    // Slice 4E: tg's way of moving the pulled thing, its empty-hand click on the turf under the crosshair (turf/attack_hand: Move_Pulled, one step
    // onto a turf next to the player; tg's reach decides), only with the active hand empty (with an item held tg's click would use it on the floor)
    // and the cursor captured
    private void MovePulledAtCrosshair()
    {
        if (_mouse.CursorMode || _hudLayer.HeldItem != 0 || !_hit.IsHit || _hit.Kind is not (TargetKind.Entity or TargetKind.Turf))
            return;
        SendClick(new ClickTarget(TargetKind.Turf, 0, _hit.X, _hit.Y, _hit.Z, 16, 16), ClickButton.Left, ClickModifiers.None);
    }

    // Whether a key of the action a held keybinding came from is down now (a binding's key past its modifiers; a mouse binding counts as down)
    private bool HeldKeyDown(KeybindingCode code)
    {
        PlayAction action = code switch
        {
            KeybindingCode.HoldThrowMode => PlayAction.HoldThrow,
            KeybindingCode.ZLevelUp => PlayAction.MoveUpLevel,
            KeybindingCode.Resist => PlayAction.Resist,
            KeybindingCode.ViewPetCommands => PlayAction.ViewPetCommands,
            _ => PlayAction.Scoreboard,
        };
        foreach (string name in _options.Bindings.KeysOf(action))
        {
            if (!Enum.TryParse(name[(name.LastIndexOf('_') + 1)..], out Key key) || Input.IsPhysicalKeyPressed(key))
                return true;
        }
        return false;
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
    private bool CloseTopWindow() => CloseInventoryMenu() || CloseActionsMenu() || _tguiWindows?.CloseTop() == true || CloseStorage();

    // D292: the close-windows key (Shift+Esc)
    private void CloseAllWindows()
    {
        _tguiWindows?.CloseAll();
        CloseStorage();
    }

    // Slice 4A: tg's worn slots exist as screen entries only while tg's inventory is expanded, so the client clicks tg's inventory toggle (which the
    // human HUD does not draw) once whenever it shows collapsed (InventoryExpander); in first person the slots are neither drawn nor clicked
    private void ExpandInventory()
    {
        long toggle = _inventoryExpander.Next(_hudLayer.CollapsedToggle, _hudLayer.ExpandedToggle);
        if (toggle != 0)
            _session?.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
    }

    private bool CloseStorage()
    {
        long close = _storageWindow?.CloseEntry ?? 0;
        return close != 0 && _session.Click(new ClickTarget(TargetKind.Screen, close, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
    }

    private void ReopenOther()
    {
        OpenInventoryFor();
        if (App.Instance.Args.Automated)
            GD.Print($"[anomaly] storage window: the other container #{_hudLayer.OtherStorage} opened again by tg's way, shown in the inventory");
    }

    private void SetCursorMode(bool on)
    {
        if (_mouse.CursorMode == on)
            return;
        _mouse.Set(on);
        CursorModeChanged();
    }

    // DV694: cursor mode and the inventory overlay as the key rules give them (TabInventory.TabPressed, InventoryPressed, InventoryEvent); the
    // inventory only with the cursor free. Opening the inventory makes cursor mode the player's own, so a machine's window closing does not end it.
    private void SetView((bool Cursor, bool Inventory) view)
    {
        bool open = view.Cursor && view.Inventory;
        if (_mouse.CursorMode != view.Cursor)
        {
            _mouse.Set(view.Cursor);
            CursorModeChanged();
        }
        else if (open && !_inventoryOpen)
            _mouse.Set(true);
        _inventoryOpen = open;
    }

    // DV694: Esc with nothing else over the view closes the inventory overlay and returns to the view (true: it was open)
    private bool CloseInventory()
    {
        if (!InventoryShown)
            return false;
        SetView((false, false));
        return true;
    }

    // DV694: something tg opened that the inventory shows (a container, a strip window, a listed storage's window): the inventory opens
    private void OpenInventoryFor() => SetView(TabInventory.InventoryEvent(_mouse.CursorMode, _inventoryOpen, _hudLayer.HumanHud));

    // Cursor mode was turned on or off (by Tab or by a window, MouseOwner); the pointer follows in SyncMouse.
    private void CursorModeChanged()
    {
        _radialModel.Reset();
        _radialSticky = false;
        if (_inventoryMenu)
            EndInventoryMenu();
        if (!_mouse.CursorMode)
            _inventoryOpen = false; // DV694: the inventory overlay is never shown with the view captured
        _drag.Cancel(); // version 17: a cursor-mode drag ends with cursor mode
        if (!_mouse.CursorMode)
            _actionsView?.SetOpen(false); // section 15: the actions menu needs the pointer: it closes with cursor mode (Tab)
        (_dragFrom, _pressQuick) = (default, false);
        _selfDrag.Cancel(); // DV695: and the first-person drag of oneself ends when cursor mode starts (Tab, a window), with nothing sent
        if (_dragIcon != null)
            _dragIcon.Visible = false;
    }

    // ---- the scripted shot scenarios (ANOMALY_SHOT=hover|radial|hud|container|throw|livehud|gun|pointer, with --fake-server; see godot/tools/README) ----

    private FakeServer FakeServerOrNull => (_feed as FakeFeed)?.Server;

    private GunSightMessage _shotGunSight; // the "gun" scenario's sight in place of the server's

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
            case "gun":
                // the fake server holds no gun: the sight of a buckshot-wide cone is set here, and fired once for the bloom (ui-overhaul.md 6)
                At(0.8, () => { AimAtEntity(server.ToolboxId); _shotGunSight = new GunSightMessage(true, 4f, 0); });
                At(2.5, () => _crosshair.Fired(App.NowSeconds));
                break;
            case "pointer":
                // cursor mode with the pointer on the floor right of the middle, clear of the inventory window: the prompts stand beside the pointer
                At(0.8, () => AimAtEntity(server.ToolboxId));
                At(1.5, () => SetCursorMode(true));
                At(1.8, () => Input.WarpMouse(GetViewport().GetVisibleRect().Size * new Vector2(0.7f, 0.45f)));
                break;
            case "livehud":
                // the doll coloured by damage, one level per part (head 1, chest 2, left arm 3, right arm 4, left leg 5, right leg none), a
                // wound outline on the right arm, and combat mode on (tg's toggle in the corner, ui-overhaul.md 6, slice E); tg's zone selector is not shown
                At(0.5, () => _session.DevCommand("damage me brute 40 head"));
                At(0.55, () => _session.DevCommand("damage me brute 80 chest"));
                At(0.6, () => _session.DevCommand("damage me brute 30 l_arm"));
                At(0.65, () => _session.DevCommand("damage me brute 40 r_arm"));
                At(0.7, () => _session.DevCommand("damage me brute 50 l_leg"));
                At(0.75, () => _session.DevCommand("wound me r_arm slash severe"));
                if (System.Environment.GetEnvironmentVariable("ANOMALY_SHOT_DISABLED") == "1")
                    At(0.8, () => _session.DevCommand("wound me l_arm blunt critical")); // tg's compound fracture disables the left arm (doll key 7)
                // slice D: three of tg's alerts as chips by the doll (no oxygen, weightless in the HUD's style, cold)
                foreach (string alert in new[] { "not_enough_oxy", "weightless", "cold" })
                    At(0.85, () => _session.DevCommand("alert me " + alert));
                At(1.0, () => _session.ToggleCombatMode());
                // slice C: the toolbox in the left hand, then the welder in the right, which stays active: both hand boxes and the hint strip show
                Pick(server.ToolboxId, 1.1);
                At(1.9, () => _session.Keybinding(KeybindingCode.SwapHands));
                Pick(server.WelderId, 2.1);
                if (System.Environment.GetEnvironmentVariable("ANOMALY_SHOT_CURSOR") == "1")
                    At(3.0, () => SetCursorMode(true)); // the cursor freed: tg's zone selector shows at its place
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
