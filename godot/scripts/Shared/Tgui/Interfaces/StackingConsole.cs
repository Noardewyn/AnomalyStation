using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StackingConsole.jsx (Lavaland L2b, D304).
// The window reads tg's ui_data of the stacking machine console (machine_stacking.dm:45) and sends release and rotate.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StackingConsole
{
    // tg: StackingConsole.jsx:12
    public static UiNode Render(TguiContext c)
    {
        // <Window width={320} height={340}> <Window.Content scrollable>: the content in the window's own area
        return c.Window(320, 340, null,
            !c.Data.Truthy("machine")
                ? new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No connected stacking machine" }
                : Content(c));
    }

    // tg: StackingConsole.jsx:28
    private static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> contents = data.Arr("contents");
        UiNode Rotate(string key, int input) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = "rotate",
            Text = "Rotate",
            OnClick = () => c.Act("rotate", ("input", input)),
        };
        var stored = new UiNode(UiKind.Section) { Key = "stored", Title = "Stored Materials" };
        if (contents.Count == 0)
            stored.With(new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "No stored materials" });
        else
        {
            var list = new UiNode(UiKind.LabeledList) { Key = "sheets" };
            foreach (UiValue sheet in contents)
            {
                string type = sheet.Str("type");
                list.With(new UiNode(UiKind.LabeledItem)
                {
                    Key = "sheet:" + type,
                    Label = sheet.Str("name"),
                    Text = sheet.Truthy("amount") ? TguiFormat.JsNumber(sheet.Num("amount")) : "Unknown",
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "release",
                        Icon = "eject",
                        Text = "Release",
                        OnClick = () => c.Act("release", ("type", type)),
                    },
                });
            }
            stored.With(list);
        }
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section) { Key = "settings" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem)
                    {
                        Key = "amount",
                        Label = "Stacking Amount",
                        Text = data.Truthy("stacking_amount") ? TguiFormat.JsNumber(data.Num("stacking_amount")) : "Unknown",
                    },
                    // <Box style={{ textTransform: 'capitalize' }}>
                    new UiNode(UiKind.LabeledItem) { Key = "input", Label = "Input", Text = TguiFormat.CssCapitalize(data.Str("input_direction")), Buttons = Rotate("rotateInput", 1) },
                    new UiNode(UiKind.LabeledItem) { Key = "output", Label = "Output", Text = TguiFormat.CssCapitalize(data.Str("output_direction")), Buttons = Rotate("rotateOutput", 0) })),
            stored);
    }
}
