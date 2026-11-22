using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoTraitor.tsx (syndicate package A2, D316): the
// traitor's info window, from /datum/antagonist/traitor's ui_static_data (datum_traitor.dm:223): the employer's intro and the objective
// printout, the employer's allegiances and thoughts, the uplink's code, and the code words. tgui-core's BlockQuote is a box. Original
// copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoTraitor
{
    // tg: AntagInfoTraitor.tsx:8-21  allystyle, badstyle, goalstyle
    private const string AllyColor = "yellow";
    private const string BadColor = "red";
    private const string GoalColor = "lightblue";

    // tg: AntagInfoTraitor.tsx:199
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = data.Str("theme", null);
        var stack = new UiNode(UiKind.Stack) { Key = "traitor", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "top", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "intro", Width = "70%" }.With(IntroductionSection(data)),
                new UiNode(UiKind.StackItem) { Key = "employer", Width = "30%" }.With(EmployerSection(data)))));
        if (data.Truthy("given_uplink"))
            stack.With(new UiNode(UiKind.StackItem) { Key = "uplink" }.With(UplinkSection(data)));
        stack.With(new UiNode(UiKind.StackItem) { Key = "codewords" }.With(CodewordsSection(data)));
        return c.Window(620, 580, null, stack);
    }

    // tg: AntagInfoTraitor.tsx:42  IntroductionSection
    private static UiNode IntroductionSection(UiValue data)
    {
        return new UiNode(UiKind.Section) { Key = "intro", Title = "Intro", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Key = "intro", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.Box) { Key = "text", FontSize = "25px", Text = data.Str("intro"), Wrap = true }),
                new UiNode(UiKind.StackItem) { Key = "objectives", Grow = 1 }.With(Objectives.ObjectivePrintout(data.Arr("objectives")))));
    }

    // tg: AntagInfoTraitor.tsx:57  EmployerSection
    private static UiNode EmployerSection(UiValue data)
    {
        var policy = new UiNode(UiKind.Button)
        {
            Key = "policy", Icon = "hammer", Text = "Policy",
            Tooltip = "This is a gameplay suggestion for bored traitors. You don't have to follow it, unless you want some ideas for how to spend the round.",
        };
        return new UiNode(UiKind.Section) { Key = "employer", Title = "Employer", Fill = true, Scrollable = true, Buttons = policy }.With(
            new UiNode(UiKind.Stack) { Key = "employer", Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "allies" }.With(
                    new UiNode(UiKind.Box) { Key = "label", Bold = true, Color = AllyColor, Text = "Your allegiances:" },
                    new UiNode(UiKind.Box) { Key = "allies", Text = data.Str("allies"), Wrap = true }),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "goal" }.With(
                    new UiNode(UiKind.Box) { Key = "label", Bold = true, Color = GoalColor, Text = "Employer thoughts:" },
                    new UiNode(UiKind.Box) { Key = "goal", Text = data.Str("goal"), Wrap = true })));
    }

    // tg: AntagInfoTraitor.tsx:102  UplinkSection: the uplink's intro, its code and failsafe, and how to unlock it
    private static UiNode UplinkSection(UiValue data)
    {
        string code = data.Str("code", null);
        string failsafe_code = data.Str("failsafe_code", null);
        var intro = new UiNode(UiKind.StackItem) { Key = "intro" }.With(new UiNode(UiKind.Box) { Key = "intro", Bold = true, Text = data.Str("uplink_intro"), Wrap = true });
        if (!string.IsNullOrEmpty(code))
            intro.With(new UiNode(UiKind.Box) { Key = "code", Bold = true, Color = GoalColor, Text = "Code: " + code });
        if (!string.IsNullOrEmpty(failsafe_code))
            intro.With(new UiNode(UiKind.Box) { Key = "failsafe", Bold = true, Color = BadColor, Text = "Failsafe: " + failsafe_code });
        return new UiNode(UiKind.Section) { Key = "uplink", Title = "Uplink" }.With(
            new UiNode(UiKind.Stack) { Key = "uplink", Fill = true }.With(
                intro,
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "unlock", Align = "center" }.With(new UiNode(UiKind.Box) { Key = "unlock", Text = data.Str("uplink_unlock_info"), Wrap = true })));
    }

    // tg: AntagInfoTraitor.tsx:148  CodewordsSection
    private static UiNode CodewordsSection(UiValue data)
    {
        var row = new UiNode(UiKind.Stack) { Key = "codewords", Fill = true };
        if (!data.Truthy("has_codewords"))
        {
            row.With(new UiNode(UiKind.Box)
            {
                Key = "none", Wrap = true,
                Text = "You have not been supplied with codewords. You will have to use alternative methods to find potential allies. Proceed with caution, however, as everyone is a potential foe.",
            });
        }
        else
        {
            row.With(
                new UiNode(UiKind.StackItem) { Key = "about", Grow = 1 }.With(
                    new UiNode(UiKind.Box)
                    {
                        Key = "about", Wrap = true,
                        Text = "Your employer provided you with the following codewords to identify fellow agents. Use the codewords during regular conversation to identify other agents. Proceed with caution, however, as everyone is a potential foe.",
                    },
                    new UiNode(UiKind.Box) { Key = "memorized", Bold = true, Color = BadColor, Wrap = true, Text = "You have memorized the codewords, allowing you to recognise them when heard." }),
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
