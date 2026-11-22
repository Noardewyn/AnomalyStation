namespace Anomaly.Core.Motion;

/// <summary>tg's rods over open space under a ceiling's floor block: none, a lattice or a catwalk (D295).</summary>
public enum CeilingLattice : byte { None, Lattice, Catwalk }

/// <summary>
/// A horizontal boundary above a cell, shared with the future floor of the next level (D295): one of tg's floor blocks (its turf
/// type, plating or a floor tile's turf) or, with no turf, the lattice or catwalk it was built from; <see cref="Broken"/> is the
/// 1-based pick from the turf's <c>broken_states()</c> (0: whole); <see cref="Damage"/> is the damage a lattice or catwalk has taken, in
/// tenths of tg's integrity points (tg's <c>DAMAGE_PRECISION</c>), out of <see cref="LatticeIntegrity"/>.
/// </summary>
public readonly record struct CeilingState(string Turf = "", CeilingLattice Lattice = CeilingLattice.None, byte Broken = 0, bool Supported = true,
    ushort Damage = 0)
{
    public const string Plating = "/turf/open/floor/plating";
    public const int MaxTurfLength = 128;
    /// <summary>tg's <c>max_integrity</c> of the lattice and the catwalk (<c>lattice.dm:10</c>).</summary>
    public const int LatticeIntegrity = 50;

    public bool IsValid => System.Enum.IsDefined(Lattice) && (string.IsNullOrEmpty(Turf)
        ? Broken == 0 && Damage < LatticeIntegrity * 10 && (Damage == 0 || Lattice != CeilingLattice.None)
        : Lattice == CeilingLattice.None && Damage == 0 && Turf.Length <= MaxTurfLength && Turf.StartsWith("/turf/open/floor", System.StringComparison.Ordinal));
    public bool Exists => !string.IsNullOrEmpty(Turf) || Lattice != CeilingLattice.None;
    public bool Sealed => !string.IsNullOrEmpty(Turf);
    /// <summary>tg's glass floors (<c>/turf/open/floor/glass</c> and its reinforced and plasma kinds) are see-through.</summary>
    public bool IsGlass => Turf?.StartsWith("/turf/open/floor/glass", System.StringComparison.Ordinal) ?? false;
    public bool TransmitsLight => !Sealed || IsGlass;
    public static CeilingState Of(string turf) => new(turf);
    public static CeilingState Of(CeilingLattice lattice) => new("", lattice);
}
