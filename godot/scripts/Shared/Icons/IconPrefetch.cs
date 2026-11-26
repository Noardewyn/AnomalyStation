namespace Anomaly.Client.Shared.Icons;

// Engine-free. The sheets a fight draws first that no map object names, read on worker threads when the game starts (IconLibrary.Prefetch):
// the first blast or wound then finds them decoded instead of waiting a few frames for them.

/// <summary>The big sheets of combat effects, prefetched at the start of a game.</summary>
public static class IconPrefetch
{
    /// <summary>tg's fullscreen overlays (damage, crit, flash, blindness; 4800 x 4320), the explosion (/obj/effect/explosion), smoke and sparks,
    /// the hotspot's fire, a burning mob, the alerts and blood.</summary>
    public static readonly string[] CombatSheets =
    {
        "icons/hud/screen_full.dmi",
        "icons/effects/96x96.dmi",
        "icons/effects/effects.dmi",
        "icons/effects/fire.dmi",
        "icons/mob/effects/onfire.dmi",
        "icons/hud/screen_alert.dmi",
        "icons/effects/blood.dmi",
    };

    /// <summary>
    /// The folders of the sheets a round draws early whatever its map: the human body and its parts, what is worn and held, the HUD, the
    /// mob and the world's effects. The join's loading screen decodes them with the round's own (the sheets its appearance tables name).
    /// </summary>
    public static readonly string[] CommonFolders =
    {
        "icons/mob/human/",
        "icons/mob/clothing/",
        "icons/mob/inhands/",
        "icons/mob/effects/",
        "icons/hud/",
        "icons/effects/",
    };

    /// <summary>Single sheets every new body draws at once that the folders leave out: the legs' masks, and what the outfit puts in the HUD
    /// (the PDA, the ID card, the jumpsuit). The miss log (AssetPreload.Miss) names more as they show.</summary>
    public static readonly string[] CommonSheets =
    {
        "icons/mob/leg_masks.dmi",
        "icons/obj/devices/modular_pda.dmi",
        "icons/obj/card.dmi",
        "icons/obj/clothing/under/color.dmi",
    };

    /// <summary>A sheet decoded larger than this (tg's station explosion, 144 MB; the old parallax) is left to be read when it is drawn.</summary>
    public const long MaxDecodedBytes = 32L * 1024 * 1024;

    /// <summary>The bytes a PNG takes decoded (width x height x 4, from its header), or -1 when it is no PNG.</summary>
    public static long DecodedBytes(byte[] png)
    {
        if (png == null || png.Length < 24 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G')
            return -1;
        long width = ((long)png[16] << 24) | ((long)png[17] << 16) | ((long)png[18] << 8) | png[19];
        long height = ((long)png[20] << 24) | ((long)png[21] << 16) | ((long)png[22] << 8) | png[23];
        return width * height * 4;
    }

    /// <summary>Whether a sheet is decoded ahead: the combat list's always, any other unless it is a giant.</summary>
    public static bool Ahead(string resource, byte[] png)
        => Array.IndexOf(CombatSheets, resource) >= 0 || DecodedBytes(png) is >= 0 and <= MaxDecodedBytes;

    /// <summary>Turns the library's background loading on and starts the combat sheets (env <c>ANOMALY_ICONS_SYNC=1</c>: every load blocks, as
    /// before, to compare).</summary>
    public static void Start(IconLibrary library)
    {
        if (Environment.GetEnvironmentVariable("ANOMALY_ICONS_SYNC") == "1")
            return;
        library.Background = true;
        foreach (string sheet in CombatSheets)
            library.Prefetch(sheet);
    }
}
