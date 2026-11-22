using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/Objectives.tsx (syndicate package A1, D316): the
// objective printout every antagonist's info window shows, and its button that replaces the objectives with a custom one. Original
// copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Objectives
{
    // tg: Objectives.tsx:38  ObjectivePrintout: the objectives' data list (count, name, explanation, complete) as "#1: explanation"
    public static UiNode ObjectivePrintout(IReadOnlyList<UiValue> objectives, bool fill = false, UiNode objectiveFollowup = null,
        string objectivePrefix = null, object objectiveTextSize = null, string titleMessage = null, string key = "objectives")
    {
        var lines = new UiNode(UiKind.StackItem) { Key = key + ":list" };
        if (objectives.Count == 0)
            lines.With(new UiNode(UiKind.Box) { Key = key + ":none", Text = "None!" });
        else
        {
            foreach (UiValue objective in objectives)
            {
                string count = TguiFormat.ToFixed(objective.Num("count"), 0);
                // tg: Objectives.tsx:58  <Stack.Item fontSize={objectiveTextSize} key={objective.count}>{prefix || '#'}{count}: {explanation}</Stack.Item>
                lines.With(new UiNode(UiKind.StackItem) { Key = key + ":" + count }.With(
                    new UiNode(UiKind.Box) { FontSize = objectiveTextSize, Text = (objectivePrefix ?? "#") + count + ": " + objective.Str("explanation"), Wrap = true }));
            }
        }

        var stack = new UiNode(UiKind.Stack) { Key = key, Fill = fill, Vertical = true }.With(
            // tg: Objectives.tsx:52  <Stack.Item bold>{titleMessage || `Your current objectives`}:</Stack.Item>
            new UiNode(UiKind.StackItem) { Key = key + ":title" }.With(
                new UiNode(UiKind.Box) { Bold = true, Text = (string.IsNullOrEmpty(titleMessage) ? "Your current objectives" : titleMessage) + ":" }),
            lines);
        if (objectiveFollowup != null)
            stack.With(new UiNode(UiKind.StackItem) { Key = key + ":followup" }.With(objectiveFollowup));
        return stack;
    }

    // tg: Objectives.tsx:93  ReplaceObjectivesButton: shown only when the antagonist may change its objectives; acts change_objectives
    public static UiNode ReplaceObjectivesButton(TguiContext c, bool can_change_objective, string button_title, string button_colour,
        string button_tooltip = "Replace your existing objectives with a custom one. This action can only be taken once")
    {
        if (!can_change_objective)
            return null;
        return new UiNode(UiKind.Button)
        {
            Key = "change_objectives",
            Color = button_colour,
            Text = button_title,
            Tooltip = button_tooltip,
            OnClick = () => c.Act("change_objectives"),
        };
    }
}
