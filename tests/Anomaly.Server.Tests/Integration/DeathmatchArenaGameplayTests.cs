using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The deathmatch arena through the real server (docs/ai/specs/deathmatch): its map json turns the auto-respawn on (DV583), a player
/// joins in the spawn room, and a killed player comes back after the map's delay; its scoreboard (DV585) counts a kill by a laser and one
/// by a knife and one by a grenade, keeps the counts over a respawn, and shows them to a player holding the scoreboard key.
/// </summary>
public class DeathmatchArenaGameplayTests
{
    private const int Second = 20;

    internal static RealHeadlessWorld Boot(string maps)
        => new(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "deathmatch_arena", MapsFolder = maps, MoveSpeedScale = 1f }, strict: false);

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // the spawn room: plan x 8..16, y 5..11 from the top of the 54-row map (DM y counts from the bottom)
    private static bool InSpawnRoom(RealHeadlessWorld w, mob m)
        => In(w, () => get_turf(m) is turf t && t.x >= 8 && t.x <= 16 && t.y >= 55 - 11 && t.y <= 55 - 5 && t.z == 1);

    [Fact]
    public void TheArenaStartsWithItsRespawnOnAndAKilledPlayerComesBackInTheSpawnRoom()
    {
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null || !File.Exists(Path.Combine(maps, "map_jsons", "deathmatch_arena.json")))
            return;
        using RealHeadlessWorld w = Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Alice");
        w.Run(2);
        Assert.Equal((true, 10f), w.Host.AutoRespawn);
        mob_living first = w.MobOf(p);
        Assert.NotNull(first);
        Assert.True(InSpawnRoom(w, first), "a player joins in the spawn room");

        w.Host.EnqueueConsoleLine("kill Alice");
        w.Run(9 * Second);
        Assert.Same(first, w.MobOf(p)); // not yet
        w.Run(2 * Second);
        mob_living second = w.MobOf(p);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotEqual((float)DEAD, In(w, () => second.stat));
        Assert.True(InSpawnRoom(w, second), "a respawn is in the spawn room");
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static void Hold<T>(RealHeadlessWorld w, mob_living_carbon_human who) where T : obj_item
        => Do(w, () =>
        {
            Waiting.NoSleep(who.dropItemToGround(who.get_active_held_item() as obj_item));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(Dm.@new<T>(who.loc)))));
        });

    private static (int Kills, int Deaths) ScoreOf(RealHeadlessWorld w, string name)
        => In(w, () => datum_deathmatch_scoreboard.Current.Scores.TryGetValue(Dm.ckey(name), out var s) ? (s.Kills, s.Deaths) : (0, 0));

    private static string[] Panel(RealHeadlessWorld w, mob m)
        => In(w, () => m.client == null ? Array.Empty<string>()
            : Dm.each<atom_movable_screen_text>(m.client.screen).Select(t => t.maptext).Where(t => t != null && t.Contains("Pixellari")).ToArray());

    [Fact]
    public void TheScoreboardCountsLaserKnifeAndGrenadeKillsOverRespawnsAndShowsThemOnItsKey()
    {
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null || !File.Exists(Path.Combine(maps, "map_jsons", "deathmatch_arena.json")))
            return;
        using RealHeadlessWorld w = Boot(maps);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(2);
        Assert.NotNull(In(w, () => datum_deathmatch_scoreboard.Current));
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(10, 47, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 47, 1)));
            Waiting.NoSleep(bobMob.adjust_oxy_loss(178)); // one laser from dead (oxygen loss: no limb caps it)
        });
        Hold<obj_item_gun_energy_laser>(w, aliceMob);
        w.Run(Second);

        // a laser: the projectile's firer kills
        Do(w, () =>
        {
            aliceMob.next_click = -1;
            // again right before the shot (his breaths since recovered some), with a breath's margin before the laser lands
            Waiting.NoSleep(bobMob.set_oxy_loss(190));
        });
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => bobMob.stat) == DEAD, In(w, () => $"stat {bobMob.stat} brute {bobMob.get_brute_loss()} burn {bobMob.get_fire_loss()} health {bobMob.health} at {bobMob.x},{bobMob.y} alice {aliceMob.x},{aliceMob.y} held {aliceMob.get_active_held_item()}"));
        Assert.Equal((1, 0), ScoreOf(w, "Alice"));
        Assert.Equal((0, 1), ScoreOf(w, "Bob"));

        // Bob comes back after the map's 10 seconds, and knifes Alice: tg's melee lastattackerckey kills
        w.Run(11 * Second);
        var bobAgain = (mob_living_carbon_human)w.MobOf(bob);
        Assert.NotSame(bobMob, bobAgain);
        Assert.Equal((0, 1), ScoreOf(w, "Bob")); // the respawn keeps the counts
        Do(w, () =>
        {
            Waiting.NoSleep(bobAgain.forceMove(locate(11, 47, 1)));
            Waiting.NoSleep(aliceMob.adjust_oxy_loss(185));
        });
        Hold<obj_item_knife_combat>(w, bobAgain);
        bob.Session.ToggleCombatMode();
        w.Run(Second);
        for (int i = 0; i < 5 && In(w, () => aliceMob.stat) != DEAD; i++)
        {
            Do(w, () => bobAgain.next_click = -1);
            Assert.True(bob.Session.Click(new ClickTarget(TargetKind.Entity, aliceMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
        }
        Assert.Equal((float)DEAD, In(w, () => aliceMob.stat));
        Assert.Equal((1, 1), ScoreOf(w, "Alice"));
        Assert.Equal((1, 1), ScoreOf(w, "Bob"));
        Assert.Empty(In(w, () => datum_deathmatch_scoreboard.Current.Rows().Where(r => r.Name != "Alice" && r.Name != "Bob").ToArray()));

        // the scoreboard key: held, Bob's screen shows the three columns (names, kills, deaths); released, they go
        Assert.Empty(Panel(w, bobAgain));
        bob.Session.Keybinding(KeybindingCode.Scoreboard);
        w.Run(5);
        string[] panel = Panel(w, bobAgain);
        Assert.True(panel.Length == 3, string.Join(" /// ", panel));
        Assert.Contains(panel, t => t.Contains("Alice") && t.Contains("Bob"));
        Assert.Contains(panel, t => t.Contains("Kills"));
        Assert.Contains(panel, t => t.Contains("Deaths"));
        Assert.True(bob.Session.Replica.Screen.Values.Any(e => bob.Session.Replica.TryGetAppearance(e.Appearance, out AppearanceEntry a) && !a.Maptext.IsNone
            && bob.Session.Replica.ResolveString(a.Maptext).Contains("Deaths")), "the client gets the panel as HUD maptext");
        bob.Session.Keybinding(KeybindingCode.Scoreboard, up: true);
        w.Run(5);
        Assert.Empty(Panel(w, bobAgain));

        // a grenade the respawned Alice handled last blows up at Bob: the player behind an explosion kills
        w.Run(11 * Second);
        mob_living aliceAgain = w.MobOf(alice);
        Assert.NotSame(aliceMob, aliceAgain);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceAgain.forceMove(locate(16, 50, 1))); // out of the blast
            Waiting.NoSleep(bobAgain.adjust_oxy_loss(170));
            var grenade = Dm.@new<obj_item_grenade_frag>(bobAgain.loc);
            grenade.fingerprintslast = aliceAgain.ckey;
            Waiting.NoSleep(grenade.detonate());
        });
        w.Run(Second);
        Assert.Equal((float)DEAD, In(w, () => bobAgain.stat));
        Assert.Equal(2, ScoreOf(w, "Alice").Kills);
        Assert.Equal((1, 2), ScoreOf(w, "Bob"));
    }
}
