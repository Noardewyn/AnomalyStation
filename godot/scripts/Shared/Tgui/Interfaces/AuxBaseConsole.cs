using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AuxBaseConsole.tsx: the auxiliary base's management console,
// its launch tab (the shuttle console's content, with the blind drop) and its turrets. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AuxBaseConsole
{
    // tg: AuxBaseConsole.tsx:33  enum TAB { Shuttle = 1, Aux }
    private const int ShuttleTab = 1;
    private const int AuxTab = 2;
    private const string TabKey = "tab";

    // tg: AuxBaseConsole.tsx:26
    private static string StatusColor(string status) => status switch
    {
        "ERROR" or "Disabled" => "bad",
        "Firing" => "average",
        "All Clear" => "good",
        _ => "bad",
    };

    // tg: AuxBaseConsole.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int tab = c.UseState(TabKey, ShuttleTab);
        string type = data.Str("type");
        bool blindDrop = data.Truthy("blind_drop");
        int turrets = data.Get("turrets").Items.Count;

        var root = new UiNode(UiKind.Section) { Key = "root", Scrollable = turrets > 0 }.With(
            new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:shuttle",
                    Icon = "list",
                    Text = type == "shuttle" ? "Shuttle Launch" : "Base Launch",
                    Selected = tab == ShuttleTab,
                    OnClick = () => c.SetState(TabKey, ShuttleTab),
                },
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:turrets",
                    Icon = "list",
                    Text = "Turrets (" + turrets + ")",
                    Selected = tab == AuxTab,
                    OnClick = () => c.SetState(TabKey, AuxTab),
                }));
        if (tab == ShuttleTab)
            root.With(ShuttleConsole.Content(c, type, blindDrop));
        if (tab == AuxTab)
            root.With(Content(c));
        return c.Window(turrets > 0 ? 620 : 350, turrets > 0 ? 310 : 260, null, root);
    }

    // tg: AuxBaseConsole.tsx:80
    private static UiNode Content(TguiContext c)
    {
        var turrets = c.Data.Get("turrets").Items.ToList();
        var section = new UiNode(UiKind.Section)
        {
            Key = "turretControl",
            Fill = true,
            Scrollable = true,
            Title = "Turret Control",
            Buttons = turrets.Count > 0 ? new UiNode(UiKind.Button) { Key = "power", Icon = "power-off", Text = "Toggle Power", OnClick = () => c.Act("turrets_power") } : null,
        };
        if (turrets.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No connected turrets" });

        var table = new UiNode(UiKind.Table) { Key = "turrets" }.With(
            new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Key = "h-unit", Text = "Unit" },
                new UiNode(UiKind.TableCell) { Key = "h-condition", Text = "Condition" },
                new UiNode(UiKind.TableCell) { Key = "h-status", Text = "Status" },
                new UiNode(UiKind.TableCell) { Key = "h-direction", Text = "Direction" },
                new UiNode(UiKind.TableCell) { Key = "h-distance", Text = "Distance" },
                new UiNode(UiKind.TableCell) { Key = "h-power", Text = "Power" }));
        foreach (UiValue turret in turrets)
        {
            string reference = turret.Str("ref");
            string status = turret.Str("status");
            table.With(new UiNode(UiKind.TableRow) { Key = "turret:" + reference }.With(
                new UiNode(UiKind.TableCell) { Key = "unit", Bold = true, Text = turret.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "condition", Text = TguiFormat.JsNumber(turret.Num("integrity")) + "%" },
                new UiNode(UiKind.TableCell) { Key = "status", Color = StatusColor(status), Text = status },
                new UiNode(UiKind.TableCell) { Key = "direction", Text = turret.Str("direction") },
                new UiNode(UiKind.TableCell) { Key = "distance", Text = TguiFormat.JsNumber(turret.Num("distance")) + "m" },
                new UiNode(UiKind.TableCell) { Key = "power" }.With(new UiNode(UiKind.Button)
                {
                    Key = "toggle",
                    Icon = "power-off",
                    Text = "Toggle",
                    OnClick = () => c.Act("single_turret_power", ("single_turret_power", reference)),
                })));
        }
        return section.With(table);
    }
}
