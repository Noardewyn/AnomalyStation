// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/areabound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole, for the museum's piggy bank (game/objects/items/piggy_bank.cs). A movable with this component is
// put back on the turf it started on whenever it leaves the area it started in. The var block is generated (D111).

/// Movables with this component will automatically return to their original turf if moved outside their initial area
// tg: code/datums/components/areabound.dm:2
public partial class datum_component_areabound : datum_component
{
    // tg: code/datums/components/areabound.dm:8
    [DmParams()]
    public override DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return DmTask<object>.FromResult(COMPONENT_INCOMPATIBLE);
        bound_area = get_area(parent) as area;
        reset_turf = get_turf(parent) as turf;
        move_tracker = Dm.@new<datum_movement_detector>(parent, Dm.@new<datum_callback>(this, nameof(check_bounds)));
        return DmTask<object>.FromResult(null);
    }

    // tg: code/datums/components/areabound.dm:15
    public virtual async DmTask check_bounds()
    {
        atom_movable AM = parent as atom_movable;
        area current = get_area(AM) as area;
        if (current != bound_area)
        {
            if (reset_turf == null || reset_turf.loc != bound_area)
            {
                stack_trace("Invalid areabound configuration", "code/datums/components/areabound.dm", 20); //qdel(src)
                return;
            }
            if (Dm.truthy(moving))
            {
                stack_trace(Dm.text("Moved during a reset move, giving up to prevent infinite recursion. Turf: [] at [], [], []", reset_turf.type, reset_turf.x, reset_turf.y, reset_turf.z), "code/datums/components/areabound.dm", 23);
                return;
            }
            moving = TRUE;
            await AM.forceMove(reset_turf);
            moving = FALSE;
        }
    }

    // tg: code/datums/components/areabound.dm:29
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref move_tracker);
        object dot = base.Destroy(force);
        return dot;
    }
}
