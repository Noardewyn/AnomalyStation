// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/combat_stamina.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// ADAPTED (M8 slice B): the room is the toy room (TgToyRoom.Install), a world without a Master, so tg's sleep runs SStimer's fire
// every tick by hand (CombatStamina.sleep, as timer_sanity does): the stamina reset is a timer (damage_procs.dm:490).

internal static class CombatStamina
{
    /// <summary>tg's sleep(ds) with SStimer firing every tick, as the Master would.</summary>
    public static async DmTask sleep(float ds)
    {
        float until = Dm.num(world.time) + ds;
        while (Dm.num(world.time) < until)
        {
            SStimer.state = SS_RUNNING;
            await SStimer.fire(FALSE);
            SStimer.state = SS_IDLE;
            await Dm.sleep(world.tick_lag);
        }
        SStimer.state = SS_RUNNING;
        await SStimer.fire(FALSE);
        SStimer.state = SS_IDLE;
    }
}

/// Tests 100 stamina damage = stamcrit
// tg: code/modules/unit_tests/combat_stamina.dm:2 (/datum/unit_test/stamcrit)
public sealed class datum_unit_test_stamcrit : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/combat_stamina.dm:5
    public override async DmTask Run()
    {
        mob_living_carbon_human tider = allocate<mob_living_carbon_human_consistent>();
        tider.stamina_regen_time = 0.2f * SECONDS;
        float stamloss_to_reach_crit_threshold = tider.maxHealth;
        await tider.adjust_stamina_loss(stamloss_to_reach_crit_threshold - 1);
        TEST_ASSERT(!Dm.truthy(tider.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))), Dm.text("Stamcrit should not be applied at [] stamina damage", stamloss_to_reach_crit_threshold - 1));
        await tider.adjust_stamina_loss(1);
        TEST_ASSERT(Dm.truthy(tider.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))), Dm.text("Stamcrit should be applied at [] stamina damage", stamloss_to_reach_crit_threshold));
        await CombatStamina.sleep(tider.stamina_regen_time * 2);
        TEST_ASSERT(!Dm.truthy(tider.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))), "Stamcrit should be removed after regen time");
    }
}

/// Tests stamina regen after the set time
// tg: code/modules/unit_tests/combat_stamina.dm:18 (/datum/unit_test/stam_regen)
public sealed class datum_unit_test_stam_regen : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/combat_stamina.dm:21
    public override async DmTask Run()
    {
        mob_living_carbon_human tider = allocate<mob_living_carbon_human_consistent>();
        tider.stamina_regen_time = 0.2f * SECONDS;
        await tider.adjust_stamina_loss(50);
        await CombatStamina.sleep(tider.stamina_regen_time * 2);
        TEST_ASSERT_EQUAL(tider.get_stamina_loss(), 0, "Stamina should be fully regenerated after regen time");
    }
}
