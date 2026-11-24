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

    /// <summary>A token's colour (<c>0xRRGGBBAA</c>) as red, green and blue from 0 to 1.</summary>
    public static Rgb ToRgb(uint rgba) => new(((rgba >> 24) & 255) / 255f, ((rgba >> 16) & 255) / 255f, ((rgba >> 8) & 255) / 255f);

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

    /// <summary>The narrowest a prompt block beside the crosshair is cut to before the doll panel; with less room there it goes left of the crosshair.</summary>
    public const float PromptMinWidth = 100f;

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

    // ---- the health doll's colours (ui-overhaul.md section 6): one standard ramp when no colourblind palette is chosen ----

    /// <summary>Damage level 0: green.</summary>
    public const uint DollUnharmed = Common;

    /// <summary>Levels 1 and 2: yellow.</summary>
    public const uint DollMinor = Command;

    /// <summary>Levels 3 and 4: orange.</summary>
    public const uint DollModerate = Engineering;

    /// <summary>Level 5: red. (A disabled part keeps tg's own picture: HealthZones.StandardColor.)</summary>
    public const uint DollSevere = Security;

    /// <summary>A missing part: dark grey.</summary>
    public const uint DollMissing = 0x4A4F55FF;

    /// <summary>A dead mob: near black.</summary>
    public const uint DollDead = 0x1A1A1AFF;

    /// <summary>The hatching (D70) over a light part colour: black lines, a little see-through ...</summary>
    public const uint HatchDark = 0x000000B3;

    /// <summary>... and over a dark one (a colourblind palette's worst levels): white lines, more see-through.</summary>
    public const uint HatchLight = 0xFFFFFF99;

    // ---- the doll panel at the middle right and the combat pill under it (LiveHud.Layout) ----

    /// <summary>The doll's picture is this many screen pixels per tg pixel at the reference size (an integer multiple at every size).</summary>
    public const float DollPixels = 3f;

    /// <summary>The room inside the panel's edges, around the doll.</summary>
    public const float DollPanelPad = 10f;

    /// <summary>The gap between the panel and tg's right-hand column of HUD entries, and between the panel and what stands left of it (the prompts, the zone labels).</summary>
    public const float DollPanelGap = 12f;

    /// <summary>The panel's fill.</summary>
    public const uint DollPanelBack = 0x1E242BB3;

    /// <summary>The panel's corner radius.</summary>
    public const float DollPanelRadius = 6f;

    /// <summary>The gap between the panel and the combat pill.</summary>
    public const float PillGap = 6f;

    /// <summary>The least width of the doll panel and the combat pill under it, one column (room for the pill's key chip and "Peaceful").</summary>
    public const float PillWidth = 150f;

    /// <summary>The combat pill's height.</summary>
    public const float PillHeight = 32f;

    /// <summary>The combat pill's text size.</summary>
    public const float PillFont = 18f;

    /// <summary>The combat pill's fill when combat mode is on.</summary>
    public const uint PillCombat = Security;
}
