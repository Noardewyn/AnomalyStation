using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Anomaly.Core.Kernel;

// The waiting model (docs/ai/specs/waiting-model.md).
//
// A DM proc that may sleep (sleep(), stoplag(), do_after(), tgui_*) is translated to a C# `async` method
// returning DmTask / DmTask<T>. This is NOT System.Threading.Tasks: nothing here uses threads, a
// synchronization context or the thread pool. Every continuation is resumed by the world's Scheduler on the
// simulation tick, in (tick, insertion) order, so execution is single-threaded and deterministic (D31).
//
// What matches DM:
//  * a proc call runs synchronously until its first real suspension;
//  * a caller that `await`s a waiting proc is blocked until it returns (DM default, waitfor = TRUE), and
//    resumes immediately when it returns, before any other scheduled work;
//  * Waiting.Detach (INVOKE_ASYNC, ASYNC, spawn) lets the caller continue as soon as the callee first
//    suspends; Waiting.WaitforFalse gives a `set waitfor = FALSE` proc's caller the '.' value at that moment;
//  * usr is an implicit argument of every call: the builder puts the caller's usr back when the call returns
//    or first suspends, and an awaiting caller resumes with its own usr (waiting-model.md section 10).
//
// Where the port deviates (docs/ai/DEVIATIONS.md): DM ends only the failing proc on a runtime error and hands
// its '.' to the caller. Here an exception thrown inside an async proc is CAPTURED in the returned task (it
// never escapes the call expression); `await` rethrows it in the caller, so it unwinds through the callers
// to the nearest isolation boundary (a scheduler entry, Waiting.Detach, a World phase, and the boundaries
// translated tg code adds), which reports it through World.ReportRuntime and carries on.
//
// Not serializable: a suspended state machine cannot be snapshotted. This is the known price of the model.

/// <summary>
/// Marker for the awaiters a DmTask proc may await (DmTask, DmTask&lt;T&gt;, sleep and any later kernel
/// awaitable). The builders refuse every other awaiter at the await, so a stray
/// <c>await Task.Delay(..)</c> becomes a runtime error instead of a thread-pool continuation outside the tick.
/// </summary>
public interface IDmAwaiter
{
}

/// <summary>
/// Untyped view of a DmTask or DmTask&lt;T&gt; for calls by name (<c>call()()</c>, callbacks, signal handlers):
/// the dynamic call machinery receives the callee's return value as <c>object</c>, and when that is a boxed
/// task it turns it into a <c>DmTask&lt;object&gt;</c> without reflection.
/// </summary>
public interface IDmTask
{
    bool IsCompleted { get; }
    bool IsFaulted { get; }

    /// <summary>
    /// The same call as a <c>DmTask&lt;object&gt;</c>: a completed task keeps its value (boxed) or its captured
    /// error; a pending one is awaited by a small adapter proc, so its later value or error reaches whoever
    /// awaits, detaches or NoSleeps the result.
    /// </summary>
    DmTask<object> AsObjectTask();
}

/// <summary>Shared completion state of a DmTask that suspended (or faulted).</summary>
internal class DmTaskState
{
    private Action _continuation;
    private object _continuationUsr;
    internal IAsyncStateMachine Machine;
    internal Action MoveNext;

    /// <summary>The world that was ambient when the proc first suspended; errors are reported there.</summary>
    internal readonly World World;

    /// <summary>The builder has handed the task to its caller (the call expression has returned).</summary>
    internal bool Returned;

    /// <summary>The caller will never await the task (Waiting.Detach, NoSleep, WaitforFalse).</summary>
    internal bool Detached;

    internal bool Done { get; private set; }
    internal Exception Error { get; private set; }

    internal DmTaskState() => World = World.CurrentOrNull;

    internal static DmTaskState Faulted(Exception error)
    {
        var s = new DmTaskState();
        s.Done = true;
        s.Error = error ?? throw new ArgumentNullException(nameof(error));
        s.Returned = true;
        return s;
    }

