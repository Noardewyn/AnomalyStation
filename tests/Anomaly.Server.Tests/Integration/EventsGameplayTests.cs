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
/// The random events on MetaStation (gaps E1a): an anomaly event drops its anomaly in one of the station's safe areas and Central
/// Command's alert reaches the crew; a vent clog spews a critter out of a station vent, and a crewman with a plunger sends it back.
/// </summary>
public class EventsGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static T Control<T>(RealHeadlessWorld w) where T : datum_round_event_control =>
        In(w, () => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    [MetaServerFact]
    public void AnAnomalyAppearsOnTheStationWithItsAlert()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 67, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var chat = new List<string>();
        alice.Client.ServerTextReceived += t => chat.Add(t.ToString());
        w.Run(Second);

        var control = Control<datum_round_event_control_anomaly_anomaly_flux>(w);
        In(w, () => { Waiting.Detach(control.run_event()); return 0; });
        obj_effect_anomaly_flux anomaly = null;
        for (int sec = 0; sec < 40 && (anomaly == null || !chat.Any(c => c.Contains("Hyper-energetic flux wave detected", StringComparison.Ordinal))); sec++)
        {
            w.Run(Second);
            anomaly ??= In(w, () => Dm.each<obj_effect_anomaly_flux>(Dm.world).FirstOrDefault());
        }
        Assert.NotNull(anomaly);
        Assert.True(In(w, () => get_area(anomaly) is area_station), "the anomaly is on the station");
        Assert.Contains(chat, c => c.Contains("Hyper-energetic flux wave detected", StringComparison.Ordinal));
    }

    [MetaServerFact]
    public void AClearedVentClogEndsWithAPlunger()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 68, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        var control = Control<datum_round_event_control_vent_clog>(w);
        In(w, () => { Waiting.Detach(control.run_event()); return 0; });
        datum_round_event_vent_clog clog = null;
        for (int sec = 0; sec < 10 && (clog == null || In(w, () => clog.living_mobs.len) == 0); sec++)
        {
            w.Run(Second);
            clog = In(w, () => Dm.each<datum_round_event_vent_clog>(SSevents.running).FirstOrDefault());
        }
        Assert.NotNull(clog);
        var vent = In(w, () => clog.vent);
        Assert.True(In(w, () => clog.living_mobs.len) >= 1, "a critter crawled out");
        Assert.True(In(w, () => get_area(vent) is area_station), "a station vent");

        // Alice walks up to the vent with a plunger and plunges it
        In(w, () =>
        {
            turf front = Dm.each<turf>(orange(1, vent)).OfType<turf_open>().First(t => !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf())));
            Waiting.NoSleep(me.forceMove(front));
            var plunger = Dm.@new<obj_item_plunger>(front);
            Waiting.NoSleep(me.put_in_active_hand(plunger));
            return 0;
        });
        w.Run(Second);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(vent), ClickButton.Left, ClickModifiers.None));
        w.Run(8 * Second);
        Assert.DoesNotContain(clog, In(w, () => Dm.each<datum_round_event_vent_clog>(SSevents.running).ToList()));
    }
}
