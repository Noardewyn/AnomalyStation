using Godot;

namespace Anomaly.Client.Hud;

/// <summary>
/// The FPS and the connection's numbers at the top right (the owner, 2026-10-13: "an option in the menu that turns the
/// drawing of the fps and the server stats in the top right corner on and off, drawn by our game UI, not ImGui"; D138).
/// Drawn like the chat lines (<see cref="ChatLines"/>): the fallback font with a dark outline, at the HUD's text scale, right
/// aligned, left of tg's alert column. The scene fills <see cref="Lines"/> every frame while it is on.
/// </summary>
public partial class StatsCorner : Control
{
    /// <summary>The lines, top first.</summary>
    public string[] Lines { get; set; } = Array.Empty<string>();

    /// <summary>The text size factor (the chat's: twice P7's sizes times the HUD scale).</summary>
    public float TextScale { get; set; } = 2f;

    /// <summary>The owner (2026-11-01): how opaque the corner's three texts (the version tag, these lines, the screentip) are (PlayOptions.HudTextOpacity).</summary>
    public static float TextAlpha { get; set; } = 0.6f;

    private int Px(float px) => Math.Max(1, (int)Math.Round(px * TextScale));

    public override void _Ready()
    {
        Name = "StatsCorner";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("StatsCorner._Process");
        Modulate = new Color(1, 1, 1, TextAlpha);
        if (Visible)
            QueueRedraw();
    }

    /// <summary>Where the corner's first line starts at text scale <paramref name="k"/>: below the build's version tag in
    /// the corner (App.AddVersionTag, docs/versioning.md). The screentip under the lines counts from here too.</summary>
    public static float Top(float k) => Math.Max(6 * k, (App.Instance?.VersionTagBottom ?? 0) + 4 * k);

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        Vector2 size = GetViewportRect().Size;
        int fontSize = Px(11);
        // tg's alerts stand in the screen's last column (screen_loc EAST-1): the text ends left of it
        float right = size.X - 48 * TextScale;
        float top = Top(TextScale);
        float y = top + font.GetAscent(fontSize);
        float lineHeight = font.GetHeight(fontSize) + 2 * TextScale;
        foreach (string line in Lines)
        {
            float width = font.GetStringSize(line, HorizontalAlignment.Left, -1, fontSize).X;
            var at = new Vector2(right - width, y);
            DrawStringOutline(font, at, line, HorizontalAlignment.Left, -1, fontSize, Px(4), new Color(0, 0, 0, 0.85f));
            DrawString(font, at, line, HorizontalAlignment.Left, -1, fontSize, new Color(0.85f, 0.95f, 0.85f));
            y += lineHeight;
        }
    }
}
