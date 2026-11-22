// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/aux_base.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice G3, D272): the auxiliary base's mobile docking port, which Meta's aux base zone loads at the round start, and its
// management console's Initialize and Destroy (2026-11-14: the construction console's drone starts at it, and the turrets placed in the base
// register with it). The console's launch window, the landing designator and the mining shuttle beacon (the aux base's deployment to
// lavaland) come with mining (after M10; live-partial.txt).

// tg: code/modules/mining/aux_base.dm:14
public partial class obj_machinery_computer_auxiliary_base : obj_machinery_computer
{
    // tg: code/modules/mining/aux_base.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "NT_AUX"));
        return dot;
    }

    // tg: code/modules/mining/aux_base.dm:45
    public override object Destroy(float? force = null) // Shouldn't be destroyable... but just in case
    {
        LAZYCLEARLIST(turrets);
        return base.Destroy(force);
    }
}

// tg: code/modules/mining/aux_base.dm:312
[DmPath("/obj/docking_port/mobile/auxiliary_base")]
public partial class obj_docking_port_mobile_auxiliary_base : obj_docking_port_mobile
{
    // tg: code/modules/mining/aux_base.dm:316
    public override async DmTask takeoff(DmList old_turfs = null, DmList new_turfs = null, DmList moved_atoms = null, object rotation = null, object movement_direction = null, obj_docking_port_stationary old_dock = null, area fallback_area = null)
    {
        foreach (turf place in Dm.each<turf>(new_turfs.Copy()))
        {
            if (place is turf_closed_mineral) // ismineralturf (__DEFINES/is_helpers.dm)
                await place.ScrapeAway();
        }
        await base.takeoff(old_turfs, new_turfs, moved_atoms, rotation, movement_direction, old_dock, fallback_area);
    }
}
