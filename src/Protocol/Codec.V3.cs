namespace Anomaly.Protocol;

// Owner: M3 P5 (the contract, MessagesV3.cs and Limits.cs, is the M3 contracts step's). The version-3 layout, behind the
// existing entry points (docs/ai/specs/protocol.md section "Version 3"):
//
//   MapGrid, after the solidity bits:
//     [count] { [varuint run length] [varuint flags byte] }*          the flags runs (none, or covering every cell)
//     [count] { [u32 0xRRGGBBAA] }*                                   the base-light table (index 1 is the first entry)
//     [count] { [varuint run length] [varuint base-light index] }*    the base-light runs (none, or covering every cell)
//     [light]                                                         the starlight values (range 0: none)
//   Delta body, after the version-2 lists, when the parts byte has PartV3:
//     [u8 v3 parts: 1 flags, 2 new base lights, 4 base-light changes, 8 new turf types, 16 turf type changes, 32 starlight]
//     flags:          [count] { [cell] [u8 flags] }*
//     new base lights:[varuint first index] [count] { [u32 rgba] }*
//     base-light chg: [count] { [cell] [varuint index] }*
//     new turf types: [varuint first index] [count] { [string] }*
//     turf type chg:  [count] { [cell] [varuint type index] }*
//     starlight:      [light]
//   Entity spawn record: flag bit 2 = a light follows. Change mask: Light is wire bit 11, its value follows GlideSize's.
//   [light] = [f32 range] and, when the range is not 0: [f32 power] [u32 rgba] [u8 flags: bit 0 on] [f32 angle] [u8 dir]
//             [f32 height] [f32 offset x] [f32 offset y] [u8 system]

public static partial class Codec
{
    /// <summary>The most bytes one encoded <see cref="LightState"/> takes.</summary>
    internal const int MaxLightBytes = 4 + 4 + 4 + 1 + 4 + 1 + 4 + 4 + 4 + 1;

    private const int PartV3 = 32;
    private const int V3Flags = 1, V3NewBaseLights = 2, V3BaseLightChanges = 4, V3NewTurfTypes = 8, V3TurfTypeChanges = 16, V3Starlight = 32;
    private const int V3All = 63;

    private static void WriteLight(ref WireWriter w, in LightState light)
    {
        if (light.IsNone)
        {
            w.WriteSingle(0f);
            return;
        }

        CheckLight(light);
        w.WriteSingle(light.Range);
        w.WriteSingle(light.Power);
        w.WriteUInt32(light.Rgba);
        w.WriteByte(light.On ? (byte)1 : (byte)0);
        w.WriteSingle(light.Angle);
        w.WriteByte(light.Dir);
        w.WriteSingle(light.Height);
        w.WriteSingle(light.OffsetX);
        w.WriteSingle(light.OffsetY);
        w.WriteByte(light.System);
    }

    private static void CheckLight(in LightState l)
    {
        if (!(l.Range > 0f && l.Range <= Limits.MaxLightRange))
            throw Bad("a light range outside 0.." + Limits.MaxLightRange);
        if (!(Math.Abs(l.Power) <= Limits.MaxLightPower))
            throw Bad("a light power outside +-" + Limits.MaxLightPower);
        if (!(l.Angle >= 0f && l.Angle <= 360f))
            throw Bad("a light angle outside 0..360");
        CheckDir(l.Dir);
        if (!(Math.Abs(l.Height) <= Limits.MaxLightHeight))
            throw Bad("a light height outside +-" + Limits.MaxLightHeight);
        if (!(Math.Abs(l.OffsetX) <= Limits.MaxLightOffset) || !(Math.Abs(l.OffsetY) <= Limits.MaxLightOffset))
            throw Bad("a light offset outside +-" + Limits.MaxLightOffset);
        if (l.System < 1 || l.System > 4)
            throw Bad("a light system outside 1..4");
    }

