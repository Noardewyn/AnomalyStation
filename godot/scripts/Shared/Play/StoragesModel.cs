using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// ui-overhaul.md section 12, slice 4B-1 (DV692, protocol version 99's Storages): the client's copy of the storages the own human carries, as the server
/// last sent them (in the Tab inventory's order, each with its place, name, slots, room, flags and items), and the last refused move with when it came
/// (for the next slice's red blink). Nothing is predicted: a move is sent and the server's next message says what it did.
/// </summary>
public sealed class StoragesModel
{
    private int _refusalsSeen;

    /// <summary>The carried storages, the back's first; none before the first message or for a mob that is not a human.</summary>
    public IReadOnlyList<CarriedStorage> Storages { get; private set; } = StoragesMessage.None.Storages;

    /// <summary>The item (its entity) of the last refused move, 0 before the first.</summary>
    public long RefusedItem { get; private set; }

    /// <summary>When the last refusal came, in the clock of <see cref="Apply"/>; negative infinity before the first.</summary>
    public double RefusedAt { get; private set; } = double.NegativeInfinity;

    /// <summary>The storage carried at <paramref name="place"/> (and, for a hand, held in <paramref name="hand"/>), null when there is none.</summary>
    public CarriedStorage At(StoragePlace place, int hand = 0)
    {
        foreach (CarriedStorage s in Storages)
        {
            if (s.Place == place && (place != StoragePlace.Hand || s.Hand == hand))
                return s;
        }
        return null;
    }

    /// <summary>The storage holding the item <paramref name="item"/> and its cell (0 for the first), or null and -1 when no listed storage shows it.</summary>
    public CarriedStorage Holding(long item, out int cell)
    {
        foreach (CarriedStorage s in Storages)
        {
            for (int i = 0; i < s.Items.Count; i++)
            {
                if (s.Items[i].Entity == item)
                {
                    cell = i;
                    return s;
                }
            }
        }
        cell = -1;
        return null;
    }

    /// <summary>Slice 4B-2: a move the client had no target to send for (a quick transfer with no backpack worn) blinks as a refused one, dated <paramref name="now"/>.</summary>
    public void RefuseLocally(long item, double now)
    {
        RefusedItem = item;
        RefusedAt = now;
    }

    /// <summary>
    /// The feed's state at <paramref name="now"/>: <paramref name="message"/> (the last Storages), and the refusals so far (<paramref name="refusals"/>,
    /// the last one's <paramref name="refusedItem"/>): a new count is a new refusal, dated now.
    /// </summary>
    public void Apply(StoragesMessage message, int refusals, long refusedItem, double now)
    {
        Storages = (message ?? StoragesMessage.None).Storages;
        if (refusals != _refusalsSeen)
        {
            if (refusals > _refusalsSeen && refusedItem > 0)
            {
                RefusedItem = refusedItem;
                RefusedAt = now;
            }
            _refusalsSeen = refusals; // a feed that started again (a new join) counts from 0
        }
    }
}
