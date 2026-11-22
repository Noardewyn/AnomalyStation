using System.Linq;
using Anomaly.Protocol;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared.Icons;

// Owner: M2 P7. The appearance compositor (D104; scope 2j, "the composed stack (overlays by layer, colour, RESET_COLOR, blend modes),
// cached per appearance id and dir"). It draws an interned appearance of the replica's tables the way BYOND's renderer draws an
// atom with its overlays and underlays, into one picture per direction.
//
// What it follows (BYOND ref "appearance_flags", "layer", "color", "transform", "pixel_x", "blend_mode"):
//  - Children (overlays above, underlays below) are drawn in layer order. A float layer (negative: tg's FLOAT_LAYER and its
//    -BODYPARTS_LAYER style indexes) sorts above the parent and, among floats, by value (tg's bigger index is lower); a layer of
//    0 or more is absolute and sorts by its value against the parent. Equal layers keep list order. Nesting is compared level by
//    level, so a child's own overlays stay with it.
//  - A child inherits the parent's colour (the two multiplied, matrices composed), alpha (multiplied) and transform (the child's
//    first, then the parent's, each about its icon's centre) unless it has RESET_COLOR, RESET_ALPHA or RESET_TRANSFORM.
//    pixel_x and pixel_y move the child inside the parent's transform; pixel_w and pixel_z move it after it.
//  - KEEP_TOGETHER draws the appearance and its children into one picture first, and then colour, alpha and transform apply to
//    that picture as a whole (children with KEEP_APART stay out of the group). tg's /mob/living/carbon/human has it.
//  - A blend_mode of 0 is the parent's (overlay at the top); add, subtract, multiply and inset overlay change how a child joins
//    what was drawn below it.
//  - A child without an icon uses its parent's; without an icon state it draws the icon's default state (BYOND).
//  - Transforms sample nearest (tg's PIXEL_SCALE).
// What it does not follow: planes beyond ordering (lighting and emissive planes are not ported, DV12), filters, maptext (the HUD draws it over
// the picture, Hud/Maptext, protocol version 18), vis_contents,
// big icons' offsets (every icon sits at the parent's lower left), and invisibility (the entity rule of M1 is the server's).

/// <summary>Read access to the replica's appearance, resource and string tables (<c>ReplicaWorld</c> implements it through <see cref="ReplicaTables"/>-style adapters in the client; tests use a dictionary).</summary>
public interface IAppearanceSource
{
    /// <summary>The appearance a reference names (public or private table); false for none or an unknown index.</summary>
    bool TryGetAppearance(TableRef reference, out AppearanceEntry entry);

    /// <summary>The string a reference names (null for none).</summary>
    string ResolveString(TableRef reference);

    /// <summary>The resource a reference names (null for none): <c>icons/...dmi</c> or <c>gags:...</c>.</summary>
    string ResolveResource(TableRef reference);
}

/// <summary>A composed appearance: the picture and where the atom's own icon square sits in it.</summary>
public sealed class ComposedRaster
{
    /// <summary>The picture (alpha where nothing is drawn).</summary>
    public Raster Image { get; init; }

    /// <summary>The x of the icon square's lower left corner inside the picture, in pixels.</summary>
    public int OriginX { get; init; }

    /// <summary>The y of it from the picture's bottom.</summary>
    public int OriginY { get; init; }

    /// <summary>True when an animated state took part (the picture depends on the time).</summary>
    public bool IsAnimated { get; init; }

    /// <summary>A free slot for the owner of the picture (the engine texture made from it).</summary>
    public object Tag { get; set; }
}

/// <summary>BYOND's <c>appearance_flags</c> the compositor reads (the values are BYOND's; Anomaly.Core.DmShim.Dm has the same).</summary>
public static class AppearanceFlag
{
    public const int ResetColor = 2;
    public const int ResetAlpha = 4;
    public const int ResetTransform = 8;
    public const int KeepTogether = 32;
    public const int KeepApart = 64;
}

