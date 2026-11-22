using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// Engineering E7 (singularity containment): tg's field generator (modules/power/singularity/field_generator.dm) and its
/// containment field (containment_field.dm) in a booted toy room. A loose generator is wrenched (secured) and welded to the floor,
/// a hand turns it on and it warms up for 15 s (+a1..+a3); two active generators four tiles apart make the three fields between
/// them (setup_field), which hold the air and stop the singularity and the energy ball; a man who walks into a field is
/// paralysed, burnt and thrown back (yeet_shock, bump_field); an emitter's beam on a generator charges it (bullet_act, the +p
/// overlays); the online pair draws its power every machines cycle (calc_power, draw_power) and shuts down when it runs out, the
/// fields gone. An EMP does nothing to it (empprotection).
/// </summary>
public class FieldGeneratorTests
{
    private const int Second = 20;
    private const int LineY = 5, WestX = 3, EastX = 7;

    private static AudioRoom Room(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: extras);
        r.Step(Second);
        return r;
    }

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o switch
    {
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        string s => s,
        _ => null,
    }).Where(s => s != null).ToList());

    private static List<obj_machinery_field_containment> Fields(AudioRoom r) => r.In(() => Dm.each<obj_machinery_field_containment>().ToList());

    /// Two generators that start on (anchored, welded, turn_on at post_machine_initialize), on the line four tiles apart.
    private static (AudioRoom R, obj_machinery_field_generator West, obj_machinery_field_generator East) Pair(ulong seed)
    {
        obj_machinery_field_generator west = null, east = null;
        AudioRoom r = Room(seed, w =>
        {
            west = Dm.@new<obj_machinery_field_generator_starts_on>(locate(WestX, LineY, 1));
            east = Dm.@new<obj_machinery_field_generator_starts_on>(locate(EastX, LineY, 1));
        });
        return (r, west, east);
    }

    [Fact]
    public void AGeneratorIsWrenchedAndWeldedToTheFloorThenTurnedOnByHandAndWarmsUp()
    {
        obj_machinery_field_generator gen = null;
        AudioRoom r = Room(7601, w => gen = Dm.@new<obj_machinery_field_generator>(locate(WestX, LineY, 1)));
        mob_living_carbon_human bob = r.Join(WestX, LineY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        Assert.False(r.In(() => Dm.truthy(gen.anchored)));
        Assert.Equal(0f, r.In(() => Dm.num(gen.state)));
        // a hand on a loose generator: "needs to be firmly secured to the floor first!" (interact :91)
        Click(r, bob, gen);
        Assert.Equal(0f, r.In(() => Dm.num(gen.active)));
        // the wrench secures it (default_unfasten_wrench, set_anchored :115: FG_SECURED)
        WallTests.Hold<obj_item_wrench>(r, bob);
        Click(r, bob, gen, 4);
        Assert.True(r.In(() => Dm.truthy(gen.anchored)));
        Assert.Equal(1f, r.In(() => Dm.num(gen.state)));
        // the lit welder welds it in two seconds (welder_act :153: FG_WELDED); welded, it cannot be unwrenched
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, gen, 4);
        Assert.Equal(2f, r.In(() => Dm.num(gen.state)));
        Assert.Equal(FAILED_UNFASTEN, r.In(() => Dm.num(gen.can_be_unfasten_wrench(bob, TRUE))));
        // an empty hand turns it on (turn_on :226: FG_CHARGING, the turf's TRAIT_CONTAINMENT_FIELD)
        r.In(() => { foreach (object held in Dm.each(bob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Click(r, bob, gen);
        Assert.Equal(1f, r.In(() => Dm.num(gen.active)));
        Assert.True(r.In(() => HAS_TRAIT(get_turf(gen), TRAIT_CONTAINMENT_FIELD)));
        // on, it cannot be switched off by hand, unwrenched or welded loose
        Click(r, bob, gen);
        Assert.Equal(1f, r.In(() => Dm.num(gen.active)));
        Assert.Equal(FAILED_UNFASTEN, r.In(() => Dm.num(gen.can_be_unfasten_wrench(bob, TRUE))));
        // it warms up a step every five seconds (warm_up :236): +a1, +a2, +a3, then start_fields; alone it links to nothing and is
        // online half a second later
        r.Step(5 * Second);
        Assert.Contains("+a1", Overlays(r, gen));
        r.Step(5 * Second);
        Assert.Contains("+a2", Overlays(r, gen));
        r.Step(5 * Second + 12);
        Assert.Contains("+a3", Overlays(r, gen));
        Assert.Equal(2f, r.In(() => Dm.num(gen.active)));
        Assert.Empty(Fields(r));
        Assert.DoesNotContain("+on", Overlays(r, gen));
        Assert.Equal(INFINITY, r.In(() => Dm.num(gen.move_resist)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoActiveGeneratorsMakeTheFieldsBetweenThemThatHoldTheAirAndStopTheSingularityAndTheBall()
    {
        (AudioRoom r, obj_machinery_field_generator west, obj_machinery_field_generator east) = Pair(7602);
        Assert.Equal(1f, r.In(() => Dm.num(west.active)));
        r.Step(16 * Second);
        List<obj_machinery_field_containment> fields = Fields(r);
        Assert.Equal(3, fields.Count);
        r.In(() =>
        {
            Assert.Equal(new float[] { WestX + 1, WestX + 2, WestX + 3 }, fields.Select(f => f.x).OrderBy(x => x));
            Assert.All(fields, f => Assert.Equal((float)LineY, f.y));
            Assert.All(fields, f => Assert.Equal((float)EAST, f.dir));
            Assert.All(fields, f => Assert.Same(west, f.field_gen_1)); // the west one's EAST pass came first (setup_field 4 at 0.3 s)
            Assert.Equal(3f, west.fields.len);
            Assert.Equal(3f, east.fields.len);
            Assert.True(Dm.@in(east, west.connected_gens));
            Assert.True(Dm.@in(west, east.connected_gens));
            Assert.Equal(2f, Dm.num(west.active));
            foreach (obj_machinery_field_containment f in fields)
            {
                turf t = get_turf(f);
                Assert.True(HAS_TRAIT(t, TRAIT_CONTAINMENT_FIELD), "give_turf_traits");
                Assert.Equal((float)ATMOS_PASS_NO, Dm.num(f.can_atmos_pass));
                Assert.Equal("Contain_F", f.icon_state);
            }
        });
        Assert.Contains("+on", Overlays(r, west));
        Assert.Contains("+on", Overlays(r, east));
        // a singularity next to the field cannot move onto it (singularity.dm:411: the turf's trait; the component's can_move
        // :266: COMSIG_ATOM_SINGULARITY_TRY_MOVE on the field and on an active generator)
        r.In(() =>
        {
            var singulo = Dm.@new<obj_singularity>(locate(WestX + 2, LineY + 3, 1), 1);
            var c = (datum_component_singularity)singulo.GetComponent(typeof(datum_component_singularity));
            turf fieldTurf = locate(WestX + 2, LineY, 1);
            Assert.False(Dm.truthy(singulo.can_move(fieldTurf)));
            Assert.False(Dm.truthy(c.can_move(fieldTurf)));
            Assert.False(Dm.truthy(c.can_move(get_turf(west))));
            Assert.True(Dm.truthy(c.can_move(locate(WestX + 2, LineY + 1, 1))));
            qdel(singulo);
            var ball = Dm.@new<obj_energy_ball>(locate(WestX + 2, LineY + 3, 1));
            Assert.False(Dm.truthy(ball.can_move(fieldTurf)));
            Assert.True(Dm.truthy(ball.can_move(locate(WestX + 2, LineY + 1, 1))));
            qdel(ball);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AManWhoWalksIntoTheFieldIsParalysedBurntAndThrownBack()
    {
        (AudioRoom r, obj_machinery_field_generator west, obj_machinery_field_generator east) = Pair(7603);
        r.Step(16 * Second);
        Assert.Equal(3, Fields(r).Count);
        mob_living_carbon_human ann = r.Join(WestX + 2, LineY - 1, "Ann");
        r.Step(2);
        obj_machinery_field_containment field = r.In(() => Dm.locate<obj_machinery_field_containment>(locate(WestX + 2, LineY, 1)));
        r.In(() => Waiting.NoSleep(step(ann, NORTH)));
        // the field refuses her (CanAllowThrough :132) and she bumps it (Bumped :121, yeet_shock :137, bump_field :158): she is
        // thrown south (throw_at an edge turf, speed 4) to the room's wall: DV416, on an arc that leaves at once and crosses its tiles as it flies
        Assert.True(r.In(() => ann.dm_flight.IsMoving), "thrown away from the field");
        Assert.True(r.In(() => Dm.truthy(ann.IsParalyzed())), "Paralyze(10 SECONDS)");
        Assert.True(r.In(() => Dm.num(ann.get_fire_loss())) > 0, "electrocute_act");
        Assert.True(r.In(() => Dm.truthy(field.has_shocked)));
        Assert.NotEmpty(r.Sounds.Of(ann, "sound/effects/gravhit.ogg"));
        r.Step(Second);
        Assert.True(r.In(() => ann.y) < LineY - 1, "thrown away from the field");
        Assert.False(r.In(() => Dm.truthy(field.has_shocked)), "clear_shock half a second later");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmitterBeamChargesAGeneratorAndThePairShutsDownWhenItsPowerRunsOut()
    {
        (AudioRoom r, obj_machinery_field_generator west, obj_machinery_field_generator east) = Pair(7604);
        Assert.Equal(20f, r.In(() => Dm.num(west.power)));
        // an emitter's beam (30 laser damage) on the west generator: bullet_act :185 adds it, up to 250; laser armour 100 keeps it whole
        r.In(() =>
        {
            obj_machinery_power_emitter emitter = Dm.@new<obj_machinery_power_emitter>(locate(WestX - 1, LineY, 1));
            emitter.dir = EAST;
            Waiting.NoSleep(emitter.fire_beam());
        });
        r.Step(2);
        Assert.Equal(50f, r.In(() => Dm.num(west.power)));
        Assert.Equal(1f, r.In(() => west.power_level)); // round(6 * 50 / 250)
        Assert.Contains("+p1", Overlays(r, west));
        Assert.Equal(r.In(() => west.max_integrity), r.In(() => Dm.num(west.get_integrity())));
        r.In(() => { west.power = 245; Waiting.NoSleep(west.bullet_act(Dm.@new<obj_projectile_beam_emitter_hitscan>(get_turf(west)))); });
        Assert.Equal((float)field_generator_max_power, r.In(() => Dm.num(west.power)));
        Assert.Contains("+p6", Overlays(r, west));
        r.In(() => west.power = 20);

        // online with three fields each: a machines cycle draws round((2 + 3) * 0.5, 1) = 3 from each (calc_power :246)
        r.Step(16 * Second);
        Assert.Equal(3, Fields(r).Count);
        float before = r.In(() => Dm.num(east.power));
        r.Step(r.In(() => (int)Dm.num(SSmachines.wait)) * 2);
        Assert.Equal(before - 3, r.In(() => Dm.num(east.power)));
        // EMP-proof (empprotection, EMP_PROTECT_SELF)
        r.In(() => Waiting.NoSleep(west.emp_act(EMP_HEAVY)));
        Assert.Equal(2f, r.In(() => Dm.num(west.active)));

        // nothing charges them: they run dry (draw_power asks the linked one when its own is gone); the first that cannot draw
        // shuts down (turn_off :207) and the fields go with both generators' links (cleanup :356); the other stays online, drawing
        // round(2 * 0.5, 1) = 1 a cycle, until its own power is gone too
        for (int t = 0; t < 30 * Second && Fields(r).Count > 0; t++)
            r.Step(1);
        Assert.Empty(Fields(r));
        Assert.Equal(1, r.In(() => new[] { west, east }.Count(g => Dm.num(g.active) == 0)));
        for (int t = 0; t < 60 * Second && r.In(() => Dm.num(west.active) + Dm.num(east.active)) > 0; t++)
            r.Step(1);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(west.active));
            Assert.Equal(0f, Dm.num(east.active));
            Assert.Equal(0f, Dm.num(west.power));
            Assert.Equal(0f, west.fields.len);
            Assert.Equal(0f, east.connected_gens.len);
            Assert.False(HAS_TRAIT(locate(WestX + 2, LineY, 1), TRAIT_CONTAINMENT_FIELD));
            Assert.Equal((float)ATMOS_PASS_YES, Dm.num(west.can_atmos_pass));
        });
        Assert.DoesNotContain("+on", Overlays(r, west));
        // and cool down a step every five seconds (cool_down :218)
        Assert.Contains("+a3", Overlays(r, east).Concat(Overlays(r, west)));
        r.Step(15 * Second + 2);
        Assert.DoesNotContain(Overlays(r, west).Concat(Overlays(r, east)), o => o.StartsWith("+a", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FourGeneratorsInASquareCloseTheLoopAndLayTheShieldFloorInside()
    {
        var gens = new List<obj_machinery_field_generator>();
        AudioRoom r = Room(7605, w =>
        {
            foreach ((int x, int y) in new[] { (3, 3), (7, 3), (3, 7), (7, 7) })
                gens.Add(Dm.@new<obj_machinery_field_generator_starts_on>(locate(x, y, 1)));
        });
        float floorCapacity = r.In(() => locate(5, 5, 1).heat_capacity);
        r.Step(16 * Second);
        // three fields on each side; each generator links to two (setup_field), so each lays the square between itself and the
        // generator both its links share (shield_floor :375, place_floor :406): 5 x 5 shields, one a tile
        Assert.Equal(12, Fields(r).Count);
        Assert.All(gens, g => Assert.Equal(2f, r.In(() => g.connected_gens.len)));
        List<obj_effect_shield> shields = r.In(() => Dm.each<obj_effect_shield>().ToList());
        Assert.Equal(25, shields.Count);
        Assert.Equal(25, r.In(() => shields.Select(sh => get_turf(sh)).Distinct().Count()));
        Assert.Equal(INFINITY, r.In(() => locate(5, 5, 1).heat_capacity)); // shield.dm:15
        Assert.Equal("wave2", r.In(() => shields[0].icon_state));
        // one generator off: the shield floor goes and the turfs' heat capacity comes back; its cleanup (:356) makes each linked
        // generator clean up too, so every field of the loop drops while the other three stay online without fields
        r.In(() => gens[0].turn_off());
        r.Step(2);
        Assert.Empty(r.In(() => Dm.each<obj_effect_shield>().ToList()));
        Assert.Equal(floorCapacity, r.In(() => locate(5, 5, 1).heat_capacity));
        Assert.Empty(Fields(r));
        Assert.All(gens.Skip(1), g => Assert.Equal(2f, r.In(() => Dm.num(g.active))));
        Assert.All(gens, g => Assert.Equal(0f, r.In(() => g.connected_gens.len)));
        r.AssertNoRuntimes();
    }
}
