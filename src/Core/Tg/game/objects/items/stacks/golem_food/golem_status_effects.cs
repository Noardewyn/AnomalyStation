// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/golem_food/golem_status_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the metamorphic buffs a golem (or anyone with a rock brain) gets from the minerals it eats, with
// the alert that counts them down and the overlay the stone limbs wear. The var blocks are generated.

/// Abstract holder for golem status effects, you should never have more than one of these active
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:3
public partial class datum_status_effect_golem : datum_status_effect
{
    ///If we find 3 or more golem parts, then we can say that the status effect overlays are noticeable enough, so we won't need the outline filter.
    private const int MAX_GOLEM_PARTS_FOUND_FOR_FILTER = 2; // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:73

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:77
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (Dm.truthy(owner.has_status_effect(type))) //the initial id of the type will be checked. Make sure to override it if not exclusive
            return FALSE;
        if (Dm.truthy(applied_fluff))
            to_chat(owner, span_notice(applied_fluff));

        if (Dm.truthy(overlay_state_prefix))
        {
            int golem_parts_found = 0;
            foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(owner.get_bodyparts()))
            {
                // these overlays won't look good on anything but golem limbs
                if (!Dm.eq(part.limb_id, SPECIES_GOLEM))
                    continue;
                datum_bodypart_overlay_simple_golem_overlay overlay = Dm.@new<datum_bodypart_overlay_simple_golem_overlay>();
                await overlay.add_to_bodypart(overlay_state_prefix, part);
                Dm.append(ref active_overlays, overlay);
                golem_parts_found++;
            }
            if (golem_parts_found > 0)
            {
                mob_living_carbon golem_owner = owner as mob_living_carbon;
                await golem_owner.update_body_parts();
                if (golem_parts_found > MAX_GOLEM_PARTS_FOUND_FOR_FILTER)
                    return TRUE;
            }
        }

        if (Dm.truthy(filter_color))
        {
            owner.add_filter(Dm.text("[]_filter", id), 2, outline_filter(size: 1.25f, color: filter_color) as DmList);
            object the_filter = owner.get_filter(Dm.text("[]_filter", id));
            animate(the_filter, Dm.named("alpha", 0)); // start at 0 alpha
            animate(the_filter, Dm.named("alpha", 150), Dm.named("time", 7.5f * SECONDS), Dm.named("loop", -1), Dm.named("easing", SINE_EASING)); // fade in and out
            animate(Dm.named("alpha", 50), Dm.named("time", 7.5f * SECONDS), Dm.named("loop", -1), Dm.named("easing", SINE_EASING));
        }

        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:111
    [DmParams("new_owner", "multiplier")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        float multiplier = Dm.num(Dm.arg(__extra, 0) ?? 1f);
        ///instead of straight out multiplying the duration, we use exponents to flatten the duration so it doesn't become exceedingly long for golems
        float exponent = 0.5f;
        float duration_mult = minimum_duration_mult;
        if (iscarbon(new_owner))
        {
            //increase the duration for each stone bodypart, while reducing the benefit of higher multipliers in relation to the inscreased duration
            duration_mult = 0.1f;
            foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(new_owner.get_bodyparts()))
            {
                if (Dm.truthy(Dm.bitand(part.biological_state, BIO_STONE)))
                {
                    duration_mult += duration_mult_per_bodypart; // should be 1 at 6 stone-y bodyparts
                    exponent = max(0.2f, exponent - 0.05f); // 0.2 at 6 bodyparts
                }
            }
        }
        else if (Dm.truthy(Dm.bitand(new_owner.mob_biotypes, MOB_MINERAL)))
        {
            exponent = 0.2f;
            duration_mult = 1;
        }
        if (multiplier > 1)
            duration = Dm.num(duration) * MathF.Pow(duration_mult * multiplier, exponent);
        else // if the multiplier is lower than 1, don't bother using powers.
            duration = Dm.num(duration) * duration_mult * multiplier;
        float buff_duration = Dm.num(duration);
        object dot = await base.on_creation(new_owner, __extra);
        if (!Dm.truthy(dot))
            return dot;
        atom_movable_screen_alert_status_effect_golem_status status_alert = linked_alert as atom_movable_screen_alert_status_effect_golem_status;
        status_alert?.update_details(buff_duration);
        RegisterSignal(new_owner, SIGNAL_REMOVETRAIT(TRAIT_ROCK_METAMORPHIC), nameof(on_metamorphic_lost));
        return dot;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:137
    [SignalHandler]
    public virtual void on_metamorphic_lost(params object[] __extra)
    {
        qdel(this);
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:141
    public override async DmTask<object> on_remove()
    {
        if (Dm.truthy(mineral_name))
            to_chat(owner, span_warning(I18n.T("The effect of the [] fades.", mineral_name)));
        QDEL_LIST(active_overlays);
        owner.remove_filter(Dm.text("[]_filter", id));
        return await base.on_remove();
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:148
    public override object get_examine_text(mob examiner = null)
    {
        if (Dm.truthy(mineral_name))
            return span_notice(I18n.T("[] body has been augmented with veins of [].", owner.p_Their(), mineral_name));
        return null;
    }
}

// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:31
public partial class atom_movable_screen_alert_status_effect_golem_status : atom_movable_screen_alert_status_effect
{
    /// Set up how the alert ACTUALLY looks, based on the effect applied
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:42
    public virtual void update_details(float buff_time = 0)
    {
        datum_status_effect_golem golem_effect = attached_effect as datum_status_effect_golem;
        if (golem_effect == null)
            throw Dm.CRASH("Golem status alert attached to invalid status effect.", "code/game/objects/items/stacks/golem_food/golem_status_effects.dm", 44);
        name = name.Replace("%SOMETHING%", golem_effect.mineral_name);
        desc = golem_effect.alert_desc;
        mineral_overlay = mutable_appearance(golem_effect.alert_icon, golem_effect.alert_icon_state) as mutable_appearance;
        update_appearance(UPDATE_ICON);

        if (buff_time > early_expiry_warning)
            _addtimer(Dm.@new<datum_callback>(this, nameof(early_warning)), buff_time - early_expiry_warning, TIMER_DELETE_ME, file: "code/game/objects/items/stacks/golem_food/golem_status_effects.dm", line: 52);
        if (buff_time > imminent_expiry_warning)
            _addtimer(Dm.@new<datum_callback>(this, nameof(imminent_warning)), buff_time - imminent_expiry_warning, TIMER_DELETE_ME, file: "code/game/objects/items/stacks/golem_food/golem_status_effects.dm", line: 54);
    }

    /// Animate to indicate effect is expiring soon
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:57
    public virtual void early_warning()
    {
        animate(this, Dm.named("alpha", 75), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1));
        animate(Dm.named("alpha", 255), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1));
    }

    /// Animate to indicate effect is expiring very soon
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:62
    public virtual void imminent_warning()
    {
        animate(this, Dm.named("alpha", 25), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1));
        animate(Dm.named("alpha", 200), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1));
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:66
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(dot as DmList, mineral_overlay);
        return dot;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:70
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref mineral_overlay);
        return base.Destroy(force);
    }
}

