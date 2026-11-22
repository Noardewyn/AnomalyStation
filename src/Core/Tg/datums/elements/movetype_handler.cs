// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/movetype_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


/**
 * An element that enables and disables movetype bitflags whenever the relative traits are added or removed.
 * It also handles the +2/-2 pixel y anim loop typical of mobs possessing the FLYING or FLOATING movetypes.
 * This element is necessary for the TRAIT_MOVE_ traits to work correctly, so make sure to attach this element
 * before adding them to non-living movables.
 */
// tg: code/datums/elements/movetype_handler.dm:7
[DmPath("/datum/element/movetype_handler")]
public partial class datum_element_movetype_handler : datum_element
{
    // tg: code/datums/elements/movetype_handler.dm:8  the var override of the type block (rules 10.2)
    public datum_element_movetype_handler()
    {
        element_flags = ELEMENT_DETACH_ON_HOST_DESTROY;
    }

    // tg: code/datums/elements/movetype_handler.dm:10  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attached_atoms = Dm.list();
    }

    // tg: code/datums/elements/movetype_handler.dm:10 (list(): dm_init_vars above)
    public DmList attached_atoms;
}

// tg: code/datums/elements/movetype_handler.dm:12
public partial class datum_element_movetype_handler : datum_element
{
    // tg: code/datums/elements/movetype_handler.dm:12
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        if (Dm.truthy(attached_atoms[target])) //Already attached.
            return dot;

        atom_movable movable_target = target as atom_movable;
        RegisterSignals(movable_target, GLOB.movement_type_addtrait_signals, nameof(on_movement_type_trait_gain));
        RegisterSignals(movable_target, GLOB.movement_type_removetrait_signals, nameof(on_movement_type_trait_loss));
        RegisterSignal(movable_target, SIGNAL_ADDTRAIT(TRAIT_NO_FLOATING_ANIM), nameof(on_no_floating_anim_trait_gain));
        RegisterSignal(movable_target, SIGNAL_REMOVETRAIT(TRAIT_NO_FLOATING_ANIM), nameof(on_no_floating_anim_trait_loss));
        attached_atoms[movable_target] = TRUE;

        if (Dm.truthy(Dm.bitand(movable_target.movement_type, Dm.bitor(FLOATING, FLYING))) && !HAS_TRAIT(movable_target, TRAIT_NO_FLOATING_ANIM))
            DO_FLOATING_ANIM(movable_target);
        return dot;
    }

    // tg: code/datums/elements/movetype_handler.dm:32
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        DmList signals_to_remove = Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_NO_FLOATING_ANIM),
            SIGNAL_REMOVETRAIT(TRAIT_NO_FLOATING_ANIM)
        );
        Dm.append(ref signals_to_remove, GLOB.movement_type_addtrait_signals);
        Dm.append(ref signals_to_remove, GLOB.movement_type_removetrait_signals);
        UnregisterSignal(source, signals_to_remove);

        Dm.remove(ref attached_atoms, source);
        STOP_FLOATING_ANIM(source);
        return base.Detach(source, __extra);
    }

    /// Called when a movement type trait is added to the movable. Enables the relative bitflag.
    // tg: code/datums/elements/movetype_handler.dm:43
    [SignalHandler]
    public virtual void on_movement_type_trait_gain(atom_movable source = null, object trait = null)
    {
        object flag = GLOB.movement_type_trait_to_flag[trait];
        if (Dm.truthy(Dm.bitand(source.movement_type, flag)))
            return;
        object old_state = source.movement_type;
        Dm.combine(ref source.movement_type, flag);
        if (!Dm.truthy(Dm.bitand(old_state, Dm.bitor(FLOATING, FLYING))) && Dm.truthy(Dm.bitand(source.movement_type, Dm.bitor(FLOATING, FLYING))) && !HAS_TRAIT(source, TRAIT_NO_FLOATING_ANIM))
            DO_FLOATING_ANIM(source);
        if (Dm.truthy(Dm.bitand(source.movement_type, UPSIDE_DOWN)))
            ADD_TRAIT(source, TRAIT_IGNORE_ELEVATION, SOURCE_MOVETYPES);
        if (Dm.truthy(source._listen_lookup?[COMSIG_MOVETYPE_FLAG_ENABLED]))
            source._SendSignal(COMSIG_MOVETYPE_FLAG_ENABLED, Dm.list(source, flag, old_state));
    }

    /// Called when a movement type trait is removed from the movable. Disables the relative bitflag if it wasn't there in the compile-time bitfield.
    // tg: code/datums/elements/movetype_handler.dm:57
    [SignalHandler]
    public virtual void on_movement_type_trait_loss(atom_movable source = null, object trait = null)
    {
        object flag = GLOB.movement_type_trait_to_flag[trait];
        if (Dm.truthy(Dm.bitand(Dm.initial(source, (atom_movable p) => p.movement_type), flag)))
            return;
        object old_state = source.movement_type;
        Dm.mask(ref source.movement_type, Dm.bitnot(flag));
        if ((Dm.truthy(Dm.bitand(old_state, Dm.bitor(FLOATING, FLYING)))) && !Dm.truthy(Dm.bitand(source.movement_type, Dm.bitor(FLOATING, FLYING))))
        {
            STOP_FLOATING_ANIM(source);
            turf pitfall = source.loc as turf; //Things that don't fly fall in open space.
            if (pitfall is turf)
                Waiting.NoSleep(pitfall.zFall(source));
        }
        if ((Dm.truthy(Dm.bitand(old_state, UPSIDE_DOWN))) && !Dm.truthy(Dm.bitand(source.movement_type, UPSIDE_DOWN)))
            REMOVE_TRAIT(source, TRAIT_IGNORE_ELEVATION, SOURCE_MOVETYPES);
        if (Dm.truthy(source._listen_lookup?[COMSIG_MOVETYPE_FLAG_DISABLED]))
            source._SendSignal(COMSIG_MOVETYPE_FLAG_DISABLED, Dm.list(source, flag, old_state));
    }

    /// Called when the TRAIT_NO_FLOATING_ANIM trait is added to the movable. Stops it from bobbing up and down.
    // tg: code/datums/elements/movetype_handler.dm:74
    [SignalHandler]
    public virtual void on_no_floating_anim_trait_gain(atom_movable source = null, object trait = null)
    {
        if (Dm.truthy(Dm.bitand(source.movement_type, Dm.bitor(FLOATING, FLYING))))
            STOP_FLOATING_ANIM(source);
    }

    /// Called when the TRAIT_NO_FLOATING_ANIM trait is removed from the mob. Restarts the bobbing animation.
    // tg: code/datums/elements/movetype_handler.dm:80
    [SignalHandler]
    public virtual void on_no_floating_anim_trait_loss(atom_movable source = null, object trait = null)
    {
        if (Dm.truthy(Dm.bitand(source.movement_type, Dm.bitor(FLOATING, FLYING))))
            DO_FLOATING_ANIM(source);
    }
}
