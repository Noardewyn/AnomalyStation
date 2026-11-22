using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Seeded fuzzing of every decoder (scope 2j "Codec"): random bytes, truncations, bit flips, inserted and deleted bytes,
/// oversized lengths and counts, NaN and infinity. Nothing may escape <c>TryDecode</c> (which never throws), a message that
/// decodes must encode back to the same bytes (the format is canonical), and the allocation of a decode stays within a
/// bound set by the message's size and the caps, never by what the message claims.
/// </summary>
public class CodecFuzzTests
{
    private const ulong Seed = 0x55_33_44_00_20261007UL;

    // The most a decoder may allocate for an input of this length: its lists (at most 8 bytes of capacity per input byte, a
    // count is checked against the bytes left), its strings (2 bytes per input byte) and a few exceptions.
    // version 81: a decoded change carries the free motion's arc (48 bytes more per change record than the 24 a byte allowed before)
    private static long Bound(int length) => 28L * length + 16 * 1024;

    static CodecFuzzTests()
    {
        // Warm every decoder so that the first call's one-time allocations do not count.
        foreach (byte[] message in Corpus(new Random(5), 4))
            Decode(message);
    }

    /// <summary>
    /// Decodes <paramref name="input"/> with every decoder, checks the invariants and returns whether the decoder named by
    /// the first byte accepted it.
    /// </summary>
    private static bool Decode(byte[] input)
    {
        bool matched = false;
        bool accepted = false;
        ProtocolError error;
        MessageId id = input.Length > 0 && WireReader.IsKnownMessageId(input[0]) ? (MessageId)input[0] : MessageId.None;

        {
            bool ok = Codec.TryDecode(input, out Hello value, out error);
            if (id == MessageId.Hello)
            {
                matched = true;
                accepted = ok;
                if (ok && value.Protocol == ProtocolVersion.Current)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out InputFrame value, out error);
            if (id == MessageId.InputFrame)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out Welcome value, out error);
            if (id == MessageId.Welcome)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out MapGrid value, out error);
            if (id == MessageId.MapGrid)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out Snapshot value, out error);
            if (id == MessageId.Snapshot)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out Delta value, out error);
            if (id == MessageId.Delta)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out ServerText value, out error);
            if (id == MessageId.ServerText)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out DisconnectInfo value, out error);
            if (id == MessageId.Disconnect)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out MotionInputMessage value, out error);
            if (id == MessageId.MotionInput)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out ActionMessage value, out error);
            if (id == MessageId.Action)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out ContextHintsMessage value, out error);
            if (id == MessageId.ContextHints)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out Sounds value, out error);
            if (id == MessageId.Sounds)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out RadialMenuMessage value, out error);
            if (id == MessageId.Radial)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out UiWindowMessage value, out error);
            if (id == MessageId.UiWindow)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        {
            bool ok = Codec.TryDecode(input, out TooltipMessage value, out error);
            if (id == MessageId.Tooltip)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }
        {
            bool ok = Codec.TryDecode(input, out CameraShakeMessage value, out error);
            if (id == MessageId.CameraShake)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }
        {
            bool ok = Codec.TryDecode(input, out RadioChannelsMessage value, out error); // version 40
            if (id == MessageId.RadioChannels)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }
        {
            bool ok = Codec.TryDecode(input, out MotionMessage value, out error); // version 42
            if (id == MessageId.Motion)
            {
                accepted = ok;
                if (ok)
                    Assert.Equal(input, Codec.Encode(value));
                else
                    Assert.Null(value);
            }
            else
            {
                Assert.False(ok);
            }
        }

        GC.KeepAlive(matched);
        return accepted;
    }

    /// <summary>Runs <see cref="Decode"/> and checks the allocation of the decoder that matches the first byte.</summary>
    private static bool DecodeBounded(byte[] input)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool ok = CodecTests.TryDecodeAny(input, out ProtocolError error);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated <= Bound(input.Length), "a decode of " + input.Length + " bytes allocated " + allocated + " bytes (first byte " + (input.Length > 0 ? input[0] : -1) + ", " + error + ")");
        Assert.Equal(ok, error == ProtocolError.None);
        Assert.Equal(ok, Decode(input));
        return ok;
    }

    private static string RandomString(Random rng, int maxChars)
    {
        int length = rng.Next(0, maxChars + 1);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            int kind = rng.Next(0, 10);
            chars[i] = kind < 6 ? (char)rng.Next('a', 'z' + 1) : kind < 8 ? (char)rng.Next(0x20, 0x7F) : (char)rng.Next(0x400, 0x4FF);
        }

        return new string(chars);
    }

    internal static LightState RandomLight(Random rng)
    {
        byte[] dirs = { 0, 1, 2, 4, 5, 6, 8, 9, 10 };
        return new LightState(
            (float)(rng.NextDouble() * (Limits.MaxLightRange - 0.25) + 0.25),
            (float)((rng.NextDouble() * 2 - 1) * Limits.MaxLightPower),
            (uint)rng.NextInt64(0, 1L << 32),
            rng.Next(2) == 0,
            rng.Next(3) == 0 ? 360f : (float)(rng.NextDouble() * 360),
            dirs[rng.Next(dirs.Length)],
            (float)((rng.NextDouble() * 2 - 1) * Limits.MaxLightHeight),
            (float)((rng.NextDouble() * 2 - 1) * Limits.MaxLightOffset),
            (float)((rng.NextDouble() * 2 - 1) * Limits.MaxLightOffset),
            (byte)rng.Next(1, 5));
    }

    private static List<TurfRun> RandomRuns(Random rng, int cells, int values)
    {
        var runs = new List<TurfRun>();
        while (cells > 0)
        {
            int run = Math.Min(cells, rng.Next(1, 30));
            runs.Add(new TurfRun(run, rng.Next(values)));
            cells -= run;
        }

        return runs;
    }

    private static EntityState RandomState(Random rng, long id, int typeCount, long tick, int appearances)
    {
        byte[] dirs = { 0, 1, 2, 4, 5, 6, 8, 9, 10 };
        int x = rng.Next(1, 256), y = rng.Next(1, 256);
        EntityState e = Msg.State(id, rng.Next(typeCount), x, y, rng.Next(1, 17), dirs[rng.Next(dirs.Length)],
            rng.Next(4) == 0 ? null : RandomString(rng, 12), rng.Next(3) == 0 ? 0f : (float)(rng.NextDouble() * 32), rng.NextInt64(0, tick + 1));
        e = e with { Posture = rng.Next(3) == 0 ? (byte)rng.Next(1, 8) : (byte)0, Appearance = TableRef.Public((uint)rng.Next(0, appearances + 1)) };
        if (rng.Next(3) == 0)
            e = e with { HasPos = true, PosX = x * 256 + rng.Next(-127, 128), PosY = y * 256 + rng.Next(-127, 128), Yaw = (byte)rng.Next(256) };
        if (rng.Next(3) == 0)
            e = e with { Light = RandomLight(rng) };
        if (rng.Next(3) == 0)
            e = e with { Flags = EntityFlags.Thrown };
        return e;
    }

    private static readonly NetField[] ChangeableFields =
    {
        NetField.X, NetField.Y, NetField.Z, NetField.Dir, NetField.Name, NetField.GlideSize, NetField.LastMoveTick,
        NetField.Pos, NetField.Yaw, NetField.Posture, NetField.Appearance, NetField.Light, NetField.Flags,
    };

    private static EntityChange RandomChange(Random rng, long id, long tick)
    {
        byte[] dirs = { 0, 1, 2, 4, 5, 6, 8, 9, 10 };
        var mask = NetField.None;
        while (mask == NetField.None)
            foreach (NetField f in ChangeableFields)
                if (rng.Next(3) == 0) mask |= f;
        int x = rng.Next(1, 256), y = rng.Next(1, 256);
        return new EntityChange
        {
            Id = id,
            Mask = mask,
            X = x,
            Y = y,
            Z = rng.Next(1, 17),
            Dir = dirs[rng.Next(dirs.Length)],
            Name = rng.Next(4) == 0 ? null : RandomString(rng, 12),
            GlideSize = (float)(rng.NextDouble() * 32),
            LastMoveTick = rng.NextInt64(0, tick + 1),
            PosX = x * 256 + rng.Next(-127, 128),
            PosY = y * 256 + rng.Next(-127, 128),
            Yaw = (byte)rng.Next(256),
            Posture = (byte)rng.Next(8),
            Appearance = TableRef.Public((uint)rng.Next(0, 5000)),
            Light = rng.Next(4) == 0 ? LightState.None : RandomLight(rng),
            Flags = rng.Next(2) == 0 ? EntityFlags.Thrown : EntityFlags.None,
        };
    }

    private static ContextHintsMessage RandomHints(Random rng)
    {
        var message = new ContextHintsMessage { Name = rng.Next(4) == 0 ? null : RandomString(rng, 30) };
        switch (rng.Next(3))
        {
            case 0:
                message.TargetKind = TargetKind.Entity;
                message.Target = rng.Next(1, 100000);
                break;
            case 1:
                message.TargetKind = TargetKind.Screen;
                message.Target = rng.Next(1, 100000);
                break;
            default:
                message.TargetKind = TargetKind.Turf;
                message.TurfX = rng.Next(1, 256);
                message.TurfY = rng.Next(1, 256);
                message.TurfZ = rng.Next(1, 17);
                break;
        }

        for (int i = rng.Next(0, Limits.MaxContextHints + 1); i > 0; i--)
            message.Hints.Add(new ContextHint((ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), RandomString(rng, 20)));
        return message;
    }

    private static AppearanceEntry RandomAppearance(Random rng, bool isPrivate, uint ownIndex)
    {
        var e = new AppearanceEntry();
        if (rng.Next(2) == 0) e.Icon = new TableRef((uint)rng.Next(1, 500), isPrivate && rng.Next(2) == 0);
        if (rng.Next(2) == 0) e.IconState = new TableRef((uint)rng.Next(1, 5000), isPrivate && rng.Next(2) == 0);
        if (rng.Next(3) == 0) e.Dir = new byte[] { 1, 2, 4, 8, 5, 6, 9, 10 }[rng.Next(8)];
        if (rng.Next(3) == 0) e.Rgba = (uint)rng.NextInt64(0, 0xFFFFFFFE);
        if (rng.Next(8) == 0) e.ColorMatrix = Enumerable.Range(0, 20).Select(_ => (float)(rng.NextDouble() * 2 - 1)).ToArray();
        if (rng.Next(3) == 0) e.Alpha = (byte)rng.Next(0, 255);
        if (rng.Next(2) == 0) e.Layer = (float)(rng.NextDouble() * 10 - 5) + 0.5f;
        if (rng.Next(3) == 0) e.Plane = (short)rng.Next(-32768, 32768);
        if (rng.Next(3) == 0) { e.PixelX = (short)rng.Next(-32, 33); e.PixelY = (short)rng.Next(-32, 33); }
        if (rng.Next(6) == 0) { e.PixelW = (short)rng.Next(1, 33); e.PixelZ = (short)rng.Next(-32, 33); }
        if (rng.Next(4) == 0) e.Transform = Enumerable.Range(0, 6).Select(_ => (float)(rng.NextDouble() * 2 - 1)).ToArray();
        if (rng.Next(3) == 0) e.AppearanceFlags = (ushort)rng.Next(1, 65536);
        if (rng.Next(6) == 0) e.BlendMode = (byte)rng.Next(1, 6);
        if (isPrivate && rng.Next(5) == 0)
        {
            e.Maptext = new TableRef((uint)rng.Next(1, 4), rng.Next(2) == 0);
            e.MaptextWidth = (short)rng.Next(0, 600);
            e.MaptextHeight = (short)rng.Next(0, 600);
            e.MaptextX = (short)rng.Next(-40, 41);
            e.MaptextY = (short)rng.Next(-40, 41);
        }
        if (ownIndex > 1)
        {
            for (int i = rng.Next(0, 4); i > 0; i--) e.Overlays.Add(new TableRef((uint)rng.Next(1, (int)ownIndex), isPrivate));
            for (int i = rng.Next(0, 2); i > 0; i--) e.Underlays.Add(new TableRef((uint)rng.Next(1, (int)ownIndex), isPrivate));
        }

        return e;
    }

    private static TableRef ScreenRef(Random rng)
    {
        uint index = (uint)rng.Next(0, 40);
        return new TableRef(index, index != 0 && rng.Next(2) == 0);
    }

    private static OwnBlock RandomOwn(Random rng)
    {
        var own = new OwnBlock { InputBufferDepth = (byte)rng.Next(0, 4) };
        if (rng.Next(3) != 0)
        {
            own.HasMotion = true;
            own.Motion = new MotionState
            {
                X = rng.Next(128, 255 * 256), Y = rng.Next(128, 255 * 256), Z = rng.Next(1, 17), Yaw = (ushort)rng.Next(65536),
                Posture = (Posture)rng.Next(2), Driver = (MotionDriver)rng.Next(4), PressTimer = (short)rng.Next(-5, 10),
                VelX = rng.Next(-40000, 40001), VelY = rng.Next(-40000, 40001),
            };
            if (rng.Next(3) == 0) own.Motion = own.Motion with { Slide = new Slide(rng.Next(128, 255 * 256), rng.Next(128, 255 * 256), (ushort)rng.Next(1, 20)) };
            own.Params = new MotionParams((float)(rng.NextDouble() * 2), (float)(rng.NextDouble() * 0.5), rng.Next(2) == 0, (short)rng.Next(0, 10),
                Feel: new MoveFeel((short)rng.Next(0, MoveFeel.MaxTicks + 1), (short)rng.Next(0, MoveFeel.MaxTicks + 1), (float)rng.NextDouble() * MoveFeel.MaxSlow, (float)rng.NextDouble() * MoveFeel.MaxSlow));
        }

        if (rng.Next(2) == 0)
        {
            own.FirstNewPrivateString = (uint)rng.Next(1, 10);
            for (int i = rng.Next(0, 4); i > 0; i--) own.NewPrivateStrings.Add(RandomString(rng, 8));
            own.FirstNewPrivateResource = (uint)rng.Next(1, 10);
            for (int i = rng.Next(0, 3); i > 0; i--) own.NewPrivateResources.Add("icons/" + RandomString(rng, 10));
            own.FirstNewPrivateAppearance = (uint)rng.Next(1, 10);
            for (int i = rng.Next(0, 4); i > 0; i--) own.NewPrivateAppearances.Add(RandomAppearance(rng, true, own.FirstNewPrivateAppearance + (uint)own.NewPrivateAppearances.Count));
        }

        long id = 0;
        foreach (List<ScreenEntry> list in new[] { own.ScreenAdded, own.ScreenChanged })
        {
            id = 0;
            for (int i = rng.Next(0, 4); i > 0; i--)
            {
                id += rng.Next(1, 30);
                list.Add(new ScreenEntry(id, ScreenRef(rng), rng.Next(4) == 0 ? null : "WEST+" + rng.Next(5) + ",CENTER")
                {
                    Group = (byte)rng.Next(0, Limits.MaxHudGroup + 1),
                    // version 12: the clickable vis_contents children (the item in a hand or a slot)
                    VisChildren = rng.Next(3) != 0 ? null : new ScreenChildren
                    {
                        Items = Enumerable.Range(0, rng.Next(1, 3)).Select(_ => new ScreenChild(rng.Next(1, 500000), new TableRef((uint)rng.Next(1, 500), rng.Next(2) == 0), rng.Next(4) == 0)).ToArray(),
                    },
                    ActionTarget = rng.Next(3) == 0 ? rng.Next(1, 500000) : 0, // version 27: an action button's item
                });
            }
        }

        id = 0;
        for (int i = rng.Next(0, 3); i > 0; i--) own.ScreenRemoved.Add(id += rng.Next(1, 30));
        foreach (List<ImageEntry> list in new[] { own.ImagesAdded, own.ImagesChanged })
        {
            id = 0;
            for (int i = rng.Next(0, 3); i > 0; i--)
            {
                id += rng.Next(1, 30);
                list.Add(new ImageEntry(id, new TableRef((uint)rng.Next(1, 500), rng.Next(2) == 0), rng.Next(3) == 0 ? 0 : rng.Next(1, 100000))
                {
                    Holder = rng.Next(3) == 0 ? 0 : rng.Next(1, 100000), // version 27: the holder and its name
                    HolderName = rng.Next(3) == 0 ? null : "tank " + rng.Next(100),
                });
            }
        }

        id = 0;
        for (int i = rng.Next(0, 3); i > 0; i--) own.ImagesRemoved.Add(id += rng.Next(1, 30));
        return own;
    }

    /// <summary>Valid messages of every kind, seeded.</summary>
    private static List<byte[]> Corpus(Random rng, int rounds)
    {
        var list = new List<byte[]>();
        for (int round = 0; round < rounds; round++)
        {
            int typeCount = rng.Next(1, 6);
            string[] types = Enumerable.Range(0, typeCount).Select(i => "/type/" + RandomString(rng, 10) + i).ToArray();
            SessionToken token = rng.Next(2) == 0 ? SessionToken.None : new SessionToken((ulong)rng.NextInt64() | 1, (ulong)rng.NextInt64());
            list.Add(Codec.Encode(new Hello(ProtocolVersion.Current, RandomString(rng, Limits.MaxNameChars), token)));
            list.Add(Codec.Encode(new InputFrame((uint)rng.NextInt64(0, uint.MaxValue), (HeldDirs)rng.Next(0, 16))));
            list.Add(Codec.Encode(new Welcome(rng.Next(0, 1000), token, rng.NextInt64(0, 1L << 40), (byte)rng.Next(1, 255), rng.NextInt64(0, 1L << 30))));
            list.Add(Codec.Encode(new ServerText(rng.Next(3) == 0 ? null : RandomString(rng, 20), RandomString(rng, 200))));
            list.Add(Codec.Encode(new DisconnectInfo((DisconnectReason)rng.Next(0, 15), rng.Next(3) == 0 ? null : RandomString(rng, 100))));
            var radio = new RadioChannelsMessage(); // version 40
            foreach (char key in "emcs".Take(rng.Next(0, 5)))
                radio.Prefixes.Add(":" + key + " ");
            list.Add(Codec.Encode(radio));
            var motion = new MotionMessage { Tick = rng.NextInt64(0, 1L << 40) }; // version 42
            long motionId = 0;
            for (int i = rng.Next(0, 20); i > 0; i--)
            {
                motionId += rng.Next(1, 5000);
                motion.Entries.Add(new MotionEntry(motionId, rng.Next(256, 200 * 256), rng.Next(256, 200 * 256), (byte)rng.Next(256)));
            }
            list.Add(Codec.Encode(motion));

            int w = rng.Next(1, 40), h = rng.Next(1, 40), l = rng.Next(1, 4);
            var grid = new MapGrid { Tick = rng.Next(1000), Width = w, Height = h, Levels = l };
            grid.TurfTypes.AddRange(types);
            int cells = w * h * l;
            while (cells > 0)
            {
                int run = Math.Min(cells, rng.Next(1, 30));
                grid.Runs.Add(new TurfRun(run, rng.Next(typeCount)));
                cells -= run;
            }

            list.Add(Codec.Encode(grid));

            grid.AppearanceRuns.Add(new TurfRun(w * h * l, rng.Next(0, 50)));
            if (rng.Next(2) == 0)
            {
                grid.AppearanceRuns.Clear();
                int left = w * h * l;
                while (left > 0)
                {
                    int run = Math.Min(left, rng.Next(1, 30));
                    grid.AppearanceRuns.Add(new TurfRun(run, rng.Next(0, 50)));
                    left -= run;
                }
            }

            int cellCount = w * h * l;
            grid.FlagRuns.AddRange(RandomRuns(rng, cellCount, CellFlags.All + 1));
            for (int i = rng.Next(0, 5); i > 0; i--) grid.BaseLights.Add(new BaseLight((uint)rng.NextInt64(0, 1L << 32)));
            grid.BaseLightRuns.AddRange(RandomRuns(rng, cellCount, grid.BaseLights.Count + 1));
            if (rng.Next(2) == 0) grid.Starlight = RandomLight(rng);
            grid.SolidBits = new byte[(cellCount + 7) / 8];
            rng.NextBytes(grid.SolidBits);
            if (cellCount % 8 != 0) grid.SolidBits[^1] &= (byte)((1 << (cellCount % 8)) - 1);
            list.Add(Codec.Encode(grid));

            long snapshotTick = rng.Next(100000);
            var snapshot = new Snapshot { Tick = snapshotTick, LastInputSeq = (uint)rng.Next(1000), OwnEntity = rng.Next(0, 50) };
            snapshot.Types.AddRange(types);
            for (int i = rng.Next(0, 4); i > 0; i--) snapshot.Resources.Add("icons/" + RandomString(rng, 10) + ".dmi");
            for (int i = rng.Next(0, 5); i > 0; i--) snapshot.Strings.Add(RandomString(rng, 8));
            for (int i = rng.Next(0, 6); i > 0; i--) snapshot.Appearances.Add(RandomAppearance(rng, false, (uint)snapshot.Appearances.Count + 1));
            long id = 0;
            for (int i = rng.Next(0, 12); i > 0; i--)
            {
                id += rng.Next(1, 20);
                snapshot.Entities.Add(RandomState(rng, id, typeCount, snapshotTick, snapshot.Appearances.Count));
            }

            if (rng.Next(2) == 0)
            {
                snapshot.Own = RandomOwn(rng);
            }

            list.Add(Codec.Encode(snapshot));

            long tick = rng.Next(100000);
            var delta = new Delta { Tick = tick, LastInputSeq = (uint)rng.Next(1000), OwnEntity = rng.Next(0, 50), FirstNewType = rng.Next(0, 10) };
            delta.NewTypes.AddRange(types.Take(rng.Next(0, typeCount + 1)));
            if (rng.Next(2) == 0)
            {
                delta.FirstNewResource = (uint)rng.Next(1, 20);
                for (int i = rng.Next(1, 3); i > 0; i--) delta.NewResources.Add("icons/" + RandomString(rng, 10));
            }

            if (rng.Next(2) == 0)
            {
                delta.FirstNewString = (uint)rng.Next(1, 20);
                for (int i = rng.Next(1, 4); i > 0; i--) delta.NewStrings.Add(RandomString(rng, 8));
            }

            if (rng.Next(2) == 0)
            {
                delta.FirstNewAppearance = (uint)rng.Next(1, 20);
                for (int i = rng.Next(1, 5); i > 0; i--) delta.NewAppearances.Add(RandomAppearance(rng, false, delta.FirstNewAppearance + (uint)delta.NewAppearances.Count));
            }

            for (int i = rng.Next(0, 6); i > 0; i--)
                delta.Spawned.Add(RandomState(rng, rng.Next(1, 500) + i * 1000, 1 + delta.FirstNewType, tick, 30));
            // Spawned ids from the loop above are not in a sorted order on purpose; make sure neighbours differ.
            for (int i = 1; i < delta.Spawned.Count; i++)
                if (delta.Spawned[i].Id == delta.Spawned[i - 1].Id)
                    delta.Spawned[i] = delta.Spawned[i] with { Id = delta.Spawned[i].Id + 1 };
            for (int i = rng.Next(0, 8); i > 0; i--)
                delta.Changed.Add(RandomChange(rng, rng.Next(1, 500) + i * 600, tick));
            for (int i = rng.Next(0, 6); i > 0; i--)
                delta.Despawned.Add(rng.Next(1, 500) + i * 700);
            for (int i = rng.Next(0, 3); i > 0; i--)
                delta.SolidChanged.Add(new CellSolidChange(rng.Next(1, 256), rng.Next(1, 256), rng.Next(1, 17), rng.Next(2) == 0));
            for (int i = rng.Next(0, 3); i > 0; i--)
                delta.TurfAppearanceChanged.Add(new CellAppearanceChange(rng.Next(1, 256), rng.Next(1, 256), rng.Next(1, 17), TableRef.Public((uint)rng.Next(0, 30))));
            for (int i = rng.Next(0, 3); i > 0; i--)
                delta.CellFlagsChanged.Add(new CellFlagsChange(rng.Next(1, 256), rng.Next(1, 256), rng.Next(1, 17), (byte)rng.Next(CellFlags.All + 1)));
            if (rng.Next(3) == 0)
            {
                delta.FirstNewBaseLight = (uint)rng.Next(1, 40);
                for (int i = rng.Next(1, 4); i > 0; i--) delta.NewBaseLights.Add(new BaseLight((uint)rng.NextInt64(0, 1L << 32)));
            }

            for (int i = rng.Next(0, 3); i > 0; i--)
                delta.BaseLightChanged.Add(new CellBaseLightChange(rng.Next(1, 256), rng.Next(1, 256), rng.Next(1, 17), (ushort)rng.Next(0, 50)));
            if (rng.Next(3) == 0)
            {
                delta.FirstNewTurfType = rng.Next(0, 40);
                for (int i = rng.Next(1, 4); i > 0; i--) delta.NewTurfTypes.Add("/turf/" + RandomString(rng, 10));
            }

            for (int i = rng.Next(0, 3); i > 0; i--)
                delta.TurfTypeChanged.Add(new CellTurfTypeChange(rng.Next(1, 256), rng.Next(1, 256), rng.Next(1, 17), rng.Next(0, 80)));
            if (rng.Next(3) == 0)
                delta.StarlightChanged = rng.Next(4) == 0 ? LightState.None : RandomLight(rng);
            if (rng.Next(2) == 0)
                delta.Own = RandomOwn(rng);
            list.Add(Codec.Encode(delta));

            list.Add(Codec.Encode(CodecV2Tests.Input((uint)rng.Next(4, 100000), rng.Next(1, 5))));
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Click, rng.Next(1, 100000), (ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), null)
            {
                TargetKind = rng.Next(2) == 0 ? TargetKind.Entity : TargetKind.Screen,
                IconX = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
                IconY = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            }));
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Click, 0, (ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), null)
            {
                TargetKind = TargetKind.Turf,
                TurfX = rng.Next(1, 256),
                TurfY = rng.Next(1, 256),
                TurfZ = rng.Next(1, 17),
                IconX = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
                IconY = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            }));
            list.Add(Codec.Encode(rng.Next(2) == 0
                ? new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Hover, rng.Next(1, 100000), ClickButton.None, ClickModifiers.None, null) { TargetKind = rng.Next(2) == 0 ? TargetKind.Entity : TargetKind.Screen }
                : new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = rng.Next(1, 256), TurfY = rng.Next(1, 256), TurfZ = rng.Next(1, 17) }));
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null)
            {
                Keybinding = (KeybindingCode)rng.Next((int)KeybindingCode.SwapHands, (int)KeybindingCode.Last + 1),
                KeyUp = rng.Next(2) == 0,
            }));
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Unhover, 0, ClickButton.None, ClickModifiers.None, null))); // version 14
            list.Add(Codec.Encode(RandomHints(rng)));
            list.Add(Codec.Encode(Anomaly.Server.Tests.M4b.ProtocolV13Tests.RandomSounds(rng)));
            list.Add(Codec.Encode(ProtocolV15Tests.RandomRadial(rng))); // version 15
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.RadialPick, 0, ClickButton.None, ClickModifiers.None, null)
            {
                RadialMenu = rng.Next(1, 1_000_000),
                RadialElement = rng.Next(0, Limits.MaxRadialElements + 1),
            }));
            list.Add(Codec.Encode(ProtocolV16Tests.RandomWindow(rng))); // version 16
            list.Add(Codec.Encode(ProtocolV16Tests.RandomUiAction(rng)));
            list.Add(Codec.Encode(ProtocolV17Tests.RandomDrop(rng))); // version 17
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, RandomString(rng, 40))));
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), (ActionKind)rng.Next(1, 4), 0, ClickButton.None, ClickModifiers.None, null)));

            // Version 19: the tooltip shown (tg's HTML text and a theme) and hidden.
            list.Add(Codec.Encode(new TooltipMessage { Kind = TooltipKind.Show, Text = "<h1>" + RandomString(rng, 30) + "</h1><p>" + RandomString(rng, 200) + "</p>", Theme = rng.Next(2) == 0 ? "default" : "trasen-knox" }));
            list.Add(Codec.Encode(new TooltipMessage { Kind = TooltipKind.Hide }));

            // Version 21: a camera shake path.
            var shake = new CameraShakeMessage();
            for (int k = rng.Next(1, 40); k > 0; k--)
                shake.Steps.Add(new CameraShakeStep(rng.Next(-64, 65), rng.Next(-64, 65), rng.Next(1, 20)));
            shake.Kind = (CameraShakeKind)rng.Next(0, 2); // version 34: a recoil kick
            list.Add(Codec.Encode(shake));

            // Version 34: an aimed click, the mouse actions, a shot's line on an entity.
            list.Add(Codec.Encode(new ActionMessage((uint)rng.Next(0, 100000), ActionKind.Click, rng.Next(1, 100000), (ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), null)
            {
                TargetKind = TargetKind.Entity,
                IconX = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
                IconY = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
                AimZone = (AimZone)rng.Next(0, (int)AimZone.Last + 1),
                Aim = ProtocolV34Tests.RandomAim(rng),
            }));
            list.Add(Codec.Encode(ProtocolV34Tests.RandomMouse(rng)));
            list.Add(Codec.Encode(Msg.Snapshot(rng.Next(10, 1000), 0, 0, new[] { "/obj/projectile" }, Msg.State(rng.Next(1, 1000), 0, rng.Next(1, 256), rng.Next(1, 256), 1) with { Shot = ProtocolV34Tests.RandomShot(rng) })));
        }

        return list;
    }

    [Fact]
    public void TheCorpusDecodesAndEncodesBackToTheSameBytes()
    {
        foreach (byte[] message in Corpus(new Random(7), 20))
            Assert.True(DecodeBounded(message), "a corpus message " + (MessageId)message[0] + " does not decode");
    }

    [Fact]
    public void RandomBytesNeverEscapeAndNeverOverAllocate()
    {
        var rng = new Random(unchecked((int)Seed));
        var buffer = new byte[600];
        for (int i = 0; i < 100_000; i++)
        {
            int length = rng.Next(0, 400);
            byte[] input = new byte[length];
            rng.NextBytes(input);
            if (length > 0 && rng.Next(4) != 0)
                input[0] = (byte)rng.Next(1, 12);
            DecodeBounded(input);
        }

        // Valid message ids with random bodies of structured bytes (small values, so counts and varints are plausible).
        for (int id = 1; id <= 11; id++)
        {
            for (int i = 0; i < 20_000; i++)
            {
                int length = rng.Next(1, 120);
                byte[] input = new byte[length];
                for (int j = 0; j < length; j++)
                    input[j] = rng.Next(3) == 0 ? (byte)rng.Next(256) : (byte)rng.Next(0, 6);
                input[0] = (byte)id;
                DecodeBounded(input);
            }
        }

        GC.KeepAlive(buffer);
    }

    [Fact]
    public void TruncationsAtEveryOffsetAreRefused()
    {
        foreach (byte[] message in Corpus(new Random(11), 6))
        {
            for (int length = 0; length < message.Length; length++)
            {
                bool ok = DecodeBounded(message.AsSpan(0, length).ToArray());
                // A Hello of another version could decode early, a current one never does.
                Assert.False(ok, "a prefix of " + length + " of " + message.Length + " bytes of a " + (MessageId)message[0] + " decoded");
            }
        }
    }

    [Fact]
    public void BitFlipsNeverEscapeAndAcceptedOnesAreStillCanonical()
    {
        int accepted = 0;
        int total = 0;
        foreach (byte[] message in Corpus(new Random(13), 8))
        {
            // Every single bit of the shorter messages; a seeded sample of the longer.
            var rng = new Random(message.Length);
            int bits = message.Length * 8;
            IEnumerable<int> positions = bits <= 4000 ? Enumerable.Range(0, bits) : Enumerable.Range(0, 4000).Select(_ => rng.Next(bits));
            foreach (int bit in positions)
            {
                byte[] copy = (byte[])message.Clone();
                copy[bit / 8] ^= (byte)(1 << (bit % 8));
                total++;
                if (DecodeBounded(copy))
                    accepted++;
            }
        }

        Assert.True(total > 20000);
        Assert.True(accepted > 0, "no bit flip survived: the corpus is too fragile to test the canonical property");
    }

    [Fact]
    public void InsertedDeletedAndOverwrittenBytesNeverEscape()
    {
        var rng = new Random(17);
        int[] interesting = { 0, 1, 2, 0x7F, 0x80, 0xFF, 0xC0, 0xF0 };
        foreach (byte[] message in Corpus(new Random(19), 8))
        {
            for (int n = 0; n < 1500; n++)
            {
                byte[] copy;
                int at = rng.Next(0, message.Length);
                switch (rng.Next(4))
                {
                    case 0: // delete a run
                        int count = rng.Next(1, 6);
                        copy = Splice(message, at, count, Array.Empty<byte>());
                        break;
                    case 1: // insert a run
                        copy = Splice(message, at, 0, Enumerable.Range(0, rng.Next(1, 6)).Select(_ => (byte)interesting[rng.Next(interesting.Length)]).ToArray());
                        break;
                    case 2: // overwrite one byte with an interesting value
                        copy = (byte[])message.Clone();
                        copy[at] = (byte)interesting[rng.Next(interesting.Length)];
                        break;
                    default: // overwrite with a forged huge varint
                        copy = (byte[])message.Clone();
                        byte[] huge = rng.Next(2) == 0 ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F } : new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
                        for (int i = 0; i < huge.Length && at + i < copy.Length; i++)
                            copy[at + i] = huge[i];
                        break;
                }

                DecodeBounded(copy);
            }
        }
    }

    /// <summary>The message with <paramref name="remove"/> bytes at <paramref name="at"/> replaced by <paramref name="insert"/>.</summary>
    private static byte[] Splice(byte[] message, int at, int remove, byte[] insert)
    {
        remove = Math.Min(remove, message.Length - at);
        var result = new byte[message.Length - remove + insert.Length];
        Array.Copy(message, 0, result, 0, at);
        Array.Copy(insert, 0, result, at, insert.Length);
        Array.Copy(message, at + remove, result, at + insert.Length, message.Length - at - remove);
        return result;
    }

    [Fact]
    public void AForgedHugeVarintAtEveryOffsetNeverAllocatesPastTheBound()
    {
        foreach (byte[] message in Corpus(new Random(23), 3))
        {
            for (int at = 1; at < message.Length; at++)
            {
                foreach (byte[] huge in new[] { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x01 } })
                {
                    byte[] copy = (byte[])message.Clone();
                    for (int i = 0; i < huge.Length && at + i < copy.Length; i++)
                        copy[at + i] = huge[i];
                    DecodeBounded(copy);
                }
            }
        }
    }

    [Fact]
    public void NaNAndInfinityAtEveryOffsetAreRefusedWhereAFloatIs()
    {
        uint[] bad = { 0x7FC00000, 0xFFC00000, 0x7F800000, 0xFF800000, 0x7F800001, 0x7FFFFFFF };
        foreach (byte[] message in Corpus(new Random(29), 4))
        {
            for (int at = 1; at + 4 <= message.Length; at++)
            {
                foreach (uint pattern in bad)
                {
                    byte[] copy = (byte[])message.Clone();
                    BitConverter.TryWriteBytes(copy.AsSpan(at, 4), pattern);
                    if (DecodeBounded(copy))
                    {
                        // Accepted only when the four bytes were not a float: whatever decoded has finite floats by construction.
                        CheckFinite(copy);
                    }
                }
            }
        }
    }

    private static void CheckFinite(byte[] message)
    {
        if (Codec.TryDecode(message, out Snapshot s, out _))
            Assert.All(s.Entities, e => Assert.True(float.IsFinite(e.GlideSize)));
        if (Codec.TryDecode(message, out Delta d, out _))
        {
            Assert.All(d.Spawned, e => Assert.True(float.IsFinite(e.GlideSize)));
            Assert.All(d.Changed, c => Assert.True(float.IsFinite(c.GlideSize)));
        }
    }

    [Fact]
    public void ANaNGlideInAnEntityRecordIsRefusedAsBadValue()
    {
        byte[] good = Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(1).String("/mob").VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1)
            .VarInt(1).VarUInt(0).VarUInt(1).VarUInt(1).VarUInt(1).Byte(2).String("").Single(1.5f).VarUInt(0).Byte(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0));
        Assert.True(Codec.TryDecode(good, out Snapshot _, out _));
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            byte[] craft = Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(1).String("/mob").VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1)
                .VarInt(1).VarUInt(0).VarUInt(1).VarUInt(1).VarUInt(1).Byte(2).String("").Single(bad).VarUInt(0).Byte(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0));
            Assert.False(Codec.TryDecode(craft, out Snapshot _, out ProtocolError error));
            Assert.Equal(ProtocolError.BadValue, error);
        }
    }

    [Fact]
    public void ForgedCountsAndLengthsAreRefusedBeforeAnythingIsAllocated()
    {
        // Each message claims a list or a string far larger than its few bytes: the refusal must come from the check
        // against the bytes left, with almost no allocation.
        var cases = new List<(string name, byte[] bytes)>
        {
            ("snapshot entities 2^18", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxEntitiesPerMessage))),
            ("snapshot types 2^16", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxTypeTableEntries))),
            ("snapshot entities 2^40", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1UL << 40))),
            ("snapshot resources 2^16", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxResourceTableEntries))),
            ("snapshot strings 2^20", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxStringTableEntries))),
            ("snapshot appearances 2^22", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxAppearanceTableEntries))),
            ("snapshot appearances 2^40", Msg.Raw(MessageId.Snapshot, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1UL << 40))),
            ("delta new resources 2^16", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(1).VarUInt(Limits.MaxResourceTableEntries))),
            ("delta new appearances 2^22", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(4).VarUInt(Limits.MaxAppearanceTableEntries))),
            ("delta solid changes 64K", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(8).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxCellChangesPerDelta))),
            ("delta turf changes 2^40", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(16).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1UL << 40))),
            ("private block screen entries", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(4).Byte(0).VarUInt(Limits.MaxScreenEntries))),
            ("private block strings 2^20", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(2).Byte(0).VarUInt(Limits.MaxStringTableEntries))),
            ("map appearance runs 1M", Msg.Raw(MessageId.MapGrid, r => r.VarUInt(0).VarUInt(255).VarUInt(255).VarUInt(16).VarUInt(1).String("/t").VarUInt(1).VarUInt(255 * 255 * 16).VarUInt(0).VarUInt(255 * 255 * 16))),
            ("map solid bits 2^40", Msg.Raw(MessageId.MapGrid, r => r.VarUInt(0).VarUInt(255).VarUInt(255).VarUInt(16).VarUInt(1).String("/t").VarUInt(1).VarUInt(255 * 255 * 16).VarUInt(0).VarUInt(0).VarUInt(1UL << 40))),
            ("delta spawned 2^18", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(Limits.MaxEntitiesPerMessage))),
            ("delta changed 2^18", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(Limits.MaxEntitiesPerMessage))),
            ("delta despawned 2^18", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxEntitiesPerMessage))),
            ("delta new types 2^16", Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(Limits.MaxTypeTableEntries))),
            ("map types 2^16", Msg.Raw(MessageId.MapGrid, r => r.VarUInt(0).VarUInt(255).VarUInt(255).VarUInt(16).VarUInt(Limits.MaxTypeTableEntries))),
            ("map runs 1M", Msg.Raw(MessageId.MapGrid, r => r.VarUInt(0).VarUInt(255).VarUInt(255).VarUInt(16).VarUInt(1).String("/t").VarUInt(255 * 255 * 16))),
            ("string 2^30", Msg.Raw(MessageId.ServerText, r => r.VarUInt(1UL << 30))),
            ("string 16 MiB", Msg.Raw(MessageId.ServerText, r => r.Byte(0).VarUInt(16 * 1024 * 1024))),
            ("string at its cap but no bytes", Msg.Raw(MessageId.ServerText, r => r.Byte(0).VarUInt(Limits.MaxServerTextBytes + 1))),
            ("hello name over cap", Msg.Raw(MessageId.Hello, r => r.Bytes((byte)'A', (byte)'N', (byte)'O', (byte)'M', (byte)ProtocolVersion.Current, 0).VarUInt(1UL << 31))),
            ("disconnect text", Msg.Raw(MessageId.Disconnect, r => r.Byte(1).VarUInt(1UL << 40))),
        };
        foreach (var (name, bytes) in cases)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool ok = CodecTests.TryDecodeAny(bytes, out ProtocolError error);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.False(ok, name);
            Assert.True(error == ProtocolError.CountTooLarge || error == ProtocolError.BadString || error == ProtocolError.Truncated
                || (name.StartsWith("map solid", StringComparison.Ordinal) && error == ProtocolError.BadValue), name + ": " + error);
            Assert.True(allocated < 4096, name + " allocated " + allocated + " bytes");
        }
    }

    [Fact]
    public void ALargeValidMessageAllocatesInProportionToItsSize()
    {
        var s = new Snapshot { Tick = 200_000, OwnEntity = 1 };
        s.Types.AddRange(new[] { "/mob", "/obj" });
        var rng = new Random(31);
        for (int i = 1; i <= 100_000; i++)
            s.Entities.Add(Msg.State(i, i & 1, 1 + i % 255, 1 + i % 200, 1, 2, rng.Next(2) == 0 ? "" : "name " + i, 0f, i));
        byte[] bytes = Codec.Encode(s);
        Codec.TryDecode(bytes, out Snapshot _, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(Codec.TryDecode(bytes, out Snapshot decoded, out _));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(100_000, decoded.Entities.Count);
        Assert.True(allocated <= Bound(bytes.Length), allocated + " bytes for a message of " + bytes.Length);
    }

    [Fact]
    public void ALargeAppearanceTableAllocatesInProportionToItsSize()
    {
        // A table of many entries (the cost per entry is an object and its overlay list, set against the bytes it took).
        var rng = new Random(33);
        var s = new Snapshot { Tick = 5, OwnEntity = 0 };
        for (int i = 1; i <= 20_000; i++)
            s.Appearances.Add(RandomAppearance(rng, false, (uint)i));
        byte[] bytes = Codec.Encode(s);
        Codec.TryDecode(bytes, out Snapshot _, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(Codec.TryDecode(bytes, out Snapshot decoded, out _));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(20_000, decoded.Appearances.Count);
        Assert.True(allocated <= Bound(bytes.Length), allocated + " bytes for a message of " + bytes.Length);
    }

    [Fact]
    public void AnAppearanceTableOfEmptyEntriesIsTheWorstCaseAndStaysUnder130BytesPerInputByte()
    {
        // The cheapest entry to send, one byte, is the dearest to decode: an object each (about 100 bytes), hence the cap on the table.
        var s = new Snapshot { Tick = 5 };
        for (int i = 0; i < Limits.MaxAppearanceTableEntries; i++)
            s.Appearances.Add(new AppearanceEntry());
        byte[] bytes = Codec.Encode(s);
        Codec.TryDecode(bytes, out Snapshot _, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(Codec.TryDecode(bytes, out Snapshot decoded, out _));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Limits.MaxAppearanceTableEntries, decoded.Appearances.Count);
        Assert.True(allocated <= 130L * bytes.Length + 16 * 1024, allocated + " bytes for a message of " + bytes.Length);
        s.Appearances.Add(new AppearanceEntry());
        Assert.Throws<ProtocolException>(() => Codec.Encode(s));
    }

    [Fact]
    public void DecodersAreDeterministic()
    {
        foreach (byte[] message in Corpus(new Random(37), 5))
        {
            bool a = CodecTests.TryDecodeAny(message, out ProtocolError ea);
            bool b = CodecTests.TryDecodeAny(message, out ProtocolError eb);
            Assert.Equal(a, b);
            Assert.Equal(ea, eb);
        }
    }
}
