using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10 slice E (D38 stage 1, multiplayer.md section 8) through the real server on MetaStation over the loopback at 100 ms each way and 2% loss:
/// what the player does with the hands shows at once, before the server has even received it, and the server's answer agrees (picking up,
/// swapping hands by key and by the hand's box, throw mode, dropping, putting a belt on and taking it off); a refused pick-up comes back; an
/// airlock the player may open starts opening at the bump and lets the own capsule through without a correction, a locked-out one is never
/// guessed, and an electrified one (which tg does not open, and the client cannot know) closes again.
/// </summary>
public class PredictionGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static LoopbackOptions Lossy(int seed) => new() { Seed = (ulong)seed, TimeoutSteps = 40000, MinLatencySteps = 2, MaxLatencySteps = 2, LossRate = 0.02, RetransmitSteps = 4 };

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // The hand box the replica shows active (its "lhandactive"/"rhandactive" overlay): its held_index.
    private static int ActiveHand(ReplicaWorld r)
    {
        foreach (ScreenEntry e in r.Screen.Values)
        {
            if (e.HandIndex == 0 || !r.TryGetAppearance(e.Appearance, out AppearanceEntry a) || a.OverlayCount == 0)
                continue;
            foreach (TableRef o in a.Overlays)
                if (r.TryGetAppearance(o, out AppearanceEntry oa) && (r.ResolveString(oa.IconState) ?? "").EndsWith("handactive", StringComparison.Ordinal))
                    return e.HandIndex;
        }
        return 0;
    }

    private static ScreenEntry Hand(ReplicaWorld r, int index) => r.Screen.Values.First(e => e.HandIndex == index);

    // The item a box shows (tg's vis_contents of the slot), 0 for none.
    private static long ItemIn(ScreenEntry box) => box.VisChildren?.Items.FirstOrDefault(c => c.IsItem).Id ?? 0;

    private static string ThrowButton(ReplicaWorld r)
        => r.Screen.Values.Select(e => r.TryGetAppearance(e.Appearance, out AppearanceEntry a) ? r.ResolveString(a.IconState) : null)
            .FirstOrDefault(s => s is "act_throw_on" or "act_throw");

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    [Fact]
    public void The_hands_show_what_the_player_does_at_once_and_the_server_agrees()
    {
        using var w = new RealHeadlessWorld(Lossy(43), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        var me = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld sees = alice.Session.Replica;
        OwnGuesses guesses = alice.Session.Guesses;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // a wrench on the floor beside her, picked up: out of the world and in her active hand at once, the server's own a round trip later
        turf spot = In(w, () => new float[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(me, d)).First(Clear));
        obj_item_wrench wrench = In(w, () => Dm.@new<obj_item_wrench>(spot));
        w.Run(Second);
        Assert.True(sees.TryGet(wrench.dm_id, out _));
        int first = ActiveHand(sees);
        Assert.Equal((int)me.active_hand_index, first);
        Assert.True(alice.Session.Click(Entity(wrench.dm_id), ClickButton.Left, ClickModifiers.None));
        Assert.False(sees.TryGet(wrench.dm_id, out _));
        Assert.Equal(wrench.dm_id, ItemIn(Hand(sees, first)));
        Assert.Null(In(w, () => me.get_active_held_item()));
        w.Run(Second);
        Assert.Same(wrench, In(w, () => me.get_active_held_item()));
        Assert.Equal(wrench.dm_id, ItemIn(Hand(sees, first)));
        Assert.False(sees.TryGet(wrench.dm_id, out _));

        // X swaps to the other hand at once; a click on the first hand's box swaps back
        alice.Session.Keybinding(KeybindingCode.SwapHands);
        int second = ActiveHand(sees);
        Assert.NotEqual(first, second);
        Assert.Equal(first, (int)me.active_hand_index);
        w.Run(Second);
        Assert.Equal(second, (int)me.active_hand_index);
        Assert.Equal(second, ActiveHand(sees));
        Assert.True(alice.Session.Click(Screen(Hand(sees, first).Id), ClickButton.Left, ClickModifiers.None));
        Assert.Equal(first, ActiveHand(sees));
        w.Run(Second);
        Assert.Equal(first, (int)me.active_hand_index);

        // R: throw mode's button
        string off = ThrowButton(sees);
        Assert.Equal("act_throw", off);
        alice.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        Assert.Equal("act_throw_on", ThrowButton(sees));
        w.Run(Second);
        Assert.True(Dm.truthy(me.throw_mode));
        Assert.Equal("act_throw_on", ThrowButton(sees));
        alice.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        Assert.Equal("act_throw", ThrowButton(sees));
        w.Run(Second);

        // the wrench goes on the belt through the belt's box (tg's tools fit a belt; she wears a jumpsuit) and comes back by a click on it
        output.WriteLine("she wears on the belt: " + In(w, () => me.belt?.GetType().Name ?? "nothing"));
        Do(w, () => { if (me.belt != null) qdel(me.belt); }); // a free belt slot (an assistant's PDA is there)
        w.Run(Second);
        ScreenEntry box = sees.Screen.Values.First(e => e.SlotId == (uint)ITEM_SLOT_BELT);
        ScreenChild held = Hand(sees, first).VisChildren.Items.Single(c => c.Id == wrench.dm_id);
        Assert.True(held.IsItem && (held.ItemSlots & (uint)ITEM_SLOT_BELT) != 0 && !held.EquipDelay && !held.HasStorage);
        Assert.True(alice.Session.Click(Screen(box.Id), ClickButton.Left, ClickModifiers.None));
        Assert.Equal(wrench.dm_id, ItemIn(sees.Screen[box.Id]));
        Assert.Equal(0, ItemIn(Hand(sees, first)));
        w.Run(Second);
        Assert.Same(wrench, In(w, () => me.belt));
        Assert.Equal(wrench.dm_id, ItemIn(sees.Screen[box.Id]));
        Assert.True(alice.Session.Click(Screen(wrench.dm_id), ClickButton.Left, ClickModifiers.None)); // a click on the wrench's pixels in its box
        Assert.Equal(wrench.dm_id, ItemIn(Hand(sees, first)));
        Assert.Equal(0, ItemIn(sees.Screen[box.Id]));
        w.Run(Second);
        Assert.Null(In(w, () => me.belt));
        Assert.Same(wrench, In(w, () => me.get_active_held_item()));

        // Q drops it: the hand empties at once; it lands in the world when the server says so
        alice.Session.Keybinding(KeybindingCode.DropItem);
        Assert.Equal(0, ItemIn(Hand(sees, first)));
        w.Run(Second);
        Assert.True(In(w, () => wrench.loc is turf));
        Assert.True(sees.TryGet(wrench.dm_id, out _));

        // a worn toolbelt is a storage: a hand on it opens it in tg, so nothing is guessed
        obj_item_storage_belt_utility belt = In(w, () => Dm.@new<obj_item_storage_belt_utility>(me.loc));
        Do(w, () => Waiting.NoSleep(me.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT)));
        w.Run(Second);
        Assert.True(sees.Screen[box.Id].VisChildren.Items.Single(c => c.Id == belt.dm_id).HasStorage);
        int made = guesses.Made;
        Assert.True(alice.Session.Click(Screen(belt.dm_id), ClickButton.Left, ClickModifiers.None));
        Assert.Equal(made, guesses.Made);
        Assert.Equal(belt.dm_id, ItemIn(sees.Screen[box.Id]));
        w.Run(Second);
        Assert.Same(belt, In(w, () => me.belt));
        Assert.Equal(0, guesses.Refused);

        // an anchored wrench: the client guesses the pick-up, tg's attack_hand refuses it, and it comes back
        obj_item_wrench bolted = In(w, () => Dm.@new<obj_item_wrench>(spot));
        Do(w, () => bolted.anchored = TRUE);
        w.Run(Second);
        Assert.True(alice.Session.Click(Entity(bolted.dm_id), ClickButton.Left, ClickModifiers.None));
        Assert.False(sees.TryGet(bolted.dm_id, out _));
        w.Run(Second);
        Assert.True(sees.TryGet(bolted.dm_id, out _));
        Assert.Equal(0, ItemIn(Hand(sees, first)));
        Assert.Equal(1, guesses.Refused);
        Assert.Equal(0, guesses.Pending);
        Assert.False(sees.HasGuesses);

        // stunned (the owner, 2026-11-18: the item flashed into the hand and back): tg's ClickOn refuses the click, so nothing is guessed
        Do(w, () => bolted.anchored = FALSE);
        Do(w, () => Waiting.NoSleep(me.Stun(5 * SECONDS)));
        w.Run(Second);
        Assert.True(sees.Own.HandsBlocked);
        made = guesses.Made;
        Assert.True(alice.Session.Click(Entity(bolted.dm_id), ClickButton.Left, ClickModifiers.None));
        Assert.Equal(made, guesses.Made);
        Assert.True(sees.TryGet(bolted.dm_id, out _));
        w.Run(Second);
        Assert.True(In(w, () => bolted.loc is turf));
        Assert.Equal(0, ItemIn(Hand(sees, first)));
        Do(w, () => Waiting.NoSleep(me.SetStun(0)));
        w.Run(Second);
        Assert.False(sees.Own.HandsBlocked);
        output.WriteLine($"guesses {guesses.Made}, refused {guesses.Refused}; corrections {alice.Counters.Corrections}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // A plain (not external) airlock in a wall running east-west, with two clear floor tiles south of it and one north; null when none fits.
    private static (obj_machinery_door_airlock Door, turf Start, turf Beyond) Doorway(RealHeadlessWorld w, Func<DoorState, bool> wanted, obj_machinery_door_airlock except = null)
        => In(w, () => Dm.each<obj_machinery_door_airlock>(world.contents)
            // the station's airlocks: CentCom's have no APC, so an electrified one shocks nobody (electrocute_mob's area source); and
            // world.contents' order shifts with what boot makes and deletes
            .Where(d => d.loc is turf && d != except && Dm.truthy(is_station_level(d.z)) && d is not obj_machinery_door_airlock_external && !Dm.truthy(d.multi_tile) && Dm.truthy(d.density) && wanted(ReplicatedFields.WireDoor(d)))
            .Select(d => (d, s1: get_step(d, SOUTH), n1: get_step(d, NORTH)))
            .Where(p => p.s1 != null && p.n1 != null && Clear(p.s1) && Clear(p.n1) && get_step(p.s1, SOUTH) is turf s2 && Clear(s2)
                && get_step(p.d, EAST)?.density == TRUE && get_step(p.d, WEST)?.density == TRUE)
            .Select(p => (p.d, get_step(p.s1, SOUTH), p.n1))
            .FirstOrDefault());

    // Walks north into the doorway for up to three seconds: the step the client guessed the door, the step the replica showed the server's
    // opening, and whether the server's mob got beyond.
    private (int Guessed, int Opened, bool Passed) WalkInto(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living mob, obj_machinery_door_airlock door, turf beyond)
    {
        ReplicaWorld sees = p.Session.Replica;
        int guessed = -1, opened = -1, step = 0;
        void Corrected(Reconciliation r) => output.WriteLine($"  step {step}: a correction of {r.ErrorTiles * 200:0.0} cm after frame {r.AckedSeq} (replayed {r.Replayed})");
        p.Session.Prediction.Corrected += Corrected;
        p.Keys = MoveKeys.Forward;
        p.Yaw = 0; // north
        w.Run(3 * Second, tick =>
        {
            step++;
            if (MotionMath.ToTiles(p.Session.Prediction.Predicted.Y) >= beyond.y)
                p.Keys = MoveKeys.None; // through: stop on the tile beyond (what lies further is not the door's)
            if (guessed < 0 && sees.TryGetDoorGuess(door.dm_id, out DoorGuess g))
            {
                guessed = step;
                output.WriteLine($"  step {step}: guessed at frame {g.PassableFrom - OwnGuesses.PassableFrames}, passable from frame {g.PassableFrom}");
            }
            if (opened < 0 && sees.TryGet(door.dm_id, out ReplicaEntity e) && sees.TryGetAppearance(e.State.Appearance, out AppearanceEntry a)
                && Anomaly.Client.Shared.World.DoorAnimation.PhaseOf(sees.ResolveString(a.IconState)) == Anomaly.Client.Shared.World.DoorPhase.Opening)
                opened = step;
        });
        p.Keys = MoveKeys.None;
        w.Run(Second);
        p.Session.Prediction.Corrected -= Corrected;
        bool passed = In(w, () => mob.y >= beyond.y);
        output.WriteLine($"door {door.dm_id} at {door.x},{door.y}: guessed at step {guessed}, the server's opening seen at step {opened}, passed {passed}");
        return (guessed, opened, passed);
    }

    [Fact]
    public void An_airlock_the_player_may_open_opens_at_the_bump_and_lets_the_capsule_through_without_a_correction()
    {
        using var w = new RealHeadlessWorld(Lossy(47), MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        mob_living me = w.MobOf(alice);
        OwnGuesses guesses = alice.Session.Guesses;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        IReadOnlyList<string> access = alice.Session.Replica.OwnAccess;
        output.WriteLine("her access: " + string.Join(", ", access));

        // a public airlock: guessed at the bump, a round trip before the server's opening shows, and walked through exactly
        var (door, start, beyond) = Doorway(w, d => d != null && (d.Flags & DoorFlags.Openable) != 0 && d.ReqAll.Length == 0 && d.ReqAny.Length == 0);
        Assert.NotNull(door);
        Do(w, () => Waiting.NoSleep(me.forceMove(start)));
        w.Run(Second);
        PredictionCounters before = alice.Counters;
        (int guessed, int opened, bool passed) = WalkInto(w, alice, me, door, beyond);
        Assert.True(guessed > 0, "the door was guessed");
        Assert.True(opened >= guessed + 3, $"guessed at {guessed}, the server's opening reached the client at {opened}");
        Assert.True(passed);
        Assert.Equal(before.Corrections, alice.Counters.Corrections);
        Assert.Equal(before.Snaps, alice.Counters.Snaps);
        Assert.Equal(0, guesses.Refused);

        // an airlock her access does not open is never guessed (the server denies it: the client shows the server's deny)
        var (locked, lockedStart, lockedBeyond) = Doorway(w, d => d != null && (d.Flags & DoorFlags.Openable) != 0 && (d.Flags & DoorFlags.Emergency) == 0 && d.UnresSides == 0
            && !OwnGuesses.HasAccess(d, access));
        Assert.NotNull(locked);
        Do(w, () => Waiting.NoSleep(me.forceMove(lockedStart)));
        w.Run(Second);
        int made = guesses.Made;
        (guessed, _, passed) = WalkInto(w, alice, me, locked, lockedBeyond);
        Assert.Equal(-1, guessed);
        Assert.False(passed);
        Assert.Equal(made, guesses.Made);

        // an electrified public airlock: tg shocks the bumper and keeps it shut; the client's guess is refused before the cell would have opened
        var (live, liveStart, liveBeyond) = Doorway(w, d => d != null && (d.Flags & DoorFlags.Openable) != 0 && d.ReqAll.Length == 0 && d.ReqAny.Length == 0, except: door);
        Assert.NotNull(live);
        Do(w, () => live.secondsElectrified = MACHINE_ELECTRIFIED_PERMANENT);
        Do(w, () => Waiting.NoSleep(me.forceMove(liveStart)));
        w.Run(Second);
        before = alice.Counters;
        (guessed, _, passed) = WalkInto(w, alice, me, live, liveBeyond);
        Assert.True(guessed > 0);
        Assert.False(passed);
        Assert.True(guesses.Refused >= 1);
        Assert.False(alice.Session.Replica.TryGetDoorGuess(live.dm_id, out _));
        Assert.True(alice.Session.Replica.IsSolid((int)live.x, (int)live.y, (int)live.z));
        output.WriteLine($"guesses {guesses.Made}, refused {guesses.Refused}; corrections {alice.Counters.Corrections - before.Corrections} after the shock");
        Assert.True(w.World.RuntimeErrorCount <= bootRuntimes + 0, "runtimes: " + (w.World.RuntimeErrorCount - bootRuntimes));
    }
}
