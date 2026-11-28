using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client;

// ui-overhaul.md section 15 (DV698): the actions menu (K) and its nine slots (Shift+1 to Shift+9). The slots are the client's own and last while the
// player has the actions; using one is tg's click on its action button.
public partial class GameScene
{
    private readonly ActionSlots _actionSlots = new();
    private ActionsView _actionsView;

    // An open actions menu is a window of the player's: it frees the cursor while open (MouseOwner) and Esc closes it.
    private bool ActionsMenuOpen => _actionsView?.Open == true;

    private void BuildActionsMenu(CanvasLayer ui)
    {
        _actionsView = new ActionsView { Name = "ActionsMenu" };
        ui.AddChild(_actionsView);
        _actionsView.Use += UseAction;
    }

    // tg's click on the action button (action_button/Click: the server checks it is the player's and usable)
    private void UseAction(long button)
    {
        if (button != 0)
            _session?.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
    }

    // Slot `slot`'s action (Shift+N), with the menu open or closed; nothing for an empty slot
    private void UseActionSlot(int slot) => UseAction(_actionSlots.ButtonIn(slot));

    private void ToggleActionsMenu() => _actionsView?.SetOpen(!ActionsMenuOpen);

    // Esc: the actions menu closes first when it is open (true: it was)
    private bool CloseActionsMenu()
    {
        if (!ActionsMenuOpen)
            return false;
        _actionsView.SetOpen(false);
        return true;
    }

    // Before the HUD is drawn: the slots follow the buttons the server lists (a new one takes the first empty slot, a gone one leaves its slot), and the
    // HUD draws each slotted button in its slot of the top-left row
    private void UpdateActionSlots()
    {
        ReplicaWorld replica = _feed.Replica;
        _actionSlots.Sync(ActionSlots.Drawable(replica.Actions, replica.Screen));
        _hudLayer.Actions = replica.Actions;
        _hudLayer.ActionSlots = _actionSlots;
    }

    // After the HUD is drawn (its row of slots laid out for this frame): the menu and what the row shows over tg's pictures
    private void FrameActionsMenu(double now)
    {
        if (_actionsView == null)
            return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        _actionsView.Frame(_feed.Replica, _dragTextures, _actionSlots, _hudLayer.ActionRow, _hudLayer.ActionCell, _hudLayer.ActionUnit,
            new HudMetrics(size.X, size.Y, _options.UiScale), _feed.Replica.Tick, now, _options);
    }
}