/// Body part overlays applied by golem status effects
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:154
public partial class datum_bodypart_overlay_simple_golem_overlay : datum_bodypart_overlay_simple
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:164
    public virtual async DmTask add_to_bodypart(object prefix = null, obj_item_bodypart part = null)
    {
        icon_state = Dm.text("[]_[]", prefix, part.body_zone);
        attached_bodypart = WEAKREF(part) as datum_weakref;
        await part.add_bodypart_overlay(this, update: FALSE);
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:169
    public override object Destroy(float? force = null)
    {
        obj_item_bodypart referenced_bodypart = attached_bodypart?.resolve() as obj_item_bodypart;
        if (referenced_bodypart != null)
            Waiting.NoSleep(referenced_bodypart.remove_bodypart_overlay(this));
        return base.Destroy(force);
    }
}

/// Freezes hunger for the duration
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:176
public partial class datum_status_effect_golem_uranium : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:183
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        ADD_TRAIT(owner, TRAIT_NOHUNGER, TRAIT_STATUS_EFFECT(id));
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_golem_hunger));
        owner.remove_status_effect(typeof(datum_status_effect_golem_statued)); // Instant fix!
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:192
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_NOHUNGER, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

/// Magic immunity
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:198
public partial class datum_status_effect_golem_silver : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:205
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.add_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_HOLY), TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:212
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_ANTIMAGIC, TRAIT_HOLY), TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

