// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that an M2 slot signature or
// var block names, in the earliest .dme file that declares it (rules 2.3); written by the M2 contracts step
// (docs/ai/specs/m2-ownership.md). Only the type is declared.

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:341
[DmPath("/obj/item/bodypart/head/skeleton")]
public partial class obj_item_bodypart_head_skeleton : obj_item_bodypart_head
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:352
[DmPath("/obj/item/bodypart/chest/skeleton")]
public partial class obj_item_bodypart_chest_skeleton : obj_item_bodypart_chest
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:363
[DmPath("/obj/item/bodypart/arm/left/skeleton")]
public partial class obj_item_bodypart_arm_left_skeleton : obj_item_bodypart_arm_left
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:372
[DmPath("/obj/item/bodypart/arm/right/skeleton")]
public partial class obj_item_bodypart_arm_right_skeleton : obj_item_bodypart_arm_right
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:381
[DmPath("/obj/item/bodypart/leg/left/skeleton")]
public partial class obj_item_bodypart_leg_left_skeleton : obj_item_bodypart_leg_left
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:390
[DmPath("/obj/item/bodypart/leg/right/skeleton")]
public partial class obj_item_bodypart_leg_right_skeleton : obj_item_bodypart_leg_right
{
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:400
[DmPath("/obj/item/bodypart/head/skeleton/nonfunctional")]
public partial class obj_item_bodypart_head_skeleton_nonfunctional : obj_item_bodypart_head_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:406
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:410
[DmPath("/obj/item/bodypart/chest/skeleton/nonfunctional")]
public partial class obj_item_bodypart_chest_skeleton_nonfunctional : obj_item_bodypart_chest_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:415
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:419
    public override void on_adding(mob_living_carbon new_owner = null)
    {
        base.on_adding(new_owner);
        // Treat people with bone chests as husks for all purposes for now
        // Ideally husking should be per-bodypart but this simplifies a lot of behaviors
        new_owner.become_husk(SKELETON_TRAIT);
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:425
    public override async DmTask on_removal(mob_living_carbon old_owner = null)
    {
        await base.on_removal(old_owner);
        await old_owner.cure_husk(SKELETON_TRAIT);
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:429
[DmPath("/obj/item/bodypart/arm/left/skeleton/nonfunctional")]
public partial class obj_item_bodypart_arm_left_skeleton_nonfunctional : obj_item_bodypart_arm_left_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:434
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:438
[DmPath("/obj/item/bodypart/arm/right/skeleton/nonfunctional")]
public partial class obj_item_bodypart_arm_right_skeleton_nonfunctional : obj_item_bodypart_arm_right_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:443
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:447
[DmPath("/obj/item/bodypart/leg/left/skeleton/nonfunctional")]
public partial class obj_item_bodypart_leg_left_skeleton_nonfunctional : obj_item_bodypart_leg_left_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:452
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:456
[DmPath("/obj/item/bodypart/leg/right/skeleton/nonfunctional")]
public partial class obj_item_bodypart_leg_right_skeleton_nonfunctional : obj_item_bodypart_leg_right_skeleton
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:461
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_blood_limb_overlay)));
        return dot;
    }
}

// GOLEM (gaps SP3a, D349)
// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:531
public partial class obj_item_bodypart_head_golem : obj_item_bodypart_head
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:549
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        worn_ears_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_EARS),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 1), Dm.kv("south", -1), Dm.kv("east", 1), Dm.kv("west", -1))),
            Dm.named("offset_y", Dm.list(Dm.kv("south", 1))));
        worn_glasses_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_GLASSES),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 1), Dm.kv("south", -1), Dm.kv("east", 1), Dm.kv("west", -1))));
        worn_head_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_HEAD),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 1), Dm.kv("south", -1), Dm.kv("east", 1), Dm.kv("west", -1))),
            Dm.named("offset_y", Dm.list(Dm.kv("south", 1))));
        worn_face_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_FACE),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 1), Dm.kv("south", -1), Dm.kv("east", 1), Dm.kv("west", -1))));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:575
public partial class obj_item_bodypart_chest_golem : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:592
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        worn_belt_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_BELT),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 1), Dm.kv("south", -1), Dm.kv("east", 1), Dm.kv("west", -1))));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:601
public partial class obj_item_bodypart_arm_left_golem : obj_item_bodypart_arm_left
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:618
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        held_hand_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_HELD),
            Dm.named("offset_x", Dm.list(Dm.kv("north", -1), Dm.kv("south", 2), Dm.kv("east", 0), Dm.kv("west", -3))),
            Dm.named("offset_y", Dm.list(Dm.kv("south", -2))));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:627
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);

        old_owner.RemoveComponentSource(REF(this), typeof(datum_component_shovel_hands));
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:632
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        await base.apply_ownership(new_owner);

        await new_owner.AddComponentFrom(REF(this), typeof(datum_component_shovel_hands));
    }
}

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:638
public partial class obj_item_bodypart_arm_right_golem : obj_item_bodypart_arm_right
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:655
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        held_hand_offset = Dm.@new<datum_worn_feature_offset>(
            Dm.named("attached_part", this),
            Dm.named("feature_key", OFFSET_HELD),
            Dm.named("offset_x", Dm.list(Dm.kv("north", 2), Dm.kv("south", -2), Dm.kv("east", 3), Dm.kv("west", 0))),
            Dm.named("offset_y", Dm.list(Dm.kv("south", -2))));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:664
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);

        old_owner.RemoveComponentSource(REF(this), typeof(datum_component_shovel_hands));
    }

    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:669
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        await base.apply_ownership(new_owner);

        await new_owner.AddComponentFrom(REF(this), typeof(datum_component_shovel_hands));
    }
}

// Species package SP2b (D349): the podperson's chest, the flowerpot butt.
// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:194
public partial class obj_item_bodypart_chest_pod : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:202
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_FLOWERPOT);
    }
}

// ---- Gaps SP2c (D349): the JELLY family's limbs (the var blocks are generated, D111) ----

// tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:108
public partial class obj_item_bodypart_chest_jelly : obj_item_bodypart_chest
{
    // tg: code/modules/surgery/bodyparts/species_parts/misc_bodyparts.dm:108
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_SLIME);
    }
}
