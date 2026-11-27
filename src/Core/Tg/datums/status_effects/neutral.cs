// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/neutral.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M7x slice D): the held-up and holding-up effects of the gunpoint component, the surrender alert (heldup applies the grouped
// surrender effect, whose alert's click is the emote that M9 translates) and their alerts; the offering and its subtypes (M9 slice F,
// the give key and the hand items); gaps R3 the in-love effect; gaps H1 the washing effect (the showers) and the hot spring's; gaps RG1 (D339) the soothed throat and
// headache and the Cherenkov radiation of the drinks, and the tinea luxor's glow; gaps RG3 the eigenstasium wild ride; the gutted corpse of the bitrunning domains. The rest of neutral.dm (caltropped, ...) comes
// with its users. The var blocks are generated (D111), but the washing effect's.

// heldup is for the person being aimed at
// tg: code/datums/status_effects/neutral.dm:163
public partial class datum_status_effect_grouped_heldup : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/neutral.dm:176
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.apply_status_effect(typeof(datum_status_effect_grouped_surrender), REF(this));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/neutral.dm:180
    public override async DmTask<object> on_remove()
    {
        owner.remove_status_effect(typeof(datum_status_effect_grouped_surrender), REF(this));
        return await base.on_remove();
    }
}

// holdup is for the person aiming
// tg: code/datums/status_effects/neutral.dm:185
public partial class datum_status_effect_holdup : datum_status_effect
{
}

// tg: code/datums/status_effects/neutral.dm:192
public partial class atom_movable_screen_alert_status_effect_holdup : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:199
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        datum_component_gunpoint gunpoint = owner.GetComponent(typeof(datum_component_gunpoint)) as datum_component_gunpoint;
        gunpoint?.cancel();
        return dot;
    }
}

//this effect gives the user an alert they can use to surrender quickly
// tg: code/datums/status_effects/neutral.dm:371
public partial class datum_status_effect_grouped_surrender : datum_status_effect_grouped
{
}

// tg: code/datums/status_effects/neutral.dm:378
public partial class atom_movable_screen_alert_status_effect_surrender : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:385
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        await owner.emote("surrender");
        return dot;
    }
}

///For when you need to make someone be prompted for surrender, but not forever
// tg: code/datums/status_effects/neutral.dm:394  (gaps S3: the security pen's prompt)
public partial class datum_status_effect_surrender_timed : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:399
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.apply_status_effect(typeof(datum_status_effect_grouped_surrender), REF(this));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/neutral.dm:403
    public override async DmTask<object> on_remove()
    {
        owner.remove_status_effect(typeof(datum_status_effect_grouped_surrender), REF(this));
        return await base.on_remove();
    }
}

