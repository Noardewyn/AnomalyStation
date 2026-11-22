using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 6 (D146): tg's jetpack (tanks/jetpack.dm, components/jetpack.dm), its full-speed movespeed modifier
/// (movespeed/modifiers/items.dm:9), the ion trail (effect_system.dm, effects_trail.dm) and the locomotion bridge's zero-gravity
/// driver: a weightless capsule mob moves by tg's own client/Move tile steps (keyLoop's call each tick the wish is held), with
/// the client's intended_direction set from the wish, and the capsule follows loc with slides. The jetpack's buttons are its
/// item actions (toggle_jetpack, jetpack_stabilization, set_internals), clicked through the client seam.
/// </summary>
public class JetpackTests
{
    private const int Second = 20;
    private const int C = 256;
    private const float Thrust = 0.01f; // jetpack.dm:29, allow_thrust's num
    private const string InternalsOn = "sound/items/internals/internals_on.ogg";

    private sealed class FlickRecorder : IAppearanceSink
    {
        public readonly List<(object Target, object State)> Flicks = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) { }

        public void Flick(object target, object state) => Flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    // A room of space (3..14, 3..13) inside the floor rim, a godmode player with a CO2 jetpack on the back.
    private static (CapsuleRoom r, mob_living_carbon_human bob, obj_item_tank_jetpack_carbondioxide jet) Flier(ulong seed, int x = 4, int y = 8)
    {
        var r = new CapsuleRoom(seed);
        r.In(() =>
        {
            for (int tx = 3; tx <= 14; tx++)
                for (int ty = 3; ty <= 13; ty++)
                    Waiting.NoSleep(locate(tx, ty, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        var jet = r.In(() =>
        {
            var j = Dm.@new<obj_item_tank_jetpack_carbondioxide>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(j, ITEM_SLOT_BACK))), "the jetpack goes on the back");
            return j;
        });
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())), "the room is weightless");
        return (r, bob, jet);
    }

    private static void Click<T>(CapsuleRoom r, mob who, obj_item item) where T : datum_action
    {
        atom button = r.In(() =>
        {
            datum_action action = Assert.Single(Dm.each_cast<datum_action>(item.actions), a => a is T);
            return action.viewers[who.hud_used] as atom;
        });
        Assert.NotNull(button);
        r.Command(() => ClientSeam.Click(who.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second / 2);
    }

    private static float Moles(CapsuleRoom r, obj_item_tank t) => r.In(() => Dm.num(t.air_contents.total_moles()));

    private static float Slowdown(CapsuleRoom r, mob m) => r.In(() => Dm.num(m.cached_multiplicative_slowdown));

    private static int Trails(CapsuleRoom r) => r.In(() => Dm.each_cast<object>(world.contents).Count(o => o is obj_effect_particle_effect_ion_trails));

    [Fact]
    public void TheCaptainsJetpackIsLiveAndItsButtonLightsIt()
    {
        // the owner's play test (2026-10-15): the Captain's jetpack showed no buttons, inert without its steal-objective hook
        var (r, bob, co2) = Flier(6609);
        var jet = r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(co2));
            var j = Dm.@new<obj_item_tank_jetpack_captain>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(j, ITEM_SLOT_BACK))), "the Captain's jetpack goes on the back");
            return j;
        });
        r.Step(Second / 2);
        Assert.NotEmpty(r.In(() => Dm.each_cast<datum_action>(jet.actions).ToList()));
        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Assert.EndsWith("-on", r.In(() => Dm.str(jet.icon_state)));
    }

    [Fact]
    public void TheToggleButtonLightsTheJetpackWithItsSpeedTraitAndOnState()
    {
        var (r, bob, jet) = Flier(6601);
        // nothing to push off and no jetpack: the held key moves nothing (Process_Spacemove, client/Move :94)
        float before = Slowdown(r, bob);
        (int X, int Y) start = r.Tile(bob);
        r.Walk(bob, 127, 0, Second, yaw: 16384);
        r.Walk(bob, 0, 0, 1, yaw: 16384); // let go (a mob without a frame repeats its last one)
        Assert.Equal(start, r.Tile(bob));
        Assert.Equal(MotionDriver.ZeroG, r.State(bob).Driver);
        Assert.False(r.Params(bob).CanMove);
        Assert.Equal("jetpack-black", r.In(() => jet.icon_state));
        // tg's CO2 fill: 6 atmospheres in 70 litres at 20 C (jetpack.dm:76)
        float full = Moles(r, jet);
        Assert.Equal(6 * ONE_ATMOSPHERE * 70f / (R_IDEAL_GAS_EQUATION * T20C), full, 3);

        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Assert.Equal(full - Thrust, Moles(r, jet), 4); // the activation check uses one thrust (components/jetpack.dm:101)
        r.In(() =>
        {
            Assert.Equal(TRUE, jet.on);
            Assert.Equal(FALSE, jet.stabilize); // tg's default: the stabilizer is off (jetpack.dm:16)
            Assert.Equal("jetpack-black-on", jet.icon_state); // jetpack.dm:106
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed))));
            Assert.True(HAS_TRAIT_FROM(bob, TRAIT_NOGRAV_ALWAYS_DRIFT, JETPACK_TRAIT)); // jetpack.dm:118
            Assert.Equal(1f / 1.5f, bob.inertia_move_multiplier_active, 4); // components/jetpack.dm:112, drift_force 1.5 newtons
            var component = jet.GetComponent(typeof(datum_component_jetpack)) as datum_component_jetpack;
            Assert.Equal(TRUE, component.active);
            Assert.NotNull(component.trail);
        });
        // the full-speed modifier counts only while floating (movetypes FLOATING): -0.3 deciseconds per tile
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(bob.movement_type, FLOATING))));
        Assert.Equal(before - 0.3f, Slowdown(r, bob), 3);
        Assert.True(r.Params(bob).CanMove); // the preview asks the jetpack (PreviewSpacemoveSignal)

        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        r.In(() =>
        {
            Assert.Equal(FALSE, jet.on);
            Assert.Equal("jetpack-black", jet.icon_state);
            Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed))));
            Assert.False(HAS_TRAIT(bob, TRAIT_NOGRAV_ALWAYS_DRIFT));
            Assert.Equal(1f, bob.inertia_move_multiplier_active, 4);
        });
        Assert.Equal(before, Slowdown(r, bob), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithTheStabilizerTheHeldKeyStepsTileByTileAndTheReleaseStops()
    {
        var (r, bob, jet) = Flier(6602);
        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Click<datum_action_item_action_jetpack_stabilization>(r, bob, jet);
        r.In(() =>
        {
            Assert.Equal(TRUE, jet.stabilize);
            Assert.False(HAS_TRAIT(bob, TRAIT_NOGRAV_ALWAYS_DRIFT)); // configure_jetpack, jetpack.dm:61
        });
        float slowdown = Slowdown(r, bob);
        float moles = Moles(r, jet);
        (int X, int Y) start = r.Tile(bob);
        var xs = new List<int> { start.X };
        const int ticks = 18; // the space ends at x 14, the floor at 15 has gravity
        for (int i = 0; i < ticks; i++)
        {
            r.Walk(bob, 127, 0, 1, yaw: 16384);
            (int X, int Y) t = r.Tile(bob);
            Assert.Equal(start.Y, t.Y);
            Assert.InRange(t.X - xs[^1], 0, 1); // tile by tile
            if (t.X != xs[^1])
                xs.Add(t.X);
            r.AssertOnLoc(bob);
            Assert.Equal(MotionDriver.ZeroG, r.State(bob).Driver);
        }
        Assert.Equal(EAST, r.In(() => Dm.num(bob.client.intended_direction)));
        int steps = xs.Count - 1;
        // client/Move's cadence: move_delay grows by the slowdown each step (mob_movement.dm:133), the first step at once
        int expected = (int)MathF.Floor(ticks * 0.5f / slowdown) + 1;
        Assert.InRange(steps, expected - 1, expected + 1);
        // each step used tg's 0.01 moles of the CO2 (move_react, allow_thrust with use_fuel)
        Assert.Equal(moles - steps * Thrust, Moles(r, jet), 4);
        // the view keeps the dir: the player looks east and flew east; the steps never turned the body (DV15)
        Assert.Equal(EAST, r.In(() => bob.dir));

        // released: the stabilizer stops the drift (the step's newtonian_move asks Process_Spacemove, which the jetpack stops)
        (int X, int Y) stop = r.Tile(bob);
        r.Walk(bob, 0, 0, 2 * Second, yaw: 16384);
        Assert.Equal(stop, r.Tile(bob));
        Assert.Null(r.In(() => bob.drift_handler));
        Assert.Equal(0f, r.In(() => Dm.num(bob.client.intended_direction)));
        Assert.Equal(moles - steps * Thrust, Moles(r, jet), 4);
        r.AssertOnLoc(bob);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutTheStabilizerTheFlierKeepsDriftingUntilTheStabilizerGoesOn()
    {
        var (r, bob, jet) = Flier(6603, x: 4, y: 6);
        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Assert.Equal(FALSE, r.In(() => jet.stabilize));
        // one push east, then the key is let go
        r.Walk(bob, 127, 0, 2, yaw: 16384);
        r.Walk(bob, 0, 0, 1, yaw: 16384);
        Assert.NotNull(r.In(() => bob.drift_handler)); // the step's newtonian_move started a drift (TRAIT_NOGRAV_ALWAYS_DRIFT)
        (int X, int Y) released = r.Tile(bob);
        float moles = Moles(r, jet);
        r.Walk(bob, 0, 0, Second, yaw: 16384);
        (int X, int Y) drifted = r.Tile(bob);
        Assert.True(drifted.X >= released.X + 2, $"drifted east from {released} to {drifted}");
        Assert.Equal(released.Y, drifted.Y);
        Assert.Equal(moles, Moles(r, jet), 5); // no key held: the drift uses no gas (move_react, :136)
        r.AssertOnLoc(bob);

        // the stabilizer on: the drift's next step halts it (after_move's Process_Spacemove, drift_handler.dm:151)
        Click<datum_action_item_action_jetpack_stabilization>(r, bob, jet);
        r.Walk(bob, 0, 0, Second / 2, yaw: 16384);
        Assert.Null(r.In(() => bob.drift_handler));
        (int X, int Y) halted = r.Tile(bob);
        Assert.True(halted.X <= drifted.X + 2, "the stabilizer stopped the drift at once: " + halted);
        r.Walk(bob, 0, 0, Second, yaw: 16384);
        Assert.Equal(halted, r.Tile(bob));
        r.AssertOnLoc(bob);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OutOfGasTheJetpackTurnsItselfOffAndThePreviewHasNoSideEffect()
    {
        var (r, bob, jet) = Flier(6604);
        // leave two and a half thrusts in the tank: turning on uses one (check_on_activation is the same allow_thrust with
        // use_fuel, jetpack.dm:53, components/jetpack.dm:101)
        r.In(() => jet.air_contents.remove(Dm.num(jet.air_contents.total_moles()) - 2.5f * Thrust));
        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Click<datum_action_item_action_jetpack_stabilization>(r, bob, jet);
        Assert.Equal(TRUE, r.In(() => jet.on));
        Assert.True(r.Params(bob).CanMove);
        // the preview reads the gas without using it
        float lit = Moles(r, jet);
        Assert.InRange(lit, 1.49f * Thrust, 1.51f * Thrust);
        r.Walk(bob, 0, 0, Second, yaw: 16384);
        Assert.Equal(lit, Moles(r, jet));

        (int X, int Y) start = r.Tile(bob);
        r.Walk(bob, 127, 0, Second, yaw: 16384);
        // one step used 0.01; the next client/Move's check (allow_thrust with 0.005 left) turned the jetpack off (:134)
        Assert.Equal(start.X + 1, r.Tile(bob).X);
        r.In(() =>
        {
            Assert.Equal(FALSE, jet.on);
            Assert.Equal("jetpack-black", jet.icon_state);
            Assert.False(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_jetpack_full_speed))));
        });
        Assert.Equal(lit - Thrust, Moles(r, jet), 5);
        Assert.False(r.Params(bob).CanMove);

        // lit again with too little gas, the preview says no and leaves the jetpack on (tg's allow_thrust would turn it off)
        r.In(() => jet.on = TRUE);
        r.Walk(bob, 0, 0, 2, yaw: 16384);
        Assert.False(r.Params(bob).CanMove);
        Assert.Equal(TRUE, r.In(() => jet.on));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIonTrailFlicksBehindEachStepAndFades()
    {
        var (r, bob, jet) = Flier(6605);
        var sink = new FlickRecorder();
        PresentationSinks.Of(r.World).Appearance = sink;
        Click<datum_action_item_action_toggle_jetpack>(r, bob, jet);
        Click<datum_action_item_action_jetpack_stabilization>(r, bob, jet);
        Assert.Equal(0, Trails(r));
        (int X, int Y) start = r.Tile(bob);
        var spots = new List<(int, int)>();
        for (int i = 0; i < 12 && r.Tile(bob).X < start.X + 3; i++)
        {
            r.Walk(bob, 127, 0, 1, yaw: 16384);
            spots.AddRange(r.In(() => Dm.each_cast<object>(world.contents).OfType<obj_effect_particle_effect_ion_trails>().Select(t => ((int)t.x, (int)t.y))));
        }
        Assert.Equal(start.X + 3, r.Tile(bob).X);
        // a particle on each turf the flier left (pre_move_react's oldposition), the ion_fade state flicked, then blank
        var left = spots.Distinct().OrderBy(s => s.Item1).ToList();
        Assert.Equal(new List<(int, int)> { (start.X, start.Y), (start.X + 1, start.Y), (start.X + 2, start.Y) }, left);
        r.In(() =>
        {
            foreach (var trail in Dm.each_cast<object>(world.contents).OfType<obj_effect_particle_effect_ion_trails>())
            {
                Assert.Equal("", trail.icon_state);
                Assert.Equal(EAST, trail.dir); // set_dir: the holder's dir (effects_trail.dm:84)
                Assert.Equal("icons/effects/effects.dmi", Dm.text("[]", trail.icon));
            }
        });
        Assert.True(sink.Flicks.Count(f => f.Target is obj_effect_particle_effect_ion_trails && Dm.eq(f.State, "ion_fade")) >= 3);
        // qdel_in_time 2 seconds (effects_trail.dm:100)
        r.Walk(bob, 0, 0, 2 * Second + 2, yaw: 16384);
        Assert.Equal(0, Trails(r));

        // The space play test (2026-10-15): flying east while looking north (DV15 keeps the view's dir on the capsule mob), the particles face
        // the flight as tg's turned mob would give them (set_dir: the step's direction), not the view.
        (int X, int Y) before = r.Tile(bob);
        for (int i = 0; i < 12 && r.Tile(bob).X < before.X + 2; i++)
            r.Walk(bob, 127, 0, 1, yaw: 0);
        Assert.Equal(before.X + 2, r.Tile(bob).X);
        r.In(() =>
        {
            Assert.Equal(NORTH, bob.dir);
            var trails = Dm.each_cast<object>(world.contents).OfType<obj_effect_particle_effect_ion_trails>().ToList();
            Assert.NotEmpty(trails);
            Assert.All(trails, t => Assert.Equal(EAST, t.dir));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWishSetsIntendedDirectionAndKeyLoopLeavesACapsuleMobToTheBridge()
    {
        // on a floor with gravity the capsule walks continuously (DV15); SSinput's keyLoop sees the intended direction but
        // must not step the mob a whole tile a second time
        var room = new CapsuleRoom(seed: 6606);
        mob_living_carbon_human a = room.Join(5, 5, "Alice");
        room.Walk(a, 127, 0, 1, yaw: 16384);
        Assert.Equal(EAST, room.In(() => Dm.num(a.client.intended_direction)));
        Assert.Equal(5 * C + 85, room.State(a).X); // BridgeTests' one running tick
        room.Walk(a, 127, 0, 1, yaw: 16384);
        Assert.Equal(5 * C + 170, room.State(a).X);
        Assert.Equal((6, 5), room.Tile(a)); // the centre crossed into 6 (tile 5 ends at 5 * 256 + 128); keyLoop would be at 7
        Assert.Equal(MotionDriver.Grounded, room.State(a).Driver);
        room.Walk(a, 0, 0, 1, yaw: 16384);
        Assert.Equal(0f, room.In(() => Dm.num(a.client.intended_direction)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheJetpackDroppedOrInAHandTurnsOffAndItsButtonsPlayTgsSounds()
    {
        // tg's jetpack plays no sound of its own (no playsound in jetpack.dm or components/jetpack.dm); its set_internals
        // button is the tank's internals toggle, which plays internals_on (tanks.dm:93)
        var r = new AudioRoom(9, 9, seed: 6607);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var jet = r.In(() =>
        {
            var mask = Dm.@new<obj_item_clothing_mask_breath>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(mask, ITEM_SLOT_MASK))));
            var j = Dm.@new<obj_item_tank_jetpack_carbondioxide>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(j, ITEM_SLOT_BACK))));
            return j;
        });
        r.Step(Second);
        datum_action Action<T>() => r.In(() => Dm.each_cast<datum_action>(jet.actions).Single(a => a is T));
        void Press(datum_action action)
        {
            atom button = r.In(() => action.viewers[bob.hud_used] as atom);
            r.Command(() => ClientSeam.Click(bob.client, button, "icon-x=16;icon-y=16;left=1;button=left"));
            r.Step(Second);
        }
        Assert.Equal(3, r.In(() => Dm.each_cast<datum_action>(jet.actions).Count())); // set_internals, toggle_jetpack, jetpack_stabilization

        r.Sounds.Heard.Clear();
        Press(Action<datum_action_item_action_set_internals>());
        Assert.Same(jet, r.In(() => bob.@internal));
        Assert.Single(r.Sounds.Of(bob, InternalsOn));
        Press(Action<datum_action_item_action_set_internals>());
        Assert.Null(r.In(() => bob.@internal));

        r.Sounds.Heard.Clear();
        Press(Action<datum_action_item_action_toggle_jetpack>());
        Assert.Equal(TRUE, r.In(() => jet.on));
        Assert.Equal("jetpack-black-on", r.In(() => jet.icon_state));
        Assert.DoesNotContain(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob));
        // the stabilizer's button is available only while the jetpack is on (toggles.dm:74)
        Press(Action<datum_action_item_action_jetpack_stabilization>());
        Assert.Equal(TRUE, r.In(() => jet.stabilize));

        // into a hand: the slot is not the back, so it turns off (jetpack.dm:65)
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(jet))));
            Assert.Equal(FALSE, jet.on);
            Assert.Equal("jetpack-black", jet.icon_state);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(jet))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(jet, ITEM_SLOT_BACK, disable_warning: TRUE, bypass_equip_delay_self: TRUE))));
        });
        Press(Action<datum_action_item_action_toggle_jetpack>());
        Assert.Equal(TRUE, r.In(() => jet.on));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(jet))))); // dropped: off (:70)
        Assert.Equal(FALSE, r.In(() => jet.on));
        r.AssertNoRuntimes();
    }
}
