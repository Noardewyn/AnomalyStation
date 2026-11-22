using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// docs/ai/translation-rules.md 1.6 (wave-0 finding K6): the classes translated code imports with <c>using static</c>
/// (<c>Dm</c>, <c>Builtins</c>) and the types it imports with <c>using</c> (Anomaly.Core.DmShim, Anomaly.Core.Kernel) never
/// define a name that tg defines as a <c>#define</c>, a global proc or a global that becomes a <c>GlobalProcs</c>
/// accessor (<c>GLOBAL_REAL</c>, <c>SUBSYSTEM_DEF</c>), because two imported members with one name make every use
/// ambiguous (CS0229, CS0121). The tg names are read from the reference clone next to the repository.
/// </summary>
public class DefineClashTests
{
    // Documented exceptions. Add "Name" here only with a comment saying why the clash is harmless.
    private static readonly HashSet<string> AllowList = new(StringComparer.Ordinal)
    {
        // BYOND's file builtins (the W3.0 file seam, Dm.Files.cs). tg #defines them only under RUSTG_OVERRIDE_BUILTINS
        // (__DEFINES/rust_g.dm:207-209), which is commented out (:10) and inactive in the production build (rules 1.7),
        // so no translated code ever sees tg's macro: the names are BYOND's.
        "file2text",
        "text2file",
        // BYOND's md5 builtin (Dm.Text.cs, M6 slice 1: the blood DNA's hash): tg #defines md5 only under the same inactive
        // RUSTG_OVERRIDE_BUILTINS (__DEFINES/rust_g.dm:297), as translation-rules.md Appendix C's md5 row says.
        "md5",
        // BYOND's builtin value type /mutable_appearance (src/Core/DmShim/mutable_appearance.cs, M2 contracts) and tg's
        // global proc mutable_appearance() (code/datums/mutable_appearance.dm:14): C# binds the bare name to the method
        // where it is invoked and to the type where a type is expected (a declaration, typeof, is, a type argument), as
        // with sound and Dm.sound; a scratch compile of both forms side by side was clean (M2 contracts).
        "mutable_appearance",
    };

    // Defines: every #define. Globals: every name GlobalProcs gets from tg, the global procs and the accessors of
    // GLOBAL_REAL, GLOBAL_REAL_VAR and the SUBSYSTEM_DEF family (rules 10.6: GLOB, Master, SStimer, ...).
    private static readonly Lazy<(HashSet<string> Defines, HashSet<string> Globals)> TgNames = new(ReadTgNames);