    internal void Complete(Exception error)
    {
        Done = true;
        Error = error;
        var c = _continuation;
        var usr = _continuationUsr;
        _continuation = null;
        _continuationUsr = null;
        if (c != null)
        {
            Resume(World ?? World.CurrentOrNull, c, usr);
            return;
        }
        // Nobody awaits. While the builder is still inside the call (an awaiter refused the first suspension)
        // the error stays in the task and the call expression returns it faulted. Once the task is out, an
        // error with no awaiter is the top of a thread of execution: report it (a detached task by design, or
        // a dropped one by mistake; either way the error is never lost).
        if (error != null && (Returned || Detached)) (World ?? World.CurrentOrNull)?.ReportRuntime(error);
    }

    internal void OnCompleted(Action continuation)
    {
        if (Done) { continuation(); return; }
        if (_continuation != null) throw new InvalidOperationException("A DmTask can be awaited by one caller only.");
        _continuation = continuation;
        // The awaiting caller resumes with the usr it had when it began to wait, whatever the callee did to it.
        _continuationUsr = (World ?? World.CurrentOrNull)?.usr;
    }

    internal void Box<TSM>(ref TSM stateMachine) where TSM : IAsyncStateMachine
    {
        if (Machine == null)
        {
            Machine = stateMachine; // boxes a struct state machine once; a class one is shared as is
            MoveNext = Machine.MoveNext;
        }
    }

    /// <summary>
    /// Nested completions of an awaiting chain resume on the native stack (callee returns, caller resumes at
    /// once, its caller resumes, ...). Beyond this depth the next resumption is handed to the outermost
    /// completion frame of the world, which runs it as soon as the inner frames have unwound. Every frame in
    /// between is a finished state machine returning from SetResult, so nothing observable runs in between
    /// and DM's "the caller resumes immediately" order is unchanged; only the native stack stays bounded.
    /// Each resumed caller runs with its own usr (the one it had when it began to wait); the usr of the
    /// completing code is put back afterwards.
    /// </summary>
    internal const int MaxInlineCompletionDepth = 64;

    private static void Resume(World world, Action continuation, object usr)
    {
        if (world == null) { continuation(); return; }
        if (world.CompletionDepth >= MaxInlineCompletionDepth)
        {
            world.DeferredCompletions.Enqueue((continuation, usr));
            return;
        }
        var outerUsr = world.usr;
        world.CompletionDepth++;
        try
        {
            world.usr = usr;
            continuation();
            if (world.CompletionDepth == 1)
            {
                while (world.DeferredCompletions.TryDequeue(out var next))
                {
                    world.usr = next.Usr;
                    next.Continuation();
                }
            }
        }
        finally
        {
            world.CompletionDepth--;
            world.usr = outerUsr;
        }
    }

    internal static InvalidOperationException ForeignAwaiter(Type awaiterType) => new(
        $"{awaiterType} is not a kernel awaiter: a DmTask proc may await only DmTask, DmTask<T>, sleep and other "
        + "IDmAwaiter awaitables (System.Threading.Tasks would resume outside the tick).");

    internal static InvalidOperationException ReadBeforeCompletion() => new(
        "DmTask read before completion: a synchronous caller cannot wait for a sleeping proc "
        + "(await it in an async proc, or use Waiting.NoSleep / Waiting.Detach).");
}

internal sealed class DmTaskState<T> : DmTaskState
{
    internal T Result;

    internal static DmTaskState<T> FaultedT(Exception error)
    {
        var s = new DmTaskState<T>();
        s.Complete(error ?? throw new ArgumentNullException(nameof(error)));
        s.Returned = true;
        return s;
    }
}

