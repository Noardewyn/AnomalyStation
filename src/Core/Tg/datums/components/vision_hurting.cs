// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/vision_hurting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6): the whole file, the stage six singularity's glare. The var block is generated (D111).

/// A component that damages eyes that look at the owner
// tg: code/datums/components/vision_hurting.dm:2
public partial class datum_component_vision_hurting : datum_component
{
    // tg: code/datums/components/vision_hurting.dm:8
    [DmParams("damage_per_second", "message", "silent")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object damage_per_second = Dm.arg(__extra, 0);
        object message = Dm.arg(__extra, 1);
        object silent = Dm.arg(__extra, 2);
        damage_per_second ??= 1;
        message ??= "Your eyes burn as you look at";
        silent ??= FALSE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.damage_per_second = damage_per_second;
        this.message = message;
        this.silent = silent;

        START_PROCESSING(SSdcs, this);
        return null;
    }

    // tg: code/datums/components/vision_hurting.dm:19  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        foreach (mob_living_carbon viewer in Dm.each<mob_living_carbon>(viewers(parent)))
        {
            if (Dm.truthy(viewer.is_blind()) || Dm.ge(viewer.get_eye_protection(), damage_per_second))
                continue;
            obj_item_organ_eyes burning_orbs = Dm.locate<obj_item_organ_eyes>(viewer.organs);
            if (!Dm.truthy(burning_orbs))
                continue;
            await burning_orbs.apply_organ_damage(Dm.mul(damage_per_second, seconds_per_tick));
            if (Dm.truthy(silent))
                return null;
            if (SPT_PROB(50, seconds_per_tick))
                to_chat(viewer, span_userdanger(I18n.T("[] []!", message, parent)));
            if (SPT_PROB(20, seconds_per_tick))
            {
                await viewer.emote("scream");
            }
        }
        return null;
    }
}
