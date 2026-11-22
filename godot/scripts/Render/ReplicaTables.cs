using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>The compositor's view of a replica's appearance, resource and string tables (public and this connection's private ones).</summary>
public sealed class ReplicaTables : IAppearanceSource
{
    private readonly ReplicaWorld _replica;

    public ReplicaTables(ReplicaWorld replica) => _replica = replica;

    /// <summary>The replica behind the tables.</summary>
    public ReplicaWorld Replica => _replica;

    public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => _replica.TryGetAppearance(reference, out entry);

    public string ResolveString(TableRef reference) => _replica.ResolveString(reference);

    public string ResolveResource(TableRef reference) => _replica.ResolveResource(reference);
}
