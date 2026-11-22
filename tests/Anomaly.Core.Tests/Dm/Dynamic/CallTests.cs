using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// Calls by name, <c>Dm.Call</c> (rules 7.3, 7.7; object-model.md section 12; waiting-model.md 4.5): resolution, the
/// argument adaptation matrix, named arguments and <c>arglist</c>, return shapes, exceptions.
/// </summary>
public class CallTests
{
    private static object Run(object src, string proc, params object[] args) => Waiting.NoSleep(Dm.Call(src, proc, args));

    // ---- resolution ----

    [Fact]
    public void ACallRunsTheMostDerivedDefinition()
    {
        // BYOND ref /proc/call: the proc is looked up on the object; procs are virtual by name (rules 7.1).
        var sheet = new fx_sheet();
        Assert.Equal(2f, Run(sheet, "use", "glass"));
        Assert.Equal(new object[] { "glass", 0f, 1f }, sheet.used_with);   // the stack override's prologue ran
        Assert.Equal(1f, Run(new fx_item(), "use", "cloth"));
    }

    [Fact]
    public void ADmNameFindsAProcRenamedForAVarClash()
    {
        // Rules 2.4: the proc of a var/proc clash is x__proc; the binder maps the DM name back.
        var callee = new fx_callee();
        Assert.Equal(7f, Run(callee, "charge", 2));
        Assert.Equal(9f, Run(callee, nameof(fx_callee.charge__proc), 2));
        Assert.True(hascall(callee, "charge"));
        Assert.Equal(9f, get_var(callee, "charge"));                   // the var keeps its name
    }

    [Fact]
    public void AHidingNewIsTheOneThatRuns()
    {
        // Rules 7.1: /client/New is its own slot; its C# method hides the inherited one ("public new virtual").
        var client = new fx_client();
        Run(client, "New", 1, 2);
        Assert.Equal(new[] { "client.New 2" }, client.log);
        var sub = new fx_client_sub();
        Run(sub, "New");
        Assert.Equal(new[] { "client_sub.New", "client.New 0" }, sub.log);
    }

    [Fact]
    public void ANullOrTextReceiverCallsAGlobalProc()
    {
        // Rules 7.7: call(GLOBAL_PROC, name)(...) and call(/proc/x)(...) are Dm.Call(null, name, ...); tg's GLOBAL_PROC is
        // a text constant (__DEFINES/callbacks.dm), so a text receiver means the same.
        Assert.Equal(5f, Run(null, nameof(DynamicFixtureGlobals.dynfx_global_sum), 2, 3));
        Assert.Equal(5f, Run("some_magic_bullshit", "dynfx_global_sum", 2, 3));
        Assert.Equal(5f, Run(null, "/proc/dynfx_global_sum", 2, 3));
        Assert.Equal(5f, Run(null, "dynfx_global_sum", named("b", 3), named("a", 2)));
    }

    [Fact]
    public void AGlobalProcPathCallsTheGlobalProcWhateverTheReceiver()
    {
        // BYOND ref /proc/call: call(ProcRef) takes a proc path; tg's callbacks keep an object and a proc reference and
        // Invoke does call(object, delegate)(...) unless the object is GLOBAL_PROC (datums/callback.dm), and tg documents
        // CALLBACK(src, GLOBAL_PROC_REF(x)) as a global proc callback (callback.dm:37; used by
        // lost_crew/lost_crew_manager.dm:100). OpenDream calls the proc value it is given, so a global proc path runs the
        // global proc even with a datum receiver. A bare name still means the object's own proc.
        var callee = new fx_callee();
        Assert.Equal(5f, Run(callee, "/proc/dynfx_global_sum", 2, 3));
        Assert.Equal(5f, Run(callee, "/proc/dynfx_global_sum", named("a", 2), named("b", 3)));
        Assert.True(Dm.Call(callee, "dynfx_global_sum", 2, 3).IsFaulted);
        Assert.False(hascall(callee, "/proc/dynfx_global_sum"));       // not a proc of the object
        Assert.True(Dm.Call(callee, "/proc/no_such_global").IsFaulted);
    }

