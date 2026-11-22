# Waiting model: how DM `sleep`, `spawn`, `waitfor` and `do_after` are translated

Status: **v1**, 2026-10-05. Implemented in `src/Core/Kernel/` (`DmTask.cs`, `Sleep.cs`, `Waiting.cs`, `Scheduler.cs`, `World.cs`), tested in `tests/Anomaly.Core.Tests/Kernel/`. Chosen by the assistant under D94 (ROADMAP M0, "the one big technical decision") and revised after the wave-0 review; **to be confirmed by the owner at checkpoint C0** (section 14 lists the items to confirm).

v1 replaces v0. What changed: detached calls go through `Waiting.Detach` instead of a bare `_ = Foo();`; synchronous procs call coloured procs through `Waiting.NoSleep`; `set waitfor = FALSE` is translated on the callee side; colouring is per C# virtual slot and generated once for the whole tg tree; delays round down like BYOND; zero-delay work issued by a phase stays in its tick; runtime errors unwind to isolation boundaries, an explicit and recorded deviation from DM.

## 1. The problem

tg code is full of procs that wait: `sleep` (344 uses), `stoplag` (66), `UNTIL` (50), `spawn` (19), `set waitfor = FALSE` (125), `do_after` (800 call sites), `tgui_*` dialogs (about 1,100), `INVOKE_ASYNC` (942), `addtimer` (1,612). BYOND runs each of them as a cooperative thread: a proc that sleeps suspends itself and every caller that waits for it, unless the callee has `waitfor = FALSE` or the call is detached (`spawn`, tg `ASYNC`/`INVOKE_ASYNC`). The core must reproduce this on a single-threaded, deterministic tick (D31) with no `System.Threading.Tasks`, no threads and no real time.

## 2. The choice

| Option | Translation effort | Serializable suspended state | Verdict |
|---|---|---|---|
| Hand-cut procs into segments with an explicit context object (PLAN PT-01) | very high: every caller up the chain is cut too | yes | too slow for a million-line port |
| Roslyn source generator producing explicit, serializable state machines | high: a small compiler; delays M0 by weeks | yes | the fallback if rollback of suspended procs is ever required (section 13) |
| C# iterator coroutines (`IEnumerable<Wait>`) | medium: nested calls and return values need manual plumbing | no | worse ergonomics than async with the same drawback |
| **`async`/`await` on a custom deterministic awaitable (`DmTask`)** | **low: the structure maps 1:1 to the DM source** | **no** | **chosen** |

`DmTask` is not `System.Threading.Tasks.Task`: the kernel's own async method builders use no threads, no synchronization context and no thread pool. Every suspended proc is resumed by the world's scheduler on the simulation tick.

## 3. The model in brief

- A proc that may wait is a C# `async` method returning `DmTask` or `DmTask<T>` (DM `.`/return value as `T`). It runs synchronously until it first suspends. Such a proc that never reaches a suspension completes synchronously, allocates nothing and costs about 3 ns per call level (section 12).
- The only suspension points are kernel awaiters, marked by the interface `IDmAwaiter`: `await sleep(..)` and `await` of another `DmTask`. The builders refuse every other awaiter at the await: `await Task.Delay(..)` is a runtime error at that line instead of a thread-pool continuation running outside the tick. An awaitable that is already complete (`await Task.CompletedTask`, a completed `ValueTask`) never reaches the builder and passes unnoticed at run time (`AForeignAwaitableThatIsAlreadyCompleteIsNotDetected`); it cannot leave the tick, and the analyzer rejects the line at build time (ANOM0004, section 5.6).
- Suspended procs are resumed by the `Scheduler`, a priority queue ordered by (wake tick, insertion sequence), inside `World.Step` (section 7). When a proc returns, the proc awaiting it resumes immediately, before any other queued work (section 11).
- An exception thrown inside an async proc is captured in its task and never escapes the call expression; `await` rethrows it in the caller (section 9).
- A task is consumed exactly once, right away: awaited, or handed to one of the call forms of section 5. It is never stored and awaited later, never awaited twice (the second awaiter gets a runtime error) and never read with `GetAwaiter().GetResult()` in translated code (reading a pending task throws). `DmTask<T>.TryGetResult`, `IsCompleted`, `IsFaulted` and `Exception` exist for the kernel, the shim and tests.

## 4. Colouring: which procs are async

### 4.1 Slots

DM procs are virtual by name, and a C# override must have its base's signature. Colouring is therefore decided per **slot**: one C# virtual hierarchy, identified by its root declaration (the topmost type that declares the proc; the builtin procs of builtin types count as declarations) and the proc name. Unrelated roots with the same name are independent: `/datum/controller/master/Initialize` sleeps (master.dm:323-327), but it is not in the `/atom/Initialize` slot (SHOULD_NOT_SLEEP), which stays synchronous. Builtin root types are their own roots even where tg re-parents them, so `/client/New`, which calls the sleeping `winexists`, is a different slot from `/datum/New`. Each global proc (`/proc/x`) is a slot of its own.

### 4.2 The rule

A slot whose **callers must wait** gets `DmTask`/`DmTask<T>` on the root and on every override, and every call into it is awaited or uses one of the forms of section 5. Callers must wait when some definition in the slot may sleep with an effective `waitfor = TRUE`. A definition may sleep when its own code, outside detached blocks, reaches a sleep primitive (`sleep`, `input`, `alert`, `shell`, `winexists`, `winget`, `world.Export`, `client.MeasureText`, ...), calls a slot whose callers must wait, or calls `..()` into a definition that may sleep with `waitfor = TRUE`.

A definition that may sleep with an effective `waitfor = FALSE` does not make its callers wait: it gets the callee-side wrapper of section 5.4. When every sleeping definition of a slot is `waitfor = FALSE`, the slot stays synchronous.

