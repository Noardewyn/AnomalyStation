// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/cat_surgeon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6; the corpse he leaves since the corpse spawners, gaps E3, D327).

// tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:7
public partial class mob_living_basic_cat_butcherer : mob_living_basic
{
    /// The stuff we drop on death.
    // tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:35  var/static/list/drop_on_death (a type-level static, rules 10.6)
    public static ref DmList drop_on_death => ref TgState.Current.mob_living_basic_cat_butcherer__drop_on_death;

    // tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_dynamic_human_appearance(this, mob_spawn_path: typeof(obj_effect_mob_spawn_corpse_human_cat_butcher), l_hand: typeof(obj_item_circular_saw), bloody_slots: Dm.bitor(ITEM_SLOT_GLOVES, ITEM_SLOT_OCLOTHING));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), drop_on_death));
        RegisterSignal(this, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(after_attack));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:47
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }

    // tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:50
    [SignalHandler]
    public virtual void after_attack(mob_living_basic attacker = null, atom target = null)
    {
        if (!iscarbon(target) || !prob(35))
            return;

        mob_living_carbon_human attacked = target as mob_living_carbon_human;
        obj_item_organ_tail_cat tail = attacked?.get_organ_by_type(typeof(obj_item_organ_tail_cat)) as obj_item_organ_tail_cat;
        if (QDELETED(tail))
            return;

        visible_message(span_warning(Dm.text("[] severs []'s tail off in one swift swipe!", this, attacked)), span_warning(Dm.text("You sever []'s tail off.", attacked)));
        Waiting.Detach(tail.Remove(attacked));
        Waiting.NoSleep(tail.forceMove(drop_location() as atom));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/cat_surgeon.dm:35  var/static/list/drop_on_death
    internal DmList mob_living_basic_cat_butcherer__drop_on_death = Dm.list(
        typeof(obj_effect_mob_spawn_corpse_human_cat_butcher),
        typeof(obj_item_circular_saw));
}
