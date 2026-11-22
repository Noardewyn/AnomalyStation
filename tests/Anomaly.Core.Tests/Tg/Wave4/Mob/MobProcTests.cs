using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using Anomaly.Core.Tests.Tg.Wave4.Movable;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The procs of code/modules/mob/mob.dm that M0 uses, one contract at a time: incapacitation (:465-483), set_stat
/// (:1515), nutrition (:1447-1484), spin (:738-770), magic checks (:982-1017), the small defaults (:446-462, :1216-1330),
/// focus.dm and the HUD signal procs. Every test is written from the DM and runs in a world with NotTranslatedThrows on.
/// </summary>
public class MobProcTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    // ---- update_incapacitated, build_incapacitated ----

    [Fact]
    public void UpdateIncapacitatedSignalsEachChangeWithTheOldAndNewFlagsAndNothingForNoChange()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_incap>();
            var listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(m, COMSIG_MOB_INCAPACITATE_CHANGED, nameof(datum_test_mob_listener.on_incapacitate_changed));

            Assert.True(Dm.eq(m.update_incapacitated(), FALSE)); // NONE -> NONE
            Assert.Empty(listener.heard);

            m.incap_flags = INCAPABLE_RESTRAINTS;
            Assert.True(Dm.eq(m.update_incapacitated(), TRUE));
            Assert.True(Dm.eq(m.update_incapacitated(), FALSE)); // unchanged now
            m.incap_flags = INCAPABLE_RESTRAINTS | INCAPABLE_STASIS;
            Assert.True(Dm.eq(m.update_incapacitated(), TRUE));
            Assert.Equal((float)(INCAPABLE_RESTRAINTS | INCAPABLE_STASIS), m.incapacitated);
            m.incap_flags = NONE;
            Assert.True(Dm.eq(m.update_incapacitated(), TRUE));
            Assert.Equal(0f, m.incapacitated);

            Assert.Equal(new[]
            {
                "INCAPACITATE_CHANGED 0 -> " + Dm.str(INCAPABLE_RESTRAINTS),
                "INCAPACITATE_CHANGED " + Dm.str(INCAPABLE_RESTRAINTS) + " -> " + Dm.str(INCAPABLE_RESTRAINTS | INCAPABLE_STASIS),
                "INCAPACITATE_CHANGED " + Dm.str(INCAPABLE_RESTRAINTS | INCAPABLE_STASIS) + " -> 0",
            }, listener.heard);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ThePlainMobIsNeverIncapacitated()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.True(Dm.eq(m.build_incapacitated(), NONE));
            Assert.True(Dm.eq(m.update_incapacitated(), FALSE));
        });
    }

    // ---- set_stat ----

    [Fact]
    public void SetStatReturnsTheOldStatAndSignalsOnlyForARealChange()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(m, COMSIG_MOB_STATCHANGE, nameof(datum_test_mob_listener.on_statchange));
            Assert.Equal((float)STABLE, m.stat);
            Assert.Null(Waiting.NoSleep(m.set_stat(STABLE))); // `if(new_stat == stat) return`
            Assert.Empty(listener.heard);
            Assert.True(Dm.eq(Waiting.NoSleep(m.set_stat(HARD_CRIT)), STABLE));
            Assert.True(Dm.eq(Waiting.NoSleep(m.set_stat(DEAD)), HARD_CRIT));
            Assert.Equal((float)DEAD, m.stat);
            Assert.Equal(new[]
            {
                "STATCHANGE " + Dm.str(HARD_CRIT) + " from " + Dm.str(STABLE),
                "STATCHANGE " + Dm.str(DEAD) + " from " + Dm.str(HARD_CRIT),
            }, listener.heard);
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- nutrition ----

    [Fact]
    public void NutritionNeverGoesBelowZeroForEitherProc()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.set_nutrition(-40));
            Assert.Equal(0f, m.nutrition); // max(0, set_to)
            Waiting.NoSleep(m.set_nutrition(120));
            Waiting.NoSleep(m.adjust_nutrition(30.5f));
            Assert.Equal(150.5f, m.nutrition);
            Waiting.NoSleep(m.adjust_nutrition(-1000));
            Assert.Equal(0f, m.nutrition);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ATraitNoHungerBlocksUnforcedChangesOfEitherProc()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.set_nutrition(100));
            ADD_TRAIT(m, TRAIT_NOHUNGER, "test");
            Waiting.NoSleep(m.adjust_nutrition(50));
            Waiting.NoSleep(m.set_nutrition(7));
            Assert.Equal(100f, m.nutrition);
            Waiting.NoSleep(m.adjust_nutrition(50, forced: TRUE));
            Assert.Equal(150f, m.nutrition);
            Waiting.NoSleep(m.set_nutrition(7, forced: TRUE));
            Assert.Equal(7f, m.nutrition);
        });
    }

    [Theory]
    [InlineData(5.9f, false)]
    [InlineData(6f, true)]
    [InlineData(-6f, true)] // abs(change) (:1455)
    [InlineData(-5f, false)]
    [InlineData(100f, true)]
    public void ALivingMobsAdjustNutritionUpdatesAtOnceFromSixAndQueuesBelow(float change, bool updatesAtOnce)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_living>();
            var listener = Dm.@new<datum_test_mob_living_nutrition_listener>();
            listener.RegisterSignal(m, COMSIG_LIVING_UPDATE_NUTRITION, nameof(datum_test_mob_living_nutrition_listener.on_update));
            Waiting.NoSleep(m.set_nutrition(300)); // a big jump: one update already (below), then count only the adjust
            listener.updates = 0;
            m.living_flags = Dm.bitand(m.living_flags, Dm.bitnot(QUEUE_NUTRITION_UPDATE)); // Initialize's random nutrition may have queued one
            Waiting.NoSleep(m.adjust_nutrition(change));
            Assert.Equal(updatesAtOnce ? 1f : 0f, listener.updates);
            // M2 P1: living_flags is declared, so a change below 6 queues the update for the next Life (mob.dm:1459,
            // living_flags |= QUEUE_NUTRITION_UPDATE) instead of counting a deferred leaf.
            Assert.Equal(!updatesAtOnce, Dm.truthy(Dm.bitand(m.living_flags, QUEUE_NUTRITION_UPDATE)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Theory]
    [InlineData(300f, 306f, true)] // from the mob's own value: a difference of exactly 6
    [InlineData(300f, 305f, false)]
    [InlineData(300f, 100f, true)]
    [InlineData(300f, 300f, false)]
    public void ALivingMobsSetNutritionUpdatesAtOnceWhenTheValueMovedBySixOrMore(float from, float to, bool updatesAtOnce)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_living>();
            Waiting.NoSleep(m.set_nutrition(from, forced: TRUE));
            var listener = Dm.@new<datum_test_mob_living_nutrition_listener>();
            listener.RegisterSignal(m, COMSIG_LIVING_UPDATE_NUTRITION, nameof(datum_test_mob_living_nutrition_listener.on_update));
            Waiting.NoSleep(m.set_nutrition(to));
            Assert.Equal(to, m.nutrition);
            Assert.Equal(updatesAtOnce ? 1f : 0f, listener.updates);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ALivingMobsBlockedNutritionChangeCountsAsNoChange()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_living>();
            Waiting.NoSleep(m.set_nutrition(300, forced: TRUE));
            ADD_TRAIT(m, TRAIT_NOHUNGER, "test");
            var listener = Dm.@new<datum_test_mob_living_nutrition_listener>();
            listener.RegisterSignal(m, COMSIG_LIVING_UPDATE_NUTRITION, nameof(datum_test_mob_living_nutrition_listener.on_update));
            Waiting.NoSleep(m.set_nutrition(10)); // no hunger: ignored, so |old - new| = 0
            Assert.Equal(300f, m.nutrition);
            Assert.Equal(0f, listener.updates);
        });
    }

    // ---- spin ----

    [Theory]
    [InlineData(NORTH, new[] { EAST, SOUTH, WEST, NORTH })]
    [InlineData(EAST, new[] { SOUTH, WEST, NORTH, EAST })]
    [InlineData(SOUTH, new[] { WEST, NORTH, EAST, SOUTH })]
    [InlineData(WEST, new[] { NORTH, EAST, SOUTH, WEST })]
    public void SpinTurnsTheMobClockwiseOneQuarterEverySpeedDecisecondsFromEveryCardinalDirection(float start, float[] expected)
    {
        var room = new MobRoom();
        TgMob m = null;
        var dirs = new List<float>();
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.dir = start;
            m.spin(4, 1);
        });
        for (int i = 0; i < 8; i++)
        {
            room.World.Step();
            dirs.Add(m.dir);
        }
        // sleep(1) is 2 ticks, the turn comes after each sleep (mob.dm:757-765)
        Assert.Equal(expected.Select(d => (float)d), new[] { dirs[1], dirs[3], dirs[5], dirs[7] });
        Assert.Equal(dirs[0], (float)start); // nothing turns before the first sleep ends
        using (room.World.Enter())
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void SpinReturnsAtOnceAddsTheTraitFromItsOwnSourceAndRemovesItAtTheEnd()
    {
        var room = new MobRoom();
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.dir = SOUTH;
            m.spin(2, 1); // set waitfor = 0: the caller never waits
            Assert.True(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.True(HAS_TRAIT_FROM(m, TRAIT_SPINNING, "trait_from_spin()"));
            Assert.Equal((float)SOUTH, m.dir);
        });
        room.World.Step(3);
        using (room.World.Enter())
            Assert.True(HAS_TRAIT(m, TRAIT_SPINNING)); // 4 ticks needed for two quarter turns of 1 ds
        room.World.Step(1);
        using (room.World.Enter())
        {
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.Equal((float)NORTH, m.dir); // SOUTH -> WEST -> NORTH
        }
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-3f, 1f)]
    [InlineData(0.5f, 1f)]
    [InlineData(4f, 0f)]
    [InlineData(4f, -1f)]
    [InlineData(4f, 0.5f)]
    [InlineData(null, 1f)]
    [InlineData(4f, null)]
    public void SpinWithATimeOrASpeedBelowOneOrMissingDoesNothingAtAll(float? spintime, float? speed)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.dir = NORTH;
            m.spin(spintime, speed);
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
            room.World.Step();
            Assert.Equal((float)NORTH, m.dir);
        });
        room.World.Step(4);
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void SpinWithATimeThatIsNotAMultipleOfTheSpeedMakesOnlyWholeQuarterTurns()
    {
        // `while(spintime >= speed)`: 5 and 2 -> 5, 3 (two turns), then 1 < 2
        var room = new MobRoom();
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.dir = NORTH;
            m.spin(5, 2);
        });
        room.World.Step(7); // two sleeps of 2 ds = 8 ticks
        using (room.World.Enter())
        {
            Assert.True(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.Equal((float)EAST, m.dir);
        }
        room.World.Step(1);
        using (room.World.Enter())
        {
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.Equal((float)SOUTH, m.dir);
        }
    }

    [Fact]
    public void TgQuirk_SpinKeepsADiagonalDirectionBecauseTheSwitchHasNoCase()
    {
        var room = new MobRoom();
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.dir = NORTHEAST;
            m.spin(2, 1);
        });
        room.World.Step(5);
        using (room.World.Enter())
        {
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.Equal((float)NORTHEAST, m.dir);
        }
    }

    [Fact]
    public void TgQuirk_TwoOverlappingSpinsShareOneTraitSourceSoTheFirstToEndRemovesIt()
    {
        var room = new MobRoom();
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.spin(2, 1); // ends after 4 ticks
            m.spin(4, 1); // ends after 8 ticks
        });
        room.World.Step(4);
        using (room.World.Enter())
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING)); // both added the same source: one REMOVE_TRAIT clears it
        room.World.Step(4);
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ASpinSpeedThatIsNotAWholeNumberOfTicksSleepsRoundedDown()
    {
        // sleep(1.4) is max(1, floor(1.4 / 0.5)) = 2 ticks (waiting-model.md 8)
        var room = new MobRoom();
        TgMob m = null;
        room.In(() =>
        {
            m = NewAt<TgMob>();
            m.dir = NORTH;
            m.spin(2.8f, 1.4f); // two turns
        });
        room.World.Step(1);
        using (room.World.Enter())
            Assert.Equal((float)NORTH, m.dir);
        room.World.Step(1);
        using (room.World.Enter())
            Assert.Equal((float)EAST, m.dir);
        room.World.Step(2);
        using (room.World.Enter())
        {
            Assert.Equal((float)SOUTH, m.dir);
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
        }
    }

    // ---- focus.dm ----

    [Fact]
    public void SetFocusStoresTheFocusAndResetsThePerspectiveOnlyForAChange()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_perspective>();
            var other = NewAt<TgMob>(4, 4);
            Assert.Same(m, m.focus); // Initialize's set_focus(src)
            Assert.Equal(new object[] { m }, m.perspectives.ToArray());

            Waiting.NoSleep(m.set_focus(m)); // `if(focus == new_focus) return`
            Assert.Single(m.perspectives);

            Waiting.NoSleep(m.set_focus(other));
            Assert.Same(other, m.focus);
            Assert.Equal(new object[] { m, other }, m.perspectives.ToArray());

            Waiting.NoSleep(m.set_focus(null));
            Assert.Null(m.focus);
            Assert.Equal(new object[] { m, other, null }, m.perspectives.ToArray()); // reset_perspective(null): the default view
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ResetPerspectiveOfAMobWithoutAClientReturnsNullAndDoesNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Null(Waiting.NoSleep(m.reset_perspective(null)));
            Assert.Null(Waiting.NoSleep(m.reset_perspective(m)));
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- magic ----

    [Fact]
    public void CanCastMagicIsAlwaysTrueForTheNoneFlagAndAskedOfTheRestrictSignalOtherwise()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.True(Dm.eq(m.can_cast_magic(NONE), TRUE));
            Assert.True(Dm.eq(m.can_cast_magic(), TRUE)); // no restriction: the signal returns NONE
            Chain.On(m, COMSIG_MOB_RESTRICT_MAGIC, "restrict", result: COMPONENT_MAGIC_BLOCKED);
            Assert.True(Dm.eq(m.can_cast_magic(NONE), TRUE)); // never asks for NONE
            Assert.True(Dm.eq(m.can_cast_magic(MAGIC_RESISTANCE_HOLY), FALSE));
            Assert.Equal(new[] { "restrict " + EngineTrace.D(m) + " " + Dm.str(MAGIC_RESISTANCE_HOLY) + " null null" }, EngineTrace.Of(room.World).Lines);
        });
    }

    [Fact]
    public void CanCastMagicDefaultsToTheMagicResistanceFlag()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Chain.On(m, COMSIG_MOB_RESTRICT_MAGIC, "restrict", result: NONE);
            Assert.True(Dm.eq(m.can_cast_magic(), TRUE));
            Assert.Equal("restrict " + EngineTrace.D(m) + " " + Dm.str(MAGIC_RESISTANCE) + " null null", EngineTrace.Of(room.World).Lines.Single());
        });
    }

    [Fact]
    public void CanBlockMagicIsFalseForTheNoneFlagWithoutAskingAnyone()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_magic>();
            ADD_TRAIT(m, TRAIT_ANTIMAGIC, "test");
            Chain.On(m, COMSIG_MOB_RECEIVE_MAGIC, "receive", result: COMPONENT_MAGIC_BLOCKED);
            Assert.True(Dm.eq(m.can_block_magic(NONE), FALSE));
            Assert.Empty(EngineTrace.Of(room.World).Lines);
            Assert.Empty(m.blocked);
        });
    }

    [Fact]
    public void CanBlockMagicIsFalseWithNothingToBlockIt()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_magic>();
            Assert.True(Dm.eq(m.can_block_magic(), FALSE));
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE_HOLY), FALSE));
            Assert.Empty(m.blocked);
        });
    }

    [Fact]
    public void ATraitAntimagicBlocksAndRunsTheBlockEffectsOnceWithTheChargeCostRule()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_magic>();
            ADD_TRAIT(m, TRAIT_ANTIMAGIC, "test");
            Assert.True(Dm.eq(m.can_block_magic(), TRUE));
            Assert.Equal(new[] { Dm.str(MAGIC_RESISTANCE) }, m.blocked.Select(b => b.flags).ToArray()); // on_block_magic_effects (:1011-1013)
            Assert.Equal(0f, m.blocked[0].sources); // an empty antimagic_sources list

            m.blocked.Clear();
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE, 0), TRUE)); // charge_cost 0 (not null): still blocked, no effects
            Assert.Empty(m.blocked);

            ADD_TRAIT(m, TRAIT_RECENTLY_BLOCKED_MAGIC, "test");
            Assert.True(Dm.eq(m.can_block_magic(), TRUE)); // blocked, but we "recently blocked magic": no effects again
            Assert.Empty(m.blocked);
        });
    }

    [Fact]
    public void AHolyTraitBlocksOnlyHolyMagic()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_magic>();
            ADD_TRAIT(m, TRAIT_HOLY, "test");
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE), FALSE));
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE_HOLY), TRUE));
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE_HOLY | MAGIC_RESISTANCE_MIND), TRUE));
        });
    }

    [Fact]
    public void AMagicBlockedSignalBlocksAndHandsTheListenerTheSourcesList()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_magic>();
            var source = NewAt<obj>();
            Chain.On(m, COMSIG_MOB_RECEIVE_MAGIC, "receive", result: COMPONENT_MAGIC_BLOCKED, act: (_, __) => { });
            Assert.True(Dm.eq(m.can_block_magic(MAGIC_RESISTANCE_MIND, 2), TRUE));
            Assert.Equal(new[] { "receive " + EngineTrace.D(m) + " " + Dm.str(MAGIC_RESISTANCE_MIND) + " 2 " + EngineTrace.D(m.blocked[0].sources_list) }, EngineTrace.Of(room.World).Lines);
            Assert.Equal(new[] { Dm.str(MAGIC_RESISTANCE_MIND) }, m.blocked.Select(b => b.flags).ToArray());
        });
    }

    // ---- the small procs ----

    [Fact]
    public void GetItemsBySlotsAsksForEveryBitOfTheSlotMaskFromTheLowestBitUp()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_slots>();
            var back = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            var belt = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            m.items[ITEM_SLOT_BACK] = back;
            m.items[ITEM_SLOT_BELT] = belt;
            DmList got = (DmList)m.get_items_by_slots(ITEM_SLOT_BELT | ITEM_SLOT_BACK);
            // bitfield_to_list walks the bits from the lowest up (the BELT bit is below the BACK bit, __DEFINES/inventory.dm)
            Assert.True(ITEM_SLOT_BELT < ITEM_SLOT_BACK);
            Assert.Equal(new object[] { belt, back }, Dm.each(got).Cast<object>().ToArray());
            Assert.Equal(new[] { Dm.str(ITEM_SLOT_BELT), Dm.str(ITEM_SLOT_BACK) }, m.asked.ToArray());
            Assert.Equal(0f, ((DmList)m.get_items_by_slots(NONE)).len);
        });
    }

    [Fact]
    public void GetSlotByItemFindsAnItemInTheHandsAndOnlyThere()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var held = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            var other = Dm.@new<obj_item_test_mob_thing>(MobRoom.T(3, 3));
            m.held_items = Dm.list(null, held);
            Assert.True(Dm.eq(m.get_slot_by_item(held), ITEM_SLOT_HANDS));
            Assert.Null(m.get_slot_by_item(other));
        });
    }

    [Fact]
    public void TgQuirk_GetSlotByItemOfNullFindsAnEmptyHand()
    {
        // "Does not check if the passed item is null, which may result in unexpected outcomes" (mob.dm:455): `null in list(null)`
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Null(m.get_slot_by_item(null)); // no hands at all
            m.held_items = Dm.list(null, null);
            Assert.True(Dm.eq(m.get_slot_by_item(null), ITEM_SLOT_HANDS));
        });
    }

    [Fact]
    public void IsLiterateNeedsTheLiterateTraitAndNoIlliterateOne()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.True(Dm.eq(m.is_literate(), FALSE));
            ADD_TRAIT(m, TRAIT_LITERATE, "a");
            Assert.True(Dm.eq(m.is_literate(), TRUE));
            ADD_TRAIT(m, TRAIT_ILLITERATE, "b");
            Assert.True(Dm.eq(m.is_literate(), FALSE));
            REMOVE_TRAIT(m, TRAIT_ILLITERATE, "b");
            Assert.True(Dm.eq(m.is_literate(), TRUE));
        });
    }

    [Theory]
    [InlineData(0f, null, null, null, false)]
    [InlineData(LIGHTING_NIGHTVISION_THRESHOLD, null, null, null, true)] // `>=`
    [InlineData(LIGHTING_NIGHTVISION_THRESHOLD - 1, null, null, null, false)]
    [InlineData(0f, 30f, 30f, 30f, true)] // the cutoff plus the mean of the colour cutoffs
    [InlineData(0f, 0f, 0f, 0f, false)]
    [InlineData(10f, 1f, 2f, 3f, true)] // 10 + (1 + 2 + 3) / 3 = 12
    public void HasNightvisionAddsTheMeanColourCutoffOnlyForThreeOfThem(float cutoff, float? red, float? green, float? blue, bool expected)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.lighting_cutoff = cutoff;
            if (red != null)
                m.lighting_color_cutoffs = Dm.list(red, green, blue);
            Assert.True(Dm.eq(m.has_nightvision(), expected ? TRUE : FALSE));
        });
    }

    [Fact]
    public void HasNightvisionIgnoresAColourCutoffListOfTheWrongLength()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.lighting_cutoff = 0;
            m.lighting_color_cutoffs = Dm.list(100, 100);
            Assert.True(Dm.eq(m.has_nightvision(), FALSE)); // `length(...) == 3` (mob.dm:1264-1265)
        });
    }

    [Fact]
    public void CanReadChecksLiteracyFirstAndAnIlliterateMobCannotReadSilently()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var book = NewAt<obj>();
            Assert.True(Dm.eq(m.can_read(book, READING_CHECK_LITERACY, silent: TRUE), FALSE));
            Assert.True(Dm.eq(m.can_read(book, READING_CHECK_LITERACY), FALSE)); // the message goes to_chat: nobody there
            ADD_TRAIT(m, TRAIT_LITERATE, "a");
            Assert.True(Dm.eq(m.can_read(book, READING_CHECK_LITERACY), TRUE));
            Assert.True(Dm.eq(m.can_read(book, NONE), TRUE)); // no check asked for
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void CanReadWithTheLightCheckCountsAnUnmarkedTurfAsLit()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            ADD_TRAIT(m, TRAIT_LITERATE, "a");
            // has_light_nearby (:1313): the toy room's turfs carry no lighting mark, so check_lumcount_above counts them as lit (lighting_turf.dm:46)
            Assert.True(Dm.eq(m.has_light_nearby(), TRUE));
            Assert.True(Dm.eq(m.can_read(null, READING_CHECK_LIGHT), TRUE));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void GetAccessIsANewEmptyListEveryTime()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = (DmList)m.get_access();
            var b = (DmList)m.get_access();
            Assert.Equal(0f, a.len);
            Assert.NotSame(a, b);
        });
    }

    [Fact]
    public void TheNoOpProcsDoNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Null(m.update_health_hud(100));
            m.update_stamina_hud();
            m.update_stat();
            m.replace_records_name("a", "b");
            Waiting.NoSleep(m.limb_attack_self());
            Assert.Null(Waiting.NoSleep(m.can_perform_action(NewAt<obj>(), NONE)));
            Assert.True(Dm.eq(m.assess_threat(JUDGE_IDCHECK), 0));
            Assert.True(Dm.eq(m.canUseStorage(), FALSE));
            Assert.True(Dm.eq(m.can_resist(), FALSE));
            Assert.Same(m, m.get_listening_mob());
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void UpdateSightSendsItsSignalAndNeedsNoHudForTheLightingPlane()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Chain.On(m, COMSIG_MOB_UPDATE_SIGHT, "sight");
            m.update_sight();
            Assert.Equal(new[] { "sight " + EngineTrace.D(m) + " null null null" }, EngineTrace.Of(room.World).Lines);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void BecomeUnclientedOfAMobThatNeverHadAClientDoesNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.become_uncliented());
            Assert.Null(m.canon_client);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void LoginAndLogoutOfAMobWithoutAClientAreTranslatedWithTheClientLayer()
    {
        // M1 P1 translated login.dm and logout.dm (the client layer); a mob without a client: Login returns FALSE at once
        // (login.dm:30-31), Logout runs through and returns TRUE, auto_deadmin_on_login returns TRUE (no holder).
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.True(Dm.eq(Waiting.NoSleep(m.Login()), FALSE));
            Assert.True(Dm.eq(Waiting.NoSleep(m.Logout()), TRUE));
            Assert.True(Dm.eq(m.auto_deadmin_on_login(), TRUE));
            // reachable by name, as the client's verbs and the player layer will call them
            Assert.True(Dm.eq(Waiting.NoSleep(Dm.Call(m, "Login")), FALSE));
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- ghostize, PossessByPlayer ----

    [Fact]
    public void PossessByPlayerTakesTheCanonicalKeyAndOnlyNullReturnsEarly()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.PossessByPlayer("Some Key_1!");
            Assert.Equal("somekey1", m.ckey);
            m.PossessByPlayer(null); // `if(isnull(ckey)) return`: the old ckey stays
            Assert.Equal("somekey1", m.ckey);
            m.PossessByPlayer(""); // an empty text is not null: it is stored (as the empty canonical key)
            Assert.Equal("", m.ckey);
            Assert.Throws<DmCrashException>(() => m.PossessByPlayer(new object()));
            m.ckey = null;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void GhostizeReturnsNullForNoKeyOrAFakeKey()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Assert.Null(m.ghostize());
            m.key = "";
            Assert.Null(m.ghostize(FALSE, TRUE));
            m.key = "@Aghost";
            Assert.Null(m.ghostize());
            m.key = null; // a real player's key passes the guard: the ghosts (D151) are tested in Ghosts/GhostTests
        });
    }

    [Fact]
    public void IsFakeKeyLooksOnlyAtTheFirstCharacterForAtSign()
    {
        Assert.True(IS_FAKE_KEY("@aghost"));
        Assert.True(IS_FAKE_KEY("@"));
        Assert.False(IS_FAKE_KEY("Player@"));
        Assert.False(IS_FAKE_KEY(""));
        Assert.False(IS_FAKE_KEY(null));
    }

    // ---- ambience ----

    [Fact]
    public void UpdateAmbienceAreaOfAMobWithoutAClientDropsTheTrackedAreaAndReturns()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            area first = (area)MobRoom.T(3, 3).loc;
            m.ambience_tracked_area = first;
            m.RegisterSignal(first, COMSIG_AREA_POWER_CHANGE, nameof(TgMob.update_ambience_area));
            Assert.True(Dm.truthy(first._listen_lookup?[COMSIG_AREA_POWER_CHANGE]));
            m.update_ambience_area(first);
            Assert.Null(m.ambience_tracked_area); // dropped, and not set to the new area: `if(!client) return` (ambience.dm:109)
            Assert.False(Dm.truthy(first._listen_lookup?[COMSIG_AREA_POWER_CHANGE])); // UnregisterSignal(old_tracked_area, ...)
            m.update_ambience_area(first); // with nothing tracked: nothing happens
            Assert.Null(m.ambience_tracked_area);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void UpdateAmbienceAreaForAMobWithAClientTracksTheNewAreasPowerChanges()
    {
        // D296: the mob tracks the new area (ambience.dm:111-113) and refreshes its buzz on the area's power changes
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.client = Dm.@new<client_test_m1_client>();
            area here = (area)MobRoom.T(3, 3).loc;
            m.update_ambience_area(here);
            Assert.Same(here, m.ambience_tracked_area);
            Assert.True(Dm.truthy(here._listen_lookup?[COMSIG_AREA_POWER_CHANGE]));
            m.update_ambience_area(null);
            Assert.Null(m.ambience_tracked_area);
            Assert.False(Dm.truthy(here._listen_lookup?[COMSIG_AREA_POWER_CHANGE]));
            m.client = null;
        });
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>Counts COMSIG_LIVING_UPDATE_NUTRITION.</summary>
[DmPath("/datum/test_mob_living_nutrition_listener")]
public class datum_test_mob_living_nutrition_listener : datum
{
    [NotHashed] public float updates;

