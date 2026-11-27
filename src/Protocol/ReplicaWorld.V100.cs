namespace Anomaly.Protocol;

// Version 100 also (MessagesV100Hotbar.cs, protocol.md section 99; DV693): the own hotbar. The replica keeps the last state (its refusal field cleared) and
// counts the refusals, so every reader of the State channel (the game client, the fake feed, the test rigs, the bots) takes the message.

public sealed partial class ReplicaWorld
{
    /// <summary>The own hotbar the server last sent, without its refusal; <see cref="HotbarMessage.None"/> before any.</summary>
    public HotbarMessage Hotbar { get; private set; } = HotbarMessage.None;

    /// <summary>How many refused presses the server reported to this replica.</summary>
    public int HotbarRefusals { get; private set; }

    /// <summary>The cell (1 to 10) of the last refused press, 0 before the first.</summary>
    public int HotbarRefusedCell { get; private set; }

    /// <summary>Applies a <see cref="HotbarMessage"/>.</summary>
    public void Apply(HotbarMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.RefusedCell != 0)
        {
            HotbarRefusals++;
            HotbarRefusedCell = message.RefusedCell;
        }
        Hotbar = message.WithoutRefusal();
    }
}
