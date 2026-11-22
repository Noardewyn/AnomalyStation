// Translated from tgstation (snapshot 1d99bd7f): code/datums/datumvars.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with the admin tooling (the VV, view variables, procs: not translated in M0, translation-rules.md
// 13). Only the slot roots that /datum/weakref overrides are declared, with the signatures of
// docs/ai/specs/generated/slots.tsv.

public partial class datum
{
    /// Called when a var is edited with the new value to change to
    // tg: code/datums/datumvars.dm:15
    public virtual async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, "vars"))
            return FALSE;
        Dm.set_var(this, Dm.str(var_name), var_value); // vars[var_name] = var_value
        datum_flags = Dm.bitor(datum_flags, DF_VAR_EDITED);
        return TRUE;
    }

    /**
     * Gets all the dropdown options in the vv menu.
     * When overriding, make sure to call . = ..() first and append to the result, that way parent items are always at the top and child items are further down.
     * Add separators by doing VV_DROPDOWN_OPTION("", "---")
     */
    // tg: code/datums/datumvars.dm:37
    [ShouldCallParent]
    public virtual object vv_get_dropdown()
    {
        Dm.NotTranslated("code/datums/datumvars.dm:37", "vv_get_dropdown: VV is admin tooling (translation-rules.md 13)");
        return default;
    }

    /**
     * This proc is only called if everything topic-wise is verified. The only verifications that should happen here is things like permission checks!
     * href_list is a reference, modifying it in these procs WILL change the rest of the proc in topic.dm of admin/view_variables!
     * This proc is for "high level" actions like admin heal/set species/etc/etc. The low level debugging things should go in admin/view_variables/topic_basic.dm in case this runtimes.
     */
    // tg: code/datums/datumvars.dm:56
    public virtual async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        Dm.NotTranslated("code/datums/datumvars.dm:56", "vv_do_topic: VV is admin tooling (translation-rules.md 13)");
        return default;
    }
}
