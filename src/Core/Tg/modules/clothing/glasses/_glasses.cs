// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/glasses/_glasses.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/item/clothing/glasses whole (M4a: equipped and dropped; M7 slice T3, D164: the colour element, the visor, thermal
// overload and the colour change), the mesons (the optical, night vision and gar ones: :88-135) and no other glasses subtype. The vision
// they give goes through /mob/living/update_sight (living.dm:2029, modules/mob/living/living.cs): the traits (TRAIT_MESON_VISION) set
// the wearer's sight flags, and the glasses their lighting cutoff and colour cutoffs; the 3D client draws them (protocol version 31).
// J1 (D298): the sunglasses' Initialize and their slapcrafting recipes.
// The syndicate campaign G1d (D316): the thermal glasses whole (:586-645: the EMP overload, the thermoncle's examine, the eyepatch's flip).
// Gaps ST2: the eyepatch (:194-249: its flip and the tint over a scarred eye; the skeleton model wears one). Its medical subtype is
// var-only. Gaps CL1 (D333): the night vision and science night goggles' off state, the wizard's chuuni eyepatch, the chemical
// sunglasses' slapcrafting, the welding goggles' visor (its icon up and down, tg's tint through update_tint), the suicides of the
// glasses, the mesons and the science goggles, the white blindfold's eye colour, the debug glasses' x-ray, the salesman's glasses
// and the nightmare vision goggles. Their adjust_fishing_difficulty elements wait for fishing (not-translated.md 209).

