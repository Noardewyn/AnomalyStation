// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/honorbound/honorbound_trauma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the honorbound trauma (no attacks on the unready, the just or the innocent; the guilty list; the punishment for
// profane magic) and its Declare Evil spell. The var blocks are generated (D111).

///Honorbound prevents you from attacking the unready, the just, or the innocent
// tg: code/modules/religion/honorbound/honorbound_trauma.dm:5
public partial class datum_brain_trauma_special_honorbound : datum_brain_trauma_special
{
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:2  #define GUILT_REASON_DECLARATION "from your declaration." (file-local, #undef at :333)
    internal const string GUILT_REASON_DECLARATION = "from your declaration.";

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:19
    public override async DmTask<object> on_gain()
    {
        //moodlet
        await owner.add_mood_event("honorbound", typeof(datum_mood_event_honorbound));
        //checking spells cast by honorbound
        RegisterSignal(owner, COMSIG_MOB_CAST_SPELL, nameof(spell_check));
        RegisterSignal(owner, COMSIG_MOB_FIRED_GUN, nameof(staff_check));

        //adds the relay_attackers element to the owner so whoever attacks him becomes guilty.
        if (!HAS_TRAIT(owner, TRAIT_RELAYING_ATTACKER))
            await owner.AddElement(typeof(datum_element_relay_attackers));
        RegisterSignal(owner, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));

