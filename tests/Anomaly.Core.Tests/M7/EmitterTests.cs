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
/// The M7 zaps slice: tg's emitter (modules/power/singularity/emitter.dm) on a cable fed by a test source: it is wrenched and
/// welded to the floor (wrench_act, welder_act: it joins the network), turned on by hand (interact), draws its active power from
/// the grid in SSmachines' early queue (process_early) and shows tg's lime overlay, turns orange and stops drawing without power,
/// and its controls lock with an engineering ID only while it is on (togglelock). Since the M7 projectiles slice its shot goes
/// when the delay has passed (fire_beam; the beam itself is ProjectileTests').
/// </summary>
public class EmitterTests
{
    private const int Second = 20;
    private const int LineY = 3, SourceX = 7, EmitterX = 3;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_test_m7apc_source Source, obj_machinery_power_emitter Emitter);

    private static Rig Room(ulong seed, bool welded)
    {
        obj_machinery_power_test_m7apc_source source = null;
        obj_machinery_power_emitter emitter = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            emitter = welded ? Dm.@new<obj_machinery_power_emitter_welded>(locate(EmitterX, LineY, 1)) : Dm.@new<obj_machinery_power_emitter>(locate(EmitterX, LineY, 1));
            emitter.dir = EAST;
        });
        r.In(() => source.output_energy = 2_000_000);
        mob_living_carbon_human bob = r.Join(EmitterX, LineY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        MachinesTick(r, 2);
        return new Rig(r, bob, source, emitter);
    }

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static string OverlayColor(Rig g) => g.R.In(() => Dm.each(g.Emitter.overlays).Select(o => o switch
    {
        mutable_appearance m when m.icon_state == "emitter_overlay" => Dm.str(m.color),
        DmAppearance d when d.icon_state == "emitter_overlay" => Dm.str(d.color),
        _ => null,
    }).FirstOrDefault(c => c != null));

    [Fact]
    public void AnEmitterIsWrenchedAndWeldedToTheFloorAndJoinsTheNetwork()
    {
        Rig g = Room(7501, welded: false);
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.anchored)));
        Assert.Null(g.R.In(() => g.Emitter.powernet));
        // a hand on a loose emitter: "needs to be firmly secured to the floor first!" (interact :172)
        WallTests.Hold<obj_item_wrench>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter, 4);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.anchored)), "the wrench anchors it (default_unfasten_wrench)");
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.welded)));
        // the lit welder welds it in two seconds (welder_act :316): it connects to the cable's network
        WallTests.Hold<obj_item_weldingtool>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter, 4);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.welded)), "the welder welds it to the floor");
        Assert.NotNull(g.R.In(() => g.Emitter.powernet));
        Assert.Same(g.R.In(() => Dm.locate<obj_structure_cable>(locate(EmitterX, LineY, 1)).powernet), g.R.In(() => g.Emitter.powernet));
        // a welded emitter cannot be unwrenched (can_be_unfasten_wrench :282); the welder cuts it free again
        Assert.Equal(FAILED_UNFASTEN, g.R.In(() => Dm.num(g.Emitter.can_be_unfasten_wrench(g.Bob, TRUE))));
        Click(g.R, g.Bob, g.Emitter, 4);
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.welded)));
        Assert.Null(g.R.In(() => g.Emitter.powernet));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AWeldedEmitterTurnsOnDrawsItsPowerAndShowsItsOverlayAndGoesOrangeWithoutPower()
    {
        Rig g = Room(7502, welded: true);
        datum_powernet net = g.R.In(() => g.Emitter.powernet as datum_powernet);
        Assert.NotNull(net);
        Assert.True(g.R.In(() => Dm.@in(g.Emitter, SSmachines.processing_early)), "the emitter draws early (Initialize :70)");
        Assert.Equal("emitter", g.R.In(() => g.Emitter.icon_state));
        Assert.Null(OverlayColor(g));

        // a hand turns it on (interact :184); the test holds the shot back (the delay starts now)
        Click(g.R, g.Bob, g.Emitter);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)));
        g.R.In(() => g.Emitter.last_shot = world.time + 600);
        float load0 = g.R.In(() => Dm.num(net.load));
        MachinesTick(g.R);
        // the active power (350 W less the servos, RefreshParts :115) over the machines' two seconds is the network's load
        float usage = g.R.In(() => g.Emitter.active_power_usage * Dm.num(SSmachines.wait) / 10f);
        Assert.Equal(300f, g.R.In(() => g.Emitter.active_power_usage)); // a tier-1 servo: 350 - 50
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.powered)));
        Assert.InRange(g.R.In(() => Dm.num(net.load)), usage - 1, usage + 1);
        Assert.Equal(COLOR_VIBRANT_LIME, OverlayColor(g));

        // without power: the source stops; the next cycles find no surplus, the emitter is unpowered and orange and draws nothing
        g.R.In(() => g.Source.output_energy = 0);
        MachinesTick(g.R, 3);
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.powered)));
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)), "it stays on, waiting for power");
        Assert.Equal(COLOR_ORANGE, OverlayColor(g));
        Assert.Equal(0f, g.R.In(() => Dm.num(net.load)));
        // power again, then the delay passes: the emitter shoots (fire_beam, emitter.dm:252; the M7 projectiles slice: its
        // flight and hits are ProjectileTests')
        g.R.In(() => g.Source.output_energy = 2_000_000);
        MachinesTick(g.R, 2);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.powered)));
        Assert.Empty(g.R.Room.Runtimes);
        g.R.In(() => g.Emitter.last_shot = 0);
        MachinesTick(g.R);
        Assert.Equal(1f, g.R.In(() => g.Emitter.shot_number));
        Assert.Empty(g.R.Room.Runtimes);
        // and a second hand turns it off: the overlay goes
        Click(g.R, g.Bob, g.Emitter);
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.active)));
        Assert.Null(OverlayColor(g));
    }

    [Fact]
    public void TheControlsLockWithAnEngineeringIdOnlyWhileTheEmitterIsOn()
    {
        Rig g = Room(7503, welded: true);
        // off: the lock refuses (togglelock :350)
        WallTests.Hold<obj_item_card_id_advanced_debug>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter);
        Assert.Equal(0f, g.R.In(() => g.Emitter.locked));
        Assert.False(g.R.In(() => Dm.truthy(g.Emitter.active)), "an ID does not switch it");
        // on, then locked with the engineering access (req_access ACCESS_ENGINE_EQUIP)
        g.R.In(() => Assert.True(Dm.@in(ACCESS_ENGINE_EQUIP, g.Emitter.req_access)));
        g.R.In(() => { foreach (object held in Dm.each(g.Bob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Click(g.R, g.Bob, g.Emitter);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)));
        g.R.In(() => g.Emitter.last_shot = world.time + 6000);
        WallTests.Hold<obj_item_card_id_advanced_debug>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter);
        Assert.Equal(1f, g.R.In(() => g.Emitter.locked));
        // a hand cannot switch a locked emitter off (interact :178); a card without the access cannot unlock it
        g.R.In(() => { foreach (object held in Dm.each(g.Bob.held_items.Copy())) if (held is obj_item i) qdel(i); });
        Click(g.R, g.Bob, g.Emitter);
        Assert.True(g.R.In(() => Dm.truthy(g.Emitter.active)), "the controls are locked");
        WallTests.Hold<obj_item_card_id>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter);
        Assert.Equal(1f, g.R.In(() => g.Emitter.locked));
        WallTests.Hold<obj_item_card_id_advanced_debug>(g.R, g.Bob);
        Click(g.R, g.Bob, g.Emitter);
        Assert.Equal(0f, g.R.In(() => g.Emitter.locked));
        // on and welded, it cannot be unwrenched or welded loose: "Turn it off first!"
        Assert.Equal(FAILED_UNFASTEN, g.R.In(() => Dm.num(g.Emitter.can_be_unfasten_wrench(g.Bob, TRUE))));
        g.R.AssertNoRuntimes();
    }
}
