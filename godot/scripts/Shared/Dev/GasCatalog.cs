using System.Linq;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Dev;

// The dev menu's gas tools (the owner, 2026-10-13: "spawn, delete and manage gases, with their icons"). Engine-free. tg's
// gases are the subtypes of /datum/gas (gas_types.dm); each class's constructor sets tg's id, name, overlay state in
// icons/effects/atmospherics.dmi, primary colour and rarity, and touches no world, so the client reads them as the server's
// GLOB.meta_gas_info does.

/// <summary>One of tg's gases.</summary>
/// <param name="Id">tg's id (<c>GAS_PLASMA</c> "plasma"): what the <c>air</c> dev command takes.</param>
/// <param name="Name">tg's name ("Plasma").</param>
/// <param name="Path">The DM path (<c>/datum/gas/plasma</c>).</param>
/// <param name="Overlay">The gas's icon state in <c>icons/effects/atmospherics.dmi</c> (tg's <c>gas_overlay</c>), or null for an invisible gas.</param>
/// <param name="Rgb">tg's <c>primary_color</c> as 0xRRGGBB (white when it has none).</param>
/// <param name="Rarity">tg's <c>rarity</c> (the common gases first).</param>
public sealed record GasEntry(string Id, string Name, string Path, string Overlay, uint Rgb, float Rarity);

/// <summary>tg's gases, the most common first.</summary>
public static class GasCatalog
{
    /// <summary>tg's gas sprites (gas_types.dm: <c>icon = 'icons/effects/atmospherics.dmi'</c> of /obj/effect/overlay/gas).</summary>
    public const string Icon = "icons/effects/atmospherics.dmi";

    private static IReadOnlyList<GasEntry> _all;
    private static readonly object Gate = new();

    /// <summary>Every concrete /datum/gas subtype, by rarity (the most common first), then by name.</summary>
    public static IReadOnlyList<GasEntry> All
    {
        get
        {
            lock (Gate)
                return _all ??= Read();
        }
    }

    private static List<GasEntry> Read()
    {
        var list = new List<GasEntry>();
        Type[] types;
        try
        {
            types = typeof(datum_gas).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
        }
        foreach (Type type in types)
        {
            if (type.IsAbstract || type == typeof(datum_gas) || !typeof(datum_gas).IsAssignableFrom(type)) continue;
            string path = type.GetCustomAttribute<DmPathAttribute>(false)?.Path;
            if (path == null) continue;
            datum_gas gas;
            try
            {
                gas = (datum_gas)Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                continue;
            }
            if (gas?.id is not string id || id.Length == 0) continue;
            list.Add(new GasEntry(id, string.IsNullOrEmpty(gas.name) ? id : gas.name, path,
                string.IsNullOrEmpty(gas.gas_overlay) ? null : gas.gas_overlay, ParseRgb(gas.primary_color as string), gas.rarity));
        }
        list.Sort(static (a, b) => a.Rarity != b.Rarity ? b.Rarity.CompareTo(a.Rarity) : string.CompareOrdinal(a.Name, b.Name));
        return list;
    }

    /// <summary>A gas by tg's id, or null.</summary>
    public static GasEntry Find(string id)
    {
        foreach (GasEntry gas in All)
        {
            if (gas.Id == id) return gas;
        }
        return null;
    }

    // "#0000ff" or "#00f" as 0xRRGGBB; white for anything else.
    internal static uint ParseRgb(string color)
    {
        if (string.IsNullOrEmpty(color) || color[0] != '#') return 0xFFFFFF;
        string hex = color.Substring(1);
        if (hex.Length == 3) hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length >= 6 && uint.TryParse(hex.AsSpan(0, 6), System.Globalization.NumberStyles.HexNumber, null, out uint rgb)) return rgb;
        return 0xFFFFFF;
    }
}
