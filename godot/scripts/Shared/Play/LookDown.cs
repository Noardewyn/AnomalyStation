namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// UI overhaul slice T5a (docs/ai/specs/ui-overhaul.md section 8, the corrected T5 design):
// looking down at one's own body in first person. The eye turns about the neck, so looking down moves it forward and down; the pitch goes
// past straight down so the view looks back at the body, one continuous turn about the camera's own right axis. Past 90 degrees the
// camera's forward points backward on the ground, so the way the player faces is taken from the view's right (the yaw), never from its forward.

/// <summary>The first-person eye looking down at its own body: the eye's place for a pitch, the pitch limits, and when the body is seen.</summary>
public static class LookDown
{
    /// <summary>How far under a standing human's eye (at a level look) the point it turns about is, metres: the neck.</summary>
    public const double PivotMetres = 0.25;

    /// <summary>How far the first-person view turns down, degrees below level: past straight down, back at the own body.</summary>
    public const double MaxDownDegrees = 135;

    /// <summary>How far the view turns up, degrees above level; and how far the other cameras (free, orbit, the map editor's) turn down.</summary>
    public const double MaxUpDegrees = 89;

    /// <summary>
    /// After the own body may first show (<see cref="BodyFrom"/>), the held things go down out of the view over this many degrees of pitch, as
    /// the body rises from the bottom of the screen.
    /// </summary>
    private const double HeldLowerDegrees = 15;

    private const double Degree = Math.PI / 180;

    /// <summary>
    /// <paramref name="pitch"/> (radians, up positive) within the limits: <see cref="MaxDownDegrees"/> down in the <paramref name="firstPerson"/>
    /// view, <see cref="MaxUpDegrees"/> down in the other cameras; <see cref="MaxUpDegrees"/> up in all.
    /// </summary>
    public static double ClampPitch(double pitch, bool firstPerson)
        => Math.Clamp(pitch, -(firstPerson ? MaxDownDegrees : MaxUpDegrees) * Degree, MaxUpDegrees * Degree);

    /// <summary>
    /// How far under an eye <paramref name="eyeMetres"/> above the feet its pivot is: <see cref="PivotMetres"/> for a standing human
    /// (<see cref="WorldScale.DefaultEyeHeight"/>), in proportion for a lower eye (lying, a small body), never more (a raised eye-height option),
    /// so the eye never turns under the floor.
    /// </summary>
    public static double Pivot(double eyeMetres) => PivotMetres * Math.Clamp(eyeMetres / WorldScale.DefaultEyeHeight, 0, 1);

    /// <summary>
    /// Where the eye <paramref name="eyeMetres"/> above the feet is at <paramref name="pitch"/> (radians, up positive) from where it is at a level
    /// look: <paramref name="pitch"/> below level it has turned about the point <see cref="Pivot"/> under it, so it is that much times the sine
    /// forward along the view's yaw and times one less the cosine down. Looking up it stays where it is.
    /// </summary>
    public static (double Forward, double Down) EyeOffset(double pitch, double eyeMetres)
    {
        if (pitch >= 0)
            return (0, 0);
        double pivot = Pivot(eyeMetres);
        return (pivot * Math.Sin(-pitch), pivot * (1 - Math.Cos(-pitch)));
    }

    /// <summary>
    /// The pitch below level (radians, positive) from which the own body is drawn for its own eye, with a vertical field of view of
    /// <paramref name="fovDegrees"/> and the camera's near plane <paramref name="nearMetres"/> away. The body is a card standing across the view
    /// where the player stands, and the eye (<paramref name="eyeMetres"/> above the feet) is its <see cref="Pivot"/> times the sine of the pitch
    /// ahead of it. Nothing of the card is in the view until the lowest edge of the view looks back past straight down (90 degrees less half the
    /// field), so from there it rises in from the bottom edge, never popping into the middle; and the eye is farther ahead of it than the near
    /// plane reaches back at any pitch (the near plane's farthest point back is its distance over the cosine of half the field), so nothing of it
    /// is cut by the near plane. Infinity: never (a pivot too short to clear the near plane: a small or a lying body).
    /// </summary>
    public static double BodyFrom(double fovDegrees, double nearMetres, double eyeMetres)
    {
        double half = fovDegrees / 2 * Degree;
        double reach = nearMetres / (Pivot(eyeMetres) * Math.Cos(half));
        if (!(reach < 1))
            return double.PositiveInfinity;
        return Math.Max(Math.PI / 2 - half, Math.Asin(reach));
    }

