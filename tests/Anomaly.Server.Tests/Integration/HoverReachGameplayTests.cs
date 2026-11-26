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
/// DV589 (the designer, 2026-11-26): the prompt beside the crosshair shows only when the player can reach the thing looked at. Through a real server
/// and client: the answer to a hover on a world thing the mob cannot reach by tg's own test of a click (ClickOn's <c>IsReachableBy</c> with the held
/// item's reach) is the empty message (a zero-length name, no hints); in reach it is tg's screentip as before. The next tile, a table's corner and a
/// spear's reach of two are in reach; three tiles, a directional window and a wall corner are not; a ghost, a screen target and the ceiling's own
/// range keep their rules; the client's repeated hover brings the hints once the player is next to the thing; the zone looked at on a carbon is
/// applied out of reach too.
/// </summary>
public class HoverReachGameplayTests
{
    private const int Second = 20;
    private const int X = 10, Y = 10; // where Alice stands, in a cleared box of floor

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private sealed class Scene : IDisposable
    {
        public RealHeadlessWorld W;
        public RealHeadlessWorld.Player Alice;
        public mob_living_carbon_human AliceMob;
        public int Z;
        public readonly List<ContextHintsMessage> Hints = new();

        public turf At(int dx, int dy) => locate(X + dx, Y + dy, Z);

        public void Dispose() => W.Dispose();
    }

    // Alice at (X, Y) in a box of bare floor from (X - 3, Y - 3) to (X + 5, Y + 3), her hands empty.
    private static Scene Room(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        var scene = new Scene { W = w, Alice = w.Join("Alice") };
        scene.AliceMob = (mob_living_carbon_human)w.MobOf(scene.Alice);
        scene.Z = (int)In(w, () => Dm.num(scene.AliceMob.z));
        In(w, () =>
        {
            for (int x = X - 3; x <= X + 5; x++)
            {
                for (int y = Y - 3; y <= Y + 3; y++)
                {
                    turf cell = locate(x, y, scene.Z);
                    foreach (obj thing in Dm.each_cast<obj>(cell).ToList())
                        qdel(thing);
                    if (cell is not turf_open_floor_iron)
                        Waiting.NoSleep(cell.ChangeTurf(typeof(turf_open_floor_iron)));
                }
            }
            Waiting.NoSleep(scene.AliceMob.forceMove(scene.At(0, 0)));
        });
        w.Run(Second);
        Assert.Null(In(w, () => scene.AliceMob.get_active_held_item()));
        scene.Alice.Client.ContextHintsReceived += scene.Hints.Add;
        return scene;
    }

    private static obj_item_pen Pen(Scene s, int dx, int dy) => In(s.W, () => Dm.@new<obj_item_pen>(s.At(dx, dy)));

    private static ClickTarget On(atom thing, AimZone zone = AimZone.None) => new(TargetKind.Entity, thing.dm_id, 0, 0, 0, 16, 16, zone);

    // A hover on the target (sent: a new target, or the same one again past the repeat interval), and the server's newest answer for it.
    private static ContextHintsMessage Look(Scene s, ClickTarget target)
    {
        s.Hints.Clear();
        Assert.True(s.Alice.Session.Hover(target, s.W.Now), "the hover went out");
        s.W.Run(11); // 0.55 s, past PlaySession.RepeatHoverInterval with room for the clock's rounding: the next Look on the same thing goes out too
        return s.Hints.LastOrDefault(h => h.TargetKind == target.Kind && (target.Kind is TargetKind.Entity or TargetKind.Screen
            ? h.Target == target.Id : h.TurfX == target.X && h.TurfY == target.Y && h.TurfZ == target.Z));
    }

    private static void AssertEmpty(ContextHintsMessage hints)
    {
        Assert.NotNull(hints);
        Assert.Equal("", hints.Name);
        Assert.Empty(hints.Hints);
    }

    private static void AssertNamed(ContextHintsMessage hints, string name)
    {
        Assert.NotNull(hints);
        Assert.Equal(name, hints.Name);
    }

    private static bool Offers(ContextHintsMessage hints, string label) => hints.Hints.Exists(h => h.Label == label);

