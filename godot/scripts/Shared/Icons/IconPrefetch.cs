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
