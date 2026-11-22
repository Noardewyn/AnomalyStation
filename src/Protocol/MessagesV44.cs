using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

/// <summary>A ceiling cell (version 57, D295): its state and the public-table picture tg would draw for its block (none: no ceiling).</summary>
public readonly record struct CeilingChange(int X, int Y, int Z, CeilingState State, TableRef Look = default);

public sealed partial class MapGrid
{
    /// <summary>All existing boundaries, including those above space. Missing cells have no ceiling.</summary>
    public List<CeilingChange> Ceilings { get; } = new();
}

public sealed partial class Delta
{
    public List<CeilingChange> Ceilings { get; } = new();
}
