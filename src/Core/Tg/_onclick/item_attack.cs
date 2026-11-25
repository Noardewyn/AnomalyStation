// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/item_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (docs/ai/specs/m4a-ownership.md, P1). Calls into later milestones' mechanics are rule-1.4 blocks with
// their milestone: the tutorials (SStutorials, M9), achievements (M10), integrity damage (take_damage, destruction M5),
// wounds (M6), speech (force_say, M9), the antagonists (M10). Fingerprints and the attack's blood are live (M6 slice 1).
// The interaction guard of D132 (InertSeam.MissingOverride, docs/ai/specs/interaction.md 7) is at the top of the item's base
// definitions and of the target's attackby; each returns the value that stops the chain (DV27).

// tg: code/_onclick/item_attack.dm:24
public partial class obj_item : obj
{
    /**
     * THIS IS THE ROOT OF ALL EVIL (I mean the root of all item-on-atom interactions)
     *
     * Implementation details
     *
     * [obj/item/proc/base_item_interaction] handles all non-combat interactions of an item with an atom.
     * This calls [atom/proc/tool_act], among others.
     *
     * [obj/item/proc/pre_attack] is called before an attack is made, before we even check for block.
     * Use pre attack to cancel attacks early based on conditions ("You cant attack with this unless you're a pacifist!")
     * After pre-attack we go into [atom/proc/attackby] which is where individual atoms handle what happens when being hit
     * Objects use [obj/item/proc/attack_atom], mobs use [obj/item/proc/attack].
     * You GENERALLY should never override attack, like ever. (On-attack effects go in afterattack)
     * Regardless, we go BACK to the atom to call [atom/proc/attacked_by], which is where the damage is actually applied.
     * Finally we call [obj/item/proc/afterattack] after damage is dealt, to handle special item interacts after an attack occurs.
     * Use afterattack for stuff like "My stick stuns when it hits people".
     *
     * Arguments
     * * mob/living/user - The mob using src on something
     * * atom/target - The atom being clicked on with src
     * * list/modifiers - click params such as alt/shift and x/y, etc
     * * list/attack_modifiers - attack modifiers such as force, damage type, etc. Only used in attacks
     */
    // tg: code/_onclick/item_attack.dm:24
    public virtual async DmTask<object> melee_attack_chain(mob user = null, atom target = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        attack_modifiers ??= Dm.list();
        // DEVIATION(DV27): the interaction guard (D132) before tg's first line.
        if (InertSeam.MissingOverride(this, "melee_attack_chain", user))
            return null;
        //Proxy replaces src cause it returns an atom that will attack the target on our behalf
        // GAP(P2): get_proxy_attacker_for (items.dm:2148, tg's body is `return src`) is still the contracts' skeleton in
        // items.cs; P2 translates it (m4a-ownership.md: not in P2's list yet, reported to the lead).
        obj_item source_atom = get_proxy_attacker_for(target, user) as obj_item;
        if (source_atom != this) //if we are someone else then call that attack chain else we can proceed with the usual stuff
            return await source_atom.melee_attack_chain(user, target, modifiers, attack_modifiers);

        object is_right_clicking = LAZYACCESS(modifiers, RIGHT_CLICK);

        object item_interact_result = await target.base_item_interaction(user as mob_living, this, modifiers);
        if (Dm.truthy(Dm.bitand(item_interact_result, ITEM_INTERACT_SUCCESS)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(item_interact_result, ITEM_INTERACT_BLOCKING)))
            return FALSE;

        // At this point it means we're not doing a non-combat interaction so let's just try to bash it

        object pre_attack_result = null;
        if (Dm.truthy(is_right_clicking))
        {
            object sw__1 = await pre_attack_secondary(target, user as mob_living, modifiers, attack_modifiers);
            if (Dm.eq(sw__1, SECONDARY_ATTACK_CALL_NORMAL))
                pre_attack_result = await pre_attack(target, user as mob_living, modifiers, attack_modifiers);
            else if (Dm.eq(sw__1, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
                return TRUE;
            else if (Dm.eq(sw__1, SECONDARY_ATTACK_CONTINUE_CHAIN))
            {
                ; // EMPTY_BLOCK_GUARD (__DEFINES/_helpers.dm:51) // Normal behavior
            }
            else
                throw Dm.CRASH("pre_attack_secondary must return an SECONDARY_ATTACK_* define, please consult code/__DEFINES/combat.dm", "code/_onclick/item_attack.dm", 47);
        }
        else
            pre_attack_result = await pre_attack(target, user as mob_living, modifiers, attack_modifiers);

        if (Dm.truthy(pre_attack_result))
            return TRUE;

        // At this point the attack is really about to happen

        object attackby_result = null;
        if (Dm.truthy(is_right_clicking))
        {
            object sw__2 = await target.attackby_secondary(this, user, modifiers, attack_modifiers);
            if (Dm.eq(sw__2, SECONDARY_ATTACK_CALL_NORMAL))
                attackby_result = await target.attackby(this, user, modifiers, attack_modifiers);
            else if (Dm.eq(sw__2, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
                return TRUE;
            else if (Dm.eq(sw__2, SECONDARY_ATTACK_CONTINUE_CHAIN))
            {
                ; // EMPTY_BLOCK_GUARD (__DEFINES/_helpers.dm:51) // Normal behavior
            }
            else
                throw Dm.CRASH("attackby_secondary must return an SECONDARY_ATTACK_* define, please consult code/__DEFINES/combat.dm", "code/_onclick/item_attack.dm", 65);
        }
        else
            attackby_result = await target.attackby(this, user, modifiers, attack_modifiers);

        if (Dm.truthy(attackby_result))
        {
            // This means the attack failed or was handled for whatever reason
            return TRUE;
        }

        // At this point it means the attack was "successful", or at least unhandled, in some way
        // This can mean nothing happened, this can mean the target took damage, etc.

        if (Dm.truthy(user.client) && isitem(target))
        {
            Dm.Deferred("code/_onclick/item_attack.dm:79", "melee_attack_chain: SStutorials.suggest_tutorial (the switch_hands and drop tutorials; SStutorials is not declared; tutorials, M10+)");
        }

        return TRUE;
    }

    /// Called when the item is in the active hand, and clicked; alternately, there is an 'activate held object' verb or you can hit pagedown.
    // tg: code/_onclick/item_attack.dm:89
    public virtual async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132); TRUE stops the chain as tg's COMPONENT_CANCEL_ATTACK_CHAIN does.
        if (InertSeam.MissingOverride(this, "attack_self", user))
            return TRUE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK_SELF]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_ATTACK_SELF, Dm.list(this, user))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        await interact(user);
        return null;
    }

