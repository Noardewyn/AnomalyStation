// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/clown_ops/outfits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3, D316: the clown operatives' outfits); the outfits with no proc are their generated var blocks (D111).

// tg: code/modules/antagonists/clown_ops/outfits.dm:1
public partial class datum_outfit_syndicate_clownop : datum_outfit_syndicate
{
    // tg: code/modules/antagonists/clown_ops/outfits.dm:24
    public override async DmTask post_equip(mob_living_carbon_human nukie = null, float? visuals_only = null)
    {
        await base.post_equip(nukie, visuals_only);
        DmList nukie_contents = nukie.get_all_contents() as DmList;
        var gun = Dm.locate<obj_item_gun_ballistic_automatic_pistol_toy_riot_clandestine>(nukie_contents);
        if (gun != null)
        {
            qdel(gun.pin);
            var new_pin = Dm.@new<obj_item_firing_pin_clown_ultra>();
            await new_pin.gun_insert(null, gun, TRUE);
        }

        var old_mask = Dm.locate<obj_item_clothing_mask_gas_syndicate>(nukie_contents);
        qdel(old_mask);

        await nukie.dna.add_mutation(typeof(datum_mutation_clumsy), MUTATION_SOURCE_MUTATOR);
    }
}
