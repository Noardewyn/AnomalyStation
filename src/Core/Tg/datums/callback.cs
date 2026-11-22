// Translated from tgstation (snapshot 1d99bd7f): code/datums/callback.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 *# Callback Datums
 *A datum that holds a proc to be called on another object, used to track proccalls to other objects
 *
 * ## USAGE
 *
 * ```
 * var/datum/callback/C = new(object|null, PROC_REF(procname), arg1, arg2, ... argn)
 * var/timerid = addtimer(C, time, timertype)
 * you can also use the compiler define shorthand
 * var/timerid = addtimer(CALLBACK(object|null, PROC_REF(procname), arg1, arg2, ... argn), time, timertype)
 * ```
 *
 * Note: proc strings can only be given for datum proc calls, global procs must be proc paths
 *
 * Also proc strings are strongly advised against because they don't compile error if the proc stops existing
 *
 * In some cases you can provide a shortform of the procname, see the proc typepath shortcuts documentation below
 *
 * ## INVOKING THE CALLBACK
 *`var/result = C.Invoke(args, to, add)` additional args are added after the ones given when the callback was created
 *
 * `var/result = C.InvokeAsync(args, to, add)` Asyncronous - returns . on the first sleep then continues on in the background
 * after the sleep/block ends, otherwise operates normally.
 *
 * ## PROC TYPEPATH SHORTCUTS
 * (these operate on paths, not types, so to these shortcuts, datum is NOT a parent of atom, etc...)
 *
 * ### proc defined on current(src) object OR overridden at src or any of its parents:
 * PROC_REF(procname)
 *
 * `CALLBACK(src, PROC_REF(some_proc_here))`
 *
 * ### global proc
 * GLOBAL_PROC_REF(procname)
 *
 * `CALLBACK(src, GLOBAL_PROC_REF(some_proc_here))`
 *
 *
 * ### proc defined on some type
 * TYPE_PROC_REF(/some/type/, some_proc_here)
 */
// tg: code/datums/callback.dm:43
[DmPath("/datum/callback")]
public partial class datum_callback : datum
{
    ///The object we will be calling the proc on
    // tg: code/datums/callback.dm:46
    // var/datum/object with the text default GLOBAL_PROC holds wider values: object (rules 3.2).
    public object @object = GLOBAL_PROC;
    ///The proc we will be calling on the object
    // tg: code/datums/callback.dm:48
    public object @delegate;
    ///A list of arguments to pass into the proc
    // tg: code/datums/callback.dm:50
    public DmList arguments;
    ///A weak reference to the user who triggered this callback
    // tg: code/datums/callback.dm:52
    public datum_weakref user;

    /**
     * Create a new callback datum
     *
     * Arguments
     * * thingtocall the object to call the proc on
     * * proctocall the proc to call on the target object
     * * ... an optional list of extra arguments to pass to the proc
     */
    // tg: code/datums/callback.dm:62
    [DmParams("thingtocall", "proctocall")]
    public override async DmTask New(params object[] __extra)
    {
        object thingtocall = Dm.arg(__extra, 0);
        object proctocall = Dm.arg(__extra, 1);
        if (Dm.truthy(thingtocall))
            @object = thingtocall;
        @delegate = proctocall;
        DmList args = Dm.args(Dm.forward(__extra, thingtocall, proctocall));
        if (length(args) > 2)
            arguments = args.Copy(3);
        if (Dm.truthy(usr))
            user = WEAKREF(usr) as datum_weakref;
    }