    private static LightState ReadLight(ref WireReader r)
    {
        float range = r.ReadSingle();
        if (range == 0f)
        {
            if (BitConverter.SingleToInt32Bits(range) != 0)
                throw Bad("a light range of negative zero (one encoding per light)");
            return LightState.None;
        }

        float power = r.ReadSingle();
        uint rgba = r.ReadUInt32();
        byte flags = r.ReadByte();
        if ((flags & ~1) != 0)
            throw Bad("a light flags byte sets bits that are not defined");
        float angle = r.ReadSingle();
        byte dir = r.ReadByte();
        float height = r.ReadSingle();
        float ox = r.ReadSingle();
        float oy = r.ReadSingle();
        byte system = r.ReadByte();
        var light = new LightState(range, power, rgba, (flags & 1) != 0, angle, dir, height, ox, oy, system);
        CheckLight(light);
        return light;
    }

    // ---- MapGrid ----

    private static long MaxMapGridV3Size(MapGrid m) =>
        10 + (long)m.FlagRuns.Count * MaxTurfRunBytes + 10 + (long)m.BaseLights.Count * 4 + 10 + (long)m.BaseLightRuns.Count * MaxTurfRunBytes + MaxLightBytes;

    private static void WriteMapGridV3(ref WireWriter w, MapGrid m, int cells)
    {
        WriteValueRuns(ref w, m.FlagRuns, cells, CellFlags.All, "flags");
        if (m.BaseLights.Count > Limits.MaxBaseLightEntries)
            throw Bad("a MapGrid base-light table of more than " + Limits.MaxBaseLightEntries + " entries");
        w.WriteCount(m.BaseLights.Count, Limits.MaxBaseLightEntries);
        foreach (BaseLight b in m.BaseLights)
            w.WriteUInt32(b.Rgba);
        WriteValueRuns(ref w, m.BaseLightRuns, cells, m.BaseLights.Count, "base-light");
        WriteLight(ref w, m.Starlight);
    }

    private static void ReadMapGridV3(ref WireReader r, MapGrid m, int cells)
    {
        ReadValueRuns(ref r, m.FlagRuns, cells, CellFlags.All, "flags");
        int lights = r.ReadCount(Limits.MaxBaseLightEntries, 4);
        m.BaseLights.Capacity = lights;
        for (int i = 0; i < lights; i++)
            m.BaseLights.Add(new BaseLight(r.ReadUInt32()));
        ReadValueRuns(ref r, m.BaseLightRuns, cells, lights, "base-light");
        m.Starlight = ReadLight(ref r);
    }

    private static void WriteValueRuns(ref WireWriter w, List<TurfRun> runs, int cells, int maxValue, string what)
    {
        w.WriteCount(runs.Count, cells);
        long covered = 0;
        foreach (TurfRun run in runs)
        {
            if (run.Count < 1 || run.TypeIndex < 0 || run.TypeIndex > maxValue)
                throw Bad("a MapGrid " + what + " run with a count below 1 or a value outside its range");
            covered += run.Count;
            if (covered > cells)
                throw Bad("the MapGrid " + what + " runs cover more cells than the map has");
            w.WriteVarUInt((ulong)run.Count);
            w.WriteVarUInt((ulong)run.TypeIndex);
        }

        if (runs.Count > 0 && covered != cells)
            throw Bad("the MapGrid " + what + " runs cover " + covered + " of " + cells + " cells");
    }

    private static void ReadValueRuns(ref WireReader r, List<TurfRun> runs, int cells, int maxValue, string what)
    {
        int n = r.ReadCount(cells, MinTurfRunBytes);
        runs.Capacity = n;
        long covered = 0;
        for (int i = 0; i < n; i++)
        {
            ulong count = r.ReadVarUInt();
            ulong value = r.ReadVarUInt();
            if (count == 0 || count > (ulong)(cells - covered))
                throw Bad("a MapGrid " + what + " run with no cells, or past the end of the map");
            if (value > (ulong)maxValue)
                throw Bad("a MapGrid " + what + " run with a value outside its range");
            covered += (long)count;
            runs.Add(new TurfRun((int)count, (int)value));
        }

        if (n > 0 && covered != cells)
            throw Bad("the MapGrid " + what + " runs do not cover the map");
    }

    // ---- Delta ----

    private static bool HasDeltaV3(Delta d) =>
        d.CellFlagsChanged.Count > 0 || d.NewBaseLights.Count > 0 || d.BaseLightChanged.Count > 0 || d.NewTurfTypes.Count > 0
        || d.TurfTypeChanged.Count > 0 || d.StarlightChanged.HasValue;

