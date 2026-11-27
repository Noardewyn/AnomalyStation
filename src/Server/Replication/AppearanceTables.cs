using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Owner: M2 P5 (docs/ai/specs/replication.md section 6). Appearance interning, BYOND's own model: an appearance is immutable and
// interned, an atom points at one. The replicator reads an atom's builtin appearance vars and its overlays and underlays
// (Anomaly.Core.DmShim.IDmAppearance), builds the wire entry and looks it up by value: one id per distinct appearance,
// append-only for the run, new rows go out once, in the Delta that first uses them.

/// <summary>
/// One set of appearance, resource and string tables: the public tables every connection shares, or one session's private
/// tables (D40: what only its owner may see, so a health level can never enter a table another session receives). Pure
/// data and reads of <see cref="IDmAppearance"/>; it never touches the world.
/// </summary>
internal sealed class AppearanceTables
{
    private const int MaxDepth = 6;

    private readonly bool _private;
    private readonly Dictionary<string, uint> _resourceIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _stringIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<AppearanceEntry, uint> _appearanceIndex = new(EntryComparer.Instance);
    private readonly Dictionary<string, (uint Rgba, bool Valid)> _colors = new(StringComparer.Ordinal);
    private readonly AppearanceEntry[] _scratch = new AppearanceEntry[MaxDepth + 1];

    public AppearanceTables(bool isPrivate)
    {
        _private = isPrivate;
        for (int i = 0; i < _scratch.Length; i++) _scratch[i] = new AppearanceEntry();
    }

    /// <summary>The resource table so far (index 1 is element 0).</summary>
    public List<string> Resources { get; } = new();

    /// <summary>The string table so far.</summary>
    public List<string> Strings { get; } = new();

    /// <summary>The appearance table so far.</summary>
    public List<AppearanceEntry> Appearances { get; } = new();

    /// <summary>How many entries of each table existed when <see cref="BeginTick"/> last ran (what a Delta already sent).</summary>
    public int ResourcesBefore { get; private set; }

    public int StringsBefore { get; private set; }

    public int AppearancesBefore { get; private set; }

    /// <summary>Entries not interned because a table was full or a string over its cap (a counter for the metrics).</summary>
    public int Overflows { get; private set; }

    /// <summary>Marks the tables' current lengths: the entries added after this call are the tick's new ones.</summary>
    public void BeginTick()
    {
        ResourcesBefore = Resources.Count;
        StringsBefore = Strings.Count;
        AppearancesBefore = Appearances.Count;
    }

    /// <summary>True when an entry was added since <see cref="BeginTick"/>.</summary>
    public bool HasNew => Resources.Count > ResourcesBefore || Strings.Count > StringsBefore || Appearances.Count > AppearancesBefore;

    /// <summary>
    /// The id of <paramref name="source"/>'s appearance, 0 when it shows nothing (no icon, no overlays and no underlays: an
    /// entity with such an appearance has none). <paramref name="keepDir"/> keeps the appearance's own <c>dir</c>: false for
    /// an entity (its <see cref="NetField.Dir"/> travels on its own), true for a screen object.
    /// </summary>
    public uint InternTop(IDmAppearance source, bool keepDir)
    {
        if (source == null) return 0;
        AppearanceEntry e = _scratch[0];
        Reset(e);
        Fill(e, source, 0, keepDir);
        if (e.Icon.IsNone && e.Maptext.IsNone && e.OverlayCount == 0 && e.UnderlayCount == 0) return 0;
        return Find(e);
    }

    /// <summary>
    /// Version 29: the id of the appearance of <paramref name="source"/> as a plane that draws nothing of its own but filters what is
    /// under it (tg's game plane master controller): as <see cref="InternTop"/>, but an appearance with filters and no icon is kept;
    /// 0 when it has neither.
    /// </summary>
    public uint InternPlane(IDmAppearance source)
    {
        if (source == null) return 0;
        AppearanceEntry e = _scratch[0];
        Reset(e);
        Fill(e, source, 0, false);
        if (e.Icon.IsNone && e.Maptext.IsNone && e.OverlayCount == 0 && e.UnderlayCount == 0 && (e.Filters == null || e.Filters.Count == 0)) return 0;
        return Find(e);
    }

