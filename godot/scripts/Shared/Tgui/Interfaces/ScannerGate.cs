using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ScannerGate.jsx (gaps SG1): the scanner gate's lock
// notice, its mode menu and each mode's settings (machinery/scanner_gate.dm:310 ui_static_data, :319 ui_data).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ScannerGate
{
    // tg: ScannerGate.jsx:6
    private static readonly string[] DiseaseThresholds = { "Positive", "Harmless", "Minor", "Medium", "Harmful", "Dangerous", "BIOHAZARD" };

    // tg: ScannerGate.jsx:16
    private static readonly (string Name, double Value)[] TargetNutrition = { ("Starving", 150), ("Obese", 600) };

    // tg: ScannerGate.jsx:27
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: ScannerGate.jsx:30  <Window width={400} height={300}>
        return c.Window(400, 300, null,
            new UiNode(UiKind.Box) { Key = "content", Scrollable = true }.With(
                AirAlarm.InterfaceLockNoticeBox(c, lockAction: "toggle_lock"),
                !data.Truthy("locked") ? Control(c, data) : null));
    }

    // tg: ScannerGate.jsx:42  SCANNER_GATE_ROUTES
    private static string Title(string mode) => mode switch
    {
        "Wanted" or "Guns" or "Mindshield" or "Disease" or "Species" or "Nutrition" => "Scanner Mode: " + mode,
        _ => "Scanner Mode: Off",
    };

    // tg: ScannerGate.jsx:73
    private static UiNode Control(TguiContext c, UiValue data)
    {
        string mode = data.Str("scan_mode");
        UiNode body = mode switch
        {
            "Wanted" => Described(c, data, "warrants", "any warrants for their arrest."),
            "Guns" => Described(c, data, "guns", "any guns."),
            "Mindshield" => Described(c, data, "mindshield", "a mindshield."),
            "Disease" => Disease(c, data),
            "Species" => Species(c, data),
            "Nutrition" => Nutrition(c, data),
            _ => Off(c),
        };
        return new UiNode(UiKind.Section)
        {
            Key = "mode",
            Title = Title(mode),
            Buttons = mode != "Off"
                ? new UiNode(UiKind.Button) { Key = "back", Icon = "arrow-left", Text = "back", OnClick = () => c.Act("set_mode", ("new_mode", "Off")) }
                : null,
        }.With(body);
    }

    // tg: ScannerGate.jsx:96
    private static UiNode Off(TguiContext c)
    {
        UiNode ModeButton(string mode) => new UiNode(UiKind.Button) { Key = "mode:" + mode, Text = mode, OnClick = () => c.Act("set_mode", ("new_mode", mode)) };
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Box) { Key = "hint", Text = "Select a scanning mode below." },
            new UiNode(UiKind.Box) { Key = "modes" }.With(
                ModeButton("Wanted"), ModeButton("Guns"), ModeButton("Mindshield"), ModeButton("Disease"), ModeButton("Species"), ModeButton("Nutrition")));
    }

    // tg: ScannerGate.jsx:131, :146, :161
    private static UiNode Described(TguiContext c, UiValue data, string key, string what)
    {
        bool reverse = data.Truthy("reverse");
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Box) { Key = "trigger:" + key, Text = "Trigger if the person scanned " + (reverse ? "does not have" : "has") + " " + what },
            Mode(c, data));
    }

    // tg: ScannerGate.jsx:176
    private static UiNode Disease(TguiContext c, UiValue data)
    {
        bool reverse = data.Truthy("reverse");
        string threshold = data.Str("disease_threshold");
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Box) { Key = "trigger:disease", Text = "Trigger if the person scanned " + (reverse ? "does not have" : "has") + " a disease equal or worse than " + threshold + "." },
            new UiNode(UiKind.Box) { Key = "thresholds" }.With(DiseaseThresholds.Select(t => new UiNode(UiKind.Button)
            {
                Key = "threshold:" + t,
                Checkbox = t == threshold,
                Text = t,
                OnClick = () => c.Act("set_disease_threshold", ("new_threshold", t)),
            })),
            Mode(c, data));
    }

    // tg: ScannerGate.jsx:203
    private static UiNode Species(TguiContext c, UiValue data)
    {
        bool reverse = data.Truthy("reverse");
        string target = data.Str("target_species_id");
        IReadOnlyList<UiValue> available = data.Get("available_species")?.Items ?? Array.Empty<UiValue>();
        UiValue species = available.FirstOrDefault(s => s.Str("specie_id") == target);
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Box)
            {
                Key = "trigger:species",
                Text = "Trigger if the person scanned is " + (reverse ? "not" : "") + " of the " + species?.Str("specie_name") + " species."
                    + (data.Truthy("target_zombie") ? " All zombie types will be detected, including dormant zombies." : ""),
            },
            new UiNode(UiKind.Box) { Key = "species" }.With(available.Select(s => new UiNode(UiKind.Button)
            {
                Key = "species:" + s.Str("specie_id"),
                Checkbox = s.Str("specie_id") == target,
                Text = s.Str("specie_name"),
                OnClick = () => c.Act("set_target_species", ("new_species_id", s.Str("specie_id"))),
            })),
            Mode(c, data));
    }

    // tg: ScannerGate.jsx:238
    private static UiNode Nutrition(TguiContext c, UiValue data)
    {
        bool reverse = data.Truthy("reverse");
        double target = data.Num("target_nutrition");
        string name = TargetNutrition.FirstOrDefault(n => n.Value == target).Name;
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Box) { Key = "trigger:nutrition", Text = "Trigger if the person scanned " + (reverse ? "does not have" : "has") + " the " + name + " nutrition level." },
            new UiNode(UiKind.Box) { Key = "nutrition" }.With(TargetNutrition.Select(n => new UiNode(UiKind.Button)
            {
                Key = "nutrition:" + n.Name,
                Checkbox = n.Value == target,
                Text = n.Name,
                OnClick = () => c.Act("set_target_nutrition", ("new_nutrition", n.Name)),
            })),
            Mode(c, data));
    }

    // tg: ScannerGate.jsx:268
    private static UiNode Mode(TguiContext c, UiValue data)
    {
        bool reverse = data.Truthy("reverse");
        return new UiNode(UiKind.LabeledList) { Key = "scanning_mode" }.With(
            new UiNode(UiKind.LabeledItem) { Label = "Scanning Mode" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggle_reverse",
                Text = reverse ? "Inverted" : "Default",
                Icon = reverse ? "random" : "long-arrow-alt-right",
                Color = reverse ? "bad" : "good",
                OnClick = () => c.Act("toggle_reverse"),
            }));
    }
}
