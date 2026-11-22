// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/burns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks and the pregen data subtypes are generated (D111). The
// reagents that act on a burn (REAGENT_AFFECTS_WOUNDS, on_burn_wound_processing) are chemistry (M8): the loop runs over the
// victim's reagents and the base hook does nothing.

/*
	Burn wounds
*/

// TODO: well, a lot really, but specifically I want to add potential fusing of clothing/equipment on the affected area, and limb infections, though those may go in body part code
// tg: code/datums/wounds/burns.dm:7
public partial class datum_wound_burn : datum_wound
{
    // tg: code/datums/wounds/burns.dm:13
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (old_wound == null && current_gauze != null && Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)))
        {
            qdel(current_gauze);
            // oops your existing gauze got burned, need a new one now
            obj_effect_decal_cleanable_ash ash = Dm.@new<obj_effect_decal_cleanable_ash>(limb.drop_location());
            ash.desc += I18n.T(" It looks like it used to be some kind of bandage.");
        }

        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/burns.dm:295
    public override object get_limb_examine_description()
    {
        return span_warning(I18n.T("The flesh on this limb appears badly cooked."));
    }
}

// tg: code/datums/wounds/burns.dm:23
public partial class datum_wound_burn_flesh : datum_wound_burn
{
    // tg: code/datums/wounds/burns.dm:48
    public override async DmTask handle_process(object seconds_per_tick = null)
    {

        if (victim == null || HAS_TRAIT(victim, TRAIT_STASIS))
            return;

        await base.handle_process(seconds_per_tick);
        float spt = Dm.num(seconds_per_tick);
        if (strikes_to_lose_limb <= 0) // we've already hit sepsis, nothing more to do
        {
            await victim.adjust_tox_loss(0.25f * spt);
            if (SPT_PROB(0.5f, seconds_per_tick))
                victim.visible_message(span_danger(I18n.T("The infection on the remnants of []'s [] shift and bubble nauseatingly!", victim, limb.plaintext_zone)), span_warning(I18n.T("You can feel the infection on the remnants of your [] coursing through your veins!", limb.plaintext_zone)), vision_distance: COMBAT_MESSAGE_RANGE);
            return;
        }

        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(((datum_reagents)victim.reagents).reagent_list))
        {
            if (Dm.truthy(Dm.bitand(reagent.chemical_flags, REAGENT_AFFECTS_WOUNDS)))
                await reagent.on_burn_wound_processing(this);
        }