[AsyncMethodBuilder(typeof(DmTaskBuilder))]
public readonly struct DmTask : IDmTask
{
    internal readonly DmTaskState State; // null: completed synchronously without error

    internal DmTask(DmTaskState state) => State = state;

    public static DmTask CompletedTask => default;

    /// <summary>A task that is already faulted with <paramref name="error"/>.</summary>
    public static DmTask FromException(Exception error) => new DmTask(DmTaskState.Faulted(error));

    public bool IsCompleted => State == null || State.Done;

    /// <summary>Completed with a captured runtime error.</summary>
    public bool IsFaulted => State != null && State.Done && State.Error != null;

    /// <summary>The captured error of a faulted task, otherwise null.</summary>
    public Exception Exception => IsFaulted ? State.Error : null;

    public DmTaskAwaiter GetAwaiter() => new DmTaskAwaiter(State);

    /// <inheritdoc/>
    public DmTask<object> AsObjectTask()
    {
        if (State == null) return DmTask<object>.FromResult(null);
        if (State.Done) return State.Error != null ? DmTask<object>.FromException(State.Error) : DmTask<object>.FromResult(null);
        return Adapt(this);
    }

    private static async DmTask<object> Adapt(DmTask task)
    {
        await task;
        return null;
    }
}

public readonly struct DmTaskAwaiter : ICriticalNotifyCompletion, IDmAwaiter
{
    private readonly DmTaskState _state;
    internal DmTaskAwaiter(DmTaskState state) => _state = state;

    public bool IsCompleted => _state == null || _state.Done;
    public void OnCompleted(Action continuation) => _state.OnCompleted(continuation);
    public void UnsafeOnCompleted(Action continuation) => _state.OnCompleted(continuation);

    /// <summary>Rethrows a captured error; throws when the task has not completed yet.</summary>
    public void GetResult()
    {
        if (_state == null) return;
        if (!_state.Done) throw DmTaskState.ReadBeforeCompletion();
        if (_state.Error != null) ExceptionDispatchInfo.Throw(_state.Error);
    }
}

public struct DmTaskBuilder
{
    private DmTaskState _state;

    public static DmTaskBuilder Create() => default;

    public void Start<TSM>(ref TSM stateMachine) where TSM : IAsyncStateMachine
    {
        // DM passes usr to every call: whatever the proc does to it before it returns or first suspends, the
        // caller continues with its own (waiting-model.md section 10).
        var world = World.CurrentOrNull;
        if (world == null)
        {
            stateMachine.MoveNext();
        }
        else
        {
            var usr = world.usr;
            stateMachine.MoveNext();
            world.usr = usr;
        }
        if (_state != null) _state.Returned = true;
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine) { }

    public void SetResult()
    {
        if (_state != null) _state.Complete(null);
    }

    public void SetException(Exception exception)
    {
        // Captured, never thrown out of the call: the caller sees it through await / Detach / NoSleep.
        _state ??= new DmTaskState();
        _state.Complete(exception);
    }

    public DmTask Task => new DmTask(_state);

