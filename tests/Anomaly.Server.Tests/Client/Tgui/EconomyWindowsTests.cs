using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P4a (D327): the client's economy windows without the engine: NtosPay's balance, transfer and history, the BankMachine's vault
/// and siphon button, the HoloPay terminal (a forced fee's confirm) and its owner's settings, and the AccountingConsole's fake desktop
/// with the crew's accounts (sorting, pay modifiers, advances), the audit log and a crashing market's message.
/// </summary>
public class EconomyWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(Func<TguiContext, UiNode> render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (render(c), c, acts);
    }

    [Fact]
    public void NtosPayShowsTheBalanceSendsByTokenAndListsTheHistory()
    {
        const string json = "{\"name\":\"Jane Doe\",\"owner_token\":\"JE-K-1234\",\"money\":150,\"wanted_token\":\"Token: CD-A-4321\","
            + "\"transaction_list\":[{\"adjusted_money\":125,\"reason\":\"Nanotrasen: Shift Payment\"},{\"adjusted_money\":-40,\"reason\":\"Transfer: To Carol Doe\"}]}";
        var local = new TguiLocalState();
        var (tree, _, acts) = Render(NtosPay.Render, "NtosPay", json, local);
        Assert.Equal("Hi, Jane Doe.", tree.Walk().Single(n => n.Key == "hi").Walk().Last().Text);
        Assert.Equal("Account balance: 150 credits", tree.Walk().Single(n => n.Key == "balance" && n.Kind == UiKind.TableRow).Walk().Last().Text);
        Assert.Equal("Token: CD-A-4321", tree.Walk().Single(n => n.Key == "wanted-token").Text);
        UiNode[] rows = tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("log:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(("green", "+125"), (rows[0].Color, rows[0].Walk().Single(n => n.Key == "amount").Text));
        Assert.Equal(("red", "-40"), (rows[1].Color, rows[1].Walk().Single(n => n.Key == "amount").Text));

        tree.Walk().Single(n => n.Key == "token" && n.Kind == UiKind.Input).OnInput("CD-A-4321");
        (tree, _, _) = Render(NtosPay.Render, "NtosPay", json, local, acts);
        tree.Walk().Single(n => n.Key == "amount" && n.Kind == UiKind.NumberInput).OnChange(40);
        (tree, _, _) = Render(NtosPay.Render, "NtosPay", json, local, acts);
        Assert.Equal(150, tree.Walk().Single(n => n.Key == "amount" && n.Kind == UiKind.NumberInput).MaxValue);
        tree.Walk().Single(n => n.Key == "send-credits").OnClick();
        Assert.Equal(("Transaction", "{\"token\":\"CD-A-4321\",\"amount\":40}"), acts.Last());

        var (noId, _, _) = Render(NtosPay.Render, "NtosPay", "{\"name\":null}");
        Assert.Contains(noId.Walk(), n => n.Key == "no-id");
    }

    [Fact]
    public void TheBankMachineShowsTheVaultAndTogglesTheSiphon()
    {
        var (tree, c, acts) = Render(BankMachine.Render, "BankMachine", "{\"current_balance\":12500,\"siphoning\":0,\"station_name\":\"Space Station 13\"}");
        Assert.Equal((350.0, 155.0), (c.WindowWidth, c.WindowHeight));
        Assert.Equal("Space Station 13 Vault", tree.Walk().Single(n => n.Key == "vault").Title);
        Assert.Equal("12 500 cr", tree.Walk().Single(n => n.Key == "balance").Text);
        UiNode siphon = tree.Walk().Single(n => n.Key == "siphon");
        Assert.Equal(("Siphon Credits", "sync", false), (siphon.Text, siphon.Icon, siphon.Selected));
        siphon.OnClick();
        Assert.Equal(("siphon", (string)null), acts.Single());
        (tree, _, acts) = Render(BankMachine.Render, "BankMachine", "{\"current_balance\":0,\"siphoning\":1,\"station_name\":\"SS13\"}");
        tree.Walk().Single(n => n.Key == "siphon").OnClick();
        Assert.Equal("halt", acts.Single().Action);
        Assert.Equal("1 234 567", TguiFormat.FormatMoney(1234567));
        Assert.Equal("-1 000", TguiFormat.FormatMoney(-1000));
    }

    [Fact]
    public void TheHoloPayTerminalConfirmsAForcedFeeAndItsOwnerSetsItUp()
    {
        const string payer = "{\"owner\":\"Bob Roe\",\"name\":\"Bob&#39;s Burgers\",\"description\":\"Pays directly into Bob Roe's bank account.\",\"force_fee\":20,"
            + "\"shop_logo\":\"hamburger\",\"max_fee\":5000,\"available_logos\":[\"donate\",\"hamburger\"],\"user\":{\"name\":\"Carol Doe\",\"balance\":50}}";
        var local = new TguiLocalState();
        var (tree, _, acts) = Render(HoloPay.Render, "HoloPay", payer, local);
        Assert.Equal("Bob's Burgers", tree.Walk().Single(n => n.Key == "shop-name").Text);
        Assert.Equal("hamburger", tree.Walk().Single(n => n.Key == "shop-logo").Icon);
        Assert.Null(tree.Walk().Single(n => n.Key == "terminal").Buttons); // not the owner: no setup
        UiNode pay = tree.Walk().Single(n => n.Key == "pay");
        Assert.Equal(("Pay 20 cr", false), (pay.Text, pay.Disabled));
        pay.OnClick(); // a forced fee asks first
        Assert.Empty(acts);
        (tree, _, _) = Render(HoloPay.Render, "HoloPay", payer, local, acts);
        tree.Walk().Single(n => n.Key == "pay").OnClick();
        Assert.Equal(("pay", (string)null), acts.Single());

        // the owner opens the settings and picks a logo
        string owner = payer.Replace("\"user\":{\"name\":\"Carol Doe\"", "\"user\":{\"name\":\"Bob Roe\"");
        local = new TguiLocalState();
        (tree, _, acts) = Render(HoloPay.Render, "HoloPay", owner, local);
        Assert.True(tree.Walk().Single(n => n.Key == "pay").Disabled); // you can't pay yourself
        tree.Walk().Single(n => n.Key == "setup").OnClick();
        (tree, _, _) = Render(HoloPay.Render, "HoloPay", owner, local, acts);
        tree.Walk().Single(n => n.Key == "logo-dropdown").OnSelected("donate");
        Assert.Equal(("logo", "{\"logo\":\"donate\"}"), acts.Last());
        tree.Walk().Single(n => n.Key == "name-area").OnCommit("Bobs Diner");
        Assert.Equal(("rename", "{\"name\":\"Bobs Diner\"}"), acts.Last());
        tree.Walk().Single(n => n.Key == "done").OnClick();
        Assert.Equal("done", acts.Last().Action);
    }

    [Fact]
    public void TheAccountingConsoleOpensItsFakeWindowsSortsAccountsAndShowsTheAudit()
    {
        const string json = "{\"accounts\":[{\"name\":\"Zed\",\"job\":\"Cook\",\"balance\":40,\"modifier\":1,\"num_advances\":0,\"id\":\"222222\"},"
            + "{\"name\":\"Amy\",\"job\":\"Station Engineer\",\"balance\":900,\"modifier\":1.25,\"num_advances\":3,\"id\":\"111111\"}],"
            + "\"audit_log\":[{\"account\":\"Amy\",\"cost\":25,\"vendor\":\"Snack machine\",\"stationtime\":\"12:05\"}],\"crashing\":0,"
            + "\"station_time\":\"12:10\",\"pic_file_format\":\"png\",\"young_ian\":0,\"max_advances\":3,\"max_pay_mod\":1.5,\"min_pay_mod\":0.5}";
        var local = new TguiLocalState();
        var (tree, c, acts) = Render(AccountingConsole.Render, "AccountingConsole", json, local);
        Assert.Equal(("ntOS95", 680.0, 440.0), (c.WindowTheme, c.WindowWidth, c.WindowHeight));
        Assert.Equal("12:10 PT", tree.Walk().Single(n => n.Key == "time").Text);
        Assert.Contains(tree.Walk(), n => n.Key == "desk-name:3" && n.Text == "Ian.png");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "fake-window");

        tree.Walk().Single(n => n.Key == "desk-button:1").OnClick();
        Assert.Equal("typesound", acts.Single().Action);
        (tree, _, _) = Render(AccountingConsole.Render, "AccountingConsole", json, local, acts);
        Assert.Equal("Crew Account Summary", tree.Walk().Single(n => n.Key == "window-name").Text);
        string[] Names(UiNode t) => t.Walk().Where(n => n.Key == "name" && n.Kind == UiKind.TableCell).Select(n => n.Text).ToArray();
        Assert.Equal(new[] { "Amy", "Zed" }, Names(tree)); // the account name ascending by default
        Assert.True(tree.Walk().Where(n => n.Key == "advance").First().Disabled); // Amy has her three advances
        UiNode payMod = tree.Walk().First(n => n.Key == "pay-mod");
        Assert.Equal("125.0%", payMod.Format(payMod.Value));
        payMod.OnChange(1.5);
        Assert.Equal(("change_pay_mod", "{\"account_id\":\"111111\",\"pay_mod\":1.5}"), acts.Last());
        tree.Walk().Where(n => n.Key == "advance").Last().OnClick();
        Assert.Equal(("paycheck_advance", "{\"account_id\":\"222222\"}"), acts.Last());

        // the balance's sort button: ascending, which resets the name's
        tree.Walk().Single(n => n.Key == "sort:sorting_balance").OnClickWith(new UiClick(false, false, false, false));
        (tree, _, _) = Render(AccountingConsole.Render, "AccountingConsole", json, local, acts);
        Assert.Equal(new[] { "Zed", "Amy" }, Names(tree));
        Assert.Equal("x", tree.Walk().Single(n => n.Key == "sort:sorting_account_name").Text);

        // the audit log, and the doom of a crashing market
        tree.Walk().Single(n => n.Key == "toolbar-button:2").OnClick();
        (tree, _, _) = Render(AccountingConsole.Render, "AccountingConsole", json.Replace("\"crashing\":0", "\"crashing\":1"), local, acts);
        Assert.Equal("Audit Log", tree.Walk().Single(n => n.Key == "window-name").Text);
        Assert.Equal(("Amy", "25 cr", "Snack machine", "12:05 PT"), (tree.Walk().Single(n => n.Key == "account").Text, tree.Walk().Single(n => n.Key == "cost").Text,
            tree.Walk().Single(n => n.Key == "vendor").Text, tree.Walk().Single(n => n.Key == "time" && n.Kind == UiKind.TableCell).Text));
        Assert.Contains(tree.Walk(), n => n.Key == "doom" && n.Kind == UiKind.Modal);
    }
}
