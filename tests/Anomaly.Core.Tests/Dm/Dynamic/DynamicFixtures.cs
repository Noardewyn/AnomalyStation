using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Dynamic;

// The test assembly's own global procs, found by calls with a null receiver like tg's GlobalProcs
// (docs/ai/specs/object-model.md section 11).
[assembly: DmGlobalProcs(typeof(DynamicFixtureGlobals))]

namespace Anomaly.Core.Tests.Dynamic;

// Small stand-ins for translated tg classes (rules 2.3, 3.8, 7.1-7.4, 10.2), shaped the way the translation rules
// shape them: snake_case DM names, public fields for vars, a virtual slot per proc, [DmParams] on overrides that
// rename or add parameters, the pre-New hook. Their DM paths live under /datum/dynfx so that they never claim a
// path of tg's own classes; they are C# roots, so the registry gives the tree root /datum/dynfx no DM parent.

/// <summary>Stand-in for tg's <c>/datum</c> (object-model.md 1.1): identity, the pre-New hook, the <c>New</c> slot.</summary>
[DmPath("/datum/dynfx")]
public class fx_datum : IDmObject
{
    /// <summary>What New and the pre-New hook saw, in order (a recording harness, rules 15).</summary>
    public readonly List<string> log = new();

    /// <summary>DM <c>tag</c> (a plain field here; tg's engine part keeps a tag index).</summary>
    public string tag;

    /// <summary>A var with a constant default (rules 10.2: a field initializer).</summary>
    public float health = 100;

    /// <summary>A null-used numeric var (rules 3.2 V4).</summary>
    public float? amount = 3;

    public string desc = "a datum";

    public object gc_destroyed;

    public long dm_id { get; private set; }

    public void dm_assign_id(long id)
    {
        if (dm_id != 0) throw new InvalidOperationException("id assigned twice");
        dm_id = id;
    }

    public virtual void dm_pre_new(DmArgs args)
    {
        log.Add("pre_new");
        dm_init_vars();
    }

    /// <summary>The object-creating var initializers (rules 10.2), ancestors first.</summary>
    protected internal virtual void dm_init_vars()
    {
    }

    /// <summary>The <c>/datum</c> <c>New</c> slot (Appendix A: <c>public virtual void New(params object[] __extra)</c>).</summary>
    public virtual void New(params object[] __extra)
    {
        log.Add("datum.New " + __extra.Length);
    }

    /// <summary>The DM type, as tg's engine part exposes it (object-model.md section 2).</summary>
    public Type type => GetType();
}

/// <summary>Stand-in for <c>/atom</c>: contents, name, gender; the silent placement in the pre-New hook.</summary>
[DmPath("/datum/dynfx/atom")]
public class fx_atom : fx_datum, IDmAtom
{
    public fx_atom loc;

    public string name = "atom";

    public string gender = "neuter";

    public DmList contents { get; } = new DmList();

    string IDmAtom.name => name;

    string IDmAtom.gender => gender;

    public object new_loc_param;

    public override void dm_pre_new(DmArgs args)
    {
        base.dm_pre_new(args);
        // BYOND ref "New proc (atom)": the object is already at its initial location when New runs.
        loc = args.InitialLocation as fx_atom;
        loc?.contents.AppendItem(this);
    }

    /// <summary>tg <c>/atom/New(loc, ...)</c> (Appendix A): all DM parameters are extras, named by [DmParams].</summary>
    [DmParams("loc")]
    public override void New(params object[] __extra)
    {
        new_loc_param = Dm.arg(__extra, 0);
        log.Add("atom.New");
    }
}

/// <summary>Stand-in for <c>/atom/movable</c>.</summary>
[DmPath("/datum/dynfx/atom/movable")]
public class fx_movable : fx_atom
{
}

/// <summary>An atom whose <c>New</c> calls its location parameter differently (BYOND: loc is still hard-coded).</summary>
[DmPath("/datum/dynfx/atom/movable/renamed")]
public class fx_renamed_loc : fx_movable
{
    public object where;

    [DmParams("where")]
    public override void New(params object[] __extra)
    {
        where = Dm.arg(__extra, 0);
        base.New(__extra);
    }
}

/// <summary>
/// Stand-in for <c>/obj/item</c> with the <c>use(used)</c> slot that overrides make variadic (Appendix A:
/// <c>public virtual object use(object used = null, params object[] __extra)</c>), and the bedsheet var of rules 10.2.
/// </summary>
[DmPath("/datum/dynfx/atom/movable/item")]
public class fx_item : fx_movable
{
    public string icon_state = "item";

