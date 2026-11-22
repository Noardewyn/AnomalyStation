// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/targeting_strategies/playable_synthesizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4): the instrument itself stays inert (a row).

// tg: code/datums/ai/basic_mobs/targeting_strategies/playable_synthesizer.dm:2
public partial class datum_targeting_strategy_playable_synthesizer : datum_targeting_strategy
{
    // tg: code/datums/ai/basic_mobs/targeting_strategies/playable_synthesizer.dm:4
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;

        if (target.GetType() != typeof(obj_item_instrument_piano_synth))
            return FALSE;

        if (!isturf(target.loc))
            return FALSE;

        // shortcut: instruments and their songs are inert, so the mouse's tree must not walk to one and block on the inert leaves; drop this line when instruments are live.
        if ((target as obj_item_instrument).song == null)
            return FALSE;

        return can_see(living_mob, target, Dm.num(vision_range));
    }
}
