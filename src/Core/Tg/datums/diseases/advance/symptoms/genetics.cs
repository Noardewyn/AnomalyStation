// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/genetics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

// tg: code/datums/diseases/advance/symptoms/genetics.dm:10
public partial class datum_symptom_genetic_mutation : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/genetics.dm:34
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStealth()) >= 5) //only give them bad mutations
            excludemuts = POSITIVE;
        if (Dm.num(A.totalStageSpeed()) >= 10) //activate dormant mutations more often at around 1.5x the pace
            symptom_delay = 30;
        if (Dm.num(A.totalResistance()) >= 8) //mutadone won't save you now
            mutadone_proof = (NEGATIVE | MINOR_NEGATIVE);
        if (Dm.num(A.totalResistance()) >= 14) //one does not simply escape Nurgle's grasp
            no_reset = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/genetics.dm:47
    [DmParams("disease")]
    public override async DmTask<object> Activate(datum_disease_advance disease = null)
    {
        object dot = null;
        dot = await base.Activate(disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon carbon = disease.affected_mob;
        if (!Dm.truthy(carbon.has_dna()))
            return dot;
        if (disease.stage == 4 || disease.stage == 5)
        {
            to_chat(carbon, span_warning(I18n.T(Dm.str(pick(Dm.list("Your skin feels itchy.", "You feel light headed."))))));
            Type mutation = carbon.get_random_mutation_path(Dm.bitand(NEGATIVE | MINOR_NEGATIVE | POSITIVE, Dm.bitnot(excludemuts))) as Type;
            if (mutation == null)
                return dot;
            await carbon.dna.add_mutation(mutation, Dm.truthy(Dm.bitand(Dm.initial(mutation, (datum_mutation p) => p.quality), mutadone_proof)) ? MUTATION_SOURCE_GENE_SYMPTOM : MUTATION_SOURCE_ACTIVATED);
            datum_mutation given_mutation = carbon.dna.get_mutation(mutation) as datum_mutation;
            if (given_mutation != null)
                given_mutation.scrambled = TRUE;
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/genetics.dm:64
    public override async DmTask<object> End(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.End(A);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(no_reset))
        {
            mob_living_carbon M = A.affected_mob;
            if (Dm.truthy(M.has_dna()))
                await M.dna.remove_all_mutations(Dm.list(MUTATION_SOURCE_GENE_SYMPTOM, MUTATION_SOURCE_ACTIVATED));
        }
        return dot;
    }
}
