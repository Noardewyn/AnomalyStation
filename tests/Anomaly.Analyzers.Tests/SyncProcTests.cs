using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>ANOM0005: [SignalHandler] and [ShouldNotSleep] methods, own or inherited, stay synchronous.</summary>
public class SyncProcTests
{
    private const string Procs = """
        public class datum
        {
            public virtual DmTask update_icon() => DmTask.CompletedTask;
            public static async DmTask Sleeps() { await sleep(1); }

            [ShouldNotSleep, ShouldCallParent]
            public virtual object update_appearance() => null;

            [SignalHandler]
            public virtual void on_moved() { }

            public virtual void plain() { }
        }

        """;

    [Fact]
    public void AnAwaitInAMarkedMethodIsReportedWithItsAsyncDeclaration()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                [SignalHandler]
                public async void on_crossed()
                {
                    await Sleeps();
                }

                [ShouldNotSleep]
                public DmTask returns_task() => DmTask.CompletedTask;

                [ShouldNotSleep]
                public async DmTask<int> sleeps() { await sleep(1); return 1; }
            }
            """),
            ("ANOM0005", "on_crossed"), ("ANOM0005", "await Sleeps()"), ("ANOM0005", "returns_task"),
            ("ANOM0005", "sleeps"), ("ANOM0005", "await sleep(1)"));
    }

    [Fact]
    public void TheAttributeIsInheritedThroughTheOverriddenChain()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override object update_appearance() => base.update_appearance();
            }

            public class gadget : thing
            {
                public override async void on_moved() { await sleep(1); }
            }

            public class widget : gadget
            {
                public override object update_appearance() => Waiting.NoSleep(Value());
                private static DmTask<object> Value() => DmTask<object>.FromResult(null);
            }

            public class sub : widget
            {
                public override async void on_moved() { await Sleeps(); }
            }

            public abstract class shape
            {
                [SignalHandler]
                public abstract DmTask on_hit();
            }

            public class circle : shape
            {
                public override async DmTask on_hit() { await sleep(1); }
            }
            """),
            ("ANOM0005", "on_moved"), ("ANOM0005", "await sleep(1)"), ("ANOM0005", "on_moved"), ("ANOM0005", "await Sleeps()"),
            ("ANOM0005", "on_hit"), ("ANOM0005", "on_hit"), ("ANOM0005", "await sleep(1)"));
    }

    [Fact]
    public void TheAttributeIsInheritedFromAnotherAssembly()
    {
        var library = Compile(Procs, assemblyName: "Lib");
        Expect(Analyze("""
            public class thing : datum
            {
                public override async void on_moved() { await sleep(1); }
            }
            """, extra: new[] { AsReference(library) }),
            ("ANOM0005", "on_moved"), ("ANOM0005", "await sleep(1)"));
    }

    [Fact]
    public void NestedLambdasAndLocalFunctionsAreNotTheMethodsOwnAwaits()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                [SignalHandler]
                public void on_entered()
                {
                    Waiting.NoSleep(update_icon());
                    Waiting.Spawn(-1, async () => { await sleep(1); });
                    Waiting.Detach(Local());
                    Waiting.Detach(Typed());

                    async DmTask Local() { await Sleeps(); }
                    async DmTask<int> Typed() { await sleep(1); return 1; }
                }

                public override object update_appearance()
                {
                    Func<DmTask> later = async () => await update_icon();
                    Waiting.Spawn(0, later);
                    return Waiting.NoSleep(Value());
                }

                private static DmTask<object> Value() => DmTask<object>.FromResult(null);
            }
            """));
    }

    [Fact]
    public void UnmarkedMethodsAndHidingMethodsAreNotSyncMandated()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override async void plain() { await sleep(1); }
                public new async DmTask on_moved() { await sleep(1); } // hides, does not override: a different slot
                public override async DmTask update_icon() { await Sleeps(); }
            }
            """));
    }

    [Fact]
    public void TheMessageNamesTheAttributeWhereItIsDeclaredAndTheFix()
    {
        var messages = Messages(Procs + """
            public class thing : datum
            {
                public override async void on_moved() { await update_icon(); }
            }
            """);
        Assert.Equal(new[]
        {
            "ANOM0005: 'on_moved' is [SignalHandler] (declared on 'datum.on_moved'), so it stays synchronous: remove 'async' "
                + "(and the DmTask return type) and call procs of DmTask slots with Waiting.NoSleep(...)",
            "ANOM0005: 'on_moved' is [SignalHandler] (declared on 'datum.on_moved'), so it stays synchronous: replace "
                + "'await update_icon()' with Waiting.NoSleep(...)",
        }, messages);
    }
}
