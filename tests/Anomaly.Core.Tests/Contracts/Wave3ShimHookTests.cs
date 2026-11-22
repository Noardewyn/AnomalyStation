using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The shim hooks of the wave-3 contracts (W3.0, S1-S8): world.map_cpu, the /world vars of code/world.dm, typesof of a
/// proc path, exact log of integral powers, the host file seam, rand_seed, the map grid hook behind world.maxx/maxy/maxz
/// and Dm.Deferred (docs/ai/translation-rules.md 1.4 and Appendix C; docs/ai/DEVIATIONS.md DV3, DV11).
/// </summary>
public class Wave3ShimHookTests
{
    // ---- S1: world.map_cpu ----

    [Fact]
    public void MapCpuIsAlwaysZero()
    {
        var w = new World();
        w.Step(5);
        using (w.Enter())
            Assert.Equal(0f, Dm.world.map_cpu); // DV3: tick usage is virtual; TICK_LIMIT_RUNNING reads it
    }

    // ---- S2: the /world vars of code/world.dm ----

    [Fact]
    public void WorldVarsHaveTheShimDefaultsAndArePerWorldStorage()
    {
        var w1 = new World();
        var w2 = new World();
        using (w1.Enter())
        {
            Assert.Null(Dm.world.turf);
            Assert.Null(Dm.world.area);
            Assert.Null(Dm.world.mob);
            Assert.Null(Dm.world.name);
            Assert.True(Dm.eq(Dm.world.view, 5));   // BYOND's default view
            Assert.Equal(0, Dm.world.@params.Count); // no start parameters
            Assert.Equal(Dm.UNIX, Dm.world.system_type);
            Assert.Equal(0f, Dm.world.sleep_offline);

            // What the Tg bootstrap does with tg's code/world.dm defaults.
            Dm.world.turf = typeof(datum);
            Dm.world.area = typeof(atom);
            Dm.world.mob = typeof(mob);
            Dm.world.name = "/tg/ Station 13";
            Dm.world.view = "15x15";
            Dm.world.sleep_offline = 1;
            Dm.world.@params["config-directory"] = "cfg";
            Assert.Same(typeof(datum), Dm.world.turf);
            Assert.Equal("15x15", Dm.world.view);
            Assert.Equal("/tg/ Station 13", Dm.world.name);
            Assert.Equal(1f, Dm.world.sleep_offline);
            Assert.Equal("cfg", Dm.world.@params["config-directory"]);
        }
        using (w2.Enter())
        {
            Assert.Null(Dm.world.turf);
            Assert.Null(Dm.world.@params["config-directory"]);
        }
    }

    [Fact]
    public void WorldVarsAreHashedOnlyWhenChanged()
    {
        var a = new World(3);
        var b = new World(3);
        using (a.Enter())
            _ = Dm.world.view; // creates the shim state: adds nothing to the hash
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        using (a.Enter())
            Dm.world.turf = typeof(datum);
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
        using (b.Enter())
            Dm.world.turf = typeof(datum);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }

    // ---- S3: typesof(/t/proc) lists only the procs the binder resolves ----

    [Fact]
    public void TypesofOfTheGlobalVarsProcsListsOnlyTranslatedProcsInProcOrder()
    {
        var w = new World();
        using (w.Enter())
        {
            DmList procs = Dm.typesof("/datum/controller/global_vars/proc");
            var names = new List<string>();
            foreach (object item in Dm.each(procs)) names.Add(((string)item)["/datum/controller/global_vars/proc/".Length..]);

            // A translated InitGlobal proc is listed; one that is not translated yet does not exist.
            Assert.Contains("InitGlobalsortInstance", names);
            Assert.DoesNotContain("InitGlobalctf_games", names); // __DEFINES/ctf.dm:11, not translated yet (gaps C3 translated ghost_role_flags, the earlier example)
            Assert.True(names.Count < ProcOrder.ProcsOf("/datum/controller/global_vars").Length);

            // Every listed proc can be called by name, and the list keeps the generated (compile) order.
            var holder = new datum_controller_global_vars();
            foreach (string name in names) Assert.True(Dm.hascall(holder, name), name);
            List<string> order = ProcOrder.ProcsOf("/datum/controller/global_vars").ToList();
            Assert.Equal(names.OrderBy(order.IndexOf).ToList(), names);
        }
    }

