// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the hand clicks (the cell out, the lock toggle), the damage overrides, can_use and the ethereals'
// drain and charge. The M7 shocks slice: shock
// (:158, over /obj/shock).

// tg: code/modules/power/apc/apc_attack.dm:9
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_attack.dm:9
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = null;
        dot = await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(await can_interact(user)))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !isturf(loc))
            return dot;
        if (!ishuman(user))
            return dot;
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        obj_item_organ_stomach_ethereal maybe_ethereal_stomach = human_user.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (!(maybe_ethereal_stomach is obj_item_organ_stomach_ethereal))
            togglelock(user as mob_living);
        else
        {
            if (Dm.num(maybe_ethereal_stomach.cell.charge__proc()) >= ETHEREAL_CHARGE_NORMAL)
                togglelock(user as mob_living);
            await ethereal_interact(human_user, maybe_ethereal_stomach, modifiers);
        }
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/power/apc/apc_attack.dm:3
    private const int ETHEREAL_APC_DRAIN_TIME = 3 * SECONDS;
    // tg: code/modules/power/apc/apc_attack.dm:5
    private const float ETHEREAL_APC_POWER_GAIN = 0.1f * STANDARD_ETHEREAL_CHARGE;
    // tg: code/modules/power/apc/apc_attack.dm:7
    private const float ETHEREAL_APC_ALERT_DELAY = 0.75f * SECONDS;

    /// Special behavior for when an ethereal interacts with an APC.
    // tg: code/modules/power/apc/apc_attack.dm:28
    public virtual async DmTask ethereal_interact(mob_living_carbon_human user = null, obj_item_organ_stomach_ethereal used_stomach = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return;
        if (cell == null)
            return;
        if (used_stomach.drain_time > Dm.num(world.time))
            return;
        if (Dm.truthy(user.combat_mode))
            await discharge_to_ethereal(user, used_stomach);
        else
            await charge_from_ethereal(user, used_stomach);
    }

    private void ethereal_alert(mob_living_carbon_human user, string message, int line) // tg's addtimer of balloon_alert, four times over
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(balloon_alert), user, message), ETHEREAL_APC_ALERT_DELAY, file: "code/modules/power/apc/apc_attack.dm", line: line);
    }

    /// Handles discharging our internal cell to an ethereal and their stomach
    // tg: code/modules/power/apc/apc_attack.dm:41
    public virtual async DmTask discharge_to_ethereal(mob_living_carbon_human user = null, obj_item_organ_stomach_ethereal used_stomach = null)
    {
        float half_max_charge = Dm.num(cell.max_charge()) / 2;
        // Ethereals can't drain APCs under half charge, so that they are forced to look to alternative power sources if the station is running low
        if (Dm.num(cell.charge__proc()) < half_max_charge)
        {
            ethereal_alert(user, I18n.T("safeties prevent draining!"), 45);
            return;
        }

        obj_item_stock_parts_power_store stomach_cell = used_stomach.cell;
        used_stomach.drain_time = Dm.num(world.time) + ETHEREAL_APC_DRAIN_TIME;
        ethereal_alert(user, I18n.T("draining power..."), 50);
        while (Dm.truthy(await do_after(user, ETHEREAL_APC_DRAIN_TIME, target: this)))
        {
            if (used_stomach == null || (used_stomach != user.get_organ_slot(ORGAN_SLOT_STOMACH)))
            {
                balloon_alert(user, I18n.T("stomach removed!?"));
                return;
            }
            if (cell == null)
            {
                balloon_alert(user, I18n.T("cell removed!"));
                return;
            }
            if (Dm.num(cell.charge__proc()) < half_max_charge)
            {
                balloon_alert(user, I18n.T("safeties kicked in!"));
                return;
            }

            float our_available_charge = Dm.num(cell.charge__proc()) - half_max_charge;
            float stomach_used_charge = Dm.num(stomach_cell.used_charge());
            float potential_charge = min(our_available_charge, stomach_used_charge);
            float to_drain = min(ETHEREAL_APC_POWER_GAIN, potential_charge);
            object energy_drained = await cell.use(to_drain, TRUE);
            await used_stomach.adjust_charge(energy_drained);

            if (Dm.num(stomach_cell.used_charge()) <= 0)
            {
                balloon_alert(user, I18n.T("your charge is full!"));
                return;
            }
            if (Dm.num(cell.charge__proc()) <= 0)
            {
                balloon_alert(user, I18n.T("apc is empty!"));
                return;
            }
        }
    }

    /// Handles charging our internal cell from an ethereal and their stomach
    // tg: code/modules/power/apc/apc_attack.dm:77
    public virtual async DmTask charge_from_ethereal(mob_living_carbon_human user = null, obj_item_organ_stomach_ethereal used_stomach = null)
    {
        if (Dm.num(cell.charge__proc()) >= Dm.num(cell.max_charge()))
        {
            ethereal_alert(user, I18n.T("apc full!"), 79);
            return;
        }
        obj_item_stock_parts_power_store stomach_cell = used_stomach.cell;
        if (Dm.num(stomach_cell.charge__proc()) <= 0)
        {
            ethereal_alert(user, I18n.T("charge is too low!"), 83);
            return;
        }

        used_stomach.drain_time = Dm.num(world.time) + ETHEREAL_APC_DRAIN_TIME;
        ethereal_alert(user, I18n.T("transferring power..."), 87);
        if (!Dm.truthy(await do_after(user, ETHEREAL_APC_DRAIN_TIME, target: this)))
            return;
        if (used_stomach == null || (used_stomach != user.get_organ_slot(ORGAN_SLOT_STOMACH)))
        {
            balloon_alert(user, I18n.T("stomach removed!?"));
            return;
        }
        if (cell == null)
        {
            balloon_alert(user, I18n.T("cell removed!"));
            return;
        }

        float stomach_charge = Dm.num(stomach_cell.charge__proc());
        float our_used_charge = Dm.num(cell.used_charge());
        float potential_charge = min(stomach_charge, our_used_charge);
        float to_drain = min(ETHEREAL_APC_POWER_GAIN, potential_charge);
        float energy_drained = Dm.num(await used_stomach.adjust_charge(-to_drain));
        await cell.give(-energy_drained);

        if (Dm.num(cell.used_charge()) <= 0)
        {
            balloon_alert(user, I18n.T("apc is full!"));
            return;
        }
        if (Dm.num(stomach_cell.charge__proc()) <= 0)
        {
            balloon_alert(user, I18n.T("out of charge!"));
            return;
        }
    }

    // attack with hand - remove cell (if cover open) or interact with the APC
    // tg: code/modules/power/apc/apc_attack.dm:113
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(opened) && (!issilicon(user)))
        {
            if (cell != null)
            {
                user.visible_message(span_notice(I18n.T("[] removes \\the [] from []!", user, cell, this)));
                balloon_alert(user, I18n.T("cell removed"));
                await user.put_in_hands(cell);
            }
            return dot;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) && !Dm.truthy(opened)) //no board; no interface
            return dot;
        return dot;
    }

    // tg: code/modules/power/apc/apc_attack.dm:127
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null) // the root slot (no live ancestor declares it yet)
    {
        await atom_break();
        return null;
    }

    // tg: code/modules/power/apc/apc_attack.dm:130
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= "";
        sound_effect ??= TRUE;
        armour_penetration ??= 0;
        // APC being at 0 integrity doesnt delete it outright. Combined with take_damage this might cause runtimes.
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && Dm.le(atom_integrity, 0))
        {
            if (Dm.truthy(sound_effect))
                play_attack_sound(damage_amount, damage_type, damage_flag);
            return null;
        }
        return await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
    }

    // tg: code/modules/power/apc/apc_attack.dm:138
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        object dot = null;
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return damage_amount;
        dot = await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
        return dot;
    }

    // tg: code/modules/power/apc/apc_attack.dm:143
    public virtual object can_use(mob user = null, float? loud = null) //used by attack_hand() and Topic()
    {
        loud ??= 0;
        object dot = null;
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as _machinery.cs).
        bool isAdminGhostAI__gap = false;
        if (isAdminGhostAI__gap)
            return TRUE;
        if (!(HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap)) // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178)
            return TRUE;
        dot = TRUE;
        if (isAI(user) || iscyborg(user))
        {
            if (Dm.truthy(aidisabled) || Dm.truthy(user.has_status_effect(typeof(datum_status_effect_firewalled))))
                dot = FALSE;
            else if (malfai is mob_living_silicon_ai && !(malfai == user || Dm.@in(user, malfai.connected_robots)))
                dot = FALSE;
        }
        if (!Dm.truthy(dot) && !Dm.truthy(loud))
            balloon_alert(user, I18n.T("it's disabled!"));
        return dot;
    }

    // tg: code/modules/power/apc/apc_attack.dm:158  (the M7 shocks slice)
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (isalien(shocking))
            return FALSE;
        shock_source = this;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }
}
