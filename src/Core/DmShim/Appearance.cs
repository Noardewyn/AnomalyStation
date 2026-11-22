using System.Runtime.CompilerServices;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: M2 P3 (the overlay snapshot engine part; docs/ai/specs/m2-ownership.md "the overlay snapshot"). The per-world
// interning table is Appearance.World.cs; the conversion of what tg adds to an atom's overlays and underlays is installed
// by src/Core/Tg/Builtin/atom.overlays.builtin.cs through DmList.dm_convert.

/// <summary>
/// An appearance as BYOND keeps it in an <c>overlays</c> or <c>underlays</c> list: an immutable snapshot of an image, a
/// mutable appearance, an atom or an atom type, taken when it is added (BYOND ref <c>/atom/var/overlays</c>: "When another
/// object is used as an overlay, a static "snapshot" of the object is taken at the time when the overlay is created. Future
/// changes to the object will not change the appearance of the overlay"; "The individual items in the list may not be
/// directly accessed, since they are stored in a special internal format"). BYOND interns appearances (equal appearances
/// are one appearance), and so does the port, per world (<see cref="Of"/>): two snapshots of equal appearances are the same
/// object, so <c>overlays -= x</c> removes the overlay whose appearance equals <c>x</c>'s current one by the list's plain
/// reference equality (<c>Dm.eq</c>), and the server's appearance interning (M2 P5) can key by reference.
/// <para>
/// The snapshot's own <see cref="overlays"/> and <see cref="underlays"/> hold snapshots too (converted recursively), or the
/// text and resources an overlay list keeps as they are (an icon state of the parent's icon, an icon). Reading a list
/// var gives a new list each time (BYOND: "Reading certain vars, such as overlays, will create a temporary list object"),
/// and reading <see cref="transform"/> or a colour matrix gives a copy, so nothing that reads a snapshot can change it.
/// Not a datum: no world id, never registered, no DM path (BYOND's appearances are not <c>/image</c>s either: tg's
/// <c>isappearance</c>, <c>__DEFINES/is_helpers.dm:17</c>).
/// </para>
/// </summary>
public sealed class DmAppearance : IDmAppearance
{
    private readonly object _icon;
    private readonly string _icon_state;
    private readonly float _dir;
    private readonly bool _inheritsDir;
    private readonly float _layer;
    private readonly float _plane;
    private readonly float _alpha;
    private readonly object _color;      // text, null, or a colour matrix copied into an object[]
    private readonly float _pixel_x;
    private readonly float _pixel_y;
    private readonly float _pixel_w;
    private readonly float _pixel_z;
    private readonly matrix _transform;  // a private copy, or null
    private readonly float _appearance_flags;
    private readonly float _blend_mode;
    private readonly float _invisibility;
    private readonly object[] _overlays;
    private readonly object[] _underlays;
    private readonly string _name;
    private readonly string _desc;
    private readonly string _maptext;
    private readonly float _maptext_width = 32;
    private readonly float _maptext_height = 32;
    private readonly float _maptext_x;
    private readonly float _maptext_y;
    private readonly float _mouse_opacity;
    private readonly object _render_source;
    private readonly object _render_target;
    private readonly float _vis_flags;
    private readonly object _filters;
    private readonly float _density;
    private readonly string _gender;
    private readonly string _screen_loc;
    private readonly int _hash;

