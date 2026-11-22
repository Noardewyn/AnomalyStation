using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ServerMonitor.jsx (M9 slice D2: the telecomms server
// monitoring console). Engine-free (compiled into the tests too). The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ServerMonitor
{
    // tg: ServerMonitor.jsx:147
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double screen = data.Num("screen");
        string error = data.Str("error");

        return c.Window(575, 400, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(error != "" ? new UiNode(UiKind.NoticeBox) { Key = "error", Text = error } : null),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(screen == 0 ? MainScreen(c) : screen == 1 ? ServerScreen(c) : null)));
    }

    // tg: ServerMonitor.jsx:17
    private static UiNode PacketInfo(TguiContext c, UiValue packet)
    {
        string job = packet.Str("job");
        return new UiNode(UiKind.StackItem) { Key = "packet:" + packet.Str("ref") }.With(
            new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Text = packet.Str("name") },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "delete", Icon = "trash", Color = "red", OnClick = () => c.Act("delete_packet", ("ref", packet.Str("ref"))) })),
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Data Type", Text = packet.Str("type") },
                new UiNode(UiKind.LabeledItem) { Label = "Source", Text = packet.Str("source") + (string.IsNullOrEmpty(job) ? "" : " (" + job + ")") },
                new UiNode(UiKind.LabeledItem) { Label = "Class", Text = packet.Str("race") },
                new UiNode(UiKind.LabeledItem) { Label = "Contents", Text = packet.Str("message") },
                new UiNode(UiKind.LabeledItem) { Label = "Language", Text = packet.Str("language") }),
            new UiNode(UiKind.Divider) { Rule = true });
    }

    // tg: ServerMonitor.jsx:47
    private static UiNode ServerScreen(TguiContext c)
    {
        UiValue server = c.Data.Get("server");
        double traffic = server.Num("traffic");
        IReadOnlyList<UiValue> packets = server.Get("packets")?.Items ?? Array.Empty<UiValue>();

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section)
            {
                Title = "Server Information",
                Buttons = new UiNode(UiKind.Button) { Key = "home", Icon = "home", Text = "Main Menu", OnClick = () => c.Act("return_home") },
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Network", Text = c.Data.Str("network") },
                new UiNode(UiKind.LabeledItem) { Label = "Server", Text = server.Str("name") },
                new UiNode(UiKind.LabeledItem)
                {
                    Label = "Total Recorded Traffic",
                    Text = traffic >= 1024 ? TguiFormat.JsNumber(traffic / 1024) + " TB" : TguiFormat.JsNumber(traffic) + " GB",
                }))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Stored Packets" }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(packets.Select(p => PacketInfo(c, p))))));
    }

    // tg: ServerMonitor.jsx:88
    private static UiNode MainScreen(TguiContext c)
    {
        UiValue data = c.Data;
        string network = data.Str("network");
        IReadOnlyList<UiValue> servers = data.Get("servers")?.Items ?? Array.Empty<UiValue>();
        string networkId = c.UseState("networkId", network);

        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Input)
                {
                    Key = "networkId",
                    TextValue = networkId,
                    Tooltip = "Network ID",
                    OnInput = v => c.SetState("networkId", v),
                    OnEnter = () => c.Act("scan_network", ("network_id", c.UseState("networkId", network))),
                },
                new UiNode(UiKind.Button) { Key = "scan", Text = "Scan", OnClick = () => c.Act("scan_network", ("network_id", networkId)) })),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Fill = true,
                Scrollable = true,
                Title = "Detected Telecommunication Servers",
                Buttons = new UiNode(UiKind.Button) { Key = "clear", Text = "Clear Buffer", Icon = "trash", Color = "red", Disabled = servers.Count == 0, OnClick = () => c.Act("clear_buffer") },
            }.With(new UiNode(UiKind.Table).With(
                new[]
                {
                    // tg: <Table.Row header>
                    new UiNode(UiKind.TableRow) { Key = "head" }.With(
                        new UiNode(UiKind.TableCell) { Bold = true, Text = "Address" },
                        new UiNode(UiKind.TableCell) { Bold = true, Text = "Identification String" },
                        new UiNode(UiKind.TableCell) { Bold = true, Text = "Name" }),
                }.Concat(servers.Select(s => new UiNode(UiKind.TableRow) { Key = "server:" + s.Str("ref") }.With(
                    new UiNode(UiKind.TableCell) { Text = s.Str("ref") },
                    new UiNode(UiKind.TableCell) { Text = s.Str("id") },
                    new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button) { Key = "view", Text = s.Str("name"), OnClick = () => c.Act("view_server", ("server", s.Str("ref"))) }))))))));
    }
}
