// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plundering_attacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file (the pirate troopers' melee): a hit takes credits from the target's ID account, dropped as a
// holochip on death. The var block is generated (D111).

/**
 * Component that makes basic mobs' melee attacks steal money from the target's ID card.
 * Plundered money is stored and dropped on death or removal of the component.
 */
// tg: code/datums/components/plundering_attacks.dm:5
public partial class datum_component_plundering_attacks : datum_component
{
    // tg: code/datums/components/plundering_attacks.dm:11
    [DmParams("plunder_amount")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object plunder_amount = Dm.arg<object>(__extra, 0) ?? 25;
        object dot = await base.Initialize(__extra);
        if (!isbasicmob(parent))
            return COMPONENT_INCOMPATIBLE;
        this.plunder_amount = Dm.num(plunder_amount);
        return dot;
    }

    // tg: code/datums/components/plundering_attacks.dm:19
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_HOSTILE_POST_ATTACKINGTARGET, nameof(attempt_plunder));
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(drop_plunder));
    }

    // tg: code/datums/components/plundering_attacks.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_HOSTILE_POST_ATTACKINGTARGET, COMSIG_LIVING_DEATH));
        drop_plunder();
    }

    // tg: code/datums/components/plundering_attacks.dm:32
    public virtual void attempt_plunder(mob_living attacker = null, mob_living_carbon_human target = null, object success = null)
    {
        // SIGNAL_HANDLER
        if (target == null || !Dm.truthy(success))
            return;
        var id_card = (target.wear_id as obj_item)?.GetID() as obj_item_card_id;
        if (id_card == null)
            return;
        var account_to_rob = id_card.registered_account as datum_bank_account;
        if (account_to_rob == null || Dm.num(account_to_rob.account_balance) == 0)
            return;
        float amount_to_steal = plunder_amount;
        if (Dm.num(account_to_rob.account_balance) < plunder_amount) //If there isn't enough, just take what's left
            amount_to_steal = Dm.num(account_to_rob.account_balance);
        plunder_stored += amount_to_steal;
        account_to_rob.adjust_money(-amount_to_steal);
        Waiting.Detach(account_to_rob.bank_card_talk(I18n.T("Transaction confirmed! Transferred [] [] to <NULL_ACCOUNT>!", amount_to_steal, MONEY_NAME)));
    }

    // tg: code/datums/components/plundering_attacks.dm:50
    public virtual void drop_plunder()
    {
        // SIGNAL_HANDLER
        if (plunder_stored == 0)
            return;
        Dm.@new<obj_item_holochip>(get_turf(parent as atom), plunder_stored);
        plunder_stored = 0;
    }
}
