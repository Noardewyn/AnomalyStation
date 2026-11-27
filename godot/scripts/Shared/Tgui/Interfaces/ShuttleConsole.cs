using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ShuttleConsole.jsx. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ShuttleConsole
{
    // tg: ShuttleConsole.jsx:66
    private static string StatusColor(string status) => status switch
    {
        "In Transit" => "good",
        "Idle" or "Igniting" or "Recharging" => "average",
        _ => "bad",
    };

    // tg: ShuttleConsole.jsx:15
    public static UiNode Render(TguiContext c)
    {
        bool authorizationRequired = c.Data.Truthy("authorization_required");
        UiNode content = Content(c, "shuttle", false); // tg: the window's type prop, "shuttle" by default

        if (authorizationRequired)
        {
            content.Children.Insert(0, new UiNode(UiKind.Modal) { Key = "lockedModal" }.With(
                new UiNode(UiKind.Box) { Key = "lockedText", Color = "bad", FontSize = "28px", TextAlign = "center", Icon = "minus-circle", Text = " SHUTTLE LOCKED" },
                new UiNode(UiKind.Button) { Key = "request", Color = "bad", Icon = "arrow-circle-right", Text = "Request Authorization", OnClick = () => c.Act("request") }));
        }

        return c.Window(350, 230, null, content);
    }

    // tg: ShuttleConsole.jsx:76  ShuttleConsoleContent (the auxiliary base's console shows it too, with its blind drop)
    public static UiNode Content(TguiContext c, string type, bool blindDrop)
    {
        UiValue data = c.Data;
        bool locked = data.Truthy("locked");
        bool authorizationRequired = data.Truthy("authorization_required");
        string status = data.Str("status");
        string destination = data.Str("destination");
        var locations = data.Get("locations").Items.ToList();
        string destinationName = locations.FirstOrDefault(l => l.Str("id") == destination)?.Str("name");

        UiNode destinationNode = locations.Count == 0
            ? new UiNode(UiKind.Box) { Key = "destination", Color = "bad", Text = "Not Available" }
            : locations.Count == 1
                ? new UiNode(UiKind.Box) { Key = "destination", Color = "average", Text = destinationName ?? "" }
                : new UiNode(UiKind.Dropdown)
                {
                    Key = "destination",
                    Width = "240px",
                    Disabled = locked || authorizationRequired,
                    Options = locations.Select(l => l.Str("name")).ToArray(),
                    TextValue = destinationName ?? "Select a Destination",
                    OnSelected = name => c.Act("set_destination", ("destination", locations.FirstOrDefault(l => l.Str("name") == name)?.Str("id"))),
                };

        // tg: ShuttleConsole.jsx:110  the base's blind drop, while it has no landing zone
        UiNode blindDropButton = type != "shuttle" && locations.Count == 0 && blindDrop
            ? new UiNode(UiKind.Button) { Key = "blindDrop", Color = "bad", Icon = "exclamation-triangle", Disabled = authorizationRequired || !blindDrop, Text = "Blind Drop", OnClick = () => c.Act("random") }
            : null;

        return new UiNode(UiKind.Section) { Key = "root" }.With(
            new UiNode(UiKind.Box) { Key = "timer", Bold = true, FontSize = "26px", TextAlign = "center", Text = data.Str("timer_str") ?? "00:00" },
            new UiNode(UiKind.Box) { Key = "status", TextAlign = "center", FontSize = "14px" }.With(
                new UiNode(UiKind.Box) { Key = "label", Bold = true, Text = "STATUS:" },
                new UiNode(UiKind.Box) { Key = "value", Color = StatusColor(status), Text = status ?? "Not Available" }),
            new UiNode(UiKind.Section) { Key = "controls", Title = type == "shuttle" ? "Shuttle Controls" : "Base Launch Controls" }.With(
                new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                    new UiNode(UiKind.LabeledItem) { Key = "location", Label = "Location" }.With(new UiNode(UiKind.Box) { Key = "here", Text = data.Str("docked_location") ?? "Not Available" }),
                    new UiNode(UiKind.LabeledItem) { Key = "destinationItem", Label = "Destination", Buttons = blindDropButton }.With(destinationNode)),
                new UiNode(UiKind.Button)
                {
                    Key = "depart",
                    Fluid = true,
                    Icon = "arrow-up",
                    TextAlign = "center",
                    Text = "Depart",
                    Disabled = destinationName == null || locked || authorizationRequired,
                    OnClick = () => c.Act("move", ("shuttle_id", destination)),
                }));
    }
}
