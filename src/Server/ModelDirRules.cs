using System.IO;
using System.Linq;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server;

/// <summary>
/// The server's share of the 3D model catalog (<c>godot/assets/models/models.json</c>, docs/ai/specs/models.md; DV56): the rules of the way a modelled
/// thing faces, which the server owns because the way is the thing's <c>dir</c>. For the things whose icon (and icon state) an entry draws: a push, a
/// bump or a pull does not turn them (<c>set_dir_on_move</c> FALSE, as tg's windows keep theirs; the owner, 2026-10-20: a 3D thing keeps its facing; an
/// entry's <c>"dir": { "keepOnPush": false }</c> opts out), its <c>"dir"</c> object's <c>awayFromWall</c> makes them facing away from the wall behind
/// them, and a free-standing model (rule single, dir4, dir8, closet, crate, console; <c>"rotate": false</c> opts out) gets tg's simple_rotation, its
/// Alt-click "Rotate left" and "Rotate right" (the radial's entries), unless it has an Alt-click of its own. The rest of an entry is the client's.
/// </summary>
public sealed class ModelDirRules
{
    /// <summary>The catalog's path in the repository, looked for upward from the working folder and the server's own.</summary>
    public const string DefaultCatalog = "godot/assets/models/models.json";

    /// <summary>One entry's rule.</summary>
    public sealed record Rule(string Id, string[] Icons, string[] ExceptIcons, string[] States, bool AwayFromWall, bool KeepOnPush, bool Rotate = false)
    {
        /// <summary>True when the entry draws <paramref name="icon"/> in <paramref name="state"/> (as the client's catalog matches).</summary>
        public bool Draws(string icon, string state)
            => !string.IsNullOrEmpty(icon) && !ExceptIcons.Contains(icon, StringComparer.Ordinal)
               && Icons.Any(i => i.EndsWith('/') ? icon.StartsWith(i, StringComparison.Ordinal)
                   : i.EndsWith('*') ? icon.StartsWith(i[..^1], StringComparison.Ordinal) : icon == i)
               && (States.Length == 0 || States.Contains(state ?? string.Empty, StringComparer.Ordinal));
    }

    /// <summary>The rules of the catalog's entries, in its order.</summary>
    public IReadOnlyList<Rule> Rules { get; }

    private ModelDirRules(List<Rule> rules) => Rules = rules;

    /// <summary>No rules (no catalog).</summary>
    public static ModelDirRules None { get; } = new(new List<Rule>());

    /// <summary>The catalog's file: <paramref name="path"/> when given, else <see cref="DefaultCatalog"/> found upward; null when there is none.</summary>
    public static string Locate(string path)
    {
        if (!string.IsNullOrEmpty(path))
            return File.Exists(path) ? Path.GetFullPath(path) : null;
        return FindUp(DefaultCatalog);
    }

    /// <summary>A repository file: the first <paramref name="relative"/> up from the working directory, then up from the server's own.</summary>
    public static string FindUp(string relative)
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (string dir = Path.GetFullPath(start); dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }

    /// <summary>The rules of a catalog's text (its comments and trailing commas allowed, as the client reads it).</summary>
    public static ModelDirRules Parse(string json)
    {
        var rules = new List<Rule>();
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (JsonElement e in document.RootElement.GetProperty("models").EnumerateArray())
        {
            bool hasDir = e.TryGetProperty("dir", out JsonElement dir);
            string kind = e.TryGetProperty("rule", out JsonElement r) ? r.GetString() ?? string.Empty : string.Empty;
            rules.Add(new Rule(
                e.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty,
                Strings(e, "icons"), Strings(e, "exceptIcons"), Strings(e, "states"),
                hasDir && dir.TryGetProperty("awayFromWall", out JsonElement away) && away.GetBoolean(),
                !(hasDir && dir.TryGetProperty("keepOnPush", out JsonElement keep) && !keep.GetBoolean()),
                e.TryGetProperty("rotate", out JsonElement rotate) ? rotate.GetBoolean() : RotatableRules.Contains(kind, StringComparer.OrdinalIgnoreCase)));
        }
        return new ModelDirRules(rules);
    }

    // The catalog rules of free-standing things (not a wall fixture, an overlay, a door or a table that joins its neighbours).
    private static readonly string[] RotatableRules = { "single", "dir4", "dir8", "closet", "crate", "console" };

    private static string[] Strings(JsonElement e, string name)
        => e.TryGetProperty(name, out JsonElement a) ? a.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray() : Array.Empty<string>();

    /// <summary>The rule of a thing, by its icon and icon state; null when no entry with a rule draws it.</summary>
    public Rule Of(atom a)
    {
        if (Rules.Count == 0 || a is not atom_movable)
            return null;
        // a GAGS icon is its key, text (DV19: "gags:<config>|<colours>", a canister's), as the client's catalog names it
        string icon = a.icon is DmResource resource ? resource.Path : a.icon as string;
        string state = a.icon_state as string;
        foreach (Rule rule in Rules)
        {
            if (rule.Draws(icon, state))
                return rule;
        }
        return null;
    }

