// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/mushroom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:1
public partial class mob_living_basic_mushroom : mob_living_basic
{
    private ref mutable_appearance cap_living => ref TgState.Current.mob_living_basic_mushroom__cap_living;
    private ref mutable_appearance cap_dead => ref TgState.Current.mob_living_basic_mushroom__cap_dead;

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        melee_damage_lower = rand(3, 5);
        melee_damage_upper = rand(10, 20);
        maxHealth = rand(50, 70);
        cap_living ??= mutable_appearance(icon, "mushroom_cap") as mutable_appearance;
        cap_dead ??= mutable_appearance(icon, "mushroom_cap_dead") as mutable_appearance;
        cap_color = rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        update_mushroomcap();
        health = maxHealth;
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_WALKING_MUSHROOM, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:73
    public override async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        object dot = await base.UnarmedAttack(A, proximity_flag, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(proximity_flag))
            return dot;

        if (A is obj_item_food_grown_mushroom)
        {
            await recover(A as obj_item);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:85
    public override async DmTask<object> melee_attack(atom target_ = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target_, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot))
            return FALSE;
        if (target_ is not mob_living_basic_mushroom target || target.stat != DEAD)
            return dot;
        if (target.faint_ticker >= 3)
        {
            await consume_mushroom(target);
            return dot;
        }
        target.faint_ticker++;
        visible_message(span_notice(Dm.text("[] chews a bit on [].", this, target)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:97
    public virtual async DmTask consume_mushroom(mob_living_basic_mushroom consumed = null)
    {
        visible_message(span_warning(Dm.text("[] devours []!", this, consumed)));
        float level_gain = consumed.powerlevel - powerlevel;
        if (level_gain >= 0 && !Dm.truthy(ckey) && !Dm.truthy(consumed.bruised)) //Player shrooms can't level up to become robust gods.
            await consumed.level_up(level_gain);
        await adjust_brute_loss(-Dm.num(consumed.maxHealth));
        qdel(consumed);
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:105
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;

        icon_state = "mushroom_color";
        update_mushroomcap();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:113
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        update_mushroomcap();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:117
    public virtual void update_mushroomcap()
    {
        cut_overlays();
        cap_living.color = cap_color;
        cap_dead.color = cap_color;
        if (stat == DEAD)
            add_overlay(cap_dead);
        else
            add_overlay(cap_living);
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:126
    public virtual async DmTask recover(obj_item mush_meal = null)
    {
        visible_message(span_notice(Dm.text("[] eats []!", this, mush_meal)));
        update_mushroomcap();
        qdel(mush_meal);
        if (recovery_cooldown > world.time) // !COOLDOWN_FINISHED
            return;
        faint_ticker = 0;
        if (stat == DEAD)
            await revive(HEAL_ALL);
        else
            await adjust_brute_loss(-5);
        recovery_cooldown = world.time + 5 * MINUTES;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:139
    public virtual async DmTask level_up(float level_gain = 0)
    {
        await adjust_brute_loss(-Dm.num(maxHealth)); //They'll always heal, even if they don't gain a level
        if (powerlevel > 9)
            return;
        if (level_gain == 0)
            level_gain = 1;
        powerlevel += level_gain;
        if (prob(25))
            melee_damage_lower = Dm.num(melee_damage_lower) + level_gain * rand(1, 5);
        else
            melee_damage_upper = Dm.num(melee_damage_upper) + level_gain * rand(1, 5);
        maxHealth = Dm.num(maxHealth) + level_gain * rand(1, 5);
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:152
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_food_grown_mushroom)
            return await base.item_interaction(user, tool, modifiers, __extra);

        await recover(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:159
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(attacking_item.force) || Dm.truthy((user as mob_living)?.combat_mode))
            bruised = TRUE;
        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:164
    public override async DmTask harvest(mob_living user = null)
    {
        for (int counter = 0; counter <= powerlevel; counter++)
        {
            obj_item_food_hugemushroomslice shroomslice = Dm.@new<obj_item_food_hugemushroomslice>(loc);
            datum_reagents reagents = shroomslice.reagents as datum_reagents;
            await reagents.add_reagent(typeof(datum_reagent_drug_mushroomhallucinogen), powerlevel);
            await reagents.add_reagent(typeof(datum_reagent_medicine_omnizine), powerlevel);
            await reagents.add_reagent(typeof(datum_reagent_medicine_synaptizine), powerlevel);
        }
    }
}

// tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:66
public partial class datum_targeting_strategy_basic_mushroom : datum_targeting_strategy_basic
{
    ///we only attacked another mushrooms
    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:70
    public override object faction_check(datum_ai_controller controller = null, mob_living living_mob = null, mob_living the_target = null)
    {
        return Dm.truthy(living_mob.faction_check_atom(the_target, exact_match: check_factions_exactly)) ? FALSE : TRUE;
    }
}

public partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:38  var/static/mutable_appearance/cap_living
    internal mutable_appearance mob_living_basic_mushroom__cap_living;
    // tg: code/modules/mob/living/basic/space_fauna/mushroom.dm:40  var/static/mutable_appearance/cap_dead
    internal mutable_appearance mob_living_basic_mushroom__cap_dead;
}