        //signal that checks for dishonorable attacks
        RegisterSignal(owner, COMSIG_LIVING_EARLY_UNARMED_ATTACK, nameof(unarmed_attack_honor));
        RegisterSignal(owner, COMSIG_MOB_ITEM_ATTACK, nameof(attack_honor));
        RegisterSignal(owner, COMSIG_MOB_TRYING_TO_FIRE_GUN, nameof(fire_gun_honor));
        datum_action_cooldown_spell_pointed_declare_evil declare = await Dm.new_async<datum_action_cooldown_spell_pointed_declare_evil>(owner);
        await declare.Grant(owner);
        return await base.on_gain();
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:39
    public override async DmTask on_lose(object silent = null)
    {
        owner.clear_mood_event("honorbound");
        UnregisterSignal(owner, Dm.list(
            COMSIG_LIVING_EARLY_UNARMED_ATTACK,
            COMSIG_MOB_ITEM_ATTACK,
            COMSIG_MOB_TRYING_TO_FIRE_GUN,
            COMSIG_MOB_CAST_SPELL,
            COMSIG_MOB_FIRED_GUN));
        await base.on_lose(silent);
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:50
    [SignalHandler]
    public virtual object unarmed_attack_honor(mob_living_carbon_human honorbound = null, atom target = null, object proximity_flag = null, object modifiers = null)
    {
        DmList modifiers_list = (DmList)modifiers;
        if (Dm.truthy(modifiers_list[ALT_CLICK]) || Dm.truthy(modifiers_list[SHIFT_CLICK]) || Dm.truthy(modifiers_list[CTRL_CLICK]) || Dm.truthy(modifiers_list[MIDDLE_CLICK]))
            return null;

        if (!Dm.truthy(proximity_flag) || !isliving(target))
            return NONE;

        mob_living punched_mob = target as mob_living;

        if (!Dm.@in(punched_mob, guilty))
            check_visible_guilt(punched_mob);

        if ((Dm.truthy(honorbound.combat_mode) || Dm.truthy(modifiers_list[RIGHT_CLICK])) && !Dm.truthy(is_honorable(honorbound, punched_mob)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        return null;
    }

    /// Signal to see if the trauma allows us to attack a target with a weapon
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:68
    [SignalHandler]
    public virtual object attack_honor(mob_living honorbound = null, atom attacked = null, DmList modifiers = null)
    {
        if (!isliving(attacked))
            return null;

        mob_living attacked_mob = attacked as mob_living;
        obj_item weapon = honorbound.get_active_held_item() as obj_item;

        if (Dm.truthy(Dm.bitand(weapon?.item_flags, NOBLUDGEON)))
            return null;

        if (!Dm.@in(attacked_mob, guilty))
            check_visible_guilt(attacked_mob);

        if ((Dm.truthy(Dm.bitand(weapon?.item_flags, NEEDS_PERMIT)) || Dm.truthy(honorbound.combat_mode) || Dm.num(weapon?.force) > 0) && !Dm.truthy(is_honorable(honorbound as mob_living_carbon_human, attacked_mob)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        return null;
    }

    /// Signal to see if we're targeting a mob that is guilty or not.
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:87
    [SignalHandler]
    public virtual object fire_gun_honor(mob_living honorbound = null, obj_item_gun the_gun_in_question = null, atom target = null, object flag = null, object @params = null)
    {
        if (the_gun_in_question is obj_item_gun_magic)
            return null;

        if (!isliving(target)) //No shooting an innocent you weren't aiming at to get around this. Aim true or go home, honor-dork
            return COMPONENT_CANCEL_GUN_FIRE;

        mob_living shot_mob = target as mob_living;

        if (!Dm.@in(shot_mob, guilty))
            check_visible_guilt(shot_mob);

        if (!Dm.truthy(is_honorable(honorbound as mob_living_carbon_human, shot_mob)))
            return COMPONENT_CANCEL_GUN_FIRE;
        return null;
    }

    /// Checks a mob for any obvious signs of evil, and applies a guilty reason for each.
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:105
    public virtual void check_visible_guilt(mob_living attacked_mob = null)
    {
        //will most likely just hit nuke ops but good catch-all. WON'T hit traitors
        if (Dm.truthy(attacked_mob.has_faction(ROLE_SYNDICATE)))
            guilty__proc(attacked_mob, I18n.T("for their misaligned association with the Syndicate!"));
        //not an antag datum check so it applies to wizard minions as well
        if (Dm.truthy(attacked_mob.has_faction(ROLE_WIZARD)))
            guilty__proc(attacked_mob, I18n.T("for blasphemous magicks!"));
        if (HAS_TRAIT(attacked_mob, TRAIT_CULT_HALO))
            guilty__proc(attacked_mob, I18n.T("for blasphemous worship!"));
        if (HAS_TRAIT(attacked_mob, TRAIT_EVIL))
            guilty__proc(attacked_mob, I18n.T("for an almost fanatical commitment to EEEEVIL!"));
        if (attacked_mob.mind != null)
        {
            datum_mind guilty_conscience = attacked_mob.mind;
            if (Dm.truthy(guilty_conscience.has_antag_datum(typeof(datum_antagonist_abductor))))
                guilty__proc(attacked_mob, I18n.T("for their blatant surgical malice..."));
            if (Dm.truthy(guilty_conscience.has_antag_datum(typeof(datum_antagonist_nightmare))))
                guilty__proc(attacked_mob, I18n.T("for being a light-consuming nightmare!"));
            if (Dm.truthy(guilty_conscience.has_antag_datum(typeof(datum_antagonist_ninja))))
                guilty__proc(attacked_mob, I18n.T("for their misaligned association with the Spider Clan!"));
            datum_antagonist_heretic heretic_datum = guilty_conscience.has_antag_datum(typeof(datum_antagonist_heretic)) as datum_antagonist_heretic;
            if (Dm.truthy(heretic_datum?.ascended))
                guilty__proc(attacked_mob, I18n.T("for blasphemous, heretical, out of control worship!"));
        }
    }

    /**
     * Called by hooked signals whenever someone attacks the person with this trauma
     * Checks if the attacker should be considered guilty and adds them to the guilty list if true
     *
     * Arguments:
     * * user: person who attacked the honorbound
     * * reason: why this person is now guilty (future pr idea: letting honorbound print a receipt for why someone is guilty? lol)
     */
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:136
    public virtual void guilty__proc(mob_living user = null, string reason = null)
    {
        reason ??= I18n.T("for no particular reason!");
        if (Dm.@in(user, guilty))
            return;
        datum_mind guilty_conscience = user.mind;
        if (guilty_conscience != null && reason != GUILT_REASON_DECLARATION) //sec and medical are immune to becoming guilty through attack (we don't check holy because holy shouldn't be able to attack eachother anyways)
        {
            datum_job job = guilty_conscience.assigned_role as datum_job;
            if (Dm.truthy(Dm.bitand(job.departments_bitflags, Dm.bitor(DEPARTMENT_BITFLAG_MEDICAL, DEPARTMENT_BITFLAG_SECURITY))))
                return;
        }
        to_chat(owner, span_notice(I18n.T("[] is now considered guilty by [] []", user, GLOB.deity, reason)));
        to_chat(user, span_danger(I18n.T("[] no longer considers you innocent!", GLOB.deity)));
        Dm.append(ref guilty, user);
    }

    ///Signal sent by the relay_attackers element. It makes the attacker guilty unless the damage was stamina or it was a shove.
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:149
    [SignalHandler]
    public virtual void on_attacked(mob source = null, mob attacker = null, object attack_flags = null)
    {
        if (!Dm.truthy(Dm.bitand(attack_flags, Dm.bitor(ATTACKER_STAMINA_ATTACK, ATTACKER_SHOVING))))
            guilty__proc(attacker as mob_living, I18n.T("for attacking [] first.", source));
    }

    /**
     * Called by attack_honor signal to check whether an attack should be allowed or not
     *
     * Arguments:
     * * honorbound_human: typecasted owner of the trauma
     * * target_creature: person honorbound_human is attacking
     */
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:161
    public virtual object is_honorable(mob_living_carbon_human honorbound_human = null, mob_living target_creature = null)
    {
        bool is_guilty = Dm.@in(target_creature, guilty);
        bool is_human = ishuman(target_creature);
        //THE UNREADY (Applies over ANYTHING else!)
        if (honorbound_human == target_creature)
            return TRUE; //oh come on now
        if (IS_UNCONSCIOUS(target_creature) || HAS_TRAIT(target_creature, TRAIT_RESTRAINED))
        {
            to_chat(honorbound_human, span_warning(I18n.T("There is no honor in attacking the <b>unready</b>.")));
            return FALSE;
        }
        //THE JUST (Applies over guilt except for med, so you best be careful!)
        if (is_human)
        {
            mob_living_carbon_human target_human = target_creature as mob_living_carbon_human;
            datum_job job = target_human.mind?.assigned_role as datum_job;
            object is_holy = target_human.mind?.holy_role;
            if (Dm.truthy(is_holy) || Dm.truthy(Dm.bitand(job?.departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
            {
                to_chat(honorbound_human, span_warning(I18n.T("There is nothing righteous in attacking the <b>just</b>.")));
                return FALSE;
            }
            if (Dm.truthy(Dm.bitand(job?.departments_bitflags, DEPARTMENT_BITFLAG_MEDICAL)) && !is_guilty)
            {
                to_chat(honorbound_human, span_warning(I18n.T("If you truly think this healer is not <b>innocent</b>, declare them guilty.")));
                return FALSE;
            }
        }
        //THE INNOCENT (human and borg exclusive)
        if (!is_guilty && (is_human || issilicon(target_creature)))
        {
            to_chat(target_creature, span_warning(I18n.T("There is nothing righteous in attacking the <b>innocent</b>.")));
            return FALSE;
        }
        return TRUE;
    }

    //spell checking
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:188
    [SignalHandler]
    public virtual void spell_check(mob user = null, datum_action_cooldown_spell spell_cast = null)
    {
        Waiting.NoSleep(punishment(user as mob_living_carbon_human, spell_cast.school));
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:192
    [SignalHandler]
    public virtual void staff_check(mob user = null, obj_item_gun gun_fired = null, object target = null, object @params = null, object zone_override = null, DmList bonus_spread_values = null)
    {
        if (!(gun_fired is obj_item_gun_magic))
            return;
        obj_item_gun_magic offending_staff = gun_fired as obj_item_gun_magic;
        Waiting.NoSleep(punishment(user as mob_living_carbon_human, offending_staff.school));
    }

    /**
     * Called when a spell is casted or a magic gun is fired, checks the signal and punishes accordingly
     *
     * Arguments:
     * * user: typecasted owner of trauma
     * * school: school of magic casted from the staff/spell
     */
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:206
    public virtual async DmTask punishment(mob_living_carbon_human user = null, object school = null)
    {
        object sw__1 = school;
        if (Dm.eq(sw__1, SCHOOL_HOLY) || Dm.eq(sw__1, SCHOOL_MIME) || Dm.eq(sw__1, SCHOOL_RESTORATION) || Dm.eq(sw__1, SCHOOL_PSYCHIC))
            return;
        else if (Dm.eq(sw__1, SCHOOL_NECROMANCY) || Dm.eq(sw__1, SCHOOL_FORBIDDEN) || Dm.eq(sw__1, SCHOOL_SANGUINE))
        {
            to_chat(user, span_userdanger(I18n.T("[] is enraged by your use of forbidden magic!", GLOB.deity)));
            await lightningbolt(user);
            user.mind.set_holy_role(NONE);
            qdel(this);
            await owner.add_mood_event("honorbound", typeof(datum_mood_event_banished)); //add mood event after we already cleared our events
            to_chat(user, span_userdanger(I18n.T("You have been excommunicated! You are no longer holy!")));
        }
        else
        {
            to_chat(user, span_userdanger(I18n.T("[] is angered by your use of [] magic!", GLOB.deity, Dm.eq(school, SCHOOL_UNSET) ? I18n.T("strange") : school)));
            await lightningbolt(user);
            await owner.add_mood_event("honorbound", typeof(datum_mood_event_holy_smite));//permanently lose your moodlet after this
        }
    }
}

// tg: code/modules/religion/honorbound/honorbound_trauma.dm:222
public partial class datum_action_cooldown_spell_pointed_declare_evil : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:245
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        declaration = I18n.T("By the divine light of [], you are an evil of this world that must be wrought low!", GLOB.deity);
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:249
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        mob grant_to = granted_to;
        if (!ishuman(grant_to))
            return FALSE;

        mob_living_carbon_human human_owner = grant_to as mob_living_carbon_human;
        datum_brain_trauma_special_honorbound honorbound = human_owner.has_trauma_type(typeof(datum_brain_trauma_special_honorbound)) as datum_brain_trauma_special_honorbound;
        if (QDELETED(honorbound))
            return FALSE;

        RegisterSignal(honorbound, COMSIG_QDELETING, nameof(on_honor_trauma_lost));
        honor_trauma = honorbound;
        return await base.Grant(granted_to, __extra);
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:262
    public override async DmTask Remove(mob removed_from = null)
    {
        await base.Remove(removed_from);
        UnregisterSignal(honor_trauma, COMSIG_QDELETING);
        honor_trauma = null;
    }

    /// If we lose our honor trauma somehow, self-delete (and clear references)
    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:268
    [SignalHandler]
    public virtual void on_honor_trauma_lost(datum source = null)
    {
        qdel(this);
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:273
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = null;
        dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (GLOB.religious_sect == null)
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("There are no deities around to approve your declaration!")));
            return FALSE;
        }

        if (GLOB.religious_sect.favor < required_favor)
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_warning(I18n.T("You need at least 150 favor to declare someone evil!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:290
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        object dot = null;
        dot = await base.is_valid_target(cast_on);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!isliving(cast_on))
        {
            to_chat(owner, span_warning(I18n.T("You can only declare living beings evil!")));
            return FALSE;
        }

        mob_living living_cast_on = cast_on as mob_living;
        if (Dm.eq(living_cast_on.stat, DEAD))
        {
            to_chat(owner, span_warning(I18n.T("Declaration on the dead? Really?")));
            return FALSE;
        }

        // sec and medical are immune to becoming guilty through attack
        // (we don't check holy, because holy shouldn't be able to attack eachother anyways)
        if (!Dm.truthy(living_cast_on.key) || living_cast_on.mind == null)
        {
            to_chat(owner, span_warning(I18n.T("There is no evil a vacant mind can do.")));
            return FALSE;
        }

        // also handles any kind of issues with self declarations
        if (Dm.truthy(living_cast_on.mind.holy_role))
        {
            to_chat(owner, span_warning(I18n.T("Followers of [] cannot be evil!", GLOB.deity)));
            return FALSE;
        }

        // cannot declare security as evil
        if (Dm.truthy(Dm.bitand(((datum_job)living_cast_on.mind.assigned_role).departments_bitflags, DEPARTMENT_BITFLAG_SECURITY)))
        {
            to_chat(owner, span_warning(I18n.T("Members of security are uncorruptable! You cannot declare one evil!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:321
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        invocation = I18n.T("[]! []", cast_on, declaration);
        return dot;
    }

    // tg: code/modules/religion/honorbound/honorbound_trauma.dm:328
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = null;
        dot = await base.cast(cast_on);
        GLOB.religious_sect.adjust_favor(-required_favor, owner as mob_living);
        honor_trauma.guilty__proc(cast_on as mob_living, datum_brain_trauma_special_honorbound.GUILT_REASON_DECLARATION);
        return dot;
    }
}
