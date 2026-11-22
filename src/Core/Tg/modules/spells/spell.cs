// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the stat panel's "check_spells" message (no stat panel in the 3D client) (gaps package C6).

/// The base action of the game's spells: cooldown-based, cast through before_cast, spell_feedback, cast, after_cast.
// tg: code/modules/spells/spell.dm:44
public partial class datum_action_cooldown_spell : datum_action_cooldown
{
    // tg: code/modules/spells/spell.dm:90
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        // If our spell is mind-bound, we only wanna grant it to our mind
        if (target is datum_mind mind_target && mind_target.current != granted_to)
            return null;

        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;

        // Register some signals so our button's icon stays up to date
        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_STATION)))
            RegisterSignal(owner, COMSIG_MOVABLE_Z_CHANGED, nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(spell_requirements, Dm.bitor(SPELL_REQUIRES_NO_ANTIMAGIC, SPELL_REQUIRES_WIZARD_GARB))))
            RegisterSignals(owner, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM), nameof(update_status_on_signal));
        if (invocation_type == INVOCATION_EMOTE)
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_EMOTEMUTE), SIGNAL_REMOVETRAIT(TRAIT_EMOTEMUTE)), nameof(update_status_on_signal));
        if (invocation_type == INVOCATION_SHOUT || invocation_type == INVOCATION_WHISPER)
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MUTE), SIGNAL_REMOVETRAIT(TRAIT_MUTE)), nameof(update_status_on_signal));

        RegisterSignals(owner, Dm.list(COMSIG_MOB_ENTER_JAUNT, COMSIG_MOB_AFTER_EXIT_JAUNT), nameof(update_status_on_signal));
        return dot;
    }

    // tg: code/modules/spells/spell.dm:114
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(removed_from, Dm.list(
            COMSIG_MOB_AFTER_EXIT_JAUNT,
            COMSIG_MOB_ENTER_JAUNT,
            COMSIG_MOB_EQUIPPED_ITEM,
            COMSIG_MOB_UNEQUIPPED_ITEM,
            COMSIG_MOVABLE_Z_CHANGED,
            SIGNAL_ADDTRAIT(TRAIT_EMOTEMUTE),
            SIGNAL_REMOVETRAIT(TRAIT_EMOTEMUTE),
            SIGNAL_ADDTRAIT(TRAIT_MUTE),
            SIGNAL_REMOVETRAIT(TRAIT_MUTE)));

        await base.Remove(removed_from);
    }

    // tg: code/modules/spells/spell.dm:131
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        return Dm.truthy(await base.IsAvailable(feedback)) ? await can_cast_spell(feedback) : FALSE;
    }

    // tg: code/modules/spells/spell.dm:134
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(on_who._listen_lookup?[COMSIG_MOB_SPELL_ACTIVATED]) ? NONE : on_who._SendSignal(COMSIG_MOB_SPELL_ACTIVATED, Dm.list(on_who, this)), SPELL_CANCEL_CAST)))
            return FALSE;

        return await base.set_click_ability(on_who);
    }

    // Where the cast chain starts
    // tg: code/modules/spells/spell.dm:141
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(owner._listen_lookup?[COMSIG_MOB_ABILITY_STARTED]) ? NONE : owner._SendSignal(COMSIG_MOB_ABILITY_STARTED, Dm.list(owner, this, target)), COMPONENT_BLOCK_ABILITY_START)))
            return FALSE;
        if (target == owner)
            target = get_caster_from_target(target) as atom;
        if (target == null || !Dm.truthy(await is_valid_target(target)))
            return FALSE;

        return await Activate(target);
    }

    /// Checks if the owner of the spell can currently cast it. Does not check anything involving potential targets.
    // tg: code/modules/spells/spell.dm:153
    public virtual async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        if (owner == null)
            throw Dm.CRASH(Dm.text("[] - can_cast_spell called on a spell without an owner!", type), "code/modules/spells/spell.dm", 155);

        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_SPELL_CAN_CAST_CHECK]) ? NONE : _SendSignal(COMSIG_SPELL_CAN_CAST_CHECK, Dm.list(this, feedback)), SPELL_CANCEL_CAST)))
            return FALSE;

        // Certain spells are not allowed on the centcom zlevel
        turf caster_turf = get_turf(owner);
        // Spells which require being on the station
        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_STATION)) && !Dm.truthy(is_station_level(caster_turf.z)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(Dm.text("You can't cast [] here!", this)));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_MIND)) && owner.mind == null)
            return FALSE; // No point in feedback here, as mindless mobs aren't players

        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_MIME_VOW)) && !HAS_MIND_TRAIT(owner, TRAIT_MIMING))
        {
            // In the future this can be moved out of spell checks exactly
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning("You must dedicate yourself to silence first!"));
            return FALSE;
        }

        // If the spell requires the user has no antimagic equipped, and they're holding antimagic
        // that corresponds with the spell's antimagic, then they can't actually cast the spell
        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_NO_ANTIMAGIC)) && !Dm.truthy(owner.can_cast_magic(antimagic_flags)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(Dm.text("Some form of antimagic is preventing you from casting []!", this)));
            return FALSE;
        }

        if (!Dm.truthy(await try_invoke(owner as mob_living, feedback)))
            return FALSE;

        if (ishuman(owner))
        {
            if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_WIZARD_GARB)))
            {
                object robe = owner.get_item_by_slot(ITEM_SLOT_OCLOTHING);
                if ((robe == null || !HAS_TRAIT(robe, TRAIT_CASTING_CLOTHING)) && !HAS_TRAIT(owner, TRAIT_LESSER_HUMANOID)) // Monkeys don't need robes to cast as they are inherently imbued with power from the banana dimension
                {
                    if (Dm.truthy(feedback))
                        to_chat(owner, span_warning("You don't feel strong enough without your robe!"));
                    return FALSE;
                }
                object hat = owner.get_item_by_slot(ITEM_SLOT_HEAD);
                object glasses = owner.get_item_by_slot(ITEM_SLOT_EYES);
                if ((hat == null || !HAS_TRAIT(hat, TRAIT_CASTING_CLOTHING)) && (glasses == null || !HAS_TRAIT(glasses, TRAIT_CASTING_CLOTHING)))
                {
                    if (Dm.truthy(feedback))
                        to_chat(owner, span_warning("You don't feel strong enough without your hat!"));
                    return FALSE;
                }
            }
        }
        else
        {
            // If you strictly need to be a human, well, goodbye.
            if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_HUMAN)))
            {
                if (Dm.truthy(feedback))
                    to_chat(owner, span_warning(Dm.text("[] can only be cast by humans!", this)));
                return FALSE;
            }

            // Otherwise, we can check for contents if they have wizardly apparel.
            if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_REQUIRES_WIZARD_GARB)))
            {
                bool any_casting = false;
                foreach (obj_item item in Dm.each_cast<obj_item>(owner.get_equipped_items()))
                {
                    if (HAS_TRAIT(item, TRAIT_CASTING_CLOTHING))
                    {
                        any_casting = true;
                        break;
                    }
                }

                if (!any_casting)
                {
                    if (Dm.truthy(feedback))
                        to_chat(owner, span_warning("You don't feel strong enough without your hat!"));
                    return FALSE;
                }
            }

            if (!Dm.truthy(Dm.bitand(spell_requirements, SPELL_CASTABLE_AS_BRAIN)) && isbrain(owner))
            {
                if (Dm.truthy(feedback))
                    to_chat(owner, span_warning(Dm.text("[] can't be cast in this state!", this)));
                return FALSE;
            }
        }

        return TRUE;
    }

    /// Check if the target we're casting on is a valid target.
    // tg: code/modules/spells/spell.dm:233
    public virtual async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return TRUE;
    }

    /// Used to get the cast_on atom if a self cast spell is being cast.
    // tg: code/modules/spells/spell.dm:241
    public virtual object get_caster_from_target(atom target = null)
    {
        atom cast_loc = target.loc;
        if (cast_loc == null)
            return null; // No magic in nullspace

        if (isturf(cast_loc))
            return target; // They're just standing around, proceed as normal

        if (HAS_TRAIT(cast_loc, TRAIT_CASTABLE_LOC))
        {
            if (HAS_TRAIT(cast_loc, TRAIT_SPELLS_TRANSFER_TO_LOC) && ismob(cast_loc.loc))
                return cast_loc.loc;
            return cast_loc;
        }
        // They're in an atom which allows casting, so redirect the caster to loc

        return null;
    }

    // The actual cast chain occurs here, in Activate().
    // tg: code/modules/spells/spell.dm:261
    public override async DmTask<object> Activate(atom target = null)
    {
        atom cast_on = target;
        // Pre-cast is the very last chance for a spell to cancel
        object precast_result = await before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(precast_result, SPELL_CANCEL_CAST)))
            return FALSE;

        // Spell is officially being cast
        // We do invocation and sound effects here, before actual cast
        if (!Dm.truthy(Dm.bitand(precast_result, SPELL_NO_FEEDBACK)))
            await spell_feedback(owner as mob_living);

        // Actually cast the spell. Main effects go here
        await cast(cast_on);

        // The entire spell is done, start the actual cooldown at its set duration
        if (!Dm.truthy(Dm.bitand(precast_result, SPELL_NO_IMMEDIATE_COOLDOWN)))
            StartCooldown();

        // And then proceed with the aftermath of the cast
        await after_cast(cast_on);
        await build_all_button_icons();

        return TRUE;
    }

    /// Actions done before the actual cast is called; the last chance to cancel. Returns a bitflag.
    // tg: code/modules/spells/spell.dm:302
    public virtual async DmTask<object> before_cast(atom cast_on = null)
    {
        // Bonus invocation check done here:
        // If the caster has no tongue and it's a verbal spell, or has no hands and is a gesture spell - cancel it
        if (ishuman(owner) && !Dm.truthy(Dm.bitand(spell_requirements, SPELL_CASTABLE_WITHOUT_INVOCATION)))
        {
            mob_living_carbon_human caster = owner as mob_living_carbon_human;
            if (invocation_type == INVOCATION_WHISPER || invocation_type == INVOCATION_SHOUT)
            {
                if (caster.get_organ_slot(ORGAN_SLOT_TONGUE) == null)
                {
                    await invocation__proc(caster);
                    to_chat(caster, span_warning(Dm.text("Your lack of tongue is making it difficult to say the correct words to cast []...", this)));
                    if (caster.click_intercept == this)
                        await unset_click_ability(caster, refund_cooldown: TRUE);
                    StartCooldown(2 * SECONDS);
                    return SPELL_CANCEL_CAST;
                }
            }
            else if (invocation_type == INVOCATION_EMOTE)
            {
                if (Dm.num(caster.usable_hands) <= 0)
                {
                    string arm_describer = Dm.num(caster.num_hands) >= 2 ? "arms limply" : (Dm.num(caster.num_hands) == 1 ? "arm wildly" : "arm stumps");
                    caster.visible_message(span_warning(Dm.text("[] wiggles around [] [].", caster, caster.p_their(), arm_describer)), ignored_mobs: caster);
                    to_chat(caster, span_warning(Dm.text("You can't position your hands correctly to invoke [][]...", this, Dm.num(caster.num_hands) > 0 ? "" : ", as you have none")));
                    if (caster.click_intercept == this)
                        await unset_click_ability(caster, refund_cooldown: TRUE);
                    StartCooldown(2 * SECONDS);
                    return SPELL_CANCEL_CAST;
                }
            }
        }

        object sig_return = !Dm.truthy(_listen_lookup?[COMSIG_SPELL_BEFORE_CAST]) ? NONE : _SendSignal(COMSIG_SPELL_BEFORE_CAST, Dm.list(this, cast_on));
        if (owner != null && Dm.truthy(owner._listen_lookup?[COMSIG_MOB_BEFORE_SPELL_CAST]))
            sig_return = Dm.bitor(sig_return, owner._SendSignal(COMSIG_MOB_BEFORE_SPELL_CAST, Dm.list(owner, this, cast_on)));

        return sig_return;
    }

    /// Actions done as the main effect of the spell.
    // tg: code/modules/spells/spell.dm:346
    public virtual async DmTask<object> cast(atom cast_on = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_CAST]))
            _SendSignal(COMSIG_SPELL_CAST, Dm.list(this, cast_on));
        if (owner != null)
        {
            if (Dm.truthy(owner._listen_lookup?[COMSIG_MOB_CAST_SPELL]))
                owner._SendSignal(COMSIG_MOB_CAST_SPELL, Dm.list(owner, this, cast_on));
            if (Dm.truthy(owner.ckey))
                owner.log_message(Dm.text("cast the spell [][].", name, cast_on != owner ? Dm.text(" on / at []", cast_on) : ""), LOG_ATTACK);
        }
        return null;
    }

    /// Actions done after the main cast is finished; the cooldown has already begun.
    // tg: code/modules/spells/spell.dm:363
    public virtual async DmTask after_cast(atom cast_on = null)
    {
        if (owner == null) // Could have been destroyed by the effect of the spell
        {
            if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_AFTER_CAST]))
                _SendSignal(COMSIG_SPELL_AFTER_CAST, Dm.list(this, cast_on));
            return;
        }

        if (Dm.truthy(sparks_amt))
            do_sparks(sparks_amt, FALSE, get_turf(owner));
        if (smoke_type != null && ispath(smoke_type, typeof(datum_effect_system_fluid_spread_smoke)))
            await do_smoke(smoke_amt, owner as atom, get_turf(owner));

        // Send signals last in case they delete the spell
        if (Dm.truthy(owner._listen_lookup?[COMSIG_MOB_AFTER_SPELL_CAST]))
            owner._SendSignal(COMSIG_MOB_AFTER_SPELL_CAST, Dm.list(owner, this, cast_on));
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_AFTER_CAST]))
            _SendSignal(COMSIG_SPELL_AFTER_CAST, Dm.list(this, cast_on));
    }

    /// Provides feedback after a spell cast occurs, in the form of a cast sound and/or invocation
    // tg: code/modules/spells/spell.dm:379
    public virtual async DmTask spell_feedback(mob_living invoker = null)
    {
        if (invoker == null)
            return;

        // even INVOCATION_NONE should go through this because the signal might change that
        await invocation__proc(invoker);
        if (Dm.truthy(sound))
            playsound(invoker, sound, 50, vary: TRUE);
    }

    /// The invocation that accompanies the spell, called from spell_feedback() before cast().
    // tg: code/modules/spells/spell.dm:389
    public virtual async DmTask invocation__proc(mob_living invoker = null)
    {
        //lists can be sent by reference, a string would be sent by value
        DmList invocation_list = Dm.list(invocation, invocation_type, garbled_invocation_prob);
        if (Dm.truthy(invoker._listen_lookup?[COMSIG_MOB_PRE_INVOCATION]))
            invoker._SendSignal(COMSIG_MOB_PRE_INVOCATION, Dm.list(invoker, this, invocation_list));
        object used_invocation_message = invocation_list[INVOCATION_MESSAGE];
        object used_invocation_type = invocation_list[INVOCATION_TYPE];
        object used_invocation_garble_prob = invocation_list[INVOCATION_GARBLE_PROB];

        if (Dm.eq(used_invocation_type, INVOCATION_SHOUT))
        {
            object said = prob(Dm.num(used_invocation_garble_prob)) ? replacetext(Dm.text("[]", used_invocation_message), " ", "`") : used_invocation_message;
            await invoker.say(said, forced: Dm.text("spell ([])", this));
        }
        else if (Dm.eq(used_invocation_type, INVOCATION_WHISPER))
        {
            object said = prob(Dm.num(used_invocation_garble_prob)) ? replacetext(Dm.text("[]", used_invocation_message), " ", "`") : used_invocation_message;
            await invoker.whisper(said, forced: Dm.text("spell ([])", this));
        }
        else if (Dm.eq(used_invocation_type, INVOCATION_EMOTE))
        {
            invoker.visible_message(
                // shortcut: REPLACE_PRONOUNS (__HELPERS/pronouns.dm:81, %PRONOUN_ in a heretic or cult emote) is not ported: no live spell uses it; upgrade with those spells
                capitalize(replacetext(Dm.text("[]", used_invocation_message), "%CASTER", Dm.text("[]", invoker))),
                capitalize(replacetext(Dm.text("[]", invocation_self_message), "%CASTER", Dm.text("[]", invoker))),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        }
    }

    /// Checks if the current OWNER of the spell is in a valid state to say the spell's invocation
    // tg: code/modules/spells/spell.dm:418
    public virtual async DmTask<object> try_invoke(mob_living invoker = null, float? feedback = null)
    {
        feedback ??= TRUE;
        if (Dm.truthy(Dm.bitand(spell_requirements, SPELL_CASTABLE_WITHOUT_INVOCATION)))
            return TRUE;

        if (invocation_type == INVOCATION_NONE)
            return TRUE;

        // If you want a spell usable by ghosts for some reason, it must be INVOCATION_NONE
        if (invoker == null)
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(Dm.text("You need to be living to invoke []!", this)));
            return FALSE;
        }

        object invoke_sig_return = !Dm.truthy(invoker._listen_lookup?[COMSIG_MOB_TRY_INVOKE_SPELL]) ? NONE : invoker._SendSignal(COMSIG_MOB_TRY_INVOKE_SPELL, Dm.list(invoker, this, feedback));
        if (Dm.truthy(Dm.bitand(invoke_sig_return, SPELL_INVOCATION_ALWAYS_SUCCEED)))
            return TRUE; // skips all of the following checks
        if (Dm.truthy(Dm.bitand(invoke_sig_return, SPELL_INVOCATION_FAIL)))
            return FALSE;

        if (invocation_type == INVOCATION_EMOTE && HAS_TRAIT(invoker, TRAIT_EMOTEMUTE))
        {
            if (Dm.truthy(feedback))
                to_chat(invoker, span_warning(Dm.text("You can't position your hands correctly to invoke []!", this)));
            return FALSE;
        }

        if ((invocation_type == INVOCATION_WHISPER || invocation_type == INVOCATION_SHOUT) && !Dm.truthy(invoker.can_speak__proc()))
        {
            if (Dm.truthy(feedback))
                to_chat(invoker, span_warning(Dm.text("You can't get the words out to invoke []!", this)));
            return FALSE;
        }

        return TRUE;
    }

    /// Resets the cooldown of the spell and allows it to be used immediately
    // tg: code/modules/spells/spell.dm:451
    public virtual async DmTask reset_spell_cooldown()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_CAST_RESET]))
            _SendSignal(COMSIG_SPELL_CAST_RESET, Dm.list(this));
        next_use_time -= Dm.num(cooldown_time); // Basically, ensures that the ability can be used now
        await build_all_button_icons();
    }

    /// Levels the spell up a single level, reducing the cooldown.
    // tg: code/modules/spells/spell.dm:460
    public virtual async DmTask<object> level_spell(float? bypass_cap = null)
    {
        // Spell cannot be levelled
        if (spell_max_level <= 1)
            return FALSE;

        // Spell is at cap, and we will not bypass it
        if (!Dm.truthy(bypass_cap) && spell_level >= spell_max_level)
            return FALSE;

        spell_level++;
        cooldown_time = Dm.max(Dm.num(cooldown_time) - cooldown_reduction_per_rank, 0.25f * SECONDS); // 0 second CD starts to break things.
        name = Dm.text("[][]", get_spell_title(), Dm.initial(GetType(), (datum_action_cooldown_spell p) => p.name));
        await build_all_button_icons(UPDATE_BUTTON_NAME);
        return TRUE;
    }

    /// Levels the spell down a single level, down to 1.
    // tg: code/modules/spells/spell.dm:478
    public virtual async DmTask<object> delevel_spell()
    {
        // Spell cannot be levelled
        if (spell_max_level <= 1)
            return FALSE;

        if (spell_level <= 1)
            return FALSE;

        spell_level--;
        object initial_cooldown = Dm.initial(GetType(), (datum_action_cooldown_spell p) => p.cooldown_time);
        if (cooldown_reduction_per_rank > 0)
            cooldown_time = Dm.min(Dm.num(cooldown_time) + cooldown_reduction_per_rank, Dm.num(initial_cooldown));
        else
            cooldown_time = Dm.max(Dm.num(cooldown_time) + cooldown_reduction_per_rank, Dm.num(initial_cooldown));

        name = Dm.text("[][]", get_spell_title(), Dm.initial(GetType(), (datum_action_cooldown_spell p) => p.name));
        await build_all_button_icons(UPDATE_BUTTON_NAME);
        return TRUE;
    }

    /// Gets the title of the spell based on its level.
    // tg: code/modules/spells/spell.dm:497
    public virtual string get_spell_title()
    {
        return spell_level switch
        {
            2 => "Efficient ",
            3 => "Quickened ",
            4 => "Free ",
            5 => "Instant ",
            6 => "Ludicrous ",
            _ => "",
        };
    }
}
