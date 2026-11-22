using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using Actor = Anomaly.Server.Tests.Integration.M8ScenarioTests.Actor;
using Scenario = Anomaly.Server.Tests.Integration.M8ScenarioTests.Scenario;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M8z's done-when (D234, D242) through the real server on MetaStation with three real clients: Alice acting, Bob the subject,
/// Carol watching, in the medbay lobby. Bob, knocked out, is dragged onto Alice: his strip window opens; she takes his shoes and his ID
/// (Carol reads "tries to remove") and puts the shoes back on him. She points at him (Carol reads the line, the arrow stands over him).
/// She climbs a table and vaults a railing. She rests by the HUD button and gets up; pulled by Carol, she resists out with the key. Bob,
/// in crit, succumbs by his alert's prompt. A second test runs it all twice from a fresh boot and compares the state hashes.
/// </summary>
public class M8zScenarioTests(ITestOutputHelper output)
{
    private const ulong Seed = 83;

    private static bool Told(Actor a, string text) => a.Texts.Any(t => t.Contains(text, StringComparison.Ordinal));

    private static turf Free(Scenario s, int x, int y) => s.In(() =>
    {
        for (int radius = 0; radius < 6; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == radius && locate(x + dx, y + dy, 1) is turf_open t && !Dm.truthy(t.density)
                        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_structure_chair || a is mob)
                        && locate(x + dx, y + dy - 1, 1) is turf_open below && !Dm.truthy(below.density)
                        && !Dm.each_cast<atom>(below.contents).Any(a => Dm.truthy(a.density) || a is mob))
                        return t;
                }
            }
        }
        return null;
    });

    private static UiWindowState Window(Actor a, string name) => Assert.Single(a.P.Session.Replica.UiWindows.Values, v => v.Interface == name);

    private static void Act(Actor a, string window, string action, params (string Key, string Value)[] values)
        => Assert.True(a.P.Session.UiAct(Window(a, window).Window, action, UiValue.ObjectOf(values.Select(v => new KeyValuePair<string, UiValue>(v.Key, UiValue.Of(v.Value))))));

    private static void Drop(Scenario s, atom source, atom over, float seconds = 1)
    {
        s.Do(() => s.Alice.Mob.next_click = -1);
        Assert.True(s.Alice.P.Session.Drop(new DragRelease(DragGestureGameplayTests.Entity(source.dm_id), DragGestureGameplayTests.Entity(over.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        s.Run(seconds);
    }

    private static void StripPart(Scenario s)
    {
        Actor alice = s.Alice, bob = s.Bob, carol = s.Carol;
        s.Place(alice, Free(s, 94, 104));
        s.Place(bob, s.In(() => get_step(alice.Mob, NORTH) as turf));
        s.Place(carol, s.In(() => get_step(alice.Mob, EAST) as turf));
        s.Do(() => Waiting.NoSleep(bob.Mob.Unconscious(120 * SECONDS)));
        s.Run(1);
        var shoes = s.In(() => (obj_item)bob.Mob.shoes);
        var id = s.In(() => (obj_item)bob.Mob.wear_id);
        Assert.NotNull(shoes);
        Assert.NotNull(id);
        string bobName = s.In(() => bob.Mob.name);

        Drop(s, bob.Mob, alice.Mob);
        Window(alice, "StripMenu");
        Act(alice, "StripMenu", "use", ("key", "shoes"));
        s.Run(s.In(() => shoes.strip_delay) / 10f + 1);
        Assert.Null(s.In(() => bob.Mob.shoes));
        Assert.True(Told(carol, "tries to remove " + bobName + "'s"));
        Act(alice, "StripMenu", "use", ("key", "id"));
        s.Run(s.In(() => id.strip_delay) / 10f + 1);
        Assert.Null(s.In(() => bob.Mob.wear_id));

        // the shoes back on him from her hand (the shoes' equip_delay_other)
        s.Do(() => Waiting.NoSleep(alice.Mob.put_in_hands(shoes)));
        s.Run(0.5f);
        Act(alice, "StripMenu", "use", ("key", "shoes"));
        s.Run(s.In(() => shoes.equip_delay_other) / 10f + 1.5f);
        Assert.Same(shoes, s.In(() => bob.Mob.shoes));
        s.Measured["strip"] = $"shoes {s.In(() => shoes.strip_delay)} ds off, {s.In(() => shoes.equip_delay_other)} ds on";
    }

    private static void PointPart(Scenario s)
    {
        Actor alice = s.Alice, bob = s.Bob, carol = s.Carol;
        s.Click(bob.Mob, button: ClickButton.Middle, modifiers: ClickModifiers.Shift, seconds: 0.3f);
        string aliceName = s.In(() => alice.Mob.name), bobName = s.In(() => bob.Mob.name);
        Assert.True(Told(carol, aliceName + "</span> points at " + bobName));
        var arrow = s.In(() => Dm.each_cast<atom>(range(1, alice.Mob)).OfType<obj_effect_temp_visual_point>().Single());
        Assert.True(carol.P.Session.Replica.TryGet(arrow.dm_id, out _));
        s.Run(3);
    }

    private static void ClimbPart(Scenario s)
    {
        Actor alice = s.Alice;
        turf spot = Free(s, 97, 106);
        int x = s.In(() => (int)spot.x), y = s.In(() => (int)spot.y);
        var table = s.In(() => Dm.@new<obj_structure_table>(locate(x, y, 1)));
        s.Place(alice, s.In(() => locate(x, y - 1, 1) as turf));
        Drop(s, alice.Mob, table, 2.5f);
        Assert.True(s.In(() => alice.Mob.loc == table.loc), "on the table");
        Assert.True(Told(alice, "You climb onto the table."));
        s.Run(2.5f); // the stun
        s.Do(() => qdel(table));
        s.Run(0.5f);

        var railing = s.In(() => Dm.@new<obj_structure_railing>(locate(x, y, 1)));
        s.Place(alice, s.In(() => locate(x, y, 1) as turf));
        Drop(s, alice.Mob, railing, 2.5f);
        Assert.Equal((x, y - 1), s.In(() => ((int)alice.Mob.x, (int)alice.Mob.y)));
        s.Run(2.5f);
        s.Do(() => qdel(railing));
        s.Run(0.5f);
    }

    private static void HudPart(Scenario s)
    {
        Actor alice = s.Alice, carol = s.Carol;
        var rest = s.In(() => (atom)alice.Mob.hud_used.screen_objects[HUD_MOB_REST]);
        s.ClickScreen(rest, 1);
        Assert.True(s.In(() => Dm.truthy(alice.Mob.resting)));
        s.ClickScreen(rest, 2);
        Assert.False(s.In(() => Dm.truthy(alice.Mob.resting)));
        s.Run(1);

        s.Place(carol, s.In(() => get_step(alice.Mob, EAST) as turf));
        s.Do(() => Waiting.NoSleep(carol.Mob.start_pulling(alice.Mob)));
        s.Run(0.5f);
        Assert.Same(alice.Mob, s.In(() => carol.Mob.pulling));
        s.Key(KeybindingCode.Resist, 0.5f);
        alice.P.Session.Keybinding(KeybindingCode.Resist, up: true);
        s.Run(0.5f);
        Assert.Null(s.In(() => carol.Mob.pulling));
    }

    private static void SuccumbPart(Scenario s)
    {
        Actor bob = s.Bob;
        s.Do(() => Waiting.NoSleep(bob.Mob.adjust_oxy_loss(160)));
        s.Run(2);
        Assert.True(s.In(() => GlobalProcs.CAN_SUCCUMB(bob.Mob)));
        var alert = s.In(() => (datum)bob.Mob.alerts[ALERT_SUCCUMB]);
        Assert.True(bob.P.Session.Click(new ClickTarget(TargetKind.Screen, alert.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        s.Run(0.5f);
        Act(bob, "TextInputModal", "submit", ("entry", ""));
        s.Run(1);
        Assert.Equal(DEAD, s.In(() => bob.Mob.stat));
        Assert.True(Told(bob, "You have given up life and succumbed to death."));
    }

    private static void All(Scenario s)
    {
        StripPart(s);
        PointPart(s);
        ClimbPart(s);
        HudPart(s);
        SuccumbPart(s);
    }

    [MetaServerFact]
    public void A_player_strips_and_dresses_a_friend_points_climbs_rests_resists_and_the_friend_succumbs()
    {
        Scenario s = M8ScenarioTests.Start(output, Seed);
        using (s.W)
        {
            All(s);
            foreach (KeyValuePair<string, string> m in s.Measured)
                output.WriteLine($"{m.Key}: {m.Value}");
            M8ScenarioTests.AssertClean(s);
        }
    }

    [MetaServerFact]
    public void The_m8z_scenario_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            Scenario s = M8ScenarioTests.Start(output, Seed);
            using (s.W)
            {
                All(s);
                return (s.W.World.ComputeStateHash(), s.W.Ticks);
            }
        }

        var first = Once();
        var second = Once();
        output.WriteLine($"first run: {first.Hash:x16} at tick {first.Tick}; second: {second.Hash:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Hash, second.Hash);
    }
}