// tg: code/modules/clothing/glasses/_glasses.dm:2
public partial class obj_item_clothing_glasses : obj_item_clothing
{
    // tg: code/modules/clothing/glasses/_glasses.dm:54
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)))
            return;
        if (Dm.truthy(invis_override) || Dm.truthy(invis_view) || lighting_cutoff != null)
        {
            //This call becomes redundant if thermal/meson/material (etc.) vision flags are present in clothing_traits, but, as of the time of this comment, it isn't really a problem.
            user.update_sight();
        }
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:62
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(invis_override) || Dm.truthy(invis_view) || lighting_cutoff != null)
        {
            //This call becomes redundant if thermal/meson/material (etc.) vision flags are present in clothing_traits, but, as of the time of this comment, it isn't really a problem.
            user.update_sight();
        }
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (glass_colour_type != null)
            Waiting.NoSleep(AddElement(typeof(datum_element_wearable_client_colour), glass_colour_type, ITEM_SLOT_EYES, GLASSES_TRAIT, Dm.named("forced", forced_glass_color), Dm.named("comsig_toggle", COMSIG_CLICK_ALT_SECONDARY)));
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:39  suicide_act: live-partial (M10).

    // tg: code/modules/clothing/glasses/_glasses.dm:43
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        alternate_worn_layer = Dm.truthy(up) ? ABOVE_BODY_FRONT_HEAD_LAYER : null;
        if (Dm.truthy(Dm.bitand(visor_vars_to_toggle, VISOR_INVISVIEW)))
            invis_view = Dm.bitxor(invis_view, Dm.initial(this, (obj_item_clothing_glasses p) => p.invis_view));
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:49
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        if (Dm.truthy(dot) && !Dm.truthy(user.is_holding(this)) && Dm.truthy(Dm.bitand(visor_vars_to_toggle, VISOR_INVISVIEW)))
            user.update_sight();
        return dot;
    }

    //called when thermal glasses are emped.
    // tg: code/modules/clothing/glasses/_glasses.dm:69
    public virtual async DmTask thermal_overload()
    {
        if (ishuman(loc))
        {
            mob_living_carbon_human H = loc as mob_living_carbon_human;
            obj_item_organ_eyes eyes = H.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (!Dm.truthy(H.is_blind()))
            {
                if (H.glasses == this)
                {
                    to_chat(H, span_danger(I18n.T("[] overloads and blinds you!", this)));
                    await H.flash_act(visual: 1);
                    await H.adjust_temp_blindness(6 * SECONDS);
                    await H.set_eye_blur_if_lower(10 * SECONDS);
                    await eyes.apply_organ_damage(5);
                }
            }
        }
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:81
    public virtual void change_glass_color(object new_color_type = null)
    {
        if (glass_colour_type != null)
            RemoveElement(typeof(datum_element_wearable_client_colour), glass_colour_type, ITEM_SLOT_EYES, GLASSES_TRAIT, Dm.named("forced", forced_glass_color));
        glass_colour_type = (Type)new_color_type;
        if (glass_colour_type != null)
            Waiting.NoSleep(AddElement(typeof(datum_element_wearable_client_colour), glass_colour_type, ITEM_SLOT_EYES, GLASSES_TRAIT, Dm.named("forced", forced_glass_color)));
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:39
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is stabbing \\the [] into [] eyes! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:88
public partial class obj_item_clothing_glasses_meson : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:103  suicide_act: live-partial (M10).

    // tg: code/modules/clothing/glasses/_glasses.dm:103
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is putting \\the [] to [] eyes and overloading the brightness! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:107
public partial class obj_item_clothing_glasses_meson_night : obj_item_clothing_glasses_meson
{
    // tg: code/modules/clothing/glasses/_glasses.dm:119
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_meson_night p) => p.icon_state) : "nvgmeson_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:137
public partial class obj_item_clothing_glasses_science : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:156
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is tightening \\the []'s straps around [] neck! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:160
public partial class obj_item_clothing_glasses_science_night : obj_item_clothing_glasses_science
{
    // tg: code/modules/clothing/glasses/_glasses.dm:171
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_science_night p) => p.icon_state) : "night_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:175
public partial class obj_item_clothing_glasses_night : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:191
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(length(color_cutoffs)) ? Dm.initial(this, (obj_item_clothing_glasses_night p) => p.icon_state) : "night_off";
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:194
public partial class obj_item_clothing_glasses_eyepatch : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:208
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        object dot = await base.click_alt(user, __extra);
        await flip_eyepatch();
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:212
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await flip_eyepatch();
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:216
    public virtual async DmTask flip_eyepatch()
    {
        flipped = Dm.not(flipped);
        icon_state = Dm.truthy(flipped) ? Dm.text("[]_flipped", base_icon_state) : Dm.str(base_icon_state);
        if (!ismob(loc))
            return;
        mob user = (mob)loc;
        await user.update_worn_glasses();
        if (!ishuman(user))
            return;
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        if (Dm.truthy(Dm.bitand(human_user.get_eye_scars(), Dm.truthy(flipped) ? RIGHT_EYE_SCAR : LEFT_EYE_SCAR)))
            tint = INFINITY;
        else
            tint = Dm.num(Dm.initial(this, nameof(tint)));
        await human_user.update_tint();
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:233
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        if (!ishuman(user))
        {
            await base.equipped__proc(user, slot, initial);
            return;
        }
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        // lol lmao
        if (Dm.truthy(Dm.bitand(human_user.get_eye_scars(), Dm.truthy(flipped) ? RIGHT_EYE_SCAR : LEFT_EYE_SCAR)))
            tint = INFINITY;
        else
            tint = Dm.num(Dm.initial(this, nameof(tint)));
        await base.equipped__proc(user, slot, initial);
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:244
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        tint = Dm.num(Dm.initial(this, nameof(tint)));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:256
public partial class obj_item_clothing_glasses_eyepatch_medical_chuuni : obj_item_clothing_glasses_eyepatch_medical
{
    // The first definition of Initialize (tg defines it twice on this type; the second's ..() calls this one, rules 2.5 and 7.5).
    // tg: code/modules/clothing/glasses/_glasses.dm:259
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CASTING_CLOTHING, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:263
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.eq(slot, ITEM_SLOT_EYES))
            ADD_TRAIT(this, TRAIT_NODROP, type);
    }

    // The second definition of Initialize (rules 2.5: its ..() is the first definition, Initialize__d0).
    // tg: code/modules/clothing/glasses/_glasses.dm:268
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        // tg: code/modules/clothing/glasses/_glasses.dm:270  var/static/list/chuuni_backstories (a proc-level static, rules 10.6)
        ref DmList chuuni_backstories = ref TgState.Current.obj_item_clothing_glasses_eyepatch_medical_chuuni__Initialize__chuuni_backstories;
        if (!Dm.truthy(chuuni_backstories))
        {
            chuuni_backstories = Dm.list(
                I18n.T("This eyepatch is a seal that contains the power of the demon king. If I remove it, I will unleash a cataclysmic destruction upon the world."),
                I18n.T("This eyepatch is a gift from the angel of light. It allows me to see the true nature of things and protect the innocent from harm."),
                I18n.T("This eyepatch is a mark of my contract with the dragon god. It grants me access to his ancient wisdom and fiery breath."),
                I18n.T("This eyepatch is a symbol of my sacrifice for the sake of love. It hides the scar that I received from saving my beloved from a fatal attack.")
            );
        }
        desc = Dm.str(pick(chuuni_backstories));
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:398
public partial class obj_item_clothing_glasses_sunglasses : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:408
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(add_glasses_slapcraft_component());
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:412
    public virtual async DmTask add_glasses_slapcraft_component()
    {
        // tg: code/modules/clothing/glasses/_glasses.dm:413  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        await AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", TgState.Current.obj_item_clothing_glasses_sunglasses__add_glasses_slapcraft_component__slapcraft_recipe_list));
    }
}

