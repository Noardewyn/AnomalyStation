using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Protocol version 19 (tg's tooltips, DV47): the player's tooltip. tg's /datum/tooltip writes into the player's BYOND client (the
// port's client.dm_tooltip_output: the page's update with the HTML text and the theme, and the window's hide); this turns the tick's
// records into at most one Tooltip message: what the client should show after the tick (the last record's state) when it differs
// from what was sent last. A show and a hide in one tick (a pointer that crossed a button) send nothing. A join shows the tooltip
// shown now again.

public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        // What the client shows: tg's text and theme, or nothing.
        private string _tooltipText;
        private string _tooltipTheme;

        /// <summary>The Tooltip message of the last <see cref="Update"/>, or null.</summary>
        public TooltipMessage PendingTooltip { get; set; }

        private void UpdateTooltip(client player, bool full)
        {
            PendingTooltip = null;
            string text = _tooltipText, theme = _tooltipTheme;
            if (player != null && player.dm_tooltip_output.Count > 0)
            {
                foreach (TooltipOutput output in player.dm_tooltip_output)
                {
                    if (output.Kind == TooltipOutputKind.Show && !string.IsNullOrEmpty(output.Text))
                    {
                        text = ServerHost.Truncate(output.Text, Limits.MaxTooltipTextBytes);
                        // A theme is a CSS class of tooltip.html; one that is no class name is drawn as the page's base look.
                        theme = TooltipMessage.IsValidTheme(output.Theme) ? output.Theme : "default";
                    }
                    else if (output.Kind == TooltipOutputKind.Hide)
                    {
                        text = null;
                        theme = null;
                    }
                }
                player.dm_tooltip_output.Clear();
            }
            bool changed = full ? text != null : text != _tooltipText || theme != _tooltipTheme;
            _tooltipText = text;
            _tooltipTheme = theme;
            if (!changed)
                return;
            PendingTooltip = text != null
                ? new TooltipMessage { Kind = TooltipKind.Show, Text = text, Theme = theme }
                : new TooltipMessage { Kind = TooltipKind.Hide };
        }
    }

    /// <inheritdoc/>
    public TooltipMessage TakeTooltip(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingTooltip == null)
            return null;
        TooltipMessage taken = state.PendingTooltip;
        state.PendingTooltip = null;
        return taken;
    }
}
