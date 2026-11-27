// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/other_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the click chain of living mobs (M2 P4, scope 2f, Q8): the unarmed attack, the right-click chain, the attack
// procs of /atom and the interaction checks, and the carbon's can_unarmed_attack (:20, the M2 integration). Not
// translated: the overrides of the types M2 does not declare (the larva, the drone and the pAI, :228-295: their types come
// with their milestones). M4a P1: _try_interact and interact (:96, :133; their admin-ghost call is a rule-1.4 block,
// M10; M5 slice 3: the ui_interact call and /atom/ui_status :120, tgui) and the interaction guard of D132 at attack_hand and
// attack_hand_secondary (docs/ai/specs/interaction.md 7).

// tg: code/_onclick/other_mobs.dm:309 (the [DmPath] of /mob/dead/new_player, tg's /world/mob, which other_mobs.dm
// declares first in .dme order; rules 2.3)
[DmPath("/mob/dead/new_player")]
public partial class mob_dead_new_player : mob_dead
{
}

// tg: code/_onclick/other_mobs.dm:301
[DmPath("/mob/living/simple_animal/hostile")]
public partial class mob_living_simple_animal_hostile : mob_living_simple_animal
{
}

// tg: code/_onclick/other_mobs.dm:2
public partial class mob_living : mob
{
    /// Checks for RIGHT_CLICK in modifiers and runs resolve_right_click_attack if so. Returns TRUE if normal chain blocked.
    // tg: code/_onclick/other_mobs.dm:2
    public virtual async DmTask<object> right_click_attack_chain(atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return null;
        object secondary_result = await resolve_right_click_attack(target, modifiers);

        if (Dm.eq(secondary_result, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.eq(secondary_result, SECONDARY_ATTACK_CONTINUE_CHAIN))
            return TRUE;
        else if (!Dm.eq(secondary_result, SECONDARY_ATTACK_CALL_NORMAL))
            throw Dm.CRASH("resolve_right_click_attack (probably attack_hand_secondary) did not return a SECONDARY_ATTACK_* define.", "code/_onclick/other_mobs.dm", 10);
        return null;
    }

    /**
     * Checks if this mob is in a valid state to punch someone.
     *
     * (Potentially) gives feedback to the mob if they cannot.
     */
    // tg: code/_onclick/other_mobs.dm:17
    public virtual object can_unarmed_attack()
    {
        return Dm.num(!HAS_TRAIT(this, TRAIT_HANDS_BLOCKED));
    }
}

// tg: code/_onclick/other_mobs.dm:20
public partial class mob_living_carbon : mob_living
{
    // tg: code/_onclick/other_mobs.dm:20
    public override object can_unarmed_attack()
    {
        object dot = base.can_unarmed_attack();
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(has_active_hand())) //can't attack without a hand.
        {
            obj_item_bodypart check_arm = get_active_hand() as obj_item_bodypart;
            if (Dm.truthy(check_arm?.bodypart_disabled))
            {
                to_chat(this, span_warning(I18n.T("Your [] is in no condition to be used.", check_arm.name)));
                return FALSE;
            }

            to_chat(this, span_notice("You look at your arm and sigh."));
            return FALSE;
        }

        return TRUE;
    }
}

// tg: code/_onclick/other_mobs.dm:36
public partial class mob_living : mob
{
    // tg: code/_onclick/other_mobs.dm:36
    [DmParams("attack_target", "proximity_flag", "modifiers")]
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        // The sole reason for this signal needing to exist is making FotNS incompatible with Hulk.
        // Note that it is send before [proc/can_unarmed_attack] is called, keep this in mind.
        object sigreturn = (!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_EARLY_UNARMED_ATTACK]) ? (object)NONE : this._SendSignal(COMSIG_LIVING_EARLY_UNARMED_ATTACK, Dm.list(this, attack_target, proximity_flag, modifiers)));
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_SKIP_ATTACK)))
            return FALSE;

        if (!Dm.truthy(can_unarmed_attack()))
            return FALSE;

        sigreturn = (!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_UNARMED_ATTACK]) ? (object)NONE : this._SendSignal(COMSIG_LIVING_UNARMED_ATTACK, Dm.list(this, attack_target, proximity_flag, modifiers)));
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(sigreturn, COMPONENT_SKIP_ATTACK)))
            return FALSE;

        if (!Dm.truthy(await right_click_attack_chain(attack_target, modifiers)))
            await resolve_unarmed_attack(attack_target, modifiers);
        return TRUE;
    }
}

