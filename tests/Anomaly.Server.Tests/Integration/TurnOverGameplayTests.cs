using System.Net;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package T, slice T4c (DV588, protocol 95 also): the radial's Turn over, through a real server and two clients. The server offers it on a lying
/// carbon next to the player; the pick starts tg's do_after of 1.5 seconds with tg's start line, then the side flips, tg's end line is said and the
/// posture byte every client receives carries it; interrupted, nothing changes and nothing more is said; a crafted pick changes nothing; the side is
/// forgotten when the body gets up; a shot reads the side the clients draw; and a turn replays to the same world.
/// </summary>
public class TurnOverGameplayTests
{
    private const int Second = 20;
    private const string Label = "Turn over";

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

    private sealed record Pair(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, RealHeadlessWorld.Player Bob, mob_living_carbon_human AliceMob,
        mob_living_carbon_human BobMob, List<string> AliceTexts, List<string> BobTexts)
    {
        public string AliceName => In(W, () => AliceMob.name);
        public string BobName => In(W, () => BobMob.name);
        public float Side => In(W, () => BobMob.dm_turned_side);
        public int Starts => AliceTexts.Count(t => t.Contains("You start turning", StringComparison.Ordinal));
        public int Ends => AliceTexts.Count(t => t.Contains("You turn ", StringComparison.Ordinal));
    }

