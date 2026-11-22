// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/tilting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file: falling and crushing (/atom/movable, the vendor's tilt) and righting a vendor. Deferred leaves: the
// crushed player's achievement (achievements, M10+).

public static partial class GlobalProcs
{
    /**
     * Can this atom be curshed by the vending machine
     * Arguments
     *
     * * atom/atom_target - the atom we are checking for
    */
    // tg: code/modules/vending/vendor/tilting.dm:7
    public static object check_atom_crushable(atom atom_target = null)
    {
        /// Contains structures and items that vendors shouldn't crush when we land on them.
        check_atom_crushable__vendor_uncrushable_objects ??= Dm.add(Dm.add(Dm.list(
            typeof(obj_structure_chair),
            typeof(obj_machinery_conveyor)
        ), GLOB.WALLITEMS_INTERIOR), GLOB.WALLITEMS_EXTERIOR) as DmList;

        //make sure its not in the list of "uncrushable" stuff
        if (Dm.truthy(is_type_in_list(atom_target, check_atom_crushable__vendor_uncrushable_objects)))
            return FALSE;

        //check if it has integrity + allow ninjas, etc to be crushed in cloak
        if (Dm.truthy(atom_target.uses_integrity) && !(atom_target.invisibility > SEE_INVISIBLE_LIVING))
            return TRUE; //SMUSH IT

        return FALSE;
    }

    // tg: code/modules/vending/vendor/tilting.dm:9  var/static/list/vendor_uncrushable_objects (rules 10.6: a proc-level static lives in TgState)
    private static ref DmList check_atom_crushable__vendor_uncrushable_objects => ref TgState.Current.check_atom_crushable__vendor_uncrushable_objects;
}

public sealed partial class TgState
{
    // tg: code/modules/vending/vendor/tilting.dm:9  check_atom_crushable's static list, built at first use (it reads GLOB)
    internal DmList check_atom_crushable__vendor_uncrushable_objects;
}

