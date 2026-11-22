namespace Anomaly.Core.DmShim;

/// <summary>
/// A DM runtime error raised by the shim or by translated code (type mismatch, list index out of bounds,
/// undefined proc, <c>CRASH</c>, a <c>throw</c>). It unwinds to the nearest isolation boundary, which reports it
/// through <c>World.ReportRuntime</c> (docs/ai/translation-rules.md section 9, DV1). <see cref="DmFile"/> and
/// <see cref="DmLine"/> carry the DM location when the translated code gave one (rules 9.5), never a C# one.
/// </summary>
public class DmRuntimeException : Exception
{
    public DmRuntimeException(string message) : base(message)
    {
    }

    public DmRuntimeException(string message, Exception inner) : base(message, inner)
    {
    }

    public DmRuntimeException(string message, string file, int line) : base(message)
    {
        DmFile = file;
        DmLine = line;
    }

    /// <summary>The DM file (<c>"code/datums/datum.dm"</c>), or null.</summary>
    public string DmFile { get; }

    /// <summary>The DM line, or 0.</summary>
    public int DmLine { get; }
}

/// <summary>DM <c>CRASH(msg)</c> (and tg's <c>ASSERT</c>, which crashes): <c>throw Dm.CRASH(msg, file, line);</c>.</summary>
public sealed class DmCrashException : DmRuntimeException
{
    public DmCrashException(string message, string file, int line) : base(message, file, line)
    {
    }
}

/// <summary>
/// DM <c>throw x</c>: <c>throw Dm.throw_value(x);</c>. <see cref="Value"/> is the thrown DM value, an
/// <see cref="exception"/> datum or any other value; <c>Dm.exception</c> turns it back into the catch variable.
/// </summary>
public sealed class DmThrowException : DmRuntimeException
{
    public DmThrowException(string message, object value, string file, int line) : base(message, file, line)
    {
        Value = value;
    }

    /// <summary>The thrown DM value.</summary>
    public object Value { get; }
}

/// <summary>Raised by <c>Dm.NotTranslated</c> for a proc or block that is not translated yet (rules 1.4).</summary>
public sealed class DmNotTranslatedException : DmRuntimeException
{
    public DmNotTranslatedException(string where, string what)
        : base($"not translated yet: {what} ({where})")
    {
        Where = where;
        What = what;
    }

    /// <summary>The DM location, <c>"code/path.dm:123"</c>.</summary>
    public string Where { get; }

    /// <summary>The proc or construct.</summary>
    public string What { get; }
}

/// <summary>
/// The BYOND <c>/exception</c> datum (BYOND ref <c>/exception</c>): what <c>EXCEPTION(value)</c> creates and what a
/// <c>catch(var/exception/e)</c> variable holds. A builtin value type: no world id, never registered.
/// </summary>
[DmPath("/exception")]
public class exception
{
    /// <summary>A text string (such as an error message) or other value identifying the exception.</summary>
    public object name;

    /// <summary>Detailed error info; BYOND fills it only for <c>world.Error()</c>. Null in the port.</summary>
    public string desc;

    /// <summary>The DM file where the error occurred, if known.</summary>
    public string file;

    /// <summary>The DM line where the error occurred, if known.</summary>
    public float line;
}
