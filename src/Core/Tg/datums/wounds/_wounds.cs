// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/_wounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2, the wound engine; second_wind's determination reagent with M8 slice B). Holds the [DmPath] of /datum/wound (rules 2.3: the earliest .dme file that declares it); the var block is generated
// (D111) but for the static exclusive_surgery_states, which lives in TgState (rules 10.6).

/*
	Wounds are specific medical complications that can arise and be applied to (currently) carbons, with a focus on humans. All of the code for and related to this is heavily WIP,
	and the documentation will be slanted towards explaining what each part/piece is leading up to, until such a time as I finish the core implementations. The original design doc
	can be found at https://hackmd.io/@Ryll/r1lb4SOwU

	Wounds are datums that operate like a mix of diseases, brain traumas, and components, and are applied to a /obj/item/bodypart (preferably attached to a carbon) when they take large spikes of damage
	or under other certain conditions (thrown hard against a wall, sustained exposure to plasma fire, etc). Wounds are categorized by the three following criteria:
		1. Severity: Either MODERATE, SEVERE, or CRITICAL. See the hackmd for more details
		2. Viable zones: What body parts the wound is applicable to. Generic wounds like broken bones and severe burns can apply to every zone, but you may want to add special wounds for certain limbs
			like a twisted ankle for legs only, or open air exposure of the organs for particularly gruesome chest wounds. Wounds should be able to function for every zone they are marked viable for.
		3. Damage type: Currently either BRUTE or BURN. Again, see the hackmd for a breakdown of my plans for each type.

	When a body part suffers enough damage to get a wound, the severity (determined by a roll or something, worse damage leading to worse wounds), affected limb, and damage type sustained are factored into
	deciding what specific wound will be applied. I'd like to have a few different types of wounds for at least some of the choices, but I'm just doing rough generals for now. Expect polishing
*/

// tg: code/datums/wounds/_wounds.dm:19
[DmPath("/datum/wound")]
public partial class datum_wound : datum
{
    // tg: code/datums/wounds/_wounds.dm:17  #define WOUND_CRITICAL_BLUNT_DISMEMBER_BONUS 15 (#undef at :800)
    private const float WOUND_CRITICAL_BLUNT_DISMEMBER_BONUS = 15;

    /// List of states -> other states that override them and prevent them from being added by wounds
    // tg: code/datums/wounds/_wounds.dm:119 (var/static: rules 10.6, a TgState field; a list creates no datum, so tg's
    // world-start value and a creation at the first use are the same list)
    public static ref DmList exclusive_surgery_states => ref TgState.Current.datum_wound__exclusive_surgery_states;

