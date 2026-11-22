// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/unequip_defib.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice E): the room is the toy room (TgToyRoom.Install); EASY_ALLOCATE() is allocate of the declared type.

/// Test you can mouse-drop a defib off your back to unequip it
// tg: code/modules/unit_tests/unequip_defib.dm:2 (/datum/unit_test/unequip_defib)
public sealed class datum_unit_test_unequip_defib : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/unequip_defib.dm:4
    public override async DmTask Run()
    {
        mob_living_carbon_human_consistent dummy = allocate<mob_living_carbon_human_consistent>();
        dummy.mock_client = Dm.@new<datum_client_interface>();
        await dummy.set_hud_used(Dm.@new(dummy.hud_type, dummy) as datum_hud);
        obj_item_defibrillator defib = allocate<obj_item_defibrillator>();
        await dummy.equip_to_slot(defib, ITEM_SLOT_BACK);

        mob old_usr = usr;

        usr = dummy; // mouse drop still uses usr

        await defib.MouseDrop(dummy.hud_used.screen_objects[datum_hud.HUD_KEY_HAND_SLOT(1)] as atom);
        if (!Dm.truthy(dummy.is_holding(defib)))
            TEST_FAIL("The dummy failed to remove the defib from their back via mouse drop.");

        usr = old_usr;
    }
}
