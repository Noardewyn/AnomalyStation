using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Supermatter.tsx (the engineering package E2, D287: the
// crystal's window for observers, and its content in NtOS's NT CIMS). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Supermatter
{
    // tg: Supermatter.tsx:19
    public static double LogScale(double value) => Math.Log2(16 + Math.Max(0, value)) - 4;

    // tg: Supermatter.tsx:416
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> smData = c.Data.Arr("sm_data");
        c.WindowTheme = "ntos";
        return c.Window(700, 400, null,
            Content(c, smData.Count > 0 ? smData[0] : null, c.Data.Get("gas_metadata"), null, ""));
    }

    // tg: Supermatter.tsx:66  SupermatterEntry: a LabeledList row as a stack, with a chevron that shows its detail. The chevron's state
    // is the component's own (useState): stateKey names this mount of the entry.
    private static IEnumerable<UiNode> Entry(TguiContext c, string stateKey, string key, string title, UiNode content, UiNode detail, bool alwaysShowChevron = false)
    {
        UiNode Row(UiNode chevron) => new UiNode(UiKind.StackItem) { Key = "entry:" + key }.With(
            new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Color = "grey", Width = "125px", Text = title + ":" },
                new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(content),
                chevron));
        if (!alwaysShowChevron && detail == null)
        {
            yield return Row(null);
            yield break;
        }
        string detailKey = stateKey + "detail:" + key;
        bool activeDetail = c.UseState(detailKey, false);
        yield return Row(new UiNode(UiKind.StackItem) { Key = "chevron-cell" }.With(new UiNode(UiKind.Button)
        {
            Key = "chevron",
            Icon = activeDetail ? "chevron-up" : "chevron-down",
            OnClick = () => c.SetState(detailKey, !activeDetail),
        }));
        // tg: Supermatter.tsx:96  {activeDetail && !!detail && <Stack.Item pl={3}>{detail}</Stack.Item>} (the padding is not drawn)
        if (activeDetail && detail != null)
            yield return new UiNode(UiKind.StackItem) { Key = "detail:" + key }.With(detail);
    }

    private static UiNode Bar(string key, double value, double min, double max, string text, params (string, double, double)[] ranges) => new(UiKind.ProgressBar)
    {
        Key = key,
        Value = value,
        MinValue = min,
        MaxValue = max,
        Ranges = ranges,
        Text = text,
    };

    // A factor list (tg: Supermatter.tsx:156 and the others): each factor's amount in green or red; null when there is none
    // (!!factors.length && ...).
    private static UiNode Factors(IReadOnlyList<UiValue> factors, Func<UiValue, string> label, Func<double, bool> green, Func<UiValue, string> text)
    {
        if (factors.Count == 0)
            return null;
        return new UiNode(UiKind.LabeledList).With(factors.Select(f => new UiNode(UiKind.LabeledItem) { Key = f.Str("name"), Label = label(f) }.With(
            new UiNode(UiKind.Box) { Color = green(f.Num("amount")) ? "green" : "red", Text = text(f) })));
    }

    // tg: Supermatter.tsx:101  SupermatterContent. sm is one of the crystals' sm_ui_data (null: none sent); stateKey names this mount of the
    // content, whose local state (the chevrons, Show All Gases) React keeps per mounted component.
    public static UiNode Content(TguiContext c, UiValue sm, UiValue gasMetadata, UiNode sectionButton, string stateKey)
    {
        double uid = sm.Num("uid");
        string areaName = sm.Str("area_name");
        double integrity = sm.Num("integrity");
        double internalEnergy = sm.Num("internal_energy");
        double zapTransmission = sm.Num("zap_transmission");
        double tempLimit = sm.Num("temp_limit");
        double wasteMultiplier = sm.Num("waste_multiplier");
        double absorbedRatio = sm.Num("absorbed_ratio");
        double gasTemperature = sm.Num("gas_temperature");
        double gasTotalMoles = sm.Num("gas_total_moles");

        string allGasKey = stateKey + "allGasActive";
        bool allGasActive = c.UseState(allGasKey, false);
        IEnumerable<KeyValuePair<string, UiValue>> composition = sm.Obj("gas_composition");
        if (!allGasActive)
            composition = composition.Where(gas => gas.Value.AsNum() != 0);
        // tg: Supermatter.tsx:130  sortBy(gas_composition, [([gas_path, amount]) => -amount]) (stable)
        List<KeyValuePair<string, UiValue>> gasComposition = composition.OrderBy(gas => -gas.Value.AsNum()).ToList();

        var smEntries = new List<UiNode>();
        smEntries.AddRange(Entry(c, stateKey, "integrity", "Integrity",
            Bar("integrity", integrity / 100, 0, 1, $"{TguiFormat.ToFixed(integrity, 2)} %",
                ("good", 0.9, double.PositiveInfinity), ("average", 0.5, 0.9), ("bad", double.NegativeInfinity, 0.5)),
            Factors(sm.Arr("integrity_factors"), f => $"{f.Str("name")} (∆)", amount => amount > 0, f => $"{TguiFormat.ToFixed(f.Num("amount"), 2)} %"),
            alwaysShowChevron: true));
        smEntries.AddRange(Entry(c, stateKey, "internal_energy", "Internal Energy",
            Bar("internal_energy", internalEnergy, 0, 5000, TguiFormat.ToFixed(sm.Num("internal_energy_coefficient"), 3) + sm.Str("internal_energy_unit"),
                ("good", double.NegativeInfinity, 5000), ("average", 5000, 7000), ("bad", 7000, double.PositiveInfinity)),
            Factors(sm.Arr("internal_energy_factors"), f => $"{f.Str("name")} (∆)", amount => amount > 0, f => TguiFormat.ToFixed(f.Num("amount"), 3) + f.Str("unit")),
            alwaysShowChevron: true));
        smEntries.AddRange(Entry(c, stateKey, "zap_transmission", "Zap Power Transmission",
            Bar("zap_transmission", zapTransmission, 0, 1e7, TguiFormat.ToFixed(sm.Num("zap_transmission_coefficient"), 2) + sm.Str("zap_transmission_unit"),
                ("teal", 1e7, double.PositiveInfinity), ("good", 2e6, 1e7), ("average", 1e6, 2e6), ("bad", double.NegativeInfinity, 1e6)),
            Factors(sm.Arr("zap_transmission_factors"), f => f.Str("name"), amount => amount > 0, f => TguiFormat.ToFixed(f.Num("amount"), 2) + f.Str("unit")),
            alwaysShowChevron: true));
        smEntries.AddRange(Entry(c, stateKey, "absorbed_moles", "Absorbed Moles",
            Bar("absorbed_moles", gasTotalMoles, 0, 2000, $"{TguiFormat.ToFixed(gasTotalMoles, 2)} Moles",
                ("good", 0, 900), ("average", 900, 1800), ("bad", 1800, double.PositiveInfinity)),
            null));
        smEntries.AddRange(Entry(c, stateKey, "temperature", "Temperature",
            Bar("temperature", LogScale(gasTemperature), 0, LogScale(10000), $"{TguiFormat.ToFixed(gasTemperature, 2)} K",
                ("teal", double.NegativeInfinity, LogScale(100)), ("good", LogScale(100), LogScale(300)),
                ("average", LogScale(300), LogScale(tempLimit)), ("bad", LogScale(tempLimit), double.PositiveInfinity)),
            null));
        smEntries.AddRange(Entry(c, stateKey, "temp_limit", "Temperature Limit",
            new UiNode(UiKind.Box) { Key = "temp_limit", Text = $"{TguiFormat.JsNumber(tempLimit)} K" },
            Factors(sm.Arr("temp_limit_factors"), f => f.Str("name"), amount => amount > 0, f => $"{TguiFormat.ToFixed(f.Num("amount"), 2)} K"),
            alwaysShowChevron: true));
        smEntries.AddRange(Entry(c, stateKey, "waste_multiplier", "Waste Multiplier",
            Bar("waste_multiplier", wasteMultiplier, 0, 20, $"{TguiFormat.ToFixed(wasteMultiplier, 2)} x",
                ("good", double.NegativeInfinity, 0.8), ("average", 0.8, 2), ("bad", 2, double.PositiveInfinity)),
            Factors(sm.Arr("waste_multiplier_factors"), f => f.Str("name"), amount => amount < 0, f => $"{TguiFormat.ToFixed(f.Num("amount"), 2)} x"),
            alwaysShowChevron: true));
        smEntries.AddRange(Entry(c, stateKey, "absorption_ratio", "Absorption Ratio",
            new UiNode(UiKind.Box) { Key = "absorption_ratio", Text = $"{TguiFormat.JsNumber(absorbedRatio * 100)}%" },
            null));

        var gasEntries = new List<UiNode>();
        foreach ((string gasPath, UiValue amountValue) in gasComposition)
        {
            double amount = amountValue.AsNum();
            (string Label, string Color)? gas = TguiFormat.GetGasFromPath(gasPath);
            gasEntries.AddRange(Entry(c, stateKey, gasPath, gas?.Label ?? "Unknown",
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "amount",
                    Color = gas?.Color,
                    Value = amount,
                    MinValue = 0,
                    MaxValue = 1,
                    Text = $"{TguiFormat.ToFixed(amount * 100, 2)}%",
                },
                GasDetail(gasMetadata?.Get(gasPath))));
        }

        // tg: Supermatter.tsx:133
        return new UiNode(UiKind.Stack) { Height = "100%" }.With(
            new UiNode(UiKind.StackItem) { Key = "sm", Grow = 1 }.With(
                new UiNode(UiKind.Section)
                {
                    Key = "sm",
                    Fill = true,
                    Scrollable = true,
                    Title = $"{TguiFormat.JsNumber(uid)}. {areaName}",
                    Buttons = sectionButton,
                }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(smEntries))),
            // tg: Supermatter.tsx:331
            new UiNode(UiKind.StackItem) { Key = "gases", Grow = 1 }.With(
                new UiNode(UiKind.Section)
                {
                    Key = "gases",
                    Fill = true,
                    Scrollable = true,
                    Title = "Gases",
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "allgas",
                        Icon = allGasActive ? "times" : "book-open",
                        Text = allGasActive ? "Hide Gases" : "Show All Gases",
                        OnClick = () => c.SetState(allGasKey, !allGasActive),
                    },
                }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(gasEntries))));
    }

    // tg: Supermatter.tsx:363  the gas entry's detail: its numeric effects at 100% composition, from the static gas_metadata
    private static UiNode GasDetail(UiValue metadata)
    {
        if (metadata == null || metadata.Kind != UiValueKind.Object)
            return new UiNode(UiKind.Box) { Key = "noeffects", Text = "Has no effects" };
        var detail = new UiNode(UiKind.Fragment);
        if (metadata.Truthy("desc"))
            detail.With(new UiNode(UiKind.Box) { Key = "br" }); // {gas_metadata[gas_path].desc && <br />}
        IReadOnlyList<UiValue> numericData = metadata.Arr("numeric_data");
        if (numericData.Count == 0)
            return detail.With(new UiNode(UiKind.Box) { Key = "nocomposition", Text = "Has no composition effects" });
        // tg: Supermatter.tsx:369  <Box mb={1}>At <b>100% Composition</b> gives:</Box> (the margin is not drawn)
        detail.With(new UiNode(UiKind.Stack) { Key = "composition" }.With(
            new UiNode(UiKind.StackItem) { Text = "At" },
            new UiNode(UiKind.StackItem) { Bold = true, Text = "100% Composition" },
            new UiNode(UiKind.StackItem) { Text = "gives:" }));
        var list = new UiNode(UiKind.LabeledList) { Key = "effects" };
        foreach (UiValue effect in numericData)
        {
            double amount = effect.Num("amount");
            if (amount == 0)
                continue;
            string unit = effect.Str("unit");
            bool positive = effect.Truthy("positive");
            // tg: Supermatter.tsx:382  <LabeledList.Item color> (the item's colour is its content's)
            list.With(new UiNode(UiKind.LabeledItem) { Key = effect.Str("name"), Label = effect.Str("name") }.With(new UiNode(UiKind.Box)
            {
                Color = positive ? (amount > 0 ? "green" : "red") : (amount < 0 ? "green" : "red"),
                Text = amount > 0 ? $"+{TguiFormat.JsNumber(amount)}{unit}" : TguiFormat.JsNumber(amount) + unit,
            }));
        }
        return detail.With(list);
    }
}
