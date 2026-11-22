// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/touch/_touch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327, with the shock and mending touch mutations): the touch spells, cast through a hand item that hits its
// target, and the hand. The spicy handshake achievement is a recorded leaf (the achievements). The var blocks are generated (D111).

/**
 * ## Touch Spell
 *
 * Touch spells are spells which function through the power of an item attack.
 */
// tg: code/modules/spells/spell_types/touch/_touch.dm:21
public partial class datum_action_cooldown_spell_touch : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/touch/_touch.dm:38
    public override object Destroy(float? force = null)
    {
        // If we have an owner, the hand is cleaned up in Remove(), which Destroy() calls.
        if (owner == null)
            QDEL_NULL(ref attached_hand);
        return base.Destroy(force);
    }

    // tg: code/modules/spells/spell_types/touch/_touch.dm:44
    public override async DmTask Remove(mob remove_from = null)
    {
        await remove_hand(remove_from as mob_living);
        await base.Remove(remove_from);
    }

    // PreActivate is overridden to not check is_valid_target on the caster, as it makes less sense.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:49
    public override async DmTask<object> PreActivate(atom target = null)
    {
        return await Activate(target);
    }

    // tg: code/modules/spells/spell_types/touch/_touch.dm:52
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return attached_hand != null ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/touch/_touch.dm:55
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        // Currently, hard checks for carbons.
        // If someone wants to add support for simplemobs
        // adminbussed to have hands, go for it
        if (!iscarbon(owner))
            return FALSE;
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        if (!Dm.truthy(Dm.bitand(carbon_owner.mobility_flags, MOBILITY_USE)))
            return FALSE;
        return TRUE;
    }

    // Checks if the mob slapped with the hand is a valid target.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:70
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return isliving(cast_on) ? TRUE : FALSE;
    }

    /**
     * Creates a new hand_path hand and equips it to the caster.
     *
     * If the equipping action fails, reverts the cooldown and returns FALSE.
     * Otherwise, registers signals and returns TRUE.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:79
    [ShouldCallParent]
    public virtual async DmTask<object> create_hand(mob_living_carbon cast_on = null)
    {
        obj_item_melee_touch_attack new_hand = Dm.@new(hand_path, cast_on, this) as obj_item_melee_touch_attack;
        if (!Dm.truthy(await cast_on.put_in_hands(new_hand, del_on_fail: TRUE)))
        {
            await reset_spell_cooldown();
            if (Dm.num(cast_on.usable_hands) == 0)
                to_chat(cast_on, span_warning(I18n.T("You dont have any usable hands!")));
            else
                to_chat(cast_on, span_warning(I18n.T("Your hands are full!")));
            return FALSE;
        }

        attached_hand = new_hand;
        register_hand_signals();
        to_chat(cast_on, draw_message);
        return TRUE;
    }

    /**
     * Unregisters any signals and deletes the hand currently summoned by the spell.
     *
     * If reset_cooldown_after is TRUE, we will additionally refund the cooldown of the spell.
     * If reset_cooldown_after is FALSE, we will instead just start the spell's cooldown
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:102
    [ShouldCallParent]
    public virtual async DmTask remove_hand(mob_living hand_owner = null, float? reset_cooldown_after = null)
    {
        reset_cooldown_after ??= FALSE;
        if (!QDELETED(attached_hand))
        {
            unregister_hand_signals();
            if (hand_owner != null)
                await hand_owner.temporarilyRemoveItemFromInventory(attached_hand);
            QDEL_NULL(ref attached_hand);
        }

        if (Dm.truthy(reset_cooldown_after))
        {
            if (hand_owner != null)
                to_chat(hand_owner, drop_message);
            await reset_spell_cooldown();
        }
        else
        {
            StartCooldown();
            await build_all_button_icons();
        }
    }

    /// Registers all signal procs for the hand.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:119
    [ShouldCallParent]
    public virtual void register_hand_signals()
    {
        RegisterSignal(attached_hand, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_hand_hit));
        RegisterSignal(attached_hand, COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY, nameof(on_hand_hit_secondary));
        RegisterSignal(attached_hand, COMSIG_ITEM_DROPPED, nameof(on_hand_dropped));
        RegisterSignal(attached_hand, COMSIG_QDELETING, nameof(on_hand_deleted));

        // We can high five with our touch hand. It casts the spell on people. Radical
        Waiting.NoSleep(attached_hand.AddElement(typeof(datum_element_high_fiver)));
        RegisterSignal(attached_hand, COMSIG_ITEM_OFFER_TAKEN, nameof(on_hand_taken));
    }

    /// Unregisters all signal procs for the hand.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:132
    [ShouldCallParent]
    public virtual void unregister_hand_signals()
    {
        UnregisterSignal(attached_hand, Dm.list(
            COMSIG_ITEM_INTERACTING_WITH_ATOM,
            COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY,
            COMSIG_ITEM_DROPPED,
            COMSIG_QDELETING,
            COMSIG_ITEM_OFFER_TAKEN));
    }

    // Touch spells don't go on cooldown OR give off an invocation until the hand is used itself.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:144
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        return Dm.bitor(Dm.bitor(await base.before_cast(cast_on), SPELL_NO_FEEDBACK), SPELL_NO_IMMEDIATE_COOLDOWN);
    }

    // tg: code/modules/spells/spell_types/touch/_touch.dm:147
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living_carbon cast_on = target as mob_living_carbon;
        if (Dm.truthy(Dm.bitand(cast_on.SEND_SIGNAL(COMSIG_TOUCH_HANDLESS_CAST, Dm.list(cast_on, this)), COMPONENT_CAST_HANDLESS)))
        {
            StartCooldown();
            return null;
        }

        if (!QDELETED(attached_hand) && Dm.@in(attached_hand, cast_on.held_items))
        {
            await remove_hand(cast_on, reset_cooldown_after: TRUE);
            return null;
        }

        await create_hand(cast_on);
        return await base.cast(target);
    }

    /**
     * Signal proc for [COMSIG_ITEM_INTERACTING_WITH_ATOM] from our attached hand.
     *
     * When our hand hits an atom, we can cast do_hand_hit() on them.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:164
    [SignalHandler, ShouldNotOverride]
    public virtual object on_hand_hit(datum source = null, mob_living caster = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(can_hit_with_hand(target, caster))))
            return NONE;

        return Waiting.NoSleep(do_hand_hit(source as obj_item_melee_touch_attack, target, caster as mob_living_carbon));
    }

    /**
     * Signal proc for [COMSIG_ITEM_INTERACTING_WITH_ATOM_SECONDARY] from our attached hand.
     *
     * When our hand hits an atom, we can cast do_hand_hit() on them.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:178
    [SignalHandler, ShouldNotOverride]
    public virtual object on_hand_hit_secondary(datum source = null, mob_living caster = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(can_hit_with_hand(target, caster))))
            return NONE;

        return Waiting.NoSleep(do_secondary_hand_hit(source as obj_item_melee_touch_attack, target, caster as mob_living_carbon));
    }

    /// Checks if the passed victim can be cast on by the caster.
    // tg: code/modules/spells/spell_types/touch/_touch.dm:188
    public virtual async DmTask<object> can_hit_with_hand(atom victim = null, mob_living caster = null)
    {
        if (!Dm.truthy(can_cast_on_self) && victim == caster)
            return FALSE;
        if (!Dm.truthy(await is_valid_target(victim)))
            return FALSE;
        if (!Dm.truthy(await can_cast_spell(feedback: TRUE)))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(caster.mobility_flags, MOBILITY_USE)))
        {
            caster.balloon_alert(caster, I18n.T("can't reach out!"));
            return FALSE;
        }

        return TRUE;
    }

    /**
     * Calls cast_on_hand_hit() from the caster onto the victim.
     * It's worth noting that victim will be guaranteed to be whatever checks are implemented in is_valid_target by this point.
     *
     * Implements checks for antimagic.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:207
    [ShouldNotOverride]
    public virtual async DmTask<object> do_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        // SEND_SIGNAL(src, COMSIG_SPELL_TOUCH_HAND_HIT, victim, caster, hand) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_TOUCH_HAND_HIT]))
            _SendSignal(COMSIG_SPELL_TOUCH_HAND_HIT, Dm.list(this, victim, caster, hand));

        if (victim is mob mob_victim && Dm.truthy(mob_victim.can_block_magic(antimagic_flags)))
            await on_antimagic_triggered(hand, victim, caster);
        else if (!Dm.truthy(await cast_on_hand_hit(hand, victim, caster)))
            return NONE;

        // SEND_SIGNAL(caster, COMSIG_SPELL_TOUCH_SPELL_ACTUALLY_CAST, src, victim) (rules 11.1 (e), a statement)
        if (Dm.truthy(caster._listen_lookup?[COMSIG_SPELL_TOUCH_SPELL_ACTUALLY_CAST]))
            caster._SendSignal(COMSIG_SPELL_TOUCH_SPELL_ACTUALLY_CAST, Dm.list(caster, this, victim));
        log_combat(caster, victim, Dm.text("cast the touch spell [] on", name), hand);
        Waiting.Detach(spell_feedback(caster)); // INVOKE_ASYNC
        await caster.do_attack_animation(victim);
        caster.changeNext_move(CLICK_CD_MELEE);
        victim.add_fingerprint(caster);
        await remove_hand(caster);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Calls do_secondary_hand_hit() from the caster onto the victim.
     * Does NOT check for antimagic on its own. Implement your own checks if you want the r-click to abide by it.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:234
    [ShouldNotOverride]
    public virtual async DmTask<object> do_secondary_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        object secondary_result = await cast_on_secondary_hand_hit(hand, victim, caster);
        switch (Dm.num(secondary_result))
        {
            // Continue will remove the hand here and stop
            case SECONDARY_ATTACK_CONTINUE_CHAIN:
                log_combat(caster, victim, Dm.text("cast the touch spell [] on", name), hand, "(secondary / alt cast)");
                Waiting.Detach(spell_feedback(caster)); // INVOKE_ASYNC
                await caster.do_attack_animation(victim);
                caster.changeNext_move(CLICK_CD_MELEE);
                victim.add_fingerprint(caster);
                await remove_hand(caster);
                return ITEM_INTERACT_SUCCESS;

            // Call normal will call the normal cast proc
            case SECONDARY_ATTACK_CALL_NORMAL:
                await do_hand_hit(hand, victim, caster);
                break;

            // Cancel chain will do nothing,
            case SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN:
                return NONE;
        }
        return null;
    }

    /**
     * The actual process of casting the spell on the victim from the caster.
     *
     * Override / extend this to implement casting effects.
     * Return TRUE on a successful cast to use up the hand (delete it)
     * Return FALSE to do nothing and let them keep the hand in hand
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:264
    public virtual async DmTask<object> cast_on_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    /**
     * For any special casting effects done if the user right-clicks
     * on touch spell instead of left-clicking
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:275
    public virtual async DmTask<object> cast_on_secondary_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        await DmTask.CompletedTask;
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    /**
     * Signal proc for [COMSIG_QDELETING] from our attached hand.
     *
     * If our hand is deleted for a reason unrelated to our spell,
     * unlink it (clear refs) and revert the cooldown
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:284
    [SignalHandler]
    public virtual void on_hand_deleted(datum source = null)
    {
        Waiting.NoSleep(remove_hand(reset_cooldown_after: TRUE));
    }

    /**
     * Signal proc for [COMSIG_ITEM_DROPPED] from our attached hand.
     *
     * If our caster drops the hand, remove the hand / revert the cast
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:295
    [SignalHandler]
    public virtual void on_hand_dropped(datum source = null, mob_living dropper = null)
    {
        Waiting.NoSleep(remove_hand(dropper, reset_cooldown_after: TRUE));
    }

    /**
     * Signal proc for [COMSIG_ITEM_OFFER_TAKEN] from our attached hand.
     *
     * Giving a high five with our hand makes it cast
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:305
    [SignalHandler]
    public virtual object on_hand_taken(obj_item source = null, mob_living_carbon offerer = null, mob_living_carbon taker = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(can_hit_with_hand(taker, offerer))))
            return null;

        // offerer.client?.give_award(/datum/award/achievement/misc/spicy_handshake, offerer): the achievements (M10)
        if (offerer.client != null)
            Dm.Deferred("code/modules/spells/spell_types/touch/_touch.dm:311", "on_hand_taken: the spicy handshake achievement (the achievements)");
        Waiting.Detach(do_hand_hit(source as obj_item_melee_touch_attack, taker, offerer)); // INVOKE_ASYNC
        return COMPONENT_OFFER_INTERRUPT;
    }

    /**
     * Called whenever our spell is cast, but blocked by antimagic.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:318
    public virtual async DmTask on_antimagic_triggered(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        await DmTask.CompletedTask;
    }
}

/**
 * ## Touch attack item
 *
 * Used for touch spells to have something physical to slap people with.
 */
