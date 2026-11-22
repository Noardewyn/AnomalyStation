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
/// The cargo and shuttle events on MetaStation (gaps E3): Central Command's shuttle loan offer reaches the crew, cargo lends the
/// supply shuttle from its console's Loan Shuttle button, the budget is paid and the shuttle comes back from CentCom loaded with the
/// loan's cargo; a stray cargo pod's alert names the station area it falls on, and its crate lands there.
/// </summary>
public class CargoEventsGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static T Control<T>(RealHeadlessWorld w) where T : datum_round_event_control =>
        In(w, () => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    [MetaServerFact]
    public void ALentSupplyShuttleComesBackWithTheLoansCargo()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var chat = new List<string>();
        alice.Client.ServerTextReceived += t => chat.Add(t.ToString());
        w.Run(Second);
        Assert.Equal("cargo_away", In(w, () => Dm.str(SSshuttle.supply.getDockedId())));

        var control = Control<datum_round_event_control_shuttle_loan>(w);
        In(w, () => { Waiting.Detach(control.run_event()); return 0; });
        datum_round_event_shuttle_loan loan = null;
        for (int sec = 0; sec < 20 && (loan == null || In(w, () => SSshuttle.shuttle_loan) == null); sec++)
        {
            w.Run(Second);
            loan ??= In(w, () => Dm.each<datum_round_event_shuttle_loan>(SSevents.running).FirstOrDefault());
        }
        Assert.NotNull(loan);
        Assert.Same(loan, In(w, () => SSshuttle.shuttle_loan));
        string offer = In(w, () => loan.situation.announcement_text);
        Assert.Contains(chat, c => c.Contains("Cargo: " + offer, StringComparison.Ordinal));
        // the pizza delivery, for a known cargo; the shuttle's five minutes shortened as an admin would
        In(w, () =>
        {
            loan.situation = Dm.@new<datum_shuttle_loan_situation_pizza_delivery>();
            loan.delay_time = 20 * SECONDS;
            return 0;
        });

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
        Assert.Contains("Loan Shuttle", window.Content.AllText());

        float budget = In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
        window.Context.Act("loan");
        w.Run(10);
        Assert.Equal(1f, In(w, () => loan.dispatched));
        Assert.Contains(chat, c => c.Contains("The supply shuttle has been loaned to CentCom.", StringComparison.Ordinal));
        Assert.Contains(chat, c => c.Contains("Cargo shuttle commandeered by", StringComparison.Ordinal));
        Assert.Equal(SHUTTLE_CALL, In(w, () => SSshuttle.supply.mode));
        Assert.True(In(w, () => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance) >= budget, "the pizza's loan pays nothing, the budget keeps its grants");
        w.Run(60);
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "Cargo");
        // the offer is taken: the next event tick loads the shuttle at CentCom and the loan's row leaves the window
        Assert.DoesNotContain("Loan Shuttle", window.Content.AllText());
        Assert.Null(In(w, () => SSshuttle.shuttle_loan));
        Assert.Equal(6, In(w, () => Dm.each<obj_item_pizzabox>(Dm.world).Count(p => get_area(p) is area_shuttle_supply)));

        // the shuttle docks at the station with the pizza aboard
        for (int i = 0; i < 2500 && !In(w, () => Dm.eq(SSshuttle.supply.getDockedId(), "cargo_home")); i++)
            w.Run(1);
        Assert.Equal("cargo_home", In(w, () => Dm.str(SSshuttle.supply.getDockedId())));
        int pizzas = In(w, () => Dm.each<obj_item_pizzabox>(Dm.world).Count(p => get_area(p) is area_shuttle_supply && Dm.truthy(is_station_level(p.z))));
        Assert.Equal(6, pizzas);
    }

    [MetaServerFact]
    public void AStrayCargoPodFallsOnTheStation()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 72, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var chat = new List<string>();
        alice.Client.ServerTextReceived += t => chat.Add(t.ToString());
        w.Run(Second);

        var control = Control<datum_round_event_control_stray_cargo>(w);
        In(w, () => { Waiting.Detach(control.run_event(announce_chance_override: 100)); return 0; });
        w.Run(Second);
        var cargo = In(w, () => Dm.each<datum_round_event_stray_cargo>(SSevents.running).First());
        area impact = In(w, () => cargo.impact_area);
        Assert.True(impact is area_station, "a station area");
        In(w, () => { cargo.possible_pack_types = Dm.list(typeof(datum_supply_pack_emergency_internals)); return 0; });

        obj_effect_pod_landingzone marker = null;
        for (int sec = 0; sec < 100 && marker == null; sec++)
        {
            w.Run(Second);
            marker = In(w, () => Dm.each<obj_effect_pod_landingzone>(Dm.world).FirstOrDefault());
        }
        Assert.NotNull(marker);
        Assert.Same(impact, In(w, () => get_area(marker)));
        Assert.Contains(chat, c => c.Contains("Stray cargo pod detected on long-range scanners. Expected location of impact: " + In(w, () => impact.name), StringComparison.Ordinal));
        turf zone = In(w, () => get_turf(marker));
        obj_structure_closet_crate crate = null;
        for (int sec = 0; sec < 30 && crate == null; sec++)
        {
            w.Run(Second);
            crate = In(w, () => Dm.each<obj_structure_closet_crate>(Dm.world).FirstOrDefault(c => c.loc == zone));
        }
        Assert.NotNull(crate);
        Assert.Equal("internals crate", In(w, () => Dm.str(crate.name)));
    }
}
