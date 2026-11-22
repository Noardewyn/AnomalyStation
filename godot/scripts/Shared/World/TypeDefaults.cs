using System.Linq;
using System.Globalization;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free (compiled into the client and, linked, into tests/Anomaly.Server.Tests/Client/). What the FakeFeed
// version 3 (FakeMap) knows about a DM type without booting tg: the defaults of its constructor.

/// <summary>
/// The defaults of a DM type that decide how the real server would replicate an instance of it right after init: its
/// icon and state, density and opacity, layer, <c>ON_BORDER_1</c>, invisibility and light. Read from the type's generated
/// class (<see cref="ReflectionTypeSource"/>, the truth for the compile-time vars), with the few facts tg's init adds on top of the
/// class's pre-init values from <see cref="PathRuleTypeSource"/> (<see cref="MergedTypeSource"/>), or for a type with no class guessed from its path.
/// </summary>
public sealed class TypeDefaults
{
    /// <summary>The DM type path.</summary>
    public string Path { get; init; }

    /// <summary>tg's <c>name</c>, null when the source does not know it.</summary>
    public string Name { get; set; }

    /// <summary>The icon path (<c>icons/obj/doors/airlocks/station/public.dmi</c>), null for none.</summary>
    public string Icon { get; set; }

    /// <summary>The icon state, null for none. For a class this is the PRE-init value, the one the map editor previews (<see cref="PostInitIconState"/> replaces it in tg's init).</summary>
    public string IconState { get; set; }

    /// <summary>
    /// tg's <c>post_init_icon_state</c> (M3 I1c): when it is set, <c>/atom/Initialize</c> replaces <c>icon_state</c> with it (atoms_initializing_EXPENSIVE.dm:138-139), whatever the map
    /// said, so it is the state the thing draws in the game; the map editor's preview state (<see cref="IconState"/>, or the map's own override) is only a picture for the editor
    /// (a meter's <c>meterX</c>, a plush's <c>map_plushie_lizard</c>, a sofa's one icon for every side). Null when the type has none (an empty string is false in DM's <c>if</c>).
    /// </summary>
    public string PostInitIconState { get; set; }

    /// <summary>
    /// tg's <c>greyscale_config</c> as its type path (<c>/datum/greyscale_config/meter</c>) when the type is a greyscale (GAGS) one: both it and <see cref="GreyscaleColors"/> set, as
    /// <c>/atom/Initialize</c> wants (atoms_initializing_EXPENSIVE.dm:128-129). Init then replaces <c>icon</c> with the generated icon, which the real server replicates as the resource
    /// <c>gags:&lt;config&gt;|&lt;colours&gt;</c> (<see cref="IconAfterInit"/>). Null for any other type.
    /// </summary>
    public string GreyscaleConfig { get; set; }

    /// <summary>tg's <c>greyscale_colors</c> (<c>#808080</c>, <c>#638799#a52f29</c>) of a greyscale type; null otherwise.</summary>
    public string GreyscaleColors { get; set; }

    /// <summary>
    /// The icon the thing draws after tg's init: the generated <c>gags:</c> icon for a greyscale type that has a <see cref="PostInitIconState"/> (the state is one of that icon's; with the map's own
    /// <paramref name="mapColors"/> if it set some), else <see cref="Icon"/>. A greyscale type with no post-init state keeps its picture (a canister's is all overlays, which the fake feed has not).
    /// </summary>
    public string IconAfterInit(string mapColors)
        => GreyscaleConfig != null && !string.IsNullOrEmpty(PostInitIconState) ? $"gags:{GreyscaleConfig}|{(string.IsNullOrEmpty(mapColors) ? GreyscaleColors : mapColors)}" : Icon;

    /// <summary>The state the thing draws after tg's init: its <see cref="PostInitIconState"/> when it has one, else the map's own <paramref name="mapIconState"/> if any, else <see cref="IconState"/>.</summary>
    public string StateAfterInit(string mapIconState) => !string.IsNullOrEmpty(PostInitIconState) ? PostInitIconState : (mapIconState ?? IconState);

    /// <summary>tg's <c>base_icon_state</c> (the stem of a smoothed picture: <c>wall</c> for <c>wall-0</c>), null when the type does not smooth (a door has a base_icon_state too, without smoothing).</summary>
    public string BaseIconState { get; set; }

    /// <summary>tg's <c>reinf</c> of a window or a windoor (M3 R6): reinforced glass; false for any other type, and for a type the source reads no class of.</summary>
    public bool Reinforced { get; set; }

    /// <summary>tg's <c>density</c>.</summary>
    public bool Density { get; set; }

    /// <summary>tg's <c>opacity</c>.</summary>
    public bool Opacity { get; set; }

    /// <summary>BYOND <c>layer</c>.</summary>
    public float Layer { get; set; } = 3f;