// this status effect is used to negotiate the high-fiving capabilities of all concerned parties
// tg: code/datums/status_effects/neutral.dm:207  (M9 slice F)
public partial class datum_status_effect_offering : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:222
    [DmParams("new_owner", "offer", "give_alert_override", "offered")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        obj_item offer = Dm.arg<obj_item>(__extra, 0);
        object give_alert_override = Dm.arg<object>(__extra, 1);
        mob_living offered = Dm.arg<mob_living>(__extra, 2);
        object dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;
        offered_item = offer;
        if (Dm.truthy(give_alert_override))
            give_alert_type = give_alert_override as Type;

        if (offered != null && await is_taker_elligible(offered))
            register_candidate(offered);
        else
        {
            foreach (mob_living possible_taker in Dm.each<mob_living>(orange(1, owner)))
            {
                if (!await is_taker_elligible(possible_taker))
                    continue;

                register_candidate(possible_taker);
            }
        }

        if (!Dm.truthy(possible_takers)) // no one around
        {
            qdel(this);
            return dot;
        }

        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(check_owner_in_range));
        RegisterSignals(offered_item, Dm.list(COMSIG_QDELETING, COMSIG_ITEM_DROPPED), nameof(dropped_item));
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:246
    public override object Destroy(float? force = null)
    {
        foreach (mob_living removed_taker in Dm.each<mob_living>(possible_takers?.Copy()))
            remove_candidate(removed_taker);
        LAZYCLEARLIST(possible_takers);
        offered_item = null;
        return base.Destroy(force);
    }

    /// Hook up the specified living mob to be offered the item in question, give them the alert and signals and all
    // tg: code/datums/status_effects/neutral.dm:254
    public virtual void register_candidate(mob_living possible_candidate = null)
    {
        atom_movable_screen_alert_give G = possible_candidate.throw_alert(Dm.text("[]", owner), give_alert_type) as atom_movable_screen_alert_give;
        if (G == null)
            return;
        LAZYADD(ref possible_takers, possible_candidate);
        RegisterSignal(possible_candidate, COMSIG_MOVABLE_MOVED, nameof(check_taker_in_range));
        G.setup(possible_candidate, this);
    }

    /// Remove the alert and signals for the specified living mob. Automatically removes the status effect when we lost the last taker
    // tg: code/datums/status_effects/neutral.dm:263
    public virtual void remove_candidate(mob_living removed_candidate = null)
    {
        removed_candidate.clear_alert(Dm.text("[]", owner));
        LAZYREMOVE(ref possible_takers, removed_candidate);
        UnregisterSignal(removed_candidate, COMSIG_MOVABLE_MOVED);
        if (!Dm.truthy(possible_takers) && !QDELING(this))
            qdel(this);
    }

    /// One of our possible takers moved, see if they left us hanging
    // tg: code/datums/status_effects/neutral.dm:271
    [SignalHandler]
    public virtual void check_taker_in_range(mob_living taker = null)
    {
        if (Dm.truthy(Waiting.NoSleep(taker.IsReachableBy(owner))) || ((owner.pulling == taker) || (taker.pulling == owner)) && !Dm.truthy(taker.incapacitated))
            return;

        to_chat(taker, span_warning(I18n.T("You moved out of range of []!", owner)));
        remove_candidate(taker);
    }

    /// The offerer moved, see if anyone is out of range now
    // tg: code/datums/status_effects/neutral.dm:280
    [SignalHandler]
    public virtual void check_owner_in_range(mob_living source = null)
    {
        foreach (object checking in Dm.each(possible_takers?.Copy()))
        {
            mob_living checking_taker = checking as mob_living;
            if (!(checking is mob_living) || (!Dm.truthy(Waiting.NoSleep(checking_taker.IsReachableBy(owner))) && !((owner.pulling == checking_taker) || (checking_taker.pulling == owner))) || Dm.truthy(checking_taker.incapacitated))
                remove_candidate(checking_taker);
        }
    }

    /// We lost the item, give it up
    // tg: code/datums/status_effects/neutral.dm:288
    [SignalHandler]
    public virtual void dropped_item(obj_item source = null)
    {
        qdel(this);
    }

    /**
     * Is our taker valid as a target for the offering? Meant to be used when registering
     * takers in `on_creation()`. You should override `additional_taker_check()` instead of this.
     *
     * Returns `TRUE` if the taker is valid as a target for the offering.
     */
    // tg: code/datums/status_effects/neutral.dm:298
    public virtual async DmTask<bool> is_taker_elligible(mob_living taker = null)
    {
        return Dm.truthy(await taker.IsReachableBy(owner)) && !Dm.truthy(taker.incapacitated) && additional_taker_check(taker);
    }

    /**
     * Additional checks added to `CanReach()` and `incapacitated` in `is_taker_elligible()`.
     * Should be what you override instead of `is_taker_elligible()`. By default, checks if the
     * taker can hold items.
     *
     * Returns `TRUE` if the taker is valid as a target for the offering based on these
     * additional checks.
     */
    // tg: code/datums/status_effects/neutral.dm:309
    public virtual bool additional_taker_check(mob_living taker = null)
    {
        return Dm.truthy(taker.can_hold_items());
    }
}

/**
 * This status effect is meant only for items that you don't actually receive
 * when offered, mostly useful for `/obj/item/hand_item` subtypes.
 */
// tg: code/datums/status_effects/neutral.dm:318
public partial class datum_status_effect_offering_no_item_received : datum_status_effect_offering
{
    // tg: code/datums/status_effects/neutral.dm:318
    public override bool additional_taker_check(mob_living taker = null)
    {
        return taker.usable_hands > 0;
    }
}

/**
 * This status effect is meant only to be used for offerings that require the target to
 * be resting (like when you're trying to give them a hand to help them up).
 * Also doesn't require them to have their hands free (since you're not giving them
 * anything).
 */
// tg: code/datums/status_effects/neutral.dm:329
public partial class datum_status_effect_offering_no_item_received_needs_resting : datum_status_effect_offering_no_item_received
{
    // tg: code/datums/status_effects/neutral.dm:329
    public override bool additional_taker_check(mob_living taker = null)
    {
        return Dm.eq(taker.body_position, LYING_DOWN);
    }