    /// Called when the item is in the active hand, and right-clicked. Intended for alternate or opposite functions, such as lowering reagent transfer amount. At the moment, there is no verb or hotkey.
    // tg: code/_onclick/item_attack.dm:95
    public virtual async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "attack_self_secondary", user))
            return TRUE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK_SELF_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_ATTACK_SELF_SECONDARY, Dm.list(this, user))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        return null;
    }

    /**
     * Called on the item before it hits something
     *
     * Arguments:
     * * atom/target - The atom about to be hit
     * * mob/living/user - The mob doing the htting
     * * list/modifiers - click params such as alt/shift etc
     * * attack_modifiers - attack modifiers such as force, damage type, etc

     * See: [/obj/item/proc/melee_attack_chain]
     */
    // tg: code/_onclick/item_attack.dm:110
    public virtual async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null) //do stuff before attackby!
    {
        // DEVIATION(DV27): the interaction guard (D132); TRUE cancels the attack.
        if (InertSeam.MissingOverride(this, "pre_attack", user))
            return TRUE;
        object signal_result = Dm.bitor((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_PRE_ATTACK]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_PRE_ATTACK, Dm.list(this, target, user, modifiers, attack_modifiers))), (!Dm.truthy(user._listen_lookup?[COMSIG_USER_PRE_ITEM_ATTACK]) ? (object)NONE : user._SendSignal(COMSIG_USER_PRE_ITEM_ATTACK, Dm.list(user, this, target, modifiers, attack_modifiers))));
        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        return FALSE; //return TRUE to avoid calling attackby after this proc does stuff
    }

    /**
     * Called on the item before it hits something, when right clicking.
     *
     * Arguments:
     * * atom/target - The atom about to be hit
     * * mob/living/user - The mob doing the htting
     * * list/modifiers - click params such as alt/shift etc
     * * list/attack_modifiers - attack modifiers such as force, damage type, etc
     *
     * See: [/obj/item/proc/melee_attack_chain]
     */
    // tg: code/_onclick/item_attack.dm:128
    public virtual async DmTask<object> pre_attack_secondary(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "pre_attack_secondary", user))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object signal_result = Dm.bitor((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_PRE_ATTACK_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_PRE_ATTACK_SECONDARY, Dm.list(this, target, user, modifiers, attack_modifiers))), (!Dm.truthy(user._listen_lookup?[COMSIG_USER_PRE_ITEM_ATTACK_SECONDARY]) ? (object)NONE : user._SendSignal(COMSIG_USER_PRE_ITEM_ATTACK_SECONDARY, Dm.list(user, this, target, modifiers, attack_modifiers))));

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CONTINUE_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;

        return SECONDARY_ATTACK_CALL_NORMAL;
    }
}

