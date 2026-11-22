using System.Linq;
using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

public static partial class Codec
{
    private static long CeilingBytes(List<CeilingChange> cells)
        => 10 + cells.Sum(c => 32L + MaxUtf8(c.State.Turf, CeilingState.MaxTurfLength));

    // Version 57 (D295): a cell's turf type, lattice, broken pick and support, then its picture in the public table (0: none);
    // version 58: then the lattice's damage in tenths.
    private static void WriteCeilings(ref WireWriter w, List<CeilingChange> cells, int max)
    {
        w.WriteCount(cells.Count, max);
        var seen = new HashSet<(int, int, int)>();
        foreach (var c in cells)
        {
            if (!seen.Add((c.X, c.Y, c.Z))) throw Bad("duplicate ceiling cell");
            ValidateCeiling(c.State);
            if (c.Look.Private || c.Look.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("a ceiling names a private or out-of-table appearance");
            WriteCoordinates(ref w, c.X, c.Y, c.Z);
            w.WriteString(c.State.Turf ?? "", CeilingState.MaxTurfLength);
            w.WriteByte((byte)c.State.Lattice);
            w.WriteByte(c.State.Broken);
            w.WriteByte(c.State.Supported ? (byte)1 : (byte)0);
            w.WriteVarUInt(c.Look.Index);
            w.WriteUInt16(c.State.Damage);
        }
    }

    private static void ReadCeilings(ref WireReader r, List<CeilingChange> cells, int max)
    {
        int count = r.ReadCount(max, 8);
        var seen = new HashSet<(int, int, int)>();
        for (int i = 0; i < count; i++)
        {
            int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "ceiling x");
            int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "ceiling y");
            int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "ceiling z");
            string turf = r.ReadString(CeilingState.MaxTurfLength) ?? throw Bad("null ceiling turf");
            var lattice = (CeilingLattice)r.ReadByte();
            byte broken = r.ReadByte();
            byte supported = r.ReadByte();
            ulong look = r.ReadVarUInt();
            ushort damage = r.ReadUInt16();
            if (supported > 1 || look > (ulong)Limits.MaxAppearanceTableEntries || !seen.Add((x, y, z))) throw Bad("invalid ceiling cell");
            var state = new CeilingState(turf, lattice, broken, supported == 1, damage);
            ValidateCeiling(state);
            cells.Add(new CeilingChange(x, y, z, state, new TableRef((uint)look, false)));
        }
    }

    private static void ValidateCeiling(CeilingState state)
    {
        if (!state.IsValid)
            throw Bad("invalid ceiling state");
    }
}
