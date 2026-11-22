// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/thermoregulation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/** Thermoregulation
 * No change to stealth.
 * Increases resistance.
 * Reduces stage speed.
 * Reduces transmissibility
 * Bonus: Regulates body temperature.
 */
// tg: code/datums/diseases/advance/symptoms/thermoregulation.dm:9
public partial class datum_symptom_thermoregulation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/thermoregulation.dm:22
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalResistance()) >= 8)
            power = 1.75f;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/thermoregulation.dm:29
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        if (A.stage >= 3)
        {
            mob_living_carbon carbon_host = A.affected_mob;
            float difference = Dm.num(((datum_species)carbon_host.dna.species).bodytemp_normal) - carbon_host.bodytemperature;
            // tg's chained comparison: (cold_limit < bodytemperature) < heat_limit, kept as it is
            if (!(Dm.num(Dm.num(((datum_species)carbon_host.dna.species).bodytemp_cold_damage_limit) < carbon_host.bodytemperature) < Dm.num(((datum_species)carbon_host.dna.species).bodytemp_heat_damage_limit))) // No need to spam chat
                to_chat(carbon_host, span_notice(I18n.T("You feel a [] spread through your body.", difference < 0 ? I18n.T("warmth") : I18n.T("chill"))));
            float stage_power = (A.stage == 3) ? Dm.num(power) * 5 : Dm.num(power) * 10; // Half as strong at stage 3
            carbon_host.adjust_bodytemperature(clamp(difference, -stage_power, stage_power));
        }
        return dot;
    }
}
