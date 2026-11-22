using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MedicalKiosk.jsx (gaps MD1). The window reads tg's
// ui_data of the medical kiosk (machinery/medical_kiosk.dm:215) and sends beginScan_1..4 and clearTarget; the scan shown is the
// window's local state (tg's shared state 'scanIndex'). tgui-core's AnimatedNumber renders as its text; the instructions' italics and
// the bold price are plain text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MedicalKiosk
{
    private static readonly (int Index, string Icon, string Name, string Description)[] Scans =
    {
        // tg: MedicalKiosk.jsx:25-58
        (1, "procedures", "General Health Scan", "Reads back exact values of your general health scan."),
        (2, "heartbeat", "Symptom Based Checkup", "Provides information based on various non-obvious symptoms, like blood levels or disease status."),
        (3, "radiation-alt", "Neurological/Radiological Scan", "Provides information about brain trauma and radiation."),
        (4, "mortar-pestle", "Chemical and Psychoactive Scan", "Provides a list of consumed chemicals, as well as potential side effects."),
    };

    // tg: MedicalKiosk.jsx:280 (the blood alcohol bar's ranges)
    private static readonly (string, double, double)[] AlcoholRanges =
    {
        ("blue", double.NegativeInfinity, 0.23),
        ("bad", 0.23, double.PositiveInfinity),
    };

    // tg: MedicalKiosk.jsx:16
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int scanIndex = c.UseState("scanIndex", 0);
        // tg: MedicalKiosk.jsx:22  <Window width={575} height={420}><Window.Content scrollable>
        return c.Window(575, 420, null,
            new UiNode(UiKind.Stack) { Key = "top" }.With(
                new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                    new UiNode(UiKind.Section) { Key = "scans" }.With(Scans.Select(s => ScanButton(c, data, s, scanIndex)))),
                new UiNode(UiKind.StackItem) { Key = "instructions", Grow = 1 }.With(Instructions(c, data))),
            data.Truthy("active_status_1") && scanIndex == 1 ? Results1(data) : null,
            data.Truthy("active_status_2") && scanIndex == 2 ? Results2(data) : null,
            data.Truthy("active_status_3") && scanIndex == 3 ? Results3(data) : null,
            data.Truthy("active_status_4") && scanIndex == 4 ? Results4(data) : null);
    }

    // tg: MedicalKiosk.jsx:76
    private static UiNode ScanButton(TguiContext c, UiValue data, (int Index, string Icon, string Name, string Description) scan, int scanIndex)
    {
        bool paid = data.Truthy("active_status_" + scan.Index);
        return new UiNode(UiKind.Stack) { Key = "scan" + scan.Index, Align = "baseline" }.With(
            new UiNode(UiKind.StackItem) { Key = "paid", Width = "16px", TextAlign = "center" }.With(
                new UiNode(UiKind.Icon) { Icon = paid ? "check" : "dollar-sign", Color = paid ? "green" : "grey" }),
            new UiNode(UiKind.StackItem) { Key = "button", Grow = 1 }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "beginScan_" + scan.Index,
                    Fluid = true,
                    Icon = scan.Icon,
                    Selected = paid && scanIndex == scan.Index,
                    Tooltip = scan.Description,
                    Text = scan.Name,
                    OnClick = () =>
                    {
                        if (!paid)
                            c.Act("beginScan_" + scan.Index);
                        c.SetState("scanIndex", scan.Index);
                    },
                }));
    }

    // tg: MedicalKiosk.jsx:111
    private static UiNode Instructions(TguiContext c, UiValue data)
    {
        return new UiNode(UiKind.Section) { Key = "instructions" }.With(
            new UiNode(UiKind.Box)
            {
                Key = "greeting",
                Text = "Greetings Valued Employee! Please select a desired automatic health check procedure. Diagnosis costs "
                       + TguiFormat.JsNumber(data.Num("kiosk_cost")) + " credits.",
            },
            new UiNode(UiKind.Box) { Key = "patient" }.With(
                new UiNode(UiKind.Box) { Key = "label", Color = "label", Text = "Patient: " + data.Str("patient_name") }),
            new UiNode(UiKind.Button)
            {
                Key = "clearTarget",
                Tooltip = "Resets the current scanning target, cancelling current scans.",
                Icon = "sync",
                Color = "average",
                Text = "Reset Scanner",
                OnClick = () => c.Act("clearTarget"),
            });
    }

    // a ProgressBar of tg's value/100 with the AnimatedNumber's text
    private static UiNode Bar(string key, double value, string color, string suffix = "") => new UiNode(UiKind.ProgressBar)
    {
        Key = key,
        Value = value / 100,
        Color = color,
        Text = TguiFormat.JsNumber(value) + suffix,
    };

    // tg: MedicalKiosk.jsx:140
    private static UiNode Results1(UiValue data)
    {
        return new UiNode(UiKind.Section) { Key = "results1", Title = "Patient Health" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Total Health" }.With(Bar("total", data.Num("patient_health"), null, "%")),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.LabeledItem) { Label = "Brute Damage" }.With(Bar("brute", data.Num("brute_health"), "bad")),
                new UiNode(UiKind.LabeledItem) { Label = "Burn Damage" }.With(Bar("burn", data.Num("burn_health"), "bad")),
                new UiNode(UiKind.LabeledItem) { Label = "Oxygen Damage" }.With(Bar("oxygen", data.Num("suffocation_health"), "bad")),
                new UiNode(UiKind.LabeledItem) { Label = "Toxin Damage" }.With(Bar("toxin", data.Num("toxin_health"), "bad"))));
    }

    // tg: MedicalKiosk.jsx:183
    private static UiNode Results2(UiValue data)
    {
        string bloodName = data.Str("blood_name");
        return new UiNode(UiKind.Section) { Key = "results2", Title = "Symptom Based Checkup" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Patient Status", Color = "good", Text = data.Str("patient_status") },
                new UiNode(UiKind.Divider) { Key = "divider1" },
                new UiNode(UiKind.LabeledItem) { Label = "Disease Status", Text = data.Str("patient_illness") },
                new UiNode(UiKind.LabeledItem) { Label = "Disease information", Text = data.Str("illness_info") },
                new UiNode(UiKind.Divider) { Key = "divider2" },
                new UiNode(UiKind.LabeledItem) { Label = bloodName + " Levels" }.With(
                    Bar("blood", data.Num("blood_levels"), "bad"),
                    new UiNode(UiKind.Box) { Key = "bleed", Color = "label", Text = data.Str("bleed_status") }),
                new UiNode(UiKind.LabeledItem) { Label = bloodName + " Information", Text = data.Str("blood_status") }));
    }

    // tg: MedicalKiosk.jsx:227
    private static UiNode Results3(UiValue data)
    {
        return new UiNode(UiKind.Section) { Key = "results3", Title = "Patient Neurological Health" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Brain Damage" }.With(Bar("brain", data.Num("brain_damage"), "good")),
                new UiNode(UiKind.LabeledItem) { Label = "Brain Status", Color = "health-0", Text = data.Str("brain_health") },
                new UiNode(UiKind.LabeledItem) { Label = "Brain Trauma Status", Text = data.Str("trauma_status") }));
    }

    // tg: MedicalKiosk.jsx:249
    private static UiNode Results4(UiValue data)
    {
        IReadOnlyList<UiValue> chemicals = data.Get("chemical_list")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> overdoses = data.Get("overdose_list")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> addictions = data.Get("addict_list")?.Items ?? Array.Empty<UiValue>();
        double alcohol = data.Num("blood_alcohol");
        return new UiNode(UiKind.Section) { Key = "results4", Title = "Chemical and Psychoactive Analysis" }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Chemical Contents" }.With(
                    chemicals.Count == 0 ? new UiNode(UiKind.Box) { Key = "none", Color = "average", Text = "No reagents detected." } : null,
                    new UiNode(UiKind.Fragment).With(chemicals.Select((chem, i) => new UiNode(UiKind.Box)
                    {
                        Key = "chem" + i,
                        Color = "good",
                        Text = TguiFormat.JsNumber(chem.Num("volume")) + " units of " + chem.Str("name"),
                    }))),
                new UiNode(UiKind.LabeledItem) { Label = "Overdose Status", Color = "bad" }.With(
                    overdoses.Count == 0 ? new UiNode(UiKind.Box) { Key = "none", Color = "good", Text = "Patient is not overdosing." } : null,
                    new UiNode(UiKind.Fragment).With(overdoses.Select((chem, i) => new UiNode(UiKind.Box) { Key = "od" + i, Text = "Overdosing on " + chem.Str("name") }))),
                new UiNode(UiKind.LabeledItem) { Label = "Addiction Status", Color = "bad" }.With(
                    addictions.Count == 0 ? new UiNode(UiKind.Box) { Key = "none", Color = "good", Text = "Patient has no addictions." } : null,
                    new UiNode(UiKind.Fragment).With(addictions.Select((chem, i) => new UiNode(UiKind.Box) { Key = "addict" + i, Text = "Addicted to " + chem.Str("name") }))),
                new UiNode(UiKind.LabeledItem) { Label = "Psychoactive Status", Text = data.Str("hallucinating_status") },
                new UiNode(UiKind.LabeledItem) { Label = "Blood Alcohol Content" }.With(
                    new UiNode(UiKind.ProgressBar)
                    {
                        Key = "alcohol",
                        Value = alcohol,
                        MinValue = 0,
                        MaxValue = 0.3,
                        Ranges = AlcoholRanges,
                        Text = TguiFormat.JsNumber(alcohol),
                    })));
    }
}
