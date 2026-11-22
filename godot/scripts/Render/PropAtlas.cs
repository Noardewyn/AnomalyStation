using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// The shared picture atlas of the static props (scope 2i): tg's composed pictures packed into one texture (<see cref="AtlasBuilder"/>)
/// with the metrics texture the prop shader reads, and the one material every chunk's props and pieces use. A picture is composed
/// once (by <see cref="AppearanceTextures"/>, cached) and stored once; the textures are uploaded by <see cref="Flush"/> when entries were added.
/// </summary>
public sealed class PropAtlas
{
    private static readonly byte[] FourDirs = { ByondDir.South, ByondDir.East, ByondDir.North, ByondDir.West };

    private readonly AtlasBuilder _builder = new();
    private readonly AppearanceTextures _textures;
    private readonly Dictionary<(uint Index, bool Private, int Frames, bool Trim), (int Base, int Frames)> _pictures = new();
    private readonly Dictionary<(uint Index, bool Private, byte Dir, bool Trim), int> _facing = new();
    private readonly Dictionary<uint, int> _swatches = new();
    private readonly Dictionary<(uint Index, bool Private, int Width, int Height), int> _frames = new();
    private readonly Dictionary<(uint Index, bool Private, int Facing, bool Trim, bool Once), AtlasAnimation> _animations = new();
    private readonly Dictionary<(uint Index, bool Private, byte Dir, int Width, int Height), AtlasAnimation> _modelAnimations = new();
    private Texture2DArray _atlasTexture;
    private ImageTexture _metricsTexture;
    private int _uploadedVersion = -1;
    private int _uploadedPages = -1, _uploadedMetricsRows = -1;

    public PropAtlas(AppearanceTextures textures)
    {
        _textures = textures;
        // the see-through glass pixels are blended in a pass of their own (prop_glass.gdshader), which takes every parameter this one does
        _glass = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/prop_glass.gdshader") };
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/prop_atlas.gdshader"), NextPass = _glass };
        // The pod door's slab drops by the wall's height plus a hair (D119: the one setting, WorldScale.WallHeight).
        Set("wall_height", WorldScale.WallHeight);
        // D121: the scale items are drawn at (the planner scaled their ground offsets by the same setting).
        Set("item_scale", WorldScale.ItemScale);
    }

    /// <summary>The material of every prop and piece MultiMesh.</summary>
    public ShaderMaterial Material { get; }

    private readonly ShaderMaterial _glass;

    private void Set(string name, Variant value)
    {
        Material.SetShaderParameter(name, value);
        _glass.SetShaderParameter(name, value);
    }


    /// <summary>Entries so far.</summary>
    public int EntryCount => _builder.Entries.Count;

    /// <summary>The atlas size in pixels.</summary>
    public Vector2I Size => new(AtlasBuilder.AtlasWidth, _builder.Height);

    /// <summary>
    /// The atlas ids of an appearance's pictures: <paramref name="frames"/> consecutive ids from <paramref name="baseId"/> (one for a
    /// still icon, four or eight by viewing direction in the shader's sector order). <paramref name="wantFrames"/> 1 composes the south
    /// picture only. <paramref name="trim"/> stores just the drawn pixels (a billboard); pieces keep the 32 x 32 square.
    /// </summary>
    public bool TryPicture(IAppearanceSource tables, TableRef appearance, bool wantFrames, bool trim, out int baseId, out int frames)
    {
        baseId = -1;
        frames = 1;
        if (appearance.IsNone)
            return false;
        int directions = wantFrames ? _textures.Directions(tables, appearance) : 1;
        int count = directions >= 8 ? 8 : directions >= 4 ? 4 : 1;
        var key = (appearance.Index, appearance.Private, count, trim);
        if (_pictures.TryGetValue(key, out var cached))
        {
            baseId = cached.Base;
            frames = cached.Frames;
            return baseId >= 0;
        }
        var rasters = new List<(Raster Image, int OriginX, int OriginY)>(count);
        for (int k = 0; k < count; k++)
        {
            byte dir = count == 1 ? ByondDir.South : count == 4 ? FourDirs[k] : ByondDir.FrontClockwise[k];
            if (!_textures.TryGet(tables, appearance, dir, 0, out ComposedRaster raster, out _))
                return false; // an icon is missing or still loading: try again later, nothing cached
            Raster image = raster.Image;
            int originX = raster.OriginX, originY = raster.OriginY;
            if (!trim)
            {
                int side = AppearanceCompositor.DefaultIconSize;
                image = image.Crop(originX, image.Height - originY - side, side, side);
                originX = 0;
                originY = 0;
            }
            rasters.Add((image, originX, originY));
        }
        int first = -1;
        foreach (var (image, ox, oy) in rasters)
        {
            int id = _builder.Add(image, ox, oy, trim);
            if (first < 0)
                first = id;
        }
        _pictures[key] = (first, count);
        baseId = first;
        frames = count;
        return true;
    }