### 4.3 One list for the whole tree

Colouring cannot be decided file by file: a slot is coloured by an override that may live in a file nobody has translated yet. No override of the Move chain sleeps directly, yet `/atom/Exited` is coloured through `/obj/machinery/harvester/Exited` -> `container_resist_act` -> `do_after` (harvester.dm:184, dna_scanner.dm:72), and `forceMove` through `unbuckle_mob` of punji sticks (towercap.dm:186-193, which calls `do_after` only when not forced). The wave-0 review estimated that 35-54% of all proc definitions are coloured, including the Move chain (`Move`, `forceMove`, `doMove`, `Moved`, `Entered`, `Exited`, `Bump`), `setDir`, `update_icon`, `attackby` and `attack_hand`; `Initialize`, `Destroy`, `Life` and `update_appearance` stay synchronous because their roots are SHOULD_NOT_SLEEP.

So `tools/coloring` computes the colouring once from the whole tg tree (every file the .dme includes), and the result is committed as **`docs/ai/specs/generated/colored-slots.tsv`**; the method, the columns and the known imprecisions are described in **`docs/ai/specs/colored-procs.md`**. The list is an input of translation: a translator looks up the slot and never colours by hand. It is regenerated when the tg snapshot changes; a slot that changes colour is re-translated together with its callers. The C# signatures that follow from the list are generated in **`docs/ai/specs/generated/slots.tsv`** (translation-rules 7.1): `DmTask` exactly for the slots whose callers must wait (`suspends = yes`), never for a slot coloured only through `waitfor = FALSE` definitions (4.2).

Over-colouring changes signatures, not behaviour: an async proc that does not reach a sleep completes synchronously and its awaiting caller continues at once, exactly as in DM.

### 4.4 Synchronous contexts

`SIGNAL_HANDLER` and `SHOULD_NOT_SLEEP(TRUE)` procs, own or inherited, are never coloured: tg's SpacemanDMM lint proves they never sleep. When one of them calls a slot whose callers must wait (a `DmTask` slot; the wave-0 review counted 922 to 4,621 such sites, depending on how calls are resolved; `/atom/update_appearance` does `. |= update_icon(updates)` at atom_appearance.dm:27), the call is written `Waiting.NoSleep(...)` (section 5.3). The same form serves any other synchronous code that calls a proc of a `DmTask` slot: shim and host code, and calls by name (4.5).

### 4.5 Calls by name

`call(obj, "name")(...)`, the `:` operator, untyped receivers, `CALLBACK`/`PROC_REF` targets and signal handlers find the slot at run time. They go through the shim's call by name, which returns `DmTask<object>`: when the callee's slot is a `DmTask` slot, its boxed task is turned into a `DmTask<object>` by `IDmTask.AsObjectTask()` (no reflection; a pending task gets a small adapter proc), and a synchronous callee's value becomes a completed task. An async caller awaits the result; a synchronous caller uses `Waiting.NoSleep`.

### 4.6 What the compiler checks

CS4014 is an error in every project (`Directory.Build.props`): inside an async method a call whose task is dropped (`Foo();`) does not compile, and in a synchronous method it fires for callees declared `async` in the same compilation. It does not catch `_ = Foo();`, a task dropped in a synchronous method when the declaration the call binds to is not `async` (an abstract or expression-bodied root, or one from another assembly), or an `await` inside a sync-mandated proc (checked with a probe project, .NET 10 SDK; CS1998 is not reported for an `async` DmTask method without `await`, which the waitfor wrappers of 5.4 rely on). The waiting-model analyzer (section 5.6, D97) closes these gaps at build time: ANOM0001 reports every dropped task, whatever the declaration the call binds to, ANOM0005 every `async`, `DmTask` return or own `await` of a sync-mandated proc, ANOM0004 a foreign awaitable that the builders let through when it is already complete, and in translated code ANOM0002 and ANOM0003 a stored task and a direct read of one. At run time nothing is silent: an error in a task nobody awaits is reported (section 9.5) and reading a pending task throws.

## 5. Call forms

| Form | Used for | What the caller sees |
|---|---|---|
| `await Foo(..)`, `object x = await Foo(..);` | a call into a `DmTask` slot (`suspends = yes` in `slots.tsv`) from an async proc (DM default, `waitfor = TRUE`) | waits until `Foo` returns and gets its value; `Foo`'s runtime error is rethrown here |
| `Waiting.Detach(Foo(..))`, `object x = Waiting.Detach(Foo(..));` | `INVOKE_ASYNC`, or `ASYNC`/`spawn(-1)` around a single call, of a proc in a `DmTask` slot (`suspends = yes` in `slots.tsv`) | continues as soon as `Foo` first sleeps; gets `Foo`'s value if it completed without sleeping, else default; `Foo`'s errors are reported, never thrown here |
| `Waiting.DetachSync(() => Foo(..))` | the same for a synchronous callee | continues; `Foo`'s error is reported |
| `Waiting.NoSleep(Foo(..))`, `object x = Waiting.NoSleep(Foo(..));` | a call into a `DmTask` slot from a synchronous proc (4.4) | a plain call when `Foo` completes (a captured error is rethrown); if `Foo` slept: a runtime error "proc slept inside a no-sleep context" is reported, default is returned and `Foo` runs on detached |
| wrapper + `__body` with `Waiting.WaitforFalse` | the callee side of `set waitfor = FALSE` (own or inherited) | calls it like any proc and never waits; gets `.` as it was at the callee's first sleep, or the return value when it did not sleep |
| `spawn(delay, async () => { .. })` (`Waiting.Spawn`) | `spawn(d) { block }`, `ASYNC { block }` | continues; the block runs as a detached thread with the caller's `usr` |

