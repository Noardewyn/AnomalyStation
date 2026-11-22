// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/huds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice F): the room is the toy room (TgToyRoom.Install); verify_basic_huds (the basic mobs' screen objects) is not ported:
// basic mobs and their HUDs are M10.

/// Tests huds are applied and removed correctly when relevant traits are added/removed
// tg: code/modules/unit_tests/huds.dm:2 (/datum/unit_test/huds)
public sealed class datum_unit_test_huds : TgUnitTest
{
    // tg: code/modules/unit_tests/_unit_tests.dm:99  #define TRAIT_SOURCE_UNIT_TESTS "unit_tests" (inside #if defined(UNIT_TESTS))
    private const string TRAIT_SOURCE_UNIT_TESTS = "unit_tests";

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/huds.dm:4
    public override async DmTask Run()
    {
        datum_atom_hud testhud = GLOB.huds[GLOB.trait_to_hud[TRAIT_SECURITY_HUD]] as datum_atom_hud;
        mob_living_carbon_human_consistent dummy = allocate<mob_living_carbon_human_consistent>();

        ADD_TRAIT(dummy, TRAIT_SECURITY_HUD, TRAIT_SOURCE_UNIT_TESTS);
        TEST_ASSERT(Dm.truthy(testhud.hud_users_all_z_levels[dummy]), "HUD not applied when trait of HUD was added");

        ADD_TRAIT(dummy, TRAIT_BLOCK_SECHUD, TRAIT_SOURCE_UNIT_TESTS);
        TEST_ASSERT(!Dm.truthy(testhud.hud_users_all_z_levels[dummy]), "HUD not removed when trait blocking HUD was added");

        REMOVE_TRAIT(dummy, TRAIT_BLOCK_SECHUD, TRAIT_SOURCE_UNIT_TESTS);
        TEST_ASSERT(Dm.truthy(testhud.hud_users_all_z_levels[dummy]), "HUD not reapplied when trait blocking HUD was removed");

        REMOVE_TRAIT(dummy, TRAIT_SECURITY_HUD, TRAIT_SOURCE_UNIT_TESTS);
        TEST_ASSERT(!Dm.truthy(testhud.hud_users_all_z_levels[dummy]), "HUD not removed when trait of HUD was removed");
    }
}
