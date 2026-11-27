using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

/// <summary>
/// M3 T2 phase B: tg's undertile element (datums/elements/undertile.dm:40-120) on cables, disposal pipes and hidden pipes, from the
/// turf's underfloor_accessibility (turf.dm:22, UNDERFLOOR_* in __DEFINES/turfs.dm:50-54): below UNDERFLOOR_VISIBLE the thing is
/// invisible (INVISIBILITY_MAXIMUM) at ALPHA_UNDERTILE with its T-ray trait; below UNDERFLOOR_INTERACTABLE it carries
/// TRAIT_UNDERFLOOR; at plating it shows again with its own alpha. The init order matters (main 46c0a44): the map's atoms are
/// initialized by SSatoms' own walk of the world, the turf's levelupdate (turf.dm:517) hides the ones already initialized, and
/// the ones initialized after their turf hide themselves.
/// </summary>
public class UndertileTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public UndertileTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private const int Maximum = 100;      // INVISIBILITY_MAXIMUM
    private const int UndertileAlpha = 128; // ALPHA_UNDERTILE

    /// <summary>The expectation of tg's hide() for a thing with the undertile element on a turf with this underfloor_accessibility.</summary>
    private static string Check(atom_movable thing, int accessibility)
    {
        string who = $"{DmTypes.PathOf(thing.GetType())} at ({thing.x},{thing.y})";
        float invisibility = Convert.ToSingle(thing.invisibility);
        if (accessibility < 1)
        {
            if (invisibility != Maximum) return $"{who}: invisibility {invisibility}, hidden under accessibility {accessibility} should be {Maximum}";
            if (thing.alpha != UndertileAlpha) return $"{who}: alpha {thing.alpha}, hidden should be {UndertileAlpha}";
            if (!HAS_TRAIT(thing, TRAIT_T_RAY_VISIBLE)) return $"{who}: hidden without TRAIT_T_RAY_VISIBLE";
        }
        else
        {
            if (invisibility != 0) return $"{who}: invisibility {invisibility}, accessibility {accessibility} should show it";
            if (thing.alpha != 255) return $"{who}: alpha {thing.alpha}, shown should be its own 255";
            if (HAS_TRAIT(thing, TRAIT_T_RAY_VISIBLE)) return $"{who}: shown with TRAIT_T_RAY_VISIBLE";
        }
        if (HAS_TRAIT(thing, TRAIT_UNDERFLOOR) != (accessibility < 2)) return $"{who}: TRAIT_UNDERFLOOR should be {accessibility < 2}";
        return null;
    }

    private static int Accessibility(turf t) => Convert.ToInt32(t.underfloor_accessibility);

    // what hid the things of a Meta cell: its turf, or broken tiling over plating, whose LateInitialize hides them as
    // UNDERFLOOR_VISIBLE (broken_flooring.dm:23)
    private static int MapAccessibility(turf t)
        => t is turf_open_floor_plating && Dm.each_cast<atom>(t.contents).Any(a => a is obj_structure_broken_flooring) ? 1 : Accessibility(t);

    [Fact]
    public void Things_made_in_the_map_step_are_hidden_or_shown_by_their_turf_when_SSatoms_walks_the_world()
    {
        var made = new List<atom_movable>();
        var room = TgToyRoom.Boot(9, 9, game: false, mapExtras: _ =>
        {
            // the order a map load makes them in: the turf, then its movables; every one waits for SSatoms
            foreach (int x in new[] { 2, 3, 4, 5 })
            {
                turf t = x switch
                {
                    2 => Dm.@new<turf_open_floor_iron>(locate(x, 2, 1)),
                    3 => Dm.@new<turf_open_floor_plating>(locate(x, 2, 1)),
                    4 => Dm.@new<turf_open_floor_iron_dark>(locate(x, 2, 1)),
                    _ => Dm.@new<turf_open_floor_plating>(locate(x, 2, 1)),
                };
                made.Add(Dm.@new<obj_structure_cable>(t));
                made.Add(Dm.@new<obj_structure_disposalpipe_segment>(t));
                made.Add(Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_hidden>(t));
            }
            // a pipe of tg's `visible` kind (hide = FALSE, atmosmachinery.dm:96) has no element: it shows over a floor tile
            made.Add(Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible>(Dm.@new<turf_open_floor_iron>(locate(6, 2, 1))));
            Assert.All(made, m => Assert.False(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1))));
        });
        using (room.World.Enter())
        {
            Assert.Empty(room.RuntimeText);
            foreach (atom_movable thing in made)
            {
                Assert.True(Dm.truthy(Dm.bitand(thing.flags_1, INITIALIZED_1)), thing.GetType().Name);
                turf t = (turf)thing.loc;
                if (thing is obj_machinery_atmospherics_pipe_smart_manifold4w_supply_visible)
                {
                    Assert.Equal(0f, Convert.ToSingle(thing.invisibility));
                    continue;
                }
                Assert.Null(Check(thing, Accessibility(t)));
            }
            // the iron floors hide (accessibility HIDDEN), the plating does not
            Assert.Equal(0, Accessibility(locate(2, 2, 1)));
            Assert.Equal(2, Accessibility(locate(3, 2, 1)));
        }
    }

    [Fact]
    public void Levelupdate_hides_a_thing_made_after_its_turf_and_a_turf_changed_under_it_hides_or_shows_it()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            turf iron = Dm.@new<turf_open_floor_iron>(locate(3, 3, 1));
            turf plating = Dm.@new<turf_open_floor_plating>(locate(4, 3, 1));
            var on_iron = new List<atom_movable>();
            var on_plating = new List<atom_movable>();
            foreach (var (t, list) in new[] { (iron, on_iron), (plating, on_plating) })
            {
                list.Add(Dm.@new<obj_structure_cable>(t));
                list.Add(Dm.@new<obj_structure_disposalpipe_segment>(t));
                list.Add(Dm.@new<obj_machinery_atmospherics_pipe_smart_manifold4w_supply_hidden>(t));
            }
            // tg hides a thing only when its turf says so (turf.dm:517 levelupdate, from the turf's Initialize): a cable made at
            // runtime on a floor tile shows until then (nothing else sends COMSIG_OBJ_HIDE, undertile.dm only listens)
            Assert.All(on_iron, m => Assert.Null(Check(m, 2)));
            Assert.All(on_plating, m => Assert.Null(Check(m, 2)));
            iron.levelupdate();
            Assert.All(on_iron, m => Assert.Null(Check(m, 0)));

            // a tile pried up (ChangeTurf to plating) shows what is under it, and a plating covered with a tile hides it again
            turf now_plating = (turf)Waiting.NoSleep(iron.dm_change_turf(typeof(turf_open_floor_plating), null, null));
            Assert.All(on_iron, m => Assert.Null(Check(m, Accessibility(now_plating))));
            Assert.Equal(2, Accessibility(now_plating));
            turf now_iron = (turf)Waiting.NoSleep(plating.dm_change_turf(typeof(turf_open_floor_iron), null, null));
            Assert.All(on_plating, m => Assert.Null(Check(m, Accessibility(now_iron))));
            Assert.Equal(0, Accessibility(now_iron));
            Assert.Empty(room.RuntimeText);
        }
    }

    private static bool HasElement(atom_movable thing)
        => !InertSeam.IsInert(thing)
        && (thing is obj_structure_cable or obj_structure_disposalpipe
            || (thing is obj_machinery_atmospherics_pipe pipe && Dm.truthy(pipe.hide))); // pipes.dm:39 setup_hiding

    [MetaFact]
    public void Every_cable_disposal_pipe_and_hidden_pipe_of_Meta_is_hidden_or_shown_as_its_turf_says()
    {
        World world = _meta.World;
        int hidden = 0, shown = 0, no_element = 0;
        var wrong = new List<string>();
        using (world.Enter())
        {
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf t = locate(x, y, 1);
                    foreach (atom_movable thing in t.contents.Items())
                    {
                        if (thing is not (obj_structure_cable or obj_structure_disposalpipe or obj_machinery_atmospherics_pipe)) continue;
                        if (!HasElement(thing))
                        {
                            no_element++;
                            if (Convert.ToSingle(thing.invisibility) != 0) wrong.Add($"{DmTypes.PathOf(thing.GetType())} at ({x},{y}) has no undertile element but is invisible");
                            continue;
                        }
                        string problem = Check(thing, MapAccessibility(t));
                        if (problem != null) wrong.Add(problem);
                        if (MapAccessibility(t) < 1) hidden++;
                        else shown++;
                    }
                }
            }
        }
        _output.WriteLine($"Meta: {hidden} hidden, {shown} shown, {no_element} without the element; {wrong.Count} wrong");
        Assert.True(hidden > 5000 && shown > 1000, $"{hidden} hidden, {shown} shown");
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(15)));
    }

    /// <summary>
    /// 2026-10-18 (the owner: the navigation beacons showed on the station's floors): the floor equipment tg hides with the undertile
    /// element in its Initialize, live since then: the nav beacons (navbeacon.dm:48), the cable terminals (terminal.dm:18), the
    /// gigabeacon (gigabeacon.dm:18, its tracking beacon invisible) and the floor safe (safe.dm:321, no T-ray trait). The beacons
    /// also fill tg's patrol and delivery lists.
    /// </summary>
    [MetaFact]
    public void Metas_nav_beacons_terminals_gigabeacon_and_floor_safe_hide_under_floor_tiles()
    {
        World world = _meta.World;
        var counts = new Dictionary<string, int>();
        var wrong = new List<string>();
        using (world.Enter())
        {
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf t = locate(x, y, 1);
                    foreach (atom_movable thing in t.contents.Items())
                    {
                        if (thing is not (obj_machinery_navbeacon or obj_machinery_power_terminal or obj_machinery_bluespace_beacon or obj_structure_safe_floor))
                            continue;
                        string key = thing.GetType().Name + (Accessibility(t) < 1 ? " hidden" : " shown");
                        counts[key] = counts.GetValueOrDefault(key) + 1;
                        if (thing is obj_structure_safe_floor)
                        {
                            float invisibility = Convert.ToSingle(thing.invisibility);
                            if (invisibility != (Accessibility(t) < 1 ? Maximum : 0))
                                wrong.Add($"floor safe at ({x},{y}): invisibility {invisibility} on accessibility {Accessibility(t)}");
                            continue;
                        }
                        string problem = Check(thing, Accessibility(t));
                        if (problem != null) wrong.Add(problem);
                        if (thing is obj_machinery_bluespace_beacon giga && Convert.ToSingle(giga.Beacon?.invisibility) != Maximum)
                            wrong.Add($"the gigabeacon's tracking beacon at ({x},{y}) is not invisible");
                    }
                }
            }
            _output.WriteLine(string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}")));
            Assert.True(counts.Where(c => c.Key.StartsWith(nameof(obj_machinery_navbeacon))).Sum(c => c.Value) >= 40, "Meta's nav beacons");
            Assert.True(counts.Where(c => c.Key.StartsWith(nameof(obj_machinery_power_terminal))).Sum(c => c.Value) >= 10, "Meta's cable terminals");
            Assert.True(counts.GetValueOrDefault(nameof(obj_machinery_navbeacon) + " hidden") > 0, "a nav beacon under a floor tile");
            Assert.True(Dm.truthy(GLOB.navbeacons["1"]), "the patrol beacons of z 1");
            Assert.True(Dm.truthy(Dm.length(GLOB.deliverybeacons)), "the delivery beacons");
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(15)));
    }
}
