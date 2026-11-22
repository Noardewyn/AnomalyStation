// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/bear/_bear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the butter bear's craft (a player's brain, with crafted mobs: a row) (gaps package C4).

//Space bears!
// tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:2
public partial class mob_living_basic_bear : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_SWIMMER, TRAIT_FENCE_CLIMBER, TRAIT_SNOWSTORM_IMMUNE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tree_climber), Dm.named("climbing_distance", 15f)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BEAR, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:55
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;

        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_bear));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:62
    public override async DmTask update_icons()
    {
        await base.update_icons();
        if (Dm.truthy(armored))
            add_overlay("armor_bear");
    }

    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:67
    public virtual void extract_combs(obj_structure_beebox hive = null)
    {
        if (!Dm.truthy(Dm.length(hive.honeycombs)))
            return;
        var honey_food = pick_n_take(hive.honeycombs) as obj_item_food_honeycomb;
        if (honey_food == null)
            return;
        Waiting.NoSleep(honey_food.forceMove(get_turf(this)));
    }
}

// tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:96
public partial class mob_living_basic_bear_snow_ancient : mob_living_basic_bear_snow
{
    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_GODMODE, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:119
public partial class mob_living_basic_bear_butter : mob_living_basic_bear //The mighty companion to Cak. Several functions used from it.
{
    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:136
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_regenerator), Dm.named("regeneration_delay", 1 * SECONDS), Dm.named("brute_per_second", 5f), Dm.named("outline_colour", COLOR_YELLOW)));
        DmList on_consume = TgState.Current.mob_living_basic_bear_butter__on_consume ??= Dm.list(
            Dm.kv(typeof(datum_reagent_consumable_nutriment), 1f),
            Dm.kv(typeof(datum_reagent_consumable_nutriment_vitamin), 0.1f));
        Waiting.NoSleep(AddElement(typeof(datum_element_consumable_mob), on_consume));
        return dot;
    }

    //Borrowed code from Cak, feeds people if they hit you. More nutriment but less vitamin to represent BUTTER.
    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:150
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy((user as mob_living)?.combat_mode) && user.reagents != null && stat == 0)
        {
            await (user.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 1);
            await (user.reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_vitamin), 0.1f);
        }
        return dot;
    }

    //Makes the butter bear's attacks against vertical targets slip said targets
    // tg: code/modules/mob/living/basic/space_fauna/bear/_bear.dm:175
    public override async DmTask<object> UnarmedAttack(atom target = null, object proximity_flag = null, DmList modifiers = null)
    {
        object dot = await base.UnarmedAttack(target, proximity_flag, modifiers);
        if (!Dm.truthy(dot) || !isliving(target))
            return dot;
        mob_living victim = target as mob_living;
        if (!Dm.eq(victim.body_position, STANDING_UP))
            return dot;
        await victim.Knockdown(20);
        playsound(loc, Dm.resource("sound/misc/slip.ogg"), 15);
        victim.visible_message(span_danger(Dm.text("[] slips on []'s butter!", victim, this)));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_bear_butter__on_consume;
}