/// Heat immunity, turns heat damage into local power
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:223
public partial class datum_status_effect_golem_plasma : datum_status_effect_golem
{
    /// What do we multiply our damage by to convert it into power?
    private const float ENERGY_PER_DAMAGE = 0.005f * STANDARD_CELL_CHARGE; // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:218
    /// Multiplier to apply to burn damage, not 0 so that we can reverse it more easily
    private const float BURN_MULTIPLIER = 0.05f; // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:220

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:230
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.add_traits(Dm.list(TRAIT_RESISTHIGHPRESSURE, TRAIT_RESISTHEAT, TRAIT_ASHSTORM_IMMUNE), TRAIT_STATUS_EFFECT(id));
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE, nameof(on_burned));
        MODIFY_PHYSIOLOGY(owner, BURN, BURN_MULTIPLIER);
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:239
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_RESISTHIGHPRESSURE, TRAIT_RESISTHEAT, TRAIT_ASHSTORM_IMMUNE), TRAIT_STATUS_EFFECT(id));
        UnregisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE);
        MODIFY_PHYSIOLOGY(owner, BURN, 1 / BURN_MULTIPLIER);
        return await base.on_remove();
    }

    /// When we take fire damage (or... technically also cold damage, we don't differentiate), zap a nearby APC
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:246
    [SignalHandler]
    public virtual void on_burned(datum source = null, object damage = null, object damagetype = null, params object[] __extra)
    {
        if (!Dm.eq(damagetype, BURN))
            return;

        obj_machinery_power_energy_accumulator ground = get_closest_atom(typeof(obj_machinery_power_energy_accumulator), view(4, owner), owner) as obj_machinery_power_energy_accumulator;
        if (ground != null)
        {
            zap_effect(ground);
            Waiting.Detach(ground.zap_act(damage, ZAP_GENERATES_POWER)); // INVOKE_ASYNC: a signal handler cannot wait
            return;
        }
        area our_area = get_area(owner) as area;
        obj_machinery_power_apc our_apc = our_area.apc;
        if (our_apc == null)
            return;
        zap_effect(our_apc);
        if (our_apc.cell != null)
            Waiting.NoSleep(our_apc.cell.give(Dm.num(damage) * ENERGY_PER_DAMAGE));
    }

    /// Shoot a beam at the target atom
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:267
    public virtual void zap_effect(atom target = null)
    {
        owner.Beam(target, icon_state: Dm.text("lightning[]", rand(1, 12)), time: 0.5f * SECONDS);
        playsound(owner, Dm.resource("sound/effects/magic/lightningshock.ogg"), 50, TRUE);
    }
}

/// Makes you spaceproof
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:273
public partial class datum_status_effect_golem_plasteel : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:280
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.add_traits(Dm.list(TRAIT_RESISTLOWPRESSURE, TRAIT_RESISTCOLD), TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:287
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_RESISTLOWPRESSURE, TRAIT_RESISTCOLD), TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

/// Makes you reflect energy projectiles
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:293
public partial class datum_status_effect_golem_gold : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:300
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        owner.flags_ricochet = Dm.num(Dm.bitor(owner.flags_ricochet, RICOCHET_SHINY));
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:307
    public override async DmTask<object> on_remove()
    {
        owner.flags_ricochet = Dm.num(Dm.bitand(owner.flags_ricochet, Dm.bitnot(RICOCHET_SHINY)));
        return await base.on_remove();
    }
}

