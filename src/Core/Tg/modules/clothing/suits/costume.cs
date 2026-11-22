// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the file's procs (the flash suit hood's white-out, the costumes' allowed lists, the cardborg suit's robot look to
// silicons with its helmet, the poncho of shame, the carp hood's carp faction and the spaceproof hood locked on, the bear suit's bear
// faction with its pelt). The fishing difficulty elements wait for fishing (not-translated.md 209); the rest of the file is var-only.
// The var blocks are generated (D111).

// tg: code/modules/clothing/suits/costume.dm:16
public partial class obj_item_clothing_head_hooded_flashsuit : obj_item_clothing_head_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_wearable_client_colour), typeof(datum_client_colour_flash_hood), ITEM_SLOT_HEAD, HELMET_TRAIT, Dm.named("forced", TRUE)));
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:45
public partial class obj_item_clothing_suit_costume_pirate_armored : obj_item_clothing_suit_costume_pirate
{
    // tg: code/modules/clothing/suits/costume.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_vest_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:77
public partial class obj_item_clothing_suit_costume_justice : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.security_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:128
public partial class obj_item_clothing_suit_costume_imperium_monk : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:136
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.chaplain_suit_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:156
public partial class obj_item_clothing_suit_toggle_owlwings : obj_item_clothing_suit_toggle
{
    // tg: code/modules/clothing/suits/costume.dm:167
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.security_vest_allowed;
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:178
public partial class obj_item_clothing_suit_costume_cardborg : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:189
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_OCLOTHING)))
            disguise(user as mob_living_carbon_human);
    }

    // tg: code/modules/clothing/suits/costume.dm:194
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (!Dm.truthy(in_use))
            return null;
        user.remove_alt_appearance("standard_borg_disguise");
        in_use = FALSE;
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        if (human_user.head is obj_item_clothing_head_costume_cardborg)
            UnregisterSignal(human_user.head, COMSIG_ITEM_DROPPED);
        return null;
    }

    // tg: code/modules/clothing/suits/costume.dm:204
    public virtual void disguise(mob_living_carbon_human human_user = null, obj_item_clothing_head_costume_cardborg borghead = null)
    {
        if (human_user == null)
            return;
        object head = borghead;
        if (!Dm.truthy(head))
            head = human_user.head;
        if (head is not obj_item_clothing_head_costume_cardborg) //why is this done this way? because equipped() is called BEFORE THE ITEM IS IN THE SLOT WHYYYY
            return;
        borghead = (obj_item_clothing_head_costume_cardborg)head;
        RegisterSignal(borghead, COMSIG_ITEM_DROPPED, nameof(helmet_drop)); // Don't need to worry about qdeleting since dropped will be called from there
        in_use = TRUE;
        image override_image = image(icon: Dm.resource("icons/mob/silicon/robots.dmi"), icon_state: "robot", loc: human_user);
        override_image.@override = TRUE;
        // override_image.add_overlay(...): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
        Dm.combine(ref override_image.overlays, mutable_appearance(Dm.resource("icons/mob/silicon/robots.dmi"), "robot_e")); //gotta look realistic
        add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_silicons), "standard_borg_disguise", override_image); //you look like a robot to robots! (including yourself because you're totally a robot)
    }

    // tg: code/modules/clothing/suits/costume.dm:218
    [SignalHandler]
    public virtual void helmet_drop(datum source = null, mob_living user = null)
    {
        UnregisterSignal(source, COMSIG_ITEM_DROPPED);
        user.remove_alt_appearance("standard_borg_disguise");
        in_use = FALSE;
    }
}

// tg: code/modules/clothing/suits/costume.dm:252
public partial class obj_item_clothing_suit_costume_poncho_ponchoshame : obj_item_clothing_suit_costume_poncho
{
    // tg: code/modules/clothing/suits/costume.dm:258
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, SHAMEBRERO_TRAIT);
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:270
public partial class obj_item_clothing_suit_hooded_carp_costume : obj_item_clothing_suit_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:284
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:286", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:288
public partial class obj_item_clothing_head_hooded_carp_hood : obj_item_clothing_head_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:300
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:302", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/suits/costume.dm:304
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
            user.add_faction("carp");
    }

    // tg: code/modules/clothing/suits/costume.dm:309
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (((mob_living_carbon_human)user).head == this)
            user.remove_faction("carp");
        return null;
    }
}

// tg: code/modules/clothing/suits/costume.dm:337
public partial class obj_item_clothing_head_hooded_carp_hood_spaceproof : obj_item_clothing_head_hooded_carp_hood
{
    // tg: code/modules/clothing/suits/costume.dm:358
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, LOCKED_HELMET_TRAIT);
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:409
public partial class obj_item_clothing_suit_hooded_shark_costume : obj_item_clothing_suit_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:420
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:422", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -4): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:424
public partial class obj_item_clothing_head_hooded_shark_hood : obj_item_clothing_head_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:434
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:436", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:438
public partial class obj_item_clothing_suit_hooded_shork_costume : obj_item_clothing_suit_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:449
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:451", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 4): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:453
public partial class obj_item_clothing_head_hooded_shork_hood : obj_item_clothing_head_hooded
{
    // tg: code/modules/clothing/suits/costume.dm:463
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:465", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 5): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:627
public partial class obj_item_clothing_suit_costume_hawaiian : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:640
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/suits/costume.dm:642", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        Dm.append(ref allowed, GLOB.personal_carry_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:657
public partial class obj_item_clothing_suit_costume_joker : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:662
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.personal_carry_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/costume.dm:712
public partial class obj_item_clothing_suit_costume_bear_suit : obj_item_clothing_suit_costume
{
    // tg: code/modules/clothing/suits/costume.dm:723
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_OCLOTHING)))
        {
            mob_living_carbon_human human_user = (mob_living_carbon_human)user;
            make_friendly(user as mob_living_carbon_human, human_user.head as obj_item_clothing_head_costume_bearpelt);
        }
    }

    // tg: code/modules/clothing/suits/costume.dm:729
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (!Dm.truthy(full_suit))
            return null;
        full_suit = FALSE;
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        UnregisterSignal(human_user.head, COMSIG_ITEM_DROPPED);
        user.remove_faction(FACTION_BEAR);
        return null;
    }

    // tg: code/modules/clothing/suits/costume.dm:738
    public virtual void make_friendly(mob_living_carbon_human human_user = null, obj_item_clothing_head_costume_bearpelt bear_head = null)
    {
        if (human_user == null)
            return;
        if (bear_head == null)
            return;
        RegisterSignal(bear_head, COMSIG_ITEM_DROPPED, nameof(helmet_drop));
        full_suit = TRUE;
        human_user.add_faction(FACTION_BEAR);
    }

    // tg: code/modules/clothing/suits/costume.dm:747
    [SignalHandler]
    public virtual void helmet_drop(datum source = null, mob_living user = null)
    {
        UnregisterSignal(source, COMSIG_ITEM_DROPPED);
        full_suit = FALSE;
        user.remove_faction(FACTION_BEAR);
    }
}
