namespace Anomaly.Protocol;

// Version 100 also (MessagesV100Hotbar.cs, docs/ai/specs/protocol.md section 99; ui-overhaul.md section 10, slice 3A, DV693):
//
//   Hotbar (server to client, State): [u8 id = 34] [u8 flags: bit 0 the bar (a human), bit 1 a belt worn; bits 2-7 zero]
//     ten cells, cell 1 first: { [u8 state: 0 empty, 1 holding, 2 out] ([tableref picture (may be none)] [varuint item entity >= 1] unless empty) }
//     [str(1..128) left hand's item name, or none] [str(1..128) right hand's] [u8 refused cell 0..10]
//   without the bar every cell is empty, no belt, no names and no refusal; without a belt cells 1 to 7 are empty.
//   a tableref is a varuint (appearance index << 1) | private, as everywhere (Codec.V2).
//
//   HotbarPress (Action kind 28, client to server, Control): [u8 cell 1..10] (Codec.V2's action).
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes. Whether the
// appearances exist is the replica's check.

public static partial class Codec
{
    private const byte HotbarFlagBar = 1;
    private const byte HotbarFlagBelt = 2;

    /// <summary>Encodes a <see cref="HotbarMessage"/> for the State channel (at most <see cref="Limits.MaxHotbarBytes"/>).</summary>
    public static byte[] Encode(HotbarMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var bytes = new byte[Limits.MaxHotbarBytes];
        var w = new WireWriter(bytes);
        w.WriteMessageId(MessageId.Hotbar);
        CheckHotbarFlags(message.Bar, message.BeltWorn);
        w.WriteByte((byte)((message.Bar ? HotbarFlagBar : 0) | (message.BeltWorn ? HotbarFlagBelt : 0)));
        for (int i = 0; i < HotbarMessage.CellCount; i++)
        {
            HotbarCell cell = message.Cells[i];
            if (cell.State > HotbarCellState.Out)
                throw Bad("a hotbar cell state " + (byte)cell.State);
            CheckHotbarCell(message, i, cell.State);
            w.WriteByte((byte)cell.State);
            if (cell.State == HotbarCellState.Empty)
            {
                if (!cell.Appearance.IsNone || cell.Entity != 0)
                    throw Bad("a picture or an item in an empty hotbar cell");
                continue;
            }
            WritePlainRef(ref w, cell.Appearance, true, Limits.MaxAppearanceTableEntries, "a hotbar cell's picture");
            // slice 4B-2: the cell's item, so the Tab inventory names the one it holds
            if (cell.Entity < 1)
                throw Bad("a hotbar cell's item " + cell.Entity);
            w.WriteVarUInt((ulong)cell.Entity);
        }
        WriteHotbarName(ref w, message.LeftHand, message.Bar);
        WriteHotbarName(ref w, message.RightHand, message.Bar);
        if (message.RefusedCell > HotbarMessage.CellCount || (!message.Bar && message.RefusedCell != 0))
            throw Bad("a hotbar refusal of cell " + message.RefusedCell);
        w.WriteByte(message.RefusedCell);
        return bytes.AsSpan(0, Finish(ref w, Limits.MaxHotbarBytes)).ToArray();
    }

    /// <summary>Decodes a <see cref="HotbarMessage"/>: known flags, ten cells of a known state (empty where there is no bar or no belt), names within their cap, a refused cell of 0 to 10.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out HotbarMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Hotbar, Limits.MaxHotbarBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte flags = r.ReadByte();
            if ((flags & ~(HotbarFlagBar | HotbarFlagBelt)) != 0)
                throw Bad("a hotbar with unknown flag bits");
            bool bar = (flags & HotbarFlagBar) != 0, belt = (flags & HotbarFlagBelt) != 0;
            CheckHotbarFlags(bar, belt);
            var probe = new HotbarMessage { Bar = bar, BeltWorn = belt };
            var cells = new HotbarCell[HotbarMessage.CellCount];
            for (int i = 0; i < HotbarMessage.CellCount; i++)
            {
                byte state = r.ReadByte();
                if (state > (byte)HotbarCellState.Out)
                    throw Bad("a hotbar cell state " + state);
                CheckHotbarCell(probe, i, (HotbarCellState)state);
                if (state == 0)
                {
                    cells[i] = HotbarCell.Empty;
                    continue;
                }
                TableRef picture = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "a hotbar cell's picture");
                long item = ReadNonNegativeLong(ref r, "a hotbar cell's item");
                if (item < 1)
                    throw Bad("a hotbar cell's item " + item);
                cells[i] = new HotbarCell((HotbarCellState)state, picture, item);
            }
            string left = ReadHotbarName(ref r, bar);
            string right = ReadHotbarName(ref r, bar);
            byte refused = r.ReadByte();
            if (refused > HotbarMessage.CellCount || (!bar && refused != 0))
                throw Bad("a hotbar refusal of cell " + refused);
            r.ExpectEnd();
            value = new HotbarMessage { Bar = bar, BeltWorn = belt, Cells = cells, LeftHand = left, RightHand = right, RefusedCell = refused };
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static void CheckHotbarFlags(bool bar, bool belt)
    {
        if (belt && !bar)
            throw Bad("a hotbar's belt without the bar");
    }

    // Without the bar no cell holds anything; without a belt no belt cell does.
    private static void CheckHotbarCell(HotbarMessage message, int index, HotbarCellState state)
    {
        if (state == HotbarCellState.Empty)
            return;
        if (!message.Bar)
            throw Bad("a hotbar cell without the bar");
        if (index < HotbarMessage.BeltCells && !message.BeltWorn)
            throw Bad("a hotbar belt cell without a belt");
    }

    private static void WriteHotbarName(ref WireWriter w, string name, bool bar)
    {
        if (name != null && (name.Length == 0 || !bar))
            throw Bad("a hotbar hand's name that is empty or without the bar");
        w.WriteString(name, Limits.MaxHotbarNameBytes);
    }

    private static string ReadHotbarName(ref WireReader r, bool bar)
    {
        string name = r.ReadString(Limits.MaxHotbarNameBytes);
        if (name != null && (name.Length == 0 || !bar))
            throw Bad("a hotbar hand's name that is empty or without the bar");
        return name;
    }
}
