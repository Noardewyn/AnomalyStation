using System.Globalization;
using System.Linq;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// tg's gas string, as a turf's <c>initial_gas_mix</c> holds it (<c>SSair.parse_gas_string</c>): the moles of each gas on the tile by tg's id, and
/// the temperature, <c>o2=22;n2=82;TEMP=293.15</c>. The map editor's gas window edits one.
/// </summary>
public sealed class GasMix
{
    /// <summary>tg's <c>OPENTURF_DEFAULT_ATMOS</c>: a room's normal air.</summary>
    public const string NormalAir = "o2=22;n2=82;TEMP=293.15";

    /// <summary>tg's <c>AIRLESS_ATMOS</c>: no gas, at the cosmic background's temperature.</summary>
    public const string Vacuum = "TEMP=2.7";

    private const double CellVolume = 2500; // tg's CELL_VOLUME, litres
    private const double GasConstant = 8.31; // tg's R_IDEAL_GAS_EQUATION

    /// <summary>The moles of each gas, by tg's id, in the order the string names them.</summary>
    public List<(string Id, double Moles)> Gases { get; } = new();

    /// <summary>The temperature in kelvin (tg's T20C when the string names none).</summary>
    public double Kelvin { get; set; } = 293.15;

    /// <summary>The pressure of the mix on one tile, in kPa (tg's <c>return_pressure</c>: n R T / V).</summary>
    public double Kpa => Gases.Sum(g => g.Moles) * GasConstant * Kelvin / CellVolume;

    /// <summary>The moles of one gas (0 when the mix has none).</summary>
    public double this[string id]
    {
        get => Gases.FirstOrDefault(g => g.Id == id).Moles;
        set
        {
            int at = Gases.FindIndex(g => g.Id == id);
            if (at >= 0)
                Gases[at] = (id, value);
            else
                Gases.Add((id, value));
        }
    }

    /// <summary>
    /// The mix of a gas string; null for a text that is not one (tg's named atmospheres, such as a planet's, are no gas string: they are kept
    /// as they are).
    /// </summary>
    public static GasMix Parse(string text)
    {
        var mix = new GasMix();
        foreach (string part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0 || !double.TryParse(part.AsSpan(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
                return null;
            string key = part.Substring(0, eq).Trim();
            if (key == "TEMP")
                mix.Kelvin = value;
            else
                mix[key] = value;
        }
        return mix;
    }

    /// <summary>The mix as tg's gas string: its gases that have any moles, then the temperature.</summary>
    public override string ToString()
        => string.Join(';', Gases.Where(g => g.Moles > 0).Select(g => g.Id + "=" + Number(g.Moles)).Append("TEMP=" + Number(Kelvin)));

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
