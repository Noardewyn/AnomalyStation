// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/economy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the departmental budgets, the paydays, the inflation that moves the vendors' prices, the audit log
// and the mail count. The earnings report, with the stock market's news (gaps P4b), goes on the newscaster (silicons SI6b). The var
// blocks are generated.

// tg: code/controllers/subsystem/economy.dm:1  SUBSYSTEM_DEF(economy)
public partial class datum_controller_subsystem_economy : datum_controller_subsystem
{
    // Processing step defines, to track what we've done so far
    // tg: code/controllers/subsystem/economy.dm:88  (#undef at :299)
    private const string ECON_DEPARTMENT_STEP = "econ_dpt_stp";
    // tg: code/controllers/subsystem/economy.dm:89
    private const string ECON_ACCOUNT_STEP = "econ_act_stp";
    // tg: code/controllers/subsystem/economy.dm:90
    private const string ECON_PRICE_UPDATE_STEP = "econ_prc_stp";

    // The New() that SUBSYSTEM_DEF(economy) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSeconomy) (__DEFINES/MC.dm:29) expanded
    // inline (it reads src).
    // tg: code/controllers/subsystem/economy.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSeconomy != this)
        {
            if (SSeconomy is datum_controller_subsystem_economy)
            {
                Recover();
                qdel(SSeconomy);
            }
            SSeconomy = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/economy.dm:70
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        //removes cargo from the split
        float budget_to_hand_out = round(budget_pool / department_accounts.len - 1);
        if (Dm.eq(time2text(world.timeofday, "DDD"), SUNDAY))
            mail_blocked = TRUE;
        foreach (object dep_id in Dm.each(department_accounts))
        {
            if (Dm.eq(dep_id, ACCOUNT_CAR)) //cargo starts with NOTHING
            {
                Dm.@new<datum_bank_account_department>(dep_id, 0, Dm.named("player_account", FALSE));
                continue;
            }
            Dm.@new<datum_bank_account_department>(dep_id, budget_to_hand_out, Dm.named("player_account", FALSE));
        }
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/economy.dm:82
    public override void Recover()
    {
        departmental_accounts = SSeconomy.departmental_accounts;
        bank_accounts_by_id = SSeconomy.bank_accounts_by_id;
        dep_cards = SSeconomy.dep_cards;
    }

    // tg: code/controllers/subsystem/economy.dm:92
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        float seconds_per_tick = wait / (5 * MINUTES);
        if (!Dm.truthy(resumed))
        {
            temporary_total = 0;
            processing_part = ECON_DEPARTMENT_STEP;
            cached_processing = department_accounts.Copy();
        }

        if (Dm.eq(processing_part, ECON_DEPARTMENT_STEP))
        {
            if (!Dm.truthy(departmental_payouts()))
                return;

            processing_part = ECON_ACCOUNT_STEP;
            cached_processing = bank_accounts_by_id.Copy();
            station_total = 0;
            station_target_buffer += STATION_TARGET_BUFFER;
        }

        if (Dm.eq(processing_part, ECON_ACCOUNT_STEP))
        {
            if (!Dm.truthy(await issue_paydays()))
                return;

            processing_part = ECON_PRICE_UPDATE_STEP;
            station_target = max(round(temporary_total / max(bank_accounts_by_id.len * 2, 1)) + station_target_buffer, 1);
        }

        if (Dm.eq(processing_part, ECON_PRICE_UPDATE_STEP))
        {
            update_boosted_exports();
            if (!HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING) && !Dm.truthy(await price_update()))
                return;
        }

        if (times_fired % ticks_per_mail == 0)
        {
            float effective_mailcount = round(living_player_count() / (inflation_value - 0.5f)); //More mail at low inflation, and vis versa.
            mail_waiting += clamp(effective_mailcount, 1, ticks_per_mail * MAX_MAIL_PER_MINUTE * seconds_per_tick);
        }

