using System.Buffers;
using System.Text;
using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: M1 P2. Contract (one Encode and one TryDecode per message, the Delta broadcast split) by the M1 contracts step;
// the bodies and the byte layout are P2's (docs/ai/specs/protocol.md).

/// <summary>
/// The protocol codec: one <c>Encode</c> and one <c>TryDecode</c> per message. Every message is <c>[u8 MessageId][payload]</c>
/// (<see cref="MessageId"/>); <c>TryDecode</c> expects the whole transport message, id included, and refuses trailing
/// bytes.
/// <list type="bullet">
/// <item><description><c>Encode(message, destination)</c> writes into a caller's span and returns the bytes written; it throws
/// <see cref="ProtocolException"/> (<see cref="ProtocolError.BufferTooSmall"/>, or a value over its cap: a sender bug). The
/// <c>Encode(message)</c> overloads return a new array of the exact size, for the large messages (MapGrid, Snapshot) and
/// for tests.</description></item>
/// <item><description><c>TryDecode</c> never throws, whatever the input: it returns false with the
/// <see cref="ProtocolError"/> and a default message (fuzzing, scope 2j). A decoded message satisfies every cap of
/// <see cref="Limits"/> and every range rule of the message's documentation.</description></item>
/// <item><description>A <see cref="Delta"/> is encoded in two parts so that the server encodes a tick's changes once for every
/// connection: <see cref="EncodeDeltaBody(Delta)"/> (once per tick) and <see cref="EncodeDelta(long, uint, long, ReadOnlySpan{byte}, Span{byte})"/>
/// (per connection: the header, then the body copied).</description></item>
/// </list>
/// The encoder validates what it writes with the decoder's own rules (a coordinate outside <c>1..MaxMapSize</c>, a direction
/// that is none of the eight, a glide that is NaN, an id that is not positive), so a server never emits a message that a
/// decoder refuses; a sender bug is a <see cref="ProtocolException"/> at the encoder, not a desync at the client.
/// </summary>
public static partial class Codec
{
    /// <summary>The largest encoded <see cref="Hello"/>: its own cap since version 85, as the login token is longer than <see cref="Limits.MaxInboundMessageBytes"/>; one UDP packet still holds it.</summary>
    public const int MaxHelloBytes = 1 + ProtocolVersion.MagicLength + 2 + 2 + Limits.MaxNameBytes + 1 + Limits.TokenBytes + 2 + Limits.MaxLoginTokenBytes;

    /// <summary>The largest encoded <see cref="InputFrame"/>.</summary>
    public const int MaxInputFrameBytes = 1 + 5 + 1;

    /// <summary>The largest encoded <see cref="Welcome"/>.</summary>
    public const int MaxWelcomeBytes = 1 + 5 + Limits.TokenBytes + 10 + 1 + 10;

    /// <summary>
    /// The largest per-connection part a Delta has besides its shared body when the host gives no private block: the header
    /// (id, tick, last input seq, own entity) and the empty private block that <c>EncodeDelta(tick, ..., body, destination)</c>
    /// appends (version 2: every Delta ends with a private block; <see cref="EmptyOwnBlockBytes"/>).
    /// </summary>
    public const int MaxDeltaHeaderBytes = 1 + 10 + 5 + 10 + EmptyOwnBlockBytes;

    // The fewest bytes one list item takes on the wire, for WireReader.ReadCount (a forged count is refused before an
    // allocation): a string is at least its length byte; an entity record is id, type, x, y, z, dir, name, glide, last move.
    private const int MinStringBytes = 1;
    private const int MinEntityStateBytes = 1 + 1 + 1 + 1 + 1 + 1 + 1 + 4 + 1 + 1 + 1;
    private const int MinEntityChangeBytes = 1 + 1 + 1;
    private const int MinDespawnBytes = 1;
    private const int MinTurfRunBytes = 2;

    // The most bytes one list item takes, for the size estimates (ids at 10 bytes, every count at its widest).
    private const int MaxEntityStateFixedBytes = 10 + 5 + 5 + 5 + 5 + 1 + 10 + 4 + 10 + 1 + 2 + 2 + 1 + 1 + 5 + MaxLightBytes + 1 + 5 + 10 + 10 + MaxShotBytes + MaxRideBytes + MaxTramBytes + MaxFlightBytes;
    private const int MaxEntityChangeFixedBytes = 10 + 5 + 5 + 5 + 5 + 1 + 10 + 4 + 10 + 2 + 2 + 1 + 1 + 5 + MaxLightBytes + 1 + 10 + MaxShotBytes + MaxRideBytes + MaxTramBytes + MaxFlightBytes;
    private const int MaxDespawnBytes = 10;
    private const int MaxTurfRunBytes = 5 + 5;

    private const int HelloFlagToken = 1;
    private const int HelloFlagLogin = 2;
    private const int HelloFlagWaitsForLoad = 4; // version 97

    /// <summary>The id of a message without decoding it; false for an empty message or an unknown id.</summary>
    public static bool TryPeekId(ReadOnlySpan<byte> message, out MessageId id)
    {
        if (message.Length == 0 || !WireReader.IsKnownMessageId(message[0]))
        {
            id = MessageId.None;
            return false;
        }

        id = (MessageId)message[0];
        return true;
    }

    // ---- client to server ----

    public static int Encode(in Hello message, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Hello);
        w.WriteBytes(ProtocolVersion.Magic);
        w.WriteUInt16(message.Protocol);
        w.WriteString(message.Name, Limits.MaxNameBytes);
        w.WriteByte((byte)((message.ReconnectToken.IsNone ? 0 : HelloFlagToken) | (message.LoginToken == null ? 0 : HelloFlagLogin) | (message.WaitsForLoad ? HelloFlagWaitsForLoad : 0)));
        if (!message.ReconnectToken.IsNone)
            w.WriteToken(message.ReconnectToken);
        if (message.LoginToken != null)
            w.WriteString(message.LoginToken, Limits.MaxLoginTokenBytes);

