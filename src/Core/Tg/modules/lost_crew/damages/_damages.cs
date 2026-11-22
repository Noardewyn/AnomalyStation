// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/_damages.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).
// DEVIATION(DV561): a weighted roll of a damage class leaves out an entry whose tg content is not ported yet (an inert species,
// the prosthetic limb quirk, the heretic's blades); the weights of the rest stay tg's. dv561_unported below is the one check.

// tg: code/modules/lost_crew/damages/_damages.dm:7
public partial class datum_corpse_damage_class : datum
{
    // tg: code/modules/lost_crew/damages/_damages.dm:2
    public const string CORPSE_DAMAGE_ORGAN_DECAY = "organ decay";
    // tg: code/modules/lost_crew/damages/_damages.dm:3
    public const string CORPSE_DAMAGE_ORGAN_LOSS = "organ loss";
    // tg: code/modules/lost_crew/damages/_damages.dm:4
    public const string CORPSE_DAMAGE_LIMB_LOSS = "limb loss";

    /// Generate and apply a possible character (species etc)
    // tg: code/modules/lost_crew/damages/_damages.dm:32
    public virtual async DmTask apply_character(mob_living_carbon_human fashion_corpse = null, DmList protected_objects = null, DmList recovered_items = null, DmList on_revive_and_player_occupancy = null, DmList body_data = null)
    {
        Type character_type = pick_weight(dv561_live(possible_character_types)) as Type; // DEVIATION(DV561)
        var character = Dm.@new(character_type) as datum_corpse_character;
        await character.apply_character(fashion_corpse, protected_objects, recovered_items, on_revive_and_player_occupancy);

        object assignment_pick = pick_weight(possible_character_assignments);
        datum_corpse_assignment assignment = null;
        if (ispath(assignment_pick))
        {
            assignment = Dm.@new((Type)assignment_pick) as datum_corpse_assignment;
            await assignment.apply_assignment(fashion_corpse, protected_objects, recovered_items, on_revive_and_player_occupancy);
            Dm.append(ref body_data, assignment.type);
        }

        object flavor_pick = pick_weight(dv561_live(possible_flavor_types)); // DEVIATION(DV561)
        if (ispath(flavor_pick))
        {
            var flavor = Dm.@new((Type)flavor_pick) as datum_corpse_flavor;
            if (flavor != null)
                await flavor.apply_flavor(fashion_corpse, protected_objects, on_revive_and_player_occupancy);
            Dm.append(ref body_data, flavor.type);
        }

        Dm.append(ref death_lore, assignment?.job_lore);

        Dm.append(ref body_data, character.type);
    }

    /// Set up injuries
    // tg: code/modules/lost_crew/damages/_damages.dm:54
    public virtual async DmTask apply_injuries(mob_living_carbon_human victim = null, DmList saved_objects = null, DmList on_revive_and_player_occupancy = null, DmList body_data = null)
    {
        bool bonus_roll = prob(80); //do an extra turn for cause of death or post mortum effect
        bool bonus_roll_used = false;
        DmList used_damage_types = Dm.list();

        datum_corpse_damage_cause_of_death cause_of_death = null;
        for (int i = 0; i <= 2; i++)
        {
            cause_of_death = pick_damage_type(possible_causes_of_death, used_damage_types, bonus_roll_used ? TRUE : FALSE) as datum_corpse_damage_cause_of_death;

            if (cause_of_death == null) //we ran out of causes of death (impressive!)
                break;

            await cause_of_death.apply_to_body(victim, rand(), saved_objects, on_revive_and_player_occupancy);
            Dm.append(ref body_data, cause_of_death.type);

            if (Dm.truthy(cause_of_death.no_bonus_roll) || !bonus_roll || prob(70)) //70% chance to do the bonus roll, otherwise pass the bonus to post mortem
                break;

            bonus_roll = false;
            bonus_roll_used = true;
        }

        for (int i = 0; i <= 2; i++)
        {
            var post_mortem = pick_damage_type(post_mortem_effects, used_damage_types) as datum_corpse_damage;
            if (post_mortem != null)
            {
                await post_mortem.apply_to_body(victim, rand(), saved_objects, on_revive_and_player_occupancy);
                Dm.append(ref body_data, post_mortem.type);
            }

            if (!bonus_roll)
                break;

            bonus_roll = false;
        }

        var decay = pick_damage_type(decays, used_damage_types) as datum_corpse_damage;
        if (decay != null)
            await decay.apply_to_body(victim, rand(), saved_objects, on_revive_and_player_occupancy);
        Dm.append(ref body_data, decay.type);

        // Simulate bloodloss by dragging/moving
        victim.adjust_blood_volume(-Dm.num(await victim.bleed_drag_amount()) * rand(20, 100));
        set_death_date(victim);

        Dm.append(ref death_lore, area_lore + " " + cause_of_death.cause_of_death);
    }

