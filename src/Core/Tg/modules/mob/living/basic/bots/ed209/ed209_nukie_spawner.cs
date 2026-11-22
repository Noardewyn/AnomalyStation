// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/ed209/ed209_nukie_spawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the uplink's Syndicate ED-209 drops in a pod). The var block is generated (D111).

// tg: code/modules/mob/living/basic/bots/ed209/ed209_nukie_spawner.dm:1
public partial class obj_item_antag_spawner_nuke_ops_ed209_nukie : obj_item_antag_spawner_nuke_ops
{
    // tg: code/modules/mob/living/basic/bots/ed209/ed209_nukie_spawner.dm:6
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(check_usability(user)))
            return null;

        to_chat(user, span_notice(I18n.T("You activate [] and wait for confirmation.", this)));
        await drop_bot();
        do_sparks(4, TRUE, this);
        qdel(this);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209_nukie_spawner.dm:15
    public virtual async DmTask drop_bot()
    {
        var nuclear_bot = Dm.@new<mob_living_basic_bot_secbot_ed209_nukie>();
        obj_structure_closet_supplypod pod = setup_pod();
        await nuclear_bot.forceMove(pod);
        Dm.@new<obj_effect_pod_landingzone>(spawn_location ?? get_turf(this), pod);
    }
}
