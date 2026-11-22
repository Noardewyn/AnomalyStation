// Engine part: the rust-g native library functions that tg calls through call_ext (code/__DEFINES/rust_g.dm), written
// natively in C# (docs/ai/translation-rules.md 13: rust-g itself is not ported; a function tg's translated code needs is
// reimplemented here with rust-g's exact behaviour). Source: github.com/tgstation/rust-g, src/*.rs.
// The file, log and time functions (logging package, wave 3) go through the world's host file seam
// (DmWorldState.Files, IDmFileSystem: rust-g's create_dir_all and file handles have no counterpart there) and the world
// clock instead of the wall clock (DV3, D31): rust-g's timestamps are world.realtime (DmWorldState.RealtimeStart plus
// world.time, deciseconds since 2000-01-01 00:00 UTC) in UTC, the world's local time, and its timers measure 0.
using System.Globalization;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/rust_g.dm:571  #define rustg_url_encode(text) RUSTG_CALL(RUST_G, "url_encode")("[text]")
    /// <summary>
    /// rust-g's <c>url_encode</c> of <c>"[text]"</c>. rust-g src/url.rs: <c>fn encode(string: &amp;str) -&gt; String {
    /// byte_serialize(string.as_bytes()).collect() }</c>, with <c>url::form_urlencoded::byte_serialize</c> (the
    /// application/x-www-form-urlencoded byte serializer of the <c>url</c> crate, imported as <c>url_dep</c>): over the
    /// UTF-8 bytes of the text, the bytes <c>*</c>, <c>-</c>, <c>.</c>, <c>_</c>, <c>0-9</c>, <c>A-Z</c>, <c>a-z</c>
    /// stay as they are, a space becomes <c>+</c>, and every other byte becomes <c>%XX</c> with upper-case hex digits
    /// (<c>percent_encoding::percent_encode_byte</c>'s table). So <c>~</c> is <c>%7E</c>, unlike RFC 3986's unreserved set.
    /// </summary>
    public static string rustg_url_encode(object text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Dm.text("[]", text));
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (byte b in bytes)
        {
            if (b == (byte)'*' || b == (byte)'-' || b == (byte)'.' || b == (byte)'_'
                || (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z'))
                sb.Append((char)b);
            else if (b == (byte)' ')
                sb.Append('+');
            else
                sb.Append('%').Append(HexUpper[b >> 4]).Append(HexUpper[b & 0xF]);
        }
        return sb.ToString();
    }

    private const string HexUpper = "0123456789ABCDEF";

    // tg: code/__DEFINES/rust_g.dm:200  #define rustg_file_read(fname) RUSTG_CALL(RUST_G, "file_read")(fname)
    /// <summary>
    /// rust-g's <c>file_read</c> (src/file.rs <c>read</c>): the whole text of the file at <paramref name="fname"/>. rust-g returns its
    /// error text for a missing file; tg's callers check <c>fexists</c> first, so a missing file reads as an empty text here.
    /// </summary>
    public static string rustg_file_read(object fname) => DmWorldState.Current.Files.ReadText(Dm.text("[]", fname)) ?? "";

    // tg: code/__DEFINES/rust_g.dm:202  #define rustg_file_write(text, fname) RUSTG_CALL(RUST_G, "file_write")(text, fname)
    /// <summary>
    /// rust-g's <c>file_write</c> (src/file.rs <c>write</c>): replaces the file at <paramref name="fname"/> with
    /// <paramref name="text"/> (rust-g creates the parent directories and writes through a temporary file; the file seam
    /// does both in one step). Returns rust-g's success value, an empty text (byond_fn of <c>.err()</c>: no error).
    /// Arguments arrive as text, as call_ext passes them.
    /// </summary>
    public static string rustg_file_write(object text, object fname)
    {
        DmWorldState.Current.Files.WriteText(Dm.text("[]", fname), Dm.text("[]", text));
        return "";
    }

    /// <summary>
    /// Silicons SI4d (D326): rust-g's <c>rustg_dmi_create_png(path, width, height, data)</c> (rust_g.dm, dmi.rs <c>create_png</c>): writes a
    /// <paramref name="width"/> x <paramref name="height"/> PNG of <paramref name="data"/>, one <c>#RRGGBB</c> or <c>#RRGGBBAA</c> colour per
    /// pixel, rows from the top, through the file seam (DmPng). Returns an empty text, or rust-g's error text.
    /// </summary>
    public static string rustg_dmi_create_png(object path, object width, object height, object data)
    {
        if (!int.TryParse(Dm.text("[]", width), out int w) || !int.TryParse(Dm.text("[]", height), out int h) || w < 1 || h < 1)
            return "invalid width or height";
        string[] colours = Dm.text("[]", data).Split('#', StringSplitOptions.RemoveEmptyEntries);
        if (colours.Length != w * h)
            return "the data has " + colours.Length + " colours for " + w * h + " pixels";
        var rgba = new uint[w * h];
        for (int i = 0; i < rgba.Length; i++)
        {
            string c = colours[i];
            if (c.Length is not (6 or 8) || !uint.TryParse(c, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint value))
                return "invalid colour #" + c;
            rgba[i] = c.Length == 6 ? value << 8 | 0xFF : value;
        }
        DmWorldState.Current.Files.WriteBytes(Dm.text("[]", path), DmPng.Encode(w, h, rgba));
        return "";
    }

    // tg: code/__DEFINES/rust_g.dm:203  #define rustg_file_append(text, fname) RUSTG_CALL(RUST_G, "file_append")(text, fname)
    /// <summary>
    /// rust-g's <c>file_append</c> (src/file.rs <c>append</c>): appends <paramref name="text"/> to the file at
    /// <paramref name="fname"/>, creating it when needed. Returns an empty text (no error).
    /// </summary>
    public static string rustg_file_append(object text, object fname)
    {
        DmWorldState.Current.Files.AppendText(Dm.text("[]", fname), Dm.text("[]", text));
        return "";
    }

    // tg: code/__DEFINES/rust_g.dm:425  #define rustg_log_write(fname, text, format) RUSTG_CALL(RUST_G, "log_write")(fname, text, format)
    /// <summary>
    /// rust-g's <c>log_write</c> (src/log.rs): appends to the file at <paramref name="fname"/>. With
    /// <paramref name="format"/> <c>"false"</c> the text is written as it is; with any other value its first line is
    /// written as <c>[%F %T%.3f] line</c> with the UTC timestamp, every further line (split at <c>\n</c>) as
    /// <c> - line</c>, each followed by a line break. tg's <c>WRITE_LOG</c> passes <c>"true"</c>,
    /// <c>WRITE_LOG_NO_FORMAT</c> <c>"false"</c> (__DEFINES/logging.dm:196-197). Returns an empty text (no error).
    /// </summary>
    public static string rustg_log_write(object fname, object text, object format)
    {
        string path = Dm.text("[]", fname);
        string data = Dm.text("[]", text);
        if (Dm.text("[]", format) == "false")
        {
            // Write the data to the file with no accoutrements.
            DmWorldState.Current.Files.AppendText(path, data);
            return "";
        }
        // write first line, timestamped; then the remaining lines
        string[] lines = data.Split('\n');
        var sb = new StringBuilder();
        sb.Append('[').Append(RustgFormatTime(RustgNow(), "%F %T%.3f")).Append("] ").Append(lines[0]).Append('\n');
        for (int i = 1; i < lines.Length; i++)
            sb.Append(" - ").Append(lines[i]).Append('\n');
        DmWorldState.Current.Files.AppendText(path, sb.ToString());
        return "";
    }

    // tg: code/__DEFINES/rust_g.dm:426  /proc/rustg_log_close_all() return RUSTG_CALL(RUST_G, "log_close_all")()
    /// <summary>
    /// rust-g's <c>log_close_all</c> (src/log.rs): drops its cached file handles and returns an empty text. The file
    /// seam keeps no handles, so nothing else happens.
    /// </summary>
    public static object rustg_log_close_all()
    {
        return "";
    }

    // tg: code/__DEFINES/rust_g.dm:539  #define rustg_time_reset(id) RUSTG_CALL(RUST_G, "time_reset")(id)
    /// <summary>
    /// rust-g's <c>time_reset</c> (src/time.rs): restarts the timer <paramref name="id"/> and returns an empty text.
    /// DV3: there is no wall clock, every timer measures 0, so there is nothing to restart.
    /// </summary>
    public static string rustg_time_reset(object id)
    {
        // DEVIATION(DV3): no wall clock; there is no timer to restart.
        return "";
    }

    // tg: code/__DEFINES/rust_g.dm:538  #define rustg_time_milliseconds(id) text2num(RUSTG_CALL(RUST_G, "time_milliseconds")(id))
    /// <summary>
    /// rust-g's <c>time_milliseconds</c> (src/time.rs, the real time since the timer's reset, as text) read through
    /// tg's <c>text2num</c>. DV3: no wall clock, so the elapsed time is always 0 (the Master's subsystem init timings,
    /// controllers/master.dm:499-504).
    /// </summary>
    public static float? rustg_time_milliseconds(object id)
    {
        // DEVIATION(DV3): no wall clock; every timer measures 0 ms.
        return Dm.text2num("0");
    }

    // tg: code/__DEFINES/rust_g.dm:550  /proc/rustg_unix_timestamp()
    /// <summary>
    /// rust-g's <c>unix_timestamp</c> (src/time.rs: seconds since the Unix epoch, <c>format!("{:.6}", ...)</c>, as
    /// text), on the world clock (DV3): world.realtime in seconds plus the 946684800 seconds from 1970 to 2000.
    /// </summary>
    public static object rustg_unix_timestamp()
    {
        double seconds = 946684800d + RustgRealtime() / 10d;
        return seconds.ToString("F6", CultureInfo.InvariantCulture);
    }

    // tg: code/__DEFINES/rust_g.dm:543  #define rustg_formatted_timestamp(format) RUSTG_CALL(RUST_G, "formatted_timestamp")(format)
    /// <summary>
    /// rust-g's <c>formatted_timestamp</c> (src/time.rs: <c>Local::now().format(format)</c>, chrono's strftime) on the
    /// world clock in UTC, the world's local time (DV3; time2text does the same). The port knows the chrono specifiers
    /// tg uses (<c>%Y %m %d %H %M %S %F %T %.3f %.6f %.9f %%</c>); any other is a runtime error.
    /// </summary>
    public static string rustg_formatted_timestamp(object format)
    {
        return RustgFormatTime(RustgNow(), Dm.text("[]", format));
    }

    // world.realtime of the ambient world as a double (the facade's float loses the sub-second part): deciseconds
    // since 2000-01-01 00:00 UTC.
    // DEVIATION(DV3): rust-g's clock is the world clock, not the host's.
    private static double RustgRealtime()
        => DmWorldState.Current.RealtimeStart + World.Current.Tick * (double)World.TickLag;

    private static DateTime RustgNow()
        => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks((long)Math.Round(RustgRealtime() * (TimeSpan.TicksPerSecond / 10d)));

    // chrono's strftime for the specifiers tg's rust-g calls use.
    // tg: code/__DEFINES/rust_g.dm:508  RUSTG_CALL(RUST_G, "sound_len")(file_path), the call of rustg_sound_length and of
    // SSsounds.get_sound_length (controllers/subsystem/sounds.dm:251)
    /// <summary>
    /// rust-g's <c>sound_len</c>: the duration of the file in deciseconds, as text. M4b audio slice 1: the length comes from the
    /// host's table (<see cref="SoundLengths"/>), which the sound tool reads from the files (M4b slice 2) where rust-g decodes them
    /// in BYOND's resource folder; a file the table lacks gives null, which tg's callers report as
    /// <c>"rustg_sound_length error: []"</c> (sounds.dm:255).
    /// </summary>
    internal static string rustg_call_sound_len(object file_path)
    {
        float? length = SoundLengths.Length(file_path as string);
        return length.HasValue ? length.Value.ToString("R", CultureInfo.InvariantCulture) : null;
    }

    // tg: code/__DEFINES/rust_g.dm:528  #define rustg_sound_length_list(file_paths) json_decode(RUSTG_CALL(RUST_G, "sound_len_list")(json_encode(file_paths)))
    /// <summary>
    /// rust-g's <c>sound_len_list</c> as tg reads it after <c>json_decode</c>: <c>list("successes" = list(path = "length"),
    /// "errors" = list(path = "message"))</c>, a path being the item's text (<c>json_encode</c> writes a resource as its path).
    /// M4b audio slice 1: the lengths (deciseconds, as text, which tg turns into numbers) come from the host's table
    /// (<see cref="SoundLengths"/>); a file the table lacks goes to the errors with the port's message.
    /// </summary>
    public static DmList rustg_sound_length_list(object file_paths)
    {
        DmList successes = Dm.list();
        DmList errors = Dm.list();
        foreach (object file_path in Dm.each(file_paths))
        {
            string path = Dm.text("[]", file_path);
            float? length = SoundLengths.Length(path);
            if (length.HasValue)
                successes[path] = length.Value.ToString("R", CultureInfo.InvariantCulture);
            else
                errors[path] = "SoundLen: the host's sound index has no such file";
        }
        return Dm.list(Dm.kv(Defines.RUSTG_SOUNDLEN_SUCCESSES, successes), Dm.kv(Defines.RUSTG_SOUNDLEN_ERRORS, errors));
    }

    private static string RustgFormatTime(DateTime time, string format)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (c != '%' || i == format.Length - 1)
            {
                sb.Append(c);
                continue;
            }
            char spec = format[++i];
            switch (spec)
            {
                case 'Y': sb.Append(time.Year.ToString("D4", CultureInfo.InvariantCulture)); break;
                case 'm': sb.Append(time.Month.ToString("D2", CultureInfo.InvariantCulture)); break;
                case 'd': sb.Append(time.Day.ToString("D2", CultureInfo.InvariantCulture)); break;
                case 'H': sb.Append(time.Hour.ToString("D2", CultureInfo.InvariantCulture)); break;
                case 'M': sb.Append(time.Minute.ToString("D2", CultureInfo.InvariantCulture)); break;
                case 'S': sb.Append(time.Second.ToString("D2", CultureInfo.InvariantCulture)); break;
                case 'F': sb.Append(RustgFormatTime(time, "%Y-%m-%d")); break;
                case 'T': sb.Append(RustgFormatTime(time, "%H:%M:%S")); break;
                case '%': sb.Append('%'); break;
                case '.' when i + 2 < format.Length && format[i + 2] == 'f' && (format[i + 1] == '3' || format[i + 1] == '6' || format[i + 1] == '9'):
                {
                    // %.3f, %.6f, %.9f: the fraction of the second with a fixed number of digits, after a dot.
                    int digits = format[i + 1] - '0';
                    long nanos = (time.Ticks % TimeSpan.TicksPerSecond) * 100;
                    long scaled = nanos / (long)Math.Pow(10, 9 - digits);
                    sb.Append('.').Append(scaled.ToString("D" + digits, CultureInfo.InvariantCulture));
                    i += 2;
                    break;
                }
                default:
                    throw Dm.CRASH("rustg_formatted_timestamp: the port does not know the chrono specifier %" + spec + " in '" + format + "'", "code/__DEFINES/rust_g.dm", 543);
            }
        }
        return sb.ToString();
    }
}
