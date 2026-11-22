using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: the map preloader against tg's DM (code/modules/mapping/preloader.dm, reader.dm build_coordinate,
/// game/atom/atoms_initializing_EXPENSIVE.dm /atom/New). A var edit of the map reaches the new atom in /atom/New
/// (the first pass, before InitAtom and so before Initialize) or, for a New that never calls ..(), right after
/// `new` returned (the second pass in build_coordinate), and in both cases before the SSatoms initialization.
/// Fixture: tests/fixtures/maps/t2/preload.dmm.
/// </summary>
public class PreloaderTests
{
    private const string Full = "hello world|42|list3|null|/obj/item/crowbar";
    private const string Defaults = "default|7|null|default|/obj/item";

    private static obj_t2_probe At(int x, int y, int index = 1)
    {
        var probes = locate(x, y, 1).contents.Items().OfType<obj_t2_probe>().ToList();
        return probes[index - 1];
    }

    [Fact]
    public void The_edits_reach_the_atom_inside_New_before_Initialize_and_survive_the_init()
    {
        World world = T2Maps.Boot("preload", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            obj_t2_probe probe = At(2, 3);
            // tg: /atom/New runs preloader_load(src) first thing (the first pass); our New override runs before it
            // (it records, then calls the parent), so New saw the defaults and Initialize saw the map's values.
            string[] trace = T2Trace.Of(world).Lines.Where(l => l.StartsWith(probe.dm_id + " ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[] { probe.dm_id + " New " + Defaults, probe.dm_id + " Initialize " + Full }, trace);
            // and they survived the initialization
            Assert.Equal(Full, probe.Snapshot());
            Assert.Equal("hello world", probe.text_var);
            Assert.Equal(42f, probe.num_var);
            Assert.Null(probe.any_var); // an edit to null replaces the default "default"
            Assert.Equal(typeof(obj_item_crowbar), probe.path_var);
            Assert.True(Dm.truthy(Dm.bitand(probe.flags_1, INITIALIZED_1)));
        }
    }

    [Fact]
    public void A_New_that_never_calls_the_parent_gets_its_edits_from_the_second_pass_before_Initialize()
    {
        World world = T2Maps.Boot("preload");
        using (world.Enter())
        {
            var probe = locate(5, 3, 1).contents.Items().OfType<obj_t2_probe_nonew>().Single();
            // tg: build_coordinate `if(GLOB.use_preloader && instance) world.preloader_load(instance)` after create_atom
            string[] trace = T2Trace.Of(world).Lines.Where(l => l.StartsWith(probe.dm_id + " ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(new[] { probe.dm_id + " New " + Defaults, probe.dm_id + " Initialize " + Full }, trace);
            Assert.Equal(Full, probe.Snapshot());
        }
    }

    [Fact]
    public void The_values_are_text_numbers_lists_and_typepaths_as_tgs_parse_constant_reads_them()
    {
        World world = T2Maps.Boot("preload");
        using (world.Enter())
        {
            obj_t2_probe probe = At(2, 3);
            Assert.Equal(3f, probe.list_var.len);
            Assert.Equal("a", probe.list_var[1]);
            Assert.True(Dm.eq(probe.list_var[2], 2f));
            var nested = Assert.IsType<DmList>(probe.list_var[3]);
            Assert.True(Dm.eq(nested["n"], 1f));

            // a negative fraction, an empty text, an association list
            obj_t2_probe other = At(3, 2);
            Assert.Equal(-1.5f, other.num_var);
            Assert.Equal("", other.text_var);
            Assert.Null(other.any_var);
            Assert.Equal(2f, other.list_var.len);
            Assert.True(Dm.eq(((DmList)other.list_var["k"])[2], 4f));
            Assert.Equal("v", other.list_var["j"]);

            // the exponent form BYOND writes
            Assert.Equal(1000000f, At(3, 1).num_var);
        }
    }

    [Fact]
    public void A_list_is_deep_copied_for_every_atom_even_when_the_tiles_share_a_model()
    {
        World world = T2Maps.Boot("preload");
        using (world.Enter())
        {
            // (2,3) and (3,3) use one model key (one cached attribute list); tg's preloader_load deep_copy_lists it
            obj_t2_probe first = At(2, 3);
            obj_t2_probe second = At(3, 3);
            Assert.NotSame(first.list_var, second.list_var);
            Assert.NotSame(first.list_var[3], second.list_var[3]);
            first.list_var.Add("changed");
            ((DmList)first.list_var[3])["n"] = 99f;
            Assert.Equal(3f, second.list_var.len);
            Assert.True(Dm.eq(((DmList)second.list_var[3])["n"], 1f));
        }
    }

    [Fact]
    public void An_atom_without_edits_keeps_its_defaults_and_the_preloader_is_off_after_the_load()
    {
        World world = T2Maps.Boot("preload");
        using (world.Enter())
        {
            // (4,3): a probe with no edits, created right after tiles whose atoms were preloaded
            Assert.Equal(Defaults, At(4, 3).Snapshot());
            // (2,2): the first probe has an edit, the second none: the second must not inherit the first's attributes
            Assert.Equal("default|1|null|default|/obj/item", At(2, 2, 1).Snapshot());
            Assert.Equal(Defaults, At(2, 2, 2).Snapshot());
            Assert.False(Dm.truthy(GLOB.use_preloader));
        }
    }

    [Fact]
    public void Builtin_atom_vars_of_a_real_tg_object_survive_its_initialization()
    {
        World world = T2Maps.Boot("preload", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            var crowbar = locate(5, 2, 1).contents.Items().OfType<obj_item_crowbar>().Single();
            Assert.Equal("mapped crowbar", crowbar.name);
            Assert.Equal("A mapped description.", crowbar.desc);
            Assert.Equal(-4f, crowbar.pixel_x);
            Assert.Equal(3f, crowbar.pixel_y);
            Assert.Equal(4f, crowbar.dir);
            Assert.Equal("#aa0000", crowbar.color);
            Assert.Equal(128f, crowbar.alpha);
            Assert.Equal(5.5f, crowbar.layer);
        }
    }

    // tg: preloader.dm:35 `what.vars[attribute] = value`. DM's `var/id = 0` (solar.dm:422) is not typed: Meta's `id = "..."`
    // makes the var hold text, and tg's solar control computers find their panels by that text (`id` of /obj/machinery/power/
    // solar, solar.dm:18). I1a's generator types a var as a value when its kinds differ (the station map's edits count), so the var holds the text.
    [Fact]
    public void A_number_typed_var_given_text_by_the_map_holds_the_text_as_in_dm()
    {
        World world = T2Maps.Boot("idtype", out TgTestWorld.Log log);
        using (world.Enter())
        {
            var text = locate(2, 1, 1).contents.Items().OfType<obj_machinery_power_solar_control>().Single();
            var number = locate(3, 1, 1).contents.Items().OfType<obj_machinery_power_solar_control>().Single();
            // the number edit works in either typing
            Assert.True(Dm.eq(Dm.get_var(number, "id"), 7f));
            Assert.True(Dm.eq(Dm.get_var(text, "id"), "solar_wing"), "solar_control.id after the load: " + Dm.get_var(text, "id") + "; runtimes: " + string.Join(" | ", log.Runtimes.Select(r => r.Message)));
            var crematorium = locate(4, 1, 1).contents.Items().OfType<obj_structure_bodycontainer_crematorium>().Single();
            Assert.True(Dm.eq(Dm.get_var(crematorium, "id"), "crema_1"));
            Assert.Empty(log.Runtimes);
        }
    }
}
