// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/slash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks and the pregen data subtypes are generated (D111). Left for
// their milestones inside the translated procs: the screams (emote, M9: Dm.Deferred). The laser gun's cauterizing shot
// (las_cauterize) is translated with the guns (M7x slice B).

/*
	Slashing wounds
*/

// tg: code/datums/wounds/slash.dm:6
public partial class datum_wound_slash : datum_wound
{
    // tg: code/datums/wounds/slash.dm:11
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

    // tg: code/datums/wounds/slash.dm:25
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (old_wound == null && current_gauze != null && Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)))
        {
            // oops your existing gauze got cut through! need a new one now
            await limb.seep_gauze(Dm.num(Dm.initial(current_gauze, (obj_item_stack_medical_wrap g) => g.absorption_capacity)) * 0.8f);
        }
        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/slash.dm:305
    public override object get_limb_examine_description()
    {
        return span_warning(I18n.T("The flesh on this limb appears badly lacerated."));
    }
}

// tg: code/datums/wounds/slash.dm:40
public partial class datum_wound_slash_flesh : datum_wound_slash
{
    // tg: code/datums/wounds/slash.dm:63
    public override object Destroy(float? force = null)
    {
        highest_scar = null;

        return base.Destroy(force);
    }

    // tg: code/datums/wounds/slash.dm:68
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        if (old_wound != null)
        {
            await set_blood_flow(max(Dm.num(old_wound.blood_flow), Dm.num(initial_flow)));
            datum_wound_slash_flesh old_cut = (datum_wound_slash_flesh)old_wound;
            if (old_cut.severity > severity && old_cut.highest_scar != null)
            {
                set_highest_scar(old_cut.highest_scar);
                old_cut.clear_highest_scar();
            }
        }
        else
        {
            await set_blood_flow(initial_flow);
            if (Dm.truthy(limb.can_bleed()) && Dm.truthy(attack_direction) && Dm.num(victim.get_blood_volume()) > BLOOD_VOLUME_OKAY)
                await victim.spray_blood(attack_direction, severity);
        }

        if (highest_scar == null)
        {
            datum_scar new_scar = Dm.@new<datum_scar>();
            set_highest_scar(new_scar);
            new_scar.generate(limb, this, add_to_scars: FALSE);
        }

        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/slash.dm:86
    public virtual void set_highest_scar(datum_scar new_scar = null)
    {
        if (highest_scar != null)
            UnregisterSignal(highest_scar, COMSIG_QDELETING);
        if (new_scar != null)
            RegisterSignal(new_scar, COMSIG_QDELETING, nameof(clear_highest_scar));
        highest_scar = new_scar;
    }

    // tg: code/datums/wounds/slash.dm:93
    [SignalHandler]
    public virtual void clear_highest_scar(datum source = null)
    {
        set_highest_scar(null);
    }

    // tg: code/datums/wounds/slash.dm:97
    public override async DmTask remove_wound(object ignore_limb = null, float? replaced = null, float? destroying = null)
    {
        if (!Dm.truthy(replaced) && highest_scar != null)
        {
            already_scarred = TRUE;
            highest_scar.lazy_attach(limb);
        }
        await base.remove_wound(ignore_limb, replaced, destroying);
    }

