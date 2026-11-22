using System.IO;

namespace Anomaly.Tools.Sounds;

/// <summary>The tool's options: <c>--tg &lt;path to tgstation&gt; --out &lt;dir&gt; [--skip-nc]</c>.</summary>
/// <param name="TgRoot">The tgstation clone (its <c>sound/</c> is read; never written).</param>
/// <param name="OutputDirectory">Where the sounds go (normally <c>godot/assets/tg</c>, git-ignored).</param>
/// <param name="SkipNonCommercial">True: files under a non-commercial licence are not copied and the index marks them absent.</param>
public sealed record SoundToolOptions(string TgRoot, string OutputDirectory, bool SkipNonCommercial = false)
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        "Usage: Anomaly.Tools.Sounds --tg <path to tgstation> --out <dir> [--skip-nc]\n"
        + "  --tg PATH   the tgstation clone whose sound/**/*.ogg and *.mp3 are copied (read only)\n"
        + "  --out DIR   the output folder (godot/assets/tg): sound/<path>.ogg, sound/index.json, sound-nc/ for non-commercial files, tts/ (the TTS server's .wav sounds)\n"
        + "  --skip-nc   do not copy the files whose attribution names a non-commercial licence (the index marks them absent)";

    /// <summary>Parses the command line (<c>--key value</c> or <c>--key=value</c>); false with <paramref name="error"/> (null for <c>--help</c>).</summary>
    public static bool TryParse(IReadOnlyList<string> args, out SoundToolOptions options, out string error)
    {
        options = null;
        error = null;
        string tg = null, output = null;
        bool skipNc = false;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg == "-h" || arg == "--help")
                return false;
            if (arg == "--skip-nc")
            {
                skipNc = true;
                continue;
            }
            string name = arg, value = null;
            int eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 2)
            {
                name = arg.Substring(0, eq);
                value = arg.Substring(eq + 1);
            }
            if (name != "--tg" && name != "--out")
            {
                error = "unknown argument '" + arg + "'";
                return false;
            }
            if (value == null)
            {
                if (i + 1 >= args.Count)
                {
                    error = name + " needs a value";
                    return false;
                }
                value = args[++i];
            }
            if (name == "--tg") tg = value;
            else output = value;
        }
        if (string.IsNullOrEmpty(tg) || string.IsNullOrEmpty(output))
        {
            error = "both --tg and --out are required";
            return false;
        }
        options = new SoundToolOptions(tg, output, skipNc);
        return true;
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (!SoundToolOptions.TryParse(args, out SoundToolOptions options, out string error))
        {
            if (error != null) Console.Error.WriteLine("Anomaly.Tools.Sounds: " + error);
            Console.Error.WriteLine(SoundToolOptions.Usage);
            return error == null ? 0 : 2;
        }
        return SoundCopier.Run(options, Console.Out, Console.Error);
    }
}
