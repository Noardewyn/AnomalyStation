namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>Which entities a click passes through to what is behind them, decided on the client by type path.</summary>
public static class ClickThrough
{
    // tg sets mouse_opacity = MOUSE_OPACITY_TRANSPARENT on these (temporary_visual.dm:6, game.dm:139 for the attack's flick visual, projectile_effects.dm:7
    // for a shot's tracer, muzzle and impact) and BYOND never gives them a click; mouse_opacity is not replicated, so the client knows them by type.
    private static readonly string[] Types = { "/atom/movable/flick_visual", "/obj/effect/temp_visual", "/obj/effect/projectile" };

    /// <summary>True for tg's short-lived visual types that are mouse-transparent: the type itself or a subtype of it.</summary>
    public static bool IsType(string typePath)
    {
        if (string.IsNullOrEmpty(typePath))
            return false;
        foreach (string type in Types)
            if (typePath == type || typePath.StartsWith(type + "/", StringComparison.Ordinal))
                return true;
        return false;
    }
}