    private static (HashSet<string>, HashSet<string>) ReadTgNames()
    {
        // Every file of the tg build: tgstation.dme itself (its own #defines such as DEBUG, rules 1.7), the root .dm
        // files, and every .dm under code/ (a superset of code/__DEFINES, rules 1.6), interface/ and _maps/.
        var files = new List<string> { Path.Combine(TgSource.Root, "tgstation.dme") };
        files.AddRange(Directory.EnumerateFiles(TgSource.Root, "*.dm", SearchOption.TopDirectoryOnly));
        foreach (string dir in new[] { "code", "interface", "_maps" })
        {
            string path = Path.Combine(TgSource.Root, dir);
            if (Directory.Exists(path)) files.AddRange(Directory.EnumerateFiles(path, "*.dm", SearchOption.AllDirectories));
        }
        // A #define at the start of a line (comments such as "// #define NORTH 1" in __DEFINES/directional.dm are not
        // defines; BYOND provides those names).
        var define = new Regex(@"^\s*#\s*define\s+(\w+)", RegexOptions.Multiline);
        var globalProc = new Regex(@"^/proc/(\w+)", RegexOptions.Multiline);
        var globalReal = new Regex(@"^\s*GLOBAL_REAL(?:_VAR)?\((\w+)\s*[,)]", RegexOptions.Multiline);
        var subsystem = new Regex(@"^\s*\w*SUBSYSTEM_DEF\((\w+)\)", RegexOptions.Multiline);
        var defines = new HashSet<string>(StringComparer.Ordinal);
        var globals = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (Match m in define.Matches(text)) defines.Add(m.Groups[1].Value);
            foreach (Match m in globalProc.Matches(text)) globals.Add(m.Groups[1].Value);
            foreach (Match m in globalReal.Matches(text)) globals.Add(m.Groups[1].Value);
            foreach (Match m in subsystem.Matches(text)) globals.Add("SS" + m.Groups[1].Value);
        }
        return (defines, globals);
    }

    private static IEnumerable<string> ImportedStaticMemberNames(Type type)
        => type.GetMembers(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name).Distinct();

    private static IEnumerable<Type> ImportedTypes()
        => typeof(Dm).Assembly.GetExportedTypes().Concat(typeof(World).Assembly.GetExportedTypes()).Where(t => !t.IsNested);

    private static string SimpleName(Type t) => t.IsGenericType ? t.Name[..t.Name.IndexOf('`')] : t.Name;

    [Fact]
    public void TheTgScanFindsKnownNames()
    {
        var (defines, globals) = TgNames.Value;
        Assert.Contains("INFINITY", defines);      // __DEFINES/maths.dm
        Assert.Contains("in_range", defines);      // __DEFINES/is_helpers.dm:3
        Assert.Contains("url_encode", defines);    // __DEFINES/rust_g_overrides.dm:2
        Assert.Contains("DEBUG", defines);         // tgstation.dme:17
        Assert.DoesNotContain("NORTH", defines);   // only commented out in __DEFINES/directional.dm
        Assert.Contains("qdel", globals);          // controllers/subsystem/garbage.dm
        Assert.Contains("get", globals);           // __HELPERS/atoms.dm:312
        Assert.Contains("GLOB", globals);          // controllers/globals.dm:2, GLOBAL_REAL
        Assert.Contains("PROFILE_TIME", globals);  // datums/profiling.dm:6, GLOBAL_REAL_VAR
        Assert.Contains("SStimer", globals);       // controllers/subsystem/timer.dm, SUBSYSTEM_DEF(timer)
        Assert.Contains("SSmobs", globals);        // controllers/subsystem/mobs.dm, SUBSYSTEM_DEF(mobs)
        Assert.True(defines.Count > 10000, "expected the whole tg define set, got " + defines.Count);
    }

    [Theory]
    [InlineData(typeof(Dm))]
    [InlineData(typeof(Builtins))]
    public void NoUsingStaticClassDefinesATgName(Type imported)
    {
        var (defines, globals) = TgNames.Value;
        string[] clashes = ImportedStaticMemberNames(imported)
            .Where(n => (defines.Contains(n) || globals.Contains(n)) && !AllowList.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.True(clashes.Length == 0, imported.Name + " defines tg names: " + string.Join(", ", clashes));
    }

    [Fact]
    public void NoImportedShimOrKernelTypeHasATgName()
    {
        var (defines, globals) = TgNames.Value;
        string[] clashes = ImportedTypes().Select(SimpleName)
            .Where(n => (defines.Contains(n) || globals.Contains(n)) && !AllowList.Contains(n))
            .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(clashes.Length == 0, "types with tg names: " + string.Join(", ", clashes));
    }

    [Fact]
    public void DmAndGlobalProcsShareNoName()
    {
        string[] shared = ImportedStaticMemberNames(typeof(Dm)).Intersect(ImportedStaticMemberNames(typeof(GlobalProcs))).ToArray();
        Assert.True(shared.Length == 0, "Dm and GlobalProcs both define: " + string.Join(", ", shared));
    }

    [Fact]
    public void NoDmFieldOrPropertyIsNamedLikeAnImportedType()
    {
        // A type and a non-method member with one name are ambiguous for the bare name (CS0229): this is why the world
        // facade's class is DmWorld and not world.
        var typeNames = new HashSet<string>(ImportedTypes().Concat(typeof(TgState).Assembly.GetExportedTypes()).Select(SimpleName), StringComparer.Ordinal);
        string[] clashes = typeof(Dm).GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m is FieldInfo or PropertyInfo)
            .Select(m => m.Name)
            .Where(typeNames.Contains)
            .ToArray();
        Assert.True(clashes.Length == 0, "Dm members named like types: " + string.Join(", ", clashes));
    }
}

/// <summary>The tg reference clone (snapshot 1d99bd7f), found next to the repository or through ANOMALY_TGSTATION.</summary>
internal static class TgSource
{
    private static readonly Lazy<string> RootPath = new(Find);

    public static string Root => RootPath.Value ?? throw new InvalidOperationException(
        "The tgstation reference clone was not found: put it next to the repository (../tgstation) or set ANOMALY_TGSTATION.");

    private static string Find()
    {
        string env = Environment.GetEnvironmentVariable("ANOMALY_TGSTATION");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "code"))) return env;
        for (DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "tgstation");
            if (Directory.Exists(Path.Combine(candidate, "code", "__DEFINES"))) return candidate;
        }
        return null;
    }
}