/// <summary>Composes appearances into pictures, caching them per appearance and direction. Main thread only.</summary>
public sealed partial class AppearanceCompositor
{
    /// <summary>The icon size when a draw has no icon to measure (tg's tile).</summary>
    public const int DefaultIconSize = 32;

    private const int MaxDepth = 12;
    private const int MaxSprites = 600;
    private const int MaxSide = 1024;

    private readonly IconLibrary _library;
    private readonly Dictionary<(uint Index, bool Private, byte Dir, bool RootOffset, bool RootOpaque), Cached> _cache = new();
    private readonly Dictionary<(uint Index, bool Private), int> _dirs = new();
    private readonly Dictionary<(uint Index, bool Private, byte Dir, bool RootOffset, bool RootOpaque, long Filters), Cached> _animatedFilters = new();
    private const int MaxAnimatedFilterPictures = 512;

    public AppearanceCompositor(IconLibrary library) => _library = library;

    /// <summary>The icons the compositor reads.</summary>
    public IconLibrary Library => _library;

    /// <summary>How many composed pictures are cached.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Drops every cached picture (a reconnect: new tables). The icon library keeps its files.</summary>
    public void Clear()
    {
        _cache.Clear();
        _dirs.Clear();
        _animatedFilters.Clear();
    }

    /// <summary>
    /// The largest number of directions (1, 4 or 8) of any state the appearance draws: an eight-direction picture is worth choosing
    /// the viewer's frame from eight sectors, a four-direction one from four.
    /// </summary>
    public int Directions(IAppearanceSource tables, TableRef root)
    {
        if (_dirs.TryGetValue((root.Index, root.Private), out int known))
            return known;
        int max = 1;
        int misses = _library.PendingMisses;
        MaxDirs(tables, root, null, 0, ref max);
        if (_library.PendingMisses == misses) // an icon still loading: counted again when it is in
            _dirs[(root.Index, root.Private)] = max;
        return max;
    }

    private void MaxDirs(IAppearanceSource tables, TableRef reference, string parentIcon, int depth, ref int max)
    {
        if (depth > MaxDepth || !tables.TryGetAppearance(reference, out AppearanceEntry entry))
            return;
        string icon = entry.Icon.IsNone ? parentIcon : tables.ResolveResource(entry.Icon);
        if (icon != null && _library.TryGetIconNow(icon, out IIcon sheet, out _) && sheet.TryGetShown(tables.ResolveString(entry.IconState) ?? string.Empty, out IIconState state))
            max = Math.Max(max, state.Dirs);
        foreach (TableRef child in entry.Underlays)
            MaxDirs(tables, child, icon, depth + 1, ref max);
        foreach (TableRef child in entry.Overlays)
            MaxDirs(tables, child, icon, depth + 1, ref max);
    }

    /// <summary>
    /// The appearance <paramref name="root"/> facing <paramref name="dir"/> at animation time <paramref name="seconds"/>; false when
    /// nothing at all could be drawn (the root and every child have a missing icon or state, or the reference is unknown). The picture
    /// carries NO pixel_x and pixel_y of the root itself unless <paramref name="rootOffset"/> is set (the HUD's screen objects, which have
    /// no 3D placement to apply it): a thing in the 3D world is moved by its placement, and the children keep their offsets relative to the root.
    /// <paramref name="rootOpaque"/> composes the root as if its alpha were 255 (M4v slice 3: a short-lived visual whose animate() chain ends at
    /// alpha 0, tg's pickup and attack images; the animation's alpha is then applied over the opaque picture).
    /// </summary>
    public bool TryCompose(IAppearanceSource tables, TableRef root, byte dir, double seconds, out ComposedRaster composed, bool rootOffset = false, bool rootOpaque = false)
        => TryCompose(tables, root, dir, seconds, out composed, rootOffset, rootOpaque, null);

