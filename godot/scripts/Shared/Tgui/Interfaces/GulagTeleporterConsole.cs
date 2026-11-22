using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GulagTeleporterConsole.jsx (gaps LC1): the labour camp
// teleporter console's teleporter and beacon, the prisoner's ID, goal, occupant and status, and the process button
// (machinery/computer/prisoner/gulag_teleporter.dm:29 ui_data).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GulagTeleporterConsole
{
    // tg: GulagTeleporterConsole.jsx:10
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool teleporter = data.Truthy("teleporter");
        bool locked = data.Truthy("teleporter_lock");
        bool stateOpen = data.Truthy("teleporter_state_open");
        bool beacon = data.Truthy("beacon");
        bool id = data.Truthy("id");
        UiValue prisoner = data.Get("prisoner");
        string prisonerName = prisoner?.Str("name");
        string crimstat = prisoner?.Str("crimstat");
        // tg: GulagTeleporterConsole.jsx:26  <Window width={350} height={295}>
        return c.Window(350, 295, null,
            new UiNode(UiKind.Section)
            {
                Key = "teleporter",
                Title = "Teleporter Console",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle_open",
                        Text = stateOpen ? "Open" : "Closed",
                        Disabled = locked,
                        Selected = stateOpen,
                        OnClick = () => c.Act("toggle_open"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "teleporter_lock",
                        Icon = locked ? "lock" : "unlock",
                        Text = locked ? "Locked" : "Unlocked",
                        Selected = locked,
                        Disabled = stateOpen,
                        OnClick = () => c.Act("teleporter_lock"),
                    }),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "unit",
                    Label = "Teleporter Unit",
                    Color = teleporter ? "good" : "bad",
                    Text = teleporter ? data.Str("teleporter_location") : "Not Connected",
                    Buttons = !teleporter ? new UiNode(UiKind.Button) { Key = "scan_teleporter", Text = "Reconnect", OnClick = () => c.Act("scan_teleporter") } : null,
                },
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "beacon",
                    Label = "Receiver Beacon",
                    Color = beacon ? "good" : "bad",
                    Text = beacon ? data.Str("beacon_location") : "Not Connected",
                    Buttons = !beacon ? new UiNode(UiKind.Button) { Key = "scan_beacon", Text = "Reconnect", OnClick = () => c.Act("scan_beacon") } : null,
                })),
            new UiNode(UiKind.Section) { Key = "prisoner", Title = "Prisoner Details" }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "id", Label = "Prisoner ID" }.With(new UiNode(UiKind.Button)
                {
                    Key = "handle_id",
                    Fluid = true,
                    Text = id ? data.Str("id_name") : "No ID",
                    OnClick = () => c.Act("handle_id"),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "goal", Label = "Point Goal" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "set_goal",
                    Value = data.Num("goal"),
                    Step = 1,
                    Width = "48px",
                    MinValue = 1,
                    MaxValue = 1000,
                    OnChange = value => c.Act("set_goal", ("value", value)),
                }),
                new UiNode(UiKind.LabeledItem) { Key = "occupant", Label = "Occupant", Text = string.IsNullOrEmpty(prisonerName) ? "No Occupant" : prisonerName },
                new UiNode(UiKind.LabeledItem) { Key = "crimstat", Label = "Criminal Status", Text = string.IsNullOrEmpty(crimstat) ? "No Status" : crimstat })),
            new UiNode(UiKind.Button)
            {
                Key = "teleport",
                Fluid = true,
                Text = "Process Prisoner",
                Disabled = !data.Truthy("can_teleport"),
                TextAlign = "center",
                Color = "bad",
                OnClick = () => c.Act("teleport"),
            });
    }
}
