// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/bed_tucking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316: the bedsheets and the plushes). The var block is generated (D111).

/// Tucking element, for things that can be tucked into bed.
// tg: code/datums/elements/bed_tucking.dm:2
public partial class datum_element_bed_tuckable : datum_element
{
    // tg: code/datums/elements/bed_tucking.dm:14
    [DmParams("target", "mapload", "x", "y", "rotation")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        object mapload = Dm.arg(__extra, 0) ?? FALSE;
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        x_offset = Dm.num(Dm.arg(__extra, 1) ?? 0);
        y_offset = Dm.num(Dm.arg(__extra, 2) ?? 0);
        starting_angle = Dm.num(Dm.arg(__extra, 3) ?? 0);
        RegisterSignal(target, COMSIG_ITEM_ATTACK_ATOM, nameof(tuck_into_bed));
        if (!Dm.truthy(mapload))
            return dot;
        turf our_home = get_turf((atom)target);
        obj_structure_bed eepy = Dm.locate<obj_structure_bed>(our_home);
        if (eepy == null)
            return dot;
        tuck((obj_item)target, eepy);
        return dot;
    }

    // tg: code/datums/elements/bed_tucking.dm:30
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_ITEM_ATTACK_ATOM, COMSIG_ITEM_PICKUP));
        return dot;
    }

    /**
     * Tuck our object into bed.
     *
     * tucked - the object being tucked
     * target_bed - the bed we're tucking them into
     * tucker - the guy doing the tucking
     */
    // tg: code/datums/elements/bed_tucking.dm:41
    [SignalHandler]
    public virtual object tuck_into_bed(obj_item tucked = null, atom target_bed = null, mob_living tucker = null)
    {
        if (target_bed is not obj_structure_bed bed)
            return null;

        if (!Dm.truthy(Waiting.NoSleep(tucker.transfer_item_to_turf(tucked, bed.drop_location() as turf))))
            return null;

        to_chat(tucker, span_notice(I18n.T("You lay [] out on [].", tucked, bed)));
        tuck(tucked, bed);
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/elements/bed_tucking.dm:54
    public virtual void tuck(obj_item tucked = null, obj_structure_bed target_bed = null)
    {
        bool left = ((int)target_bed.dir & (int)target_bed.left_headrest_dirs) != 0;
        tucked.dir = left ? EAST : WEST;
        tucked.pixel_x = left ? -x_offset : x_offset;
        tucked.pixel_y = y_offset;
        if (Dm.truthy(starting_angle))
        {
            rotation_degree = left ? starting_angle + 180 : starting_angle;
            tucked.transform = turn(tucked.transform, rotation_degree) as matrix;
            RegisterSignal(tucked, COMSIG_ITEM_PICKUP, nameof(untuck));
        }
    }

    /**
     * If we rotate our object, then we need to un-rotate it when it's picked up
     *
     * tucked - the object that is tucked
     */
    // tg: code/datums/elements/bed_tucking.dm:68
    [SignalHandler]
    public virtual void untuck(obj_item tucked = null)
    {
        tucked.transform = turn(tucked.transform, -rotation_degree) as matrix;
        UnregisterSignal(tucked, COMSIG_ITEM_PICKUP);
    }
}
