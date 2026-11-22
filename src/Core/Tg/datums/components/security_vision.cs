// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/security_vision.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/// This component allows you to judge someone's level of criminal activity by examining them
// tg: code/datums/components/security_vision.dm:2
public partial class datum_component_security_vision : datum_component
{
    // tg: code/datums/components/security_vision.dm:8
    [DmParams("judgement_criteria", "update_judgement_criteria")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object judgement_criteria = Dm.arg(__extra, 0);
        datum_callback update_judgement_criteria = Dm.arg<datum_callback>(__extra, 1);
        await base.Initialize(__extra);
        if (!ismob(parent))
            return COMPONENT_INCOMPATIBLE;
        this.judgement_criteria = judgement_criteria;
        this.update_judgement_criteria = update_judgement_criteria;
        return null;
    }

    // tg: code/datums/components/security_vision.dm:15
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOB_EXAMINING, nameof(on_examining));
    }

    // tg: code/datums/components/security_vision.dm:18
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOB_EXAMINING);
    }

    /// When we examine something, check if we have any extra data to add
    // tg: code/datums/components/security_vision.dm:22
    [SignalHandler]
    public virtual void on_examining(mob source = null, atom target = null, DmList examine_strings = null, DmList examine_overrides = null)
    {
        if (target is not mob_living perp)
            return;
        object updated = update_judgement_criteria == null ? null : Waiting.NoSleep(update_judgement_criteria.Invoke());
        judgement_criteria = Dm.truthy(updated) ? updated : judgement_criteria;

        float threat_level = Dm.num(perp.assess_threat(judgement_criteria));
        if (threat_level >= THREAT_ASSESS_MAXIMUM)
            Dm.append(ref examine_strings, span_boldwarning(Dm.text("Assessed threat level of []! Extreme danger of criminal activity!", threat_level)));
        else if (threat_level >= THREAT_ASSESS_DANGEROUS)
            Dm.append(ref examine_strings, span_warning(Dm.text("Assessed threat level of []. Criminal scum detected!", threat_level)));
        else if (threat_level >= 1)
            Dm.append(ref examine_strings, span_notice(Dm.text("Assessed threat level of []. Probably not dangerous... yet.", threat_level)));
        else
            Dm.append(ref examine_strings, span_notice("Seems to be a trustworthy individual."));
    }
}
