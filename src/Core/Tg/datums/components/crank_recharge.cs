// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crank_recharge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the hand crank of the laser musket, the smoothbore disabler, the thermal pistols and the
// repeater: used in hand, it charges the gun's cell after a do_after, with its crank sound and balloon; the thermal pistols are
// spun (only with the gun-flip trait of their holster). The var block is generated (D111).

// Cranking feature on the laser musket and smoothbore disabler, could probably be used on more than guns
// tg: code/datums/components/crank_recharge.dm:2
public partial class datum_component_crank_recharge : datum_component
{
    // tg: code/datums/components/crank_recharge.dm:21
    [DmParams("charging_cell", "spin_to_win", "charge_amount", "cooldown_time", "charge_sound", "charge_sound_cooldown_time", "charge_move")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object charging_cell = Dm.arg(__extra, 0);
        object spin_to_win = Dm.arg(__extra, 1) ?? FALSE;
        object charge_amount = Dm.arg(__extra, 2) ?? 500;
        object cooldown_time = Dm.arg(__extra, 3) ?? 2 * SECONDS;
        object charge_sound = Dm.arg(__extra, 4) ?? Dm.resource("sound/items/weapons/laser_crank.ogg");
        object charge_sound_cooldown_time = Dm.arg(__extra, 5) ?? 1.8f * SECONDS;
        object charge_move = Dm.arg(__extra, 6) ?? NONE;
        object dot = await base.Initialize(Dm.forward(__extra, charging_cell, spin_to_win, charge_amount, cooldown_time, charge_sound, charge_sound_cooldown_time, charge_move));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        if (charging_cell == null || !istype(charging_cell, typeof(obj_item_stock_parts_power_store)))
            return COMPONENT_INCOMPATIBLE;
        this.charging_cell = charging_cell as obj_item_stock_parts_power_store;
        this.spin_to_win = Dm.num(spin_to_win);
        this.charge_amount = charge_amount;
        this.cooldown_time = cooldown_time;
        this.charge_sound = charge_sound;
        this.charge_sound_cooldown_time = charge_sound_cooldown_time;
        this.charge_move = Dm.num(charge_move);
        return dot;
    }

    // tg: code/datums/components/crank_recharge.dm:34
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
    }

    // tg: code/datums/components/crank_recharge.dm:38
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_ITEM_ATTACK_SELF);
    }

    // tg: code/datums/components/crank_recharge.dm:42
    [SignalHandler]
    public virtual object on_attack_self(obj source = null, mob_living user = null)
    {
        Waiting.Detach(crank(source, user)); //game doesnt like signal handler and do afters mingling
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/crank_recharge.dm:48
    public virtual async DmTask crank(obj source = null, mob user = null)
    {
        if (charging_cell.charge >= charging_cell.maxcharge)
        {
            source.balloon_alert(user, I18n.T("already charged!"));
            return;
        }
        if (Dm.truthy(is_charging))
            return;
        if (Dm.truthy(spin_to_win) && !HAS_TRAIT(user, TRAIT_GUNFLIP))
        {
            source.balloon_alert(user, I18n.T("need holster to spin!"));
            return;
        }

        is_charging = TRUE;
        if (charge_sound_cooldown <= world.time) // COOLDOWN_FINISHED(src, charge_sound_cooldown), inline (rules 11.1 (a))
        {
            charge_sound_cooldown = world.time + Dm.num(charge_sound_cooldown_time); // COOLDOWN_START(src, charge_sound_cooldown, charge_sound_cooldown_time)
            playsound(source, charge_sound, 40);
        }
        source.balloon_alert(user, I18n.T("charging..."));
        if (!Dm.truthy(await do_after(user, cooldown_time, source, interaction_key: DOAFTER_SOURCE_CHARGE_CRANKRECHARGE, timed_action_flags: charge_move)))
        {
            is_charging = FALSE;
            return;
        }
        await charging_cell.give(charge_amount);
        source.update_appearance();
        is_charging = FALSE;
        if (Dm.truthy(spin_to_win))
            source.SpinAnimation(4, 2); //What a badass
        source.balloon_alert(user, I18n.T("charged"));
    }
}
