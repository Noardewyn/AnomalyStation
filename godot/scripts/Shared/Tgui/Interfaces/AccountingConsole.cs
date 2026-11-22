using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AccountingConsole (index.tsx, UsersScreen.tsx,
// AuditScreen.tsx, FakeWindow.tsx, FakeDesktopButton.tsx, FakeToolbarButton.tsx, Sort.tsx, helpers.ts, types.ts; gaps P4a, D327): the
// account lookup console's fake ntOS95 desktop: the paychecks, audit and Ian icons, their fake windows (the crew's accounts with the pay
// modifiers, paycheck advances and sorting; the audit log; Ian's picture), the doom message of a crashing market, and the toolbar. The
// window's Accounting__ styles (the white window content, the title bar's blue, the toolbar) are the theme's colours; tgui's Blink draws
// its text steadily (the kit has no blink, as ElevatorPanel). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AccountingConsole
{
    // tg: types.ts:34  enum SCREENS
    private const int ScreenNone = 0, ScreenUsers = 1, ScreenAudit = 2, ScreenIan = 3;

    // tg: types.ts:41  enum SORTING
    private const int SortingAscending = 0, SortingDescending = 1, SortingNone = 2;

    // tg: helpers.ts:1
    private static readonly string[] DoomMessages =
    {
        "BUY GOLD!",
        "BUY LOW, SELL HIGH!",
        "INVEST IN CRYPTO!",
        "SELL EVERYTHING!",
        "THE ECONOMY IS COLLAPSING!",
        "THE ECONOMY IS RUINED!",
        "THE MARKET IS CRASHING!",
        "THE STATION IS GOING BANKRUPT!",
    };

    // Used when the economy is crashing to get a random funny message.
    // tg: helpers.ts:13
    private static string GetRandomDoomMessage() => DoomMessages[System.Random.Shared.Next(DoomMessages.Length)];

    // tg: index.tsx:12
    public static UiNode Render(TguiContext c)
    {
        string station_time = c.Data.Str("station_time", "00:00");
        string pic_file_format = c.Data.Str("pic_file_format", "png");
        bool young_ian = c.Data.Truthy("young_ian");
        int screenmode = c.UseState("screen", ScreenNone);

        string ianFileName = young_ian ? "Ian's first birthday." + pic_file_format : "Ian." + pic_file_format;

        c.WindowTheme = "ntOS95";
        UiNode screen = screenmode switch
        {
            ScreenUsers => new UiNode(UiKind.StackItem) { Key = "users-window", Grow = 1 }.With(FakeWindow(c, "Crew Account Summary", UsersScreen(c))),
            ScreenAudit => new UiNode(UiKind.StackItem) { Key = "audit-window", Grow = 1 }.With(FakeWindow(c, "Audit Log", AuditScreen(c))),
            ScreenIan => new UiNode(UiKind.StackItem) { Key = "ian-window" }.With(FakeWindow(c, ianFileName,
                new UiNode(UiKind.Box) { Key = "ian", DmIcon = "icons/mob/simple/pets.dmi", DmIconState = young_ian ? "puppy" : "corgi", Width = "300px", Height = "300px" })),
            _ => null,
        };

        return c.Window(680, 440, null, new UiNode(UiKind.Stack) { Key = "desktop", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "screen" }.With(new UiNode(UiKind.Stack) { Height = "355px" }.With(
                new UiNode(UiKind.StackItem) { Key = "icons", Width = "100px" }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                    FakeDesktopButton(c, "paychecks.exe", ScreenUsers, "icons/obj/card.dmi", "budgetcard")
                        .Concat(FakeDesktopButton(c, "audit.exe", ScreenAudit, "icons/obj/service/bureaucracy.dmi", "docs_verified"))
                        .Concat(FakeDesktopButton(c, ianFileName, ScreenIan, "icons/mob/simple/pets.dmi", young_ian ? "puppy" : "corgi")).ToArray())),
                screen)),
            new UiNode(UiKind.StackItem) { Key = "toolbar", Grow = 1 }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "user" }.With(new UiNode(UiKind.Button) { Key = "user-button", Disabled = true, Icon = "user" }),
                new UiNode(UiKind.StackItem) { Key = "tb-users" }.With(FakeToolbarButton(c, "Account Management", screenmode, ScreenUsers)),
                new UiNode(UiKind.StackItem) { Key = "tb-audit" }.With(FakeToolbarButton(c, "Audit Log", screenmode, ScreenAudit)),
                new UiNode(UiKind.StackItem) { Key = "tb-ian" }.With(FakeToolbarButton(c, ianFileName, screenmode, ScreenIan)),
                new UiNode(UiKind.StackItem) { Key = "spacer", Grow = 1 },
                new UiNode(UiKind.StackItem) { Key = "clock" }.With(new UiNode(UiKind.Button) { Key = "time", Disabled = true, Text = station_time + " PT" })))));
    }

    // tg: FakeDesktopButton.tsx:13  an icon and its name under it
    private static UiNode[] FakeDesktopButton(TguiContext c, string name, int ownerScreenMode, string icon, string iconState) => new[]
    {
        new UiNode(UiKind.StackItem) { Key = "desk:" + ownerScreenMode }.With(new UiNode(UiKind.ImageButton)
        {
            Key = "desk-button:" + ownerScreenMode,
            Color = "transparent",
            DmIcon = icon,
            DmIconState = iconState,
            OnClick = () =>
            {
                c.SetState("screen", ownerScreenMode);
                c.Act("typesound");
            },
        }),
        new UiNode(UiKind.StackItem) { Key = "desk-name:" + ownerScreenMode, Color = "white", TextAlign = "center", Text = name },
    };

    // tg: FakeToolbarButton.tsx:13
    private static UiNode FakeToolbarButton(TguiContext c, string name, int currentScreenMode, int ownerScreenMode) => new UiNode(UiKind.Button)
    {
        Key = "toolbar-button:" + ownerScreenMode,
        Width = "120px",
        Text = name,
        Selected = currentScreenMode == ownerScreenMode, // tg: textColor black on a white background
        OnClick = () =>
        {
            c.SetState("screen", ownerScreenMode);
            c.Act("typesound");
        },
    };

    // tg: FakeWindow.tsx:30
    private static UiNode FakeWindow(TguiContext c, string name, UiNode children) =>
        new UiNode(UiKind.Stack) { Key = "fake-window", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "title-bar" }.With(new UiNode(UiKind.Stack) { Height = "30px" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "window-name", Color = "white", Text = name }),
                new UiNode(UiKind.StackItem) { Key = "close" }.With(new UiNode(UiKind.Button)
                {
                    Key = "close-button",
                    Icon = "times",
                    OnClick = () =>
                    {
                        c.SetState("screen", ScreenNone);
                        c.Act("typesound");
                    },
                }))),
            new UiNode(UiKind.StackItem) { Key = "window-content", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "content", Height = "100%" }.With(children)));

    // tg: Sort.tsx:12  SortButton: a click steps none -> ascending -> descending -> none and resets the others; a right click resets it
    private static UiNode SortButton(TguiContext c, string key, int sorting, string[] otherSorters) => new UiNode(UiKind.Button)
    {
        Key = "sort:" + key,
        Height = "16px",
        FontSize = "10px",
        Text = sorting == SortingAscending ? "^" : sorting == SortingDescending ? "v" : "x",
        OnClickWith = click =>
        {
            c.Act("typesound");
            if (click.Right)
            {
                c.SetState(key, SortingNone);
                return;
            }
            if (sorting == SortingNone)
                c.SetState(key, SortingAscending);
            else if (sorting == SortingAscending)
                c.SetState(key, SortingDescending);
            else
                c.SetState(key, SortingNone);
            foreach (string otherSorter in otherSorters)
                c.SetState(otherSorter, SortingNone);
        },
    };

    private static UiNode HeaderCell(TguiContext c, string key, string title, int sorting, string[] otherSorters) =>
        new UiNode(UiKind.TableCell) { Key = "header:" + key, Bold = true }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Grow = 1, FontSize = "14px", Text = title },
            new UiNode(UiKind.StackItem).With(SortButton(c, key, sorting, otherSorters))));

    private static UiNode DoomModal(TguiContext c) => c.Data.Truthy("crashing")
        ? new UiNode(UiKind.Modal) { Key = "doom", Width = "300px", TextAlign = "center" }.With(new UiNode(UiKind.Box) { Text = GetRandomDoomMessage() })
        : null;

    // tg: UsersScreen.tsx:15
    private static UiNode UsersScreen(TguiContext c)
    {
        double max_pay_mod = c.Data.Num("max_pay_mod");
        double min_pay_mod = c.Data.Num("min_pay_mod");
        double max_advances = c.Data.Num("max_advances");

        int accountNameSorting = c.UseState("sorting_account_name", SortingAscending);
        int balanceSorting = c.UseState("sorting_balance", SortingNone);
        int jobSorting = c.UseState("sorting_job", SortingNone);

        var accountsSorted = c.Data.Arr("accounts").ToList();
        // tg: :32  Array.sort with the first active sorter's comparison (a stable sort, as JavaScript's)
        int Compare(UiValue a, UiValue b)
        {
            if (accountNameSorting == SortingAscending)
                return string.CompareOrdinal(a.Str("name"), b.Str("name")) > 0 ? 1 : -1;
            if (accountNameSorting == SortingDescending)
                return string.CompareOrdinal(a.Str("name"), b.Str("name")) > 0 ? -1 : 1;
            if (balanceSorting == SortingAscending)
                return a.Num("balance").CompareTo(b.Num("balance"));
            if (balanceSorting == SortingDescending)
                return b.Num("balance").CompareTo(a.Num("balance"));
            if (jobSorting == SortingAscending)
                return string.CompareOrdinal(a.Str("job"), b.Str("job")) > 0 ? 1 : -1;
            if (jobSorting == SortingDescending)
                return string.CompareOrdinal(a.Str("job"), b.Str("job")) > 0 ? -1 : 1;
            return 0;
        }
        accountsSorted = accountsSorted.Select((account, i) => (account, i)).OrderBy(p => p, Comparer<(UiValue account, int i)>.Create((x, y) =>
        {
            int result = Compare(x.account, y.account);
            return result != 0 ? result : x.i.CompareTo(y.i);
        })).Select(p => p.account).ToList();

        var table = new UiNode(UiKind.Table) { Key = "accounts" }.With(new UiNode(UiKind.TableRow) { Key = "headers" }.With(
            HeaderCell(c, "sorting_account_name", "Account", accountNameSorting, new[] { "sorting_balance", "sorting_job" }),
            HeaderCell(c, "sorting_balance", "Balance", balanceSorting, new[] { "sorting_account_name", "sorting_job" }),
            HeaderCell(c, "sorting_job", "Assignment", jobSorting, new[] { "sorting_account_name", "sorting_balance" }),
            new UiNode(UiKind.TableCell) { Key = "header:pay", Bold = true, FontSize = "14px", Text = "Pay" },
            new UiNode(UiKind.TableCell) { Key = "header:advances", Bold = true, FontSize = "14px", Text = "Advances" }));
        int index = 0;
        foreach (UiValue account in accountsSorted)
        {
            string id = account.Get("id")?.AsStr() ?? TguiFormat.JsNumber(account.Num("id"));
            double num_advances = account.Num("num_advances");
            table.With(new UiNode(UiKind.TableRow) { Key = "account_" + id + "_" + index++ }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = account.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "balance", Text = TguiFormat.JsNumber(account.Num("balance")) + " cr" },
                new UiNode(UiKind.TableCell) { Key = "job", Text = account.Str("job") },
                new UiNode(UiKind.TableCell) { Key = "pay" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "pay-mod",
                    Fluid = true,
                    Value = account.Num("modifier"),
                    MinValue = min_pay_mod,
                    MaxValue = max_pay_mod,
                    Step = 0.05,
                    Format = value => TguiFormat.ToFixed(value * 100, 1) + "%",
                    OnChange = value => c.Act("change_pay_mod", ("account_id", id), ("pay_mod", value)),
                }),
                new UiNode(UiKind.TableCell) { Key = "advances" }.With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "advance",
                        Height = "12px",
                        Width = "12px",
                        FontSize = "8px",
                        Text = "+",
                        Disabled = num_advances >= max_advances,
                        OnClick = () => c.Act("paycheck_advance", ("account_id", id)),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "count", Text = TguiFormat.JsNumber(num_advances) }))));
        }
        return new UiNode(UiKind.Section) { Key = "users-screen", Scrollable = true, Fill = true }.With(DoomModal(c), table);
    }

    // tg: AuditScreen.tsx:6
    private static UiNode AuditScreen(TguiContext c)
    {
        var table = new UiNode(UiKind.Table) { Key = "audit" }.With(new UiNode(UiKind.TableRow) { Key = "headers" }.With(
            new UiNode(UiKind.TableCell) { Key = "h-account", Bold = true, FontSize = "14px", Text = "Account" },
            new UiNode(UiKind.TableCell) { Key = "h-cost", Bold = true, FontSize = "14px", Text = "Cost" },
            new UiNode(UiKind.TableCell) { Key = "h-location", Bold = true, FontSize = "14px", Text = "Location" },
            new UiNode(UiKind.TableCell) { Key = "h-time", Bold = true, FontSize = "14px", Text = "Timestamp" }));
        int index = 0;
        foreach (UiValue purchase in c.Data.Arr("audit_log"))
        {
            string account = purchase.Str("account", "");
            string stationtime = purchase.Str("stationtime", "");
            table.With(new UiNode(UiKind.TableRow) { Key = "audit_" + index++ }.With(
                new UiNode(UiKind.TableCell) { Key = "account", Text = account.Length > 0 ? account : "???" },
                new UiNode(UiKind.TableCell) { Key = "cost", Text = TguiFormat.JsNumber(purchase.Num("cost")) + " cr" },
                new UiNode(UiKind.TableCell) { Key = "vendor", Text = purchase.Str("vendor") },
                new UiNode(UiKind.TableCell) { Key = "time", Text = (stationtime.Length > 0 ? stationtime : "00:00") + " PT" }));
        }
        return new UiNode(UiKind.Section) { Key = "audit-screen", Scrollable = true, Fill = true }.With(DoomModal(c), table);
    }
}
