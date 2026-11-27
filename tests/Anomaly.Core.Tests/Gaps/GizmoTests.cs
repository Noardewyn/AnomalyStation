using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Science's gizmos in toy rooms (research/gizmo/): a gizmo takes a random name and makes its wires from its controller's
/// interface, whose puzzle holds one code sequence a gizmode; pulsing a sequence's wires in order pings and then solves it (the
/// gizmode goes off), a wrong wire buzzes and starts over; the beyblade gizmo spins and wanders while its mover runs; the toggle
/// gizmo hums to life and powers down; the voice gizmo listens for its words.
/// </summary>
public class GizmoTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13, int height = 9)
    {
        var r = new AudioRoom(width, height, seed: seed);
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    /// <summary>The wire that enters a code symbol ("Gizmo Pulse N" is the N-th wire of the gizmo's wire set).</summary>
    private static object WireFor(datum_wires_gizmo wires, datum_gizmo_puzzle puzzle, object symbol) =>
        wires.cryptic_wires[puzzle.cryptic_pulse.Find(symbol)];

    [Fact]
    public void PulsingASequenceSolvesItAndAWrongWireBuzzes()
    {
        var (r, feed, bob) = Room(15021);
        var gizmo = r.In(() => Dm.@new<obj_machinery_gizmo>(locate(6, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => Dm.@in(gizmo.name, obj_machinery_gizmo.possible_names)), "the gizmo has no gizmo name");
        var controller = r.In(() => gizmo.controller_value);
        Assert.NotNull(controller);
        var wires = Assert.IsType<datum_wires_gizmo>(r.In(() => gizmo.wires));
        var puzzle = r.In(() => wires.puzzle_value);
        Assert.NotNull(puzzle);
        int sequences = (int)r.In(() => Dm.length(puzzle.code_sequences));
        Assert.True(sequences >= 1, "the puzzle has no sequence");
        Assert.Equal(sequences, r.In(() => Dm.length(puzzle.solution_callbacks)));
        r.In(() =>
        {
            foreach (DmList sequence in Dm.each<DmList>(puzzle.code_sequences))
                Assert.Equal(3, Dm.length(sequence));
        });

        // a wire no sequence starts with buzzes
        object wrong = r.In(() =>
        {
            DmList starts = Dm.list();
            foreach (DmList sequence in Dm.each<DmList>(puzzle.code_sequences))
                Dm.combine(ref starts, sequence[1]);
            foreach (object symbol in Dm.each(puzzle.cryptic_pulse))
                if (!Dm.@in(symbol, starts))
                    return symbol;
            return null;
        });
        if (wrong != null)
        {
            r.In(() => Waiting.NoSleep(wires.pulse(WireFor(wires, puzzle, wrong), bob)));
            r.Step(2);
            Assert.True(feed.Any("balloon: buzz"), "a wrong wire does not buzz");
            Assert.Equal(0, r.In(() => Dm.length(puzzle.current_sequence)));
        }

        // the first sequence: two pings, then solved
        r.Step(Second);
        DmList first = r.In(() => puzzle.code_sequences[1] as DmList);
        object result = null;
        for (int i = 1; i <= 3; i++)
        {
            int index = i;
            result = r.In(() => Waiting.NoSleep(puzzle.on_pulse(puzzle.cryptic_pulse.Find(first[index]), bob, gizmo, TRUE)));
            if (i < 3 && Dm.num(result) == GIZMO_PUZZLE_CORRECT)
                Assert.Equal(index, r.In(() => Dm.length(puzzle.current_sequence)));
        }
        Assert.Contains(Dm.num(result), new float[] { GIZMO_PUZZLE_SOLVED, GIZMO_PUZZLE_SOLVED_MODE_CONTROL });
        Assert.Equal(0, r.In(() => Dm.length(puzzle.current_sequence)));
        r.Step(Second);
    }

    [Fact]
    public void TheBeybladeGizmoSpinsAndWandersWhileItsMoverRuns()
    {
        var (r, _, _) = Room(15022);
        var gizmo = r.In(() => Dm.@new<obj_machinery_gizmo_beyblade>(locate(6, 4, 1)));
        r.Step(2);
        string idle = r.In(() => Dm.str(gizmo.icon_state));
        Assert.False(idle.EndsWith("_spinning", StringComparison.Ordinal));
        var start = r.In(() => Dm.@new<datum_gizpulse_start_moving>());
        r.In(() => Waiting.NoSleep(start.activate(gizmo)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(gizmo.moving)));
        Assert.True(r.In(() => Dm.truthy(gizmo.density)));
        Assert.Equal(idle + "_spinning", r.In(() => Dm.str(gizmo.icon_state)));
        turf from = r.In(() => gizmo.loc as turf);
        bool moved = false;
        for (int i = 0; i < 10 * Second && !moved; i++)
        {
            r.Step(1);
            moved = r.In(() => gizmo.loc != from);
        }
        Assert.True(moved, "the spinning gizmo stayed put");

        var stop = r.In(() => Dm.@new<datum_gizpulse_stop_moving>());
        r.In(() => Waiting.NoSleep(stop.activate(gizmo)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(gizmo.moving)));
        Assert.False(r.In(() => Dm.truthy(gizmo.density)));
        Assert.Equal(idle, r.In(() => Dm.str(gizmo.icon_state)));
        turf stopped = r.In(() => gizmo.loc as turf);
        r.Step(5 * Second);
        Assert.Same(stopped, r.In(() => gizmo.loc as turf));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToggleGizmoHumsToLifeAndPowersDown()
    {
        var (r, feed, _) = Room(15023);
        var tubes = r.In(() => Dm.@new<obj_machinery_gizmo_toggle_tucker_tubes>(locate(6, 4, 1)));
        r.Step(2);
        string idle = r.In(() => Dm.str(tubes.icon_state));
        r.In(() => tubes.SEND_SIGNAL(COMSIG_GIZMO_ON_STATE, Dm.list(tubes)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(tubes.on_state)));
        Assert.Equal(idle + "_on", r.In(() => Dm.str(tubes.icon_state)));
        Assert.True(feed.Any("hums to life."));
        r.In(() => tubes.SEND_SIGNAL(COMSIG_GIZMO_OFF_STATE, Dm.list(tubes)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(tubes.on_state)));
        Assert.Equal(idle, r.In(() => Dm.str(tubes.icon_state)));
        Assert.True(feed.Any("powers down."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVoiceGizmoListensForItsWords()
    {
        var (r, _, _) = Room(15024);
        var gizmo = r.In(() => Dm.@new<obj_machinery_gizmo_voice>(locate(6, 4, 1)));
        r.Step(2);
        Assert.IsType<datum_wires_gizmo>(r.In(() => gizmo.wires));
        var voice = r.In(() => gizmo.GetComponent(typeof(datum_component_gizmo_voice)) as datum_component_gizmo_voice);
        Assert.NotNull(voice);
        Assert.NotNull(r.In(() => voice.puzzle_value));
        r.AssertNoRuntimes();
    }
}
