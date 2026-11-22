using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Contracts;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// The type registry and the typepath builtins (docs/ai/specs/object-model.md section 9; rules 10.4). Expected orders
/// come from the rule of object-model.md section 9 (depth-first preorder, children in compile order from the
/// generated list; types the list does not name after the listed ones by ordinal path), never from running the port.
/// </summary>
public class TypeRegistryTests
{
    [Fact]
    public void PathsMapToClassesBothWaysForTheShimBuiltinsAndTranslatedClasses()
    {
        // object-model.md section 9: /list -> DmList and the shim's builtin value types; rules 2.3 for the rest.
        Assert.Equal(typeof(DmList), DmTypes.FromPath("/list"));
        Assert.Equal(typeof(sound), DmTypes.FromPath("/sound"));
        Assert.Equal(typeof(exception), DmTypes.FromPath("/exception"));
        Assert.Equal(typeof(matrix), DmTypes.FromPath("/matrix"));
        Assert.Equal(typeof(DmWorld), DmTypes.FromPath("/world"));
        Assert.Equal("/datum/dynfx/atom/movable/item/stack", DmTypes.PathOf(typeof(fx_stack)));
        Assert.Equal(typeof(fx_point), DmTypes.FromPath("/datum/dynfx/point"));
        Assert.Null(DmTypes.PathOf(typeof(TestDatum)));        // no [DmPath]
        Assert.Null(DmTypes.PathOf(null));
        Assert.Null(DmTypes.FromPath("/datum/dynfx/no_such_type"));
        Assert.Null(DmTypes.FromPath("/DATUM/dynfx"));            // paths are case-sensitive
        Assert.Equal(typeof(fx_atom), text2path("/datum/dynfx/atom"));
        Assert.Null(text2path(""));
        Assert.Null(text2path(null));
        Assert.Null(text2path("/datum/dynfx/atom/"));             // BYOND ref text2path: null if there is no such type
    }

    [Fact]
    public void NoTwoClassesClaimOneDmPath()
    {
        // Rules 2.3: [DmPath] exactly once per class, and the mapping is injective after the __2 renames.
        Assert.Empty(DmTypes.Registry.Duplicates);
    }

    [Fact]
    public void EveryTgClassIsListedInTheGeneratedTypeOrder()
    {
        // object-model.md section 9: "a test fails when a [DmPath] type is missing from the list".
        // M3 P1 (GAP(M3 P1 -> lead)): the registry registers the Tg assembly's classes from the generated table
        // (types-converter.md 9.4), so the classes are read here by reflection: each is in the table, under its path.
        Assembly tg = typeof(GlobalProcs).Assembly;
        var listed = new HashSet<string>(DmTypes.Registry.Order.TypePaths, StringComparer.Ordinal);
        var missing = new List<string>();
        var port = new HashSet<Type>(tg.GetCustomAttributes<DmPortTypeAttribute>().Select(a => a.Type)); // the port's own types, unlisted
        foreach (Type t in tg.GetTypes())
        {
            string path = t.GetCustomAttribute<DmPathAttribute>(inherit: false)?.Path;
            if (path == null) continue;
            if (port.Contains(t))
            {
                if (listed.Contains(path) || DmTypes.FromPath(path) != t || DmTypes.PathOf(t) != path) missing.Add(path + " (" + t.Name + ", a port type: unlisted and registered)");
                continue;
            }
            if (!listed.Contains(path) || DmTypes.FromPath(path) != t || DmTypes.PathOf(t) != path) missing.Add(path + " (" + t.Name + ")");
        }
        Assert.True(missing.Count == 0, "Tg classes missing from TypeOrder.g.cs or registered under another path: " + string.Join(", ", missing));
    }

    [Fact]
    public void ParentsFollowCSharpInheritance()
    {
        // Rules 2.3: C# inheritance equals DM path inheritance.
        Assert.Equal(typeof(fx_item), DmTypes.ParentOf(typeof(fx_stack)));
        Assert.Equal(typeof(fx_datum), parent_type(typeof(fx_atom)));
        Assert.Null(DmTypes.ParentOf(typeof(fx_datum)));         // a C# root below no registered type
        Assert.Null(DmTypes.ParentOf(null));
        Assert.Null(parent_type(typeof(TestThing)));             // unregistered, and so is its base
    }

