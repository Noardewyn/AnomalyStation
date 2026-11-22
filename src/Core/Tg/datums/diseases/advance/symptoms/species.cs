// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var blocks are generated (D111).

/* Necrotic Metabolism
 * Increases stealth
 * Reduces resistance
 * Slightly increases stage speed
 * No effect to transmissibility
 * Critical level
 * Bonus: Infected corpses spread disease and undead species are infectable
*/
// tg: code/datums/diseases/advance/symptoms/species.dm:9
public partial class datum_symptom_undead_adaptation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/species.dm:21
    public override void OnAdd(datum_disease_advance A = null)
    {
        A.process_dead = TRUE;
        Dm.combine(ref A.infectable_biotypes, MOB_UNDEAD);
    }

    // tg: code/datums/diseases/advance/symptoms/species.dm:25
    public override void OnRemove(datum_disease_advance A = null)
    {
        A.process_dead = FALSE;
        Dm.mask(ref A.infectable_biotypes, Dm.bitnot(MOB_UNDEAD));
    }
}

/* Inorganic Biology
 * Slight stealth reduction
 * Tremendous resistance increase
 * Reduces stage speed
 * Greatly increases transmissibility
 * Critical level
 * Bonus: Enables infection of mineral biotype species
*/
// tg: code/datums/diseases/advance/symptoms/species.dm:37
public partial class datum_symptom_inorganic_adaptation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/species.dm:49
    public override void OnAdd(datum_disease_advance A = null)
    {
        Dm.combine(ref A.infectable_biotypes, MOB_MINERAL | MOB_ROBOTIC); // Plasmamen, golems, and androids.
    }

    // tg: code/datums/diseases/advance/symptoms/species.dm:52
    public override void OnRemove(datum_disease_advance A = null)
    {
        Dm.mask(ref A.infectable_biotypes, Dm.bitnot(MOB_MINERAL | MOB_ROBOTIC));
    }
}
