using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The life of a client-less mob, from code/modules/mob/mob.dm: New (:59), Initialize (:81), GenerateTag (:108), the HUD
/// image bookkeeping (:142-199), the HUD trait signals (:1617-1664) and Destroy (:26). Risk R11: /mob/Initialize reaches many
/// leaves of other packages, and none may throw for a mob without a client.
/// </summary>
public class MobLifecycleTests
{
    // M7 power helpers slice: GLOB.dview_mob (mobs.dm:661, the dview mob the bluespace light replacer's scan uses) is made with the
    // globals, as in tg, and takes the first mob tag: the first mob a test makes is mob_1 and the counter starts at 1.
    private const float DviewMobs = 1;

    // ---- New and the tag ----

    [Fact]
    public void NewGeneratesTheTagBeforeInitializeRuns()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = Dm.@new<mob_test_mob_tag_recorder>(MobRoom.T(2, 2));
            Assert.Equal("mob_1", m.tag_at_initialize); // "This needs to happen IMMEDIATELY" (mob.dm:60)
            Assert.Equal("mob_1", m.tag);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheTagIsMobAndTheCountOfMobsCreatedNeverReusedAfterADelete()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            Assert.Equal(DviewMobs, TgMob.next_mob_id);
            var a = NewMob();
            var b = NewMob();
            Assert.Equal("mob_2", b.tag);
            qdel(b);
            Assert.Null(b.tag); // /datum/Destroy clears it
            var c = NewMob();
            Assert.Equal("mob_1", a.tag);
            Assert.Equal("mob_3", c.tag); // the counter went up for the deleted one too (`next_mob_id++`, :110)
            Assert.Equal(DviewMobs + 3f, TgMob.next_mob_id);
        });
    }

    [Fact]
    public void TheMobCounterIsPerWorld()
    {
        var one = new MobRoom(seed: 3);
        var two = new MobRoom(seed: 3);
        one.In(() =>
        {
            NewMob();
            NewMob();
            NewMob();
            Assert.Equal(DviewMobs + 3f, TgMob.next_mob_id);
        });
        two.In(() =>
        {
            Assert.Equal(DviewMobs, TgMob.next_mob_id); // a TgState field of the world (rules 10.6), not a C# static
            Assert.Equal("mob_1", NewMob().tag);
        });
    }

    [Fact]
    public void OnlyMobsGetAGeneratedTag()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            Assert.Null(Dm.@new<obj>(MobRoom.T(1, 1)).tag);
            Assert.Equal(DviewMobs, TgMob.next_mob_id);
        });
    }

    private static TgMob NewMob(int x = 3, int y = 3) => Dm.@new<TgMob>(MobRoom.T(x, y));

    // ---- Initialize ----

    [Fact]
    public void InitializeCallsItsLeavesInTgsOrderAndParentInitializeComesAfterTheNutrition()
    {
        // mob.dm:81-101: the lists, update_incapacitated, set_focus, prepare_huds, [alternate appearances], set_nutrition,
        // `. = ..()` (which marks the mob INITIALIZED_1), then setup_hud_traits, update_config_movespeed,
        // initialize_actionspeed, [update_movespeed, become_hearing_sensitive], log_mob_tag.
        var room = new MobRoom();
        room.In(() =>
        {
            var m = Dm.@new<mob_test_mob_order>(MobRoom.T(3, 3));
            Assert.Equal(new[]
            {
                "update_incapacitated",
                "set_focus",
                "prepare_huds",
                "set_nutrition",
                "setup_hud_traits+init",
                "update_config_movespeed+init",
                "initialize_actionspeed+init",
                "log_mob_tag+init",
            }, m.order);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMobWithoutAClientInitializesWithoutAnyRuntimeAndEveryLeafIsReachedOrDeferred()
    {
        // NotTranslatedThrows is on in these worlds: a NotTranslated leaf would be a reported runtime.
        var room = new MobRoom();
        room.In(() =>
        {
            for (int i = 0; i < 3; i++)
                NewMob(1 + i, 1);
            Dm.@new<mob_test_mob_dead>(MobRoom.T(1, 4));
            Dm.@new<mob_living>(MobRoom.T(1, 5));
        });
        Assert.Empty(room.RuntimeText);
        using (room.World.Enter())
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len); // every Initialize returned a hint and none slept or was deleted early
    }

    [Fact]
    public void TheGlobalMobCreatedSignalIsSentFirstBeforeTheMobIsOnAnyList()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var spy = Dm.@new<datum_test_mob_created_spy>();
            spy.RegisterSignal(SSdcs, COMSIG_GLOB_MOB_CREATED, nameof(datum_test_mob_created_spy.on_created));
            var m = NewMob();
            Assert.Single(spy.seen);
            Assert.Same(m, spy.seen[0].mob);
            Assert.False(spy.seen[0].in_mob_list); // SEND_GLOBAL_SIGNAL comes before add_to_mob_list (:82-83)
            Assert.False(spy.seen[0].in_alive_list);
            Assert.False(spy.seen[0].initialized);
            Assert.True(Dm.@in(m, GLOB.mob_list));
        });
    }

    [Fact]
    public void AMobGoesOnTheMobListAndTheAliveListUnlessItsStatIsDead()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var alive = NewMob(1, 1);
            var dead = Dm.@new<mob_test_mob_dead>(MobRoom.T(2, 1));
            Assert.True(Dm.@in(alive, GLOB.mob_list));
            Assert.True(Dm.@in(dead, GLOB.mob_list));
            Assert.True(Dm.@in(alive, GLOB.alive_mob_list));
            Assert.False(Dm.@in(alive, GLOB.dead_mob_list));
            Assert.True(Dm.@in(dead, GLOB.dead_mob_list));
            Assert.False(Dm.@in(dead, GLOB.alive_mob_list));
            Assert.Equal(2f, GLOB.mob_list.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheListOperationsOfAMobAreIdempotent()
    {
        // `GLOB.mob_list |= src` (mob_lists.dm:3): a second add leaves one entry
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            m.add_to_mob_list();
            m.add_to_alive_mob_list();
            Assert.Equal(1f, GLOB.mob_list.len);
            Assert.Equal(1f, GLOB.alive_mob_list.len);
            m.remove_from_dead_mob_list(); // not on it: nothing happens
            Assert.Equal(1f, GLOB.mob_list.len);
        });
    }

    [Fact]
    public void InitializeSetsTheFocusToTheMobItself()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            Assert.Same(m, m.focus);
            Assert.True(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1)));
        });
    }

    [Fact]
    public void InitializeSetsTheIncapacitationFromBuildIncapacitatedWithoutASignalBecauseNothingListensYet()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = Dm.@new<mob_test_mob_incap>(MobRoom.T(3, 3));
            Assert.Equal((float)NONE, m.incapacitated);
        });
        room.In(() =>
        {
            var m = Dm.@new<mob_test_mob_incap_from_start>(MobRoom.T(3, 3));
            Assert.Equal((float)INCAPABLE_RESTRAINTS, m.incapacitated); // update_incapacitated ran in Initialize (:85)
        });
    }

    [Fact]
    public void TheStartingNutritionIsAWholeNumberInTgsRangeAndFollowsTheSeed()
    {
        float First(ulong seed)
        {
            var room = new MobRoom(seed);
            return room.In(() => NewMob().nutrition);
        }

        float a = First(7);
        Assert.InRange(a, NUTRITION_LEVEL_START_MIN, NUTRITION_LEVEL_START_MAX);
        Assert.Equal(Math.Floor(a), a);
        Assert.Equal(a, First(7)); // rand(NUTRITION_LEVEL_START_MIN, NUTRITION_LEVEL_START_MAX) is the world's seeded RNG
        var distinct = new HashSet<float>(Enumerable.Range(1, 12).Select(seed => First((ulong)seed)));
        Assert.True(distinct.Count > 1, "different seeds give different nutrition");
    }

    [Fact]
    public void InitializeLogsTheTagWithTheKeyNameAndTheTypePath()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            logger.waiting_log_calls = null; // the logger of this test world has not initialized its categories: calls wait
            var m = Dm.@new<mob_test_mob_tag_recorder>(MobRoom.T(3, 3));
            Assert.Equal(1f, logger.waiting_log_calls.len);
            var call = (DmList)logger.waiting_log_calls[1];
            Assert.Equal(LOG_CATEGORY_DEBUG_MOBTAG, call[1]);
            // "TAG: [tag] CREATED: [key_name(src)] \[[type]\]" (mob.dm:101)
            Assert.Equal("TAG: mob_1 CREATED: *no key*/(test mob tag recorder) [/mob/test_mob_tag_recorder]", call[2]);
            Assert.Null(call[3]);
        });
    }

    [Fact]
    public void LogMobTagGoesToTheMobTagCategoryWithItsData()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            logger.waiting_log_calls = null;
            m.log_mob_tag("hello", Dm.list(Dm.kv("a", 1)));
            var call = (DmList)logger.waiting_log_calls[1];
            Assert.Equal("debug-mobtag", call[1]);
            Assert.Equal("hello", call[2]);
            Assert.True(Dm.eq(((DmList)call[3])["a"], 1));
        });
    }

    // ---- HUD images ----

    [Fact]
    public void PrepareHudsMakesOneImageOfTheMobPerHudPossibleAndMarksItActive()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            Assert.Equal(1f, m.hud_list.len); // hud_possible = list(ANTAG_HUD) (mob_defines.dm:14)
            var image = Assert.IsType<image>(m.hud_list[ANTAG_HUD]);
            Assert.Same(m, image.loc);
            Assert.True(Dm.eq(image.icon, Dm.resource("icons/mob/huds/hud.dmi")));
            Assert.Equal("", image.icon_state);
            Assert.Equal((float)Dm.bitor(Dm.bitor(RESET_COLOR, PIXEL_SCALE), KEEP_APART), image.appearance_flags);
            Assert.Same(image, m.active_hud_list[ANTAG_HUD]);
        });
    }

    [Fact]
    public void PrepareHudsIsLenientAboutBeingCalledTwice()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            DmList list = m.hud_list;
            object image = list[ANTAG_HUD];
            m.prepare_huds();
            Assert.Same(list, m.hud_list);
            Assert.Same(image, m.hud_list[ANTAG_HUD]);
        });
    }

    [Fact]
    public void AHudWithTheListHintGetsAnEmptyListNotAnImage()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = Dm.@new<obj_test_mob_hud>(MobRoom.T(1, 1));
            Assert.Equal(2f, o.hud_list.len);
            Assert.IsType<image>(o.hud_list["image_hud"]);
            var list = Assert.IsType<DmList>(o.hud_list["list_hud"]); // HUD_LIST_LIST (:197-198)
            Assert.Equal(0f, list.len);
            // an empty list is still an object (truthy), so it is "active" like an image
            Assert.Same(list, o.active_hud_list["list_hud"]);
            Assert.Same(o.hud_list["image_hud"], o.active_hud_list["image_hud"]);
        });
    }

    [Fact]
    public void AnAtomWithoutHudPossibleGetsAnEmptyHudList()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = Dm.@new<obj>(MobRoom.T(1, 1));
            Assert.Null(o.hud_list); // objs do not call prepare_huds
            o.prepare_huds();
            Assert.NotNull(o.hud_list);
            Assert.Equal(0f, o.hud_list.len);
        });
    }

    [Fact]
    public void SetHudImageActiveReturnsFalseForAnUnknownCategoryANonTextOneOrAnActiveOne()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            Assert.True(Dm.eq(m.set_hud_image_active(5), FALSE)); // !istext (:143)
            Assert.True(Dm.eq(m.set_hud_image_active("no such hud"), FALSE)); // !hud_list?[hud_category]
            Assert.True(Dm.eq(m.set_hud_image_active(ANTAG_HUD), FALSE)); // already active
            Assert.True(Dm.eq(m.set_hud_image_inactive(5), FALSE));
        });
    }

    [Fact]
    public void SetHudImageInactiveThenActiveRoundTrips()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            object image = m.hud_list[ANTAG_HUD];
            Assert.True(Dm.eq(m.set_hud_image_inactive(ANTAG_HUD), TRUE));
            Assert.Null(m.active_hud_list); // LAZYREMOVE emptied it
            Assert.Same(image, m.hud_list[ANTAG_HUD]); // the cached image stays
            Assert.True(Dm.eq(m.set_hud_image_active(ANTAG_HUD), TRUE));
            Assert.Same(image, m.active_hud_list[ANTAG_HUD]);
            Assert.True(Dm.eq(m.set_hud_image_inactive(ANTAG_HUD, update_huds: FALSE), TRUE));
            Assert.Null(m.active_hud_list);
            Assert.True(Dm.eq(m.set_hud_image_active(ANTAG_HUD, update_huds: FALSE), TRUE));
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- HUD traits ----

    [Fact]
    public void SetupHudTraitsRegistersTheAddAndRemoveSignalsOfEveryHudTraitAndBlocker()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            Assert.True(GLOB.trait_to_hud.len > 0);
            Assert.True(GLOB.trait_blockers_to_hud.len > 0);
            foreach (object trait in Dm.each(GLOB.trait_to_hud))
            {
                Assert.True(Dm.truthy(m._listen_lookup[SIGNAL_ADDTRAIT(trait)]), "add " + trait);
                Assert.True(Dm.truthy(m._listen_lookup[SIGNAL_REMOVETRAIT(trait)]), "remove " + trait);
            }
            foreach (object blocker in Dm.each(GLOB.trait_blockers_to_hud))
            {
                Assert.True(Dm.truthy(m._listen_lookup[SIGNAL_ADDTRAIT(blocker)]), "add " + blocker);
                Assert.True(Dm.truthy(m._listen_lookup[SIGNAL_REMOVETRAIT(blocker)]), "remove " + blocker);
            }
        });
    }

    [Fact]
    public void AHudBlockerTraitOnAMobWithoutTheBlockedHudDoesNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            ADD_TRAIT(m, TRAIT_BLOCK_MEDHUD, "test"); // no TRAIT_MEDICAL_HUD to hide (mob.dm:1647-1651)
            REMOVE_TRAIT(m, TRAIT_BLOCK_MEDHUD, "test");
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AHudTraitShowsTheMobItsDataHudAndRemovingItHidesIt()
    {
        // GLOB.huds (datums/hud.dm:10) exists since M8 slice F: the trait's handler (mob.dm:1625-1632) shows the data HUD
        // the trait names to the mob, and its removal hides it (:1634-1642).
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            var datahud = (datum_atom_hud)GLOB.huds[GLOB.trait_to_hud[TRAIT_MEDICAL_HUD]];
            ADD_TRAIT(m, TRAIT_MEDICAL_HUD, "test");
            Assert.True(HAS_TRAIT(m, TRAIT_MEDICAL_HUD));
            Assert.True(Dm.truthy(datahud.hud_users_all_z_levels[m]));
            REMOVE_TRAIT(m, TRAIT_MEDICAL_HUD, "test");
            Assert.False(Dm.truthy(datahud.hud_users_all_z_levels[m]));
            Assert.DoesNotContain(DmWorldState.Of(World.Current).DeferredSites, s => s.Contains("code/modules/mob/mob.dm:1632", StringComparison.Ordinal));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AHudTraitBlockedByAnActiveBlockerReturnsBeforeTheDataHudIsRead()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            ADD_TRAIT(m, TRAIT_BLOCK_MEDHUD, "test");
            ADD_TRAIT(m, TRAIT_MEDICAL_HUD, "test"); // blocked: hud_trait_enabled returns at :1627-1629
            Assert.True(HAS_TRAIT(m, TRAIT_MEDICAL_HUD));
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- Destroy ----

    [Fact]
    public void DestroyTakesTheMobOffEveryListAndClearsTheFocus()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var alive = NewMob(1, 1);
            var dead = Dm.@new<mob_test_mob_dead>(MobRoom.T(2, 1));
            alive.add_to_mob_suicide_list();
            Assert.True(Dm.@in(alive, GLOB.suicided_mob_list));
            qdel(alive);
            qdel(dead);
            Assert.Equal(0f, length(GLOB.mob_list));
            Assert.Equal(0f, length(GLOB.alive_mob_list));
            Assert.Equal(0f, length(GLOB.dead_mob_list));
            Assert.Equal(0f, length(GLOB.suicided_mob_list));
            Assert.Null(alive.focus);
            Assert.True(QDELETED(alive));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DestroyOfAMobWithAProgressbarsListReportsItAndClearsIt()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            m.progressbars = Dm.list(1);
            qdel(m);
            Assert.Null(m.progressbars);
        });
        var runtime = Assert.Single(room.Runtimes);
        Assert.Contains("destroyed with elements in its progressbars list", runtime.Message);
    }

    [Fact]
    public void DestroyOfAMobWithoutAClientButWithAKeyIsReportedExceptForAFakeKey()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            m.ckey = "player";
            qdel(m);
            var fake = NewMob();
            fake.ckey = "@aghost"; // "FUCK YOU AGHOST CODE" (mob.dm:30)
            qdel(fake);
            var none = NewMob();
            qdel(none);
        });
        var runtime = Assert.Single(room.Runtimes);
        Assert.Contains("Mob without client but with associated ckey, player, has been deleted.", runtime.Message);
    }

    [Fact]
    public void DestroyOfAMobWithAClientIsReported()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            m.client = Dm.@new<client_test_m1_client>();
            qdel(m);
            Assert.True(QDELETED(m));
            m.client = null;
        });
        Assert.Contains(room.Runtimes, e => e.Message.Contains("Mob with client has been deleted."));
    }

    [Fact]
    public void DestroyedMobsLeaveNoSpatialGridMembershipOrProcessing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewMob();
            Assert.True(HAS_TRAIT(m, TRAIT_HEARING_SENSITIVE)); // become_hearing_sensitive (:100)
            Assert.NotNull(m.important_recursive_contents);
            qdel(m);
            Assert.True(QDELETED(m));
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- create and destroy ----

    /// <summary>
    /// The translated creatable types of /obj and /mob (create_and_destroy.dm): valid_typesof(/atom/movable), which drops the
    /// types that are their own abstract_type (/atom/movable, /obj, /mob), minus tg's uncreatables. Reads GLOB (the abstract
    /// types are cached per world), so it runs inside the room's world.
    /// </summary>
    private static List<Type> CreatableObjAndMobTypes(MobRoom room)
        => room.In(() => Anomaly.Core.Tests.Tg.UnitTests.datum_unit_test_create_and_destroy.TranslatedMovableTypes()
            .Where(t => typeof(obj).IsAssignableFrom(t) || typeof(TgMob).IsAssignableFrom(t))
            .Where(t => t != typeof(obj_item_bodypart) && !typeof(obj_docking_port).IsAssignableFrom(t))
            .Where(t => !typeof(obj_effect_mapping_helpers).IsAssignableFrom(t)) // unit_test.dm:302, translated since M3 P3
            .Where(t => !typeof(obj_item_mob_holder).IsAssignableFrom(t)) // unit_test.dm:361: expects a mob to holderize (gaps C2)
            .Where(t => t != typeof(obj_item_inhand_desk_bell)) // desk_bell.dm:140: only made around its bell (gaps C5)
            .Where(t => t != typeof(obj_effect_bug_moving)) // unit_test.dm:286: not meant to spawn without the machine wand (gaps DV2)
            .Where(t => !typeof(obj_effect_skyfall_landingzone).IsAssignableFrom(t)) // unit_test.dm:358: needs a linked mecha (D321)
            .Where(t => !typeof(obj_effect_pod_landingzone).IsAssignableFrom(t) && !typeof(obj_effect_pod_landingzone_effect).IsAssignableFrom(t)) // unit_test.dm:318-319: need their pod (syndicate G4c)
            .Where(t => !new[] { typeof(obj_effect_mob_spawn_corpse_human_assistant_beesease_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection), typeof(obj_effect_mob_spawn_corpse_human_bitrunner), typeof(obj_effect_mob_spawn_corpse_human_cyber_police) }.Contains(t)) // gaps E3: the assistant's outfit's ID trim reads SSid_access (outfit.dm:213), not in the harness world; the bitrunner's and the cyber police's outfits have one too
            .Where(t => !typeof(mob_eye_camera_remote_base_construction).IsAssignableFrom(t)) // unit_test.dm:327: no linked console (the aux base console)
            .Where(t => !typeof(mob_eye_camera_remote_shuttle_docker).IsAssignableFrom(t)) // unit_test.dm:329: no linked console (syndicate N1c)
            .Where(t => !typeof(mob_eye_imaginary_friend).IsAssignableFrom(t)) // unit_test.dm:323: no trauma to pass in (gaps K1)
            .Where(t => !typeof(obj_effect_baseturf_helper).IsAssignableFrom(t)) // unit_test.dm:321: the baseturf limit (live since gaps BR1)
            .Where(t => t != typeof(obj_effect_landmark_bitrunning_crate_replacer)) // landmarks.dm:43: returns under UNIT_TESTS; the port's production branch needs its domain's crates (gaps BR1)
            .Where(t => !typeof(obj_machinery_gravity_generator_main).IsAssignableFrom(t)) // M3 R4: its 3x3 frame does not fit the room's corner
            .Where(t => !typeof(mob_dead_observer).IsAssignableFrom(t)) // the ghosts (D151): made on a turf it needs a station level (observer.dm:127-131), which MobRoom lacks
            .Where(t => t != typeof(obj_effect_decal_cleanable_blood_trail)) // unit_test.dm:290: "Should not exist outside of holders" (M6 slice 1)
            .Where(t => !typeof(obj_machinery_doomsday_device).IsAssignableFrom(t)) // unit_test.dm:299: "This should be obvious" (silicons SI8)
            // the APC (M7 slice 3) sets its area's channels off when it is made outside the map load and when it goes
            // (apc_main.dm:225, :329, update_area_power_usage), so the test room's later lights start unpowered and START_PROCESSING
            // their emergency power on SSmachines, which the harness world lacks (tg's runs with every subsystem)
            .Where(t => !typeof(obj_machinery_power_apc).IsAssignableFrom(t))
            // unit_test.dm:312-316: these need a mob to see them (M8 slice E, the hallucinations, translated them)
            .Where(t => !typeof(obj_effect_client_image_holder).IsAssignableFrom(t) && !typeof(obj_projectile_hallucination).IsAssignableFrom(t) && !typeof(obj_item_hallucinated).IsAssignableFrom(t))
            .Where(t => !typeof(obj_machinery_launchpad_briefcase).IsAssignableFrom(t)) // unit_test.dm:300: "briefcase launchpads erroring" (syndicate G2)
            .Where(t => !typeof(obj_structure_ethereal_crystal).IsAssignableFrom(t)) // unit_test.dm:325: the ethereal crystal needs its heart (gaps OG1)
            .Where(t => !typeof(obj_item_modular_computer_processor).IsAssignableFrom(t)) // unit_test.dm:309: the console's processing unit crashes outside its console (live since E2)
            .Where(t => !typeof(obj_machinery_space_heater_improvised_chem_heater).IsAssignableFrom(t)) // its RefreshParts reads its subsystem's wait (spaceheater.dm:537), and MobRoom's Master lists no SSmachines (E9)
            .Where(t => !typeof(obj_machinery_power_manufacturing_crafter).IsAssignableFrom(t)) // its Initialize starts processing on SSmanufacturing (crafter.dm:25), which MobRoom's Master does not list (E16)
            // unit_test.dm:307, :339, :373 (silicons SI2): the silicons' PDAs delete themselves outside a silicon, the preset-model
            // cyborgs invoke async in their Initialize, and the robot models never spawn outside a robot
            .Where(t => !typeof(obj_item_modular_computer_pda_silicon).IsAssignableFrom(t) && !typeof(mob_living_silicon_robot_model).IsAssignableFrom(t))
            .Where(t => !typeof(obj_item_robot_model).IsAssignableFrom(t))
            // the sort-router starts processing on SSobj (sorter.dm:33), which tg's /obj/Destroy does not stop for a machine (objs.dm:72): it
            // stays in SSobj.processing until SSobj's next fire drops it, after this check (E16)
            .Where(t => !typeof(obj_machinery_power_manufacturing_sorter).IsAssignableFrom(t))
            // the repairbot likewise (repairbot.dm:96; its Destroy, :221, does not stop it): SSobj's next fire drops it (gaps C3)
            .Where(t => !typeof(mob_living_basic_bot_repairbot).IsAssignableFrom(t))
            // unit_test.dm:367: a tram "runtimes if not paired with a landmark" (its controller CRASHes without the nav beacons); the
            // elevators of the same typesof pass here and stay
            .Where(t => !typeof(obj_structure_transport_linear_tram).IsAssignableFrom(t))
            // the second syndi-kit picks its kit at random (uplink_kits.dm:186, pick_weight): its bees kit holds the beesease bottle, whose
            // disease is not ported (reagent_containers.dm:81, diseases after the core version), and the uplink hides the kit (DV343). The
            // live types of syndicate G4d moved the room's random stream onto that kit
            .Where(t => t != typeof(obj_item_storage_box_syndicate_bundle_b))
            .ToList());

    [Fact]
    public void EveryTranslatedObjAndMobTypeCreatesAndDestroysLeavingTheGlobalListsEmpty()
    {
        var room = new MobRoom();
        TgStrings.Mount(DmWorldState.Of(room.World).Files, TgClone.Root); // C4: the crab's and carp's speech (speechmod)
        var types = CreatableObjAndMobTypes(room);
        Assert.True(types.Count >= 5, "found " + types.Count + " obj and mob types");
        Assert.Contains(typeof(TgMob), types.Concat(new[] { typeof(TgMob) }));
        // GAP(M3 P1 -> lead): the hand-written skeletons that got tg's var block reach their listed leaf (VarBlockLeaves).
        var unexpected = new List<string>();
        // M4b audio slice 6: a hotspot's Initialize plays its puff (LINDA_fire.dm:144), and playsound finds its listeners in
        // SSspatial_grid's cells, which tg's MC initializes before the atoms (MobRoom keeps the grid uninitialized, so the
        // other tests' mob counts hold no oranges ears).
        room.In(() => Waiting.NoSleep(SSspatial_grid.Initialize()));
        room.In(() => TgStrings.Mount(DmWorldState.Of(World.Current).Files, Anomaly.Core.Tests.Tools.TgClone.Root)); // J2: the french beret and the italian moustache load their speech at init
        room.In(() =>
        {
            // Gaps S3: the fortune, fishing tip and junk mail papers pick from GLOB string lists that tg's GLOB reads at world start
            // (strings.dm:4-12): the room mounts the strings after its GLOB
            GLOB.InitGlobalfirst_names(); // the bitrunning revolutionary names itself from them (revolutionary.dm:96-105)
            GLOB.InitGlobalfirst_names_male();
            GLOB.InitGlobalfirst_names_female();
            GLOB.InitGloballast_names();
            GLOB.InitGlobalphonetic_alphabet(); // syndicate G3a: a deathrattle group names itself from it (implant_deathrattle.dm)
            GLOB.InitGlobalwisdoms();
            GLOB.InitGlobalfishing_tips();
            GLOB.InitGlobaljunkmail_messages();
            GLOB.InitGlobaladjectives(); // syndicate G4a: junk mail's pamphlet and red pill letter pick an adjective (mail.dm:232)
            GLOB.InitGlobalposibrain_names(); // silicons SI2: a positronic brain names itself from it (positronic.dm:33)
            GLOB.InitGlobalai_names(); // silicons SI9: the saboteur cyborg's chameleon picks an AI name (borgchameleon.dm:32)
            GLOB.InitGlobalgross_adjectives(); // gaps P3b: the fly's infused organs name themselves from it (odd_organ_name, names.dm:288)
            GLOB.InitGlobalclown_names(); // the legion-infested corpses: the consumed clown names itself from them (mining_corpses.dm:261)
            GLOB.InitGlobalgolem_names(); // and the corpses of the other species from their languages (terrum.dm:32, calcic.dm:31, draconic.dm:41, moffic.dm:34)
            GLOB.InitGlobalplasmaman_names();
            GLOB.InitGloballizard_names_male();
            GLOB.InitGloballizard_names_female();
            GLOB.InitGlobalmoth_first();
            GLOB.InitGlobalmoth_last();
            // the Thing's ruin machines (its phase depleters, the aggro gates) queue their links on SSqueuelinks, which tg's MC always makes
            if (SSqueuelinks == null)
                Dm.@new<datum_controller_subsystem_queuelinks>();
            // silicons SI3: the printers' auto-unlocking techwebs (the lathe's, the smelter's, the biogenerator's) register with SSresearch
            // and read its design table; tg's MC always makes it
            if (SSresearch == null)
            {
                Dm.@new<datum_controller_subsystem_research>();
                Waiting.NoSleep(SSresearch.Initialize());
            }
        });
        room.In(() =>
        {
            foreach (Type type in types)
            {
                int before = room.RuntimeText.Count();
                atom creation = Dm.@new(type, MobRoom.T(3, 3)) as atom;
                unexpected.AddRange(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(type, room.RuntimeText.Skip(before).ToList()));
                Assert.NotNull(creation);
                if (!QDELETED(creation))
                    qdel(creation, force: TRUE);
                Assert.True(QDELETED(creation), type.Name);
                // What a type spawns on creation is cleared as tg's create_and_destroy does (create_and_destroy.dm:31-35):
                // the random jumpsuits make a colour jumpsuit where they stand and delete themselves (color.dm:43-51), and the
                // hallucination anomaly makes its decoys on the floors around it (anomalies_hallucination.dm:57, orange(1)).
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        foreach (atom to_kill in Dm.each_cast<atom>(MobRoom.T(3 + dx, 3 + dy).contents.Copy()))
                            qdel(to_kill, force: TRUE);
                Assert.True(length(GLOB.mob_list) == 0, type.Name + " left a mob in GLOB.mob_list");
                Assert.True(length(GLOB.alive_mob_list) == 0, type.Name + " left a mob in GLOB.alive_mob_list");
                Assert.True(length(GLOB.dead_mob_list) == 0, type.Name + " left a mob in GLOB.dead_mob_list");
                Assert.True(length(GLOB.suicided_mob_list) == 0, type.Name + " left a mob in GLOB.suicided_mob_list");
                Assert.True(length(GLOB.objects_by_id_tag) == 0, type.Name + " left an id tag");
                Assert.True(SSobj.processing.len == 0, type.Name + " left a processing object");
            }
            Assert.Equal(0f, MobRoom.T(3, 3).contents.len);
        });
        Assert.True(unexpected.Count == 0, string.Join("\n", unexpected));
        using (room.World.Enter())
        {
            Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.AssertOnlyListedBadInitializeCalls(SSatoms.BadInitializeCalls); // GAP(M3 P1 -> lead)
            foreach (object path in Dm.each(SSgarbage.items))
            {
                var item = (datum_qdel_item)SSgarbage.items[path];
                Assert.False(Dm.truthy(item.no_respect_force), item.name + " ignored force");
                Assert.False(Dm.truthy(item.no_hint), item.name + " returned no hint");
            }
        }
    }

    [Fact]
    public void MobsCreatedAndDeletedInABatchLeaveTheGlobalStateAsItWas()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var made = new List<TgMob>();
            for (int i = 0; i < 20; i++)
                made.Add(NewMob(1 + i % 5, 1 + i / 5));
            Assert.Equal(20f, GLOB.mob_list.len);
            Assert.Equal(20f, GLOB.alive_mob_list.len);
            foreach (var m in made)
                qdel(m);
            Assert.Equal(0f, GLOB.mob_list.len);
            Assert.Equal(0f, GLOB.alive_mob_list.len);
        });
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>A mob that records its tag at the start of Initialize.</summary>
[DmPath("/mob/test_mob_tag_recorder")]
public class mob_test_mob_tag_recorder : TgMob
{
    [NotHashed] public string tag_at_initialize;
    [NotHashed] public object mapload_seen = "never initialized";

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        tag_at_initialize = tag;
        mapload_seen = mapload;
        return base.Initialize(mapload, __extra);
    }
}

