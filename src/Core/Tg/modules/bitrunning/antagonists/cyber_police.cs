// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/cyber_police.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the cyber police, the glitch that hunts the bitrunners with the sleeping carp, and its outfit. The var blocks are
// generated.

// tg: code/modules/bitrunning/antagonists/cyber_police.dm:1
public partial class datum_antagonist_bitrunning_glitch_cyber_police : datum_antagonist_bitrunning_glitch
{
    // tg: code/modules/bitrunning/antagonists/cyber_police.dm:5
    public override async DmTask on_gain()
    {
        await base.on_gain();

        if (!ishuman(owner.current))
        {
            stack_trace("humans only for this position", "code/modules/bitrunning/antagonists/cyber_police.dm", 9);
            return;
        }

        await convert_agent();

        datum_martial_art_the_sleeping_carp carp = Dm.@new<datum_martial_art_the_sleeping_carp>(this);
        await carp.teach(owner.current as mob_living);
    }
}

// tg: code/modules/bitrunning/antagonists/cyber_police.dm:17
public partial class datum_outfit_cyber_police : datum_outfit
{
    // tg: code/modules/bitrunning/antagonists/cyber_police.dm:26
    public override async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        if (!Dm.truthy(visuals_only))
            return;

        user.set_facial_hairstyle("Shaved", update: FALSE);
        user.set_haircolor("#4B3D28", update: FALSE);
        user.set_hairstyle("Business Hair");
        await DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/antagonists/cyber_police.dm:34
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        obj_item_clothing_under officer_uniform = user.w_uniform as obj_item_clothing_under;
        if (officer_uniform is obj_item_clothing_under)
        {
            officer_uniform.set_has_sensor(NO_SENSORS);
            officer_uniform.set_sensor_mode(SENSOR_OFF);
        }
        await DmTask.CompletedTask;
    }
}