// tg: code/_onclick/other_mobs.dm:58
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/_onclick/other_mobs.dm:58
    [DmParams("attack_target", "proximity_flag", "modifiers")]
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        // Humans can always check themself regardless of having their hands blocked or w/e
        if (attack_target == this && !Dm.truthy(combat_mode) && HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
        {
            check_self_for_injuries();
            return TRUE;
        }

        return await base.UnarmedAttack(attack_target, proximity_flag, modifiers);
    }
}

// tg: code/_onclick/other_mobs.dm:66
public partial class mob_living_carbon : mob_living
{
    // tg: code/_onclick/other_mobs.dm:66
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        return await attack_target.attack_paw(this, modifiers);
    }
}

// tg: code/_onclick/other_mobs.dm:69
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/_onclick/other_mobs.dm:69
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ISADVANCEDTOOLUSER(this))
            return await base.resolve_unarmed_attack(attack_target, modifiers, __extra);

        return await attack_target.attack_hand(this, modifiers);
    }

    // tg: code/_onclick/other_mobs.dm:75
    public override async DmTask<object> resolve_right_click_attack(atom target = null, DmList modifiers = null)
    {
        return await target.attack_hand_secondary(this, modifiers);
    }
}

// tg: code/_onclick/other_mobs.dm:79
public partial class atom : datum
{
    /// Return TRUE to cancel other attack hand effects that respect it. Modifiers is the assoc list for click info such as if it was a right click.
    // tg: code/_onclick/other_mobs.dm:79
    public virtual async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132, M4a P1) on the target before tg's first line: null is "not handled".
        if (InertSeam.MissingOverride(this, "attack_hand", user))
            return null;
        object dot = null;
        dot = FALSE;
        if (!Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_NO_FINGERPRINT_ATTACK_HAND)))
            add_fingerprint(user); // tg: code/_onclick/other_mobs.dm:82
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_HAND]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACK_HAND, Dm.list(this, user, modifiers))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            dot = TRUE;
        if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_ATTACK_HAND)))
            dot = await _try_interact(user);
        return dot;
    }

    /// When the user uses their hand on an item while holding right-click
    /// Returns a SECONDARY_ATTACK_* value.
    // tg: code/_onclick/other_mobs.dm:90
    public virtual async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        // DEVIATION(DV27): the interaction guard (D132, M4a P1): the chain stops as tg's cancel does.
        if (InertSeam.MissingOverride(this, "attack_hand_secondary", user))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_HAND_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACK_HAND_SECONDARY, Dm.list(this, user, modifiers))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    //Return a non FALSE value to cancel whatever called this from propagating, if it respects it.
    // tg: code/_onclick/other_mobs.dm:96
    public virtual async DmTask<object> _try_interact(mob user = null)
    {
        // tg: :97  if(isAdminGhostAI(user)) //admin abuse
        // GAP(lead): isAdminGhostAI (__DEFINES/is_helpers.dm: isAdminObserver(M) && M.client.AI_Interact) needs
        // check_rights_for, not declared; an admin observer is a ghost with an admin holder, which none has before M10.
        if (isobserver(user) && Dm.truthy(user.client?.holder))
        {
            Dm.NotTranslated("code/_onclick/other_mobs.dm:97", "_try_interact: isAdminGhostAI(user), the admin ghost's interaction (admin rights, M10+)");
            return null;
        }
        if (Dm.truthy(await can_interact(user)))
            return await interact(user);
        return FALSE;
    }

    // tg: code/_onclick/other_mobs.dm:103
    public virtual async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        require_adjacent_turf ??= TRUE;
        if (!Dm.truthy(await user.can_interact_with(this, Dm.bitand(interaction_flags_atom, INTERACT_ATOM_ALLOW_USER_LOCATION))))
            return FALSE;
        if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_REQUIRES_DEXTERITY)) && !ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_IGNORE_INCAPACITATED)))
        {
            float ignore_flags = NONE;
            if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_IGNORE_RESTRAINED)))
                Dm.combine(ref ignore_flags, INCAPABLE_RESTRAINTS);
            if (!Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_CHECK_GRAB)))
                Dm.combine(ref ignore_flags, INCAPABLE_GRAB);

            if (Dm.truthy(INCAPACITATED_IGNORING(user, ignore_flags)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/_onclick/other_mobs.dm:133
    public virtual async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_NO_FINGERPRINT_INTERACT)))
            add_hiddenprint(user); // tg: code/_onclick/other_mobs.dm:135
        else
            add_fingerprint(user); // tg: code/_onclick/other_mobs.dm:137
        if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_UI_INTERACT)))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UI_INTERACT]))
                this._SendSignal(COMSIG_ATOM_UI_INTERACT, Dm.list(this, user));
            return await ui_interact(user); // M5 slice 3: the tgui window (D148, protocol 16)
        }
        return FALSE;
    }

    // tg: code/_onclick/other_mobs.dm:120
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        //Check if both user and atom are at the same location
        if (!Dm.truthy(await can_interact(user)))
            dot = Dm.min(dot, UI_UPDATE);
        return dot;
    }
}