    public float force = 0;

    public List<object> used_with;

    public virtual object use(object used = null, params object[] __extra)
    {
        used_with = new List<object> { used };
        return 1f;
    }
}

/// <summary>
/// Stand-in for <c>/obj/item/stack</c>: tg's <c>use(used, transfer = FALSE, check = TRUE)</c> (stack.dm:586) as rules 7.2
/// translate it, with the extra parameters unpacked from <c>__extra</c> and their defaults applied by the prologue.
/// </summary>
[DmPath("/datum/dynfx/atom/movable/item/stack")]
public class fx_stack : fx_item
{
    public fx_stack()
    {
        force = 5;
        icon_state = "stack";
    }

    [DmParams("used", "transfer", "check")]
    public override object use(object used = null, params object[] __extra)
    {
        float? transfer = Dm.arg<float?>(__extra, 0);
        float? check = Dm.arg<float?>(__extra, 1);
        transfer ??= Dm.FALSE;
        check ??= Dm.TRUE;
        used_with = new List<object> { used, transfer, check };
        return 2f;
    }
}

/// <summary>An override that renames the slot's first parameter (rules 7.2: [DmParams] lists all DM names).</summary>
[DmPath("/datum/dynfx/atom/movable/item/stack/sheet")]
public class fx_sheet : fx_stack
{
    [DmParams("amount", "transfer", "check")]
    public override object use(object used = null, params object[] __extra)
    {
        return base.use(used, __extra);
    }
}

/// <summary>Rules 10.2's bedsheet: a constructor-assigned override and an object-creating default in dm_init_vars.</summary>
[DmPath("/datum/dynfx/atom/movable/item/bedsheet")]
public class fx_bedsheet : fx_item
{
    public DmList dream_messages;

    public fx_bedsheet()
    {
        icon_state = "sheetwhite";
    }

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dream_messages = Dm.list("white");
    }
}

/// <summary>Rules 10.2's blue bedsheet.</summary>
[DmPath("/datum/dynfx/atom/movable/item/bedsheet/blue")]
public class fx_bedsheet_blue : fx_bedsheet
{
    public fx_bedsheet_blue()
    {
        icon_state = "sheetblue";
    }

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dream_messages = Dm.list("blue");
    }
}

/// <summary>
/// A constant override of a var that an ancestor initializes at run time (rules 10.2: <c>dream_messages = null</c>
/// goes into the subtype's <c>dm_init_vars</c>, after the ancestor's initializer has run).
/// </summary>
[DmPath("/datum/dynfx/atom/movable/item/bedsheet/plain")]
public class fx_bedsheet_plain : fx_bedsheet
{
    public List<object> seen_before_override;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        seen_before_override = new List<object> { dream_messages?[1f] };
        dream_messages = null;
    }
}

/// <summary>
/// tg's <c>/datum/point</c> (code/datums/position_point_vector.dm:73,
/// <c>New(_x, _y, _z, _pixel_x = 0, _pixel_y = 0)</c>), the named-argument case of rules 7.3.
/// </summary>
[DmPath("/datum/dynfx/point")]
public class fx_point : fx_datum
{
    public float x = 0;
    public float y = 0;
    public float z = 0;
    public float pixel_x = 0;
    public float pixel_y = 0;

    [DmParams("_x", "_y", "_z", "_pixel_x", "_pixel_y")]
    public override void New(params object[] __extra)
    {
        object _x = Dm.arg(__extra, 0);
        object _y = Dm.arg(__extra, 1);
        object _z = Dm.arg(__extra, 2);
        float? _pixel_x = Dm.arg<float?>(__extra, 3);
        float? _pixel_y = Dm.arg<float?>(__extra, 4);
        _pixel_x ??= 0;
        _pixel_y ??= 0;
        initialize_location(_x, _y, _z, _pixel_x, _pixel_y);
    }

    // tg's initialize_location without the pixel-to-tile carry (position_point_vector.dm:91).
    public virtual void initialize_location(object tile_x = null, object tile_y = null, object tile_z = null, object p_x = null, object p_y = null)
    {
        if (tile_x != null)
            x = Dm.num(tile_x);
        if (tile_y != null)
            y = Dm.num(tile_y);
        if (tile_z != null)
            z = Dm.num(tile_z);
        if (p_x != null)
            pixel_x = Dm.num(p_x);
        if (p_y != null)
            pixel_y = Dm.num(p_y);
    }
}