// tg: code/_onclick/item_attack.dm:150
public partial class atom : datum
{
    /**
     * Called on an object being hit by an item
     *
     * Arguments:
     * * obj/item/attacking_item - The item hitting this atom
     * * mob/user - The wielder of this item
     * * list/modifiers - click params such as alt/shift etc
     * * list/attack_modifiers - attack modifiers such as force, damage type, etc
     *
     * See: [/obj/item/proc/melee_attack_chain]
     */
    // tg: code/_onclick/item_attack.dm:150
    public virtual async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the target; TRUE is tg's "handled, stop".
        if (InertSeam.MissingOverride(this, "attackby", user))
            return TRUE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACKBY]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACKBY, Dm.list(this, attacking_item, user, modifiers, attack_modifiers))), COMPONENT_NO_AFTERATTACK)))
            return TRUE;
        return FALSE;
    }

    /**
     * Called on an object being right-clicked on by an item
     *
     * Arguments:
     * * obj/item/weapon - The item hitting this atom
     * * mob/user - The wielder of this item
     * * list/modifiers - click params such as alt/shift etc
     * * list/attack_modifiers - attack modifiers such as force, damage type, etc
     *
     * See: [/obj/item/proc/melee_attack_chain]
     */
    // tg: code/_onclick/item_attack.dm:166
    public virtual async DmTask<object> attackby_secondary(obj_item weapon = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the target.
        if (InertSeam.MissingOverride(this, "attackby_secondary", user))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object signal_result = (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACKBY_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACKBY_SECONDARY, Dm.list(this, weapon, user, modifiers, attack_modifiers)));

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CONTINUE_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;

        return SECONDARY_ATTACK_CALL_NORMAL;
    }
}

// tg: code/_onclick/item_attack.dm:177
public partial class obj : atom_movable
{
    // tg: code/_onclick/item_attack.dm:177
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(await base.attackby(attacking_item, user, modifiers, attack_modifiers)))
            return TRUE;
        if (!Dm.truthy(Dm.bitand(obj_flags, CAN_BE_HIT)))
            return FALSE;
        return await attacking_item.attack_atom(this, user as mob_living, modifiers, attack_modifiers);
    }
}

// tg: code/_onclick/item_attack.dm:184
public partial class mob_living : mob
{
    // tg: code/_onclick/item_attack.dm:184
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (HAS_TRAIT(this, TRAIT_READY_TO_OPERATE))
        {
            object surgery_ret = await user.perform_surgery(this, tool, Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) ? TRUE : FALSE);
            if (Dm.truthy(surgery_ret))
                return surgery_ret;
        }

        if (this == user)
        {
            object manual_cauterization = await try_manual_cauterize(tool);
            if (Dm.truthy(Dm.bitand(manual_cauterization, ITEM_INTERACT_ANY_BLOCKER)))
                return manual_cauterization;
        }

        return NONE;
    }

    // tg: code/_onclick/item_attack.dm:199
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(await base.attackby(attacking_item, user, modifiers, attack_modifiers)))
            return TRUE;
        user.changeNext_move(attacking_item.attack_speed);
        return await attacking_item.attack__proc(this, user as mob_living, modifiers, attack_modifiers);
    }

    // tg: code/_onclick/item_attack.dm:205
    public override async DmTask<object> attackby_secondary(obj_item weapon = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object result = await weapon.attack_secondary(this, user as mob_living, modifiers, attack_modifiers);

        // Normal attackby updates click cooldown, so we have to make up for it
        if (!Dm.eq(result, SECONDARY_ATTACK_CALL_NORMAL))
        {
            if (Dm.truthy(weapon.secondary_attack_speed))
                user.changeNext_move(weapon.secondary_attack_speed);
            else
                user.changeNext_move(weapon.attack_speed);
        }

        return result;
    }
}

