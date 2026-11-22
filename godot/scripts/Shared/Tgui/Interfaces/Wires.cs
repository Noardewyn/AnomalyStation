// Port of tgstation's Wires.tsx: wire colors and cut/mend/pulse actions.
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

public static class Wires
{
    public static UiNode Render(TguiContext c)
    {
        var rows = new UiNode(UiKind.LabeledList);
        foreach (UiValue wire in c.Data.Arr("wires"))
        {
            string color = wire.Str("color");
            bool cut = wire.Truthy("cut");
            string purpose = wire.Str("wire");
            rows.With(new UiNode(UiKind.LabeledItem) { Label = string.IsNullOrEmpty(purpose) ? color : color + " (" + purpose + ")" }.With(
                new UiNode(UiKind.Button) { Key = color + "-cut", Text = cut ? "Mend" : "Cut", Icon = "cut", OnClick = () => c.Act("cut", ("wire", color)) },
                new UiNode(UiKind.Button) { Key = color + "-pulse", Text = "Pulse", Icon = "bolt", Disabled = cut, OnClick = () => c.Act("pulse", ("wire", color)) }));
        }
        var status = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue line in c.Data.Arr("status"))
            status.With(new UiNode(UiKind.Box) { Text = line.AsStr() });
        return c.Window(440, 580, null, new UiNode(UiKind.Section) { Title = "Wires" }.With(rows),
            new UiNode(UiKind.Section) { Title = "Status" }.With(status));
    }
}
