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

    /// <summary>The narrowest a prompt block beside the crosshair is cut to before the vitals at the right edge (and their chips); with less room there it goes left of the crosshair.</summary>
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

    // ---- the vitals: the full stack at the right edge, or the reduced doll left of tg's right column (LiveHud.Layout) ----

    /// <summary>The doll's picture is this many screen pixels per tg pixel at the reference size (an integer multiple at every size).</summary>
    public const float DollPixels = 3f;

    /// <summary>The gap between the reduced doll and tg's right-hand column of HUD entries, and between the vitals and what stands left of them (the prompts, the zone labels).</summary>
    public const float DollPanelGap = 12f;

    /// <summary>The dark fill of the alert chips and the hand boxes (slice B's doll panel's, which slice E removed).</summary>
    public const uint DollPanelBack = 0x1E242BB3;

    /// <summary>An alert chip's corner radius.</summary>
    public const float DollPanelRadius = 6f;

    // ---- the hands either side of the hotbar at the bottom centre (LiveHud.Hands; wireframes Mockup 1; ui-overhaul.md section 10) ----

    /// <summary>The hotbar's cells (1 to 7 the belt, 8 and 9 the pockets, 0 the ID slot): how many ...</summary>
    public const int HotbarSlots = 10;

    /// <summary>... their side ...</summary>
    public const float HotbarSlot = 48f;

    /// <summary>... the gap between two in one group ...</summary>
    public const float HotbarSlotGap = 4f;

    /// <summary>... and between two groups (the belt's, the pockets', the ID slot's).</summary>
    public const float HotbarGroupGap = 12f;

    /// <summary>The hotbar's whole width: ten cells, seven gaps within the groups and two between them.</summary>
    public const float HotbarWidth = HotbarSlots * HotbarSlot + (HotbarSlots - 3) * HotbarSlotGap + 2 * HotbarGroupGap;

    /// <summary>A cell's rim: the other hand box's faint one.</summary>
    public const uint HotbarRim = HandRim;

    /// <summary>... red for <see cref="HotbarRefusedSeconds"/> after a press that did nothing.</summary>
    public const uint HotbarRefusedRim = Security;

    /// <summary>How long a refused cell's rim is red, seconds.</summary>
    public const double HotbarRefusedSeconds = 0.33;

    /// <summary>An out item's picture (in one of the player's hands, taken from the cell): its opacity.</summary>
    public const float HotbarOutAlpha = 1f / 3f;

    /// <summary>An empty cell's key cap: its opacity.</summary>
    public const float HotbarEmptyCapAlpha = 0.45f;

    /// <summary>The belt's cells with no belt worn: their opacity.</summary>
    public const float HotbarNoBeltAlpha = 0.5f;

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


    /// <summary>The gap between a hand box and the hotbar.</summary>
    public const float HandGap = 8f;

    /// <summary>The swap-hands key cap in the inactive hand box's top outer corner: its height ...</summary>
    public const float SwapCapHeight = 18f;

    /// <summary>... the room between it and the box's edges ...</summary>
    public const float SwapCapInset = 3f;

    /// <summary>... and its text size.</summary>
    public const float SwapCapFont = 12f;

    /// <summary>A key cap's label too wide for its cap is drawn smaller, to this share of the cap's text size at least, then cut (LiveHud.CapLabel).</summary>
    public const float CapLeastShare = 0.6f;

    /// <summary>A handcuffed or blocked hand (tg's handcuff or blocked overlay on its box): its border, whether it is the active hand or not ...</summary>
    public const uint HandMarkedBorder = Security;

    /// <summary>... and its lighter fill, so that tg's mark over it reads at a glance.</summary>
    public const uint HandMarkedBack = 0x5A6470D9;

    // ---- the held-item hint strip over the hands (LiveHud.HintBy): slice D's combat pill's height, dark fill and rounded ends ----

    /// <summary>The strip's height.</summary>
    public const float HintHeight = 32f;

    /// <summary>The gap between the strip and the top of the hands' row (the active box) under it.</summary>
    public const float HintRise = 6f;

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
    /// The room the vitals leave at the screen's top: the build's version tag (11 text units, about 32
    /// reference pixels with its line), the gap under it and the ping and FPS chip (StatsCorner.Top), and a margin.
    /// </summary>
    public const float CardTopRoom = 32f + 8f + StatusChipHeight + ScreenMargin;

    // ---- the full vitals stack on the human HUD (LiveHud.VitalsColumn; slice E) ----

    /// <summary>The gap between two pictures of the stack, and between the mood face and tg's combat toggle under it.</summary>
    public const float VitalsMargin = 8f;

    // ---- tg's alerts as a stack of chips by the vitals (LiveHud.AlertStack) ----

    /// <summary>An alert chip's side: a square holding tg's alert picture.</summary>
    public const float AlertChip = 40f;

    /// <summary>The room between a chip's edge and the alert's picture.</summary>
    public const float AlertChipInset = 4f;

    /// <summary>The least room between a chip's edge and the visible art it shows (ui-overhaul.md section 11, <c>LiveHud.ChipArt</c>).</summary>
    public const float AlertChipArtMargin = 2f;

    /// <summary>The gap between two chips.</summary>
    public const float AlertChipGap = 4f;

    /// <summary>The gap between the doll's top and the lowest chip above it, and between the doll and a column of chips left of it.</summary>
    public const float AlertStackGap = 6f;

    /// <summary>The least side the chips are made when they do not fit at full size, screen pixels (not reference pixels).</summary>
    public const float AlertChipMinPixels = 24f;

    // ---- the chat in first person (ChatPlace; ui-overhaul.md section 13) ----

    /// <summary>The gap between the bottom of the chat window at its default place, or of the chat lines, and the top of the bottom row under it.</summary>
    public const float ChatRowGap = 8f;

    /// <summary>The share of its opacity the round's chat window's backing (its panel, title bar and tabs) is drawn at while the player is not typing.</summary>
    public const float ChatRestAlpha = 0.35f;

    /// <summary>The outline of the chat window's log text while the window is see-through, so it reads over the room (at the window's scale 1).</summary>
    public const int ChatLogOutline = 4;

    /// <summary>The most a press in the chat window may move, screen pixels, and still be a click (not a drag, a resize or a selection).</summary>
    public const float ChatClickSlop = 4f;
}
