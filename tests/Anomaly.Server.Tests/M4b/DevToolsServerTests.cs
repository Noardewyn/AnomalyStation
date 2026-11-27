using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Core.Kernel;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Server;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// The dev menu's tools (the owner, 2026-10-13), the server's half: a player's <c>spawn</c> is tg's spawn panel (any /obj,
/// /mob or /turf type, an amount, a dir, at a position or the player's feet), <c>delete</c> is tg's admin Delete (qdel, a
/// turf's ScrapeAway, never a player's mob), and <c>air</c> changes the air of the open turfs around a position through
/// tg's gas_mixture procs.
/// </summary>
public class DevToolsServerTests
{
    private static (TestServer Server, FakeRemote Alice) Start()
    {
        TestServer server = TestServer.Start(TestServer.DefaultConfig(devCommands: true));
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        return (server, alice);
    }

    private static string Run(TestServer server, FakeRemote alice, string line)
    {
        int before = alice.Messages(MessageId.ServerText).Count();
        alice.Action(ActionKind.DevCommand, text: line);
        server.Tick(2);
        string[] texts = alice.Messages(MessageId.ServerText).Skip(before).Select(m => System.Net.WebUtility.HtmlDecode(FakeCodec.DecodeServerText(m).Html)).ToArray();
        return string.Join("\n", texts);
    }

