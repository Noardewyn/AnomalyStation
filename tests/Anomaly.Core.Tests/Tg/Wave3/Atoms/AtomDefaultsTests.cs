using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave3.Atoms.AtomsTrace;

namespace Anomaly.Core.Tests.Tg.Wave3.Atoms;

/// <summary>
/// Type defaults as DM gives them (rules 10.2): BYOND's builtin defaults of a type, then tg's overrides. tg sets layer,
/// plane and appearance_flags on /atom (_atom.dm:8-11); BYOND's builtin /obj, /mob, /turf and /area define their own
/// layer below it, which wins over the /atom override. Only values that tg's later files (atoms_movable.dm, objs.dm,
/// mob_defines.dm, turf.dm, areas.dm, wave 4) do not override again are pinned here.
/// </summary>
public class AtomDefaultsTests
{
    private static TResult Initial<T, TResult>(Func<T, TResult> read) where T : class => Dm.initial(typeof(T), read);

    [Fact]
    public void TheVarsBlockOfAtomHasTgsDefaults()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal(ABOVE_NORMAL_TURF_LAYER, Initial((atom p) => p.layer));
            Assert.Equal((float)GAME_PLANE, Initial((atom p) => p.plane));
            Assert.True(Dm.eq(Initial((atom p) => p.appearance_flags), Dm.bitor(TILE_BOUND, LONG_GLIDE)));
            Assert.Equal(typeof(atom), Initial((atom p) => p.abstract_type));
            // BYOND builtins tg does not touch.
            Assert.Equal((float)SOUTH, Initial((atom p) => p.dir));
            Assert.Equal(255f, Initial((atom p) => p.alpha));
            Assert.Equal(0f, Initial((atom p) => p.density));
            Assert.Equal(0f, Initial((atom p) => p.opacity));
            Assert.Equal(1f, Initial((atom p) => p.mouse_opacity));
            Assert.Equal(NEUTER, Initial((atom p) => p.gender));
            // _atom.dm:14-151
            Assert.True(Dm.eq(Initial((atom p) => p.flags_1), NONE));
            Assert.True(Dm.eq(Initial((atom p) => p.pass_flags_self), NONE));
            Assert.Equal(1f, Initial((atom p) => p.receive_ricochet_chance_mod));
            Assert.Equal(0.33f, Initial((atom p) => p.receive_ricochet_damage_coeff));
            Assert.True(Dm.eq(Initial((atom p) => p.rad_insulation), RAD_NO_INSULATION));
            Assert.Equal((float)COMPLEX_LIGHT, Initial((atom p) => p.light_system));
            Assert.Equal(0f, Initial((atom p) => p.light_range));
            Assert.Equal(1f, Initial((atom p) => p.light_power));
            Assert.Equal(COLOR_WHITE, Initial((atom p) => p.light_color));
            Assert.Equal((float)TRUE, Initial((atom p) => p.light_on));
            Assert.Equal("", Initial((atom p) => p.light_render_source));
            Assert.Equal(360f, Initial((atom p) => p.light_angle));
            Assert.Equal((float)NORTH, Initial((atom p) => p.light_dir));
            Assert.Equal((float)LIGHTING_HEIGHT, Initial((atom p) => p.light_height));
            Assert.Equal((float)CANASTARPASS_DENSITY, Initial((atom p) => p.can_astar_pass));
            Assert.Equal(TACMAP_SOLID, Initial((atom p) => p.tacmap_color));
            Assert.Equal(0f, Initial((atom p) => p.base_pixel_x));
            Assert.Null(Initial((atom p) => p.reagents));
            Assert.Null(Initial((atom p) => p.smoothing_groups));
            Assert.Null(Initial((atom p) => p.ai_controller));
            Assert.Null(Initial((atom p) => p.hud_possible));
            Assert.Null(Initial((atom p) => p.light_sources));
        }
    }

    [Fact]
    public void AnObjKeepsByondsObjLayerUnderTgsAtomOverride()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal(OBJ_LAYER, Initial((obj p) => p.layer));
            Assert.Equal((float)GAME_PLANE, Initial((obj p) => p.plane));
            Assert.Equal(0f, Initial((obj p) => p.density));
            Assert.Equal("obj", Initial((obj p) => p.name)); // BYOND: the type's name with underscores as spaces
            // A live /obj has the same values (the constructor chain, not a separate prototype table).
            var o = Dm.@new<obj>();
            Assert.Equal(OBJ_LAYER, o.layer);
            Assert.Equal((float)GAME_PLANE, o.plane);
        }
    }

    [Fact]
    public void AMobIsDenseOnTheMobLayer()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal(MOB_LAYER, Initial((mob p) => p.layer));
            Assert.Equal(1f, Initial((mob p) => p.density));
            Assert.Equal((float)GAME_PLANE, Initial((mob p) => p.plane));
            Assert.Equal("mob", Initial((mob p) => p.name));
        }
    }

    [Fact]
    public void ATurfIsOnTheTurfLayerAndAnAreaOnTheAreaLayerAndLuminous()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal(2f, Initial((turf p) => p.layer)); // BYOND's TURF_LAYER (tg #defines the name as an #error)
            Assert.Equal((float)GAME_PLANE, Initial((turf p) => p.plane));
            Assert.Equal("turf", Initial((turf p) => p.name));
            Assert.Equal(AREA_LAYER, Initial((area p) => p.layer));
            Assert.Equal(1f, Initial((area p) => p.luminosity));
        }
    }

    [Fact]
    public void TheTemplateNoopTypesHaveTheirNames()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal("Area Passthrough", Initial((area_template_noop p) => p.name)); // preloader.dm:35-36
            Assert.Equal("Turf Passthrough", Initial((turf_template_noop p) => p.name)); // :39-41
            Assert.Equal("noop", Initial((turf_template_noop p) => p.icon_state));
        }
    }

    [Fact]
    public void AnAtomWithoutVarsSetInitializesWithoutTouchingItsOptionalParts()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var a = Dm.@new<obj_test_atoms_probe>();
            // atom/Initialize (atoms_initializing:115-159) with no hud_possible, greyscale, color, light, post-init icon
            // state, smoothing groups, integrity, materials, ai_controller: only the flag and the plane.
            Assert.True(Dm.eq(a.flags_1, INITIALIZED_1));
            Assert.Equal((float)GAME_PLANE, a.plane);
            Assert.Null(a.icon_state);
            Assert.Null(a.ai_controller);
            Assert.DoesNotContain(DmWorldState.Of(t.World).DeferredSites, s => s.Contains("icon_smoothing"));
            Assert.Empty(t.Messages);
        }
    }

    [Fact]
    public void InitializeAppliesThePostInitIconStateAndMakesTheAiControllerFromItsPath()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            var a = Dm.@new<obj_test_atoms_probe>();
            SSatoms.map_loader_stop("template");
            a.icon_state = "map_icon";
            a.post_init_icon_state = "real_icon";
            a.ai_controller = typeof(datum_test_atoms_part);
            a.smoothing_groups = "1,2,";
            SSatoms.InitializeAtoms(Dm.list(a));
            Assert.Equal("real_icon", a.icon_state); // atoms_initializing:138-139
            // ai_controller = new ai_controller(src) when it is a path (:154-155)
            Assert.IsType<datum_test_atoms_part>(a.ai_controller);
            // SETUP_SMOOTHING with smoothing groups: PARSE_SMOOTHING_GROUPS turns "1,2," into its bitflag list (M3 P4:
            // SET_SMOOTHING_GROUPS, __HELPERS/bitflag_lists.dm:15), cached in GLOB.bitflag_lists.
            var groups = Assert.IsType<DmList>(a.smoothing_groups);
            Assert.True(Dm.eq(groups["0"], 6)); // (1 << 1) | (1 << 2)
            Assert.Same(groups, GLOB.bitflag_lists["1,2,"]);
        }
    }
}

