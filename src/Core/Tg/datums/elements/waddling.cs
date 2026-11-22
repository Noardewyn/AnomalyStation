// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/waddling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/datums/elements/waddling.dm:3
public partial class datum_element_waddling : datum_element
{
    // tg: code/datums/elements/waddling.dm:3
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        if (!HAS_TRAIT(target, TRAIT_WADDLING))
            stack_trace(Dm.text("[] added to [] without adding TRAIT_WADDLING first. Please use AddElementTrait instead.", type, target), "code/datums/elements/waddling.dm", 9);
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(Waddle));
        return dot;
    }

    // tg: code/datums/elements/waddling.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    // tg: code/datums/elements/waddling.dm:15
    [SignalHandler]
    public virtual void Waddle(atom_movable moved = null, atom oldloc = null, object direction = null, object forced = null)
    {
        if (Dm.truthy(forced) || Dm.truthy(CHECK_MOVE_LOOP_FLAGS(moved, MOVEMENT_LOOP_OUTSIDE_CONTROL)))
            return;
        if (isliving(moved))
        {
            mob_living living_moved = moved as mob_living;
            if (living_moved.buckled != null || Dm.truthy(living_moved.incapacitated) || (Dm.eq(living_moved.body_position, LYING_DOWN) && !HAS_TRAIT(living_moved, TRAIT_FLOPPING)))
                return;
            if (!Dm.eq(living_moved.body_position, LYING_DOWN) && HAS_TRAIT(living_moved, TRAIT_FLOOR_FLOPPING))
                return;
        }
        waddling_animation(moved);
    }

    // tg: code/datums/elements/waddling.dm:27
    public virtual void waddling_animation(atom_movable target = null)
    {
        object prev_pixel_z = target.pixel_z;
        Dm.animate(target, Dm.named("pixel_z", Dm.num(target.pixel_z) + 4), Dm.named("time", 0));
        object prev_transform = target.transform;
        Dm.animate(Dm.named("pixel_z", prev_pixel_z), Dm.named("transform", ((matrix)target.transform).Turn(Dm.num(pick(-12, 0, 12)))), Dm.named("time", 2));
        Dm.animate(Dm.named("transform", prev_transform), Dm.named("time", 0));
    }
}
