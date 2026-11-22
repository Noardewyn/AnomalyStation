// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the goliath's infused organs (red night vision eyes, lavaland lungs, the brain that turns an arm into
// a tendril hammer, the ashstorm-proof heart) and their set bonus, lava immunity; the tendril hammer's mining-fauna blow. The colour
// defines are file-local; the var blocks are generated (D111).

///goliath eyes, simple night vision
// tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:15
public partial class obj_item_organ_eyes_night_vision_goliath : obj_item_organ_eyes_night_vision
{
    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their eyes are blood red and stone-like."), BODY_ZONE_PRECISE_EYES));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_goliath)));
        return dot;
    }
}

///goliath lungs! You can breathe lavaland air mix but can't breath pure O2 from a tank anymore.
// tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:40
public partial class obj_item_organ_lungs_lavaland_goliath : obj_item_organ_lungs_lavaland
{
    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their back is covered in small tendrils."), BODY_ZONE_CHEST));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_goliath)));
        return dot;
    }
}

///goliath brain. you can't use gloves but one of your arms becomes a tendril hammer that can be used to mine!
// tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:56
public partial class obj_item_organ_brain_goliath : obj_item_organ_brain
{
    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their arm is just a mass of plate and tendrils."), BODY_ZONE_CHEST));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_goliath)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:75
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (!ishuman(brain_owner))
            return dot;
        mob_living_carbon_human human_receiver = brain_owner as mob_living_carbon_human;
        if (!Dm.truthy(human_receiver.can_mutate()))
            return dot;
        datum_species rec_species = human_receiver.dna.species as datum_species;
        await rec_species.update_no_equip_flags(brain_owner, Dm.bitor(rec_species.no_equip_flags, ITEM_SLOT_GLOVES));
        hammer = Dm.@new<obj_item_goliath_infuser_hammer>();
        await brain_owner.put_in_hands(hammer);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:87
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(brain_owner);
        if (!ishuman(brain_owner))
            return;
        mob_living_carbon_human human_receiver = brain_owner as mob_living_carbon_human;
        if (!Dm.truthy(human_receiver.can_mutate()))
            return;
        datum_species rec_species = human_receiver.dna.species as datum_species;
        await rec_species.update_no_equip_flags(brain_owner, Dm.initial(rec_species, (datum_species s) => s.no_equip_flags));
        if (hammer != null)
        {
            brain_owner.visible_message(span_warning(I18n.T("The [] disintegrates!", hammer)));
            QDEL_NULL(ref hammer);
        }
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:102
public partial class obj_item_goliath_infuser_hammer : obj_item
{
    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:126
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:130
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("You can use your tendril hammer arm to deliver a devastating blow against mining fauna, but only once every two seconds."));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:134
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living target = target_mob;
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        //If we're on cooldown, we'll do a normal attack.
        if (!(tendril_hammer_cd <= world.time)) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
            return dot;

        //do a normal attack if our target isn't living, since we're gonna define them after this.
        if (!isliving(target))
            return dot;

        mob_living fresh_pancake = target;

        // Check for nemesis factions on the target.
        if (!Dm.truthy(fresh_pancake.has_faction(Dm.list(FACTION_MINING, FACTION_BOSS))))
        {
            // Target is not a nemesis, so attack normally.
            return dot;
        }

        // Apply nemesis-specific effects.
        await nemesis_effects(user, fresh_pancake);

        // Target is a nemesis, and so now we do the extra big damage and go on cooldown
        await fresh_pancake.apply_damage(mining_bonus_force, Dm.str(damtype)); //smush
        tendril_hammer_cd = world.time + 2 * SECONDS; // COOLDOWN_START, inline
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:159
    public virtual async DmTask nemesis_effects(mob_living user = null, mob_living target = null)
    {
        if (target is mob_living_simple_animal_hostile_asteroid_elite)
            return;
        ///we obtain the relative direction from the bat itself to the target
        if (!QDELETED(target))
            await target.throw_at(get_edge_target_turf(target, get_cardinal_dir(this, target)) as atom, rand(1, 2), prob(60) ? 1 : 4, user);
    }
}

/// goliath heart gives you the ability to survive ash storms.
// tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:168
public partial class obj_item_organ_heart_goliath : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/goliath_organs.dm:180
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their skin has visible hard plates growing from within."), BODY_ZONE_CHEST));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_goliath)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }
}