    /// <summary>
    /// The atlas id of an appearance composed for ONE fixed direction (a floor hatch lies on the floor turned by its dir, not by where the viewer is): the picture of
    /// <paramref name="dir"/> (0: south). <paramref name="trim"/> stores just the drawn pixels.
    /// </summary>
    public bool TryPictureFacing(IAppearanceSource tables, TableRef appearance, byte dir, bool trim, out int id)
    {
        id = -1;
        if (appearance.IsNone)
            return false;
        if (dir == 0)
            dir = ByondDir.South;
        var key = (appearance.Index, appearance.Private, dir, trim);
        if (_facing.TryGetValue(key, out int cached))
        {
            id = cached;
            return id >= 0;
        }
        if (!_textures.TryGet(tables, appearance, dir, 0, out ComposedRaster raster, out _))
            return false; // an icon is missing or still loading: try again later, nothing cached
        Raster image = raster.Image;
        int originX = raster.OriginX, originY = raster.OriginY;
        if (!trim)
        {
            int side = AppearanceCompositor.DefaultIconSize;
            image = image.Crop(originX, image.Height - originY - side, side, side);
            originX = 0;
            originY = 0;
        }
        id = _builder.Add(image, originX, originY, trim);
        _facing[key] = id;
        return true;
    }

    /// <summary>
    /// D142: an appearance's icon state animation (or its <c>flick</c>, <paramref name="once"/>) in the atlas: the timeline and, per
    /// picture of it, the first atlas id of its direction frames (<paramref name="facing"/> -1: by viewing direction, as
    /// <see cref="TryPicture"/> with frames; else that one direction, as <see cref="TryPictureFacing"/>). True with a null
    /// <paramref name="animation"/> when nothing in it animates; false while an icon is missing or loading (try again later).
    /// </summary>
    public bool TryAnimation(IAppearanceSource tables, TableRef appearance, int facing, bool trim, bool once, out AtlasAnimation animation)
    {
        animation = null;
        if (appearance.IsNone)
            return false;
        var key = (appearance.Index, appearance.Private, facing, trim, once);
        if (_animations.TryGetValue(key, out animation))
            return true;
        byte[] dirs;
        if (facing >= 0)
            dirs = new[] { facing == 0 ? ByondDir.South : (byte)facing };
        else
        {
            int directions = _textures.Directions(tables, appearance);
            int count = directions >= 8 ? 8 : directions >= 4 ? 4 : 1;
            dirs = new byte[count];
            for (int k = 0; k < count; k++)
                dirs[k] = count == 1 ? ByondDir.South : count == 4 ? FourDirs[k] : ByondDir.FrontClockwise[k];
        }
        AppearanceCompositor compositor = _textures.Compositor;
        if (!compositor.TryCompose(tables, appearance, dirs[0], 0, out _))
            return false;
        AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, appearance, dirs[0]), once);
        if (timeline == null)
        {
            _animations[key] = null;
            return true;
        }
        var rasters = new List<(Raster Image, int OriginX, int OriginY)>(timeline.Count * dirs.Length);
        for (int i = 0; i < timeline.Count; i++)
            foreach (byte dir in dirs)
            {
                if (!compositor.TryCompose(tables, appearance, dir, timeline.SampleAt(i), out ComposedRaster raster))
                    return false;
                Raster image = raster.Image;
                int originX = raster.OriginX, originY = raster.OriginY;
                if (!trim)
                {
                    int side = AppearanceCompositor.DefaultIconSize;
                    image = image.Crop(originX, image.Height - originY - side, side, side);
                    originX = 0;
                    originY = 0;
                }
                rasters.Add((image, originX, originY));
            }
        var ids = new int[timeline.Count];
        for (int k = 0; k < rasters.Count; k++)
        {
            int id = _builder.Add(rasters[k].Image, rasters[k].OriginX, rasters[k].OriginY, trim);
            if (k % dirs.Length == 0)
                ids[k / dirs.Length] = id;
        }
        _animations[key] = animation = new AtlasAnimation(timeline, ids, dirs.Length);
        return true;
    }

    /// <summary>
    /// A model surface's animation in its authored frame dimensions, including 64 x 32 bar signs.
    /// False while an icon is missing or loading; a null animation means a still appearance.
    /// </summary>
    public bool TryModelAnimation(IAppearanceSource tables, TableRef appearance, byte dir, int width, int height, out AtlasAnimation animation)
    {
        animation = null;
        if (width == AppearanceCompositor.DefaultIconSize && height == AppearanceCompositor.DefaultIconSize)
            return TryAnimation(tables, appearance, dir, trim: false, once: false, out animation);
        var key = (appearance.Index, appearance.Private, dir, width, height);
        if (_modelAnimations.TryGetValue(key, out animation))
            return true;
        AppearanceCompositor compositor = _textures.Compositor;
        if (!compositor.TryCompose(tables, appearance, dir, 0, out _))
            return false;
        AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, appearance, dir), once: false);
        if (timeline == null)
        {
            _modelAnimations[key] = null;
            return true;
        }
        var ids = new int[timeline.Count];
        for (int i = 0; i < ids.Length; i++)
        {
            if (!compositor.TryCompose(tables, appearance, dir, timeline.SampleAt(i), out ComposedRaster raster))
                return false;
            Raster image = raster.Image.Crop(raster.OriginX, raster.Image.Height - raster.OriginY - height, width, height);
            ids[i] = _builder.Add(image, 0, 0, trim: false);
        }
        _modelAnimations[key] = animation = new AtlasAnimation(timeline, ids, 1);
        return true;
    }

    /// <summary>A still picture for a sliding door's opening/closing pose.</summary>
    public bool TryStill(IAppearanceSource tables, TableRef appearance, bool last, out int id)
    {
        id = -1;
        if (appearance.IsNone)
            return false;
        var key = (appearance.Index, appearance.Private, last ? -3 : -2, false);
        if (_pictures.TryGetValue(key, out var cached))
        {
            id = cached.Base;
            return true;
        }
        AppearanceCompositor compositor = _textures.Compositor;
        if (!compositor.TryCompose(tables, appearance, ByondDir.South, 0, out _))
            return false;
        double seconds = 0;
        if (last)
        {
            AnimationTimeline timeline = AnimationTimeline.Of(compositor.AnimatedStates(tables, appearance, ByondDir.South), once: true);
            seconds = timeline.SampleAt(timeline.Count - 1);
        }
        if (!compositor.TryCompose(tables, appearance, ByondDir.South, seconds, out ComposedRaster raster))
            return false;
        int side = AppearanceCompositor.DefaultIconSize;
        Raster image = raster.Image.Crop(raster.OriginX, raster.Image.Height - raster.OriginY - side, side, side);
        id = _builder.Add(image, 0, 0, false);
        _pictures[key] = (id, 1);
        return true;
    }

    /// <summary>
    /// M4v slice 2: the animation of a library icon state's south frames (the picture of <see cref="TryLibraryPicture"/>; a border
    /// firelock shows its full-tile counterpart's state, whose frames are tg's), the 32 x 32 square. Null when the state is still or missing.
    /// </summary>
    public AtlasAnimation LibraryAnimation(string icon, string state, bool trim)
    {
        if (icon == null || state == null || !_textures.Library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetShown(state, out IIconState iconState)
            || !iconState.IsAnimated)
            return null;
        var key = ((uint)(icon.GetHashCode() * 31 + state.GetHashCode()) ^ 0x5bd1e995u, true, -4, trim, false);
        if (_animations.TryGetValue(key, out AtlasAnimation known))
            return known;
        AnimationTimeline timeline = AnimationTimeline.Of(new[] { iconState }, once: false);
        AtlasAnimation animation = null;
        if (timeline != null)
        {
            var ids = new int[timeline.Count];
            for (int i = 0; i < timeline.Count; i++)
                ids[i] = _builder.Add(iconState.Get(0, iconState.FrameAt(timeline.SampleAt(i))), 0, 0, trim);
            animation = new AtlasAnimation(timeline, ids, 1);
        }
        _animations[key] = animation;
        return animation;
    }

    /// <summary>A picture's size in tg pixels (0 x 0 for no picture).</summary>
    public (int Width, int Height) PixelSizeOf(int id) => id >= 0 && id < _builder.Entries.Count ? (_builder.Entries[id].Width, _builder.Entries[id].Height) : (0, 0);

    /// <summary>The height in metres of the quad of atlas entry <paramref name="id"/> (a decal's drawn pixels at 2 m per 32 pixels); 0 for an unknown or empty entry.</summary>
    /// <summary>
    /// Whether a trimmed picture reaches the west (left) and east (right) edge of its 32-pixel frame, to half a pixel: a part of a picture tg splits over
    /// neighbouring tiles (a station map's halves) runs on over that edge.
    /// </summary>
    public (bool West, bool East) FrameEdgesOf(int id)
    {
        if (id < 0 || id >= _builder.Entries.Count || _builder.Entries[id].Width == 0)
            return (false, false);
        AtlasEntry e = _builder.Entries[id];
        float half = TileSpace.TileSize / 2f, slack = TileSpace.TileSize / 64f;
        return (e.CenterX - e.QuadWidth / 2f <= -half + slack, e.CenterX + e.QuadWidth / 2f >= half - slack);
    }

    public float QuadHeightOf(int id) => id >= 0 && id < _builder.Entries.Count ? _builder.Entries[id].QuadHeight : 0f;

    /// <summary>How high the middle of a trimmed picture's drawn pixels is over its frame's bottom (metres, its offsets in it).</summary>
    public float CentreHeightOf(int id) => id >= 0 && id < _builder.Entries.Count ? _builder.Entries[id].BottomY + _builder.Entries[id].QuadHeight / 2f : 0f;

    /// <summary>
    /// D142: a lit lamp's picture: the fixture's state (<paramref name="icon"/>, <paramref name="state"/>, south frame) with tg's glow sheet
    /// state (<paramref name="glowIcon"/>, <paramref name="glowState"/>: update_overlays' emissive_appearance) put over it, so exactly the
    /// pixels tg lights glow and the fitting around them stays; trimmed like <see cref="TryLibraryPicture"/>. False when either is missing.
    /// </summary>
    public bool TryLitLibraryPicture(string icon, string state, string glowIcon, string glowState, bool trim, out int id)
    {
        id = -1;
        if (icon == null || state == null || glowIcon == null || glowState == null
            || !_textures.Library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetShown(state, out IIconState iconState)
            || !_textures.Library.TryGetIcon(glowIcon, out IIcon glowSheet) || !glowSheet.TryGetShown(glowState, out IIconState glow))
            return false;
        uint key = (uint)((icon.GetHashCode() * 31 + state.GetHashCode()) * 31 + glowState.GetHashCode()) ^ 0x9E3779B9u;
        var picKey = (key, true, -1, trim);
        if (_pictures.TryGetValue(picKey, out var cached))
        {
            id = cached.Base;
            return true;
        }
        Raster lit = iconState.Get(0, 0).Clone();
        lit.Blend(glow.Get(0, 0), IconBlend.Overlay);
        id = _builder.Add(lit, 0, 0, trim);
        _pictures[picKey] = (id, 1);
        return true;
    }

    /// <summary>
    /// M5 slice 1 (D142): a library icon state's south frame (<paramref name="icon"/>, <paramref name="state"/>) with another state
    /// (<paramref name="overIcon"/>, <paramref name="overState"/>) drawn over it, as tg's overlay draws over its atom: a window's
    /// straight run with tg's crack overlay (WindowCracks). Trimmed like <see cref="TryLibraryPicture"/>. False when either is missing.
    /// </summary>
    public bool TryLibraryPictureWithOverlay(string icon, string state, string overIcon, string overState, bool trim, out int id)
    {
        id = -1;
        if (icon == null || state == null || overIcon == null || overState == null
            || !_textures.Library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetShown(state, out IIconState iconState)
            || !_textures.Library.TryGetIcon(overIcon, out IIcon overSheet) || !overSheet.TryGetShown(overState, out IIconState over))
            return false;
        uint key = (uint)(((icon.GetHashCode() * 31 + state.GetHashCode()) * 31 + overIcon.GetHashCode()) * 31 + overState.GetHashCode()) ^ 0x7F4A7C15u;
        var picKey = (key, true, -2, trim);
        if (_pictures.TryGetValue(picKey, out var cached))
        {
            id = cached.Base;
            return true;
        }
        Raster composed = iconState.Get(0, 0).Clone();
        composed.Blend(over.Get(0, 0), IconBlend.Overlay);
        id = _builder.Add(composed, 0, 0, trim);
        _pictures[picKey] = (id, 1);
        return true;
    }

    /// <summary>The atlas id of a picture made from a library icon state directly (no replica entry): the south frame, the 32 x 32 square.</summary>
    public bool TryLibraryPicture(string icon, string state, bool trim, out int id)
    {
        id = -1;
        if (icon == null || state == null || !_textures.Library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetShown(state, out IIconState iconState))
            return false;
        uint key = (uint)(icon.GetHashCode() * 31 + state.GetHashCode());
        var picKey = (key, false, -1, trim);
        if (_pictures.TryGetValue(picKey, out var cached))
        {
            id = cached.Base;
            return true;
        }
        id = _builder.Add(iconState.Get(0, 0), 0, 0, trim);
        _pictures[picKey] = (id, 1);
        return true;
    }

    /// <summary>
    /// D147: a library icon state's south frame laid over a flat colour (<c>0xRRGGBBAA</c>), so its transparent pixels show the colour; the
    /// whole 32 x 32 square, untrimmed (a picture repeated in squares, the window base's grating).
    /// </summary>
    public bool TryLibraryPictureOver(string icon, string state, uint background, out int id)
    {
        id = -1;
        if (icon == null || state == null || !_textures.Library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetShown(state, out IIconState iconState))
            return false;
        uint key = (uint)((icon.GetHashCode() * 31 + state.GetHashCode()) * 31 + background.GetHashCode());
        var picKey = (key, true, -1, false);
        if (_pictures.TryGetValue(picKey, out var cached))
        {
            id = cached.Base;
            return true;
        }
        Raster frame = iconState.Get(0, 0);
        var over = new Raster(frame.Width, frame.Height);
        byte br = (byte)(background >> 24), bg = (byte)(background >> 16), bb = (byte)(background >> 8);
        for (int i = 0; i < frame.Pixels.Length; i += 4)
        {
            int a = frame.Pixels[i + 3];
            over.Pixels[i] = (byte)((frame.Pixels[i] * a + br * (255 - a)) / 255);
            over.Pixels[i + 1] = (byte)((frame.Pixels[i + 1] * a + bg * (255 - a)) / 255);
            over.Pixels[i + 2] = (byte)((frame.Pixels[i + 2] * a + bb * (255 - a)) / 255);
            over.Pixels[i + 3] = 255;
        }
        id = _builder.Add(over, 0, 0, trim: false);
        _pictures[picKey] = (id, 1);
        return true;
    }

    /// <summary>The atlas id of a flat colour (<c>0xRRGGBBAA</c>).</summary>
    /// <summary>
    /// The atlas id of an appearance composed facing south and cut to its icon's frame of <paramref name="width"/> by <paramref name="height"/> pixels from the icon
    /// square's lower-left corner (a closet's 32 x 48 frame, which <see cref="TryPicture"/> would cut to the 32 x 32 square).
    /// </summary>
    public bool TryPictureFrame(IAppearanceSource tables, TableRef appearance, int width, int height, out int id)
    {
        id = -1;
        if (appearance.IsNone)
            return false;
        if (_frames.TryGetValue((appearance.Index, appearance.Private, width, height), out id))
            return id >= 0;
        if (!_textures.TryGet(tables, appearance, ByondDir.South, 0, out ComposedRaster raster, out _))
            return false;
        Raster image = raster.Image;
        int top = image.Height - raster.OriginY - height;
        if (raster.OriginX < 0 || top < 0 || raster.OriginX + width > image.Width)
            return false;
        id = _builder.Add(image.Crop(raster.OriginX, top, width, height), 0, 0, trim: false);
        _frames[(appearance.Index, appearance.Private, width, height)] = id;
        return true;
    }

    /// <summary>Stores a picture that is no tg icon (a 3D model's own texture, <see cref="ModelLibrary"/>): its whole raster, its id.</summary>
    public int AddImage(Raster image) => _builder.Add(image, 0, 0, trim: false);

    public int Swatch(uint rgba)
    {
        if (!_swatches.TryGetValue(rgba, out int id))
            _swatches[rgba] = id = _builder.AddSwatch(rgba);
        return id;
    }

    /// <summary>
    /// Uploads the atlas and the metrics when entries were added since the last call: only the pages that changed, as layers of a texture
    /// array. The array keeps <see cref="SparePages"/> blank layers beyond the pages, so a new page is one layer's upload; the array is made
    /// again (every page at once) only when they run out.
    /// </summary>
    public void Flush()
    {
        if (_uploadedVersion == _builder.Version)
            return;
        _uploadedVersion = _builder.Version;
        List<int> dirty = _builder.TakeDirtyPages();
        IReadOnlyList<Raster> pages = _builder.Pages;
        if (_atlasTexture == null || pages.Count > _uploadedPages)
        {
            var images = new Godot.Collections.Array<Image>();
            foreach (Raster page in pages)
                images.Add(PageImage(page));
            Image blank = Image.CreateEmpty(AtlasBuilder.AtlasWidth, AtlasBuilder.PageHeight, false, Image.Format.Rgba8);
            for (int i = 0; i < SparePages; i++)
                images.Add(blank);
            _atlasTexture ??= new Texture2DArray();
            _atlasTexture.CreateFromImages(images);
            _uploadedPages = pages.Count + SparePages;
            Set("atlas", _atlasTexture);
            Set("atlas_size", new Vector2(AtlasBuilder.AtlasWidth, AtlasBuilder.PageHeight));
        }
        else
        {
            foreach (int layer in dirty)
                _atlasTexture.UpdateLayer(PageImage(pages[layer]), layer);
        }

        float[] metrics = _builder.Metrics();
        int texels = Math.Max(2, _builder.Entries.Count * 2);
        int rows = (texels + 2047) / 2048;
        var padded = new float[rows * 2048 * 4];
        Array.Copy(metrics, padded, metrics.Length);
        var bytes = new byte[padded.Length * sizeof(float)];
        Buffer.BlockCopy(padded, 0, bytes, 0, bytes.Length);
        Image table = Image.CreateFromData(2048, rows, false, Image.Format.Rgbaf, bytes);
        if (_metricsTexture == null || _uploadedMetricsRows != rows)
        {
            _metricsTexture = ImageTexture.CreateFromImage(table);
            _uploadedMetricsRows = rows;
            Set("metrics", _metricsTexture);
        }
        else
            _metricsTexture.Update(table);
    }

    private const int SparePages = 2;

    private static Image PageImage(Raster page) => Image.CreateFromData(page.Width, page.Height, false, Image.Format.Rgba8, page.Pixels);
}

/// <summary>An animation in the prop atlas (<see cref="PropAtlas.TryAnimation"/>): its timeline and, per picture, the first of its <see cref="Directions"/> consecutive direction ids.</summary>
public sealed record AtlasAnimation(AnimationTimeline Timeline, int[] Ids, int Directions);
