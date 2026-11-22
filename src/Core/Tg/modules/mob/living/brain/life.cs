// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing: a gibbed player's brain mob): the whole file.

// tg: code/modules/mob/living/brain/life.dm:2
public partial class mob_living_brain : mob_living
{
    // tg: code/modules/mob/living/brain/life.dm:2
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (loc == null || HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;

        if (container != null)
        {
            if (!istype(container, typeof(obj_item_brain_processor)))
            {
                stack_trace("/mob/living/brain with container set, but container was not an MMI!", "code/modules/mob/living/brain/life.dm", 8);
                container = null;
            }
            if (container != null && !Dm.truthy(container.contains(this)))
            {
                stack_trace("/mob/living/brain with container set, but we weren't inside of it!", "code/modules/mob/living/brain/life.dm", 11);
                container = null;
            }
        }
        object dot = base.Life(seconds_per_tick, __extra);
        handle_emp_damage(Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/mob/living/brain/life.dm:16
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (health > dead_threshold)
            return;
        if (stat != DEAD)
            Waiting.NoSleep(death());
        obj_item_organ_brain BR;
        if (loc is obj_item_organ_brain in_brain)
            BR = in_brain;
        else
            BR = (astype(container, typeof(obj_item_brain_processor_organic)) as obj_item_brain_processor_organic)?.brain;
        if (BR != null)
            Waiting.NoSleep(BR.set_organ_damage(BRAIN_DAMAGE_DEATH)); //beaten to a pulp
    }

    // tg: code/modules/mob/living/brain/life.dm:31
    public virtual void handle_emp_damage(float seconds_per_tick)
    {
        if (!Dm.truthy(emp_damage))
            return;

        if (stat == DEAD)
            emp_damage = 0;
        else
            emp_damage = Dm.num(Dm.max(emp_damage - (0.5f * seconds_per_tick), 0));
    }
}
