using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Protocol;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>The replicator of protocol version 2 (docs/ai/specs/replication.md sections 6 to 9): appearances, capsules, posture, cells, private blocks.</summary>
public class ReplicatorV2Tests
{
    internal static mutable_appearance Layered(string icon, string state, float layer = -1f, string color = null, float alpha = 255f, float pixelX = 0f)
    {
        var m = new mutable_appearance { layer = layer, alpha = alpha, pixel_x = pixelX };
        if (icon != null) m.icon = Dm.resource(icon);
        m.icon_state = state;
        if (color != null) m.color = color;
        return m;
    }

    private static mob NewMob(V2Run run, int x, int y) => run.In(() => Dm.@new<mob>(run.Turf(x, y)));

    private static obj NewObj(V2Run run, int x, int y, string icon, string state)
    {
        return run.In(() =>
        {
            var o = Dm.@new<obj>(run.Turf(x, y));
            o.icon = Dm.resource(icon);
            o.icon_state = state;
            return o;
        });
    }

    private static string Text(V2Run run, SessionView s, atom_movable a)
    {
        Assert.True(s.Replica.TryGet(a.dm_id, out ReplicaEntity e), "entity " + a.dm_id + " is not in the replica");
        return AppearanceText.Of(s.Replica, e.State.Appearance);
    }

    private static string Truth(atom_movable a) => AppearanceText.Of(a, true);

    // ---- appearances ----

    [Fact]
    public void AppearancesAreInternedFromTheAtomsOneIdPerDistinctAppearanceAndSentOnce()
    {
        V2Run run = V2Run.Quiet();
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        run.Step();
        int before = run.Replicator.Appearances.Count;
        obj a = NewObj(run, 3, 3, "icons/obj/a.dmi", "box");
        obj b = NewObj(run, 4, 5, "icons/obj/a.dmi", "box");
        obj c = NewObj(run, 5, 3, "icons/obj/a.dmi", "box_open");
        obj d = NewObj(run, 6, 3, "icons/obj/a.dmi", "box");
        run.In(() =>
        {
            b.name = "another box";
            d.overlays = Dm.list(Layered("icons/obj/a.dmi", "lid", color: "#80ff0080"));
            d.underlays = Dm.list("shadow");
        });
        run.Step();

        Delta delta = Wire.DecodeDelta(run.LastFrame);
        Assert.Equal(4, delta.Spawned.Count);
        TableRef ida = Id(session, a), idb = Id(session, b), idc = Id(session, c), idd = Id(session, d);
        Assert.False(ida.IsNone);
        Assert.Equal(ida, idb); // one id per distinct appearance, whatever the entity
        Assert.NotEqual(ida, idc);
        Assert.NotEqual(ida, idd);
        // a/b, c, the lid overlay, the shadow underlay and d itself: five new rows, each sent in this Delta only.
        Assert.Equal(5, delta.NewAppearances.Count);
        Assert.Equal(before + 5, run.Replicator.Appearances.Count);
        Assert.Equal((uint)before + 1, delta.FirstNewAppearance);
        Assert.Equal(new[] { "icons/obj/a.dmi" }, delta.NewResources.Where(r => r.EndsWith("a.dmi", StringComparison.Ordinal)).ToArray());
        for (int i = 0; i < 5; i++)
        {
            run.Step();
            Delta later = Wire.DecodeDelta(run.LastFrame);
            Assert.Empty(later.NewAppearances);
            Assert.Empty(later.NewStrings);
            Assert.Empty(later.NewResources);
        }

        foreach (obj o in new[] { a, b, c, d })
            Assert.Equal(Truth(o), Text(run, session, o));
        Assert.Contains("lid", Text(run, session, d));
        Assert.Contains("#80ff0080", Text(run, session, d));
    }

    private static TableRef Id(SessionView s, atom_movable a)
    {
        Assert.True(s.Replica.TryGet(a.dm_id, out ReplicaEntity e));
        return e.State.Appearance;
    }

