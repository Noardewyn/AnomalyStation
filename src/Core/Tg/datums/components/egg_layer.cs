// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/egg_layer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # egg layer component!
 *
 * Component that manages how many eggs to lay, what can be fed to the mob to make them lay more, and what is actually laid.
 */
// tg: code/datums/components/egg_layer.dm:8
public partial class datum_component_egg_layer : datum_component
{
    // tg: code/datums/components/egg_layer.dm:26
    [DmParams("egg_type", "food_types", "feed_messages", "lay_messages", "eggs_left", "eggs_added_from_eating", "max_eggs_held", "egg_laid_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent)) //yes, you could make a tameable toolbox.
            return COMPONENT_INCOMPATIBLE;

        this.egg_type = (Type)Dm.arg(__extra, 0);
        this.food_types = Dm.arg(__extra, 1) as DmList;
        this.feed_messages = Dm.arg(__extra, 2) as DmList;
        this.lay_messages = Dm.arg(__extra, 3) as DmList;
        this.eggs_left = Dm.arg(__extra, 4);
        this.eggs_added_from_eating = Dm.arg(__extra, 5);
        this.max_eggs_held = Dm.arg(__extra, 6);
        this.egg_laid_callback = Dm.arg(__extra, 7) as datum_callback;

        START_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/datums/components/egg_layer.dm:42
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(feed_food));
    }

    // tg: code/datums/components/egg_layer.dm:46
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_ATTACKBY);
    }

    // tg: code/datums/components/egg_layer.dm:50
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSobj, this);
        egg_laid_callback = null;
        return dot;
    }

    // tg: code/datums/components/egg_layer.dm:55
    [SignalHandler]
    public virtual object feed_food(datum source = null, obj_item food = null, mob_living attacker = null, object @params = null)
    {
        atom at_least_atom = parent as atom;
        if (!Dm.truthy(is_type_in_list(food, food_types)))
            return null;
        if (isliving(at_least_atom))
        {
            mob_living potentially_dead_horse = at_least_atom as mob_living;
            if (potentially_dead_horse.stat == DEAD)
            {
                to_chat(attacker, span_warning(Dm.text("[] is dead!", parent)));
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }
        if (Dm.num(eggs_left) > Dm.num(max_eggs_held))
        {
            to_chat(attacker, span_warning(Dm.text("[] doesn't seem hungry!", parent)));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        attacker.visible_message(span_notice(Dm.text("[] hand-feeds [] to [].", attacker, food, parent)), span_notice(Dm.text("You hand-feed [] to [].", food, parent)));
        at_least_atom.visible_message(pick(feed_messages));
        qdel(food);
        eggs_left = Dm.num(eggs_left) + Dm.min(Dm.num(eggs_left) + Dm.num(eggs_added_from_eating), Dm.num(max_eggs_held));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/egg_layer.dm:75
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSOBJ_DT();
        atom at_least_atom = parent as atom;
        if (isliving(at_least_atom))
        {
            mob_living potentially_dead_horse = at_least_atom as mob_living;
            if (IS_UNCONSCIOUS_OR_CRIT(potentially_dead_horse))
                return null;
        }
        if (!Dm.truthy(eggs_left) || !SPT_PROB(1.5f, seconds_per_tick))
            return null;

        at_least_atom.visible_message(span_alertalien(Dm.text("[] []", at_least_atom, pick(lay_messages))));
        eggs_left = Dm.num(eggs_left) - 1;
        obj_item egg = Dm.@new(egg_type, get_turf(at_least_atom)) as obj_item;
        egg.pixel_x = rand(-6, 6);
        egg.pixel_y = rand(-6, 6);
        if (egg_laid_callback != null)
            Waiting.NoSleep(egg_laid_callback.Invoke(egg));
        return null;
    }
}
