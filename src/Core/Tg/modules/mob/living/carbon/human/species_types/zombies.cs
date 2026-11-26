// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/zombies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the high-functioning zombie species and the zombified status effect that makes (and unmakes) an
// infectious zombie, with its mutant claws, regeneration and infection organ. The movespeed modifiers are generated. The infection's
// spreading by bites is the claw's (modules/zombie/items.cs); the romerol that hands it out as an event stays out (D333, D349).

// tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:1
public partial class datum_species_zombie : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:8
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:13
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
        await human_who_gained_species.apply_status_effect(typeof(datum_status_effect_zombie_uninfected));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:17
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(human, new_species, pref_load, __extra);
        human.remove_status_effect(typeof(datum_status_effect_zombie_uninfected));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:21
    public override object get_physical_attributes()
    {
        return I18n.T("Zombies are undead, and thus completely immune to any environmental hazard, or any physical threat besides blunt force trauma and burns. Their limbs are easy to pop off their joints, but they can somehow just slot them back in.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:25
    public override object get_species_description()
    {
        return I18n.T("A rotting zombie! They descend upon Space Station Thirteen Every year to spook the crew! \"Sincerely, the Zombies!\"");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:28
    public override object get_species_lore()
    {
        return Dm.list(I18n.T("Zombies have long lasting beef with Botanists. Their last incident involving a lawn with defensive plants has left them very unhinged."));
    }

    // Override for the default temperature perks, so we can establish that they don't care about temperature very much
    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:32
    public override object create_pref_temperature_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, "thermometer-half"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("No Body Temperature")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Having long since departed, Zombies do not have anything regulating their body temperature anymore. This means that the environment decides their body temperature - which they don't mind at all, until it gets a bit too hot.")))));

        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:48
public partial class datum_status_effect_zombie : datum_status_effect
{
    /// List of traits applied to all zombie types
    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:82  (a static list in tg: built per use, nothing ever changes it)
    public static DmList zombie_traits => Dm.list(
        TRAIT_BLOODY_MESS,
        TRAIT_COLD_BLOODED,
        TRAIT_EASILY_WOUNDED,
        TRAIT_EASYDISMEMBER,
        TRAIT_FAKEDEATH,
        TRAIT_LIMBATTACHMENT,
        TRAIT_LIVERLESS_METABOLISM,
        TRAIT_NOBREATH,
        TRAIT_NOCRITDAMAGE,
        TRAIT_NODEATH,
        TRAIT_NOHUNGER,
        TRAIT_RADIMMUNE,
        TRAIT_RESISTCOLD,
        TRAIT_RESISTHIGHPRESSURE,
        TRAIT_RESISTLOWPRESSURE,
        TRAIT_TOXIMMUNE);

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:105
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!ishuman(owner) || HAS_TRAIT(owner, TRAIT_UNHUSKABLE))
            return FALSE;

        mob_living_carbon_human new_zombie = owner as mob_living_carbon_human;
        RegisterSignal(new_zombie, COMSIG_HUMAN_SPEC_STUN, nameof(on_spec_stun));
        if (movespeed_mod != null)
            await new_zombie.add_movespeed_modifier(movespeed_mod);
        MODIFY_PHYSIOLOGY(new_zombie, STAMINA, stamina_modifier); // Zombie stam resist
        new_zombie.damage_resistance += damage_modifier;
        new_zombie.add_traits(Dm.bitor(unique_traits, zombie_traits) as DmList, TRAIT_STATUS_EFFECT(id));
        new_zombie.lighting_cutoff_red = 25;
        new_zombie.lighting_cutoff_green = 35;
        new_zombie.lighting_cutoff_blue = 5;
        new_zombie.update_sight();
        new_zombie.set_combat_mode(TRUE, silent: TRUE, force: TRUE);
        set_zombie_temperature();
        add_zombie_biotypes();

        // Ensures we have an infection organ even if we were applied by something else
        obj_item_organ_zombie_infection infection = new_zombie.get_organ_slot(ORGAN_SLOT_ZOMBIE) as obj_item_organ_zombie_infection;
        if (infection == null)
        {
            infection = Dm.@new<obj_item_organ_zombie_infection>();
            await infection.Insert(new_zombie);
            RegisterSignal(infection, COMSIG_ORGAN_REMOVED, nameof(organ_removed));
        }

        obj_item_bodypart_head head = new_zombie.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (!QDELETED(head))
            head.can_dismember = TRUE;

        obj_item_organ_tongue old_tongue = new_zombie.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
        if (!QDELETED(old_tongue))
        {
            await old_tongue.Remove(new_zombie, special: TRUE);
            if (!QDELETED(old_tongue))
            {
                await old_tongue.moveToNullspace();
                removed_tongue = WEAKREF(old_tongue) as datum_weakref;
            }
        }

        obj_item_organ_tongue_zombie new_tongue = Dm.@new<obj_item_organ_tongue_zombie>();
        await new_tongue.Insert(new_zombie, special: TRUE);

        if (zombie_hand != null)
            await new_zombie.AddComponent(typeof(datum_component_mutant_hands), Dm.named("mutant_hand_path", zombie_hand));
        if (regen_amount > 0)
        {
            await new_zombie.AddComponent(typeof(datum_component_regenerator),
                Dm.named("regeneration_delay", regen_time),
                Dm.named("brute_per_second", regen_amount),
                Dm.named("burn_per_second", regen_amount),
                Dm.named("tox_per_second", regen_amount),
                Dm.named("oxy_per_second", regen_amount * 0.5f),
                Dm.named("heals_wounds", TRUE));
        }

        new_zombie.become_husk(id);

        RegisterSignal(new_zombie, COMSIG_SPECIES_GAIN, nameof(zombie_species_changed));
        RegisterSignal(new_zombie, COMSIG_LIVING_DEATH, nameof(zombie_died_somehow));
        RegisterSignal(new_zombie, COMSIG_CARBON_POST_ATTACH_LIMB, nameof(zombie_limb_gained));
        RegisterSignal(new_zombie, COMSIG_CARBON_POST_REMOVE_LIMB, nameof(zombie_limb_lost));

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:170
    public override async DmTask<object> on_remove()
    {
        mob_living_carbon_human was_zombie = owner as mob_living_carbon_human;

        obj_item_organ_tongue_zombie old_tongue = was_zombie.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue_zombie;
        obj_item_organ_tongue removed_tongue_real = removed_tongue?.resolve() as obj_item_organ_tongue;
        if (!QDELETED(old_tongue))
            qdel(old_tongue);
        if (!QDELETED(removed_tongue_real))
            await removed_tongue_real.Insert(was_zombie, special: TRUE);

        obj_item_bodypart_head head = was_zombie.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (!QDELETED(head))
            head.can_dismember = Dm.initial(head, (obj_item_bodypart_head p) => p.can_dismember);

        qdel(was_zombie.GetComponent(typeof(datum_component_mutant_hands)));
        qdel(was_zombie.GetComponent(typeof(datum_component_regenerator)));
        UnregisterSignal(was_zombie, COMSIG_HUMAN_SPEC_STUN);
        if (movespeed_mod != null)
            was_zombie.remove_movespeed_modifier(movespeed_mod);
        MODIFY_PHYSIOLOGY(was_zombie, STAMINA, 1 / stamina_modifier);
        was_zombie.damage_resistance -= damage_modifier;
        was_zombie.remove_traits(Dm.bitor(zombie_traits, unique_traits) as DmList, TRAIT_STATUS_EFFECT(id));
        was_zombie.lighting_cutoff_red = Dm.initial(was_zombie, (mob_living_carbon_human p) => p.lighting_cutoff_red);
        was_zombie.lighting_cutoff_green = Dm.initial(was_zombie, (mob_living_carbon_human p) => p.lighting_cutoff_green);
        was_zombie.lighting_cutoff_blue = Dm.initial(was_zombie, (mob_living_carbon_human p) => p.lighting_cutoff_blue);
        was_zombie.update_sight();
        await was_zombie.cure_husk(id);
        restore_zombie_temperature();
        remove_zombie_biotypes();

        obj_item_organ_zombie_infection infection = was_zombie.get_organ_slot(ORGAN_SLOT_ZOMBIE) as obj_item_organ_zombie_infection;
        if (!QDELETED(infection))
        {
            UnregisterSignal(infection, COMSIG_ORGAN_REMOVED);
            qdel(infection);
        }

        UnregisterSignal(was_zombie, COMSIG_SPECIES_GAIN);
        UnregisterSignal(was_zombie, COMSIG_LIVING_DEATH);
        UnregisterSignal(was_zombie, COMSIG_CARBON_POST_ATTACH_LIMB);
        UnregisterSignal(was_zombie, COMSIG_CARBON_POST_REMOVE_LIMB);
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:210
    public virtual void add_zombie_biotypes()
    {
        if (!Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_UNDEAD)))
        {
            owner.mob_biotypes = Dm.num(Dm.bitor(owner.mob_biotypes, MOB_UNDEAD));
            added_biotypes = MOB_UNDEAD;
        }

        if (Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_ORGANIC)))
        {
            owner.mob_biotypes = Dm.num(Dm.bitand(owner.mob_biotypes, Dm.bitnot(MOB_ORGANIC)));
            removed_biotypes = MOB_ORGANIC;
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:219
    [SignalHandler]
    // DEVIATION(DV587): the source first, so the 2 s cap applies (tg passes the list as the source and the cap never runs)
    public virtual void on_spec_stun(object source = null, DmList amount = null)
    {
        amount[1] = Dm.min(Dm.num(amount[1]), max_stun_length);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:223
    public virtual void remove_zombie_biotypes()
    {
        owner.mob_biotypes = Dm.num(Dm.bitand(owner.mob_biotypes, Dm.bitnot(added_biotypes)));
        owner.mob_biotypes = Dm.num(Dm.bitor(owner.mob_biotypes, removed_biotypes));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:227
    public virtual void set_zombie_temperature()
    {
        mob_living_carbon_human zombie = owner as mob_living_carbon_human;
        datum_species species = zombie.dna.species as datum_species;
        species.bodytemp_normal = T0C;
        species.bodytemp_heat_damage_limit = FIRE_MINIMUM_TEMPERATURE_TO_EXIST;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:232
    public virtual void restore_zombie_temperature()
    {
        mob_living_carbon_human zombie = owner as mob_living_carbon_human;
        datum_species species = zombie.dna.species as datum_species;
        species.bodytemp_normal = Dm.initial(species, (datum_species p) => p.bodytemp_normal);
        species.bodytemp_heat_damage_limit = Dm.initial(species, (datum_species p) => p.bodytemp_heat_damage_limit);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:237
    [SignalHandler]
    public virtual void zombie_limb_gained(mob_living_carbon_human zombie = null, obj_item_bodypart limb = null, params object[] __extra)
    {
        if (limb is obj_item_bodypart_head head)
            head.can_dismember = TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:242
    [SignalHandler]
    public virtual void zombie_limb_lost(mob_living_carbon_human zombie = null, obj_item_bodypart limb = null, params object[] __extra)
    {
        if (limb is obj_item_bodypart_head head)
            head.can_dismember = Dm.initial(head, (obj_item_bodypart_head p) => p.can_dismember);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:247
    [SignalHandler]
    public virtual void organ_removed(obj_item_organ_zombie_infection infection = null, params object[] __extra)
    {
        UnregisterSignal(infection, COMSIG_ORGAN_REMOVED);
        qdel(this);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:252
    [SignalHandler]
    public virtual void zombie_died_somehow(mob_living_carbon_human zombie = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
            return;

        obj_item_organ_zombie_infection infection = owner.get_organ_slot(ORGAN_SLOT_ZOMBIE) as obj_item_organ_zombie_infection;
        qdel(infection); // this will in turn qdel this status effect
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:260
    [SignalHandler]
    public virtual void zombie_species_changed(mob_living_carbon_human zombie = null, params object[] __extra)
    {
        set_zombie_temperature();
        add_zombie_biotypes();
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:273
public partial class datum_status_effect_zombie_uninfected : datum_status_effect_zombie
{
    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:284
    [SignalHandler]
    public override void organ_removed(obj_item_organ_zombie_infection infection = null, params object[] __extra)
    {
        return; // nope
    }

    // tg: code/modules/mob/living/carbon/human/species_types/zombies.dm:287
    [SignalHandler]
    public override void zombie_died_somehow(mob_living_carbon_human zombie = null, object gibbed = null)
    {
        return; // you're stuck with it bud
    }
}
