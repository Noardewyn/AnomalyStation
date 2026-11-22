using System.IO;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Maptext;

namespace Anomaly.Client.Hud.Maptext;

/// <summary>
/// HUD maptext (protocol version 18, D142): tg's map fonts (interface/fonts: Grand9K Pixel, Pixellari, TinyUnicode, Spess Font, VCR OSD
/// Mono), read as plain files from <c>fonts/</c> in the converted-asset folder (<see cref="IconPaths.Root"/>; tools/Anomaly.Tools.Icons copies them
/// from tg's interface/fonts, as it converts the icons, and export-windows.sh ships them with the icons). They are pixel fonts, drawn without
/// smoothing at sizes that are multiples of their grid so they stay crisp. A font that is not there falls back to the engine's default font.
/// Also the layout's measurements (<see cref="IMaptextMetrics"/>) at scale 1.
/// </summary>
public sealed class MaptextFonts : IMaptextMetrics
{
    private static readonly string[] Files = { "Grand9K_Pixel.ttf", "Pixellari.ttf", "TinyUnicode.ttf", "SpessFont.ttf", "VCR_OSD_Mono.ttf" };

    private readonly Font[] _faces = new Font[Files.Length + 1]; // the files, then protocol 19's sans (MaptextFont.Sans)

    // Protocol version 19: tg's tooltip page asks for "bold 12px Arial, \"Helvetica Neue\", Helvetica, sans-serif" (tooltip.html).
    private static readonly string[] SansNames = { "Arial", "Helvetica Neue", "Helvetica", "Liberation Sans", "DejaVu Sans", "sans-serif" };
    private readonly Dictionary<(MaptextFont, bool, bool), Font> _variants = new();
    private readonly string _root;

    public MaptextFonts(string folder = null)
    {
        _root = folder ?? Path.Combine(IconPaths.Root, "fonts");
    }

    /// <summary>The number of fonts found on disk (the rest use the engine's fallback).</summary>
    public int Loaded { get; private set; }

    /// <summary>The font of a style (a bold or italic variant is made from the face).</summary>
    public Font Get(MaptextFont font, bool bold, bool italic)
    {
        var key = (font, bold, italic);
        if (_variants.TryGetValue(key, out Font known)) return known;
        if (font == MaptextFont.Sans && bold)
        {
            // A system font has its own bold face (the browser's), not an emboldened regular one.
            Font boldFace = new SystemFont { FontNames = SansNames, FontWeight = 700, FontItalic = italic, Antialiasing = TextServer.FontAntialiasing.Gray };
            _variants[key] = boldFace;
            return boldFace;
        }

        Font face = Face(font);
        if (bold || italic)
        {
            var variation = new FontVariation { BaseFont = face };
            if (bold) variation.VariationEmbolden = 0.6f;
            if (italic) variation.VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-0.2f, 1f), Vector2.Zero);
            face = variation;
        }

        _variants[key] = face;
        return face;
    }

    private Font Face(MaptextFont font)
    {
        int index = (int)font;
        if (_faces[index] != null) return _faces[index];
        if (font == MaptextFont.Sans)
            return _faces[index] = new SystemFont { FontNames = SansNames, Antialiasing = TextServer.FontAntialiasing.Gray };
        string path = Path.Combine(_root, Files[index]);
        Font face = null;
        try
        {
            if (File.Exists(path))
            {
                var file = new FontFile
                {
                    Data = File.ReadAllBytes(path),
                    Antialiasing = TextServer.FontAntialiasing.None,
                    SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
                    Hinting = TextServer.Hinting.None,
                    GenerateMipmaps = false,
                };
                face = file;
                Loaded++;
            }
        }
        catch (Exception e)
        {
            GD.PushWarning("[anomaly] maptext font " + path + ": " + e.Message);
        }

        _faces[index] = face ?? ThemeDB.FallbackFont;
        return _faces[index];
    }

    /// <summary>The size an engine font is drawn at for <paramref name="sizePx"/> map pixels at <paramref name="scale"/> screen pixels each (whole pixels, never below 1).</summary>
    public static int ScreenSize(float sizePx, float scale) => Math.Max(1, (int)MathF.Round(sizePx * scale));

    /// <inheritdoc />
    public float Width(in MaptextStyle style, string text)
        => Get(style.Font, style.Bold, style.Italic).GetStringSize(text, HorizontalAlignment.Left, -1, ScreenSize(style.SizePx, 1f)).X;

    /// <inheritdoc />
    public float LineHeight(in MaptextStyle style)
        => Get(style.Font, style.Bold, style.Italic).GetHeight(ScreenSize(style.SizePx, 1f));

    /// <inheritdoc />
    public float Ascent(in MaptextStyle style)
        => Get(style.Font, style.Bold, style.Italic).GetAscent(ScreenSize(style.SizePx, 1f));
}
