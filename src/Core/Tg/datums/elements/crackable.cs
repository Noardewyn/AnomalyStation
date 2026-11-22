// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/crackable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the pressure tanks of the syndicate base). The var block is generated (D111).

/// Adds crack overlays to an object when integrity gets low
// tg: code/datums/elements/crackable.dm:2
public partial class datum_element_crackable : datum_element
{
    // tg: code/datums/elements/crackable.dm:9
    [DmParams("target", "crack_icon", "crack_states", "crack_integrity")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object crack_icon = Dm.arg(__extra, 0);
        DmList crack_states = Dm.arg<DmList>(__extra, 1);
        object crack_integrity = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, Dm.forward(__extra, crack_icon, crack_states, crack_integrity));
        if (!isobj(target))
            return ELEMENT_INCOMPATIBLE;
        this.crack_integrity = Dm.truthy(crack_integrity) ? Dm.num(crack_integrity) : this.crack_integrity;
        if (crack_appearances == null) // This is the first attachment and we need to do first time setup
        {
            crack_appearances = Dm.list();
            foreach (object state in Dm.each(crack_states))
            {
                for (int i = 1; i <= 35; i++)
                {
                    var crack = (mutable_appearance)mutable_appearance(crack_icon, Dm.str(state));
                    // crack.transform.Turn(i * 10): BYOND reads an appearance's transform as a copy, so tg's turn is lost and every crack
                    // stays unturned (as here)
                    Dm.append(ref crack_appearances, crack);
                }
            }
        }
        RegisterSignal(target, COMSIG_ATOM_INTEGRITY_CHANGED, nameof(IntegrityChanged));
        return dot;
    }

    // tg: code/datums/elements/crackable.dm:23
    [SignalHandler]
    public virtual void IntegrityChanged(obj source = null, object old_value = null, object new_value = null)
    {
        if (Dm.num(new_value) >= Dm.num(source.max_integrity) * crack_integrity)
            return;
        Waiting.NoSleep(source.AddComponent(typeof(datum_component_cracked), crack_appearances, crack_integrity));
    }
}
