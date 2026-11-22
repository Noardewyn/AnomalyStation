using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// M4v slice 4 (protocol version 11, D143). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// How a HUD picture shows a screen object's <c>animate()</c> look (<see cref="AnimatedLook"/>, AnimationPlayback): BYOND draws a screen
/// object's pixel offsets, transform, colour and alpha as it draws an atom's, on the screen's plane (tg's alert sliding in from 32 pixels
/// to the right, <c>alert.dm:66</c>; a fullscreen fading out, <c>fullscreen.dm:31</c>). The HUD control moves by the offsets and the
/// transform's translation (screen pixels, y down), turns and scales by the transform's linear part about the icon square's centre (a
/// shear is not drawn: a control has none), and takes the colour and alpha as its modulate.
/// </summary>
public readonly record struct HudLook(float MoveX, float MoveY, float Rotation, float ScaleX, float ScaleY, float Red, float Green, float Blue, float Alpha)
{
    /// <summary>No change.</summary>
    public static HudLook None => new(0, 0, 0, 1, 1, 1, 1, 1, 1);

    /// <summary>
    /// The look of screen entry <paramref name="entry"/>'s animate() chains at <paramref name="tick"/>, played against its appearance and taken
    /// over its picture composed with the root opaque (the HUD composes it so while a chain plays: a fullscreen's fade ends at alpha 0, which
    /// the appearance already holds); false when no chain plays now (none, or every track ended: the appearance shows). True also when the
    /// look is no change over the opaque picture: a chain that has not started yet shows its start values (BYOND draws the vars before the
    /// call until it starts), which for a fade from opaque is the opaque picture, not the appearance's end alpha (the live check of
    /// 2026-10-14: a one-frame blink before a fullscreen's fade).
    /// </summary>
    public static bool TryPlay(ReplicaWorld replica, in ScreenEntry entry, double tick, out AnimatedLook look)
    {
        look = AnimatedLook.None;
        return entry.Animation != null && replica.TryGetAppearance(entry.Appearance, out AppearanceEntry root)
            && AnimationPlayback.TryEvaluate(entry.Animation, tick, Anomaly.Core.Motion.MotionMath.TicksPerSecond, AnimatedBase.Of(root), out look, composedAlpha: 255);
    }

    /// <summary>
    /// The control's look for <paramref name="look"/> at <paramref name="pixelScale"/> screen pixels per icon pixel. BYOND's y runs up and the
    /// screen's down; <c>Turn</c> is clockwise on the screen, as a control's positive rotation is.
    /// </summary>
    public static HudLook Of(in AnimatedLook look, float pixelScale)
    {
        Affine t = look.Transform;
        double scaleX = System.Math.Sqrt(t.A * t.A + t.D * t.D);
        double rotation = scaleX > 1e-9 ? -System.Math.Atan2(t.D, t.A) : 0;
        double scaleY = scaleX > 1e-9 ? (t.A * t.E - t.B * t.D) / scaleX : System.Math.Sqrt(t.B * t.B + t.E * t.E);
        return new HudLook(
            (float)((look.PixelX + look.PixelW + t.C) * pixelScale),
            (float)(-(look.PixelY + look.PixelZ + t.F) * pixelScale),
            (float)rotation, (float)scaleX, (float)scaleY,
            (float)look.Red, (float)look.Green, (float)look.Blue, (float)System.Math.Clamp(look.Alpha, 0, 1));
    }
}