// tg: code/_onclick/other_mobs.dm:126
public partial class atom_movable : atom
{
    // tg: code/_onclick/other_mobs.dm:126
    [DmParams("user")]
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        object dot = null;
        dot = await base.can_interact(user, require_adjacent_turf);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(anchored) && Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_REQUIRES_ANCHORED)))
            return FALSE;
        return dot;
    }
}

// tg: code/_onclick/other_mobs.dm:144
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/_onclick/other_mobs.dm:144
    public override async DmTask<object> RangedAttack(atom A = null, object modifiers = null)
    {
        object dot = null;
        dot = await base.RangedAttack(A, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (isturf(A) && get_dist(this, A) <= 1)
        {
            await Move_Pulled(A);
            return TRUE;
        }
        return dot;
    }
}

/*
	Animals & All Unspecified
*/

// tg: code/_onclick/other_mobs.dm:161
public partial class mob_living : mob
{
    /**
     * Called when the unarmed attack hasn't been stopped by the LIVING_UNARMED_ATTACK_BLOCKED macro or the right_click_attack_chain proc.
     * This will call an attack proc that can vary from mob type to mob type on the target.
     */
    // tg: code/_onclick/other_mobs.dm:161
    public virtual async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        await attack_target.attack_animal(this, modifiers);
        return null;
    }

    /**
     * Called when an unarmed attack performed with right click hasn't been stopped by the LIVING_UNARMED_ATTACK_BLOCKED macro.
     * This will call a secondary attack proc that can vary from mob type to mob type on the target.
     * Sometimes, a target is interacted differently when right_clicked, in that case the secondary attack proc should return
     * a SECONDARY_ATTACK_* value that's not SECONDARY_ATTACK_CALL_NORMAL.
     * Otherwise, it should just return SECONDARY_ATTACK_CALL_NORMAL. Failure to do so will result in an exception (runtime error).
     */
    // tg: code/_onclick/other_mobs.dm:171
    public virtual async DmTask<object> resolve_right_click_attack(atom target = null, DmList modifiers = null)
    {
        return await target.attack_animal_secondary(this, modifiers);
    }
}

