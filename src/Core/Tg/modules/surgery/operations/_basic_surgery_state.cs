// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/_basic_surgery_state.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file.

/// Used to track the state of surgeries on a mob generically rather than a bodypart
// tg: code/modules/surgery/operations/_basic_surgery_state.dm:2
public partial class datum_status_effect_basic_surgery_state : datum_status_effect
{
    // tg: code/modules/surgery/operations/_basic_surgery_state.dm:11
    [DmParams("new_owner", "added_state", "removed_state")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object added_state = Dm.arg(__extra, 0) ?? NONE;
        object removed_state = Dm.arg(__extra, 1) ?? NONE;
        object dot = await base.on_creation(new_owner, Dm.forward(__extra, added_state, removed_state));
        surgery_state = Dm.bitand(Dm.num(added_state), Dm.bitnot(Dm.num(removed_state)));
        // SEND_SIGNAL(owner, COMSIG_LIVING_UPDATING_SURGERY_STATE, NONE, surgery_state, added_state) (rules 11.1 (e))
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_UPDATING_SURGERY_STATE]))
            owner._SendSignal(COMSIG_LIVING_UPDATING_SURGERY_STATE, Dm.list(owner, NONE, surgery_state, added_state));
        return dot;
    }

    // tg: code/modules/surgery/operations/_basic_surgery_state.dm:16
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (Dm.truthy(owner.has_limbs))
            stack_trace(Dm.text("Applied a basic surgery state to [], which has limbs. This status effect is intended for limbless mobs.", owner), "code/modules/surgery/operations/_basic_surgery_state.dm", 19);
        return dot;
    }

    // tg: code/modules/surgery/operations/_basic_surgery_state.dm:21
    public override object get_examine_text(mob examiner = null)
    {
        if (HAS_SURGERY_STATE(surgery_state, SURGERY_SKIN_OPEN))
            return I18n.T("[] skin is open[].", owner.p_Their(), HAS_SURGERY_STATE(surgery_state, SURGERY_BONE_SAWED) ? I18n.T(" and bones are sawed opened") : "");
        // other states are not yet supported
        stack_trace(Dm.text("Surgery state holder had unsupported state(s): []", jointext(bitfield_to_list(surgery_state, SURGERY_STATE_READABLE()), ", ")), "code/modules/surgery/operations/_basic_surgery_state.dm", 25);
        return null;
    }

    // tg: code/modules/surgery/operations/_basic_surgery_state.dm:28
    [DmParams("old_owner", "added_state", "removed_state")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object added_state = Dm.arg(__extra, 0) ?? NONE;
        object removed_state = Dm.arg(__extra, 1) ?? NONE;
        float old_state = surgery_state;
        surgery_state = Dm.bitor(surgery_state, Dm.num(added_state));
        surgery_state = Dm.bitand(surgery_state, Dm.bitnot(Dm.num(removed_state)));
        // SEND_SIGNAL(owner, COMSIG_LIVING_UPDATING_SURGERY_STATE, old_state, surgery_state, added_state | removed_state)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_UPDATING_SURGERY_STATE]))
            owner._SendSignal(COMSIG_LIVING_UPDATING_SURGERY_STATE, Dm.list(owner, old_state, surgery_state, Dm.bitor(Dm.num(added_state), Dm.num(removed_state))));
        if (!Dm.truthy(surgery_state))
            qdel(this);
    }
}