/// <summary>
/// Procs of every shape the binder adapts: typed parameters, C# keywords as DM names, a var/proc clash
/// (rules 2.4), void, coloured and value returns, throwing procs.
/// </summary>
[DmPath("/datum/dynfx/caller")]
public class fx_callee : fx_datum
{
    /// <summary>A var that clashes with a proc of the same name: the proc is <c>charge__proc</c> (rules 2.4).</summary>
    public float charge = 5;

    public object last;

    public List<object> seen;

    public virtual object charge__proc(object amount = null)
    {
        charge += Dm.num(amount);
        return charge;
    }

    public virtual object pair(object a = null, object b = null)
    {
        seen = new List<object> { a, b };
        return a;
    }

    public virtual object variadic(object a = null, params object[] __extra)
    {
        seen = new List<object> { a };
        seen.AddRange(__extra);
        return __extra.Length;
    }

    public virtual object typed(float? n = null, string s = null, fx_atom atom = null, DmList list = null, Type path = null)
    {
        seen = new List<object> { n, s, atom, list, path };
        return null;
    }

    public virtual object numeric(float f = 0)
    {
        seen = new List<object> { f };
        return f;
    }

    /// <summary>C# keywords as DM parameter names (rules 2.2): reflection reports them without the <c>@</c>.</summary>
    public virtual object fire(object @event = null, object @params = null)
    {
        seen = new List<object> { @event, @params };
        return null;
    }

    /// <summary>A shim-builtin-like C# default (<c>Cut(Start = 1, End = 0)</c>).</summary>
    public object with_default(float start = 1, float end = 0)
    {
        seen = new List<object> { start, end };
        return start;
    }

    /// <summary>
    /// tg's <c>/datum/component/vision_hurting/Initialize(damage_per_second=1, message="Your eyes burn as you look at",
    /// silent = FALSE)</c> (code/datums/components/vision_hurting.dm:8) with its default prologue (rules 7.2).
    /// </summary>
    public virtual object hurt(object damage_per_second = null, object message = null, object silent = null)
    {
        damage_per_second ??= 1f;
        message ??= "Your eyes burn as you look at";
        silent ??= Dm.FALSE;
        seen = new List<object> { damage_per_second, message, silent };
        return null;
    }

    /// <summary>
    /// A proc defined twice on one type (docs/ai/specs/move-chain.md section 9, item 1; rules 7.5): the earlier
    /// definition is the non-virtual <c>twice__d0</c>, which the later one's <c>..()</c> calls.
    /// </summary>
    public object twice__d0() => "first";

    public virtual object twice() => "second, then " + twice__d0();

    /// <summary>A proc defined three times: <c>thrice__d0</c>, <c>thrice__d1</c>, then <c>thrice</c>.</summary>
    public object thrice__d0() => "first";

    public object thrice__d1() => "second, then " + thrice__d0();

    public virtual object thrice() => "third, then " + thrice__d1();

    /// <summary>Names that only look like an earlier definition: still procs.</summary>
    public object d1() => "d1";

    public object not__dx() => "not__dx";

    /// <summary>Members of byref-like types, which a C# helper may have and DM never does.</summary>
    public object span_taker(ReadOnlySpan<char> text) => text.Length;

    public ReadOnlySpan<char> span_maker() => "span".AsSpan();

    public ReadOnlySpan<char> span_view => "view".AsSpan();

    public virtual void nothing()
    {
        last = "nothing ran";
    }

    public virtual bool condition() => true;

    public virtual string text() => "text";

    public virtual object add_one(object x = null) => Dm.num(x) + 1;

    public virtual async DmTask<object> nap(object ds = null)
    {
        last = "napping";
        await Dm.sleep(Dm.num(ds));
        last = "woke";
        return "rested";
    }

    public virtual async DmTask nap_void(object ds = null)
    {
        await Dm.sleep(Dm.num(ds));
        last = "woke (void)";
    }

    public virtual async DmTask<float> quick() => await DmTask<float>.FromResult(7f);

    public virtual object throws_crash() => throw Dm.CRASH("crashed", "code/dynfx.dm", 12);

    public virtual object throws_clr() => throw new InvalidOperationException("a C# error");

    public virtual async DmTask<object> throws_after_sleep()
    {
        await Dm.sleep(1);
        throw new ArgumentException("late error");
    }