    // Alice and Bob on neighbouring tiles, Alice's hands empty; Bob lying down (resting) unless told otherwise.
    private static Pair Make(ulong seed, bool lie = true)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceTexts = new List<string>();
        var bobTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(WebUtility.HtmlDecode(t.Html));
        bob.Client.ServerTextReceived += t => bobTexts.Add(WebUtility.HtmlDecode(t.Html));
        In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
        });
        w.Run(Second);
        if (lie)
        {
            In(w, () => Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            Assert.Equal(LYING_DOWN, In(w, () => bobMob.body_position));
        }
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        return new Pair(w, alice, bob, aliceMob, bobMob, aliceTexts, bobTexts);
    }

    private static ClickTarget On(atom target) => new(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16, AimZone.None);

    private static byte PostureSeenBy(RealHeadlessWorld.Player viewer, atom target)
        => viewer.Session.Replica.Entities.Single(e => e.Id == target.dm_id).State.Posture;

    // The hints the server sends Alice for a fresh hover on the target: whether they offer Turn over (as the radial's Extra entry).
    private static bool Offered(RealHeadlessWorld w, RealHeadlessWorld.Player viewer, atom target)
    {
        var hints = new List<ContextHintsMessage>();
        void Take(ContextHintsMessage m) => hints.Add(m);
        viewer.Client.ContextHintsReceived += Take;
        try
        {
            w.Run(4);
            viewer.Session.Rehover();
            Assert.True(viewer.Session.Hover(On(target), w.Now), "the hover went out");
            w.Run(6);
            ContextHintsMessage last = hints.LastOrDefault(h => h.Target == target.dm_id);
            Assert.NotNull(last);
            return last.Hints.Any(h => h.Label == Label && h.Action == ContextHintAction.Extra);
        }
        finally
        {
            viewer.Client.ContextHintsReceived -= Take;
        }
    }

    private static void Pick(Pair p, atom target = null) => Assert.True(p.Alice.Session.ContextPick(On(target ?? p.BobMob), Label));

    [Fact]
    public void The_radial_offers_turn_over_on_an_adjacent_lying_carbon_only()
    {
        Pair p = Make(5881, lie: false);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            Assert.False(Offered(w, p.Alice, p.BobMob), "not on a standing person");
            In(w, () => Waiting.NoSleep(p.BobMob.set_resting(Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            Assert.True(Offered(w, p.Alice, p.BobMob), "on a lying person next to her");
            Assert.Contains((RadialExtra.TurnOver, Label), In(w, () => RadialExtras.Of(p.AliceMob, p.BobMob)));
            // not on herself, lying too
            In(w, () => Waiting.NoSleep(p.AliceMob.set_resting(Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            Assert.DoesNotContain(In(w, () => RadialExtras.Of(p.AliceMob, p.AliceMob)), e => e.Kind == RadialExtra.TurnOver);
            In(w, () => Waiting.NoSleep(p.AliceMob.set_resting(Dm.FALSE, Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            // not on a thing that is no carbon, lying where Bob lies (its lying picture has no front and back)
            var sloth = In(w, () => (mob_living)DevCommands.Spawn(typeof(mob_living_basic_sloth_citrus), get_turf(p.BobMob), 1, 2, out _).Single());
            In(w, () => sloth.body_position = LYING_DOWN);
            w.Run(4);
            Assert.False(Offered(w, p.Alice, sloth), "not on a sloth");
            // not from two tiles away
            In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                Waiting.NoSleep(p.BobMob.forceMove(locate(Dm.num(start.x) + 2, Dm.num(start.y), Dm.num(start.z))));
            });
            w.Run(Second);
            Assert.Equal(LYING_DOWN, In(w, () => p.BobMob.body_position));
            Assert.False(Offered(w, p.Alice, p.BobMob), "not from two tiles away");
        }
    }

    [Fact]
    public void A_pick_turns_the_body_after_the_do_after_and_every_client_sees_the_side()
    {
        Pair p = Make(5882);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            Assert.Null(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, p.BobMob)));
            Assert.True(Offered(w, p.Alice, p.BobMob));
            Pick(p);
            w.Run(4);
            // the start: tg's visible_message, the second person to Alice, the third to Bob; nothing turned yet
            Assert.Contains(p.AliceTexts, t => t.Contains($"You start turning {p.BobName} over.", StringComparison.Ordinal));
            Assert.Contains(p.BobTexts, t => t.Contains($"{p.AliceName} starts turning {p.BobName} over.", StringComparison.Ordinal));
            Assert.True(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "tg's do_after runs");
            w.Run(Second); // 1.2 seconds after the pick
            Assert.Equal(0f, p.Side);
            Assert.Equal(0, p.Ends);
            w.Run(Second);
            // a conscious body on the floor is drawn back up: its first turn puts it face up
            Assert.Equal(SOUTH, p.Side);
            Assert.Contains(p.AliceTexts, t => t.Contains($"You turn {p.BobName} over.", StringComparison.Ordinal));
            Assert.Contains(p.BobTexts, t => t.Contains($"{p.AliceName} turns {p.BobName} over.", StringComparison.Ordinal));
            Assert.Equal(1, p.Starts);
            Assert.Equal(1, p.Ends);
            Assert.True(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, p.BobMob)) == true, "Alice's client receives face up");
            Assert.True(PostureBits.TurnedFaceUp(PostureSeenBy(p.Bob, p.BobMob)) == true, "Bob's own client too");

            // turned again: back up
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(NORTH, p.Side);
            Assert.Equal(2, p.Ends);
            Assert.True(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, p.BobMob)) == false, "Alice's client receives back up");
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    [Fact]
    public void A_second_pick_during_the_first_does_nothing()
    {
        Pair p = Make(5883);
        using (p.W)
        {
            Pick(p);
            p.W.Run(4);
            Pick(p);
            p.W.Run(4);
            Pick(p);
            p.W.Run(2 * Second);
            Assert.Equal(1, p.Starts);
            Assert.Equal(1, p.Ends);
            Assert.Equal(SOUTH, p.Side); // one turn, not three
        }
    }

    [Theory]
    [InlineData("the actor walks away")]
    [InlineData("the target stands up")]
    [InlineData("the actor's hands are full")]
    [InlineData("the actor's hands are blocked")]
    [InlineData("the actor is stunned")]
    public void An_interrupted_turn_changes_nothing_and_says_nothing_more(string how)
    {
        Pair p = Make(5884);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            Pick(p);
            w.Run(Second / 2);
            Assert.Equal(1, p.Starts);
            In(w, () =>
            {
                switch (how)
                {
                    case "the actor walks away":
                        turf start = get_turf(p.AliceMob);
                        Waiting.NoSleep(p.AliceMob.forceMove(locate(Dm.num(start.x) - 2, Dm.num(start.y), Dm.num(start.z))));
                        break;
                    case "the target stands up":
                        Waiting.NoSleep(p.BobMob.set_resting(Dm.FALSE, Dm.TRUE, Dm.TRUE));
                        break;
                    case "the actor's hands are full":
                        DevCommands.Give(p.AliceMob, "/obj/item/crowbar");
                        DevCommands.Give(p.AliceMob, "/obj/item/screwdriver");
                        break;
                    case "the actor's hands are blocked":
                        ADD_TRAIT(p.AliceMob, TRAIT_HANDS_BLOCKED, "turn over test");
                        break;
                    default:
                        Waiting.NoSleep(p.AliceMob.Stun(10 * SECONDS));
                        break;
                }
            });
            w.Run(2 * Second);
            if (how == "the actor's hands are full")
                Assert.Empty(In(w, () => Dm.each<object>(p.AliceMob.get_empty_held_indexes() as DmList ?? Dm.list()).ToList()));
            if (how == "the target stands up")
                Assert.NotEqual(LYING_DOWN, In(w, () => p.BobMob.body_position));
            Assert.Equal(0f, p.Side);
            Assert.Equal(0, p.Ends);
            Assert.DoesNotContain(p.BobTexts, t => t.Contains("turns", StringComparison.Ordinal) && t.Contains("over.", StringComparison.Ordinal));
            Assert.Null(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, p.BobMob)));
            Assert.False(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "the do_after ended");
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    /// <summary>
    /// The free hand at the pick: the offer does not ask for one (the spec's offer is a lying carbon next to the actor), the pick does
    /// (<c>dm_has_free_hand</c>): an actor whose two hands are full is offered Turn over and refused at the pick, silently, with no do_after.
    /// </summary>
    [Fact]
    public void An_actor_with_both_hands_full_is_offered_turn_over_and_refused_at_the_pick()
    {
        Pair p = Make(5891);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            In(w, () =>
            {
                DevCommands.Give(p.AliceMob, "/obj/item/crowbar");
                DevCommands.Give(p.AliceMob, "/obj/item/screwdriver");
            });
            w.Run(4);
            Assert.Empty(In(w, () => Dm.each<object>(p.AliceMob.get_empty_held_indexes() as DmList ?? Dm.list()).ToList()));
            Assert.True(Offered(w, p.Alice, p.BobMob), "offered: the hands are checked at the pick");
            Pick(p);
            w.Run(2);
            Assert.False(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "no do_after");
            w.Run(2 * Second);
            Assert.Equal((0, 0, 0f), (p.Starts, p.Ends, p.Side));
            Assert.DoesNotContain(p.BobTexts, t => t.Contains("turning", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// The per-tick check alone: Alice starts with a crowbar in her active hand and her other hand empty; mid-turn the empty hand is filled
    /// directly, her active hand's item unchanged, so tg's own do_after check (the active item) does not stop it: <c>turn_over_checks</c> does, at
    /// once, long before the 1.5 seconds would end.
    /// </summary>
    [Fact]
    public void Losing_the_last_empty_hand_mid_turn_stops_it_at_once()
    {
        Pair p = Make(5892);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            obj_item crowbar = In(w, () =>
            {
                DevCommands.Give(p.AliceMob, "/obj/item/crowbar");
                return (obj_item)p.AliceMob.get_active_held_item();
            });
            Assert.NotNull(crowbar);
            Assert.Null(In(w, () => p.AliceMob.get_inactive_held_item()));
            Pick(p);
            w.Run(Second / 2);
            Assert.Equal(1, p.Starts);
            Assert.True(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))));
            In(w, () =>
            {
                var screwdriver = Dm.@new<obj_item_screwdriver>(get_turf(p.AliceMob));
                Assert.True(Dm.truthy(Waiting.NoSleep(p.AliceMob.put_in_hand(screwdriver, p.AliceMob.get_inactive_hand_index()))));
            });
            Assert.Same(crowbar, In(w, () => p.AliceMob.get_active_held_item()));
            Assert.Empty(In(w, () => Dm.each<object>(p.AliceMob.get_empty_held_indexes() as DmList ?? Dm.list()).ToList()));
            w.Run(2); // 0.6 seconds after the pick: the do_after would run 0.9 seconds more
            Assert.False(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "the per-tick check ended the do_after");
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Ends, p.Side));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    // tg's drop_limb of the arm of Alice's active or inactive hand (the slot stays in held_items, empty, with no hand behind it)
    private static void DropArm(Pair p, bool active)
    {
        In(p.W, () =>
        {
            bool leftActive = Dm.num(p.AliceMob.active_hand_index) == 1;
            string zone = leftActive == active ? BODY_ZONE_L_ARM : BODY_ZONE_R_ARM;
            Waiting.NoSleep(((obj_item_bodypart)p.AliceMob.get_bodypart(zone)).drop_limb());
            Assert.Null(p.AliceMob.get_bodypart(zone));
        });
    }

    private static bool FreeHand(Pair p) => In(p.W, () => mob_living.dm_has_free_hand(p.AliceMob));

    // A pick refused before the do_after: no start message, no do_after, no flip.
    private static void AssertRefused(Pair p)
    {
        Pick(p);
        p.W.Run(2);
        Assert.False(In(p.W, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "no do_after");
        p.W.Run(2 * Second);
        Assert.Equal((0, 0, 0f), (p.Starts, p.Ends, p.Side));
    }

    /// <summary>A one-armed actor whose only hand is full is refused at the pick: the lost arm's empty slot is no free hand (has_hand_for_held_index).</summary>
    [Fact]
    public void A_one_armed_actor_whose_hand_is_full_is_refused()
    {
        Pair p = Make(5894);
        using (p.W)
        {
            DropArm(p, active: false);
            In(p.W, () => DevCommands.Give(p.AliceMob, "/obj/item/crowbar"));
            Assert.IsType<obj_item_crowbar>(In(p.W, () => p.AliceMob.get_active_held_item()));
            Assert.NotEmpty(In(p.W, () => Dm.each<object>(p.AliceMob.get_empty_held_indexes() as DmList ?? Dm.list()).ToList())); // the lost arm's slot
            Assert.False(FreeHand(p));
            AssertRefused(p);
        }
    }

    /// <summary>A one-armed actor whose only hand is empty turns the body over.</summary>
    [Fact]
    public void A_one_armed_actor_with_an_empty_hand_turns_the_body_over()
    {
        Pair p = Make(5895);
        using (p.W)
        {
            DropArm(p, active: false);
            Assert.True(FreeHand(p));
            Pick(p);
            p.W.Run(2 * Second);
            Assert.Equal((1, 1, SOUTH), (p.Starts, p.Ends, p.Side));
        }
    }

    /// <summary>
    /// Alice holds a crowbar in her active hand; mid-turn she loses the arm of her only empty hand, the active hand's item unchanged (tg's own
    /// do_after check passes): the per-tick turn_over_checks stops the turn at once.
    /// </summary>
    [Fact]
    public void Losing_the_arm_of_the_only_empty_hand_mid_turn_stops_it_at_once()
    {
        Pair p = Make(5896);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            obj_item crowbar = In(w, () =>
            {
                DevCommands.Give(p.AliceMob, "/obj/item/crowbar");
                return (obj_item)p.AliceMob.get_active_held_item();
            });
            Assert.NotNull(crowbar);
            Pick(p);
            w.Run(Second / 2);
            Assert.Equal(1, p.Starts);
            Assert.True(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))));
            DropArm(p, active: false);
            Assert.Same(crowbar, In(w, () => p.AliceMob.get_active_held_item()));
            Assert.False(FreeHand(p));
            w.Run(2); // 0.6 seconds after the pick: the do_after would run 0.9 seconds more
            Assert.False(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, p.BobMob))), "the per-tick check ended the do_after");
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Ends, p.Side));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    /// <summary>Both arms lost: refused at the pick (turn_over_checks' free hand, which runs before tg's can_perform_action).</summary>
    [Fact]
    public void An_actor_with_no_arms_is_refused()
    {
        Pair p = Make(5897);
        using (p.W)
        {
            DropArm(p, active: false);
            DropArm(p, active: true);
            Assert.False(FreeHand(p));
            AssertRefused(p);
        }
    }

    [Fact]
    public void The_side_is_forgotten_when_the_body_gets_up()
    {
        Pair p = Make(5885);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(SOUTH, p.Side);
            In(w, () => Waiting.NoSleep(p.BobMob.set_resting(Dm.FALSE, Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            Assert.Equal(STANDING_UP, In(w, () => p.BobMob.body_position));
            Assert.Equal(0f, p.Side);
            In(w, () => Waiting.NoSleep(p.BobMob.set_resting(Dm.TRUE, Dm.TRUE)));
            w.Run(Second);
            Assert.Equal(LYING_DOWN, In(w, () => p.BobMob.body_position));
            Assert.Equal(0f, p.Side);
            byte posture = PostureSeenBy(p.Alice, p.BobMob);
            Assert.True(PostureBits.IsLying(posture));
            Assert.Null(PostureBits.TurnedFaceUp(posture)); // the default rule applies again: back up, conscious on the floor
            Assert.Equal(ByondDir.North, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 15, 4, PostureBits.IsLimp(posture), PostureBits.IsBuckled(posture), PostureBits.TurnedFaceUp(posture)));
        }
    }

    [Fact]
    public void A_body_on_furniture_turns_face_down_first_and_one_without_a_player_is_sent_too()
    {
        Pair p = Make(5886, lie: false);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            // a crewman with no player (no capsule: the replicator is not polling his posture) on an operating table next to Alice
            mob_living_carbon_human patient = In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                turf there = locate(Dm.num(start.x), Dm.num(start.y) + 1, Dm.num(start.z));
                var table = (atom_movable)DevCommands.Spawn(typeof(obj_structure_table_optable), there, 1, 2, out _).Single();
                var made = (mob_living_carbon_human)DevCommands.Spawn(typeof(mob_living_carbon_human), there, 1, 2, out _).Single();
                Waiting.NoSleep(table.buckle_mob(made, force: Dm.TRUE));
                return made;
            });
            w.Run(Second);
            Assert.Equal(LYING_DOWN, In(w, () => patient.body_position));
            Assert.True(In(w, () => patient.dm_lies_buckled));
            byte posture = PostureSeenBy(p.Alice, patient);
            Assert.True(PostureBits.IsLying(posture) && PostureBits.IsBuckled(posture));
            Assert.Null(PostureBits.TurnedFaceUp(posture));
            Assert.True(Offered(w, p.Alice, patient));
            Pick(p, patient);
            w.Run(2 * Second);
            Assert.Equal(NORTH, In(w, () => patient.dm_turned_side)); // drawn face up on the table: the first turn puts it face down
            Assert.True(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, patient)) == false, "the posture of a mob with no capsule is sent again");
            Pick(p, patient);
            w.Run(2 * Second);
            Assert.Equal(SOUTH, In(w, () => patient.dm_turned_side));
            Assert.True(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, patient)) == true);
        }
    }

    [Fact]
    public void A_limp_body_turns_face_up_first_then_face_down()
    {
        Pair p = Make(5887);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            // limp (tg's stat not CONSCIOUS: here dead), still a lying carbon: the actor's checks are the actor's
            In(w, () => Waiting.NoSleep(p.BobMob.death()));
            w.Run(Second);
            Assert.True(PostureBits.IsLimp(PostureSeenBy(p.Alice, p.BobMob)), "Alice's client sees the body limp");
            Assert.Null(PostureBits.TurnedFaceUp(PostureSeenBy(p.Alice, p.BobMob)));
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(SOUTH, p.Side);
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(NORTH, p.Side);
        }
    }

    [Fact]
    public void The_target_deleted_during_the_do_after_throws_nothing()
    {
        Pair p = Make(5888, lie: false);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            mob_living_carbon_human npc = In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                var made = (mob_living_carbon_human)DevCommands.Spawn(typeof(mob_living_carbon_human), locate(Dm.num(start.x), Dm.num(start.y) - 1, Dm.num(start.z)), 1, 2, out _).Single();
                Waiting.NoSleep(made.set_resting(Dm.TRUE, Dm.TRUE));
                return made;
            });
            w.Run(Second);
            int runtimes = w.World.RuntimeErrorCount;
            Pick(p, npc);
            w.Run(Second / 2);
            Assert.Equal(1, p.Starts);
            In(w, () => qdel(npc));
            w.Run(2 * Second);
            Assert.Equal(0, p.Ends);
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
            Assert.False(In(w, () => Dm.truthy(DOING_INTERACTION_WITH_TARGET(p.AliceMob, npc))), "the do_after ended");
        }
    }

    [Fact]
    public void A_crafted_pick_is_refused_or_ignored_without_a_runtime()
    {
        Pair p = Make(5889, lie: false);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            int runtimes = w.World.RuntimeErrorCount;
            var alice = w.Host.Sessions.Single(s => s.Key == "Alice");

            // a target that is not lying
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Starts, p.Side));

            // a target id that does not exist: refused as a click's
            int rejected = alice.ClicksRejected;
            Assert.True(p.Alice.Session.ContextPick(new ClickTarget(TargetKind.Entity, 999_999, 0, 0, 0, 16, 16, AimZone.None), Label));
            w.Run(4);
            Assert.Equal(rejected + 1, alice.ClicksRejected);

            // not adjacent: two tiles away
            In(w, () => Waiting.NoSleep(p.BobMob.set_resting(Dm.TRUE, Dm.TRUE)));
            In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                Waiting.NoSleep(p.BobMob.forceMove(locate(Dm.num(start.x) + 2, Dm.num(start.y), Dm.num(start.z))));
            });
            w.Run(Second);
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Starts, p.Side));

            // next to her again, but Alice is dead
            In(w, () =>
            {
                turf start = get_turf(p.AliceMob);
                Waiting.NoSleep(p.BobMob.forceMove(locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
                Waiting.NoSleep(p.AliceMob.death());
            });
            w.Run(Second);
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Starts, p.Side));

            // and a ghost
            In(w, () => p.AliceMob.ghostize(Dm.TRUE));
            w.Run(Second);
            Assert.IsType<mob_dead_observer>(In(w, () => alice.Client.mob));
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal((0, 0f), (p.Starts, p.Side));
            Assert.Null(PostureBits.TurnedFaceUp(PostureSeenBy(p.Bob, p.BobMob)));
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    [Fact]
    public void A_shot_reads_the_side_the_clients_draw()
    {
        Pair p = Make(5890);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            // before the turn: a conscious body on the floor is drawn back up from every side, by the clients and the server's shots alike
            AssertFrames(w, p, ByondDir.North);
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(SOUTH, p.Side);
            AssertFrames(w, p, ByondDir.South);
            // the eyes of the face-up frame are on the head the shot's mask reads (tg's doll's eye pixel, 16 by 26)
            Assert.Equal(BODY_ZONE_HEAD, In(w, () => ShotGeometry.ZoneAt(p.BobMob, ByondDir.South, 16, 26)));
        }
    }

    /// <summary>
    /// A real shot (the dev hook's hitscan laser along Alice's camera aim, as AimedShotGameplayTests) at Bob lying four tiles east, before and after
    /// the turn, at the same pixel of the picture her client draws: a pixel that is one arm on the back-up frame and the other arm on the face-up
    /// frame (the arms swap sides). Each time the arm that burns is the one her client's zone map names there for the side it draws, so a server that
    /// read the other side would burn the other arm.
    /// </summary>
    [Fact]
    public void A_real_shot_at_the_same_pixel_burns_the_arm_the_client_draws_there_before_and_after_the_turn()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        Pair p = Make(5893);
        using (p.W)
        {
            RealHeadlessWorld w = p.W;
            p.Alice.Session.DevCommand("shoot me /obj/projectile/beam/emitter/hitscan");
            MoveBob(w, p, 4);
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var tables = new ReplicaTables(p.Alice.Session.Replica);
            ReplicaEntity seen = p.Alice.Session.Replica.Entities.Single(e => e.Id == p.BobMob.dm_id);
            Func<byte, int, int, AimZone> zone = (dir, x, y) => BodyZoneMap.LimbOf(BodyZoneAim.ZoneAtPixel(tables, compositor, seen.State.Appearance, dir, x, y));
            // a pixel deep inside an arm on both frames (its 3 x 3 square one zone), a different arm on each
            bool Arm(AimZone z) => z is AimZone.LeftArm or AimZone.RightArm;
            bool Solid(byte dir, int x, int y) => Enumerable.Range(-1, 3).All(dx => Enumerable.Range(-1, 3).All(dy => zone(dir, x + dx, y + dy) == zone(dir, x, y)));
            (int px, int py) = (from y in Enumerable.Range(-8, 48) from x in Enumerable.Range(-8, 48)
                                where Arm(zone(ByondDir.North, x, y)) && Arm(zone(ByondDir.South, x, y)) && zone(ByondDir.North, x, y) != zone(ByondDir.South, x, y)
                                    && Solid(ByondDir.North, x, y) && Solid(ByondDir.South, x, y)
                                orderby y descending, Math.Abs(x - 16.5) // the upper arm, well over the floor
                                select (x, y)).First();
            AimZone back = zone(ByondDir.North, px, py), front = zone(ByondDir.South, px, py);
            Assert.NotEqual(back, front);

            Assert.Equal(back, ShootAt(w, p, tables, zone, px, py, expectedDir: ByondDir.North));

            MoveBob(w, p, 1);
            Pick(p);
            w.Run(2 * Second);
            Assert.Equal(SOUTH, p.Side);
            MoveBob(w, p, 4);
            Assert.Equal(front, ShootAt(w, p, tables, zone, px, py, expectedDir: ByondDir.South));
        }
    }

    private static void MoveBob(RealHeadlessWorld w, Pair p, int east)
    {
        In(w, () =>
        {
            turf start = get_turf(p.AliceMob);
            Waiting.NoSleep(p.BobMob.forceMove(locate(Dm.num(start.x) + east, Dm.num(start.y), Dm.num(start.z))));
        });
        w.Run(10);
        Assert.Equal(LYING_DOWN, In(w, () => p.BobMob.body_position));
    }

    // Alice fires at the picture pixel (px, py) of the frame her client draws for Bob now (checked to be expectedDir), aiming her camera at that pixel
    // where her client draws it: the billboard at Bob's drawn position plus his root appearance's pixel_x and pixel_y as a ground offset at the mob
    // scale (EntityLayer; tg's lying pixel_y shifts him), facing back along the shot, its lowest drawn body row on the floor, 0.05 m an icon pixel.
    // Returns the one part that burned, after checking it is the arm the client's zone map names at the pixel.
    private static AimZone ShootAt(RealHeadlessWorld w, Pair p, ReplicaTables tables, Func<byte, int, int, AimZone> zone, int px, int py, byte expectedDir)
    {
        ReplicaEntity seen = p.Alice.Session.Replica.Entities.Single(e => e.Id == p.BobMob.dm_id);
        Assert.True(p.Alice.Session.Poses.TryGetPose(seen, out RenderPose drawn));
        Assert.True(tables.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        double bx = drawn.X + look.PixelX * ShotGeometry.MobScale / 32.0, by = drawn.Y + look.PixelY * ShotGeometry.MobScale / 32.0;
        (float ax, float ay, int az) = p.Alice.Session.Prediction.RenderPosition();
        byte posture = seen.State.Posture;
        byte dir = BillboardMath.ChooseLyingDir(seen.State.Yaw, bx, by, ax, ay, 4, PostureBits.IsLimp(posture), PostureBits.IsBuckled(posture), PostureBits.TurnedFaceUp(posture));
        Assert.Equal(expectedDir, dir);
        AimZone named = zone(dir, px, py);
        int lowest = (from y in Enumerable.Range(-8, 48) from x in Enumerable.Range(-8, 48) where zone(dir, x, y) != AimZone.None select y).Min();
        double right = (px - 16.5) * 0.05, up = (py - lowest + 0.5) * 0.05; // metres; shooting east, the picture's right is south
        double eastE = (ax - 0.5) * 2, northE = (ay - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (bx - 0.5) * 2 - eastE, dy = (by - 0.5) * 2 - right - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        ShotAim aim = PlaySession.AimOf(new PickRay(eastE, northE, upE, az, dx / length, dy / length, dz / length, 10));
        string[] all = { BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG };
        Dictionary<string, float> before = all.ToDictionary(z => z, z => In(w, () => Dm.num(((obj_item_bodypart)p.BobMob.get_bodypart(z)).burn_dam)));
        Assert.True(p.Alice.Session.Click(new ClickTarget(TargetKind.Entity, p.BobMob.dm_id, 0, 0, 0, (byte)px, (byte)py, AimZone.None), ClickButton.Left, ClickModifiers.None, aim));
        w.Run(10);
        string burned = Assert.Single(all, z => In(w, () => Dm.num(((obj_item_bodypart)p.BobMob.get_bodypart(z)).burn_dam)) > before[z]);
        AimZone hit = burned == BODY_ZONE_L_ARM ? AimZone.LeftArm : burned == BODY_ZONE_R_ARM ? AimZone.RightArm : AimZone.None;
        Assert.True(named == hit, $"frame {dir} pixel {px},{py}: the client names {named}, the shot burned {burned}");
        return hit;
    }

    private sealed class ReplicaTables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // The frame the server's shot takes for viewers on Bob's four sides is the one Alice's client draws from her replica of him.
    private static void AssertFrames(RealHeadlessWorld w, Pair p, byte expected)
    {
        byte posture = PostureSeenBy(p.Alice, p.BobMob);
        foreach ((double dx, double dy) in new[] { (0.0, 5.0), (0.0, -5.0), (5.0, 0.0), (-5.0, 0.0) })
        {
            byte server = In(w, () => ShotGeometry.LyingFrame(p.BobMob, 0, 10, 10, 10 + dx, 10 + dy));
            byte client = BillboardMath.ChooseLyingDir(0, 10, 10, 10 + dx, 10 + dy, 4, PostureBits.IsLimp(posture), PostureBits.IsBuckled(posture), PostureBits.TurnedFaceUp(posture));
            Assert.Equal(expected, server);
            Assert.Equal(server, client);
        }
    }

    [Fact]
    public void A_turn_is_recorded_and_replayed_to_the_same_hash_and_side()
    {
        CommandLog log;
        ulong hash;
        long end;
        using (var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 400 }))
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            RealHeadlessWorld.Player bob = w.Join("Bob");
            mob_living bobMob = w.MobOf(bob);
            w.Run(10);
            // side by side and Bob knocked down, by console commands (everything that changes the world is a command, so the replay does it too)
            w.Host.EnqueueConsoleLine("teleport Alice 6 6");
            w.Host.EnqueueConsoleLine("teleport Bob 7 6");
            w.Run(10);
            w.Host.EnqueueConsoleLine("knockdown Bob 30");
            w.Run(10);
            Assert.Equal(LYING_DOWN, In(w, () => bobMob.body_position));
            Assert.True(alice.Session.ContextPick(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16, AimZone.None), Label));
            w.Run(2 * Second);
            Assert.Equal(SOUTH, In(w, () => bobMob.dm_turned_side));
            w.Host.RequestStop();
            for (int i = 0; i < 10 && !w.Host.Finished; i++)
                w.Run(1);
            Assert.True(w.Host.Finished);
            (hash, end) = (w.Host.FinalHash, w.Host.Tick);
            log = CommandLog.Parse(w.Host.Log.ToText(), out string error);
            Assert.Null(error);
        }
        Assert.Contains(log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.ContextPick);

        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths); // the live world's table (RealHeadlessWorld)
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(hash, result.FinalHash);
        using (replay.World.Enter())
            Assert.Equal(SOUTH, (replay.World.Objects.TryGet(replay.AvatarOf("Bob"), out object o) ? o as mob_living : null)?.dm_turned_side);
    }
}
