using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// The pictures of floors, ceilings and walls (scope 2i): every distinct turf picture is a layer of a <see cref="Texture2DArray"/>
/// (32 x 32, mipmapped, nearest filtering), so a chunk draws all its tiles with one material and one MultiMesh whatever their number.
/// A picture whose icon is missing (or a map with no appearances) gets a flat tinted layer, M1's look. A page holds at most
/// <see cref="PageLayers"/> layers (the engine's limit is 2048); more pictures start another page and another MultiMesh per chunk.
/// </summary>
public sealed class TileKit
{
    /// <summary>Layers per Texture2DArray page.</summary>
    public const int PageLayers = 1800;

    private const int Side = AppearanceCompositor.DefaultIconSize;

    private readonly AppearanceTextures _textures;
    private readonly IAppearanceSource _tables;
    private readonly List<List<Image>> _pages = new() { new List<Image>() };
    private readonly Dictionary<long, (int Page, int Layer)> _layers = new();
    private readonly Dictionary<(string Picture, int Page), int> _sideLayers = new();
    private readonly List<ShaderMaterial> _materials = new();
    private readonly List<int> _uploaded = new(); // the layers of each page that its Texture2DArray has
    private readonly Dictionary<string, Variant> _parameters = new();
    private readonly Dictionary<(int Page, int Layer), int> _groups = new();

    public TileKit(AppearanceTextures textures, IAppearanceSource tables)
    {
        _textures = textures;
        _tables = tables;
    }

    /// <summary>
    /// The overlays a turf's pictures leave out because they are drawn another way (C-4's charge stuck to a wall or a floor, a model at its attach point); null:
    /// none.
    /// </summary>
    public OverlayStripper Stripper { get; set; }

    /// <summary>Layers whose picture is tg's, and layers that are flat tints.</summary>
    public int TexturedLayers { get; private set; }

    public int TintedLayers { get; private set; }

    /// <summary>True when a ceiling layer (the dimmed plating) is among the textured layers.</summary>
    public bool HasCeilingLayer { get; private set; }

    /// <summary>The number of Texture2DArray pages.</summary>
    public int PageCount => _pages.Count;

    /// <summary>The page and layer of a tile's picture (the texture is made once per distinct picture).</summary>
    public (int Page, int Layer) LayerOf(TileCell tile, IReadOnlyList<string> turfTypes)
    {
        if (tile.Shape == TileShape.Ceiling)
            HasCeilingLayer = true;
        // D295: a ceiling's block is drawn with the picture tg would draw for it, the floors' way; without one (the fake feed, a
        // diagonal wall's cut corner) it is tg's plating
        if (tile.Shape == TileShape.Ceiling && tile.Appearance.IsNone)
            return Get(PlatingKey, PlatingImage, textured: true);
        if (!tile.Appearance.IsNone && _tables != null && _textures != null)
        {
            long key = tile.Appearance.Index;
            if (_layers.TryGetValue(key, out var existing))
                return existing;
            (IAppearanceSource source, TableRef look) = Shown(tile.Appearance);
            if (TryTurfImages(source, look, out List<Image> images, out AnimationTimeline timeline))
                return images.Count == 1 ? Get(key, () => images[0], textured: true) : GetAnimated(key, images, timeline);
        }
        long tintKey = -1000L - tile.TypeIndex * 2L - (tile.Shape == TileShape.Wall ? 1 : 0);
        string type = tile.TypeIndex >= 0 && tile.TypeIndex < turfTypes.Count ? turfTypes[tile.TypeIndex] : null;
        return Get(tintKey, () => TintImage(type, tile.Shape == TileShape.Wall), textured: false);
    }

    /// <summary>
    /// The layer of a wall's one SIDE picture (D121, the owner: a wall seen from the side shows one picture, the straight east-west run <c>&lt;base&gt;-12</c> of its type, whatever
    /// junction its own appearance carries; <see cref="StraightRun"/> is the rule the windows use too), in the same page as the tile's own picture, which the top face keeps.
    /// -1 when the tile is not a wall, has no appearance, or its icon has no straight run: the sides then show the tile's own picture.
    /// </summary>
    public int SideLayerOf(TileCell tile, int page, int ownLayer)
    {
        if (tile.Shape != TileShape.Wall || tile.Appearance.IsNone || _tables == null || _textures == null)
            return -1;
        // a wall between walls on the levels over and under it joins them (Smoothing.SideJunction); an icon without that picture keeps the plain run
        int junction = Smoothing.SideJunction(tile.Stack);
        if (StraightRun.TryFind(_tables, tile.Appearance, _textures.Library, out _, out _, junction))
            return SideLayerFor(tile, page, ownLayer, junction);
        return SideLayerFor(tile, page, ownLayer, Smoothing.EastWest);
    }