    /// <summary>
    /// Version 26 (D156): as <see cref="TryCompose(IAppearanceSource, TableRef, byte, double, out ComposedRaster, bool, bool)"/> with the
    /// filters of the entries in <paramref name="filterLooks"/> drawn as it gives them (a filter animation's values now,
    /// <see cref="FilterPlayback"/>: the root's, a screen entry's children's; null: each its own). The animated values are rounded (a
    /// sixteenth of a pixel, a whole alpha) so a running animation reuses its pictures.
    /// </summary>
    public bool TryCompose(IAppearanceSource tables, TableRef root, byte dir, double seconds, out ComposedRaster composed, bool rootOffset, bool rootOpaque,
        IReadOnlyDictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks)
    {
        composed = null;
        if (filterLooks is { Count: > 0 })
            return TryComposeFiltered(tables, root, dir, seconds, out composed, rootOffset, rootOpaque, filterLooks);
        var key = (root.Index, root.Private, dir, rootOffset, rootOpaque);
        if (_cache.TryGetValue(key, out Cached cached))
        {
            if (cached.Animated == null)
            {
                composed = cached.Result;
                return composed != null;
            }
            long signature = Signature(cached.Animated, seconds);
            if (signature == cached.Signature)
            {
                composed = cached.Result;
                return composed != null;
            }
        }

        if (!tables.TryGetAppearance(root, out AppearanceEntry entry))
            return false;
        int misses = _library.PendingMisses;
        var run = new Run(this, tables, seconds, rootOffset, rootOpaque);
        Context top = Context.Root(dir);
        run.Visit(entry, top, Array.Empty<double>(), true, 0, run.Sprites);
        List<IIconState> animated = run.Animated.Count > 0 ? run.Animated : null;
        ComposedRaster result = run.Sprites.Count == 0 ? null : Render(run.Sprites, run.RootWidth, run.RootHeight, animated != null);
        if (_library.PendingMisses != misses)
            return false; // an icon is still loading (IconLibrary.Background): nothing cached, composed again on a later frame
        _cache[key] = new Cached
        {
            Result = result,
            Animated = animated?.ToArray(),
            Signature = animated == null ? 0 : Signature(animated, seconds),
            Blank = run.Sprites.Count == 0 && run.FoundIcon && !run.MissingIcon,
        };
        composed = result;
        return composed != null;
    }

    private bool TryComposeFiltered(IAppearanceSource tables, TableRef root, byte dir, double seconds, out ComposedRaster composed, bool rootOffset, bool rootOpaque,
        IReadOnlyDictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks)
    {
        composed = null;
        var rounded = new Dictionary<AppearanceEntry, IReadOnlyList<FilterEntry>>(ReferenceEqualityComparer.Instance);
        long signature = 17;
        foreach (KeyValuePair<AppearanceEntry, IReadOnlyList<FilterEntry>> look in filterLooks.OrderBy(l => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(l.Key)))
        {
            List<FilterEntry> made = look.Value.Select(Rounded).ToList();
            rounded[look.Key] = made;
            signature = signature * 1_000_003 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(look.Key);
            foreach (FilterEntry f in made)
                signature = signature * 1_000_003 + FilterSignature(f);
        }
        var key = (root.Index, root.Private, dir, rootOffset, rootOpaque, signature);
        if (_animatedFilters.TryGetValue(key, out Cached cached) && (cached.Animated == null || Signature(cached.Animated, seconds) == cached.Signature))
        {
            composed = cached.Result;
            return composed != null;
        }
        if (!tables.TryGetAppearance(root, out AppearanceEntry entry))
            return false;
        int misses = _library.PendingMisses;
        var run = new Run(this, tables, seconds, rootOffset, rootOpaque) { FilterLooks = rounded };
        run.Visit(entry, Context.Root(dir), Array.Empty<double>(), true, 0, run.Sprites);
        List<IIconState> animated = run.Animated.Count > 0 ? run.Animated : null;
        ComposedRaster result = run.Sprites.Count == 0 ? null : Render(run.Sprites, run.RootWidth, run.RootHeight, animated != null);
        if (_library.PendingMisses != misses)
            return false; // an icon is still loading: nothing cached
        if (_animatedFilters.Count >= MaxAnimatedFilterPictures)
            _animatedFilters.Clear();
        _animatedFilters[key] = new Cached
        {
            Result = result,
            Animated = animated?.ToArray(),
            Signature = animated == null ? 0 : Signature(animated, seconds),
        };
        composed = result;
        return composed != null;
    }

