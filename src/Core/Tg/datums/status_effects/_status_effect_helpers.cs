// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/_status_effect_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Status effect helpers for living mobs

// tg: code/datums/status_effects/_status_effect_helpers.dm:13
public partial class mob_living : mob
{
    /**
     * Applies a given status effect to this mob.
     *
     * new_effect - TYPEPATH of a status effect to apply.
     * Additional status effect arguments can be passed.
     *
     * Returns the instance of the created effected, if successful.
     * Returns 'null' if unsuccessful.
     */
    // tg: code/datums/status_effects/_status_effect_helpers.dm:13
    public virtual async DmTask<object> apply_status_effect(Type new_effect = null, params object[] __extra)
    {
        // DEVIATION(DV258): the dev godmode and invulnerability take no status effect (the owner, 2026-11-02)
        if (DevCommands.RefusesStatusEffects(this))
            return null;
        DmList args = Dm.args(new_effect, __extra);
        DmList arguments = args.Copy();

        // If the status effect we're applying doesn't allow multiple effects, we need to handle it
        if (Dm.initial(new_effect, (datum_status_effect p) => p.status_type) != STATUS_EFFECT_MULTIPLE)
        {
            foreach (datum_status_effect existing_effect in Dm.each_cast<datum_status_effect>(status_effects))
            {
                if (!Dm.eq(existing_effect.id, Dm.initial(new_effect, (datum_status_effect p) => p.id)))
                    continue;

                float sw__1 = existing_effect.status_type;
                // Multiple are allowed, continue as normal. (Not normally reachable)
                if (sw__1 == STATUS_EFFECT_MULTIPLE)
                    break;
                // Only one is allowed of this type - early return
                else if (sw__1 == STATUS_EFFECT_UNIQUE)
                    return null;
                // Replace the existing instance (deletes it).
                else if (sw__1 == STATUS_EFFECT_REPLACE)
                    await existing_effect.be_replaced();
                // Refresh the existing type, then early return
                else if (sw__1 == STATUS_EFFECT_REFRESH)
                {
                    await Dm.Call(existing_effect, nameof(datum_status_effect.refresh), Dm.arglist(arguments));
                    return null;
                }
            }
        }

        // For the new effect the 1st argument is this mob
        arguments[1] = this;

        // Create the status effect with our mob + our arguments
        datum_status_effect new_instance = await Dm.new_async(new_effect, arguments) as datum_status_effect;
        if (!QDELETED(new_instance))
            return new_instance;
        return null;
    }

    /**
     * Removes all instances of a given status effect from this mob
     *
     * removed_effect - TYPEPATH of a status effect to remove.
     * Additional status effect arguments can be passed - these are passed into before_remove.
     *
     * Returns TRUE if at least one was removed.
     */
    // tg: code/datums/status_effects/_status_effect_helpers.dm:55
    public virtual object remove_status_effect(object removed_effect = null, params object[] __extra)
    {
        DmList args = Dm.args(removed_effect, __extra);
        DmList arguments = args.Copy(2);

        object dot = null;
        dot = FALSE;
        foreach (datum_status_effect existing_effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            if (Dm.eq(existing_effect.id, Dm.initial(removed_effect, "id")) && Dm.truthy(Waiting.NoSleep(Dm.Call(existing_effect, nameof(datum_status_effect.before_remove), Dm.arglist(arguments)))))
            {
                qdel(existing_effect);
                dot = TRUE;
            }
        }

        return dot;
    }

    ///Gets every status effect of an ID and returns all of them in a list, rather than the individual 'has_status_effect'
    // tg: code/datums/status_effects/_status_effect_helpers.dm:89
    public virtual object get_all_status_effect_of_id(object checked_effect = null)
    {
        DmList all_effects_of_type = Dm.list();
        foreach (datum_status_effect present_effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            if (Dm.eq(present_effect.id, Dm.initial(checked_effect, "id")))
                Dm.append(ref all_effects_of_type, present_effect);
        }

        return all_effects_of_type;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:73
public partial class mob : atom_movable
{
    /**
     * Checks if this mob has a status effect that shares the passed effect's ID
     *
     * checked_effect - TYPEPATH of a status effect to check for. Checks for its ID, not its typepath
     *
     * Returns an instance of a status effect, or NULL if none were found.
     */
    // tg: code/datums/status_effects/_status_effect_helpers.dm:73
    public virtual object has_status_effect(object checked_effect = null)
    {
        // Yes I'm being cringe and putting this on the mob level even though status effects only apply to the living level
        // There's quite a few places (namely examine and, bleh, cult code) where it's easier to not need to cast to living before checking
        // for an effect such as blindness
        return null;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:79
public partial class mob_living : mob
{
    // tg: code/datums/status_effects/_status_effect_helpers.dm:79
    public override object has_status_effect(object checked_effect = null)
    {
        foreach (datum_status_effect present_effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            if (Dm.eq(present_effect.id, Dm.initial(checked_effect, "id")))
                return present_effect;
        }

        return null;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:107
public partial class mob : atom_movable
{
    /**
     * Checks if this mob has a status effect that shares the passed effect's ID
     * and has the passed sources are in its list of sources (ONLY works for grouped efects!)
     *
     * checked_effect - TYPEPATH of a status effect to check for. Checks for its ID, not its typepath
     *
     * Returns an instance of a status effect, or NULL if none were found.
     */
    // tg: code/datums/status_effects/_status_effect_helpers.dm:107
    public virtual object has_status_effect_from_source(object checked_effect = null, object sources = null)
    {
        // See [/mob/proc/has_status_effect] for reason behind having this on the mob level
        return null;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:111
public partial class mob_living : mob
{
    // tg: code/datums/status_effects/_status_effect_helpers.dm:111
    public override object has_status_effect_from_source(object checked_effect = null, object sources = null)
    {
        if (!ispath(checked_effect))
            throw Dm.CRASH("has_status_effect_from_source passed with an improper status effect path.", "code/datums/status_effects/_status_effect_helpers.dm", 115);

        if (!islist(sources))
            sources = Dm.list(sources);

        foreach (datum_status_effect_grouped present_effect in Dm.each<datum_status_effect_grouped>(status_effects))
        {
            if (!Dm.eq(present_effect.id, Dm.initial((Type)checked_effect, (datum_status_effect_grouped p) => p.id)))
                continue;
            object matching_sources = Dm.bitand(present_effect.sources, sources);
            if (Dm.truthy(length(matching_sources)))
                return present_effect;
        }

        return null;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:136
public partial class mob : atom_movable
{
    /**
     * Returns a list of all status effects that share the passed effect type's ID
     *
     * checked_effect - TYPEPATH of a status effect to check for. Checks for its ID, not its typepath
     *
     * Returns a list
     */
    // tg: code/datums/status_effects/_status_effect_helpers.dm:136
    public virtual object has_status_effect_list(object checked_effect = null)
    {
        // See [/mob/proc/has_status_effect] for reason behind having this on the mob level
        return null;
    }
}

// tg: code/datums/status_effects/_status_effect_helpers.dm:140
public partial class mob_living : mob
{
    // tg: code/datums/status_effects/_status_effect_helpers.dm:140
    public override object has_status_effect_list(object checked_effect = null)
    {
        DmList effects_found = Dm.list();
        foreach (datum_status_effect present_effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            if (Dm.eq(present_effect.id, Dm.initial(checked_effect, "id")))
                Dm.append(ref effects_found, present_effect);
        }

        return effects_found;
    }
}