    // tg: code/datums/status_effects/neutral.dm:332
    [DmParams("new_owner", "offer", "give_alert_override", "offered")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(check_owner_standing));
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:336
    public override void register_candidate(mob_living possible_candidate = null)
    {
        base.register_candidate(possible_candidate);
        RegisterSignal(possible_candidate, COMSIG_LIVING_SET_BODY_POSITION, nameof(check_candidate_resting));
    }

    // tg: code/datums/status_effects/neutral.dm:340
    public override void remove_candidate(mob_living removed_candidate = null)
    {
        UnregisterSignal(removed_candidate, COMSIG_LIVING_SET_BODY_POSITION);
        base.remove_candidate(removed_candidate);
    }

    /// Simple signal handler that ensures that, if the owner stops standing, the offer no longer stands either!
    // tg: code/datums/status_effects/neutral.dm:345
    [SignalHandler]
    public virtual void check_owner_standing(mob_living owner = null)
    {
        if (Dm.eq(this.owner.body_position, STANDING_UP))
            return;

        // This doesn't work anymore if the owner is no longer standing up, sorry!
        qdel(this);
    }

    /// Simple signal handler that ensures that, should a candidate now be standing up, the offer won't be standing for them anymore!
    // tg: code/datums/status_effects/neutral.dm:353
    [SignalHandler]
    public virtual void check_candidate_resting(mob_living candidate = null)
    {
        if (Dm.eq(candidate.body_position, LYING_DOWN))
            return;

        // No longer lying down? You're no longer eligible to take the offer, sorry!
        remove_candidate(candidate);
    }
}

/// Subtype for high fives, so we can fake out people
// tg: code/datums/status_effects/neutral.dm:365
public partial class datum_status_effect_offering_no_item_received_high_five : datum_status_effect_offering_no_item_received
{
    // tg: code/datums/status_effects/neutral.dm:366
    [SignalHandler]
    public override void dropped_item(obj_item source = null)
    {
        // Lets us "too slow" people, instead of qdeling we just handle the ref
        offered_item = null;
    }
}

// tg: code/datums/status_effects/neutral.dm:897  (the owner, 2026-11-02: the dev esp, tg's Toggle Admin ESP)
public partial class datum_status_effect_admin_esp : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:906
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        real_invis_see = owner.see_invisible;
        owner.set_invis_see(SEE_INVISIBLE_ADMIN);
        RegisterSignal(owner, COMSIG_MOB_SEE_INVIS_CHANGE, nameof(on_invis_changed));
        return TRUE;
    }

    // tg: code/datums/status_effects/neutral.dm:912
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOB_SEE_INVIS_CHANGE);
        owner.set_invis_see(real_invis_see); // restore our 'real' invis_see
        return null;
    }

    /// Whenever our invis_see updates from some other source, keep real_invis_see up to date
    // tg: code/datums/status_effects/neutral.dm:917
    [SignalHandler]
    public virtual void on_invis_changed(datum source = null, object see_invis = null, object old_invis = null)
    {
        // SIGNAL_HANDLER
        real_invis_see = see_invis;
    }
}

// Gaps R3 (D327): the in-love effect of Valentine's Day (hearts over the date only the lover sees) and its alert.

// tg: code/datums/status_effects/neutral.dm:73
public partial class datum_status_effect_in_love : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:80
    [DmParams("new_owner", "date")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        mob_living date = Dm.arg<mob_living>(__extra, 0);
        object dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;

        linked_alert.desc = I18n.T("You're in love with []! How lovely.", date.real_name);
        hearts = WEAKREF(date.add_alt_appearance(
            typeof(datum_atom_hud_alternate_appearance_basic_one_person),
            "in_love",
            image(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "love_hearts", loc: date),
            null,
            new_owner) as datum);
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:94
    public override async DmTask<object> on_remove()
    {
        // DEVIATION(DV430): tg's QDEL_NULL(hearts) deletes only the weak reference and leaves the hearts drawn over the date; the
        // port deletes the alternate appearance it points to.
        qdel((hearts as datum_weakref)?.resolve() as datum);
        QDEL_NULL(ref hearts);
        await DmTask.CompletedTask;
        return null;
    }
}