Forbidden in translated code: `_ = Foo();` for a `DmTask` (write `Waiting.Detach`), storing a task to await it later, awaiting a task twice, `.GetAwaiter().GetResult()`/`TryGetResult` (kernel, shim and tests only), and awaiting anything that is not a kernel awaitable (`System.Threading.Tasks` in particular). Each is a build error of the analyzer of 5.6 (ANOM0001 to ANOM0004).

Translated code writes DM spellings from the shim (`using static Anomaly.Core.DmShim.Dm;`): `await sleep(ds)` and `spawn(delay, body)` forward to the kernel's `Sleeping.Sleep` and `Waiting.Spawn`; the `Waiting.*` forms are written as they are.

### 5.1 `await`

```csharp
// tg: code/datums/timed_actions.dm:103
public virtual async DmTask<object> @await(object delay = null)
{
    delay ??= world.tick_lag;     // DM default prologue (translation-rules 7.2; a non-constant default, P5)
    object dot = null;
    START_PROCESSING(SStimed_actions, this);
    status = ACTION_WORKING;
    while (status == ACTION_WORKING && world.time < end_time)
        await sleep(world.tick_lag);
    ...
    dot = Dm.num(status == ACTION_SUCCEEDED);
    ...
    return dot;
}

// a caller in a coloured proc
if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
    return;
```

### 5.2 Detached calls

DM `INVOKE_ASYNC(obj, PROC_REF(x), a)` is `spawn(-1) { call(obj, x)(a) }` (`__DEFINES/callbacks.dm:5-20`): the callee runs now until its first sleep, then the caller continues, and the callee's runtime can never stop the caller because it is another thread. `Waiting.Detach(obj.x(a))` does exactly that: by the time `Detach` runs, `x` has already run up to its first suspension; `Detach` reports an error `x` raised before that, and marks a pending task so that an error raised later is reported when `x` finishes. `Detach<T>` returns the value when `x` completed without sleeping (default otherwise), for the rare caller that reads it. A synchronous `x` uses `Waiting.DetachSync(() => obj.x(a))`, which runs it inside a try/catch boundary and restores the caller's `usr` afterwards (section 10); a hot site may inline both instead of allocating the lambda.

### 5.3 `Waiting.NoSleep`

```csharp
// tg: code/game/atom/atom_appearance.dm:16  SHOULD_NOT_SLEEP(TRUE)
[ShouldNotSleep, ShouldCallParent]
public virtual object update_appearance(float? updates = null)
{
    updates ??= ALL;
    object dot = null;
    dot = NONE;
    ...
    if (Dm.truthy(Dm.bitand(updates, UPDATE_ICON)))
        Dm.combine(ref dot, Waiting.NoSleep(update_icon(updates)));
    return dot;
}
```

tg's lint guarantees the callee does not sleep on these paths (`update_icon` is coloured because some override can sleep, not because this call does). If it does sleep anyway, that is a runtime: `NoSleep` reports it with the call expression, returns default and lets the callee finish detached (its later error is reported too). A captured error of a completed callee is rethrown, like a synchronous call.

### 5.4 `set waitfor = FALSE`: the callee-side wrapper

In DM `waitfor = FALSE` is a property of the callee: when it or one of its callees sleeps, its caller continues at once and receives the `.` the callee had at that moment (BYOND reference, waitfor setting). It is inherited by every override (OpenDream `DMCodeTree.Procs.cs` copies `DisableWaitfor` from the parent proc; tg never resets it), and it can differ between overrides of one slot (`/area/Entered` has it, areas.dm:582, the turf and atom `Entered` do not). Callers use the value: `thing.process(wait * 0.1) == PROCESS_KILL` (processing.dm:28), `var/state = queue_node.ignite(...)` (master.dm:902), `!extra_checks.InvokeAsync()` (timed_actions.dm:123).

