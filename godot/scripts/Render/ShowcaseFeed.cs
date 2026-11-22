using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// A feed over a replica that was filled by hand (the showcase, <c>Showcase.cs</c>): a world frozen at one tick, so the renderers can be
/// seen and checked without a server. It implements only what the renderers read.
/// </summary>
public sealed class ShowcaseFeed : IWorldFeed
{
    private readonly ReplicaWorld _replica;

    public ShowcaseFeed(ReplicaWorld replica) => _replica = replica;

    public FeedPhase Phase => FeedPhase.Playing;

    public string CloseReason => null;

    public bool Rejected => false;

    public string Description => "showcase";

    public void Poll(double nowSeconds)
    {
    }

    public uint LastSentSeq => 0;

    public uint LastAppliedSeq => 0;

    public MapGrid Map => _replica.Map;

    public long Tick => _replica.Tick;

    public long DeltaCount => 0;

    public long OwnEntity => _replica.OwnEntity;

    public int Count => _replica.Count;

    public IEnumerable<ReplicaEntity> Entities => _replica.Entities;

#pragma warning disable CS0067 // the frozen world never spawns or despawns
    public event Action<ReplicaEntity> Spawned;

    public event Action<ReplicaEntity> Despawned;

    public event Action<ServerText> ServerTextReceived;
#pragma warning restore CS0067

    public int RoundTripMs => 0;

    public long BytesSent => 0;

    public long BytesReceived => 0;

    public void Disconnect()
    {
    }

    public ReplicaWorld Replica => _replica;
}
