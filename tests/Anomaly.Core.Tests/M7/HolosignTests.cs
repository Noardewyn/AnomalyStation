using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice T3 (D164): the holosign projectors (holosign_creator.dm) and the holosigns they make (holosign.dm): tg's unit test
/// place_holosign (the janitor's projector puts a wet floor holobarrier on the next tile), the engineering barrier that a runner cannot
/// pass and a walker can, the holofan (the ATMOS projector's holofirelock) that holds the air of a breached room and lets people through,
/// the projector's limit and its clearing, the sign's translucent picture and its glitch when touched.
/// </summary>
public class HolosignTests
{
    private const int Second = 20;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Right = "icon-x=16;icon-y=16;right=1;button=right";

    private static (AudioRoom R, mob_living_carbon_human Bob, ChatCapture Chat) Build(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: extras);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        return (r, bob, chat);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static void Click(AudioRoom r, mob who, atom target, string how = Left, float seconds = 1.5f)
    {
        r.Command(() => ClientSeam.Click(who.client, target, how));
        r.Step((int)(seconds * Second));
    }

    private static List<T> SignsAt<T>(AudioRoom r, int x, int y) where T : obj_structure_holosign => r.In(() => Dm.each(locate(x, y, 1).contents).OfType<T>().ToList());

    // tg's unit test place_holosign (unit_tests/holofan_placement.dm)
    [Fact]
    public void TheJanitorsProjectorPutsAWetFloorHolobarrierOnTheNextTile()
    {
        var (r, bob, _) = Build(8501);
        var projector = Hold<obj_item_holosign_creator_janibarrier>(r, bob);
        Click(r, bob, r.In(() => locate(4, 5, 1)));
        obj_structure_holosign_barrier_wetsign sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_wetsign>(r, 4, 5));
        r.In(() =>
        {
            Assert.Same(projector, sign.projector);
            Assert.Single(Dm.each(projector.signs));
            Assert.Equal("holosign_dense", sign.icon_state);
            Assert.True(Dm.truthy(sign.density));
            Assert.True(Dm.truthy(sign.anchored));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEngineeringBarrierIsDenseStopsARunnerLetsAWalkerAndTheUnconsciousThroughAndPlacesAfterItsSecond()
    {
        var (r, bob, chat) = Build(8502);
        var projector = Hold<obj_item_holosign_creator_engineering>(r, bob);
        r.In(() =>
        {
            Assert.Equal("signmaker_engi", projector.icon_state);
            Assert.Equal(12f, projector.max_signs);
            Assert.Equal(1 * SECONDS, projector.creation_time);
        });
        // the projector's second (creation_time 1 SECONDS): not yet there after half a second, there after a whole
        r.Command(() => ClientSeam.Click(bob.client, locate(4, 5, 1), Left));
        r.Step(Second / 2);
        Assert.Empty(SignsAt<obj_structure_holosign_barrier_engineering>(r, 4, 5));
        r.In(() => Assert.True(Dm.truthy(projector.holocreator_busy)));
        r.Step(Second);
        var sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_engineering>(r, 4, 5));
        r.In(() =>
        {
            Assert.False(Dm.truthy(projector.holocreator_busy));
            Assert.Equal("holosign_engi", sign.icon_state);
            Assert.Equal(1f, sign.max_integrity);
            Assert.Equal(RAD_LIGHT_INSULATION, Dm.num(sign.rad_insulation)); // "slightly blocks radiation"
            Assert.True(Dm.truthy(sign.density));
            Assert.Equal(ATMOS_PASS_YES, Dm.num(sign.can_atmos_pass)); // it does not hold air: that is the holofan
            Assert.Contains("It is currently maintaining <b>1/12</b> projections.", string.Join(" ", Dm.each(Waiting.NoSleep(projector.examine(bob))).Select(Dm.str)));

            // "Can only be passed by walking" (holosign.dm:100)
            bob.move_intent = MOVE_INTENT_RUN;
            Assert.False(Dm.truthy(Waiting.NoSleep(sign.CanAllowThrough(bob, EAST))));
            bob.move_intent = MOVE_INTENT_WALK;
            Assert.True(Dm.truthy(Waiting.NoSleep(sign.CanAllowThrough(bob, EAST))));
            bob.move_intent = MOVE_INTENT_RUN;
            // not an unconscious body: "Lets not prevent dragging unconscious/dead people"
            Waiting.NoSleep(bob.Unconscious(100));
            Assert.True(Dm.truthy(Waiting.NoSleep(sign.CanAllowThrough(bob, EAST))));
        });
        // the same sign on a tile that has one is refused, and a dense tile is refused
        r.In(() => Waiting.NoSleep(bob.SetUnconscious(0)));
        Click(r, bob, r.In(() => locate(4, 5, 1)));
        Assert.Single(SignsAt<obj_structure_holosign_barrier_engineering>(r, 4, 5));
        r.AssertNoRuntimes();
        _ = chat;
    }

    [Fact]
    public void TheProjectorsLimitRefusesTheLastSignAndZClearsEveryHologram()
    {
        var (r, bob, chat) = Build(8503);
        var projector = Hold<obj_item_holosign_creator_atmos>(r, bob); // 6 signs, no wait
        r.In(() => Assert.Equal(6f, projector.max_signs));
        // the projector reaches the tiles next to the user (interact_with_atom, not the ranged one): the six round Bob at (3, 5)
        (int X, int Y)[] around = { (4, 4), (4, 5), (4, 6), (3, 4), (3, 6), (2, 5) };
        foreach ((int x, int y) in around)
            Click(r, bob, r.In(() => locate(x, y, 1)), seconds: 0.5f);
        r.In(() =>
        {
            Assert.Equal(6f, projector.signs.len);
            Assert.Contains("It is currently maintaining <b>6/6</b> projections.", string.Join(" ", Dm.each(Waiting.NoSleep(projector.examine(bob))).Select(Dm.str)));
        });
        // the seventh: "max capacity!" and no sign
        Click(r, bob, r.In(() => locate(2, 4, 1)), seconds: 0.5f);
        Assert.Empty(SignsAt<obj_structure_holosign_barrier_atmos>(r, 2, 4));
        r.In(() => Assert.Equal(6f, projector.signs.len));
        // Z (attack_self) clears them all
        r.In(() => Waiting.NoSleep(projector.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(length(projector.signs)));
            Assert.Empty(Dm.each(world.contents).OfType<obj_structure_holosign>());
        });
        // and a projector deleted takes its signs along
        Click(r, bob, r.In(() => locate(4, 5, 1)), seconds: 0.5f);
        Assert.Single(SignsAt<obj_structure_holosign_barrier_atmos>(r, 4, 5));
        r.In(() => qdel(projector));
        r.Step(2);
        Assert.Empty(SignsAt<obj_structure_holosign_barrier_atmos>(r, 4, 5));
        r.AssertNoRuntimes();
        _ = chat;
    }