public partial class atom_movable
{
    /**
     * Causes src to fall onto [target], crushing everything on it (including itself) with [damage]
     * and a small chance to do a spectacular effect per entity (if a chance above 0 is provided).
     *
     * Returns: A collection of bitflags defined in crushing.dm. Read that file's documentation for info.
     */
    // tg: code/modules/vending/vendor/tilting.dm:41
    public virtual async DmTask<object> fall_and_crush(turf target = null, object damage = null, float? chance_to_crit = null, object forced_crit_case = null, object paralyze_time = null, object crush_dir = null, string damage_type = null, string damage_flag = null, float? rotation = null)
    {
        chance_to_crit ??= 0;
        crush_dir ??= get_dir(get_turf(this), target);
        damage_type ??= BRUTE;
        damage_flag ??= MELEE;
        rotation ??= 90;

        if (!(target != null)) throw Dm.CRASH(assertion_message("code/modules/vending/vendor/tilting.dm", 43, "!isnull(target)"), "code/modules/vending/vendor/tilting.dm", 43);

        float flags_to_return = NONE;

        if (!Dm.truthy(await target.is_blocked_turf(TRUE, this, Dm.list(this))))
        {
            foreach (atom atom_target in Dm.each<atom>(Dm.add(target.contents, target)))
            {
                if (isarea(atom_target))
                    continue;

                // SEND_SIGNAL(atom_target, COMSIG_PRE_TILT_AND_CRUSH, src), expanded inline (rules 11.1 (e))
                if (Dm.truthy(atom_target._listen_lookup?[COMSIG_PRE_TILT_AND_CRUSH]) && Dm.truthy(Dm.bitand(atom_target._SendSignal(COMSIG_PRE_TILT_AND_CRUSH, Dm.list(atom_target, this)), COMPONENT_IMMUNE_TO_TILT_AND_CRUSH)))
                    continue;

                object crit_case = forced_crit_case;
                if (crit_case == null && chance_to_crit > 0)
                {
                    if (prob(Dm.num(chance_to_crit)))
                        crit_case = pick_weight(get_crit_crush_chances() as DmList);
                }
                float crit_rebate_mult = 1; // lessen the normal damage we deal for some of the crits

                if (crit_case != null)
                {
                    crit_rebate_mult = Dm.num(fall_and_crush_crit_rebate_table(crit_case));
                    await apply_crit_crush(crit_case, atom_target);
                }

                float adjusted_damage = Dm.num(damage) * crit_rebate_mult;
                bool crushed = false;
                if (isliving(atom_target))
                {
                    crushed = true;
                    mob_living living_target = (mob_living)atom_target;
                    bool was_alive = !Dm.eq(living_target.stat, DEAD);
                    object blocked = living_target.run_armor_check(attack_flag: damage_flag);
                    if (iscarbon(living_target))
                    {
                        mob_living_carbon carbon_target = (mob_living_carbon)living_target;
                        if (prob(30))
                            await carbon_target.apply_damage(max(0, adjusted_damage), damage_type, blocked: Dm.num(blocked), spread_damage: TRUE, attack_direction: crush_dir); // the 30% chance to spread the damage means you escape breaking any bones
                        else
                        {
                            float brute = (damage_type == BRUTE ? Dm.num(damage) : 0) * 0.5f;
                            float burn = (damage_type == BURN ? Dm.num(damage) : 0) * 0.5f;
                            await carbon_target.take_bodypart_damage(brute, burn, check_armor: TRUE, wound_bonus: 5); // otherwise, deal it to 2 random limbs (or the same one) which will likely shatter something
                            await carbon_target.take_bodypart_damage(brute, burn, check_armor: TRUE, wound_bonus: 5);
                        }
                        await carbon_target.AddElement(typeof(datum_element_squish), 80 * SECONDS);
                    }
                    else
                        await living_target.apply_damage(adjusted_damage, damage_type, blocked: Dm.num(blocked), attack_direction: crush_dir);

                    await living_target.Paralyze(paralyze_time);
                    await living_target.emote("scream");
                    playsound(living_target, Dm.resource("sound/effects/blob/blobattack.ogg"), 40, TRUE);
                    playsound(living_target, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                    post_crush_living(living_target, was_alive ? TRUE : FALSE);
                    flags_to_return = Dm.bitor(flags_to_return, Dm.bitor(SUCCESSFULLY_CRUSHED_MOB, SUCCESSFULLY_CRUSHED_ATOM));
                }
                else if (Dm.truthy(check_atom_crushable(atom_target)))
                {
                    await atom_target.take_damage(adjusted_damage, damage_type, damage_flag, FALSE, crush_dir);
                    crushed = true;
                    flags_to_return = Dm.bitor(flags_to_return, SUCCESSFULLY_CRUSHED_ATOM);
                }

                if (crushed)
                {
                    atom_target.visible_message(span_danger(I18n.T("[] is crushed by []!", atom_target, this)), span_userdanger(I18n.T("You are crushed by []!", this)));
                    // SEND_SIGNAL(atom_target, COMSIG_POST_TILT_AND_CRUSH, src), expanded inline (rules 11.1 (e))
                    if (Dm.truthy(atom_target._listen_lookup?[COMSIG_POST_TILT_AND_CRUSH]))
                        atom_target._SendSignal(COMSIG_POST_TILT_AND_CRUSH, Dm.list(atom_target, this));
                }
            }

            object to_turn = turn(transform, Dm.num(rotation));
            animate(this, Dm.named("transform", to_turn), Dm.named("time", 0.2f * SECONDS));
            playsound(this, Dm.resource("sound/effects/bang.ogg"), 40);

            visible_message(span_danger(I18n.T("[] tips over, slamming hard onto []!", this, target)));
            flags_to_return = Dm.bitor(flags_to_return, SUCCESSFULLY_FELL_OVER);
            post_tilt();
        }
        else
            visible_message(span_danger(I18n.T("[] rebounds comically as it fails to slam onto []!", this, target)));

        await Move(target, crush_dir); // we still TRY to move onto it for shit like teleporters
        return flags_to_return;
    }

    /**
     * Returns a assoc list of (critcase -> num), where critcase is a critical define in crushing.dm and num is a weight.
     * Use with pickweight to acquire a random critcase.
     */
    // tg: code/modules/vending/vendor/tilting.dm:138
    public virtual object get_crit_crush_chances()
    {
        return Dm.list(
            Dm.kv(CRUSH_CRIT_SHATTER_LEGS, 100),
            Dm.kv(CRUSH_CRIT_PARAPLEGIC, 80),
            Dm.kv(CRUSH_CRIT_HEADGIB, 20),
            Dm.kv(CRUSH_CRIT_SQUISH_LIMB, 100)
        );
    }

    /**
     * Exists for the purposes of custom behavior.
     * Called directly after [crushed] is crushed.
     */
    // tg: code/modules/vending/vendor/tilting.dm:155
    public virtual void post_crush_living(mob_living crushed = null, object was_alive = null)
    {
        return;
    }

    /**
     * Exists for the purposes of custom behavior.
     * Called directly after src actually rotates and falls over.
     */
    // tg: code/modules/vending/vendor/tilting.dm:162
    public virtual void post_tilt()
    {
        return;
    }

    /**
     * Should be where critcase effects are actually implemented. Use this to apply critcases.
     *
     * Returns:
     * TRUE if a crit case is successfully applied, FALSE otherwise.
     */
    // tg: code/modules/vending/vendor/tilting.dm:174
    public virtual async DmTask<object> apply_crit_crush(object crit_case = null, atom atom_target = null)
    {
        switch (crit_case)
        {
            case CRUSH_CRIT_SHATTER_LEGS: // shatter their legs and bleed 'em
            {
                if (!iscarbon(atom_target))
                    return FALSE;
                mob_living_carbon carbon_target = (mob_living_carbon)atom_target;
                await carbon_target.bleed(150);
                obj_item_bodypart left_leg = carbon_target.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart;
                if (left_leg != null)
                    await left_leg.receive_damage(brute: 200);
                obj_item_bodypart right_leg = carbon_target.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart;
                if (right_leg != null)
                    await right_leg.receive_damage(brute: 200);
                if (left_leg != null || right_leg != null)
                    carbon_target.visible_message(span_danger(I18n.T("[]'s legs shatter with a sickening crunch!", carbon_target)), span_userdanger("Your legs shatter with a sickening crunch!"));
                return TRUE;
            }
            case CRUSH_CRIT_PARAPLEGIC: // paralyze this binch
            {
                // the new paraplegic gets like 4 lines of losing their legs so skip them
                if (!iscarbon(atom_target))
                    return FALSE;
                mob_living_carbon carbon_target = (mob_living_carbon)atom_target;
                visible_message(span_danger(I18n.T("[]'s spinal cord is obliterated with a sickening crunch!", carbon_target)), ignored_mobs: Dm.list(carbon_target));
                await carbon_target.gain_trauma(typeof(datum_brain_trauma_severe_paralysis_paraplegic));
                return TRUE;
            }
            case CRUSH_CRIT_SQUISH_LIMB: // limb squish!
            {
                if (!iscarbon(atom_target))
                    return FALSE;
                mob_living_carbon carbon_target = (mob_living_carbon)atom_target;
                foreach (obj_item_bodypart squish_part in Dm.each<obj_item_bodypart>(carbon_target.get_bodyparts()))
                {
                    object severity = pick(WOUND_SEVERITY_MODERATE, WOUND_SEVERITY_SEVERE, WOUND_SEVERITY_CRITICAL);
                    if (!Dm.truthy(await carbon_target.cause_wound_of_type_and_severity(WOUND_BLUNT, squish_part, severity, wound_source: I18n.T("crushed by []", this))))
                        await squish_part.receive_damage(brute: 30);
                }
                carbon_target.visible_message(span_danger(I18n.T("[]'s body is maimed underneath the mass of []!", carbon_target, this)), span_userdanger(I18n.T("Your body is maimed underneath the mass of []!", this)));
                return TRUE;
            }
            case CRUSH_CRIT_HEADGIB: // skull squish!
            {
                if (!iscarbon(atom_target))
                    return FALSE;
                mob_living_carbon carbon_target = (mob_living_carbon)atom_target;
                obj_item_bodypart_head carbon_head = carbon_target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
                if (carbon_head != null)
                {
                    if (Dm.truthy(await carbon_head.dismember()))
                    {
                        carbon_target.visible_message(span_danger(I18n.T("[] explodes in a shower of gore beneath []!", carbon_head, this)), span_userdanger("Oh f-"));
                        await carbon_head.drop_organs();
                        qdel(carbon_head);
                        Dm.@new<obj_effect_gibspawner_human_bodypartless>(get_turf(carbon_target), carbon_target);
                    }
                }
                return TRUE;
            }
        }

        return FALSE;
    }

    /**
     * Allows damage to be reduced on certain crit cases.
     * Args:
     * * crit_case: The critical case chosen.
     */
    // tg: code/modules/vending/vendor/tilting.dm:242
    public virtual object fall_and_crush_crit_rebate_table(object crit_case = null)
    {
        if (!(crit_case != null)) throw Dm.CRASH(assertion_message("code/modules/vending/vendor/tilting.dm", 243, "!isnull(crit_case)"), "code/modules/vending/vendor/tilting.dm", 243);

        switch (crit_case)
        {
            case CRUSH_CRIT_SHATTER_LEGS:
                return 0.2f;
            default:
                return 1;
        }
    }
}

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    /**
     * Tilts ontop of the atom supplied, if crit is true some extra shit can happen. See [fall_and_crush] for return values.
     * Arguments:
     * fatty - atom to tilt the vendor onto
     * local_crit_chance - percent chance of a critical hit
     * forced_crit - specific critical hit case to use, if any
     * range - the range of the machine when thrown if not adjacent
    */
    // tg: code/modules/vending/vendor/tilting.dm:212
    public virtual async DmTask<object> tilt(atom fatty = null, float? local_crit_chance = null, object forced_crit = null, float? range = null)
    {
        local_crit_chance ??= crit_chance;
        range ??= 1;
        if (QDELETED(this) || !Dm.truthy(has_gravity(get_turf(this) as turf)))
            return null;

        object dot = NONE;

        object picked_rotation = pick(90, 270);
        if (Dm.truthy(await Adjacent(fatty)))
        {
            dot = await fall_and_crush(get_turf(fatty), squish_damage, local_crit_chance, forced_crit, 6 * SECONDS, rotation: Dm.num(picked_rotation));

            if (Dm.truthy(Dm.bitand(dot, SUCCESSFULLY_FELL_OVER)))
            {
                visible_message(span_danger(I18n.T("[] tips over!", this)));
                tilted = TRUE;
                tilted_rotation = Dm.num(picked_rotation);
                layer = ABOVE_MOB_LAYER;
            }
        }

        if (!Dm.eq(get_turf(fatty), get_turf(this)))
            await throw_at(get_turf(fatty), range, 1, spin: FALSE, quickstart: FALSE);
        return dot;
    }

