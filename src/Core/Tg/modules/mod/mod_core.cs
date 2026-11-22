// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO1 (D333): the file but the soul shard core. The MOD cores that power a control unit: the base core, the infinite core, the
// standard core with its power cell (put in and taken out with an empty hand while the cover is open, charged by a cyborg
// recharger), the ethereal core (it draws on its wearer's ethereal stomach), the plasma core refuelled with plasma ore and sheets,
// the lavaland plasma flower core (flowers bloom where its wearer walks while the suit is on), the Candela core that recharges from
// a powered mining beacon network; each with its charge level for the spacesuit HUD and its charge bar colour and text for the
// MODsuit window. Left with rows (not-translated.md 219): the soul shard core and its wizard kind (:501-656: its charge is its
// wearer's sanity (the moods, gaps MS1), but its soulstone and its backlash read the cult, heretic and wizard antagonists, paused by
// D333), and the lavaland core's pollen particles (:478, the particle holder is not ported). The var blocks are generated (D111).

// tg: code/modules/mod/mod_core.dm:1
public partial class obj_item_mod_core : obj_item_mod
{
    // tg: code/modules/mod/mod_core.dm:13
    public override object Destroy(float? force = null)
    {
        if (mod != null)
            Waiting.NoSleep(uninstall());
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_core.dm:18
    public virtual async DmTask install(obj_item_mod_control mod_unit = null)
    {
        mod = mod_unit;
        mod.core = this;
        await forceMove(mod);
        mod.update_charge_alert();
    }

    // tg: code/modules/mod/mod_core.dm:24
    [DmParams()]
    public virtual async DmTask uninstall(params object[] __extra)
    {
        mod.core = null;
        mod.update_charge_alert();
        mod = null;
    }

    /// Returns the item responsible for charging the suit, like a power cell, an ethereal's stomach, the core itself, etc.
    // tg: code/modules/mod/mod_core.dm:30
    public virtual object charge_source()
    {
        return null;
    }

    /// Returns the amount of charge in the core.
    // tg: code/modules/mod/mod_core.dm:34
    public virtual object charge_amount()
    {
        return 0;
    }

    /// Returns the max amount of charge stored in the core.
    // tg: code/modules/mod/mod_core.dm:38
    [DmParams()]
    public virtual object max_charge_amount(params object[] __extra)
    {
        return 1;
    }

    /// Adds a set amount of charge to the core.
    // tg: code/modules/mod/mod_core.dm:42
    public virtual async DmTask<object> add_charge(object amount = null)
    {
        return FALSE;
    }

    /// Subtracts a set amount of charge from the core.
    // tg: code/modules/mod/mod_core.dm:46
    public virtual async DmTask<object> subtract_charge(object amount = null)
    {
        return FALSE;
    }

    /// Checks if there's enough charge in the core to use an amount of energy.
    // tg: code/modules/mod/mod_core.dm:50
    public virtual object check_charge(object amount = null)
    {
        return FALSE;
    }

    /// Returns what icon state to display on the HUD for the charge level of this core
    // tg: code/modules/mod/mod_core.dm:54
    public virtual object get_charge_icon_state()
    {
        return SPACESUIT_NO_ICON;
    }

    /// Gets what the UI should use for the charge bar color.
    // tg: code/modules/mod/mod_core.dm:58
    public virtual object get_chargebar_color()
    {
        return "bad";
    }

    /// Gets what the UI should use for the charge bar text.
    // tg: code/modules/mod/mod_core.dm:62
    public virtual object get_chargebar_string()
    {
        object charge_amount = this.charge_amount();
        object max_charge_amount = this.max_charge_amount();
        return I18n.T("[] of [] ([]%)", display_energy(charge_amount), display_energy(this.max_charge_amount()), round(Dm.num(Dm.div(Dm.mul(100, charge_amount), max_charge_amount)), 1));
    }
}

// tg: code/modules/mod/mod_core.dm:68
public partial class obj_item_mod_core_infinite : obj_item_mod_core
{
    // tg: code/modules/mod/mod_core.dm:74
    public override object charge_source()
    {
        return this;
    }

