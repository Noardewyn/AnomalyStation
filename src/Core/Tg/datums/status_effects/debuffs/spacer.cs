// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/spacer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps MS1): the low gravity planet mood event's procs; the spacer status effects come with the spacer quirk (owner-deferred,
// D332). The var blocks are generated (D111).

// tg: code/datums/status_effects/debuffs/spacer.dm:131
public partial class datum_mood_event_spacer_on_planet_low_grav : datum_mood_event_spacer_on_planet
{
    // tg: code/datums/status_effects/debuffs/spacer.dm:135
    public override async DmTask add_effects(params object[] __extra)
    {
        await base.add_effects(__extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(lower_mood_bonus)), 5 * MINUTES, TIMER_DELETE_ME, file: "code/datums/status_effects/debuffs/spacer.dm", line: 137);
    }

    // tg: code/datums/status_effects/debuffs/spacer.dm:139
    public virtual void lower_mood_bonus()
    {
        mood_change -= 1;
        owner.mob_mood.update_mood();
        if (mood_change > 1)
            _addtimer(Dm.@new<datum_callback>(this, nameof(lower_mood_bonus)), 5 * MINUTES, TIMER_DELETE_ME, file: "code/datums/status_effects/debuffs/spacer.dm", line: 143);
    }
}
