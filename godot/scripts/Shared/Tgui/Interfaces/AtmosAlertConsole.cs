using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AtmosAlertConsole.tsx (the engineering package E6,
// D287: the atmospheric alert consoles). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AtmosAlertConsole
{
    // tg: AtmosAlertConsole.tsx:10
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> priority = c.Data.Arr("priority");
        IReadOnlyList<UiValue> minor = c.Data.Arr("minor");

        // <ul>: each <li> a line with its bullet, the alerts' buttons in theirs
        var section = new UiNode(UiKind.Section) { Key = "alarms", Title = "Alarms" };
        if (priority.Count == 0)
            section.With(new UiNode(UiKind.Box) { Key = "no-priority", Color = "good", Text = "• No priority alerts" });
        foreach (UiValue alert in priority)
            section.With(Alert(c, "priority:", alert.AsStr(), "bad"));
        if (minor.Count == 0)
            section.With(new UiNode(UiKind.Box) { Key = "no-minor", Color = "good", Text = "• No minor alerts" });
        foreach (UiValue alert in minor)
            section.With(Alert(c, "minor:", alert.AsStr(), "average"));

        // <Window.Content scrollable>: the content in the window's own area
        return c.Window(350, 300, null, section);
    }

    // tg: AtmosAlertConsole.tsx:25  <li key={alert}><Button icon="times" content={alert} color onClick={() => act('clear', { zone: alert })} /></li>
    private static UiNode Alert(TguiContext c, string list, string zone, string color) => new UiNode(UiKind.Box) { Key = list + zone }.With(
        new UiNode(UiKind.Button) { Key = "clear", Icon = "times", Text = zone, Color = color, OnClick = () => c.Act("clear", ("zone", zone)) });
}
