// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/recharge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the self-recharging gun under the kinetic accelerator; the mini energy crossbow (syndicate G2); gaps GU1 (D333): the
// fantasy bonuses (the fantasy component itself is not ported) and the SC/FISHER disruptor (its lore, its melee hit and its throw
// disrupting what they strike, saboteur.dm). The shrink ray (the abductors' gear, abductor_items.dm) keeps its own procs and stays
// inert.

// tg: code/modules/projectiles/guns/energy/recharge.dm:2
public partial class obj_item_gun_energy_recharge : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/recharge.dm:21
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        recharge_time = Dm.num(modify_fantasy_variable("recharge_time", recharge_time, -Dm.num(bonus), minimum: 0.2f * SECONDS));
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:25
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        recharge_time = Dm.num(reset_fantasy_variable("recharge_time", recharge_time));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(holds_charge))
            empty();
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:35
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        object dot = await base.shoot_live_shot(user, pointblank, pbtarget, message);
        attempt_reload();
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:39
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(can_shoot()))
            attempt_reload();
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:44
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (!QDELING(this) && !Dm.truthy(holds_charge))
        {
            // Put it on a delay because moving item from slot to hand
            // calls dropped().
            _addtimer(Dm.@new<datum_callback>(this, nameof(empty_if_not_held)), 0.1f * SECONDS, file: "code/modules/projectiles/guns/energy/recharge.dm", line: 50);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:51
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
        attempt_reload();
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:55
    public virtual void empty_if_not_held()
    {
        if (!ismob(loc))
        {
            empty();
            deltimer(recharge_timerid);
        }
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:60
    public virtual void empty()
    {
        if (cell != null)
            Waiting.NoSleep(cell.use(cell.charge));
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:65
    public virtual void attempt_reload(object set_recharge_time = null)
    {
        if (cell == null)
            return;
        if (Dm.eq(cell.charge, cell.maxcharge))
            return;
        if (!Dm.truthy(set_recharge_time))
            set_recharge_time = recharge_time;
        float carried = 0;
        if (!Dm.truthy(unique_frequency))
        {
            // A runtime in tg: a gun in nullspace (a test's, or one deleted mid-way) has no loc to read.
            foreach (obj_item_gun_energy_recharge recharging_gun in Dm.each<obj_item_gun_energy_recharge>(loc?.get_all_contents()))
            {
                if (recharging_gun.type != type || Dm.truthy(recharging_gun.unique_frequency))
                    continue;
                carried++;
            }
        }
        carried = max(carried, 1);
        deltimer(recharge_timerid);
        recharge_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(reload)), Dm.num(set_recharge_time) * carried, TIMER_STOPPABLE, file: "code/modules/projectiles/guns/energy/recharge.dm", line: 80);
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:82
    public virtual void reload()
    {
        Waiting.NoSleep(cell.give(cell.maxcharge));
        if (!Dm.truthy(suppressed) && Dm.truthy(recharge_sound))
            playsound(loc, recharge_sound, 60, TRUE);
        else
            to_chat(loc, span_warning(I18n.T("[] silently charges up.", this)));
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:90
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(no_charge_state) && !Dm.truthy(can_shoot()))
            Dm.append(ref dot, Dm.text("[]_empty", base_icon_state));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:95
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(no_charge_state))
        {
            if (Dm.truthy(can_shoot()))
                icon_state = Dm.str(base_icon_state);
            else
                icon_state = Dm.str(no_charge_state);
        }
        return dot;
    }
}

// The syndicate campaign G2 (D316): the mini energy crossbow and its candy corn and large variants.
// tg: code/modules/projectiles/guns/energy/recharge.dm:103
public partial class obj_item_gun_energy_recharge_ebow : obj_item_gun_energy_recharge
{
    // tg: code/modules/projectiles/guns/energy/recharge.dm:123
    public override async DmTask add_bayonet_point()
    {
        await AddComponent(typeof(datum_component_bayonet_attachable), Dm.named("offset_x", 20), Dm.named("offset_y", 12));
    }
}

/// A silly gun that does literally zero damage, but disrupts electrical sources of light, like flashlights.
// tg: code/modules/projectiles/guns/energy/recharge.dm:151
public partial class obj_item_gun_energy_recharge_fisher : obj_item_gun_energy_recharge
{
    // tg: code/modules/projectiles/guns/energy/recharge.dm:166
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("The SC/FISHER is an illegally-modified kinetic accelerator that's been cut down and refit into a miniature energy gun chassis, optimized for temporary, but effective, electronic warfare.<br><br>The reengineered kinetic accelerator central to the SC/FISHER's functionality has been modified for its kinetic bolts to <b>temporarily disrupt flashlights, cameras, APCs, and pAI speech modules</b>, in return for dealing no damage. This effect works longer against targets struck with the SC/FISHER either in melee or by having it thrown at them, but you probably shouldn't be throwing it at people.<br><br>While some would argue that sacrificing damage for a light-disrupting, fixture-breaking gimmick makes the SC/FISHER a dead-end in equipment development, others argue that it is both amusing and tactically sound to be able to shoot at light sources and pesky pAIs to disrupt their function.<br><br>Caveat emptor."))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:184
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        obj_projectile_energy_fisher_melee simulated_hit = Dm.@new<obj_projectile_energy_fisher_melee>();
        simulated_hit.firer = user;
        await simulated_hit.on_hit(target_mob);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/recharge.dm:192
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        // ...you reeeeeally just shoot them, but in case you can't/won't
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        obj_projectile_energy_fisher_melee simulated_hit = Dm.@new<obj_projectile_energy_fisher_melee>();
        simulated_hit.firer = throwingdatum?.get_thrower() as atom_movable;
        Waiting.NoSleep(simulated_hit.on_hit(hit_atom));
        return dot;
    }
}
