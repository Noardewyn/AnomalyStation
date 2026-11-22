using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Inits;

/// <summary>M3 I1a on the converted MetaStation: no preloader type mismatches, no random poster checker, no maintenance spawner left.</summary>
public class I1aMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public I1aMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Meta_boots_with_its_posters_picked_and_its_maintenance_loot_spawned()
    {
        using (_meta.World.Enter())
        {
            // task 1: the 5 "type mismatch" runtimes of the lead's real boot (solar_control and crematorium ids) are gone
            Assert.DoesNotContain(_meta.Log.Runtimes, e => e.Message.Contains("type mismatch", StringComparison.Ordinal));

            int posters = 0, randomPosters = 0, checkers = 0, spawnersLeft = 0, cleanables = 0, cleanablesLive = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is obj_effect_spawner_random_maintenance) spawnersLeft++;
                        if (thing is obj_effect_decal_cleanable)
                        {
                            cleanables++;
                            if (!InertSeam.IsInert(thing)) cleanablesLive++;
                        }
                        if (thing is not obj_structure_sign_poster poster) continue;
                        posters++;
                        if (Dm.initial(poster.GetType(), (obj_structure_sign_poster p) => p.random_basetype) == null) continue;
                        randomPosters++;
                        if (poster.icon_state is "random_anything" or "random_official" or "random_contraband") checkers++;
                    }
                }
            }
            _output.WriteLine($"Meta: {posters} posters, {randomPosters} random posters picked, {checkers} checkers; {spawnersLeft} maintenance spawners left; {cleanables} cleanable decals ({cleanablesLive} live); runtimes {_meta.Log.Runtimes.Count}");
            foreach (var group in _meta.Log.Runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message).OrderByDescending(g => g.Count()))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(92, randomPosters); // MetaStation.dmm: 25 random, 28 official, 39 contraband random posters
            Assert.Equal(0, checkers); // task 2: no random_anything checker after boot
            Assert.Equal(0, spawnersLeft); // task 3: every maintenance spawner spawned and deleted itself (misc.dm:34)
        }
    }
}
