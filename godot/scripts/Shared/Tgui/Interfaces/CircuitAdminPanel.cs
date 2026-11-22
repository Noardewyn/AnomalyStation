using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CircuitAdminPanel.tsx (gaps CI1, D339, D341: View All
// Circuits, the console's circuits command). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CircuitAdminPanel
{
    // tg: CircuitAdminPanel.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        var rows = new List<UiNode>
        {
            new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Bold = true, Text = "Circuit name" },
                new UiNode(UiKind.TableCell) { Key = "creator", Bold = true, Text = "Creator" },
                new UiNode(UiKind.TableCell) { Key = "actions", Bold = true, Text = "Actions" }),
        };
        foreach (UiValue circuit in data.Arr("circuits"))
        {
            string @ref = circuit.Str("ref");
            UiNode Act(string action, string text) => new UiNode(UiKind.Button) { Key = action, Text = text, OnClick = () => c.Act(action, ("circuit", @ref)) };
            var actions = new List<UiNode>
            {
                Act("follow_circuit", "Follow"),
                Act("open_circuit", "Open"),
                Act("vv_circuit", "VV"),
                Act("save_circuit", "Save"),
                Act("duplicate_circuit", "Duplicate"),
            };
            if (circuit.Truthy("has_inserter"))
                actions.Add(Act("open_player_panel", "Player Panel"));
            rows.Add(new UiNode(UiKind.TableRow) { Key = @ref }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = circuit.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "creator", Text = circuit.Str("creator") },
                new UiNode(UiKind.TableCell) { Key = "actions" }.With(new UiNode(UiKind.Stack) { Key = "actions" }.With(
                    actions.Select(a => new UiNode(UiKind.StackItem) { Key = a.Key }.With(a))))));
        }

        // <Window title="Circuit Admin Panel" width={1200} height={500}>
        return c.Window(1200, 500, "Circuit Admin Panel", new UiNode(UiKind.Stack) { Key = "root", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top" }.With(new UiNode(UiKind.Stack) { Key = "top" }.With(
                new UiNode(UiKind.StackItem) { Key = "spacer", Grow = 1 },
                new UiNode(UiKind.StackItem) { Key = "sound" }.With(new UiNode(UiKind.Button)
                {
                    Key = "disable_circuit_sound",
                    Text = "Disable all circuit sound emitters",
                    OnClick = () => c.Act("disable_circuit_sound"),
                }))),
            new UiNode(UiKind.StackItem) { Key = "table" }.With(new UiNode(UiKind.Table) { Key = "circuits" }.With(rows))));
    }
}
