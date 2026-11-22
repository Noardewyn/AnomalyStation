using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Anomaly.Core.Kernel;

/// <summary>
/// Holder for DM's implicit return var '.' of a <c>set waitfor = FALSE</c> proc: the body writes every
/// <c>. = x</c> through it, and the wrapper hands its value to the caller when the body first suspends
/// (see <see cref="Waiting.WaitforFalse{T}(DmTask{T}, DotBox{T})"/>).
/// </summary>
public sealed class DotBox<T>
{
    public T Value;
}

/// <summary>
/// The call forms of the waiting model besides <c>await</c> (docs/ai/specs/waiting-model.md, "Call forms").
/// </summary>
public static class Waiting
{
    // ---- detached calls: INVOKE_ASYNC, ASYNC / spawn(-1) around a call ----

    /// <summary>
    /// A detached call (DM <c>INVOKE_ASYNC</c>, <c>ASYNC { Foo() }</c>): <c>Waiting.Detach(Foo(...))</c>. The
    /// callee has already run up to its first suspension; the caller continues. An isolation boundary: a
    /// captured error is reported now, a later one when the callee finishes.
    /// </summary>
    public static void Detach(DmTask task)
    {
        var state = task.State;
        if (state == null) return;
        if (state.Done) { if (state.Error != null) Report(state, state.Error); return; }
        state.Detached = true;
    }

    /// <summary>
    /// A detached call whose value the caller reads: the callee's value when it completed without sleeping,
    /// otherwise default (DM: the call was a separate thread, so its value is not seen).
    /// </summary>
    public static T Detach<T>(DmTask<T> task)
    {
        var state = task.State;
        if (state == null) return task.TryGetResult(out var value) ? value : default;
        if (state.Done)
        {
            if (state.Error != null) { Report(state, state.Error); return default; }
            return state.Result;
        }
        state.Detached = true;
        return default;
    }

    /// <summary>
    /// A detached call of a synchronous proc (not a <c>DmTask</c> slot), for <c>INVOKE_ASYNC</c> of a proc that never
    /// sleeps: runs <paramref name="call"/> now inside an isolation boundary.
    /// </summary>
    public static void DetachSync(Action call)
    {
        var world = World.CurrentOrNull;
        object usr = world?.usr;
        try { call(); }
        catch (Exception e) when (world != null) { world.ReportRuntime(e); } // no world to report to: do not lose it
        finally { if (world != null) world.usr = usr; } // usr is per call in DM
    }

    // ---- calls of DmTask slots from a synchronous context (SIGNAL_HANDLER, SHOULD_NOT_SLEEP, any non-async proc) ----

    /// <summary>
    /// A synchronous proc calling a proc of a <c>DmTask</c> slot: <c>Waiting.NoSleep(Foo(...))</c>. If the callee completed,
    /// this is a plain call (a captured error is rethrown). If it slept, that is a runtime ("proc slept inside
    /// a no-sleep context") reported here; the caller continues and the callee runs on detached.
    /// </summary>
    public static void NoSleep(DmTask task, [CallerArgumentExpression(nameof(task))] string call = null)
    {
        var state = task.State;
        if (state == null) return;
        if (state.Done) { if (state.Error != null) ExceptionDispatchInfo.Throw(state.Error); return; }
        state.Detached = true;
        Report(state, SleptInNoSleepContext(call));
    }

    /// <summary>The value form of <see cref="NoSleep(DmTask, string)"/>: default when the callee slept.</summary>
    public static T NoSleep<T>(DmTask<T> task, [CallerArgumentExpression(nameof(task))] string call = null)
    {
        var state = task.State;
        if (state == null || state.Done) return task.GetAwaiter().GetResult();
        state.Detached = true;
        Report(state, SleptInNoSleepContext(call));
        return default;
    }

    // ---- set waitfor = FALSE: the callee-side wrapper ----

    /// <summary>
    /// The wrapper of a proc with <c>set waitfor = FALSE</c> (own or inherited from the root), with the slot's
    /// signature from <c>slots.tsv</c> (translation-rules.md 7.1-7.4; here the synchronous <c>/datum/process</c>):
    /// <code>
    /// public override object process(object seconds_per_tick = null, params object[] __extra)
    /// {
    ///     DotBox&lt;object&gt; dot = new DotBox&lt;object&gt;();
    ///     return Waiting.WaitforFalse(process__body(seconds_per_tick, __extra, dot), dot);
    /// }
    /// private async DmTask&lt;object&gt; process__body(object seconds_per_tick, object[] __extra, DotBox&lt;object&gt; dot)
    /// {
    ///     // the DM default prologue first (translation-rules.md 7.2), then the DM statements, '.' as dot.Value
    ///     ...
    ///     return dot.Value;
    /// }
    /// </code>
    /// If the body completed, its value is returned (a captured error is rethrown, like a plain call). If it
    /// suspended, the caller gets '.' as the body left it at that moment (default when
    /// <paramref name="dot"/> is null, i.e. the body never writes '.' before it may sleep) and the body runs
    /// on detached: its later return value is discarded and a later error is reported. Callers call the
    /// wrapper like any proc and never wait on it. In a <c>DmTask</c> slot the wrapper is an <c>async</c>
    /// DmTask&lt;T&gt; method with the same body, so a rethrown error is captured in its task.
    /// </summary>
    public static T WaitforFalse<T>(DmTask<T> body, DotBox<T> dot)
    {
        var state = body.State;
        if (state == null || state.Done) return body.GetAwaiter().GetResult();
        state.Detached = true;
        return dot != null ? dot.Value : default;
    }

    /// <summary>The wrapper of a <c>set waitfor = FALSE</c> proc that returns nothing.</summary>
    public static void WaitforFalse(DmTask body)
    {
        var state = body.State;
        if (state == null) return;
        if (state.Done) { if (state.Error != null) ExceptionDispatchInfo.Throw(state.Error); return; }
        state.Detached = true;
    }

    // ---- spawn ----

    /// <summary>
    /// DM <c>spawn(delay) { body }</c>. A negative delay (<c>spawn(-1)</c>, tg <c>ASYNC</c>) runs the body now
    /// until its first sleep and then continues the caller; 0 runs it later in this tick after everything
    /// already queued; a positive delay rounds like sleep. The body runs as a detached thread with the usr of
    /// the spawning code. Copy the locals the body reads into fresh locals first: DM copies them, a C# lambda
    /// shares them.
    /// </summary>
    public static void Spawn(float delay, Func<DmTask> body)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (delay < 0f)
        {
            Detach(Start(body));
            return;
        }
        World.Current.Scheduler.After(Scheduler.TicksFromDeciseconds(delay), () => Detach(Start(body)));
    }

    private static DmTask Start(Func<DmTask> body)
    {
        var world = World.CurrentOrNull;
        object usr = world?.usr;
        try { return body(); }
        catch (Exception e) { return DmTask.FromException(e); } // a non-async body that throws
        finally { if (world != null) world.usr = usr; }         // a non-async body that assigns usr
    }

    // ---- helpers ----

    private static WaitingException SleptInNoSleepContext(string call) => new(
        $"proc slept inside a no-sleep context (SIGNAL_HANDLER / SHOULD_NOT_SLEEP / synchronous caller): {call ?? "?"}; "
        + "the caller continued with a default value and the callee runs on detached.");

    private static void Report(DmTaskState state, Exception error)
    {
        var world = state.World ?? World.CurrentOrNull;
        if (world == null) ExceptionDispatchInfo.Throw(error); // no world to report to: do not lose it
        world.ReportRuntime(error);
    }
}
