using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/WarrantConsole.tsx (gaps SC2, D333: the security
// warrant console). The crew with outstanding citations sorted by name, the selected one's citations with their fine's colour, a printed
// bounty and a payment towards a fine. Engine-free. The selected record is the window's local state (tg's useLocalState), the amount to
// pay each citation's (tg's useState).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class WarrantConsole
{
    private const string SelectedKey = "warrantRecord";

    // tg: WarrantConsole.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue selectedRecord = CurrentRecord(c);
        return c.Window(500, 500, null,
            new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "list", Grow = 2 }.With(RecordList(c)),
                selectedRecord != null ? new UiNode(UiKind.StackItem) { Key = "view", Grow = 3 }.With(ViewRecord(c, selectedRecord)) : null));
    }

    /** Displays all valid records with warrants. */
    // tg: WarrantConsole.tsx:61
    private static UiNode RecordList(TguiContext c)
    {
        IReadOnlyList<UiValue> records = c.Data.Arr("records");
        var sorted = records.OrderBy(record => record.Str("crew_name", null), System.StringComparer.Ordinal).ToList();
        string selected = c.UseState<string>(SelectedKey, null);

        void SelectHandler(UiValue record)
        {
            if (selected == record.Str("crew_ref"))
                c.SetState<string>(SelectedKey, null);
            else
                c.SetState(SelectedKey, record.Str("crew_ref"));
        }

        UiNode content;
        if (records.Count == 0)
            content = new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "No citations issued." };
        else
        {
            content = new UiNode(UiKind.Tabs) { Key = "records", Vertical = true };
            for (int index = 0; index < sorted.Count; index++)
            {
                UiValue record = sorted[index];
                content.With(new UiNode(UiKind.Tab)
                {
                    Key = "record:" + index,
                    Candystripe = true,
                    Selected = selected != null && selected == record.Str("crew_ref"),
                    Text = record.Str("crew_name") + ": " + record.Arr("citations").Count,
                    OnClick = () => SelectHandler(record),
                });
            }
        }

        return new UiNode(UiKind.Section)
        {
            Key = "citations",
            Fill = true,
            Scrollable = true,
            Title = "Citations",
            Buttons = new UiNode(UiKind.Button) { Key = "refresh", Icon = "sync", Tooltip = "Refresh", OnClick = () => c.Act("refresh") },
        }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(content));
    }

    /** Views info on the current selection. */
    // tg: WarrantConsole.tsx:117
    private static UiNode ViewRecord(TguiContext c, UiValue foundRecord)
    {
        IReadOnlyList<UiValue> citations = foundRecord.Arr("citations");
        var stack = new UiNode(UiKind.Stack) { Fill = true, Vertical = true };
        for (int index = 0; index < citations.Count; index++)
            stack.With(new UiNode(UiKind.StackItem) { Key = "citation:" + index }.With(CitationManager(c, foundRecord, citations[index])));
        return new UiNode(UiKind.Section) { Key = "record", Fill = true, Scrollable = true, Title = foundRecord.Str("crew_name") }.With(stack);
    }

    /** Handles paying fines */
    // tg: WarrantConsole.tsx:137
    private static UiNode CitationManager(TguiContext c, UiValue foundRecord, UiValue citation)
    {
        string crew_ref = foundRecord.Str("crew_ref");
        double fine = citation.Num("fine");
        string fine_ref = citation.Str("fine_ref");

        string payingKey = "paying:" + fine_ref;
        double paying = c.UseState(payingKey, 5.0);
        bool payingIsValid = c.UseState(payingKey + ":valid", true);

        var list = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "details", Label = "Details" }.With(
                // tg's BlockQuote
                new UiNode(UiKind.Box) { Color = "label", Wrap = true, Text = citation.Str("details") }),
            new UiNode(UiKind.LabeledItem) { Key = "author", Label = "Author", Text = citation.Str("author") },
            new UiNode(UiKind.LabeledItem) { Key = "time", Label = "Time", Text = citation.Str("time") },
            new UiNode(UiKind.LabeledItem) { Key = "fine", Label = "Fine", Text = TguiFormat.JsNumber(fine) },
            new UiNode(UiKind.LabeledItem) { Key = "paid", Label = "Paid", Text = TguiFormat.JsNumber(citation.Num("paid")) },
            fine > 0
                ? new UiNode(UiKind.LabeledItem) { Key = "pay", Label = "Pay" }.With(
                    new UiNode(UiKind.NumberInput)
                    {
                        Key = payingKey,
                        MaxValue = fine,
                        MinValue = 5,
                        Value = paying,
                        OnChange = value => c.SetState(payingKey, value),
                        OnValidation = valid => c.SetState(payingKey + ":valid", valid),
                    },
                    c.ButtonConfirm("pay:" + fine_ref, new UiNode(UiKind.Button)
                    {
                        Disabled = !payingIsValid,
                        Text = "Pay",
                        OnClick = () => c.Act("pay", ("amount", paying), ("crew_ref", crew_ref), ("fine_ref", fine_ref)),
                    }))
                : null);

        return new UiNode(UiKind.Collapsible)
        {
            Key = "fine:" + fine_ref,
            Color = FineColor(fine),
            Title = citation.Str("fine_name"),
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "print",
                Disabled = fine <= 0,
                Icon = "print",
                Text = "Print",
                OnClick = () => c.Act("print", ("crew_ref", crew_ref), ("fine_ref", fine_ref)),
            },
        }.With(list);
    }

    /// <summary>tg's getCurrentRecord (WarrantConsole.tsx:213): the selected record as the data has it now, or null.</summary>
    public static UiValue CurrentRecord(TguiContext c)
    {
        string selected = c.UseState<string>(SelectedKey, null);
        if (selected == null)
            return null;
        return c.Data.Arr("records").FirstOrDefault(record => record.Str("crew_ref") == selected);
    }

    /** Returns a color based on the fine amount */
    // tg: WarrantConsole.tsx:230
    public static string FineColor(double fine) => fine switch
    {
        > 700 => "bad",
        > 300 => "average",
        0 => "grey",
        _ => "",
    };
}
