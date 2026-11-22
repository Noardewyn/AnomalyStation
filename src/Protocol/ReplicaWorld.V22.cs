namespace Anomaly.Protocol;

// Version 22 (the ghosts, D151): the private block's observer layer (MessagesV22.cs). Its entities join the same table as the
// body's: they are spawned, changed and despawned after the body's lists and before the own state, and the events see them
// with the body's at the end of the message.

public sealed partial class ReplicaWorld
{
    private void ApplyLayer(OwnBlock block, long tick)
    {
        foreach (EntityState e in block.LayerSpawned)
        {
            CheckState(e, _types.Count, _appearances.Count);
            var entity = new ReplicaEntity(this, _types[e.Type], e, tick);
            if (!_byId.TryAdd(e.Id, entity))
                throw Bad("layer entity " + e.Id + " spawns and exists");
            _ordered.Add(entity);
            _orderedStale = true;
            _spawnedScratch.Add(entity);
            if (entity.HasLight)
                NoteLight(entity);
        }

        foreach (EntityChange change in block.LayerChanged)
        {
            if (!_byId.TryGetValue(change.Id, out ReplicaEntity entity))
                throw Bad("layer entity " + change.Id + " changes but does not exist");
            LightState before = entity.Light;
            entity.Change(change, tick, _map, _appearances.Count);
            if ((change.Mask & NetField.Light) != 0 && before != entity.Light)
                NoteLight(entity);
        }

        if (block.LayerDespawned.Count == 0)
            return;
        foreach (long id in block.LayerDespawned)
        {
            if (!_byId.Remove(id, out ReplicaEntity entity))
                throw Bad("layer entity " + id + " despawns but does not exist");
            _despawnedScratch.Add(entity);
            if (entity.HasLight)
            {
                entity.ClearLight();
                NoteLight(entity);
            }
        }

        _ordered.RemoveAll(e => !_byId.TryGetValue(e.Id, out ReplicaEntity live) || !ReferenceEquals(live, e));
    }
}
