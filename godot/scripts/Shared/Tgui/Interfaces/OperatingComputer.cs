using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OperatingComputer/ (index.tsx, PatientStateView.tsx,
// SurgeryProceduresView.tsx, ExperimentView.tsx, helpers.ts, types.ts; gaps P1, D298). The window reads tg's ui_data and ui_static_data
// of the operating computer (operating_computer.dm:162, :199) and sends change_zone and open_experiments. Leaves: the catalog's fuzzy
// search is a case-insensitive substring of each operation's name and tool (as the ore silo's, tgui-core's useFuzzySearch is not at
// hand), the tooltips are the kit's text tooltips (their lines joined) and Blink draws its icon steadily. The experiments tab draws
// ExperimentConfigure's TechwebServer and Experiment (gaps RD3). The theme's DeForest logo behind the content is not drawn.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class OperatingComputer
{
    // tg: types.ts:64  enum ComputerTabs
    private const int PatientState = 1, OperationCatalog = 2, Experiments = 3;

    private const string TabKey = "tab", QueryKey = "query", PinnedKey = "pinned_operation", FilterKey = "filter_by_tool",
        SortKey = "catalog_sort_type", RoboticKey = "catalog_filter";

    // tg: types.ts:46  damageTypes
    private static readonly (string Label, string Type)[] DamageTypes =
    {
        ("Brute", "bruteLoss"),
        ("Burn", "fireLoss"),
        ("Toxin", "toxLoss"),
        ("Respiratory", "oxyLoss"),
    };

    // tg: index.tsx:16
    public static UiNode Render(TguiContext c)
    {
        int tab = c.UseState(TabKey, PatientState);
        IReadOnlyList<UiValue> surgeries = c.Data.Arr("surgeries");
        IReadOnlyList<UiValue> techwebs = c.Data.Arr("techwebs");
        string query = c.UseState(QueryKey, "");
        // tg: index.tsx:21  useFuzzySearch({ searchArray: surgeries, getSearchString: (item) => `${item.name} ${item.tool_rec}` })
        IReadOnlyList<UiValue> results = query.Length == 0 ? Array.Empty<UiValue>()
            : surgeries.Where(s => (s.Str("name") + " " + s.Str("tool_rec")).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        string[] pinned = c.UseState(PinnedKey, Array.Empty<string>());

        UiNode Tab(int index, string text) => new UiNode(UiKind.Tab) { Key = "tab" + index, Text = text, Selected = tab == index, OnClick = () => c.SetState(TabKey, index) };
        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true }.With(Tab(PatientState, "Patient State"), Tab(OperationCatalog, "Operation Catalog"));
        if (techwebs.Count > 0)
            tabs.With(Tab(Experiments, "Experiments"));

        UiNode body = tab switch
        {
            PatientState => PatientStateView(c, text => c.SetState(QueryKey, text), pinned),
            OperationCatalog => SurgeryProceduresView(c, results, query, pinned),
            _ => ExperimentView(c),
        };

        // tg: index.tsx:33  <Window width={tab === ComputerTabs.PatientState ? 350 : 430} height={610} theme="operating_computer">
        c.WindowTheme = "operating_computer";
        return c.Window(tab == PatientState ? 350 : 430, 610, null,
            new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(tabs),
                new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(body),
                new UiNode(UiKind.StackItem) { Key = "footer", TextAlign = "right", Color = "label", FontSize = "0.7em" }.With(
                    new UiNode(UiKind.Section) { Key = "copyright" }.With(
                        new UiNode(UiKind.Box) { Key = "text", Text = "DefOS 1.0 © Nanotrasen-Deforest Corporation. All rights reserved.", Color = "label", FontSize = "0.7em", TextAlign = "right" }))));
    }

    private static void SetPinned(TguiContext c, string[] pinned, string name) =>
        c.SetState(PinnedKey, pinned.Contains(name) ? pinned.Where(op => op != name).ToArray() : pinned.Append(name).ToArray());

    // ---- helpers.ts ----

    // tgui-core's capitalizeFirst
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // tg: helpers.ts:4  extractSurgeryName
    public static (string Name, string Tool, string TrueName) ExtractSurgeryName(UiValue operation, bool catalog)
    {
        // operation names may be "make incision", "lobotomy", or "disarticulation (amputation)"
        string name = operation.Str("name"), toolRec = operation.Str("tool_rec");
        if (string.IsNullOrEmpty(name))
            return ("Error Surgery", "Error", null);
        int parenthesis = name.IndexOf('(', StringComparison.Ordinal);
        if (parenthesis == -1)
            return (CapitalizeFirst(name), toolRec, null);
        int close = name.IndexOf(')', StringComparison.Ordinal);
        string inParenthesis = (close > parenthesis ? name[(parenthesis + 1)..close] : name[(parenthesis + 1)..]).Trim();
        if (!catalog)
            return (CapitalizeFirst(inParenthesis), toolRec, null);
        string outParenthesis = CapitalizeFirst(name[..parenthesis].Trim());
        return (CapitalizeFirst(outParenthesis), toolRec, CapitalizeFirst(inParenthesis));
    }

    // tg: helpers.ts:33  extractRequirementMap
    public static List<(string Title, List<string> Contents)> ExtractRequirementMap(UiValue surgery)
    {
        IReadOnlyList<UiValue> requirements = surgery.Arr("requirements");
        if (requirements.Count != 4)
            return new List<(string, List<string>)>(); // we can assert this never happens, but just in case...
        List<string> Strings(UiValue list) => list.Kind == UiValueKind.Array ? list.Items.Select(v => v.AsStr()).ToList() : new List<string>();
        List<string> hardRequirements = Strings(requirements[0]);
        List<string> softRequirements = Strings(requirements[1]);
        List<string> optionalRequirements = Strings(requirements[2]);
        List<string> blockedRequirements = Strings(requirements[3]);

        // you *have* to have one of the soft requirements, so if there's only one...
        if (softRequirements.Count == 1)
        {
            hardRequirements.Add(softRequirements[0]);
            softRequirements.Clear();
        }

        string OptionalTitle()
        {
            if (optionalRequirements.Count == 1)
                return hardRequirements.Count == 0 ? "The following is optional:" : "Additionally, the following is optional:";
            return hardRequirements.Count == 0 ? "All of the following are optional:" : "Additionally, all of the following are optional:";
        }

        string BlockedTitle()
        {
            if (blockedRequirements.Count == 1)
                return hardRequirements.Count == 0 ? "The following would block the procedure:" : "However, the following would block the procedure:";
            return hardRequirements.Count == 0 ? "Any of the following would block the procedure:" : "However, any of the following would block the procedure:";
        }

        return new List<(string, List<string>)>
        {
            ((hardRequirements.Count == 1 ? "The" : "All of the") + " following are required:", hardRequirements),
            ("Additionally, one of the following is required:", softRequirements),
            (OptionalTitle(), optionalRequirements),
            (BlockedTitle(), blockedRequirements),
        };
    }

    // ---- PatientStateView.tsx ----

    // tg: PatientStateView.tsx:30
    private static UiNode PatientStateView(TguiContext c, Action<string> setSearchText, string[] pinned)
    {
        if (!c.Data.Truthy("has_table"))
            return new UiNode(UiKind.Section) { Key = "patient_state", Fill = true }.With(
                new UiNode(UiKind.NoticeBox) { Key = "notable", Color = "yellow", TextAlign = "center", Text = "No table detected" });
        UiValue patient = c.Data.Field("patient");
        if (patient == null || patient.Kind != UiValueKind.Object)
            return new UiNode(UiKind.Section) { Key = "patient_state", Fill = true }.With(
                new UiNode(UiKind.NoticeBox) { Key = "nopatient", Color = "red", TextAlign = "center", Text = "No patient detected" });
        return new UiNode(UiKind.Section) { Key = "patient_state", Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "column", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "main" }.With(PatientStateMainStateView(patient)),
                new UiNode(UiKind.StackItem) { Key = "surgery" }.With(PatientStateSurgeryStateView(c, patient, c.Data.Str("target_zone"))),
                new UiNode(UiKind.StackItem) { Key = "next", Grow = 1 }.With(PatientStateNextOperationsView(c, pinned, setSearchText))));
    }

    // tg: PatientStateView.tsx:83
    private static UiNode PatientStateMainStateView(UiValue patient)
    {
        double health = patient.Num("health"), maxHealth = patient.Num("maxHealth");
        double bloodLevel = patient.Num("blood_level"), standardBlood = patient.Num("standard_blood_level");
        var list = new UiNode(UiKind.LabeledList) { Key = "vitals" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "row:state", Label = "State", Color = patient.Str("statstate"), Text = patient.Str("stat") },
            new UiNode(UiKind.LabeledItem) { Key = "row:blood_type", Label = "Blood Type", Text = patient.Str("blood_type") is { Length: > 0 } bt ? bt : "Unable to determine blood type" },
            new UiNode(UiKind.LabeledItem) { Key = "row:health", Label = "Health" }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "health",
                    Value = health,
                    MinValue = patient.Num("minHealth"),
                    MaxValue = maxHealth,
                    Color = health >= 0 ? "good" : "average",
                    Text = Math.Round(health, MidpointRounding.AwayFromZero) + "%",
                }),
            new UiNode(UiKind.LabeledItem) { Key = "row:blood", Label = "Blood Level" }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "blood",
                    Value = bloodLevel,
                    MinValue = 0,
                    MaxValue = standardBlood,
                    Color = bloodLevel >= standardBlood * 0.7 ? "good" : bloodLevel >= standardBlood * 0.45 ? "average" : "bad",
                    Text = (standardBlood > 0 ? Math.Round(bloodLevel / standardBlood * 100, MidpointRounding.AwayFromZero) : 0) + "%",
                }));
        foreach ((string label, string type) in DamageTypes)
        {
            list.With(new UiNode(UiKind.LabeledItem) { Key = "row:" + type, Label = label }.With(
                new UiNode(UiKind.ProgressBar)
                {
                    Key = type,
                    Value = maxHealth != 0 ? patient.Num(type) / maxHealth : 0,
                    Color = "bad",
                    Text = TguiFormat.JsNumber(TguiFormat.Round(patient.Num(type), 0)), // AnimatedNumber's settled value
                }));
        }
        return list;
    }

    // tg: PatientStateView.tsx:143
    private static UiNode PatientStateSurgeryStateView(TguiContext c, UiValue patient, string targetZone)
    {
        var states = new UiNode(UiKind.Stack) { Key = "states", Vertical = true };
        foreach (UiValue state in patient.Arr("surgery_state"))
            states.With(new UiNode(UiKind.StackItem) { Key = "s:" + state.AsStr(), Text = "- " + state.AsStr() });
        return new UiNode(UiKind.Stack) { Key = "surgery" }.With(
            new UiNode(UiKind.StackItem) { Key = "zones" }.With(
                BodyZoneSelector.Render(c, "zone_selector", targetZone, zone =>
                {
                    if (zone != targetZone)
                        c.Act("change_zone", ("new_zone", zone));
                }, theme: "slimecore")),
            new UiNode(UiKind.StackItem) { Key = "list" }.With(states));
    }

    // tg: PatientStateView.tsx:181
    private static UiNode PatientStateNextOperationsView(TguiContext c, string[] pinned, Action<string> setSearchText)
    {
        List<UiValue> possibleNextOperations = c.Data.Arr("surgeries").Where(operation => operation.Truthy("show_as_next")).ToList();

        List<string> allTools = new List<string> { "all tools" }.Concat(possibleNextOperations
            .Select(operation => operation.Str("tool_rec"))
            .SelectMany(tool => tool.Split(" / "))
            .Distinct()).ToList();

        string filterByTool = c.UseState(FilterKey, allTools[0]);

        // tg: :201  the pinned operations first (a stable sort), then :222 the priority ones first
        if (pinned.Length > 0)
            possibleNextOperations = possibleNextOperations.OrderBy(o => pinned.Contains(o.Str("name")) ? 0 : 1).ToList();
        possibleNextOperations = possibleNextOperations.OrderBy(o => o.Truthy("priority") ? 0 : 1).ToList();

        var list = new UiNode(UiKind.Stack) { Key = "operations", Vertical = true, Fill = true };
        if (possibleNextOperations.Count == 0)
            list.With(new UiNode(UiKind.StackItem) { Key = "none" }.With(
                new UiNode(UiKind.NoticeBox) { Key = "none", Color = "green", TextAlign = "center", Text = "No operations available" }));
        else
        {
            foreach (UiValue operation in possibleNextOperations.Where(operation => filterByTool == allTools[0] || operation.Str("tool_rec").Contains(filterByTool, StringComparison.Ordinal)))
            {
                (string name, string tool, _) = ExtractSurgeryName(operation, false);
                string opName = operation.Str("name");
                bool isPinned = pinned.Contains(opName);
                bool priority = operation.Truthy("priority");
                var tooltip = new List<string>();
                if (priority)
                    tooltip.Add("! Recommended next step");
                if (isPinned)
                    tooltip.Add("Pinned");
                tooltip.Add(operation.Str("desc"));
                tooltip.Add($"Left click {(isPinned ? "unpins operation from" : "pins operation to")} the top.");
                if (operation.Truthy("show_in_list"))
                    tooltip.Add("Right click opens operation info.");
                var row = new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "name", Text = name });
                if (priority)
                    row.With(new UiNode(UiKind.StackItem) { Key = "priority" }.With(new UiNode(UiKind.Icon) { Key = "blink", Icon = "exclamation" }));
                if (isPinned)
                    row.With(new UiNode(UiKind.StackItem) { Key = "pin" }.With(new UiNode(UiKind.Icon) { Key = "pin", Icon = "thumbtack" }));
                row.With(
                    new UiNode(UiKind.StackItem) { Key = "grow", Grow = 1 },
                    new UiNode(UiKind.StackItem) { Key = "tool", Text = TguiFormat.CapitalizeAll(tool), FontSize = "0.9rem" });
                list.With(new UiNode(UiKind.StackItem) { Key = "next:" + opName }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "op:" + opName,
                        Fluid = true,
                        Tooltip = string.Join("\n", tooltip),
                        Color = priority ? "caution" : isPinned ? "danger" : null,
                        OnClickWith = click =>
                        {
                            if (click.Right)
                            {
                                // tg: :259  onContextMenu
                                if (operation.Truthy("show_in_list"))
                                {
                                    c.SetState(TabKey, OperationCatalog);
                                    setSearchText(opName);
                                }
                                return;
                            }
                            SetPinned(c, pinned, opName);
                        },
                    }.With(row)));
            }
        }

        return new UiNode(UiKind.Section)
        {
            Key = "possible",
            Title = "Possible Operations",
            Scrollable = true,
            Fill = true,
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "filter",
                Icon = "filter",
                Tooltip = "Filter by recommended tool. Right click to reset.",
                Width = "100px",
                Selected = filterByTool != allTools[0],
                Text = CapitalizeFirst(filterByTool),
                OnClickWith = click => c.SetState(FilterKey, click.Right ? allTools[0] : allTools[(allTools.IndexOf(filterByTool) + 1) % allTools.Count]),
            },
        }.With(list);
    }

    // ---- SurgeryProceduresView.tsx ----

    // tg: SurgeryProceduresView.tsx:19  SurgeryRequirementsInner
    private static UiNode SurgeryRequirementsInner(int index, string catText, List<string> catContents)
    {
        var contents = new UiNode(UiKind.Stack) { Key = "contents", Vertical = true };
        for (int i = 0; i < catContents.Count; i++)
            contents.With(new UiNode(UiKind.StackItem) { Key = "req" + i, Text = "- " + CapitalizeFirst(catContents[i]) });
        return new UiNode(UiKind.StackItem) { Key = "cat" + index }.With(
            new UiNode(UiKind.Box) { Key = "title", Text = catText, Color = "label" },
            contents);
    }

    // tg: SurgeryProceduresView.tsx:51
    private static UiNode SurgeryProceduresView(TguiContext c, IReadOnlyList<UiValue> searchedSurgeries, string searchText, string[] pinned)
    {
        IReadOnlyList<UiValue> surgeries = c.Data.Arr("surgeries");
        string sortType = c.UseState(SortKey, "default");
        bool filterRobotic = c.UseState(RoboticKey, false);
        IReadOnlyList<UiValue> rawSurgeryList = searchedSurgeries.Count > 0 ? searchedSurgeries : surgeries;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<UiValue> surgeryList = rawSurgeryList
            .Where(surgery => surgery.Truthy("show_in_list"))
            .Where(surgery => !filterRobotic || !surgery.Truthy("mechanic"))
            .Where(surgery => seen.Add(surgery.Str("name")))
            .ToList();

        if (sortType == "name")
            surgeryList = surgeryList.OrderBy(s => s.Str("name"), StringComparer.Ordinal).ToList();
        else if (sortType == "tool")
            surgeryList = surgeryList.OrderBy(s => s.Str("tool_rec"), StringComparer.Ordinal).ToList();

        if (pinned.Length > 0)
            surgeryList = surgeryList.OrderBy(s => pinned.Contains(s.Str("name")) ? 0 : 1).ToList();

        var section = new UiNode(UiKind.Section)
        {
            Key = "catalog",
            Title = " ",
            Scrollable = true,
            Fill = true,
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Width = "215px",
                    Tooltip = "Search...",
                    TextValue = searchText,
                    OnInput = value => c.SetState(QueryKey, value),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "robotic",
                    Icon = "filter",
                    Tooltip = "Filter out robotic surgeries.",
                    Selected = filterRobotic,
                    Text = "Hide Mechanic",
                    OnClick = () => c.SetState(RoboticKey, !filterRobotic),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "sort",
                    Width = "75px",
                    Icon = "sort",
                    Tooltip = "Cycle between sorting methods.",
                    Text = CapitalizeFirst(sortType),
                    OnClick = () => c.SetState(SortKey, sortType == "default" ? "name" : sortType == "name" ? "tool" : "default"),
                }),
        };

        foreach (UiValue surgery in surgeryList)
        {
            (string name, string tool, string trueName) = ExtractSurgeryName(surgery, true);
            string opName = surgery.Str("name");
            bool isPinned = pinned.Contains(opName);
            var head = new UiNode(UiKind.Stack) { Key = "head", Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "title" }.With(
                    new UiNode(UiKind.Stack) { Key = "title" }.With(
                        new UiNode(UiKind.StackItem) { Key = "pin" }.With(
                            new UiNode(UiKind.Button) { Key = "pin", Icon = "thumbtack", Color = isPinned ? "danger" : null, OnClick = () => SetPinned(c, pinned, opName) }),
                        new UiNode(UiKind.StackItem) { Key = "name", Text = name, Bold = true, FontSize = "1.2rem" })));
            if (!string.IsNullOrEmpty(trueName))
                head.With(new UiNode(UiKind.StackItem) { Key = "true_name", Text = $"({trueName})", FontSize = "0.9rem" });
            head.With(
                new UiNode(UiKind.StackItem) { Key = "grow", Grow = 1 },
                new UiNode(UiKind.StackItem) { Key = "tool", Text = TguiFormat.CapitalizeAll(tool), TextAlign = "right", FontSize = "0.9rem" });

            var requirements = new UiNode(UiKind.Stack) { Key = "requirements", Vertical = true };
            List<(string Title, List<string> Contents)> map = ExtractRequirementMap(surgery);
            for (int i = 0; i < map.Count; i++)
            {
                if (map[i].Contents.Count > 0)
                    requirements.With(SurgeryRequirementsInner(i, map[i].Title, map[i].Contents));
            }

            section.With(new UiNode(UiKind.Stack) { Key = "op:" + opName, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "head" }.With(head),
                new UiNode(UiKind.StackItem) { Key = "body" }.With(
                    new UiNode(UiKind.Box) { Key = "body", Color = "label" }.With(
                        new UiNode(UiKind.Stack) { Key = "body", Vertical = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "desc", Text = surgery.Str("desc"), Bold = true, Wrap = true },
                            new UiNode(UiKind.StackItem) { Key = "requirements" }.With(
                                new UiNode(UiKind.Collapsible) { Key = "req:" + opName, Title = "Requirements", Open = isPinned }.With(requirements)))))));
        }
        return section;
    }

    // ---- ExperimentView.tsx ----

    // tg: ExperimentView.tsx:6
    private static UiNode ExperimentView(TguiContext c)
    {
        IReadOnlyList<UiValue> techwebs = c.Data.Arr("techwebs");
        IReadOnlyList<UiValue> experiments = c.Data.Arr("experiments");
        // tg: ExperimentView.tsx:20  <TechwebServer techwebs={techwebs} can_select={false} />
        UiNode servers = ExperimentConfigure.TechwebServer(c, techwebs, canSelect: false);
        var column = new UiNode(UiKind.Stack) { Key = "experiments", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "servers" }.With(
                new UiNode(UiKind.Section)
                {
                    Key = "servers",
                    Title = "Servers",
                    Fill = true,
                    Buttons = new UiNode(UiKind.Button) { Key = "open", Text = "Open Config", OnClick = () => c.Act("open_experiments") },
                }.With(servers)));
        if (techwebs.Any(e => e.Truthy("selected")))
        {
            var list = new UiNode(UiKind.Section) { Key = "list", Title = "Experiments", Scrollable = true, Fill = true };
            if (experiments.Count > 0)
            {
                // tg: ExperimentView.tsx:29  experiments.sort((a, b) => (a.name > b.name ? 1 : -1)).map((exp, i) => <Experiment key={i} exp={exp} can_select={false} />)
                foreach (UiValue exp in experiments.OrderBy(e => e.Str("name"), StringComparer.Ordinal))
                    list.With(ExperimentConfigure.Experiment(c, exp, null, canSelect: false));
            }
            else
                list.With(new UiNode(UiKind.NoticeBox) { Key = "none", Color = "yellow", Text = "No experiments found!" });
            column.With(new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(list));
        }
        return column;
    }
}
