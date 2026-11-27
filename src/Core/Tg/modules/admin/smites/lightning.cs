// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/smites/lightning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only lightningbolt translated (the chapel: the honorbound's and the sparring god's punishments); the admin smites come with
// the admin tools.

public static partial class GlobalProcs
{
    ///this is the actual bolt effect and damage, made into its own proc because it is used elsewhere
    // tg: code/modules/admin/smites/lightning.dm:11
    public static async DmTask lightningbolt(mob_living user = null)
    {
        turf lightning_source = get_step(get_step(user, NORTH), NORTH) as turf;
        lightning_source.Beam(user, icon_state: Dm.text("lightning[]", rand(1, 12)), time: 5);
        await user.adjust_fire_loss(LIGHTNING_BOLT_DAMAGE);
        playsound(get_turf(user), Dm.resource("sound/effects/magic/lightningbolt.ogg"), 50, TRUE);
        if (ishuman(user))
        {
            mob_living_carbon_human human_target = user as mob_living_carbon_human;
            await human_target.electrocution_animation(LIGHTNING_BOLT_ELECTROCUTION_ANIMATION_LENGTH);
        }
    }
}
