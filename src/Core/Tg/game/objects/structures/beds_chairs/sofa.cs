// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/beds_chairs/sofa.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice A (D223): the whole file (the sofas and benches buckle as chairs); electrify_self (the shock kit's electric chair)
// since gaps AS1. The var blocks (with the COLORED_SOFA subtypes) are generated.

// tg: code/game/objects/structures/beds_chairs/sofa.dm:16
public partial class obj_structure_chair_sofa : obj_structure_chair
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_soft_landing)));
        return dot;
    }

    // tg: code/game/objects/structures/beds_chairs/sofa.dm:29
    public override async DmTask electrify_self(obj_item_assembly_shock_kit input_shock_kit = null, mob user = null, DmList overlays_from_child_procs = null)
    {
        if (overlays_from_child_procs == null)
        {
            mutable_appearance echair_overlay = mutable_appearance(Dm.resource("icons/obj/chairs.dmi"), "echair_over", OBJ_LAYER, this, appearance_flags: KEEP_APART) as mutable_appearance;
            echair_overlay.pixel_x = -1;
            overlays_from_child_procs = Dm.list(echair_overlay);
        }
        await base.electrify_self(input_shock_kit, user, overlays_from_child_procs);
    }
}

// tg: code/game/objects/structures/beds_chairs/sofa.dm:48
public partial class obj_structure_chair_sofa_corner : obj_structure_chair_sofa
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:36
    public override void handle_layer() //only the armrest/back of this chair should cover the mob.
    {
    }
}

// Make crafted brown sofas use the "retro sofa" display name without changing map-spawned ones.
// tg: code/game/objects/structures/beds_chairs/sofa.dm:51
public partial class obj_structure_chair_sofa_middle_brown : obj_structure_chair_sofa_middle
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            name = I18n.T("retro sofa");
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/sofa.dm:51
public partial class obj_structure_chair_sofa_left_brown : obj_structure_chair_sofa_left
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            name = I18n.T("retro sofa");
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/sofa.dm:51
public partial class obj_structure_chair_sofa_right_brown : obj_structure_chair_sofa_right
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            name = I18n.T("retro sofa");
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/sofa.dm:51
public partial class obj_structure_chair_sofa_corner_brown : obj_structure_chair_sofa_corner
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            name = I18n.T("retro sofa");
        return dot;
    }
}

// tg: code/game/objects/structures/beds_chairs/sofa.dm:87
public partial class obj_structure_chair_sofa_corp_corner : obj_structure_chair_sofa_corp
{
    // tg: code/game/objects/structures/beds_chairs/sofa.dm:90
    public override void handle_layer() //only the armrest/back of this chair should cover the mob.
    {
    }
}