    /// <summary>BYOND <c>plane</c> (M3 R5, D122): tg's <c>FLOOR_PLANE</c> (-6) for the lattice, the disposal pipes and the wires, <c>GAME_PLANE</c> (-4) for what stands in the room; 0 when the source does not know it (the client then takes the game plane).</summary>
    public float Plane { get; set; }

    /// <summary>tg's <c>piping_layer</c> of an atmospherics machine (M3 R5): 1 to 5, 3 the default, which moves its layer by 0.001 each (<c>pipes.dm:151</c>, <c>components_base.dm:get_pipe_layer_offset</c>); 3 for anything else.</summary>
    public int PipingLayer { get; set; } = 3;

    /// <summary>tg's <c>pipe_color</c> of an atmospherics pipe or machine (a hex colour text, M3 R5), null for none: its place in <c>GLOB.pipe_colors_ordered</c> adds 0.0001 per index to the layer.</summary>
    public string PipeColor { get; set; }

    /// <summary>tg's turf <c>underfloor_accessibility</c> (M3 R5, D122: <see cref="PropKinds.TileAccess"/>; 0 for anything that is not a turf): what shows of the cables, pipes and disposals under the turf's floor.</summary>
    public int UnderfloorAccessibility { get; set; }

    /// <summary>BYOND <c>dir</c> (2, south, by default).</summary>
    public byte Dir { get; set; } = ByondDirConst.South;

    /// <summary>BYOND <c>pixel_x</c>.</summary>
    public short PixelX { get; set; }

    /// <summary>BYOND <c>pixel_y</c>.</summary>
    public short PixelY { get; set; }

    /// <summary>tg's <c>invisibility</c> (D115 shows up to <c>ReplicatedFields.MaxEntityInvisibility</c>).</summary>
    public float Invisibility { get; set; }

    /// <summary>Whether <c>flags_1</c> has <c>ON_BORDER_1</c>.</summary>
    public bool OnBorder { get; set; }

    /// <summary>tg's light vars as the replicator would send them (<see cref="LightState.None"/> for none).</summary>
    public LightState Light { get; set; }

    /// <summary>
    /// The path of tg's <c>random_basetype</c> (a poster's: <c>/obj/structure/sign/poster/random</c> picks one of the subtypes of <c>/obj/structure/sign/poster</c> in its
    /// <c>Initialize</c>, poster.dm:187), null for a type that does not randomise itself. Before that init its picture is the placeholder state <c>random_anything</c>, a magenta checker.
    /// </summary>
    public string RandomBaseType { get; set; }

    /// <summary>tg's <c>sharpness</c> is set (an edge or a point: a knife, a sword, a spear), and its <c>w_class</c>: the own swing's whoosh (SwingSound).</summary>
    public bool Sharp { get; set; }

    public int WeightClass { get; set; }

    /// <summary>A copy to change fields of.</summary>
    public TypeDefaults Copy() => (TypeDefaults)MemberwiseClone();
}

/// <summary>The BYOND direction values this folder needs (the icon pipeline's <c>ByondDir</c> has them too).</summary>
/// <summary>D115's entity threshold, as <c>ReplicatedFields.MaxEntityInvisibility</c> on the server (SEE_INVISIBLE_LIVING).</summary>
internal static class ReplicatedFieldsLimit
{
    public const float MaxEntityInvisibility = 25f;
}

internal static class ByondDirConst
{
    public const byte North = 1, South = 2, East = 4, West = 8;
}

/// <summary>Where <see cref="FakeMap"/> gets a type's defaults.</summary>
public interface ITypeSource
{
    /// <summary>The defaults of <paramref name="typePath"/>; false when the source knows nothing about the type.</summary>
    bool TryGet(string typePath, out TypeDefaults defaults);
}

/// <summary>A type source that can list what a self-randomising type may become (the fake feed does what tg's init does: M3 R3, the random posters).</summary>
public interface IRandomTypeSource
{
    /// <summary>
    /// The types tg's <c>poster/randomise(base_type)</c> may pick for <paramref name="basePath"/> (poster.dm:187): its subtypes that have an icon state, are not <c>never_random</c>
    /// and have no pixel offset (so no directional helper), without the subtypes under <paramref name="excludedPrefixes"/>; in path order.
    /// </summary>
    IReadOnlyList<TypeDefaults> RandomChoices(string basePath, IReadOnlyList<string> excludedPrefixes);
}

