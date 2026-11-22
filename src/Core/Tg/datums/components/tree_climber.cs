// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tree_climber.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/*
 * A component given to mobs that can climb trees
 */
// tg: code/datums/components/tree_climber.dm:6
public partial class datum_component_tree_climber : datum_component
{
    // tg: code/datums/components/tree_climber.dm:10
    [DmParams("climbing_distance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object climbing_distance = Dm.arg(__extra, 0) ?? 20f;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.climbing_distance = Dm.num(climbing_distance);

        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return null;
    }

    // tg: code/datums/components/tree_climber.dm:17
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(climb_tree));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/tree_climber.dm:21
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_ATOM_EXAMINE));
    }

    // tg: code/datums/components/tree_climber.dm:24
    public override object Destroy(float? force = null)
    {
        if (current_tree != null)
            handle_climb_tree(parent as mob_living, current_tree); //remove mob from tree and handle deletion of signals
        return base.Destroy(force);
    }

    // tg: code/datums/components/tree_climber.dm:29
    [SignalHandler]
    public virtual object climb_tree(mob_living source = null, atom target = null)
    {
        if (target is not obj_structure_flora_tree)
            return null;

        if (!Dm.truthy(can_climb_tree(target as obj_structure_flora_tree))) //check if another animal is on the tree
        {
            to_chat(source, span_warning(Dm.text("[] is blocked!", target)));
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        handle_climb_tree(source, target as obj_structure);

        if (current_tree != null)
        {
            Waiting.NoSleep(source.forceMove(get_turf(current_tree)));
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        DmList possible_drops = get_adjacent_open_turfs(target) as DmList;
        foreach (turf droploc in Dm.each<turf>(possible_drops.Copy()))
        {
            if (!Dm.truthy(Waiting.NoSleep(droploc.is_blocked_turf(exclude_mobs: TRUE))))
                continue;
            Dm.remove(ref possible_drops, droploc);
        }
        if (Dm.truthy(length(possible_drops)))
            Waiting.NoSleep(source.forceMove(pick(possible_drops) as atom));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/components/tree_climber.dm:54
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (current_tree != null)
            Dm.append(ref examine_text, Dm.text("It is clinging to []!", current_tree));
    }

    // tg: code/datums/components/tree_climber.dm:59
    public virtual object can_climb_tree(obj_structure_flora_tree target = null)
    {
        if (current_tree != null)
            return TRUE;
        turf tree_turf = get_turf(target) as turf;
        if (Dm.locate<mob_living>(tree_turf.contents) != null)
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/components/tree_climber.dm:67
    public virtual void handle_climb_tree(mob_living climber = null, obj target_tree = null)
    {
        float offset = current_tree != null ? -climbing_distance : climbing_distance;
        Dm.animate(climber, Dm.named("pixel_y", Dm.num(climber.pixel_y) + offset), Dm.named("time", 2));
        Waiting.NoSleep(climber.Stun(2 * SECONDS, ignore_canstun: TRUE));
        if (current_tree != null)
        {
            remove_tree_signals();
            current_tree = null;
            return;
        }

        current_tree = target_tree;
        register_tree_signals();
    }

    // tg: code/datums/components/tree_climber.dm:79
    [SignalHandler]
    public virtual void remove_from_tree()
    {
        handle_climb_tree(parent as mob_living, current_tree);
    }

    // tg: code/datums/components/tree_climber.dm:84
    public virtual void register_tree_signals()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(remove_from_tree));
        RegisterSignal(current_tree, COMSIG_PREQDELETED, nameof(remove_from_tree));
    }

    // tg: code/datums/components/tree_climber.dm:88
    public virtual void remove_tree_signals()
    {
        UnregisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE);
        UnregisterSignal(current_tree, COMSIG_PREQDELETED);
    }
}
