using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Fax.tsx (gaps S3, D298: the fax machines). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Fax
{
    // tg: Fax.tsx:41
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool syndicate = data.Truthy("syndicate_network");
        bool has_paper = data.Truthy("has_paper");
        // tg: Fax.tsx:44  the visible faxes (and not the syndicate's unless this one is on its network), sorted by name
        List<UiValue> faxes = data.Arr("faxes")
            .Where(f => f.Truthy("visible") && (syndicate || !f.Truthy("syndicate_network")))
            .OrderBy(f => f.Str("fax_name"), StringComparer.Ordinal)
            .ToList();
        List<UiValue> special_networks = data.Arr("special_faxes").Where(f => syndicate || !f.Truthy("emag_needed")).ToList();

        var sendButtons = new List<UiNode>();
        foreach (UiValue special in special_networks)
        {
            string id = special.Str("fax_id"), name = special.Str("fax_name");
            sendButtons.Add(new UiNode(UiKind.Button)
            {
                Key = "special:" + id,
                Tooltip = name,
                Disabled = !has_paper,
                Color = special.Str("color"),
                Text = name,
                OnClick = () => c.Act("send_special", ("id", id), ("name", name)),
            });
        }
        foreach (UiValue fax in faxes)
        {
            string id = fax.Str("fax_id"), name = fax.Str("fax_name");
            sendButtons.Add(new UiNode(UiKind.Button)
            {
                Key = "send:" + id,
                Tooltip = name,
                Disabled = !has_paper,
                Color = fax.Truthy("syndicate_network") ? "red" : "blue",
                Text = name,
                OnClick = () => c.Act("send", ("id", id), ("name", name)),
            });
        }

        UiValue history = data.Get("fax_history");
        IReadOnlyList<UiValue> historyRows = data.Arr("fax_history");

        // tg: Fax.tsx:58  <Window width={340} height={540}> <Window.Content scrollable>
        return c.Window(340, 540, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.Section) { Title = "About Fax" }.With(new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Network name" }.With(new UiNode(UiKind.Box) { Text = data.Str("fax_name") }),
                        new UiNode(UiKind.LabeledItem) { Label = "Network ID" }.With(new UiNode(UiKind.Box) { Text = data.Str("fax_id") }),
                        new UiNode(UiKind.LabeledItem) { Label = "Visible to Network" }.With(new UiNode(UiKind.Box) { Text = data.Truthy("visible") ? "true" : "false" }))),
                    new UiNode(UiKind.Section)
                    {
                        Title = "Paper",
                        Buttons = new UiNode(UiKind.Button) { Key = "remove", Text = "Remove", Disabled = !has_paper, OnClick = () => c.Act("remove") },
                    }.With(new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Paper" }.With(has_paper
                            ? new UiNode(UiKind.Box) { Color = "green", Text = "Paper in tray" }
                            : new UiNode(UiKind.Box) { Color = "red", Text = "No paper" }))),
                    new UiNode(UiKind.Section) { Title = "Send" }.With(faxes.Count == 0 && special_networks.Count == 0
                        ? new UiNode(UiKind.Box) { Text = "The fax couldn't detect any other faxes on the network." }
                        : new UiNode(UiKind.Stack) { Wrap = true }.With(sendButtons.Select(b => new UiNode(UiKind.StackItem).With(b)))),
                    new UiNode(UiKind.Section)
                    {
                        Title = "History",
                        Buttons = new UiNode(UiKind.Button) { Key = "history_clear", Text = "Clear", Disabled = history == null || history.Kind == UiValueKind.Null, OnClick = () => c.Act("history_clear") },
                    }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(historyRows.Select((h, i) => new UiNode(UiKind.StackItem) { Key = "history:" + i }.With(
                        new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Color = h.Str("history_type") == "Send" ? "green" : "red", Text = h.Str("history_type") }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = $"{h.Str("history_fax_name")} - {h.Str("history_time")}" }))))))))));
    }
}
