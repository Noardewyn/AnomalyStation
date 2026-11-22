using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosFilter.tsx (package E1: the gas filter's window, the
// supermatter loop's filters). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AtmosFilter
{
    // tg: tgui/packages/tgui/interfaces/AtmosFilter.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> filter_types = data.Arr("filter_types");
        bool on = data.Truthy("on");
        double rate = data.Num("rate");
        double max_rate = data.Num("max_rate");

        // tg: AtmosFilter.tsx:27  <Window width={440} height={240}>
        return c.Window(440, 240, null,
            // tg: AtmosFilter.tsx:29  <Section buttons={power button} fill title="Gas Filter">
            new UiNode(UiKind.Section)
            {
                Title = "Gas Filter",
                Fill = true,
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = on ? "power-off" : "times",
                    Text = on ? "On" : "Off",
                    Selected = on,
                    OnClick = () => c.Act("power"),
                },
            }.With(
                new UiNode(UiKind.LabeledList).With(
                    // tg: AtmosFilter.tsx:42
                    new UiNode(UiKind.LabeledItem) { Label = "Transfer Rate" }.With(
                        new UiNode(UiKind.NumberInput)
                        {
                            Key = "rate",
                            Value = rate,
                            Step = 1,
                            Width = "63px",
                            Unit = "L/s",
                            MinValue = 0,
                            MaxValue = max_rate,
                            OnChange = value => c.Act("rate", ("rate", value)),
                        },
                        new UiNode(UiKind.Button)
                        {
                            Key = "rate-max",
                            Icon = "plus",
                            Text = "Max",
                            Disabled = rate == max_rate,
                            OnClick = () => c.Act("rate", ("rate", "max")),
                        }),
                    // tg: AtmosFilter.tsx:68  the filter buttons wrap at the window's width, as tgui's inline buttons do
                    new UiNode(UiKind.LabeledItem) { Label = "Filter Types", Wrap = true }.With(
                        filter_types.Select(filter =>
                        {
                            string gas_id = filter.Str("gas_id");
                            bool enabled = filter.Truthy("enabled");
                            return new UiNode(UiKind.Button)
                            {
                                Key = gas_id,
                                Icon = enabled ? "check-square-o" : "square-o",
                                Selected = enabled,
                                OnClick = () => c.Act("toggle_filter", ("val", gas_id)),
                                Text = TguiFormat.GetGasLabel(gas_id),
                            };
                        }).ToArray()))));
    }
}
