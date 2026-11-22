using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// Runtime errors, DM exceptions and the not-translated marker (docs/ai/translation-rules.md 1.4, 9.4, 9.5; BYOND ref
/// <c>/proc/CRASH</c>, <c>/exception</c>, <c>/proc/EXCEPTION</c>, the throw statement).
/// </summary>
public class ErrorTests
{
    [Fact]
    public void CrashCarriesTheMessageAndTheDmLocation()
    {
        DmCrashException e = Dm.CRASH("Subsystem did not fire", "code/controllers/subsystem.dm", 165);
        Assert.Equal("Subsystem did not fire", e.Message);
        Assert.Equal("code/controllers/subsystem.dm", e.DmFile);
        Assert.Equal(165, e.DmLine);
        Assert.IsAssignableFrom<DmRuntimeException>(e);
        Assert.Equal("", Dm.CRASH().Message);
        Assert.Null(Dm.CRASH("assert text").DmFile); // tg's ASSERT passes a finished message
    }

    [Fact]
    public void ExceptionMacroBuildsTheDatum()
    {
        exception e = Dm.EXCEPTION("bad state", "code/x.dm", 12);
        Assert.Equal("bad state", e.name);
        Assert.Equal("code/x.dm", e.file);
        Assert.Equal(12f, e.line);
        Assert.Null(e.desc);
    }

    [Fact]
    public void AThrownExceptionDatumIsTheCatchVariable()
    {
        exception thrown = Dm.EXCEPTION("oops", "code/x.dm", 3);
        exception caught = null;
        try
        {
            throw Dm.throw_value(thrown);
        }
        catch (Exception ex)
        {
            caught = Dm.exception(ex);
        }
        Assert.Same(thrown, caught);
    }

    [Fact]
    public void AThrownOtherValueIsTheCatchVariablesName()
    {
        DmThrowException ex = Dm.throw_value("just text");
        Assert.Equal("just text", ex.Value);
        Assert.Equal("just text", ex.Message);
        Assert.Equal("just text", Dm.exception(ex).name);
        Assert.Null(Dm.exception(Dm.throw_value(null)).name);
    }

    [Fact]
    public void ARuntimeErrorBecomesAnExceptionDatumWithItsMessageAndLocation()
    {
        exception fromCrash = Dm.exception(Dm.CRASH("boom", "code/y.dm", 7));
        Assert.Equal("boom", fromCrash.name);
        Assert.Equal("code/y.dm", fromCrash.file);
        Assert.Equal(7f, fromCrash.line);
        exception fromClr = Dm.exception(new InvalidOperationException("clr"));
        Assert.Equal("clr", fromClr.name);
        Assert.Null(fromClr.file);
        Assert.Null(Dm.exception(null));
    }

    [Fact]
    public void NotTranslatedThrowsWithoutAWorldAndByDefault()
    {
        var e = Assert.Throws<DmNotTranslatedException>(() => Dm.NotTranslated("code/datums/datum.dm:12", "vv_edit_var"));
        Assert.Equal("code/datums/datum.dm:12", e.Where);
        Assert.Equal("vv_edit_var", e.What);
        var w = new World();
        using (w.Enter())
            Assert.Throws<DmNotTranslatedException>(() => Dm.NotTranslated("code/x.dm:1", "p"));
    }

    [Fact]
    public void ARunningWorldReportsEachNotTranslatedSiteOnceAndCarriesOn()
    {
        var reported = new List<Exception>();
        var w = new World { RuntimeErrorSink = reported.Add };
        DmWorldState.Of(w).NotTranslatedThrows = false;
        using (w.Enter())
        {
            Dm.NotTranslated("code/x.dm:1", "p");
            Dm.NotTranslated("code/x.dm:1", "p");
            Dm.NotTranslated("code/x.dm:9", "q");
        }
        Assert.Equal(2, reported.Count);
        Assert.All(reported, e => Assert.IsType<DmNotTranslatedException>(e));
        Assert.Equal(2, w.RuntimeErrorCount);
    }
}