    /// <summary>
    /// The id of the appearance BYOND's <c>flick(state, atom)</c> plays on <paramref name="source"/> (version 8): its appearance
    /// with <paramref name="flicked"/> in place of its icon state (a string) or of its icon (an icon file); 0 when that shows nothing.
    /// </summary>
    public uint InternFlick(IDmAppearance source, object flicked)
    {
        if (source == null) return 0;
        AppearanceEntry e = _scratch[0];
        Reset(e);
        Fill(e, source, 0, false);
        switch (flicked)
        {
            case string state:
                e.IconState = state.Length == 0 ? TableRef.None : StringRef(state);
                break;
            case DmResource resource:
                e.Icon = ResourceRef(resource.Path);
                break;
            default:
                return 0;
        }
        if (e.Icon.IsNone && e.Maptext.IsNone && e.OverlayCount == 0 && e.UnderlayCount == 0) return 0;
        return Find(e);
    }

    /// <summary>
    /// Protocol version 12: the id of a screen object's <c>vis_contents</c> child drawn alone, the same row <see cref="InternTop"/> flattens
    /// into the holder for it (<see cref="AddVisContents"/>, a top-level holder's child at depth 1); 0 when it shows nothing.
    /// </summary>
    public uint InternVisChild(Anomaly.Core.Tg.atom child) => child == null ? 0 : InternItem(child, 1, visChild: true);

    // The overlay or underlay item as an id, 0 when the item shows nothing and is skipped.
    private uint InternItem(object item, int depth, object inheritedIcon = null, bool visChild = false)
    {
        AppearanceEntry e = _scratch[depth];
        Reset(e);
        switch (item)
        {
            case IDmAppearance appearance:
                Fill(e, appearance, depth, true);
                // BYOND ref /atom/var/vis_flags: with VIS_INHERIT_ICON a vis_contents object draws the holder's icon file (a solar panel's
                // glass is its overlay object with only an icon state, solar.dm:64)
                if (inheritedIcon != null)
                    e.Icon = IconRef(inheritedIcon);
                // VIS_INHERIT_LAYER and VIS_INHERIT_PLANE: it is drawn on the holder's layer and plane, as an overlay of FLOAT_LAYER and FLOAT_PLANE
                // is, over the holder; on its own layer it went under a holder whose layer is higher (tg's greenscreen feed, layer 3, under the
                // status display's screen, 3.3, status_display.dm:945)
                if (visChild && item is Anomaly.Core.Tg.atom child)
                {
                    int flags = (int)child.vis_flags;
                    if ((flags & (int)Dm.VIS_INHERIT_LAYER) != 0)
                        e.Layer = Dm.FLOAT_LAYER;
                    if ((flags & (int)Dm.VIS_INHERIT_PLANE) != 0)
                        e.Plane = ToShort(Dm.FLOAT_PLANE);
                }
                break;
            case string state when state.Length > 0:
                e.IconState = StringRef(state);
                if (e.IconState.IsNone) return 0;
                break;
            case DmResource resource:
                e.Icon = ResourceRef(resource.Path);
                if (e.Icon.IsNone) return 0;
                break;
            case Anomaly.Core.DmShim.icon dynamic: // M6 slice 7: an /icon object as an overlay shows its state (tg's damaged clothing)
                e.Icon = IconRef(dynamic);
                if (e.Icon.IsNone) return 0;
                e.IconState = string.IsNullOrEmpty(dynamic.dm_state) ? TableRef.None : StringRef(dynamic.dm_state);
                break;
            default:
                return 0;
        }

        return Find(e);
    }

    private uint Find(AppearanceEntry e)
    {
        if (_appearanceIndex.TryGetValue(e, out uint id)) return id;
        if (Appearances.Count >= Limits.MaxAppearanceTableEntries)
        {
            Overflows++;
            return 0;
        }

        AppearanceEntry copy = Clone(e);
        Appearances.Add(copy);
        id = (uint)Appearances.Count;
        _appearanceIndex.Add(copy, id);
        return id;
    }

