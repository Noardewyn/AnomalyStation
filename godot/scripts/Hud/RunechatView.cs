using Godot;
using Anomaly.Client.Hud.Maptext;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Maptext;

namespace Anomaly.Client.Hud;

/// <summary>
/// M9 slice A, DEVIATION(DV160): tg's runechat drawn by the client over the speakers in 3D. Each line the server sends (protocol version 38's
/// Runechat: tg's finished maptext) is measured at tg's <c>CHAT_MESSAGE_WIDTH</c> with the maptext fonts, stacked and faded by
/// <see cref="RunechatModel"/> (tg's finish_image_generation), and drawn on the screen over the speaker's picture: the line's box is 112 map pixels
/// wide, centred on the speaker, its bottom <c>pixel_z</c> over the picture's top. A map pixel is <see cref="MapPixelMetres"/> at the speaker, so the
/// lines shrink with distance, but never under <see cref="MinScale"/> screen pixels (nor over <see cref="MaxScale"/>) times the HUD's text scale, so they stay readable. The radio
/// and emote icons tg prefixes (chat_icons.dmi) are drawn in front of the first line.
/// </summary>
public sealed partial class RunechatView : Control
{
    /// <summary>The size of a map pixel at the speaker, metres (a line box 112 pixels wide is 1.1 m).</summary>
    public const float MapPixelMetres = 0.01f;

    /// <summary>The smallest and largest screen pixels per map pixel, before the HUD's text scale (the chat lines' own).</summary>
    public const float MinScale = 1.5f, MaxScale = 3f;

    private readonly RunechatModel _model = new();
    private readonly Dictionary<string, (List<PlacedText> Texts, float Height)> _layouts = new();
    private readonly List<Rid> _canvases = new();
    private readonly Dictionary<string, Texture2D> _icons = new();
    private MaptextRenderer _renderer;
    private IconLibrary _library;

    /// <summary>The HUD's text scale (the chat lines', <see cref="ChatLines.TextScale"/>).</summary>
    public float HudScale { get; set; } = 1f;

    public override void _Ready()
    {
        Name = "Runechat";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void Bind(MaptextRenderer renderer, IconLibrary library)
    {
        _renderer = renderer;
        _library = library;
    }

    /// <summary>A line from the server: measured (tg's MeasureText) and stacked over its speaker.</summary>
    public void Add(long target, string maptext, int lifespan, double now)
    {
        if (_renderer == null || string.IsNullOrEmpty(maptext))
            return;
        (List<PlacedText> _, float height) = Layout(maptext);
        _model.Add(target, maptext, lifespan, height, now);
    }

    public void Clear() => _model.Clear();

    // The line laid out in its 112-pixel box, and its height in map pixels.
    private (List<PlacedText> Texts, float Height) Layout(string maptext)
    {
        if (_layouts.TryGetValue(maptext, out var cached))
            return cached;
        MaptextDocument doc = MaptextMarkup.Parse(maptext);
        MaptextLayoutResult layout = MaptextLayout.Place(doc, _renderer.Fonts, RunechatModel.Width, 4096);
        var result = (layout.Items, Math.Max(1f, layout.Height));
        if (_layouts.Count > 512)
            _layouts.Clear();
        _layouts[maptext] = result;
        return result;
    }

    /// <summary>Per frame: the lines placed over their speakers as <paramref name="camera"/> sees them.</summary>
    public void Update(Camera3D camera, EntityLayer entities, double now)
    {
        _model.Prune(now);
        int used = 0;
        if (camera != null && entities != null && _renderer != null)
        {
            foreach ((long target, List<RunechatLine> lines) in _model.Seen)
            {
                if (!entities.TryGetChatPoint(target, out Vector3 point, out bool shown) || !shown || camera.IsPositionBehind(point))
                    continue;
                Vector2 anchor = camera.UnprojectPosition(point);
                float pixelsPerMetre = camera.UnprojectPosition(point + camera.GlobalBasis.Y).DistanceTo(anchor);
                float scale = Math.Clamp(pixelsPerMetre * MapPixelMetres, MinScale, MaxScale) * HudScale;
                foreach (RunechatLine line in lines)
                {
                    float alpha = line.Alpha(now);
                    if (alpha <= 0.003f)
                        continue;
                    DrawLine(Canvas(used++), line, anchor, scale, alpha, now);
                }
            }
        }
        for (int i = used; i < _canvases.Count; i++)
            RenderingServer.CanvasItemClear(_canvases[i]);
    }

    private Rid Canvas(int index)
    {
        while (_canvases.Count <= index)
        {
            Rid rid = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(rid, GetCanvasItem());
            _canvases.Add(rid);
        }
        return _canvases[index];
    }

    private void DrawLine(Rid canvas, RunechatLine line, Vector2 anchor, float scale, float alpha, double now)
    {
        RenderingServer.CanvasItemClear(canvas);
        (List<PlacedText> texts, float height) = Layout(line.Maptext);
        // The image's maptext box: 112 wide, centred on the speaker; maptext_height = mheight * 1.25 with its bottom pixel_z over the
        // speaker's bottom, which is StartingHeight under the picture's top (the anchor); the text hangs from the box's top.
        float boxHeight = height * 1.25f;
        float bottom = anchor.Y - (line.PixelZ(now) - RunechatModel.StartingHeight) * scale;
        var topLeft = new Vector2(Mathf.Round(anchor.X - RunechatModel.Width / 2f * scale), Mathf.Round(bottom - boxHeight * scale));
        RenderingServer.CanvasItemSetTransform(canvas, new Transform2D(0f, topLeft));
        RenderingServer.CanvasItemSetModulate(canvas, new Color(1, 1, 1, alpha));
        RenderingServer.CanvasItemSetDrawIndex(canvas, (int)(line.Order & 0x7FFFFFFF));
        foreach (PlacedText placed in texts)
            _renderer.DrawText(canvas, placed, scale);
        if (texts.Count > 0 && Icon(RunechatModel.IconStateOf(line.Maptext)) is Texture2D icon)
        {
            PlacedText first = texts[0];
            float size = RunechatModel.IconSize * scale;
            var at = new Vector2(first.X * scale - size - scale, first.Baseline * scale - size);
            RenderingServer.CanvasItemAddTextureRect(canvas, new Rect2(at, new Vector2(size, size)), icon.GetRid());
        }
    }

    private Texture2D Icon(string state)
    {
        if (state == null || _library == null)
            return null;
        if (_icons.TryGetValue(state, out Texture2D texture))
            return texture;
        texture = null;
        if (_library.GetSheet("icons/ui/chat/chat_icons.dmi") is IconSheet sheet && sheet.TryGetState(state, out IIconState iconState))
            texture = ImageTexture.CreateFromImage(Render.AppearanceTextures.ToImage(iconState.Get(0, 0)));
        _icons[state] = texture;
        return texture;
    }

    public override void _ExitTree()
    {
        foreach (Rid rid in _canvases)
            RenderingServer.FreeRid(rid);
        _canvases.Clear();
    }
}
