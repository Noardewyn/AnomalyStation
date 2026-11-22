// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/character/_character.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

/// Datum for controlling the base character, such as species, scarring, styles, augments etc
// tg: code/modules/lost_crew/character/_character.dm:2
public partial class datum_corpse_character : datum
{
    // tg: code/modules/lost_crew/character/_character.dm:6
    public virtual async DmTask apply_character(mob_living_carbon_human fashionable_corpse = null, DmList saved_objects = null, DmList recovered_items = null, DmList on_revive_and_player_occupancy = null)
    {
        fashionable_corpse.set_species(species_type);
        await fashionable_corpse.fully_replace_character_name(fashionable_corpse.real_name, fashionable_corpse.generate_random_mob_name());
    }
}

/// Not really all roundstart species, but plasmaman is a bit too flamboyant and felinids aren't interesting
// tg: code/modules/lost_crew/character/_character.dm:11
public partial class datum_corpse_character_mostly_roundstart : datum_corpse_character
{
    // tg: code/modules/lost_crew/character/_character.dm:19
    public override async DmTask apply_character(mob_living_carbon_human fashionable_corpse = null, DmList saved_objects = null, DmList recovered_items = null, DmList on_revive_and_player_occupancy = null)
    {
        species_type = pick_weight(datum_corpse_damage_class.dv561_live(possible_species)) as Type; // DEVIATION(DV561): no inert species
        await base.apply_character(fashionable_corpse, saved_objects, recovered_items, on_revive_and_player_occupancy);
    }
}

/// used by the morgue trays to spawn bodies (obeying three different configs???????????????????? yes please daddy give me more config for benign features)
// tg: code/modules/lost_crew/character/_character.dm:27
public partial class datum_corpse_character_morgue : datum_corpse_character
{
    // tg: code/modules/lost_crew/character/_character.dm:29
    public override async DmTask apply_character(mob_living_carbon_human fashionable_corpse = null, DmList saved_objects = null, DmList recovered_items = null, DmList on_revive_and_player_occupancy = null)
    {
        float use_species = Dm.truthy(config.Get(typeof(datum_config_entry_flag_morgue_cadaver_disable_nonhumans))) ? 0 : 1;
        float species_probability = Dm.num(config.Get(typeof(datum_config_entry_number_morgue_cadaver_other_species_probability))) * use_species;
        object override_species = config.Get(typeof(datum_config_entry_string_morgue_cadaver_override_species));

        if (Dm.truthy(override_species))
            species_type = GLOB.species_list[override_species] as Type;

        else if (prob(species_probability))
        {
            // DEVIATION(DV561): the pick leaves out the selectable species the port has not ported yet (inert, D338)
            DmList selectable = Dm.list();
            foreach (object species_id in Dm.each(get_selectable_species()))
            {
                if (GLOB.species_list[species_id] is Type id_type && InertSeam.IsInertType(id_type))
                    continue;
                Dm.append(ref selectable, species_id);
            }
            species_type = GLOB.species_list[pick(selectable)] as Type;

            if (species_type == null)
            {
                stack_trace(Dm.text("failed to spawn cadaver with species ID []", species_type), "code/modules/lost_crew/character/_character.dm", 41); //if it's invalid they'll just be a human, so no need to worry too much aside from yelling at the server owner lol.
                species_type = Dm.initial(this, (datum_corpse_character p) => p.species_type);
            }
        }

        await base.apply_character(fashionable_corpse, saved_objects, recovered_items, on_revive_and_player_occupancy);
    }
}

// tg: code/modules/lost_crew/character/_character.dm:46
public partial class datum_corpse_character_pod : datum_corpse_character
{
    // tg: code/modules/lost_crew/character/_character.dm:49
    public override async DmTask apply_character(mob_living_carbon_human fashionable_corpse = null, DmList saved_objects = null, DmList recovered_items = null, DmList on_revive_and_player_occupancy = null)
    {
        await base.apply_character(fashionable_corpse, saved_objects, recovered_items, on_revive_and_player_occupancy);

        Dm.append(ref recovered_items, Dm.@new<obj_item_plant_analyzer>()); //needed to properly healthscan them
    }
}
