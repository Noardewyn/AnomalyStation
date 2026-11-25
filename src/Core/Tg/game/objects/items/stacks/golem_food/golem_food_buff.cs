// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/golem_food/golem_food_buff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the buffs a golem gets from the minerals it eats. The gibtonite and bluespace buffs hand the golem a launcher
// item (gibtonite_hand, bluespace_finger) that is not translated: those two apply_effects are live-partial rows. The var blocks are generated.

/// An effect you gain from eating minerals
// tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:4
public partial class datum_golem_food_buff : datum
{
    /// Returns true if the passed mob can currently gain this buff
    // tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:13
    public virtual object can_consume(mob_living consumer = null)
    {
        if (!Dm.truthy(exclusive))
            return TRUE;
        datum_status_effect_golem existing = consumer.has_status_effect(typeof(datum_status_effect_golem)) as datum_status_effect_golem;
        return existing == null || !Dm.truthy(existing.exclusive) || istype(existing, status_effect);
    }

    /// Called when someone actually eats this
    // tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:20
    public virtual void on_consumption(mob_living_carbon consumer = null, atom_movable consumed = null, object multiplier = null)
    {
        if (!HAS_TRAIT(consumer, TRAIT_ROCK_METAMORPHIC))
            return;
        apply_effects(consumer, consumed, multiplier ?? 1f);
    }

    /// Apply our desired effects to the eater
    // tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:26
    public virtual void apply_effects(mob_living_carbon consumer = null, atom_movable consumed = null, object multiplier = null)
    {
        if (status_effect != null)
            Waiting.NoSleep(consumer.apply_status_effect(status_effect, multiplier ?? 1f));
    }
}

/// More filling, and heals you
// tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:38
public partial class datum_golem_food_buff_iron : datum_golem_food_buff
{
    // tg: code/game/objects/items/stacks/golem_food/golem_food_buff.dm:45
    public override void apply_effects(mob_living_carbon consumer = null, atom_movable consumed = null, object multiplier = null)
    {
        if (Dm.eq(consumer.health, consumer.maxHealth))
            return;
        Waiting.NoSleep(consumer.heal_ordered_damage(healed_amount * Dm.num(multiplier ?? 1f), damage_heal_order));
        Dm.@new<obj_effect_temp_visual_heal>(get_turf(consumer), COLOR_HEALING_CYAN);
    }
}
