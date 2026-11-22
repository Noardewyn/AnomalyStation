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
/// The M7 delamination slice (G6, D167): the three anomaly kinds only the delamination's effect_anomaly makes. The vortex pulls,
/// throws and blasts (anomalies_vortex.dm), the bioscrambler pursues the nearest conscious carbon and swaps an organ and a limb of
/// those around it (anomalies_bioscrambler.dm, carbon_defense.dm:606; DV89: never an inert type), the dimensional anomaly turns the
/// tiles around it into a theme and goes away when its teleports run out (anomalies_dimensional.dm, anomalies_dimensional_themes.dm).
/// </summary>
public class DelamAnomalyTests
{
    private const int Second = 20;
    private const int X = 8, Y = 6;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(15, 12, seed: seed);
        r.Step(Second);
        // the room's area counts as the station's, as SSmapping registers the station level's areas (mapping.dm:509): a dimensional
        // anomaly with nothing left to transmute relocates through the anomaly placer, which picks a station area (anomaly_placer.dm:10)
        r.In(() => Dm.append(ref GLOB.the_station_areas, ((area)get_area(locate(2, 2, 1))).type));
        return r;
    }

    private static T Make<T>(AudioRoom r, params object[] args) where T : obj_effect_anomaly => r.In(() =>
    {
        var a = (T)Dm.@new(typeof(T), new object[] { locate(X, Y, 1) }.Concat(args).ToArray());
        a.move_chance = 0;
        return a;
    });

    private static bool Any<T>(AudioRoom r, turf at) where T : atom => r.In(() => Dm.each<T>(at.contents).Any(a => !QDELETED(a)));

    [Fact]
    public void TheVortexPullsLooseThingsAndBlastsItsRingAndDetonatesInAWave()
    {
        AudioRoom r = Room(7961);
        r.Join(X + 3, Y + 3, "Ann"); // a listener
        r.Step(2);
        obj_effect_anomaly_bhole vortex = Make<obj_effect_anomaly_bhole>(r, 2 * SECONDS, FALSE);
        obj_item wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(X + 1, Y, 1)));
        r.In(() =>
        {
            // a ring of radius 1, every pull and every turf (affect_coord, anomalies_vortex.dm:35-64)
            Waiting.NoSleep(vortex.grav(1, EXPLODE_HEAVY, 100, 100));
            Assert.Same(locate(X, Y, 1), wrench.loc);
            Assert.True(Dm.@in(locate(X + 1, Y, 1), SSexplosions.medturf), "the heavy ring's turfs are queued for SSexplosions");
            Assert.True(Dm.@in(locate(X - 1, Y - 1, 1), SSexplosions.medturf));
            Waiting.NoSleep(vortex.detonate());
        });
        Assert.True(Any<obj_effect_temp_visual_circle_wave_vortex_small>(r, r.In(() => locate(X, Y, 1))), "the small vortex wave (:67)");
        Assert.Contains(r.Sounds.Heard, s => s.File == "sound/effects/hallucinations/far_noise.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBioscramblerPursuesAndScramblesTheOrgansAndLimbsAroundIt()
    {
        AudioRoom r = Room(7962);
        mob_living_carbon_human bob = r.Join(X + 4, Y, "Bob");
        r.Step(2);
        // the pursuer's target is the nearest conscious carbon on its level (:60-75)
        obj_effect_anomaly_bioscrambler hunter = Make<obj_effect_anomaly_bioscrambler>(r, null, FALSE);
        Assert.Same(bob, r.In(() => hunter.pursuit_target?.resolve()));
        r.In(() => Waiting.NoSleep(hunter.move_anomaly()));
        Assert.Equal(X + 1, r.In(() => (int)hunter.x));
        r.In(() => qdel(hunter));

        // the docile one the delamination makes stays (:79) and pulses every 10 seconds within 2 tiles (:23-31)
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(X + 2, Y, 1))));
        obj_effect_anomaly_bioscrambler_docile docile = Make<obj_effect_anomaly_bioscrambler_docile>(r, null, FALSE);
        var partsBefore = r.In(() => Dm.each<obj_item_bodypart>(bob.bodyparts).Select(p => p.GetType()).ToList());
        r.In(() => Waiting.NoSleep(docile.anomalyEffect(2)));
        Assert.True(Any<obj_effect_temp_visual_circle_wave_bioscrambler>(r, r.In(() => locate(X, Y, 1))));
        Assert.Contains(r.Sounds.Heard, s => s.File == "sound/effects/magic/cosmic_energy.ogg");
        Assert.Equal(r.In(() => world.time) + 100, r.In(() => docile.pulse_cooldown));
        // tg's lists: every organ but the blacklisted and robotic ones, every limb but the blacklisted and robotic ones (:653-668)
        Assert.True(r.In(() => GLOB.bioscrambler_valid_organs.len) > 100);
        Assert.False(r.In(() => Dm.@in(typeof(obj_item_organ_lungs), GLOB.bioscrambler_valid_organs)));
        Assert.False(r.In(() => Dm.@in(typeof(obj_item_organ_heart_cybernetic), GLOB.bioscrambler_valid_organs)));
        // ten more scrambles: an organ marked ORGAN_MUTANT (:624) or a limb of another type (:631-635), of live types only (DV89)
        r.In(() =>
        {
            for (int i = 0; i < 10; i++)
                Waiting.NoSleep(bob.bioscramble("the test"));
        });
        bool mutant = r.In(() => Dm.each<obj_item_organ>(bob.organs).Any(o => Dm.truthy(Dm.bitand(o.organ_flags, ORGAN_MUTANT))));
        var partsAfter = r.In(() => Dm.each<obj_item_bodypart>(bob.bodyparts).Select(p => p.GetType()).ToList());
        Assert.True(mutant || !partsAfter.SequenceEqual(partsBefore), "something has changed about Bob");
        Assert.All(r.In(() => Dm.each<obj_item_organ>(bob.organs).Select(o => o.GetType()).ToList()), t => Assert.False(InertSeam.IsInertType(t)));
        Assert.All(partsAfter, t => Assert.False(InertSeam.IsInertType(t)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDimensionalAnomalyTurnsTheTilesIntoItsThemeAndGoesAwayAtTheEnd()
    {
        AudioRoom r = Room(7963);
        r.Join(2, 2, "Ann"); // a listener
        r.Step(2);
        // the toy room's floor is indestructible, which no theme converts (:90): iron floors and a wall around the anomaly
        r.In(() =>
        {
            for (int x = X - 3; x <= X + 3; x++)
                for (int y = Y - 3; y <= Y + 3; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(x == X + 3 && y == Y ? typeof(turf_closed_wall) : typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        obj_effect_anomaly_dimensional dim = Make<obj_effect_anomaly_dimensional>(r, null, FALSE);
        Assert.InRange(r.In(() => Dm.num(dim.teleports_left)), 1, 4);
        Assert.Equal(r.In(() => ((DmList)valid_subtypesof(typeof(datum_dimension_theme))).len), r.In(() => SSmaterials.dimensional_themes.len)); // init_subtypes_w_path_keys (materials.dm:65)
        r.In(() =>
        {
            dim.prepare_area(typeof(datum_dimension_theme_natural));
            Assert.IsType<datum_dimension_theme_natural>(dim.theme);
            Assert.True(dim.target_turfs.len > 0);
            Assert.Contains(Dm.each_cast<object>(dim.overlays).OfType<DmAppearance>(), a => a.icon_state == "map_flower"); // the theme's icon (:77-82)
            Assert.Contains(Dm.each_cast<object>(dim.overlays).OfType<DmAppearance>(), a => a.icon_state == "dimensional_overlay");
        });
        turf first = r.In(() => dim.target_turfs[1] as turf);
        (int fx, int fy) = r.In(() => ((int)first.x, (int)first.y));
        float left = r.In(() => dim.target_turfs.len);
        r.In(() => Waiting.NoSleep(dim.transmute_area()));
        Assert.Equal(left - 1, r.In(() => dim.target_turfs.len));
        // the natural theme's grass (anomalies_dimensional_themes.dm:300), with its flash and sound
        Assert.IsType<turf_open_floor_grass>(r.In(() => locate(fx, fy, 1)));
        Assert.True(Any<obj_effect_temp_visual_transmute_tile_flash>(r, r.In(() => locate(fx, fy, 1))));
        // the natural theme's walls are wood (:301)
        r.In(() =>
        {
            Waiting.NoSleep(dim.theme.apply_theme(locate(X + 3, Y, 1)));
            Assert.IsType<turf_closed_wall_mineral_wood_nonmetal>(locate(X + 3, Y, 1));
        });
        Assert.Contains(r.Sounds.Heard, s => s.File == "sound/effects/magic/blind.ogg");
        // no tiles and no teleports left: it detonates, which deletes it (:48-50, :95)
        r.In(() =>
        {
            dim.target_turfs = Dm.list();
            dim.teleports_left = 0;
            Waiting.NoSleep(dim.transmute_area());
        });
        Assert.True(r.In(() => QDELETED(dim)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATargetTurfReplacedBeforeItsTurnIsTransmutedAtItsCell()
    {
        // the M7 visual check: an explosion scraped a tile of the anomaly's prepared list to plating, and the anomaly's ChangeTurf of
        // the replaced turf (off the map, no area) threw in turf Initialize, ChangeTurf and SSair every turn, never leaving the list
        AudioRoom r = Room(7963);
        r.In(() =>
        {
            for (int x = X - 3; x <= X + 3; x++)
                for (int y = Y - 3; y <= Y + 3; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        obj_effect_anomaly_dimensional dim = Make<obj_effect_anomaly_dimensional>(r, null, FALSE);
        r.In(() => dim.prepare_area(typeof(datum_dimension_theme_natural)));
        (int fx, int fy, float left) = r.In(() =>
        {
            var first = (turf)dim.target_turfs[1];
            (int x, int y) = ((int)first.x, (int)first.y);
            Waiting.NoSleep(first.ChangeTurf(typeof(turf_open_floor_plating))); // tg's reference names the cell's plating now
            return (x, y, dim.target_turfs.len);
        });
        r.In(() => Waiting.NoSleep(dim.transmute_area()));
        Assert.Equal(left - 1, r.In(() => dim.target_turfs.len));
        Assert.IsType<turf_open_floor_grass>(r.In(() => locate(fx, fy, 1)));
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlasmaThemesFloorTracksWhatStepsOnItAndSetsABareFootAlight()
    {
        AudioRoom r = Room(7963);
        mob_living_carbon_human ann = r.Dummy(X - 1, Y);
        r.Step(2);
        r.In(() => Waiting.NoSleep(locate(X, Y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating)))));
        // the plasma theme lays a plasma material floor (anomalies_dimensional_themes.dm:16, :230), a contact material whose
        // floor tracks what steps on it (_material.dm:128, material_turf_tracking.dm)
        r.In(() => Waiting.NoSleep(((datum_dimension_theme)SSmaterials.dimensional_themes[typeof(datum_dimension_theme_plasma)]).apply_theme(locate(X, Y, 1))));
        turf_open_floor_material floor = r.In(() => Assert.IsType<turf_open_floor_material>(locate(X, Y, 1)));
        Assert.Single(r.In(() => Dm.each_cast<datum_component_material_turf_tracking>(floor.GetComponents(typeof(datum_component_material_turf_tracking))).ToList()));
        // a thing that lands on it touches it (trigger_effect: no target mob, material_turf_tracking.dm:184)
        r.In(() => Waiting.NoSleep(Dm.@new<obj_item_wrench>(locate(X + 1, Y, 1)).forceMove(floor)));
        // a shod foot does not reach the floor (optional.dm:116)
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(ann.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_shoes_sneakers_black>(ann.loc), ITEM_SLOT_FEET))));
            Waiting.NoSleep(ann.forceMove(floor));
        });
        Assert.Equal(0f, r.In(() => Dm.num(ann.fire_stacks)));
        // a bare one does: plasma's fire stacks (firestacker, optional.dm:113)
        r.In(() =>
        {
            Waiting.NoSleep(ann.forceMove(locate(X - 1, Y, 1)));
            qdel(ann.shoes);
            Waiting.NoSleep(ann.forceMove(floor));
        });
        Assert.True(r.In(() => Dm.num(ann.fire_stacks)) > 0, "the bare foot on plasma catches fire stacks");
        r.AssertNoRuntimes();
    }
}
