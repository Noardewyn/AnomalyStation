// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/aoe_spell/repulse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the repulse spell (the plasma fist's Tornado Sweep casts it), whole with the wizard's and the
// xeno's tail sweep (its strongman check reads the athletics skill, gaps SL1). The var blocks are generated (D111).

// tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:1
public partial class datum_action_cooldown_spell_aoe_repulse : datum_action_cooldown_spell_aoe
{
    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:9
    public override object get_caster_from_target(atom target = null)
    {
        if (target.loc is obj_structure_closet)
            return target;

        return base.get_caster_from_target(target);
    }

    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:15
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return Dm.truthy(await base.is_valid_target(cast_on)) || cast_on.loc is obj_structure_closet ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:18
    public override async DmTask<object> cast(atom cast_on = null)
    {
        if (cast_on.loc is obj_structure_closet open_closet)
        {
            await open_closet.open(force: TRUE);
            open_closet.visible_message(span_warning(I18n.T("[] suddenly flies open!", open_closet)));
        }

        return await base.cast(cast_on);
    }

    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:26
    public override DmList get_things_to_cast_on(atom center = null)
    {
        DmList things = Dm.list();
        foreach (atom_movable nearby_movable in Dm.each<atom_movable>(view(aoe_radius, center)))
        {
            if (nearby_movable == owner || nearby_movable == center)
                continue;
            if (Dm.truthy(nearby_movable.anchored))
                continue;

            Dm.append(ref things, nearby_movable);
        }

        return things;
    }

    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:38
    public override async DmTask cast_on_thing_in_aoe(atom victim = null, atom caster = null)
    {
        if (victim is mob victim_mob)
        {
            if (Dm.truthy(victim_mob.can_block_magic(antimagic_flags)))
                return;
        }

        var throwtarget = get_edge_target_turf(caster, get_dir(caster, get_step_away(victim as atom_movable, caster))) as atom;
        float dist_from_caster = get_dist(victim, caster);

        if (dist_from_caster == 0)
        {
            if (victim is mob_living victim_living)
            {
                await victim_living.Paralyze(10 * SECONDS);
                await victim_living.adjust_brute_loss(5);
                to_chat(victim, span_userdanger(I18n.T("You're slammed into the floor by []!", caster)));
            }
        }
        else
        {
            if (sparkle_path != null)
            {
                // Created sparkles will disappear on their own
                Dm.@new(sparkle_path, get_turf(victim), get_dir(caster, victim));
            }

            if (victim is mob_living victim_living)
            {
                await victim_living.Paralyze(4 * SECONDS);
                to_chat(victim, span_userdanger(I18n.T("You're thrown back by []!", caster)));
            }

            // So stuff gets tossed around at the same time.
            await ((atom_movable)victim).safe_throw_at(
                target: throwtarget,
                range: clamp(max_throw - clamp(dist_from_caster - 2, 0, dist_from_caster), 3, max_throw),
                speed: 1,
                thrower: caster is mob ? caster : null,
                force: repulse_force);
        }
    }
}

// tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:86
public partial class datum_action_cooldown_spell_aoe_repulse_xeno : datum_action_cooldown_spell_aoe_repulse
{
    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:105
    public override async DmTask<object> cast(atom cast_on = null)
    {
        if (cast_on is mob_living_carbon carbon_caster)
        {
            playsound(get_turf(carbon_caster), "sound/mobs/non-humanoids/hiss/hiss5.ogg", 80, TRUE, TRUE);
            carbon_caster.spin(0.6f * SECONDS, 1);
        }
        if (cast_on is mob_living living_caster && !Dm.truthy(is_strongman(living_caster)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(after_spin)), 0.6f * SECONDS, TIMER_DELETE_ME, file: "code/modules/spells/spell_types/aoe_spell/repulse.dm", line: 111);

        return await base.cast(cast_on);
    }

    /// Returns true if you're strong enough
    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:116
    public virtual object is_strongman(mob_living owner = null)
    {
        if (HAS_TRAIT(owner, TRAIT_STRENGTH))
            return TRUE;
        object strength_level = owner.mind?.get_skill_level(typeof(datum_skill_athletics));
        return Dm.num(Dm.ge(strength_level, SKILL_LEVEL_MASTER));
    }

    /// You're not strong enough :(
    // tg: code/modules/spells/spell_types/aoe_spell/repulse.dm:123
    public virtual async DmTask after_spin()
    {
        if (QDELETED(owner) || Dm.truthy(is_strongman(owner as mob_living)))
            return;
        var living_owner = (mob_living)owner;
        await living_owner.Knockdown(5 * SECONDS);
    }
}
