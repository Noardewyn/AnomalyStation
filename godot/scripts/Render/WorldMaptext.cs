using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// Gaps W1 (protocol version 74): BYOND's maptext on the world's atoms (tg's status displays, the ticket machine's number, the countdowns). Each text box is
/// drawn once with the HUD's maptext renderer (<see cref="MaptextRenderer"/>: tg's map fonts, the markup, the box's clip) into a page, a small viewport
/// shared by every atom that shows the same text in a box of the same size; an atom shows it on a quad (<see cref="WorldTextQuad"/>,
/// <c>shaders/world_maptext.gdshader</c>) placed by <see cref="WorldMaptextPlacement"/> on its model's face or its billboard.
/// </summary>
public sealed partial class WorldMaptext : Node
{
    /// <summary>The page's texels per map pixel (the pixel fonts stay crisp with nearest filtering).</summary>
    public const int TexelsPerPixel = 2;

    private const int IdlePages = 32;

    private static Shader _shader;
    private readonly Dictionary<(string Text, int Width, int Height), Page> _pages = new();
    private readonly List<(string Text, int Width, int Height)> _idle = new();
    private readonly Dictionary<(string Icon, string State), MaskBounds?> _masks = new();
    private readonly IconLibrary _icons;

    private sealed class Page
    {
        public SubViewport Viewport;
        public int Users;
    }

    public WorldMaptext(IconLibrary icons)
    {
        _icons = icons;
        Name = "WorldMaptext";
    }

    /// <summary>The pages alive (in use or idle).</summary>
    public int PageCount => _pages.Count;

    /// <summary>The quads of <paramref name="items"/> that can show, each with its page; the caller places each quad's node and frees it with <see cref="Release"/>.</summary>
    public List<WorldTextQuad> Make(MaptextItem[] items, bool billboard)
    {
        var made = new List<WorldTextQuad>();
        foreach (MaptextItem item in items)
        {
            MaskBounds? mask = item.Mask is MaptextMask m ? MaskOf(m) : null;
            if (item.Mask != null && mask == null)
                continue; // a mask that is not there yet (or clear): BYOND would show nothing of it
            if (!WorldMaptextPlacement.TryRegion(item, mask, out WorldTextRegion region))
                continue;
            var key = (item.Text, region.BoxWidth, region.BoxHeight);
            _shader ??= GD.Load<Shader>("res://shaders/world_maptext.gdshader");
            var material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("text", Acquire(key));
            material.SetShaderParameter("region", new Vector4(region.BoxLeft, region.BoxTop, region.Width, region.Height));
            material.SetShaderParameter("box", new Vector2(region.BoxWidth, region.BoxHeight));
            material.SetShaderParameter("billboard", billboard);
            var quad = new QuadMesh();
            var node = new MeshInstance3D { Mesh = quad, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            made.Add(new WorldTextQuad(node, quad, material, item, region, key));
        }
        return made;
    }

    /// <summary>Frees the quads (their nodes and their pages' use).</summary>
    public void Release(List<WorldTextQuad> quads)
    {
        if (quads == null)
            return;
        foreach (WorldTextQuad q in quads)
        {
            q.Node.QueueFree();
            if (!_pages.TryGetValue(q.Key, out Page page) || --page.Users > 0)
                continue;
            _idle.Add(q.Key); // kept a while: a timer's text comes back, a display shows its line again
            while (_idle.Count > IdlePages)
            {
                var oldest = _idle[0];
                _idle.RemoveAt(0);
                if (_pages.TryGetValue(oldest, out Page gone) && gone.Users <= 0)
                {
                    _pages.Remove(oldest);
                    gone.Viewport.QueueFree();
                }
            }
        }
        quads.Clear();
    }

    private Texture2D Acquire((string Text, int Width, int Height) key)
    {
        if (!_pages.TryGetValue(key, out Page page))
        {
            using var made = Anomaly.Client.Dev.FrameProbe.Measure("maptext/page", key.Text);
            var viewport = new SubViewport
            {
                Size = new Vector2I(key.Width * TexelsPerPixel, key.Height * TexelsPerPixel),
                TransparentBg = true,
                Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
            };
            viewport.AddChild(new PageDrawer(key.Text, key.Width, key.Height));
            AddChild(viewport);
            _pages[key] = page = new Page { Viewport = viewport };
        }
        if (page.Users++ == 0)
            _idle.Remove(key);
        return page.Viewport.GetTexture();
    }

    /// <summary>The opaque bounds of an icon state's first frame (a model maps its base state's onto its own face), cached; null when it is not there.</summary>
    public MaskBounds? OpaqueBoundsOf(string icon, string state) => icon == null ? null : MaskOf(new MaptextMask(icon, state, 0f, 0f));

    private MaskBounds? MaskOf(MaptextMask mask)
    {
        var key = (mask.Icon, mask.State ?? string.Empty);
        if (_masks.TryGetValue(key, out MaskBounds? known))
            return known;
        MaskBounds? bounds = null;
        if (_icons != null && _icons.TryGetIcon(mask.Icon, out IIcon icon) && icon.TryGetShown(mask.State, out IIconState state))
            bounds = WorldMaptextPlacement.OpaqueBounds(state.Get(0, 0));
        _masks[key] = bounds;
        return bounds;
    }

    // One page's drawing: the text box at the page's top left, TexelsPerPixel texels a map pixel.
    private sealed partial class PageDrawer : Control
    {
        private readonly string _text;
        private readonly int _width, _height;
        private readonly MaptextBoxes _boxes = new();

        public PageDrawer(string text, int width, int height)
        {
            _text = text;
            _width = width;
            _height = height;
            TextureFilter = TextureFilterEnum.Nearest;
            Size = new Vector2(width * TexelsPerPixel, height * TexelsPerPixel);
        }

        public override void _Draw()
        {
            _boxes.Begin(GetCanvasItem());
            MaptextRenderer.Shared.DrawBox(_boxes, _text, _width, _height, TexelsPerPixel);
            _boxes.End();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
                _boxes.Free();
        }
    }
}

/// <summary>One world maptext's quad (<see cref="WorldMaptext.Make"/>): its node, which its owner places, and the text's shift while it scrolls.</summary>
public sealed class WorldTextQuad
{
    private float _shift;

    internal WorldTextQuad(MeshInstance3D node, QuadMesh quad, ShaderMaterial material, MaptextItem item, WorldTextRegion region, (string, int, int) key)
    {
        Node = node;
        Quad = quad;
        Material = material;
        Item = item;
        Region = region;
        Key = key;
    }

    public MeshInstance3D Node { get; }

    public QuadMesh Quad { get; }

    public ShaderMaterial Material { get; }

    public MaptextItem Item { get; }

    public WorldTextRegion Region { get; }

    internal (string Text, int Width, int Height) Key { get; }

    /// <summary>
    /// Shows the text at its maptext_x as <paramref name="animation"/> plays it at <paramref name="renderTick"/> (its entry's chains,
    /// <see cref="AnimationTrack.Child"/>); false when none plays (it rests where its appearance has it).
    /// </summary>
    public bool Step(EntityAnimation animation, double renderTick)
    {
        bool playing = AnimationPlayback.TryMaptextX(animation, Item.Source, renderTick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, Item.MaptextX, out double x);
        float shift = playing ? (float)(x - Item.MaptextX) : 0f;
        if (shift != _shift)
        {
            _shift = shift;
            Material.SetShaderParameter("shift", shift);
        }
        return playing;
    }
}