So the definition is translated as a wrapper (the override, with the slot's signature) and a private async body that writes `.` through a `DotBox<T>`:

```csharp
// tg: code/controllers/subsystem.dm:139
[ShouldNotOverride]
public virtual object ignite(float? resumed = null)
{
    DotBox<object> dot = new DotBox<object>();
    return Waiting.WaitforFalse(ignite__body(resumed, dot), dot);
}

private async DmTask<object> ignite__body(float? resumed, DotBox<object> dot)
{
    resumed ??= FALSE;                                             // DM default prologue (translation-rules 7.2)
    dot.Value = SS_IDLE;
    tick_allocation_last = Master.current_ticklimit - TICK_USAGE;
    tick_allocation_avg = MC_AVERAGE(tick_allocation_avg, tick_allocation_last);
    dot.Value = SS_SLEEPING;
    try { await fire(resumed); }
    catch (Exception e) { World.Current.ReportRuntime(e); }        // isolation boundary (translation-rules.md 9.2)
    dot.Value = state;
    if (state == SS_SLEEPING)
    {
        slept_count++;
        state = SS_IDLE;
    }
    ...
    return dot.Value;
}
```

Rules:
- Every definition with an effective `waitfor = FALSE` whose body may sleep gets the pair. The wrapper has the slot's signature (translation-rules 7.1-7.4: `float?`/`object` parameters with `= null` defaults, an `object` or narrowed return type); the body is `private async DmTask<T> <name>__body(<params>, DotBox<T> dot)` with the same parameters and the `T` of the slot's return type, and it starts with the DM default prologue (translation-rules 7.2), so the defaults are applied before the first sleep exactly as in a plain proc. Inside the body every read and write of `.` goes through `dot.Value` (instead of the `dot` local of translation-rules 7.4), and every `return;` or fall-off returns `dot.Value`. A proc without a value uses `Waiting.WaitforFalse(DmTask body)` and no box.
- When the body never assigns `.` before a point where it may sleep, pass `null` instead of a box: the caller then gets default (DM null) at the sleep, and nothing is allocated. Hot slots such as `process()` use this.
- In a synchronous slot the wrapper is a plain method (`ignite`, `process`). In a slot that suspends its callers through other overrides (`Entered`, a `DmTask` slot), the wrapper is an `async` method with the same body: it always returns a completed task, so its awaiting caller never blocks.
- `..()` calls `base.<name>(..)`, the parent's wrapper, which returns at the parent's first sleep. A definition whose body can sleep only through `..()` therefore needs no wrapper of its own; a definition whose body cannot sleep at all is a plain method (`/datum/process` is `return PROCESS_KILL`).
- Once the wrapper has returned, the body runs on detached: its later return value is discarded and a later runtime error is reported.

Tests (`CallFormTests`): `ignite()` returns `SS_SLEEPING` when `fire()` sleeps and the stand-in MC skips that subsystem until the tail of `ignite` sets it idle; a `fire()` runtime ends only `fire()` and the MC keeps running; `process()` returning `PROCESS_KILL` synchronously stops processing, while a `process()` that sleeps first (pod.dm:25-30, "can't rely on return PROCESS_KILL") hands back null and stays processed; `..()` keeps the parent's `.`; `/area/Entered` in the coloured `Entered` slot never blocks `Move`.

### 5.5 `spawn`

`spawn(delay, async () => { block })` (`Waiting.Spawn`). A negative delay (`spawn(-1)`, tg `ASYNC`) runs the block now until its first sleep, then the caller continues; 0 runs it later in this tick after everything already queued; a positive delay rounds like `sleep`. The block is a detached thread: it runs with the `usr` of the spawning code, it is an isolation boundary, and its value is discarded.

DM copies the proc's locals into the spawned block (BYOND reference, spawn), while a C# lambda shares captured variables (a loop variable shows its last value in every block). The translator copies every local the block reads into a fresh local `<name>__s` declared right before the spawn (translation-rules section 8): `float i__s = i; spawn(1, async () => { use(i__s); });`. The same holds for `ASYNC { }` blocks that read locals after their first sleep. `INVOKE_ASYNC` needs nothing: its arguments are evaluated at once. Pinned by `SpawnedBodiesSeeCopiedLocalsOnlyWhenTheTranslatorCopiesThem`.

### 5.6 The analyzer (D97)

`tools/Anomaly.Analyzers` is a Roslyn analyzer that every project built with the repository's `Directory.Build.props` loads: the kernel, the shim, `Anomaly.Core.Tg`, the server, the tools and `tests/Anomaly.Core.Tests` (not the Godot client, which has its own props, nor the analyzer's own tests). Its diagnostics are errors, so the 0-warning, 0-error build refuses a violation. It finds the kernel's types by full metadata name (`Anomaly.Core.Kernel.DmTask`, ``DmTask`1``, `IDmTask`, `IDmAwaiter`, `Waiting`; `Anomaly.Core.DmShim.SignalHandlerAttribute`, `Anomaly.Core.DmShim.ShouldNotSleepAttribute`) and does nothing in a compilation that does not see the kernel. "A task" below is a value of type `DmTask`, `DmTask<T>` or `IDmTask`.

| Id | Reports | Scope |
|---|---|---|
| ANOM0001 | a dropped task: an expression statement (also `obj?.Foo();`, an expression-bodied `void` method and a lambda converted to `Action`, so `Waiting.DetachSync(() => Foo())` for a coloured `Foo`) or a discard (`_ = Foo();`, `var _ = Foo();`, a discard in a deconstruction, `out _`), whatever the declaration the call binds to | every analyzed project |
| ANOM0002 | a stored task: a task that is not used at once as the operand of `await`, an argument of a `Waiting.*` call, or the returned value of its method or lambda; the value is followed through `?:`, `??`, switch-expression arms, `?.`, a conversion to another task type and `IDmTask.AsObjectTask()`. It is reported where it is stored (a variable, a field, an `out`/`ref` argument, a tuple or an array), passed to another method or converted to `object`; awaiting a task twice needs a stored task, so it is caught there | translated code: a project with the MSBuild property `$(AnomalyTranslatedCode)` set to `true` (`Anomaly.Core.Tg`) |
| ANOM0003 | a direct read: an instance member of a task other than `AsObjectTask()` (`GetAwaiter()`, `TryGetResult`, `IsCompleted`, `IsFaulted`, `Exception`), an explicit `GetAwaiter()` of a kernel awaitable (`sleep(1).GetAwaiter()`), a member of a stored kernel awaiter | translated code |
| ANOM0004 | a foreign awaitable: an `await`, `await foreach` or `await using` in a function that returns `DmTask` or `DmTask<T>` (a method, lambda or local function) whose awaiter does not implement `IDmAwaiter`: `Task`, `ValueTask`, `Task.Yield()`, `dynamic` | every analyzed project |
| ANOM0005 | an `async` modifier, a `DmTask` return type or an own `await` on a method marked `[SignalHandler]` or `[ShouldNotSleep]` or overriding one that is (the attributes are `Inherited = false`, so the analyzer walks the overridden chain itself, tg's "own or inherited" of 4.4); an `await` in a nested lambda or local function is that function's, not the method's | every analyzed project |

Not reported: the call forms of the table in section 5, an expression-bodied pass-through (`=> base.Foo()`, `=> _AddElement(..)`, `=> DmTask.CompletedTask`), a call by name's `task.AsObjectTask()` returned or awaited, the static members `DmTask.CompletedTask`, `FromResult` and `FromException`. When the analyzer was turned on (start of M1) translated code used its 330 tasks in exactly these ways (263 `await`s, 51 `Waiting.NoSleep`, 6 `Waiting.WaitforFalse`, 3 `Waiting.Detach`, 7 returns) and `src/` and `tools/` had no violation.

A deliberate exception (a kernel, shim or host helper that keeps a task; a test that drops one or awaits a foreign awaiter on purpose) is a local `#pragma warning disable ANOM000x` with a one-line reason. `src/` and `tools/` have none; `tests/Anomaly.Core.Tests` has them where tests start do_afters they watch without awaiting (`DoAfterContractTests` file-wide, `MobDeviationTests`) and where they pin the builders' run-time refusal of a foreign awaiter (`CallFormMatrixTests`, `ErrorModelTests`).

The analyzer's tests compile snippets against the real kernel and shim assemblies with plain Roslyn: `dotnet test tests/Anomaly.Analyzers.Tests`, or both suites with `dotnet test AnomalyStation.sln`. Cost: about 0.05 s of analyzer time in a clean compile of `Anomaly.Core.Tg` (Roslyn's `ReportAnalyzer`), within the run-to-run noise of the project's build time.

