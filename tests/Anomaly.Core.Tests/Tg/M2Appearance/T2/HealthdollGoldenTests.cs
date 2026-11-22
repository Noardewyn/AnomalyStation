using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M2Appearance.T2.AppearanceRig;

namespace Anomaly.Core.Tests.Tg.M2Appearance.T2;

/// <summary>
/// The health doll and the health indicator of a real human (D107), written from the DM: the doll's limb states
/// (screen_objects.dm:986-1036 update_icon_state): "[zone][key]" with key clamp(ceil(damage / max_damage x 5), 0, 5) from the
/// part's brute plus burn, 6 for a missing part, 7 for a disabled one, "DEAD" for every limb of a dead owner; the indicator
/// (carbon.dm:574-598): health0 at full health, health[6 - ceil(health / (maxHealth x 0.2))] while alive and above zero,
/// health6 at zero or below, health7 when dead. The owner is a human with a client and tg's create_mob_hud.
/// </summary>
public class HealthdollGoldenTests
{
    private static string Doll(AppearanceRig rig, string zone)
        => rig.Room.In(() => ((atom)((atom_movable_screen_healthdoll_human)rig.Human.hud_used.screen_objects[HUD_MOB_HEALTHDOLL]).limbs[zone]).icon_state);

    private static string Indicator(AppearanceRig rig)
        => rig.Room.In(() => ((atom)rig.Human.hud_used.screen_objects[HUD_MOB_HEALTH]).icon_state);

    [Fact]
    public void AHealthyDollIsZeroInEveryZoneAndTheIndicatorIsFull()
    {
        var rig = new AppearanceRig(1901, hud: true);
        foreach (string zone in Zones)
            Assert.Equal(zone + "0", Doll(rig, zone));
        Assert.Equal("health0", Indicator(rig));
        rig.Room.AssertNoRuntimes();
    }

    // each limb holds 50 (the chest and the head 200); 1 point away from each fifth: 9 and 11, 19 and 21, ... (the whole 200 of the
    // chest or the head kills: every limb then shows DEAD, see below)
    public static TheoryData<string, float, int> DollCases()
    {
        var data = new TheoryData<string, float, int>();
        foreach (string zone in new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_LEG })
            foreach ((float damage, int key) in new (float, int)[] { (1, 1), (9, 1), (11, 2), (19, 2), (21, 3), (29, 3), (31, 4), (39, 4), (41, 5), (49, 5), (50, 5) })
                data.Add(zone, damage, key);
        foreach (string zone in new[] { BODY_ZONE_CHEST, BODY_ZONE_HEAD })
            foreach ((float damage, int key) in new (float, int)[] { (1, 1), (39, 1), (41, 2), (79, 2), (81, 3), (119, 3), (121, 4), (159, 4), (161, 5), (199, 5) })
                data.Add(zone, damage, key);
        return data;
    }

    [Theory]
    [MemberData(nameof(DollCases))]
    public void AZonesStateIsTheCeilingOfItsDamageInFifths(string zone, float damage, int key)
    {
        var rig = new AppearanceRig(1902, hud: true);
        rig.Damage(BRUTE, damage, zone);
        Assert.Equal(damage, rig.Room.In(() => rig.Part(zone).get_damage() is var d ? Dm.num(d) : -1), 2);
        Assert.Equal(zone + key, Doll(rig, zone));
        foreach (string other in Zones.Where(z => z != zone))
            Assert.Equal(other + "0", Doll(rig, other));
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void BruteAndBurnAddUpInTheSamePart()
    {
        var rig = new AppearanceRig(1903, hud: true);
        rig.Damage(BRUTE, 10f, BODY_ZONE_L_ARM);
        Assert.Equal("l_arm1", Doll(rig, BODY_ZONE_L_ARM)); // 10/50 = 0.2 -> exactly a fifth: key 1
        rig.Damage(BURN, 11f, BODY_ZONE_L_ARM);
        Assert.Equal("l_arm3", Doll(rig, BODY_ZONE_L_ARM)); // 21/50 -> 2.1 -> 3
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void ADisabledPartShowsSevenAndItsOwnStateOnceItIsEnabledAgain()
    {
        // a human's limb is disabled by paralysis of the limb (disabling_threshold_percentage is LIMB_NO_DISABLE: damage alone
        // never disables it, _bodyparts.dm:947-956)
        var rig = new AppearanceRig(1904, hud: true);
        rig.Damage(BRUTE, 25f, BODY_ZONE_R_ARM);
        obj_item_bodypart arm = rig.Part(BODY_ZONE_R_ARM);
        rig.Room.Command(() =>
        {
            ADD_TRAIT(arm, TRAIT_PARALYSIS, "t2");
            Waiting.NoSleep(arm.update_disabled());
        });
        rig.Room.Step();
        Assert.True(Dm.truthy(rig.Room.In(() => arm.bodypart_disabled)));
        Assert.Equal("r_arm7", Doll(rig, BODY_ZONE_R_ARM));
        rig.Room.Command(() =>
        {
            REMOVE_TRAIT(arm, TRAIT_PARALYSIS, "t2");
            Waiting.NoSleep(arm.update_disabled());
        });
        rig.Room.Step();
        Assert.Equal("r_arm3", Doll(rig, BODY_ZONE_R_ARM)); // 25/50 = 0.5 -> 2.5 -> 3
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadOwnersLimbsAllShowDead()
    {
        var rig = new AppearanceRig(1905, hud: true);
        rig.Damage(BRUTE, 45f, BODY_ZONE_CHEST);
        rig.Room.Command(() => Waiting.NoSleep(rig.Human.death()));
        rig.Room.Step();
        Assert.Equal(DEAD, Dm.num(rig.Room.In(() => rig.Human.stat)));
        foreach (string zone in Zones)
            Assert.Equal(zone + "DEAD", Doll(rig, zone));
        Assert.Equal("health7", Indicator(rig));
        rig.Room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(0, "health0")]     // full health
    [InlineData(1, "health1")]     // 99: 6 - ceil(4.95) = 1
    [InlineData(19, "health1")]    // 81: ceil(4.05) = 5
    [InlineData(21, "health2")]    // 79: ceil(3.95) = 4
    [InlineData(39, "health2")]    // 61: ceil(3.05) = 4
    [InlineData(41, "health3")]    // 59: ceil(2.95) = 3
    public void TheIndicatorIsSixMinusTheCeilingOfTheHealthInFifths(int oxy, string expected)
    {
        var rig = new AppearanceRig(1906, hud: true);
        if (oxy > 0)
        {
            rig.Room.Command(() => Waiting.NoSleep(rig.Human.adjust_oxy_loss(oxy)));
            rig.Room.Step();
        }
        Assert.Equal(100f - oxy, rig.Room.In(() => rig.Human.health));
        Assert.Equal(expected, Indicator(rig));
        rig.Room.AssertNoRuntimes();
    }
}