    [Fact]
    public void AnEntityThatShowsNothingHasNoAppearanceAndItsTablesStayEmpty()
    {
        V2Run run = V2Run.Quiet();
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        int before = run.Replicator.Appearances.Count;
        obj plain = run.In(() => Dm.@new<obj>(run.Turf(3, 3)));
        run.Step();
        Assert.True(Id(session, plain).IsNone);
        Assert.Equal(before, run.Replicator.Appearances.Count);
        // An icon state alone shows nothing either (no icon to take it from); an overlay does show something.
        run.In(() => plain.icon_state = "lonely");
        run.Step();
        Assert.True(Id(session, plain).IsNone);
        run.In(() => plain.overlays = Dm.list(Layered("icons/obj/x.dmi", "part")));
        run.Step();
        Assert.False(Id(session, plain).IsNone);
        Assert.Equal(Truth(plain), Text(run, session, plain));
    }

    [Fact]
    public void ChangingAnAppearanceVarOrAListReachesTheReplicaInItsOwnTickAndEarlierAppearancesAreReused()
    {
        V2Run run = V2Run.Quiet(sweepDivisor: 1, minSweep: 0); // the sweep visits everyone every tick: in-place list edits count too
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        obj a = NewObj(run, 3, 3, "icons/obj/a.dmi", "box");
        obj b = NewObj(run, 5, 5, "icons/obj/a.dmi", "box");
        run.Step();
        TableRef boxId = Id(session, a);

        run.In(() => a.icon_state = "box_open");
        run.Step();
        Assert.Contains(Wire.DecodeDelta(run.LastFrame).Changed, c => c.Id == a.dm_id && c.Mask == NetField.Appearance);
        Assert.Equal(Truth(a), Text(run, session, a));

        int rows = run.Replicator.Appearances.Count;
        run.In(() => a.icon_state = "box");
        run.Step();
        Assert.Equal(boxId, Id(session, a)); // back to an appearance the table already has: no new row
        Assert.Equal(rows, run.Replicator.Appearances.Count);

        foreach (Action change in new Action[]
                 {
                     () => a.color = "#ff0000",
                     () => a.alpha = 128,
                     () => a.layer = 4.5f,
                     () => a.plane = 3,
                     () => a.pixel_x = -8,
                     () => a.pixel_y = 5,
                     () => a.appearance_flags = (int)Dm.RESET_COLOR | (int)Dm.KEEP_TOGETHER,
                     () => a.blend_mode = 2,
                     () => { var m = Dm.matrix(); m.Turn(90); a.transform = m; },
                     () => a.overlays = Dm.list(Layered("icons/obj/a.dmi", "o1"), "o2", Dm.resource("icons/obj/z.dmi")),
                     () => a.underlays = Dm.list(Layered(null, "u1", alpha: 40)),
                     () => a.color = Dm.list(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f),
                     () => a.color = Dm.list(0.5f, 0f, 0f, 0f, 0.5f, 0f, 0f, 0f, 0.5f, 0.1f, 0.2f, 0.3f),
                     () => a.overlays.Add(Layered("icons/obj/a.dmi", "added in place")),
                     () => a.overlays.Cut(),
                 })
        {
            run.In(change);
            run.Step();
            Assert.Equal(Truth(a), Text(run, session, a));
            Assert.Equal(Truth(b), Text(run, session, b)); // the neighbour is untouched
        }
    }

    [Fact]
    public void AnInPlaceListEditWithNoHintArrivesWithinTheSweepPeriod()
    {
        V2Run run = V2Run.Quiet(); // the default sweep: 1/64 of the slots per tick, at least 32
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        var things = new List<obj>();
        for (int i = 0; i < 40; i++) things.Add(NewObj(run, 2 + i % 12, 3 + i / 12, "icons/obj/a.dmi", "s" + i));
        run.Step();
        run.In(() =>
        {
            foreach (obj o in things) o.overlays.Add(Layered("icons/obj/a.dmi", "quiet"));
        });
        int period = run.Replicator.SweepPeriodTicks;
        Assert.InRange(period, 1, 64);
        for (int i = 0; i <= period; i++) run.Step();
        foreach (obj o in things)
            Assert.Equal(Truth(o), Text(run, session, o));
    }

