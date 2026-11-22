// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/changeling_power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the changeling power base (the purchase that grants it, the activation chain can_sting,
// sting_action, sting_feedback and the chemical cost, the requirements of DNA, absorptions, stat and stasis). The var block is
// generated (D111).

/*
 * Don't use the apostrophe in name or desc. Causes script errors.//probably no longer true
 */

// tg: code/modules/antagonists/changeling/changeling_power.dm:5
public partial class datum_action_changeling : datum_action
{
    /*
    changeling code now relies on on_purchase to grant powers.
    if you override it, MAKE SURE you call parent or it will not be usable
    the same goes for Remove(). if you override Remove(), call parent or else your power won't be removed on respec
    */

    // tg: code/modules/antagonists/changeling/changeling_power.dm:47
    public virtual async DmTask on_purchase(mob user = null, object is_respec = null)
    {
        await Grant(user);//how powers are added rather than the checks in mob.dm
    }

    // tg: code/modules/antagonists/changeling/changeling_power.dm:50
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        mob user = owner;
        if (user == null || !Dm.truthy(IS_CHANGELING(user)))
            return dot;
        await try_to_sting(user as mob_living);
        return dot;
    }

    /**
     *Contrary to the name, this proc isn't just used by changeling stings. It handles the activation of the action and the deducation of its cost.
     *The order of the proc chain is:
     *can_sting(). Should this fail, the process gets aborted early.
     *sting_action(). This proc usually handles the actual effect of the action.
     *Should sting_action succeed the following will be done:
     *sting_feedback(). Produces feedback on the performed action. Don't ask me why this isn't handled in sting_action()
     *The deduction of the cost of this power.
     *Returns TRUE on a successful activation.
     */
    // tg: code/modules/antagonists/changeling/changeling_power.dm:69
    public virtual async DmTask<object> try_to_sting(mob_living user = null, mob_living target = null)
    {
        if (!Dm.truthy(await can_sting(user, target)))
            return FALSE;
        if (Dm.truthy(disabled_by_fire) && Dm.truthy(user.fire_stacks) && Dm.truthy(user.on_fire))
        {
            user.balloon_alert(user, I18n.T("on fire!"));
            return FALSE;
        }
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (Dm.truthy(await sting_action(user, target)))
        {
            sting_feedback(user, target);
            changeling.adjust_chemicals(-chemical_cost);
            user.changeNext_move(CLICK_CD_MELEE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/antagonists/changeling/changeling_power.dm:83
    [ShouldCallParent]
    public virtual async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        SSblackbox.record_feedback("nested tally", "changeling_powers", 1, Dm.list(Dm.text("[]", name)));
        return FALSE;
    }

    // tg: code/modules/antagonists/changeling/changeling_power.dm:88
    public virtual object sting_feedback(mob_living user = null, mob_living target = null)
    {
        return FALSE;
    }

    // Fairly important to remember to return 1 on success >.< // Return TRUE not 1 >.<
    // tg: code/modules/antagonists/changeling/changeling_power.dm:92
    public virtual async DmTask<object> can_sting(mob_living user = null, mob_living target = null)
    {
        if (!Dm.truthy(can_be_used_by(user)))
            return FALSE;
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (changeling.chem_charges < chemical_cost)
        {
            user.balloon_alert(user, I18n.T("needs [] chemicals!", chemical_cost));
            return FALSE;
        }
        if (changeling.absorbed_count < req_dna)
        {
            user.balloon_alert(user, I18n.T("needs [] dna sample\\s!", req_dna));
            return FALSE;
        }
        if (changeling.true_absorbs < req_absorbs)
        {
            user.balloon_alert(user, I18n.T("needs [] absorption\\s!", req_absorbs));
            return FALSE;
        }
        if (req_stat < Dm.num(IS_UNCONSCIOUS_OR_CRIT(user)) || (req_stat == STABLE && IS_UNCONSCIOUS(user)))
        {
            user.balloon_alert(user, I18n.T("incapacitated!"));
            return FALSE;
        }
        if (HAS_TRAIT(user, TRAIT_DEATHCOMA) && !Dm.truthy(ignores_fakedeath))
        {
            user.balloon_alert(user, I18n.T("playing dead!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/changeling_power.dm:113
    public virtual object can_be_used_by(mob_living user = null)
    {
        if (QDELETED(user))
            return FALSE;
        if (!ishuman(user))
            return FALSE;
        if (Dm.truthy(req_human) && HAS_TRAIT(user, TRAIT_LESSER_HUMANOID))
        {
            user.balloon_alert(user, I18n.T("become human!"));
            return FALSE;
        }
        return TRUE;
    }
}
