using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (complete). The DM spellings of the waiting primitives (docs/ai/specs/waiting-model.md
// sections 5 and 6); they forward to the Kernel.
public static partial class Dm
{
    /// <summary>
    /// DM <c>sleep(Delay)</c> (BYOND ref <c>/proc/sleep</c>), written <c>await sleep(ds);</c> in an async proc.
    /// Forwards to <see cref="Sleeping.Sleep"/>: a positive delay wakes after max(1, floor(Delay / tick_lag))
    /// ticks; 0, a negative delay or NaN resumes later in the same tick after everything already queued
    /// (waiting-model.md section 8).
    /// </summary>
    public static SleepAwaitable sleep(float Delay) => Sleeping.Sleep(Delay);

    /// <summary>
    /// DM <c>spawn(Delay) { body }</c> (BYOND ref <c>/proc/spawn</c>), written
    /// <c>spawn(d, async () => { ... });</c> after copying the locals the block reads (rules section 8).
    /// Forwards to <see cref="Waiting.Spawn"/>: a negative delay runs the block now until its first sleep, 0 runs
    /// it later in this tick, a positive delay rounds like <see cref="sleep"/>; the block is a detached thread with
    /// the spawning code's <c>usr</c> and an isolation boundary.
    /// </summary>
    public static void spawn(float Delay, Func<DmTask> body) => Waiting.Spawn(Delay, body);

    /// <summary>
    /// DM <c>input(Recipient=usr, Message, Title, Default) as Type in List</c> (BYOND ref <c>/proc/input</c>): a
    /// coloured builtin that sleeps until the client answers. Until the UI milestone it is a stub that completes
    /// at once with null (waiting-model.md section 6); its final signature comes with the UI seam.
    /// </summary>
    public static DmTask<object> input(params object[] args) => DmTask<object>.FromResult(null);

    /// <summary>
    /// DM <c>alert(Usr=usr, Message, Title, Button1="Ok", Button2, Button3)</c> (BYOND ref <c>/proc/alert</c>): a
    /// coloured builtin that sleeps until the client answers. Until the UI milestone it is a stub that completes
    /// at once with null (waiting-model.md section 6).
    /// </summary>
    public static DmTask<object> alert(params object[] args) => DmTask<object>.FromResult(null);
}