        if (HAS_TRAIT(victim, TRAIT_VIRUS_RESISTANCE))
            sanitization += 0.9f;
        if (HAS_TRAIT(victim, TRAIT_IMMUNODEFICIENCY))
        {
            infection = Dm.num(infection) + 0.05f;
            sanitization = max(sanitization - 0.15f, 0);
            if (infection_rate <= 0.15f && prob(50))
                infection_rate += 0.001f;
        }

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze != null)
            await limb.seep_gauze(WOUND_BURN_SANITIZATION_RATE * spt);

        if (flesh_healing > 0) // good bandages multiply the length of flesh healing
        {
            float bandage_factor = Dm.truthy(current_gauze?.burn_cleanliness_bonus) ? Dm.num(current_gauze.burn_cleanliness_bonus) : 1;
            flesh_damage = max(flesh_damage - (0.5f * spt), 0);
            flesh_healing = max(flesh_healing - (0.5f * bandage_factor * spt), 0); // good bandages multiply the length of flesh healing
        }

        // if we have little/no infection, the limb doesn't have much burn damage, and our nutrition is good, heal some flesh
        if (Dm.num(infection) <= WOUND_INFECTION_MODERATE && (Dm.num(limb.burn_dam) < 5) && (Dm.num(victim.nutrition) >= NUTRITION_LEVEL_FED))
            flesh_healing += 0.2f;

        // here's the check to see if we're cleared up
        if ((flesh_damage <= 0) && (Dm.num(infection) <= WOUND_INFECTION_MODERATE))
        {
            to_chat(victim, span_green(I18n.T("The burns on your [] have cleared up!", limb.plaintext_zone)));
            qdel(this);
            return;
        }

        // sanitization is checked after the clearing check but before the actual ill-effects, because we freeze the effects of infection while we have sanitization
        if (sanitization > 0)
        {
            float bandage_factor = Dm.truthy(current_gauze?.burn_cleanliness_bonus) ? Dm.num(current_gauze.burn_cleanliness_bonus) : 1;
            infection = max(Dm.num(infection) - (WOUND_BURN_SANITIZATION_RATE * spt), 0);
            sanitization = max(sanitization - (WOUND_BURN_SANITIZATION_RATE * bandage_factor * spt), 0);
            return;
        }

        infection = Dm.num(infection) + infection_rate * spt;
        float sw__1 = Dm.num(infection);
        if (0 <= sw__1 && sw__1 <= WOUND_INFECTION_MODERATE)
            return;
        else if (WOUND_INFECTION_MODERATE <= sw__1 && sw__1 <= WOUND_INFECTION_SEVERE)
        {
            if (SPT_PROB(15, seconds_per_tick))
            {
                await victim.adjust_tox_loss(0.2f);
                if (prob(6))
                    to_chat(victim, span_warning(I18n.T("The blisters on your [] ooze a strange pus...", limb.plaintext_zone)));
            }
        }
        else if (WOUND_INFECTION_SEVERE <= sw__1 && sw__1 <= WOUND_INFECTION_CRITICAL)
        {
            if (!Dm.truthy(disabling))
            {
                if (SPT_PROB(1, seconds_per_tick))
                {
                    to_chat(victim, span_warning(I18n.T("<b>Your [] completely locks up, as you struggle for control against the infection!</b>", limb.plaintext_zone)));
                    await set_disabling(TRUE);
                    return;
                }
            }
            else if (SPT_PROB(4, seconds_per_tick))
            {
                to_chat(victim, span_notice(I18n.T("You regain sensation in your [], but it's still in terrible shape!", limb.plaintext_zone)));
                await set_disabling(FALSE);
                return;
            }

            if (SPT_PROB(10, seconds_per_tick))
                await victim.adjust_tox_loss(0.5f);
        }
        else if (WOUND_INFECTION_CRITICAL <= sw__1 && sw__1 <= WOUND_INFECTION_SEPTIC)
        {
            if (!Dm.truthy(disabling))
            {
                if (SPT_PROB(1.5f, seconds_per_tick))
                {
                    to_chat(victim, span_warning(I18n.T("<b>You suddenly lose all sensation of the festering infection in your []!</b>", limb.plaintext_zone)));
                    await set_disabling(TRUE);
                    return;
                }
            }
            else if (SPT_PROB(1.5f, seconds_per_tick))
            {
                to_chat(victim, span_notice(I18n.T("You can barely feel your [] again, and you have to strain to retain motor control!", limb.plaintext_zone)));
                await set_disabling(FALSE);
                return;
            }

            if (SPT_PROB(2.48f, seconds_per_tick))
            {
                if (prob(20))
                {
                    to_chat(victim, span_warning(I18n.T("You contemplate life without your []...", limb.plaintext_zone)));
                    await victim.adjust_tox_loss(0.75f);
                }
                else
                    await victim.adjust_tox_loss(1);
            }
        }
        else if (WOUND_INFECTION_SEPTIC <= sw__1)
        {
            if (SPT_PROB(0.5f * sw__1, seconds_per_tick))
            {
                strikes_to_lose_limb--;
                if (2 <= strikes_to_lose_limb)
                    to_chat(victim, span_deadsay(I18n.T("<b>The infection in your [] is literally dripping off, you feel horrible!</b>", limb.plaintext_zone)));
                else if (strikes_to_lose_limb == 1)
                    to_chat(victim, span_deadsay(I18n.T("<b>Infection has just about completely claimed your []!</b>", limb.plaintext_zone)));
                else if (strikes_to_lose_limb == 0)
                {
                    to_chat(victim, span_deadsay(I18n.T("<b>The last of the nerve endings in your [] wither away, as the infection completely paralyzes your joint connector.</b>", limb.plaintext_zone)));
                    threshold_penalty = Dm.num(threshold_penalty) * 2; // piss easy to destroy
                    await set_disabling(TRUE);
                }
            }
        }
    }

    // tg: code/datums/wounds/burns.dm:154
    public override async DmTask<object> set_disabling(object new_value = null)
    {
        object dot = await base.set_disabling(new_value);
        if (Dm.truthy(new_value) && strikes_to_lose_limb <= 0)
            treat_text_short = I18n.T("Amputate or augment limb immediately, or place the patient into cryogenics.");
        else
            treat_text_short = Dm.initial(this, (datum_wound_burn_flesh w) => w.treat_text_short);
        return dot;
    }

    // tg: code/datums/wounds/burns.dm:161
    public override object get_wound_description(mob user = null)
    {
        if (strikes_to_lose_limb <= 0)
            return span_deadsay(I18n.T("<B>[] [] has locked up completely and is non-functional.</B>", victim.p_Their(), limb.plaintext_zone));

        string condition = I18n.T("[] [] []", victim.p_Their(), limb.plaintext_zone, examine_desc);
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze != null)
        {
            string bandage_condition = null;
            float capacity = Dm.num(current_gauze.absorption_capacity);
            if (0 <= capacity && capacity <= 1.25f)
                bandage_condition = I18n.T("nearly ruined ");
            else if (1.25f <= capacity && capacity <= 2.75f)
                bandage_condition = I18n.T("badly worn ");
            else if (2.75f <= capacity && capacity <= 4)
                bandage_condition = I18n.T("slightly stained ");
            else if (4 <= capacity)
                bandage_condition = I18n.T("clean ");

            condition += I18n.T(" underneath a dressing of [][].", bandage_condition, current_gauze.name);
        }
        else
        {
            float sw__1 = Dm.num(infection);
            if (WOUND_INFECTION_MODERATE <= sw__1 && sw__1 <= WOUND_INFECTION_SEVERE)
                condition += I18n.T(", []", span_deadsay(I18n.T("with early signs of infection.")));
            else if (WOUND_INFECTION_SEVERE <= sw__1 && sw__1 <= WOUND_INFECTION_CRITICAL)
                condition += I18n.T(", []", span_deadsay(I18n.T("with growing clouds of infection.")));
            else if (WOUND_INFECTION_CRITICAL <= sw__1 && sw__1 <= WOUND_INFECTION_SEPTIC)
                condition += I18n.T(", []", span_deadsay(I18n.T("with streaks of rotten infection!")));
            else if (WOUND_INFECTION_SEPTIC <= sw__1)
                return span_deadsay(I18n.T("<B>[] [] is a mess of charred skin and infected rot!</B>", victim.p_Their(), limb.plaintext_zone));
            else
                condition += "!";
        }

        return I18n.T("<B>[]</B>", condition);
    }

    // tg: code/datums/wounds/burns.dm:195
    [DmParams("simple")]
    public override object severity_text(params object[] __extra)
    {
        string dot = Dm.text("[]", base.severity_text(__extra));
        dot += I18n.T(" Burn / ");
        float sw__1 = Dm.num(infection);
        if (sw__1 <= WOUND_INFECTION_MODERATE)
            dot += I18n.T("No");
        else if (WOUND_INFECTION_MODERATE <= sw__1 && sw__1 <= WOUND_INFECTION_SEVERE)
            dot += I18n.T("Moderate");
        else if (WOUND_INFECTION_SEVERE <= sw__1 && sw__1 <= WOUND_INFECTION_CRITICAL)
            dot += I18n.T("<b>Severe</b>");
        else if (WOUND_INFECTION_CRITICAL <= sw__1 && sw__1 <= WOUND_INFECTION_SEPTIC)
            dot += I18n.T("<b>Critical</b>");
        else if (WOUND_INFECTION_SEPTIC <= sw__1)
            dot += I18n.T("<b>Total</b>");
        dot += I18n.T(" Infection");
        return dot;
    }

    // tg: code/datums/wounds/burns.dm:211
    public override object get_scanner_description(mob user = null)
    {
        if (strikes_to_lose_limb <= 0) // Unclear if it can go below 0, best to not take the chance
        {
            string oopsie = I18n.T("Type: []<br>Severity: []", name, severity_text());
            oopsie += I18n.T("<div class='ml-3'>Infection Level: []</div>", span_deadsay(I18n.T("The body part has suffered complete sepsis and must be removed. Amputate or augment limb immediately, or place the patient in a cryotube.")));
            return oopsie;
        }

        string dot = Dm.text("[]", base.get_scanner_description(user));
        dot += "<div class='ml-3'>";

        if (Dm.num(infection) <= sanitization && flesh_damage <= flesh_healing)
            dot += I18n.T("No further treatment required: Burns will heal shortly.");
        else
        {
            float sw__1 = Dm.num(infection);
            if (WOUND_INFECTION_MODERATE <= sw__1 && sw__1 <= WOUND_INFECTION_SEVERE)
                dot += I18n.T("Infection Level: Moderate\n");
            else if (WOUND_INFECTION_SEVERE <= sw__1 && sw__1 <= WOUND_INFECTION_CRITICAL)
                dot += I18n.T("Infection Level: Severe\n");
            else if (WOUND_INFECTION_CRITICAL <= sw__1 && sw__1 <= WOUND_INFECTION_SEPTIC)
                dot += I18n.T("Infection Level: []\n", span_deadsay(I18n.T("CRITICAL")));
            else if (WOUND_INFECTION_SEPTIC <= sw__1)
                dot += I18n.T("Infection Level: []\n", span_deadsay(I18n.T("LOSS IMMINENT")));
            if (Dm.num(infection) > sanitization)
                dot += I18n.T("\tSurgical debridement, antibiotics/sterilizers, or regenerative mesh will rid infection. Paramedic UV penlights are also effective.\n");

            if (flesh_damage > 0)
                dot += I18n.T("Flesh damage detected: Application of ointment, regenerative mesh, Synthflesh, or ingestion of \"Miner's Salve\" will repair damaged flesh. Good nutrition, rest, and keeping the wound clean can also slowly repair flesh.\n");
        }
        dot += "</div>";
        return dot;
    }

    /*
        new burn common procs
    */

    /// Checks if the wound is in a state that ointment or flesh will help
    // tg: code/datums/wounds/burns.dm:244
    public virtual object can_be_ointmented_or_meshed()
    {
        if (Dm.num(infection) > 0 && sanitization < Dm.num(infection))
            return TRUE;
        if (flesh_damage > 0 && flesh_healing <= flesh_damage)
            return TRUE;
        return FALSE;
    }

    /// Paramedic UV penlights
    // tg: code/datums/wounds/burns.dm:252
    public virtual void uv(obj_item_flashlight_pen_paramedic I = null, mob user = null)
    {
        if (!(I.uv_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            to_chat(user, span_notice(I18n.T("[] is still recharging!", I)));
            return;
        }
        if (Dm.num(infection) <= 0 || Dm.num(infection) < sanitization)
        {
            to_chat(user, span_notice(I18n.T("There's no infection to treat on []'s []!", victim, limb.plaintext_zone)));
            return;
        }

        user.visible_message(span_notice(I18n.T("[] flashes the burns on []'s [] with [].", user, victim, limb, I)), span_notice(I18n.T("You flash the burns on [] [] with [].", user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone, I)), vision_distance: COMBAT_MESSAGE_RANGE);
        sanitization += I.uv_power;
        I.uv_cooldown = world.time + I.uv_cooldown_length; // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
    }

    // tg: code/datums/wounds/burns.dm:264
    public override async DmTask treat(obj_item tool = null, mob user = null)
    {
        if (istype(tool, typeof(obj_item_flashlight_pen_paramedic)))
            uv((obj_item_flashlight_pen_paramedic)tool, user);
    }

    // people complained about burns not healing on stasis beds, so in addition to checking if it's cured, they also get the special ability to very slowly heal on stasis beds if they have the healing effects stored
    // tg: code/datums/wounds/burns.dm:269
    public override void on_stasis(object seconds_per_tick = null)
    {
        base.on_stasis(seconds_per_tick);
        if (strikes_to_lose_limb <= 0) // we've already hit sepsis, nothing more to do
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
                victim.visible_message(span_danger(I18n.T("The infection on the remnants of []'s [] shift and bubble nauseatingly!", victim, limb.plaintext_zone)), span_warning(I18n.T("You can feel the infection on the remnants of your [] coursing through your veins!", limb.plaintext_zone)), vision_distance: COMBAT_MESSAGE_RANGE);
            return;
        }
        if (flesh_healing > 0)
            flesh_damage = max(flesh_damage - (0.1f * Dm.num(seconds_per_tick)), 0);
        if ((flesh_damage <= 0) && (Dm.num(infection) <= 1))
        {
            to_chat(victim, span_green(I18n.T("The burns on your [] have cleared up!", limb.plaintext_zone)));
            qdel(this);
            return;
        }
        if (sanitization > 0)
            infection = max(Dm.num(infection) - (0.1f * WOUND_BURN_SANITIZATION_RATE * Dm.num(seconds_per_tick)), 0);
    }

    // tg: code/datums/wounds/burns.dm:284
    public override async DmTask on_synthflesh(object reac_volume = null)
    {
        flesh_healing += Dm.num(reac_volume) * 0.5f; // 20u patch will heal 10 flesh standard
    }
}

// tg: code/datums/wounds/burns.dm:401
public partial class datum_wound_burn_flesh_severe_cursed_brand : datum_wound_burn_flesh_severe
{
    // tg: code/datums/wounds/burns.dm:407
    public override object get_limb_examine_description()
    {
        return span_warning(I18n.T("The flesh on this limb has several ornate symbols burned into it, with pitting throughout."));
    }
}
