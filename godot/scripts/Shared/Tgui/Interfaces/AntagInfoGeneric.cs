using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoGeneric.tsx (syndicate package A1, D316): the
// antagonist info window that the antag info button opens, from /datum/antagonist's ui_static_data (antag_datum.dm:143). Original
// copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoGeneric
{
    // tg: AntagInfoGeneric.tsx:12
    public static UiNode Render(TguiContext c)
    {
        string antag_name = c.Data.Str("antag_name");
        IReadOnlyList<UiValue> objectives = c.Data.Arr("objectives");
        // tg: AntagInfoGeneric.tsx:16  <Window width={620} height={250}><Section scrollable fill><Stack vertical>
        return c.Window(620, 250, null,
            new UiNode(UiKind.Section) { Key = "info", Scrollable = true, Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
                    // tg: AntagInfoGeneric.tsx:20  <Stack.Item textColor="red" fontSize="20px">You are the {antag_name}!</Stack.Item>
                    new UiNode(UiKind.StackItem) { Key = "you_are" }.With(
                        new UiNode(UiKind.Box) { Color = "red", FontSize = "20px", Text = "You are the " + antag_name + "!" }),
                    new UiNode(UiKind.StackItem) { Key = "printout" }.With(Objectives.ObjectivePrintout(objectives)))));
    }
}
