// Translated from tgstation (snapshot 1d99bd7f): code/datums/signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum
{
    /**
     * Register to listen for a signal from the passed in target
     *
     * This sets up a listening relationship such that when the target object emits a signal
     * the source datum this proc is called upon, will receive a callback to the given proctype
     * Use PROC_REF(procname), TYPE_PROC_REF(type,procname) or GLOBAL_PROC_REF(procname) macros to validate the passed in proc at compile time.
     * PROC_REF for procs defined on current type or its ancestors, TYPE_PROC_REF for procs defined on unrelated type and GLOBAL_PROC_REF for global procs.
     * Return values from procs registered must be a bitfield
     *
     * Arguments:
     * * datum/target The target to listen for signals from
     * * signal_type A signal name
     * * proctype The proc to call back when the signal is emitted
     * * override If a previous registration exists you must explicitly set this
     */
    // tg: code/datums/signals.dm:16
    public virtual void RegisterSignal(datum target = null, object signal_type = null, object proctype = null, float? @override = null)
    {
        @override ??= FALSE;
        target = turf.dm_identity(target); // TURF-IDENTITY: a replaced turf's reference names its cell's turf, as tg's does
        if (QDELETED(this) || QDELETED(target))
            return;

        if (islist(signal_type))
        {
            DmList signal_type_list = signal_type as DmList;
            string message = Dm.text("([]) is registering [] as a list, the older method. Change it to RegisterSignals.", target.type, signal_type_list.Join(", "));

            if (!Dm.@in(message, RegisterSignal__known_failures))
            {
                RegisterSignal__known_failures[message] = TRUE;
                stack_trace(Dm.text("[] []", target, message), "code/datums/signals.dm", 27);
            }

            RegisterSignals(target, signal_type as DmList, proctype, @override);
            return;
        }

        DmList procs = (_signal_procs is var or__1 && Dm.truthy(or__1) ? or__1 : (_signal_procs = Dm.list()));
        DmList target_procs = (procs[target] is var or__2 && Dm.truthy(or__2) ? or__2 : (procs[target] = Dm.list())) as DmList;
        DmList lookup = (target._listen_lookup is var or__3 && Dm.truthy(or__3) ? or__3 : (target._listen_lookup = Dm.list()));

        object exists = target_procs[signal_type];
        target_procs[signal_type] = proctype;

        if (Dm.truthy(exists))
        {
            if (!Dm.truthy(@override))
            {
                string override_message = Dm.text("[] overridden. Use override = TRUE to suppress this warning.\nTarget: [] ([]) Existing Proc: [] New Proc: []", signal_type, target, target.type, exists, proctype);
                log_signal(override_message);
                stack_trace(override_message, "code/datums/signals.dm", 43);
            }
            return;
        }

        object looked_up = lookup[signal_type];

        if (looked_up == null) // Nothing has registered here yet
            lookup[signal_type] = this;
        else if (!islist(looked_up)) // One other thing registered here
            lookup[signal_type] = Dm.list(looked_up, this);
        else // Many other things have registered here
            Dm.append(ref looked_up, this);
    }

    /// The proc-level static of RegisterSignal (rules 10.6): <c>var/static/list/known_failures</c>.
    // tg: code/datums/signals.dm:21
    private static ref DmList RegisterSignal__known_failures => ref TgState.Current.datum__RegisterSignal__known_failures;

    /// Registers multiple signals to the same proc.
    // tg: code/datums/signals.dm:56
    public virtual void RegisterSignals(datum target = null, DmList signal_types = null, object proctype = null, float? @override = null)
    {
        @override ??= FALSE;
        foreach (object signal_type in Dm.each(signal_types))
            RegisterSignal(target, signal_type, proctype, @override);
    }

    /**
     * Stop listening to a given signal from target
     *
     * Breaks the relationship between target and source datum, removing the callback when the signal fires
     *
     * Doesn't care if a registration exists or not
     *
     * Arguments:
     * * datum/target Datum to stop listening to signals from
     * * sig_typeor_types Signal string key or list of signal keys to stop listening to specifically
     */
    // tg: code/datums/signals.dm:71
    public virtual void UnregisterSignal(datum target = null, object sig_type_or_types = null)
    {
        target = turf.dm_identity(target); // TURF-IDENTITY: a replaced turf's reference names its cell's turf, as tg's does
        DmList lookup = target._listen_lookup;
        if (!Dm.truthy(_signal_procs) || !Dm.truthy(_signal_procs[target]) || !Dm.truthy(lookup))
            return;
        if (!islist(sig_type_or_types))
            sig_type_or_types = Dm.list(sig_type_or_types);
        foreach (object sig in Dm.each(sig_type_or_types))
        {
            if (!Dm.truthy(((DmList)_signal_procs[target])[sig]))
            {
                if (!istext(sig))
                    stack_trace(Dm.text("We're unregistering with something that isn't a valid signal \\[[]\\], you fucked up", sig), "code/datums/signals.dm", 80);
                continue;
            }
            // DM switch with a break that leaves the enclosing for loop: an if chain over a temp (rules 6.7).
            float sw__1 = length(lookup[sig]);
            if (sw__1 == 2)
                lookup[sig] = ((DmList)Dm.sub(lookup[sig], this))[1];
            else if (sw__1 == 1)
            {
                stack_trace(Dm.text("[] ([]) somehow has single length list inside _listen_lookup", target, target.type), "code/datums/signals.dm", 86);
                if (Dm.@in(this, lookup[sig]))
                {
                    Dm.remove(ref lookup, sig);
                    if (!Dm.truthy(length(lookup)))
                    {
                        target._listen_lookup = null;
                        break;
                    }
                }
            }
            else if (sw__1 == 0)
            {
                if (!Dm.eq(lookup[sig], this))
                    continue;
                Dm.remove(ref lookup, sig);
                if (!Dm.truthy(length(lookup)))
                {
                    target._listen_lookup = null;
                    break;
                }
            }
            else
                Dm.remove_at(lookup, sig, this);
        }

        Dm.remove_at(_signal_procs, target, sig_type_or_types);
        if (!Dm.truthy(((DmList)_signal_procs[target]).len))
            Dm.remove(ref _signal_procs, target);
    }

    // tg: code/__DEFINES/dcs/helpers.dm  SEND_SIGNAL(target, sigtype, arguments...): nothing listens -> NONE, no call
    public object SEND_SIGNAL(object sigtype, DmList arguments) => Dm.truthy(_listen_lookup?[sigtype]) ? _SendSignal(sigtype, arguments) : NONE;

    /**
     * Internal proc to handle most all of the signaling procedure
     *
     * Will runtime if used on datums with an empty lookup list
     *
     * Use the [SEND_SIGNAL] define instead
     */
    // tg: code/datums/signals.dm:113
    public virtual object _SendSignal(object sigtype = null, DmList arguments = null)
    {
        object dot = null;
        object target = _listen_lookup[sigtype];
        if (!Dm.truthy(length(target)))
        {
            datum listening_datum = target as datum;
            object result = null;
            try { result = Waiting.NoSleep(Dm.Call(listening_datum, (string)((DmList)listening_datum._signal_procs[this])[sigtype], Dm.arglist(arguments))); }
            catch (Exception e) { World.Current.ReportRuntime(e); result = null; } // isolation boundary (translation-rules.md 9.2)
            return Dm.bitor(NONE, result);
        }
        dot = NONE;
        // This exists so that even if one of the signal receivers unregisters the signal,
        // all the objects that are receiving the signal get the signal this final time.
        // AKA: No you can't cancel the signal reception of another object by doing an unregister in the same signal.
        DmList queued_calls = Dm.list();
        // This should be faster than doing `var/datum/listening_datum as anything in target` as it does not implicitly copy the list
        for (float i = 1, i__end = length(target); i <= i__end; i++)
        {
            datum listening_datum = ((DmList)target)[i] as datum;
            queued_calls.Add(listening_datum, ((DmList)listening_datum._signal_procs[this])[sigtype]);
        }
        for (float i = 1, i__end = length(queued_calls); i <= i__end; i += 2)
        {
            object result = null;
            try { result = Waiting.NoSleep(Dm.Call(queued_calls[i], (string)queued_calls[i + 1], Dm.arglist(arguments))); }
            catch (Exception e) { World.Current.ReportRuntime(e); result = null; } // isolation boundary (translation-rules.md 9.2)
            Dm.combine(ref dot, result);
        }
        return dot;
    }
}

public sealed partial class TgState
{
    /// tg <c>var/static/list/known_failures = list()</c> in <c>/datum/proc/RegisterSignal</c> (rules 10.6).
    // tg: code/datums/signals.dm:21
    internal DmList datum__RegisterSignal__known_failures = Dm.list();
}
