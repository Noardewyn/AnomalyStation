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
/// (the shape cue; a shader keyed by <see cref="HatchStyle"/>) and, under a colourblind palette, in the palette's colour for
/// the level, and a text label per zone lists its name and level beside the doll. Fullscreen effects (the damage and crit
/// overlays) are limited by the player's strength setting.
/// </para>
/// </summary>
public partial class HudLayer : CanvasLayer, IHudSurface
{
    private const string ShaderCode = @"
shader_type canvas_item;
uniform int style = 0;
uniform bool use_tint = false;
uniform vec4 tint : source_color = vec4(1.0);
uniform vec4 hatch_color : source_color = vec4(0.0, 0.0, 0.0, 0.8);
uniform float cell = 2.0;

float diag(vec2 p, float dir, float period, float width) {
    float v = mod(p.x + dir * p.y, period);
    return v < width ? 1.0 : 0.0;
}

float hatch(vec2 p) {
    float c = cell;
    if (style == 1) return diag(p, 1.0, 5.0 * c, 1.0 * c);
    if (style == 2) return diag(p, 1.0, 3.0 * c, 1.0 * c);
    if (style == 3) return max(diag(p, 1.0, 4.0 * c, 1.0 * c), diag(p, -1.0, 4.0 * c, 1.0 * c));
    if (style == 4) return max(diag(p, 1.0, 3.0 * c, 1.2 * c), diag(p, -1.0, 3.0 * c, 1.2 * c));
    if (style == 5) return max(diag(p, 1.0, 3.0 * c, 2.0 * c), diag(p, -1.0, 3.0 * c, 2.0 * c));
    if (style == 6) { vec2 q = mod(p, 4.0 * c) - 2.0 * c; return length(q) < 0.9 * c ? 1.0 : 0.0; }
    if (style == 7) return mod(p.x, 4.0 * c) < 1.6 * c ? 1.0 : 0.0;
    if (style == 8) return mod(p.y, 3.0 * c) < 1.2 * c ? 1.0 : 0.0;
    return 0.0;
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    vec3 rgb = tex.rgb;
    if (use_tint) {
        float l = dot(rgb, vec3(0.299, 0.587, 0.114));
        rgb = tint.rgb * (0.55 + 0.6 * l);
    }
    float h = hatch(FRAGCOORD.xy);
    COLOR = vec4(mix(rgb, hatch_color.rgb, h * hatch_color.a), tex.a);
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

    // M4a P7 (D131): the entries of an open storage and of the inventory are drawn by the native windows (ContainerWindow, InventoryPanel), not at their screen_loc.
    private static bool InWindow(ScreenEntry entry) => entry.Group is HudGroup.Storage or HudGroup.ToggleableInventory;

    /// <inheritdoc />
    public void Collect(List<HudDrawn> bottomToTop)
    {
        foreach (Item item in _order)
        {
            if (!item.Container.Visible || !item.HasSpec || item.Hidden)
                continue;
            if (item.Zones.Count > 0)
            {
                foreach (ZoneNode node in item.Zones)
                {
                    if (node.Rect.Visible)
                        bottomToTop.Add(new HudDrawn(item.Entry.Id, new HudRect(node.Rect.Position.X, node.Rect.Position.Y, node.Rect.Size.X, node.Rect.Size.Y), null, 0, 0));
                }
            }
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

    // The owner (2026-10-29): the shown item action buttons in reading order, each with its key (ActionButton1-9) at its bottom right
    private void NumberActionButtons()
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
            item.KeyHint ??= NewLabel(item.Container);
            Label hint = item.KeyHint;
            hint.Text = KeyChips.Label(keys[0]);
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.VerticalAlignment = VerticalAlignment.Bottom;
            hint.AddThemeFontSizeOverride("font_size", Math.Max(10, (int)(item.LastRect.Height * 0.3f)));
            float inset = item.LastRect.Width * 0.06f;
            hint.Position = new Vector2(item.LastRect.Left, item.LastRect.Top);
            hint.Size = new Vector2(item.LastRect.Width - inset, item.LastRect.Height - inset * 0.5f);
            hint.Visible = true;
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
            // tg's layer order within each root (the HUD's, the fullscreens' and the splash's canvas layers)
            int hudIndex = 0, fullscreenIndex = 0, splashIndex = 0;
            foreach (Item ordered in _order)
            {
                Node parent = ordered.Container.GetParent();
                parent.MoveChild(ordered.Container, parent == _fullscreenRoot ? fullscreenIndex++ : parent == _splashRoot ? splashIndex++ : hudIndex++);
            }
            _sortDirty = false;
        }
        _zoneStates.Clear();
        _zoneRects.Clear();
        MeasureAlertColumn(metrics);
        int total = 0;
        foreach (Item item in _order)
            UpdateItem(item, metrics, nowSeconds, ref total);
        NumberActionButtons();
        UpdateLabels(metrics);
        if (_zoneStates.Count > 0)
        {
            if (_lastDollTotal >= 0 && total > _lastDollTotal)
                DamageTaken?.Invoke();
            _lastDollTotal = total;
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
    private HudRect Place(Item item, in HudMetrics metrics, int width, int height, int originX, int originY, out bool fullscreen)
    {
        int slot = ScreenLoc.AlertSlot(item.Spec);
        if (slot == 0 || width >= HudMetrics.GridTiles * 32)
            return ScreenLoc.Place(item.Spec, metrics, width, height, originX, originY, out fullscreen);
        fullscreen = false;
        HudRect icon = ScreenLoc.PlaceAlert(slot, _alertSlots, metrics, _alertFloor);
        float k = icon.Width / HudMetrics.IconPixels;
        return new HudRect(icon.Left - originX * k, icon.Bottom - (height - originY) * k, width * k, height * k);
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
        _labels.Position = new Vector2(left - _labels.Size.X - 6f, top);
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
        item.Container.Visible = true;
        if (item.Analyze)
            Analyze(item);
        float pixelScale = metrics.PixelScale;
        if (item.Zones.Count > 0)
        {
            UpdateZones(item, metrics, now, ref total);
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
        HudLook moved = playing ? HudLook.Of(look, pixelScale) : HudLook.None;
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
        Vector3 forward = -camera.GlobalTransform.Basis.Z;
        return Mathf.Atan2(forward.X, -forward.Z);
    }

    // Protocol version 18: the maptexts of the entry's picture (its own, its overlays' and the item in its hand), over it, at the HUD's pixel size.
    // The box sits at the icon's lower left corner, which is the entry's tile; the entry's animate() chain moves the text with the picture.
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

        HudRect tile = Place(item, metrics, 32, 32, 0, 0, out _);
        item.Text.Modulate = item.Whole != null ? item.Whole.Modulate : Colors.White;
        item.Text.SetText(renderer, real, _replica, shown, new Vector2(tile.Left + moved.MoveX, tile.Top + tile.Height + moved.MoveY), metrics.PixelScale);
    }

    // The appearance is read again whenever its entry changed: does it hold the doll's zones?
    private void Analyze(Item item)
    {
        item.Analyze = false;
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
        float cell = Mathf.Max(2f, metrics.PixelScale * 1.1f);
        // Version 26 (D156): the doll's limbs' filter chains (tg's wound outline pulsing, screen_objects.dm:1023), carried in the doll's entry
        var real = _textures as AppearanceTextures;
        double tick = double.IsNaN(RenderTick) ? _replica.Tick : RenderTick;
        Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks = null;
        if (real != null && item.Entry.Animation != null)
            FilterPlayback.TryEvaluate(real.TablesOf(_replica), item.Entry.Appearance, item.Entry.Animation, tick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, out filterLooks);
        foreach (ZoneNode node in item.Zones)
        {
            ZoneState state = node.Zone.Zone;
            ComposedAppearance composed;
            if (filterLooks != null && real.TryGet(real.TablesOf(_replica), node.Zone.Node, 2, now, out ComposedRaster raster, out GodotPicture picture, rootOffset: true, filterLooks: filterLooks))
            {
                composed = new ComposedAppearance(picture.Texture, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
            }
            else if (!_textures.TryCompose(_replica, node.Zone.Node, 2, now, out composed))
            {
                node.Rect.Visible = false;
                continue;
            }
            HudRect rect = ScreenLoc.Place(item.Spec, metrics, composed.PixelWidth, composed.PixelHeight, composed.OriginX, composed.OriginY, out _);
            Apply(node.Rect, composed.Texture, rect);
            node.Rect.Visible = true;
            _zoneStates.Add(state);
            _zoneRects.Add(rect);
            HatchStyle hatch = _options.HatchCues ? HealthZones.Hatch(state) : HatchStyle.None;
            Rgb? palette = HealthZones.PaletteColor(_options.Colorblind, state);
            node.Material.SetShaderParameter("style", (int)hatch);
            node.Material.SetShaderParameter("cell", cell);
            node.Material.SetShaderParameter("use_tint", palette.HasValue);
            Color tint = palette.HasValue ? new Color(palette.Value.R, palette.Value.G, palette.Value.B) : Colors.White;
            node.Material.SetShaderParameter("tint", tint);
            float light = palette.HasValue ? 0.299f * tint.R + 0.587f * tint.G + 0.114f * tint.B : 0.5f;
            node.Material.SetShaderParameter("hatch_color", light > 0.45f ? new Color(0, 0, 0, 0.8f) : new Color(1, 1, 1, 0.85f));
            if (state.Kind == ZoneKind.Level)
                total += state.Level;
            else if (state.Kind == ZoneKind.Dead)
                total += HealthZones.MaxLevel;
        }
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
        public Label KeyHint; // its action button's number key
        public MultitoolIndicator Scanner;
        public MaptextNode Text; // protocol version 18: the entry's maptexts
        public TableRef FlickSeen; // M4v slice 4: the flick whose length FlickLength is
        public long FlickSeenTick;
        public double FlickLength;
        public HudRect LastRect;
        public bool Hidden;
        public bool Fullscreen;
        public int OriginX, OriginY;
        public readonly List<ZoneNode> Zones = new();
    }

    private sealed class ZoneNode
    {
        public TextureRect Rect;
        public ShaderMaterial Material;
        public FoundZone Zone;
    }
}
