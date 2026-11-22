// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/atmos_requirements.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but check_safe_environment's unit-test branch (PERFORM_ALL_TESTS, FALSE in production, rules 1.7) (gaps package C1).

/**
 * ## atmos requirements element!
 *
 * bespoke element that deals damage to the attached mob when the atmos requirements aren't satisfied
 */
// tg: code/datums/elements/atmos_requirements.dm:6
public partial class datum_element_atmos_requirements : datum_element
{
    // tg: code/datums/elements/atmos_requirements.dm:15
    [DmParams("target", "atmos_requirements", "unsuitable_atmos_damage", "mapload")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList atmos_requirements = Dm.arg<DmList>(__extra, 0);
        object unsuitable_atmos_damage = Dm.arg(__extra, 1) ?? 5;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(atmos_requirements))
            throw Dm.CRASH(Dm.text("[] added to [] without any requirements specified.", type, target), "code/datums/elements/atmos_requirements.dm", 20);
        DmList @short = atmos_requirements;
        if (!Dm.truthy(@short["min_oxy"]) && !Dm.truthy(@short["max_oxy"]) && !Dm.truthy(@short["min_plas"]) && !Dm.truthy(@short["max_plas"]) && !Dm.truthy(@short["min_co2"]) && !Dm.truthy(@short["max_co2"]) && !Dm.truthy(@short["min_n2"]) && !Dm.truthy(@short["max_n2"]))
            throw Dm.CRASH(Dm.text("[] added to [] with all requirements set to 0.", type, target), "code/datums/elements/atmos_requirements.dm", 23);
        this.atmos_requirements = atmos_requirements;
        this.unsuitable_atmos_damage = unsuitable_atmos_damage;
        RegisterSignal(target, COMSIG_LIVING_HANDLE_BREATHING, nameof(on_non_stasis_life));
        return dot;
    }

    // tg: code/datums/elements/atmos_requirements.dm:31
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_HANDLE_BREATHING);
        return dot;
    }

    ///signal called by the living mob's life() while non stasis
    // tg: code/datums/elements/atmos_requirements.dm:36
    [SignalHandler]
    public virtual void on_non_stasis_life(mob_living target = null, object seconds_per_tick = null)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (Dm.truthy(is_breathable_atmos(target)))
        {
            target.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            return;
        }
        Waiting.NoSleep(target.adjust_brute_loss(Dm.num(unsuitable_atmos_damage) * Dm.num(seconds_per_tick)));
        target.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy));
    }

    // tg: code/datums/elements/atmos_requirements.dm:44
    public virtual object is_breathable_atmos(mob_living target = null)
    {
        if (Dm.truthy(target.pulledby) && Dm.num(((mob)target.pulledby).grab_state) >= GRAB_KILL && Dm.truthy(atmos_requirements["min_oxy"]))
            return FALSE;

        if (!isopenturf(target.loc))
            return TRUE;

        bool can_breathe_vacuum = HAS_TRAIT(target, TRAIT_NO_BREATHLESS_DAMAGE) || HAS_TRAIT(target, TRAIT_NOBREATH);

        float min_oxy = can_breathe_vacuum ? 0 : Dm.num(atmos_requirements["min_oxy"]);
        float min_plasma = can_breathe_vacuum ? 0 : Dm.num(atmos_requirements["min_plas"]);
        float min_n2 = can_breathe_vacuum ? 0 : Dm.num(atmos_requirements["min_n2"]);
        float min_co2 = can_breathe_vacuum ? 0 : Dm.num(atmos_requirements["min_co2"]);

        turf_open open_turf = (turf_open)target.loc;
        if (open_turf.air == null)
        {
            if (can_breathe_vacuum)
                return TRUE;
            if (Dm.truthy(min_oxy) || Dm.truthy(min_plasma) || Dm.truthy(min_n2) || Dm.truthy(min_co2))
                return FALSE;
            return TRUE;
        }

        DmList gases = get_atmos_req_list(open_turf) as DmList;

        if (!in_range_req(gases["oxy"], min_oxy, atmos_requirements["max_oxy"]))
            return FALSE;
        if (!in_range_req(gases["plas"], min_plasma, atmos_requirements["max_plas"]))
            return FALSE;
        if (!in_range_req(gases["n2"], min_n2, atmos_requirements["max_n2"]))
            return FALSE;
        if (!in_range_req(gases["co2"], min_co2, atmos_requirements["max_co2"]))
            return FALSE;
        return TRUE;
    }

    // ISINRANGE(gas, min, (max || INFINITY))
    private static bool in_range_req(object gas, float min, object max)
    {
        float value = Dm.num(gas);
        return value >= min && value <= (Dm.truthy(max) ? Dm.num(max) : INFINITY);
    }

    // tg: code/datums/elements/atmos_requirements.dm:81
    public virtual object get_atmos_req_list(turf_open open_turf = null)
    {
        datum_gas_mixture air = open_turf.air as datum_gas_mixture;
        DmList open_turf_moles = air.moles;
        air.assert_gases(typeof(datum_gas_oxygen), typeof(datum_gas_pluoxium), typeof(datum_gas_nitrogen), typeof(datum_gas_carbon_dioxide), typeof(datum_gas_plasma));

        DmList return_gases = Dm.list();
        return_gases["plas"] = open_turf_moles[typeof(datum_gas_plasma)];
        return_gases["oxy"] = Dm.num(open_turf_moles[typeof(datum_gas_oxygen)]) + (Dm.num(open_turf_moles[typeof(datum_gas_pluoxium)]) * PLUOXIUM_PROPORTION);
        return_gases["n2"] = open_turf_moles[typeof(datum_gas_nitrogen)];
        return_gases["co2"] = open_turf_moles[typeof(datum_gas_carbon_dioxide)];

        air.garbage_collect();

        return return_gases;
    }

    ///Ensures that maploaded mobs are in a safe environment. Unit test stuff.
    // tg: code/datums/elements/atmos_requirements.dm:96
    public virtual void check_safe_environment(mob_living living_mob = null)
    {
        Dm.Deferred("code/datums/elements/atmos_requirements.dm:96", "check_safe_environment: a tg unit test's map check (never called in production, PERFORM_ALL_TESTS)");
    }
}
