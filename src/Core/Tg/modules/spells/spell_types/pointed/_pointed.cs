// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/pointed/_pointed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the pointed base (gaps package C6) and the projectile base (gaps P3, D327: cryo- and pyrokinesis).

/// These spells override the caster's click, allowing them to cast the spell on whatever is clicked on.
// tg: code/modules/spells/spell_types/pointed/_pointed.dm:11
public partial class datum_action_cooldown_spell_pointed : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:23
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(active_msg))
            active_msg = Dm.text("You prepare to use [] on a target...", this);
        if (!Dm.truthy(deactive_msg))
            deactive_msg = Dm.text("You dispel [].", this);
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:30
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;

        await on_activation(on_who);
        return dot;
    }

    // Note: Destroy() calls Remove(), Remove() calls unset_click_ability() if our spell is active.
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:38
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;

        await on_deactivation(on_who, refund_cooldown);
        return dot;
    }

    /// Called when the spell is activated / the click ability is set to our spell
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:51
    public virtual async DmTask<object> on_activation(mob on_who = null)
    {
        to_chat(on_who, span_notice(Dm.text("[] <B>Left-click to cast the spell on a target!</B>", active_msg)));
        await build_all_button_icons();
        return TRUE;
    }

    /// Called when the spell is deactivated / the click ability is unset from our spell
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:59
    public virtual async DmTask<object> on_deactivation(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        if (Dm.truthy(refund_cooldown))
            to_chat(on_who, span_notice(Dm.text("[]", deactive_msg))); // Only send the "deactivation" message if they're willingly disabling the ability
        await build_all_button_icons();
        return TRUE;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:68
    public override async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        atom aim_assist_target = null;
        if (Dm.truthy(aim_assist))
            aim_assist_target = aim_assist__proc(clicker, target) as atom;
        return await base.InterceptClickOn(clicker, @params, aim_assist_target ?? target);
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:74
    public virtual object aim_assist__proc(mob_living clicker = null, atom target = null)
    {
        if (!isturf(target))
            return null;

        // Find any human, or if that fails, any living target
        return (object)Dm.locate<mob_living_carbon_human>(target) ?? Dm.locate<mob_living>(target);
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:81
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        if (cast_on == owner)
        {
            to_chat(owner, span_warning(Dm.text("You cannot cast [] on yourself!", this)));
            return FALSE;
        }

        return TRUE;
    }

    // tg's two before_cast overrides (:45 and :88) are one here: they run in tg's order.
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:45
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:88
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
        {
            await on_deactivation(owner, refund_cooldown: FALSE);
            return dot;
        }

        if (owner != null && Dm.num(get_dist(get_turf(owner), get_turf(cast_on))) > cast_range)
        {
            cast_on.balloon_alert(owner, "too far away!");
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        }
        return dot;
    }
}

// tg: code/modules/spells/spell_types/pointed/_pointed.dm:102
public partial class datum_action_cooldown_spell_pointed_projectile : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:113
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (projectile_amount > 1)
            unset_after_click = FALSE;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:118
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:121
    public override async DmTask<object> on_activation(mob on_who = null)
    {
        object dot = await base.on_activation(on_who);
        if (!Dm.truthy(dot))
            return dot;

        current_amount = projectile_amount;
        return dot;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:128
    public override async DmTask<object> on_deactivation(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.on_deactivation(on_who, refund_cooldown);
        if (projectile_amount > 1 && Dm.truthy(current_amount))
        {
            StartCooldown(cooldown_time * ((projectile_amount - current_amount) / projectile_amount));
            current_amount = 0;
        }
        return dot;
    }

    // cast_on is a turf, or atom target, that we clicked on to fire at.
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:135
    public override async DmTask<object> cast(atom cast_on = null)
    {
        await base.cast(cast_on);
        atom caster = get_caster_from_target(owner) as atom;
        if (!isturf(caster.loc))
            return FALSE;

        turf caster_turf = caster.loc as turf;
        // Get the tile infront of the caster, based on their direction
        turf caster_front_turf = get_step(owner, owner.dir);

        await fire_projectile(cast_on);
        await owner.newtonian_move(get_angle(caster_front_turf, caster_turf));
        if (current_amount <= 0)
            await unset_click_ability(owner, refund_cooldown: FALSE);

        return TRUE;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:152
    public override async DmTask after_cast(atom cast_on = null)
    {
        await base.after_cast(cast_on);
        if (current_amount > 0)
        {
            // We still have projectiles to cast!
            // Reset our cooldown and let them fire away
            await reset_spell_cooldown();
        }
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:159
    public virtual async DmTask<object> fire_projectile(atom target = null)
    {
        current_amount--;
        for (int i = 1; i <= projectiles_per_fire; i++)
        {
            obj_projectile to_fire = await Dm.new_async(projectile_type) as obj_projectile;
            await ready_projectile(to_fire, target, owner, i);
            // SEND_SIGNAL(owner, COMSIG_MOB_SPELL_PROJECTILE, src, target, to_fire) (rules 11.1 (e), a statement)
            if (Dm.truthy(owner._listen_lookup?[COMSIG_MOB_SPELL_PROJECTILE]))
                owner._SendSignal(COMSIG_MOB_SPELL_PROJECTILE, Dm.list(owner, this, target, to_fire));
            await to_fire.fire();
        }
        return TRUE;
    }

    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:168
    public virtual async DmTask ready_projectile(obj_projectile to_fire = null, atom target = null, mob user = null, object iteration = null)
    {
        to_fire.firer = owner;
        to_fire.fired_from = this;
        await to_fire.aim_projectile(target, owner);
        RegisterSignal(to_fire, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_cast_hit));

        if (to_fire is obj_projectile_magic magic_to_fire)
            magic_to_fire.antimagic_flags = antimagic_flags;
    }

    /// Signal proc for whenever the projectile we fire hits someone.
    /// Pretty much relays to the spell when the projectile actually hits something.
    // tg: code/modules/spells/spell_types/pointed/_pointed.dm:180
    [SignalHandler]
    public virtual void on_cast_hit(datum source = null, mob firer = null, atom target = null, object angle = null, object hit_limb = null)
    {
        // SEND_SIGNAL(src, COMSIG_SPELL_PROJECTILE_HIT, source, firer, target, angle, hit_limb) (rules 11.1 (e), a statement)
        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_PROJECTILE_HIT]))
            _SendSignal(COMSIG_SPELL_PROJECTILE_HIT, Dm.list(this, source, firer, target, angle, hit_limb));
    }
}
