using Microsoft.CodeAnalysis;

namespace Anomaly.Analyzers;

/// <summary>
/// The diagnostics of the waiting-model analyzer (D97; docs/ai/specs/waiting-model.md section 5.6). All are errors: the
/// repository builds with 0 warnings and 0 errors, so a violation cannot slip in. A deliberate exception in kernel, shim
/// or host code is a local <c>#pragma warning disable ANOM000x</c> with a one-line reason.
/// </summary>
public static class Descriptors
{
    public const string Category = "WaitingModel";

    /// <summary>ANOM0001: a DmTask value is discarded (every project in scope).</summary>
    public static readonly DiagnosticDescriptor DroppedTask = new(
        id: "ANOM0001",
        title: "A DmTask is dropped",
        messageFormat: "The DmTask of '{0}' is dropped: await it, write Waiting.Detach(...) for a detached call (INVOKE_ASYNC, ASYNC), or Waiting.NoSleep(...) in a synchronous proc",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A DmTask is consumed exactly once, right away: awaited, or handed to Waiting.Detach / Waiting.NoSleep "
            + "/ Waiting.WaitforFalse, or returned. A task dropped by an expression statement or a discard ('_ = Foo();', "
            + "'var _ = Foo();', a discard in a deconstruction or an out argument) loses the caller's wait; CS4014 "
            + "catches only part of these.");

    /// <summary>ANOM0002: a DmTask value is stored or passed on instead of being consumed at once (translated code).</summary>
    public static readonly DiagnosticDescriptor StoredTask = new(
        id: "ANOM0002",
        title: "A DmTask is stored instead of consumed at once",
        messageFormat: "The DmTask of '{0}' is {1}: translated code awaits a task at once, passes it to a Waiting.* call or returns it, and never stores it to await later",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "In translated code (Anomaly.Core.Tg) a DmTask-typed value is used at once as the operand of 'await', an "
            + "argument of a Waiting.* call, or the returned value of its method or lambda (also through '?:', '??', a "
            + "switch expression, a conversion to another task type and IDmTask.AsObjectTask()). Storing a task to "
            + "await it later and awaiting it twice are forbidden (waiting-model.md section 5).");

    /// <summary>ANOM0003: translated code reads a task's state or result directly.</summary>
    public static readonly DiagnosticDescriptor BlockingRead = new(
        id: "ANOM0003",
        title: "A DmTask or kernel awaiter is read directly",
        messageFormat: "'{0}' reads a DmTask or a kernel awaiter directly, which is for the kernel, the shim and tests: await the task, or write Waiting.NoSleep(...) in a synchronous proc",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "GetAwaiter().GetResult() throws on a pending task, and TryGetResult, IsCompleted, IsFaulted and "
            + "Exception observe a task without waiting for it: translated code never uses them (waiting-model.md "
            + "section 3). IDmTask.AsObjectTask() is allowed.");

    /// <summary>ANOM0004: a DmTask proc awaits something that is not a kernel awaitable.</summary>
    public static readonly DiagnosticDescriptor ForeignAwaitable = new(
        id: "ANOM0004",
        title: "A DmTask proc awaits a foreign awaitable",
        messageFormat: "'{0}' awaits '{1}', whose awaiter does not implement IDmAwaiter: a DmTask proc awaits only DmTask, DmTask<T>, sleep and other kernel awaitables",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The DmTask builders refuse a foreign awaiter (Task, ValueTask, Task.Yield, ...) when the proc "
            + "suspends on it, but an already completed one passes unnoticed (waiting-model.md section 3); the analyzer "
            + "closes that hole. 'await foreach' and 'await using' count as awaits.");

    /// <summary>ANOM0005: a [SignalHandler] / [ShouldNotSleep] method (own or inherited) awaits or is async.</summary>
    public static readonly DiagnosticDescriptor AwaitInSyncProc = new(
        id: "ANOM0005",
        title: "A sync-mandated proc awaits",
        messageFormat: "'{0}' is [{1}] (declared on '{2}'), so it stays synchronous: {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "SIGNAL_HANDLER and SHOULD_NOT_SLEEP procs, own or inherited through the overridden chain, are never "
            + "coloured (waiting-model.md section 4.4): no 'async', no DmTask return type, no 'await' of their own. They "
            + "call procs of DmTask slots through Waiting.NoSleep(...). An await inside a nested lambda or local "
            + "function is not the method's own.");
}