    // tg: code/modules/mod/mod_core.dm:77
    public override object charge_amount()
    {
        return INFINITY;
    }

    // tg: code/modules/mod/mod_core.dm:80
    [DmParams()]
    public override object max_charge_amount(params object[] __extra)
    {
        return INFINITY;
    }

    // tg: code/modules/mod/mod_core.dm:83
    public override async DmTask<object> add_charge(object amount = null)
    {
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:86
    public override async DmTask<object> subtract_charge(object amount = null)
    {
        return amount;
    }

    // tg: code/modules/mod/mod_core.dm:89
    public override object check_charge(object amount = null)
    {
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:92
    public override object get_charge_icon_state()
    {
        return SPACESUIT_CELL_HIGH;
    }

    // tg: code/modules/mod/mod_core.dm:95
    public override object get_chargebar_color()
    {
        return "teal";
    }

    // tg: code/modules/mod/mod_core.dm:98
    public override object get_chargebar_string()
    {
        return I18n.T("Infinite");
    }
}

// tg: code/modules/mod/mod_core.dm:101
public partial class obj_item_mod_core_standard : obj_item_mod_core
{
    // tg: code/modules/mod/mod_core.dm:112
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_core.dm:116
    public override async DmTask install(obj_item_mod_control mod_unit = null)
    {
        await base.install(mod_unit);
        if (cell != null)
            await install_cell(cell);
        RegisterSignal(mod, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(mod, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(mod, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_mod_interaction));
        RegisterSignal(mod, COMSIG_MOD_WEARER_SET, nameof(on_wearer_set));
        if (mod.wearer != null)
            on_wearer_set(mod, mod.wearer);
    }

    // tg: code/modules/mod/mod_core.dm:127
    [DmParams()]
    public override async DmTask uninstall(params object[] __extra)
    {
        if (!QDELETED(cell))
            await cell.forceMove(drop_location() as atom);
        UnregisterSignal(mod, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_MOD_WEARER_SET
        ));
        if (mod.wearer != null)
            on_wearer_unset(mod, mod.wearer);
        await base.uninstall(__extra);
    }

    // tg: code/modules/mod/mod_core.dm:140
    public override object charge_source()
    {
        return cell;
    }

    // tg: code/modules/mod/mod_core.dm:143
    public override object charge_amount()
    {
        obj_item_stock_parts_power_store charge_source = this.charge_source() as obj_item_stock_parts_power_store;
        return (charge_source?.charge is var or__1 && Dm.truthy(or__1) ? or__1 : 0);
    }

    // tg: code/modules/mod/mod_core.dm:147
    [DmParams("amount")]
    public override object max_charge_amount(params object[] __extra)
    {
        object amount = Dm.arg(__extra, 0);
        obj_item_stock_parts_power_store charge_source = this.charge_source() as obj_item_stock_parts_power_store;
        return (charge_source?.maxcharge is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
    }

    // tg: code/modules/mod/mod_core.dm:151
    public override async DmTask<object> add_charge(object amount = null)
    {
        object dot = null;
        obj_item_stock_parts_power_store charge_source = this.charge_source() as obj_item_stock_parts_power_store;
        if (charge_source == null)
            return FALSE;
        dot = await charge_source.give(amount);
        if (Dm.truthy(dot))
            mod.update_charge_alert();
        return dot;
    }

    // tg: code/modules/mod/mod_core.dm:160
    public override async DmTask<object> subtract_charge(object amount = null)
    {
        object dot = null;
        obj_item_stock_parts_power_store charge_source = this.charge_source() as obj_item_stock_parts_power_store;
        if (charge_source == null)
            return FALSE;
        dot = await charge_source.use(amount, TRUE);
        if (Dm.truthy(dot))
            mod.update_charge_alert();
        return dot;
    }

    // tg: code/modules/mod/mod_core.dm:169
    public override object check_charge(object amount = null)
    {
        return Dm.num(Dm.ge(charge_amount(), amount));
    }

    // tg: code/modules/mod/mod_core.dm:172
    public override object get_charge_icon_state()
    {
        if (charge_source() == null)
            return SPACESUIT_CELL_MISSING;

        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= 0.75f && sw__1 <= INFINITY)
            return SPACESUIT_CELL_HIGH;
        else if (sw__1 >= 0.5f && sw__1 <= 0.75f)
            return SPACESUIT_CELL_MID;
        else if (sw__1 >= 0.25f && sw__1 <= 0.5f)
            return SPACESUIT_CELL_LOW;
        else if (sw__1 >= 0.02f && sw__1 <= 0.25f)
            return SPACESUIT_CELL_VERY_LOW;

        return SPACESUIT_CELL_EMPTY;
    }

    // tg: code/modules/mod/mod_core.dm:188
    public override object get_chargebar_color()
    {
        if (charge_source() == null)
            return "transparent";
        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= -INFINITY && sw__1 <= 0.33f)
            return "bad";
        else if (sw__1 >= 0.33f && sw__1 <= 0.66f)
            return "average";
        else if (sw__1 >= 0.66f && sw__1 <= INFINITY)
            return "good";
        return null;
    }

    // tg: code/modules/mod/mod_core.dm:199
    public override object get_chargebar_string()
    {
        if (charge_source() == null)
            return I18n.T("Power Cell Missing");
        return base.get_chargebar_string();
    }

    // tg: code/modules/mod/mod_core.dm:204
    public virtual async DmTask install_cell(object new_cell = null)
    {
        cell = new_cell as obj_item_stock_parts_power_store;
        await cell.forceMove(this);
        mod.update_charge_alert();
    }

    // tg: code/modules/mod/mod_core.dm:209
    public virtual void uninstall_cell()
    {
        if (cell == null)
            return;
        cell = null;
        mod.update_charge_alert();
    }

    // tg: code/modules/mod/mod_core.dm:215
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
            uninstall_cell();
        return dot;
    }

