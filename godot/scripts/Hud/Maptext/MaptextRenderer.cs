using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud.Maptext;

/// <summary>
/// HUD maptext (protocol version 18, D142): draws the maptexts of an appearance tree on a canvas item. The tree's texts come from
/// <see cref="MaptextCollector"/>, each is parsed once (<see cref="MaptextMarkup"/>) and laid out in its box (<see cref="MaptextLayout"/>) with the
/// loaded fonts, and every line is drawn at the HUD's pixel size, outlined as tg's <c>-dm-text-outline</c> asks. The box's lower left sits at the
/// icon's lower left plus maptext_x and maptext_y; the text starts at the box's top. World maptext (runechat, floating text over atoms) is M9.
/// </summary>
public sealed class MaptextRenderer
{
    private readonly MaptextFonts _fonts;
    private readonly MaptextCollector _collector = new();
    private readonly Dictionary<(string Text, int Width, int Height), Placed> _layouts = new();
    private ReplicaWorld _replica;

    public MaptextRenderer(MaptextFonts fonts = null) => _fonts = fonts ?? new MaptextFonts();

    private static MaptextRenderer _shared;

    /// <summary>The client's one renderer (the fonts are read once; the HUD layer and the container windows share it).</summary>
    public static MaptextRenderer Shared => _shared ??= new MaptextRenderer();

    /// <summary>The fonts.</summary>
    public MaptextFonts Fonts => _fonts;

    /// <summary>Whether the appearance has any maptext.</summary>
    public bool Has(AppearanceTextures textures, ReplicaWorld replica, TableRef appearance)
        => Items(textures, replica, appearance).Length > 0;

    /// <summary>The maptexts of an appearance (the caches start afresh for a new replica).</summary>
    public MaptextItem[] Items(AppearanceTextures textures, ReplicaWorld replica, TableRef appearance)
    {
        if (!ReferenceEquals(replica, _replica))
        {
            _replica = replica;
            _collector.Clear();
        }

        return _collector.Collect(textures.TablesOf(replica), appearance, rootOffset: true);
    }

    /// <summary>
    /// Draws the maptexts of <paramref name="appearance"/> on <paramref name="target"/> (call inside its <c>_Draw</c>). <paramref name="iconLowerLeft"/> is
    /// the root icon's lower left corner in the target's coordinates and <paramref name="scale"/> the size of a map pixel in the target's. Each
    /// text is drawn in a clip of its box, as BYOND draws it (<see cref="MaptextClip"/>): <paramref name="boxes"/> holds the clipping canvas items
    /// (the caller's, begun and ended around the draw).
    /// </summary>
    public void Draw(CanvasItem target, AppearanceTextures textures, ReplicaWorld replica, TableRef appearance, Vector2 iconLowerLeft, float scale, MaptextBoxes boxes)
    {
        foreach (MaptextItem item in Items(textures, replica, appearance))
            DrawItem(boxes, item, iconLowerLeft, scale);
    }

    /// <summary>Gaps W1 (protocol version 74): draws one text box with its top left at the target's origin, <paramref name="scale"/> target pixels a map pixel (a world text's page).</summary>
    public void DrawBox(MaptextBoxes boxes, string text, int width, int height, float scale)
        => DrawItem(boxes, new MaptextItem(text, width, height, 0f, 0f), new Vector2(0f, MaptextClip.BoxHeight(height) * scale), scale);

    private void DrawItem(MaptextBoxes boxes, MaptextItem item, Vector2 iconLowerLeft, float scale)
    {
        int boxWidth = MaptextClip.BoxWidth(item.Width), boxHeight = MaptextClip.BoxHeight(item.Height);
        if (!_layouts.TryGetValue((item.Text, boxWidth, boxHeight), out Placed placedText))
        {
            MaptextDocument doc = MaptextMarkup.Parse(item.Text);
            MaptextLayoutResult layout = MaptextLayout.Place(doc, _fonts, boxWidth, boxHeight);
            placedText = new Placed(MaptextClip.Visible(layout, _fonts, boxWidth, boxHeight));
            if (_layouts.Count > 2048) _layouts.Clear();
            _layouts[(item.Text, boxWidth, boxHeight)] = placedText;
        }

        if (placedText.Texts.Count == 0)
            return;
        // The box's top left: its bottom is Y above the icon's bottom, it is Height tall. The text is drawn relative to it, clipped to it.
        var boxTopLeft = new Vector2(Mathf.Round(iconLowerLeft.X + item.X * scale), Mathf.Round(iconLowerLeft.Y - (item.Y + boxHeight) * scale));
        Rid canvas = boxes.Box(new Rect2(boxTopLeft, new Vector2(boxWidth * scale, boxHeight * scale)));
        foreach (ClippedText clipped in placedText.Texts)
            DrawText(canvas, clipped.Text, scale);
    }