    /// Wrapped pickweight so we can have a bit more controle over how we pick our rules
    // tg: code/modules/lost_crew/damages/_damages.dm:97
    public virtual object pick_damage_type(DmList damages = null, DmList used_damage_types = null, object bonus_roll_used = null)
    {
        DmList possible_damages = Dm.list();

        foreach (object damage in Dm.each(damages))
        {
            // DM precedence: (a && b && c) || (d && e)
            if (ispath(damage) && Dm.truthy(Dm.initial((Type)damage, (datum_corpse_damage p) => p.damage_type)) && Dm.@in(Dm.initial((Type)damage, (datum_corpse_damage p) => p.damage_type), used_damage_types)
                || Dm.truthy(bonus_roll_used) && ispath(damage) && Dm.truthy(Dm.initial((Type)damage, (datum_corpse_damage p) => p.no_bonus_roll)))
                continue;
            if (dv561_unported(damage)) // DEVIATION(DV561)
                continue;
            possible_damages[damage] = damages[damage];
        }

        object chosen_type = pick_weight(possible_damages);
        if (!ispath(chosen_type)) //can also be null for some variants
            return null;

        var chosen = Dm.@new((Type)chosen_type) as datum_corpse_damage;

        if (Dm.truthy(chosen.damage_type))
            Dm.append(ref used_damage_types, chosen.damage_type);
        return chosen;
    }

    /// Soulfully give a date of death for health analyzers
    // tg: code/modules/lost_crew/damages/_damages.dm:116
    public virtual void set_death_date(mob_living_carbon body = null)
    {
        float died_how_long_ago = rand(lore_death_time_min, lore_death_time_max);
        body.timeofdeath = world.time - died_how_long_ago;

        float death_real_time = world.realtime - died_how_long_ago;
        string current_date = time2text(death_real_time, "DD Month", 0);
        float current_year = Dm.num(text2num(time2text(death_real_time, "YYYY", NO_TIMEZONE))) + STATION_YEAR_OFFSET;
        body.station_timestamp_timeofdeath = $"{current_date} {Dm.str(current_year)}";
    }

    // DEVIATION(DV561): whether a weighted entry of the recovered crew's rolls needs tg content the port does not have yet: an inert
    // species (D338, not-translated.md section 226), a character or a damage class that only makes inert species, a quirk flavour
    // (the quirks wait for the owner, D333) or the heretic's blades (the heretic, D333). A null or text entry ("null" = 9) stays.
    internal static bool dv561_unported(object entry)
    {
        if (entry is not Type type)
            return false;
        if (InertSeam.IsInertType(type))
            return true;
        if (type.IsSubclassOf(typeof(datum_corpse_character)) || type == typeof(datum_corpse_character))
            return Dm.initial(type, (datum_corpse_character p) => p.species_type) is Type species && InertSeam.IsInertType(species);
        if (type.IsSubclassOf(typeof(datum_corpse_flavor_quirk)))
            return Dm.initial(type, (datum_corpse_flavor_quirk p) => p.quirk) is Type quirk && InertSeam.IsInertType(quirk);
        if (type == typeof(datum_corpse_damage_cause_of_death_melee_weapon_heretic))
            return InertSeam.IsInertType(typeof(obj_item_melee_sickly_blade));
        if (type.IsSubclassOf(typeof(datum_corpse_damage_class)))
        {
            // the list vars are made in dm_init_vars, so a prototype's are null (Dm.initial): read a fresh instance
            var scenario = Dm.@new(type) as datum_corpse_damage_class;
            foreach (object character in Dm.each(scenario.possible_character_types))
            {
                if (!dv561_unported(character))
                    return false;
            }
            return true;
        }
        return false;
    }

    // DEVIATION(DV561): a weighted list without its unported entries (dv561_unported)
    internal static DmList dv561_live(DmList weights)
    {
        if (weights == null)
            return null;
        DmList live = Dm.list();
        foreach (object entry in Dm.each(weights))
        {
            if (dv561_unported(entry))
                continue;
            live[entry] = weights[entry];
        }
        return live;
    }
}

// tg: code/modules/lost_crew/damages/_damages.dm:126
public partial class datum_corpse_damage : datum
{
    /// Tear IT UPPP!!! Apply any damages to the body that we need to
    // tg: code/modules/lost_crew/damages/_damages.dm:133
    public virtual async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        return null;
    }
}
