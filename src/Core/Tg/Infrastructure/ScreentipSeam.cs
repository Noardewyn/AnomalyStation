using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// Owner: the M4a contracts step (complete; frozen: docs/ai/specs/m4a-ownership.md). D125, docs/ai/specs/interaction.md section 6.
// A host seam, not tg code: it stands for the maptext tg's on_mouse_enter writes into the screentip screen object
// (game/atom/_atom.dm:868-960), which the port replaces by a structured message (DEVIATIONS.md, the context-hint row).

/// <summary>
/// One entry of tg's screentip context as the host receives it: tg's context key (<c>SCREENTIP_CONTEXT_*</c>,
/// <c>code/__DEFINES/screentips.dm</c>) split into the click it names, and tg's label.
/// </summary>
/// <param name="Button">tg's button name: <c>LEFT_CLICK</c> ("left") or <c>RIGHT_CLICK</c> ("right").</param>
/// <param name="Shift">The key names Shift.</param>
/// <param name="Ctrl">The key names Ctrl.</param>
/// <param name="Alt">The key names Alt.</param>
/// <param name="Label">tg's label text as the context list holds it ("Open", "Weld shut").</param>
public readonly record struct ScreentipHint(string Button, bool Shift, bool Ctrl, bool Alt, string Label);

/// <summary>Where the screentips of a world go (the server's: a <c>ContextHints</c> message to the viewer's session, M4a P6). Write-only, like the presentation sinks (presentation-seam.md section 1).</summary>
public interface IScreentipSink
{
    /// <summary>
    /// tg built the screentip of <paramref name="target"/> for <paramref name="viewer"/>: tg's <c>used_name</c>
    /// (<paramref name="name"/>; null when tg shows none: screentips off or <c>NO_SCREENTIPS_1</c>) and the context entries in
    /// tg's order of lines (<see cref="ScreentipSeam.ContextKeys"/>). Called during the tick, in the simulation's order.
    /// </summary>
    void Screentip(client viewer, atom target, string name, IReadOnlyList<ScreentipHint> hints);
}

/// <summary>
/// The screentip seam (D125): tg's <c>on_mouse_enter</c> (M4a P1's translation) gathers tg's context list (<c>add_context</c>,
/// the item and target signals, the contextual screentip elements) exactly as tg does and, where tg composes the maptext of
/// <c>active_hud.screentip_text</c>, calls <see cref="Publish"/> with the name and the list. The world's sink (none by
/// default: nothing happens) receives them.
/// </summary>
public static class ScreentipSeam
{
    /// <summary>tg's context keys in tg's order of screentip lines (<c>_atom.dm:908-946</c>): LMB, RMB, Ctrl-LMB, Ctrl-RMB, Alt-LMB, Alt-RMB, Shift-LMB, Ctrl-Shift-LMB.</summary>
    public static readonly System.Collections.ObjectModel.ReadOnlyCollection<string> ContextKeys = Array.AsReadOnly(new[]
    {
        SCREENTIP_CONTEXT_LMB, SCREENTIP_CONTEXT_RMB, SCREENTIP_CONTEXT_CTRL_LMB, SCREENTIP_CONTEXT_CTRL_RMB,
        SCREENTIP_CONTEXT_ALT_LMB, SCREENTIP_CONTEXT_ALT_RMB, SCREENTIP_CONTEXT_SHIFT_LMB, SCREENTIP_CONTEXT_CTRL_SHIFT_LMB,
    });

    /// <summary>The world's sink (null: none).</summary>
    public static IScreentipSink SinkOf(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Holder.Of(world).Sink;
    }

    /// <summary>Installs the world's sink (the host, before the world runs; null removes it).</summary>
    public static void Install(World world, IScreentipSink sink)
    {
        ArgumentNullException.ThrowIfNull(world);
        Holder.Of(world).Sink = sink;
    }

    /// <summary>
    /// The click a tg context key names (<see cref="ContextKeys"/>): false for any other text.
    /// </summary>
    public static bool TryKey(string key, out string button, out bool shift, out bool ctrl, out bool alt)
    {
        button = null;
        shift = ctrl = alt = false;
        switch (key)
        {
            case SCREENTIP_CONTEXT_LMB: button = LEFT_CLICK; return true;
            case SCREENTIP_CONTEXT_RMB: button = RIGHT_CLICK; return true;
            case SCREENTIP_CONTEXT_CTRL_LMB: button = LEFT_CLICK; ctrl = true; return true;
            case SCREENTIP_CONTEXT_CTRL_RMB: button = RIGHT_CLICK; ctrl = true; return true;
            case SCREENTIP_CONTEXT_ALT_LMB: button = LEFT_CLICK; alt = true; return true;
            case SCREENTIP_CONTEXT_ALT_RMB: button = RIGHT_CLICK; alt = true; return true;
            case SCREENTIP_CONTEXT_SHIFT_LMB: button = LEFT_CLICK; shift = true; return true;
            case SCREENTIP_CONTEXT_CTRL_SHIFT_LMB: button = LEFT_CLICK; ctrl = true; shift = true; return true;
            default: return false;
        }
    }

    /// <summary>
    /// tg's screentip of <paramref name="target"/> for <paramref name="viewer"/>: <paramref name="name"/> (tg's
    /// <c>used_name</c>, or null for no screentip) and tg's <paramref name="context"/> list (key to label; null or empty: a
    /// screentip without context). Takes the keys tg shows, in tg's order of lines; a label that is not text, or a key tg
    /// does not show, is left out as tg's <c>build_context</c> leaves it out. Does nothing without a sink, for a null or
    /// deleted viewer or target, or for an unregistered one (presentation-seam.md rule 4).
    /// </summary>
    public static void Publish(client viewer, atom target, string name, DmList context)
    {
        if (viewer == null || target == null || viewer.gc_destroyed != null || target.gc_destroyed != null)
            return;
        if (viewer.dm_id == 0 || target.dm_id == 0)
            return;
        IScreentipSink sink = Holder.Of(World.Current).Sink;
        if (sink == null)
            return;
        var hints = new List<ScreentipHint>();
        if (context != null)
        {
            foreach (string key in ContextKeys)
            {
                if (!Dm.@in(key, context))
                    continue;
                if (context[key] is not string label)
                    continue;
                TryKey(key, out string button, out bool shift, out bool ctrl, out bool alt);
                hints.Add(new ScreentipHint(button, shift, ctrl, alt, label));
            }
        }
        sink.Screentip(viewer, target, name, hints);
    }

    // The per-world sink (World.GetExtension: no static state).
    private sealed class Holder
    {
        internal IScreentipSink Sink;

        internal static Holder Of(World world) => world.GetExtension(static _ => new Holder());
    }
}
