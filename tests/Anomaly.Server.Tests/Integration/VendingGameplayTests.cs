using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package S2 (code/modules/vending) on MetaStation through the real server and a client's own stack. A crewman clicks one of Meta's
/// cola machines (its random spawner's pick): tg's Vending window lists its products with their prices and his account's balance; a click
/// on a product's card buys it with his job's bank account (gaps P4a: the starting paychecks pay it), the can comes into his hand and the
/// window's stock drops. Then a coffee machine: the coffee comes out with the machine's vend animation (its flick of
/// coffee-vend) on the client.
/// </summary>
public class VendingGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    // the first vendor of that kind (its subtypes too) in a station room with a clear tile beside it
    private static T Vendor<T>() where T : obj_machinery_vending
        => Dm.each<T>().Where(v => v.z == 1 && get_area(v) is area_station && ClearNeighbour(v) != null).OrderBy(v => v.dm_id).FirstOrDefault();

    [MetaServerFact]
    public void A_crewman_buys_a_can_from_a_cola_machine_through_its_window_and_a_coffee_with_the_machines_vend_animation()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 95, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        output.WriteLine(In(w, () => string.Join(", ", Dm.each<obj_machinery_vending>().Where(v => v.z == 1).GroupBy(v => v.GetType().Name).Select(g => g.Key + " " + g.Count()))));
        Assert.All(In(w, () => Dm.each<obj_machinery_vending>().Where(v => v.z == 1).ToList()), v => Assert.False(InertSeam.IsInert(v), v.GetType().Name));

        // 1. a cola machine: the click opens tg's window with its products as cards
        obj_machinery_vending_cola cola = In(w, Vendor<obj_machinery_vending_cola>);
        Assert.NotNull(cola);
        output.WriteLine(In(w, () => $"{Dm.str(cola.name)} ({cola.GetType().Name}) at {cola.x},{cola.y} in {Dm.str(((area)get_area(cola)).name)}"));
        Do(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            Waiting.NoSleep(me.forceMove(ClearNeighbour(cola)));
        });
        w.Run(Second);
        Assert.True(p.Session.Click(On(cola), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Vending", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((431.0, 635.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        List<UiNode> cards = content.Walk().Where(n => n.Kind == UiKind.ImageButton && !n.Disabled).ToList();
        output.WriteLine("the window shows: " + string.Join(" | ", cards.Select(c => c.AllText())));
        Assert.NotEmpty(cards);
        UiNode card = cards[0];
        Assert.DoesNotContain("FREE", card.AllText()); // the vendor charges (gaps P4a)
        string path = card.Key["product:".Length..];
        datum_data_vending_product record = In(w, () => Dm.each_cast<datum_data_vending_product>(cola.product_records).First(r => obj_machinery_vending.SANITIZED_PATH(r.product_path) == path));
        float before = In(w, () => Dm.num(record.amount));
        // the crewman's job gave him a bank account with his starting paychecks (_job.dm:210), on his ID card
        datum_bank_account account = In(w, () => (me.get_idcard(TRUE) as obj_item_card_id)?.registered_account);
        Assert.NotNull(account);
        float balance = In(w, () => account.account_balance);
        Assert.True(balance > 0, "starting paychecks " + balance);
        Assert.Contains(content.Walk(), n => n.Text != null && n.Text.StartsWith(TguiFormat.JsNumber(balance), StringComparison.Ordinal)); // the window's balance
        float price = In(w, () =>
        {
            float listed = Dm.num(record.price);
            // a crewman of the vendor's department pays a fifth (discount_check, inventory.dm:294)
            return Dm.eq(account.account_job?.paycheck_department, cola.payment_department) ? max(round(listed * DEPARTMENT_DISCOUNT), 1) : listed;
        });
        output.WriteLine($"balance {balance}, price {price}");

        // 2. a click on the card buys it: the product comes into the hand, the stock drops in the window too
        card.OnClick();
        w.Run(Second);
        obj_item held = In(w, () => me.get_active_held_item() as obj_item);
        Assert.NotNull(held);
        Assert.Same(In(w, () => record.product_path), held.GetType());
        Assert.Equal(before - 1, In(w, () => Dm.num(record.amount)));
        Assert.Equal(before - 1, window.Merged.Get("stock").Get(path).Num("amount"));
        Assert.Equal(balance - price, In(w, () => account.account_balance));
        output.WriteLine(In(w, () => "bought " + Dm.str(held.name)));
        p.Session.UiClose(window.Window);
        w.Run(Second);
        Assert.Empty(replica.UiWindows.Tgui());
        windows.Sync(replica.UiWindows, 1280, 720, 1); // the client's frame drops the closed window (the next one takes its index)

        // 3. a coffee machine: the coffee comes out with the machine's vend animation, seen by the client
        obj_machinery_vending_coffee coffee = In(w, Vendor<obj_machinery_vending_coffee>);
        Assert.NotNull(coffee);
        Do(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            Waiting.NoSleep(me.forceMove(ClearNeighbour(coffee)));
        });
        w.Run(Second);
        Assert.True(p.Session.Click(On(coffee), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        content = Shown(windows, replica, window.Window);
        output.WriteLine(In(w, () => $"{Dm.str(coffee.name)} at {coffee.x},{coffee.y} in {Dm.str(((area)get_area(coffee)).name)}, operational {coffee.is_operational}")
            + "; the window shows: " + string.Join(" | ", content.Walk().Where(n => n.Kind == UiKind.ImageButton).Select(c => c.AllText() + (c.Disabled ? " (disabled)" : ""))));
        content.Walk().First(n => n.Kind == UiKind.ImageButton && !n.Disabled).OnClick();
        w.Run(Second / 2);
        Assert.NotNull(In(w, () => me.get_active_held_item()));
        Assert.True(replica.TryGet(coffee.dm_id, out ReplicaEntity seen));
        Assert.Contains("coffee-vend", CryoGameplayTests.States(replica, seen.State.FlickAppearance));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
