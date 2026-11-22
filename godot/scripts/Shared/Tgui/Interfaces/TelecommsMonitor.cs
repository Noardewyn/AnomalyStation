using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TelecommsMonitor.tsx (M9 slice D2: the telecomms
// monitoring console). Engine-free (compiled into the tests too). The machines' pictures are tg's "tcomms" sprite sheet
// (TguiSpritesheets, DV44). The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TelecommsMonitor
{
    // tg: TelecommsMonitor.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double screen = data.Num("screen");
        string statusMessage = data.Str("statusMessage");

        return c.Window(350, 500, "T-comms Monitoring Console", new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(!string.IsNullOrEmpty(statusMessage) ? new UiNode(UiKind.NoticeBox) { Key = "status", Text = statusMessage } : null),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(screen == 0 ? MainScreen(c) : screen == 1 ? MachineScreen(c) : null)));
    }

    // tg: TelecommsMonitor.tsx:59
    private static UiNode MainScreen(TguiContext c)
    {
        UiValue data = c.Data;
        string network = data.Str("network");
        IReadOnlyList<UiValue> machines = data.Get("machines")?.Items ?? Array.Empty<UiValue>();
        string networkId = c.UseState("networkId", network);

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section) { Fill = true }.With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                    {
                        Key = "networkId",
                        Fluid = true,
                        TextValue = networkId,
                        Tooltip = "Enter Network ID...",
                        OnInput = v => c.SetState("networkId", v),
                        OnEnter = () => c.Act("probe", ("id", c.UseState("networkId", network))),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "probe", Text = "Probe Network", OnClick = () => c.Act("probe", ("id", networkId)) })))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(MachineList(c, "main", "Detected Network Entities",
                new UiNode(UiKind.Button) { Key = "flush", Icon = "trash", Color = "red", Tooltip = "Flush Buffer", Disabled = machines.Count == 0, OnClick = () => c.Act("flush") },
                machines)));
    }

    // tg: TelecommsMonitor.tsx:103
    private static UiNode MachineScreen(TguiContext c)
    {
        UiValue machine = c.Data.Get("machine");
        IReadOnlyList<UiValue> linkedMachines = machine?.Get("linkedMachines")?.Items ?? Array.Empty<UiValue>();

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section)
            {
                Title = "Entity Information",
                Buttons = new UiNode(UiKind.Button) { Key = "home", Icon = "home", Text = "Main Menu", OnClick = () => c.Act("home") },
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Network", Text = machine.Str("network") },
                new UiNode(UiKind.LabeledItem) { Label = "Network ID", Text = machine.Str("id") },
                new UiNode(UiKind.LabeledItem) { Label = "Network Entity", Text = TguiFormat.ToTitleCase(machine.Str("name")) }))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(MachineList(c, "linked", "Linked Entities", null, linkedMachines)));
    }

    // tg: TelecommsMonitor.tsx:148
    private static UiNode MachineList(TguiContext c, string list, string title, UiNode buttons, IReadOnlyList<UiValue> machines)
    {
        bool searching = c.UseState(list + ":searching", false);
        string search = c.UseState(list + ":search", "");

        var sortedMachines = machines.Where(m => m.Str("id").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var titleButtons = new UiNode(UiKind.Box).With(
            new UiNode(UiKind.Button) { Key = "search", Icon = "magnifying-glass", Selected = searching, Disabled = machines.Count == 0, Tooltip = "Search by ID", OnClick = () => c.SetState(list + ":searching", !searching) },
            buttons);

        UiNode body;
        if (sortedMachines.Count > 0)
        {
            body = new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    sortedMachines.Select((m, index) => new UiNode(UiKind.StackItem) { Key = "machine:" + index }.With(new UiNode(UiKind.Button)
                    {
                        Key = "select",
                        Fluid = true,
                        Tooltip = TguiFormat.ToTitleCase(m.Str("name")),
                        OnClick = () => c.Act("view", ("id", m.Str("id"))),
                    }.With(new UiNode(UiKind.Stack) { Fill = true, Align = "center" }.With(
                        // tg: <Box className={classes(['tcomms32x32', machine.icon])} />
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "icon", DmIcon = TguiSpritesheets.Tcomms32, DmIconState = m.Str("icon") }),
                        new UiNode(UiKind.StackItem) { Grow = 1, Text = m.Str("id") })))))),
                searching ? new UiNode(UiKind.StackItem) { Height = 2 }.With(new UiNode(UiKind.Input)
                {
                    Key = "searchInput",
                    Fluid = true,
                    AutoFocus = true,
                    TextValue = search,
                    Tooltip = "Enter machine ID...",
                    OnInput = v => c.SetState(list + ":search", v),
                }) : null);
        }
        else
            body = new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No machines connected!" };

        return new UiNode(UiKind.Section) { Scrollable = true, Fill = true, Title = title, Buttons = titleButtons }.With(body);
    }
}