// tg: code/_onclick/item_attack.dm:227
public partial class obj_item : obj
{
    /**
     * Called from [/mob/living/proc/attackby]
     *
     * Arguments:
     * * mob/living/target_mob - The mob being hit by this item
     * * mob/living/user - The mob hitting with this item
     * * modifiers - click modifiers such as alt/shift or x/y
     * * attack_modifiers - attack modifiers such as force, damage type, etc
     */
    // tg: code/_onclick/item_attack.dm:227
    public virtual async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132); TRUE is tg's "handled, stop".
        if (InertSeam.MissingOverride(this, "attack", user))
            return TRUE;
        DmList atk_mods = attack_modifiers as DmList;
        object signal_return = ((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_ATTACK, Dm.list(this, target_mob, user, modifiers, attack_modifiers))) is var or__1 && Dm.truthy(or__1) ? or__1 : (!Dm.truthy(user._listen_lookup?[COMSIG_MOB_ITEM_ATTACK]) ? (object)NONE : user._SendSignal(COMSIG_MOB_ITEM_ATTACK, Dm.list(user, target_mob, user, modifiers, attack_modifiers))));
        if (Dm.truthy(Dm.bitand(signal_return, COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(signal_return, COMPONENT_SKIP_ATTACK)))
            return FALSE;

        if (Dm.truthy(Dm.bitand(item_flags, NOBLUDGEON)))
            return FALSE;

        float final_force = CALCULATE_FORCE(this, atk_mods);
        if (damtype != STAMINA && Dm.truthy(final_force) && HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You don't want to harm other living beings!")));
            return FALSE;
        }
        if (aimed_limb_missing(user, target_mob)) // DEVIATION(DV592)
            return TRUE;

        if (!Dm.truthy(LAZYACCESS(atk_mods, SILENCE_HITSOUND)))
        {
            if (!Dm.truthy(final_force) && !HAS_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND))
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), get_clamped_volume(), TRUE, -1); // tg: code/_onclick/item_attack.dm:244
            else if (Dm.truthy(hitsound))
                playsound(this, hitsound, get_clamped_volume(), TRUE, extrarange: Dm.truthy(stealthy_audio) ? SILENCED_SOUND_EXTRARANGE : -1, falloff_distance: 0); // tg: code/_onclick/item_attack.dm:246
        }

        target_mob.lastattacker = user.real_name;
        target_mob.lastattackerckey = user.ckey;

        if (Dm.truthy(final_force) && target_mob == user && Dm.truthy(user.client))
            Dm.Deferred("code/_onclick/item_attack.dm:252", "attack: user.client.give_award(/datum/award/achievement/misc/selfouch, user) (achievements, M10+)");

        if (get(this, typeof(mob_living)) == user) // telekinesis.
            await user.do_attack_animation(target_mob);
        if (Dm.eq(await target_mob.attacked_by(this, user, modifiers, atk_mods), ATTACK_FAILED))
            return TRUE;

        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_AFTERATTACK]))
            this._SendSignal(COMSIG_ITEM_AFTERATTACK, Dm.list(this, target_mob, user, modifiers, attack_modifiers));
        if (Dm.truthy(target_mob._listen_lookup?[COMSIG_ATOM_AFTER_ATTACKEDBY]))
            target_mob._SendSignal(COMSIG_ATOM_AFTER_ATTACKEDBY, Dm.list(target_mob, this, user, modifiers, attack_modifiers));
        await afterattack(target_mob, user, modifiers, atk_mods);
        if (final_force > 0)
            log_combat(user, target_mob, "attacked", this.name, Dm.text("(COMBAT MODE: []) (DAMTYPE: [])", uppertext(Dm.str(user.combat_mode)), uppertext(damtype)));
        // tg: code/_onclick/item_attack.dm:265
        add_fingerprint(user);
        return FALSE; // unhandled
    }

    /// The equivalent of [/obj/item/proc/attack] but for alternate attacks, AKA right clicking
    // tg: code/_onclick/item_attack.dm:269
    public virtual async DmTask<object> attack_secondary(mob_living victim = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132).
        if (InertSeam.MissingOverride(this, "attack_secondary", user))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object signal_result = (!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_ATTACK_SECONDARY, Dm.list(this, victim, user, modifiers, attack_modifiers)));

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;

        if (Dm.truthy(Dm.bitand(signal_result, COMPONENT_SECONDARY_CONTINUE_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;

        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    /// The equivalent of the standard version of [/obj/item/proc/attack] but for non mob targets.
    // tg: code/_onclick/item_attack.dm:281
    public virtual async DmTask<object> attack_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object signal_return = Dm.bitor((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK_ATOM]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_ATTACK_ATOM, Dm.list(this, attacked_atom, user, modifiers, attack_modifiers))), (!Dm.truthy(user._listen_lookup?[COMSIG_LIVING_ATTACK_ATOM]) ? (object)NONE : user._SendSignal(COMSIG_LIVING_ATTACK_ATOM, Dm.list(user, attacked_atom, modifiers, attack_modifiers))));
        if (Dm.truthy(Dm.bitand(signal_return, COMPONENT_SKIP_ATTACK)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(signal_return, COMPONENT_CANCEL_ATTACK_CHAIN)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(item_flags, NOBLUDGEON)))
            return FALSE;
        user.changeNext_move(attack_speed);
        if (get(this, typeof(mob_living)) == user) // telekinesis.
            await user.do_attack_animation(attacked_atom);
        if (Dm.eq(await attacked_atom.attacked_by(this, user, modifiers, attack_modifiers), ATTACK_FAILED))
            return TRUE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_AFTERATTACK]))
            this._SendSignal(COMSIG_ITEM_AFTERATTACK, Dm.list(this, attacked_atom, user, modifiers, attack_modifiers));
        if (Dm.truthy(attacked_atom._listen_lookup?[COMSIG_ATOM_AFTER_ATTACKEDBY]))
            attacked_atom._SendSignal(COMSIG_ATOM_AFTER_ATTACKEDBY, Dm.list(attacked_atom, this, user, modifiers, attack_modifiers));
        await afterattack(attacked_atom, user, modifiers, attack_modifiers);
        return FALSE; // unhandled
    }
}

