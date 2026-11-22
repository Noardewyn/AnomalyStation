using System.IO;
using System.Reflection;
using Anomaly.Core.Tg;
using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// The tgstation clone the converter reads (located like the tool does: <c>$ANOMALY_TGSTATION</c>, <c>../tgstation</c>
/// next to the repository or next to the main checkout of a worktree) and one shared conversion of it.
/// </summary>
internal static class TgClone
{
    public static readonly string Root = LocateOrNull();

    public static readonly string RepositoryRoot = TgLocator.FindRepositoryRoot(AppContext.BaseDirectory);

    public static string GeneratedDirectory => Path.Combine(RepositoryRoot, "src", "Core", "Tg", "Generated", "Defines");

    private static readonly Lazy<ConverterOutput> LazyOutput = new(() => DefinesConverter.Generate(Root));

    /// <summary>The conversion of the clone (computed once per test run).</summary>
    public static ConverterOutput Output => LazyOutput.Value;

    public static DefineResult Result(string name)
    {
        DefineResult r = Output.Find(name);
        Assert.True(r != null, $"{name} is not in the conversion");
        return r;
    }

    /// <summary>The value of the generated constant <c>Defines.name</c>, read by reflection.</summary>
    public static object Constant(string name)
    {
        FieldInfo field = typeof(Defines).GetField(name, BindingFlags.Public | BindingFlags.Static);
        Assert.True(field != null, $"Defines.{name} does not exist");
        Assert.True(field.IsLiteral, $"Defines.{name} is not a const");
        return field.GetRawConstantValue();
    }

    public static bool HasConstant(string name) => typeof(Defines).GetField(name, BindingFlags.Public | BindingFlags.Static) != null;

    private static string LocateOrNull()
    {
        try
        {
            return TgLocator.Locate(null, AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        }
        catch (DirectoryNotFoundException)
        {
            return null; // $ANOMALY_TGSTATION points nowhere: the clone-dependent tests are skipped
        }
    }
}

/// <summary>A fact that needs the tgstation clone; skipped when the clone is not found.</summary>
public sealed class TgFactAttribute : FactAttribute
{
    public TgFactAttribute()
    {
        if (TgClone.Root == null)
            Skip = "tgstation clone not found (set ANOMALY_TGSTATION or put it at ../tgstation)";
    }
}

/// <summary>Conversion of a DM snippet, for tests that do not need the clone.</summary>
internal static class Snippet
{
    public static ConverterOutput Convert(string dm) => DefinesConverter.ConvertText(dm.Replace("\r\n", "\n"));

    public static DefineResult Define(string dm, string name)
    {
        DefineResult r = Convert(dm).Find(name);
        Assert.True(r != null, $"{name} not seen in the snippet");
        return r;
    }

    public static object Value(string dm, string name)
    {
        DefineResult r = Define(dm, name);
        Assert.True(r.Emitted, $"{name} not emitted: {r.Kind}: {r.Reason}");
        return r.Value;
    }
}
