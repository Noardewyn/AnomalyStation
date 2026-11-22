using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Anomaly.Analyzers;

/// <summary>
/// The waiting-model analyzer (D97; docs/ai/specs/waiting-model.md section 5.6):
/// <list type="bullet">
/// <item>ANOM0001 a dropped task, ANOM0004 a foreign awaitable in a DmTask proc, ANOM0005 an await in a
/// <c>[SignalHandler]</c>/<c>[ShouldNotSleep]</c> method: in every project that references the kernel;</item>
/// <item>ANOM0002 a stored task and ANOM0003 a blocking read: only in translated code, a project whose MSBuild property
/// <c>AnomalyTranslatedCode</c> is <c>true</c> (Anomaly.Core.Tg).</item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WaitingModelAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The analyzer config key of the MSBuild property that marks translated code.</summary>
    public const string TranslatedCodeOption = "build_property.AnomalyTranslatedCode";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Descriptors.DroppedTask,
        Descriptors.StoredTask,
        Descriptors.BlockingRead,
        Descriptors.ForeignAwaitable,
        Descriptors.AwaitInSyncProc);

    public override void Initialize(AnalysisContext context)
    {
        // The Tg project's generated files (defines, type order) are translation output too.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var k = KernelSymbols.TryCreate(context.Compilation);
        if (k == null) return; // the compilation does not see the kernel: no task can occur

        // Rule 1: dropped tasks.
        context.RegisterOperationAction(c => DroppedTasks.OnExpressionStatement(c, k), OperationKind.ExpressionStatement);
        context.RegisterOperationAction(c => DroppedTasks.OnDiscard(c, k), OperationKind.Discard);
        context.RegisterOperationAction(c => DroppedTasks.OnVariableDeclarator(c, k), OperationKind.VariableDeclarator);

        // Rules 4 and 5: awaits.
        context.RegisterOperationAction(c => Awaits.OnAwait(c, k), OperationKind.Await);
        context.RegisterOperationAction(c => Awaits.OnLoop(c, k), OperationKind.Loop);
        context.RegisterOperationAction(c => Awaits.OnUsing(c, k), OperationKind.Using, OperationKind.UsingDeclaration);
        context.RegisterSymbolAction(c => Awaits.OnMethod(c, k), SymbolKind.Method);

        if (!IsTranslatedCode(context.Options)) return;

        // Rules 2 and 3: translated code only.
        context.RegisterOperationAction(c => StoredTasks.OnTaskValue(c, k),
            OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.FieldReference,
            OperationKind.LocalReference, OperationKind.ParameterReference, OperationKind.ArrayElementReference,
            OperationKind.ObjectCreation, OperationKind.DefaultValue, OperationKind.Conversion, OperationKind.Await);
        context.RegisterOperationAction(c => StoredTasks.OnArgument(c, k), OperationKind.Argument);
        context.RegisterOperationAction(c => BlockingReads.OnMemberAccess(c, k),
            OperationKind.Invocation, OperationKind.PropertyReference, OperationKind.FieldReference,
            OperationKind.MethodReference, OperationKind.EventReference);
    }

    private static bool IsTranslatedCode(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(TranslatedCodeOption, out var value)
        && string.Equals(value?.Trim(), "true", System.StringComparison.OrdinalIgnoreCase);

    // ---- shared helpers ----

    /// <summary>The source text of an expression for a message, shortened to one line.</summary>
    internal static string Show(SyntaxNode node)
    {
        var text = node.ToString();
        int nl = text.IndexOfAny(new[] { '\r', '\n' });
        if (nl >= 0) text = text.Substring(0, nl).TrimEnd() + " ...";
        return text.Length <= 80 ? text : text.Substring(0, 77) + "...";
    }

    /// <summary>
    /// The function an operation's own code belongs to: the nearest enclosing lambda, anonymous method or local
    /// function, otherwise the member whose body is analyzed.
    /// </summary>
    internal static IMethodSymbol EnclosingFunction(IOperation operation, ISymbol containingSymbol)
    {
        for (var p = operation.Parent; p != null; p = p.Parent)
        {
            if (p is IAnonymousFunctionOperation lambda) return lambda.Symbol;
            if (p is ILocalFunctionOperation local) return local.Symbol;
        }
        return containingSymbol as IMethodSymbol;
    }
}