        SSstock_market.news_string = "";
    }

    /**
     * Handy proc for obtaining a department's bank account, given the department ID, AKA the define assigned for what department they're under.
     */
    // tg: code/controllers/subsystem/economy.dm:129
    public virtual datum_bank_account_department get_dep_account(object dep_id = null)
    {
        foreach (datum_bank_account_department D in Dm.each<datum_bank_account_department>(departmental_accounts))
        {
            if (Dm.eq(D.department_id, dep_id))
                return D;
        }
        return null;
    }

    /// Pick X amount of random exports to boost in price for the tick
    // tg: code/controllers/subsystem/economy.dm:135
    public virtual void update_boosted_exports()
    {
        boosted_exports.Cut();
        // Don't randomize export values during unit tests or we'll get flakies (tg: #ifndef UNIT_TESTS, a production build runs it)
        DmList valid_exports = valid_typesof(typeof(datum_export)) as DmList;
        for (float i = 1; i <= rand(EXPORT_BOOST_MIN_AMOUNT, EXPORT_BOOST_MAX_AMOUNT); i++)
            Dm.append(ref boosted_exports, pick_n_take(valid_exports));
    }

    /**
     * Departmental income payments are kept static and linear for every department, and paid out once every 5 minutes, as determined by MAX_GRANT_DPT.
     * Iterates over every department account for the same payment.
     */
    // tg: code/controllers/subsystem/economy.dm:148
    public virtual object departmental_payouts()
    {
        // son sonic speed? cache? hot over in cold food why? (datum var accesses are slow, cache lists for sonic speed)
        DmList cached_processing = this.cached_processing;
        for (int i = 1; i <= length(cached_processing); i++)
        {
            datum_bank_account dept_account = get_dep_account(cached_processing[i]);
            if (dept_account == null)
                continue;
            dept_account.adjust_money(MAX_GRANT_DPT);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
            {
                cached_processing.Cut(1, i + 1);
                return FALSE;
            }
        }
        return TRUE;
    }

    /**
     * Issues all our bank-accounts paydays, and gets an idea of how much money is in circulation
     */
    // tg: code/controllers/subsystem/economy.dm:164
    public virtual async DmTask<object> issue_paydays()
    {
        DmList cached_processing = this.cached_processing;
        for (int i = 1; i <= length(cached_processing); i++)
        {
            var bank_account = cached_processing[cached_processing[i]] as datum_bank_account;
            if (bank_account?.account_job != null)
                temporary_total += Dm.num(bank_account.account_job.paycheck) * STARTING_PAYCHECKS;
            await bank_account.payday(1, skippable: TRUE);
            station_total += bank_account.account_balance;
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
            {
                cached_processing.Cut(1, i + 1);
                return FALSE;
            }
        }
        return TRUE;
    }

    /**
     * Updates the the inflation_value, effecting newscaster alerts and the mail system.
     **/
    // tg: code/controllers/subsystem/economy.dm:180
    public virtual async DmTask<object> price_update()
    {
        string fluff_string = "";
        if (!HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING))
            fluff_string = I18n.T(", but company countermeasures protect <b>YOU</b> from being affected!");
        else
            fluff_string = I18n.T(", and company countermeasures are failing to protect <b>YOU</b> from being affected. We're all doomed!");
        earning_report = I18n.T("<b>Sector Economic Report</b><br><br> Sector vendor prices is currently at <b>[]%</b>[]<br><br> The station spending power is currently <b>[] []</b>, and the crew's targeted allowance is at <b>[] []</b>.<br><br>[]",
            SSeconomy.inflation_value__proc() * 100, fluff_string, station_total, MONEY_NAME_CAPITALIZED, station_target, MONEY_NAME_CAPITALIZED, SSstock_market.news_string);
        bool update_alerts = false;
        if (HAS_TRAIT(SSstation, STATION_TRAIT_ECONOMY_ALERTS) && (living_player_count() > 1))
        {
            datum_bank_account moneybags = null;
            DmList typecache_bank = price_update__typecache_bank ??= typecacheof(Dm.list(typeof(datum_bank_account_department), typeof(datum_bank_account_remote))) as DmList;
            foreach (object i in Dm.each(bank_accounts_by_id))
            {
                var current_acc = bank_accounts_by_id[i] as datum_bank_account;
                if (Dm.truthy(typecache_bank[current_acc.type]))
                    continue;
                if (moneybags == null || moneybags.account_balance < current_acc.account_balance)
                    moneybags = current_acc;
            }
            if (moneybags != null)
            {
                earning_report = Dm.add(earning_report, I18n.T("Our GMM Spotlight would like to alert you that <b>[]</b> is your station's most affulent crewmate! They've hit it big with [] [] saved. ", moneybags.account_holder, moneybags.account_balance, MONEY_NAME));
                update_alerts = true;
                await inflict_moneybags(moneybags);
            }
        }
        earning_report = Dm.add(earning_report, I18n.T("That's all from the <i>Nanotrasen Economist Division</i>."));
        GLOB.news_network.submit_article(earning_report, "Station Earnings Report", NEWSCASTER_STATION_ANNOUNCEMENTS, null, update_alert: update_alerts ? TRUE : FALSE); // silicons SI6b
        return TRUE;
    }

    /**
     * Proc that returns a value meant to shift inflation values in vendors, based on how much money exists on the station.
     *
     * If crew are somehow aquiring far too much money, this value will dynamically cause vendables across the station to skyrocket in price until some money is spent.
     * Additionally, civilain bounties will cost less, and cargo goodies will increase in price as well.
     * The goal here is that if you want to spend money, you'll have to get it, and the most efficient method is typically from other players.
     **/
    // tg: code/controllers/subsystem/economy.dm:212  (the var of the same name keeps it, rules 2.4)
    public virtual float inflation_value__proc()
    {
        if (!(bank_accounts_by_id.len > 0))
            return 1;
        if (HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING))
            return inflation_value; //early return instead of the actual check
        inflation_value = max(round(((station_total / bank_accounts_by_id.len) / station_target), 0.1f), 1.0f);
        return inflation_value;
    }

    /**
     * Proc that adds a set of strings and ints to the audit log, tracked by the economy SS.
     *
     * * account: The bank account of the person purchasing the item.
     * * price_to_use: The cost of the purchase made for this transaction.
     * * vendor: The object or structure medium that is charging the user. For Vending machines that's the machine, for payment component that's the parent, cargo that's the crate, etc.
     */
    // tg: code/controllers/subsystem/economy.dm:227
    public virtual void add_audit_entry(datum_bank_account account = null, object price_to_use = null, object vendor = null)
    {
        if (account == null || price_to_use == null || !Dm.truthy(vendor))
            throw Dm.CRASH("Track purchases was missing an argument! (Account, Price, or Vendor.)", "code/controllers/subsystem/economy.dm", 229);

        Dm.append(ref audit_log, Dm.list(Dm.list(
            Dm.kv("account", Dm.text("[]", account.account_holder)),
            Dm.kv("cost", price_to_use),
            Dm.kv("vendor", Dm.text("[]", (vendor as atom)?.name ?? vendor)),
            Dm.kv("stationtime", round_timestamp("hh:mm")))));
    }

    /**
     * Iterates over the machines list for vending machines, resets their regular and premium product prices (Not contraband), and sends a message to the newscaster network.
     */
    // tg: code/controllers/subsystem/economy.dm:241
    public virtual void update_vending_prices()
    {
        DmList prices_to_update = Dm.list();
        // Assoc list of "z level" -> if it's on the station
        // Hack, is station z level is too expensive to do for each machine, I hate this place
        DmList station_z_status = Dm.list();
        foreach (obj_machinery_vending vending_lad in Dm.each_cast<obj_machinery_vending>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_vending)) as DmList))
        {
            if (vending_lad is obj_machinery_vending_custom)
                continue;
            float vending_level = vending_lad.z;
            object station_status = station_z_status[Dm.text("[]", vending_level)];
            if (station_status == null)
            {
                station_status = is_station_level(vending_level);
                station_z_status[Dm.text("[]", vending_level)] = station_status;
            }
            if (!Dm.truthy(station_status))
                continue;
            Dm.append(ref prices_to_update, vending_lad);
        }
        for (int i = 1; i <= length(prices_to_update); i++)
        {
            var vending = prices_to_update[i] as obj_machinery_vending;
            vending.reset_prices(vending.product_records, Dm.add(vending.coin_records, vending.hidden_records) as DmList);
        }
    }

    // tg: code/controllers/subsystem/economy.dm:287
    public virtual async DmTask<object> inflict_moneybags(datum_bank_account moneybags = null)
    {
        if (moneybags == null)
            return FALSE;
        mob_living card_holder = null;
        foreach (obj card in Dm.each<obj>(moneybags?.bank_cards))
        {
            if (isidcard(card))
                card_holder = recursive_loc_check(card, typeof(mob_living)) as mob_living;
        }
        if (!isliving(card_holder)) //If on a living mob
            return FALSE;
        await card_holder.adjust_timed_status_effect(wait, typeof(datum_status_effect_spotlight_light));
        return TRUE;
    }
}

