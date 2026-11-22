// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/viral.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var blocks are generated (D111).

/*Viral adaptation
 * Greatly increases stealth
 * Tremendous buff for resistance
 * Greatly decreases stage speed
 * No effect to transmissibility
 *
 * Bonus: Buffs resistance & stealth. Extremely useful for buffing viruses
*/
// tg: code/datums/diseases/advance/symptoms/viral.dm:9
public partial class datum_symptom_viraladaptation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/viral.dm:20
    [DmParams("our_disease")]
    public override void OnAdd(datum_disease_advance our_disease = null)
    {
        base.OnAdd(our_disease);
        our_disease.NeuterSymptom(this);
    }
}

/*Viral evolution
 * Reduces stealth
 * Greatly reduces resistance
 * Tremendous buff for stage speed
 * Greatly increases transmissibility
 *
 * Bonus: Buffs transmission and speed. Extremely useful for buffing viruse*
*/
// tg: code/datums/diseases/advance/symptoms/viral.dm:33
public partial class datum_symptom_viralevolution : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/viral.dm:44
    [DmParams("our_disease")]
    public override void OnAdd(datum_disease_advance our_disease = null)
    {
        base.OnAdd(our_disease);
        our_disease.NeuterSymptom(this);
    }
}
