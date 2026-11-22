// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/turfs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only get_turf, get_area, Z_TURFS (M3 P4: smooth_zlevel), ALL_TURFS (M4b slice 1: SSair's setup_allturfs) and
// RANGE_TURFS / RECT_TURFS (M4c slice 8: connect_range and the proximity fields) translated (the function-like macros over the map
// builtins; rules 11.1: A is used only as an argument, so object), ADD_TURF_TRANSPARENCY and REMOVE_TURF_TRANSPARENCY (D325, multi-z), TURF_MIDPOINT (gaps E2c).
// The constants of turfs.dm are generated into Defines;
// its other function-like macros (IS_OPAQUE_TURF, TURF_FROM_COORDS_LIST, the CORNER_* helpers, the
// transparency macros) come with their first users.

public static partial class GlobalProcs
{
    ///Returns a list of turf in a square
    // tg: code/__DEFINES/turfs.dm:16  #define RANGE_TURFS(RADIUS, CENTER) RECT_TURFS(RADIUS, RADIUS, CENTER)
    public static DmList RANGE_TURFS(object RADIUS, atom CENTER) => RECT_TURFS(RADIUS, RADIUS, CENTER);

    // tg: code/__DEFINES/turfs.dm:19
    public static DmList RECT_TURFS(object H_RADIUS, object V_RADIUS, atom CENTER)
    {
        return block(
            CENTER.x - Dm.num(H_RADIUS), CENTER.y - Dm.num(V_RADIUS), CENTER.z,
            CENTER.x + Dm.num(H_RADIUS), CENTER.y + Dm.num(V_RADIUS), CENTER.z
        );
    }

    // tg: code/__DEFINES/turfs.dm:117  #define TURF_MIDPOINT(a, b) (locate(((a.x + b.x) * 0.5), (a.y + b.y) * 0.5, (a.z + b.z) * 0.5))
    // (gaps E2c: the earthquake's fault line)
    public static turf TURF_MIDPOINT(atom a, atom b) => locate((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, (a.z + b.z) * 0.5f);

    // tg: code/__DEFINES/turfs.dm:26
    public static DmList Z_TURFS(float ZLEVEL)
    {
        return block(1, 1, ZLEVEL, world.maxx, world.maxy, ZLEVEL);
    }

    // tg: code/__DEFINES/turfs.dm:  #define CORNER_BLOCK(corner, width, height) CORNER_BLOCK_OFFSET(corner, width, height, 0, 0)
    public static DmList CORNER_BLOCK(atom corner, float width, float height) => CORNER_BLOCK_OFFSET(corner, width, height, 0, 0);

    /// Returns a list of turfs similar to CORNER_BLOCK but with offsets
    // tg: code/__DEFINES/turfs.dm  #define CORNER_BLOCK_OFFSET(corner, width, height, offset_x, offset_y)
    public static DmList CORNER_BLOCK_OFFSET(atom corner, float width, float height, float offset_x, float offset_y)
        => block(corner.x + offset_x, corner.y + offset_y, corner.z, corner.x + (width - 1) + offset_x, corner.y + (height - 1) + offset_y, corner.z);

    /// Returns an outline (neighboring turfs) of the given block
    // tg: code/__DEFINES/turfs.dm  #define CORNER_OUTLINE(corner, width, height)
    public static DmList CORNER_OUTLINE(atom corner, float width, float height)
    {
        DmList outline = CORNER_BLOCK_OFFSET(corner, width + 2, 1, -1, -1);
        Dm.append(ref outline, CORNER_BLOCK_OFFSET(corner, width + 2, 1, -1, height));
        Dm.append(ref outline, CORNER_BLOCK_OFFSET(corner, 1, height, -1, 0));
        Dm.append(ref outline, CORNER_BLOCK_OFFSET(corner, 1, height, width, 0));
        return outline;
    }

    /// Makes the set turf transparent
    // tg: code/__DEFINES/turfs.dm:132  #define ADD_TURF_TRANSPARENCY(modturf, source) (D325)
    public static void ADD_TURF_TRANSPARENCY(turf modturf, object source)
    {
        if (!HAS_TRAIT(modturf, TURF_Z_TRANSPARENT_TRAIT))
            Waiting.NoSleep(modturf.AddElement(typeof(datum_element_turf_z_transparency)));
        ADD_TRAIT(modturf, TURF_Z_TRANSPARENT_TRAIT, source);
    }

    /// Removes the transparency from the set turf
    // tg: code/__DEFINES/turfs.dm:137  #define REMOVE_TURF_TRANSPARENCY(modturf, source) (D325)
    public static void REMOVE_TURF_TRANSPARENCY(turf modturf, object source)
    {
        REMOVE_TRAIT(modturf, TURF_Z_TRANSPARENT_TRAIT, source);
        if (!HAS_TRAIT(modturf, TURF_Z_TRANSPARENT_TRAIT))
            modturf.RemoveElement(typeof(datum_element_turf_z_transparency));
    }

    // tg: code/__DEFINES/turfs.dm:29  #define ALL_TURFS(...) block(1, 1, 1, world.maxx, world.maxy, world.maxz)
    public static DmList ALL_TURFS() => block(1, 1, 1, world.maxx, world.maxy, world.maxz);

    /**
     * Get the turf that `A` resides in, regardless of any containers.
     *
     * Use in favor of `A.loc` or `src.loc` so that things work correctly when
     * stored inside an inventory, locker, or other container.
     */
    // tg: code/__DEFINES/turfs.dm:79
    public static turf get_turf(object A)
    {
        return get_step(A, 0);
    }

    /**
     * Get the ultimate area of `A`, similarly to [get_turf].
     *
     * Use instead of `A.loc.loc`.
     */
    // tg: code/__DEFINES/turfs.dm:86
    public static object get_area(object A)
    {
        return (isarea(A) ? A : (object)get_step(A, 0)?.loc);
    }
}