// tg: code/_onclick/other_mobs.dm:177
public partial class atom : datum
{
    /**
     * Called when a simple animal is unarmed attacking / clicking on this atom.
     */
    // tg: code/_onclick/other_mobs.dm:177
    public virtual async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_ANIMAL]))
            this._SendSignal(COMSIG_ATOM_ATTACK_ANIMAL, Dm.list(this, user, modifiers));
        return null;
    }

    /**
     * Called when a simple animal or basic mob right clicks an atom.
     * Returns a SECONDARY_ATTACK_* value.
     */
    // tg: code/_onclick/other_mobs.dm:184
    public virtual async DmTask<object> attack_animal_secondary(mob user = null, DmList modifiers = null)
    {
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    ///When a basic mob attacks something, either by AI or user.
    // tg: code/_onclick/other_mobs.dm:188
    [ShouldCallParent]
    public virtual async DmTask<object> attack_basic_mob(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_BASIC_MOB]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACK_BASIC_MOB, Dm.list(this, user))), COMSIG_BASIC_ATTACK_CANCEL_CHAIN)))
            return FALSE;
        return await handle_basic_attack(user, modifiers); //return value of attack animal, this is how much damage was dealt to the attacked thing
    }

    ///This exists so stuff can override the default call of attack_animal for attack_basic_mob
    ///Remove this when simple animals are removed and everything can be handled on attack basic mob.
    // tg: code/_onclick/other_mobs.dm:196
    public virtual async DmTask<object> handle_basic_attack(object user = null, object modifiers = null)
    {
        return await attack_animal(user as mob, modifiers as DmList);
    }

    ///Attacked by monkey. It doesn't need its own *_secondary proc as it just uses attack_hand_secondary instead.
    // tg: code/_onclick/other_mobs.dm:200
    public virtual async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_PAW]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_ATTACK_PAW, Dm.list(this, user, modifiers))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(interaction_flags_atom, INTERACT_ATOM_ATTACK_PAW)))
            dot = await _try_interact(user);
        return dot;
    }
}

/*
	Aliens
	Defaults to same as monkey in most places
*/

// tg: code/_onclick/other_mobs.dm:211
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/_onclick/other_mobs.dm:211
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        await attack_target.attack_alien(this, modifiers);
        return null;
    }

    // tg: code/_onclick/other_mobs.dm:214
    public override async DmTask<object> resolve_right_click_attack(atom target = null, DmList modifiers = null)
    {
        return await target.attack_alien_secondary(this, modifiers);
    }
}

// tg: code/_onclick/other_mobs.dm:217
public partial class atom : datum
{
    // tg: code/_onclick/other_mobs.dm:217
    public virtual async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        return await attack_paw(user, modifiers);
    }

    /**
     * Called when an alien right clicks an atom.
     * Returns a SECONDARY_ATTACK_* value.
     */
    // tg: code/_onclick/other_mobs.dm:224
    public virtual async DmTask<object> attack_alien_secondary(mob_living_carbon_alien user = null, DmList modifiers = null)
    {
        return SECONDARY_ATTACK_CALL_NORMAL;
    }
}

// Babby aliens
// tg: code/_onclick/other_mobs.dm:228 (gaps X5)
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/_onclick/other_mobs.dm:228
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        await attack_target.attack_larva(this, modifiers);
        return null;
    }

    // tg: code/_onclick/other_mobs.dm:231
    public override async DmTask<object> resolve_right_click_attack(atom target = null, DmList modifiers = null)
    {
        return await target.attack_larva_secondary(this, modifiers);
    }

    // tg: code/_onclick/other_mobs.dm:234
    public override object can_unarmed_attack() //We bite stuff, and our head is always free.
    {
        return TRUE;
    }
}

