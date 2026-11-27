using System.Linq;
using System.Text;
using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// The HUD (D107, amends D44 for the HUD): the session's private <c>client.screen</c> entries, which tg's own translated code
/// computes on the server (the health doll, the health indicator, fullscreens, alerts) and which arrive only in their owner's
/// private block, drawn with tg's sprites (through <see cref="IAppearanceTextures"/>, M2 P7's atlas) at their <c>screen_loc</c>,
/// mapped from tg's 15 x 15 view grid onto the screen's edges (<see cref="ScreenLoc"/>).
/// <para>
/// D70's additions are made from the same replicated state, nothing more: when an entry's appearance holds the doll's zone
/// states (<c>head3</c>, <c>l_arm0</c>, ...; <see cref="HudReader"/>), each zone is drawn on its own with a hatching by its level
/// (the shape cue; a shader keyed by <see cref="HatchStyle"/>) and in the palette's colour for the level (the standard ramp, or a
/// colourblind one), and a text label per zone lists its name and level beside the doll. Fullscreen effects (the damage and crit
/// overlays) are limited by the player's strength setting.
/// </para>
/// <para>
/// The live HUD (ui-overhaul.md section 6, <see cref="LiveHud"/>): the doll is re-placed (it shows injuries only; its wound outlines stay tg's),
/// still taking tg's clicks, and tg's zone selector is shown only while the cursor is free. Slice C, on the human HUD: tg's two hand boxes are
/// drawn as native boxes either side of the hotbar at the bottom centre (<see cref="LiveHudView"/>); the boxes still take tg's clicks and drags.
/// Package 3: the hotbar's ten cells between them (the belt's seven, a click on one under Tab its press; tg's pocket and ID slots drawn as cells 8
/// to 10, still tg's for clicks and drags). Slice 4A, under Tab only: the character panel (the own sprite, taking clicks as the doll does, with tg's
/// worn slots round it in cells, still tg's for clicks, drags and tooltips) and the chat window's docked place (<see cref="TabInventory"/>). Slice D,
/// on the human HUD: a hint strip over the hands names the held item (package 3) and its keys, not under Tab, and tg's alerts are chips by the vitals (above them, or left of them) instead of tg's column (DV82, which other HUDs keep), each chip
/// taking its alert's clicks and hover. Slice E, on the human HUD: the doll, tg's health indicator and mood face stand bare against the screen's
/// right edge (or the doll alone left of tg's right column on a crowded screen), tg's status indicators and sleep button are chips, tg's combat
/// toggle is in the corner with, under Tab only, the zone selector, the memories button and the area creator left of it, and tg's buttons that a
/// key does are neither drawn nor clicked.
/// </para>
/// </summary>
public partial class HudLayer : CanvasLayer, IHudSurface
{
    private const string ShaderCode = @"
shader_type canvas_item;
uniform int shape = 0; // HealthZones.Pattern: HatchShape, its period and line width in cells
uniform float period = 1.0;
uniform float width = 0.0;
uniform float cell = 2.0;
uniform bool use_tint = false;
uniform vec4 tint : source_color = vec4(1.0);
uniform float tint_ref = 0.0; // the standard ramp: the limb's brightest channel (HealthZones.Brightest); 0 for a colourblind palette
uniform vec4 hatch_color : source_color = vec4(0.0, 0.0, 0.0, 0.7);
uniform sampler2D limb : filter_nearest; // the limb's own picture, without its filters (tg's wound outline)
uniform vec4 limb_rect = vec4(0.0, 0.0, 1.0, 1.0); // where it lies in this picture: UV offset (xy) and size (zw)

float line(float v, float per, float w) { return mod(v, per) < w ? 1.0 : 0.0; }

// the same lines as HealthZones.Hatched
float hatch(vec2 p) {
    float per = period * cell, w = width * cell;
    if (shape == 1) return line(p.x + p.y, per, w);
    if (shape == 2) return max(line(p.x + p.y, per, w), line(p.x - p.y, per, w));
    if (shape == 3) { vec2 q = mod(p, per) - per * 0.5; return dot(q, q) < w * w ? 1.0 : 0.0; }
    if (shape == 4) return line(p.x, per, w);
    if (shape == 5) return line(p.y, per, w);
    if (shape == 6) return max(max(line(p.x + p.y, per, w), line(p.x - p.y, per, w)), line(p.x, per, w));
    return 0.0;
}

void fragment() {
    vec4 shown = texture(TEXTURE, UV);
    vec2 luv = (UV - limb_rect.xy) / limb_rect.zw;
    vec4 own = (luv.x >= 0.0 && luv.y >= 0.0 && luv.x <= 1.0 && luv.y <= 1.0) ? texture(limb, luv) : vec4(0.0);
    if (own.a < 0.5) {
        COLOR = shown; // around the limb: its filters as tg draws them (the wound outline's red), never tinted
    } else {
        vec3 rgb = own.rgb;
        if (use_tint && tint_ref > 0.0) {
            rgb = tint.rgb * clamp(max(rgb.r, max(rgb.g, rgb.b)) / tint_ref, 0.0, 1.0);
        } else if (use_tint) {
            float l = dot(rgb, vec3(0.299, 0.587, 0.114));
            rgb = tint.rgb * (0.55 + 0.6 * l);
        }
        COLOR = vec4(mix(rgb, hatch_color.rgb, hatch(FRAGCOORD.xy) * hatch_color.a), own.a);
    }
}
";

    private readonly Dictionary<long, Item> _items = new();
    private readonly List<Item> _order = new();
    private ReplicaWorld _replica;
    private IAppearanceTextures _textures;
    private PlayOptions _options;
    private Control _root;
    private Shader _shader;
    private bool _sortDirty;
    private Control _fullscreenRoot;
    private Control _splashRoot; // tg's SPLASHSCREEN_PLANE (syndicate N1a: the cinematic)
    private ColorRect _splashBackdrop;

    // tg: code/__DEFINES/layers.dm  #define SPLASHSCREEN_PLANE 42
    private const int SplashscreenPlane = 42;
    private int _lastDollTotal = -1;
    private Label _labels;
    private readonly List<ZoneState> _zoneStates = new();
    private readonly List<HudRect> _zoneRects = new();
    private LiveHudView _view; // ui-overhaul.md section 6: the hands, the hint strip and the alert chips' boxes, under tg's pictures
    private LiveHudLayout _layout;
    private HandsLayout _hands;
    private bool _humanHud; // slice C: tg's inventory toggle is on the screen, so the hands and the bottom slots are re-placed (LiveHud.Placed)
    private readonly List<Item> _wornSlots = new(); // slice 4A: tg's worn slots, in the character panel under Tab
    private TabInventoryLayout _panel; // slice 4A: the chat column and the character panel under Tab on the human HUD
    private readonly List<HudRect> _tgDrawn = new(); // tg's entries still drawn under Tab, which the Tab inventory keeps clear of
    private ComposedRaster _spriteRaster; // slice 4A: the own sprite's last picture, and its icon square as the HUD pick gets it (TabInventory.SpritePick)
    private Anomaly.Client.Shared.Icons.Raster _spritePick, _spriteShown; // the pick's square of that picture; the one this frame (null: no sprite drawn)
    private HudRect _spriteRect;
    private TableRef _spriteLook; // the appearance it was composed from, and its zone map (made once per appearance)
    private BodyZoneMap _spriteMap;
    private TableRef _spriteMapLook;
    private int _spriteMapSnapshots;
    private readonly MaptextNode[] _cellTexts = new MaptextNode[HotbarMessage.CellCount]; // package 3: a stack's amount on its cell's item

    /// <summary>Package 3 (ui-overhaul.md section 10): the own hotbar as the server last sent it, which the bar's cells show. Set by the scene each frame.</summary>
    public HotbarModel Hotbar { get; set; }
    private readonly List<Item> _alerts = new(); // slice D: tg's alerts on the human HUD, in tg's order
    private List<HudRect> _chips = new(); // their chips (Item.Cell is the index)

    /// <summary>Whether the cursor is free (cursor mode, Tab): tg's zone selector is shown then only (<see cref="LiveHud.Shown"/>). Set by the scene each frame.</summary>
    public bool CursorMode { get; set; }

    /// <summary>Whether the HUD is the human HUD (tg's inventory toggle is on the screen): the hands, the slots and the Tab inventory are ours.</summary>
    public bool HumanHud => _humanHud;

    /// <summary>Slice 4A: where the chat window is docked under Tab (<see cref="TabInventory.ChatDock"/>); null: at its own place.</summary>
    public HudRect? ChatDock { get; private set; }

    /// <summary>The health doll's screen entry (0: none): an empty-hand click on the own sprite is a click on it (<see cref="TabInventory.SelfClick"/>).</summary>
    public long DollEntry { get; private set; }

    /// <summary>Slice 4A: tg's inventory toggle while it shows tg's inventory collapsed (its <c>toggle</c> state), else 0 (<see cref="InventoryExpander"/>).</summary>
    public long CollapsedToggle { get; private set; }

