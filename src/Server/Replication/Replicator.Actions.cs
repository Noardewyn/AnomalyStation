using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Version 100 also (DV698, ui-overhaul.md section 15; protocol.md section 100): the own action buttons that are on the client's screen this tick (the
// screen objects the screen entries were made from), with their actions' names, tg's active state (is_action_active) and a cooldown action's cooldown
// (next_use_time against world.time, in server ticks), and the palette's screen objects; sent when they change and in a join.

public sealed partial class Replicator
{
    private sealed partial class SessionState
    {
        private ActionsMessage _actionsSent;

        /// <summary>The Actions message of the last <see cref="Update"/>, or null when nothing changed.</summary>
        public ActionsMessage PendingActions { get; set; }

        private readonly List<ActionButtonState> _actionButtons = new();
        private readonly List<long> _actionPalette = new();

        // After the screen entries: _current holds this tick's screen objects in increasing id order.
        private void UpdateActions(long tick, bool full)
        {
            _actionButtons.Clear();
            _actionPalette.Clear();
            foreach (atom_movable m in _current)
            {
                if (m is atom_movable_screen_movable_action_button { linked_action: datum_action action } button && action.gc_destroyed == null)
                {
                    if (_actionButtons.Count < Limits.MaxActionButtons && (_actionButtons.Count == 0 || _actionButtons[^1].Button != m.dm_id))
                        _actionButtons.Add(Read(button, action, tick));
                }
                else if (m is atom_movable_screen_button_palette or atom_movable_screen_palette_scroll or atom_movable_screen_action_landing
                    && _actionPalette.Count < Limits.MaxActionPalette && (_actionPalette.Count == 0 || _actionPalette[^1] != m.dm_id))
                    _actionPalette.Add(m.dm_id);
            }
            var now = new ActionsMessage { Buttons = _actionButtons.ToArray(), Palette = _actionPalette.ToArray() };
            if (!full && now.Equals(_actionsSent ?? ActionsMessage.None))
                return;
            _actionsSent = now;
            PendingActions = now;
        }

        private static ActionButtonState Read(atom_movable_screen_movable_action_button button, datum_action action, long tick)
        {
            string name = Dm.jointext(Dm.list(button), ""); // the button's name as tg's update_button_name set it, plain text
            name = string.IsNullOrEmpty(name) ? null : ServerHost.Truncate(name, Limits.MaxActionNameBytes);
            bool active = Dm.truthy(action.is_action_active(button));
            long end = 0;
            int ticks = 0;
            if (action is datum_action_cooldown cooldown)
            {
                // world.time and the cooldown in deciseconds, as tg keeps them; the client counts in server ticks
                double left = (cooldown.next_use_time - World.Current.Time) * MotionMath.TicksPerSecond / 10.0;
                if (left > 0)
                {
                    int leftTicks = (int)Math.Min(Limits.MaxActionCooldownTicks, Math.Max(1, Math.Round(left)));
                    double length = (cooldown.cooldown_time ?? 0f) * MotionMath.TicksPerSecond / 10.0;
                    ticks = (int)Math.Clamp(Math.Round(length), leftTicks, Limits.MaxActionCooldownTicks); // an overridden cooldown longer than its own: its whole run
                    end = tick + leftTicks;
                }
            }
            return new ActionButtonState(button.dm_id, name, active, end, ticks);
        }
    }

    /// <inheritdoc/>
    public ActionsMessage TakeActions(int session)
    {
        if (!_sessions.TryGetValue(session, out SessionState state) || state.PendingActions == null)
            return null;
        ActionsMessage taken = state.PendingActions;
        state.PendingActions = null;
        return taken;
    }
}
