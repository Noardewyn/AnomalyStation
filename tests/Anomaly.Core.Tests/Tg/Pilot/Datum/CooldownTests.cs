using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The timer-cooldown procs of code/datums/datum.dm (end_cooldown :295, reset_cooldown :311) and the cooldown macros
/// they use (code/__DEFINES/cooldowns.dm:74-98), written from the DM source. The timers that call them are the timer
/// package's; here the procs are called as the timer would call them.
/// </summary>
public class CooldownTests
{
    private static test_datum_listener Listen(datum source, string signal, List<string> trace)
    {
        test_datum_listener l = Dm.@new<test_datum_listener>();
        l.trace = trace;
        l.label = "l";
        l.RegisterSignal(source, signal, nameof(test_datum_listener.on_cooldown_signal));
        return l;
    }

    [Fact]
    public void TheCooldownSignalsAreNamedAfterTheIndex()
    {
        Assert.Equal("cooldown_pilot", COMSIG_CD_STOP("pilot"));
        Assert.Equal("cd_reset_pilot", COMSIG_CD_RESET("pilot"));
        Assert.Equal("cooldown_7", COMSIG_CD_STOP(7));
    }

    [Fact]
    public void EndCooldownSignalsThenEndsOnlyThatCooldown()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            source.cooldowns = Dm.list(Dm.kv("pilot", -1), Dm.kv("other", 4));
            Listen(source, COMSIG_CD_STOP("pilot"), trace);

            Assert.True(Dm.eq(TIMER_COOLDOWN_RUNNING(source, "pilot"), -1));
            end_cooldown(source, "pilot");

            // SEND_SIGNAL(source, COMSIG_CD_STOP(index)) with no argument but the source, while the cooldown is still
            // set, then TIMER_COOLDOWN_END (datum.dm:298-299).
            Assert.Equal(new[] { "l cooldown signal timeleft=null extra=null running=-1" }, trace);
            Assert.Null(TIMER_COOLDOWN_RUNNING(source, "pilot"));
            Assert.Equal(new object[] { "other" }, Dm.each(source.cooldowns).ToArray());

            // LAZYREMOVE nulls the list when it empties.
            end_cooldown(source, "other");
            Assert.Null(source.cooldowns);
            Assert.Null(TIMER_COOLDOWN_RUNNING(source, "other"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ResetCooldownWithoutListenersOnlyEndsTheCooldown()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum source = Dm.@new<datum>();
            source.cooldowns = Dm.list(Dm.kv("pilot", 3));

            // SEND_SIGNAL evaluates its arguments only when someone listens (rules 11.1 (e)), so the time left
            // (S_TIMER_COOLDOWN_TIMELEFT, which reads the timer) is never asked for here.
            reset_cooldown(source, "pilot");
            Assert.Null(source.cooldowns);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ResetCooldownSignalsTheTimeLeftWhileTheCooldownRunsThenEndsIt()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            // timeleft (controllers/subsystem/timer.dm:687) looks the timer id up in SStimer: "3", a TIMER_STOPPABLE id
            // (num2text(nextid, 100), timer.dm:410) of a timer that has already run and left timer_id_dict, gives null.
            Dm.@new<datum_controller_subsystem_timer>();
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            source.cooldowns = Dm.list(Dm.kv("pilot", "3"), Dm.kv("other", 4));
            Listen(source, COMSIG_CD_RESET("pilot"), trace);

            reset_cooldown(source, "pilot");

            // SEND_SIGNAL(source, COMSIG_CD_RESET(index), S_TIMER_COOLDOWN_TIMELEFT(source, index)) while the cooldown is
            // still set, then TIMER_COOLDOWN_END (datum.dm:314-315).
            Assert.Equal(new[] { "l cooldown signal timeleft=null extra=null running=3" }, trace);
            Assert.Equal(new object[] { "other" }, Dm.each(source.cooldowns).ToArray());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ADeletedSourceKeepsItsCooldowns()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum source = Dm.@new<datum>();
            Listen(source, COMSIG_CD_STOP("pilot"), trace);
            DmList cooldowns = Dm.list(Dm.kv("pilot", 1));
            qdel(source);
            source.cooldowns = cooldowns;

            end_cooldown(source, "pilot");
            reset_cooldown(source, "pilot");
            end_cooldown(null, "pilot");

            // if(QDELETED(source)) return (datum.dm:296-297, :312-313)
            Assert.Same(cooldowns, source.cooldowns);
            Assert.Equal(1f, cooldowns.len);
            Assert.Empty(trace);
            Assert.Empty(t.Runtimes);
        }
    }
}