    [Fact]
    public void TypesofListsTheTypeAndItsSubtypesInTreeOrder()
    {
        // BYOND ref /proc/typesof: "all types that are derived from the specified base types, including the base types
        // themselves". The fixtures are not in the generated list, so they sort by ordinal path: a preorder with
        // children by name.
        DmList types = typesof(typeof(fx_atom));
        Assert.Equal(new object[]
        {
            typeof(fx_atom), typeof(fx_movable), typeof(fx_item), typeof(fx_bedsheet), typeof(fx_bedsheet_blue),
            typeof(fx_bedsheet_plain), typeof(fx_stack), typeof(fx_sheet), typeof(fx_renamed_loc),
        }, Items(types));
        Assert.Equal(new[] { typeof(fx_stack), typeof(fx_sheet) }, DmTypes.Subtypes(typeof(fx_stack), includeRoot: true));
        Assert.Equal(new[] { typeof(fx_sheet) }, DmTypes.Subtypes(typeof(fx_stack), includeRoot: false));
        Assert.Empty(DmTypes.Subtypes(typeof(fx_sheet), includeRoot: false));
        Assert.True(DmTypes.TreeOrdinal(typeof(fx_atom)) < DmTypes.TreeOrdinal(typeof(fx_movable)));
        Assert.Equal(-1, DmTypes.TreeOrdinal(typeof(TestDatum)));
    }

    [Fact]
    public void SubtreesAreCachedAndReadOnly()
    {
        IReadOnlyList<Type> first = DmTypes.Subtypes(typeof(fx_item), includeRoot: true);
        Assert.Same(first, DmTypes.Subtypes(typeof(fx_item), includeRoot: true));
        Assert.True(((IList<Type>)first).IsReadOnly);
        // typesof builds a new list each time: changing it does not change the next answer.
        DmList types = typesof(typeof(fx_stack));
        types.len = 0;
        Assert.Equal(2, typesof(typeof(fx_stack)).Count);
    }

    [Fact]
    public void TypesofTakesTypesObjectsAndTextAndSkipsNullsAndLists()
    {
        // OpenDream NativeProc_typesof: a type, an object (its type) or text; null and lists are skipped; each argument
        // appends its own subtree, so a type named twice appears twice.
        var stack = new fx_stack();
        DmList types = typesof(null, list(), "/datum/dynfx/atom/movable/item/stack", stack, typeof(fx_sheet));
        Assert.Equal(new object[] { typeof(fx_stack), typeof(fx_sheet), typeof(fx_stack), typeof(fx_sheet), typeof(fx_sheet) }, Items(types));
        Assert.Equal(0, typesof(null).Count);          // a single null arrives as a null params array
        Assert.Equal(0, typesof("/datum/dynfx/nothing_here").Count);
        Assert.Equal(new object[] { typeof(DmList) }, Items(typesof(typeof(DmList))));
    }

    [Fact]
    public void TypesofSkipsATypeOrObjectThatIsNoDmType()
    {
        // OpenDream NativeProc_typesof: an argument that is neither a type, an object nor text is skipped ("continue");
        // a .NET class without [DmPath] on it or a base (a host or BCL class) is no DM type, and its instances no DM
        // objects, so they add nothing (as new and initial() refuse them).
        Assert.Equal(0, typesof(typeof(System.Text.StringBuilder)).Count);
        Assert.Equal(0, typesof(new System.Text.StringBuilder()).Count);
        Assert.Equal(new object[] { typeof(fx_stack), typeof(fx_sheet) }, Items(typesof(typeof(object), typeof(fx_stack), new object())));
    }

    [Fact]
    public void TypesofAProcPathListsTheProcsDeclaredOnTheTypeInOrderAndCallTakesThem()
    {
        // BYOND ref /proc/typesof ("can also be used to list procs and verbs"); object-model.md section 9: declared on
        // the type itself (not inherited, not overrides), in compile order. tg calls them by name:
        // controllers/globals.dm: for(var/I in typesof(/datum/controller/global_vars/proc)) call(src, I)().
        DmList procs = typesof("/datum/dynfx/procs/proc");
        Assert.Equal(new object[] { "/datum/dynfx/procs/proc/zeta", "/datum/dynfx/procs/proc/alpha", "/datum/dynfx/procs/proc/start" }, Items(procs));
        var holder = new fx_procs();
        Assert.Equal("zeta", Waiting.NoSleep(Dm.Call(holder, (string)procs[1])));
        Assert.Equal("start", Waiting.NoSleep(Dm.Call(holder, (string)procs[3])));
        Assert.Equal(0, typesof("/datum/dynfx/procs/verb").Count);
    }

