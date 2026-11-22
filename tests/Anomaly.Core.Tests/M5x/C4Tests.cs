using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5x;

/// <summary>
/// M5x slice 4: C4 (grenades/plastic.dm) and the grenade base (_grenade.dm). Planting takes a 3 s do_after on the target
/// (moving cancels it), the charge leaves the hand for nullspace and its overlay goes on the target; after det_time seconds the
/// target takes a heavy ex_act of its own (a wall is dismantled, a mob is spared it) and a (0, 0, 3) blast follows, the overlay
/// cut; a pacifist cannot plant on a living target; a charge destroyed by another blast detonates (atom_deconstruct); the
/// grenade's screwdriver cycles its fuse through tg's list.
/// </summary>
public class C4Tests
{
    private const int Second = 20;
    private const int W = 20, H = 14;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        r.In(() =>
        {
            for (int x = 3; x <= W - 2; x++)
            {
                for (int y = 3; y <= H - 2; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
            Waiting.NoSleep(locate(10, 7, 1).ChangeTurf(typeof(turf_closed_wall), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        r.Step(Second);
        return r;
    }

    private static turf At(int x, int y) => locate(x, y, 1);

    private static bool HasSticker(atom a) => Dm.each_cast<object>(a.overlays ?? Dm.list()).OfType<DmAppearance>().Any(o => o.icon_state == "plastic-explosive2");

    private static obj_item_grenade_c4 GiveC4(AudioRoom r, mob_living_carbon_human who)
        => r.In(() =>
        {
            var c4 = Dm.@new<obj_item_grenade_c4>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(c4))));
            return c4;
        });

    [Fact]
    public void APlantedChargeDismantlesItsWallAfterItsTimerAndTheLightBlastFollows()
    {
        var r = Room(6401);
        var engineer = r.Dummy(9, 7);
        obj_item_grenade_c4 c4 = GiveC4(r, engineer);
        Assert.Equal(10f, c4.det_time); // seconds
        r.In(() => Waiting.Detach(c4.interact_with_atom(At(10, 7), engineer)));
        r.Step(Second);
        Assert.Same(engineer, r.In(() => c4.loc)); // the 3 s do_after
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.Null(c4.loc); // nullspace
            Assert.Same(At(10, 7), c4.target);
            Assert.True(Dm.truthy(c4.active));
            Assert.True(HasSticker(At(10, 7)), "the charge's overlay on the wall");
            Assert.Equal("plastic-explosive2", c4.plastic_overlay.icon_state);
            Assert.Equal(aim(engineer, At(10, 7)), c4.aim_dir);
        });
        // a step away to stand outside the heavy ex_act's reach (the light blast still reaches)
        r.In(() => Waiting.NoSleep(engineer.forceMove(At(4, 7))));
        r.Step(7 * Second);
        Assert.True(r.In(() => At(10, 7) is turf_closed_wall), "not before its 10 s");
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(c4));
            Assert.False(At(10, 7) is turf_closed, "dismantled: " + At(10, 7).type);
            Assert.False(HasSticker(At(10, 7)), "the overlay cut");
            // dismantle_wall(1, 1), devastate_wall: the wall's sheets and the girder's iron, then the light blast's throws scatter
            // them around the epicenter
            var sheets = Enumerable.Range(6, 9).SelectMany(x => Enumerable.Range(3, 9).SelectMany(y => Dm.each_cast<atom>(At(x, y).contents))).OfType<obj_item_stack_sheet_iron>().ToList();
            Assert.Equal(3f, sheets.Sum(st => Dm.num(st.amount)));
        });
        r.AssertNoRuntimes();

        static float aim(atom from, atom to) => get_dir(from, to);
    }

    // DV255: the face is the side of the tile the crosshair's ray went in by, in tile pixels with a tile's centre at 32 times its x
    [Theory]
    [InlineData(9, 7, 90, 8)] // straight east from the west: the west face
    [InlineData(9, 6.2, 45, 8)] // from the south-west, in through the west side's lower half
    [InlineData(9.8, 6, 20, 2)] // from the south-west, in through the south side
    [InlineData(10, 8, 180, 1)]
    [InlineData(11, 7, 270, 4)]
    [InlineData(10.2, 7, 0, 2)] // the eye on the tile: the side behind it
    public void TheFaceIsTheSideTheAimWentInBy(double eyeX, double eyeY, double yaw, int face)
    {
        var r = Room(6410);
        r.In(() =>
        {
            DmList modifiers = Dm.params2list(ClientSeam.AimParam(eyeX * 32, eyeY * 32, 26, yaw, 0));
            Assert.Equal(face, PointGeometry.FaceAimedAt(modifiers, At(10, 7)));
            Assert.Equal(0, PointGeometry.FaceAimedAt(Dm.list(), At(10, 7))); // no aim
        });
    }

    // DV255: a vending machine's own code would refuse the charge as stock, and one with no translated item_interaction would be guarded
    [Fact]
    public void AChargeSticksToAVendingMachineOnTheClickedFace()
    {
        var r = Room(6411);
        var engineer = r.Dummy(9, 7);
        var vending = r.In(() => Dm.@new<obj_machinery_vending_cola>(At(9, 8)));
        obj_item_grenade_c4 c4 = GiveC4(r, engineer);
        r.In(() =>
        {
            engineer.next_click = -1;
            DmList modifiers = Dm.params2list(ClientSeam.AimParam(9 * 32, 7 * 32, 26, 0, 0));
            Waiting.Detach(vending.base_item_interaction(engineer, c4, modifiers));
        });
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Equal(vending, c4.target);
            DmAppearance sticker = Dm.each_cast<object>(vending.overlays).OfType<DmAppearance>().Single(o => o.icon_state == "plastic-explosive2");
            Assert.Equal(SOUTH, sticker.dir);
        });
    }

    [Fact]
    public void MovingCancelsThePlantingAndThePacifistCannotPlantOnAPerson()
    {
        var r = Room(6402);
        var engineer = r.Dummy(9, 7);
        var victim = r.Dummy(8, 7);
        obj_item_grenade_c4 c4 = GiveC4(r, engineer);
        r.In(() => Waiting.Detach(c4.interact_with_atom(At(10, 7), engineer)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(engineer.forceMove(At(9, 8))));
        r.Step(3 * Second);
        Assert.Same(engineer, r.In(() => c4.loc));
        Assert.Null(r.In(() => c4.target));

        r.In(() => ADD_TRAIT(engineer, TRAIT_PACIFISM, "test"));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(c4.interact_with_atom(victim, engineer))));
        Assert.Same(engineer, r.In(() => c4.loc));
    }

    [Fact]
    public void AChargeOnAPersonSparesThemTheHeavyBlastButNotTheLightOne()
    {
        var r = Room(6403);
        var engineer = r.Dummy(9, 7);
        var victim = r.Dummy(8, 7);
        obj_item_grenade_c4 c4 = GiveC4(r, engineer);
        r.In(() => Waiting.Detach(c4.interact_with_atom(victim, engineer)));
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Same(victim, c4.target);
            Assert.Equal((float)FLOAT_LAYER, c4.plastic_overlay.layer);
            Waiting.NoSleep(engineer.forceMove(At(16, 11)));
        });
        r.Step(11 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(c4));
            float brute = Dm.num(victim.get_brute_loss());
            Assert.InRange(brute, 5f, 30f); // the light blast's (EXPLODE_LIGHT: 30 at most), never the heavy 60
            Assert.Equal(0f, Dm.num(victim.get_fire_loss()));
        });
    }

    [Fact]
    public void AChargeDestroyedByAnotherBlastDetonates()
    {
        var r = Room(6404);
        obj_item_grenade_c4 c4 = r.In(() => Dm.@new<obj_item_grenade_c4>(At(6, 5)));
        r.Step(1);
        r.In(() => Waiting.Detach(explosion(At(5, 5), 0, 2, 3, flame_range: 0, flash_range: 0, adminlog: FALSE, ignorecap: TRUE, silent: TRUE)));
        r.Step(1);
        r.In(() =>
        {
            Assert.True(QDELETED(c4), "a heavy blast's 100..250 damage breaks the 40-integrity charge");
            Assert.True(Dm.truthy(Dm.bitand(c4.dud_flags, GRENADE_USED)), "and it went off");
        });
    }

    [Fact]
    public void TheGrenadeScrewdriverCyclesTheFuseThroughTgsList()
    {
        var r = Room(6405);
        r.In(() =>
        {
            var g = Dm.@new<obj_item_grenade>(At(5, 5));
            Assert.Equal(50f, g.det_time);
            g.change_det_time(); // "5" is the last entry: back to the first
            Assert.Equal(0f, g.det_time);
            g.change_det_time(); // "Instant" -> "3"
            Assert.Equal(30f, g.det_time);
            g.change_det_time();
            Assert.Equal(40f, g.det_time);
            g.change_det_time(9); // the multitool's pick, clamped to 5 s
            Assert.Equal(50f, g.det_time);
        });
    }
}
