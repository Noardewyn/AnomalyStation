using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// The cached string lists of code/__HELPERS/string_lists.dm (<c>string_list</c>, <c>baseturfs_string_list</c>) and the
/// strings files of code/__HELPERS/_string_lists.dm (<c>load_strings_file</c>).
/// </summary>
public class StringListTests
{
    [Fact]
    public void EqualListsAreCachedAsTheFirstOneUnderTheirJoinedText()
    {
        using var w = new SupportWorld();
        // M5 slice 8: GLOB's init_crafting_recipes interns the recipes' tool lists (_recipes.dm:68) before the test's own
        float before = GLOB.string_lists.Count;
        DmList first = Dm.list("neutral", "station");
        Assert.Same(first, string_list(first));
        Assert.Same(first, string_list(Dm.list("neutral", "station")));
        Assert.Same(first, GLOB.string_lists["neutral-station"]);
        Assert.NotSame(first, string_list(Dm.list("station", "neutral"))); // the order is part of the key
        Assert.Equal(before + 2, GLOB.string_lists.Count);
    }

    [Fact]
    public void TypepathsJoinAsTheirPathsAndTheJoinedKeyCanCollide()
    {
        using var w = new SupportWorld();
        DmList paths = Dm.list(typeof(turf_test_support_floor), typeof(obj_test_support_box));
        Assert.Same(paths, string_list(paths));
        Assert.Same(paths, GLOB.string_lists["/turf/test_support_floor-/obj/test_support_box"]);

        // tg keys by values.Join("-"): list("a-b") and list("a", "b") are the same entry.
        DmList joined = Dm.list("a-b");
        Assert.Same(joined, string_list(joined));
        Assert.Same(joined, string_list(Dm.list("a", "b")));
    }

    [Fact]
    public void BaseturfsStringListPassesNonListsAndCachesLists()
    {
        using var w = new SupportWorld();
        Assert.Same(typeof(turf_test_support_floor), Waiting.NoSleep(baseturfs_string_list(typeof(turf_test_support_floor), locate(1, 1, 1))));
        DmList baseturfs = Dm.list(typeof(turf_test_support_floor));
        Assert.Same(baseturfs, Waiting.NoSleep(baseturfs_string_list(baseturfs, locate(1, 1, 1))));
        Assert.Same(baseturfs, Waiting.NoSleep(baseturfs_string_list(Dm.list(typeof(turf_test_support_floor)), locate(1, 1, 1))));
    }

    [Fact]
    public void AnOverlongBaseturfsListIsReportedAndReplacedByTheFlashingWall()
    {
        using var w = new SupportWorld();
        DmWorldState.Of(w.World).NotTranslatedThrows = false; // ChangeTurf is M3
        DmList tall = Dm.list_n(11);
        var result = (DmList)Waiting.NoSleep(baseturfs_string_list(tall, locate(1, 1, 1)));
        Assert.Equal(new object[] { typeof(turf_closed_indestructible_baseturfs_ded) }, Dm.each(result).Cast<object>().ToArray());
        Assert.Same(result, GLOB.string_lists["/turf/closed/indestructible/baseturfs_ded"]);
        Assert.Contains(w.Runtimes, e => e.Message.Contains("it should never be this long")); // the stack_trace
        Assert.Contains(w.Runtimes, e => e is DmNotTranslatedException && e.Message.Contains("code/__HELPERS/string_lists.dm:23"));
    }

    [Fact]
    public void TheDebugWallKeepsTgsDefaults()
    {
        var wall = new turf_closed_indestructible_baseturfs_ded();
        Assert.Equal("Report this", wall.name);
        Assert.Equal("fucked_baseturfs", wall.icon_state);
        Assert.Equal(Dm.resource("icons/turf/debug.dmi"), wall.icon);
    }

    [Fact]
    public void AStringsFileIsLoadedOnceIntoTheCache()
    {
        using var w = new SupportWorld();
        w.Files.WriteText("strings/test_support.json", "{\"greetings\": [\"hi\", \"hello\"]}");
        load_strings_file("test_support.json");
        Assert.Equal("test_support.json", GLOB.string_filename_current_key);
        var greetings = (DmList)((DmList)GLOB.string_cache["test_support.json"])["greetings"];
        Assert.Equal(new object[] { "hi", "hello" }, Dm.each(greetings).Cast<object>().ToArray());

        w.Files.WriteText("strings/test_support.json", "{}");
        load_strings_file("test_support.json"); // already cached: no work to do
        Assert.Same(greetings, ((DmList)GLOB.string_cache["test_support.json"])["greetings"]);

        Assert.Contains("file not found: strings/missing.json", Assert.ThrowsAny<Exception>(() => load_strings_file("missing.json")).Message);
        Assert.Equal("missing.json", GLOB.string_filename_current_key);
    }
}
