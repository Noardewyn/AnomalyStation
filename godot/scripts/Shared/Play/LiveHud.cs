using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: ui-overhaul.md section 6 (package 2, slice B). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The server still computes tg's screen entries and a click still goes to them (D107 as revised by ui-overhaul.md section 1); the client
// re-places the health doll into a panel at the middle right and draws tg's combat toggle as a native pill under it. The doll only shows
// injuries: targeting is where the player looks (the owner, 2026-11-24), so tg's zone selector shows only while the cursor is free.

/// <summary>What a screen entry is for the live HUD.</summary>
public enum LiveHudRole : byte
{
    /// <summary>Drawn at tg's <c>screen_loc</c> as before.</summary>
    Tg,

    /// <summary>tg's health doll (its appearance holds the doll's zone states, <see cref="HudReader"/>): re-placed into the doll panel.</summary>
    Doll,

    /// <summary>
    /// tg's target zone selector (icon state <c>zone_sel</c>): at tg's place in cursor mode only (<see cref="LiveHud.Shown"/>); in the captured view
    /// targeting is where the player looks.
    /// </summary>
    ZoneSelector,

    /// <summary>tg's combat toggle (icon state <c>combat</c> or <c>combat_off</c>): drawn as the native combat pill.</summary>
    CombatToggle,
}

/// <summary>Where the live HUD's boxes go, screen pixels (<see cref="LiveHud.Layout"/>).</summary>
/// <param name="Panel">The doll panel's background.</param>
/// <param name="Doll">The square the doll's 32 x 32 icon fills (<see cref="ScreenLoc.FitIcon"/>).</param>
/// <param name="Pill">The combat pill under the panel.</param>
/// <param name="DollScale">Screen pixels per tg pixel of the doll: a whole number.</param>
public readonly record struct LiveHudLayout(HudRect Panel, HudRect Doll, HudRect Pill, int DollScale)
{
    /// <summary>The left edge of the panel and the pill together: whatever stands left of them (the zone labels, the prompts) ends a gap before it.</summary>
    public float Left => Math.Min(Panel.Left, Pill.Left);
}