    // Copies the source's appearance; the overlay lists are converted with OverlayItem (snapshots of their own).
    private DmAppearance(IDmAppearance source, bool inheritsDir)
    {
        _icon = source.icon;
        _icon_state = source.icon_state;
        _dir = source.dir;
        _inheritsDir = inheritsDir;
        _layer = source.layer;
        _plane = source.plane;
        _alpha = source.alpha;
        _color = source.color is DmList matrix ? matrix.CopyItems() : source.color;
        _pixel_x = source.pixel_x;
        _pixel_y = source.pixel_y;
        _pixel_w = source.pixel_w;
        _pixel_z = source.pixel_z;
        _transform = source.transform is matrix m ? m.Copy() : null;
        _appearance_flags = source.appearance_flags;
        _blend_mode = source.blend_mode;
        _invisibility = source.invisibility;
        _filters = Dm.filter_list(source.filters); // a copy: the source's list may change after the snapshot (M6 slice 7)
        _render_target = source.render_target;
        _overlays = ConvertItems(source.overlays);
        _underlays = ConvertItems(source.underlays);
        _mouse_opacity = 1;
        _gender = "neuter";
        switch (source)
        {
            case DmAppearance other:
                _name = other._name;
                _desc = other._desc;
                _maptext = other._maptext;
                _maptext_width = other._maptext_width;
                _maptext_height = other._maptext_height;
                _maptext_x = other._maptext_x;
                _maptext_y = other._maptext_y;
                _mouse_opacity = other._mouse_opacity;
                _render_source = other._render_source;
                _vis_flags = other._vis_flags;
                _density = other._density;
                _gender = other._gender;
                _screen_loc = other._screen_loc;
                break;
            case image img:
                _name = img.name;
                _desc = img.desc;
                _maptext = img.maptext;
                _maptext_width = img.maptext_width;
                _maptext_height = img.maptext_height;
                _maptext_x = img.maptext_x;
                _maptext_y = img.maptext_y;
                _mouse_opacity = img.mouse_opacity;
                _render_source = img.render_source;
                _vis_flags = img.vis_flags;
                if (img is mutable_appearance ma)
                {
                    _density = ma.density;
                    _gender = ma.gender;
                    _screen_loc = ma.screen_loc;
                }
                break;
            case IDmAtom atom:
                // The vars new /mutable_appearance(atom) takes from an atom besides its appearance (mutable_appearance.cs).
                _name = atom.name;
                _gender = atom.gender;
                _desc = Dm.get_var(atom, "desc") as string;
                _density = Dm.num(Dm.get_var(atom, "density"));
                _maptext = source.maptext;
                _maptext_width = source.maptext_width;
                _maptext_height = source.maptext_height;
                _maptext_x = source.maptext_x;
                _maptext_y = source.maptext_y;
                break;
        }
        _hash = ComputeHash();
    }

    /// <summary>
    /// The interned snapshot of <paramref name="source"/>'s current appearance in the ambient world (outside a world, a
    /// snapshot of its own). A <see cref="DmAppearance"/> is its own snapshot. An image's direction counts as set (not
    /// inherited from the overlay's parent) when it is not <c>SOUTH</c>, a mutable appearance's and an atom's never (see
    /// <see cref="InheritsDir"/>).
    /// </summary>
    public static DmAppearance Of(IDmAppearance source)
    {
        if (source == null) return null;
        if (source is DmAppearance already) return already;
        bool inherits = source switch
        {
            mutable_appearance => true,
            image img => img.dir == Dm.SOUTH,
            _ => true,
        };
        var made = new DmAppearance(source, inherits);
        World world = World.CurrentOrNull;
        return world == null ? made : DmWorldState.Of(world).InternAppearance(made);
    }

    /// <summary>
    /// The conversion of an item added to (or looked up in) an atom's <c>overlays</c> or <c>underlays</c>
    /// (<see cref="DmList.dm_convert"/>): an image, a mutable appearance, an atom or a snapshot becomes its interned
    /// snapshot; an atom type becomes the snapshot of its prototype's appearance (BYOND: "The data types that may be used
    /// as overlays are icons, icon states (text strings), objects, and object types"); text (an icon state of the parent's
    /// icon), an icon resource and anything else stay as they are (<see cref="IDmAppearance"/>'s overlay rule). Idempotent.
    /// </summary>
    public static object OverlayItem(object item) => item switch
    {
        null => null,
        DmAppearance already => already,
        IDmAppearance source => Of(source),
        Type type when DmTypes.PathOf(type) != null && DmPrototypes.Prototype(type) is IDmAppearance prototype => Of(prototype),
        _ => item,
    };

    /// <summary><see cref="OverlayItem"/> as the delegate an engine list holds (immutable, shared by every list).</summary>
    public static readonly Func<object, object> OverlayConverter = OverlayItem;

    /// <summary>
    /// Whether this overlay takes its direction from what it is drawn on (BYOND: an overlay "may inherit things from the
    /// base object like its icon (if left empty), direction"; tg <c>datums/mutable_appearance.dm:3</c> and :53-55: mutable
    /// appearances always inherit, an <c>/image</c> made with an explicit <c>dir</c> keeps it through a hidden flag).
    /// The shim's image does not record that flag, so an image keeps its own direction when it is not <c>SOUTH</c>
    /// (SHIM-GAP: image has no "dir set" flag; a dropped limb's explicit <c>SOUTH</c> image counts as inheriting).
    /// Port machinery for the client's compositor (M2 P5, P7), not a DM var.
    /// </summary>
    public bool InheritsDir => _inheritsDir;

    // ---- the appearance (BYOND names, read-only) ----

    /// <summary>BYOND <c>icon</c>.</summary>
    public object icon => _icon;

    /// <summary>BYOND <c>icon_state</c>.</summary>
    public string icon_state => _icon_state;

    /// <summary>BYOND <c>dir</c> (see <see cref="InheritsDir"/>).</summary>
    public float dir => _dir;

    /// <summary>BYOND <c>layer</c>.</summary>
    public float layer => _layer;

