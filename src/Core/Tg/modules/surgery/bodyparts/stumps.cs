// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/stumps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 4): the invisible bodyparts that stand in for a missing limb. The var blocks are generated
// (D111).

// Invisible bodyparts that serve as placeholders for a missing limb
// Used so we can add behavior to "missing limbs" without putting it on the mob itself

// tg: code/modules/surgery/bodyparts/stumps.dm:4
[DmPath("/obj/item/bodypart/head/stump")]
public partial class obj_item_bodypart_head_stump : obj_item_bodypart_head
{
    // tg: code/modules/surgery/bodyparts/stumps.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = "stump";
        // these traits are largely redundant, as many places that check for disabled limbs filter stumps.
        // however, marking them as unusable for the sake of completeness might make it easier to work with.
        ADD_TRAIT(this, TRAIT_PARALYSIS, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/stumps.dm:22
[DmPath("/obj/item/bodypart/leg/left/stump")]
public partial class obj_item_bodypart_leg_left_stump : obj_item_bodypart_leg_left
{
    // tg: code/modules/surgery/bodyparts/stumps.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = "stump";
        ADD_TRAIT(this, TRAIT_PARALYSIS, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/stumps.dm:35
[DmPath("/obj/item/bodypart/leg/right/stump")]
public partial class obj_item_bodypart_leg_right_stump : obj_item_bodypart_leg_right
{
    // tg: code/modules/surgery/bodyparts/stumps.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = "stump";
        ADD_TRAIT(this, TRAIT_PARALYSIS, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/stumps.dm:48
[DmPath("/obj/item/bodypart/arm/left/stump")]
public partial class obj_item_bodypart_arm_left_stump : obj_item_bodypart_arm_left
{
    // tg: code/modules/surgery/bodyparts/stumps.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = "stump";
        ADD_TRAIT(this, TRAIT_PARALYSIS, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/stumps.dm:61
[DmPath("/obj/item/bodypart/arm/right/stump")]
public partial class obj_item_bodypart_arm_right_stump : obj_item_bodypart_arm_right
{
    // tg: code/modules/surgery/bodyparts/stumps.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        name = "stump";
        ADD_TRAIT(this, TRAIT_PARALYSIS, INNATE_TRAIT);
        return dot;
    }
}