/// <summary>SET_PLANE_IMPLICIT and SET_PLANE_EXPLICIT (__HELPERS/_planes.dm:39-60), as /atom/Initialize uses them.</summary>
public class AtomPlaneTests
{
    [Fact]
    public void WithoutPlaneOffsetsThePlaneIsSetAsIs()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 2);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var a = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 2));
            Assert.Equal((float)GAME_PLANE, a.plane);
            a.plane = 7;
            SET_PLANE_IMPLICIT(a, GAME_PLANE);
            Assert.Equal((float)GAME_PLANE, a.plane);
        }
    }

    [Fact]
    public void WithPlaneOffsetsAnAtomOnALowerZGetsItsZsOffsetUnlessThePlaneIsBlacklisted()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 2);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSmapping.max_plane_offset = 1;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1);
            var up = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var down = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 2));
            Assert.Equal((float)GAME_PLANE, up.plane);
            Assert.Equal((float)(GAME_PLANE - PLANE_RANGE), down.plane); // GET_NEW_PLANE: new - PLANE_RANGE * offset

            SSmapping.plane_offset_blacklist = Dm.list(Dm.kv(Dm.text("[]", GAME_PLANE), TRUE));
            var kept = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 2));
            Assert.Equal((float)GAME_PLANE, kept.plane);
        }
    }

    [Fact]
    public void WithPlaneOffsetsAnAtomOffTheMapTakesItsOwnPlanesOffset()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSmapping.max_plane_offset = 1;
            SSmapping.plane_to_offset = Dm.list(Dm.kv(Dm.text("[]", GAME_PLANE), 1));
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.Equal((float)(GAME_PLANE - PLANE_RANGE), a.plane);
        }
    }

    [Fact]
    public void TgQuirkWithoutAPlaneToOffsetTableThePlaneItselfIsUsedAsTheOffset()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSmapping.max_plane_offset = 1;
            SSmapping.plane_to_offset = null;
            // PLANE_TO_OFFSET(plane) is (SSmapping.plane_to_offset ? plane_to_offset["[plane]"] : plane) (_planes.dm:73).
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.Equal((float)(GAME_PLANE - PLANE_RANGE * GAME_PLANE), a.plane);
        }
    }
}