    [Fact]
    public void IstypeWithATypeValueTestsDerivation()
    {
        // BYOND ref /proc/istype: "1 if Val is derived from Type"; Val is an object instance.
        var stack = new fx_stack();
        Assert.True(istype(stack, typeof(fx_item)));
        Assert.True(istype(stack, typeof(fx_stack)));
        Assert.False(istype(new fx_item(), typeof(fx_stack)));
        Assert.False(istype(null, typeof(fx_item)));
        Assert.False(istype(stack, null));
        Assert.False(istype(typeof(fx_stack), typeof(fx_item)));  // a typepath is not an instance: ispath
        Assert.False(istype(5f, typeof(fx_item)));
        Assert.True(istype(list(), typeof(DmList)));                // istype(x, /list)
        Assert.Same(stack, astype(stack, typeof(fx_item)));         // BYOND ref /proc/astype
        Assert.Null(astype(new fx_item(), typeof(fx_stack)));
    }

    [Fact]
    public void IspathTestsTypepaths()
    {
        // BYOND ref /proc/ispath, including its example: ispath(new/mob) is false.
        Assert.True(ispath(typeof(fx_stack)));
        Assert.False(ispath(new fx_stack()));
        Assert.False(ispath("/datum/dynfx/atom"));
        Assert.False(ispath(null));
        Assert.True(ispath(typeof(fx_stack), typeof(fx_item)));
        Assert.True(ispath(typeof(fx_item), typeof(fx_item)));
        Assert.False(ispath(typeof(fx_item), typeof(fx_stack)));
        Assert.True(ispath(typeof(fx_item), null));                  // OpenDream: a non-type Type argument is ignored
        Assert.True(DmTypes.IsSubtypeOrSame(typeof(fx_sheet), typeof(fx_datum)));
        Assert.False(DmTypes.IsSubtypeOrSame(typeof(fx_datum), typeof(fx_sheet)));
    }

    // Classes for registries built in the test: no [DmPath], so the process-wide registry never sees them.
    private class O_datum { }
    private class O_b : O_datum { }
    private class O_bx : O_b { }
    private class O_a : O_datum { }
    private class O_a_unlisted : O_a { }
    private class O_z_unlisted : O_datum { }
    private class O_sound { }
    private class O_list { }
    private class O_claims_b_too { }

    private static DmTypeRegistry Build(IDmTypeOrder order, params (Type Type, string Path)[] types)
        => new(types.Select(t => new KeyValuePair<Type, string>(t.Type, t.Path)), order);

    [Fact]
    public void TheGeneratedOrderDecidesTreeOrderAndUnlistedTypesComeAfterIt()
    {
        // object-model.md section 9: preorder with children in compile order (here /datum/b was declared before
        // /datum/a), types missing from the list after the listed ones, by ordinal path.
        var order = new DmTypeOrder(new[] { "/datum", "/datum/b", "/datum/b/x", "/datum/a", "/list", "/sound" }, null);
        DmTypeRegistry registry = Build(order,
            (typeof(O_a_unlisted), "/datum/a/unlisted"), (typeof(O_z_unlisted), "/datum/z_unlisted"), (typeof(O_a), "/datum/a"),
            (typeof(O_sound), "/sound"), (typeof(O_bx), "/datum/b/x"), (typeof(O_datum), "/datum"), (typeof(O_b), "/datum/b"),
            (typeof(O_list), "/list"));
        Assert.Equal(new[] { typeof(O_datum), typeof(O_b), typeof(O_bx), typeof(O_a), typeof(O_sound), typeof(O_a_unlisted), typeof(O_z_unlisted) },
            registry.Subtypes(typeof(O_datum), includeRoot: true));
        Assert.Equal(new[] { typeof(O_a_unlisted) }, registry.Subtypes(typeof(O_a), includeRoot: false));
        Assert.Equal(0, registry.TreeOrdinal(typeof(O_datum)));
        Assert.Equal(3, registry.TreeOrdinal(typeof(O_a)));
        Assert.Equal(6, registry.TreeOrdinal(typeof(O_a_unlisted)));   // the first unlisted type: after all 6 listed
        Assert.Equal(7, registry.TreeOrdinal(typeof(O_z_unlisted)));
    }

