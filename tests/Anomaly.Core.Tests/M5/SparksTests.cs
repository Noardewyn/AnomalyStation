using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 1: tg's sparks (effect_system/effects_sparks.dm): do_sparks spreads its sparks over the floor, each plays the
/// sparks sound and flicks its icon state, heats its tile (hotspot_expose) and is gone after its decay; the burning
/// component (datums/components/burning.dm) burns a flammable object down through take_damage and leaves ash.
/// </summary>
public class SparksTests
{
    private const int Second = 20;

    private static int Sparks(AudioRoom r) => r.In(() =>
    {
        int n = 0;
        for (int x = 1; x <= 12; x++)
            for (int y = 1; y <= 12; y++)
                n += Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<obj_effect_particle_effect_sparks>().Count();
        return n;
    });

    [Fact]
    public void DoSparksSpreadsSparksThatSoundAndDecay()
    {
        var r = new AudioRoom(12, 12, seed: 5121);
        mob_living_carbon_human bob = r.Join(6, 3, "Bob");
        r.Step(Second);
        r.Sounds.Heard.Clear();
        r.In(() => do_sparks(3, TRUE, locate(6, 6, 1)));
        Assert.Equal(3, Sparks(r));
        r.Step();
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith("sound/effects/sparks/sparks", StringComparison.Ordinal));
        r.Step(5 * Second);
        Assert.Equal(0, Sparks(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenLitTubeSparks()
    {
        var r = new AudioRoom(12, 12, seed: 5122);
        mob_living_carbon_human bob = r.Join(6, 3, "Bob");
        var light = r.In(() => Dm.@new<obj_machinery_light>(locate(6, 6, 1)));
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.truthy(light.on)), "the tube is lit");
        r.In(() => Waiting.NoSleep(light.break_light_tube()));
        Assert.True(Sparks(r) > 0, "break_light_tube's do_sparks (light.dm:702)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFlammableItemCatchesFireAndBurnsToAsh()
    {
        var r = new AudioRoom(10, 10, seed: 5123);
        var item = r.In(() =>
        {
            var o = Dm.@new<obj_item_storage_toolbox_mechanical>(locate(5, 5, 1));
            Dm.combine(ref o.resistance_flags, FLAMMABLE);
            Dm.mask(ref o.resistance_flags, Dm.bitnot(FIRE_PROOF));
            return o;
        });
        r.Step();
        r.In(() => Waiting.NoSleep(item.fire_act(300, 100)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(item.resistance_flags, ON_FIRE))), "the burning component sets ON_FIRE");
        Assert.NotNull(r.In(() => item.GetComponent(typeof(datum_component_burning))));
        r.Step(60 * Second);
        Assert.True(r.In(() => QDELETED(item)), "burnt down by SSburning's take_damage (burning.dm:77)");
        Assert.NotEmpty(r.In(() => Dm.each_cast<atom>(locate(5, 5, 1).contents).OfType<obj_effect_decal_cleanable_ash>().ToList()));
        r.AssertNoRuntimes();
    }
}
