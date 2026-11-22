namespace Anomaly.Protocol;

// Version 19 (MessagesV19.cs, protocol.md section 28, DV47): the session's tooltip. The replica keeps what is shown (tg's HTML text and
// theme) and raises TooltipChanged. The server sends only changes, so a hide while nothing is shown is a desync. A MapGrid (the start of
// every join) forgets it; the server sends a shown tooltip again after the join.

public sealed partial class ReplicaWorld
{
    private Action<TooltipMessage> _tooltipChangedHandlers;

    /// <summary>The tooltip shown now (a <see cref="TooltipKind.Show"/> message), or null.</summary>
    public TooltipMessage Tooltip { get; private set; }

    /// <summary>Raised after each applied <see cref="TooltipMessage"/> with the tooltip shown now (null after a hide).</summary>
    public event Action<TooltipMessage> TooltipChanged
    {
        add => _tooltipChangedHandlers += value;
        remove => _tooltipChangedHandlers -= value;
    }

    /// <summary>
    /// Applies a <see cref="TooltipMessage"/>: throws <see cref="ProtocolException"/> (and changes nothing) for one before a Snapshot or a hide
    /// while no tooltip is shown.
    /// </summary>
    public void Apply(TooltipMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_hasSnapshot)
            throw Bad("a Tooltip message arrives before any Snapshot");
        if (message.Kind == TooltipKind.Hide)
        {
            if (Tooltip == null)
                throw Bad("a tooltip hides but none is shown");
            Tooltip = null;
        }
        else
        {
            Tooltip = message;
        }
        _tooltipChangedHandlers?.Invoke(Tooltip);
    }

    private void TakeMapCellsV19() => Tooltip = null;
}