    [Fact]
    public void ATopLevelBuiltinWithoutARegisteredBaseIsADatum()
    {
        // BYOND ref /datum/var/parent_type: a top-level type without an explicit parent_type defaults to /datum; /list
        // is one of BYOND's roots (OpenDream's DM standard library declares /sound, /exception, /matrix under /datum).
        DmTypeRegistry registry = Build(DmTypeOrder.Empty,
            (typeof(O_datum), "/datum"), (typeof(O_sound), "/sound"), (typeof(O_list), "/list"), (typeof(O_b), "/datum/b"));
        Assert.Equal(typeof(O_datum), registry.ParentOf(typeof(O_sound)));
        Assert.True(registry.IsSubtypeOrSame(typeof(O_sound), typeof(O_datum)));
        Assert.Null(registry.ParentOf(typeof(O_list)));
        Assert.Null(registry.ParentOf(typeof(O_datum)));
        Assert.False(registry.IsSubtypeOrSame(typeof(O_list), typeof(O_datum)));
        // Without an order source every type sorts by ordinal path.
        Assert.Equal(new[] { typeof(O_datum), typeof(O_b), typeof(O_sound) }, registry.Subtypes(typeof(O_datum), includeRoot: true));
    }

    private class O_client : O_datum { }
    private class O_client_x : O_client { }

    [Fact]
    public void AReparentedTypeStaysInsideItsParentsRunWithoutAGeneratedOrder()
    {
        // object-model.md section 9 and BYOND ref /proc/typesof: DM order is a depth-first preorder of the type tree
        // (OpenDream DreamObjectTree.GetAllDescendants), so typesof(T) starts with T and T's subtree is contiguous.
        // tg re-parents /client under /datum (client_defines.dm:36, parent_type = /datum): its path is no extension of
        // /datum's, so sorting unlisted types by bare path would put /client before /datum. Siblings still sort by path.
        DmTypeRegistry registry = Build(DmTypeOrder.Empty,
            (typeof(O_b), "/datum/b"), (typeof(O_client_x), "/client/x"), (typeof(O_datum), "/datum"), (typeof(O_client), "/client"));
        Assert.Equal(new[] { typeof(O_datum), typeof(O_client), typeof(O_client_x), typeof(O_b) },
            registry.Subtypes(typeof(O_datum), includeRoot: true));
        Assert.Equal(new[] { typeof(O_client), typeof(O_client_x) }, registry.Subtypes(typeof(O_client), includeRoot: true));
        // Listed types keep their place; the unlisted ones follow in that same preorder.
        DmTypeRegistry partly = Build(new DmTypeOrder(new[] { "/datum", "/datum/b" }, null),
            (typeof(O_b), "/datum/b"), (typeof(O_client_x), "/client/x"), (typeof(O_datum), "/datum"), (typeof(O_client), "/client"));
        Assert.Equal(new[] { 0, 1, 2, 3 }, new[] { typeof(O_datum), typeof(O_b), typeof(O_client), typeof(O_client_x) }.Select(partly.TreeOrdinal));
    }

    [Fact]
    public void ASecondClassClaimingAPathIsRecordedAndTheFirstKeepsIt()
    {
        DmTypeRegistry registry = Build(DmTypeOrder.Empty, (typeof(O_b), "/datum/b"), (typeof(O_claims_b_too), "/datum/b"));
        Assert.Equal(typeof(O_b), registry.FromPath("/datum/b"));
        Assert.Null(registry.PathOf(typeof(O_claims_b_too)));
        string duplicate = Assert.Single(registry.Duplicates);
        Assert.StartsWith("/datum/b: ", duplicate);
    }

    [Fact]
    public void TheOrderSourceListsProcsPerType()
    {
        var order = new DmTypeOrder(Array.Empty<string>(), new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("/datum/controller/global_vars", new[] { "InitGlobalb", "InitGlobala" }),
        });
        Assert.True(order.TryGetProcs("/datum/controller/global_vars", verbs: false, out IReadOnlyList<string> procs));
        Assert.Equal(new[] { "InitGlobalb", "InitGlobala" }, procs);
        Assert.False(order.TryGetProcs("/datum/controller/global_vars", verbs: true, out _));
        Assert.False(order.TryGetProcs("/datum", verbs: false, out _));
    }

    private static object[] Items(DmList list)
    {
        var items = new object[list.Count];
        for (int i = 0; i < items.Length; i++) items[i] = list[(float)(i + 1)];
        return items;
    }
}
