// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/radiation_protected_clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file.

/// Marks the item as being radiation protected.
/// Adds the TRAIT_RADIATION_PROTECTED_CLOTHING trait, as well as adding an
/// extra bit to the examine descrpition.
// tg: code/datums/elements/radiation_protected_clothing.dm:4
public partial class datum_element_radiation_protected_clothing : datum_element
{
    // tg: code/datums/elements/radiation_protected_clothing.dm:6
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isclothing(target))
            return ELEMENT_INCOMPATIBLE;

        ADD_TRAIT(target, TRAIT_RADIATION_PROTECTED_CLOTHING, REF(this));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/radiation_protected_clothing.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        REMOVE_TRAIT(source, TRAIT_RADIATION_PROTECTED_CLOTHING, REF(this));
        UnregisterSignal(source, COMSIG_ATOM_EXAMINE);

        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/radiation_protected_clothing.dm:21
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        // SIGNAL_HANDLER

        Dm.append(ref examine_text, span_notice(I18n.T("A patch with a hazmat sign on the side suggests it would <b>protect you from radiation</b>.")));
    }
}
