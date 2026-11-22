using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.R4;

/// <summary>
/// M3 R4, the owner's first walk on Meta: the station had no gravity (tg's station areas default to zero gravity; the
/// station's gravity generator gives its level gravity through GLOB.gravity_generators and SSmapping's gravity by z-level),
/// so tg's Process_Spacemove refused every step and the client's prediction was rolled back to the spawn. The generator's
/// gravity path is translated (modules/power/gravitygenerator.cs).
/// </summary>
public class GravityGeneratorTests
{
    [Fact]
    public void A_generator_gives_its_level_gravity_and_takes_it_away_when_disabled()
    {
        // The P2 fixture station (7x6, a station level with no generator); the generator stands at 4,3: its parts at 3..5, 3..5.
        World world = MapLoadTests.BootFixture(out _);
        using (world.Enter())
        {
            Assert.Equal(0f, Dm.num(SSmapping.gravity_by_z_level[1]));
            var gen = Dm.@new<obj_machinery_gravity_generator_main>(locate(4, 3, 1));
            DmList here = (DmList)GLOB.gravity_generators["1"];
            Assert.NotNull(here);
            Assert.Equal(1, here.Count);
            Assert.Same(gen, here[1]);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));

            // setup_parts: the 3x3 block from the bottom middle, eight parts; the middle one (count 5) holds the overlays,
            // the top row (counts 1 to 3) is not dense.
            Assert.Equal(8, gen.generator_parts.Count);
            Assert.Equal(5f, gen.center_part.sprite_number);
            Assert.Equal((4f, 4f), (gen.center_part.x, gen.center_part.y));
            foreach (object o in Dm.each(gen.generator_parts))
            {
                var part = (obj_machinery_gravity_generator_part)o;
                Assert.Same(gen, part.main_part);
                Assert.Equal(part.sprite_number > 3 ? 1f : 0f, Dm.num(part.density));
                Assert.Equal(Dm.text("on_[]", part.sprite_number), part.icon_state);
            }
            Assert.Equal("on_8", gen.icon_state);
            // M4b audio slice 5: the hum (/datum/looping_sound/gravgen) started with enable (gravitygenerator.dm:341): its start
            // sound plays on a sound token
            Assert.True(Dm.truthy(gen.soundloop.is_active()));
            Assert.NotNull(gen.soundloop.sound_token_instance);

            // tg's blackout (gravitygenerator.dm:459, an M10 stub here): the breaker off, set_power, disable. disable's
            // update_use_power runs power_change, which leaves the generator discharging; its next process finishes the
            // shutdown.
            gen.charge_count = 0;
            gen.breaker = 0;
            gen.set_power();
            Waiting.NoSleep(gen.disable());
            Assert.Equal(0, ((DmList)GLOB.gravity_generators["1"]).Count);
            Assert.False(Dm.truthy(gen.soundloop.is_active())); // disable stops the hum (gravitygenerator.dm:359)
            Assert.Equal(0f, Dm.num(SSmapping.gravity_by_z_level[1]));
            Assert.Equal("on_8", gen.icon_state); // discharging
            gen.process(null);
            Assert.Equal("off_8", gen.icon_state);

            // Charging back: the breaker on, set_power (POWER_UP), fifty processes of +2, then enable.
            gen.breaker = 1;
            gen.set_power();
            for (int n = 0; n < 50; n++)
                gen.process(null);
            Assert.Equal(0f, Dm.num(SSmapping.gravity_by_z_level[1]));
            Assert.Equal(100f, gen.charge_count);
            gen.process(null);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));
        }
    }

    [MetaFact]
    public void Metas_station_has_gravity_from_its_generator()
    {
        World world = MetaMaps.Boot(out _);
        using (world.Enter())
        {
            DmList gens = (DmList)GLOB.gravity_generators["1"];
            Assert.NotNull(gens);
            Assert.Equal(1, gens.Count);
            var gen = (obj_machinery_gravity_generator_main)gens[1];
            Assert.IsType<area_station_engineering_gravity_generator>(get_turf(gen).loc);
            Assert.Equal(1f, Dm.num(SSmapping.gravity_by_z_level[1]));
            Assert.Equal(1f, Dm.num(is_station_level(1f)));
            // A station hallway: tg's station areas have no gravity of their own (areas.dm:90), the level has the generator's.
            turf hall = null;
            foreach (object t in Dm.each(GLOB.station_turfs ?? Dm.list()))
            {
                if (((turf)t).loc is area_station_hallway) { hall = (turf)t; break; }
            }
            Assert.NotNull(hall);
            Assert.Equal(0f, Dm.num(((area)hall.loc).default_gravity));
            Assert.Equal(1f, Dm.num(hall.has_gravity()));
        }
    }
}
