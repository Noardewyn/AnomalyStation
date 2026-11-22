using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tests.Tg.Pilot.Lists.ListsBehaviourWorld;

namespace Anomaly.Core.Tests.Tg.Pilot.Lists;

/// <summary>
/// Behaviour of the comparators of code/__HELPERS/cmp.dm, read from the DM code (each test names the proc and its
/// line). The sorts put <c>a</c> before <c>b</c> when <c>cmp(a, b)</c> is negative (sort_instance.dm), and
/// <c>sorttext(T1, T2)</c> is 1 when T1 sorts before T2, case-insensitively (BYOND ref), so the "_asc" text comparators
/// pass their arguments swapped. Comparators that read <c>initial()</c> are checked with types whose compile-time
/// value (the prototype, rules 10.3) differs from the instance's current one.
/// </summary>
public class CmpBehaviourTests
{
    private static string Ref(string name) => ProcRef(name);

    private static void AssertSign(int expected, object result)
        => Assert.True(Math.Sign(Dm.num(result)) == expected, "expected sign " + expected + ", got " + Dm.str(result));

    // ---- cmp_numeric_dsc, cmp_numeric_asc (:1, :4)

    [Fact]
    public void NumericComparatorsSubtract()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-2f, GlobalProcs.cmp_numeric_asc(1f, 3f));
        AssertDmEqual(2f, GlobalProcs.cmp_numeric_dsc(1f, 3f));
        AssertDmEqual(0.5f, GlobalProcs.cmp_numeric_asc(2.5f, 2));
        AssertDmEqual(-4f, GlobalProcs.cmp_numeric_asc(null, 4f));       // null is 0 in arithmetic
        AssertDmEqual(4f, GlobalProcs.cmp_numeric_dsc(null, 4f));
        AssertDmEqual(0f, GlobalProcs.cmp_numeric_asc(null, null));
        // Text in a subtraction is a DM runtime error (type mismatch).
        Assert.ThrowsAny<DmRuntimeException>(() => GlobalProcs.cmp_numeric_asc("a", 1f));
    }

    // ---- cmp_text_asc, cmp_text_dsc (:7, :10)

    [Fact]
    public void TextComparatorsAreCaseInsensitiveSorttext()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc("apple", "banana"));
        AssertDmEqual(1f, GlobalProcs.cmp_text_asc("banana", "apple"));
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc("Apple", "banana"));
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc("apple", "Banana"));
        AssertDmEqual(0f, GlobalProcs.cmp_text_asc("Same", "sAME"));
        AssertDmEqual(1f, GlobalProcs.cmp_text_dsc("apple", "banana"));
        AssertDmEqual(-1f, GlobalProcs.cmp_text_dsc("banana", "apple"));
        AssertDmEqual(0f, GlobalProcs.cmp_text_dsc("x", "X"));
        // Text order, not number order.
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc("10", "9"));
        // A null sorts before every text.
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc(null, "a"));
        AssertDmEqual(1f, GlobalProcs.cmp_text_asc("a", null));
    }

    [Fact]
    public void TextComparatorsTakeValuesThatAreNotText()
    {
        // sort_list's default comparator is cmp_text_asc (_lists.dm:733), and tg sorts mobs (mobs.dm:415, :424) and
        // typepaths (atoms.dm:299, admin/fun_balloon.dm:81) with it. The BYOND reference does not say what sorttext
        // does with a value that is not text; OpenDream (rules 0.2) treats it like null: before every text, and equal
        // to another such value (NativeProc_sorttext).
        using var w = new ListsBehaviourWorld();
        var m = new mob();
        AssertDmEqual(-1f, GlobalProcs.cmp_text_asc(m, "a"));
        AssertDmEqual(1f, GlobalProcs.cmp_text_asc("a", m));
        AssertDmEqual(0f, GlobalProcs.cmp_text_asc(m, new mob()));
        AssertDmEqual(0f, GlobalProcs.cmp_text_asc(typeof(obj), typeof(mob)));
        AssertDmEqual(0f, GlobalProcs.cmp_text_asc(5f, 3f));
        AssertDmEqual(1f, GlobalProcs.cmp_text_dsc(m, "a"));
        AssertDmEqual(0f, GlobalProcs.cmp_text_dsc(typeof(obj), null));
    }

    [Fact]
    public void SortListOfValuesThatAreNotTextKeepsThemInOrderBeforeTheTexts()
    {
        using var w = new ListsBehaviourWorld();
        var m1 = new mob { name = "zed" };
        var m2 = new mob { name = "amy" };
        AssertItems(GlobalProcs.sort_list(DL(m1, "b", m2, "a")), m1, m2, "a", "b");
        AssertItems(GlobalProcs.sort_list(DL(typeof(obj), typeof(mob), typeof(atom))), typeof(obj), typeof(mob), typeof(atom));
    }

    // ---- cmp_embed_text_asc, cmp_embed_text_dsc (:13, :20): "[a]" and "[b]" (a datum through REF)

    [Fact]
    public void EmbedTextComparatorsCompareTheTextForm()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-1f, GlobalProcs.cmp_embed_text_asc(10f, 9f));    // "10" before "9"
        AssertDmEqual(1f, GlobalProcs.cmp_embed_text_dsc(10f, 9f));
        AssertDmEqual(0f, GlobalProcs.cmp_embed_text_asc(1.5f, "1.5"));  // a number and its text are equal here
        AssertDmEqual(-1f, GlobalProcs.cmp_embed_text_asc(null, "a"));    // "[null]" is ""
        AssertDmEqual(1f, GlobalProcs.cmp_embed_text_asc(typeof(obj), typeof(mob)));   // "/obj" after "/mob"
        AssertDmEqual(0f, GlobalProcs.cmp_embed_text_asc("ABC", "abc"));
    }

    // ---- cmp_list_len_asc, cmp_list_len_dsc (:27, :30)

    [Fact]
    public void ListLengthComparators()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-2f, GlobalProcs.cmp_list_len_asc(DL(1f), DL(1f, 2f, 3f)));
        AssertDmEqual(2f, GlobalProcs.cmp_list_len_dsc(DL(1f), DL(1f, 2f, 3f)));
        AssertDmEqual(-1f, GlobalProcs.cmp_list_len_asc(null, DL(null)));   // length(null) is 0; list(null) has 1
        AssertDmEqual(0f, GlobalProcs.cmp_list_len_asc(Dm.list(), null));
        DmList lists = DL(DL(1f, 2f), DL(1f), Dm.list(), DL(3f, 4f), DL(5f));
        GlobalProcs.sortTim(lists, Ref(nameof(GlobalProcs.cmp_list_len_dsc)));
        Assert.Equal(new[] { 2, 2, 1, 1, 0 }, Items(lists).Select(l => ((DmList)l).Count).ToArray());
        AssertDmEqual(1f, ((DmList)Items(lists)[0])[1f]);                  // ties keep their order
        AssertDmEqual(3f, ((DmList)Items(lists)[1])[1f]);
    }

    // ---- cmp_name_asc, cmp_name_dsc, cmp_init_name_asc (:33-40)

    private sealed class Apple : obj
    {
        public Apple() { name = "apple"; }
    }

    private sealed class Banana : obj
    {
        public Banana() { name = "Banana"; }
    }

    [Fact]
    public void NameComparatorsReadTheCurrentNames()
    {
        using var w = new ListsBehaviourWorld();
        var a = new obj { name = "alpha" };
        var b = new mob { name = "Beta" };
        AssertDmEqual(-1f, GlobalProcs.cmp_name_asc(a, b));
        AssertDmEqual(1f, GlobalProcs.cmp_name_dsc(a, b));
        AssertDmEqual(0f, GlobalProcs.cmp_name_asc(a, new obj { name = "ALPHA" }));
        Assert.ThrowsAny<Exception>(() => GlobalProcs.cmp_name_asc(a, null));   // b.name of null
    }

    [Fact]
    public void InitNameComparatorReadsTheCompileTimeNames()
    {
        // sorttext(initial(b.name), initial(a.name)): a renamed instance still sorts by its type's name.
        using var w = new ListsBehaviourWorld();
        var apple = new Apple { name = "zzz" };
        var banana = new Banana { name = "aaa" };
        AssertDmEqual(-1f, GlobalProcs.cmp_init_name_asc(apple, banana));
        AssertDmEqual(1f, GlobalProcs.cmp_name_asc(apple, banana));          // the current names say the opposite
        AssertDmEqual(1f, GlobalProcs.cmp_init_name_asc(banana, apple));
    }

    // ---- cmp_records_asc, cmp_records_dsc (:42, :45)

    [Fact]
    public void RecordComparatorsUseTheRecordNames()
    {
        using var w = new ListsBehaviourWorld();
        var zed = new datum_record { name = "Zed" };
        var amy = new datum_record { name = "amy" };
        var unknown = new datum_record();                                     // "Unknown" (record.dm:20)
        AssertDmEqual(1f, GlobalProcs.cmp_records_asc(zed, amy));
        AssertDmEqual(-1f, GlobalProcs.cmp_records_dsc(zed, amy));
        DmList records = DL(zed, unknown, amy);
        GlobalProcs.sortTim(records, Ref(nameof(GlobalProcs.cmp_records_asc)));
        AssertItems(records, amy, unknown, zed);
        GlobalProcs.sortTim(records, Ref(nameof(GlobalProcs.cmp_records_dsc)));
        AssertItems(records, zed, unknown, amy);
    }

    // ---- cmp_datum_* (:49-59): they read datum.vars, which is not translated (rules 10.7)

    [Fact]
    public void DatumVarComparatorsAreNotTranslated()
    {
        using var w = new ListsBehaviourWorld();
        var r = new datum_record();
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.cmp_datum_numeric_asc(r, r, "name"));
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.cmp_datum_numeric_dsc(r, r, "name"));
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.cmp_datum_text_asc(r, r, "name"));
        Assert.Throws<DmNotTranslatedException>(() => GlobalProcs.cmp_datum_text_dsc(r, r, "name"));
    }

    // ---- cmp_ckey_asc, cmp_ckey_dsc, cmp_playtime_asc, cmp_playtime_dsc (:61-71)

    private sealed class PlaytimeClient : client
    {
        private readonly object _exp;

        public PlaytimeClient(string key, object exp)
        {
            ckey = key;
            _exp = exp;
        }

        public readonly List<float?> Asked = new();

        public override object get_exp_living(float? pure_numeric = null)
        {
            Asked.Add(pure_numeric);
            return _exp;
        }
    }

    [Fact]
    public void CkeyComparators()
    {
        using var w = new ListsBehaviourWorld();
        var bob = new client { ckey = "bob" };
        var alice = new client { ckey = "alice" };
        AssertDmEqual(1f, GlobalProcs.cmp_ckey_asc(bob, alice));
        AssertDmEqual(-1f, GlobalProcs.cmp_ckey_dsc(bob, alice));
        AssertDmEqual(0f, GlobalProcs.cmp_ckey_asc(bob, new client { ckey = "bob" }));
    }

    [Fact]
    public void PlaytimeComparatorsAreBothAscending()
    {
        // tg's cmp_playtime_dsc has the same body as cmp_playtime_asc (cmp.dm:70-71): both are ascending.
        using var w = new ListsBehaviourWorld();
        var veteran = new PlaytimeClient("vet", 600f);
        var rookie = new PlaytimeClient("new", 30f);
        AssertDmEqual(570f, GlobalProcs.cmp_playtime_asc(veteran, rookie));
        AssertDmEqual(570f, GlobalProcs.cmp_playtime_dsc(veteran, rookie));
        Assert.Equal(new float?[] { 1f, 1f }, veteran.Asked);                // get_exp_living(TRUE)
        DmList clients = DL(veteran, rookie);
        GlobalProcs.sortTim(clients, Ref(nameof(GlobalProcs.cmp_playtime_dsc)));
        AssertItems(clients, rookie, veteran);
    }

    // ---- the subsystem comparators (:73-83)

    private sealed class EarlySubsystem : datum_controller_subsystem
    {
        public EarlySubsystem() { init_stage = 1; }   // INITSTAGE_FIRST
    }

    [Fact]
    public void SubsystemComparators()
    {
        using var w = new ListsBehaviourWorld();
        var a = new datum_controller_subsystem { init_order = 10, priority = 5, name = "Atoms" };
        var b = new datum_controller_subsystem { init_order = 2, priority = 9, name = "air" };
        AssertDmEqual(8f, GlobalProcs.cmp_subsystem_init(a, b));
        AssertDmEqual(-4f, GlobalProcs.cmp_subsystem_priority(a, b));
        AssertDmEqual(1f, GlobalProcs.cmp_subsystem_display(a, b));         // "air" before "Atoms"
        // initial(a.init_stage): the type's value (INITSTAGE_MAIN, 3), whatever the instance holds now.
        a.init_stage = 99;
        var early = new EarlySubsystem();
        AssertDmEqual(0f, GlobalProcs.cmp_subsystem_init_stage(a, b));
        AssertDmEqual(-2f, GlobalProcs.cmp_subsystem_init_stage(early, b));
        AssertDmEqual(2f, GlobalProcs.cmp_subsystem_init_stage(a, early));
    }

    // ---- assoc-list comparators: cmp_filter_data_priority (:85), cmp_generic_stat_item_time (:103),
    //      cmp_profile_* (:108-115), cmp_assoc_list_name (:195)

    [Fact]
    public void FilterDataPriority()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-3f, GlobalProcs.cmp_filter_data_priority(Dm.list(Dm.kv("priority", 1f)), Dm.list(Dm.kv("priority", 4f))));
        AssertDmEqual(2f, GlobalProcs.cmp_filter_data_priority(Dm.list(Dm.kv("priority", 2f)), Dm.list()));   // missing: null, 0
    }

    [Fact]
    public void GenericStatItemTimeIsDescendingByTimeThenByCount()
    {
        // `. = B[STAT_ENTRY_TIME] - A[STAT_ENTRY_TIME]`, then the counts when the times are equal (STAT_ENTRY_TIME 1,
        // STAT_ENTRY_COUNT 2, __DEFINES/stat_tracking.dm).
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-3f, GlobalProcs.cmp_generic_stat_item_time(DL(5f, 1f), DL(2f, 9f)));
        AssertDmEqual(8f, GlobalProcs.cmp_generic_stat_item_time(DL(5f, 1f), DL(5f, 9f)));
        AssertDmEqual(0f, GlobalProcs.cmp_generic_stat_item_time(DL(5f, 9f), DL(5f, 9f)));
        DmList stats = DL(DL(1f, 5f, "a"), DL(3f, 1f, "b"), DL(1f, 7f, "c"), DL(3f, 1f, "d"));
        GlobalProcs.sortTim(stats, Ref(nameof(GlobalProcs.cmp_generic_stat_item_time)));
        Assert.Equal(new object[] { "b", "d", "c", "a" }, Items(stats).Select(l => ((DmList)l)[3f]).ToArray());
    }

    [Fact]
    public void ProfileComparatorsAreDescending()
    {
        // PROFILE_ITEM_TIME 1, PROFILE_ITEM_COUNT 2 (__DEFINES/profile.dm).
        using var w = new ListsBehaviourWorld();
        DmList a = DL(10f, 2f), b = DL(9f, 3f);
        AssertDmEqual(-1f, GlobalProcs.cmp_profile_time_dsc(a, b));
        AssertDmEqual(1f, GlobalProcs.cmp_profile_count_dsc(a, b));
        // average: 9/3 - 10/2 = -2; a zero or missing count divides by 1 (`|| 1`).
        AssertDmEqual(-2f, GlobalProcs.cmp_profile_avg_time_dsc(a, b));
        AssertDmEqual(-1f, GlobalProcs.cmp_profile_avg_time_dsc(DL(8f, 0f), DL(7f, null)));
        DmList profiles = DL(DL(4f, 4f), DL(9f, 1f), DL(6f, 2f));
        GlobalProcs.sortTim(profiles, Ref(nameof(GlobalProcs.cmp_profile_avg_time_dsc)));
        Assert.Equal(new[] { 9f, 6f, 4f }, Items(profiles).Select(l => Dm.num(((DmList)l)[1f])).ToArray());
    }

    [Fact]
    public void AssocListNameIsAscendingByTheNameKey()
    {
        using var w = new ListsBehaviourWorld();
        DmList x = Dm.list(Dm.kv("name", "beta")), y = Dm.list(Dm.kv("name", "Alpha")), z = Dm.list(Dm.kv("name", "gamma"));
        AssertDmEqual(1f, GlobalProcs.cmp_assoc_list_name(x, y));
        DmList entries = DL(z, x, y);
        GlobalProcs.sortTim(entries, Ref(nameof(GlobalProcs.cmp_assoc_list_name)));
        AssertItems(entries, y, x, z);
    }

    // ---- cmp_timer (:88), cmp_ruincost_priority (:91), cmp_qdel_item_time (:94)

    private sealed class CheapRuin : datum_map_template_ruin
    {
        public CheapRuin() { cost = 1; }
    }

    private sealed class DearRuin : datum_map_template_ruin
    {
        public DearRuin() { cost = 7; }
    }

    [Fact]
    public void TimerAndRuinCostComparators()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-50f, GlobalProcs.cmp_timer(new datum_timedevent { timeToRun = 50f }, new datum_timedevent { timeToRun = 100f }));
        AssertDmEqual(50f, GlobalProcs.cmp_timer(new datum_timedevent { timeToRun = 50f }, new datum_timedevent()));
        // initial(A.cost): an instance's changed cost does not count.
        var dear = new DearRuin { cost = 0 };
        AssertDmEqual(6f, GlobalProcs.cmp_ruincost_priority(dear, new CheapRuin()));
        AssertDmEqual(-7f, GlobalProcs.cmp_ruincost_priority(new datum_map_template_ruin(), dear));
    }

    [Fact]
    public void QdelItemTimeComparesHardDeleteTimeThenDestroyTimeThenFailuresThenQdels()
    {
        // cmp.dm:94-101: . = B.hard_delete_time - A.hard_delete_time, then destroy_time, failures and qdels while 0.
        using var w = new ListsBehaviourWorld();
        datum_qdel_item Item(float hard, float destroy, float failures, float qdels)
            => new datum_qdel_item { hard_delete_time = hard, destroy_time = destroy, failures = failures, qdels = qdels };
        AssertDmEqual(3f, GlobalProcs.cmp_qdel_item_time(Item(1, 9, 9, 9), Item(4, 0, 0, 0)));
        AssertDmEqual(-2f, GlobalProcs.cmp_qdel_item_time(Item(5, 7, 9, 9), Item(5, 5, 0, 0)));
        AssertDmEqual(1f, GlobalProcs.cmp_qdel_item_time(Item(5, 5, 2, 9), Item(5, 5, 3, 0)));
        AssertDmEqual(-4f, GlobalProcs.cmp_qdel_item_time(Item(5, 5, 3, 6), Item(5, 5, 3, 2)));
        AssertDmEqual(0f, GlobalProcs.cmp_qdel_item_time(Item(5, 5, 3, 2), Item(5, 5, 3, 2)));
    }

    // ---- cmp_atom_layer_asc (:117)

    [Fact]
    public void AtomLayerComparesPlanesFirstThenLayers()
    {
        using var w = new ListsBehaviourWorld();
        var low = new obj { plane = 0, layer = 9 };
        var high = new obj { plane = 1, layer = 2 };
        var high2 = new obj { plane = 1, layer = 3.5f };
        AssertDmEqual(-1f, GlobalProcs.cmp_atom_layer_asc(low, high));
        AssertDmEqual(-1.5f, GlobalProcs.cmp_atom_layer_asc(high, high2));
        DmList atoms = DL(high2, low, high);
        GlobalProcs.sortTim(atoms, Ref(nameof(GlobalProcs.cmp_atom_layer_asc)));
        AssertItems(atoms, low, high, high2);
    }

    // ---- cmp_advdisease_resistance_asc (:123)

    private sealed class Disease : datum_disease_advance
    {
        private readonly float _resistance;
        private readonly List<string> _log;
        private readonly string _id;

        public Disease(string id, float resistance, List<string> log)
        {
            _id = id;
            _resistance = resistance;
            _log = log;
        }

        public override object totalResistance()
        {
            _log.Add(_id);
            return _resistance;
        }
    }

    [Fact]
    public void AdvancedDiseaseResistanceCallsATotalResistanceThenB()
    {
        using var w = new ListsBehaviourWorld();
        var log = new List<string>();
        AssertDmEqual(-3f, GlobalProcs.cmp_advdisease_resistance_asc(new Disease("a", 2f, log), new Disease("b", 5f, log)));
        Assert.Equal(new[] { "a", "b" }, log);
    }

    // ---- cmp_quirk_asc (:126)

    private sealed class GoodQuirkB : datum_quirk
    {
        public GoodQuirkB() { name = "Bravery"; value = 4; }
    }

    private sealed class GoodQuirkA : datum_quirk
    {
        public GoodQuirkA() { name = "alertness"; value = 1; }
    }

    private sealed class BadQuirk : datum_quirk
    {
        public BadQuirk() { name = "Clumsy"; value = -2; }
    }

    private sealed class NeutralQuirk : datum_quirk
    {
        public NeutralQuirk() { name = "Accent"; value = 0; }
    }

    [Fact]
    public void QuirksSortPositiveThenNegativeThenNeutralAndByNameWithin()
    {
        // a_sign = sign(initial(A.value) * -1): a positive value gives -1, a negative one 1, a neutral one 0, made 2.
        using var w = new ListsBehaviourWorld();
        var bravery = new GoodQuirkB();
        var alertness = new GoodQuirkA();
        var clumsy = new BadQuirk { value = 100 };                           // initial() reads -2
        var accent = new NeutralQuirk();
        var test = new datum_quirk();                                         // "Test Quirk", 0
        AssertDmEqual(-2f, GlobalProcs.cmp_quirk_asc(bravery, clumsy));      // -1 - 1
        AssertDmEqual(-3f, GlobalProcs.cmp_quirk_asc(bravery, accent));      // -1 - 2
        AssertDmEqual(-1f, GlobalProcs.cmp_quirk_asc(clumsy, accent));       // 1 - 2
        AssertDmEqual(1f, GlobalProcs.cmp_quirk_asc(bravery, alertness));    // same sign: sorttext("alertness", "Bravery")
        AssertDmEqual(0f, GlobalProcs.cmp_quirk_asc(accent, new NeutralQuirk()));
        DmList quirks = DL(test, clumsy, accent, bravery, alertness);
        GlobalProcs.sortTim(quirks, Ref(nameof(GlobalProcs.cmp_quirk_asc)));
        AssertItems(quirks, alertness, bravery, clumsy, accent, test);
    }

    // ---- cmp_job_display_with_departments_asc (:144), cmp_department_display_asc (:147)

    private sealed class Job : datum_job
    {
        private readonly object _order;

        public Job(object order) { _order = order; }

        public override object display_order_with_department() => _order;
    }

    [Fact]
    public void JobAndDepartmentDisplayOrders()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-9f, GlobalProcs.cmp_job_display_with_departments_asc(new Job(1f), new Job(10f)));
        AssertDmEqual(4f, GlobalProcs.cmp_department_display_asc(new datum_job_department { display_order = 5 }, new datum_job_department { display_order = 1 }));
    }

    // ---- cmp_reagents_asc (:150), cmp_typepaths_asc (:153), cmp_num_string_asc (:156), cmp_mob_realname_dsc (:159)

    private sealed class Water : datum_reagent
    {
        public Water() { name = "Water"; }
    }

    private sealed class Ammonia : datum_reagent
    {
        public Ammonia() { name = "ammonia"; }
    }

    [Fact]
    public void ReagentsSortByTheirCompileTimeNames()
    {
        using var w = new ListsBehaviourWorld();
        var water = new Water { name = "aaa" };
        AssertDmEqual(1f, GlobalProcs.cmp_reagents_asc(water, new Ammonia()));
        AssertDmEqual(1f, GlobalProcs.cmp_reagents_asc(new Ammonia(), new datum_reagent()));   // "" sorts first
    }

    [Fact]
    public void TypepathsSortByTheirPathText()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(1f, GlobalProcs.cmp_typepaths_asc(typeof(obj), typeof(mob)));
        AssertDmEqual(-1f, GlobalProcs.cmp_typepaths_asc(typeof(obj), typeof(obj_item)));   // "/obj" is a prefix of "/obj/item"
        DmList paths = DL(typeof(obj_item), typeof(mob), typeof(datum_reagent), typeof(obj));
        GlobalProcs.sortTim(paths, Ref(nameof(GlobalProcs.cmp_typepaths_asc)));
        AssertItems(paths, typeof(datum_reagent), typeof(mob), typeof(obj), typeof(obj_item));
    }

    [Fact]
    public void NumStringComparatorSubtractsTheNumbers()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(1f, GlobalProcs.cmp_num_string_asc("10", "9"));
        AssertDmEqual(-7.5f, GlobalProcs.cmp_num_string_asc("2.5", "10"));
        AssertDmEqual(-5f, GlobalProcs.cmp_num_string_asc("abc", "5"));     // text2num("abc") is null: 0
        AssertDmEqual(3f, GlobalProcs.cmp_num_string_asc("3 apples", null));  // the leading number
        DmList numbers = DL("10", "9", "100", "-1");
        GlobalProcs.sortTim(numbers, Ref(nameof(GlobalProcs.cmp_num_string_asc)));
        AssertItems(numbers, "-1", "9", "10", "100");
    }

    [Fact]
    public void MobRealNameIsDescending()
    {
        using var w = new ListsBehaviourWorld();
        var anna = new mob { real_name = "Anna" };
        var boris = new mob { real_name = "boris" };
        AssertDmEqual(1f, GlobalProcs.cmp_mob_realname_dsc(anna, boris));
        DmList mobs = DL(anna, boris);
        GlobalProcs.sortTim(mobs, Ref(nameof(GlobalProcs.cmp_mob_realname_dsc)));
        AssertItems(mobs, boris, anna);
    }

    // ---- cmp_bodypart_by_body_part_asc (:163), cmp_port_order_asc (:167), cmp_uplink_category_desc (:171)

    private sealed class Head : obj_item_bodypart
    {
        public Head() { body_part = 1f; }      // HEAD
    }

    private sealed class Chest : obj_item_bodypart
    {
        public Chest() { body_part = 2f; }     // CHEST
    }

    private sealed class Surplus : datum_uplink_category
    {
        public Surplus() { weight = 10; }
    }

    [Fact]
    public void BodypartPortAndUplinkCategoryComparators()
    {
        using var w = new ListsBehaviourWorld();
        // limb::body_part reads the compile-time value of the instance's type (rules 10.3).
        var head = new Head { body_part = 512f };
        AssertDmEqual(-1f, GlobalProcs.cmp_bodypart_by_body_part_asc(head, new Chest()));
        AssertDmEqual(0f, GlobalProcs.cmp_bodypart_by_body_part_asc(new obj_item_bodypart(), new obj_item_bodypart()));   // null - null
        // compare1.order - compare2.order: the current values (default 1, port.dm:26).
        AssertDmEqual(-2f, GlobalProcs.cmp_port_order_asc(new datum_port(), new datum_port { order = 3 }));
        AssertDmEqual(1f, GlobalProcs.cmp_port_order_asc(new datum_port(), new datum_port { order = null }));
        // initial(compare2.weight) - initial(compare1.weight): descending.
        AssertDmEqual(-10f, GlobalProcs.cmp_uplink_category_desc(new Surplus(), new datum_uplink_category { weight = 50 }));
    }

    // ---- cmp_crafting_req_priority (:181), cmp_heretic_knowledge (:191), cmp_mob_health (:199), cmp_deathmatch_mods (:202)

    private sealed class Water2 : datum_reagent
    {
    }

    private sealed class EarlyKnowledge : datum_heretic_knowledge
    {
        public EarlyKnowledge() { priority = 5; }
    }

    [Fact]
    public void CraftingRequirementsPutReagentPathsFirst()
    {
        using var w = new ListsBehaviourWorld();
        AssertDmEqual(-1f, GlobalProcs.cmp_crafting_req_priority(typeof(Water2), typeof(obj_item)));
        AssertDmEqual(1f, GlobalProcs.cmp_crafting_req_priority(typeof(obj_item), typeof(datum_reagent)));
        AssertDmEqual(0f, GlobalProcs.cmp_crafting_req_priority(typeof(mob), typeof(obj)));
        // ispath() of an instance is false: a reagent object is not a reagent path.
        AssertDmEqual(1f, GlobalProcs.cmp_crafting_req_priority(new datum_reagent(), typeof(datum_reagent)));
        DmList reqs = DL(typeof(obj_item), typeof(datum_reagent), typeof(mob), typeof(Water2));
        GlobalProcs.sortTim(reqs, Ref(nameof(GlobalProcs.cmp_crafting_req_priority)));
        AssertItems(reqs, typeof(datum_reagent), typeof(Water2), typeof(obj_item), typeof(mob));
    }

    [Fact]
    public void HereticKnowledgeMobHealthAndDeathmatchModifiers()
    {
        using var w = new ListsBehaviourWorld();
        // initial(knowledge_b.priority) - initial(knowledge_a.priority): higher priority first.
        var early = new EarlyKnowledge { priority = -100 };
        AssertDmEqual(-5f, GlobalProcs.cmp_heretic_knowledge(early, new datum_heretic_knowledge()));
        // mob_b.health - mob_a.health: healthier first.
        var hurt = new mob_living { health = 20 };
        var fine = new mob_living();                                         // MAX_LIVING_HEALTH
        AssertDmEqual(80f, GlobalProcs.cmp_mob_health(hurt, fine));
        DmList mobs = DL(hurt, fine);
        GlobalProcs.sortTim(mobs, Ref(nameof(GlobalProcs.cmp_mob_health)));
        AssertItems(mobs, fine, hurt);
        // sorttext(b.name, a.name): ascending by name.
        AssertDmEqual(-1f, GlobalProcs.cmp_deathmatch_mods(new datum_deathmatch_modifier { name = "Fast" }, new datum_deathmatch_modifier { name = "slow" }));
        AssertDmEqual(1f, GlobalProcs.cmp_deathmatch_mods(new datum_deathmatch_modifier(), new datum_deathmatch_modifier { name = "Big heads" }));
    }

    // ---- cmp_fish_fluid (:209)

    private sealed class Goldfish : obj_item_fish
    {
        public Goldfish() { name = "goldfish"; }
    }

    private sealed class Angelfish : obj_item_fish
    {
        public Angelfish() { name = "angelfish"; }
    }

    private sealed class Clownfish : obj_item_fish
    {
        public Clownfish() { name = "clownfish"; required_fluid_type = "Saltwater"; }
    }

    private sealed class Lavafish : obj_item_fish
    {
        public Lavafish() { name = "lavafish"; required_fluid_type = "Air"; }
    }

    private sealed class Strangefish : obj_item_fish
    {
        public Strangefish() { name = "strange"; required_fluid_type = "Plasma"; }
    }

    [Fact]
    public void FishSortByFluidOrderThenByNameDescending()
    {
        // cmp_numeric_asc(position_a, position_b) || cmp_text_asc(initial(b.name), initial(a.name)): the fluids in
        // fluids_priority order (an unknown fluid's Find is 0, first), then, for one fluid, cmp_text_asc with the
        // names swapped, which is sorttext(a_name, b_name): names in descending order.
        using var w = new ListsBehaviourWorld();
        var gold = new Goldfish();
        var angel = new Angelfish();
        var clown = new Clownfish();
        var lava = new Lavafish();
        var odd = new Strangefish();
        AssertDmEqual(-1f, GlobalProcs.cmp_fish_fluid(gold, clown));          // Find: 1 - 2
        AssertDmEqual(-6f, GlobalProcs.cmp_fish_fluid(odd, lava));            // 0 - 6
        AssertDmEqual(1f, GlobalProcs.cmp_fish_fluid(angel, gold));           // same fluid: "angelfish" goes after
        // initial() of the type: the instance's current fluid and name do not count.
        var renamed = new Goldfish { name = "aaa", required_fluid_type = "Air" };
        AssertDmEqual(0f, GlobalProcs.cmp_fish_fluid(renamed, gold));
        DmList fish = DL(lava, angel, clown, odd, gold);
        GlobalProcs.sortTim(fish, Ref(nameof(GlobalProcs.cmp_fish_fluid)));
        AssertItems(fish, odd, gold, angel, clown, lava);
    }

    // ---- cmp_vending_prices (:223), cmp_item_vending_prices (:226), cmp_rped_sort (:230),
    //      cmp_camera_ctag_asc (:235), cmp_client_colours (:239)

    private sealed class RatedPart : obj_item
    {
        private readonly float _rating;
        private readonly List<float> _log;

        public RatedPart(float rating, List<float> log)
        {
            _rating = rating;
            _log = log;
        }

        public override object get_part_rating()
        {
            _log.Add(_rating);
            return _rating;
        }
    }

    [Fact]
    public void PriceComparatorsAreDescending()
    {
        using var w = new ListsBehaviourWorld();
        var cheap = new datum_data_vending_product { price = 5f };
        var dear = new datum_data_vending_product { price = 50f };
        AssertDmEqual(45f, GlobalProcs.cmp_vending_prices(cheap, dear));
        AssertDmEqual(-5f, GlobalProcs.cmp_vending_prices(cheap, new datum_data_vending_product()));   // null price: 0
        AssertDmEqual(-10f, GlobalProcs.cmp_item_vending_prices(new obj_item { custom_price = 30f }, new obj_item { custom_price = 20f }));
    }

    [Fact]
    public void RpedSortPutsHigherTiersFirstAndRatesTheSecondItemFirst()
    {
        using var w = new ListsBehaviourWorld();
        var log = new List<float>();
        var tier1 = new RatedPart(1f, log);
        var tier4 = new RatedPart(4f, log);
        AssertDmEqual(3f, GlobalProcs.cmp_rped_sort(tier1, tier4));
        Assert.Equal(new[] { 4f, 1f }, log);                                  // second_item.get_part_rating() first
        DmList parts = DL(tier1, tier4);
        GlobalProcs.sortTim(parts, Ref(nameof(GlobalProcs.cmp_rped_sort)));
        AssertItems(parts, tier4, tier1);
        // The base /obj/item/get_part_rating (items.dm:1308) rates any item 0 (translated with the RPED, gaps OD3).
        AssertDmEqual(0f, GlobalProcs.cmp_rped_sort(new obj_item(), new obj_item()));
    }

    [Fact]
    public void CameraTagsAndClientColours()
    {
        using var w = new ListsBehaviourWorld();
        var bridge = new obj_machinery_camera { c_tag = "Bridge" };
        var arrivals = new obj_machinery_camera { c_tag = "arrivals" };
        AssertDmEqual(1f, GlobalProcs.cmp_camera_ctag_asc(bridge, arrivals));
        AssertDmEqual(-1f, GlobalProcs.cmp_camera_ctag_asc(new obj_machinery_camera(), arrivals));   // a null tag first
        // second_color.priority - first_color.priority: higher priority first (default CLIENT_COLOR_FILTER_PRIORITY 4).
        AssertDmEqual(-3f, GlobalProcs.cmp_client_colours(new datum_client_colour(), new datum_client_colour { priority = 1 }));
    }
}
