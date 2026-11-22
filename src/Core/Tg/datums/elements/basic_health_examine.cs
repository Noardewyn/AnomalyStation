// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/basic_health_examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Adds a line of health condition to a basic mob's examine.
// tg: code/datums/elements/basic_health_examine.dm:3
public partial class datum_element_basic_health_examine : datum_element
{
    // tg: code/datums/elements/basic_health_examine.dm:12
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isbasicmob(target))
            return ELEMENT_INCOMPATIBLE;
        this.light_damage_message = Dm.arg(__extra, 0);
        this.heavy_damage_message = Dm.arg(__extra, 1);
        this.heavy_threshold = Dm.arg(__extra, 2);
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/basic_health_examine.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_EXAMINE);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/basic_health_examine.dm:25
    [SignalHandler]
    public virtual void on_examine(mob_living_basic source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.eq(source.health, source.maxHealth))
            return;
        if (Dm.num(source.health) < Dm.num(source.maxHealth) * Dm.num(heavy_threshold))
        {
            Dm.append(ref examine_list, span_danger(Dm.text("[]", heavy_damage_message)));
            return;
        }
        Dm.append(ref examine_list, span_danger(Dm.text("[]", light_damage_message)));
    }
}
