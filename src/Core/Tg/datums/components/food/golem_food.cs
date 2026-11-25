// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food/golem_food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the component that makes a mineral edible to a golem, and the abstract snack it eats. The var
// blocks are generated.

/// Component which allows something to be eaten by a golem
// tg: code/datums/components/food/golem_food.dm:4
public partial class datum_component_golem_food : datum_component
{
    // tg: code/datums/components/food/golem_food.dm:14
    [DmParams("consume_on_eat", "golem_food_key", "extra_validation", "food_multiplier")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object consume_on_eat = Dm.arg(__extra, 0) ?? TRUE;
        object golem_food_key = Dm.arg(__extra, 1);
        datum_callback extra_validation = Dm.arg<datum_callback>(__extra, 2);
        object food_multiplier = Dm.arg(__extra, 3) ?? 1f;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(golem_food_key) || !Dm.truthy(is_path_in_list(golem_food_key, GLOB.golem_stack_food_directory)))
            throw Dm.CRASH("Golem food type not specified, this is a required parameter.", "code/datums/components/food/golem_food.dm", 18);

        this.consume_on_eat = consume_on_eat;
        snack_type = GLOB.golem_stack_food_directory[golem_food_key] as datum_golem_food_buff;
        this.extra_validation = extra_validation;
        if (Dm.truthy(food_multiplier))
            this.food_multiplier = food_multiplier;
        return null;
    }

    // tg: code/datums/components/food/golem_food.dm:26
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(on_attack));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/food/golem_food.dm:31
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_ATTACK, COMSIG_ATOM_EXAMINE));
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/food/golem_food.dm:35
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref golem_snack);
        snack_type = null;
        extra_validation = null;
        return base.Destroy(force);
    }

    /// Attempt to feed this item to golem
    // tg: code/datums/components/food/golem_food.dm:42
    [SignalHandler]
    public virtual object on_attack(atom source = null, mob_living target = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode) || !HAS_TRAIT(target, TRAIT_ROCK_EATER))
            return null;
        if (extra_validation != null && !Dm.truthy(Waiting.NoSleep(extra_validation.Invoke())))
        {
            source.balloon_alert(user, I18n.T("not edible!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(snack_type.can_consume(target)))
        {
            source.balloon_alert(user, I18n.T("can't consume!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        if (golem_snack == null)
            create_golem_snack(source);
        Waiting.Detach(golem_snack.attack__proc(target, user, modifiers)); // INVOKE_ASYNC: eating takes a do_after, a signal handler cannot wait for it
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Creates our golem snack atom instance
    // tg: code/datums/components/food/golem_food.dm:58
    public virtual void create_golem_snack(atom source = null)
    {
        golem_snack = Dm.@new<obj_item_food_golem_food>(null);
        golem_snack.setup(
            name: source.name,
            consume_food: consume_on_eat,
            food_buff: snack_type,
            owner: parent as atom,
            nutrition_mod: food_multiplier);
        RegisterSignal(golem_snack, COMSIG_QDELETING, nameof(on_food_destroyed));
    }

    /// Reference handling for abstract food object
    // tg: code/datums/components/food/golem_food.dm:70
    [SignalHandler]
    public virtual void on_food_destroyed(params object[] __extra)
    {
        golem_snack = null;
    }

    /// Add extra examine text to people who have golem brains
    // tg: code/datums/components/food/golem_food.dm:75
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (!HAS_TRAIT(user, TRAIT_ROCK_METAMORPHIC))
            return;
        Dm.append(ref examine_text, span_notice(snack_type.added_info));
    }
}

/// Abstract food item used to feed golems
// tg: code/datums/components/food/golem_food.dm:83
public partial class obj_item_food_golem_food : obj_item_food
{
    /// Set up some properties based on a passed-in item that the golem will pretend to eat
    // tg: code/datums/components/food/golem_food.dm:96
    public virtual void setup(string name = null, object consume_food = null, datum_golem_food_buff food_buff = null, atom owner = null, object nutrition_mod = null)
    {
        this.name = name;
        this.consume_food = Dm.num(consume_food ?? TRUE);
        this.food_buff = food_buff;
        this.owner = owner;
        this.bite_consumption = Dm.num(food_buff.nutrition) * Dm.num(nutrition_mod);
        RegisterSignal(owner, COMSIG_QDELETING, nameof(on_parent_destroyed));
    }

    /// Clean ourselves up if our parent dies
    // tg: code/datums/components/food/golem_food.dm:111
    [SignalHandler]
    public virtual void on_parent_destroyed(datum destroyed_thing = null, params object[] __extra)
    {
        qdel(this);
    }

    // tg: code/datums/components/food/golem_food.dm:115
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible),
            Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(took_bite))),
            Dm.named("volume", 1E+31f));
    }

    /// Called when someone bites this food, subtract one charge from our material stack
    // tg: code/datums/components/food/golem_food.dm:120
    public virtual void took_bite(mob eater = null, params object[] __extra)
    {
        if (owner == null)
        {
            qdel(this);
            return;
        }

        food_buff.on_consumption(eater as mob_living_carbon, owner as atom_movable);
        if (!Dm.truthy(consume_food))
        {
            qdel(this);
            return;
        }

        if (isstack(owner))
        {
            obj_item_stack stack_owner = owner as obj_item_stack;
            Waiting.NoSleep(stack_owner.use(1));
        }
        else
            qdel(owner);
    }
}
