// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/anchored_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// tg: code/modules/unit_tests/anchored_mobs.dm:1 (/datum/unit_test/anchored_mobs; class named by rules 2.3)
// Over the mob types the port declares (typesof also lists the test assembly's fixture mobs, which keep tg's default).
public sealed class datum_unit_test_anchored_mobs : TgUnitTest
{
    // tg: code/modules/unit_tests/anchored_mobs.dm:1
    public override async DmTask Run()
    {
        DmList L = Dm.list();
        foreach (object i in Dm.each(typesof(typeof(Anomaly.Core.Tg.mob))))
        {
            Type M = i as Type; // var/mob/M = i: a typepath read from a list (rules 3.2, V3)
            if (Dm.truthy(Dm.initial(M, (Anomaly.Core.Tg.mob p) => p.anchored)))
                Dm.append(ref L, Dm.text("[]", i));
        }
        TEST_ASSERT(!Dm.truthy(L.len), Dm.text("The following mobs are defined as anchored. This is incompatible with the new move force/resist system and needs to be revised.: []", L.Join(" ")));
    }
}
