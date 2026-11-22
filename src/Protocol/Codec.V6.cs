namespace Anomaly.Protocol;

// Version 6 (MessagesV6.cs, docs/ai/specs/protocol.md section 16):
//
//   Airflow (server to client, State): [u8 id = 13] [count <= 2048 runs]
//     { [varuint z 1..16] [u8 y 1..255] [u8 x0 1..255] [count 1..255 cells] { [u8 cell] }* }*
//   [cell] = 0 (no flow) | 0xFF (unchanged) | dir << 5 | level, dir 1..6 (north, south, east, west, up, down), level 1..31
//   then (version 7) [count <= 512 vents] { [varuint z 1..16] [u8 y 1..255] [u8 x 1..255] [u8 vent] }*
//   [vent] = 0 (stopped) | kind << 5 | level, kind 1 out of the device, 2 into it, level 1..31
//
// A run stays inside the map's row (x0 + count - 1 <= 255); the cells of all runs are at most 4096. Every rule of the
// earlier codecs holds: TryDecode never throws, every count is capped before anything is allocated, and a message that
// decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes an <see cref="AirflowUpdate"/> for the State channel (at most <see cref="Limits.MaxAirflowBytes"/>).</summary>
    public static int Encode(AirflowUpdate message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Airflow);
        w.WriteCount(message.Runs.Count, Limits.MaxAirflowRuns);
        int cells = 0;
        foreach (AirflowRun run in message.Runs)
        {
            CheckAirflowRun(run.X0, run.Y, run.Z, run.Cells?.Length ?? 0);
            cells += run.Cells.Length;
            if (cells > Limits.MaxAirflowCells)
                throw Bad("an airflow update over " + Limits.MaxAirflowCells + " cells");
            w.WriteVarUInt((ulong)run.Z);
            w.WriteByte((byte)run.Y);
            w.WriteByte((byte)run.X0);
            w.WriteCount(run.Cells.Length, Limits.MaxAirflowRunCells);
            foreach (byte cell in run.Cells)
            {
                if (cell != Airflow.Unchanged && !Airflow.IsFlow(cell))
                    throw Bad("an airflow cell byte that is no flow");
            }
            w.WriteBytes(run.Cells);
        }
        w.WriteCount(message.Vents.Count, Limits.MaxAirflowVents);
        foreach (AirflowVent vent in message.Vents)
        {
            CheckAirflowVent(vent.X, vent.Y, vent.Z, vent.Cell);
            w.WriteVarUInt((ulong)vent.Z);
            w.WriteByte((byte)vent.Y);
            w.WriteByte((byte)vent.X);
            w.WriteByte(vent.Cell);
        }

        return Finish(ref w, Limits.MaxAirflowBytes);
    }

    /// <summary>The encoded <see cref="AirflowUpdate"/>.</summary>
    public static byte[] Encode(AirflowUpdate message) =>
        EncodeToArray(Limits.MaxAirflowBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes an <see cref="AirflowUpdate"/>: runs inside the map's range and caps, every cell byte a flow or unchanged.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out AirflowUpdate value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Airflow, Limits.MaxAirflowBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var result = new AirflowUpdate();
            int runs = r.ReadCount(Limits.MaxAirflowRuns, 5);
            int total = 0;
            for (int i = 0; i < runs; i++)
            {
                int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "an airflow run z");
                int y = r.ReadByte();
                int x0 = r.ReadByte();
                int n = r.ReadCount(Limits.MaxAirflowRunCells, 1);
                CheckAirflowRun(x0, y, z, n);
                total += n;
                if (total > Limits.MaxAirflowCells)
                    throw Bad("an airflow update over " + Limits.MaxAirflowCells + " cells");
                byte[] cells = r.ReadBytes(n).ToArray();
                foreach (byte cell in cells)
                {
                    if (cell != Airflow.Unchanged && !Airflow.IsFlow(cell))
                        throw Bad("an airflow cell byte that is no flow");
                }
                result.Runs.Add(new AirflowRun(x0, y, z, cells));
            }
            int vents = r.ReadCount(Limits.MaxAirflowVents, 4);
            for (int i = 0; i < vents; i++)
            {
                int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "an airflow vent z");
                int y = r.ReadByte();
                int x = r.ReadByte();
                byte cell = r.ReadByte();
                CheckAirflowVent(x, y, z, cell);
                result.Vents.Add(new AirflowVent(x, y, z, cell));
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

    private static void CheckAirflowVent(int x, int y, int z, byte cell)
    {
        if (z < 1 || z > Limits.MaxMapLevels || y < 1 || y > Limits.MaxMapSize || x < 1 || x > Limits.MaxMapSize)
            throw Bad("an airflow vent outside the map's range");
        if (!AirflowVents.IsVent(cell))
            throw Bad("an airflow vent byte that is no air movement");
    }

    private static void CheckAirflowRun(int x0, int y, int z, int count)
    {
        if (z < 1 || z > Limits.MaxMapLevels || y < 1 || y > Limits.MaxMapSize || x0 < 1 || x0 > Limits.MaxMapSize)
            throw Bad("an airflow run outside the map's range");
        if (count < 1 || x0 + count - 1 > Limits.MaxMapSize)
            throw Bad("an airflow run of no cells or past the end of its row");
    }
}
