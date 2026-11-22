using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.SmokeListsWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Smoke tests of the comparators of code/__HELPERS/cmp.dm: a negative result puts a first. sorttext(a, b) is 1 when
/// a sorts before b, so the "_asc" text comparators call it with the arguments swapped (cmp.dm:7).
/// </summary>
public class SmokeCmpTests
{
    [Fact]
    public void NumericAndTextComparators()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal(-2f, GlobalProcs.cmp_numeric_asc(1f, 3f));
        Assert.Equal(2f, GlobalProcs.cmp_numeric_dsc(1f, 3f));
        Assert.Equal(-1f, GlobalProcs.cmp_numeric_asc(null, 1f));          // null counts as 0
        Assert.Equal(-1f, GlobalProcs.cmp_text_asc("apple", "Banana"));    // case-insensitive
        Assert.Equal(1f, GlobalProcs.cmp_text_dsc("apple", "Banana"));
        Assert.Equal(0f, GlobalProcs.cmp_text_asc("a", "A"));
        Assert.Equal(-1f, GlobalProcs.cmp_embed_text_asc(1f, 2f));          // compared as "[a]" text
        Assert.Equal(-1f, GlobalProcs.cmp_typepaths_asc(typeof(mob), typeof(obj)));
        Assert.Equal(-9f, GlobalProcs.cmp_num_string_asc("1", "10"));
        Assert.Equal(-1f, GlobalProcs.cmp_list_len_asc(NewList(1f), NewList(1f, 2f)));
        Assert.Equal(1f, GlobalProcs.cmp_list_len_dsc(NewList(1f), NewList(1f, 2f)));
    }

    [Fact]
    public void AssocListComparators()
    {
        using var w = new SmokeListsWorld();
        Assert.Equal(-1f, GlobalProcs.cmp_filter_data_priority(Dm.list(Dm.kv("priority", 1f)), Dm.list(Dm.kv("priority", 2f))));
        Assert.Equal(-1f, GlobalProcs.cmp_assoc_list_name(Dm.list(Dm.kv("name", "a")), Dm.list(Dm.kv("name", "b"))));
        // cmp_generic_stat_item_time: by time, then by count, both descending.
        DmList a = NewList(5f, 1f), b = NewList(5f, 3f);
        Assert.Equal(2f, GlobalProcs.cmp_generic_stat_item_time(a, b));
        // cmp_profile_avg_time_dsc: a zero count divides by 1 (`|| 1`).
        Assert.Equal(-4f, Dm.num(GlobalProcs.cmp_profile_avg_time_dsc(NewList(8f, 2f), NewList(0f, 0f))));
    }

    [Fact]
    public void ComparatorsOverSkeletonTypes()
    {
        using var w = new SmokeListsWorld();
        var x = new atom { name = "x", plane = 1, layer = 5 };
        var y = new atom { name = "Y", plane = 1, layer = 2 };
        Assert.Equal(-1f, GlobalProcs.cmp_name_asc(x, y));
        Assert.Equal(1f, GlobalProcs.cmp_name_dsc(x, y));
        Assert.Equal(3f, GlobalProcs.cmp_atom_layer_asc(x, y));
        y.plane = 3;
        Assert.Equal(-2f, GlobalProcs.cmp_atom_layer_asc(x, y));

        var s1 = new datum_controller_subsystem { init_order = 10, priority = 5 };
        var s2 = new datum_controller_subsystem { init_order = 2, priority = 9, name = "b" };
        s1.name = "a";
        Assert.Equal(8f, GlobalProcs.cmp_subsystem_init(s1, s2));
        Assert.Equal(-4f, GlobalProcs.cmp_subsystem_priority(s1, s2));
        Assert.Equal(-1f, GlobalProcs.cmp_subsystem_display(s1, s2));
        Assert.Equal(0f, GlobalProcs.cmp_subsystem_init_stage(s1, s2));   // initial(): both INITSTAGE_MAIN

        Assert.Equal(0f, GlobalProcs.cmp_crafting_req_priority(typeof(datum_reagent), typeof(datum_reagent)));
        Assert.Equal(-1f, GlobalProcs.cmp_crafting_req_priority(typeof(datum_reagent), typeof(obj)));
        Assert.Equal(-50f, GlobalProcs.cmp_mob_health(new mob_living(), new mob_living { health = 50 }));
        Assert.Equal(0f, GlobalProcs.cmp_fish_fluid(new obj_item_fish(), new obj_item_fish()));
        Assert.Equal(0f, GlobalProcs.cmp_quirk_asc(new datum_quirk(), new datum_quirk()));
        Assert.Equal(-1f, GlobalProcs.cmp_port_order_asc(new datum_port(), new datum_port { order = 2 }));
    }

    [Fact]
    public void TheFishFluidOrderIsAPerWorldStatic()
    {
        // var/static/list/fluids_priority (cmp.dm:210) lives in the world's TgState (rules 10.6); the field is internal
        // for now (see the RULE-GAP in cmp.cs), so the test reads it by reflection.
        var field = typeof(TgState).GetField("GlobalProcs__cmp_fish_fluid__fluids_priority", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        using var w = new SmokeListsWorld();
        DmList order = (DmList)field.GetValue(TgState.Current);
        Assert.Equal(new object[] { "Freshwater", "Saltwater", "Anadromous", "Sulfuric Water", "Any Fluid", "Air" }, Items(order));
        using var other = new SmokeListsWorld();
        Assert.NotSame(order, field.GetValue(TgState.Current));
    }

    [Fact]
    public void ComparatorsThatWaitForOtherPackagesSayWhy()
    {
        using var w = new SmokeListsWorld();
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.cmp_datum_numeric_asc(new datum_record(), new datum_record(), "name"));
        // A datum made by a C# constructor has no world id, so ref() is a DM runtime error (object-model.md section 3);
        // one made by new has a ref text.
        Assert.Throws<DmRuntimeException>(() => GlobalProcs.cmp_embed_text_asc(new datum_record(), "x"));
        Assert.NotNull(GlobalProcs.cmp_embed_text_asc(Dm.@new<datum_record>(), "x"));
    }
}
