using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 22 (the ghosts, D151): the observer layer. BYOND sends a client the atoms whose invisibility is at most its mob's
// see_invisible (BYOND ref /atom/var/invisibility, /mob/var/see_invisible). The entities every viewer sees (invisibility up to
// SEE_INVISIBLE_LIVING, 25: the M3 rule of DV24) stay in the shared Delta body; the entities above that and up to
// SEE_INVISIBLE_OBSERVER (60, a ghost's INVISIBILITY_OBSERVER) are layer entities: the same shadows, stamps and change records,
// but their spawns, changes and despawns go to each session's private block (D40), filtered by that session's mob's
// see_invisible at the tick. A living player's client never receives a ghost; a ghost's client receives every ghost it sees.
// An entity whose invisibility crosses 25 leaves its layer this tick and enters the other one the next (as a visual that
// changes kind), so one tick never names an id in both the body and a block.
public sealed partial class Replicator
{
    // The ids of the layer entities, in id order (each session's block walks them in that order).
    private readonly SortedSet<long> _layerIds = new();

    // The change records of the layer entities this tick (Examine), by id; read by every session's block.
    private readonly Dictionary<long, EntityChange> _layerChanged = new();

    /// <summary>The number of observer-layer entities (version 22).</summary>
    public int LayerEntityCount => _layerIds.Count;

    private sealed partial class SessionState
    {
        // The layer entities this session's client has (spawned to it and not despawned), in id order.
        private readonly SortedSet<long> _layerHeld = new();
        private readonly List<long> _layerGone = new();

        /// <summary>The layer entities this session's client has now (tests and diagnostics).</summary>
        public IReadOnlyCollection<long> LayerHeld => _layerHeld;

        /// <summary>Version 42: true when the client holds the layer entity (the Motion messages name only those).</summary>
        public bool HoldsLayer(long id) => _layerHeld.Contains(id);

        // Fills the block's layer part: what the session's mob sees of the layer now against what its client has. A session
        // without a client or a mob sees nothing of the layer.
        private void UpdateLayer(Replicator owner, client player, long tick)
        {
            OwnBlock block = Block;
            block.LayerTick = tick;
            block.LayerSpawned.Clear();
            block.LayerChanged.Clear();
            block.LayerDespawned.Clear();
            mob viewer = player?.mob;
            float seeInvisible = viewer == null || viewer.gc_destroyed != null ? -1 : viewer.see_invisible;
            // DEVIATION(DV382): the mob that is the session's client.eye (a camera eye) is sent to it whatever its invisibility
            long eyeMob = player?.eye is mob eye && eye != viewer && eye.dm_id != 0 ? eye.dm_id : 0;
            _layerGone.Clear();
            foreach (long id in owner._layerIds)
            {
                ref Shadow s = ref owner._slots[owner._index[id]];
                bool sees = s.Invisibility <= seeInvisible || id == eyeMob || id == viewer?.dm_id; // the own mob even when invisimin
                bool held = _layerHeld.Contains(id);
                if (sees && !held)
                {
                    block.LayerSpawned.Add(StateOf(in s));
                    _layerHeld.Add(id);
                }
                else if (sees)
                {
                    if (owner._layerChanged.TryGetValue(id, out EntityChange change))
                        block.LayerChanged.Add(change);
                }
                else if (held)
                {
                    _layerGone.Add(id);
                }
            }
            foreach (long id in _layerHeld)
                if (!owner._layerIds.Contains(id))
                    _layerGone.Add(id);
            if (_layerGone.Count == 0)
                return;
            _layerGone.Sort();
            foreach (long id in _layerGone)
            {
                _layerHeld.Remove(id);
                block.LayerDespawned.Add(id);
            }
        }
    }
}