    // tg: code/modules/vending/vendor/tilting.dm:231
    public override void post_crush_living(mob_living crushed = null, object was_alive = null)
    {
        if (Dm.truthy(was_alive) && Dm.eq(crushed.stat, DEAD) && crushed.client != null)
            Dm.Deferred("code/modules/vending/vendor/tilting.dm:234", "post_crush_living: crushed.client.give_award(/datum/award/achievement/misc/vendor_squish, crushed) (achievements, M10+)");

        add_memory_in_range(crushed, 7, typeof(datum_memory_witness_vendor_crush), Dm.named("protagonist", crushed), Dm.named("antagonist", this));

        base.post_crush_living(crushed, was_alive);
    }

    // tg: code/modules/vending/vendor/tilting.dm:254
    public override object fall_and_crush_crit_rebate_table(object crit_case = null)
    {
        return Dm.eq(crit_case, VENDOR_CRUSH_CRIT_GLASSCANDY) ? 0.33f : base.fall_and_crush_crit_rebate_table(crit_case);
    }

    // tg: code/modules/vending/vendor/tilting.dm:257
    public override object get_crit_crush_chances()
    {
        return Dm.list(
            Dm.kv(VENDOR_CRUSH_CRIT_GLASSCANDY, 100),
            Dm.kv(VENDOR_CRUSH_CRIT_PIN, 100)
        );
    }

