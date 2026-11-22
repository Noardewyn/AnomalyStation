using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LaunchpadRemote.tsx (the syndicate campaign G2,
// D316: the briefcase launchpad's remote). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LaunchpadRemote
{
    // tg: tgui/packages/tgui/interfaces/LaunchpadRemote.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        c.WindowTheme = "syndicate";
        UiNode content;
        if (!data.Truthy("has_pad"))
            content = new UiNode(UiKind.NoticeBox) { Text = "No Launchpad Connected" };
        else if (data.Truthy("pad_closed"))
            content = new UiNode(UiKind.NoticeBox) { Text = "Launchpad Closed" };
        else
            content = LaunchpadConsole.LaunchpadControl(c);
        return c.Window(340, 275, "Briefcase Launchpad Remote", content);
    }
}
