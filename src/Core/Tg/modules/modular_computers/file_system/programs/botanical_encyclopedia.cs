// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the Botanical Encyclopedia, every plant's seed data and the plant traits, with its
// NtosBotanicalEncyclopedia window. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm:1
public partial class datum_computer_file_program_botanical_encyclopedia : datum_computer_file_program
{
    /// Lazy list of all seed data for valid seed types.
    /// Generated when first needed to display the UI.
    // tg: code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm:13  var/static/list/all_seed_data = null
    private static DmList all_seed_data
    {
        get => TgState.Current.datum_computer_file_program_botanical_encyclopedia__all_seed_data;
        set => TgState.Current.datum_computer_file_program_botanical_encyclopedia__all_seed_data = value;
    }

    // tg: code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm:15
    public virtual async DmTask generate_all_seed_data()
    {
        all_seed_data = Dm.list();

        foreach (Type seed_type in Dm.each_cast<Type>(valid_subtypesof(typeof(obj_item_seeds))))
        {
            if (seed_type.IsAssignableTo(typeof(obj_item_seeds_random)))
                continue;

            obj_item_seeds seeds = Dm.@new(seed_type) as obj_item_seeds;

            DmList seed_data = await generate_seed_data_from(seeds) as DmList;

            seed_data["name"] = full_capitalize(Dm.initial(seed_type, (obj_item_seeds s) => s.plantname));
            seed_data["icon"] = Dm.initial(seed_type, (obj_item_seeds s) => s.growing_icon);
            object icon_harvest = Dm.initial(seed_type, (obj_item_seeds s) => s.icon_harvest);
            seed_data["icon_state"] = Dm.truthy(icon_harvest) ? icon_harvest : Dm.text("[]-harvest", Dm.initial(seed_type, (obj_item_seeds s) => s.species));

            all_seed_data.Add(Dm.list(seed_data));

            qdel(seeds);
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm:35
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        if (all_seed_data == null)
            await generate_all_seed_data();

        DmList data = Dm.list();

        data["seeds"] = all_seed_data;
        data["cycle_seconds"] = HYDROTRAY_CYCLE_DELAY / 10;
        data["trait_db"] = Dm.list();

        foreach (datum_plant_gene trait in Dm.each<datum_plant_gene>(GLOB.plant_traits))
        {
            Dm.append_at(data, "trait_db", Dm.list(Dm.list(
                Dm.kv("path", trait.GetType()),
                Dm.kv("name", trait.get_name()),
                Dm.kv("icon", trait.icon),
                Dm.kv("description", trait.description))));
        }

        return data;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/botanical_encyclopedia.dm:13  the program's var/static/list/all_seed_data (rules 10.6)
    internal DmList datum_computer_file_program_botanical_encyclopedia__all_seed_data;
}
