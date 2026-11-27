// Translated from tgstation (snapshot 1d99bd7f): the Initialize and overlay procs of the job lockers' clothing: code/modules/clothing/ears/_ears.dm, gloves/special.dm, gloves/insulated.dm, gloves/color.dm, glasses/_glasses.dm, suits/bio.dm, suits/straightjacket.dm, suits/utility.dm, suits/jobs.dm, suits/labcoat.dm, suits/cloaks.dm, head/hardhat.dm, head/welding.dm, shoes/magboots.dm, under/jobs/medical.dm, under/accessories/medals.dm, code/modules/paperwork/stamps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298). equipment_bodypart_texture (body textures) and clothing_dirt are rows (not-translated.md 114; the hat_stabilizer came with gaps MO1);
// the emissive worn overlays are under DV12.

// tg: code/modules/clothing/ears/_ears.dm:14
public partial class obj_item_clothing_ears_earmuffs : obj_item_clothing_ears
{
    // tg: code/modules/clothing/ears/_ears.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_earhealing)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_wearertargeting_earprotection)));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:545
public partial class obj_item_clothing_glasses_blindfold : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:560
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 8));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:323
public partial class obj_item_clothing_glasses_regular : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:335
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_knockoff), 25, Dm.list(BODY_ZONE_PRECISE_EYES), slot_flags));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)))));
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:343
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable movable = null)
    {
        if (Dm.eq(damaged_clothes, CLOTHING_SHREDDED))
            return;
        if (Dm.truthy(Dm.bitand(item_flags, IN_INVENTORY)))
            return;
        if (isliving(movable))
        {
            mob_living crusher = movable as mob_living;
            if (!Dm.eq(crusher.move_intent, MOVE_INTENT_WALK) && (!Dm.truthy(Dm.bitand(crusher.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) || crusher.buckled != null))
            {
                playsound(this, Dm.resource("sound/effects/footstep/glass_step.ogg"), 30, TRUE);
                visible_message(span_warning(I18n.T("[] steps on [], damaging it!", crusher, this)));
                Waiting.Detach(take_damage(100, sound_effect: FALSE));
            }
        }
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:356
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await base.atom_destruction(damage_flag);
        detach_clothing_traits(TRAIT_NEARSIGHTED_CORRECTED);
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:360
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        await base.welder_act(user, I);
        if (Dm.eq(damaged_clothes, CLOTHING_PRISTINE))
            return null;
        if (!Dm.truthy(I.tool_start_check(user, amount: 1)))
            return null;
        if (Dm.truthy(await I.use_tool(this, user, 10, volume: 30)))
        {
            user.visible_message(span_notice(I18n.T("[] welds [] back together.", user, this)), span_notice(I18n.T("You weld [] back together.", this)));
            await repair(user);
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:371
    public override async DmTask repair(mob user = null)
    {
        await base.repair(user);
        attach_clothing_traits(TRAIT_NEARSIGHTED_CORRECTED);
    }
}

// tg: code/modules/clothing/gloves/special.dm:163
public partial class obj_item_clothing_gloves_atmos : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:146
public partial class obj_item_clothing_gloves_chief_engineer : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/insulated.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/color.dm:32
public partial class obj_item_clothing_gloves_fingerless : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/color.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -4));

        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", Dm.list(typeof(datum_crafting_recipe_gripperoffbrand)))));
        return dot;
    }
}

// tg: code/modules/clothing/suits/bio.dm:3
public partial class obj_item_clothing_head_bio_hood : obj_item_clothing_head
{
    // tg: code/modules/clothing/suits/bio.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // clothing_dirt (:22), fishing (:23) and equipment_bodypart_texture (:25): rows
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE))); // :24, gaps MO1
        return dot;
    }
}

// tg: code/modules/clothing/suits/bio.dm:33
public partial class obj_item_clothing_suit_bio_suit : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/bio.dm:60
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra); // equipment_bodypart_texture, fishing: rows
}

// tg: code/modules/clothing/suits/bio.dm:137
public partial class obj_item_clothing_suit_bio_suit_cmo : obj_item_clothing_suit_bio_suit
{
    // tg: code/modules/clothing/suits/bio.dm:140
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = Dm.add(allowed, Dm.list(typeof(obj_item_melee_baton_telescopic))) as DmList;
        return dot;
    }
}