    [Fact]
    public void TheTablesAreAppendOnlyAndALateJoinerHoldsTheSameOnes()
    {
        V2Run run = V2Run.Quiet(sweepDivisor: 1, minSweep: 0);
        SessionView early = run.Connect("Early", NewMob(run, 2, 2));
        obj a = NewObj(run, 3, 3, "icons/obj/a.dmi", "x0");
        var rng = new Random(5);
        int lastCount = 0;
        for (int i = 0; i < 200; i++)
        {
            string state = "x" + rng.Next(8);
            run.In(() =>
            {
                a.icon_state = state;
                if (rng.Next(3) == 0) a.overlays = Dm.list(Layered("icons/obj/a.dmi", "o" + rng.Next(4)));
            });
            run.Step();
            Assert.True(run.Replicator.Appearances.Count >= lastCount);
            lastCount = run.Replicator.Appearances.Count;
        }

        // 8 states x (no overlay or one of 4) plus the 4 overlay rows: a few dozen rows, never one per tick.
        Assert.InRange(lastCount, 8, 60);
        SessionView late = run.Connect("Late", NewMob(run, 4, 4));
        Assert.Equal(early.Replica.Resources, late.Replica.Resources);
        Assert.Equal(early.Replica.Strings, late.Replica.Strings);
        Assert.Equal(early.Replica.Appearances.Count, late.Replica.Appearances.Count);
        for (int i = 0; i < early.Replica.Appearances.Count; i++)
            Msg.AssertEqual(early.Replica.Appearances[i], late.Replica.Appearances[i]);
        for (int i = 0; i < 30; i++)
        {
            run.In(() => a.icon_state = "x" + rng.Next(8));
            run.Step();
            Assert.Equal(Truth(a), Text(run, late, a));
            Assert.Equal(Truth(a), Text(run, early, a));
        }
    }

    [Fact]
    public void InvisibleEntitiesStayOutOfEveryTable()
    {
        V2Run run = V2Run.Quiet();
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        obj secret = NewObj(run, 3, 3, "icons/obj/SECRET.dmi", "hidden_state");
        run.In(() => secret.invisibility = Defines.INVISIBILITY_ABSTRACT);
        for (int i = 0; i < 5; i++) run.Step();
        Assert.False(session.Replica.TryGet(secret.dm_id, out _));
        Assert.DoesNotContain("icons/obj/SECRET.dmi", session.Replica.Resources);
        Assert.DoesNotContain("hidden_state", session.Replica.Strings);
        // Spawned invisible, never visible: not in the server's table either.
        Assert.DoesNotContain("icons/obj/SECRET.dmi", run.Replicator.Resources);
        run.In(() => secret.invisibility = 0);
        run.Step();
        Assert.True(session.Replica.TryGet(secret.dm_id, out _));
        Assert.Contains("icons/obj/SECRET.dmi", session.Replica.Resources);
    }

    // ---- capsules and posture ----

    private static MotionState Capsule(int x, int y, int offsetX, int offsetY, ushort yaw) =>
        MotionState.AtTile(x, y, 1, yaw) with { X = x * 256 + offsetX, Y = y * 256 + offsetY };

