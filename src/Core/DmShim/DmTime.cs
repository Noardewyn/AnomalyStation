using System.Globalization;
using System.Text;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

/// <summary>
/// <c>time2text</c> (BYOND ref <c>/proc/time2text</c>) without a wall clock or a host time zone (D31, DV3): timestamps
/// are deciseconds since 00:00 UTC, January 1, 2000; the "current date" of a time-of-day timestamp is the date of
/// <c>world.realtime</c> (the host-set <see cref="DmWorldState.RealtimeStart"/> plus <c>world.time</c>), and the
/// world's local time is UTC. Owner: values.
/// </summary>
internal static class DmTime
{
    private const string DefaultFormat = "DDD MMM DD hh:mm:ss YYYY";
    private const double DecisecondsPerDay = 864000d;
    private static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static string Format(float timestamp, string format, float? timezone)
    {
        double ds = float.IsNaN(timestamp) || float.IsInfinity(timestamp) ? 0d : timestamp;
        if (ds >= 0d && ds < DecisecondsPerDay) ds += CurrentDayStart();
        ds = Math.Clamp(ds, -6.0e11, 2.5e12); // years 100 to 9900: DateTime's range
        DateTime time = Epoch.AddMilliseconds(ds * 100d);
        if (timezone.HasValue && !float.IsNaN(timezone.Value)) time = time.AddHours(timezone.Value);
        format ??= DefaultFormat;
        // The fields, longest first where one is a prefix of another (BYOND ref: "Anything else in the format string
        // is inserted directly into the output").
        string[] fields = { "YYYY", "YY", "Month", "MMM", "MM", "Day", "DDD", "DD", "hh", "mm", "ss" };
        DateTimeFormatInfo names = CultureInfo.InvariantCulture.DateTimeFormat;
        var sb = new StringBuilder(format.Length + 16);
        for (int i = 0; i < format.Length;)
        {
            string field = null;
            foreach (string f in fields)
            {
                if (string.CompareOrdinal(format, i, f, 0, f.Length) == 0)
                {
                    field = f;
                    break;
                }
            }
            if (field == null)
            {
                sb.Append(format[i++]);
                continue;
            }
            switch (field)
            {
                case "YYYY": sb.Append(time.Year.ToString("0000", CultureInfo.InvariantCulture)); break;
                case "YY": sb.Append((time.Year % 100).ToString("00", CultureInfo.InvariantCulture)); break;
                case "Month": sb.Append(names.GetMonthName(time.Month)); break;
                case "MMM": sb.Append(names.GetAbbreviatedMonthName(time.Month)); break;
                case "MM": sb.Append(time.Month.ToString("00", CultureInfo.InvariantCulture)); break;
                case "Day": sb.Append(names.GetDayName(time.DayOfWeek)); break;
                case "DDD": sb.Append(names.GetAbbreviatedDayName(time.DayOfWeek)); break;
                case "DD": sb.Append(time.Day.ToString("00", CultureInfo.InvariantCulture)); break;
                case "hh": sb.Append(time.Hour.ToString("00", CultureInfo.InvariantCulture)); break;
                case "mm": sb.Append(time.Minute.ToString("00", CultureInfo.InvariantCulture)); break;
                case "ss": sb.Append(time.Second.ToString("00", CultureInfo.InvariantCulture)); break;
            }
            i += field.Length;
        }
        return sb.ToString();
    }

    // Midnight of the date of world.realtime, in deciseconds since 2000 (0 without an ambient world).
    private static double CurrentDayStart()
    {
        World world = World.CurrentOrNull;
        if (world == null) return 0d;
        double realtime = DmWorldState.Of(world).RealtimeStart + world.Time;
        return Math.Floor(realtime / DecisecondsPerDay) * DecisecondsPerDay;
    }
}