    private void Fill(AppearanceEntry e, IDmAppearance a, int depth, bool keepDir)
    {
        e.Icon = IconRef(a.icon);
        string state = a.icon_state;
        // M4c slice 4: an icon object (icon(file, state), the radial's pictures) shows its file's state when the appearance names none.
        if (string.IsNullOrEmpty(state) && a.icon is Anomaly.Core.DmShim.icon dynamic) state = dynamic.dm_state;
        e.IconState = string.IsNullOrEmpty(state) ? TableRef.None : StringRef(state);
        e.Dir = keepDir ? ReplicatedFields.WireDir(a.dir) : (byte)0;
        object color = a.color;
        if (color is string text)
        {
            e.Rgba = ParseColor(text);
        }
        else if (color is DmList list)
        {
            e.ColorMatrix = Matrix(list);
            e.Rgba = 0xFFFFFFFF;
        }

        e.Alpha = (byte)Math.Clamp((int)MathF.Round(Finite(a.alpha, 255f)), 0, 255);
        e.Layer = Finite(a.layer, 0f);
        e.Plane = ToShort(a.plane);
        e.PixelX = ToShort(a.pixel_x);
        e.PixelY = ToShort(a.pixel_y);
        e.PixelW = ToShort(a.pixel_w);
        e.PixelZ = ToShort(a.pixel_z);
        e.Transform = Transform(a.transform);
        e.AppearanceFlags = (ushort)((int)Finite(a.appearance_flags, 0f) & 0xFFFF);
        e.BlendMode = (byte)Math.Clamp((int)Finite(a.blend_mode, 0f), 0, 255);
        // Version 18: a session's HUD draws maptext. Version 74 (gaps W1): the public tables carry it too, the world's atoms draw it (tg's
        // status displays); BYOND shows an atom's maptext to everyone who sees the atom.
        string maptext = a.maptext;
        if (!string.IsNullOrEmpty(maptext))
        {
            e.Maptext = StringRef(maptext);
            if (!e.Maptext.IsNone)
            {
                e.MaptextWidth = ToShort(Finite(a.maptext_width, AppearanceEntry.DefaultMaptextSize));
                e.MaptextHeight = ToShort(Finite(a.maptext_height, AppearanceEntry.DefaultMaptextSize));
                if (e.MaptextWidth < 0) e.MaptextWidth = 0;
                if (e.MaptextHeight < 0) e.MaptextHeight = 0;
                e.MaptextX = ToShort(a.maptext_x);
                e.MaptextY = ToShort(a.maptext_y);
            }
        }

        // Version 25 (D155): the filters the client draws, and the render target a filter takes a picture by.
        e.Filters = Filters(a.filters);
        e.RenderTarget = a.render_target is string target && target.Length > 0 ? StringRef(target) : TableRef.None;
        // Version 99 (DV687): the particle set the atom emits (a particle holder's), its vars as JSON for the client to draw.
        string particles = a is Anomaly.Core.Tg.atom_movable emitter ? ParticleSets.Text(emitter.particles) : null;
        e.Particles = particles != null ? StringRef(particles) : TableRef.None;

        if (depth < MaxDepth)
        {
            AddItems(e.Overlays, a.overlays, depth + 1);
            AddItems(e.Underlays, a.underlays, depth + 1);
            if (a is Anomaly.Core.Tg.atom holder)
                AddVisContents(e, holder, holder.icon, holder.vis_contents, depth + 1);
            else if (a is image picture)
                AddVisContents(e, picture, picture.icon, picture.vis_contents, depth + 1); // silicons SI4: an image's (the AI detector HUD's)
        }
    }

    // BYOND draws an atom's vis_contents as part of it: each object with its own appearance, layer and plane, under the atom
    // when its vis_flags has VIS_UNDERLAY (BYOND ref /atom/var/vis_contents, /atom/var/vis_flags). The wire has no
    // vis_contents, so they are flattened into the atom's overlays (or underlays) as drawn, after its own (M2 integration:
    // tg's health doll keeps its limbs there, screen_objects.dm:968-979).
    private void AddVisContents(AppearanceEntry e, object holder, object holderIcon, DmList list, int depth)
    {
        if (list == null || list.Count == 0) return;
        int count = list.Count;
        for (int i = 1; i <= count && i <= list.Count; i++)
        {
            if (list[(float)i] is not Anomaly.Core.Tg.atom child) continue;
            // Version 10: a flick visual (tg's flick_overlay_view) on a turf or an entity is an entity of its own (NetField.VisHost),
            // drawn over its host with its own animation; only a screen object still draws one as part of its picture.
            // M7x: a gun's firing effect on its shooter is one too.
            // M8x (D210): so is a visual helper (/atom/movable/visual: the cryo cell's occupant, which the clients draw into the cell's picture).
            // M8y: so is a unique vis overlay (the stasis bed's mattress, whose 5 s fade the clients play).
            if ((child is Anomaly.Core.Tg.atom_movable m && (ReplicatedFields.IsInsideVisual(m) || ReplicatedFields.IsHeldVisual(m) || ReplicatedFields.IsInnerVisual(m))) && holder is not Anomaly.Core.Tg.atom_movable_screen) continue;
            // DV32: a turf's gas overlays (tg's update_visuals puts GAS_OVERLAYS in its vis_contents) travel as the cell's gas levels
            // (protocol version 5) and the client draws them as a haze; in the turf's picture they were drawn twice, and every level
            // a spreading gas passed through was a new turf picture the client rebuilt its chunk for.
            if (child is Anomaly.Core.Tg.obj_effect_overlay_gas && holder is Anomaly.Core.Tg.turf) continue;
            List<TableRef> into = ((int)child.vis_flags & (int)Dm.VIS_UNDERLAY) != 0 ? e.Underlays : e.Overlays;
            if (into.Count >= Limits.MaxAppearanceOverlays) continue;
            uint id = InternItem(child, depth, ((int)child.vis_flags & (int)Dm.VIS_INHERIT_ICON) != 0 ? holderIcon : null, visChild: true);
            if (id != 0) into.Add(new TableRef(id, _private));
        }
    }