/// <summary>Rule 1 (ANOM0001): a task value that is discarded.</summary>
internal static class DroppedTasks
{
    public static void OnExpressionStatement(OperationAnalysisContext c, KernelSymbols k)
    {
        var value = ((IExpressionStatementOperation)c.Operation).Operation;
        // An assignment or an increment stores its value; storing is rule 2's business.
        if (value is IAssignmentOperation or IDeconstructionAssignmentOperation or IIncrementOrDecrementOperation) return;
        if (!k.IsTask(value.Type)) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.DroppedTask, value.Syntax.GetLocation(), WaitingModelAnalyzer.Show(value.Syntax)));
    }

    public static void OnDiscard(OperationAnalysisContext c, KernelSymbols k)
    {
        var discard = (IDiscardOperation)c.Operation;
        if (!k.IsTask(discard.Type)) return;
        // '_ = Foo();' names Foo(); a discard in a deconstruction or an out argument names itself.
        SyntaxNode shown = discard.Parent is ISimpleAssignmentOperation a && a.Target == discard ? a.Value.Syntax : discard.Syntax;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.DroppedTask, shown.GetLocation(), WaitingModelAnalyzer.Show(shown)));
    }

    public static void OnVariableDeclarator(OperationAnalysisContext c, KernelSymbols k)
    {
        // 'var _ = Foo();' declares a local named '_' that nothing reads: a discard in spirit.
        var declarator = (IVariableDeclaratorOperation)c.Operation;
        if (declarator.Symbol.Name != "_" || !k.IsTask(declarator.Symbol.Type)) return;
        var init = declarator.Initializer ?? (declarator.Parent as IVariableDeclarationOperation)?.Initializer;
        if (init == null) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.DroppedTask, init.Value.Syntax.GetLocation(), WaitingModelAnalyzer.Show(init.Value.Syntax)));
    }
}

