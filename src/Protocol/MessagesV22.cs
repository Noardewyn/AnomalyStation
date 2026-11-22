namespace Anomaly.Protocol;

// Version 22 (the ghosts, D151, protocol.md section 31): the observer layer. BYOND sends a client only the atoms whose
// invisibility is at most its mob's see_invisible; the shared Delta body carries the entities every viewer sees (invisibility up
// to SEE_INVISIBLE_LIVING, 25), and the entities above that (a ghost's INVISIBILITY_OBSERVER, 60) travel in the private block of
// each connection whose mob sees them, as spawns, changes and despawns of that connection's own view.

public sealed partial class OwnBlock
{
    /// <summary>
    /// Version 22: the tick the layer records' last-move, flick and animation ticks are relative to: the Delta's or the
    /// Snapshot's tick. The encoders of a whole message use the message's tick; <c>Codec.EncodeOwnBlock</c> uses this one, and a
    /// decoder sets it.
    /// </summary>
    public long LayerTick { get; set; }

    /// <summary>Version 22: observer-layer entities this connection now sees (it did not before this block), with their full record.</summary>
    public List<EntityState> LayerSpawned { get; } = new();

    /// <summary>Version 22: observer-layer entities this connection sees that changed this tick.</summary>
    public List<EntityChange> LayerChanged { get; } = new();

    /// <summary>Version 22: observer-layer entities this connection no longer sees (deleted, gone from the turfs, or out of its sight).</summary>
    public List<long> LayerDespawned { get; } = new();
}
