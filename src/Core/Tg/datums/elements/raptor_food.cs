// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/raptor_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// Kept as tg has it: nothing in tg sends COMSIG_ITEM_EATEN_BY_BASIC_MOB (signals_food.dm:86), so on_eaten never runs and a raptor's
// foods_eaten stays empty; the examine hint works.

/// Element which influences raptor children upon owner's consumption as food
// tg: code/datums/elements/raptor_food.dm:2
public partial class datum_element_raptor_food : datum_element
{
    // tg: code/datums/elements/raptor_food.dm:22
    [DmParams("target", "attack_modifier", "health_modifier", "speed_modifier", "ability_modifier", "growth_modifier", "personality_traits", "color_chances")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object attack_modifier = Dm.arg(__extra, 0);
        object health_modifier = Dm.arg(__extra, 1);
        object speed_modifier = Dm.arg(__extra, 2);
        object ability_modifier = Dm.arg(__extra, 3);
        object growth_modifier = Dm.arg(__extra, 4);
        DmList personality_traits = Dm.arg<DmList>(__extra, 5);
        DmList color_chances = Dm.arg<DmList>(__extra, 6);
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        this.attack_modifier = attack_modifier;
        this.health_modifier = health_modifier;
        this.speed_modifier = speed_modifier;
        this.ability_modifier = ability_modifier;
        this.growth_modifier = growth_modifier;
        this.personality_traits = personality_traits;
        this.color_chances = color_chances;
        RegisterSignal(target, COMSIG_ITEM_EATEN_BY_BASIC_MOB, nameof(on_eaten));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine_more));
        return dot;
    }

    // tg: code/datums/elements/raptor_food.dm:36
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_EATEN_BY_BASIC_MOB, COMSIG_ATOM_EXAMINE_MORE));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/raptor_food.dm:40
    [SignalHandler]
    public virtual void on_examine_more(obj_item source = null, mob examiner = null, DmList examine_list = null)
    {
        if (!(examiner is mob_living living_examiner) || !(living_examiner.buckled is mob_living_basic_raptor))
            return;

        // Just to check for priority
        float strognest_effect = Dm.num(Dm.max(attack_modifier, health_modifier, speed_modifier, ability_modifier, growth_modifier, 0));

        // Only personality or color probability effects, or just pure negatives
        if (strognest_effect == 0)
        {
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have unique effects on your raptor's offspring if you fed it to them...")));
            return;
        }

        if (Dm.eq(strognest_effect, attack_modifier))
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have make your raptor's offspring stronger if you fed it to them...")));
        else if (Dm.eq(strognest_effect, health_modifier))
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have make your raptor's offspring tougher if you fed it to them...")));
        else if (Dm.eq(strognest_effect, speed_modifier))
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have make your raptor's offspring faster if you fed it to them...")));
        else if (Dm.eq(strognest_effect, ability_modifier))
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have make your raptor's offspring more capable if you fed it to them...")));
        else if (Dm.eq(strognest_effect, growth_modifier))
            Dm.append(ref examine_list, span_notice(I18n.T("You reckon this would have make your raptor's offspring grow faster if you fed it to them...")));
    }

    // tg: code/datums/elements/raptor_food.dm:65
    [SignalHandler]
    public virtual void on_eaten(obj_item source = null, mob_living eater = null, mob_living feeder = null)
    {
        if (!(eater is mob_living_basic_raptor raptor))
            return;

        DmList our_food = raptor.inherited_stats.foods_eaten[source.type] as DmList;
        if (our_food != null)
        {
            our_food["amount"] = Dm.num(our_food["amount"]) + 1;
            return;
        }

        raptor.inherited_stats.foods_eaten[source.type] = Dm.list(
            Dm.kv("amount", 1),
            Dm.kv("attack", attack_modifier),
            Dm.kv("health", health_modifier),
            Dm.kv("speed", speed_modifier),
            Dm.kv("ability", ability_modifier),
            Dm.kv("growth", growth_modifier),
            Dm.kv("traits", personality_traits?.Copy()),
            Dm.kv("color_chances", color_chances?.Copy()));
    }
}