    private void AddItems(List<TableRef> into, DmList list, int depth)
    {
        if (list == null || list.Count == 0) return;
        int count = list.Count;
        for (int i = 1; i <= count && i <= list.Count; i++)
        {
            if (into.Count >= Limits.MaxAppearanceOverlays) break;
            uint id = InternItem(list[(float)i], depth);
            if (id != 0) into.Add(new TableRef(id, _private));
        }
    }

    // ---- value conversions ----

    // BYOND's filters list as the wire's filters (protocol.md sections 34.1, 35.1): every filter at its position (an animation names it
    // by it; version 26), up to Limits.MaxAppearanceFilters, with the parameters of its type and BYOND's defaults (BYOND ref "filters");
    // null for none.
    private List<FilterEntry> Filters(object filters)
    {
        if (filters is not DmList list || list.Count == 0) return null;
        List<FilterEntry> made = null;
        for (int i = 1; i <= list.Count && i <= Limits.MaxAppearanceFilters; i++)
        {
            // a filters list holds only filters (Dm.filter_list) of the types BYOND knows (Dm.filter), each of which has a kind
            if (list[(float)i] is not dm_filter f || Filter(f) is not FilterEntry entry) break;
            (made ??= new List<FilterEntry>()).Add(entry);
        }
        return made;
    }