    /// <summary>Draws one placed text on a canvas item whose origin is the text's box's top left.</summary>
    public void DrawText(Rid canvas, in PlacedText placed, float scale)
    {
        MaptextStyle style = placed.Style;
        Font font = _fonts.Get(style.Font, style.Bold, style.Italic);
        int size = MaptextFonts.ScreenSize(style.SizePx, scale);
        var pos = new Vector2(Mathf.Round(placed.X * scale), Mathf.Round(placed.Baseline * scale));
        Color color = ToColor(style.Rgba);
        if (style.OutlinePx > 0)
            font.DrawStringOutline(canvas, pos, placed.Text, HorizontalAlignment.Left, -1, size, Math.Max(1, (int)MathF.Round(style.OutlinePx * scale)), ToColor(style.OutlineRgba));
        font.DrawString(canvas, pos, placed.Text, HorizontalAlignment.Left, -1, size, color);
        float width = placed.Width * scale;
        float thick = Mathf.Max(1f, scale);
        if (style.Underline)
            RenderingServer.CanvasItemAddLine(canvas, pos + new Vector2(0, thick), pos + new Vector2(width, thick), color, thick);
        if (style.Strike)
            RenderingServer.CanvasItemAddLine(canvas, pos - new Vector2(0, size * 0.3f), pos + new Vector2(width, -size * 0.3f), color, thick);
    }

    private sealed record Placed(List<ClippedText> Texts);

    private static Color ToColor(uint rgba)
        => new(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);
}

/// <summary>
/// The clipping canvas items of one drawer (BYOND clips maptext to its box, <see cref="MaptextClip"/>): one child canvas item of the drawer's per
/// drawn box, with the box as its clip rect, reused draw over draw. <see cref="Begin"/> before drawing, <see cref="End"/> after, <see cref="Free"/>
/// when the drawer goes.
/// </summary>
public sealed class MaptextBoxes
{
    private readonly List<Rid> _items = new();
    private Rid _parent;
    private int _used;

    /// <summary>The boxes drawn in the last draw.</summary>
    public int Used => _used;

    /// <summary>Starts a draw under <paramref name="parent"/> (the drawer's canvas item).</summary>
    public void Begin(Rid parent)
    {
        _parent = parent;
        _used = 0;
    }

    /// <summary>A cleared canvas item clipped to <paramref name="box"/> (in the parent's coordinates), whose origin is the box's top left.</summary>
    public Rid Box(Rect2 box)
    {
        if (_used == _items.Count)
        {
            Rid made = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(made, _parent);
            RenderingServer.CanvasItemSetClip(made, true);
            _items.Add(made);
        }

        Rid item = _items[_used++];
        RenderingServer.CanvasItemSetParent(item, _parent);
        RenderingServer.CanvasItemClear(item);
        RenderingServer.CanvasItemSetVisible(item, true);
        RenderingServer.CanvasItemSetTransform(item, new Transform2D(0f, box.Position));
        RenderingServer.CanvasItemSetCustomRect(item, true, new Rect2(Vector2.Zero, box.Size));
        RenderingServer.CanvasItemSetDefaultTextureFilter(item, RenderingServer.CanvasItemTextureFilter.Nearest);
        return item;
    }

    /// <summary>Hides the boxes the last draw did not use.</summary>
    public void End()
    {
        for (int i = _used; i < _items.Count; i++)
        {
            RenderingServer.CanvasItemClear(_items[i]);
            RenderingServer.CanvasItemSetVisible(_items[i], false);
        }
    }

    /// <summary>Frees the canvas items.</summary>
    public void Free()
    {
        foreach (Rid item in _items)
            RenderingServer.FreeRid(item);
        _items.Clear();
        _used = 0;
    }
}

/// <summary>A control that draws the maptexts of one HUD entry's appearance (a child of the entry's container; it does not take the mouse).</summary>
public partial class MaptextNode : Control
{
    private MaptextRenderer _renderer;
    private AppearanceTextures _textures;
    private ReplicaWorld _replica;
    private TableRef _appearance;
    private Vector2 _lowerLeft;
    private float _scale;
    private bool _dirty = true;
    private readonly MaptextBoxes _boxes = new();

    public MaptextNode()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    /// <summary>The texts drawn now.</summary>
    public int ItemCount { get; private set; }

    /// <summary>Sets what to draw and where; redraws only when something changed.</summary>
    public void SetText(MaptextRenderer renderer, AppearanceTextures textures, ReplicaWorld replica, TableRef appearance, Vector2 iconLowerLeft, float scale)
    {
        if (_dirty == false && _appearance == appearance && _lowerLeft == iconLowerLeft && Mathf.IsEqualApprox(_scale, scale) && ReferenceEquals(_replica, replica) && Visible)
            return;
        _renderer = renderer;
        _textures = textures;
        _replica = replica;
        _appearance = appearance;
        _lowerLeft = iconLowerLeft;
        _scale = scale;
        _dirty = false;
        ItemCount = renderer.Items(textures, replica, appearance).Length;
        Visible = ItemCount > 0;
        QueueRedraw();
    }

    public override void _Draw()
    {
        _boxes.Begin(GetCanvasItem());
        if (_renderer != null && Visible)
            _renderer.Draw(this, _textures, _replica, _appearance, _lowerLeft, _scale, _boxes);
        _boxes.End();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
            _boxes.Free();
    }
}