    // An animated filter's numbers rounded as the cache keys them: positions and sizes to a sixteenth, alpha whole, colours as they are.
    private static FilterEntry Rounded(FilterEntry f)
    {
        FilterEntry r = f.Copy();
        static float Q(float v) => MathF.Round(v * 16f) / 16f;
        r.X = Q(r.X);
        r.Y = Q(r.Y);
        r.Size = Q(r.Size);
        r.Offset = Q(r.Offset);
        r.Radius = Q(r.Radius);
        r.Alpha = MathF.Round(r.Alpha);
        if (r.ColorMatrix != null)
            for (int i = 0; i < r.ColorMatrix.Length; i++)
                r.ColorMatrix[i] = MathF.Round(r.ColorMatrix[i] * 64f) / 64f;
        return r;
    }

    private static long FilterSignature(FilterEntry f)
    {
        var h = new HashCode();
        h.Add(f.Kind);
        h.Add(f.X);
        h.Add(f.Y);
        h.Add(f.Size);
        h.Add(f.Offset);
        h.Add(f.Alpha);
        h.Add(f.Radius);
        h.Add(f.Falloff);
        h.Add(f.Repeat);
        h.Add(f.Density);
        h.Add(f.Threshold);
        h.Add(f.Factor);
        h.Add(f.Rgba);
        h.Add(f.Flags);
        if (f.ColorMatrix != null)
            foreach (float n in f.ColorMatrix) h.Add(n);
        return (uint)h.ToHashCode() | ((long)f.Kind << 32);
    }

    /// <summary>
    /// The animated icon states the picture of <paramref name="root"/> facing <paramref name="dir"/> is drawn from (null: none animates,
    /// or nothing could be drawn): what <see cref="AnimationTimeline.Of"/> steps through.
    /// </summary>
    public IReadOnlyList<IIconState> AnimatedStates(IAppearanceSource tables, TableRef root, byte dir, bool rootOffset = false)
    {
        if (!TryCompose(tables, root, dir, 0, out _, rootOffset))
            return null;
        return _cache.TryGetValue((root.Index, root.Private, dir, rootOffset, false), out Cached cached) ? cached.Animated : null;
    }

    /// <summary>
    /// True when <paramref name="root"/> draws nothing although every icon it names is there: its icon states (and their children's) are
    /// missing from their icons and no icon has a default (unnamed) state. BYOND then shows nothing (tg's trail particles blank themselves
    /// with <c>icon_state = ""</c> after their flick, effects_trail.dm:72), so the renderers draw no placeholder for it. False when an icon
    /// file is missing (the placeholder stays: the icons are not converted) or when the picture composes. True while an icon is still
    /// loading (IconLibrary.Background): nothing is drawn for those few frames, not a placeholder.
    /// </summary>
    public bool IsBlank(IAppearanceSource tables, TableRef root, byte dir, bool rootOffset = false, bool rootOpaque = false)
    {
        int misses = _library.PendingMisses;
        if (TryCompose(tables, root, dir, 0, out _, rootOffset, rootOpaque))
            return false;
        if (_library.PendingMisses != misses)
            return true;
        return _cache.TryGetValue((root.Index, root.Private, dir, rootOffset, rootOpaque), out Cached cached) && cached.Blank;
    }

    private static long Signature(IReadOnlyList<IIconState> animated, double seconds)
    {
        long signature = 17;
        foreach (IIconState state in animated)
            signature = signature * 31 + state.FrameAt(seconds);
        return signature;
    }

    private sealed class Cached
    {
        public ComposedRaster Result;
        public IIconState[] Animated;
        public long Signature;
        public bool Blank;
    }

    // ---- the walk ----

    private readonly record struct Context(
        Affine ToRoot, double PostX, double PostY, ColorMatrix Color, int Alpha, int Blend, string Icon, byte Dir, int Width, int Height)
    {
        public static Context Root(byte dir) => new(Affine.Identity, 0, 0, ColorMatrix.Identity, 255, 1, null, dir, DefaultIconSize, DefaultIconSize);
    }