    // tg: code/modules/vending/vendor/tilting.dm:263
    public override async DmTask<object> apply_crit_crush(object crit_case = null, atom atom_target = null)
    {
        object dot = await base.apply_crit_crush(crit_case, atom_target);
        if (Dm.truthy(dot))
            return TRUE;

        switch (crit_case)
        {
            case VENDOR_CRUSH_CRIT_GLASSCANDY:
            {
                if (!iscarbon(atom_target))
                    return FALSE;
                mob_living_carbon carbon_target = (mob_living_carbon)atom_target;
                for (float i = 1; i <= 7; i++)
                {
                    obj_item_shard shard = Dm.@new<obj_item_shard>(get_turf(carbon_target));
                    datum_embedding embed = shard.get_embed() as datum_embedding;
                    embed.embed_chance = 100;
                    embed.ignore_throwspeed_threshold = TRUE;
                    embed.impact_pain_mult = 1;
                    await carbon_target.hitby(shard, skipcatch: TRUE, hitpush: FALSE);
                    embed.embed_chance = Dm.initial(embed, (datum_embedding p) => p.embed_chance);
                    embed.ignore_throwspeed_threshold = Dm.initial(embed, (datum_embedding p) => p.ignore_throwspeed_threshold);
                    embed.impact_pain_mult = Dm.initial(embed, (datum_embedding p) => p.impact_pain_mult);
                }
                return TRUE;
            }
            case VENDOR_CRUSH_CRIT_PIN: // pin them beneath the machine until someone untilts it
            {
                if (!isliving(atom_target))
                    return FALSE;
                mob_living living_target = (mob_living)atom_target;
                await forceMove(get_turf(living_target));
                await buckle_mob(living_target, force: TRUE);
                living_target.visible_message(span_danger(I18n.T("[] is pinned underneath []!", living_target, this)), span_userdanger(I18n.T("You are pinned down by []!", this)));
                return TRUE;
            }
        }

        return FALSE;
    }