    private static long MaxDeltaV3Size(Delta d) =>
        1 + 10 + 10 + (long)d.CellFlagsChanged.Count * (5 + 5 + 5 + 1) + 10 + 10 + (long)d.NewBaseLights.Count * 4
        + 10 + (long)d.BaseLightChanged.Count * (5 + 5 + 5 + 5) + 10 + 10 + MaxNewTurfTypeBytes(d)
        + 10 + (long)d.TurfTypeChanged.Count * (5 + 5 + 5 + 5) + MaxLightBytes;

    private static long MaxNewTurfTypeBytes(Delta d)
    {
        long size = 0;
        foreach (string type in d.NewTurfTypes)
            size += 10 + MaxUtf8(type, Limits.MaxTypePathBytes);
        return size;
    }

    private static int V3CellChanges(Delta d) => d.CellFlagsChanged.Count + d.BaseLightChanged.Count + d.TurfTypeChanged.Count;

    private static void WriteDeltaV3(ref WireWriter w, Delta d)
    {
        int v3 = (d.CellFlagsChanged.Count > 0 ? V3Flags : 0) | (d.NewBaseLights.Count > 0 ? V3NewBaseLights : 0)
            | (d.BaseLightChanged.Count > 0 ? V3BaseLightChanges : 0) | (d.NewTurfTypes.Count > 0 ? V3NewTurfTypes : 0)
            | (d.TurfTypeChanged.Count > 0 ? V3TurfTypeChanges : 0) | (d.StarlightChanged.HasValue ? V3Starlight : 0);
        w.WriteByte((byte)v3);
        if ((v3 & V3Flags) != 0)
        {
            w.WriteCount(d.CellFlagsChanged.Count, Limits.MaxCellChangesPerDelta);
            foreach (CellFlagsChange c in d.CellFlagsChanged)
            {
                if ((c.Flags & ~CellFlags.All) != 0)
                    throw Bad("a cell flags byte that sets bits that are not defined");
                WriteCell(ref w, c.X, c.Y, c.Z);
                w.WriteByte(c.Flags);
            }
        }

        if ((v3 & V3NewBaseLights) != 0)
        {
            if (d.FirstNewBaseLight < 1 || (long)d.FirstNewBaseLight - 1 + d.NewBaseLights.Count > Limits.MaxBaseLightEntries)
                throw new ProtocolException(ProtocolError.CountTooLarge, "the base-light table would pass " + Limits.MaxBaseLightEntries + " entries");
            w.WriteVarUInt(d.FirstNewBaseLight);
            w.WriteCount(d.NewBaseLights.Count, Limits.MaxBaseLightEntries);
            foreach (BaseLight b in d.NewBaseLights)
                w.WriteUInt32(b.Rgba);
        }

        if ((v3 & V3BaseLightChanges) != 0)
        {
            w.WriteCount(d.BaseLightChanged.Count, Limits.MaxCellChangesPerDelta);
            foreach (CellBaseLightChange c in d.BaseLightChanged)
            {
                if (c.BaseLight > Limits.MaxBaseLightEntries)
                    throw Bad("a base-light change names an entry outside its table");
                WriteCell(ref w, c.X, c.Y, c.Z);
                w.WriteVarUInt(c.BaseLight);
            }
        }

        if ((v3 & V3NewTurfTypes) != 0)
        {
            if (d.FirstNewTurfType < 0 || (long)d.FirstNewTurfType + d.NewTurfTypes.Count > Limits.MaxTurfTypeEntries)
                throw new ProtocolException(ProtocolError.CountTooLarge, "the turf type table would pass " + Limits.MaxTurfTypeEntries + " entries");
            w.WriteVarUInt((ulong)d.FirstNewTurfType);
            w.WriteCount(d.NewTurfTypes.Count, Limits.MaxNewTurfTypesPerDelta);
            foreach (string type in d.NewTurfTypes)
            {
                if (type == null)
                    throw Bad("a null turf type path");
                w.WriteString(type, Limits.MaxTypePathBytes);
            }
        }

        if ((v3 & V3TurfTypeChanges) != 0)
        {
            w.WriteCount(d.TurfTypeChanged.Count, Limits.MaxCellChangesPerDelta);
            foreach (CellTurfTypeChange c in d.TurfTypeChanged)
            {
                if (c.TypeIndex < 0 || c.TypeIndex >= Limits.MaxTurfTypeEntries)
                    throw Bad("a turf type change names an entry outside its table");
                WriteCell(ref w, c.X, c.Y, c.Z);
                w.WriteVarUInt((ulong)c.TypeIndex);
            }
        }

        if ((v3 & V3Starlight) != 0)
            WriteLight(ref w, d.StarlightChanged.Value);
    }

