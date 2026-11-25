// Translated from tgstation (snapshot 1d99bd7f): code/modules/zombie/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the zombie's claws, whose hits plant the infection organ in humans and devour the dead, and the
// infecting proc itself. The vars are generated.

// tg: code/modules/zombie/items.dm:2
public partial class obj_item_mutant_hand_zombie : obj_item_mutant_hand
{
    // tg: code/modules/zombie/items.dm:16
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (QDELETED(target))
            return;
        if (ishuman(target))
            await try_to_zombie_infect(target as mob_living_carbon_human, user as mob_living, (user as mob_living).zone_selected, infect_chance);
        else if (isliving(target))
            await check_feast(target as mob_living, user as mob_living);
    }

    // tg: code/modules/zombie/items.dm:69
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is ripping [] brains out! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_theyre())));
        obj_item_bodypart head = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            await head.dismember();
        return BRUTELOSS;
    }

    // tg: code/modules/zombie/items.dm:76
    public virtual async DmTask check_feast(mob_living target = null, mob_living user = null)
    {
        if (target.stat == DEAD)
        {
            float hp_gained = target.maxHealth;
            target.investigate_log("has been devoured by a zombie.", INVESTIGATE_DEATHS);
            await target.gib(DROP_ALL_REMAINS);
            bool need_mob_update = false;
            need_mob_update |= Dm.truthy(await user.adjust_brute_loss(-hp_gained, updating_health: FALSE));
            need_mob_update |= Dm.truthy(await user.adjust_tox_loss(-hp_gained, updating_health: FALSE));
            need_mob_update |= Dm.truthy(await user.adjust_fire_loss(-hp_gained, updating_health: FALSE));
            need_mob_update |= Dm.truthy(await user.adjust_organ_loss(ORGAN_SLOT_BRAIN, -hp_gained)); // Zom Bee gibbers "BRAAAAISNSs!1!"
            await user.set_nutrition(min(Dm.num(user.nutrition) + hp_gained, NUTRITION_LEVEL_FULL));
            if (need_mob_update)
                await user.updatehealth();
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/zombie/items.dm:21
    public static async DmTask try_to_zombie_infect(mob_living_carbon_human target = null, mob_living user = null, object def_zone = null, float? base_chance = null)
    {
        def_zone ??= BODY_ZONE_CHEST;
        float chance = base_chance ?? 100;
        // CHECK_DNA_AND_SPECIES(target) (__DEFINES/DNA.dm:3, a statement macro that returns from the caller: inline, rules 11.1)
        if (!Dm.truthy(target.dna?.species))
            return;
        if (!prob(chance))
            return;

        // Can't zombify with no head
        if (target.get_bodypart(BODY_ZONE_HEAD) == null)
            return;

        // spaceacillin has a 75% chance to block infection
        if (HAS_TRAIT(target, TRAIT_VIRUS_RESISTANCE) && !HAS_TRAIT(target, TRAIT_IMMUNODEFICIENCY) && prob(75))
            return;

        obj_item_bodypart actual_limb = target.get_bodypart(Dm.str(def_zone)) as obj_item_bodypart;

        // What you hitting bro?
        if (actual_limb == null)
            return;

        float limb_damage = Dm.num(actual_limb.get_damage());
        float limb_armor = max(0, Dm.num(target.getarmor(actual_limb, BIO)) - 25);

        // This is a pretty jank way to do this, but in short:
        // if they have thick material on that bodypart it will always need at least 25 previous limb damage to trigger an infection.
        // and if their bio armor isn't thick it's a bit weaker.
        foreach (obj_item_clothing iter_clothing in Dm.each_cast<obj_item_clothing>(target.get_clothing_on_part(actual_limb)))
        {
            if (Dm.truthy(Dm.bitand(iter_clothing.clothing_flags, THICKMATERIAL)))
                limb_armor += 25;
        }

        if (limb_armor > limb_damage)
            return;

        obj_item_organ_zombie_infection infection = target.get_organ_slot(ORGAN_SLOT_ZOMBIE) as obj_item_organ_zombie_infection;
        if (infection == null)
        {
            infection = Dm.@new<obj_item_organ_zombie_infection>();
            await infection.Insert(target);
            to_chat(user, span_alien(I18n.T("You see [] twitch for a moment as [] head is covered in \\a [] - [] been infected.", target, target.p_their(), infection, target.p_Theyve())));
        }
    }
}
