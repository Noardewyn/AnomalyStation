using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Tools.ToyWorld;

/// <summary>
/// The toy world runner: runs <see cref="ToyScenario"/> (the scripted M0 scenario in tg's real toy room) and shows what
/// happened, so there is something to look at. The whole program minus the console, so a test can run it.
/// Exit codes: 0 fine, 1 <c>--twice</c> found two worlds of one seed that differ, 2 a bad command line or a runtime error in
/// a world.
/// </summary>
public static class ToyWorldRunner
{
    public const string Usage =
        "Usage: Anomaly.Tools.ToyWorld [--seed N] [--twice]\n"
        + "  --seed N   world seed, an unsigned 64-bit integer (default 1)\n"
        + "  --twice    run the scenario in two worlds of one seed and compare their state hashes (exit code 1 if they differ)\n"
        + "Frames: # wall, . floor, o obj, @ mob (its dir is the arrow under the frame).";

    private const int FramesPerBand = 4;
    private const int Gap = 3;

    /// <summary>Runs the program for <paramref name="args"/>, writing to <paramref name="output"/> and <paramref name="error"/>.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ulong seed = ToyScenario.DefaultSeed;
        bool twice = false;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string inline = null;
            int eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 2)
            {
                inline = arg.Substring(eq + 1);
                arg = arg.Substring(0, eq);
            }
            switch (arg)
            {
                case "-h":
                case "--help":
                    output.WriteLine(Usage);
                    return 0;
                case "--twice":
                    twice = true;
                    break;
                case "--seed":
                    string text = inline ?? (i + 1 < args.Count ? args[++i] : null);
                    if (text == null || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                        return Fail(error, "--seed needs an unsigned 64-bit integer");
                    break;
                default:
                    return Fail(error, "unknown argument " + args[i]);
            }
        }
        return twice ? RunTwice(seed, output, error) : RunOnce(seed, output, error);
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine("error: " + message);
        error.WriteLine(Usage);
        return 2;
    }

    // ---- --twice ----

    private static int RunTwice(ulong seed, TextWriter output, TextWriter error)
    {
        ToyScenario first = ToyScenario.Run(seed);
        ToyScenario second = ToyScenario.Run(seed);
        bool same = first.StateHash == second.StateHash;
        output.WriteLine($"toy world, seed {seed}, the scenario in two worlds");
        output.WriteLine($"  run 1: state hash {Hash(first.StateHash)}");
        output.WriteLine($"  run 2: state hash {Hash(second.StateHash)}");
        output.WriteLine(same ? "identical" : "DIFFERENT");
        int runtimes = ReportRuntimes(first, error) + ReportRuntimes(second, error);
        return !same ? 1 : runtimes > 0 ? 2 : 0;
    }

    // ---- one run ----

    private static int RunOnce(ulong seed, TextWriter output, TextWriter error)
    {
        ToyScenario scenario = ToyScenario.Run(seed);
        output.WriteLine($"toy world, seed {seed}: tg's real toy room under the real Master, one mob and one obj");
        output.WriteLine($"ticks are world ticks (20 per second); the first command runs on t{scenario.StartTick}");
        output.WriteLine();
        WriteFrames(scenario, output);
        output.WriteLine("log");
        foreach (ToyLogEntry entry in scenario.Log)
            output.WriteLine("  " + entry);
        output.WriteLine();
        WriteDoAfter(scenario.CompletedDoAfter, "completes", output);
        WriteDoAfter(scenario.CancelledDoAfter, "cancelled by a step", output);
        int runtimes = ReportRuntimes(scenario, output);
        output.WriteLine($"state hash {Hash(scenario.StateHash)} at t{scenario.World.Tick}");
        return runtimes > 0 ? 2 : 0;
    }

    private static void WriteDoAfter(ToyDoAfter doAfter, string what, TextWriter output)
    {
        string result = !doAfter.Done ? "never came back"
            : $"{(doAfter.Succeeded ? "TRUE" : "FALSE")} on t{doAfter.EndTick}, {doAfter.EndTick - doAfter.StartTick} ticks after it started";
        output.WriteLine($"do_after #{doAfter.Number} ({what}): {result}");
    }

    private static int ReportRuntimes(ToyScenario scenario, TextWriter output)
    {
        foreach (Exception e in scenario.Runtimes)
            output.WriteLine("runtime: " + e.GetType().Name + ": " + e.Message.Split('\n')[0]);
        return scenario.Runtimes.Count;
    }

    private static string Hash(ulong hash) => "0x" + hash.ToString("x16", CultureInfo.InvariantCulture);

    // ---- frames, four to a row ----

    private static void WriteFrames(ToyScenario scenario, TextWriter output)
    {
        IReadOnlyList<ToyFrame> frames = scenario.Frames;
        int width = frames.Max(f => f.Rows[0].Length);
        width = Math.Max(width, frames.Max(f => f.Caption.Length));
        for (int first = 0; first < frames.Count; first += FramesPerBand)
        {
            ToyFrame[] band = frames.Skip(first).Take(FramesPerBand).ToArray();
            WriteBandLine(band, width, (f, i) => $"#{first + i + 1} t{f.Tick}", output);
            WriteBandLine(band, width, (f, _) => f.Caption, output);
            for (int row = 0; row < band[0].Rows.Count; row++)
                WriteBandLine(band, width, (f, _) => f.Rows[row], output);
            WriteBandLine(band, width, (f, _) => "@ " + Arrow(f.MobDir) + " " + DirName(f.MobDir), output);
            output.WriteLine();
        }
    }

    private static void WriteBandLine(ToyFrame[] band, int width, Func<ToyFrame, int, string> cell, TextWriter output)
    {
        var line = new StringBuilder();
        for (int i = 0; i < band.Length; i++)
            line.Append(cell(band[i], i).PadRight(width + Gap));
        output.WriteLine(line.ToString().TrimEnd());
    }

    // BYOND's dir numbers: NORTH 1, SOUTH 2, EAST 4, WEST 8, and their sums for the diagonals.
    private static string Arrow(float dir) => (int)dir switch
    {
        1 => "↑",
        2 => "↓",
        4 => "→",
        8 => "←",
        5 => "↗",
        9 => "↖",
        6 => "↘",
        10 => "↙",
        _ => "?",
    };

    private static string DirName(float dir) => (int)dir switch
    {
        1 => "north",
        2 => "south",
        4 => "east",
        8 => "west",
        5 => "northeast",
        9 => "northwest",
        6 => "southeast",
        10 => "southwest",
        _ => Dm.str(dir),
    };
}
