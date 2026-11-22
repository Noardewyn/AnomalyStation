using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>Runtime var access by name, DM <c>a:b</c>, <c>a?:b</c> and <c>a:b = v</c> (rules 4.8, 3.5, 3.8).</summary>
public class VarAccessTests
{
    [Fact]
    public void ReadsAndWritesFieldsAndBuiltinPropertiesByName()
    {
        var atom = new fx_atom { name = "crate" };
        Assert.Equal("crate", get_var(atom, "name"));
        Assert.Equal(100f, get_var(atom, "health"));
        Assert.Same(atom.contents, get_var(atom, "contents"));          // an engine property (object-model.md 2)
        Assert.Equal("box", set_var(atom, "name", "box"));
        Assert.Equal("box", atom.name);
        Assert.Equal(typeof(fx_atom), get_var(atom, "type"));
    }

    [Fact]
    public void WritesConvertTheValueToTheVarsCSharpTypeByRules35()
    {
        var item = new fx_item();
        set_var(item, "force", 7);                                     // int -> float (Dm.num)
        Assert.Equal(7f, item.force);
        set_var(item, "force", null);                                  // null -> 0
        Assert.Equal(0f, item.force);
        set_var(item, "amount", null);                                 // float? keeps null (Dm.num_or_null)
        Assert.Null(item.amount);
        set_var(item, "loc", new fx_datum());                          // a class var: `as`, another kind gives null
        Assert.Null(item.loc);
        Assert.Throws<DmRuntimeException>(() => set_var(item, "force", "heavy"));  // text into a number: type mismatch
    }

    [Fact]
    public void AnUnknownVarOrANullObjectIsARuntimeError()
    {
        // BYOND: reading a var the object does not have, or a var of null, is a runtime error.
        Assert.Throws<DmRuntimeException>(() => get_var(new fx_datum(), "no_such_var"));
        Assert.Throws<DmRuntimeException>(() => get_var(null, "name"));
        Assert.Throws<DmRuntimeException>(() => set_var(null, "name", "x"));
        Assert.Throws<DmRuntimeException>(() => set_var(new fx_datum(), "type", typeof(fx_atom)));   // read-only
        Assert.Throws<DmRuntimeException>(() => get_var(typeof(fx_datum), "Name"));   // a .NET object has no DM vars
        Assert.Throws<DmRuntimeException>(() => get_var(5f, "type"));
        Assert.Null(get_var_or_null(null, "name"));                    // a?:b gives null for a null a
        Assert.Equal("a datum", get_var_or_null(new fx_datum(), "desc"));
    }

    [Fact]
    public void TheEnginesTypeVarsExistOnEveryObject()
    {
        // BYOND: every datum has type and parent_type (rules 10.7); a list's type is /list.
        Assert.Equal(typeof(DmList), get_var(list(), "type"));
        Assert.Equal(typeof(fx_item), get_var(new fx_stack(), "parent_type"));
    }

    [Fact]
    public void DatumVarsIsNotTranslated()
    {
        // Rules 10.7: vars is not translated (Dm.NotTranslated: it throws in tests).
        Assert.Throws<DmNotTranslatedException>(() => get_var(new fx_datum(), "vars"));
    }

    [Fact]
    public void DatumVarsInARunningWorldIsReportedOnceAndReadsNull()
    {
        // Rules 1.4 and 10.7: in a running world (NotTranslatedThrows off) Dm.NotTranslated reports once per site through
        // World.ReportRuntime and returns, so a read of vars by name gives null and the world carries on.
        World w = DynamicWorld.New(out List<string> errors);
        DmWorldState.Of(w).NotTranslatedThrows = false;
        using (w.Enter())
        {
            fx_datum datum = Dm.@new<fx_datum>();
            Assert.Null(get_var(datum, "vars"));
            Assert.Null(get_var_or_null(datum, "vars"));
            Assert.Null(initial(datum, "vars"));
        }
        string report = Assert.Single(errors);
        Assert.StartsWith(nameof(DmNotTranslatedException) + ": not translated yet: datum.vars", report);
    }

    [Fact]
    public void AccessorsAreCachedPerTypeAndName()
    {
        DmVars.Accessor first = DmVars.Find(typeof(fx_item), "force");
        Assert.Same(first, DmVars.Find(typeof(fx_item), "force"));
        Assert.Same(DmVars.Accessor.Missing, DmVars.Find(typeof(fx_item), "no_such_var"));
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_item item = Dm.@new<fx_item>();
            set_var(item, "icon_state", "x");
            Assert.Equal("x", get_var(item, "icon_state"));
        }
    }
}