// tg: code/modules/clothing/suits/straightjacket.dm:2
public partial class obj_item_clothing_suit_jacket_straight_jacket : obj_item_clothing_suit_jacket
{
    // tg: code/modules/clothing/suits/straightjacket.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_DANGEROUS_EQUIP, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/clothing/suits/utility.dm:18
public partial class obj_item_clothing_suit_utility_fire : obj_item_clothing_suit_utility
{
    // tg: code/modules/clothing/suits/utility.dm:40
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra); // equipment_bodypart_texture, fishing: rows
    // tg: code/modules/clothing/suits/utility.dm:45  worn_overlays: the emissive (DV12), a row
}

// tg: code/modules/clothing/suits/utility.dm:95
public partial class obj_item_clothing_head_utility_bomb_hood : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/suits/utility.dm:113
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // clothing_dirt (:115), fishing (:116) and equipment_bodypart_texture (:118): rows
        Waiting.NoSleep(AddComponent(typeof(datum_component_hat_stabilizer), Dm.named("loose_hat", TRUE))); // :117, gaps MO1
        return dot;
    }
}

// tg: code/modules/clothing/suits/utility.dm:130
public partial class obj_item_clothing_suit_utility_bomb_suit : obj_item_clothing_suit_utility
{
    // tg: code/modules/clothing/suits/utility.dm:152
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra); // equipment_bodypart_texture, fishing: rows
}

// tg: code/modules/clothing/suits/labcoat.dm:104
public partial class obj_item_clothing_suit_toggle_labcoat_virologist : obj_item_clothing_suit_toggle_labcoat
{
    // tg: code/modules/clothing/suits/labcoat.dm:113
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = Dm.add(allowed, typeof(obj_item_storage_bag_bio)) as DmList;
        return dot;
    }
}

// tg: code/modules/clothing/suits/cloaks.dm:4
public partial class obj_item_clothing_neck_cloak : obj_item_clothing_neck
{
    // tg: code/modules/clothing/suits/cloaks.dm:19
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is strangling []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/modules/clothing/head/hardhat.dm:207
public partial class obj_item_clothing_head_utility_hardhat_welding_atmos : obj_item_clothing_head_utility_hardhat_welding
{
    // tg: code/modules/clothing/head/hardhat.dm:224
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra); // equipment_bodypart_texture: a row
    // tg: code/modules/clothing/head/hardhat.dm:228  worn_overlays: the emissive (DV12), a row
}

// tg: code/modules/clothing/head/welding.dm:2
public partial class obj_item_clothing_head_utility_welding : obj_item_clothing_head_utility
{
    // tg: code/modules/clothing/head/welding.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(up))
            Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 8));
        return dot;
    }

    // tg: code/modules/clothing/head/welding.dm:32
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/head/welding.dm:35
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        if (Dm.truthy(up))
            RemoveElement(typeof(datum_element_adjust_fishing_difficulty));
        else
            await AddElement(typeof(datum_element_adjust_fishing_difficulty), 8);
        return dot;
    }

    // tg: code/modules/clothing/head/welding.dm:42
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = $"{Dm.initial(this, nameof(icon_state))}{(Dm.truthy(up) ? "up" : "")}";
        inhand_icon_state = $"{Dm.initial(this, nameof(inhand_icon_state))}{(Dm.truthy(up) ? "off" : "")}";
        return dot;
    }
}

// tg: code/modules/clothing/shoes/magboots.dm:96
public partial class obj_item_clothing_shoes_magboots_advance : obj_item_clothing_shoes_magboots
{
    // tg: code/modules/clothing/shoes/magboots.dm:104
    public override object add_stealing_item_objective() => add_item_to_steal(this, typeof(obj_item_clothing_shoes_magboots_advance));
}

// tg: code/modules/clothing/under/jobs/medical.dm:45
public partial class obj_item_clothing_under_rank_medical_chief_medical_officer_scrubs : obj_item_clothing_under_rank_medical_chief_medical_officer
{
    // tg: code/modules/clothing/under/jobs/medical.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3)); //FISH DOCTOR?!
        return dot;
    }
}