// tg: code/_onclick/other_mobs.dm:237 (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/_onclick/other_mobs.dm:237
    public virtual async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ATTACK_LARVA]))
            this._SendSignal(COMSIG_ATOM_ATTACK_LARVA, Dm.list(this, user, modifiers));
        return null;
    }

    /**
     * Called when an alien larva right clicks an atom.
     * Returns a SECONDARY_ATTACK_* value.
     */
    // tg: code/_onclick/other_mobs.dm:244
    public virtual async DmTask<object> attack_larva_secondary(mob user = null, DmList modifiers = null)
    {
        return SECONDARY_ATTACK_CALL_NORMAL;
    }

    /*
        Drones
    */

    // tg: code/_onclick/other_mobs.dm:251-255: the drone's overrides are not declared (/mob/living/basic/drone is a
    // type-only skeleton; drones, M10).

    /// Defaults to attack_hand. Override it when you don't want drones to do same stuff as humans.
    // tg: code/_onclick/other_mobs.dm:258
    public virtual async DmTask<object> attack_drone(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers);
        return null;
    }

    /**
     * Called when a maintenance drone right clicks an atom.
     * Defaults to attack_hand_secondary.
     * When overriding it, remember that it ought to return a SECONDARY_ATTACK_* value.
     */
    // tg: code/_onclick/other_mobs.dm:266
    public virtual async DmTask<object> attack_drone_secondary(mob_living_basic_drone user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    /*
        pAI
    */

    // tg: code/_onclick/other_mobs.dm:281-285: the pAI's overrides are at the end of the file (silicons SI6).

    // tg: code/_onclick/other_mobs.dm:287
    public virtual async DmTask<object> attack_pai(mob user = null, DmList modifiers = null)
    {
        return null;
    }

    /**
     * Called when a pAI right clicks an atom.
     * Returns a SECONDARY_ATTACK_* value.
     */
    // tg: code/_onclick/other_mobs.dm:294
    public virtual async DmTask<object> attack_pai_secondary(mob user = null, DmList modifiers = null)
    {
        return SECONDARY_ATTACK_CALL_NORMAL;
    }
}

/*
	Brain
*/

// tg: code/_onclick/other_mobs.dm:273
public partial class mob_living_brain : mob_living
{
    // tg: code/_onclick/other_mobs.dm:273
    [DmParams("attack_target", "proximity_flag", "modifiers")]
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null) //Stops runtimes due to attack_animal being the default
    {
        return null;
    }
}

/*
	Hostile animals
*/

// tg: code/_onclick/other_mobs.dm:301
public partial class mob_living_simple_animal_hostile : mob_living_simple_animal
{
    // tg: code/_onclick/other_mobs.dm:301
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        GiveTarget(attack_target);
        Waiting.Detach(AttackingTarget(attack_target)); // INVOKE_ASYNC
        return null;
    }
}

/*
	New Players:
	Have no reason to click on anything at all.
*/

// tg: code/_onclick/other_mobs.dm:309
public partial class mob_dead_new_player : mob_dead
{
    // tg: code/_onclick/other_mobs.dm:309
    [DmParams()]
    public override async DmTask ClickOn(atom A = null, object @params = null)
    {
        return;
    }
}

// tg: code/_onclick/other_mobs.dm:211
[DmPath("/mob/living/carbon/alien")]
public partial class mob_living_carbon_alien : mob_living_carbon
{
}

// tg: code/_onclick/other_mobs.dm:273
[DmPath("/mob/living/brain")]
public partial class mob_living_brain : mob_living
{
}

// tg: code/_onclick/other_mobs.dm:281  (silicons SI6, D326)
public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/_onclick/other_mobs.dm:281
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        await attack_target.attack_pai(this, modifiers);
        return null;
    }

    // tg: code/_onclick/other_mobs.dm:284
    public override async DmTask<object> resolve_right_click_attack(atom target = null, DmList modifiers = null)
    {
        return await target.attack_pai_secondary(this, modifiers);
    }
}
