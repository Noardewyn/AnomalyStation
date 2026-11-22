using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Wave3.Engine.EngineTestHelpers;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave3.Engine;

/// <summary>
/// The other BYOND builtin vars and procs of the atom engine parts (object-model.md section 2; presentation-seam.md
/// section 5): BYOND's per-type defaults and default names, the appearance and visibility notifications, the engine
/// lists, and the builtin Enter, Cross, Uncross, Crossed and Uncrossed that tg replaces (move-chain.md E4).
/// </summary>
public class BuiltinVarTests
{
    [Fact]
    public void BuiltinDefaultsFollowTheByondReference()
    {
        // BYOND ref layer var: 1 AREA_LAYER, 2 TURF_LAYER, 3 OBJ_LAYER, 4 MOB_LAYER; dir SOUTH; gender "neuter";
        // mob density 1; area luminosity 1 (OpenDream DMStandard Atoms); alpha 255; mouse_opacity 1. Since W4.0 the
        // engine constructors end with tg's overrides of each builtin type (translation-rules.md 10.2, "Builtin
        // types"), so a var tg overrides shows tg's value: noted per line.
        var turf = new turf_test_engine_floor();
        var area = new area_test_engine_room();
        var obj = new obj_test_engine_box();
        var mob = new mob_test_engine_mob();
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, new[] { area.layer, turf.layer, obj.layer, mob.layer });
        Assert.Equal(new[] { 0f, 0f, 0f, 1f }, new[] { area.density, turf.density, obj.density, mob.density });
        // tg's /turf sets luminosity = 1 (game/turfs/turf.dm:9).
        Assert.Equal(new[] { 1f, 1f, 0f, 0f }, new[] { area.luminosity, turf.luminosity, obj.luminosity, mob.luminosity });
        Assert.All(new atom[] { area, turf, obj, mob }, a =>
        {
            Assert.Equal(Dm.SOUTH, a.dir);
            Assert.Equal(Dm.NEUTER, a.gender);
            Assert.Equal(255f, a.alpha);
            Assert.Equal(0f, a.opacity);
            Assert.Equal(32f, a.maptext_width);
            Assert.Equal(32f, a.maptext_height);
            Assert.NotNull(a.overlays);
            Assert.NotNull(a.underlays);
            Assert.NotNull(a.vis_contents);
            Assert.Null(a.loc);
        });
        Assert.All(new atom[] { turf, obj, mob }, a =>
        {
            Assert.Equal(1f, a.mouse_opacity);
            Assert.Equal(0f, a.invisibility);
        });
        // tg's /area: mouse_opacity = MOUSE_OPACITY_TRANSPARENT, invisibility = INVISIBILITY_LIGHTING (areas.dm:13-14).
        Assert.Equal((float)Anomaly.Core.Tg.Defines.MOUSE_OPACITY_TRANSPARENT, area.mouse_opacity);
        Assert.Equal((float)Anomaly.Core.Tg.Defines.INVISIBILITY_LIGHTING, area.invisibility);
        Assert.Equal(Dm.SLIDE_STEPS, obj.animate_movement); // tg's /obj (objs.dm:4); BYOND's default is FORWARD_STEPS
        Assert.Equal(32f, mob.bound_width);
        Assert.Equal(32f, mob.bound_height);
        Assert.Equal(8f, mob.glide_size); // tg's /atom/movable (atoms_movable.dm:4); BYOND's default is 0
        Assert.Equal(1e6f, mob.see_in_dark); // tg's /mob (mob_defines.dm:22); BYOND's default is 2
    }

    [Fact]
    public void TheDefaultNameIsTheTypeNameAndAnAncestorsExplicitNameWins()
    {
        // BYOND ref name var: "The name of the object type with underscores converted to spaces."
        Assert.Equal("test engine floor", new turf_test_engine_floor().name);
        Assert.Equal("Space", new area_test_engine_room().name); // tg's /area sets name = "Space" (areas.dm:7): an ancestor's explicit name
        Assert.Equal("test engine box", new obj_test_engine_box().name);
        Assert.Equal("inner kind", new obj_test_engine_box_inner_kind().name);
        Assert.Equal("test engine mob", new mob_test_engine_mob().name);
        Assert.Equal("wall", new turf_test_engine_wall().name);
        Assert.Equal("named thing", new obj_test_engine_named().name);
        Assert.Equal("named thing", new obj_test_engine_named_plain_child().name); // inherited, not "plain child"
    }

    [Fact]
    public void TheShimSeesAnAtomsNameAndGenderThroughIDmAtom()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            IDmAtom view = box;
            Assert.Equal("test engine box", view.name);
            Assert.Equal(Dm.NEUTER, view.gender);
            box.name = "Bob";
            box.gender = Dm.MALE;
            Assert.Equal("Bob", view.name);
            Assert.Equal(Dm.MALE, view.gender);
            Assert.Same(box.contents, view.contents);
            Assert.Equal("Bob is here", Dm.text("[] is here", box));
        }
    }

    [Fact]
    public void EveryAppearanceAssignmentToARegisteredAtomNotifiesItsAspect()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            RecordingSinks sinks = RecordingSinks.Install(t.World);
            string id = EngineTrace.D(box);

            box.icon = "icons/obj.dmi";
            box.icon_state = "open";
            box.dir = Dm.NORTH;
            box.layer = 5;
            box.plane = -1;
            box.alpha = 128;
            box.color = "#ff0000";
            box.pixel_x = 1;
            box.pixel_y = 2;
            box.pixel_w = 3;
            box.pixel_z = 4;
            box.transform = null;
            box.overlays = Dm.list("o");
            box.underlays = Dm.list("u");
            box.vis_contents = null;
            box.maptext = "hi";
            box.maptext_width = 64;
            box.maptext_height = 64;
            box.maptext_x = 1;
            box.maptext_y = 1;
            box.appearance_flags = 1;
            box.vis_flags = 1;
            box.blend_mode = 2;
            box.filters = null;
            box.render_target = "t";
            box.render_source = "s";
            box.glide_size = 8;
            box.opacity = 1;
            box.invisibility = 101;

            string[] aspects =
            {
                "Icon", "IconState", "Dir", "Layer", "Plane", "Alpha", "Color", "Pixel", "Pixel", "Pixel", "Pixel", "Transform",
                // M2 P3: the list's own owner hints the in-place Add of the assignment, then the setter hints again
                "Overlays", "Overlays", "Underlays", "Underlays", "VisContents", "Maptext", "Maptext", "Maptext", "Maptext", "Maptext",
                "AppearanceFlags", "AppearanceFlags", "BlendMode", "Filters", "Render", "Render", "Glide",
            };
            var expected = aspects.Select(a => "tick 0 changed " + id + " " + a).ToList();
            expected.Add("tick 0 visibility " + id);
            expected.Add("tick 0 visibility " + id);
            Assert.Equal(expected, sinks.Lines);
        }
    }

    [Fact]
    public void PrototypesAndAtomsInTheirConstructorNotifyNothing()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            RecordingSinks sinks = RecordingSinks.Install(t.World);
            var prototype = new obj_test_engine_box(); // dm_id 0: setters only store (object-model.md section 2)
            prototype.dir = Dm.EAST;
            prototype.opacity = 1;
            prototype.overlays = null;
            Assert.Equal(Dm.EAST, prototype.dir);
            Dm.@new<turf_test_engine_wall>(locate(1, 1, 1)); // its constructor assigns density and name; BYOND's layer too
            Dm.@new<obj_test_engine_crate>();
            Assert.Empty(sinks.Lines);
        }
        var outside = new obj_test_engine_box();
        outside.icon_state = "x"; // no world at all
        Assert.Equal("x", outside.icon_state);
    }

    [Fact]
    public void TheEngineListsAreNeverNullAndAssignmentsReplaceTheirItems()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            DmList overlays = box.overlays;
            Dm.append(box.overlays, "a"); // rules 4.3: in place
            Dm.append(box.overlays, "b");
            Assert.Equal(new object[] { "a", "b" }, Items(overlays));

            box.overlays = null; // tg's cut_overlays: `overlays = null` empties the builtin list (object-model.md section 2)
            Assert.Same(overlays, box.overlays);
            Assert.Empty(Items(box.overlays));

            DmList replacement = Dm.list("x", "y");
            box.overlays = replacement;
            Assert.Same(overlays, box.overlays);
            Assert.Equal(new object[] { "x", "y" }, Items(box.overlays));
            Dm.append(replacement, "z"); // the assigned list was copied, not adopted
            Assert.Equal(2, box.overlays.Count);

            box.underlays = Dm.list("u");
            box.underlays = box.underlays; // the same list: kept
            Assert.Equal(new object[] { "u" }, Items(box.underlays));
            var shown = Dm.@new<obj_test_engine_box>();
            box.vis_contents = Dm.list(shown);
            Assert.Equal(new object[] { shown }, Items(box.vis_contents));
            Assert.Null(shown.loc); // vis_contents shows an atom, it does not move it
        }
    }

    [Fact]
    public void AnOverlaysAndAVisContentsMutationThroughListOperatorsNotify()
    {
        var t = new EngineWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            RecordingSinks sinks = RecordingSinks.Install(t.World);
            string id = EngineTrace.D(box);
            // presentation-seam.md section 5: tg's add_overlay (`overlays += ...`, overlays.dm:82) reaches Changed(Overlays)
            // (M2 P3's overlay owner, Builtin/atom.overlays.builtin.cs); an in-place vis_contents edit reaches
            // Changed(VisContents) since the M2 integration (the replicator draws vis_contents as part of the atom).
            Dm.append(box.overlays, "a");
            Dm.remove(box.overlays, "a");
            Dm.append(box.vis_contents, Dm.@new<obj_test_engine_box>());
            Assert.Equal(new[] { "tick 0 changed " + id + " Overlays", "tick 0 changed " + id + " Overlays", "tick 0 changed " + id + " VisContents" }, sinks.Lines);
        }
    }

    [Fact]
    public void ByondsEnterPermitsEverythingOnAreasObjsAndMobs()
    {
        var t = new EngineWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var walker = Dm.@new<mob_test_engine_mob>(); // dense
            var area = (area)locate(1, 1, 1).loc;
            var box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
            // BYOND ref Enter: "Areas, objs, and mobs will always permit anything to enter by default."
            Assert.True(Dm.eq(Waiting.NoSleep(area.Enter(walker)), 1));
            Assert.True(Dm.eq(Waiting.NoSleep(box.Enter(walker)), 1));
            Assert.True(Dm.eq(Waiting.NoSleep(walker.Enter(box)), 1));

            // BYOND's turf branch (a turf asks its own Cross and then each occupant's) is no longer reachable: tg's
            // /turf/Enter (turf.dm:434, "Do not call ..()"), translated in wave 4 (package B), replaces it on every turf,
            // these fixtures included. A movable's Cross is tg's (atoms_movable.dm:934: the signals, then CanPass), which
            // lets the dense walker overlap the non-dense box.
            turf floor = locate(1, 1, 1);
            Assert.True(Dm.eq(Waiting.NoSleep(box.Cross(walker)), 1));
            // Wave 4 (package A): tg's /atom/movable/Uncross CRASHes (atoms_movable.dm:966); BYOND's default is a turf's.
            Assert.True(Dm.eq(floor.Uncross(walker), 1)); // "allows O to get away"
            Assert.Empty(t.Runtimes);
        }
    }
}