public partial class obj_machinery_vending
{
    /**
     * Reassign the prices of the vending machine as a result of the inflation value, as provided by SSeconomy
     *
     * This rebuilds both /datum/data/vending_products lists for premium and standard products based on their most relevant pricing values.
     * Arguments:
     * * recordlist - the list of standard product datums in the vendor to refresh their prices.
     * * premiumlist - the list of premium product datums in the vendor to refresh their prices.
     */
    // tg: code/controllers/subsystem/economy.dm:269
    public virtual void reset_prices(DmList recordlist = null, DmList premiumlist = null)
    {
        float inflation_value = HAS_TRAIT(SSeconomy, TRAIT_MARKET_CRASHING) ? SSeconomy.inflation_value__proc() : 1;
        default_price = round(Dm.initial(this, (obj_machinery_vending p) => p.default_price) * inflation_value);
        extra_price = round(Dm.initial(this, (obj_machinery_vending p) => p.extra_price) * inflation_value);

        foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(recordlist))
        {
            Type potential_product = record.product_path as Type;
            float custom_price = round(Dm.num(Dm.initial(potential_product, (obj_item p) => p.custom_price)) * inflation_value);
            record.price = Dm.bitor(custom_price, default_price);
        }
        foreach (datum_data_vending_product premium_record in Dm.each_cast<datum_data_vending_product>(premiumlist))
        {
            Type potential_product = premium_record.product_path as Type;
            float premium_custom_price = round(Dm.num(Dm.initial(potential_product, (obj_item p) => p.custom_premium_price)) * inflation_value);
            object custom_price = Dm.initial(potential_product, (obj_item p) => p.custom_price);
            if (!Dm.truthy(premium_custom_price) && Dm.truthy(custom_price)) //For some ungodly reason, some premium only items only have a custom_price
                premium_record.price = extra_price + round(Dm.num(custom_price) * inflation_value);
            else
                premium_record.price = Dm.truthy(premium_custom_price) ? premium_custom_price : extra_price;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/economy.dm:1  SUBSYSTEM_DEF(economy): GLOBAL_REAL(SSeconomy, ...)
    internal datum_controller_subsystem_economy SSeconomy;

    // tg: code/controllers/subsystem/economy.dm:190  var/static/list/typecache_bank (a proc-level static, rules 10.6)
    internal DmList datum_controller_subsystem_economy__price_update__typecache_bank;
}

public partial class datum_controller_subsystem_economy
{
    private static ref DmList price_update__typecache_bank => ref TgState.Current.datum_controller_subsystem_economy__price_update__typecache_bank;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_economy SSeconomy => ref TgState.Current.SSeconomy;
}
