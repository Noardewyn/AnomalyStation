// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_integrity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice H (the round-end report's station integrity): the whole file. The var block is generated (D111).

/**
 * For counting up how much of the station exists at the start of the shift, and how much is left behind by the end of it.
 */
// tg: code/datums/station_integrity.dm:10
public partial class datum_station_state : datum
{
    // tg: code/datums/station_integrity.dm:19
    public virtual void count()
    {
        floor = 0;
        wall = 0;
        r_wall = 0;
        window = 0;
        door = 0;
        grille = 0;
        mach = 0;
        foreach (object Z in Dm.each(SSmapping.levels_by_trait(ZTRAIT_STATION)))
        {
            foreach (turf T in Dm.each<turf>(Z_TURFS(Dm.num(Z))))
            {
                // don't count shuttles since they may have just left
                if (T.loc is area_shuttle)
                    continue;

                if (isfloorturf(T))
                {
                    var TF = (turf_open_floor)T;
                    if (Dm.truthy(TF.burnt))
                        floor += 1;
                    else
                        floor += 2;
                }

                if (iswallturf(T))
                    wall += 1;

                if (T is turf_closed_wall_r_wall TRW)
                {
                    if (Dm.eq(TRW.d_state, INTACT))
                        r_wall += 2;
                    else
                        r_wall += 1;
                }

                foreach (obj O in Dm.each<obj>(T.contents))
                {
                    if (O is obj_structure_window)
                        window += 1;
                    else if (O is obj_structure_grille GR)
                    {
                        if (!Dm.truthy(GR.broken))
                            grille += 1;
                    }
                    else if (O is obj_machinery_door)
                        door += 1;
                    else if (ismachinery(O))
                        mach += 1;
                    CHECK_TICK();
                }
                CHECK_TICK();
            }
            CHECK_TICK();
        }
    }

    // tg: code/datums/station_integrity.dm:68
    public virtual float score(datum_station_state result = null)
    {
        if (result == null)
            return 0;
        float output = 0;
        output += Dm.num(result.floor) / Dm.num(max(floor, 1));
        output += Dm.num(result.r_wall) / Dm.num(max(r_wall, 1));
        output += Dm.num(result.wall) / Dm.num(max(wall, 1));
        output += Dm.num(result.window) / Dm.num(max(window, 1));
        output += Dm.num(result.door) / Dm.num(max(door, 1));
        output += Dm.num(result.grille) / Dm.num(max(grille, 1));
        output += Dm.num(result.mach) / Dm.num(max(mach, 1));
        return output / 7;
    }
}