// tg: code/_onclick/item_attack.dm:299
public partial class atom : datum
{
    /// Called from [/obj/item/proc/attack_atom] and [/obj/item/proc/attack] if the attack succeeds
    // tg: code/_onclick/item_attack.dm:299
    public virtual async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(uses_integrity))
        {
            stack_trace("attacked_by() was called on an object that doesn't use integrity!", "code/_onclick/item_attack.dm", 301);
            return ATTACK_FAILED;
        }

        float final_force = CALCULATE_FORCE(attacking_item, attack_modifiers);
        if (final_force <= 0)
            return 0;

        // DEVIATION(DV27): a hit on a type whose own attacked_by or damage procs are not translated is refused to the user with
        // "not ported yet" (D132); the attack chain stops there, as tg's ATTACK_FAILED stops it.
        if (InertSeam.MissingOverride(this, "attacked_by", user) || InertSeam.MissingDamageOverride(this, user))
            return ATTACK_FAILED;

        object damage = await take_damage(final_force, attacking_item.damtype, MELEE, 1, get_dir(this, user));
        //only witnesses close by and the victim see a hit message.
        user.visible_message(span_danger(I18n.T("[] hits [] with [][]", user, this, attacking_item, Dm.truthy(damage) ? "." : I18n.T(", without leaving a mark!"))),
            span_danger(I18n.T("You hit [] with [][]", this, attacking_item, Dm.truthy(damage) ? "." : I18n.T(", without leaving a mark!"))), null, COMBAT_MESSAGE_RANGE);
        log_combat(user, this, "attacked", attacking_item);
        return damage;
    }
}

// tg: code/_onclick/item_attack.dm:315
public partial class area : atom
{
    // tg: code/_onclick/item_attack.dm:315
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        stack_trace("areas are NOT supposed to have attacked_by() called on them!", "code/_onclick/item_attack.dm", 316);
        return ATTACK_FAILED;
    }
}

// tg: code/_onclick/item_attack.dm:319
public partial class mob_living : mob
{
    // tg: code/_onclick/item_attack.dm:319
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object targeting = check_zone(user.zone_selected);
        // DEVIATION(DV591): an aimed melee hit lands on the selected zone; tg's 80% zone roll (90% on a lying target) is not made
        if (user != this)
            targeting = get_random_valid_zone(targeting, 100);
        // GAP(lead): parse_zone_with_bodypart (__HELPERS/mobs.dm) is not declared; parse_zone (the same text for a zone
        // without a bodypart name override) stands for it.
        object targeting_human_readable = parse_zone(targeting);

        if (!Dm.truthy(LAZYACCESS(attack_modifiers, SILENCE_DEFAULT_MESSAGES)))
            send_item_attack_message(attacking_item, user, targeting_human_readable, targeting);

