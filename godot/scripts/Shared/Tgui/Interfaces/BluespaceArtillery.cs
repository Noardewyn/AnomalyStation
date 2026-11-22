using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BluespaceArtillery.tsx (gaps R1: the bluespace artillery
// control console). The window reads the console's ui_data (bsa.dm:322: connected, notice, unlocked, visualizing, target) and sends
// build, visualize, unvisualize, fire and recalibrate. The kit has no margin and no line height (the FIRE button's lineHeight="46px", the
// Box mt={1}, the style margin auto are not drawn). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BluespaceArtillery
{
    // tg: BluespaceArtillery.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool connected = data.Truthy("connected");
        string notice = data.Str("notice");
        bool unlocked = data.Truthy("unlocked");
        bool visualizing = data.Truthy("visualizing");
        string target = data.Str("target");

        // tg: BluespaceArtillery.tsx:26  <Window width={400} height={220}>
        return c.Window(400, 220, null,
            // tg: BluespaceArtillery.tsx:28  {!!notice && <NoticeBox>{notice}</NoticeBox>}
            !string.IsNullOrEmpty(notice) ? new UiNode(UiKind.NoticeBox) { Key = "notice", Text = notice } : null,
            connected ? Connected(c, unlocked, target) : Maintenance(c, visualizing));
    }

    // tg: BluespaceArtillery.tsx:30-71  (the connected cannon: its target and the fire button)
    private static UiNode Connected(TguiContext c, bool unlocked, string target)
    {
        return new UiNode(UiKind.Fragment).With(
            // tg: BluespaceArtillery.tsx:31
            new UiNode(UiKind.Section)
            {
                Key = "target",
                Title = "Target",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "recalibrate",
                    Icon = "crosshairs",
                    Disabled = !unlocked,
                    OnClick = () => c.Act("recalibrate"),
                },
            }.With(
                // tg: BluespaceArtillery.tsx:41
                new UiNode(UiKind.Box)
                {
                    Key = "target-name",
                    Color = !string.IsNullOrEmpty(target) ? "average" : "bad",
                    FontSize = "25px",
                    Text = !string.IsNullOrEmpty(target) ? target : "No Target Set",
                }),
            // tg: BluespaceArtillery.tsx:45
            new UiNode(UiKind.Section) { Key = "firing" }.With(
                unlocked
                    // tg: BluespaceArtillery.tsx:47
                    ? new UiNode(UiKind.Box).With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "fire",
                            Fluid = true,
                            Text = "FIRE",
                            Color = "bad",
                            Disabled = string.IsNullOrEmpty(target),
                            FontSize = "30px",
                            TextAlign = "center",
                            OnClick = () => c.Act("fire"),
                        })
                    // tg: BluespaceArtillery.tsx:60
                    : new UiNode(UiKind.Fragment).With(
                        new UiNode(UiKind.Box) { Key = "locked", Color = "bad", FontSize = "18px", Text = "Bluespace artillery is currently locked." },
                        new UiNode(UiKind.Box)
                        {
                            Key = "authorization",
                            Wrap = true,
                            Text = "Awaiting authorization via keycard reader from at minimum two station heads.",
                        })));
    }

    // tg: BluespaceArtillery.tsx:73-92  (no cannon yet: the deployment)
    private static UiNode Maintenance(TguiContext c, bool visualizing)
    {
        return new UiNode(UiKind.Section) { Key = "maintenance" }.With(
            new UiNode(UiKind.LabeledList).With(
                // tg: BluespaceArtillery.tsx:75
                new UiNode(UiKind.LabeledItem) { Label = "Maintenance" }.With(
                    visualizing
                        ? new UiNode(UiKind.Fragment).With(
                            new UiNode(UiKind.Button) { Key = "build", Color = "good", Text = "Confirm Position", OnClick = () => c.Act("build") },
                            new UiNode(UiKind.Button) { Key = "unvisualize", Color = "bad", Text = "Cancel", OnClick = () => c.Act("unvisualize") })
                        : new UiNode(UiKind.Button) { Key = "visualize", Icon = "wrench", Text = "Begin Deployment", OnClick = () => c.Act("visualize") })));
    }
}
