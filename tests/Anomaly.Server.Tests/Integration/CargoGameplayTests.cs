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
/// tg's supply console: a pack ordered from its window arrives in a crate on the cargo shuttle, paid from the cargo budget its window
/// shows (gaps P4b); what is loaded on the shuttle for its trip back is sold into the budget.
/// </summary>
public class CargoGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void An_ordered_pack_comes_on_the_shuttle_in_a_crate()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 51, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        Assert.Equal("cargo_away", In(w, () => Dm.str(SSshuttle.supply.getDockedId())));
        Assert.True(In(w, () => SSshuttle.supply_packs.len > 100), "tg's packs are loaded");
        var console = In(w, () =>
        {
            var c = Dm.each<obj_machinery_computer_cargo>(Dm.world).First(m => m is not obj_machinery_computer_cargo_request);
            turf_open front = Dm.each<turf>(orange(1, c)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
            Waiting.NoSleep(me.forceMove(front));
            return c;
        });
        w.Run(10);

        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "Cargo");
        Assert.Contains("Catalog", window.Content.AllText());

        // the window shows the cargo budget; the budget pays the order when the shuttle buys it
        In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).adjust_money(5000));
        w.Run(60); // SStgui's next pass sends the open window its new data
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "Cargo");
        float budget = In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
        Assert.Contains(TguiFormat.FormatMoney(budget) + " credits", window.Content.AllText());

        string id = In(w, () => Dm.text("[]", typeof(datum_supply_pack_emergency_internals)));
        window.Context.Act("add", ("id", id));
        w.Run(10);
        Assert.Equal(1f, In(w, () => (float)SSshuttle.shopping_list.len));
        float price = In(w, () => ((datum_supply_order)SSshuttle.shopping_list[1]).get_final_cost());
        float fired = In(w, () => SSeconomy.times_fired);

        window.Context.Act("send");
        var seen = new List<string>();
        for (int i = 0; i < 2500 && !In(w, () => Dm.eq(SSshuttle.supply.getDockedId(), "cargo_home")); i++)
            w.Run(1);
        Assert.Equal("cargo_home", In(w, () => Dm.str(SSshuttle.supply.getDockedId())));
        Assert.Equal(0f, In(w, () => (float)SSshuttle.shopping_list.len));
        Assert.True(In(w, () => Dm.each<obj_structure_closet_crate>(Dm.world).Any(crate => get_area(crate) is area_shuttle_supply && crate.name.Contains('#'))), "the crate came with the shuttle");
        // the budget paid it (with the grants of the economy's fires meanwhile)
        float grants = MAX_GRANT_DPT * (In(w, () => SSeconomy.times_fired) - fired);
        Assert.Equal(budget - price + grants, In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance));

        // the economy's mail came with the shuttle; the crew takes it off (mail may not ride back, the blacklist)
        int mailCrates = In(w, () =>
        {
            var crates = Dm.each<obj_structure_closet_crate_mail_economy>(Dm.world).Where(crate => get_area(crate) is area_shuttle_supply).ToList();
            foreach (var crate in crates)
                Waiting.NoSleep(crate.forceMove(me.loc));
            return crates.Count;
        });
        Assert.True(mailCrates <= 1);

        // iron sheets loaded on the shuttle are sold when it leaves: the budget gets their value and the window CentCom's message
        var sheets = In(w, () =>
        {
            turf_open floor = Dm.each<area>(SSshuttle.supply.shuttle_areas).SelectMany(a => Dm.each<turf_open_floor>(a.get_turfs_from_all_zlevels()))
                .First(t => !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf())));
            return Dm.@new<obj_item_stack_sheet_iron>(floor, 50);
        });
        float beforeSale = In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
        fired = In(w, () => SSeconomy.times_fired);
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "Cargo");
        Assert.True(In(w, () => Dm.truthy(SSshuttle.supply.canMove())), "the shuttle can move: " + In(w, () => string.Join(", ",
            Dm.each<area>(SSshuttle.supply.shuttle_areas).SelectMany(a => Dm.each<turf>(a.get_turfs_from_all_zlevels())).SelectMany(t => Dm.each<atom>(t.get_all_contents()))
                .Where(a => Dm.truthy(is_type_in_typecache(a, GLOB.blacklisted_cargo_types)) && a is not obj_docking_port).Select(a => a.type.ToString()))));
        window.Context.Act("send");
        w.Run(10);
        Assert.False(In(w, () => Dm.eq(SSshuttle.supply.mode, SHUTTLE_IDLE)), "the shuttle left: " + In(w, () => Dm.str(SSshuttle.supply.getStatusText())));
        for (int i = 0; i < 2500 && !In(w, () => Dm.eq(SSshuttle.supply.getDockedId(), "cargo_away")); i++)
            w.Run(1);
        Assert.Equal("cargo_away", In(w, () => Dm.str(SSshuttle.supply.getDockedId())));
        Assert.True(In(w, () => QDELETED(sheets)), "the sheets were sold");
        grants = MAX_GRANT_DPT * (In(w, () => SSeconomy.times_fired) - fired);
        Assert.True(In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance) > beforeSale + grants, "the sale paid the budget");
        w.Run(60);
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "Cargo");
        Assert.Contains("iron", window.Content.AllText());
    }
}
