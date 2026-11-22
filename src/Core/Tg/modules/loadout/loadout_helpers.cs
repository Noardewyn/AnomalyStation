// Translated from tgstation (snapshot 1d99bd7f): code/modules/loadout/loadout_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): equip_outfit_and_loadout without the loadout: the loadout preference (/datum/preference/loadout, its items)
// is not translated, so a player's loadout is empty and the outfit is equipped as it is (not-translated.md 79).

public partial class mob_living_carbon_human : mob_living_carbon
{
    /**
     * Equips this mob with a given outfit and loadout items as per the passed preferences.
     *
     * Loadout items override the pre-existing item in the corresponding slot of the job outfit.
     * Some generic items are added to the backpack, if they are not able to be equipped elsewhere.
     */
    // tg: code/modules/loadout/loadout_helpers.dm:14
    public virtual async DmTask<object> equip_outfit_and_loadout(object outfit = null, datum_preferences preference_source = null, float? visuals_only = null)
    {
        outfit ??= typeof(datum_outfit);
        visuals_only ??= FALSE;
        if (preference_source == null)
            return await equipOutfit(outfit, visuals_only);

        datum_outfit equipped_outfit;
        if (ispath(outfit, typeof(datum_outfit)))
            equipped_outfit = Dm.@new((Type)outfit) as datum_outfit;
        else if (outfit is datum_outfit given)
            equipped_outfit = given;
        else
            throw Dm.CRASH(Dm.text("Invalid outfit passed to equip_outfit_and_loadout ([])", outfit), "code/modules/loadout/loadout_helpers.dm", 28);

        // var/list/item_details = preference_source.read_preference(/datum/preference/loadout); the loadout's items inserted into the
        // outfit (:30-38) and their on_equip_item (:43-54): the loadout is not translated, so there are none
        // Equip the outfit loadout items included
        if (!Dm.truthy(await equipped_outfit.equip(this, visuals_only)))
            return FALSE;
        return TRUE;
    }
}
