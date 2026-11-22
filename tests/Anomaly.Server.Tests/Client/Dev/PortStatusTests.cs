using System.IO;
using Anomaly.Client.Shared.Dev;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Dev;

/// <summary>
/// The port status overlay (the owner, 2026-10-28): a type's colour from the translation tracker's rows, its own procs below
/// the nearest base type, and the real index read whole.
/// </summary>
public class PortStatusTests
{
    private readonly ITestOutputHelper _out;

    public PortStatusTests(ITestOutputHelper output) => _out = output;

    private const string Rows = """
        # a header
        type	/obj	/atom/movable	-	base	obj	1	1
        type	/obj/machinery	/obj	m.dm:1	base	obj_machinery	1	1
        proc	/obj/machinery	emp_act	m.dm:2	severity	no	-	proc root
        type	/obj/machinery/good	/obj/machinery	g.dm:1	live	x	1	1
        proc	/obj/machinery/good	attack_hand	g.dm:2	user	yes	full	proc
        type	/obj/machinery/good/sub	/obj/machinery/good	g.dm:9	live	x	0	1
        type	/obj/machinery/half	/obj/machinery	h.dm:1	live	x	1	1
        proc	/obj/machinery/half	attack_hand	h.dm:2	user	yes	full	proc
        proc	/obj/machinery/half	attack_tk	h.dm:3	user	stub	deferred	proc
        type	/obj/machinery/nohands	/obj/machinery	n.dm:1	live	x	1	1
        proc	/obj/machinery/nohands	ui_act	n.dm:2	action	yes	skeleton	proc
        type	/obj/machinery/dead	/obj/machinery	d.dm:1	inert	-	0	1
        proc	/obj/machinery/dead	examine	d.dm:2	user	no	-	proc
        """;

    [Fact]
    public void TheColourComesFromTheTypesOwnProcsAndNotFromTheBaseTypes()
    {
        PortIndex index = PortIndex.Parse(new StringReader(Rows));

        PortReport good = index.Of("/obj/machinery/good");
        Assert.Equal(PortLevel.Green, good.Level);
        Assert.Equal(1, good.BaseGaps); // /obj/machinery's emp_act, counted apart
        Assert.Equal(PortLevel.Green, index.Of("/obj/machinery/good/sub").Level); // a var-only subtype inherits the parent's procs
        Assert.Single(index.Of("/obj/machinery/good/sub").Own);

        PortReport half = index.Of("/obj/machinery/half");
        Assert.Equal(PortLevel.Yellow, half.Level);
        Assert.Equal("stub (deferred)", half.Own.Single(p => p.Name == "attack_tk").Status);

        Assert.Equal(PortLevel.Red, index.Of("/obj/machinery/nohands").Level); // a key proc that is only a skeleton
        Assert.Equal(PortLevel.Red, index.Of("/obj/machinery/dead").Level);
        Assert.Equal(PortLevel.Unknown, index.Of("/obj/nowhere").Level);
        Assert.Equal(PortLevel.Unknown, index.Of(null).Level);
    }

    [Fact]
    public void TheRealIndexReadsAndColoursMetaStationsThings()
    {
        string path = Path.Combine(RepoRoot(), "docs", "ai", "specs", "generated", "types-index.tsv");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        PortIndex index;
        using (var reader = new StreamReader(path))
            index = PortIndex.Parse(reader);
        _out.WriteLine($"{index.TypeCount} types in {watch.ElapsedMilliseconds} ms");
        Assert.True(index.TypeCount > 40000);

        Assert.Equal(PortLevel.Red, index.Of("/obj/vehicle/sealed/mecha/ripley").Level); // inert at the index's commit (the vending machines were the example until S2, the holopad until silicons SI4)
        // the extinguisher cabinet was the example until gaps ST5 translated its attack_tk
        PortReport bin = index.Of("/obj/structure/bedsheetbin");
        Assert.Equal(PortLevel.Yellow, bin.Level); // add_context is a deferred stub (the screentips, D264)
        Assert.Contains(bin.Own, p => p.Name == "item_interaction" && p.Ported);
    }

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "AnomalyStation.sln")))
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("no AnomalyStation.sln above the tests");
        return dir;
    }
}
