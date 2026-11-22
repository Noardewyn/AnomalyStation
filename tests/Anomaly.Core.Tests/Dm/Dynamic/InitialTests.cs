using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// Prototypes and <c>initial()</c> (rules 10.2, 10.3; object-model.md sections 3 and 5): the compile-time value of a
/// var is what the constructor alone leaves, prototypes need no world, and building them changes no world.
/// </summary>
public class InitialTests
{
    [Fact]
    public void InitialReadsTheDeclaredDefaultAndTheSubtypesConstructorOverride()
    {
        // BYOND ref /proc/initial: "the original compile-time value of a variable"; rules 10.2: a subtype's override is
        // assigned in its constructor, the most derived one last.
        Assert.Equal("item", initial(typeof(fx_item), (fx_item p) => p.icon_state));
        Assert.Equal("sheetwhite", initial(typeof(fx_bedsheet), (fx_item p) => p.icon_state));
        Assert.Equal("sheetblue", initial(typeof(fx_bedsheet_blue), (fx_item p) => p.icon_state));
        Assert.Equal(5f, initial(typeof(fx_sheet), (fx_item p) => p.force));       // inherited from the stack's override
        Assert.Equal((float?)3, initial(typeof(fx_datum), (fx_datum p) => p.amount));
    }

    [Fact]
    public void InitialOfAnInstanceReadsItsRuntimeTypesPrototypeNotTheInstance()
    {
        // Rules 10.3: initial(x.var) is the runtime type's value, whatever x's declared type and current value.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_item sheet = Dm.@new<fx_bedsheet_blue>();
            sheet.icon_state = "torn";
            Assert.Equal("sheetblue", initial(sheet, (fx_item p) => p.icon_state));
            Assert.Equal("sheetblue", initial(sheet, "icon_state"));               // untyped x
            Assert.Equal(100f, initial(sheet, "health"));
        }
    }

    [Fact]
    public void AnObjectCreatingDefaultIsNullOnThePrototypeAndCreatedOnEveryInstance()
    {
        // Rules 10.2/10.3 and object-model.md section 5: a list() default runs in dm_init_vars, which prototypes skip,
        // so initial() reads null as in DM; each new object gets its own list, the ancestor's created and dropped first.
        Assert.Null(initial(typeof(fx_bedsheet_blue), (fx_bedsheet p) => p.dream_messages));
        Assert.Null(initial(typeof(fx_bedsheet_blue), "dream_messages"));
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_bedsheet_blue a = Dm.@new<fx_bedsheet_blue>();
            fx_bedsheet_blue b = Dm.@new<fx_bedsheet_blue>();
            Assert.Equal("blue", a.dream_messages[1f]);
            Assert.NotSame(a.dream_messages, b.dream_messages);
        }
    }

    [Fact]
    public void AConstantOverrideOfARuntimeInitializedVarRunsAfterTheAncestorsInitializer()
    {
        // Rules 10.2 (object-model.md section 13): the ancestor's list is created and dropped, as in OpenDream, whose
        // per-type init proc calls its parent's first; initial() reads null either way.
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            fx_bedsheet_plain plain = Dm.@new<fx_bedsheet_plain>();
            Assert.Null(plain.dream_messages);
            Assert.Equal(new object[] { "white" }, plain.seen_before_override);
        }
        Assert.Null(initial(typeof(fx_bedsheet_plain), (fx_bedsheet p) => p.dream_messages));
    }

    [Fact]
    public void PrototypesNeedNoWorldAndGetNoId()
    {
        // object-model.md sections 3 and 5: C# constructors never touch the World, so a prototype is built without one.
        Assert.Null(World.CurrentOrNull);
        Assert.Equal("stack", initial(typeof(fx_stack), (fx_item p) => p.icon_state));
        Assert.Equal(100f, initial(typeof(sound), (sound p) => p.volume));         // BYOND ref /sound/var/volume
        Assert.Equal(0, initial(typeof(fx_point), (fx_point p) => p.dm_id));
        Assert.Throws<DmRuntimeException>(() => initial(null, (fx_item p) => p.force));
        Assert.Throws<DmRuntimeException>(() => initial(typeof(fx_datum), (fx_item p) => p.force)); // a datum is no item
        Assert.Throws<DmRuntimeException>(() => initial(typeof(fx_datum), "no_such_var"));
    }

    [Fact]
    public void InitialOfANonDmTypeOrValueIsARuntimeError()
    {
        // BYOND ref /proc/initial: "the original compile-time value of a variable", which only DM types have. A host or
        // BCL type has no DM vars and is not built as a prototype (no object of an arbitrary .NET class is created).
        Assert.Throws<DmRuntimeException>(() => initial(typeof(System.Text.StringBuilder), (System.Text.StringBuilder p) => p.Capacity));
        Assert.Throws<DmRuntimeException>(() => initial(typeof(List<object>), "Count"));
        Assert.Throws<DmRuntimeException>(() => initial("text", "len"));
        Assert.Throws<DmRuntimeException>(() => initial(5f, "x"));
        Assert.Equal(0f, initial(typeof(DmList), "len"));                       // /list is a DM type
    }

    [Fact]
    public void BuildingPrototypesAndObjectsInOneWorldChangesNoOtherWorld()
    {
        // object-model.md section 13 (the cross-world identity test): ids are per world and prototypes belong to none,
        // so world B's id sequence and state hash do not depend on what world A built, nor on which prototypes exist.
        ulong fresh = new World(99).ComputeStateHash();
        var b = new World(99);
        ulong before = b.ComputeStateHash();
        Assert.Equal(fresh, before);

        var a = new World(99);
        using (a.Enter())
        {
            initial(typeof(fx_bedsheet), (fx_item p) => p.icon_state);
            initial(typeof(fx_point), "x");
            Dm.@new<fx_point>(1, 2, 3);
            Dm.@new<fx_bedsheet_blue>();
            Assert.Equal(3, a.AllocateId());
        }
        Assert.Equal(before, b.ComputeStateHash());
        Assert.Equal(1, b.AllocateId());                                           // B's first id is still 1
    }

    [Fact]
    public void AConstructorMayReadItsParentTypesInitialValue()
    {
        // Rules 10.3: parent_type::var inside a var override is Dm.initial(typeof(<parent class>), ...) in the constructor
        // (object-model.md section 5: it builds the parent's prototype while the child's is being built, no recursion).
        Assert.Equal(1f, initial(typeof(fx_point_shifted), (fx_point p) => p.x));
        Assert.Equal(16f, initial(typeof(fx_point_shifted), (fx_point p) => p.pixel_x));
        Assert.Equal(0f, initial(typeof(fx_point), (fx_point p) => p.x));            // the parent's own value is untouched
        World w = DynamicWorld.New(out _);
        using (w.Enter())
        {
            // New(_x, _y, _z, _pixel_x = 0, _pixel_y = 0) stores its defaulted _pixel_x (position_point_vector.dm:73, :91):
            // the instance reads 0 where initial() reads the type's 16; x keeps 1 because _x is null.
            fx_point_shifted point = Dm.@new<fx_point_shifted>(named("_z", 2));
            Assert.Equal((1f, 0f, 2f, 0f), (point.x, point.y, point.z, point.pixel_x));
            Assert.Equal(16f, initial(point, (fx_point p) => p.pixel_x));
        }
    }

    [Fact]
    public void AScenarioGivesTheSameIdsRefsAndHashWhetherTheShimsCachesAreColdOrWarm()
    {
        // object-model.md section 13 (the cross-world identity test, with the caches a fresh process starts without):
        // the first run builds the factories, invokers, accessors and prototypes of types no other test uses, the second
        // finds them built. Nothing of that is world state, so both runs give the same ids, references and state hash.
        (ulong Hash, string Refs) cold = ColdScenario();
        (ulong Hash, string Refs) warm = ColdScenario();
        Assert.Equal(cold, warm);
        Assert.Equal("[0x1] [0x2] [0x3]", cold.Refs);
    }

    private static (ulong, string) ColdScenario()
    {
        var w = new World(17);
        var keep = new List<object>();
        string refs;
        using (w.Enter())
        {
            fx_cold parent = Dm.@new<fx_cold>();
            Assert.Equal(50f, initial(typeof(fx_cold_child), (fx_datum p) => p.health));
            Assert.Equal("a cold child", initial(typeof(fx_cold_child), "desc"));
            fx_cold_child child = Dm.@new<fx_cold_child>(1);
            Assert.Equal(54f, Waiting.NoSleep(Dm.Call(child, "warm", named("by", 2))));
            Assert.True(hascall(parent, "warm"));
            set_var(parent, "desc", "warmed");
            Assert.Equal("warmed", get_var(parent, "desc"));
            DmList types = typesof(typeof(fx_cold));
            Assert.Equal((2, typeof(fx_cold), typeof(fx_cold_child)), (types.Count, (Type)types[1f], (Type)types[2f]));
            fx_datum third = Dm.@new<fx_datum>();
            refs = @ref(parent) + " " + @ref(child) + " " + @ref(third);
            keep.Add(parent);
            keep.Add(child);
            keep.Add(third);
        }
        ulong hash = w.ComputeStateHash();
        GC.KeepAlive(keep);
        return (hash, refs);
    }

    [Fact]
    public void TheSameCreationsGiveTheSameIdsAndStateHashInEveryWorld()
    {
        // translation-rules.md 15: the same scenario twice gives the same World.ComputeStateHash().
        ulong first = Scenario();
        initial(typeof(fx_sheet), (fx_item p) => p.icon_state);                    // a prototype built in between
        Assert.Equal(first, Scenario());
    }

    private static ulong Scenario()
    {
        var w = new World(5);
        var keep = new List<object>();
        using (w.Enter())
        {
            fx_atom room = Dm.@new<fx_atom>();
            keep.Add(room);
            keep.Add(Dm.@new<fx_movable>(room));
            keep.Add(Dm.@new<fx_point>(named("_z", 2)));
        }
        ulong hash = w.ComputeStateHash();
        GC.KeepAlive(keep);
        return hash;
    }
}
