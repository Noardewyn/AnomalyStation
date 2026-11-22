// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pricetag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file. The var block is generated (D111).

/*
 * Pricetag component.
 *
 * Used when exporting items via the cargo system.
 * Gives a cut of the profit to one or multiple bank accounts.
 */
// tg: code/datums/components/pricetag.dm:7
public partial class datum_component_pricetag : datum_component
{
    // tg: code/datums/components/pricetag.dm:14
    [DmParams("pay_to_account", "profit_ratio", "delete_on_unwrap")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        var pay_to_account = Dm.arg(__extra, 0) as DmList;
        float profit_ratio = Dm.num(Dm.arg(__extra, 1) ?? 1f);
        object delete_on_unwrap = Dm.arg(__extra, 2) ?? TRUE;
        if (!isobj(parent)) //Has to account for both objects and sellable structures like crates.
            return COMPONENT_INCOMPATIBLE;

        if (pay_to_account == null)
        {
            stack_trace(Dm.text("[] component was added to something without a pay_to_account list!", type), "code/datums/components/pricetag.dm", 19);
            return COMPONENT_INCOMPATIBLE;
        }

        if (!Dm.truthy(length(pay_to_account)))
        {
            stack_trace(Dm.text("[] component was created with an empty pay_to_account list!", type), "code/datums/components/pricetag.dm", 23);
            return COMPONENT_INCOMPATIBLE;
        }

        float total_contribution = 0;
        foreach (object totaler in Dm.each(pay_to_account))
        {
            if (!Dm.truthy(pay_to_account[totaler]))
                pay_to_account[totaler] = 1f; //In case we just want a list with one item to prevent things from getting fucked up.

            total_contribution += Dm.num(pay_to_account[totaler]);
        }

        foreach (object individual in Dm.each(pay_to_account))
        {
            float individual_cut = profit_ratio * (Dm.num(pay_to_account[individual]) / total_contribution);
            payees[individual] = individual_cut;
        }

        this.delete_on_unwrap = Dm.num(delete_on_unwrap);
        return null;
    }

    // tg: code/datums/components/pricetag.dm:38
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ITEM_EXPORTED, nameof(on_parent_sold));
        // Register this regardless of delete_on_unwrap because it could change by inherited components.
        RegisterSignal(parent, COMSIG_ITEM_UNWRAPPED, nameof(on_parent_unwrap));
    }

    // tg: code/datums/components/pricetag.dm:43
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EXPORTED, COMSIG_ITEM_UNWRAPPED));
        await base.UnregisterFromParent(__extra);
    }

    /*
     * Inheriting an incoming / new version of price tag:
     *
     * If the account passed in the incoming version is already in our list,
     * only override it if the ratio is better for the payee
     *
     * If the account passed in the incoming version is not in our list, add it like normal.
     *
     * If the incoming version shouldn't delete when unwrapped,
     * our version shouldn't either.
     * We don't care about the other way around
     * (Don't go from non-deleting to deleting)
     */
    // tg: code/datums/components/pricetag.dm:62
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        object pay_to_account = Dm.arg(__extra, 0);
        float profit_ratio = Dm.num(Dm.arg(__extra, 1) ?? 1f);
        object delete_on_unwrap = Dm.arg(__extra, 2) ?? TRUE;
        if (length(payees) == 1)
        {
            if (pay_to_account is DmList accounts)
            {
                // DEVIATION(DV410): tg keys the payees by the list itself (the bounty cube's handling tip, `universal_scanner.dm:236`),
                // and the sale's payout then runtimes on it; each account of the list is a payee at the ratio
                foreach (object account in Dm.each(accounts.Copy()))
                {
                    if (payees[account] != null && Dm.num(payees[account]) >= profit_ratio) // They're already getting a better ratio, don't scam them
                        continue;
                    payees[account] = profit_ratio;
                }
            }
            else
            {
                if (payees[pay_to_account] != null && Dm.num(payees[pay_to_account]) >= profit_ratio) // They're already getting a better ratio, don't scam them
                    return;

                payees[pay_to_account] = profit_ratio;
            }
        }
        if (!Dm.truthy(delete_on_unwrap))
            this.delete_on_unwrap = Dm.num(delete_on_unwrap);
    }

    /*
     * Signal proc for [COMSIG_STRUCTURE_UNWRAPPED] and [COMSIG_ITEM_UNWRAPPED].
     *
     * Once it leaves its wrapped container,
     * the parent should loses its pricetag component
     * (if delete_on_unwrap is TRUE)
     */
    // tg: code/datums/components/pricetag.dm:79
    public virtual void on_parent_unwrap(obj source = null)
    {
        if (!Dm.truthy(delete_on_unwrap))
            return;

        qdel(this);
    }

    /*
     * Signal proc for [COMSIG_ITEM_EXPORTED].
     *
     * Pays out money to everyone in the payees list.
     */
    // tg: code/datums/components/pricetag.dm:92
    public virtual object on_parent_sold(obj source = null, datum_export export = null, datum_export_report report = null, object item_value = null)
    {
        if (!isnum(item_value))
            return null;

        // Gotta see how much money we've lost by the end of things.
        float overall_item_price = Dm.num(item_value);
        float running_tally = 0;

        foreach (datum_bank_account payee in Dm.each_cast<datum_bank_account>(payees))
        {
            // Every payee with a ratio gets a cut based on the item's total value
            float payee_cut = round(Dm.num(item_value) * Dm.num(payees[payee]));
            // And of course, the cut is removed from what cargo gets. (But not below zero, just in case)
            overall_item_price = Math.Max(0, overall_item_price - payee_cut);

            payee.adjust_money(payee_cut, I18n.T("Pricetag: [] Sale", capitalize(format_text(source.name))));
            Waiting.NoSleep(payee.bank_card_talk(I18n.T("Sale of [] recorded. [] [] added to account.", source, payee_cut, MONEY_NAME)));
            running_tally += payee_cut;
        }
        // Update the report with the modified final price
        report.total_value[export] = Dm.add(report.total_value[export], overall_item_price);
        report.total_amount[export] = Dm.add(report.total_amount[export], Dm.num(export.get_amount(source)) * export.amount_report_multiplier);

        // And ensure we don't double-add to the report
        return COMPONENT_STOP_EXPORT_REPORT;
    }
}