// tg: code/modules/clothing/under/accessories/medals.dm:116
public partial class obj_item_clothing_accessory_medal_plasma : obj_item_clothing_accessory_medal
{
    // tg: code/modules/clothing/under/accessories/medals.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/medals.dm:127
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null) => Dm.num(exposed_temperature) > 300 ? TRUE : FALSE;

    // tg: code/modules/clothing/under/accessories/medals.dm:130
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        atmos_spawn_air(Dm.text("[]=20;[]", GAS_PLASMA, TURF_TEMPERATURE(exposed_temperature)));
        visible_message(span_danger(I18n.T("\\The [] bursts into flame!", this)), span_userdanger(I18n.T("Your [] bursts into flame!", this)));
        qdel(this);
        return null;
    }
}

// tg: code/modules/clothing/under/accessories/medals.dm:139
public partial class obj_item_clothing_accessory_medal_silver_emergency_services : obj_item_clothing_accessory_medal_silver
{
    // tg: code/modules/clothing/under/accessories/medals.dm:146
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (insignia_desc is string)
            desc = Dm.text("[] []", desc, insignia_desc);
        return dot;
    }
}

// tg: code/modules/clothing/masks/bandana.dm:2
public partial class obj_item_clothing_mask_bandana : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/bandana.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(up))
        {
            dot.Add(I18n.T("Use in-hand to untie it to wear as a mask!"));
            return dot;
        }
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_NECK)))
            dot.Add(I18n.T("Alt-click to untie it to wear as a mask!"));
        else
        {
            dot.Add(I18n.T("Use in-hand to tie it up to wear as a hat!"));
            dot.Add(I18n.T("Alt-click to tie it up to wear on your neck!"));
        }
        return dot;
    }

    // tg: code/modules/clothing/masks/bandana.dm:31
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/masks/bandana.dm:34
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_NECK)))
        {
            to_chat(user, span_warning(I18n.T("You must undo [] in order to push it into a hat!", this)));
            return FALSE;
        }
        return await base.adjust_visor(user);
    }

    // tg: code/modules/clothing/masks/bandana.dm:40
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        if (Dm.truthy(up))
            undyeable = TRUE;
        else
            undyeable = Dm.num(Dm.initial(this, nameof(undyeable)));
    }

    // tg: code/modules/clothing/masks/bandana.dm:47
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!iscarbon(user))
            return NONE;
        mob_living_carbon char_mob = user as mob_living_carbon;
        if (char_mob.get_item_by_slot(ITEM_SLOT_NECK) == this || char_mob.get_item_by_slot(ITEM_SLOT_MASK) == this || char_mob.get_item_by_slot(ITEM_SLOT_HEAD) == this)
        {
            to_chat(user, span_warning(I18n.T("You can't tie [] while wearing it!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        else if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HEAD)))
        {
            to_chat(user, span_warning(I18n.T("You must undo [] before you can tie it into a neckerchief!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        else if (!Dm.truthy(user.is_holding(this)))
        {
            to_chat(user, span_warning(I18n.T("You must be holding [] in order to tie it!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_MASK)))
        {
            undyeable = TRUE;
            slot_flags = ITEM_SLOT_NECK;
            worn_y_offset = -3;
            // widen.Scale(1.25, 1), transform = widen: the worn sprite's width
            user.visible_message(span_notice(I18n.T("[] ties [] up like a neckerchief.", user, this)), span_notice(I18n.T("You tie [] up like a neckerchief.", this)));
            flags_inv = NONE;
            flags_cover = NONE;
            return CLICK_ACTION_SUCCESS;
        }
        undyeable = Dm.num(Dm.initial(this, nameof(undyeable)));
        slot_flags = Dm.num(Dm.initial(this, nameof(slot_flags)));
        worn_y_offset = Dm.num(Dm.initial(this, nameof(worn_y_offset)));
        user.visible_message(span_notice(I18n.T("[] unties the neckerchief.", user)), span_notice(I18n.T("You untie the neckerchief.")));
        flags_inv = Dm.num(Dm.initial(this, nameof(flags_inv)));
        flags_cover = Dm.num(Dm.initial(this, nameof(flags_cover)));
        return CLICK_ACTION_SUCCESS;
    }
}