/// <summary>The live HUD's reading of tg's screen entries and its layout (ui-overhaul.md section 6, wireframes Mockup 1).</summary>
public static class LiveHud
{
    /// <summary>
    /// What <paramref name="entry"/> is, by its icon state and group, never by its place: an entry of a native window (a storage, the
    /// inventory) or an action button is always <see cref="LiveHudRole.Tg"/>.
    /// </summary>
    public static LiveHudRole RoleOf(ReplicaWorld replica, ScreenEntry entry)
    {
        if (entry.Group is HudGroup.Storage or HudGroup.ToggleableInventory || entry.ActionTarget > 0
            || !replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
            return LiveHudRole.Tg;
        string state = replica.ResolveString(app.IconState);
        if (state == "zone_sel")
            return LiveHudRole.ZoneSelector;
        if (state is "combat" or "combat_off")
            return LiveHudRole.CombatToggle;
        var zones = new List<FoundZone>();
        HudReader.Collect(replica, entry.Appearance, zones);
        return zones.Count > 0 ? LiveHudRole.Doll : LiveHudRole.Tg;
    }

    /// <summary>
    /// Whether an entry of <paramref name="role"/> is drawn and takes clicks now: tg's zone selector only while the cursor is free (cursor mode, Tab),
    /// since looking aims at six zones only and the eyes, mouth and groin (tg's surgeries, a ctrl-click on the doll) are chosen on it; every other
    /// entry always.
    /// </summary>
    public static bool Shown(LiveHudRole role, bool cursorMode) => role != LiveHudRole.ZoneSelector || cursorMode;

    /// <summary>Whether the combat toggle <paramref name="toggle"/> shows combat mode on (tg's icon state <c>combat</c>).</summary>
    public static bool CombatOn(ReplicaWorld replica, ScreenEntry toggle)
        => replica.TryGetAppearance(toggle.Appearance, out AppearanceEntry app) && replica.ResolveString(app.IconState) == "combat";

    /// <summary>The combat pill's word.</summary>
    public static string CombatText(bool on) => on ? I18n.T("Combat") : I18n.T("Peaceful");

    /// <summary>
    /// What the HUD pick gets for the doll entry <paramref name="id"/>: the doll's own 32 x 32 icon square in its box, with no picture, so a point
    /// on it is tg's icon pixel whatever size its zones' pictures are (a wound outline grows them).
    /// </summary>
    public static HudDrawn DollDrawn(long id, in LiveHudLayout layout) => new(id, ScreenLoc.FitIcon(layout.Doll, 32, 32, 0, 0), null, 0, 0);

    /// <summary>What the HUD pick gets for the combat toggle <paramref name="id"/>: the pill, with no picture (all of it takes the click).</summary>
    public static HudDrawn PillDrawn(long id, in LiveHudLayout layout) => new(id, layout.Pill, null, 0, 0);

    /// <summary>Where a column of text left of the panel and the pill (the zone labels) ends, screen pixels from the left.</summary>
    public static float LeftOfPanel(in LiveHudLayout layout, in HudMetrics metrics) => layout.Left - UiTokens.DollPanelGap * UiTokens.Unit(metrics);

    // tg's lowest row of the bottom-right buttons that stand under the panel's column (ui_above_movement_top, ui_human_area): the pill stays above it
    private static readonly ScreenLocSpec BottomRow = ScreenLoc.TryParse("EAST-2:26,SOUTH+1:24", out ScreenLocSpec spec) ? spec : default;

    /// <summary>
    /// The doll panel at the middle right, its right edge <see cref="UiTokens.DollPanelGap"/> left of tg's right-hand column (the tiles at
    /// <c>EAST-1</c>: the health, mood, stamina and internals icons and the alerts), and the combat pill under it; the two centred vertically,
    /// raised when that would reach tg's bottom-right buttons. The doll is a whole multiple of tg's pixels (<see cref="UiTokens.DollPixels"/>
    /// at the reference size), made smaller, down to one, while the panel and the pill do not fit above those buttons. Everything else scales
    /// with <see cref="UiTokens.Unit"/> (the options' HUD scale).
    /// </summary>
    public static LiveHudLayout Layout(in HudMetrics metrics)
    {
        float u = UiTokens.Unit(metrics);
        float pad = UiTokens.DollPanelPad * u, gap = UiTokens.PillGap * u, pill = MathF.Round(UiTokens.PillHeight * u);
        float right = metrics.Width - 2 * metrics.Tile - UiTokens.DollPanelGap * u; // EAST-1's tile starts two tiles from the right edge
        float floor = ScreenLoc.Place(BottomRow, metrics, 32, 32, 0, 0, out _).Top - UiTokens.DollPanelGap * u;
        int k = Math.Max(1, (int)Math.Round(UiTokens.DollPixels * u, MidpointRounding.AwayFromZero));
        float PanelHeight(int scale) => MathF.Round(2 * pad + HudMetrics.IconPixels * scale);
        while (k > 1 && PanelHeight(k) + gap + pill > floor)
            k--;
        float side = HudMetrics.IconPixels * k, panelHeight = PanelHeight(k), block = panelHeight + gap + pill;
        float width = MathF.Round(Math.Max(side + 2 * pad, UiTokens.PillWidth * u)); // the panel and the pill one column, the doll centred
        float top = MathF.Round(Math.Max(0f, Math.Min((metrics.Height - block) / 2f, floor - block)));
        var panel = new HudRect(MathF.Floor(right - width), top, width, panelHeight);
        var doll = new HudRect(panel.Left + MathF.Floor((width - side) / 2f), top + MathF.Round(pad), side, side);
        var pillRect = new HudRect(panel.Left, MathF.Round(top + panelHeight + gap), width, pill);
        return new LiveHudLayout(panel, doll, pillRect, k);
    }
}
