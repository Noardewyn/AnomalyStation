using System.Buffers.Binary;

namespace Anomaly.Protocol;

// Version 16 (MessagesV16.cs, docs/ai/specs/protocol.md section 25):
//
//   UiWindow (server to client, State): [u8 id = 16] [u8 kind: 0 update, 1 close] [varuint window 1..200]
//     update: [str(1..64) interface] [str(0..256) title] [u8 status + 1: 0..3]
//             [u8 flags: bit 0 refreshing, bit 1 data, bit 2 static data; bits 3-7 zero]
//             (data: [value]) (static data: [value])
//     close:  nothing more
//   value: [u8 tag] then by tag:
//     0 null, 1 false, 2 true: nothing
//     3 integer:  [varint zigzag]                 (a number that is a whole number of at most 53 bits)
//     4 number:   [u64 IEEE 754 double, LE]      (any other finite number)
//     5 string:   [str(4096)]                     (never the null string)
//     6 array:    [count] { value }*
//     7 object:   [count] { [str(128) key] value }*
//   A value nests at most 16 deep and a payload holds at most 32768 values.
//
//   UiAct (Action kind 11, client to server, Control): [varuint window 1..200] [str(1..48) action] [value params: an object or null,
//          at most 140 bytes]
//   UiClose (Action kind 12): [varuint window 1..200]
//
// Every rule of the earlier codecs holds: TryDecode never throws, every count is capped before anything is allocated (a value of a
// count takes at least one byte each), and a message that decodes encodes back to the same bytes (the integer tag is chosen
// exactly when a number is a whole number in range, so a decoded number re-encodes with its tag).

public static partial class Codec
{
    private const byte UiTagNull = 0, UiTagFalse = 1, UiTagTrue = 2, UiTagInteger = 3, UiTagNumber = 4, UiTagString = 5, UiTagArray = 6, UiTagObject = 7;
    private const double UiMaxInteger = 9007199254740991; // 2^53 - 1

