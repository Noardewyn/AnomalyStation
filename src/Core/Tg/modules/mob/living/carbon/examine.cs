// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/examine.dm (M9 slice G: the examine of a carbon and a
// human: what is worn and held, the injuries, the death text, the status effects, the HUD lines and the closer look)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The chat icons (icon2html, __HELPERS/icons.dm) are not declared: the client's chat is text, so an examine line names the item
// without its picture (the leaf atom_examine.cs's get_examine_icon defers).

// tg: code/modules/mob/living/carbon/examine.dm:4
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/examine.dm:4
    public override object get_examine_icon(mob user = null)
    {
        return null;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:7
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/examine.dm:2  #define ADD_NEWLINE_IF_NECESSARY(list)
    private static void ADD_NEWLINE_IF_NECESSARY(DmList list)
    {
        if (Dm.length(list) > 0 && Dm.truthy(list[Dm.length(list)]))
            Dm.append(ref list, "");
    }

    // tg: code/modules/mob/living/carbon/examine.dm:7
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE) && !isobserver(user))
            return Dm.list(span_warning(I18n.T("You're struggling to make out any details...")));

        object t_He = p_They();
        object t_His = p_Their();
        object t_his = p_their();
        object t_him = p_them();
        object t_has = p_have();
        object t_is = p_are();

        DmList dot = Dm.list();
        Dm.append(ref dot, get_clothing_examine_info(user as mob_living));
        // give us some space between clothing examine and the rest
        ADD_NEWLINE_IF_NECESSARY(dot);

        bool appears_dead = false;
        bool just_sleeping = false;

        if (IS_DEAD_OR_FAKING(this))
        {
            appears_dead = true;

            obj_item_clothing_glasses shades = get_item_by_slot(ITEM_SLOT_EYES) as obj_item_clothing_glasses;
            bool are_we_in_weekend_at_bernies = Dm.truthy(shades?.tint) && Dm.truthy(buckled) && buckled is obj_vehicle_ridden_wheelchair;

            if (isliving(user) && (HAS_MIND_TRAIT(user, TRAIT_NAIVE) || are_we_in_weekend_at_bernies))
                just_sleeping = true;

            if (!just_sleeping)
            {
                // since this is relatively important and giving it space makes it easier to read
                ADD_NEWLINE_IF_NECESSARY(dot);
                if (HAS_TRAIT(this, TRAIT_SUICIDED))
                    Dm.append(ref dot, span_warning(I18n.T("[] appear[] to have committed suicide... there is no hope of recovery.", t_He, p_s())));

                Dm.append(ref dot, generate_death_examine_text());
            }
        }

        //Status effects
        object status_examines = get_status_effect_examinations(user);
        if (Dm.truthy(Dm.length(status_examines)))
            Dm.append(ref dot, status_examines);

        if (Dm.truthy(get_bodypart(BODY_ZONE_HEAD)) && !Dm.truthy(get_organ_by_type(typeof(obj_item_organ_brain))))
            Dm.append(ref dot, span_deadsay(I18n.T("It appears that [] brain is missing...", t_his)));

        DmList disabled = Dm.list();
        foreach (obj_item_bodypart body_part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(body_part.bodypart_disabled))
                Dm.append(ref disabled, body_part);

            bool treatment_distance = isliving(user) && get_dist(this, user) <= CARBON_EXAMINE_EMBEDDING_MAX_DIST;
            foreach (obj_item embedded in Dm.each_cast<obj_item>(body_part.embedded_objects))
            {
                datum_embedding embed = (datum_embedding)embedded.get_embed();
                if (Dm.truthy(embed.stealthy_embed))
                    continue;
                bool harmless = Dm.truthy(embed.is_harmless());
                string stuck_wordage = harmless ? "stuck to" : "embedded in";
                string embed_line = I18n.T("\\a []", embedded);
                if (treatment_distance)
                    embed_line = I18n.T("<a href='byond://?src=[];embedded_object=[];embedded_limb=[]'>\\a []</a>", REF(this), REF(embedded), REF(body_part), embedded);
                string embed_text = I18n.T("[] [] [] [] [] []!", t_He, t_has, embed_line, stuck_wordage, t_his, body_part.plaintext_zone);
                if (harmless)
                    Dm.append(ref dot, span_italics(span_notice(embed_text)));
                else
                    Dm.append(ref dot, span_boldwarning(embed_text));
            }

            obj_item_tourniquet current_tourniquet = LAZYACCESS(body_part.applied_items, LIMB_ITEM_TOURNIQUET) as obj_item_tourniquet;
            if (current_tourniquet != null)
            {
                string tourniquet_href = I18n.T("\\a []", current_tourniquet);
                if (treatment_distance)
                    tourniquet_href = I18n.T("<a href='byond://?src=[];remove_tourniquet=[]'>[]</a>", REF(this), REF(body_part), tourniquet_href);
                string tourniquet_msg = I18n.T("[] [] [] tightly secured around [] [].", t_He, t_has, tourniquet_href, t_his, Dm.eq(body_part.body_zone, BODY_ZONE_HEAD) ? "neck" : body_part.plaintext_zone);
                if (Dm.eq(body_part.body_zone, BODY_ZONE_HEAD))
                    Dm.append(ref dot, span_boldwarning(tourniquet_msg));
                else
                    Dm.append(ref dot, span_notice(tourniquet_msg));
            }

            obj_item_stack_medical_wrap current_gauze = LAZYACCESS(body_part.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
            if (current_gauze != null)
            {
                string gauze_href = Dm.str(current_gauze.name);
                if (treatment_distance) // only shows the href if we're adjacent
                    gauze_href = I18n.T("<a href='byond://?src=[];gauze_limb=[]'>[]</a>", REF(this), REF(body_part), gauze_href);
                Dm.append(ref dot, span_notice(I18n.T("There is some [] wrapped around [] [].", gauze_href, t_his, body_part.plaintext_zone)));
            }

            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(body_part.wounds))
            {
                if (iter_wound.examine_desc == null)
                    continue;
                Dm.append(ref dot, span_danger(Dm.str(iter_wound.get_examine_description(user))));
            }

            object surgery_examine = body_part.get_surgery_examine();
            if (Dm.truthy(surgery_examine))
                Dm.append(ref dot, surgery_examine);
        }

        foreach (obj_item_bodypart body_part in Dm.each_cast<obj_item_bodypart>(disabled))
        {
            object damage_text;
            if (HAS_TRAIT(body_part, TRAIT_DISABLED_BY_WOUND))
                continue; // skip if it's disabled by a wound (cuz we'll be able to see the bone sticking out!)
            if (Dm.num(body_part.get_damage()) < Dm.num(body_part.max_damage)) //we don't care if it's stamcritted
                damage_text = "limp and lifeless";
            else
                damage_text = (body_part.brute_dam >= body_part.burn_dam) ? body_part.heavy_brute_msg : body_part.heavy_burn_msg;
            Dm.append(ref dot, span_boldwarning(I18n.T("[] [] looks []!", capitalize(t_his), body_part.plaintext_zone, damage_text)));
        }

        //stores missing limbs
        int l_limbs_missing = 0;
        int r_limbs_missing = 0;
        foreach (object missing_limb in Dm.each(get_missing_limbs()))
        {
            if (Dm.eq(missing_limb, BODY_ZONE_HEAD))
            {
                Dm.append(ref dot, span_deadsay(I18n.T("<B>[] [] is missing!</B>", t_His, parse_zone(missing_limb))));
                continue;
            }
            if (Dm.eq(missing_limb, BODY_ZONE_L_ARM) || Dm.eq(missing_limb, BODY_ZONE_L_LEG))
                l_limbs_missing++;
            else if (Dm.eq(missing_limb, BODY_ZONE_R_ARM) || Dm.eq(missing_limb, BODY_ZONE_R_LEG))
                r_limbs_missing++;

            Dm.append(ref dot, span_boldwarning(I18n.T("[] [] is missing!", capitalize(t_his), parse_zone(missing_limb))));
        }

        if (l_limbs_missing >= 2 && r_limbs_missing == 0)
            Dm.append(ref dot, span_tinydanger(I18n.T("[] look[] all right now...", t_He, p_s())));
        else if (l_limbs_missing == 0 && r_limbs_missing >= 2)
            Dm.append(ref dot, span_tinydanger(I18n.T("[] really keep[] to the left...", t_He, p_s())));
        else if (l_limbs_missing >= 2 && r_limbs_missing >= 2)
            Dm.append(ref dot, span_tinydanger(I18n.T("[] []n't seem all there...", t_He, p_do())));

        if (!(user == this && Dm.truthy(has_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy))))) //fake healthy
        {
            float temp;
            if (user == this && Dm.truthy(has_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_crit))))//fake damage
                temp = 50;
            else
                temp = Dm.num(get_brute_loss());
            DmList damage_desc = get_majority_bodypart_damage_desc() as DmList;
            if (Dm.truthy(temp))
            {
                if (temp < 25)
                    Dm.append(ref dot, span_danger(I18n.T("[] [] minor [].", t_He, t_has, damage_desc[BRUTE])));
                else if (temp < 50)
                    Dm.append(ref dot, span_danger(I18n.T("[] [] <b>moderate</b> []!", t_He, t_has, damage_desc[BRUTE])));
                else
                    Dm.append(ref dot, span_bolddanger(I18n.T("[] [] severe []!", t_He, t_has, damage_desc[BRUTE])));
            }

            temp = Dm.num(get_fire_loss());
            if (Dm.truthy(temp))
            {
                if (temp < 25)
                    Dm.append(ref dot, span_danger(I18n.T("[] [] minor [].", t_He, t_has, damage_desc[BURN])));
                else if (temp < 50)
                    Dm.append(ref dot, span_danger(I18n.T("[] [] <b>moderate</b> []!", t_He, t_has, damage_desc[BURN])));
                else
                    Dm.append(ref dot, span_bolddanger(I18n.T("[] [] severe []!", t_He, t_has, damage_desc[BURN])));
            }
        }

        if (Dm.truthy(pulledby?.grab_state))
            Dm.append(ref dot, span_warning(I18n.T("[] [] restrained by []'s grip.", t_He, t_is, pulledby)));

        if (nutrition < NUTRITION_LEVEL_STARVING - 50)
            Dm.append(ref dot, span_warning(I18n.T("[] [] severely malnourished.", t_He, t_is)));
        else if (nutrition >= NUTRITION_LEVEL_FAT)
        {
            if (Dm.num(user.nutrition) < NUTRITION_LEVEL_STARVING - 50)
                Dm.append(ref dot, span_hypnophrase(I18n.T("[] [] plump and delicious looking - Like a fat little piggy. A tasty piggy.", t_He, t_is)));
            else
                Dm.append(ref dot, I18n.T("<b>[] [] quite chubby.</b>", t_He, t_is));
        }
        if (disgust >= DISGUST_LEVEL_GROSS && disgust <= DISGUST_LEVEL_VERYGROSS)
            Dm.append(ref dot, I18n.T("[] look[] a bit grossed out.", t_He, p_s()));
        else if (disgust >= DISGUST_LEVEL_VERYGROSS && disgust <= DISGUST_LEVEL_DISGUSTED)
            Dm.append(ref dot, I18n.T("[] look[] really grossed out.", t_He, p_s()));
        else if (disgust >= DISGUST_LEVEL_DISGUSTED)
            Dm.append(ref dot, I18n.T("[] look[] extremely disgusted.", t_He, p_s()));

        float apparent_blood_volume = Dm.truthy(CAN_HAVE_BLOOD(this)) ? Dm.num(get_blood_volume(apply_modifiers: TRUE)) : BLOOD_VOLUME_NORMAL;
        if (HAS_TRAIT(this, TRAIT_USES_SKINTONES) && ishuman(this))
        {
            mob_living_carbon_human husrc = (mob_living_carbon_human)this; // gross istypesrc but easier than refactoring even further for now
            if (husrc.skin_tone == "albino")
                apparent_blood_volume -= (BLOOD_VOLUME_NORMAL * 0.25f); // knocks you down a few pegs
        }
        if (apparent_blood_volume >= BLOOD_VOLUME_OKAY && apparent_blood_volume <= BLOOD_VOLUME_SAFE)
            Dm.append(ref dot, span_warning(I18n.T("[] [] pale skin.", t_He, t_has)));
        else if (apparent_blood_volume >= BLOOD_VOLUME_BAD && apparent_blood_volume <= BLOOD_VOLUME_OKAY)
            Dm.append(ref dot, span_boldwarning(I18n.T("[] look[] like pale death.", t_He, p_s())));
        else if (apparent_blood_volume <= BLOOD_VOLUME_BAD)
            Dm.append(ref dot, span_deadsay(I18n.T("<b>[] resemble[] a crushed, empty juice pouch.</b>", t_He, p_s())));

        if (Dm.truthy(is_bleeding()))
        {
            DmList bleeding_limbs = Dm.list();
            DmList grasped_limbs = Dm.list();

            foreach (obj_item_bodypart body_part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            {
                if (Dm.truthy(body_part.cached_bleed_rate))
                    Dm.append(ref bleeding_limbs, body_part.plaintext_zone);
                if (body_part.grasped_by != null)
                    Dm.append(ref grasped_limbs, body_part.plaintext_zone);
            }

            if (Dm.truthy(LAZYLEN(bleeding_limbs)))
            {
                string bleed_text = "<b>";
                if (appears_dead)
                {
                    bleed_text += "<span class='deadsay'>";
                    bleed_text += I18n.T("Blood is visible in [] open ", t_his);
                }
                else
                {
                    bleed_text += "<span class='warning'>";
                    bleed_text += I18n.T("[] [] bleeding from [] ", t_He, t_is, t_his);
                }

                bleed_text += Dm.str(english_list(bleeding_limbs, and_text: " and "));

                if (appears_dead)
                    bleed_text += I18n.T(", but it has pooled and is not flowing.");
                else
                {
                    if (HAS_TRAIT(this, TRAIT_BLOOD_FOUNTAIN))
                        bleed_text += I18n.T(" incredibly quickly");
                    bleed_text += "!";
                }

                if (appears_dead)
                    bleed_text += "<span class='deadsay'>";
                else
                    bleed_text += "<span class='warning'>";
                bleed_text += "</b>";

                Dm.append(ref dot, bleed_text);
                if (Dm.truthy(LAZYLEN(grasped_limbs)))
                {
                    foreach (object grasped_part in Dm.each(grasped_limbs))
                        Dm.append(ref dot, I18n.T("[] [] holding [] [] to slow the bleeding!", t_He, t_is, t_his, grasped_part));
                }
            }
        }

        if (Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_teslium), needs_metabolizing: TRUE)))
            Dm.append(ref dot, span_smallnoticeital(I18n.T("[] [] emitting a gentle blue glow!", t_He, t_is))); // this should be signalized

        mob_living living_user = user as mob_living;
        if (Dm.truthy(living_user?._listen_lookup?[COMSIG_CARBON_MID_EXAMINE])) // Adds examine text after clothing and wounds but before death and scars
            living_user._SendSignal(COMSIG_CARBON_MID_EXAMINE, Dm.list(living_user, this, dot));
        if (just_sleeping)
            Dm.append(ref dot, span_notice(I18n.T("[] []n't responding to anything around [] and seem[] to be asleep.", t_He, t_is, t_him, p_s())));
        else if (!appears_dead)
        {
            if (this != user)
            {
                if (HAS_TRAIT(user, TRAIT_SPIRITUAL) && Dm.truthy(mind?.holy_role) && user != this)
                {
                    Dm.append(ref dot, I18n.T("[] [] a holy aura about [].", t_He, t_has, t_him));
                    await living_user.add_mood_event("religious_comfort", typeof(datum_mood_event_religiously_comforted));
                }
            }

            if (IS_UNCONSCIOUS(this))
                Dm.append(ref dot, span_notice(I18n.T("[] []n't responding to anything around [] and seem[] to be asleep.", t_He, t_is, t_him, p_s())));
            else if (stat == SOFT_CRIT)
                Dm.append(ref dot, span_notice(I18n.T("[] [] barely conscious.", t_He, t_is)));
            else if (HAS_TRAIT(this, TRAIT_DUMB))
                Dm.append(ref dot, I18n.T("[] [] a stupid expression on [] face.", t_He, t_has, t_his));

            obj_item_organ_brain brain = get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
            if (brain != null && ai_controller == null)
            {
                string npc_message = "";
                if (HAS_TRAIT(brain, TRAIT_GHOSTROLE_ON_REVIVE) || HAS_TRAIT(this, TRAIT_GHOSTROLE_ON_REVIVE))
                    npc_message = I18n.T("Soul is pending...");
                else if (!Dm.truthy(key))
                    npc_message = I18n.T("[] [] totally catatonic. The stresses of life in deep-space must have been too much for []. Any recovery is unlikely.", t_He, t_is, t_him);
                else if (!Dm.truthy(client))
                    npc_message = I18n.T("[] [] a blank, absent-minded stare and appears completely unresponsive to anything. [] may snap out of it soon.", t_He, t_has, t_He);
                if (Dm.truthy(npc_message))
                {
                    // give some space since this is usually near the end
                    ADD_NEWLINE_IF_NECESSARY(dot);
                    Dm.append(ref dot, span_deadsay(npc_message));
                }
            }
        }

        float scar_severity = 0;
        foreach (datum_scar scar in Dm.each_cast<datum_scar>(all_scars))
        {
            if (Dm.truthy(scar.is_visible(user)))
                scar_severity += Dm.num(scar.severity);
        }

        if (scar_severity >= 1)
        {
            // give some space since this is even more usually near the end
            ADD_NEWLINE_IF_NECESSARY(dot);
            if (scar_severity <= 4)
                Dm.append(ref dot, span_tinynoticeital(I18n.T("[] [] visible scarring, you can look again to take a closer look...", t_He, t_has)));
            else if (scar_severity >= 5 && scar_severity <= 8)
                Dm.append(ref dot, span_smallnoticeital(I18n.T("[] [] several bad scars, you can look again to take a closer look...", t_He, t_has)));
            else if (scar_severity >= 9 && scar_severity <= 11)
                Dm.append(ref dot, span_notice(I18n.T("<i>[] [] significantly disfiguring scarring, you can look again to take a closer look...</i>", t_He, t_has)));
            else if (scar_severity >= 12)
                Dm.append(ref dot, span_notice(I18n.T("<b><i>[] [] just absolutely fucked up, you can look again to take a closer look...</i></b>", t_He, t_is)));
        }

        if (HAS_TRAIT(this, TRAIT_HUSK))
        {
            if (HAS_TRAIT_FROM(this, TRAIT_HUSK, Dm.initial(typeof(datum_status_effect_zombie), (datum_status_effect_zombie p) => p.id)))
                Dm.append(ref dot, span_warning(I18n.T("[] skin has rotted into a sickly green color.", t_His))); // future todo: lizards don't have skin, they have scales
            else
                Dm.append(ref dot, span_warning(I18n.T("This body has been reduced to a grotesque husk.")));
        }
        if (HAS_MIND_TRAIT(user, TRAIT_MORBID))
        {
            if (HAS_TRAIT(this, TRAIT_DISSECTED))
                Dm.append(ref dot, span_notice(I18n.T("[] appear[] to have been dissected. Useless for examination... <b><i>for now.</i></b>", t_He, p_s())));
            if (HAS_TRAIT(this, TRAIT_SURGICALLY_ANALYZED))
                Dm.append(ref dot, span_notice(I18n.T("A skilled hand has mapped this one's internal intricacies. It will be far easier to perform future experimentations upon []. <b><i>Exquisite.</i></b>", user.p_them())));
        }
        if (isliving(user) && HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FITNESS))
            Dm.append(ref dot, await compare_fitness(living_user));

        object hud_info = get_hud_examine_info(user as mob_living);
        if (Dm.truthy(Dm.length(hud_info)))
            Dm.append(ref dot, hud_info);

        if (isobserver(user))
        {
            ADD_NEWLINE_IF_NECESSARY(dot);
            Dm.append(ref dot, I18n.T("<b>Quirks:</b> []", get_quirk_string(FALSE, CAT_QUIRK_ALL)));
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXAMINE]))
            this._SendSignal(COMSIG_ATOM_EXAMINE, Dm.list(this, user, dot));
        if (Dm.truthy(Dm.length(dot)))
        {
            dot[1] = "<span class='info'>" + Dm.str(dot[1]);
            dot[Dm.length(dot)] = Dm.str(dot[Dm.length(dot)]) + "</span>";
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/examine.dm:303
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (HAS_TRAIT(this, TRAIT_INVISIBLE_MAN))
            return dot;
        foreach (datum_scar iter_scar in Dm.each_cast<datum_scar>(all_scars))
        {
            if (Dm.truthy(iter_scar.is_visible(user)))
                Dm.append(ref dot, iter_scar.get_examine_description(user));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:314
public partial class mob_living : mob
{
    /**
     * Shows any and all examine text related to any status effects the user has.
     */
    // tg: code/modules/mob/living/carbon/examine.dm:314
    public virtual object get_status_effect_examinations(mob examiner = null)
    {
        DmList examine_list = Dm.list();

        foreach (datum_status_effect effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            object effect_text = effect.get_examine_text(examiner);
            if (!Dm.truthy(effect_text))
                continue;

            Dm.append(ref examine_list, effect_text);
        }

        if (!Dm.truthy(Dm.length(examine_list)))
            return null;

        return examine_list.Join("<br>");
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:330
public partial class mob_living_carbon : mob_living
{
    /// Returns death message for mob examine text
    // tg: code/modules/mob/living/carbon/examine.dm:330
    public virtual object generate_death_examine_text()
    {
        mob_dead_observer ghost = get_ghost(TRUE, TRUE) as mob_dead_observer;
        object t_He = p_They();
        object t_his = p_their();
        object t_is = p_are();
        //This checks to see if the body is revivable
        obj_item_organ brain = get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ;
        if ((brain != null && HAS_TRAIT(brain, TRAIT_GHOSTROLE_ON_REVIVE)) || HAS_TRAIT(this, TRAIT_GHOSTROLE_ON_REVIVE))
            return span_deadsay(I18n.T("[] [] limp and unresponsive; there are no signs of life, but another soul may take [] place...", t_He, t_is, t_his));
        bool client_like = Dm.truthy(client) || HAS_TRAIT(this, TRAIT_MIND_TEMPORARILY_GONE);
        bool valid_ghost = Dm.truthy(ghost?.can_reenter_corpse) && Dm.truthy(ghost?.client);
        bool valid_soul = brain != null || !HAS_TRAIT(this, TRAIT_FAKE_SOULLESS);
        if ((brain != null && client_like) || (valid_ghost && valid_soul))
            return span_deadsay(I18n.T("[] [] limp and unresponsive; there are no signs of life...", t_He, t_is));
        return span_deadsay(I18n.T("[] [] limp and unresponsive; there are no signs of life and [] soul has departed...", t_He, t_is, t_his));
    }

    /// Returns a list of "damtype" => damage description based off of which bodypart description is most common
    // tg: code/modules/mob/living/carbon/examine.dm:347
    public virtual object get_majority_bodypart_damage_desc()
    {
        DmList seen_damage = Dm.list(); // This looks like: ({Damage type} = list({Damage description for that damage type} = {number of times it has appeared}, ...), ...)
        DmList most_seen_damage = Dm.list(); // This looks like: ({Damage type} = {Frequency of the most common description}, ...)
        DmList final_descriptions = Dm.list(); // This looks like: ({Damage type} = {Most common damage description for that type}, ...)
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            foreach (object damage_type in Dm.each(part.damage_examines))
            {
                object damage_desc = part.damage_examines[damage_type];
                if (!Dm.truthy(seen_damage[damage_type]))
                    seen_damage[damage_type] = Dm.list();

                DmList seen = (DmList)seen_damage[damage_type];
                if (!Dm.truthy(seen[damage_desc]))
                    seen[damage_desc] = 1;
                else
                    seen[damage_desc] = Dm.num(seen[damage_desc]) + 1;

                if (Dm.num(seen[damage_desc]) > Dm.num(most_seen_damage[damage_type]))
                {
                    most_seen_damage[damage_type] = seen[damage_desc];
                    final_descriptions[damage_type] = damage_desc;
                }
            }
        }
        return final_descriptions;
    }

    /// Coolects examine information about the mob's clothing and equipment
    // tg: code/modules/mob/living/carbon/examine.dm:368
    public virtual object get_clothing_examine_info(mob_living user = null)
    {
        DmList dot = Dm.list();
        object t_He = p_They();
        object t_His = p_Their();
        object t_his = p_their();
        object t_has = p_have();
        object t_is = p_are();
        //Hands
        foreach (obj_item held_thing in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(Dm.bitand(held_thing.item_flags, Dm.bitor(ABSTRACT, HAND_ITEM))) || HAS_TRAIT(held_thing, TRAIT_EXAMINE_SKIP))
                continue;
            Dm.append(ref dot, I18n.T("[] [] holding [] in [] [].", t_He, t_is, held_thing.examine_title(user), t_his, get_held_index_name(get_held_index_of_item(held_thing))));
        }
        foreach (obj_item_bodypart_arm part in Dm.each<obj_item_bodypart_arm>(get_bodyparts()))
        {
            if (!Dm.truthy(Dm.bitand(part.bodypart_flags, BODYPART_PSEUDOPART)))
                continue;
            obj_item corresponding_item = (get_item_for_held_index(part.held_index) as obj_item) ?? part;
            Dm.append(ref dot, I18n.T("[] [] a [] in place of [] [].", t_He, t_has, corresponding_item.examine_title(user), t_his, Dm.initial(part, (obj_item_bodypart_arm p) => p.plaintext_zone)));
        }
        //handcuffed?
        if (Dm.truthy(handcuffed))
        {
            string cables_or_cuffs = handcuffed is obj_item_restraints_handcuffs_cable ? "restrained with cable" : "handcuffed";
            Dm.append(ref dot, span_warning(I18n.T("[] [] []!", t_He, t_is, cables_or_cuffs)));
        }
        //eyes
        if (!Dm.truthy(Dm.bitand(obscured_slots, HIDEEYES)))
        {
            if (HAS_TRAIT(this, TRAIT_UNNATURAL_RED_GLOWY_EYES))
                Dm.append(ref dot, span_warning(I18n.T("<B>[] eyes are glowing with an unnatural red aura!</B>", t_His)));
            else if (HAS_TRAIT(this, TRAIT_BLOODSHOT_EYES))
                Dm.append(ref dot, span_warning(I18n.T("<B>[] eyes are bloodshot!</B>", t_His)));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:397
public partial class mob_living_carbon_human : mob_living_carbon
{
    // Yes there's a lot of copypasta here, we can improve this later when carbons are less dumb in general
    // tg: code/modules/mob/living/carbon/examine.dm:397
    public override object get_clothing_examine_info(mob_living user = null)
    {
        DmList dot = Dm.list();
        object t_He = p_They();
        object t_His = p_Their();
        object t_his = p_their();
        object t_has = p_have();
        object t_is = p_are();

        //uniform
        if (Dm.truthy(w_uniform) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEJUMPSUIT)) && !HAS_TRAIT(w_uniform, TRAIT_EXAMINE_SKIP))
        {
            //accessory
            string accessory_message = "";
            if (w_uniform is obj_item_clothing_under undershirt)
            {
                DmList accessories = undershirt.list_accessories_with_icon(user) as DmList;
                if (Dm.truthy(Dm.length(accessories)))
                    accessory_message = I18n.T(" with [] attached", english_list(accessories));
            }

            Dm.append(ref dot, I18n.T("[] [] wearing [][].", t_He, t_is, w_uniform.examine_title(user), accessory_message));
        }
        //head
        if (Dm.truthy(head) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEHEADGEAR)) && !HAS_TRAIT(head, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] wearing [] on [] head.", t_He, t_is, head.examine_title(user), t_his));
        //mask
        if (Dm.truthy(wear_mask) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEMASK)) && !HAS_TRAIT(wear_mask, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] [] on [] face.", t_He, t_has, wear_mask.examine_title(user), t_his));
        //neck
        if (Dm.truthy(wear_neck) && !Dm.truthy(Dm.bitand(obscured_slots, HIDENECK)) && !HAS_TRAIT(wear_neck, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] wearing [] around [] neck.", t_He, t_is, wear_neck.examine_title(user), t_his));
        //eyes
        if (!Dm.truthy(Dm.bitand(obscured_slots, HIDEEYES)))
        {
            if (Dm.truthy(glasses) && !HAS_TRAIT(glasses, TRAIT_EXAMINE_SKIP))
                Dm.append(ref dot, I18n.T("[] [] [] covering [] eyes.", t_He, t_has, glasses.examine_title(user), t_his));
            else if (HAS_TRAIT(this, TRAIT_UNNATURAL_RED_GLOWY_EYES))
                Dm.append(ref dot, span_warning(I18n.T("<B>[] eyes are glowing with an unnatural red aura!</B>", t_His)));
            else if (HAS_TRAIT(this, TRAIT_BLOODSHOT_EYES))
                Dm.append(ref dot, span_warning(I18n.T("<B>[] eyes are bloodshot!</B>", t_His)));
        }
        //ears
        if (Dm.truthy(ears) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEEARS)) && !HAS_TRAIT(ears, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] [] on [] ears.", t_He, t_has, ears.examine_title(user), t_his));
        //suit/armor
        if (Dm.truthy(wear_suit) && !HAS_TRAIT(wear_suit, TRAIT_EXAMINE_SKIP))
        {
            Dm.append(ref dot, I18n.T("[] [] wearing [].", t_He, t_is, wear_suit.examine_title(user)));
            //suit/armor storage
            if (Dm.truthy(s_store) && !Dm.truthy(Dm.bitand(obscured_slots, HIDESUITSTORAGE)) && !HAS_TRAIT(s_store, TRAIT_EXAMINE_SKIP))
                Dm.append(ref dot, I18n.T("[] [] carrying [] on [] [].", t_He, t_is, s_store.examine_title(user), t_his, wear_suit.name));
        }
        //back
        if (Dm.truthy(back) && !HAS_TRAIT(back, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] [] on [] back.", t_He, t_has, back.examine_title(user), t_his));
        //ID
        if (Dm.truthy(wear_id) && !HAS_TRAIT(wear_id, TRAIT_EXAMINE_SKIP))
        {
            obj_item_card_id id = wear_id.GetID() as obj_item_card_id;
            if (id != null && get_dist(user, this) <= ID_EXAMINE_DISTANCE)
            {
                string id_href = I18n.T("<a href='byond://?src=[];see_id=1;id_ref=[];id_name=[];examine_time=[]'>[]</a>", REF(this), REF(id), id.registered_name, world.time, wear_id.examine_title(user));
                Dm.append(ref dot, I18n.T("[] [] wearing [].", t_He, t_is, id_href));
            }
            else
                Dm.append(ref dot, I18n.T("[] [] wearing [].", t_He, t_is, wear_id.examine_title(user)));
        }
        //Hands
        foreach (obj_item held_thing in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(Dm.bitand(held_thing.item_flags, Dm.bitor(ABSTRACT, HAND_ITEM))) || HAS_TRAIT(held_thing, TRAIT_EXAMINE_SKIP))
                continue;
            Dm.append(ref dot, I18n.T("[] [] holding [] in [] [].", t_He, t_is, held_thing.examine_title(user), t_his, get_held_index_name(get_held_index_of_item(held_thing))));
        }
        foreach (obj_item_bodypart_arm part in Dm.each<obj_item_bodypart_arm>(get_bodyparts()))
        {
            if (!Dm.truthy(Dm.bitand(part.bodypart_flags, BODYPART_PSEUDOPART)))
                continue;
            obj_item corresponding_item = (get_item_for_held_index(part.held_index) as obj_item) ?? part;
            Dm.append(ref dot, I18n.T("[] [] [] in place of [] [].", t_He, t_has, corresponding_item.examine_title(user), t_his, Dm.initial(part, (obj_item_bodypart_arm p) => p.plaintext_zone)));
        }
        //gloves
        if (Dm.truthy(gloves) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEGLOVES)) && !HAS_TRAIT(gloves, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] [] on [] hands.", t_He, t_has, gloves.examine_title(user), t_his));
        else if (Dm.truthy(GET_ATOM_BLOOD_DECAL_LENGTH(this)) && Dm.truthy(num_hands))
        {
            DmList blood_stains = GET_ATOM_BLOOD_DECALS(this);
            datum_blood_type blood_type = blood_stains[blood_stains[Dm.length(blood_stains)]] as datum_blood_type;
            object blood_descriptior = "blood";
            if (blood_type != null)
                blood_descriptior = LOWER_TEXT(blood_type.get_blood_name());
            Dm.append(ref dot, span_warning(I18n.T("[] [] [][]-stained hand[]!", t_He, t_has, num_hands > 1 ? "" : "a ", blood_descriptior, num_hands > 1 ? "s" : "")));
        }
        else if (Dm.truthy(blood_in_hands) && Dm.truthy(num_hands))
            Dm.append(ref dot, span_warning(I18n.T("[] [] []blood-stained hand[]!", t_He, t_has, num_hands > 1 ? "" : "a ", num_hands > 1 ? "s" : "")));
        //handcuffed?
        if (Dm.truthy(handcuffed))
        {
            string cables_or_cuffs = handcuffed is obj_item_restraints_handcuffs_cable ? "restrained with cable" : "handcuffed";
            Dm.append(ref dot, span_warning(I18n.T("[] [] []!", t_He, t_is, cables_or_cuffs)));
        }
        //belt
        if (Dm.truthy(belt) && !Dm.truthy(Dm.bitand(obscured_slots, HIDEBELT)) && !HAS_TRAIT(belt, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] [] about [] waist.", t_He, t_has, belt.examine_title(user), t_his));
        //shoes
        if (Dm.truthy(shoes) && !Dm.truthy(Dm.bitand(obscured_slots, HIDESHOES)) && !HAS_TRAIT(shoes, TRAIT_EXAMINE_SKIP))
            Dm.append(ref dot, I18n.T("[] [] wearing [] on [] feet.", t_He, t_is, shoes.examine_title(user), t_his));
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:488
public partial class mob_living_carbon : mob_living
{
    /// Collects info displayed about any HUDs the user has when examining src
    // tg: code/modules/mob/living/carbon/examine.dm:488
    public virtual object get_hud_examine_info(mob_living user = null)
    {
        return null;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:491
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/examine.dm:491
    public override object get_hud_examine_info(mob_living user = null)
    {
        DmList dot = Dm.list();

        object perpname = get_face_name(Dm.str(get_id_name("")));
        string title = "";
        if (Dm.truthy(perpname) && (HAS_TRAIT(user, TRAIT_SECURITY_HUD) || HAS_TRAIT(user, TRAIT_MEDICAL_HUD)) && (!IS_UNCONSCIOUS_OR_CRIT(user) || isobserver(user)) && user != this)
        {
            datum_record_crew target_record = find_record(perpname) as datum_record_crew;
            if (target_record != null)
            {
                Dm.append(ref dot, I18n.T("Rank: []", target_record.rank));
                Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=1;photo_front=1;examine_time=[]'>\\[Front photo\\]</a><a href='byond://?src=[];hud=1;photo_side=1;examine_time=[]'>\\[Side photo\\]</a>", REF(this), world.time, REF(this), world.time));
            }
            if (HAS_TRAIT(user, TRAIT_MEDICAL_HUD) && HAS_TRAIT(user, TRAIT_SECURITY_HUD))
            {
                title = separator_hr(I18n.T("Medical & Security Analysis"));
                Dm.append(ref dot, get_medhud_examine_info(user, target_record));
                Dm.append(ref dot, get_sechud_examine_info(user, target_record));
            }
            else if (HAS_TRAIT(user, TRAIT_MEDICAL_HUD))
            {
                title = separator_hr(I18n.T("Medical Analysis"));
                Dm.append(ref dot, get_medhud_examine_info(user, target_record));
            }
            else if (HAS_TRAIT(user, TRAIT_SECURITY_HUD))
            {
                title = separator_hr(I18n.T("Security Analysis"));
                Dm.append(ref dot, get_sechud_examine_info(user, target_record));
            }
        }

        // applies the separator correctly without an extra line break
        if (Dm.truthy(title) && Dm.truthy(Dm.length(dot)))
            dot[1] = title + Dm.str(dot[1]);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:520
public partial class mob_living_carbon : mob_living
{
    /// Collects information displayed about src when examined by a user with a medical HUD.
    // tg: code/modules/mob/living/carbon/examine.dm:520
    public virtual object get_medhud_examine_info(mob_living user = null, datum_record_crew target_record = null)
    {
        DmList dot = Dm.list();

        DmList cybers = Dm.list();
        foreach (obj_item_organ_cyberimp cyberimp in Dm.each<obj_item_organ_cyberimp>(organs))
        {
            if (Dm.truthy(IS_ROBOTIC_ORGAN(cyberimp)) && !Dm.truthy(Dm.bitand(cyberimp.organ_flags, ORGAN_HIDDEN)))
                Dm.append(ref cybers, cyberimp.examine_title(user));
        }
        if (Dm.truthy(Dm.length(cybers)))
        {
            Dm.append(ref dot, I18n.T("<span class='notice ml-1'>Detected cybernetic modifications:</span>"));
            Dm.append(ref dot, I18n.T("<span class='notice ml-2'>[]</span>", english_list(cybers, and_text: ", and")));
        }
        if (target_record != null)
        {
            Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=m;physical_status=1;examine_time=[]'>\\[[]\\]</a>", REF(this), world.time, target_record.physical_status));
            Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=m;mental_status=1;examine_time=[]'>\\[[]\\]</a>", REF(this), world.time, target_record.mental_status));
        }
        else
        {
            Dm.append(ref dot, I18n.T("\\[Record Missing\\]"));
            Dm.append(ref dot, I18n.T("\\[Record Missing\\]"));
        }
        Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=m;evaluation=1;examine_time=[]'>\\[Medical evaluation\\]</a>", REF(this), world.time));
        Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=m;quirk=1;examine_time=[]'>\\[See quirks\\]</a>", REF(this), world.time));
        return dot;
    }

    /// Collects information displayed about src when examined by a user with a security HUD.
    // tg: code/modules/mob/living/carbon/examine.dm:540
    public virtual object get_sechud_examine_info(mob_living user = null, datum_record_crew target_record = null)
    {
        DmList dot = Dm.list();

        object wanted_status = WANTED_NONE;
        object security_note = I18n.T("None.");

        if (target_record != null)
        {
            wanted_status = target_record.wanted_status;
            if (Dm.truthy(target_record.security_note))
                security_note = target_record.security_note;
        }
        if (ishuman(user))
            Dm.append(ref dot, I18n.T("Criminal status: <a href='byond://?src=[];hud=s;status=1;examine_time=[]'>\\[[]\\]</a>", REF(this), world.time, wanted_status));
        else
            Dm.append(ref dot, I18n.T("Criminal status: []", wanted_status));
        Dm.append(ref dot, I18n.T("Important Notes: []", security_note));
        Dm.append(ref dot, I18n.T("Security record: <a href='byond://?src=[];hud=s;view=1;examine_time=[]'>\\[View\\]</a>", REF(this), world.time));
        if (ishuman(user))
            Dm.append(ref dot, I18n.T("<a href='byond://?src=[];hud=s;add_citation=1;examine_time=[]'>\\[Add citation\\]</a><a href='byond://?src=[];hud=s;add_crime=1;examine_time=[]'>\\[Add crime\\]</a><a href='byond://?src=[];hud=s;add_note=1;examine_time=[]'>\\[Add note\\]</a>",
                REF(this), world.time, REF(this), world.time, REF(this), world.time));
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/examine.dm:561
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/examine.dm:561
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;

        if (w_uniform is obj_item_clothing_under undershirt && !Dm.truthy(Dm.bitand(obscured_slots, HIDEJUMPSUIT)) && !HAS_TRAIT(w_uniform, TRAIT_EXAMINE_SKIP))
        {
            if (undershirt.has_sensor == BROKEN_SENSORS)
                Dm.append(ref dot, Dm.list(span_notice(I18n.T("\\The []'s medical sensors are sparking.", undershirt))));
        }

        if ((HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE) || HAS_TRAIT(this, TRAIT_INVISIBLE_MAN)) && !isobserver(user))
            return dot;

        DmList limbs_text = get_mismatched_limb_text() as DmList;
        if (Dm.truthy(LAZYLEN(limbs_text)))
            Dm.append(ref dot, limbs_text);

        object agetext = get_age_text();
        if (Dm.truthy(agetext))
            Dm.append(ref dot, agetext);
        return dot;
    }

    /// Reports all body parts which are mismatched with the user's species
    // tg: code/modules/mob/living/carbon/examine.dm:581
    public virtual object get_mismatched_limb_text()
    {
        DmList covered = get_covered_body_zones() as DmList;
        DmList texts = Dm.list();
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            object part_id = part.limb_id;
            Type expected_part = (dna?.species as datum_species)?.bodypart_overrides?[part.body_zone] as Type;
            object expected_id = expected_part == null ? null : Dm.initial(expected_part, (obj_item_bodypart p) => p.limb_id);
            // only report abnormal bodyparts
            if (Dm.eq(part_id, expected_id))
                continue;
            // same shape bodyparts are concealed by clothing
            // this means you can see ex. digitigrade legs through clothes
            // but you can't see ex. cybernetic legs through clothes
            if (Dm.eq(part.bodyshape, expected_part == null ? null : Dm.initial(expected_part, (obj_item_bodypart p) => p.bodyshape)) && Dm.@in(part.body_zone, covered))
                continue;
            Dm.append(ref texts, span_notice(I18n.T("[] [] \\a [].", p_They(), p_have(), part)));
        }

        return texts;
    }

    /// Reports how old the mob appears to be
    // tg: code/modules/mob/living/carbon/examine.dm:601
    public virtual object get_age_text()
    {
        if (Dm.truthy(Dm.bitand(obscured_slots, HIDEFACE)))
            return null;

        string age_text = null;
        float years = Dm.num(age);
        if (years <= 25)
            age_text = "very young";
        else if (years >= 26 && years <= 35)
            age_text = "of adult age";
        else if (years >= 36 && years <= 55)
            age_text = "middle-aged";
        else if (years >= 56 && years <= 75)
            age_text = "rather old";
        else if (years >= 76 && years <= 100)
            age_text = "very old";
        else if (years >= 101)
            age_text = "withering away";

        return span_notice(I18n.T("[] appear[] to be [].", p_They(), p_s(), age_text));
    }
}
