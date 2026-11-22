// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/phobia.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the phobias; the bananium nuke leaves everyone afraid of clowns). The phobias' subtypes are their
// generated var blocks (D111).

// tg: code/datums/brain_damage/phobia.dm:1
public partial class datum_brain_trauma_mild_phobia : datum_brain_trauma_mild
{
    // tg: code/datums/brain_damage/phobia.dm:18
    [DmParams("new_phobia_type")]
    public override async DmTask New(params object[] __extra)
    {
        object new_phobia_type = Dm.arg(__extra, 0);
        if (Dm.truthy(new_phobia_type))
            phobia_type = new_phobia_type;

        if (!Dm.truthy(phobia_type))
            phobia_type = pick(GLOB.phobia_types);

        gain_text = span_warning(I18n.T("You start finding [] very unnerving...", phobia_type));
        lose_text = span_notice(I18n.T("You no longer feel afraid of [].", phobia_type));
        scan_desc += Dm.text(" of []", phobia_type);
        await base.New(__extra);
    }

    // tg: code/datums/brain_damage/phobia.dm:30
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        var fear = await owner.AddComponentFrom(REF(this), typeof(datum_component_fearful), Dm.list(typeof(datum_terror_handler_startle))) as datum_component_fearful;
        var phobia = fear.add_handler(terror_handler, REF(this)) as datum_terror_handler_phobia_source;
        phobia.trigger_regex = GLOB.phobia_regexes[phobia_type] as regex;
        phobia.trigger_mobs = GLOB.phobia_mobs[phobia_type] as DmList;
        phobia.trigger_objs = GLOB.phobia_objs[phobia_type] as DmList;
        phobia.trigger_turfs = GLOB.phobia_turfs[phobia_type] as DmList;
        phobia.trigger_species = GLOB.phobia_species[phobia_type] as DmList;
        phobia.mood_event_type = mood_event_type;
        return dot;
    }

    // tg: code/datums/brain_damage/phobia.dm:41
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        owner.RemoveComponentSource(REF(this), typeof(datum_component_fearful));
    }
}
