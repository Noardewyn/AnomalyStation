using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Builders and comparers for the protocol tests.</summary>
internal static class Msg
{
    /// <summary>The fields of protocol version 1's change mask.</summary>
    public const NetField Version1Fields = NetField.X | NetField.Y | NetField.Z | NetField.Dir | NetField.Name | NetField.GlideSize | NetField.LastMoveTick;

    /// <summary>An entity record as the decoder gives it: the position of a non-capsule is its tile's centre.</summary>
    public static EntityState State(long id, int type = 0, int x = 1, int y = 1, int z = 1, byte dir = 2, string name = "", float glide = 0f, long lastMove = 0, EntityFlags flagsOf = EntityFlags.None) =>
        new(id, type, x, y, z, dir, name, glide, lastMove) { PosX = x * 256, PosY = y * 256, Flags = flagsOf };

    public static EntityChange Move(long id, int x, int y, long tick, byte? dir = null, float? glide = null)
    {
        var c = new EntityChange { Id = id, Mask = NetField.X | NetField.Y | NetField.LastMoveTick, X = x, Y = y, LastMoveTick = tick };
        if (dir.HasValue)
        {
            c.Mask |= NetField.Dir;
            c.Dir = dir.Value;
        }

        if (glide.HasValue)
        {
            c.Mask |= NetField.GlideSize;
            c.GlideSize = glide.Value;
        }

        return c;
    }

    public static MapGrid Map(int width, int height, int levels, string[] types, params TurfRun[] runs)
    {
        var grid = new MapGrid { Tick = 7, Width = width, Height = height, Levels = levels };
        grid.TurfTypes.AddRange(types);
        grid.Runs.AddRange(runs);
        return grid;
    }

    /// <summary>A one-type map of <paramref name="width"/> by <paramref name="height"/> cells.</summary>
    public static MapGrid FlatMap(int width, int height, int levels = 1)
    {
        return Map(width, height, levels, new[] { "/turf/open/floor" }, new TurfRun(width * height * levels, 0));
    }

    public static Snapshot Snapshot(long tick, long own, uint seq, string[] types, params EntityState[] entities)
    {
        var s = new Snapshot { Tick = tick, OwnEntity = own, LastInputSeq = seq };
        s.Types.AddRange(types);
        s.Entities.AddRange(entities);
        return s;
    }

    public static Delta Delta(long tick, long own = 0, uint seq = 0, int firstNewType = 0, string[] newTypes = null, EntityState[] spawned = null, EntityChange[] changed = null, long[] despawned = null)
    {
        var d = new Delta { Tick = tick, OwnEntity = own, LastInputSeq = seq, FirstNewType = firstNewType };
        if (newTypes != null) d.NewTypes.AddRange(newTypes);
        if (spawned != null) d.Spawned.AddRange(spawned);
        if (changed != null) d.Changed.AddRange(changed);
        if (despawned != null) d.Despawned.AddRange(despawned);
        return d;
    }

    // ---- equality (the messages are classes or structs with lists, so xunit's reference equality is not enough) ----

