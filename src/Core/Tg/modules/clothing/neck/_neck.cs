// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/neck/_neck.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /obj/item/clothing/neck's worn_overlays and separate_worn_overlays, /obj/item/clothing/neck/tie's
// worn_overlays and /obj/item/clothing/neck/petcollar's mob_can_equip translated (M4a clothing: neckwear is put on, taken
// off and drawn on the wearer). M8 slice D: the stethoscope's listening. J1 (D298): the ties whole (tying and untying, wearing above or
// below the suit; the screentip is a live-partial row). P1 (D298): the robe cape's surgery aid. The scarves, the collars' names, the necklaces and the other subtypes of this
// file are translated with their mechanics (M8 medicine, M9). Gaps CL1 (D333): the merchant's necklace (the export price of what it
// touches, or its sale for a holochip), the beads' random colour and the wreath (its glow is DV12).

// tg: code/modules/clothing/neck/_neck.dm:1
public partial class obj_item_clothing_neck : obj_item_clothing
{
    // tg: code/modules/clothing/neck/_neck.dm:11
    [DmParams("standing", "isinhands")]
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || !Dm.truthy(Dm.bitand(body_parts_covered, HEAD)))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damagedmask"));
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:18
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || !Dm.truthy(Dm.bitand(body_parts_covered, HEAD)))
            return dot;
        object blood_overlay = await get_blood_overlay("mask", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:50
public partial class obj_item_clothing_neck_tie : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(clip_on))
            update_appearance(UPDATE_ICON);
        register_context();
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The tie can be worn above or below your suit. Alt-Right-click to toggle.")));
        if (Dm.truthy(clip_on))
            Dm.append(ref dot, span_notice(I18n.T("Looking closely, you can see that it's actually a cleverly disguised clip-on.")));
        else if (!Dm.truthy(is_tied))
            Dm.append(ref dot, span_notice(I18n.T("The tie can be tied with Alt-Click.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The tie can be untied with Alt-Click.")));
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:87
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(clip_on))
            return NONE;
        to_chat(user, span_notice(I18n.T("You concentrate as you begin [] []...", Dm.truthy(is_tied) ? I18n.T("untying") : I18n.T("tying"), this)));
        float tie_timer_actual = tie_timer;
        // Mirrors give you a boost to your tying speed. I realize this stacks and I think that's hilarious.
        foreach (obj_structure_mirror reflection in Dm.each<obj_structure_mirror>(view(2, user)))
            tie_timer_actual *= 0.8f;
        // Heads of staff are experts at tying their ties.
        if (HAS_TRAIT(user, TRAIT_FAST_TYING))
            tie_timer_actual *= 0.5f;
        // Tie/Untie our tie
        if (!Dm.truthy(await do_after(user, tie_timer_actual)))
        {
            to_chat(user, span_notice(I18n.T("Your fingers fumble away from [] as your concentration breaks.", this)));
            return CLICK_ACTION_BLOCKING;
        }
        // Clumsy & Dumb people have trouble tying their ties.
        if ((HAS_TRAIT(user, TRAIT_CLUMSY) || HAS_TRAIT(user, TRAIT_DUMB)) && prob(50))
        {
            to_chat(user, span_notice(I18n.T("You just can't seem to get a proper grip on []!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        // Success!
        is_tied = Dm.truthy(is_tied) ? FALSE : TRUE;
        user.visible_message(
            span_notice(I18n.T("[] adjusts [] tie[].", user, user.p_their(), HAS_TRAIT(user, TRAIT_BALD) ? "" : I18n.T(" and runs a hand across [] head", user.p_their()))),
            span_notice(I18n.T("You successfully [] []!", Dm.truthy(is_tied) ? I18n.T("tied") : I18n.T("untied"), this))
        );
        update_appearance(UPDATE_ICON);
        await user.update_clothing(ITEM_SLOT_NECK);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/neck/_neck.dm:116
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        alternate_worn_layer = Dm.eq(alternate_worn_layer, Dm.initial(this, (obj_item p) => p.alternate_worn_layer)) ? NONE : Dm.initial(this, (obj_item p) => p.alternate_worn_layer);
        await user.update_clothing(ITEM_SLOT_NECK);
        balloon_alert(user, I18n.T("wearing [] suits", Dm.eq(alternate_worn_layer, Dm.initial(this, (obj_item p) => p.alternate_worn_layer)) ? I18n.T("below") : I18n.T("above")));
        return null;
    }

    // tg: code/modules/clothing/neck/_neck.dm:121
    [DmParams()]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(clip_on))
            return dot;
        // Normal strip & equip delay, along with 2 second self equip since you need to squeeze your head through the hole.
        if (Dm.truthy(is_tied))
        {
            icon_state = "tie_greyscale_tied";
            strip_delay = 4 * SECONDS;
            equip_delay_other = 4 * SECONDS;
            equip_delay_self = 2 * SECONDS;
        }
        else // Extremely quick strip delay, it's practically a ribbon draped around your neck
        {
            icon_state = "tie_greyscale_untied";
            strip_delay = 1 * SECONDS;
            equip_delay_other = 1 * SECONDS;
            equip_delay_self = 0 * SECONDS;
        }
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:148
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        mob_living_carbon_human wearer = loc as mob_living_carbon_human;
        if (!ishuman(wearer))
            return dot;
        obj_item_clothing_under undershirt = wearer.w_uniform as obj_item_clothing_under;
        if (!(undershirt is obj_item_clothing_under) || !Dm.truthy(LAZYLEN(undershirt.attached_accessories)))
            return dot;
        if (Dm.truthy(alternate_worn_layer))
            Dm.append(ref dot, undershirt.get_accessory_overlays());
        return dot;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:235
public partial class obj_item_clothing_neck_stethoscope : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:240
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, -3) //FISH DOCTOR?!
        Dm.Deferred("code/modules/clothing/neck/_neck.dm:242", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -3), the fishing difficulty (fishing, M10+)");
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:244
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] puts \\the [] to [] chest! It looks like [] won't hear much!", user, this, user.p_their(), user.p_they())));
        return OXYLOSS;
    }

    // tg: code/modules/clothing/neck/_neck.dm:248
    [DmParams("target", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living target = target_mob;
        if (!ishuman(target) || !isliving(user))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(user.combat_mode))
            return null;

        mob_living_carbon carbon_patient = target as mob_living_carbon;
        object body_part = carbon_patient.parse_zone_with_bodypart(user.zone_selected);
        float oxy_loss = Dm.num(carbon_patient.get_oxy_loss());

        string heart_strength;
        string pulse_pressure;
        bool having_heart_attack = Dm.truthy(carbon_patient.has_status_effect(typeof(datum_status_effect_heart_attack)));
        bool heart_noises = true;

        obj_item_organ_heart heart = carbon_patient.get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        obj_item_organ_lungs lungs = carbon_patient.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        obj_item_organ_liver liver = carbon_patient.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
        obj_item_organ_appendix appendix = carbon_patient.get_organ_slot(ORGAN_SLOT_APPENDIX) as obj_item_organ_appendix;

        DmList render_list = Dm.list(); //information will be packaged in a list for clean display to the user

        //determine what specific action we're taking
        if (Dm.eq(body_part, BODY_ZONE_CHEST)) //Listening to the chest
        {
            user.visible_message(span_notice(I18n.T("[] places [] against []'s [] and listens attentively.", user, this, carbon_patient, body_part)), ignored_mobs: user);
            if (HAS_TRAIT(user, TRAIT_DEAF))
            {
                to_chat(user, span_notice(I18n.T("You place [] against []'s []. Fat load of good it does you though, since you can't hear.", this, carbon_patient, body_part)));
                return null;
            }
            Dm.append(ref render_list, span_info(I18n.T("You place [] against []'s []:\n", this, carbon_patient, body_part)));

            //assess breathing
            bool lung_noises = true;
            if (lungs == null
                || carbon_patient.stat == DEAD
                || HAS_TRAIT(carbon_patient, TRAIT_FAKEDEATH)
                || HAS_TRAIT(carbon_patient, TRAIT_NOBREATH)
                || Dm.truthy(carbon_patient.failed_last_breath)
                || Dm.truthy(carbon_patient.losebreath)) //If pt is dead or otherwise not breathing
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>[] not breathing!</span>\n", target.p_Theyre()));
                lung_noises = false;
            }
            else if (Dm.num(lungs.damage) > 10) //if breathing, check for lung damage
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You hear fluid in [] lungs!</span>\n", target.p_their()));
            }
            else if (oxy_loss > 10) //if they have suffocation damage
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>[] breathing heavily!</span>\n", target.p_Theyre()));
            }
            else
            {
                Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>[] breathing normally.</span>\n", target.p_Theyre())); //they're okay :D
            }
            if (lung_noises)
                Dm.append(ref render_list, Dm.text("<span class='notice ml-1'>[]</span>\n", await lungs.hear_breath_noise(user)));
            //assess heart
            if (Dm.eq(body_part, BODY_ZONE_CHEST)) //if we're listening to the chest
            {
                if (heart == null || !Dm.truthy(heart.is_beating()) || carbon_patient.stat == DEAD)
                {
                    Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You don't hear a heartbeat!</span>\n")); //they're dead or their heart isn't beating
                    heart_noises = false;
                }
                else if (having_heart_attack)
                {
                    Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You hear a rapid, irregular heartbeat.</span>\n"));
                }
                else if (Dm.num(heart.damage) > 10 || Dm.num(carbon_patient.get_blood_volume(apply_modifiers: TRUE)) <= BLOOD_VOLUME_OKAY)
                {
                    Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You hear a weak heartbeat.</span>\n")); //their heart is damaged, or they have critical blood
                }
                else
                {
                    Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>You hear a healthy heartbeat.</span>\n")); //they're okay :D
                }
                if (heart_noises)
                    Dm.append(ref render_list, Dm.text("<span class='notice ml-1'>[]</span>\n", await heart.hear_beat_noise(user)));
            }
        }
        else if (Dm.eq(body_part, BODY_ZONE_PRECISE_GROIN)) //If we're targeting the groin
        {
            Dm.append(ref render_list, span_info(I18n.T("You carefully press down on []'s abdomen:\n", carbon_patient)));
            user.visible_message(span_notice(I18n.T("[] presses their hands against []'s abdomen.", user, carbon_patient)), ignored_mobs: user);

            //assess abdominal organs
            bool appendix_okay = true;
            bool liver_okay = true;
            if (liver == null) //sanity check, ensure the patient actually has a liver
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You can't feel anything where [] liver would be.</span>\n", target.p_their()));
                liver_okay = false;
            }
            else if (Dm.num(liver.damage) > 10)
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>[] liver feels firm.</span>\n", target.p_Their())); //their liver is damaged
                liver_okay = false;
            }
            if (appendix == null) //sanity check, ensure the patient actually has an appendix
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You can't feel anything where [] appendix would be.</span>\n", target.p_their()));
                appendix_okay = false;
            }
            else if (Dm.num(appendix.damage) > 10 && !IS_UNCONSCIOUS_OR_CRIT(carbon_patient))
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>[] screams when you lift your hand from [] appendix!</span>\n", target, target.p_their())); //scream if their appendix is damaged and they're awake
                await target.emote("scream");
                appendix_okay = false;
            }
            if (liver_okay && appendix_okay) //if they have all their organs and have no detectable damage
                Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>You don't find anything abnormal.</span>\n")); //they're okay :D
        }
        else if (Dm.eq(body_part, BODY_ZONE_PRECISE_EYES) || Dm.eq(body_part, BODY_ZONE_PRECISE_MOUTH))
        {
            balloon_alert(user, I18n.T("can't do that!"));
            return null;
        }
        else //targeting an extremity or the head
        {
            if (Dm.eq(body_part, BODY_ZONE_HEAD))
            {
                Dm.append(ref render_list, span_info(I18n.T("You carefully press your fingers to []'s neck:\n", carbon_patient)));
                user.visible_message(span_notice(I18n.T("[] presses their fingers against []'s neck.", user, carbon_patient)), ignored_mobs: user);
            }
            else
            {
                Dm.append(ref render_list, span_info(I18n.T("You carefully press your fingers to []'s []:\n", carbon_patient, body_part)));
                user.visible_message(span_notice(I18n.T("[] presses their fingers against []'s [].", user, carbon_patient, body_part)), ignored_mobs: user);
            }

            float cached_blood_volume = Dm.num(carbon_patient.get_blood_volume(apply_modifiers: TRUE));

            //assess pulse (heart & blood level)
            if (heart == null || !Dm.truthy(heart.is_beating()) || cached_blood_volume <= BLOOD_VOLUME_OKAY || carbon_patient.stat == DEAD)
            {
                Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>You can't find a pulse!</span>\n")); //they're dead, their heart isn't beating, or they have critical blood
            }
            else
            {
                if (having_heart_attack)
                    heart_strength = span_danger(I18n.T("irregular"));
                else if (Dm.num(heart.damage) > 10)
                    heart_strength = span_danger(I18n.T("weak")); //their heart is damaged
                else
                    heart_strength = span_notice(I18n.T("regular")); //they're okay :D

                if ((cached_blood_volume <= BLOOD_VOLUME_SAFE && cached_blood_volume > BLOOD_VOLUME_OKAY) || having_heart_attack)
                    pulse_pressure = span_danger(I18n.T("thready")); //low blood
                else
                    pulse_pressure = span_notice(I18n.T("strong")); //they're okay :D

                Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>[] pulse is [] and [].</span>\n", target.p_Their(), pulse_pressure, heart_strength));
            }
        }

        //display our packaged information in an examine block for easy reading
        to_chat(user, boxed_message(jointext(render_list, "")), type: MESSAGE_TYPE_INFO);
        return null;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:495