// Gaps H1: the shower's washing effect and the hot spring's (washing_regen), with their alerts. The washing effect's var block is
// hand-written: tg's shower_reagent holds the shower's reagent datum (has_reagent), the hot spring's a type path, so it is an object
// here (types-converter.md 3.4).
// tg: code/datums/status_effects/neutral.dm:661
public partial class datum_status_effect_washing_regen : datum_status_effect
{
    /// How much stamina we regain from washing
    // tg: code/datums/status_effects/neutral.dm:667
    public float stamina_heal_per_tick = -4;
    /// How much brute, tox and fie damage we heal from this
    // tg: code/datums/status_effects/neutral.dm:669
    public float heal_per_tick = 0;
    /// The main reagent used for the shower (if no reagent is at least 70% of volume then it's null)
    // tg: code/datums/status_effects/neutral.dm:671
    public object shower_reagent;

    // tg: code/datums/status_effects/neutral.dm:662  (a hand-written var block: shower_reagent holds a datum or a type path)
    public datum_status_effect_washing_regen()
    {
        id = "shower_regen";
        duration = STATUS_EFFECT_PERMANENT;
        status_type = STATUS_EFFECT_UNIQUE;
        alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen);
    }

    // tg: code/datums/status_effects/neutral.dm:673
    [DmParams("new_owner", "shower_reagent")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object shower_reagent = Dm.arg(__extra, 0);
        if (!Dm.truthy(this.shower_reagent))
            this.shower_reagent = shower_reagent;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/neutral.dm:678
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        // istype() is false for a type path, so the hot spring's path falls through every branch (tg's own behaviour)
        if (shower_reagent is datum_reagent_blood)
        {
            if (HAS_TRAIT(owner, TRAIT_MORBID) || HAS_TRAIT(owner, TRAIT_EVIL) || Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_UNDEAD)))
                alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen_bloody_like);
            else
                alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen_bloody_dislike);
        }
        else if (shower_reagent is datum_reagent_water)
        {
            if (HAS_TRAIT(owner, TRAIT_WATER_HATER) && !HAS_TRAIT(owner, TRAIT_WATER_ADAPTATION))
                alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen_hater);
            else
                alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen);
        }
        else if (!Dm.truthy(shower_reagent)) // dirty shower
            alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen_dislike);
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:693
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        float seconds = Dm.num(seconds_between_ticks);

        bool is_disgusted = false;

        if (shower_reagent is datum_reagent_water)
        {
            bool water_adaptation = HAS_TRAIT(owner, TRAIT_WATER_ADAPTATION);
            bool water_hater = HAS_TRAIT(owner, TRAIT_WATER_HATER);
            float stam_recovery = (water_hater && !water_adaptation ? -stamina_heal_per_tick : stamina_heal_per_tick) * seconds;
            float recovery = heal_per_tick;
            if (water_adaptation)
            {
                recovery -= 1;
                stam_recovery *= 1.5f;
            }
            else if (water_hater)
                recovery *= 0;
            recovery *= seconds;

            float healed = 0;
            if (recovery != 0) //very mild healing for those with the water adaptation trait (fish infusion)
            {
                healed += Dm.num(await owner.adjust_oxy_loss(recovery * (water_adaptation ? 1.5f : 1f), updating_health: FALSE, required_biotype: MOB_ORGANIC));
                healed += Dm.num(await owner.adjust_fire_loss(recovery, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
                healed += Dm.num(await owner.adjust_tox_loss(recovery, updating_health: FALSE, required_biotype: MOB_ORGANIC));
                healed += Dm.num(await owner.adjust_brute_loss(recovery, updating_health: FALSE, required_bodytype: BODYTYPE_ORGANIC));
            }
            healed += Dm.num(await owner.adjust_stamina_loss(stam_recovery, updating_stamina: FALSE));
            if (healed != 0)
                await owner.updatehealth();
        }
        else if (shower_reagent is datum_reagent_blood)
        {
            bool enjoy_bloody_showers = HAS_TRAIT(owner, TRAIT_MORBID) || HAS_TRAIT(owner, TRAIT_EVIL) || Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_UNDEAD));
            is_disgusted = !enjoy_bloody_showers;
        }
        else if (!Dm.truthy(shower_reagent)) // dirty shower
            is_disgusted = true;

        if (is_disgusted)
            owner.adjust_disgust(2);
        return dot;
    }
}