## 6. DM constructs and their C# form

| DM | C# | Notes |
|---|---|---|
| `sleep(ds)` | `await sleep(ds);` | positive `ds`: `max(1, floor(ds / tick_lag))` ticks (section 8) |
| `sleep(0)`, `sleep()` | `await sleep(0);` | yields: resumes later in the same tick, after everything already queued |
| `sleep(-1)` | `await sleep(-1);` | same as `sleep(0)`: BYOND's backlog check is not modelled (tg has no direct use) |
| `stoplag(ds)` | `await stoplag(ds);` | tg global proc (`__HELPERS/stoplag.dm`), coloured; with the virtual tick usage of 0 its loop runs once and sleeps `initial_delay` (default `world.tick_lag`; one tick while the MC has not finished initializing) |
| `UNTIL(X)` | `while (!<X as a condition, translation-rules 4.1>) await stoplag();` (`while (!Dm.truthy(target)) await stoplag();`) | `__DEFINES/_helpers.dm:31`; translation-rules 8, 11.3 |
| `SLEEP_CHECK_DEATH(X, A)`, `SLEEP_NOT_DEL(t)` | the expanded macro with `await sleep(..)` | |
| `CHECK_TICK`, `TICK_CHECK`, `MC_TICK_CHECK` | not a suspension | virtual tick usage 0 (translation-rules 11.3, DV3); never seeds colouring |
| `spawn(d) { block }` | `spawn(d, async () => { block });` | copy the locals the block reads first (5.5) |
| `spawn(-1) { block }`, `ASYNC { block }` | `spawn(-1, async () => { block });`, or `Waiting.Detach(Foo(..));` when the block is one call | runs now until the first sleep, then the caller continues |
| `INVOKE_ASYNC(obj, PROC_REF(x), a)` | `Waiting.Detach(obj.x(a));` (`x` in a `DmTask` slot), `Waiting.DetachSync(() => obj.x(a));` (synchronous `x`, also one coloured only through `waitfor = FALSE`) | through the call by name (4.5) when the receiver type is not known; a bare `_ = obj.x(a);` is forbidden |
| `INVOKE_ASYNC(GLOBAL_PROC, GLOBAL_PROC_REF(x), a)` | `Waiting.Detach(x(a));` (`x` a `DmTask` global proc), `Waiting.DetachSync(() => x(a));` (synchronous `x`) | `x` is a `GlobalProcs` member |
| `set waitfor = FALSE` / `= 0` (own or inherited) | wrapper + `__body` (5.4) | callers call it normally |
| `set waitfor = TRUE` / `= 1` | nothing | the default |
| `do_after(user, delay, target, ..)` | `await do_after(user, delay, target, ..)` | `/proc/do_after` (timed_actions.dm:211) is coloured; the `timed_action` datum and `SStimed_actions` are translated as tg (7.3) |
| `callback.Invoke(..)` | `await cb.Invoke(..)` in an async proc, `Waiting.NoSleep(cb.Invoke(..))` in a synchronous one | returns `DmTask<object>`: its target is found at run time and the colouring treats a call by name as one that may sleep; `Invoke` is an isolation boundary (9.2) |
| `callback.InvokeAsync(..)` | `cb.InvokeAsync(..)` | `set waitfor = FALSE` (callback.dm:129): a synchronous wrapper returning the target's value if it did not sleep, else null; timers call their callbacks this way (timer.dm:125, :172) |
| `tgui_alert`, `tgui_input_*`, `tgui_input_list`, ... | `await tgui_alert(..)` | translated tg procs, coloured; they poll with `stoplag(1)` until the player answers (tgui_input/alert.dm:90-92), so they need nothing beyond `sleep`; stubs until the UI milestone |
| `input()`, `alert()` (builtins) | `await input(..)`, `await alert(..)` | shim builtins, coloured (they sleep until the client answers); stubs that complete at once with null until the UI milestone |
| `shell`, `winexists`, `winget`, `world.Export`, `client.MeasureText` | coloured builtins or `NotTranslated` | they sleep in BYOND, so they seed the colouring |
| `addtimer(CALLBACK(..), wait)` | the translated `SStimer` | callbacks run through `InvokeAsync`; `addtimer` applies its own `CEILING` to `tick_lag`, so section 8 does not change timers |
| `usr` | `usr` (the `GlobalProcs` accessor typed `mob` over `World.usr`, translation-rules 10.8) | section 10 |
| `try { } catch (var/exception/e) { }` | C# `try { } catch (Exception ..) { }`, spelled as in translation-rules.md | also catches a callee's runtime raised after the callee slept (9.4) |

## 7. Tick order

### 7.1 `World.Step`