public partial class obj_item_clothing_neck_petcollar : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:514
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        tagname = await sanitize_name(await tgui_input_text(user, "Would you like to change the name on the tag?", "Pet Naming", "Spot", MAX_NAME_LEN));
        if (!Dm.truthy(tagname) || length(tagname) == 0)
        {
            name = initial(this, "name") as string;
            tagname = null;
            return null;
        }
        name = Dm.text("[] - []", initial(this, "name"), tagname);
        return null;
    }

    // tg: code/modules/clothing/neck/_neck.dm:506
    [DmParams("mob", "slot", "disable_warning", "bypass_equip_delay_self", "ignore_equipped", "indirect_action")]
    public override async DmTask<object> mob_can_equip(mob_living mob = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        disable_warning ??= FALSE;
        bypass_equip_delay_self ??= FALSE;
        ignore_equipped ??= FALSE;
        indirect_action ??= FALSE;
        if (!HAS_TRAIT(mob, TRAIT_LESSER_HUMANOID) && !Dm.truthy(human_wearable))
            return FALSE;
        return await base.mob_can_equip(mob, slot, disable_warning, bypass_equip_delay_self, ignore_equipped, indirect_action);
    }
}

// tg: code/modules/clothing/neck/_neck.dm:205
public partial class obj_item_clothing_neck_robe_cape : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_surgery_aid), "cape"));
        return dot;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:536