// The static lists of the glasses (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/clothing/glasses/_glasses.dm:413  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/hudsunsec, /datum/crafting_recipe/hudsunmed, /datum/crafting_recipe/hudsundiag, /datum/crafting_recipe/scienceglasses)
    internal DmList obj_item_clothing_glasses_sunglasses__add_glasses_slapcraft_component__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_hudsunsec), typeof(datum_crafting_recipe_hudsunmed), typeof(datum_crafting_recipe_hudsundiag), typeof(datum_crafting_recipe_scienceglasses));
    // tg: code/modules/clothing/glasses/_glasses.dm:434  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/scienceglassesremoval)
    internal DmList obj_item_clothing_glasses_sunglasses_chemical__add_glasses_slapcraft_component__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_scienceglassesremoval));
    // tg: code/modules/clothing/glasses/_glasses.dm:270  var/static/list/chuuni_backstories
    internal DmList obj_item_clothing_glasses_eyepatch_medical_chuuni__Initialize__chuuni_backstories;

}

// tg: code/modules/clothing/glasses/_glasses.dm:426
public partial class obj_item_clothing_glasses_sunglasses_chemical : obj_item_clothing_glasses_sunglasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:433
    public override async DmTask add_glasses_slapcraft_component()
    {
        // tg: code/modules/clothing/glasses/_glasses.dm:434  var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        await AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_clothing_glasses_sunglasses_chemical__add_glasses_slapcraft_component__slapcraft_recipe_list)
        );
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:505
public partial class obj_item_clothing_glasses_welding : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:521
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(up))
            Dm.Deferred("code/modules/clothing/glasses/_glasses.dm:524", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 8): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:526
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:529
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        // if(up) RemoveElement(/datum/element/adjust_fishing_difficulty) else AddElement(/datum/element/adjust_fishing_difficulty, 8)
        Dm.Deferred("code/modules/clothing/glasses/_glasses.dm:531", "adjust_visor: the adjust_fishing_difficulty element of the visor: fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:536
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_clothing_glasses_welding p) => p.icon_state), Dm.truthy(up) ? "up" : "");
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:540
public partial class obj_item_clothing_glasses_welding_up : obj_item_clothing_glasses_welding
{
    // tg: code/modules/clothing/glasses/_glasses.dm:540
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visor_toggling();
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:567
public partial class obj_item_clothing_glasses_blindfold_white : obj_item_clothing_glasses_blindfold
{
    // tg: code/modules/clothing/glasses/_glasses.dm:574
    [DmParams("user", "slot")]
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)) && !Dm.truthy(colored_before))
        {
            mob_living_carbon_human human_user = (mob_living_carbon_human)user;
            await add_atom_colour(BlendRGB(human_user.eye_color_left, human_user.eye_color_right, 0.5f), FIXED_COLOUR_PRIORITY);
            colored_before = TRUE;
        }
        return await base.visual_equipped(user, slot, initial);
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:700
public partial class obj_item_clothing_glasses_debug : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:724
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/glasses/_glasses.dm:726", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -15): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:728
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(xray))
            detach_clothing_traits(TRAIT_XRAY_VISION);
        else
            attach_clothing_traits(TRAIT_XRAY_VISION);
        xray = Dm.num(!Dm.truthy(xray));
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:742
public partial class obj_item_clothing_glasses_salesman : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:751
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)))
            return;
        bigshot = user as mob_living_carbon_human;
        RegisterSignal(bigshot, COMSIG_CARBON_SANITY_UPDATE, nameof(moodshift));
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:758
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (bigshot != null)
            UnregisterSignal(bigshot, COMSIG_CARBON_SANITY_UPDATE);
        bigshot = null; // initial(bigshot): a mob var's default is null
        icon_state = Dm.initial(this, (obj_item_clothing_glasses_salesman p) => p.icon_state);
        desc = Dm.initial(this, (obj_item_clothing_glasses_salesman p) => p.desc);
        return null;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:766
    [SignalHandler]
    public virtual void moodshift(atom_movable source = null, object amount = null)
    {
        if (Dm.num(amount) < SANITY_UNSTABLE)
        {
            icon_state = "salesman_fzz";
            desc = I18n.T("A pair of glasses, the lenses are full of TV static. They've certainly seen better days...");
            Waiting.NoSleep(bigshot.update_worn_glasses());
        }
        else
        {
            icon_state = Dm.initial(this, (obj_item_clothing_glasses_salesman p) => p.icon_state);
            desc = Dm.initial(this, (obj_item_clothing_glasses_salesman p) => p.desc);
            Waiting.NoSleep(bigshot.update_worn_glasses());
        }
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:777
public partial class obj_item_clothing_glasses_nightmare_vision : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:792
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/glasses/_glasses.dm:794", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 13): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:796
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stored_hallucination);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:800
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)))
            return;
        //5% chance to get mare vision
        if (prob(5))
        {
            stored_hallucination = await ((mob_living)user).cause_hallucination(
                typeof(datum_hallucination_delusion_preset_mare),
                this.name,
                Dm.named("duration", 0),
                Dm.named("affects_us", TRUE),
                Dm.named("affects_others", TRUE),
                Dm.named("skip_nearby", FALSE),
                Dm.named("play_wabbajack", FALSE)
            ) as datum_hallucination;
        }
    }

    // tg: code/modules/clothing/glasses/_glasses.dm:816
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        QDEL_NULL(ref stored_hallucination);
        return dot;
    }
}

