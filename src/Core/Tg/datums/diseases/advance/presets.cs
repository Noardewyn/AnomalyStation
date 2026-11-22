// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/presets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the cold and flu presets and the random disease of the virus crates and the events. The var
// blocks are generated (D111).

// Cold
// tg: code/datums/diseases/advance/presets.dm:2
public partial class datum_disease_advance_cold : datum_disease_advance
{
    // tg: code/datums/diseases/advance/presets.dm:5
    public override async DmTask New(params object[] __extra)
    {
        name = "Cold";
        symptoms = Dm.list(Dm.@new<datum_symptom_sneeze>());
        await base.New(__extra);
    }
}

// Flu
// tg: code/datums/diseases/advance/presets.dm:11
public partial class datum_disease_advance_flu : datum_disease_advance
{
    // tg: code/datums/diseases/advance/presets.dm:14
    public override async DmTask New(params object[] __extra)
    {
        name = "Flu";
        symptoms = Dm.list(Dm.@new<datum_symptom_cough>());
        await base.New(__extra);
    }
}

//Randomly generated Disease, for virus crates and events
// tg: code/datums/diseases/advance/presets.dm:20
public partial class datum_disease_advance_random : datum_disease_advance
{
    // tg: code/datums/diseases/advance/presets.dm:24
    [DmParams("max_symptoms", "max_level")]
    public override async DmTask New(params object[] __extra)
    {
        object max_symptoms = Dm.arg(__extra, 0);
        object max_level = Dm.arg(__extra, 1);
        max_level ??= 8;
        if (!Dm.truthy(max_symptoms))
            max_symptoms = rand(1, VIRUS_SYMPTOM_LIMIT);
        DmList possible_symptoms = Dm.list();
        foreach (object symptom in Dm.each(subtypesof(typeof(datum_symptom))))
        {
            Type S = (Type)symptom;
            if (Dm.gt(Dm.initial(S, (datum_symptom p) => p.level), max_level))
                continue;
            if (Dm.num(Dm.initial(S, (datum_symptom p) => p.level)) <= 0) //unobtainable symptoms
                continue;
            Dm.append(possible_symptoms, S);
        }
        for (float i = 1, i__end = Dm.num(max_symptoms); i <= i__end; i++)
        {
            Type chosen_symptom = pick_n_take(possible_symptoms) as Type;
            if (chosen_symptom != null)
            {
                datum_symptom S = (datum_symptom)Dm.@new(chosen_symptom);
                Dm.append(symptoms, S);
            }
        }
        await Refresh();

        name = Dm.text("Sample #[]", rand(1, 10000));
    }
}
