using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ImplantChair.jsx (gaps DV2): the mindshield implanter's
// occupant, its door, the implanting with its recharge cog and the implants left with the replenishing spinner
// (items/implants/implantchair.dm:39 ui_data). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ImplantChair
{
    // tg: ImplantChair.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue occupant = data.Get("occupant");
        string occupantName = occupant?.Str("name");
        double stat = occupant?.Num("stat") ?? 0;
        bool open = data.Truthy("open");
        bool ready = data.Truthy("ready");
        string specialName = data.Str("special_name");
        // tg: ImplantChair.jsx:9  <Window width={375} height={280}>
        return c.Window(375, 280, null,
            new UiNode(UiKind.Section) { Key = "occupant", Title = "Occupant Information", TextAlign = "center" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = string.IsNullOrEmpty(occupantName) ? "No Occupant" : occupantName },
                    data.Truthy("occupied")
                        ? new UiNode(UiKind.LabeledItem)
                        {
                            Key = "status",
                            Label = "Status",
                            Color = stat == 0 ? "good" : stat == 1 ? "average" : "bad",
                            Text = stat == 0 ? "Conscious" : stat == 1 ? "Unconscious" : "Dead",
                        }
                        : null)),
            new UiNode(UiKind.Section) { Key = "operations", Title = "Operations", TextAlign = "center" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "door_row", Label = "Door" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "door",
                        Icon = open ? "unlock" : "lock",
                        Color = open ? "default" : "red",
                        Text = open ? "Open" : "Closed",
                        OnClick = () => c.Act("door"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "implant_row", Label = "Implant Occupant" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "implant",
                            Icon = "code-branch",
                            Text = ready ? (string.IsNullOrEmpty(specialName) ? "Implant" : specialName) : "Recharging",
                            OnClick = () => c.Act("implant"),
                        },
                        // tg: data.ready === 0
                        data.Num("ready", 1) == 0 ? new UiNode(UiKind.Icon) { Key = "cog", Icon = "cog", Color = "orange", Spin = true } : null),
                    new UiNode(UiKind.LabeledItem) { Key = "remaining_row", Label = "Implants Remaining", Text = TguiFormat.JsNumber(data.Num("ready_implants")) }.With(
                        // tg: data.replenishing === 1
                        data.Num("replenishing") == 1 ? new UiNode(UiKind.Icon) { Key = "sync", Icon = "sync", Color = "red", Spin = true } : null))));
    }
}