    [Fact]
    public void AnUndefinedProcIsARuntimeErrorInTheTask()
    {
        // BYOND: calling a proc the object does not have is a runtime error ("undefined proc").
        DmTask<object> task = Dm.Call(new fx_callee(), "no_such_proc");
        Assert.True(task.IsFaulted);
        Assert.IsType<DmRuntimeException>(task.Exception);
        Assert.Contains("undefined proc: no_such_proc", task.Exception.Message);
        Assert.True(Dm.Call(null, "no_such_global").IsFaulted);
        Assert.True(Dm.Call(5f, "f").IsFaulted);
        Assert.True(Dm.Call(new fx_callee(), null).IsFaulted);
    }

    [Fact]
    public void HascallAnswersForObjectsOnly()
    {
        // BYOND ref /proc/hascall; OpenDream NativeProc_hascall: 0 unless Object is an object and ProcName text.
        var callee = new fx_callee();
        Assert.True(hascall(callee, "pair"));
        Assert.True(hascall(callee, "use") == false);
        Assert.True(hascall(new fx_stack(), "use"));
        Assert.False(hascall(callee, "no_such_proc"));
        Assert.False(hascall(null, "pair"));
        Assert.False(hascall("text", "pair"));
        Assert.False(hascall(callee, null));
        Assert.False(hascall(callee, "ToString"));                    // C# object members are not DM procs
        Assert.False(hascall(typeof(fx_callee), "GetMethods"));         // nor are a .NET object's methods
        Assert.True(Dm.Call(typeof(fx_callee), "GetMethods").IsFaulted);
    }

    [Fact]
    public void AListHasTheListProcs()
    {
        // BYOND ref "procs (list)": Add, Copy, Cut, Find, Insert, Join, Remove, RemoveAll, Splice, Swap. tg tells lists
        // (and special lists) apart by them: isfilter(thing) is !hascall(thing, "Cut") && ... (__DEFINES/is_helpers.dm:21),
        // view_variables.dm:24 counts hascall(thing, "Cut") as a list. The shim's own C# members (Count) are no procs.
        DmList items = list(1, 2);
        foreach (string proc in new[] { "Add", "Copy", "Cut", "Find", "Insert", "Join", "Remove", "RemoveAll", "Splice", "Swap" })
            Assert.True(hascall(items, proc), proc);
        Assert.False(hascall(items, "Count"));
        Assert.False(hascall(items, "AppendItem"));
        Assert.False(hascall(items, "len"));                            // a var, not a proc
    }

