// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/holographic_nature.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164, the holosigns): the whole file. The var block is generated but for the static loc_connections (TgState, rules 10.6).

/*
 * A component given to holographic objects to make them glitch out when passed through
 */
// tg: code/datums/components/holographic_nature.dm:1  #define GLITCH_DURATION 0.45 SECONDS, :2  #define GLITCH_REMOVAL_DURATION 0.25 SECONDS (file-local)
// tg: code/datums/components/holographic_nature.dm:6
public partial class datum_component_holographic_nature : datum_component
{
    private const float GLITCH_DURATION = 0.45f * SECONDS;
    private const float GLITCH_REMOVAL_DURATION = 0.25f * SECONDS;

    ///list of signals we apply to our turf
    // tg: code/datums/components/holographic_nature.dm:10  var/static/list/loc_connections (a type-level static, rules 10.6)
    private static ref DmList loc_connections => ref TgState.Current.datum_component_holographic_nature__loc_connections;

    // tg: code/datums/components/holographic_nature.dm:14
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/holographic_nature.dm:18
    public override async DmTask RegisterWithParent()
    {
        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, loc_connections);
        if (isliving(parent))
        {
            RegisterSignal(parent, COMSIG_LIVING_MOB_BUMP, nameof(on_bumped));
            RegisterSignal(parent, COMSIG_LIVING_MOB_BUMPED, nameof(on_bumped));
            RegisterSignal(parent, COMSIG_MOB_APPLY_DAMAGE, nameof(on_mob_damaged));
            return;
        }

        atom atom_parent = (atom)parent;
        if (isobj(atom_parent) && Dm.truthy(atom_parent.uses_integrity))
            RegisterSignal(parent, COMSIG_ATOM_TAKE_DAMAGE, nameof(on_object_damaged));
    }

    // tg: code/datums/components/holographic_nature.dm:30
    [SignalHandler]
    public virtual void on_mob_damaged(mob_living source = null, object damage_amount = null, object damagetype = null, object def_zone = null, object blocked = null, object wound_bonus = null, object exposed_wound_bonus = null, object sharpness = null, object attack_direction = null, object attacking_item = null)
    {
        if (Dm.eq(damagetype, BURN) || Dm.eq(damagetype, BRUTE))
            apply_effects();
    }

    // tg: code/datums/components/holographic_nature.dm:35
    [SignalHandler]
    public virtual void on_object_damaged(obj source = null, object damage = null, object damage_type = null, params object[] __extra)
    {
        if (Dm.eq(damage_type, BURN) || Dm.eq(damage_type, BRUTE))
            apply_effects();
    }

    // tg: code/datums/components/holographic_nature.dm:40
    [SignalHandler]
    public virtual void on_entered(atom_movable source = null, atom_movable thing = null, params object[] __extra)
    {
        atom_movable movable_parent = (atom_movable)parent;
        if (!isturf(movable_parent.loc))
            return;
        if (isprojectile(thing) || Dm.truthy(thing.density))
            apply_effects();
    }

    // tg: code/datums/components/holographic_nature.dm:46
    [SignalHandler]
    public virtual void on_bumped(atom_movable source = null, mob_living crossing_mob = null)
    {
        apply_effects();
    }

    // tg: code/datums/components/holographic_nature.dm:50
    public virtual void apply_effects()
    {
        if (!(glitch_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;
        glitch_cooldown = world.time + (GLITCH_DURATION + GLITCH_REMOVAL_DURATION); // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        apply_wibbly_filters(parent as atom);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.remove_wibbly_filters), parent, GLITCH_REMOVAL_DURATION), GLITCH_DURATION, file: "code/datums/components/holographic_nature.dm", line: 54);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/holographic_nature.dm:10  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList datum_component_holographic_nature__loc_connections = Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_holographic_nature.on_entered)));
}
