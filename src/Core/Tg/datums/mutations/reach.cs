// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/reach.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: telekinesis, the way to every attack_tk of _onclick/telekinesis.cs, and elastic arms (gaps P3, D327).

///Telekinesis lets you interact with objects from range, and gives you a light blue halo around your head.
// tg: code/datums/mutations/reach.dm:2
public partial class datum_mutation_telekinesis : datum_mutation
{
    // tg: code/datums/mutations/reach.dm:14
    public override async DmTask<object> on_acquiring(mob_living_carbon_human homan = null)
    {
        object dot = await base.on_acquiring(homan);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(homan, COMSIG_MOB_ATTACK_RANGED, nameof(on_ranged_attack));
        return dot;
    }

    // tg: code/datums/mutations/reach.dm:20
    public override async DmTask<object> on_losing(mob_living_carbon_human homan = null)
    {
        object dot = await base.on_losing(homan);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(homan, COMSIG_MOB_ATTACK_RANGED);
        return dot;
    }

    ///Triggers on COMSIG_MOB_ATTACK_RANGED. Usually handles stuff like picking up items at range.
    // tg: code/datums/mutations/reach.dm:27
    [SignalHandler]
    public virtual object on_ranged_attack(mob source = null, atom target = null, DmList modifiers = null)
    {
        // tg: :12  var/static/list/blacklisted_atoms = typecacheof(list(/atom/movable/screen)) (a type-level static, rules 10.6)
        TgState state = TgState.Current;
        state.datum_mutation_telekinesis__blacklisted_atoms ??= typecacheof(Dm.list(typeof(atom_movable_screen))) as DmList;
        if (Dm.truthy(is_type_in_typecache(target, state.datum_mutation_telekinesis__blacklisted_atoms)))
            return null;
        if (!Dm.truthy(tkMaxRangeCheck(source, target)) || source.z != target.z)
            return null;
        return Waiting.NoSleep(target.attack_tk(source));
    }
}

// tg: code/datums/mutations/reach.dm:35
public partial class datum_mutation_elastic_arms : datum_mutation
{
    // tg: code/datums/mutations/reach.dm:46
    public override async DmTask<object> on_acquiring(mob_living_carbon_human homan = null)
    {
        object dot = await base.on_acquiring(homan);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(homan, COMSIG_LIVING_TRY_PUT_IN_HAND, nameof(on_owner_equipping_item));
        RegisterSignal(homan, COMSIG_LIVING_TRY_PULL, nameof(on_owner_try_pull));
        homan.reach_length++;
        return dot;
    }

    // tg: code/datums/mutations/reach.dm:54
    public override async DmTask<object> on_losing(mob_living_carbon_human homan = null)
    {
        object dot = await base.on_losing(homan);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(homan, Dm.list(COMSIG_LIVING_TRY_PUT_IN_HAND, COMSIG_LIVING_TRY_PULL));
        homan.reach_length = Dm.min(1, homan.reach_length - 1);
        return dot;
    }

    /// signal sent when prompting if an item can be equipped
    // tg: code/datums/mutations/reach.dm:62
    [SignalHandler]
    public virtual object on_owner_equipping_item(mob_living_carbon_human owner = null, obj_item pick_item = null)
    {
        if ((pick_item.w_class > WEIGHT_CLASS_BULKY) && !Dm.truthy(Dm.bitand(pick_item.item_flags, ABSTRACT | HAND_ITEM))) // cant decide if i should limit to huge or bulky.
        {
            pick_item.balloon_alert(owner, I18n.T("arms too floppy to wield!"));
            return COMPONENT_LIVING_CANT_PUT_IN_HAND;
        }
        return null;
    }

    /// signal sent when owner tries to pull
    // tg: code/datums/mutations/reach.dm:69
    [SignalHandler]
    public virtual object on_owner_try_pull(mob_living_carbon owner = null, atom_movable target = null, object force = null)
    {
        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            if (Dm.num(living_target.mob_size) > MOB_SIZE_HUMAN)
            {
                living_target.balloon_alert(owner, I18n.T("arms too floppy to pull this!"));
                return COMSIG_LIVING_CANCEL_PULL;
            }
        }
        if (isitem(target))
        {
            obj_item item_target = target as obj_item;
            if (item_target.w_class > WEIGHT_CLASS_BULKY)
            {
                item_target.balloon_alert(owner, I18n.T("arms too floppy to pull this!"));
                return COMSIG_LIVING_CANCEL_PULL;
            }
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/mutations/reach.dm:12
    internal DmList datum_mutation_telekinesis__blacklisted_atoms;
}