public partial class obj_item_clothing_neck_necklace_dope_merchant : obj_item_clothing_neck_necklace_dope
{
    // tg: code/modules/clothing/neck/_neck.dm:543
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        selling = Dm.num(!Dm.truthy(selling));
        to_chat(user, span_notice(I18n.T("[] has been set to [] mode.", this, Dm.truthy(selling) ? I18n.T("'Sell'") : I18n.T("'Get Price'"))));
        return dot;
    }

    // tg: code/modules/clothing/neck/_neck.dm:548
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        datum_export_report ex = await export_item_and_contents(interacting_with as atom_movable, delete_unsold: selling, dry_run: Dm.num(!Dm.truthy(selling)));
        float price = 0;
        foreach (object x in Dm.each(ex.total_amount))
            price += Dm.num(ex.total_value[x]);

        if (price != 0)
        {
            float true_price = round(price * profit_scaling);
            float fee_display = round(price - true_price);
            // MONEY_NAME_AUTOPURAL(fee_display) (__DEFINES/economy.dm:99), inline (rules 11.1)
            to_chat(user, span_notice(I18n.T("[] [], value: <b>[]</b> [][].[]",
                Dm.truthy(selling) ? I18n.T("Sold") : I18n.T("Getting the price of"),
                interacting_with,
                true_price,
                MONEY_NAME,
                Dm.truthy(interacting_with.contents.len) ? I18n.T(" (exportable contents included)") : "",
                profit_scaling < 1 && Dm.truthy(selling) ? I18n.T("<b>[]</b> [] taken as processing fee\\s.", fee_display, Dm.text("credit[]", fee_display == 1 ? "" : "s")) : "")));
            if (Dm.truthy(selling))
                Dm.@new<obj_item_holochip>(get_turf(user), true_price);
        }
        else
            to_chat(user, span_warning(I18n.T("There is no export value for [] or any items within it.", interacting_with)));

        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:565
public partial class obj_item_clothing_neck_beads : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:574
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        color = color = pick("#ff0077", "#d400ff", "#2600ff", "#00ccff", "#00ff2a", "#e5ff00", "#ffae00", "#ff0000", "#ffffff");
        return dot;
    }
}

// tg: code/modules/clothing/neck/_neck.dm:578
public partial class obj_item_clothing_neck_wreath : obj_item_clothing_neck
{
    // tg: code/modules/clothing/neck/_neck.dm:587
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "wreath_emissive", src, alpha = src.alpha): the wreath's
        // glow on the emissive plane is not created
        return dot;
    }
}
