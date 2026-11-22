// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/confusion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Gaps VI1 (D341): the whole file. ADAPTED: the room is the toy room (TgToyRoom.Install).

// Checks that the confusion symptom correctly gives, and removes, confusion
// tg: code/modules/unit_tests/confusion.dm:2 (/datum/unit_test/confusion_symptom)
public sealed class datum_unit_test_confusion_symptom : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/confusion.dm:2
    public override async DmTask Run()
    {
        mob_living_carbon_human dummy = allocate<mob_living_carbon_human_consistent>();
        datum_disease_advance_confusion disease = allocate<datum_disease_advance_confusion>();
        datum_symptom_confusion confusion = (datum_symptom_confusion)disease.symptoms[1];
        disease.processing = TRUE;
        await disease.update_stage(5);
        await disease.infect(dummy, make_copy: FALSE);
        await confusion.Activate(disease);
        TEST_ASSERT(Dm.truthy(dummy.has_status_effect(typeof(datum_status_effect_confusion))), "Human is not confused after getting symptom.");
        await disease.cure();
        TEST_ASSERT(!Dm.truthy(dummy.has_status_effect(typeof(datum_status_effect_confusion))), "Human is still confused after curing confusion.");
    }
}

// tg: code/modules/unit_tests/confusion.dm:14
[DmPath("/datum/disease/advance/confusion")]
public class datum_disease_advance_confusion : datum_disease_advance
{
    // tg: code/modules/unit_tests/confusion.dm:14
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(symptoms, Dm.@new<datum_symptom_confusion>());
        await Refresh();
    }
}