    /**
     * Qdel a callback datum
     * This is not allowed and will stack trace. callback datums are structs, if they are referenced they exist
     *
     * Arguments
     * * force set to true to force the deletion to be allowed.
     * * ... an optional list of extra arguments to pass to the proc
     */
    // tg: code/datums/callback.dm:79
    // SHOULD_CALL_PARENT(FALSE) is dropped (rules 7.9).
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(force))
            return base.Destroy(force);
        stack_trace(Dm.text("Callbacks can not be qdeleted. If they are referenced, they must exist. ([] [])", Dm.eq(@object, GLOBAL_PROC) ? (object)GLOBAL_PROC : ((datum)@object).type, @delegate), "code/datums/callback.dm", 83);
        return QDEL_HINT_LETMELIVE;
    }

    /**
     * Invoke this callback
     *
     * Calls the registered proc on the registered object, if the user ref
     * can be resolved it also inclues that as an arg
     *
     * If the datum being called on is varedited, the call is wrapped via [WrapAdminProcCall][/proc/WrapAdminProcCall]
     */
    // tg: code/datums/callback.dm:94
    public virtual async DmTask<object> Invoke(params object[] __extra)
    {
        DmList args = Dm.args(__extra);
        if (!Dm.truthy(usr))
        {
            datum_weakref W = user;
            if (Dm.truthy(W))
            {
                mob M = W.resolve() as mob;
                if (Dm.truthy(M))
                {
                    if (Dm.truthy(length(args)))
                        return await Dm.Call(world, nameof(GlobalProcs.push_usr), Dm.arglist(Dm.add(Dm.list(M, this), args)));
                    return await world.push_usr(M, this);
                }
            }
        }

        if (!Dm.truthy(@object))
            return null;

        DmList calling_arguments = arguments;
        if (Dm.truthy(length(args)))
        {
            if (Dm.truthy(length(arguments)))
                calling_arguments = Dm.add(calling_arguments, args) as DmList; //not += so that it creates a new list so the arguments list stays clean
            else
                calling_arguments = args;
        }
        if (Dm.truthy(Dm.bitand(datum_flags, DF_VAR_EDITED)))
        {
            Dm.NotTranslated("code/datums/callback.dm:113", "Invoke: the DF_VAR_EDITED admin proc-call branch (HandleUserlessProcCall, WrapAdminProcCall) is admin tooling");
            return null;
        }
        if (Dm.eq(@object, GLOBAL_PROC))
        {
            try { return await Dm.Call(null, (string)@delegate, Dm.arglist(calling_arguments)); }
            catch (Exception e) { World.Current.ReportRuntime(e); return null; } // isolation boundary (translation-rules.md 9.2)
        }
        try { return await Dm.Call(@object, (string)@delegate, Dm.arglist(calling_arguments)); }
        catch (Exception e) { World.Current.ReportRuntime(e); return null; } // isolation boundary (translation-rules.md 9.2)
    }

    /**
     * Invoke this callback async (waitfor=false)
     *
     * Calls the registered proc on the registered object, if the user ref
     * can be resolved it also inclues that as an arg
     *
     * If the datum being called on is varedited, the call is wrapped via WrapAdminProcCall
     */
    // tg: code/datums/callback.dm:129
    public virtual object InvokeAsync(params object[] __extra)
    {
        return Waiting.WaitforFalse(InvokeAsync__body(__extra, null), null);
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4); it never writes '.' before it may
    // sleep, so it has no box and its caller gets null when it sleeps.
    private async DmTask<object> InvokeAsync__body(object[] __extra, DotBox<object> dot)
    {
        DmList args = Dm.args(__extra);
        if (!Dm.truthy(usr))
        {
            datum_weakref W = user;
            if (Dm.truthy(W))
            {
                mob M = W.resolve() as mob;
                if (Dm.truthy(M))
                {
                    if (Dm.truthy(length(args)))
                        return await Dm.Call(world, nameof(GlobalProcs.push_usr), Dm.arglist(Dm.add(Dm.list(M, this), args)));
                    return await world.push_usr(M, this);
                }
            }
        }

        if (!Dm.truthy(@object))
            return null;

        DmList calling_arguments = arguments;
        if (Dm.truthy(length(args)))
        {
            if (Dm.truthy(length(arguments)))
                calling_arguments = Dm.add(calling_arguments, args) as DmList; //not += so that it creates a new list so the arguments list stays clean
            else
                calling_arguments = args;
        }
        if (Dm.truthy(Dm.bitand(datum_flags, DF_VAR_EDITED)))
        {
            Dm.NotTranslated("code/datums/callback.dm:150", "InvokeAsync: the DF_VAR_EDITED admin proc-call branch (HandleUserlessProcCall, WrapAdminProcCall) is admin tooling");
            return null;
        }
        if (Dm.eq(@object, GLOBAL_PROC))
        {
            try { return await Dm.Call(null, (string)@delegate, Dm.arglist(calling_arguments)); }
            catch (Exception e) { World.Current.ReportRuntime(e); return null; } // isolation boundary (translation-rules.md 9.2)
        }
        try { return await Dm.Call(@object, (string)@delegate, Dm.arglist(calling_arguments)); }
        catch (Exception e) { World.Current.ReportRuntime(e); return null; } // isolation boundary (translation-rules.md 9.2)
    }
}