    public void AwaitOnCompleted<TAwaiter, TSM>(ref TAwaiter awaiter, ref TSM stateMachine)
        where TAwaiter : INotifyCompletion where TSM : IAsyncStateMachine
    {
        if (awaiter is not IDmAwaiter) throw DmTaskState.ForeignAwaiter(typeof(TAwaiter));
        _state ??= new DmTaskState();
        _state.Box(ref stateMachine);
        awaiter.OnCompleted(_state.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TSM>(ref TAwaiter awaiter, ref TSM stateMachine)
        where TAwaiter : ICriticalNotifyCompletion where TSM : IAsyncStateMachine
    {
        if (awaiter is not IDmAwaiter) throw DmTaskState.ForeignAwaiter(typeof(TAwaiter));
        _state ??= new DmTaskState();
        _state.Box(ref stateMachine);
        awaiter.UnsafeOnCompleted(_state.MoveNext);
    }
}

[AsyncMethodBuilder(typeof(DmTaskBuilder<>))]
public readonly struct DmTask<T> : IDmTask
{
    internal readonly DmTaskState<T> State; // null: completed synchronously with _result
    private readonly T _result;

    internal DmTask(T result) { State = null; _result = result; }
    internal DmTask(DmTaskState<T> state) { State = state; _result = default; }

    public static DmTask<T> FromResult(T value) => new DmTask<T>(value);

    /// <summary>A task that is already faulted with <paramref name="error"/>.</summary>
    public static DmTask<T> FromException(Exception error) => new DmTask<T>(DmTaskState<T>.FaultedT(error));

    public bool IsCompleted => State == null || State.Done;

    /// <summary>Completed with a captured runtime error.</summary>
    public bool IsFaulted => State != null && State.Done && State.Error != null;

    /// <summary>The captured error of a faulted task, otherwise null.</summary>
    public Exception Exception => IsFaulted ? State.Error : null;

    /// <summary>The value of a task that completed without error; false while pending or when faulted.</summary>
    public bool TryGetResult(out T value)
    {
        if (State == null) { value = _result; return true; }
        if (State.Done && State.Error == null) { value = State.Result; return true; }
        value = default;
        return false;
    }

    public DmTaskAwaiter<T> GetAwaiter() => new DmTaskAwaiter<T>(State, _result);

    /// <inheritdoc/>
    public DmTask<object> AsObjectTask()
    {
        if (State == null) return DmTask<object>.FromResult(_result);
        if (State.Done) return State.Error != null ? DmTask<object>.FromException(State.Error) : DmTask<object>.FromResult(State.Result);
        return Adapt(this);
    }

    private static async DmTask<object> Adapt(DmTask<T> task) => await task;
}

public readonly struct DmTaskAwaiter<T> : ICriticalNotifyCompletion, IDmAwaiter
{
    private readonly DmTaskState<T> _state;
    private readonly T _result;
    internal DmTaskAwaiter(DmTaskState<T> state, T result) { _state = state; _result = result; }

    public bool IsCompleted => _state == null || _state.Done;
    public void OnCompleted(Action continuation) => _state.OnCompleted(continuation);
    public void UnsafeOnCompleted(Action continuation) => _state.OnCompleted(continuation);

    /// <summary>The value, or the captured error rethrown; throws when the task has not completed yet.</summary>
    public T GetResult()
    {
        if (_state == null) return _result;
        if (!_state.Done) throw DmTaskState.ReadBeforeCompletion();
        if (_state.Error != null) ExceptionDispatchInfo.Throw(_state.Error);
        return _state.Result;
    }
}

public struct DmTaskBuilder<T>
{
    private DmTaskState<T> _state;
    private T _syncResult;

    public static DmTaskBuilder<T> Create() => default;

    public void Start<TSM>(ref TSM stateMachine) where TSM : IAsyncStateMachine
    {
        // DM passes usr to every call: whatever the proc does to it before it returns or first suspends, the
        // caller continues with its own (waiting-model.md section 10).
        var world = World.CurrentOrNull;
        if (world == null)
        {
            stateMachine.MoveNext();
        }
        else
        {
            var usr = world.usr;
            stateMachine.MoveNext();
            world.usr = usr;
        }
        if (_state != null) _state.Returned = true;
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine) { }

    public void SetResult(T result)
    {
        if (_state != null) { _state.Result = result; _state.Complete(null); }
        else _syncResult = result;
    }

    public void SetException(Exception exception)
    {
        _state ??= new DmTaskState<T>();
        _state.Complete(exception);
    }

    public DmTask<T> Task => _state != null ? new DmTask<T>(_state) : new DmTask<T>(_syncResult);

    public void AwaitOnCompleted<TAwaiter, TSM>(ref TAwaiter awaiter, ref TSM stateMachine)
        where TAwaiter : INotifyCompletion where TSM : IAsyncStateMachine
    {
        if (awaiter is not IDmAwaiter) throw DmTaskState.ForeignAwaiter(typeof(TAwaiter));
        _state ??= new DmTaskState<T>();
        _state.Box(ref stateMachine);
        awaiter.OnCompleted(_state.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TSM>(ref TAwaiter awaiter, ref TSM stateMachine)
        where TAwaiter : ICriticalNotifyCompletion where TSM : IAsyncStateMachine
    {
        if (awaiter is not IDmAwaiter) throw DmTaskState.ForeignAwaiter(typeof(TAwaiter));
        _state ??= new DmTaskState<T>();
        _state.Box(ref stateMachine);
        awaiter.UnsafeOnCompleted(_state.MoveNext);
    }
}
