using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TramControl.tsx (the tram's controls in the car,
// tram_controls.dm's ui_data and ui_static_data; it sends send). As the other windows of the kit: a Dimmer is a notice over the
// window's top (the kit has no overlay), and the line between the platform circles is left out.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TramControl
{
    // tg: TramControl.tsx:28
    private static readonly Dictionary<string, string> Department2Color = new()
    {
        ["Arrivals"] = "black", ["Service"] = "olive", ["Command"] = "blue", ["Security"] = "red", ["Medical"] = "teal",
        ["Engineering"] = "yellow", ["Cargo"] = "brown", ["Science"] = "purple", ["Departures"] = "white",
        ["Reception"] = "white", ["Botany"] = "olive", ["Chemistry"] = "teal", ["Processing"] = "brown", ["Xenobiology"] = "purple",
        ["Ordnance"] = "yellow", ["Office"] = "red", ["Dormitories"] = "black",
    };

    // tg: TramControl.tsx:50
    private static readonly Dictionary<string, string> Color2Blurb = new()
    {
        ["blue"] = "This is the tram's current location.",
        ["green"] = "This is the selected destination.",
        ["transparent"] = "Click to set destination.",
    };

    // tg: TramControl.tsx:66
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool broken = data.Truthy("broken");
        bool moving = data.Truthy("moving");
        IReadOnlyList<UiValue> destinations = data.Arr("destinations");
        string tram_location = data.Str("tram_location");
        int transitIndex = c.UseState("transitIndex", 1);

        UiNode body;
        if (broken)
            body = BrokenTramDimmer();
        else
        {
            var row = new UiNode(UiKind.Stack) { Key = "dests", Fill = true };
            for (int i = 0; i < destinations.Count; i++)
                row.Children.Add(new UiNode(UiKind.StackItem) { Key = destinations[i].Str("name") }.With(Destination(c, destinations, i, transitIndex, tram_location)));
            UiValue target = transitIndex < destinations.Count ? destinations[transitIndex] : null;
            body = new UiNode(UiKind.Section) { Key = "section", Fill = true }.With(
                moving ? MovingTramDimmer(tram_location) : null,
                new UiNode(UiKind.Stack) { Key = "stack", Align = "center", Fill = true, FontSize = "16px", Justify = "space-around", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "title", Text = "Nanotrasen Transit System" },
                    new UiNode(UiKind.StackItem) { Key = "row" }.With(row),
                    new UiNode(UiKind.StackItem) { Key = "send" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "send",
                        Disabled = target == null || tram_location == target.Str("name"),
                        OnClick = () => c.Act("send", ("destination", target.Num("id"))),
                        Text = "Send Tram",
                    })));
        }
        return c.Window(600, 300, "Tram Controls", body);
    }

    // tg: TramControl.tsx:124
    private static UiNode BrokenTramDimmer()
        => new UiNode(UiKind.NoticeBox) { Key = "broken", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Color = "red", Icon = "triangle-exclamation", Size = 10 }),
                new UiNode(UiKind.StackItem) { Key = "text", FontSize = "14px", Color = "red", Text = "Check Tram Controller!" }));

    // tg: TramControl.tsx:139
    private static UiNode MovingTramDimmer(string tram_location)
        => new UiNode(UiKind.NoticeBox) { Key = "moving", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = "sync-alt", Color = "green", Size = 11 }),
                new UiNode(UiKind.StackItem) { Key = "text", FontSize = "14px", Color = "green", Text = "The tram is travelling to " + tram_location + "!" }));

    // tg: TramControl.tsx:157
    private static UiNode Destination(TguiContext c, IReadOnlyList<UiValue> destinations, int index, int transitIndex, string tram_location)
    {
        UiValue dest = destinations[index];
        string color = string.IsNullOrEmpty(tram_location) ? "bad"
            : dest.Str("name") == tram_location ? "blue" : transitIndex == index ? "green" : "transparent";
        var stack = new UiNode(UiKind.Stack) { Key = "dest", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "circle" }.With(new UiNode(UiKind.Button)
            {
                Key = "circle",
                Circular = true,
                Width = "4.9em",
                Height = "4.9em",
                Color = color,
                Tooltip = Color2Blurb.GetValueOrDefault(color),
                OnClick = () => c.SetState("transitIndex", index),
                Icon = "circle-o",
                FontSize = "59px",
            }));
        IReadOnlyList<KeyValuePair<string, UiValue>> icons = dest.Obj("dest_icons");
        if (icons.Count > 0)
        {
            var deps = new UiNode(UiKind.Stack) { Key = "deps" };
            foreach (KeyValuePair<string, UiValue> dep in icons)
                deps.Children.Add(new UiNode(UiKind.StackItem) { Key = dep.Key }.With(new UiNode(UiKind.Button)
                {
                    Key = "dep",
                    Color = Department2Color.GetValueOrDefault(dep.Key),
                    Icon = dep.Value.AsStr(),
                    Tooltip = dep.Key,
                }));
            stack.Children.Add(new UiNode(UiKind.StackItem) { Key = "icons" }.With(deps));
        }
        return stack;
    }
}
