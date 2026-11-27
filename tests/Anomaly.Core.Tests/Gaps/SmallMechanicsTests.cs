using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The MetaStation leftovers' small mechanics: concrete poured over a grille or a catwalk becomes a wet wall or floor that cures in its
/// time (with tg's grey filter while wet), the colour menu recolours a canister with the colour count of its GAGS configuration
/// (DV686), a lit cigarette's smoke and its smoker's are particle holders with tg's sets (DV687), and a lit lighter's flame flickers
/// (DV688).
/// </summary>
public class SmallMechanicsTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Listen(r);
        mob_living_carbon_human bob = r.Join(5, 4, "bob");
        r.Step(Second);
        return (r, bob);
    }

    private static obj_item_reagent_containers_cup_beaker Bucket(AudioRoom r, mob_living_carbon_human who, Type reagent, float amount) => r.In(() =>
    {
        var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker>(who.loc);
        Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(reagent, amount));
        Waiting.NoSleep(who.put_in_active_hand(beaker));
        return beaker;
    });

    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    [Fact]
    public void Concrete_poured_on_a_grille_makes_a_wet_wall_that_cures_in_a_minute()
    {
        var (r, bob) = Room(15321);
        // concrete's wall is poured over a grille (its girder_type), hexacrete's over a girder
        obj_structure_grille grille = r.In(() => Dm.@new<obj_structure_grille>(locate(6, 4, 1)));
        var bucket = Bucket(r, bob, typeof(datum_reagent_concrete), 25);
        r.In(() => Waiting.Detach(bucket.pour_concrete(grille, bob)));
        RunFor(r, 2 * SECONDS);
        var wall = r.In(() => locate(6, 4, 1)) as turf_closed_wall_concrete;
        Assert.NotNull(wall);
        Assert.True(r.In(() => wall.harden_lvl) < 1f);
        Assert.NotNull(r.In(() => wall.get_filter("harden")));
        Assert.Equal(15f, r.In(() => Dm.num(((datum_reagents)bucket.reagents).total_volume)), 3);
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("slosh", StringComparison.Ordinal));
        RunFor(r, 62 * SECONDS);
        Assert.Equal(1f, r.In(() => wall.harden_lvl));
        Assert.Null(r.In(() => wall.get_filter("harden")));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Concrete_splashed_on_a_catwalk_makes_a_soft_concrete_floor_that_hardens()
    {
        var (r, _) = Room(15322);
        r.In(() =>
        {
            // a catwalk is laid on a plating (its scrap goes back to it)
            turf plating = Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_open_floor_plating))) as turf;
            Waiting.NoSleep(plating.place_on_top(typeof(turf_open_floor_catwalk_floor)));
        });
        r.In(() =>
        {
            var concrete = Dm.@new<datum_reagent_concrete>();
            concrete.volume = 5;
            Waiting.NoSleep(concrete.expose_atom(locate(6, 4, 1), 5, TOUCH));
        });
        var floor = r.In(() => locate(6, 4, 1)) as turf_open_floor_concrete;
        Assert.NotNull(floor);
        Assert.Equal(0f, r.In(() => floor.harden_lvl));
        // a smoke machine's concrete (TOUCH | INHALE) pours nothing
        r.In(() =>
        {
            // a catwalk is laid on a plating (its scrap goes back to it)
            turf plating = Waiting.NoSleep(locate(7, 4, 1).ChangeTurf(typeof(turf_open_floor_plating))) as turf;
            Waiting.NoSleep(plating.place_on_top(typeof(turf_open_floor_catwalk_floor)));
        });
        r.In(() =>
        {
            var concrete = Dm.@new<datum_reagent_concrete>();
            concrete.volume = 5;
            Waiting.NoSleep(concrete.expose_atom(locate(7, 4, 1), 5, Dm.bitor(TOUCH, INHALE)));
        });
        Assert.IsType<turf_open_floor_catwalk_floor>(r.In(() => locate(7, 4, 1)));
        RunFor(r, 62 * SECONDS);
        Assert.Equal(1f, r.In(() => floor.harden_lvl));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_canisters_colour_menu_takes_its_configurations_colours_and_paints_the_canister()
    {
        var (r, bob) = Room(15323);
        var canister = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister>(locate(6, 4, 1)));
        var menu = r.In(() => Dm.@new<datum_greyscale_modify_menu>(
            canister, bob, Dm.list(Dm.text("[]", typeof(datum_greyscale_config_canister))), Dm.@new<datum_callback>(canister, nameof(canister.recolor)),
            "", typeof(datum_greyscale_config_canister), canister.greyscale_colors));
        Assert.NotNull(r.In(() => menu.config));
        Assert.Equal(1f, r.In(() => menu.config.expected_colors));
        Assert.True(r.In(() => Dm.@in("window-base", menu.config.icon_states)));
        var data = r.In(() => Waiting.NoSleep(menu.ui_data(bob)) as DmList);
        Assert.Equal(1, r.In(() => ((DmList)data["colors"]).len));
        Assert.StartsWith("gags:/datum/greyscale_config/canister|", r.In(() => Dm.str(menu.sprite_data["finished"])));
        var ui = r.In(() =>
        {
            var window = Dm.@new<datum_tgui>(bob, menu, "GreyscaleModifyMenu");
            window.status = UI_INTERACTIVE;
            return window;
        });

        r.In(() => Waiting.NoSleep(menu.ui_act("recolor", Dm.list(Dm.kv("color_index", "1"), Dm.kv("new_color", "#zz0000")), ui, null)));
        Assert.Equal("#6b6b80", r.In(() => Dm.str(menu.split_colors[1])));
        r.In(() => Waiting.NoSleep(menu.ui_act("recolor", Dm.list(Dm.kv("color_index", "1"), Dm.kv("new_color", "#FF0000")), ui, null)));
        Assert.Equal("#ff0000", r.In(() => Dm.str(menu.split_colors[1])));
        r.In(() => Waiting.NoSleep(menu.ui_act("apply", Dm.list(), ui, null)));
        Assert.Equal("#ff0000", r.In(() => Dm.str(canister.greyscale_colors)));
        Assert.Equal("gags:/datum/greyscale_config/canister|#ff0000", r.In(() => Dm.str(canister.icon)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lit_cigarette_smokes_and_its_smoker_puffs_through_particle_holders()
    {
        var (r, bob) = Room(15324);
        var cig = r.In(() => Dm.@new<obj_item_cigarette>(bob.loc));
        r.In(() => Waiting.NoSleep(cig.light__proc()));
        Assert.NotNull(r.In(() => cig.cig_smoke));
        var smoke = r.In(() => cig.cig_smoke.particles) as particles_smoke_cig;
        Assert.NotNull(smoke);
        Assert.Equal(0.75f, r.In(() => Dm.num(smoke.scale)), 3); // tg's 0.5, times 1.5 on a cigarette
        Assert.True(r.In(() => Dm.@in(cig.cig_smoke, cig.vis_contents)));

        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(cig, ITEM_SLOT_MASK)))));
        var puff = r.In(() => cig.mob_smoke);
        Assert.NotNull(puff);
        Assert.True(r.In(() => Dm.@in(puff, bob.vis_contents)));
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        Assert.Equal(8f, r.In(() => Dm.num(((DmList)((particles)puff.particles).position)[1])));
        r.In(() => Waiting.NoSleep(bob.setDir(WEST)));
        Assert.Equal(-8f, r.In(() => Dm.num(((DmList)((particles)puff.particles).position)[1])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lighters_flame_flickers()
    {
        var (r, bob) = Room(15325);
        var lighter = r.In(() => Dm.@new<obj_item_lighter>(bob.loc));
        Assert.NotNull(r.In(() => lighter.middleman));
        Assert.True(r.In(() => lighter.dm_light_flicker));
        r.In(() => qdel(lighter));
        r.AssertNoRuntimes();
    }
}
