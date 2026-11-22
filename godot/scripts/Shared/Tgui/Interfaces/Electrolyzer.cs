using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Electrolyzer.tsx (engineering package E12a: the
// portable electrolyzer's window). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Electrolyzer
{
    // tg: Electrolyzer.tsx:20
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasPowercell = data.Truthy("hasPowercell");
        bool on = data.Truthy("on");
        bool open = data.Truthy("open");
        bool anchored = data.Truthy("anchored");
        double powerLevel = data.Num("powerLevel");

        // tg: Electrolyzer.tsx:25  <Window width={400} height={305}>
        return c.Window(400, 305, null,
            new UiNode(UiKind.Section)
            {
                Title = "Power",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "eject",
                        Icon = "eject",
                        Text = "Eject Cell",
                        Disabled = !hasPowercell || !open,
                        OnClick = () => c.Act("eject"),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = on ? "power-off" : "times",
                        Text = on ? "On" : "Off",
                        Selected = on,
                        Disabled = !hasPowercell && !anchored,
                        OnClick = () => c.Act("power"),
                    }),
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: Electrolyzer.tsx:47  the cell's charge bar, or 'None' in red
                    hasPowercell
                        ? new UiNode(UiKind.LabeledItem) { Label = "Cell" }.With(
                            new UiNode(UiKind.ProgressBar)
                            {
                                Key = "cell",
                                Value = powerLevel / 100,
                                Ranges = new[] { ("good", 0.6, double.PositiveInfinity), ("average", 0.3, 0.6), ("bad", double.NegativeInfinity, 0.3) },
                            })
                        : new UiNode(UiKind.LabeledItem) { Label = "Cell", Color = "bad", Text = "None" })));
    }
}