    // One filter: its kind, BYOND's default for every parameter not given, the shim's flag values as the wire's.
    private FilterEntry Filter(dm_filter f)
    {
        FilterKind? kind = f.type switch
        {
            "alpha" => FilterKind.Alpha,
            "outline" => FilterKind.Outline,
            "displace" => FilterKind.Displace,
            "color" => FilterKind.Color,
            "angular_blur" => FilterKind.AngularBlur,
            "bloom" => FilterKind.Bloom,
            "blur" => FilterKind.Blur,
            "drop_shadow" => FilterKind.DropShadow,
            "layer" => FilterKind.Layer,
            "motion_blur" => FilterKind.MotionBlur,
            "radial_blur" => FilterKind.RadialBlur,
            "rays" => FilterKind.Rays,
            "ripple" => FilterKind.Ripple,
            "wave" => FilterKind.Wave,
            "greyscale" => FilterKind.Greyscale,
            _ => null,
        };
        if (kind == null) return null;
        var e = new FilterEntry { Kind = kind.Value };
        float N(string name, float fallback) => f.Has(name) && f.Get(name) != null ? Finite(Number(f.Get(name)), fallback) : fallback;
        int flags = (int)N("flags", 0f);
        e.X = N("x", FilterDefault(f.type, "x"));
        e.Y = N("y", FilterDefault(f.type, "y"));
        e.Size = N("size", FilterDefault(f.type, "size"));
        switch (e.Kind)
        {
            case FilterKind.Alpha:
                e.Flags = (byte)(flags & (FilterFlags.MaskInverse | FilterFlags.MaskSwap));
                Source(f, e);
                break;
            case FilterKind.Displace:
                Source(f, e);
                break;
            case FilterKind.Layer:
                e.Flags = (flags & (int)Dm.FILTER_UNDERLAY) != 0 ? FilterFlags.Underlay : FilterFlags.Overlay;
                Source(f, e);
                LayerColour(f.Get("color"), e);
                if (f.Get("transform") is matrix m)
                    e.Transform = new[] { Finite(m.a, 1f), Finite(m.b, 0f), Finite(m.c, 0f), Finite(m.d, 0f), Finite(m.e, 1f), Finite(m.f, 0f) };
                e.BlendMode = (byte)Math.Clamp((int)N("blend_mode", 0f), 0, 5);
                break;
            case FilterKind.Outline:
                e.Flags = (byte)(flags & (FilterFlags.OutlineSharp | FilterFlags.OutlineSquare));
                if (f.Get("color") is string outline) e.Rgba = ParseColor(outline);
                e.Alpha = Math.Clamp(N("alpha", 255f), 0f, 255f); // tg's, D156: the health doll's pulse
                break;
            case FilterKind.Color:
                e.ColorMatrix = f.Get("color") switch
                {
                    DmList matrix => Matrix(matrix),
                    string tint => TintMatrix(ParseColor(tint)),
                    _ => null,
                };
                e.Space = (byte)Math.Clamp((int)N("space", 0f), 0, 3);
                break;
            case FilterKind.AngularBlur:
            case FilterKind.RadialBlur:
                e.Offset = N("offset", 0f);
                break;
            case FilterKind.Bloom:
                e.ThresholdRgba = f.Get("threshold") is string threshold ? ParseColor(threshold) : 0x000000FF;
                e.Offset = N("offset", FilterDefault(f.type, "offset"));
                e.Alpha = Math.Clamp(N("alpha", 255f), 0f, 255f);
                break;
            case FilterKind.DropShadow:
                e.Offset = N("offset", 0f);
                e.Rgba = f.Get("color") is string shadow ? ParseColor(shadow) : 0x00000080;
                break;
            case FilterKind.Rays:
                e.Rgba = f.Get("color") switch
                {
                    string ray => ParseColor(ray),
                    DmList diagonal => Diagonal(diagonal),
                    _ => 0xFFFFFFFF,
                };
                e.Offset = N("offset", 0f);
                e.Density = MathF.Max(1f, MathF.Round(N("density", FilterDefault(f.type, "density"))));
                e.Threshold = Math.Clamp(N("threshold", FilterDefault(f.type, "threshold")), 0f, 1f);
                e.Factor = Math.Clamp(N("factor", 0f), 0f, 1f);
                // BYOND's default draws an image's rays under it; the shim's FILTER_OVERLAY is 0 (OpenDream's), so a given 0 is an overlay
                e.Flags = !f.Has("flags") ? (byte)(FilterFlags.Overlay | FilterFlags.Underlay)
                    : (flags & (int)Dm.FILTER_UNDERLAY) != 0 ? FilterFlags.Underlay : FilterFlags.Overlay;
                break;
            case FilterKind.Ripple:
                e.Repeat = N("repeat", FilterDefault(f.type, "repeat"));
                e.Radius = N("radius", FilterDefault(f.type, "radius"));
                e.Falloff = N("falloff", FilterDefault(f.type, "falloff"));
                e.Flags = (byte)(flags & (int)Dm.WAVE_BOUNDED) != 0 ? FilterFlags.WaveBounded : (byte)0;
                break;
            case FilterKind.Wave:
                e.Offset = N("offset", 0f);
                e.Flags = (byte)(((flags & (int)Dm.WAVE_BOUNDED) != 0 ? FilterFlags.WaveBounded : 0) | ((flags & (int)Dm.WAVE_SIDEWAYS) != 0 ? FilterFlags.WaveSideways : 0));
                break;
        }
        return e;
    }

    /// <summary>
    /// BYOND's default of a filter's numeric parameter for its type (BYOND ref "Filter effects"; bloom's offset as OpenDream's): what
    /// the client draws when the parameter was not given, and what an animation of it starts from.
    /// </summary>
    internal static float FilterDefault(string type, string name) => name switch
    {
        "x" => type == "drop_shadow" ? 1f : 0f,
        "y" => type == "drop_shadow" ? -1f : 0f,
        "size" => type switch { "radial_blur" => 0.01f, "rays" => 16f, _ => 1f },
        "offset" => type == "bloom" ? 1f : 0f,
        "alpha" => 255f,
        "repeat" => 2f,
        "falloff" => 1f,
        "density" => 10f,
        "threshold" => 0.5f,
        _ => 0f, // radius, factor
    };

    /// <summary>A colour matrix of 9 to 20 numbers as 20 (the identity kept: an animation may end at it); null for a list BYOND cannot read.</summary>
    internal static float[] FullMatrix(DmList list)
    {
        float[] m = Matrix(list);
        if (m != null) return m;
        if (list.Count is not (9 or 12 or 16 or 20)) return null;
        var identity = new float[20];
        identity[0] = identity[5] = identity[10] = identity[15] = 1f;
        return identity;
    }