    /// <summary>
    /// The layers of a wall's four side pictures (south, east, north, west), each its own face's junction (<see cref="BlockFaces.WallFace"/>: framed where the face meets what
    /// the wall does not join, the owner 2026-11-10); null when the wall's state carries no junction (it does not smooth: its sides keep <see cref="SideLayerOf"/>), -1 for a
    /// face whose picture the icon lacks.
    /// </summary>
    public int[] FaceLayersOf(TileCell tile, int page, int ownLayer)
    {
        if (tile.Shape != TileShape.Wall || tile.Appearance.IsNone || _tables == null || _textures == null || !_tables.TryGetAppearance(tile.Appearance, out AppearanceEntry entry)
            || !WindowJunction.TryParse(_tables.ResolveString(entry.IconState), out int junction))
            return null;
        var layers = new int[4];
        for (int i = 0; i < 4; i++)
        {
            int face = BlockFaces.WallFace(junction, FaceOrder[i], tile.Stack, tile.NearStack, tile.Diagonals);
            layers[i] = StraightRun.TryFind(_tables, tile.Appearance, _textures.Library, out _, out _, face) ? SideLayerFor(tile, page, ownLayer, face) : -1;
        }
        return layers;
    }

    // the order of the colour channels turf_kit.gdshader reads a wall's faces from
    private static readonly byte[] FaceOrder = { ByondDirConst.South, ByondDirConst.East, ByondDirConst.North, ByondDirConst.West };

    // The layer of the wall's side picture of a junction, made once per icon, state and page.
    private int SideLayerFor(TileCell tile, int page, int ownLayer, int junction)
    {
        if (!StraightRun.TryFind(_tables, tile.Appearance, _textures.Library, out string icon, out string state, junction))
            return -1;
        bool known_ = _tables.TryGetAppearance(tile.Appearance, out AppearanceEntry entry);
        if (known_ && _tables.ResolveString(entry.IconState) == state)
            return ownLayer; // the wall already is its straight run (its own picture carries its overlays)
        // M5x slice 2: a wall's overlays are drawn on its sides too, over the straight run; one drawn as a model (C4's charge stuck to it, plastic.dm:150,
        // on the face it was planted on) is not
        (IAppearanceSource shownSource, TableRef shownLook) = Shown(tile.Appearance);
        AppearanceEntry shown = null;
        bool overlays = known_ && shownSource.TryGetAppearance(shownLook, out shown) && shown.OverlayCount > 0;
        var key = (icon + "#" + state + (overlays ? "#" + tile.Appearance.Index : ""), page);
        if (_sideLayers.TryGetValue(key, out int known))
            return known;
        if (!StraightRun.TryGetPicture(_tables, tile.Appearance, _textures.Library, out Raster picture, junction))
            return -1;
        Raster square = StraightRun.TileSquare(_tables, tile.Appearance, picture, Side);
        if (overlays)
        {
            foreach (TableRef overlay in shown.Overlays)
            {
                if (_textures.TryGet(_tables, overlay, ByondDir.South, 0, out ComposedRaster drawn, out _))
                    square.Blend(drawn.Image, IconBlend.Overlay, -drawn.OriginX, -(drawn.Image.Height - drawn.OriginY - Side));
            }
        }
        Image image = AppearanceTextures.ToImage(square);
        image.GenerateMipmaps();
        // Side pictures are few (a wall type's junctions that show): a page that is full still takes them (the engine's limit is 2048 layers, the page's own 1800 leaves room)
        if (_pages[page].Count >= 2040)
            return -1;
        _pages[page].Add(image);
        TexturedLayers++;
        return _sideLayers[key] = _pages[page].Count - 1;
    }

    /// <summary>
    /// The quarter turns of the diagonal wall mesh for a wall whose icon state is a diagonal corner (<c>shuttle_wall-5-diagonal</c>: tg's SMOOTH_DIAGONAL_CORNERS); -1 for any other tile
    /// (a plain box).
    /// </summary>
    public int DiagonalTurnsOf(TileCell tile)
    {
        if (tile.Shape != TileShape.Wall || tile.Appearance.IsNone || _tables == null || !_tables.TryGetAppearance(tile.Appearance, out AppearanceEntry entry))
            return -1;
        string state = _tables.ResolveString(entry.IconState);
        if (state == null || !state.EndsWith("-diagonal", StringComparison.Ordinal))
            return -1;
        string head = state.Substring(0, state.Length - "-diagonal".Length);
        int dash = head.LastIndexOf('-');
        return dash > 0 && int.TryParse(head.AsSpan(dash + 1), out int junction) ? TileKitParts.TurnsOf(junction) : -1;
    }