    public static void AssertEqual(MapGrid expected, MapGrid actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.Levels, actual.Levels);
        Assert.Equal(expected.TurfTypes, actual.TurfTypes);
        Assert.Equal(expected.Runs, actual.Runs);
        Assert.Equal(expected.AppearanceRuns, actual.AppearanceRuns);
        Assert.Equal(expected.SolidBits ?? Array.Empty<byte>(), actual.SolidBits ?? Array.Empty<byte>());
        Assert.Equal(expected.FlagRuns, actual.FlagRuns);
        Assert.Equal(expected.BaseLights, actual.BaseLights);
        Assert.Equal(expected.BaseLightRuns, actual.BaseLightRuns);
        Assert.Equal(expected.Starlight, actual.Starlight);
        Assert.Equal(expected.NoParallaxLevels, actual.NoParallaxLevels);
    }

    public static void AssertEqual(Snapshot expected, Snapshot actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.LastInputSeq, actual.LastInputSeq);
        Assert.Equal(expected.OwnEntity, actual.OwnEntity);
        Assert.Equal(expected.Types, actual.Types);
        Assert.Equal(expected.Entities, actual.Entities);
        Assert.Equal(expected.Resources, actual.Resources);
        Assert.Equal(expected.Strings, actual.Strings);
        AssertEqual(expected.Appearances, actual.Appearances);
        AssertEqual(expected.Own, actual.Own);
    }

    public static void AssertEqual(IReadOnlyList<AppearanceEntry> expected, IReadOnlyList<AppearanceEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++) AssertEqual(expected[i], actual[i]);
    }

    public static void AssertEqual(AppearanceEntry e, AppearanceEntry a)
    {
        Assert.Equal(e.Icon, a.Icon);
        Assert.Equal(e.IconState, a.IconState);
        Assert.Equal(e.Dir, a.Dir);
        Assert.Equal(e.Rgba, a.Rgba);
        Assert.Equal(e.ColorMatrix, a.ColorMatrix);
        Assert.Equal(e.Alpha, a.Alpha);
        Assert.Equal(BitConverter.SingleToInt32Bits(e.Layer), BitConverter.SingleToInt32Bits(a.Layer));
        Assert.Equal(e.Plane, a.Plane);
        Assert.Equal(e.PixelX, a.PixelX);
        Assert.Equal(e.PixelY, a.PixelY);
        Assert.Equal(e.PixelW, a.PixelW);
        Assert.Equal(e.PixelZ, a.PixelZ);
        Assert.Equal(e.Transform, a.Transform);
        Assert.Equal(e.AppearanceFlags, a.AppearanceFlags);
        Assert.Equal(e.BlendMode, a.BlendMode);
        Assert.Equal(e.Maptext, a.Maptext);
        if (!e.Maptext.IsNone)
            Assert.Equal((e.MaptextWidth, e.MaptextHeight, e.MaptextX, e.MaptextY), (a.MaptextWidth, a.MaptextHeight, a.MaptextX, a.MaptextY));
        Assert.Equal(e.Overlays, a.Overlays);
        Assert.Equal(e.Underlays, a.Underlays);
    }

    public static void AssertEqual(OwnBlock e, OwnBlock a)
    {
        Assert.Equal(e.HasMotion, a.HasMotion);
        if (e.HasMotion)
        {
            Assert.Equal(e.Motion, a.Motion);
            Assert.Equal(e.Params, a.Params);
        }

        Assert.Equal(e.InputBufferDepth, a.InputBufferDepth);
        Assert.Equal(e.ActionsDone, a.ActionsDone); // version 43
        Assert.Equal(e.HandsBlocked, a.HandsBlocked); // version 89
        Assert.Equal(e.MeleeCooldownMs, a.MeleeCooldownMs); // version 95
        Assert.Equal(e.Access, a.Access);
        Assert.Equal(e.Eye, a.Eye); // version 51
        Assert.Equal(e.StaticChunks, a.StaticChunks); // version 78
        Assert.Equal(e.Popups, a.Popups);
        Assert.Equal(e.Verbs, a.Verbs);
        Assert.Equal(e.ViewAreas, a.ViewAreas);
        Assert.Equal(e.NewPrivateStrings, a.NewPrivateStrings);
        Assert.Equal(e.NewPrivateResources, a.NewPrivateResources);
        AssertEqual(e.NewPrivateAppearances, a.NewPrivateAppearances);
        if (e.NewPrivateStrings.Count > 0) Assert.Equal(e.FirstNewPrivateString, a.FirstNewPrivateString);
        if (e.NewPrivateResources.Count > 0) Assert.Equal(e.FirstNewPrivateResource, a.FirstNewPrivateResource);
        if (e.NewPrivateAppearances.Count > 0) Assert.Equal(e.FirstNewPrivateAppearance, a.FirstNewPrivateAppearance);
        Assert.Equal(e.ScreenAdded, a.ScreenAdded);
        Assert.Equal(e.ScreenChanged, a.ScreenChanged);
        Assert.Equal(e.ScreenRemoved, a.ScreenRemoved);
        Assert.Equal(e.ImagesAdded, a.ImagesAdded);
        Assert.Equal(e.ImagesChanged, a.ImagesChanged);
        Assert.Equal(e.ImagesRemoved, a.ImagesRemoved);
    }

    public static void AssertEqual(Delta expected, Delta actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.LastInputSeq, actual.LastInputSeq);
        Assert.Equal(expected.OwnEntity, actual.OwnEntity);
        AssertBodyEqual(expected, actual);
    }

    public static void AssertBodyEqual(Delta expected, Delta actual)
    {
        if (expected.NewTypes.Count > 0) Assert.Equal(expected.FirstNewType, actual.FirstNewType);
        Assert.Equal(expected.NewTypes, actual.NewTypes);
        Assert.Equal(expected.NewResources, actual.NewResources);
        Assert.Equal(expected.NewStrings, actual.NewStrings);
        if (expected.NewResources.Count > 0) Assert.Equal(expected.FirstNewResource, actual.FirstNewResource);
        if (expected.NewStrings.Count > 0) Assert.Equal(expected.FirstNewString, actual.FirstNewString);
        if (expected.NewAppearances.Count > 0) Assert.Equal(expected.FirstNewAppearance, actual.FirstNewAppearance);
        AssertEqual(expected.NewAppearances, actual.NewAppearances);
        Assert.Equal(expected.SolidChanged, actual.SolidChanged);
        Assert.Equal(expected.TurfAppearanceChanged, actual.TurfAppearanceChanged);
        Assert.Equal(expected.CellFlagsChanged, actual.CellFlagsChanged);
        Assert.Equal(expected.NewBaseLights, actual.NewBaseLights);
        if (expected.NewBaseLights.Count > 0) Assert.Equal(expected.FirstNewBaseLight, actual.FirstNewBaseLight);
        Assert.Equal(expected.BaseLightChanged, actual.BaseLightChanged);
        Assert.Equal(expected.NewTurfTypes, actual.NewTurfTypes);
        if (expected.NewTurfTypes.Count > 0) Assert.Equal(expected.FirstNewTurfType, actual.FirstNewTurfType);
        Assert.Equal(expected.TurfTypeChanged, actual.TurfTypeChanged);
        Assert.Equal(expected.StarlightChanged, actual.StarlightChanged);
        Assert.Equal(expected.Spawned, actual.Spawned);
        Assert.Equal(expected.Despawned, actual.Despawned);
        Assert.Equal(expected.Changed.Count, actual.Changed.Count);
        for (int i = 0; i < expected.Changed.Count; i++)
        {
            EntityChange e = expected.Changed[i], a = actual.Changed[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Mask, a.Mask);
            if ((e.Mask & NetField.X) != 0) Assert.Equal(e.X, a.X);
            if ((e.Mask & NetField.Y) != 0) Assert.Equal(e.Y, a.Y);
            if ((e.Mask & NetField.Z) != 0) Assert.Equal(e.Z, a.Z);
            if ((e.Mask & NetField.Dir) != 0) Assert.Equal(e.Dir, a.Dir);
            if ((e.Mask & NetField.Name) != 0) Assert.Equal(e.Name, a.Name);
            if ((e.Mask & NetField.GlideSize) != 0) Assert.Equal(e.GlideSize, a.GlideSize);
            if ((e.Mask & NetField.LastMoveTick) != 0) Assert.Equal(e.LastMoveTick, a.LastMoveTick);
            if ((e.Mask & NetField.Pos) != 0) { Assert.Equal(e.PosX, a.PosX); Assert.Equal(e.PosY, a.PosY); }
            if ((e.Mask & NetField.Yaw) != 0) Assert.Equal(e.Yaw, a.Yaw);
            if ((e.Mask & NetField.Posture) != 0) Assert.Equal(e.Posture, a.Posture);
            if ((e.Mask & NetField.Appearance) != 0) Assert.Equal(e.Appearance, a.Appearance);
            if ((e.Mask & NetField.Light) != 0) Assert.Equal(e.Light, a.Light);
            if ((e.Mask & NetField.Flags) != 0) Assert.Equal(e.Flags, a.Flags);
            if ((e.Mask & NetField.Flick) != 0) { Assert.Equal(e.FlickAppearance, a.FlickAppearance); Assert.Equal(e.FlickTick, a.FlickTick); }
            if ((e.Mask & NetField.Animation) != 0) Assert.Equal(e.Animation, a.Animation);
            if ((e.Mask & NetField.VisHost) != 0) Assert.Equal(e.VisHost, a.VisHost);
            if ((e.Mask & NetField.Door) != 0) Assert.Equal(e.Door, a.Door); // version 43
            if ((e.Mask & NetField.Ride) != 0) Assert.Equal(e.Ride, a.Ride); // version 77
            if ((e.Mask & NetField.Tram) != 0) Assert.Equal(e.Tram, a.Tram); // version 80
            if ((e.Mask & NetField.Flight) != 0) Assert.Equal(e.Flight, a.Flight); // version 81
        }
    }

    /// <summary>A hand-written message: the first byte, then whatever the test writes.</summary>
    public static byte[] Raw(MessageId id, Action<RawWriter> body)
    {
        var raw = new RawWriter();
        raw.Byte((byte)id);
        body(raw);
        return raw.ToArray();
    }

    public sealed class RawWriter
    {
        private readonly List<byte> _bytes = new();

        public RawWriter Byte(byte value)
        {
            _bytes.Add(value);
            return this;
        }

        public RawWriter Bytes(params byte[] values)
        {
            _bytes.AddRange(values);
            return this;
        }

        public RawWriter VarUInt(ulong value)
        {
            Span<byte> buffer = stackalloc byte[10];
            var w = new WireWriter(buffer);
            w.WriteVarUInt(value);
            _bytes.AddRange(w.Written.ToArray());
            return this;
        }

        public RawWriter VarInt(long value)
        {
            Span<byte> buffer = stackalloc byte[10];
            var w = new WireWriter(buffer);
            w.WriteVarInt(value);
            _bytes.AddRange(w.Written.ToArray());
            return this;
        }

        public RawWriter Single(float value)
        {
            _bytes.AddRange(BitConverter.GetBytes(value));
            return this;
        }

        public RawWriter String(string value)
        {
            if (value == null)
                return Byte(0);
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            return VarUInt((ulong)utf8.Length + 1).Bytes(utf8);
        }

        public byte[] ToArray() => _bytes.ToArray();
    }
}
