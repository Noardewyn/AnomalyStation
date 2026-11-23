using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Hud;

/// <summary>
/// The FPS and the connection's numbers at the top right (the owner, 2026-10-13: "an option in the menu that turns the
/// drawing of the fps and the server stats in the top right corner on and off, drawn by our game UI, not ImGui"; D138).
/// The ping and the FPS are one chip at the top (ui-overhaul.md section 6, <see cref="UiTokens"/>), the other numbers lines under it,
/// drawn like the chat lines (<see cref="ChatLines"/>): the fallback font with a dark outline, at the HUD's text scale, right
/// aligned, left of tg's alert column. The scene fills <see cref="Chip"/> and <see cref="Lines"/> every frame while it is on.
/// </summary>
public partial class StatsCorner : Control
{
    /// <summary>The ping and FPS chip's text.</summary>
    public string Chip { get; set; } = string.Empty;

    /// <summary>The lines under the chip, top first.</summary>
    public string[] Lines { get; set; } = Array.Empty<string>();

    /// <summary>The text size factor (the chat's: twice P7's sizes times the HUD scale).</summary>
    public float TextScale { get; set; } = 2f;

    /// <summary>Screen pixels per reference pixel of the chip (<see cref="UiTokens.Unit"/>), set by the scene with <see cref="TextScale"/>.</summary>
    public float Unit { get; set; } = 1f;

    /// <summary>The owner (2026-11-01): how opaque the corner's three texts (the version tag, these lines, the prompts) are (PlayOptions.HudTextOpacity).</summary>
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

    /// <summary>Where the corner's chip starts at text scale <paramref name="k"/>: below the build's version tag in
    /// the corner (App.AddVersionTag, docs/versioning.md).</summary>
    public static float Top(float k) => Math.Max(6 * k, (App.Instance?.VersionTagBottom ?? 0) + 4 * k);

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        Vector2 size = GetViewportRect().Size;
        int fontSize = Px(11);
        float u = Unit;
        // tg's alerts stand in the screen's last column (screen_loc EAST-1): the text ends left of it
        float right = size.X - 48 * TextScale;
        float top = Top(TextScale);
        if (!string.IsNullOrEmpty(Chip))
        {
            int chipFont = Math.Max(1, (int)Math.Round(UiTokens.StatusChipFont * u));
            float width = font.GetStringSize(Chip, HorizontalAlignment.Left, -1, chipFont).X + 2 * UiTokens.ChipPadX * u;
            float height = UiTokens.StatusChipHeight * u;
            ChatLines.DrawChip(this, font, new Rect2(right - width, top, width, height), Chip, chipFont, u, 1f); // Modulate carries the opacity
            top += height + 4 * u;
        }
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
