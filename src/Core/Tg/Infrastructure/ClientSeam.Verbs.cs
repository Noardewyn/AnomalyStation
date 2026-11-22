using System.Linq;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 78 (silicons SI4, D326): the stat panel's command verbs. tg declares a verb with GAME_VERB, which makes a
// /datum/verb_metadata of it (its name, category, verb and body paths, __DEFINES/verb.dm); BYOND lists a mob's verbs that have a
// category in the stat panel's tabs (the AI's "AI Commands"): the verbs its type declares and the ones tg gives it at run time
// (add_verb, SSverbs' assign_verb: the mob's verbs list). A verb clicked there runs with usr the mob, and its proc hands itself to
// SSverbs' invoke_verb (__DEFINES/verb.dm:12). The port's client draws the tabs from the list the replicator sends (CommandVerbs.For)
// and sends the clicked verb's path back (ClientSeam.RunVerb).

/// <summary>One command verb: tg's metadata of it.</summary>
public sealed record CommandVerb(string Category, string Name, string Path, Type Owner);

/// <summary>
/// The command verbs tg declares (its /datum/verb_metadata types) that a player can run from the stat panel: a category, no arguments
/// the verb itself takes (a context verb's target comes from a right click, not the panel), the verb proc and its body translated on
/// its owner type. Read once, from the types' initial values (no world needed).
/// </summary>
public static class CommandVerbs
{
    private static readonly Lazy<Dictionary<string, CommandVerb>> s_byPath = new(Load);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<CommandVerb>> s_byType = new();

    /// <summary>
    /// The command verbs of <paramref name="holder"/>: those its type (and its ancestors) declares and those tg gave it at run time
    /// (its <see cref="mob.verbs"/>), by category then name.
    /// </summary>
    public static IReadOnlyList<CommandVerb> For(mob holder)
    {
        if (holder == null)
            return Array.Empty<CommandVerb>();
        Type type = holder.GetType();
        IReadOnlyList<CommandVerb> declared = s_byType.GetOrAdd(type, static t => s_byPath.Value.Values
            .Where(v => v.Path.Contains("/verb/", StringComparison.Ordinal) && v.Owner.IsAssignableFrom(t))
            .OrderBy(v => v.Category, StringComparer.Ordinal).ThenBy(v => v.Name, StringComparer.Ordinal).ToList());
        if (holder.verbs.len == 0)
            return declared;
        var all = new List<CommandVerb>(declared);
        foreach (object item in Dm.each(holder.verbs))
        {
            if (item is string path && s_byPath.Value.TryGetValue(path, out CommandVerb verb) && verb.Owner.IsAssignableFrom(type) && !all.Contains(verb))
                all.Add(verb);
        }
        if (all.Count == declared.Count)
            return declared;
        return all.OrderBy(v => v.Category, StringComparer.Ordinal).ThenBy(v => v.Name, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, CommandVerb> Load()
    {
        var verbs = new Dictionary<string, CommandVerb>(StringComparer.Ordinal);
        foreach (Type type in typeof(datum_verb_metadata).Assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(datum_verb_metadata)) || type.IsAbstract)
                continue;
            string category = Dm.initial(type, (datum_verb_metadata m) => m.category) as string;
            string name = Dm.initial(type, (datum_verb_metadata m) => m.name) as string;
            string path = Dm.initial(type, (datum_verb_metadata m) => m.verb_path) as string;
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                continue;
            // GAME_VERB's /owner/verb/x, GAME_VERB_PROC's /owner/proc/x
            int at = path.IndexOf("/verb/", StringComparison.Ordinal);
            int length = "/verb/".Length;
            if (at <= 0)
            {
                at = path.IndexOf("/proc/", StringComparison.Ordinal);
                length = "/proc/".Length;
            }
            if (at <= 0)
                continue;
            Type owner = DmTypes.FromPath(path.Substring(0, at));
            if (owner == null || !typeof(mob).IsAssignableFrom(owner))
                continue; // the stat panel of a mob: its own verbs (a client's are the port's menus)
            // the verb proc, `X(params object[] __extra)`: a context verb (`X(atom __context_target = null, params object[] __extra)`)
            // is not listed
            MethodInfo proc = owner.GetMethod(path.Substring(at + length), BindingFlags.Public | BindingFlags.Instance, new[] { typeof(object[]) });
            if (proc == null || proc.GetParameters().Length != 1)
                continue;
            // and its body (/owner/proc/__gvb_x), which SSverbs.invoke_verb calls: a verb whose body is not translated is not listed
            string body = Dm.initial(type, (datum_verb_metadata m) => m.body_path) as string;
            int bodyAt = body?.IndexOf("/proc/", StringComparison.Ordinal) ?? -1;
            if (bodyAt <= 0 || owner.GetMember(body.Substring(bodyAt + "/proc/".Length), MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance).Length == 0)
                continue;
            verbs[path] = new CommandVerb(category, name, path, owner);
        }
        return verbs;
    }
}

public static partial class ClientSeam
{
    /// <summary>
    /// Version 78: the player clicked the command verb <paramref name="path"/> in the stat panel: BYOND runs the verb with <c>usr</c> the
    /// client's mob, and the verb hands itself to SSverbs (INVOKE_ASYNC(SSverbs, invoke_verb, src, path, args, usr), __DEFINES/verb.dm:12).
    /// A verb the mob does not have (its type changed since, a stale click) is a click on nothing.
    /// </summary>
    public static void RunVerb(client player, string path)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob == null || string.IsNullOrEmpty(path) || GlobalProcs.SSverbs == null)
            return;
        mob holder = player.mob;
        if (!CommandVerbs.For(holder).Any(v => string.Equals(v.Path, path, StringComparison.Ordinal)))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = holder;
            Waiting.Detach(GlobalProcs.SSverbs.invoke_verb(holder, path, Dm.list(), holder));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// Version 78 (DV387): the player closed the popup window <paramref name="name"/>: tg's on-close command, the client's native verb
    /// <c>handle_popup_close</c> (map_popups.dm:131), which clears the window's map. A window the client does not have open is ignored.
    /// </summary>
    public static void ClosePopup(client player, string name)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || string.IsNullOrEmpty(name) || !Dm.truthy(player.dm_popups[name]))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // winshow(0) is the player's own closing: the window leaves the client's list, then BYOND runs the on-close command
            Dm.remove(ref player.dm_popups, name);
            player.dm_popups_serial++;
            player.handle_popup_close(name);
        }
        finally
        {
            world.usr = caller_usr;
        }
    }
}