    /// <summary>
    /// The floor under a diagonal corner wall: the cut-away corner of such a wall shows the picture tg puts under it (its first underlay: the neighbour floor's, which smoothing
    /// copies there, or space's), and without a floor in the cell there is a hole down to the void. False when the wall is not a diagonal one or its underlay is space's (open space
    /// outside the hull needs no floor).
    /// </summary>
    public bool TryUnderlayLayer(TileCell tile, out int page, out int layer)
    {
        page = layer = 0;
        if (DiagonalTurnsOf(tile) < 0 || !_tables.TryGetAppearance(tile.Appearance, out AppearanceEntry entry) || entry.Underlays.Count == 0)
            return false;
        TableRef under = entry.Underlays[0];
        if (!_tables.TryGetAppearance(under, out AppearanceEntry underlay))
            return false;
        string icon = _tables.ResolveResource(underlay.Icon);
        if (icon == null || icon.Contains("space", StringComparison.OrdinalIgnoreCase))
            return false;
        long key = -2000000000L - under.Index;
        if (_layers.TryGetValue(key, out var existing))
        {
            (page, layer) = existing;
            return true;
        }
        if (!_textures.TryGet(_tables, under, ByondDir.South, 0, out ComposedRaster raster, out _))
            return false;
        (page, layer) = Get(key, () => SquareImage(raster), textured: true);
        return true;
    }

    private (int Page, int Layer) Get(long key, Func<Image> make, bool textured)
    {
        if (_layers.TryGetValue(key, out var existing))
            return existing;
        List<Image> page = _pages[^1];
        if (page.Count >= PageLayers)
        {
            page = new List<Image>();
            _pages.Add(page);
        }
        Image image = make();
        image.GenerateMipmaps();
        page.Add(image);
        var entry = (_pages.Count - 1, page.Count - 1);
        _layers[key] = entry;
        if (textured)
            TexturedLayers++;
        else
            TintedLayers++;
        return entry;
    }

    // M4v slice 2 (D143): an animated turf picture's layers, one per picture of its timeline (AnimationTimeline: BYOND's delays,
    // loops and rewind), consecutive in one page; the renderer steps its tiles through them (TurfFrames).
    private (int Page, int Layer) GetAnimated(long key, List<Image> images, AnimationTimeline timeline)
    {
        List<Image> page = _pages[^1];
        if (page.Count + images.Count > PageLayers)
        {
            page = new List<Image>();
            _pages.Add(page);
        }
        int first = page.Count;
        var layers = new int[images.Count];
        for (int i = 0; i < images.Count; i++)
        {
            images[i].GenerateMipmaps();
            page.Add(images[i]);
            layers[i] = first + i;
        }
        TexturedLayers += images.Count;
        var entry = (_pages.Count - 1, first);
        _layers[key] = entry;
        _groups[entry] = Frames.Add(timeline, layers);
        return entry;
    }

    /// <summary>The animated turf pictures (M4v slice 2): their timelines and layers.</summary>
    public TurfFrames Frames { get; } = new();

    /// <summary>The <see cref="Frames"/> group whose first layer is <paramref name="layer"/> of <paramref name="page"/>; -1 for a still picture.</summary>
    public int GroupOf(int page, int layer) => _groups.TryGetValue((page, layer), out int group) ? group : -1;

    private readonly Dictionary<long, bool> _wet = new();

    /// <summary>
    /// True when the tile carries tg's water overlay (wet_floor.dm update_overlay: water.dmi "wet_floor_static" or "wet_static"). Its pale sheen vanishes under 3D
    /// light, so the shader also draws the tile dark and glossy.
    /// </summary>
    // DEVIATION(DV265): a wet floor is drawn darker and glossy over tg's water overlay, which alone is invisible in 3D.
    public bool IsWet(TileCell tile)
    {
        if (tile.Shape != TileShape.Floor || tile.Appearance.IsNone || _tables == null)
            return false;
        if (_wet.TryGetValue(tile.Appearance.Index, out bool known))
            return known;
        bool wet = _tables.TryGetAppearance(tile.Appearance, out AppearanceEntry entry)
            && entry.Overlays.Any(o => _tables.TryGetAppearance(o, out AppearanceEntry e) && _tables.ResolveResource(e.Icon) == "icons/effects/water.dmi"
                && _tables.ResolveString(e.IconState) is "wet_floor_static" or "wet_static");
        return _wet[tile.Appearance.Index] = wet;
    }

    // The turf's picture, or its pictures when its icon states animate (null timeline: one still picture).
    // The appearance a turf's picture is composed from: its own, or a copy without the overlays drawn as models (Stripper).
    private (IAppearanceSource Source, TableRef Look) Shown(TableRef appearance)
        => Stripper == null ? (_tables, appearance) : (Stripper, Stripper.Strip(appearance));

