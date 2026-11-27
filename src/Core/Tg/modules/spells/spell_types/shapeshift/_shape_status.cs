// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/shapeshift/_shape_status.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the shapechange status effect (the caster held in stasis inside the shape, and put back when the shape dies,
// is turned into another mob or the spell is cast again), its spell-sourced subtype with the damage carried over between the two
// bodies, and the shapeshifted alert that casts the spell back. The var blocks are generated (D111).

// A status effect for having a mob temporarily (usually) change form into that of another mob.
// When the status effect is removed, the mob is reverted back to their human form in most cases.
// If you want a more permanent polymorph, see [/proc/wabbajack].
// tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:5
public partial class datum_status_effect_shapechange_mob : datum_status_effect
{
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:16
    [DmParams("new_owner", "caster")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        mob_living caster = Dm.arg(__extra, 0) as mob_living;
        // If the mob is shapechanging while being deleted (for example, a bitrunning avatar dying and getting booted out)
        if (QDELETED(caster))
        {
            qdel(this);
            return null;
        }

        // If any type or subtype of shapeshift mob is on the new_owner already throw an error and self-delete
        if (Dm.locate_type((Type)type, new_owner.status_effects) != null)
        {
            stack_trace("Mob shapechange status effect applied to a mob which already was shapechanged, which will definitely cause issues.", "code/modules/spells/spell_types/shapeshift/_shape_status.dm", 24);
            qdel(this);
            return null;
        }

        // No caster mob, no one to put in the mob. Self-delete
        if (!(caster is mob_living))
        {
            stack_trace("Mob shapechange status effect applied without a proper caster.", "code/modules/spells/spell_types/shapeshift/_shape_status.dm", 30);
            qdel(this);
            return null;
        }

        caster_mob = caster;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:37
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (caster_mob.mind != null)
            await caster_mob.mind.transfer_to(owner);
        await caster_mob.forceMove(owner);
        ADD_TRAIT(caster_mob, TRAIT_NO_TRANSFORM, TRAIT_STATUS_EFFECT(id));
        await caster_mob.apply_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_SHAPECHANGE_EFFECT);

