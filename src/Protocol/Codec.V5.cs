namespace Anomaly.Protocol;

// M4b slice 3 (MessagesV5.cs). The version-5 layout behind the existing entry points (docs/ai/specs/protocol.md section 15):
//
//   MapGrid, after the version-3 fields:
//     [count <= 32] { [str(32) gas kind] }*                           the gas kinds (kind 1 is the first)
//     [count] { [cell] [gases] }*                                     the cells that show gas
//   Delta body, after the version-3 part, when the parts byte has PartV5:
//     [count >= 1] { [cell] [gases] }*                                the cells whose gas changed (gases may be empty)
//   [gases] = [count <= 16] { [u8 kind 1..kinds] [u8 level 1..80] }*
//
//   DevGasReport (server to client, Control): [u8 id = 12] [u8 x0 1..255] [u8 y0 1..255] [varuint z 1..16]
//     [u8 width 1..15] [u8 height 1..15] [count <= 32] { [str(48) id] [str(48) name] [u32 rgb] }*
//     then width x height cells: [u8 kind 0..3] and, for an open or space cell: [f32 pressure] [f32 temperature]
//     [count <= 32] { [u8 gas] [f32 moles] }*
//
// Every rule of the earlier codecs holds: TryDecode never throws, every count is capped before anything is allocated, and
// a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    private const int PartV5 = 64;
    // A cell (three varuints) and its gas count: a Delta's cleared cell; a MapGrid cell has at least one gas (two bytes more).
    private const int MinGasCellBytes = 3 + 1;

    // ---- the gases of a cell ----

    private static void WriteGases(ref WireWriter w, GasLevel[] gases, int kinds, bool allowEmpty)
    {
        gases ??= Array.Empty<GasLevel>();
        if (gases.Length == 0 && !allowEmpty)
            throw Bad("a MapGrid gas cell that shows no gas");
        w.WriteCount(gases.Length, Limits.MaxGasesPerCell);
        foreach (GasLevel g in gases)
        {
            if (g.Kind < 1 || g.Kind > kinds)
                throw Bad("a gas level names a kind outside the gas kinds table");
            if (g.Level < 1 || g.Level > Limits.MaxGasLevel)
                throw Bad("a gas level outside 1.." + Limits.MaxGasLevel);
            w.WriteByte(g.Kind);
            w.WriteByte(g.Level);
        }
    }

    private static GasLevel[] ReadGases(ref WireReader r, int kinds, bool allowEmpty)
    {
        int n = r.ReadCount(Limits.MaxGasesPerCell, 2);
        if (n == 0 && !allowEmpty)
            throw Bad("a MapGrid gas cell that shows no gas");
        var gases = n == 0 ? Array.Empty<GasLevel>() : new GasLevel[n];
        for (int i = 0; i < n; i++)
        {
            byte kind = r.ReadByte();
            byte level = r.ReadByte();
            if (kind < 1 || kind > kinds)
                throw Bad("a gas level names a kind outside the gas kinds table");
            if (level < 1 || level > Limits.MaxGasLevel)
                throw Bad("a gas level outside 1.." + Limits.MaxGasLevel);
            gases[i] = new GasLevel(kind, level);
        }

        return gases;
    }

    private static long MaxGasCellBytes(CellGasChange c) => 15 + 5 + (long)(c.Gases?.Length ?? 0) * 2;

    // ---- MapGrid ----

    private static long MaxMapGridV5Size(MapGrid m)
    {
        long size = 10 + 10;
        foreach (string kind in m.GasKinds)
            size += 5 + MaxUtf8(kind, Limits.MaxGasKindBytes);
        foreach (CellGasChange c in m.GasCells)
            size += MaxGasCellBytes(c);
        return size;
    }

    private static void WriteMapGridV5(ref WireWriter w, MapGrid m, int cells)
    {
        if (m.GasKinds.Count > Limits.MaxGasKinds)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a MapGrid of more than " + Limits.MaxGasKinds + " gas kinds");
        w.WriteCount(m.GasKinds.Count, Limits.MaxGasKinds);
        foreach (string kind in m.GasKinds)
        {
            if (kind == null)
                throw Bad("a null gas kind");
            w.WriteString(kind, Limits.MaxGasKindBytes);
        }

        w.WriteCount(m.GasCells.Count, cells);
        foreach (CellGasChange c in m.GasCells)
        {
            if (c.X > m.Width || c.Y > m.Height || c.Z > m.Levels)
                throw Bad("a MapGrid gas cell outside the map");
            WriteCell(ref w, c.X, c.Y, c.Z);
            WriteGases(ref w, c.Gases, m.GasKinds.Count, false);
        }
    }

    private static void ReadMapGridV5(ref WireReader r, MapGrid m, int cells)
    {
        int kinds = r.ReadCount(Limits.MaxGasKinds, MinStringBytes);
        m.GasKinds.Capacity = kinds;
        for (int i = 0; i < kinds; i++)
            m.GasKinds.Add(r.ReadString(Limits.MaxGasKindBytes) ?? throw Bad("a null gas kind"));
        int n = r.ReadCount(cells, MinGasCellBytes + 2);
        m.GasCells.Capacity = n;
        for (int i = 0; i < n; i++)
        {
            ReadCell(ref r, out int x, out int y, out int z);
            if (x > m.Width || y > m.Height || z > m.Levels)
                throw Bad("a MapGrid gas cell outside the map");
            m.GasCells.Add(new CellGasChange(x, y, z, ReadGases(ref r, kinds, false)));
        }
    }

    // ---- Delta ----

    private static bool HasDeltaV5(Delta d) => d.CellGasChanged.Count > 0;

    private static long MaxDeltaV5Size(Delta d)
    {
        long size = 10;
        foreach (CellGasChange c in d.CellGasChanged)
            size += MaxGasCellBytes(c);
        return size;
    }

    private static void WriteDeltaV5(ref WireWriter w, Delta d)
    {
        w.WriteCount(d.CellGasChanged.Count, Limits.MaxCellChangesPerDelta);
        foreach (CellGasChange c in d.CellGasChanged)
        {
            WriteCell(ref w, c.X, c.Y, c.Z);
            WriteGases(ref w, c.Gases, Limits.MaxGasKinds, true);
        }
    }

    private static void ReadDeltaV5(ref WireReader r, Delta d, int cellBudget)
    {
        int n = r.ReadCount(cellBudget, MinGasCellBytes);
        if (n == 0) throw Bad("a Delta flags gas changes and has none");
        d.CellGasChanged.Capacity = n;
        for (int i = 0; i < n; i++)
        {
            ReadCell(ref r, out int x, out int y, out int z);
            d.CellGasChanged.Add(new CellGasChange(x, y, z, ReadGases(ref r, Limits.MaxGasKinds, true)));
        }
    }

    // ---- DevGasReport (server to client, Control channel) ----

    private const int DevGasSide = 2 * Limits.DevGasProbeRadius + 1;

    /// <summary>Encodes a <see cref="DevGasReport"/> for the Control channel (at most <see cref="Limits.MaxDevGasReportBytes"/>).</summary>
    public static int Encode(DevGasReport message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.DevGasReport);
        if (message.X0 < 1 || message.X0 > Limits.MaxMapSize || message.Y0 < 1 || message.Y0 > Limits.MaxMapSize || message.Z < 1 || message.Z > Limits.MaxMapLevels)
            throw Bad("a dev gas report outside the map's range");
        if (message.Width < 1 || message.Width > DevGasSide || message.Height < 1 || message.Height > DevGasSide)
            throw Bad("a dev gas report of a size outside 1.." + DevGasSide);
        if (message.Cells.Count != message.Width * message.Height)
            throw Bad("a dev gas report whose cells do not fill it");
        w.WriteByte((byte)message.X0);
        w.WriteByte((byte)message.Y0);
        w.WriteVarUInt((ulong)message.Z);
        w.WriteByte((byte)message.Width);
        w.WriteByte((byte)message.Height);
        w.WriteCount(message.Gases.Count, Limits.MaxDevGases);
        foreach (DevGasInfo gas in message.Gases)
        {
            if (gas.Id == null || gas.Name == null)
                throw Bad("a dev gas without an id or a name");
            if (gas.Rgb > 0xFFFFFF)
                throw Bad("a dev gas colour over 0xFFFFFF");
            w.WriteString(gas.Id, Limits.MaxDevGasTextBytes);
            w.WriteString(gas.Name, Limits.MaxDevGasTextBytes);
            w.WriteUInt32(gas.Rgb);
        }

        foreach (DevGasCell cell in message.Cells)
        {
            if (cell.Kind > DevCellKind.Space)
                throw Bad("a dev gas cell of an unknown kind");
            w.WriteByte((byte)cell.Kind);
            if (cell.Kind is not (DevCellKind.Open or DevCellKind.Space))
            {
                if (cell.Moles is { Length: > 0 } || cell.Pressure != 0 || cell.Temperature != 0)
                    throw Bad("air on a dev gas cell that has none");
                continue;
            }

            w.WriteSingle(cell.Pressure);
            w.WriteSingle(cell.Temperature);
            DevGasAmount[] moles = cell.Moles ?? Array.Empty<DevGasAmount>();
            w.WriteCount(moles.Length, Limits.MaxDevGases);
            foreach (DevGasAmount amount in moles)
            {
                if (amount.Gas >= message.Gases.Count)
                    throw Bad("a dev gas amount names a gas outside the table");
                w.WriteByte(amount.Gas);
                w.WriteSingle(amount.Moles);
            }
        }

        return Finish(ref w, Limits.MaxDevGasReportBytes);
    }

    /// <summary>The encoded <see cref="DevGasReport"/>.</summary>
    public static byte[] Encode(DevGasReport message) =>
        EncodeToArray(Limits.MaxDevGasReportBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="DevGasReport"/>: inside the map's range, at most 15 x 15 cells of a known kind, the gas table and every cell's gases within their caps.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out DevGasReport value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.DevGasReport, Limits.MaxDevGasReportBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var result = new DevGasReport { X0 = r.ReadByte(), Y0 = r.ReadByte() };
            if (result.X0 < 1 || result.Y0 < 1)
                throw Bad("a dev gas report outside the map's range");
            result.Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a dev gas report z");
            result.Width = r.ReadByte();
            result.Height = r.ReadByte();
            if (result.Width < 1 || result.Width > DevGasSide || result.Height < 1 || result.Height > DevGasSide)
                throw Bad("a dev gas report of a size outside 1.." + DevGasSide);
            int gases = r.ReadCount(Limits.MaxDevGases, 2 * MinStringBytes + 4);
            for (int i = 0; i < gases; i++)
            {
                string id = r.ReadString(Limits.MaxDevGasTextBytes);
                string name = r.ReadString(Limits.MaxDevGasTextBytes);
                uint rgb = r.ReadUInt32();
                if (id == null || name == null)
                    throw Bad("a dev gas without an id or a name");
                if (rgb > 0xFFFFFF)
                    throw Bad("a dev gas colour over 0xFFFFFF");
                result.Gases.Add(new DevGasInfo(id, name, rgb));
            }

            int cells = result.Width * result.Height;
            result.Cells.Capacity = cells;
            for (int i = 0; i < cells; i++)
            {
                byte kind = r.ReadByte();
                if (kind > (byte)DevCellKind.Space)
                    throw Bad("a dev gas cell of an unknown kind");
                if (kind is not ((byte)DevCellKind.Open or (byte)DevCellKind.Space))
                {
                    result.Cells.Add(new DevGasCell((DevCellKind)kind, 0, 0, Array.Empty<DevGasAmount>()));
                    continue;
                }

                float pressure = r.ReadSingle();
                float temperature = r.ReadSingle();
                int n = r.ReadCount(Limits.MaxDevGases, 5);
                var moles = n == 0 ? Array.Empty<DevGasAmount>() : new DevGasAmount[n];
                for (int j = 0; j < n; j++)
                {
                    byte gas = r.ReadByte();
                    if (gas >= gases)
                        throw Bad("a dev gas amount names a gas outside the table");
                    moles[j] = new DevGasAmount(gas, r.ReadSingle());
                }

                result.Cells.Add(new DevGasCell((DevCellKind)kind, pressure, temperature, moles));
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
}