/**
	Helper datum for the select callbacks proc
 */
// tg: code/datums/callback.dm:161
[DmPath("/datum/callback_select")]
public partial class datum_callback_select : datum
{
    // tg: code/datums/callback.dm:162
    public DmList finished;
    // tg: code/datums/callback.dm:163
    public object pendingcount;
    // tg: code/datums/callback.dm:164
    public object total;

    // tg: code/datums/callback.dm:166  (gaps ST2: translated for the library subsystem's shelves)
    [DmParams("count", "savereturns")]
    public override async DmTask New(params object[] __extra)
    {
        object count = Dm.arg(__extra, 0);
        object savereturns = Dm.arg(__extra, 1);
        total = count;
        if (Dm.truthy(savereturns))
            finished = Dm.list_n(Dm.num(count)); // new(count)
        await DmTask.CompletedTask;
    }

    // tg: code/datums/callback.dm:172  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4; the callback may sleep)
    public virtual void invoke_callback(object index = null, datum_callback callback = null, DmList callback_args = null, float? savereturn = null)
    {
        Waiting.WaitforFalse(invoke_callback__body(index, callback, callback_args, savereturn));
    }

    private async DmTask invoke_callback__body(object index, datum_callback callback, DmList callback_args, float? savereturn)
    {
        savereturn ??= TRUE;
        if (callback == null || !istype(callback, typeof(datum_callback)))
            //This check only exists because the alternative is callback_select would block forever if given invalid data
            throw Dm.CRASH("invalid callback passed to invoke_callback", "code/datums/callback.dm", 176);
        if (!Dm.truthy(length(callback_args)))
            callback_args = Dm.list();
        pendingcount = Dm.num(pendingcount) + 1;
        object rtn = await Dm.Call(callback, nameof(datum_callback.Invoke), Dm.arglist(callback_args)); // callback.Invoke(arglist(callback_args))
        pendingcount = Dm.num(pendingcount) - 1;
        if (Dm.truthy(savereturn))
            finished[index] = rtn;
    }
}

public static partial class GlobalProcs
{
    /**
     * Runs a list of callbacks asyncronously, returning only when all have finished
     *
     * Callbacks can be repeated, to call it multiple times
     *
     * Arguments:
     * * list/callbacks the list of callbacks to be called
     * * list/callback_args the list of lists of arguments to pass into each callback
     * * savereturns Optionally save and return the list of returned values from each of the callbacks
     * * resolution The number of byond ticks between each time you check if all callbacks are complete
     */
    // tg: code/datums/callback.dm:196
    // (gaps ST2: translated for the library subsystem's shelves)
    public static async DmTask<object> callback_select(DmList callbacks = null, DmList callback_args = null, float? savereturns = null, float? resolution = null)
    {
        savereturns ??= TRUE;
        resolution ??= 1;
        if (callbacks == null)
            return null;
        float count = length(callbacks);
        if (!Dm.truthy(count))
            return null;
        callback_args ??= Dm.list();

        callback_args.len = count;

        datum_callback_select CS = Dm.@new<datum_callback_select>(count, savereturns);
        for (int i = 1; i <= count; i++)
            CS.invoke_callback(i, callbacks[i] as datum_callback, callback_args[i] as DmList, savereturns);

        while (Dm.truthy(CS.pendingcount))
            await sleep(resolution.Value * world.tick_lag);
        return CS.finished;
    }
}
