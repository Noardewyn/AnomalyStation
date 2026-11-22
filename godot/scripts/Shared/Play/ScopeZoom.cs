using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// M7x slice D, DV98: tg's scope component (datums/components/scope.dm) shifts the 2D view toward the cursor, scaled by its range_modifier. In the
// first-person view the camera ray is the aim, so the scope narrows the field of view by the range_modifier instead: while the scope's
// screen object (the "scope1" fullscreen the component puts on the user's screen) is there, the camera zooms. The range_modifier rides in the
// hundredths of the screen object's layer above tg's FULLSCREEN_LAYER (scope.cs zoom), so the protocol carries it already.

/// <summary>The scope's zoom, from the own screen objects, and the field of view and the mouse speed it gives.</summary>
public static class ScopeZoom
{
    /// <summary>tg's <c>FULLSCREEN_LAYER</c> (<c>__DEFINES/layers.dm:310</c>): the layer a fullscreen object has.</summary>
    public const float FullscreenLayer = 2f;

    /// <summary>The widest zoom taken (a layer's hundredths cannot carry more than 100 anyway).</summary>
    public const float MaxZoom = 10f;

    /// <summary>The seconds a zoom takes to settle most of the way (the scope-in is not a cut).</summary>
    public const float EaseSeconds = 0.12f;

    /// <summary>True for the scope's own fullscreen: <c>icon_state</c> "scope0" or "scope1" of the fullscreens' file.</summary>
    public static bool IsScope(string icon, string iconState)
        => iconState != null && iconState.StartsWith("scope", StringComparison.Ordinal)
           && icon != null && icon.EndsWith("screen_full.dmi", StringComparison.Ordinal);

    /// <summary>The range_modifier a scope's layer carries (1 for a layer that carries none).</summary>
    public static float RangeModifier(float layer)
    {
        float hundredths = (layer - FullscreenLayer) * 100f;
        return hundredths < 1f ? 1f : Math.Min(hundredths, MaxZoom);
    }

    /// <summary>
    /// The zoom the own screen objects ask for: the range_modifier of the scope's fullscreen, 1 without one. A fullscreen that is fading out
    /// (tg's <c>clear_fullscreen</c> animates its alpha away for a second before it takes it off) is the scope put away: no zoom.
    /// </summary>
    public static float Of(ReplicaWorld replica)
    {
        if (replica == null)
            return 1f;
        float zoom = 1f;
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if (entry.Animation != null || !replica.TryGetAppearance(entry.Appearance, out AppearanceEntry look))
                continue;
            if (IsScope(replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState)))
                zoom = Math.Max(zoom, RangeModifier(look.Layer));
        }
        return zoom;
    }

    /// <summary>The vertical field of view that sees 1/<paramref name="zoom"/> of the tangent of <paramref name="fovDegrees"/>'s half.</summary>
    public static float FovDegrees(float fovDegrees, float zoom)
    {
        if (zoom <= 1f)
            return fovDegrees;
        double half = Math.Tan(fovDegrees * Math.PI / 360.0) / zoom;
        return (float)(2.0 * Math.Atan(half) * 180.0 / Math.PI);
    }

    /// <summary>One frame of the ease toward <paramref name="target"/> (exponential, frame-rate independent).</summary>
    public static float Approach(float current, float target, float seconds)
    {
        if (seconds <= 0f)
            return current;
        float t = 1f - (float)Math.Exp(-seconds / EaseSeconds * 3.0);
        float next = current + (target - current) * t;
        return Math.Abs(next - target) < 0.005f ? target : next;
    }
}
