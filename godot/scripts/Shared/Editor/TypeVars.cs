using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>One var of a type as the map editor lists it: its name and its type's value as a literal of the map format (null for a value that has none: an object).</summary>
public sealed record TypeVar(string Name, string Default);

/// <summary>
/// The vars of tg's types for the map editor's properties panel (docs/ai/specs/map-editor.md 5.2): read, as the spawn catalog reads a type's
/// name and picture, from the type's class in the translated code, whose constructor assigns the type's plain values and touches no world.
/// </summary>
public static class TypeVars
{
    // BYOND's built-ins that are not a mapper's to set on a map: where a thing is and what it holds come from the tile
    private static readonly HashSet<string> NotMapped = new(StringComparer.Ordinal)
    {
        "type", "parent_type", "vars", "loc", "locs", "x", "y", "z", "contents", "verbs", "vis_contents", "vis_locs", "overlays", "underlays", "appearance",
    };

    private static readonly Regex VarName = new("^[a-z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, IReadOnlyList<TypeVar>> Known = new(StringComparer.Ordinal);

    /// <summary>The vars of the type of a path, by name; none for a path the translated code has no class of.</summary>
    public static IReadOnlyList<TypeVar> Of(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Array.Empty<TypeVar>();
        lock (Known)
        {
            if (!Known.TryGetValue(path, out IReadOnlyList<TypeVar> vars))
                Known[path] = vars = Read(path);
            return vars;
        }
    }

    private static IReadOnlyList<TypeVar> Read(string path)
    {
        Type type = DmTypes.FromPath(path);
        if (type == null || type.IsAbstract)
            return Array.Empty<TypeVar>();
        object instance;
        try
        {
            instance = Activator.CreateInstance(type);
        }
        catch (Exception e) when (e is TargetInvocationException or MissingMethodException or MemberAccessException)
        {
            return Array.Empty<TypeVar>(); // a class whose constructor needs a world is no plain-values class
        }
        var vars = new Dictionary<string, TypeVar>(StringComparer.Ordinal);
        const BindingFlags members = BindingFlags.Public | BindingFlags.Instance;
        foreach (FieldInfo field in type.GetFields(members))
        {
            if (IsVar(field.Name) && !field.IsInitOnly)
                vars[field.Name] = new TypeVar(field.Name, Literal(field.GetValue(instance)));
        }
        foreach (PropertyInfo property in type.GetProperties(members))
        {
            if (!IsVar(property.Name) || !property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                continue;
            try
            {
                vars[property.Name] = new TypeVar(property.Name, Literal(property.GetValue(instance)));
            }
            catch (TargetInvocationException)
            {
                // a var whose getter needs a world: listed without its value
                vars[property.Name] = new TypeVar(property.Name, null);
            }
        }
        return vars.Values.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
    }

    // (a name that starts with an underscore is the datum's own bookkeeping: its timers, signals and components)
    private static bool IsVar(string name) => VarName.IsMatch(name) && name[0] != '_' && !name.StartsWith("dm_", StringComparison.Ordinal) && !NotMapped.Contains(name);

    /// <summary>A plain value as a literal of the map format (map-format.md 4.3), or null for a value the format has no literal of.</summary>
    public static string Literal(object value) => value switch
    {
        null => "null",
        string text => Quote(text),
        bool flag => flag ? "1" : "0",
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        int or long or byte or short => Convert.ToString(value, CultureInfo.InvariantCulture),
        Type type => DmTypes.PathOf(type),
        DmResource resource => "'" + resource.Path + "'",
        _ => null,
    };

    private static string Quote(string text)
    {
        var quoted = new StringBuilder(text.Length + 2).Append('"');
        foreach (char c in text)
        {
            if (c == '\n')
                quoted.Append("\\n");
            else if (c is '"' or '\\')
                quoted.Append('\\').Append(c);
            else
                quoted.Append(c);
        }
        return quoted.Append('"').ToString();
    }
}
