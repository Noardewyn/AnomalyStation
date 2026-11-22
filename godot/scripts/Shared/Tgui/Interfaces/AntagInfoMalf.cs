using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoMalf.tsx (silicons SI8, D326): the malfunctioning
// AI's info window, from /datum/antagonist/malf_ai's ui_static_data (malf_ai.dm:173): the flavor's intro with the objectives, the
// diagnostics (the goal and the allies) and the code words. tgui-core's BlockQuote is a box. Original copyright and license: tgstation,
// AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoMalf
{
    // tg: AntagInfoMalf.tsx:16-29  allystyle, badstyle, goalstyle
    private const string AllyColor = "yellow";
    private const string BadColor = "red";
    private const string GoalColor = "lightgreen";

    // tg: AntagInfoMalf.tsx:184
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "hackerman";
        return c.Window(660, 530, null,
            new UiNode(UiKind.Stack) { Key = "malf", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "top", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "intro", Width = "70%" }.With(IntroductionSection(c, data)),
                    new UiNode(UiKind.StackItem) { Key = "flavor", Width = "30%" }.With(FlavorSection(data)))),
                new UiNode(UiKind.StackItem) { Key = "codewords" }.With(CodewordsSection(data))));
    }

    // tg: AntagInfoMalf.tsx:58  IntroductionSection
    private static UiNode IntroductionSection(TguiContext c, UiValue data)
    {
        return new UiNode(UiKind.Section) { Key = "intro", Title = "Intro", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Key = "intro", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.Box) { Key = "text", FontSize = "25px", Text = data.Str("intro"), Wrap = true }),
                new UiNode(UiKind.StackItem) { Key = "objectives", Grow = 1 }.With(Objectives.ObjectivePrintout(data.Arr("objectives"),
                    titleMessage: "Your prime objectives", objectivePrefix: "≥-",
                    objectiveFollowup: Objectives.ReplaceObjectivesButton(c, data.Truthy("can_change_objective"), "Overwrite Objectives Data", "green")))));
    }

    // tg: AntagInfoMalf.tsx:84  FlavorSection
    private static UiNode FlavorSection(UiValue data)
    {
        var policy = new UiNode(UiKind.Button)
        {
            Key = "policy", Icon = "hammer", Text = "Policy",
            Tooltip = "This is a gameplay suggestion for bored ais. You don't have to follow it, unless you want some ideas for how to spend the round.",
        };
        return new UiNode(UiKind.Section) { Key = "flavor", Title = "Diagnostics", Fill = true, Buttons = policy }.With(
            new UiNode(UiKind.Stack) { Key = "flavor", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "goal" }.With(
                    new UiNode(UiKind.Box) { Key = "label", Bold = true, Color = GoalColor, Text = "System Integrity Report:" },
                    new UiNode(UiKind.Box) { Key = "goal", Text = ">" + data.Str("goal"), Wrap = true }),
                new UiNode(UiKind.Divider) { Key = "divider1" },
                new UiNode(UiKind.StackItem) { Key = "allies", Grow = 1 }.With(
                    new UiNode(UiKind.Box) { Key = "label", Bold = true, Color = AllyColor, Text = "Morality Core Report:" },
                    new UiNode(UiKind.Box) { Key = "allies", Text = ">" + data.Str("allies"), Wrap = true }),
                new UiNode(UiKind.Divider) { Key = "divider2" },
                new UiNode(UiKind.StackItem) { Key = "grade" }.With(
                    new UiNode(UiKind.Box) { Key = "label", Bold = true, Color = BadColor, Text = "Overall Sentience Coherence Grade: FAILING." },
                    new UiNode(UiKind.Box) { Key = "report", Text = ">Report to Nanotrasen?" },
                    new UiNode(UiKind.Box) { Key = "answer", Text = ">>N" })));
    }

    // tg: AntagInfoMalf.tsx:137  CodewordsSection
    private static UiNode CodewordsSection(UiValue data)
    {
        var row = new UiNode(UiKind.Stack) { Key = "codewords", Fill = true };
        if (!data.Truthy("has_codewords"))
        {
            row.With(new UiNode(UiKind.Box)
            {
                Key = "none", Wrap = true,
                Text = "You have not been supplied the Syndicate codewords. You will have to use alternative methods to find potential allies. Proceed with caution, however, as everyone is a potential foe.",
            });
        }
        else
        {
            row.With(
                new UiNode(UiKind.StackItem) { Key = "about", Grow = 1 }.With(
                    new UiNode(UiKind.Box)
                    {
                        Key = "about", Wrap = true,
                        Text = "New access to restricted channels has provided you with intercepted syndicate codewords. Syndicate agents will respond as if you're one of their own. Proceed with caution, however, as everyone is a potential foe.",
                    },
                    new UiNode(UiKind.Box) { Key = "flag", Bold = true, Color = BadColor, Wrap = true, Text = "The speech recognition subsystem has been configured to flag these codewords." }),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "words", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "words", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "phrases_label" }.With(new UiNode(UiKind.Box) { Text = "Code Phrases:" }),
                    new UiNode(UiKind.StackItem) { Key = "phrases" }.With(new UiNode(UiKind.Box) { Key = "phrases", Bold = true, Color = "blue", Text = data.Str("phrases"), Wrap = true }),
                    new UiNode(UiKind.StackItem) { Key = "responses_label" }.With(new UiNode(UiKind.Box) { Text = "Code Responses:" }),
                    new UiNode(UiKind.StackItem) { Key = "responses" }.With(new UiNode(UiKind.Box) { Key = "responses", Bold = true, Color = "red", Text = data.Str("responses"), Wrap = true }))));
        }
        return new UiNode(UiKind.Section) { Key = "codewords", Title = "Codewords" }.With(row);
    }
}