    // The icon (a file, an icon object with its state) or the render source of a mask, a map or a second picture.
    private void Source(dm_filter f, FilterEntry e)
    {
        object icon = f.Get("icon");
        e.Icon = IconRef(icon);
        if (icon is Anomaly.Core.DmShim.icon dynamic && !string.IsNullOrEmpty(dynamic.dm_state)) e.IconState = StringRef(dynamic.dm_state);
        if (f.Get("render_source") is string source && source.Length > 0) e.RenderSource = StringRef(source);
    }

    // A layer filter's colour: a colour text (a tint of the second picture) or a colour matrix.
    private void LayerColour(object color, FilterEntry e)
    {
        switch (color)
        {
            case string tint:
                e.ColorMatrix = TintMatrix(ParseColor(tint));
                break;
            case DmList matrix:
                e.ColorMatrix = Matrix(matrix);
                break;
        }
    }

    // Rays' colour given as a matrix: its diagonal (BYOND ref "Rays filter"), each channel clamped to a byte.
    private static uint Diagonal(DmList list)
    {
        float[] m = Matrix(list);
        if (m == null) return 0xFFFFFFFF;
        static uint B(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
        return (B(m[0]) << 24) | (B(m[5]) << 16) | (B(m[10]) << 8) | B(m[15]);
    }

    // A colour text as a colour filter's matrix: each channel multiplied (BYOND's colour as a matrix); null for white.
    private static float[] TintMatrix(uint rgba)
    {
        if (rgba == 0xFFFFFFFF) return null;
        var m = new float[20];
        m[0] = (rgba >> 24) / 255f;
        m[5] = ((rgba >> 16) & 255) / 255f;
        m[10] = ((rgba >> 8) & 255) / 255f;
        m[15] = (rgba & 255) / 255f;
        return m;
    }

    private TableRef IconRef(object icon) => icon switch
    {
        DmResource resource => ResourceRef(resource.Path),
        Anomaly.Core.DmShim.icon dynamic => ResourceRef(dynamic.dm_resource()), // M4c slice 4: an icon object's file; M6 slice 7: with its Blend recipe
        string path when path.Length > 0 => ResourceRef(path),
        _ => TableRef.None,
    };

    private TableRef ResourceRef(string path)
    {
        if (string.IsNullOrEmpty(path)) return TableRef.None;
        if (_resourceIndex.TryGetValue(path, out uint id)) return new TableRef(id, _private);
        if (Resources.Count >= Limits.MaxResourceTableEntries || path.Length > Limits.MaxResourceBytes || Encoding.UTF8.GetByteCount(path) > Limits.MaxResourceBytes)
        {
            Overflows++;
            return TableRef.None;
        }

        Resources.Add(path);
        id = (uint)Resources.Count;
        _resourceIndex.Add(path, id);
        return new TableRef(id, _private);
    }

    private TableRef StringRef(string text)
    {
        if (_stringIndex.TryGetValue(text, out uint id)) return new TableRef(id, _private);
        if (Strings.Count >= Limits.MaxStringTableEntries)
        {
            Overflows++;
            return TableRef.None;
        }

        string wire = Cut(text, Limits.MaxTableStringBytes);
        if (!ReferenceEquals(wire, text) && _stringIndex.TryGetValue(wire, out id))
        {
            _stringIndex.Add(text, id);
            return new TableRef(id, _private);
        }

        Strings.Add(wire);
        id = (uint)Strings.Count;
        _stringIndex[wire] = id;
        if (!ReferenceEquals(wire, text)) _stringIndex[text] = id;
        return new TableRef(id, _private);
    }

    // The string within the cap, cut at a character boundary (never between the halves of a surrogate pair).
    private static string Cut(string text, int maxBytes)
    {
        if (text.Length * 3 <= maxBytes || Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;
        int bytes = 0, i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int chars = 1, width;
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                width = 4;
                chars = 2;
            }
            else
            {
                width = c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            }

            if (bytes + width > maxBytes) break;
            bytes += width;
            i += chars;
        }

        return text.Substring(0, i);
    }