/// Makes you hard to see
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:313
public partial class datum_status_effect_golem_diamond : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:331
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        RegisterSignals(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOB_THROW, COMSIG_MOB_ATTACK_HAND, COMSIG_MOB_ITEM_ATTACK), nameof(on_reveal));
        owner.alpha = moving_alpha;
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_light_speed));

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        foreach (obj_item_bodypart_arm arm in Dm.each<obj_item_bodypart_arm>(carbon_owner.get_bodyparts()))
            set_arm_fluff(arm);
        return TRUE;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:344
    public override async DmTask<object> tick(object delta_time = null)
    {
        owner.alpha = max(Dm.num(owner.alpha) - alpha_per_tick, 0);
        return null;
    }

    /// Reset alpha to starting value
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:348
    [SignalHandler]
    public virtual void on_reveal(params object[] __extra)
    {
        owner.alpha = moving_alpha;
    }

    /// Make our arm do slashing effects
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:353
    public virtual void set_arm_fluff(obj_item_bodypart_arm arm = null)
    {
        initial_unarmed_verbs[arm] = arm.unarmed_attack_verbs;
        initial_unarmed_verbs_past[arm] = arm.unarmed_attack_verbs_continuous;
        arm.unarmed_attack_verbs = Dm.list("slash");
        arm.unarmed_attack_verbs_continuous = Dm.list("slashes");
        arm.grappled_attack_verb = "lacerate";
        arm.grappled_attack_verb_continuous = "lacerates";
        arm.unarmed_attack_effect = ATTACK_EFFECT_CLAW;
        arm.unarmed_attack_sound = Dm.resource("sound/items/weapons/slash.ogg");
        arm.unarmed_miss_sound = Dm.resource("sound/items/weapons/slashmiss.ogg");
        RegisterSignal(arm, COMSIG_QDELETING, nameof(on_arm_destroyed));
        LAZYADD(ref modified_arms, arm);
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:366
    public override async DmTask<object> on_remove()
    {
        owner.alpha = Dm.initial(owner, (mob_living p) => p.alpha);
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_light_speed));
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOB_THROW, COMSIG_MOB_ATTACK_HAND, COMSIG_MOB_ITEM_ATTACK));
        foreach (obj_item_bodypart_arm arm in Dm.each_cast<obj_item_bodypart_arm>(modified_arms?.Copy()))
            reset_arm_fluff(arm);
        LAZYCLEARLIST(modified_arms);
        initial_unarmed_verbs.Cut();
        initial_unarmed_verbs_past.Cut();
        return await base.on_remove();
    }

    /// Make our arm do whatever it originally did
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:378
    public virtual void reset_arm_fluff(obj_item_bodypart_arm arm = null)
    {
        if (arm == null)
            return;
        arm.unarmed_attack_verbs = initial_unarmed_verbs[arm] as DmList;
        arm.unarmed_attack_verbs_continuous = initial_unarmed_verbs_past[arm] as DmList;
        arm.grappled_attack_verb = Dm.initial(arm, (obj_item_bodypart p) => p.grappled_attack_verb);
        arm.grappled_attack_verb_continuous = Dm.initial(arm, (obj_item_bodypart p) => p.grappled_attack_verb_continuous);
        arm.unarmed_attack_effect = Dm.initial(arm, (obj_item_bodypart p) => p.unarmed_attack_effect);
        arm.unarmed_attack_sound = Dm.initial(arm, (obj_item_bodypart p) => p.unarmed_attack_sound);
        arm.unarmed_miss_sound = Dm.initial(arm, (obj_item_bodypart p) => p.unarmed_miss_sound);
        UnregisterSignal(arm, COMSIG_QDELETING);
    }

    /// Remove references to deleted arms
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:391
    [SignalHandler]
    public virtual void on_arm_destroyed(obj_item_bodypart_arm arm = null, params object[] __extra)
    {
        Dm.remove(ref modified_arms, arm);
        initial_unarmed_verbs.Remove(arm);
        initial_unarmed_verbs_past.Remove(arm);
    }
}

