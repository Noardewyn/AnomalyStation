using System.Linq;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Models;

/// <summary>Nested source overlays, including independent slots that happen to display the same state.</summary>
public static class ModelOverlays
{
    /// <summary>The source overlay's own colour, including its alpha, without sampling its sprite.</summary>
    public static uint Tint(IAppearanceSource source, AppearanceEntry root, string state)
    {
        foreach (var overlay in All(source, root))
            if (source.ResolveString(overlay.Entry.IconState) == state)
                return overlay.Entry.Rgba;
        return ModelColors.NoTint;
    }

    public static IEnumerable<(TableRef Reference, AppearanceEntry Entry)> All(IAppearanceSource source, AppearanceEntry root)
    {
        var seen = new HashSet<TableRef>();
        // the root's underlays that show (tg's multilayer cable hub: a node per cable layer, alpha 0 while the layer is off), then its overlays
        foreach (TableRef under in root.Underlays)
        {
            if (seen.Add(under) && source.TryGetAppearance(under, out AppearanceEntry entry) && entry.Alpha > 0)
                yield return (under, entry);
        }
        var pending = new Stack<TableRef>(root.Overlays.AsEnumerable().Reverse());
        while (pending.Count > 0)
        {
            TableRef reference = pending.Pop();
            if (!seen.Add(reference) || !source.TryGetAppearance(reference, out AppearanceEntry entry))
                continue;
            yield return (reference, entry);
            foreach (TableRef child in entry.Overlays.AsEnumerable().Reverse())
                pending.Push(child);
        }
    }

    public static bool HasSlot(IAppearanceSource source, AppearanceEntry root, IReadOnlyCollection<string> states, short pixelW)
        => All(source, root).Any(o => o.Entry.PixelW == pixelW && states.Contains(source.ResolveString(o.Entry.IconState)));
}
