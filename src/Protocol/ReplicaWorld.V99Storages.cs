namespace Anomaly.Protocol;

// Version 99 also (MessagesV99Storages.cs, protocol.md section 98; DV692): the own carried storages. The replica keeps the last state (its refusal
// field cleared) and counts the refusals, so every reader of the State channel (the game client, the fake feed, the test rigs, the bots) takes it.

public sealed partial class ReplicaWorld
{
    /// <summary>The own carried storages the server last sent, without the refusal; <see cref="StoragesMessage.None"/> before any.</summary>
    public StoragesMessage Storages { get; private set; } = StoragesMessage.None;

    /// <summary>How many refused inventory moves the server reported to this replica.</summary>
    public int StoragesRefusals { get; private set; }

    /// <summary>The item of the last refused move, 0 before the first.</summary>
    public long StoragesRefusedItem { get; private set; }

    /// <summary>Applies a <see cref="StoragesMessage"/>.</summary>
    public void Apply(StoragesMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.RefusedItem != 0)
        {
            StoragesRefusals++;
            StoragesRefusedItem = message.RefusedItem;
        }
        Storages = message.WithoutRefusal();
    }
}
