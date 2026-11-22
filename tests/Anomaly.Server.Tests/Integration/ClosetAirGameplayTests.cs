using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// A player shut in a closet or crate breathes (closets.dm: return_air, LateInitialize, process_internal_air; crates.dm and freezer.dm: the freezers'
/// cooling) through the real server on MetaStation. An ordinary closet and crate breathe the turf's air; a map-loaded sealed one (the kitchen freezer
/// locker, the freezer crate) breathes its own copy of the turf's air and the freezer cools it.
/// </summary>
public class ClosetAirGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public ClosetAirGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The player shut in a closet for a minute: the closet is made (spawned on his turf, or the first map-loaded one of the type), he is put inside, Life runs.
    private record struct Result(float Oxy, float Body, float CloseAir, float ClosetKelvin, float Moles);

    private Result Shut<T>(bool mapped) where T : obj_structure_closet
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int bootRuntimes = w.World.RuntimeErrorCount;
        w.Run(Second);

        var closet = In(w, () =>
        {
            if (!mapped)
                return Dm.@new<T>(me.loc);
            T found = null;
            w.World.Objects.ForEachLiveInIdOrder((_, o) => { if (found == null && o is T t && t.GetType() == typeof(T) && Dm.truthy(t.is_maploaded)) found = t; });
            return found;
        });
        Assert.NotNull(closet);
        w.Run(Second);
        In(w, () => { Assert.False(Dm.truthy(closet.opened)); return 0; });
        float oxy0 = In(w, () => Dm.num(me.get_oxy_loss()));
        In(w, () => Waiting.NoSleep(me.forceMove(closet)));
        w.Run(60 * Second);

        var r = In(w, () =>
        {
            var air = closet.internal_air;
            return new Result(Dm.num(me.get_oxy_loss()) - oxy0, me.bodytemperature, Dm.num(((datum_gas_mixture)Waiting.NoSleep(closet.return_air())).temperature), air == null ? -1f : air.temperature, air == null ? -1f : Dm.num(air.total_moles()));
        });
        _output.WriteLine($"{typeof(T).Name}: oxy +{r.Oxy:0.#}, body {r.Body:0.0} K, air {r.CloseAir:0.0} K, internal {r.ClosetKelvin:0.0} K, {r.Moles:0.0} mol, stat {In(w, () => me.stat)}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
        return r;
    }

    [MetaServerFact]
    public void A_player_shut_in_an_ordinary_locker_breathes()
        => Assert.True(Shut<obj_structure_closet>(mapped: false).Oxy <= 0.5f);

    [MetaServerFact]
    public void A_player_shut_in_an_ordinary_crate_breathes()
        => Assert.True(Shut<obj_structure_closet_crate>(mapped: false).Oxy <= 0.5f);

    [MetaServerFact]
    public void A_player_shut_in_the_kitchen_freezer_locker_breathes_and_gets_cold()
    {
        var r = Shut<obj_structure_closet_secure_closet_freezer_kitchen>(mapped: true);
        Assert.True(r.Oxy <= 0.5f, $"oxy {r.Oxy}");
        Assert.True(r.Moles > 10, "the closet's own copy of the turf's air");
        Assert.InRange(r.ClosetKelvin, 213.1f, 213.2f); // 4 K a second, down to T0C - 60 (freezer.dm)
        Assert.True(r.Body < 300f, $"the body cooled to {r.Body} K");
    }

    [MetaServerFact]
    public void A_player_shut_in_the_freezer_crate_breathes_and_gets_cold()
    {
        var r = Shut<obj_structure_closet_crate_freezer>(mapped: true);
        Assert.True(r.Oxy <= 0.5f, $"oxy {r.Oxy}");
        Assert.True(r.Moles > 10, "the crate's own copy of the turf's air");
        Assert.InRange(r.ClosetKelvin, 213.1f, 213.2f);
        Assert.True(r.Body < 300f, $"the body cooled to {r.Body} K");
    }

    // tg: a sealed closet that was not map-loaded starts with an empty mixture (closets.dm:198): nothing to breathe, as in tg.
    [MetaServerFact]
    public void A_spawned_freezer_crate_has_an_empty_mixture_as_in_tg()
    {
        var r = Shut<obj_structure_closet_crate_freezer>(mapped: false);
        Assert.Equal(0f, r.Moles);
        Assert.True(r.Oxy > 0, "no air to breathe");
    }
}