    // BYOND colour text ("#rgb", "#rrggbb", "#rrggbbaa", an HTML colour name) as 0xRRGGBBAA; white (no tint) for null, empty or a bad text.
    private uint ParseColor(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0xFFFFFFFF;
        if (_colors.TryGetValue(text, out var cached)) return cached.Valid ? cached.Rgba : 0xFFFFFFFF;
        uint rgba = 0xFFFFFFFF;
        bool valid = false;
        try
        {
            DmList parts = Dm.rgb2num(text);
            if (parts.Count >= 3)
            {
                uint r = Byte(parts, 1), g = Byte(parts, 2), b = Byte(parts, 3);
                uint a = parts.Count >= 4 ? Byte(parts, 4) : 255u;
                rgba = (r << 24) | (g << 16) | (b << 8) | a;
                valid = true;
            }
        }
        catch (DmRuntimeException)
        {
            // an unknown colour text: BYOND shows no tint
        }

        _colors[text] = (rgba, valid);
        return rgba;
    }

    private static uint Byte(DmList parts, int index) => (uint)Math.Clamp((int)MathF.Round(Finite(Number(parts[(float)index]), 0f)), 0, 255);

    // A DM number is a boxed float; anything else counts as 0.
    private static float Number(object value) => value switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        long l => l,
        _ => 0f,
    };

    // BYOND's colour matrix as 20 floats in its row order (rr rg rb ra, gr gg gb ga, br bg bb ba, ar ag ab aa, r0 g0 b0 a0);
    // a list of 9, 12 or 16 numbers is completed the way BYOND completes it. Null for an identity matrix, a list of another
    // length or non-numbers (BYOND shows no tint for what it cannot read).
    private static float[] Matrix(DmList list)
    {
        int count = list.Count;
        if (count != 9 && count != 12 && count != 16 && count != 20) return null;
        var m = new float[20];
        float N(int i) => Finite(Number(list[(float)(i + 1)]), 0f);
        int row = count == 9 || count == 12 ? 3 : 4;
        for (int r = 0; r < row; r++)
            for (int c = 0; c < row; c++)
                m[r * 4 + c] = N(r * row + c);
        if (count == 9 || count == 12)
        {
            m[15] = 1f; // alpha row: ar ag ab aa = 0 0 0 1
            if (count == 12) { m[16] = N(9); m[17] = N(10); m[18] = N(11); }
        }
        else if (count == 16)
        {
            // a 4 x 4 matrix has no constants
        }
        else
        {
            for (int i = 0; i < 4; i++) m[16 + i] = N(16 + i);
        }

        // identity: rr gg bb aa = 1, everything else 0
        bool identity = true;
        for (int i = 0; i < 20 && identity; i++)
            identity = m[i] == (i == 0 || i == 5 || i == 10 || i == 15 ? 1f : 0f);
        return identity ? null : m;
    }

    private static float[] Transform(object transform)
    {
        if (transform is not matrix m) return null;
        if (m.a == 1f && m.b == 0f && m.c == 0f && m.d == 0f && m.e == 1f && m.f == 0f) return null;
        return new[] { Finite(m.a, 1f), Finite(m.b, 0f), Finite(m.c, 0f), Finite(m.d, 0f), Finite(m.e, 1f), Finite(m.f, 0f) };
    }

    private static float Finite(float value, float fallback) => float.IsFinite(value) ? value : fallback;

    private static short ToShort(float value) => (short)Math.Clamp((int)MathF.Round(Finite(value, 0f)), short.MinValue, short.MaxValue);

    private static void Reset(AppearanceEntry e)
    {
        e.Icon = default;
        e.IconState = default;
        e.Dir = 0;
        e.Rgba = 0xFFFFFFFF;
        e.ColorMatrix = null;
        e.Alpha = 255;
        e.Layer = 0f;
        e.Plane = 0;
        e.PixelX = e.PixelY = e.PixelW = e.PixelZ = 0;
        e.Transform = null;
        e.AppearanceFlags = 0;
        e.BlendMode = 0;
        e.Maptext = default;
        e.MaptextWidth = e.MaptextHeight = AppearanceEntry.DefaultMaptextSize;
        e.MaptextX = e.MaptextY = 0;
        e.Filters = null;
        e.RenderTarget = default;
        e.Particles = default;
        if (e.OverlayCount > 0) e.Overlays.Clear();
        if (e.UnderlayCount > 0) e.Underlays.Clear();
    }

    private static AppearanceEntry Clone(AppearanceEntry e)
    {
        var copy = new AppearanceEntry
        {
            Icon = e.Icon,
            IconState = e.IconState,
            Dir = e.Dir,
            Rgba = e.Rgba,
            ColorMatrix = (float[])e.ColorMatrix?.Clone(),
            Alpha = e.Alpha,
            Layer = e.Layer,
            Plane = e.Plane,
            PixelX = e.PixelX,
            PixelY = e.PixelY,
            PixelW = e.PixelW,
            PixelZ = e.PixelZ,
            Transform = (float[])e.Transform?.Clone(),
            AppearanceFlags = e.AppearanceFlags,
            BlendMode = e.BlendMode,
            Maptext = e.Maptext,
            MaptextWidth = e.MaptextWidth,
            MaptextHeight = e.MaptextHeight,
            MaptextX = e.MaptextX,
            MaptextY = e.MaptextY,
            Filters = e.Filters?.ConvertAll(f => f.Copy()),
            RenderTarget = e.RenderTarget,
            Particles = e.Particles,
        };
        if (e.OverlayCount > 0) copy.Overlays.AddRange(e.Overlays);
        if (e.UnderlayCount > 0) copy.Underlays.AddRange(e.Underlays);
        return copy;
    }

    /// <summary>Structural equality of entries (floats by their bits), the interning key.</summary>
    internal sealed class EntryComparer : IEqualityComparer<AppearanceEntry>
    {
        public static readonly EntryComparer Instance = new();

        public bool Equals(AppearanceEntry x, AppearanceEntry y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            return x.Icon == y.Icon && x.IconState == y.IconState && x.Dir == y.Dir && x.Rgba == y.Rgba && x.Alpha == y.Alpha
                && BitConverter.SingleToInt32Bits(x.Layer) == BitConverter.SingleToInt32Bits(y.Layer)
                && x.Plane == y.Plane && x.PixelX == y.PixelX && x.PixelY == y.PixelY && x.PixelW == y.PixelW && x.PixelZ == y.PixelZ
                && x.AppearanceFlags == y.AppearanceFlags && x.BlendMode == y.BlendMode
                && x.Maptext == y.Maptext && (x.Maptext.IsNone || (x.MaptextWidth == y.MaptextWidth && x.MaptextHeight == y.MaptextHeight
                    && x.MaptextX == y.MaptextX && x.MaptextY == y.MaptextY))
                && FloatsEqual(x.ColorMatrix, y.ColorMatrix) && FloatsEqual(x.Transform, y.Transform)
                && x.RenderTarget == y.RenderTarget && x.Particles == y.Particles && FiltersEqual(x.Filters, y.Filters)
                && RefsEqual(x.Overlays, x.OverlayCount, y.Overlays, y.OverlayCount)
                && RefsEqual(x.Underlays, x.UnderlayCount, y.Underlays, y.UnderlayCount);
        }

        public int GetHashCode(AppearanceEntry e)
        {
            var h = new HashCode();
            h.Add(e.Icon);
            h.Add(e.IconState);
            h.Add(e.Dir);
            h.Add(e.Rgba);
            h.Add(e.Alpha);
            h.Add(BitConverter.SingleToInt32Bits(e.Layer));
            h.Add(e.Plane);
            h.Add(e.PixelX);
            h.Add(e.PixelY);
            h.Add(e.AppearanceFlags);
            h.Add(e.Maptext);
            h.Add(e.RenderTarget);
            h.Add(e.Filters?.Count ?? 0);
            h.Add(e.OverlayCount);
            for (int i = 0; i < e.OverlayCount; i++) h.Add(e.Overlays[i]);
            h.Add(e.UnderlayCount);
            for (int i = 0; i < e.UnderlayCount; i++) h.Add(e.Underlays[i]);
            if (e.ColorMatrix != null) foreach (float f in e.ColorMatrix) h.Add(BitConverter.SingleToInt32Bits(f));
            if (e.Transform != null) foreach (float f in e.Transform) h.Add(BitConverter.SingleToInt32Bits(f));
            return h.ToHashCode();
        }

        private static bool FloatsEqual(float[] a, float[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i])) return false;
            return true;
        }

        private static bool FiltersEqual(List<FilterEntry> a, List<FilterEntry> b)
        {
            int ac = a?.Count ?? 0, bc = b?.Count ?? 0;
            if (ac != bc) return false;
            for (int i = 0; i < ac; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        private static bool RefsEqual(List<TableRef> a, int ac, List<TableRef> b, int bc)
        {
            if (ac != bc) return false;
            for (int i = 0; i < ac; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