/// <summary>Rules 4 (ANOM0004, foreign awaitables) and 5 (ANOM0005, awaits in sync-mandated methods).</summary>
internal static class Awaits
{
    public static void OnAwait(OperationAnalysisContext c, KernelSymbols k)
    {
        var await = (IAwaitOperation)c.Operation;
        var function = WaitingModelAnalyzer.EnclosingFunction(await, c.ContainingSymbol);
        CheckOwnAwait(c, k, function, WaitingModelAnalyzer.Show(await.Syntax), await.Syntax.GetLocation());

        if (function == null || !k.IsDmTaskReturn(function.ReturnType)) return;
        if (await.Syntax is not AwaitExpressionSyntax syntax || await.SemanticModel == null) return;
        var info = await.SemanticModel.GetAwaitExpressionInfo(syntax);
        ITypeSymbol awaiter = info.GetAwaiterMethod?.ReturnType;
        if (!info.IsDynamic && awaiter == null) return; // does not bind: the compiler reports it
        if (!info.IsDynamic && k.IsKernelAwaiter(awaiter)) return;
        string awaited = await.Operation.Type?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat) ?? "dynamic";
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.ForeignAwaitable, syntax.GetLocation(), WaitingModelAnalyzer.Show(syntax), awaited));
    }

    // 'await foreach': MoveNextAsync (and DisposeAsync) are awaited.
    public static void OnLoop(OperationAnalysisContext c, KernelSymbols k)
    {
        if (c.Operation is not IForEachLoopOperation { IsAsynchronous: true } loop) return;
        var function = WaitingModelAnalyzer.EnclosingFunction(loop, c.ContainingSymbol);
        var keyword = loop.Syntax is CommonForEachStatementSyntax fe ? fe.AwaitKeyword.GetLocation() : loop.Syntax.GetLocation();
        CheckOwnAwait(c, k, function, "await foreach", keyword);

        if (function == null || !k.IsDmTaskReturn(function.ReturnType) || loop.SemanticModel == null) return;
        if (loop.Syntax is not CommonForEachStatementSyntax syntax) return;
        var info = loop.SemanticModel.GetForEachStatementInfo(syntax);
        var moveNext = info.MoveNextMethod?.ReturnType;
        if (moveNext != null && k.IsKernelAwaiter(AwaiterOf(moveNext))
            && (info.DisposeMethod == null || k.IsKernelAwaiter(AwaiterOf(info.DisposeMethod.ReturnType)))) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.ForeignAwaitable, keyword, "await foreach",
            moveNext?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat) ?? "?"));
    }

    // 'await using': DisposeAsync is awaited.
    public static void OnUsing(OperationAnalysisContext c, KernelSymbols k)
    {
        bool isAsync;
        SyntaxToken awaitKeyword;
        switch (c.Operation)
        {
            case IUsingOperation u:
                isAsync = u.IsAsynchronous;
                awaitKeyword = u.Syntax is UsingStatementSyntax us ? us.AwaitKeyword : default;
                break;
            case IUsingDeclarationOperation d:
                isAsync = d.IsAsynchronous;
                awaitKeyword = d.Syntax is LocalDeclarationStatementSyntax ld ? ld.AwaitKeyword : default;
                break;
            default:
                return;
        }
        if (!isAsync) return;
        var function = WaitingModelAnalyzer.EnclosingFunction(c.Operation, c.ContainingSymbol);
        var location = awaitKeyword.RawKind != 0 ? awaitKeyword.GetLocation() : c.Operation.Syntax.GetLocation();
        CheckOwnAwait(c, k, function, "await using", location);

        // No kernel type is async-disposable: every 'await using' in a DmTask proc awaits a ValueTask.
        if (function == null || !k.IsDmTaskReturn(function.ReturnType)) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.ForeignAwaitable, location, "await using", "DisposeAsync()"));
    }

    // A sync-mandated method declared 'async' or returning DmTask.
    public static void OnMethod(SymbolAnalysisContext c, KernelSymbols k)
    {
        var method = (IMethodSymbol)c.Symbol;
        if (method.IsImplicitlyDeclared) return;
        bool isAsync = method.IsAsync;
        bool returnsTask = k.IsDmTaskReturn(method.ReturnType);
        if (!isAsync && !returnsTask) return;
        var mandate = k.FindSyncMandate(method);
        if (mandate == null) return;
        string fix = isAsync
            ? "remove 'async' (and the DmTask return type) and call procs of DmTask slots with Waiting.NoSleep(...)"
            : "return the slot's synchronous type, not DmTask";
        foreach (var location in method.Locations)
            c.ReportDiagnostic(Diagnostic.Create(Descriptors.AwaitInSyncProc, location, method.Name, mandate.Attribute,
                Declared(mandate), fix));
    }

    private static void CheckOwnAwait(OperationAnalysisContext c, KernelSymbols k, IMethodSymbol function, string shown, Location location)
    {
        // Only the method's own awaits: one inside a nested lambda or local function belongs to that function.
        if (function == null || function.MethodKind is MethodKind.AnonymousFunction or MethodKind.LocalFunction) return;
        var mandate = k.FindSyncMandate(function);
        if (mandate == null) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.AwaitInSyncProc, location, function.Name, mandate.Attribute,
            Declared(mandate), $"replace '{shown}' with Waiting.NoSleep(...)"));
    }

    private static string Declared(SyncMandate mandate) =>
        mandate.DeclaredOn.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat) + "." + mandate.DeclaredOn.Name;

    /// <summary>The awaiter type of an awaitable by the instance-method pattern (<c>GetAwaiter()</c>), or null.</summary>
    private static ITypeSymbol AwaiterOf(ITypeSymbol awaitable)
    {
        for (var t = awaitable; t != null; t = t.BaseType)
            foreach (var m in t.GetMembers("GetAwaiter"))
                if (m is IMethodSymbol { IsStatic: false, Parameters.Length: 0 } g) return g.ReturnType;
        return null;
    }
}

