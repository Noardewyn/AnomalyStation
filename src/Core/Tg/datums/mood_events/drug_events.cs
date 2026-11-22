// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/drug_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var blocks are generated (D111).

// tg: code/datums/mood_events/drug_events.dm:9
public partial class datum_mood_event_maintenance_high : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:14
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        float value = rand(-1, 6); // chance for it to suck
        mood_change = value;
        if (value < 0)
            description = I18n.T("No! Don't! My gloves! Auuuuurgh!");
        else
            description = Dm.initial(this, (datum_mood_event p) => p.description);
    }
}

// tg: code/datums/mood_events/drug_events.dm:37
public partial class datum_mood_event_overdose : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:41
    [DmParams("drug_name")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object drug_name = Dm.arg(__extra, 0);
        description = I18n.T("I think I took a bit too much of that []!", drug_name);
    }
}

// tg: code/datums/mood_events/drug_events.dm:44
public partial class datum_mood_event_withdrawal_light : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:47
    [DmParams("drug_name")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object drug_name = Dm.arg(__extra, 0);
        description = I18n.T("I could use some []...", drug_name);
    }
}

// tg: code/datums/mood_events/drug_events.dm:50
public partial class datum_mood_event_withdrawal_medium : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:53
    [DmParams("drug_name")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object drug_name = Dm.arg(__extra, 0);
        description = I18n.T("I really need [].", drug_name);
    }
}

// tg: code/datums/mood_events/drug_events.dm:56
public partial class datum_mood_event_withdrawal_severe : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:59
    [DmParams("drug_name")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object drug_name = Dm.arg(__extra, 0);
        description = I18n.T("Oh god, I need some of that []!", drug_name);
    }
}

// tg: code/datums/mood_events/drug_events.dm:110
public partial class datum_mood_event_eigentrip : datum_mood_event
{
    // tg: code/datums/mood_events/drug_events.dm:108  #define EIGENTRIP_MOOD_RANGE 10 (file-local, #undef at :123)
    private const float EIGENTRIP_MOOD_RANGE = 10;

    // tg: code/datums/mood_events/drug_events.dm:115
    [DmParams("param")]
    public override async DmTask add_effects(params object[] __extra)
    {
        float value = rand(-EIGENTRIP_MOOD_RANGE, EIGENTRIP_MOOD_RANGE);
        mood_change = value;
        if (value < 0)
            description = I18n.T("I swapped places with an alternate reality version of myself! I want to go home!");
        else
            description = I18n.T("I swapped places with an alternate reality version of myself! Though, this place is much better than my old life.");
    }
}
