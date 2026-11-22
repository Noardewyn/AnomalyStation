using System.IO;

namespace Anomaly.Tools.Icons;

// Owner: M2 P7 (the conversion); the command line and IconToolOptions are the M2 contracts step's (frozen).

/// <summary>The tool's options: <c>--tg &lt;path to tgstation&gt; --out &lt;dir&gt;</c>, or <c>--masks &lt;file&gt;</c> for the body-part mask table (M7x slice A).</summary>
/// <param name="TgRoot">The tgstation clone (its <c>icons/</c> and <c>code/datums/greyscale/json_configs/</c> are read; never written).</param>
/// <param name="OutputDirectory">Where the atlases go (normally <c>godot/assets/tg</c>, git-ignored until D88's infrastructure pass).</param>
/// <param name="MasksFile">M7x slice A: where the body-part mask table goes (<c>src/Core/Tg/Generated/BodyMasks.bin</c>, committed); null to convert the icons.</param>
public sealed record IconToolOptions(string TgRoot, string OutputDirectory, string MasksFile = null, string StatesFile = null)
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        "Usage: Anomaly.Tools.Icons --tg <path to tgstation> --out <dir> | --masks <file> | --states <file>\n"
        + "  --tg PATH   the tgstation clone whose icons/ (.dmi) and code/datums/greyscale/json_configs/ are converted (read only)\n"
        + "  --out DIR   the output folder (godot/assets/tg): <icon path>.png and <icon path>.json per .dmi, gags/<config>.json\n"
        + "  --masks FILE  instead: the body-part mask table (src/Core/Tg/Generated/BodyMasks.bin, M7x slice A)\n"
        + "  --states FILE instead: the icon state table (src/Core/Tg/Generated/IconStates.bin, silicons SI4)";

    /// <summary>Parses the command line (<c>--key value</c> or <c>--key=value</c>); false with <paramref name="error"/> (null for <c>--help</c>).</summary>
    public static bool TryParse(IReadOnlyList<string> args, out IconToolOptions options, out string error)
    {
        options = null;
        error = null;
        string tg = null, output = null, masks = null, states = null;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg == "-h" || arg == "--help")
                return false;
            string name = arg, value = null;
            int eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 2)
            {
                name = arg.Substring(0, eq);
                value = arg.Substring(eq + 1);
            }
            if (name != "--tg" && name != "--out" && name != "--masks" && name != "--states")
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
            else if (name == "--masks") masks = value;
            else if (name == "--states") states = value;
            else output = value;
        }
        int modes = (string.IsNullOrEmpty(output) ? 0 : 1) + (string.IsNullOrEmpty(masks) ? 0 : 1) + (string.IsNullOrEmpty(states) ? 0 : 1);
        if (string.IsNullOrEmpty(tg) || modes != 1)
        {
            error = "--tg and one of --out, --masks and --states are required";
            return false;
        }
        options = new IconToolOptions(tg, output, masks, states);
        return true;
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        if (!IconToolOptions.TryParse(args, out IconToolOptions options, out string error))
        {
            if (error != null) Console.Error.WriteLine("Anomaly.Tools.Icons: " + error);
            Console.Error.WriteLine(IconToolOptions.Usage);
            return error == null ? 0 : 2;
        }
        if (options.MasksFile != null)
            return BodyMaskTable.Run(options.TgRoot, options.MasksFile, Console.Out, Console.Error);
        if (options.StatesFile != null)
            return IconStateTable.Run(options.TgRoot, options.StatesFile, Console.Out, Console.Error);
        return IconConverter.Run(options, Console.Out, Console.Error);
    }
}