1. `Tick++`; `world.time = Tick * tick_lag` (0.5 ds at 20 TPS).
2. `Rng.Reseed(seed, Tick)`.
3. `Scheduler.RunDue(Tick)` with `usr = null`: every entry due at or before this tick, in (wake tick, sequence) order, including entries made due while draining (`sleep(0)`, `spawn(0)`). This is BYOND's step 2 in tg's `.github/guides/TICK_ORDER.md`: sleeping procs resume in the order they went to sleep (master.dm:678, "sleeps are processed in the order received").
4. Each registered phase in registration order (`World.AddPhase`): `usr = null`, run the phase (an isolation boundary), then `Scheduler.RunDue(Tick)` again, so zero-delay work the phase issued resumes in the same tick. The phases are tg's `world/Tick` (added by `TgWorld.Boot`, before every host phase: BYOND calls it after the sleepers and before SendMaps), the player command phase (BYOND step 6: client verbs run after SendMaps) and, later, replication.

Work issued outside a `Step` (host code inside `world.Enter()`, tests) runs in the first drain of the next `Step`.

### 7.2 The Master Controller is a coroutine

`Master.Loop` (master.dm:588) is translated literally as an async proc started detached at world start. It sleeps `world.tick_lag * (processing * sleep_delta)` (master.dm:682, :793) like any proc, so it wakes inside step 3 among the other sleepers, and every subsystem fires there. There is no "subsystem phase". A subsystem that sleeps in `fire()` is handled by `ignite` (5.4); the tick-limit heuristics run on the virtual tick usage of 0.

### 7.3 Consequence: the do_after poll order

`timed_action.await` sleeps `world.tick_lag` per iteration and `SStimed_actions`, a ticker subsystem, polls `process()` each tick from the MC. Which of the two runs first in a tick depends on which went to sleep first in the tick before, and that depends on where the do_after started:

| A 1 s do_after started at tick 10 | Order in each later tick | Polls | Last poll | Result |
|---|---|---|---|---|
| by a player command (a phase, after the MC fired in tick 10) | the MC polls, then the await wakes | 20 | tick 30, by the MC | TRUE at tick 30 |
| inside the MC (from a subsystem fire) | the await wakes, then the MC polls | 21 | tick 30, by the await itself | TRUE at tick 30 |

The result and its tick are the same; the poll count differs by one, so an `extra_checks` callback runs 20 or 21 times. The first row is BYOND's FIFO rule for verbs; tg's comment at timed_actions.dm:110 ("await will tick first before the subsystem itself") describes the second. Pinned by `SchedulerTests.ADoAfterStartedByAPlayerCommandIsPolledByTheMcBeforeItsAwaitWakes` and `ADoAfterStartedInsideTheMcWakesBeforeTheMcPollsIt`, which use a stand-in MC coroutine. Neither row has been checked against a real BYOND run.

## 8. Delays and limits

- **Rounding.** `sleep(ds)` and `spawn(ds)` with a positive `ds` wake after `max(1, floor(ds / tick_lag))` ticks, the BYOND/OpenDream rule (OpenDream `ProcScheduler.Delays.cs` rounds down with a one-tick minimum; tg `config/config.txt:470` says BYOND rounds timer values down). A relative epsilon of 1e-6 keeps exact multiples computed in float (`0.1f * 15` is 3 ticks). Examples: 0.3 -> 1, 0.5 -> 1, 0.55 -> 1, 0.75 -> 1, 1 -> 2, 1.1 -> 2, 1.25 -> 2, 1.5 -> 3, 4.3 -> 8, 10 -> 20 ticks. The MC's sleep with a fractional `sleep_delta` therefore stays at one tick, as in tg.
- **Zero and below.** 0, negative and NaN mean the same tick, after everything already queued.
- **Huge delays.** tg `INFINITY` (1e31), +inf and anything beyond the 64-bit tick range mean never; tick additions saturate and nothing wraps around.
- **Exact ticks and raw entries.** `Sleeping.SleepTicks(n)` sleeps exactly `n` ticks. `Scheduler.At(tick, action)` and `After(ticks, action)` add raw entries for kernel and host code; translated code uses `sleep` and `spawn`.
- **Host timing.** `World.StepDeciseconds(ds)` steps until at least `ds` of game time have passed (`Scheduler.TicksCovering`, ceiling); it is not DM rounding.
- **Nothing is dropped.** A chain of more than `Scheduler.MaxZeroDelayHops` (100,000) zero-delay hops in one tick is a runaway loop: a sleeping proc gets a `WaitingException` at its `sleep` (it unwinds to its boundary and is reported there), a spawned block or raw entry is reported and moved to the next tick. `MaxRunsPerDrain` (1,000,000) is only a backstop: the drain stops, reports, and leaves the remaining entries queued with their keys, so they run first in the next drain (the re-drain after the next phase, or the first drain of the next tick). A runaway that catches its own `WaitingException` and keeps going is cut at every hop and stopped by the backstop in every drain; the other procs still run in order.

## 9. Runtime errors: a deliberate deviation

### 9.1 DM and the port

In DM an uncaught runtime error ends only the failing proc, and its caller continues with that proc's `.` (BYOND reference, throw; OpenDream `DreamThread` pops one frame), unless a `try`/`catch` is active further up the stack, in which case control unwinds to it.

The port deviates. An exception thrown inside an async proc is captured in its task and never leaves the call expression; `await` rethrows it in the caller, so it unwinds through its callers to the nearest **isolation boundary**, which reports it through `World.ReportRuntime` (counted in `RuntimeErrorCount`, which is part of the state hash) and carries on. Synchronous procs propagate exceptions the same way. The deviation is recorded in `docs/ai/DEVIATIONS.md`: runtime errors are bugs (D86), and reproducing DM exactly would need a catch frame that returns `.` in every translated proc. `ErrorModelTests.AwaitedCalleeRuntimeAfterSleepUnwindsTheCallerToItsBoundary` pins it.

### 9.2 Isolation boundaries

