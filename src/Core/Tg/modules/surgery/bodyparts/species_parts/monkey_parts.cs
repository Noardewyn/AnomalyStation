// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/species_parts/monkey_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The procs of the file (gaps package C5); the limbs' var overrides are generated.

// tg: code/modules/surgery/bodyparts/species_parts/monkey_parts.dm:2
public partial class obj_item_bodypart_head_monkey : obj_item_bodypart_head
{
    // tg: code/modules/surgery/bodyparts/species_parts/monkey_parts.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        worn_head_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_HEAD),
            Dm.named("offset_y", Dm.list(Dm.kv("south", 1))));
        worn_glasses_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_GLASSES),
            Dm.named("offset_y", Dm.list(Dm.kv("south", 1))));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/monkey_parts.dm:28
public partial class obj_item_bodypart_chest_monkey : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/monkey_parts.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        worn_neck_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_NECK),
            Dm.named("offset_y", Dm.list(Dm.kv("south", 1))));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/surgery/bodyparts/species_parts/monkey_parts.dm:55
    public override object update_mob_heights(mob_living_carbon holder = null)
    {
        if (HAS_TRAIT(holder, TRAIT_DWARF))
            return MONKEY_HEIGHT_DWARF;

        if (HAS_TRAIT(holder, TRAIT_TOO_TALL))
            return MONKEY_HEIGHT_TALL;

        return MONKEY_HEIGHT_MEDIUM;
    }
}
