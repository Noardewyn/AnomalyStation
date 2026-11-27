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
/// tg's civilian bounties on MetaStation (gaps P4c): a crewman clicks the civilian bounty terminal with his ID card in hand, the
/// terminal takes the card and opens its window, and the window hands him three bounties to choose from and takes his pick.
/// </summary>
public class BountyGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void A_crewman_takes_a_civilian_bounty_at_the_terminal()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var (terminal, card) = In(w, () =>
        {
            var t = Dm.each<obj_machinery_computer_piratepad_control_civilian>(Dm.world).First();
            turf_open front = Dm.each<turf>(orange(1, t)).OfType<turf_open>().First(x => !Dm.truthy(x.density));
            Waiting.NoSleep(me.forceMove(front));
            var id = me.wear_id as obj_item_card_id;
            Waiting.NoSleep(me.put_in_active_hand(id));
            return (t, id);
        });
        Assert.NotNull(card);
        Assert.NotNull(In(w, () => card.registered_account));
        Assert.NotNull(In(w, () => terminal.pad_ref?.resolve()));
        w.Run(10);

        Assert.True(alice.Session.Click(CryoGameplayTests.On(terminal), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        Assert.Same(card, In(w, () => terminal.inserted_scan_id));
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        // tg opens the window before it keeps the card (id_insert's ui_interact, bounty_machinery.dm:322, then :50): the card shows
        // with SStgui's next update
        TguiWindow window = null;
        for (int t = 0; t < 2 * 20 && window?.Content.AllText().Contains("Welcome valued employee.", StringComparison.Ordinal) != true; t++)
        {
            w.Run(1);
            windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
            window = windows.Open.First(x => x.Interface == "CivCargoHoldTerminal");
        }
        Assert.Contains("Welcome valued employee.", window.Content.AllText());

        window.Context.Act("bounty");
        w.Run(60);
        Assert.Equal(3f, In(w, () => (float)card.registered_account.bounties.len));
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "CivCargoHoldTerminal");
        Assert.Contains("Please Select a Bounty:", window.Content.AllText());

        window.Context.Act("pick", ("value", 1));
        w.Run(10);
        Assert.NotNull(In(w, () => card.registered_account.civilian_bounty));
        Assert.Null(In(w, () => card.registered_account.bounties));
    }
}