        return Finish(ref w, MaxHelloBytes);
    }

    public static byte[] Encode(in Hello message)
    {
        Span<byte> buffer = stackalloc byte[MaxHelloBytes];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>
    /// Decodes a Hello. The magic and the protocol version are read first: a Hello of another version than
    /// <see cref="ProtocolVersion.Current"/> decodes to <c>Hello(version, null, None)</c> without its remainder being read
    /// (a later version may lay the rest out differently), so the host can always answer
    /// <see cref="DisconnectReason.WrongVersion"/>. The name is checked for UTF-8 and its byte cap only; the name rules
    /// (<see cref="NameRules"/>) are the host's.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out Hello value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Hello, MaxHelloBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            if (!r.ReadBytes(ProtocolVersion.MagicLength).SequenceEqual(ProtocolVersion.Magic))
                throw new ProtocolException(ProtocolError.BadMagic, "a Hello does not start with the magic");
            ushort version = r.ReadUInt16();
            if (version != ProtocolVersion.Current)
            {
                value = new Hello(version, null, SessionToken.None);
                return true;
            }

            string name = r.ReadString(Limits.MaxNameBytes);
            byte flags = r.ReadByte();
            if ((flags & ~(HelloFlagToken | HelloFlagLogin | HelloFlagWaitsForLoad)) != 0)
                throw Bad("a Hello sets reserved flags");
            SessionToken token = SessionToken.None;
            if ((flags & HelloFlagToken) != 0)
            {
                token = r.ReadToken();
                if (token.IsNone)
                    throw Bad("a Hello flags a token and sends none");
            }
            string login = null;
            if ((flags & HelloFlagLogin) != 0)
            {
                login = r.ReadString(Limits.MaxLoginTokenBytes);
                if (string.IsNullOrEmpty(login))
                    throw Bad("a Hello flags a login token and sends none");
            }

            r.ExpectEnd();
            value = new Hello(version, name, token, login, (flags & HelloFlagWaitsForLoad) != 0);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    public static int Encode(in InputFrame message, Span<byte> destination)
    {
        if ((message.HeldDirs & ~HeldDirs.All) != 0)
            throw Bad("an InputFrame holds a direction bit that is not N, S, E or W");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.InputFrame);
        w.WriteVarUInt(message.Seq);
        w.WriteByte((byte)message.HeldDirs);
        return Finish(ref w, Limits.MaxInboundMessageBytes);
    }

    public static byte[] Encode(in InputFrame message)
    {
        Span<byte> buffer = stackalloc byte[MaxInputFrameBytes];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> message, out InputFrame value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.InputFrame, Limits.MaxInboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ulong seq = r.ReadVarUInt();
            if (seq > uint.MaxValue)
                throw Bad("an InputFrame seq is over 32 bits");
            byte dirs = r.ReadByte();
            if ((dirs & ~(byte)HeldDirs.All) != 0)
                throw Bad("an InputFrame holds a direction bit that is not N, S, E or W");
            r.ExpectEnd();
            value = new InputFrame((uint)seq, (HeldDirs)dirs);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    // ---- server to client ----

    public static int Encode(in Welcome message, Span<byte> destination)
    {
        CheckNonNegative(message.Session, "a Welcome session");
        CheckNonNegative(message.Tick, "a Welcome tick");
        CheckNonNegative(message.OwnEntity, "a Welcome own entity");
        if (message.TickRate == 0)
            throw Bad("a Welcome tick rate of 0");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Welcome);
        w.WriteVarUInt((ulong)message.Session);
        w.WriteToken(message.Token);
        w.WriteVarUInt((ulong)message.Tick);
        w.WriteByte(message.TickRate);
        w.WriteVarUInt((ulong)message.OwnEntity);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(in Welcome message)
    {
        Span<byte> buffer = stackalloc byte[MaxWelcomeBytes];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> message, out Welcome value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Welcome, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            int session = ReadNonNegativeInt(ref r, "a Welcome session");
            SessionToken token = r.ReadToken();
            long tick = ReadNonNegativeLong(ref r, "a Welcome tick");
            byte tickRate = r.ReadByte();
            if (tickRate == 0)
                throw Bad("a Welcome tick rate of 0");
            long own = ReadNonNegativeLong(ref r, "a Welcome own entity");
            r.ExpectEnd();
            value = new Welcome(session, token, tick, tickRate, own);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    /// <summary>An upper bound of <see cref="Encode(MapGrid)"/>'s size (a buffer that always fits it).</summary>
    public static long MaxEncodedSize(MapGrid message)
    {
        long size = 1 + 10 + 5 + 5 + 5 + 10 + 10 + 10 + 10;
        foreach (string type in message.TurfTypes)
            size += 10 + MaxUtf8(type, Limits.MaxTypePathBytes);
        return size + (long)message.Runs.Count * MaxTurfRunBytes + (long)message.AppearanceRuns.Count * MaxTurfRunBytes
            + (message.SolidBits?.Length ?? 0) + MaxMapGridV3Size(message) + MaxMapGridV5Size(message) + CeilingBytes(message.Ceilings) + NoParallaxBytes(message) + LinkedBelowBytes(message) + MapLookBytes(message) + WallHeightBytes + TurfLightBytes(message.TurfLights);
    }

    public static int Encode(MapGrid message, Span<byte> destination)
    {
        if (message.Width < 1 || message.Width > Limits.MaxMapSize || message.Height < 1 || message.Height > Limits.MaxMapSize)
            throw Bad("a MapGrid width or height outside 1.." + Limits.MaxMapSize);
        if (message.Levels < 1 || message.Levels > Limits.MaxMapLevels)
            throw Bad("a MapGrid level count outside 1.." + Limits.MaxMapLevels);
        CheckNonNegative(message.Tick, "a MapGrid tick");
        int cells = message.Width * message.Height * message.Levels;
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.MapGrid);
        w.WriteVarUInt((ulong)message.Tick);
        w.WriteVarUInt((ulong)message.Width);
        w.WriteVarUInt((ulong)message.Height);
        w.WriteVarUInt((ulong)message.Levels);
        w.WriteCount(message.TurfTypes.Count, Limits.MaxTypeTableEntries);
        foreach (string type in message.TurfTypes)
        {
            if (type == null)
                throw Bad("a null turf type path");
            w.WriteString(type, Limits.MaxTypePathBytes);
        }

        w.WriteCount(message.Runs.Count, cells);
        long covered = 0;
        foreach (TurfRun run in message.Runs)
        {
            if (run.Count < 1 || run.TypeIndex < 0 || run.TypeIndex >= message.TurfTypes.Count)
                throw Bad("a MapGrid run with a count below 1 or a type index outside the table");
            covered += run.Count;
            if (covered > cells)
                throw Bad("the MapGrid runs cover more cells than the map has");
            w.WriteVarUInt((ulong)run.Count);
            w.WriteVarUInt((ulong)run.TypeIndex);
        }

        if (covered != cells)
            throw Bad("the MapGrid runs cover " + covered + " of " + cells + " cells");

        // Version 2: the turf appearance runs (none at all, or covering every cell) and the solidity bits (none, or one per cell).
        w.WriteCount(message.AppearanceRuns.Count, cells);
        covered = 0;
        foreach (TurfRun run in message.AppearanceRuns)
        {
            if (run.Count < 1 || run.TypeIndex < 0 || run.TypeIndex > Limits.MaxAppearanceTableEntries)
                throw Bad("a MapGrid appearance run with a count below 1 or an appearance index outside the table");
            covered += run.Count;
            if (covered > cells)
                throw Bad("the MapGrid appearance runs cover more cells than the map has");
            w.WriteVarUInt((ulong)run.Count);
            w.WriteVarUInt((ulong)run.TypeIndex);
        }

        if (message.AppearanceRuns.Count > 0 && covered != cells)
            throw Bad("the MapGrid appearance runs cover " + covered + " of " + cells + " cells");
        byte[] bits = message.SolidBits ?? Array.Empty<byte>();
        int bitBytes = (cells + 7) / 8;
        if (bits.Length != 0 && bits.Length != bitBytes)
            throw Bad("the MapGrid solidity bits are " + bits.Length + " bytes, the map needs " + bitBytes + " (or none)");
        if (bits.Length != 0 && !PaddingIsZero(bits, cells))
            throw Bad("the MapGrid solidity bits set a bit past the last cell");
        w.WriteVarUInt((ulong)bits.Length);
        w.WriteBytes(bits);
        WriteMapGridV3(ref w, message, cells);
        WriteMapGridV5(ref w, message, cells);
        foreach (var c in message.Ceilings)
            if (c.X > message.Width || c.Y > message.Height || c.Z > message.Levels) throw Bad("ceiling outside map");
        WriteCeilings(ref w, message.Ceilings, cells);
        WriteNoParallax(ref w, message);
        WriteLinkedBelow(ref w, message);
        WriteMapLook(ref w, message);
        WriteWallHeight(ref w, message);
        foreach (TurfLightChange c in message.TurfLights) // version 96
            if (c.X > message.Width || c.Y > message.Height || c.Z > message.Levels) throw Bad("a turf light outside the map");
        WriteTurfLights(ref w, message.TurfLights, 2 * cells, false);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(MapGrid message) =>
        EncodeToArray(MaxEncodedSize(message), (Span<byte> buffer) => Encode(message, buffer));

    public static bool TryDecode(ReadOnlySpan<byte> message, out MapGrid value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.MapGrid, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var grid = new MapGrid { Tick = ReadNonNegativeLong(ref r, "a MapGrid tick") };
            grid.Width = ReadRanged(ref r, 1, Limits.MaxMapSize, "a MapGrid width");
            grid.Height = ReadRanged(ref r, 1, Limits.MaxMapSize, "a MapGrid height");
            grid.Levels = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a MapGrid level count");
            int cells = grid.Width * grid.Height * grid.Levels;
            int typeCount = r.ReadCount(Limits.MaxTypeTableEntries, MinStringBytes);
            grid.TurfTypes.Capacity = typeCount;
            for (int i = 0; i < typeCount; i++)
                grid.TurfTypes.Add(ReadTypePath(ref r));
            int runCount = r.ReadCount(cells, MinTurfRunBytes);
            grid.Runs.Capacity = runCount;
            long covered = 0;
            for (int i = 0; i < runCount; i++)
            {
                ulong count = r.ReadVarUInt();
                ulong typeIndex = r.ReadVarUInt();
                if (count == 0 || count > (ulong)(cells - covered))
                    throw Bad("a MapGrid run with no cells, or past the end of the map");
                if (typeIndex >= (ulong)typeCount)
                    throw Bad("a MapGrid run with a type index outside the table");
                covered += (long)count;
                grid.Runs.Add(new TurfRun((int)count, (int)typeIndex));
            }

            if (covered != cells)
                throw Bad("the MapGrid runs do not cover the map");

            int appearanceRuns = r.ReadCount(cells, MinTurfRunBytes);
            grid.AppearanceRuns.Capacity = appearanceRuns;
            covered = 0;
            for (int i = 0; i < appearanceRuns; i++)
            {
                ulong count = r.ReadVarUInt();
                ulong index = r.ReadVarUInt();
                if (count == 0 || count > (ulong)(cells - covered))
                    throw Bad("a MapGrid appearance run with no cells, or past the end of the map");
                if (index > (ulong)Limits.MaxAppearanceTableEntries)
                    throw Bad("a MapGrid appearance run with an index outside the table");
                covered += (long)count;
                grid.AppearanceRuns.Add(new TurfRun((int)count, (int)index));
            }

            if (appearanceRuns > 0 && covered != cells)
                throw Bad("the MapGrid appearance runs do not cover the map");
            int bitBytes = (cells + 7) / 8;
            ulong bitCount = r.ReadVarUInt();
            if (bitCount != 0 && bitCount != (ulong)bitBytes)
                throw Bad("the MapGrid solidity bits are " + bitCount + " bytes, the map needs " + bitBytes + " (or none)");
            if (bitCount != 0)
            {
                byte[] bits = r.ReadBytes(bitBytes).ToArray();
                if (!PaddingIsZero(bits, cells))
                    throw Bad("the MapGrid solidity bits set a bit past the last cell");
                grid.SolidBits = bits;
            }

            ReadMapGridV3(ref r, grid, cells);
            ReadMapGridV5(ref r, grid, cells);
            ReadCeilings(ref r, grid.Ceilings, cells);
            foreach (var c in grid.Ceilings)
                if (c.X > grid.Width || c.Y > grid.Height || c.Z > grid.Levels) throw Bad("ceiling outside map");
            ReadNoParallax(ref r, grid);
            ReadLinkedBelow(ref r, grid);
            ReadMapLook(ref r, grid);
            ReadWallHeight(ref r, grid);
            ReadTurfLights(ref r, grid.TurfLights, 2 * cells, false); // version 96
            foreach (TurfLightChange c in grid.TurfLights)
                if (c.X > grid.Width || c.Y > grid.Height || c.Z > grid.Levels) throw Bad("a turf light outside the map");
            r.ExpectEnd();
            value = grid;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    // The bits past the last cell of a solidity bitmap must be 0 (one encoding per map).
    private static bool PaddingIsZero(byte[] bits, int cells)
    {
        int used = cells % 8;
        return used == 0 || (bits[^1] >> used) == 0;
    }

    /// <summary>An upper bound of <see cref="Encode(Snapshot)"/>'s size (a buffer that always fits it).</summary>
    public static long MaxEncodedSize(Snapshot message)
    {
        long size = 1 + 10 + 5 + 10 + 10 + 10 + 10 + 10;
        foreach (string type in message.Types)
            size += 10 + MaxUtf8(type, Limits.MaxTypePathBytes);
        foreach (string resource in message.Resources)
            size += 10 + MaxUtf8(resource, Limits.MaxResourceBytes);
        foreach (string text in message.Strings)
            size += 10 + MaxUtf8(text, Limits.MaxTableStringBytes);
        foreach (AppearanceEntry appearance in message.Appearances)
            size += MaxAppearanceSize(appearance);
        return size + EntityStatesSize(message.Entities) + MaxOwnBlockSize(message.Own);
    }

    public static int Encode(Snapshot message, Span<byte> destination)
    {
        CheckNonNegative(message.Tick, "a Snapshot tick");
        CheckNonNegative(message.OwnEntity, "a Snapshot own entity");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Snapshot);
        w.WriteVarUInt((ulong)message.Tick);
        w.WriteVarUInt(message.LastInputSeq);
        w.WriteVarUInt((ulong)message.OwnEntity);
        w.WriteCount(message.Types.Count, Limits.MaxTypeTableEntries);
        foreach (string type in message.Types)
        {
            if (type == null)
                throw Bad("a null type path");
            w.WriteString(type, Limits.MaxTypePathBytes);
        }

        // Version 2: the public tables, complete (index 1 is the first entry), then the entities, then the private block.
        if ((long)message.Resources.Count + message.Strings.Count + message.Appearances.Count > Limits.MaxNewTableEntriesPerMessage)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a Snapshot with more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
        WriteTable(ref w, message.Resources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes);
        WriteTable(ref w, message.Strings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes);
        w.WriteCount(message.Appearances.Count, Limits.MaxAppearanceTableEntries);
        for (int i = 0; i < message.Appearances.Count; i++)
        {
            if (message.Appearances[i] == null)
                throw Bad("a null appearance entry");
            WriteAppearance(ref w, message.Appearances[i], false, (uint)i + 1);
        }

        WriteEntityStates(ref w, message.Entities, true, Limits.MaxEntitiesPerMessage, message.Tick);
        WriteOwnBlock(ref w, message.Own, message.Tick);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(Snapshot message) =>
        EncodeToArray(MaxEncodedSize(message), (Span<byte> buffer) => Encode(message, buffer));

    public static bool TryDecode(ReadOnlySpan<byte> message, out Snapshot value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Snapshot, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var snapshot = new Snapshot { Tick = ReadNonNegativeLong(ref r, "a Snapshot tick") };
            snapshot.LastInputSeq = ReadUInt32Varint(ref r, "a Snapshot last input seq");
            snapshot.OwnEntity = ReadNonNegativeLong(ref r, "a Snapshot own entity");
            int typeCount = r.ReadCount(Limits.MaxTypeTableEntries, MinStringBytes);
            snapshot.Types.Capacity = typeCount;
            for (int i = 0; i < typeCount; i++)
                snapshot.Types.Add(ReadTypePath(ref r));
            int resources = ReadTable(ref r, snapshot.Resources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes);
            int strings = ReadTable(ref r, snapshot.Strings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes);
            int appearances = r.ReadCount(Limits.MaxAppearanceTableEntries, MinAppearanceBytes);
            if ((long)resources + strings + appearances > Limits.MaxNewTableEntriesPerMessage)
                throw new ProtocolException(ProtocolError.CountTooLarge, "a Snapshot with more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
            snapshot.Appearances.Capacity = appearances;
            for (int i = 0; i < appearances; i++)
                snapshot.Appearances.Add(ReadAppearance(ref r, false, (uint)i + 1));
            ReadEntityStates(ref r, snapshot.Entities, true, typeCount, Limits.MaxEntitiesPerMessage, snapshot.Tick, appearances);
            ReadOwnBlock(ref r, snapshot.Own, snapshot.Tick);
            r.ExpectEnd();
            value = snapshot;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static void WriteTable(ref WireWriter w, List<string> list, int maxEntries, int maxBytes)
    {
        w.WriteCount(list.Count, maxEntries);
        foreach (string s in list)
        {
            if (s == null)
                throw Bad("a null table string");
            w.WriteString(s, maxBytes);
        }
    }

    private static int ReadTable(ref WireReader r, List<string> into, int maxEntries, int maxBytes)
    {
        int count = r.ReadCount(maxEntries, MinStringBytes);
        into.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            string s = r.ReadString(maxBytes);
            if (s == null)
                throw new ProtocolException(ProtocolError.BadString, "a null table string");
            into.Add(s);
        }

        return count;
    }

    /// <summary>An upper bound of <see cref="EncodeDeltaBody(Delta)"/>'s size (a buffer that always fits it).</summary>
    public static long MaxDeltaBodySize(Delta message)
    {
        long size = 10 + 10 + 10 + 10 + 10 + 10;
        foreach (string type in message.NewTypes)
            size += 10 + MaxUtf8(type, Limits.MaxTypePathBytes);
        foreach (string resource in message.NewResources)
            size += 10 + MaxUtf8(resource, Limits.MaxResourceBytes);
        foreach (string text in message.NewStrings)
            size += 10 + MaxUtf8(text, Limits.MaxTableStringBytes);
        foreach (AppearanceEntry appearance in message.NewAppearances)
            size += MaxAppearanceSize(appearance);
        size += EntityStatesSize(message.Spawned);
        size += 10;
        foreach (EntityChange change in message.Changed)
            size += MaxEntityChangeFixedBytes + ((change.Mask & NetField.Name) != 0 ? MaxUtf8(change.Name, Limits.MaxEntityNameBytes) : 0)
                + 15 + ((change.Mask & NetField.Animation) != 0 ? MaxAnimationBytes(change.Animation) : 0) + ((change.Mask & NetField.Door) != 0 ? MaxDoorBytes(change.Door) : 0);
        size += 10 + (long)message.Despawned.Count * MaxDespawnBytes;
        size += 10 + 10 + ((long)message.SolidChanged.Count + message.TurfAppearanceChanged.Count) * (5 + 5 + 5 + 5);
        return size + MaxDeltaV3Size(message) + MaxDeltaV5Size(message) + CeilingBytes(message.Ceilings) + 5 + TurfLightBytes(message.TurfLightChanged);
    }

    /// <summary>The whole Delta, header, body and private block (<see cref="Delta.Own"/>; tests, and a server that sends one connection only).</summary>
    public static int Encode(Delta message, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteDeltaHeader(ref w, message.Tick, message.LastInputSeq, message.OwnEntity);
        WriteDeltaBody(ref w, message);
        WriteOwnBlock(ref w, message.Own, message.Tick);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(Delta message) =>
        EncodeToArray(MaxDeltaBodySize(message) + MaxDeltaHeaderBytes + MaxOwnBlockSize(message.Own), (Span<byte> buffer) => Encode(message, buffer));

    public static bool TryDecode(ReadOnlySpan<byte> message, out Delta value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Delta, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var delta = new Delta { Tick = ReadNonNegativeLong(ref r, "a Delta tick") };
            delta.LastInputSeq = ReadUInt32Varint(ref r, "a Delta last input seq");
            delta.OwnEntity = ReadNonNegativeLong(ref r, "a Delta own entity");
            ReadDeltaBody(ref r, delta);
            ReadOwnBlock(ref r, delta.Own, delta.Tick);
            r.ExpectEnd();
            value = delta;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    /// <summary>
    /// The body of a Delta (from <see cref="Delta.FirstNewType"/> on, up to the private block), encoded once per tick for
    /// every connection. It depends on <see cref="Delta.Tick"/> (an entity's last move tick travels relative to it) and on
    /// nothing else of the header: <see cref="Delta.LastInputSeq"/>, <see cref="Delta.OwnEntity"/> and <see cref="Delta.Own"/> are ignored.
    /// </summary>
    public static byte[] EncodeDeltaBody(Delta message) =>
        EncodeToArray(MaxDeltaBodySize(message), (Span<byte> buffer) => EncodeDeltaBody(message, buffer));

    /// <summary>The body of a Delta into <paramref name="destination"/>; returns the bytes written (<see cref="ProtocolError.BufferTooSmall"/> when it does not fit).</summary>
    public static int EncodeDeltaBody(Delta message, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteDeltaBody(ref w, message);
        if (w.Position > Limits.MaxOutboundMessageBytes - MaxDeltaHeaderBytes)
            throw new ProtocolException(ProtocolError.TooLong, "a Delta body of " + w.Position + " bytes does not leave room for its header under the message cap");
        return w.Position;
    }

    /// <summary>
    /// The body of a Delta into a reusable buffer for the broadcast: <paramref name="buffer"/> is allocated, or replaced by
    /// a larger one, only when the body might not fit, so a server that keeps one buffer for the run allocates nothing in a
    /// steady state. Returns the body's length; the bytes are <c>buffer[0..length]</c> until the next call.
    /// </summary>
    public static int EncodeDeltaBody(Delta message, ref byte[] buffer)
    {
        long bound = MaxDeltaBodySize(message);
        if (bound > Limits.MaxOutboundMessageBytes)
            bound = Limits.MaxOutboundMessageBytes;
        if (buffer == null || buffer.Length < bound)
            buffer = new byte[Math.Max(bound, buffer == null ? 0 : buffer.Length * 2L)];
        return EncodeDeltaBody(message, buffer);
    }

    /// <summary>
    /// One connection's Delta with no private block of its own (version 1's call; the version-2 Delta always ends with a
    /// private block, so an empty one is appended, <see cref="EmptyOwnBlockBytes"/>): its header (<paramref name="tick"/>,
    /// <paramref name="lastInputSeq"/>, <paramref name="ownEntity"/>) followed by a <paramref name="body"/> from
    /// <see cref="EncodeDeltaBody(Delta)"/>. Needs <see cref="MaxDeltaHeaderBytes"/> plus the body's length.
    /// </summary>
    public static int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        WriteDeltaHeader(ref w, tick, lastInputSeq, ownEntity);
        w.WriteBytes(body);
        WriteOwnBlock(ref w, null, 0);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static int Encode(in ServerText message, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.ServerText);
        w.WriteString(message.Type, Limits.MaxServerTextTypeBytes);
        w.WriteString(message.Html, Limits.MaxServerTextBytes);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(in ServerText message)
    {
        long bound = 1 + 20 + MaxUtf8(message.Type, Limits.MaxServerTextTypeBytes) + MaxUtf8(message.Html, Limits.MaxServerTextBytes);
        Span<byte> buffer = bound <= 1024 ? stackalloc byte[(int)bound] : new byte[bound];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> message, out ServerText value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.ServerText, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            string type = r.ReadString(Limits.MaxServerTextTypeBytes);
            string html = r.ReadString(Limits.MaxServerTextBytes);
            r.ExpectEnd();
            value = new ServerText(type, html);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    public static int Encode(in DisconnectInfo message, Span<byte> destination)
    {
        if (message.Reason > DisconnectReason.LoginRequired)
            throw Bad("an unknown disconnect reason " + (byte)message.Reason);
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Disconnect);
        w.WriteByte((byte)message.Reason);
        w.WriteString(message.Text, Limits.MaxDisconnectTextBytes);
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    public static byte[] Encode(in DisconnectInfo message)
    {
        long bound = 1 + 1 + 10 + MaxUtf8(message.Text, Limits.MaxDisconnectTextBytes);
        Span<byte> buffer = bound <= 1024 ? stackalloc byte[(int)bound] : new byte[bound];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> message, out DisconnectInfo value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Disconnect, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte reason = r.ReadByte();
            if (reason > (byte)DisconnectReason.LoginRequired)
                throw Bad("an unknown disconnect reason " + reason);
            string text = r.ReadString(Limits.MaxDisconnectTextBytes);
            r.ExpectEnd();
            value = new DisconnectInfo((DisconnectReason)reason, text);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    // ---- the Delta layout (header, then the body shared by every connection, then the connection's private block) ----

    private static void WriteDeltaHeader(ref WireWriter w, long tick, uint lastInputSeq, long ownEntity)
    {
        CheckNonNegative(tick, "a Delta tick");
        CheckNonNegative(ownEntity, "a Delta own entity");
        w.WriteMessageId(MessageId.Delta);
        w.WriteVarUInt((ulong)tick);
        w.WriteVarUInt(lastInputSeq);
        w.WriteVarUInt((ulong)ownEntity);
    }

    private static void WriteDeltaBody(ref WireWriter w, Delta message)
    {
        CheckNonNegative(message.Tick, "a Delta tick");
        if (message.FirstNewType < 0 || (long)message.FirstNewType + message.NewTypes.Count > Limits.MaxTypeTableEntries)
            throw new ProtocolException(ProtocolError.CountTooLarge, "the type table would pass " + Limits.MaxTypeTableEntries + " entries");
        long entities = (long)message.Spawned.Count + message.Changed.Count + message.Despawned.Count;
        if (entities > Limits.MaxEntitiesPerMessage)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a Delta of " + entities + " entity records is over " + Limits.MaxEntitiesPerMessage);
        if ((long)message.NewResources.Count + message.NewStrings.Count + message.NewAppearances.Count > Limits.MaxNewTableEntriesPerMessage)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a Delta that adds more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
        w.WriteVarUInt((ulong)message.FirstNewType);
        w.WriteCount(message.NewTypes.Count, Limits.MaxTypeTableEntries);
        foreach (string type in message.NewTypes)
        {
            if (type == null)
                throw Bad("a null type path");
            w.WriteString(type, Limits.MaxTypePathBytes);
        }

        // The parts byte says which of the version-2 lists the body carries (a list is present only when it is not empty).
        int parts = (message.NewResources.Count > 0 ? PartResources : 0) | (message.NewStrings.Count > 0 ? PartStrings : 0)
            | (message.NewAppearances.Count > 0 ? PartAppearances : 0) | (message.SolidChanged.Count > 0 ? PartSolid : 0)
            | (message.TurfAppearanceChanged.Count > 0 ? PartTurf : 0) | (HasDeltaV3(message) ? PartV3 : 0) | (HasDeltaV5(message) ? PartV5 : 0);
        w.WriteByte((byte)parts);
        if ((parts & PartResources) != 0)
            WriteStringTable(ref w, message.FirstNewResource, message.NewResources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes);
        if ((parts & PartStrings) != 0)
            WriteStringTable(ref w, message.FirstNewString, message.NewStrings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes);
        if ((parts & PartAppearances) != 0)
            WriteAppearanceTable(ref w, message.FirstNewAppearance, message.NewAppearances, false);
        WriteEntityStates(ref w, message.Spawned, false, Limits.MaxEntitiesPerMessage, message.Tick);
        w.WriteCount(message.Changed.Count, Limits.MaxEntitiesPerMessage);
        long previous = 0;
        foreach (EntityChange change in message.Changed)
        {
            WriteId(ref w, change.Id, previous, false);
            previous = change.Id;
            WriteChange(ref w, change, message.Tick);
        }

        w.WriteCount(message.Despawned.Count, Limits.MaxEntitiesPerMessage);
        previous = 0;
        foreach (long id in message.Despawned)
        {
            WriteId(ref w, id, previous, false);
            previous = id;
        }

        if ((long)message.SolidChanged.Count + message.TurfAppearanceChanged.Count + V3CellChanges(message) + message.CellGasChanged.Count + message.Ceilings.Count > Limits.MaxCellChangesPerDelta)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a Delta of more than " + Limits.MaxCellChangesPerDelta + " cell changes");
        if ((parts & PartSolid) != 0)
            WriteSolidChanges(ref w, message.SolidChanged, Limits.MaxCellChangesPerDelta);
        if ((parts & PartTurf) != 0)
            WriteTurfChanges(ref w, message.TurfAppearanceChanged, Limits.MaxCellChangesPerDelta);
        if ((parts & PartV3) != 0)
            WriteDeltaV3(ref w, message);
        if ((parts & PartV5) != 0)
            WriteDeltaV5(ref w, message);
        WriteCeilings(ref w, message.Ceilings, Limits.MaxCellChangesPerDelta);
        WriteSun(ref w, message.SunAzimuth);
        WriteTurfLights(ref w, message.TurfLightChanged, Limits.MaxCellChangesPerDelta, true); // version 96
    }

    private const int PartResources = 1;
    private const int PartStrings = 2;
    private const int PartAppearances = 4;
    private const int PartSolid = 8;
    private const int PartTurf = 16;
    private const int PartsAll = 127;

    private static void ReadDeltaBody(ref WireReader r, Delta delta)
    {
        delta.FirstNewType = ReadRanged(ref r, 0, Limits.MaxTypeTableEntries, "a Delta first new type");
        int typeCount = r.ReadCount(Limits.MaxTypeTableEntries - delta.FirstNewType, MinStringBytes);
        delta.NewTypes.Capacity = typeCount;
        for (int i = 0; i < typeCount; i++)
            delta.NewTypes.Add(ReadTypePath(ref r));
        byte parts = r.ReadByte();
        if ((parts & ~PartsAll) != 0)
            throw Bad("a Delta sets part bits that are not defined");
        int resources = 0, strings = 0, appearances = 0;
        if ((parts & PartResources) != 0)
        {
            delta.FirstNewResource = ReadStringTable(ref r, delta.NewResources, Limits.MaxResourceTableEntries, Limits.MaxResourceBytes, out resources);
            if (resources == 0) throw Bad("a Delta flags new resources and has none");
        }

        if ((parts & PartStrings) != 0)
        {
            delta.FirstNewString = ReadStringTable(ref r, delta.NewStrings, Limits.MaxStringTableEntries, Limits.MaxTableStringBytes, out strings);
            if (strings == 0) throw Bad("a Delta flags new strings and has none");
        }

        if ((parts & PartAppearances) != 0)
        {
            delta.FirstNewAppearance = ReadAppearanceTable(ref r, delta.NewAppearances, false, out appearances);
            if (appearances == 0) throw Bad("a Delta flags new appearances and has none");
        }

        if ((long)resources + strings + appearances > Limits.MaxNewTableEntriesPerMessage)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a Delta that adds more than " + Limits.MaxNewTableEntriesPerMessage + " table entries");
        int budget = Limits.MaxEntitiesPerMessage;
        // A spawned entity's appearance may be one of the entries this Delta adds, whose number the codec cannot compare
        // with the table's length (the replica does), so only the table's cap bounds it here.
        ReadEntityStates(ref r, delta.Spawned, false, Limits.MaxTypeTableEntries, budget, delta.Tick, Limits.MaxAppearanceTableEntries);
        budget -= delta.Spawned.Count;
        int changed = r.ReadCount(budget, MinEntityChangeBytes);
        delta.Changed.Capacity = changed;
        long previous = 0;
        for (int i = 0; i < changed; i++)
        {
            long id = ReadId(ref r, previous, false);
            previous = id;
            delta.Changed.Add(ReadChange(ref r, id, delta.Tick));
        }

        budget -= changed;
        int despawned = r.ReadCount(budget, MinDespawnBytes);
        delta.Despawned.Capacity = despawned;
        previous = 0;
        for (int i = 0; i < despawned; i++)
        {
            long id = ReadId(ref r, previous, false);
            previous = id;
            delta.Despawned.Add(id);
        }

        if ((parts & PartSolid) != 0)
        {
            ReadSolidChanges(ref r, delta.SolidChanged, Limits.MaxCellChangesPerDelta);
            if (delta.SolidChanged.Count == 0) throw Bad("a Delta flags solidity changes and has none");
        }

        if ((parts & PartTurf) != 0)
        {
            ReadTurfChanges(ref r, delta.TurfAppearanceChanged, Limits.MaxCellChangesPerDelta - delta.SolidChanged.Count);
            if (delta.TurfAppearanceChanged.Count == 0) throw Bad("a Delta flags turf appearance changes and has none");
        }

        if ((parts & PartV3) != 0)
            ReadDeltaV3(ref r, delta, Limits.MaxCellChangesPerDelta - delta.SolidChanged.Count - delta.TurfAppearanceChanged.Count);
        if ((parts & PartV5) != 0)
            ReadDeltaV5(ref r, delta, Limits.MaxCellChangesPerDelta - delta.SolidChanged.Count - delta.TurfAppearanceChanged.Count - V3CellChanges(delta));
        ReadCeilings(ref r, delta.Ceilings, Limits.MaxCellChangesPerDelta - delta.SolidChanged.Count - delta.TurfAppearanceChanged.Count - V3CellChanges(delta) - delta.CellGasChanged.Count);
        delta.SunAzimuth = ReadSun(ref r);
        ReadTurfLights(ref r, delta.TurfLightChanged, Limits.MaxCellChangesPerDelta, true); // version 96
    }

    // ---- entity records ----

    // [id diff] [varuint type] [varuint x] [varuint y] [varuint z] [u8 dir] [str name] [f32 glide] [varuint ticks since the last move]
    // [u8 flags: bit 0 capsule (position), bit 1 posture, bit 2 light, bit 3 entity flags (version 4); the rest zero]
    //     if capsule: [u16 posX] [u16 posY] [u8 yaw]       if posture: [u8 posture]
    // [varuint appearance]   (a public table index, 0 for none)
    //     if light: a light      if entity flags: [u8 EntityFlags, not 0]
    // version 8: flags bit 4, a flick; version 9: bit 5, an animation; version 10: bit 6, a visual: [varuint host id, 0 for its turf]
    // version 34: bit 7, a shot (Codec.V34.cs)

    private static void WriteEntityStates(ref WireWriter w, List<EntityState> list, bool strictlyIncreasing, int max, long tick)
    {
        w.WriteCount(list.Count, max);
        long previous = 0;
        foreach (EntityState e in list)
        {
            WriteId(ref w, e.Id, previous, strictlyIncreasing);
            previous = e.Id;
            WriteType(ref w, e.Type);
            WriteCoordinates(ref w, e.X, e.Y, e.Z);
            CheckDir(e.Dir);
            w.WriteByte(e.Dir);
            w.WriteString(e.Name, Limits.MaxEntityNameBytes);
            CheckGlide(e.GlideSize);
            w.WriteSingle(e.GlideSize);
            WriteMoveTick(ref w, e.LastMoveTick, tick);
            CheckEntityFlags(e.Flags);
            w.WriteByte((byte)((e.HasPos ? 1 : 0) | (e.Posture != 0 ? 2 : 0) | (e.Light.IsNone ? 0 : 4) | (e.Flags != EntityFlags.None ? 8 : 0) | (e.FlickAppearance.IsNone ? 0 : 16) | (e.Animation == null ? 0 : 32) | (e.IsVisual ? 64 : 0) | (e.Shot.HasValue || e.Door != null || !e.Ride.IsNone || !e.Tram.IsNone || !e.Flight.IsNone ? 128 : 0)));
            if (e.HasPos)
            {
                WritePos(ref w, e.PosX, e.PosY);
                w.WriteByte(e.Yaw);
            }

            if (e.Posture != 0)
            {
                CheckPosture(e.Posture);
                w.WriteByte(e.Posture);
            }

            WriteEntityAppearance(ref w, e.Appearance);
            if (!e.Light.IsNone)
                WriteLight(ref w, e.Light);
            if (e.Flags != EntityFlags.None)
                w.WriteByte((byte)e.Flags);
            if (!e.FlickAppearance.IsNone)
                WriteFlick(ref w, e.FlickAppearance, e.FlickTick, tick);
            if (e.Animation != null)
                WriteAnimation(ref w, e.Animation, tick);
            if (e.IsVisual)
                WriteVisHost(ref w, e.VisHost, e.Id);
            else if (e.VisHost != 0)
                throw Bad("an entity that is no visual has a visual's host");
            if (e.Shot.HasValue || e.Door != null || !e.Ride.IsNone || !e.Tram.IsNone || !e.Flight.IsNone)
                w.WriteByte((byte)((e.Shot.HasValue ? EntityExtShot : 0) | (e.Door != null ? EntityExtDoor : 0) | (e.Ride.IsNone ? 0 : EntityExtRide) | (e.Tram.IsNone ? 0 : EntityExtTram) | (e.Flight.IsNone ? 0 : EntityExtFlight))); // version 43, 77, 80, 81
            if (e.Shot.HasValue)
                WriteShot(ref w, e.Shot.Value);
            if (e.Door != null)
                WriteDoor(ref w, e.Door);
            if (!e.Ride.IsNone)
                WriteRide(ref w, e.Ride, tick);
            if (!e.Tram.IsNone)
                WriteTram(ref w, e.Tram, tick);
            if (!e.Flight.IsNone)
                WriteFlight(ref w, e.Flight, tick);
        }
    }

    private static void ReadEntityStates(ref WireReader r, List<EntityState> list, bool strictlyIncreasing, int typeLimit, int max, long tick, int appearanceLimit)
    {
        int count = r.ReadCount(max, MinEntityStateBytes);
        list.Capacity = count;
        long previous = 0;
        for (int i = 0; i < count; i++)
        {
            long id = ReadId(ref r, previous, strictlyIncreasing);
            previous = id;
            int type = ReadType(ref r, typeLimit);
            int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "an entity x");
            int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "an entity y");
            int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "an entity z");
            byte dir = r.ReadByte();
            CheckDir(dir);
            string name = r.ReadString(Limits.MaxEntityNameBytes);
            float glide = r.ReadSingle();
            CheckGlide(glide);
            long lastMove = ReadMoveTick(ref r, tick);
            byte flags = r.ReadByte();

            int posX = x * MotionMath.SubunitsPerTile, posY = y * MotionMath.SubunitsPerTile;
            byte yaw = 0;
            if ((flags & 1) != 0)
            {
                ReadPos(ref r, out posX, out posY);
                yaw = r.ReadByte();
            }

            byte posture = 0;
            if ((flags & 2) != 0)
            {
                posture = r.ReadByte();
                CheckPosture(posture);
                if (posture == 0)
                    throw Bad("an entity record flags a posture and it is 0");
            }

            TableRef appearance = ReadEntityAppearance(ref r, appearanceLimit);
            LightState light = LightState.None;
            if ((flags & 4) != 0)
            {
                light = ReadLight(ref r);
                if (light.IsNone)
                    throw Bad("an entity record flags a light and its range is 0");
            }

            var entityFlags = EntityFlags.None;
            if ((flags & 8) != 0)
            {
                entityFlags = (EntityFlags)r.ReadByte();
                CheckEntityFlags(entityFlags);
                if (entityFlags == EntityFlags.None)
                    throw Bad("an entity record flags entity flags and they are 0");
            }

            TableRef flick = TableRef.None;
            long flickTick = 0;
            if ((flags & 16) != 0)
                ReadFlick(ref r, tick, appearanceLimit, out flick, out flickTick);
            EntityAnimation animation = null;
            if ((flags & 32) != 0)
            {
                animation = ReadAnimation(ref r, tick);
                if (animation == null)
                    throw Bad("an entity record flags an animation and it has no track");
            }
            long visHost = 0;
            if ((flags & 64) != 0)
                visHost = ReadVisHost(ref r, id);
            byte ext = (flags & 128) != 0 ? r.ReadByte() : (byte)0;
            if ((flags & 128) != 0 && (ext == 0 || (ext & ~(EntityExtShot | EntityExtDoor | EntityExtRide | EntityExtTram | EntityExtFlight)) != 0))
                throw Bad("an entity record's extension byte is empty or sets bits that are not defined");
            ShotState? shot = (ext & EntityExtShot) != 0 ? ReadShot(ref r) : null;
            DoorState door = (ext & EntityExtDoor) != 0 ? ReadDoor(ref r) : null;
            RideState ride = (ext & EntityExtRide) != 0 ? ReadRide(ref r, tick) : RideState.None;
            Anomaly.Core.Motion.TramRide tram = (ext & EntityExtTram) != 0 ? ReadTram(ref r, tick) : Anomaly.Core.Motion.TramRide.None;
            if ((ext & EntityExtTram) != 0 && tram.IsNone)
                throw Bad("an entity record flags a tram trip and it has none");
            Anomaly.Core.Motion.FlightArc flight = (ext & EntityExtFlight) != 0 ? ReadFlight(ref r, tick) : Anomaly.Core.Motion.FlightArc.None;
            if ((ext & EntityExtFlight) != 0 && flight.IsNone)
                throw Bad("an entity record flags a flight and it has none");
            if ((ext & EntityExtRide) != 0 && ride.IsNone)
                throw Bad("an entity record flags a ride and it has none");

            list.Add(new EntityState(id, type, x, y, z, dir, name, glide, lastMove)
            {
                HasPos = (flags & 1) != 0,
                PosX = posX,
                PosY = posY,
                Yaw = yaw,
                Posture = posture,
                Appearance = appearance,
                Light = light,
                Flags = entityFlags,
                FlickAppearance = flick,
                FlickTick = flickTick,
                Animation = animation,
                IsVisual = (flags & 64) != 0,
                VisHost = visHost,
                Shot = shot,
                Door = door,
                Ride = ride,
                Tram = tram,
                Flight = flight,
            });
        }
    }

    private static long EntityStatesSize(List<EntityState> list)
    {
        long size = 10;
        foreach (EntityState e in list)
            size += MaxEntityStateFixedBytes + 1 + MaxUtf8(e.Name, Limits.MaxEntityNameBytes) + MaxAnimationBytes(e.Animation) + MaxDoorBytes(e.Door);
        return size;
    }

    // The change mask is a varuint over the fields in this order (the common ones first, so a mover's Pos and Yaw fit one
    // byte): Pos, Yaw, X, Y, Z, Dir, LastMoveTick | Appearance, Posture, Name, GlideSize, Light, Flags, Flick, Animation, VisHost, Shot (version 34).
    // The values follow in that order.
    private static readonly NetField[] WireOrder =
    {
        NetField.Pos, NetField.Yaw, NetField.X, NetField.Y, NetField.Z, NetField.Dir, NetField.LastMoveTick,
        NetField.Appearance, NetField.Posture, NetField.Name, NetField.GlideSize, NetField.Light, NetField.Flags, NetField.Flick,
        NetField.Animation, NetField.VisHost, NetField.Shot, NetField.Door, NetField.Ride, NetField.Tram, NetField.Flight,
    };

    private static uint ToWireMask(NetField mask)
    {
        uint wire = 0;
        for (int i = 0; i < WireOrder.Length; i++)
            if ((mask & WireOrder[i]) != 0)
                wire |= 1u << i;
        return wire;
    }

    private static NetField FromWireMask(uint wire)
    {
        var mask = NetField.None;
        for (int i = 0; i < WireOrder.Length; i++)
            if ((wire & (1u << i)) != 0)
                mask |= WireOrder[i];
        return mask;
    }

    private static void WriteChange(ref WireWriter w, in EntityChange change, long tick)
    {
        NetField mask = change.Mask;
        if (mask == NetField.None || (mask & ~NetField.Changeable) != 0)
            throw Bad("a change mask that is empty or holds a bit that is not a changeable field");
        w.WriteVarUInt(ToWireMask(mask));
        if ((mask & NetField.Pos) != 0)
            WritePos(ref w, change.PosX, change.PosY);
        if ((mask & NetField.Yaw) != 0)
            w.WriteByte(change.Yaw);
        if ((mask & NetField.X) != 0)
            w.WriteVarUInt(CheckCoordinate(change.X, Limits.MaxMapSize, "x"));
        if ((mask & NetField.Y) != 0)
            w.WriteVarUInt(CheckCoordinate(change.Y, Limits.MaxMapSize, "y"));
        if ((mask & NetField.Z) != 0)
            w.WriteVarUInt(CheckCoordinate(change.Z, Limits.MaxMapLevels, "z"));
        if ((mask & NetField.Dir) != 0)
        {
            CheckDir(change.Dir);
            w.WriteByte(change.Dir);
        }

        if ((mask & NetField.LastMoveTick) != 0)
            WriteMoveTick(ref w, change.LastMoveTick, tick);
        if ((mask & NetField.Appearance) != 0)
            WriteEntityAppearance(ref w, change.Appearance);
        if ((mask & NetField.Posture) != 0)
        {
            CheckPosture(change.Posture);
            w.WriteByte(change.Posture);
        }

        if ((mask & NetField.Name) != 0)
            w.WriteString(change.Name, Limits.MaxEntityNameBytes);
        if ((mask & NetField.GlideSize) != 0)
        {
            CheckGlide(change.GlideSize);
            w.WriteSingle(change.GlideSize);
        }

        if ((mask & NetField.Light) != 0)
            WriteLight(ref w, change.Light);
        if ((mask & NetField.Flags) != 0)
        {
            CheckEntityFlags(change.Flags);
            w.WriteByte((byte)change.Flags);
        }

        if ((mask & NetField.Flick) != 0)
            WriteFlick(ref w, change.FlickAppearance, change.FlickTick, tick);
        if ((mask & NetField.Animation) != 0)
            WriteAnimation(ref w, change.Animation, tick);
        if ((mask & NetField.VisHost) != 0)
            WriteVisHost(ref w, change.VisHost, change.Id);
        if ((mask & NetField.Shot) != 0)
            WriteShot(ref w, change.Shot);
        if ((mask & NetField.Door) != 0)
            WriteDoor(ref w, change.Door);
        if ((mask & NetField.Ride) != 0)
            WriteRide(ref w, change.Ride, tick);
        if ((mask & NetField.Tram) != 0)
            WriteTram(ref w, change.Tram, tick);
        if ((mask & NetField.Flight) != 0)
            WriteFlight(ref w, change.Flight, tick);
    }

    // Version 10: a visual's host is the id of the entity it is drawn on, or 0 for its own turf; never itself, never negative.
    private static void WriteVisHost(ref WireWriter w, long host, long id)
    {
        if (host < 0 || host == id)
            throw Bad("a visual's host " + host + " is negative or the visual itself");
        w.WriteVarUInt((ulong)host);
    }

    private static long ReadVisHost(ref WireReader r, long id)
    {
        ulong host = r.ReadVarUInt();
        if (host > long.MaxValue || (long)host == id)
            throw Bad("a visual's host is out of range or the visual itself");
        return (long)host;
    }

    // Version 8: a flick is its appearance (a public reference, never none) and the ticks since it started (like a move tick).
    private static void WriteFlick(ref WireWriter w, TableRef appearance, long flickTick, long tick)
    {
        if (appearance.IsNone)
            throw Bad("a flick without an appearance");
        WriteEntityAppearance(ref w, appearance);
        if (flickTick < 0 || flickTick > tick)
            throw Bad("a flick's tick " + flickTick + " is outside 0 to the message's tick " + tick);
        w.WriteVarUInt((ulong)(tick - flickTick));
    }

    private static void ReadFlick(ref WireReader r, long tick, int appearanceLimit, out TableRef appearance, out long flickTick)
    {
        appearance = ReadEntityAppearance(ref r, appearanceLimit);
        if (appearance.IsNone)
            throw Bad("a flick without an appearance");
        ulong ago = r.ReadVarUInt();
        if (ago > (ulong)tick)
            throw Bad("a flick starts before tick 0");
        flickTick = tick - (long)ago;
    }

    private static EntityChange ReadChange(ref WireReader r, long id, long tick)
    {
        ulong bits = r.ReadVarUInt();
        if (bits == 0 || bits >> WireOrder.Length != 0)
            throw Bad("a change mask that is empty or holds a bit that is not a changeable field");
        NetField mask = FromWireMask((uint)bits);
        var change = new EntityChange { Id = id, Mask = mask };
        if ((mask & NetField.Pos) != 0)
            ReadPos(ref r, out change.PosX, out change.PosY);
        if ((mask & NetField.Yaw) != 0)
            change.Yaw = r.ReadByte();
        if ((mask & NetField.X) != 0)
            change.X = ReadRanged(ref r, 1, Limits.MaxMapSize, "an entity x");
        if ((mask & NetField.Y) != 0)
            change.Y = ReadRanged(ref r, 1, Limits.MaxMapSize, "an entity y");
        if ((mask & NetField.Z) != 0)
            change.Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "an entity z");
        if ((mask & NetField.Dir) != 0)
        {
            change.Dir = r.ReadByte();
            CheckDir(change.Dir);
        }

        if ((mask & NetField.LastMoveTick) != 0)
            change.LastMoveTick = ReadMoveTick(ref r, tick);
        if ((mask & NetField.Appearance) != 0)
            change.Appearance = ReadEntityAppearance(ref r, Limits.MaxAppearanceTableEntries);
        if ((mask & NetField.Posture) != 0)
        {
            change.Posture = r.ReadByte();
            CheckPosture(change.Posture);
        }

        if ((mask & NetField.Name) != 0)
            change.Name = r.ReadString(Limits.MaxEntityNameBytes);
        if ((mask & NetField.GlideSize) != 0)
        {
            change.GlideSize = r.ReadSingle();
            CheckGlide(change.GlideSize);
        }

        if ((mask & NetField.Light) != 0)
            change.Light = ReadLight(ref r);
        if ((mask & NetField.Flags) != 0)
        {
            change.Flags = (EntityFlags)r.ReadByte();
            CheckEntityFlags(change.Flags);
        }

        if ((mask & NetField.Flick) != 0)
            ReadFlick(ref r, tick, Limits.MaxAppearanceTableEntries, out change.FlickAppearance, out change.FlickTick);
        if ((mask & NetField.Animation) != 0)
            change.Animation = ReadAnimation(ref r, tick);
        if ((mask & NetField.VisHost) != 0)
            change.VisHost = ReadVisHost(ref r, id);
        if ((mask & NetField.Shot) != 0)
            change.Shot = ReadShot(ref r);
        if ((mask & NetField.Door) != 0)
            change.Door = ReadDoor(ref r);
        if ((mask & NetField.Ride) != 0)
            change.Ride = ReadRide(ref r, tick);
        if ((mask & NetField.Tram) != 0)
            change.Tram = ReadTram(ref r, tick);
        if ((mask & NetField.Flight) != 0)
            change.Flight = ReadFlight(ref r, tick);

        return change;
    }

    // An entity's last move tick travels as the ticks since it (at most the message's tick: it never lies in the future).
    private static void WriteMoveTick(ref WireWriter w, long lastMoveTick, long tick)
    {
        if (lastMoveTick < 0 || lastMoveTick > tick)
            throw Bad("an entity's last move tick " + lastMoveTick + " is outside 0 to the message's tick " + tick);
        w.WriteVarUInt((ulong)(tick - lastMoveTick));
    }

    private static long ReadMoveTick(ref WireReader r, long tick)
    {
        ulong ago = r.ReadVarUInt();
        if (ago > (ulong)tick)
            throw Bad("an entity's last move is " + ago + " ticks ago, before tick 0");
        return tick - (long)ago;
    }

    // A public appearance reference of an entity: the index only (varuint, 0 for none), never a private entry (D40).
    private static void WriteEntityAppearance(ref WireWriter w, TableRef appearance)
    {
        if (appearance.Private || appearance.Index > (uint)Limits.MaxAppearanceTableEntries)
            throw Bad("an entity's appearance is private or outside its table");
        w.WriteVarUInt(appearance.Index);
    }

    private static TableRef ReadEntityAppearance(ref WireReader r, int limit)
    {
        ulong index = r.ReadVarUInt();
        if (index > (ulong)limit)
            throw Bad("an entity's appearance is outside its table");
        return new TableRef((uint)index, false);
    }

    private static void CheckEntityFlags(EntityFlags flags)
    {
        if ((flags & ~EntityFlags.All) != 0)
            throw Bad("an entity flags byte " + (byte)flags + " sets bits that are not defined");
    }

    private static void CheckPosture(byte posture)
    {
        // version 99 (DV686): the turned side's two bits, only on a lying body, face up only with the side fixed
        if ((posture & PostureBits.SideFixed) != 0 && (posture & PostureBits.Lying) == 0 || (posture & (PostureBits.SideFixed | PostureBits.FaceUp)) == PostureBits.FaceUp)
            throw Bad("a posture byte " + posture + " sets a turned side that is not defined");
    }

    // ---- ids: zigzag differences from the previous id of the list (0 before the first) ----

    private static void WriteId(ref WireWriter w, long id, long previous, bool strictlyIncreasing)
    {
        if (id < 1)
            throw Bad("an entity id that is not positive");
        if (id == previous || (strictlyIncreasing && id < previous))
            throw Bad(strictlyIncreasing ? "snapshot entities must be in increasing id order" : "an entity id repeated next to itself");
        w.WriteVarInt(id - previous);
    }

    private static long ReadId(ref WireReader r, long previous, bool strictlyIncreasing)
    {
        long diff = r.ReadVarInt();
        if (diff == 0 || (strictlyIncreasing && diff < 0))
            throw Bad(strictlyIncreasing ? "snapshot entities must be in increasing id order" : "an entity id repeated next to itself");
        if (diff > 0)
        {
            if (previous > long.MaxValue - diff)
                throw Bad("an entity id past the range of an id");
            return previous + diff;
        }

        if (diff == long.MinValue || previous + diff < 1)
            throw Bad("an entity id that is not positive");
        return previous + diff;
    }

    // ---- field validation (the same rules at the encoder and the decoder) ----

    private static void WriteType(ref WireWriter w, int type)
    {
        if (type < 0 || type >= Limits.MaxTypeTableEntries)
            throw Bad("a type index outside the table");
        w.WriteVarUInt((ulong)type);
    }

    private static int ReadType(ref WireReader r, int limit)
    {
        ulong type = r.ReadVarUInt();
        if (type >= (ulong)limit)
            throw Bad("a type index outside the table");
        return (int)type;
    }

    private static void WriteCoordinates(ref WireWriter w, int x, int y, int z)
    {
        w.WriteVarUInt(CheckCoordinate(x, Limits.MaxMapSize, "x"));
        w.WriteVarUInt(CheckCoordinate(y, Limits.MaxMapSize, "y"));
        w.WriteVarUInt(CheckCoordinate(z, Limits.MaxMapLevels, "z"));
    }

    private static ulong CheckCoordinate(int value, int max, string axis)
    {
        if (value < 1 || value > max)
            throw Bad("an entity " + axis + " of " + value + " is outside 1.." + max);
        return (ulong)value;
    }

    // The eight BYOND directions, and 0 for an atom that has none.
    private static void CheckDir(byte dir)
    {
        switch (dir)
        {
            case 0:
            case 1:
            case 2:
            case 4:
            case 5:
            case 6:
            case 8:
            case 9:
            case 10:
                return;
            default:
                throw Bad("a direction " + dir + " that is none of the eight");
        }
    }

    private static void CheckGlide(float glide)
    {
        if (!(glide >= 0f && glide <= Limits.MaxGlideSize))
            throw Bad("a glide size outside 0.." + Limits.MaxGlideSize);
    }

    private static void CheckNonNegative(long value, string what)
    {
        if (value < 0)
            throw Bad(what + " is negative");
    }

    private static long ReadNonNegativeLong(ref WireReader r, string what)
    {
        ulong value = r.ReadVarUInt();
        if (value > long.MaxValue)
            throw Bad(what + " is over 63 bits");
        return (long)value;
    }

    private static int ReadNonNegativeInt(ref WireReader r, string what)
    {
        ulong value = r.ReadVarUInt();
        if (value > int.MaxValue)
            throw Bad(what + " is over 31 bits");
        return (int)value;
    }

    private static uint ReadUInt32Varint(ref WireReader r, string what)
    {
        ulong value = r.ReadVarUInt();
        if (value > uint.MaxValue)
            throw Bad(what + " is over 32 bits");
        return (uint)value;
    }

    private static int ReadRanged(ref WireReader r, int min, int max, string what)
    {
        ulong value = r.ReadVarUInt();
        if (value < (ulong)min || value > (ulong)max)
            throw Bad(what + " is outside " + min + ".." + max);
        return (int)value;
    }

    private static string ReadTypePath(ref WireReader r)
    {
        string path = r.ReadString(Limits.MaxTypePathBytes);
        if (path == null)
            throw new ProtocolException(ProtocolError.BadString, "a null type path");
        return path;
    }

    // ---- plumbing ----

    private static bool Begin(ReadOnlySpan<byte> message, MessageId expected, int maxLength, out ProtocolError error)
    {
        if (message.Length == 0)
        {
            error = ProtocolError.Truncated;
            return false;
        }

        if (message.Length > maxLength)
        {
            error = ProtocolError.TooLong;
            return false;
        }

        if (!WireReader.IsKnownMessageId(message[0]))
        {
            error = ProtocolError.UnknownMessage;
            return false;
        }

        if ((MessageId)message[0] != expected)
        {
            error = ProtocolError.WrongMessage;
            return false;
        }

        error = ProtocolError.None;
        return true;
    }

    private static int Finish(ref WireWriter w, int maxLength)
    {
        if (w.Position > maxLength)
            throw new ProtocolException(ProtocolError.TooLong, "a message of " + w.Position + " bytes is over its cap of " + maxLength);
        return w.Position;
    }

    private delegate int EncodeInto(Span<byte> buffer);

    // Encodes into a pooled buffer of at most the message cap, then copies the exact size out.
    private static byte[] EncodeToArray(long bound, EncodeInto encode)
    {
        bool capped = bound > Limits.MaxOutboundMessageBytes;
        int size = (int)Math.Min(bound, Limits.MaxOutboundMessageBytes);
        byte[] rented = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            int length;
            try
            {
                length = encode(rented.AsSpan(0, size));
            }
            catch (ProtocolException e) when (capped && e.Error == ProtocolError.BufferTooSmall)
            {
                throw new ProtocolException(ProtocolError.TooLong, "the message is over its cap of " + Limits.MaxOutboundMessageBytes + " bytes");
            }

            return rented.AsSpan(0, length).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int MaxUtf8(string value, int cap) =>
        value == null ? 0 : Math.Min(cap, Encoding.UTF8.GetMaxByteCount(value.Length));

    private static ProtocolException Bad(string message) => new(ProtocolError.BadValue, message);
}
