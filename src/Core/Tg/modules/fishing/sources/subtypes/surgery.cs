// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/sources/subtypes/surgery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the autowiki's table (a live-partial row): fishing in an open body pulls out one of its organs that
// surgery has reached; fished through a portal from anything else, a random organ. The var block is generated (D111).

// tg: code/modules/fishing/sources/subtypes/surgery.dm:3
public partial class datum_fish_source_surgery : datum_fish_source
{
    // tg: code/modules/fishing/sources/subtypes/surgery.dm:15
    public override async DmTask<object> spawn_reward(object reward_path = null, atom spawn_location = null, atom fishing_spot = null, params object[] __extra)
    {
        if (!Dm.eq(reward_path, FISHING_RANDOM_ORGAN))
            return await base.spawn_reward(reward_path, spawn_location, fishing_spot, __extra);

        if (istype(fishing_spot, typeof(obj_machinery_fishing_portal_generator)))
        {
            obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)fishing_spot;
            fishing_spot = portal.current_linked_atom;
        }

        if (!iscarbon(fishing_spot))
        {
            Type random_type = pick(Dm.sub(subtypesof(typeof(obj_item_organ)), GLOB.prototype_organs)) as Type;
            return await Dm.new_async(random_type, spawn_location);
        }

        mob_living_carbon carbon = (mob_living_carbon)fishing_spot;
        DmList possible_organs = Dm.list();
        foreach (obj_item_organ fishable in Dm.each<obj_item_organ>(carbon.organs))
        {
            if (Dm.truthy(Dm.bitand(fishable.organ_flags, Dm.bitor(Dm.bitor(ORGAN_UNREMOVABLE, ORGAN_EXTERNAL), ORGAN_VITAL))))
                continue;
            if (!LIMB_HAS_SURGERY_STATE(fishable.bodypart_owner, ALL_SURGERY_FISH_STATES(fishable.zone)))
                continue;
            possible_organs.Add(fishable);
        }

        if (!Dm.truthy(length(possible_organs)))
            return null;
        obj_item_organ chosen = (obj_item_organ)pick(possible_organs);
        await chosen.Remove(chosen.owner);
        await chosen.forceMove(spawn_location);
        return chosen;
    }
}