    /// <summary>
    /// DEVIATION(DV56): applies a rule to a thing that tg just initialized (on the map or made later): it keeps its dir when pushed or pulled (a
    /// vehicle, driven by its dir, does not), it faces away from the wall behind it, north first, then west, east and south (south, tg's own, when no wall
    /// touches it) unless its map says the way it faces (DV508), and a free-standing one can be turned by hand (tg's simple_rotation: Alt-click, the radial's "Rotate left/right"; tg's anchored check
    /// stays, so a thing bolted down says "need to unwrench!").
    /// </summary>
    public void ApplyTo(atom a)
    {
        Rule rule = Of(a);
        if (rule == null || a is not atom_movable movable || a is obj_vehicle)
            return;
        if (rule.KeepOnPush)
            movable.set_dir_on_move = Dm.FALSE;
        // DEVIATION(DV508): not a thing whose map says the way it faces (the mapper turned it, or the random spawner that made it)
        if (rule.AwayFromWall && movable.loc is turf && !MapSeam.HasMapDir(movable))
            Waiting.NoSleep(movable.setDir(AwayFromWall(movable)));
        if (!rule.Rotate || a is not obj)
            return;
        // A thing keeps the Alt-clicks it answers itself; one whose Alt-click is its own (a canister's: the held tank comes out) turns by Alt-right-click
        // alone, a quarter clockwise a click.
        bool left = OwnsAltClick(a, Defines.COMSIG_CLICK_ALT, nameof(atom.click_alt)), right = OwnsAltClick(a, Defines.COMSIG_CLICK_ALT_SECONDARY, nameof(atom.click_alt_secondary));
        if (!left && !right)
            Waiting.NoSleep(a.AddElement(typeof(datum_element_simple_rotation), Defines.NONE));
        else if (!right)
            Waiting.NoSleep(a.AddElement(typeof(RotateRightOnly), Defines.NONE));
    }

    // A handler on the signal (a chair's own simple_rotation, a window's), or a ported click proc of its type (a closet's lock, once ported, then wins over
    // the turn).
    private static bool OwnsAltClick(atom a, string signal, string proc)
        => Dm.truthy(a._listen_lookup?[signal]) || Overridden(a.GetType(), proc);

    private static bool Overridden(Type type, string name)
        => type.GetMethods().Where(m => m.Name == name).Any(m => m.DeclaringType != m.GetBaseDefinition().DeclaringType);

    private static float AwayFromWall(atom a)
    {
        if (get_step(a, Dm.NORTH) is turf_closed)
            return Dm.SOUTH;
        if (get_step(a, Dm.WEST) is turf_closed)
            return Dm.EAST;
        if (get_step(a, Dm.EAST) is turf_closed)
            return Dm.WEST;
        if (get_step(a, Dm.SOUTH) is turf_closed)
            return Dm.NORTH;
        return Dm.SOUTH;
    }

    /// <summary>Starts applying the rules to every atom tg initializes from now on (tg's <c>COMSIG_GLOB_ATOM_AFTER_POST_INIT</c> on SSdcs); call in the world.</summary>
    public void Listen()
    {
        if (Rules.Count == 0)
            return;
        var listener = Dm.@new<ModelDirListener>();
        listener.Rules = this;
        listener.RegisterSignal(SSdcs, Defines.COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(ModelDirListener.on_atom_initialized));
    }
}

/// <summary>
/// DEVIATION(DV56): tg's simple_rotation on Alt-right-click only, for a thing whose Alt-click is its own (a canister ejects its tank): "Rotate right", a
/// quarter clockwise a click, so four clicks go round.
/// </summary>
internal class RotateRightOnly : datum_element_simple_rotation
{
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!Dm.eq(dot, Defines.ELEMENT_INCOMPATIBLE))
            UnregisterSignal(target, Defines.COMSIG_CLICK_ALT);
        return dot;
    }

    // only its own entry: the thing's own Alt-click entry stays (a canister's tank)
    public override object on_requesting_context_from_item(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (context == null || !Dm.truthy(Waiting.NoSleep(can_be_rotated(user, source as obj, Defines.ROTATION_CLOCKWISE, silent: Dm.TRUE))))
            return Defines.NONE;
        context[Defines.SCREENTIP_CONTEXT_ALT_RMB] = "Rotate right";
        return Defines.CONTEXTUAL_SCREENTIP_SET;
    }
}

/// <summary>The listener of tg's global atom init signal for <see cref="ModelDirRules"/>: a plain datum with no DM path of its own (as ToyListener).</summary>
internal class ModelDirListener : datum
{
    public ModelDirRules Rules;

    [SignalHandler]
    public virtual object on_atom_initialized(datum source = null, object created = null)
    {
        // tg's InitAtom sends the signal for an atom its Initialize qdeleted too (an emergency closet's 1%, utility_closets.dm:30)
        if (created is atom a && !QDELETED(a))
            Rules.ApplyTo(a);
        return Defines.NONE;
    }
}
