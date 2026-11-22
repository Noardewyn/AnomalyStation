// Translated from tgstation (snapshot 1d99bd7f): code/datums/helper_datums/stack_end_detector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Note (scope risk R10): the detector relies on BYOND freeing the canary as soon as the stack frame that holds it ends
// (reference counting). In C# the canary lives until a garbage collection, so check() turns false only after a GC that
// follows the frame's end, not at the end itself. Only the Failsafe reads check() (failsafe.dm:73), and the Failsafe is not
// ported (DV10), so nothing in the port reads it.

/**
	Stack End Detector.
	Can detect if a given code stack has exited, used by the mc for stack overflow detection.

 **/
// tg: code/datums/helper_datums/stack_end_detector.dm:6
[DmPath("/datum/stack_end_detector")]
public partial class datum_stack_end_detector : datum
{
    // tg: code/datums/helper_datums/stack_end_detector.dm:7
    public datum_weakref _WF;
    // tg: code/datums/helper_datums/stack_end_detector.dm:8
    public datum_stack_canary _canary;

    // tg: code/datums/helper_datums/stack_end_detector.dm:10
    public override async DmTask New(params object[] __extra)
    {
        _canary = Dm.@new<datum_stack_canary>();
        _WF = WEAKREF(_canary) as datum_weakref;
    }

    /** Prime the stack overflow detector.
		Store the return value of this proc call in a proc level var.
		Can only be called once.
	**/
    // tg: code/datums/helper_datums/stack_end_detector.dm:18
    public virtual object prime_canary()
    {
        object dot = null;
        if (!Dm.truthy(_canary))
            throw Dm.CRASH("Prime_canary called twice", "code/datums/helper_datums/stack_end_detector.dm", 20);
        dot = _canary;
        _canary = null;
        return dot;
    }

    /// Returns true if the stack is still going. Calling before the canary has been primed also returns true
    // tg: code/datums/helper_datums/stack_end_detector.dm:25
    public virtual object check()
    {
        return Dm.num(!!Dm.truthy(_WF.resolve()));
    }
}

/// Stack canary. Will go away if the stack it was primed by is ended by byond for return or stack overflow reasons.
// tg: code/datums/helper_datums/stack_end_detector.dm:29
[DmPath("/datum/stack_canary")]
public partial class datum_stack_canary : datum
{
    /// empty proc to avoid warnings about unused variables. Call this proc on your canary in the stack it's watching.
    // tg: code/datums/helper_datums/stack_end_detector.dm:32
    public virtual void use_variable()
    {
    }
}
