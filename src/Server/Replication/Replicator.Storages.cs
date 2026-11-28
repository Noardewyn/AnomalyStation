using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 100 also (DV696, ui-overhaul.md section 12, slice 4B-1; protocol.md section 100): the storages the own human carries, read after the tick from
// that session's own mob (CarriedStorages.List) and sent to that session only, when they change, when a move was refused, and in a join. The message
// is not rebuilt every tick: a cheap print of what tg's state says (the storages, their numbers and flags, their items' ids, weights and stack
// counts) is taken each tick, and the message (names and pictures interned) is built only when the print changed or a hint of the tick named one of
// the storages or items last sent (a move, a new look, a new name). An item's picture is the item drawn alone, as a slot's vis_contents child (the
// private row the HUD's slot and the hotbar use). Slice 4C-1: the other container the player has open by tg's state (CarriedStorages.Other, fields only) is the
// last entry, in the print and the message alike.

public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private StoragesMessage _storagesSent;
        private CarriedStorages _storagesOf; // the state whose refusals _storagesRefusals counts
        private int _storagesRefusals;
        private long _storagesLookedAt = long.MinValue;
        private long _storagesPrint;
        private readonly HashSet<long> _storagesWatched = new(); // the storages' and items' ids of the last built message

        /// <summary>The Storages message of the last <see cref="Update"/>, or null when nothing changed.</summary>
        public StoragesMessage PendingStorages { get; set; }

        /// <summary>How many times this session's message was built (the change detection's test reads it).</summary>
        public int StoragesBuilds { get; private set; }

        private void UpdateStorages(Replicator owner, client player, long tick, bool full)
        {
            long lookedBefore = _storagesLookedAt;
            _storagesLookedAt = tick;
            mob_living_carbon_human human = player?.mob is mob_living_carbon_human h && h.gc_destroyed == null ? h : null;
            CarriedStorages kept = human?.dm_carried;
            long refused = 0;
            if (kept != _storagesOf)
            {
                // a state made since the last look counts its refusals from 0; one that already existed (a body re-entered, a join) starts from its
                // count, so an old refusal is not replayed (as the hotbar's)
                _storagesOf = kept;
                _storagesRefusals = !full && kept != null && kept.CreatedTick > lookedBefore ? 0 : kept?.Refusals ?? 0;
            }
            if (kept != null && kept.Refusals != _storagesRefusals)
            {
                _storagesRefusals = kept.Refusals;
                refused = full ? 0 : kept.RefusedItem;
            }
            long print = human == null ? 0 : Print(human);
            bool look = full || _storagesSent == null || print != _storagesPrint;
            if (!look)
            {
                foreach (long id in _storagesWatched)
                {
                    if (owner._hinted.Contains(id))
                    {
                        look = true;
                        break;
                    }
                }
            }
            if (!look && refused == 0)
                return;
            _storagesPrint = print;
            StoragesMessage now = _storagesSent ?? StoragesMessage.None;
            if (look)
            {
                now = human == null ? StoragesMessage.None : Build(human);
                StoragesBuilds++;
            }
            if (!full && refused == 0 && now.Equals(_storagesSent))
                return;
            _storagesSent = now;
            PendingStorages = refused == 0 ? now : new StoragesMessage { Storages = now.Storages, RefusedItem = refused };
        }

        // What tg's state says, as one number: each carried storage's item, place, hand, flags and numbers, and its contents' ids, weight classes and
        // stack counts. Names and pictures are not read here; their changes come as hints of the ids watched.
        private static long Print(mob_living_carbon_human human)
        {
            var hash = new HashCode();
            foreach (CarriedStorages.Carried c in CarriedStorages.List(human))
                Print(ref hash, c.Item, c.Storage, c.Place, c.Hand);
            if (CarriedStorages.Other(human) is datum_storage other)
                Print(ref hash, other.parent, other, CarriedPlace.Other, 0);
            return hash.ToHashCode();
        }

        private static void Print(ref HashCode hash, atom parent, datum_storage s, CarriedPlace place, int hand)
        {
            hash.Add(parent.dm_id);
            hash.Add((byte)place);
            hash.Add(hand);
            hash.Add(s.locked);
            hash.Add(s.display_contents);
            hash.Add(s.max_slots);
            hash.Add(s.max_total_storage);
            int n = 0;
            foreach (obj_item item in Dm.each<obj_item>(s.real_location.contents))
            {
                hash.Add(item.dm_id);
                hash.Add(item.w_class);
                if (item is obj_item_stack stack)
                    hash.Add(stack.amount);
                n++;
            }
            hash.Add(n);
        }

        private StoragesMessage Build(mob_living_carbon_human human)
        {
            _storagesWatched.Clear();
            var storages = new List<CarriedStorage>();
            foreach (CarriedStorages.Carried c in CarriedStorages.List(human))
                storages.Add(Entry(c.Item, c.Storage, c.Place, c.Hand));
            // slice 4C-1: the other container, last, while tg keeps it open for this player
            if (CarriedStorages.Other(human) is datum_storage other)
                storages.Add(Entry(other.parent, other, CarriedPlace.Other, 0));
            return new StoragesMessage { Storages = storages.ToArray() };
        }

        private CarriedStorage Entry(atom parent, datum_storage s, CarriedPlace place, int hand)
        {
            bool shows = CarriedStorages.ShowsItems(s);
            List<obj_item> shown = CarriedStorages.ShownItems(s);
            var items = new StorageItem[shown.Count];
            for (int k = 0; k < shown.Count; k++)
            {
                obj_item item = shown[k];
                _storagesWatched.Add(item.dm_id);
                uint look = Tables.InternVisChild(item);
                int count = item is obj_item_stack stack ? Cap(Dm.num(stack.amount)) : 0;
                items[k] = new StorageItem(item.dm_id, NameOf(item), look == 0 ? TableRef.None : new TableRef(look, true), count);
            }
            _storagesWatched.Add(parent.dm_id);
            return new CarriedStorage
            {
                Entity = parent.dm_id,
                Place = (StoragePlace)(byte)place,
                Hand = (byte)hand,
                Name = NameOf(parent),
                Slots = Cap(s.max_slots),
                // a locked or hidden storage's room is not sent either: tg shows nothing of what is in it
                Used = shows ? Cap(Dm.num(s.get_total_weight())) : 0,
                Limit = Cap(s.max_total_storage),
                Locked = Dm.truthy(s.locked),
                Hidden = !Dm.truthy(s.locked) && !Dm.truthy(s.display_contents),
                Cut = CarriedStorages.IsCut(s),
                Items = items,
            };
        }

        private static int Cap(float value) => float.IsNaN(value) || value <= 0 ? 0 : value >= Limits.MaxStorageNumber ? Limits.MaxStorageNumber : (int)value;

        // The thing's name as plain text (no article, no markers), cut at the cap; never empty.
        private static string NameOf(atom item)
        {
            string name = Dm.jointext(Dm.list(item), "");
            return string.IsNullOrEmpty(name) ? "?" : ServerHost.Truncate(name, Limits.MaxStorageNameBytes);
        }
    }

    /// <inheritdoc/>
    public StoragesMessage TakeStorages(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingStorages == null)
            return null;
        StoragesMessage taken = state.PendingStorages;
        state.PendingStorages = null;
        return taken;
    }

    /// <summary>How many times a session's Storages message was built since it joined (0 for an unknown session).</summary>
    public int StoragesBuilds(int session) => _sessions.TryGetValue(session, out SessionState state) ? state.StoragesBuilds : 0;
}
