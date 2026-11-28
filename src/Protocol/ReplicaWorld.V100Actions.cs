namespace Anomaly.Protocol;

// Version 100 also (MessagesV100Actions.cs, protocol.md section 100; DV698): the own action buttons. The replica keeps the last message, so every reader
// of the State channel (the game client, the test rigs, the bots) takes it.

public sealed partial class ReplicaWorld
{
    /// <summary>The own action buttons the server last sent; <see cref="ActionsMessage.None"/> before any.</summary>
    public ActionsMessage Actions { get; private set; } = ActionsMessage.None;

    /// <summary>Applies an <see cref="ActionsMessage"/>.</summary>
    public void Apply(ActionsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Actions = message;
    }
}
