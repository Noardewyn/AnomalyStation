// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/block_area_power_fail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the element that spares an area from the grid check's power failure (on the area, a machine or a
// mob in it, or a turf of it).

/**
 * ## block_area_power_fail
 *
 * Element that interacts with the grid check event (and similar effects) to protect certain rooms
 *
 * * Attach to an area to prevent arbitrary power outages from affecting it
 * * Attach to a movable to prevent arbitrary power outages from affecting the area it's in
 * * Attach to a turf to prevent arbitrary power outages from affecting its area
 */
// tg: code/datums/elements/block_area_power_fail.dm:15
public partial class datum_element_block_area_power_fail : datum_element
{
    // tg: code/datums/elements/block_area_power_fail.dm:2-4  the file-local AREA_TRAIT_SOURCE, MOVABLE_TRAIT_SOURCE, TURF_TRAIT_SOURCE
    private string AREA_TRAIT_SOURCE(datum the_source) => Dm.text("[]_[]", type, the_source.type);
    private string MOVABLE_TRAIT_SOURCE(datum the_source) => Dm.text("[]_[]", type, REF(the_source));
    private string TURF_TRAIT_SOURCE(turf the_source) => Dm.text("[]_[]-[]-[]", type, the_source.x, the_source.y, the_source.z);

    // tg: code/datums/elements/block_area_power_fail.dm:18
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (isarea(target)) // practically just a complicated way to do add trait
            ADD_TRAIT(target, TRAIT_AREA_BLOCK_POWER_FAIL, AREA_TRAIT_SOURCE(target));

        else if (ismovable(target)) // manages adding and removing the trait as the movable moves
        {
            var movable_target = (atom_movable)target;
            movable_target.become_area_sensitive(Dm.str(type));
            RegisterSignal(target, COMSIG_ENTER_AREA, nameof(on_movable_entered_area));
            RegisterSignal(target, COMSIG_EXIT_AREA, nameof(on_movable_exited_area));
            on_movable_entered_area(movable_target, get_area(movable_target) as area);
        }

        else if (isturf(target)) // turfs don't move so it just adds the trait to the turf's area
        {
            var turf_target = (turf)target;
            ADD_TRAIT(turf_target.loc, TRAIT_AREA_BLOCK_POWER_FAIL, TURF_TRAIT_SOURCE(turf_target));
        }

        else
            return ELEMENT_INCOMPATIBLE;
        return dot;
    }

    // tg: code/datums/elements/block_area_power_fail.dm:36
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        if (isarea(source))
            REMOVE_TRAIT(source, TRAIT_AREA_BLOCK_POWER_FAIL, AREA_TRAIT_SOURCE(source));

        else if (ismovable(source))
        {
            var movable_source = (atom_movable)source;
            movable_source.lose_area_sensitivity(Dm.str(type));
            UnregisterSignal(source, COMSIG_ENTER_AREA);
            UnregisterSignal(source, COMSIG_EXIT_AREA);
            on_movable_exited_area(movable_source, get_area(movable_source) as area);
        }

        else if (isturf(source))
        {
            var turf_source = (turf)source;
            REMOVE_TRAIT(turf_source.loc, TRAIT_AREA_BLOCK_POWER_FAIL, TURF_TRAIT_SOURCE(turf_source));
        }
        return dot;
    }

    // tg: code/datums/elements/block_area_power_fail.dm:53
    [SignalHandler]
    public virtual void on_movable_entered_area(atom_movable source = null, area new_area = null)
    {
        if (new_area != null)
            ADD_TRAIT(new_area, TRAIT_AREA_BLOCK_POWER_FAIL, MOVABLE_TRAIT_SOURCE(source));
    }

    // tg: code/datums/elements/block_area_power_fail.dm:59
    [SignalHandler]
    public virtual void on_movable_exited_area(atom_movable source = null, area old_area = null)
    {
        if (old_area != null)
            REMOVE_TRAIT(old_area, TRAIT_AREA_BLOCK_POWER_FAIL, MOVABLE_TRAIT_SOURCE(source));
    }
}
