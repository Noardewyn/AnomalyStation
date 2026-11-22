// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/jacket.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the Initialize of the jackets (the personal carry list of firearms and holsters); syndicate N2c (D316): the overwatch agent's
// blood-red letterman jacket. Gaps CL1 (D333): the rest of the file (the improvised firearms on the trench coat, the bomber, leather,
// military and letterman jackets, the security list on the armoured leather jacket). The var blocks are generated.

// tg: code/modules/clothing/suits/jacket.dm:2
public partial class obj_item_clothing_suit_jacket : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/jacket.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.personal_carry_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:26
public partial class obj_item_clothing_suit_toggle_jacket : obj_item_clothing_suit_toggle
{
    // tg: code/modules/clothing/suits/jacket.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.personal_carry_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:60
public partial class obj_item_clothing_suit_toggle_jacket_trenchcoat : obj_item_clothing_suit_toggle_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:119
public partial class obj_item_clothing_suit_jacket_bomber : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:129
public partial class obj_item_clothing_suit_jacket_leather : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:137
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:146
public partial class obj_item_clothing_suit_jacket_leather_armored : obj_item_clothing_suit_jacket_leather
{
    // tg: code/modules/clothing/suits/jacket.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_vest_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:180
public partial class obj_item_clothing_suit_jacket_miljacket : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:186
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:190
public partial class obj_item_clothing_suit_jacket_letterman : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:197
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:201
public partial class obj_item_clothing_suit_jacket_letterman_red : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:208
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:212
public partial class obj_item_clothing_suit_jacket_letterman_syndie : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/jacket.dm:223
public partial class obj_item_clothing_suit_jacket_letterman_nanotrasen : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/jacket.dm:230
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.improvised_firearm_allowed);
        return dot;
    }
}