/// <summary>Rule 2 (ANOM0002): a task value in translated code that is not consumed at once.</summary>
internal static class StoredTasks
{
    public static void OnTaskValue(OperationAnalysisContext c, KernelSymbols k)
    {
        var op = c.Operation;
        if (!k.IsTask(op.Type)) return;

        // Transparent wrappers are not sources: the value they wrap is followed through them.
        if (op is IConversionOperation conv && k.IsTask(conv.Operand.Type)) return;
        if (IsAsObjectTask(op, k)) return;
        if (IsWriteTarget(op)) return;

        var (consumer, child) = Consumer(op, k);
        string use = Classify(consumer, child, k);
        if (use == null) return;
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.StoredTask, op.Syntax.GetLocation(), WaitingModelAnalyzer.Show(op.Syntax), use));
    }

    // A task received through an out or ref argument: the callee stores it into a variable of the caller.
    public static void OnArgument(OperationAnalysisContext c, KernelSymbols k)
    {
        var arg = (IArgumentOperation)c.Operation;
        if (arg.Parameter == null || arg.Parameter.RefKind is not (RefKind.Out or RefKind.Ref)) return;
        if (!k.IsTask(arg.Parameter.Type) || arg.Value is IDiscardOperation) return; // 'out _' is rule 1
        string kind = arg.Parameter.RefKind == RefKind.Out ? "out" : "ref";
        c.ReportDiagnostic(Diagnostic.Create(Descriptors.StoredTask, arg.Syntax.GetLocation(), WaitingModelAnalyzer.Show(arg.Syntax),
            $"stored through an {kind} argument"));
    }

    // IDmTask.AsObjectTask() turns a task into another task (calls by name, waiting-model.md 4.5): the value is
    // followed through it, and the call is not a source of its own.
    private static bool IsAsObjectTask(IOperation op, KernelSymbols k) =>
        op is IInvocationOperation { TargetMethod.Name: "AsObjectTask", Instance: { } receiver } && k.IsTask(receiver.Type);

    private static bool IsWriteTarget(IOperation op)
    {
        switch (op.Parent)
        {
            case IAssignmentOperation a when a.Target == op:
                return true;
            case IArgumentOperation arg when arg.Parameter != null && arg.Parameter.RefKind is RefKind.Out or RefKind.Ref:
                return true; // reported once by OnArgument
        }
        if (op is ILocalReferenceOperation { IsDeclaration: true }) return true;
        // An element of a deconstruction target: '(t, x) = ...'.
        IOperation cur = op;
        while (cur.Parent is ITupleOperation or IDeclarationExpressionOperation) cur = cur.Parent;
        return cur != op && cur.Parent is IDeconstructionAssignmentOperation d && d.Target == cur;
    }

    /// <summary>
    /// Follows a task value up through the forms that pass it on unchanged (a conversion to another task type,
    /// <c>?:</c>, <c>??</c>, a switch expression arm, <c>?.</c>, <c>AsObjectTask()</c>) to the operation that uses it.
    /// </summary>
    private static (IOperation consumer, IOperation child) Consumer(IOperation op, KernelSymbols k)
    {
        IOperation cur = op;
        while (true)
        {
            var p = cur.Parent;
            switch (p)
            {
                case IConversionOperation conv when k.IsTask(conv.Type):
                    cur = p;
                    continue;
                case IConditionalOperation cond when cond.Condition != cur:
                    cur = p;
                    continue;
                case ICoalesceOperation:
                    cur = p;
                    continue;
                case ISwitchExpressionArmOperation arm when arm.Value == cur && arm.Parent != null:
                    cur = arm.Parent;
                    continue;
                case IConditionalAccessOperation access when access.WhenNotNull == cur:
                    cur = p;
                    continue;
                case IConditionalAccessOperation access when access.Operation == cur && IsAsObjectTask(access.WhenNotNull, k):
                    cur = p; // '(x as IDmTask)?.AsObjectTask()'
                    continue;
                case IInvocationOperation inv when inv.Instance == cur && IsAsObjectTask(inv, k):
                    cur = p;
                    continue;
                default:
                    return (p, cur);
            }
        }
    }

    /// <summary>What the consumer does with the task, for the message; null when it is allowed or another rule's.</summary>
    private static string Classify(IOperation consumer, IOperation child, KernelSymbols k)
    {
        switch (consumer)
        {
            case null:
                return null;
            case IAwaitOperation:
                return null;
            case IReturnOperation r:
                return r.Kind == OperationKind.YieldReturn ? "yielded" : null;
            case IArgumentOperation arg:
                var target = (arg.Parent as IInvocationOperation)?.TargetMethod ?? (arg.Parent as IObjectCreationOperation)?.Constructor;
                if (k.IsWaitingMethod(target)) return null;
                if (target != null) return $"passed to '{target.ContainingType?.Name}.{target.Name}'";
                return "passed as an argument";
            // Dropped: rule 1.
            case IExpressionStatementOperation:
                return null;
            case ISimpleAssignmentOperation a when a.Target is IDiscardOperation:
                return null;
            case IVariableInitializerOperation init when init.Parent is IVariableDeclaratorOperation { Symbol.Name: "_" }:
                return null;
            // A member read on the task itself (other than AsObjectTask): rule 3.
            case IInvocationOperation inv when inv.Instance == child:
                return null;
            case IPropertyReferenceOperation prop when prop.Instance == child:
                return null;
            case IFieldReferenceOperation field when field.Instance == child:
                return null;
            case IMethodReferenceOperation method when method.Instance == child:
                return null;
            case IConditionalAccessOperation access when access.Operation == child:
                return null;
            // Stores.
            case IVariableInitializerOperation:
            case IFieldInitializerOperation:
            case IPropertyInitializerOperation:
            case IAssignmentOperation:
            case ITupleOperation:
            case IArrayInitializerOperation:
            case ICollectionExpressionOperation:
            case IAnonymousObjectCreationOperation:
                return "stored";
            case IConversionOperation conv:
                return $"converted to '{conv.Type?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}'";
            default:
                return "used as a value";
        }
    }
}

