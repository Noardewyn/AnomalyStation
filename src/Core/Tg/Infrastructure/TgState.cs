using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

/// <summary>
/// All DM global and static state of one world (docs/ai/translation-rules.md 10.6; docs/ai/specs/object-model.md
/// section 10): <c>GLOBAL_REAL</c> vars such as <c>GLOB</c> and <c>Master</c>, every <c>SUBSYSTEM_DEF</c> (<c>SSx</c>),
/// and the <c>var/static</c> / <c>var/global</c> vars of types and procs as fields named <c>a_b__x</c> and
/// <c>a_b__p__x</c>. One instance per <see cref="World"/>, created on first access through the Kernel extension slot;
/// no translated class has a mutable C# static field. Translated code reaches the fields through static ref
/// accessors that keep the DM spelling (<c>GlobalProcs.GLOB</c>, <c>SStimer</c>, a type's <c>foo</c>).
/// <para>
/// A partial class: this part holds the infrastructure, and each translated file adds the fields of the globals
/// and statics it defines in its own part. Field initializers hold constant initializers and those that create no
/// registered datum; initializers that create registered datums are assignments in <see cref="InitStatics"/>.
/// </para>
/// </summary>
public sealed partial class TgState
{
    private TgState(World world)
    {
        _world = world;
        // object-model.md section 8: TgState is hashed as one more object, its fields by name. The contributor adds
        // nothing while every field is at its default, so creating TgState never changes the world's hash.
        world.AddStateHasher(nameof(TgState), static (World w, ref StateHasher h) => Of(w).HashState(ref h));
    }

    // The fields of this world's TgState that are not at their default, by name (DmFieldHasher.HashFields).
    private void HashState(ref StateHasher hasher) => DmFieldHasher.HashFields(this, ref hasher);

    /// <summary>The state of the ambient world (<see cref="World.Current"/>), created on first use.</summary>
    public static TgState Current => World.Current.GetExtension(static w => new TgState(w));

    /// <summary>The state of <paramref name="world"/>, created on first use (hosts and tests).</summary>
    public static TgState Of(World world) => world.GetExtension(static w => new TgState(w));

    /// <summary>
    /// tg <c>GLOBAL_REAL(GLOB, /datum/controller/global_vars)</c> (code/controllers/globals.dm): the DM var itself,
    /// null until <c>/datum/controller/global_vars/New</c> sets <c>GLOB = src</c>. Translated code reads and writes it
    /// as <c>GlobalProcs.GLOB__var</c> and reaches the <c>GLOBAL_*</c> vars through <c>GlobalProcs.GLOB</c> (rules 10.6).
    /// </summary>
    public datum_controller_global_vars GLOB;

    /// <summary>
    /// The storage of the <c>GLOBAL_*</c> vars before the GLOB datum exists (rules 10.6): they are <c>var/global</c>
    /// in DM, so tg writes and reads them through a null <c>GLOB</c> (<c>/world/Genesis</c> sets
    /// <c>GLOB.config_error_log</c>, game/world.dm:91; <c>Master/New</c> reads <c>GLOB.sortInstance</c>,
    /// controllers/master.dm:110, before it creates GLOB at :128). An object of the GLOB type made without its
    /// constructor: not a datum of the world (no id, not registered), every var null or 0. Assigning
    /// <c>GLOB__var</c> moves its values into the new GLOB and drops it.
    /// </summary>
    [NotHashed]
    private datum_controller_global_vars _globStorage;

    /// <summary>The holder of the <c>GLOBAL_*</c> vars: GLOB once it exists, the pre-GLOB storage before.</summary>
    internal datum_controller_global_vars GlobVars => GLOB ?? (_globStorage ??=
        (datum_controller_global_vars)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(datum_controller_global_vars)));

    /// <summary>
    /// <c>GLOB = value</c>: a non-null value takes every var the pre-GLOB storage holds that is not null or 0, as
    /// DM's <c>var/global</c> storage keeps them across the creation of the datum; the storage is dropped.
    /// </summary>
    internal void SetGlob(datum_controller_global_vars value)
    {
        GLOB = value;
        if (value is null || _globStorage is null) return;
        // Every var declared on the GLOB type: the GLOBAL_* vars and its own instance vars.
        foreach (System.Reflection.FieldInfo f in typeof(datum_controller_global_vars).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
        {
            object v = f.GetValue(_globStorage);
            if (v is null || (f.FieldType.IsValueType && v.Equals(Activator.CreateInstance(f.FieldType)))) continue;
            f.SetValue(value, v);
        }
        _globStorage = null;
    }

    /// <summary>tg <c>GLOBAL_REAL(Master, /datum/controller/master)</c> (code/controllers/master.dm): the Master Controller.</summary>
    public datum_controller_master Master;

    // The world this state belongs to (InitStatics runs in it). Not world state: not hashed.
    [NotHashed]
    private readonly World _world;

    [NotHashed]
    private bool _staticsInitialized;

    /// <summary>
    /// Runs the static initializers that create registered datums (<c>var/static/datum/x = new ...</c>): first tg's genesis
    /// call, <c>var/static/_ = world.Genesis()</c> (<c>code/genesis_call.dme</c>), which creates the logger, the Master, the
    /// subsystems, the configuration and <c>GLOB</c>; then the others in DM compile order (<c>ProcOrder.g.cs</c> order,
    /// object-model.md section 10), in this state's world (entered for the call). Called once by the world bootstrap
    /// (<see cref="TgWorld.Boot"/>) before anything else runs; BYOND initializes global and static vars at world start, before
    /// <c>world/New</c>. A second call throws.
    /// </summary>
    public void InitStatics()
    {
        if (_staticsInitialized) throw new InvalidOperationException("TgState.InitStatics runs once per world.");
        _staticsInitialized = true;
        using World.Scope scope = _world.Enter();
        // tg: code/genesis_call.dme:52  /world/proc/_() var/static/_ = world.Genesis(): BYOND runs this static initializer
        // before every other one (tg's init order, code/game/world.dm:15-26; "THE VERY FIRST LINE OF DM CODE TO EXECUTE").
        // Genesis creates the logger and the Master, whose New creates the configuration, every subsystem and GLOB. It is
        // called from a synchronous context, so a sleep inside it is reported (DV5); none is expected at world start.
        // Genesis returns nothing: the static holds null, so no field keeps it.
        Waiting.NoSleep(Dm.world.Genesis());
        // Then one assignment per other static initializer that creates a registered datum, in DM compile order (tg: in
        // reverse declaration order, code/game/world.dm:26).
        // tg: code/game/turfs/open/space/space.dm:58  /turf/open/space/var/static/datum/gas_mixture/immutable/space/space_gas = new
        turf_open_space__space_gas = Dm.@new<datum_gas_mixture_immutable_space>();
        // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:176  /obj/item/organ/lungs/on_mob_remove()
        // var/static/datum/gas_mixture/immutable/dummy = new(BREATH_VOLUME)
        obj_item_organ_lungs__on_mob_remove__dummy = Dm.@new<datum_gas_mixture_immutable>(Defines.BREATH_VOLUME);
        // tg: code/modules/surgery/organs/internal/lungs/_lungs.dm:648  /obj/item/organ/lungs/check_breath()
        // var/static/datum/gas_mixture/immutable/empty_breath = new(BREATH_VOLUME)
        obj_item_organ_lungs__check_breath__empty_breath = Dm.@new<datum_gas_mixture_immutable>(Defines.BREATH_VOLUME);
    }
}
