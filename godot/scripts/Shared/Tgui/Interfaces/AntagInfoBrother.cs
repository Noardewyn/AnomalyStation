using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoBrother.tsx (syndicate package X4, D316): the
// blood brother's info window, from /datum/antagonist/brother's ui_static_data (brother.dm:202; tg's Info type lists a brothers field
// the datum never sends and the window never reads). Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class AntagInfoBrother
{
    // tg: AntagInfoBrother.tsx:13
    public static UiNode Render(TguiContext c)
    {
        string antag_name = c.Data.Str("antag_name");
        IReadOnlyList<UiValue> objectives = c.Data.Arr("objectives");
        // tg: AntagInfoBrother.tsx:17  <Window width={620} height={250}><Section scrollable fill><Stack vertical>
        return c.Window(620, 250, null,
            new UiNode(UiKind.Section) { Key = "info", Scrollable = true, Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                    // tg: AntagInfoBrother.tsx:21  <Stack.Item textColor="red" fontSize="20px">You are the {antag_name}!</Stack.Item>
                    new UiNode(UiKind.StackItem) { Key = "you_are" }.With(
                        new UiNode(UiKind.Box) { Color = "red", FontSize = "20px", Text = "You are the " + antag_name + "!" }),
                    // tg: AntagInfoBrother.tsx:24  <ObjectivePrintout objectives={objectives} />
                    new UiNode(UiKind.StackItem) { Key = "printout" }.With(Objectives.ObjectivePrintout(objectives)))));
    }
}
