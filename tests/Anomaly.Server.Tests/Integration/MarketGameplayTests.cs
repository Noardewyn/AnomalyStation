using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The order consoles on MetaStation (gaps P4d): a crewman at the kitchen's produce console fills the cart in its window and buys the
/// groceries, which the supply shuttle brings in the kitchen's freezer crate; a cook at the service department's order console orders
/// a crate for free and the console cools down.
/// </summary>
public class MarketGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static TguiWindow OpenWindow(RealHeadlessWorld w, RealHeadlessWorld.Player player, TguiWindows windows, atom target, string @interface)
    {
        Assert.True(player.Session.Click(CryoGameplayTests.On(target), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        windows.Sync(player.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.First(x => x.Interface == @interface);
    }

    private static void StandBy(RealHeadlessWorld w, mob_living_carbon_human me, atom target) => In(w, () =>
    {
        turf_open front = Dm.each<turf>(orange(1, target)).OfType<turf_open>().First(x => !Dm.truthy(x.density) && !Dm.truthy(Waiting.NoSleep(x.is_blocked_turf())));
        Waiting.NoSleep(me.forceMove(front));
        return 0;
    });

    [MetaServerFact]
    public void A_crewman_buys_groceries_at_the_produce_console()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var console = In(w, () => Dm.each<obj_machinery_computer_order_console_cook>(Dm.world).First());
        var account = In(w, () => (me.wear_id as obj_item_card_id)?.registered_account);
        Assert.NotNull(account);
        In(w, () => account.adjust_money(1000));
        StandBy(w, me, console);
        w.Run(10);

        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        TguiWindow window = OpenWindow(w, alice, windows, console, "ProduceConsole");
        Assert.Contains("Potato", window.Content.AllText());
        string potato = In(w, () => Dm.str(REF(Dm.each<datum_orderable_item_veggies_potato>(GLOB.order_console_products).Single())));

        float before = In(w, () => account.account_balance);
        window.Context.Act("cart_set", ("target", potato), ("amt", 25));
        w.Run(10);
        window.Context.Act("purchase");
        w.Run(10);
        Assert.Equal(before - In(w, () => Dm.round(250 * console.cargo_cost_multiplier)), In(w, () => account.account_balance));
        Assert.Equal(25f, In(w, () => Dm.num(SSshuttle.chef_groceries[Dm.locate_ref(potato)])));
        w.Run(60); // SStgui's next pass sends the open window its new data: the order-sent dimmer
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "ProduceConsole");
        Assert.Contains("Order sent! Machine on cooldown...", window.Content.AllText());

        // the shuttle's next arrival brings them in the kitchen's freezer crate
        In(w, () => { Waiting.NoSleep(SSshuttle.supply.buy()); return 0; });
        var crate = In(w, () => Dm.each<obj_structure_closet_crate_freezer>(Dm.world).FirstOrDefault(x => get_area(x) is area_shuttle_supply && Dm.str(x.name) == "kitchen produce freezer"));
        Assert.NotNull(crate);
        Assert.Equal(25, In(w, () => Dm.each<obj_item_food_grown_potato>(crate.contents).Count()));
    }

    [MetaServerFact]
    public void A_cook_orders_a_free_crate_at_the_service_console()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var console = In(w, () => Dm.each<obj_machinery_modular_computer_preset_cargochat_service>(Dm.world).First());
        In(w, () => Waiting.NoSleep(SSid_access.apply_trim_to_card((obj_item_card_id)me.wear_id, typeof(datum_id_trim_job_cook))));
        StandBy(w, me, console);
        w.Run(10);

        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        TguiWindow window = OpenWindow(w, alice, windows, console, "NtosDeptOrder");
        Assert.Contains("Order", window.Content.AllText());
        var app = In(w, () => (datum_computer_file_program_department_order)console.cpu.find_file_by_name("dept_order"));
        string pack = In(w, () =>
        {
            var data = (DmList)Waiting.NoSleep(app.ui_static_data(me));
            var first = Dm.each_cast<DmList>((DmList)data["supplies"]).SelectMany(group => Dm.each_cast<DmList>((DmList)group["packs"])).First();
            return Dm.str(first["id"]);
        });

        window.Context.Act("order", ("id", pack));
        w.Run(10);
        var order = In(w, () => app.department_order);
        Assert.NotNull(order);
        Assert.Contains(order, In(w, () => Dm.each<datum_supply_order>(SSshuttle.shopping_list).ToList()));
        Assert.Null(In(w, () => order.paying_account)); // free for the department
        w.Run(60);
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "NtosDeptOrder");
        Assert.Contains("Ready for another order in", window.Content.AllText());
    }
}
