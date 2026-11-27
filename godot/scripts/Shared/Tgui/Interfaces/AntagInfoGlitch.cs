using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoGlitch.tsx: the bitrunning glitch's info
// window (the cyber police, the cyber tactical unit, the netguardian prime and the domain's ghost actors), from /datum/antagonist's
// ui_static_data (antag_datum.dm:143), drawn as a terminal listing. Its lines mix inline styled words (tg's span styles), so each is
// the chat's HTML text (UiKind.Html, which reads inline color and font-size). Original copyright and license: tgstation, AGPL-3.0
// (code). See ../tgstation/LICENSE.

public static class AntagInfoGlitch
{
    // tg: AntagInfoGlitch.tsx:12-19  textStyles
    private const string VariableStyle = "color:white";
    private const string DangerStyle = "color:red";

    private static string Span(string style, string text) => "<span style='" + style + "'>" + text + "</span>";

    // The chat's HTML text does not size itself to its lines in a scrolling section: each line takes the height its words wrap to
    // in the 350 px window (about 45 characters a line of 13 px text, 21 px each).
    private static UiNode Line(string key, string html, string fontSize = null, bool bold = false)
    {
        int characters = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "").Length;
        int lines = Math.Max(1, (characters + 44) / 45);
        return new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Html)
        {
            Key = key,
            TextValue = Span((bold ? "font-weight:bold;" : "") + (fontSize != null ? "font-size:" + fontSize : ""), html),
            Height = (lines * 21 + (fontSize != null ? 8 : 4)) + "px",
        });
    }

    // tg: AntagInfoGlitch.tsx:21
    public static UiNode Render(TguiContext c)
    {
        string antag_name = c.Data.Str("antag_name");
        IReadOnlyList<UiValue> objectives = c.Data.Arr("objectives");
        string explanation = objectives.Count > 0 ? objectives[0].Str("explanation") : "";
        // tg: AntagInfoGlitch.tsx:26  <Window width={350} height={450} theme="ntos_terminal"><Section scrollable fill><Stack fill vertical>
        c.WindowTheme = "ntos_terminal";
        var stack = new UiNode(UiKind.Stack) { Key = "terminal", Vertical = true, Fill = true };
        stack.With(
            Line("fn", "FN TERMINATE_INTRUDERS (REF)"),
            new UiNode(UiKind.Divider) { Key = "divider1" },
            // tg: AntagInfoGlitch.tsx:32
            Line("init", Span(VariableStyle, "Initialize(" + antag_name + ")"), "16px", bold: true),
            // tg: AntagInfoGlitch.tsx:35
            Line("crime", Span(DangerStyle, "Bitrunning") + " is a crime. Your mission: " + Span(VariableStyle, "Eliminate")
                + " organic intruders to maintain the integrity of the system."));
        foreach (UiNode node in SpecificInfo(antag_name))
            stack.With(node);
        stack.With(
            new UiNode(UiKind.Divider) { Key = "divider2" },
            // tg: AntagInfoGlitch.tsx:44
            new UiNode(UiKind.StackItem) { Key = "objective" }.With(new UiNode(UiKind.Box) { Key = "objective", Opacity = 0.6, Text = "// " + explanation, Wrap = true }),
            Line("targets", "const " + Span(VariableStyle, "TARGETS") + " ="),
            Line("intruders", Span(VariableStyle, "system.") + Span(DangerStyle, "INTRUDERS")),
            Line("while", "while " + Span(VariableStyle, "TARGETS") + ".LIFE != " + Span(VariableStyle, "stat.") + "DEAD"),
            Line("kill", Span(VariableStyle, "action.") + Span(DangerStyle, "KILL()")),
            Line("call", "terminate_intruders([0x70cf4020])"));
        return c.Window(350, 450, null,
            new UiNode(UiKind.Section) { Key = "glitch", Scrollable = true, Fill = true }.With(stack));
    }

    // tg: AntagInfoGlitch.tsx:72  SpecificInfo
    private static IEnumerable<UiNode> SpecificInfo(string antag_name)
    {
        switch (antag_name)
        {
            case "Cyber Police":
                yield return Line("specific1", "To assist your task, your program has been loaded with cutting edge " + Span(VariableStyle, "martial arts") + " skills.");
                yield return Line("specific2", "Ranged weaponry is " + Span(DangerStyle, "forbidden") + ". Ballistic defense is frowned upon. Style is paramount.");
                break;
            case "Cyber Tac":
                yield return Line("specific1", "You are an advanced combat unit. You have been outfitted with " + Span(VariableStyle, "lethal weaponry") + ".");
                yield return Line("specific2", Span(DangerStyle, "Terminate") + " organic life at any cost.");
                break;
            case "NetGuardian Prime":
                yield return Line("specific1", Span(DangerStyle + ";font-size:16px", "ORGANIC LIFE MUST BE TERMINATED."));
                break;
        }
    }
}
