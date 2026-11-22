using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Intellicard.jsx (silicons SI4, D326). tgui-core's
// BlockQuote is a box.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Intellicard
{
    private static readonly (string, double, double)[] IntegrityRanges =
    {
        ("good", 70, double.PositiveInfinity),
        ("average", 50, 70),
        ("bad", double.NegativeInfinity, 50),
    };

    // tg: Intellicard.jsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string name = data.Str("name");
        bool isDead = data.Truthy("isDead"), isBraindead = data.Truthy("isBraindead");
        bool wireless = data.Truthy("wireless"), radio = data.Truthy("radio"), wiping = data.Truthy("wiping");
        IReadOnlyList<UiValue> laws = data.Get("laws")?.Items ?? Array.Empty<UiValue>();
        bool offline = isDead || isBraindead;
        bool named = !string.IsNullOrEmpty(name);
        return c.Window(500, 500, null,
            new UiNode(UiKind.Section)
            {
                Title = named ? name : "Empty Card",
                Buttons = named ? new UiNode(UiKind.Button) { Key = "wipe", Icon = "trash", Text = wiping ? "Stop Wiping" : "Wipe", Disabled = isDead, OnClick = () => c.Act("wipe") } : null,
            }.With(named
                ? new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Status", Color = offline ? "bad" : "good", Text = offline ? "Offline" : "Operation" },
                    new UiNode(UiKind.LabeledItem) { Label = "Software Integrity" }.With(
                        new UiNode(UiKind.ProgressBar) { Value = data.Num("health"), MinValue = 0, MaxValue = 100, Ranges = IntegrityRanges }),
                    new UiNode(UiKind.LabeledItem) { Label = "Settings" }.With(
                        new UiNode(UiKind.Button) { Key = "wireless", Icon = "signal", Text = "Wireless Activity", Selected = wireless, OnClick = () => c.Act("wireless") },
                        new UiNode(UiKind.Button) { Key = "radio", Icon = "microphone", Text = "Subspace Radio", Selected = radio, OnClick = () => c.Act("radio") }),
                    new UiNode(UiKind.LabeledItem) { Label = "Laws" }.With(
                        laws.Select(law => new UiNode(UiKind.Box) { Key = law.AsStr(), Text = law.AsStr() })))
                : null));
    }
}