    // tg: code/datums/wounds/_wounds.dm:126
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        unique_id = generate_unique_id();
        await update_actionspeed_modifier();
    }

    // tg: code/datums/wounds/_wounds.dm:132
    public override object Destroy(float? force = null)
    {
        if (limb != null)
            Waiting.NoSleep(remove_wound(destroying: QDELING(limb) ? TRUE : FALSE));

        QDEL_NULL(ref actionspeed_mod);

        return base.Destroy(force);
    }

    /// If we should have an actionspeed_mod, ensures we do and updates its slowdown. Otherwise, ensures we don't have one
    /// by qdeleting any existing modifier.
    // tg: code/datums/wounds/_wounds.dm:142
    public virtual async DmTask update_actionspeed_modifier()
    {
        if (Dm.truthy(should_have_actionspeed_modifier()))
        {
            if (actionspeed_mod == null)
                await generate_actionspeed_modifier();
            actionspeed_mod.multiplicative_slowdown = Dm.num(get_effective_actionspeed_modifier());
            victim?.update_actionspeed();
        }
        else
            remove_actionspeed_modifier();
    }

    /// Returns TRUE if we have an interaction_efficiency_penalty, and if we are on the arms, FALSE otherwise.
    // tg: code/datums/wounds/_wounds.dm:152
    public virtual object should_have_actionspeed_modifier()
    {
        return (limb != null && victim != null && (Dm.eq(limb.body_zone, BODY_ZONE_L_ARM) || Dm.eq(limb.body_zone, BODY_ZONE_R_ARM)) && interaction_efficiency_penalty != 0) ? TRUE : FALSE;
    }

    /// If we have no actionspeed_mod, generates a new one with our unique ID, sets actionspeed_mod to it, then returns it.
    // tg: code/datums/wounds/_wounds.dm:156
    public virtual async DmTask<object> generate_actionspeed_modifier()
    {
        if (actionspeed_mod != null)
            return actionspeed_mod;

        datum_actionspeed_modifier_wound_interaction_inefficiency new_modifier = await Dm.new_async<datum_actionspeed_modifier_wound_interaction_inefficiency>(unique_id, this);
        new_modifier.multiplicative_slowdown = Dm.num(get_effective_actionspeed_modifier());
        if (victim != null)
            await victim.add_actionspeed_modifier(new_modifier);

        actionspeed_mod = new_modifier;
        return actionspeed_mod;
    }

    /// If we have an actionspeed_mod, qdels it and sets our ref of it to null.
    // tg: code/datums/wounds/_wounds.dm:170
    public virtual void remove_actionspeed_modifier()
    {
        if (actionspeed_mod == null)
            return;

        victim?.remove_actionspeed_modifier(actionspeed_mod);
        QDEL_NULL(ref actionspeed_mod);
    }

    /// Generates the ID we use for [unique_id], which is also set as our actionspeed mod's ID
    // tg: code/datums/wounds/_wounds.dm:178
    public virtual object generate_unique_id()
    {
        return REF(this); // unique, cannot change, a perfect id
    }

    /**
     * apply_wound() is used once a wound type is instantiated to assign it to a bodypart, and actually come into play.
     *
     *
     * Arguments:
     * * limb: The bodypart we're wounding, we don't care about the person, we can get them through the limb
     * * silent: Not actually necessary I don't think, was originally used for demoting wounds so they wouldn't make new messages, but I believe old_wound took over that, I may remove this shortly
     * * old_wound: If our new wound is a replacement for one of the same time (promotion or demotion), we can reference the old one just before it's removed to copy over necessary vars
     * * smited- If this is a smite, we don't care about this wound for stat tracking purposes (not yet implemented)
     * * attack_direction: For bloodsplatters, if relevant
     * * wound_source: The source of the wound, such as a weapon.
     */
    // tg: code/datums/wounds/_wounds.dm:193
    public virtual async DmTask<object> apply_wound(obj_item_bodypart limb = null, float? silent = null, datum_wound old_wound = null, float? smited = null, object attack_direction = null, object wound_source = null, float? replacing = null, params object[] __extra)
    {
        silent ??= FALSE;
        smited ??= FALSE;
        wound_source ??= "Unknown";
        replacing ??= FALSE;

        if (!Dm.truthy(limb.is_woundable()) || !Dm.truthy(can_be_applied_to(limb, old_wound)))
        {
            qdel(this);
            return FALSE;
        }

        if (isatom(wound_source))
        {
            atom wound_atom = (atom)wound_source;
            this.wound_source = wound_atom.name;
        }
        else if (istext(wound_source))
            this.wound_source = wound_source;
        else
            this.wound_source = "Unknown";

        await set_victim(limb.owner);
        await set_limb(limb, replacing);
        LAZYADD(ref victim.all_wounds, this);
        LAZYADD(ref limb.wounds, this);
        update_descriptions();
        limb.update_wounds();
        if (status_effect_type != null)
            await victim.apply_status_effect(status_effect_type, this);
        // SEND_SIGNAL(victim, COMSIG_CARBON_GAIN_WOUND, src, limb) (rules 11.1 (e), a statement)
        if (Dm.truthy(victim._listen_lookup?[COMSIG_CARBON_GAIN_WOUND]))
            victim._SendSignal(COMSIG_CARBON_GAIN_WOUND, Dm.list(victim, this, limb));
        victim.update_health_hud();

        object demoted = null;
        if (old_wound != null)
            demoted = (severity <= old_wound.severity) ? TRUE : FALSE;

        if (severity == WOUND_SEVERITY_TRIVIAL)
            return null;

        if (!Dm.truthy(silent) && !Dm.truthy(demoted))
        {
            string msg = span_danger(I18n.T("[]'s [] []!", victim, limb.plaintext_zone, occur_text));
            float vis_dist = COMBAT_MESSAGE_RANGE;

            if (severity > WOUND_SEVERITY_SEVERE)
            {
                msg = I18n.T("<b>[]</b>", msg);
                vis_dist = DEFAULT_MESSAGE_RANGE;
            }

            victim.visible_message(msg, span_userdanger(I18n.T("Your [] []!", limb.plaintext_zone, occur_text)), vision_distance: vis_dist);
            if (Dm.truthy(sound_effect))
                playsound(limb.owner, sound_effect, sound_volume + (20 * severity), TRUE, falloff_exponent: SOUND_FALLOFF_EXPONENT + 2, ignore_walls: FALSE, falloff_distance: 0);
        }

        await wound_injury(old_wound, attack_direction: attack_direction);
        if (!Dm.truthy(demoted))
            await second_wind();

        return TRUE;
    }

    /// Returns TRUE if we can be applied to the limb.
    // tg: code/datums/wounds/_wounds.dm:243
    public virtual object can_be_applied_to(obj_item_bodypart limb = null, datum_wound old_wound = null)
    {
        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[type];

        // We assume we aren't being randomly applied - we have no reason to believe we are
        // And, besides, if we were, you could just as easily check our pregen data rather than run this proc
        // Generally speaking this proc is called in apply_wound, which is called when the caller is already confidant in its ability to be applied
        return pregen_data.can_be_applied_to(limb, old_wound: old_wound);
    }

    /// Returns the zones we can be applied to.
    // tg: code/datums/wounds/_wounds.dm:252
    public virtual object get_viable_zones()
    {
        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[type];

        return pregen_data.viable_zones;
    }

    /// Returns the biostate we require to be applied.
    // tg: code/datums/wounds/_wounds.dm:258
    public virtual object get_required_biostate()
    {
        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[type];

        return pregen_data.required_limb_biostate;
    }

    // Updates descriptive texts for the wound, in case it can get altered for whatever reason
    // tg: code/datums/wounds/_wounds.dm:264
    public virtual void update_descriptions()
    {
        return;
    }

    // tg: code/datums/wounds/_wounds.dm:267
    [SignalHandler]
    public virtual void null_victim()
    {
        Waiting.NoSleep(set_victim(null));
    }

    /// Setter for [victim]. Should completely transfer signals, attributes, etc. To the new victim - if there is any, as it can be null.
    // tg: code/datums/wounds/_wounds.dm:272
    public virtual async DmTask set_victim(object new_victim = null)
    {
        if (victim != null)
        {
            UnregisterSignal(victim, Dm.list(COMSIG_QDELETING, COMSIG_MOB_SWAP_HANDS, COMSIG_CARBON_POST_REMOVE_LIMB, COMSIG_CARBON_POST_ATTACH_LIMB));
            UnregisterSignal(victim, COMSIG_QDELETING);
            UnregisterSignal(victim, COMSIG_MOB_SWAP_HANDS);
            UnregisterSignal(victim, COMSIG_CARBON_POST_REMOVE_LIMB);
            UnregisterSignal(victim, COMSIG_ATOM_ITEM_INTERACTION);
            if (actionspeed_mod != null)
                victim.remove_actionspeed_modifier(actionspeed_mod); // no need to qdelete it, just remove it from our victim
        }

        remove_wound_from_victim();
        victim = new_victim as mob_living_carbon;
        if (victim != null)
        {
            RegisterSignal(victim, COMSIG_QDELETING, nameof(null_victim));
            RegisterSignals(victim, Dm.list(COMSIG_MOB_SWAP_HANDS, COMSIG_CARBON_POST_REMOVE_LIMB, COMSIG_CARBON_POST_ATTACH_LIMB), nameof(add_or_remove_actionspeed_mod));
            RegisterSignal(victim, COMSIG_ATOM_ITEM_INTERACTION, nameof(interact_try_treating));
            if (limb != null)
            {
                await start_limping_if_we_should(); // the status effect already handles removing itself
                await add_or_remove_actionspeed_mod();
            }
        }
    }

    /// Proc called to change the variable `limb` and react to the event.
    /// * replaced - Is the wound being overriden by another (stronger) wound?
    /// * destroying - Is this coming from a limb's Destroy() call? If so, cut down on updates we cause
    // tg: code/datums/wounds/_wounds.dm:295
    public virtual async DmTask<object> set_limb(obj_item_bodypart new_value = null, float? replaced = null, float? destroying = null)
    {
        replaced ??= FALSE;
        destroying ??= FALSE;

        object dot = null;
        if (limb == new_value)
            return FALSE; //Limb can either be a reference to something or `null`. Returning the number variable makes it clear no change was made.

        dot = limb;

        if (limb != null) // if we're nulling limb, we're basically detaching from it, so we should remove ourselves in that case
        {
            UnregisterSignal(limb, COMSIG_BODYPART_UPDATING_SURGERY_STATE);
            LAZYREMOVE(ref limb.wounds, this);
            if (!Dm.truthy(destroying))
                limb.update_wounds(replaced);
            if (Dm.truthy(disabling))
                limb.remove_traits(Dm.list(TRAIT_PARALYSIS, TRAIT_DISABLED_BY_WOUND), REF(this));
            if (Dm.truthy(surgery_states))
                await limb.remove_surgical_state(surgery_states);
        }

        limb = new_value;

        // POST-CHANGE

        if (limb != null)
        {
            RegisterSignal(limb, COMSIG_QDELETING, nameof(source_died));
            if (Dm.truthy(disabling))
                limb.add_traits(Dm.list(TRAIT_PARALYSIS, TRAIT_DISABLED_BY_WOUND), REF(this));

            exclusive_surgery_states ??= Dm.list(
                Dm.kv(Dm.text("[]", SURGERY_SKIN_CUT), SURGERY_SKIN_OPEN),
                Dm.kv(Dm.text("[]", SURGERY_VESSELS_UNCLAMPED), SURGERY_VESSELS_CLAMPED),
                Dm.kv(Dm.text("[]", SURGERY_BONE_SAWED), SURGERY_BONE_DRILLED),
                Dm.kv(Dm.text("[]", SURGERY_BONE_DRILLED), SURGERY_BONE_SAWED));
            if (Dm.truthy(surgery_states)) // first check filters invalid states
            {
                foreach (object state in Dm.each(exclusive_surgery_states))
                {
                    if (!Dm.truthy(Dm.bitand(limb.surgery_state, exclusive_surgery_states[state])))
                        continue;
                    object actual_state = text2num(state);
                    if (Dm.truthy(Dm.bitand(actual_state, surgery_states)))
                        surgery_states = Dm.num(Dm.bitand(surgery_states, Dm.bitnot(actual_state)));
                }
            }

            if (Dm.truthy(surgery_states)) // second check applies any remaining valid states
            {
                await limb.add_surgical_state(surgery_states);
                // NB: don't check state changes until AFTER we finish apply our own, or we'll just undo ourselves
                RegisterSignal(limb, COMSIG_BODYPART_UPDATING_SURGERY_STATE, nameof(on_surgery_state_change));
            }

            if (victim != null)
            {
                await start_limping_if_we_should(); // the status effect already handles removing itself
                await add_or_remove_actionspeed_mod();
            }

            update_inefficiencies(replaced);
        }
        return dot;
    }

    /// Used to remove states applied or removed by operations from ourselves as to not remove them if we heal mid-surgery
    // tg: code/datums/wounds/_wounds.dm:340
    [SignalHandler]
    public virtual void on_surgery_state_change(datum source = null, object old_state = null, object current_state = null, object changed_states = null)
    {
        // Any state that changes, adding or removing, should henceforth be untouched by us. Let the surgeon handle it.
        surgery_states = Dm.num(Dm.bitand(surgery_states, Dm.bitnot(changed_states)));
    }

    // tg: code/datums/wounds/_wounds.dm:345
    public virtual async DmTask add_or_remove_actionspeed_mod()
    {
        await update_actionspeed_modifier();
        if (actionspeed_mod != null)
        {
            if (victim.get_active_hand() == limb)
                await victim.add_actionspeed_modifier(actionspeed_mod, TRUE);
            else
                victim.remove_actionspeed_modifier(actionspeed_mod);
        }
    }

    // tg: code/datums/wounds/_wounds.dm:353
    public virtual async DmTask start_limping_if_we_should()
    {
        if ((Dm.eq(limb.body_zone, BODY_ZONE_L_LEG) || Dm.eq(limb.body_zone, BODY_ZONE_R_LEG)) && Dm.num(limp_slowdown) > 0 && Dm.num(limp_chance) > 0)
            await victim.apply_status_effect(typeof(datum_status_effect_limp));
    }

    // tg: code/datums/wounds/_wounds.dm:357
    [SignalHandler]
    public virtual void source_died()
    {
        qdel(this);
    }

    /// Remove the wound from whatever it's afflicting, and cleans up whatever status effects it had or modifiers it had on interaction times.
    /// * ignore_limb - Used for detachments where we only want to forget the victim
    /// * replaced - If the wound is being replaced by another type
    /// * destroying - If we're being removed by a limb getting destroyed
    // tg: code/datums/wounds/_wounds.dm:365
    public virtual async DmTask remove_wound(object ignore_limb = null, float? replaced = null, float? destroying = null)
    {
        replaced ??= FALSE;
        destroying ??= FALSE;

        //TODO: have better way to tell if we're getting removed without replacement (full heal) scar stuff
        mob_living_carbon old_victim = victim;
        obj_item_bodypart old_limb = limb;

        await set_disabling(FALSE);
        if (limb != null && Dm.truthy(can_scar) && !Dm.truthy(already_scarred) && !Dm.truthy(replaced) && !Dm.truthy(destroying))
        {
            already_scarred = TRUE;
            datum_scar new_scar = Dm.@new<datum_scar>();
            new_scar.generate(limb, this);
        }

        remove_actionspeed_modifier();

        null_victim(); // we use the proc here because some behaviors may depend on changing victim to some new value

        if (limb != null && !Dm.truthy(ignore_limb))
            await set_limb(null, replaced, destroying); // since we're removing limb's ref to us, we should do the same
            // if you want to keep the ref, do it externally, there's no reason for us to remember it

        if (ismob(old_victim))
        {
            mob mob_victim = old_victim;
            // SEND_SIGNAL(mob_victim, COMSIG_CARBON_POST_LOSE_WOUND, src, old_limb, ignore_limb, replaced, destroying) (rules 11.1 (e))
            if (Dm.truthy(mob_victim._listen_lookup?[COMSIG_CARBON_POST_LOSE_WOUND]))
                mob_victim._SendSignal(COMSIG_CARBON_POST_LOSE_WOUND, Dm.list(mob_victim, this, old_limb, ignore_limb, replaced, destroying));
            if (!Dm.truthy(replaced) && limb == null)
                mob_victim.update_health_hud();
        }
    }

    // tg: code/datums/wounds/_wounds.dm:390
    public virtual void remove_wound_from_victim()
    {
        if (victim == null)
            return;
        LAZYREMOVE(ref victim.all_wounds, this);
        // SEND_SIGNAL(victim, COMSIG_CARBON_LOSE_WOUND, src, limb) (rules 11.1 (e), a statement)
        if (Dm.truthy(victim._listen_lookup?[COMSIG_CARBON_LOSE_WOUND]))
            victim._SendSignal(COMSIG_CARBON_LOSE_WOUND, Dm.list(victim, this, limb));
    }

    /**
     * replace_wound() is used when you want to replace the current wound with a new wound, presumably of the same category, just of a different severity (either up or down counts)
     *
     * Arguments:
     * * new_wound- The wound instance you want to replace this
     * * smited- If this is a smite, we don't care about this wound for stat tracking purposes (not yet implemented)
     */
    // tg: code/datums/wounds/_wounds.dm:403
    public virtual async DmTask<object> replace_wound(datum_wound new_wound = null, float? smited = null, object attack_direction = null)
    {
        smited ??= FALSE;

        object dot = null;
        already_scarred = TRUE;
        obj_item_bodypart cached_limb = limb; // remove_wound() nulls limb so we have to track it locally
        await remove_wound(replaced: TRUE);
        await new_wound.apply_wound(cached_limb, old_wound: this, smited: smited, attack_direction: attack_direction, wound_source: wound_source, replacing: TRUE);
        if (HAS_TRAIT(this, TRAIT_WOUND_SCANNED) && severity > new_wound.severity)
        {
            foreach (object trait_source in Dm.each(GET_TRAIT_SOURCES(this, TRAIT_WOUND_SCANNED) as DmList))
                ADD_TRAIT(new_wound, TRAIT_WOUND_SCANNED, trait_source);
        }
        dot = new_wound;
        qdel(this);
        return dot;
    }

    /// The immediate negative effects faced as a result of the wound
    // tg: code/datums/wounds/_wounds.dm:415
    public virtual async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        return;
    }

    /// Proc called to change the variable `disabling` and react to the event.
    // tg: code/datums/wounds/_wounds.dm:419
    public virtual async DmTask<object> set_disabling(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(disabling, new_value))
            return null;
        dot = disabling;
        disabling = new_value;
        if (Dm.truthy(disabling))
        {
            if (!Dm.truthy(dot) && limb != null) //Gained disabling.
                limb.add_traits(Dm.list(TRAIT_PARALYSIS, TRAIT_DISABLED_BY_WOUND), REF(this));
        }
        else if (Dm.truthy(dot) && limb != null) //Lost disabling.
            limb.remove_traits(Dm.list(TRAIT_PARALYSIS, TRAIT_DISABLED_BY_WOUND), REF(this));
        if (Dm.truthy(limb?.can_be_disabled))
            await limb.update_disabled();
        return dot;
    }

    /// Setter for [interaction_efficiency_penalty]. Updates the actionspeed of our actionspeed mod.
    // tg: code/datums/wounds/_wounds.dm:433
    public virtual async DmTask set_interaction_efficiency_penalty(object new_value = null)
    {
        bool should_update = (Dm.num(new_value) != interaction_efficiency_penalty);

        interaction_efficiency_penalty = Dm.num(new_value);

        if (should_update)
            await update_actionspeed_modifier();
    }

    /// Returns a "adjusted" interaction_efficiency_penalty that will be used for the actionspeed mod.
    // tg: code/datums/wounds/_wounds.dm:442
    public virtual object get_effective_actionspeed_modifier()
    {
        return interaction_efficiency_penalty - 1;
    }

    /// Returns the decisecond multiplier of any click interactions, assuming our limb is being used.
    // tg: code/datums/wounds/_wounds.dm:446
    public virtual object get_action_delay_mult()
    {
        return interaction_efficiency_penalty;
    }

    /// Returns the decisecond increment of any click interactions, assuming our limb is being used.
    // tg: code/datums/wounds/_wounds.dm:452
    public virtual object get_action_delay_increment()
    {
        return 0;
    }

    /// Updates our limping and interaction penalties in accordance with our gauze.
    // tg: code/datums/wounds/_wounds.dm:458
    public virtual void update_inefficiencies(float? replaced_or_replacing = null)
    {
        replaced_or_replacing ??= FALSE;

        if (Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)))
        {
            float splint_factor = Dm.num(limb.get_splint_factor());
            if (Dm.@in(limb.body_zone, Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG)))
            {
                limp_slowdown = Dm.num(Dm.initial(this, (datum_wound p) => p.limp_slowdown)) * splint_factor;
                limp_chance = Dm.num(Dm.initial(this, (datum_wound p) => p.limp_chance)) * splint_factor;
            }
            else if (Dm.@in(limb.body_zone, GLOB.arm_zones))
            {
                if (splint_factor < 1)
                    Waiting.NoSleep(set_interaction_efficiency_penalty(1 + (Dm.num(get_effective_actionspeed_modifier()) * splint_factor)));
                else
                    Waiting.NoSleep(set_interaction_efficiency_penalty(Dm.initial(this, (datum_wound p) => p.interaction_efficiency_penalty)));
            }

            if (Dm.truthy(Dm.initial(this, (datum_wound p) => p.disabling)))
                Waiting.NoSleep(set_disabling(!Dm.truthy(limb.is_splinted()) ? TRUE : FALSE));

            limb.update_wounds(replaced_or_replacing);
        }

        Waiting.NoSleep(start_limping_if_we_should());
    }

    /// Additional beneficial effects when the wound is gained, in case you want to give a temporary boost to allow the victim to try an escape or last stand
    // tg: code/datums/wounds/_wounds.dm:480
    public virtual async DmTask second_wind()
    {
        var blood = (datum_reagents)victim.reagents;
        if (Dm.eq(severity, WOUND_SEVERITY_MODERATE))
            await blood.add_reagent(typeof(datum_reagent_determination), WOUND_DETERMINATION_MODERATE);
        else if (Dm.eq(severity, WOUND_SEVERITY_SEVERE))
            await blood.add_reagent(typeof(datum_reagent_determination), WOUND_DETERMINATION_SEVERE);
        else if (Dm.eq(severity, WOUND_SEVERITY_CRITICAL))
            await blood.add_reagent(typeof(datum_reagent_determination), WOUND_DETERMINATION_CRITICAL);
        else if (Dm.eq(severity, WOUND_SEVERITY_LOSS))
            await blood.add_reagent(typeof(datum_reagent_determination), WOUND_DETERMINATION_LOSS);
    }

    // tg: code/datums/wounds/_wounds.dm:491
    [SignalHandler]
    public virtual object interact_try_treating(datum source = null, mob_living user = null, obj_item tool = null, params object[] __extra)
    {
        return try_treating(tool, user);
    }

    /**
     * Attempt to treat the wound with the given item/tool by the given user
     * This proc leads into [/datum/wound/proc/treat]
     *
     * You can specify what items or tools you want to be intercepted
     * with var/list/treatable_by and var/treatable_tool,
     * then if an item fulfills one of those requirements and our wound claims it first,
     * it goes over to treat() and treat_self().
     *
     * Arguments:
     * * I: The item we're trying to use
     * * user: The mob trying to use it on us
     */
    // tg: code/datums/wounds/_wounds.dm:509
    public virtual object try_treating(obj_item tool = null, mob_living user = null)
    {
        if (!Dm.eq(limb.body_zone, user.zone_selected))
            return NONE;

        if (Dm.truthy(user.combat_mode) && Dm.truthy(tool.force))
            return NONE;

        if (!Dm.truthy(item_can_treat(tool, user)))
            return NONE;

        // now that we've determined we have a valid attempt at treating,
        // we can stomp on their dreams if we're already interacting with the patient or if their part is obscured
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, victim)))
        {
            to_chat(user, span_warning(I18n.T("You're already interacting with []!", victim)));
            return ITEM_INTERACT_BLOCKING;
        }

        // next we check if the bodypart in actually accessible (not under thick clothing). We skip the species trait check since skellies
        // & such may need to use bone gel but may be wearing a space suit for..... whatever reason a skeleton would wear a space suit for
        if (ishuman(victim))
        {
            mob_living_carbon_human victim_human = (mob_living_carbon_human)victim;
            if (!Dm.truthy(victim_human.try_inject(user, injection_flags: Dm.bitor(INJECT_CHECK_IGNORE_SPECIES, INJECT_TRY_SHOW_ERROR_MESSAGE))))
                return ITEM_INTERACT_BLOCKING;
        }

        Waiting.Detach(treat(tool, user));
        return ITEM_INTERACT_SUCCESS;
    }

    /// Returns TRUE if the item can be used to treat our wounds. Hooks into treat() - only things that return TRUE here may be used there.
    // tg: code/datums/wounds/_wounds.dm:538
    public virtual object item_can_treat(obj_item potential_treater = null, mob user = null)
    {
        // check if we have a valid treatable tool
        if (Dm.@in(potential_treater.tool_behaviour, treatable_tools))
            return TRUE;
        // failing that, see if we're aggro grabbing them and if we have an item that works for aggro grabs only
        if ((user == victim || (user.pulling == victim && Dm.num(user.grab_state) >= GRAB_AGGRESSIVE)) && Dm.truthy(check_grab_treatments(potential_treater, user)))
            return TRUE;
        // failing THAT, we check if we have a generally allowed item
        if (Dm.truthy(is_type_in_list(potential_treater, treatable_by)))
            return TRUE;
        return FALSE;
    }

    /// Return TRUE if we have an item that can only be used while aggro grabbed
    /// (unhanded aggro grab treatments go in [/datum/wound/proc/try_handling]).
    /// Treatment is still is handled in [/datum/wound/proc/treat]
    // tg: code/datums/wounds/_wounds.dm:553
    public virtual object check_grab_treatments(obj_item tool = null, mob user = null)
    {
        return FALSE;
    }

    /// Like try_treating() but for unhanded interactions, used by joint dislocations for manual bodypart chiropractice for example.
    /// Ignores thick material checks since you can pop an arm into place through a thick suit unlike using sutures
    // tg: code/datums/wounds/_wounds.dm:558
    public virtual async DmTask<object> try_handling(mob_living user = null)
    {
        return FALSE;
    }

    /// Someone is using something that might be used for treating the wound on this limb
    // tg: code/datums/wounds/_wounds.dm:562
    public virtual async DmTask treat(obj_item tool = null, mob user = null)
    {
        return;
    }

    /// If var/processing is TRUE, this is run on each life tick
    // tg: code/datums/wounds/_wounds.dm:566
    public virtual async DmTask handle_process(object seconds_per_tick = null)
    {
        return;
    }

    /// For use in do_after callback checks
    // tg: code/datums/wounds/_wounds.dm:570
    public virtual object still_exists()
    {
        return (!QDELETED(this) && limb != null) ? TRUE : FALSE;
    }

    /// When our parent bodypart is hurt.
    // tg: code/datums/wounds/_wounds.dm:574
    public virtual async DmTask receive_damage(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object attack_direction = null, object damage_source = null)
    {
        return;
    }

    /// Called from cryoxadone and pyroxadone when they're proc'ing. Wounds will slowly be fixed separately from other methods when these are in effect. crappy name but eh
    // tg: code/datums/wounds/_wounds.dm:578
    public virtual async DmTask<object> on_xadone(object power = null)
    {
        cryo_progress = Dm.num(cryo_progress) + Dm.num(power);

        handle_xadone_progress();
        return null;
    }

    /// Does various actions based on [cryo_progress]. By default, qdeletes the wound past a certain threshold.
    // tg: code/datums/wounds/_wounds.dm:584
    public virtual void handle_xadone_progress()
    {
        if (Dm.num(cryo_progress) > Dm.num(get_xadone_progress_to_qdel()))
            qdel(this);
    }

    /// Returns the amount of [cryo_progress] we need to be qdeleted.
    // tg: code/datums/wounds/_wounds.dm:589
    public virtual object get_xadone_progress_to_qdel()
    {
        return base_xadone_progress_to_qdel * severity;
    }

    /// When synthflesh is applied to the victim, we call this. No sense in setting up an entire chem reaction system for wounds when we only care for a few chems. Probably will change in the future
    // tg: code/datums/wounds/_wounds.dm:595
    public virtual async DmTask on_synthflesh(object reac_volume = null)
    {
        return;
    }

    /// Called when the patient is undergoing stasis, so that having fully treated a wound doesn't make you sit there helplessly until you think to unbuckle them
    // tg: code/datums/wounds/_wounds.dm:599
    public virtual void on_stasis(object seconds_per_tick = null)
    {
        return;
    }

    /// Sets our blood flow
    // tg: code/datums/wounds/_wounds.dm:603
    public virtual async DmTask set_blood_flow(object set_to = null)
    {
        await adjust_blood_flow(Dm.num(set_to) - Dm.num(blood_flow));
    }

    /// Use this to modify blood flow. You must use this to change the variable
    /// Takes the amount to adjust by, and the lowest amount we're allowed to have post adjust
    // tg: code/datums/wounds/_wounds.dm:608
    public virtual async DmTask adjust_blood_flow(object adjust_by = null, float? minimum = null)
    {
        minimum ??= 0;

        if (!Dm.truthy(adjust_by))
            return;
        object old_flow = blood_flow;
        blood_flow = max(Dm.num(blood_flow) + Dm.num(adjust_by), Dm.num(minimum));

        if (Dm.eq(old_flow, blood_flow))
            return;

        /// Update our bleed rate
        limb.refresh_bleed_rate();
    }

    /// Used when we're being dragged while bleeding, the value we return is how much bloodloss this wound causes from being dragged. Since it's a proc, you can let bandages soak some of the blood
    // tg: code/datums/wounds/_wounds.dm:621
    public virtual async DmTask<object> drag_bleed_amount()
    {
        return null;
    }

    /**
     * get_bleed_rate_of_change() is used in [/mob/living/carbon/proc/bleed_warn] to gauge whether this wound (if bleeding) is becoming worse, better, or staying the same over time
     *
     * Returns BLOOD_FLOW_STEADY if we're not bleeding or there's no change (like piercing), BLOOD_FLOW_DECREASING if we're clotting (non-critical slashes, gauzed, coagulant, etc), BLOOD_FLOW_INCREASING if we're opening up (crit slashes/heparin/nitrous oxide)
     */
    // tg: code/datums/wounds/_wounds.dm:629
    public virtual object get_bleed_rate_of_change()
    {
        if (Dm.truthy(blood_flow) && HAS_TRAIT(victim, TRAIT_BLOOD_FOUNTAIN))
            return BLOOD_FLOW_INCREASING;
        return BLOOD_FLOW_STEADY;
    }

    /**
     * get_examine_description() is used in carbon/examine and human/examine to show the status of this wound. Useful if you need to show some status like the wound being splinted or bandaged.
     *
     * Return the full string line you want to show, note that we're already dealing with the 'warning' span at this point, and that \n is already appended for you in the place this is called from
     *
     * Arguments:
     * * mob/user: The user examining the wound's owner, if that matters
     */
    // tg: code/datums/wounds/_wounds.dm:642
    public virtual object get_examine_description(mob user = null)
    {
        object dot = null;
        dot = get_wound_description(user);
        if (HAS_TRAIT(this, TRAIT_WOUND_SCANNED))
            dot = I18n.T("[][]", dot, span_notice(I18n.T("<br>There is a holo-image next to the wound that seems to contain indications for treatment.")));

        return dot;
    }

    // tg: code/datums/wounds/_wounds.dm:649
    public virtual object get_wound_description(mob user = null)
    {
        string desc;

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (Dm.truthy(Dm.bitand(wound_flags, ACCEPTS_GAUZE)) && current_gauze != null)
        {
            object sling_condition = get_gauze_condition();
            desc = I18n.T("[] [] is []fastened in a sling of []", victim.p_Their(), limb.plaintext_zone, sling_condition, current_gauze.name);
        }
        else
            desc = I18n.T("[] [] []", victim.p_Their(), limb.plaintext_zone, examine_desc);

        desc = Dm.text("[]", modify_desc_before_span(desc, user));

        return get_desc_intensity(desc);
    }

    /**
     * Used when a mob is examining themselves / their limbs
     *
     * Reports what this wound looks like to them
     *
     * It should be formatted as an extension of the limb:
     * Input is something like "Your chest is bruised.",
     * you would add something like "It is bleeding."
     *
     * * self_aware - if TRUE, the examiner is more aware of themselves and thus may get more detailed information
     *
     * Return a string, to be concatenated with other organ / limb status strings. Include spans and punctuation.
     */
    // tg: code/datums/wounds/_wounds.dm:676
    public virtual object get_self_check_description(object self_aware = null)
    {
        switch (severity)
        {
            case WOUND_SEVERITY_TRIVIAL:
                return span_danger(I18n.T("It's suffering [] [].", a_or_from, LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_MODERATE:
                return span_warning(I18n.T("It's suffering [] [].", a_or_from, LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_SEVERE:
                return span_boldwarning(I18n.T("It's suffering [] []!", a_or_from, LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
            case WOUND_SEVERITY_CRITICAL:
                return span_boldwarning(I18n.T("It's suffering [] []!!", a_or_from, LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name)));
        }
        return null;
    }

    /// A hook proc used to modify desc before it is spanned via [get_desc_intensity]. Useful for inserting spans yourself.
    // tg: code/datums/wounds/_wounds.dm:688
    public virtual object modify_desc_before_span(object desc = null, mob user = null)
    {
        return desc;
    }

    // tg: code/datums/wounds/_wounds.dm:691
    public virtual object get_gauze_condition()
    {
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze == null)
            return null;

        float sw__1 = Dm.num(current_gauze.absorption_capacity);
        if (0 <= sw__1 && sw__1 <= 1.25f)
            return I18n.T("just barely ");
        if (1.25f <= sw__1 && sw__1 <= 2.75f)
            return I18n.T("loosely ");
        if (2.75f <= sw__1 && sw__1 <= 4)
            return I18n.T("mostly ");
        if (4 <= sw__1)
            return I18n.T("tightly ");
        return null;
    }

    /// Spans [desc] based on our severity.
    // tg: code/datums/wounds/_wounds.dm:708
    public virtual object get_desc_intensity(object desc = null)
    {
        if (severity > WOUND_SEVERITY_MODERATE)
            return span_bold(I18n.T("[]!", desc));
        return I18n.T("[].", desc);
    }

    /**
     * Prints the details about the wound for the wound scanner on simple mode
     */
    // tg: code/datums/wounds/_wounds.dm:717
    public virtual object get_scanner_description(mob user = null)
    {
        return I18n.T("Type: []<br>Severity: []<br>Description: []<br>Recommended Treatment: []", name, severity_text(), desc, treat_text);
    }

    /**
     * Prints the details about the wound for the wound scanner on complex mode
     */
    // tg: code/datums/wounds/_wounds.dm:726
    public virtual object get_simple_scanner_description(mob user = null)
    {
        string severity_text_formatted = Dm.text("[]", severity_text());
        for (float i = 1; i <= severity; i++)
            severity_text_formatted += "!";

        string scanner_text = I18n.T("[] detected!<br>Risk: []<br>Description: []<br>", name, severity_text_formatted, Dm.truthy(simple_desc) ? simple_desc : desc);
        if (Dm.truthy(simple_treat_text))
            scanner_text += I18n.T("<i>Treatment Guide: []</i><br>", simple_treat_text);
        if (Dm.truthy(homemade_treat_text))
            scanner_text += I18n.T("<i>Homemade Remedies: []</i>", homemade_treat_text);

        return scanner_text;
    }

    /**
     * Returns what text describes this wound
     */
    // tg: code/datums/wounds/_wounds.dm:744
    public virtual object severity_text(params object[] __extra)
    {
        switch (severity)
        {
            case WOUND_SEVERITY_TRIVIAL:
                return I18n.T("Trivial");
            case WOUND_SEVERITY_MODERATE:
                return I18n.T("Moderate");
            case WOUND_SEVERITY_SEVERE:
                return I18n.T("<b>Severe</b>");
            case WOUND_SEVERITY_CRITICAL:
                return I18n.T("<b>Critical</b>");
        }
        return null;
    }

    /// Returns TRUE if our limb is the head or chest, FALSE otherwise.
    /// Essential in the sense of "we cannot live without it".
    // tg: code/datums/wounds/_wounds.dm:757
    public virtual object limb_essential()
    {
        return (Dm.eq(limb.body_zone, BODY_ZONE_HEAD) || Dm.eq(limb.body_zone, BODY_ZONE_CHEST)) ? TRUE : FALSE;
    }

    /// Getter proc for our scar_keyword, in case we might have some custom scar gen logic.
    // tg: code/datums/wounds/_wounds.dm:761
    public virtual object get_scar_keyword(obj_item_bodypart scarred_limb = null, object add_to_scars = null)
    {
        return scar_keyword;
    }

    /// Getter proc for our scar_file, in case we might have some custom scar gen logic.
    // tg: code/datums/wounds/_wounds.dm:765
    public virtual object get_scar_file(obj_item_bodypart scarred_limb = null, object add_to_scars = null)
    {
        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)get_pregen_data();
        // basically we iterate over biotypes until we find the one we want
        // fleshy burns will look for flesh then bone
        // dislocations will look for flesh, then bone, then metal
        object file = default_scar_file;
        foreach (object biotype in Dm.each(pregen_data.scar_priorities))
        {
            if (Dm.truthy(Dm.bitand(scarred_limb.biological_state, text2num(biotype))))
            {
                file = GLOB.biotypes_to_scar_file[biotype];
                break;
            }
        }

        return file;
    }

    /// Returns what string is displayed when a limb that has sustained this wound is examined
    /// (This is examining the LIMB ITSELF, when it's not attached to someone.)
    // tg: code/datums/wounds/_wounds.dm:780
    public virtual object get_limb_examine_description()
    {
        return null;
    }

    /// Gets the flat percentage chance increment of a dismember occurring, if a dismember is attempted (requires mangled flesh and bone). returning 15 = +15%.
    // tg: code/datums/wounds/_wounds.dm:784
    public virtual object get_dismember_chance_bonus(object existing_chance = null)
    {
        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)get_pregen_data();

        if (Dm.eq(pregen_data.required_wounding_type, WOUND_BLUNT) && severity >= WOUND_SEVERITY_CRITICAL)
            return WOUND_CRITICAL_BLUNT_DISMEMBER_BONUS; // we only require mangled bone (T2 blunt), but if there's a critical blunt, we'll add 15% more
        return null;
    }

    /// Returns our pregen data, which is practically guaranteed to exist, so this proc can safely be used raw.
    /// In fact, since it's RETURN_TYPEd to wound_pregen_data, you can even directly access the variables without having to store the value of this proc in a typed variable.
    /// Ex. get_pregen_data().wound_series
    // tg: code/datums/wounds/_wounds.dm:795
    public virtual object get_pregen_data()
    {
        return GLOB.all_wound_pregen_data[type];
    }
}

public sealed partial class TgState
{
    // tg: code/datums/wounds/_wounds.dm:119  var/static/list/exclusive_surgery_states
    internal DmList datum_wound__exclusive_surgery_states;
}
