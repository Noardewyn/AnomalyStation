using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/EmergencyShuttleConsole.tsx. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class EmergencyShuttleConsole
{
    // tg: EmergencyShuttleConsole.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool enabled = data.Truthy("enabled");
        bool emagged = data.Truthy("emagged");
        bool enginesStarted = data.Truthy("engines_started");

        var authorizations = new UiNode(UiKind.Section) { Key = "authorizations", Title = "Authorizations" };
        authorizations.Buttons = new UiNode(UiKind.Box)
        {
            Key = "remaining",
            Bold = true,
            Color = emagged ? "bad" : "good",
            Text = emagged ? "ERROR" : "Remaining: " + data.Str("authorizations_remaining"),
        };
        int shown = 0;
        foreach (UiValue authorization in data.Get("authorizations").Items)
        {
            authorizations.Children.Add(new UiNode(UiKind.Box)
            {
                Key = "auth:" + authorization.Str("name"),
                Bold = true,
                FontSize = "16px",
                Text = authorization.Str("name") + " (" + authorization.Str("job") + ")",
            });
            shown++;
        }
        if (shown == 0)
            authorizations.Children.Add(new UiNode(UiKind.Box) { Key = "none", Bold = true, TextAlign = "center", FontSize = "16px", Color = "average", Text = "No Active Authorizations" });

        var launch = new UiNode(UiKind.Section) { Key = "launch", Title = "Early Launch Authorization" };
        launch.Buttons = new UiNode(UiKind.Button) { Key = "abort", Color = "bad", Disabled = !enabled, Icon = "times", Text = "Repeal All", OnClick = () => c.Act("abort") };
        launch.Children.Add(new UiNode(UiKind.Stack) { Key = "buttons" }.With(
            new UiNode(UiKind.StackItem) { Key = "a", Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "authorize", Color = "good", Disabled = !enabled, Fluid = true, Icon = "exclamation-triangle", Text = "AUTHORIZE", OnClick = () => c.Act("authorize") }),
            new UiNode(UiKind.StackItem) { Key = "r", Grow = 1 }.With(new UiNode(UiKind.Button) { Key = "repeal", Disabled = !enabled, Fluid = true, Icon = "minus", Text = "REPEAL", OnClick = () => c.Act("repeal") })));
        launch.Children.Add(authorizations);

        return c.Window(400, 350, null, new UiNode(UiKind.Section) { Key = "root" }.With(
            new UiNode(UiKind.Box) { Key = "timer", Bold = true, FontSize = "40px", TextAlign = "center", Text = data.Str("timer_str") },
            new UiNode(UiKind.Box) { Key = "engines", TextAlign = "center", FontSize = "16px" }.With(
                new UiNode(UiKind.Box) { Key = "label", Bold = true, Text = "ENGINES:" },
                new UiNode(UiKind.Box) { Key = "state", Color = enginesStarted ? "good" : "average", Text = enginesStarted ? " Online" : " Idle" }),
            launch));
    }
}