    [Fact]
    public void ACapsuleMobSendsItsPositionAndYawInTheTicksTheyChangeAndOnlyThen()
    {
        V2Run run = V2Run.Quiet();
        SessionView session = run.Connect("Watcher", NewMob(run, 2, 2));
        mob m = NewMob(run, 5, 5);
        run.Step();
        Assert.True(session.Replica.TryGet(m.dm_id, out ReplicaEntity e));
        Assert.False(e.HasPos);

        // Becoming a capsule: the change record carries the position and yaw (the first record).
        run.Locomotion.Capsules[m.dm_id] = Capsule(5, 5, 0, 0, 0x4000);
        run.Step();
        ChangeOf(run, m, out EntityChange become);
        Assert.Equal(NetField.Pos | NetField.Yaw, become.Mask);
        Assert.True(e.HasPos);
        Assert.Equal((5 * 256, 5 * 256, (byte)0x40), (e.State.PosX, e.State.PosY, e.State.Yaw));
        Assert.False(MotionOf(run, session, m, out _)); // version 42: nothing changed since the record

        run.Step(); // nothing moved: nothing sent for it
        Assert.False(ChangeOf(run, m, out _));
        Assert.False(MotionOf(run, session, m, out _));

        // Version 42: afterwards a slide or a turn is in the Motion, not in a change record.
        run.Locomotion.Capsules[m.dm_id] = Capsule(5, 5, 40, -25, 0x4000);
        run.Step();
        Assert.False(ChangeOf(run, m, out _));
        Assert.True(MotionOf(run, session, m, out MotionEntry slid));
        Assert.Equal((5 * 256 + 40, 5 * 256 - 25, (byte)0x40), (slid.PosX, slid.PosY, slid.Yaw));
        Assert.Equal((5 * 256 + 40, 5 * 256 - 25), (e.State.PosX, e.State.PosY));
        Assert.Equal(run.World.World.Tick, run.Replicator.ChangedAt(m.dm_id, NetField.Pos)); // still stamped

        run.Locomotion.Capsules[m.dm_id] = Capsule(5, 5, 40, -25, 0x4100);
        run.Step();
        Assert.False(ChangeOf(run, m, out _));
        Assert.True(MotionOf(run, session, m, out MotionEntry turned));
        Assert.Equal((byte)0x41, turned.Yaw);
        Assert.Equal((byte)0x41, e.State.Yaw);

        // Crossing into the next tile: tg moved the mob (X, LastMoveTick) in the record, the capsule inside the new tile in the Motion.
        run.In(() => Waiting.NoSleep(m.forceMove(run.Turf(6, 5))));
        run.Locomotion.Capsules[m.dm_id] = Capsule(6, 5, -100, 0, 0x4100);
        run.Step();
        ChangeOf(run, m, out EntityChange crossed);
        Assert.Equal(NetField.X | NetField.LastMoveTick, crossed.Mask);
        Assert.Equal(6, e.State.X);
        Assert.Equal(6 * 256 - 100, e.State.PosX);
        Assert.Equal(run.World.World.Tick, e.State.LastMoveTick);

        // A stopped mob is still named for Limits.MotionResendTicks ticks after its last change (a lost Motion is covered), then not.
        for (int i = 1; i < Limits.MotionResendTicks; i++)
        {
            run.Step();
            Assert.True(MotionOf(run, session, m, out MotionEntry again), "resend " + i);
            Assert.Equal(6 * 256 - 100, again.PosX);
        }
        run.Step();
        Assert.False(MotionOf(run, session, m, out _));
    }

    // Version 42: the session's Motion entry for the movable after the last Step, if any.
    private static bool MotionOf(V2Run run, SessionView session, atom_movable a, out MotionEntry entry)
    {
        var messages = new List<MotionMessage>();
        run.Replicator.BuildMotion(session.Ordinal, messages);
        foreach (MotionMessage message in messages)
        {
            Assert.Equal(run.World.World.Tick, message.Tick);
            foreach (MotionEntry e in message.Entries)
            {
                if (e.Id != a.dm_id) continue;
                entry = e;
                return true;
            }
        }
        entry = default;
        return false;
    }

    private static bool ChangeOf(V2Run run, atom_movable a, out EntityChange change)
    {
        Delta delta = Wire.DecodeDelta(run.LastFrame);
        foreach (EntityChange c in delta.Changed)
        {
            if (c.Id != a.dm_id) continue;
            change = c;
            return true;
        }

        change = default;
        return false;
    }

    [Fact]
    public void ACapsuleThatStopsBeingOneKeepsSendingItsTileCentre()
    {
        V2Run run = V2Run.Quiet();
        SessionView session = run.Connect("Watcher", NewMob(run, 2, 2));
        mob m = NewMob(run, 5, 5);
        run.Locomotion.Capsules[m.dm_id] = Capsule(5, 5, 60, 30, 0x2000);
        run.Step();
        session.Replica.TryGet(m.dm_id, out ReplicaEntity e);
        Assert.True(e.HasPos);

        run.Locomotion.Capsules.Remove(m.dm_id); // the session left; the mob stays
        run.Step();
        Assert.True(e.HasPos);
        Assert.Equal((5 * 256, 5 * 256), (e.State.PosX, e.State.PosY));
        Assert.Equal((byte)0x20, e.State.Yaw); // the last yaw stays
        run.In(() => Waiting.NoSleep(m.forceMove(run.Turf(8, 5))));
        run.Step();
        Assert.Equal((8 * 256, 5 * 256), (e.State.PosX, e.State.PosY));

        // Only a mob can be a capsule: the seam never names an obj, but if a view did, the replicator ignores it.
        obj thing = NewObj(run, 9, 9, "icons/obj/a.dmi", "t");
        run.Locomotion.Capsules[thing.dm_id] = Capsule(9, 9, 10, 10, 0);
        run.Step();
        session.Replica.TryGet(thing.dm_id, out ReplicaEntity prop);
        Assert.False(prop.HasPos);

        // A late joiner gets the capsule's record whole.
        run.Locomotion.Capsules[m.dm_id] = Capsule(8, 5, -50, 90, 0x8000);
        run.Step();
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        late.Replica.TryGet(m.dm_id, out ReplicaEntity same);
        Assert.True(same.HasPos);
        Assert.Equal(e.State, same.State with { });
    }

