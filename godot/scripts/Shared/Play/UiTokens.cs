namespace Anomaly.Client.Shared;

// Owner: ui-overhaul.md section 2 (package 2). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The UI overhaul's sizes and colours, in one place (ui-overhaul.md section 2). Sizes are reference pixels on a screen of
/// 1080 lines; <see cref="Unit"/> turns them into screen pixels with the player's HUD scale. Colours are <c>0xRRGGBBAA</c>,
/// the palette from the wireframes' section 7. Later slices add their own tokens here.
/// </summary>
public static class UiTokens
{
    /// <summary>
    /// Screen pixels per reference pixel: the options' HUD scale, shrunk on a screen lower than 1080 lines and not grown on a higher
    /// one, as the HUD's text is (<see cref="HudMetrics.TextScale"/> is twice this).
    /// </summary>
    public static float Unit(HudMetrics metrics) => metrics.TextScale / 2f;

    // ---- the palette (wireframes section 7) ----

    /// <summary>Common radio, soft mint.</summary>
    public const uint Common = 0x8FD694FF;

    /// <summary>Security, crimson red.</summary>
    public const uint Security = 0xFF4D4DFF;

    /// <summary>Engineering, safety amber.</summary>
    public const uint Engineering = 0xFFA62BFF;

    /// <summary>Medical, clinical cyan.</summary>
    public const uint Medical = 0x38D9F5FF;

    /// <summary>Science, violet.</summary>
    public const uint Science = 0xC77DFFFF;

    /// <summary>Command, high-luminance gold.</summary>
    public const uint Command = 0xFFE156FF;

    /// <summary>Local speech: white text ...</summary>
    public const uint LocalText = 0xFFFFFFFF;

    /// <summary>... on a dark slate pill.</summary>
    public const uint LocalBack = 0x1E242BFF;

    // ---- the Tab overlay's columns (wireframes section 3), shares of the screen's width ----

    /// <summary>Where the chat column ends and the doll column starts.</summary>
    public const float OverlayLeftShare = 0.28f;

    /// <summary>Where the doll column ends and the containers' column starts.</summary>
    public const float OverlayRightShare = 0.64f;

    // ---- chips: a key label or a short status in a small rounded box, white on the local pill's slate with a white rim ----

    /// <summary>A chip's fill: <see cref="LocalBack"/>, a little see-through.</summary>
    public const uint ChipBack = 0x1E242BD9;

    /// <summary>A chip's rim.</summary>
    public const uint ChipRim = 0xFFFFFFB3;

    /// <summary>A key chip's height.</summary>
    public const float ChipHeight = 24f;

    /// <summary>A key chip's text size.</summary>
    public const float ChipFont = 16f;

    /// <summary>The room left and right of a chip's text.</summary>
    public const float ChipPadX = 7f;

    /// <summary>A chip's corner radius.</summary>
    public const float ChipRadius = 5f;

    /// <summary>A chip's rim width.</summary>
    public const float ChipRimWidth = 1.5f;

    /// <summary>The ping and FPS chip's height (top right).</summary>
    public const float StatusChipHeight = 30f;

    /// <summary>The ping and FPS chip's text size.</summary>
    public const float StatusChipFont = 20f;

    // ---- the prompts beside the crosshair (KeyChips.PromptBlock) ----

    /// <summary>The gap between what the block stands beside (the crosshair's arms or the pointer) and the block.</summary>
    public const float PromptOffsetX = 10f;

    /// <summary>Where the block's top is, from the crosshair down (negative: the name line is about level with it, the prompt lines under it).</summary>
    public const float PromptOffsetY = -28f;

    /// <summary>The target's name line: its height ...</summary>
    public const float PromptNameLine = 26f;

    /// <summary>... and its text size.</summary>
    public const float PromptNameFont = 22f;

    /// <summary>A prompt line's height: a chip and the gap under it.</summary>
    public const float PromptLine = 28f;

    /// <summary>A prompt's text size.</summary>
    public const float PromptFont = 20f;

    /// <summary>The gap between a chip and its text.</summary>
    public const float PromptChipGap = 8f;

    /// <summary>The widest a prompt block grows.</summary>
    public const float PromptMaxWidth = 560f;

    /// <summary>The least room kept between the HUD's text and the screen's edge.</summary>
    public const float ScreenMargin = 16f;

    /// <summary>The dark outline of the HUD text drawn straight over the world (the prompts), so it reads over bright scenery: its colour ...</summary>
    public const uint TextOutline = 0x000000F2;

    /// <summary>... and its width.</summary>
    public const float TextOutlineWidth = 12f;

    // ---- the balloon alert under the crosshair (ChatLines), so that the prompts keep clear of it ----

    /// <summary>The balloon's baseline, from the crosshair down.</summary>
    public const float BalloonBaseline = 156f;

    /// <summary>The balloon's text size.</summary>
    public const float BalloonFont = 34f;

    /// <summary>The widest the balloon's line is.</summary>
    public const float BalloonWidth = 800f;
}
