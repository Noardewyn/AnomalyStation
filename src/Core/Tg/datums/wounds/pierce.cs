// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/pierce.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks are generated (D111). Left for its milestone inside the
// translated procs: the scream of a cauterized puncture (emote, M9: Dm.Deferred).

/*
	Piercing wounds
*/
// tg: code/datums/wounds/pierce.dm:4
public partial class datum_wound_pierce : datum_wound
{
    // tg: code/datums/wounds/pierce.dm:9
    public override object get_self_check_description(object self_aware = null)
    {
        if (!Dm.truthy(limb.can_bleed()))
            return base.get_self_check_description(self_aware);

        switch (severity)
        {
            case WOUND_SEVERITY_TRIVIAL:
                return span_danger(I18n.T("It's leaking blood from a small [].", LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_MODERATE:
                return span_warning(I18n.T("It's leaking blood from a [].", LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_SEVERE:
                return span_boldwarning(I18n.T("It's leaking blood from a serious []!", LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_CRITICAL:
                return span_boldwarning(I18n.T("It's leaking blood from a major []!!", LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
        }
        return null;
    }

    // tg: code/datums/wounds/pierce.dm:23
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (old_wound == null && current_gauze != null && Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)))
        {
            // oops your existing gauze got penetrated through! need a new one now
            await limb.seep_gauze(Dm.num(Dm.initial(current_gauze, (obj_item_stack_medical_wrap g) => g.absorption_capacity)) * 0.8f);
        }
        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/pierce.dm:197
    public override object get_limb_examine_description()
    {
        return span_warning(I18n.T("The flesh on this limb appears badly perforated."));
    }
}

// tg: code/datums/wounds/pierce.dm:30
public partial class datum_wound_pierce_bleed : datum_wound_pierce
{
    // tg: code/datums/wounds/pierce.dm:54
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        await set_blood_flow(initial_flow);
        if (Dm.truthy(limb.can_bleed()) && Dm.truthy(attack_direction) && Dm.num(victim.get_blood_volume()) > BLOOD_VOLUME_OKAY)
            await victim.spray_blood(attack_direction, severity);
        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/pierce.dm:60
    public override async DmTask receive_damage(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object attack_direction = null, object damage_source = null)
    {
        if (victim.stat == DEAD || Dm.num(wounding_dmg) < 5 || !Dm.truthy(limb.can_bleed()) || !Dm.truthy(victim.get_blood_volume()) || !prob(Dm.num(internal_bleeding_chance) + Dm.num(wounding_dmg)))
            return;
        // 20 force attack ~=  5-16 blood loss ~= 1%-3% of blood volume
        // 30 force attack ~= 6-20 blood loss ~= 1%-4% of blood volume
        float blood_bled = sqrt(Dm.num(wounding_dmg)) * internal_bleeding_coefficient * Dm.num(limb.get_splint_factor()) * Dm.num(pick(0.75f, 1, 1.25f, 1.5f));
        if (8 <= blood_bled && blood_bled <= 12)
            victim.visible_message(
                span_smalldanger(I18n.T("Blood droplets fly from the hole in []'s [].", victim, limb.plaintext_zone)),
                span_danger(I18n.T("You cough up a bit of blood from the blow to your [].", limb.plaintext_zone)),
                vision_distance: COMBAT_MESSAGE_RANGE,
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        else if (12 <= blood_bled && blood_bled <= 18)
            victim.visible_message(
                span_smalldanger(I18n.T("A small stream of blood spurts from the hole in []'s []!", victim, limb.plaintext_zone)),
                span_danger(I18n.T("You spit out a string of blood from the blow to your []!", limb.plaintext_zone)),
                vision_distance: COMBAT_MESSAGE_RANGE,
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        else if (18 <= blood_bled)
            victim.visible_message(
                span_danger(I18n.T("A spray of blood streams from the gash in []'s []!", victim, limb.plaintext_zone)),
                span_bolddanger(I18n.T("You choke up on a spray of blood from the blow to your []!", limb.plaintext_zone)),
                vision_distance: COMBAT_MESSAGE_RANGE,
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        await victim.bleed(blood_bled);
        if (blood_bled >= 18)
            await victim.spray_blood(attack_direction);
    }

    // tg: code/datums/wounds/pierce.dm:92
    public override object get_bleed_rate_of_change()
    {
        //basically if a species doesn't bleed, the wound is stagnant and will not heal on its own (nor get worse)
        if (!Dm.truthy(limb.can_bleed()))
            return BLOOD_FLOW_STEADY;
        if (HAS_TRAIT(victim, TRAIT_BLOOD_FOUNTAIN))
            return BLOOD_FLOW_INCREASING;
        if (Dm.truthy(LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE)) || Dm.num(clot_rate) > 0)
            return BLOOD_FLOW_DECREASING;
        if (Dm.num(clot_rate) < 0)
            return BLOOD_FLOW_INCREASING;
        return BLOOD_FLOW_STEADY;
    }

    // tg: code/datums/wounds/pierce.dm:104
    public override async DmTask handle_process(object seconds_per_tick = null)
    {
        if (victim == null || HAS_TRAIT(victim, TRAIT_STASIS))
            return;


        if (Dm.truthy(limb.can_bleed()))
        {
            if (Dm.num(victim.bodytemperature) < (BODYTEMP_NORMAL - 10))
            {
                await adjust_blood_flow(-0.1f * Dm.num(seconds_per_tick));
                if (QDELETED(this))
                    return;
                if (SPT_PROB(2.5f, seconds_per_tick))
                    to_chat(victim, span_notice(I18n.T("You feel the [] in your [] firming up from the cold!", LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name), limb.plaintext_zone)));
            }

            if (HAS_TRAIT(victim, TRAIT_BLOOD_FOUNTAIN))
                await adjust_blood_flow(0.25f * Dm.num(seconds_per_tick)); // old heparin used to just add +2 bleed stacks per tick, this adds 0.5 bleed flow to all open cuts which is probably even stronger as long as you can cut them first
        }

        //gauze always reduces blood flow, even for non bleeders
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (Dm.truthy(current_gauze?.absorption_rate))
        {
            float gauze_power = Dm.num(current_gauze.absorption_rate);
            await limb.seep_gauze(gauze_power * Dm.num(seconds_per_tick));
            await adjust_blood_flow((-Dm.num(clot_rate) * Dm.num(seconds_per_tick)) + (-gauze_power * Dm.num(gauzed_clot_rate) * Dm.num(seconds_per_tick)));
        }
        //otherwise, only clot if it's a bleeder
        else if (Dm.truthy(limb.can_bleed()))
            await adjust_blood_flow(-Dm.num(clot_rate) * Dm.num(seconds_per_tick));
    }

    // tg: code/datums/wounds/pierce.dm:130
    public override async DmTask adjust_blood_flow(object adjust_by = null, float? minimum = null)
    {
        await base.adjust_blood_flow(adjust_by, minimum);
        if (Dm.num(blood_flow) > WOUND_MAX_BLOODFLOW)
            blood_flow = WOUND_MAX_BLOODFLOW;
        if (Dm.num(blood_flow) <= 0 && !QDELETED(this))
        {
            to_chat(victim, span_green(I18n.T("The holes on your [] have []!", limb.plaintext_zone, !Dm.truthy(limb.can_bleed()) ? I18n.T("healed up") : I18n.T("stopped bleeding"))));
            qdel(this);
        }
    }

    // tg: code/datums/wounds/pierce.dm:138
    public override object check_grab_treatments(obj_item tool = null, mob user = null)
    {
        // if we're using something hot but not a cautery, we need to be aggro grabbing them first,
        // so we don't try treating someone we're eswording
        return Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST ? TRUE : FALSE;
    }

    // tg: code/datums/wounds/pierce.dm:143
    public override async DmTask treat(obj_item tool = null, mob user = null)
    {
        if (Dm.eq(tool.tool_behaviour, TOOL_CAUTERY) || Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            await tool_cauterize(tool, user);
    }

    // tg: code/datums/wounds/pierce.dm:147
    public override async DmTask<object> on_xadone(object power = null)
    {
        object dot = await base.on_xadone(power);

        if (limb != null) // parent can cause us to be removed, so its reasonable to check if we're still applied
            await adjust_blood_flow(-0.03f * Dm.num(power)); // i think it's like a minimum of 3 power, so .09 blood_flow reduction per tick is pretty good for 0 effort
        return dot;
    }

    // tg: code/datums/wounds/pierce.dm:153
    public override async DmTask on_synthflesh(object reac_volume = null)
    {
        await base.on_synthflesh(reac_volume);
        await adjust_blood_flow(-0.025f * Dm.num(reac_volume)); // 20u * 0.05 = -1 blood flow, less than with slashes but still good considering smaller bleed rates
    }

    /// If someone is using either a cautery tool or something with heat to cauterize this pierce
    // tg: code/datums/wounds/pierce.dm:158
    public virtual async DmTask<object> tool_cauterize(obj_item I = null, mob user = null)
    {

        float improv_penalty_mult = (Dm.eq(I.tool_behaviour, TOOL_CAUTERY) ? 1 : 1.25f); // 25% longer and less effective if you don't use a real cautery
        float self_penalty_mult = (user == victim ? 1.5f : 1); // 50% longer and less effective if you do it to yourself

        float treatment_delay = base_treat_time * self_penalty_mult * improv_penalty_mult;

        if (HAS_TRAIT(this, TRAIT_WOUND_SCANNED))
        {
            treatment_delay *= 0.5f;
            user.visible_message(span_danger(I18n.T("[] begins expertly cauterizing []'s [] with []...", user, victim, limb.plaintext_zone, I)), span_warning(I18n.T("You begin cauterizing [] [] with [], keeping the holo-image indications in mind...", user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone, I)));
        }
        else
            user.visible_message(span_danger(I18n.T("[] begins cauterizing []'s [] with []...", user, victim, limb.plaintext_zone, I)), span_warning(I18n.T("You begin cauterizing [] [] with []...", user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone, I)));

        playsound(user, Dm.resource("sound/items/handling/surgery/cautery1.ogg"), 75, TRUE);

        if (!Dm.truthy(await do_after(user, treatment_delay, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return TRUE;

        playsound(user, Dm.resource("sound/items/handling/surgery/cautery2.ogg"), 75, TRUE);

        string bleeding_wording = (Dm.truthy(limb.can_bleed()) ? I18n.T("bleeding") : I18n.T("holes"));
        user.visible_message(span_green(I18n.T("[] cauterizes some of the [] on [].", user, bleeding_wording, victim)), span_green(I18n.T("You cauterize some of the [] on [].", bleeding_wording, victim)));
        await victim.apply_damage(2 + severity, BURN, limb, wound_bonus: CANT_WOUND);
        if (prob(30))
        {
            await victim.emote("scream");
        }
        float blood_cauterized = (0.6f / (self_penalty_mult * improv_penalty_mult));
        await adjust_blood_flow(-blood_cauterized);

        if (Dm.num(blood_flow) > 0)
            try_treating(I, user as mob_living);
        return null;
    }
}

// tg: code/datums/wounds/pierce.dm:200
public partial class datum_wound_pierce_bleed_moderate : datum_wound_pierce_bleed
{
    // tg: code/datums/wounds/pierce.dm:223
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
        {
            examine_desc = I18n.T("has a small, torn hole");
            occur_text = I18n.T("splits a small hole open");
        }
    }
}

// tg: code/datums/wounds/pierce.dm:228
public partial class datum_wound_pregen_data_flesh_pierce_breakage : datum_wound_pregen_data_flesh_pierce
{
    // tg: code/datums/wounds/pierce.dm:235
    public override object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        if (isprojectile(damage_source))
            return 0;
        return weight;
    }
}

// tg: code/datums/wounds/pierce.dm:256
public partial class datum_wound_pierce_bleed_moderate_projectile : datum_wound_pierce_bleed_moderate
{
    // tg: code/datums/wounds/pierce.dm:265
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
        {
            examine_desc = I18n.T("has a small, circular hole");
            occur_text = I18n.T("splits a small hole open");
        }
    }
}

// tg: code/datums/wounds/pierce.dm:270
public partial class datum_wound_pregen_data_flesh_pierce_breakage_projectile : datum_wound_pregen_data_flesh_pierce_breakage
{
    // tg: code/datums/wounds/pierce.dm:273
    public override object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        if (!isprojectile(damage_source))
            return 0;
        return weight;
    }
}

// tg: code/datums/wounds/pierce.dm:278
public partial class datum_wound_pierce_bleed_severe : datum_wound_pierce_bleed
{
    // tg: code/datums/wounds/pierce.dm:301
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
            occur_text = I18n.T("tears a hole open");
    }
}

// tg: code/datums/wounds/pierce.dm:305
public partial class datum_wound_pregen_data_flesh_pierce_open_puncture : datum_wound_pregen_data_flesh_pierce
{
    // tg: code/datums/wounds/pierce.dm:312
    public override object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        if (isprojectile(damage_source))
            return 0;
        return weight;
    }
}

// tg: code/datums/wounds/pierce.dm:322
public partial class datum_wound_pregen_data_flesh_pierce_open_puncture_projectile : datum_wound_pregen_data_flesh_pierce_open_puncture
{
    // tg: code/datums/wounds/pierce.dm:325
    public override object get_weight(obj_item_bodypart limb = null, object woundtype = null, object damage = null, object attack_direction = null, object damage_source = null)
    {
        if (!isprojectile(damage_source))
            return 0;
        return weight;
    }
}

// tg: code/datums/wounds/pierce.dm:330
public partial class datum_wound_pierce_bleed_severe_eye : datum_wound_pierce_bleed_severe
{
    // tg: code/datums/wounds/pierce.dm:336
    [DmParams("limb", "silent", "old_wound", "smited", "attack_direction", "wound_source", "replacing", "right_side")]
    public override async DmTask<object> apply_wound(obj_item_bodypart limb = null, float? silent = null, datum_wound old_wound = null, float? smited = null, object attack_direction = null, object wound_source = null, float? replacing = null, params object[] __extra)
    {
        object right_side = Dm.arg<object>(__extra, 0);
        obj_item_organ_eyes eyes = Dm.locate<obj_item_organ_eyes>(limb);
        if (!istype(eyes, typeof(obj_item_organ_eyes)))
            return FALSE;
        object dot = await base.apply_wound(limb, silent, old_wound, smited, attack_direction, wound_source, replacing, __extra);
        this.right_side = Dm.num(right_side);
        examine_desc = I18n.T("has its [] eye pierced clean through, blood spewing from the cavity", Dm.truthy(right_side) ? I18n.T("right") : I18n.T("left"));
        RegisterSignal(limb, COMSIG_BODYPART_UPDATE_WOUND_OVERLAY, nameof(wound_overlay));
        await limb.update_part_wound_overlay();
        return dot;
    }

    // tg: code/datums/wounds/pierce.dm:346
    public override async DmTask remove_wound(object ignore_limb = null, float? replaced = null, float? destroying = null)
    {
        if (limb != null)
            UnregisterSignal(limb, COMSIG_BODYPART_UPDATE_WOUND_OVERLAY);
        await base.remove_wound(ignore_limb, replaced, destroying);
    }

    // tg: code/datums/wounds/pierce.dm:351
    [SignalHandler]
    public virtual object wound_overlay(obj_item_bodypart source = null, object limb_bleed_rate = null)
    {

        if (Dm.num(limb_bleed_rate) <= BLEED_OVERLAY_LOW || Dm.num(limb_bleed_rate) > BLEED_OVERLAY_GUSH)
            return null;

        if (Dm.num(blood_flow) <= BLEED_OVERLAY_LOW)
            return null;

        source.bleed_overlay_icon = Dm.truthy(right_side) ? "r_eye" : "l_eye";
        return COMPONENT_PREVENT_WOUND_OVERLAY_UPDATE;
    }
}

// tg: code/datums/wounds/pierce.dm:363
public partial class datum_wound_pregen_data_flesh_pierce_open_puncture_eye : datum_wound_pregen_data_flesh_pierce_open_puncture
{
    // tg: code/datums/wounds/pierce.dm:368
    [DmParams("limb", "suggested_wounding_types", "old_wound", "random_roll", "duplicates_allowed", "care_about_existing_wounds")]
    public override object can_be_applied_to(obj_item_bodypart limb = null, object suggested_wounding_type = null, datum_wound old_wound = null, float? random_roll = null, object duplicates_allowed = null, float? care_about_existing_wounds = null)
    {
        if (Dm.locate<obj_item_organ_eyes>(limb) == null)
            return FALSE;
        return base.can_be_applied_to(limb, suggested_wounding_type, old_wound, random_roll, duplicates_allowed, care_about_existing_wounds);
    }
}

//what happens if you try to listen to the heartbeat of a corrupt heart while not a heretic
// tg: code/datums/wounds/pierce.dm:373
public partial class datum_wound_pierce_bleed_severe_magicalearpain : datum_wound_pierce_bleed_severe
{
    // tg: code/datums/wounds/pierce.dm:385
    public override async DmTask<object> apply_wound(obj_item_bodypart limb = null, float? silent = null, datum_wound old_wound = null, float? smited = null, object attack_direction = null, object wound_source = null, float? replacing = null, params object[] __extra)
    {
        obj_item_organ_ears ears = Dm.locate<obj_item_organ_ears>(limb);
        if (!istype(ears, typeof(obj_item_organ_ears)))
            return FALSE;
        return await base.apply_wound(limb, silent, old_wound, smited, attack_direction, wound_source, replacing, __extra);
    }
}