    [Fact]
    public void ThePostureOfLivingMobsFollowsBodyPositionAndLyingAngle()
    {
        V2Run run = V2Run.Quiet(sweepDivisor: 1, minSweep: 0);
        SessionView session = run.Connect("Watcher", NewMob(run, 2, 2));
        var capsule = run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(5, 5)));
        var idle = run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(7, 5)));
        run.Locomotion.Capsules[capsule.dm_id] = Capsule(5, 5, 0, 0, 0);
        run.Step();
        session.Replica.TryGet(capsule.dm_id, out ReplicaEntity c);
        session.Replica.TryGet(idle.dm_id, out ReplicaEntity i);
        Assert.Equal(0, c.State.Posture);

        run.In(() =>
        {
            capsule.body_position = Defines.LYING_DOWN;
            capsule.lying_angle = 90;
            idle.body_position = Defines.LYING_DOWN;
            idle.lying_angle = 270;
        });
        run.Step();
        Assert.Equal(PostureBits.Encode(true, 90), c.State.Posture);
        Assert.Equal(PostureBits.Encode(true, 270), i.State.Posture); // the sweep found the one nobody polls
        Assert.True(PostureBits.IsLying(c.State.Posture));
        Assert.Equal(270, PostureBits.LyingAngle(i.State.Posture));
        // The capsule's was polled: found in its own tick; the plain mob's only by the sweep.
        Assert.Contains(run.Replicator.LastSweepCatches, s => s.Id == idle.dm_id && (s.Fields & NetField.Posture) != 0);
        Assert.DoesNotContain(run.Replicator.LastSweepCatches, s => s.Id == capsule.dm_id);

        run.In(() =>
        {
            capsule.body_position = Defines.STANDING_UP;
            capsule.lying_angle = 90; // tg keeps the angle: a standing mob's posture byte does not carry it
        });
        run.Step();
        Assert.Equal(0, c.State.Posture);

        // DV573: buckled to an object (here a bed's place: any obj) is a bit of its own; buckled to a mob is not
        var bed = run.In(() => Dm.@new<obj_structure_bed>(run.Turf(5, 5)));
        run.In(() => capsule.buckled = bed);
        run.Step();
        Assert.True(PostureBits.IsBuckled(c.State.Posture));
        run.In(() => capsule.buckled = idle);
        run.Step();
        Assert.False(PostureBits.IsBuckled(c.State.Posture));
    }

    // ---- the map's cells ----

    [Fact]
    public void TheJoinsMapCarriesTurfAppearancesAndSolidityAndTheyStayCurrentAsTheWorldChanges()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000); // the cell sweep looks at every cell every tick
        run.In(() =>
        {
            turf floor = run.Turf(6, 6);
            floor.icon = Dm.resource("icons/turf/floors.dmi");
            floor.icon_state = "floor";
        });
        run.Step();
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        Assert.Empty(V2Views.CellDifferences(session.Replica, run.World.World));
        Assert.Contains("floors.dmi", AppearanceText.Of(session.Replica, session.Replica.TurfAppearance(6, 6, 1)));
        Assert.True(session.Replica.IsSolid(0, 3, 1)); // outside
        Assert.True(session.Replica.IsSolid(1, 1, 1)); // the room's wall

        // A dense obj makes its tile solid in the tick it appears; moving it frees one tile and takes another.
        obj crate = NewObj(run, 5, 5, "icons/obj/crate.dmi", "closed");
        run.In(() => crate.density = 1);
        run.Step();
        Assert.True(session.Replica.IsSolid(5, 5, 1));
        Assert.Contains(Wire.DecodeDelta(run.LastFrame).SolidChanged, c => c is { X: 5, Y: 5, Solid: true });
        run.In(() => Waiting.NoSleep(crate.forceMove(run.Turf(7, 5))));
        run.Step();
        Assert.False(session.Replica.IsSolid(5, 5, 1));
        Assert.True(session.Replica.IsSolid(7, 5, 1));
        Delta moved = Wire.DecodeDelta(run.LastFrame);
        Assert.Equal(2, moved.SolidChanged.Count);
        run.In(() => qdel(crate));
        run.Step();
        Assert.False(session.Replica.IsSolid(7, 5, 1));

        // A dense MOB is no wall (capsules collide with it as capsules); a turf's density flip has no hint: the cell sweep finds it.
        mob crowd = NewMob(run, 9, 9);
        run.In(() => crowd.density = 1);
        run.Step();
        Assert.False(session.Replica.IsSolid(9, 9, 1));
        run.In(() => run.Turf(10, 3).density = 1);
        run.Step();
        Assert.True(session.Replica.IsSolid(10, 3, 1));

        // A turf's appearance var is hinted.
        run.In(() => run.Turf(6, 6).icon_state = "damaged");
        run.Step();
        Delta turfChange = Wire.DecodeDelta(run.LastFrame);
        Assert.Contains(turfChange.TurfAppearanceChanged, c => c is { X: 6, Y: 6 });
        Assert.Empty(V2Views.CellDifferences(session.Replica, run.World.World));

        // A joiner after all that gets the map as it is.
        SessionView late = run.Connect("Late", NewMob(run, 3, 3));
        Assert.Empty(V2Views.CellDifferences(late.Replica, run.World.World));
    }

    [Fact]
    public void WithoutAnyHintTheCellSweepFindsAChangedTurfWithinItsPeriod()
    {
        V2Run run = V2Run.Quiet(size: 12, cellSweep: 16);
        SessionView session = run.Connect("Early", NewMob(run, 2, 2));
        int period = run.Replicator.CellSweepPeriodTicks;
        Assert.InRange(period, 2, 20);
        run.In(() => run.Turf(8, 8).density = 1);
        for (int i = 0; i <= period; i++) run.Step();
        Assert.True(session.Replica.IsSolid(8, 8, 1));
    }

    // ---- the private block ----

    [Fact]
    public void TheOwnMotionAndTheBufferDepthComeInTheSessionsBlockOnly()
    {
        V2Run run = V2Run.Quiet();
        mob mine = NewMob(run, 4, 4);
        SessionView me = run.Connect("Me", mine);
        SessionView other = run.Connect("Other", NewMob(run, 8, 8));
        run.Locomotion.Capsules[mine.dm_id] = Capsule(4, 4, 33, -12, 0x1234);
        me.LastInputSeq = 41;
        me.InputBufferDepth = 2;
        other.InputBufferDepth = 1;
        run.Step();
        Assert.True(me.Replica.Own.HasMotion);
        Assert.Equal(Capsule(4, 4, 33, -12, 0x1234), me.Replica.Own.Motion);
        Assert.Equal(MotionParams.DefaultRun, me.Replica.Own.Params);
        Assert.Equal(41u, me.Replica.Own.AckedSeq);
        Assert.Equal(2, me.Replica.Own.InputBufferDepth);
        Assert.Equal(run.World.World.Tick, me.Replica.Own.Tick);
        // The other session's mob is no capsule here: its block has no motion, and never mine.
        Assert.False(other.Replica.Own.HasMotion);
        Assert.Equal(1, other.Replica.Own.InputBufferDepth);
        // Both see my capsule's public Pos and yaw, as everyone does.
        other.Replica.TryGet(mine.dm_id, out ReplicaEntity seen);
        Assert.True(seen.HasPos);
        Assert.Equal(4 * 256 + 33, seen.State.PosX);
    }

    [Fact]
    public void ScreenEntriesAreThePlayersOwnAndFollowTheirObjectsInTheTickTheyChange()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewMob(run, 4, 4));
        SessionView other = run.Connect("Other", NewMob(run, 8, 8));
        obj doll = null;
        // M4a P2: tg's /datum/hud/New adds the screentip object to every mob's HUD (hud.dm:103), so a player's screen also
        // holds its HUD's own objects; the test follows the doll among them.
        long[] Hud(SessionView s) => run.In(() =>
        {
            DmList objects = s.Client.mob?.hud_used?.screen_objects;
            return objects == null ? Array.Empty<long>() : Enumerable.Range(1, (int)objects.len).Select(i => objects[objects[i]]).OfType<atom>().Select(a => a.dm_id).ToArray();
        });
        run.In(() =>
        {
            doll = Dm.@new<obj>(null);
            doll.icon = Dm.resource("icons/hud/screen_gen.dmi");
            doll.icon_state = "head0";
            doll.screen_loc = "WEST,CENTER-2";
            me.Client.screen.Add(doll);
        });
        run.Step();
        Assert.Equal(new[] { doll.dm_id }, me.Replica.Screen.Keys.Except(Hud(me)).ToArray());
        Assert.Empty(other.Replica.Screen.Keys.Except(Hud(other)));
        ScreenEntry entry = me.Replica.Screen[doll.dm_id];
        Assert.Equal("WEST,CENTER-2", entry.ScreenLoc);
        Assert.True(entry.Appearance.Private);
        Assert.Equal(AppearanceText.Of(doll, true, keepDir: true), AppearanceText.Of(me.Replica, entry.Appearance));
        // The screen object is no entity (it has no turf), and nothing of it is in the public tables.
        Assert.False(me.Replica.TryGet(doll.dm_id, out _));
        Assert.DoesNotContain("head0", me.Replica.Strings);
        Assert.DoesNotContain("icons/hud/screen_gen.dmi", me.Replica.Resources);
        Assert.Contains("head0", me.Replica.PrivateStrings);

        var changed = new List<long>();
        me.Replica.ScreenEntryChanged += e => changed.Add(e.Id);
        run.In(() => doll.icon_state = "head3");
        run.Step();
        Assert.Equal(new[] { doll.dm_id }, changed);
        Assert.Equal(AppearanceText.Of(doll, true, keepDir: true), AppearanceText.Of(me.Replica, me.Replica.Screen[doll.dm_id].Appearance));
        run.In(() => doll.screen_loc = "EAST,CENTER");
        run.Step();
        Assert.Equal("EAST,CENTER", me.Replica.Screen[doll.dm_id].ScreenLoc);
        run.Step(); // nothing changed: nothing sent
        Assert.Equal(2, changed.Count);

        var gone = new List<long>();
        me.Replica.ScreenEntryRemoved += gone.Add;
        run.In(() => me.Client.screen.Remove(doll));
        run.Step();
        Assert.Equal(new[] { doll.dm_id }, gone);
        Assert.Empty(me.Replica.Screen.Keys.Except(Hud(me)));

        // A rejoin starts the private tables afresh (the Snapshot carries them whole).
        run.In(() => me.Client.screen.Add(doll));
        run.Step();
        run.Leave(me);
        SessionView again = run.Connect("Me2", NewMob(run, 5, 5));
        run.In(() => again.Client.screen.Add(doll));
        run.Step();
        Assert.Equal(new[] { doll.dm_id }, again.Replica.Screen.Keys.Except(Hud(again)).ToArray());
    }

    [Fact]
    public void AScreenObjectsInPlaceOverlayEditIsFoundBySweepingTheScreen()
    {
        V2Run run = V2Run.Quiet(screenSweep: 5);
        SessionView me = run.Connect("Me", NewMob(run, 4, 4));
        obj fullscreen = null;
        run.In(() =>
        {
            fullscreen = Dm.@new<obj>(null);
            fullscreen.icon = Dm.resource("icons/hud/screen_full.dmi");
            fullscreen.icon_state = "crit";
            me.Client.screen.Add(fullscreen);
        });
        run.Step();
        run.In(() => fullscreen.overlays.Add(Layered("icons/hud/screen_full.dmi", "pulse")));
        for (int i = 0; i < 6; i++) run.Step();
        Assert.Equal(AppearanceText.Of(fullscreen, true, keepDir: true), AppearanceText.Of(me.Replica, me.Replica.Screen[fullscreen.dm_id].Appearance));
    }

    // ---- the contract ----

    [Fact]
    public void TheVersion2EntryPointsFollowTheCallOrderAndTheOutsideTheWorldRule()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewMob(run, 4, 4));
        long tick = run.World.World.Tick;
        PrivateInputs inputs = run.Inputs(me);
        // A private block and a join are for the tick the shadows hold (after its BuildDelta).
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.BuildPrivate(tick + 1, inputs));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.BuildPrivate(tick - 1, inputs));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.BuildJoin(tick - 1, inputs));
        run.World.Step();
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.BuildPrivate(tick + 1, inputs)); // the world is a tick ahead of the shadows
        run.Replicator.BuildDelta(tick + 1);
        Assert.True(run.Replicator.BuildPrivate(tick + 1, inputs).Bytes.Length >= Codec.EmptyOwnBlockBytes);
        // Like the rest of the replicator, never inside the world.
        using (run.World.World.Enter())
        {
            Assert.Throws<InvalidOperationException>(() => run.Replicator.BuildPrivate(tick + 1, inputs));
            Assert.Throws<InvalidOperationException>(() => run.Replicator.BuildJoin(tick + 1, inputs));
        }

        // The version-1 join (no session) gives the public tables and an empty private block.
        JoinMessages join = run.Replicator.BuildJoin(tick + 1, me.Avatar.dm_id, 0);
        Snapshot snapshot = Wire.DecodeSnapshot(join.Snapshot);
        Assert.False(snapshot.Own.HasMotion);
        Assert.Empty(snapshot.Own.ScreenAdded);
        Assert.Equal(run.Replicator.Appearances.Count, snapshot.Appearances.Count);
    }

    [Fact]
    public void TheHostReachesTheVersion2MembersThroughTheInterface()
    {
        // The host holds an IReplicator: the version-2 members must be the replicator's, not the interface's throwing defaults.
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewMob(run, 4, 4));
        run.World.Step();
        IReplicator replicator = run.Replicator;
        replicator.BuildDelta(run.World.World.Tick);
        PrivateFrame frame = replicator.BuildPrivate(run.World.World.Tick, run.Inputs(me));
        Assert.False(frame.Bytes.IsEmpty);
        JoinMessages join = replicator.BuildJoin(run.World.World.Tick, run.Inputs(me));
        Assert.NotEmpty(join.MapGrid);
        Assert.NotEmpty(join.Snapshot);
        replicator.Forget(me.Ordinal);
    }

    [Fact]
    public void TheFieldStampsTellWhenPosPostureAndAppearanceLastChanged()
    {
        V2Run run = V2Run.Quiet(sweepDivisor: 1, minSweep: 0);
        run.Connect("Me", NewMob(run, 4, 4));
        mob_living_carbon_human h = run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(6, 6)));
        run.Step();
        long born = run.World.World.Tick;
        Assert.Equal(born, run.Replicator.ChangedAt(h.dm_id, NetField.Pos));
        run.Step();
        run.Step();
        Assert.Equal(born, run.Replicator.ChangedAt(h.dm_id, NetField.Appearance));
        run.Locomotion.Capsules[h.dm_id] = Capsule(6, 6, 20, 0, 0);
        run.Step();
        long posTick = run.World.World.Tick;
        Assert.Equal(posTick, run.Replicator.ChangedAt(h.dm_id, NetField.Pos));
        Assert.Equal(posTick, run.Replicator.ChangedAt(h.dm_id, NetField.Yaw));
        Assert.Equal(born, run.Replicator.ChangedAt(h.dm_id, NetField.Posture));
        run.In(() =>
        {
            h.body_position = Defines.LYING_DOWN;
            h.overlays = Dm.list(Layered("icons/mob/human/dam_human.dmi", "head_11"));
        });
        run.Step();
        long lay = run.World.World.Tick;
        Assert.Equal(lay, run.Replicator.ChangedAt(h.dm_id, NetField.Posture));
        Assert.Equal(lay, run.Replicator.ChangedAt(h.dm_id, NetField.Appearance));
        Assert.Equal(posTick, run.Replicator.ChangedAt(h.dm_id, NetField.Pos));
        Assert.Equal(-1, run.Replicator.ChangedAt(123456789, NetField.Pos));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.ChangedAt(h.dm_id, NetField.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.Replicator.ChangedAt(h.dm_id, NetField.Pos | NetField.Yaw));
    }
}