    /// <summary>A coloured proc that fails before its first sleep without <paramref name="ds"/>, after it with one.</summary>
    public virtual async DmTask<object> fails(object ds = null)
    {
        if (ds == null)
            throw new KeyNotFoundException("early error");
        await Dm.sleep(Dm.num(ds));
        throw new KeyNotFoundException("late error");
    }

    /// <summary>The same for a coloured proc without a value (<c>DmTask</c>).</summary>
    public virtual async DmTask fails_void(object ds = null)
    {
        if (ds == null)
            throw new FormatException("early void error");
        await Dm.sleep(Dm.num(ds));
        throw new FormatException("late void error");
    }
}

/// <summary>An override of the callee that renames <c>pair</c>'s parameters (rules 7.2 [DmParams]).</summary>
[DmPath("/datum/dynfx/caller/renamed")]
public class fx_callee_renamed : fx_callee
{
    [DmParams("first", "second")]
    public override object pair(object a = null, object b = null) => base.pair(a, b);

    [DmParams("@event", "params")]
    public override object fire(object @event = null, object @params = null) => base.fire(@event, @params);
}

/// <summary>
/// Stand-in for tg's <c>/client</c>: a builtin root that tg re-parents under <c>/datum</c>, whose <c>New</c> is its own
/// slot and hides the inherited one (rules 7.1: <c>public new virtual ... New(...)</c>).
/// </summary>
[DmPath("/datum/dynfx/client")]
public class fx_client : fx_datum
{
    public new virtual void New(params object[] __extra)
    {
        log.Add("client.New " + __extra.Length);
    }
}

/// <summary>A subtype of the hiding <c>New</c>'s class that overrides it.</summary>
[DmPath("/datum/dynfx/client/sub")]
public class fx_client_sub : fx_client
{
    public override void New(params object[] __extra)
    {
        log.Add("client_sub.New");
        base.New(__extra);
    }
}

/// <summary>A subtype that inherits the hiding <c>New</c> without overriding it.</summary>
[DmPath("/datum/dynfx/client/sub/leaf")]
public class fx_client_leaf : fx_client_sub
{
}

/// <summary>
/// Records what the pre-New hook and <c>New</c> see of the object's identity, and creates another object in its
/// object-creating var initializer (rules 10.2: <c>var/datum/point/inner = new(1, 2, 3)</c> is assigned in
/// <c>dm_init_vars</c>).
/// </summary>
[DmPath("/datum/dynfx/id_probe")]
public class fx_id_probe : fx_datum
{
    public fx_point inner;

    public List<object> seen_at_pre_new;

    public List<object> seen_at_new;

    public override void dm_pre_new(DmArgs args)
    {
        seen_at_pre_new = Identity();
        base.dm_pre_new(args);
    }

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        inner = Dm.@new<fx_point>(1, 2, 3);
    }

    public override void New(params object[] __extra)
    {
        seen_at_new = Identity();
        base.New(__extra);
    }

    private List<object> Identity()
        => new() { dm_id, World.Current.Objects.TryGet(dm_id, out object registered) && ReferenceEquals(registered, this) };
}

/// <summary>
/// tg's <c>parent_type::var</c> in a var override (rules 10.3): the constructor reads the parent type's prototype,
/// <c>x = parent_type::x + 1</c>.
/// </summary>
[DmPath("/datum/dynfx/point/shifted")]
public class fx_point_shifted : fx_point
{
    public fx_point_shifted()
    {
        x = Dm.initial(typeof(fx_point), (fx_point p) => p.x) + 1;
        pixel_x = Dm.initial(typeof(fx_point), (fx_point p) => p.pixel_x) + 16;
    }
}

/// <summary>Types that only the cold-cache scenario uses, so its first run builds their factories, invokers and prototypes.</summary>
[DmPath("/datum/dynfx/cold")]
public class fx_cold : fx_datum
{
    public fx_cold()
    {
        health = 50;
    }

    public virtual object warm(object amount = null)
    {
        health += Dm.num(amount);
        return health;
    }
}

[DmPath("/datum/dynfx/cold/child")]
public class fx_cold_child : fx_cold
{
    public fx_cold_child()
    {
        desc = "a cold child";
    }

    [DmParams("by")]
    public override object warm(object amount = null)
    {
        return base.warm(Dm.num(amount) * 2);
    }
}