    private sealed class Sprite
    {
        public Raster Image;
        public int Left, Bottom;
        public Affine ToRoot;
        public ColorMatrix Color;
        public int Alpha;
        public int Blend;
        public double[] Path;
        public int Seq;
    }

    private sealed class Run
    {
        private readonly AppearanceCompositor _owner;
        private readonly IAppearanceSource _tables;
        private readonly double _seconds;
        private readonly bool _rootOffset;
        private readonly bool _rootOpaque;
        private int _seq;
        private bool _rootSeen;

        public readonly List<Sprite> Sprites = new();
        public readonly List<IIconState> Animated = new();

        /// <summary>Version 25: the pictures of the render targets met so far, by name (a filter's render source takes them).</summary>
        public readonly Dictionary<string, ComposedRaster> Targets = new(StringComparer.Ordinal);

        /// <summary>Version 26: the filters of animated entries as an animation shows them now, by entry (null: each its own).</summary>
        public IReadOnlyDictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> FilterLooks;

        public int RootWidth = DefaultIconSize, RootHeight = DefaultIconSize;

        /// <summary>An icon file named by the walk was found, or one could not be read (<see cref="IsBlank"/>).</summary>
        public bool FoundIcon, MissingIcon;

        public Run(AppearanceCompositor owner, IAppearanceSource tables, double seconds, bool rootOffset, bool rootOpaque = false)
        {
            _owner = owner;
            _tables = tables;
            _seconds = seconds;
            _rootOffset = rootOffset;
            _rootOpaque = rootOpaque;
        }

