// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/jaunt/_jaunt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// A basic subtype for jaunt related spells: the caster floats in a phased_mob holder.
// tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:15
public partial class datum_action_cooldown_spell_jaunt : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:23
    public override object get_caster_from_target(atom target = null)
    {
        if (istype(target.loc, jaunt_type))
            return target;

        return base.get_caster_from_target(target);
    }

    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:29
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(target._listen_lookup?[COMSIG_MOB_PRE_JAUNT]) ? NONE : target._SendSignal(COMSIG_MOB_PRE_JAUNT, Dm.list(target, target)), COMPONENT_BLOCK_JAUNT)))
            return FALSE;
        return await base.PreActivate(target);
    }

    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:34
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        return Dm.bitor(await base.before_cast(cast_on), SPELL_NO_FEEDBACK); // Don't do the feedback until after we're jaunting
    }

    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:37
    public override async DmTask<object> can_cast_spell(float? feedback = null)
    {
        feedback ??= TRUE;
        object dot = await base.can_cast_spell(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        area owner_area = get_area(owner) as area;
        turf owner_turf = get_turf(owner);
        if (owner_area == null || owner_turf == null)
            return FALSE; // nullspaced?

        if (!Dm.truthy(check_teleport_valid(owner, owner_turf, TELEPORT_CHANNEL_MAGIC)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_danger("Some dull, universal force is stopping you from jaunting here."));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(owner_turf.turf_flags, NOJAUNT)))
        {
            if (Dm.truthy(feedback))
                to_chat(owner, span_danger("An otherwordly force is preventing you from jaunting here."));
            return FALSE;
        }

        return isliving(owner) ? TRUE : FALSE;
    }

    /// Places the jaunter in a jaunt holder mob; returns the holder.
    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:66
    public virtual async DmTask<object> enter_jaunt(mob_living jaunter = null, turf loc_override = null)
    {
        obj_effect_dummy_phased_mob jaunt = Dm.@new(jaunt_type, (object)loc_override ?? get_turf(jaunter), jaunter) as obj_effect_dummy_phased_mob;
        RegisterSignal(jaunt, COMSIG_MOB_EJECTED_FROM_JAUNT, nameof(on_jaunt_exited));
        check_flags = (int)Dm.num(check_flags) & ~(int)AB_CHECK_PHASED;
        jaunter.add_traits(Dm.list(TRAIT_MAGICALLY_PHASED, TRAIT_RUNECHAT_HIDDEN, TRAIT_WEATHER_IMMUNE), REF(this));
        // Don't do the feedback until we have runechat hidden.
        // Otherwise the text will follow the jaunt holder, which reveals where our caster is travelling.
        await spell_feedback(jaunter);

        // This needs to happen at the end, after all the traits and stuff is handled
        if (Dm.truthy(jaunter._listen_lookup?[COMSIG_MOB_ENTER_JAUNT]))
            jaunter._SendSignal(COMSIG_MOB_ENTER_JAUNT, Dm.list(jaunter, this, jaunt));
        return jaunt;
    }

    /// Ejects the unjaunter from jaunt; the jaunt object in turn should call on_jaunt_exited.
    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:90
    public virtual async DmTask<object> exit_jaunt(mob_living unjaunter = null, turf loc_override = null)
    {
        if (unjaunter.loc is not obj_effect_dummy_phased_mob jaunt)
            return FALSE;

        if (jaunt.jaunter != unjaunter)
            throw Dm.CRASH("Jaunt spell attempted to exit_jaunt with an invalid unjaunter, somehow.", "code/modules/spells/spell_types/jaunt/_jaunt.dm", 98);

        if (loc_override != null)
            await jaunt.forceMove(loc_override);
        await jaunt.eject_jaunter();
        return TRUE;
    }

    /// Called when a mob is ejected from the jaunt holder and goes back to normal.
    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:113
    [SignalHandler]
    public virtual void on_jaunt_exited(obj_effect_dummy_phased_mob jaunt = null, mob_living unjaunter = null)
    {
        check_flags = (int)Dm.num(check_flags) | (int)AB_CHECK_PHASED;
        unjaunter.remove_traits(Dm.list(TRAIT_MAGICALLY_PHASED, TRAIT_RUNECHAT_HIDDEN, TRAIT_WEATHER_IMMUNE), REF(this));
        // This needs to happen at the end, after all the traits and stuff is handled
        if (Dm.truthy(unjaunter._listen_lookup?[COMSIG_MOB_AFTER_EXIT_JAUNT]))
            unjaunter._SendSignal(COMSIG_MOB_AFTER_EXIT_JAUNT, Dm.list(unjaunter, this));
    }

    // tg: code/modules/spells/spell_types/jaunt/_jaunt.dm:120
    public override async DmTask Remove(mob removed_from = null)
    {
        await exit_jaunt(removed_from as mob_living);
        if (removed_from.loc is obj_effect_dummy_phased_mob jaunt) // In case you have made exit_jaunt conditional, as in mirror walk
            await jaunt.eject_jaunter();
        await base.Remove(removed_from);
    }
}