/// <summary>A type only the cold-cache parallel test uses, so its threads meet every shim cache empty.</summary>
[DmPath("/datum/dynfx/parallel")]
public class fx_parallel : fx_datum
{
    public object seed;

    public fx_parallel()
    {
        desc = "parallel";
    }

    [DmParams("seed")]
    public override void New(params object[] __extra)
    {
        seed = Dm.arg(__extra, 0);
        base.New(__extra);
    }

    public virtual object twice_seed(object plus = null) => Dm.num(seed) * 2 + Dm.num(plus);
}

/// <summary>A type whose procs typesof(/t/proc) lists: declared ones only, in declaration order.</summary>
[DmPath("/datum/dynfx/procs")]
public class fx_procs : fx_datum
{
    /// <summary>An earlier duplicate definition (rules 2.5 <c>__d0</c>): the port's name, no DM proc of its own.</summary>
    public object zeta__d0() => "zeta first";

    public virtual object zeta() => "zeta";

    public void dm_hook()
    {
    }

    public virtual object alpha() => "alpha";

    public virtual object start__proc() => "start";

    public override void New(params object[] __extra) => base.New(__extra);
}

/// <summary>
/// A root with a coloured <c>New</c> slot (waiting-model.md 4.3: <c>/datum</c> <c>New</c> is coloured at the snapshot),
/// for <c>Dm.new_async</c> and the no-sleep rule of <c>Dm.@new</c> (DV5).
/// </summary>
[DmPath("/datum/dynfx_async")]
public class fx_async_datum : IDmObject
{
    public readonly List<string> log = new();

    public long dm_id { get; private set; }

    public void dm_assign_id(long id) => dm_id = id;

    public void dm_pre_new(DmArgs args) => log.Add("pre_new");

    public virtual DmTask New(params object[] __extra)
    {
        log.Add("New");
        return DmTask.CompletedTask;
    }
}

/// <summary>A <c>New</c> that sleeps (a <c>sleeping-new.tsv</c> row with <c>suspends = yes</c>).</summary>
[DmPath("/datum/dynfx_async/sleeper")]
public class fx_sleeping_new : fx_async_datum
{
    [DmParams("ds")]
    public override async DmTask New(params object[] __extra)
    {
        log.Add("New start");
        await Dm.sleep(Dm.num(Dm.arg(__extra, 0)));
        log.Add("New end");
    }
}

/// <summary>A <c>New</c> that fails after it slept.</summary>
[DmPath("/datum/dynfx_async/failing")]
public class fx_failing_new : fx_async_datum
{
    public override async DmTask New(params object[] __extra)
    {
        await Dm.sleep(1);
        throw new InvalidOperationException("New failed late");
    }
}

/// <summary>A datum whose <c>New</c> throws at once.</summary>
[DmPath("/datum/dynfx/broken")]
public class fx_broken : fx_datum
{
    public override void New(params object[] __extra) => throw Dm.CRASH("bad New", "code/dynfx.dm", 40);
}

/// <summary>
/// A datum below a BYOND builtin value type, like tg's <c>/mutable_appearance/emissive_blocker</c>: created by
/// <c>new</c> with its var initializers and <c>New</c>, but given no world id (object-model.md section 3).
/// </summary>
[DmPath("/mutable_appearance/dynfx_overlay")]
public class fx_overlay : IDmObject
{
    public readonly List<string> log = new();

    public DmList overlays;

    public long dm_id { get; private set; }

    public void dm_assign_id(long id) => dm_id = id;

    public void dm_pre_new(DmArgs args)
    {
        overlays = Dm.list("emissive");
        log.Add("pre_new");
    }

    public virtual void New(params object[] __extra) => log.Add("New " + __extra.Length);
}

/// <summary>The test assembly's global procs (tg's <c>/proc/x</c> live in <c>GlobalProcs</c>, rules 7.8).</summary>
public static class DynamicFixtureGlobals
{
    public static object dynfx_global_sum(object a = null, object b = null) => Dm.num(a) + Dm.num(b);

    public static async DmTask<object> dynfx_global_nap(object ds = null)
    {
        await Dm.sleep(Dm.num(ds));
        return "global woke";
    }
}

/// <summary>Worlds for dynamic tests: runtime errors are recorded as "Type: message".</summary>
internal static class DynamicWorld
{
    public static World New(out List<string> errors)
    {
        var recorded = new List<string>();
        errors = recorded;
        return new World(7) { RuntimeErrorSink = e => recorded.Add(e.GetType().Name + ": " + e.Message) };
    }
}
