// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/power_store.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 2 (the space suit's thermal regulator runs on a cell): the power store's charge procs, Initialize, Moved, the
// overlays, examine, the magic charge, the corruption, the rigged explosion check and the corrupted cell's explosion (M5x slice 3, with
// ex_act), emp_act (M7 EMP), attack_self, get_part_rating. get_electrocute_damage is the M7 shocks slice's. The var block is generated.

/**
 * # Power store abstract type
 *
 * Abstract type for a stock part that holds power.
 */
// tg: code/modules/power/power_store.dm:11
public partial class obj_item_stock_parts_power_store : obj_item_stock_parts
{
    // tg: code/modules/power/power_store.dm:1  #define ETHEREAL_CELL_DRAIN_TIME (3.5 SECONDS) (file-local, rules 11.4; read by ethereal_drain)
    // tg: code/modules/power/power_store.dm:2  #define ETHEREAL_CELL_POWER_DRAIN (0.75 * STANDARD_CELL_CHARGE) (as above)

    // tg: code/modules/power/power_store.dm:38
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return this;
    }

    // tg: code/modules/power/power_store.dm:47
    [DmParams("mapload", "override_maxcharge")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object override_maxcharge = Dm.arg(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, override_maxcharge));
        create_reagents(5, Dm.bitor(INJECTABLE, DRAINABLE));
        if (Dm.truthy(override_maxcharge))
            maxcharge = Dm.num(override_maxcharge);
        rating = max(round(maxcharge / (rating_base * 10), 1), 1);
        if (!Dm.truthy(empty) && !Dm.truthy(charge))
            charge = maxcharge;
        if (Dm.truthy(ratingdesc))
            desc = Dm.text("[][]", desc, I18n.T(" This one has a rating of [][].", display_energy(maxcharge), prob(10) ? I18n.T(", and you should not swallow it") : "")); //joke works better if it's not on every cell
        update_appearance();

        RegisterSignal(this, COMSIG_ITEM_MAGICALLY_CHARGED, nameof(on_magic_charge));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/modules/power/power_store.dm:59  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_stock_parts_power_store__Initialize__loc_connections;

    // tg: code/modules/power/power_store.dm:66
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!isturf(old_loc))
            update_appearance();
        return dot;
    }

    /**
     * Signal proc for [COMSIG_ITEM_MAGICALLY_CHARGED]
     *
     * If we, or the item we're located in, is subject to the charge spell, gain some charge back
     */
    // tg: code/modules/power/power_store.dm:77
    [SignalHandler]
    public virtual object on_magic_charge(datum source = null, datum_action_cooldown_spell_charge spell = null, mob_living caster = null)
    {
        object dot = null;
        // This shouldn't be running if we're not being held by a mob,
        // or if we're not within an object being held by a mob, but just in case...
        if (!ismovable(loc))
            return dot;

        dot = COMPONENT_ITEM_CHARGED;

        if (prob(80))
            maxcharge -= rating_base * 0.2f;

        if (maxcharge <= 1) // Div by 0 protection
        {
            maxcharge = 1;
            dot = Dm.bitor(dot, COMPONENT_ITEM_BURNT_OUT);
        }

        charge = maxcharge;
        update_appearance();

        // Guns need to process their chamber when we've been charged
        if (loc is obj_item_gun gun_loc)
        {
            Waiting.NoSleep(gun_loc.process_chamber());
            return dot;
        }

        // The thing we're in might have overlays or icon states for whether the cell is charged
        if (!ismob(loc))
            loc.update_appearance();

        return dot;
    }

    // tg: code/modules/power/power_store.dm:108
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(grown_battery))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/cell_charger.dmi"), "grown_wires"));
        if ((charge < 0.01f) || !Dm.truthy(charge_light_type))
            return dot;
        Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/cell_charger.dmi"), Dm.text("[]-[]-o[]", cell_size_prefix, charge_light_type, (Dm.num(percent()) >= 99.5f) ? 2 : 1)));
        return dot;
    }

    /**
     * Returns the percentage of the cell's charge.
     */
    // tg: code/modules/power/power_store.dm:132
    public virtual object percent() // return % charge of cell
    {
        return 100 * charge / maxcharge;
    }

    /**
     * Returns the maximum charge of the cell.
     */
    // tg: code/modules/power/power_store.dm:138
    public virtual object max_charge()
    {
        return maxcharge;
    }

    /**
     * Returns the current charge of the cell.
     */
    // tg: code/modules/power/power_store.dm:144  (renamed charge__proc: the var charge, rules 2.4)
    public virtual object charge__proc()
    {
        return charge;
    }

    /**
     * Returns the amount of charge used on the cell.
     */
    // tg: code/modules/power/power_store.dm:150
    public virtual object used_charge()
    {
        return maxcharge - charge;
    }

    /// Use power from the cell.
    /// Args:
    /// - used: Amount of power in joules to use.
    /// - force: If true, uses the remaining power from the cell if there isn't enough power to supply the demand.
    /// Returns: The power used from the cell in joules.
    // tg: code/modules/power/power_store.dm:158
    [DmParams("used", "force")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        object force = Dm.arg(__extra, 0) ?? FALSE;
        float power_used = min(Dm.num(used), charge);
        if (power_used > 0 && Dm.truthy(await try_explode()))
            return 0; // The cell decided to explode so we won't be able to use it.
        if (!Dm.truthy(force) && charge < Dm.num(used))
            return 0;
        charge -= power_used;
        if (!(loc is obj_machinery_power_apc))
            SSblackbox.record_feedback("tally", "cell_used", 1, type);
        return power_used;
    }

    /// Recharge the cell.
    /// Args:
    /// - amount: The amount of energy to give to the cell in joules.
    /// Returns: The power given to the cell in joules.
    // tg: code/modules/power/power_store.dm:173
    public virtual async DmTask<object> give(object amount = null)
    {
        float power_used = min(maxcharge - charge, Dm.num(amount));
        charge += power_used;
        if (Dm.truthy(amount))
            await try_explode();
        return power_used;
    }

    /**
     * Changes the charge of the cell.
     * Args:
     * - amount: The energy to give to the cell (can be negative).
     * Returns: The energy that was given to the cell (can be negative).
     */
    // tg: code/modules/power/power_store.dm:186
    public virtual async DmTask<object> change(object amount = null)
    {
        float energy_used = clamp(Dm.num(amount), -charge, maxcharge - charge);
        charge += energy_used;
        if (Dm.truthy(energy_used))
            await try_explode();
        return energy_used;
    }

    // tg: code/modules/power/power_store.dm:193
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(corrupted))
            Dm.append(ref dot, span_danger(I18n.T("This [] seems to be faulty!", name)));
        else if (charge_light_type != null)
            Dm.append(ref dot, I18n.T("The charge meter reads []%.", CEILING(Dm.num(percent()), 0.1f))); //so it doesn't say 0% charge when the overlay indicates it still has charge
        return dot;
    }

    // tg: code/modules/power/power_store.dm:200
    public virtual async DmTask<object> try_explode(float? max_charge = null)
    {
        max_charge ??= FALSE;
        float check_charge = charge;
        if (Dm.truthy(max_charge))
            check_charge = maxcharge;

        if (!Dm.truthy(check_charge))
            return FALSE;

        if (!Dm.truthy(corrupted))
        {
            if (!Dm.truthy(Dm.bitand(reagents == null ? null : await ((datum_reagents)reagents).spark_act(check_charge, SPARK_ACT_ENCLOSED), SPARK_ACT_DESTRUCTIVE)))
                return FALSE;
            message_admins(Dm.text("[] has triggered a rigged power cell explosion at [].", ADMIN_LOOKUPFLW(usr), AREACOORD(loc)));
            usr?.log_message("triggered a rigged power cell explosion", LOG_GAME);
            usr?.log_message("triggered a rigged power cell explosion", LOG_VICTIM, log_globally: FALSE);
            qdel(this);
            return TRUE;
        }

        float range_heavy = round(sqrt(check_charge / (3.6f * rating_base)));
        float range_light = round(sqrt(check_charge / (0.9f * rating_base)));
        if (!Dm.truthy(range_light))
        {
            corrupt();
            return FALSE;
        }

        message_admins(Dm.text("[] has triggered a corrupted power cell explosion at [].", ADMIN_LOOKUPFLW(usr), AREACOORD(loc)));
        usr?.log_message("triggered a corrupted power cell explosion", LOG_GAME);
        usr?.log_message("triggered a corrupted power cell explosion", LOG_VICTIM, log_globally: FALSE);
        await explosion(this, devastation_range: -1, heavy_impact_range: range_heavy, light_impact_range: range_light, flash_range: range_light);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/power/power_store.dm:236
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await use((STANDARD_CELL_CHARGE / Dm.num(severity)) * emp_damage_modifier, TRUE);
        return dot;
    }

    // tg: code/modules/power/power_store.dm:242  (M5x slice 3; set waitfor = FALSE, inherited from /atom/ex_act: the wrapper
    // of waiting-model.md 5.4)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object dot = base.ex_act(severity, target, __extra);
        if (QDELETED(this))
            return FALSE;

        switch (Dm.num(severity))
        {
            case EXPLODE_HEAVY:
                if (prob(50))
                    corrupt();
                break;
            case EXPLODE_LIGHT:
                if (prob(25))
                    corrupt();
                break;
        }

        return TRUE;
    }

    // tg: code/modules/power/power_store.dm:230
    public virtual void corrupt(object force = null)
    {
        charge /= 2;
        maxcharge = max(maxcharge / 2, chargerate);
        if (Dm.truthy(force) || prob(10))
            corrupted = TRUE;
    }

    // tg: code/modules/power/power_store.dm:41
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(charge));
        Dm.append(ref dot, nameof(corrupted));
        return dot;
    }

    // tg: code/modules/power/power_store.dm:116
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, nameof(charge)))
        {
            charge = clamp(Dm.num(var_value), 0, maxcharge);
            return TRUE;
        }
        if (Dm.eq(var_name, nameof(maxcharge)))
        {
            if (charge > Dm.num(var_value))
                charge = Dm.num(var_value);
        }
        if (Dm.eq(var_name, nameof(corrupted)) && Dm.truthy(var_value) && !Dm.truthy(corrupted))
        {
            corrupt(TRUE);
            return TRUE;
        }
        return await base.vv_edit_var(var_name, var_value);
    }

    // tg: code/modules/power/power_store.dm:257
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is licking the electrodes of []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        do_sparks(2, TRUE, user);
        object eating_success = await do_after(user, 5 * SECONDS, this);
        if (QDELETED(user))
            return SHAME;
        if (!Dm.truthy(eating_success) || QDELETED(this) || charge == 0)
        {
            user.visible_message(span_suicide(I18n.T("[] chickens out!", user)));
            return SHAME;
        }
        playsound(user, "sound/effects/sparks/sparks1.ogg", charge / maxcharge);
        float damage = charge / (1 * KILO * JOULES);
        float discharged_energy = charge;
        await user.electrocute_act(damage, this, 1, Dm.bitor(SHOCK_IGNORE_IMMUNITY, Dm.bitor(SHOCK_DELAY_STUN, SHOCK_NOGLOVES)));
        charge = 0;
        update_appearance();
        if (!Dm.eq(user.stat, DEAD))
        {
            to_chat(user, span_suicide(I18n.T("There's not enough charge in [] to kill you!", this)));
            return SHAME;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(gib_user), user, discharged_energy), 3 * SECONDS, file: "code/modules/power/power_store.dm", line: 275);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/power/power_store.dm:278
    public virtual async DmTask gib_user(mob_living user = null, object discharged_energy = null)
    {
        if (QDELETED(user))
            return;
        if (Dm.num(discharged_energy) < STANDARD_BATTERY_CHARGE)
            return;
        await user.dropItemToGround(this);
        await user.dust(just_ash: TRUE, drop_items: TRUE);
        playsound(this, "sound/effects/magic/lightningshock.ogg", 50, TRUE, 10);
        await tesla_zap(source: this, zap_range: 10, power: discharged_energy);
    }

    // tg: code/modules/power/power_store.dm:288
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!ishuman(user))
            return dot;

        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        obj_item_organ_stomach_ethereal user_stomach = human_user.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach_ethereal;
        if (!(user_stomach is obj_item_organ_stomach_ethereal))
            return dot;
        if (user_stomach.drain_time > world.time)
            return dot;

        await ethereal_drain(human_user, user_stomach);
        return dot;
    }

    // tg: code/modules/power/power_store.dm:335
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        Dm.append(ref SSexplosions.high_mov_atom, this);
        return null;
    }

    // tg: code/modules/power/power_store.dm:338
    public virtual object get_electrocute_damage()
    {
        return ELECTROCUTE_DAMAGE(Dm.div(charge, max(0.001f * STANDARD_CELL_CHARGE, 1))); // Wouldn't want it to consider more energy than whatever is actually in the cell if for some strange reason someone set the STANDARD_CELL_CHARGE to below 1kJ.
    }

    // tg: code/modules/power/power_store.dm:341
    public override object get_part_rating()
    {
        return maxcharge * 10 + charge;
    }

    // tg: code/modules/power/power_store.dm:1
    private const float ETHEREAL_CELL_DRAIN_TIME = 3.5f * SECONDS;
    // tg: code/modules/power/power_store.dm:2
    private const float ETHEREAL_CELL_POWER_DRAIN = 0.75f * STANDARD_CELL_CHARGE;
    // tg: code/modules/power/power_store.dm:4
    private const float ETHEREAL_CELL_POWER_GAIN_FACTOR = 0.08f;

    /// Handles letting an ethereal drain our charge into their stomach
    // tg: code/modules/power/power_store.dm:306
    public virtual async DmTask ethereal_drain(mob_living_carbon_human user = null, obj_item_organ_stomach_ethereal used_stomach = null)
    {
        if (Dm.num(charge__proc()) <= 0)
        {
            balloon_alert(user, I18n.T("out of charge!"));
            return;
        }

        obj_item_stock_parts_power_store stomach_cell = used_stomach.cell;
        used_stomach.drain_time = Dm.num(world.time) + ETHEREAL_CELL_DRAIN_TIME;
        to_chat(user, span_notice(I18n.T("You begin clumsily channeling power from [] into your body.", this)));

        while (Dm.truthy(await do_after(user, ETHEREAL_CELL_DRAIN_TIME, target: this)))
        {
            if (used_stomach == null || (used_stomach != user.get_organ_slot(ORGAN_SLOT_STOMACH)))
            {
                balloon_alert(user, I18n.T("stomach removed!?"));
                return;
            }

            float our_charge = Dm.num(charge__proc());
            float scaled_stomach_used_charge = Dm.num(stomach_cell.used_charge()) / ETHEREAL_CELL_POWER_GAIN_FACTOR;
            float potential_charge = min(our_charge, scaled_stomach_used_charge);
            float to_drain = min(ETHEREAL_CELL_POWER_DRAIN, potential_charge);
            float energy_drained = Dm.num(await use(to_drain, TRUE));
            await used_stomach.adjust_charge(energy_drained * ETHEREAL_CELL_POWER_GAIN_FACTOR);
            update_appearance(UPDATE_OVERLAYS);

            if (Dm.num(stomach_cell.used_charge()) <= 0)
            {
                balloon_alert(user, I18n.T("your charge is full!"));
                return;
            }
            if (Dm.num(charge__proc()) <= 0)
            {
                balloon_alert(user, I18n.T("out of charge!"));
                return;
            }
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/power_store.dm:59  var/static/list/loc_connections = list(COMSIG_ITEM_MAGICALLY_CHARGED = PROC_REF(on_magic_charge))
    internal DmList obj_item_stock_parts_power_store__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ITEM_MAGICALLY_CHARGED, nameof(obj_item_stock_parts_power_store.on_magic_charge)));
}
