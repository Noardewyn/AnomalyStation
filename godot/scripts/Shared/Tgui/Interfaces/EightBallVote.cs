using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/EightBallVote.tsx (syndicate G4a, D316: the haunted
// magic eightball's ghost vote).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class EightBallVote
{
    // tg: tgui/packages/tgui/interfaces/EightBallVote.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool shaking = data.Truthy("shaking");

        // tg: EightBallVote.tsx:32  const idealHeight = shaking ? 265 : 70;
        return c.Window(300, shaking ? 265 : 70, null,
            !shaking
                ? new UiNode(UiKind.NoticeBox) { Color = "red", TextAlign = "center", Text = "No question is currently being asked." }
                : new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.NoticeBox) { Color = "green", TextAlign = "center", Text = "A question is currently being asked!" },
                    Question(c)));
    }

    // tg: EightBallVote.tsx:53
    private static UiNode Question(TguiContext c)
    {
        UiValue data = c.Data;
        bool shaking = data.Truthy("shaking");
        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Box) { Bold = true, TextAlign = "center", FontSize = "16px", Text = "\"" + data.Str("question") + "\"" },
            new UiNode(UiKind.Divider),
            new UiNode(UiKind.Stack).With(
                data.Arr("answers").Select(answer =>
                {
                    string name = answer.Str("answer");
                    return new UiNode(UiKind.StackItem) { Key = name, Grow = 1 }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "vote",
                            Fluid = true,
                            Bold = true,
                            Disabled = !shaking,
                            Selected = answer.Truthy("selected"),
                            FontSize = "16px",
                            TextAlign = "center",
                            Text = TguiFormat.ToTitleCase(name),
                            OnClick = () => c.Act("vote", ("answer", name)),
                        },
                        new UiNode(UiKind.Box) { Bold = true, TextAlign = "center", FontSize = "30px", Text = answer.Num("amount").ToString() });
                })));
    }
}