        object armor_block = Dm.min(run_armor_check(
                def_zone: targeting,
                attack_flag: MELEE,
                absorb_text: span_notice(I18n.T("Your armor has protected your []!", targeting_human_readable)),
                soften_text: span_warning(I18n.T("Your armor has softened a hit to your []!", targeting_human_readable)),
                armour_penetration: attacking_item.armour_penetration,
                weak_against_armour: attacking_item.weak_against_armour
            ), ARMOR_MAX_BLOCK);

        float final_force = CALCULATE_FORCE(attacking_item, attack_modifiers);
        if (Dm.truthy(Dm.bitand(mob_biotypes, Dm.bitor(Dm.bitor(MOB_ROBOTIC, MOB_MINERAL), MOB_SKELETAL)))) // this should probably check hit bodypart for humanoids
        {
            // GAP(lead): the slot types get_demolition_modifier's target as /obj (objs.cs) where tg passes this mob; the
            // mob's demolition traits are not read until the slot's type is widened (the item's demolition_mod is).
            final_force *= Dm.num(attacking_item.get_demolition_modifier(null));
        }

        float wounding = attacking_item.wound_bonus;
        if (Dm.truthy(Dm.bitand(attacking_item.item_flags, SURGICAL_TOOL)) && !Dm.truthy(user.combat_mode) && HAS_TRAIT(user, TRAIT_READY_TO_OPERATE))
            wounding = CANT_WOUND;

        if (user != this)
        {
            // This doesn't factor in armor, or most damage modifiers (physiology). Your mileage may vary
            if (Dm.truthy(await check_block(attacking_item, final_force, Dm.text("\\the []", attacking_item), MELEE_ATTACK, attacking_item.armour_penetration, attacking_item.damtype)))
            {
                LAZYSET(ref attack_modifiers, DAMAGE_DONE, 0);
                return ATTACK_FAILED;
            }
        }

        if (Dm.truthy(attacking_item._listen_lookup?[COMSIG_ITEM_ATTACK_ZONE]))
            attacking_item._SendSignal(COMSIG_ITEM_ATTACK_ZONE, Dm.list(attacking_item, this, user, targeting));

        if (final_force <= 0)
        {
            LAZYSET(ref attack_modifiers, DAMAGE_DONE, 0);
            return 0;
        }

        if (ishuman(this) || Dm.truthy(client)) // istype(src) is kinda bad, but it's to avoid spamming the blackbox
        {
            SSblackbox.record_feedback("nested tally", "item_used_for_combat", 1, Dm.list(Dm.text("[]", attacking_item.force), Dm.text("[]", attacking_item.type)));
            SSblackbox.record_feedback("tally", "zone_targeted", 1, user.zone_selected);
        }

        object damage_done = await apply_damage(
            damage: final_force,
            damagetype: attacking_item.damtype,
            def_zone: targeting,
            blocked: Dm.num_or_null(armor_block),
            wound_bonus: wounding,
            exposed_wound_bonus: attacking_item.exposed_wound_bonus,
            sharpness: Dm.num_or_null(attacking_item.get_sharpness()),
            attack_direction: get_dir(user, this),
            attacking_item: attacking_item
        );

        LAZYSET(ref attack_modifiers, DAMAGE_DONE, damage_done);
        await attack_effects(damage_done, targeting, armor_block, attacking_item, user);

        return damage_done;
    }

    /**
     * Called when we take damage, used to cause effects such as a blood splatter.
     *
     * Return TRUE if an effect was done, FALSE otherwise.
     */
    // tg: code/_onclick/item_attack.dm:387
    public virtual async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        if (Dm.le(damage_done, 0) || attacking_item.damtype != BRUTE || !prob(25 + Dm.num(damage_done) * 2))
            return FALSE;

        await attacking_item.add_mob_blood(this);
        await add_splatter_floor(get_turf(this));
        if (get_dist(attacker, this) > 1)
            return TRUE;

        if (!ishuman(attacker))
        {
            await attacker.add_mob_blood(this);
            return TRUE;
        }

        object bloodied_things = ITEM_SLOT_GLOVES;
        if (Dm.ge(damage_done, 20) || (Dm.ge(damage_done, 15) && prob(25)))
        {
            Dm.combine(ref bloodied_things, Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING));
            if (prob(33) && Dm.ge(damage_done, 10))
                Dm.combine(ref bloodied_things, ITEM_SLOT_FEET);
            if (prob(33) && Dm.ge(damage_done, 24)) // fireaxe damage, because heeeeere's johnny
                Dm.combine(ref bloodied_things, ITEM_SLOT_MASK);
            if (prob(33) && Dm.ge(damage_done, 30)) // esword damage
                Dm.combine(ref bloodied_things, ITEM_SLOT_HEAD);
        }

        mob_living_carbon_human human_attacker = attacker as mob_living_carbon_human;
        await human_attacker.add_blood_DNA_to_items(get_blood_dna_list() as DmList, bloodied_things);
        return TRUE;
    }
}