    /// <summary>BYOND <c>plane</c>.</summary>
    public float plane => _plane;

    /// <summary>BYOND <c>alpha</c>.</summary>
    public float alpha => _alpha;

    /// <summary>BYOND <c>color</c>: text, null, or a new list of the colour matrix.</summary>
    public object color => _color is object[] items ? Dm.list(items) : _color;

    /// <summary>BYOND <c>pixel_x</c>.</summary>
    public float pixel_x => _pixel_x;

    /// <summary>BYOND <c>pixel_y</c>.</summary>
    public float pixel_y => _pixel_y;

    /// <summary>BYOND <c>pixel_w</c>.</summary>
    public float pixel_w => _pixel_w;

    /// <summary>BYOND <c>pixel_z</c>.</summary>
    public float pixel_z => _pixel_z;

    /// <summary>BYOND <c>transform</c>: a copy of the matrix, or null.</summary>
    public object transform => _transform?.Copy();

    /// <summary>BYOND <c>appearance_flags</c>.</summary>
    public float appearance_flags => _appearance_flags;

    /// <summary>BYOND <c>blend_mode</c>.</summary>
    public float blend_mode => _blend_mode;

    /// <summary>BYOND <c>invisibility</c>.</summary>
    public float invisibility => _invisibility;

    /// <summary>BYOND <c>overlays</c>: a new list of the overlay snapshots.</summary>
    public DmList overlays => Dm.list(_overlays);

    /// <summary>BYOND <c>underlays</c>: a new list of the underlay snapshots.</summary>
    public DmList underlays => Dm.list(_underlays);

    /// <summary>BYOND <c>name</c>.</summary>
    public string name => _name;

    /// <summary>BYOND <c>desc</c>.</summary>
    public string desc => _desc;

    /// <summary>BYOND <c>maptext</c>.</summary>
    public string maptext => _maptext;

    /// <summary>BYOND <c>maptext_width</c>.</summary>
    public float maptext_width => _maptext_width;

    /// <summary>BYOND <c>maptext_height</c>.</summary>
    public float maptext_height => _maptext_height;

    /// <summary>BYOND <c>maptext_x</c>.</summary>
    public float maptext_x => _maptext_x;

    /// <summary>BYOND <c>maptext_y</c>.</summary>
    public float maptext_y => _maptext_y;

    /// <summary>BYOND <c>mouse_opacity</c>.</summary>
    public float mouse_opacity => _mouse_opacity;

    /// <summary>BYOND <c>render_source</c>.</summary>
    public object render_source => _render_source;

    /// <summary>BYOND <c>render_target</c>.</summary>
    public object render_target => _render_target;

    /// <summary>BYOND <c>vis_flags</c>.</summary>
    public float vis_flags => _vis_flags;

    /// <summary>BYOND <c>filters</c>: a copy of the source's filters list, compared item by item (the same filter objects in order).</summary>
    public object filters => _filters;