    // tg: code/datums/wounds/slash.dm:103
    public override object get_wound_description(mob user = null)
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze == null)
            return base.get_wound_description(user);

        string msg = I18n.T("The cuts on [] [] are wrapped with ", victim.p_their(), limb.plaintext_zone);
        // how much life we have left in these bandages
        float capacity = Dm.num(current_gauze.absorption_capacity);
        if (0 <= capacity && capacity <= 1.25f)
            msg += I18n.T("nearly ruined ");
        else if (1.25f <= capacity && capacity <= 2.75f)
            msg += I18n.T("badly worn ");
        else if (2.75f <= capacity && capacity <= 4)
            msg += I18n.T("slightly bloodied ");
        else if (4 <= capacity)
            msg += I18n.T("clean ");
        msg += I18n.T("[]!", current_gauze.name);

        return I18n.T("<B>[]</B>", msg);
    }

    // tg: code/datums/wounds/slash.dm:123
    [DmParams("wounding_type", "wounding_dmg", "wound_bonus")]
    public override async DmTask receive_damage(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object attack_direction = null, object damage_source = null)
    {
        if (victim == null) // if we are dismembered, we can still take damage, its fine to check here
            return;

        if (victim.stat != DEAD && !Dm.eq(wound_bonus, CANT_WOUND) && Dm.eq(wounding_type, WOUND_SLASH)) // can't stab dead bodies to make it bleed faster this way
            await adjust_blood_flow(WOUND_SLASH_DAMAGE_FLOW_COEFF * Dm.num(wounding_dmg));

        await base.receive_damage(wounding_type, wounding_dmg, wound_bonus, attack_direction, damage_source);
    }

    // tg: code/datums/wounds/slash.dm:132
    public override async DmTask<object> drag_bleed_amount()
    {
        // say we have 3 severe cuts with 3 blood flow each, pretty reasonable
        // compare with being at 100 brute damage before, where you bled (brute/100 * 2), = 2 blood per tile
        float bleed_amt = min(Dm.num(blood_flow) * 0.1f, 1); // 3 * 3 * 0.1 = 0.9 blood total, less than before! the share here is .3 blood of course.

        // if(limb.seep_gauze(bleed_amt * 0.33)) return 0: gauze stops all bleeding from dragging on this limb, but wears the gauze out
        // quicker; seep_gauze returns nothing (bodyparts/wounds.dm:331), so the branch is never taken
        await limb.seep_gauze(bleed_amt * 0.33f);

        return bleed_amt;
    }

    // tg: code/datums/wounds/slash.dm:142
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
        return null;
    }

    // tg: code/datums/wounds/slash.dm:153
    public override async DmTask handle_process(object seconds_per_tick = null)
    {
        if (victim == null || HAS_TRAIT(victim, TRAIT_STASIS))
            return;

        // in case the victim has the NOBLOOD trait, the wound will simply not clot on its own
        if (Dm.truthy(limb.can_bleed()))
        {
            if (Dm.num(clot_rate) > 0)
            {
                await adjust_blood_flow(-Dm.num(clot_rate) * Dm.num(seconds_per_tick));
                if (QDELETED(this))
                    return;
            }

            if (HAS_TRAIT(victim, TRAIT_BLOOD_FOUNTAIN))
                await adjust_blood_flow(0.25f); // old heparin used to just add +2 bleed stacks per tick, this adds 0.5 bleed flow to all open cuts which is probably even stronger as long as you can cut them first
        }

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (Dm.truthy(current_gauze?.absorption_rate))
        {
            float gauze_power = Dm.num(current_gauze.absorption_rate);
            await limb.seep_gauze(gauze_power * Dm.num(seconds_per_tick));
            await adjust_blood_flow(-gauze_power * Dm.num(seconds_per_tick));
        }
    }

    /* BEWARE, THE BELOW NONSENSE IS MADNESS. bones.dm looks more like what I have in mind and is sufficiently clean, don't pay attention to this messiness */

    // tg: code/datums/wounds/slash.dm:175
    public override object check_grab_treatments(obj_item tool = null, mob user = null)
    {
        if (istype(tool, typeof(obj_item_gun_energy_laser)))
            return TRUE;
        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST) // if we're using something hot but not a cautery, we need to be aggro grabbing them first, so we don't try treating someone we're eswording
            return TRUE;
        return FALSE;
    }

    // tg: code/datums/wounds/slash.dm:182
    public override async DmTask treat(obj_item tool = null, mob user = null)
    {
        if (istype(tool, typeof(obj_item_gun_energy_laser)))
            await las_cauterize((obj_item_gun_energy_laser)tool, user);
        else if (Dm.eq(tool.tool_behaviour, TOOL_CAUTERY) || Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            await tool_cauterize(tool, user);
    }

    // tg: code/datums/wounds/slash.dm:188
    public override async DmTask<object> try_handling(mob_living user = null)
    {
        if (user.pulling != victim || !HAS_TRAIT(user, TRAIT_WOUND_LICKER) || !Dm.truthy(victim.try_inject(user, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE)))
            return FALSE;
        if (user.hud_used?.screen_objects[HUD_MOB_ZONE_SELECTOR] != null && !Dm.eq(user.zone_selected, limb.body_zone))
            return FALSE;

        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, victim)))
        {
            to_chat(user, span_warning(I18n.T("You're already interacting with []!", victim)));
            return null;
        }
        if (iscarbon(user))
        {
            mob_living_carbon carbon_user = (mob_living_carbon)user;
            if (Dm.truthy(carbon_user.is_mouth_covered()))
            {
                to_chat(user, span_warning(I18n.T("Your mouth is covered, you can't lick []'s wounds!", victim)));
                return null;
            }
            if (!Dm.truthy(carbon_user.get_organ_slot(ORGAN_SLOT_TONGUE)))
            {
                to_chat(user, span_warning(I18n.T("You can't lick wounds without a tongue!"))); // f in chat
                return null;
            }
        }

        return await lick_wounds(user as mob_living_carbon_human);
    }

    /// if a felinid is licking this cut to reduce bleeding
    // tg: code/datums/wounds/slash.dm:209
    public virtual async DmTask<object> lick_wounds(mob_living_carbon_human user = null)
    {
        // transmission is one way patient -> felinid since google said cat saliva is antiseptic or whatever, and also because felinids are already risking getting beaten for this even without people suspecting they're spreading a deathvirus
        foreach (datum_disease iter_disease in Dm.each_cast<datum_disease>(victim.diseases))
        {
            if (Dm.truthy(Dm.bitand(iter_disease.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                continue;
            await user.ForceContractDisease(iter_disease);
        }

        if (Dm.truthy(user.combat_mode))
            return FALSE;

        user.visible_message(span_notice(I18n.T("[] begins licking the wounds on []'s [].", user, victim, limb.plaintext_zone)), span_notice(I18n.T("You begin licking the wounds on []'s []...", victim, limb.plaintext_zone)), ignored_mobs: victim);
        to_chat(victim, span_notice(I18n.T("[] begins to lick the wounds on your [].", user, limb.plaintext_zone)));
        if (!Dm.truthy(await do_after(user, base_treat_time, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return FALSE;

        user.visible_message(span_notice(I18n.T("[] licks the wounds on []'s [].", user, victim, limb.plaintext_zone)), span_notice(I18n.T("You lick some of the wounds on []'s []", victim, limb.plaintext_zone)), ignored_mobs: victim);
        to_chat(victim, span_green(I18n.T("[] licks the wounds on your []!", user, limb.plaintext_zone)));
        mob victim_stored = victim;
        await adjust_blood_flow(-0.5f);

        if (Dm.num(blood_flow) > Dm.num(minimum_flow))
            await try_handling(user);
        else if (demotes_to != null)
            to_chat(user, span_green(I18n.T("You successfully lower the severity of [] cuts.", user == victim_stored ? I18n.T("your") : I18n.T("[]'s", victim_stored))));

        return TRUE;
    }

    // tg: code/datums/wounds/slash.dm:236
    public override async DmTask adjust_blood_flow(object adjust_by = null, float? minimum = null)
    {
        await base.adjust_blood_flow(adjust_by, minimum);
        if (Dm.num(blood_flow) > WOUND_MAX_BLOODFLOW)
            blood_flow = WOUND_MAX_BLOODFLOW;
        if (Dm.num(blood_flow) < Dm.num(minimum_flow) && !QDELETED(this))
        {
            if (demotes_to != null)
                await replace_wound((datum_wound)await Dm.new_async(demotes_to));
            else
            {
                to_chat(victim, span_green(I18n.T("The cut on your [] has []!", limb.plaintext_zone, !Dm.truthy(limb.can_bleed()) ? I18n.T("healed up") : I18n.T("stopped bleeding"))));
                qdel(this);
            }
        }
    }

    // tg: code/datums/wounds/slash.dm:247
    public override async DmTask<object> on_xadone(object power = null)
    {
        object dot = await base.on_xadone(power);
        await adjust_blood_flow(-0.03f * Dm.num(power)); // i think it's like a minimum of 3 power, so .09 blood_flow reduction per tick is pretty good for 0 effort
        return dot;
    }

    // tg: code/datums/wounds/slash.dm:251
    public override async DmTask on_synthflesh(object reac_volume = null)
    {
        await base.on_synthflesh(reac_volume);
        await adjust_blood_flow(-0.075f * Dm.num(reac_volume)); // 20u * 0.075 = -1.5 blood flow, pretty good for how little effort it is
    }

    /// If someone's putting a laser gun up to our cut to cauterize it
    // tg: code/datums/wounds/slash.dm:256
    public virtual async DmTask las_cauterize(obj_item_gun_energy_laser lasgun = null, mob user = null)
    {
        float self_penalty_mult = (user == victim ? 1.25f : 1);
        user.visible_message(span_warning(I18n.T("[] begins aiming [] directly at []'s []...", user, lasgun, victim, limb.plaintext_zone)), span_userdanger(I18n.T("You begin aiming [] directly at [] []...", lasgun, user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone)));
        if (!Dm.truthy(await do_after(user, base_treat_time * self_penalty_mult, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return;
        float damage = Dm.num(lasgun.chambered.loaded_projectile.damage);
        lasgun.chambered.loaded_projectile.wound_bonus -= 30;
        lasgun.chambered.loaded_projectile.damage *= self_penalty_mult;
        if (!Dm.truthy(await lasgun.process_fire(victim, victim, TRUE, null, limb.body_zone as string)))
            return;
        await victim.emote("scream");
        victim.visible_message(span_warning(I18n.T("The cuts on []'s [] scar over!", victim, limb.plaintext_zone)));
        await adjust_blood_flow(-1 * (damage / (5 * self_penalty_mult))); // 20 / 5 = 4 bloodflow removed, p good
    }

    /// If someone is using either a cautery tool or something with heat to cauterize this cut
    // tg: code/datums/wounds/slash.dm:271
    public virtual async DmTask tool_cauterize(obj_item I = null, mob user = null)
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
            return;

        playsound(user, Dm.resource("sound/items/handling/surgery/cautery2.ogg"), 75, TRUE);

        string bleeding_wording = (Dm.truthy(limb.can_bleed()) ? I18n.T("bleeding") : I18n.T("cuts"));
        user.visible_message(span_green(I18n.T("[] cauterizes some of the [] on [].", user, bleeding_wording, victim)), span_green(I18n.T("You cauterize some of the [] on [].", bleeding_wording, victim)));
        await victim.apply_damage(2 + severity, BURN, limb, wound_bonus: CANT_WOUND);
        if (prob(30))
        {
            await victim.emote("scream");
        }
        float blood_cauterized = (0.6f / (self_penalty_mult * improv_penalty_mult));
        mob victim_stored = victim;
        await adjust_blood_flow(-blood_cauterized);

        if (Dm.num(blood_flow) > Dm.num(minimum_flow))
            try_treating(I, user as mob_living);

        else if (demotes_to != null)
            to_chat(user, span_green(I18n.T("You successfully lower the severity of [] cuts.", user == victim_stored ? I18n.T("your") : I18n.T("[]'s", victim_stored))));
    }
}

// tg: code/datums/wounds/slash.dm:308
public partial class datum_wound_slash_flesh_moderate : datum_wound_slash_flesh
{
    // tg: code/datums/wounds/slash.dm:328
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
            occur_text = I18n.T("is cut open");
    }
}

// tg: code/datums/wounds/slash.dm:339
public partial class datum_wound_slash_flesh_severe : datum_wound_slash_flesh
{
    // tg: code/datums/wounds/slash.dm:369
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
            occur_text = I18n.T("is ripped open");
    }
}

// tg: code/datums/wounds/slash.dm:373
public partial class datum_wound_slash_flesh_critical : datum_wound_slash_flesh
{
    // tg: code/datums/wounds/slash.dm:396
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
            occur_text = I18n.T("is torn open");
    }
}

// Subtype for cleave (heretic spell)
// tg: code/datums/wounds/slash.dm:419
public partial class datum_wound_slash_flesh_critical_cleave : datum_wound_slash_flesh_critical
{
    // tg: code/datums/wounds/slash.dm:424
    public override void update_descriptions()
    {
        if (!Dm.truthy(limb.can_bleed()))
            occur_text = I18n.T("is ruptured");
    }
}