// tg: code/datums/status_effects/neutral.dm:749
public partial class datum_status_effect_washing_regen_hot_spring : datum_status_effect_washing_regen
{
    // tg: code/datums/status_effects/neutral.dm:755
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (HAS_TRAIT(owner, TRAIT_WATER_HATER) && !HAS_TRAIT(owner, TRAIT_WATER_ADAPTATION))
            alert_type = typeof(atom_movable_screen_alert_status_effect_washing_regen_hotspring_hater);
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:760
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        owner.adjust_bodytemperature(10 * Dm.num(seconds_between_ticks), 0, T0C + 45);
        return dot;
    }
}

// gaps RG1 (D339): the drinks' soothed throat (the peppermint patty, the sea breeze, the mouthwashes, menthol), soothed headache (the
// suffering bastard) and Cherenkov radiation (the thermonuclear daiquiri).

// tg: code/datums/status_effects/neutral.dm:98
public partial class datum_status_effect_throat_soothed : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:103
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        ADD_TRAIT(owner, TRAIT_SOOTHED_THROAT, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:107
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_SOOTHED_THROAT, TRAIT_STATUS_EFFECT(id));
        return dot;
    }
}

// tg: code/datums/status_effects/neutral.dm:112
public partial class datum_status_effect_headache_soothed : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:117
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        ADD_TRAIT(owner, TRAIT_SOOTHED_HEADACHE, TRAIT_STATUS_EFFECT(id));
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:121
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        REMOVE_TRAIT(owner, TRAIT_SOOTHED_HEADACHE, TRAIT_STATUS_EFFECT(id));
        return dot;
    }
}

///Makes the mob glow blue and rarely emit nuclear particles
// tg: code/datums/status_effects/neutral.dm:622
public partial class datum_status_effect_cherenkov_radiation : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:628
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        if (Dm.truthy(duration))
            this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/neutral.dm:633
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        mob_light_obj = owner.mob_light(2, 4, "#33ddff") as obj_effect_dummy_lighting_obj_moblight;
        return TRUE;
    }

    // tg: code/datums/status_effects/neutral.dm:637
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (prob(3))
            await owner.fire_nuclear_particle();
        return null;
    }

    // tg: code/datums/status_effects/neutral.dm:641
    public override async DmTask<object> on_remove()
    {
        QDEL_NULL(ref mob_light_obj);
        return null;
    }
}

// tg: code/datums/status_effects/neutral.dm:643
public partial class datum_status_effect_gutted : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:649
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOB_STATCHANGE, nameof(stop_gutting));
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/status_effects/neutral.dm:653
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOB_STATCHANGE);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/status_effects/neutral.dm:656
    [SignalHandler]
    public virtual void stop_gutting()
    {
        qdel(this);
    }
}

///Makes the mob luminescent for the duration of the effect.
// tg: code/datums/status_effects/neutral.dm:601  (gaps RG1: the tinea luxor's glow)
public partial class datum_status_effect_tinlux_light : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:608
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        if (Dm.truthy(duration))
            this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/neutral.dm:613
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        mob_light_obj = owner.mob_light(2, 1.5f, "#ccff33") as obj_effect_dummy_lighting_obj_moblight;
        return TRUE;
    }

    // tg: code/datums/status_effects/neutral.dm:617
    public override async DmTask<object> on_remove()
    {
        QDEL_NULL(ref mob_light_obj);
        return null;
    }
}

// Gaps RG3 (D339): the eigenstasium wild ride (the overdose of eigenstasium; bluespace dust, the stabilizing agent and unoverdosed
// eigenstasium hold it back). Its last phase's quirk reroll waits for the quirks (D333); its dropped temporary moods are
// live since gaps MS1 (gaps train 11).
// tg: code/datums/status_effects/neutral.dm:431
public partial class datum_status_effect_eigenstasium : datum_status_effect
{
    // tg: code/datums/status_effects/neutral.dm:424
    private const float EIGENSTASIUM_MAX_BUFFER = -251;
    // tg: code/datums/status_effects/neutral.dm:425
    private const float EIGENSTASIUM_STABILISATION_RATE = 5;
    // tg: code/datums/status_effects/neutral.dm:426
    private const float EIGENSTASIUM_PHASE_1_END = 50;
    // tg: code/datums/status_effects/neutral.dm:427
    private const float EIGENSTASIUM_PHASE_2_END = 80;
    // tg: code/datums/status_effects/neutral.dm:428
    private const float EIGENSTASIUM_PHASE_3_START = 100;
    // tg: code/datums/status_effects/neutral.dm:429
    private const float EIGENSTASIUM_PHASE_3_END = 150;

