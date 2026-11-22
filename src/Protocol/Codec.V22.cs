namespace Anomaly.Protocol;

// Version 22 (the ghosts, D151): the private block's observer layer (MessagesV22.cs, protocol.md section 31).
//   flags bit 4: [count spawned] { entity record }* [count changed] { [id diff] change record }* [count despawned] { [id diff] }*
// The records are those of the Delta body (section 5), relative to the message's tick; the three lists are in strictly
// increasing id order, at most Limits.MaxEntitiesPerMessage records in all.

public static partial class Codec
{
    private const int OwnFlagLayer = 16;

    private static bool HasLayer(OwnBlock own) => own.LayerSpawned.Count > 0 || own.LayerChanged.Count > 0 || own.LayerDespawned.Count > 0;

    private static long MaxLayerSize(OwnBlock own)
    {
        long size = EntityStatesSize(own.LayerSpawned) + 10;
        foreach (EntityChange change in own.LayerChanged)
            size += MaxEntityChangeFixedBytes + ((change.Mask & NetField.Name) != 0 ? MaxUtf8(change.Name, Limits.MaxEntityNameBytes) : 0)
                + 15 + ((change.Mask & NetField.Animation) != 0 ? MaxAnimationBytes(change.Animation) : 0) + ((change.Mask & NetField.Door) != 0 ? MaxDoorBytes(change.Door) : 0);
        return size + 10 + (long)own.LayerDespawned.Count * MaxDespawnBytes;
    }

    private static void WriteLayer(ref WireWriter w, OwnBlock own, long tick)
    {
        if ((long)own.LayerSpawned.Count + own.LayerChanged.Count + own.LayerDespawned.Count > Limits.MaxEntitiesPerMessage)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a private block of more than " + Limits.MaxEntitiesPerMessage + " layer entity records");
        WriteEntityStates(ref w, own.LayerSpawned, true, Limits.MaxEntitiesPerMessage, tick);
        w.WriteCount(own.LayerChanged.Count, Limits.MaxEntitiesPerMessage);
        long previous = 0;
        foreach (EntityChange change in own.LayerChanged)
        {
            WriteId(ref w, change.Id, previous, true);
            previous = change.Id;
            WriteChange(ref w, change, tick);
        }

        w.WriteCount(own.LayerDespawned.Count, Limits.MaxEntitiesPerMessage);
        previous = 0;
        foreach (long id in own.LayerDespawned)
        {
            WriteId(ref w, id, previous, true);
            previous = id;
        }
    }

    private static void ReadLayer(ref WireReader r, OwnBlock own, long tick)
    {
        int budget = Limits.MaxEntitiesPerMessage;
        ReadEntityStates(ref r, own.LayerSpawned, true, Limits.MaxTypeTableEntries, budget, tick, Limits.MaxAppearanceTableEntries);
        budget -= own.LayerSpawned.Count;
        int changed = r.ReadCount(budget, MinEntityChangeBytes);
        own.LayerChanged.Capacity = changed;
        long previous = 0;
        for (int i = 0; i < changed; i++)
        {
            long id = ReadId(ref r, previous, true);
            previous = id;
            own.LayerChanged.Add(ReadChange(ref r, id, tick));
        }

        budget -= changed;
        int despawned = r.ReadCount(budget, MinDespawnBytes);
        own.LayerDespawned.Capacity = despawned;
        previous = 0;
        for (int i = 0; i < despawned; i++)
        {
            long id = ReadId(ref r, previous, true);
            previous = id;
            own.LayerDespawned.Add(id);
        }

        if (own.LayerSpawned.Count + own.LayerChanged.Count + despawned == 0)
            throw Bad("a private block flags layer entities and has none");
    }
}
