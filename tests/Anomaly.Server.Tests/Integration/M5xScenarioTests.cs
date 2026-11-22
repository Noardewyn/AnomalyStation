using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5x's demo check (D149, D150) through the real server on MetaStation with headless Godot clients. An engineer in the Aux base
/// construction zone sets a C-4 charge's timer in tg's number window, plants it on the zone's west wall (42,155) and walks out; a
/// second player waits two tiles from the wall. The charge breaches the wall: the victim takes the light blast (hurt, knocked down,
/// deafened), both clients' views shake (protocol 21), the engineer hears the blast, and the zone vents into space through the
/// hole. Then a dev singularity in the Recreation Area eats and grows for 15 s. No runtime beyond the boot's. The second test runs
/// the steps twice from a fresh boot with the same seed and compares the state hashes.
/// </summary>
public class M5xScenarioTests
{
    private const int Second = 20;
    private const int WallX = 42, WallY = 155;

    private readonly ITestOutputHelper _output;

    public M5xScenarioTests(ITestOutputHelper output) => _output = output;

    private static RealHeadlessWorld Boot()
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true) /* the engineer watches the singularity from across the station */, strict: false);
        MetaServer.ClearAuxBase(w.World); // the C4 breaches the empty construction zone
        return w;
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => At(x, y) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    private sealed record Run(RealHeadlessWorld.Player Engineer, RealHeadlessWorld.Player Victim, obj_item_grenade_c4 Charge, List<SoundEvent> EngineerHeard);

    // The C4 part: the timer set in the client's window, the plant, the walk out, the blast and the vent.
    private static Run BreachTheAuxBase(RealHeadlessWorld w, ITestOutputHelper output)
    {
        RealHeadlessWorld.Player engineer = w.Join("Engineer");
        RealHeadlessWorld.Player victim = w.Join("Victim");
        var me = (mob_living_carbon_human)w.MobOf(engineer);
        var heard = new List<SoundEvent>();
        engineer.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => engineer.Session.UiAct(window, action, parameters),
            SendClose = window => engineer.Session.UiClose(window),
        };
        obj_item_grenade_c4 c4;
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(WallX + 1, WallY)));
            Waiting.NoSleep(w.MobOf(victim).forceMove(At(WallX + 3, WallY + 2)));
            c4 = Dm.@new<obj_item_grenade_c4>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(c4))));
        }
        w.Run(Second);

        engineer.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(engineer.Session.Replica.UiWindows.Tgui());
        windows.Sync(engineer.Session.Replica.UiWindows, 1280, 720, 1);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "entry").OnChange(10);
        windows.Sync(engineer.Session.Replica.UiWindows, 1280, 720, 1);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "submit").OnClick();
        w.Run(Second);
        Assert.True(engineer.Session.Click(new ClickTarget(TargetKind.Turf, 0, WallX, WallY, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(4 * Second);
        Assert.Null(In(w, () => c4.loc));

        // out through the zone's airlock (47,150) to the room south of it; the victim steps up beside the wall
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.forceMove(At(48, 148)));
            Waiting.NoSleep(w.MobOf(victim).forceMove(At(WallX + 2, WallY)));
        }
        w.Run(12 * Second); // the 10 s fuse from the plant, the inputs of two players arriving a few ticks late
        output.WriteLine($"the wall: {In(w, () => DmTypes.PathOf(At(WallX, WallY).GetType()))}, the charge gone: {In(w, () => QDELETED(c4))}");
        return new Run(engineer, victim, c4, heard);
    }

    [MetaServerFact]
    public void The_C4_breaches_the_Aux_base_hurting_and_deafening_a_bystander_and_a_singularity_eats_the_recreation_area()
    {
        using RealHeadlessWorld w = Boot();
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        float before = Kpa(w, 48, 155);
        Run run = BreachTheAuxBase(w, _output);
        var victim = (mob_living_carbon_human)w.MobOf(run.Victim);

        Assert.True(In(w, () => QDELETED(run.Charge)), "the charge went off after its 10 s");
        Assert.False(In(w, () => At(WallX, WallY) is turf_closed), "the wall is breached");
        Assert.InRange(In(w, () => Dm.num(victim.get_brute_loss())), 5f, 45f); // the light ex_act's 30 (human_defense.dm), then its random wound roll and the blast's throw
        Assert.True(In(w, () => HAS_TRAIT(victim, Defines.TRAIT_DEAF)), "the bystander is deafened");
        Assert.True(run.Victim.Session.Replica.CameraShakes > 0 && run.Engineer.Session.Replica.CameraShakes > 0, "both views shook");
        Assert.Contains(run.EngineerHeard, e => e.File != null && e.File.StartsWith("sound/effects/explosion/explosion", StringComparison.Ordinal));

        float lowest = before;
        for (int t = 0; t < 60 && lowest >= before * 0.9f; t++)
        {
            w.Run(Second);
            lowest = Math.Min(lowest, Kpa(w, 48, 155));
        }
        _output.WriteLine($"the zone: {before:0.0} kPa before, {lowest:0.0} kPa after the blast");
        Assert.True(lowest < before * 0.9f, "the zone vents through the hole");

        // the singularity part
        run.Engineer.Session.DevCommand("singularity 146 178 1");
        w.Run(15 * Second);
        obj_singularity singulo = In(w, () => Dm.each_cast<datum_component_singularity>(GLOB.singularities).Select(c => c.parent as obj_singularity).FirstOrDefault());
        Assert.NotNull(singulo);
        _output.WriteLine($"the singularity after 15 s: energy {In(w, () => singulo.energy)}, stage {In(w, () => singulo.current_size)}");
        Assert.True(In(w, () => singulo.energy) > 600f, "it grew by what it ate");
        Assert.Contains(run.Engineer.Session.Replica.Entities, e => e.Id == singulo.dm_id);
        // Nothing the players did ran into a runtime but tg's own of a thing the singularity's thrown items push into it
        List<string> others = runtimes.Where(r => !DelaminationGameplayTests.SingularityHitbyRuntime(r)).ToList();
        Assert.True(others.Count == 0, string.Join("\n", others.Take(3)));
        Assert.Equal(bootRuntimes + runtimes.Count, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void The_breach_and_the_singularity_run_twice_from_a_fresh_boot_end_with_the_same_state_hash()
    {
        (ulong AfterBlast, ulong End, long Tick) Once()
        {
            using RealHeadlessWorld w = Boot();
            Run run = BreachTheAuxBase(w, _output);
            ulong afterBlast = w.World.ComputeStateHash();
            run.Engineer.Session.DevCommand("singularity 146 178 1");
            w.Run(10 * Second);
            return (afterBlast, w.World.ComputeStateHash(), w.Ticks);
        }

        var first = Once();
        var second = Once();
        _output.WriteLine($"first run: after the blast {first.AfterBlast:x16}, end {first.End:x16} at tick {first.Tick}; second: {second.AfterBlast:x16}, {second.End:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.AfterBlast, second.AfterBlast);
        Assert.Equal(first.End, second.End);
    }
}