    [Fact]
    public void An_item_on_the_next_tile_is_named_with_its_hints_and_three_tiles_away_the_answer_is_empty()
    {
        using Scene s = Room(71);
        obj_item_pen near = Pen(s, 1, 0), far = Pen(s, 3, 0);
        s.W.Run(2);

        ContextHintsMessage nearHints = Look(s, On(near));
        AssertNamed(nearHints, "pen");
        Assert.True(Offers(nearHints, "Pull"), "the radial's Pull (Ctrl+click) on a thing next to her");

        ContextHintsMessage farHints = Look(s, On(far));
        AssertEmpty(farHints);
        Assert.Equal(TargetKind.Entity, farHints.TargetKind);
        Assert.Equal(0, s.W.Host.SessionOf(1).Strikes);
    }

    [Fact]
    public void Next_to_it_but_behind_a_window_or_round_a_wall_corner_the_answer_is_empty_and_over_a_table_it_has_hints()
    {
        using Scene s = Room(72);
        In(s.W, () =>
        {
            // east: a directional window on the next tile's west edge, between her and the pen there
            Dm.@new<obj_structure_window_spawner_directional_west>(s.At(1, 0));
            // north and west: walls, so the pen north-west is round a corner (tg's Adjacent needs one of the two tiles between open)
            Waiting.NoSleep(s.At(-1, 0).ChangeTurf(typeof(turf_closed_wall)));
            Waiting.NoSleep(s.At(0, 1).ChangeTurf(typeof(turf_closed_wall)));
            // south: a table; the pen south-east is reached over it (tg's LETPASSTHROW), the way past the window being shut
            Dm.@new<obj_structure_table>(s.At(0, -1));
        });
        obj_item_pen behindGlass = Pen(s, 1, 0), roundWalls = Pen(s, -1, 1), overTable = Pen(s, 1, -1), onTable = Pen(s, 0, -1);
        s.W.Run(Second);

        AssertEmpty(Look(s, On(behindGlass)));
        AssertEmpty(Look(s, On(roundWalls)));
        ContextHintsMessage table = Look(s, On(overTable));
        AssertNamed(table, "pen");
        Assert.NotEmpty(table.Hints);
        ContextHintsMessage tableTop = Look(s, On(onTable));
        AssertNamed(tableTop, "pen");
        Assert.NotEmpty(tableTop.Hints);
    }

    [Fact]
    public void A_spear_reaches_two_tiles_and_with_empty_hands_the_same_pen_is_out_of_reach()
    {
        using Scene s = Room(73);
        obj_item_pen pen = Pen(s, 2, 0);
        s.W.Run(2);
        AssertEmpty(Look(s, On(pen)));

        In(s.W, () => DevCommands.Give(s.AliceMob, "/obj/item/spear"));
        s.W.Run(Second);
        Assert.Equal(2f, In(s.W, () => Dm.num((s.AliceMob.get_active_held_item() as obj_item)?.reach)));
        AssertNamed(Look(s, On(pen)), "pen");
    }

    [Fact]
    public void Walking_up_to_the_thing_while_looking_at_it_brings_its_hints_with_the_repeated_hover_and_stepping_back_takes_them()
    {
        using Scene s = Room(74);
        obj_item_pen pen = Pen(s, 3, 0);
        s.W.Run(2);
        AssertEmpty(Look(s, On(pen)));

        // she moves next to it; the client keeps the same thing under the crosshair: the repeat sends the hover again
        In(s.W, () => Waiting.NoSleep(s.AliceMob.forceMove(s.At(2, 0))));
        s.W.Run(2);
        int sent = s.Alice.Session.HoversSent;
        AssertNamed(Look(s, On(pen)), "pen");
        Assert.Equal(sent + 1, s.Alice.Session.HoversSent);

        In(s.W, () => Waiting.NoSleep(s.AliceMob.forceMove(s.At(0, 0))));
        s.W.Run(2);
        AssertEmpty(Look(s, On(pen)));
    }

