using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemSeparator.tsx (gaps package P5). The window reads
// the distillation apparatus's ui_data (chem_separator.dm:451: the flask, beaker and fuel with their volume, temperature and colour,
// the burner knob, the condenser) and ui_static_data (:445) and sends drain, filter, knob and cool. The kit has no CSS transform: the
// row labels sit in tg's label column unshifted. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemSeparator
{
    // tg: ChemSeparator.tsx:66  a holder's bar: "<ceil volume> of <max> units at <ceil temp>K"
    private static UiNode HolderBar(string key, UiValue holder, string width) => new UiNode(UiKind.ProgressBar)
    {
        Key = key,
        Height = 2,
        MinValue = 0,
        MaxValue = holder.Num("maximum_volume"),
        Value = holder.Num("total_volume"),
        Color = holder.Str("color"),
        Width = width,
        Text = TguiFormat.JsNumber(Math.Ceiling(holder.Num("total_volume"))) + " of " + TguiFormat.JsNumber(holder.Num("maximum_volume")) + " units at " + TguiFormat.JsNumber(Math.Ceiling(holder.Num("temp"))) + "K",
    };

    // tg: ChemSeparator.tsx:31
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue flask = data.Get("flask");
        UiValue beaker = data.Get("beaker");
        UiValue fuel = data.Get("fuel");
        bool hasBeaker = beaker?.Truthy ?? false, hasFuel = fuel?.Truthy ?? false;

        UiNode list = new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "flask", Label = "Flask:" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem).With(HolderBar("flaskBar", flask, "170px")),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "drain",
                    Width = 6,
                    TextAlign = "center",
                    Icon = "arrow-down",
                    Disabled = flask.Num("total_volume") <= 0 || !hasBeaker,
                    Text = "Drain",
                    OnClick = () => c.Act("drain"),
                }))),
            hasBeaker
                ? new UiNode(UiKind.LabeledItem) { Key = "beaker", Label = "Beaker:" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem).With(HolderBar("beakerBar", beaker, "170px")),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "filter",
                        Width = 6,
                        TextAlign = "center",
                        Icon = "filter",
                        Disabled = beaker.Num("total_volume") <= 0,
                        Text = "Filter",
                        OnClick = () => c.Act("filter"),
                    })))
                : null,
            new UiNode(UiKind.LabeledItem) { Key = "knob", Label = "Burner Knob:" }.With(new UiNode(UiKind.Knob)
            {
                Key = "knobValue",
                Size = 1.3,
                MinValue = 1,
                MaxValue = data.Num("max_burner_knob_settings"),
                Step = 1,
                StepPixelSize = 10,
                Value = data.Num("knob"),
                OnChange = value => c.Act("knob", ("amount", value)),
            }),
            hasFuel
                ? new UiNode(UiKind.LabeledItem) { Key = "fuel", Label = "Fuel Source:" }.With(HolderBar("fuelBar", fuel, "170px"))
                : null,
            // tg: :213  condenser_installed && (flask.total_volume > 0 || beaker?.total_volume > 0) && fuel?.total_volume > 0
            data.Truthy("condenser_installed") && (flask.Num("total_volume") > 0 || (hasBeaker && beaker.Num("total_volume") > 0)) && hasFuel && fuel.Num("total_volume") > 0
                ? new UiNode(UiKind.LabeledItem) { Key = "cooling", Label = "Cooling:" }.With(new UiNode(UiKind.Button)
                {
                    Key = "cool",
                    Icon = "thermometer-full",
                    Disabled = data.Truthy("burner_on"),
                    Selected = data.Truthy("condenser_on"),
                    FontSize = "15px",
                    Text = "Start",
                    OnClick = () => c.Act("cool"),
                })
                : null);

        // tg: ChemSeparator.tsx:45  <Window width={370} height={215}>
        return c.Window(370, 215, null, list);
    }
}
