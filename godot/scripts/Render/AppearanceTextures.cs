using Godot;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>What the client keeps of one composed picture: the engine texture and the materials that draw it.</summary>
public sealed class GodotPicture
{
    /// <summary>The texture (nearest filtering needs no mipmaps).</summary>
    public ImageTexture Texture;

    /// <summary>A see-through picture (<see cref="Raster.IsTranslucent"/>): drawn blended by its alpha, not cut by the scissor.</summary>
    public bool Translucent;

    private ImageTexture _mipmapped;
    private ShaderMaterial _standing;

    /// <summary>The same picture with mipmaps (floors and walls seen at a slant shimmer without them).</summary>
    public ImageTexture Mipmapped(Raster image)
    {
        if (_mipmapped == null)
        {
            Image mip = AppearanceTextures.ToImage(image);
            mip.GenerateMipmaps();
            _mipmapped = ImageTexture.CreateFromImage(mip);
        }
        return _mipmapped;
    }

    private static Shader _shader;
    private static Shader _translucentShader;

    /// <summary>The blended billboard (<c>shaders/picture_billboard_translucent.gdshader</c>): a see-through picture's material at rest and while it animates.</summary>
    public static Shader TranslucentShader => _translucentShader ??= GD.Load<Shader>("res://shaders/picture_billboard_translucent.gdshader");

    /// <summary>
    /// A Y-axis billboard material: nearest filtering, alpha scissor, and the light grid's term like every other world surface
    /// (M3 I1c; <c>shaders/picture_billboard.gdshader</c>), plus the shadowed Godot lights on top (D104).
    /// </summary>
    public ShaderMaterial Standing => _standing ??= Make(Texture, Translucent);

    private static ShaderMaterial Make(ImageTexture texture, bool translucent)
    {
        _shader ??= GD.Load<Shader>("res://shaders/picture_billboard.gdshader");
        var material = new ShaderMaterial { Shader = translucent ? TranslucentShader : _shader };
        material.SetShaderParameter("picture", texture);
        return material;
    }
}

/// <summary>
/// The client's <see cref="IAppearanceTextures"/> (M2 P7, D104): the icon library over the converted tg icons, the appearance compositor
/// and the engine textures made from its pictures (cached with them, so a picture is uploaded once). Main thread only. When the converted
/// icons are missing every composition fails (the renderers draw their placeholders) and the problem is logged once.
/// </summary>
public sealed class AppearanceTextures : IAppearanceTextures
{
    private readonly AppearanceCompositor _compositor;
    private ReplicaWorld _lastReplica;
    private ReplicaTables _tables;
    private int _lastSnapshots;

    public AppearanceTextures(string assetsRoot = null)
    {
        var library = new IconLibrary(AssetPreload.IconFiles(assetsRoot ?? IconPaths.Root));
        library.ProblemFound += problem => GD.PushWarning("[anomaly] icons: " + problem);
        _compositor = new AppearanceCompositor(library);
    }

    /// <summary>The compositor (engine-free).</summary>
    public AppearanceCompositor Compositor => _compositor;

    /// <summary>The icon library.</summary>
    public IconLibrary Library => _compositor.Library;

    /// <summary>The tables of <paramref name="replica"/>; a new replica (a reconnect) starts the caches afresh.</summary>
    public ReplicaTables TablesOf(ReplicaWorld replica)
    {
        // a Snapshot (the player's mob changed) restarts the private tables at index 1: the pictures cached by index are stale
        if (!ReferenceEquals(replica, _lastReplica) || replica.SnapshotsApplied != _lastSnapshots)
        {
            _compositor.Clear();
            _lastReplica = replica;
            _lastSnapshots = replica.SnapshotsApplied;
            _tables = new ReplicaTables(replica);
        }
        return _tables;
    }

    public bool TryCompose(ReplicaWorld replica, TableRef appearance, byte dir, double seconds, out ComposedAppearance composed)
    {
        composed = default;
        // The screen objects of the HUD have no 3D placement, so their own pixel offset stays in the picture (M3 R2).
        if (!TryGet(TablesOf(replica), appearance, dir, seconds, out ComposedRaster raster, out GodotPicture picture, rootOffset: true))
            return false;
        composed = new ComposedAppearance(picture.Texture, raster.Image.Width, raster.Image.Height, raster.OriginX, raster.OriginY);
        return true;
    }

    /// <summary>
    /// Composes <paramref name="appearance"/> of any table source (the showcase uses a hand-made one) and returns the picture with its engine objects.
    /// The picture of a thing in the 3D world carries no pixel_x and pixel_y of the root appearance (M3 R2: the placement applies them);
    /// <paramref name="rootOffset"/> keeps them in the picture (the HUD).
    /// </summary>
    /// <paramref name="filterLooks"/>: version 26, the filters of animated entries as their filter animations show them now (FilterPlayback).
    public bool TryGet(IAppearanceSource tables, TableRef appearance, byte dir, double seconds, out ComposedRaster raster, out GodotPicture picture, bool rootOffset = false, bool rootOpaque = false,
        IReadOnlyDictionary<AppearanceEntry, IReadOnlyList<FilterEntry>> filterLooks = null)
    {
        picture = null;
        if (!_compositor.TryCompose(tables, appearance, dir, seconds, out raster, rootOffset, rootOpaque, filterLooks))
            return false;
        picture = raster.Tag as GodotPicture;
        if (picture == null)
        {
            picture = new GodotPicture { Texture = ImageTexture.CreateFromImage(ToImage(raster.Image)), Translucent = raster.Image.IsTranslucent() };
            raster.Tag = picture;
        }
        return true;
    }

    /// <summary>How many directions the appearance's icons have (1, 4 or 8).</summary>
    public int Directions(IAppearanceSource tables, TableRef appearance) => _compositor.Directions(tables, appearance);

    public void Clear() => _compositor.Clear();

    /// <summary>A mipmapped texture of a raster (a floor or wall picture seen at a slant).</summary>
    public static ImageTexture MipmappedTexture(Raster raster)
    {
        Image image = ToImage(raster);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>The engine image of a raster (RGBA8).</summary>
    public static Image ToImage(Raster raster) => Image.CreateFromData(raster.Width, raster.Height, false, Image.Format.Rgba8, raster.Pixels);
}
