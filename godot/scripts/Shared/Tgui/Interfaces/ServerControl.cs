using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ServerControl.tsx (gaps RD2, D333: the R&D server
// control console): the research servers with their status tooltips and the switch that halts one, the R&D consoles with their
// locations and locks, and the research history. tg puts each server's and console's cell and button straight into the table after
// an empty header row; here each is one striped row of the two. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ServerControl
{
    // tg: ServerControl.tsx:42
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("server_connected"))
        {
            return c.Window(575, 450, null, new UiNode(UiKind.NoticeBox)
            {
                Key = "disconnected",
                Color = "danger",
                TextAlign = "center",
                Text = "Not connected to a Server. Please sync one using a multitool.",
            });
        }

        UiValue servers = data.Get("servers");
        UiValue consoles = data.Get("consoles");
        IReadOnlyList<UiValue> logs = data.Arr("logs");

        var content = new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true };

        if (servers == null || servers.IsNull)
            content.With(new UiNode(UiKind.NoticeBox) { Key = "noServers", Color = "info", Text = "No servers found." });
        else
        {
            var table = new UiNode(UiKind.Table) { Key = "servers", TextAlign = "center" }.With(
                new UiNode(UiKind.TableRow) { Key = "head" }.With(new UiNode(UiKind.TableCell) { Bold = true, Text = "Research Servers" }));
            foreach (UiValue server in servers.Items)
            {
                bool disabled = server.Truthy("server_disabled");
                string @ref = server.Str("server_ref");
                table.With(new UiNode(UiKind.TableRow) { Key = "server:" + @ref, Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = " " + server.Str("server_name") },
                    new UiNode(UiKind.TableCell) { Key = "switch" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "lockdown",
                        Tooltip = server.Str("server_details"),
                        Color = disabled ? "bad" : "good",
                        Text = disabled ? "Offline" : "Online",
                        Fluid = true,
                        TextAlign = "center",
                        OnClick = () => c.Act("lockdown_server", ("selected_server", @ref)),
                    })));
            }
            content.With(new UiNode(UiKind.Section) { Key = "serverSection" }.With(table));
        }

        if (consoles == null || consoles.IsNull)
            content.With(new UiNode(UiKind.NoticeBox) { Key = "noConsoles", Color = "info", Text = "No consoles found." });
        else
        {
            var table = new UiNode(UiKind.Table) { Key = "consoles", TextAlign = "center" }.With(
                new UiNode(UiKind.TableRow) { Key = "head" }.With(new UiNode(UiKind.TableCell) { Bold = true, Text = "Research Consoles" }));
            foreach (UiValue console in consoles.Items)
            {
                bool locked = console.Truthy("console_locked");
                string @ref = console.Str("console_ref");
                table.With(new UiNode(UiKind.TableRow) { Key = "console:" + @ref, Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = " " + console.Str("console_name") + " - Location: " + console.Str("console_location") + " " },
                    new UiNode(UiKind.TableCell) { Key = "lock" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "lockConsole",
                        Color = locked ? "bad" : "good",
                        Text = locked ? "LOCKED" : "UNLOCKED",
                        Fluid = true,
                        TextAlign = "center",
                        OnClick = () => c.Act("lock_console", ("selected_console", @ref)),
                    })));
            }
            content.With(new UiNode(UiKind.Section) { Key = "consoleSection", Align = "right" }.With(table));
        }

        var history = new UiNode(UiKind.Collapsible) { Key = "history", Title = "Research History" };
        if (logs.Count == 0)
            history.With(new UiNode(UiKind.NoticeBox) { Key = "noHistory", Color = "info", Text = "No history found." });
        else
        {
            var table = new UiNode(UiKind.Table) { Key = "logs" }.With(
                new UiNode(UiKind.TableRow) { Key = "head" }.With(
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Research Name" },
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Cost" },
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Researcher Name" },
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Console Location" }));
            int index = 0;
            foreach (UiValue log in logs)
            {
                table.With(new UiNode(UiKind.TableRow) { Key = "log:" + index++ + ":" + log.Str("node_name"), Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = log.Str("node_name") },
                    new UiNode(UiKind.TableCell) { Key = "cost", Text = log.Str("node_cost") },
                    new UiNode(UiKind.TableCell) { Key = "researcher", Text = log.Str("node_researcher") },
                    new UiNode(UiKind.TableCell) { Key = "location", Text = log.Str("node_research_location") }));
            }
            history.With(new UiNode(UiKind.Section) { Key = "historySection" }.With(table));
        }
        content.With(history);

        // <Window width={575} height={400}><Window.Content scrollable>
        return c.Window(575, 400, null, content);
    }
}
