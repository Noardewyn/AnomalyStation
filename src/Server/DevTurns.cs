using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Server;

/// <summary>
/// The dev commands <c>rotate</c> and <c>mount</c> (DV56): a thing turned, a wall mount hung on another wall, for the round. What a map keeps is
/// turned and hung in the map editor, which writes the map's own file (docs/ai/specs/map-editor.md 5.14).
/// </summary>
public static class DevTurns
{
    /// <summary>The BYOND dirs a thing can face: north, east, south, west, the order <c>rotate</c> turns them clockwise.</summary>
    public static readonly int[] Clockwise = { (int)NORTH, (int)EAST, (int)SOUTH, (int)WEST };

    /// <summary>
    /// Hangs a wall mount somewhere else. tg's <c>atom_mounted</c> takes any move of a mounted thing for being torn off its wall and deconstructs it (<c>on_move</c>), so the
    /// component goes first and the thing mounts again, on its new wall, as it did at Initialize.
    /// </summary>
    public static void Mount(obj thing, turf to, int pixelX, int pixelY, int dir)
    {
        datum mounted = thing.GetComponent(typeof(datum_component_atom_mounted)) as datum;
        if (mounted != null)
            qdel(mounted);
        Waiting.NoSleep(thing.forceMove(to));
        Waiting.NoSleep(thing.setDir((float)dir));
        thing.pixel_x = pixelX;
        thing.pixel_y = pixelY;
        if (mounted != null)
            Waiting.NoSleep(thing.find_and_mount_on_atom());
    }

    /// <summary>The next way a thing faces when it is turned a quarter: clockwise (seen from above) or counter-clockwise; a diagonal or 0 turns from north.</summary>
    public static int Turn(int dir, bool clockwise)
    {
        int i = Array.IndexOf(Clockwise, dir);
        if (i < 0)
            return (int)(clockwise ? EAST : WEST);
        return Clockwise[(i + (clockwise ? 1 : 3)) % 4];
    }
}
