using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AntagInfoSentient.jsx (syndicate G3c, D316): the
// intelligence potion's creature's info window, from /datum/antagonist/sentient_creature's ui_static_data (sentient_creature.dm:33).
// tgui-core's BlockQuote is a box. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AntagInfoSentient
{
    // tg: AntagInfoSentient.jsx:6
    public static UiNode Render(TguiContext c)
    {
        string enslaved_to = c.Data.Str("enslaved_to", null);
        bool holographic = c.Data.Truthy("holographic");
        string p_them = c.Data.Str("p_them", null);
        string p_their = c.Data.Str("p_their", null);
        // tg: AntagInfoSentient.jsx:15  {!!enslaved_to && ...}{!!holographic && ...}
        string quote = "All at once it makes sense: you know what you are and who you are! Self awareness is yours!"
            + (!string.IsNullOrEmpty(enslaved_to)
                ? " You are grateful to be self aware and owe " + enslaved_to + " a great debt. Serve " + enslaved_to + ", and assist " + p_them + " in completing " + p_their + " goals at any cost."
                : "")
            + (holographic
                ? " You also become depressingly aware that you are not a real creature, but instead a holoform. Your existence is limited to the parameters of the holodeck."
                : "");
        // tg: AntagInfoSentient.jsx:10  <Window width={400} height={400} theme="neutral"><Section fill><Stack vertical fill textAlign="center">
        c.WindowTheme = "neutral";
        return c.Window(400, 400, null,
            new UiNode(UiKind.Section) { Key = "info", Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Fill = true, TextAlign = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "title" }.With(
                        new UiNode(UiKind.Box) { FontSize = "20px", Text = "You are a sentient creature!" }),
                    new UiNode(UiKind.StackItem) { Key = "quote" }.With(
                        new UiNode(UiKind.Box) { Text = quote }))));
    }
}
