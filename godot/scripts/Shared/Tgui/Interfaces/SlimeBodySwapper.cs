using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SlimeBodySwapper.jsx (gaps SP2c, D349: the slimeperson's body swap).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SlimeBodySwapper
{
    // tg: SlimeBodySwapper.jsx:8
    private static string StatusColor(string status) => status switch
    {
        "Dead" => "bad",
        "Unconscious" => "average",
        "Conscious" => "good",
        _ => null,
    };

    // tg: SlimeBodySwapper.jsx:14
    private static string OccupiedText(string occupied) => occupied switch
    {
        "owner" => "You Are Here",
        "stranger" => "Occupied",
        "available" => "Swap",
        _ => null,
    };

    // tg: SlimeBodySwapper.jsx:20
    private static UiNode BodyEntry(TguiContext c, UiValue body) =>
        new UiNode(UiKind.Section)
        {
            Key = "body-" + body.Str("name"),
            Title = body.Str("name"),
            Color = body.Str("htmlcolor"),
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "swap-" + body.Str("name"),
                Text = OccupiedText(body.Str("occupied")),
                Selected = body.Str("occupied") == "owner",
                Color = body.Str("occupied") == "stranger" ? "bad" : null,
                OnClick = () => c.Act("swap", ("ref", body.Str("ref"))),
            },
        }.With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status", Bold = true, Color = StatusColor(body.Str("status")) }
                    .With(new UiNode(UiKind.Box) { Text = body.Str("status") }),
                new UiNode(UiKind.LabeledItem) { Key = "jelly", Label = "Jelly" }
                    .With(new UiNode(UiKind.Box) { Text = TguiFormat.ToFixed(body.Num("exoticblood"), 0) }),
                new UiNode(UiKind.LabeledItem) { Key = "location", Label = "Location" }
                    .With(new UiNode(UiKind.Box) { Text = body.Str("area") })));

    // tg: SlimeBodySwapper.jsx:52
    public static UiNode Render(TguiContext c) =>
        c.Window(400, 400, null,
            new UiNode(UiKind.Section).With(c.Data.Arr("bodies").Select(b => BodyEntry(c, b)).ToArray()));
}
