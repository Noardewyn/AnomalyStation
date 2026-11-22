using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemMaster.tsx (gaps package P5). The window reads
// the ChemMaster's ui_data (chem_master.dm:276: the beaker, the buffer, the printing state, the selected and suggested containers)
// and ui_static_data (:248: the printable container categories with tg's pictures) and sends eject, insert, setCustomTransfer,
// transfer, toggleTransferMode, stopPrinting, setPillDuration, selectContainer and create. The kit has no CSS text transform or
// opacity on a button: the category titles are capitalized in C#, the Insert button's missing-container look is its tooltip;
// tgui-core's AnimatedNumber renders as its text. Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class ChemMaster
{
    // tg: ChemMaster.tsx:72
    public static UiNode Render(TguiContext c)
    {
        string analyzed = c.UseState<string>("analyzedReagent", null);
        UiValue analyzedReagent = analyzed == null ? null : FindReagent(c.Data, analyzed);
        // tg: ChemMaster.tsx:76  <Window width={450} height={620}><Window.Content scrollable>
        return c.Window(450, 620, null,
            analyzedReagent != null
                ? AnalysisResults(analyzedReagent, () => c.SetState<string>("analyzedReagent", null))
                : ChemMasterContent(c));
    }

    /// The reagent analysed: tg keeps the row itself in the window's state, the port keeps its ref and reads the row from the data
    /// (so a reagent that leaves both holders closes the analysis)
    private static UiValue FindReagent(UiValue data, string reference) =>
        (data.Get("beaker")?.Arr("contents") ?? Array.Empty<UiValue>())
            .Concat(data.Get("buffer")?.Arr("contents") ?? Array.Empty<UiValue>())
            .FirstOrDefault(r => r.Str("ref") == reference);

    // tg: ChemMaster.tsx:96  ChemMasterContent
    private static UiNode ChemMasterContent(TguiContext c)
    {
        UiValue data = c.Data;
        bool isPrinting = data.Truthy("isPrinting");
        UiValue beaker = data.Get("beaker");
        bool hasBeaker = beaker?.Truthy ?? false;
        UiValue buffer = data.Get("buffer");
        IReadOnlyList<UiValue> bufferContents = buffer?.Arr("contents") ?? Array.Empty<UiValue>();
        double itemCount = c.UseState("itemCount", 1.0);
        bool showPreferredContainer = c.UseState("showPreferredContainer", false);

        // tg: ChemMaster.tsx:124  Beaker
        UiNode beakerButtons = hasBeaker
            ? new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "volume", Color = "label", Text = TguiFormat.JsNumber(beaker.Num("currentVolume")) + " / " + TguiFormat.JsNumber(beaker.Num("maxVolume")) + " units " },
                new UiNode(UiKind.NumberInput)
                {
                    Key = "custom",
                    Width = "55px",
                    Unit = "u",
                    Step = 5,
                    StepPixelSize = 3,
                    Value = data.Num("customTransferAmount"),
                    MinValue = 0,
                    MaxValue = beaker.Num("maxVolume"),
                    OnChange = value => c.Act("setCustomTransfer", ("target", value)),
                },
                new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject", OnClick = () => c.Act("eject") })
            : new UiNode(UiKind.Button)
            {
                Key = "insert",
                Icon = "eject",
                Tooltip = !data.Truthy("hasBeakerInHand") ? "You need to hold a container in your hand" : null,
                Text = "Insert",
                OnClick = () => c.Act("insert"),
            };
        UiNode beakerBody = !hasBeaker
            ? new UiNode(UiKind.Box) { Key = "none", Color = "label", Text = "No beaker loaded." }
            : beaker.Num("currentVolume") == 0
                ? new UiNode(UiKind.Box) { Key = "empty", Color = "label", Text = "Beaker is empty." }
                : new UiNode(UiKind.Table) { Key = "contents" }.With(beaker.Arr("contents").Select(chemical => ReagentEntry(c, chemical, "buffer")).ToArray());
        UiNode beakerSection = new UiNode(UiKind.Section) { Key = "beaker", Title = "Beaker", Buttons = beakerButtons }.With(beakerBody);

        // tg: ChemMaster.tsx:189  Buffer
        bool isTransfering = data.Truthy("isTransfering");
        UiNode bufferSection = new UiNode(UiKind.Section)
        {
            Key = "buffer",
            Title = "Buffer",
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "volume", Color = "label", Text = TguiFormat.JsNumber(buffer?.Num("currentVolume") ?? 0) + " / " + TguiFormat.JsNumber(buffer?.Num("maxVolume") ?? 0) + " units" },
                new UiNode(UiKind.Button)
                {
                    Key = "mode",
                    Color = isTransfering ? "good" : "bad",
                    Icon = isTransfering ? "exchange-alt" : "trash",
                    Text = isTransfering ? "Moving reagents" : "Destroying reagents",
                    OnClick = () => c.Act("toggleTransferMode"),
                }),
        }.With(bufferContents.Count == 0
            ? new UiNode(UiKind.Box) { Key = "empty", Color = "label", Text = "Buffer is empty." }
            : new UiNode(UiKind.Table) { Key = "contents" }.With(bufferContents.Select(chemical => ReagentEntry(c, chemical, "beaker")).ToArray()));

        UiNode root = new UiNode(UiKind.Box) { Key = "content" }.With(beakerSection, bufferSection);

        if (!isPrinting)
        {
            // tg: ChemMaster.tsx:222  Packaging
            UiNode packagingButtons = null;
            if (bufferContents.Count != 0)
            {
                double each = Math.Round(Math.Min(data.Num("selectedContainerVolume"), (buffer?.Num("currentVolume") ?? 0) / itemCount) * 100) / 100;
                packagingButtons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "suggest", Checkbox = showPreferredContainer, Text = "Suggest", OnClick = () => c.SetState("showPreferredContainer", !showPreferredContainer) },
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = "count",
                        Unit = "items",
                        Step = 1,
                        Value = itemCount,
                        MinValue = 1,
                        MaxValue = data.Num("maxPrintable"),
                        OnChange = value => c.SetState("itemCount", value),
                    },
                    data.Str("selectedContainerCategory") == "pills"
                        ? new UiNode(UiKind.NumberInput)
                        {
                            Key = "duration",
                            Unit = "s",
                            Step = 1,
                            Value = data.Num("selectedPillDuration"),
                            MinValue = 0,
                            MaxValue = data.Num("maxPillDuration"),
                            OnChange = value => c.Act("setPillDuration", ("duration", value)),
                        }
                        : null,
                    new UiNode(UiKind.Box) { Key = "each", Text = " " + TguiFormat.JsNumber(each) + " u. each " },
                    new UiNode(UiKind.Button) { Key = "print", Icon = "flask", Text = "Print", OnClick = () => c.Act("create", ("itemCount", itemCount)) });
            }
            UiNode packaging = new UiNode(UiKind.Section) { Key = "packaging", Title = "Packaging", Buttons = packagingButtons };
            foreach (UiValue category in data.Arr("categories"))
            {
                UiNode group = new UiNode(UiKind.Box) { Key = "cat:" + category.Str("name") }.With(GroupTitle(category.Str("name")));
                UiNode row = new UiNode(UiKind.Stack) { Key = "containers", Wrap = true };
                foreach (UiValue container in category.Arr("containers"))
                    row.With(new UiNode(UiKind.StackItem) { Key = "c:" + container.Str("ref") }.With(ContainerButton(c, category, container, showPreferredContainer)));
                packaging.With(group.With(row));
            }
            root.With(packaging);
        }
        else
        {
            // tg: ChemMaster.tsx:290  Printing
            double progress = data.Num("printingProgress"), total = data.Num("printingTotal");
            root.With(new UiNode(UiKind.Section)
            {
                Key = "printing",
                Title = "Printing",
                Buttons = new UiNode(UiKind.Button) { Key = "stop", Color = "bad", Icon = "times", Text = "Stop", OnClick = () => c.Act("stopPrinting") },
            }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "progress",
                Value = progress,
                MinValue = 0,
                MaxValue = total,
                Color = "good",
                Text = "Printing " + TguiFormat.JsNumber(progress) + " out of " + TguiFormat.JsNumber(total),
            }));
        }
        return root;
    }

    // tg: ChemMaster.tsx:330  ReagentEntry
    private static UiNode ReagentEntry(TguiContext c, UiValue chemical, string transferTo)
    {
        bool isPrinting = c.Data.Truthy("isPrinting");
        double custom = c.Data.Num("customTransferAmount");
        string reference = chemical.Str("ref");
        UiNode Transfer(string key, string text, double amount) => new UiNode(UiKind.Button)
        {
            Key = key,
            Disabled = isPrinting,
            Text = text,
            OnClick = () => c.Act("transfer", ("reagentRef", reference), ("amount", amount), ("target", transferTo)),
        };
        return new UiNode(UiKind.TableRow) { Key = "r:" + reference }.With(
            new UiNode(UiKind.TableCell) { Key = "name", Color = "label", Text = chemical.Str("name") + " " + TguiFormat.JsNumber(chemical.Num("volume")) + "u" },
            new UiNode(UiKind.TableCell) { Key = "buttons", Width = "1%" }.With(
                Transfer("1", "1", 1),
                Transfer("5", "5", 5),
                Transfer("10", "10", 10),
                Transfer("custom", TguiFormat.JsNumber(custom), custom),
                Transfer("all", "All", 1000),
                new UiNode(UiKind.Button) { Key = "ask", Icon = "ellipsis-h", Tooltip = "Custom amount", Disabled = isPrinting, OnClick = () => c.Act("transfer", ("reagentRef", reference), ("amount", -1), ("target", transferTo)) },
                new UiNode(UiKind.Button) { Key = "analyze", Icon = "question", Tooltip = "Analyze", OnClick = () => c.SetState("analyzedReagent", reference) }));
    }

    // tg: ChemMaster.tsx:440  ContainerButton
    private static UiNode ContainerButton(TguiContext c, UiValue category, UiValue container, bool showPreferredContainer)
    {
        UiValue data = c.Data;
        string selected = data.Str("selectedContainerRef"), suggested = data.Str("suggestedContainerRef"), reference = container.Str("ref");
        bool isPillPatch = category.Str("name") is "pills" or "patches";
        return new UiNode(UiKind.ImageButton)
        {
            Key = "container:" + reference,
            DmIcon = container.Str("icon"),
            DmIconState = container.Str("icon_state"),
            Size = isPillPatch ? 1.5 : 2, // imageSize 48 or 64 (32 px each)
            Tooltip = TguiFormat.Capitalize(container.Str("name")) + " (" + TguiFormat.JsNumber(container.Num("volume")) + "u)",
            Color = showPreferredContainer && selected != suggested && reference == suggested ? "blue" : "transparent",
            Selected = reference == selected,
            Disabled = data.Truthy("isPrinting"),
            OnClick = () => c.Act("selectContainer", ("ref", reference)),
        };
    }

    // tg: ChemMaster.tsx:477  AnalysisResults
    private static UiNode AnalysisResults(UiValue analysisData, Action onExit)
    {
        double purity = analysisData.Num("purity");
        string purityLevel = purity <= 0.5 ? "bad" : purity <= 0.75 ? "average" : "good"; // Color names
        double overdose = analysisData.Num("overdose");
        IReadOnlyList<UiValue> addictionTypes = analysisData.Arr("addictionTypes");
        string color = analysisData.Str("color");
        return new UiNode(UiKind.Section)
        {
            Key = "analysis",
            Title = "Analysis Results",
            Buttons = new UiNode(UiKind.Button) { Key = "back", Icon = "arrow-left", Text = "Back", OnClick = onExit },
        }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Label = "Name", Text = analysisData.Str("name") },
            new UiNode(UiKind.LabeledItem) { Label = "Purity" }.With(new UiNode(UiKind.Box) { Color = purityLevel, Text = TguiFormat.Capitalize(purityLevel) }),
            new UiNode(UiKind.LabeledItem) { Label = "pH", Text = TguiFormat.JsNumber(analysisData.Num("pH")) },
            new UiNode(UiKind.LabeledItem) { Label = "Color" }.With(new UiNode(UiKind.ColorBox) { Color = color }, new UiNode(UiKind.Box) { Text = " " + color }),
            new UiNode(UiKind.LabeledItem) { Label = "Description", Text = analysisData.Str("description") },
            new UiNode(UiKind.LabeledItem) { Label = "Metabolization Rate", Text = TguiFormat.JsNumber(analysisData.Num("metaRate")) + " units/second" },
            new UiNode(UiKind.LabeledItem) { Label = "Overdose Threshold", Text = overdose > 0 ? TguiFormat.JsNumber(overdose) + " units" : "N/A" },
            // tg: :536  addictionTypes.toString(): JS joins an array with commas
            new UiNode(UiKind.LabeledItem) { Label = "Addiction Types", Text = addictionTypes.Count > 0 ? string.Join(",", addictionTypes.Select(a => a.AsStr())) : "N/A" }));
    }

    // tg: ChemMaster.tsx:543  GroupTitle
    private static UiNode GroupTitle(string title) =>
        new UiNode(UiKind.Stack) { Key = "title", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider)),
            new UiNode(UiKind.StackItem) { Color = "gray" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.CssCapitalize(title) }),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Divider)));
}