///Syndicate item that upgrades the flash protection of your eyes.
// The syndicate campaign G1c (D316): the polarized contacts.
// tg: code/modules/clothing/glasses/_glasses.dm:483
public partial class obj_item_syndicate_contacts : obj_item
{
    // tg: code/modules/clothing/glasses/_glasses.dm:490
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(user.get_organ_slot(ORGAN_SLOT_EYES)))
        {
            to_chat(user, span_warning(I18n.T("You have no eyes to apply the contacts to!")));
            return dot;
        }
        obj_item_organ_eyes eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;

        to_chat(user, span_notice(I18n.T("You begin applying the contact lenses to your eyes...")));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            return dot;
        to_chat(user, span_notice(I18n.T("The contacts seamlessly merge with your iris.")));
        eyes.flash_protect += FLASH_PROTECTION_WELDER;
        to_chat(user, span_warning(I18n.T("\\The [] disintegrates into nothing.", this)));
        qdel(this);
        return dot;
    }
}

// The syndicate campaign G1d (D316): the thermal scanners (TRAIT_THERMAL_VISION gives the wearer SEE_MOBS: the client draws the mobs
// behind the walls, DV374), the chameleon thermals (their chameleon action is the glasses' no-preset one), the x-ray goggles, the
// thermoncle and the thermal eyepatch.
// tg: code/modules/clothing/glasses/_glasses.dm:586
public partial class obj_item_clothing_glasses_thermal : obj_item_clothing_glasses
{
    // tg: code/modules/clothing/glasses/_glasses.dm:600
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await thermal_overload();
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:621
public partial class obj_item_clothing_glasses_thermal_monocle : obj_item_clothing_glasses_thermal
{
    // tg: code/modules/clothing/glasses/_glasses.dm:626
    public override async DmTask<object> examine(mob user = null, params object[] __extra) //Different examiners see a different description!
    {
        if (Dm.eq(user.gender, MALE))
            desc = replacetext(desc, "person", "man");
        else if (Dm.eq(user.gender, FEMALE))
            desc = replacetext(desc, "person", "woman");
        object dot = await base.examine(user, __extra);
        desc = Dm.initial(this, (obj_item_clothing_glasses_thermal_monocle m) => m.desc);
        return dot;
    }
}

// tg: code/modules/clothing/glasses/_glasses.dm:635
public partial class obj_item_clothing_glasses_thermal_eyepatch : obj_item_clothing_glasses_thermal
{
    // tg: code/modules/clothing/glasses/_glasses.dm:642
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        icon_state = Dm.eq(icon_state, base_icon_state) ? Dm.text("[]_flipped", base_icon_state) : Dm.str(base_icon_state);
        await user.update_worn_glasses();
        return dot;
    }
}
