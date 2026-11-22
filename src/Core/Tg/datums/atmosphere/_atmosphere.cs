// Translated from tgstation (snapshot 1d99bd7f): code/datums/atmosphere/_atmosphere.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "atmosphere": SSair.preprocess_gas_string makes every /datum/atmosphere at the first
// gas string it parses, generate_atmos, air.dm:772): the randomised gas string of a planetary atmosphere.

// tg: code/datums/atmosphere/_atmosphere.dm:1
[DmPath("/datum/atmosphere")]
public partial class datum_atmosphere : datum
{
    // tg: code/datums/atmosphere/_atmosphere.dm:2
    public object gas_string;
    // tg: code/datums/atmosphere/_atmosphere.dm:3
    public object id;

    // tg: code/datums/atmosphere/_atmosphere.dm:5
    public DmList base_gases; // A list of gases to always have
    // tg: code/datums/atmosphere/_atmosphere.dm:6
    public DmList normal_gases; // A list of allowed gases:base_amount
    // tg: code/datums/atmosphere/_atmosphere.dm:7
    public DmList restricted_gases; // A list of allowed gases like normal_gases but each can only be selected a maximum of one time
    // tg: code/datums/atmosphere/_atmosphere.dm:8
    public float restricted_chance = 10; // Chance per iteration to take from restricted gases

    // tg: code/datums/atmosphere/_atmosphere.dm:10
    public object minimum_pressure;
    // tg: code/datums/atmosphere/_atmosphere.dm:11
    public object maximum_pressure;

    // tg: code/datums/atmosphere/_atmosphere.dm:13
    public object minimum_temp;
    // tg: code/datums/atmosphere/_atmosphere.dm:14
    public object maximum_temp;

    // tg: code/datums/atmosphere/_atmosphere.dm:16
    public override async DmTask New(params object[] __extra)
    {
        generate_gas_string();
    }

    // tg: code/datums/atmosphere/_atmosphere.dm:19
    public virtual void generate_gas_string()
    {
        DmList spicy_gas = restricted_gases.Copy();
        float target_pressure = rand(Dm.num(minimum_pressure), Dm.num(maximum_pressure));
        float pressure_scalar = target_pressure / Dm.num(maximum_pressure);

        // rules 10.6 (settled at the M2 integration): tg's SSstation always exists here (the Master makes every subsystem before any turf); the port's test
        // harnesses build turfs without it, so a missing SSstation reads as no station trait instead of a runtime error.
        if (SSstation != null && HAS_TRAIT(SSstation, STATION_TRAIT_UNNATURAL_ATMOSPHERE))
            restricted_chance = restricted_chance + 40;

        // First let's set up the gasmix and base gases for this template
        // We make the string from a gasmix in this proc because gases need to calculate their pressure
        datum_gas_mixture gasmix = Dm.@new<datum_gas_mixture>();
        gasmix.temperature = rand(Dm.num(minimum_temp), Dm.num(maximum_temp));
        DmList cached_moles = gasmix.moles;
        foreach (object i in Dm.each(base_gases))
            cached_moles[i] = base_gases[i];

        // Now let the random choices begin
        object gastype = null;
        object amount = null;
        while (Dm.num(gasmix.return_pressure()) < target_pressure)
        {
            if (!prob(restricted_chance) || !Dm.truthy(length(spicy_gas)))
            {
                gastype = pick(normal_gases);
                amount = normal_gases[gastype];
            }
            else
            {
                gastype = pick(spicy_gas);
                amount = spicy_gas[gastype];
                Dm.remove(ref spicy_gas, gastype); //You can only pick each restricted gas once
            }

            amount = Dm.mul(amount, rand(50, 200) / 100f); // Randomly modifes the amount from half to double the base for some variety
            amount = Dm.mul(amount, pressure_scalar); // If we pick a really small target pressure we want roughly the same mix but less of it all
            amount = CEILING(Dm.num(amount), 0.1f);

            Dm.append_at(cached_moles, gastype, amount);
        }

        // Ensure that minimum_pressure is actually a hard lower limit
        target_pressure = clamp(target_pressure, Dm.num(minimum_pressure) + (Dm.num(cached_moles[gastype]) * 0.1f), Dm.num(maximum_pressure));

        // That last one put us over the limit, remove some of it
        while (Dm.num(gasmix.return_pressure()) > target_pressure)
            cached_moles[gastype] = Dm.num(cached_moles[gastype]) - Dm.num(cached_moles[gastype]) * 0.1f;
        cached_moles[gastype] = FLOOR(Dm.num(cached_moles[gastype]), 0.1f);
        gasmix.garbage_collect();

        // Now finally lets make that string
        DmList gas_string_builder = Dm.list();
        DmList cached_gas_id = (DmList)datum_gas_mixture.gas_meta[META_GAS_ID];
        foreach ((object gas_id, object gas_amount) in Dm.pairs(cached_moles))
            Dm.append(ref gas_string_builder, Dm.text("[]=[]", cached_gas_id[gas_id], gas_amount));
        Dm.append(ref gas_string_builder, Dm.text("TEMP=[]", gasmix.temperature));
        gas_string = gas_string_builder.Join(";");
    }
}
