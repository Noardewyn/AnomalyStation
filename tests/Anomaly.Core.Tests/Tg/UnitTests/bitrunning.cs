// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/bitrunning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (gaps BR1, D339): the room is the toy room (TgToyRoom.Install).

/// Ensures settings on vdoms are correct
// tg: code/modules/unit_tests/bitrunning.dm:2 (/datum/unit_test/bitrunner_vdom_settings)
public sealed class datum_unit_test_bitrunner_vdom_settings : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/bitrunning.dm:4
    public override async DmTask Run()
    {
        obj_structure_closet_crate_secure_bitrunning_decrypted cache = allocate<obj_structure_closet_crate_secure_bitrunning_decrypted>();

        foreach (Type path in Dm.each<Type>(subtypesof(typeof(datum_lazy_template_virtual_domain))))
        {
            datum_lazy_template_virtual_domain vdom = Dm.@new(path) as datum_lazy_template_virtual_domain;
            TEST_ASSERT_NOTNULL(vdom.key, Dm.text("[] should have a key", path));
            TEST_ASSERT_NOTNULL(vdom.map_name, Dm.text("[] should have a map name", path));

            if (length(vdom.completion_loot) == 0)
                continue;

            TEST_ASSERT_EQUAL(await cache.spawn_loot(vdom.completion_loot), TRUE, Dm.text("[] didn't spawn loot. Completion loot should be an associative list", path));
        }
    }
}