        RegisterSignal(owner, COMSIG_LIVING_PRE_WABBAJACKED, nameof(on_pre_wabbajack));
        RegisterSignal(owner, COMSIG_PRE_MOB_CHANGED_TYPE, nameof(on_pre_type_change));
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(on_shape_death));
        RegisterSignal(caster_mob, COMSIG_LIVING_DEATH, nameof(on_caster_death));
        RegisterSignal(caster_mob, COMSIG_QDELETING, nameof(on_caster_deleted));

        caster_mob.SEND_SIGNAL(COMSIG_LIVING_SHAPESHIFTED, Dm.list(caster_mob, owner));
        return TRUE;
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:52
    public override async DmTask<object> on_remove()
    {
        // If the owner was straight up deleted we shouldn't restore anyone
        // (the caster's stored in the owner's contents so if it's qdel'd so is the caster)
        if (!QDELETED(owner))
            await restore_caster();

        // Either restore_caster() or other signals should have handled this reference by now,
        // but juuust in case make sure nothing sticks around.
        caster_mob = null;
        return null;
    }

    /// Called when we're shot by the Wabbajack but before we change into a different mob
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:63
    [SignalHandler]
    public virtual object on_pre_wabbajack(mob_living source = null)
    {
        Waiting.NoSleep(on_mob_transformed(source));
        return STOP_WABBAJACK;
    }

    /// Called when we're turned into a different mob via the change_mob_type proc
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:69
    [SignalHandler]
    public virtual object on_pre_type_change(mob_living source = null)
    {
        Waiting.NoSleep(on_mob_transformed(source));
        return COMPONENT_BLOCK_MOB_CHANGE;
    }

    /// Called when the transformed mob tries to change into a different kind of mob, we wouldn't handle this well so we'll just turn back
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:75
    public virtual async DmTask on_mob_transformed(mob_living source = null)
    {
        mob_living revealed_mob = caster_mob;
        source.visible_message(span_warning(I18n.T("[] gets pulled back to their normal form!", revealed_mob)));
        await restore_caster();
        await revealed_mob.Paralyze(10 * SECONDS, ignore_canstun: TRUE);
    }

    /// Restores the caster back to their human form.
    /// if kill_caster_after is TRUE, the caster will have death() called on them after restoring.
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:83
    public virtual async DmTask restore_caster(float? kill_caster_after = null)
    {
        kill_caster_after ??= FALSE;
        if (Dm.truthy(already_restored) || caster_mob == null)
            return;

        if (QDELETED(owner))
            throw Dm.CRASH("Mob shapechange effect called restore_caster while the owner was qdeleted, this shouldn't happen.", "code/modules/spells/spell_types/shapeshift/_shape_status.dm", 88);

        already_restored = TRUE;
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_PRE_WABBAJACKED, COMSIG_LIVING_DEATH));
        UnregisterSignal(caster_mob, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_DEATH));

        REMOVE_TRAIT(caster_mob, TRAIT_NO_TRANSFORM, TRAIT_STATUS_EFFECT(id));
        caster_mob.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_SHAPECHANGE_EFFECT);

        atom former_loc = owner.loc;
        await owner.moveToNullspace();
        await caster_mob.forceMove(former_loc); // This is to avoid crushing our former cockroach body

        if (Dm.truthy(kill_caster_after))
            await caster_mob.death();

        await after_unchange();

        // We're about to remove the status effect and clear owner so we need to cache this
        mob_living former_body = owner;

        // Do this late as it will destroy the status effect we are in and null a bunch of values we are trying to use
        if (owner.mind != null)
            await owner.mind.transfer_to(caster_mob);

        // Destroy the owner after all's said and done, this will also destroy our status effect (src)
        // retore_caster() should never reach this point while either the owner or the effect is being qdeleted
        qdel(former_body);
    }

    /// Effects done after the casting mob has reverted to their human form.
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:117
    public virtual async DmTask after_unchange()
    {
        await DmTask.CompletedTask;
        owner.SEND_SIGNAL(COMSIG_LIVING_UNSHAPESHIFTED, Dm.list(owner, caster_mob));
    }

    /// Signal proc for [COMSIG_LIVING_DEATH] from our owner.
    /// If our owner mob is killed, we should revert back to normal.
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:123
    [SignalHandler]
    public virtual void on_shape_death(datum source = null, object gibbed = null)
    {
        // gibbed = deleted = nothing to restore
        if (Dm.truthy(gibbed))
            return;

        Waiting.NoSleep(restore_caster());
    }

    /// Signal proc for [COMSIG_LIVING_DEATH] from our caster.
    /// If our internal caster is killed, kill our owner, too (which causes the above signal).
    /// This should very rarely end up being called but you never know
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:135
    [SignalHandler]
    public virtual void on_caster_death(datum source = null, object gibbed = null)
    {
        // Our caster inside was gibbed, mirror the gib to our mob
        if (Dm.truthy(gibbed))
            Waiting.NoSleep(owner.gib(DROP_ALL_REMAINS));

        // Otherwise our caster died, just make our mob die
        else
            Waiting.NoSleep(owner.death());
    }

    /// Signal proc for [COMSIG_QDELETING] from our caster, delete us / our owner if we get deleted
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:147
    [SignalHandler]
    public virtual void on_caster_deleted(datum source = null)
    {
        caster_mob = null;
        if (QDELETED(owner))
            return;

        qdel(owner);
    }
}