    [SignalHandler]
    public virtual void on_update(datum source = null) => updates++;
}

/// <summary>A mob that records the perspective resets it is asked for (set_focus ends in reset_perspective, focus.dm:5).</summary>
[DmPath("/mob/test_mob_perspective")]
public class mob_test_mob_perspective : TgMob
{
    [NotHashed] public readonly List<object> perspectives = new();

    public override async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        perspectives.Add(new_eye);
        return await base.reset_perspective(new_eye, __extra);
    }
}

/// <summary>A mob that records its magic block effects (/mob/proc/on_block_magic_effects, mob.dm:1017).</summary>
[DmPath("/mob/test_mob_magic")]
public class mob_test_mob_magic : TgMob
{
    public sealed record Blocked(string flags, float sources, DmList sources_list);

    [NotHashed] public readonly List<Blocked> blocked = new();

    public override void on_block_magic_effects(object magic_flags = null, DmList antimagic_sources = null)
        => blocked.Add(new Blocked(Dm.str(magic_flags), antimagic_sources?.len ?? -1, antimagic_sources));
}

/// <summary>A mob whose slots hold what the test put there (inventory is M2: get_item_by_slot is the base's null).</summary>
[DmPath("/mob/test_mob_slots")]
public class mob_test_mob_slots : TgMob
{
    [NotHashed] public readonly Dictionary<float, object> items = new();
    [NotHashed] public readonly List<string> asked = new();

    public override object get_item_by_slot(object slot_id = null)
    {
        asked.Add(Dm.str(slot_id));
        return items.TryGetValue(Dm.num(slot_id), out object item) ? item : null;
    }
}
