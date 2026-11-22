using System;
using System.IO;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud.Tgui;

/// <summary>
/// M9 slice C (the owner's playtest, 2026-10-23): tgui's icon font, tg's Font Awesome Free (html/font-awesome), read as plain files from
/// <c>fonts/font-awesome/</c> in the converted-asset folder (tools/Anomaly.Tools.Icons copies them). Its solid face becomes a fallback of the
/// engine's default font, so an icon's character (<see cref="Glyph"/>, a private-use code point) draws in any label or button beside its text.
/// </summary>
public static class FontAwesomeFont
{
    private static Dictionary<string, string> _names;
    private static bool _tried;

    /// <summary>The character of a Font Awesome icon name, or null (unknown, or the font is not there).</summary>
    public static string Glyph(string name)
    {
        Install();
        // tgui-core's Icon prepends "fa-" only to a name without it (tg's FA_ICON_* defines carry it: the species' perks, gaps SP1)
        if (name != null && name.StartsWith("fa-", StringComparison.Ordinal))
            name = name[3..];
        return _names != null && _names.TryGetValue(name, out string glyph) ? glyph : null;
    }

    /// <summary>Loads the face and its names once and adds the face to the default font's fallbacks.</summary>
    public static void Install()
    {
        if (_tried)
            return;
        _tried = true;
        string root = Path.Combine(IconPaths.Root, "fonts", "font-awesome");
        string css = Path.Combine(root, "all.min.css"), ttf = Path.Combine(root, "fa-solid-900.ttf");
        try
        {
            if (!File.Exists(css) || !File.Exists(ttf))
            {
                GD.PushWarning("[anomaly] tgui icons: no Font Awesome in " + root + " (run tools/Anomaly.Tools.Icons): symbols stand in");
                return;
            }
            var face = new FontFile { Data = File.ReadAllBytes(ttf), Antialiasing = TextServer.FontAntialiasing.Gray };
            Font main = ThemeDB.FallbackFont;
            Godot.Collections.Array<Font> fallbacks = main.Fallbacks;
            fallbacks.Add(face);
            main.Fallbacks = fallbacks;
            _names = FontAwesomeNames.Parse(File.ReadAllText(css));
        }
        catch (Exception e)
        {
            GD.PushWarning("[anomaly] tgui icons: " + e.Message);
        }
    }
}
