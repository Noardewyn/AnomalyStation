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

    // ---- the doll panel and the combat pill under it: the vitals column at the right edge, or the reduced column left of tg's (LiveHud.Layout) ----

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

    // ---- the hands either side of the hotbar's gap at the bottom centre (LiveHud.Hands; wireframes Mockup 1) ----

    /// <summary>The hotbar's slots: how many ...</summary>
    public const int HotbarSlots = 9;

    /// <summary>... their side ...</summary>
    public const float HotbarSlot = 48f;

    /// <summary>... and the gap between two.</summary>
    public const float HotbarSlotGap = 4f;

    /// <summary>The hotbar's whole width: nine slots and the eight gaps between them.</summary>
    public const float HotbarWidth = HotbarSlots * HotbarSlot + (HotbarSlots - 1) * HotbarSlotGap;

    /// <summary>The room under the hotbar and the hands, to the screen's bottom edge.</summary>
    public const float HotbarMargin = 16f;

    /// <summary>The active hand's box: its side ...</summary>
    public const float HandActive = 72f;

    /// <summary>... and the other hand's.</summary>
    public const float HandInactive = 56f;

    /// <summary>The active hand's border: its width ...</summary>
    public const float HandBorder = 2f;

    /// <summary>... and colour (the palette's amber).</summary>
    public const uint HandActiveBorder = Engineering;

    /// <summary>A hand box's fill.</summary>
    public const uint HandBack = DollPanelBack;

    /// <summary>The other hand box's faint rim (not a highlight: it only keeps the box seen over a dark floor), one reference pixel wide.</summary>
    public const uint HandRim = 0xFFFFFF40;

    /// <summary>The room between a hand box's edge and the held item's picture.</summary>
    public const float HandInset = 6f;

    /// <summary>
    /// The least a hand box may be, screen pixels (not reference pixels): tg's own 32-pixel slot at its native size. The row keeps clear of the
    /// chat window only while its smaller box keeps this size (LiveHud.Hands).
    /// </summary>
    public const float HandMinPixels = 32f;

    /// <summary>The gap between a hand box and the hotbar.</summary>
    public const float HandGap = 8f;

    /// <summary>The swap-hands key cap in the inactive hand box's top outer corner: its height ...</summary>
    public const float SwapCapHeight = 18f;

    /// <summary>... the room between it and the box's edges ...</summary>
    public const float SwapCapInset = 3f;

    /// <summary>... and its text size.</summary>
    public const float SwapCapFont = 12f;

    /// <summary>A handcuffed or blocked hand (tg's handcuff or blocked overlay on its box): its border, whether it is the active hand or not ...</summary>
    public const uint HandMarkedBorder = Security;

    /// <summary>... and its lighter fill, so that tg's mark over it reads at a glance.</summary>
    public const uint HandMarkedBack = 0x5A6470D9;

    // ---- the held-item hint strip over the hands (LiveHud.HintBy; the designer, 2026-11-26): the combat pill's height, dark fill and rounded ends ----

    /// <summary>The room at each end of the strip.</summary>
    public const float HintPad = 8f;

    /// <summary>The gap after the item's picture and between two entries.</summary>
    public const float HintGap = 12f;

    /// <summary>The gap between an entry's key cap and its verb.</summary>
    public const float HintCapGap = 6f;

    /// <summary>The strip's text and caps: a share of the prompts' size ...</summary>
    public const float HintShare = 0.8f;

    /// <summary>... and the least share of the prompts' size a strip too wide for the hands' row is made smaller to; below it the strip is left out.</summary>
    public const float HintLeastShare = 0.6f;

    /// <summary>
    /// How high tg's drop and swap buttons are drawn in their 32 x 32 squares, tg pixels from the bottom: their pictures (screen_midnight.dmi
    /// "act_drop" and "act_swap", the default HUD style) are opaque in rows 22 to 31 of 32 only. The hint strip sits on that visible part.
    /// </summary>
    public const float DropSwapVisible = 10f;

    /// <summary>
    /// The room the doll's column leaves at the screen's top: the build's version tag (11 text units, about 32
    /// reference pixels with its line), the gap under it and the ping and FPS chip (StatsCorner.Top), and a margin.
    /// </summary>
    public const float CardTopRoom = 32f + 8f + StatusChipHeight + ScreenMargin;

    // ---- the vitals column on the human HUD (LiveHud.Layout; the designer, 2026-11-26) ----

    /// <summary>The width of the vitals column, the panel and the pill in it (never narrower than the panel's widest cell and its padding).</summary>
    public const float VitalsWidth = 128f;

    /// <summary>The gap between the column and the screen's right edge, and between the pill and tg's highest button under it.</summary>
    public const float VitalsMargin = 8f;

    /// <summary>The room inside the panel around its cells.</summary>
    public const float VitalsPad = 8f;

    /// <summary>The gap between two cells of the panel (a line of <see cref="VitalsLine"/> in its middle).</summary>
    public const float VitalsCellGap = 4f;

    /// <summary>The line between two cells: the faint rim the other hand box has.</summary>
    public const uint VitalsLine = HandRim;

    // ---- tg's alerts as a stack of chips by the doll panel (LiveHud.AlertStack; the designer, 2026-11-26) ----

    /// <summary>An alert chip's side: a square holding tg's alert picture.</summary>
    public const float AlertChip = 40f;

    /// <summary>The room between a chip's edge and the alert's picture.</summary>
    public const float AlertChipInset = 4f;

    /// <summary>The gap between two chips.</summary>
    public const float AlertChipGap = 4f;

    /// <summary>The gap between the doll panel's top and the lowest chip.</summary>
    public const float AlertStackGap = 6f;

    /// <summary>The least side the chips are made when they do not fit at full size, screen pixels (not reference pixels).</summary>
    public const float AlertChipMinPixels = 24f;
}
