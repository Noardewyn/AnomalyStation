// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/miniature/vim.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ## VIM!!!!!!!
 *
 * It's a teenie miniature mecha... for critters!
 * For the critters that cannot be understood, there is a sound creator in the mecha. It also has headlights.
 */
// tg: code/modules/vehicles/mecha/miniature/vim.dm:7
public partial class obj_vehicle_sealed_mecha_vim : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/miniature/vim.dm:55
    public override async DmTask<object> mob_try_enter(mob rider = null)
    {
        if (issilicon(rider))
        {
            rider.balloon_alert(rider, I18n.T("can't fit inside!"));
            return FALSE;
        }

        mob_living animal_or_basic = rider as mob_living;
        if (Dm.num(animal_or_basic.mob_size) > maximum_mob_size)
        {
            rider.balloon_alert(rider, I18n.T("can't fit inside!"));
            return FALSE;
        }

        await mob_enter(rider);
        await moved_inside(rider as mob_living);
        return null;
    }

    // tg: code/modules/vehicles/mecha/miniature/vim.dm:68
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_noise_chime), VEHICLE_CONTROL_DRIVE);
        initialize_controller_action_type(typeof(datum_action_vehicle_sealed_noise_buzz), VEHICLE_CONTROL_DRIVE);
    }

    // tg: code/modules/vehicles/mecha/miniature/vim.dm:73
    public override async DmTask toggle_lights(object forced_state = null, mob user = null)
    {
        await base.toggle_lights(forced_state, user);
        update_appearance();
    }

    // tg: code/modules/vehicles/mecha/miniature/vim.dm:77
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(mecha_flags, LIGHTS_ON)))
            Dm.append(ref dot, mutable_appearance(icon, "vim_headlights"));
        return dot;
    }
}