        /// <summary>Draws <paramref name="e"/> and its children into <paramref name="into"/>; <paramref name="path"/> is the entry's sort path (its parent's plus its layer relative to the parent).</summary>
        public void Visit(AppearanceEntry e, Context parent, double[] path, bool isRoot, int depth, List<Sprite> into)
        {
            if (depth > MaxDepth || Sprites.Count > MaxSprites)
                return;
            string icon = e.Icon.IsNone ? parent.Icon : _tables.ResolveResource(e.Icon);
            string stateName = _tables.ResolveString(e.IconState) ?? string.Empty;
            IIconState state = null;
            bool pending = false;
            if (icon != null && _owner._library.TryGetIconNow(icon, out IIcon source, out pending))
            {
                FoundIcon = true;
                source.TryGetShown(stateName, out state); // BYOND: a missing state shows the default one
            }
            else if (icon != null && !pending)
            {
                MissingIcon = true;
            }
            int width = state?.Width ?? parent.Width, height = state?.Height ?? parent.Height;
            if (isRoot && !_rootSeen)
            {
                _rootSeen = true;
                RootWidth = width;
                RootHeight = height;
            }

            int flags = e.AppearanceFlags;
            // BYOND: an overlay or underlay turns with its parent; tg's mutable_appearance and image carry BYOND's default dir
            // (SOUTH), so a SOUTH child follows the parent too (the owner's report, 2026-10-09: other players always faced
            // the viewer, their body parts and clothes being SOUTH overlays of a root without an icon). Another explicit dir
            // stays the child's own.
            byte dir = isRoot ? parent.Dir : (e.Dir != 0 && e.Dir != ByondDir.South ? e.Dir : parent.Dir);

            // Own transform about the centre of the icon, then pixel_x and pixel_y; then the parent's transform.
            Affine own = Affine.Identity;
            if (e.Transform != null)
                own = Affine.Translate(-width / 2.0, -height / 2.0).Then(Affine.FromMatrix(e.Transform)).Then(Affine.Translate(width / 2.0, height / 2.0));
            // The root's own offset is the placement's (M3 R2): only a caller with no placement asks for it in the picture.
            if (!isRoot || _rootOffset)
                own = own.Then(Affine.Translate(e.PixelX, e.PixelY));
            Affine parentTransform = (flags & AppearanceFlag.ResetTransform) != 0 ? Affine.Translate(parent.ToRoot.C, parent.ToRoot.F) : parent.ToRoot;

            ColorMatrix ownColor = e.ColorMatrix != null ? ColorMatrix.FromList(e.ColorMatrix) : ColorMatrix.Tint(e.Rgba);
            ColorMatrix color = (flags & AppearanceFlag.ResetColor) != 0 ? ownColor : ownColor.Then(parent.Color);
            int alpha = isRoot && _rootOpaque ? 255 : (flags & AppearanceFlag.ResetAlpha) != 0 ? e.Alpha : e.Alpha * parent.Alpha / 255;
            int blend = e.BlendMode switch { 1 => 1, 2 => 2, 3 => 3, 4 => 4, 5 => 5, _ => parent.Blend };

            var context = new Context(own.Then(parentTransform), parent.PostX + e.PixelW, parent.PostY + e.PixelZ, color, alpha, blend, icon, dir, width, height);

            // Version 25 (D155): filters apply to the picture of the appearance with its children, so a filtered appearance is drawn as a
            // group, as KEEP_TOGETHER draws one; a render target keeps that picture by name for a later filter's render source, and one
            // starting with * is not drawn itself (BYOND ref "render_target").
            bool keepTogether = (flags & AppearanceFlag.KeepTogether) != 0;
            IReadOnlyList<FilterEntry> filters = FilterLooks != null && FilterLooks.TryGetValue(e, out IReadOnlyList<FilterEntry> animatedFilters) ? animatedFilters : e.Filters;
            bool filtered = filters is { Count: > 0 };
            string target = e.RenderTarget.IsNone ? null : _tables.ResolveString(e.RenderTarget);
            bool hidden = target != null && target.StartsWith('*');
            if (keepTogether || filtered || target != null)
            {
                // The appearance and its children into one picture; colour, alpha and transform then apply to the picture as a whole.
                var inner = new List<Sprite>();
                var group = new Context(Affine.Identity, 0, 0, ColorMatrix.Identity, 255, 1, icon, dir, width, height);
                EmitOwn(state, group, new[] { 0.0 }, inner);
                // KEEP_APART children are drawn outside the group, after it: the group's render target is the picture without them, which
                // they may take as a render source (tg's blood stain cut to a colour-filtered item's shape, decals/blood.dm:46; M6 slice 7b)
                List<(AppearanceEntry Child, double Relative)> apart = null;
                foreach ((AppearanceEntry child, double relative) in Children(e))
                {
                    if (keepTogether && (child.AppearanceFlags & AppearanceFlag.KeepApart) != 0)
                        (apart ??= new List<(AppearanceEntry, double)>()).Add((child, relative));
                    else
                        Visit(child, group, new[] { relative }, false, depth + 1, inner);
                }
                ComposedRaster picture = inner.Count == 0 ? null : _owner.Render(inner, width, height, false);
                if (picture != null)
                {
                    if (filtered)
                        picture = _owner.ApplyFilters(_tables, filters, picture, Targets);
                    if (target != null)
                        Targets[target] = picture;
                    if (!hidden)
                    {
                        into.Add(new Sprite
                        {
                            Image = picture.Image,
                            Left = -picture.OriginX,
                            Bottom = -picture.OriginY,
                            ToRoot = context.ToRoot.Then(Affine.Translate(context.PostX, context.PostY)),
                            Color = context.Color,
                            Alpha = context.Alpha,
                            Blend = context.Blend,
                            Path = Append(path, 0.0),
                            Seq = _seq++,
                        });
                    }
                }
                if (apart != null)
                    foreach ((AppearanceEntry child, double relative) in apart)
                        Visit(child, context, Append(path, relative), false, depth + 1, into);
                return;
            }

            EmitOwn(state, context, Append(path, 0.0), into);
            foreach ((AppearanceEntry child, double relative) in Children(e))
                Visit(child, context, Append(path, relative), false, depth + 1, into);
        }