/// <summary>The map preloader (modules/mapping/preloader.dm) as /atom/New uses it (atoms_initializing:67-68).</summary>
public class AtomPreloaderTests
{
    [Fact]
    public void TheGlobalsStartUnarmedInTheirDeclarationOrder()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.True(Dm.eq(GLOB.use_preloader, FALSE));
            Assert.Null(GLOB._preloader_attributes);
            Assert.Null(GLOB._preloader_path);
            Assert.Equal(0f, GLOB.all_mutations.len);
            Assert.Equal("use_preloader,_preloader_attributes,_preloader_path,all_mutations,full_sequences,bad_mutations,"
                + "good_mutations,not_good_mutations,alias_mutations", GLOB.gvars_datum_init_order.Join(","));
        }
    }

    [Fact]
    public void PreloaderSetupWithNoAttributesArmsNothing()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            world.preloader_setup(Dm.list(), typeof(obj_test_atoms_probe));
            Assert.True(Dm.eq(GLOB.use_preloader, FALSE));
            Assert.Null(GLOB._preloader_path);
        }
    }

    [Fact]
    public void ThePreloaderWaitsForAnAtomOfExactlyItsTypeAndDisarmsBeforeLoading()
    {
        var t = new AtomsTestWorld();
        DmWorldState.Of(t.World).NotTranslatedThrows = false;
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            DmList attributes = Dm.list(Dm.kv("name", "preloaded"));
            world.preloader_setup(attributes, typeof(obj_test_atoms_probe_lateload));
            Assert.True(Dm.eq(GLOB.use_preloader, TRUE));
            Assert.Same(attributes, GLOB._preloader_attributes);

            // "in case the instantiated atom is creating other atoms in New()": a parent type does not take it.
            Dm.@new<obj_test_atoms_probe>();
            Assert.True(Dm.eq(GLOB.use_preloader, TRUE));
            Assert.Empty(t.Runtimes);

            var a = Dm.@new<obj_test_atoms_probe_lateload>();
            Assert.True(Dm.eq(GLOB.use_preloader, FALSE));
            // M3 P2: the vars[] write is translated (MapVars.TrySet, preloader.dm:30)
            Assert.Empty(t.Runtimes);
            Assert.Equal("preloaded", a.name);
            Assert.True(Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1))); // atom/New goes on to InitAtom
            Assert.Contains(Id(a) + " LateInitialize initialized=1", t.Trace);
        }
    }
}
