using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Pandemic/ (index.tsx, Beaker.tsx, Specimen.tsx,
// Virus.tsx, Symptom.tsx, helpers.ts; gaps package VI1). The window reads the PanD.E.M.I.C.'s ui_data (pandemic.dm:133: the beaker,
// the blood's DNA and type, its antibodies and its viruses with their symptoms) and sends eject_beaker, empty_beaker,
// empty_eject_beaker, create_vaccine_bottle, create_culture_bottle and rename_disease. tgui's Tooltip wrapper of a LabeledList.Item
// is the item's own tooltip; the kit's Input has no placeholder, so the rename field shows an empty value for "Unknown" as tg does.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Pandemic
{
    // tg: Pandemic/helpers.ts:2  getColor
    private static string GetColor(double severity)
    {
        if (severity <= -10)
            return "blue";
        if (severity <= -5)
            return "darkturquoise";
        if (severity <= 0)
            return "green";
        if (severity <= 7)
            return "yellow";
        if (severity <= 13)
            return "orange";
        return "bad";
    }

    // tgui-core's capitalizeFirst
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static UiNode Item(string key, string label, string text, string color = null, string tooltip = null) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label, Color = color, Tooltip = tooltip, Text = text };

    // tg: Pandemic/index.tsx:9
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasBeaker = data.Truthy("has_beaker"), hasBlood = data.Truthy("has_blood");
        UiNode stack = new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "beaker" }.With(BeakerDisplay(c)));
        if (hasBeaker && hasBlood)
            stack.With(new UiNode(UiKind.StackItem) { Key = "specimen", Grow = 1 }.With(SpecimenDisplay(c)));
        return c.Window(650, 500, null, stack);
    }

    // tg: Pandemic/Beaker.tsx:15  BeakerDisplay
    private static UiNode BeakerDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasBeaker = data.Truthy("has_beaker"), hasBlood = data.Truthy("has_blood");
        UiValue beaker = data.Get("beaker");
        double volume = beaker.Num("volume");
        bool cantEmpty = !hasBeaker || volume == 0;
        UiNode content;
        if (!hasBeaker)
            content = new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No beaker loaded." };
        else if (volume == 0)
            content = new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "Beaker is empty." };
        else if (!hasBlood)
            content = new UiNode(UiKind.NoticeBox) { Key = "noblood", Text = "No blood sample loaded." };
        else
            content = new UiNode(UiKind.Stack) { Key = "info", Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "info" }.With(Info(c)),
                new UiNode(UiKind.StackItem) { Key = "antibodies" }.With(Antibodies(c)));

        UiNode buttons = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Button) { Key = "emptyEject", Icon = "times", Text = "Empty and Eject", Color = "bad", Disabled = cantEmpty, OnClick = () => c.Act("empty_eject_beaker") },
            new UiNode(UiKind.Button) { Key = "empty", Icon = "trash", Text = "Empty", Disabled = cantEmpty, OnClick = () => c.Act("empty_beaker") },
            new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject", Disabled = !hasBeaker, OnClick = () => c.Act("eject_beaker") });
        return new UiNode(UiKind.Section) { Key = "beakerSection", Title = "Beaker", Buttons = buttons }.With(content);
    }

    // tg: Pandemic/Beaker.tsx:72  Info
    private static UiNode Info(TguiContext c)
    {
        UiValue beaker = c.Data.Get("beaker"), blood = c.Data.Get("blood");
        if (beaker == null || beaker.Kind == UiValueKind.Null || blood == null || blood.Kind == UiValueKind.Null)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No beaker loaded" };
        double capacity = beaker.Num("capacity");
        return new UiNode(UiKind.Stack) { Key = "info" }.With(
            new UiNode(UiKind.StackItem) { Key = "blood", Grow = 2 }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                Item("dna", "DNA", CapitalizeFirst(blood.Str("dna"))),
                Item("type", "Type", CapitalizeFirst(blood.Str("type"))))),
            new UiNode(UiKind.StackItem) { Key = "container", Grow = 2 }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "container", Label = "Container" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "volume",
                    Color = "darkred",
                    Value = beaker.Num("volume"),
                    MinValue = 0,
                    MaxValue = capacity,
                    Ranges = new[] { ("good", capacity * 0.85, capacity), ("average", capacity * 0.25, capacity * 0.85), ("bad", 0.0, capacity * 0.25) },
                }))));
    }

    // tg: Pandemic/Beaker.tsx:115  Antibodies
    private static UiNode Antibodies(TguiContext c)
    {
        bool isReady = c.Data.Truthy("is_ready");
        IReadOnlyList<UiValue> resistances = c.Data.Arr("resistances");
        UiNode item = new UiNode(UiKind.LabeledItem) { Key = "antibodies", Label = "Antibodies" };
        if (resistances.Count == 0)
            item.Text = "None";
        else
            foreach (UiValue resistance in resistances)
            {
                string id = resistance.Str("id");
                item.With(new UiNode(UiKind.Button)
                {
                    Key = "vaccine:" + resistance.Str("name") + ":" + id,
                    Icon = "eye-dropper",
                    Disabled = !isReady,
                    Tooltip = "Creates a vaccine bottle.",
                    Text = resistance.Str("name"),
                    OnClick = () => c.Act("create_vaccine_bottle", ("index", id)),
                });
            }
        return new UiNode(UiKind.LabeledList) { Key = "list" }.With(item);
    }

    // tg: Pandemic/Specimen.tsx:9  SpecimenDisplay
    private static UiNode SpecimenDisplay(TguiContext c)
    {
        bool isReady = c.Data.Truthy("is_ready");
        IReadOnlyList<UiValue> viruses = c.Data.Arr("viruses");
        int tab = c.UseState("pandemic:tab", 0);
        UiValue virus = tab >= 0 && tab < viruses.Count ? viruses[tab] : null;

        UiNode buttons = new UiNode(UiKind.Stack) { Key = "buttons" };
        if (viruses.Count > 1)
        {
            UiNode tabs = new UiNode(UiKind.Tabs) { Key = "tabs" };
            for (int i = 0; i < viruses.Count; i++)
            {
                int index = i;
                tabs.With(new UiNode(UiKind.Tab) { Key = "tab:" + i, Selected = tab == i, Text = viruses[i].Str("name"), OnClick = () => c.SetState("pandemic:tab", index) });
            }
            buttons.With(new UiNode(UiKind.StackItem) { Key = "tabs" }.With(tabs));
        }
        double virusIndex = virus.Num("index");
        buttons.With(new UiNode(UiKind.StackItem) { Key = "cultureItem" }.With(new UiNode(UiKind.Button)
        {
            Key = "culture",
            Icon = "flask",
            Disabled = !isReady || virus == null,
            Tooltip = virus != null ? null : "No virus culture found.",
            Text = "Create Culture Bottle",
            OnClick = () => c.Act("create_culture_bottle", ("index", virusIndex)),
        }));

        UiNode section = new UiNode(UiKind.Section) { Key = "specimen", Fill = true, Scrollable = true, Title = "Specimen", Buttons = buttons };
        if (virus == null)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "nothing", Color = "good", Text = "Nothing detected." });
        UiNode body = new UiNode(UiKind.Stack) { Key = "virus", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "virus" }.With(VirusDisplay(c, virus)));
        UiValue symptoms = virus.Get("symptoms");
        if (symptoms != null && symptoms.Kind == UiValueKind.Array)
            body.With(new UiNode(UiKind.StackItem) { Key = "symptoms" }.With(SymptomDisplay(symptoms.Items)));
        return section.With(body);
    }

    // tg: Pandemic/Virus.tsx:18  VirusDisplay
    private static UiNode VirusDisplay(TguiContext c, UiValue virus)
    {
        UiNode stack = new UiNode(UiKind.Stack) { Key = "virusDisplay", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 3 }.With(VirusInfo(c, virus)));
        if (virus.Truthy("is_adv"))
            stack.With(
                new UiNode(UiKind.Divider) { Key = "div" },
                new UiNode(UiKind.StackItem) { Key = "traits", Grow = 1 }.With(VirusTraits(virus)));
        return stack;
    }

    // tg: Pandemic/Virus.tsx:41  Info
    private static UiNode VirusInfo(TguiContext c, UiValue virus)
    {
        string name = virus.Str("name");
        double index = virus.Num("index");
        UiNode nameNode = virus.Truthy("can_rename")
            ? new UiNode(UiKind.Input)
            {
                Key = "rename:" + index,
                TextValue = name == "Unknown" ? "" : name,
                OnCommit = value => c.Act("rename_disease", ("index", index), ("name", value)),
            }
            : new UiNode(UiKind.Box) { Key = "name", Color = "bad", Text = TguiFormat.DecodeHtmlEntities(name) };
        return new UiNode(UiKind.LabeledList) { Key = "info" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name" }.With(nameNode),
            Item("desc", "Description", virus.Str("description")),
            Item("agent", "Agent", CapitalizeFirst(virus.Str("agent"))),
            Item("spread", "Spread", virus.Str("spread")));
    }

    // tg: Pandemic/Virus.tsx:79  Traits
    private static UiNode VirusTraits(UiValue virus)
    {
        double resistance = virus.Num("resistance"), stageSpeed = virus.Num("stage_speed"), stealth = virus.Num("stealth");
        double transmission = virus.Num("transmission"), severity = virus.Num("severity");
        return new UiNode(UiKind.Section) { Key = "statistics", Title = "Statistics" }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            Item("resistance", "Resistance", TguiFormat.JsNumber(resistance), GetColor(resistance), "Protection from cures and natural recovery."),
            Item("stage_speed", "Stage speed", TguiFormat.JsNumber(stageSpeed), GetColor(stageSpeed), "Symptomic progression."),
            Item("stealth", "Stealth", TguiFormat.JsNumber(stealth), GetColor(stealth), "Detection difficulty from medical equipment."),
            Item("transmission", "Transmissibility", TguiFormat.JsNumber(transmission), GetColor(transmission), "Decides the spread type."),
            Item("severity", "Severity", TguiFormat.JsNumber(severity), GetColor(severity), "Overall danger posed by the disease.")));
    }

    // tg: Pandemic/Symptom.tsx:17  SymptomDisplay
    private static UiNode SymptomDisplay(IReadOnlyList<UiValue> symptoms)
    {
        if (symptoms.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "nosymptoms", Text = "No symptoms detected." };
        UiNode section = new UiNode(UiKind.Section) { Key = "symptoms", Fill = true, Title = "Symptoms" };
        foreach (UiValue symptom in symptoms)
        {
            string name = symptom.Str("name");
            section.With(new UiNode(UiKind.Collapsible) { Key = "symptom:" + name, Title = name }.With(
                new UiNode(UiKind.Stack) { Key = "body", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "desc", Grow = 3 }.With(
                        new UiNode(UiKind.Box) { Key = "desc", Text = symptom.Str("desc") },
                        Thresholds(symptom)),
                    new UiNode(UiKind.Divider) { Key = "div" },
                    new UiNode(UiKind.StackItem) { Key = "traits", Grow = 1 }.With(SymptomTraits(symptom)))));
        }
        return section;
    }

    // tg: Pandemic/Symptom.tsx:48  Thresholds (Object.entries of the threshold_desc assoc list)
    private static UiNode Thresholds(UiValue symptom)
    {
        UiValue raw = symptom.Get("threshold_desc");
        List<KeyValuePair<string, UiValue>> thresholds = raw?.Kind switch
        {
            UiValueKind.Object => raw.Fields.ToList(),
            // an empty DM list serializes as a JSON array: Object.entries([]) is empty
            UiValueKind.Array => raw.Items.Select((v, i) => new KeyValuePair<string, UiValue>(i.ToString(), v)).ToList(),
            _ => new List<KeyValuePair<string, UiValue>>(),
        };
        UiNode section = new UiNode(UiKind.Section) { Key = "thresholds", Title = "Thresholds" };
        if (thresholds.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Text = "None" });
        UiNode list = new UiNode(UiKind.LabeledList) { Key = "list" };
        int row = 0;
        foreach (KeyValuePair<string, UiValue> threshold in thresholds)
            list.With(Item("t:" + row++, threshold.Key, threshold.Value.AsStr()));
        return section.With(list);
    }

    // tg: Pandemic/Symptom.tsx:74  Traits
    private static UiNode SymptomTraits(UiValue symptom)
    {
        double level = symptom.Num("level"), resistance = symptom.Num("resistance"), stageSpeed = symptom.Num("stage_speed");
        double stealth = symptom.Num("stealth"), transmission = symptom.Num("transmission");
        return new UiNode(UiKind.Section) { Key = "modifiers", Title = "Modifiers" }.With(new UiNode(UiKind.LabeledList) { Key = "list" }.With(
            Item("level", "Level", TguiFormat.JsNumber(level), GetColor(level), "Rarity of the symptom."),
            Item("resistance", "Resistance", TguiFormat.JsNumber(resistance), GetColor(resistance), "Protection from cures and natural recovery."),
            Item("stage_speed", "Stage Speed", TguiFormat.JsNumber(stageSpeed), GetColor(stageSpeed), "Symptomic progression."),
            Item("stealth", "Stealth", TguiFormat.JsNumber(stealth), GetColor(stealth), "Detection difficulty from medical equipment."),
            Item("transmission", "Transmission", TguiFormat.JsNumber(transmission), GetColor(transmission), "Decides the spread type."),
            Item("cure", "Cure", symptom.Str("symptom_cure"), symptom.Str("cure_color", null), "What reagent remedies this symptom.")));
    }
}