    private static bool FiltersEqual(object a, object b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is not DmList x || b is not DmList y || x.Count != y.Count) return false;
        for (int i = 1; i <= x.Count; i++)
        {
            object fa = x[(float)i], fb = y[(float)i];
            // M6 slice 7b: by value, as BYOND compares appearances (an atom's filters are copies of the ones assigned)
            if (!ReferenceEquals(fa, fb) && !(fa is dm_filter da && fb is dm_filter db && da.SameAs(db))) return false;
        }
        return true;
    }

    /// <summary>BYOND <c>density</c>.</summary>
    public float density => _density;

    /// <summary>BYOND <c>gender</c>.</summary>
    public string gender => _gender;

    /// <summary>BYOND <c>screen_loc</c>.</summary>
    public string screen_loc => _screen_loc;

    // ---- interning (structural equality; the snapshot itself keeps reference equality for Dm.eq) ----

    // Snapshots of an overlay list's items (converted, never null lists).
    private static object[] ConvertItems(DmList list)
    {
        if (list == null || list.Count == 0) return Array.Empty<object>();
        object[] items = list.CopyItems();
        for (int i = 0; i < items.Length; i++) items[i] = OverlayItem(items[i]);
        return items;
    }

    private int ComputeHash()
    {
        var h = new HashCode();
        h.Add(_icon is null ? 0 : ValueHash(_icon));
        h.Add(_icon_state is null ? 0 : string.GetHashCode(_icon_state, StringComparison.Ordinal));
        h.Add(_dir);
        h.Add(_inheritsDir);
        h.Add(_layer);
        h.Add(_plane);
        h.Add(_alpha);
        h.Add(_appearance_flags);
        h.Add(_pixel_x);
        h.Add(_pixel_y);
        h.Add(_pixel_w);
        h.Add(_pixel_z);
        h.Add(_blend_mode);
        h.Add(_color is string c ? string.GetHashCode(c, StringComparison.Ordinal) : _color is object[] m ? m.Length : 0);
        h.Add(_overlays.Length);
        foreach (object item in _overlays) h.Add(item is null ? 0 : ValueHash(item));
        h.Add(_underlays.Length);
        foreach (object item in _underlays) h.Add(item is null ? 0 : ValueHash(item));
        return h.ToHashCode();
    }

    // A hash consistent with Dm.eq for the values an appearance holds (snapshots are interned: by reference).
    private static int ValueHash(object value) => value switch
    {
        string s => string.GetHashCode(s, StringComparison.Ordinal),
        DmResource r => r.GetHashCode(),
        _ when Dm.TryGetNumber(value, out float f) => f == 0f ? 0 : f.GetHashCode(),
        _ => RuntimeHelpers.GetHashCode(value),
    };

    /// <summary>Whether two snapshots are the same appearance (every var equal; nested snapshots by reference).</summary>
    internal bool SameAppearance(DmAppearance other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || _hash != other._hash) return false;
        return Dm.eq(_icon, other._icon)
            && string.Equals(_icon_state, other._icon_state, StringComparison.Ordinal)
            && _dir == other._dir && _inheritsDir == other._inheritsDir
            && _layer == other._layer && _plane == other._plane && _alpha == other._alpha
            && SameColor(_color, other._color)
            && _pixel_x == other._pixel_x && _pixel_y == other._pixel_y && _pixel_w == other._pixel_w && _pixel_z == other._pixel_z
            && SameMatrix(_transform, other._transform)
            && _appearance_flags == other._appearance_flags && _blend_mode == other._blend_mode && _invisibility == other._invisibility
            && SameItems(_overlays, other._overlays) && SameItems(_underlays, other._underlays)
            && string.Equals(_name, other._name, StringComparison.Ordinal)
            && string.Equals(_desc, other._desc, StringComparison.Ordinal)
            && string.Equals(_maptext, other._maptext, StringComparison.Ordinal)
            && _maptext_width == other._maptext_width && _maptext_height == other._maptext_height
            && _maptext_x == other._maptext_x && _maptext_y == other._maptext_y
            && _mouse_opacity == other._mouse_opacity
            && Dm.eq(_render_source, other._render_source) && Dm.eq(_render_target, other._render_target)
            && _vis_flags == other._vis_flags && FiltersEqual(_filters, other._filters)
            && _density == other._density
            && string.Equals(_gender, other._gender, StringComparison.Ordinal)
            && string.Equals(_screen_loc, other._screen_loc, StringComparison.Ordinal);
    }

    internal int StructuralHash => _hash;

    private static bool SameColor(object a, object b)
    {
        if (a is object[] ma && b is object[] mb) return SameItems(ma, mb);
        if (a is object[] || b is object[]) return false;
        return Dm.eq(a, b);
    }

    private static bool SameMatrix(matrix a, matrix b)
    {
        if (a == null || b == null) return a == null && b == null;
        return a.a == b.a && a.b == b.b && a.c == b.c && a.d == b.d && a.e == b.e && a.f == b.f;
    }

    private static bool SameItems(object[] a, object[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!Dm.eq(a[i], b[i])) return false;
        }
        return true;
    }

    // ---- IDmAppearance ----

    object IDmAppearance.icon => _icon;
    string IDmAppearance.icon_state => _icon_state;
    float IDmAppearance.dir => _dir;
    float IDmAppearance.layer => _layer;
    float IDmAppearance.plane => _plane;
    float IDmAppearance.alpha => _alpha;
    object IDmAppearance.color => color;
    float IDmAppearance.pixel_x => _pixel_x;
    float IDmAppearance.pixel_y => _pixel_y;
    float IDmAppearance.pixel_w => _pixel_w;
    float IDmAppearance.pixel_z => _pixel_z;
    object IDmAppearance.transform => transform;
    float IDmAppearance.appearance_flags => _appearance_flags;
    float IDmAppearance.blend_mode => _blend_mode;
    float IDmAppearance.invisibility => _invisibility;
    DmList IDmAppearance.overlays => overlays;
    DmList IDmAppearance.underlays => underlays;
}

/// <summary>The structural equality of snapshots, for the per-world interning table (Appearance.World.cs).</summary>
internal sealed class DmAppearanceComparer : IEqualityComparer<DmAppearance>
{
    public static readonly DmAppearanceComparer Instance = new();

    public bool Equals(DmAppearance a, DmAppearance b) => a is null ? b is null : a.SameAppearance(b);

    public int GetHashCode(DmAppearance obj) => obj.StructuralHash;
}
