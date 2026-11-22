using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV418) D329: the drop key puts the held item down where the crosshair is. tg drops at the feet (the drop_item keybinding,
// dropItemToGround) and places on a table at the clicked pixel (table_place_act); in first person the crosshair names the point: on a
// table in reach it is tg's own placing with that point as the click's pixel, on a free floor in reach the item is put there (tg's
// transfer_item_to_turf with the point's pixel offset, as the table's), anywhere else it is tg's drop at the feet.

public static partial class ClientSeam
{
    /// <summary>
    /// Version 82 (D329): the player puts what the active hand holds down at the crosshair's point on <paramref name="target"/>
    /// (<paramref name="params"/>: a left click's with the camera's aim, <see cref="AimParam"/>). Detached, as BYOND starts a verb.
    /// </summary>
    public static void PutDown(client player, atom target, string @params)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (player.mob is not mob_living user || iscyborg(user) || target == null || target.gc_destroyed != null)
        {
            PressKeybinding(player, typeof(datum_keybinding_mob_drop_item));
            return;
        }
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = user;
            Waiting.Detach(PutDownAt(player, user, target, Dm.params2list(@params)));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    private static async DmTask PutDownAt(client player, mob_living user, atom target, DmList modifiers)
    {
        if (user.get_active_held_item() is obj_item item && user.loc is turf && get_turf(target) is turf spot && !HAS_TRAIT(user, TRAIT_HANDS_BLOCKED)
            && TryReadAim(modifiers, out double east, out double north, out double up, out double yaw, out double pitch) && Dm.truthy(await spot.Adjacent(user)))
        {
            obj_structure_table table = Dm.locate<obj_structure_table>(spot);
            if (table != null && !Dm.truthy(table.is_flipped))
            {
                // tg's placing on a table, its click's pixel the crosshair's point on the top
                (int ox, int oy) = PixelOffsetOn(spot, east, north, up, yaw, pitch, FlightSeam.TableTopPixels);
                modifiers[ICON_X] = (ox + 16).ToString(System.Globalization.CultureInfo.InvariantCulture);
                modifiers[ICON_Y] = (oy + 16).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await table.table_place_act(user, item, modifiers);
                return;
            }
            if (table == null && !Dm.truthy(spot.density) && !Dm.truthy(await spot.is_blocked_turf(exclude_mobs: TRUE, source_atom: item)))
            {
                (int ox, int oy) = PixelOffsetOn(spot, east, north, up, yaw, pitch, 0);
                await user.transfer_item_to_turf(item, spot, ox, oy);
                return;
            }
        }
        PressKeybinding(player, typeof(datum_keybinding_mob_drop_item));
    }

    /// <summary>
    /// Where the crosshair's ray (DV94's aim, east and north in tile pixels) meets the plane <paramref name="lift"/> tile pixels over
    /// <paramref name="spot"/>'s floor, as a pixel offset from the tile's middle (tg's 32 a tile, within half a tile); the middle when the
    /// ray does not come down to it.
    /// </summary>
    internal static (int X, int Y) PixelOffsetOn(turf spot, double east, double north, double up, double yawDegrees, double pitchDegrees, double lift)
    {
        if (pitchDegrees >= -1 || up <= lift)
            return (0, 0);
        double along = (up - lift) / Math.Tan(-pitchDegrees * Math.PI / 180);
        double yaw = yawDegrees * Math.PI / 180;
        double x = east + Math.Sin(yaw) * along - Dm.num(spot.x) * FlightArc.PixelsPerTile;
        double y = north + Math.Cos(yaw) * along - Dm.num(spot.y) * FlightArc.PixelsPerTile;
        const int half = 16; // tg: a table's placing clamps within half an icon
        return ((int)Math.Clamp(Math.Round(x), -half, half), (int)Math.Clamp(Math.Round(y), -half, half));
    }
}
