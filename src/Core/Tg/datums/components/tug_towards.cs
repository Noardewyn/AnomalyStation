// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tug_towards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (a patient on an IV drip leans toward its stand). The var block is generated.

/// Component that, when applied, will tug the parent towards the given target
// tg: code/datums/components/tug_towards.dm:4
public partial class datum_component_tug_towards : datum_component
{
    // tg: code/datums/components/tug_towards.dm:16
    [DmParams("tugging_to", "strength")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom tugging_to = Dm.arg(__extra, 0) as atom;
        object strength = Dm.arg(__extra, 1);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        if (!(tugging_to is atom))
            throw Dm.CRASH(assertion_message("code/datums/components/tug_towards.dm", 30, "istype(tugging_to)"), "code/datums/components/tug_towards.dm", 30);

        add_tugging_to_target(tugging_to, strength);

        RegisterSignals(parent, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_MOB_BUCKLED,
            COMSIG_MOB_UNBUCKLED
        ), nameof(update_tug));
        return null;
    }

    // tg: code/datums/components/tug_towards.dm:40
    public override object Destroy(float? force = null)
    {
        tugging_to_targets.Cut();

        if (isliving(parent))
        {
            mob_living living_parent = (mob_living)parent;
            living_parent.remove_offsets(REF(this));
        }
        else
        {
            animate(
                parent,
                Dm.named("pixel_x", -current_tug_offset_x),
                Dm.named("pixel_y", -current_tug_offset_y),
                Dm.named("time", 0.2f * SECONDS),
                Dm.named("flags", ANIMATION_RELATIVE)
            );
        }

        return base.Destroy(force);
    }

    // tg: code/datums/components/tug_towards.dm:58
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        atom tugging_to = Dm.arg(__extra, 0) as atom;
        object strength = Dm.arg(__extra, 1);
        add_tugging_to_target(tugging_to, strength);
    }

    /// Removes the tug target, and deletes the component if there are no more targets
    // tg: code/datums/components/tug_towards.dm:67
    public virtual void remove_tug_target(atom target = null)
    {
        tugging_to_targets.Remove(target);

        if (tugging_to_targets.len == 0)
            qdel(this);
        else
            update_tug();
    }

    // tg: code/datums/components/tug_towards.dm:75
    private void add_tugging_to_target(atom tugging_to = null, object strength = null)
    {
        strength ??= 0.8f;
        tugging_to_targets[tugging_to] = strength;

        RegisterSignal(tugging_to, COMSIG_PREQDELETED, nameof(on_tugging_to_qdeleting));
        RegisterSignal(tugging_to, COMSIG_MOVABLE_MOVED, nameof(update_tug));

        update_tug();
    }

    // tg: code/datums/components/tug_towards.dm:87
    [SignalHandler]
    public virtual void on_tugging_to_qdeleting(datum target = null)
    {
        tugging_to_targets.Remove(target);

        if (tugging_to_targets.len == 0)
            qdel(this);
        else
            update_tug();
    }

    // tg: code/datums/components/tug_towards.dm:97
    [SignalHandler]
    public virtual void update_tug(params object[] __extra)
    {
        if (QDELETED(this))
            return; // another movement call could have deleted us

        atom atom_parent = (atom)parent;
        mob mob_parent = parent as mob;

        float total_tug_x = 0;
        float total_tug_y = 0;

        if (mob_parent == null || mob_parent.buckled == null)
        {
            float tuggers = 0;

            foreach (atom target in Dm.each_cast<atom>(tugging_to_targets))
            {
                if (target.z != atom_parent.z)
                    continue;

                tuggers += 1;

                float strength = Dm.num(tugging_to_targets[target]);
                total_tug_x += sign(target.x - atom_parent.x) * strength;
                total_tug_y += sign(target.y - atom_parent.y) * strength;
            }

            if (tuggers > 0)
            {
                total_tug_x /= tuggers;
                total_tug_y /= tuggers;

                // GAP: world.icon_size is ICON_SIZE_ALL (tg's world definition); the shim's world does not declare it.
                float half_size = ICON_SIZE_ALL * 0.5f;
                total_tug_x *= half_size;
                total_tug_y *= half_size;
            }
        }

        if (total_tug_x == current_tug_offset_x && total_tug_y == current_tug_offset_y)
            return;

        if (isliving(mob_parent))
        {
            mob_living living_parent = (mob_living)mob_parent;
            living_parent.add_offsets(REF(this), x_add: total_tug_x, y_add: total_tug_y);
        }
        else
        {
            animate(
                atom_parent,
                Dm.named("pixel_x", -current_tug_offset_x + total_tug_x),
                Dm.named("pixel_y", -current_tug_offset_y + total_tug_y),
                Dm.named("time", 0.2f * SECONDS),
                Dm.named("flags", ANIMATION_RELATIVE)
            );
        }

        current_tug_offset_x = total_tug_x;
        current_tug_offset_y = total_tug_y;
    }
}