        // The children of an entry with their layer relative to it: underlays then overlays, in list order. A float layer
        // (negative) floats just above the parent (overlays) or just below it (underlays), ordered among floats by its value; any other
        // layer is absolute, so it is relative to the parent's own layer.
        private IEnumerable<(AppearanceEntry, double)> Children(AppearanceEntry e)
        {
            double basis = Math.Max(0, e.Layer);
            foreach (TableRef reference in e.Underlays)
            {
                if (_tables.TryGetAppearance(reference, out AppearanceEntry child))
                    yield return (child, Relative(child, basis, true, child.Plane < e.Plane));
            }
            foreach (TableRef reference in e.Overlays)
            {
                if (_tables.TryGetAppearance(reference, out AppearanceEntry child))
                    yield return (child, Relative(child, basis, false, child.Plane != FloorLayers.FloatPlane && child.Plane < e.Plane));
            }
        }

        // belowPlane: an underlay or overlay on a lower plane than its parent is under it whatever its layer (a diagonal wall's space underlay is on tg's floor plane at layer 10002, over the wall's 2.058 by layer alone;
        // a rock's wall-plane copy, add_large_wall_overlay, is under its smoothed game-plane picture)
        private static double Relative(AppearanceEntry child, double basis, bool underlay, bool belowPlane)
        {
            if (belowPlane)
                return -1e-9;
            if (child.Layer < 0)
                return (1000.0 + Math.Max(child.Layer, -1000.0)) / 1000.0 - (underlay ? 1.0 : 0.0);
            double relative = child.Layer - basis;
            // At the parent's own layer an underlay is still below it and an overlay above (the parent is drawn first among equals).
            return underlay && relative == 0 ? -1e-9 : relative;
        }

        private void EmitOwn(IIconState state, Context c, double[] path, List<Sprite> into)
        {
            if (state == null)
                return;
            if (state.IsAnimated && !Animated.Contains(state))
                Animated.Add(state);
            Raster frame = state.Get(ByondDir.Index(c.Dir, state.Dirs), state.FrameAt(_seconds));
            into.Add(new Sprite
            {
                Image = frame,
                Left = 0,
                Bottom = 0,
                ToRoot = c.ToRoot.Then(Affine.Translate(c.PostX, c.PostY)),
                Color = c.Color,
                Alpha = c.Alpha,
                Blend = c.Blend,
                Path = path,
                Seq = _seq++,
            });
        }

        private static double[] Append(double[] path, double value)
        {
            var result = new double[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = value;
            return result;
        }
    }

    // ---- the picture ----