/// <summary>Rule 3 (ANOM0003): translated code reads a task's state or result, or uses a kernel awaiter directly.</summary>
internal static class BlockingReads
{
    public static void OnMemberAccess(OperationAnalysisContext c, KernelSymbols k)
    {
        var op = c.Operation;
        IOperation receiver;
        ISymbol member;
        switch (op)
        {
            case IInvocationOperation inv: receiver = inv.Instance; member = inv.TargetMethod; break;
            case IPropertyReferenceOperation prop: receiver = prop.Instance; member = prop.Property; break;
            case IFieldReferenceOperation field: receiver = field.Instance; member = field.Field; break;
            case IMethodReferenceOperation method: receiver = method.Instance; member = method.Method; break;
            case IEventReferenceOperation ev: receiver = ev.Instance; member = ev.Event; break;
            default: return;
        }
        if (receiver == null || member == null || member.IsStatic) return;
        // A '?.' receiver is the conditional access instance: look at the accessed expression.
        var receiverType = receiver is IConditionalAccessInstanceOperation ? FindAccessed(receiver)?.Type : receiver.Type;

        bool report;
        if (k.IsTaskType(receiverType)) // not a nullable task: HasValue and Value are Nullable's
        {
            // AsObjectTask turns a task into another task (calls by name, waiting-model.md 4.5); rule 2 follows it.
            report = member.Name != "AsObjectTask";
        }
        else if (member is IMethodSymbol { Name: "GetAwaiter", Parameters.Length: 0 } getAwaiter && k.IsKernelAwaiter(getAwaiter.ReturnType))
        {
            report = true; // 'sleep(1).GetAwaiter()'
        }
        else if (k.IsKernelAwaiter(receiverType))
        {
            // 'x.GetAwaiter().GetResult()' is reported once, at GetAwaiter.
            report = !(receiver is IInvocationOperation { TargetMethod.Name: "GetAwaiter" });
        }
        else
        {
            report = false;
        }
        if (report)
            c.ReportDiagnostic(Diagnostic.Create(Descriptors.BlockingRead, op.Syntax.GetLocation(), WaitingModelAnalyzer.Show(op.Syntax)));
    }

    private static IOperation FindAccessed(IOperation instance)
    {
        for (var p = instance.Parent; p != null; p = p.Parent)
            if (p is IConditionalAccessOperation access) return access.Operation;
        return null;
    }
}