    /// <summary>Slice 4A: tg's inventory toggle while it shows tg's inventory expanded (<c>toggle_active</c>), else 0.</summary>
    public long ExpandedToggle { get; private set; }

    /// <summary>
    /// Slice 4A: the part of the own sprite in the character panel at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) of a pick on it (the
    /// nine zones of aiming by look, from the sprite's own picture); None off its body or without it.
    /// </summary>
    public AimZone SpriteZoneAt(byte iconX, byte iconY)
    {
        if (_textures is not AppearanceTextures real || _spriteLook.IsNone || iconX == 0 || iconY == 0)
            return AimZone.None;
        ReplicaTables tables = real.TablesOf(_replica);
        if (_spriteMapLook != _spriteLook || _spriteMap == null || _spriteMapSnapshots != _replica.SnapshotsApplied)
        {
            _spriteMap = BodyZoneAim.ZoneMap(tables, real.Compositor, _spriteLook, ByondDir.South);
            (_spriteMapLook, _spriteMapSnapshots) = (_spriteLook, _replica.SnapshotsApplied);
        }
        return BodyZoneAim.ZoneAtPixel(tables, real.Compositor, _spriteLook, _spriteMap, iconX, iconY);
    }

    // the role an item is drawn in this frame: the hands, the bottom slots and the alerts only on the human HUD, tg's right-column indicators
    // only with its full vitals stack (slices D and E)
    private LiveHudRole RoleNow(Item item) => LiveHud.Placed(item.Role, _humanHud, _layout.Vitals);

    private static bool IsHand(LiveHudRole role) => role is LiveHudRole.LeftHand or LiveHudRole.RightHand;

    /// <summary>
    /// Slice T5c: the doll's part under the free pointer (<see cref="DollZoneAt"/> refined by <see cref="BodyZoneAim.DollFaceAt"/>; None off the doll). Over the eyes or the mouth tg's own hover
    /// picture of the zone selector (<c>obj/effect/overlay/zone_sel</c>: screen_gen.dmi's "eyes" or "mouth", alpha 128) is drawn on the doll. Set by
    /// the scene each frame.
    /// </summary>
    public AimZone DollHover { get; set; }

    private static readonly Dictionary<string, Texture2D> FacePictures = new();

    /// <summary>Where what stands left of the vitals and their chips must end (the prompts beside the crosshair), screen pixels; infinity while no doll is shown.</summary>
    public float PanelLeftEdge { get; private set; } = float.PositiveInfinity;

    /// <summary>Raised when the doll shows more damage than before (the camera shakes a little).</summary>
    public event Action DamageTaken;

    /// <summary>M4v slice 4 (protocol version 11): the render tick the screen objects' flicks and animate() chains play at (the entities' one; NaN: the replica's tick).</summary>
    public double RenderTick { get; set; } = double.NaN;

    /// <summary>The screen entries drawn.</summary>
    public int ItemCount => _items.Count;

    /// <summary>True when the screen entry <paramref name="id"/> draws the health doll (its appearance holds the doll's zones): a drop on it is a drop on the own mob (the owner, 2026-10-15).</summary>
    public bool IsDoll(long id) => _items.TryGetValue(id, out Item item) && item.Zones.Count > 0;

    /// <summary>D183: the limb of the health doll entry <paramref name="id"/> drawn at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) of a click on it (<see cref="BodyZoneAim.DollZoneAt"/>).</summary>
    public AimZone DollZoneAt(long id, byte iconX, byte iconY)
        => _textures is AppearanceTextures real && _items.TryGetValue(id, out Item item) && item.Zones.Count > 0
            ? BodyZoneAim.DollZoneAt(real.TablesOf(_replica), real.Compositor, item.Entry.Appearance, iconX, iconY)
            : AimZone.None;

    /// <summary>The zone labels now shown, one per line (for the run summary and tests).</summary>
    public string ZoneLabelText { get; private set; } = string.Empty;

    /// <summary>Whether a picture of tg's splash plane is shown now (a cinematic): the scene hides the interface meanwhile.</summary>
    public bool SplashShown
    {
        get
        {
            foreach (Item item in _order)
            {
                if (item.Splash && item.Whole != null && item.Whole.Visible && item.Whole.Texture != null)
                    return true;
            }
            return false;
        }
    }

    /// <summary>The interface switch (GameScene.Interface.cs): the screen objects of the HUD hide; tg's fullscreens and splash plane stay.</summary>
    public void SetScreenHidden(bool hidden)
    {
        if (_root != null)
            _root.Visible = !hidden;
    }

    /// <summary>Black behind tg's splash plane while a picture of it is shown (the scene's frame, GameScene.Interface.cs).</summary>
    public void SetSplashBackdrop(bool shown)
    {
        if (_splashBackdrop != null && _splashBackdrop.Visible != shown)
            _splashBackdrop.Visible = shown;
    }

    /// <summary>The root control (a screenshot of the HUD can be taken from it).</summary>
    public Control Root => _root;