    private bool TryTurfImages(IAppearanceSource source, TableRef appearance, out List<Image> images, out AnimationTimeline timeline)
    {
        images = null;
        timeline = null;
        if (!_textures.TryGet(source, appearance, ByondDir.South, 0, out ComposedRaster raster, out _))
            return false;
        images = new List<Image> { SquareImage(raster) };
        if (!raster.IsAnimated)
            return true;
        AnimationTimeline animated = AnimationTimeline.Of(_textures.Compositor.AnimatedStates(source, appearance, ByondDir.South), once: false);
        if (animated == null || animated.Count <= 1)
            return true;
        var frames = new List<Image>(animated.Count);
        for (int i = 0; i < animated.Count; i++)
        {
            if (!_textures.TryGet(source, appearance, ByondDir.South, animated.SampleAt(i), out ComposedRaster frame, out _))
                return true; // the still picture, as before
            frames.Add(SquareImage(frame));
        }
        images = frames;
        timeline = animated;
        return true;
    }

    private static Image SquareImage(ComposedRaster raster)
    {
        Raster square = raster.Image.Width == Side && raster.Image.Height == Side && raster.OriginX == 0 && raster.OriginY == 0
            ? raster.Image
            : raster.Image.Crop(raster.OriginX, raster.Image.Height - raster.OriginY - Side, Side, Side);
        return AppearanceTextures.ToImage(square);
    }

    // The plating of tg's floors; or a flat grey when the icons are missing. The shader dims it.
    private const long PlatingKey = -1000000000L;
    private Image PlatingImage()
    {
        if (_textures != null && _textures.Library.TryGetIcon("icons/turf/floors.dmi", out IIcon floors) && floors.TryGetState("plating", out IIconState plating))
            return AppearanceTextures.ToImage(plating.Get(0, 0));
        return AppearanceTextures.ToImage(Raster.Solid(Side, Side, 0x707078FF));
    }

    private static Image TintImage(string typePath, bool wall)
    {
        Rgb c = TurfStyle.Tint(typePath);
        uint Byte(float v) => (uint)Math.Clamp((int)Math.Round(v * 255f), 0, 255);
        uint rgba = (Byte(c.R) << 24) | (Byte(c.G) << 16) | (Byte(c.B) << 8) | 0xFF;
        return AppearanceTextures.ToImage(Raster.Solid(Side, Side, rgba));
    }

    /// <summary>
    /// Makes the Texture2DArrays and the materials once every tile has asked for its layer; called again after the map changed
    /// (a turf replaced after the join), it remakes only the pages that gained layers, and a page's material stays the same.
    /// </summary>
    public void Finish()
    {
        Shader shader = null;
        for (int i = 0; i < _pages.Count; i++)
        {
            if (i < _materials.Count && _uploaded[i] == _pages[i].Count)
                continue;
            var array = new Texture2DArray();
            var images = new Godot.Collections.Array<Image>();
            foreach (Image image in _pages[i])
                images.Add(image);
            if (images.Count == 0)
                images.Add(AppearanceTextures.ToImage(Raster.Solid(Side, Side, 0xFF00FFFF)));
            Error error = array.CreateFromImages(images);
            if (error != Error.Ok)
                GD.PushError($"[anomaly] tile kit: Texture2DArray page {i} failed: {error}");
            if (i == _materials.Count)
            {
                shader ??= GD.Load<Shader>("res://shaders/turf_kit.gdshader");
                var material = new ShaderMaterial { Shader = shader };
                foreach (KeyValuePair<string, Variant> parameter in _parameters)
                    material.SetShaderParameter(parameter.Key, parameter.Value);
                _materials.Add(material);
                _uploaded.Add(0);
            }
            _materials[i].SetShaderParameter("pictures", array);
            if (_glassMaterials.TryGetValue(i, out var glassMaterial)) glassMaterial.SetShaderParameter("pictures", array);
            _uploaded[i] = _pages[i].Count;
        }
    }

    /// <summary>The material of a page (after <see cref="Finish"/>).</summary>
    private readonly Dictionary<int, ShaderMaterial> _glassMaterials = new();
    public ShaderMaterial MaterialOf(int page, bool glass = false)
    {
        if (!glass) return _materials[page];
        if (!_glassMaterials.TryGetValue(page, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/turf_glass.gdshader") };
            foreach (var parameter in _parameters) material.SetShaderParameter(parameter.Key, parameter.Value);
            material.SetShaderParameter("pictures", _materials[page].GetShaderParameter("pictures"));
            _glassMaterials.Add(page, material);
        }
        return material;
    }

    /// <summary>Sets a uniform on every page's material (the ceiling switch).</summary>
    public void SetParameter(string name, Variant value)
    {
        _parameters[name] = value;
        foreach (ShaderMaterial material in _materials)
            material.SetShaderParameter(name, value);
        foreach (ShaderMaterial material in _glassMaterials.Values)
            material.SetShaderParameter(name, value);
    }
}
