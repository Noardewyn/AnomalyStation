using Anomaly.Protocol;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// Protocol version 13 (M4b audio slice 2, docs/ai/specs/protocol.md section 22): the Sounds message's round trip, its bytes, every
/// refusal of the encoder and the decoder, and the replica's path table and checks (it keeps no sound state and raises
/// SoundsReceived with the files resolved).
/// </summary>
public class ProtocolV13Tests
{
    private static SoundEvent Play(uint path, ushort channel = 7, byte volume = 28) =>
        new() { Kind = SoundKind.Play, Channel = channel, Path = path, Volume = volume };

    private static Sounds Message(uint first, IEnumerable<string> paths, params SoundEvent[] events)
    {
        var m = new Sounds { FirstPath = first };
        m.NewPaths.AddRange(paths);
        m.Events.AddRange(events);
        return m;
    }

    private static MapGrid Grid(int size = 8)
    {
        var grid = new MapGrid { Tick = 5, Width = size, Height = size, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        return grid;
    }

    [Fact]
    public void TheVersionIsThirteenAndSoundsIsMessageFourteen()
    {
        Assert.True(ProtocolVersion.Current >= 13);
        Assert.Equal(14, (int)MessageId.Sounds);
        Assert.True(WireReader.IsKnownMessageId(14));
    }

    [Fact]
    public void A_client_of_version_12_is_refused()
    {
        using var server = Anomaly.Server.Tests.Server.TestServer.Start();
        Anomaly.Server.Tests.Server.FakeRemote remote = server.Connect("Alice", protocol: 12);
        server.Tick();
        Assert.Equal(DisconnectReason.WrongVersion, remote.Rejected?.Reason);
    }

    [Fact]
    public void EveryFieldRoundTripsAndTheBytesAreTheLayout()
    {
        var full = new SoundEvent
        {
            Kind = SoundKind.Play, Channel = 1011, Path = 2, Volume = 200, Muffle = 143, Occlusion = 255, Environment = 21,
            Repeat = true, Mute = true, Positional = true, X = 255, Y = 1, Z = 16, Frequency = 44100f, Offset = 12.5f,
        };
        var update = new SoundEvent { Kind = SoundKind.Update, Channel = 1018, Volume = 10, Muffle = 3, Occlusion = 64, Positional = true, X = 4, Y = 5, Z = 1 };
        var stop = new SoundEvent { Kind = SoundKind.Stop, Channel = 1018 };
        var stopAll = new SoundEvent { Kind = SoundKind.Stop, Channel = 0 };
        var anyChannel = new SoundEvent { Kind = SoundKind.Play, Channel = 0, Path = 1, Volume = 100, Frequency = -1f };
        Sounds message = Message(1, new[] { "sound/machines/airlock/airlock.ogg", "sound/effects/sparks/sparks1.ogg" }, full, update, stop, stopAll, anyChannel);
        byte[] bytes = Codec.Encode(message);
        Assert.Equal((byte)MessageId.Sounds, bytes[0]);
        Assert.True(Codec.TryDecode(bytes, out Sounds back, out ProtocolError error), error.ToString());
        Assert.Equal(1u, back.FirstPath);
        Assert.Equal(message.NewPaths, back.NewPaths);
        Assert.Equal(message.Events, back.Events);
        Assert.Equal(bytes, Codec.Encode(back));
        Assert.True(bytes.Length <= Limits.MaxSoundsBytes);

        // a plain play: id, path count 0, first 0, event count 1, then head, channel (u16), path, volume, muffle, occlusion, environment
        byte[] plain = Codec.Encode(Message(0, Array.Empty<string>(), Play(3, channel: 0x0102)));
        Assert.Equal(new byte[] { 14, 0, 0, 1, 0x00, 0x02, 0x01, 3, 28, 0, 0, 0 }, plain);
        // a stop is its head and channel; a positional update adds x, y, z
        Assert.Equal(new byte[] { 14, 0, 0, 1, 0x02, 0xFA, 0x03 }, Codec.Encode(Message(0, Array.Empty<string>(), stop)));
        Assert.Equal(new byte[] { 14, 0, 0, 1, 0x11, 0xFA, 0x03, 10, 3, 64, 0, 4, 5, 1 }, Codec.Encode(Message(0, Array.Empty<string>(), update)));
        // the full play's head: kind 0, repeat, mute, positional, frequency, offset
        Assert.Equal(0x7C, Codec.Encode(Message(0, Array.Empty<string>(), full))[4]);
        Assert.Equal(100f, back.Events[0].VolumeValue);
    }

    [Fact]
    public void TheEncoderRefusesWhatTheDecoderWould()
    {
        string[] none = Array.Empty<string>();
        foreach (SoundEvent bad in new[]
                 {
                     new SoundEvent { Kind = (SoundKind)3, Channel = 1 },
                     Play(1, channel: 1025),
                     new SoundEvent { Kind = SoundKind.Update, Channel = 0 },
                     Play(0),
                     Play(Limits.MaxSoundTableEntries + 1),
                     Play(1, volume: 201),
                     Play(1) with { Environment = 27 },
                     new SoundEvent { Kind = SoundKind.Stop, Channel = 1, Repeat = true },
                     new SoundEvent { Kind = SoundKind.Stop, Channel = 1, Volume = 1 },
                     new SoundEvent { Kind = SoundKind.Update, Channel = 1, Path = 1 },
                     Play(1) with { Positional = true, X = 0, Y = 1, Z = 1 },
                     Play(1) with { Positional = true, X = 1, Y = 1, Z = Limits.MaxMapLevels + 1 },
                     Play(1) with { X = 3 },
                     Play(1) with { Frequency = float.NaN },
                     Play(1) with { Frequency = 2_000_000f },
                     Play(1) with { Offset = -1f },
                     Play(1) with { Offset = 40000f },
                 })
        {
            Assert.Throws<ProtocolException>(() => Codec.Encode(Message(0, none, bad)));
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(1, none, Play(1)))); // a start with no paths
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(0, new[] { "a.ogg" }))); // paths with no start
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(1, new[] { "" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(1, new[] { new string('a', Limits.MaxSoundPathBytes + 1) })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(1, Enumerable.Range(0, Limits.MaxSoundPaths + 1).Select(i => "p" + i))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(0, none, Enumerable.Range(0, Limits.MaxSoundEvents + 1).Select(_ => Play(1)).ToArray())));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Message(Limits.MaxSoundTableEntries, new[] { "a", "b" })));
        // the caps themselves pass
        Codec.Encode(Message(1, Enumerable.Range(0, Limits.MaxSoundPaths).Select(i => new string('p', Limits.MaxSoundPathBytes - 2) + i.ToString("00")),
            Enumerable.Range(0, Limits.MaxSoundEvents).Select(_ => Play(1) with { Positional = true, X = 9, Y = 9, Z = 300 % 16 + 1, Frequency = 1f, Offset = 1f }).ToArray()));
    }

    // One event after an empty path table: [14][0][0][1] then the event's bytes.
    private static byte[] Raw(params byte[] eventBytes) => Msg.Raw(MessageId.Sounds, r => r.Byte(0).VarUInt(0).VarUInt(1).Bytes(eventBytes));

    private static ProtocolError Refusal(byte[] message)
    {
        Assert.False(Codec.TryDecode(message, out Sounds value, out ProtocolError error), "accepted: " + Convert.ToHexString(message));
        Assert.Null(value);
        Assert.NotEqual(ProtocolError.None, error);
        return error;
    }

    [Fact]
    public void TheDecoderRefusesEveryViolation()
    {
        Assert.True(Codec.TryDecode(Raw(0x00, 7, 0, 1, 28, 0, 0, 0), out Sounds _, out _)); // the good base
        Assert.True(Codec.TryDecode(Raw(0x80, 7, 0, 1, 28, 0, 0, 0), out Sounds _, out _)); // version 78: the top bit is a play's wait
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x82, 7, 0))); // a stop with a wait
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x03, 7, 0))); // kind 3
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x00, 0x01, 0x04, 1, 28, 0, 0, 0))); // channel 1025
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x01, 0, 0, 28, 0, 0, 0))); // an update on channel 0
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x06, 7, 0))); // a stop with repeat
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x00, 7, 0, 0, 28, 0, 0, 0))); // path index 0
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x00, 7, 0, 0x81, 0x80, 0x04, 28, 0, 0, 0))); // path index 65537
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x00, 7, 0, 1, 201, 0, 0, 0))); // volume 100.5
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x00, 7, 0, 1, 28, 0, 0, 27))); // environment 26
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x10, 7, 0, 1, 28, 0, 0, 0, 0, 5, 1))); // x 0
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x10, 7, 0, 1, 28, 0, 0, 0, 5, 0, 1))); // y 0
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x10, 7, 0, 1, 28, 0, 0, 0, 5, 5, (byte)(Limits.MaxMapLevels + 1)))); // z over the levels
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x20, 7, 0, 1, 28, 0, 0, 0, 0x00, 0x00, 0xC0, 0x7F))); // a NaN frequency
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x20, 7, 0, 1, 28, 0, 0, 0, 0, 0, 0, 0))); // a frequency of 0 flagged
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x40, 7, 0, 1, 28, 0, 0, 0, 0, 0, 0x80, 0xBF))); // offset -1
        Assert.Equal(ProtocolError.BadValue, Refusal(Raw(0x40, 7, 0, 1, 28, 0, 0, 0, 0x00, 0x00, 0x80, 0x7F))); // an infinite offset
        Assert.Equal(ProtocolError.TrailingBytes, Refusal(Raw(0x02, 7, 0, 0)));
        Assert.Equal(ProtocolError.Truncated, Refusal(Raw(0x00, 7, 0, 1, 28, 0, 0)));

        // the path table part: a start with no paths, paths with no start, an empty or null path, too many paths or events
        Assert.Equal(ProtocolError.BadValue, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(0).VarUInt(1).VarUInt(0))));
        Assert.Equal(ProtocolError.BadValue, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(1).VarUInt(0).String("a.ogg").VarUInt(0))));
        Assert.Equal(ProtocolError.BadString, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(1).VarUInt(1).String("").VarUInt(0))));
        Assert.Equal(ProtocolError.BadString, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(1).VarUInt(1).Byte(0).Byte(0).VarUInt(0))));
        Assert.Equal(ProtocolError.CountTooLarge, Refusal(Msg.Raw(MessageId.Sounds, r => r.VarUInt(33).VarUInt(1).Bytes(new byte[33 * 2]).VarUInt(0))));
        Assert.Equal(ProtocolError.CountTooLarge, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(0).VarUInt(0).VarUInt(65).Bytes(new byte[65 * 3]))));
        Assert.Equal(ProtocolError.CountTooLarge, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(0).VarUInt(0).VarUInt(10).Bytes(0x02, 1, 0))));
        Assert.Equal(ProtocolError.BadValue, Refusal(Msg.Raw(MessageId.Sounds, r => r.Byte(0).VarUInt(70000).VarUInt(0))));

        // every truncation of a full message
        byte[] good = Codec.Encode(Message(1, new[] { "sound/a.ogg" }, Play(1) with { Positional = true, X = 3, Y = 4, Z = 1, Frequency = 2f, Offset = 3f },
            new SoundEvent { Kind = SoundKind.Stop, Channel = 5 }));
        for (int length = 0; length < good.Length; length++)
            Assert.False(Codec.TryDecode(good.AsSpan(0, length), out Sounds _, out _), "a prefix of " + length + " bytes was accepted");
        Assert.True(CodecTests.TryDecodeAny(good, out _));
    }

    [Fact]
    public void TheReplicaResolvesThePathsKeepsNoSoundStateAndRefusesWhatDoesNotFit()
    {
        var replica = new ReplicaWorld();
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        Sounds first = Message(1, new[] { "sound/a.ogg", "sound/b.ogg" }, Play(2), new SoundEvent { Kind = SoundKind.Stop, Channel = 3 });
        Assert.Throws<ProtocolException>(() => replica.Apply(first)); // before any MapGrid
        Assert.Equal(0, replica.SoundPathCount);

        replica.Apply(Grid());
        Assert.True(replica.TryApply(Codec.Encode(first), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Sounds, id);
        Assert.Equal(2, replica.SoundPathCount);
        Assert.Equal("sound/b.ogg", heard[0].File);
        Assert.Equal(SoundKind.Stop, heard[1].Kind);
        Assert.Null(heard[1].File);

        // an old index needs no path; the next paths continue the table
        heard.Clear();
        replica.Apply(Message(3, new[] { "sound/c.ogg" }, Play(1), Play(3) with { Positional = true, X = 8, Y = 8, Z = 1 }));
        Assert.Equal(new[] { "sound/a.ogg", "sound/c.ogg" }, heard.Select(e => e.File));
        Assert.Equal("sound/c.ogg", replica.SoundPath(3));
        Assert.Null(replica.SoundPath(4));

        // refused, and nothing changes: a start that does not continue the table, an index past it, a tile outside the map
        heard.Clear();
        Assert.Throws<ProtocolException>(() => replica.Apply(Message(5, new[] { "sound/d.ogg" })));
        Assert.Throws<ProtocolException>(() => replica.Apply(Message(4, new[] { "sound/d.ogg" }, Play(5))));
        Assert.Throws<ProtocolException>(() => replica.Apply(Message(4, new[] { "sound/d.ogg" }, Play(1) with { Positional = true, X = 9, Y = 1, Z = 1 })));
        Assert.Throws<ProtocolException>(() => replica.Apply(Message(0, Array.Empty<string>(), Play(1) with { Positional = true, X = 1, Y = 1, Z = 2 })));
        Assert.Equal(3, replica.SoundPathCount);
        Assert.Empty(heard);

        // a MapGrid (a join) restarts the table
        replica.Apply(Grid());
        Assert.Equal(0, replica.SoundPathCount);
        Assert.Throws<ProtocolException>(() => replica.Apply(Message(0, Array.Empty<string>(), Play(1))));
        replica.Apply(Message(1, new[] { "sound/e.ogg" }, Play(1)));
        Assert.Equal("sound/e.ogg", Assert.Single(heard).File);
    }

    [Fact]
    public void SoundsAppliedBeforeAnyoneListensReachTheFirstListener()
    {
        // D296: the join tick's sounds (the area's buzz) are applied by the connect screen before the game scene listens
        var replica = new ReplicaWorld();
        replica.Apply(Grid());
        replica.Apply(Message(1, new[] { "sound/ambience/general/shipambience.ogg" }, Play(1) with { Channel = 1017, Repeat = true }));
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        Assert.Equal("sound/ambience/general/shipambience.ogg", Assert.Single(heard).File);
        // once heard, they are not handed out again; a new MapGrid drops what nobody heard
        var second = new List<SoundEvent>();
        replica.SoundsReceived += events => second.AddRange(events);
        Assert.Empty(second);

        var other = new ReplicaWorld();
        other.Apply(Grid());
        for (int i = 0; i < ReplicaWorld.MaxUnheardSounds + 10; i++)
            other.Apply(Message(i == 0 ? 1u : 0u, i == 0 ? new[] { "sound/a.ogg" } : Array.Empty<string>(), Play(1)));
        var newest = new List<SoundEvent>();
        other.SoundsReceived += events => newest.AddRange(events);
        Assert.Equal(ReplicaWorld.MaxUnheardSounds, newest.Count); // the newest kept

        var rejoined = new ReplicaWorld();
        rejoined.Apply(Grid());
        rejoined.Apply(Message(1, new[] { "sound/a.ogg" }, Play(1)));
        rejoined.Apply(Grid());
        var none = new List<SoundEvent>();
        rejoined.SoundsReceived += events => none.AddRange(events);
        Assert.Empty(none);
    }

    /// <summary>A random valid Sounds message (the fuzz corpus's).</summary>
    internal static Sounds RandomSounds(Random rng)
    {
        var m = new Sounds();
        int paths = rng.Next(0, 4);
        m.FirstPath = paths == 0 ? 0u : (uint)rng.Next(1, 100);
        for (int i = 0; i < paths; i++)
            m.NewPaths.Add("sound/" + (char)('a' + rng.Next(26)) + i + ".ogg");
        int events = rng.Next(0, 6);
        for (int i = 0; i < events; i++)
        {
            var kind = (SoundKind)rng.Next(0, 3);
            if (kind == SoundKind.Stop)
            {
                m.Events.Add(new SoundEvent { Kind = kind, Channel = (ushort)rng.Next(0, Limits.MaxSoundChannel + 1) });
                continue;
            }
            bool positional = rng.Next(2) == 0;
            m.Events.Add(new SoundEvent
            {
                Kind = kind,
                Channel = (ushort)rng.Next(kind == SoundKind.Update ? 1 : 0, Limits.MaxSoundChannel + 1),
                Path = kind == SoundKind.Play ? (uint)rng.Next(1, 200) : 0,
                Volume = (byte)rng.Next(0, Limits.MaxSoundVolume + 1),
                Muffle = (byte)rng.Next(256),
                Occlusion = (byte)rng.Next(256),
                Environment = (byte)rng.Next(0, Limits.MaxSoundEnvironment + 2),
                Repeat = rng.Next(2) == 0,
                Mute = rng.Next(3) == 0,
                Positional = positional,
                X = positional ? (byte)rng.Next(1, 256) : (byte)0,
                Y = positional ? (byte)rng.Next(1, 256) : (byte)0,
                Z = positional ? rng.Next(1, Limits.MaxMapLevels + 1) : 0,
                Frequency = rng.Next(2) == 0 ? 0f : rng.Next(32000, 55001),
                Offset = rng.Next(3) == 0 ? (float)(rng.NextDouble() * 100 + 0.1) : 0f,
            });
        }
        return m;
    }
}
