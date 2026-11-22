using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoChangeling.tsx (gaps K1, D327): the
// changeling's info window, from /datum/antagonist/changeling's ui_data (changeling.dm:1017) and the antagonist's ui_static_data: the
// intro with the absorbed DNA count and the objectives (with the button that evolves new directives), the abilities' guide, the
// betrayal warning, the stolen memories with their dropdown and the stolen antagonist information. The guide's paragraphs mix inline
// styled words (tg's span styles), so each is the chat's HTML text (UiKind.Html, which reads inline color and font-weight); tg's
// Dimmer is the box it covers. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoChangeling
{
    // tg: AntagInfoChangeling.tsx:21-54  hivestyle, absorbstyle, revivestyle, transformstyle, storestyle, hivemindstyle, fallenstyle
    private const string HiveStyle = "font-weight:bold;color:yellow";
    private const string AbsorbStyle = "color:red;font-weight:bold";
    private const string ReviveStyle = "color:lightblue;font-weight:bold";
    private const string TransformStyle = "color:orange;font-weight:bold";
    private const string StoreStyle = "color:lightgreen;font-weight:bold";
    private const string HivemindStyle = "color:violet;font-weight:bold";
    private const string FallenStyle = "color:black;font-weight:bold";

    private static string Span(string style, string text) => "<span style='" + style + "'>" + text + "</span>";

    // tg: AntagInfoChangeling.tsx:71
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: AntagInfoChangeling.tsx:73  <Window width={720} height={750}>
        return c.Window(720, 750, null,
            new UiNode(UiKind.Stack) { Key = "changeling", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "intro", Height = 16 }.With(IntroductionSection(c, data)),
                new UiNode(UiKind.StackItem) { Key = "abilities", Grow = 4 }.With(AbilitiesSection(data)),
                BetrayalWarning(),
                new UiNode(UiKind.StackItem) { Key = "stolen", Grow = 3 }.With(
                    new UiNode(UiKind.Stack) { Key = "stolen", Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "memories", Grow = 1 }.With(MemoriesSection(c, data)),
                        new UiNode(UiKind.StackItem) { Key = "patterns", Grow = 1 }.With(VictimPatternsSection(data))))));
    }

    // tg: AntagInfoChangeling.tsx:105  IntroductionSection
    private static UiNode IntroductionSection(TguiContext c, UiValue data)
    {
        string true_name = data.Str("true_name");
        string hive_name = data.Str("hive_name");
        var dna = new UiNode(UiKind.Button)
        {
            Key = "dna", Icon = "dna", Tooltip = "Absorbed DNA", Color = "purple", Text = TguiFormat.JsNumber(data.Num("absorbed_dna")),
        };
        return new UiNode(UiKind.Section) { Key = "intro", Title = "Intro", Fill = true, Scrollable = true, Buttons = dna }.With(
            new UiNode(UiKind.Stack) { Key = "intro", Vertical = true, Fill = true }.With(
                // tg: AntagInfoChangeling.tsx:133  You are {true_name} from the <span style={hivestyle}> {hive_name}</span>.
                new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Stack) { Key = "name" }.With(
                    new UiNode(UiKind.Box) { Key = "you", FontSize = "25px", Text = "You are " + true_name + " from the" },
                    new UiNode(UiKind.Box) { Key = "hive", FontSize = "25px", Bold = true, Color = "yellow", Text = " " + hive_name },
                    new UiNode(UiKind.Box) { Key = "dot", FontSize = "25px", Text = "." })),
                // tg: AntagInfoChangeling.tsx:137  <ObjectivePrintout objectives={objectives} objectiveFollowup={<ReplaceObjectivesButton .../>} />
                new UiNode(UiKind.StackItem) { Key = "objectives" }.With(Objectives.ObjectivePrintout(data.Arr("objectives"),
                    objectiveFollowup: Objectives.ReplaceObjectivesButton(c, data.Truthy("can_change_objective"), "Evolve New Directives", "green")))));
    }

    private static UiNode Paragraph(string key, string html) =>
        new UiNode(UiKind.StackItem) { Key = key, Grow = 1 }.With(new UiNode(UiKind.Html) { Key = key, TextValue = Span("color:#a0a0a0", html) });

    // tg: AntagInfoChangeling.tsx:153  AbilitiesSection
    private static UiNode AbilitiesSection(UiValue data)
    {
        string true_name = data.Str("true_name");
        return new UiNode(UiKind.Section) { Key = "abilities", Title = "Abilities", Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "abilities", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "column1", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "column1", Fill = true, Vertical = true }.With(
                    // tg: AntagInfoChangeling.tsx:161
                    Paragraph("absorb", "Your " + Span(AbsorbStyle, " Absorb DNA") + " ability allows you to steal the DNA and memories of a victim. The "
                        + Span(AbsorbStyle, " Extract DNA Sting") + " ability also steals the DNA of a victim, and is undetectable, but does not grant you their memories or speech patterns."),
                    new UiNode(UiKind.Divider) { Key = "divider1" },
                    // tg: AntagInfoChangeling.tsx:169
                    Paragraph("revive", "Your " + Span(ReviveStyle, " Reviving Stasis") + " ability allows you to revive. It means nothing short of a complete body destruction can stop you! "
                        + "Obviously, this is loud and so should not be done in front of people you are not planning on silencing."))),
                new UiNode(UiKind.Divider) { Key = "divider2" },
                new UiNode(UiKind.StackItem) { Key = "column2", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "column2", Fill = true, Vertical = true }.With(
                    // tg: AntagInfoChangeling.tsx:182
                    Paragraph("transform", "Your " + Span(TransformStyle, " Transform") + " ability allows you to change into the form of those you have collected DNA from, "
                        + "lethally and nonlethally. It will also mimic (NOT REAL CLOTHING) the clothing they were wearing for every slot you have open."),
                    new UiNode(UiKind.Divider) { Key = "divider3" },
                    // tg: AntagInfoChangeling.tsx:190
                    Paragraph("store", "The " + Span(StoreStyle, " Cellular Emporium") + " is where you purchase more abilities beyond your starting kit. You have 10 genetic points "
                        + "to spend on abilities and you are able to readapt after absorbing a body, refunding your points for different kits."))),
                new UiNode(UiKind.Divider) { Key = "divider4" },
                new UiNode(UiKind.StackItem) { Key = "column3", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "column3", Fill = true, Vertical = true }.With(
                    // tg: AntagInfoChangeling.tsx:203
                    Paragraph("chemicals", "All abilities require using " + Span(HivemindStyle, "chemicals") + ", you can see how much you have with the HUD on the left side of the "
                        + "screen. You may also hover your cursor over it to see the maximum amount of chemicals you can hold. This number can increase by "
                        + Span(AbsorbStyle, " absorbing") + " other Changelings."),
                    new UiNode(UiKind.Divider) { Key = "divider5" },
                    // tg: AntagInfoChangeling.tsx:213
                    Paragraph("hivemind", "All Changelings, regardless of origin, are linked together by the " + Span(HivemindStyle, "hivemind") + ". You may communicate to other "
                        + "Changelings under your mental alias, " + Span(HivemindStyle, true_name) + ", by starting a message with " + Span(HivemindStyle, ":g")
                        + ". Work together, and you will bring the station to new heights of terror.")))));
    }

    // tg: AntagInfoChangeling.tsx:229  BetrayalWarning
    private static UiNode BetrayalWarning()
    {
        return new UiNode(UiKind.NoticeBox) { Key = "betrayal", Color = "danger" }.With(new UiNode(UiKind.Html)
        {
            Key = "betrayal",
            TextValue = "Other Changelings are strong allies, but some Changelings may betray you. Changelings grow in power greatly by absorbing their kind, and getting "
                + "absorbed by another Changeling will leave you as a " + Span(FallenStyle, "Fallen Changeling") + ". There is no greater humiliation.",
        });
    }

    // tg: AntagInfoChangeling.tsx:241  MemoriesSection
    private static UiNode MemoriesSection(TguiContext c, UiValue data)
    {
        IReadOnlyList<UiValue> memories = data.Arr("memories");
        // tg: AntagInfoChangeling.tsx:244  const [selectedMemory, setSelectedMemory] = useState((!!memories && memories[0]) || null)
        string selectedName = c.UseState<string>("selectedMemory", memories.Count > 0 ? memories[0].Str("name") : null);
        UiValue selectedMemory = memories.FirstOrDefault(m => m.Str("name") == selectedName);
        var info = new UiNode(UiKind.Button)
        {
            Key = "info", Icon = "info",
            Tooltip = "Absorbing targets allows you to collect their memories. They should help you impersonate your target!",
        };
        var section = new UiNode(UiKind.Section) { Key = "memories", Title = "Stolen Memories", Fill = true, Scrollable = memories.Count > 0, Buttons = info };
        // tg: AntagInfoChangeling.tsx:271  (!!memories && !memories.length && <Dimmer fontSize="20px">Absorb a victim first!</Dimmer>) || ...
        if (memories.Count == 0)
            return section.With(new UiNode(UiKind.Box) { Key = "dimmer", FontSize = "20px", TextAlign = "center", Text = "Absorb a victim first!" });
        return section.With(new UiNode(UiKind.Stack) { Key = "memories", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "dropdown" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "memory", Width = "100%",
                Options = memories.Select(m => m.Str("name")).ToArray(),
                TextValue = selectedMemory?.Str("name"),
                OnSelected = name => c.SetState("selectedMemory", name),
            }),
            new UiNode(UiKind.StackItem) { Key = "story" }.With(new UiNode(UiKind.Box) { Key = "story", Wrap = true, Text = selectedMemory?.Str("story") ?? "" })));
    }

    // tg: AntagInfoChangeling.tsx:285  VictimPatternsSection
    private static UiNode VictimPatternsSection(UiValue data)
    {
        string stolen_antag_info = data.Str("stolen_antag_info", null);
        var section = new UiNode(UiKind.Section) { Key = "patterns", Title = "Additional Stolen Information", Fill = true, Scrollable = !string.IsNullOrEmpty(stolen_antag_info) };
        if (string.IsNullOrEmpty(stolen_antag_info))
            return section.With(new UiNode(UiKind.Box) { Key = "dimmer", FontSize = "20px", TextAlign = "center", Text = "Absorb a victim first!" });
        return section.With(new UiNode(UiKind.Box) { Key = "info", Wrap = true, Text = stolen_antag_info });
    }
}
