// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the lizard's limbs (their greyscale sprites drawn in the mutant colour), the chest's butt sprite and the
// digitigrade legs (datums/components/digitigrade_limb.cs). The var blocks are generated (D111).

// tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:12
public partial class obj_item_bodypart_chest_lizard : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:19
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_LIZARD);
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:62
public partial class obj_item_bodypart_leg_left_digitigrade : obj_item_bodypart_leg_left
{
    // tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_digitigrade_limb), SPECIES_LIZARD, Dm.initial(this, (obj_item_bodypart p) => p.limb_id)));
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:75
public partial class obj_item_bodypart_leg_right_digitigrade : obj_item_bodypart_leg_right
{
    // tg: code/modules/surgery/bodyparts/species_parts/lizard_bodyparts.dm:84
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_digitigrade_limb), SPECIES_LIZARD, Dm.initial(this, (obj_item_bodypart p) => p.limb_id)));
        return dot;
    }
}
