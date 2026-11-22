using System.Linq;
using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 34 (M7x slice A, D177, docs/ai/specs/protocol.md section 43): BYOND's MouseDown, MouseDrag and MouseUp reach tg's
// /client procs (code/_onclick/drag_drop.dm:129, :143, :173), which tg's full auto listens to; and the camera's aim rides tg's click
// params as one port key (DV94), so it reaches aim_projectile through tg's own chain (fire_gun, process_fire, fire_casing, throw_proj).

public static partial class ClientSeam
{
    /// <summary>
    /// DEVIATION(DV94): the click params key of the shot's 3D aim, <c>anomaly-aim=EAST,NORTH,UP,YAW,PITCH</c>: the eye's point in absolute tile
    /// pixels (32 to a tile, a tile's centre at 32 times its x; the height above the level's floor in the same pixels) and the crosshair's
    /// direction in degrees (the yaw clockwise from north as tg's projectile angle, the pitch up). tg's params never hold it.
    /// </summary>
    public const string AIM_PARAM = "anomaly-aim";

    /// <summary>
    /// The <see cref="AIM_PARAM"/> pair (no separator) for an eye at (<paramref name="east"/>, <paramref name="north"/>, <paramref name="up"/>)
    /// tile pixels aiming along <paramref name="yawDegrees"/> and <paramref name="pitchDegrees"/>; invariant culture, four decimals.
    /// </summary>
    public static string AimParam(double east, double north, double up, double yawDegrees, double pitchDegrees)
        => AIM_PARAM + "=" + string.Join(",", new[] { east, north, up, yawDegrees, pitchDegrees }.Select(v => Math.Round(v, 4).ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// DEVIATION(DV527): the click params key the server adds beside <see cref="AIM_PARAM"/>, <c>anomaly-rewind=TICKS</c>: how many ticks behind its
    /// world the client drew the others when it aimed (the click's seen tick, protocol 88), up to <see cref="ShotRewind.MaxTicks"/>.
    /// </summary>
    public const string REWIND_PARAM = "anomaly-rewind";

    /// <summary>The <see cref="REWIND_PARAM"/> pair for <paramref name="ticks"/> (two decimals).</summary>
    public static string RewindParam(double ticks) => REWIND_PARAM + "=" + Math.Round(ticks, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>The click's <see cref="REWIND_PARAM"/> within 0 to <see cref="ShotRewind.MaxTicks"/>; 0 when there is none.</summary>
    public static float ReadRewind(DmList modifiers)
        => modifiers?[REWIND_PARAM] is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double ticks) && double.IsFinite(ticks)
            ? (float)Math.Clamp(ticks, 0, ShotRewind.MaxTicks) : 0f;

    /// <summary>Whether the click's modifiers hold a camera's aim (<see cref="AIM_PARAM"/>): the click of a 3D client on the world.</summary>
    public static bool HasAim(DmList modifiers) => modifiers != null && modifiers[AIM_PARAM] is string;

    /// <summary>
    /// Reads <see cref="AIM_PARAM"/> from tg's modifiers list (<c>params2list</c> of the click params); false when the key is absent or does not
    /// hold five finite numbers with the pitch within a quarter turn.
    /// </summary>
    public static bool TryReadAim(DmList modifiers, out double east, out double north, out double up, out double yawDegrees, out double pitchDegrees)
    {
        east = north = up = yawDegrees = pitchDegrees = 0;
        if (modifiers == null || modifiers[AIM_PARAM] is not string text)
            return false;
        string[] parts = text.Split(',');
        if (parts.Length != 5)
            return false;
        var values = new double[5];
        for (int i = 0; i < 5; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
                return false;
        if (Math.Abs(values[4]) > 90)
            return false;
        (east, north, up, yawDegrees, pitchDegrees) = (values[0], values[1], values[2], values[3], values[4]);
        return true;
    }

    /// <summary>
    /// Version 34: a button went down over <paramref name="target"/> (null: nothing under the crosshair, which BYOND never gives a map
    /// MouseDown for, so none runs): tg's <c>/client/MouseDown(object, location, control, params)</c> with <c>usr</c> the client's mob, the
    /// location rule of <see cref="Click"/> and the map's control. Detached: tg's MouseDown may loop an autoclick (<c>CanMobAutoclick</c>).
    /// The pressed thing is kept for the drags (BYOND's <c>src_object</c>).
    /// </summary>
    public static void MouseDown(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        player.dm_mouse_down = player.dm_mouse_held_over = target;
        if (target == null || target.gc_destroyed != null)
            return;
        WithUsr(player, () => Waiting.Detach(player.MouseDown(target, LocationOf(player, target), Defines.SKIN_MAPWINDOW_MAP, @params)));
    }

    /// <summary>
    /// Version 34: the mouse moved with a button held: tg's <c>/client/MouseDrag(src_object, over_object, src_location, over_location,
    /// src_control, over_control, params)</c> with the pressed thing as <c>src_object</c> and <paramref name="over"/> (null over nothing) as
    /// <c>over_object</c>. Detached (its parent's default is BYOND's MouseDrag of the src_object, which tg's atoms may override).
    /// </summary>
    public static void MouseDrag(client player, atom over, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        atom pressed = player.dm_mouse_down is atom p && p.gc_destroyed == null ? p : null;
        if (over != null && over.gc_destroyed != null)
            over = null;
        player.dm_mouse_held_over = over;
        WithUsr(player, () => Waiting.Detach(player.MouseDrag(pressed, over, pressed == null ? null : LocationOf(player, pressed), over == null ? null : LocationOf(player, over),
            Defines.SKIN_MAPWINDOW_MAP, Defines.SKIN_MAPWINDOW_MAP, @params)));
    }

    /// <summary>
    /// Version 34: the button went up over <paramref name="target"/> (null over nothing): tg's <c>/client/MouseUp(object, location, control,
    /// params)</c>. Not detached: tg's MouseUp does not sleep. The press's drags end here: BYOND would call MouseDrop on a release after a
    /// drag, but a held trigger is no drag and drop (the cursor mode's Drop is, DV45), so only tg's MouseDrop's own bookkeeping
    /// (<c>drag_start</c>, <c>drag_details</c>, drag_drop.dm:193) is reset.
    /// </summary>
    public static void MouseUp(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (target != null && target.gc_destroyed != null)
            target = null;
        player.dm_mouse_down = player.dm_mouse_held_over = null;
        WithUsr(player, () => player.MouseUp(target, target == null ? null : LocationOf(player, target), Defines.SKIN_MAPWINDOW_MAP, @params));
        player.drag_start = 0;
        player.drag_details = null;
    }

    /// <summary>
    /// Version 70: the wheel turned <paramref name="deltaY"/> (BYOND's notches of 120, above 0 up) over <paramref name="target"/>: BYOND's
    /// <c>/client/MouseWheel(object, delta_x, delta_y, location, control, params)</c>, whose default is the object's <c>/atom/MouseWheel</c>
    /// (tg's <c>usr.MouseWheelOn</c>, which the RPD listens to). Over nothing the mob's own turf is the object: BYOND's map always has a turf
    /// under the mouse.
    /// </summary>
    public static void MouseWheel(client player, atom target, int deltaY, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (target == null || target.gc_destroyed != null)
            target = GlobalProcs.get_turf(player.mob);
        if (target == null)
            return;
        WithUsr(player, () => target.MouseWheel(0, deltaY, LocationOf(player, target), Defines.SKIN_MAPWINDOW_MAP, @params));
    }

    private static void WithUsr(client player, Action call)
    {
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            call();
        }
        finally
        {
            world.usr = caller_usr;
        }
    }
}