// tg: code/modules/spells/spell_types/touch/_touch.dm:331
public partial class obj_item_melee_touch_attack : obj_item_melee
{
    // tg: code/modules/spells/spell_types/touch/_touch.dm:348
    [DmParams("mapload", "spell")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_action_cooldown_spell spell = Dm.arg(__extra, 0) as datum_action_cooldown_spell;
        object dot = base.Initialize(mapload, __extra);

        if (spell != null)
            spell_which_made_us = WEAKREF(spell) as datum_weakref;
        return dot;
    }

    /**
     * When the hand component of a touch spell is qdel'd, (the hand is dropped or otherwise lost),
     * the cooldown on the spell that made it is automatically refunded.
     *
     * However, if you want to consume the hand and not give a cooldown,
     * such as adding a unique behavior to the hand specifically, this function will do that.
     */
    // tg: code/modules/spells/spell_types/touch/_touch.dm:361
    public virtual async DmTask remove_hand_with_no_refund(mob holder = null)
    {
        datum_action_cooldown_spell_touch hand_spell = spell_which_made_us?.resolve() as datum_action_cooldown_spell_touch;
        if (!QDELETED(hand_spell))
        {
            await hand_spell.remove_hand(holder as mob_living, reset_cooldown_after: FALSE);
            return;
        }

        // We have no spell associated for some reason, just delete us as normal.
        await holder.temporarilyRemoveItemFromInventory(this, force: TRUE);
        qdel(this);
    }
}
