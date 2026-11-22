// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/slime/actions.dm:2
public partial class datum_action_innate_slime : datum_action_innate
{
    // tg: code/modules/mob/living/basic/slime/actions.dm:15
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext_x = 2;
        return button;
    }

    // tg: code/modules/mob/living/basic/slime/actions.dm:20
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        mob_living_basic_slime slime_owner = owner as mob_living_basic_slime;
        if (life_stage_required != null && !Dm.eq(slime_owner.life_stage, life_stage_required))
            return FALSE;
        if (Dm.num(slime_owner.nutrition) < nutrition_cost)
            return FALSE;
        if (Dm.truthy(needs_growth) && slime_owner.amount_grown < SLIME_EVOLUTION_THRESHOLD)
            return FALSE;
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/slime/actions.dm:39
public partial class datum_action_innate_slime_evolve : datum_action_innate_slime
{
    // tg: code/modules/mob/living/basic/slime/actions.dm:47
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button button = null, float? force = null)
    {
        await base.update_button_status(button, force);
        mob_living_basic_slime slime_owner = owner as mob_living_basic_slime;
        if (life_stage_required != null && !Dm.eq(slime_owner.life_stage, life_stage_required))
        {
            button.maptext = "";
            return;
        }
        button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("[]/[]", slime_owner.amount_grown, SLIME_EVOLUTION_THRESHOLD));
    }

    // tg: code/modules/mob/living/basic/slime/actions.dm:55
    public override async DmTask<object> Activate()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            if (owner.stat == DEAD)
                owner.balloon_alert(owner, "dead!");
            else if (IS_UNCONSCIOUS(owner))
                owner.balloon_alert(owner, "unconscious!");
            else
                owner.balloon_alert(owner, "in critical!");
            return FALSE;
        }
        mob_living_basic_slime slime_owner = owner as mob_living_basic_slime;
        if (Dm.eq(slime_owner.life_stage, SLIME_LIFE_STAGE_ADULT))
        {
            slime_owner.balloon_alert(slime_owner, "already adult!");
            return null;
        }
        if (slime_owner.amount_grown < SLIME_EVOLUTION_THRESHOLD)
        {
            slime_owner.balloon_alert(slime_owner, "need to grow!");
            return null;
        }
        if (Dm.num(slime_owner.nutrition) < nutrition_cost)
        {
            slime_owner.balloon_alert(slime_owner, "need food!");
            return null;
        }
        await slime_owner.adjust_nutrition(-nutrition_cost);
        slime_owner.set_life_stage(SLIME_LIFE_STAGE_ADULT);
        slime_owner.update_name();
        await slime_owner.regenerate_icons();
        slime_owner.amount_grown = 0;
        return null;
    }
}

// tg: code/modules/mob/living/basic/slime/actions.dm:87
public partial class datum_action_innate_slime_reproduce : datum_action_innate_slime
{
    // tg: code/modules/mob/living/basic/slime/actions.dm:94
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button button = null, float? force = null)
    {
        await base.update_button_status(button, force);
        mob_living_basic_slime slime_owner = owner as mob_living_basic_slime;
        if (life_stage_required != null && !Dm.eq(slime_owner.life_stage, life_stage_required))
        {
            button.maptext = "";
            return;
        }
        button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("[]/[]", slime_owner.amount_grown, SLIME_EVOLUTION_THRESHOLD));
    }

    // tg: code/modules/mob/living/basic/slime/actions.dm:102
    public override async DmTask<object> Activate()
    {
        mob_living_basic_slime slime_owner = owner as mob_living_basic_slime;
        await slime_owner.reproduce();
        return null;
    }
}

public partial class mob_living_basic_slime
{
    ///Attempts to split the slime into four
    // tg: code/modules/mob/living/basic/slime/actions.dm:107
    public virtual async DmTask<object> reproduce()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            if (stat == DEAD)
                balloon_alert(this, "dead!");
            else if (IS_UNCONSCIOUS(this))
                balloon_alert(this, "unconscious!");
            else
                balloon_alert(this, "in critical!");
            return FALSE;
        }

        if (!isopenturf(loc))
            balloon_alert(this, "not here!");

        if (!Dm.eq(life_stage, SLIME_LIFE_STAGE_ADULT))
        {
            balloon_alert(this, "not adult!");
            return null;
        }

        if (amount_grown < SLIME_EVOLUTION_THRESHOLD)
        {
            balloon_alert(this, "need growth!");
            return null;
        }

        DmList friends_list = Dm.list();
        foreach (mob_living_basic_slime friend in Dm.each<mob_living_basic_slime>(loc))
        {
            if (QDELETED(friend))
                continue;
            if (friend == this)
                continue;
            Dm.append(ref friends_list, friend);
        }

        overcrowded = Dm.length(friends_list) >= SLIME_OVERCROWD_AMOUNT ? TRUE : FALSE;
        if (Dm.truthy(overcrowded))
        {
            balloon_alert(this, "overcrowded!");
            return null;
        }

        float new_nutrition = Dm.floor(Dm.num(nutrition) * 0.9f);
        float new_powerlevel = Dm.floor(powerlevel * 0.25f);
        object drop_loc = drop_location();
        DmList created_slimes = Dm.list(this);
        DmList slime_friends = Dm.list();
        foreach (object ally_ref in Dm.each(allies))
        {
            mob_living possible_friend = Dm.locate_ref(ally_ref) as mob_living;
            if (QDELETED(possible_friend) || !Dm.@in(possible_friend, GLOB.mob_living_list))
                continue;
            Dm.append(ref slime_friends, possible_friend);
        }

        object our_faction = get_faction();
        for (int i = 1; i <= 3; i++)
        {
            mob_living_basic_slime baby = Dm.@new<mob_living_basic_slime>(drop_loc, get_random_mutation());
            Dm.append(ref created_slimes, baby);
            baby.add_faction(our_faction);
            foreach (object slime_friend in Dm.each(slime_friends))
                await baby.befriend(slime_friend as mob_living);
            await step_away(baby, this);
        }

        await set_nutrition(SLIME_STARTING_NUTRITION);

        foreach (mob_living_basic_slime baby in Dm.each_cast<mob_living_basic_slime>(created_slimes))
        {
            if (Dm.truthy(ckey)) // Player slimes are more robust at spliting. Once an oversight of poor copypasta, now a feature!
                await baby.set_nutrition(new_nutrition);
            baby.powerlevel = new_powerlevel;
            if (mutation_chance != 0)
                baby.mutation_chance = Dm.clamp(mutation_chance + rand(-5, 5), 0, 100);
            else
                baby.mutation_chance = 0;
        }

        set_life_stage(SLIME_LIFE_STAGE_BABY);
        set_slime_type(get_random_mutation());
        amount_grown = 0;
        mutator_used = FALSE;
        return null;
    }

    ///Returns a random slime mutation for this slime's type
    // tg: code/modules/mob/living/basic/slime/actions.dm:179
    public virtual object get_random_mutation()
    {
        if (mutation_chance >= 100)
            return typeof(datum_slime_type_rainbow);
        else if (prob(mutation_chance))
            return pick_weight(slime_type.mutations);
        else
            return slime_type.GetType();
    }
}