/// <summary>
/// The FakeFeed's type source (M3 R2): the type's generated class is the truth for the vars it sets at compile time (icon, icon state, dir, pixel
/// offsets, density, opacity, layer, invisibility, name, the border flag, the light vars) and the path rules add only what tg's init does on top
/// of those pre-init values and the class cannot say. A rule never overrides a var the class gives (a rule is a prefix of a family, a class is the
/// type's own: <c>/obj/machinery/door/poddoor/shutters/preopen</c> is <c>open</c> and not dense in tg whatever its family's rule says; a stool's
/// directional helpers offset nothing; a fire alarm's is 26 pixels and not 32). The facts a rule still adds (<see cref="Merge"/>):
/// <list type="bullet">
/// <item><description><b>hidden</b>: a rule that hides the family (pipes, cables, disposal pipes, helpers, landmarks: tg hides them in init, under the floor or by deleting
/// them, D115) is final;</description></item>
/// <item><description><b>a light lit</b>: a light fixture's class has no light range before its init (tg sets it in <c>update</c>), the rule has the lit tube or bulb;</description></item>
/// <item><description><b>a picture</b>, when the class has none or no icon state (a hand-written class that only has what its milestone needed, and the generated subtypes of one: a camera's class has the fax machine's icon), and the smoothing stem of a family the rule smooths.</description></item>
/// </list>
/// A type with no class gets the rule's answer, or the plain placeholder of <paramref name="fallback"/>.
/// </summary>
public sealed class MergedTypeSource : ITypeSource, IRandomTypeSource
{
    private readonly ITypeSource _classes, _rules, _fallback;
    private readonly Dictionary<string, TypeDefaults> _cache = new();

    /// <param name="classes">The generated classes (<see cref="ReflectionTypeSource"/>).</param>
    /// <param name="rules">The path rules that name a family (<see cref="PathRuleTypeSource"/> with <c>specificOnly</c>).</param>
    /// <param name="fallback">The answer for a type with neither a class nor a rule (the plain placeholder); null for none.</param>
    public MergedTypeSource(ITypeSource classes, ITypeSource rules, ITypeSource fallback = null)
    {
        _classes = classes;
        _rules = rules;
        _fallback = fallback;
    }

    public IReadOnlyList<TypeDefaults> RandomChoices(string basePath, IReadOnlyList<string> excludedPrefixes)
        => _classes is IRandomTypeSource random ? random.RandomChoices(basePath, excludedPrefixes) : Array.Empty<TypeDefaults>();

    public bool TryGet(string typePath, out TypeDefaults defaults)
    {
        if (_cache.TryGetValue(typePath, out defaults))
            return defaults != null;
        _classes.TryGet(typePath, out TypeDefaults cls);
        _rules.TryGet(typePath, out TypeDefaults rule);
        if (rule == null && (cls == null || string.IsNullOrEmpty(cls.IconState)) && _fallback != null)
            _fallback.TryGet(typePath, out rule);
        defaults = Merge(cls, rule);
        _cache[typePath] = defaults;
        return defaults != null;
    }

    /// <summary>The merge of a type's class answer and a rule's (either may be null); see the class's summary for which field comes from where.</summary>
    public static TypeDefaults Merge(TypeDefaults cls, TypeDefaults rule)
    {
        if (cls == null)
            return rule;
        if (rule == null)
            return cls;
        // Hidden after init is final: the class only has the pre-init values, and tg hides these families in init.
        if (rule.Invisibility > ReplicatedFieldsLimit.MaxEntityInvisibility)
            return rule;
        TypeDefaults merged = cls.Copy();
        // A class with no picture, or with an icon but no state, has not got its own: a hand-written skeleton class (M0-M2: /obj/machinery/camera) and the
        // generated subtypes of one only declare the vars they changed, so their icon is the base's (a camera's is the fax machine's file, with no state).
        if (string.IsNullOrEmpty(cls.IconState) && !string.IsNullOrEmpty(rule.IconState))
        {
            merged.Icon = rule.Icon;
            merged.IconState = rule.IconState;
        }
        merged.BaseIconState ??= rule.BaseIconState;
        if (cls.UnderfloorAccessibility == 0 && rule.UnderfloorAccessibility != 0)
            merged.UnderfloorAccessibility = rule.UnderfloorAccessibility;
        if (cls.Light.IsNone && !rule.Light.IsNone)
            merged.Light = rule.Light;
        merged.OnBorder = cls.OnBorder || rule.OnBorder;
        return merged;
    }
}

/// <summary>
/// The constructor defaults of the type's class in <c>Anomaly.Core.Tg</c> (rules 10.2: a generated constructor assigns plain
/// defaults and never touches a world). The class is found by its <c>[DmPath]</c>; a constructor that throws means the type
/// is unknown to this source. Reads the builtin vars by name, so it works for hand-written and generated classes alike.
/// </summary>
public sealed class ReflectionTypeSource : ITypeSource, IRandomTypeSource
{
    private readonly Assembly[] _assemblies;
    private readonly Dictionary<string, Type> _resolved = new();
    private Dictionary<string, Type> _all;

    /// <summary>
    /// The classes of <c>Anomaly.Core.Tg</c> and of the shim, loaded by naming the assembly: the answer never depends on which assemblies the
    /// process happened to have loaded before (M3 R2; it used to scan the AppDomain, so the same call gave a different source in the game and in the tests).
    /// </summary>
    public ReflectionTypeSource() : this(typeof(DmPathAttribute).Assembly, TgAssembly)
    {
    }