    [Fact]
    public void ThePortsHooksAndEarlierDefinitionsAreNoDmProcs()
    {
        // object-model.md 1.1: dm_assign_id and dm_pre_new are the port's hooks (tg has no proc named dm_*), and rules
        // 2.5: name__d0 is the port's name for an earlier duplicate definition. tg calls procs by a name it got from
        // data (the admin proc call, hascall then call), so these must be unknown to hascall and call() like any name
        // the object has no proc for; calling dm_assign_id again would re-identify the object, dm_pre_new would place it
        // and reinitialize its vars a second time.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_datum datum = Dm.@new<fx_datum>();
            Assert.False(hascall(datum, "dm_assign_id"));
            Assert.False(hascall(datum, "dm_pre_new"));
            DmTask<object> reassign = Dm.Call(datum, "dm_assign_id", 9);
            Assert.IsType<DmRuntimeException>(reassign.Exception);
            Assert.Contains("undefined proc", reassign.Exception.Message);
            Assert.IsType<DmRuntimeException>(Dm.Call(datum, "dm_pre_new", (object)null).Exception);
            Assert.Equal(1, datum.dm_id);
            Assert.Equal(new[] { "pre_new", "datum.New 0" }, datum.log);
        }
        var callee = new fx_callee();
        Assert.Equal("second, then first", Run(callee, "twice"));
        Assert.False(hascall(callee, "twice__d0"));
        Assert.True(Dm.Call(callee, "twice__d0").IsFaulted);
        Assert.Equal("third, then second, then first", Run(callee, "thrice"));
        Assert.False(hascall(callee, "thrice__d0"));
        Assert.False(hascall(callee, "thrice__d1"));
        Assert.True(hascall(callee, "d1"));
        Assert.True(hascall(callee, "not__dx"));
    }

    [Fact]
    public void MembersOfByRefLikeTypesAreNoProcsOrVarsAndNeverBreakTheLookup()
    {
        // A DM value is never a span; a helper with a span parameter or result is not callable by name (and must not
        // make hascall or a var read fail with a .NET error while its invoker is compiled).
        var callee = new fx_callee();
        Assert.False(hascall(callee, "span_taker"));
        Assert.False(hascall(callee, "span_maker"));
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "span_taker", "x").Exception);
        Assert.Throws<DmRuntimeException>(() => get_var(callee, "span_view"));
    }

    // ---- positional arguments ----

    [Fact]
    public void MissingArgumentsAreNullAndExtraOnesAreDropped()
    {
        // BYOND ref "arguments (proc)": arguments not given default to null; passing more arguments than defined is
        // allowed ("named arguments (proc)": "perfectly acceptable to pass more arguments than were explicitly defined").
        var callee = new fx_callee();
        Run(callee, "pair", 1);
        Assert.Equal(new object[] { 1, null }, callee.seen);
        Run(callee, "pair", 1, 2, 3, 4);
        Assert.Equal(new object[] { 1, 2 }, callee.seen);
        Run(callee, "pair");
        Assert.Equal(new object[] { null, null }, callee.seen);
    }

    [Fact]
    public void ExtraArgumentsOfAVariadicSlotGoToExtra()
    {
        // Rules 7.2: a variadic slot's trailing params object[] __extra receives the DM parameters past the root's.
        var callee = new fx_callee();
        Assert.Equal(2, Run(callee, "variadic", "a", "b", "c"));
        Assert.Equal(new object[] { "a", "b", "c" }, callee.seen);
        Assert.Equal(0, Run(callee, "variadic"));
        // A single null argument arrives as a null params array and means one null (docs/ai/specs/shim-ownership.md).
        Assert.Equal(0, Run(callee, "variadic", null));
        Assert.Equal(new object[] { null }, callee.seen);
        Assert.Equal(1, Run(callee, "variadic", null, null));
    }

    [Fact]
    public void ArgumentsAreConvertedToTheParameterTypesByRules35()
    {
        var callee = new fx_callee();
        var atom = new fx_atom();
        DmList items = list(1);
        // float? <- any numeric box (Dm.num_or_null), string <- text, class <- `as`, DmList and Type <- `as`.
        Run(callee, "typed", 3, "s", atom, items, typeof(fx_atom));
        Assert.Equal(new object[] { 3f, "s", atom, items, typeof(fx_atom) }, callee.seen);
        Run(callee, "typed", 2.5, null, new fx_datum(), "not a list", "not a type");
        Assert.Equal(new object[] { 2.5f, null, null, null, null }, callee.seen);   // the DM implicit downcast gives null
        Run(callee, "typed", true);
        Assert.Equal(1f, callee.seen[0]);                                           // a C# bool is the DM number 1
        // float <- Dm.num: null is 0; text is a type mismatch (rules 3.5).
        Assert.Equal(0f, Run(callee, "numeric", null));
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "numeric", "x").Exception);
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "typed", null, 5).Exception);   // string <- a number: mismatch
    }

    [Fact]
    public void ABuiltinsCSharpDefaultAppliesToAnOmittedOrNullArgument()
    {
        // Rules 7.2: in DM a default applies when the argument is omitted or null (OpenDream DMProcBuilder).
        var callee = new fx_callee();
        Run(callee, "with_default");
        Assert.Equal(new object[] { 1f, 0f }, callee.seen);
        Run(callee, "with_default", null, 4);
        Assert.Equal(new object[] { 1f, 4f }, callee.seen);
    }

    // ---- named arguments and arglist ----

    [Fact]
    public void NamedArgumentsBindByDmParameterName()
    {
        // BYOND ref "named arguments (proc)"; rules 7.3: Dm.named items, matched against the definition's names.
        var callee = new fx_callee();
        Run(callee, "pair", named("b", 2), named("a", 1));
        Assert.Equal(new object[] { 1, 2 }, callee.seen);
        Run(callee, "pair", 1, named("b", 2));
        Assert.Equal(new object[] { 1, 2 }, callee.seen);
        Run(callee, "pair", named("a", 1), named("a", 3));              // a later value for the same name wins
        Assert.Equal(new object[] { 3, null }, callee.seen);
    }

    [Fact]
    public void APositionalArgumentAfterANamedOneTakesItsCallPosition()
    {
        // OpenDream DMProc.ToProcArguments: an unnamed argument goes to the parameter at its position in the call,
        // counting the named arguments before it, and a later argument overwrites an earlier one.
        var callee = new fx_callee();
        Run(callee, "pair", named("b", 2), 1);
        Assert.Equal(new object[] { null, 1 }, callee.seen);
        var stack = new fx_stack();
        Run(stack, "use", named("check", 0), 7);                         // position 1 is transfer, in __extra
        Assert.Equal(new object[] { null, 7f, 0f }, stack.used_with);
        Run(stack, "use", 5, 1, named("transfer", null));               // null: the prologue's default (rules 7.2)
        Assert.Equal(new object[] { 5, 0f, 1f }, stack.used_with);
    }

    [Fact]
    public void AnOverrideThatRenamesAParameterAcceptsOnlyItsOwnNames()
    {
        // BYOND ref "named arguments (proc)": the overriding definition's names are matched, so a child that calls the
        // parameter "by" refuses "amount" while its parent takes it.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_cold parent = Dm.@new<fx_cold>();
            fx_cold_child child = Dm.@new<fx_cold_child>();
            Assert.Equal(53f, Run(parent, "warm", named("amount", 3)));
            Assert.Equal(56f, Run(child, "warm", named("by", 3)));
            Assert.Contains("has no argument named \"amount\"", Dm.Call(child, "warm", named("amount", 3)).Exception.Message);
        }
    }

    [Fact]
    public void TheOverridingDefinitionsNamesAreTheOnesMatched()
    {
        // BYOND ref "named arguments (proc)": "When an object procedure is overridden, the variable names in the new
        // definition are the ones that get matched against named arguments".
        var renamed = new fx_callee_renamed();
        Run(renamed, "pair", named("second", 2), named("first", 1));
        Assert.Equal(new object[] { 1, 2 }, renamed.seen);
        DmTask<object> old = Dm.Call(renamed, "pair", named("a", 1));
        Assert.Contains("has no argument named \"a\"", old.Exception.Message);
    }

    [Fact]
    public void NamedArgumentsReachExtraParametersInDmParamsOrder()
    {
        // Rules 7.2/7.3: stack.use(5, check = FALSE) binds check to its [DmParams] position in __extra.
        var stack = new fx_stack();
        Run(stack, "use", 5, named("check", 0));
        Assert.Equal(new object[] { 5, 0f, 0f }, stack.used_with);      // transfer defaulted by the prologue
        var sheet = new fx_sheet();
        Run(sheet, "use", named("amount", 7), named("transfer", 1));
        Assert.Equal(new object[] { 7, 1f, 1f }, sheet.used_with);
    }

    [Fact]
    public void AnUnknownArgumentNameIsARuntimeError()
    {
        // BYOND ref "named arguments (proc)": "named arguments that do not match any of the arguments of the procedure
        // being called will generate a runtime error".
        DmTask<object> task = Dm.Call(new fx_callee(), "pair", named("c", 1));
        Assert.IsType<DmRuntimeException>(task.Exception);
        Assert.Equal("/datum/dynfx/caller/proc/pair has no argument named \"c\"", task.Exception.Message);
    }

    [Fact]
    public void CSharpKeywordsAreMatchedByTheirDmNames()
    {
        // Rules 2.2: @event, @params; reflection reports the DM name, and an '@' written in [DmParams] is dropped.
        var callee = new fx_callee();
        Run(callee, "fire", named("params", "p"), named("event", "e"));
        Assert.Equal(new object[] { "e", "p" }, callee.seen);
        var renamed = new fx_callee_renamed();
        Run(renamed, "fire", named("event", 1));
        Assert.Equal(new object[] { 1, null }, renamed.seen);
    }

    [Fact]
    public void ArglistSpreadsAListAndItsTextKeysBecomeNamedArguments()
    {
        // BYOND ref /proc/arglist: "If the items in the list are associations, these are treated as named arguments";
        // rules 7.3: text keys (with any value, AnArglistKeyWhoseValueIsNullIsStillANamedArgument).
        var callee = new fx_callee();
        Run(callee, "pair", arglist(list(1, 2)));
        Assert.Equal(new object[] { 1, 2 }, callee.seen);
        Run(callee, "pair", arglist(list(kv("b", 2), kv("a", 1))));
        Assert.Equal(new object[] { 1, 2 }, callee.seen);
        Run(callee, "variadic", arglist(list("x", "y", "z")));
        Assert.Equal(new object[] { "x", "y", "z" }, callee.seen);
    }

    [Fact]
    public void AnArglistKeyWhoseValueIsNullIsStillANamedArgument()
    {
        // OpenDream DMProc.ToProcArguments: an item is a named argument when the list has it as a key
        // (argList.ContainsKey), whatever its value. tg relies on it: AddComponent(/datum/component/vision_hurting,
        // damage_per_second = 0.1, message = null, silent = TRUE) (antagonists/voidwalker/sunwalker_mob.dm:40) reaches
        // Initialize(damage_per_second=1, message="Your eyes burn as you look at", silent = FALSE) as
        // Initialize(arglist(arguments)) (components/_component.dm:47), and message = null must take the default.
        var callee = new fx_callee();
        Run(callee, "hurt", arglist(list(kv("damage_per_second", 0.1f), kv("message", null), kv("silent", 1))));
        Assert.Equal(new object[] { 0.1f, "Your eyes burn as you look at", 1 }, callee.seen);
        Run(callee, "pair", arglist(list(kv("b", 2), kv("a", null))));
        Assert.Equal(new object[] { null, 2 }, callee.seen);
        Run(callee, "pair", arglist(list(1, kv("b", null))));
        Assert.Equal(new object[] { 1, null }, callee.seen);
        // A key without a parameter of that name is still a runtime error, null value or not.
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "pair", arglist(list(kv("c", null)))).Exception);
    }

    [Fact]
    public void AnArglistKeyIsMatchedAsWrittenNotAsAProcPath()
    {
        // BYOND ref "named arguments (proc)": the name is matched against the parameter names; a text key holding a
        // slash is no parameter name, so it is an unknown name, not the parameter named after its last segment.
        var callee = new fx_callee();
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "pair", arglist(list(kv("x/b", 1)))).Exception);
        Assert.IsType<DmRuntimeException>(Dm.Call(callee, "pair", named("x/b", 1)).Exception);
    }

    [Fact]
    public void ArglistOfANonListGivesNoArguments()
    {
        // OpenDream DMProc.ToProcArguments: "Using a non-list gives you no arguments"; tg's /datum/callback/Invoke
        // passes arglist(calling_arguments), which is null for a callback made without arguments (callback.dm).
        var callee = new fx_callee();
        callee.seen = null;
        Run(callee, "pair", arglist(null));
        Assert.Equal(new object[] { null, null }, callee.seen);
        Assert.Equal(0, Run(callee, "variadic", arglist(5)));
    }

    [Fact]
    public void ANonTextKeyWithAValueCannotBeAnArgument()
    {
        // OpenDream DMProc.ToProcArguments: "List contains a non-string key, and cannot be used as an arglist".
        var key = new fx_datum();
        DmTask<object> task = Dm.Call(new fx_callee(), "pair", arglist(list(kv(key, 1))));
        Assert.IsType<DmRuntimeException>(task.Exception);
        Assert.IsType<DmRuntimeException>(Dm.Call(new fx_callee(), "pair", arglist(list(kv(key, null)))).Exception);
    }

    // ---- results ----

    [Fact]
    public void SynchronousResultsAreCompletedTasks()
    {
        // waiting-model.md 4.5: a synchronous callee's value as a completed task; void gives null; a C# bool is 1 or 0.
        var callee = new fx_callee();
        DmTask<object> task = Dm.Call(callee, "nothing");
        Assert.True(task.IsCompleted);
        Assert.True(task.TryGetResult(out object value));
        Assert.Null(value);
        Assert.Equal("nothing ran", callee.last);
        Assert.Equal(1f, Run(callee, "condition"));
        Assert.Equal("text", Run(callee, "text"));
        Assert.Equal(7f, Run(callee, "quick"));                        // a coloured proc that did not sleep
    }

    [Fact]
    public void AColouredCalleeThatSleepsIsAwaitedThroughTheCall()
    {
        // waiting-model.md 4.5 and 5.1: the caller waits until the callee returns and gets its value.
        World w = DynamicWorld.New(out List<string> errors);
        var callee = new fx_callee();
        DmTask<object> caller;
        using (w.Enter())
            caller = AwaitCall(callee, "nap", 1);
        Assert.False(caller.IsCompleted);
        Assert.Equal("napping", callee.last);
        w.Step(2);                                                     // sleep(1) is 2 ticks (waiting-model.md 8)
        Assert.True(caller.TryGetResult(out object result));
        Assert.Equal("rested", result);
        Assert.Empty(errors);

        DmTask<object> global;
        using (w.Enter())
            global = AwaitCall(null, "dynfx_global_nap", 0);
        w.Step();
        Assert.True(global.TryGetResult(out result));
        Assert.Equal("global woke", result);

        DmTask<object> voidCall;
        using (w.Enter())
            voidCall = AwaitCall(callee, "nap_void", 1);
        w.Step(2);
        Assert.True(voidCall.TryGetResult(out result));
        Assert.Null(result);
        Assert.Equal("woke (void)", callee.last);
    }

    [Fact]
    public void ASleepingCalleeCalledFromASynchronousContextFollowsTheNoSleepRule()
    {
        // waiting-model.md 5.3 / DV5: the caller continues with null, the callee runs on detached, a runtime is reported.
        World w = DynamicWorld.New(out List<string> errors);
        var callee = new fx_callee();
        using (w.Enter())
            Assert.Null(Waiting.NoSleep(Dm.Call(callee, "nap", 1)));
        Assert.Single(errors);
        w.Step(2);
        Assert.Equal("woke", callee.last);
    }

    // ---- exceptions keep their type ----

    [Fact]
    public void ACalleesExceptionKeepsItsTypeAndInstance()
    {
        // Rules 9: a runtime error is a C# exception that unwinds to an isolation boundary; the binder never wraps it.
        var callee = new fx_callee();
        DmTask<object> crash = Dm.Call(callee, "throws_crash");
        var crashError = Assert.IsType<DmCrashException>(crash.Exception);
        Assert.Equal("code/dynfx.dm", crashError.DmFile);
        Assert.Same(crashError, Assert.Throws<DmCrashException>(() => Waiting.NoSleep(crash)));
        DmTask<object> clr = Dm.Call(callee, "throws_clr");
        Assert.IsType<InvalidOperationException>(clr.Exception);
        Assert.Throws<InvalidOperationException>(() => Waiting.NoSleep(clr));
    }

    [Fact]
    public void ALateErrorOfASleepingCalleeReachesTheAwaitingCallerWithItsType()
    {
        World w = DynamicWorld.New(out List<string> errors);
        DmTask<string> caller;
        using (w.Enter())
            caller = CatchTypeName(new fx_callee(), "throws_after_sleep");
        w.Step(2);
        Assert.True(caller.TryGetResult(out string caught));
        Assert.Equal(nameof(ArgumentException), caught);
        Assert.Empty(errors);
    }

    [Fact]
    public void ADetachedCallReportsTheErrorAndNeverStopsTheCaller()
    {
        // waiting-model.md 5.2: INVOKE_ASYNC(obj, x) of a proc that fails, or of one that does not exist.
        World w = DynamicWorld.New(out List<string> errors);
        using (w.Enter())
        {
            Waiting.Detach(Dm.Call(new fx_callee(), "throws_clr"));
            Waiting.Detach(Dm.Call(new fx_callee(), "no_such_proc"));
            Waiting.Detach(Dm.Call(new fx_callee(), "throws_after_sleep"));
        }
        Assert.Equal(new[] { "InvalidOperationException: a C# error", "DmRuntimeException: undefined proc: no_such_proc (/datum/dynfx/caller)" }, errors);
        w.Step(2);
        Assert.Equal("ArgumentException: late error", errors[2]);
    }

    [Fact]
    public void AColouredCalleeThatFailsBeforeItSleepsKeepsItsExceptionInstance()
    {
        // waiting-model.md 4.5, 9.5: an error a coloured callee raises before its first suspension is captured in its
        // task; through a call by name the same instance reaches NoSleep and await, nobody reports it on the way, and a
        // detached call reports it once. (Debug builds async state machines as classes, Release as structs.)
        World w = DynamicWorld.New(out List<string> errors);
        var callee = new fx_callee();
        using (w.Enter())
        {
            DmTask<object> early = Dm.Call(callee, "fails");
            Exception error = Assert.IsType<KeyNotFoundException>(early.Exception);
            Assert.Same(error, Assert.Throws<KeyNotFoundException>(() => Waiting.NoSleep(early)));
            DmTask<object> earlyVoid = Dm.Call(callee, "fails_void");
            Assert.IsType<FormatException>(earlyVoid.Exception);
            Assert.Throws<FormatException>(() => Waiting.NoSleep(earlyVoid));
            Assert.Empty(errors);
            Waiting.Detach(Dm.Call(callee, "fails"));
        }
        Assert.Equal(new[] { "KeyNotFoundException: early error" }, errors);
    }

    [Fact]
    public void AColouredCalleeThatFailsAfterItSleptReachesTheAwaitingCallerWithItsType()
    {
        World w = DynamicWorld.New(out List<string> errors);
        var callee = new fx_callee();
        DmTask<string> late;
        DmTask<string> lateVoid;
        using (w.Enter())
        {
            late = CatchTypeName(callee, "fails", 1);
            lateVoid = CatchTypeName(callee, "fails_void", 1);
        }
        Assert.False(late.IsCompleted);
        w.Step(2);
        Assert.True(late.TryGetResult(out string name));
        Assert.Equal(nameof(KeyNotFoundException), name);
        Assert.True(lateVoid.TryGetResult(out name));
        Assert.Equal(nameof(FormatException), name);
        Assert.Empty(errors);
    }

    [Fact]
    public void ErrorsOfDetachedAndNoSleepCallsAreReportedExactlyOnce()
    {
        // waiting-model.md 9.5: never lost, never doubled. A callee that sleeps under NoSleep gives the no-sleep runtime
        // now and its own late error later; a detached one only its late error; both through the call by name's adapter.
        World w = DynamicWorld.New(out List<string> errors);
        using (w.Enter())
        {
            Assert.Null(Waiting.NoSleep(Dm.Call(new fx_callee(), "throws_after_sleep")));
            Waiting.Detach(Dm.Call(new fx_callee(), "fails", 1));
            Waiting.Detach(Dm.Call(new fx_callee(), "fails_void", 1));
        }
        Assert.Single(errors);
        Assert.StartsWith("WaitingException: proc slept inside a no-sleep context", errors[0]);
        w.Step(2);
        Assert.Equal(new[] { "ArgumentException: late error", "KeyNotFoundException: late error", "FormatException: late void error" }, errors.Skip(1));
        w.Step(4);
        Assert.Equal(4, errors.Count);
    }

    private static async DmTask<object> AwaitCall(object src, string proc, params object[] args) => await Dm.Call(src, proc, args);

    private static async DmTask<string> CatchTypeName(object src, string proc, params object[] args)
    {
        try
        {
            await Dm.Call(src, proc, args);
            return "no error";
        }
        catch (Exception e)
        {
            return e.GetType().Name;
        }
    }
}
