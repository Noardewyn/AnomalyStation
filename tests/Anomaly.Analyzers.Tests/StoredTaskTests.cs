using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>ANOM0002: in translated code a task is awaited, handed to Waiting.* or returned at once, never stored.</summary>
public class StoredTaskTests
{
    private const string Procs = """
        public abstract class datum
        {
            public abstract DmTask Run();
            public virtual DmTask<object> Value() => DmTask<object>.FromResult(null);
            public virtual DmTask<object> Named(string name) => Value().AsObjectTask();
            public static async DmTask Sleeps() { await sleep(1); }
            public static async DmTask<int> SleepsT() { await sleep(1); return 1; }
            public static void Take(DmTask task) { }
            public static async DmTask AwaitLater(DmTask task) { await task; }
        }

        """;

    [Fact]
    public void StoringATaskToAwaitItLaterIsReportedWhereTheTaskIsStored()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public DmTask pending;
                public override DmTask Run() => DmTask.CompletedTask;

                public async DmTask M()
                {
                    var t = Run();
                    await t;
                    DmTask<int> u = SleepsT();
                    await u;
                    await u; // awaited twice: only possible after storing it
                    pending = Sleeps();
                    IDmTask boxed = Value();
                    DmTask nothing = default;
                    Out(out DmTask received);
                    await received;
                }

                public static void Out(out DmTask task) => task = DmTask.CompletedTask; // a store too
            }
            """, translated: true),
            ("ANOM0002", "Run()"), ("ANOM0002", "SleepsT()"), ("ANOM0002", "Sleeps()"), ("ANOM0002", "Value()"),
            ("ANOM0002", "default"), ("ANOM0002", "out DmTask received"), ("ANOM0002", "DmTask.CompletedTask"));
    }

    [Fact]
    public void PassingATaskOnOrConvertingItToObjectIsReported()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;

                public async DmTask M()
                {
                    Take(Run());
                    await AwaitLater(Sleeps());
                    object o = Value();
                    var pair = (Run(), 1);
                    var list = new[] { Run() };
                    Func<object> f = () => SleepsT();
                }

                public object Boxed() => Value();
            }
            """, translated: true),
            ("ANOM0002", "Run()"), ("ANOM0002", "Sleeps()"), ("ANOM0002", "Value()"), ("ANOM0002", "Run()"),
            ("ANOM0002", "Run()"), ("ANOM0002", "SleepsT()"), ("ANOM0002", "Value()"));
    }

    [Fact]
    public void AwaitingWaitingFormsAndReturningAreAllowed()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;

                // Expression-bodied pass-throughs, the base call included.
                public override DmTask<object> Value() => base.Value();
                public DmTask AddElement() => Sleeps();
                public DmTask<object> ByName(string name) => Named(name).AsObjectTask();
                public DmTask<object> Typed() => DmTask<object>.FromResult(1);

                public async DmTask<object> M(bool flag, int n)
                {
                    await Run();
                    object v = await Value();
                    int i = await SleepsT();
                    await (flag ? Run() : Sleeps());
                    await (n switch { 0 => Run(), _ => DmTask.CompletedTask });
                    await Named("x").AsObjectTask();
                    Waiting.Detach(Sleeps());
                    int d = Waiting.Detach(SleepsT());
                    Waiting.Spawn(-1, () => Run());
                    Waiting.Spawn(0, async () => { await Run(); });
                    return await Value();
                }

                public object Sync()
                {
                    Waiting.NoSleep(Run());
                    DotBox<object> dot = new DotBox<object>();
                    return Waiting.WaitforFalse(Body(dot), dot);
                }

                private async DmTask<object> Body(DotBox<object> dot)
                {
                    dot.Value = 1;
                    await sleep(1);
                    return dot.Value;
                }

                public DmTask Choose(bool flag)
                {
                    if (flag) return Run();
                    return flag ? Sleeps() : default;
                }

                public IDmTask Untyped() => Value();
            }
            """, translated: true));
    }

    [Fact]
    public void ACallByNameReturningAsObjectTaskIsAllowedAndStoringItsResultIsNot()
    {
        // The shape of a call by name (waiting-model.md 4.5): a boxed task becomes a DmTask<object>.
        Expect(Analyze(Procs + """
            public static class by_name
            {
                public static DmTask<object> Result(object value) =>
                    value is IDmTask task ? task.AsObjectTask() : DmTask<object>.FromResult(value);

                public static DmTask<object> Maybe(object value) =>
                    (value as IDmTask)?.AsObjectTask() ?? DmTask<object>.FromResult(value);

                public static async DmTask<object> Stored(object value)
                {
                    var pending = (value as IDmTask)?.AsObjectTask();
                    return pending.HasValue ? await pending.Value : null; // Nullable's members are not task reads
                }
            }
            """, translated: true),
            ("ANOM0002", "value as IDmTask"));
    }

    [Fact]
    public void LambdasAndLocalFunctionsAreCheckedOnTheirOwnTerms()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;

                public void M()
                {
                    Func<DmTask> returns = () => Run();
                    Func<DmTask> stores = () => { var t = Run(); return t; };
                    Waiting.Detach(Local());

                    async DmTask Local()
                    {
                        DmTask<int> later = SleepsT();
                        await later;
                        await Run();
                    }
                }
            }
            """, translated: true),
            ("ANOM0002", "Run()"), ("ANOM0002", "SleepsT()"));
    }

    [Fact]
    public void ACallThroughAnAbstractDeclarationOrAGenericTaskIsTheSame()
    {
        Expect(Analyze(Procs + """
            public abstract class holder<T>
            {
                public abstract DmTask<T> Get();

                public async DmTask<T> Twice()
                {
                    DmTask<T> first = Get();
                    await first;
                    return await Get();
                }
            }
            """, translated: true),
            ("ANOM0002", "Get()"));
    }

    [Fact]
    public void OutsideTranslatedCodeStoringIsNotReported()
    {
        Expect(Analyze(Procs + """
            public class host
            {
                public async DmTask M(datum d)
                {
                    var t = d.Run();
                    await t;
                    datum.Take(d.Run());
                }
            }
            """, translated: false));
    }

    [Fact]
    public void TheMessageSaysWhatHappensToTheTask()
    {
        var messages = Messages(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;
                public void M() { Take(Run()); object o = Value(); }
            }
            """, translated: true);
        Assert.Equal(new[]
        {
            "ANOM0002: The DmTask of 'Run()' is passed to 'datum.Take': translated code awaits a task at once, passes it to a "
                + "Waiting.* call or returns it, and never stores it to await later",
            "ANOM0002: The DmTask of 'Value()' is converted to 'object': translated code awaits a task at once, passes it to a "
                + "Waiting.* call or returns it, and never stores it to await later",
        }, messages);
    }
}