/// <summary>A mob that is incapacitated from the start (build_incapacitated is read by Initialize's update_incapacitated).</summary>
[DmPath("/mob/test_mob_incap_from_start")]
public class mob_test_mob_incap_from_start : TgMob
{
    public override object build_incapacitated(params object[] __extra) => INCAPABLE_RESTRAINTS;
}

/// <summary>Records what a COMSIG_GLOB_MOB_CREATED sender looks like at the time of the signal.</summary>
[DmPath("/datum/test_mob_created_spy")]
public class datum_test_mob_created_spy : datum
{
    public sealed record Seen(TgMob mob, bool in_mob_list, bool in_alive_list, bool initialized);

    [NotHashed] public readonly List<Seen> seen = new();

    [SignalHandler]
    public virtual void on_created(datum source = null, object created = null)
    {
        var m = (TgMob)created;
        seen.Add(new Seen(m, Dm.@in(m, GLOB.mob_list), Dm.@in(m, GLOB.alive_mob_list), Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1))));
    }
}

/// <summary>An obj with one image HUD and one list HUD (hud_possible's assoc value is the hint, atom_hud.dm:54).</summary>
[DmPath("/obj/test_mob_hud")]
public class obj_test_mob_hud : obj
{
    public obj_test_mob_hud()
    {
        hud_possible = Dm.list("image_hud", Dm.kv("list_hud", HUD_LIST_LIST));
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        prepare_huds(); // objs do not call it themselves
        return dot;
    }
}

