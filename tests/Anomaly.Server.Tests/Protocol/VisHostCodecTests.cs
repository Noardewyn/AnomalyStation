using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 10 (docs/ai/specs/protocol.md section 19): a short-lived visual's host (NetField.VisHost) on the wire.</summary>
public class VisHostCodecTests
{
    [Fact]
    public void A_visual_on_an_entity_and_one_on_its_turf_round_trip_in_records_and_changes()
    {
        EntityState onMob = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { IsVisual = true, VisHost = 5 };
        EntityState onTurf = Msg.State(8, 0, 4, 3, 1, 0, null, 0f, 10) with { IsVisual = true, VisHost = 0 };
        EntityState plain = Msg.State(9, 0, 4, 4, 1, 0, null, 0f, 10);
        Snapshot s = Msg.Snapshot(100, 7, 0, new[] { "/atom/movable/flick_visual" }, onMob, onTurf, plain);
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot snapshot, out ProtocolError error), error.ToString());
        Assert.True(snapshot.Entities[0].IsVisual);
        Assert.Equal(5, snapshot.Entities[0].VisHost);
        Assert.True(snapshot.Entities[1].IsVisual);
        Assert.Equal(0, snapshot.Entities[1].VisHost);
        Assert.False(snapshot.Entities[2].IsVisual);

        Delta d = Msg.Delta(200, changed: new[] { new EntityChange { Id = 7, Mask = NetField.VisHost | NetField.X, X = 4, VisHost = 9 } });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(9, decoded.Changed[0].VisHost);
    }

    [Fact]
    public void The_replica_keeps_a_visual_a_visual_and_follows_a_new_host()
    {
        var replica = new ReplicaWorld();
        var map = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        map.TurfTypes.Add("/turf/open/floor");
        map.Runs.Add(new TurfRun(map.CellCount, 0));
        replica.Apply(map);
        EntityState onTurf = Msg.State(8, 0, 4, 3, 1, 0, null, 0f, 10) with { IsVisual = true, VisHost = 0 };
        replica.Apply(Msg.Snapshot(100, 8, 0, new[] { "/atom/movable/flick_visual" }, onTurf));
        ReplicaEntity visual = replica.Entities.Single(e => e.Id == 8);
        Assert.True(visual.IsVisual);
        Assert.Equal(0, visual.VisHost);
        replica.Apply(Msg.Delta(101, changed: new[] { new EntityChange { Id = 8, Mask = NetField.VisHost, VisHost = 3 } }));
        Assert.True(visual.IsVisual);
        Assert.Equal(3, visual.VisHost);
        replica.Apply(Msg.Delta(102, changed: new[] { new EntityChange { Id = 8, Mask = NetField.Dir, Dir = 4 } }));
        Assert.True(visual.IsVisual);
        Assert.Equal(3, visual.VisHost);
    }

    [Fact]
    public void The_codec_refuses_a_visual_on_itself_an_undefined_record_flag_and_a_host_on_an_ordinary_entity()
    {
        EntityState self = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { IsVisual = true, VisHost = 7 };
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(100, 7, 0, new[] { "/obj" }, self)));
        EntityState stray = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { VisHost = 3 };
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(100, 7, 0, new[] { "/obj" }, stray)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(200, changed: new[] { new EntityChange { Id = 7, Mask = NetField.VisHost, VisHost = 7 } })));

        // A record whose flags byte sets bit 7 (version 34: a shot follows) and has no shot after the host is refused: find the flags
        // byte of a visual record (bit 6 set, the host after it) by flipping each byte that holds exactly 64.
        EntityState visual = Msg.State(7, 0, 3, 3, 1, 0, null, 0f, 10) with { IsVisual = true, VisHost = 0 };
        byte[] good = Codec.Encode(Msg.Snapshot(100, 7, 0, new[] { "/obj" }, visual));
        Assert.True(Codec.TryDecode(good, out Snapshot _, out _));
        bool refused = false;
        for (int i = 0; i < good.Length; i++)
        {
            if (good[i] != 64) continue;
            byte[] bytes = (byte[])good.Clone();
            bytes[i] = 64 | 128;
            if (!Codec.TryDecode(bytes, out Snapshot _, out ProtocolError error) && error is ProtocolError.BadValue or ProtocolError.Truncated)
                refused = true;
        }
        Assert.True(refused);
    }
}