    public void Bind(ReplicaWorld replica, IAppearanceTextures textures, PlayOptions options)
    {
        _replica = replica;
        _textures = textures;
        _options = options;
        _shader = new Shader { Code = ShaderCode };
        _root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _view = new LiveHudView { Name = "LiveHud" };
        _root.AddChild(_view); // the root's first child: tg's pictures are drawn over it (Update keeps it first)
        // tg draws the fullscreens (the damage, crit and death overlays) on FULLSCREEN_PLANE, below the HUD planes, so the HUD
        // stays visible over them: here they go on their own canvas layer under every UI layer (the debug HUD 1, this HUD
        // and the windows 2, the UI 3), above the 3D view (the owner, 2026-10-09).
        var fullscreens = new CanvasLayer { Name = "Fullscreens", Layer = 0 };
        AddChild(fullscreens);
        _fullscreenRoot = new Control { Name = "FullscreenRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _fullscreenRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fullscreens.AddChild(_fullscreenRoot);
        // tg's splash plane (the cinematics) over the 3D view and tg's fullscreens; while it shows, the scene hides the whole interface
        // (GameScene.Interface.cs: the owner, 2026-11-11)
        var splash = new CanvasLayer { Name = "Splash", Layer = 1 };
        AddChild(splash);
        // black behind a splash picture, as tg's cinematic backdrop (a fullscreen of the watching mob, which a ghost made mid-cinematic has not)
        _splashBackdrop = new ColorRect { Name = "SplashBackdrop", Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _splashBackdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        splash.AddChild(_splashBackdrop);
        _splashRoot = new Control { Name = "SplashRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _splashRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        splash.AddChild(_splashRoot);
        replica.ScreenEntryChanged += OnChanged;
        replica.ScreenEntryRemoved += OnRemoved;
        foreach (ScreenEntry entry in replica.Screen.Values)
            OnChanged(entry);
    }

    public override void _ExitTree()
    {
        if (_replica != null)
        {
            _replica.ScreenEntryChanged -= OnChanged;
            _replica.ScreenEntryRemoved -= OnRemoved;
        }
    }

    // M4a P7 (D131): the entries of an open storage are drawn by the native window (ContainerWindow), not at their screen_loc; slice 4A: tg's toggled
    // inventory's slots are worn slots (the character panel), and anything else of that group is not drawn.
    private static bool InWindow(ScreenEntry entry) => entry.Group == HudGroup.Storage || (entry.Group == HudGroup.ToggleableInventory && entry.SlotId == 0);

    /// <inheritdoc />
    public void Collect(List<HudDrawn> bottomToTop)
    {
        if (_view.Hands.Shown) // package 3: the belt's cells are ours, a click on one is its press (PlaySession.Click)
            LiveHud.BeltDrawn(_hands, bottomToTop, hidden: !_root.Visible);
        // slice 4A: the own sprite under the slots' cells, on the pixels drawn of it: its icon square alone (TabInventory.SpritePick; a transparent
        // pixel, or a part of a larger picture cut off at the square, is no click on it)
        if (_root.Visible && _panel.Shown && _spriteShown != null)
            bottomToTop.Add(new HudDrawn(TabInventory.SpriteId, _panel.Sprite, _spriteShown, 0, 0));
        foreach (Item item in _order)
        {
            if (!item.Container.Visible || !item.HasSpec || item.Hidden || !LiveHud.Shown(RoleNow(item), CursorMode))
                continue;
            if (RoleNow(item) == LiveHudRole.WornSlot) // slice 4A: the panel's cell is tg's slot for clicks, drags and its tooltip
            {
                LiveHud.SlotCellDrawn(_panel.CellAt(item.Cell), _panel.CellUnit, item.Entry.Id, HudPictures.ChildrenOf(_replica, _textures, item.Entry, _lastNow), bottomToTop);
                continue;
            }
            if (item.Zones.Count > 0)
            {
                // the doll's own 32 x 32 icon square, once: a point on it is tg's icon pixel whatever size the zones' pictures are (a wound
                // outline grows them), and BodyZoneAim.DollZoneAt finds the limb there
                if (item.Zones.Any(node => node.Rect.Visible))
                    bottomToTop.Add(item.Role == LiveHudRole.Doll ? LiveHud.DollDrawn(item.Entry.Id, _layout) : new HudDrawn(item.Entry.Id, item.IconSquare, null, 0, 0));
            }
            else if (RoleNow(item) == LiveHudRole.Alert)
            {
                if (item.Cell < _chips.Count) // slice D: the whole chip is tg's alert
                    bottomToTop.Add(LiveHud.AlertDrawn(item.Entry.Id, _chips[item.Cell]));
            }
            else if (IsHand(RoleNow(item)))
            {
                // slice C: the native box is tg's hand box for clicks and drags, the held item's own pixels in its picture square the item's
                HudRect box = item.Role == LiveHudRole.LeftHand ? _hands.Left : _hands.Right;
                bottomToTop.Add(new HudDrawn(item.Entry.Id, box, null, 0, 0));
                bottomToTop.Add(new HudDrawn(item.Entry.Id, LiveHud.HandPicture(box, _hands.Unit), null, 0, 0, HudPictures.ChildrenOf(_replica, _textures, item.Entry, _lastNow)));
            }
            else if (RoleNow(item) == LiveHudRole.BarSlot && _view.Hands.Shown) // package 3: the cell is tg's pocket or ID slot for clicks and drags
                LiveHud.BarSlotDrawn(_hands, item.Entry.Id, item.Cell, HudPictures.ChildrenOf(_replica, _textures, item.Entry, _lastNow), bottomToTop);
            else if (item.Whole != null && item.Whole.Visible && item.Whole.Texture != null && item.Fullscreen == false)
                // Version 12: the item in a hand or a slot (the entry's vis_contents child) is hit on its own pixels before the slot.
                bottomToTop.Add(new HudDrawn(item.Entry.Id, item.LastRect, HudPictures.RasterOf(item.Whole.Texture), item.OriginX, item.OriginY,
                    HudPictures.ChildrenOf(_replica, _textures, item.Entry, _lastNow)));
        }
    }

    /// <summary>
    /// Version 27: where tg's action button of the item <paramref name="itemId"/> is drawn now (<see cref="ScreenEntry.ActionTarget"/>: the
    /// tank of "Set Internals"), for that item's progress bars to go under it; false when no shown button is the item's.
    /// </summary>
    public (bool Found, HudRect Rect) ActionButtonOf(long itemId)
    {
        foreach (Item item in _order)
        {
            if (item.Entry.ActionTarget == itemId && item.Container.Visible && !item.Hidden && item.Whole != null && item.Whole.Visible && !item.Fullscreen)
                return (true, item.LastRect);
        }
        return (false, default);
    }

    /// <summary>
    /// The right edge, in pixels, of the shown HUD entries that begin within <paramref name="band"/> pixels of the screen's top-left corner
    /// (the action buttons' first row), 0 when none: where a control beside them must start not to cover them.
    /// </summary>
    public float TopLeftRowRight(float band)
    {
        float right = 0f;
        foreach (Item item in _order)
        {
            if (item.Container.Visible && !item.Hidden && item.Whole != null && item.Whole.Visible && !item.Fullscreen
                && item.LastRect.Top < band && item.LastRect.Left < band * 6)
                right = Mathf.Max(right, item.LastRect.Right);
        }
        return right;
    }

    /// <summary>The owner (2026-10-29): the id of the <paramref name="number"/>th (from 1) shown item action button, top row first and left to right; 0 when none.</summary>
    public long ActionButtonNumber(int number) => _numbered.ElementAtOrDefault(number - 1)?.Entry.Id ?? 0;

    private readonly List<Item> _numbered = new();

    // The owner (2026-10-29): the shown item action buttons in reading order, each with its key (ActionButton1-9) as a key cap in its top left corner
    private void NumberActionButtons(float unit)
    {
        _numbered.Clear();
        _numbered.AddRange(_order.Where(item => item.Entry.ActionTarget > 0 && item.Container.Visible && !item.Hidden && item.Whole != null && item.Whole.Visible && !item.Fullscreen)
            .OrderBy(item => Mathf.Round(item.LastRect.Top / 4f)).ThenBy(item => item.LastRect.Left));
        foreach (Item item in _order)
        {
            int number = _numbered.IndexOf(item) + 1;
            IReadOnlyList<string> keys = number is >= 1 and <= 9 ? _options.Bindings.KeysOf(PlayAction.ActionButton1 + (byte)(number - 1)) : null;
            if (keys == null || keys.Count == 0)
            {
                if (item.KeyHint != null)
                    item.KeyHint.Visible = false;
                continue;
            }
            // a key cap in the button's top left corner, as a hotbar cell's (its label made smaller, then cut, to fit)
            if (item.KeyHint == null)
            {
                item.KeyHint = new LiveHudView.KeyCap();
                item.KeyHint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                item.Container.AddChild(item.KeyHint);
            }
            item.Container.MoveChild(item.KeyHint, -1); // over the button's picture
            item.KeyHint.Set(item.LastRect, KeyChips.Label(keys[0]), unit);
            item.KeyHint.Visible = true;
        }
    }

    private double _lastNow;

    private void OnChanged(ScreenEntry entry)
    {
        if (!_items.TryGetValue(entry.Id, out Item item))
        {
            item = new Item { Container = new Control { Name = "S" + entry.Id, MouseFilter = Control.MouseFilterEnum.Ignore } };
            _root.AddChild(item.Container);
            _items[entry.Id] = item;
            _order.Add(item);
        }
        item.Entry = entry;
        item.Hidden = InWindow(entry);
        item.HasSpec = ScreenLoc.TryParse(entry.ScreenLoc, out item.Spec);
        item.Analyze = true;
        item.Layer = _replica.TryGetAppearance(entry.Appearance, out AppearanceEntry appearance) ? appearance.Layer : 0f;
        item.Splash = appearance != null && appearance.Plane == SplashscreenPlane;
        _sortDirty = true;
    }

    private void OnRemoved(long id)
    {
        if (!_items.Remove(id, out Item item))
            return;
        _order.Remove(item);
        item.Container.QueueFree();
        if (_items.Count == 0)
            _lastDollTotal = -1;
    }

    /// <summary>Draws the HUD for this frame (call once per frame after the feed was polled).</summary>
    public void Update(double nowSeconds)
    {
        if (_root == null)
            return;
        _lastNow = nowSeconds;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        var metrics = new HudMetrics(size.X, size.Y, _options.UiScale);
        if (_sortDirty)
        {
            _order.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Entry.Id.CompareTo(b.Entry.Id));
            // tg's layer order within each root (the HUD's, the fullscreens' and the splash's canvas layers); the HUD's after the live HUD's view
            int hudIndex = 1, fullscreenIndex = 0, splashIndex = 0;
            foreach (Item ordered in _order)
            {
                Node parent = ordered.Container.GetParent();
                parent.MoveChild(ordered.Container, parent == _fullscreenRoot ? fullscreenIndex++ : parent == _splashRoot ? splashIndex++ : hudIndex++);
            }
            _sortDirty = false;
        }
        _zoneStates.Clear();
        _zoneRects.Clear();
        UpdateLiveHud(metrics);
        MeasureAlertColumn(metrics);
        int total = 0;
        foreach (Item item in _order)
            UpdateItem(item, metrics, nowSeconds, ref total);
        NumberActionButtons(UiTokens.Unit(metrics));
        UpdateLabels(metrics);
        if (_zoneStates.Count > 0)
        {
            if (_lastDollTotal >= 0 && total > _lastDollTotal)
                DamageTaken?.Invoke();
            _lastDollTotal = total;
        }
    }

    private bool _dollShown;

    // ui-overhaul.md section 6: where the live HUD's re-placed entries go (slice C: on the human HUD the hands; package 3: the bar's cells; slice 4A:
    // the Tab inventory; slice D: the vitals, the alert chips and the hint strip; slice E: the vitals bare, tg's combat toggle in the corner), what
    // its view draws, and where what stands left of them must end.
    private void UpdateLiveHud(in HudMetrics metrics)
    {
        bool doll = false, human = false;
        Item left = null, right = null, active = null;
        _wornSlots.Clear();
        _alerts.Clear();
        (DollEntry, CollapsedToggle, ExpandedToggle) = (0, 0, 0);
        foreach (Item item in _order)
        {
            if (!item.HasSpec || item.Hidden)
                continue;
            if (item.Analyze)
                Analyze(item);
            switch (item.Role)
            {
                case LiveHudRole.Doll:
                    doll = true;
                    DollEntry = item.Entry.Id;
                    break;
                case LiveHudRole.InventoryToggle:
                    human = true;
                    // slice 4A: tg's own state of the toggle says whether tg's inventory is expanded (screen_objects/human.dm update_icon_state)
                    string state = _replica.TryGetAppearance(item.Entry.Appearance, out AppearanceEntry look) ? _replica.ResolveString(look.IconState) : null;
                    if (state == "toggle")
                        CollapsedToggle = item.Entry.Id;
                    else if (state == "toggle_active")
                        ExpandedToggle = item.Entry.Id;
                    break;
                case LiveHudRole.LeftHand: left = item; break;
                case LiveHudRole.RightHand: right = item; break;
                case LiveHudRole.WornSlot:
                    item.Cell = TabInventory.CellOf(item.Entry.SlotId);
                    _wornSlots.Add(item);
                    break;
                case LiveHudRole.BarSlot:
                    item.Cell = LiveHud.BarCellOfSlot(item.Entry.SlotId);
                    break;
            }
            // the active hand, whichever (a third or fourth hand stays at tg's place): the one with tg's active overlay
            if (active == null && item.Entry.HandIndex > 0 && LiveHud.IsActiveHand(_replica, item.Entry))
                active = item;
        }
        _dollShown = doll;
        _humanHud = human;
        _layout = LiveHud.Layout(metrics, human);
        // slice D: on the human HUD tg's alerts are chips by the vitals, in tg's order, and with the full stack slice E's status chips (hunger,
        // stamina, internals, suit cell, any other entry of tg's right status column, sleep) follow them while tg draws them
        foreach (Item item in _order)
        {
            if (!item.HasSpec || item.Hidden || RoleNow(item) != LiveHudRole.Alert)
                continue;
            item.Cell = int.MaxValue; // no chip unless it gets one below
            if (LiveHud.ChipShown(item.Role, _replica, item.Entry, StateExists)) // slice E: the stamina chip only while stamina is down
                _alerts.Add(item);
        }
        static int Order(Item item) => LiveHud.ChipOrder(item.Role, item.Spec);
        _alerts.Sort((a, b) => Order(a) != Order(b) ? Order(a).CompareTo(Order(b)) : a.Entry.Id.CompareTo(b.Entry.Id));
        for (int i = 0; i < _alerts.Count; i++)
            _alerts[i].Cell = i;
        _chips = LiveHud.AlertStack(metrics, _alerts.Count, _layout);
        _view.ShowAlerts(_chips);
        bool leftActive = active != null && active == left, rightActive = active != null && active == right;
        // package 3: the held item's name leads the hint strip (the server's names of the items in the own hands)
        string heldName = leftActive ? Hotbar?.LeftHand : rightActive ? Hotbar?.RightHand : null;
        float named = 0f, hint = human ? HintNatural(UiTokens.Unit(metrics), heldName, out named) : 0f;
        _hands = LiveHud.Hands(metrics, leftActive, rightActive, human ? _layout : default, hint, named, CursorMode);
        UpdatePanel(metrics, human);
        PanelLeftEdge = doll ? LiveHud.LeftOfVitals(_layout, metrics) : float.PositiveInfinity;
        if (_chips.Count > 0) // slice D: the prompts end before alert chips left of the vitals too
            PanelLeftEdge = Math.Min(PanelLeftEdge, LiveHud.LeftOfAlerts(_chips, metrics));
        LiveHudView.HandsScene scene = _view.Hands;
        scene.Shown = human && (left != null || right != null);
        if (!scene.Shown)
        {
            HideCellTexts();
            _view.ShowHands();
            return;
        }
        UpdateCells(scene);
        scene.Layout = _hands;
        scene.LeftActive = leftActive;
        scene.RightActive = rightActive;
        scene.HasLeft = left != null;
        scene.HasRight = right != null;
        HudRect leftSquare = LiveHud.HandPicture(_hands.Left, _hands.Unit), rightSquare = LiveHud.HandPicture(_hands.Right, _hands.Unit);
        Pictures(left, leftSquare, scene.Left);
        Pictures(right, rightSquare, scene.Right);
        Overlays(left, leftSquare, scene.LeftOverlays);
        Overlays(right, rightSquare, scene.RightOverlays);
        scene.Swap = LiveHud.SwapKey(_options.Bindings);
        scene.LeftMarked = left != null && LiveHud.HandMarked(_replica, left.Entry);
        scene.RightMarked = right != null && LiveHud.HandMarked(_replica, right.Entry);
        scene.HintLines = _hintLines;
        // the held-item hint strip over the row (slice D): the active hand's item and the keys for it; none with an empty active hand, or where the
        // strip is left out at this size (its stack count and hand are on the hand box)
        scene.Hint = _hands.Hint;
        scene.HintShare = _hands.HintShare;
        scene.HintUnit = UiTokens.Unit(metrics);
        Pictures(_hands.Hint.Width > 0f ? active : null, LiveHud.HintPictureSquare(_hands.Hint, scene.HintUnit, _hands.HintShare), scene.HintPictures);
        scene.HintShown = scene.HintPictures.Count > 0;
        // package 3: the name, cut to the width the layout left it
        scene.HintName = _hands.HintName > 0f ? LiveHud.HintName(heldName, _hands.HintName, _measureVerb) : null;
        _view.ShowHands();
    }

    // Slice 4A: under Tab on the human HUD the chat column and the character panel (TabInventory.Layout), clear of tg's entries still drawn there;
    // the panel's cells for tg's worn slots on the screen, and the own sprite facing the viewer as the game draws the body, clipped to its square.
    private void UpdatePanel(in HudMetrics metrics, bool human)
    {
        _panel = default;
        _spriteLook = TableRef.None;
        _spriteShown = null; // no pick on the sprite unless it is drawn this frame
        if (human && CursorMode)
        {
            _tgDrawn.Clear();
            foreach (Item item in _order)
            {
                LiveHudRole role = RoleNow(item);
                bool tg = role is LiveHudRole.Tg or LiveHudRole.CombatToggle or LiveHudRole.ZoneSelector or LiveHudRole.Memories or LiveHudRole.AreaCreator;
                // what tg draws now: a picture made at least once, or one of tg's entries that stands at its human place only under Tab
                if (!tg || !item.HasSpec || item.Hidden || item.Splash || item.Fullscreen || (item.Whole?.Texture == null && LiveHud.HumanPlace(role) == null))
                    continue;
                HudRect at = Place(item, metrics, 32, 32, 0, 0, out bool full);
                if (!full && at.Width < metrics.Width / 2f && at.Height < metrics.Height / 2f) // not a picture over the screen (a range, an overlay)
                    _tgDrawn.Add(at);
            }
            _panel = TabInventory.Layout(metrics, _hands, _layout, _tgDrawn);
        }
        ChatDock = TabInventory.ChatDock(CursorMode, human, _panel);
        _panelCells.Clear();
        if (!_panel.Shown || !_root.Visible)
        {
            _view.ShowPanel(_panelCells, 0f, null, default, default);
            return;
        }
        foreach (Item item in _wornSlots)
        {
            if (item.Cell >= 0)
                _panelCells.Add(_panel.Cells[item.Cell]);
        }
        Texture2D sprite = null;
        Rect2 shown = default, source = default;
        if (_textures is AppearanceTextures real && _replica.TryGet(_replica.OwnEntity, out ReplicaEntity me) && !me.State.Appearance.IsNone
            && real.TryGet(real.TablesOf(_replica), me.State.Appearance, ByondDir.South, _lastNow, out ComposedRaster raster, out GodotPicture picture) && raster.Image != null)
        {
            _spriteLook = me.State.Appearance;
            if (!ReferenceEquals(_spriteRaster?.Image, raster.Image)) // the pick's square, made once per picture (the compositor keeps its pictures)
                (_spriteRaster, _spritePick) = (raster, TabInventory.SpritePick(raster.Image, raster.OriginX, raster.OriginY));
            _spriteShown = _spritePick;
            _spriteRect = ScreenLoc.FitIcon(_panel.Sprite, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
            // a picture larger than the icon square (a big hat, a lying body's turned corners) is cut at the square, so it never covers a cell
            var whole = new Rect2(_spriteRect.Left, _spriteRect.Top, _spriteRect.Width, _spriteRect.Height);
            shown = whole.Intersection(new Rect2(_panel.Sprite.Left, _panel.Sprite.Top, _panel.Sprite.Width, _panel.Sprite.Height));
            float k = _spriteRect.Width / raster.Image.Width;
            if (shown.Size.X > 0f && shown.Size.Y > 0f && k > 0f)
            {
                sprite = picture.Texture;
                source = new Rect2((shown.Position - whole.Position) / k, shown.Size / k);
            }
        }
        _view.ShowPanel(_panelCells, _panel.CellUnit, sprite, shown, source);
    }

    private readonly List<HudRect> _panelCells = new();

    // Package 3: the bar's ten cells as the hotbar model says (LiveHud.CellLook), each item drawn as a hand box draws its item, with a stack's
    // amount (its maptext) on the item's square; tg's pocket and ID slots are only their cells' click areas (Collect).
    private void UpdateCells(LiveHudView.HandsScene scene)
    {
        HotbarModel bar = Hotbar ?? NoHotbar;
        var real = _textures as AppearanceTextures;
        for (int cell = 1; cell <= HotbarMessage.CellCount; cell++)
        {
            HudRect rect = LiveHud.BarCell(_hands, cell);
            HotbarCellLook look = LiveHud.CellLook(bar, cell, _options.Bindings, _lastNow);
            LiveHudView.CellScene shown = scene.Cells[cell - 1];
            shown.Rect = rect;
            shown.Look = look;
            shown.Pictures.Clear();
            HudRect square = LiveHud.HandPicture(rect, _hands.Unit);
            if (!look.Picture.IsNone && _textures.TryCompose(_replica, look.Picture, 2, _lastNow, out ComposedAppearance composed))
            {
                HudRect at = LiveHud.ChildPicture(square, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY);
                shown.Pictures.Add(new LiveHudView.Picture(composed.Texture, new Rect2(at.Left, at.Top, at.Width, at.Height)));
            }
            MaptextNode text = _cellTexts[cell - 1] ??= NewCellText(cell);
            if (real == null || look.Picture.IsNone)
                text.Visible = false;
            else
            {
                text.SetText(MaptextRenderer.Shared, real, _replica, look.Picture, new Vector2(square.Left, square.Bottom), square.Width / 32f);
                text.Modulate = new Color(1f, 1f, 1f, look.Alpha * look.PictureAlpha);
            }
        }
    }

    private static readonly HotbarModel NoHotbar = new();

    private MaptextNode NewCellText(int cell)
    {
        var text = new MaptextNode { Name = "Cell" + cell + "Maptext" };
        _view.AddChild(text); // over the view's own drawing (the cells), under tg's pictures
        return text;
    }

    private void HideCellTexts()
    {
        foreach (MaptextNode text in _cellTexts)
        {
            if (text != null)
                text.Visible = false;
        }
    }

    // Slice D: the hint strip's entries (use, drop, throw with their bound keys) and the widths of their key caps and verbs at the compact size, at
    // the HUD's unit: the strip's natural width (LiveHud.HintWidth), which the hands' layout fits over the row; package 3: the held item's name
    // (`name`, null: none) measured at the verbs' size (`named`, 0: none).
    private float HintNatural(float unit, string name, out float named)
    {
        _hintLines = LiveHud.HintLines(_options.Bindings);
        Font font = ThemeDB.FallbackFont;
        float k = unit * UiTokens.HintShare;
        int capSize = Math.Max(1, (int)Math.Round(UiTokens.ChipFont * k)), verbSize = Math.Max(1, (int)Math.Round(UiTokens.PromptFont * k));
        _hintWidths.Clear();
        foreach (PromptLine line in _hintLines)
            _hintWidths.Add((font.GetStringSize(line.Key, HorizontalAlignment.Left, -1, capSize).X + 2 * UiTokens.ChipPadX * k, font.GetStringSize(line.Text, HorizontalAlignment.Left, -1, verbSize).X));
        _measureVerb = text => font.GetStringSize(text, HorizontalAlignment.Left, -1, verbSize).X;
        named = LiveHud.HintName(name, float.PositiveInfinity, _measureVerb) is string whole ? _measureVerb(whole) : 0f;
        return LiveHud.HintWidth(unit, _hintWidths);
    }

    private Func<string, float> _measureVerb = _ => 0f;

    private List<PromptLine> _hintLines = new();
    private readonly List<(float Cap, float Verb)> _hintWidths = new();

    // tg's overlays on a hand box that the native box draws over the held items (LiveHud.HandOverlays: the handcuffs, the blocked mark), placed as
    // the box's own picture would hold them for an icon square at `square`.
    private void Overlays(Item hand, HudRect square, List<LiveHudView.Picture> into)
    {
        into.Clear();
        if (hand == null)
            return;
        (int shiftX, int shiftY) = _replica.TryGetAppearance(hand.Entry.Appearance, out AppearanceEntry holder) ? (holder.PixelX, holder.PixelY) : (0, 0);
        foreach (TableRef overlay in LiveHud.HandOverlays(_replica, hand.Entry))
        {
            if (!_textures.TryCompose(_replica, overlay, 2, _lastNow, out ComposedAppearance composed))
                continue;
            HudRect at = LiveHud.ChildPicture(square, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY, shiftX, shiftY);
            into.Add(new LiveHudView.Picture(composed.Texture, new Rect2(at.Left, at.Top, at.Width, at.Height)));
        }
    }

    // The pictures of the items a hand box holds (its vis_contents children, as HudPicker hits them), placed for an icon square at `square`.
    private void Pictures(Item hand, HudRect square, List<LiveHudView.Picture> into)
    {
        into.Clear();
        ScreenChild[] children = hand?.Entry.VisChildren?.Items;
        if (children == null)
            return;
        (int shiftX, int shiftY) = _replica.TryGetAppearance(hand.Entry.Appearance, out AppearanceEntry holder) ? (holder.PixelX, holder.PixelY) : (0, 0);
        foreach (ScreenChild child in children)
        {
            if (!_textures.TryCompose(_replica, child.Appearance, 2, _lastNow, out ComposedAppearance composed))
                continue;
            HudRect at = LiveHud.ChildPicture(square, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY, shiftX, shiftY);
            into.Add(new LiveHudView.Picture(composed.Texture, new Rect2(at.Left, at.Top, at.Width, at.Height)));
        }
    }

    private int _alertSlots;
    private float _alertFloor;

    // DV82 (ScreenLoc.PlaceAlert): how many of tg's alert slots are in use and where the right-hand column's next icon below them starts (its
    // icons on the centre row or under it: the mood, the pulse, the doll), so the alerts never reach it.
    private void MeasureAlertColumn(in HudMetrics metrics)
    {
        _alertSlots = 0;
        _alertFloor = metrics.Height;
        foreach (Item item in _order)
        {
            if (!item.HasSpec || item.Hidden)
                continue;
            int slot = ScreenLoc.AlertSlot(item.Spec);
            if (slot > 0)
                _alertSlots = Math.Max(_alertSlots, slot);
            else if (!item.Spec.HasRange && item.Spec.X.Anchor == ScreenAnchor.High && item.Spec.Y.Anchor == ScreenAnchor.Center && item.Spec.Y.Tile <= 0)
                _alertFloor = Math.Min(_alertFloor, ScreenLoc.Place(item.Spec, metrics, 32, 32, 0, 0, out _).Top);
        }
    }

    // An item's rectangle: tg's screen_loc, or an alert's place in its column (DV82); the picture's own square is where the place puts a 32 x 32 icon.
    // The health doll is re-placed (ui-overhaul.md section 6, LiveHud.Layout), its icon pixels kept: its zones, Collect and a click go through here.
    private HudRect Place(Item item, in HudMetrics metrics, int width, int height, int originX, int originY, out bool fullscreen)
    {
        if (item.Role == LiveHudRole.Doll)
        {
            fullscreen = false;
            return ScreenLoc.FitIcon(_layout.Doll, width, height, originX, originY);
        }
        if (RoleNow(item) == LiveHudRole.WornSlot) // slice 4A: tg's picture in its cell of the character panel, as a bar cell's item, its icon pixels kept
        {
            fullscreen = false;
            HudRect cell = _panel.CellAt(item.Cell);
            return ScreenLoc.FitIcon(cell.Width > 0f ? LiveHud.HandPicture(cell, _panel.CellUnit) : default, width, height, originX, originY);
        }
        if (RoleNow(item) == LiveHudRole.Alert && item.Cell < _chips.Count) // slice D: tg's picture in its chip; section 11: its visible art fitted to it
        {
            fullscreen = false;
            return LiveHud.ChipPicture(_chips[item.Cell], ChipArt(item), width, height, originX, originY);
        }
        if (LiveHud.VitalSquare(RoleNow(item), _layout) is HudRect vital) // slice D: tg's indicator in its place under the doll
        {
            fullscreen = false;
            return ScreenLoc.FitIcon(vital, width, height, originX, originY);
        }
        if (_humanHud && LiveHud.HumanPlace(RoleNow(item)) is ScreenLocSpec moved) // slice E: tg's combat toggle in the corner, the Tab row left of it
            return ScreenLoc.Place(moved, metrics, width, height, originX, originY, out fullscreen);
        int slot = ScreenLoc.AlertSlot(item.Spec);
        if (slot == 0 || width >= HudMetrics.GridTiles * 32)
            return ScreenLoc.Place(item.Spec, metrics, width, height, originX, originY, out fullscreen);
        fullscreen = false;
        HudRect icon = ScreenLoc.PlaceAlert(slot, _alertSlots, metrics, _alertFloor);
        float k = icon.Width / HudMetrics.IconPixels;
        return new HudRect(icon.Left - originX * k, icon.Bottom - (height - originY) * k, width * k, height * k);
    }

    // Section 11: a chip's visible art, found again only when the entry's icon, icon state, overlays, underlays or outline change (or the tables
    // restart); null keeps tg's whole square (nothing opaque, the icon unreadable, or still loading this frame)
    private HudRect? ChipArt(Item item)
    {
        if (_textures is not AppearanceTextures real || !_replica.TryGetAppearance(item.Entry.Appearance, out AppearanceEntry look))
            return null;
        float outline = LiveHud.OutlineReach(look);
        bool same = item.ArtKnown && item.ArtSnapshots == _replica.SnapshotsApplied && item.ArtIcon == look.Icon && item.ArtState == look.IconState
            && item.ArtOutline == outline && item.ArtLayers.Count == look.OverlayCount + look.UnderlayCount;
        for (int i = 0; same && i < look.OverlayCount; i++)
            same = item.ArtLayers[i] == look.Overlays[i];
        for (int i = 0; same && i < look.UnderlayCount; i++)
            same = item.ArtLayers[look.OverlayCount + i] == look.Underlays[i];
        if (same)
            return item.Art;
        if (!LiveHud.TryChipArtBounds(real.Compositor, real.TablesOf(_replica), item.Entry.Appearance, out HudRect? art))
            return null;
        item.ArtKnown = true;
        item.Art = art;
        item.ArtSnapshots = _replica.SnapshotsApplied;
        item.ArtIcon = look.Icon;
        item.ArtState = look.IconState;
        item.ArtOutline = outline;
        item.ArtLayers.Clear();
        if (look.OverlayCount > 0)
            item.ArtLayers.AddRange(look.Overlays);
        if (look.UnderlayCount > 0)
            item.ArtLayers.AddRange(look.Underlays);
        return art;
    }

    // D70's text labels: one column for all the doll's zones, whichever entries carry them (the doll's own appearance, or one entry per limb).
    private void UpdateLabels(in HudMetrics metrics)
    {
        if (_zoneStates.Count == 0 || !_options.ZoneLabels)
        {
            if (_labels != null)
                _labels.Visible = false;
            ZoneLabelText = string.Empty;
            return;
        }
        _labels ??= NewLabel(_root);
        var ordered = new List<(ZoneState State, HudRect Rect)>();
        for (int i = 0; i < _zoneStates.Count; i++)
            ordered.Add((_zoneStates[i], _zoneRects[i]));
        ordered.Sort((a, b) => Array.IndexOf(HealthZones.Zones, a.State.Zone).CompareTo(Array.IndexOf(HealthZones.Zones, b.State.Zone)));
        var text = new StringBuilder();
        string previous = null;
        foreach ((ZoneState state, HudRect _) in ordered)
        {
            if (state.Zone == previous)
                continue;
            previous = state.Zone;
            text.Append(HealthZones.Label(state)).Append('\n');
        }
        string lines = text.ToString().TrimEnd('\n');
        float left = _zoneRects.Min(r => r.Left), top = _zoneRects.Min(r => r.Top), bottom = _zoneRects.Max(r => r.Bottom);
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(metrics.Tile * 0.2f), 11, 30);
        _labels.AddThemeFontSizeOverride("font_size", fontSize);
        _labels.Visible = true;
        if (_labels.Text != lines)
            _labels.Text = lines;
        _labels.Size = new Vector2(metrics.Tile * 2.6f, Mathf.Max(bottom - top, 1f));
        // the column ends a gap left of the vitals (ui-overhaul.md 6)
        float right = _dollShown ? Math.Min(LiveHud.LeftOfVitals(_layout, metrics), LiveHud.LeftOfAlerts(_chips, metrics)) : left - 6f; // slice D: before alert chips left of them
        _labels.Position = new Vector2(right - _labels.Size.X, _dollShown ? _layout.Doll.Top : top);
        if (_dollShown) // the prompts beside the crosshair end before the labels too
        {
            float textWidth = _labels.GetThemeFont("font").GetMultilineStringSize(lines, HorizontalAlignment.Right, -1, fontSize).X;
            PanelLeftEdge = Math.Min(PanelLeftEdge, right - textWidth - UiTokens.DollPanelGap * UiTokens.Unit(metrics));
        }
        _labels.HorizontalAlignment = HorizontalAlignment.Right;
        ZoneLabelText = lines;
    }

    private void UpdateItem(Item item, in HudMetrics metrics, double now, ref int total)
    {
        if (!item.HasSpec || item.Hidden)
        {
            item.Container.Visible = false;
            return;
        }
        if (item.Analyze)
            Analyze(item);
        // tg's zone selector only while the cursor is free, slice E's hidden buttons never (LiveHud.Shown): hidden, Collect leaves it out too
        // slice E: an entry drawn as a chip draws nothing while it has none (tg fading its hunger bar out: the chip follows the fade's target)
        // slice 4A: a worn slot only in its cell of the character panel (none without the panel, or for a slot the panel does not show)
        item.Container.Visible = LiveHud.Shown(RoleNow(item), CursorMode) && !(RoleNow(item) == LiveHudRole.Alert && item.Cell >= _chips.Count)
            && !(RoleNow(item) == LiveHudRole.WornSlot && _panel.CellAt(item.Cell).Width <= 0f);
        if (!item.Container.Visible)
            return;
        float pixelScale = metrics.PixelScale;
        if (item.Zones.Count > 0)
        {
            UpdateZones(item, metrics, now, ref total);
            return;
        }
        if (IsHand(RoleNow(item)))
        {
            // drawn as the native hand box (LiveHudView), which Collect reports under this entry
            if (item.Whole != null)
                item.Whole.Visible = false;
            foreach (TextureRect big in item.Fitted)
                big.Visible = false;
            ShowMaptext(item, metrics, item.Entry.Appearance, HudLook.None); // slice C: a held stack's amount over the box's picture square, as tg puts it on its slot
            return;
        }
        if (RoleNow(item) == LiveHudRole.BarSlot)
        {
            // package 3: drawn as its hotbar cell (LiveHudView, from the hotbar model, its stack's amount too), which Collect reports under this entry
            if (item.Whole != null)
                item.Whole.Visible = false;
            foreach (TextureRect big in item.Fitted)
                big.Visible = false;
            if (item.Text != null)
                item.Text.Visible = false;
            return;
        }
        // M4v slice 4 (protocol version 11): the entry's flick plays over its picture, and its animate() chains move, turn, tint and fade it.
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        TableRef shown = item.Entry.Appearance;
        double seconds = now;
        if (FlickNow(item, tick, out double age))
        {
            shown = item.Entry.FlickAppearance;
            seconds = age;
        }
        var real = _textures as AppearanceTextures;
        if (IsMultitoolArrow(shown) && _replica.TryGetAppearance(shown, out AppearanceEntry arrow))
        {
            if (item.Whole != null) item.Whole.Visible = false;
            item.Scanner ??= new MultitoolIndicator();
            if (item.Scanner.GetParent() == null) item.Container.AddChild(item.Scanner);
            Vector2 center = Input.MouseMode == Input.MouseModeEnum.Captured ? GetViewport().GetVisibleRect().Size / 2 : GetViewport().GetMousePosition();
            uint rgba = arrow.Rgba;
            item.Scanner.Set(center, ApcIndicator.Angle(Affine.FromMatrix(arrow.Transform), CameraBearing()),
                new Color(((rgba >> 24) & 255) / 255f, ((rgba >> 16) & 255) / 255f, ((rgba >> 8) & 255) / 255f), metrics.TextScale);
            item.Scanner.Visible = true;
            return;
        }
        if (item.Scanner != null) item.Scanner.Visible = false;
        AnimatedLook look = AnimatedLook.None;
        bool playing = real != null && HudLook.TryPlay(_replica, item.Entry, tick, out look);
        // Version 26 (D156): its filters' chains and its vis_contents children's (the health doll's limbs pulse their wound outlines).
        Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks = null;
        bool filtering = real != null && item.Entry.Animation != null
            && FilterPlayback.TryEvaluate(real.TablesOf(_replica), shown, item.Entry.Animation, tick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, out filterLooks);
        // DV575: an item too big for its slot's square is drawn on its own, fitted into it (UpdateFitted); the slot's picture leaves it out
        List<ComposedAppearance> fitted = real != null && item.Entry.VisChildren != null ? HudPictures.OversizedChildren(_replica, _textures, item.Entry, seconds) : null;
        IAppearanceSource tables = null;
        if (fitted is { Count: > 0 })
            shown = HudPictures.WithoutOversized(real, _replica, shown, out tables);
        ComposedAppearance composed;
        if (playing || filtering || tables != null)
        {
            // Composed with the root opaque: a chain that ends transparent (a fullscreen's fade) draws its alpha over the picture (as a visual's, slice 3).
            if (!real.TryGet(tables ?? real.TablesOf(_replica), shown, 2, seconds, out ComposedRaster raster, out GodotPicture picture, rootOffset: true, rootOpaque: playing, filterLooks))
            {
                if (item.Whole != null)
                    item.Whole.Visible = false;
                ShowMaptext(item, metrics, shown, HudLook.None);
                return;
            }
            composed = new ComposedAppearance(picture.Texture, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
        }
        else if (!_textures.TryCompose(_replica, shown, 2, seconds, out composed))
        {
            if (item.Whole != null)
                item.Whole.Visible = false;
            ShowMaptext(item, metrics, shown, HudLook.None); // a screen object that is only text (tg's /screen/text)
            return;
        }
        item.Whole ??= NewRect(item.Container, null);
        HudRect rect = Place(item, metrics, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY, out bool fullscreen);
        if (item.Splash && fullscreen && !item.Spec.HasRange)
        {
            // tg's cinematic (SPLASHSCREEN_PLANE: the station picture, 512 x 512, over tg's square view): fitted to the screen's height, in its middle
            float side = Math.Min(metrics.Width, metrics.Height);
            rect = new HudRect((metrics.Width - side) / 2f, (metrics.Height - side) / 2f, side, side);
        }
        Apply(item.Whole, composed.Texture, rect);
        HudLook moved = playing ? LiveHud.ChipLook(RoleNow(item), HudLook.Of(look, pixelScale), item.Role) : HudLook.None; // slice D: a chip's picture stays in it
        if (playing)
        {
            float perPixel = composed.PixelWidth > 0 ? rect.Width / composed.PixelWidth : pixelScale;
            item.Whole.PivotOffset = new Vector2((composed.OriginX + 16) * perPixel, rect.Height - (composed.OriginY + 16) * perPixel);
            item.Whole.Position += new Vector2(moved.MoveX, moved.MoveY);
        }
        item.Whole.Rotation = moved.Rotation;
        item.Whole.Scale = new Vector2(moved.ScaleX, moved.ScaleY);
        // D70: a limit on the strength of fullscreen effects (flashes, the damage and crit overlays).
        item.Whole.Modulate = new Color(moved.Red, moved.Green, moved.Blue, moved.Alpha * (fullscreen ? _options.FullscreenStrength : 1f));
        item.Whole.Visible = !fullscreen || _options.FullscreenStrength > 0.001f;
        UpdateFitted(item, fitted, rect, composed);
        item.LastRect = rect;
        item.OriginX = composed.OriginX;
        item.OriginY = composed.OriginY;
        item.Fullscreen = fullscreen;
        ShowMaptext(item, metrics, shown, moved);
        // tg's SPLASHSCREEN_PLANE (the cinematic and its black backdrop): its own layer, never dimmed (D70 is for effects)
        Control parent = item.Splash ? _splashRoot : fullscreen ? _fullscreenRoot : _root;
        if (item.Splash)
        {
            item.Whole.Modulate = new Color(moved.Red, moved.Green, moved.Blue, moved.Alpha);
            item.Whole.MouseFilter = Control.MouseFilterEnum.Stop; // nothing hidden under it takes a click
        }
        item.Whole.Visible = item.Splash || item.Whole.Visible;
        if (item.Container.GetParent() != parent)
        {
            item.Container.Reparent(parent, keepGlobalTransform: false);
            _sortDirty = true;
        }
        _ = pixelScale;
    }

    // DV575: the oversized items of a slot, each fitted into the slot's icon square (moved by its own pixel offset, as BYOND moves its children)
    private void UpdateFitted(Item item, List<ComposedAppearance> fitted, HudRect rect, in ComposedAppearance holder)
    {
        int count = fitted?.Count ?? 0;
        for (int i = 0; i < Math.Max(count, item.Fitted.Count); i++)
        {
            if (i >= count)
            {
                item.Fitted[i].Visible = false;
                continue;
            }
            if (i >= item.Fitted.Count)
                item.Fitted.Add(NewRect(item.Container, null));
            float k = holder.PixelWidth > 0 ? rect.Width / holder.PixelWidth : 0f;
            (int shiftX, int shiftY) = _replica.TryGetAppearance(item.Entry.Appearance, out AppearanceEntry own) ? (own.PixelX, own.PixelY) : (0, 0);
            var square = new Rect2(rect.Left + (holder.OriginX + shiftX) * k, rect.Top + rect.Height - (holder.OriginY + shiftY + 32) * k, 32f * k, 32f * k);
            Rect2 at = HudPictures.FittedIn(square, fitted[i]);
            Apply(item.Fitted[i], fitted[i].Texture, new HudRect(at.Position.X, at.Position.Y, at.Size.X, at.Size.Y));
            item.Fitted[i].Modulate = item.Whole.Modulate;
            item.Fitted[i].Visible = item.Whole.Visible;
        }
    }

    private bool IsMultitoolArrow(TableRef shown)
        => _replica.TryGetAppearance(shown, out AppearanceEntry entry) && _replica.ResolveString(entry.IconState) == "multitool_arrow";

    // The compass bearing (clockwise from north, radians) the 3D camera looks along: north is -Z, east +X.
    private float CameraBearing()
    {
        Camera3D camera = GetViewport()?.GetCamera3D();
        if (camera == null)
            return 0f;
        Vector3 right = camera.GlobalTransform.Basis.X; // the yaw's: past straight down the forward points back (T5a)
        return (float)LookDown.CompassYaw(right.X, right.Z);
    }

    // Protocol version 18: the maptexts of the entry's picture (its own, its overlays' and the item in its hand), over it, at the HUD's pixel size.
    // The box sits at the icon's lower left corner, which is the entry's tile; the entry's animate() chain moves the text with the picture.
    // Slice C: a hand box's or (slice 4A) a worn slot's texts stand on its picture square there and scale with it (LiveHud.MaptextSquare).
    private void ShowMaptext(Item item, in HudMetrics metrics, TableRef shown, in HudLook moved)
    {
        if (_textures is not AppearanceTextures real)
            return;
        MaptextRenderer renderer = MaptextRenderer.Shared;
        if (item.Text == null && !renderer.Has(real, _replica, shown))
            return;
        if (item.Text == null)
        {
            item.Text = new MaptextNode { Name = "Maptext" };
            item.Container.AddChild(item.Text);
        }

        LiveHudRole role = RoleNow(item);
        HudRect? square = role == LiveHudRole.Alert && item.Cell < _chips.Count ? LiveHud.AlertPicture(_chips[item.Cell])
            : LiveHud.VitalSquare(role, _layout) ?? LiveHud.MaptextSquare(role, _hands, _panel, item.Cell);
        HudRect tile = square ?? Place(item, metrics, 32, 32, 0, 0, out _);
        item.Text.Modulate = item.Whole != null && !IsHand(role) ? item.Whole.Modulate : Colors.White;
        item.Text.SetText(renderer, real, _replica, shown, new Vector2(tile.Left + moved.MoveX, tile.Top + tile.Height + moved.MoveY), square is HudRect s ? s.Width / 32f : metrics.PixelScale);
    }

    // The appearance is read again whenever its entry changed: does it hold the doll's zones?
    private void Analyze(Item item)
    {
        item.Analyze = false;
        item.Role = LiveHud.RoleOf(_replica, item.Entry);
        var found = new List<FoundZone>();
        item.Indicator = HudReader.Collect(_replica, item.Entry.Appearance, found);
        // Rebuild the zone rects only when the set of zones changed.
        bool same = found.Count == item.Zones.Count;
        for (int i = 0; same && i < found.Count; i++)
            same = item.Zones[i].Zone.Zone.Zone == found[i].Zone.Zone;
        if (!same)
        {
            foreach (ZoneNode node in item.Zones)
                node.Rect.QueueFree();
            item.Zones.Clear();
            foreach (FoundZone f in found)
            {
                var material = new ShaderMaterial { Shader = _shader };
                var node = new ZoneNode { Rect = NewRect(item.Container, material), Material = material };
                item.Zones.Add(node);
            }
            if (item.Whole != null)
                item.Whole.Visible = false;
        }
        for (int i = 0; i < found.Count; i++)
        {
            item.Zones[i].Zone = found[i];
        }
    }

    private void UpdateZones(Item item, in HudMetrics metrics, double now, ref int total)
    {
        float cell = Mathf.Max(2f, item.Role == LiveHudRole.Doll ? _layout.DollScale : metrics.PixelScale); // the hatching's cell: one of the doll's tg pixels
        item.IconSquare = Place(item, metrics, 32, 32, 0, 0, out _);
        // Version 26 (D156): the doll's limbs' filter chains (tg's wound outline pulsing, screen_objects.dm:1023), carried in the doll's entry
        var real = _textures as AppearanceTextures;
        ReplicaTables tables = real?.TablesOf(_replica);
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks = null;
        if (real != null && item.Entry.Animation != null)
            FilterPlayback.TryEvaluate(tables, item.Entry.Appearance, item.Entry.Animation, tick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, out filterLooks);
        foreach (ZoneNode node in item.Zones)
        {
            ZoneState state = node.Zone.Zone;
            // ui-overhaul.md 6: the limb alone, its filters left out, is what is tinted (a steady colour, its brightest channel the ramp's
            // reference); tg's picture with its filters (the wound outline, pulsing) is drawn around it as tg draws it
            ComposedAppearance limb, shown;
            if (real != null && tables.TryGetAppearance(node.Zone.Node, out AppearanceEntry limbEntry))
            {
                bool filtered = limbEntry.Filters is { Count: > 0 } || filterLooks?.ContainsKey(limbEntry) == true;
                if (!real.TryGet(tables, node.Zone.Node, 2, now, out ComposedRaster limbRaster, out GodotPicture limbPicture, rootOffset: true, filterLooks: filtered ? node.NoFilters(limbEntry) : null))
                {
                    node.Rect.Visible = false;
                    continue;
                }
                limb = new ComposedAppearance(limbPicture.Texture, limbRaster.Image.Width, limbRaster.Image.Height, limbRaster.OriginX, limbRaster.OriginY);
                if (!ReferenceEquals(node.BrightestOf, limbRaster.Image))
                    (node.BrightestOf, node.Brightest) = (limbRaster.Image, HealthZones.Brightest(limbRaster.Image)); // once per picture, not per frame
                shown = filtered && real.TryGet(tables, node.Zone.Node, 2, now, out ComposedRaster raster, out GodotPicture picture, rootOffset: true, filterLooks: filterLooks)
                    ? new ComposedAppearance(picture.Texture, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY)
                    : limb;
            }
            else if (_textures.TryCompose(_replica, node.Zone.Node, 2, now, out limb))
            {
                shown = limb;
                node.BrightestOf = null;
                node.Brightest = 0f;
            }
            else
            {
                node.Rect.Visible = false;
                continue;
            }
            HudRect rect = Place(item, metrics, shown.PixelWidth, shown.PixelHeight, shown.OriginX, shown.OriginY, out _);
            HudRect own = Place(item, metrics, limb.PixelWidth, limb.PixelHeight, limb.OriginX, limb.OriginY, out _);
            Apply(node.Rect, shown.Texture, rect);
            node.Rect.Visible = true;
            node.Material.SetShaderParameter("limb", limb.Texture);
            node.Material.SetShaderParameter("limb_rect", rect.Width > 0 && rect.Height > 0
                ? new Vector4((own.Left - rect.Left) / rect.Width, (own.Top - rect.Top) / rect.Height, own.Width / rect.Width, own.Height / rect.Height)
                : new Vector4(0, 0, 1, 1));
            _zoneStates.Add(state);
            _zoneRects.Add(own);
            HatchPattern hatch = HealthZones.Pattern(_options.HatchCues ? HealthZones.Hatch(state) : HatchStyle.None);
            Rgb? palette = HealthZones.PaletteColor(_options.Colorblind, state);
            node.Material.SetShaderParameter("shape", (int)hatch.Shape);
            node.Material.SetShaderParameter("period", hatch.Period);
            node.Material.SetShaderParameter("width", hatch.Width);
            node.Material.SetShaderParameter("cell", cell);
            node.Material.SetShaderParameter("use_tint", palette.HasValue);
            node.Material.SetShaderParameter("tint_ref", _options.Colorblind == ColorblindMode.Off ? node.Brightest : 0f);
            Color tint = palette.HasValue ? new Color(palette.Value.R, palette.Value.G, palette.Value.B) : Colors.White;
            node.Material.SetShaderParameter("tint", tint);
            // untinted (tg's own disabled picture, dark grey): the light lines show on it
            float light = palette.HasValue ? 0.299f * tint.R + 0.587f * tint.G + 0.114f * tint.B : 0.3f;
            node.Material.SetShaderParameter("hatch_color", ChatLines.Rgba(light > 0.45f ? UiTokens.HatchDark : UiTokens.HatchLight));
            if (state.Kind == ZoneKind.Level)
                total += state.Level;
            else if (state.Kind == ZoneKind.Dead)
                total += HealthZones.MaxLevel;
        }
        // slice T5c: tg's hover picture of the eyes or the mouth under the pointer, over the limbs on the doll's own icon square
        string face = item.Role != LiveHudRole.Doll ? null : DollHover switch { AimZone.Eyes => "eyes", AimZone.Mouth => "mouth", _ => null };
        Texture2D facePicture = face != null && real != null ? FacePicture(real.Library, face) : null;
        if (facePicture != null)
        {
            item.Face ??= NewRect(item.Container, null);
            item.Container.MoveChild(item.Face, -1); // over zone pictures made since
            item.Face.Modulate = new Color(1f, 1f, 1f, 128f / 255f); // tg's alpha of the hover overlay
            Apply(item.Face, facePicture, item.IconSquare);
        }
        if (item.Face != null)
            item.Face.Visible = facePicture != null;
    }

    // tg's zone selector's hover picture of a zone (screen_gen.dmi), loaded once
    // slice D: whether an icon file has a state (LiveHud.IndicatorShown); without the real icons (tests, placeholders) every state counts
    private bool StateExists(string icon, string state)
        => _textures is not AppearanceTextures real || real.Library.GetSheet(icon) is not IconSheet sheet || sheet.TryGetState(state, out _);

    private static Texture2D FacePicture(IconLibrary icons, string state)
    {
        if (!FacePictures.TryGetValue(state, out Texture2D texture))
            FacePictures[state] = texture = icons.GetSheet("icons/hud/screen_gen.dmi") is IconSheet sheet && sheet.TryGetState(state, out IIconState found)
                ? ImageTexture.CreateFromImage(AppearanceTextures.ToImage(found.Get(0, 0)))
                : null;
        return texture;
    }

    private static TextureRect NewRect(Control parent, Material material)
    {
        var rect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = material,
        };
        parent.AddChild(rect);
        return rect;
    }

    private static Label NewLabel(Control parent)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.Off };
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.97f, 1f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 5);
        parent.AddChild(label);
        return label;
    }

    // M4v slice 4: the entry's last flick while it plays at the tick, and the seconds into it. Its length (the flicked states played once) is
    // found once per flick, as the world's entities find theirs (EntityLayer.FlickNow); while an icon is missing or loading it is looked for again.
    private bool FlickNow(Item item, double tick, out double seconds)
    {
        seconds = 0;
        TableRef flicked = item.Entry.FlickAppearance;
        if (flicked.IsNone || _textures is not AppearanceTextures real)
            return false;
        if (flicked != item.FlickSeen || item.Entry.FlickTick != item.FlickSeenTick || item.FlickLength <= 0)
        {
            item.FlickSeen = flicked;
            item.FlickSeenTick = item.Entry.FlickTick;
            ReplicaTables tables = real.TablesOf(_replica);
            item.FlickLength = real.Compositor.TryCompose(tables, flicked, ByondDir.South, 0, out _)
                ? FlickPlayback.LengthOf(real.Compositor.AnimatedStates(tables, flicked, ByondDir.South))
                : 0;
        }
        return FlickPlayback.TryAge(item.Entry.FlickTick, tick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, item.FlickLength, out seconds);
    }

    private static void Apply(TextureRect target, Texture2D texture, HudRect rect)
    {
        if (target.Texture != texture)
            target.Texture = texture;
        target.Position = new Vector2(rect.Left, rect.Top);
        target.Size = new Vector2(rect.Width, rect.Height);
    }

    private sealed class Item
    {
        public Control Container;
        public ScreenEntry Entry;
        public ScreenLocSpec Spec;
        public bool HasSpec;
        public float Layer;
        public bool Splash; // tg's SPLASHSCREEN_PLANE (syndicate N1a: the cinematic)
        public bool Analyze = true;
        public int Indicator = -1;
        public TextureRect Whole;
        public readonly List<TextureRect> Fitted = new(); // DV575: the slot's items too big for it, fitted into its square
        public LiveHudView.KeyCap KeyHint; // its action button's key cap
        public MultitoolIndicator Scanner;
        public MaptextNode Text; // protocol version 18: the entry's maptexts
        public TableRef FlickSeen; // M4v slice 4: the flick whose length FlickLength is
        public long FlickSeenTick;
        public double FlickLength;
        public HudRect LastRect;
        public bool Hidden;
        public bool Fullscreen;
        public int OriginX, OriginY;
        public LiveHudRole Role; // ui-overhaul.md section 6: the doll and the combat toggle are re-placed, the zone selector shown in cursor mode only
        public HudRect IconSquare; // a doll entry's own 32 x 32 icon square, where it is drawn (what Collect reports)
        public int Cell; // slice 4A: a worn slot's cell of the character panel (-1: none); package 3: a bar slot's cell (8 to 10); slice D: an alert's chip
        public TextureRect Face; // slice T5c: tg's eyes or mouth hover picture on the doll
        public readonly List<ZoneNode> Zones = new();
        // section 11: a chip's visible art (LiveHud.TryChipArtBounds), and the icon, icon state, overlays and underlays it was found for
        public bool ArtKnown;
        public HudRect? Art;
        public int ArtSnapshots;
        public float ArtOutline;
        public TableRef ArtIcon, ArtState;
        public readonly List<TableRef> ArtLayers = new();
    }

    private sealed class ZoneNode
    {
        public TextureRect Rect;
        public ShaderMaterial Material;
        public FoundZone Zone;
        public Anomaly.Client.Shared.Icons.Raster BrightestOf; // the limb's picture Brightest was measured on
        public float Brightest;
        private AppearanceEntry _noFiltersOf;
        private Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> _noFilters;

        // the compositor's filter looks that draw the limb without its filters (one dictionary per limb, so its pictures are cached)
        public Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> NoFilters(AppearanceEntry limb)
        {
            if (!ReferenceEquals(_noFiltersOf, limb))
            {
                _noFiltersOf = limb;
                _noFilters = new Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>>(ReferenceEqualityComparer.Instance) { [limb] = Array.Empty<FilterEntry>() };
            }
            return _noFilters;
        }
    }
}
