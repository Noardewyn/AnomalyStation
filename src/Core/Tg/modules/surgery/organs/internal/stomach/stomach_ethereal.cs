// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, the ethereal's battery: what APCs, cells and fixtures charge and drain. The species itself is not ported, so only a
// stomach placed by hand runs it. The var block keeps cell by hand: tg assigns a typepath to other vars of that name (rules 3.2, V3).

// tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:1
public partial class obj_item_organ_stomach_ethereal : obj_item_organ_stomach
{
    /// Where the energy of the stomach is stored.
    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:7
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:2  the var overrides of the type block (the class declares cell by hand, so the block is here)
    public obj_item_organ_stomach_ethereal()
    {
        name = "biological battery";
        icon_state = "stomach-p"; //Welp. At least it's more unique in functionaliy.
        desc = "A crystal-like organ that stores the electric charge of ethereals.";
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:5
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        organ_traits = Dm.list(TRAIT_NOHUNGER, TRAIT_ETHEREAL_METABOLISM); // We have our own hunger mechanic.
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_ethereal>(this);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:15
    public override object Destroy(float? force = null)
    {
        qdel(cell);
        cell = null;
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:19
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        await adjust_charge(-ETHEREAL_DISCHARGE_RATE * Dm.num(seconds_per_tick));
        await handle_charge(owner, seconds_per_tick);
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:24
    public override async DmTask<object> on_mob_insert(mob_living_carbon stomach_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(stomach_owner, special, movement_flags);
        RegisterSignal(stomach_owner, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(charge));
        RegisterSignal(stomach_owner, COMSIG_LIVING_ELECTROCUTE_ACT, nameof(on_electrocute));
        RegisterSignal(stomach_owner, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL), nameof(on_multitool_act));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:30
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon stomach_owner = organ_owner as mob_living_carbon;
        UnregisterSignal(stomach_owner, COMSIG_PROCESS_BORGCHARGER_OCCUPANT);
        UnregisterSignal(stomach_owner, COMSIG_LIVING_ELECTROCUTE_ACT);
        UnregisterSignal(stomach_owner, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL));
        stomach_owner.clear_mood_event("charge");
        stomach_owner.clear_alert(ALERT_ETHEREAL_CHARGE);
        stomach_owner.clear_alert(ALERT_ETHEREAL_OVERCHARGE);
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:39
    [SignalHandler]
    public virtual void charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null)
    {
        Waiting.NoSleep(charge_cell.Invoke(cell, Dm.num(seconds_per_tick) / 3.5f)); // Ethereals don't have NT designed charging ports, so they charge slower.
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:44
    [SignalHandler]
    public virtual void on_electrocute(datum source = null, object shock_damage = null, object shock_source = null, float? siemens_coeff = null, float? flags = null)
    {
        siemens_coeff ??= 1;
        flags ??= NONE;
        if (Dm.truthy(Dm.bitand(flags, SHOCK_ILLUSION)))
            return;
        Waiting.NoSleep(adjust_charge(Dm.num(shock_damage) * siemens_coeff * 2));
        to_chat(owner, span_notice(I18n.T("You absorb some of the shock into your body!")));
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:51
    [SignalHandler]
    public virtual object on_multitool_act(atom source = null, mob user = null, obj_item tool = null)
    {
        // Checks to ensure we're not doing surgery on a robotic part.
        if (owner == null)
            return Waiting.NoSleep(multitool_act(user as mob_living, tool));

        obj_item_bodypart targeted_part = owner.get_bodypart(user.zone_selected) as obj_item_bodypart;
        if (targeted_part == null || !Dm.eq(targeted_part.bodytype, BODYTYPE_ROBOTIC))
            return Waiting.NoSleep(multitool_act(user as mob_living, tool));

        if (HAS_TRAIT(targeted_part, TRAIT_READY_TO_OPERATE))
            return null;

        return Waiting.NoSleep(multitool_act(user as mob_living, tool));
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:66
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        // Intentionally formatted in the exact same way as multitooling a cable for comedic effect.
        // It's as if the multitool is mistaking the ethereal/biological battery for a cable.
        string power_info;
        if (cell.charge > 0)
            power_info = span_danger(I18n.T("Total power: []\nLoad: []\nExcess power: []", display_power(cell.charge), (owner != null && !Dm.eq(owner.stat, DEAD)) ? display_power(ETHEREAL_DISCHARGE_RATE) : 0, display_power(max(0, cell.charge - ETHEREAL_CHARGE_FULL))));
        else
            power_info = span_danger(I18n.T("The [] is not powered.", owner != null ? owner.name : name));

        to_chat(user, power_info);
        return ITEM_INTERACT_SUCCESS;
    }

    /**Changes the energy of the crystal stomach.
    * Args:
    * - amount: The change of the energy, in joules.
    * Returns: The amount of energy that actually got changed in joules.
    **/
    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:83
    public virtual async DmTask<object> adjust_charge(object amount = null)
    {
        float amount_changed = clamp(Dm.num(amount), ETHEREAL_CHARGE_NONE - Dm.num(cell.charge__proc()), ETHEREAL_CHARGE_DANGEROUS - Dm.num(cell.charge__proc()));
        return await cell.change(amount_changed);
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:87
    public virtual async DmTask handle_charge(mob_living_carbon carbon = null, object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        float sw__1 = Dm.num(cell.charge__proc());
        if (sw__1 <= ETHEREAL_CHARGE_NONE)
        {
            await carbon.add_mood_event("charge", typeof(datum_mood_event_decharged));
            carbon.throw_alert(ALERT_ETHEREAL_CHARGE, typeof(atom_movable_screen_alert_emptycell_ethereal));
            if (carbon.health > 10.5f)
                await carbon.apply_damage(0.65f, TOX, forced: TRUE);
        }
        else if (sw__1 <= ETHEREAL_CHARGE_LOWPOWER)
        {
            await carbon.add_mood_event("charge", typeof(datum_mood_event_decharged));
            carbon.throw_alert(ALERT_ETHEREAL_CHARGE, typeof(atom_movable_screen_alert_lowcell_ethereal), 3);
            if (carbon.health > 10.5f)
                await carbon.apply_damage(0.325f * spt, TOX, forced: TRUE);
        }
        else if (sw__1 <= ETHEREAL_CHARGE_NORMAL)
        {
            await carbon.add_mood_event("charge", typeof(datum_mood_event_lowpower));
            carbon.throw_alert(ALERT_ETHEREAL_CHARGE, typeof(atom_movable_screen_alert_lowcell_ethereal), 2);
        }
        else if (sw__1 >= ETHEREAL_CHARGE_ALMOSTFULL && sw__1 <= ETHEREAL_CHARGE_FULL)
            await carbon.add_mood_event("charge", typeof(datum_mood_event_charged));
        else if (sw__1 >= ETHEREAL_CHARGE_FULL && sw__1 <= ETHEREAL_CHARGE_OVERLOAD)
        {
            await carbon.add_mood_event("charge", typeof(datum_mood_event_overcharged));
            carbon.throw_alert(ALERT_ETHEREAL_OVERCHARGE, typeof(atom_movable_screen_alert_ethereal_overcharge), 1);
            await carbon.apply_damage(0.2f, TOX, forced: TRUE);
        }
        else if (sw__1 >= ETHEREAL_CHARGE_OVERLOAD && sw__1 <= ETHEREAL_CHARGE_DANGEROUS)
        {
            await carbon.add_mood_event("charge", typeof(datum_mood_event_supercharged));
            carbon.throw_alert(ALERT_ETHEREAL_OVERCHARGE, typeof(atom_movable_screen_alert_ethereal_overcharge), 2);
            await carbon.apply_damage(0.325f * spt, TOX, forced: TRUE);
            if (SPT_PROB(5, spt)) // 5% each seacond for ethereals to explosively release excess energy if it reaches dangerous levels
                await discharge_process(carbon);
        }
        else
        {
            owner.clear_mood_event("charge");
            carbon.clear_alert(ALERT_ETHEREAL_CHARGE);
            carbon.clear_alert(ALERT_ETHEREAL_OVERCHARGE);
        }
    }

    // tg: code/modules/surgery/organs/internal/stomach/stomach_ethereal.dm:124
    public virtual async DmTask discharge_process(mob_living_carbon carbon = null)
    {
        to_chat(carbon, span_warning(I18n.T("You begin to lose control over your charge!")));
        carbon.visible_message(span_danger(I18n.T("[] begins to spark violently!", carbon)));

        mutable_appearance overcharge = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "electricity", EFFECTS_LAYER); //shameless copycode from lightning spell
        carbon.add_overlay(overcharge);

        if (Dm.truthy(await do_after(carbon, 5 * SECONDS, timed_action_flags: Dm.bitor(IGNORE_USER_LOC_CHANGE, Dm.bitor(IGNORE_HELD_ITEM, IGNORE_INCAPACITATED)))))
        {
            if (ishuman(carbon))
            {
                mob_living_carbon_human human = carbon as mob_living_carbon_human;
                if (human.dna?.species != null)
                {
                    //fixed_mut_color is also ethereal color (for some reason)
                    carbon.flash_lighting_fx(5, 7, Dm.str(Dm.truthy(((datum_species)human.dna.species).fixed_mut_color) ? ((datum_species)human.dna.species).fixed_mut_color : human.dna.features[FEATURE_MUTANT_COLOR]));
                }
            }

            playsound(carbon, "sound/effects/magic/lightningshock.ogg", 100, TRUE, extrarange: 5);
            carbon.cut_overlay(overcharge);
            // Only a small amount of the energy gets discharged as the zap. The rest dissipates as heat. Keeps the damage and energy from the zap the same regardless of what STANDARD_CELL_CHARGE is.
            float discharged_energy = -Dm.num(await adjust_charge(ETHEREAL_CHARGE_FULL - Dm.num(cell.charge__proc()))) * min(7500 / STANDARD_CELL_CHARGE, 1);
            await tesla_zap(source: carbon, zap_range: 2, power: discharged_energy, cutoff: 1 * KILO * JOULES, zap_flags: Dm.bitor(ZAP_OBJ_DAMAGE, Dm.bitor(ZAP_LOW_POWER_GEN, ZAP_ALLOW_DUPLICATES)));
            carbon.visible_message(span_danger(I18n.T("[] violently discharges energy!", carbon)), span_warning(I18n.T("You violently discharge energy!")));

            if (prob(10)) //chance of developing heart disease to dissuade overcharging oneself
            {
                await carbon.apply_status_effect(typeof(datum_status_effect_heart_attack));
                to_chat(carbon, span_userdanger(I18n.T("You're pretty sure you just felt your heart stop for a second there..")));
                carbon.playsound_local(carbon, "sound/effects/singlebeat.ogg", 100, 0);
            }

            await carbon.Paralyze(100);
        }
    }
}