    // tg: code/modules/mod/mod_core.dm:220
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob examiner = null, DmList examine_text = null)
    {
        if (!Dm.truthy(mod.open))
            return;
        Dm.append(ref examine_text, cell != null ? I18n.T("You could remove the cell with an empty hand.") : I18n.T("You could use a cell on it to install one."));
    }

    // tg: code/modules/mod/mod_core.dm:227
    [SignalHandler]
    public virtual object on_attack_hand(datum source = null, mob_living user = null)
    {
        if (Dm.truthy(mod.seconds_electrified) && Dm.truthy(charge_amount()) && Dm.truthy(Waiting.NoSleep(mod.shock__proc(user, 100))))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(mod.open) && mod.loc == user)
        {
            Waiting.Detach(mod_uninstall_cell(user));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return NONE;
    }

    // tg: code/modules/mod/mod_core.dm:237
    public virtual async DmTask mod_uninstall_cell(mob_living user = null)
    {
        if (cell == null)
        {
            mod.balloon_alert(user, I18n.T("no cell!"));
            return;
        }
        mod.balloon_alert(user, I18n.T("removing cell..."));
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: mod)))
        {
            mod.balloon_alert(user, I18n.T("interrupted!"));
            return;
        }
        mod.balloon_alert(user, I18n.T("cell removed"));
        playsound(mod, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        obj_item cell_to_move = cell;
        await cell_to_move.forceMove(drop_location() as atom);
        await user.put_in_hands(cell_to_move);
    }

    // tg: code/modules/mod/mod_core.dm:251
    [SignalHandler]
    public virtual object on_mod_interaction(datum source = null, mob_living user = null, obj_item thing = null)
    {
        return Waiting.NoSleep(item_interaction(user, thing));
    }

    // tg: code/modules/mod/mod_core.dm:256
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return Dm.truthy(await replace_cell(tool, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/modules/mod/mod_core.dm:259
    public virtual async DmTask<object> replace_cell(obj_item attacking_item = null, mob user = null)
    {
        if (!(attacking_item is obj_item_stock_parts_power_store_cell))
            return FALSE;
        if (!Dm.truthy(mod.open))
        {
            mod.balloon_alert(user, I18n.T("cover closed!"));
            playsound(mod, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        if (cell != null)
        {
            mod.balloon_alert(user, I18n.T("already has cell!"));
            playsound(mod, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SILENCED_SOUND_EXTRARANGE);
            return FALSE;
        }
        await install_cell(attacking_item);
        mod.balloon_alert(user, I18n.T("cell installed"));
        playsound(mod, Dm.resource("sound/machines/click.ogg"), 50, TRUE, SILENCED_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:275
    [SignalHandler]
    public virtual void on_wearer_set(datum source = null, mob user = null)
    {
        RegisterSignal(mod.wearer, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(on_borg_charge));
        RegisterSignal(mod, COMSIG_MOD_WEARER_UNSET, nameof(on_wearer_unset));
    }

    // tg: code/modules/mod/mod_core.dm:281
    [SignalHandler]
    public virtual void on_wearer_unset(datum source = null, mob user = null)
    {
        UnregisterSignal(mod.wearer, COMSIG_PROCESS_BORGCHARGER_OCCUPANT);
        UnregisterSignal(mod, COMSIG_MOD_WEARER_UNSET);
    }

    // tg: code/modules/mod/mod_core.dm:287
    [SignalHandler]
    public virtual void on_borg_charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null)
    {
        obj_item_stock_parts_power_store target_cell = charge_source() as obj_item_stock_parts_power_store;
        if (target_cell == null)
            return;

        if (Dm.truthy(Waiting.NoSleep(charge_cell.Invoke(target_cell, seconds_per_tick))))
            mod.update_charge_alert();
    }
}

// tg: code/modules/mod/mod_core.dm:297
public partial class obj_item_mod_core_ethereal : obj_item_mod_core
{
    // tg: code/modules/mod/mod_core.dm:308
    public override object charge_source()
    {
        obj_item_organ_stomach_ethereal ethereal_stomach = mod.wearer.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (!(ethereal_stomach is obj_item_organ_stomach_ethereal))
            return null;
        return ethereal_stomach;
    }

    // tg: code/modules/mod/mod_core.dm:314
    public override object charge_amount()
    {
        obj_item_organ_stomach_ethereal charge_source = this.charge_source() as obj_item_organ_stomach_ethereal;
        return (charge_source?.cell.charge__proc() is var or__1 && Dm.truthy(or__1) ? or__1 : ETHEREAL_CHARGE_NONE);
    }

    // tg: code/modules/mod/mod_core.dm:318
    [DmParams()]
    public override object max_charge_amount(params object[] __extra)
    {
        return ETHEREAL_CHARGE_FULL;
    }

    // tg: code/modules/mod/mod_core.dm:321
    public override async DmTask<object> add_charge(object amount = null)
    {
        obj_item_organ_stomach_ethereal charge_source = this.charge_source() as obj_item_organ_stomach_ethereal;
        if (charge_source == null)
            return FALSE;
        await charge_source.adjust_charge(Dm.num(amount) * charge_modifier);
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:328
    public override async DmTask<object> subtract_charge(object amount = null)
    {
        obj_item_organ_stomach_ethereal charge_source = this.charge_source() as obj_item_organ_stomach_ethereal;
        if (charge_source == null)
            return FALSE;
        return Dm.neg(await charge_source.adjust_charge(-Dm.num(amount) * charge_modifier));
    }

    // tg: code/modules/mod/mod_core.dm:334
    public override object check_charge(object amount = null)
    {
        return Dm.num(Dm.ge(charge_amount(), Dm.num(amount) * charge_modifier));
    }

    // tg: code/modules/mod/mod_core.dm:337
    public override object get_charge_icon_state()
    {
        return charge_source() == null ? SPACESUIT_CELL_MISSING : SPACESUIT_NO_ICON;
    }

    // tg: code/modules/mod/mod_core.dm:340
    public override object get_chargebar_color()
    {
        if (charge_source() == null)
            return "transparent";
        float sw__1 = Dm.num(charge_amount());
        if (sw__1 >= -INFINITY && sw__1 <= ETHEREAL_CHARGE_LOWPOWER)
            return "bad";
        else if (sw__1 >= ETHEREAL_CHARGE_LOWPOWER && sw__1 <= ETHEREAL_CHARGE_NORMAL)
            return "average";
        else if (sw__1 >= ETHEREAL_CHARGE_NORMAL && sw__1 <= ETHEREAL_CHARGE_FULL)
            return "good";
        else if (sw__1 >= ETHEREAL_CHARGE_FULL && sw__1 <= INFINITY)
            return "teal";
        return null;
    }

    // tg: code/modules/mod/mod_core.dm:353
    public override object get_chargebar_string()
    {
        obj_item_organ_stomach_ethereal charge_source = this.charge_source() as obj_item_organ_stomach_ethereal;
        if (this.charge_source() == null || charge_source.cell == null)
            return I18n.T("Biological Battery Missing");
        return base.get_chargebar_string();
    }
}

// tg: code/modules/mod/mod_core.dm:362
public partial class obj_item_mod_core_plasma : obj_item_mod_core
{
    // tg: code/modules/mod/mod_core.dm:359  #define PLASMA_CORE_ORE_CHARGE (file-local, #undef at :444): its use is the generated charger_list
    // tg: code/modules/mod/mod_core.dm:360  #define PLASMA_CORE_SHEET_CHARGE (file-local, #undef at :445): likewise

    // tg: code/modules/mod/mod_core.dm:374
    public override async DmTask install(obj_item_mod_control mod_unit = null)
    {
        await base.install(mod_unit);
        RegisterSignal(mod, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_mod_interaction));
    }

    // tg: code/modules/mod/mod_core.dm:378
    [DmParams()]
    public override async DmTask uninstall(params object[] __extra)
    {
        UnregisterSignal(mod, COMSIG_ATOM_ITEM_INTERACTION);
        await base.uninstall(__extra);
    }

    // tg: code/modules/mod/mod_core.dm:382
    public override object charge_source()
    {
        return this;
    }

    // tg: code/modules/mod/mod_core.dm:385
    public override object charge_amount()
    {
        return charge;
    }

    // tg: code/modules/mod/mod_core.dm:388
    [DmParams()]
    public override object max_charge_amount(params object[] __extra)
    {
        return maxcharge;
    }

    // tg: code/modules/mod/mod_core.dm:391
    public override async DmTask<object> add_charge(object amount = null)
    {
        charge = min(maxcharge, charge + Dm.num(amount));
        mod.update_charge_alert();
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:396
    public override async DmTask<object> subtract_charge(object amount = null)
    {
        amount = min(Dm.num(amount), charge);
        charge -= Dm.num(amount);
        mod.update_charge_alert();
        return amount;
    }

    // tg: code/modules/mod/mod_core.dm:402
    public override object check_charge(object amount = null)
    {
        return Dm.num(Dm.ge(charge_amount(), amount));
    }

    // tg: code/modules/mod/mod_core.dm:405
    public override object get_charge_icon_state()
    {
        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= 0.75f && sw__1 <= INFINITY)
            return SPACESUIT_CELL_HIGH;
        else if (sw__1 >= 0.5f && sw__1 <= 0.75f)
            return SPACESUIT_CELL_MID;
        else if (sw__1 >= 0.25f && sw__1 <= 0.5f)
            return SPACESUIT_CELL_LOW;
        else if (sw__1 >= 0.02f && sw__1 <= 0.25f)
            return SPACESUIT_CELL_VERY_LOW;

        return SPACESUIT_CELL_EMPTY;
    }

    // tg: code/modules/mod/mod_core.dm:418
    public override object get_chargebar_color()
    {
        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= -INFINITY && sw__1 <= 0.33f)
            return "bad";
        else if (sw__1 >= 0.33f && sw__1 <= INFINITY)
            return "purple";
        return null;
    }

    // tg: code/modules/mod/mod_core.dm:425
    [SignalHandler]
    public virtual object on_mod_interaction(datum source = null, mob_living user = null, obj_item thing = null)
    {
        return Waiting.NoSleep(item_interaction(user, thing));
    }

    // tg: code/modules/mod/mod_core.dm:430
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return Dm.truthy(await charge_plasma(tool as obj_item_stack, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/modules/mod/mod_core.dm:433
    public virtual async DmTask<object> charge_plasma(obj_item_stack plasma = null, mob user = null)
    {
        object charge_given = is_type_in_list(plasma, charger_list, zebra: TRUE);
        if (!Dm.truthy(charge_given))
            return FALSE;
        float uses_needed = min(Dm.num(plasma.amount), ROUND_UP(Dm.num(Dm.div(Dm.sub(max_charge_amount(), charge_amount()), charge_given))));
        if (uses_needed <= 0 || !Dm.truthy(await plasma.use(uses_needed)))
            return FALSE;
        await add_charge(uses_needed * Dm.num(charge_given));
        balloon_alert(user, I18n.T("core refueled"));
        return TRUE;
    }
}

// tg: code/modules/mod/mod_core.dm:447
public partial class obj_item_mod_core_plasma_lavaland : obj_item_mod_core_plasma
{
    // tg: code/modules/mod/mod_core.dm:463
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref particle_effect);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_core.dm:467
    public override async DmTask install(obj_item_mod_control mod_unit = null)
    {
        await base.install(mod_unit);
        RegisterSignal(mod_unit, COMSIG_MOD_TOGGLED, nameof(on_toggle));
    }

    // tg: code/modules/mod/mod_core.dm:471 (tg bug kept: every caller passes no argument, so the toggle signal stays registered)
    [DmParams("mod_unit")]
    public override async DmTask uninstall(params object[] __extra)
    {
        obj_item_mod_control mod_unit = Dm.arg<obj_item_mod_control>(__extra, 0);
        await base.uninstall(Dm.forward(__extra, mod_unit));
        UnregisterSignal(mod_unit, COMSIG_MOD_TOGGLED);
    }

    // tg: code/modules/mod/mod_core.dm:475
    [SignalHandler]
    public virtual void on_toggle()
    {
        if (Dm.truthy(mod.active))
        {
            // particle_effect = new(mod.wearer, /particles/pollen/modsuit, PARTICLE_ATTACH_MOB): the pollen around the wearer; the
            // particle holder (game/objects/effects/particle_holder.dm) is a skeleton, so it is not made (a row, not-translated.md 219)
            Dm.Deferred("code/modules/mod/mod_core.dm:478", "on_toggle: the /particles/pollen/modsuit particle holder on the wearer (particles: the particle holder is not ported)");
            RegisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED, nameof(spread_flowers));
            return;
        }

        QDEL_NULL(ref particle_effect);
        UnregisterSignal(mod.wearer, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/modules/mod/mod_core.dm:485
    [SignalHandler]
    public virtual void spread_flowers(atom source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (!isturf(oldloc))
            return;

        DmList possible_flower_types = spread_flowers__possible_flower_types ??= Dm.list(
            typeof(obj_structure_flora_bush_lavendergrass_style_random),
            typeof(obj_structure_flora_bush_flowers_yw_style_random),
            typeof(obj_structure_flora_bush_flowers_br_style_random),
            typeof(obj_structure_flora_bush_flowers_pp_style_random)
        );
        Type chosen_type = pick(possible_flower_types) as Type;
        object flower_boots = Dm.@new(chosen_type, oldloc);
        animate(flower_boots, Dm.named("alpha", 0), 1 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(flower_boots as datum) : flower_boots), 1 * SECONDS, file: "code/modules/mod/mod_core.dm", line: 499); // QDEL_IN(flower_boots, 1 SECONDS)
    }

    // tg: code/modules/mod/mod_core.dm:490  var/static/list/possible_flower_types
    private static ref DmList spread_flowers__possible_flower_types => ref TgState.Current.obj_item_mod_core_plasma_lavaland__spread_flowers__possible_flower_types;
}

// tg: code/modules/mod/mod_core.dm:658
public partial class obj_item_mod_core_candela : obj_item_mod_core
{
    // tg: code/modules/mod/mod_core.dm:672
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        handler = Dm.@new<datum_candela_item_handler>(this);
        return dot;
    }

    // tg: code/modules/mod/mod_core.dm:676
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref handler);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_core.dm:680
    public override async DmTask install(obj_item_mod_control mod_unit = null)
    {
        await base.install(mod_unit);
        RegisterSignal(mod_unit, COMSIG_MOD_WEARER_SET, nameof(on_mod_wearer_set));
        RegisterSignal(mod_unit, COMSIG_MOD_WEARER_UNSET, nameof(on_mod_wearer_unset));
        if (mod.wearer != null)
            on_mod_wearer_set(mod, mod.wearer);
    }

    // tg: code/modules/mod/mod_core.dm:687
    [DmParams()]
    public override async DmTask uninstall(params object[] __extra)
    {
        UnregisterSignal(mod, Dm.list(COMSIG_MOD_WEARER_SET, COMSIG_MOD_WEARER_UNSET));
        if (mod.wearer != null)
            on_mod_wearer_unset(mod);
        await base.uninstall(__extra);
    }

    // tg: code/modules/mod/mod_core.dm:693
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (charge >= max_charge || !Dm.truthy(mod.active))
            return null;

        if (handler.network == null)
            handler.locate_closest_network();

        if (Dm.truthy(Dm.bitand(handler.network?.powered, CANDELA_NETWORK_POWERED)))
            Waiting.NoSleep(add_charge(charge_recovery_rate * Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/modules/mod/mod_core.dm:703
    [SignalHandler]
    public virtual void on_mod_wearer_set(datum source = null, mob_living wearer = null)
    {
        handler.set_owner(wearer);
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/mod/mod_core.dm:709
    [SignalHandler]
    public virtual void on_mod_wearer_unset(datum source = null)
    {
        handler.set_owner(null);
        STOP_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/mod/mod_core.dm:715
    public override object charge_source()
    {
        return this;
    }

    // tg: code/modules/mod/mod_core.dm:718
    public override object charge_amount()
    {
        return charge;
    }

    // tg: code/modules/mod/mod_core.dm:721
    [DmParams()]
    public override object max_charge_amount(params object[] __extra)
    {
        return max_charge;
    }

    // tg: code/modules/mod/mod_core.dm:724
    public override async DmTask<object> add_charge(object amount = null)
    {
        charge = min(max_charge, charge + Dm.num(amount));
        mod.update_charge_alert();
        return TRUE;
    }

    // tg: code/modules/mod/mod_core.dm:729
    public override async DmTask<object> subtract_charge(object amount = null)
    {
        amount = min(Dm.num(amount), charge);
        charge -= Dm.num(amount);
        mod.update_charge_alert();
        return amount;
    }

    // tg: code/modules/mod/mod_core.dm:735
    public override object check_charge(object amount = null)
    {
        return Dm.num(Dm.ge(charge_amount(), amount));
    }

    // tg: code/modules/mod/mod_core.dm:738
    public override object get_charge_icon_state()
    {
        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= 0.75f && sw__1 <= INFINITY)
            return SPACESUIT_CELL_HIGH;
        else if (sw__1 >= 0.5f && sw__1 <= 0.75f)
            return SPACESUIT_CELL_MID;
        else if (sw__1 >= 0.25f && sw__1 <= 0.5f)
            return SPACESUIT_CELL_LOW;
        else if (sw__1 >= 0.02f && sw__1 <= 0.25f)
            return SPACESUIT_CELL_VERY_LOW;

        return SPACESUIT_CELL_EMPTY;
    }

    // tg: code/modules/mod/mod_core.dm:751
    public override object get_chargebar_color()
    {
        float sw__1 = round(Dm.num(Dm.div(charge_amount(), max_charge_amount())), 0.01f);
        if (sw__1 >= -INFINITY && sw__1 <= 0.33f)
            return "bad";
        else if (sw__1 >= 0.33f && sw__1 <= INFINITY)
            return "cyan";
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/mod_core.dm:490  var/static/list/possible_flower_types (in spread_flowers)
    internal DmList obj_item_mod_core_plasma_lavaland__spread_flowers__possible_flower_types;
}
