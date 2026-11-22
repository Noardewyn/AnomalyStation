// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (the reagent forms of the gases that a lung puts in the blood: freon's, halon's and nitrium's
// slowdown, healium's healing sleep, the hypernoblium protection of a plasmaman, the nitrosyl plasmide's toxins, pluoxium healing a
// sleeper's organs, zauker). The var blocks are generated (D111).

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:1
public partial class datum_reagent_freon : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:10
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_freon));
    }

    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:14
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_freon));
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:18
public partial class datum_reagent_halon : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:28
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_halon));
    }

    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:32
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_halon));
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:36
public partial class datum_reagent_healium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:45
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        await affected_mob.SetSleeping(1 * SECONDS);
    }

    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:49
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var breather = (mob_living)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await breather.SetSleeping(30 * SECONDS);
        float need_mob_update;
        need_mob_update = Dm.num(await breather.adjust_fire_loss(-2 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await breather.adjust_tox_loss(-5 * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await breather.adjust_brute_loss(-2 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:59
public partial class datum_reagent_hypernoblium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:68
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var breather = (mob_living_carbon)affected_mob;
        if (isplasmaman(breather))
            await breather.set_timed_status_effect(10 * SECONDS * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), typeof(datum_status_effect_hypernob_protection));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:73
public partial class datum_reagent_nitrium_high_metabolization : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:85
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var breather = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await breather.adjust_stamina_loss(-4 * k, updating_stamina: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await breather.adjust_tox_loss(0.1f * (current_cycle - 1) * k, updating_health: FALSE, required_biotype: affected_biotype)); // 1 toxin damage per cycle at cycle 10
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:93
public partial class datum_reagent_nitrium_low_metabolization : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:103
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        await affected_mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nitrium));
    }

    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:107
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        affected_mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_nitrium));
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:111
public partial class datum_reagent_pluoxium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:120
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var breather = (mob_living_carbon)affected_mob;
        if (!IS_UNCONSCIOUS(breather))
            return dot;

        foreach (obj_item_organ organ_being_healed in Dm.each_cast<obj_item_organ>(breather.organs))
        {
            if (!Dm.truthy(organ_being_healed.damage))
                continue;

            if (Dm.truthy(await organ_being_healed.apply_organ_damage(-0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), required_organ_flag: ORGAN_ORGANIC)))
                dot = UPDATE_MOB_HEALTH;
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:132
public partial class datum_reagent_zauker : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/atmos_gas_reagents.dm:141
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var breather = (mob_living)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        float need_mob_update;
        need_mob_update = Dm.num(await breather.adjust_brute_loss(6 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await breather.adjust_oxy_loss(1 * k, updating_health: FALSE, required_biotype: affected_biotype));
        need_mob_update += Dm.num(await breather.adjust_fire_loss(2 * k, updating_health: FALSE, required_bodytype: affected_bodytype));
        need_mob_update += Dm.num(await breather.adjust_tox_loss(2 * k, updating_health: FALSE, required_biotype: affected_biotype));
        if (Dm.truthy(need_mob_update))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}
