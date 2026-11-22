namespace Anomaly.Protocol;

// Version 21 (M5x slice 2, protocol.md section 30): the camera shake. tg's shake_camera (mob_helpers.dm:164) animates the player's
// BYOND client's pixel_x and pixel_y along a random path inside a box of the shake's strength (the view jerks and comes back); the
// server sends the path of the tick's last animate chain on the player's client as one CameraShake message, after the tick's Delta,
// and the client moves the first-person eye along it (D70: off with the camera shake switch).

/// <summary>One keyframe of a <see cref="CameraShakeMessage"/>: where the view goes, in BYOND pixels, and in how long.</summary>
/// <param name="PixelX">The client's <c>pixel_x</c> at the end of the step (the view east of the eye, pixels; 32 to a tile).</param>
/// <param name="PixelY">The client's <c>pixel_y</c> at the end of the step (the view north, pixels).</param>
/// <param name="Time">The step's length in deciseconds (BYOND's <c>animate</c> <c>time</c>, at least 1), eased linearly.</param>
public readonly record struct CameraShakeStep(int PixelX, int PixelY, int Time);

/// <summary>
/// The view's shake path for one session (server to client, State channel): BYOND <c>animate()</c> steps on the player's client
/// (<c>pixel_x</c>, <c>pixel_y</c>, <c>time</c>). A message replaces the path running on the client, as a new animate chain replaces
/// the old one on the same object; the steps run in order from the client's own view position at the message's arrival.
/// </summary>
public sealed partial class CameraShakeMessage
{
    /// <summary>The steps, 1 to <see cref="Limits.MaxCameraShakeSteps"/>.</summary>
    public List<CameraShakeStep> Steps { get; } = new();
}
