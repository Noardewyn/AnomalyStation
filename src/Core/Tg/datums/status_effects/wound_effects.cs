// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/wound_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2: the limp of a wounded leg, the wounds' own status effects and their slower clicks, and the
// determined effect of the determination reagent, which only that reagent's metabolism gives: M8). The var blocks and the
// alerts are generated (D111).

// tg: code/datums/status_effects/wound_effects.dm:9
public partial class datum_status_effect_determined : datum_status_effect
{
    // tg: code/datums/status_effects/wound_effects.dm:14
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = null;
        dot = await base.on_apply(__extra);
        owner.visible_message(span_danger(I18n.T("[]'s body tenses up noticeably, gritting against [] pain!", owner, owner.p_their())), span_notice(I18n.T("<b>Your senses sharpen as your body tenses up from the wounds you've sustained!</b>")),
            vision_distance: COMBAT_MESSAGE_RANGE);
        // MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, WOUND_DETERMINATION_BLEED_MOD) (__DEFINES/living.dm:53, a statement macro
        // that assigns the target's var: inline, rules 11.1 (a))
        LAZYINITLIST(ref owner.physiology);
        if (owner.physiology[PHYS_COEFF_BLEED] == null)
            owner.physiology[PHYS_COEFF_BLEED] = DEFAULT_PHYSIOLOGY_VAL;
        owner.physiology[PHYS_COEFF_BLEED] = Dm.mul(owner.physiology[PHYS_COEFF_BLEED], WOUND_DETERMINATION_BLEED_MOD);
        if (round(Dm.num(owner.physiology[PHYS_COEFF_BLEED]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref owner.physiology, PHYS_COEFF_BLEED);
        RegisterSignal(owner, COMSIG_LIVING_UNARMED_ATTACK, nameof(unarmed_strike));
        return dot;
    }

    // tg: code/datums/status_effects/wound_effects.dm:21
    public override async DmTask<object> on_remove()
    {
        owner.visible_message(span_danger(I18n.T("[]'s body slackens noticeably!", owner)), span_warning(I18n.T("<b>Your adrenaline rush dies off, and the pain from your wounds come aching back in...</b>")), vision_distance: COMBAT_MESSAGE_RANGE);
        // MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 1/WOUND_DETERMINATION_BLEED_MOD) (inline, as above)
        LAZYINITLIST(ref owner.physiology);
        if (owner.physiology[PHYS_COEFF_BLEED] == null)
            owner.physiology[PHYS_COEFF_BLEED] = DEFAULT_PHYSIOLOGY_VAL;
        owner.physiology[PHYS_COEFF_BLEED] = Dm.mul(owner.physiology[PHYS_COEFF_BLEED], 1 / WOUND_DETERMINATION_BLEED_MOD);
        if (round(Dm.num(owner.physiology[PHYS_COEFF_BLEED]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref owner.physiology, PHYS_COEFF_BLEED);
        UnregisterSignal(owner, COMSIG_LIVING_UNARMED_ATTACK);
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/wound_effects.dm:27
    [SignalHandler]
    public virtual object unarmed_strike(mob_living source = null, atom attack_target = null, object proximity = null, object modifiers = null)
    {
        if (!Dm.truthy(proximity) || !isliving(attack_target))
            return NONE;

        obj_item_bodypart arm = source.get_active_hand() as obj_item_bodypart;
        datum_wound_bruised arm_bruises = arm.get_wound_type(typeof(datum_wound_bruised)) as datum_wound_bruised;
        if (arm_bruises != null)
        {
            arm_bruises.bruise_ticks = Dm.initial(arm_bruises, (datum_wound_bruised p) => p.bruise_ticks);
            return null;
        }
        arm_bruises = Dm.@new<datum_wound_bruised>();

        string wound_source;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            wound_source = "defensive injuries from attempting to ward off";
        else
            wound_source = "offensive injuries from attempting to fight";

        if (ishuman(attack_target))
            wound_source += " a humanoid";
        else if (issilicon(attack_target))
            wound_source += " something mechanical";
        else
            wound_source += " an animal";
        Waiting.NoSleep(arm_bruises.apply_wound(arm, TRUE, wound_source: wound_source));
        return null;
    }
}

// tg: code/datums/status_effects/wound_effects.dm:55
public partial class datum_status_effect_limp : datum_status_effect
{
    // tg: code/datums/status_effects/wound_effects.dm:77
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;
        mob_living_carbon carbon_owner = (mob_living_carbon)owner;
        left = carbon_owner.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg_left;
        right = carbon_owner.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg_right;
        update_limp(this);
        RegisterSignal(carbon_owner, COMSIG_MOVABLE_MOVED, nameof(check_step));
        RegisterSignal(carbon_owner, COMSIG_CARBON_REMOVE_LIMB, nameof(on_limb_removed));
        RegisterSignals(carbon_owner, Dm.list(COMSIG_CARBON_GAIN_WOUND, COMSIG_CARBON_POST_LOSE_WOUND, COMSIG_CARBON_ATTACH_LIMB), nameof(update_limp));
        return TRUE;
    }

    // tg: code/datums/status_effects/wound_effects.dm:89
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_CARBON_GAIN_WOUND, COMSIG_CARBON_POST_LOSE_WOUND, COMSIG_CARBON_ATTACH_LIMB, COMSIG_CARBON_REMOVE_LIMB));
        left = null;
        right = null;
        return null;
    }

