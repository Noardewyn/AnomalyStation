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
/// The newscaster on MetaStation (silicons SI6b): a priority announcement goes on the news, the newscasters call it out, and a
/// crewman clicks a newscaster on the wall, opens the Station Announcements channel and reads the story in the window; he posts a
/// bounty on its bounty board.
/// </summary>
public class NewscasterGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void A_crewman_reads_the_announcements_at_a_newscaster()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var caster = In(w, () =>
        {
            var c = Dm.each<obj_machinery_newscaster>(Dm.world).First(n => !Dm.truthy(Dm.bitand(n.machine_stat, Dm.bitor(NOPOWER, BROKEN))) && Dm.each<turf>(orange(1, n)).OfType<turf_open>().Any(t => !Dm.truthy(t.density)));
            turf_open front = Dm.each<turf>(orange(1, c)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf())));
            Waiting.NoSleep(me.forceMove(front));
            return c;
        });
        Assert.True(In(w, () => GLOB.allCasters.len) > 50, "Meta's newscasters are live");
        w.Run(10);

        In(w, () => { priority_announce("The coffee machine is fixed.", "Good news"); return 0; });
        w.Run(10);
        Assert.Equal(1f, In(w, () => caster.alert));

        Assert.True(alice.Session.Click(CryoGameplayTests.On(caster), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "PhysicalNewscaster");
        Assert.Contains("Station Announcements", window.Content.AllText());
        Assert.Equal(0f, In(w, () => caster.alert)); // opening it checks the messages

        float channelId = In(w, () => Dm.num(((datum_feed_channel)GLOB.news_network.network_channels_by_name[NEWSCASTER_STATION_ANNOUNCEMENTS]).channel_id));
        window.Context.Act("setChannel", ("channel", channelId));
        w.Run(60);
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        window = windows.Open.First(x => x.Interface == "PhysicalNewscaster");
        Assert.Contains("The coffee machine is fixed.", string.Join("\n", window.Content.Walk().Select(n => n.TextValue ?? n.Text ?? "")));

        // a bounty on the board
        window.Context.Act("bountyText", ("bountytext", "Bring me coffee."));
        w.Run(10);
        window.Context.Act("createBounty");
        w.Run(10);
        Assert.Equal("Bring me coffee.", In(w, () => Dm.str(Dm.each<datum_station_request>(GLOB.request_list).Single().description)));
    }
}