    // tg: code/datums/status_effects/neutral.dm:445
    public override object Destroy(float? force = null)
    {
        if (alt_clone != null)
        {
            UnregisterSignal(alt_clone, COMSIG_QDELETING);
            qdel(alt_clone);
            alt_clone = null;
        }
        return base.Destroy(force);
    }

    // tg: code/datums/status_effects/neutral.dm:451
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        //This stuff runs every cycle
        if (prob(5))
            do_sparks(5, FALSE, owner);

        //If we have a reagent that blocks the effects
        bool block_effects = false;
        if (Dm.truthy(owner.has_reagent(typeof(datum_reagent_bluespace))))
        {
            current_cycle = Dm.max(EIGENSTASIUM_MAX_BUFFER, (current_cycle - (EIGENSTASIUM_STABILISATION_RATE * 1.5f))); //cap to -250
            block_effects = true;
        }
        if (Dm.truthy(owner.has_reagent(typeof(datum_reagent_stabilizing_agent))))
        {
            current_cycle = Dm.max(EIGENSTASIUM_MAX_BUFFER, (current_cycle - EIGENSTASIUM_STABILISATION_RATE));
            block_effects = true;
        }
        var eigen = owner.has_reagent(typeof(datum_reagent_eigenstate)) as datum_reagent;
        if (eigen != null)
        {
            if (Dm.truthy(eigen.overdosed))
                block_effects = false;
            else
            {
                current_cycle = Dm.max(EIGENSTASIUM_MAX_BUFFER, (current_cycle - (EIGENSTASIUM_STABILISATION_RATE * 2)));
                block_effects = true;
            }
        }

        if (!QDELETED(alt_clone)) //catch any stragglers
        {
            do_sparks(5, FALSE, alt_clone);
            owner.visible_message(I18n.T("[] is snapped across to a different alternative reality!", owner));
            qdel(alt_clone);
            alt_clone = null;
        }

        if (block_effects)
        {
            if (!Dm.truthy(stable_message))
            {
                owner.visible_message(I18n.T("You feel stable...for now."));
                stable_message = TRUE;
            }
            return dot;
        }
        stable_message = FALSE;

        //Increment cycle
        current_cycle++; //needs to be done here because phase 2 can early return

