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
/// The procs of mob.dm and objs.dm that package C leaves for later milestones (translation-rules.md 1.4: "never leave a
/// silent empty body"): each is Dm.NotTranslated, so a world with NotTranslatedThrows on stops at it and names the tg site,
/// and the pieces of the same files that are translated but only reach other types' leaves (observers, client colours, the
/// HUD) behave as tg's.
/// </summary>
public class MobSeamTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    private static void AssertSeam(string file, Action call)
    {
        var e = Assert.Throws<DmNotTranslatedException>(call);
        Assert.StartsWith(file + ":", e.Where);
    }

    private static void AssertSeam(string file, Func<DmTask<object>> call) => AssertSeam(file, () => Waiting.NoSleep(call()));

    private static void AssertSeam(string file, Func<DmTask> call) => AssertSeam(file, () => Waiting.NoSleep(call()));

    [Fact]
    public void TheMobProcsOfLaterMilestonesStopAtTheirTgSiteInsteadOfDoingNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var other = NewAt<obj>();
            const string mobdm = "code/modules/mob/mob.dm";
            // M4a P2: show_message, attack_ui, execute_mode, swap_hand, perform_hand_swap and activate_hand are translated
            // (tests/Anomaly.Core.Tests/Tg/M4aHands/P2); they are no longer seams.
            // visible_message is translated (closet-air fix: mob.dm:280, to the hearers in view); its runechat and emphasis are deferred leaves.
            // M9 slice A: audible_message and runechat_prefs_check are translated (mob.dm:358-438), as visible_message's runechat and emphasis.
            // run_examinate and the living handle_eye_contact are translated (M4a P2, D128)
            // M6 slice 3b: Topic is translated (D154: a chat link reaches it through ClientSeam.Topic)
            AssertSeam(mobdm, () => m.check_respawn_delay());
            AssertSeam(mobdm, () => m.get_status_tab_items());
            // The ghosts (D151): get_ghost and grab_ghost are translated (mob.dm:960-966); a mob without a mind has no ghost.
            Assert.Null(m.get_ghost());
            Assert.Null(m.grab_ghost());
            // notify_revival is translated (M8 slice E, the defibrillator)
            // M9 slice H: fully_replace_character_name now reaches the translated replace_identification_name below
            Assert.True(Dm.truthy(Waiting.NoSleep(m.fully_replace_character_name("a", "b"))));
            Assert.Equal("b", Dm.str(m.real_name));
            // M9 slice H: replace_identification_name is translated (mob.dm:1189): the carried ID and PDA take the new name
            var id = Dm.@new<obj_item_card_id>(m);
            id.registered_name = "a";
            var pda = Dm.@new<obj_item_modular_computer_pda>(m);
            Waiting.NoSleep(id.forceMove(pda)); // inside a PDA too: tg searches all of the mob's contents
            pda.saved_identification = "a";
            Waiting.NoSleep(m.replace_identification_name("a", "b"));
            Assert.Equal("b", Dm.str(id.registered_name));
            Assert.Equal("b", Dm.str(pda.saved_identification));
            // gaps S3: can_write is translated (mob.dm:1279); without a tool nobody writes
            Assert.False(Dm.truthy(Waiting.NoSleep(m.can_write(null))));
            // gaps package G4: open_memory_panel is translated (mob.dm:1562); a mob without a mind is only told it has none
            Waiting.NoSleep(m.open_memory_panel());
            // M2 F1: update_equipment_speed_mods is translated (mob.dm:1486); a mob with nothing equipped has no equipment slowdown
            Waiting.NoSleep(m.update_equipment_speed_mods());
            AssertSeam(mobdm, () => m.serialize_list(null, null));
            AssertSeam(mobdm, () => m.vv_get_dropdown());
            AssertSeam(mobdm, () => m.vv_do_topic(Dm.list()));
            // M4a P2: mode (mode__proc) is translated: it queues or calls execute_mode (DEFAULT_QUEUE_OR_CALL_VERB).
        });
    }

    [Fact]
    public void TheLivingMobProcsOfLaterMilestonesStopAtTheirTgSiteToo()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var living = NewAt<mob_living>();
            const string mobdm = "code/modules/mob/mob.dm";
            AssertSeam(mobdm, () => living.on_block_magic_effects(MAGIC_RESISTANCE, Dm.list()));
            var carbon = NewAt<mob_living_carbon>();
            AssertSeam(mobdm, () => carbon.can_eye_contact());
            Assert.True(Dm.eq(living.can_eye_contact(), TRUE));
        });
    }

    [Fact]
    public void TheObjProcsOfLaterMilestonesStopAtTheirTgSiteInsteadOfDoingNothing()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var o = NewAt<obj>();
            const string objsdm = "code/game/objects/objs.dm";
            // handle_internal_lifeform is translated (M5 slice 1): ObjBreathingTests.
            // freeze is translated (gaps SR1: the organ box freezes what it holds, objs.dm:265): StorageSR1Tests.
            // can_be_unfasten_wrench and default_unfasten_wrench are translated (M5 slice 7: the machine frame's wrench).
            // handle_ricochet is translated (M7 projectiles slice: objs.dm:248): ProjectileTests.
            // shock is translated (M7 shocks slice: objs.dm:402): ShockTests.
            AssertSeam(objsdm, () => o.vv_get_dropdown());
            AssertSeam(objsdm, () => o.vv_do_topic(Dm.list()));
        });
    }

    [Fact]
    public void TheSmallPiecesOfMobDmThatAreTranslatedButNeedNoClient()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var o = NewAt<obj>();
            m.examining(o, Dm.list()); // `return`: a hook for subtypes
            m.clear_from_recent_examines("ref"); // no client: returns (mob.dm:681-683)
            m.handle_eye_contact(null);
            m.update_pull_hud_icon(); // no HUD: nothing to update
            Assert.True(Dm.eq(m.update_rest_hud_icon(), FALSE)); // `hud_used?...`: no HUD
            m.update_mouse_pointer(); // no client
            m.sync_lighting_plane_cutoff();
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMobsClientPartsRunWithTheClientLayerOfM1()
    {
        // M1 P1 translated the client parts (reset_perspective, clear_from_recent_examines, update_mouse_pointer).
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.client = Dm.@new<client_test_m1_client>();
            Assert.True(Dm.eq(Waiting.NoSleep(m.reset_perspective(null)), TRUE)); // on a turf: the mob's own perspective
            Assert.Equal(MOB_PERSPECTIVE, m.client.perspective);
            m.clear_from_recent_examines("ref");
            m.update_mouse_pointer();
            m.client = null;
        });
        Assert.Empty(room.RuntimeText);
    }

    // ---- Destroy reaching other types' leaves ----

    [Fact]
    public void DestroyResetsThePerspectiveOfEveryObserverOfTheMob()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var a = NewAt<mob_dead_test_mob_watcher>(4, 4);
            var b = NewAt<mob_dead_test_mob_watcher>(5, 5);
            m.observers = Dm.list(a, b);
            a.perspectives.Clear(); // set_focus(src) of its own Initialize reset it to itself
            b.perspectives.Clear();
            qdel(m);
            Assert.Equal(new object[] { null }, a.perspectives.ToArray()); // observe.reset_perspective(null) (mob.dm:46-48)
            Assert.Equal(new object[] { null }, b.perspectives.ToArray());
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DestroyDeletesTheClientColoursAndEmptiesTheList()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var one = Dm.@new<datum>();
            var two = Dm.@new<datum>();
            m.client_colours = Dm.list(one, two);
            qdel(m);
            Assert.True(QDELETED(one));
            Assert.True(QDELETED(two));
            Assert.Equal(0f, m.client_colours.len); // QDEL_LIST cuts the list
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DestroyDeletesTheHudOfTheMob()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            var hud = Dm.@new<datum_hud>(m);
            m.hud_used = hud;
            qdel(m);
            Assert.True(QDELETED(hud));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DestroyOfAMobWithScreenAlertsAPersistentClientOrAMockClientRunsClean()
    {
        // mock_client.mob (tg's mock clients) is not translated; clear_alert (P3) and persistent_client?.set_mob (M1 P1) are:
        // the alerts are cleared and the persistent client lets the deleted mob go
        var room = new MobRoom();
        room.In(() =>
        {
            var withAlert = NewAt<TgMob>();
            withAlert.alerts = Dm.list("hungry");
            qdel(withAlert);
            var withPersistent = NewAt<TgMob>();
            withPersistent.persistent_client = Dm.@new<datum_persistent_client>();
            qdel(withPersistent);
            var withMock = NewAt<TgMob>();
            withMock.mock_client = Dm.@new<datum_client_interface>();
            qdel(withMock);
        });
        // M2 P2 (with P3's alerts): Destroy clears each alert (mob.dm:43); "hungry" holds no alert, so clear_alert returns. M8 slice E
        // translated the mock client's teardown (mob.dm:55), so none of the three stops at a seam any more.
        Assert.Empty(room.Runtimes);
    }

    [Fact]
    public void AMemoryPanelTakesTheHolderFromAClientOrFromAMobsClient()
    {
        // "user can either be a client or a mob due to byondcode(tm)" (mob.dm:1577-1584)
        var room = new MobRoom();
        room.In(() =>
        {
            var client = Dm.@new<client_test_m1_client>();
            if (SSjob == null) Dm.@new<datum_controller_subsystem_job>(); // a mind's New reads SSjob (_mind.dm:116), which this bare world lacks
            var mind = Dm.@new<datum_mind>();
            var fromClient = Dm.@new<datum_memory_panel>(client, mind);
            Assert.Same(client, fromClient.holder);
            Assert.Same(mind, fromClient.mind_reference);

            var m = NewAt<TgMob>();
            var fromMob = Dm.@new<datum_memory_panel>(m, mind);
            Assert.Null(fromMob.holder); // a mob without a client
            m.client = client;
            Assert.Same(client, Dm.@new<datum_memory_panel>(m, mind).holder);
            m.client = null;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void DestroyOfAMobWithAMindClearsTheMindsCurrent()
    {
        // `if(mind?.current == src) mind.set_current(null)` (mob.dm:51-52; translated with the ghosts, D151)
        var room = new MobRoom();
        datum_mind mind = null;
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            if (SSjob == null) Dm.@new<datum_controller_subsystem_job>(); // a mind's New reads SSjob (_mind.dm:116), which this bare world lacks
            mind = Dm.@new<datum_mind>();
            m.mind = mind;
            mind.set_current(m);
            Assert.Same(m, mind.current);
            qdel(m);
            Assert.Null(mind.current);
        });
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>An observer that records the perspective resets it is asked for.</summary>
[DmPath("/mob/dead/test_mob_watcher")]
public class mob_dead_test_mob_watcher : mob_dead
{
    [NotHashed] public readonly List<object> perspectives = new();

    public override async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        perspectives.Add(new_eye);
        return await base.reset_perspective(new_eye, __extra);
    }
}