Provided by the kernel: each scheduler entry (a woken sleeper, a spawned block, a raw entry); each `World` phase; `Waiting.Detach`, `DetachSync` and `Spawn`; the detached remainder of a callee after `NoSleep` or a `waitfor = FALSE` wrapper returned.

Written in translated code, at the call: the `fire()` call inside `ignite` (so `RunQueue` maps the leftover `SS_RUNNING` to `SS_IDLE`, as in DM); each timer callback; each signal handler call inside `_SendSignal` (signals.dm:113); each callback `Invoke`/`InvokeAsync`; each per-item `process()` in processing subsystems and `Life()` in `SSmobs`; `InitAtom` during atom initialization; the `Destroy()` call inside `/proc/qdel` (garbage.dm:375: a runtime in `Destroy` ends `Destroy` and qdel carries on with a null hint, as in DM); the `subsystem.Initialize()` call inside the Master's `init_subsystem` (master.dm:501); each `LateInitialize()` in SSatoms' late-loader loop (atoms.dm:70).

Written in the engine parts, where BYOND itself starts a proc: the engine's call of `client/New` for a connecting player (`ClientSeam.Connect`, M1), whose runtime refuses the connection and is recorded for the host (`ClientConnection.Error`).

The spelling is

```csharp
try { await fire(resumed); }
catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)
```

### 9.3 Procs that read `.` after a runtime

tg code that inspects `.` after a runtime, or relies on a runtime ending only the proc that raised it, gets an explicit try/catch where it is translated, whose catch reports the error and returns the `.` the DM proc had at that point: `Recreate_MC` (master.dm:257, `. = -1 //so if we runtime, things know we failed`), `CheckQueue` (:797, `. = 0`), `RunQueue` (:832, "Returns 0 if runtimed"), `_stack_trace` (`__HELPERS/stack_trace.dm:3-4`, "without ending the current proc"). The list is kept in translation-rules.md 9.3.

### 9.4 DM `try`/`catch`, `CRASH`, `stack_trace`

A DM `try`/`catch` becomes a C# `try`/`catch`; it also catches a callee's runtime raised after the callee slept, because `await` rethrows it. Every C# exception counts as a DM runtime error, whatever its type (an `InvalidCastException` from a cast is DM's runtime on a value of the wrong type), so boundaries and catches take `Exception`. `CRASH()` throws, and `stack_trace()` reports through `World.ReportRuntime`, at the location of its use site, and returns; their exact C# spelling (and that of the DM exception datum in a `catch`) belongs to translation-rules.md (9.1, 9.5).

### 9.5 Errors are never lost and never doubled

- A task that fails before its call expression returns keeps the error for its caller: `await` rethrows it, `Detach` reports it, `NoSleep` and the waitfor wrapper rethrow it.
- A task that fails when nobody awaits it (detached by design, or dropped by mistake) is reported once, when it completes.
- Misuse is a runtime error at the misuse: awaiting a foreign awaiter, awaiting a task twice (only the second awaiter faults), reading a pending task, sleeping with no ambient `World` (the task faults; `Detach` rethrows because there is no world to report to).

## 10. `usr`

In DM `usr` is an implicit argument of every call: "each procedure inherits the value from its caller" (BYOND reference, usr var (proc)), so whatever a callee assigns, its caller keeps its own. tg relies on it. `world.push_usr` (`__HELPERS/mobs.dm:709-716`) restores `usr` only after the invoked callback returns, which is after the callback's sleep when it sleeps; `callback.InvokeAsync` (`set waitfor = FALSE`, callback.dm:129) pushes its stored user only `if(!usr)`, so `SStimer.fire` calling it for one timer after another depends on `usr` still being null after an earlier callback slept. `SSverbs.invoke_verb` (`subsystem/verbs.dm:33`, `:58`) assigns `usr` and never restores it.

The port has one ambient `World.usr` and gives it these per-call semantics:
- The builder of a coloured proc saves `usr` when the proc is called and puts it back when the call expression returns, whether the proc completed, failed or suspended. So an awaiting caller, `Waiting.Detach`, `Waiting.NoSleep` and a waitfor wrapper always continue with their own `usr`.
- A caller that awaited a pending task resumes with the `usr` it had when it began to wait, also when the resumption goes through the completion trampoline (section 11).
- Every scheduler entry captures the `usr` that was ambient when it was scheduled (a `sleep`, a `spawn`, `Scheduler.At`) and runs with it, restoring the previous value afterwards. So a proc keeps its `usr` across its sleeps, and spawned blocks and timers created during a player command keep that player.
- `Waiting.Spawn(-1, ..)` and `Waiting.DetachSync` restore the caller's `usr` when the body returns.
- Each phase starts with `usr = null`; the command phase sets it per command.

The kernel cannot see inside a synchronous (not `async`) proc, so a translated synchronous proc that assigns `usr` saves it on entry and restores it on every exit: `mob usr__caller = usr; try { ... } finally { usr = usr__caller; }` (translation-rules 7.4, 10.8). tg assigns `usr` in a few dozen lines, most of them in coloured procs. A kernel awaitable added later besides `DmTask` and `sleep` must capture the waiting proc's `usr` and re-impose it on resumption, as `DmTask` and the scheduler do. Pinned by `UsrTests` and `UsrAttackTests` (the `SStimer`/`InvokeAsync` case is `TimerCallbacksEachRunWithTheirOwnUserWhenAnEarlierOneSlept`).

No closure is allocated per sleep: the entry carries the `usr`. The save and restore on every coloured call is included in the numbers of section 12.

## 11. Completion order and depth

