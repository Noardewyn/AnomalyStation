// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/gatfruit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/gatfruit.dm:23
public partial class obj_item_food_grown_gatfruit : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/gatfruit.dm:33
    [DmParams("mapload", "new_seed")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_reagents holder = reagents as datum_reagents;
        holder.flags = Dm.bitand(Dm.num(holder.flags), ~INJECTABLE); //id rather not have this be filled with initropidril without effort
        return dot;
    }

    // tg: code/modules/hydroponics/grown/gatfruit.dm:37
    public override async DmTask<object> generate_trash(atom location = null)
    {
        //if you set this to anything but the revolver i will find you... and... downvote your pr...
        obj_item_gun_ballistic_revolver_peashooter gun = Dm.@new(trash_type, location ?? drop_location()) as obj_item_gun_ballistic_revolver_peashooter;
        float potency_percentage = CLAMP01(Dm.num(((obj_item_seeds)seed).potency) / 100);
        obj_item_ammo_box magazine = gun.magazine as obj_item_ammo_box;
        datum_reagents holder = reagents as datum_reagents;
        float amount_to_trans = Dm.num(holder.total_volume) / Dm.num(magazine.max_ammo);
        foreach (obj_item_ammo_casing_pea casing in Dm.each_cast<obj_item_ammo_casing_pea>(magazine.ammo_list()))
        {
            casing.damage = Dm.floor(Dm.max(5, LERP(5, Dm.num(casing.max_damage), potency_percentage)));
            if (Dm.truthy(holder.total_volume))
                await holder.trans_to(casing, amount_to_trans);
        }
        return gun;
    }
}