    // a wall at x = 7, the room west of it, the space east of it; the doorway (7, 5) is a wall until the test opens it
    private static void Breach(World w)
    {
        for (int y = 1; y <= 9; y++) // the room's top row too: an airless gap there leaked as soon as SSair's stagger woke it (gaps C1)
            Dm.@new<turf_closed_wall>(locate(7, y, 1));
        for (int x = 8; x <= 12; x++)
            for (int y = 1; y <= 8; y++)
                Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
    }

    // The toy room starts without air (SSair sets its turfs up after the map extras): fill the room as a station's (100 moles at 20 C, about
    // 97 kPa), then open the doorway.
    private static void FillAndOpen(AudioRoom r) => r.In(() =>
    {
        for (int x = 1; x <= 6; x++)
            for (int y = 1; y <= 9; y++)
                if (locate(x, y, 1) is turf_open open)
                    open.air = SSair.parse_gas_string("o2=21;n2=79;TEMP=293.15") as datum_gas_mixture;
        Waiting.NoSleep(locate(7, 5, 1).ChangeTurf(typeof(turf_open_floor_plating)));
    });

    private static float Pressure(AudioRoom r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    [Fact]
    public void TheHolofanHoldsTheAirOfABreachedRoomAndLetsAPlayerWalkThrough()
    {
        // without the sign the doorway vents the room into space ...
        var (open, _, _) = Build(8504, Breach);
        FillAndOpen(open);
        open.Step(15 * Second);
        float vented = Pressure(open, 5, 5);
        Assert.True(vented < 60, "the doorway vents: " + vented + " kPa");

        // ... with the holofan in it the room keeps its air
        var (r, bob, _) = Build(8505, Breach);
        FillAndOpen(r);
        float start = Pressure(r, 5, 5);
        Assert.True(start > 90, "the sealed room at the start: " + start + " kPa");
        var projector = Hold<obj_item_holosign_creator_atmos>(r, bob);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(6, 5, 1)))); // next to the doorway
        r.Step(2);
        Click(r, bob, r.In(() => locate(7, 5, 1)), seconds: 0.5f);
        obj_structure_holosign_barrier_atmos sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_atmos>(r, 7, 5));
        r.In(() =>
        {
            Assert.Equal(ATMOS_PASS_NO, Dm.num(sign.can_atmos_pass));
            Assert.False(Dm.truthy(sign.density)); // "it does not prevent solid objects from passing through, gas is kept out"
            Assert.True(Dm.truthy(sign.block_superconductivity()));
            Assert.True(HAS_TRAIT(locate(7, 5, 1), TRAIT_FIREDOOR_STOP));
        });
        r.Step(15 * Second);
        float held = Pressure(r, 5, 5);
        Assert.True(held > 90, "the room keeps its air behind the holofan: " + held + " kPa");
        Assert.True(held > vented + 30);

        // a person walks through it (even running): density is off
        r.In(() =>
        {
            bob.move_intent = MOVE_INTENT_RUN;
            Waiting.NoSleep(bob.forceMove(locate(6, 5, 1)));
        });
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(locate(7, 5, 1), EAST)))));
        r.In(() => Assert.Same(locate(7, 5, 1), get_turf(bob)));
        // taking the sign away lets the air go
        r.In(() => qdel(sign));
        r.Step(30 * Second); // the vent's pace follows SSair's phase in the seeded Master (silicons SI3: one more subsystem shifted it)
        Assert.True(Pressure(r, 5, 5) < held - 30, "the air goes once the sign is gone: " + Pressure(r, 5, 5) + " kPa, held " + held);
        r.AssertNoRuntimes();
        _ = projector;
    }

    [Fact]
    public void ASignIsTranslucentItsPictureIsAVisOverlayAndTheHolofansClearviewMakesItAlmostInvisible()
    {
        var (r, bob, chat) = Build(8506);
        var projector = Hold<obj_item_holosign_creator_atmos>(r, bob);
        Click(r, bob, r.In(() => locate(4, 5, 1)), seconds: 0.5f);
        var sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_atmos>(r, 4, 5));
        r.In(() =>
        {
            // create_vis_overlay (holosign.dm:64): the sign itself is alpha 0 and its picture, holosigns.dmi's translucent holo_firelock, a
            // vis overlay that keeps its own alpha (RESET_ALPHA) above mobs
            Assert.Equal(0f, Dm.num(sign.alpha));
            var overlay = Assert.IsType<obj_effect_overlay_vis>(Assert.Single(Dm.each(sign.vis_contents)));
            Assert.Equal("holo_firelock", overlay.icon_state);
            Assert.Equal(255f, Dm.num(overlay.alpha));
            Assert.True(Dm.truthy(Dm.bitand(overlay.appearance_flags, RESET_ALPHA)));
            Assert.True(Dm.truthy(Dm.bitand(overlay.appearance_flags, KEEP_APART)));
            Assert.Equal(ABOVE_MOB_LAYER, Dm.num(overlay.layer));
            Assert.Single(Dm.each(sign.managed_vis_overlays));
        });
        // right Z: clearview (holosign_creator.dm:186): the sign is alpha 25, no mouse, a new overlay without RESET_ALPHA; 40 s later it resets
        r.In(() => Waiting.NoSleep(projector.attack_self_secondary(bob)));
        r.In(() =>
        {
            Assert.True(Dm.truthy(projector.clearview));
            Assert.Equal(25f, Dm.num(sign.alpha));
            Assert.Equal(MOUSE_OPACITY_TRANSPARENT, Dm.num(sign.mouse_opacity));
            var overlay = (obj_effect_overlay_vis)Dm.each(sign.vis_contents).Single();
            Assert.False(Dm.truthy(Dm.bitand(overlay.appearance_flags, RESET_ALPHA)));
        });
        r.Step(41 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(projector.clearview));
            Assert.Equal(150f, Dm.num(sign.alpha)); // initial(alpha)
            var overlay = (obj_effect_overlay_vis)Dm.each(sign.vis_contents).Single();
            Assert.True(Dm.truthy(Dm.bitand(overlay.appearance_flags, RESET_ALPHA)));
        });
        // a colour on the projector reaches the sign's overlay
        r.AssertNoRuntimes();
        _ = chat;
    }

    [Fact]
    public void TouchingASignMakesItGlitchForAMomentAndASwatBreaksIt()
    {
        var (r, bob, _) = Build(8507);
        var projector = Hold<obj_item_holosign_creator_atmos>(r, bob);
        Click(r, bob, r.In(() => locate(4, 5, 1)), seconds: 0.5f);
        var sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_atmos>(r, 4, 5));
        r.In(() => Assert.Empty(Dm.each(sign.filter_data)));
        // a dense mob entering its tile (holographic_nature.dm:40) starts the glitch: tg's seven wibbly filters ...
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 5, 1))));
        r.Step(2);
        r.In(() => Assert.Equal(7f, Dm.num(length(sign.filter_data))));
        // ... which go after 0.45 s and the 0.25 s of their removal
        r.Step(Second);
        r.In(() => Assert.Equal(0f, Dm.num(length(sign.filter_data))));
        // a punch (attack_hand: attack_holosign, 5 brute) breaks the 1-integrity sign
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 5, 1))));
        Click(r, bob, sign, seconds: 1);
        Assert.Empty(SignsAt<obj_structure_holosign_barrier_atmos>(r, 4, 5));
        r.In(() => Assert.Equal(0f, Dm.num(length(projector.signs))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpBreaksASignAndThePenliteBarrierFitsEveryoneWhileDiseasesAreNotPorted()
    {
        var (r, bob, _) = Build(8508);
        var projector = Hold<obj_item_holosign_creator_medical>(r, bob);
        Click(r, bob, r.In(() => locate(4, 5, 1)), seconds: 1.5f);
        var sign = Assert.Single(SignsAt<obj_structure_holosign_barrier_medical>(r, 4, 5));
        r.In(() =>
        {
            Assert.Equal("holo_medical", sign.icon_state);
            Assert.True(Dm.truthy(Waiting.NoSleep(sign.CanAllowThrough(bob, EAST)))); // nobody carries a virus yet
            Waiting.NoSleep(sign.emp_act(EMP_HEAVY));
        });
        r.Step(2);
        Assert.Empty(SignsAt<obj_structure_holosign_barrier_medical>(r, 4, 5));
        r.AssertNoRuntimes();
        _ = projector;
    }
}
