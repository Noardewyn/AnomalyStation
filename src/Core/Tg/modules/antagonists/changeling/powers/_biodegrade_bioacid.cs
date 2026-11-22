// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/_biodegrade_bioacid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the adaptive bio-acid that Biodegrade sprays on a captor (changelings neutralize it). The var
// block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/_biodegrade_bioacid.dm:1
public partial class datum_reagent_toxin_acid_bio_acid : datum_reagent_toxin_acid
{
    // tg: code/modules/antagonists/changeling/powers/_biodegrade_bioacid.dm:12
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        if (Dm.truthy(IS_CHANGELING(exposed_mob)))
        {
            to_chat(exposed_mob, span_changeling(I18n.T("We excrete a bio-agent to neutralize the bio-acid. It is routine and reflexive to do so.")));
            volume = min(0.1f, Dm.num(volume));
            await holder.update_total();
            return;
        }
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        await exposed_mob.adjust_fire_loss(round(Dm.num(reac_volume) * min(1 - Dm.num(touch_protection)), 0.1f) * 3, required_bodytype: BODYTYPE_ORGANIC); // full bio protection = 100% damage reduction
        await exposed_mob.acid_act(10, 50);
    }

    // tg: code/modules/antagonists/changeling/powers/_biodegrade_bioacid.dm:22
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        if (SPT_PROB(50, seconds_per_tick))
            await ((mob_living_carbon)affected_mob).emote(pick("screech", "cry"));
        return dot;
    }
}