    private ComposedRaster Render(List<Sprite> sprites, int squareWidth, int squareHeight, bool animated)
    {
        sprites.Sort(static (a, b) =>
        {
            int n = Math.Min(a.Path.Length, b.Path.Length);
            for (int i = 0; i < n; i++)
            {
                int c = a.Path[i].CompareTo(b.Path[i]);
                if (c != 0)
                    return c;
            }
            int byLength = a.Path.Length.CompareTo(b.Path.Length);
            return byLength != 0 ? byLength : a.Seq.CompareTo(b.Seq);
        });

        // The union of the transformed sprites and the icon square (so that the square's origin is stable).
        double minX = 0, minY = 0, maxX = squareWidth, maxY = squareHeight;
        foreach (Sprite s in sprites)
        {
            double w = s.Image.Width, h = s.Image.Height;
            foreach ((double cx, double cy) in new[] { (0.0, 0.0), (w, 0.0), (0.0, h), (w, h) })
            {
                (double x, double y) = s.ToRoot.Apply(s.Left + cx, s.Bottom + cy);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }
        int left = (int)Math.Floor(minX + 1e-6), right = (int)Math.Ceiling(maxX - 1e-6);
        int bottom = (int)Math.Floor(minY + 1e-6), top = (int)Math.Ceiling(maxY - 1e-6);
        int width = Math.Min(right - left, MaxSide), height = Math.Min(top - bottom, MaxSide);
        if (width <= 0 || height <= 0)
            return null;

        var image = new Raster(width, height);
        byte[] scratch = new byte[4];
        foreach (Sprite s in sprites)
        {
            if (!s.ToRoot.TryInvert(out Affine inverse))
                continue;
            int imageWidth = s.Image.Width, imageHeight = s.Image.Height;
            bool tinted = !s.Color.IsIdentity || s.Alpha < 255;
            for (int oy = 0; oy < height; oy++)
            {
                double py = top - oy - 0.5;
                for (int ox = 0; ox < width; ox++)
                {
                    double px = left + ox + 0.5;
                    (double lx, double ly) = inverse.Apply(px, py);
                    int col = (int)Math.Floor(lx - s.Left), fromBottom = (int)Math.Floor(ly - s.Bottom);
                    if (col < 0 || col >= imageWidth || fromBottom < 0 || fromBottom >= imageHeight)
                        continue;
                    int src = ((imageHeight - 1 - fromBottom) * imageWidth + col) * 4;
                    byte[] pixels = s.Image.Pixels;
                    if (pixels[src + 3] == 0 && s.Blend != 5)
                        continue;
                    int dst = (oy * width + ox) * 4;
                    if (tinted)
                    {
                        scratch[0] = pixels[src];
                        scratch[1] = pixels[src + 1];
                        scratch[2] = pixels[src + 2];
                        scratch[3] = pixels[src + 3];
                        s.Color.Apply(scratch, 0);
                        if (s.Alpha < 255)
                            scratch[3] = Raster.Mul(scratch[3], s.Alpha);
                        DrawPixel(image.Pixels, dst, scratch, 0, s.Blend);
                    }
                    else
                    {
                        DrawPixel(image.Pixels, dst, pixels, src, s.Blend);
                    }
                }
            }
        }
        return new ComposedRaster { Image = image, OriginX = -left, OriginY = -bottom, IsAnimated = animated };
    }

    // BLEND_OVERLAY 1, ADD 2, SUBTRACT 3, MULTIPLY 4, INSET_OVERLAY 5: how a drawn pixel joins the picture below it.
    private static void DrawPixel(byte[] dst, int d, byte[] src, int s, int blend)
    {
        int sa = src[s + 3];
        switch (blend)
        {
            case 2:
                if (sa == 0)
                    return;
                dst[d] = (byte)Math.Min(255, dst[d] + Raster.Mul(src[s], sa));
                dst[d + 1] = (byte)Math.Min(255, dst[d + 1] + Raster.Mul(src[s + 1], sa));
                dst[d + 2] = (byte)Math.Min(255, dst[d + 2] + Raster.Mul(src[s + 2], sa));
                dst[d + 3] = (byte)Math.Max(dst[d + 3], sa);
                break;
            case 3:
                if (sa == 0)
                    return;
                dst[d] = (byte)Math.Max(0, dst[d] - Raster.Mul(src[s], sa));
                dst[d + 1] = (byte)Math.Max(0, dst[d + 1] - Raster.Mul(src[s + 1], sa));
                dst[d + 2] = (byte)Math.Max(0, dst[d + 2] - Raster.Mul(src[s + 2], sa));
                break;
            case 4:
                if (sa == 0)
                    return;
                // Multiply the picture below by the colour, as far as the pixel is opaque.
                dst[d] = Raster.Mul(dst[d], 255 - sa + Raster.Mul(src[s], sa));
                dst[d + 1] = Raster.Mul(dst[d + 1], 255 - sa + Raster.Mul(src[s + 1], sa));
                dst[d + 2] = Raster.Mul(dst[d + 2], 255 - sa + Raster.Mul(src[s + 2], sa));
                break;
            case 5:
            {
                // Only where the picture below is drawn, in proportion to its alpha.
                int clipped = Raster.Mul(sa, dst[d + 3]);
                if (clipped == 0)
                    return;
                int keep = dst[d + 3];
                Raster.BlendPixel(dst, d, new[] { src[s], src[s + 1], src[s + 2], (byte)clipped }, 0, IconBlend.Overlay);
                dst[d + 3] = (byte)keep;
                break;
            }
            default:
                Raster.BlendPixel(dst, d, src, s, IconBlend.Overlay);
                break;
        }
    }
}