    private static void ReadDeltaV3(ref WireReader r, Delta d, int cellBudget)
    {
        byte v3 = r.ReadByte();
        if (v3 == 0 || (v3 & ~V3All) != 0)
            throw Bad("a Delta flags version-3 parts and has none, or sets part bits that are not defined");
        if ((v3 & V3Flags) != 0)
        {
            int n = r.ReadCount(cellBudget, MinCellChangeBytes);
            cellBudget -= n;
            d.CellFlagsChanged.Capacity = n;
            for (int i = 0; i < n; i++)
            {
                ReadCell(ref r, out int x, out int y, out int z);
                byte flags = r.ReadByte();
                if ((flags & ~CellFlags.All) != 0)
                    throw Bad("a cell flags byte that sets bits that are not defined");
                d.CellFlagsChanged.Add(new CellFlagsChange(x, y, z, flags));
            }

            if (n == 0) throw Bad("a Delta flags cell flag changes and has none");
        }

        if ((v3 & V3NewBaseLights) != 0)
        {
            ulong first = r.ReadVarUInt();
            if (first < 1 || first > (ulong)Limits.MaxBaseLightEntries)
                throw Bad("a Delta first new base light outside the table");
            d.FirstNewBaseLight = (uint)first;
            int n = r.ReadCount(Limits.MaxBaseLightEntries - (int)first + 1, 4);
            if (n == 0) throw Bad("a Delta flags new base lights and has none");
            d.NewBaseLights.Capacity = n;
            for (int i = 0; i < n; i++)
                d.NewBaseLights.Add(new BaseLight(r.ReadUInt32()));
        }

        if ((v3 & V3BaseLightChanges) != 0)
        {
            int n = r.ReadCount(cellBudget, MinCellChangeBytes);
            cellBudget -= n;
            d.BaseLightChanged.Capacity = n;
            for (int i = 0; i < n; i++)
            {
                ReadCell(ref r, out int x, out int y, out int z);
                ulong index = r.ReadVarUInt();
                if (index > (ulong)Limits.MaxBaseLightEntries)
                    throw Bad("a base-light change names an entry outside its table");
                d.BaseLightChanged.Add(new CellBaseLightChange(x, y, z, (ushort)index));
            }

            if (n == 0) throw Bad("a Delta flags base-light changes and has none");
        }

        if ((v3 & V3NewTurfTypes) != 0)
        {
            d.FirstNewTurfType = ReadRanged(ref r, 0, Limits.MaxTurfTypeEntries, "a Delta first new turf type");
            int n = r.ReadCount(Math.Min(Limits.MaxNewTurfTypesPerDelta, Limits.MaxTurfTypeEntries - d.FirstNewTurfType), MinStringBytes);
            if (n == 0) throw Bad("a Delta flags new turf types and has none");
            d.NewTurfTypes.Capacity = n;
            for (int i = 0; i < n; i++)
                d.NewTurfTypes.Add(ReadTypePath(ref r));
        }

        if ((v3 & V3TurfTypeChanges) != 0)
        {
            int n = r.ReadCount(cellBudget, MinCellChangeBytes);
            d.TurfTypeChanged.Capacity = n;
            for (int i = 0; i < n; i++)
            {
                ReadCell(ref r, out int x, out int y, out int z);
                ulong index = r.ReadVarUInt();
                if (index >= (ulong)Limits.MaxTurfTypeEntries)
                    throw Bad("a turf type change names an entry outside its table");
                d.TurfTypeChanged.Add(new CellTurfTypeChange(x, y, z, (int)index));
            }

            if (n == 0) throw Bad("a Delta flags turf type changes and has none");
        }

        if ((v3 & V3Starlight) != 0)
            d.StarlightChanged = ReadLight(ref r);
    }
}