/// <summary>Mobs and objs that a loaded map makes before the Master initializes SSatoms (tick 20 of a boot).</summary>
public class MobMapLoadTests
{
    [Fact]
    public void AtomsMadeBeforeSSatomsInitializesAreInitializedByItInCreationOrderWithMapload()
    {
        var made = new List<atom>();
        var room = new BootedRoom(4, w =>
        {
            for (int i = 0; i < 4; i++)
            {
                made.Add(Dm.@new<mob_test_mob_tag_recorder>(locate(1 + i, 1, 1)));
                made.Add(Dm.@new<obj_test_mob_tagged_n>(locate(1 + i, 2, 1)));
            }
            // New already ran for every one of them (GenerateTag first), Initialize has not
            Assert.All(made.OfType<mob_test_mob_tag_recorder>(), m => Assert.NotNull(m.tag));
            Assert.All(made.OfType<mob_test_mob_tag_recorder>(), m => Assert.False(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1))));
            Assert.Equal(0f, GLOB.mob_list.len);
            Assert.Equal(0f, GLOB.objects_by_id_tag.len);
        });
        room.In(() =>
        {
            var mobs = made.OfType<mob_test_mob_tag_recorder>().ToList();
            Assert.Equal(new[] { "mob_1", "mob_2", "mob_3", "mob_4" }, mobs.Select(m => m.tag));
            Assert.All(mobs, m => Assert.True(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1))));
            Assert.All(mobs, m => Assert.True(Dm.truthy(m.mapload_seen), "Initialize(mapload) with mapload TRUE"));
            Assert.Equal(mobs.Cast<object>().ToArray(), Dm.each(GLOB.mob_list).Cast<object>().Where(o => o is mob_test_mob_tag_recorder).ToArray());
            Assert.All(mobs, m => Assert.Same(m, m.focus));
            Assert.Equal(4f, GLOB.objects_by_id_tag.len);
            foreach (var o in made.OfType<obj_test_mob_tagged_n>())
                Assert.Same(o, GLOB.objects_by_id_tag[o.id_tag]);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMobMadeAfterTheBootIsInitializedAtOnceWithoutMapload()
    {
        var room = new BootedRoom();
        room.In(() =>
        {
            var m = Dm.@new<mob_test_mob_tag_recorder>(locate(2, 2, 1));
            Assert.True(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1)));
            Assert.False(Dm.truthy(m.mapload_seen));
            Assert.True(Dm.@in(m, GLOB.mob_list));
        });
        Assert.Empty(room.RuntimeText);
    }
}
