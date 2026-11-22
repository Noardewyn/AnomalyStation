using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// Smoke tests of package C's translation (wave 4): a mob and an obj are created and destroyed without a runtime of the
/// package's own, the mob life cycle does what mob.dm says (the tag, the HUD images, the focus, nutrition, the
/// incapacitation and stat signals), and the small procs of mob.dm, objs.dm and the logging helpers behave as tg's.
/// Written before packages A and D were merged, when their skeletons were reached (and reported once per site) without
/// throwing (<see cref="MobTestWorld"/>; those packages are merged now).
/// </summary>
public class MobSmokeTests
{
    private static void AssertOnlyOtherPackagesAreMissing(MobTestWorld t)
    {
        Assert.Empty(t.OwnRuntimes.Select(e => e.ToString()));
        foreach (Exception e in t.Runtimes)
            Assert.True(e.Message.Contains("(package A)") || e.Message.Contains("(package D)"), e.Message);
    }

    [Fact]
    public void AMobWithoutAClientIsCreatedAndDestroyedWithoutARuntimeOfPackageC()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_MOB_CREATED, nameof(datum_test_mob_listener.on_mob_created));

            var m = Dm.@new<TgMob>(locate(1, 1, 1));

            // New: GenerateTag before anything else (mob.dm:59-61, :108-110)
            Assert.Equal("mob_1", m.tag); // mob_0 is GLOB.dview_mob (mobs.dm:661), made with the globals as in tg
            Assert.Equal(2f, TgMob.next_mob_id);
            // Initialize (mob.dm:81-101)
            Assert.Equal(new[] { "GLOB MOB_CREATED mob_1" }, listener.heard);
            Assert.True(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1)));
            Assert.Same(m, m.focus);
            Assert.Equal((float)NONE, m.incapacitated);
            Assert.InRange(m.nutrition, NUTRITION_LEVEL_START_MIN, NUTRITION_LEVEL_START_MAX);
            // prepare_huds over hud_possible = list(ANTAG_HUD) (mob_defines.dm:14): one image of the mob, active
            var antag = m.hud_list[ANTAG_HUD] as image;
            Assert.NotNull(antag);
            Assert.Same(m, antag.loc);
            Assert.True(Dm.eq(antag.icon, Dm.resource("icons/mob/huds/hud.dmi")));
            Assert.Equal("", antag.icon_state);
            Assert.Equal((float)Dm.bitor(Dm.bitor(RESET_COLOR, PIXEL_SCALE), KEEP_APART), antag.appearance_flags);
            Assert.Same(antag, m.active_hud_list[ANTAG_HUD]);
            // setup_hud_traits: every HUD trait signal is registered (mob.dm:1617-1623)
            foreach (object hud_trait in Dm.each(GLOB.trait_to_hud))
                Assert.True(Dm.truthy(m._listen_lookup[SIGNAL_ADDTRAIT(hud_trait)]));

            var second = Dm.@new<TgMob>(locate(1, 1, 1));
            Assert.Equal("mob_2", second.tag);

            qdel(m);
            qdel(second);
            Assert.True(QDELETED(m));
            Assert.Null(m.focus);
            Assert.Equal(0f, length(GLOB.mob_list));
            Assert.Equal(0f, length(GLOB.alive_mob_list));
            Assert.Equal(0f, length(GLOB.dead_mob_list));
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void AnObjRegistersItsIdTagAndLeavesProcessingWhenDestroyed()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var o = Dm.@new<obj_test_mob_tagged>(locate(2, 2, 1));
            Assert.Same(o, GLOB.objects_by_id_tag["test_mob_tag"]);
            START_PROCESSING(SSobj, o);
            Assert.True(Dm.@in(o, SSobj.processing));

            qdel(o);
            Assert.True(QDELETED(o));
            Assert.False(Dm.@in(o, SSobj.processing));
            Assert.Equal(0f, length(GLOB.objects_by_id_tag));
            // SStgui.close_uis (objs.dm:77) is translated since M5 slice 7: no deferred leaf there any more
            Assert.DoesNotContain("code/game/objects/objs.dm:77", DmWorldState.Of(t.World).DeferredSites);
        }
        Assert.Empty(t.Runtimes.Select(e => e.ToString()));
    }

    [Fact]
    public void CheckOnTableRaisesAnAnchoredObjOnlyOverATable()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var bare = Dm.@new<obj_test_mob_tabletop>(locate(1, 1, 1));
            Assert.Equal(0f, bare.pixel_z); // initial(pixel_z)

            Dm.@new<obj_structure_table>(locate(2, 1, 1));
            var raised = Dm.@new<obj_test_mob_tabletop>(locate(2, 1, 1));
            Assert.Equal(5f, raised.pixel_z);

            raised.anchored = FALSE;
            raised.check_on_table();
            Assert.Equal(0f, raised.pixel_z);
        }
        // GAP(M3 P1 -> lead): the table's tg var block reaches its listed leaf (Tools/Types/VarBlockLeaves.cs).
        Assert.Empty(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(typeof(obj_structure_table), t.Runtimes.Select(e => e.ToString()).ToList()));
    }

    [Fact]
    public void KeyNameDescribesMobsTextAndOtherDatumsAsTgDoes()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            var o = Dm.@new<obj>(locate(1, 1, 1));
            Assert.Equal("*null*", key_name(null));
            Assert.Equal("*no key*/(mob)", key_name(m));
            Assert.Equal("*no key*", key_name(m, include_name: FALSE));
            Assert.Equal("[obj]", key_name(o));
            Assert.Equal("[*invalid*]", key_name(5));
            Assert.Equal("SomeKey[DC]", key_name("SomeKey"));
            Assert.Equal("<a href='byond://?priv_msg=somekey'>SomeKey[DC]</a>", key_name_admin("SomeKey"));
            Assert.Equal("*no key*/(mob) (mob_1)", key_name_and_tag(m));
            Assert.Equal("*null* (!tagless!)", key_name_and_tag(null));
            m.key = "Player";
            m.ckey = "player";
            m.real_name = "John Doe";
            Assert.Equal("Player[DC]/(John Doe)", key_name(m));
            Assert.Equal("(INVALID LOCATION)", loc_name(null));
            m.key = null; // ghostize would turn a keyed mob into an observer (M2)
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void PossessByPlayerStoresTheCanonicalKeyAndRejectsAnythingButText()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            m.PossessByPlayer(null);
            Assert.Null(m.ckey);
            m.PossessByPlayer("Some Player");
            Assert.Equal("someplayer", m.ckey);
            var crash = Assert.Throws<DmCrashException>(() => m.PossessByPlayer(5));
            Assert.Contains("non-text ckey", crash.Message);
            m.ckey = null;
        }
    }

    [Fact]
    public void GhostizeReturnsAtOnceForAMobWithoutAKeyOrWithAnAdminsFakeKey()
    {
        var t = new MobTestWorld(notTranslatedThrows: true);
        using (t.World.Enter())
        {
            var m = Dm.@new<mob_test_mob_incap>();
            Assert.Null(m.ghostize());
            m.key = "@admin";
            Assert.True(IS_FAKE_KEY(m.key));
            Assert.Null(m.ghostize());
            m.key = "player";
            Assert.False(IS_FAKE_KEY(m.key)); // past the guard a player's mob becomes an observer (the ghosts, D151: Ghosts/GhostTests)
            m.key = null;
            Assert.False(IS_FAKE_KEY(null));
        }
    }

    [Fact]
    public void UpdateIncapacitatedSignalsOnlyAChange()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<mob_test_mob_incap>(locate(1, 1, 1));
            var listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(m, COMSIG_MOB_INCAPACITATE_CHANGED, nameof(datum_test_mob_listener.on_incapacitate_changed));
            Assert.True(Dm.eq(m.update_incapacitated(), FALSE));
            m.incap_flags = INCAPABLE_RESTRAINTS;
            Assert.True(Dm.eq(m.update_incapacitated(), TRUE));
            Assert.Equal((float)INCAPABLE_RESTRAINTS, m.incapacitated);
            Assert.Equal(new[] { "INCAPACITATE_CHANGED 0 -> " + Dm.str(INCAPABLE_RESTRAINTS) }, listener.heard);
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void SetStatReturnsTheOldStatAndSignalsTheChange()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            var listener = Dm.@new<datum_test_mob_listener>();
            listener.RegisterSignal(m, COMSIG_MOB_STATCHANGE, nameof(datum_test_mob_listener.on_statchange));
            Assert.Null(Waiting.NoSleep(m.set_stat(STABLE)));
            Assert.True(Dm.eq(Waiting.NoSleep(m.set_stat(DEAD)), STABLE));
            Assert.Equal((float)DEAD, m.stat);
            Assert.Equal(new[] { "STATCHANGE " + Dm.str(DEAD) + " from " + Dm.str(STABLE) }, listener.heard);
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void NutritionNeverGoesBelowZeroAndNohungerBlocksUnforcedChanges()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            Waiting.NoSleep(m.set_nutrition(100));
            Waiting.NoSleep(m.adjust_nutrition(-30));
            Assert.Equal(70f, m.nutrition);
            Waiting.NoSleep(m.adjust_nutrition(-500));
            Assert.Equal(0f, m.nutrition);
            ADD_TRAIT(m, TRAIT_NOHUNGER, "test");
            Waiting.NoSleep(m.set_nutrition(200));
            Assert.Equal(0f, m.nutrition);
            Waiting.NoSleep(m.set_nutrition(200, forced: TRUE));
            Assert.Equal(200f, m.nutrition);
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void SpinReturnsAtOnceAndTurnsTheMobClockwiseEverySpeedDeciseconds()
    {
        var t = new MobTestWorld();
        var dirs = new List<float>();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            m.dir = SOUTH;
            m.spin(4, 1); // set waitfor = 0 (mob.dm:749): the caller never waits
            Assert.True(HAS_TRAIT(m, TRAIT_SPINNING));
            Assert.Equal(SOUTH, m.dir);
            for (int i = 0; i < 8; i++)
            {
                t.World.Step();
                dirs.Add(m.dir);
            }
            Assert.False(HAS_TRAIT(m, TRAIT_SPINNING));
        }
        // sleep(1) is two ticks; SOUTH -> WEST -> NORTH -> EAST -> SOUTH (mob.dm:757-765)
        Assert.Equal(new[] { SOUTH, WEST, WEST, NORTH, NORTH, EAST, EAST, SOUTH }, dirs);
        AssertOnlyOtherPackagesAreMissing(t);
    }

    [Fact]
    public void TheSmallMobProcsReturnTgsDefaults()
    {
        var t = new MobTestWorld();
        using (t.World.Enter())
        {
            var m = Dm.@new<TgMob>(locate(1, 1, 1));
            Assert.Same(m, m.get_listening_mob());
            Assert.Null(Dm.@new<obj>(locate(1, 1, 1)).get_listening_mob());
            Assert.True(Dm.eq(m.can_resist(), FALSE));
            Assert.True(Dm.eq(m.canUseStorage(), FALSE));
            Assert.True(Dm.eq(m.assess_threat(null), 0));
            Assert.Null(m.get_item_by_slot(ITEM_SLOT_HANDS));
            Assert.Null(m.get_slot_by_item(null));
            Assert.Equal(0f, ((DmList)m.get_access()).len);
            Assert.True(Dm.eq(m.mob_negates_gravity(), FALSE));
            Assert.True(Dm.eq(m.can_cast_magic(), TRUE));
            Assert.True(Dm.eq(m.can_block_magic(), FALSE));
            ADD_TRAIT(m, TRAIT_ANTIMAGIC, "test");
            Assert.True(Dm.eq(m.can_block_magic(), TRUE));
            Assert.True(Dm.eq(m.is_literate(), FALSE));
            ADD_TRAIT(m, TRAIT_LITERATE, "test");
            Assert.True(Dm.eq(m.is_literate(), TRUE));
            Assert.True(Dm.eq(m.has_nightvision(), Dm.num(LIGHTING_CUTOFF_VISIBLE >= LIGHTING_NIGHTVISION_THRESHOLD)));
            Assert.True(Dm.eq(Waiting.NoSleep(m.blind_examine_check(null)), TRUE));
        }
        AssertOnlyOtherPackagesAreMissing(t);
    }
}