    /// <summary>The assembly of the translated tg code, named explicitly so that it is loaded (and found) whatever was loaded before.</summary>
    public static Assembly TgAssembly => typeof(TgWorld).Assembly;

    public ReflectionTypeSource(params Assembly[] assemblies) => _assemblies = assemblies.Where(a => a != null).Distinct().ToArray();

    /// <summary>How many classes the source knows (this scans every type of the assemblies once: about two seconds for tg's 46,000).</summary>
    public int ClassCount => AllClasses().Count;

    // The full index by [DmPath]: every type of the assemblies, scanned once and only when a class could not be found by its name.
    private Dictionary<string, Type> AllClasses()
    {
        if (_all != null)
            return _all;
        var all = new Dictionary<string, Type>();
        foreach (Assembly assembly in _assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                DmPathAttribute path = type.GetCustomAttribute<DmPathAttribute>(false);
                if (path != null && !type.IsAbstract)
                    all[path.Path] = type;
            }
        }
        return _all = all;
    }

    // The class of a path. tg's classes are named after their paths (/obj/machinery/light/directional/north is obj_machinery_light_directional_north in
    // Anomaly.Core.Tg), so a name lookup finds one without scanning the 46,000 types (that scan costs two seconds at every client start); a path the
    // name does not give (an odd character, a class that is abstract) falls back to the full index.
    private Type FindClass(string typePath)
    {
        if (_resolved.TryGetValue(typePath, out Type known))
            return known;
        Type found = null;
        if (typePath.Length > 1 && typePath[0] == '/')
        {
            string name = typePath.Substring(1).Replace('/', '_');
            foreach (Assembly assembly in _assemblies)
            {
                foreach (string space in Spaces)
                {
                    Type candidate = assembly.GetType(space + "." + name, throwOnError: false);
                    if (candidate != null && !candidate.IsAbstract && candidate.GetCustomAttribute<DmPathAttribute>(false)?.Path == typePath)
                    {
                        found = candidate;
                        break;
                    }
                }
                if (found != null)
                    break;
            }
        }
        // a path tg has not got (a directional window's renderer asks for "<parent>/fulltile" up its parents) has no class to find: the full
        // index is for tg's paths the name does not give, and building it on the first miss stalled the join by seconds (7 s, 2026-10-29)
        if (found == null && TgPaths.Contains(typePath))
            AllClasses().TryGetValue(typePath, out found);
        _resolved[typePath] = found;
        return found;
    }

    private static readonly string[] Spaces = { "Anomaly.Core.Tg", "Anomaly.Core.DmShim" };

    private static HashSet<string> _tgPaths;
    private static HashSet<string> TgPaths => _tgPaths ??= new HashSet<string>(TypeOrder.Paths, StringComparer.Ordinal);

    /// <summary>tg's <c>ON_BORDER_1</c>: <c>1 &lt;&lt; 1</c> (code/__DEFINES/_flags.dm:25; it was read as 1 &lt;&lt; 9 until M3 R2, so no class ever had it).</summary>
    public const int OnBorder1 = 1 << 1;

    /// <summary>tg's <c>USES_SMOOTHING</c> (SMOOTH_BITMASK | SMOOTH_BITMASK_CARDINALS, code/__DEFINES/icon_smoothing.dm).</summary>
    public const int UsesSmoothing = (1 << 0) | (1 << 1);

    public bool TryGet(string typePath, out TypeDefaults defaults)
    {
        defaults = null;
        Type type = FindClass(typePath);
        if (type == null)
            return false;
        object instance;
        try
        {
            instance = Activator.CreateInstance(type);
        }
        catch (Exception)
        {
            return false; // a constructor that needs a world: not a plain-defaults class
        }
        if (instance == null)
            return false;
        float flags = Number(instance, "flags_1");
        // Only a type that smooths (SMOOTH_BITMASK or SMOOTH_BITMASK_CARDINALS) has a smoothed picture: a door's or a button's base_icon_state is just the stem of its states.
        bool smooths = ((int)Number(instance, "smoothing_flags") & UsesSmoothing) != 0;
        // tg's `if(greyscale_config && greyscale_colors) update_greyscale()` (atoms_initializing_EXPENSIVE.dm:128-129): init replaces the icon with the generated one.
        string gagsColors = Member(instance, "greyscale_colors") as string;
        string gagsConfig = Member(instance, "greyscale_config") is Type configType && !string.IsNullOrEmpty(gagsColors)
            ? configType.GetCustomAttribute<DmPathAttribute>(false)?.Path
            : null;
        defaults = new TypeDefaults
        {
            Path = typePath,
            Name = Member(instance, "name") as string,
            Icon = ResourcePath(Member(instance, "icon")),
            IconState = Member(instance, "icon_state") as string,
            PostInitIconState = Member(instance, "post_init_icon_state") as string is { Length: > 0 } postInit ? postInit : null,
            GreyscaleConfig = gagsConfig,
            GreyscaleColors = gagsConfig != null ? gagsColors : null,
            BaseIconState = smooths ? Member(instance, "base_icon_state") as string : null,
            Reinforced = Number(instance, "reinf") != 0f,
            Density = Number(instance, "density") != 0f,
            Opacity = Number(instance, "opacity") != 0f,
            Layer = NumberOr(instance, "layer", 3f),
            Plane = Number(instance, "plane"),
            PipingLayer = (int)NumberOr(instance, "piping_layer", 3f),
            PipeColor = Member(instance, "pipe_color") as string,
            UnderfloorAccessibility = (int)Number(instance, "underfloor_accessibility"),
            Dir = (byte)NumberOr(instance, "dir", ByondDirConst.South),
            PixelX = (short)Number(instance, "pixel_x"),
            PixelY = (short)Number(instance, "pixel_y"),
            Invisibility = Number(instance, "invisibility"),
            OnBorder = ((int)flags & OnBorder1) != 0,
            Light = LightOf(instance),
            RandomBaseType = (Member(instance, "random_basetype") as Type)?.GetCustomAttribute<DmPathAttribute>(false)?.Path,
            Sharp = Number(instance, "sharpness") != 0f,
            WeightClass = (int)Number(instance, "w_class"),
        };
        return true;
    }

    private readonly Dictionary<string, List<TypeDefaults>> _randomChoices = new();

    /// <summary>tg: <c>/obj/structure/sign/poster/randomise</c> (code/game/objects/effects/posters/poster.dm:187): the subtypes it may pick. Only the classes whose names start with the base's are looked at (a name scan, not the 46,000 attributes).</summary>
    public IReadOnlyList<TypeDefaults> RandomChoices(string basePath, IReadOnlyList<string> excludedPrefixes)
    {
        string key = basePath + "|" + string.Join("|", excludedPrefixes);
        if (_randomChoices.TryGetValue(key, out List<TypeDefaults> known))
            return known;
        var choices = new List<TypeDefaults>();
        string stem = basePath.Substring(1).Replace('/', '_');
        foreach (Assembly assembly in _assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                if (type.IsAbstract || !type.Name.StartsWith(stem + "_", StringComparison.Ordinal))
                    continue;
                string path = type.GetCustomAttribute<DmPathAttribute>(false)?.Path;
                if (path == null || !path.StartsWith(basePath + "/", StringComparison.Ordinal) || excludedPrefixes.Any(prefix => PropKinds.Is(path, prefix)))
                    continue;
                if (!TryGet(path, out TypeDefaults defaults) || string.IsNullOrEmpty(defaults.IconState) || defaults.PixelX != 0 || defaults.PixelY != 0)
                    continue;
                object instance = Activator.CreateInstance(type);
                if (Number(instance, "never_random") != 0f)
                    continue;
                choices.Add(defaults);
            }
        }
        choices.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        _randomChoices[key] = choices;
        return choices;
    }

    // tg: a light is on the atom while its light_system is not NO_LIGHT_SUPPORT (0) and light_power and light_range are not 0 (light-grid.md 1).
    // /obj/item/camera, for one, has a range and a power but no light system: it is no light, and the codec refuses a system outside 1..4.
    private static LightState LightOf(object instance)
    {
        float range = Number(instance, "light_range");
        float power = Number(instance, "light_power");
        int system = (int)NumberOr(instance, "light_system", 1f);
        if (range <= 0f || power == 0f || system < 1 || system > 4)
            return LightState.None;
        object color = Member(instance, "light_color");
        uint rgba = 0xFFFFFFFF;
        if (color is string text && text.Length >= 7 && text[0] == '#'
            && uint.TryParse(text.AsSpan(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            rgba = (rgb << 8) | 0xFF;
        return new LightState(Math.Min(range, Limits.MaxLightRange), Math.Clamp(power, -Limits.MaxLightPower, Limits.MaxLightPower), rgba, Number(instance, "light_on") != 0f,
            Math.Clamp(NumberOr(instance, "light_angle", 360f), 0f, 360f), (byte)NumberOr(instance, "light_dir", 0f), 1f, 0f, 0f, (byte)system);
    }

    private static object Member(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.CanRead)
            return property.GetValue(instance);
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(instance);
    }

    private static float Number(object instance, string name) => NumberOr(instance, name, 0f);

    private static float NumberOr(object instance, string name, float fallback)
    {
        object value = Member(instance, name);
        return value switch
        {
            float f => f,
            int i => i,
            double d => (float)d,
            byte b => b,
            short s => s,
            _ => fallback,
        };
    }

    private static string ResourcePath(object resource)
    {
        if (resource == null)
            return null;
        // DmResource renders as its path in text (rules 3.7).
        string text = resource.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}

/// <summary>
/// The fallback for a type whose class is not on main yet (before M3 P1's generated classes): the icon, state, density,
/// opacity, border flag and light that tg's source gives the type families MetaStation is made of, chosen by path prefix
/// (the most specific rule wins). A type no rule names gets <c>null</c> icon (the client draws its placeholder box).
/// This is data for the fake feed only; the real server replicates whatever the class says.
/// </summary>
public sealed class PathRuleTypeSource : ITypeSource
{
    private readonly bool _specificOnly;

    /// <param name="specificOnly">Answer only for the hidden families and the types a rule names, not with the plain
    /// placeholder default for any other object; the fake map puts such a source BEFORE the generated classes, because a
    /// rule states what a type looks like after tg's init (lights lit, border flags, hidden pipes), which the classes'
    /// pre-init defaults cannot say.</param>
    public PathRuleTypeSource(bool specificOnly = false) => _specificOnly = specificOnly;

    private readonly record struct Rule(string Prefix, string Icon, string State, bool Density, bool Opacity, float Layer, bool Border, string Base, short PixelX, short PixelY, LightState Light, float Invisibility);

    private static readonly List<Rule> Rules = BuildRules();

    private static Rule R(string prefix, string icon, string state, bool density = false, bool opacity = false, float layer = 3f, bool border = false, string baseState = null, short px = 0, short py = 0, LightState light = default, float invisibility = 0f)
        => new(prefix, icon, state, density, opacity, layer, border, baseState, px, py, light, invisibility);

    private static LightState Tube() => new(8f, 1f, 0xFFFFFFFF, true, 170f, 0, 1f, 0f, 0f, 1);

    private static LightState Bulb() => new(4f, 0.75f, 0xFFD9A0FF, true, 170f, 0, 1f, 0f, 0f, 1);

    private static List<Rule> BuildRules()
    {
        // tg: code/game/turfs/**, structures, doors, lights. Layers follow tg's defines (TURF_LAYER 2, OBJ_LAYER 3, ABOVE_OBJ 3.1, ...).
        var rules = new List<Rule>
        {
            R("/turf/open/space", "icons/turf/space.dmi", "space", layer: 1.9f),
            R("/turf/open/floor/plating", "icons/turf/floors.dmi", "plating", layer: 2f),
            R("/turf/open/floor/engine", "icons/turf/floors.dmi", "engine", layer: 2f),
            R("/turf/open/floor/iron/dark", "icons/turf/floors.dmi", "darkfull", layer: 2f),
            R("/turf/open/floor/iron/white", "icons/turf/floors.dmi", "white", layer: 2f),
            R("/turf/open/floor/iron", "icons/turf/floors.dmi", "floor", layer: 2f),
            R("/turf/open/floor/wood", "icons/turf/floors.dmi", "wood", layer: 2f),
            R("/turf/open/floor/carpet", "icons/turf/floors/carpet.dmi", "carpet-0", layer: 2f),
            R("/turf/open/floor/grass", "icons/turf/floors.dmi", "grass0", layer: 2f),
            R("/turf/open/floor", "icons/turf/floors.dmi", "floor", layer: 2f),
            R("/turf/open", "icons/turf/floors.dmi", "floor", layer: 2f),
            R("/turf/closed/wall/r_wall", "icons/turf/walls/reinforced_wall.dmi", "reinforced_wall-0", density: true, opacity: true, layer: 2f, baseState: "reinforced_wall"),
            R("/turf/closed/wall", "icons/turf/walls/wall.dmi", "wall-0", density: true, opacity: true, layer: 2f, baseState: "wall"),
            R("/turf/closed", "icons/turf/walls/wall.dmi", "wall-0", density: true, opacity: true, layer: 2f, baseState: "wall"),
            R("/obj/machinery/door/airlock/external", "icons/obj/doors/airlocks/external/external.dmi", "closed", density: true, opacity: true, layer: 3.3f),
            R("/obj/machinery/door/airlock/glass", "icons/obj/doors/airlocks/station/public.dmi", "closed", density: true, layer: 3.3f),
            R("/obj/machinery/door/airlock", "icons/obj/doors/airlocks/station/public.dmi", "closed", density: true, opacity: true, layer: 3.3f),
            R("/obj/machinery/door/window", "icons/obj/doors/windoor.dmi", "left", density: true, layer: 3.3f, border: true),
            R("/obj/machinery/door/poddoor/shutters", "icons/obj/doors/shutters.dmi", "closed", density: true, opacity: true, layer: 3.3f),
            R("/obj/machinery/door/poddoor", "icons/obj/doors/blastdoor.dmi", "closed", density: true, opacity: true, layer: 3.3f),
            R("/obj/machinery/door/firedoor", "icons/obj/doors/doorfire.dmi", "door_open", layer: 3.2f),
            R("/obj/structure/window/reinforced/fulltile", "icons/obj/smooth_structures/reinforced_window.dmi", "reinforced_window-0", density: true, layer: 3.2f, baseState: "reinforced_window"),
            R("/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window-0", density: true, layer: 3.2f, baseState: "window"),
            R("/obj/structure/window/reinforced", "icons/obj/structures.dmi", "rwindow", density: true, layer: 3.2f, border: true),
            R("/obj/structure/window", "icons/obj/structures.dmi", "window", density: true, layer: 3.2f, border: true),
            R("/obj/structure/grille", "icons/obj/structures.dmi", "grille", density: true, layer: 3.1f),
            // M3 I1d: the operating table is a table that does not smooth (tables_racks.dm:904, smoothing_flags = NONE): its own
            // picture, no table base (the table rule's "table" base drew "table-N" states its icon lacks, a stand-in box).
            R("/obj/structure/table/optable", "icons/obj/medical/surgery_table.dmi", "surgery_table", density: true, layer: 3.1f),
            R("/obj/structure/table/glass", "icons/obj/smooth_structures/glass_table.dmi", "glass_table-0", density: true, layer: 3.1f, baseState: "glass_table"),
            R("/obj/structure/table/wood", "icons/obj/smooth_structures/wood_table.dmi", "wood_table-0", density: true, layer: 3.1f, baseState: "wood_table"),
            R("/obj/structure/table/reinforced", "icons/obj/smooth_structures/reinforced_table.dmi", "reinforced_table-0", density: true, layer: 3.1f, baseState: "reinforced_table"),
            R("/obj/structure/table", "icons/obj/smooth_structures/table.dmi", "table-0", density: true, layer: 3.1f, baseState: "table"),
            R("/obj/structure/railing", "icons/obj/railings.dmi", "railing", density: true, layer: 3.2f, border: true),
            R("/obj/structure/plasticflaps", "icons/obj/structures.dmi", "plasticflaps", layer: 4f),
            R("/obj/machinery/light/small", "icons/obj/lighting.dmi", "bulb", layer: 3.3f, light: Bulb()),
            R("/obj/machinery/light", "icons/obj/lighting.dmi", "tube", layer: 3.3f, light: Tube()),
            R("/obj/structure/chair", "icons/obj/chairs.dmi", "chair", layer: 3f),
            R("/obj/structure/closet", "icons/obj/storage/closet.dmi", "generic", density: true, layer: 3.1f),
            R("/obj/structure/rack", "icons/obj/structures.dmi", "rack", density: true, layer: 3f),
            R("/obj/machinery/camera", "icons/obj/machines/camera.dmi", "camera", layer: 3.3f),
            R("/obj/machinery/airalarm", "icons/obj/machines/wallmounts.dmi", "alarm0", layer: 3.3f),
            R("/obj/machinery/firealarm", "icons/obj/machines/wallmounts.dmi", "fire0", layer: 3.3f),
            R("/obj/machinery/power/apc", "icons/obj/machines/wallmounts.dmi", "apc0", layer: 3.3f),
            R("/obj/machinery/light_switch", "icons/obj/machines/wallmounts.dmi", "light-on", layer: 3.3f),
            R("/obj/machinery/button", "icons/obj/machines/wallmounts.dmi", "button", layer: 3.3f),
            R("/obj/structure/sign", "icons/obj/signs.dmi", "bluecross", layer: 3.3f),
            R("/obj/structure/extinguisher_cabinet", "icons/obj/wallmounts.dmi", "extinguisher", layer: 3.3f),
            R("/obj/item/radio/intercom", "icons/obj/machines/wallmounts.dmi", "intercom", layer: 3.3f),
            R("/obj/machinery/computer", "icons/obj/machines/computer.dmi", "computer", density: true, layer: 3.1f),
            R("/obj/machinery/vending", "icons/obj/machines/vending.dmi", "generic", density: true, layer: 3.1f),
        };
        // Longest prefix first, so the most specific rule wins.
        rules.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        return rules;
    }

    /// <summary>The type families that never become entities (D115: tg hides them with <c>invisibility</c> above 25 or deletes them in init): pipes, cables, disposals, ducts, decals, landmarks, spawners, mapping helpers, areas' abstract objects.</summary>
    private static readonly string[] HiddenPrefixes =
    {
        "/obj/effect", "/obj/docking_port", "/obj/structure/sign/poster/contraband",
    };

    // tg's turf underfloor_accessibility (code/game/turfs/**): plating, space and the misc turfs 2 (UNDERFLOOR_INTERACTABLE), glass and catwalk floors 1 (UNDERFLOOR_VISIBLE), every other
    // turf the default 0 (UNDERFLOOR_HIDDEN). The generated classes have it as a var; this serves the types a class does not cover (tests).
    private static int AccessibilityOf(string typePath)
        => PropKinds.Is(typePath, "/turf/open/space") || PropKinds.Is(typePath, "/turf/open/floor/plating") || PropKinds.Is(typePath, "/turf/open/openspace") || PropKinds.Is(typePath, "/turf/open/misc")
            ? PropKinds.TileAccess.Interactable
            : PropKinds.Is(typePath, "/turf/open/floor/glass") || PropKinds.Is(typePath, "/turf/open/floor/catwalk_floor") ? PropKinds.TileAccess.Visible : PropKinds.TileAccess.Hidden;

    public bool TryGet(string typePath, out TypeDefaults defaults)
    {
        defaults = null;
        // M3 R5 (D122): the pipes, cables, disposals, lattice and the atmospherics machines are no longer hidden by their path: tg's undertile element hides some of them under an intact
        // floor tile, which the fake feed does from the turf (FakeMap, PropKinds.HasUndertile); the lattice and the machines show everywhere. What is still hidden here is what tg deletes
        // or hides for good (effects, landmarks, spawners, helpers, docking ports).
        foreach (string hidden in HiddenPrefixes)
        {
            if (typePath.StartsWith(hidden, StringComparison.Ordinal))
            {
                defaults = new TypeDefaults { Path = typePath, Invisibility = 101f };
                return true;
            }
        }
        foreach (Rule rule in Rules)
        {
            if (typePath == rule.Prefix || typePath.StartsWith(rule.Prefix + "/", StringComparison.Ordinal))
            {
                defaults = new TypeDefaults
                {
                    Path = typePath,
                    Icon = rule.Icon,
                    IconState = rule.State,
                    BaseIconState = rule.Base,
                    Density = rule.Density,
                    Opacity = rule.Opacity,
                    Layer = rule.Layer,
                    OnBorder = rule.Border,
                    PixelX = rule.PixelX,
                    PixelY = rule.PixelY,
                    Light = rule.Light,
                    Invisibility = rule.Invisibility,
                    UnderfloorAccessibility = typePath.StartsWith("/turf", StringComparison.Ordinal) ? AccessibilityOf(typePath) : 0,
                };
                ApplyDirectional(typePath, defaults);
                return true;
            }
        }
        // Items, machines and structures no rule names: a plain, non-dense object with no icon (the client's placeholder).
        if (!_specificOnly && (typePath.StartsWith("/obj", StringComparison.Ordinal) || typePath.StartsWith("/mob", StringComparison.Ordinal)))
        {
            defaults = new TypeDefaults { Path = typePath };
            ApplyDirectional(typePath, defaults);
            return true;
        }
        return false;
    }

    // tg: MAPPING_DIRECTIONAL_HELPERS(path, offset) (code/__DEFINES/directional.dm:32): /directional/north has dir NORTH and pixel_y = offset, south has
    // -offset, east pixel_x = offset, west -offset. The offset is the type's own, given where the helpers are made: tg's wall furniture 24 to 32
    // pixels toward the wall (the fire alarm 26, the air alarm 27, the extinguisher cabinet 29, the door button 24, a sign 32), a light, a camera,
    // a window, a windoor and a stool none, a sink -14 and a shower -16 (they stand against the wall). The generated class has the real value;
    // this table serves the types a class does not cover (tests).
    private static readonly (string Prefix, short Offset)[] DirectionalOffsets =
    {
        ("/obj/machinery/firealarm", 26), ("/obj/machinery/airalarm", 27), ("/obj/structure/extinguisher_cabinet", 29), ("/obj/machinery/newscaster", 30),
        ("/obj/machinery/light_switch", 26), ("/obj/machinery/button", 24), ("/obj/machinery/power/apc", 25), ("/obj/item/radio/intercom", 27),
        ("/obj/machinery/camera", 0), ("/obj/machinery/light", 0), ("/obj/structure/window", 0), ("/obj/machinery/door/window", 0), ("/obj/machinery/door/firedoor", 0),
        ("/obj/structure/chair", 0), ("/obj/structure/steps", 0), ("/obj/structure/sink", -14), ("/obj/machinery/shower", -16),
    };

    private static void ApplyDirectional(string typePath, TypeDefaults defaults)
    {
        int at = typePath.LastIndexOf("/directional/", StringComparison.Ordinal);
        if (at < 0)
            return;
        short offset = 32;
        foreach ((string prefix, short value) in DirectionalOffsets)
        {
            if (PropKinds.Is(typePath, prefix))
            {
                offset = value;
                break;
            }
        }
        switch (typePath.Substring(at + "/directional/".Length))
        {
            case "north": defaults.Dir = ByondDirConst.North; defaults.PixelY = offset; break;
            case "south": defaults.Dir = ByondDirConst.South; defaults.PixelY = (short)-offset; break;
            case "east": defaults.Dir = ByondDirConst.East; defaults.PixelX = offset; break;
            case "west": defaults.Dir = ByondDirConst.West; defaults.PixelX = (short)-offset; break;
        }
    }
}
