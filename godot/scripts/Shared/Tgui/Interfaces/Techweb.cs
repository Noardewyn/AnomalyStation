using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Techweb/ (index, Start, Content, Router, Overview,
// helpers, nodes/TechNode, nodes/detail, disks/DiskMenu, disks/disks, LockedExperiment) and NtosTechweb.tsx (gaps RD2, D333: the R&D
// console and the Nanotrasen Science Hub app). The header with the security protocols, the points and their rate, the queue, the lock
// and the inserted disks; the researched, available and future nodes and the search; a node's costs, prerequisites, experiments,
// designs and its research, queue and details buttons; a node's detail with its required and unlocked nodes; the disks' stored data
// with their upload, download and eject. The static data come compressed (rdconsole.dm compress_id) and are expanded here as tg's
// useRemappedBackend does. The route, tab and search are the window's state (tg: useState). A design's picture is a class of tg's
// "design" sheet (TguiSpritesheets.Design32, DV44) and its tooltip is text (the kit's tooltips are). An experiment is the
// ExperimentConfigure window's panel. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Techweb
{
    private const int TabResearched = 0;
    private const int TabAvailable = 1;
    private const int TabFuture = 2;

    private static readonly IReadOnlyList<(string Color, double From, double To)> ProgressRanges = new[]
    {
        ("good", 0.5, double.PositiveInfinity),
        ("average", 0.25, 0.5),
        ("bad", double.NegativeInfinity, 0.25),
    };

    // tg: Techweb/index.tsx:4  Techweb
    public static UiNode Render(TguiContext c) => c.Window(640, 735, null, Start(c));

    // tg: Techweb/index.tsx:16  AppTechweb (NtosTechweb.tsx:3)
    public static UiNode RenderNtos(TguiContext c) => NtosWindow.Render(c, 640, 735, null, Start(c));

    // tg: Techweb/Start.tsx:9  TechwebStart
    private static UiNode Start(TguiContext c)
    {
        UiValue data = c.Data;
        if (data.Truthy("locked"))
        {
            return new UiNode(UiKind.Modal) { Key = "locked", Width = "15em", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "title", Bold = true, Text = "Console Locked" },
                new UiNode(UiKind.Button) { Key = "unlock", Icon = "unlock", Text = "Unlock", OnClick = () => c.Act("toggleLock") });
        }

        if (!data.Truthy("stored_research"))
        {
            return new UiNode(UiKind.Modal) { Key = "noWeb", Width = "25em", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "title", Bold = true, Text = "No research techweb found, please synchronize the console." });
        }

        return Content(c, new Remapped(data));
    }

    /// <summary>tg: Techweb/helpers.ts:28-91  the static data expanded from their compressed ids.</summary>
    private sealed class Remapped
    {
        public sealed class Node
        {
            public string Name;
            public string Description;
            public List<(string Type, double Value)> Costs = new();
            public List<string> PrerequisiteNodes = new();
            public List<string> UnlockedDesigns = new();
            public List<string> UnlockedNodes = new();
            public List<string> RequiredExperiments = new();
            public List<KeyValuePair<string, UiValue>> DiscountExperiments = new();
            public List<KeyValuePair<string, UiValue>> DiscountBoosts = new();
        }

        public sealed class Design
        {
            public string Name;
            public UiValue Cost;
            public double BuildTypes;
            public bool HasBuildTypes;
            public double DepartmentFlags;
            public string Class;
        }

        public readonly Dictionary<string, Node> NodeCache = new(StringComparer.Ordinal);
        public readonly Dictionary<string, Design> DesignCache = new(StringComparer.Ordinal);
        public readonly IReadOnlyList<KeyValuePair<string, UiValue>> BuildTypes;
        public readonly IReadOnlyList<KeyValuePair<string, UiValue>> DepartmentFlags;
        public readonly double SheetMaterialAmount;

        public Remapped(UiValue data)
        {
            UiValue staticData = data.Get("static_data");
            IReadOnlyList<UiValue> idCache = staticData.Arr("id_cache");
            string RemapId(string id) => int.TryParse(id, out int i) && i >= 1 && i <= idCache.Count ? idCache[i - 1].AsStr() : null;
            string RemapValue(UiValue v) => RemapId(v.AsStr());

            foreach (var (id, node) in staticData.Obj("node_cache"))
            {
                NodeCache[RemapId(id) ?? id] = new Node
                {
                    Name = node.Str("name"),
                    Description = node.Str("description"),
                    Costs = node.Obj("costs").Select(kv => (RemapId(kv.Key), kv.Value.AsNum())).ToList(),
                    PrerequisiteNodes = node.Arr("prerequisite_nodes").Select(RemapValue).ToList(),
                    UnlockedDesigns = node.Arr("unlocked_designs").Select(RemapValue).ToList(),
                    UnlockedNodes = node.Arr("unlocked_nodes").Select(RemapValue).ToList(),
                    RequiredExperiments = node.Arr("required_experiments").Select(v => v.AsStr()).ToList(),
                    DiscountExperiments = node.Obj("discount_experiments").ToList(),
                    DiscountBoosts = node.Obj("discount_boosts").ToList(),
                };
            }

            foreach (var (id, entry) in staticData.Obj("design_cache"))
            {
                IReadOnlyList<UiValue> fields = entry.Items;
                string classes = fields.Count > 4 ? fields[4].AsStr() : "";
                DesignCache[RemapId(id) ?? id] = new Design
                {
                    Name = fields.Count > 0 ? fields[0].AsStr() : "",
                    Cost = fields.Count > 1 ? fields[1] : null,
                    HasBuildTypes = fields.Count > 2 && !fields[2].IsNull,
                    BuildTypes = fields.Count > 2 ? fields[2].AsNum() : 0,
                    DepartmentFlags = fields.Count > 3 ? fields[3].AsNum() : 0,
                    // classes.startsWith('design') ? classes : `design32x32 ${classes}`: the sheet is TguiSpritesheets.Design32 (DV44)
                    Class = classes,
                };
            }

            SheetMaterialAmount = staticData.Num("SHEET_MATERIAL_AMOUNT", 100);
            BuildTypes = staticData.Obj("build_types");
            DepartmentFlags = staticData.Obj("department_flags");
        }
    }

    // tg: Techweb/Content.tsx:7  TechwebContent
    private static UiNode Content(TguiContext c, Remapped r)
    {
        UiValue data = c.Data;
        bool secProtocols = data.Truthy("sec_protocols");
        UiValue points = data.Get("points");
        UiValue pointsLastTick = data.Get("points_last_tick");
        UiValue abbreviations = data.Get("point_types_abbreviations");
        IReadOnlyList<KeyValuePair<string, UiValue>> queueNodes = data.Obj("queue_nodes");

        var list = new UiNode(UiKind.LabeledList) { Key = "header" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "security", Label = "Security" }.With(
                new UiNode(UiKind.Box) { Key = "protocols", Color = secProtocols ? "good" : "bad", Text = secProtocols ? "Engaged" : "Disengaged" }));
        foreach (var (type, amount) in points?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>())
        {
            double perTick = pointsLastTick.Num(type);
            list.With(new UiNode(UiKind.LabeledItem) { Key = "points:" + type, Label = abbreviations.Str(type) }.With(
                new UiNode(UiKind.Box) { Key = "amount", Bold = true, Text = amount.AsStr() },
                perTick != 0 ? new UiNode(UiKind.Box) { Key = "rate", Text = " (+" + TguiFormat.JsNumber(perTick) + "/sec)" } : null));
        }
        var queue = new UiNode(UiKind.LabeledItem) { Key = "queue", Label = "Queue" };
        if (queueNodes.Count == 0)
            queue.Text = "Empty";
        else
        {
            foreach (var (nodeId, queuer) in queueNodes)
            {
                queue.With(new UiNode(UiKind.Button)
                {
                    Key = "queued:" + nodeId,
                    Tooltip = "Added by: " + queuer.AsStr(),
                    Text = r.NodeCache.TryGetValue(nodeId, out Remapped.Node queued) ? queued.Name : nodeId,
                });
            }
        }
        list.With(queue);

        var side = new UiNode(UiKind.StackItem) { Key = "side" }.With(
            new UiNode(UiKind.Button) { Key = "lock", Fluid = true, Icon = "lock", Text = "Lock Console", OnClick = () => c.Act("toggleLock") });
        if (data.Truthy("d_disk"))
        {
            side.With(new UiNode(UiKind.Button)
            {
                Key = "designDisk",
                Fluid = true,
                Text = "Design Disk Inserted",
                OnClick = () => SetRoute(c, "disk", diskType: "design"),
            });
        }
        if (data.Truthy("t_disk"))
        {
            side.With(new UiNode(UiKind.Button)
            {
                Key = "techDisk",
                Fluid = true,
                Text = "Tech Disk Inserted",
                OnClick = () => SetRoute(c, "disk", diskType: "tech"),
            });
        }

        return new UiNode(UiKind.Stack) { Key = "viewport", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerSection" }.With(new UiNode(UiKind.Stack) { Key = "headerContent" }.With(
                new UiNode(UiKind.StackItem) { Key = "list" }.With(list),
                new UiNode(UiKind.StackItem) { Key = "spacer", Grow = 1 },
                side)),
            new UiNode(UiKind.StackItem) { Key = "router", Grow = 1 }.With(Router(c, r)));
    }

    private static void SetRoute(TguiContext c, string route, string diskType = null, string selectedNode = null)
    {
        c.SetState("techweb:route", route);
        c.SetState("techweb:diskType", diskType ?? "");
        c.SetState("techweb:selectedNode", selectedNode ?? "");
    }

    // tg: Techweb/Router.tsx:8  TechwebRouter
    private static UiNode Router(TguiContext c, Remapped r)
    {
        string route = c.UseState("techweb:route", "");
        if (route == "disk")
            return DiskMenu(c, r, c.UseState("techweb:diskType", ""));
        if (route == "details")
            return NodeDetail(c, r, c.UseState("techweb:selectedNode", ""));
        return Overview(c, r);
    }

    // tg: Techweb/Overview.tsx:15  TechwebOverview
    private static UiNode Overview(TguiContext c, Remapped r)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> nodes = data.Arr("nodes");
        int tabIndex = c.UseState("techweb:tab", TabAvailable);
        string searchText = c.UseState("techweb:search", "");

        // Only search when 3 or more characters have been input
        bool searching = !string.IsNullOrEmpty(searchText) && searchText.Trim().Length > 1;

        Remapped.Node Cached(UiValue n) => r.NodeCache.TryGetValue(n.Str("path"), out Remapped.Node node) ? node : null;

        IEnumerable<UiValue> displayedNodes;
        if (searching)
        {
            displayedNodes = nodes.Where(x =>
            {
                Remapped.Node n = Cached(x);
                return n != null && (n.Name.ToLowerInvariant().Contains(searchText, StringComparison.Ordinal)
                    || n.Description.ToLowerInvariant().Contains(searchText, StringComparison.Ordinal)
                    || n.UnlockedDesigns.Any(e => r.DesignCache.TryGetValue(e ?? "", out Remapped.Design d) && d.Name.ToLowerInvariant().Contains(searchText, StringComparison.Ordinal)));
            });
        }
        else
        {
            displayedNodes = (tabIndex < 2 ? nodes.Where(x => x.Num("tier") == tabIndex) : nodes.Where(x => x.Num("tier") >= tabIndex))
                .OrderBy(x => Cached(x)?.Name ?? "", StringComparer.Ordinal);
        }

        void SwitchTab(int tab)
        {
            c.SetState("techweb:tab", tab);
            c.SetState("techweb:search", "");
        }

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
            new UiNode(UiKind.Tab) { Key = "researched", Text = "Researched", Selected = !searching && tabIndex == TabResearched, OnClick = () => SwitchTab(TabResearched) },
            new UiNode(UiKind.Tab) { Key = "available", Text = "Available", Selected = !searching && tabIndex == TabAvailable, OnClick = () => SwitchTab(TabAvailable) },
            new UiNode(UiKind.Tab) { Key = "future", Text = "Future", Selected = !searching && tabIndex == TabFuture, OnClick = () => SwitchTab(TabFuture) },
            searching ? new UiNode(UiKind.Tab) { Key = "results", Text = "Search Results", Selected = true } : null);

        var list = new UiNode(UiKind.Section) { Key = "nodes", Fill = true, Scrollable = true };
        foreach (UiValue n in displayedNodes)
            list.With(TechNode(c, r, n));

        return new UiNode(UiKind.Stack) { Key = "overview", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerTabs" }.With(new UiNode(UiKind.Stack) { Key = "tabRow", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Align = "center" }.With(new UiNode(UiKind.Box) { Text = "Web View" }),
                new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(tabs),
                new UiNode(UiKind.StackItem) { Key = "search", Align = "center" }.With(new UiNode(UiKind.Input)
                {
                    Key = "searchInput",
                    Tooltip = "Search...",
                    TextValue = searchText,
                    OnInput = value => c.SetState("techweb:search", value ?? ""),
                }))),
            new UiNode(UiKind.StackItem) { Key = "overviewNodes", Grow = 1 }.With(list));
    }

    // tg: Techweb/nodes/TechNode.tsx:25  TechNode
    private static UiNode TechNode(TguiContext c, Remapped r, UiValue node, bool nodetails = false, bool nocontrols = false)
    {
        UiValue data = c.Data;
        UiValue experiments = data.Get("experiments");
        UiValue points = data.Get("points");
        IReadOnlyList<UiValue> nodes = data.Arr("nodes");
        UiValue abbreviations = data.Get("point_types_abbreviations");
        IReadOnlyList<KeyValuePair<string, UiValue>> queueNodes = data.Obj("queue_nodes");

        string path = node.Str("path");
        bool canUnlock = node.Truthy("can_unlock");
        bool haveExperimentsDone = node.Truthy("have_experiments_done");
        UiValue tierValue = node.Get("tier");
        bool hasTier = tierValue != null && !tierValue.IsNull;
        double tier = tierValue.AsNum(double.NaN);
        bool enqueuedByUser = node.Truthy("enqueued_by_user");
        bool isFree = node.Truthy("is_free");
        bool discountBoosted = node.Truthy("discount_boosted");

        if (!r.NodeCache.TryGetValue(path, out Remapped.Node cached))
            return null;

        bool ExperimentCompleted(string type) => experiments?.Get(type) is { } e && e.Truthy("completed");

        int expcompl = cached.RequiredExperiments.Count(ExperimentCompleted);
        int techcompl = cached.PrerequisiteNodes.Count(x => nodes.FirstOrDefault(y => y.Str("path") == x) is { } prereq && prereq.Num("tier", -1) == 0);

        // Notice that this logic will have to be changed if we make the discounts
        // pool-specific
        double nodeDiscountExperiments = cached.DiscountExperiments.Where(kv => ExperimentCompleted(kv.Key)).Sum(kv => kv.Value.AsNum());

        // Will need to be changed (along with some backend/DM code) if boosts should
        // ever vary by point type. As is, this simply adds up all discount boosts.
        double nodeDiscountBoosts = discountBoosted ? cached.DiscountBoosts.Sum(kv => kv.Value.AsNum()) : 0;

        bool queueEmpty = queueNodes.Count == 0;
        bool inQueue = queueNodes.Any(kv => kv.Key == path);

        UiNode buttons = null;
        if (!nocontrols)
        {
            buttons = new UiNode(UiKind.Fragment) { Key = "buttons" };
            if (hasTier && tier > 0)
            {
                if (canUnlock && (isFree || queueEmpty))
                {
                    buttons.With(new UiNode(UiKind.Button)
                    {
                        Key = "research",
                        Icon = "lightbulb",
                        Text = "Research",
                        Disabled = !canUnlock || tier > 1 || !queueEmpty,
                        OnClick = () => c.Act("researchNode", ("node_path", path)),
                    });
                }
                else if (enqueuedByUser)
                {
                    buttons.With(new UiNode(UiKind.Button)
                    {
                        Key = "dequeue",
                        Icon = "trash",
                        Color = "bad",
                        Text = "Dequeue",
                        OnClick = () => c.Act("dequeueNode", ("node_path", path)),
                    });
                }
                else if (inQueue && !enqueuedByUser)
                    buttons.With(new UiNode(UiKind.Button) { Key = "queued", Icon = "check", Color = "good", Text = "Queued" });
                else
                {
                    buttons.With(new UiNode(UiKind.Button)
                    {
                        Key = "enqueue",
                        Icon = "lightbulb",
                        Text = "Enqueue",
                        Disabled = !haveExperimentsDone || inQueue || techcompl < cached.PrerequisiteNodes.Count,
                        OnClick = () => c.Act("enqueueNode", ("node_path", path)),
                    });
                }
            }
            if (!nodetails)
            {
                buttons.With(new UiNode(UiKind.Button)
                {
                    Key = "details",
                    Icon = "tasks",
                    Text = "Details",
                    OnClick = () => SetRoute(c, "details", selectedNode: path),
                });
            }
        }

        var section = new UiNode(UiKind.Section) { Key = "node:" + path, Title = cached.Name, Buttons = buttons };

        if (!(hasTier && tier == 0))
        {
            var progress = new UiNode(UiKind.Stack) { Key = "progress" };
            foreach (var (type, value) in cached.Costs)
            {
                double reqPts = Math.Max(0, value - nodeDiscountExperiments - nodeDiscountBoosts);
                double have = points.Num(type ?? "");
                double nodeProg = Math.Min(reqPts, have);
                progress.With(new UiNode(UiKind.StackItem) { Key = "cost:" + type, Grow = 1 }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "bar",
                    Ranges = ProgressRanges,
                    Value = reqPts == 0 ? 1 : Math.Min(1, have / reqPts),
                    Text = abbreviations.Str(type ?? "") + " (" + TguiFormat.JsNumber(nodeProg) + "/" + TguiFormat.JsNumber(reqPts) + ")",
                }));
            }
            if (cached.PrerequisiteNodes.Count > 0)
            {
                progress.With(new UiNode(UiKind.StackItem) { Key = "tech", Grow = 1 }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "bar",
                    Ranges = ProgressRanges,
                    Value = (double)techcompl / cached.PrerequisiteNodes.Count,
                    Text = "Tech (" + techcompl + "/" + cached.PrerequisiteNodes.Count + ")",
                }));
            }
            if (cached.RequiredExperiments.Count > 0)
            {
                progress.With(new UiNode(UiKind.StackItem) { Key = "experiments", Grow = 1 }.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "bar",
                    Ranges = ProgressRanges,
                    Value = (double)expcompl / cached.RequiredExperiments.Count,
                    Text = "Experiments (" + expcompl + "/" + cached.RequiredExperiments.Count + ")",
                }));
            }
            section.With(progress);
        }

        section.With(new UiNode(UiKind.Box) { Key = "description", Text = cached.Description });

        var designs = new UiNode(UiKind.Box) { Key = "designs" };
        foreach (string design in cached.UnlockedDesigns)
        {
            if (design == null || !r.DesignCache.TryGetValue(design, out Remapped.Design d))
                continue;
            designs.With(new UiNode(UiKind.ImageButton)
            {
                Key = "design:" + design,
                DmIcon = TguiSpritesheets.Design32,
                DmIconState = d.Class,
                Tooltip = DesignTooltip(r, d),
            });
        }
        section.With(designs);

        if (cached.RequiredExperiments.Count > 0)
        {
            var required = new UiNode(UiKind.Collapsible) { Key = "requiredExperiments", Title = "Required Experiments" };
            int index = 0;
            foreach (string k in cached.RequiredExperiments)
            {
                UiValue thisExp = experiments?.Get(k);
                required.With(thisExp == null || thisExp.IsNull
                    ? LockedExperiment("locked:" + index)
                    : ExperimentConfigure.Experiment(c, thisExp));
                index++;
            }
            section.With(required);
        }

        if (cached.DiscountExperiments.Count > 0)
        {
            var discount = new UiNode(UiKind.Collapsible) { Key = "discountExperiments", Title = "Discount-Eligible Experiments" };
            int index = 0;
            foreach (var (k, amount) in cached.DiscountExperiments)
            {
                UiValue thisExp = experiments?.Get(k);
                discount.With(thisExp == null || thisExp.IsNull
                    ? LockedExperiment("locked:" + index)
                    : ExperimentConfigure.Experiment(c, thisExp, new UiNode(UiKind.Box)
                    {
                        Key = "discount",
                        Text = "Provides a discount of " + amount.AsStr() + " points to all required point pools.",
                    }));
                index++;
            }
            section.With(discount);
        }

        return section;
    }

    // tg: Techweb/nodes/TechNode.tsx:189-224  the design's tooltip: its name, build types, departments and cost, as text
    private static string DesignTooltip(Remapped r, Remapped.Design d)
    {
        var lines = new List<string> { d.Name };
        if (d.HasBuildTypes)
        {
            long buildTypes = (long)d.BuildTypes;
            long departmentFlags = (long)d.DepartmentFlags;
            lines.Add("• " + string.Join(", ", r.BuildTypes.Where(kv => long.TryParse(kv.Key, out long key) && (buildTypes & key) != 0).Select(kv => kv.Value.AsStr())));
            if (r.DepartmentFlags.Any(kv => long.TryParse(kv.Key, out long key) && (key & departmentFlags) == 0))
                lines.Add("• " + string.Join(", ", r.DepartmentFlags.Where(kv => long.TryParse(kv.Key, out long key) && (departmentFlags & key) != 0).Select(kv => kv.Value.AsStr())));
            if (d.Cost != null)
                lines.Add(MaterialCostSequence.CostText(UiValue.ObjectOf(("cost", d.Cost)), r.SheetMaterialAmount));
        }
        return string.Join("\n", lines);
    }

    // tg: Techweb/LockedExperiment.tsx:3  LockedExperiment
    private static UiNode LockedExperiment(string key) =>
        new UiNode(UiKind.Box) { Key = key }.With(
            new UiNode(UiKind.Button) { Key = "name", Fluid = true, Background = "#40628a", Disabled = true }.With(
                new UiNode(UiKind.Stack) { Key = "row", Align = "center", Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Key = "title" }.With(
                        new UiNode(UiKind.Icon) { Key = "lock", Icon = "lock" },
                        new UiNode(UiKind.Box) { Key = "text", Color = "rgba(0, 0, 0, 0.6)", Text = "Undiscovered Experiment" }),
                    new UiNode(UiKind.StackItem) { Key = "tag" }.With(new UiNode(UiKind.Box) { Color = "rgba(0, 0, 0, 0.5)", Text = "???" }))),
            new UiNode(UiKind.Box)
            {
                Key = "content",
                Text = "This experiment has not been discovered yet, continue researching nodes in the tree to discover the contents of this experiment.",
            });

    // tg: Techweb/nodes/detail.tsx:20  TechwebNodeDetail, :44  TechNodeDetail
    private static UiNode NodeDetail(TguiContext c, Remapped r, string selectedNode)
    {
        IReadOnlyList<UiValue> nodes = c.Data.Arr("nodes");
        UiValue node = string.IsNullOrEmpty(selectedNode) ? null : nodes.FirstOrDefault(x => x.Str("path") == selectedNode);
        if (node == null || !r.NodeCache.TryGetValue(node.Str("path"), out Remapped.Node cached))
            return null;

        int tabIndex = c.UseState("techweb:detailTab", 0);

        List<UiValue> prereqNodes = nodes.Where(x => cached.PrerequisiteNodes.Contains(x.Str("path"))).ToList();
        int complPrereq = cached.PrerequisiteNodes.Count(x => nodes.FirstOrDefault(y => y.Str("path") == x) is { } prereq && prereq.Num("tier", -1) == 0);
        List<UiValue> unlockedNodes = nodes.Where(x => cached.UnlockedNodes.Contains(x.Str("path"))).ToList();

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
            new UiNode(UiKind.Tab) { Key = "required", Text = "Required (" + complPrereq + "/" + prereqNodes.Count + ")", Selected = tabIndex == 0, OnClick = () => c.SetState("techweb:detailTab", 0) },
            new UiNode(UiKind.Tab) { Key = "unlocks", Text = "Unlocks (" + unlockedNodes.Count + ")", Selected = tabIndex == 1, OnClick = () => c.SetState("techweb:detailTab", 1) });

        var list = new UiNode(UiKind.Section) { Key = "list", Fill = true, Scrollable = true };
        foreach (UiValue n in tabIndex == 0 ? prereqNodes : unlockedNodes)
            list.With(TechNode(c, r, n));

        return new UiNode(UiKind.Stack) { Key = "detail", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerTabs" }.With(new UiNode(UiKind.Stack) { Key = "tabRow", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Align = "center" }.With(new UiNode(UiKind.Box) { Text = "Node" }),
                new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(tabs),
                new UiNode(UiKind.StackItem) { Key = "home", Align = "center" }.With(new UiNode(UiKind.Button)
                {
                    Key = "homeButton",
                    Icon = "home",
                    Text = "Home",
                    OnClick = () => SetRoute(c, ""),
                }))),
            new UiNode(UiKind.StackItem) { Key = "selected" }.With(TechNode(c, r, node, nodetails: true), new UiNode(UiKind.Divider) { Key = "divider" }),
            new UiNode(UiKind.StackItem) { Key = "nodes", Grow = 1 }.With(list));
    }

    // tg: Techweb/disks/DiskMenu.tsx:11  TechwebDiskMenu
    private static UiNode DiskMenu(TguiContext c, Remapped r, string diskType)
    {
        UiValue data = c.Data;
        // Check for the disk actually being inserted
        if ((diskType == "design" && !data.Truthy("d_disk")) || (diskType == "tech" && !data.Truthy("t_disk")))
            return null;

        UiNode content = diskType == "design" ? DesignDisk(c, r) : TechDisk(c, r);
        string title = diskType.Length > 0 ? char.ToUpperInvariant(diskType[0]) + diskType[1..] + " Disk" : " Disk";

        var actions = new UiNode(UiKind.StackItem) { Key = "actions", Align = "center" };
        if (diskType == "tech")
            actions.With(new UiNode(UiKind.Button) { Key = "load", Icon = "save", Text = "Web → Disk", OnClick = () => c.Act("loadTech") });
        actions.With(
            new UiNode(UiKind.Button) { Key = "upload", Icon = "upload", Text = "Disk → Web", OnClick = () => c.Act("uploadDisk", ("type", diskType)) },
            new UiNode(UiKind.Button)
            {
                Key = "eject",
                Icon = "eject",
                Text = "Eject",
                OnClick = () =>
                {
                    c.Act("ejectDisk", ("type", diskType));
                    SetRoute(c, "");
                },
            },
            new UiNode(UiKind.Button) { Key = "home", Icon = "home", Text = "Home", OnClick = () => SetRoute(c, "") });

        return new UiNode(UiKind.Stack) { Key = "disk", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "headerTabs" }.With(new UiNode(UiKind.Stack) { Key = "tabRow", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Align = "center" }.With(new UiNode(UiKind.Box) { Text = title }),
                new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
                    new UiNode(UiKind.Tab) { Key = "stored", Text = "Stored Data", Selected = true })),
                actions)),
            new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(content));
    }

    // tg: Techweb/disks/disks.tsx:7  TechwebDesignDisk
    private static UiNode DesignDisk(TguiContext c, Remapped r)
    {
        UiValue disk = c.Data.Get("d_disk");
        if (disk == null || disk.IsNull)
            return null;
        var slots = new UiNode(UiKind.Fragment) { Key = "slots" };
        int i = 0;
        foreach (UiValue x in disk.Arr("blueprints"))
        {
            var slot = new UiNode(UiKind.Section) { Key = "slot:" + i, Title = "Slot " + (i + 1) };
            string design = x.IsNull ? null : x.AsStr();
            if (design == null)
                slot.Text = "Empty";
            else
            {
                r.DesignCache.TryGetValue(design, out Remapped.Design d);
                slot.With(
                    new UiNode(UiKind.Box) { Key = "contains", Text = "Contains the design for " },
                    new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = (d?.Name ?? design) + ":" },
                    new UiNode(UiKind.Box) { Key = "pic", DmIcon = TguiSpritesheets.Design32, DmIconState = d?.Class ?? "", Width = "32px", Height = "32px" });
            }
            slots.With(slot);
            i++;
        }
        return slots;
    }

    // tg: Techweb/disks/disks.tsx:34  TechwebTechDisk
    private static UiNode TechDisk(TguiContext c, Remapped r)
    {
        UiValue disk = c.Data.Get("t_disk");
        if (disk == null || disk.IsNull)
            return null;
        var list = new UiNode(UiKind.Section) { Key = "stored", Fill = true, Scrollable = true };
        foreach (var (path, _) in disk.Obj("stored_research"))
            list.With(TechNode(c, r, UiValue.ObjectOf(("path", UiValue.Of(path))), nocontrols: true));
        return list;
    }
}