    /// <summary>Encodes a <see cref="UiWindowMessage"/> for the State channel (at most <see cref="Limits.MaxUiWindowBytes"/>).</summary>
    public static int Encode(UiWindowMessage message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.UiWindow);
        if (message.Kind > UiWindowKind.Close)
            throw Bad("an unknown UI window kind " + (byte)message.Kind);
        w.WriteByte((byte)message.Kind);
        if (message.Window < 1 || message.Window > Limits.MaxUiWindows)
            throw Bad("a UI window " + message.Window + " outside 1.." + Limits.MaxUiWindows);
        w.WriteVarUInt((ulong)message.Window);
        if (message.Kind == UiWindowKind.Close)
        {
            if (message.Interface != null || message.Title != null || message.Status != 0 || message.Refreshing || message.Data != null || message.StaticData != null)
                throw Bad("a UI window close with an update's fields");
            return Finish(ref w, Limits.MaxUiWindowBytes);
        }
        if (string.IsNullOrEmpty(message.Interface))
            throw new ProtocolException(ProtocolError.BadString, "a UI window update without an interface");
        w.WriteString(message.Interface, Limits.MaxUiInterfaceBytes);
        w.WriteString(message.Title ?? "", Limits.MaxUiTitleBytes);
        if (message.Status < -1 || message.Status > 2)
            throw Bad("a UI window status " + message.Status);
        w.WriteByte((byte)(message.Status + 1));
        byte flags = (byte)((message.Refreshing ? 1 : 0) | (message.Data != null ? 2 : 0) | (message.StaticData != null ? 4 : 0));
        w.WriteByte(flags);
        int budget = Limits.MaxUiValues;
        if (message.Data != null)
            WriteUiValue(ref w, message.Data, 0, ref budget);
        if (message.StaticData != null)
            WriteUiValue(ref w, message.StaticData, 0, ref budget);
        return Finish(ref w, Limits.MaxUiWindowBytes);
    }

    /// <summary>The encoded <see cref="UiWindowMessage"/>.</summary>
    public static byte[] Encode(UiWindowMessage message) =>
        EncodeToArray(Limits.MaxUiWindowBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="UiWindowMessage"/>: the kind, a window in range, a named interface, a status in range, known flags, values within the caps.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out UiWindowMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.UiWindow, Limits.MaxUiWindowBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte kind = r.ReadByte();
            if (kind > (byte)UiWindowKind.Close)
                throw Bad("an unknown UI window kind " + kind);
            ulong window = r.ReadVarUInt();
            if (window < 1 || window > Limits.MaxUiWindows)
                throw Bad("a UI window " + window + " outside 1.." + Limits.MaxUiWindows);
            var result = new UiWindowMessage { Kind = (UiWindowKind)kind, Window = (int)window };
            if (result.Kind == UiWindowKind.Update)
            {
                result.Interface = r.ReadString(Limits.MaxUiInterfaceBytes);
                if (string.IsNullOrEmpty(result.Interface))
                    throw new ProtocolException(ProtocolError.BadString, "a UI window update without an interface");
                result.Title = r.ReadString(Limits.MaxUiTitleBytes);
                if (result.Title == null)
                    throw new ProtocolException(ProtocolError.BadString, "a UI window update with a null title");
                byte status = r.ReadByte();
                if (status > 3)
                    throw Bad("a UI window status " + (status - 1));
                result.Status = status - 1;
                byte flags = r.ReadByte();
                if ((flags & ~7) != 0)
                    throw Bad("a UI window update with unknown flag bits");
                result.Refreshing = (flags & 1) != 0;
                int budget = Limits.MaxUiValues;
                if ((flags & 2) != 0)
                    result.Data = ReadUiValue(ref r, 0, ref budget);
                if ((flags & 4) != 0)
                    result.StaticData = ReadUiValue(ref r, 0, ref budget);
            }
            r.ExpectEnd();
            value = result;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    /// <summary>The bytes a UI value takes on the wire (a <see cref="ActionKind.UiAct"/>'s params are capped by it).</summary>
    public static int UiValueBytes(UiValue value)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxUiParamsBytes + 1];
        var w = new WireWriter(buffer);
        int budget = Limits.MaxUiValues;
        try
        {
            WriteUiValue(ref w, value, 0, ref budget);
        }
        catch (ProtocolException e) when (e.Error == ProtocolError.BufferTooSmall)
        {
            return int.MaxValue;
        }
        return w.Position;
    }

    private static void WriteUiValue(ref WireWriter w, UiValue value, int depth, ref int budget)
    {
        if (value == null)
            throw Bad("a UI value that is not there");
        if (depth > Limits.MaxUiDepth)
            throw Bad("a UI value nested deeper than " + Limits.MaxUiDepth);
        if (--budget < 0)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a UI payload of more than " + Limits.MaxUiValues + " values");
        switch (value.Kind)
        {
            case UiValueKind.Null:
                w.WriteByte(UiTagNull);
                break;
            case UiValueKind.False:
                w.WriteByte(UiTagFalse);
                break;
            case UiValueKind.True:
                w.WriteByte(UiTagTrue);
                break;
            case UiValueKind.Number:
                double n = value.Number;
                if (!double.IsFinite(n))
                    throw Bad("a NaN or infinite UI number");
                if (IsUiInteger(n))
                {
                    w.WriteByte(UiTagInteger);
                    w.WriteVarInt((long)n);
                }
                else
                {
                    w.WriteByte(UiTagNumber);
                    w.WriteUInt64((ulong)BitConverter.DoubleToInt64Bits(n));
                }
                break;
            case UiValueKind.String:
                w.WriteByte(UiTagString);
                w.WriteString(value.Text ?? "", Limits.MaxUiStringBytes);
                break;
            case UiValueKind.Array:
                w.WriteByte(UiTagArray);
                w.WriteCount(value.Items.Count, Limits.MaxUiValues);
                foreach (UiValue item in value.Items)
                    WriteUiValue(ref w, item, depth + 1, ref budget);
                break;
            case UiValueKind.Object:
                w.WriteByte(UiTagObject);
                w.WriteCount(value.Fields.Count, Limits.MaxUiValues);
                foreach (KeyValuePair<string, UiValue> field in value.Fields)
                {
                    w.WriteString(field.Key ?? "", Limits.MaxUiKeyBytes);
                    WriteUiValue(ref w, field.Value, depth + 1, ref budget);
                }
                break;
            default:
                throw Bad("an unknown UI value kind " + (byte)value.Kind);
        }
    }

    private static UiValue ReadUiValue(ref WireReader r, int depth, ref int budget)
    {
        if (depth > Limits.MaxUiDepth)
            throw Bad("a UI value nested deeper than " + Limits.MaxUiDepth);
        if (--budget < 0)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a UI payload of more than " + Limits.MaxUiValues + " values");
        byte tag = r.ReadByte();
        switch (tag)
        {
            case UiTagNull:
                return UiValue.Null;
            case UiTagFalse:
                return UiValue.False;
            case UiTagTrue:
                return UiValue.True;
            case UiTagInteger:
                long whole = r.ReadVarInt();
                if (whole > (long)UiMaxInteger || whole < -(long)UiMaxInteger)
                    throw Bad("a UI integer over 53 bits");
                return UiValue.WrapNumber(whole);
            case UiTagNumber:
                double n = BitConverter.Int64BitsToDouble((long)r.ReadUInt64());
                if (!double.IsFinite(n))
                    throw Bad("a NaN or infinite UI number");
                if (IsUiInteger(n))
                    throw Bad("a whole UI number sent as a double"); // one encoding per value
                return UiValue.WrapNumber(n);
            case UiTagString:
                string text = r.ReadString(Limits.MaxUiStringBytes);
                if (text == null)
                    throw new ProtocolException(ProtocolError.BadString, "a null UI string");
                return UiValue.WrapString(text);
            case UiTagArray:
            {
                int count = r.ReadCount(Limits.MaxUiValues, 1);
                var items = new List<UiValue>(Math.Min(count, 256));
                for (int i = 0; i < count; i++)
                    items.Add(ReadUiValue(ref r, depth + 1, ref budget));
                return UiValue.WrapArray(items);
            }
            case UiTagObject:
            {
                int count = r.ReadCount(Limits.MaxUiValues, 2);
                var fields = new List<KeyValuePair<string, UiValue>>(Math.Min(count, 256));
                for (int i = 0; i < count; i++)
                {
                    string key = r.ReadString(Limits.MaxUiKeyBytes);
                    if (key == null)
                        throw new ProtocolException(ProtocolError.BadString, "a null UI field name");
                    fields.Add(new KeyValuePair<string, UiValue>(key, ReadUiValue(ref r, depth + 1, ref budget)));
                }
                return UiValue.WrapObject(fields);
            }
            default:
                throw Bad("an unknown UI value tag " + tag);
        }
    }

    private static bool IsUiInteger(double n) => Math.Floor(n) == n && Math.Abs(n) <= UiMaxInteger && !(n == 0 && double.IsNegative(n));

    // ---- the version 16 actions (called from Codec.V2's Action codec) ----

    private static void WriteUiAction(ref WireWriter w, in ActionMessage message)
    {
        if (message.UiWindow < 1 || message.UiWindow > Limits.MaxUiWindows)
            throw Bad("a UI action on window " + message.UiWindow);
        w.WriteVarUInt((ulong)message.UiWindow);
        if (message.Kind == ActionKind.UiClose)
        {
            if (message.UiAction != null || message.UiParams != null)
                throw Bad("a UI close with an action or params");
            return;
        }
        if (string.IsNullOrEmpty(message.UiAction))
            throw new ProtocolException(ProtocolError.BadString, "a UI act without an action");
        w.WriteString(message.UiAction, Limits.MaxUiActionBytes);
        UiValue parameters = message.UiParams ?? UiValue.Null;
        if (parameters.Kind != UiValueKind.Object && parameters.Kind != UiValueKind.Null)
            throw Bad("a UI act whose params are not an object");
        int start = w.Position;
        int budget = Limits.MaxUiValues;
        WriteUiValue(ref w, parameters, 0, ref budget);
        if (w.Position - start > Limits.MaxUiParamsBytes)
            throw new ProtocolException(ProtocolError.TooLong, "a UI act's params over " + Limits.MaxUiParamsBytes + " bytes");
    }

    private static void ReadUiAction(ref WireReader r, ActionKind kind, out int window, out string action, out UiValue parameters)
    {
        ulong index = r.ReadVarUInt();
        if (index < 1 || index > Limits.MaxUiWindows)
            throw Bad("a UI action on window " + index);
        window = (int)index;
        action = null;
        parameters = null;
        if (kind == ActionKind.UiClose)
            return;
        action = r.ReadString(Limits.MaxUiActionBytes);
        if (string.IsNullOrEmpty(action))
            throw new ProtocolException(ProtocolError.BadString, "a UI act without an action");
        int start = r.Position;
        int budget = Limits.MaxUiValues;
        UiValue read = ReadUiValue(ref r, 0, ref budget);
        if (r.Position - start > Limits.MaxUiParamsBytes)
            throw new ProtocolException(ProtocolError.TooLong, "a UI act's params over " + Limits.MaxUiParamsBytes + " bytes");
        if (read.Kind != UiValueKind.Object && read.Kind != UiValueKind.Null)
            throw Bad("a UI act whose params are not an object");
        parameters = read.Kind == UiValueKind.Null ? null : read;
    }
}