    // ---- S4: log exact for integral powers of the base ----

    [Theory]
    [InlineData(2f, 1f, 0f)]
    [InlineData(2f, 2f, 1f)]
    [InlineData(2f, 4f, 2f)]
    [InlineData(2f, 8f, 3f)]
    [InlineData(2f, 16777216f, 24f)]
    [InlineData(2f, 0.5f, -1f)]
    [InlineData(10f, 1000f, 3f)]
    [InlineData(10f, 0.001f, -3f)]
    [InlineData(3f, 243f, 5f)]
    public void LogIsExactForIntegralPowersOfTheBase(float X, float Y, float expected)
    {
        // master.dm:558: SetRunLevel indexes a list with log(2, new_runlevel) + 1.
        Assert.Equal(expected, Dm.log(X, Y));
    }

    [Fact]
    public void LogOfOtherValuesIsTheQuotient()
    {
        Assert.Equal((float)(Math.Log(3) / Math.Log(2)), Dm.log(2f, 3f));
        Assert.Equal((float)(Math.Log(10) / Math.Log(4)), Dm.log(4f, 10f));
        Assert.Throws<DmRuntimeException>(() => Dm.log(1f, 8f));
    }

    // ---- S5: the host file seam ----

    [Fact]
    public void FileBuiltinsWorkOnTheWorldsInMemoryStore()
    {
        var w = new World();
        using (w.Enter())
        {
            Assert.IsType<DmMemoryFiles>(DmWorldState.Current.Files);
            Assert.Equal(0f, Dm.fexists("config/config.txt"));
            Assert.Null(Dm.file2text("config/config.txt"));

            Assert.Equal(1f, Dm.text2file("FPS 20\n", "config/config.txt"));
            Assert.Equal(1f, Dm.text2file("TICKLAG 0.5", "config/config.txt")); // appends
            Assert.Equal("FPS 20\nTICKLAG 0.5", Dm.file2text("config/config.txt"));
            Assert.Equal(1f, Dm.fexists("config/config.txt"));
            Assert.Equal(1f, Dm.fexists("config/")); // a directory exists while a file is inside it

            DmFileRef f = Dm.file("data/logs/round.log");
            Assert.True(Dm.isfile(f));
            Assert.False(Dm.isfile("data/logs/round.log"));
            Assert.Equal("data/logs/round.log", Dm.text("[]", f)); // a file reference renders as its path
            Dm.output_to(f, "line one");                            // WRITE_FILE: a line per output
            Dm.output_to(f, 2);
            Assert.Equal("line one\n2\n", Dm.file2text(f));

            Assert.Equal(1f, Dm.fcopy("config/config.txt", "backup/config.txt"));
            Assert.Equal(0f, Dm.fcopy("missing.txt", "backup/missing.txt"));
            Assert.Equal(new object[] { "backup/", "config/", "data/" }, Items(Dm.flist("")));
            Assert.Equal(new object[] { "config.txt" }, Items(Dm.flist("config/con")));

            Assert.Equal(1f, Dm.fdel("backup/config.txt"));
            Assert.Equal(0f, Dm.fdel("backup/config.txt"));
            Assert.Equal(1f, Dm.fdel("data/")); // a directory and everything in it
            Assert.Equal(0f, Dm.fexists(f));
            Assert.Equal(new[] { "config/config.txt" }, ((DmMemoryFiles)DmWorldState.Current.Files).Paths);
        }
        using (new World().Enter())
            Assert.Equal(0f, Dm.fexists("config/config.txt")); // per world
    }

    [Fact]
    public void FileArgumentsAcceptNullAndRefuseWritingADirectory()
    {
        using (new World().Enter())
        {
            Assert.Null(Dm.file(null));
            Assert.Equal(0f, Dm.fexists(null));
            Assert.Equal(0f, Dm.text2file("x", null));
            Assert.Equal(0, Dm.flist(null).Count);
            DmFileRef f = Dm.file("a.txt");
            Assert.Same(f, Dm.file(f));
            Assert.Throws<DmRuntimeException>(() => Dm.text2file("x", "logs/"));
        }
    }