    // tg: code/datums/status_effects/wound_effects.dm:99
    [SignalHandler]
    public virtual void check_step(mob whocares = null, object OldLoc = null, object Dir = null, object forced = null)
    {
        if (owner.client == null || Dm.eq(owner.body_position, LYING_DOWN) || !Dm.truthy(owner.has_gravity()) || Dm.truthy(Dm.bitand(owner.movement_type, Dm.bitor(FLYING, FLOATING))) || Dm.truthy(forced) || owner.buckled != null)
            return;

        // less limping while we have determination still
        float determined_mod = Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_determined))) ? 0.5f : 1;

        obj_item_bodypart leg_about_to_limp;
        float limp_chance;
        float limp_slowdown;
        if (next_leg == left)
        {
            leg_about_to_limp = left;
            limp_chance = limp_chance_left;
            limp_slowdown = slowdown_left;
            next_leg = right;
        }
        else
        {
            leg_about_to_limp = right;
            limp_chance = limp_chance_right;
            limp_slowdown = slowdown_right;
            next_leg = left;
        }

        // SEND_SIGNAL(owner, COMSIG_CARBON_LIMPING, leg_about_to_limp) & COMPONENT_CANCEL_LIMP (rules 11.1 (e), a value)
        if (Dm.truthy(Dm.bitand((!Dm.truthy(owner._listen_lookup?[COMSIG_CARBON_LIMPING]) ? (object)NONE : owner._SendSignal(COMSIG_CARBON_LIMPING, Dm.list(owner, leg_about_to_limp))), COMPONENT_CANCEL_LIMP)))
            return;

        if (prob(limp_chance * determined_mod))
        {
            // DEVIATION(DV15): a capsule step does not set move_delay to world.time first (tg's client/Move does,
            // mob_movement.dm:107-112, before the move that sends this signal), so the limp's pause counts from now.
            owner.client.move_delay = max(owner.client.move_delay, world.time) + limp_slowdown * determined_mod;
        }
    }

    /// We need to make sure that we properly clear these refs if one of the owner's limbs gets deleted
    // tg: code/datums/status_effects/wound_effects.dm:129
    [SignalHandler]
    public virtual void on_limb_removed(datum source = null, obj_item_bodypart limb_lost = null, object special = null, object dismembered = null)
    {
        if (limb_lost == left)
            left = null;
        if (limb_lost == right)
            right = null;

        update_limp(); // calling this with no arg so we know it's coming from here and not a signal
    }

    // tg: code/datums/status_effects/wound_effects.dm:139
    [SignalHandler]
    public virtual void update_limp(datum source = null)
    {
        mob_living_carbon carbon_mob = (mob_living_carbon)owner;
        if (source != null) // if we don't have a source, that means we are calling it from on_limb_removed. In that case we do not want to reassign these to the about-to-be-removed limbs (which can cause hanging refs)
        {
            left = carbon_mob.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg_left;
            right = carbon_mob.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg_right;
        }

        if (left == null && right == null)
        {
            carbon_mob.remove_status_effect(this);
            return;
        }

        slowdown_left = 0;
        slowdown_right = 0;
        limp_chance_left = 0;
        limp_chance_right = 0;

        // technically you can have multiple wounds causing limps on the same limb, even if practically only bone wounds cause it in normal gameplay
        if (left != null)
        {
            foreach (object thing in Dm.each(left.wounds))
            {
                datum_wound wound = (datum_wound)thing;
                slowdown_left += Dm.num(wound.limp_slowdown);
                limp_chance_left = max(limp_chance_left, Dm.num(wound.limp_chance));
            }
        }

        if (right != null)
        {
            foreach (object thing in Dm.each(right.wounds))
            {
                datum_wound wound = (datum_wound)thing;
                slowdown_right += Dm.num(wound.limp_slowdown);
                limp_chance_right = max(limp_chance_right, Dm.num(wound.limp_chance));
            }
        }

        // this handles losing your leg with the limp and the other one being in good shape as well
        if (!Dm.truthy(slowdown_left) && !Dm.truthy(slowdown_right))
        {
            carbon_mob.remove_status_effect(this);
            return;
        }
    }
}