    [Fact]
    public void ASpawnMakesTheAmountOfAnyTypeFacingTheDirAndATurfTypeChangesTheTurf()
    {
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Assert.Contains("spawned 3 x /obj/item/wrench at (6,7,1)", Run(server, alice, "spawn /obj/item/wrench 6 7 1 3 8"));
            using (server.World.Enter())
            {
                obj_item_wrench[] wrenches = Dm.each<obj_item_wrench>(locate(6, 7, 1).contents).ToArray();
                Assert.Equal(3, wrenches.Length);
                Assert.All(wrenches, w => Assert.Equal(8f, Dm.num(w.dir)));
                Assert.All(wrenches, w => Assert.True((((int)w.flags_1) & (int)Defines.ADMIN_SPAWNED_1) != 0));
            }

            Assert.Contains("(8,8,1) is /turf/closed/wall now", Run(server, alice, "spawn /turf/closed/wall 8 8"));
            using (server.World.Enter())
                Assert.IsType<turf_closed_wall>(locate(8, 8, 1));

            // no position: at the player's feet (tg's "Current location")
            Assert.Contains("spawned", Run(server, alice, "spawn /obj/item/crowbar"));
            using (server.World.Enter())
            {
                mob me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Single(Dm.each<obj_item_crowbar>(get_turf(me) is turf t ? t.contents : null));
            }

            Assert.Contains("tg has no /obj, /mob or /turf type /obj/item/no_such_thing", Run(server, alice, "spawn /obj/item/no_such_thing 5 5"));
            Assert.Equal(0, server.Host.SessionOf(1).Strikes);
        }
    }

    [Fact]
    public void AReviveUnderGodmodeBringsThePlayerBackOnTheirFeet()
    {
        // the owner's play test (2026-10-13): dead, then godmode, then revive left the body in hard crit for good
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Run(server, alice, "kill me");
            Assert.Contains("still dead", Run(server, alice, "fullheal me"));
            Run(server, alice, "godmode me");
            Run(server, alice, "revive me");
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Equal((float)Defines.STABLE, Dm.num(me.stat));
                Assert.Equal((float)Defines.STANDING_UP, Dm.num(me.body_position));
                Assert.Equal(Dm.num(me.maxHealth), Dm.num(me.health));
                Assert.True(HAS_TRAIT(me, Defines.TRAIT_GODMODE)); // put back
            }
        }
    }

    [Fact]
    public void MobInfoAnswersThePanelWithThePlayersAndTgsNumbers()
    {
        // D140: the dev menu's Player tab asks mobinfo; the answer is one dev_mob ServerText, not a chat line
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Run(server, alice, "damage me brute 30 chest");
            Run(server, alice, "godmode me");
            int before = alice.Messages(MessageId.ServerText).Count();
            alice.Action(ActionKind.DevCommand, text: "mobinfo me");
            server.Tick(2);
            ServerText answer = alice.Messages(MessageId.ServerText).Skip(before).Select(FakeCodec.DecodeServerText).Single();
            Assert.Equal(ServerHost.MobInfoTextType, answer.Type);
            var info = Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(answer.Html));
            Assert.Null(info.Error);
            Assert.Single(info.Players);
            Assert.Equal("Alice", info.Players[0].Key);
            Assert.Equal("conscious", info.Value("Mob", "stat"));
            Assert.Equal("30", info.Value("Damage", "brute"));
            Assert.StartsWith("brute 30", info.Value("Bodyparts", "chest"));
            Assert.True(info.IsOn("godmode"));
            Assert.False(info.IsOn("noclip"));
            Assert.Contains(info.Sections, sec => sec.Name == "Organs" && sec.Rows.Count > 3);

            // another player by the mob's id; an unknown one says so
            alice.Action(ActionKind.DevCommand, text: "mobinfo #" + info.Players[0].Id);
            alice.Action(ActionKind.DevCommand, text: "mobinfo nobody");
            server.Tick(2);
            ServerText[] more = alice.Messages(MessageId.ServerText).Skip(before + 1).Select(FakeCodec.DecodeServerText).ToArray();
            Assert.Equal("30", Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(more[0].Html)).Value("Damage", "brute"));
            Assert.Contains("no player or mob", Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(more[1].Html)).Error);
        }
    }

    [Fact]
    public void SpawnedMobsAreCapsulesAndTheirNumberIsCapped()
    {
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Run(server, alice, "spawn /mob/living/carbon/human 9 9 1 2");
            using (server.World.Enter())
                Assert.All(Dm.each<mob_living_carbon_human>(locate(9, 9, 1).contents), h => Assert.True(server.Locomotion.IsRegistered(h)));
            Assert.Contains("the limit of 100 mobs", Run(server, alice, "spawn /mob/living/carbon/human 10 10 1 99"));
        }
    }

    [Fact]
    public void ADeleteQdelsAThingScrapesATurfAndSparesAPlayersMob()
    {
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Run(server, alice, "spawn /obj/item/wrench 5 5");
            long wrench;
            using (server.World.Enter())
                wrench = Dm.each<obj_item_wrench>(locate(5, 5, 1).contents).Single().dm_id;
            Assert.Contains("deleted", Run(server, alice, $"delete #{wrench}"));
            using (server.World.Enter())
                Assert.Empty(Dm.each<obj_item_wrench>(locate(5, 5, 1).contents));

            Run(server, alice, "spawn /turf/closed/wall 7 7");
            Assert.Contains("scraped away (now ", Run(server, alice, "delete 7 7"));
            using (server.World.Enter())
                Assert.IsAssignableFrom<turf_open>(locate(7, 7, 1));

            Assert.Contains("is a player's mob", Run(server, alice, "delete me"));
            Assert.Contains("is a player's mob", Run(server, alice, "delete Alice"));
            using (server.World.Enter())
                Assert.Contains(Dm.each<mob_living_carbon_human>(), m => m.ckey == "alice" && !QDELETED(m));
            Assert.Contains("no player or thing '#999999'", Run(server, alice, "delete #999999"));
        }
    }

    [Fact]
    public void AirAddsRemovesHeatsAndSetsTheGasOfTheTurfsAroundAPosition()
    {
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Type plasma = typeof(datum_gas_plasma);
            float Moles(int x, int y) => Dm.num(((turf_open)locate(x, y, 1)).air.moles[plasma]);

            // the test room's layout has a wall at (6, 6): a wall holds no air, its eight neighbours change. SSair waits while the
            // command's own gas is read (when LINDA first shares it depends on the Master's queue)
            using (server.World.Enter())
                GlobalProcs.SSair.can_fire = Dm.FALSE;
            Assert.Contains("on 8 open turf(s) within 1 of (6,6,1)", Run(server, alice, "air add plasma 50 6 6 1 1"));
            using (server.World.Enter())
            {
                Assert.Equal(50f, Moles(5, 5), 1);
                Assert.Equal(50f, Moles(7, 7), 1);
                Assert.Equal(0f, Moles(6, 9));
                GlobalProcs.SSair.can_fire = Dm.TRUE;
            }

            // below 0 removes, never below none (the whole patch: LINDA shares the gas between the commands)
            Run(server, alice, "air add plasma -80 6 6 1 1");
            using (server.World.Enter())
                Assert.Equal(0f, Moles(6, 5));
            Run(server, alice, "air add plasma 50 6 6 1 1");

            Run(server, alice, "air temp 900 6 6 1 2");
            using (server.World.Enter())
                Assert.InRange(((turf_open)locate(6, 5, 1)).air.temperature, 850f, 900f); // the walls already took a little heat

            Run(server, alice, "air remove plasma 6 6 1 2");
            using (server.World.Enter())
                Assert.Equal(0f, Moles(5, 5));

            Run(server, alice, "air set vacuum 5 5");
            using (server.World.Enter())
                Assert.True(Dm.num(((turf_open)locate(5, 5, 1)).air.total_moles()) < 0.01f);

            Assert.Contains("tg has no gas 'unobtainium'", Run(server, alice, "air add unobtainium 5 6 6"));
            // the rim's walls hold no air: within 2 of a corner only the floor changes
            // the rim's walls hold no air: within 1 of the corner floor only the floor changes
            Assert.Contains("on 4 open turf(s) within 1 of (2,2,1)", Run(server, alice, "air temp 300 2 2 1 1"));
        }
    }

    [Fact]
    public void GodmodeKeepsStatusEffectsOffAndInvulnerabilityRefusesOtherMobsClicks()
    {
        // the owner (2026-11-02): no effects under godmode; invuln: no one else can act on the mob
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            FakeRemote bob = server.Join("Bob");
            bob.Motion(0, 0);
            server.Tick(2);
            Run(server, alice, "godmode me");
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Null(Waiting.NoSleep(me.apply_status_effect(typeof(datum_status_effect_dizziness), 10 * Defines.SECONDS)));
                Assert.True(HAS_TRAIT(me, Defines.TRAIT_SHOCKIMMUNE));
            }
            Assert.Contains("applied", Run(server, alice, "effect me add dizziness 5")); // the dev command's own effect still applies
            Run(server, alice, "godmode me");

            Assert.Contains("invulnerable on", Run(server, alice, "invuln me"));
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                mob_living_carbon_human other = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "bob");
                Assert.True(HAS_TRAIT(me, Defines.TRAIT_GODMODE));
                Assert.True(HAS_TRAIT(me, Defines.TRAIT_PUSHIMMUNE));
                Assert.True(DevCommands.ShieldsFrom(other, me));
                Assert.False(DevCommands.ShieldsFrom(me, me)); // the mob itself still acts on itself
                Assert.Null(Waiting.NoSleep(me.apply_status_effect(typeof(datum_status_effect_dizziness), 10 * Defines.SECONDS)));
            }
            Assert.Contains("invulnerable off", Run(server, alice, "invuln me"));
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.False(HAS_TRAIT(me, Defines.TRAIT_GODMODE));
                Assert.False(HAS_TRAIT(me, Defines.TRAIT_PUSHIMMUNE));
            }
        }
    }

    [Fact]
    public void AGhostsMobInfoAndCommandsAreItsBodys()
    {
        // the owner (2026-11-02): the Player tab shows a ghost's data and its commands work, on the body it left
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Run(server, alice, "kill me");
            long ghostId;
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                if (me.mind == null) Waiting.NoSleep(me.mind_initialize()); // a test server's joins have no mind
                ghostId = Assert.IsType<mob_dead_observer>(me.ghostize(Dm.TRUE)).dm_id;
            }
            server.Tick(2);
            int before = alice.Messages(MessageId.ServerText).Count();
            alice.Action(ActionKind.DevCommand, text: "mobinfo #" + ghostId);
            server.Tick(2);
            ServerText answer = alice.Messages(MessageId.ServerText).Skip(before).Select(FakeCodec.DecodeServerText).Single();
            var info = Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(answer.Html));
            Assert.Null(info.Error);
            Assert.Equal("dead", info.Value("Mob", "stat"));
            Assert.DoesNotContain("not a living mob", Run(server, alice, "damage #" + ghostId + " brute 5"));
        }
    }

    [Fact]
    public void ABodilessGhostStillGetsItsCommandsAndEspShowsTheGhosts()
    {
        // the owner (2026-11-02): a ghost with no body keeps revive and the teleports; esp lets a living mob see the ghosts
        (TestServer server, FakeRemote alice) = Start();
        using (server)
        {
            Assert.Contains("esp on", Run(server, alice, "esp me"));
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Equal((float)Defines.SEE_INVISIBLE_ADMIN, me.see_invisible);
            }
            Assert.Contains("esp off", Run(server, alice, "esp me"));

            // invisimin (DV576): ghost-like invisibility and a see-through model, and back
            Assert.Contains("invisimin on", Run(server, alice, "invisimin me"));
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Equal((float)Defines.INVISIBILITY_OBSERVER, me.invisibility);
                Assert.Equal((float)DevCommands.InvisiminAlpha, me.alpha);
            }
            Assert.Contains("invisimin off", Run(server, alice, "invisimin me"));
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.Equal(0f, me.invisibility);
                Assert.Equal(255f, me.alpha);
            }
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                Assert.True(me.see_invisible < Defines.SEE_INVISIBLE_OBSERVER);
            }

            long ghostId;
            using (server.World.Enter())
            {
                mob_living_carbon_human me = Dm.each<mob_living_carbon_human>().First(m => m.ckey == "alice");
                if (me.mind == null) Waiting.NoSleep(me.mind_initialize()); // a test server's joins have no mind
                ghostId = Assert.IsType<mob_dead_observer>(me.ghostize(Dm.FALSE)).dm_id;
                qdel(me);
            }
            server.Tick(2);
            int before = alice.Messages(MessageId.ServerText).Count();
            alice.Action(ActionKind.DevCommand, text: "mobinfo #" + ghostId);
            server.Tick(2);
            ServerText answer = alice.Messages(MessageId.ServerText).Skip(before).Select(FakeCodec.DecodeServerText).Single();
            var info = Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(answer.Html));
            Assert.Null(info.Error);
            Assert.True(info.IsBodilessGhost);
            Assert.Contains("a new body", Run(server, alice, "revive #" + ghostId)); // a new body (tg's admin respawn)
        }
    }
}
