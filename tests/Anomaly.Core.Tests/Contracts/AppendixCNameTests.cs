using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// Every name of docs/ai/translation-rules.md Appendix C that the shim, the Kernel and the Tg infrastructure provide
/// exists with its DM spelling (reflection reports <c>@new</c> as "new"). The lists mirror Appendix C; a name added
/// there is added here. The other direction is checked too: every public type and member of the Kernel and the shim,
/// and of the Tg infrastructure, is named in Appendix C (<see cref="AppendixCNamesEveryPublicNameOfTheKernelTheShimAndTheTgInfrastructure"/>).
/// </summary>
public class AppendixCNameTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    // C.1, class Dm: BYOND builtins written bare.
    public static readonly string[] BareDmNames =
    {
        "TRUE", "FALSE", "NORTH", "SOUTH", "EAST", "WEST", "NORTHEAST", "NORTHWEST", "SOUTHEAST", "SOUTHWEST", "UP", "DOWN",
        "world",
        "sleep", "spawn", "input", "alert",
        "prob", "rand", "pick", "roll",
        "round", "floor", "ceil", "trunc", "fract", "sign", "abs", "sqrt", "min", "max", "clamp", "log", "lerp",
        "sin", "cos", "tan", "arcsin", "arccos", "arctan",
        "length", "length_char", "copytext", "findtext", "findtextEx", "findlasttext", "replacetext", "replacetextEx",
        "splicetext", "spantext", "nonspantext", "uppertext", "lowertext", "trimtext", "splittext", "jointext", "sorttext",
        "sorttextEx", "cmptext", "cmptextEx", "ckey", "ckeyEx", "ascii2text", "text2ascii", "num2text", "text2num", "html_encode", "html_decode",
        "json_encode", "json_decode", "params2list", "list2params", "time2text", "rgb", "rgb2num",
        "copytext_char", "findtext_char", "findtextEx_char", "findlasttext_char", "replacetext_char", "replacetextEx_char",
        "splicetext_char", "spantext_char", "nonspantext_char", "splittext_char", "text2ascii_char",
        "istype", "astype", "ispath", "isnum", "istext", "islist", "isfile", "isicon", "typesof", "text2path", "hascall", "ref",
        "animate", "flick", "sound", "image", "matrix", "icon",
        "turn",
        "rand_seed", "file", "fexists", "fdel", "fcopy", "file2text", "text2file", "flist", // W3.0
    };

    // C.2, members of Dm written qualified.
    public static readonly string[] QualifiedDmNames =
    {
        "num", "num_or_null", "truthy", "not", "eq", "equiv", "lt", "le", "gt", "ge", "cmp",
        "add", "sub", "mul", "div", "neg", "mod", "fmod", "pow", "inc", "dec", "postinc", "preinc", "postdec", "predec",
        "append", "remove", "combine", "mask", "bitxor_assign", "append_at", "remove_at", "combine_at", "mask_at",
        "bitxor_assign_at", "inc_at", "dec_at", "postinc_at", "preinc_at", "postdec_at", "predec_at",
        "bitand", "bitor", "bitxor", "bitnot", "shl", "shr",
        "in", "in_to", "each", "each_cast", "each_as", "pairs",
        "list", "alist", "kv", "list_n", "pick_weighted",
        "str", "text", "textchar",
        "new", "new_async", "named", "arglist", "Call", "arg", "forward", "args", "initial", "get_var", "set_var",
        "get_var_or_null", "locate", "locate_ref", "parent_type",
        "CRASH", "EXCEPTION", "exception", "throw_value", "NotTranslated", "Deferred",
        "output_to",
    };

    public static readonly string[] ShimTypeNames =
    {
        "DmList", "DmAs", "DmTypes", "DmArgs", "I18n", "IDmObject", "IDmAtom",
        "DmPathAttribute", "DmParamsAttribute", "ShouldCallParentAttribute", "ShouldNotSleepAttribute", "SignalHandlerAttribute",
        "ShouldNotOverrideAttribute", "ShouldBePureAttribute", "DmProtectedAttribute", "DmPrivateAttribute", "DmFinalAttribute",
        "NotHashedAttribute", "DmGlobalProcsAttribute", "DmTypeOrderAttribute",
        "IMessageSink", "ISoundSink", "IAppearanceSink", "AppearanceAspect", "PresentationSinks", "NullSinks", "RecordingSinks",
        "DmWorldState", "DmWorld", "DmWorldLog", "sound", "matrix", "exception",
        "DmRuntimeException", "DmCrashException", "DmThrowException", "DmNotTranslatedException",
        "DmEach`1", "DmPairs`1", "DmKeyValue", "DmNamedArg", "DmArgList",
        "IDmFileSystem", "DmMemoryFiles", "DmDirectoryFiles", "DmFileRef", // W3.0 file seam
        "image", // W4.0, decision D3
    };

    [Theory]
    [MemberData(nameof(Bare))]
    public void EveryBareBuiltinIsAPublicStaticMemberOfDm(string name)
        => Assert.NotEmpty(typeof(Dm).GetMember(name, PublicStatic));

    [Theory]
    [MemberData(nameof(Qualified))]
    public void EveryQualifiedHelperIsAPublicStaticMemberOfDm(string name)
        => Assert.NotEmpty(typeof(Dm).GetMember(name, PublicStatic));

    [Fact]
    public void OutputIsLeftForByondsOutputBuiltin()
    {
        // Rules 12: A << B is Dm.output_to(A, B); the name output belongs to BYOND's output(msg, control), which the UI
        // seam adds later (Appendix C, planned).
        Assert.Empty(typeof(Dm).GetMember("output", PublicStatic));
        Assert.NotEmpty(typeof(Dm).GetMember("output_to", PublicStatic));
    }

    [Theory]
    [MemberData(nameof(ShimTypes))]
    public void EveryShimTypeExists(string name)
        => Assert.NotNull(typeof(Dm).Assembly.GetType("Anomaly.Core.DmShim." + name));

    public static TheoryData<string> Bare() => new(BareDmNames);

    public static TheoryData<string> Qualified() => new(QualifiedDmNames);

    public static TheoryData<string> ShimTypes() => new(ShimTypeNames);

    [Theory]
    [InlineData("append")]
    [InlineData("remove")]
    [InlineData("combine")]
    [InlineData("mask")]
    [InlineData("bitxor_assign")]
    public void CompoundAssignmentsHaveARefOverloadAndAnEngineListOverload(string name)
    {
        MethodInfo[] overloads = typeof(Dm).GetMethods(PublicStatic).Where(m => m.Name == name).ToArray();
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(DmList).MakeByRefType());
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(object).MakeByRefType());
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(float?).MakeByRefType());
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(DmList)); // A.overlays += x
    }

    [Theory]
    [InlineData("inc")]
    [InlineData("dec")]
    [InlineData("postinc")]
    [InlineData("preinc")]
    [InlineData("postdec")]
    [InlineData("predec")]
    public void IncrementsTakeTheirTargetByRef(string name)
    {
        MethodInfo[] overloads = typeof(Dm).GetMethods(PublicStatic).Where(m => m.Name == name).ToArray();
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(object).MakeByRefType());
        Assert.Contains(overloads, m => m.GetParameters()[0].ParameterType == typeof(float?).MakeByRefType());
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("Remove")]
    [InlineData("RemoveAll")]
    [InlineData("Cut")]
    [InlineData("Copy")]
    [InlineData("Find")]
    [InlineData("Insert")]
    [InlineData("Join")]
    [InlineData("Swap")]
    [InlineData("Splice")]
    [InlineData("len")]
    public void DmListHasTheDmListProcs(string name) => Assert.NotEmpty(typeof(DmList).GetMember(name, PublicInstance));

    [Fact]
    public void DmListIndexersTakeANumberOrAnyValue()
    {
        PropertyInfo[] indexers = typeof(DmList).GetProperties(PublicInstance).Where(p => p.GetIndexParameters().Length == 1).ToArray();
        Assert.Contains(indexers, p => p.GetIndexParameters()[0].ParameterType == typeof(float));
        Assert.Contains(indexers, p => p.GetIndexParameters()[0].ParameterType == typeof(object));
    }

    [Fact]
    public void VariadicBuiltinsAndCallsTakeParamsArrays()
    {
        foreach (string name in new[] { "list", "alist", "pick", "Call", "args", "forward", "text", "animate", "typesof", "new" })
        {
            Assert.Contains(typeof(Dm).GetMethods(PublicStatic).Where(m => m.Name == name),
                m => m.GetParameters().LastOrDefault()?.IsDefined(typeof(ParamArrayAttribute)) == true);
        }
        Assert.True(typeof(I18n).GetMethod("T").GetParameters()[1].IsDefined(typeof(ParamArrayAttribute)));
    }

    private static MethodInfo DmMethod(string name, params Type[] parameters)
        => typeof(Dm).GetMethod(name, PublicStatic, parameters)
           ?? throw new Xunit.Sdk.XunitException($"Dm.{name}({string.Join(", ", parameters.Select(p => p.Name))}) is missing");

    [Fact]
    public void ValueTypesFollowTheRules()
    {
        // Rules 3.1: TRUE and FALSE are DM numbers, never a C# bool or int.
        Assert.Equal(typeof(float), typeof(Dm).GetField("TRUE", PublicStatic).FieldType);
        Assert.Equal(typeof(float), typeof(Dm).GetField("FALSE", PublicStatic).FieldType);

        // Rules 3.5: num to float, num_or_null to float?; 4.1: truthy is a C# condition, not a DM number.
        Assert.Equal(typeof(float), DmMethod("num", typeof(object)).ReturnType);
        Assert.Equal(typeof(float), DmMethod("num", typeof(float?)).ReturnType);
        Assert.Equal(typeof(float), DmMethod("num", typeof(bool)).ReturnType);
        Assert.Equal(typeof(float?), DmMethod("num_or_null", typeof(object)).ReturnType);
        foreach (Type t in new[] { typeof(float), typeof(float?), typeof(string), typeof(object), typeof(DmList), typeof(Type) })
            Assert.Equal(typeof(bool), DmMethod("truthy", t).ReturnType);
        foreach (Type t in new[] { typeof(float), typeof(float?), typeof(string), typeof(object) })
            Assert.Equal(typeof(float), DmMethod("not", t).ReturnType);

        // Rules 4.2: the eq overloads for the call shapes of the rules, all C# conditions.
        foreach ((Type a, Type b) in new[]
        {
            (typeof(object), typeof(object)), (typeof(float), typeof(float)), (typeof(object), typeof(float)), (typeof(float), typeof(object)),
            (typeof(float?), typeof(float?)), (typeof(float?), typeof(float)), (typeof(float), typeof(float?)),
        })
            Assert.Equal(typeof(bool), DmMethod("eq", a, b).ReturnType);

        // Rules 4.1: the boolean builtins stay C# conditions.
        Assert.Equal(typeof(bool), DmMethod("prob", typeof(float)).ReturnType);
        Assert.Equal(typeof(bool), DmMethod("istype", typeof(object), typeof(Type)).ReturnType);
        Assert.Equal(typeof(bool), DmMethod("ispath", typeof(object)).ReturnType);
        Assert.Equal(typeof(bool), DmMethod("hascall", typeof(object), typeof(string)).ReturnType);
        foreach (string name in new[] { "isnum", "istext", "islist", "isfile", "isicon" })
            Assert.Equal(typeof(bool), DmMethod(name, typeof(object)).ReturnType);

        // Rules 4.6: numeric builtins take and return float; min, max and clamp also have the object form.
        foreach (string name in new[] { "round", "floor", "ceil", "trunc", "fract", "sign", "abs", "sqrt", "log", "sin", "cos", "tan", "arcsin", "arccos", "arctan" })
            Assert.Equal(typeof(float), DmMethod(name, typeof(float)).ReturnType);
        Assert.Equal(typeof(float), DmMethod("min", typeof(float[])).ReturnType);
        Assert.Equal(typeof(object), DmMethod("min", typeof(object[])).ReturnType);
        Assert.Equal(typeof(float), DmMethod("max", typeof(float[])).ReturnType);
        Assert.Equal(typeof(object), DmMethod("max", typeof(object[])).ReturnType);
        Assert.Equal(typeof(float), DmMethod("clamp", typeof(float), typeof(float), typeof(float)).ReturnType);
        Assert.Equal(typeof(object), DmMethod("clamp", typeof(object), typeof(object), typeof(object)).ReturnType);
        Assert.Equal(typeof(float), DmMethod("length", typeof(object)).ReturnType);
        Assert.Equal(typeof(float), DmMethod("findtext", typeof(object), typeof(object), typeof(float), typeof(float)).ReturnType);
        Assert.Equal(typeof(string), DmMethod("copytext", typeof(object), typeof(float), typeof(float)).ReturnType);
        // Rules 3.5 (v1.2): a builtin's text argument takes the object as is; uppertext and lowertext keep text typed.
        Assert.Equal(typeof(float), DmMethod("sorttext", typeof(object[])).ReturnType);
        Assert.Equal(typeof(string), DmMethod("uppertext", typeof(string)).ReturnType);
        Assert.Equal(typeof(object), DmMethod("uppertext", typeof(object)).ReturnType);
        Assert.Equal(typeof(float?), DmMethod("text2num", typeof(object)).ReturnType); // rules 5.5: null when not a number

        // Rules 6.2: L.len is a DM number; 7.7: a call by name is always a DmTask<object>.
        Assert.Equal(typeof(float), typeof(DmList).GetProperty("len").PropertyType);
        Assert.Equal(typeof(DmTask<object>), DmMethod("Call", typeof(object), typeof(string), typeof(object[])).ReturnType);
    }

    [Fact]
    public void Wave3FacadeAndWorldStateNamesExist()
    {
        // C.1, the world facade (W3.0): map_cpu and the /world vars of code/world.dm.
        foreach (string name in new[] { "map_cpu", "turf", "area", "mob", "name", "view", "params", "system_type", "sleep_offline" })
            Assert.NotNull(typeof(DmWorld).GetProperty(name, PublicInstance));
        // C.2, DmWorldState (W3.0): their storage, the map grid hook, the file seam and the Deferred counters.
        foreach (string name in new[] { "turf", "area", "mob", "name", "view", "params", "system_type", "sleep_offline", "MapResized", "Files", "DeferredCount", "DeferredSites" })
            Assert.NotEmpty(typeof(DmWorldState).GetMember(name, PublicInstance));
        foreach (string name in new[] { "Exists", "ReadText", "WriteText", "AppendText", "Delete", "Copy", "List" })
            Assert.NotNull(typeof(IDmFileSystem).GetMethod(name));
        Assert.NotNull(typeof(DmMemoryFiles).GetProperty("Paths"));
        Assert.NotNull(typeof(DmDirectoryFiles).GetProperty("Root"));
        Assert.NotNull(typeof(DmFileRef).GetProperty("Path"));
    }

    [Fact]
    public void KernelNamesExist()
    {
        foreach (string name in new[] { "Current", "ReportRuntime", "AllocateId", "usr", "Time", "Step", "Objects", "GetExtension" })
            Assert.NotEmpty(typeof(World).GetMember(name, PublicStatic | PublicInstance));
        // The host and test API row of Appendix C.2.
        foreach (string name in new[]
        {
            "CurrentOrNull", "Enter", "Tick", "TickLag", "Fps", "Seed", "StepDeciseconds", "AddPhase", "RuntimeErrorSink",
            "RuntimeErrorCount", "ComputeStateHash", "AddStateHasher", "Rng", "Scheduler",
        })
            Assert.NotEmpty(typeof(World).GetMember(name, PublicStatic | PublicInstance));
        Assert.NotNull(typeof(World.Scope));
        foreach (string name in new[] { "At", "After", "RunDue", "TicksFromDeciseconds", "TicksCovering", "MaxZeroDelayHops", "MaxRunsPerDrain", "Pending", "Sequence" })
            Assert.NotEmpty(typeof(Scheduler).GetMember(name, PublicStatic | PublicInstance));
        foreach (string name in new[] { "NextFloat", "NextInt", "NextUInt64", "Reseed", "State", "Mix64" })
            Assert.NotEmpty(typeof(Rng).GetMember(name, PublicStatic | PublicInstance));
        foreach (string name in new[] { "Create", "Add", "Value" })
            Assert.NotEmpty(typeof(StateHasher).GetMember(name, PublicStatic | PublicInstance));
        Assert.True(typeof(StateHashContributor).IsSubclassOf(typeof(Delegate)));
        Assert.True(typeof(WaitingException).IsSubclassOf(typeof(Exception)));
        foreach (string name in new[] { "Detach", "DetachSync", "NoSleep", "WaitforFalse", "Spawn" })
            Assert.NotEmpty(typeof(Waiting).GetMember(name, PublicStatic));
        foreach (string name in new[] { "Register", "Unregister", "TryGet", "HoldStrongly", "IsHeldStrongly", "ForEachLiveInIdOrder", "TryGetNextLive", "Count", "HashInto" })
            Assert.NotEmpty(typeof(ObjectRegistry).GetMember(name, PublicInstance));
        Assert.NotEmpty(typeof(Sleeping).GetMember("Sleep", PublicStatic));
        Assert.NotEmpty(typeof(Sleeping).GetMember("SleepTicks", PublicStatic));
        Assert.NotNull(typeof(DmTask).GetProperty("IsCompleted"));
        Assert.NotNull(typeof(DmTask).GetProperty("IsFaulted"));
        Assert.NotNull(typeof(DmTask<object>).GetMethod("TryGetResult"));
        foreach (string name in new[] { "CompletedTask", "FromException", "Exception", "AsObjectTask" })
            Assert.NotEmpty(typeof(DmTask).GetMember(name, PublicStatic | PublicInstance));
        foreach (string name in new[] { "FromResult", "FromException", "Exception", "AsObjectTask" })
            Assert.NotEmpty(typeof(DmTask<object>).GetMember(name, PublicStatic | PublicInstance));
        Assert.True(typeof(IDmTask).IsInterface);
        Assert.Equal(typeof(SleepAwaitable), DmMethod("sleep", typeof(float)).ReturnType);
        foreach (Type t in new[] { typeof(DmTaskBuilder), typeof(DmTaskBuilder<>), typeof(DmTaskAwaiter), typeof(DmTaskAwaiter<>), typeof(SleepAwaiter) })
            Assert.True(t.IsPublic, t.Name);
        Assert.NotNull(typeof(DotBox<object>));
        Assert.True(typeof(IDmAwaiter).IsInterface);
        Assert.True(typeof(IStateHashable).IsInterface);
    }

    [Fact]
    public void TheTypeRegistryAndGeneratedOrderNamesExist()
    {
        // Appendix C.2: the type registry, the pre-New hook's arguments and the generated order (object-model.md 9).
        foreach (string name in new[] { "PathOf", "FromPath", "ParentOf", "Subtypes", "TreeOrdinal", "IsSubtypeOrSame" })
            Assert.NotEmpty(typeof(DmTypes).GetMember(name, PublicStatic));
        foreach (string name in new[] { "Positional", "Named", "TryGetNamed", "Empty", "InitialLocation" })
            Assert.NotEmpty(typeof(DmArgs).GetMember(name, PublicStatic | PublicInstance));
        Assert.NotNull(typeof(TypeOrder).GetField("Paths", PublicStatic));
        Assert.True(typeof(TypeOrder).GetField("Count", PublicStatic).IsLiteral);
        Assert.NotNull(typeof(ProcOrder).GetField("Procs", PublicStatic));
        Assert.NotNull(typeof(ProcOrder).GetField("Verbs", PublicStatic));
        Assert.NotNull(typeof(ProcOrder).GetMethod("ProcsOf", PublicStatic));
    }

    // Names that only the C# compiler and runtime call: the foreach, await and async-builder patterns, delegate and
    // record members, indexers, enum internals. They are not DM or port names and are not listed in Appendix C.
    private static readonly HashSet<string> MachineryNames = new(StringComparer.Ordinal)
    {
        "GetEnumerator", "Enumerator", "Current", "MoveNext", "Reset", "Dispose", "GetAwaiter", "GetResult", "OnCompleted",
        "UnsafeOnCompleted", "Start", "SetResult", "SetException", "SetStateMachine", "AwaitOnCompleted",
        "AwaitUnsafeOnCompleted", "Task", "Invoke", "BeginInvoke", "EndInvoke", "Item", "value__", "Equals",
        "GetHashCode", "ToString", "Deconstruct", "EqualityContract", "<Clone>$",
    };

    private static HashSet<string> AppendixCWords()
    {
        string rules = File.ReadAllText(Path.Combine(Anomaly.Core.Tests.Tools.TgClone.RepositoryRoot, "docs", "ai", "translation-rules.md"));
        int start = rules.IndexOf("## Appendix C.", StringComparison.Ordinal);
        Assert.True(start >= 0, "translation-rules.md has no Appendix C");
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match code in Regex.Matches(rules.Substring(start), "`([^`]+)`"))
        {
            foreach (Match word in Regex.Matches(code.Groups[1].Value, "[A-Za-z_][A-Za-z0-9_]*"))
                words.Add(word.Value);
        }
        return words;
    }

    private static string SimpleName(Type t)
    {
        string name = t.Name;
        int tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
    }

    private static IEnumerable<string> PublicMemberNames(Type t)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (MemberInfo m in t.GetMembers(flags))
        {
            if (m is ConstructorInfo) continue;
            if (m is MethodInfo method && method.IsSpecialName) continue;              // property accessors, operators
            if (m is FieldInfo field && field.IsLiteral && t == typeof(Dm)) continue; // the stddef constants row
            if (m is Type) continue;                                                  // nested types: checked as types
            yield return m.Name;
        }
    }

    [Fact]
    public void AppendixCNamesEveryPublicNameOfTheKernelTheShimAndTheTgInfrastructure()
    {
        // Rules 0.4: Appendix C lists every shim, Kernel and Tg-infrastructure name. Every public type of the Kernel and
        // the shim (an attribute by its name in brackets, [DmPath]) and every public member they declare must occur in
        // a code span of Appendix C. Dm's constants are covered by the row of BYOND's stddef constants; GlobalProcs'
        // members by the rows of tg's global procs and the generated span_* helpers.
        HashSet<string> words = AppendixCWords();
        var types = typeof(World).Assembly.GetExportedTypes().Concat(typeof(Dm).Assembly.GetExportedTypes())
            .Concat(new[] { typeof(TgState), typeof(TypeOrder), typeof(ProcOrder), typeof(GlobalProcs), typeof(Builtins), typeof(Defines) })
            .ToList();
        var missing = new List<string>();
        foreach (Type t in types)
        {
            string name = SimpleName(t);
            string bare = name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
            if (!MachineryNames.Contains(name) && !words.Contains(name) && !words.Contains(bare))
                missing.Add(t.FullName);
            if (t == typeof(GlobalProcs) || t == typeof(Defines)) continue;
            foreach (string member in PublicMemberNames(t).Distinct())
            {
                if (!MachineryNames.Contains(member) && !words.Contains(member))
                    missing.Add(t.FullName + "." + member);
            }
        }
        Assert.True(missing.Count == 0, "public names missing from translation-rules.md Appendix C: " + string.Join(", ", missing));
    }

    [Fact]
    public void TgInfrastructureNamesExist()
    {
        Assert.NotNull(typeof(TgState).GetProperty("Current", PublicStatic));
        Assert.NotNull(typeof(TgState).GetMethod("Of", PublicStatic, new[] { typeof(World) }));
        Assert.NotNull(typeof(TgState).GetMethod("InitStatics", PublicInstance));
        Assert.Equal(typeof(datum_controller_global_vars), typeof(GlobalProcs).GetProperty("GLOB", PublicStatic).PropertyType);
        Assert.NotNull(typeof(GlobalProcs).GetProperty("GLOB__var", PublicStatic).SetMethod);
        Assert.True(typeof(GlobalProcs).GetProperty("Master", PublicStatic).PropertyType.IsByRef);
        Assert.Equal(typeof(mob), typeof(GlobalProcs).GetProperty("usr", PublicStatic).PropertyType);
        Assert.True(typeof(Builtins).IsAbstract && typeof(Builtins).IsSealed); // a static class
        Assert.True(typeof(Defines).IsAbstract && typeof(Defines).IsSealed);
        Assert.Equal(typeof(GlobalProcs), typeof(GlobalProcs).Assembly.GetCustomAttribute<DmGlobalProcsAttribute>().Type);
    }
}
