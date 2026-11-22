// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the stoat's infused organs (the weaker heart that makes one tall, the biting fangs, the chirping
// tongue that loves mice and eggs and makes one hungrier, the dark-seeing eyes, the furred ears that hear further, the snout) and their
// set bonus: fearless, dodging bigger attackers, with the instincts' moods of who is nearby. The colour define is file-local; the var
// blocks are generated (D111).

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:4
public partial class datum_status_effect_organ_set_bonus_stoat : datum_status_effect_organ_set_bonus
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:15
    public override async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        object dot = await base.enable_bonus(inserted_organ, visuals_only);
        RegisterSignal(owner, COMSIG_LIVING_CHECK_BLOCK, nameof(dodge_bigger_attack));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:19
    public override async DmTask disable_bonus(obj_item_organ removed_organ = null, object visuals_only = null)
    {
        await base.disable_bonus(removed_organ, visuals_only);
        UnregisterSignal(owner, COMSIG_LIVING_CHECK_BLOCK);
        owner.clear_mood_event("stoat_enemy");
        owner.clear_mood_event("stoat_friendly");
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:25
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, COMSIG_LIVING_CHECK_BLOCK);
        owner.clear_mood_event("stoat_enemy");
        owner.clear_mood_event("stoat_friendly");
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:31
    [SignalHandler]
    public virtual object dodge_bigger_attack(datum source = null, atom_movable hit_by = null, object damage = null, object the_attack = null, object attack_type = null, params object[] __extra)
    {
        if (!Dm.eq(attack_type, UNARMED_ATTACK) && !Dm.eq(attack_type, OVERWHELMING_ATTACK) && !Dm.eq(attack_type, LEAP_ATTACK))
            return NONE;
        if (!(big_attack_dodge_cd <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
            return NONE;
        if (isliving(hit_by))
        {
            mob_living attacker = hit_by as mob_living;
            if (attacker.mob_size <= owner.mob_size)
                return NONE;
        }
        else if (!ismecha(hit_by))
            return NONE;

        if (Dm.truthy(owner.incapacitated) || Dm.truthy(owner.is_blind()))
            return FAILED_BLOCK;

        Waiting.Detach(owner.emote("spin")); // INVOKE_ASYNC
        owner.visible_message(
            span_warning(I18n.T("[] instinctively dodges [] from []!", owner, the_attack, hit_by)),
            span_warning(I18n.T("You instinctively dodge out of the way of [] from []!", the_attack, hit_by)),
            vision_distance: COMBAT_MESSAGE_RANGE);
        Waiting.NoSleep(owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_stoat_dodge)));
        _addtimer(Dm.@new<datum_callback>(owner, nameof(mob.remove_movespeed_modifier), typeof(datum_movespeed_modifier_stoat_dodge)), 1 * SECONDS, file: "code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm", line: 55);
        big_attack_dodge_cd = world.time + 5 * SECONDS; // COOLDOWN_START, inline
        playsound(owner, "sound/items/weapons/punchmiss.ogg", 25, TRUE, -1);
        return SUCCESSFUL_BLOCK;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:60
    public virtual bool is_dangerous_mob(mob_living target = null)
    {
        if (IS_UNCONSCIOUS(target))
            return false;
        if (target is mob_living_basic_stoat)
            return Dm.eq(owner.gender, MALE) && Dm.eq(target.gender, MALE); // other stoats are ENEMIES if we are both males
        foreach (obj_item weapon in Dm.each<obj_item>(target.held_items))
        {
            if (Dm.num(weapon.force) > 15 || isgun(weapon))
                return true;
        }
        if (target.mob_size > owner.mob_size)
            return true;
        if (target.mob_size == owner.mob_size)
            return !ishuman(target); // assuming same-sized animals are enemies, same-sized humans are friends
        return false;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:74
    public virtual bool is_friendly_mob(mob_living target = null)
    {
        if (IS_UNCONSCIOUS(target))
            return false;
        if (target is mob_living_basic_stoat)
            return !Dm.eq(owner.gender, MALE) || !Dm.eq(target.gender, MALE);
        if (ishuman(target))
            return true;
        return false;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:83
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        if (!Dm.truthy(bonus_active))
            return dot;
        float nearby_friends = 0;
        float nearby_enemies = 0;
        foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(oview(5, owner)))
            nearby_enemies++;
        foreach (mob_living nearby in Dm.each<mob_living>(oview(5, owner)))
        {
            if (is_dangerous_mob(nearby))
                nearby_enemies++;
            else if (is_friendly_mob(nearby))
                nearby_friends++;
        }

        if (nearby_enemies != 0)
        {
            if (nearby_enemies == 1)
                await owner.add_mood_event("stoat_enemy", typeof(datum_mood_event_stoat_enemies_nearby_one));
            else if (nearby_enemies >= 2 && nearby_enemies <= 4)
                await owner.add_mood_event("stoat_enemy", typeof(datum_mood_event_stoat_enemies_nearby_multiple));
            else if (nearby_enemies >= 4)
                await owner.add_mood_event("stoat_enemy", typeof(datum_mood_event_stoat_enemies_nearby_crowd));
            owner.clear_mood_event("stoat_friendly");
        }

        else
        {
            if (nearby_friends == 0)
                await owner.add_mood_event("stoat_friendly", typeof(datum_mood_event_stoat_friendlies_nearby_one));
            else if (nearby_friends >= 2 && nearby_friends <= 4)
                await owner.add_mood_event("stoat_friendly", typeof(datum_mood_event_stoat_friendlies_nearby_multiple));
            else if (nearby_friends >= 4)
                await owner.add_mood_event("stoat_friendly", typeof(datum_mood_event_stoat_friendlies_nearby_crowd));
            owner.clear_mood_event("stoat_enemy");
        }
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:119
public partial class obj_item_organ_heart_stoat : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:131
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:136
public partial class obj_item_organ_fangs_stoat : obj_item_organ_fangs
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:146
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:150
public partial class obj_item_organ_tongue_stoat : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:164
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:168
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (prob(1))
            playsound(owner, "sound/mobs/non-humanoids/stoat/stoat_sounds.ogg", 100);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:173
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(receiver, COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY, nameof(get_perceived_food_quality)); // RegisterSignals with the one signal
        MODIFY_PHYSIOLOGY(receiver, PHYS_COEFF_HUNGER_MOD, 2);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:178
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY);
        MODIFY_PHYSIOLOGY(organ_owner, PHYS_COEFF_HUNGER_MOD, 0.5f);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:183
    [SignalHandler]
    public virtual void get_perceived_food_quality(mob_living_carbon consumer = null, obj_item_food consumed_food = null, DmList extra_quality = null)
    {
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return;
        if (consumed_food is obj_item_food_deadmouse || consumed_food is obj_item_food_egg)
            Dm.append(ref extra_quality, LIKED_FOOD_QUALITY_CHANGE);
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:191
public partial class obj_item_organ_eyes_stoat : obj_item_organ_eyes
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:205
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their eyes are black orbs."), zone));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:210
public partial class obj_item_organ_ears_stoat : obj_item_organ_ears
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:221
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their ears are furred, and twitch occasionally."), zone));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:226
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        organ_owner.eavesdrop_range += 2;
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:230
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        ((mob_living_carbon)organ_owner).eavesdrop_range -= 2;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:234
public partial class obj_item_organ_snout_stoat : obj_item_organ_snout
{
    // tg: code/game/machinery/dna_infuser/organ_sets/stoat_organs.dm:237
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_stoat)));
        return dot;
    }
}