        //These run on specific cycles
        if (current_cycle == 0)
            to_chat(owner, span_userdanger(I18n.T("You feel like you're being pulled across to somewhere else. You feel empty inside.")));
        //phase 1
        else if (current_cycle >= 1 && current_cycle <= EIGENSTASIUM_PHASE_1_END)
        {
            await owner.set_jitter_if_lower(4 * SECONDS);
            await owner.adjust_nutrition(-4);
        }
        //phase 2
        else if (current_cycle >= EIGENSTASIUM_PHASE_1_END && current_cycle <= EIGENSTASIUM_PHASE_2_END)
        {
            if (current_cycle == 51)
            {
                to_chat(owner, span_userdanger(I18n.T("You start to convlse violently as you feel your consciousness merges across realities, your possessions flying wildy off your body!")));
                await owner.set_jitter_if_lower(400 * SECONDS);
                await owner.Knockdown(10);
            }

            DmList items = Dm.list();
            float max_loop;
            if (length(owner.get_contents()) >= 10)
                max_loop = 10;
            else
                max_loop = length(owner.get_contents());
            for (float i = 1; i <= max_loop; i++)
            {
                var item = ((DmList)owner.get_contents())[i] as obj_item;
                if (item == null || Dm.truthy(Dm.bitand(item.item_flags, DROPDEL)) || HAS_TRAIT(item, TRAIT_NODROP)) // can't teleport these kinds of items
                    continue;
                items.Add(item);
            }

            if (!Dm.truthy(LAZYLEN(items)))
                return dot; // tg: return ..()
            var picked = pick(items) as obj_item;
            await owner.dropItemToGround(picked, TRUE);
            do_sparks(5, FALSE, picked);
            await do_teleport(picked, get_turf(picked), 3, no_effects: TRUE);
            do_sparks(5, FALSE, picked);
        }
        //phase 3 - little break to get your items
        else if (current_cycle >= EIGENSTASIUM_PHASE_3_START && current_cycle <= EIGENSTASIUM_PHASE_3_END)
        {
            //Clone function - spawns a clone then deletes it - simulates multiple copies of the player teleporting in
            if (phase_3_cycle == 0) //Loops 0 -> 1 -> 2 -> 1 -> 2 -> 1 ...ect.
            {
                await owner.set_jitter_if_lower(200 * SECONDS);
                to_chat(owner, span_userdanger(I18n.T("Your eigenstate starts to rip apart, drawing in alternative reality versions of yourself!")));
            }
            else if (phase_3_cycle == 1)
            {
                var typepath = owner.type;
                alt_clone = Dm.@new(typepath, owner.loc) as mob_living_carbon;
                alt_clone.appearance = owner.appearance;
                alt_clone.real_name = owner.real_name;
                RegisterSignal(alt_clone, COMSIG_QDELETING, nameof(remove_clone_from_var));
                owner.visible_message(I18n.T("[] splits into seemingly two versions of themselves!", owner));
                await do_teleport(alt_clone, get_turf(alt_clone), 2, no_effects: TRUE); //teleports clone so it's hard to find the real one!
                do_sparks(5, FALSE, alt_clone);
                await alt_clone.emote("spin");
                await owner.emote("spin");
                object say_phrases = strings(EIGENSTASIUM_FILE, "lines");
                await alt_clone.say(pick(say_phrases));
            }
            else if (phase_3_cycle == 2)
                phase_3_cycle = 0; //counter
            phase_3_cycle++;
            await do_teleport(owner, get_turf(owner), 2, no_effects: TRUE); //Teleports player randomly
            do_sparks(5, FALSE, owner);
        }
        //phase 4
        else if (current_cycle >= EIGENSTASIUM_PHASE_3_END)
        {
            //clean up and remove status
            SSblackbox?.record_feedback("tally", "chemical_reaction", 1, "Eigenstasium wild rides ridden");
            do_sparks(5, FALSE, owner);
            await do_teleport(owner, get_turf(owner), 2, no_effects: TRUE); //teleports clone so it's hard to find the real one!
            do_sparks(5, FALSE, owner);
            await owner.Sleeping(100);
            await owner.set_jitter_if_lower(100 * SECONDS);
            to_chat(owner, span_userdanger(I18n.T("You feel your eigenstate settle, as \"you\" become an alternative version of yourself!")));
            await owner.emote("me", 1, I18n.T("flashes into reality suddenly, gasping as they gaze around in a bewildered and highly confused fashion!"), TRUE);
            owner.log_message("has become an alternative universe version of themselves via EIGENSTASIUM.", LOG_GAME);
            //new you new stuff
            Dm.Deferred("code/datums/status_effects/neutral.dm:565", "eigenstasium wild ride: SSquirks.randomise_quirks(owner) (the quirks, D333)");
            await (owner.reagents as datum_reagents).remove_all(1000);
            owner.mob_mood.remove_temp_moods(); //New you, new moods. (gaps MS1 and RG3 met in gaps train 11)
            var human_mob = owner as mob_living_carbon_human;
            await owner.add_mood_event("Eigentrip", typeof(datum_mood_event_eigentrip));
            if (QDELETED(human_mob))
                return dot;
            if (prob(1)) //low chance of the alternative reality returning to monkey
            {
                var monkey_tail = Dm.@new<obj_item_organ_tail_monkey>();
                await monkey_tail.Insert(human_mob, movement_flags: DELETE_IF_REPLACED);
            }
            var human_species = human_mob.dna?.species as datum_species;
            if (human_species != null)
            {
                await human_species.randomize_active_features(human_mob);
                await human_species.randomize_active_underwear(human_mob);
            }

            owner.remove_status_effect(typeof(datum_status_effect_eigenstasium));
        }
        return dot;
    }

    // tg: code/datums/status_effects/neutral.dm:582
    [SignalHandler]
    public virtual void remove_clone_from_var(params object[] __extra)
    {
        UnregisterSignal(alt_clone, COMSIG_QDELETING);
    }

    // tg: code/datums/status_effects/neutral.dm:586
    public override async DmTask<object> on_remove()
    {
        if (!QDELETED(alt_clone)) //catch any stragilers
        {
            do_sparks(5, FALSE, alt_clone);
            owner.visible_message(I18n.T("One of the []s suddenly phases out of reality in front of you!", owner));
            qdel(alt_clone);
            alt_clone = null;
        }
        return await base.on_remove();
    }
}
