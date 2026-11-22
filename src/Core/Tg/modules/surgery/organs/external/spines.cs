// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/spines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file. The lizard's back spines and, through its tail, the tail spines (tails.cs), drawn from tg's sprites.

///A lizards spines (those things on their back), but also including tail spines (gasp)
// tg: code/modules/surgery/organs/external/spines.dm:2
public partial class obj_item_organ_spines : obj_item_organ
{
    // tg: code/modules/surgery/organs/external/spines.dm:17
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        // If we have a tail, attempt to add a tail spines overlay
        obj_item_organ_tail our_tail = receiver.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (our_tail != null)
            await our_tail.try_insert_tail_spines(our_tail.bodypart_owner);
        return await base.on_mob_insert(receiver, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/external/spines.dm:23
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        // If we have a tail, remove any tail spines overlay
        obj_item_organ_tail our_tail = (organ_owner as mob_living_carbon).get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (our_tail != null)
            await our_tail.remove_tail_spines(our_tail.bodypart_owner);
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }
}

///Bodypart overlay for spines
// tg: code/modules/surgery/organs/external/spines.dm:30
public partial class datum_bodypart_overlay_mutant_spines : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/spines.dm:40
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.num(Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEJUMPSUIT)));
    }

    // tg: code/modules/surgery/organs/external/spines.dm:43
    public override async DmTask set_dye_color(object new_color = null, obj_item_organ organ = null)
    {
        obj_item_organ_tail tail = organ?.owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (tail?.tail_spines_overlay != null)
            await tail.tail_spines_overlay.set_dye_color(new_color, organ);
        await base.set_dye_color(new_color, organ);
    }
}