// tg: code/_onclick/item_attack.dm:417
public partial class mob_living_carbon : mob_living
{
    // tg: code/_onclick/item_attack.dm:417
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        obj_item_bodypart hit_bodypart = (Dm.truthy(get_bodypart(hit_zone as string)) ? get_bodypart(hit_zone as string) : get_bodypart()) as obj_item_bodypart;
        if (!Dm.truthy(hit_bodypart.can_bleed()))
            return FALSE;

        return await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
    }
}

// tg: code/_onclick/item_attack.dm:424
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/_onclick/item_attack.dm:424
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        object dot = null;
        dot = await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
        object sw__1 = hit_zone;
        if (Dm.eq(sw__1, BODY_ZONE_HEAD))
        {
            if (Dm.truthy(dot))
            {
                object bloodied_things = Dm.bitor(ITEM_SLOT_MASK, ITEM_SLOT_HEAD);
                if (prob(33))
                    Dm.combine(ref bloodied_things, ITEM_SLOT_EYES);
                await add_blood_DNA_to_items(get_blood_dna_list() as DmList, bloodied_things);
            }

            if (!Dm.truthy(attacking_item.get_sharpness()) && !HAS_TRAIT(this, TRAIT_HEAD_INJURY_BLOCKED) && attacking_item.damtype == BRUTE)
            {
                if (prob(Dm.num(damage_done)))
                {
                    await adjust_organ_loss(ORGAN_SLOT_BRAIN, 20);
                    if (!IS_UNCONSCIOUS_OR_CRIT(this))
                    {
                        visible_message(
                            span_danger(I18n.T("[] is knocked senseless!", this)),
                            span_userdanger(I18n.T("You're knocked senseless!"))
                        );
                        await set_confusion_if_lower(20 * SECONDS);
                        await adjust_eye_blur(20 * SECONDS);
                    }
                    if (prob(10))
                        await gain_trauma(typeof(datum_brain_trauma_mild_concussion));
                }
                else
                {
                    await adjust_organ_loss(ORGAN_SLOT_BRAIN, Dm.num(damage_done) * 0.2f);
                }

                // rev deconversion through blunt trauma.
                // this can be signalized to the rev datum
                if (Dm.truthy(mind) && !IS_UNCONSCIOUS_OR_CRIT(this) && this != attacker && prob(Dm.num(damage_done) + ((100 - health) * 0.5f)))
                {
                    var rev = mind.has_antag_datum(typeof(datum_antagonist_rev)) as datum_antagonist_rev; // gaps V1
                    if (rev != null)
                        await rev.remove_revolutionary(attacker);
                }
            }
        }
        else if (Dm.eq(sw__1, BODY_ZONE_CHEST))
        {
            if (Dm.truthy(dot))
            {
                await add_blood_DNA_to_items(get_blood_dna_list() as DmList, Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING));
            }

            if (!IS_UNCONSCIOUS_OR_CRIT(this) && !Dm.truthy(attacking_item.get_sharpness()) && !HAS_TRAIT(this, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED) && attacking_item.damtype == BRUTE)
            {
                if (prob(Dm.num(damage_done)))
                {
                    visible_message(
                        span_danger(I18n.T("[] is knocked down!", this)),
                        span_userdanger(I18n.T("You're knocked down!"))
                    );
                    await apply_effect(6 * SECONDS, EFFECT_KNOCKDOWN, Dm.num_or_null(armor_block));
                }
            }
        }

        // Triggers force say events
        if (Dm.gt(damage_done, 10) || (Dm.ge(damage_done, 5) && prob(33)))
            force_say();
        return dot;
    }
}

