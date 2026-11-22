// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/floor_is_lava.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file, the wizard's "the floor is lava": it burns the players standing on a bare floor. The var block is
// generated (D111).

//Causes fire damage to anyone not standing on a dense object.
// tg: code/datums/weather/weather_types/floor_is_lava.dm:2
public partial class datum_weather_floor_is_lava : datum_weather
{
    // tg: code/datums/weather/weather_types/floor_is_lava.dm:30
    public override object can_weather_act_mob(mob_living mob_to_check = null)
    {
        if (mob_to_check.client == null) //Only sentient people are going along with it!
            return FALSE;
        object dot = base.can_weather_act_mob(mob_to_check);
        if (!Dm.truthy(dot) || issilicon(mob_to_check) || mob_to_check.buckled is obj_structure_bed)
            return FALSE;
        turf mob_turf = get_turf(mob_to_check);
        if (Dm.truthy(mob_turf.density)) //Walls are not floors.
            return FALSE;
        foreach (obj_structure structure_to_check in Dm.each<obj_structure>(mob_turf.contents))
        {
            if (Dm.truthy(structure_to_check.density))
                return FALSE;
        }
        if (Dm.truthy(Dm.bitand(mob_to_check.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return FALSE;
        return dot;
    }

    // tg: code/datums/weather/weather_types/floor_is_lava.dm:46
    public override async DmTask weather_act_mob(mob_living victim = null)
    {
        await victim.adjust_fire_loss(3);
        await base.weather_act_mob(victim);
    }
}
