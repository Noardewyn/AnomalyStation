using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoRevolution.tsx (gaps V1, D327): the
// revolutionary's and the head revolutionary's info window, from /datum/antagonist/rev's ui_static_data (revolution.dm:85, :92): the
// leader's or the comrade's guidance, the heads of staff to kill or exile, and for a leader the heads' code words and the conversion
// guideline field (set_conversion_objective). The placeholder's example guideline is picked at random as tg's is; tgui's italic items
// are plain boxes. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoRevolution
{
    // tg: AntagInfoRevolution.tsx:26  formatCodes: takes [a, b, c] and returns "a, b, and c"
    private static string FormatCodes(IReadOnlyList<UiValue> text)
    {
        var codes = text.Select(t => t.AsStr()).ToList();
        if (codes.Count == 0)
            return "";
        if (codes.Count == 1)
            return codes[0];
        if (codes.Count == 2)
            return codes[0] + " and " + codes[1];
        return string.Join(", ", codes.Take(codes.Count - 1)) + ", and " + codes[^1];
    }

    // tg: AntagInfoRevolution.tsx:35  these are just placeholders they don't actually get passed to the player
    private static readonly string[] JokeObjectives =
    {
        "Annoy the heads of staff as much as possible without harming them.",
        "Disguise as the heads of staff and confuse the crew.",
        "Make revolutionary artwork and vandalize the station with it.",
        "Mass produce picket signs and wave them around the station.",
        "Start a cult worshipping the proletariat.",
    };

    private static readonly string[] PlaceholderObjectives =
    {
        "Sabotage the station's power grid.",
        "Steal the Head of Personnel's pet dog.",
        "Turn the Head of Security's team against them.",
        "Waste the station's funds and resources.",
        "Blow up the Research Director with a bomb.",
        "Bribe the Quartermaster to self-exile.",
        "Capture anyone with a mindshield and have it surgically removed.",
        "Create an army of mechs to march on the bridge.",
        "Cut off the air supply to the brig.",
        "Equip your leaders with the best gear you can find.",
        "Find the remaining heads of staff and eliminate them.",
        "Follow your leaders as closely as possible.",
        "Gather weapons and armor.",
        "Go out in a blaze of glory.",
        "Lie low until we have enough numbers to strike.",
        "Occupy the bridge and hold it until the heads of staff surrender.",
        "Protect your leaders at all costs, even if it means sacrifice.",
        "Protest in front of the bridge.",
        "Rescue other revolutionaries from the brig.",
        "Rush the Captain and take his hat.",
        "Subvert the AI to betray the heads of staff.",
        "Take the Chief Medical Officer hostage.",
        "Throw the Chief Engineer into the Supermatter.",
        "Trick the heads of staff into exiling themselves.",
        "Wait for the emergency shuttle, then strike in the chaos.",
    };

    // tg: AntagInfoRevolution.tsx:35  randomObjectivePlaceholderText (tg rerolls it every 30 seconds for a leader)
    private static string RandomObjectivePlaceholderText()
    {
        if (Random.Shared.NextDouble() <= 0.01)
            return JokeObjectives[Random.Shared.Next(JokeObjectives.Length)];
        return PlaceholderObjectives[Random.Shared.Next(PlaceholderObjectives.Length)];
    }

    // the placeholder of the open window: tg keeps it in the component's state, rerolled every 30 seconds
    private static string s_placeholder = RandomObjectivePlaceholderText();
    private static DateTime s_placeholderAt = DateTime.UtcNow;

    // tg: AntagInfoRevolution.tsx:79
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool leader = data.Truthy("leader");
        IReadOnlyList<UiValue> code_phrases = data.Arr("code_phrases");
        IReadOnlyList<UiValue> code_responses = data.Arr("code_responses");
        IReadOnlyList<UiValue> heads = data.Arr("heads");
        bool lone_wolf = data.Truthy("lone_wolf");
        string conversion_objective = data.Str("conversion_objective");
        int max_length = (int)data.Num("conversion_objective_max_length");

        if (leader && (DateTime.UtcNow - s_placeholderAt).TotalSeconds >= 30)
        {
            s_placeholder = RandomObjectivePlaceholderText();
            s_placeholderAt = DateTime.UtcNow;
        }

        var stack = new UiNode(UiKind.Stack) { Key = "stack", Vertical = true };
        // tg: AntagInfoRevolution.tsx:107  Viva la Revolution!
        stack.With(new UiNode(UiKind.StackItem) { Key = "title" }.With(
            new UiNode(UiKind.Box) { Key = "title", Color = "red", FontSize = "24px", TextAlign = "center", Text = "Viva la Revolution!" }));

        var guide = new UiNode(UiKind.Stack) { Key = "guide", Vertical = true };
        if (leader)
        {
            guide.With(Line("wolf", lone_wolf
                ? "- You are a lone leader of the revolution. It is recommended to act swiftly and decisively - when the revolution is sufficiently large, more leaders will be promoted."
                : "- There are multiple leaders of the revolution. It is recommended to work together and establish a plan BEFORE you start converting the crew - being outed early can prove extremely detrimental."),
                Line("flash", "- Convert the crew to your cause with a flash - any flash will work."),
                Line("mindshield", "- Mindshields will prevent conversion. You can identify them via the flashing blue border around their job icon."),
                Line("lost", "- The revolution is lost if you and your fellow leaders are all killed or exiled. Do not let that happen!"));
        }
        else
        {
            guide.With(Line("help", "- Help your cause. Do not harm your fellow freedom fighters."),
                Line("icons", "- You can identify your comrades by the red \"R\" icons, and your leaders by the blue \"R\" icons."),
                Line("lost", "- The revolution is lost if all of your leaders are killed or exiled. Do not let that happen!"));
        }
        stack.With(new UiNode(UiKind.StackItem) { Key = "guide" }.With(guide));

        // tg: AntagInfoRevolution.tsx:155  the heads of staff
        if (heads.Count > 0)
        {
            stack.With(new UiNode(UiKind.Divider) { Key = "div_heads" });
            var list = new UiNode(UiKind.Stack) { Key = "heads", Vertical = true };
            list.With(new UiNode(UiKind.StackItem) { Key = "heads_title" }.With(
                new UiNode(UiKind.Box) { Key = "heads_title", FontSize = "16px", TextAlign = "center", Text = "You must kill or exile the heads of staff:" }));
            for (int i = 0; i < heads.Count; i++)
                list.With(Line("head-" + i, "- " + heads[i].Str("name") + ", the " + heads[i].Str("role")));
            stack.With(new UiNode(UiKind.StackItem) { Key = "heads" }.With(list));
        }

        if (leader)
        {
            // tg: AntagInfoRevolution.tsx:173  the code words
            if (code_phrases.Count > 0 && code_responses.Count > 0)
            {
                stack.With(new UiNode(UiKind.Divider) { Key = "div_codes" });
                stack.With(new UiNode(UiKind.StackItem) { Key = "codes" }.With(new UiNode(UiKind.Stack) { Key = "codes", Vertical = true }.With(
                    Line("codes_intro", "To identify your fellow leaders, use the following code:"),
                    new UiNode(UiKind.StackItem) { Key = "phrases" }.With(new UiNode(UiKind.Box) { Key = "phrases", Color = "blue", Text = "Phrases: " + FormatCodes(code_phrases), Wrap = true }),
                    new UiNode(UiKind.StackItem) { Key = "responses" }.With(new UiNode(UiKind.Box) { Key = "responses", Color = "red", Text = "Responses: " + FormatCodes(code_responses), Wrap = true }))));
                stack.With(new UiNode(UiKind.Divider) { Key = "div_codes2" });
            }
            // tg: AntagInfoRevolution.tsx:195  the conversion guideline
            stack.With(new UiNode(UiKind.Divider) { Key = "div_guideline" });
            stack.With(new UiNode(UiKind.StackItem) { Key = "guideline" }.With(new UiNode(UiKind.Stack) { Key = "guideline", Vertical = true }.With(
                Line("guideline_intro", "Set a conversion guideline: This text is shown only to newly converted revolutionaries."),
                new UiNode(UiKind.StackItem) { Key = "guideline_input" }.With(new UiNode(UiKind.Input)
                {
                    Key = "guideline", Fluid = true, Tooltip = "Ex: " + s_placeholder, MaxLength = max_length, TextValue = conversion_objective ?? "",
                    OnCommit = value => c.Act("set_conversion_objective", ("conversion_objective", value)),
                }))));
        }

        // tg: AntagInfoRevolution.tsx:104  <Window width={400} height={leader ? 500 : 400}><Section scrollable fill>
        return c.Window(400, leader ? 500 : 400, null,
            new UiNode(UiKind.Section) { Key = "info", Scrollable = true, Fill = true }.With(stack));
    }

    private static UiNode Line(string key, string text) =>
        new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Box) { Key = key, Text = text, Wrap = true });
}