// tg: code/_onclick/item_attack.dm:479
public partial class obj_item : obj
{
    /**
     * Last proc in the [/obj/item/proc/melee_attack_chain].
     * Returns a bitfield containing AFTERATTACK_PROCESSED_ITEM if the user is likely intending to use this item on another item.
     * Some consumers currently return TRUE to mean "processed". These are not consistent and should be taken with a grain of salt.
     *
     * Arguments:
     * * atom/target - The thing that was hit
     * * mob/user - The mob doing the hitting
     * * proximity_flag - is 1 if this afterattack was called on something adjacent, in your square, or on your person.
     * * click_parameters - is the params string from byond [/atom/proc/Click] code, see that documentation.
     */
    // tg: code/_onclick/item_attack.dm:479
    public virtual async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132): an item whose own afterattack is not ported does nothing after the hit.
        if (InertSeam.MissingOverride(this, "afterattack", user))
            return;
        return;
    }

    // tg: code/_onclick/item_attack.dm:483
    public virtual object get_clamped_volume()
    {
        if (Dm.truthy(w_class))
        {
            if (Dm.truthy(force))
                return clamp((force + w_class) * 4, 30, 100);// Add the item's force to its weight class and multiply by 4, then clamp the value between 30 and 100
            else
                return clamp(w_class * 6, 10, 100); // Multiply the item's weight class by 6, then clamp the value between 10 and 100
        }
        return null;
    }
}

// tg: code/_onclick/item_attack.dm:490
public partial class mob_living : mob
{
    // tg: code/_onclick/item_attack.dm:490
    public virtual object send_item_attack_message(obj_item weapon = null, mob_living user = null, object hit_area = null, object def_zone = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(weapon._listen_lookup?[COMSIG_SEND_ITEM_ATTACK_MESSAGE_OBJECT]) ? (object)NONE : weapon._SendSignal(COMSIG_SEND_ITEM_ATTACK_MESSAGE_OBJECT, Dm.list(weapon, this, user))), SIGNAL_MESSAGE_MODIFIED)))
            return TRUE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_SEND_ITEM_ATTACK_MESSAGE_CARBON]) ? (object)NONE : this._SendSignal(COMSIG_SEND_ITEM_ATTACK_MESSAGE_CARBON, Dm.list(this, weapon, user))), SIGNAL_MESSAGE_MODIFIED)))
            return TRUE;

        if (!Dm.truthy(weapon.force) && !Dm.truthy(Dm.length(weapon.attack_verb_simple)) && !Dm.truthy(Dm.length(weapon.attack_verb_continuous)))
            return null;

        // Sanity in case one is null for some reason
        float picked_index = rand(Dm.max(Dm.length(weapon.attack_verb_simple), Dm.length(weapon.attack_verb_continuous)));

        object message_verb_continuous = "attacks";
        object message_verb_simple = "attack";
        object message_hit_area = get_hit_area_message(hit_area);
        // Sanity in case one is... longer than the other?
        if (Dm.truthy(picked_index) && Dm.length(weapon.attack_verb_continuous) >= picked_index)
            message_verb_continuous = weapon.attack_verb_continuous[picked_index];
        if (Dm.truthy(picked_index) && Dm.length(weapon.attack_verb_simple) >= picked_index)
            message_verb_simple = weapon.attack_verb_simple[picked_index];

        string attack_message_spectator = I18n.T("[] [][] with []!", this, message_verb_continuous, message_hit_area, weapon);
        string attack_message_victim = I18n.T("Something [] you[] with []!", message_verb_continuous, message_hit_area, weapon);
        string attack_message_attacker = I18n.T("You [] [][] with []!", message_verb_simple, this, message_hit_area, weapon);
        if (Dm.@in(user, viewers(this, null)))
        {
            attack_message_spectator = I18n.T("[] [] [][] with []!", user, message_verb_continuous, this, message_hit_area, weapon);
            attack_message_victim = I18n.T("[] [] you[] with []!", user, message_verb_continuous, message_hit_area, weapon);
        }
        if (user == this)
            attack_message_victim = I18n.T("You [] yourself[] with [].", message_verb_simple, message_hit_area, weapon);
        visible_message(span_danger(I18n.T("[]", attack_message_spectator)),
            span_userdanger(I18n.T("[]", attack_message_victim)), null, COMBAT_MESSAGE_RANGE, user);
        if (Dm.truthy(is_blind()))
            to_chat(this, span_danger(I18n.T("Someone hits you[]!", message_hit_area)));
        to_chat(user, span_danger(I18n.T("[]", attack_message_attacker)));
        return 1;
    }

    /// Overridable proc so subtypes can have unique targetted strike zone messages, return a string.
    // tg: code/_onclick/item_attack.dm:526
    public virtual object get_hit_area_message(object input_area = null)
    {
        if (Dm.truthy(input_area))
            return I18n.T(" in the []", input_area);

        return "";
    }
}