When a proc returns, the proc awaiting it resumes at once on the same native stack, then its own caller, and so on: DM's "the caller continues as soon as the callee returns", before any other queued work. A deep awaiting chain would nest native frames for every level, so beyond 64 nested completions the next resumption is queued on the world and run by the outermost completion frame as soon as the inner frames unwind. Those inner frames are finished procs returning from their completion, so nothing observable runs in between and the order is unchanged. `CompletionDepthTests` runs a 50,000-level chain on a 1 MB thread (about 4,400 levels overflowed it before) and checks that queued resumptions still run before the next scheduled entry.

## 12. Hot-path costs

Measured by `tests/Anomaly.Core.Tests/Kernel/HotPathBenchmarkTests.cs` (`dotnet test tests/Anomaly.Core.Tests -c Release --filter HotPath --logger "console;verbosity=detailed"`). Release build (struct state machines), Apple M1 Pro, .NET 10, 2026-10-05, several runs while other builds shared the machine; the test reports and does not gate. The numbers include the per-call `usr` save and restore of section 10, which did not move the synchronous chain outside its run-to-run noise.

| Case | Time | Allocated |
|---|---|---|
| 4-deep virtual call chain, plain `int` | 3.7-3.9 ns per call | 0 B |
| the same chain as `async DmTask<int>`, completing synchronously | 16-17 ns per call | 0 B |
| `waitfor = FALSE` wrapper with a `DotBox`, body completes | 5-10 ns per call | 24 B (the box; 0 with a null box) |
| first suspension of a proc (state, boxed state machine, resume delegate, queue slot) | | about 310 B |
| its wake-up and completion | | 0 B |
| 10,000 procs waking every tick, about half of them awaiting a callee that sleeps | 4.0-4.7 ms per tick | 900 KB per tick (92 B per wake-up), 7-8 gen0 GCs in 50 ticks |

Debug builds use class state machines and allocate on every call; quote Release numbers. In short: a coloured call level that does not sleep costs about 3 ns and nothing on the heap, so over-colouring is cheap; a proc pays one allocation burst at its first suspension and nothing for later sleeps of the same frame; 10,000 wake-ups per tick take under a tenth of the 50 ms tick.

## 13. What is lost

A suspended proc is a compiler-generated state machine and cannot be snapshotted. D31 asks for do_after and dialogs as serializable data with continuations: the `timed_action` datum, its progress and the dialog state are plain data, but the awaiting proc frame is not. Consequences, accepted for now:
- **Prediction rollback** (D38 stage 1, M10) covers movement, hands and doors: the predicted subset must not depend on suspended procs, or a suspended proc cancels the prediction of that command (cancel-by-default is already the PLAN's contract).
- **Crash-recovery snapshots** (D71) cannot restore sleeping procs; objects mid-animation settle to their final state on restore. Timers (`addtimer` entries) are plain data and are restored.
- **State hashing** includes the RNG state, the scheduler's sequence counter and the wake tick and sequence of every pending entry, not the suspended frames themselves; two runs that diverge only inside a suspended frame are detected when the frame resumes.

If one of these blocks a milestone, the fallback is the generated-state-machine option of section 2, applied to the same translated code: the call forms of section 5 stay the same, so the cost is in the generator, not in the translated code.

Also not modelled: BYOND ends a sleeping proc whose `src` was hard-deleted (tg uses `qdel`, and hard deletes are garbage-collection failures that the port does not have); `sleep(-1)`'s backlog check (8); procs resumed by `walk()` before other sleepers (tg calls `walk` only from SDQL admin wrappers, which are not translated).

## 14. Open items for checkpoint C0

1. The runtime-error deviation (section 9) and its boundary list.
2. Delay rounding (section 8) comes from OpenDream and tg's config comment, not from a BYOND run.
3. The do_after poll order (7.3) follows BYOND's documented FIFO rule; no BYOND run confirms which proc wakes first.
4. A Roslyn analyzer that flags an unawaited `DmTask` in any method (including `_ =` discards), any `await` inside `[SignalHandler]`/`[ShouldNotSleep]` methods and any `await` of a non-kernel awaitable (the completed ones pass the run-time check, section 3). D88 defers linters; this one protects translation correctness, so it needs the owner's call. **Done** at the start of M1 (D97): section 5.6.
5. Suspended procs are not serializable (section 13).

## Kernel API reference

| Type | Members used by translated code, the shim and hosts |
|---|---|
| `DmTask`, `DmTask<T>` | `async` return types; `IsCompleted`, `IsFaulted`, `Exception`, `TryGetResult` (`DmTask<T>`), `CompletedTask`, `FromResult`, `FromException`; `IDmTask.AsObjectTask()` for calls by name |
| `IDmAwaiter` | marker of the awaiters a `DmTask` proc may await |
| `Sleeping` | `Sleep(ds)`, `SleepTicks(n)` (the shim's `sleep` forwards to `Sleep`) |
| `Waiting` | `Detach`, `Detach<T>`, `DetachSync`, `NoSleep`, `NoSleep<T>`, `WaitforFalse`, `WaitforFalse<T>`, `Spawn` (the shim's `spawn` forwards to it) |
| `DotBox<T>` | `.` of a `waitfor = FALSE` body |
| `Scheduler` | `At`, `After`, `RunDue`, `TicksFromDeciseconds`, `TicksCovering`, `MaxZeroDelayHops`, `MaxRunsPerDrain`, `Pending`, `Sequence` |
| `World` | `Step`, `Step(n)`, `StepDeciseconds`, `AddPhase`, `Enter`, `Current`, `usr`, `ReportRuntime`, `RuntimeErrorSink`, `RuntimeErrorCount`, `ComputeStateHash`, `AddStateHasher`, `Tick`, `Time`, `TickLag`, `Rng`; `Objects` and `GetExtension` (object-model.md sections 6 and 10) |
| `WaitingException` | runtime errors raised by the waiting model itself (runaway loop, slept in a no-sleep context, stopped drain) |