/// Makes you tougher
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:399
public partial class datum_status_effect_golem_titanium : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:414
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return FALSE;
        RegisterSignal(owner, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_punched));
        MODIFY_PHYSIOLOGY(owner, BRUTE, brute_modifier);
        foreach (obj_item_bodypart_arm arm in Dm.each<obj_item_bodypart_arm>(owner.get_bodyparts()))
            buff_arm(arm);
        return dot;
    }

    /// Give mining mobs an extra slap
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:424
    [SignalHandler]
    public virtual object on_punched(mob_living puncher = null, atom punchee = null, object proximity = null, params object[] __extra)
    {
        if (!Dm.truthy(proximity) || !isliving(punchee))
            return NONE;
        mob_living victim = punchee as mob_living;
        if (victim.body_position == LYING_DOWN || !Dm.truthy(victim.has_faction(Dm.list(FACTION_MINING, FACTION_BOSS))))
            return NONE;
        Waiting.NoSleep(victim.apply_damage(mining_bonus, BRUTE));
        return null;
    }

    /// Make the targeted arm big and strong
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:434
    public virtual void buff_arm(obj_item_bodypart_arm arm = null)
    {
        arm.unarmed_damage_low += damage_increase;
        arm.unarmed_damage_high += damage_increase;
        RegisterSignal(arm, COMSIG_QDELETING, nameof(on_arm_destroyed));
        LAZYADD(ref modified_arms, arm);
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:440
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_UNARMED_ATTACK);
        MODIFY_PHYSIOLOGY(owner, BRUTE, 1 / brute_modifier);
        foreach (obj_item_bodypart_arm arm in Dm.each_cast<obj_item_bodypart_arm>(modified_arms?.Copy()))
            debuff_arm(arm);
        LAZYCLEARLIST(modified_arms);
        return await base.on_remove();
    }

    /// Make the targeted arm small and weak
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:449
    public virtual void debuff_arm(obj_item_bodypart_arm arm = null)
    {
        if (arm == null)
            return;
        arm.unarmed_damage_low -= damage_increase;
        arm.unarmed_damage_high -= damage_increase;
        UnregisterSignal(arm, COMSIG_QDELETING);
    }

    /// Remove references to deleted arms
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:457
    [SignalHandler]
    public virtual void on_arm_destroyed(obj_item_bodypart_arm arm = null, params object[] __extra)
    {
        Dm.remove(ref modified_arms, arm);
    }
}

/// Makes you slippery
// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:463
public partial class datum_status_effect_golem_bananium : datum_status_effect_golem
{
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:472
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        await owner.AddElementTrait(TRAIT_WADDLING, TRAIT_STATUS_EFFECT(id), typeof(datum_element_waddling));
        ADD_TRAIT(owner, TRAIT_NO_SLIP_WATER, TRAIT_STATUS_EFFECT(id));
        slipperiness = await owner.AddComponent(typeof(datum_component_slippery),
            Dm.named("knockdown", 12 * SECONDS),
            Dm.named("lube_flags", NO_SLIP_WHEN_WALKING),
            Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(try_slip)))) as datum_component_slippery;
        return dot;
    }

    /// Only slip people when we're down on the ground
    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:486
    public virtual object try_slip(mob_living slipper = null, mob_living slippee = null)
    {
        return owner.body_position == LYING_DOWN;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:489
    public override async DmTask<object> on_remove()
    {
        owner.remove_traits(Dm.list(TRAIT_WADDLING, TRAIT_NO_SLIP_WATER), TRAIT_STATUS_EFFECT(id));
        QDEL_NULL(ref slipperiness);
        return await base.on_remove();
    }
}

// tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:497
public partial class datum_status_effect_golem_lightbulb : datum_status_effect_golem
{
    private const string LIGHTBULB_FILTER = "filter_lightbulb_glow"; // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:495

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:511
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot))
            return dot;
        to_chat(owner, span_notice(I18n.T("You start to emit a healthy glow.")));
        lightbulb = owner.mob_light(glow_range, glow_power, glow_color) as obj_effect_dummy_lighting_obj_moblight;
        owner.add_filter(LIGHTBULB_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", glow_color), Dm.kv("alpha", 60), Dm.kv("size", 1)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/golem_food/golem_status_effects.dm:519
    public override async DmTask<object> on_remove()
    {
        QDEL_NULL(ref lightbulb);
        owner.remove_filter(LIGHTBULB_FILTER);
        to_chat(owner, span_warning(I18n.T("Your glow fades.")));
        return await base.on_remove();
    }
}