    [Fact]
    public void A_ghost_still_gets_the_name_of_a_thing_far_away()
    {
        using Scene s = Room(75);
        obj_item_pen pen = Pen(s, 4, 0);
        s.W.Run(2);
        AssertEmpty(Look(s, On(pen)));

        In(s.W, () => s.AliceMob.ghostize(Dm.TRUE));
        s.W.Run(Second);
        Assert.IsType<mob_dead_observer>(In(s.W, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == "Alice").mob));
        AssertNamed(Look(s, On(pen)), "pen");
    }

    [Fact]
    public void The_item_in_her_own_hand_is_named_as_a_screen_target()
    {
        using Scene s = Room(76);
        In(s.W, () => DevCommands.Give(s.AliceMob, "/obj/item/pen"));
        s.W.Run(Second);
        var held = In(s.W, () => s.AliceMob.get_active_held_item() as obj_item_pen);
        Assert.NotNull(held);
        ContextHintsMessage hints = Look(s, new ClickTarget(TargetKind.Screen, held.dm_id, 0, 0, 0, 16, 16));
        AssertNamed(hints, "pen");
        Assert.Equal(TargetKind.Screen, hints.TargetKind);
    }

    [Fact]
    public void Out_of_reach_the_zone_looked_at_on_a_person_is_still_selected()
    {
        using Scene s = Room(77);
        RealHeadlessWorld.Player bob = s.W.Join("Bob");
        var bobMob = (mob_living_carbon_human)s.W.MobOf(bob);
        In(s.W, () => Waiting.NoSleep(bobMob.forceMove(s.At(3, 0))));
        s.W.Run(Second);

        AssertEmpty(Look(s, On(bobMob, AimZone.LeftLeg)));
        Assert.Equal(BODY_ZONE_L_LEG, In(s.W, () => s.AliceMob.zone_selected));

        In(s.W, () => Waiting.NoSleep(bobMob.forceMove(s.At(1, 0))));
        s.W.Run(2);
        ContextHintsMessage near = Look(s, On(bobMob, AimZone.Head));
        Assert.Equal(BODY_ZONE_HEAD, In(s.W, () => s.AliceMob.zone_selected));
        Assert.False(string.IsNullOrEmpty(near.Name));
        Assert.Contains(near.Hints, h => h.Label == "Headpat");
    }

    [Fact]
    public void With_the_pet_key_held_one_hover_of_a_befriended_pet_opens_its_radial_once()
    {
        // tg's pet commands (obeys_commands.dm): while the key is held, MouseEntered on the pet opens its radial (7 s). Every hover runs tg's hover,
        // so the client sends no repeats while the key is held (PlaySessionTests); one hover opens one radial.
        using Scene s = Room(79);
        mob_living_basic_pet_dog_corgi corgi = null;
        In(s.W, () =>
        {
            foreach ((int dx, int dy) in new[] { (2, 0), (1, 1), (1, -1) }) // a nook, so he stays under the crosshair
                Waiting.NoSleep(s.At(dx, dy).ChangeTurf(typeof(turf_closed_wall)));
            corgi = Dm.@new<mob_living_basic_pet_dog_corgi>(s.At(1, 0));
        });
        s.W.Run(2);
        In(s.W, () => Waiting.NoSleep(corgi.befriend(s.AliceMob)));
        s.W.Run(Second);
        var opened = new HashSet<long>();
        var openTicks = new List<long>();
        s.Alice.Session.Replica.RadialChanged += m =>
        {
            if (m.Kind != RadialMenuKind.Close && opened.Add(m.MenuId))
                openTicks.Add(s.W.Host.Tick);
        };
        ClickTarget onCorgi = On(corgi), floor = new(TargetKind.Turf, 0, X - 1, Y, s.Z);

        ContextHintsMessage named = Look(s, onCorgi);
        Assert.Contains("corgi", named?.Name ?? ""); // (tg's name carries a text macro's mark)
        s.W.Run(2 * Second);
        Assert.Empty(opened); // the key is not held: nothing listens

        Look(s, floor);
        s.Alice.Session.Keybinding(KeybindingCode.ViewPetCommands);
        s.W.Run(2);
        Look(s, onCorgi);
        s.W.Run(10 * Second); // past the radial's 7 seconds, no other hover
        Assert.True(opened.Count == 1, "one hover with the key held, radials opened at ticks " + string.Join(", ", openTicks));
        s.Alice.Session.Keybinding(KeybindingCode.ViewPetCommands, up: true);
    }

    [Fact]
    public void A_repeated_hover_is_answered_with_fresh_radial_extras()
    {
        using Scene s = Room(80);
        obj_item_pen pen = Pen(s, 1, 0);
        s.W.Run(2);
        ContextHintsMessage first = Look(s, On(pen));
        AssertNamed(first, "pen");
        Assert.True(Offers(first, "Pull"));

        // a pull started: the repeat's answer has Stop pulling now (as D230's Rehover needs)
        In(s.W, () => Waiting.NoSleep(s.AliceMob.start_pulling(pen)));
        s.W.Run(2);
        ContextHintsMessage again = Look(s, On(pen));
        AssertNamed(again, "pen");
        Assert.True(Offers(again, "Stop pulling"), string.Join(", ", again.Hints.Select(h => h.Label)));

        // a ceiling answer in between, then the pen again: answered again
        Assert.NotNull(Look(s, new ClickTarget(TargetKind.Ceiling, 0, X + 1, Y, s.Z)));
        AssertNamed(Look(s, On(pen)), "pen");
    }

    [Theory]
    // (Strip is refused to all three; the cuffed cannot pull (blocked hands add TRAIT_PULL_BLOCKED) and a pAI's pull_force is 0, so they are offered
    // nothing; the locked cyborg is offered Pull)
    [InlineData("cuffed", "You are incapacitated at the moment!", "")] // tg's can_perform_action
    [InlineData("pai", "Your holochasis does not allow you to do this!", "")] // its override (pai.dm)
    [InlineData("borg", "You can't do that right now!", "Pull")] // its override in lockdown (robot.dm)
    public void Working_out_a_hovers_radial_extras_puts_no_line_in_the_players_chat_and_other_lines_still_arrive(string who, string refusal, string offered)
    {
        // the radial extras' Strip asks tg's can_perform_action (virtual: each mob type has its own refusal line); a hover must not talk
        using Scene s = Room(81);
        RealHeadlessWorld.Player bob = s.W.Join("Bob");
        var bobMob = (mob_living_carbon_human)s.W.MobOf(bob);
        In(s.W, () => Waiting.NoSleep(bobMob.forceMove(s.At(1, 0))));
        mob_living looker = s.AliceMob;
        switch (who)
        {
            case "cuffed":
                In(s.W, () => Waiting.NoSleep(s.AliceMob.equip_to_slot(Dm.@new<obj_item_restraints_handcuffs>(s.At(0, 0)), ITEM_SLOT_HANDCUFFED)));
                break;
            case "pai":
                In(s.W, () => DevCommands.Paiize(s.AliceMob));
                s.W.Run(Second);
                looker = s.W.MobOf(s.Alice);
                Assert.IsType<mob_living_silicon_pai>(looker);
                In(s.W, () =>
                {
                    var pai = (mob_living_silicon_pai)looker;
                    pai.holochassis_ready = Dm.TRUE; // a new pAI's emitters are still recycling
                    Assert.True(Dm.truthy(Waiting.NoSleep(pai.fold_out(Dm.TRUE))));
                });
                break;
            case "borg":
                looker = In(s.W, () => (mob_living_silicon_robot)Waiting.NoSleep(s.AliceMob.Robotize()));
                s.W.Run(2 * Second);
                In(s.W, () => Waiting.NoSleep(((mob_living_silicon_robot)looker).SetLockdown(Dm.TRUE)));
                break;
        }
        s.W.Run(Second);
        Assert.Same(looker, s.W.MobOf(s.Alice));
        Assert.IsAssignableFrom<turf>(In(s.W, () => looker.loc)); // on the floor next to Bob (a pAI unfolded into its holoform)

        // the premise: worked out unmuted, the extras talk (tg's refusal for this mob type, exactly one line) and offer what tg allows
        var texts = new List<string>();
        s.Alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        List<string> extras = In(s.W, () => RadialExtras.Of(looker, bobMob).Select(e => e.Label).ToList());
        s.W.Run(2);
        Assert.True(texts.Count == 1 && texts[0].Contains(refusal, StringComparison.Ordinal), "unmuted: " + string.Join(" | ", texts));
        Assert.True(extras.SequenceEqual(offered.Split(',', StringSplitOptions.RemoveEmptyEntries)), "offered unmuted: " + string.Join(",", extras));
        texts.Clear();
        const string Elsewhere = "A line from elsewhere.";
        int sent = 0;
        for (int i = 0; i < 4; i++)
        {
            s.Hints.Clear();
            Assert.True(s.Alice.Session.Hover(On(bobMob), s.W.Now));
            // in the same ticks, something else tells the player something
            s.W.Run(11, _ => { In(s.W, () => to_chat(looker, Elsewhere)); sent++; });
            ContextHintsMessage hints = s.Hints.LastOrDefault(h => h.Target == bobMob.dm_id);
            Assert.NotNull(hints);
            Assert.All(extras, label => Assert.Contains(hints.Hints, h => h.Label == label)); // the same entries as worked out unmuted
            Assert.DoesNotContain(hints.Hints, h => h.Label == "Strip"); // tg's own checks still refuse it
        }
        s.W.Run(2); // the last tick's line reaches the client
        Assert.Equal(Enumerable.Repeat(Elsewhere, sent), texts);
    }

    [Theory]
    [InlineData("cuffed")]
    [InlineData("paralyzed")]
    public void A_cuffed_or_paralyzed_player_still_sees_only_what_is_in_reach(string state)
    {
        // a state of the moment does not switch far names back on: the gate is where the mob stands, not what it can do now
        using Scene s = Room(83);
        obj_item_pen near = Pen(s, 1, 0), far = Pen(s, 4, 0);
        In(s.W, () =>
        {
            if (state == "cuffed")
                Waiting.NoSleep(s.AliceMob.equip_to_slot(Dm.@new<obj_item_restraints_handcuffs>(s.At(0, 0)), ITEM_SLOT_HANDCUFFED));
            else
                Waiting.NoSleep(s.AliceMob.Paralyze(60 * SECONDS));
        });
        s.W.Run(Second);
        Assert.True(In(s.W, () => state == "cuffed" ? s.AliceMob.handcuffed != null : Dm.truthy(s.AliceMob.IsParalyzed())));
        AssertNamed(Look(s, On(near)), "pen");
        AssertEmpty(Look(s, On(far)));
    }

    [Theory]
    [InlineData("mech")] // tg hands the pilot's click to the mech (COMSIG_MOB_CLICKON) and ClickOn stops at loc.AllowClick()
    [InlineData("locker")] // a closed locker does not allow the click either
    public void A_mob_inside_a_mech_or_a_locker_gets_tgs_screentip_as_before_near_and_far(string inside)
    {
        using Scene s = Room(82);
        obj_item_pen near = Pen(s, 1, 0), far = Pen(s, 4, 0);
        atom holder = In(s.W, () =>
        {
            if (inside == "mech")
            {
                var mech = Dm.@new<obj_vehicle_sealed_mecha_ripley>(s.At(0, 0));
                Waiting.NoSleep(mech.moved_inside(s.AliceMob));
                return (atom)mech;
            }
            var locker = Dm.@new<obj_structure_closet>(s.At(0, 0));
            Waiting.NoSleep(s.AliceMob.forceMove(locker));
            return locker;
        });
        s.W.Run(Second);
        Assert.Same(holder, In(s.W, () => s.AliceMob.loc));
        AssertNamed(Look(s, On(near)), "pen");
        AssertNamed(Look(s, On(far)), "pen");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // a gun's shot from afar shows nothing either
    public void The_ceiling_is_named_over_the_next_tile_and_not_three_tiles_away(bool gun)
    {
        using Scene s = Room(78);
        if (gun)
        {
            In(s.W, () => DevCommands.Give(s.AliceMob, "/obj/item/gun/ballistic/automatic/pistol"));
            s.W.Run(Second);
            Assert.IsAssignableFrom<obj_item_gun>(In(s.W, () => s.AliceMob.get_active_held_item()));
        }
        ContextHintsMessage near = Look(s, new ClickTarget(TargetKind.Ceiling, 0, X + 1, Y, s.Z));
        Assert.NotNull(near);
        Assert.StartsWith("Ceiling: ", near.Name);
        AssertEmpty(Look(s, new ClickTarget(TargetKind.Ceiling, 0, X + 3, Y, s.Z)));
    }
}
