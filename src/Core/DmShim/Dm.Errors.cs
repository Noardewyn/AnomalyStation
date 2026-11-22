using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (complete). Runtime errors, DM exceptions and the not-translated and deferred markers
// (docs/ai/translation-rules.md sections 1.4, 9.4 and 9.5).
public static partial class Dm
{
    /// <summary>
    /// DM <c>CRASH(msg)</c>: ends the current proc with a runtime error (BYOND ref <c>/proc/CRASH</c>). Written
    /// <c>throw Dm.CRASH(msg, "code/x.dm", 123);</c> with the DM location of the statement (rules 9.5); tg's
    /// <c>ASSERT</c> passes a ready message and no location. A non-text <paramref name="msg"/> is rendered like
    /// <c>"[msg]"</c>.
    /// </summary>
    public static DmCrashException CRASH(object msg = null, string file = null, int line = 0)
        => new DmCrashException(MessageText(msg), file, line);

    /// <summary>
    /// DM <c>EXCEPTION(value)</c>, BYOND's stddef macro for <c>new /exception(value, __FILE__, __LINE__)</c>
    /// (BYOND ref <c>/proc/EXCEPTION</c>): an <see cref="exception"/> datum whose <c>name</c> is
    /// <paramref name="value"/>. Written <c>Dm.EXCEPTION(msg, "code/x.dm", 123)</c> (rules 9.4).
    /// </summary>
    public static exception EXCEPTION(object value, string file = null, int line = 0)
        => new exception { name = value, file = file, line = line };

    /// <summary>
    /// The catch variable of a DM <c>catch(var/exception/e)</c> built from the C# exception (rules 9.4): the
    /// thrown <see cref="exception"/> datum itself for <c>throw EXCEPTION(...)</c>; for <c>throw</c> of another
    /// value, a datum whose <c>name</c> is that value (DM would bind the value itself to the variable); for a
    /// runtime error, a datum with the error message as <c>name</c> and its DM location, as BYOND creates one
    /// (BYOND ref <c>/exception</c>).
    /// </summary>
    public static exception exception(Exception ex)
    {
        switch (ex)
        {
            case null:
                return null;
            case DmThrowException thrown when thrown.Value is exception datum:
                return datum;
            case DmThrowException thrown:
                return new exception { name = thrown.Value, file = thrown.DmFile, line = thrown.DmLine };
            case DmRuntimeException runtime:
                return new exception { name = runtime.Message, file = runtime.DmFile, line = runtime.DmLine };
            default:
                return new exception { name = ex.Message };
        }
    }

    /// <summary>
    /// DM <c>throw x</c> (BYOND ref <c>/statement/throw</c>): <c>throw Dm.throw_value(x);</c>. The returned C#
    /// exception carries <paramref name="value"/>; a DM <c>catch</c> gets it back through <see cref="exception"/>.
    /// An <see cref="exception"/> datum keeps its file and line.
    /// </summary>
    public static DmThrowException throw_value(object value)
    {
        if (value is exception datum)
            return new DmThrowException(MessageText(datum.name), datum, datum.file, (int)datum.line);
        return new DmThrowException(MessageText(value), value, null, 0);
    }

    /// <summary>
    /// A proc or block that is not translated yet (rules 1.4):
    /// <c>Dm.NotTranslated("code/path.dm:123", "proc_name");</c>. Throws a <see cref="DmNotTranslatedException"/>
    /// when there is no ambient world or the world's <see cref="DmWorldState.NotTranslatedThrows"/> is set (the
    /// default, so tests fail loudly); otherwise reports it once per site through <c>World.ReportRuntime</c> and
    /// returns, so a running world carries on.
    /// </summary>
    public static void NotTranslated(string where, string what)
    {
        var error = new DmNotTranslatedException(where, what);
        World world = World.CurrentOrNull;
        if (world == null) throw error;
        DmWorldState state = DmWorldState.Of(world);
        if (state.NotTranslatedThrows) throw error;
        if (state.FirstNotTranslatedReport(where, what)) world.ReportRuntime(error);
    }

    /// <summary>
    /// A leaf that is deliberately not translated in the current milestone because nothing the milestone runs reads its
    /// effects (rules 1.4; lead decision W3 G1): <c>Dm.Deferred("code/path.dm:123", "proc_name: why");</c>, followed
    /// like <see cref="NotTranslated"/> by the return the proc would take. Unlike <see cref="NotTranslated"/> it never
    /// throws: it counts the call in the world (<see cref="DmWorldState.DeferredCount"/>,
    /// <see cref="DmWorldState.DeferredSites"/>), writes one line to the host log (<see cref="DmWorldState.WorldLog"/>)
    /// for the first call at each <paramref name="where"/> in the world, and returns. Without an ambient world it does
    /// nothing. Every use is listed in <c>docs/ai/specs/not-translated.md</c> with the milestone that translates it.
    /// </summary>
    public static void Deferred(string where, string what)
    {
        World world = World.CurrentOrNull;
        if (world == null) return;
        DmWorldState state = DmWorldState.Of(world);
        if (state.CountDeferred(where)) state.WorldLog?.Invoke("Deferred (not translated in this milestone): " + where + " " + what);
    }

    // The text of an error message value: text as is, null as "", anything else as DM renders it ("[x]").
    private static string MessageText(object msg) => msg switch
    {
        null => "",
        string s => s,
        _ => str(msg),
    };
}
