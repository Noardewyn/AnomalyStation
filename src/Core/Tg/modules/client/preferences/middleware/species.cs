// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/middleware/species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file, as the antagonists' middleware (antags.cs): the middleware datum is not translated; its asset, the
// species page's 64 px pictures, is sent with the preferences' static data as /icon recipes (DV44), each the dummy of that species in
// the consistent assistant's clothes flattened as tg's spritesheet makes it.

public partial class datum_preferences : datum
{
    // tg: code/modules/client/preferences/middleware/species.dm:4
    // get_ui_assets: list(get_asset_datum(/datum/asset/spritesheet_batched/species)) (DV44: sent with the static data by preferences.cs)

    // DEVIATION(DV44): the species spritesheet's pictures, list(key = list(resource, icon state)) as the preferences' pictures
    public virtual async DmTask<DmList> species_preview_icons()
    {
        TgState state = TgState.Current;
        if (state.species_spritesheet == null)
            state.species_spritesheet = await datum_asset_spritesheet_batched_species_create_spritesheets();
        DmList icons = Dm.list();
        foreach (object key in Dm.each(state.species_spritesheet))
        {
            icon picture = state.species_spritesheet[key] as icon;
            icons[key] = Dm.list(picture.dm_resource(), picture.dm_state);
        }
        return icons;
    }

    // /datum/asset/spritesheet_batched/species/create_spritesheets: the sheet's pictures by key, as insert_icon would add them (DV44)
    // tg: code/modules/client/preferences/middleware/species.dm:13
    private static async DmTask<DmList> datum_asset_spritesheet_batched_species_create_spritesheets()
    {
        DmList species_icons = Dm.list();
        foreach (object species_id in Dm.each(get_selectable_species()))
        {
            Type species_type = GLOB.species_list[species_id] as Type;

            mob_living_carbon_human_dummy_consistent dummy = Dm.@new<mob_living_carbon_human_dummy_consistent>();
            dummy.set_species(species_type);
            await dummy.equipOutfit(typeof(datum_outfit_job_assistant_consistent), visuals_only: TRUE);
            await ((datum_species)dummy.dna.species).prepare_human_for_preview(dummy);

            icon dummy_icon = get_flat_uni_icon(dummy) as icon;
            dummy_icon.Scale(64, 64);
            dummy_icon.Crop(15, 64 - 31, 15 + 31, 64);
            dummy_icon.Scale(64, 64);
            species_icons[sanitize_css_class_name(Dm.initial(species_type, (datum_species p) => p.name))] = dummy_icon; // insert_icon

            SSatoms.prepare_deletion(dummy);
        }
        return species_icons;
    }
}

public sealed partial class TgState
{
    // DV44: the species spritesheet's pictures (/datum/asset/spritesheet_batched/species)
    internal DmList species_spritesheet;
}
