// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/jetpack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 6: the jetpack). The var block is generated (D111). One port proc of the locomotion bridge's
// preview, preview_stabilize, follows the translated procs (see its comment).

// Welcome to the jetpack component
// Apply this to something when you want it to be "like a jetpack"
// So propulsion through space on move, that sort of thing
// tg: code/datums/components/jetpack.dm:4
public partial class datum_component_jetpack : datum_component
{
    /**
     * Arguments:
     * * stabilize - If we should drift when we finish moving, or sit stable in space]
     * * drift_force - How much force is applied whenever the user tries to move, applied as a multiplier to the user's inertia_move_multiplier.
     * * activation_signal - Signal we activate on
     * * deactivation_signal - Signal we deactivate on
     * * return_flag - Flag to return if activation fails
     * * check_on_move - Callback we call each time we attempt a move, we expect it to retun true if the move is ok, false otherwise. It expects an arg, TRUE if fuel should be consumed, FALSE othewise
     * * effect_type - Type of trail_follow to spawn
     */
    // tg: code/datums/components/jetpack.dm:37
    [DmParams("stabilize", "drift_force", "activation_signal", "deactivation_signal", "return_flag", "check_on_move", "check_on_activation", "effect_type")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object stabilize = Dm.arg(__extra, 0) ?? FALSE;
        object drift_force = Dm.arg(__extra, 1) ?? 1 * NEWTONS;
        object activation_signal = Dm.arg(__extra, 2);
        object deactivation_signal = Dm.arg(__extra, 3);
        object return_flag = Dm.arg(__extra, 4);
        datum_callback check_on_move = Dm.arg(__extra, 5) as datum_callback;
        datum_callback check_on_activation = Dm.arg(__extra, 6) as datum_callback;
        Type effect_type = Dm.arg(__extra, 7) as Type;
        object dot = await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(activation_signal)) // Can't activate? go away
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, activation_signal, nameof(activate));
        if (Dm.truthy(deactivation_signal))
            RegisterSignal(parent, deactivation_signal, nameof(deactivate));

        this.stabilize = Dm.num(stabilize);
        this.check_on_move = check_on_move;
        this.check_on_activation = check_on_activation;
        this.activation_signal = activation_signal;
        this.deactivation_signal = deactivation_signal;
        this.return_flag = return_flag;
        this.effect_type = effect_type;
        this.drift_force = drift_force;
        return dot;
    }

    // tg: code/datums/components/jetpack.dm:66
    [DmParams("component", "original", "stabilize", "drift_force", "activation_signal", "deactivation_signal", "return_flag", "check_on_move", "check_on_activation", "effect_type")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object stabilize = Dm.arg(__extra, 0);
        object drift_force = Dm.arg(__extra, 1) ?? 1 * NEWTONS;
        object activation_signal = Dm.arg(__extra, 2);
        object deactivation_signal = Dm.arg(__extra, 3);
        object return_flag = Dm.arg(__extra, 4);
        datum_callback check_on_move = Dm.arg(__extra, 5) as datum_callback;
        datum_callback check_on_activation = Dm.arg(__extra, 6) as datum_callback;
        Type effect_type = Dm.arg(__extra, 7) as Type;
        UnregisterSignal(parent, this.activation_signal);
        if (Dm.truthy(this.deactivation_signal))
            UnregisterSignal(parent, this.deactivation_signal);
        RegisterSignal(parent, activation_signal, nameof(activate));
        if (Dm.truthy(deactivation_signal))
            RegisterSignal(parent, deactivation_signal, nameof(deactivate));

        this.stabilize = Dm.num(stabilize);
        this.check_on_move = check_on_move;
        this.check_on_activation = check_on_activation;
        this.activation_signal = activation_signal;
        this.deactivation_signal = deactivation_signal;
        this.return_flag = return_flag;
        this.effect_type = effect_type;
        this.drift_force = drift_force;

        if (Dm.truthy(trail) && trail.effect_type != effect_type)
            setup_trail(trail.holder as mob);
    }

    // tg: code/datums/components/jetpack.dm:86
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref trail);
        check_on_move = null;
        check_on_activation = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/jetpack.dm:92
    public virtual void setup_trail(mob user = null)
    {
        QDEL_NULL(ref trail);
        trail = Dm.@new(effect_type, user) as datum_effect_system_trail_follow;
        trail.auto_process = FALSE;
        Waiting.NoSleep(trail.start());
    }

    // tg: code/datums/components/jetpack.dm:98
    [SignalHandler]
    public virtual object activate(datum source = null, mob new_user = null)
    {
        if (check_on_activation != null && !Dm.truthy(Waiting.NoSleep(check_on_activation.Invoke())))
            return return_flag;

        active = TRUE;
        RegisterSignal(new_user, COMSIG_MOVABLE_MOVED, nameof(move_react));
        RegisterSignal(new_user, COMSIG_MOVABLE_PRE_MOVE, nameof(pre_move_react));
        RegisterSignal(new_user, COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE, nameof(on_pushoff));
        RegisterSignal(new_user, COMSIG_MOVABLE_DRIFT_BLOCK_INPUT, nameof(on_input_block));
        RegisterSignal(new_user, COMSIG_MOVABLE_SPACEMOVE, nameof(stabilize__proc));
        if (Dm.truthy(effect_type))
            setup_trail(new_user);
        new_user.inertia_move_multiplier_active /= Dm.num(drift_force); // lower multiplier = faster drifting
        return null;
    }

    // tg: code/datums/components/jetpack.dm:114
    [SignalHandler]
    public virtual void deactivate(datum source = null, mob old_user = null)
    {
        if (!Dm.truthy(active))
            return;

        active = FALSE;
        UnregisterSignal(old_user, Dm.list(
            COMSIG_MOVABLE_PRE_MOVE,
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE,
            COMSIG_MOVABLE_DRIFT_BLOCK_INPUT,
            COMSIG_MOVABLE_SPACEMOVE));
        QDEL_NULL(ref trail);
        old_user.inertia_move_multiplier_active *= Dm.num(drift_force);
    }

    // tg: code/datums/components/jetpack.dm:131
    [SignalHandler]
    public virtual void move_react(mob source = null)
    {
        if (!Dm.truthy(should_trigger(source)))
            return;

        if (Dm.truthy(source.client.intended_direction) && Dm.truthy(Waiting.NoSleep(check_on_move.Invoke(TRUE)))) //You use jet when press keys. yes.
            trail?.generate_effect();
    }

    /// Handles all active 0g movement, including both manual (trying to move a direction in 0g) and automatic (drifting idly in 0g)
    // tg: code/datums/components/jetpack.dm:140  (the proc stabilize, renamed: the var stabilize, rules 2.4)
    [SignalHandler]
    public virtual object stabilize__proc(mob source = null, object movement_dir = null, object continuous_move = null, object backup = null)
    {
        /*
         * Checks if we should stop any active movement
         *
         * Obviously we stop all forms of drifting if we have stabilizers active (allowing free space movement)
         * Less obviously, we stop need to stop drift if we are trying to move *while passively drifting*
         * (Without checking the latter, jetpacks will act very weird and jank. As you move in one direction,
         * you will simultaneously drift in that direction, causing you to jump/skip a tile every so often.)
         *
         * Either way, we need to check that we have the "fuel" to stop this
         * DO NOT CONSUME FUEL HERE, just check if we have it
         * This is done because things other then us can use our fuel
         */
        if ((Dm.truthy(stabilize) || (!Dm.truthy(continuous_move) && Dm.truthy(movement_dir))) && Dm.truthy(Waiting.NoSleep(check_on_move.Invoke(FALSE))))
            return COMSIG_MOVABLE_STOP_SPACEMOVE;
        return NONE;
    }

    // tg: code/datums/components/jetpack.dm:158
    public virtual object should_trigger(mob source = null)
    {
        if (!Dm.truthy(source) || !Dm.truthy(source.client)) //Don't allow jet self using
            return FALSE;
        if (!isturf(source.loc)) //You can't use jet in nowhere or from mecha/closet
            return FALSE;
        if (!Dm.truthy(Dm.bitand(source.movement_type, FLOATING)) || Dm.truthy(source.buckled)) //You don't want use jet in gravity or while buckled.
            return FALSE;
        if (Dm.truthy(source.pulledby)) //You don't must use jet if someone pull you
            return FALSE;
        if (Dm.truthy(source.throwing)) //You don't must use jet if you thrown
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/components/jetpack.dm:171
    [SignalHandler]
    public virtual void pre_move_react(mob source = null)
    {
        if (trail != null)
            trail.oldposition = get_turf(source);
    }

    // tg: code/datums/components/jetpack.dm:175
    [SignalHandler]
    public virtual object on_input_block(mob source = null)
    {
        if (!Dm.truthy(should_trigger(source)))
            return NONE;

        if (!Dm.truthy(Waiting.NoSleep(check_on_move.Invoke(TRUE))))
            return NONE;

        return DRIFT_ALLOW_INPUT;
    }

    // tg: code/datums/components/jetpack.dm:186
    [SignalHandler]
    public virtual object on_pushoff(mob source = null, object movement_dir = null, object continuous_move = null, atom backup = null)
    {
        if (Dm.eq(get_dir(source, backup), movement_dir) || source.loc == backup.loc)
            return NONE;

        if (!Dm.truthy(source.client?.intended_direction) || Dm.eq(source.client.intended_direction, get_dir(source, backup)))
            return NONE;

        if (source.drift_handler == null)
            return NONE;

        if (!Dm.truthy(should_trigger(source)) || !Dm.truthy(Waiting.NoSleep(check_on_move.Invoke(FALSE))))
            return NONE;

        return COMPONENT_PREVENT_SPACEMOVE_HALT;
    }

    /**
     * The port's own (the locomotion bridge's preview, LocomotionSeam.PreviewSpacemove): stabilize's answer to a player's
     * own step (a movement direction, not continuous) without its side effect. tg's check_on_move.Invoke(FALSE) is the
     * jetpack's allow_thrust with use_fuel FALSE, which still turns the jetpack off when the gas has run out; the preview
     * asks obj_item_tank_jetpack.preview_thrust instead, which only reads; the implantable thrusters' own preview
     * (augments_chest.cs) and the MODsuit ion jetpack module's (modules_general.cs, gaps MO1) likewise. Another check_on_move
     * (the wings: not on Meta's path) answers no. With <paramref name="continuous"/> it answers a drift's continuous move
     * (newtonian_move's), which only the stabilizers stop.
     */
    internal bool preview_stabilize(bool continuous = false)
    {
        if (!Dm.truthy(active) || check_on_move == null || (continuous && !Dm.truthy(stabilize)))
            return false;
        if (check_on_move.@object is obj_item_tank_jetpack jetpack && Dm.eq(check_on_move.@delegate, nameof(obj_item_tank_jetpack.allow_thrust)))
            return jetpack.preview_thrust(check_on_move.arguments?[1]);
        // gaps package P6: the implantable thrusters' allow_thrust, read through its own preview
        if (check_on_move.@object is obj_item_organ_cyberimp_chest_thrusters thrusters && Dm.eq(check_on_move.@delegate, nameof(obj_item_organ_cyberimp_chest_thrusters.allow_thrust)))
            return thrusters.preview_thrust(check_on_move.arguments?[1]);
        // gaps MO1: the MODsuit ion jetpack module's allow_thrust, read through its own preview (check_power only)
        if (check_on_move.@object is obj_item_mod_module_jetpack modjet && Dm.eq(check_on_move.@delegate, nameof(obj_item_mod_module_jetpack.allow_thrust)))
            return modjet.preview_thrust();
        return false;
    }
}
