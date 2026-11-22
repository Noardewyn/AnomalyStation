// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/stacked_metab.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// The whole test (M8 slice A).

// tg: code/modules/unit_tests/stacked_metab.dm:1
public sealed class datum_unit_test_stacked_metabolization_effect_verify : TgUnitTest
{
    // tg: code/modules/unit_tests/stacked_metab.dm:3
    public override async DmTask Run()
    {
        foreach (Type effect_type in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_stacked_metabolization_effect))))
        {
            datum_stacked_metabolization_effect effect = Dm.@new(effect_type) as datum_stacked_metabolization_effect;

            if (!Dm.truthy(length(effect.requirements)))
            {
                TEST_FAIL(Dm.text("Effect [] does not have any requirments!", effect));
                qdel(effect);
                continue;
            }

            foreach (object key in Dm.each(effect.requirements))
            {
                if (!ispath(key, typeof(datum_reagent)))
                {
                    TEST_FAIL(Dm.text("Effect [] has an non reagent key [] as requirment", effect, key));
                    break;
                }

                object count = effect.requirements[key];
                if (Dm.num(count) < 1)
                {
                    TEST_FAIL(Dm.text("Effect [] has an invalid requirement count [] for key [] ", effect, count, key));
                    break;
                }
            }

            qdel(effect);
        }
    }
}
