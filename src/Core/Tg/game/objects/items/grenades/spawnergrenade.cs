// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/spawnergrenade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3b: the feral cat grenades). The subtypes have no procs; a grenade whose creature is not ported yet
// (the viscerators, the mutant clowns) spawns tg's inert mob. The var blocks are generated (D111).

// tg: code/game/objects/items/grenades/spawnergrenade.dm:1
public partial class obj_item_grenade_spawnergrenade : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/spawnergrenade.dm:10
    public override void apply_grenade_fantasy_bonuses(object quality = null)
    {
        deliveryamt = Dm.num(modify_fantasy_variable("deliveryamt", deliveryamt, quality));
    }

    // tg: code/game/objects/items/grenades/spawnergrenade.dm:13
    public override void remove_grenade_fantasy_bonuses(object quality = null)
    {
        deliveryamt = Dm.num(reset_fantasy_variable("deliveryamt", deliveryamt));
    }

    // tg: code/game/objects/items/grenades/spawnergrenade.dm:16
    public override async DmTask<object> detonate(mob_living lanced_by = null) // Prime now just handles the two loops that query for people in lockers and people who can see it.
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        if (spawner_type != null && Dm.truthy(deliveryamt))
        {
            // Make a quick flash
            turf target_turf = get_turf(this);
            playsound(target_turf, Dm.resource("sound/effects/phasein.ogg"), 100, TRUE);
            foreach (mob_living_carbon target_carbon in Dm.each<mob_living_carbon>(viewers(target_turf, null)))
                await target_carbon.flash_act();

            // Spawn some hostile syndicate critters and spread them out
            DmList spawned = await spawn_and_random_walk(spawner_type, target_turf, deliveryamt, walk_chance: 50, admin_spawn: Dm.truthy(Dm.bitand(flags_1, ADMIN_SPAWNED_1)) ? TRUE : FALSE) as DmList;
            afterspawn(spawned);
            foreach (mob_living created in Dm.each_cast<mob_living>(spawned))
                ADD_TRAIT(created, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
        }

        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/grenades/spawnergrenade.dm:38
    public virtual void afterspawn(DmList spawned = null)
    {
        return;
    }
}