//Quirk variant of limping. Will always be applied as long as you have a leg to stand on.
// tg: code/datums/status_effects/wound_effects.dm:175
public partial class datum_status_effect_limp_quirk : datum_status_effect_limp
{
    // tg: code/datums/status_effects/wound_effects.dm:179
    [SignalHandler]
    public override void update_limp(datum source = null)
    {
        mob_living_carbon carbon_mob = (mob_living_carbon)owner;
        left = carbon_mob.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg_left;
        right = carbon_mob.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg_right;

        slowdown_left = 0;
        slowdown_right = 0;
        limp_chance_left = 0;
        limp_chance_right = 0;


        if (left != null)
        {
            slowdown_left = 7; //Same as compound fracture
            limp_chance_left = 70;
        }

        else if (right != null)
        {
            slowdown_right = 7;
            limp_chance_right = 70;
        }
    }
}


/////////////////////////
//////// WOUNDS /////////
/////////////////////////

// wound status effect base
// tg: code/datums/status_effects/wound_effects.dm:204
public partial class datum_status_effect_wound : datum_status_effect
{
    // tg: code/datums/status_effects/wound_effects.dm:211
    [DmParams("new_owner", "incoming_wound")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object incoming_wound = Dm.arg(__extra, 0);
        linked_wound = incoming_wound as datum_wound;
        linked_limb = linked_wound.limb;
        return await base.on_creation(new_owner, Dm.forward(__extra, incoming_wound));
    }

    // tg: code/datums/status_effects/wound_effects.dm:216
    public override async DmTask<object> on_remove()
    {
        linked_wound = null;
        linked_limb = null;
        UnregisterSignal(owner, COMSIG_CARBON_LOSE_WOUND);
        return null;
    }

    // tg: code/datums/status_effects/wound_effects.dm:221
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;
        RegisterSignal(owner, COMSIG_CARBON_LOSE_WOUND, nameof(check_remove));
        return TRUE;
    }

    /// check if the wound getting removed is the wound we're tied to
    // tg: code/datums/status_effects/wound_effects.dm:228
    [SignalHandler]
    public virtual void check_remove(mob_living L = null, datum_wound W = null)
    {
        if (W == linked_wound)
            qdel(this);
    }

    // tg: code/datums/status_effects/wound_effects.dm:234
    public override object nextmove_modifier()
    {
        mob_living_carbon C = (mob_living_carbon)owner;

        if (C.get_active_hand() == linked_limb)
            return linked_wound.get_action_delay_mult();

        return base.nextmove_modifier();
    }

    // tg: code/datums/status_effects/wound_effects.dm:242
    public override object nextmove_adjust()
    {
        mob_living_carbon C = (mob_living_carbon)owner;

        if (C.get_active_hand() == linked_limb)
            return linked_wound.get_action_delay_increment();

        return base.nextmove_adjust();
    }
}

// tg: :251-282  the bone, slash, pierce and burn subtypes only set their id (generated, D111).
