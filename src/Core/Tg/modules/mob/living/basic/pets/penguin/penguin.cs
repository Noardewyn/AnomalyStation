// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/penguin/penguin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the fishing, the egg laying and the growing up (rows: their components are inert) (gaps package C2).

// tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:2
public partial class mob_living_basic_pet_penguin : mob_living_basic_pet
{
    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:44
    private static DmList delicious_food => TgState.Current.mob_living_basic_pet_penguin__delicious_food ??= Dm.list(typeof(obj_item_fish));

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_MOB_CAN_DIG, TRAIT_NODROWN, TRAIT_SWIMMER), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_cultist_pet)));
        Waiting.NoSleep(AddElement(typeof(datum_element_wears_collar)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "honk"));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_PENGUIN, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddComponent(typeof(datum_component_profound_fisher)));
        Waiting.NoSleep(AddElementTrait(TRAIT_WADDLING, INNATE_TRAIT, typeof(datum_element_waddling)));

        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), 10f, null, null, null, null, null, delicious_food));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_BASIC_FOODS, typecacheof(delicious_food));

        if (!Dm.truthy(can_lay_eggs))
            return dot;
        Waiting.NoSleep(AddComponent(typeof(datum_component_egg_layer), typeof(obj_item_food_egg_penguin_egg), Dm.list(typeof(obj_item_food_fishmeat)), null, Dm.list("lays an egg.", "squats down and croons.", "begins making a huge racket.", "begins clucking raucously."), 0f, 1f, 1f, Dm.@new<datum_callback>(this, nameof(lay_penguin_egg))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:62
    public override async DmTask<object> UnarmedAttack(atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        object dot = await base.UnarmedAttack(attack_target, proximity_flag, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(proximity_flag))
            return dot;
        if (attack_target is not obj_item_food_egg_penguin_egg)
            return dot;

        remove_egg(); //to check if we already have a egg
        obj_item egg_target = attack_target as obj_item;
        await egg_target.forceMove(this);
        carried_egg = attack_target as obj;
        add_overlay("penguin_egg_overlay");
        RegisterSignal(egg_target, COMSIG_QDELETING, nameof(on_hatch_egg));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:79
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        remove_egg();
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:83
    public virtual void lay_penguin_egg(obj_item penguin_egg = null)
    {
        if (prob(35))
            Waiting.NoSleep(penguin_egg.AddComponent(typeof(datum_component_fertile_egg), typeof(mob_living_basic_pet_penguin_baby), 1f, 2f, 400f, 0f, typecacheof(Dm.list(typeof(turf), typeof(mob_living_basic_pet_penguin)))));
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:95
    [SignalHandler]
    public virtual void on_hatch_egg()
    {
        remove_egg();
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:99
    public virtual void remove_egg()
    {
        if (carried_egg == null)
            return;
        Waiting.NoSleep(carried_egg.forceMove(get_turf(this)));
        UnregisterSignal(carried_egg, COMSIG_QDELETING);
        carried_egg = null;
        cut_overlay("penguin_egg_overlay");
    }
}

// tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:130
public partial class mob_living_basic_pet_penguin_baby : mob_living_basic_pet_penguin
{
    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:145
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(can_grow_up))
            return dot;
        DmList weight_mobtypes = Dm.list(Dm.kv(typeof(mob_living_basic_pet_penguin_emperor), 5), Dm.kv(typeof(mob_living_basic_pet_penguin_emperor_shamebrero), 1));
        object grown_type = pick_weight(weight_mobtypes);
        Waiting.NoSleep(AddComponent(typeof(datum_component_growth_and_differentiation), null, grown_type, 100f, 0.5f, 1f, null, Dm.list(COMSIG_MOB_CLIENT_LOGIN), Dm.@new<datum_callback>(this, nameof(ready_to_grow))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/penguin/penguin.dm:165
    public virtual object ready_to_grow()
    {
        return !IS_UNCONSCIOUS_OR_CRIT(this) ? TRUE : FALSE;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pet_penguin__delicious_food;
}