    [Fact]
    public void AHostDirectoryIsTheOnlyRealDiskAndKeepsPathsInsideItsRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "anomaly-files-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            var w = new World();
            DmWorldState.Of(w).Files = new DmDirectoryFiles(root);
            using (w.Enter())
            {
                Assert.Equal(1f, Dm.text2file("hello", "data/logs/a.log"));
                Assert.True(File.Exists(Path.Combine(root, "data", "logs", "a.log")));
                Assert.Equal("hello", Dm.file2text("data/logs/a.log"));
                Assert.Equal(1f, Dm.fexists("data/"));
                Assert.Equal(1f, Dm.fcopy("data/logs/a.log", "data/b.log"));
                Assert.Equal(new object[] { "b.log", "logs/" }, Items(Dm.flist("data/")));
                Assert.Equal(0f, Dm.fexists("../outside.txt"));
                Dm.text2file("x", "../outside.txt"); // dropped: outside the root
                Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root), "outside.txt")));
                Assert.Equal(1f, Dm.fdel("data/"));
                Assert.False(Directory.Exists(Path.Combine(root, "data")));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ---- S6: rand_seed ----

    [Fact]
    public void RandSeedDoesNotTouchTheWorldRng()
    {
        var w = new World(42);
        w.Step(1);
        using (w.Enter())
        {
            ulong before = w.Rng.State;
            Dm.rand_seed(29051994); // DV11: the host seeds the world RNG and the Kernel reseeds it every tick
            Assert.Equal(before, w.Rng.State);
        }
    }

    // ---- S7: the map grid hook ----

    [Fact]
    public void ChangingTheMapSizeCallsTheGridHookWithTheOldSize()
    {
        var w = new World();
        var calls = new List<string>();
        DmWorldState.Of(w).MapResized = (x, y, z) =>
        {
            DmWorldState s = DmWorldState.Of(w);
            calls.Add($"{x},{y},{z}->{s.maxx},{s.maxy},{s.maxz}");
        };
        using (w.Enter())
        {
            Dm.world.maxx = 10;
            Dm.world.maxy = 5;
            Dm.world.maxz++;
            Dm.world.maxx = 10; // no change, no call
            Dm.world.maxx = 12;
        }
        DmWorldState.Of(w).maxz = 7; // the storage itself is silent
        Assert.Equal(new[] { "0,0,0->10,0,0", "10,0,0->10,5,0", "10,5,0->10,5,1", "10,5,1->12,5,1" }, calls);
    }

    // ---- S8: Dm.Deferred ----

    [Fact]
    public void DeferredCountsLogsOncePerSiteAndNeverThrows()
    {
        var w = new World();
        var log = new List<string>();
        DmWorldState state = DmWorldState.Of(w);
        state.WorldLog = log.Add;
        using (w.Enter())
        {
            Dm.Deferred("code/controllers/subsystem/blackbox.dm:231", "record_feedback");
            Dm.Deferred("code/controllers/subsystem/blackbox.dm:231", "record_feedback");
            Dm.Deferred("code/controllers/subsystem/icon_smooth.dm:68", "free_deferred");
        }
        Assert.Equal(3, state.DeferredCount);
        Assert.Equal(new[] { "code/controllers/subsystem/blackbox.dm:231", "code/controllers/subsystem/icon_smooth.dm:68" }, state.DeferredSites);
        Assert.Equal(2, log.Count);
        Assert.Contains("code/controllers/subsystem/blackbox.dm:231", log[0]);
        Assert.Equal(0, w.RuntimeErrorCount); // not a runtime error, unlike Dm.NotTranslated
        Dm.Deferred("code/x.dm:1", "no world"); // no ambient world: nothing happens
        Assert.Equal(0, DmWorldState.Of(new World()).DeferredCount);
    }

    private static object[] Items(DmList list) => Dm.each(list).Cast<object>().ToArray();
}