    /**
     * Rights the vendor up, unpinning mobs under it, if any.
     * Arguments:
     * user - mob that has untilted the vendor
     */
    // tg: code/modules/vending/vendor/tilting.dm:300
    public virtual async DmTask untilt(mob user = null)
    {
        if (user != null)
            user.visible_message(span_notice(I18n.T("[] rights [].", user, this)),
                span_notice(I18n.T("You right [].", this)));

        unbuckle_all_mobs(TRUE);

        tilted = FALSE;
        layer = Dm.initial(this, (atom p) => p.layer);

        object to_turn = turn(transform, -tilted_rotation);
        animate(this, Dm.named("transform", to_turn), Dm.named("time", 0.2f * SECONDS));
        tilted_rotation = 0;
    }

    // tg: code/modules/vending/vendor/tilting.dm:314
    public override async DmTask<object> onZImpact(turf impacted_turf = null, object levels = null, float? impact_flags = null)
    {
        impact_flags = Dm.num(Dm.bitor(impact_flags ?? 0, ZIMPACT_NO_MESSAGE));
        if (Dm.truthy(Dm.bitand(await fall_and_crush(impacted_turf, squish_damage, paralyze_time: 6 * SECONDS, crush_dir: UP, rotation: Dm.num(pick(90, 270))), SUCCESSFULLY_FELL_OVER)))
            impact_flags = Dm.num(Dm.bitor(impact_flags, ZIMPACT_NO_SPIN));
        return await base.onZImpact(impacted_turf, levels, impact_flags);
    }
}
