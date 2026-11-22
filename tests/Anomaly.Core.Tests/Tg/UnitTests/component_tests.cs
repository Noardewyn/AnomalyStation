// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/component_tests.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// tg: code/modules/unit_tests/component_tests.dm:1 (/datum/unit_test/component_duping; class named by rules 2.3)
// typesof(/datum/component) is every component type the type registry knows: the translated ones and the test
// fixtures (Tg/Pilot/Datum, Tg/Pilot/Dcs). In the port /datum/component/var/dupe_mode is a float (rules 3.2 V4), so a
// type cannot give it a value that is not a number and the check holds by construction; what the test still checks is
// that the prototype of every component type can be built and read (object-model.md section 5).
public sealed class datum_unit_test_component_duping : TgUnitTest
{
    // tg: code/modules/unit_tests/component_tests.dm:1
    public override async DmTask Run()
    {
        DmList bad_dms = Dm.list();
        foreach (object t in Dm.each(typesof(typeof(datum_component))))
        {
            // RULE-GAP: var/datum/component/comp = t holds a typepath read from a list; 3.4 gives it the declared class,
            // whose as-downcast would null the path; it is Type (the intent of V3), as the dcs translation does.
            Type comp = t as Type;
            if (!isnum(Dm.initial(comp, (datum_component p) => p.dupe_mode)))
                Dm.append(ref bad_dms, t);
        }
        TEST_ASSERT(!Dm.truthy(length(bad_dms)), Dm.text("Components with invalid dupe modes: ([])", bad_dms.Join(",")));
    }
}

/// <summary>Checks of the port of component_tests.dm itself, so that its pass is not vacuous.</summary>
public class ComponentDupingPortTests
{
    [Fact]
    public void TheTestWalksEveryComponentTypeTheRegistryKnows()
    {
        DmList types = typesof(typeof(datum_component));
        Assert.Equal(typeof(datum_component), types[1]);
        Assert.Contains(typeof(Anomaly.Core.Tests.Tg.Pilot.Dcs.test_dcs_allowed_sub_deeper), Dm.each(types));
        Assert.Contains(typeof(Anomaly.Core.Tests.Tg.Pilot.Dcs.test_dcs_sources), Dm.each(types));
        // The recording base of the fixtures is a C# class without [DmPath]: no DM type.
        Assert.DoesNotContain(typeof(Anomaly.Core.Tests.Tg.Pilot.Dcs.dcs_recording_component), Dm.each(types));
    }
}
