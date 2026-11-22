// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/status_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): the file is translated except the quirks and personalities (their datums and SSquirks,
// SSpersonalities: M9), which keep Dm.NotTranslated with their milestone.

// tg: code/modules/mob/living/status_procs.dm:9
public partial class mob_living : mob
{
    /**
     * Checks if we have stun immunity. Godmode always passes this check.
     *
     * * check_flags - bitflag of status flags that must be set in order for the stun to succeed. Passing NONE will always return false.
     * * force_stun - whether we ignore stun immunity with the exception of godmode
     *
     * returns TRUE if stun immune, FALSE otherwise
     */
    // tg: code/modules/mob/living/status_procs.dm:10
    [ShouldCallParent]
    public virtual object check_stun_immunity(float? check_flags = null, float? force_stun = null)
    {
        check_flags ??= CANSTUN;
        force_stun ??= FALSE;

        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return TRUE;

        if (Dm.truthy(force_stun)) // Does not take priority over god mode? I guess
            return FALSE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_GENERIC_STUN_CHECK]) ? NONE : _SendSignal(COMSIG_LIVING_GENERIC_STUN_CHECK, Dm.list(this, check_flags, force_stun))), COMPONENT_NO_STUN)))
            return TRUE;

        if (HAS_TRAIT(this, TRAIT_STUNIMMUNE))
            return TRUE;

        // Do we have the correct flag set to allow this status?
        // This checks that ALL flags are set, not just one of them.
        if (Dm.eq(Dm.bitand(status_flags, check_flags), check_flags))
            return FALSE;

        return TRUE;
    }

    /* STUN */
    // tg: code/modules/mob/living/status_procs.dm:33
    public virtual object IsStun() //If we're stunned
    {
        return has_status_effect(typeof(datum_status_effect_incapacitating_stun));
    }

    // tg: code/modules/mob/living/status_procs.dm:36
    public virtual object AmountStun() //How many deciseconds remain in our stun
    {
        datum_status_effect_incapacitating_stun S = IsStun() as datum_status_effect_incapacitating_stun;
        if (Dm.truthy(S))
            return S.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:42
    public virtual async DmTask<object> Stun(object amount = null, float? ignore_canstun = null) //Can't go below remaining duration
    {
        ignore_canstun ??= FALSE;
        amount = Dm.mul(amount, GET_PHYSIOLOGY(this, PHYS_COEFF_STUN));
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_STUN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_STUN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_stun S = IsStun() as datum_status_effect_incapacitating_stun;
        if (Dm.truthy(S))
            S.duration = Dm.num_or_null(max(amount, S.duration));
        else if (Dm.gt(amount, 0))
            S = await apply_status_effect(typeof(datum_status_effect_incapacitating_stun), amount) as datum_status_effect_incapacitating_stun;
        return S;
    }

    // tg: code/modules/mob/living/status_procs.dm:55
    public virtual async DmTask<object> SetStun(object amount = null, float? ignore_canstun = null) //Sets remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_STUN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_STUN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_stun S = IsStun() as datum_status_effect_incapacitating_stun;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(S))
                qdel(S);
        }
        else
        {
            if (Dm.truthy(S))
                S.duration = Dm.num_or_null(amount);
            else
                S = await apply_status_effect(typeof(datum_status_effect_incapacitating_stun), amount) as datum_status_effect_incapacitating_stun;
        }
        return S;
    }

    // tg: code/modules/mob/living/status_procs.dm:71
    public virtual async DmTask<object> AdjustStun(object amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_STUN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_STUN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_stun S = IsStun() as datum_status_effect_incapacitating_stun;
        if (Dm.truthy(S))
            Dm.append(ref S.duration, amount);
        else if (Dm.gt(amount, 0))
            S = await apply_status_effect(typeof(datum_status_effect_incapacitating_stun), amount) as datum_status_effect_incapacitating_stun;
        return S;
    }

    /* KNOCKDOWN */
    // tg: code/modules/mob/living/status_procs.dm:84
    public virtual object IsKnockdown() //If we're knocked down
    {
        return has_status_effect(typeof(datum_status_effect_incapacitating_knockdown));
    }

    // tg: code/modules/mob/living/status_procs.dm:87
    public virtual object AmountKnockdown() //How many deciseconds remain in our knockdown
    {
        datum_status_effect_incapacitating_knockdown K = IsKnockdown() as datum_status_effect_incapacitating_knockdown;
        if (Dm.truthy(K))
            return K.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:93
    public virtual async DmTask<object> Knockdown(object amount = null, float? daze_amount = null, float? ignore_canstun = null) //Can't go below remaining duration
    {
        daze_amount ??= 0;
        ignore_canstun ??= FALSE;
        amount = Dm.mul(amount, Dm.mul(GET_PHYSIOLOGY(this, PHYS_COEFF_KNOCKDOWN), GET_PHYSIOLOGY(this, PHYS_COEFF_STUN)));
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_KNOCKDOWN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_KNOCKDOWN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANKNOCKDOWN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_knockdown K = IsKnockdown() as datum_status_effect_incapacitating_knockdown;
        if (Dm.truthy(K))
            K.duration = Dm.num_or_null(max(amount, K.duration));
        else if (Dm.gt(amount, 0))
            K = await apply_status_effect(typeof(datum_status_effect_incapacitating_knockdown), amount) as datum_status_effect_incapacitating_knockdown;
        if (Dm.num(daze_amount) > 0)
            await apply_status_effect(typeof(datum_status_effect_dazed), daze_amount);
        return K;
    }

    // tg: code/modules/mob/living/status_procs.dm:108
    public virtual async DmTask<object> SetKnockdown(object amount = null, float? ignore_canstun = null) //Sets remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_KNOCKDOWN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_KNOCKDOWN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANKNOCKDOWN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_knockdown K = IsKnockdown() as datum_status_effect_incapacitating_knockdown;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(K))
                qdel(K);
        }
        else
        {
            if (Dm.truthy(K))
                K.duration = Dm.num_or_null(amount);
            else
                K = await apply_status_effect(typeof(datum_status_effect_incapacitating_knockdown), amount) as datum_status_effect_incapacitating_knockdown;
        }
        return K;
    }

    // tg: code/modules/mob/living/status_procs.dm:124
    public virtual async DmTask<object> AdjustKnockdown(object amount = null, float? daze_amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        daze_amount ??= 0;
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_KNOCKDOWN]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_KNOCKDOWN, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANKNOCKDOWN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_knockdown K = IsKnockdown() as datum_status_effect_incapacitating_knockdown;
        if (Dm.truthy(K))
            Dm.append(ref K.duration, amount);
        else if (Dm.gt(amount, 0))
            K = await apply_status_effect(typeof(datum_status_effect_incapacitating_knockdown), amount) as datum_status_effect_incapacitating_knockdown;
        if (Dm.num(daze_amount) > 0)
            await apply_status_effect(typeof(datum_status_effect_dazed), daze_amount);
        return K;
    }

    /* IMMOBILIZED */
    // tg: code/modules/mob/living/status_procs.dm:139
    public virtual object IsImmobilized() //If we're immobilized
    {
        return has_status_effect(typeof(datum_status_effect_incapacitating_immobilized));
    }

    // tg: code/modules/mob/living/status_procs.dm:142
    public virtual object AmountImmobilized() //How many deciseconds remain in our Immobilized status effect
    {
        datum_status_effect_incapacitating_immobilized I = IsImmobilized() as datum_status_effect_incapacitating_immobilized;
        if (Dm.truthy(I))
            return I.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:148
    public virtual async DmTask<object> Immobilize(object amount = null, float? ignore_canstun = null) //Can't go below remaining duration
    {
        ignore_canstun ??= FALSE;
        amount = Dm.mul(amount, GET_PHYSIOLOGY(this, PHYS_COEFF_STUN));
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_IMMOBILIZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_IMMOBILIZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_immobilized I = IsImmobilized() as datum_status_effect_incapacitating_immobilized;
        if (Dm.truthy(I))
            I.duration = Dm.num_or_null(max(amount, I.duration));
        else if (Dm.gt(amount, 0))
            I = await apply_status_effect(typeof(datum_status_effect_incapacitating_immobilized), amount) as datum_status_effect_incapacitating_immobilized;
        return I;
    }

    // tg: code/modules/mob/living/status_procs.dm:161
    public virtual async DmTask<object> SetImmobilized(object amount = null, float? ignore_canstun = null) //Sets remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_IMMOBILIZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_IMMOBILIZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_immobilized I = IsImmobilized() as datum_status_effect_incapacitating_immobilized;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(I))
                qdel(I);
        }
        else
        {
            if (Dm.truthy(I))
                I.duration = Dm.num_or_null(amount);
            else
                I = await apply_status_effect(typeof(datum_status_effect_incapacitating_immobilized), amount) as datum_status_effect_incapacitating_immobilized;
        }
        return I;
    }

    // tg: code/modules/mob/living/status_procs.dm:177
    public virtual async DmTask<object> AdjustImmobilized(object amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_IMMOBILIZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_IMMOBILIZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_immobilized I = IsImmobilized() as datum_status_effect_incapacitating_immobilized;
        if (Dm.truthy(I))
            Dm.append(ref I.duration, amount);
        else if (Dm.gt(amount, 0))
            I = await apply_status_effect(typeof(datum_status_effect_incapacitating_immobilized), amount) as datum_status_effect_incapacitating_immobilized;
        return I;
    }

    /* PARALYZED */
    // tg: code/modules/mob/living/status_procs.dm:190
    public virtual object IsParalyzed(params object[] __extra) //If we're paralyzed
    {
        return has_status_effect(typeof(datum_status_effect_incapacitating_paralyzed));
    }

    // tg: code/modules/mob/living/status_procs.dm:193
    public virtual object AmountParalyzed() //How many deciseconds remain in our Paralyzed status effect
    {
        // DM passes FALSE; IsParalyzed declares no parameter, so it is dropped (rules 7.3).
        datum_status_effect_incapacitating_paralyzed P = IsParalyzed() as datum_status_effect_incapacitating_paralyzed;
        if (Dm.truthy(P))
            return P.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:199
    public virtual async DmTask<object> Paralyze(object amount = null, float? ignore_canstun = null) //Can't go below remaining duration
    {
        ignore_canstun ??= FALSE;
        amount = Dm.mul(amount, GET_PHYSIOLOGY(this, PHYS_COEFF_STUN));
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_PARALYZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_PARALYZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(Dm.bitor(CANSTUN, CANKNOCKDOWN), ignore_canstun))) // this requires both can stun and can knockdown
            return null;
        // DM passes FALSE; IsParalyzed declares no parameter, so it is dropped (rules 7.3).
        datum_status_effect_incapacitating_paralyzed P = IsParalyzed() as datum_status_effect_incapacitating_paralyzed;
        if (Dm.truthy(P))
            P.duration = Dm.num_or_null(max(amount, P.duration));
        else if (Dm.gt(amount, 0))
            P = await apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed), amount) as datum_status_effect_incapacitating_paralyzed;
        return P;
    }

    // tg: code/modules/mob/living/status_procs.dm:212
    public virtual async DmTask<object> SetParalyzed(object amount = null, float? ignore_canstun = null) //Sets remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_PARALYZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_PARALYZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(Dm.bitor(CANSTUN, CANKNOCKDOWN), ignore_canstun)))
            return null;
        // DM passes FALSE; IsParalyzed declares no parameter, so it is dropped (rules 7.3).
        datum_status_effect_incapacitating_paralyzed P = IsParalyzed() as datum_status_effect_incapacitating_paralyzed;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(P))
                qdel(P);
        }
        else
        {
            if (Dm.truthy(P))
                P.duration = Dm.num_or_null(amount);
            else
                P = await apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed), amount) as datum_status_effect_incapacitating_paralyzed;
        }
        return P;
    }

    // tg: code/modules/mob/living/status_procs.dm:228
    public virtual async DmTask<object> AdjustParalyzed(object amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_PARALYZE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_PARALYZE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(Dm.bitor(CANSTUN, CANKNOCKDOWN), ignore_canstun)))
            return null;
        // DM passes FALSE; IsParalyzed declares no parameter, so it is dropped (rules 7.3).
        datum_status_effect_incapacitating_paralyzed P = IsParalyzed() as datum_status_effect_incapacitating_paralyzed;
        if (Dm.truthy(P))
            Dm.append(ref P.duration, amount);
        else if (Dm.gt(amount, 0))
            P = await apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed), amount) as datum_status_effect_incapacitating_paralyzed;
        return P;
    }

    /* INCAPACITATED */


    /// Proc that returns the remaining duration of the status efect in deciseconds.
    // tg: code/modules/mob/living/status_procs.dm:244
    public virtual object amount_incapacitated()
    {
        datum_status_effect_incapacitating_incapacitated incapacitated_status_effect = has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)) as datum_status_effect_incapacitating_incapacitated;
        if (Dm.truthy(incapacitated_status_effect))
            return incapacitated_status_effect.duration;
        else
            return 0;
    }

    /** Proc that actually applies the status effect.
     * Applies the Incapacitated status effect to a mob/living.
     * * amount - Amount of time the status effect should be applied for, in deciseconds.
     * * ignore_canstun - If TRUE, the mob's resistance to stuns is ignored.
     */
    // tg: code/modules/mob/living/status_procs.dm:256
    public virtual async DmTask<object> incapacitate(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_INCAPACITATE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_INCAPACITATE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_incapacitated incapacitated_status_effect = has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)) as datum_status_effect_incapacitating_incapacitated;
        if (Dm.truthy(incapacitated_status_effect))
            incapacitated_status_effect.duration = Dm.num_or_null(max(amount, incapacitated_status_effect.duration));
        else if (Dm.gt(amount, 0))
            incapacitated_status_effect = await apply_status_effect(typeof(datum_status_effect_incapacitating_incapacitated), amount) as datum_status_effect_incapacitating_incapacitated;
        return incapacitated_status_effect;
    }

    /** Proc that set the incapacitated status effect's remaining duration to a certain time.
     * Checks if the mob has the status effect. If yes, it sets the duration to the amount passed in arguments. If not, applies the status effect
     * and sets the duration to the amount passed in arguments.
     * * amount - Amount of time the status effect should be set to, in deciseconds.
     * * ignore_canstun - If TRUE, the mob's resistance to stuns is ignored.
     */
    // tg: code/modules/mob/living/status_procs.dm:274
    public virtual async DmTask<object> set_incapacitated(object amount = null, float? ignore_canstun = null)
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_INCAPACITATE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_INCAPACITATE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_incapacitated incapacitated_status_effect = has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)) as datum_status_effect_incapacitating_incapacitated;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(incapacitated_status_effect))
                qdel(incapacitated_status_effect);
        }
        else
        {
            if (Dm.truthy(incapacitated_status_effect))
                incapacitated_status_effect.duration = Dm.num_or_null(amount);
            else
                incapacitated_status_effect = await apply_status_effect(typeof(datum_status_effect_incapacitating_incapacitated), amount) as datum_status_effect_incapacitating_incapacitated;
        }
        return incapacitated_status_effect;
    }

    /** Proc that adds duration to an incapacitated status effect.
     * Checks if the mob has the status effect. If yes, it adds the amount passed in arguments to the remaining duration. If not, applies the status effect
     * and sets the duration to the amount passed in arguments.
     * * amount - Amount of time the status effect should be set to, in deciseconds.
     * * ignore_canstun - If TRUE, the mob's resistance to stuns is ignored.
     */
    // tg: code/modules/mob/living/status_procs.dm:296
    public virtual async DmTask<object> adjust_incapacitated(object amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_INCAPACITATE]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_INCAPACITATE, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANSTUN, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_incapacitated incapacitated_status_effect = has_status_effect(typeof(datum_status_effect_incapacitating_incapacitated)) as datum_status_effect_incapacitating_incapacitated;
        if (Dm.truthy(incapacitated_status_effect))
            Dm.append(ref incapacitated_status_effect.duration, amount);
        else if (Dm.gt(amount, 0))
            incapacitated_status_effect = await apply_status_effect(typeof(datum_status_effect_incapacitating_incapacitated), amount) as datum_status_effect_incapacitating_incapacitated;
        return incapacitated_status_effect;
    }

    //Blanket
    // tg: code/modules/mob/living/status_procs.dm:309
    public virtual async DmTask AllImmobility(object amount = null)
    {
        await Paralyze(amount);
        await Knockdown(amount);
        await Stun(amount);
        await Immobilize(amount);
        await Unconscious(amount);
    }


    // tg: code/modules/mob/living/status_procs.dm:317
    public virtual async DmTask SetAllImmobility(object amount = null)
    {
        await SetParalyzed(amount);
        await SetKnockdown(amount);
        await SetStun(amount);
        await SetImmobilized(amount);
        await SetUnconscious(amount);
    }


    // tg: code/modules/mob/living/status_procs.dm:325
    public virtual async DmTask AdjustAllImmobility(object amount = null)
    {
        await AdjustParalyzed(amount);
        await AdjustKnockdown(amount);
        await AdjustStun(amount);
        await AdjustImmobilized(amount);
        await AdjustUnconscious(amount);
    }


    /* UNCONSCIOUS */
    // tg: code/modules/mob/living/status_procs.dm:334
    public virtual object IsUnconscious() //If we're unconscious
    {
        return has_status_effect(typeof(datum_status_effect_incapacitating_unconscious));
    }

    // tg: code/modules/mob/living/status_procs.dm:337
    public virtual object AmountUnconscious() //How many deciseconds remain in our unconsciousness
    {
        datum_status_effect_incapacitating_unconscious U = IsUnconscious() as datum_status_effect_incapacitating_unconscious;
        if (Dm.truthy(U))
            return U.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:343
    public virtual async DmTask<object> Unconscious(object amount = null, float? ignore_canstun = null) //Can't go below remaining duration
    {
        ignore_canstun ??= FALSE;
        amount = Dm.mul(amount, GET_PHYSIOLOGY(this, PHYS_COEFF_STUN));
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_UNCONSCIOUS]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_UNCONSCIOUS, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANUNCONSCIOUS, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_unconscious U = IsUnconscious() as datum_status_effect_incapacitating_unconscious;
        if (Dm.truthy(U))
            U.duration = Dm.num_or_null(max(amount, U.duration));
        else if (Dm.gt(amount, 0))
            U = await apply_status_effect(typeof(datum_status_effect_incapacitating_unconscious), amount) as datum_status_effect_incapacitating_unconscious;
        return U;
    }

    // tg: code/modules/mob/living/status_procs.dm:356
    public virtual async DmTask<object> SetUnconscious(object amount = null, float? ignore_canstun = null) //Sets remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_UNCONSCIOUS]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_UNCONSCIOUS, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANUNCONSCIOUS, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_unconscious U = IsUnconscious() as datum_status_effect_incapacitating_unconscious;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(U))
                qdel(U);
        }
        else if (Dm.truthy(U))
            U.duration = Dm.num_or_null(amount);
        else
            U = await apply_status_effect(typeof(datum_status_effect_incapacitating_unconscious), amount) as datum_status_effect_incapacitating_unconscious;
        return U;
    }

    // tg: code/modules/mob/living/status_procs.dm:371
    public virtual async DmTask<object> AdjustUnconscious(object amount = null, float? ignore_canstun = null) //Adds to remaining duration
    {
        ignore_canstun ??= FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_UNCONSCIOUS]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_UNCONSCIOUS, Dm.list(this, amount, ignore_canstun))), COMPONENT_NO_STUN)))
            return null;
        if (Dm.truthy(check_stun_immunity(CANUNCONSCIOUS, ignore_canstun)))
            return null;
        datum_status_effect_incapacitating_unconscious U = IsUnconscious() as datum_status_effect_incapacitating_unconscious;
        if (Dm.truthy(U))
            Dm.append(ref U.duration, amount);
        else if (Dm.gt(amount, 0))
            U = await apply_status_effect(typeof(datum_status_effect_incapacitating_unconscious), amount) as datum_status_effect_incapacitating_unconscious;
        return U;
    }

    /* SLEEPING */
    // tg: code/modules/mob/living/status_procs.dm:384
    public virtual object IsSleeping() //If we're asleep
    {
        if (!HAS_TRAIT(this, TRAIT_SLEEPIMMUNE))
            return has_status_effect(typeof(datum_status_effect_incapacitating_sleeping));
        return null;
    }

    // tg: code/modules/mob/living/status_procs.dm:388
    public virtual object AmountSleeping() //How many deciseconds remain in our sleep
    {
        datum_status_effect_incapacitating_sleeping S = IsSleeping() as datum_status_effect_incapacitating_sleeping;
        if (Dm.truthy(S))
            return S.duration;
        return 0;
    }

    // tg: code/modules/mob/living/status_procs.dm:394
    public virtual async DmTask<object> Sleeping(object amount = null) //Can't go below remaining duration
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_SLEEP]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_SLEEP, Dm.list(this, amount))), COMPONENT_NO_STUN)))
            return null;
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return null;
        datum_status_effect_incapacitating_sleeping S = IsSleeping() as datum_status_effect_incapacitating_sleeping;
        if (Dm.truthy(S))
            S.duration = Dm.num_or_null(max(amount, S.duration));
        else if (Dm.gt(amount, 0))
            S = await apply_status_effect(typeof(datum_status_effect_incapacitating_sleeping), amount) as datum_status_effect_incapacitating_sleeping;
        return S;
    }

    // tg: code/modules/mob/living/status_procs.dm:406
    public virtual async DmTask<object> SetSleeping(object amount = null) //Sets remaining duration
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_SLEEP]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_SLEEP, Dm.list(this, amount))), COMPONENT_NO_STUN)))
            return null;
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return null;
        datum_status_effect_incapacitating_sleeping S = IsSleeping() as datum_status_effect_incapacitating_sleeping;
        if (Dm.le(amount, 0))
        {
            if (Dm.truthy(S))
                qdel(S);
        }
        else if (Dm.truthy(S))
            S.duration = Dm.num_or_null(amount);
        else
            S = await apply_status_effect(typeof(datum_status_effect_incapacitating_sleeping), amount) as datum_status_effect_incapacitating_sleeping;
        return S;
    }

    // tg: code/modules/mob/living/status_procs.dm:421
    public virtual async DmTask<object> AdjustSleeping(object amount = null) //Adds to remaining duration
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_STATUS_SLEEP]) ? NONE : _SendSignal(COMSIG_LIVING_STATUS_SLEEP, Dm.list(this, amount))), COMPONENT_NO_STUN)))
            return null;
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return null;
        datum_status_effect_incapacitating_sleeping S = IsSleeping() as datum_status_effect_incapacitating_sleeping;
        if (Dm.truthy(S))
            Dm.append(ref S.duration, amount);
        else if (Dm.gt(amount, 0))
            S = await apply_status_effect(typeof(datum_status_effect_incapacitating_sleeping), amount) as datum_status_effect_incapacitating_sleeping;
        return S;
    }

    ///////////////////////// CLEAR STATUS /////////////////////////

    // tg: code/modules/mob/living/status_procs.dm:435
    public virtual async DmTask adjust_status_effects_on_shake_up()
    {
        await AdjustStun(-6 * SECONDS);
        await AdjustKnockdown(-6 * SECONDS);
        await AdjustUnconscious(-6 * SECONDS);
        await AdjustSleeping(-10 * SECONDS);
        await AdjustParalyzed(-6 * SECONDS);
        await AdjustImmobilized(-6 * SECONDS);
    }

    ///////////////////////////////// FROZEN /////////////////////////////////////

    /* FROZEN */
    // tg: code/modules/mob/living/status_procs.dm:446
    public virtual object IsFrozen()
    {
        // tg: :447  return has_status_effect(/datum/status_effect/freon)
        // GAP(lead): /datum/status_effect/freon (datums/status_effects/debuffs/freon.dm, cryogenics and freon, M4b) is not
        // declared, so no mob has it and the answer is null.
        return null;
    }

    // tg: code/modules/mob/living/status_procs.dm:458
    public virtual async DmTask<object> add_quirk(Type quirktype = null, client override_client = null, float? add_unique = null, float? announce = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:458", "add_quirk: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
        return default;
    }

    // tg: code/modules/mob/living/status_procs.dm:470
    public virtual object remove_quirk(object quirktype = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:470", "remove_quirk: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
        return default;
    }

    // tg: code/modules/mob/living/status_procs.dm:477
    public virtual object has_quirk(object quirktype = null)
    {
        // M9 slice B: the deathgasp asks it (no quirk is given yet, so the list is empty)
        foreach (datum_quirk quirk in Dm.each<datum_quirk>(quirks))
        {
            if (Dm.eq(quirk.type, quirktype))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/status_procs.dm:491
    public virtual object get_quirk(object quirktype = null)
    {
        // Gaps S1: an eater's food allergy asks it (no quirk is given yet, so the list is empty)
        foreach (datum_quirk quirk in Dm.each<datum_quirk>(quirks))
        {
            if (Dm.eq(quirk.type, quirktype))
                return quirk;
        }
        return null;
    }

    // tg: code/modules/mob/living/status_procs.dm:505
    public virtual object get_quirk_string(float? medical = null, float? category = null, float? from_scan = null)
    {
        medical ??= FALSE;
        category ??= CAT_QUIRK_ALL;
        from_scan ??= FALSE;
        DmList dat = Dm.list();
        foreach (datum_quirk candidate in Dm.each<datum_quirk>(quirks))
        {
            if (Dm.truthy(from_scan) && ((int)candidate.quirk_flags & QUIRK_HIDE_FROM_SCAN) != 0)
                continue;
            switch (category)
            {
                case CAT_QUIRK_MAJOR_DISABILITY:
                    if (candidate.value >= -4)
                        continue;
                    break;
                case CAT_QUIRK_MINOR_DISABILITY:
                    // ISINRANGE(candidate.value, -4, -1) (__DEFINES/maths.dm:89), expanded inline (rules 11.1 (e))
                    if (!(candidate.value >= -4 && candidate.value <= -1))
                        continue;
                    break;
                case CAT_QUIRK_NOTES:
                    if (candidate.value < 0)
                        continue;
                    break;
            }
            Dm.append(ref dat, Dm.truthy(medical) ? candidate.medical_record_text : candidate.name);
        }

        if (!Dm.truthy(length(dat)))
            return Dm.truthy(medical) ? "No issues have been declared." : "None";
        return Dm.truthy(medical) ? dat.Join("<br>") : dat.Join(", ");
    }

    // tg: code/modules/mob/living/status_procs.dm:526
    public virtual void cleanse_quirk_datums() //removes all trait datums
    {
        QDEL_LAZYLIST(ref quirks);
    }

    // tg: code/modules/mob/living/status_procs.dm:529
    public virtual async DmTask transfer_quirk_datums(mob_living to_mob = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:529", "transfer_quirk_datums: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
    }

    // tg: code/modules/mob/living/status_procs.dm:541
    public virtual async DmTask add_personality(object personality_type = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:541", "add_personality: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
    }

    // tg: code/modules/mob/living/status_procs.dm:546
    public virtual async DmTask add_personalities(DmList new_personalities = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:546", "add_personalities: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
    }

    // tg: code/modules/mob/living/status_procs.dm:551
    public virtual void remove_personality(object personality_type = null)
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:551", "remove_personality: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
    }

    /// Helper to clear all personalities from a mob
    // tg: code/modules/mob/living/status_procs.dm:556
    public virtual void clear_personalities()
    {
        foreach (object personality_type in Dm.each(personalities))
            remove_personality(personality_type);
    }

    // tg: code/modules/mob/living/status_procs.dm:561
    public virtual object get_parsonality_string()
    {
        Dm.NotTranslated("code/modules/mob/living/status_procs.dm:561", "get_parsonality_string: not in M2 (M8 husk/quirks, M10+ personalities/drunk)");
        return default;
    }

    // tg: code/modules/mob/living/status_procs.dm:568
    public virtual async DmTask<object> cure_husk(object source = null)
    {
        REMOVE_TRAIT(this, TRAIT_HUSK, source);
        if (HAS_TRAIT(this, TRAIT_HUSK))
            return FALSE;
        UnregisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_UNHUSKABLE));
        return TRUE;
    }

    // tg: code/modules/mob/living/status_procs.dm:575
    public virtual object become_husk(object source = null)
    {
        if (HAS_TRAIT(this, TRAIT_UNHUSKABLE))
            return FALSE;
        bool was_husk = HAS_TRAIT(this, TRAIT_HUSK);
        ADD_TRAIT(this, TRAIT_HUSK, source);
        if (!was_husk)
            RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_UNHUSKABLE), nameof(became_unhuskable));
        return TRUE;
    }

    /// Called when we become unhuskable while already husked
    // tg: code/modules/mob/living/status_procs.dm:585
    [SignalHandler]
    public virtual void became_unhuskable()
    {
        Waiting.NoSleep(cure_husk());
    }

    // tg: code/modules/mob/living/status_procs.dm:589
    public virtual void cure_fakedeath(object source = null)
    {
        remove_traits(Dm.list(TRAIT_FAKEDEATH, TRAIT_DEATHCOMA), source);
        if (stat != DEAD)
            station_timestamp_timeofdeath = null;
    }

    /// Induces fake death on a living mob.
    // tg: code/modules/mob/living/status_procs.dm:595
    public virtual async DmTask fakedeath(object source = null, float? silent = null)
    {
        silent ??= FALSE;
        if (stat != DEAD)
        {
            if (!Dm.truthy(silent))
            {
                // tg: :598  emote("deathgasp")
                await emote("deathgasp");
            }
            station_timestamp_timeofdeath = round_timestamp();
        }

        if (!HAS_TRAIT(this, TRAIT_FAKEDEATH) && !Dm.truthy(silent))
            await send_death_moodlets();
        add_traits(Dm.list(TRAIT_FAKEDEATH, TRAIT_DEATHCOMA), source);
    }

    ///Unignores all slowdowns that lack the IGNORE_NOSLOW flag.
    // tg: code/modules/mob/living/status_procs.dm:606
    public virtual void unignore_slowdown(object source = null)
    {
        REMOVE_TRAIT(this, TRAIT_IGNORESLOWDOWN, source);
        update_movespeed();
    }

    ///Ignores all slowdowns that lack the IGNORE_NOSLOW flag.
    // tg: code/modules/mob/living/status_procs.dm:611
    public virtual void ignore_slowdown(object source = null)
    {
        ADD_TRAIT(this, TRAIT_IGNORESLOWDOWN, source);
        update_movespeed();
    }

    ///Ignores specific slowdowns. Accepts a list of slowdowns.
    // tg: code/modules/mob/living/status_procs.dm:616
    public virtual void add_movespeed_mod_immunities(object source = null, object slowdown_type = null, float? update = null)
    {
        update ??= TRUE;
        if (islist(slowdown_type))
        {
            foreach (object listed_type__it in Dm.each(slowdown_type))
            {
                object listed_type = listed_type__it;
                if (ispath(listed_type))
                    listed_type = Dm.text("[]", listed_type); //Path2String
                LAZYADDASSOCLIST(ref movespeed_mod_immunities, listed_type, source);
            }
        }
        else
        {
            if (ispath(slowdown_type))
                slowdown_type = Dm.text("[]", slowdown_type); //Path2String
            LAZYADDASSOCLIST(ref movespeed_mod_immunities, slowdown_type, source);
        }
        if (Dm.truthy(update))
            update_movespeed();
    }

    ///Unignores specific slowdowns. Accepts a list of slowdowns.
    // tg: code/modules/mob/living/status_procs.dm:630
    public virtual void remove_movespeed_mod_immunities(object source = null, object slowdown_type = null, float? update = null)
    {
        update ??= TRUE;
        if (islist(slowdown_type))
        {
            foreach (object listed_type__it in Dm.each(slowdown_type))
            {
                object listed_type = listed_type__it;
                if (ispath(listed_type))
                    listed_type = Dm.text("[]", listed_type); //Path2String
                LAZYREMOVEASSOC(ref movespeed_mod_immunities, listed_type, source);
            }
        }
        else
        {
            if (ispath(slowdown_type))
                slowdown_type = Dm.text("[]", slowdown_type); //Path2String
            LAZYREMOVEASSOC(ref movespeed_mod_immunities, slowdown_type, source);
        }
        if (Dm.truthy(update))
            update_movespeed();
    }

    /**
     * Adjusts a timed status effect on the mob,taking into account any existing timed status effects.
     * This can be any status effect that takes into account "duration" with their initialize arguments.
     *
     * Positive durations will add deciseconds to the duration of existing status effects
     * or apply a new status effect of that duration to the mob.
     *
     * Negative durations will remove deciseconds from the duration of an existing version of the status effect,
     * removing the status effect entirely if the duration becomes less than zero (less than the current world time).
     *
     * duration - the duration, in deciseconds, to add or remove from the effect
     * effect - the type of status effect being adjusted on the mob
     * max_duration - optional - if set, positive durations will only be added UP TO the passed max duration
     */
    // tg: code/modules/mob/living/status_procs.dm:657
    public virtual async DmTask adjust_timed_status_effect(object duration = null, object effect = null, object max_duration = null)
    {
        if (!isnum(duration))
            throw Dm.CRASH(Dm.text("adjust_timed_status_effect: called with an invalid duration. (Got: [])", duration), "code/modules/mob/living/status_procs.dm", 659);

        if (!ispath(effect, typeof(datum_status_effect)))
            throw Dm.CRASH(Dm.text("adjust_timed_status_effect: called with an invalid effect type. (Got: [])", effect), "code/modules/mob/living/status_procs.dm", 662);

        // If we have a max duration set, we need to check our duration does not exceed it
        if (isnum(max_duration))
        {
            if (Dm.le(max_duration, 0))
                throw Dm.CRASH(Dm.text("adjust_timed_status_effect: Called with an invalid max_duration. (Got: [])", max_duration), "code/modules/mob/living/status_procs.dm", 667);

            if (Dm.ge(duration, max_duration))
                duration = max_duration;
        }

        datum_status_effect existing = has_status_effect(effect) as datum_status_effect;
        if (Dm.truthy(existing))
        {
            if (isnum(max_duration) && Dm.gt(duration, 0))
            {
                // Check the duration remaining on the existing status effect
                // If it's greater than / equal to our passed max duration, we don't need to do anything
                if (Dm.ge(existing.duration, max_duration))
                    return;

                // Otherwise, add duration up to the max (max_duration - remaining_duration),
                // or just add duration if it doesn't exceed our max at all
                Dm.append(ref existing.duration, min(Dm.sub(max_duration, existing.duration), duration));
            }

            else
                Dm.append(ref existing.duration, duration);

            // If the duration was decreased and is now less 0 seconds,
            // qdel it / clean up the status effect immediately
            // (rather than waiting for the process tick to handle it)
            if (Dm.num(existing.duration) <= 0)
                qdel(existing);
        }

        else if (Dm.gt(duration, 0))
            await apply_status_effect(effect as Type, duration);
    }

    /**
     * Sets a timed status effect of some kind on a mob to a specific value.
     * If only_if_higher is TRUE, it will only set the value up to the passed duration,
     * so any pre-existing status effects of the same type won't be reduced down
     *
     * duration - the duration, in deciseconds, of the effect. 0 or lower will either remove the current effect or do nothing if none are present
     * effect - the type of status effect given to the mob
     * only_if_higher - if TRUE, we will only set the effect to the new duration if the new duration is longer than any existing duration
     */
    // tg: code/modules/mob/living/status_procs.dm:705
    public virtual async DmTask set_timed_status_effect(object duration = null, object effect = null, float? only_if_higher = null)
    {
        only_if_higher ??= FALSE;
        if (!isnum(duration))
            throw Dm.CRASH(Dm.text("set_timed_status_effect: called with an invalid duration. (Got: [])", duration), "code/modules/mob/living/status_procs.dm", 707);

        if (!ispath(effect, typeof(datum_status_effect)))
            throw Dm.CRASH(Dm.text("set_timed_status_effect: called with an invalid effect type. (Got: [])", effect), "code/modules/mob/living/status_procs.dm", 710);

        datum_status_effect existing = has_status_effect(effect) as datum_status_effect;
        if (Dm.truthy(existing))
        {
            // set_timed_status_effect to 0 technically acts as a way to clear effects,
            // though remove_status_effect would achieve the same goal more explicitly.
            if (Dm.le(duration, 0))
            {
                qdel(existing);
                return;
            }

            if (Dm.truthy(only_if_higher))
            {
                // If the existing status effect has a higher remaining duration
                // than what we aim to set it to, don't downgrade it - do nothing (return)
                if (Dm.ge(existing.duration, duration))
                    return;
            }

            // Set the duration accordingly
            existing.duration = Dm.num_or_null(duration);
        }

        else if (Dm.gt(duration, 0))
            await apply_status_effect(effect as Type, duration);
    }

    /**
     * Gets how many deciseconds are remaining in
     * the duration of the passed status effect on this mob.
     *
     * If the mob is unaffected by the passed effect, returns 0.
     */
    // tg: code/modules/mob/living/status_procs.dm:738
    public virtual object get_timed_status_effect_duration(object effect = null)
    {
        if (!ispath(effect, typeof(datum_status_effect)))
            throw Dm.CRASH(Dm.text("get_timed_status_effect_duration: called with an invalid effect type. (Got: [])", effect), "code/modules/mob/living/status_procs.dm", 740);

        datum_status_effect existing = has_status_effect(effect) as datum_status_effect;
        if (!Dm.truthy(existing))
            return 0;
        // Infinite duration status effects technically are not "timed status effects"
        // by name or nature, but support is included just in case.
        if (Dm.eq(existing.duration, STATUS_EFFECT_PERMANENT))
            return INFINITY;

        return existing.duration;
    }

    /**
     * Adjust the "drunk value" the mob is currently experiencing,
     * or applies a drunk effect if the mob isn't currently drunk (or tipsy)
     *
     * The drunk effect doesn't have a set duration, like dizziness or drugginess,
     * but instead relies on a value that decreases every status effect tick (2 seconds) by:
     * 4% the current drunk_value + 0.01
     *
     * A "drunk value" of 6 is the border between "tipsy" and "drunk".
     *
     * amount - the amount of "drunkness" to apply to the mob.
     * down_to - the lower end of the clamp, when adding the value
     * up_to - the upper end of the clamp, when adding the value
     */
    // tg: code/modules/mob/living/status_procs.dm:766
    public virtual async DmTask adjust_drunk_effect(object amount = null, float? down_to = null, float? up_to = null)
    {
        down_to ??= 0;
        up_to ??= INFINITY;
        if (!isnum(amount))
            throw Dm.CRASH(Dm.text("adjust_drunk_effect: called with an invalid amount. (Got: [])", amount), "code/modules/mob/living/status_procs.dm", 768);

        datum_status_effect_inebriated inebriation = has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        if (inebriation != null)
            await inebriation.set_drunk_value(clamp(inebriation.drunk_value + Dm.num(amount), Dm.num(down_to), Dm.num(up_to)));
        else if (Dm.gt(amount, 0))
            await apply_status_effect(typeof(datum_status_effect_inebriated_tipsy), amount);
    }


    /**
     * Directly sets the "drunk value" the mob is currently experiencing to the passed value,
     * or applies a drunk effect with the passed value if the mob isn't currently drunk
     *
     * set_to - the amount of "drunkness" to set on the mob.
     */
    // tg: code/modules/mob/living/status_procs.dm:783
    public virtual async DmTask set_drunk_effect(object set_to = null)
    {
        if (!isnum(set_to) || Dm.lt(set_to, 0))
            throw Dm.CRASH(Dm.text("set_drunk_effect: called with an invalid value. (Got: [])", set_to), "code/modules/mob/living/status_procs.dm", 785);

        datum_status_effect_inebriated inebriation = has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        if (inebriation != null)
            await inebriation.set_drunk_value(set_to);
        else if (Dm.gt(set_to, 0))
            await apply_status_effect(typeof(datum_status_effect_inebriated_tipsy), set_to);
    }

    /// Helper to get the amount of drunkness the mob's currently experiencing.
    // tg: code/modules/mob/living/status_procs.dm:794
    public virtual object get_drunk_amount()
    {
        datum_status_effect_inebriated inebriation = has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        return Dm.truthy(inebriation?.drunk_value) ? inebriation.drunk_value : 0;
    }
}
