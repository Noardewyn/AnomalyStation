// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/butchering.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The butchering of corpses (gaps package C4): the limbs and throats of people, the mecha's drill and the recycler's are rows.

// tg: code/datums/components/butchering.dm:1
public partial class datum_component_butchering : datum_component
{
    // tg: code/datums/components/butchering.dm:18
    [DmParams("speed", "effectiveness", "bonus_modifier", "butcher_sound", "disabled", "can_be_blunt", "butcher_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        this.speed = Dm.num(Dm.arg(__extra, 0) ?? (8 * SECONDS));
        this.effectiveness = Dm.num(Dm.arg(__extra, 1) ?? 100f);
        this.bonus_modifier = Dm.num(Dm.arg(__extra, 2) ?? 0f);
        this.butcher_sound = Dm.arg(__extra, 3) ?? Dm.resource("sound/effects/butcher.ogg");
        if (Dm.truthy(Dm.arg(__extra, 4)))
            this.butchering_enabled = FALSE;
        this.can_be_blunt = Dm.num(Dm.arg(__extra, 5) ?? FALSE);
        this.butcher_callback = Dm.arg(__extra, 6) as datum_callback;
        return null;
    }

    // tg: code/datums/components/butchering.dm:36
    public override object Destroy(float? force = null)
    {
        butcher_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/butchering.dm:40
    public override async DmTask RegisterWithParent()
    {
        if (!isitem(parent))
            return;
        obj_item item_parent = parent as obj_item;
        item_parent.item_flags = Dm.num(Dm.bitor(item_parent.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(on_item_attack));
        RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_item_interaction));
    }

    // tg: code/datums/components/butchering.dm:50
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_ATTACK, COMSIG_ITEM_INTERACTING_WITH_ATOM));
    }

    // tg: code/datums/components/butchering.dm:53
    [SignalHandler]
    public virtual object on_item_attack(obj_item source = null, mob_living victim = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(source.get_sharpness()) && !Dm.truthy(can_be_blunt))
            return null;

        if (!Dm.truthy(user.combat_mode))
            return null;

        if (victim.stat == DEAD && (victim.butcher_results != null || victim.guaranteed_butcher_results != null) && Dm.truthy(butchering_enabled))
        {
            Waiting.Detach(start_butcher(source, victim, user));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (!ishuman(victim) || !Dm.truthy(source.force))
            return null;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && Dm.truthy(butchering_enabled))
        {
            Waiting.Detach(butcher_human(source, victim as mob_living_carbon_human, user));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (user.pulling != victim || Dm.num(user.grab_state) < GRAB_AGGRESSIVE || !Dm.eq(user.zone_selected, BODY_ZONE_HEAD))
            return null;

        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning("You don't want to harm other living beings!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        if (Dm.truthy(victim.has_status_effect(typeof(datum_status_effect_neck_slice))))
            return null;

        Waiting.Detach(start_neck_slice(source, victim as mob_living_carbon_human, user));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/butchering.dm:88
    [SignalHandler]
    public virtual object on_item_interaction(obj_item source = null, mob_living user = null, atom target = null)
    {
        if (!Dm.truthy(source.get_sharpness()) && !Dm.truthy(can_be_blunt))
            return null;

        if (!Dm.truthy(user.combat_mode) || !Dm.truthy(butchering_enabled))
            return null;

        if (target is obj_item_bodypart limb)
        {
            Waiting.Detach(butcher_limb(source, limb, user));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return null;
    }

    // Severed limbs and people butchered for meat (get_butcher_drops, drop_organs, the replacement limbs and the fat) wait for the surgery states to expose them (a row).
    // tg: code/datums/components/butchering.dm:128
    public virtual async DmTask butcher_limb(obj_item source = null, obj_item_bodypart target = null, mob_living user = null)
    {
        Dm.Deferred("code/datums/components/butchering.dm:128", "butcher_limb: cutting the meat off a person's limb");
    }

    // tg: code/datums/components/butchering.dm:274
    public virtual async DmTask start_butcher(obj_item source = null, mob_living target = null, mob_living user = null)
    {
        to_chat(user, span_notice(Dm.text("You begin to butcher []...", target)));
        playsound(target.loc, butcher_sound, 50, TRUE, -1);
        if (Dm.truthy(await do_after(user, Dm.num(speed), target)) && Dm.truthy(await target.Adjacent(source)))
            await on_butchering(user, target);
    }

    // tg: code/datums/components/butchering.dm:280
    public virtual async DmTask butcher_human(obj_item source = null, mob_living_carbon_human victim = null, mob_living user = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, victim)))
        {
            to_chat(user, span_warning(Dm.text("You're already interacting with []!", victim)));
            return;
        }

        DmList butcher_spots = TgState.Current.datum_component_butchering__butcher_spots ??= (DmList)typecacheof(Dm.list(typeof(obj_structure_table), typeof(obj_structure_bed), typeof(obj_machinery_stasis), typeof(obj_structure_kitchenspike)));

        bool found_spot = false;
        foreach (obj thing in Dm.each<obj>(victim.loc))
        {
            if (Dm.truthy(is_type_in_typecache(thing, butcher_spots)))
            {
                found_spot = true;
                break;
            }
        }

        if (!found_spot)
        {
            to_chat(user, span_warning(Dm.text("You need a better spot to butcher []!", victim)));
            return;
        }

        obj_item_bodypart limb = victim.get_bodypart(deprecise_zone(user.zone_selected) as string) as obj_item_bodypart;
        if (limb == null)
        {
            to_chat(user, span_warning(Dm.text("[] doesn't have a []!", victim, parse_zone(deprecise_zone(user.zone_selected) as string))));
            return;
        }

        await butcher_limb(source, limb, user);
    }

    // tg: code/datums/components/butchering.dm:305
    public virtual async DmTask start_neck_slice(obj_item source = null, mob_living_carbon_human victim = null, mob_living user = null)
    {
        Dm.Deferred("code/datums/components/butchering.dm:305", "start_neck_slice: slitting a pinned victim's throat (the neck_slice status effect stays inert)");
    }

    /**
     * Handles a user butchering a target
     *
     * Arguments:
     * - [butcher][/mob/living]: The mob doing the butchering
     * - [target][/mob/living]: The mob being butchered
     */
    // tg: code/datums/components/butchering.dm:343
    public virtual async DmTask on_butchering(atom butcher = null, mob_living target = null)
    {
        DmList results = Dm.list();
        atom location = target.drop_location() as atom;
        float final_effectiveness = Dm.num(effectiveness) - Dm.num(target.butcher_difficulty);
        float bonus_chance = Dm.max(0, (final_effectiveness - 100) + Dm.num(bonus_modifier)); //so 125 total effectiveness = 25% extra chance
        if (Dm.truthy(Dm.bitand(target.flags_1, HOLOGRAM_1)))
        {
            butcher.visible_message(span_notice(Dm.text("[] tries to butcher [], but it vanishes.", butcher, target)),
                span_notice(Dm.text("You try to butcher [], but it vanishes.", target)));
            qdel(target);
            return;
        }

        DmList failures = Dm.list();
        DmList bonuses = Dm.list();

        foreach (object remains in Dm.each(target.butcher_results))
        {
            Type remains_type = remains as Type;
            float amount = Dm.truthy(target.butcher_results[remains]) ? Dm.num(target.butcher_results[remains]) : 1;
            bool is_stack = ispath(remains_type, typeof(obj_item_stack));
            float rounds = amount;
            for (int i = 1; i <= rounds; i++)
            {
                if (!prob(final_effectiveness))
                {
                    Dm.combine(ref failures, initial(remains_type, "name"));
                    amount -= 1;
                    continue;
                }
                if (prob(bonus_chance))
                {
                    if (!is_stack)
                        Dm.append(ref results, Dm.@new(remains_type, location));
                    amount += 1;
                    Dm.combine(ref bonuses, initial(remains_type, "name"));
                }
                if (!is_stack)
                    Dm.append(ref results, Dm.@new(remains_type, location));
            }
            if (is_stack && amount > 0)
                Dm.append(ref results, Dm.@new(remains_type, location, amount));
        }
        target.butcher_results?.Cut();

        if (butcher != null)
        {
            if (Dm.length(failures) > 0)
                to_chat(butcher, span_warning(Dm.text("You fail to harvest some of the [] from [].", english_list(failures), target)));
            if (Dm.length(bonuses) > 0)
                to_chat(butcher, span_info(Dm.text("You harvest some extra [] from []!", english_list(bonuses), target)));
        }

        foreach (object guaranteed_remains in Dm.each(target.guaranteed_butcher_results))
        {
            Type remains_type = guaranteed_remains as Type;
            float amount = Dm.num(target.guaranteed_butcher_results[guaranteed_remains]);
            if (ispath(remains_type, typeof(obj_item_stack)))
            {
                Dm.append(ref results, Dm.@new(remains_type, location, amount));
                continue;
            }
            for (int i = 1; i <= amount; i++)
                Dm.append(ref results, Dm.@new(remains_type, location));
        }
        target.guaranteed_butcher_results?.Cut();

        // Transfer delicious reagents to meat
        if (target.reagents is datum_reagents target_reagents)
        {
            float meat_produced = 0;
            foreach (obj_item_food target_meat in Dm.each<obj_item_food>(results))
                meat_produced += 1;
            foreach (obj_item_food target_meat in Dm.each<obj_item_food>(results))
                await target_reagents.trans_to(target_meat, Dm.num(target_reagents.total_volume) / meat_produced, remove_blacklisted: TRUE);
        }

        // Don't forget yummy diseases either! (gaps VI1)
        if (iscarbon(target))
        {
            mob_living_carbon host_target = (mob_living_carbon)target;
            DmList diseases = await host_target.get_static_viruses() as DmList;
            if (Dm.truthy(LAZYLEN(diseases)))
            {
                DmList diseases_to_add = Dm.list();
                foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
                {
                    // admin or special viruses that should not be reproduced
                    if (!Dm.truthy(Dm.bitand(disease.spread_flags, DISEASE_SPREAD_SPECIAL | DISEASE_SPREAD_NON_CONTAGIOUS)))
                        Dm.append(diseases_to_add, disease);
                }

                if (Dm.truthy(LAZYLEN(diseases_to_add)))
                {
                    foreach (obj diseased_remains in Dm.each<obj>(results))
                        await diseased_remains.AddComponent(typeof(datum_component_infective), diseases_to_add);
                }
            }
        }

        if (butcher != null)
            butcher.visible_message(span_notice(Dm.text("[] butchers [].", butcher, target)),
                span_notice(Dm.text("You butcher [].", target)));
        if (butcher_callback != null)
            await butcher_callback.Invoke(butcher, target);
        await target.harvest(butcher as mob_living);
        await target.gib(Dm.bitor(DROP_BRAIN, DROP_ORGANS));
    }
}

public sealed partial class TgState
{
    internal DmList datum_component_butchering__butcher_spots;
}
