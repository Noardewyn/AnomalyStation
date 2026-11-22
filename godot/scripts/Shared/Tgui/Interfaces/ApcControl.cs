using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ApcControl.tsx (the engineering package E6, D287: the
// power flow control console). Engine-free (compiled into the tests too). Margins and the spinning cog are not drawn.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ApcControl
{
    // tg: ApcControl.tsx:20  enum Screen { ControlPanel = 1, LogView }
    private const int ControlPanelScreen = 1;
    private const int LogViewScreen = 2;

    private const string TabKey = "tabIndex";
    private const string SortKey = "sortByState";

    private sealed record Apc(string Name, bool Operating, UiValue Charge, string Load, double Charging, double Eqp, double Lgt, double Env, UiValue Ref, string Id);

    // tg: ApcControl.tsx:52
    public static UiNode Render(TguiContext c)
    {
        bool authenticated = c.Data.Truthy("authenticated");
        return c.Window(550, 500, "APC Controller", authenticated ? LoggedIn(c) : LoggedOut(c));
    }

    // tg: ApcControl.tsx:65
    private static UiNode LoggedOut(TguiContext c)
    {
        bool emagged = c.Data.Truthy("emagged");
        string text = emagged ? "Open" : "Log In";

        return new UiNode(UiKind.Section) { Key = "loggedout", Fill = true }.With(
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Stack) { Fill = true, Vertical = true, Justify = "center", Align = "center" }.With(
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Key = "bolt", Icon = "bolt-lightning", Color = "yellow", Size = 8 }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "version", Bold = true, Text = "Zeus™ Controller Version 0.19" }),
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "copyright", Color = "label", Text = "Copyright 2526 Nanotrasen" }))),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box)
                {
                    Key = "disclaimer",
                    Color = "#2a2a2a",
                    Wrap = true,
                    Text = "Nanotrasen and its affiliates do not endorse this product. Risk of serious bodily injury or death is inherent in the use "
                        + "of any device that generates electricity. Nanotrasen is not responsible for any damages caused by the use of this product.",
                }),
                new UiNode(UiKind.StackItem).With(
                    // tg: ApcControl.tsx:90  <NoticeBox style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    new UiNode(UiKind.NoticeBox) { Key = "notice" }.With(
                        new UiNode(UiKind.Stack) { Justify = "space-between", Align = "center" }.With(
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "Authorized personnel only." }),
                            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                            {
                                Key = "login",
                                Icon = "sign-in-alt",
                                Color = emagged ? "" : "good",
                                Text = text,
                                OnClick = () => c.Act("log-in"),
                            }))))));
    }

    // tg: ApcControl.tsx:115
    private static UiNode LoggedIn(TguiContext c)
    {
        bool restoring = c.Data.Truthy("restoring");
        int tabIndex = c.UseState(TabKey, ControlPanelScreen);

        var stack = new UiNode(UiKind.Stack) { Key = "loggedin", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:apcs",
                    Text = "APC Control Panel",
                    Selected = tabIndex == ControlPanelScreen,
                    OnClick = () =>
                    {
                        c.SetState(TabKey, ControlPanelScreen);
                        c.Act("check-apcs");
                    },
                },
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:logs",
                    Text = "Log View Panel",
                    Selected = tabIndex == LogViewScreen,
                    OnClick = () =>
                    {
                        c.SetState(TabKey, LogViewScreen);
                        c.Act("check-logs");
                    },
                })));
        // tg: ApcControl.tsx:146  <Dimmer fontSize="32px"><Icon name="cog" spin />{' Resetting...'}</Dimmer>
        if (restoring)
            stack.With(new UiNode(UiKind.Modal) { Key = "restoring" }.With(new UiNode(UiKind.Box) { Key = "resetting", Icon = "cog", FontSize = "32px", Text = " Resetting..." }));

        var body = new UiNode(UiKind.StackItem) { Grow = 1 };
        if (tabIndex == ControlPanelScreen)
        {
            body.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Height = 3 }.With(new UiNode(UiKind.Section).With(ControlPanel(c))),
                new UiNode(UiKind.Divider),
                new UiNode(UiKind.StackItem) { Grow = 4 }.With(new UiNode(UiKind.Section) { Key = "apcs", Fill = true, Scrollable = true }.With(ApcControlScene(c)))));
        }
        if (tabIndex == LogViewScreen)
            body.With(new UiNode(UiKind.Section) { Key = "logs", Scrollable = true }.With(new UiNode(UiKind.Box) { Height = 34 }.With(LogPanel(c))));
        return stack.With(body);
    }

    // tg: ApcControl.tsx:182
    private static UiNode ControlPanel(TguiContext c)
    {
        bool emagged = c.Data.Truthy("emagged");
        bool logging = c.Data.Truthy("logging");
        string sortByField = c.UseState(SortKey, "name");

        UiNode SortBox(string field, string label) => new UiNode(UiKind.Button)
        {
            Key = "sort:" + field,
            Checkbox = sortByField == field,
            Text = label,
            OnClick = () => c.SetState(SortKey, field),
        };

        var right = new UiNode(UiKind.StackItem);
        if (emagged)
        {
            right.With(
                new UiNode(UiKind.Button) { Key = "toggle-logs", Color = logging ? "bad" : "good", Text = logging ? "Stop Logging" : "Restore Logging", OnClick = () => c.Act("toggle-logs") },
                new UiNode(UiKind.Button) { Key = "restore", Text = "Reset Console", OnClick = () => c.Act("restore-console") });
        }
        right.With(new UiNode(UiKind.Button) { Key = "logout", Icon = "sign-out-alt", Color = "bad", Text = "Log Out", OnClick = () => c.Act("log-out") });

        return new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem).With(
                new UiNode(UiKind.Box) { Color = "label", Text = "Sort by:" },
                SortBox("name", "Name"),
                SortBox("charge", "Charge"),
                SortBox("draw", "Draw")),
            new UiNode(UiKind.StackItem),
            right);
    }

    // es-toolkit's compareValues: a < b, a > b, else equal (NaN included)
    private static int Compare(double a, double b) => a < b ? -1 : a > b ? 1 : 0;

    // tg: ApcControl.tsx:242
    private static UiNode ApcControlScene(TguiContext c)
    {
        string sortByField = c.UseState(SortKey, "name");

        List<Apc> withIndex = c.Data.Arr("apcs").Select((apc, idx) => new Apc(
            apc.Str("name"), apc.Truthy("operating"), apc.Field("charge"), apc.Str("load"), apc.Num("charging"),
            apc.Num("eqp"), apc.Num("lgt"), apc.Num("env"), apc.Field("ref"), apc.Str("name") + idx)).ToList();

        // tg: ApcControl.tsx:252  sortBy (stable): -charge is NaN for "NOCELL"
        static double Charge(Apc apc) => apc.Charge?.Kind == UiValueKind.Number ? apc.Charge.Number : double.NaN;
        Comparison<Apc> compare = sortByField switch
        {
            "name" => (a, b) => Math.Sign(string.CompareOrdinal(a.Name, b.Name)),
            "charge" => (a, b) => Compare(-Charge(a), -Charge(b)),
            "draw" => (a, b) => Compare(-PowerMonitor.PowerRank(a.Load), -PowerMonitor.PowerRank(b.Load)) is var r && r != 0 ? r
                : Compare(-PowerMonitor.ParseFloat(a.Load), -PowerMonitor.ParseFloat(b.Load)),
            _ => null,
        };
        List<Apc> sorted = compare == null ? new List<Apc>() : withIndex.OrderBy(a => a, Comparer<Apc>.Create(compare)).ToList();

        var table = new UiNode(UiKind.Table) { Key = "apcs" }.With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = "On/Off" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Area" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Charge" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Draw", TextAlign = "right" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Eqp" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Lgt" },
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Env" }));
        foreach (Apc apc in sorted)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "apc:" + apc.Id, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "cell:breaker" }.With(new UiNode(UiKind.Button)
                {
                    Key = "breaker",
                    Icon = apc.Operating ? "power-off" : "times",
                    Color = apc.Operating ? "good" : "bad",
                    OnClick = () => c.Act("breaker", ("ref", apc.Ref)),
                }),
                new UiNode(UiKind.TableCell) { Key = "cell:name" }.With(new UiNode(UiKind.Button)
                {
                    Key = "access",
                    Fluid = true,
                    Color = "transparent",
                    Text = apc.Name,
                    OnClick = () => c.Act("access-apc", ("ref", apc.Ref)),
                }),
                PowerMonitor.AreaCharge(apc.Charging, Charge(apc)),
                new UiNode(UiKind.TableCell) { Key = "load", TextAlign = "right", Text = apc.Load },
                StatusCell(c, "equipment", apc.Eqp, apc),
                StatusCell(c, "lighting", apc.Lgt, apc),
                StatusCell(c, "environ", apc.Env, apc)));
        }
        return table;
    }

    // tg: ApcControl.tsx:330
    private static UiNode LogPanel(TguiContext c)
    {
        var panel = new UiNode(UiKind.Box) { Key = "logpanel" };
        IReadOnlyList<UiValue> logs = c.Data.Arr("logs");
        for (int idx = logs.Count - 1; idx >= 0; idx--)
        {
            string entry = logs[idx].Str("entry");
            int colonIndex = entry.LastIndexOf(':');
            // JavaScript's substring clamps a negative end to 0
            string timestamp = entry[..Math.Max(colonIndex, 0)];
            string message = entry[(colonIndex + 1)..];
            panel.With(new UiNode(UiKind.Box) { Key = "log:" + entry + idx, Candystripe = true }.With(
                new UiNode(UiKind.Box) { Key = "timestamp", Bold = true, Color = "label", Text = timestamp + ":" },
                new UiNode(UiKind.Box) { Key = "message", Text = message }));
        }
        return panel;
    }

    // tg: ApcControl.tsx:356  AreaStatusColorButton: bit 2 is the channel's power, bit 1 its auto mode
    private static UiNode StatusCell(TguiContext c, string target, double status, Apc apc)
    {
        bool power = ((int)status & 2) != 0;
        bool mode = ((int)status & 1) != 0;
        return new UiNode(UiKind.TableCell) { Key = "cell:" + target, TextAlign = "center" }.With(new UiNode(UiKind.Button)
        {
            Key = target,
            Icon = mode ? "sync" : "power-off",
            Color = power ? "good" : "bad",
            OnClick = () => c.Act("toggle-minor", ("type", target), ("value", StatusChange(status)), ("ref", apc.Ref)),
        });
    }

    // tg: ApcControl.tsx:378  mode flip power flip both flip: 0, 2, 3
    private static double StatusChange(double status) => status == 0 ? 2 : status == 2 ? 3 : 0;
}
