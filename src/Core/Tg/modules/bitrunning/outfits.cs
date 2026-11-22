// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/outfits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the outfits the domains force on avatars: the echolocator (its psyker head and brain wait
// for the psykers, not-translated section 230) and the island brawl's armed beach bum. The var blocks are generated (D111).

// tg: code/modules/bitrunning/outfits.dm:3
public partial class datum_outfit_echolocator : datum_outfit
{
    // tg: code/modules/bitrunning/outfits.dm:14
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);
        Dm.Deferred("code/modules/bitrunning/outfits.dm:16", "user.psykerize(): the psyker head and brain (religion/burdened/psyker.dm, the echolocation and the psychic spells) wait for the psykers");
    }
}

// tg: code/modules/bitrunning/outfits.dm:26
public partial class datum_outfit_beachbum_combat : datum_outfit
{
    // tg: code/modules/bitrunning/outfits.dm:54
    public override async DmTask post_equip(mob_living_carbon_human bum = null, float? visuals_only = null)
    {
        await base.post_equip(bum, visuals_only);

        int choice = (int)rand(1, length(ranged_weaps));
        Type weapon = ranged_weaps[choice] as Type;
        await bum.put_in_active_hand(Dm.@new(weapon) as obj_item);

        Type ammo = corresponding_ammo[choice] as Type;
        // tg's two spare rounds: the pockets get two more new ones, these two land on the floor only when a pocket refuses
        // (otherwise they stay in nullspace, as tg has it)
        obj_item ammo1 = Dm.@new(ammo) as obj_item;
        obj_item ammo2 = Dm.@new(ammo) as obj_item;

        if (!Dm.truthy(await bum.equip_to_slot_if_possible(Dm.@new(ammo) as obj_item, ITEM_SLOT_LPOCKET)))
            await ammo1.forceMove(get_turf(bum));
        if (!Dm.truthy(await bum.equip_to_slot_if_possible(Dm.@new(ammo) as obj_item, ITEM_SLOT_RPOCKET)))
            await ammo2.forceMove(get_turf(bum));

        if (prob(50))
            await bum.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_glasses_sunglasses>(), ITEM_SLOT_EYES);
    }
}
