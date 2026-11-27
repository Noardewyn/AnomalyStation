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

/// <summary>A shaft miner's trip on the real server: down to Lavaland on the mining shuttle, a shelter unfolded from a capsule, and back.</summary>
public class MiningTripGameplayTests
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static void In(RealHeadlessWorld w, Action f) { using (w.World.Enter()) f(); }

    private static TguiWindow OpenWindow(RealHeadlessWorld w, RealHeadlessWorld.Player player, atom console)
    {
        Assert.True(player.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => player.Session.UiAct(window, action, parameters), SendClose = window => player.Session.UiClose(window) };
        windows.Sync(player.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.First(x => x.Interface == "ShuttleConsole");
    }

    private static void FlyTo(RealHeadlessWorld w, obj_docking_port_mobile shuttle, string dock)
    {
        for (int i = 0; i < 2000 && !In(w, () => shuttle.get_docked() is obj_docking_port_stationary d && Dm.eq(d.shuttle_id, dock)); i++)
            w.Run(1);
        Assert.Equal(dock, In(w, () => Dm.str(((obj_docking_port_stationary)shuttle.get_docked()).shuttle_id)));
    }

    [MetaServerFact]
    public void A_miner_flies_down_to_Lavaland_unfolds_a_shelter_and_flies_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13040, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player mina = w.Join("Mina");
        var me = (mob_living_carbon_human)w.MobOf(mina);
        obj_docking_port_mobile shuttle = null;
        obj_machinery_computer_shuttle_mining console = null;
        In(w, () =>
        {
            shuttle = (obj_docking_port_mobile)SSshuttle.getShuttle("mining");
            console = Dm.each<obj_machinery_computer_shuttle_mining>(Dm.world).First(m => m.GetType() == typeof(obj_machinery_computer_shuttle_mining) && get_area(m) is area_shuttle_mining);
            turf_open front = Dm.each<turf>(orange(1, console)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && get_area(t) is area_shuttle_mining);
            Waiting.NoSleep(me.forceMove(front));
        });
        w.Run(10);

        // down to Lavaland
        TguiWindow window = OpenWindow(w, mina, console);
        Assert.Contains("lavaland mine", window.Content.AllText());
        window.Context.Act("move", ("shuttle_id", "mining_away"));
        FlyTo(w, shuttle, "mining_away");
        float lavaland = In(w, () => ((obj_docking_port_stationary)SSshuttle.getDock("mining_away")).z);
        Assert.Equal(lavaland, In(w, () => me.z));
        w.Run(10 * 20); // up again after the landing's jolt

        // a capsule thrown onto open ground beside the dock unfolds a shelter there
        turf spot = In(w, () =>
        {
            var alpha = (datum_map_template_shelter)SSmapping.shelter_templates["shelter_alpha"];
            Assert.NotNull(alpha);
            var dock = (obj_docking_port_stationary)SSshuttle.getDock("mining_away");
            return Dm.each<turf>(orange(30, dock)).Where(t => t.loc is area_lavaland_surface).OrderBy(t => get_dist(t, dock))
                .FirstOrDefault(t => Dm.eq(alpha.check_deploy(t, null), SHELTER_DEPLOY_ALLOWED));
        });
        Assert.NotNull(spot);
        (float x, float y, float z) at = In(w, () => (spot.x, spot.y, spot.z));
        var capsule = In(w, () =>
        {
            var c = Dm.@new<obj_item_survivalcapsule>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(c));
            Waiting.NoSleep(c.attack_self(me));
            Waiting.NoSleep(c.forceMove(spot));
            return c;
        });
        w.Run(5 * 20 + 10);
        In(w, () =>
        {
            Assert.True(QDELETED(capsule));
            // the template's turfs replace the ground: the shelter's floor stands where the capsule lay
            turf floor = locate(at.x, at.y, at.z);
            Assert.IsType<area_misc_survivalpod>(get_area(floor));
            Assert.Contains(Dm.each<obj_item_gps_computer>(Dm.world), g => get_area(g) == get_area(floor));
        });

        // back aboard and home
        window = OpenWindow(w, mina, console);
        window.Context.Act("move", ("shuttle_id", "mining_home"));
        FlyTo(w, shuttle, "mining_home");
        Assert.Equal(1f, In(w, () => me.z));
        Assert.DoesNotContain("survival_pod.dm", w.Err.ToString());
        Assert.DoesNotContain("shelters.dm", w.Err.ToString());
    }
}
