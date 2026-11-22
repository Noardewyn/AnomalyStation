using System.Linq;
using System.Reflection;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. Type tests and typepath builtins that take a Type value (docs/ai/translation-rules.md 10.4;
// docs/ai/specs/object-model.md section 9). Constant type tests are C# `is`.
public static partial class Dm
{
    /// <summary>
    /// DM <c>istype(Val, Type)</c> with a <c>Type</c> value (BYOND ref <c>/proc/istype</c>; rules 10.4): whether Val is
    /// an instance of Type or a subtype (<c>typeof(DmList)</c> for <c>/list</c>). A constant type is <c>x is T</c>.
    /// </summary>
    public static bool istype(object Val, Type Type)
    {
        // BYOND: Val is "an object instance"; a typepath, a number or text is never an instance (use ispath).
        if (Val == null || Type == null || Val is Type || Val is string || TryGetNumber(Val, out _)) return false;
        return Type.IsInstanceOfType(Val) || DmTypes.IsSubtypeOrSame(Val.GetType(), Type);
    }

    /// <summary>DM <c>astype(Val, Type)</c> with a <c>Type</c> value (BYOND 516): Val when <see cref="istype"/>, else null.</summary>
    public static object astype(object Val, Type Type) => istype(Val, Type) ? Val : null;

    /// <summary>DM <c>ispath(Val)</c> (BYOND ref <c>/proc/ispath</c>): whether Val is a typepath.</summary>
    public static bool ispath(object Val) => Val is Type;

    /// <summary>
    /// DM <c>ispath(Val, Type)</c>: whether Val is a typepath derived from Type (Type itself included). A null Type is
    /// the one-argument form (OpenDream <c>NativeProc_ispath</c>: a Type argument that is not a type is ignored).
    /// </summary>
    public static bool ispath(object Val, Type Type)
    {
        if (Val is not Type path) return false;
        return Type == null || DmTypes.IsSubtypeOrSame(path, Type);
    }

    /// <summary>
    /// DM <c>typesof(Type1, Type2, ...)</c> (BYOND ref <c>/proc/typesof</c>): a list of the types derived from the
    /// given ones, themselves included, in DM type-tree order (object-model.md section 9); a proc path such as
    /// <c>/datum/controller/global_vars/proc</c> gives its procs in compile order.
    /// </summary>
    public static DmList typesof(params object[] Types)
    {
        // OpenDream NativeProc_typesof: each argument is a type, an object (its type) or text (a type path, or a
        // "/proc" or "/verb" path); null, lists and other values are skipped, and so are a .NET class that is no DM
        // type and its instances; results are appended argument by argument, so a type named twice appears twice.
        Types ??= OneNull();
        var result = new DmList();
        foreach (object item in Types)
        {
            switch (item)
            {
                case null:
                case DmList:
                    break;
                case Type type:
                    if (DmTypes.Registry.IsDmClass(type)) AppendSubtree(result, type);
                    break;
                case string text when text.EndsWith("/proc", StringComparison.Ordinal):
                    AppendProcs(result, text.Substring(0, text.Length - "/proc".Length), verbs: false);
                    break;
                case string text when text.EndsWith("/verb", StringComparison.Ordinal):
                    AppendProcs(result, text.Substring(0, text.Length - "/verb".Length), verbs: true);
                    break;
                case string text:
                    Type named = text2path(text);
                    if (named != null) AppendSubtree(result, named);
                    break;
                default:
                    if (!TryGetNumber(item, out _) && DmTypes.Registry.IsDmClass(item.GetType()))
                        AppendSubtree(result, DmTypes.Registry.DmTypeOf(item.GetType()) ?? item.GetType());
                    break;
            }
        }
        return result;
    }

    private static void AppendSubtree(DmList result, Type root)
    {
        foreach (Type type in DmTypes.Subtypes(root, includeRoot: true))
            result.AppendItem(type);
    }

    // typesof(/t/proc): the procs declared on /t itself (not inherited, not overrides) in compile order, as proc
    // paths "/t/proc/name" (what DM's "[I]" prints for a proc reference; Dm.Call takes the last segment). The
    // generated ProcOrder gives the order, filtered to the procs the binder resolves on the class (the translated
    // ones); without it, the procs that the class itself declares (new virtual or
    // non-virtual public methods the binder can call, so not the port's dm_* hooks or name__d0 definitions) in
    // declaration order.
    private static void AppendProcs(DmList result, string typePath, bool verbs)
    {
        string kind = verbs ? "/verb/" : "/proc/";
        if (DmTypes.Registry.Order.TryGetProcs(typePath, verbs, out IReadOnlyList<string> names))
        {
            // W3.0 (S3): only the procs the binder resolves on the type; a proc that is not translated yet does not
            // exist in the port, so tg's call(src, I)() over the list (controllers/globals.dm:63) never meets an
            // undefined proc. No class for the type: no proc exists.
            Type owner = text2path(typePath);
            if (owner == null) return;
            foreach (string name in names)
            {
                if (DmBinder.ResolvesOn(owner, name)) result.AppendItem(typePath + kind + name);
            }
            return;
        }
        if (verbs) return; // verbs are not translated before the command layer (rules 7.10)
        Type type = text2path(typePath);
        if (type == null) return;
        IEnumerable<MethodInfo> declared = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetBaseDefinition() == m && DmBinder.IsBindable(m))
            .OrderBy(m => m.MetadataToken);
        foreach (MethodInfo method in declared)
        {
            string name = method.Name.EndsWith("__proc", StringComparison.Ordinal) ? method.Name[..^"__proc".Length] : method.Name;
            result.AppendItem(typePath + kind + name);
        }
    }

    /// <summary>DM <c>text2path(T)</c> (BYOND ref <c>/proc/text2path</c>): the typepath named by T, or null.</summary>
    public static Type text2path(string T) => string.IsNullOrWhiteSpace(T) ? null : DmTypes.FromPath(T);

    /// <summary>DM <c>hascall(Object, ProcName)</c> (BYOND ref <c>/proc/hascall</c>): whether the object has a proc of that name.</summary>
    public static bool hascall(object Object, string ProcName)
    {
        // OpenDream NativeProc_hascall: false unless Object is an object and ProcName is text; a global proc path names
        // no proc of the object (even though call(Object, "/proc/x") runs that global proc).
        if (Object == null || Object is string || Object is Type || TryGetNumber(Object, out _) || string.IsNullOrEmpty(ProcName)) return false;
        if (DmBinder.IsGlobalProcPath(ProcName)) return false;
        return DmBinder.Resolve(Object, ProcName) != DmBinder.Target.Missing;
    }

    /// <summary>
    /// BYOND <c>ref(Object)</c> (BYOND ref <c>/proc/ref</c>), which tg's <c>REF</c>/<c>text_ref</c> expand to: the text
    /// form of a reference (object-model.md section 3): <c>"[0x" + dm_id in lowercase hex + "]"</c> for a datum, a
    /// kind-prefixed number for a list (<c>"[0xf000001]"</c>, allocated per world on its first ref), a typepath
    /// (<c>"[0x9" + its type-order ordinal + "]"</c>) and <c>world</c> (<c>"[0xe000000]"</c>); <c>"[0x0]"</c> for null.
    /// Text, numbers, prototypes and builtin values without an id have none: a runtime error.
    /// </summary>
    public static string @ref(object Object) => DmRefs.RefText(Object);
}