// A subtype for a shapechange sourced from a shapeshift spell.
// tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:158
public partial class datum_status_effect_shapechange_mob_from_spell : datum_status_effect_shapechange_mob
{
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:162
    [DmParams("new_owner", "caster", "source_spell")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        datum_action_cooldown_spell_shapeshift source_spell = Dm.arg(__extra, 1) as datum_action_cooldown_spell_shapeshift;
        if (!(source_spell is datum_action_cooldown_spell_shapeshift))
        {
            stack_trace("Mob shapechange \"from spell\" status effect applied without a source spell.", "code/modules/spells/spell_types/shapeshift/_shape_status.dm", 170);
            qdel(this);
            return null;
        }

        source_weakref = WEAKREF(source_spell) as datum_weakref;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:171
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        datum_action_cooldown_spell_shapeshift source_spell = source_weakref.resolve() as datum_action_cooldown_spell_shapeshift;
        if (source_spell.owner == caster_mob)
        {
            // Assuming the spell is owned by the caster, give it over to the shapeshifted mob
            // so they can actually transform back to their original form
            await source_spell.Grant(owner);

            if (Dm.truthy(source_spell.convert_damage))
            {
                float damage_to_apply = Dm.num(owner.maxHealth) * (Dm.num(caster_mob.get_total_damage()) / Dm.num(caster_mob.maxHealth));

                await owner.apply_damage(damage_to_apply, source_spell.convert_damage_type, forced: TRUE, spread_damage: TRUE, wound_bonus: CANT_WOUND);
                // Only transfer blood if both mobs are supposed to have a blood volume
                if (Dm.truthy(CAN_HAVE_BLOOD(owner)) && Dm.truthy(CAN_HAVE_BLOOD(caster_mob)))
                    owner.set_blood_volume(caster_mob.get_blood_volume());
            }
        }

        foreach (datum_action bodybound_action in Dm.each_cast<datum_action>(caster_mob.actions))
        {
            if (bodybound_action.target != caster_mob)
                continue;
            await bodybound_action.Grant(owner);
        }

        return await base.on_apply(__extra);
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:193
    public override async DmTask restore_caster(float? kill_caster_after = null)
    {
        datum_action_cooldown_spell_shapeshift source_spell = source_weakref.resolve() as datum_action_cooldown_spell_shapeshift;
        // The owner = owner check here is specifically for edge cases in which the owner of the spell
        // is no longer in control of the shapeshifted mob, such as mindswapping out of a shapeshift
        if (!QDELETED(source_spell) && source_spell.owner == owner)
            await source_spell.Grant(caster_mob);
        if (owner?.contents != null)
        {
            // Prevent round removal and consuming stuff when losing shapeshift
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(owner.contents))
            {
                if (thing == caster_mob || HAS_TRAIT(thing, TRAIT_NOT_BARFABLE))
                    continue;
                await thing.forceMove(get_turf(owner));
            }
        }

        foreach (datum_action bodybound_action in Dm.each_cast<datum_action>(owner.actions))
        {
            if (bodybound_action.target != caster_mob)
                continue;
            await bodybound_action.Grant(caster_mob);
        }

        await base.restore_caster(kill_caster_after);
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:213
    public override async DmTask after_unchange()
    {
        await base.after_unchange();
        datum_action_cooldown_spell_shapeshift source_spell = source_weakref?.resolve() as datum_action_cooldown_spell_shapeshift;
        if (QDELETED(source_spell) || !Dm.truthy(source_spell.convert_damage))
            return;

        await caster_mob.fully_heal(HEAL_DAMAGE); // Remove all of our damage before setting our health to a proportion of the former transformed mob's health
        float damage_to_apply = Dm.num(caster_mob.maxHealth) * (Dm.num(owner.get_total_damage()) / Dm.num(owner.maxHealth));
        await caster_mob.apply_damage(damage_to_apply, source_spell.convert_damage_type, forced: TRUE, spread_damage: TRUE, wound_bonus: CANT_WOUND);
        // Only transfer blood if both mobs are supposed to have a blood volume
        if (Dm.truthy(CAN_HAVE_BLOOD(owner)) && Dm.truthy(CAN_HAVE_BLOOD(caster_mob)))
            caster_mob.set_blood_volume(owner.get_blood_volume());
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:226
    public override void on_shape_death(datum source = null, object gibbed = null)
    {
        datum_action_cooldown_spell_shapeshift source_spell = source_weakref.resolve() as datum_action_cooldown_spell_shapeshift;
        // If our spell dictates our wizard dies when our shape dies, we won't restore by default
        if (!QDELETED(source_spell) && Dm.truthy(source_spell.die_with_shapeshifted_form))
        {
            // (But if our spell says we should revert on death anyways, we'll also do that)
            if (Dm.truthy(source_spell.revert_on_death))
                Waiting.NoSleep(restore_caster(kill_caster_after: TRUE));
            // Otherwise, we just do nothing - we dead
            return;
        }

        base.on_shape_death(source, gibbed); // Restore like normal
    }

    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:238
    [DmParams("source")]
    public override void on_caster_death(datum source = null, object gibbed = null)
    {
        datum_action_cooldown_spell_shapeshift source_spell = source_weakref.resolve() as datum_action_cooldown_spell_shapeshift;
        // If our spell does not have revert_on_death, don't do anything when our caster dies
        if (!QDELETED(source_spell) && !Dm.truthy(source_spell.revert_on_death))
            return;

        base.on_caster_death(source, gibbed); // Kill our owner and revert, like normal
    }
}

// tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:247
public partial class atom_movable_screen_alert_status_effect_shapeshifted : atom_movable_screen_alert_status_effect
{
    // tg: code/modules/spells/spell_types/shapeshift/_shape_status.dm:254
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        mob_living living_user = usr as mob_living;
        if (!(living_user is mob_living))
            return dot;

        // Clicking the action will try to cast whatever spell shifted us in the first place
        foreach (datum_action_cooldown_spell_shapeshift shift_spell in Dm.each<datum_action_cooldown_spell_shapeshift>(living_user.actions))
        {
            if (shift_spell.shapeshift_type != null && shift_spell.shapeshift_type.IsInstanceOfType(living_user))
            {
                await shift_spell.Trigger();
                return TRUE;
            }
        }
        return dot;
    }
}
