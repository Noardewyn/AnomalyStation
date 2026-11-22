using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 8: the gravity generator's graviton field (gravitygenerator.dm:345, `gravity_field = new(src, 2, TRUE, 6)`):
/// tg's proximity monitor (proximity_monitor.dm, field.dm), its gravity field (fields/gravity.dm) and the forced_gravity
/// element (elements/forced_gravity.dm) on every turf within 2 tiles of the generator (the edge is a field), so the turfs
/// there have a gravity of 6 (the very high gravity alert, the gravity movespeed modifier, tg's crushing damage in Life)
/// while the rest of the level has the generator's 1. Disabling the generator deletes the field and takes the element off.
/// </summary>
public class GravityFieldTests
{
    [Fact]
    public void The_generators_field_forces_heavy_gravity_within_two_tiles_and_goes_with_the_generator()
    {
        // The test room (its area has tg's standard gravity); the generator at 5,4 (its parts 4..6, 4..6), the field's centre.
        var r = new AudioRoom(11, 11, seed: 8101);
        r.In(() =>
        {
            var gen = Dm.@new<obj_machinery_gravity_generator_main>(locate(5, 4, 1));
            datum_proximity_monitor_advanced_gravity field = gen.gravity_field;
            Assert.NotNull(field);
            Assert.Same(gen, field.host);
            Assert.Same(gen, field.hasprox_receiver);
            Assert.Equal(2f, Dm.num(field.current_range));
            Assert.Equal(6f, field.gravity_value);
            // field.dm:122: the field turfs are RANGE_TURFS(1), the edge turfs the ring at 2; gravity.dm:3 edge_is_a_field
            Assert.Equal(9, field.field_turfs.Count);
            Assert.Equal(16, field.edge_turfs.Count);
            Assert.Equal(25, field.modified_turfs.Count);
            Assert.True(GetComponent<datum_component_connect_range>(field) != null, "the monitor hooks its range (set_range)");
            Assert.Single(Dm.each_cast<datum_component_connect_containers>(field.GetComponents(typeof(datum_component_connect_containers)))); // set_host (COMPONENT_DUPE_SELECTIVE)

            turf edge = (turf)locate(3, 4, 1), inside = (turf)locate(5, 3, 1), outside = (turf)locate(2, 4, 1);
            Assert.True(HAS_TRAIT(edge, TRAIT_FORCED_GRAVITY));
            Assert.True(HAS_TRAIT(inside, TRAIT_FORCED_GRAVITY));
            Assert.False(HAS_TRAIT(outside, TRAIT_FORCED_GRAVITY));
            Assert.Equal(6f, Dm.num(edge.has_gravity()));
            Assert.Equal(1f, Dm.num(outside.has_gravity()));

            // A person walks into the edge: the very high gravity (living.dm:1261 update_gravity).
            var bob = Dm.@new<mob_living_carbon_human>(outside);
            Assert.Equal(1f, Dm.num(bob.gravity_state));
            Waiting.NoSleep(bob.forceMove(edge));
            Assert.Equal(6f, Dm.num(bob.gravity_state));
            Assert.IsType<atom_movable_screen_alert_veryhighgravity>(bob.alerts[ALERT_GRAVITY]);
            Assert.NotNull(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_gravity)));
            Waiting.NoSleep(bob.forceMove(outside));
            Assert.Equal(1f, Dm.num(bob.gravity_state));
            Assert.Null(bob.alerts[ALERT_GRAVITY]);
            Waiting.NoSleep(bob.forceMove(edge));
            Assert.Equal(6f, Dm.num(bob.gravity_state));

            // enable on a running generator keeps its field (gravitygenerator.dm:343, isnull(gravity_field)).
            Waiting.NoSleep(gen.enable());
            Assert.Same(field, gen.gravity_field);

            // Disable (tg's blackout path): the field is deleted (gravitygenerator.dm:360 QDEL_NULL), the element comes off
            // every turf (cleanup_field_turf -> RemoveElement), and the person on the old edge loses the forced gravity
            // (TRAIT_FORCED_GRAVITY's removal on the loc, init_signals.dm:312): the room's own gravity is left.
            gen.charge_count = 0;
            gen.breaker = 0;
            gen.set_power();
            Waiting.NoSleep(gen.disable());
            Assert.Null(gen.gravity_field);
            Assert.True(QDELETED(field));
            Assert.Equal(0, field.modified_turfs.Count);
            Assert.False(HAS_TRAIT(edge, TRAIT_FORCED_GRAVITY));
            Assert.False(HAS_TRAIT(inside, TRAIT_FORCED_GRAVITY));
            Assert.Equal(1f, Dm.num(edge.has_gravity()));
            Assert.Equal(1f, Dm.num(bob.gravity_state));
            Assert.Null(bob.alerts[ALERT_GRAVITY]);
        });
        r.AssertNoRuntimes();
    }

    private static T GetComponent<T>(datum d) where T : datum_component => d.GetComponent(typeof(T)) as T;
}