    /// <summary>
    /// Whether the own body is drawn for its own eye this frame: the first-person view of a body standing on a turf (not inside a locker or
    /// another thing), the player's own (not a ghost, not seeing through another's eyes, not in an exosuit), standing and moving (not lying, not
    /// limp: unconscious or dead), not looking down a scope, looking down at <paramref name="pitch"/> (radians, up positive) at least
    /// <see cref="BodyFrom"/>. Otherwise only its shadow is, as before (D288).
    /// </summary>
    public static bool ShowsOwnBody(double pitch, double fovDegrees, double nearMetres, double eyeMetres, bool firstPerson, bool onTurf, bool ghost,
        bool observing, bool lying, bool limp, bool exosuit, bool scoped)
        => CanShowOwnBody(firstPerson, onTurf, ghost, observing, lying, limp, exosuit, scoped) && -pitch >= BodyFrom(fovDegrees, nearMetres, eyeMetres);

    /// <summary>Whether looking down far enough would show the own body (<see cref="ShowsOwnBody"/> at any pitch): a held drag of oneself ends when it would not.</summary>
    public static bool CanShowOwnBody(bool firstPerson, bool onTurf, bool ghost, bool observing, bool lying, bool limp, bool exosuit, bool scoped)
        => firstPerson && onTurf && !ghost && !observing && !lying && !limp && !exosuit && !scoped;

    /// <summary>
    /// Slice T5b (the own body is a target while it is looked at, as other people are): the
    /// entity the world picks leave out: the own mob <paramref name="ownEntity"/>, except while its body is drawn for its own eye
    /// (<paramref name="bodyShown"/>, <see cref="ShowsOwnBody"/>), when it is hit on its body parts as anyone else is (0: nothing left out).
    /// </summary>
    public static long PickExclude(long ownEntity, bool bodyShown) => bodyShown ? 0 : ownEntity;

    /// <summary>
    /// How far the first-person held things are lowered out of the view (0: where they are held, 1: all the way down) while the own body is shown:
    /// none at <see cref="BodyFrom"/>, all within <see cref="HeldLowerDegrees"/> more, smoothly.
    /// </summary>
    public static float HeldLowered(double pitch, double fovDegrees, double nearMetres, double eyeMetres)
    {
        double t = Math.Clamp((-pitch - BodyFrom(fovDegrees, nearMetres, eyeMetres)) / (HeldLowerDegrees * Degree), 0, 1);
        return (float)(t * t * (3 - 2 * t));
    }

    /// <summary>
    /// The way the view faces on the ground (unit, Godot's frame: x east, z south) from the camera's right (<paramref name="rightX"/>,
    /// <paramref name="rightZ"/>): up x right. The pitch turns about the right, so the right stays the player's right at every pitch, where
    /// the forward's own ground part turns back past straight down.
    /// </summary>
    public static (double X, double Z) Ahead(double rightX, double rightZ)
    {
        double length = Math.Sqrt(rightX * rightX + rightZ * rightZ);
        return length < 1e-9 ? (0, -1) : (rightZ / length, -rightX / length);
    }

    /// <summary>The compass bearing the view faces (radians clockwise from north) from the camera's right (<see cref="Ahead"/>).</summary>
    public static double CompassYaw(double rightX, double rightZ)
    {
        (double x, double z) = Ahead(rightX, rightZ);
        return Math.Atan2(x, -z);
    }

    /// <summary>
    /// The view's pitch (radians, up positive) from its forward (<paramref name="forwardX"/>, <paramref name="forwardY"/>, <paramref name="forwardZ"/>)
    /// and its right, measured from the way it faces (<see cref="Ahead"/>): it goes on past -90 degrees as the view turns on, where the
    /// arcsine of the forward's height would turn back.
    /// </summary>
    public static double Pitch(double forwardX, double forwardY, double forwardZ, double rightX, double rightZ)
    {
        (double x, double z) = Ahead(rightX, rightZ);
        return Math.Atan2(forwardY, forwardX * x + forwardZ * z);
    }
}
